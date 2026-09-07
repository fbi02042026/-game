using UnityEngine;

/// <summary>
/// 佣兵 AI：职业 × 稀有度 → 目标优先级与站位意图（可执行子集，不含预判/传奇 Combo）。
/// </summary>
public static class MercAiPolicy
{
    public enum JobKind
    {
        Unknown,
        Shield,
        Heavy,
        Berserker,
        Ranger,
        Mage,
        Priest
    }

    public enum TargetPriority
    {
        Nearest,
        LowestHp,
        FocusOrNearPlayer,
        FocusPrefer,
        DenseCluster,
        ThreatNearPlayer
    }

    public enum Stance
    {
        Default,
        BetweenPlayerAndEnemy,
        FrontOfPlayer,
        KeepMidRange,
        KeepFar,
        Backline
    }

    public struct Policy
    {
        public JobKind Job;
        public TargetPriority Priority;
        public Stance Stance;
        public bool GuardPlayerWhenLowHp;
        public float PreferFocusWeight;
    }

    public static JobKind ResolveJob(string jobName)
    {
        if (string.IsNullOrEmpty(jobName)) return JobKind.Unknown;
        if (jobName.Contains("剑盾") || jobName.Contains("盾")) return JobKind.Shield;
        if (jobName.Contains("重武")) return JobKind.Heavy;
        if (jobName.Contains("狂战")) return JobKind.Berserker;
        if (jobName.Contains("游侠")) return JobKind.Ranger;
        if (jobName.Contains("牧师")) return JobKind.Priest;
        if (jobName.Contains("法师")) return JobKind.Mage;
        return JobKind.Unknown;
    }

    public static Policy Get(string jobName, MercRosterDefs.MercRarity rarity)
    {
        var job = ResolveJob(jobName);
        var p = new Policy
        {
            Job = job,
            Priority = TargetPriority.Nearest,
            Stance = Stance.Default,
            GuardPlayerWhenLowHp = false,
            PreferFocusWeight = rarity >= MercRosterDefs.MercRarity.Rare ? 2.5f : 1.25f
        };

        switch (job)
        {
            case JobKind.Shield:
                p.Priority = rarity >= MercRosterDefs.MercRarity.Rare
                    ? TargetPriority.FocusOrNearPlayer
                    : TargetPriority.ThreatNearPlayer;
                p.Stance = Stance.BetweenPlayerAndEnemy;
                p.GuardPlayerWhenLowHp = true;
                break;
            case JobKind.Heavy:
                p.Priority = rarity >= MercRosterDefs.MercRarity.Rare
                    ? TargetPriority.FocusPrefer
                    : TargetPriority.Nearest;
                p.Stance = Stance.FrontOfPlayer;
                break;
            case JobKind.Berserker:
                p.Priority = rarity >= MercRosterDefs.MercRarity.Rare
                    ? TargetPriority.LowestHp
                    : TargetPriority.Nearest;
                if (rarity >= MercRosterDefs.MercRarity.Legendary)
                    p.Priority = TargetPriority.FocusPrefer;
                break;
            case JobKind.Ranger:
                p.Priority = rarity >= MercRosterDefs.MercRarity.Rare
                    ? TargetPriority.FocusPrefer
                    : TargetPriority.Nearest;
                p.Stance = Stance.KeepMidRange;
                break;
            case JobKind.Mage:
                p.Priority = rarity >= MercRosterDefs.MercRarity.Rare
                    ? TargetPriority.FocusPrefer
                    : TargetPriority.DenseCluster;
                p.Stance = Stance.KeepFar;
                break;
            case JobKind.Priest:
                p.Priority = TargetPriority.Nearest;
                p.Stance = Stance.Backline;
                break;
        }

        return p;
    }

    public const float FocusMarkRange = 2.35f;
    public const float MidRangeIdeal = 3.2f;
    public const float FarRangeIdeal = 4.2f;
    public const float ClusterRadius = 1.8f;
    public const float PlayerLowHpRatio = 0.3f;

