#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Assets/Art/UI/Icons/EquipIcons 下图标绑定到 EquipTemplate.icon。
/// </summary>
public static class EquipIconBinder
{
    [MenuItem("Tools/_归档/装备/绑定 EquipIcons 到装备模板")]
    public static void BindAllTemplates()
    {
        string[] guids = AssetDatabase.FindAssets("t:EquipTemplate", new[] { "Assets/Resources/Config/Equips" });
        int bound = 0;
        int missing = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var tpl = AssetDatabase.LoadAssetAtPath<EquipTemplate>(path);
            if (tpl == null || string.IsNullOrEmpty(tpl.iconFileName)) continue;

            var sp = EquipIcons.Get(tpl.iconFileName);
            if (sp == null)
            {
                Debug.LogWarning($"[EquipIconBinder] 未找到图标: {tpl.iconFileName} ({tpl.templateId})");
                missing++;
                continue;
            }

            tpl.icon = sp;
            tpl.ApplyDefaultGridSize();
            EditorUtility.SetDirty(tpl);
            bound++;
        }

        AssetDatabase.SaveAssets();
        EquipIcons.ClearCache();
        EditorUtility.DisplayDialog("绑定完成",
            $"已绑定 {bound} 个装备模板。\n未找到图标 {missing} 个。\n请在 EquipTemplate 的 Icon File Name 填 EquipIcons 下文件名（不含 .png）。",
            "确定");
    }

    [MenuItem("Tools/_归档/装备/列出 EquipIcons 全部文件名")]
    public static void ListIconNames()
    {
        var names = EquipIcons.GetAllFileNames();
        Debug.Log("[EquipIcons] 共 " + names.Length + " 个：\n" + string.Join("\n", names));
    }
}
#endif
