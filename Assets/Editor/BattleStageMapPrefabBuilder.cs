using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成战斗内「石墩关卡图」预制体：Resources/Prefabs/Battle/BattleStageMap.prefab
/// 已存在时弹窗确认，绝不静默覆盖。
/// </summary>
public static class BattleStageMapPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Battle/BattleStageMap.prefab";

    [MenuItem("Tools/_归档/UI/生成战斗关卡石墩界面")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("战斗关卡石墩界面",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里换过的图和位置。\n确定要重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));

        var host = new GameObject("BattleStageMap", typeof(RectTransform));
        try
        {
            var canvas = host.AddComponent<Canvas>();
            UICanvasSetup.Apply(canvas);
            canvas.sortingOrder = 880;
            host.AddComponent<GraphicRaycaster>();

            BattleStageMapUI.BuildHierarchy(host);

            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                EditorUtility.DisplayDialog("战斗关卡石墩界面", "保存失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            EditorUtility.DisplayDialog("战斗关卡石墩界面",
                "已生成：\n" + PrefabAssetPath +
                "\n\n流程：进图 → 锁晃两下消失 → 滚盘 → 旗+btn06落石墩 → 点击\n" +
                "三态：\n" +
                "· Banner/Icon —— 当前可打（彩色旗）\n" +
                "· ClearedMark —— 已通关灰旗\n" +
                "· Lock —— 未解锁；当前关解锁动画播在这里\n" +
                "Pedestal_0 在最下面。Highlight 已不需要。\n" +
                "btn06 拖到组件 currentOutlineMaterial，或用 Resources/Materials/btn06。",
                "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/_归档/UI/生成锻造附魔关弹窗")]
    public static void BuildCraftPopups()
    {
        BuildOne("Assets/Resources/Prefabs/Battle/ForgeStagePopup.prefab", CraftStagePopupUI.Kind.Forge);
        BuildOne("Assets/Resources/Prefabs/Battle/EnchantStagePopup.prefab", CraftStagePopupUI.Kind.Enchant);
    }

    static void BuildOne(string path, CraftStagePopupUI.Kind kind)
    {
        if (File.Exists(path))
        {
            bool ok = EditorUtility.DisplayDialog("锻造/附魔弹窗",
                "已存在：\n" + path + "\n覆盖？", "覆盖", "跳过");
            if (!ok) return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string name = kind == CraftStagePopupUI.Kind.Forge ? "ForgeStagePopup" : "EnchantStagePopup";
        var host = new GameObject(name, typeof(RectTransform));
        try
        {
            var canvas = host.AddComponent<Canvas>();
            UICanvasSetup.Apply(canvas);
            canvas.sortingOrder = 920;
            host.AddComponent<GraphicRaycaster>();
            CraftStagePopupUI.BuildHierarchy(host, kind);
            PrefabUtility.SaveAsPrefabAsset(host, path);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("锻造/附魔弹窗", "已处理：\n" + path, "好的");
    }
}
