using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天赋 V4 静态表（右列重制）。
/// 左列 L1-L40（金币，线性必点）；右列 12 个「天赋石」节点（花石、可升级、分批开放、互斥）。
/// 右列数据来源：Assets/Data/Source/Tables/talent_right.csv → Resources/Data/Tables/talent_right.bytes。
/// 缺表/解析失败时回退到下方 HardcodedRightNodes()，保证天赋界面一定开得起来。
/// </summary>
public static class TalentDefs
{
    public enum AttrKind
    {
        Attack,
        Hp,
        Defense,
        CritRate,
        AtkSpeed,
        CritDamage,
        GoldDrop,
        BagSlots,
        MatDrop,
        TalentStoneDrop,
        StaminaRegen,
        ShopDiscount,
        EnhanceMatCap,
        KeepGoldOnExtract,
        LeftGoldDiscount,
        SkillCooldown,
        SkillDamage,
        WeaponSwordShield,
        WeaponHeavy,
        WeaponRangedMagic,
        PhysDamage,
        MagicDamage,
        PhysPen,
        MoveSpeed,
        Custom
    }

    public enum Side
    {
        Left,
        Right
    }

    /// <summary>右列节点类型：属性（单效果可升级）/ 互斥（同组只点一个）/ 6选1+可升级 / 一次性。</summary>
    public enum RightNodeType
    {
        Attr,
        Mutex,
        JobChoice,
        OneTime
    }

    [Serializable]
    public class Effect
    {
        public AttrKind kind;
        public float value;
        public string display;
    }

    [Serializable]
    public class LeftNode
    {
        public int index;
        public string id;
        public string name;
        public int goldCost;
        public int recommendLevel;
        public Effect effect;
    }

    /// <summary>右列节点的一个选项（JobChoice 有 6 个；其余节点只有 1 个）。</summary>
    [Serializable]
    public class TalentRightOption
    {
        public string key;
        public string name;
        public AttrKind kind;
        /// <summary>第 L 级（索引 L-1）的绝对值，例如 R_F1 = [5,10,15,20,25] 表示 Lv1..Lv5 分别为 +5%..+25%。</summary>
        public float[] valueByLevel;
    }

    /// <summary>
    /// 右列天赋节点（重制后的唯一模型，取代旧 ChoiceNode）。
    /// 字段：unlockLeftIndex 门槛 / costByLevel[] 各级花费 / maxLevel 上限 / mutexGroup 互斥组。
    /// </summary>
    [Serializable]
    public class TalentRightNode
    {
        public int index;
        public string id;
        public string name;
        public RightNodeType type;
        /// <summary>任务3：解锁需要的左列已点数量门槛。</summary>
        public int unlockLeftIndex;
        public int maxLevel;
        /// <summary>costByLevel[L-1] = 从第 L-1 级升到第 L 级的花费（L=1 即解锁费）。</summary>
        public int[] costByLevel;
        /// <summary>任务4：互斥组名；"" 表示无互斥。</summary>
        public string mutexGroup = "";
        /// <summary>选项：非 JobChoice 长度 1；JobChoice 长度 6（对应 6 职业）。</summary>
        public TalentRightOption[] options;

        public bool IsJobChoice => type == RightNodeType.JobChoice;
        public bool IsOneTime => type == RightNodeType.OneTime;

        public TalentRightOption ChosenOption(int chosenJob1Based)
        {
            if (options == null || options.Length == 0) return null;
            int i = chosenJob1Based - 1;
            return (i >= 0 && i < options.Length) ? options[i] : options[0];
        }

        /// <summary>节点在指定等级、指定（已选）选项下的效果绝对值。</summary>
        public float EffectValue(int level, int chosenJob1Based)
        {
            var opt = IsJobChoice ? ChosenOption(chosenJob1Based)
                                  : (options != null && options.Length > 0 ? options[0] : null);
            if (opt == null || opt.valueByLevel == null) return 0f;
            int i = level - 1;
            if (i < 0 || i >= opt.valueByLevel.Length) return 0f;
            return opt.valueByLevel[i];
        }

