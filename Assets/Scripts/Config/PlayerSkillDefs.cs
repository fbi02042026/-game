using System;
using UnityEngine;

/// <summary>
/// 玩家可携带技能。元数据+战斗数优先读 <see cref="PlayerSkillTable"/>（CSV Cook）；
/// 缺表回退本类 Fallback（与 player_skills.csv 1:1，战斗数从现网 Ally SO 种子）。
/// 运行时伤害/治疗/Buff/AOE 不读 Ally SkillConfig。未知 id 返回 null。
/// </summary>
public static class PlayerSkillDefs
{
    /// <summary>城镇技能选择页（SkillSelectUI）只展示前 6 个基础技能；其余全部是局内抽卡池专用。</summary>
    public const int TownSelectCount = 6;

    public enum Kind
    {
        Heal,
        Shield,
        AtkBuff,
        AtkSpeedBuff,
        CritBuff,
        Aoe
    }

    /// <summary>技能放出的阶段（2026-09-15 分层解锁）。
    /// 前期 = 开局就能用；中期/后期靠章节、天赋、商店、成就逐步放出。
    /// 只用于 UI 分组与文档，真正判定看 <see cref="Def.unlockSource"/>。</summary>
    public enum UnlockTier
    {
        Early,  // 前期：开局解锁
        Mid,    // 中期
        Late    // 后期
    }

    /// <summary>技能解锁途径。
    /// None = 开局自带；Chapter = 通关指定章节自动解锁（动态判定，不写存档）；
    /// Talent / Shop / Achievement 解锁后写入 SaveData.unlockedSkills。</summary>
    public enum UnlockSource
    {
        None,
        Chapter,
        Talent,
        Shop,
        Achievement
    }

    [Serializable]
    public class Def
    {
        public string id;
        public string displayName;
        public Kind kind;
        public string desc;
        public string numbers;
        public float cooldown;
        public float duration;
        public string useHint;
        // ===== 解锁条件（2026-09-15 分层解锁）=====
        // 技能池按「前期 10 / 中期 8 / 后期 6」分批放出，避免开局 24 个一起糊脸。
        // unlockSource 决定走哪条途径；章节用 unlockChapter 动态判定，
        // 天赋/商店/成就解锁后写入 SaveData.unlockedSkills。
        public int unlockChapter; // 通关该章后解锁（SaveData 权威通关集合包含该章）；0=不按章节
        public UnlockSource unlockSource;
        public UnlockTier unlockTier;
        /// <summary>非章节途径的参数：天赋节点 key / 成就 id（商店不用，看价格）。</summary>
        public string unlockParam;
        /// <summary>商店途径：价格。0 = 不卖。</summary>
        public int unlockPrice;
        /// <summary>商店途径：货币（0=金币 1=天赋石 2=钻石）。</summary>
        public int unlockCurrency;
        public string allyConfigId;
        public Color tint;
        public SkillSystem.SkillType skillType;
        public AttackVfxKit attackKit;
        public float damageMultiplier;
        public float baseDamage;
        public float aoeRadius;
        public int projectileCount;
        public float projectileSpeed;
        public AttrType buffAttr;
        public float buffValue;
        public bool buffIsPercent;
        public float healBase;
        public float healPercentOfMax;
        /// <summary>治疗 = 目标最大生命 × healPercentOfMax + 施法者攻击 × healAtkMul。
        /// 给治疗一条随装备成长的线（与佣兵治疗 atk×mul 同模型），两者都吃星级乘数。</summary>
        public float healAtkMul;
        public float energyCost;
        public bool hasCombat;
    }

