using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局内构筑导演（肉鸽影子的落地点）：
/// 1) 进战把 <see cref="RunLoadout"/> 的技能/佣兵灌进战斗；
/// 2) 升级时弹「方向 → 三选一」（技能 / 佣兵 / 装备）；
/// 3) 技佣兵与技能全部走本局构筑，不写城镇酒馆存档；本局结束即作废，金币/天赋石照常保留。
/// 佣兵不再随关卡自动升级，只能靠抽卡升。
/// </summary>
public class RunDraftDirector : MonoBehaviour
{
    public static RunDraftDirector Instance { get; private set; }

    /// <summary>局内佣兵相对玩家脚下的生成偏移。</summary>
    const float MercSpawnOffsetX = 1.6f;

    public static RunDraftDirector Ensure(BattleManager bm)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("RunDraftDirector");
        if (bm != null) go.transform.SetParent(bm.transform, false);
        return go.AddComponent<RunDraftDirector>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 进战
    // ============================================================

    public void OnRunStart()
    {
        var job = PlayerJobDefs.GetSelected();

        // 引导局：走内存模式构筑，不读不写 PlayerPrefs，撤离后整包丢掉
        bool tutorial = BattleManager.Instance != null
            && BattleManager.Instance.Rules != null
            && BattleManager.Instance.Rules.EnableRunDraft;

        bool resumed = false;
        if (tutorial)
            RunLoadout.BeginMemoryMode(job);
        else
            resumed = RunLoadout.ResumeOrBegin(job);

        // 开局启动金：**每局只补一次**（2026-10-05 主人拍板「开始金币是每局的，不是每关的」）。
        // 续关（resumed=true）不再补。
        // 【2026-10-10 主人拍板】不再发另一套抽奖币 —— 直接进局内金币钱包（BattleManager.currentGold），
        // 「进战斗只带启动金」：城镇金币不带进战斗，HUD 上那个数就是本局能花的钱，抽一次立刻看得见在减。
        // ⚠ 2026-10-05 主人最终口径：**战斗内的都是局内的** —— 所以下面几笔都是「进得来、带不走」的
        //   局内金币（走 BattleManager.AddRunGold(n, false)，抬高本局起点 → 结算差额不含它们）。
        //   局外金币 Gold **只有**结算界面按通过关卡数发（StageGoldDefs）＋ 局内真正赚到的那部分。
        //   局内金币的全部来源（**就这三条，别处不再有第二个给币口径**）：
        //     ① 每局启动金：**正式关 200 / 引导局 270**（GrantRunStarterGold，2026-10-14 主人拍板；
        //       2026-10-23 拍板引导局也归 200）+ 初始资金天赋每级 80
        //     ② 每关通关：50 起，**每关 ×1.4 递增**，单关封顶 200（GrantStageCoins，
        //        金币本不发 —— Mode.GrantsRunCoins=false）
        //     ③ 抽中「金币」安慰奖（一抽价 × 0.5 = 40）
        //   ⚠【2026-10-10 主人拍板】抽价每抽 ×1.1 不封顶、通关产出每关 ×1.4 复利 →
        //     前期紧、后期松（单关产出第 6 关起触顶 200，之后每关都够抽 1~2 次）。
        //   ⚠【2026-10-13 主人拍板】启动金 270 → 200（「正式关的金币改成200 / 抽的太容易了」），
        //     开局只够 2 抽、第 2 关进关差一抽 —— 前期更紧是主人要的口径。
        //   ⚠【2026-10-14 主人拍板】引导局启动金保持 270，三拍照常**扣钱**、不送免费抽（旧免费额度已删）。
        //     【2026-10-23 主人拍板】抽价改 40 起、每抽 +10 后引导金也归 200（三拍 150，仍兜得住）。
        if (!resumed)
        {
            // 金币本免费抽额度每局重算（ResetStageFreeDraws，金币本 FreeEntryDraws=5）：
            // _freeDrawsUsed 跨局不清会串到下一局 → 每局进关统一清一次（续关不清，行为同从前）。
            SlotMachineSystem.ResetStageFreeDraws();
            // 【2026-10-19 主人拍板】本局抽中抽奖币的次数也每局清零（上限见 SlotMachineDefs.MAX_GOLD_PRIZE_PER_RUN）。
            SlotMachineSystem.ResetRunGoldPrizeCount();
            // 【2026-10-20 主人拍板】本局已抽中装备指纹清零（抽奖不再重复出同一把武器；续关不清）。
            DraftPool.ResetDrawnEquipTemplates();
            // 【2026-10-24 主人拍板】保底稀有计数每局清零（续关不清，同抽奖币计数口径）。
            SlotMachineSystem.ResetRunEquipWhiteStreak();
            SlotMachineSystem.GrantRunStarterGold(tutorial);
        }

        // 【2026-10-25 主人拍板】局内等级复活：每次进战斗都从存档永久账本回填等级/经验
        // （新档 playerLevel=1、引导局自然 =1，均无操作）。
        RestoreHeroProgress();
        // 【2026-10-24 主人拍板「只要打完就清」】resumed 现在只剩一种来源：中断局弹窗点了「继续」。
        // 局内金币池从中断快照接回（确认时 ConfirmResumeInterruptedRun 写进 runGold），
        // 启动金不重发（防杀进程反复刷启动金），靠这里把池子接回来；开新局走上面 !resumed 分支照发。
        if (resumed) RestoreRunGoldPool();

        RebuildPlayerSkills();
        RestoreRunMercs();
        RunLoadout.Save();

        RunSkillBarUI.Ensure();
        RunSkillBarUI.Refresh();

        if (tutorial) return;

        // 【2026-10-22 主人原话「中途意外退出没打完 → 让玩家选继续/重新开始」】
        // 从这一刻起本局标记为「战斗中」：现在退出游戏 = 中断，下次进本会弹「继续上一局 / 重新开始」。
        // 干净收尾（打一关回一次回城 / 局结束清档）会把标记清掉或连局一起丢，见 RunLoadout 三个出口。
        RunLoadout.MarkBattleBegun();

        if (resumed && RunLoadout.Data != null)
        {
            // 续关：只说「续到哪了」，不再罗列 Lv/技能数/佣兵数（2026-10-11 主人拍板：
            // 主人原话「不要每次上来都写本次构筑什么什么的」—— 那些数字玩家看一眼就忘，还挡视线）。
            GlobalToastUI.Show($"继续上一局 · 第{RunLoadout.Data.chapter}章");
        }
        // 新局：不再弹「本局构筑已生成：XX · 技能N」（同上，主人拍板去掉）。
        // 开场氛围交给进战斗那张章节卡（ChapterSplashOverlay：章节标题 + 开场正文 + 引言），
        // 已经有开场白了，别在这里再抄一份数据。
    }

