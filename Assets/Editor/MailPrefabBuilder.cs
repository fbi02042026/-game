using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 邮件 UI 预制体生成器（2026-10-16 T4，主人拍板「按照我的效果图生成预制体」）。
///
/// 一次生成两个预制体（菜单：Tools/UI/生成邮件预制体（列表窗+详情窗））：
/// · Assets/Resources/Prefabs/UI/MailListWindow.prefab   —— 列表窗（<see cref="MailListUI"/> 运行时加载）
/// · Assets/Resources/Prefabs/UI/MailDetailWindow.prefab —— 信件详情羊皮纸窗（<see cref="MailDetailUI"/> 运行时加载）
///
/// 口径（与 DraftOddsPopupPrefabBuilder 同一套尺子）：
/// · 根 = Canvas(Overlay + sortingOrder) + GraphicRaycaster + 业务脚本 —— 运行时 Ensure→Build 里
///   再由 UICanvasSetup.ApplyPopup 统一套成摄像机模式（builder 里绝不碰相机，编辑模式禁 DDOL）。
/// · 节点几何 = 当前效果图对位值（底框 636×949 / 羊皮纸 720×965）；生成后**主人可直接在编辑器里调**，
///   运行时代码对预制体分支只绑定不覆盖几何（MailListUI/MailDetailUI.Build 双模式）。
/// · 节点名全 ASCII，与运行时 BindFromPrefab 按名绑定一一对应，改名必须两边同步。
/// · 行（邮件列表项）与贴纸是动态的，预制体不放模板，运行时代码克隆。
///
/// ⚠ 邮件美术是 56 字符 guid（2026-09-24 口径：拖不进 prefab 会丢 m_Sprite）。程序化序列化
///   是否丢未验证过 → 生成后回读实例自检 sprite 引用，丢了当场报错（届时按 09-24 口径点对点换发 hex32）。
/// </summary>
public static class MailPrefabBuilder
{
    // ===== 出入口（与运行时 MailListUI/MailDetailUI 的 PrefabPath 一致）=====
    const string ListPrefabPath = "Assets/Resources/Prefabs/UI/MailListWindow.prefab";
    const string DetailPrefabPath = "Assets/Resources/Prefabs/UI/MailDetailWindow.prefab";

    // ===== 美术资产（全部引用，不复制不新建）=====
    const string ArtDir = "Assets/Resources/Art/UI/邮件";
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";

    const string SpriteWindow = ArtDir + "/_0010_底框.png";            // 636×949
    const string SpriteClose = ArtDir + "/_0005_关闭.png";             // 88×90 自带 X
    const string SpriteBtnClaimAll = ArtDir + "/_0006_一键领取.png";    // 199×65 无字橙框
    const string SpriteBtnDeleteRead = ArtDir + "/_0007_删除已读.png";  // 200×66 无字蓝框
    const string SpritePaper = ArtDir + "/_0000s_0002_信件.png";        // 720×965 羊皮纸
    const string SpriteStamp = ArtDir + "/_0000_已领取.png";            // 98×45 印章条

    // ===== 配色（与 MailListUI/MailDetailUI 运行时常量一致）=====
    static readonly Color ColDim = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color ColTitleBlue = new Color(0.95f, 0.93f, 0.88f, 1f);
    static readonly Color ColInk = new Color(0.30f, 0.19f, 0.10f, 1f);
    static readonly Color ColInkDim = new Color(0.47f, 0.36f, 0.24f, 1f);
    static readonly Color ColBtnTextOrange = new Color(0.42f, 0.22f, 0.06f, 1f);
    static readonly Color ColBtnTextBlue = new Color(0.95f, 0.93f, 0.88f, 1f);
    static readonly Color ColBtnPaper = new Color(0.55f, 0.38f, 0.20f, 1f);

