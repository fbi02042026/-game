using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 天赋图标：左列属性图标 + 右/中列天赋图标（Art/UI/Icons 下裁切图）。
/// </summary>
public static class TalentIcons
{
    const string AttrRoot = "Assets/Art/UI/Icons/属性图标/";
    const string TalentRoot = "Assets/Art/UI/Icons/天赋图标/";

    static readonly string[] LeftAttrFiles =
    {
        "角色_0001s_0000_攻击",
        "角色_0001s_0001_生命",
        "角色_0001s_0002_防御",
        "角色_0001s_0003_暴击",
        "角色_0001s_0004_攻速"
    };

    public static Sprite GetLeftAttr(int slot0to4)
    {
        if (slot0to4 < 0 || slot0to4 >= LeftAttrFiles.Length) return null;
        return Load(AttrRoot + LeftAttrFiles[slot0to4] + ".png");
    }

    /// <summary>按选项展示名取图标（与天赋图标文件夹文件名一致）。</summary>
    public static Sprite GetTalent(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return null;
        // 设计文档名 → 资源文件名
        switch (displayName)
        {
            case "物理训练": return Load(TalentRoot + "物理专精.png");
            case "魔法训练": return Load(TalentRoot + "魔法专精.png");
            case "物理共鸣": return Load(TalentRoot + "物理专精.png");
            case "魔法共鸣": return Load(TalentRoot + "魔法专精.png");
            case "力量掌握": return Load(TalentRoot + "力量爆发.png");
            case "元素掌握": return Load(TalentRoot + "魔法专精.png");
            case "物理本能": return Load(TalentRoot + "弱点洞察.png");
            case "魔法本能": return Load(TalentRoot + "远魔专精.png");
            case "物理极限": return Load(TalentRoot + "物理专精.png");
            case "魔法极限": return Load(TalentRoot + "魔法专精.png");
            case "强化采集 II": return Load(TalentRoot + "战利品筛选.png");
            case "资源管理": return Load(TalentRoot + "点金之手.png");
            case "终极觉醒": return Load(TalentRoot + "觉醒.png");
            default:
                return Load(TalentRoot + displayName + ".png");
        }
    }

    static Sprite Load(string assetPath)
    {
#if UNITY_EDITOR
        var ed = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (ed != null) return ed;
#endif
        // 正式包需将图标放入 Resources；编辑器 Play 模式走 AssetDatabase。
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string folder = assetPath.Contains("属性") ? "UI/AttrIcons" : "UI/TalentIcons";
        var res = Resources.Load<Sprite>($"{folder}/{file}");
        if (res != null) return res;
        // 兼容未拷贝 Resources 时仍用文件名在 AttrIcons 根下找
        return Resources.Load<Sprite>($"UI/AttrIcons/{file}")
               ?? Resources.Load<Sprite>($"UI/TalentIcons/{file}");
    }
}
