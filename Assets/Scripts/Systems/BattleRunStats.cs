using System;

/// <summary>
/// 本局战斗统计快照（GDD §11.3 结算界面）。
/// </summary>
[Serializable]
public class BattleRunStats
{
    public int KillCount;
    public int EliteKillCount;
    public int BossKillCount;
    public float DamageDealt;
    public float BossDamageDealt;
    public float DamageTaken;
    public float BattleTimeSec;
    public long GoldGained;
    public int TalentGained;
    public int EquipCount;
    public int EnchantStoneDelta;
    public int DecomposeMatDelta;
    public bool IsDeath;
    public int Chapter;
    public string StageTitle;

    public void Reset()
    {
        KillCount = 0;
        EliteKillCount = 0;
        BossKillCount = 0;
        DamageDealt = 0f;
        BossDamageDealt = 0f;
        DamageTaken = 0f;
        BattleTimeSec = 0f;
        GoldGained = 0;
        TalentGained = 0;
        EquipCount = 0;
        EnchantStoneDelta = 0;
        DecomposeMatDelta = 0;
        IsDeath = false;
        Chapter = 1;
        StageTitle = "";
    }
}
