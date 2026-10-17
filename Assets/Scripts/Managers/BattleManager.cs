using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 战斗编排器：本局状态、胜负/结算、能量与开场过场。
/// 波次规划/出怪 → <see cref="WavePlanner"/>；技能施放 → <see cref="SkillCastService"/>。
/// </summary>
public class BattleManager : Singleton<BattleManager>, ICombatBoundSingleton
{
    #region 场景引用与单位
    [Header("场景引用")]
    public Hero hero;
    public Transform spawnPoint;
    public Transform endPoint;
    public Transform[] monsterSpawnPoints;
    /// <summary>用户摆好的 unit 节点：所有人物/怪物挂在其下，位置即草地站立线</summary>
    public Transform unitRoot;

    public List<UnitBase> allyUnits = new List<UnitBase>();
    public List<UnitBase> monsters = new List<UnitBase>();
    public StageData currentStage;
    #endregion
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
    int _enchantAtRunStart;
    int _matsAtRunStart;
    public BattleRunStats RunStats { get; private set; } = new BattleRunStats();
    public bool isInBattle = false;
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

    /// <summary>当前局是否无限裂隙（2026-10-17 新玩法，见 EndlessBattleMode）</summary>
    public bool IsEndlessRift => Mode != null && Mode.Id == BattleModeId.Endless;

    /// <summary>无限裂隙当前层数（绝对层，1 起；进本 = 记录点+1，续层 ++）</summary>
    public int EndlessRiftStage { get; private set; }

    /// <summary>
    /// 本模式掉不掉装备。由 <see cref="Mode"/> 自己声明（金币本 false，主线/引导 true）。
    /// 结算只看这个，不再判「是不是金币本」—— 以后「武器副本」这类活动也能自然掉装。
    /// </summary>
    public bool DropsEquipment => Mode != null && Mode.DropsEquipment;
    public float DifficultyStatScale => GameConfig.GetDifficultyStatScale(BattleDifficulty);
    public float DifficultyGoldMul => GameConfig.GetDifficultyGoldMul(BattleDifficulty);

    #endregion
    #region 波次系统
    // === 波次系统（规划/出怪在 WavePlanner；此处只留本局状态） ===
    WavePlanner _wavePlanner;
    internal WavePlanner Planner => _wavePlanner ?? (_wavePlanner = new WavePlanner(this));
    /// <summary>本关抽到的关卡模式名；空 = 没走模式逻辑（仍是旧均分波次）。UI 播报用。</summary>
    public string StageModeName => _wavePlanner != null ? _wavePlanner.StageModeName : "";
    /// <summary>本关模式的播报文案（如「两侧都有敌人」），给玩家 3 秒读条时间。</summary>
    public string StageModeTelegraph => _wavePlanner != null ? _wavePlanner.StageModeTelegraph : "";

    internal List<WaveData> _waves = new List<WaveData>();
    internal int _totalWaves = 0;
    internal bool _allWavesSpawned = false;
    private bool _stageCleared = false;
    internal int _totalMonstersSpawnedThisStage = 0;
    // 2026-09-26 主人拍板：按本关击杀的物理/魔法怪数量推导「主导怪物类型」，用于掉落按类型分池。
    int _physicalKills, _magicKills;
    /// <summary>当前进行中的波次下标；-1=尚未开刷</summary>
    internal int _activeWaveIndex = -1;
    private bool _waveAnnounceRunning;
    Coroutine _waveAnnounceCo;
    // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】波次闸门看门狗计时（真实时间）
    float _waveGateStuckSec;
    /// <summary>连杀计数</summary>
    private int _killCombo;
    private float _lastKillTime;
    /// <summary>友军连杀加速倍率（1~1.5）。</summary>
    public float KillComboSpeedMul { get; private set; } = 1f;

    void SyncKillComboHaste()
    {
        KillComboSpeedMul = GameConfig.GetKillComboSpeedMul(_killCombo);
    }

    // === 技能能量 ===
    /// <summary>
    /// 玩家技能能量 0~1，<b>每个技能槽各一条</b>（V6）。索引 = RunLoadout 的技能槽序。
    /// 只在盟友受击时按「伤害/最大生命」回充（见 AddCombatSkillEnergy），<b>冷却中的槽不充能</b> —— 这条是天然错峰的关键。
    /// 槽序同时是自动释放优先级：SkillSystem.GetReadyPlayerSkill 取第一个「能量满且不在冷却」的技能。
    /// </summary>
    #endregion
    #region 玩家技能能量
    public float[] playerSkillEnergy = new float[Mathf.Max(1, RunLoadout.MaxSkillSlots)];
    public const float MAX_SKILL_ENERGY = 1f;

    /// <summary>读某个技能槽的能量（越界返回 0）。</summary>
    public float GetPlayerSkillEnergy(int slot)
    {
        if (playerSkillEnergy == null) return 0f;
        if (slot < 0 || slot >= playerSkillEnergy.Length) return 0f;
        return playerSkillEnergy[slot];
    }

