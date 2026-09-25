#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按设计图生成酒馆「招募佣兵」三选一弹窗预制体。
/// 菜单：Tools/UI/生成招募佣兵弹窗预制体
/// 批处理：-executeMethod MercenaryRecruitPopupPrefabBuilder.BuildBatch
/// </summary>
public static class MercenaryRecruitPopupPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab";

    [MenuItem("Tools/UI/生成招募佣兵弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(PrefabPath))
        {
            if (!EditorUtility.DisplayDialog(
                    "招募佣兵弹窗",
                    "已存在 MercenaryRecruitPopup.prefab。\n是否按最新布局覆盖？\n（会丢掉你在预制体上的手改）",
                    "覆盖", "取消"))
                return;
        }
        BuildInternal(showDialog: true);
    }

    /// <summary>批处理入口：无确认弹窗，直接覆盖。</summary>
    public static void BuildBatch()
    {
        BuildInternal(showDialog: false);
    }

    static void BuildInternal(bool showDialog)
    {
        string dir = Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var root = new GameObject("MercenaryRecruitPopup", typeof(RectTransform));
        try
        {
            var ui = root.AddComponent<MercenaryRecruitPopupUI>();
            ui.BuildFallbackHierarchy();
            // 预制体默认关闭 Root，运行时 Show 再开
            if (ui.root != null) ui.root.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ok || prefab == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("招募佣兵弹窗", "保存失败，请看 Console。", "好");
                Debug.LogError("[MercenaryRecruitPopup] 预制体保存失败: " + PrefabPath);
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[MercenaryRecruitPopup] 已生成 " + PrefabPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("完成",
                    "已生成：\n" + PrefabPath +
                    "\n\n节点：Title / Subtitle / Card0~2 / RefreshButton / ConfirmButton / SkipAnim\n" +
                    "可在 Inspector 替换底图与立绘。",
                    "好");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