    /// <summary>
    /// 进战斗回填局内等级（2026-10-25 主人拍板「升级提示 + 每职业倾向加成」复活局内等级；
    /// 2026-10-25 矫正：真源 = 存档永久账本 SaveData.playerLevel/playerExp，跨关/跨章累计）：
    /// Hero.InitNewRun 会把 level 归 1，这里从 LevelSystem（存档真源）回填 level/exp，
    /// 并把「倾向属性 × (level-1)」重放到 attr（InitNewRun 已 ResetToBase，不重放会丢成长）。
    /// 引导局新档自然 =1 → no-op。
    /// </summary>
    void RestoreHeroProgress()
    {
        var hero = Hero.Instance;
        if (hero == null) return;
        hero.pendingLevelUps = 0;
        if (!RunLoadout.IsActive || RunLoadout.Data == null) return;
        if (BattleLootMode.Active) return;
        LevelSystem.RestoreHeroLevel(hero);
    }

    /// <summary>
    /// 【2026-10-24 主人拍板「只要打完就清」】续局（= 中断局弹窗点了继续）还原局内金币池。
    /// 池子在中断局弹窗确认时从中断快照接回（RunLoadout.ConfirmResumeInterruptedRun 写进 runGold），
    /// 这里还原进 BattleManager。
    /// </summary>
    void RestoreRunGoldPool()
    {
        if (RunLoadout.Data == null) return;
        BattleManager.Instance?.RestoreRunGoldPool(RunLoadout.Data.runGold);
    }

    /// <summary>每帧检查升级队列（由 BattleManager.Update 调用）。</summary>
    public void Tick()
    {
        if (LevelUpDraftUI.IsShowing) return;

        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle) return;
        if (bm.IsTutorialRun)
        {
            // 引导关的三选一由 TutorialDirector 脚本化触发（打完精英后那一次），
            // 这里不再自然插卡；升级队列要清掉，否则引导一结束会连弹好几次。
            ClearTutorialLevelUpQueue();
            return;
        }
        if (!bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        // 传送门已开（等玩家走进去）时不再插卡，避免打断撤离节奏
        if (bm.PortalWalkMode) return;

        var hero = Hero.Instance;
        if (hero == null || hero.isDead) return;

        // 等级系统已停用：不再有升级抽卡，队列直接清零（属性成长只走城镇天赋 + 存档等级回填）
        if (hero.pendingLevelUps > 0) hero.pendingLevelUps = 0;
        return;
    }

    /// <summary>
    /// 引导局：清掉自然升级队列。这里丢的只是"要不要弹抽卡"的计数，不会让玩家少拿属性。
    /// </summary>
    static void ClearTutorialLevelUpQueue()
    {
        var hero = Hero.Instance;
        if (hero == null || hero.pendingLevelUps <= 0) return;
        hero.pendingLevelUps = 0;
    }

    // ============================================================
    // 通关：只记录进度（不再自动升级佣兵、不再额外出三选一）
    // ============================================================