    /// <summary>缺表时的 1:1 种子；数值与 player_skills.csv / 设计文档一致。</summary>
    public static readonly Def[] Fallback =
    {
        new Def
        {
            id = "heal_spring",
            displayName = "治愈之泉",
            kind = Kind.Heal,
            desc = "给当前生命比例最低的我方单位回血（玩家或佣兵）。",
            numbers = "恢复目标 30% 最大生命",
            cooldown = 12f,
            duration = 0f,
            useHint = "血量危险时手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_heal",
            tint = new Color(0.35f, 0.75f, 0.4f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.Heal,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.MaxHp,
            healPercentOfMax = 0.3f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "holy_barrier",
            displayName = "圣盾壁垒",
            kind = Kind.Shield,
            desc = "获得护盾，持续期间免疫控制。",
            numbers = "获得 35% 最大生命的护盾，持续 5 秒",
            cooldown = 18f,
            duration = 5f,
            useHint = "精英/Boss 放大招前、或被包围时手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_shield",
            tint = new Color(0.35f, 0.55f, 0.9f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 4f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Defense,
            buffValue = 0.35f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "battle_surge",
            displayName = "战意爆发",
            kind = Kind.AtkBuff,
            desc = "短时间内大幅提升攻击。",
            numbers = "攻击 +30%，持续 8 秒",
            cooldown = 18f,
            duration = 8f,
            useHint = "精英/Boss 战或大量小怪时手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_atk_up",
            tint = new Color(0.9f, 0.45f, 0.25f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Attack,
            buffValue = 0.3f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "gale_stance",
            displayName = "疾风架势",
            kind = Kind.AtkSpeedBuff,
            desc = "进入疾风状态，攻速大幅提升。",
            numbers = "攻速 +35%，持续 6 秒",
            cooldown = 15f,
            duration = 6f,
            useHint = "输出窗口期手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_atk_speed",
            tint = new Color(0.4f, 0.7f, 0.95f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.AttackSpeed,
            buffValue = 0.35f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "deadly_focus",
            displayName = "致命专注",
            kind = Kind.CritBuff,
            desc = "集中精神，暴击率提升。",
            numbers = "暴击率 +25%，持续 8 秒",
            cooldown = 18f,
            duration = 8f,
            useHint = "Boss 战或精英怪出现时手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_crit_up",
            tint = new Color(0.95f, 0.55f, 0.25f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.CritRate,
            buffValue = 0.25f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "thunder_verdict",
            displayName = "天雷裁决",
            kind = Kind.Aoe,
            desc = "召唤天雷轰击目标区域。",
            numbers = "造成 300% 攻击的范围伤害",
            cooldown = 25f,
            duration = 0f,
            useHint = "怪群聚集或 Boss 虚弱时手动点击",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "ally_thunder",
            tint = new Color(0.65f, 0.4f, 0.9f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.None,
            damageMultiplier = 3f,
            aoeRadius = 8f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Attack,
            energyCost = 1f,
            hasCombat = true
        },

        // ================= 局内抽卡扩充池 =================
        // 说明：这些技能不进城镇技能选择页（SkillSelectUI 只认前 6 格），
        // 只在战斗内「升级三选一」出现。无 allyConfigId → VFX 走 attackKit 共用套，
        // 不依赖 Resources/VFX/Skills/Ally 下的专属预制体。
        new Def
        {
            id = "flame_burst",
            displayName = "烈焰爆裂",
            kind = Kind.Aoe,
            desc = "以自身为中心引爆烈焰，灼烧周围敌人。",
            numbers = "造成 260% 攻击的范围伤害",
            cooldown = 20f,
            duration = 0f,
            useHint = "怪群聚集时自动释放",
            unlockChapter = 3,
            unlockSource = UnlockSource.Shop,
            unlockTier = UnlockTier.Mid,
            unlockPrice = 8000,
            unlockCurrency = 0,
            allyConfigId = "",
            tint = new Color(0.98f, 0.45f, 0.24f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.Orb,
            damageMultiplier = 2.6f,
            aoeRadius = 7f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "thunder_chain",
            displayName = "连锁闪电",
            kind = Kind.Aoe,
            desc = "闪电在敌群中跳跃，逐个递减伤害。",
            numbers = "连锁 5 段，首段 160% 攻击",
            cooldown = 18f,
            duration = 0f,
            useHint = "敌人密集时收益最高",
            unlockChapter = 4,
            unlockSource = UnlockSource.Talent,
            unlockTier = UnlockTier.Mid,
            unlockParam = "talent_skill_thunder_chain",
            allyConfigId = "",
            tint = new Color(0.66f, 0.48f, 0.98f),
            skillType = SkillSystem.SkillType.Chain,
            attackKit = AttackVfxKit.Orb,
            damageMultiplier = 1.6f,
            aoeRadius = 8f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "wolf_volley",
            displayName = "狼群箭雨",
            kind = Kind.Aoe,
            desc = "召唤狼群齐射，同时命中多个敌人。",
            numbers = "对 3 个敌人各造成 120% 攻击",
            cooldown = 16f,
            duration = 0f,
            useHint = "分散敌人也能稳住输出",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "",
            tint = new Color(0.40f, 0.82f, 0.52f),
            skillType = SkillSystem.SkillType.Projectile,
            attackKit = AttackVfxKit.Bow,
            damageMultiplier = 1.2f,
            aoeRadius = 10f,
            projectileCount = 3,
            projectileSpeed = 14f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "war_banner",
            displayName = "战旗号令",
            kind = Kind.AtkBuff,
            desc = "竖起战旗，全队攻击大幅提升。",
            numbers = "全队攻击 +35%，持续 10 秒",
            cooldown = 24f,
            duration = 10f,
            useHint = "Boss 战开场立即释放",
            unlockChapter = 5,
            unlockSource = UnlockSource.Talent,
            unlockTier = UnlockTier.Mid,
            unlockParam = "talent_skill_war_banner",
            allyConfigId = "",
            tint = new Color(0.98f, 0.78f, 0.28f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Attack,
            buffValue = 0.35f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "iron_wall",
            displayName = "铁壁壁垒",
            kind = Kind.Shield,
            desc = "展开壁垒，大幅提升全队防御。",
            numbers = "全队防御 +45%，持续 8 秒",
            cooldown = 22f,
            duration = 8f,
            useHint = "被围或 Boss 蓄力时释放",
            unlockChapter = 2,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Mid,
            // V6：原来空着 → 走 attackKit 兜底套，玩家看不出这是护盾。
            // 复用圣盾壁垒那套 ally_shield（Resources/VFX/Skills/Ally/ally_shield.prefab 已存在），
            // 教程要让玩家「看见护盾放出来」，第二面护盾也必须一眼可辨。
            allyConfigId = "ally_shield",
            tint = new Color(0.42f, 0.72f, 0.94f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 5f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Defense,
            buffValue = 0.45f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "frost_nova",
            displayName = "霜华新星",
            kind = Kind.Aoe,
            desc = "冰霜自脚下炸开，覆盖极大范围。",
            numbers = "造成 220% 攻击的超大范围伤害",
            cooldown = 28f,
            duration = 0f,
            useHint = "清屏利器，冷却较长",
            unlockChapter = 8,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Late,
            allyConfigId = "",
            tint = new Color(0.55f, 0.85f, 1f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.Orb,
            damageMultiplier = 2.2f,
            aoeRadius = 9f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "blood_harvest",
            displayName = "血之收割",
            kind = Kind.Aoe,
            desc = "以血为刃横扫战场，伤害极高。",
            numbers = "造成 340% 攻击的范围伤害",
            cooldown = 30f,
            duration = 0f,
            useHint = "收割残血群",
            unlockChapter = 8,
            unlockSource = UnlockSource.Achievement,
            unlockTier = UnlockTier.Late,
            unlockParam = "kill_total_1000",
            allyConfigId = "",
            tint = new Color(0.85f, 0.22f, 0.30f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.MeleeSlash,
            damageMultiplier = 3.4f,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },

        // ============ 2026-09-15 第二轮扩充（13 → 24）============
        // 背景：技能从能量槽改成纯冷却制后，4 个技槽会持续轮转，13 个技能的重复度太高。
        // 补齐方向：① 牧师只有 1 个技能 → 补治疗线；② 重甲只有 1 个 → 补近战线；
        // ③ 召唤流只有 1 个 → 补到 3 个；④ 守护流补短 CD 与长 CD 两档，覆盖不同节奏。
        new Def
        {
            id = "swift_mend",
            displayName = "迅愈术",
            kind = Kind.Heal,
            desc = "快速缝合伤口，抬住血量最低的队友。",
            numbers = "恢复目标 22% 最大生命 + 1.8 倍攻击",
            cooldown = 9f,
            duration = 0f,
            useHint = "冷却短，适合持续续航",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "",
            tint = new Color(0.45f, 0.85f, 0.55f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.Heal,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.MaxHp,
            healPercentOfMax = 0.22f,
            healAtkMul = 1.8f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "sacred_revival",
            displayName = "圣愈术",
            kind = Kind.Heal,
            desc = "以圣光重塑血肉，瞬间拉回濒死的队友。",
            numbers = "恢复目标 48% 最大生命 + 4.0 倍攻击",
            cooldown = 22f,
            duration = 0f,
            useHint = "救命大治疗，冷却较长",
            unlockChapter = 5,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Late,
            allyConfigId = "",
            tint = new Color(0.55f, 0.95f, 0.7f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.Heal,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.MaxHp,
            healPercentOfMax = 0.48f,
            healAtkMul = 4.0f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "stone_skin",
            displayName = "石肤术",
            kind = Kind.Shield,
            desc = "皮肤石化，全队防御小幅提升。",
            numbers = "全队防御 +30%，持续 10 秒",
            cooldown = 16f,
            duration = 10f,
            useHint = "短 CD 常驻减伤",
            unlockChapter = 2,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Mid,
            allyConfigId = "",
            tint = new Color(0.62f, 0.58f, 0.50f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Defense,
            buffValue = 0.30f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "aegis_oath",
            displayName = "守护誓约",
            kind = Kind.Shield,
            desc = "立下誓约，全队防御大幅提升并持续更久。",
            numbers = "全队防御 +55%，持续 12 秒",
            cooldown = 26f,
            duration = 12f,
            useHint = "Boss 大招前的减伤窗口",
            unlockChapter = 8,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Late,
            allyConfigId = "",
            tint = new Color(0.95f, 0.85f, 0.40f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Defense,
            buffValue = 0.55f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "bull_rush",
            displayName = "蛮牛冲锋",
            kind = Kind.Aoe,
            desc = "低头猛冲，撞飞前方一片敌人。",
            numbers = "造成 200% 攻击的范围伤害",
            cooldown = 14f,
            duration = 0f,
            useHint = "短 CD 清杂兵",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "",
            tint = new Color(0.80f, 0.55f, 0.28f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.MeleeSlash,
            damageMultiplier = 2.0f,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "quake_slam",
            displayName = "震地重击",
            kind = Kind.Aoe,
            desc = "砸裂地面，冲击波横扫大片敌人。",
            numbers = "造成 300% 攻击的范围伤害",
            cooldown = 21f,
            duration = 0f,
            useHint = "中 CD 高伤，范围较大",
            unlockChapter = 3,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Mid,
            allyConfigId = "",
            tint = new Color(0.70f, 0.45f, 0.22f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.MeleeSlash,
            damageMultiplier = 3.0f,
            aoeRadius = 8f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "hawk_eye",
            displayName = "鹰眼",
            kind = Kind.CritBuff,
            desc = "锁定要害，全队暴击率提升。",
            numbers = "暴击率 +35%，持续 10 秒",
            cooldown = 20f,
            duration = 10f,
            useHint = "爆发窗口开暴击",
            unlockChapter = 4,
            unlockSource = UnlockSource.Shop,
            unlockTier = UnlockTier.Mid,
            unlockPrice = 3000,
            unlockCurrency = 0,
            allyConfigId = "",
            tint = new Color(1f, 0.70f, 0.30f),
            skillType = SkillSystem.SkillType.Buff,
            attackKit = AttackVfxKit.None,
            aoeRadius = 6f,
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.CritRate,
            buffValue = 0.35f,
            buffIsPercent = true,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "arrow_storm",
            displayName = "箭雨风暴",
            kind = Kind.Aoe,
            desc = "万箭齐落，覆盖全场敌人。",
            numbers = "对 5 个敌人各造成 90% 攻击",
            cooldown = 22f,
            duration = 0f,
            useHint = "敌人越多收益越高",
            unlockChapter = 5,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Late,
            allyConfigId = "",
            tint = new Color(0.75f, 0.90f, 0.45f),
            skillType = SkillSystem.SkillType.Projectile,
            attackKit = AttackVfxKit.Bow,
            damageMultiplier = 0.9f,
            aoeRadius = 10f,
            projectileCount = 5,
            projectileSpeed = 14f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "spirit_wolf",
            displayName = "灵狼突袭",
            kind = Kind.Aoe,
            desc = "唤出两头灵狼扑向敌群。",
            numbers = "对 3 个敌人各造成 130% 攻击",
            cooldown = 18f,
            duration = 0f,
            useHint = "召唤流核心，中距离多点爆发",
            unlockChapter = 3,
            unlockSource = UnlockSource.Chapter,
            unlockTier = UnlockTier.Mid,
            allyConfigId = "",
            tint = new Color(0.50f, 0.90f, 0.65f),
            skillType = SkillSystem.SkillType.Projectile,
            attackKit = AttackVfxKit.Bow,
            damageMultiplier = 1.3f,
            aoeRadius = 10f,
            projectileCount = 3,
            projectileSpeed = 15f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "blade_storm",
            displayName = "剑刃风暴",
            kind = Kind.Aoe,
            desc = "旋身斩击，剑气绞碎周身敌人。",
            numbers = "造成 235% 攻击的范围伤害",
            cooldown = 18f,
            duration = 0f,
            useHint = "被围时最有效",
            unlockChapter = 0,
            unlockSource = UnlockSource.None,
            unlockTier = UnlockTier.Early,
            allyConfigId = "",
            tint = new Color(0.85f, 0.88f, 0.95f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.MeleeSlash,
            damageMultiplier = 2.35f,
            aoeRadius = 7f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        },
        new Def
        {
            id = "arcane_flame",
            displayName = "秘法烈焰",
            kind = Kind.Aoe,
            desc = "引燃秘法之火，焚烧大片区域。",
            numbers = "造成 310% 攻击的范围伤害",
            cooldown = 26f,
            duration = 0f,
            useHint = "火系高伤，配合灼烧流派",
            unlockChapter = 8,
            unlockSource = UnlockSource.Achievement,
            unlockTier = UnlockTier.Late,
            unlockParam = "equip_collect_50",
            allyConfigId = "",
            tint = new Color(1f, 0.50f, 0.20f),
            skillType = SkillSystem.SkillType.AOE,
            attackKit = AttackVfxKit.Orb,
            damageMultiplier = 3.1f,
            aoeRadius = 8f,
            projectileCount = 1,
            projectileSpeed = 12f,
            energyCost = 1f,
            hasCombat = true
        }
    };

    static Def[] _all;
    static bool _loaded;

    public static Def[] All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _all = null;
        PlayerSkillTable.Reload();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded && _all != null) return;
        _loaded = true;
        _all = CloneFallback();

        if (!PlayerSkillTable.HasData) return;

        var rows = PlayerSkillTable.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int idx = IndexOfLoaded(row.Id);
            if (idx >= 0)
                Overlay(_all[idx], row);
            else
            {
                var extra = new Def { tint = Color.white };
                Overlay(extra, row);
                Append(extra);
            }
        }
    }

    static Def[] CloneFallback()
    {
        var copy = new Def[Fallback.Length];
        for (int i = 0; i < Fallback.Length; i++)
            copy[i] = CloneDef(Fallback[i]);
        return copy;
    }

    static Def CloneDef(Def src)
    {
        return new Def
        {
            id = src.id,
            displayName = src.displayName,
            kind = src.kind,
            desc = src.desc,
            numbers = src.numbers,
            cooldown = src.cooldown,
            duration = src.duration,
            useHint = src.useHint,
            unlockChapter = src.unlockChapter,
            unlockSource = src.unlockSource,
            unlockTier = src.unlockTier,
            unlockParam = src.unlockParam,
            unlockPrice = src.unlockPrice,
            unlockCurrency = src.unlockCurrency,
            allyConfigId = src.allyConfigId,
            tint = src.tint,
            skillType = src.skillType,
            attackKit = src.attackKit,
            damageMultiplier = src.damageMultiplier,
            baseDamage = src.baseDamage,
            aoeRadius = src.aoeRadius,
            projectileCount = src.projectileCount,
            projectileSpeed = src.projectileSpeed,
            buffAttr = src.buffAttr,
            buffValue = src.buffValue,
            buffIsPercent = src.buffIsPercent,
            healBase = src.healBase,
            healPercentOfMax = src.healPercentOfMax,
            healAtkMul = src.healAtkMul,
            energyCost = src.energyCost,
            hasCombat = src.hasCombat
        };
    }

    static void Overlay(Def dest, PlayerSkillTable.Row row)
    {
        dest.id = row.Id;
        dest.displayName = row.DisplayName;
        dest.kind = row.Kind;
        dest.desc = row.Desc;
        dest.numbers = row.Numbers;
        dest.cooldown = row.Cooldown;
        dest.duration = row.Duration;
        dest.useHint = row.UseHint;
        dest.unlockChapter = row.UnlockChapter;
        dest.allyConfigId = row.AllyConfigId;
        if (row.HasCombat)
        {
            dest.skillType = row.SkillType;
            dest.attackKit = row.AttackKit;
            dest.damageMultiplier = row.DamageMultiplier;
            dest.baseDamage = row.BaseDamage;
            dest.aoeRadius = row.AoeRadius;
            dest.projectileCount = row.ProjectileCount;
            dest.projectileSpeed = row.ProjectileSpeed;
            dest.buffAttr = row.BuffAttr;
            dest.buffValue = row.BuffValue;
            dest.buffIsPercent = row.BuffIsPercent;
            dest.healBase = row.HealBase;
            dest.healPercentOfMax = row.HealPercentOfMax;
            dest.healAtkMul = row.HealAtkMul;
            dest.energyCost = row.EnergyCost;
            dest.hasCombat = true;
        }
    }

    public static Def GetByAllyConfigId(string allyId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(allyId)) return null;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].allyConfigId == allyId) return _all[i];
        return null;
    }

    static void Append(Def def)
    {
        var next = new Def[_all.Length + 1];
        Array.Copy(_all, next, _all.Length);
        next[_all.Length] = def;
        _all = next;
    }

    static int IndexOfLoaded(string id)
    {
        if (_all == null || string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return i;
        return -1;
    }

    public static Def Get(int index)
    {
        EnsureLoaded();
        if (index < 0 || index >= _all.Length) return null;
        return _all[index];
    }

    /// <summary>未知 id 返回 null（不再静默回退 All[0]）。</summary>
    public static Def GetById(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return _all[i];
        return null;
    }

    /// <summary>未找到返回 -1。</summary>
    public static int IndexOf(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return i;
        return -1;
    }

    public static bool IsUnlocked(Def def, SaveData data)
    {
        if (def == null) return false;
        // 1. 已通过天赋 / 商店 / 成就解锁过（写进存档，优先级最高）
        if (data != null && data.unlockedSkills != null && data.unlockedSkills.Contains(def.id)) return true;
        // 2. 开局自带
        if (def.unlockSource == UnlockSource.None && def.unlockChapter <= 0) return true;
        // 3. 章节解锁：动态判定，不写存档
        if (def.unlockChapter > 0)
        {
            if (def.id == "holy_barrier" && data != null && data.chapter1ChoiceDone) return true;
            if (data != null && data.clearedChapterIds != null && data.clearedChapterIds.Contains(def.unlockChapter))
                return true;
            int chapter = data != null ? data.maxUnlockedChapter : 1;
            return chapter > def.unlockChapter;
        }
        // 4. 天赋 / 商店 / 成就：尚未写进存档 = 未解锁
        return false;
    }

    /// <summary>写入解锁记录（天赋节点、成就奖励调用）。章节解锁不要走这里。</summary>
    public static bool Unlock(string skillId, SaveData data)
    {
        if (string.IsNullOrEmpty(skillId) || data == null) return false;
        data.unlockedSkills ??= new System.Collections.Generic.HashSet<string>();
        if (data.unlockedSkills.Contains(skillId)) return false;
        data.unlockedSkills.Add(skillId);
        return true;
    }

    /// <summary>成就完成时调用：解锁所有以该成就为解锁条件的技能。返回解锁个数。</summary>
    public static int UnlockSkillsByAchievement(string achievementId, SaveData data)
    {
        if (string.IsNullOrEmpty(achievementId) || data == null) return 0;
        var all = All;
        if (all == null) return 0;
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            if (def == null || def.unlockSource != UnlockSource.Achievement) continue;
            if (string.IsNullOrEmpty(def.unlockParam) || def.unlockParam != achievementId) continue;
            if (Unlock(def.id, data)) n++;
        }
        return n;
    }

    public static string FormatDetail(Def def)
    {
        if (def == null) return "";
        return $"{def.numbers}。冷却 {def.cooldown:0} 秒。{def.useHint}。";
    }

    public static string FormatUnlockHint(Def def)
    {
        if (def == null) return "未解锁";
        switch (def.unlockSource)
        {
            case UnlockSource.Chapter:
                return def.unlockChapter > 0 ? $"未解锁：通关第{def.unlockChapter}章后解锁" : "未解锁";
            case UnlockSource.Talent:
                return "未解锁：在天赋树中解锁";
            case UnlockSource.Shop:
                return $"未解锁：可在技能页花 {def.unlockPrice} {CurrencyName(def.unlockCurrency)} 解锁";
            case UnlockSource.Achievement:
                return "未解锁：完成对应成就后解锁";
            default:
                return def.unlockChapter > 0 ? $"未解锁：通关第{def.unlockChapter}章后解锁" : "未解锁";
        }
    }

    public static string CurrencyName(int currency)
    {
        switch (currency)
        {
            case 1: return "天赋石";
            case 2: return "钻石";
            default: return "金币";
        }
    }
}
