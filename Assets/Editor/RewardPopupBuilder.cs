#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用「获得奖励」弹窗预制体生成器（2026-09-23）。
/// ⚠ 手动触发：菜单 Tools/奖励弹窗/生成预制体。没有 [InitializeOnLoadMethod]，不会自动跑。
///
/// 素材：Assets/Art/UI/奖励弹窗/（**主人自己替换，保持下面四个文件名即可**）
///   - FILE_PANEL  面板底 → 九宫格（BORDER_PANEL），Image.type=Sliced，拉高时边框不变形
///   - FILE_CELL   奖励格 → 九宫格（BORDER_CELL），模板格子
///   - FILE_TITLE  顶部标题（皇冠 + 获得奖励字 + 旗帜），原样显示不拉伸
///   - FILE_BTN    确定按钮（图自带「确定」二字，代码不加 Text）
/// 找不到素材时 Image 留空槽（不报错），主人在 Inspector 里拖也行。
///
/// ⚠ 换图后注意：spriteBorder 是按**当前图的像素尺寸**定的，换了尺寸不同的图要改 BORDER_*。
///
/// 节点名 = RewardPopupUI.BuildFromPrefab 的绑定契约：Panel/Grid/CellTemplate/ConfirmBtn/Title。
/// </summary>
public static class RewardPopupBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/RewardPopup.prefab";
    const string ArtDir = "Assets/Art/UI/奖励弹窗";

    // ---- 素材文件名（换图覆盖同名文件即可，别改名）----
    const string FILE_PANEL = "奖励弹窗_底板.png";
    const string FILE_CELL = "奖励弹窗_格子.png";
    const string FILE_TITLE = "奖励弹窗_标题.png";
    const string FILE_BTN = "奖励弹窗_按钮.png";

    // ---- 九宫格边框（像素，相对各自原图；Vector4: x=左 y=下 z=右 w=上）----
    static readonly Vector4 BORDER_PANEL = new Vector4(70, 62, 70, 62);   // 按 982×347 定的
    static readonly Vector4 BORDER_CELL = new Vector4(52, 52, 52, 52);    // 按 240×222 定的

    // 与 RewardPopupUI 的常量保持一致
    const float ART_SCALE = 0.55f;
    const float PANEL_W = 540f;
    const float PANEL_H = 420f;         // 只是预制体里的初始高度，运行时按奖励数量改
    const float TITLE_H = 160f;
    const float CELL_W = 96f;           // 与 RewardPopupUI 一排 5 个的排版保持一致
    const float CELL_H = 88f;
    const float LABEL_H = 30f;

    [MenuItem("Tools/奖励弹窗/生成预制体")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("生成奖励弹窗预制体",
            "将覆盖重建 " + PrefabPath, "生成", "取消"))
            return;

        var log = new StringBuilder();

        // ---- 1. 先把素材设为 Sprite 并设九宫格 border ----
        SetSprite(ArtDir + "/" + FILE_PANEL, BORDER_PANEL, log);
        SetSprite(ArtDir + "/" + FILE_CELL, BORDER_CELL, log);
        SetSprite(ArtDir + "/" + FILE_TITLE, Vector4.zero, log);
        SetSprite(ArtDir + "/" + FILE_BTN, Vector4.zero, log);
        AssetDatabase.Refresh();

        // ---- 2. 建树 ----
        var root = new GameObject("RewardPopup", typeof(RectTransform));
        string report;
        try
        {
            BuildTree(root, log);
            report = log.ToString();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Debug.Log("[RewardPopupBuilder] 生成完成 → " + PrefabPath + "\n" + report,
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        EditorUtility.DisplayDialog("奖励弹窗", "生成完成（详见 Console）", "好");
    }

    static void BuildTree(GameObject root, StringBuilder log)
    {
        root.layer = 5;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 900;                        // = GameConfig.UiSort.TownPopup

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // 全屏透明遮罩：挡住下层点击（点空白不关闭，防误触）
        var mask = Node(root, "Mask", V(0, 0), V(1, 1), V(0, 0), V(0, 0), V(0.5f, 0.5f));
        var maskImg = Img(mask.gameObject, new Color(0f, 0f, 0f, 0.55f), null);
        maskImg.raycastTarget = true;

        // 面板（九宫格底板）
        var panel = Node(root, "Panel", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 0f), V(PANEL_W, PANEL_H), V(0.5f, 0.5f));
        var pImg = Img(panel.gameObject, Color.white, Art(log, FILE_PANEL));
        pImg.type = Image.Type.Sliced;                    // ★ 九宫格拉伸
        pImg.raycastTarget = true;                        // 挡住穿透

        // 标题（压在面板上沿）
        var title = Node(panel, "Title", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 158f), V(835f * ART_SCALE, TITLE_H), V(0.5f, 0.5f));
        Img(title.gameObject, Color.white, Art(log, FILE_TITLE));

        // 格子容器（动态生成，运行时清空重铺）
        var grid = Node(panel, "Grid", V(0.5f, 1f), V(0.5f, 1f), V(0f, 0f), V(PANEL_W - 40f, 0f), V(0.5f, 1f));

        // 确定按钮（图自带「确定」二字）
        var btn = Node(panel, "ConfirmBtn", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, -PANEL_H / 2f + 52f), V(404f * ART_SCALE, 84f * ART_SCALE), V(0.5f, 0.5f));
        var bImg = Img(btn.gameObject, Color.white, Art(log, FILE_BTN));
        bImg.raycastTarget = true;
        var button = btn.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;

        // 模板格子（根下，默认隐藏；运行时 Instantiate 到 Grid）
        var tpl = Node(root, "CellTemplate", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 0f), V(CELL_W, CELL_H), V(0.5f, 0.5f));
        Img(tpl.gameObject, Color.white, Art(log, FILE_CELL));
        var icon = Node(tpl, "Icon", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 10f), V(62f, 62f), V(0.5f, 0.5f));
        Img(icon.gameObject, new Color(1f, 1f, 1f, 0.12f), null);
        var label = Node(tpl, "Label", V(0.5f, 0f), V(0.5f, 0f), V(0f, -4f), V(100f, LABEL_H), V(0.5f, 0f));
        Txt(label.gameObject, "奖励名 ×N", 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.88f, 1f));
        tpl.gameObject.SetActive(false);
    }

    // ---------- 工具（与 DailyLoginFullBuilder 同款） ----------

    static void SetSprite(string path, Vector4 border, StringBuilder log)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
        {
            log.AppendLine("⚠ 找不到素材: " + path);
            return;
        }
        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (imp.spriteBorder != border)
        {
            imp.spriteBorder = border;                    // Vector4: x=左 y=下 z=右 w=上
            dirty = true;
        }
        if (dirty) imp.SaveAndReimport();
    }

    static Sprite Art(StringBuilder log, string file)
    {
        var p = ArtDir + "/" + file;
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (s == null && log != null)
            log.AppendLine("⚠ 找不到 Sprite: " + p);
        return s;
    }

    static RectTransform Node(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size; rt.pivot = pivot;
        return rt;
    }

    static RectTransform Node(GameObject parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
        => Node(parent != null ? parent.transform : null, name, aMin, aMax, pos, size, pivot);

    static Image Img(GameObject go, Color c, Sprite sp)
    {
        var img = go.AddComponent<Image>();
        img.color = c;
        img.sprite = sp;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(GameObject go, string s, int size, TextAnchor align, Color c)
    {
        var t = go.AddComponent<Text>();
        t.text = s;
        t.fontSize = size;
        t.alignment = align;
        t.color = c;
        t.raycastTarget = false;
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        t.font = f;   // 运行时 GameFonts.ApplyToHierarchy 统一刷
        return t;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);
}
#endif
