using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Battle.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：引导战斗主流程 —— BattleRoutine、战斗 Notify/运行时重置、台词工具、刷怪步进、点佣兵头像步

public partial class TutorialDirector
{
    /// <summary>P3 用：本段是否已经观察到玩家技能真的放出去了。</summary>
    bool _sawPlayerSkillCast;

    /// <summary>引导战重进时重置运行时状态（背包另由 StoryProgress 清）。</summary>
    public void ResetBattleTutorialRuntime()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }
        _battleTutorialFlowStarted = false;
        // 2026-10-06 主人拍板：ShowMercHud 改成只读推导（SlotMachineSystem.TutorialMercDrawn），
        // 这里的赋值删掉 —— 抽数每局归零，等于自动重置。
        WaitingEvacuate = false;
        SkillUsedThisStep = false;
        AllowBattleSkillClick = false;
        _sawPlayerSkillCast = false;
    }

    /// <summary>已废弃：引导改为打开冒险页再开战，不再从底栏直进战斗。</summary>
    public bool TryEnterTutorialBattleFromNav() => false;

    public void NotifyBattleSplashFinished()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.IsTutorialRun) return;
        // 过场若重复回调，不要把进行中的引导协程掐断（会留下气泡/卡流程）
        if (_battleTutorialFlowStarted && _flow != null) return;
        if (_flow != null) StopCoroutine(_flow);
        _battleTutorialFlowStarted = true;
        _flow = StartCoroutine(BattleRoutine());
    }

    public void NotifyPlayerSkillUsed()
    {
        SkillUsedThisStep = true;
        _sawPlayerSkillCast = true;
    }

    IEnumerator BattleRoutine()
    {
        var bm = BattleManager.Instance;
        var hint = TutorialHintUI.Ensure();
        var ui = BattleUI.Instance;
        var headTalk = BattleHeadTalkUI.Ensure();
        SkillUsedThisStep = false;
        AllowBattleSkillClick = false;
        WaitingEvacuate = false;

        // —— 0) 进关先明示启动金（2026-10-18 主人拍板；同日二次拍板：并入引导步骤播）——
        // 玩家对账懵的根因：城镇金币不带入战斗，进关发启动金（引导局 270），HUD 从 500 跳 270
        // 像「被扣了 230」；三拍扣的 80/88/97 花的又全是启动金——其实城镇存款一分没动。
        // 原来走 GlobalToastUI 飘过即逝，现在改成引导第 0 步气泡：先把第一波刷出来站好，
        // 启动金说明压 3 秒让玩家读完，再接「先打一波」——读字时不打架；怪的进场走位
        // 仍由 CoTeachControls 的 3.2 秒空档负责（「先打一波」到首波接战的相对时机不变）。
        // 停留 3 秒想调就调这一处。
        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
        yield return EnsureTutorialStep(bm, 1);
        hint.Show(
            "本局启动金 " + SlotMachineSystem.RunStarterGold(true) + " 抽奖币（自带金币不带入战斗）· 抽价每抽涨 10%，花完为止",
            null, 3f);
        yield return new WaitForSecondsRealtime(3f);

        // —— 0) 第一波进场 + 自动技能（选职已在冒险页完成）——
        // 【2026-10-09】摇杆已于 2026-10-05 整条链路停用（BattleJoystick 永不创建，见 BattleUI.Backpack.cs:386），
        //   这里只剩「借 3.2 秒读字时间让怪走进场」，不再有教走路这一步（第一波已在上一步刷）。
        //   冻结已提到上方「启动金说明」步骤开头，此处不再重复。
        // 【2026-10-12 主人拍板】「先打一波」这句**提到玩家一出场就播** ——
        //   主人原话「先打一波那个引导在玩家一出来就播」。
        //   原位置在 CoTeachControls 之后（刷怪 + 3.2 秒进场走完才弹），玩家已经在场上站了 3 秒多才看到，
        //   现在移到刷怪之前，出场即见；怪照常在下面那 3.2 秒里走上来。
        // 【2026-10-16 主人拍板】后半句去说明书腔（文案调性：有趣、流行、适合年轻人）
        hint.Show("先打一波热热身～你负责逛街，打架的事交给系统就好啦！", null, 8f);
        // 第一波已在上方「启动金说明」步骤里刷（EnsureTutorialStep 不看是否已刷过，
        // 重复调用会多排一波），这里 preloadStep 传 0，只保留 3.2 秒进场空档。
        yield return CoTeachControls(bm, hint, ui, preloadStep: 0);

        // —— 1) 首波清场后进入宝箱剧情（不再刷第二小波）——
        // 2026-09-27 主人反馈「引导到自动攻击时怪还没出来」→ **先刷怪、再弹提示**。
        // 2026-09-28 再提前：第 1 波已在开场「启动金说明」步骤里刷（见上方 EnsureTutorialStep），
        // 借读字 + 3.2 秒空档走完进场，这里不再重复刷
        // （EnsureTutorialStep 不看是否已刷过，重复调用会多排一波）。
        TutorialBattleTable.EnsureLoaded();
        // 【2026-10-08 主人拍板】第一波那句「热身」引导已于 2026-10-12 上移到 BattleRoutine 开场
        //   （玩家一出场就播，见上方 hint.Show）。文案唯一源仍是那一处，想改措辞改那里即可。
        if (bm != null) bm.UnitsCanAct = true;
        yield return WaitFieldClear();

        yield return WaitFieldClear(strict: true);
        if (bm != null && bm.GetAliveMonsterCount() > 0)
        {
            hint.Show("还有几只漏网之鱼，补个刀呀～", null, 3f); // 【2026-10-16 主人拍板】换更有味的说法
            yield return WaitFieldClear(strict: true);
        }

        // —— 1c) 第一抽：走正式入口 CoMidBattleDraft（冻场 → 弹面板 → 抽一次 → 解冻）。
        // 抽奖是主玩法，不是引导脚本 —— 这里的节拍只是「什么时候弹」，规则在 SlotMachineSystem。
        // 【2026-10-07 主人拍板】文案不剧透类别（主人原话「不要说这次抽的是佣兵，要给玩家惊喜」），
        // 三拍统一用 TutorialDrawBeatText —— 类别按 SlotMachineDefs.TutorialDrawOrder 定序。
        // 引导三拍的钱**只有开局那 240**（每拍一抽 80，正好三抽）；不再有任何中途补贴 ——
        // 2026-10-05 主人拍板「清零 + 不要总打补丁」，TUTORIAL_WAVE_BONUS_COINS 整条链路已删。
        //
        // 【2026-10-08 主人拍板】这一拍的<b>位置</b>：原来排在「打完两波（order=1 + order=2）」之后，
        //   玩家还没拿过装备、也还没体会过换装前后的差别就抽了 —— 看不出「换了装备」。
        //   现在<b>前移到第一波（order=1）清场之后</b>：先用初始武器打完一波 → 抽到装备换上 →
        //   紧接着 order=2 混编波，换装带来的变强立刻看得见。拍数不变（仍是三抽）。
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        // 2026-10-06 主人拍板：招募完立刻刷一次头像栏，否则要等下一次全量刷新才亮出来
        // （ShowMercHud 由本局抽数推导，抽数已在 CoInstantPick 里 +1）。
        ui?.UpdateCharacterSlots();

        // —— 1c+) 肉鸽口径说明（2026-10-18 主人拍板：「肉鸽玩法每次进都是从头获取」）——
        // 放在第一抽刚开完这里趁热讲：玩家刚体验过「抽 → 穿上」，构筑概念最具体的一刻。
        // 不硬等流程（下一拍就是压场波，要保住「大展身手 → 被围」的节奏），气泡 6 秒自动消失。
        hint.Show("抽到的装备、技能、伙伴都是本局构筑哟～出了战斗会清空，说不定下回的你会更强！",
            null, 6f);

        // —— 1d) 【2026-10-08 主人拍板】抽中武器以为能大展身手，结果十几只小怪压上来 ——
        // 主人原话：「抽中一个武器 以为大展身手的时候到了 结果来了十几个小怪
        //   正在玩家感觉要死了的时候 佣兵闪亮登场」。
        // 这一波（tutorial_battle.csv order=7）只给**数量**压迫：12 只、单体血 20~24
        // 比主流波 38~40 低，换装后一刀一只也清不完一轮，被围住自然就「要死了」。
        yield return EnsureTutorialStep(bm, 7);
        // 【2026-10-09 主人拍板】这里**不等清场** —— 等的是「被围住快撑不住」这个时刻：
        // 主人要的节奏是「十几只压上来 → 玩家觉得要死了 → 这时候塔克才登场」，
        // 真等玩家把 12 只都打完（WaitFieldClear），塔克就变成「清完场才姗姗来迟」，整段演出废掉。
        // ① 先等 12 只全部进场站定（波次是异步进场的，不能怪还没露面就开始吐槽）；
        yield return WaitMonstersFinishedEnter(bm);
        // ② 【2026-10-14 主人拍板·恢复压力等待】「让玩家的血再掉少点再出现剧情」（主人原话
        //    「不是打的太狠 是打的再狠点」）—— 怪围殴把玩家血打到阈值才进躺尸剧情，
        //    「要死了」的感觉才够。10-12 曾拍板「不用打直接弹剧情」作废；
        //    阈值/超时真源在 CoWaitAmbushPressure，要调只调那里。
        //    【2026-10-16 主人拍板】阈值 20% → 40%（原话「把玩家打到40%的血塔克出现」）。
        yield return CoWaitAmbushPressure(bm);

        // —— 1e)【2026-10-09 主人拍板】第 4~9 步：十几只压上来 → 玩家躺平 → 塔克天降一趟清场 → 玩家看傻 ——
        // 主人 10-08 口述：抽到武器以为能大展身手，结果十几只压上来；正当玩家觉得要死了的时候，佣兵闪亮登场。
        // 这一段 Hero **只被冻结，绝不改写坐标**（只能等，不能推着英雄走 / 瞬移）。
        // 【2026-10-09 主人拍板删除】压场波（order=7）后面原本还接着一条 order=2 混编波拍，
        //   主人定稿的 14 步里这里是直接接塔克天降戏，中间没有「再打一波」的位置 —— 整段删掉；
        //   正常波由后面的 order=5 那一段负责。
        hint.Hide();
        if (bm != null)
        {
            bm.UnitsCanAct = false;
            HaltUnit(Hero.Instance);
        }
        // 第 4 步：吐槽一句（搞笑档：下面这两句是唯一文案源，想改措辞改这里即可）
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "我才刚握上新装备……这就结束了？", 2.4f),
            // 第 5 步：躺尸 —— Hero 没有倒地 / 躺下动画（只有死亡动画，不能用），
            // 所以这一步只用「冻结 + 自暴自弃的台词 + 停顿」表达放弃挣扎，不变动画、不碰 transform。
            new TalkLine(Hero.Instance, "算了，我躺一会儿——你们随意。", 2.4f));
        headTalk?.HideNow();
        yield return new WaitForSecondsRealtime(0.5f);

        // 第 6~8 步：塔克天降 → 丢下「躲在我后面。」→ 一趟跑完全场，直接停在怪的身后，全体一起死。
        // 这段之前必须把场上所有怪也冻住，否则它们会在塔克跑动时追着玩家改写坐标。
        if (bm != null && bm.monsters != null)
        {
            for (int i = 0; i < bm.monsters.Count; i++)
                HaltUnit(bm.monsters[i]);
        }
        Mercenary rescuer = null;
        // CoTutorialRescueBeat 内部：① 抛物线跳进来（原样保留）② 落地丢一句「躲在我后面。」
        // ③ 逛一圈扫完直接统一判死（2026-10-12 起不再回原位，见方法内注释）。
        // ⚠ 第 7 步「躲在我后面。」放在清场**之前**（写在 CoTutorialRescueBeat 里、落地之后、起跑之前）：
        //   主人定稿的顺序是「天降 → 丢话 → 清场」，先护住玩家再动手才顺 —— 所以这里**不再**重复说第二遍。
        yield return CoTutorialRescueBeat(bm, m => rescuer = m, headTalk);

        // 第 9 步：玩家惊呆（这一句是唯一文案源，想改措辞改这里即可）
        // 【2026-10-14 主人补拍板】演出已改成「幻影一击全清」（不再挨个打），旧词
        //「他刚才是不是把所有人打了一遍？」与画面矛盾 —— 换成没看清出手的落差感。
        // 【2026-10-16 主人拍板】换成「我勒个豆」流行梗，主打崇拜+惊讶。
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "我勒个豆？！", 2.4f));
        headTalk?.HideNow();

        // —— 3b) 第二抽：抽中的就是刚才救场那位（塔克·重盾，稀有） ——
        // 【2026-10-07 主人拍板】天降那位 = 这一抽开出来的佣兵，抽完他正式入队。
        // ① 两波后（装备）→ ② 这里（按定序出佣兵）→ ③ 精英波清完（技能）。
        // 保底卡在 SlotMachineSystem.BuildGuaranteedMercCard（碎片那条口径已作废）。
        hint.Hide();
        // 【2026-10-10 主人拍板】天降替身**不再收掉** —— 主人原话口径「第二次抽奖时塔克消失了但名字还在，
        // 不要让塔克消失」。现在替身原地保留，第二抽招募时由 RunDraftDirector.SpawnRunMerc 把他**直接转正**
        //（复用同一个实例重绑属性），全程塔克不消失、也不会出现两个塔克。
        // if (rescuer != null) MercenaryManager.Instance?.DespawnMercenary(rescuer);
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        // 【2026-10-14 主人报「第二次抽奖继续后玩家和佣兵都不动，按说应该继续往前走才对」】
        // 根因：救援戏（本方法上方 :538-542）把 UnitsCanAct 冻在 false 后再没恢复；第二抽的
        // CoMidBattleDraft finally 还原的是它进入时的 false → 点完「继续」人/佣兵/怪全冻着，
        // 只能干等 WaitMonstersFinishedEnter 10s 超时（Editor.log:73601「等待怪进场超时」实锤）。
        // 与第三抽后的既有处理（本方法下方 CoMidBattleDraft 之后的同款一行）一致：抽完立刻恢复战斗。
        if (bm != null) bm.UnitsCanAct = true;
        ui?.UpdateCharacterSlots();

        // —— 3c)【2026-10-15 主人拍板】引导新一步：点佣兵头像看新伙伴 ——
        // 获得佣兵弹窗已不随抽奖默认弹出（改点头像弹，见 BattleUI.OnMercAvatarClicked），
        // 这里高亮佣兵头像教玩家点一次：点了弹详情，关掉后继续；60s 没点也放行不卡流程。
        yield return CoWaitMercAvatarTap(hint, ui);
        hint.Hide();
        // 【2026-10-16 主人拍板】「组队后佣兵会自动战斗」这句从精英波开头**提前到塔克入队后**——
        // 主人原话「佣兵进度后会自动那句有点靠后了 应该在塔克入队后就说」。点完头像这一步
        // （入队教学拍）收尾即说，别再压到精英波预告之后。
        hint.Show("组队之后，佣兵会自动帮你打架哟～", null, 3f);

        // —— 4) 第三波：塔克已在队，跟着一起打 ——
        // 2026-10-07 主人拍板：塔克第 2 抽就招募入队了，原来这段
        //「牧师在前方眩晕被围殴 → 清场 → 三段对白 → 入队」**整段删除**，第 4 拍改成普通波次。
        hint.Hide();
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        // 塔克已经在队里了：这里只从佣兵管理器取场上那个单位（精英宿敌对白 / 压轴 / 撤离要用）
        var activeMercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        // 【2026-10-15 主人拍板】「跟紧我，别走散」必须**塔克**说：按引导佣兵 hireId（H003）
        // 从在场佣兵里点名，不再按槽位 [0] 取（防以后在场顺序变化把台词安到别人头上）。
        // 找不到 hireId 精确命中时退第一只（引导局在场的本来就是塔克）；一只都没有 → 报出来，台词由 TalkBlock 跳过。
        var merc = (Mercenary)null;
        if (activeMercs != null)
        {
            string tutHireId = StoryProgress.TutorialMercHireId;
            for (int i = 0; i < activeMercs.Count; i++)
            {
                var m0 = activeMercs[i];
                if (m0 == null) continue;
                if (merc == null) merc = m0;
                if (!string.IsNullOrEmpty(tutHireId) && m0.hireId == tutHireId) { merc = m0; break; }
            }
        }
        if (merc == null)
            Debug.LogError("[Tutorial] 「跟紧我，别走散」找不到在场塔克（引导佣兵没在场），台词会被跳过");

        // 【2026-10-07 主人拍板「数据表最好不要为了引导改动」】
        // 这一拍直接用表里现成的 **order=5 普通波**（4 只，含 1 远程），一个数字都不改；
        // 表上含 1 精英的 order=4 留给后面的宿敌戏（见下）。
        bm?.QueueTutorialStep(5);

        // 【2026-10-12 主人报「第三波还是看不到敌人就弹对话框」】先等这波怪进场站定再弹提示
        // （波次异步入场，原来 Queue 完立刻 hint.Show，玩家看到对话框时场上还没怪）。
        yield return WaitMonstersFinishedEnter(bm);

        // 【2026-10-14 主人拍板·方案 A】「跟紧我，别走散。」从「整波清完之后」**提到这里** ——
        //   原位置排在 WaitFieldClear + 30s 清场守卫之后，玩家得先把这一波怪全打完才看到这句，
        //   主人原话「打完之后才弹剧情跟紧我」。现在怪一进场站定就说。
        //   【2026-10-14 二次修正】解冻已提前到第二抽 CoMidBattleDraft 结束（本方法上方新增的那行）：
        //   主人报「继续后玩家和佣兵都不动，按说应该继续往前走」。现在点完继续人就恢复战斗，
        //   怪也能正常进场（不再干等 10s 超时）；本对话播的时候 TalkBlock 自行短暂冻结、播完即恢复。
        // 【2026-10-07 主人反馈「佣兵说的话太快了，没看到」】停留 0.75 → 1.9。
        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "跟紧我，别走散。", 1.9f));

        // 不冻结战斗：玩家可随时上前清怪。
        // 2026-09-18：这里原本会直接改写 Hero 坐标把玩家往前推（最高 18/秒），
        // 触发瞬间看起来就是「被瞬移」。只等待，绝不动画家坐标。
        if (bm != null) bm.UnitsCanAct = true;
        // 【2026-10-17 主人拍板】敌人按距离打所有我方单位（玩家/佣兵），不再全程锁英雄。
        // 波刷出时已带 hero 锁（flank 波自动 target=hero），战斗恢复前解锁，塔克也会被怪打。
        bm.ClearMonsterForcedTargets();
        // 【2026-10-16 主人拍板】删与开场重复的「自动攻击」教学，顺接塔克「跟紧我」（第三波已组队）
        hint.Show("又来一波！这回有塔克搭把手，凑近就开打呀～", null, 5f);
        yield return WaitFieldClear(strict: true);
        // 清场兜底：WaitFieldClear 靠「存活数 + 未刷出波次」连读 0.45s 判定已清，
        // 遇到刷怪空窗 / 存活数同帧缓存会提前返回。
        // 这里再按现成的存活怪计数确认一次，确保场上真的清空了才继续（上限 30s，不会卡死流程）。
        float clearGuard = 0f;
        while (bm != null && clearGuard < 30f
               && (bm.GetAliveMonsterCount() > 0 || bm.HasPendingWaves))
        {
            clearGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        bm.ClearMonsterForcedTargets();

        AllowBattleSkillClick = false;

        // 【2026-10-14 主人拍板·方案 A】「跟紧我，别走散。」已提到上面 WaitMonstersFinishedEnter 之后，
        //   这里不再重复播 —— 原位置在清场守卫之后，玩家要打完一整波才看到这句话。
        headTalk?.HideNow();
        hint.Hide();

        // —— 第三次抽（技能）：走正式入口 CoMidBattleDraft ——
        // 2026-10-05 主人拍板「技能就是初始带的那个，只不过这回是抽奖给的」。
        // 【2026-10-11 主人拍板·口径已改】不再有「本局第一次抽到技能 = 白送职业初始技能」的保底
        //（原 SlotMachineSystem.BuildGuaranteedSkillCard 已删）—— 引导这一抽同样是**随机池**，
        // 抽到什么就是什么，全职业一个概率。
        // ⚠ 原来这里的「固定三选一 + 拖拽定释放顺序」教学拍（CoTutorialSkillDraft）已被这一抽顶掉并删除。
        //   拖拽教学改由 SkillOrderGuide 做软引导：玩家凑够 2 个技能时弹一条会自己消失的气泡，不挡操作。
        // 【2026-10-12 主人拍板「每次引导战力一样」】引导局这一抽**固定发「治愈之泉」**
        //   （SlotMachineDefs.TutorialFixedSkillId，见 DraftPool.CollectNewSkills 引导分支）；
        //   正式关仍是随机池，全职业一个概率。
        // 第三抽的钱来自开局 240 的最后 80（240 − 80×3 = 0），中途没有任何补贴。
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        if (bm != null) bm.UnitsCanAct = true;

        // —— 5) 精英宿敌：认出塔克 → 放狠话 → 打到残血留遗言 ——
        // 【2026-10-07 主人拍板】精英和塔克有过节：出场点名挑衅，被塔克打死前留一句
        //「我不过是个小杂鱼……你前面的路将会是一片黑暗」，然后塔克「……」「走吧」收尾。
        // 用表里现成的 **order=4**（本来就是「含 1 精英」那波，HP 档 55~65），不新增、不改数值。
        var stepElite = TutorialBattleTable.GetStepOrDefault(4);
        if (stepElite.eliteCount > 0)
        {
            // 【2026-10-07 主人反馈「玩家说的 boss 那句话也没看到」】先让玩家自己喊一句，
            // 再砸「精英来袭」预告 —— 两句之间要能看清字，停留一律 ≥1.9s。
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(Hero.Instance, "好家伙，来了个更大的？！", 1.9f)); // 【2026-10-16 主人拍板】换流行语气（「我勒个豆」已给惊呆句，这里避开不撞）
            // 2026-10-09 主人拍板：Boss 大图 wave_boss_incoming 本来就写着「首领来袭」，
            // 这里再传副标题 = 上面图片 + 下面文字两个提示（主人口中的"上下 2 个首领来袭"）。
            // 去掉文字，只留大图；Boss 波一律不叠副标题（与 BattleManager 2026-10-07 口径一致）。
            yield return BattleWaveAnnounceUI.CoPlay(BattleWaveAnnounceUI.Kind.Boss);
        }
        // 2026-10-09 主人拍板：order=4 是 around 波，QueueTutorialStep 里 around 缺 forcedTarget
        // 会直接 return（"跳过以免围空点"）→ 这波根本没排上，场上没有精英，主人才"没见到精英怪就结束了"。
        // 这里补上围杀目标（英雄），让这波真正刷出、含 1 只放大精英（eliteCount=1）。
        // 【2026-10-17 主人拍板】forcedTarget 只当 around 波的定位锚点用，刷出后立即解锁 ——
        // 精英和小怪都按距离打玩家/塔克，不再全程锁英雄。
        yield return EnsureTutorialStep(bm, 4, forcedTarget: Hero.Instance);
        bm.ClearMonsterForcedTargets();
        if (bm != null) bm.UnitsCanAct = true;
        // 【2026-10-16】「组队后佣兵会自动战斗」已提前到塔克入队后（本方法上方二抽头像步收尾处），
        // 这里不再重复 Show —— 原位置压在精英波预告之后，主人报「有点靠后了」。

        Monster rival = null;
        yield return CoFindWaveElite(bm, m => rival = m);

        if (rival != null && !rival.isDead)
        {
            // 【2026-10-07 主人反馈「说的话太快了，没看到」】气泡停留一律往 2 秒以上给。
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(rival, "老大交代过：小心那个扛盾的。说的就是你吧？", 2.4f),
                new TalkLine(rival, "我看也没什么了不起——让我试试你的实力。", 2.4f));
            if (bm != null) bm.UnitsCanAct = true;
        }
        else
        {
            Debug.LogError("[Tutorial] 精英波没找到精英单位（order=4 的 eliteCount 是不是写 0 了？），宿敌对白跳过");
        }

        // —— V6 P3：这一波充满能量，让玩家看见技能自动放出去 ——
        yield return CoWatchPlayerSkill(bm, hint);

        if (rival != null)
        {
            yield return CoEliteLastWords(bm, headTalk, rival);
            // 【2026-10-14 主人拍板】遗言播完**立即播死亡动画**，塔克不再冲上来补一刀 ——
            // 原「塔克收头」演出（CoTuckerFinishElite：冻场 → 冲身侧 → 挥剑+闪电 → 判死）整段删除。
            // 现在遗言说完直接判死，精英走自己的死亡动画。
            if (rival != null && !rival.isDead)
            {
                bool canActBefore = bm.UnitsCanAct;
                bm.UnitsCanAct = false;
                rival.TakeDamage(TutorialRescueDamage, false, true, true, 0, merc, suppressDamageText: true);
                Debug.Log("[Tutorial] 精英宿敌：遗言完直接判死（不演塔克收头）");
                bm.UnitsCanAct = canActBefore;
            }
        }

        yield return WaitFieldClear(strict: true);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "……", 1.8f),
            new TalkLine(merc, "走吧。", 2.0f));
        headTalk?.HideNow();
        hint.Hide();

        // —— 14)【2026-10-15 主人拍板】引导结尾也走传送门：「所有打完一关后都出现传送门，
        // 进传送门的步骤」照旧（玩家先走、佣兵隔一个身位跟着进，各播一次传送特效）。
        // 全员进完经 BattleManager 的引导分支接撤离结算（BattleSettlement → 回城）。
        // 【2026-10-09 旧口径】塔克说完「走吧。」直接回城结算、没有门——已被上面 10-15 拍板覆盖。
        // 这里先停一下让最后两句台词读完，再开门。
        yield return new WaitForSecondsRealtime(0.6f);
        if (bm != null)
            bm.BeginTutorialPortalExit();
        else
            Debug.LogError("[Tutorial] 引导收尾失败：BattleManager 为空，无法触发传送门出口");

        _battleTutorialFlowStarted = false;
        _flow = null;
    }

    // ============================================================
    // 【2026-10-15 主人拍板】引导新一步：点佣兵头像看新伙伴
    // 获得佣兵弹窗改为点头像弹出（不再抽中默认弹，见 BattleUI.OnMercAvatarClicked /
    // MercObtainPopupUI.ShowFromEntry）。引导局在二抽（塔克入队）后高亮佣兵头像，
    // 等玩家点一次；BattleUI.OnMercAvatarClicked 点到人后回调 NotifyMercAvatarTapped()。
    // 60s 没点也放行（与引导既有超时口径一致），绝不卡死流程。
    // ============================================================
    bool _mercAvatarTapped;

    /// <summary>BattleUI.OnMercAvatarClicked 点佣兵头像后回调（引导这一步等的就是这一下）。</summary>
    public void NotifyMercAvatarTapped() => _mercAvatarTapped = true;

    IEnumerator CoWaitMercAvatarTap(TutorialHintUI hint, BattleUI ui, float timeoutSec = 60f)
    {
        _mercAvatarTapped = false;
        var slotRt = (ui != null && ui.mercSlot1 != null && ui.mercSlot1.root != null)
            ? ui.mercSlot1.root.transform as RectTransform : null;
        hint.ShowHard("点一点新伙伴的头像，看看他的底细吧～", slotRt);
        float t = 0f;
        while (!_mercAvatarTapped && t < timeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!_mercAvatarTapped)
            Debug.LogWarning("[Tutorial] 等玩家点佣兵头像超时（60s），放行继续流程");
    }

    static void HaltUnit(UnitBase u)
    {
        if (u == null || u.rb == null) return;
        u.rb.velocity = Vector2.zero;
    }

    /// <summary>放技能的点击目标：优先玩家头像槽，退回技能头像按钮。</summary>
    static RectTransform ResolvePlayerSkillTarget(BattleUI ui)
    {
        if (ui == null) return null;
        if (ui.playerSlot != null && ui.playerSlot.root != null
            && ui.playerSlot.root.activeInHierarchy)
            return ui.playerSlot.root.GetComponent<RectTransform>();
        if (ui.playerSkillAvatar != null && ui.playerSkillAvatar.root != null
            && ui.playerSkillAvatar.root.activeInHierarchy)
            return ui.playerSkillAvatar.root.GetComponent<RectTransform>();
        return null;
    }

    struct TalkLine
    {
        public UnitBase speaker;
        public string text;
        public float hold;
        public TalkLine(UnitBase s, string t, float h) { speaker = s; text = t; hold = h; }
    }

    /// <summary>
    /// 一段对话只冻一次全场；台词直接 yield CoPlayLine，点一下跳字、再点跳句。
    /// 说话人缺失则跳过该句，不卡流程。
    /// restoreAct=false 时对话结束后保持冻结（入队等关键段用）。
    /// </summary>
    static IEnumerator TalkBlock(BattleManager bm, BattleHeadTalkUI talk, params TalkLine[] lines)
    {
        yield return TalkBlock(bm, talk, true, lines);
    }

    static IEnumerator TalkBlock(BattleManager bm, BattleHeadTalkUI talk, bool restoreAct, params TalkLine[] lines)
    {
        if (talk == null || lines == null || lines.Length == 0)
            yield break;

        TutorialHintUI.Instance?.Hide();

        bool prev = bm == null || bm.UnitsCanAct;
        if (bm != null) bm.UnitsCanAct = false;

        try
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.speaker == null || line.speaker.isDead || string.IsNullOrEmpty(line.text))
                    continue;
                yield return talk.CoPlayLine(line.speaker, line.text, line.hold);
            }
        }
        finally
        {
            talk.HideNow();
            if (bm != null) bm.UnitsCanAct = restoreAct && prev;
        }
    }

    /// <summary>单句兼容入口。</summary>
    static IEnumerator TalkHeld(BattleManager bm, BattleHeadTalkUI talk, UnitBase speaker,
        string content, float hold)
    {
        yield return TalkBlock(bm, talk, new TalkLine(speaker, content, hold));
    }

    /// <summary>
    /// 进战后的开场：刷第一波并借技能提示的空档让怪走进场。
    /// 【2026-10-09】原本这里还有一步「教摇杆」，摇杆已于 2026-10-05 停用，那一步不再存在。
    /// </summary>
    /// <param name="preloadStep">&gt;0 时在技能提示之前先把这一波刷出来（让怪借读字时间进场）。</param>
    static IEnumerator CoTeachControls(BattleManager bm, TutorialHintUI hint, BattleUI ui, int preloadStep = 0)
    {
        if (bm != null) bm.UnitsCanAct = true;
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // 原来这一段是「教摇杆」：建摇杆 → 显示「下方滑动移动，松手自动锁敌。」
        // → 死等玩家真的推一下（最多 15 秒）。摇杆没了，这一步绝不能留 —— 留着就是白等 15 秒。
        // var joyRoot = ui != null ? ui.transform
        //     : (BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        // BattleJoystick.EnsureOn(joyRoot);
        // BattleJoystick.Instance?.SetVisible(true);
        // RectTransform stickRt = BattleJoystick.Instance != null
        //     ? BattleJoystick.Instance.StickHighlight
        //     : null;
        // hint.Show("下方滑动移动，松手自动锁敌。", stickRt, -1f);
        // float waitJoy = 0f;
        // while (BattleJoystick.Instance == null || !BattleJoystick.Instance.IsHeld)
        // {
        //     waitJoy += Time.unscaledDeltaTime;
        //     if (waitJoy > 15f) break;
        //     yield return null;
        // }
        // hint.Hide();

        // 2026-09-28 主人反馈「第一波的敌人出来的还是晚，再早点」：
        // 把第 1 波的刷怪提到技能提示之前，利用这 3.2 秒读字时间让怪走进场，
        // 等下面开场那句提示（BattleRoutine 开头 hint.Show）出来时，怪已经站在脸上了。
        if (preloadStep > 0)
            yield return EnsureTutorialStep(bm, preloadStep);

        // 【2026-10-07 主人拍板】这句「技能自动释放 + 顺序」的提示**不再开局就弹** ——
        // 它属于「抽中技能、能调整顺序」的那一拍，出口收敛到 SkillOrderGuide.Text（唯一文案源），
        // 由 RunSkillBarUI.Refresh 在技能数凑够 2 个时弹。这里只保留原来的 3.2 秒空档：
        // 这段时间是给上面 preloadStep 刷出来的第一波怪走进场用的（主人口径「第一波要早点」），
        // 别把 yield 一起删掉，删了怪会站得更远。
        yield return new WaitForSecondsRealtime(3.2f);
        // 【2026-10-05 「不要摇杆了」→ 停用，先注释不删】
        // BattleJoystick.Instance?.ResetStickIdle();

        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
    }

    /// <summary>清场后：教程内直接刷下一波，不播正式关的波次预告（避免剩最后一只怪时卡住感）。</summary>
    static IEnumerator CoTutorialNextWave(BattleManager bm, TutorialHintUI hint, float delaySec, int order)
    {
        hint.Hide();
        yield return EnsureTutorialStep(bm, order);
    }

    /// <summary>
    /// 教程刷怪兜底：最多重试 3 次，每次等两帧看有没有真出怪。
    /// 导演只点步进号；人数/进场/HP 由 QueueTutorialStep 读表。
    /// </summary>
    static IEnumerator EnsureTutorialStep(BattleManager bm, int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        if (bm == null) yield break;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bm.QueueTutorialStep(order, anchorX, forcedTarget);
            yield return null;
            yield return null;
            if (bm.GetAliveMonsterCount() > 0)
            {
                Debug.Log($"[Tutorial] 刷怪成功 step={order} attempt={attempt + 1} alive={bm.GetAliveMonsterCount()}");
                yield break;
            }
            Debug.LogWarning($"[Tutorial] 刷怪未出怪 step={order} attempt={attempt + 1}，重试");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Debug.LogError($"[Tutorial] 连续 3 次刷怪失败 step={order}，跳过本波以免卡流程");
    }

    /// <summary>
    /// 引导三拍抽奖的统一文案（2026-10-07 主人拍板）。
    ///
    /// <para>① <b>不剧透类别</b>：原来写的是「这次是伙伴 / 这次是本命技能」，
    /// 主人原话「不是说过不要说这次抽的是佣兵吗，要给玩家惊喜」——抽到什么自己开出来才有惊喜感。</para>
    /// <para>② <b>三拍同一句</b>：文案只有这一个出口，别在三处各写一份（铁律「不多入口」）。</para>
    /// </summary>
    // 【2026-10-16 主人拍板】「奖励你抽一次」→「奖励一抽」，前半加一句夸（三拍仍同一句出口）
    // 【2026-10-18 主人拍板】引导全程换前台小姐口吻（可爱版）
    const string TutorialDrawBeatText = "清得漂亮！奖励你抽一抽～开出什么全看手气哟！";
}
