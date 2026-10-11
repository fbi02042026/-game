using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-11 主人拍板】抽奖概率公示弹窗 —— <b>预制体生成器</b>。
///
/// <para>主人原话：「概率公示弹窗给我生成预制体」—— 原来 <see cref="DraftOddsPopupUI"/>
/// 是运行时用代码一行行 new 出来的（纯色块 + 硬编码坐标），主人想在编辑器里直接调外观。
/// 改成跟 <c>EquipReplaceConfirmPopup</c> 同一套路子：<b>外观 100% 由预制体负责，
/// 脚本只填数据</b>。</para>
///
/// <b>资源口径全部取自项目现有美术，不造新图</b>：
/// · Panel 底图 = <c>Assets/Art/UI/NavCharacter/角色_0002s_0010_图层-7.png</c>（Sliced，a=0.98，
///   与撤离弹窗 / 替换装备确认弹窗同款）<br/>
/// · 右上角关闭 = <c>Assets/Art/UI/Talent/天赋_0007_关闭.png</c>（天赋界面同款）<br/>
/// · 字体 = <c>Assets/Resources/Fonts/fusion-pixel.ttf</c>
/// </para>
///
/// <b>为什么要 RowTemplate / HeadTemplate</b>：概率行数是<b>活的</b>（技能/佣兵/装备/金币各有几档，
/// 随当前卡池变），没法在预制体里摆死。所以预制体里放两个<b>默认隐藏的模板节点</b>，
/// 运行时克隆它们 —— 行的字号 / 颜色 / 对齐主人都能在预制体里直接调，脚本一个排版数字都不写。
///
/// <para>生成后<b>别手工改 prefab</b>：改名 / 换图 / 调间距一律<b>重跑本菜单</b>
/// （约定：prefab 由生成器单点负责，避免两处真值打架）。
/// 但<b>主人手工调过之后就别再重跑</b>（会覆盖掉主人改的东西）—— 见 AGENTS.md 铁律 22。</para>
/// </summary>
public static class DraftOddsPopupPrefabBuilder
{
    // ===== 出入口（唯一） =====
    const string PrefabPath = "Assets/Resources/Prefabs/Battle/DraftOddsPopup.prefab";
    /// <summary>运行时 <see cref="DraftOddsPopupUI"/> 加载的路径，两处必须一致。</summary>
    const string RuntimeResourcePath = "Prefabs/Battle/DraftOddsPopup";

    // ===== 项目现有资源（全部引用，不复制、不新建） =====
    const string PanelSpritePath = "Assets/Art/UI/NavCharacter/角色_0002s_0010_图层-7.png";
    const string CloseSpritePath = "Assets/Art/UI/Talent/天赋_0007_关闭.png";
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";

    // ===== 排版真值 =====
    const float PanelW = 620f;
    const float PanelH = 560f;
    const float RowH = 36f;
    const float HeadH = 34f;
    /// <summary>列表容器相对 Panel 的留白（左/右 34，上 104 给标题，下 96 给页脚）。</summary>
    const float ListPadX = 34f;
    const float ListPadTop = 104f;
    const float ListPadBottom = 96f;
    /// <summary>关闭按钮：64 的按钮半宽 32，中心往里 40 → 右边缘离面板边 8，不探出去。</summary>
    const float CloseInset = 40f;
    const float CloseSize = 64f;

    [MenuItem("Tools/UI/生成抽奖概率公示弹窗预制体")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[DraftOddsPopup] 找不到字体 {FontPath}，停止生成");
            return;
        }
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        var closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
        if (panelSprite == null || closeSprite == null)
        {
            Debug.LogError("[DraftOddsPopup] 弹窗美术资源缺失（Panel 底图 / 关闭按钮），停止生成");
            return;
        }

