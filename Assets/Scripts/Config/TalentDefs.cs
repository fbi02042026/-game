using System;
using System.Collections.Generic;

/// <summary>
/// 天赋 V3 静态表（与设计文档一致）。
/// 左列 L1-L40（金币）；中列 C1-C5、右列 R1-R10（天赋石，多选一）。
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
        Custom
    }

    public enum Side
    {
        Left,
        Right
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

    [Serializable]
    public class ChoiceOption
    {
        public string key;
        public string name;
        public Effect effect;
    }

    [Serializable]
    public class ChoiceNode
    {
        public int index;
        public string id;
        public string groupName;
        public int stoneCost;
        public int requireLeftIndex;
        public ChoiceOption[] options;
    }

    /// <summary>兼容旧名</summary>
    public class RightNode : ChoiceNode { }
    public class RightOption : ChoiceOption { }

    public static readonly LeftNode[] Left;
    /// <summary>右列额外首行：流派选择（物理专精 / 魔法专精），存档 id 为 C1。</summary>
    public static readonly ChoiceNode RightExtra;
    public static readonly ChoiceNode[] Right;

    static TalentDefs()
    {
        Left = BuildLeft();
        RightExtra = BuildRightExtra();
        Right = BuildRight();
    }

    public static LeftNode GetLeft(int index1Based)
    {
        if (index1Based < 1 || index1Based > Left.Length) return null;
        return Left[index1Based - 1];
    }

    public static ChoiceNode GetRightExtra() => RightExtra;

    public static ChoiceNode GetRight(int index1Based)
    {
        if (index1Based < 1 || index1Based > Right.Length) return null;
        return Right[index1Based - 1];
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

    public static int RightUnlockedCount(IDictionary<string, int> talents)
    {
        return CountChoiceBranch(Right, talents);
    }

    public static bool IsRightExtraUnlocked(IDictionary<string, int> talents)
    {
        if (talents == null || RightExtra == null) return false;
        return talents.TryGetValue(RightExtra.id, out int lv) && lv > 0;
    }

    static int CountChoiceBranch(ChoiceNode[] branch, IDictionary<string, int> talents)
    {
        if (talents == null || branch == null) return 0;
        int n = 0;
        for (int i = 0; i < branch.Length; i++)
        {
            if (talents.TryGetValue(branch[i].id, out int lv) && lv > 0) n++;
            else break;
        }
        return n;
    }

    public static int CountBagRowUnlocks(IDictionary<string, int> talents)
    {
        if (talents == null) return 0;
        int n = 0;
        for (int i = 0; i < Right.Length; i++)
        {
            var node = Right[i];
            if (node?.options == null) continue;
            if (!talents.TryGetValue(node.id, out int opt) || opt <= 0) continue;
            int idx = opt - 1;
            if (idx < 0 || idx >= node.options.Length) continue;
            if (node.options[idx].effect != null && node.options[idx].effect.kind == AttrKind.BagSlots)
                n++;
        }
        return n;
    }

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

    static ChoiceNode BuildRightExtra()
    {
        return new ChoiceNode
        {
            index = 1,
            id = "C1",
            groupName = "流派选择",
            stoneCost = 10,
            requireLeftIndex = 8,
            options = new[]
            {
                Opt("A", "物理专精", AttrKind.PhysDamage, 5,
                    "单手剑、双剑、大剑、长柄、剑盾伤害 +5%"),
                // 旧文案多写了「技能冷却 -3%」，实现里没有 —— 去掉，避免玩家按文案期待
                Opt("B", "魔法专精", AttrKind.MagicDamage, 5,
                    "法术伤害 +5%"),
            }
        };
    }

    static ChoiceNode[] BuildRight()
    {
        return new[]
        {
            MakeRight(1, "战斗入门", 5, 4,
                Opt("A", "磨砺", AttrKind.Attack, 2, "攻击 +2"),
                Opt("B", "坚韧", AttrKind.Hp, 15, "生命 +15")),
            MakeRight(2, "生存/收集", 8, 8,
                Opt("A", "扩容", AttrKind.BagSlots, 1, "战斗背包上限 +1 格"),
                Opt("B", "敛财", AttrKind.GoldDrop, 5, "金币掉落 +5%")),
            // 武器专精统一按「当前职业」落到对应的 Power（见 AttrSystem.PrimaryPowerAttr），
            // 所以三个选项都只承诺「当前职业武器伤害」，不再挂靠具体武器名的额外效果。
            MakeRight(3, "武器专精", 12, 12,
                Opt("A", "剑盾专精", AttrKind.WeaponSwordShield, 5, "当前职业武器伤害 +5%"),
                Opt("B", "重兵专精", AttrKind.WeaponHeavy, 5, "当前职业武器伤害 +5%"),
                Opt("C", "远魔专精", AttrKind.WeaponRangedMagic, 5, "当前职业武器伤害 +5%")),
            // 以下四组的原效果（体力恢复 / 商店折扣 / 材料掉落 / 天赋石掉落 / 撤离保留金币）
            // 在 AttrSystem 里没有消费点 → 点了完全没反应。改为立刻生效的属性，文案同步改对。
            MakeRight(4, "恢复/成长", 18, 16,
                Opt("A", "快速休整", AttrKind.Hp, 40, "生命 +40"),
                Opt("B", "讨价", AttrKind.GoldDrop, 3, "金币掉落 +3%")),
            MakeRight(5, "战利品", 28, 20,
                Opt("A", "战利品筛选", AttrKind.Attack, 3, "攻击 +3"),
                Opt("B", "天赋共鸣", AttrKind.CritRate, 1, "暴击率 +1%")),
            MakeRight(6, "资源利用", 40, 24,
                Opt("A", "慧眼识材", AttrKind.Defense, 2, "防御 +2"),
                Opt("B", "裂隙亲和", AttrKind.Hp, 30, "生命 +30")),
            MakeRight(7, "战前准备", 55, 28,
                Opt("A", "背包 +1", AttrKind.BagSlots, 1, "战斗背包上限 +1 格"),
                Opt("B", "体能训练", AttrKind.Attack, 4, "攻击 +4")),
            MakeRight(8, "战斗强化", 75, 32,
                Opt("A", "弱点洞察", AttrKind.CritDamage, 10, "暴击伤害 +10%"),
                Opt("B", "力量爆发", AttrKind.Attack, 5, "攻击 +5")),
            MakeRight(9, "经济", 100, 36,
                Opt("A", "勤俭持家", AttrKind.LeftGoldDiscount, 5, "左侧天赋金币消耗 -5%"),
                // GoldDrop 只作用于金币，旧文案承诺了装备与天赋石 —— 改回真实范围
                Opt("B", "资源管理", AttrKind.GoldDrop, 10, "金币掉落 +10%")),
            MakeRight(10, "终极觉醒", 140, 40,
                // 旧文案多写了「伤害 +5%」，实现里没有 —— 去掉
                Opt("-", "觉醒", AttrKind.SkillCooldown, 10, "所有主动技能冷却 -10%")),
        };
    }

    static ChoiceNode MakeRight(int index, string group, int cost, int requireLeft, params ChoiceOption[] opts)
    {
        return new ChoiceNode
        {
            index = index,
            id = "R" + index,
            groupName = group,
            stoneCost = cost,
            requireLeftIndex = requireLeft,
            options = opts
        };
    }

    static ChoiceOption Opt(string key, string name, AttrKind kind, float value, string display)
    {
        return new ChoiceOption { key = key, name = name, effect = Fx(kind, value, display) };
    }

    static Effect Fx(AttrKind kind, float value, string display)
    {
        return new Effect { kind = kind, value = value, display = display };
    }
}
