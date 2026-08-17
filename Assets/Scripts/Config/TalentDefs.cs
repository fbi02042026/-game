using System;
using System.Collections.Generic;

/// <summary>
/// 天赋 V3 静态表（与 Docs/像素冒险：裂缝之刃_天赋系统设计.md / 数值表一致）。
/// UI 与后续解锁逻辑共用，不依赖 ScriptableObject。
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
        public string display; // 展示用，如「攻击 +3」
    }

    [Serializable]
    public class LeftNode
    {
        public int index;          // 1..40
        public string id;          // L1..L40
        public string name;        // 力量 I
        public int goldCost;
        public int recommendLevel;
        public Effect effect;
    }

    [Serializable]
    public class RightOption
    {
        public string key;         // A/B/C/-
        public string name;
        public Effect effect;
    }

    [Serializable]
    public class RightNode
    {
        public int index;          // 1..10
        public string id;          // R1..R10
        public string groupName;   // 武器专精
        public int stoneCost;
        public int requireLeftIndex; // R_N 需 L_(4N)
        public RightOption[] options;
    }

    public static readonly LeftNode[] Left;
    public static readonly RightNode[] Right;

    static TalentDefs()
    {
        Left = BuildLeft();
        Right = BuildRight();
    }

    public static LeftNode GetLeft(int index1Based)
    {
        if (index1Based < 1 || index1Based > Left.Length) return null;
        return Left[index1Based - 1];
    }

    public static RightNode GetRight(int index1Based)
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

    static LeftNode[] BuildLeft()
    {
        // 每 5 个一组：力量→体质→防御→精准→敏捷 × 8
        string[] names = { "力量", "体质", "防御", "精准", "敏捷" };
        string[] romans = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        float[] atk = { 3, 4, 5, 6, 7, 8, 9, 10 };
        float[] hp = { 10, 12, 15, 18, 22, 26, 30, 35 };
        float[] def = { 2, 2, 3, 3, 4, 4, 5, 5 };
        var list = new LeftNode[40];
        for (int i = 0; i < 40; i++)
        {
            int group = i / 5;
            int slot = i % 5;
            string nm = names[slot] + " " + romans[group];
            Effect fx;
            switch (slot)
            {
                case 0:
                    fx = Fx(AttrKind.Attack, atk[group], $"攻击 +{atk[group]:0}");
                    break;
                case 1:
                    fx = Fx(AttrKind.Hp, hp[group], $"生命 +{hp[group]:0}");
                    break;
                case 2:
                    fx = Fx(AttrKind.Defense, def[group], $"防御 +{def[group]:0}");
                    break;
                case 3:
                    fx = Fx(AttrKind.CritRate, 0.5f, "暴击率 +0.5%");
                    break;
                default:
                    fx = Fx(AttrKind.AtkSpeed, 1f, "攻击速度 +1%");
                    break;
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

    static RightNode[] BuildRight()
    {
        return new[]
        {
            R(1, "战斗入门", 5, Opt("A", "磨砺", AttrKind.Attack, 2, "攻击 +2"),
                Opt("B", "坚韧", AttrKind.Hp, 15, "生命 +15")),
            R(2, "生存/收集", 8, Opt("A", "扩容", AttrKind.BagSlots, 1, "战斗背包上限 +1 格"),
                Opt("B", "敛财", AttrKind.GoldDrop, 5, "金币掉落 +5%")),
            R(3, "武器专精", 12,
                Opt("A", "剑盾专精", AttrKind.WeaponSwordShield, 5, "单手剑/盾伤害 +5%，攻击速度 +2%"),
                Opt("B", "重兵专精", AttrKind.WeaponHeavy, 5, "大剑/长柄武器伤害 +5%，暴击伤害 +5%"),
                Opt("C", "远魔专精", AttrKind.WeaponRangedMagic, 5, "单手杖/弓箭伤害 +5%，技能冷却 -5%")),
            R(4, "恢复/交易", 18, Opt("A", "快速休整", AttrKind.StaminaRegen, 10, "体力恢复速度 +10%"),
                Opt("B", "讨价还价", AttrKind.ShopDiscount, 5, "商店购买价格 -5%")),
            R(5, "材料获取", 28, Opt("A", "强化采集", AttrKind.MatDrop, 1, "强化材料掉落 +1"),
                Opt("B", "天赋共鸣", AttrKind.TalentStoneDrop, 1, "天赋石掉落 +1")),
            R(6, "资源利用", 40, Opt("A", "慧眼识材", AttrKind.MatDrop, 1, "装备分解获得强化石 +1"),
                Opt("B", "裂缝亲和", AttrKind.KeepGoldOnExtract, 5, "撤离时保留金币比例 +5%")),
            R(7, "扩容 II", 55, Opt("A", "背包 +1", AttrKind.BagSlots, 1, "战斗背包上限 +1 格"),
                Opt("B", "仓库扩容", AttrKind.EnhanceMatCap, 20, "强化材料上限 +20")),
            R(8, "战斗强化", 75, Opt("A", "弱点洞察", AttrKind.CritDamage, 10, "暴击伤害 +10%"),
                Opt("B", "力量爆发", AttrKind.Attack, 5, "攻击 +5")),
            R(9, "经济", 100, Opt("A", "勤俭持家", AttrKind.LeftGoldDiscount, 5, "左侧天赋金币消耗 -5%"),
                Opt("B", "强化采集 II", AttrKind.MatDrop, 1, "强化材料掉落 +1")),
            R(10, "终极觉醒", 140, Opt("-", "终极觉醒", AttrKind.SkillCooldown, 10, "所有主动技能冷却 -10%，伤害 +5%")),
        };
    }

    static RightNode R(int index, string group, int cost, params RightOption[] opts)
    {
        return new RightNode
        {
            index = index,
            id = "R" + index,
            groupName = group,
            stoneCost = cost,
            requireLeftIndex = index * 4,
            options = opts
        };
    }

    static RightOption Opt(string key, string name, AttrKind kind, float value, string display)
    {
        return new RightOption { key = key, name = name, effect = Fx(kind, value, display) };
    }

    static Effect Fx(AttrKind kind, float value, string display)
    {
        return new Effect { kind = kind, value = value, display = display };
    }
}