        /// <summary>当前等级的增益文案，如「物理伤害 +10%」「攻击 +6」。一次性节点返回节点名。</summary>
        public string EffectSummary(int level, int chosenJob1Based)
        {
            if (IsOneTime || level <= 0) return name;
            var opt = IsJobChoice ? ChosenOption(chosenJob1Based)
                                  : (options != null && options.Length > 0 ? options[0] : null);
            if (opt == null) return name;
            float v = EffectValue(level, chosenJob1Based);
            if (v == 0f) return name;
            string unit = (opt.kind == AttrKind.Attack || opt.kind == AttrKind.Hp) ? "" : "%";
            return KindLabel(opt.kind) + " +" + FormatNum(v) + unit;
        }
    }

    /// <summary>天赋石产出常量（任务6）。掉落/每日由另一子代理接，这里只产出数值。</summary>
    public static class TalentStoneRewards
    {
        /// <summary>每关首次通关（按章 1..8）：2/3/4/5/6/7/8/10 石。</summary>
        static readonly int[] FirstClear = { 2, 3, 4, 5, 6, 7, 8, 10 };
        /// <summary>整章通关额外：3/5/7/9/11/14/17/20 石。</summary>
        static readonly int[] ChapterClear = { 3, 5, 7, 9, 11, 14, 17, 20 };
        /// <summary>每日保底：登录 3 + 每日任务 5 = 8 石/天。</summary>
        public const int DailyStones = 8;

        public static int FirstClearStone(int chapter1Based)
        {
            int i = chapter1Based - 1;
            return (i >= 0 && i < FirstClear.Length) ? FirstClear[i] : 0;
        }

        public static int ChapterClearStone(int chapter1Based)
        {
            int i = chapter1Based - 1;
            return (i >= 0 && i < ChapterClear.Length) ? ChapterClear[i] : 0;
        }
    }

    public static readonly LeftNode[] Left;
    /// <summary>右列 12 节点（来自 talent_right.csv，缺表回退硬编码）。</summary>
    public static readonly TalentRightNode[] RightNodes;

    static TalentDefs()
    {
        Left = BuildLeft();
        RightNodes = BuildRightNodes();
    }

    // ===== 左列查询（保持不变） =====
    public static LeftNode GetLeft(int index1Based)
    {
        if (index1Based < 1 || index1Based > Left.Length) return null;
        return Left[index1Based - 1];
    }

    public static int LeftUnlockedCount(IDictionary<string, int> talents)
    {
        if (talents == null) return 0;
        int n = 0;
        for (int i = 0; i < Left.Length; i++)
        {
            if (talents.TryGetValue(Left[i].id, out int lv) && lv > 0) n++;
            else break;
        }
        return n;
    }

    // ===== 右列查询 =====
    public static int RightNodeCount => RightNodes.Length;

    public static TalentRightNode GetRightNode(int index1Based)
    {
        if (index1Based < 1 || index1Based > RightNodes.Length) return null;
        return RightNodes[index1Based - 1];
    }

