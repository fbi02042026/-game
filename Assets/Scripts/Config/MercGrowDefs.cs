/// <summary>
/// 佣兵升阶（局外养成等级）纯数据表（2026-10-23 主人拍板，施工单《佣兵升阶（局外养成等级）》T1）。
///
/// 口径（全部定稿，改动只动这里；与升星 MercStarDefs 是两套并行系统，互不引用）：
///   · 等级上限按稀有度：普通 25 / 稀有 35 / 传奇 50（读 MercRosterDefs.MercRarity）。
///   · 每级消耗（升到 currentLevel+1 这一级，只有金币 + 职业徽记两项）：
///       金币   = 基础价 × (1 + 0.25 × (currentLevel - 1))，基础价按稀有度 普通90/稀有150/传奇240，向下取整。
///       职业徽记：Lv1-10 不需要（0）；Lv11-20 低级×1/级；Lv21-35 中级×2/级；Lv36-50 高级×3/级。
///         档位字符串映射：低级="普通"、中级="稀有"、高级="传奇"（与 MercGrowInventory.BadgeId 的 tier 一致）。
///   · 🔴【2026-10-23 主人拍板 C1=A】养成不吃碎片，碎片归升星专供 —— 本表**没有任何碎片消耗字段**
///     （连 0 的壳都不留，防后人误读）；扣费只走金币 + 徽记两条。
///   · 随机属性（升级时掷，攻/防/血三项）：
///       ① 三项各加保底 FloorPctFor(currentLevel)：≤10 → +2%；11~25 → +1.5%；26~50 → +1%
///         【2026-10-23 主人拍板 C2=A 保底分段递减】（升到 Lv2 时按 currentLevel=1 取值）。
///       ② 再等权抽 1 项，额外加成按稀有度分档：普通 5.6~6.6% / 稀有 5.9~6.9% / 传奇 6.4~7.5%
///         （2026-11-03 反推补足，使满级期望命中 1.90 / 2.24 / 2.80；旧的 +3~5% 已删除，只留这一套）。
///       ③ 保底轨道：记录 lastRolled；当 newLevel % 5 == 0 时，额外项先从「非上轮项」里抽（防一路全落同一项）。
///   · 升阶节点：newLevel ∈ {10,20,30,40,50}，节点额外消耗 = 该级当段徽记 ×5（只有徽记，没有碎片，
///     2026-10-23 拍板 Q9 定稿；Lv20/Lv40 节点**不发任何技能**【C3=A 一期不做技能槽】，节点只是消耗加码 + 阶位 +1）。
///   · 技能强化档（按等级推导，不单独存）：Lv≥10 → 1 档；Lv≥30 且稀有/传奇 → 2 档；Lv≥50 且传奇 → 3 档。
///     系数表 StrengthenMul = { 1.0, 1.20, 1.40, 1.60 }（下标 = 档位）。
/// </summary>
public static class MercGrowDefs
{
    // ============================ 等级上限 ============================

