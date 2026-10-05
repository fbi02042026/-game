/// <summary>
/// 本局战斗规则包。引导特例收拢于此，避免往 BattleManager 核心循环继续堆 IsTutorialRun。
/// 正式关用 Formal；引导关用 Tutorial。身份判断仍可用 BattleManager.IsTutorialRun（= Rules.Active）。
/// Phase 4 之后禁止再往 BM 核心循环加新的 IsTutorialRun 分支；新旗标加在本包。
/// </summary>
public sealed class TutorialRules
{
    public static readonly TutorialRules Formal = CreateFormal();
    public static readonly TutorialRules Tutorial = CreateTutorial();

    public static TutorialRules Current
    {
        get
        {
            var bm = BattleManager.Instance;
            return bm != null && bm.Rules != null ? bm.Rules : Formal;
        }
    }

    public bool Active { get; private set; }

    public bool SkipMercPrecall { get; private set; }
    public bool SuppressStageClear { get; private set; }
    public bool SkipLegacyOnEvacuate { get; private set; }
    public bool SkipWaveAnnounce { get; private set; }
    public bool SkipFirstWaveAuto { get; private set; }
    public bool DirectorOwnsWaves { get; private set; }
    public bool SkipChapter1Ending { get; private set; }
    public bool SkipChapter1RunModifiers { get; private set; }
    public bool SkipRiftEnteredAchievement { get; private set; }
    public bool SkipMercDeathBanter { get; private set; }
    public bool SkipLaodunAchievement { get; private set; }
    public bool SkipStarterWeaponFallback { get; private set; }
    public bool HideKillComboHud { get; private set; }
    public bool SkipMercHireClearOnEvacuate { get; private set; }
    public bool SkipPendingAdventureOnEvacuate { get; private set; }
    public bool SkipMercBanter { get; private set; }
    public bool UseTutorialSplash { get; private set; }
    public bool UseTutorialSprites { get; private set; }
    public bool ApplyTableMonsterHp { get; private set; }
    public bool QuestCountsOnlySpawnedWaves { get; private set; }
    public bool AllowStackSpawnWhileAlive { get; private set; }

    /// <summary>
    /// 引导关是否也开「局内构筑」（装备 / 佣兵 / 技能都从抽奖来）。
    /// 以前引导关整条构筑链是关掉的（玩家学不到核心循环），现在打开。
    /// 2026-10-05：教学拍已改成正式抽奖入口 <c>BattleManager.CoMidBattleDraft</c>，
    /// 卡池与保底规则都在正式系统里（引导三拍的类型定序 = <c>SlotMachineDefs.TutorialDrawOrder</c>），
    /// 引导不再自带一套。⚠ 定序**只有引导局走**，正式关从第 1 抽起就是等权随机。
    /// </summary>
    public bool EnableRunDraft { get; private set; }

    /// <summary>
    /// 引导关开局是否「强制引导玩家抽奖一次」（圈住随机按钮、抽完收起）。
    /// 2026-09-29 主人要求：引导关一开始就教玩家用进关抽奖，之后按既有节拍一步一步走。
    /// 正式关恒 false —— 不抽就直接开打，不给打扰。
    ///
    /// <para><b>== 引导「每拍只抽一次」这个开关是唯一真源 ==</b>
    /// 2026-10-05 主人拍板：「引导的时候每次只能抽一次，然后还是按那个顺序引导」。
    /// 为 <c>true</c> 时 <c>BattleManager.OneDrawPerBeat</c> 也为 true →
    /// ① 开局（<c>CoStageEntryDraft</c>）抽一抽就收面板开打；
    /// ②③ 打完两波 / 再两波那两拍走 <c>CoMidBattleDraft</c>（它本身一拍一抽）。
    /// 三拍 × 一抽 = 保底定序走满三格 = <b>装备 → 佣兵 → 技能</b>，顺序不另写一份。</para>
    /// </summary>
    public bool GuideEntryDraft { get; private set; }

    /// <summary>
    /// 引导局：抽奖抽到「佣兵」时给该佣兵的**本命碎片**而不是直接招募。
    /// 2026-10-05 主人拍板「保留救援戏，抽奖给小白碎片」—— 小白走第 4 拍剧情救援入队，
    /// 抽奖再招一个会跟剧情打架，所以这一抽发的是 `frag:H011`。
    /// 正式关恒 false：抽到佣兵就是正常招募。
    /// </summary>
    public bool MercDraftGivesFragment { get; private set; }