    public static TalentRightNode GetRightNodeById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < RightNodes.Length; i++)
            if (RightNodes[i].id == id) return RightNodes[i];
        return null;
    }

    public static int GetRightNodeLevel(IDictionary<string, int> talents, string id)
    {
        if (talents == null || string.IsNullOrEmpty(id)) return 0;
        int lv = 0;
        talents.TryGetValue(id, out lv);
        return lv < 0 ? 0 : lv;
    }

    /// <summary>JobChoice 节点已选职业（1-based 选项下标），未选返回 0。</summary>
    public static int GetRightNodeChosenJob(IDictionary<string, int> talents, string id)
    {
        if (talents == null || string.IsNullOrEmpty(id)) return 0;
        int j = 0;
        talents.TryGetValue(id + "_job", out j);
        return j < 0 ? 0 : j;
    }

    public static int RightUnlockedCount(IDictionary<string, int> talents)
    {
        if (talents == null) return 0;
        int n = 0;
        for (int i = 0; i < RightNodes.Length; i++)
        {
            int lv = 0;
            if (talents.TryGetValue(RightNodes[i].id, out lv) && lv > 0) n++;
        }
        return n;
    }

    // ===== 任务3：等级上限公式（实现位置，加注释）=====
    /// <summary>
    /// 可升上限 = min(maxLevel, 1 + floor((当前左列已点数 - unlockLeftIndex) / 5))。
    /// 例：R_F1 门槛 10 → L10 上限1、L15 上限2、L20 上限3、L25 上限4、L30 上限5。
    /// 未达门槛返回 0（不可解锁）。
    /// </summary>
    public static int RightNodeLevelCap(TalentRightNode node, int leftUnlocked)
    {
        if (node == null) return 0;
        if (leftUnlocked < node.unlockLeftIndex) return 0;
        int cap = 1 + (leftUnlocked - node.unlockLeftIndex) / 5; // 整数除法即 floor
        return cap < node.maxLevel ? cap : node.maxLevel;
    }

    // ===== 任务4：互斥组（实现位置，加注释）=====
    /// <summary>
    /// 任务4：同一 mutexGroup 内只要有一个「其它」节点已点（level>0），本节点即被锁（置灰、不可点，用锁图标）。
    /// </summary>
    public static bool IsRightNodeMutexLocked(TalentRightNode node, IDictionary<string, int> talents)
    {
        if (node == null || string.IsNullOrEmpty(node.mutexGroup) || talents == null) return false;
        for (int i = 0; i < RightNodes.Length; i++)
        {
            var o = RightNodes[i];
            if (o == node || string.IsNullOrEmpty(o.mutexGroup)) continue;
            if (!string.Equals(o.mutexGroup, node.mutexGroup)) continue;
            int lv = 0;
            if (talents.TryGetValue(o.id, out lv) && lv > 0) return true;
        }
        return false;
    }

    // ===== 任务7：背包 / 技能槽 / 双修 查询 =====
    /// <summary>是否已解锁背包第 4 行（R_BAG 点了即 true）。实际网格改造由另一子代理做。</summary>
    public static bool IsBagRow4Unlocked(IDictionary<string, int> talents) =>
        talents != null && GetRightNodeLevel(talents, "R_BAG") > 0;

    /// <summary>主动技能槽上限：基础 3，R_SLOT 点了变 4。佣兵位固定 2，不在此处。</summary>
    public static int ActiveSkillSlotCap(IDictionary<string, int> talents)
    {
        int cap = 3;
        if (talents != null && GetRightNodeLevel(talents, "R_SLOT") > 0) cap = 4;
        return cap;
    }

    /// <summary>是否解锁第二流派（R_DUAL 点了即 true；其解锁本身要求主流派满 5 级，见 TalentSystem）。</summary>
    public static bool IsDualPathUnlocked(IDictionary<string, int> talents) =>
        talents != null && GetRightNodeLevel(talents, "R_DUAL") > 0;

    /// <summary>兼容旧存档/GameConfig：解锁的额外背包行数（R_BAG 提供 1 行）。</summary>
    public static int CountBagRowUnlocks(IDictionary<string, int> talents) =>
        IsBagRow4Unlocked(talents) ? 1 : 0;

    /// <summary>洗点时退石计算用：节点从 0 升到 level 累计花费的石。</summary>
    public static int RightNodeTotalSpent(TalentRightNode node, int level)
    {
        if (node == null || level <= 0 || node.costByLevel == null) return 0;
        int sum = 0;
        int n = level < node.costByLevel.Length ? level : node.costByLevel.Length;
        for (int i = 0; i < n; i++) sum += node.costByLevel[i];
        return sum;
    }

    // ===== 构建：优先读表，缺表回退硬编码 =====
    static TalentRightNode[] BuildRightNodes()
    {
        try
        {
            string raw = GameTableStore.LoadText(ContentPaths.Data.TalentRight);
            if (!string.IsNullOrEmpty(raw))
            {
                var parsed = ParseRightNodesFromCsv(raw);
                if (parsed != null && parsed.Length > 0) return parsed;
            }
#if UNITY_EDITOR
            Debug.LogWarning("[TalentDefs] 缺 talent_right 表，回退硬编码右列。");
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TalentDefs] 右列表解析失败，回退硬编码：" + e.Message);
        }
        return HardcodedRightNodes();
    }

    static TalentRightNode[] ParseRightNodesFromCsv(string raw)
    {
        var rows = GameTableCsv.ParseRows(raw);
        if (rows.Count < 2) return null;
        var list = new List<TalentRightNode>();
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c == null || c.Length < 11) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var node = new TalentRightNode
            {
                index = list.Count + 1,
                id = id,
                name = c[1].Trim(),
                type = ParseType(c[2]),
                unlockLeftIndex = GameTableCsv.TryInt(c, 3, 0),
                maxLevel = GameTableCsv.TryInt(c, 4, 1),
                mutexGroup = GameTableCsv.IsWildcard(c[5]) ? "" : c[5].Trim(),
                costByLevel = SplitInts(c[6]),
            };
            if (node.maxLevel < 1) node.maxLevel = 1;
            node.costByLevel = PadCost(node.costByLevel, node.maxLevel);

            string[] keys = SplitSlash(c[7]);
            string[] names = SplitSlash(c[8]);
            string[] kinds = SplitSlash(c[9]);
            float[] vals = SplitFloats(c[10]);

            if (node.type == RightNodeType.JobChoice)
            {
                int n = keys.Length >= names.Length ? keys.Length : names.Length;
                if (n < 1) n = 1;
                node.options = new TalentRightOption[n];
                for (int o = 0; o < n; o++)
                {
                    node.options[o] = new TalentRightOption
                    {
                        // JOB 统一落到「当前职业主伤害」（见 AttrSystem.PrimaryPowerAttr）
                        key = GetOr(keys, o, "job" + o),
                        name = GetOr(names, o, "职业" + (o + 1)),
                        kind = AttrKind.WeaponSwordShield,
                        valueByLevel = (float[])vals.Clone()
                    };
                }
            }
            else
            {
                node.options = new TalentRightOption[1];
                node.options[0] = new TalentRightOption
                {
                    key = GetOr(keys, 0, "main"),
                    name = GetOr(names, 0, node.name),
                    kind = ParseKind(GetOr(kinds, 0, "Custom")),
                    valueByLevel = (float[])vals.Clone()
                };
            }
            list.Add(node);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    static int[] PadCost(int[] src, int maxLevel)
    {
        var fill = new int[maxLevel];
        for (int k = 0; k < maxLevel; k++)
            fill[k] = (src != null && k < src.Length) ? src[k] : 0;
        return fill;
    }

    // ===== 硬编码回退（与 talent_right.csv 逐字对应）=====
    static TalentRightNode[] HardcodedRightNodes()
    {
        var list = new List<TalentRightNode>();
        list.Add(HNode(1, "R_F1", "流派·物理专精", RightNodeType.Mutex, 10, 5, "path",
            new[] { 5, 12, 22, 38, 60 }, AttrKind.PhysDamage, new[] { 5f, 10f, 15f, 20f, 25f }, "phys", "物理专精"));
        list.Add(HNode(2, "R_F2", "流派·魔法专精", RightNodeType.Mutex, 10, 5, "path",
            new[] { 5, 12, 22, 38, 60 }, AttrKind.MagicDamage, new[] { 5f, 10f, 15f, 20f, 25f }, "magic", "魔法专精"));
        list.Add(HNode(3, "R_C1", "利刃", RightNodeType.Attr, 20, 3, "",
            new[] { 6, 14, 26 }, AttrKind.Attack, new[] { 3f, 6f, 10f }, "atk", "利刃"));
        list.Add(HNode(4, "R_C2", "体魄", RightNodeType.Attr, 20, 3, "",
            new[] { 6, 14, 26 }, AttrKind.Hp, new[] { 40f, 80f, 140f }, "hp", "体魄"));
        list.Add(HNode(5, "R_C3", "精准", RightNodeType.Attr, 20, 3, "",
            new[] { 14, 30, 52 }, AttrKind.CritRate, new[] { 1f, 2f, 3f }, "crit", "精准"));
        list.Add(HNode(6, "R_C4", "迅捷", RightNodeType.Attr, 20, 3, "",
            new[] { 14, 30, 52 }, AttrKind.AtkSpeed, new[] { 3f, 6f, 10f }, "spd", "迅捷"));
        list.Add(HNode(7, "R_C5", "疾行", RightNodeType.Attr, 30, 3, "",
            new[] { 25, 50, 85 }, AttrKind.MoveSpeed, new[] { 3f, 6f, 10f }, "move", "疾行"));
        list.Add(HNode(8, "R_C6", "凝神", RightNodeType.Attr, 30, 3, "",
            new[] { 25, 50, 85 }, AttrKind.SkillCooldown, new[] { 4f, 8f, 13f }, "cd", "凝神"));
        // R_JOB：6 选 1 + 可升级（职业伤害 +4%×等级，满 +20%）
        list.Add(HJob(9, "R_JOB", "职业专精", 30, 5, "job",
            new[] { 15, 32, 55, 85, 125 }, new[] { 4f, 8f, 12f, 16f, 20f },
            new[] { "剑盾卫士", "狂战士", "游侠", "法师", "牧师", "重装" }));
        // 一次性节点：kind=Custom（无数值效果，仅靠查询判定解锁状态）
        list.Add(HNode(10, "R_BAG", "背包扩容", RightNodeType.OneTime, 30, 1, "",
            new[] { 60 }, AttrKind.Custom, new[] { 0f }, "bag", "背包扩容"));
        list.Add(HNode(11, "R_SLOT", "技能槽 IV", RightNodeType.OneTime, 40, 1, "",
            new[] { 95 }, AttrKind.Custom, new[] { 0f }, "slot", "技能槽 IV"));
        list.Add(HNode(12, "R_DUAL", "双修解锁", RightNodeType.OneTime, 40, 1, "",
            new[] { 180 }, AttrKind.Custom, new[] { 0f }, "dual", "双修解锁"));
        return list.ToArray();
    }

    static TalentRightNode HNode(int index, string id, string name, RightNodeType type,
        int unlock, int max, string mutex, int[] cost, AttrKind kind, float[] vals,
        string optKey, string optName)
    {
        return new TalentRightNode
        {
            index = index,
            id = id,
            name = name,
            type = type,
            unlockLeftIndex = unlock,
            maxLevel = max,
            mutexGroup = mutex,
            costByLevel = cost,
            options = new[]
            {
                new TalentRightOption { key = optKey, name = optName, kind = kind, valueByLevel = vals }
            }
        };
    }

    static TalentRightNode HJob(int index, string id, string name, int unlock, int max, string mutex,
        int[] cost, float[] vals, string[] jobNames)
    {
        var opts = new TalentRightOption[jobNames.Length];
        for (int o = 0; o < jobNames.Length; o++)
            opts[o] = new TalentRightOption
            {
                key = "job" + o,
                name = jobNames[o],
                kind = AttrKind.WeaponSwordShield,
                valueByLevel = (float[])vals.Clone()
            };
        return new TalentRightNode
        {
            index = index,
            id = id,
            name = name,
            type = RightNodeType.JobChoice,
            unlockLeftIndex = unlock,
            maxLevel = max,
            mutexGroup = mutex,
            costByLevel = cost,
            options = opts
        };
    }

    // ===== 左列（保持不变）=====
    static LeftNode[] BuildLeft()
    {
        string[] names = { "力量", "体质", "防御", "精准", "敏捷" };
        string[] romans = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        float[] atk = { 5, 6, 7, 8, 10, 12, 14, 16 };
        float[] hp = { 25, 30, 40, 50, 60, 75, 90, 110 };
        float[] def = { 3, 4, 5, 6, 8, 10, 12, 15 };
        var list = new LeftNode[40];
        for (int i = 0; i < 40; i++)
        {
            int group = i / 5;
            int slot = i % 5;
            string nm = names[slot] + " " + romans[group];
            Effect fx;
            switch (slot)
            {
                case 0: fx = Fx(AttrKind.Attack, atk[group], $"攻击 +{atk[group]:0}"); break;
                case 1: fx = Fx(AttrKind.Hp, hp[group], $"生命 +{hp[group]:0}"); break;
                case 2: fx = Fx(AttrKind.Defense, def[group], $"防御 +{def[group]:0}"); break;
                case 3: fx = Fx(AttrKind.CritRate, 1f, "暴击率 +1%"); break;
                default: fx = Fx(AttrKind.AtkSpeed, 2f, "攻击速度 +2%"); break;
            }
            list[i] = new LeftNode
            {
                index = i + 1,
                id = "L" + (i + 1),
                name = nm,
                goldCost = 50 + i * 15,
                recommendLevel = i + 1,
                effect = fx
            };
        }
        return list;
    }

    // ===== 解析辅助 =====
    static RightNodeType ParseType(string s)
    {
        if (string.IsNullOrEmpty(s)) return RightNodeType.Attr;
        switch (s.Trim().ToLowerInvariant())
        {
            case "mutex": return RightNodeType.Mutex;
            case "jobchoice":
            case "job": return RightNodeType.JobChoice;
            case "onetime":
            case "once": return RightNodeType.OneTime;
            default: return RightNodeType.Attr;
        }
    }

    static AttrKind ParseKind(string s)
    {
        if (string.IsNullOrEmpty(s)) return AttrKind.Custom;
        if (s.Trim().Equals("JOB", StringComparison.OrdinalIgnoreCase)) return AttrKind.WeaponSwordShield;
        if (Enum.TryParse<AttrKind>(s.Trim(), true, out AttrKind k)) return k;
        return AttrKind.Custom;
    }

    static string[] SplitSlash(string s)
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        var parts = s.Split('/');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    static int[] SplitInts(string s)
    {
        var parts = SplitSlash(s);
        var outp = new List<int>();
        for (int i = 0; i < parts.Length; i++)
        {
            int v;
            if (int.TryParse(parts[i], out v)) outp.Add(v);
        }
        return outp.ToArray();
    }

    static float[] SplitFloats(string s)
    {
        var parts = SplitSlash(s);
        var outp = new List<float>();
        for (int i = 0; i < parts.Length; i++)
        {
            float v;
            if (float.TryParse(parts[i], out v)) outp.Add(v);
        }
        return outp.ToArray();
    }

    static string GetOr(string[] arr, int i, string def)
    {
        if (arr != null && i >= 0 && i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
        return def;
    }

    static string KindLabel(AttrKind k)
    {
        switch (k)
        {
            case AttrKind.PhysDamage: return "物理伤害";
            case AttrKind.MagicDamage: return "魔法伤害";
            case AttrKind.WeaponSwordShield: return "职业伤害";
            case AttrKind.Attack: return "攻击";
            case AttrKind.Hp: return "生命";
            case AttrKind.Defense: return "防御";
            case AttrKind.CritRate: return "暴击率";
            case AttrKind.AtkSpeed: return "攻速";
            case AttrKind.MoveSpeed: return "移速";
            case AttrKind.SkillCooldown: return "技能冷却";
            default: return "";
        }
    }

    static string FormatNum(float v)
    {
        return v == (int)v ? ((int)v).ToString() : v.ToString("0.##");
    }

    static Effect Fx(AttrKind kind, float value, string display)
    {
        return new Effect { kind = kind, value = value, display = display };
    }
}