        // ---- 根：Canvas + GraphicRaycaster（铁律 10：新 Canvas 必补 GraphicRaycaster）+ 业务脚本 ----
        var go = new GameObject("DraftOddsPopup",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GameConfig.UiSort.DraftOddsPopup;
        if (go.GetComponent<DraftOddsPopupUI>() == null)
            go.AddComponent<DraftOddsPopupUI>();

        var root = Node("Root", go.transform);
        Stretch(root);

        // ---- 遮罩：点它就关（非模态，不逼玩家做决定） ----
        var dim = Node("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        // ---- Panel：与撤离 / 替换装备两个弹窗同款底图 ----
        var panel = Node("Panel", root);
        SetAnchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(1f, 1f, 1f, 0.98f);

        // ---- 标题 / 副标题 ----
        var title = Node("Title", panel);
        TopAnchor(title, 0f, -16f, PanelW - 40f, 44f);
        var titleText = MakeText(title.gameObject, font, 34, new Color(1f, 0.92f, 0.72f), TextAnchor.MiddleCenter);
        titleText.text = "抽奖概率";
        titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        var sub = Node("Sub", panel);
        TopAnchor(sub, 0f, -60f, PanelW - 40f, 34f);
        var subText = MakeText(sub.gameObject, font, 20, new Color(0.74f, 0.74f, 0.78f), TextAnchor.MiddleCenter);
        subText.text = "下面是单次抽奖的真实比例，随当前卡池实时变化。";

        // ---- 列表容器：行由运行时按卡池克隆 RowTemplate 生成 ----
        var list = Node("List", panel);
        Stretch(list);
        list.offsetMin = new Vector2(ListPadX, ListPadBottom);
        list.offsetMax = new Vector2(-ListPadX, -ListPadTop);
        BuildRowTemplate(list, font);
        BuildHeadTemplate(list, font);

        // ---- 页脚 ----
        var foot = Node("Foot", panel);
        TopAnchor(foot, 0f, -PanelH + 70f, PanelW - 40f, 30f);
        var footText = MakeText(foot.gameObject, font, 18, new Color(0.62f, 0.62f, 0.66f), TextAnchor.MiddleCenter);
        footText.text = "品质越高越稀有，好东西本来就该难抽。";

        // ---- 右上角关闭（主人拍板：不要底部的「确定/关闭」大按钮） ----
        var close = Node("BtnClose", panel);
        SetAnchor(close, new Vector2(1f, 1f), new Vector2(1f, 1f));
        close.anchoredPosition = new Vector2(-CloseInset, -CloseInset);
        close.sizeDelta = new Vector2(CloseSize, CloseSize);
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.sprite = closeSprite;
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.transition = Selectable.Transition.ColorTint;

        // 统一一次字体 / 描边口径
        GameFonts.ApplyToHierarchy(go.transform);

        EnsureFolder("Assets/Resources/Prefabs/Battle");
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DraftOddsPopup] 已生成 {PrefabPath}　（运行时加载路径：Resources/{RuntimeResourcePath}）");
    }

    /// <summary>
    /// 一行「名字 ……… 百分比」的模板（<b>默认隐藏</b>，运行时克隆）。
    /// 左 55% 放名字、右 45% 放百分比，中间空着 —— 跟原来代码里画出来的效果一致。
    /// </summary>
    static void BuildRowTemplate(RectTransform list, Font font)
    {
        var row = Node("RowTemplate", list);
        SetAnchor(row, new Vector2(0f, 1f), new Vector2(1f, 1f));
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = new Vector2(0f, RowH);

        var name = Node("Name", row);
        SetAnchor(name, new Vector2(0f, 0f), new Vector2(0.55f, 1f));
        name.offsetMin = new Vector2(10f, 0f);
        name.offsetMax = Vector2.zero;
        var nameText = MakeText(name.gameObject, font, 24, Color.white, TextAnchor.MiddleLeft);
        nameText.text = "技能";

        var pct = Node("Pct", row);
        SetAnchor(pct, new Vector2(0.55f, 0f), new Vector2(1f, 1f));
        pct.offsetMin = Vector2.zero;
        pct.offsetMax = new Vector2(-10f, 0f);
        var pctText = MakeText(pct.gameObject, font, 26, Color.white, TextAnchor.MiddleRight);
        pctText.text = "100.0%";

        row.gameObject.SetActive(false);   // 模板不参与显示，只当克隆源
    }

    /// <summary>段标题模板（「抽到什么」/「什么品质」，<b>默认隐藏</b>，运行时克隆）。</summary>
    static void BuildHeadTemplate(RectTransform list, Font font)
    {
        var head = Node("HeadTemplate", list);
        SetAnchor(head, new Vector2(0f, 1f), new Vector2(1f, 1f));
        head.pivot = new Vector2(0.5f, 1f);
        head.anchoredPosition = Vector2.zero;
        head.sizeDelta = new Vector2(0f, HeadH);
        var t = MakeText(head.gameObject, font, 22, new Color(0.95f, 0.86f, 0.62f), TextAnchor.MiddleLeft);
        t.text = "抽到什么";
        head.gameObject.SetActive(false);
    }

    // ===== 小工具（与 EquipReplaceConfirmPrefabBuilder 同一把尺子） =====

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