    /// <summary>等级上限按稀有度（2026-10-23 主人拍板：普通 25 / 稀有 35 / 传奇 50）。</summary>
    public static int MaxLevel(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 50;
            case MercRosterDefs.MercRarity.Rare: return 35;
            default: return 25;
        }
    }

    // ============================ 每级消耗（金币） ============================

    /// <summary>金币基础价按稀有度（2026-10-23 主人拍板：普通 90 / 稀有 150 / 传奇 240）。</summary>
    public static int GoldBaseCost(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 240;
            case MercRosterDefs.MercRarity.Rare: return 150;
            default: return 90;
        }
    }

    /// <summary>
    /// 升到 currentLevel+1 这一级的金币消耗：
    /// 基础价 × (1 + 0.25 × (currentLevel - 1))，向下取整。
    /// 例：普通 Lv1→2 = 90；Lv9→10 = 90×3 = 270；Lv24→25 = 90×6.75 = 607。
    /// </summary>
    public static int GoldCost(MercRosterDefs.MercRarity rarity, int currentLevel)
    {
        if (currentLevel < 1) currentLevel = 1;
        float gold = GoldBaseCost(rarity) * (1f + 0.25f * (currentLevel - 1));
        return (int)gold;   // 向下取整（正数域截断即 floor）
    }

    // ============================ 每级消耗（职业徽记） ============================

    /// <summary>
    /// 升到 currentLevel+1 这一级的职业徽记消耗数量（档位见 <see cref="BadgeTierForLevel"/>）：
    /// Lv1-10 → 0；Lv11-20 → 1；Lv21-35 → 2；Lv36-50 → 3（2026-10-23 主人拍板）。
    /// </summary>
    public static int BadgeCountForLevel(int currentLevel)
    {
        if (currentLevel <= 10) return 0;   // Lv1-10 不需要徽记
        if (currentLevel <= 20) return 1;   // Lv11-20 低级 ×1/级
        if (currentLevel <= 35) return 2;   // Lv21-35 中级 ×2/级
        return 3;                           // Lv36-50 高级 ×3/级
    }

    /// <summary>
    /// 升到 currentLevel+1 这一级的徽记档位字符串（与 MercGrowInventory.BadgeId(jobKey, tier) 的 tier 一致）：
    /// 低级="普通"、中级="稀有"、高级="传奇"。该级不需要徽记时返回 null。
    /// </summary>
    public static string BadgeTierForLevel(int currentLevel)
    {
        if (currentLevel <= 10) return null;    // 低级
        if (currentLevel <= 20) return "普通";   // 低级
        if (currentLevel <= 35) return "稀有";   // 中级
        return "传奇";                           // 高级
    }

    // ============================ 升阶节点（10/20/30/40/50） ============================

    /// <summary>升阶节点等级集合（newLevel 命中即为节点：消耗加码 + ascended+1；一期不发技能，C3=A）。</summary>
    public static bool IsAscendNode(int newLevel)
    {
        return newLevel == 10 || newLevel == 20 || newLevel == 30 || newLevel == 40 || newLevel == 50;
    }

    /// <summary>节点额外徽记数量：当段徽记 ×5（2026-10-23 拍板 Q9 定稿；没有碎片项）。</summary>
    public const int AscendNodeBadgeCount = 5;

    /// <summary>
    /// 升阶节点的「当段」徽记档位：按节点等级落进的徽记档位段取值；
    /// Lv10 节点本身处于免徽记段（Lv1-10），按下一段（Lv11-20 低级="普通"）取值 —— 即
    /// 10→普通 / 20→普通 / 30→稀有 / 40→传奇 / 50→传奇。非节点返回 null。
    /// </summary>
    public static string AscendNodeBadgeTier(int newLevel)
    {
        if (!IsAscendNode(newLevel)) return null;
        if (newLevel <= 20) return "普通";
        if (newLevel <= 35) return "稀有";
        return "传奇";
    }

    // ============================ 随机属性（升级时掷） ============================

    /// <summary>
    /// 三项（攻/防/血）各自的保底加成百分比，按**升级前等级**分段（升到 Lv2 按 currentLevel=1 取值）：
    /// ≤10 → +2%；11~25 → +1.5%；26~50 → +1%（2026-10-23 主人拍板 C2=A 保底分段递减）。
    /// </summary>
    public static float FloorPctFor(int currentLevel)
    {
        if (currentLevel <= 10) return 0.02f;
        if (currentLevel <= 25) return 0.015f;
        return 0.01f;
    }

    /// <summary>
    /// 额外掷骰那单项的加成下限/上限（2026-11-03 按稀有度分档，全项目只留这一套口径）：
    /// 普通 5.6~6.6%（均值 6.1） / 稀有 5.9~6.9%（均值 6.4） / 传奇 6.4~7.5%（均值 6.95）。
    /// 缘由：C2=A 保底分段递减后，v4 的 +3~5% 只能到 1.73 / 1.97 / 2.32，够不到施工单验收
    /// 1.90 / 2.24 / 2.80；这里按目标反推补足额外项，旧的 +3~5% 常量已删除，禁止两套范围并存。
    /// 校验（均值）：普通 41% + 24×6.1%/3 = 90%；稀有 51.5% + 34×6.4%/3 = 124%；传奇 66.5% + 49×6.95%/3 = 180%。
    /// </summary>
    public static float ExtraRollPctMin(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare: return 0.059f;
            case MercRosterDefs.MercRarity.Legendary: return 0.064f;
            default: return 0.056f;
        }
    }

    /// <summary>额外掷骰单项加成上限（分档口径见 <see cref="ExtraRollPctMin"/>）。</summary>
    public static float ExtraRollPctMax(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare: return 0.069f;
            case MercRosterDefs.MercRarity.Legendary: return 0.075f;
            default: return 0.066f;
        }
    }

    /// <summary>随机属性项数：0=攻 1=防 2=血。</summary>
    public const int AptitudeKindCount = 3;

    /// <summary>
    /// 保底轨道规则：newLevel 是 5 的倍数时，额外掷骰项先从「非上轮项」里抽（避免一路全落同一项）。
    /// </summary>
    public static bool ExtraRollAvoidsLast(int newLevel)
    {
        return newLevel % 5 == 0;
    }

    // ============================ 技能强化档 ============================

    /// <summary>强化系数表（下标 = 档位 0~3）：1.0 / 1.20 / 1.40 / 1.60。</summary>
    public static readonly float[] StrengthenMul = { 1.0f, 1.20f, 1.40f, 1.60f };

    /// <summary>
    /// 技能强化档（按等级+稀有度推导，不单独存）：
    /// Lv≥10 → 1 档；Lv≥30 且 稀有/传奇 → 2 档；Lv≥50 且 传奇 → 3 档；否则 0 档。
    /// 🔴 一期不做第 2/3 技能槽（2026-10-23 主人拍板 C3=A），Lv20/Lv40 节点不发任何技能，
    /// 本文件不写职业→被动池的抽取逻辑。
    /// </summary>
    public static int SkillTier(int level, MercRosterDefs.MercRarity rarity)
    {
        if (level >= 50 && rarity == MercRosterDefs.MercRarity.Legendary) return 3;
        if (level >= 30 && (rarity == MercRosterDefs.MercRarity.Legendary || rarity == MercRosterDefs.MercRarity.Rare)) return 2;
        if (level >= 10) return 1;
        return 0;
    }
}
