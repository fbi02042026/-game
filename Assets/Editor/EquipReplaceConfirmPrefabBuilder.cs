using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-06 主人拍板】「换上新装备吗」替换装备确认弹窗 —— <b>预制体生成器</b>。
///
/// <para>主人原话：「用项目里的资源把替换装备的那个弹窗拼出来然后生成个预制体，
/// 把"换上新装备吗"那个字放到弹窗的上方 不要挡住弹窗」。</para>
///
/// <b>资源口径全部取自项目现有美术</b>（照抄主人已调好的
/// <c>Resources/Prefabs/Battle/EvacuateConfirmPopup</c>，不自己造图、不加新素材）：
/// · Panel 底图 = <c>Art/UI/NavCharacter/角色_0002s_0010_图层-7.png</c>（Image.Type.Sliced，a=0.98）<br/>
/// · 「换上新的」= <c>Art/UI/Common/确定按钮.png</c><br/>
/// · 「留着旧的」= <c>Art/UI/Common/取消按钮.png</c><br/>
/// · 字体 = <c>Resources/Fonts/fusion-pixel.ttf</c>（与撤离弹窗同一个）
/// </para>
///
/// <b>标题为什么要外置</b>：原来「换上新装备吗？」是 Panel 的子节点，跟着面板一起
/// 压在内容上；现在把它挂到 <c>Root</c> 下、锚在 Panel <b>顶边之外</b>，
/// 一行字的高度完全落在弹窗外面，<b>绝不遮挡面板内容</b>。
///
/// <para>生成后用不用手写 refill：改名 / 换图都是<b>重跑本菜单</b>，别手工改 prefab
/// （约定：prefab 由生成器单点负责，避免两处真值打架）。</para>
/// </summary>
public static class EquipReplaceConfirmPrefabBuilder
{
    // ===== 出入口（唯一） =====
    const string PrefabPath = "Assets/Resources/Prefabs/Battle/EquipReplaceConfirmPopup.prefab";

    // ===== 项目现有资源（全部引用，不复制、不新建） =====
    const string PanelSpritePath = "Assets/Art/UI/NavCharacter/角色_0002s_0010_图层-7.png";
    const string OkSpritePath = "Assets/Art/UI/Common/确定按钮.png";
    const string CancelSpritePath = "Assets/Art/UI/Common/取消按钮.png";
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";

    // ===== 排版真值（沿用撤离弹窗的锚点习惯） =====
    // Panel：Root 中心锚点，size 560×490
    const float PanelW = 560f;
    const float PanelH = 490f;
    /// <summary>标题底边离 Panel 顶边的间隙（标题整行在弹窗外面）。</summary>
    const float TitleGap = 10f;
    const float TitleH = 52f;

    // 对比列（Panel 内，父顶锚点）
    const float ColumnW = 254f;
    const float ColumnH = 300f;
    const float ColumnX = 132f;
    const float ColumnTopY = -18f;

    // 按钮
    const float ButtonW = 200f;
    const float ButtonH = 62f;
    const float ButtonX = 112f;

    /// <summary>运行时 <see cref="EquipReplaceConfirmUI"/> 加载的路径，两处必须一致。</summary>
    const string RuntimeResourcePath = "Prefabs/Battle/EquipReplaceConfirmPopup";