    [MenuItem("Tools/UI/生成邮件预制体（列表窗+详情窗）")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError("[MailPrefab] 找不到字体 " + FontPath + "，停止生成");
            return;
        }
        var spWindow = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteWindow);
        var spClose = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteClose);
        var spClaim = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteBtnClaimAll);
        var spDelete = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteBtnDeleteRead);
        var spPaper = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePaper);
        var spStamp = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteStamp);
        if (spWindow == null || spClose == null || spClaim == null || spDelete == null || spPaper == null || spStamp == null)
        {
            Debug.LogError("[MailPrefab] 邮件美术缺失（Art/UI/邮件/），停止生成");
            return;
        }

        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/UI");

        BuildListWindow(font, spWindow, spClose, spClaim, spDelete);
        BuildDetailWindow(font, spPaper, spClose, spStamp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MailPrefab] 两个邮件预制体已生成：" + ListPrefabPath + " / " + DetailPrefabPath);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 列表窗（底框 636×949；节点名与 MailListUI.BindFromPrefab 一一对应）
    // ────────────────────────────────────────────────────────────────────────

    static void BuildListWindow(Font font, Sprite spWindow, Sprite spClose, Sprite spClaim, Sprite spDelete)
    {
        var go = new GameObject("MailListWindow", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;   // 运行时 ApplyPopup 统一套摄像机模式
        canvas.overrideSorting = true;
        canvas.sortingOrder = 600;
        go.AddComponent<MailListUI>();

        var root = Node("Root", go.transform);
        Stretch(root);

        // 遮罩：点它关闭
        var dim = Node("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = ColDim;
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        // 窗体底框
        var window = Node("Window", root);
        SetCenter(window, Vector2.zero, new Vector2(636f, 949f));
        var winImg = window.gameObject.AddComponent<Image>();
        winImg.sprite = spWindow;
        winImg.raycastTarget = true;
        winImg.preserveAspect = true;

        // 标题（顶部蓝边带；左上信封是底图自带装饰）
        var title = Node("Title", window);
        SetCenter(title, new Vector2(0f, 430f), new Vector2(400f, 56f));
        var titleText = MakeText(title.gameObject, font, 34, ColTitleBlue, TextAnchor.MiddleCenter);
        titleText.text = "邮件";
        titleText.fontStyle = FontStyle.Bold;

        // 关闭按钮（自带 X）
        var close = Node("BtnClose", window);
        SetCenter(close, new Vector2(250f, 424f), new Vector2(64f, 65f));
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.sprite = spClose;
        closeImg.preserveAspect = true;
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.transition = Selectable.Transition.ColorTint;

        // 列表滚动区（对齐底图羊皮纸 y≈-300~+380；背景透明，羊皮纸透出来）
        var list = Node("MailList", window);
        SetCenter(list, new Vector2(0f, 36f), new Vector2(576f, 640f));
        var listImg = list.gameObject.AddComponent<Image>();
        listImg.color = Color.clear;
        listImg.raycastTarget = true;
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var viewport = Node("Viewport", list);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Node("Content", viewport);
        SetTopStretch(content);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 10f);

        scroll.viewport = viewport;
        scroll.content = content;

        // 空态
        var empty = Node("EmptyText", window);
        SetCenter(empty, new Vector2(0f, 40f), new Vector2(560f, 60f));
        var emptyText = MakeText(empty.gameObject, font, 26, ColInkDim, TextAnchor.MiddleCenter);
        emptyText.text = "暂无邮件";
        empty.gameObject.SetActive(false);

        // 底部按钮（无字空框 + 叠字 Label）
        BuildIconButton(window, "ClaimAllButton", spClaim, new Vector2(-145f, -390f), new Vector2(200f, 66f),
            "一键领取", ColBtnTextOrange, font);
        BuildIconButton(window, "DeleteReadButton", spDelete, new Vector2(145f, -390f), new Vector2(200f, 66f),
            "删除已读", ColBtnTextBlue, font);

        GameFonts.ApplyToHierarchy(go.transform);
        SaveAndVerify(go, ListPrefabPath, "Window", winImg);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 详情窗（羊皮纸 720×965；节点名与 MailDetailUI.BindFromPrefab 一一对应）
    // ────────────────────────────────────────────────────────────────────────

    static void BuildDetailWindow(Font font, Sprite spPaper, Sprite spClose, Sprite spStamp)
    {
        var go = new GameObject("MailDetailWindow", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 610;
        go.AddComponent<MailDetailUI>();

        var root = Node("Root", go.transform);
        Stretch(root);

        var dim = Node("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = ColDim;
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        var paper = Node("PaperWindow", root);
        SetCenter(paper, Vector2.zero, new Vector2(720f, 965f));
        var paperImg = paper.gameObject.AddComponent<Image>();
        paperImg.sprite = spPaper;
        paperImg.raycastTarget = true;
        paperImg.preserveAspect = true;

        // 标题 / 发件人
        var title = Node("TitleText", paper);
        SetCenter(title, new Vector2(0f, 368f), new Vector2(620f, 50f));
        var titleText = MakeText(title.gameObject, font, 30, ColInk, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;

        var sender = Node("SenderText", paper);
        SetCenter(sender, new Vector2(0f, 324f), new Vector2(620f, 28f));
        var senderText = MakeText(sender.gameObject, font, 20, ColInkDim, TextAnchor.MiddleLeft);
        senderText.text = "发件人：";

        // 正文
        var body = Node("BodyText", paper);
        SetCenter(body, new Vector2(0f, 190f), new Vector2(620f, 240f));
        var bodyText = MakeText(body.gameObject, font, 22, ColInk, TextAnchor.UpperLeft);
        bodyText.text = "正文";

        // 贴纸行（正文下方横排，动态生成贴纸）
        var stickerRow = Node("StickerRow", paper);
        SetCenter(stickerRow, new Vector2(-300f, 16f), new Vector2(600f, 88f));
        stickerRow.pivot = new Vector2(0f, 0.5f);
        stickerRow.gameObject.SetActive(false);

        // 配图（detailImagePath / 附件 iconOverride 共用图位）
        var detailImage = Node("DetailImage", paper);
        SetCenter(detailImage, new Vector2(0f, -120f), new Vector2(380f, 230f));
        var dImg = detailImage.gameObject.AddComponent<Image>();
        dImg.preserveAspect = true;
        dImg.raycastTarget = false;
        detailImage.gameObject.SetActive(false);

        // 附件清单
        var reward = Node("RewardText", paper);
        SetCenter(reward, new Vector2(0f, -330f), new Vector2(620f, 120f));
        var rewardText = MakeText(reward.gameObject, font, 22, ColInk, TextAnchor.UpperLeft);
        rewardText.text = "附件：";

        // 底部单按钮（运行时切 确定/领取/已领取）
        var action = Node("ActionButton", paper);
        SetCenter(action, new Vector2(0f, -425f), new Vector2(220f, 70f));
        var actionImg = action.gameObject.AddComponent<Image>();
        actionImg.color = ColBtnPaper;
        var actionBtn = action.gameObject.AddComponent<Button>();
        actionBtn.targetGraphic = actionImg;
        var actionLabelGo = Node("Label", action);
        Stretch(actionLabelGo);
        var actionLabel = MakeText(actionLabelGo.gameObject, font, 28, Color.white, TextAnchor.MiddleCenter);
        actionLabel.text = "确定";

        // 已领取印章
        var stamp = Node("StampClaimed", paper);
        SetCenter(stamp, new Vector2(-165f, -425f), new Vector2(80f, 80f));
        var stampImg = stamp.gameObject.AddComponent<Image>();
        stampImg.sprite = spStamp;
        stampImg.preserveAspect = true;
        stampImg.raycastTarget = false;
        stamp.gameObject.SetActive(false);

        // 关闭按钮
        var close = Node("BtnClose", paper);
        SetCenter(close, new Vector2(295f, 425f), new Vector2(64f, 65f));
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.sprite = spClose;
        closeImg.preserveAspect = true;
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.transition = Selectable.Transition.ColorTint;

        GameFonts.ApplyToHierarchy(go.transform);
        SaveAndVerify(go, DetailPrefabPath, "PaperWindow", paperImg);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 保存 + 56 字符 guid 序列化自检
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>存盘后回读实例，检查关键 Image 的 sprite 引用还在不在（56 字符 guid 若序列化丢失当场报错）。</summary>
    static void SaveAndVerify(GameObject go, string prefabPath, string windowName, Image keyImage)
    {
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (loaded == null)
        {
            Debug.LogError("[MailPrefab] 保存后回读失败：" + prefabPath);
            return;
        }
        Image verify = null;
        FindImageByName(loaded.transform, windowName, ref verify);
        if (verify == null || verify.sprite == null)
        {
            Debug.LogError("[MailPrefab] 回读后 " + windowName + " 的 sprite 引用为空——邮件美术是 56 字符 guid，" +
                           "序列化进 prefab 丢失（2026-09-24 已知坑）。按口径给 Art/UI/邮件/ 点对点换发 hex32 后重跑本菜单。" + prefabPath);
        }
        else
        {
            Debug.Log("[MailPrefab] 回读自检 OK：" + windowName + " sprite 引用正常（" + prefabPath + "）");
        }
    }

    static void FindImageByName(Transform t, string name, ref Image found)
    {
        if (found != null) return;
        if (t.name == name)
        {
            var img = t.GetComponent<Image>();
            if (img != null) { found = img; return; }
        }
        for (int i = 0; i < t.childCount; i++)
            FindImageByName(t.GetChild(i), name, ref found);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 小工具（与 DraftOddsPopupPrefabBuilder 同一把尺子）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>带底图 + 可选文字 Label 的整图按钮（_0006_/_0007_ 是无字空框，文字运行时/预制体都叠在 Label 上）。</summary>
    static void BuildIconButton(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size,
        string label, Color labelColor, Font font)
    {
        var go = Node(name, parent);
        SetCenter(go, pos, size);
        var img = go.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        var btn = go.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var labelGo = Node("Label", go);
        Stretch(labelGo);
        var t = MakeText(labelGo.gameObject, font, 26, labelColor, TextAnchor.MiddleCenter);
        t.text = label;
    }

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text MakeText(GameObject go, Font font, int size, Color color, TextAnchor align)
    {
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>中心锚点 + 固定尺寸（与运行时布局同一把尺子：y 正=上）。</summary>
    static void SetCenter(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetTopStretch(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int idx = path.LastIndexOf('/');
        string parent = path.Substring(0, idx);
        string folder = path.Substring(idx + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
