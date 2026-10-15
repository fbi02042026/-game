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
        /// <summary>近战专属（2026-09-15）：true 时远程职业（游侠/法师/牧师）抽不到。
        /// 只给"冲锋、剑刃风暴"这类动作本身就要求贴身的技能打标记——
        /// 避免"脆皮游侠冲进怪堆"这种语义与操作都别扭的组合。
        /// AOE 落点已改成可选远程位置，所以只有真正过不去的语义才需要这个标记。</summary>
        public bool meleeOnly;
        public string allyConfigId;
        public Color tint;
        public SkillSystem.SkillType skillType;
        public AttackVfxKit attackKit;
        public float damageMultiplier;
        public float baseDamage;
        public float aoeRadius;
        /// <summary>
        /// AOE / 连锁单次最多命中几个目标（**0 = 不限**，保持「半径内全体通吃」的旧行为）。
        /// <para>【2026-10-11 主人拍板】天雷裁决要削弱：原来是无上限 AOE（半径 8 内全体吃 350%），
        /// 怪一多就是全屏清场 → 改成本字段封顶 3 个、伤害降到 80%。
        /// 真源在 player_skills.csv 末列 <c>aoeMaxTargets</c>，这里只是缺表时的 Fallback。</para>
        /// <para>星级成长：只填** 1 星 **的值，运行时每多 1 星 +1（见 <c>RunDraftDirector.BuildRunSkill</c>）。</para>
        /// </summary>
        public int aoeMaxTargets;
        /// <summary>
        /// 本技能的星级伤害步长，覆盖全局 <c>SkillDraftMeta.StarDamageStep</c>（0.18）。
        /// **0 = 用全局**；【2026-10-11 主人拍板】天雷裁决填 0.10（每星 +10%），别的技能留 0。
        /// </summary>
        public float starDamageStep;
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
        /// <summary>
        /// 单次释放耗蓝（2026-10-06 主人拍板）。0 = 无蓝（纯 CD，不参与 MP 闸门）。
        /// 真源：player_skills.csv「耗蓝」列（索引 24）；缺表时走 Fallback 里的这个值，两边同值。
        /// </summary>
        public float mpCost;
    }

    /// <summary>缺表时的 1:1 种子；数值与 player_skills.csv / 设计文档一致。</summary>
    public static readonly Def[] Fallback =
    {
        new Def
        {
            id = "heal_spring",
            mpCost = 100f,
            displayName = "治愈之泉",
            kind = Kind.Heal,
            desc = "给当前生命比例最低的我方单位回血（玩家或佣兵）。",
            numbers = "恢复目标 30% 最大生命",
            // 【2026-10-15 主人拍板】CD 12 → 24（主人：太快了，再变慢一倍）；与 player_skills.csv 同值。
            cooldown = 24f,
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
            mpCost = 120f,
            displayName = "天雷裁决",
            kind = Kind.Aoe,
            desc = "召唤天雷轰击目标区域。",
            // 【2026-10-11 主人拍板】削弱：350% → 80% 攻击，命中目标封顶 3 个（原来半径内全体）。
            numbers = "造成 80% 攻击的范围伤害 每星多打 1 个目标",
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
            // 【2026-10-11 主人拍板】3.5 → 0.8（80% 攻击）；与 player_skills.csv 同值。
            damageMultiplier = 0.8f,
            aoeRadius = 8f,       // 【2026-10-11 主人拍板】半径不变，永远是 8
            aoeMaxTargets = 3,    // 1 星 3 个，每星 +1（2 星 4 个 …）
            starDamageStep = 0.10f, // 每星 +10%（全局是 +18%，天雷削弱后单独压低）
            projectileCount = 1,
            projectileSpeed = 12f,
            buffAttr = AttrType.Attack,
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
        _idIndexBuilt = false;
        PlayerSkillTable.Reload();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded && _all != null) return;
        _loaded = true;
        _all = CloneFallback();

        // 缺表也要乘：表在不在都得延长 CD，所以这里只跳过 overlay，不能整段 return
        if (PlayerSkillTable.HasData)
        {
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

        // 表与 Fallback 只在这里统一乘一次（倍率见 GameConfig.SKILL_COOLDOWN_MUL，
        // 2026-10-10 起为 1f = 配表值即实机值，不要再乘），
        // 战斗（PlayerSkillTable.BuildRuntimeConfig）与 UI 文案（FormatDetail）看到的是同一份冷却。
        if (_all != null)
        {
            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i] == null) continue;
                _all[i].cooldown *= GameConfig.SKILL_COOLDOWN_MUL;
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
            meleeOnly = src.meleeOnly,
            allyConfigId = src.allyConfigId,
            tint = src.tint,
            skillType = src.skillType,
            attackKit = src.attackKit,
            damageMultiplier = src.damageMultiplier,
            baseDamage = src.baseDamage,
            aoeRadius = src.aoeRadius,
            aoeMaxTargets = src.aoeMaxTargets,
            starDamageStep = src.starDamageStep,
            projectileCount = src.projectileCount,
            projectileSpeed = src.projectileSpeed,
            buffAttr = src.buffAttr,
            buffValue = src.buffValue,
            buffIsPercent = src.buffIsPercent,
            healBase = src.healBase,
            healPercentOfMax = src.healPercentOfMax,
            healAtkMul = src.healAtkMul,
            energyCost = src.energyCost,
            hasCombat = src.hasCombat,
            // 【2026-10-12 补漏】原来这一行漏抄，Fallback 里写的 mpCost（治愈之泉 100 / 天雷裁决 120）
            // 克隆后全变 0 —— 缺表时 MpCostOf 会退化成「人人无蓝」，与「表与 Fallback 两边同值」口径打架。
            mpCost = src.mpCost
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
            dest.aoeMaxTargets = row.AoeMaxTargets;
            dest.starDamageStep = row.StarDamageStep;
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

    /// <summary>
    /// 玩家技能单次耗蓝的<b>唯一出口</b>（2026-10-06）。
    /// 读表优先（player_skills.csv「耗蓝」列），缺表回退 Fallback —— 两边数值必须同值。
    /// 返回值：&gt;0 = 有蓝；0 = 无蓝（纯 CD）；<b>-1 = 这个 id 根本不存在</b>（调用方按无蓝处理，不静默当 0）。
    /// </summary>
    public static float MpCostOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1f;
        if (PlayerSkillTable.TryGet(id, out var row)) return row.MpCost;
        var def = GetById(id);
        return def != null ? def.mpCost : -1f;
    }

    // 2026-10-10：运行时 id（ally_* 别名）→ 玩家真 id 的反查索引。
    // 只在 EnsureLoaded 之后按 _all 惰性建一次，不重复解析 CSV。
    static System.Collections.Generic.HashSet<string> _realIds;
    static System.Collections.Generic.Dictionary<string, string> _aliasToReal;
    static bool _idIndexBuilt;

    /// <summary>
    /// 运行时 id → 玩家真 id（2026-10-10 主人拍板：统一用玩家真 id，别名只在装配 SO 时用）。
    /// <para>
    /// 运行时 <c>ActiveSkill.skillId</c> 是 <see cref="PlayerSkillTable.BuildRuntimeConfig"/> 写进去的
    /// allyConfigId 别名（SkillRegistry 按别名索引 VFX，这个不能改）；而 player_skills 表、
    /// 触发条件表、冷却键都以<b>真 id</b> 为键。因此在按 id 查表 / 查触发 / 上冷却前必须先过这里。
    /// </para>
    /// 已是真 id → 原样返回；是已登记的 ally_* 别名 → 返回该 def 的 id；
    /// 两者都不是（佣兵/怪物技能 id 等）→ 原样返回，<b>不兜底造假 id</b>。
    /// </summary>
    public static string ResolveRealSkillId(string runtimeId)
    {
        if (string.IsNullOrEmpty(runtimeId)) return runtimeId;
        EnsureLoaded();
        BuildIdIndex();
        if (_realIds.Contains(runtimeId)) return runtimeId;
        return _aliasToReal.TryGetValue(runtimeId, out string real) ? real : runtimeId;
    }

    static void BuildIdIndex()
    {
        if (_idIndexBuilt && _realIds != null && _aliasToReal != null) return;
        _realIds = new System.Collections.Generic.HashSet<string>();
        _aliasToReal = new System.Collections.Generic.Dictionary<string, string>();
        if (_all != null)
        {
            for (int i = 0; i < _all.Length; i++)
            {
                var d = _all[i];
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                _realIds.Add(d.id);
                if (string.IsNullOrEmpty(d.allyConfigId)) continue;
                // 同一个别名被多个 def 登记时（2026-10-11 之前是 ally_shield 同时挂在 holy_barrier 与 iron_wall 上）
                // 先登记者胜 —— 与 GetByAllyConfigId 的线性扫描顺序一致，避免两处解析结果打架。
                if (!_aliasToReal.ContainsKey(d.allyConfigId))
                    _aliasToReal[d.allyConfigId] = d.id;
            }
        }
        _idIndexBuilt = true;
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
