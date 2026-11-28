using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleManager partial —— Boss 关进场 boss 线 + 三选一（2026-11-11 主人拍板
/// 「章节线要加上 boss 线，用 boss 线去丰富剧情」「三选一就是战前剧情之后出现的」）。
/// 主线正式关 Boss 关、章节线战前剧情之后弹：先两句 boss 线引子（BossStoryBeats，用图鉴外号），
/// 再三句「玩家的回答」选一句，选完才生成对应战意（本场加成），引导语按章收口外号。
/// 【2026-11-12 主人拍板】按钮只写三句「玩家的回答」；按本章 boss 线换词。
/// 🔴【2026-11-28 主人「把三选一给的什么 buff 加到选项的后面」】按钮文案改成
/// 「回答 + 换行 + 括号效果」（BlessingChoiceLabel，两行同 26 号、效果棕色）—— 玩家选之前就知道能拿到什么。
/// 原「按钮不写效果」口径作废：Boss 关前没有佣兵助阵，这条战意是唯一的外部增援，必须写明。
/// 【2026-11-12 最新拍板「效果多些变化 / 循序也要变化」】战意池每场洗牌抽 3 条
/// （出的三条与排列都变），三句回答也洗牌 → 下标不再固定绑定战意，每场都得自己看按钮。
/// 加成统一走 tempBuffs（石像赐福同款，主角+佣兵都吃）。
/// 🔴【2026-11-12 主人拍板「必须是单关的」】换关开场由 LoadStage 调 RevertBossStageBlessing
/// 撤销上一关战意（run 三件套回基线、tempBuffs 只按引用摘掉本关那一条），不写任何存档。
/// 复用 DialogueUI 选项链（与第一章石碑三选一同一条 UI），60 秒无输入看门狗自动选第一项放行，
/// 绝不把 Boss 关卡死在进场。
/// 🔴【2026-11-28 主人报「选了战斗之前的三选一没有给我 buff」】根因不是没生效，是**看不见**。
/// 处置：① 池子里 3 条关卡级修饰（迟滞/悬赏/背水）整条撤掉、全部换成属性，选了必然在 buff 条亮牌；
/// ② BuffHudUI 在「等开始战斗」阶段不再整条收起，改为提前亮出这张战意牌（主人「选完当然要挂到 buff 那」）；
/// ③ 落地 + 写牌 + toast 统一走 ApplyBossBlessing 这一个出口，别再散着调。
/// </summary>
public partial class BattleManager
{
    /// <summary>
    /// 单条 Boss 关战意：名号 + 一条属性加成。
    /// 🔴【2026-11-28 主人「boss 之前没有佣兵来助阵了，把三选一给的什么 buff 加到选项的后面」「换掉」】
    /// 池子里的「迟滞（怪攻速）/悬赏（金币）/背水（我方承伤）」三条关卡级修饰**已整条撤掉** ——
    /// 它们不进 tempBuffs，buff 条上永远没有牌，玩家选完等于零反馈（主人报的「没有给我 buff」）。
    /// 现在 6 条全部是纯属性加成，走同一条 ApplyTeamTempBuff → 必然在 buff 条上亮牌。
    /// 属性只取 BuffHudUI 已登记 buff 图的 7 种，不引入新属性、不新增图标。
    /// </summary>
    struct BossBlessing
    {
        /// <summary>名号（砺刃 / 疾风 / 疾行…），只用于日志，不上按钮。</summary>
        public string Label;
        /// <summary>加成属性。<see cref="UseJobAttack"/> 为真时忽略本字段，按职业取物攻/魔攻。</summary>
        public AttrType Attr;
        /// <summary>按职业取「物攻 / 魔攻」（砺刃专用）：战士吃物攻、法师吃魔攻，按钮文案跟着职业走。</summary>
        public bool UseJobAttack;
        public float Value;
        public bool IsPercent;
    }