    public static UnitBase PickTarget(Mercenary merc, Policy policy, UnitBase sticky)
    {
        if (BattleManager.Instance == null || merc == null) return null;
        var monsters = BattleManager.Instance.monsters;
        if (monsters == null || monsters.Count == 0) return null;

        // 粘滞：打完当前怪前不因集火切换目标
        if (sticky != null && !sticky.isDead && sticky.isAlly != merc.isAlly)
            return sticky;

        UnitBase focus = FocusMarkSystem.MarkedEnemy;
        float myX = UnitBase.GetCombatX(merc);
        float heroX = Hero.Instance != null ? UnitBase.GetCombatX(Hero.Instance) : myX;

        UnitBase best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < monsters.Count; i++)
        {
            var e = monsters[i];
            if (e == null || e.isDead) continue;

            float distMe = Mathf.Abs(myX - UnitBase.GetCombatX(e));
            float distHero = Mathf.Abs(heroX - UnitBase.GetCombatX(e));
            float hpRatio = e.attr != null
                ? e.currentHp / Mathf.Max(1f, e.attr.GetAttr(AttrType.MaxHp))
                : 1f;

            float score = 0f;
            switch (policy.Priority)
            {
                case TargetPriority.Nearest:
                    score = 1000f - distMe;
                    break;
                case TargetPriority.LowestHp:
                    score = (1f - hpRatio) * 500f - distMe * 0.5f;
                    break;
                case TargetPriority.ThreatNearPlayer:
                    score = 800f - distHero * 2f - distMe * 0.25f;
                    break;
                case TargetPriority.FocusOrNearPlayer:
                    score = 700f - distHero * 2f;
                    if (focus == e) score += 400f * policy.PreferFocusWeight;
                    break;
                case TargetPriority.FocusPrefer:
                    score = 600f - distMe;
                    if (focus == e) score += 500f * policy.PreferFocusWeight;
                    break;
                case TargetPriority.DenseCluster:
                    score = CountNearbyEnemies(e, ClusterRadius) * 120f - distMe;
                    if (focus == e) score += 200f * policy.PreferFocusWeight;
                    break;
            }

            if (focus == e && policy.Priority != TargetPriority.FocusPrefer
                && policy.Priority != TargetPriority.FocusOrNearPlayer)
                score += 80f * policy.PreferFocusWeight;

            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        return best;
    }

    static int CountNearbyEnemies(UnitBase center, float radius)
    {
        if (center == null || BattleManager.Instance?.monsters == null) return 0;
        float cx = UnitBase.GetCombatX(center);
        int n = 0;
        var list = BattleManager.Instance.monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.isDead) continue;
            if (Mathf.Abs(UnitBase.GetCombatX(e) - cx) <= radius) n++;
        }
        return n;
    }

    /// <summary>站位期望 X；null 表示不干预，走默认追敌。</summary>
    public static bool TryGetDesiredX(Mercenary merc, Policy policy, UnitBase target, out float desiredX)
    {
        desiredX = 0f;
        var hero = Hero.Instance;
        if (merc == null || hero == null) return false;
        float hx = UnitBase.GetCombatX(hero);
        float mx = UnitBase.GetCombatX(merc);

        if (policy.GuardPlayerWhenLowHp && hero.attr != null)
        {
            float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
            if (maxHp > 1f && hero.currentHp / maxHp < PlayerLowHpRatio)
            {
                desiredX = hx - 0.55f;
                return true;
            }
        }

        switch (policy.Stance)
        {
            case Stance.BetweenPlayerAndEnemy:
                if (target == null) return false;
                float tx = UnitBase.GetCombatX(target);
                desiredX = Mathf.Lerp(hx, tx, 0.42f);
                return true;
            case Stance.FrontOfPlayer:
                desiredX = hx + 1.1f + merc.GetPartyIndexOrZero() * 0.35f;
                return true;
            case Stance.KeepMidRange:
                if (target == null) return false;
                float tMid = UnitBase.GetCombatX(target);
                float face = tMid >= mx ? -1f : 1f;
                desiredX = tMid + face * MidRangeIdeal;
                return true;
            case Stance.KeepFar:
                if (target == null) return false;
                float tFar = UnitBase.GetCombatX(target);
                float f2 = tFar >= mx ? -1f : 1f;
                desiredX = tFar + f2 * FarRangeIdeal;
                return true;
            case Stance.Backline:
                desiredX = hx - 1.35f - merc.GetPartyIndexOrZero() * 0.25f;
                return true;
            default:
                return false;
        }
    }
}
