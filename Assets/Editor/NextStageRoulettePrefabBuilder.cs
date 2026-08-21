using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成「随机滚动关卡弹窗」预制体到 Resources/Prefabs/Battle/NextStageRoulette.prefab。
/// 已存在时弹窗确认，绝不静默覆盖。
/// </summary>
public static class NextStageRoulettePrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Battle/NextStageRoulette.prefab";

    [MenuItem("Tools/UI/生成随机滚动关卡弹窗")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("随机滚动关卡弹窗",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里替换过的美术图和调过的位置。\n确定要重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));

        var host = new GameObject("NextStageRoulette", typeof(RectTransform));
        try
        {
            var canvas = host.AddComponent<Canvas>();
            UICanvasSetup.Apply(canvas);
            canvas.sortingOrder = 900;
            host.AddComponent<GraphicRaycaster>();

            NextStageRouletteUI.BuildHierarchy(host);

            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                EditorUtility.DisplayDialog("随机滚动关卡弹窗", "保存预制体失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            EditorUtility.DisplayDialog("随机滚动关卡弹窗",
                "已生成：\n" + PrefabAssetPath +
                "\n\n换美术：\n" +
                "· Backdrop / Shade —— 背景与压暗\n" +
                "· ReelViewport / Content / Card_* —— 滚动条与关卡卡\n" +
                "· Card_*/Icon —— 关卡图标\n" +
                "· StopButton / EnterButton —— 停止、进入按钮\n" +
                "· CenterFrame —— 中间选中框\n\n" +
                "关卡图标也可放到 Resources/Art/UI/StageIcons/\n" +
                "stage_normal / elite / rest / forge / enchant / boss",
                "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/UI/检查随机滚动关卡弹窗")]
    public static void Inspect()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (go == null)
        {
            EditorUtility.DisplayDialog("随机滚动关卡弹窗",
                "还没有预制体。\n运行时会用代码搭一个临时的。\n\n" +
                "点「Tools/UI/生成随机滚动关卡弹窗」生成后即可替换美术。", "好的");
            return;
        }
        var ui = go.GetComponent<NextStageRouletteUI>();
        EditorUtility.DisplayDialog("随机滚动关卡弹窗",
            "预制体存在：" + PrefabAssetPath +
            "\n组件 NextStageRouletteUI：" + (ui != null ? "已挂" : "缺失（需重新生成）"),
            "好的");
    }
}
