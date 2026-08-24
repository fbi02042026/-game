#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 微信小游戏常用竖版分辨率：一键加入 / 切换 Game 视图。
/// 菜单：Tools/分辨率测试
/// </summary>
public static class PortraitResolutionTool
{
    struct Res
    {
        public string name;
        public int w, h;
        public Res(string n, int width, int height) { name = n; w = width; h = height; }
    }

    static readonly Res[] PortraitSizes =
    {
        new Res("设计稿 720x1280", 720, 1280),
        new Res("iPhone SE/8 750x1334", 750, 1334),
        new Res("1080x1920 FHD", 1080, 1920),
        new Res("全面屏 1080x2340", 1080, 2340),
        new Res("iPhone 12/13 1170x2532", 1170, 2532),
        new Res("iPhone 14 Pro Max 1290x2796", 1290, 2796),
        new Res("安卓常见 720x1600", 720, 1600),
        new Res("安卓常见 1080x2400", 1080, 2400),
    };

    [MenuItem("Tools/_归档/分辨率测试/注册全部竖版分辨率")]
    public static void RegisterAll()
    {
        int added = 0;
        for (int i = 0; i < PortraitSizes.Length; i++)
        {
            if (AddCustomSize(PortraitSizes[i].w, PortraitSizes[i].h, PortraitSizes[i].name))
                added++;
        }
        EditorUtility.DisplayDialog("分辨率",
            $"已注册/确认 {PortraitSizes.Length} 个竖版分辨率（新增 {added}）\n" +
            "在 Game 视图左上角切换。\n" +
            "运行时会自动：更高机型 Match Width + 放大相机，避免左右裁切。",
            "OK");
    }

    [MenuItem("Tools/_归档/分辨率测试/切换到 720x1280 设计稿")]
    public static void SwitchDesign() => SwitchTo(720, 1280);

    [MenuItem("Tools/_归档/分辨率测试/切换到 750x1334")]
    public static void Switch750() => SwitchTo(750, 1334);

    [MenuItem("Tools/_归档/分辨率测试/切换到 1080x1920")]
    public static void Switch1080() => SwitchTo(1080, 1920);

    [MenuItem("Tools/_归档/分辨率测试/切换到 1080x2340")]
    public static void Switch2340() => SwitchTo(1080, 2340);

    [MenuItem("Tools/_归档/分辨率测试/切换到 1170x2532")]
    public static void Switch1170() => SwitchTo(1170, 2532);

    [MenuItem("Tools/_归档/分辨率测试/切换到 720x1600")]
    public static void Switch720_1600() => SwitchTo(720, 1600);

    [MenuItem("Tools/_归档/分辨率测试/切换到 1080x2400")]
    public static void Switch1080_2400() => SwitchTo(1080, 2400);

    static void SwitchTo(int w, int h)
    {
        AddCustomSize(w, h, $"{w}x{h}");
        int idx = FindSizeIndex(w, h);
        if (idx < 0)
        {
            Debug.LogWarning($"[分辨率] 未找到 {w}x{h}，请先执行「注册全部竖版分辨率」");
            return;
        }
        SetGameViewSize(idx);
        Debug.Log($"[分辨率] Game 视图 → {w}x{h}");
    }

    static bool AddCustomSize(int width, int height, string text)
    {
        if (FindSizeIndex(width, height) >= 0) return false;

        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var instanceProp = singletonType.GetProperty("instance");
        var gameViewSizes = instanceProp.GetValue(null, null);
        var getGroup = sizesType.GetMethod("GetGroup");
        // 0 = Standalone
        object group = getGroup.Invoke(gameViewSizes, new object[] { 0 });

        var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
        var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
        // FixedResolution = 1
        object sizeType = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");
        var ctor = gameViewSizeType.GetConstructor(new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
        object newSize = ctor.Invoke(new object[] { sizeType, width, height, text });

        var addCustomSize = group.GetType().GetMethod("AddCustomSize");
        addCustomSize.Invoke(group, new[] { newSize });
        return true;
    }

    static int FindSizeIndex(int width, int height)
    {
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var instanceProp = singletonType.GetProperty("instance");
        var gameViewSizes = instanceProp.GetValue(null, null);
        var getGroup = sizesType.GetMethod("GetGroup");
        object group = getGroup.Invoke(gameViewSizes, new object[] { 0 });

        var getTotalCount = group.GetType().GetMethod("GetTotalCount");
        int count = (int)getTotalCount.Invoke(group, null);
        var getGameViewSize = group.GetType().GetMethod("GetGameViewSize");
        var widthProp = getGameViewSize.ReturnType.GetProperty("width");
        var heightProp = getGameViewSize.ReturnType.GetProperty("height");

        for (int i = 0; i < count; i++)
        {
            object size = getGameViewSize.Invoke(group, new object[] { i });
            int w = (int)widthProp.GetValue(size, null);
            int h = (int)heightProp.GetValue(size, null);
            if (w == width && h == height) return i;
        }
        return -1;
    }

    static void SetGameViewSize(int index)
    {
        var gvWndType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var gvWnd = EditorWindow.GetWindow(gvWndType);
        var selectedSizeIndexProp = gvWndType.GetProperty("selectedSizeIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        selectedSizeIndexProp.SetValue(gvWnd, index, null);
        gvWnd.Repaint();
    }
}
#endif
