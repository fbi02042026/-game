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
/// 其余技能在身边范围内有怪时就放（2026-11-08 主人拍板，不再数怪的数量）；2026-11-10 起该怪还必须在战斗镜头内。见 <see cref="IsTriggerMet"/>。
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
        AnyAllyHpBelow,        // 我方任意单位（玩家或佣兵）血量比例 <= hpRatio 才放
        NearbyMonstersAtLeast   // 自身半径内、且在战斗镜头内的怪才放（2026-11-08 起 monsterCount 统一 = 1）
    }

    /// <summary>单个技能的触发条件。</summary>
    public struct Trigger
    {
        public TriggerKind kind;
        public float hpRatio;       // HpBelow 用：血量比例阈值
        public int monsterCount;    // NearbyMonstersAtLeast 用：怪物数量（2026-11-08 起统一 = 1）
        public float radius;        // NearbyMonstersAtLeast 用：半径（世界单位）

        public bool IsSatisfied()
        {
            switch (kind)
            {
                case TriggerKind.Always:
                    return true;
                case TriggerKind.HpBelow:
                    return IsUnitHpBelow(Hero.Instance, hpRatio);
                case TriggerKind.AnyAllyHpBelow:
                {
                    // 2026-10-20 主人拍板「治愈之泉触发改成全队血线」：玩家报「回复技能不能给佣兵用」——
                    // 旧触发只读玩家血（Hero.Instance），佣兵掉血、玩家满血时技能永不自动释放。
                    // 现口径：玩家或任一在场佣兵 ≤ hpRatio 都触发；治疗目标仍由
                    // SkillCastService.FindPreferredHealTarget 选血量比例最低者（本就含佣兵）。
                    if (IsUnitHpBelow(Hero.Instance, hpRatio)) return true;
                    var mercs = MercenaryManager.Instance != null
                        ? MercenaryManager.Instance.GetActiveMercs() : null;
                    if (mercs == null) return false;
                    for (int i = 0; i < mercs.Count; i++)
                        if (IsUnitHpBelow(mercs[i], hpRatio)) return true;
                    return false;
                }
                case TriggerKind.NearbyMonstersAtLeast:
                    // 2026-11-10 主人拍板「只有看到怪才打 如果没看到怪即使怪在范围内也不打 也就是说怪必须在镜头里」：
                    // 触发与伤害链同尺 —— 半径内且 GameConfig.IsInCombatViewport 为真才算数。
                    // 旧 OR（HasEliteOrBossOnField）不做视口过滤，会绕过本规则；且其判定已被
                    // CountNearbyMonsters>=1 完全覆盖（精英/Boss 也在 monsters 列表里）→ 整段删除，不留第二出口。
                    return CountNearbyMonsters(radius) >= monsterCount;
                default:
                    return true;
            }
        }
    }

    /// <summary>单个单位的血量比例是否 ≤ ratio（死亡 / 无属性 / 无上限 → false）。</summary>
    static bool IsUnitHpBelow(UnitBase u, float ratio)
    {
        if (u == null || u.isDead || u.attr == null) return false;
        float max = u.attr.GetAttr(AttrType.MaxHp);
        if (max <= 0f) return false;
        return u.currentHp / max <= ratio;
    }

    /// <summary>身边「近」的半径（世界单位），与 AOE 半径（8）同一量级。</summary>
    const float NearbyRadius = 9f;

    /// <summary>
    /// 风龙卷专属触发半径（2026-11-08 主人拍板「范围太远，怪还没进镜头就触发技能」）：
    /// 共用 NearbyRadius=9 是镜头半屏宽(≈3.04)的 3 倍，且当时 CountNearbyMonsters 不做视口过滤（11-10 已统一补上），
    /// 怪还在屏外就满足触发。单独收到 3 —— 怪进镜头才放；其余技能仍用 NearbyRadius，不受影响。
    /// 2026-11-08 后续拍板：怪数阈值统一 1，半径 3 保持不变。
    /// </summary>
    const float WhirlwindTriggerRadius = 3f;

    /// <summary>
    /// 星落专属触发半径（2026-11-08 主人「星落也是同理」）：与风龙卷同口径，收到镜头半屏宽 ≈3.04 内，
    /// 怪进镜头才放。2026-11-08 后续拍板改为「范围内有怪就放」，半径 3 保持不变；弹窗文案见 TriggerText。
    /// </summary>
    const float StarfallTriggerRadius = 3f;

    /// <summary>
    /// 重斩专属触发半径（2026-11-10 主人反馈「还没看到怪就释放了，这个应该是近距离才释放的」）：
    /// 原先共用 NearbyRadius=9，是镜头半屏宽(≈3.04)的 3 倍，且当时 CountNearbyMonsters 不做视口过滤（11-10 已统一补上），
    /// 怪还在屏外就满足触发；更关键的是重斩 aoeRadius 只有 4.5，触发半径 9 > 伤害半径 4.5，
    /// 远处那只怪根本劈不到，白进 25 秒 CD。收到 3 与风龙卷/星落同口径：怪进镜头才放，
    /// 且 3 &lt; 4.5 保证触发时 AOE 覆盖得到目标。其余技能半径不动。
    /// </summary>
    const float HeavyCleaveTriggerRadius = 3f;

    /// <summary>
    /// 各玩家技能触发条件（按 skillId，与 player_skills.csv 的 useHint 对应）。
    /// 血线两技（治疗/护盾）不参与本次改动，保持原口径：
    ///  holy_barrier    圣盾壁垒(护盾)   → 自身血 <= 80% 开
    ///  heal_spring     治愈之泉(加血)   → 全队(玩家或佣兵)血 ≤ 80% 开（2026-10-20 主人拍板）
    /// 其余 7 技统一 = 身边范围内有怪就放（含精英/Boss 进入半径）：
    ///  thunder_verdict / battle_surge / deadly_focus / gale_stance /
    ///  heavy_cleave / whirlwind / starfall
    /// 未在表里的技能（如装备附带技）走 Always，保持旧行为。
    /// 🔴 2026-11-08 主人拍板「不用写身边有几个释放，机制全部改成身边有怪在范围内释放」：
    ///    NearbyMonstersAtLeast 的 monsterCount 一律 = 1，不再数怪的数量；
    ///    半径按各技能原值（NearbyRadius=9；重斩/风龙卷/星落=3，怪进镜头才放 —— 2026-11-08 口径，重斩 11-10 补齐）。
    ///    血线两技保持血线，改成「有怪就放」会满血空放治疗/护盾，且推翻 10-20 拍板。
    /// 2026-11-10：原 2026-10-21 加的「精英/Boss 进入半径即满足」OR 整段删除 ——
    ///    它不做视口过滤，会绕过「怪必须在镜头里」；且其判定已被 CountNearbyMonsters>=1 完全覆盖。
    ///    现在只有一条判定：半径内 + 战斗镜头内。Boss 远处刷出不触发，逼近且进镜头才放。
    /// 玩家可见文案在 <see cref="TriggerText"/>（技能弹窗里展示的就是它，改表记得同步语义）。
    /// </summary>
    static readonly Dictionary<string, Trigger> Triggers = new Dictionary<string, Trigger>
    {
        ["holy_barrier"]    = new Trigger { kind = TriggerKind.HpBelow,               hpRatio = 0.8f },
        ["heal_spring"]     = new Trigger { kind = TriggerKind.AnyAllyHpBelow,          hpRatio = 0.8f },
        ["thunder_verdict"] = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        ["battle_surge"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        ["deadly_focus"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        ["gale_stance"]     = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        // 2026-11-05 新技；2026-11-08 起统一「范围内有怪就放」，半径仍各自保留
        // 2026-11-10：半径 9 → 3，修「怪还没看见就空放」（见 HeavyCleaveTriggerRadius）
        ["heavy_cleave"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = HeavyCleaveTriggerRadius },
        ["whirlwind"]       = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = WhirlwindTriggerRadius },
        ["starfall"]        = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = StarfallTriggerRadius },
    };

    /// <summary>
    /// 触发条件的中文文案（2026-10-21 主人拍板：释放条件要写进技能弹窗信息里）。
    /// 血线档的数值仍从 <see cref="Triggers"/> 表取；NearbyMonstersAtLeast 档按 2026-11-08 拍板固定为
    /// 「身边范围内有敌人（含精英/Boss）时自动释放」，不再随表里的怪数 / 半径变。
    /// 未登记触发条件的技能返回 null（调用方回退 useHint）。
    /// </summary>
    public static string TriggerText(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        // 2026-10-27 主人拍板：疾风架势改「手动窗口技」，弹窗文案单独走窗口语义（不再套「自动释放」句式）。
        // 传入可能是运行时别名（ally_atk_speed），先解析真 id 再比对。
        string resolvedId = PlayerSkillDefs.ResolveRealSkillId(skillId);
        if (resolvedId == "gale_stance")
            return $"就绪后弹 {GameConfig.GALE_WINDOW_DURATION:0} 秒点击窗口：点屏幕多打一次（威力 {Mathf.RoundToInt(GameConfig.GALE_EXTRA_DMG_MULT * 100f)}%）；不点则只上攻速";
        if (!Triggers.TryGetValue(resolvedId, out var t)) return null;
        switch (t.kind)
        {
            case TriggerKind.HpBelow:
                return $"自身血量 ≤{Mathf.RoundToInt(t.hpRatio * 100f)}% 时自动释放";
            case TriggerKind.AnyAllyHpBelow:
                return $"任意队友血量 ≤{Mathf.RoundToInt(t.hpRatio * 100f)}% 时自动释放";
            case TriggerKind.NearbyMonstersAtLeast:
                // 2026-11-08 主人拍板「不用写身边有几个释放」：不再输出数量 / 半径数字，
                // 统一写「身边范围内有怪就放」；精英/Boss 仍在句尾注明（它们也在 monsters 列表里，同样走镜头判定）。
                // 旧的数字模板整段删除，防止以后调 monsterCount 又把数字文案带回来。
                return "身边范围内有敌人（含精英/Boss）时自动释放";
            default:
                return null;
        }
    }

    /// <summary>已报过「未登记触发条件」的 id：本方法每帧都会被调用，同一个 id 只报一次，别刷屏。</summary>
    static readonly HashSet<string> _missingTriggerLogged = new HashSet<string>();

    /// <summary>供 BattleManager.FindReadySkillSlot 调用：该技能此刻是否满足触发条件。</summary>
    public static bool IsTriggerMet(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return true;
        // 2026-10-10：调用方（BattleManager.FindReadySkillSlot）传进来的是运行时 id，
        // 即 PlayerSkillTable.BuildRuntimeConfig 写进去的 ally_* 别名；Triggers 以玩家真 id 为键。
        // 不解析的话 6 个基础技能全部走「未映射 → 默认放」，血线 / 怪群条件形同虚设。
        string realId = PlayerSkillDefs.ResolveRealSkillId(skillId);
        if (!Triggers.TryGetValue(realId, out var t))
        {
            // 未映射 → 默认放（旧行为）。真 id 查不到说明这技能压根没登记触发条件，打 Error 让人补表，
            // 不再静默放行到没人发现。
            if (_missingTriggerLogged.Add(realId))
                Debug.LogError($"[PlayerSkillPassive] 技能 id 未登记触发条件 → 按 Always 放行（runtimeId={skillId}, realId={realId}）");
            return true;
        }
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
            // 2026-11-10 主人拍板「只有看到怪才打 如果没看到怪即使怪在范围内也不打 也就是说怪必须在镜头里」：
            // 在半径条件上**叠加**战斗镜头判定，与伤害链 GetEnemiesInRangeAt(SkillSystem.cs:326)、
            // MoveAoeExecutor.Scan(:159) 共用 GameConfig.IsInCombatViewport 同一把尺，屏外怪一律不计数。
            // 不是回退 2026-10-21 去掉的「全屏检测」：半径仍在，视口只是附加条件（半径外、哪怕屏内也不触发）。
            if (!GameConfig.IsInCombatViewport(m)) continue;
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
