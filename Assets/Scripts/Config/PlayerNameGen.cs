using System.Text;
using UnityEngine;

/// <summary>
/// 玩家冒险者随机名字：修饰词 + 的 + 名字。规则见策划表「玩家冒险者随机名字生成表」。
/// </summary>
public static class PlayerNameGen
{
    public enum Gender
    {
        Neutral = 0,
        Male = 1,
        Female = 2,
    }

    static readonly string[] Modifiers =
    {
        "逐风", "裂隙", "星陨", "暮火", "无影", "狂焰", "碎银", "寒霜", "流光", "深渊",
        "苍穹", "暗影", "雷霆", "赤霄", "玄铁", "破军", "斩龙", "绝尘", "断罪", "裁决",
        "圣裁", "幽冥", "虚空", "燎原", "永恒", "孤城", "镇世", "破天", "不灭", "天罚",
        "摸鱼", "退堂鼓", "随缘", "加班", "夜猫", "氪金", "干饭", "逍遥", "吃瓜", "打工",
        "背锅", "打拼", "划水", "咸鱼", "攒钱",
        "王城", "苍蓝", "灰烬", "星火", "雷鸣", "霜狼", "铁炉", "银月",
    };

    static readonly string[] MemeModifiers =
    {
        "摸鱼", "退堂鼓", "随缘", "加班", "夜猫", "氪金", "干饭", "逍遥", "吃瓜", "打工",
        "背锅", "打拼", "划水", "咸鱼", "攒钱",
    };

    static readonly string[] MaleNames =
    {
        "破晓", "断岳", "问天", "乘风", "猎空", "铸星", "焚河", "听雷", "饮雪", "踏歌",
        "摘星", "逐月", "镇岳", "横刀", "挽弓", "执盾", "独行", "无锋", "狂歌", "啸天",
    };

    static readonly string[] FemaleNames =
    {
        "挽月", "听雪", "流萤", "灼华", "凝霜", "落霞", "逐星", "织梦", "吟风", "碎玉",
        "青鸾", "红袖", "素问", "凌波", "寒烟", "倾城", "无忧", "清欢", "琉璃", "星落",
    };

    static readonly string[] NeutralNames =
    {
        "逐风", "观澜", "衔烛", "枕云", "问心", "行远", "守一", "无涯", "无名", "自在",
        "归鸿", "浮生", "长明", "孤舟", "旧梦",
    };

    const float MemeModifierChance = 0.25f;

    public static string Roll(Gender gender = Gender.Neutral)
    {
        string mod = PickModifier();
        string name = PickGivenName(gender);
        return mod + "\u7684" + name;
    }

    static string PickModifier()
    {
        if (Random.value < MemeModifierChance)
            return MemeModifiers[Random.Range(0, MemeModifiers.Length)];
        return Modifiers[Random.Range(0, Modifiers.Length)];
    }

    static string PickGivenName(Gender gender)
    {
        switch (gender)
        {
            case Gender.Male:
                return Random.value < 0.5f
                    ? MaleNames[Random.Range(0, MaleNames.Length)]
                    : NeutralNames[Random.Range(0, NeutralNames.Length)];
            case Gender.Female:
                return Random.value < 0.5f
                    ? FemaleNames[Random.Range(0, FemaleNames.Length)]
                    : NeutralNames[Random.Range(0, NeutralNames.Length)];
            default:
                return NeutralNames[Random.Range(0, NeutralNames.Length)];
        }
    }

    /// <summary>校验玩家手输或随机名；空/过长/非法字符返回 false。</summary>
    public static bool TryValidate(string raw, out string cleaned, out string error)
    {
        cleaned = Sanitize(raw);
        error = null;
        if (string.IsNullOrEmpty(cleaned))
        {
            error = "\u8bf7\u8f93\u5165\u5192\u9669\u8005\u59d3\u540d";
            return false;
        }

        int len = CountHanChars(cleaned);
        if (len < 2 || len > 8)
        {
            error = "\u59d3\u540d\u9700\u4e3a 2\u20138 \u4e2a\u6c49\u5b57";
            return false;
        }

        foreach (char c in cleaned)
        {
            if (IsHan(c) || c == '\u7684' || c == '\u00b7' || c == '\u30fb')
                continue;
            error = "\u4ec5\u652f\u6301\u6c49\u5b57\u4e0e\u300c\u7684\u300d\u300c\u00b7\u300d";
            return false;
        }

        return true;
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw.Trim())
        {
            if (!char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    static int CountHanChars(string s)
    {
        int n = 0;
        foreach (char c in s)
        {
            if (IsHan(c)) n++;
        }
        return n;
    }

    static bool IsHan(char c) => c >= 0x4E00 && c <= 0x9FFF;
}
