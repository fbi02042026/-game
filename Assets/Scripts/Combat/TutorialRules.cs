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
    /// 引导关是否也开「局内构筑」（升级三选一 / 佣兵 / 技能三选一）。
    /// 以前引导关整条构筑链是关掉的（玩家学不到核心循环），现在打开，
    /// 但只放 <see cref="MaxTutorialDrafts"/> 次技能三选一，且卡组由 TutorialDirector 固定。
    /// </summary>
    public bool EnableRunDraft { get; private set; }

    /// <summary>引导关允许的技能三选一上限（0 = 不限）。</summary>
    public int MaxTutorialDrafts { get; private set; }

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
            SpawnStagger = 0.35f,
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
            MaxTutorialDrafts = 1,
            EngageMinAhead = 4.5f,
            EngageAheadOverride = 4.5f,
            WaveSpacingMul = 1.65f,
            SpawnStagger = 0.65f,
            MonsterCountMul = 1f
        };
    }
}