    /// <summary>阶段推进时的落档：章节/关卡进度写进本局构筑，供「继续上一局」。</summary>
    public void NoteStageProgress()
    {
        if (!RunLoadout.IsActive || RunLoadout.Data == null) return;

        var ch = ChapterManager.Instance;
        if (ch != null) RunLoadout.Data.chapter = Mathf.Max(1, ch.currentChapter);

        var bm = BattleManager.Instance;
        if (bm != null && bm.currentStage != null)
            RunLoadout.Data.stageIndex = bm.currentStage.stageIndex;

        RunLoadout.Save();
        RestoreRunMercs();
        RefreshSkillPower();
    }

    // ============================================================
    // 选卡结算
    // ============================================================

    void OnCardPicked(DraftCard card)
    {
        string msg = ApplyCard(card);
        RunLoadout.Save();
        RunSkillBarUI.Refresh();
        // 2026-10-10 主人反馈：抽中佣兵的 toast 弹出时，底部头像槽 / 佣兵技能槽还是空的。
        // 根因是「先弹 toast、后刷 UI」—— 这里改成先刷底部 HUD，再弹 toast，玩家一眼能同时看到两处。
        if (BattleUI.Instance != null)
        {
            BattleUI.Instance.UpdateCharacterSlots();
            BattleUI.Instance.UpdateMercSkillSlots();
            BattleUI.Instance.UpdateSkillAvatars();
        }
        if (!string.IsNullOrEmpty(msg))
            GlobalToastUI.Show(msg, true);   // 2026-10-06 主人拍板：物品/技能/佣兵获得，force 弹出
    }

    /// <summary>
    /// 按技能 id 造一张「获得新技能」卡（教程固定卡组用，不随机）。
    /// id 无效时返回 IsValid=false 的卡，调用方自己过滤。
    /// </summary>
    public static DraftCard BuildSkillCardById(string id)
    {
        var def = PlayerSkillDefs.GetById(id);
        if (def == null || string.IsNullOrEmpty(def.id)) return default;

        var rar = SkillDraftMeta.Rarity(id);
        var tag = SkillDraftMeta.Tag(id);
        string tagName = SynergyTagUtil.DisplayName(tag);
        string prefix = string.IsNullOrEmpty(tagName) ? "" : "[" + tagName + "] ";
        string body = string.IsNullOrEmpty(def.numbers) ? def.desc : def.numbers;

        return new DraftCard
        {
            Kind = DraftCardKind.SkillNew,
            Id = id,
            Title = def.displayName,
            Desc = prefix + body,
            Rarity = rar,
            Tag = tag,
            Star = 1,
            IsAffinity = SkillDraftMeta.IsAffinity(id, PlayerJobDefs.GetSelected()),
            PowerDelta = PowerCalc.SkillPowerDelta(rar, 1)
        };
    }

    /// <summary>把一张卡的效果真正落进本局构筑（教程三选一也走这里，确保和正式局同一条链路）。</summary>
    public string ApplyCard(DraftCard card)
    {
        TryApplyCard(card, out string msg);
        return msg;
    }

