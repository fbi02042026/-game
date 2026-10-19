using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成冒险日志三期面板预制体（碎片合成 / 里程商店弹窗）：
/// Resources/Prefabs/Town/AdventureLogPhase3Panel.prefab
/// 层级由 AdventureLogPhase3Panel.BuildHierarchy 构建（单一来源）。
/// 已存在时弹窗确认，绝不静默覆盖；工程加载时若缺失会自动生成一次。
/// </summary>
[InitializeOnLoad]
public static class Phase3PanelPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Town/AdventureLogPhase3Panel.prefab";

    static Phase3PanelPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBakeIfMissing;
    }

    static void AutoBakeIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(PrefabAssetPath)) return;
        Build(silent: true);
    }

    [MenuItem("Tools/UI/生成三期面板预制体")]
    public static void BuildMenu()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("三期面板预制体",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里改过的样式和位置。\n确定要重新生成吗？（层级以 AdventureLogPhase3Panel.BuildHierarchy 为准）",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }
        Build(silent: false);
    }

    static void Build(bool silent)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));
        var host = new GameObject(AdventureLogPhase3Panel.RootName, typeof(RectTransform));
        try
        {
            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            AdventureLogPhase3Panel.BuildHierarchy(rt);

            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                Debug.LogError("[Phase3Panel] 预制体保存失败，请看 Console");
                if (!silent) EditorUtility.DisplayDialog("三期面板预制体", "保存失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log("[Phase3Panel] 已生成 " + PrefabAssetPath);
            if (!silent)
                EditorUtility.DisplayDialog("三期面板预制体",
                    "已生成：\n" + PrefabAssetPath +
                    "\n\n运行时 AdventureLogPhase3Panel 只加载这个预制体并按节点名绑定；\n改了层级结构后请重新执行本菜单覆盖生成。",
                    "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