    /// <summary>
    /// Boss 关战意池（6 条，全属性）。【2026-11-28 主人「换掉」把 3 条关卡级修饰换成属性】
    /// 每场 Fisher-Yates 洗牌取前 3 条：出的三条不固定、排列也不固定；三句回答同步洗牌，
    /// 所以「哪句回答 → 哪条战意」每场都不同（原「固定一一对应」口径作废）。
    /// 🔴【2026-11-28 主人「把三选一给的什么 buff 加到选项的后面」】按钮文案现在**自带效果**
    /// （回答 + 换行 + 括号效果，两行同 26 号、效果棕色），玩家选之前就知道能拿到什么，见 BlessingChoiceLabel。
    /// 【2026-11-12】「回魂：立即回满血」撤下：Boss 关进场本就满血，效果玩家无感；
    /// HealAllAlliesToFull() 保留备用（当前无调用，见文件末注释）。
    /// </summary>
    static readonly BossBlessing[] BossBlessingPool =
    {
        new BossBlessing { Label = "砺刃", UseJobAttack = true,                 Value = 0.15f, IsPercent = true },
        new BossBlessing { Label = "疾风", Attr = AttrType.AttackSpeed,         Value = 0.15f, IsPercent = true },
        new BossBlessing { Label = "铁壁", Attr = AttrType.Defense,             Value = 0.20f, IsPercent = true },
        new BossBlessing { Label = "固本", Attr = AttrType.MaxHp,               Value = 0.20f, IsPercent = true },
        new BossBlessing { Label = "凝神", Attr = AttrType.CritRate,            Value = 0.08f, IsPercent = false },
        // 【2026-11-28 新补】移速是 7 种已登记图里唯一没用上的，补进来凑第 6 条（走位手感实感明显）。
        new BossBlessing { Label = "疾行", Attr = AttrType.MoveSpeed,           Value = 0.15f, IsPercent = true },
    };

    /// <summary>战意实际作用的属性（砺刃按职业取物攻/魔攻，其余直接读 Attr）。</summary>
    static AttrType ResolveBlessingAttr(BossBlessing b)
        => b.UseJobAttack ? PlayerJobBaseStats.CurrentAttackAttr() : b.Attr;

    /// <summary>战意效果文案（「暴击 +8%」这种纯效果，不带名号）。选项按钮与 buff 牌共用同一条。</summary>
    static string BlessingEffectText(BossBlessing b)
    {
        var a = ResolveBlessingAttr(b);
        return EquipUiText.Attr(a) + " +" + EquipUiText.Value(a, b.Value, b.IsPercent);
    }

    /// <summary>
    /// 选项按钮文案 = 回答 + 换行 + 括号效果。
    /// 【2026-11-28 主人「把三选一给的什么 buff 加到选项的后面」】按钮必须自带效果，选之前就知道能拿到什么。
    /// 按钮 Label 480×82、RichText 开（DialogueUI.prefab 的 Choice_0/Label，预制体字号 24）：
    /// 🔴【2026-11-28 主人两次「buff 的字太小了 / 还是有点小」】效果行 17 → 22 → **26**，
    /// 并把回答也一起提到 26 —— 两行同号，效果只靠「棕色 + 括号」区分主次，不再靠缩字号。
    /// 依据：Unity 该字体行高 ≈ 字号本身（截图实测 24+22 两行合计约 46px），26+26 ≈ 52px，
    /// 塞进 82 高还余 15px 上下，不会顶出按钮；改大前先看这条，别把字号顶到 28（两行 56px 会贴边）。
    /// </summary>
    static string BlessingChoiceLabel(string answer, BossBlessing b)
        => "<size=26>" + answer + "</size>\n<size=26><color=#7A4318>（"
           + BlessingEffectText(b) + "）</color></size>";

    /// <summary>
    /// 本关 Boss 三选一加进 tempBuffs 的那一条（AttrBonusData 是 class，换关按引用精确摘除）。
    /// 只记这一条，绝不整份 tempBuffs.Clear() —— 石像赐福 / 局内强化卡是整局 buff，不能误删。
    /// </summary>
    AttrBonusData _bossStageTempBuff;

    /// <summary>
    /// 本关战意的展示文案（选完即写，换关 Revert 清空）。
    /// 【2026-11-28 主人「选完当然要挂到 buff 那」】BuffHudUI 在「等开始战斗」阶段就靠它提前亮牌，
    /// 所以必须在选完的同一帧写进去，不能等开打。
    /// </summary>
    string _stageBlessingLabel;

    /// <summary>本关战意牌用哪张图标（现成 7 种之一）。</summary>
    AttrType _stageBlessingIcon;

    /// <summary>
    /// 本关是否已经选过战意（选完即真，换关 Revert 清空）。
    /// 【2026-11-28 主人「选完当然要挂到 buff 那」】BuffHudUI 在「等开始战斗」阶段靠它决定
    /// 要不要提前亮出这张战意牌 —— 玩家选完立刻要能在 buff 条上看到自己拿到了什么。
    /// </summary>
    public bool HasStageBlessing => !string.IsNullOrEmpty(_stageBlessingLabel);