    /// <summary>交战点最少超前（引导 4.5，正式 2.0）。</summary>
    public float EngageMinAhead { get; private set; }

    /// <summary>&gt;0 时交战距离 = max(MONSTER_ENGAGE_OFFSET, 本值)。≤0 只用表外常量。</summary>
    public float EngageAheadOverride { get; private set; }

    public float WaveSpacingMul { get; private set; }
    public float SpawnStagger { get; private set; }

    /// <summary>
    /// 引导关每波人数相对 tutorial_battle 表的缩放，<b>当前 1.0 = 不缩放</b>。
    /// 引导关人数真源就是 CSV，代码不叠乘——之前"实际怪比表里多"是
    /// <c>WavePlanner.QueueTutorialWaveCore</c> 的补刷 bug（同帧缓存误判导致补刷一整波、数量翻倍），
    /// 已修，不要再靠改这个倍率去压数量。临时调难度才动这里。
    /// </summary>
    public float MonsterCountMul { get; private set; }

    /// <summary>
    /// 按本局规则缩放一波人数。只作用于引导关；正式关原样返回，避免在核心刷怪轨上分叉。
    /// 最小 1 只；两侧夹击/围攻（flank/around）保底 2 只，否则阵型失去意义。
    /// </summary>
    public int ScaleMonsterCount(int tableCount, bool keepFormation = false)
    {
        if (!Active || tableCount <= 0) return tableCount;
        if (UnityEngine.Mathf.Approximately(MonsterCountMul, 1f)) return tableCount;

        int scaled = UnityEngine.Mathf.RoundToInt(tableCount * MonsterCountMul);
        if (scaled > tableCount) scaled = tableCount;
        if (scaled < 1) scaled = 1;
        if (keepFormation && scaled < 2) scaled = 2;
        return scaled;
    }

    public float ResolveEngageAhead(float defaultOffset)
    {
        if (EngageAheadOverride > 0f)
            return UnityEngine.Mathf.Max(defaultOffset, EngageAheadOverride);
        return defaultOffset;
    }

    static TutorialRules CreateFormal()
    {
        return new TutorialRules
        {
            Active = false,
            EngageMinAhead = 2.0f,
            EngageAheadOverride = -1f,
            WaveSpacingMul = 1f,
            // 【2026-10-05 主人拍板「多点出生点」】入场起点已按序号往外错开，不再靠长间隔排队出场。
            // 只留 0.12s 把实例化帧错开（防同帧卡顿），一整波 8 只在 0.84s 内全部出海。
            SpawnStagger = 0.12f,
            MonsterCountMul = 1f
        };
    }

    static TutorialRules CreateTutorial()
    {
        return new TutorialRules
        {
            Active = true,
            SkipMercPrecall = true,
            SuppressStageClear = true,
            SkipLegacyOnEvacuate = true,
            SkipWaveAnnounce = true,
            SkipFirstWaveAuto = true,
            DirectorOwnsWaves = true,
            SkipChapter1Ending = true,
            SkipChapter1RunModifiers = true,
            SkipRiftEnteredAchievement = true,
            SkipMercDeathBanter = true,
            SkipLaodunAchievement = true,
            SkipStarterWeaponFallback = true,
            HideKillComboHud = true,
            SkipMercHireClearOnEvacuate = true,
            SkipPendingAdventureOnEvacuate = true,
            SkipMercBanter = true,
            UseTutorialSplash = true,
            UseTutorialSprites = true,
            ApplyTableMonsterHp = true,
            QuestCountsOnlySpawnedWaves = true,
            AllowStackSpawnWhileAlive = true,
            EnableRunDraft = true,
            GuideEntryDraft = true,
            MercDraftGivesFragment = true,
            EngageMinAhead = 4.5f,
            EngageAheadOverride = 4.5f,
            WaveSpacingMul = 1.65f,
            // 同上：0.65 → 0.12。教程围攻/夹击波走 staggerOverride=0（同帧同时到场），不受影响。
            SpawnStagger = 0.12f,
            MonsterCountMul = 1f
        };
    }
}
