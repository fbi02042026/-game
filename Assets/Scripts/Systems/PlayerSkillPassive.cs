using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家主动技改为被动：自动释放，禁止点头像手动放。
/// 雷击奥义仍走 HeroThunderUltimate 满条自动。
/// <para>
/// 2026-09-15：充能槽取消，改<b>纯冷却制</b> —— 开局由 BattleManager 按槽位错峰给初始 CD，
/// 冷却好了按槽序放一个，放完重新进入自己的冷却；任意两次释放之间还要隔 GameConfig.PLAYER_SKILL_GCD 秒，
/// 避免多个技能同时冷却完毕时在同一瞬间全部炸出来。
/// </para>
/// <para>
/// 2026-09-17：冷却好了≠该放，还要等<b>战场状态触发条件</b>。例：护盾/加血在血量降到 80% 时才开；
/// 箭雨类 AOE 在身边 >=3 个怪时才砸（避免一上来就空放）。见 <see cref="IsTriggerMet"/>。
/// </para>
/// </summary>
public static class PlayerSkillPassive
{
    /// <summary>永久 false：战斗内不可点放主动技。</summary>
    public const bool AllowManualCast = false;

    /// <summary>玩家主动技触发条件类型。</summary>
    public enum TriggerKind
    {
        Always,                 // 冷却好就放（默认；未映射的技能走这个）
        HpBelow,               // 自身血量比例 <= hpRatio 才放
        NearbyMonstersAtLeast   // 自身半径内怪物 >= monsterCount 才放
    }

    /// <summary>单个技能的触发条件。</summary>
    public struct Trigger
    {
        public TriggerKind kind;
        public float hpRatio;       // HpBelow 用：血量比例阈值
        public int monsterCount;    // NearbyMonstersAtLeast 用：怪物数量
        public float radius;        // NearbyMonstersAtLeast 用：半径（世界单位）

        public bool IsSatisfied()
        {
            switch (kind)
            {
                case TriggerKind.Always:
                    return true;
                case TriggerKind.HpBelow:
                {
                    var hero = Hero.Instance;
                    if (hero == null || hero.attr == null) return false;
                    float max = hero.attr.GetAttr(AttrType.MaxHp);
                    if (max <= 0f) return false;
                    return hero.currentHp / max <= hpRatio;
                }
                case TriggerKind.NearbyMonstersAtLeast:
                    return CountNearbyMonsters(radius) >= monsterCount;
                default:
                    return true;
            }
        }
    }

    /// <summary>身边「近」的半径（世界单位），与 AOE 半径（8）同一量级。</summary>
    const float NearbyRadius = 9f;

    /// <summary>
    /// 各玩家技能触发条件（按 skillId，与 player_skills.csv 的 useHint 对应）：
    ///  holy_barrier    圣盾壁垒(护盾)   → 血 <= 80% 开
    ///  heal_spring     治愈之泉(加血)   → 血 <= 80% 开
    ///  thunder_verdict 天雷裁决(AOE/箭雨类) → 身边 >= 3 怪 开
    ///  battle_surge    战意爆发(攻buff)  → 身边 >= 2 怪 开
    ///  deadly_focus    致命专注(暴击buff) → 身边 >= 1 怪 开
    ///  gale_stance     疾风架势(攻速buff) → 身边 >= 1 怪 开
    /// 未在表里的技能（如装备附带技）走 Always，保持旧行为。
    /// </summary>
    static readonly Dictionary<string, Trigger> Triggers = new Dictionary<string, Trigger>
    {
        ["holy_barrier"]    = new Trigger { kind = TriggerKind.HpBelow,               hpRatio = 0.8f },
        ["heal_spring"]     = new Trigger { kind = TriggerKind.HpBelow,               hpRatio = 0.8f },
        ["thunder_verdict"] = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 3, radius = NearbyRadius },
        ["battle_surge"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 2, radius = NearbyRadius },
        ["deadly_focus"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        ["gale_stance"]     = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
    };

    /// <summary>供 BattleManager.FindReadySkillSlot 调用：该技能此刻是否满足触发条件。</summary>
    public static bool IsTriggerMet(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return true;
        if (!Triggers.TryGetValue(skillId, out var t)) return true; // 未映射 → 默认放
        return t.IsSatisfied();
    }

    static int CountNearbyMonsters(float radius)
    {
        var bm = BattleManager.Instance;
        var hero = Hero.Instance;
        if (bm == null || hero == null || bm.monsters == null) return 0;
        int n = 0;
        Vector2 self = hero.transform.position;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (Vector2.Distance(self, m.transform.position) <= radius) n++;
        }
        return n;
    }

    public static void TickAutoCast()
    {
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        // 全局间隔没到：直接返回，省掉每帧的 FindReadySkillSlot 遍历（早退闸）
        if (!bm.IsPlayerSkillGcdReady) return;
        // 槽序即释放优先级：多个技能同时就绪时取最靠前的那个（且已满足触发条件）。
        // 本方法只负责触发，真正的释放、上冷却与上膛在 SkillCastService.TryUsePlayerSkillSlot。
        if (bm.FindReadySkillSlot() < 0) return;
        bm.TryUsePlayerSkill();
    }
}