    /// <summary>
    /// 是否要额外补一张「本关战意」牌（属性类战意已进 tempBuffs，由 BuffHudUI 的常驻段自然亮牌，不重复）。
    /// 当前 6 条战意全是属性类 → 恒 false，保留是为了以后再加非属性战意时不至于又变成「选了看不见」。
    /// </summary>
    public bool StageBlessingNeedsBadge => HasStageBlessing && _bossStageTempBuff == null;

    /// <summary>战意牌图标（现成 7 种之一）。</summary>
    public AttrType StageBlessingIcon => _stageBlessingIcon;

    /// <summary>战意牌简介文案（「暴击 +8%」这种纯效果，不带名号）。</summary>
    public string StageBlessingLabel => _stageBlessingLabel;

    /// <summary>Fisher-Yates 洗牌（每场战意组合 / 回答排列都靠它）。</summary>
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    /// <summary>
    /// 给主角 + 全体存活佣兵挂一条本局临时属性（石像赐福 BattleStatueEventUI:272 同款口径）：
    /// 同一份 tempBuffs 列表，主角 RecalcAttr、佣兵各自 RecalcAttr；阵亡的不重算（复活链会重新带上）。
    /// 属性类战意走这里，不碰伤害公式。
    /// </summary>
    void ApplyTeamTempBuff(AttrType attr, float value, bool isPercent)
    {
        var buff = new AttrBonusData { attrType = attr, value = value, isPercent = isPercent };
        tempBuffs.Add(buff);
        _bossStageTempBuff = buff;   // 记下本关这一条，换关时按引用撤销
        if (Hero.Instance != null) Hero.Instance.RecalcAttr();
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                var m = mercs[i];
                if (m == null || m.isDead || m.attr == null) continue;
                m.RecalcAttr();
            }
        }
        BattleUI.Instance?.ForceRefreshLiveBars();
    }

    /// <summary>
    /// 【2026-11-12 主人拍板「必须是单关的」】换关开场撤销上一关 Boss 三选一战意。
    /// 由 LoadStage（BattleManager.Waves.cs）调用，所有换关路径都走它，是唯一收口。
    /// run 三件套按 StartNewRun 同款基线重套（1f + ApplyChapter1RunModifiersFromSave），
    /// 不抹玩家第一章存档选边；tempBuffs 只按引用移除本关那一条，石像赐福 / 强化卡照留。
    /// 上一关结算在 LoadStage 之前已完成，撤销不会回追上一关的金币。
    /// </summary>
    void RevertBossStageBlessing()
    {
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        runAllyDamageTakenMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        // 战意牌跟着本关一起撤（必须在下面 _bossStageTempBuff == null 的提前 return 之前清）：
        // 只要漏了这一步，buff 条上那张「本关战意」牌就会一路挂到后面每一关。
        _stageBlessingLabel = null;

        if (_bossStageTempBuff == null) return;
        // StartNewRun 可能已整份 Clear（新一局）：Remove 返回 false 就不必重算，只把引用清掉。
        if (tempBuffs.Remove(_bossStageTempBuff))
        {
            if (Hero.Instance != null) Hero.Instance.RecalcAttr();
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.isDead || m.attr == null) continue;
                    m.RecalcAttr();
                }
            }
            BattleUI.Instance?.ForceRefreshLiveBars();
        }
        _bossStageTempBuff = null;
    }

    /// <summary>是否弹 Boss 关三选一（门槛收口在这一处）。</summary>
    bool ShouldOfferBossBlessing()
    {
        if (!IsFormalMainRun) return false;                            // 副本/竞技场/裂隙/支线岛一律不弹
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return false; // 难度局不发加成
        if (Rules.Active || Rules.DirectorOwnsWaves) return false;     // 引导局有自己的节奏
        // 【2026-11-26 主人拍板】三选一只允许「每 10 关剧情」这一处；战前 Boss 三选一只留大 Boss 关（关 10）。
        // 关 5 是小 Boss（BossKind=1）不再弹 buff 三选一；关号真源 BossKindOfStage，不写死数字。
        var cm = ChapterManager.Instance;
        if (cm == null)
        {
            Debug.LogError("[BossBlessing] 取不到 ChapterManager，三选一按不弹处理");
            return false;
        }
        if (ActProgressionDefs.BossKindOfStage(cm.CurrentSubIndex0) != 2) return false;
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    /// <summary>
    /// 【2026-11-28 主人报「选了战斗之前的三选一没有给我 buff」】应用战意的**唯一出口**：
    /// ① 落地属性（走 ApplyTeamTempBuff：tempBuffs + 主角/佣兵重算）→ ② 写本关展示文案（buff 条上的战意牌读它）
    /// → ③ toast 落账。两处调用（选中 / 150 秒兜底）都走这里，保证「生效的」「按钮上写的」「buff 条上看到的」永远同一条。
    /// 文案与属性都由同一条 BossBlessing 现算（BlessingEffectText），不会再出现文案与实际对不上。
    /// </summary>
    void ApplyBossBlessing(BossBlessing b)
    {
        var attr = ResolveBlessingAttr(b);
        ApplyTeamTempBuff(attr, b.Value, b.IsPercent);

        _stageBlessingIcon = attr;
        _stageBlessingLabel = BlessingEffectText(b);

        ShowBossBlessingToast();
        Debug.Log($"[BossBlessing] 本关战意「{b.Label}」已生效：{_stageBlessingLabel}"
                  + $"（{attr}，tempBuffs 共 {tempBuffs.Count} 条）");
    }

    /// <summary>
    /// 【2026-11-12 主人「战斗前加的，要给个 toast 告诉玩家加了什么属性」】
    /// 文案统一取 ApplyBossBlessing 写好的 _stageBlessingLabel，口径与 buff 条上的战意牌完全一致。
    /// force=true 对齐石像赐福 BattleStatueEventUI:287，剧情窗/气泡还在时也不吞这条提示。
    /// 【2026-11-28】措辞由「本关加成：」改成「获得战意：」—— 主人报「没有给我 buff」，
    /// 原措辞读起来像一条说明而不是一次获得。
    /// </summary>
    void ShowBossBlessingToast()
    {
        if (string.IsNullOrEmpty(_stageBlessingLabel)) return;
        UIManager.Instance?.ShowToast("获得战意：" + _stageBlessingLabel, true);
    }

    /// <summary>弹三选一并应用所选加成。任何异常都放行开打，绝不把 Boss 关卡死在进场。</summary>
    IEnumerator CoBossStageBlessingChoice()
    {
        if (!ShouldOfferBossBlessing()) yield break;

        // 实时抽改版后 CoFirstWaveHardFallback 等 _preBattleStoryPlaying 落 false 才兜底刷怪：
        // 选卡期间置位压住；【2026-11-11】不在这里复位 —— 面板已在走进场前提前 BeginLive，
        // 压制统一由 DoStartFight（点「开始战斗」）解除，中途置 false 会让一次性闸门脱离、2.2s 后硬刷抢跑。
        // 常显抽卡面板不收：它在 Canvas(100)，三选一剧情窗在 13000，压不到选项按钮；
        // 收掉反而会让 Boss 关整场没有抽奖按钮（主人拍板「关卡一上来就显示」）。
        _preBattleStoryPlaying = true;

        // 【2026-11-12 最新拍板「效果多些变化 / 循序也要变化」】6 条池子洗牌取 3 条，
        // 组合与排列每场都不同；三句按章回答也洗牌，玩家不能靠位置背答案，每场都值得再试一次。
        var pool = new List<BossBlessing>(BossBlessingPool);
        Shuffle(pool);
        var picked = new[] { pool[0], pool[1], pool[2] };

        var answers = new List<string>(BossStoryBeats.GetBlessingAnswers(CurrentChapter));
        Shuffle(answers);

        // 🔴【2026-11-28 主人「把三选一给的什么 buff 加到选项的后面」】按钮 = 回答 + 括号效果。
        // 两份列表各自洗过，按下标一一配对本身就是随机的（哪句回答配哪条战意每场都变），
        // 所以这里只做「同下标拼文案」，不再额外洗一次。
        var choiceLabels = new string[picked.Length];
        for (int i = 0; i < choiceLabels.Length; i++)
            choiceLabels[i] = BlessingChoiceLabel(answers[i], picked[i]);

        // boss 线整段没挂背景图，剧情窗会直接浮在战斗画面上 → 铺一层常驻黑色半透底，
        // 排序 12999（Default 层最高）在剧情窗 Effects 层 13000 之下、不挡点击；选完 / 异常 / 超时统一撤掉。
        // 【2026-11-13 主人拍板】这层是战前选择唯一的背景底（50%），对话窗自带的 Dim 由 hideFullDim 关掉，
        // 不再出现首次叠两层、第二回只剩一层导致「剧情底又变透明了一些」。
        GameObject dimGo = BattleAmbienceBanner.ShowPersistent();

        bool done = false;
        try
        {
            var beats = new List<StoryBeat>();
            // 【2026-11-11 主人拍板「章节线要加上 boss 线」】两句按章的 boss 线引子先播，
            // 再进三选一；外号在引子里埋、在引导语里收。
            // 【2026-11-12】选项 = 三句玩家回答（同 Chapter1Story.PlayEnding 石碑三选一），
            // 选完才按洗牌后的下标生成战意并 toast 落账。
            // 【2026-11-28 主人「把三选一给的什么 buff 加到选项的后面」】按钮文案已带上该选项会给的效果，
            // 玩家选之前就知道能拿到什么（Boss 关前没有佣兵助阵，这条战意就是唯一的外部增援）。
            BossStoryBeats.AppendIntro(beats, CurrentChapter);
            beats.Add(
                // 与 Chapter1Story.PlayEnding 的选项 beat 同构（speaker=-1 + 左侧玩家 + choices）
                new StoryBeat
                {
                    leftName = "你",
                    leftPortraitId = StoryPortraits.Player,
                    speaker = -1,
                    text = BossStoryBeats.GetBlessingLead(CurrentChapter),
                    choices = choiceLabels
                });
            StoryDirector.Ensure().Play(beats, null, choice =>
            {
                try
                {
                    int idx = Mathf.Clamp(choice, 0, 2);
                    // 【2026-11-28】统一走 ApplyBossBlessing（落地 + 写战意牌文案 + toast 三合一），
                    // 不再散着调 Apply / ShowBossBlessingToast，避免「生效了但没痕迹」再次发生。
                    ApplyBossBlessing(picked[idx]);
                }
                finally
                {
                    // 半透底与 done 必须无论加成是否落地都收干净，不能把 Boss 关卡在暗屏里
                    BattleAmbienceBanner.HidePersistent(dimGo);
                    dimGo = null;
                    done = true;
                }
            }, hideFullDim: true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleManager] Boss 关三选一异常，跳过直接开打: " + e.Message);
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            BattleAmbienceBanner.HidePersistent(dimGo);
            dimGo = null;
            done = true;
        }

        // 看门狗兜底（StoryDirector 自带 30s 单行 / 60s 选项看门狗，这里是双保险）。
        // 【2026-11-11】加了 boss 线两句引子后最坏情况 = 2×30s + 60s = 120s，
        // 外层必须比它宽，否则点击被吞时会先强制放行、选项窗还挂在屏上。
        float guard = 0f;
        while (!done && guard < 150f)
        {
            guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        if (!done)
        {
            Debug.LogWarning("[BattleManager] Boss 关三选一 150 秒无结果，强制放行");
            StoryDirector.Ensure().StopPlaying();
            var dlg2 = DialogueUI.Instance;
            if (dlg2 != null) dlg2.Hide();
            // 兜底也走同一个出口：玩家至少拿到第一条战意，且 buff 条上一定有牌。
            ApplyBossBlessing(picked[0]);
            BattleAmbienceBanner.HidePersistent(dimGo);            dimGo = null;
        }
        // 【2026-11-11】不在此复位 _preBattleStoryPlaying：与上方置位配对，统一由 DoStartFight 解除，
        // 避免进关抽奖面板已提前开出时，硬性保险在 2.2s 后抢刷首波（Editor.log 42151 实证）。
    }

    /// <summary>
    /// 我方全体（主角+在队佣兵+场上宠物）回满血（原「回魂」加成用）。【2026-11-12】回魂撤下后
    /// 当前无调用，方法保留备用（不删现有代码）；阵亡单位不拉起。
    /// </summary>
    void HealAllAlliesToFull()
    {
        if (hero != null && hero.attr != null && hero.currentHp > 0f)
            hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                var m = mercs[i];
                if (m == null || m.attr == null || m.currentHp <= 0f) continue;
                m.currentHp = m.attr.GetAttr(AttrType.MaxHp);
            }
        }
        // 【2026-11-28 主人拍板「让加血也给宠物加」】宠物不在 GetActiveMercs，单独补满
        ForEachAlivePet(p =>
        {
            if (p.attr != null && p.currentHp > 0f) p.currentHp = p.attr.GetAttr(AttrType.MaxHp);
        });
        BattleUI.Instance?.ForceRefreshLiveBars();
    }
}
