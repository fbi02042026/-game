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

    /// <summary>交战点最少超前（引导 4.5，正式 2.0）。</summary>
    public float EngageMinAhead { get; private set; }

    /// <summary>&gt;0 时交战距离 = max(MONSTER_ENGAGE_OFFSET, 本值)。≤0 只用表外常量。</summary>
    public float EngageAheadOverride { get; private set; }

    public float WaveSpacingMul { get; private set; }
    public float SpawnStagger { get; private set; }

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
            SpawnStagger = 0.35f
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
            EngageMinAhead = 4.5f,
            EngageAheadOverride = 4.5f,
            WaveSpacingMul = 1.65f,
            SpawnStagger = 0.65f
        };
    }
}