    [MenuItem("Tools/装备/生成替换装备确认弹窗预制体")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[EquipReplaceConfirm] 找不到字体 {FontPath}，停止生成");
            return;
        }
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        var okSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OkSpritePath);
        var cancelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CancelSpritePath);
        if (panelSprite == null || okSprite == null || cancelSprite == null)
        {
            Debug.LogError("[EquipReplaceConfirm] 弹窗美术资源缺失（Panel 底图 / 确定按钮 / 取消按钮），停止生成");
            return;
        }

        // ---- 根：Canvas + GraphicRaycaster（铁律 10：新 Canvas 必补 GraphicRaycaster）+ 业务脚本 ----
        var go = new GameObject("EquipReplaceConfirmPopup", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GameConfig.UiSort.EquipReplaceConfirm;
        if (go.GetComponent<EquipReplaceConfirmUI>() == null)
            go.AddComponent<EquipReplaceConfirmUI>();

        var root = Node("Root", go.transform);
        Stretch(root);

        // ---- 遮罩：吃掉点击，避免穿透到下面的抽奖面板 ----
        var dim = Node("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        // 与撤离弹窗同一口径：点空白 = 关闭。有趣的是这里「关闭」= 保守方向（留着旧的），
        // 与超时同一个出口 —— 绝不会让玩家误触就把身上的装备顶掉。
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        // ---- 【主人拍板】标题外置：挂在 Root 下、垫在 Panel 顶边之外 ----
        // Panel 半高 245 + 间隙 10 = 255（pivot 在自身底边中点 → 这一整行都在弹窗之上）
        var title = Node("Title", root);
        SetAnchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));
        title.anchoredPosition = new Vector2(0f, PanelH * 0.5f + TitleGap);
        title.sizeDelta = new Vector2(PanelW - 40f, TitleH);
        var titleText = MakeText(title.gameObject, font, 34, new Color(1f, 0.92f, 0.72f), TextAnchor.MiddleCenter);
        titleText.text = "换上新装备吗？";
        titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        // ---- Panel：照抄撤离弹窗（Sliced + a=0.98），只是更高（要装两列对比） ----
        var panel = Node("Panel", root);
        SetAnchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(1f, 1f, 1f, 0.98f);

        // ---- 两列对比：左 = 抽到的，右 = 现在穿的 ----
        BuildColumn(panel, "ColumnNew", -ColumnX, "抽到的", font);
        BuildColumn(panel, "ColumnOld", ColumnX, "现在穿的", font);

        // ---- 结论行 ----
        var verdict = Node("Verdict", panel);
        TopAnchor(verdict, 0f, -328f, PanelW - 40f, 36f);
        var verdictText = MakeText(verdict.gameObject, font, 26, new Color(0.55f, 0.95f, 0.60f), TextAnchor.MiddleCenter);
        verdictText.supportRichText = true;

        // ---- 提示行 ----
        var hint = Node("Hint", panel);
        TopAnchor(hint, 0f, -368f, PanelW - 40f, 28f);
        var hintText = MakeText(hint.gameObject, font, 20, new Color(0.72f, 0.72f, 0.76f), TextAnchor.MiddleCenter);
        hintText.text = "不换的那件会拆成强化石，不会浪费";

        // ---- 两个按钮：确定/取消，与撤离弹窗同一套美术 ----
        BuildButton(panel, "ReplaceButton", -ButtonX, okSprite, "换上新的", font);
        BuildButton(panel, "KeepButton", ButtonX, cancelSprite, "留着旧的", font);

        // 统一一次反射 / 描边口径
        GameFonts.ApplyToHierarchy(go.transform);

        EnsureFolder("Assets/Resources/Prefabs/Battle");
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipReplaceConfirm] 已生成 {PrefabPath}　（运行时加载路径：Resources/{RuntimeResourcePath}）");
    }

    /// <summary>一列对比：标题 + 装备图标 + 名字 + 品质/星级 + 描述。</summary>
    static void BuildColumn(Transform panel, string nodeName, float x, string header, Font font)
    {
        var col = Node(nodeName, panel);
        TopAnchor(col, x, ColumnTopY, ColumnW, ColumnH);
        var colImg = col.gameObject.AddComponent<Image>();
        colImg.color = new Color(1f, 1f, 1f, 0.05f);
        colImg.raycastTarget = false;

        var head = Node("Head", col);
        TopAnchor(head, 0f, -6f, ColumnW - 14f, 30f);
        MakeText(head.gameObject, font, 24, new Color(0.85f, 0.82f, 0.70f), TextAnchor.MiddleCenter).text = header;

        // 图标：预制体里留空 <c>sprite</c>，运行时按装备赋图（真源在 DraftPool）
        var icon = Node("Icon", col);
        TopAnchor(icon, 0f, -44f, 96f, 96f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;

        var name = Node("Name", col);
        TopAnchor(name, 0f, -148f, ColumnW - 14f, 30f);
        var nameText = MakeText(name.gameObject, font, 24, Color.white, TextAnchor.MiddleCenter);
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMaxSize = 24;

        var info = Node("Info", col);
        TopAnchor(info, 0f, -182f, ColumnW - 14f, 28f);
        var infoText = MakeText(info.gameObject, font, 22, new Color(0.42f, 0.72f, 0.98f), TextAnchor.MiddleCenter);
        infoText.supportRichText = true;

        var desc = Node("Desc", col);
        TopAnchor(desc, 0f, -214f, ColumnW - 14f, 80f);
        var descText = MakeText(desc.gameObject, font, 19, new Color(0.80f, 0.80f, 0.84f), TextAnchor.UpperLeft);
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.supportRichText = true;
    }

    static void BuildButton(Transform panel, string nodeName, float x, Sprite sprite, string label, Font font)
    {
        var rt = Node(nodeName, panel);
        TopAnchor(rt, x, -406f, ButtonW, ButtonH);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        // 项目其它弹窗统一用 ColorTint；SpriteSwap 没有配 state 的话按下去毫无反馈
        btn.transition = Selectable.Transition.ColorTint;

        var lab = Node("Label", rt);
        Stretch(lab);
        lab.anchoredPosition = new Vector2(0f, 2f);
        var t = MakeText(lab.gameObject, font, 26, new Color(1f, 1f, 0.7216f, 1f), TextAnchor.MiddleCenter);
        t.text = label;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
    }

    // ===== 小工具 =====

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

    /// <summary>「父顶锚点」：往下为负，跟主人在 Inspector 里量 Panel 内节点同一把尺子。</summary>
    static void TopAnchor(RectTransform rt, float x, float y, float w, float h)
    {
        SetAnchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
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
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Battle");
    }
}