    /// <summary>
    /// 同 <see cref="ApplyCard"/>，但<b>明确告诉调用方「到底生效了没有」</b>。
    ///
    /// <para>2026-10-05 补：抽奖是<b>先扣钱、后发奖</b>的，原来只有一句文案，
    /// 调用方分不清「技能槽已满 / 背包已满 / 佣兵位已满」是失败还是成功 ——
    /// 于是出现「钱扣了、东西没拿到」的漏洞。现在判据只有这一个出口，
    /// 抽奖侧拿到 false 就<b>不扣钱、不推进保底定序</b>。</para>
    ///
    /// <para><paramref name="msg"/>：成功 = 获得文案，失败 = 失败原因（可直接弹给玩家）。</para>
    /// </summary>
    public bool TryApplyCard(DraftCard card, out string msg)
    {
        msg = null;
        switch (card.Kind)
        {
            case DraftCardKind.SkillNew:
                if (!RunLoadout.TryAddSkill(card.Id)) { msg = "技能槽已满"; return false; }
                RebuildPlayerSkills();
                // 【2026-10-06 主人拍板】新获得的技能打「新」标记，点「继续」才消失。
                NewLootMarks.Mark(NewLootMarks.KindSkill, card.Id);
                msg = $"获得技能：{card.Title}";
                return true;

            case DraftCardKind.SkillUp:
                if (!RunLoadout.TryUpgradeSkill(card.Id)) { msg = $"{card.Title} 已满星"; return false; }
                RebuildPlayerSkills();
                msg = $"{card.Title} 升至 ★{card.Star}";
                return true;

            case DraftCardKind.MercRecruit:
                return RecruitMerc(card, out msg);

            case DraftCardKind.MercLevelUp:
            {
                int lv = RunLoadout.TryLevelUpMerc(card.Id);
                if (lv <= 0) { msg = "佣兵已离队"; return false; }
                RefreshMercUnit(card.Id);
                msg = $"{card.Title} 升至 Lv{lv}";
                return true;
            }

            // 【2026-10-20 主人拍板】战斗佣兵升星暂时注释：不会抽取一样的佣兵，升星卡已不进池（DraftPool.BuildWeightedPool），
            // 此分支自然不可达。恢复时连同 DraftPool 两处注释放开即可。
            //case DraftCardKind.MercStarUp:
            //    if (!RunLoadout.TryUpgradeMercStar(card.Id)) { msg = $"{card.Title} 已满星"; return false; }
            //    RefreshMercUnit(card.Id);
            //    msg = $"{card.Title} 升至 ★{card.Star}（Lv 同时 +1）";
            //    return true;

            case DraftCardKind.Equip:
                // 2026-10-05 主人拍板：装备替换必须弹窗问玩家，不能直接塞进去。
                // 弹窗是异步的，所以装备一律走 CoApplyEquipCard（协程）；
                // 这里 fail closed —— 谁在协程外偷偷调用都会立刻暴露，绝不会静默走无确认的旧路径。
                msg = "装备奖励必须走替换确认流程（CoApplyEquipCard）";
                return false;

            case DraftCardKind.Gold:
            {
                // 主人拍板「抽中的金币是当局抽奖用的」→ 进局内金币钱包（本局能继续抽），带不出裂隙。
                // 【2026-10-10 主人拍板】抽奖与启动金统一成局内金币，不再有第二套抽奖币。
                // 【2026-10-19 主人拍板】生效才计数（本局抽中次数上限的唯一记账点，
                // 见 SlotMachineDefs.MAX_GOLD_PRIZE_PER_RUN / SlotMachineSystem.GoldAllowed）。
                if (card.Amount <= 0) { msg = "抽奖币奖励数值异常"; return false; }
                var bm = BattleManager.Instance;
                if (bm == null) { msg = "抽奖币奖励发放失败：不在战斗中"; return false; }
                bm.AddRunGold(card.Amount, false);
                SlotMachineSystem.NoteGoldPrize();
                msg = $"获得抽奖币 ×{card.Amount}";
                return true;
            }

            // ⚠【2026-10-05 已删除】case DraftCardKind.Material —— 主人拍板「强化石不能抽奖得到」，
            // 强化石只剩铁匠铺 / 分解两条路。走到 default 会 LogError 报错，不会静默当成功。

            case DraftCardKind.PowerUp:
            {
                // 强化卡要写 BattleManager.tempBuffs，不在战斗中就什么都没加上 —— 算失败，别当成功吞掉
                string m = ApplyPowerUp(card.Id, card.Title);
                if (string.IsNullOrEmpty(m)) { msg = "强化未能生效（不在战斗中）"; return false; }
                msg = m;
                return true;
            }

            default:
                msg = "无法识别的奖励";
                return false;
        }
    }

    /// <summary>
    /// 装备卡的生效流程 —— **异步**，因为要先问玩家换不换。
    ///
    /// <para>2026-10-05 主人拍板：「只会抽中高级的直接替换低级的，然后低级的变成材料，
    /// 不过需要弹出个弹窗告诉玩家是否替换」。</para>
    ///
    /// 两条路都算「生效了」（玩家拿到了东西，就该扣钱）：
    /// <list type="bullet">
    /// <item>换上新的 → 新件入包，旧件由 <see cref="BattleBagSystem.TryEquipFromReward"/> 折成强化石；</item>
    /// <item>留着旧的 → 新件同样折成强化石（不占背包，也不白抽）。</item>
    /// </list>
    /// 【2026-10-14 主人拍板】同部位没有旧件时**也弹窗**（右侧栏显示「无」），不再直接入包。
    /// </summary>
    /// <param name="done">(是否生效, 给玩家看的文案)。</param>
    public IEnumerator CoApplyEquipCard(DraftCard card, System.Action<bool, string> done)
    {
        var eq = card.Equip;
        if (eq == null)
        {
            done?.Invoke(false, "装备数据缺失");
            yield break;
        }

        // 【2026-10-20 主人拍板】抽奖给出的装备记一笔：本局后续抽奖避开同模板，
        // 不再出现「两次抽出同一个武器、数值还一模一样」（见 DraftPool.NoteDrawnEquip）。
        DraftPool.NoteDrawnEquip(eq);

        var bag = BattleBagSystem.Instance;
        if (bag == null)
        {
            done?.Invoke(false, "背包系统未就绪");
            yield break;
        }

        // 谁会被顶掉 —— 判据只有 BattleBagSystem.FindSameSlotEquip 一处
        var old = bag.FindSameSlotEquip(eq);
        bool replace = true;
        // 【2026-10-14 主人拍板】抽中装备**一律弹替换确认**（原「同部位没有旧件时不弹窗、直接入包」作废）：
        // 没有旧件也要弹，右侧栏（ColumnOld）显示「无」，玩家点「换上新的」才穿；
        // 点「留着旧的」→ 新件折强化石，与有旧件时的口径一致（玩家自选自担）。
        // old == eq 防同实例重入的语义保留。
        if (old != eq)
        {
            bool chose = false;
            yield return EquipReplaceConfirmUI.CoShow(eq, old, r => chose = r);
            replace = chose;
        }

        if (replace)
        {
            // 【2026-10-06 主人报「抽到的钉锤没换到装备栏、也没换到玩家形象上」——根因修复】
            //
            // 旧写法 <c>TryEquipFromReward</c> → <c>TryAddUniqueBySlot</c> → <c>TryAcquireLoadoutItem</c>，
            // 那条链的注释白纸黑字写着「**只入包不穿槽**」：武器只落进背包格子，
            // 不写 <c>_equippedBySlot</c>（=装备栏），<c>NotifyCostumeChanged</c> 这条刷新 SPUM 的
            // 链路自然也就读不到它 —— 装备栏与玩家形象两处都纹丝不动，符合主人看到的现象。
            //
            // 真正「穿上」的出口是 <c>TryEquipDirect</c>：走 <c>EquipItem</c> → 写穿戴槽 +
            // <c>NotifyCostumeChanged()</c>（内含 <c>HeroCostumeManager.RefreshCostume</c> 刷 SPUM 时装），
            // 顺带把顶下来的旧件 <c>ScrapEquip</c> 折成局内币（2026-10-18 拆分改币，不再发材料）——
            // 正好就是主人定的「低级的变成钱」。
            if (!bag.TryEquipDirect(eq))
            {
                done?.Invoke(false, $"{NameOf(card, eq)} 穿戴失败");
                yield break;
            }
            var hero = Hero.Instance;
            if (hero != null) hero.RecalcAttr();
            // 【2026-10-06 主人拍板】换上的新装备打「新」标记，点「继续」才消失。
            NewLootMarks.Mark(NewLootMarks.KindEquip, eq.templateId);
            // 2026-10-10 主人拍板：装备到账文案统一「恭喜获得 XXX」（原「获得装备：XXX」）
            // 2026-10-12 主人拍板：装备名按稀有度上色（色值统一走 RarityPalette，别再抄）。
            // 这条 msg 同时喂三处出口：战斗内抽奖 Toast + 「等玩家点继续」硬引导文案 + 通关结算 Toast。
            done?.Invoke(true, $"恭喜获得 {ColoredName(card, eq)}");
            yield break;
        }

        // 玩家选择留着旧的：新件折成局内币（【2026-10-18 主人拍板】拆分改币，ScrapEquip → GrantScrapGold
        // 自带提示与发币；不再发 DecomposeMat）
        bag.ScrapEquip(eq);
        done?.Invoke(true, $"{NameOf(card, eq)} 已折合金币");   // 【2026-10-18 双钱包拆分】折金进「本局获得金币」
    }

