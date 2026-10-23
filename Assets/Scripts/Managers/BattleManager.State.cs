using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 本局状态 · 模式 · 难度
    public long currentGold = 0;
    /// <summary>
    /// 本局**已通关但还没发**的局外金币（StageGoldDefs 的每关固定额度）。
    /// <para><b>2026-10-05 主人拍板：「局外在结算界面才发」</b> —— 通关时只往这里累加，
    /// 不进 <c>currentGold</c>、不飘字、不动 HUD；到结算那一刻由
    /// <see cref="GrantPendingStageGold"/>（唯一出口，挂在 <see cref="PersistBattleGold"/> 里）一次性发放。
    /// 于是战斗里看到的金币就是进关时的金币，金币奖励变成结算界面上的一笔。</para>
    /// </summary>
    long _runStageGoldPending = 0;
    /// <summary>本局待发的局外金币（中断存档要写它，杀进程才不会吞掉这笔钱）。</summary>
    public long RunStageGoldPending => _runStageGoldPending;
    /// <summary>本局已通关关卡数（撤离补偿用：每关 +1 天赋石，2026-10-17 主人拍板）。</summary>
    int _runStagesCleared;
    /// <summary>开战时城镇金币快照；死亡时本局增量清零用</summary>
    long _goldAtRunStart;
    /// <summary>本局开始时的金币基线（中断存档要写它，撤离开局才知道净赚多少）</summary>
    public long GoldAtRunStart => _goldAtRunStart;
    /// <summary>
    /// 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】已写回城镇的获得金币水位。
    /// PersistBattleGold 每关都会走一次，写回的是「获得总额 − 已付水位」的增量；
    /// 这个水位保证逐关写回不重复入账。开局归零。
    /// </summary>
    long _earnedPaidGold;
    /// <summary>
    /// 本局获得的金币（顶部 HUD 真源）= 真正赚到的（任务金/宝箱/装备折金/通关结算金等，
    /// 全部记进 <see cref="BattleRunStats.GoldEarned"/>）＋ 还没发的按关结算金。
    /// 死亡 / 撤离 / 通关写回城镇的就是这个数 —— 顶部看到多少，落袋就是多少，抽奖花的是另一本账。
    /// </summary>
    public long RunEarnedGold => RunStats.GoldEarned + _runStageGoldPending;
    int _enchantAtRunStart;
    int _matsAtRunStart;
    public BattleRunStats RunStats { get; private set; } = new BattleRunStats();
    public bool isInBattle = false;
    /// <summary>
    /// 本局已收尾（撤离 / 死亡 / 回城）【2026-10-18 主人报「撤离结算下面还能点抽奖」】：
    /// 进关抽奖面板常驻期间玩家可以从暂停菜单撤离 —— isInBattle 此时还是 false（首波没刷），
    /// 不能当退场判据。撤离/死亡置位本标志，让挂起的抽奖等待协程当场退出，
    /// 不再走「开战」后续（StartPartyAdvance / 刷首波），也避免回城后旧协程串台新一局。
    /// 每局开始（BattleStartSequenceCoroutine 开头）复位。
    /// </summary>
    public bool _runClosed = false;
    /// <summary>自动战斗未开放：UI 已隐藏，战斗逻辑不读取。勿在本阶段接线 AI。</summary>
    public bool isAutoBattle = false;
    public List<AttrBonusData> tempBuffs = new List<AttrBonusData>();
    /// <summary>开战过场结束后才允许单位行动</summary>
    public bool UnitsCanAct { get; set; } = true;
    /// <summary>
    /// 剧情仍冻结战斗（UnitsCanAct=false），但允许怪物继续走进场。
    /// 仅埋伏过场等短窗口使用；对白期间必须关掉。
    /// </summary>
    public bool AllowMonsterMapEnter { get; set; }
    /// <summary>黑幕结束后队伍从左走进场（此期间播走路动画，但不战斗）</summary>
    public bool PartyIntroWalking { get; private set; }
    /// <summary>本局规则包。引导特例走 TutorialRules，勿再往核心循环加 IsTutorialRun。</summary>
    TutorialRules _rules = TutorialRules.Formal;
    public TutorialRules Rules
    {
        get => _rules ?? TutorialRules.Formal;
        private set => _rules = value ?? TutorialRules.Formal;
    }
    public bool IsTutorialRun => Rules.Active;
    /// <summary>
    /// 【引导节拍】本局是不是「每拍只抽一次」的引导局。
    /// <para><b>2026-10-05 主人拍板：「引导的时候每次只能抽一次，然后还是按那个顺序引导」</b>
    /// —— 引导一共三拍：① 开局（<see cref="CoStageEntryDraft"/>）② 打完两波 ③ 再打完两波
    ///（②③ 由 <c>TutorialDirector</c> 调 <see cref="CoMidBattleDraft"/>）。
    /// 每拍弹一次面板、<b>只许抽一抽</b>，抽完立刻收面板继续教学。</para>
    /// <para>抽什么由 <c>SlotMachineDefs.TutorialDrawOrder</c>（装备→佣兵→技能）+ 本局累计抽数决定，
    /// 所以「每拍一次」恰好 = 每拍走一格定序 —— 三拍下来正好是装备 / 佣兵 / 技能。<b>顺序不在这里写第二份。</b></para>
    /// <para>⚠ 这套定序<b>只在引导局生效</b>：正式关从第 1 抽起就等权随机
    ///（2026-10-05 主人拍板「正式关的时候应该都是随机的，按我设定的来」）。</para>
    /// ⚠ 这是**唯一**判定「引导要不要限制抽数」的地方。以前散在 <c>CoStageEntryDraft</c> 的一个
    /// `if (tutorialGuide) break` 里、`CoMidBattleDraft` 靠「没写循环」隐式成立 —— 那是补丁，现已收成一处。
    /// </summary>
    public bool OneDrawPerBeat => Rules.GuideEntryDraft;
    /// <summary>
    /// 本局玩法模式。<b>「这是哪一种玩法」一律问它</b>，
    /// 不要在核心里再散 <c>IsGoldDungeon</c> / <c>IsEndless</c> 这类 bool —— 每加一种玩法就要在
    /// 同样 8 处各加一个分支，正是「改一处牵连另一处」的来源。
    /// 开局由 <see cref="ResolveMode"/> 定一次，整局不变。
    /// </summary>
    public IBattleMode Mode { get; private set; }
    /// <summary>引导拿剑后：强伤一刀一个怪的爽点阶段。</summary>
    public bool TutorialPowerFantasy { get; private set; }
    /// <summary>
    /// 是否拦住「清完波次 → 宝箱结算」。
    /// 【2026-10-05 主人拍板】真源唯一 = 当前规则包 <see cref="TutorialRules.SuppressStageClear"/>，不再缓存成可变字段。
    /// 原先只在 StartNewRun / 教程波里赋值，LoadStage 不重设 → 换关换 Mode 时会残留 true，
    /// 该关清完永不结算（无宝箱无传送门）。改成只读直通后结构上不可能残留。
    /// </summary>
    public bool SuppressStageClear => Rules.SuppressStageClear;
    /// <summary>同上：真源唯一 = 规则包，不缓存。</summary>
    public bool SkipLegacyOnEvacuate => Rules.SkipLegacyOnEvacuate;
    /// <summary>本局金币获得倍率（剧情选择等）</summary>
    public float runGoldGainMul = 1f;
    /// <summary>关卡事件层金币补偿倍率（增援突袭 / 环境灾害）。默认 1 = 无事件，关闭事件层时恒为 1。</summary>
    public float stageEventGoldMul = 1f;

    /// <summary>
    /// 结算金币时真正该用的倍率 = 局内修正 × 天赋「金币掉落」（AttrType.GoldBonus）。
    /// GoldBonus 由 AttrSystem（天赋 GoldDrop）写入过，但之前<b>全项目没有任何地方读取</b>，
    /// 所以天赋 R2B / R9B 点了看不出变化 —— 现在在这里消费它。
    /// </summary>
    public float GoldGainMul
    {
        get
        {
            float bonus = 0f;
            var hero = Hero.Instance;
            if (hero != null && hero.attr != null)
                bonus = Mathf.Max(0f, hero.attr.GetAttr(AttrType.GoldBonus));
            return runGoldGainMul * (1f + bonus) * stageEventGoldMul;
        }
    }
    /// <summary>当前关任务清关金币（HUD 预览 + 开箱发放）</summary>
    public int StageQuestClearGold { get; private set; }
    bool _stageQuestGoldGranted;
    string _stageQuestObjective = "击败所有敌人";
    /// <summary>本关已击杀数（进度条口径；勿用 goal-alive，未刷波次会假满）</summary>
    int _defeatedMonsterKills;
    internal int _tutorialSpriteMelee = 1;
    internal int _tutorialSpriteRanged = 2;
    internal int _tutorialEliteCount;
    internal int _tutorialWaveMonsterCount;
    /// <summary>引导波远程只数；&lt;0 = 走旧规则自动折算（近战:远程 ≈ 4:2）。</summary>
    internal int _tutorialRangedCount = TutorialBattleTable.AutoRangedCount;
    internal float _tutorialHpMin = TutorialBattleTable.DefaultHpMin;
    internal float _tutorialHpMax = TutorialBattleTable.DefaultHpMax;
    internal float _tutorialEliteHpMin = TutorialBattleTable.DefaultEliteHpMin;
    internal float _tutorialEliteHpMax = TutorialBattleTable.DefaultEliteHpMax;
    /// <summary>引导波远程怪 HP 档；0 = 不单独降血，与近战同档。</summary>
    internal float _tutorialRangedHpMin;
    internal float _tutorialRangedHpMax;
    internal bool _tutorialHpFromTable;
    /// <summary>本局怪物攻速倍率（剧情选择等）</summary>
    public float runMonsterAtkSpeedMul = 1f;
    /// <summary>正在走向 chuansongmen，放宽屏幕钳制</summary>
    public bool PortalWalkMode => _portalActive && !_stageCleared;
    /// <summary>佣兵相对主角前后间距（世界单位）；近战前/远程后由 UnitCrowd 决定符号。</summary>
    public const float MERC_FRONT_SPACING = 0.55f;
    [System.Obsolete("Use MERC_FRONT_SPACING / UnitCrowd.GetMercDesiredCombatX")]
    public const float MERC_BEHIND_SPACING = MERC_FRONT_SPACING;
    /// <summary>开场从站位左侧多远走进来（越小出生越靠镜头/越靠右）</summary>
    const float PARTY_ENTER_FROM = 1.15f;

    float _battleStartTime;
    internal bool _firstWaveSpawned;
    bool _eliteToastShownThisStage;
    bool _battleIntroFinished;
    Coroutine _firstWaveSpawnCo;

    public int CurrentChapter => ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
    public int BattleDifficulty { get; private set; }
    /// <summary>
    /// 是否金币本。
    /// 以前是开局从 <c>AdventureUI.PendingGoldDungeon</c> 拷进来的一个独立 bool（虽然只有一处写入），
    /// 现在改为从 <see cref="Mode"/> 派生——玩法身份只有一个来源，不再有第二个可写的地方。
    /// 对外签名不变，Monster / ChapterManager / BattleStateSaver 照旧读。
    /// </summary>
    public bool IsGoldDungeon => Mode != null && Mode.Id == BattleModeId.GoldDungeon;

    /// <summary>当前局是否徽记副本（2026-10-23 新玩法，见 BadgeDungeonBattleMode）。派生写法照抄 IsGoldDungeon。</summary>
    public bool IsBadgeDungeon => Mode != null && Mode.Id == BattleModeId.BadgeDungeon;

    /// <summary>当前局是否无限裂隙（2026-10-17 新玩法，见 EndlessBattleMode）</summary>
    public bool IsEndlessRift => Mode != null && Mode.Id == BattleModeId.Endless;

    /// <summary>当前局是否支线岛（2026-10-23 合并方案，见 SideIslandBattleMode）。派生写法照抄 IsGoldDungeon。</summary>
    public bool IsSideIsland => Mode != null && Mode.Id == BattleModeId.SideIsland;

    /// <summary>
    /// 【2026-10-22 主人拍板】主线正式关（v4 回合制新流程的作用域）= Mode 是 NormalBattleMode。
    /// 引导局（TutorialBattleMode）/金币本（GoldDungeonBattleMode）/无限裂隙（EndlessBattleMode）一律 false。
    /// v4 新逻辑（类型定死 / 波数 9/10 / 回合末抽奖 / 停通关补贴 / 去宝箱直结算 / 石像事件）都必须过这道过滤。
    /// </summary>
    public bool IsFormalMainRun => Mode != null && Mode.Id == BattleModeId.Normal;

    /// <summary>无限裂隙当前层数（绝对层，1 起；进本 = 记录点+1，续层 ++）</summary>
    public int EndlessRiftStage { get; private set; }

    /// <summary>
    /// 本局支线岛 id（2026-10-23 合并方案；1..8，非岛局恒 0）。
    /// 进本时从 AdventureUI.PendingSideIsland 拷入（Pending 读后即清，岛 id 留这里给结算读，
    /// 照 EndlessRiftStage 的「进本捕获」模式）。
    /// </summary>
    public int SideIslandId { get; private set; }

    /// <summary>
    /// 本模式掉不掉装备。由 <see cref="Mode"/> 自己声明（金币本 false，主线/引导 true）。
    /// 结算只看这个，不再判「是不是金币本」—— 以后「武器副本」这类活动也能自然掉装。
    /// </summary>
    public bool DropsEquipment => Mode != null && Mode.DropsEquipment;
    /// <summary>
    /// 难度属性倍率（2026-10-23 主人拍板）：活动副本（金币/徽记）按难度 ×1.5 递增（1.0/1.5/2.25）；
    /// 其余模式（主线/引导/无限裂隙）照旧走全局曲线（1.0/1.35/2.0）。无限裂隙不选难度，天然除外。
    /// </summary>
    public float DifficultyStatScale => (IsGoldDungeon || IsBadgeDungeon)
        ? GameConfig.GetActivityDifficultyStatScale(BattleDifficulty)
        : GameConfig.GetDifficultyStatScale(BattleDifficulty);
    public float DifficultyGoldMul => GameConfig.GetDifficultyGoldMul(BattleDifficulty);

    #endregion
}
