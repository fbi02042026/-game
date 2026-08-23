using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把装备模板的 spumName 对齐到 SPUM 精灵文件名（优先 iconFileName）。
/// 菜单：Tools/装备/补全 spumName（按图标文件名）
/// </summary>
public static class EquipSpumNameFiller
{
    [MenuItem("Tools/装备/补全 spumName（按图标文件名）")]
    public static void FillFromIconFileName()
    {
        var spumNames = CollectSpumSpriteNames();
        string[] guids = AssetDatabase.FindAssets("t:EquipTemplate", new[] { "Assets/Resources/Config/Equips" });
        int filled = 0, corrected = 0, skipped = 0, missing = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var tpl = AssetDatabase.LoadAssetAtPath<EquipTemplate>(path);
            if (tpl == null) continue;

            string icon = tpl.iconFileName;
            if (string.IsNullOrEmpty(icon))
            {
                skipped++;
                continue;
            }

            if (!spumNames.Contains(icon))
            {
                missing++;
                Debug.LogWarning($"[EquipSpum] 无对应 SPUM 精灵: {tpl.templateId} icon={icon}");
                continue;
            }

            if (tpl.spumName == icon)
            {
                skipped++;
                continue;
            }

            bool wasEmpty = string.IsNullOrEmpty(tpl.spumName);
            tpl.spumName = icon;
            EditorUtility.SetDirty(tpl);
            if (wasEmpty) filled++;
            else corrected++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("补全 spumName",
            $"新建 {filled}\n纠正 {corrected}\n跳过 {skipped}\n图标无 SPUM {missing}", "好的");
        Debug.Log($"[EquipSpum] done fill={filled} corrected={corrected} skip={skipped} miss={missing}");
    }

    static HashSet<string> CollectSpumSpriteNames()
    {
        var set = new HashSet<string>();
        string root = Path.Combine(Application.dataPath, "SPUM", "Resources", "Addons");
        if (!Directory.Exists(root)) return set;
        foreach (string file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
            set.Add(Path.GetFileNameWithoutExtension(file));
        return set;
    }
}
