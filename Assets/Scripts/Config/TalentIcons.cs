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

    /// <summary>
    /// 2026-09-29：左列改 4 类，顺序 **体质 → 攻击 → 防御 → 攻击**（与 <c>TalentDefs.BuildLeft</c> 的
    /// names 数组一一对应，改顺序时两边必须同时改）。
    /// 2026-10-10：第 4 槽原是「智力 → 魔攻」，随左列取消智力、改成第二个「攻击」槽，
    /// 图标按主人要求统一换成物攻图（与第 2 槽同一张），不再出现「节点叫攻击却配魔攻图」。
    /// ⚠️ 角色面板「魔攻」行（<c>CharacterUI</c>）仍用魔攻图 —— 那一行显示的确实是魔攻数值，未随本次改动。
    /// </summary>
    static readonly string[] LeftAttrFiles =
    {
        "角色_0001s_0001_生命",   // 0 体质 → 生命
        "角色_0001s_0000_攻击",   // 1 力量 → 物攻
        "角色_0001s_0002_防御",   // 2 防御
        "角色_0001s_0000_攻击"    // 3 攻击（2026-10-10 主人拍板「全部换物攻的图标」：原「智力 → 魔攻」，
                                  //    左列取消智力后本槽已是第二个攻击槽，统一用物攻图，与第 2 槽同一张）
    };

    /// <summary>
    /// 2026-09-29：按属性图标文件名取图（供 CharacterUI 给「魔攻 / 魔防」行换图标用）。
    /// 走 <see cref="Load"/>，即优先 Resources/UI/AttrIcons —— 新图必须放一份进 Resources，
    /// 否则真机（<c>DeviceParity.EditorFallbackEnabled = false</c>）会加载不到、露白框。
    /// </summary>
    public static Sprite GetAttrSprite(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        return Load(AttrRoot + fileNameWithoutExt + ".png");
    }

    public static Sprite GetLeftAttr(int slot0to3)
    {
        if (slot0to3 < 0 || slot0to3 >= LeftAttrFiles.Length) return null;
        return Load(AttrRoot + LeftAttrFiles[slot0to3] + ".png");
    }

    /// <summary>按选项展示名取图标（与天赋图标文件夹文件名一致）。</summary>
    public static Sprite GetTalent(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            Debug.LogError("[TalentIcons] GetTalent 收到空的展示名，取不到天赋图标。");
            return null;
        }
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

            // 2026-09-29 主人指定映射（talent_right.csv 的 optNames → 图标文件名）：
            // R_JOB 六职业 → 三张职业专精图；一次性节点 → 敛财/扩容/快速休整/天赋共鸣。
            case "剑盾卫士": return Load(TalentRoot + "剑盾专精.png");   // 剑盾
            case "狂战士":                                              // 重兵
            case "重装":   return Load(TalentRoot + "重兵专精.png");
            case "游侠":                                                // 远魔
            case "法师":
            case "牧师":   return Load(TalentRoot + "远魔专精.png");
            case "背包扩容":  return Load(TalentRoot + "敛财.png");
            case "技能槽 IV": return Load(TalentRoot + "扩容.png");
            case "双修解锁":  return Load(TalentRoot + "快速休整.png");
            case "精英猎手":  return Load(TalentRoot + "天赋共鸣.png");
            // 2026-09-29：R_SLOT/R_DUAL 换节点后补的图（复用图标目录里已有的两张）。
            case "致命一击":  return Load(TalentRoot + "弱点洞察.png");
            case "技能精通":  return Load(TalentRoot + "力量爆发.png");
            // 2026-09-29：进关抽奖相关两个新节点。幸运提升有同名图会走 default；
            // 初始资金暂时复用「点金之手」，等主人出专属图再换。
            case "初始资金":  return Load(TalentRoot + "点金之手.png");
            // 利刃 / 体魄 / 精准 / 迅捷 / 疾行 / 凝神：图标与节点同名，走下边 default 直取。

            default:
                // 2026-09-29：右列 V4 重制后节点名换了一批，图标目录里大多还是旧版名字 →
                // 大量节点取不到图。按主人口径「不要兜底」：取不到就报错，不静默露白框。
                var s = Load(TalentRoot + displayName + ".png");
                if (s == null)
                    Debug.LogError($"[TalentIcons] 天赋图标缺失：Resources/UI/TalentIcons/{displayName}.png " +
                                   "（右列节点名与图标文件名对不上，请补图或改节点名）。");
                return s;
        }
    }

    static Sprite Load(string assetPath)
    {
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string folder = assetPath.Contains("属性") ? "UI/AttrIcons" : "UI/TalentIcons";
        // 优先 Resources（真机与编辑器一致），避免只靠 AssetDatabase 时图标全是模板默认旋涡
        var res = Resources.Load<Sprite>($"{folder}/{file}");
        if (res != null) return res;
        res = Resources.Load<Sprite>($"UI/AttrIcons/{file}")
              ?? Resources.Load<Sprite>($"UI/TalentIcons/{file}");
        if (res != null) return res;
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            var ed = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (ed != null) return ed;
        }
#endif
        return null;
    }
}