    /// <summary>写某个技能槽的能量（自动钳到 0~1，并通知 HUD）。</summary>
    public void SetPlayerSkillEnergy(int slot, float value)
    {
        if (playerSkillEnergy == null) return;
        if (slot < 0 || slot >= playerSkillEnergy.Length) return;
        playerSkillEnergy[slot] = Mathf.Clamp(value, 0f, MAX_SKILL_ENERGY);
        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy[slot]);
    }

    /// <summary>全部技能槽能量清零（开战/波次重置用）。</summary>
    public void ClearAllPlayerSkillEnergy()
    {
        if (playerSkillEnergy == null) return;
        for (int i = 0; i < playerSkillEnergy.Length; i++)
            playerSkillEnergy[i] = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
    }

    /// <summary>
    /// 找出一个可以释放的技能槽：能量已满，且该技能不在冷却中。返回 -1 表示没有。
    /// 槽序即优先级 —— 多个同时就绪时取最靠前的那个。
    /// </summary>
    public int FindReadySkillSlot()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return -1;
        // 用 SkillSystem 的本体列表：返回引用不分配（RunLoadout.SkillIds() 每次 new List，
        // 而本方法每帧都会被调用，不能在这里产生 GC）。
        var skills = sys.GetPlayerSkills();
        if (skills == null) return -1;

        int count = Mathf.Min(playerSkillEnergy.Length, skills.Count);
        for (int i = 0; i < count; i++)
        {
            // 纯冷却制下不再看能量，只看冷却（PLAYER_SKILL_USE_ENERGY 置 true 可退回原行为）
            if (GameConfig.PLAYER_SKILL_USE_ENERGY && playerSkillEnergy[i] < MAX_SKILL_ENERGY - 0.001f) continue;
            var s = skills[i];
            if (s == null || string.IsNullOrEmpty(s.skillId)) continue;
            if (sys.IsOnCooldown(s.skillId)) continue;
            // 2026-09-17：冷却好≠该放，还要等技能自己的战场状态触发条件（血线/怪群等）。
            if (!PlayerSkillPassive.IsTriggerMet(s.skillId)) continue;
            // 2026-10-06 主人拍板：蓝不足 = 这一槽本轮不可释放，**顺位给下一个**（与触发条件不满足同一口径）。
            // 无蓝技能（耗蓝 0）永远过得去 = 地板节奏；耗蓝技能才吃稀缺窗口，见设计文档 §9。
            // 2026-10-10：s.skillId 是 ally_* 别名，MpCostOf 按真 id 查表（查不到返回 -1 = 不存在），
            // 用别名查会恒得 -1 → 蓝不够也照样选中该槽，SkillCastService 里扣蓝失败整轮空转，
            // 顺位在后面的无蓝技能（0 蓝）跟着一起被卡死。这里解析成真 id 再判定。
            float mpCost = PlayerSkillDefs.MpCostOf(PlayerSkillDefs.ResolveRealSkillId(s.skillId));
            if (mpCost > 0f && _playerMp + 0.001f < mpCost) continue;
            return i;
        }
        return -1;
    }

    // ============================================================
    // 玩家技能：纯冷却制调度（2026-09-15）
    // 开局按槽位错峰给初始 CD；放完一个后全局间隔 PLAYER_SKILL_GCD 才能放下一个。
    // 用实例字段而非静态字段：随 BattleManager 生命周期自然重置，不会跨局残留。
    // ============================================================

    /// <summary>下次允许释放玩家技能的时间（Time.time 轴）。</summary>
    public float NextPlayerSkillCastAt { get; private set; } = -1f;

    /// <summary>全局释放间隔是否已过（true = 现在可以放）。</summary>
    public bool IsPlayerSkillGcdReady => Time.time >= NextPlayerSkillCastAt;

    /// <summary>释放成功后调用：上膛，接下来 PLAYER_SKILL_GCD 秒内不再放。</summary>
    public void ArmPlayerSkillGcd() => NextPlayerSkillCastAt = Time.time + GameConfig.PLAYER_SKILL_GCD;

    /// <summary>立即解除间隔限制（开战/换关时用）。</summary>
    public void ResetPlayerSkillGcd() => NextPlayerSkillCastAt = Time.time;

    /// <summary>
    /// 开局错峰：按槽位给每个玩家技能挂一段初始冷却，避免 4 个技能同时就绪一起炸出来。
    /// 第 1 槽固定 0.5 秒（最快登场），2~4 槽 = 自身 CD × 0.33 / 0.66 / 1.0。
    /// 只在这关刚开始时调用一次（LoadStage / StartNewRun），不要在升星或拖拽排序时调用
    /// ——那会把战斗中已经跑掉的冷却重置。
    /// </summary>
    public void PrimePlayerSkillOpeningCooldowns()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return;
        var skills = sys.GetPlayerSkills();
        if (skills == null) return;

        for (int i = 0; i < skills.Count && i < GameConfig.PLAYER_SKILL_OPENING_CD_MUL.Length; i++)
        {
            var s = skills[i];
            if (s == null || string.IsNullOrEmpty(s.skillId)) continue;
            if (sys.IsOnCooldown(s.skillId)) continue;   // 已有更长冷却的不覆盖

            // 2026-09-27 主人拍板：**每个技能进战都要先走一遍自己的完整冷却**，不许一上来就放。
            // 原实现：第 1 槽固定 0.5s、2~4 槽只跑自身 CD 的 0.33/0.66/1.0 倍 —— 等于刚进战就能放，
            // 主人反馈「玩家的技能不要一上来就释放」。现在统一 = 技能自身 CD；
            // 各技能 CD 本来就不一样，天然错峰，不再需要 PLAYER_SKILL_OPENING_CD_MUL 那套倍率
            // （常量保留不删，但本函数已不使用，别再拿它改回错峰）。
            float cd = s.cooldown;
            if (cd > 0.01f) sys.RegisterCooldown(s.skillId, cd);
        }
    }

    /// <summary>
    /// 佣兵蓝条 MP（最多 2 槽）。2026-10-06 主人拍板：由「技能能量」改为真正的蓝条，
    /// 按<b>槽位</b>存（换佣兵不回满，防「换人刷蓝」）。
    /// </summary>
    readonly float[] mercMp = new float[2];
    /// <summary>本关该槽是否已灌满过：每关开局回满一次，之后只靠自然回复。</summary>
    readonly bool[] _mercMpFilled = new bool[2];
    #endregion
    #region 佣兵技能与施放
    internal Coroutine _spawnWaveCo;
    internal int _offscreenEnterSideToggle;
    SkillCastService _skillCast;
    internal SkillCastService SkillCast => _skillCast ?? (_skillCast = new SkillCastService(this));

    /// <summary>取第 index 个在场佣兵（越界 / 无管理器 → null）。</summary>
    static Mercenary MercAt(int index)
    {
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null || index < 0 || index >= mercs.Count) return null;
        return mercs[index];
    }

    /// <summary>该槽 MP 池上限（该佣兵是「无蓝」技能 → 0，蓝条不显示）。</summary>
    public static float MercMpPool(int index)
    {
        var m = MercAt(index);
        if (m == null) return 0f;
        return MpProfile.Pool(MpProfile.OfMercSkill(m.equippedSkillId), m.mercLevel, m.mercStar);
    }

    /// <summary>该槽每秒自然回复（该佣兵是「无蓝」技能 → 0）。</summary>
    public static float MercMpRegen(int index)
    {
        var m = MercAt(index);
        if (m == null) return 0f;
        return MpProfile.Regen(MpProfile.OfMercSkill(m.equippedSkillId), m.mercStar);
    }

    /// <summary>
    /// 佣兵槽蓝条比例 0~1（当前蓝 ÷ 池上限），喂 UI 用。
    /// 槽未启用 / 该佣兵技能无蓝 → 0（蓝条由 UI 侧隐藏）。
    /// </summary>
    public float GetMercMp(int index)
    {
        if (index < 0 || index >= mercMp.Length) return 0f;
        float pool = MercMpPool(index);
        if (pool <= 0f) return 0f;
        return Mathf.Clamp01(mercMp[index] / pool);
    }

    /// <summary>
    /// 扣蓝的<b>唯一出口</b>：蓝够才扣并返回 true；蓝不够 / 槽位非法 → false（调用方不放技能）。
    /// cost &lt;= 0 视为无蓝技能，直接放行、不扣蓝。
    /// </summary>
    public bool TrySpendMercMp(Mercenary merc, float cost)
    {
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null || merc == null) return false;
        int slot = mercs.IndexOf(merc);
        if (slot < 0 || slot >= mercMp.Length) return false;
        if (cost <= 0f) return true;
        if (mercMp[slot] + 0.001f < cost) return false;
        mercMp[slot] = Mathf.Max(0f, mercMp[slot] - cost);
        BattleUI.Instance?.UpdateSkillEnergy(slot + 1, GetMercMp(slot));
        return true;
    }

    // ============================================================
    // 玩家蓝条（2026-10-06 主人拍板：与佣兵同一套 MpProfile，4 个技能共用一条）
    // ============================================================

    /// <summary>玩家当前蓝量。</summary>
    float _playerMp;
    /// <summary>本关是否已灌满过（每关开局回满，与佣兵同口径）。</summary>
    bool _playerMpFilled;

    /// <summary>
    /// 玩家本局有没有蓝：4 个技能里<b>只要有一个耗蓝 &gt; 0</b> 就有。
    /// 与佣兵同口径 —— 按「技能本身」判，不按职业，避免「职业有蓝但技能不耗蓝」的孤儿配置。
    /// </summary>
    public static bool PlayerHasMp()
    {
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        if (ids == null) return false;
        for (int i = 0; i < ids.Count; i++)
            if (MpProfile.OfPlayerSkill(ids[i]) != MpArchetype.None) return true;
        return false;
    }

    /// <summary>玩家蓝条星级：4 技共用一条蓝 → 按已装备技能里<b>最高</b>的那个星算。</summary>
    static int PlayerMpStar()
    {
        int star = 1;
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        if (ids == null) return star;
        for (int i = 0; i < ids.Count; i++)
            star = Mathf.Max(star, RunLoadout.StarOf(ids[i]));
        return star;
    }

    /// <summary>
    /// 实时读取当前职业使用的「攻击」：法系读 MagicAttack，其余职业读 Attack，真源就是 AttrSystem 那一份。
    /// 每次算上限 / 每次回蓝都<b>现读一遍</b> → 局内升级和装备变化立刻生效，不必等下一关。
    /// 城镇非战斗期可能拿不到 Hero / attr：记录错误后返回 0，保留原有 null 宽容度、不引入崩溃。
    /// </summary>
    static float PlayerAttackPower()
    {
        var hero = Hero.Instance;
        if (hero == null)
        {
            Debug.LogError("[BattleManager] PlayerAttackPower 失败：找不到 Hero，攻击换算项按 0 处理");
            return 0f;
        }
        if (hero.attr == null)
        {
            Debug.LogError("[BattleManager] PlayerAttackPower 失败：Hero.attr 为空，攻击换算项按 0 处理");
            return 0f;
        }
        return hero.attr.GetAttr(PlayerJobBaseStats.CurrentAttackAttr());
    }

    /// <summary>
    /// 实时读取「智力」基础属性：真源同样是 AttrSystem 那一份，与 PlayerAttackPower 同口径。
    /// 【2026-10-10 追加澄清·恢复】装备的 INTELLIGENCE 词条仍然有效：既走 ApplyDerivedAttributes 派生魔攻，
    /// 也通过 PlayerMpPool / PlayerMpRegen 喂蓝条（攻击项 + 智力项双叠加）。
    /// 只是<b>天赋左列</b>不再加智力（改投 Attack / MagicAttack），所以这里的来源主要是基础值 / 等级 / 披风 / 装备词条。
    /// 城镇非战斗期可能拿不到 Hero / attr：记录错误后返回 0，保留原有 null 宽容度、不引入崩溃。
    /// </summary>
    static float PlayerIntelligence()
    {
        var hero = Hero.Instance;
        if (hero == null)
        {
            Debug.LogError("[BattleManager] PlayerIntelligence 失败：找不到 Hero，智力换算项按 0 处理");
            return 0f;
        }
        if (hero.attr == null)
        {
            Debug.LogError("[BattleManager] PlayerIntelligence 失败：Hero.attr 为空，智力换算项按 0 处理");
            return 0f;
        }
        return hero.attr.GetAttr(AttrType.Intelligence);
    }

    /// <summary>
    /// 玩家 MP 池上限（本局没有耗蓝技能 → 0，蓝条不显示、不做闸门）。
    /// 【2026-10-10 主人拍板】攻击项<b>叠加</b>在 MpProfile 的星级/等级曲线之上（不是替换）；
    /// 【2026-10-10 追加澄清】智力项再叠加一层（装备智力词条续效）：
    /// 完整公式 = MpProfile.Pool(Player, HeroLevel, PlayerMpStar())
    ///          + 当前职业攻击 × MP_POOL_PER_ATTACK
    ///          + 智力 × MP_POOL_PER_INTELLIGENCE。
    /// </summary>
    public static float PlayerMpPool()
    {
        if (!PlayerHasMp()) return 0f;
        return MpProfile.Pool(MpArchetype.Player, RunLoadout.HeroLevel, PlayerMpStar())
             + PlayerAttackPower() * GameConfig.MP_POOL_PER_ATTACK
             + PlayerIntelligence() * GameConfig.MP_POOL_PER_INTELLIGENCE;
    }

    /// <summary>
    /// 玩家每秒自然回复（本局没有耗蓝技能 → 0）。
    /// 【2026-10-10 主人拍板】攻击项<b>叠加</b>在 MpProfile 的星级曲线之上（不是替换）；
    /// 【2026-10-10 追加澄清】智力项再叠加一层（装备智力词条续效）：
    /// 完整公式 = MpProfile.Regen(Player, PlayerMpStar())
    ///          + 当前职业攻击 × MP_REGEN_PER_ATTACK
    ///          + 智力 × MP_REGEN_PER_INTELLIGENCE。
    /// </summary>
    public static float PlayerMpRegen()
    {
        if (!PlayerHasMp()) return 0f;
        return MpProfile.Regen(MpArchetype.Player, PlayerMpStar())
             + PlayerAttackPower() * GameConfig.MP_REGEN_PER_ATTACK
             + PlayerIntelligence() * GameConfig.MP_REGEN_PER_INTELLIGENCE;
    }

    /// <summary>玩家蓝够不够这一发（<b>只看不扣</b>；扣蓝唯一出口仍是 TrySpendPlayerMp）。</summary>
    public bool CanAffordPlayerMp(float cost)
    {
        if (cost <= 0f) return true;
        return _playerMp + 0.001f >= cost;
    }

    /// <summary>玩家蓝条比例 0~1（喂 UI 用）。</summary>
    public float GetPlayerMp()
    {
        float pool = PlayerMpPool();
        if (pool <= 0f) return 0f;
        return Mathf.Clamp01(_playerMp / pool);
    }

    /// <summary>
    /// 扣玩家蓝的<b>唯一出口</b>：够才扣并返回 true；不够 → false（不放技能）。
    /// cost &lt;= 0 视为无蓝技能，直接放行、不扣蓝。
    /// </summary>
    public bool TrySpendPlayerMp(float cost)
    {
        if (cost <= 0f) return true;
        if (_playerMp + 0.001f < cost) return false;
        _playerMp = Mathf.Max(0f, _playerMp - cost);
        return true;
    }

    /// <summary>玩家蓝条每帧：本关首次进场灌满 → 之后按自然回复回蓝（面板冻结时 deltaTime=0 不回）。</summary>
    void TickPlayerMp(float dt)
    {
        float pool = PlayerMpPool();
        if (pool <= 0f)
        {
            if (_playerMp > 0f) _playerMp = 0f;
            return;
        }
        if (!_playerMpFilled)
        {
            _playerMp = pool;
            _playerMpFilled = true;
        }
        else
        {
            _playerMp = Mathf.Min(pool, _playerMp + PlayerMpRegen() * dt);
        }
        // 上限（曲线 + 攻击项 + 智力项）随局内状态实时变化：万一上限回落（更换 / 卸下装备、攻击增益消失等），
        // 当前蓝量 clamp 回上限内，别出现「当前蓝 > 上限」的显示（GetPlayerMp 的 Clamp01 也会跟着失真）。
        if (_playerMp > pool) _playerMp = pool;
    }

    // === 传送门 ===
    /// <summary>传送门是否已激活（所有怪清完后激活，玩家进入后通关）</summary>
    #endregion
    #region 传送门
    private bool _portalActive = false;
    private bool _rewardSequenceStarted = false;
    private Transform _chuanSongMen;

    // 【2026-10-11 主人拍板】传送门改用主人新加的 chuansongmen 预制体（PortalGate，挂 BattleUI 下）。
    // 场景里那个旧的 chuansongmen 节点不再参与通关流程（保留不删）。
    private PortalGate _portalGate;
    /// <summary>要走进门的队伍（开门那一刻定好：玩家 + 当时活着的佣兵），避免每帧重新取列表。</summary>
    private readonly List<UnitBase> _portalParty = new List<UnitBase>();
    private readonly HashSet<UnitBase> _portalEntered = new HashSet<UnitBase>();
    private bool _heroEnteredPortal;
    private float _heroEnteredAt;
    private float _portalOpenedAt;
    /// <summary>
    /// 【2026-10-15 主人拍板】引导结尾也走传送门（「所有打完一关后都出现传送门」）：
    /// true = 这个门是引导局的出口，全员进门后不走正常关清场链，直接接引导撤离结算（settlement → 回城）。
    /// </summary>
    private bool _tutorialPortalPending;
    /// <summary>玩家进门后，最多再等佣兵这么久（秒）—— 超时也走结算，绝不把玩家锁在门口。</summary>
    const float PortalMercWaitTimeout = 2.5f;

    /// <summary>
    /// 旧 endPoint 传送门路径（已弃用）。正式通关走 chuansongmen：StageClearRewardDirector → NotifyChuanSongMenOpened。
    /// </summary>
    [System.Obsolete("使用 chuansongmen 通关导演，勿再调用 ActivatePortal")]
    void ActivatePortal()
    {
        GamePerf.Log("[BattleManager] ActivatePortal 已弃用，忽略（请走 chuansongmen）");
    }

    IEnumerator CoActivatePortal()
    {
        yield break;
    }

    /// <summary>开战时预挂传送门动画，避免清场瞬间 AddComponent</summary>
    public static void EnsurePortalAnimatorReady(Transform end)
    {
        if (end == null) return;
        var fx = end.GetComponent<PortalAnimator>();
        if (fx == null) fx = end.GetComponentInChildren<PortalAnimator>(true);
        if (fx == null)
            fx = end.gameObject.AddComponent<PortalAnimator>();
        fx.Warm();
        // 保持未激活，等真正通关再开
        if (fx.gameObject != end.gameObject)
            fx.gameObject.SetActive(false);
        else
            fx.enabled = false;
    }

    internal void ExtendCameraMaxX(float worldX)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;
        if (worldX > follow.maxX)
            follow.maxX = worldX;
    }

    /// <summary>
    /// 隐藏传送门（新一局开始时，打完怪才出现）
    /// </summary>
    void HidePortal()
    {
        if (endPoint != null)
        {
            // 隐藏EndPoint视觉（传送门），但保留碰撞/逻辑
            SpriteRenderer portalSr = endPoint.GetComponent<SpriteRenderer>();
            if (portalSr != null)
            {
                portalSr.enabled = false;
            }
            var portalFx = endPoint.GetComponentInChildren<PortalAnimator>(true);
            if (portalFx != null)
            {
                portalFx.enabled = false;
                if (portalFx.gameObject != endPoint.gameObject)
                    portalFx.gameObject.SetActive(false);
            }
        }
    }
    #endregion
    #region 生命周期与开局
    protected override void Awake()
    {
        base.Awake();
    }

    public void StartNewRun()
    {
        StartNewRunInternal();
    }

    /// <summary>
    /// 定本局的玩法模式。<b>这是全项目唯一决定「这是哪一种玩法」的地方。</b>
    /// 以后加无限关卡 / 世界BOSS / PVP，只在这里多加一条返回，其余代码一行不动。
    /// </summary>
    IBattleMode ResolveMode()
    {
        // 引导优先：它由剧情触发，跟玩家在冒险界面选哪个按钮无关。
        // 注意要在 StoryProgress.ConsumeTutorialBattleFlag() 之前判。
        if (StoryProgress.ShouldStartTutorialBattle()) return BattleModes.Tutorial;

        // 活动副本（冒险界面下标 4）。这是一个「类」，不是一种玩法：
        // 现在只开了金币本；以后佣兵副本 / 武器副本 / 技能副本上了，
        // 在这里按「当前开放的是哪一个活动」返回对应模式即可。
        if (AdventureUI.PendingGoldDungeon) return BattleModes.GoldDungeon;

        // 无限裂隙（冒险界面下标 2）
        if (AdventureUI.PendingEndlessRift) return BattleModes.Endless;

        return BattleModes.Normal;
    }

    void StartNewRunInternal()
    {
        Debug.Log("[BattleManager] ===== StartNewRun 开始 =====");
        StopBattleSpawnCoroutines();
        // 新一局：清空「章内不重复」的关卡模式记录，重新洗牌（V3.0 起模式允许重复，此处仅清压力阀）
        StageModeTable.ResetRun();
        Planner?.ResetPressure();
        // 【2026-10-10 主人拍板】战斗钱包局内化：**城镇金币不带进战斗**，开局只有本局启动金。
        // 启动金由 RunDraftDirector.OnRunStart（非续关）走 SlotMachineSystem.GrantRunStarterGold 补 ——
        // 那里才读得到「初始资金」天赋等级。这里先归零，_goldAtRunStart 会随那笔一起抬起来，
        // 于是结算差额 = 本局净赚（抽奖花掉的钱也扣在里面），启动金既不会被白刷也不会被重复写回。
        currentGold = 0;
        _goldAtRunStart = 0;
        _runStageGoldPending = 0;
        _runStagesCleared = 0;
        var data = SaveSystem.Instance?.Data;
        _enchantAtRunStart = data != null ? data.enchantStones : 0;
        _matsAtRunStart = data != null ? data.decomposeMats : 0;
        _stageQuestGoldGranted = false;
        _defeatedMonsterKills = 0;
        RunStats.Reset();
        RunStats.Chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        {
            var save = SaveSystem.Instance?.Data;
            string pn = save != null && !string.IsNullOrEmpty(save.playerDisplayName)
                ? save.playerDisplayName : "冒险者";
            RunStats.EnsureAlly(BattleRunStats.PlayerMvpKey, pn);
        }
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        ResetPortalGateState();
        UnitsCanAct = false;
        AllowMonsterMapEnter = false;
        isAutoBattle = false;
        MonsterAttackStyleTable.Reload();
        // 纯冷却制：清能量换成「解除间隔 + 按槽位错峰上初始 CD」
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();
        // 上一局/上一关残留的攻击/防御/暴击增益（定时增益层）必须清掉，否则会跨关带着走
        Hero.Instance?.attr?.ClearTimedBuffs();
        ResetMpForNewStage();
        MercenaryManager.Instance?.ClearAllMercs();
        allyUnits.RemoveAll(u => u == null || u is Mercenary);

        HidePortal();
        EnsureRewardDirector();
        StageClearRewardDirector.Instance?.CacheSceneRefs();
        StageClearRewardDirector.Instance?.HideClearProps();
        EnsurePortalAnimatorReady(endPoint);

        if (hero == null)
            hero = Hero.Instance != null ? Hero.Instance : FindObjectOfType<Hero>();
        if (hero == null)
        {
            Debug.LogError("[BattleManager] StartNewRun 失败：找不到 Hero，无法开战/刷怪");
            return;
        }

        if (!allyUnits.Contains(hero))
            allyUnits.Add(hero);

        try
        {
            hero.InitNewRun();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] hero.InitNewRun 异常（继续开战）: {e}");
        }

        // 玩法身份只在这里定一次；规则包由模式自带，不再单独判一次 IsTutorial。
        Mode = ResolveMode();
        Rules = Mode.Rules;
        TutorialPowerFantasy = false;
        _tutorialHpFromTable = false;
        if (Rules.Active)
        {
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
            TutorialDirector.Instance?.ResetBattleTutorialRuntime();
            StoryProgress.ConsumeTutorialBattleFlag();
        }
        // 选职后一律发职业武器（含教程）；木剑兜底仅非教程
        if (GridBackpackSystem.Instance != null)
        {
            PlayerJobDefs.ApplyForBattle();
            if (!Rules.SkipStarterWeaponFallback
                && GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand) == null)
            {
                if (GridBackpackSystem.Instance.EnsureStarterWeapon())
                    hero.RecalcAttr();
            }
        }
        // 【2026-10-05】SuppressStageClear / SkipLegacyOnEvacuate 已改成只读直通规则包，
        // 这里不再赋值（原先只在这一处设，换关只走 LoadStage 时会残留上一关的值）。
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        if (Rules.Active)
            Debug.Log("[BattleManager] 新手引导战斗：短关+强制撤离（规则包 TutorialRules）");

        // === 局内构筑（肉鸽影子）：技能 + 佣兵全部走本局 loadout，不读城镇酒馆存档 ===
        // 新手引导局：V6 起也开构筑（走内存模式），让玩家在引导里就学会三选一与技能顺序
        if (!Rules.Active || Rules.EnableRunDraft)
            RunDraftDirector.Ensure(this).OnRunStart();

        // loadout 生效时不再走城镇酒馆雇佣；否则保留旧路径（异常/特殊规则兜底）
        if (!RunLoadout.IsActive && !GameConfig.SOLO_PLAYER_BATTLE && !Rules.SkipMercPrecall)
        {
            EnsureTestMercenaries();
            SpawnMercenaries();
        }

        int targetChapter = AdventureUI.PendingBattleChapter;
        if (targetChapter < 1)
            targetChapter = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (targetChapter < 1) targetChapter = 1;
        BattleDifficulty = Mathf.Clamp(AdventureUI.PendingBattleDifficulty, 0, 2);
        // IsGoldDungeon 不再在这里写入：它由 Mode 派生（见属性定义）。
        // PendingGoldDungeon 只保留「读完即清」，避免下一局被上一局的静态标志污染。
        AdventureUI.PendingBattleChapter = 0;
        AdventureUI.PendingBattleDifficulty = 0;
        AdventureUI.PendingGoldDungeon = false;

        // 无限裂隙：起始层 = 记录点 + 1（读完即清，防污染）；普通章号参数对它无意义
        if (AdventureUI.PendingEndlessRift)
        {
            EndlessRiftStage = EndlessRiftRules.StartStage();
            AdventureUI.PendingEndlessRift = false;
            targetChapter = GameConfig.EndlessChapterForStage(EndlessRiftStage);
        }

        if (ChapterManager.Instance == null)
        {
            Debug.LogError("[BattleManager] ChapterManager 为空，用临时 Normal 关强行 LoadStage");
            LoadStage(new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() });
            return;
        }

        // 建关交给模式自己：核心不需要知道一共有几种玩法。
        Mode.BuildChapter(ChapterManager.Instance, targetChapter);

        StageData first = null;
        if (ChapterManager.Instance.availableNextStages != null && ChapterManager.Instance.availableNextStages.Count > 0)
            first = ChapterManager.Instance.availableNextStages[0];
        else if (ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
            first = ChapterManager.Instance.stageMap[0];

        if (first == null)
        {
            Debug.LogError("[BattleManager] 关卡图为空，创建临时 Normal 关");
            first = new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() };
        }

        Debug.Log($"[BattleManager] StartNewRun → LoadStage type={first.type} idx={first.stageIndex}");
        if (!Rules.SkipRiftEnteredAchievement)
            AdventureLogAchievements.OnRiftEntered();
        LoadStage(first);
    }

    IEnumerator CoPreLevelThenLoad(StageData first)
    {
        // 遗产战前三选一已取消；保留方法避免外部误调编译失败
        LoadStage(first);
        yield break;
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();

        for (int i = data.permanentMercs.Count - 1; i >= 0; i--)
        {
            var m = data.permanentMercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId))
            {
                data.permanentMercs.RemoveAt(i);
                continue;
            }
            // 无效弓手预制体 id 纠正
            if (m.mercId.StartsWith("gongshou") && Resources.Load<GameObject>("Units/" + m.mercId) == null)
                m.mercId = "gongshou101";
            if (m.level < 1) m.level = 1;
            if (m.star < 1) m.star = 1;
            if (string.IsNullOrEmpty(m.displayName)) m.displayName = m.mercId;
            if (string.IsNullOrEmpty(m.uid)) m.uid = System.Guid.NewGuid().ToString("N");
            MercSkillMigrate.AlignMercenary(m);
        }

        // 软著版：佣兵由酒馆三选一反复招募，不再空档自动塞弓手。
        // 旧逻辑保留注释，便于日后调试：
        // if (data.permanentMercs.Count == 0)
        // {
        //     data.permanentMercs.Add(new MercenaryData {
        //         mercId = "gongshou101", displayName = "测试弓手",
        //         favorLevel = 1, level = 1, star = 1,
        //         skillId = SkillRegistry.DefaultMercRangedSkillId,
        //         uid = System.Guid.NewGuid().ToString("N")
        //     });
        // }

        if (data.townLevel.tavern < 1 && data.permanentMercs.Count > 0)
            data.townLevel.tavern = 1;
    }

    void SpawnMercenaries()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) return;

        var data = SaveSystem.Instance?.Data;
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : hero.transform.position;
        basePos.y = UnitBase.GROUND_Y;
        basePos.z = 0f;

        int max = mm.GetMaxMercSlots();
        int spawned = 0;
        bool countedLaodun = false;
        var party = mm.GetActiveMercData();
        for (int i = 0; i < party.Count && spawned < max; i++)
        {
            var md = party[i];
            if (md == null || string.IsNullOrEmpty(md.mercId)) continue;
            if (!GameConfig.IsMercAvailable(md.mercId, data)) continue;

            Vector3 pos = basePos;
            if (hero != null)
                pos.x = UnitCrowd.GetMercDesiredCombatX(hero, null, spawned);
            else
                pos.x = basePos.x + MERC_FRONT_SPACING * (spawned + 1);

            var merc = mm.SpawnMercenary(md.mercId, pos, Mathf.Max(1, md.level));
            if (merc != null)
            {
                MercSkillMigrate.AlignMercenary(md);
                string active = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercSkillId(md)
                    : md.skillId;
                string passive = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercPassiveSkillId(md)
                    : md.passiveSkillId;
                merc.SetupBattleSkills(active, passive);
                merc.SetPartyIndex(spawned);
                merc.SetDisplayName(md.displayName);
                if (!string.IsNullOrEmpty(md.hireId))
                    merc.SetHireId(md.hireId);
                else
                {
                    string resolved = MercPortraitSprites.ResolveHireId(md.mercId);
                    if (!string.IsNullOrEmpty(resolved))
                        merc.SetHireId(resolved);
                }
                if (!string.IsNullOrEmpty(md.displayName))
                    merc.gameObject.name = "Merc_" + md.displayName;
                allyUnits.Add(merc);
                merc.OnDead += OnMercenaryDead;
                RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
                spawned++;
                if (!Rules.SkipLaodunAchievement && !countedLaodun && md.mercId.StartsWith("dunbing"))
                {
                    AdventureLogAchievements.OnLaodunBattled();
                    countedLaodun = true;
                }
            }
        }
        Debug.Log($"[BattleManager] 已生成佣兵 {spawned} 个 (tavern槽={max})");
    }

    /// <summary>
    /// 【2026-10-17】阵亡佣兵下一关复活 —— 兑现 2026-10-06 拍板「阵亡下一关按 20% 血复活（每过一关养回 30%）」。
    /// <para>旧实现只有开新一局才 <see cref="SpawnMercenaries"/>，同一局内阵亡的佣兵永远回不来
    /// （玩家报「佣兵打着打着就消失了」）。进下一关时扫 <see cref="RunLoadout"/>：
    /// fallen=true 且场上没有实例的，按 <see cref="RunLoadout.MercReviveHpRatio"/> 的血量重新生成
    /// （血量 = 20% + 30%×已过关数，封顶 100%）。</para>
    /// <para>生成参数与 <see cref="RunDraftDirector.RecruitMerc"/> 对齐：星级 / 技能 / 名字 / hireId /
    /// OnDead 订阅一个不少，保证它再阵亡时走同一条死亡链路。</para>
    /// </summary>
    void ReviveFallenMercsForStage()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || !RunLoadout.IsActive) return;

        var onField = mm.GetActiveMercs();
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var e = mercs[i];
            if (e == null || !e.fallen) continue;
            string key = !string.IsNullOrEmpty(e.hireId) ? e.hireId : e.mercId;

            // 场上已有同 id 实例就不补（理论上 fallen=true 时实例已 Destroy，这里只防手抖双生成）
            bool alreadyOnField = false;
            for (int k = 0; k < onField.Count; k++)
            {
                var m = onField[k];
                if (m == null) continue;
                string mKey = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
                if (mKey == key) { alreadyOnField = true; break; }
            }
            if (alreadyOnField) continue;

            var heroRef = hero != null ? hero : Hero.Instance;
            int slot = mm.GetActiveMercs().Count;
            Vector3 pos;
            if (heroRef != null)
                pos = new Vector3(UnitCrowd.GetMercDesiredCombatX(heroRef, null, slot), UnitBase.GROUND_Y,
                    unitRoot != null ? unitRoot.position.z : 0f);
            else
                pos = new Vector3(transform.position.x + MERC_FRONT_SPACING * (slot + 1), UnitBase.GROUND_Y, 0f);

            var merc = mm.SpawnMercenary(e.mercId, pos, Mathf.Max(1, e.level));
            if (merc == null)
            {
                Debug.LogError($"[BattleManager] 阵亡佣兵复活失败：预制体缺失 mercId={e.mercId}");
                continue;
            }
            merc.mercStar = Mathf.Clamp(e.star, 1, 5);
            // SpawnMercenary → Init 会重置满血，惩罚必须在这里补砍回去（与 RunDraftDirector 同口径）
            float reviveRatio = RunLoadout.MercReviveHpRatio(e);
            if (reviveRatio < 1f && merc.attr != null)
            {
                float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
                merc.currentHp = Mathf.Max(1f, maxHp * reviveRatio);
            }
            merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
            merc.SetPartyIndex(slot);
            merc.SetDisplayName(e.displayName);
            if (!string.IsNullOrEmpty(e.hireId))
                merc.SetHireId(e.hireId);
            if (!string.IsNullOrEmpty(e.displayName))
                merc.gameObject.name = "Merc_" + e.displayName;
            if (!allyUnits.Contains(merc))
                allyUnits.Add(merc);
            merc.OnDead += OnMercenaryDead;
            merc.Face(1);
            Debug.Log($"[BattleManager] 阵亡佣兵复活：{e.displayName}({e.mercId}) 血量 {reviveRatio * 100f:0.}%（fallenStages={e.fallenStages}）");
        }
    }

    #endregion
    #region 战斗查询与增益
    public void OnMercenaryDead(UnitBase merc)
    {
        allyUnits.Remove(merc);
        // 【2026-10-17 诊断日志】玩家报「佣兵没死就消失」——佣兵平时没血条，真机大伤害秒杀看不出来。
        // 这里把阵亡实锤打出来：谁、多少血死的、死在哪，下次复现直接对日志。
        if (merc is Mercenary md)
            Debug.LogWarning($"[BattleManager] 佣兵阵亡：{md.DisplayName}({md.mercId}) 血量={md.currentHp:0.}/max={md.attr?.GetAttr(AttrType.MaxHp) ?? 0f:0.} pos={merc.transform.position:F1}");
        else
            Debug.LogWarning($"[BattleManager] 佣兵阵亡：{merc.name}");
        // 2026-10-06 主人拍板：阵亡不再零成本 —— 记进本局构筑，下一关按 20% 血复活（每过一关养回 30%）。
        if (merc is Mercenary m)
            RunLoadout.MarkMercFallen(!string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId);
        TryPlayMercDeathLine(merc);
    }

    void TryPlayMercDeathLine(UnitBase unit)
    {
        if (Rules.SkipMercDeathBanter) return;
        if (!(unit is Mercenary m) || m == null) return;
        string key = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
        string line = MercLineTable.Pick(key, MercLineTable.Scene.Death);
        if (string.IsNullOrEmpty(line)) return;

        // 死亡动画期间单位仍在：优先头顶气泡；否则 Toast
        if (unit != null && unit.gameObject != null && unit.gameObject.activeInHierarchy
            && (BattleHeadTalkUI.Instance == null || !BattleHeadTalkUI.Instance.IsShowing))
        {
            BattleHeadTalkUI.Ensure().PlayLine(unit, line, 1.25f);
            return;
        }
        if (UIManager.Instance != null)
            UIManager.Instance.ShowToast(line);
        else
            GlobalToastUI.Show(line);
    }

    void ApplyChapter1RunModifiersFromSave()
    {
        if (Rules.SkipChapter1RunModifiers) return;
        string c = StoryProgress.GetChoice(1);
        if (c == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (c == "B") runGoldGainMul = 1.1f;
    }

    public void ApplyChapter1ChoiceModifiers(string choiceId)
    {
        if (choiceId == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (choiceId == "B") runGoldGainMul = 1.1f;
    }

    public int GetAliveMonsterCount() => CountAliveMonsters();

    /// <summary>是否还有「已排队但未刷出」的波次（教程步/普通波都算）。用于清场判定，避免刷怪空窗被误判为已清。</summary>
    public bool HasPendingWaves => Planner != null && Planner.FindNextUnspawnedWaveIndex() >= 0;

    /// <summary>
    /// 玩家技能充能（V6）：<b>每个技能槽各一条</b>，受击时给所有「未满 且 不在冷却中」的槽同时注入。
    /// 冷却中的槽不充 —— 这条是天然错峰的关键：短 CD 技能先攒满先放，长 CD 的护盾后放，
    /// 不需要额外的「充能队列」逻辑，也不会出现四个技能同时爆发出。
    /// 总量由 GameConfig.SKILL_ENERGY_CHARGE_MUL 统一压制（这是手感主旋钮）。
    /// </summary>
    public void AddPlayerSkillEnergy(float rawAmount)
    {
        if (playerSkillEnergy == null || rawAmount <= 0f) return;

        float mul = Mathf.Max(0f, GameConfig.SKILL_ENERGY_CHARGE_MUL);
        float add = rawAmount * mul;
        if (add <= 0f) return;

        var sys = SkillSystem.Instance;
        var skills = sys != null ? sys.GetPlayerSkills() : null;
        int count = skills != null
            ? Mathf.Min(playerSkillEnergy.Length, skills.Count)
            : playerSkillEnergy.Length;

        for (int i = 0; i < count; i++)
        {
            if (playerSkillEnergy[i] >= MAX_SKILL_ENERGY) continue;      // 已满，不浪费

            if (skills != null)
            {
                var s = skills[i];
                if (s != null && !string.IsNullOrEmpty(s.skillId) && sys.IsOnCooldown(s.skillId))
                    continue;                                            // 冷却中，不充能
            }

            playerSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy[i] + add);
        }
        BattleUI.Instance?.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
    }

    /// <summary>
    /// 把所有技能槽的能量充满（V6：技能改为各自一条能量后，这里不再只充一条）。
    /// 目前无正式调用方，是留给剧情/教程的显式钩子 —— 教程最后一波会用它保证玩家一定看到技能放出。
    /// </summary>
    /// <summary>
    /// 教程钩子：让玩家的技能进入「可预期的一段冷却」后自然放出来（教程最后一波靠它让玩家看到技能）。
    /// 2026-09-27 主人拍板：这里**不再清冷却**（ClearPlayerSkillCooldowns 会让技能瞬间可放，
    /// 与主人口径「技能刚上来要先走一遍冷却」冲突）→ 改为重新挂一遍完整开场冷却。
    /// </summary>
    public void FillPlayerSkillEnergy()
    {
        // 纯冷却制：技能本来就不看能量，要做的是「解除全局间隔 + 重新挂完整冷却」，让它走完 CD 再放
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();

        if (GameConfig.PLAYER_SKILL_USE_ENERGY && playerSkillEnergy != null)
        {
            for (int i = 0; i < playerSkillEnergy.Length; i++)
                playerSkillEnergy[i] = MAX_SKILL_ENERGY;
            BattleUI.Instance?.UpdateSkillEnergy(0, MAX_SKILL_ENERGY);
        }
    }

    /// <summary>兼容旧的「单条能量」HUD：返回所有技能槽里充得最高的那条。</summary>
    public float PlayerSkillEnergyPeak
    {
        get
        {
            if (playerSkillEnergy == null) return 0f;
            float peak = 0f;
            for (int i = 0; i < playerSkillEnergy.Length; i++)
                if (playerSkillEnergy[i] > peak) peak = playerSkillEnergy[i];
            return peak;
        }
    }

    /// <summary>盟友受击回能：传入 finalDamage/MaxHp（打满血约攒满一槽）。</summary>
    public void AddCombatSkillEnergy(UnitBase unit, float amount)
    {
        if (unit == null || !unit.isAlly || amount <= 0f || !isInBattle) return;
        if (_stageCleared || _portalActive) return;

        // 玩家侧：纯冷却制下不充能（PLAYER_SKILL_USE_ENERGY 置 true 可退回原行为）。
        if (unit is Hero)
        {
            if (GameConfig.PLAYER_SKILL_USE_ENERGY)
                AddPlayerSkillEnergy(amount);
            return;
        }

        // 2026-10-06 主人拍板：佣兵改「自然回复制」，受击回蓝这条通道整段删除（旧实现不留）。
        // 佣兵蓝条只由 TickMercMp 每秒回，受击不再给任何蓝 —— 否则血线越低回得越快，与紧张感口径相反。
        return;
    }

    /// <summary>引导开箱拿剑后进入强伤+多怪爽点。</summary>
    #endregion
    #region 新手引导战斗
    public void BeginTutorialPowerFantasy()
    {
        if (!IsTutorialRun) return;
        TutorialPowerFantasy = true;
        Debug.Log("[BattleManager] 引导拿剑爽点：一刀一个 + 后续加怪");
    }
    /// <summary>导演节拍：按 tutorial_battle 步进刷一波。count/进场方向/HP 档来自表，不由导演发明。</summary>
    public void QueueTutorialStep(int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        Planner.QueueTutorialStep(order, anchorX, forcedTarget);
    }

    public void QueueTutorialWave(int count)
    {
        Planner.QueueTutorialWave(count);
    }

    public Mercenary SpawnTutorialMerc(string mercId, float hpRatio)
    {
        return SpawnTutorialMercAt(mercId, hpRatio, 7.5f, stunned: true);
    }

    /// <summary>
    /// 引导救援：牧师刷在玩家前方固定距离，原地眩晕；周围刷怪围殴她。
    /// </summary>
    public Mercenary SpawnTutorialMercAt(string mercId, float hpRatio, float aheadDist, bool stunned)
    {
        var mm = MercenaryManager.Instance;
        // 2026-09-27：以前失败只返回 null，导演侧只看到「牧师入队失败：merc 为空」这半句，
        // 不知道卡在哪一关。现在按 fail-closed 把原因打出来（不静默沿用、不拿别的单位顶替）。
        if (mm == null)
        {
            Debug.LogError("[BattleManager] SpawnTutorialMercAt 失败：MercenaryManager.Instance 为空");
            return null;
        }
        if (hero == null)
        {
            Debug.LogError("[BattleManager] SpawnTutorialMercAt 失败：hero 为空（玩家已阵亡或本局已收尾）");
            return null;
        }

        float heroX = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        Vector3 pos = new Vector3(heroX + aheadDist, UnitBase.GROUND_Y, z);
        string useId = string.IsNullOrEmpty(mercId) ? StoryProgress.TutorialMercId : mercId;
        var merc = mm.SpawnMercenary(useId, pos, 1);
        if (merc == null)
        {
            Debug.LogError($"[BattleManager] SpawnTutorialMercAt 失败：SpawnMercenary 返回空 mercId={useId}");
            return null;
        }
        MercRosterDefs.GetSkillIds(useId, out string active, out string passive);
        merc.SetupBattleSkills(active, passive);
        merc.SetDisplayName(StoryProgress.TutorialMercDisplayName);
        merc.SetHireId(StoryProgress.TutorialMercHireId);
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
        float maxHp = merc.attr != null ? merc.attr.GetAttr(AttrType.MaxHp) : 100f;
        merc.currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        merc.Face(-1);
        if (stunned)
            merc.SetTutorialStunned(true);
        ExtendCameraMaxX(pos.x + 6f);
        return merc;
    }
    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。走 SpawnWave 参数，不再另开刷怪轨。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        Planner.SpawnTutorialAmbushAround(victim, count);
    }

    /// <summary>诱饵埋伏：从锚点左右两侧刷怪。走 SpawnWave 参数（bilateralEnter）。</summary>
    public void SpawnTutorialFlankAmbush(int count, float? anchorX = null)
    {
        Planner.SpawnTutorialFlankAmbush(count, anchorX);
    }

    public void RetargetAllMonsters(UnitBase target)
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null || m.isDead) continue;
            m.SetForcedTarget(target);
        }
    }

    public void ClearMonsterForcedTargets()
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null) continue;
            m.SetForcedTarget(null);
        }
    }

    // ============================================================
    // 波次生成
    // ============================================================

    #endregion
    #region 关卡加载与波次执行
    public void LoadStage(StageData stage)
    {
        MonsterStatsTable.Reload();
        StageDropTable.Reload();
        StageSpawnTable.Reload();
        TutorialBattleTable.Reload();
        MonsterAttackStyleTable.Reload();
        BattleQuestTable.Reload();
        SpritePickWeightTable.Reload();
        WaveSlotTable.Reload();
        ChapterStatScaleTable.Reload();
        ChapterRouteTable.Reload();
        StageEventTable.Reload(); // 事件层 V1.0：每关重载（缺表/关闭时 Draw 返回 null，回退无事件）
        BossStageVariantTable.Reload(); // Boss 关变体 V1.0：每关重载（缺表/关闭时回退「只有 Boss 本体」）

        currentStage = stage;
        ClearAllMonsters();
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        ResetPortalGateState();
        _totalMonstersSpawnedThisStage = 0;
        // 【2026-10-05 波次排查】一局内连打多关时，任务金币只发第一关：
        // _stageQuestGoldGranted 原先只在 StartNewRun 复位，LoadStage 没复位，
        // 第 1 关领过之后永久为 true → 第 2 关起 TryGrantStageQuestGold 直接 return。
        _stageQuestGoldGranted = false;
        _physicalKills = 0;
        _magicKills = 0;
        _eliteToastShownThisStage = false;
        AllowMonsterMapEnter = false;
        // 纯冷却制：清能量换成「解除间隔 + 按槽位错峰上初始 CD」
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();
        // 上一局/上一关残留的攻击/防御/暴击增益（定时增益层）必须清掉，否则会跨关带着走
        Hero.Instance?.attr?.ClearTimedBuffs();
        ResetMpForNewStage();
        HeroThunderUltimate.Instance?.ResetForBattle();

        float startX = GetStageStartX();
        float z = unitRoot != null ? unitRoot.position.z : 0f;
        // 只摆玩家/佣兵，不要改写用户的 SpawnPoint 坐标
        GameConfig.SetWorldPosition(hero.gameObject, new Vector3(startX, UnitBase.GROUND_Y, z));
        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
        GameConfig.AttachToUnitRoot(hero.transform);
        Vector3 mercBasePos = new Vector3(startX, UnitBase.GROUND_Y, z);
        MercenaryManager.Instance?.ResetMercenaries(mercBasePos);
        // 【2026-10-17】阵亡佣兵下一关复活（兑现 2026-10-06 拍板；只在新一关生效，反复重打同一关不白刷
        // —— fallenStages 由「通关」TickFallenMercsStagePassed 推进）
        ReviveFallenMercsForStage();
        // 镜头先对齐；偏左显示
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);
            follow.SetTarget(hero.transform);
        }
        isInBattle = true;
        Time.timeScale = 1f;
        FocusMarkSystem.Ensure()?.ResetForBattle();
        BattleUI.Instance?.EnsureBattleControls();
        // 2026-10-09 主人拍板：进战斗自动收起背包弹窗，避免带着弹窗开打。
        BackpackPopupUI.Instance?.Hide();
        // 【2026-10-11 主人拍板】**点装备弹出来的那几层同样要自动关掉** ——
        // 主人原话「点击装备出来的弹窗 开始战斗要自动关闭 跟背包一样」。
        // BackpackItemActionUI = 点装备后浮出的「装备 / 分解 / 丢弃」操作层；
        // EquipStatTipUI = 装备属性浮字。开打了还糊在屏幕上会挡住战斗 UI。
        BackpackItemActionUI.Instance?.Hide();
        EquipStatTipUI.Instance?.Hide();
        BattleUI.Instance?.EnsureRunSkillSlotsRepaired();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);

        // 触发章节背景切换
        SwitchBattleBackground(CurrentChapter);

        // 按关卡类型切 BGM（Loading 中会记 pending，结束后再播）
        GameBgm.PlayForStage(stage.type);

        // 进关即写一次中断存档（后续由 BattleStateSaver 心跳续写退出时刻）
        BattleStateSaver.Instance?.SaveBattleState();

        switch (stage.type)
        {
            case StageType.Normal:
                Planner.SetupNormalWaves(stage.stageIndex);
                break;
            case StageType.Elite:
                Planner.SetupEliteWaves(stage.stageIndex);
                break;
            case StageType.Boss:
                Planner.SetupBossWave(stage.stageIndex);
                break;
            case StageType.Rest:
                isInBattle = false;
                LoadRestStage();
                break;
            default:
                // 商人/诅咒/锻造/附魔等：静默当普通战斗关
                stage.type = StageType.Normal;
                Planner.SetupNormalWaves(stage.stageIndex);
                break;
        }

        ResetWaveProgress();

        BattleUI.Instance?.UpdateCharacterSlots();
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
        BattleUI.Instance?.RefreshBattleHud();
        // 进关立刻把进度条 Marker 挂到当前 Node
        if (currentStage != null)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);
        int monsterGoal = GetStageMonsterGoal();
        var stageQuest = BattleQuestConfig.GetStageQuest(CurrentChapter, stage.type, IsGoldDungeon, BattleDifficulty);
        StageQuestClearGold = stageQuest.clearGold;
        _stageQuestObjective = stageQuest.objective;
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, 0, monsterGoal, StageQuestClearGold);

        if (isInBattle)
        {
            if (Rules.DirectorOwnsWaves)
                Planner.PrepareTutorialWaves();
            PlacePartyAt(startX, z);
            UnitsCanAct = false;
            _battleIntroFinished = false;
            _battleStartTime = Time.unscaledTime;
            EnsureMonsterPrefabReady();
            Analytics.StageStart(CurrentChapter, stage.stageIndex); // 埋点：关卡开始

            Debug.Log($"[BattleManager] LoadStage 战斗关 type={stage.type} waves={_waves?.Count ?? 0} heroX={UnitBase.GetCombatX(hero):F2}");

            if (Instance != this)
                Debug.LogError("[BattleManager] 当前实例不是单例实例！说明存在重复 BattleManager，Update/协程不会在本实例上运行");
            if (!gameObject.activeInHierarchy)
                Debug.LogError($"[BattleManager] 宿主 {gameObject.name} 未激活，协程与 Update 不会执行");

            StopBattleSpawnCoroutines();
            StartCoroutine("BattleStartSequenceCoroutine");
            MercBattleBanter.EnsureOn(this);
            // 教程关由 TutorialDirector 控刷怪，禁止硬性首波/紧急刷怪抢跑
            if (!Rules.SkipFirstWaveAuto)
                _firstWaveHardFallbackCo = StartCoroutine(CoFirstWaveHardFallback());
        }
        else
        {
            UnitsCanAct = true;
            Debug.LogWarning($"[BattleManager] 非战斗关 type={stage.type}，不刷怪");
        }
    }

    Coroutine _firstWaveHardFallbackCo;
    /// <summary>
    /// 战前剧情（TryPlayPreBattle）或进关抽奖进行中：硬刷兜底须等它结束，防止剧透/选卡时刷怪抢跑。
    /// 2026-09-29 起进关抽奖也复用这个标志，见 <see cref="CoStageEntryDraft"/>。
    /// </summary>
    bool _preBattleStoryPlaying;

    /// <summary>停掉开战/硬刷协程，避免连续 LoadStage 叠多个兜底。</summary>
    public void StopBattleSpawnCoroutines()
    {
        StopCoroutine("BattleStartSequenceCoroutine");
        StopWaveAnnounce();
        // 战前剧情若在播放中被打断，标志位必须复位，否则 CoFirstWaveHardFallback 会永久卡在等待里
        _preBattleStoryPlaying = false;
        if (_firstWaveSpawnCo != null)
        {
            StopCoroutine(_firstWaveSpawnCo);
            _firstWaveSpawnCo = null;
        }
        if (_firstWaveHardFallbackCo != null)
        {
            StopCoroutine(_firstWaveHardFallbackCo);
            _firstWaveHardFallbackCo = null;
        }
    }
    /// <summary>无视走路门槛，立刻刷下一波；失败则紧急造怪（不依赖配置/图集）</summary>
    void ForceSpawnFirstWaveNow() => Planner.ForceSpawnFirstWaveNow();

    void EmergencySpawnVisibleMonsters(int count) => Planner.EmergencySpawnVisibleMonsters(count);


    void ResetWaveProgress()
    {
        _activeWaveIndex = -1;
        _allWavesSpawned = false;
        _killCombo = 0;
        _lastKillTime = 0f;
        SyncKillComboHaste();
        CombatJuice.ResetComboToast();
        _firstWaveSpawned = false;
        _battleIntroFinished = false;
        _battleStartTime = Time.unscaledTime;
        BattleSideHud.Instance?.ResetCombo();
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }
    /// <summary>波次为空时强制补一波（兜底）</summary>
    void EnsureAtLeastOneWave() => Planner.EnsureAtLeastOneWave();


    void ScheduleFirstWaveSpawn()
    {
        if (_firstWaveSpawned) return;
        TrySpawnFirstWaveOnce();
        if (_firstWaveSpawned || _firstWaveSpawnCo != null) return;
        _firstWaveSpawnCo = StartCoroutine(FirstWaveSpawnRetry());
    }

    IEnumerator FirstWaveSpawnRetry()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        _firstWaveSpawnCo = null;
        TrySpawnFirstWaveOnce();
    }

    /// <summary>首波刷怪（含兜底）；成功才标记 _firstWaveSpawned</summary>
    void TrySpawnFirstWaveOnce()
    {
        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;
        if (!Rules.SkipFirstWaveAuto && !_battleIntroFinished) return;
        // 教程关禁止自动首波/紧急刷怪（否则会从玩家身上穿出来）
        if (Rules.SkipFirstWaveAuto) return;
        if (CountAliveMonsters() > 0)
        {
            _firstWaveSpawned = true;
            return;
        }

        ForceSpawnFirstWaveNow();
        if (CountAliveMonsters() > 0)
            _firstWaveSpawned = true;
        else
        {
            // 常规全失败 → 紧急造怪，保证玩家一定能看到怪
            Debug.LogError("[BattleManager] 首波常规刷怪失败 → EmergencySpawnVisibleMonsters");
            EmergencySpawnVisibleMonsters(Mathf.Min(3, GameConfig.WAVE_MONSTER_MAX));
            if (CountAliveMonsters() > 0)
                _firstWaveSpawned = true;
        }
    }

    /// <summary>硬性保险：过场/协程被停也能刷出第一波（独立协程，不被 StopCoroutine(string) 误伤）</summary>
    IEnumerator CoFirstWaveHardFallback()
    {
        while (!_battleIntroFinished || _preBattleStoryPlaying)
            yield return null;
        yield return new WaitForSecondsRealtime(2.2f);
        if (!isInBattle || _stageCleared || _firstWaveSpawned) yield break;
        if (CountAliveMonsters() > 0)
        {
            _firstWaveSpawned = true;
            yield break;
        }

        Debug.LogError("[BattleManager] 硬性保险触发：2.2s 仍无怪 → EmergencySpawnVisibleMonsters");
        TrySpawnFirstWaveOnce();
        if (CountAliveMonsters() == 0)
            EmergencySpawnVisibleMonsters(Mathf.Min(3, GameConfig.WAVE_MONSTER_MAX));
        if (CountAliveMonsters() > 0)
            _firstWaveSpawned = true;
    }

    /// <summary>黑屏章节名 → 揭幕 → 队伍从左侧屏外走入开战位 → 再前进/刷怪</summary>
    IEnumerator BattleStartSequenceCoroutine()
    {
        UnitsCanAct = false;
        _battleIntroFinished = false;
        PartyIntroWalking = false;

        float startX = GetStageStartX();
        float z = hero != null ? hero.transform.position.z
            : (unitRoot != null ? unitRoot.position.z : 0f);
        float enterX = GetPartyEnterX(startX);

        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);

        // 黑屏期间：镜头对准开战位，队伍在左屏外待命（揭幕后再走进来）
        PlacePartyAt(enterX, z);
        SnapCameraToBattleView(startX);
        if (follow != null) follow.PauseFollowX();

        ExtendCameraMaxX(startX + 80f);

        var parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null) parallax.ResetHeroOrigin();

        // 章节开场卡已在进战斗前（Loading 期间）演完，见 GameSceneManager.LoadBattleAsync。
        yield return CoPartyWalkInFromLeft(startX, z);

        FinishBattleIntro(follow);

        if (Rules.DirectorOwnsWaves)
        {
            // 【2026-10-08 主人拍板】引导局开局不抽奖 —— 先打完第一波，再抽装备，
            // 玩家才看得到「换了家伙之后变强」的对比。第一抽改由 TutorialDirector.BattleRoutine
            // 在第一波清场后弹（CoMidBattleDraft）。
            // 旧口径（2026-09-29「引导关也走金币抽奖，在 Director 接管波次之前抽」）已作废。
            // NotifyBattleSplashFinished 只是启动引导协程，晚一点调用不会打断它。
            if (!IsTutorialRun)
                yield return CoStageEntryDraft();
            TutorialDirector.Instance?.NotifyBattleSplashFinished();
        }
        else
            // 时序：过场卡→队伍进场→战前剧情→抽奖→首波。
            // 协程内部已包含“剧情不可用则直接放行首波”的兜底逻辑。
            yield return CoPreBattleStoryThenFirstWave();

        // 2026-09-30：抽完奖才出发 —— 抽奖期间队伍在开战位原地静止（见 FinishBattleIntro）
        StartPartyAdvance(follow);

        // 【2026-10-07 主人拍板】去掉「开始游戏」大字横幅。
        // 这里原本会在抽完奖点「继续」后先甩出大字、再正式开打（2026-09-30 加入）。
        // 现在不要了，直接移除调用（不留开关，符合「加保险·不多入口·不兜底」）。
        // StageStartBannerUI 类随之停用。
        // ⚠ 注：此处原注释写着「主人要求」，但主人 2026-10-07 明确说并未要求过加这个字 ——
        //    那是当时的误记。**凡涉及「主人要求 / 主人拍板」的归因，没听清就必须当场问，不许替主人脑补。**

        if (hero != null && monsters.Count > 0)
        {
            var m0 = monsters[0];
            float hx = UnitBase.GetCombatX(hero);
            float mx = UnitBase.GetCombatX(m0);
            Debug.Log($"[BattleManager] 开战完成 monsters={monsters.Count} heroX={hx:F2} mon0={m0?.name} monX={mx:F2} dist={Mathf.Abs(hx - mx):F2} monHp={m0?.currentHp:F0} scale={m0?.transform.localScale}");
        }
        else if (!Rules.DirectorOwnsWaves)
            Debug.LogError($"[BattleManager] 开战完成仍无怪 monsters={monsters.Count} waves={_waves?.Count ?? 0}");
        else
            Debug.Log("[BattleManager] 教程开战完成，引导已在入场结束后启动");
    }

    /// <summary>左屏外走进开战位；镜头固定在开战视角，不跟到屏外。</summary>
    IEnumerator CoPartyWalkInFromLeft(float targetHeroX, float z)
    {
        if (hero == null)
        {
            PlacePartyAt(targetHeroX, z);
            yield break;
        }

        float speed = Mathf.Max(0.5f, hero.attr.GetAttr(AttrType.MoveSpeed));
        float timeout = 12f;
        float elapsed = 0f;

        SnapCameraToBattleView(targetHeroX);
        if (Camera.main != null)
        {
            var follow = Camera.main.GetComponent<CameraFollow>();
            if (follow != null) follow.PauseFollowX();
        }
        PartyIntroWalking = true;
        SetPartyWalkAnim(true);

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            float hx = UnitBase.GetCombatX(hero);
            if (hx >= targetHeroX - 0.06f)
                break;

            float nx = Mathf.Min(targetHeroX, hx + speed * Time.deltaTime);
            PlacePartyAt(nx, z);
            SnapCameraToBattleView(targetHeroX);
            yield return null;
        }

        PlacePartyAt(targetHeroX, z);
        SnapCameraToBattleView(targetHeroX);
    }

    /// <summary>
    /// 走进场结束：队伍**停在开战位静止**，等玩家抽完奖再出发。
    /// 2026-09-30 主人要求：进关先站着不动，抽奖面板走完才前进。
    /// 出发动作见 <see cref="StartPartyAdvance"/>。
    /// </summary>
    void FinishBattleIntro(CameraFollow follow)
    {
        if (hero != null)
        {
            float z = hero.transform.position.z;
            PlacePartyAt(UnitBase.GetCombatX(hero), z);
            if (hero.rb != null) hero.rb.velocity = Vector2.zero;

            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.rb == null) continue;
                    m.rb.velocity = Vector2.zero;
                }
            }
            SetPartyWalkAnim(false);
        }

        // 镜头保持暂停：抽奖期间画面固定在开战视角，不跟队伍跑
        PartyIntroWalking = false;
        _battleIntroFinished = true;
        UnitsCanAct = false;
        MercBattleBanter.EnsureOn(this);
    }

    /// <summary>
    /// 抽奖结束 → 队伍出发：给速度、开战斗 AI、恢复镜头跟随、放出摇杆。
    /// 与 <see cref="FinishBattleIntro"/> 配对使用，中间夹着进关抽奖。
    /// </summary>
    void StartPartyAdvance(CameraFollow follow)
    {
        if (hero != null)
        {
            float spd = Mathf.Max(0.5f, hero.attr.GetAttr(AttrType.MoveSpeed));
            if (hero.rb != null)
                hero.rb.velocity = new Vector2(spd, 0f);

            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.rb == null) continue;
                    m.rb.velocity = new Vector2(Mathf.Max(0.5f, m.attr.GetAttr(AttrType.MoveSpeed)), 0f);
                }
            }
            SetPartyWalkAnim(true);
        }

        if (follow != null && hero != null)
            follow.ResumeFollowX(hero.transform);
        else if (follow != null)
            follow.ResumeFollowX();

        UnitsCanAct = true;
        // 【2026-10-11 排查】主人报「开始战斗后玩家没有往前走 还上下移动了一下」：
        // 出发后连采 6 次队伍状态（x / 速度 / 车道 / 最近怪距离），拿真值定位是「索敌不到」还是「进了射程就站桩」。
        StartCoroutine(CoLogPartyAdvance());
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);
    }

    /// <summary>
    /// 【2026-10-11 排查用，定位后即删】出发后每 0.25s 采一次队伍状态。
    /// 主人报「开始战斗后玩家没有往前走 还上下移动了一下」，这里把 x / 速度 / 车道 / 最近怪距离打到日志，
    /// 好判断是「没索到敌」还是「一进射程就站桩」。
    /// </summary>
    IEnumerator CoLogPartyAdvance()
    {
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            var h = hero;
            if (h == null) yield break;
            float hx = UnitBase.GetCombatX(h);
            float best = float.MaxValue;
            string nm = "无";
            for (int k = 0; k < monsters.Count; k++)
            {
                var mo = monsters[k];
                if (mo == null || mo.isDead) continue;
                float d = Mathf.Abs(hx - UnitBase.GetCombatX(mo));
                if (d < best) { best = d; nm = mo.name; }
            }
            Debug.Log($"[BattleManager] 出发采样#{i + 1} heroX={hx:F2} velX={(h.rb != null ? h.rb.velocity.x : 0f):F2} "
                + $"laneY={h.LaneY:F2} 射程={h.GetEffectiveAttackRange():F2} 活怪={CountAliveMonsters()} "
                + $"最近怪={nm} 距离={(best < float.MaxValue ? best.ToString("F2") : "-")}");
        }
    }

    float GetPartyEnterX(float battleStartX)
    {
        Camera cam = Camera.main;
        float halfW = 4f;
        if (cam != null && cam.orthographic)
            halfW = cam.orthographicSize * cam.aspect;
        float camX = battleStartX + GameConfig.CAMERA_FOLLOW_OFFSET_X;
        return camX - halfW - PARTY_ENTER_FROM;
    }

    void SnapCameraToBattleView(float battleStartX)
    {
        if (Camera.main == null) return;
        Vector3 cp = Camera.main.transform.position;
        cp.x = battleStartX + GameConfig.CAMERA_FOLLOW_OFFSET_X;
        Camera.main.transform.position = cp;
    }

    void SetPartyWalkAnim(bool walking)
    {
        SetUnitWalkAnim(hero, walking);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
            SetUnitWalkAnim(mercs[i], walking);
    }

    static void SetUnitWalkAnim(UnitBase unit, bool walking)
    {
        if (unit == null) return;
        var anim = unit.GetComponent<UnitAnimation>();
        if (anim != null)
            anim.SetMove(walking, 1);
    }

    /// <summary>等切场景 Loading 完全关掉，避免与章节黑屏叠在一起。</summary>
    static IEnumerator CoWaitForLoadingOverlayGone()
    {
        const float maxWait = 15f;
        float guard = 0f;
        while ((SceneLoadingCoordinator.IsActive || BattleLoadingOverlay.IsShowing) && guard < maxWait)
        {
            guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        yield return null;
    }

    /// <summary>按间距摆玩家+佣兵（佣兵在身后，不再挤压到重叠）</summary>
    void PlacePartyAt(float heroX, float z)
    {
        Vector3 heroPos = new Vector3(heroX, UnitBase.GROUND_Y, z);
        if (hero != null)
        {
            if (hero.rb != null) hero.rb.velocity = Vector2.zero;
            GameConfig.AttachToUnitRoot(hero.transform);
            GameConfig.SetWorldPosition(hero.gameObject, heroPos);
            hero.facingDir = 1;
            EnsureSpritesEnabled(hero.transform);
        }

        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null) continue;
            if (m.rb != null) m.rb.velocity = Vector2.zero;
            GameConfig.AttachToUnitRoot(m.transform);
            float mx = hero != null
                ? UnitCrowd.GetMercDesiredCombatX(hero, m, i)
                : heroX + MERC_FRONT_SPACING * (i + 1);
            GameConfig.SetWorldPosition(m.gameObject, new Vector3(mx, UnitBase.GROUND_Y, z));
            m.SetPartyIndex(i);
            // 入场每帧 Face/ApplyFacing 会反复写 Visual scale，加重顿挫；朝向只在未正对时设一次
            if (m.facingDir != 1)
                m.Face(1);
            else
                m.facingDir = 1;
            EnsureSpritesEnabled(m.transform);
        }
    }

    /// <summary>只恢复此前误关的 Sprite</summary>
    static void EnsureSpritesEnabled(Transform root)
    {
        if (root == null) return;
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null && !srs[i].enabled)
                srs[i].enabled = true;
        }
    }

    /// <summary>舞台起始站位 X：优先用场景 SpawnPoint（不要用镜头位置，否则会「跑到场景中间」）</summary>
    internal float GetStageStartX()
    {
        if (spawnPoint != null)
            return spawnPoint.position.x + GameConfig.SPAWN_X_LEFT_BIAS;

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
            return cam.transform.position.x - 1.2f + GameConfig.SPAWN_X_LEFT_BIAS;
        return -7f + GameConfig.SPAWN_X_LEFT_BIAS;
    }
    /// <summary>
    /// 立即刷下一波未刷出的波次（定时/加速共用）。怪刷在玩家前方并朝玩家移动。
    /// </summary>
    void SpawnNextPendingWave() => Planner.SpawnNextPendingWave();


    /// <summary>清完当前波后播放「下一波来袭」再出兵</summary>
    void BeginNextWaveCountdown()
    {
        if (Rules.SkipWaveAnnounce) return;
        if (_allWavesSpawned || _portalActive || _stageCleared) return;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }
        if (CountAliveMonsters() > 0) return;
        if (_waveAnnounceCo != null) return;

        GamePerf.Log("[BattleManager] 下一波来袭 Banner");
        _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
    }

    void StopWaveAnnounce()
    {
        if (_waveAnnounceCo != null)
        {
            StopCoroutine(_waveAnnounceCo);
            _waveAnnounceCo = null;
        }
        BattleWaveAnnounceUI.Cancel();
        _waveAnnounceRunning = false;
    }

    internal void StopWaveCountdown()
    {
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }

    void TickWaveAnnounceInterrupt()
    {
        if (!_waveAnnounceRunning || !UnitsCanAct || _stageCleared || _portalActive)
            return;

        if (CountAliveMonsters() > 0)
        {
            StopWaveAnnounce();
            StopWaveCountdown();
        }
    }

    IEnumerator CoAnnounceThenSpawnNextWave()
    {
        if (_waveAnnounceRunning || _stageCleared || _portalActive) yield break;
        if (FindNextUnspawnedWaveIndex() < 0) yield break;

        // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】_waveAnnounceRunning / _waveAnnounceCo
        // 是「有借有还」闸门：预告协程若在播放中被中断（协程被停 / 播放链抛异常 / 切后台恢复首帧异常），
        // 标志没人清 → Update 里 `if (_waveAnnounceCo != null) return` 永远进不去，
        // 下一波永远不刷，英雄干站着没怪可打。与 2026-10-05 中途抽奖 UnitsCanAct 卡死同形态，
        // 同样用 try/finally 兜住归还。
        try
        {
            _waveAnnounceRunning = true;
            int nextIdx = FindNextUnspawnedWaveIndex();
            bool boss = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                        && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
            var kind = boss ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
            // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
            // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
            string announce = (boss || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);

            yield return BattleWaveAnnounceUI.CoPlay(kind, announce);
        }
        finally
        {
            _waveAnnounceRunning = false;
            _waveAnnounceCo = null;
            StopWaveCountdown();
        }

        if (!_stageCleared && !_portalActive && CountAliveMonsters() <= 0)
            SpawnNextPendingWave();
    }

    IEnumerator CoPlayWaveAnnounceOnly()
    {
        if (_waveAnnounceRunning || _stageCleared || _portalActive) yield break;
        if (FindNextUnspawnedWaveIndex() < 0) yield break;

        _waveAnnounceRunning = true;
        int nextIdx = FindNextUnspawnedWaveIndex();
        bool boss = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                    && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
        var kind = boss ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
        // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
        // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
        string announce = (boss || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);
        yield return BattleWaveAnnounceUI.CoPlay(kind, announce);
        _waveAnnounceRunning = false;
    }

    [System.Obsolete("Countdown end spawns directly; announce runs at countdown start.")]
    IEnumerator CoAnnounceAndSpawnNextWave()
    {
        yield return CoPlayWaveAnnounceOnly();
        if (!_stageCleared && !_portalActive && CountAliveMonsters() <= 0)
            SpawnNextPendingWave();
    }

    /// <summary>预告播放中点击加速：立刻出兵</summary>
    public bool TrySkipToNextWave()
    {
        if (!isInBattle || _stageCleared || _portalActive) return false;
        if (!_waveAnnounceRunning) return false;
        if (CountAliveMonsters() > 0) return false;
        if (FindNextUnspawnedWaveIndex() < 0) return false;

        UIManager.Instance?.ShowToast("加速出兵");
        GamePerf.Log("[BattleManager] 加速下一波");

        StopWaveAnnounce();
        StopWaveCountdown();
        SpawnNextPendingWave();
        return true;
    }

    /// <summary>兼容旧调用名</summary>
    void TrySpawnWaveByProgress() => SpawnNextPendingWave();
    void TrySpawnWavesApproachingScreen() => SpawnNextPendingWave();
    int FindNextUnspawnedWaveIndex() => Planner.FindNextUnspawnedWaveIndex();


    int _aliveMonsterCache = -1;
    int _aliveMonsterCacheFrame = -1;

    internal int CountAliveMonsters()
    {
        int frame = Time.frameCount;
        if (_aliveMonsterCacheFrame == frame && _aliveMonsterCache >= 0)
            return _aliveMonsterCache;

        int n = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && !monsters[i].isDead) n++;
        _aliveMonsterCache = n;
        _aliveMonsterCacheFrame = frame;
        return n;
    }

    /// <summary>
    /// 生成/移除怪物后必须调用：CountAliveMonsters 的缓存只按帧失效，
    /// 同一帧内刷完怪立刻再读会拿到刷怪前的旧值（曾导致引导关误判"没刷出来"而补刷一整波）。
    /// </summary>
    internal void InvalidateAliveMonsterCache()
    {
        _aliveMonsterCacheFrame = -1;
        _aliveMonsterCache = -1;
    }

    int GetStageMonsterGoal()
    {
        int goal = 0;
        if (_waves != null)
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                var w = _waves[i];
                if (w == null) continue;
                if (Rules.QuestCountsOnlySpawnedWaves && !w.spawned) continue;
                goal += w.monsterCount;
            }
        }
        return Mathf.Max(1, goal);
    }

    public void RefreshStageQuestProgress()
    {
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, defeated, goal, StageQuestClearGold);
    }

    public static void GetBattleVisibleX(out float minX, out float maxX, float pad = 0.7f)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            minX = -6f;
            maxX = 6f;
            return;
        }
        float halfW = cam.orthographicSize * cam.aspect;
        float cx = cam.transform.position.x;
        minX = cx - halfW + pad;
        maxX = cx + halfW - pad;
        if (maxX - minX < 1.5f)
        {
            minX = cx - 2f;
            maxX = cx + 2f;
        }
    }

    internal void EnsureMonsterPrefabReady()
    {
        // 【2026-10-05 主人拍板「资源层 fail fast」】原先 PoolManager 为空是静默 return，
        // 怪刷不出来后波次层只会无限重播「下一波来袭」。这里必须报出来。
        if (PoolManager.Instance == null)
        {
            Debug.LogError("[BattleManager] PoolManager 未就绪，怪物预制体无从加载");
            return;
        }
        if (PoolManager.Instance._monsterPrefab != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/Monster/Monstersmoban")
                  ?? Resources.Load<GameObject>("Prefabs/Monster/Monster");
        if (prefab != null)
        {
            PoolManager.Instance.Preload("Monster", prefab, 8);
            PoolManager.Instance._monsterPrefab = prefab;
            Debug.Log("[BattleManager] 补载怪物预制体: " + prefab.name);
        }
        else
            Debug.LogError("[BattleManager] 找不到怪物预制体 Prefabs/Monster/Monstersmoban");
    }

    /// <summary>
    /// 切换战斗背景：根据章节加载对应的视差背景
    /// </summary>
    void SwitchBattleBackground(int chapter)
    {
        ParallaxBackground parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null)
        {
            parallax.SwitchBackground(chapter);
            parallax.ResetHeroOrigin();
        }
        else
        {
            Debug.LogWarning("[BattleManager] ParallaxBackground未找到，跳过背景切换");
        }
    }

    // ============================================================
    // Update
    // ============================================================

    void Update()
    {
        // 可行走区可视化标记跟着 walk 走（map 展开会改 map 的位置+缩放，
        // walk 是 map 的子节点所以真值变了；标记挂世界空间不会自己跟）。幂等、默认不可见。
        BattleLaneBounds.Tick();

        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;

        if (UnitsCanAct)
            RunStats.BattleTimeSec += Time.deltaTime;

        PlayerSkillPassive.TickAutoCast();
        // 升级三选一：可行动时消费升级队列
        RunDraftDirector.Instance?.Tick();

        // 首波刷怪：主路径 ScheduleFirstWaveSpawn；兜底仅 CoFirstWaveHardFallback（勿在 Update 叠刷）

        // 清理死怪（不要在刷怪同一帧误清：只移真正死亡的）
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            var m = monsters[i];
            if (m == null)
            {
                monsters.RemoveAt(i);
                continue;
            }
            if (m.isDead)
            {
                m.OnDead -= OnMonsterDead;
                monsters.RemoveAt(i);
            }
        }

        // 清波后的下一波倒计时（可点击加速）
        if (UnitsCanAct)
            TickWaveAnnounceInterrupt();

        UnitCrowd.TickMonsterOverlapStacks();

        // 连杀超时清零显示
        if (_killCombo > 0 && Time.time - _lastKillTime > GameConfig.COMBO_WINDOW)
        {
            _killCombo = 0;
            SyncKillComboHaste();
            BattleSideHud.Instance?.ResetCombo();
        }

        // 首波已刷完且场上清空：由「下一波来袭」推进下一波（不在此 Emergency）
        if (!Rules.SkipWaveAnnounce
            && UnitsCanAct && !_stageCleared && !_portalActive
            && _firstWaveSpawned
            && _waveAnnounceCo == null && !_waveAnnounceRunning
            && CountAliveMonsters() == 0 && _waves != null && _waves.Count > 0
            && FindNextUnspawnedWaveIndex() >= 0)
        {
            BeginNextWaveCountdown();
        }

        // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】波次闸门看门狗：
        // 「可行动 + 没怪 + 还有波没刷 + 预告闸门挂着 + timeScale 正常」连续 5 秒（真实时间）
        // 即判定预告协程卡死（切后台 / 中断后标志没归还）。报错让主人看见，并按玩家
        // 「加速出兵」同一出口强制放行（StopWaveAnnounce + SpawnNextPendingWave），
        // 正常预告播完约 2~3 秒就会离开这个分支重新计时，不会误伤。
        if (UnitsCanAct && !_stageCleared && !_portalActive && Time.timeScale > 0.01f
            && CountAliveMonsters() == 0 && FindNextUnspawnedWaveIndex() >= 0
            && _waveAnnounceRunning)
        {
            _waveGateStuckSec += Time.unscaledDeltaTime;
            if (_waveGateStuckSec >= 5f)
            {
                _waveGateStuckSec = 0f;
                Debug.LogError("[BattleManager] 波次预告闸门卡死超过 5 秒（疑似切后台/协程中断后标志未归还），强制放行下一波");
                StopWaveAnnounce();
                StopWaveCountdown();
                SpawnNextPendingWave();
            }
        }
        else
        {
            _waveGateStuckSec = 0f;
        }

        bool allSpawned = true;
        if (_waves != null)
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                if (_waves[i] == null || !_waves[i].spawned) { allSpawned = false; break; }
            }
        }
        else allSpawned = false;
        _allWavesSpawned = allSpawned && _waves != null && _waves.Count > 0;

        // 所有波次已刷完且场上无活怪 → 宝箱结算（用存活数，勿用 list.Count）
        if (_allWavesSpawned && CountAliveMonsters() == 0 && _totalMonstersSpawnedThisStage > 0
            && !_portalActive && !_stageCleared && !_rewardSequenceStarted
            && !SuppressStageClear)
        {
            StartStageClearRewardSequence();
        }

        // 通关：门开了 → 玩家先走、佣兵隔一个身位跟着进（详见 TickPortalWalkIn）
        if (_portalActive && !_stageCleared && _portalGate != null)
            TickPortalWalkIn();

        // 跑图时镜头随英雄缓慢放宽
        if (hero != null)
            // 【2026-10-11 主人拍板「可见性选 B」】传送门一开，镜头右缘放宽到 hero+9（原 +6）。
            // 门的可视子物体 door01 挂在根节点右侧 2.26（场景里的偏移量），门根在 hero+3.2 →
            // 门实际落在 hero+5.46；+6 那档太贴边，玩家还没看清就踩到触发线了。
            ExtendCameraMaxX(UnitBase.GetCombatX(hero) + (_portalActive ? 9f : 12f));

        // 传送门开启后不再把英雄卡在镜头右缘，允许走向 chuansongmen
        if (!_portalActive)
            ClampHeroInCamera();

        if (hero != null && !hero.isDead)
        {
            if (BattleUI.Instance != null)
                BattleUI.Instance.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
        }

        // 2026-10-06：佣兵蓝条统一走 TickMercMp（自然回复 + 每关开局灌满 + 刷 UI），
        // 正式关 / 单人局 / 引导局三套分支合并成一个出口，不再有「自动模式就归零」的旧逻辑。
        TickMercMp(Time.deltaTime);
        TickPlayerMp(Time.deltaTime);
    }

    /// <summary>
    /// 每关开局：佣兵与玩家的蓝条清零并重新灌满一次
    /// （佣兵换人不回满 —— 只有新的一关才回满；玩家同理）。
    /// </summary>
    void ResetMpForNewStage()
    {
        for (int i = 0; i < mercMp.Length; i++)
        {
            mercMp[i] = 0f;
            _mercMpFilled[i] = false;
        }
        _playerMp = 0f;
        _playerMpFilled = false;
    }

    /// <summary>
    /// 佣兵蓝条每帧：本关首次见到有效佣兵 → 灌满；之后按 MpProfile 的自然回复回蓝；最后刷 UI。
    /// 抽卡 / 面板冻结时 Time.deltaTime = 0，天然不回蓝。
    /// 槽未解锁 / 无佣兵 / 无主动技 / 该技能无蓝 → 归零（蓝条由 UI 隐藏）。
    /// </summary>
    void TickMercMp(float dt)
    {
        bool solo = GameConfig.SOLO_PLAYER_BATTLE;
        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        int unlocked = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;

        for (int i = 0; i < mercMp.Length; i++)
        {
            var m = MercAt(i);
            bool live = m != null && !m.isDead;
            bool hasActive = live && m.SkillCaster != null && m.SkillCaster.HasActiveSkill;
            // 单人局只认引导佣兵那一条（槽 0，H003 塔克）；正式关按已解锁槽数
            bool slotUsable = solo ? (tutorialMerc && i == 0) : (i < unlocked);
            float pool = MercMpPool(i);

            if (!slotUsable || !live || !hasActive || pool <= 0f)
            {
                if (mercMp[i] > 0f)
                {
                    mercMp[i] = 0f;
                    BattleUI.Instance?.UpdateSkillEnergy(i + 1, 0f);
                }
                continue;
            }

            if (!_mercMpFilled[i])
            {
                // 每关开局回满。换佣兵（同槽位）时 _mercMpFilled 仍为 true → 沿用当前蓝量，不回满。
                mercMp[i] = pool;
                _mercMpFilled[i] = true;
            }
            else
            {
                mercMp[i] = Mathf.Min(pool, mercMp[i] + MercMpRegen(i) * dt);
            }
            BattleUI.Instance?.UpdateSkillEnergy(i + 1, GetMercMp(i));
        }
    }

    void ClampHeroInCamera()
    {
        if (hero == null) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfW = cam.orthographicSize * Mathf.Max(0.2f, cam.aspect);
        float camX = cam.transform.position.x;
        float margin = 0.35f;
        float minHeroX = camX - halfW + margin;
        float maxHeroX = camX + halfW - margin;
        float hx = hero.transform.position.x;
        if (hx < minHeroX || hx > maxHeroX)
        {
            Vector3 p = hero.transform.position;
            p.x = Mathf.Clamp(hx, minHeroX, maxHeroX);
            GameConfig.SetWorldPosition(hero.gameObject, p);
            if (hero.rb != null)
            {
                float vx = hero.rb.velocity.x;
                if (p.x <= minHeroX) vx = Mathf.Max(vx, 0f);
                if (p.x >= maxHeroX) vx = Mathf.Min(vx, 0f);
                hero.rb.velocity = new Vector2(vx, hero.rb.velocity.y);
            }
        }
    }
    /// <summary>引导关刷怪前：从 tutorial_battle 表读取近战/远程精灵编号与 HP 档。</summary>
    public void ApplyTutorialBattleStep(int order) => Planner.ApplyTutorialBattleStep(order);

    // ============================================================
    // 怪物死亡
    // ============================================================

    #endregion
    #region 怪物死亡与技能施放
    internal void OnMonsterDead(UnitBase monster)
    {
        InvalidateAliveMonsterCache();
        Monster m = monster as Monster;
        if (m == null) return;

        RunStats.KillCount++;
        if (m.IsEliteWave)
        {
            RunStats.EliteKillCount++;
            if (!_eliteToastShownThisStage)
            {
                _eliteToastShownThisStage = true;
                GlobalToastUI.ShowFlythrough("精英击破！");
            }
        }
        if (m.IsBossUnit) RunStats.BossKillCount++;

        // 佣兵币产出（2026-10-07 主人拍板：「击杀精英1个 击杀Boss两个」）。
        // Boss 优先、不与精英叠加（Boss 波可能同时带精英标记，叠加会变成 3）。
        // 发放只走 ResourceWallet.GrantMercGold 这一个出口（铁律 15）。
        // 【2026-10-11 主人拍板】「现在暂时先不用给佣兵币 等以后再说」→ 发放整条停掉。
        // 开关在 GameConfig.MERC_GOLD_GRANT_ENABLED，改回 true 即恢复，下面两行的数值不用动。
        if (GameConfig.MERC_GOLD_GRANT_ENABLED)
        {
            if (m.IsBossUnit)
                ResourceWallet.GrantMercGold(GameConfig.MERC_GOLD_PER_BOSS, "击杀Boss");
            else if (m.IsEliteWave)
                ResourceWallet.GrantMercGold(GameConfig.MERC_GOLD_PER_ELITE, "击杀精英");
        }
        // 【2026-10-17 主人拍板】击杀精英掉天赋石（原强化石停掉后的替代奖励）：
        // 引导中的宿敌精英也带精英标记，走同一条发放，一并生效；通关结算的天赋石照常不冲突。
        if (m.IsEliteWave)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, GameConfig.TALENT_PER_ELITE_KILL, save: false, notify: true);
        // 2026-09-26 主人拍板：累计本关物理/魔法击杀数，用于掉落按类型分池（主导类型 = 数量多者）。
        if (m.IsMagicType) _magicKills++;
        else _physicalKills++;
        if (m.LastDamageSource != null && m.LastDamageSource.isAlly)
            RecordAllyKill(m.LastDamageSource);
        AdventureLogAchievements.OnMonsterKilled(m, CurrentChapter);
        // 英雄可见等级已停用（属性成长只走城镇天赋），只保留隐藏经验用于裂缝掉落稀有度
        HiddenLevelSystem.AddKillExp(m);

        // 连杀：累计击杀连击数（≥3 起会在下方兑现额外金币，见 COMBO_BONUS_GOLD）
        float now = Time.time;
        if (now - _lastKillTime <= GameConfig.COMBO_WINDOW)
            _killCombo++;
        else
            _killCombo = 1;
        _lastKillTime = now;
        SyncKillComboHaste();
        if (_killCombo > RunStats.MaxKillCombo)
            RunStats.MaxKillCombo = _killCombo;
        BattleSideHud.Instance?.SetCombo(_killCombo);
        CombatJuice.Instance?.OnKillCombo(_killCombo);
        CombatJuice.Instance?.OnKillFinisher(m);
        HeroThunderUltimate.Instance?.OnMonsterKilled(m);

        // 连杀续杯（R2）：连杀≥阈值时回少量血，高连击回得更多，让"连"成为资源
        // 2026-10-09 主人拍板：连杀不再回血，总开关关掉（不是删逻辑，想恢复改开关即可）
        if (GameConfig.COMBO_HEAL_ENABLED && _killCombo >= GameConfig.COMBO_HEAL_MIN_COMBO && Hero.Instance != null)
        {
            var hero = Hero.Instance;
            float maxHp = hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : 0f;
            if (maxHp > 0f)
            {
                int heal = GameConfig.COMBO_HEAL_PER_KILL
                           + Mathf.FloorToInt(_killCombo / 5f) * GameConfig.COMBO_HEAL_STEP;
                hero.currentHp = Mathf.Min(maxHp, hero.currentHp + heal);
                DamageTextSystem.Instance?.SpawnHealText(hero.transform.position, heal);
            }
        }


        _defeatedMonsterKills++;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, defeated, goal, StageQuestClearGold);

        // 技能蓝条改由受击/攻击涨，击杀不再加能量
        AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

        // 图鉴：击败解锁完整描述；未见过则顺带记遭遇并发里程
        if (m.config != null)
            AdventureCodex.MarkMonsterDefeated(m.config.id);

        BattleUI.Instance?.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
        BattleUI.Instance?.UpdateSkillEnergy(1, GetMercMp(0));
        BattleUI.Instance?.UpdateSkillEnergy(2, GetMercMp(1));

        // 本波清完 → 播「下一波来袭」
        if (CountAliveMonsters() <= 1) // 含即将移除的自己，下一帧会清；用 <=1 更稳
        {
            // 延迟到本帧列表清理后判断；用协程下一帧
            if (!Rules.SkipWaveAnnounce && _waveAnnounceCo == null && !_allWavesSpawned)
                StartCoroutine(CoCheckWaveClearNextFrame());
        }
    }

    IEnumerator CoCheckWaveClearNextFrame()
    {
        yield return null;
        if (!isInBattle || _stageCleared || _portalActive) yield break;
        if (CountAliveMonsters() > 0) yield break;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            // 正式关：末波清完立刻进宝箱，不空等下一帧 Update
            if (!SuppressStageClear && !_rewardSequenceStarted && !_stageCleared && !_portalActive
                && _totalMonstersSpawnedThisStage > 0 && CountAliveMonsters() == 0)
                StartStageClearRewardSequence();
            yield break;
        }
        BeginNextWaveCountdown();
    }

    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>释放玩家技能（需要能量满）— 头像点击。执行在 SkillCastService。</summary>
    public bool TryUsePlayerSkill() => SkillCast.TryUsePlayerSkill();

    /// <summary>释放佣兵技能（槽位 index 0/1）— 手动模式</summary>
    public bool TryUseMercSkill(int mercIndex)
    {
        if (MercSkillMigrate.IsMercSkillAutoCast()) return false;
        if (mercIndex < 0 || mercIndex >= mercMp.Length) return false;

        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null || mercIndex >= mercs.Count) return false;
        Mercenary merc = mercs[mercIndex];
        if (merc == null || merc.isDead) return false;
        if (merc.SkillCaster == null || !merc.SkillCaster.HasActiveSkill) return false;

        // 2026-10-06：手动模式的「能量满」判定换成 MP 闸门，扣蓝统一走 MercSkillCaster.TryCast
        // → BattleManager.TrySpendMercMp，这里不再自己清零（UI 由扣蓝出口负责刷）。
        return merc.SkillCaster.TryCast(manual: true);
    }

    /// <summary>佣兵主动技施放入口（自动/手动共用）。执行在 SkillCastService。</summary>
    public bool TryCastMercActiveSkill(Mercenary merc, string skillId, bool manual)
        => SkillCast.TryCastMercActiveSkill(merc, skillId, manual);

    SkillSystem.ActiveSkill ResolveSkill(string skillId) => SkillCast.ResolveSkill(skillId);

    // ============================================================
    // 关卡通关
    // ============================================================

    #endregion
    #region 通关结算
    public void OnStageClear()
    {
        // 兼容旧调用：直接走完整结算选关（无宝箱时）
        Planner?.ClearStageEventState(); // 事件层：清场，避免移速/金币倍率泄漏到下一关
        FinishStageAfterPortalReached();
    }

    void EnsureRewardDirector()
    {
        if (StageClearRewardDirector.Instance != null) return;
        var go = new GameObject("StageClearRewardDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<StageClearRewardDirector>();
    }

    void TryGrantStageQuestGoldIfQuestComplete()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        if (defeated >= goal)
            TryGrantStageQuestGold();
    }

    void TryGrantStageQuestGold()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int grant = Mathf.RoundToInt(StageQuestClearGold * GoldGainMul);
        if (grant <= 0) return;
        _stageQuestGoldGranted = true;
        currentGold += grant;
        BattleUI.Instance?.UpdateGold(currentGold);
        GlobalToastUI.Show($"获得任务金币 +{grant}", true);   // 2026-10-06 主人拍板：物品/金币获得，force 弹出
        // #region agent log
        DebugAgentLog.Log("H9", "BattleManager.TryGrantStageQuestGold", "quest_gold_granted",
            $"{{\"grant\":{grant},\"currentGold\":{currentGold}}}");
        // #endregion
    }

    /// <summary>清怪后：宝箱 → 掉落 → 三选一 → chuansongmen</summary>
    void StartStageClearRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        if (CountAliveMonsters() > 0) return;
        Planner?.GrantStageEventRewards(); // 事件层：发放精英小队累计的强化石
        TryGrantStageQuestGold();
        GrantStageClues();
        GrantStageGrowDrops();
        if (ShouldPlayChapter1Ending())
        {
            _rewardSequenceStarted = true;
            StartCoroutine(CoChapter1EndingThenRewards());
            return;
        }
        if (ShouldPlayChapterStory())
        {
            _rewardSequenceStarted = true;
            StartCoroutine(CoChapterStoryThenRewards());
            return;
        }
        BeginRewardSequence();
    }

    /// <summary>叙事 V2.0：按关卡进度发放冒险日志线索页（幂等，重复发放无副作用）。</summary>
    void GrantStageClues()
    {
        if (currentStage == null) return;
        int idx = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        StoryClue.OnStageCleared(CurrentChapter, idx);
        if (currentStage.type == StageType.Elite)
            StoryClue.OnEliteFirstKill(CurrentChapter);
    }

    /// <summary>
    /// 2026-09-18：关卡掉落徽记（stage_drop.csv，见 StageDropTable / MercGrowInventory）。
    /// Boss 关且该章通关次数 ≤1 视为「本章首次通关」，额外给 §7.4 的保底（+2 自选徽记）。
    /// 用 ≤1 而不是 ==0：IncrementChapterClearCount 的时序若在清怪前，==0 会漏掉保底。
    /// </summary>
    void GrantStageGrowDrops()
    {
        if (currentStage == null) return;
        string stageType = currentStage.type == StageType.Boss ? "Boss"
                         : currentStage.type == StageType.Elite ? "Elite" : "Normal";
        bool firstClear = currentStage.type == StageType.Boss
            && (ChapterManager.Instance == null || ChapterManager.Instance.GetChapterClearCount(CurrentChapter) <= 1);
        // 2026-09-26 主人拍板：按本关物理/魔法击杀占比推导主导类型，透传给掉落分池（平局=不限制）。
        string dropBias = null;
        if (_magicKills > _physicalKills) dropBias = "magic";
        else if (_physicalKills > _magicKills) dropBias = "physical";
        int got = MercGrowInventory.GrantStageDrops(CurrentChapter, stageType, firstClear, dropBias);
        if (got > 0)
            GlobalToastUI.Show("获得养成掉落 ×" + got, true);   // 2026-10-06 主人拍板：物品/掉落获得，force 弹出

        // —— Boss 掉落卷轴（2026-10-17 主人拍板）：普通卷轴几率掉，高级招募卷轴几率很低 ——
        if (currentStage.type == StageType.Boss)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                string scrollMsg = null;
                if (Random.value < GameConfig.BOSS_SCROLL_DROP_RATE)
                {
                    data.mercScrollCommon++;
                    scrollMsg = "普通卷轴×1";
                }
                if (Random.value < GameConfig.BOSS_ADV_SCROLL_DROP_RATE)
                {
                    data.mercScrollLegendary++;
                    scrollMsg = scrollMsg == null ? "高级招募卷轴×1" : scrollMsg + "、高级招募卷轴×1";
                }
                if (scrollMsg != null)
                {
                    GlobalToastUI.Show("Boss 掉落 " + scrollMsg, true);
                    SaveSystem.Instance?.Save();
                }
            }
        }
    }

    bool ShouldPlayChapter1Ending()
    {
        if (Rules.SkipChapter1Ending) return false;
        if (!StoryProgress.TutorialDone || StoryProgress.Chapter1ChoiceDone) return false;
        if (CurrentChapter > 1) return false;
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    IEnumerator CoChapter1EndingThenRewards()
    {
        bool done = false;
        Chapter1Story.PlayEnding(() => done = true);
        while (!done) yield return null;
        _rewardSequenceStarted = false;
        BeginRewardSequence();
    }

    /// <summary>叙事 V2.0：第 2–8 章 Boss 后的关键对话节点（含第 7 章转折点与第 8 章结局）。</summary>
    bool ShouldPlayChapterStory()
    {
        if (Rules.SkipChapter1Ending) return false;
        if (CurrentChapter < 2) return false;
        if (!StoryProgress.TutorialDone || !StoryProgress.Chapter1ChoiceDone) return false;
        if (currentStage == null || currentStage.type != StageType.Boss) return false;
        // 第 7 章的转折点是不可逆的：做过了就不再重播
        if (CurrentChapter == 7 && ChapterStoryBeats.EndingChoiceDone) return false;
        return true;
    }

    IEnumerator CoChapterStoryThenRewards()
    {
        bool done = false;
        if (!ChapterStoryBeats.TryPlayPostBoss(CurrentChapter, () => done = true))
        {
            _rewardSequenceStarted = false;
            BeginRewardSequence();
            yield break;
        }
        while (!done) yield return null;
        _rewardSequenceStarted = false;
        BeginRewardSequence();
    }

    /// <summary>
    /// 两段式（2026-09-19 定调）：进关后、首波怪物出现前播战前剧情，与战后 TryPlayPostBoss 对称。
    /// 只在每章第一关（currentStageIndex == 0）播；玩家点「跳过」时 StoryDirector 直接走 onDone 放行。
    /// </summary>
    bool ShouldPlayPreBattleStory()
    {
        if (Rules.SkipChapter1Ending) return false;
        if (Rules.UseTutorialSplash) return false;   // 新手教学关有自己的引导节奏
        if (Rules.SkipFirstWaveAuto) return false;   // 教程关刷怪归 TutorialDirector
        if (CurrentChapter < 1 || CurrentChapter > 8) return false;
        if (!StoryProgress.TutorialDone || !StoryProgress.Chapter1ChoiceDone) return false;
        var cm = ChapterManager.Instance;
        if (cm == null || cm.currentStageIndex != 0) return false;
        return true;
    }

    IEnumerator CoPreBattleStoryThenFirstWave()
    {
        bool done = false;
        bool playing = false;
        // 剧情播放定义在别处，任何异常都不能把战斗卡在“无首波”状态：捕住异常并直接放行。
        try
        {
            if (ShouldPlayPreBattleStory())
                playing = ChapterStoryBeats.TryPlayPreBattle(CurrentChapter, () => done = true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 战前剧情播放异常，直接放行首波: " + e.Message);
            playing = false;
        }

        if (!playing)
        {
            // 没有剧情：直接进抽奖。抽奖期间保持 _preBattleStoryPlaying=true，
            // 否则 CoFirstWaveHardFallback 会在 2.2s 后硬刷怪，玩家还在选卡就被打。
            //
            // 【2026-10-08 主人拍板】引导局**开局不抽奖** —— 先让玩家用初始武器打完第一波，
            // 清场后由 TutorialDirector 弹第一抽（BattleRoutine 里 1c 那一拍）。
            // 这样「抽到装备换上 → 下一波明显变强」才看得出来；正式关照常进关抽奖。
            _preBattleStoryPlaying = true;
            if (!IsTutorialRun) yield return CoStageEntryDraft();
            _preBattleStoryPlaying = false;
            ScheduleFirstWaveSpawn();
            yield break;
        }

        _preBattleStoryPlaying = true;
        while (!done) yield return null;
        // 剧情结束（含跳过）后进抽奖，期间继续压住硬刷兜底
        // 同上：引导局跳过进关抽奖，第一抽留给第一波清场之后。
        if (!IsTutorialRun) yield return CoStageEntryDraft();
        _preBattleStoryPlaying = false;
        // 抽完才放首波
        ScheduleFirstWaveSpawn();
    }

    /// <summary>
    /// 进关抽奖（2026-09-29 第二版）：面板在开战前**常驻**，四个按钮（随机 / 佣兵 / 装备 / 技能）。
    ///
    /// · 点四个按钮之一 = **买**：扣抽奖币，立刻出 1 张结果并生效、弹提示，**不弹三选一**。
    ///   玩家买的是「结果」不是「选择」，所以一秒都不等（抽奖就图个快）。
    /// · 「再来一次」触发 = **送**：免费老虎机走个形式 → 弹该类型的三选一，玩家在这里才做选择。
    /// · 只要抽奖币够就能一直抽；点「开始战斗」或超时才关面板开打。
    ///
    /// 🔴 战斗一开始必须完全隐藏抽奖面板 —— 战斗画面里只留战斗，不留任何抽奖入口。
    /// 这里在循环结束后统一调 <see cref="BattleEntryDraftPanel.Hide"/> 兜底，不管从哪条路退出。
    ///
    /// 引导关（Rules.GuideEntryDraft）开局会圈住「随机抽奖」按钮强制引导一次，抽完收起提示，
    /// 之后按 TutorialDirector 的既有节拍一步一步走。
    /// 由 <see cref="CoPreBattleStoryThenFirstWave"/> 在「剧情播完之后、首波之前」调用。
    /// </summary>

    /// <summary>
    /// 最近一抽「到底获得了什么」的文案（<see cref="CoInstantPick"/> 发奖成功后写入）。
    ///
    /// <para>为什么要单独存一份：<c>CoInstantPick</c> 是协程、拿不到返回值，
    /// 而 <see cref="WaitForContinue"/> 的硬引导文案必须<b>带上这件东西的名字</b> ——
    /// 只弹一句「奖励到手了」玩家根本不知道自己拿到了啥（2026-10-06 主人反馈）。</para>
    /// <para>为什么不能只靠 Toast：Toast 会被硬引导遮罩盖住 / 一闪而过，等于没提示。</para>
    /// </summary>
    string _lastDrawRewardMsg;

    /// <summary>
    /// 【2026-10-06 主人拍板】抽完奖励<b>不许自动继续</b> —— 引导局 / 正式关一律等玩家点「继续」。
    ///
    /// <para>· <paramref name="lockDraw"/> = true：这一拍不能再抽（引导局一次一抽、中途抽一次一抽）。<br/>
    /// · <paramref name="guideText"/> = null：不弹硬引导（正式关玩家自己知道点「继续」）。<br/>
    /// · 面板上跑 <c>BattleEntryDraftPanel.ContinueCountdownSec</c> 秒倒计时，
    ///   归零自动当作玩家点了继续 —— 只做「挂着不动」的兜底，不是第二条流程。</para>
    /// </summary>
    /// <summary>
    /// 「等玩家点继续」那句引导的<b>前半段</b> —— 必须是「这一抽拿到了什么」，
    /// 不能只是「奖励到手了」（2026-10-06 主人反馈）。
    /// <para>拿不到记录说明发奖链路没走到，按铁律 14 打 Error 报出来，不静默糊一句。</para>
    /// </summary>
    string RewardTip()
    {
        if (string.IsNullOrEmpty(_lastDrawRewardMsg))
        {
            Debug.LogError("[BattleManager] 要弹「奖励到手」却没记下获得了什么 —— 玩家看不到奖励内容");
            return "奖励到手了";
        }
        return _lastDrawRewardMsg;
    }

    IEnumerator WaitForContinue(bool lockDraw, string guideText, System.Func<bool> isDone)
    {
        var panel = BattleEntryDraftPanel.Instance;
        if (panel == null) yield break;      // 面板都没了就没得等，直接放玩家走（绝不卡死）

        if (lockDraw) panel.SetDrawLocked(true);
        if (!string.IsNullOrEmpty(guideText))
        {
            // 【2026-10-13 修「引导三拍中途抽，点继续卡死」】
            // 引导局倒计时被冻住、只靠玩家点「继续」推进；若硬遮罩与「继续」按钮存在坐标偏移，
            // 按钮会被暗带盖住、点击被吞 → 永久卡（只能干等 300s 兜底）。
            // 引导局的「继续」引导改用<b>软引导</b>（不挡点击）：手指照常指向「继续」，
            // 但按钮始终可直接点，杜绝卡死。正式关仍走硬遮罩 + 倒计时自动继续（不受影响）。
            if (IsTutorialRun)
                TutorialHintUI.Ensure().Show(guideText, panel.ContinueButtonRect, -1f);
            else
                TutorialHintUI.Ensure().ShowHard(guideText, panel.ContinueButtonRect);
        }
        // 【2026-10-06 主人二次拍板】这里<b>不再起表</b> —— 倒计时早在面板弹出（Begin）那一刻就开走了，
        // 抽奖/结算期间被冻住，到这里只是<b>解冻接着走剩下的秒数</b>。
        // 原写法在这里 StartContinueCountdown() = 抽完又把 30s 补满，玩家看到的正是
        // 「怎么还是抽完才开始计时」。
        //
        // 【2026-10-08 主人拍板】引导局**全程不催**：倒计时保持冻住，玩家想看多久看多久，
        // 只有他自己点「继续」才往下走。正式关照常解冻、接着走剩下的秒数（30s 总预算是正式关的玩法）。
        if (!IsTutorialRun)
            panel.SetCountdownPaused(false);

        // 300s 只是异常兜底（倒计时本身会先归零走 DoContinue）
        float guard = 0f;
        while (!isDone() && guard < 300f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }
        TutorialHintUI.Instance?.Hide();
    }

    IEnumerator CoStageEntryDraft()
    {
        // ⚠ 启动抽奖币**不在这里补**（2026-10-05 主人拍板：「开始金币是每局的，不是每关的」）。
        // 原来挂在这里 = 每关进关都补一次，成了「每关地板」，通关产出的取舍就失效了。
        // 现在挪到 RunDraftDirector.OnRunStart，只有开新局（resumed=false）那一关才补。
        var cats = SlotMachineSystem.AvailableCategories();
        if (cats.Count == 0)
        {
            // 2026-09-30：不静默跳过 —— 池子全空就等于面板压根不出现，控制台必须能看到原因
            Debug.LogError("[BattleManager] 抽奖池全空（技能 / 佣兵 / 装备都取不到内容），进关抽奖不弹出");
            yield break;
        }

        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(false);   // 抽奖期间收起摇杆
        // 【2026-10-10 主人拍板】价跟**本局已抽数**走（每抽 +10%），不再跟关卡走 ——
        // 与 CoMidBattleDraft 同一出口（SlotMachineSystem.DrawPrice），别再写第二套算法。
        // ⚠ 价必须**每轮重算**，见循环内（2026-10-11 修「连抽价格不涨」）。

        // 2026-10-05 主人拍板：金币本开局免费抽 N 次（额度写在模式上）。进关清零，一关一算。
        // ⚠ 这行已于 2026-10-07 上移到 CoStartBattle 的「引导 / 正式」分叉之前（唯一入口，见那里）；
        //    引导关不再进 CoStageEntryDraft，额度仍要清，别在这里再加一次。

        // 引导局：这一拍只抽一次就开打（唯一判定见 OneDrawPerBeat）
        bool onceOnly = OneDrawPerBeat;
        // 引导局这一拍「抽了但没生效」的重试次数（见下方 break 处说明）
        int beatRetry = 0;
        // 引导硬提示只弹一次（重试那一圈不再重复弹遮罩）
        bool guideHintShown = false;

        while (true)
        {
            DraftCategory? picked = null;
            int choice = 0;
            // 【2026-10-11 修】价必须**每轮重算**：每抽一次本局抽数 +1，下一抽贵 10%
            // （主人原话「每抽一次奖增加下一次抽奖花费的金币 每次+10%」）。
            // 旧写法把 price / focusPrice 算在 while 外面，面板连抽 N 次都还是同一个价 ——
            // 主人反馈「抽了一次奖后 花费没有增长」就是这个。
            int price = SlotMachineSystem.DrawPrice();
            int focusPrice = SlotMachineSystem.DrawFocusPrice();
            // 还有免费额度 → 这一抽 0 币（定向也免费），面板上显示「免费 ×剩余」
            int freeLeft = SlotMachineSystem.FreeDrawsLeft();
            int showPrice = freeLeft > 0 ? 0 : price;
            int showFocus = freeLeft > 0 ? 0 : focusPrice;
            // 2026-09-30 主人改版：抽奖按钮长在战斗 HUD 底部的 BackpackPanel 里
            //（BackpackPanel 收起 600 / 展开 750），不再弹居中面板 SlotOfferUI。
            BattleEntryDraftPanel.Show(showPrice, showFocus, cats, currentGold,
                c => { picked = c; choice = 1; },
                () => choice = 2, freeLeft);

            // 同上（CoMidBattleDraft 那份）：引导局不催，面板一弹出就冻住倒计时。
            // 现在引导局已跳过进关抽奖，这行是给「以后引导又走这条路」兜的同一道保险。
            if (IsTutorialRun)
                BattleEntryDraftPanel.Instance?.SetCountdownPaused(true);

            // 引导关：开局先教玩家抽奖，圈住「随机抽奖」按钮
            if (onceOnly && !guideHintShown)
            {
                guideHintShown = true;
                var rect = BattleEntryDraftPanel.Instance != null
                    ? BattleEntryDraftPanel.Instance.NormalButtonRect : null;
                // 【2026-10-08 主人拍板】原文案「先抽一次奖，开局白拿一个强化。」作废 ——
                //   主人原话「引导文字不要说开局白拿一个抽奖」：引导现在也不再从开局白送，
                //   第一抽是**打完第一波才给**的（见 TutorialDirector 1c 那一拍）。
                //   文案只说「抽一次」，不提白拿、不剧透类别。
                TutorialHintUI.Ensure().ShowHard("先抽一次奖，看看能开出什么。", rect);
            }

            // 面板常驻，给足看构筑的时间；不点就一直不开战（上限 5 分钟防卡死）
        float offerGuard = 0f;
        while (choice == 0 && offerGuard < 300f)
        {
            offerGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (choice != 1) break;

        // 【2026-10-06 主人二次拍板】玩家一按抽奖就<b>冻结</b>倒计时：接下来是扣币 / 老虎机 /
        // 三选一 / 换装备弹窗这一串结算，这段时间不该催玩家点「继续」。
        // ⚠ 冻的是<b>剩余秒数</b>，不重新起表 —— 结算完接着走剩下的，绝不给它偷偷补满。
        if (BattleEntryDraftPanel.Instance != null)
            BattleEntryDraftPanel.Instance.SetCountdownPaused(true);

        // 每一轮都重算一次可抽类型：上一抽可能把技能槽 / 佣兵位占满，
        // 用旧列表会随机到一个已经发不出去的类型（2026-10-05）。
        cats = SlotMachineSystem.AvailableCategories();

            // null = 点的是随机；有值 = 点的是定向
            var target = picked.HasValue ? picked.Value : SlotMachineSystem.ResolveCategory(cats);
            // 定向价按类型各自的币种算（佣兵 = 佣兵币价，装备 / 技能 = 抽奖币价）。
            int cost = freeLeft > 0 ? 0
                     : (picked.HasValue ? SlotMachineSystem.FocusPrice(picked.Value) : price);
            // 只在这里「查余额」；真正的扣款挪进 CoInstantPick，等结果确认能生效了才扣
            // （2026-10-05：原来先扣后出结果，槽位/背包满时会白扣钱）
            // 免费抽 cost = 0，余额检查自然通过，也不会扣币。币种口径见 CanAffordDraw。
            if (!CanAffordDraw(picked, cost, out string shortBy))
            {
                UIManager.Instance?.ShowToast(shortBy + "不足");
                break;
            }

            // 【2026-10-12 主人拍板】「再来一次」= **抽奖结果之一**，不再是抽完之后的额外赠送。
            // 主人原话：「为什么抽中了技能还出现了再抽一次　再抽一次应该也算是抽中的东西」。
            // 于是这一抽**先掷**「再来一次」：
            //   · 命中 → 本次结果就是它：不出佣兵 / 技能 / 装备，**不扣币、不推进抽数**，
            //     只送一次免费抽，回面板让玩家用这次免费额度再点（按钮金额当场变「免费」）；
            //   · 未命中 → 才真的出类型奖品（往下走 CoInstantPick）。
            // ⚠ 引导局不参与（那一拍只抽一次），判定沿用同一处 onceOnly，别在别处加第二个。
            // ⚠ 旧的「抽完再掷、额外再送一次免费抽」（2026-10-11 口径）已删除，不留开关。
            if (!onceOnly && SlotMachineSystem.RollReroll())
            {
                SlotMachineSystem.GrantBonusFreeDraw();
                var rerollPanel = BattleEntryDraftPanel.Instance;
                if (rerollPanel != null)
                {
                    rerollPanel.SetCountdownPaused(true);
                    // 按钮金额**当场**变成免费（不等面板下一轮重开），主人要求的就是这个瞬间的反馈
                    rerollPanel.RefreshFreeLeft(currentGold, SlotMachineSystem.FreeDrawsLeft());
                }
                UIManager.Instance?.ShowToast("再来一次！这一抽免费");
                // 【2026-10-06 主人拍板】左侧滑入 → 慢慢滑行 1 秒 → 快速向右滑走的大字横幅
                SlotMachineAgainBanner.Show();
                // 等横幅播完（0.30 滑入 + 1.00 滑行 + 0.22 滑出 ≈ 1.52s）再回面板
                yield return new WaitForSecondsRealtime(1.55f);
                if (rerollPanel != null) rerollPanel.SetCountdownPaused(false);
                continue;   // 这一抽的结果就是「再来一次」—— 回面板，让玩家点这次免费的
            }

            // 买了就直接给结果，不走三选一。
            // 「这一抽到底生效没有」唯一真源 = 保底定序有没有往前走（生效才 NoteDraw）。
            int drawsBefore = SlotMachineSystem.DrawIndex;
            yield return CoInstantPick(target, cost);
            bool landed = SlotMachineSystem.DrawIndex > drawsBefore;

            // 引导局：这一拍**只抽一次**就开打 —— 不进「再来一次」、也不再回到面板。
            // 面板常驻连抽是正式关的玩法；引导这里若让玩家一口气把三次抽完，
            // 后面「打完两波抽佣兵 / 再两波抽技能」两拍就吃不到保底定序了（序号是本局累计的）。
            //
            // ⚠ 唯一例外：这一抽**没生效**（池空 / 技能槽满 / 佣兵位满 / 玩家不肯换装备）时定序不推进，
            //   放它过去 = 引导三拍会缺一件（缺的往往是最后那件本命技能，引导直接断在半路）。
            //   → 让它再点一次；最多重试 1 次，池子真出不了卡时不把玩家卡在面板里。
            // ⚠ 「只抽一次」的判定只用 OneDrawPerBeat 一处，别在别处加第二个。
            // 【2026-10-06 主人拍板】引导局：只有「这一抽没生效」才放它再点一次（最多重试 1 次，
            // 否则引导三拍会缺一件）；抽到了就往下走，进「等玩家点继续」。
            if (onceOnly && !landed && ++beatRetry <= 1)
                continue;

            if (onceOnly)
            {
                // 引导局 = 一次一抽：锁住抽奖按钮（只剩「继续」能点），引导指向「继续」，
                // 等玩家点继续 / 倒计时归零。
                yield return WaitForContinue(true, $"{RewardTip()}　点「继续」开打。", () => choice == 2);
                break;
            }

            // 正式关：**连抽仍然是玩法** —— 抽完回到面板，玩家想再抽就再抽；
            // 不想抽、或者币不够（按钮会自动置灰）时，点「继续」就继续战斗。
            // ⚠ 不是「每抽一次就得点一次继续」：这里不锁按钮、不 break。
            //
            // ⚠【2026-10-12 纠正一处「记反了」的归因，主人当天又报了一次】
            // 旧注释写的是「2026-10-11 主人拍板：抽一次重置一次计时」，但它**自己引用的主人原话**
            // 是「时间是固定的 不是抽一次重置一次计时」—— 引文和结论正好相反，是当时的误记，
            // 而且主人 2026-10-12 明确又强调了一遍（原话「不是说过倒计时不要抽一次重置一次吗」）。
            // 按归因纪律纠正：主人从头到尾要的都是 **总预算固定 = 面板弹出时那 30s**。
            // → 这一抽结算完只**解冻**（接着走剩下的秒数），**绝不补满重来**。
            //   与 2026-10-06 那份口径一致，不存在「以 10-11 为准」这回事，别再改回 StartContinueCountdown。
            var entryPanel = BattleEntryDraftPanel.Instance;
            if (entryPanel != null) entryPanel.SetCountdownPaused(false);

            // ⚠【2026-10-12 已删】原来这里还有一段「抽完再掷 RollReroll → 额外再送一次免费抽」。
            //   主人报「抽中了技能还出现了再抽一次」→ 判定挪到这一抽**开始之前**（见本循环上方），
            //   「再来一次」从此是抽奖结果之一，不再与类型奖品叠加。别把这段加回来。
        }

        // 退出循环 = 要开战了：收起 BackpackPanel、隐藏抽奖按钮与「继续」，引导提示也关掉
        BattleEntryDraftPanel.Hide();
        TutorialHintUI.Instance?.Hide();
    }

    /// <summary>
    /// 抽奖「查余额」的<b>唯一出口</b>（只看不扣；真扣款在 <see cref="CoInstantPick"/> 里、
    /// 确认结果能生效之后才做）。【2026-10-10 主人拍板】抽奖统一扣**战斗内金币**——
    /// 不再分佣兵币 / 抽奖币，余额就是 HUD 上那个 currentGold。
    /// </summary>
    /// <param name="shortBy">不够时回传币种显示名（「金币」），给 toast 用。</param>
    bool CanAffordDraw(DraftCategory? picked, int cost, out string shortBy)
    {
        shortBy = ResourceWallet.DisplayName(ResourceWallet.ResourceType.Gold);
        return cost <= 0 || currentGold >= cost;
    }

    /// <summary>
    /// 抽奖扣金币的<b>唯一出口</b>（2026-10-10 主人拍板「每抽一次奖增加下一次抽奖花费的金币」配套）：
    /// 直接扣战斗内 currentGold（= 局外金币的战斗镜像，收尾经 <see cref="PersistBattleGold"/> 差额写回存档）。
    /// 扣完立刻刷 HUD；余额不足返回 false（调用方已先用 <see cref="CanAffordDraw"/> 拦过，这里是保险）。
    /// </summary>
    public bool SpendBattleGold(int cost)
    {
        if (cost <= 0) return true;
        if (currentGold < cost)
        {
            Debug.LogError($"[BattleManager] 抽奖扣金币失败：需 {cost}，现有 {currentGold}（不应走到这里，前置 CanAffordDraw 漏拦）");
            return false;
        }
        currentGold -= cost;
        BattleUI.Instance?.UpdateGold(currentGold);
        return true;
    }

    /// <summary>
    /// 局内进钱的<b>唯一出口</b>（2026-10-10 主人拍板「进战斗只带启动金」配套）：
    /// 启动金 / 每关通关补贴 / 抽中的金币，三种都走这里进 <c>currentGold</c>。
    /// </summary>
    /// <param name="countsAsEarned">
    /// true = 本局赚的钱（击杀掉落等），结算会写回存档；
    /// false = <b>局内循环资源</b>（启动金 / 通关补贴 / 抽中金币）：抬高 <c>_goldAtRunStart</c>，
    /// 于是结算差额不含它 —— 能抽，但带不出裂隙（跟「战斗内的都是局内的」同口径，也不会被白刷）。
    /// </param>
    public void AddRunGold(int n, bool countsAsEarned)
    {
        if (n <= 0) return;
        currentGold += n;
        if (!countsAsEarned) _goldAtRunStart += n;
        BattleUI.Instance?.UpdateGold(currentGold);
    }

    /// <summary>
    /// 点了抽奖按钮：不弹三选一，直接从该类型里抽 1 张、立刻生效，并把结果提示给玩家。
    /// 玩家买的是结果，不是选择 —— 选择留给免费的老虎机三选一。
    ///
    /// <para><b>扣钱与发奖的顺序（2026-10-05 修的漏洞）</b>：原来是「外层先扣钱 → 这里出结果」，
    /// 于是「池子空 / 卡无效 / 技能槽满 / 佣兵位满 / 背包满」这些情况都会
    /// <b>钱扣了、东西没拿到</b>。现在改成：<b>先出结果并确认真的生效，生效了才扣钱</b>；
    /// 拿不到东西就一分不扣、保底定序也不推进（不算玩家抽过这一抽）。</para>
    ///
    /// <param name="cost">这一抽的价格（币）。由 <see cref="BattleManager"/> 的编排层算好传进来，
    /// 本方法不再自己算价，避免两处价格对不上。</param>
    /// </summary>
    IEnumerator CoInstantPick(DraftCategory cat, int cost)
    {
        var dir = RunDraftDirector.Instance ?? RunDraftDirector.Ensure(this);
        if (dir == null)
        {
            Debug.LogError("[BattleManager] RunDraftDirector 缺失，抽奖奖励无法生效（未扣币）");
            yield break;
        }

        // 【2026-10-07 主人拍板】佣兵定向「一次一枚币，有几率失败」：
        // 花一次钱开一次口，未必说得动对方 —— 失败 = 钱照花、人没来（设计好的赌，不是 bug）。
        // ⚠ 引导局的保底佣兵（塔克）绝不许失败，否则引导会断在半路 → 引导局整段跳过；
        // ⚠ 免费额度（金币本）那几次 cost = 0，也不参与失败判定。
        // 【2026-10-10 主人拍板】扣的币统一为战斗内金币（SpendBattleGold 唯一出口）。
        if (cat == DraftCategory.Merc && cost > 0 && !IsTutorialRun)
        {
            float rate = SlotMachineDefs.MERC_FOCUS_SUCCESS_RATE;
            if (Random.value >= rate)
            {
                SpendBattleGold(cost);                        // 失败也要把这笔钱花掉
                Debug.Log($"[BattleManager] 佣兵定向招募失败（成功率 {rate:P0}），扣 {cost} 金币");
                UIManager.Instance?.ShowToast("招募失败：这一回没能说动对方。", true);
                yield break;
            }
        }

        // ① 保底：引导三拍的保底内容**只有这一处**编排，不许散成两处。
        DraftCard card = default;
        // 【2026-10-11 主人拍板】技能类不再保底：原来的「本局第一次抽到技能 = 白送职业初始技能」
        // （SlotMachineSystem.BuildGuaranteedSkillCard）已整条删除，技能一律走下面的随机池。
        // 2026-10-07 主人拍板：引导第 2 抽（佣兵）= 直接招募塔克本人入队（H003 剑盾卫士，稀有），
        // 碎片那条口径已作废（TutorialRules.MercDraftGivesFragment 整条删除）。
        if (cat == DraftCategory.Merc)
            card = SlotMachineSystem.BuildGuaranteedMercCard();
        // 2026-10-06 主人澄清：第一抽是「随机拿到一件新装备」，不是保底木盾；
        // 木制圆盾（equip_shield_1）只是**起步装备**换成它（player_job_base_stats P001 副手），
        // 跟抽奖是两码事 —— 装备保底那条已撤回，装备类一律走随机池。

        // ② 其余走正常卡池随机：按权重从整池里抽 1 张（概率与公示弹窗一致）
        if (!card.IsValid)
        {
            // 要不要带安慰奖（金币）唯一出口 = SlotMachineSystem.ConsolationAllowed()：
            // 引导定序窗口内不带（主人拍板引导三拍必须是 装备→佣兵→技能）；
            // 正式关第 1 抽也不带（2026-10-13 #3：防「开局花 80 抽回 40」的开局挫败），第 2 抽起恢复纯随机。
            card = DraftPool.PickOne(cat, SlotMachineSystem.ConsolationAllowed());
        }

        if (!card.IsValid)
        {
            // fail closed：出不了卡就不许扣钱，报出来让主人看见
            Debug.LogError($"[BattleManager] 抽奖池出不了卡（{cat}），本次未扣币、未计入保底定序");
            UIManager.Instance?.ShowToast("这一抽没能出结果，未扣币");
            yield break;
        }

        // ③ 先确认这张卡现在真能生效（槽位 / 星级 / 背包位 / 玩家肯不肯换装备），生效了才扣钱
        // 【2026-10-11 主人拍板】抽了技能/佣兵要立刻涨战力：与免费老虎机同口径，
        // 发奖前后各取一次真值算差（见下方 CoPickFromCategory 的既有写法），别漏这一拍。
        int powerBefore = RunLoadout.TotalPower();
        bool ok;
        string msg;
        if (card.Kind == DraftCardKind.Equip)
        {
            // 装备要走「要不要替换」的确认弹窗（2026-10-05 主人拍板），是异步的
            ok = false;
            msg = null;
            yield return dir.CoApplyEquipCard(card, (o, m) => { ok = o; msg = m; });
        }
        else
        {
            ok = dir.TryApplyCard(card, out msg);
        }

        if (!ok)
        {
            Debug.LogError($"[BattleManager] 抽奖结果无法生效（{cat}）：{msg}　本次未扣币、未计入保底定序");
            UIManager.Instance?.ShowToast($"{msg}（未扣币）");
            yield break;
        }

        // 免费抽（金币本开局送的那几次 / 「再来一次」送的那次）：不扣币，只消耗免费额度。
        // ⚠ 抽数序号照常推进 —— 引导局靠它一格一格走「装备→佣兵→技能」，
        //   不推进的话引导三拍会全落在同一类上，等于白教（正式关是纯随机，序号只作统计）。
        // 【2026-10-10 主人拍板】付费抽统一扣战斗内金币（价格已按抽数 +10% 递增）。
        // 【2026-10-11 主人拍板】例外：「再来一次」那一抽**不推进抽数** ——
        //   主人原话「这次抽不用涨费用」，否则奖励反而把下一抽的价格顶上去，玩家点不动。
        bool bonusFree = false;
        if (cost > 0)
            SpendBattleGold(cost);
        else if (SlotMachineSystem.ConsumeBonusFreeDraw())
            bonusFree = true;
        else
            SlotMachineSystem.ConsumeFreeDraw();

        if (!bonusFree)
            SlotMachineSystem.NoteDraw();      // 本局抽数 +1（跨关不清零）
        RunLoadout.Save();
        RunDraftDirector.RefreshSkillPower();
        // 【2026-10-11 主人拍板】战力涨了 → 头顶飘「战力 +N」+ 三档称赞；没涨就只刷数字。
        // 与 CoPickFromCategory（免费老虎机）同一口径，别两边写法分叉。
        int powerDelta = RunLoadout.TotalPower() - powerBefore;
        if (powerDelta > 0) PlayerPowerHud.NotifyGain(powerDelta);
        else PlayerPowerHud.NotifyPowerChanged();
        // 2026-10-05：抽完必须重建技能栏，否则新技能不上 HUD（连带「拖动改顺序」的软引导也没机会弹）。
        RunSkillBarUI.Refresh();
        // 【2026-10-10 主人拍板】抽到技能要**立即**看到底部技槽图标（主人反馈「抽了技能后没有立即显示图标」）：
        // 左栏 Rebuild 不刷底部 4 槽，逐帧 tick 又要等下一帧比对 —— 这里主动刷一次，抽完当场亮出来。
        BattleUI.Instance?.UpdateRunSkillSlots();
        BattleUI.Instance?.UpdateMercSkillSlots();
        // 【2026-10-10 主人反馈】抽到佣兵/技能后**头像栏**也要立即亮（UpdateCharacterSlots 内部自带
        // ShowMercHud 推导，抽到第一个佣兵时整条头像栏都是靠它挂出来的）。
        BattleUI.Instance?.UpdateCharacterSlots();

        string got = !string.IsNullOrEmpty(msg) ? msg
                   : !string.IsNullOrEmpty(card.Title) ? card.Title
                   : "已生效";
        // 【2026-10-06 主人反馈「获得的东西怎么不弹出提示，只弹奖励到手了」】
        // 存一份给后面的「等玩家点继续」硬引导用 —— 引导文案里会带这份内容。
        // 【2026-10-06 主人二次拍板：物品变动都要弹，不管对话/遮罩在不在】→ Toast 走 force=true，
        // 不再被 AnyBubbleShowing 吞掉；引导文案那份保留，等于屏幕浮字 + 引导框双保险。
        // ⚠ 这行赋值不动：RewardTip()（「等玩家点继续」硬引导文案）还读它。
        _lastDrawRewardMsg = $"【{SlotMachineSystem.CategoryName(cat)}】{got}";
        // 【2026-10-13 抽奖优化 #1】Toast 与开奖卡顺序对调（修「文字先剧透」）：
        // 真东西（装备/技能/佣兵）先弹卡，Toast 延后到开奖卡演出结束的**唯一通知**再弹
        // （DraftPrizePopupUI 的 onFinished，与停留时长常量同源，不写死等待秒数）；
        // 金币安慰奖维持 Toast 先弹（卡的价值低，无所谓剧透）。
        if (card.Kind == DraftCardKind.Gold)
        {
            UIManager.Instance?.ShowToast(_lastDrawRewardMsg, true);
            // 【2026-10-12 主人拍板】补「开奖」的仪式感：Toast 一闪就没了，玩家对抽奖没概念
            // （主人原话「现在没有抽中奖的喜悦 玩家对抽奖这个没有概念 怎么优化一下」）。
            ShowDrawPrizeCard(card, cat, powerDelta);
        }
        else
        {
            // 【2026-10-14 主人拍板】佣兵/技能**不再弹开奖卡**（主人原话「最开始那个不要了 只要我做的这个」）
            //   —— 开奖卡只留给金币安慰奖。
            // 【2026-10-15 主人拍板】获得佣兵弹窗（huodeyongbing）**不再随抽中默认弹出** ——
            //   入口移到战斗头像栏：点佣兵头像才弹（BattleUI.OnMercAvatarClicked → ShowFromEntry），
            //   抽中当场只流入队横幅（MercJoinBannerUI 照旧报喜）+ Toast；引导局在二抽后教这一步。
            bool rewardPopupClosed = !(card.Kind == DraftCardKind.SkillNew || card.Kind == DraftCardKind.SkillUp);
            // 闭包捕获本地副本：别让下一抽改写了 _lastDrawRewardMsg 字段后 Toast 内容串台
            string toastMsg = _lastDrawRewardMsg;
            if (card.Kind == DraftCardKind.MercRecruit)
                UIManager.Instance?.ShowToast(toastMsg, true);
            else if (card.Kind == DraftCardKind.SkillNew || card.Kind == DraftCardKind.SkillUp)
                StartCoroutine(SkillObtainPopupUI.CoShow(card, () =>
                {
                    UIManager.Instance?.ShowToast(toastMsg, true);
                    rewardPopupClosed = true;
                }));
            else if (card.Kind != DraftCardKind.Equip && DraftRewardDetailUI.NeedsDetail(card))
            {
                // 佣兵升级/升星拍暂无新预制体：维持「开奖卡 → 详情弹窗」原样（主人没点名改）。
                ShowDrawPrizeCard(card, cat, powerDelta, () =>
                    StartCoroutine(DraftRewardDetailUI.CoShow(card, () => UIManager.Instance?.ShowToast(toastMsg, true))));
            }
            else
                UIManager.Instance?.ShowToast(toastMsg, true);

            // 等获得弹窗关闭（玩家点「确认」；CoShow 内部 while(!root.activeSelf) 等到关才结束，
            // 异常路径 Root 缺失也会当场回调 onClosed，不会死等）。超时只是异常兜底，放行不卡死。
            float popupGuard = 0f;
            while (!rewardPopupClosed && popupGuard < 300f)
            {
                popupGuard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (popupGuard >= 300f)
                Debug.LogWarning("[BattleManager] 等获得弹窗关闭超时（300s），放行继续流程");
        }
    }

    /// <summary>
    /// 【2026-10-12 主人拍板】抽中生效后弹一张开奖卡（演出细节见 <see cref="DraftPrizePopupUI"/>）。
    /// 这是开奖演出的<b>唯一出口</b>：文案 / 配色 / 稀有度档次都在这里定，别在别处再弹一份。
    /// ⚠ 稀有度配色只能问 <see cref="RarityPalette"/>，不许在本文件里抄色值。
    /// </summary>
    /// <param name="powerDelta">【2026-10-13 #2·裁定1】发奖后<b>实际生效</b>的战力增量（L2947 那个差），
    /// 不用 <see cref="DraftCard.PowerDelta"/> 预估值 —— 与头顶飘字同源，两屏不打架。</param>
    /// <param name="onCardFinished">【2026-10-13 #1】开奖卡演出结束的唯一通知；非空 = Toast 等它再弹（真东西）。</param>
    void ShowDrawPrizeCard(DraftCard card, DraftCategory cat, int powerDelta, System.Action onCardFinished = null)
    {
        string title = card.Title;
        string kind = SlotMachineSystem.CategoryName(cat);
        Color tint = RarityPalette.Get(card.Rarity);
        int tier = SkillRarityTier(card.Rarity);
        Sprite icon = null;
        // 【2026-10-13 #2】第 3 行两件套：品质词（色 = 卡面 tint）+ 特例文案（note）
        string rarityLabel = null;
        string note = null;

        // 【2026-10-13 #4】金币安慰奖分支<b>提到最前</b>：安慰奖是按类别混进各类池子的，
        // card.Kind==Gold 时 cat 仍可能是技能/佣兵 —— 旧写法排在 cat 分支后面，
        // 定向技能/佣兵抽到金币会被错标成「获得技能 / 佣兵升级」。
        if (card.Kind == DraftCardKind.Gold)
        {
            // 「折扣」语义：金色卡 + icon_gold + 第 1 行「获得金币」+ 无光晕/无全屏闪/停 0.60s（tier=普通档）。
            // 特判金色，不走 RarityPalette.Get(card.Rarity)（否则白灰 = 视觉最差档）。
            kind = "获得金币";
            title = "金币 +" + card.Amount;
            tint = RarityPalette.Gold;
            icon = CommonIconSprites.Load("icon_gold");
            // 第 3 行只显示「回血 N」—— 直接用卡池给的 Desc（CollectGoldCard 已改成这句，不另写一份）
            note = card.Desc;
        }
        else if (card.Kind == DraftCardKind.Equip && card.Equip != null)
        {
            // 装备的稀有度在实例上（Rarity 枚举），不用卡片的 SkillRarity
            tint = RarityPalette.Get(card.Equip.rarity);
            tier = EquipRarityTier(card.Equip.rarity);
            icon = card.Equip.icon ?? card.Equip.template?.icon;
            kind = "获得装备";
            // 品质词：装备先过 DraftPool.MapRarity（唯一映射，不写第二份），词色跟卡框同色
            rarityLabel = SkillRarityUtil.DisplayName(DraftPool.MapRarity(card.Equip.rarity));
            // 【2026-10-13 #2·裁定1】实际增量为 0 = 玩家留着旧的、新件已折成分解材料 → 不显示「战力 +0」
            if (powerDelta <= 0) note = "已折成分解材料";
        }
        else if (cat == DraftCategory.Skill)
        {
            icon = BattleUI.LoadRunSkillIcon(card.Id);
            // 槽位参照系（2026-10-13 #2）：槽没满 → 「获得技能 n/4」；槽满走「技能升星」分支，不显示 4/4
            kind = card.Kind == DraftCardKind.SkillUp || RunLoadout.IsSkillFull
                 ? "技能升星"
                 : $"获得技能 {RunLoadout.SkillIds().Count}/{RunLoadout.MaxSkillSlots}";
            rarityLabel = SkillRarityUtil.DisplayName(card.Rarity);
        }
        else if (cat == DraftCategory.Merc)
        {
            // 佣兵头像要按 AssetId 找，这里不另外加载 —— 没图就只显示名字（不兜底造图）
            // 槽位参照系（2026-10-13 #2）：「招募佣兵 n/2」，n = 入队后计数（与 CategoryHint 同一口径）
            kind = card.Kind == DraftCardKind.MercRecruit
                 ? $"招募佣兵 {RunLoadout.Mercs().Count}/{RunLoadout.MaxRunMercs}"
                 : (card.Kind == DraftCardKind.MercStarUp ? "佣兵升星" : "佣兵升级");
            rarityLabel = SkillRarityUtil.DisplayName(card.Rarity);
        }

        if (string.IsNullOrEmpty(title)) title = kind;
        DraftPrizePopupUI.Show(BattleUI.Instance != null ? BattleUI.Instance.transform : null,
                               title, kind, tint, icon, tier, powerDelta, rarityLabel, note, onCardFinished);
    }

    static int SkillRarityTier(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return DraftPrizePopupUI.TierLegend;
            case SkillRarity.Epic: return DraftPrizePopupUI.TierEpic;
            case SkillRarity.Rare: return DraftPrizePopupUI.TierRare;
            default: return DraftPrizePopupUI.TierCommon;
        }
    }

    static int EquipRarityTier(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return DraftPrizePopupUI.TierLegend;
            case Rarity.Epic: return DraftPrizePopupUI.TierEpic;
            case Rarity.Rare: return DraftPrizePopupUI.TierRare;
            case Rarity.Uncommon: return DraftPrizePopupUI.TierRare;   // 绿档与蓝档同档演出
            default: return DraftPrizePopupUI.TierCommon;
        }
    }

    /// <summary>
    /// 战斗中再开一次进关抽奖 —— **正式入口，不是引导特例**。
    /// <para>2026-10-05 主人拍板：抽奖是主玩法（正式关也抽），所以「打完两波抽一次」这种节拍
    /// 走这里，不要在 TutorialDirector 里另写一套弹面板的代码。</para>
    /// 行为：冻住单位 + 收摇杆 → 弹进关抽奖面板 → 抽一次 → 收面板 → 恢复原来能不能动。
    ///
    /// <para><b>本方法天然「一次一抽」</b>（没有连抽循环）—— 引导的第 ②③ 拍走它，
    /// 正好对上主人「引导每次只能抽一次」的要求，不需要再另加限制开关。</para>
    /// </summary>
    /// <param name="guideText">给 null = 不强引导；给了就用硬遮罩圈住「随机抽奖」按钮。</param>
    public IEnumerator CoMidBattleDraft(string guideText = null)
    {
        var cats = SlotMachineSystem.AvailableCategories();
        if (cats.Count == 0)
        {
            // 与开局抽奖同口径：池子全空不静默跳过，控制台必须能看到原因
            Debug.LogError("[BattleManager] 抽奖池全空，中途抽奖不弹出");
            yield break;
        }

        bool canActBefore = UnitsCanAct;
        // ⚠ 冻住单位是「有借有还」：协程若在等待玩家时被外部 StopCoroutine（撤离 / 死亡 / 切场景），
        // finally 仍会执行 —— 否则 UnitsCanAct 会永远停在 false，整场战斗谁都动不了（2026-10-05）。
        try
        {
            UnitsCanAct = false;
            var hero = Hero.Instance;
            if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;
            // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
            // BattleJoystick.Instance?.SetVisible(false);

            int price = SlotMachineSystem.DrawPrice();
            int focusPrice = SlotMachineSystem.DrawFocusPrice();

            DraftCategory? picked = null;
            int choice = 0;
            int freeLeft = SlotMachineSystem.FreeDrawsLeft();
            // 【2026-10-10 主人拍板】面板余额显示战斗内金币（抽奖真扣的就是它，不再是另一套抽奖币）
            BattleEntryDraftPanel.Show(freeLeft > 0 ? 0 : price, freeLeft > 0 ? 0 : focusPrice,
                cats, currentGold,
                c => { picked = c; choice = 1; },
                () => choice = 2, freeLeft);

            // 【2026-10-08 主人拍板】引导局**不催**：面板一弹出就把倒计时冻住（引导三拍都走这里），
            // 玩家看清楚自己抽到了什么再点「继续」。正式关照常跑那 30s 总预算。
            // 后面 WaitForContinue 里也有同一道判定，保证整段引导都不解冻。
            if (IsTutorialRun)
                BattleEntryDraftPanel.Instance?.SetCountdownPaused(true);

            if (!string.IsNullOrEmpty(guideText) && BattleEntryDraftPanel.Instance != null)
                TutorialHintUI.Ensure().ShowHard(guideText, BattleEntryDraftPanel.Instance.NormalButtonRect);

            float guard = 0f;
            while (choice == 0 && guard < 300f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            TutorialHintUI.Instance?.Hide();

            // 【2026-10-06 主人拍板】引导三拍（装备→佣兵→技能）靠「本局累计抽数」定位序，
            // 玩家这一拍直接点「继续」（或倒计时走完）不抽 → 定序**不走格**，下一拍就重复出同一类
            //（主人报的正是「第二次没抽，第三次抽就抽出佣兵了」）。
            // → 引导局这一拍替他补抽一次，把定序推回正轨；**正式关一律不帮忙**
            //（主人原话「正式关就不要帮忙抽了」——正式关等权随机，本就没有定序可错）。
            if (choice == 2 && OneDrawPerBeat)
            {
                var autoTarget = SlotMachineSystem.ResolveCategory(cats);
                int autoCost = freeLeft > 0 ? 0 : price;
                if (currentGold < autoCost)
                {
                    // fail closed：不静默跳过，报出来让主人看见「引导三拍的钱不够了」
                    Debug.LogError($"[BattleManager] 引导补抽失败：局内金币不足" +
                                   $"（需 {autoCost}，现有 {currentGold}），本拍定序不会推进");
                }
                else
                {
                    int autoBefore = SlotMachineSystem.DrawIndex;
                    yield return CoInstantPick(autoTarget, autoCost);
                    Debug.Log($"[BattleManager] 引导补抽：玩家这一拍没抽，已自动抽一次（{autoTarget}）" +
                              $"　抽数 {autoBefore} → {SlotMachineSystem.DrawIndex}");
                }
            }

            if (choice == 1)
            {
                // null = 点的是随机；有值 = 点的是定向
                var target = picked.HasValue ? picked.Value : SlotMachineSystem.ResolveCategory(cats);
                int cost = freeLeft > 0 ? 0
                         : (picked.HasValue ? SlotMachineSystem.FocusPrice(picked.Value) : price);
                // 与开局抽奖同口径：这里只查余额（按各自币种），真扣款等 CoInstantPick 确认结果能生效了再做
                if (!CanAffordDraw(picked, cost, out string shortBy))
                    UIManager.Instance?.ShowToast(shortBy + "不足");
                else
                {
                    yield return CoInstantPick(target, cost);
                    // 【2026-10-06 主人拍板】抽完奖励不许自动继续（引导 ②③ 拍 + 正式关中途抽都算）：
                    // 锁抽 + 面板倒计时，等玩家点「继续」才收面板、恢复战斗。
                    choice = 0;
                    yield return WaitForContinue(true,
                        !string.IsNullOrEmpty(guideText) ? $"{RewardTip()}　点「继续」继续战斗。" : null,
                        () => choice == 2);
                    // 【2026-10-08 主人拍板】点完「继续」立刻弹一句「继续前进」——
                    // 面板收起、战斗恢复的那一刻，给玩家一个明确的「接着往前走」的信号。
                    // 只走引导（guideText 非空）；正式关不打扰。想改停留秒数就改这个 2.2f。
                    if (!string.IsNullOrEmpty(guideText))
                        TutorialHintUI.Ensure().Show("继续前进", null, 2.2f);
                }
            }
        }
        finally
        {
            BattleEntryDraftPanel.Hide();
            // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
            // BattleJoystick.Instance?.SetVisible(true);
            UnitsCanAct = canActBefore;
        }
    }

    /// <summary>
    /// 「再来一次」的免费奖励：随机一类 → 老虎机走个形式 → 弹**该类型的三选一**。
    /// 不扣钱；玩家在这里才做选择，所以演出可以慢一点（JACKPOT_REVEAL_SEC）。
    /// </summary>
    IEnumerator CoJackpot(List<DraftCategory> cats)
    {
        var cat = SlotMachineSystem.RollCategory(cats);

        bool rolled = false;
        SlotMachineUI.Show(cats, cat, 0, () => rolled = true);
        float rollGuard = 0f;
        while (!rolled && rollGuard < 20f)
        {
            rollGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!rolled)
        {
            Debug.LogWarning("[BattleManager] 老虎机没有回调，跳过本次免费奖励");
            yield break;
        }

        yield return CoPickFromCategory(cat, 0);
    }

    /// <summary>老虎机停下那一类的三选一：复用现成的 LevelUpDraftUI.Show。</summary>
    /// <param name="retry">重选次数。选中的卡没生效时允许再选一次，但最多 <c>MaxFreeRetry</c> 次，防止死循环。</param>
    IEnumerator CoPickFromCategory(DraftCategory cat, int retry)
    {
        var cards = DraftPool.BuildCards(cat);
        // 免费奖励出不了卡就安静跳过（不扣钱，没什么可退的），但要报出来
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning($"[BattleManager] 免费老虎机出不了卡（{cat}），本次奖励作废");
            yield break;
        }

        var dir = RunDraftDirector.Instance ?? RunDraftDirector.Ensure(this);
        if (dir == null)
        {
            Debug.LogError("[BattleManager] RunDraftDirector 缺失，抽奖奖励无法生效");
            yield break;
        }

        bool picked = false;
        bool landed = false;
        // 2026-10-05：装备卡要弹「换不换」确认窗，是异步的，不能在回调里直接结算 ——
        // 这里只记下玩家选了哪张，真正的生效挪到等待循环之后（那里才能 yield）。
        DraftCard chosen = default;
        LevelUpDraftUI.Show(cards, "抽奖结果 · 选一个", card =>
        {
            if (card.IsValid) chosen = card;
            picked = true;
        }, manageFreeze: false);

        float guard = 0f;
        while (!picked && guard < 180f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }

        if (picked && chosen.IsValid)
        {
            string msg = null;
            // 【2026-10-06 主人拍板】战力涨幅：发奖前后各取一次真值，差多少就在玩家头顶飘多少。
            // 不用 RunLoadout.ConsumeLastDelta（那是「相对上次调用」的累计，会跨次叠加）。
            int powerBefore = RunLoadout.TotalPower();
            if (chosen.Kind == DraftCardKind.Equip)
                yield return dir.CoApplyEquipCard(chosen, (o, m) => { landed = o; msg = m; });
            else
                // 与「买了就出结果」同口径：分不清成功失败就会把「槽位已满」当成获奖弹给玩家
                landed = dir.TryApplyCard(chosen, out msg);

            // 【2026-10-13 主人拍板】免费抽同口径：详情弹窗（点任意位置关）在前，Toast 延后，防剧透
            if (landed && DraftRewardDetailUI.NeedsDetail(chosen))
                yield return DraftRewardDetailUI.CoShow(chosen);
            if (!string.IsNullOrEmpty(msg)) UIManager.Instance?.ShowToast(msg);
            if (landed)
            {
                RunLoadout.Save();
                RunDraftDirector.RefreshSkillPower();
                // 2026-10-05：免费这一抽也可能给技能，技能栏必须重建，否则新技能不上 HUD
                RunSkillBarUI.Refresh();
                // 【2026-10-06 主人拍板】战力涨了 → 头顶飘「战力 +N」+ 三档称赞；没涨就只刷数字。
                int delta = RunLoadout.TotalPower() - powerBefore;
                if (delta > 0) PlayerPowerHud.NotifyGain(delta);
                else PlayerPowerHud.NotifyPowerChanged();
            }
        }

        // 选了但没生效（槽位/背包满）：让玩家能再选一张，别白白吞掉这次免费奖励。
        // 最多重选 MaxFreeRetry 次 —— 三张全发不出去时不能一直弹下去（会死循环）。
        if (picked && !landed)
        {
            if (retry >= MaxFreeRetry)
            {
                Debug.LogWarning($"[BattleManager] 免费老虎机（{cat}）连续 {retry} 次选中的卡都没生效，放弃本次奖励");
                yield break;
            }
            Debug.LogWarning($"[BattleManager] 免费老虎机选中的卡未生效（{cat}），重选第 {retry + 1} 次");
            yield return CoPickFromCategory(cat, retry + 1);
        }
    }

    /// <summary>免费老虎机「选中的卡没生效」时最多重选几次。0 = 不重选。</summary>
    const int MaxFreeRetry = 1;

    void BeginRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        _rewardSequenceStarted = true;
        isInBattle = false;
        UnitsCanAct = false;

        FocusMarkSystem.Ensure()?.ResetForBattle();
        BattleUI.Instance?.EnsureBattleControls();
        BattleUI.Instance?.EnsureRunSkillSlotsRepaired();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(false);

        if (currentStage == null)
        {
            Debug.LogError("[BattleManager] 结算时 currentStage 为空");
            FinishStageAfterPortalReached();
            return;
        }

        // 2026-09-29：通关发抽奖币（与金币奖励完全分开，只用于下关进关抽奖）
        // 2026-10-05：发不发由**模式**说了算（IBattleMode.GrantsRunCoins）。
        //   金币本打完就结算回城、币随局清零，再发一笔就是条「+100 然后归零」的假收益 → 不发。
        if (Mode == null)
            Debug.LogError("[BattleManager] BeginRewardSequence：Mode 为空，无法判断本模式发不发抽奖币（本次未发）");
        else if (Mode.GrantsRunCoins)
            SlotMachineSystem.GrantStageCoins(currentStage.type);

        // 2026-10-05 主人拍板「局外在结算界面才发」：这里**不再**即时加金币、不再飘字。
        // 只把本关额度记进待发池，等结算（PersistBattleGold → GrantPendingStageGold）一次性发。
        // 口径不变：打完一章拿满，中途撤离 / 阵亡只拿已通关那几关的部分（结算侧就是这个算法）。
        int stageGold = StageGoldDefs.CurrentStageGold();
        if (stageGold > 0)
            _runStageGoldPending += stageGold;
        _runStagesCleared++; // 撤离补偿计数：本局已通关关数（2026-10-17）

        bool isBoss = currentStage.type == StageType.Boss;
        if (isBoss)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex, atEndFlag: true);
        else
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);

        int bonusStar = 0;
        int equipCount = GameConfig.EQUIP_CHOOSE_COUNT;
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        HiddenLevelSystem.AddStageClearExp(ch);
        int bonusGold = 0;
        if (!_stageQuestGoldGranted)
        {
            bonusGold = StageQuestClearGold > 0
                ? StageQuestClearGold
                : BattleQuestConfig.GetClearGold(ch, currentStage.type, BattleDifficulty);
            bonusGold = Mathf.RoundToInt(bonusGold * GoldGainMul);
        }

        // 问「这个模式掉不掉装备」，不再问「是不是金币本」——
        // 以后「武器副本」这类活动就算走活动入口，只要声明 DropsEquipment = true 就会掉装。
        if (!DropsEquipment)
            equipCount = 0;
        else if (isBoss)
        {
            bonusStar = 2;
            equipCount += 1;
        }
        else if (currentStage.type == StageType.Elite)
            bonusStar = 1;

        // 多出的奖励件直接折金，三选一只展示 3 张
        int blacksmithLevel = TownSystem.Instance != null ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith) : 1;
        List<EquipInstance> rewards = (DropsEquipment && ConfigManager.Instance != null)
            ? ConfigManager.Instance.GetRandomEquipInstances(equipCount, blacksmithLevel, bonusStar, currentStage.type, GameConfig.RIFT_DROP_ATTR_BONUS, false, ch)
            : new List<EquipInstance>();

        if (rewards != null && rewards.Count > 3)
        {
            for (int i = 3; i < rewards.Count; i++)
                bonusGold += GameConfig.EquipScrapGold(rewards[i].rarity, rewards[i].star);
            rewards.RemoveRange(3, rewards.Count - 3);
        }

        EnsureRewardDirector();
        StageClearRewardDirector.Instance.Begin(rewards, bonusGold, currentStage.type);
        Debug.Log($"[BattleManager] 开始宝箱结算 type={currentStage.type} bonusGold={bonusGold} equips={rewards?.Count ?? 0}");
    }

    /// <summary>
    /// 门开了（<see cref="PortalGate"/> 已挂到 BattleUI 下）：把要进门的队伍记下来，全员朝右走进门。
    /// <para>【2026-10-11 主人拍板】门是 UI 节点但代表世界位置：进门判定用 <c>PortalGate.WorldEnterX</c>，
    /// 玩家先走、佣兵隔一个身位跟着进，每人进门各播一次传送特效（见 <see cref="TickPortalWalkIn"/>）。</para>
    /// </summary>
    public void NotifyChuanSongMenOpened(PortalGate gate)
    {
        _portalGate = gate;
        _chuanSongMen = null;          // 场景旧门不再参与流程
        _portalActive = gate != null;
        _heroEnteredPortal = false;
        _heroEnteredAt = 0f;
        _portalOpenedAt = Time.unscaledTime;
        _portalEntered.Clear();
        _portalParty.Clear();

        if (_portalActive)
        {
            if (hero != null) _portalParty.Add(hero);
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                    if (mercs[i] != null) _portalParty.Add(mercs[i]);
            }
            float gateX = gate.WorldEnterX;
            if (!float.IsNaN(gateX)) ExtendCameraMaxX(gateX + 3f);
        }

        // 让英雄/佣兵继续向右走向传送门
        UnitsCanAct = true;
        isInBattle = true; // Update 里检测走近传送门需要跑
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);
        if (hero != null)
        {
            hero.Face(1);
            if (hero.rb != null)
                hero.rb.velocity = new Vector2(Mathf.Max(0.4f, hero.attr != null ? hero.attr.GetAttr(AttrType.MoveSpeed) : 1.2f), 0f);
        }
    }

    /// <summary>
    /// 门开着的时候每帧看队伍走到哪了：谁跨过门中心 → 播一次传送特效 + 藏起来；
    /// 玩家进了之后再等佣兵（最多 <see cref="PortalMercWaitTimeout"/> 秒）→ 全员进完走通关结算（黑屏 → 关卡界面）。
    /// </summary>
    void TickPortalWalkIn()
    {
        float gateX = _portalGate.WorldEnterX;
        if (float.IsNaN(gateX))
        {
            // 门挂不到 Canvas / 拿不到世界坐标时不能把玩家锁在门口：3s 后报错并直接结算。
            if (Time.unscaledTime - _portalOpenedAt >= 3f)
            {
                Debug.LogError("[BattleManager] 传送门的世界坐标一直算不出来（门没挂上 Canvas？），3s 后直接走通关结算");
                FinishStageAfterPortalReached();
            }
            return;
        }

        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u == null || u.isDead || _portalEntered.Contains(u)) continue;
            if (UnitBase.GetCombatX(u) < gateX - PortalGate.EnterTolerance) continue;

            _portalEntered.Add(u);
            _portalGate.PlayEnterEffect(u.transform.position);
            PortalGate.SetUnitVisible(u, false);
            if (u == hero)
            {
                _heroEnteredPortal = true;
                _heroEnteredAt = Time.unscaledTime;
            }
            Debug.Log($"[BattleManager] {u.name} 进门 x={UnitBase.GetCombatX(u):F2} 门X={gateX:F2}");
        }

        // 玩家阵亡 / 不在场时不可能进门 —— 别把流程卡死，按「已进」起算收尾计时。
        if (!_heroEnteredPortal && (hero == null || hero.isDead))
        {
            _heroEnteredPortal = true;
            _heroEnteredAt = Time.unscaledTime;
        }

        if (!_heroEnteredPortal) return;

        bool allIn = true;
        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u == null || u.isDead || u == hero) continue;
            if (!_portalEntered.Contains(u)) { allIn = false; break; }
        }

        if (allIn || Time.unscaledTime - _heroEnteredAt >= PortalMercWaitTimeout)
            FinishStageAfterPortalReached();
    }

    /// <summary>
    /// 新一局 / 新一关 / 通关结算：把传送门整体收掉，并把进过门的单位**重新显示出来**
    /// （进门时只关了渲染，逻辑还在 —— 不还原的话下一关玩家和佣兵就是隐形的）。
    /// </summary>
    void ResetPortalGateState()
    {
        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u != null) PortalGate.SetUnitVisible(u, true);
        }
        if (_portalGate != null)
        {
            _portalGate.Close();
            _portalGate = null;
        }
        _chuanSongMen = null;
        _portalParty.Clear();
        _portalEntered.Clear();
        _heroEnteredPortal = false;
        _heroEnteredAt = 0f;
        _portalOpenedAt = 0f;
    }

    // 【2026-10-11】旧 PlayPortalEnterVfxOnce 已删：进门特效的唯一出口是 PortalGate.PlayEnterEffect
    // （玩家 / 佣兵各播一次，原来那个「只播一次」的写法和主人「佣兵进也要播」的要求冲突）。

    /// <summary>走进 chuansongmen 后：写档 → 结算 → 选关/回城</summary>
    public void FinishStageAfterPortalReached()
    {
        if (_stageCleared) return;
        _stageCleared = true;
        // 【2026-10-15 主人拍板】引导局传送门出口：全员进门 → 引导撤离结算（BattleSettlement → 回城），
        // 不进下面的正常关清场链（不发关卡奖励 / 不推进章节 / 不演选关）。
        if (_tutorialPortalPending)
        {
            _tutorialPortalPending = false;
            ResetPortalGateState();
            isInBattle = false;
            UnitsCanAct = false;
            if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;
            ClearAllMonsters();
            TriggerTutorialEvacuateWithSettlement();
            return;
        }
        // 门收掉 + 进过门的单位重新显示（不还原的话下一关玩家/佣兵是隐形的）
        ResetPortalGateState();
        // 2026-10-06 主人拍板：真正通关一关 → 阵亡过的佣兵养回 30% 血（防 20% 血进场的死亡螺旋）。
        // 挂在「通关」而不是「加载下一关」上：反复重打同一关不会白刷恢复。
        RunLoadout.TickFallenMercsStagePassed();
        // V3.0 压力阀：把本关通关血量喂给下一关（连续轻松通关 → 下一关多一波）
        float clearMaxHp = hero != null && hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : 0f;
        Planner?.NotifyStageResult(false, clearMaxHp > 0f ? hero.currentHp / clearMaxHp : 1f);
        isInBattle = false;
        UnitsCanAct = false;

        if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;

        PersistBattleGold();
        ChapterManager.Instance?.OnStageComplete();

        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : CurrentChapter;
        Analytics.StageEnd(ch, currentStage != null ? currentStage.stageIndex : 0, true,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡通关（胜利）
        bool bossStage = currentStage != null && currentStage.type == StageType.Boss;
        AdventureLogFragments.TryDropOnStageClear(ch, bossStage);

        // 通关结算交给模式：核心不需要知道一共有几种玩法、各自发什么、之后去哪。
        if (Mode == null)
        {
            Debug.LogError("[BattleManager] 通关结算失败：Mode 为空（本局不是从 StartNewRun 开始的？）");
            return;
        }
        Mode.SettleStageClear(this);
    }

    /// <summary>
    /// 主线冒险的通关结算：boss 弹「回城 / 进下一章」选择，普通关进下一关。
    /// 内容原样从 <see cref="FinishStageAfterPortalReached"/> 搬过来，由 <see cref="NormalBattleMode"/> 调用。
    /// 金币本这类模式有自己的结算，不进这里。
    /// </summary>
    public void SettleNormalStageClear()
    {
        bool isBoss = currentStage != null && currentStage.type == StageType.Boss;
        if (isBoss)
        {
            ShowVictorySettlementThen(() =>
            {
                UIManager.Instance?.ShowChapterClearChoice(
                    onReturnTown: EndRunAndReturnToTown,
                    onNextChapter: () =>
                    {
                        // 走路线表：主线 1→2→5→6→7→8，第 8 章之后没有下一章（终局）
                        int cur = ChapterManager.Instance?.currentChapter ?? 1;
                        int next = ChapterRouteTable.NextChapter(cur, SaveSystem.Instance?.Data);
                        if (next < 1)
                        {
                            Debug.Log("[BattleManager] 已是路线终点，直接回城");
                            EndRunAndReturnToTown();
                            return;
                        }
                        ChapterManager.Instance?.StartChapter(next);
                        if (ChapterManager.Instance?.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
                            ChapterManager.Instance.SelectStage(ChapterManager.Instance.stageMap[0]);
                    });
            });
        }
        else
        {
            // 【2026-10-11 主人拍板 · 三】普通关通关**不再弹「本局结算面板」**。
            // 主人原话：「怎么打完一关就结算了，我跟你说的打完一关进传送门，然后弹老虎机怎么没有了」。
            // BattleSettlementUI 的语义是「**一局结束**」：MVP / 总伤害 / 本局累计金币 / 本局装备数，
            // 每通一关都弹一次 = 玩家看到它就以为本局打完了，后面的关卡图 + 老虎机根本没人走到
            // （Editor.log 实锤：结算面板弹出后玩家停了 130 秒直接退出，关卡图一次没起来）。
            // 现在关间流程严格照主人给的那一条：
            //   宝箱 → 传送门 → 走进去 → 过场 → 关卡图（石墩）→ 老虎机演下一关类型 → 落旗 → 点石墩进关。
            // 结算面板只在**整局收尾**弹：章末 Boss（下面 isBoss 分支）/ 死亡 / 撤离 / 回城。
            var cm = ChapterManager.Instance;
            var stages = cm != null ? cm.availableNextStages : null;
            int gateChapter = cm != null ? cm.currentChapter : 1;
            GoChapterGate(gateChapter, stages);
        }
    }

    /// <summary>
    /// 无限裂隙每层通关结算（由 EndlessBattleMode.SettleStageClear 调，2026-10-17 主人拍板）：
    ///   · 宝箱金币每层都发（可重复小额）；
    ///   · 首通奖励（金币+天赋石）只在该层是本次首通时发，记录点层 ×3；
    ///   · 记录点层（5 的倍数）弹「撤离结算 / 继续深入」双选，普通层过场后自动进下一层；
    ///   · 死亡 / 撤离不走这里（走死亡撤离链）——当层什么都不发，已通层奖励已逐层即时发放。
    /// </summary>
    public void SettleEndlessStageClear()
    {
        int stage = Mathf.Max(1, EndlessRiftStage);
        bool isBoss = GameConfig.IsEndlessBossStage(stage);

        // —— 宝箱掉落：每次通关都发 ——
        int chestGold = Mathf.RoundToInt(GameConfig.EndlessChestGold(stage) * GoldGainMul);
        if (chestGold > 0)
        {
            currentGold += chestGold;
            BattleUI.Instance?.UpdateGold(currentGold);
            GlobalToastUI.Show($"宝箱开启，金币 +{chestGold}", true);
        }

        // —— 每 10 层固定宝箱加发普通卷轴（2026-10-17 主人拍板）——
        if (stage % GameConfig.ENDLESS_CHEST_SCROLL_INTERVAL == 0)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                data.mercScrollCommon++;
                GlobalToastUI.Show($"第 {stage} 层固定宝箱：普通卷轴×1", true);
                SaveSystem.Instance?.Save();
            }
        }

        // —— 首通奖励：只在真正推进进度（本次首通）时发 ——
        if (EndlessRiftRules.MarkCleared(stage))
        {
            int mul = isBoss ? GameConfig.ENDLESS_BOSS_FIRSTCLEAR_MUL : 1;
            int gold = Mathf.RoundToInt(GameConfig.EndlessFirstClearGold(stage) * mul * GoldGainMul);
            int talent = GameConfig.EndlessFirstClearTalent(stage) * mul;
            if (gold > 0)
            {
                currentGold += gold;
                BattleUI.Instance?.UpdateGold(currentGold);
            }
            if (talent > 0)
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talent, save: false, notify: true);
            GlobalToastUI.Show(isBoss
                ? $"首通第 {stage} 层记录点！金币 +{gold}，天赋石 +{talent}"
                : $"首通第 {stage} 层：金币 +{gold}，天赋石 +{talent}", true);
            SaveSystem.Instance?.Save();
        }

        // —— 续层 / 记录点双选 ——
        if (isBoss)
        {
            UIManager.Instance?.ShowRiftCheckpointChoice(
                onEvacuate: EndRunAndReturnToTown,
                onContinue: AdvanceEndlessRiftStage);
            return;
        }
        AdvanceEndlessRiftStage();
    }

    /// <summary>
    /// 无限裂隙续层：过场（黑屏 + 本层映射章背景图，正好呈现换景）后重建下一层并进战。
    /// 不打关卡图（石墩）——无限裂隙的关间流程就是「打完一层 → 宝箱 → 下一层」。
    /// </summary>
    void AdvanceEndlessRiftStage()
    {
        EndlessRiftStage++;
        var cm = ChapterManager.Instance;
        if (cm == null)
        {
            Debug.LogError("[BattleManager] 无限裂隙续层失败：ChapterManager 为空，回城收尾");
            EndRunAndReturnToTown();
            return;
        }
        int nextStage = EndlessRiftStage;
        StartCoroutine(CoChapterGateLoading(GameConfig.EndlessChapterForStage(nextStage), () =>
        {
            cm.StartEndlessRift(nextStage);
            if (cm.stageMap != null && cm.stageMap.Count > 0)
                cm.SelectStage(cm.stageMap[0]);
        }));
    }

    /// <summary>
    /// 关间：过场（黑屏 + 本章背景图）→ 关卡选择界面（石墩图）。老虎机在关卡图里演，不在这里。
    /// 普通关与「只有一条路」的关走的是同一条路，不存在跳过关卡图的分支。
    /// </summary>
    void GoChapterGate(int chapter, System.Collections.Generic.List<StageData> stages)
    {
        // 【2026-10-11 主人拍板 · 一】**无论几条路都走完整关卡图流程**
        // （石墩图 → 老虎机演这一关的类型 → 落旗 → 点石墩进关）。
        // 旧实现在「只有一条路」时直接 `cm.SelectStage(next[0])` 跳过整条 ——
        // 于是玩家看不到老虎机，也就不知道下一关是普通/精英/恢复/Boss，
        // 主人反馈「也没有弹出抽奖界面」指的就是这个。
        // 而章节分支表里 **只有分支点才有 2 条路**，第 1 关及绝大多数关都是单路 →
        // 旧逻辑等于「几乎每关都跳过」，老虎机基本播不出来。
        //
        // 【2026-10-11 主人拍板 · 二】进关卡选择界面（石墩图）**之前**先来一次
        // 「黑屏 + 本章背景图」的 loading 过场。主人原话：
        // 「走进传送门后黑屏然后出现 loading 图」「进入关卡选择界面时不用加载 loading 界面吗」。
        // 关卡与关卡之间**不切场景**（LoadStage 只重置状态），本来不会有 loading，
        // 所以这一层是专门为过场显式插的 —— 主人给的每章背景图就显示在这里。
        StartCoroutine(CoChapterGateLoading(chapter, () => UIManager.Instance?.ShowStageSelectUI(stages)));
    }

    /// <summary>进关卡选择界面前那次 loading 过场停留多久（秒）。要调快慢只改这一个数。</summary>
    public const float ChapterGateLoadingSeconds = 1.2f;

    /// <summary>
    /// 「黑屏 + 本章背景图」的 loading 过场（2026-10-11 主人拍板）。
    /// 关卡之间不切场景，本来不会有 loading；主人要这个过场，所以在这里显式插一次。
    /// 复用的就是切场景那套 <see cref="LoadingUI"/>（同源同款，不另造一套过场），
    /// 背景图走 `Resources/UI/loading/beijing0N`。
    /// </summary>
    IEnumerator CoChapterGateLoading(int chapter, System.Action after)
    {
        BattleLoadingOverlay.Show(GameConfig.GetChapterMapName(chapter));
        BattleLoadingOverlay.SetChapterBackdrop(chapter);
        BattleLoadingOverlay.SetProgress(1f);
        yield return new WaitForSecondsRealtime(ChapterGateLoadingSeconds);
        BattleLoadingOverlay.Hide();
        after?.Invoke();
    }

    /// <summary>
    /// 本局构筑收尾：死亡 / 撤离 / 回城统一走这里。
    /// 引导局走内存模式：整包丢掉，不动玩家真正的本局存档；
    /// 金币与天赋石已按阶段写回城镇存档，掉落装备本就不带出。
    ///
    /// ⚠ **局内钱包的作废出口就在这里**（2026-10-05 主人拍板：「打完一章后清零，
    /// 不过玩家可以选择是否进行下一章，这时不清零」）。
    ///   · 回城（章末选「回城」）／阵亡／撤离 → 都走本方法 → **局内钱作废**（currentGold 里的
    ///     启动金 / 通关补贴 / 抽中金币都带不走，只有「本局真正赚到的」经 PersistBattleGold 写回）；
    ///   · 章末选「进下一章」→ <b>不走本方法</b>（`SettleNormalStageClear` 只切章，不收尾）→ **保留**。
    ///   别在别处再写第二份清零。下一局进关由 `SlotMachineSystem.GrantRunStarterGold` 重新补启动金。
    /// </summary>
    void EndRunLoadout()
    {
        if (Rules.Active)
        {
            if (Rules.EnableRunDraft)
            {
                RunLoadout.EndMemoryMode();
                RunSkillBarUI.Refresh();
            }
            return;
        }
        RunLoadout.Clear();
        RunSkillBarUI.Refresh();
    }

    /// <summary>
    /// 通关后「结束本局并回城」：清本局装备 → 清佣兵 → 清雇佣会话 → 结束构筑 → 回城。
    /// 这段原本在通关结算里被复制了 3 份，收成一处；以后改收尾流程只改这里。
    /// 注意：死亡（<see cref="OnHeroDead"/>）与主动撤离（<see cref="TriggerEvacuation"/>）
    /// 走的是另一套收尾（遗产 / 教程流程，且不回城），不共用本方法。
    /// </summary>
    public void EndRunAndReturnToTown()
    {
        GridBackpackSystem.Instance?.ClearRunEquipment();
        MercenaryManager.Instance?.ClearAllMercs();
        MercHireSession.ClearHired();
        EndRunLoadout();
        // 2026-10-09 主人拍板：通关回城也要清掉中断快照，否则通关落盘的金币会在崩溃恢复时被重复入账
        // （金币已在 FinishStageAfterPortalReached → PersistBattleGold 落盘一次，快照不清 = 下次启动 SettleInterruptedRun 再追加一次）
        BattleStateSaver.Instance?.ClearBattleState();
        GameSceneManager.Instance?.ReturnToTown();
    }

    /// <summary>
    /// 把「本局已通关但还没发」的局外金币一次性发掉 —— <b>唯一的发放出口</b>。
    /// <para>2026-10-05 主人拍板「局外在结算界面才发」：通关时只累加 <see cref="_runStageGoldPending"/>，
    /// 到<b>结算这一刻</b>（死亡 / 撤离 / 通关回城三条收尾都要走 <see cref="PersistBattleGold"/>）才入账，
    /// 于是结算界面的「金币 +N」与 HUD 上真正多出来的钱是同一笔，不会出现「战斗中先涨、结算再涨一次」。</para>
    /// ⚠ 别在别处再写第二份发放 —— 多发一次就是刷金。
    /// </summary>
    public void GrantPendingStageGold()
    {
        if (_runStageGoldPending <= 0) return;
        long gold = _runStageGoldPending;
        _runStageGoldPending = 0;
        currentGold += gold;
        BattleUI.Instance?.UpdateGold(currentGold);
        Debug.Log($"[BattleManager] 结算发放局外金币 +{gold}（本局通过关卡累计）");
    }

    void PersistBattleGold()
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;
        // 结算才发的局外金币：必须在算差额之前入账，否则写不回存档（钱就凭空少了）
        GrantPendingStageGold();
        // 战斗内金币写回城镇：差额走 ResourceWallet，避免突破上限。
        // 【2026-10-10 主人拍板】战斗钱包局内化后，基准不再是城镇金币而是**本局起点** _goldAtRunStart
        // （它已含启动金 / 通关补贴 / 抽中金币这些「进得来带不走」的钱）→
        // delta = 本局真正赚到的（击杀掉落 + 结算按关卡数发的那笔），抽奖花掉的已经扣在里面。
        // 【2026-10-17 主人拍板】只加不减：delta < 0 意味着「启动金等局内循环资源被花掉且没赚回来」
        // —— 抽奖有 CanAffordDraw 拦着，亏的只可能是「进得来带不走」的钱，花光就花光，
        // 绝不倒扣城镇钱包（否则玩家打一趟本城镇金币反而变少）。
        long delta = currentGold - _goldAtRunStart;
        if (delta > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, delta, save: true, notify: false);
        else
            SaveSystem.Instance.Save();
        currentGold = _goldAtRunStart;
    }

    #endregion
    #region 死亡与撤离
    public void OnHeroDead()
    {
        Planner?.NotifyStageResult(true, 0f); // V3.0 压力阀：死过 → 下一关减一波
        isInBattle = false;
        Analytics.StageEnd(CurrentChapter, currentStage != null ? currentStage.stageIndex : 0, false,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡结束（阵亡/失败）
        MercenaryManager.Instance?.ClearAllMercs();
        AdventureLogAchievements.OnDied();
        EndRunLoadout();
        // 引导关死亡：不走遗产，也不标记教程通关 —— 重进引导战斗（2026-09-27 主人拍板）
        if (Rules.Active || SkipLegacyOnEvacuate)
        {
            RestartTutorialBattle();
            return;
        }
        TriggerLegacyFlow(isDeath: true);
    }

    public void TriggerEvacuation()
    {
        TryGrantStageQuestGoldIfQuestComplete();
        isInBattle = false;
        Analytics.StageEnd(CurrentChapter, currentStage != null ? currentStage.stageIndex : 0, false,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡结束（主动撤离）
        ClearAllMonsters();
        MercenaryManager.Instance?.ClearAllMercs();
        // 主动撤离 = 本局结束：构筑作废，但金币/天赋石已入账
        EndRunLoadout();
        if (!Rules.SkipMercHireClearOnEvacuate)
            MercHireSession.ClearHired();
        AdventureLogAchievements.OnEvacuated(Rules.Active);
        // 撤离回城后打开冒险页（引导局另有收尾，不抢页签）
        if (!Rules.SkipPendingAdventureOnEvacuate)
            TownHubController.PendingOpenAdventure = true;
        // 教程也走结算界面，关闭后再回城接剧情
        if (SkipLegacyOnEvacuate || Rules.Active)
        {
            TriggerTutorialEvacuateWithSettlement();
            return;
        }
        TriggerLegacyFlow(isDeath: false);
    }

    /// <summary>引导撤离：弹出结算 → 再回城（TownAfterBattle 剧情）。</summary>
    void TriggerTutorialEvacuateWithSettlement()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        if (!_stageQuestGoldGranted && StageQuestClearGold > 0)
            TryGrantStageQuestGold();

        // 结算前先快照；局内装备仍清（裂缝口径）
        GridBackpackSystem.Instance?.ClearRunEquipment();
        PersistBattleGold();
        // 【2026-10-12 主人拍板「为什么没有获得金币，多少给点才能点天赋」】
        // 引导三拍把启动金花得只剩几个币，局内净增 ≤ 0 → 结算不显示金币、回城点不起天赋第一节点。
        // 通关固定发一笔（SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD），直接入账城镇钱包。
        if (SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD,
                save: true, notify: false);
            Debug.Log($"[BattleManager] 引导通关金币 +{SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD}");
        }
        // 2026-09-19：天赋石不再由战斗内金币换算产出，改由冒险日志里程碑（首次/整章通关）发放。
        int talentGain = 0;
        StoryProgress.MarkTutorialBattleCleared();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();

        FillSettlementSnapshot(isDeath: false, talentGain);
        // 引导局 GoldGained 按局内净增算会被三拍抽花费冲成 0 → 结算「获得奖励」直接显示这笔通关金币
        // （long 字段，不用 Mathf.Max——没有 long 重载会 CS0266）
        long tutorialGold = SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD;
        if (RunStats.GoldGained < tutorialGold) RunStats.GoldGained = tutorialGold;
        AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        BattleSettlementUI.Show(RunStats, () =>
        {
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    /// <summary>
    /// 【2026-10-15 主人拍板】引导结尾传送门出口：「所有打完一关后都出现传送门，进传送门的步骤」照旧 ——
    /// 玩家先走、佣兵隔一个身位跟着进，每人进门各播一次传送特效（见 <see cref="TickPortalWalkIn"/>）；
    /// 全员进完（或超时）经 <see cref="FinishStageAfterPortalReached"/> 的引导分支接撤离结算 → 回城。
    /// 门开不出来也不锁死玩家：LogError 后直接走撤离结算。
    /// </summary>
    public void BeginTutorialPortalExit()
    {
        if (_tutorialPortalPending) return;
        _tutorialPortalPending = true;
        var gate = PortalGate.Open(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        if (gate != null)
        {
            NotifyChuanSongMenOpened(gate);
            return;
        }
        Debug.LogError("[BattleManager] 引导结尾传送门没开出来（PortalGate.Open 返回空，详见其上一条报错），直接走撤离结算");
        _tutorialPortalPending = false;
        TriggerTutorialEvacuateWithSettlement();
    }

    /// <summary>
    /// 引导局阵亡收尾：重新进入引导战斗（重头走引导步骤）。
    /// 2026-09-27 主人拍板：旧实现是「MarkTutorialBattleCleared + 回城」，
    /// 等于把引导里的一次阵亡当成通关 —— 回主界面就直接加载引导后续剧情。
    /// 现在只重进战斗场景（TutorialBattleCleared 没写 → ShouldStartTutorialBattle 仍为 true，
    /// 重新加载后还是引导局），金币照常写回，通关奖励不再发。
    /// </summary>
    void RestartTutorialBattle()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        GridBackpackSystem.Instance?.ClearRunEquipment();
        // currentGold 含城镇底金，必须走差额写回，禁止整额 Add
        PersistBattleGold();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();

        var gsm = GameSceneManager.Instance;
        if (gsm == null)
        {
            Debug.LogError("[BattleManager] 引导局阵亡后重进战斗失败：GameSceneManager 为空");
            return;
        }
        Debug.LogWarning("[BattleManager] 引导局阵亡：重新进入引导战斗");
        gsm.LoadBattleScene();
    }

    /// <summary>
    /// 死亡 / 撤离结算：构筑（装备/技能/佣兵）一律清空；
    /// 金币与材料/附魔石等资源一律保留 —— 死亡也保留本局赚到的金币，并按金币产出天赋石。
    /// </summary>
    void TriggerLegacyFlow(bool isDeath)
    {
        // 裂缝口径：局内装备/武器退出清空，不带出城镇
        GridBackpackSystem.Instance?.ClearRunEquipment();

        PersistBattleGold();
        // 金币保留 → 天赋石照发（死亡与撤离同规则）
        int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
        if (talentGain > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);

        // 【2026-10-17 主人拍板】撤离补偿（替代已停掉的精英事件强化石）：
        // 强化石现已不再掉落，撤离按本局已通关关数每关 +1 天赋石，中途撤离不空手。
        // 只在主动撤离（isDeath=false）发；死亡不发；引导局不走本方法（另有固定通关金币）。
        // 并入 talentGain，让结算界面「天赋石 +N」包含这笔；落盘由下方 Save() 统一做。
        if (!isDeath && _runStagesCleared > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, _runStagesCleared, save: false, notify: true);
            Debug.Log($"[BattleManager] 撤离补偿：本局已通关 {_runStagesCleared} 关 → 天赋石 +{_runStagesCleared}");
            talentGain += _runStagesCleared;
        }

        SaveSystem.Instance?.Save();
        BattleStateSaver.Instance?.ClearBattleState();

        FillSettlementSnapshot(isDeath, talentGain);
        if (!isDeath)
            AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        TownHubController.PendingOpenAdventure = true;
        BattleSettlementUI.Show(RunStats, () =>
        {
            MercHireSession.ClearHired();
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    void FillSettlementSnapshot(bool isDeath, int talentGain, bool isVictory = false)
    {
        RunStats.IsDeath = isDeath;
        RunStats.IsVictory = isVictory && !isDeath;
        RunStats.GoldGained = Mathf.Max(0, (int)(currentGold - _goldAtRunStart));   // 死亡也保留金币，结算照常显示
        RunStats.TalentGained = talentGain;
        RunStats.EquipCount = GridBackpackSystem.Instance != null
            ? GridBackpackSystem.Instance.GetAllItemsForLegacy().Count
            : 0;
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            RunStats.EnchantStoneDelta = Mathf.Max(0, data.enchantStones - _enchantAtRunStart);
            RunStats.DecomposeMatDelta = Mathf.Max(0, data.decomposeMats - _matsAtRunStart);
            RunStats.Chapter = ChapterManager.Instance != null
                ? ChapterManager.Instance.currentChapter
                : CurrentChapter;
        }
        if (currentStage != null)
            RunStats.StageTitle = ChapterManager.Instance != null
                ? ChapterManager.Instance.GetCurrentStageDisplayName()
                : GameConfig.GetChapterMapName(RunStats.Chapter);
        else
            RunStats.StageTitle = GameConfig.GetChapterMapName(RunStats.Chapter);
        RunStats.ResolveMvp();

        // 2026-10-13：数据埋点（TapTap 数据分析）。全部取自上面已算好的结算快照，不额外采集。
        ReportRunEndAnalytics();
    }

    /// <summary>把结算快照转为一条 #run_end 埋点。失败不阻塞结算流程。</summary>
    void ReportRunEndAnalytics()
    {
        try
        {
            GameAnalytics.ReportRunEnd(
                isDeath: RunStats.IsDeath,
                isVictory: RunStats.IsVictory,
                chapter: RunStats.Chapter,
                stageId: currentStage != null ? currentStage.stageIndex : 0,
                goldGained: RunStats.GoldGained,
                talentGained: RunStats.TalentGained,
                equipCount: RunStats.EquipCount,
                stoneDelta: RunStats.EnchantStoneDelta,
                decomposeDelta: RunStats.DecomposeMatDelta,
                mvp: RunStats.MvpKey,
                durationSec: RunStats.BattleTimeSec);
            GameAnalytics.AddRunCount();
            if (RunStats.IsDeath) GameAnalytics.AddDeathCount();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] #run_end 埋点失败：{e.Message}");
        }
    }

    /// <summary>通关后弹结算，再执行后续选关/回城。</summary>
    public void ShowVictorySettlementThen(System.Action afterConfirm)
    {
        // 金币已在通关时 PersistBattleGold 写回；天赋石改由冒险日志里程碑发放，此处不再按金币补发。
        int talentGain = 0;
        FillSettlementSnapshot(isDeath: false, talentGain, isVictory: true);
        AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        BattleSettlementUI.Show(RunStats, afterConfirm);
    }

    #endregion
    #region 战斗统计
    public void RecordDamageDealt(float amount, bool toBoss)
    {
        if (amount <= 0f) return;
        RunStats.DamageDealt += amount;
        if (toBoss) RunStats.BossDamageDealt += amount;
    }

    public void RecordDamageTaken(float amount)
    {
        if (amount <= 0f) return;
        RunStats.DamageTaken += amount;
    }

    public void RecordCrit()
    {
        RunStats.CritCount++;
    }

    public static string GetAllyMvpKey(UnitBase u)
    {
        if (u == null) return BattleRunStats.PlayerMvpKey;
        if (u is Mercenary merc)
        {
            if (!string.IsNullOrEmpty(merc.hireId)) return merc.hireId;
            if (!string.IsNullOrEmpty(merc.mercId)) return merc.mercId;
            return "merc";
        }
        return BattleRunStats.PlayerMvpKey;
    }

    public static string GetAllyMvpDisplayName(UnitBase u)
    {
        if (u is Mercenary merc)
        {
            // 2026-10-09 主人拍板：结算 MVP 必须显示花名册**真名**（塔克），不是战斗名签用的**花名**（重盾）。
            // 只在这一个出口改优先级，不动 Mercenary.SetDisplayName 的全局口径（那会影响所有战斗名签）。
            MercRosterDefs.Def def;
            if (!string.IsNullOrEmpty(merc.hireId) && MercRosterDefs.TryGetByHireId(merc.hireId, out def)
                && !string.IsNullOrEmpty(def.Name))
                return def.Name;
            if (!string.IsNullOrEmpty(merc.mercId) && MercRosterDefs.TryGetByAssetId(merc.mercId, out def)
                && !string.IsNullOrEmpty(def.Name))
                return def.Name;
            if (!string.IsNullOrEmpty(merc.DisplayName)) return merc.DisplayName;
            // 【2026-10-11 主人拍板「佣兵名字要统一」】旧写法在这里还留了一条「退回昵称（重盾）」的兜底，
            // 结果花名册没真名时结算 MVP 会写出第二个叫法 → 删掉；缺真名就退回编号，不拿昵称顶（铁律 14）。
            return !string.IsNullOrEmpty(merc.mercId) ? merc.mercId : "佣兵";
        }
        var save = SaveSystem.Instance?.Data;
        return save != null && !string.IsNullOrEmpty(save.playerDisplayName)
            ? save.playerDisplayName : "冒险者";
    }

    public void RecordAllyDamage(UnitBase source, float amount)
    {
        if (source == null || !source.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.damage += amount;
    }

    public void RecordAllyKill(UnitBase source)
    {
        if (source == null || !source.isAlly) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.kills++;
    }

    public void RecordAllyHeal(UnitBase healer, float amount)
    {
        if (healer == null || !healer.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(healer), GetAllyMvpDisplayName(healer));
        c.healingDone += amount;
    }

    public void ClearAllMonsters()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            m.OnDead -= OnMonsterDead;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Release(m.gameObject);
            else
                Destroy(m.gameObject);
        }
        monsters.Clear();
        _aliveMonsterCache = -1;
    }

    // ============================================================
    // 特殊关卡
    // ============================================================

    // 商人/诅咒/锻造/附魔：本版不进池，保留方法供后续开放
    [System.Obsolete("本版未开放商人关")]
    void LoadMerchantStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放商人关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    [System.Obsolete("本版未开放诅咒关")]
    void LoadCurseStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放诅咒关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    /// <summary>
    /// 锻造关：弹锻造窗（发强化材料），关掉后继续推关。
    /// </summary>
    void LoadForgeStage()
    {
        CraftStagePopupUI.ShowForge(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadEnchantStage()
    {
        CraftStagePopupUI.ShowEnchant(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadRestStage()
    {
        // 【2026-10-11 主人报「老虎机轮到恢复关卡后弹不出界面 卡到这了」】
        // 进恢复关先打一条：主人下次卡住时，Editor.log 里有这行 = 已经进关、问题在弹窗本身；
        // 没有这行 = 压根没进关（卡在关卡图/老虎机那一段），查 BattleStageMapUI。
        Debug.Log("[BattleManager] 进入恢复关 → 准备弹 RestStagePopup");
        // 弹窗里已经回过 50% 血；关掉后继续推关
        UIManager.Instance.ShowRestUI(() =>
        {
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        }, null);
    }

    List<CurseBuff> GenerateCurseOptions()
    {
        // 2026-09-29：攻击% 按职业分流到 Attack / MagicAttack，否则法师/牧师选「嗜血」=
        // 加在用不上的物攻上，选「疾风」= 扣了个自己没有的东西。
        AttrType atk = PlayerJobBaseStats.CurrentAttackAttr();
        return new List<CurseBuff>
        {
            new CurseBuff { buffName = "嗜血：攻击+30%，生命-15%", buff = new AttrBonusData { attrType = atk, value = 0.3f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MaxHp, value = -0.15f, isPercent = true } },
            new CurseBuff { buffName = "疾风：攻速+40%，攻击-20%", buff = new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.4f, isPercent = true }, debuff = new AttrBonusData { attrType = atk, value = -0.2f, isPercent = true } },
            new CurseBuff { buffName = "坚壁：生命+50%，移速-30%", buff = new AttrBonusData { attrType = AttrType.MaxHp, value = 0.5f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MoveSpeed, value = -0.3f, isPercent = true } }
        };
    }
    #endregion
}