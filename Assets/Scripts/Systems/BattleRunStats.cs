using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本局战斗统计快照（结算界面）。
/// </summary>
[Serializable]
public class BattleRunStats
{
    public const string PlayerMvpKey = "player";
    public const float MvpDamageWeight = 1f;
    public const float MvpHealWeight = 0.5f;
    /// <summary>每击杀折合固定分（约一只小怪血量量级）。</summary>
    public const float MvpKillScore = 80f;

    public int KillCount;
    public int EliteKillCount;
    public int BossKillCount;
    public float DamageDealt;
    public float BossDamageDealt;
    public float DamageTaken;
    public float BattleTimeSec;
    public int CritCount;
    public int MaxKillCombo;
    public float HealingReceived;
    public long GoldGained;
    public int DiamondGained;
    public int TalentGained;
    public int EquipCount;
    public int EnchantStoneDelta;
    public int DecomposeMatDelta;
    public bool IsDeath;
    public bool IsVictory;
    public int Chapter;
    public string StageTitle;

    /// <summary>本场最佳：player 或佣兵 hireId。</summary>
    public string MvpKey = PlayerMvpKey;
    public string MvpDisplayName = "冒险者";
    public float MvpScore;

    [Serializable]
    public class AllyContribution
    {
        public string key;
        public string displayName;
        public float damage;
        public int kills;
        public float healingDone;

        public float Score =>
            damage * MvpDamageWeight + kills * MvpKillScore + healingDone * MvpHealWeight;
    }

    public List<AllyContribution> AllyContributions = new List<AllyContribution>();

    public void Reset()
    {
        KillCount = 0;
        EliteKillCount = 0;
        BossKillCount = 0;
        DamageDealt = 0f;
        BossDamageDealt = 0f;
        DamageTaken = 0f;
        BattleTimeSec = 0f;
        CritCount = 0;
        MaxKillCombo = 0;
        HealingReceived = 0f;
        GoldGained = 0;
        DiamondGained = 0;
        TalentGained = 0;
        EquipCount = 0;
        EnchantStoneDelta = 0;
        DecomposeMatDelta = 0;
        IsDeath = false;
        IsVictory = false;
        Chapter = 1;
        StageTitle = "";
        MvpKey = PlayerMvpKey;
        MvpDisplayName = "冒险者";
        MvpScore = 0f;
        AllyContributions.Clear();
    }

    public AllyContribution EnsureAlly(string key, string displayName)
    {
        if (string.IsNullOrEmpty(key)) key = PlayerMvpKey;
        for (int i = 0; i < AllyContributions.Count; i++)
        {
            if (AllyContributions[i] != null && AllyContributions[i].key == key)
            {
                if (!string.IsNullOrEmpty(displayName))
                    AllyContributions[i].displayName = displayName;
                return AllyContributions[i];
            }
        }
        var c = new AllyContribution
        {
            key = key,
            displayName = string.IsNullOrEmpty(displayName) ? key : displayName
        };
        AllyContributions.Add(c);
        return c;
    }

    /// <summary>综合分最高者；平分优先玩家。</summary>
    public void ResolveMvp()
    {
        AllyContribution best = null;
        float bestScore = -1f;
        for (int i = 0; i < AllyContributions.Count; i++)
        {
            var c = AllyContributions[i];
            if (c == null) continue;
            float s = c.Score;
            bool better = s > bestScore + 0.01f
                || (Mathf.Abs(s - bestScore) <= 0.01f && c.key == PlayerMvpKey);
            if (better)
            {
                bestScore = s;
                best = c;
            }
        }
        if (best == null)
        {
            MvpKey = PlayerMvpKey;
            MvpDisplayName = "冒险者";
            MvpScore = 0f;
            return;
        }
        MvpKey = best.key;
        MvpDisplayName = string.IsNullOrEmpty(best.displayName) ? best.key : best.displayName;
        MvpScore = bestScore;
    }
}

/// <summary>结算奖励格（运行时填充，不含经验）。</summary>
[Serializable]
public class SettlementRewardCell
{
    public string label;
    public int count;
    public Sprite icon;
    public Color frameColor = new Color(0.55f, 0.45f, 0.25f, 1f);
}
