using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成「战斗掉落装备弹窗」预制体到 Resources/Prefabs/Battle/EquipDropPopup.prefab。
/// 结构与运行时 EquipDropPopupUI.BuildHierarchy 完全一致，生成后可以在 Inspector 里换美术图。
/// 已存在时必须弹窗确认，绝不静默覆盖。
/// </summary>
public static class EquipDropPopupPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Battle/EquipDropPopup.prefab";

    [MenuItem("Tools/_归档/UI/生成掉落装备弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("掉落装备弹窗",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里替换过的美术图和调过的位置。\n确定要重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));

        var host = new GameObject("EquipDropPopup");
        try
        {
            EquipDropPopupUI.BuildHierarchy(host);
            host.AddComponent<EquipDropPopupUI>();

            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                EditorUtility.DisplayDialog("掉落装备弹窗", "保存预制体失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            EditorUtility.DisplayDialog("掉落装备弹窗",
                "已生成：\n" + PrefabAssetPath +
                "\n\n三种形态由代码切换，节点都在同一个预制体里：\n" +
                "· 身上没装备 → Card0 + PrimaryButton「装备」/ SecondaryButton「放入背包」\n" +
                "· 已有该部位 → 显示 ComparePanel，按钮变「替换」/「丢弃」\n" +
                "· 宝箱三选一 → Card0/1/2 全开，点卡片切换 SelectedMark\n\n" +
                "换美术：直接把图拖到 Panel / Card* / 各 Button 的 Image 上。",
                "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/_归档/UI/检查掉落装备弹窗预制体")]
    public static void Inspect()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (go == null)
        {
            EditorUtility.DisplayDialog("掉落装备弹窗",
                "还没有预制体。\n运行时会用代码搭一个临时的，功能正常但没有美术图。\n\n" +
                "点「Tools/UI/生成掉落装备弹窗预制体」生成后即可替换美术。", "好的");
            return;
        }
        var ui = go.GetComponent<EquipDropPopupUI>();
        EditorUtility.DisplayDialog("掉落装备弹窗",
            "预制体存在：" + PrefabAssetPath +
            "\n组件 EquipDropPopupUI：" + (ui != null ? "已挂" : "缺失（需重新生成）"),
            "好的");
    }
}
