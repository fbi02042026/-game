using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成「恢复关·生命恢复」弹窗预制体。
/// 路径：Resources/Prefabs/Battle/RestStagePopup.prefab
/// 已存在时弹窗确认，绝不静默覆盖。
/// </summary>
public static class RestStagePopupPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Battle/RestStagePopup.prefab";

    [MenuItem("Tools/UI/生成恢复关弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("恢复关弹窗",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里替换过的美术图和调过的位置。\n确定要重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));

        var host = new GameObject("RestStagePopup", typeof(RectTransform));
        try
        {
            var canvas = host.AddComponent<Canvas>();
            UICanvasSetup.Apply(canvas);
            canvas.sortingOrder = 920;
            host.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            RestStagePopupUI.BuildHierarchy(host);

            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                EditorUtility.DisplayDialog("恢复关弹窗", "保存预制体失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            EditorUtility.DisplayDialog("恢复关弹窗",
                "已生成：\n" + PrefabAssetPath +
                "\n\n换美术：\n" +
                "· Panel —— 外框\n" +
                "· Illustration —— 仙泉插画（也可放 Resources/Art/UI/RestStage/illustration）\n" +
                "· CloseButton / ContinueButton —— 关闭、继续冒险\n" +
                "· StatusIcon —— 状态小图标\n\n" +
                "效果：打开时英雄+佣兵各回复 50% 最大生命。",
                "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/UI/检查恢复关弹窗预制体")]
    public static void Inspect()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (go == null)
        {
            EditorUtility.DisplayDialog("恢复关弹窗",
                "还没有预制体。\n点「Tools/UI/生成恢复关弹窗预制体」生成后即可换美术。", "好的");
            return;
        }
        var ui = go.GetComponent<RestStagePopupUI>();
        EditorUtility.DisplayDialog("恢复关弹窗",
            "预制体存在：" + PrefabAssetPath +
            "\n组件 RestStagePopupUI：" + (ui != null ? "已挂" : "缺失"),
            "好的");
    }
}
