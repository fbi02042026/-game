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
        AnyAllyHpBelow,        // 我方任意单位（玩家或佣兵）血量比例 <= hpRatio 才放
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
                    // 2026-10-21（当日报，回归修复）：精英/Boss 进入半径即视为满足 —— 单 Boss / 单精英战只有 1~2 只怪，
                    // battle_surge(≥2) / thunder_verdict(≥3) 都不卡死。但 OR 必须「进入半径」（与 CountNearbyMonsters 同口径），
                    // 否则 Boss 刚在远处刷出就触发 → 「一进图就空砸」。Boss 逼近到身边才放。
                    if (CountNearbyMonsters(radius) >= monsterCount) return true;
                    return HasEliteOrBossOnField(radius);
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
    /// 各玩家技能触发条件（按 skillId，与 player_skills.csv 的 useHint 对应）：
    ///  holy_barrier    圣盾壁垒(护盾)   → 血 <= 80% 开
    ///  heal_spring     治愈之泉(加血)   → 全队(玩家或佣兵)血 ≤ 80% 开（2026-10-20 主人拍板）
    ///  thunder_verdict 天雷裁决(AOE/箭雨类) → 身边 >= 3 怪，或 Boss/精英进入半径 开
    ///  battle_surge    战意爆发(攻buff)  → 身边 >= 2 怪，或 Boss/精英进入半径 开
    ///  deadly_focus    致命专注(暴击buff) → 身边 >= 1 怪，或 Boss/精英进入半径 开
    ///  gale_stance     疾风架势(攻速buff) → 身边 >= 1 怪，或 Boss/精英进入半径 开
    /// 未在表里的技能（如装备附带技）走 Always，保持旧行为。
    /// 2026-10-21（当日报，回归修复）：怪群阈值外加一条 OR —— **场上精英/Boss 进入半径即视为满足**，
    /// 单 Boss / 单精英战只有 1~2 只怪也不卡死（battle_surge 的「≥2 怪」、thunder_verdict 的「≥3 怪」最该开的窗口）。
    /// ★ OR 必须「进入半径」才满足（与 CountNearbyMonsters 同口径）：Boss 刚在远处刷出、还没接战不触发，
    ///   逼近到身边才放 —— 否则一进 Boss 图就直接空砸，正是「进图就放、不看怪数」的回归。
    /// 玩家可见文案在 <see cref="TriggerText"/>（技能弹窗里展示的就是它，改表记得同步语义）。
    /// </summary>
    static readonly Dictionary<string, Trigger> Triggers = new Dictionary<string, Trigger>
    {
        ["holy_barrier"]    = new Trigger { kind = TriggerKind.HpBelow,               hpRatio = 0.8f },
        ["heal_spring"]     = new Trigger { kind = TriggerKind.AnyAllyHpBelow,          hpRatio = 0.8f },
        ["thunder_verdict"] = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 3, radius = NearbyRadius },
        ["battle_surge"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 2, radius = NearbyRadius },
        ["deadly_focus"]    = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
        ["gale_stance"]     = new Trigger { kind = TriggerKind.NearbyMonstersAtLeast, monsterCount = 1, radius = NearbyRadius },
    };

    /// <summary>
    /// 场上是否存在精英或 Boss，且**进入半径**（与 CountNearbyMonsters 同口径）：
    /// Boss/精英是「重要目标」，但其刚在远处刷出、还没接战时不应触发技能 → 必须逼近到身边（距离 ≤ radius）才算。
    /// 不做视口过滤（Boss 体型大，屏幕边缘也算战斗目标）。
    /// </summary>
    static bool HasEliteOrBossOnField(float radius)
    {
        var bm = BattleManager.Instance;
        var hero = Hero.Instance;
        if (bm == null || hero == null || bm.monsters == null) return false;
        Vector2 self = hero.transform.position;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var u = bm.monsters[i];
            if (u == null || u.isDead) continue;
            // monsters 是 List<UnitBase>，精英/Boss 标记在 Monster 上（同 BattleManager.MonsterDeath.cs:13 口径）
            if (!(u is Monster m)) continue;
            if ((m.IsEliteWave || m.IsBossUnit) && Vector2.Distance(self, m.transform.position) <= radius) return true;
        }
        return false;
    }

    /// <summary>
    /// 触发条件的中文精确文案（2026-10-21 主人拍板：释放条件要写进技能弹窗信息里）。
    /// 唯一真源 = 上面的 <see cref="Triggers"/> 表 —— UI 不许自己另写一套，改表即改文案。
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
                // 与 IsSatisfied 对齐：精英/Boss 进入半径即满足（单 Boss/精英战这个最该开的窗口），文案必须带上。
                return t.monsterCount <= 1
                    ? "身边有敌人（含精英/Boss）时自动释放"
                    : $"身边 {t.radius:0} 内有 {t.monsterCount} 名敌人（含精英/Boss）时自动释放";
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
            // 2026-10-21（当日报）：改为纯自身半径检测，去掉视口（全屏）过滤 —— 只在主角半径内的怪才算数，
            // 不管怪在屏幕上/外、不管是不是场上远处目标；半径外的怪一律不触发（主人拍板「以自身范围，不要全屏检测」）。
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