    static string NameOf(DraftCard card, EquipInstance eq)
    {
        if (!string.IsNullOrEmpty(card.Title)) return card.Title;
        if (eq != null && !string.IsNullOrEmpty(eq.equipName)) return eq.equipName;
        return "装备";
    }

    /// <summary>
    /// 装备名按稀有度包富文本颜色（2026-10-12 主人：获得装备时名字颜色按稀有度来）。
    /// 色值唯一来源 = <see cref="RarityPalette.Get(Rarity)"/>；eq 缺失时退回纯名字（不猜颜色）。
    /// 出口（GlobalToastUI / TutorialHintUI）都是 UnityEngine.UI.Text，默认吃 <c>&lt;color&gt;</c> 富文本。
    /// </summary>
    static string ColoredName(DraftCard card, EquipInstance eq)
    {
        string name = NameOf(card, eq);
        if (eq == null) return name;
        Color c = RarityPalette.Get(eq.rarity);
        return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + name + "</color>";
    }

    string ApplyPowerUp(string id, string title)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return null;

        switch (id)
        {
            case "pow_hp":
                bm.tempBuffs.Add(new AttrBonusData { attrType = AttrType.MaxHp, value = 0.12f, isPercent = true });
                break;
            case "pow_spd":
                bm.tempBuffs.Add(new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.10f, isPercent = true });
                break;
            default:
                // 2026-09-29：攻击% 按职业分流（法师/牧师 → MagicAttack）
                bm.tempBuffs.Add(new AttrBonusData { attrType = PlayerJobBaseStats.CurrentAttackAttr(), value = 0.14f, isPercent = true });
                break;
        }

        var hero = Hero.Instance;
        if (hero != null) hero.RecalcAttr();
        return $"{title} 已生效";
    }

    bool RecruitMerc(DraftCard card, out string msg)
    {
        msg = null;
        var data = BuildMercData(card.Id, card.HireId, card.MercLevel, card.Star);
        if (data == null) { msg = "佣兵数据缺失"; return false; }

        var entry = new RunMercEntry
        {
            hireId = data.hireId,
            mercId = data.mercId,
            displayName = data.displayName,
            nickname = data.nickname,
            level = data.level,
            star = data.star,
            skillId = data.skillId,
            passiveSkillId = data.passiveSkillId
        };
        if (!RunLoadout.TryAddMerc(entry)) { msg = "佣兵位已满"; return false; }
        SpawnRunMerc(entry);
        // 【2026-10-24 主人拍板「钩子肯定需要」】N-1 佣兵参战计数：常规肉鸽局的佣兵都从这里入队，
        // 每次入队算一次参战（S006/S007/S008「参战 10 次」喂食口）。唯一出口见 OnMercBattled。
        AdventureLogAchievements.OnMercBattled(entry.hireId);
        // 【2026-10-07 主人拍板】抽中佣兵要**报喜**，不要干巴巴一句「佣兵加入：XXX」。
        // 主人原话：「显示恭喜抽中史诗佣兵 实力大增什么的」—— 引导第 2 抽开出来的就是这位。
        // 稀有度名字只有 SkillRarityUtil 一个真源（与 SlotMachineSystem.BuildGuaranteedMercCard 同款写法），
        // 不许在这里另写一套「Rare→稀有」的枚举映射。
        // 花名册查一次，喜报名与横幅稀有度共用同一结果（不多入口）
        bool hasRoster = MercRosterDefs.TryGetByHireId(entry.hireId, out var rd);
        string rarityName = hasRoster
            ? SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(rd.Rarity))
            : "";
        // 【2026-10-10 主人拍板】喜报只写真名（塔克），不带花名（重盾）。
        string fullName = !string.IsNullOrEmpty(entry.displayName)
            ? entry.displayName : entry.nickname;
        // 【2026-10-12 主人拍板】入队横幅：抽中瞬间从左侧划入 → 慢滑 → 快速右消失（同「再来一次」横幅）。
        // 节点是主人在 BattleUI 预制体摆的「佣兵入队」（立绘 + 名字 + 「— 加入小队 —」），代码只驱动。
        // 名字颜色按稀有度走 RarityPalette（主人：名字要和稀有度对齐）；不在战斗中时控制器自行 LogError。
        // 【2026-10-20 主人拍板】拥有状态口径（主人明确别搞错）：
        // ① 引导战斗**临时入队 ≠ 已拥有**——只进本局队伍，不写永久解锁；回城后每日登录 D1 点领取才是真正拥有；
        // ② 普通三选一**购买完成 = 已拥有**——首次抽到该佣兵：写永久解锁 + 自动补池 + 弹「获得佣兵」演出（取代横幅）；
        //    已拥有的佣兵再抽到只算本局入队，照旧横幅，不弹"第二次获得"。
        bool inTutorial = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        bool newlyOwned = false;
        if (!inTutorial)
        {
            var d = SaveSystem.Instance?.Data;
            if (d != null && d.UnlockMerc(entry.hireId))
            {
                d.EnsurePoolFilled();        // 照 MailSystem 先例：解锁后自动补全抽奖池
                SaveSystem.Instance.Save();
                newlyOwned = true;
            }
        }
        if (newlyOwned)
            MercGainCinematicUI.Show(entry.hireId, null);
        else
            MercJoinBannerUI.Show(entry.hireId, fullName,
                hasRoster ? rd.Rarity : MercRosterDefs.MercRarity.Common);
        // 【2026-06 主人拍板】佣兵技能一样要标「新」（主人点名：不管是佣兵技能还是装备）。
        NewLootMarks.Mark(NewLootMarks.KindSkill, entry.skillId);
        // 【2026-10-14 主人拍板】喜报里佣兵名字按稀有度上色（富文本包住名字，其它字不变色）。
        // 颜色走 RarityPalette 真源，与横幅名字、入队弹窗名字同一张色表，不另写一套映射。
        string coloredName = "<color=#" + ColorUtility.ToHtmlStringRGB(
            RarityPalette.Get(hasRoster ? rd.Rarity : MercRosterDefs.MercRarity.Common))
            + ">" + fullName + "</color>";
        msg = string.IsNullOrEmpty(rarityName)
            ? $"恭喜抽中佣兵·{coloredName}，实力大增！"
            : $"恭喜抽中{rarityName}佣兵·{coloredName}，实力大增！";
        return true;
    }

    static MercenaryData BuildMercData(string assetId, string hireId, int level, int star)
    {
        if (string.IsNullOrEmpty(assetId)) return null;
        if (!MercRosterDefs.TryGetByAssetId(assetId, out var def))
            return null;

        var data = new MercenaryData
        {
            mercId = string.IsNullOrEmpty(def.AssetId) ? assetId : def.AssetId,
            hireId = string.IsNullOrEmpty(hireId) ? def.HireId : hireId,
            displayName = def.Name,
            nickname = def.Nickname,
            level = Mathf.Max(1, level),
            star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5),
            favorLevel = 1,
            skillId = def.ActiveSkillId,
            passiveSkillId = def.PassiveSkillId
        };
        if (!string.IsNullOrEmpty(data.hireId) && string.IsNullOrEmpty(data.displayName))
            data.displayName = data.hireId;
        MercSkillMigrate.AlignMercenary(data);
        return data;
    }

    // ============================================================
    // 技能装配
    // ============================================================

    /// <summary>
    /// 按 RunLoadout 当前技能顺序重建 SkillSystem。
    /// 对外：V6 拖拽排序（LevelUpDraftUI / 构筑面板）改完顺序后要立刻刷新释放优先级。
    /// </summary>
    public void RebuildPlayerSkills()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return;

        var job = PlayerJobDefs.GetSelected();
        var ids = RunLoadout.SkillIds();
        var list = new List<SkillSystem.ActiveSkill>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            var sk = BuildRunSkill(ids[i], job);
            if (sk != null) list.Add(sk);
        }
        sys.SetPlayerSkills(list);
    }

    /// <summary>星级 + 流派势能 + 职业亲和 → 本局实际技能数值。</summary>
    public static SkillSystem.ActiveSkill BuildRunSkill(string skillId, PlayerJobId job)
    {
        var active = SkillRegistry.Instance != null ? SkillRegistry.Instance.GetActiveSkill(skillId) : null;
        if (active == null) return null;

        int star = RunLoadout.StarOf(skillId);
        var tag = SkillDraftMeta.Tag(skillId);
        // 【2026-10-11 主人拍板】星级伤害步长可 per-skill 覆盖（表列 starDamageStep）：
        // 天雷裁决削弱后单独定成每星 +10%，其余技能不填 → 回退全局 +18%。
        float dmgMul = SkillDraftMeta.StarDamageMul(star, active.starDamageStep) * RunLoadout.ThemeDamageMul(tag);
        // 【2026-10-21 主人拍板】升星不再减 CD —— 星级只强在伤害/增益/持续，冷却恒为 1 星值；
        // 职业亲和 ×0.85 保留（亲和是职业匹配奖励，不是升星成长）。
        float cdMul = 1f;
        if (SkillDraftMeta.IsAffinity(skillId, job))
        {
            dmgMul *= SkillDraftMeta.AffinityDamageMul;
            cdMul *= SkillDraftMeta.AffinityCooldownMul;
        }

        active.damageMultiplier *= dmgMul;
        active.baseDamage *= dmgMul;
        active.cooldown = Mathf.Max(0.6f, active.cooldown * cdMul);

        // 【2026-10-11 主人拍板】AOE 命中数随星级涨：表里填的是 **1 星** 的值，每多 1 星 +1。
        // 半径不变（aoeRadius 不参与星级）→ 变强的是「砸中几个」，不是「砸多大一片」。
        // 0 = 不限的技能不受影响（继续保持半径内全体）。
        if (active.aoeMaxTargets > 0)
            active.aoeMaxTargets += Mathf.Max(0, star - 1);

        // 治疗 / 增益也吃星级：以前只涨冷却，治疗量与增益幅度永远停在 1 星数值。
        // 治疗 = 目标最大生命 × healPercentOfMax + 施法者攻击 × healAtkMul，两段一起涨。
        float buffMul = SkillDraftMeta.StarBuffMul(star) * RunLoadout.ThemeDamageMul(tag);
        if (SkillDraftMeta.IsAffinity(skillId, job))
            buffMul *= SkillDraftMeta.AffinityDamageMul;
        active.healBase *= buffMul;
        active.healPercentOfMax *= buffMul;
        active.healAtkMul *= buffMul;
        active.buffValue *= buffMul;
        if (active.duration > 0f)
            active.duration *= SkillDraftMeta.StarDurationMul(star);
        return active;
    }

    // ============================================================
    // 佣兵装配
    // ============================================================

    void RestoreRunMercs()
    {
        var bm = BattleManager.Instance;
        var mm = MercenaryManager.Instance;
        if (bm == null || mm == null) return;

        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var e = mercs[i];
            if (e == null) continue;
            if (IsMercAlreadyOut(e)) continue;
            SpawnRunMerc(e);
        }
    }

    static bool IsMercAlreadyOut(RunMercEntry e)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || e == null) return false;
        var active = mm.GetActiveMercs();
        for (int i = 0; i < active.Count; i++)
        {
            var m = active[i];
            if (m == null) continue;
            if (!string.IsNullOrEmpty(e.hireId) && m.hireId == e.hireId) return true;
            if (m.mercId == e.mercId) return true;
        }
        return false;
    }

    static Mercenary FindOutMerc(string hireIdOrMercId)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || string.IsNullOrEmpty(hireIdOrMercId)) return null;
        var active = mm.GetActiveMercs();
        for (int i = 0; i < active.Count; i++)
        {
            var m = active[i];
            if (m == null) continue;
            if (m.hireId == hireIdOrMercId || m.mercId == hireIdOrMercId) return m;
        }
        return null;
    }

    /// <summary>抽卡升级/升星后：把场上该佣兵按新等级重算（Init 会重置满血，视为顺带补血）。</summary>
    void RefreshMercUnit(string hireIdOrMercId)
    {
        var merc = FindOutMerc(hireIdOrMercId);
        var entry = RunLoadout.FindMerc(hireIdOrMercId);
        if (merc == null || entry == null) return;

        merc.Init(entry.mercId, Mathf.Max(1, entry.level));
        merc.mercStar = Mathf.Clamp(entry.star, 1, 5);   // 2026-10-06：MP 池/回复的星级真源
        merc.SetupBattleSkills(entry.skillId, entry.passiveSkillId);
        merc.SetDisplayName(entry.displayName);
        if (!string.IsNullOrEmpty(entry.hireId)) merc.SetHireId(entry.hireId);
        if (!string.IsNullOrEmpty(entry.displayName))
            merc.gameObject.name = "Merc_" + entry.displayName;
        Debug.Log($"[RunDraft] 佣兵刷新：{entry.displayName} Lv{entry.level} ★{entry.star}");
    }

    void SpawnRunMerc(RunMercEntry e)
    {
        var bm = BattleManager.Instance;
        var mm = MercenaryManager.Instance;
        if (bm == null || mm == null || e == null || string.IsNullOrEmpty(e.mercId)) return;

        // 【2026-10-10 主人拍板】引导局：场上天降的塔克替身**直接转正**，不再"收掉 → 重新生成"
        // （主人反馈「第二次抽奖时塔克消失了但名字还在，不要让塔克消失」）。
        // 判据 = 引导局 && 场上已有同 hireId 佣兵（引导局只此一位，正式局不受影响）。
        if (bm.IsTutorialRun)
        {
            var actives = mm.GetActiveMercs();
            Mercenary standIn = null;
            for (int i = 0; i < actives.Count; i++)
            {
                var m = actives[i];
                if (m != null && !string.IsNullOrEmpty(m.hireId) && m.hireId == e.hireId) { standIn = m; break; }
            }
            if (standIn != null)
            {
                int slot0 = Mathf.Max(0, actives.Count - 1);   // 替身已占一个 active 名额
                PromoteMerc(standIn, e, slot0);
                Debug.Log($"[RunDraft] 引导替身转正：{e.displayName}({e.hireId}) 原地入队，不重新生成");
                return;
            }
        }

        var hero = bm.hero != null ? bm.hero : Hero.Instance;
        float baseX = hero != null ? UnitBase.GetCombatX(hero) + MercSpawnOffsetX : MercSpawnOffsetX;
        float z = bm.unitRoot != null ? bm.unitRoot.position.z : 0f;
        int slot = mm.GetActiveMercs().Count;
        if (hero != null)
            baseX = UnitCrowd.GetMercDesiredCombatX(hero, null, slot);

        var merc = mm.SpawnMercenary(e.mercId, new Vector3(baseX, UnitBase.GROUND_Y, z), Mathf.Max(1, e.level));
        if (merc == null) return;

        merc.mercStar = Mathf.Clamp(e.star, 1, 5);       // 2026-10-06：MP 池/回复的星级真源

        // 2026-10-06 主人拍板：阵亡过的佣兵**带惩罚复活**（血 20% 起步，每通过一关 +30%）。
        // SpawnMercenary → Init 会重置满血，所以惩罚必须在这里补砍回去。
        float reviveRatio = RunLoadout.MercReviveHpRatio(e);
        if (reviveRatio < 1f && merc.attr != null)
        {
            float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
            merc.currentHp = Mathf.Max(1f, maxHp * reviveRatio);
            Debug.Log($"[RunDraft] 佣兵 {e.displayName} 阵亡复活：血量 {reviveRatio * 100f:0.}%（蓝条照常灌满）");
        }

        merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
        merc.SetPartyIndex(slot);
        merc.SetDisplayName(e.displayName);
        if (!string.IsNullOrEmpty(e.hireId))
            merc.SetHireId(e.hireId);
        if (!string.IsNullOrEmpty(e.displayName))
            merc.gameObject.name = "Merc_" + e.displayName;

        if (!bm.allyUnits.Contains(merc))
            bm.allyUnits.Add(merc);
        merc.OnDead += bm.OnMercenaryDead;
        merc.Face(1);

        Debug.Log($"[RunDraft] 局内佣兵出场：{e.displayName}({e.mercId}) Lv{e.level} ★{e.star}");
    }

    /// <summary>
    /// 引导局专用：把天降演出替身**原地转正**为正式入队佣兵（2026-10-10 主人拍板「不要让塔克消失」）。
    /// 只重绑局内数据（星级/技能/站位/名字），不重新生成实例 —— 全程塔克不消失。
    /// </summary>
    void PromoteMerc(Mercenary merc, RunMercEntry e, int slot)
    {
        var bm = BattleManager.Instance;
        if (merc == null || e == null) return;
        merc.mercStar = Mathf.Clamp(e.star, 1, 5);
        merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
        merc.SetPartyIndex(slot);
        merc.SetDisplayName(e.displayName);
        if (!string.IsNullOrEmpty(e.hireId))
            merc.SetHireId(e.hireId);
        if (!string.IsNullOrEmpty(e.displayName))
            merc.gameObject.name = "Merc_" + e.displayName;
        if (bm != null)
        {
            if (!bm.allyUnits.Contains(merc))
                bm.allyUnits.Add(merc);
            merc.OnDead -= bm.OnMercenaryDead;   // 替身阶段已挂过一次，先摘再挂，防重复订阅
            merc.OnDead += bm.OnMercenaryDead;
        }
        merc.Face(1);
    }

    // ============================================================
    // 战力可视化
    // ============================================================

    public static void RefreshSkillPower()
    {
        RunSkillBarUI.Refresh();
    }
}
