using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ⚠️ 一次性脚手架：生成竞技场弹窗预制体 Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab。
/// 菜单：Tools/竞技场/生成竞技场弹窗预制体。
///
/// 🔴 项目铁律（2026-10-12 踩坑定稿）：主人手改过预制体之后【禁止重跑本生成器】——
/// 重跑 = 排版打回旧版 + 手工节点被抹掉。改排版一律直接改 prefab。
/// 本脚本只第一次（或主人明确要求推倒重来并确认覆盖对话框）时用。
///
/// 📐 契约 v2（2026-10-29 17:46 主人发参考图重构：豪华排行榜布局）：
/// ArenaPopupUI(root: Canvas+CanvasScaler+GraphicRaycaster+ArenaPopupUI, 保存为 inactive)
/// └─ Root
///    ├─ Dim
///    └─ Panel (680×1140)
///       ├─ TitleText「对战排行榜」/ CloseBtn{Label} / TriesText「对战次数 N/M」
///       ├─ AdBonusBtn{Label}(看广告+2) / RewardBtn{Label}(排行榜奖励说明)
///       ├─ Podium1/2/3（前三领奖台卡，各自内含 RankNum/HeadImg/NameText/PtsIcon/PtsText —— 同名节点，绑定从各自根搜）
///       ├─ MyBar（我的排名条：MyRankText/MyHead/MyName/MyPtsIcon/MyPtsText）
///       ├─ NoticeText（赛季结算横幅）
///       └─ List(ScrollRect) → Viewport(RectMask2D) → Content → RowTemplate(inactive 640×56)
///          └─ RankText / HeadImg / NameText / PtsIcon / PtsText / FightBtn{FightLabel} / DashText
///    ├─ JobSelect → JobBox → JobTitleText / JobDescText / JobCard0..2{JobIcon,JobNameText} / JobCancelBtn{Label}
///    └─ Result → ResultBgImg / Veil / MyIconImg / BotIconImg / VsText / MyLabelText / BotLabelText
///               / VerdictText / PtsIconImg / ResultPtsText / FlavorText / GoldText / OkBtn{Label}
///
/// 美术来源：面板 9-slice 与按钮图直接从 EvacuateConfirmPopup 预制体的 Image 上取，
/// 不猜资源 guid；拉不到就 LogWarning + 纯色顶替（主人可在 prefab 里手换）。
/// 战力图标（PtsIcon/MyPtsIcon）用主人口径的 Resources/UI/Art/Common/对战积分。
/// 字号只用 24/48（fusion-pixel 12px 点阵整倍数）；字体 GameFonts.GetChinese()。
/// </summary>
public static class ArenaPopupPrefabBuilder
{
    const string SavePath = "Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab";
    const string EvacuatePrefabPath = "Prefabs/Battle/EvacuateConfirmPopup";
    const string PowerIconPath = "UI/Art/Common/对战积分";

    // ── 尺寸/坐标（与运行时 ArenaPopupUI 常量一致）──
    const float PanelW = 680f;
    const float PanelH = 1140f;
    const int RowTotal = ArenaRules.BotCount + 1;   // 101
    const float RowStep = 64f;
    const float RowH = 56f;
    const float ViewportH = 592f;
    const float ContentH = RowTotal * RowStep + 8f;

    // ── 颜色（行色运行时还会写，见 ArenaPopupUI 注释；领奖台卡色运行时按名次 tint）──
    static readonly Color PanelColor = new Color(0.13f, 0.12f, 0.18f, 0.97f);
    static readonly Color RowColorA = new Color(0.21f, 0.20f, 0.28f, 0.95f);
    static readonly Color BtnSlate = new Color(0.35f, 0.36f, 0.45f, 1f);
    static readonly Color BtnAmber = new Color(0.82f, 0.52f, 0.16f, 1f);
    static readonly Color TxtMain = Color.white;
    static readonly Color TxtSub = new Color(0.75f, 0.75f, 0.80f, 1f);
    static readonly Color TxtGold = new Color(1f, 0.80f, 0.35f, 1f);
    static readonly Color PodiumGold = new Color(0.62f, 0.44f, 0.14f, 0.95f);
    static readonly Color PodiumSilver = new Color(0.42f, 0.46f, 0.56f, 0.95f);
    static readonly Color PodiumBronze = new Color(0.52f, 0.33f, 0.20f, 0.95f);

    [MenuItem("Tools/竞技场/生成竞技场弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(SavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "竞技场弹窗预制体已存在",
                "ArenaPopupUI.prefab 已存在。\n\n" +
                "⚠️ 本菜单是生成器脚手架：主人手改过预制体后再重跑 = 排版打回旧版、手工节点被抹掉（项目铁律）。\n\n" +
                "契约 v2（参考图豪华版布局）与 v1 不同——若 v1 预制体主人没手改过，可以覆盖升级；手改过就取消，先备份。\n\n" +
                "确定要覆盖？",
                "覆盖重来", "取消");
            if (!overwrite) return;
        }

        // 美术从 EvacuateConfirmPopup 现拉，不猜 guid
        Sprite panelSprite = null, buttonSprite = null;
        var evac = Resources.Load<GameObject>(EvacuatePrefabPath);
        if (evac != null)
        {
            var panelT = UiFind.Deep(evac.transform, "Panel");
            if (panelT != null)
            {
                var img = panelT.GetComponent<Image>();
                if (img != null) panelSprite = img.sprite;
            }
            var btnT = UiFind.Deep(evac.transform, "EvacuateButton");
            if (btnT != null)
            {
                var img = btnT.GetComponent<Image>();
                if (img != null) buttonSprite = img.sprite;
            }
        }
        else
            Debug.LogWarning($"[ArenaBuilder] 参考预制体没加载到：{EvacuatePrefabPath}");
        if (panelSprite == null) Debug.LogWarning("[ArenaBuilder] 没拉到面板 9-slice，先用纯色，主人在 prefab 里手换图");
        if (buttonSprite == null) Debug.LogWarning("[ArenaBuilder] 没拉到按钮图，先用纯色，主人在 prefab 里手换图");

        var font = GameFonts.GetChinese();
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var powerIcon = Resources.Load<Sprite>(PowerIconPath);
        if (powerIcon == null) Debug.LogWarning("[ArenaBuilder] 战力图标没加载到：" + PowerIconPath + "（主人确认 .meta 已生成后可手拖）");

        // ── 根：Canvas + ArenaPopupUI ──
        var root = new GameObject("ArenaPopupUI", typeof(RectTransform));
        root.AddComponent<Canvas>();
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<ArenaPopupUI>();
        Stretch(root.GetComponent<RectTransform>());

        var rootChild = MakeImage(root.transform, "Root", Color.clear);
        Stretch(rootChild.rectTransform);

        // ── Dim ──
        var dim = MakeImage(rootChild.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 挡住下层点击

        // ── Panel ──
        var panel = MakeImage(rootChild.transform, "Panel", PanelColor, panelSprite, Image.Type.Sliced);
        SetRT(panel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(PanelW, PanelH));
        var p = panel.transform;

        // 顶部行：标题（左） / 次数 / 看广告 / 关闭（右）
        var title = MakeText(p, "TitleText", "对战排行榜", 48, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(title.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-160f, 536f), V2(300f, 54f));

        var close = MakeButton(p, "CloseBtn", "X", 24, BtnSlate, buttonSprite, font);
        SetRT(close.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(302f, 540f), V2(44f, 44f));

        var tries = MakeText(p, "TriesText", "对战次数 10/10", 24, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(tries.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(110f, 540f), V2(200f, 40f));

        var ad = MakeButton(p, "AdBonusBtn", "+2", 24, BtnAmber, buttonSprite, font);
        SetRT(ad.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(242f, 540f), V2(60f, 40f));

        var reward = MakeButton(p, "RewardBtn", "排行榜奖励", 24, BtnSlate, buttonSprite, font);
        SetRT(reward.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(242f, 486f), V2(140f, 40f));

        // 前三领奖台（第 1 名居中最高；卡色运行时按名次 tint 金/银/铜）
        BuildPodium(p, "Podium1", V2(0f, 348f), 200f, 270f, 116f, PodiumGold, powerIcon, font);
        BuildPodium(p, "Podium2", V2(-198f, 330f), 176f, 230f, 96f, PodiumSilver, powerIcon, font);
        BuildPodium(p, "Podium3", V2(198f, 330f), 176f, 230f, 96f, PodiumBronze, powerIcon, font);

        // 我的排名条
        var myBar = MakeImage(p, "MyBar", new Color(0.17f, 0.22f, 0.34f, 0.97f), panelSprite, Image.Type.Sliced);
        SetRT(myBar.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 172f), V2(620f, 64f));
        var myRank = MakeText(myBar.transform, "MyRankText", "我的排名 第101名", 24, TextAnchor.MiddleLeft, TxtGold, font);
        SetRT(myRank.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-238f, 0f), V2(190f, 36f));
        var myHead = MakeImage(myBar.transform, "MyHead", Color.white);
        SetRT(myHead.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-112f, 0f), V2(48f, 48f));
        myHead.preserveAspect = true;
        myHead.raycastTarget = false;
        var myName = MakeText(myBar.transform, "MyName", "我", 24, TextAnchor.MiddleLeft, TxtMain, font);
        SetRT(myName.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-22f, 0f), V2(150f, 32f));
        var myPtsIcon = MakeImage(myBar.transform, "MyPtsIcon", Color.white, powerIcon);
        SetRT(myPtsIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(120f, 0f), V2(24f, 24f));
        myPtsIcon.preserveAspect = true;
        myPtsIcon.raycastTarget = false;
        var myPts = MakeText(myBar.transform, "MyPtsText", "450", 24, TextAnchor.MiddleLeft, TxtGold, font);
        SetRT(myPts.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(176f, 0f), V2(120f, 32f));

        // 赛季结算横幅
        var notice = MakeText(p, "NoticeText", "", 24, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(notice.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -472f), V2(600f, 30f));
        notice.gameObject.SetActive(false);

        // ── List（ScrollRect）→ Viewport → Content → RowTemplate ──
        var list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(p, false);
        var listRt = list.GetComponent<RectTransform>();
        SetRT(listRt, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -166f), V2(640f, ViewportH));
        var scroll = list.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 15f;

        var viewport = MakeImage(list.transform, "Viewport", Color.clear);
        Stretch(viewport.rectTransform);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        SetRT(contentRt, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 0f), V2(640f, ContentH));
        scroll.viewport = viewport.rectTransform;
        scroll.content = contentRt;

        BuildRowTemplate(content.transform, font, buttonSprite, powerIcon);

        BuildJobSelect(rootChild.transform, font, panelSprite, buttonSprite);
        BuildResult(rootChild.transform, font, buttonSprite);

        // ── 保存（保存为 inactive：实例化即隐藏，由 ArenaPopupUI.Ensure 控制显隐）──
        root.SetActive(false);
        var dir = Path.GetDirectoryName(SavePath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ArenaBuilder] 已生成 {SavePath}（契约 v2 参考图布局；主人改排版直接改 prefab，禁止重跑本菜单）");
        EditorUtility.DisplayDialog("生成完成",
            "已生成 Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab（契约 v2 参考图布局）\n\n" +
            "以后改排版/图标/颜色直接改预制体，【禁止重跑本菜单】。\n" +
            "节点改名要先对齐 ArenaPopupUI.cs 的绑定（缺节点会 LogError 报名字）。",
            "好");
    }

    /// <summary>领奖台卡：名次大字 + 头像 + 名字 + 战力图标/数字（同名子节点，运行时从各自卡根搜）。</summary>
    static void BuildPodium(Transform parent, string name, Vector2 pos, float w, float h, float headSize,
        Color cardColor, Sprite powerIcon, Font font)
    {
        var card = MakeImage(parent, name, cardColor, null, Image.Type.Sliced);
        SetRT(card.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), pos, V2(w, h));
        card.raycastTarget = true;   // 点卡 → 详情（运行时接 Button）

        var rank = MakeText(card.transform, "RankNum", "1", 48, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(rank.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, h * 0.5f - 40f), V2(70f, 52f));

        var head = MakeImage(card.transform, "HeadImg", Color.white);
        SetRT(head.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, h * 0.5f - 62f - headSize * 0.5f), V2(headSize, headSize));
        head.preserveAspect = true;
        head.raycastTarget = false;

        var nameT = MakeText(card.transform, "NameText", "对手", 24, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(nameT.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -h * 0.5f + 57f), V2(w - 30f, 30f));

        var icon = MakeImage(card.transform, "PtsIcon", Color.white, powerIcon);
        SetRT(icon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-w * 0.22f, -h * 0.5f + 23f), V2(26f, 26f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var pts = MakeText(card.transform, "PtsText", "0", 24, TextAnchor.MiddleLeft, TxtGold, font);
        SetRT(pts.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(w * 0.12f, -h * 0.5f + 23f), V2(w * 0.55f, 32f));
    }

    static void BuildRowTemplate(Transform content, Font font, Sprite buttonSprite, Sprite powerIcon)
    {
        var row = MakeImage(content, "RowTemplate", RowColorA);
        SetRT(row.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, -4f), V2(640f, RowH));
        row.raycastTarget = false;   // 运行时克隆后打开（行点击 → 详情）

        var rank = MakeText(row.transform, "RankText", "1", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(rank.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-278f, 0f), V2(40f, 36f));

        var head = MakeImage(row.transform, "HeadImg", Color.white);
        SetRT(head.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-218f, 0f), V2(44f, 44f));
        head.preserveAspect = true;
        head.raycastTarget = false;

        var name = MakeText(row.transform, "NameText", "名字占位", 24, TextAnchor.MiddleLeft, TxtMain, font);
        SetRT(name.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-128f, 0f), V2(170f, 32f));

        var icon = MakeImage(row.transform, "PtsIcon", Color.white, powerIcon);
        SetRT(icon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(42f, 0f), V2(20f, 20f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var pts = MakeText(row.transform, "PtsText", "0", 24, TextAnchor.MiddleLeft, TxtMain, font);
        SetRT(pts.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(88f, 0f), V2(100f, 32f));

        var fight = MakeButton(row.transform, "FightBtn", "对战", 24, BtnAmber, buttonSprite, font);
        SetRT(fight.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(247f, 0f), V2(92f, 44f));

        var dash = MakeText(row.transform, "DashText", "-", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(dash.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(247f, 0f), V2(60f, 32f));

        row.gameObject.SetActive(false);   // 模板恒隐藏（运行时克隆 101 行）
    }

    static void BuildJobSelect(Transform parent, Font font, Sprite panelSprite, Sprite buttonSprite)
    {
        var js = MakeImage(parent, "JobSelect", new Color(0f, 0f, 0f, 0.66f));
        Stretch(js.rectTransform);
        js.raycastTarget = true;

        var box = MakeImage(js.transform, "JobBox", PanelColor, panelSprite, Image.Type.Sliced);
        SetRT(box.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(560f, 440f));

        var title = MakeText(box.transform, "JobTitleText", "选择出战职业", 24, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(title.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -40f), V2(400f, 40f));

        var desc = MakeText(box.transform, "JobDescText", "出战职业代表你登场，输赢看战力比", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(desc.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -86f), V2(480f, 34f));

        var jobs = PlayerJobDefs.Playable;
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 190f;
            var card = MakeImage(box.transform, "JobCard" + i, RowColorA);
            SetRT(card.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(x, 20f), V2(160f, 190f));
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;

            var icon = MakeImage(card.transform, "JobIcon", Color.white);
            SetRT(icon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 28f), V2(100f, 100f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (i < jobs.Length)
            {
                var sp = PlayerJobDefs.TryLoadJobIcon(jobs[i]);
                if (sp != null) icon.sprite = sp;
            }

            var label = MakeText(card.transform, "JobNameText", i < jobs.Length ? PlayerJobDefs.Get(jobs[i]).DisplayName : "职业", 24,
                TextAnchor.MiddleCenter, TxtMain, font);
            SetRT(label.rectTransform, V2(0.5f, 0f), V2(0.5f, 0f), V2(0.5f, 0.5f), V2(0f, 22f), V2(150f, 34f));
        }

        var cancel = MakeButton(box.transform, "JobCancelBtn", "算了", 24, BtnSlate, buttonSprite, font);
        SetRT(cancel.image.rectTransform, V2(0.5f, 0f), V2(0.5f, 0f), V2(0.5f, 0.5f), V2(0f, 60f), V2(200f, 56f));

        js.gameObject.SetActive(false);
    }

    static void BuildResult(Transform parent, Font font, Sprite buttonSprite)
    {
        var res = MakeImage(parent, "Result", new Color(0.06f, 0.06f, 0.09f, 1f));
        Stretch(res.rectTransform);
        res.raycastTarget = true;

        var bg = MakeImage(res.transform, "ResultBgImg", Color.white);
        Stretch(bg.rectTransform);
        bg.preserveAspect = true;
        bg.raycastTarget = false;

        var veil = MakeImage(res.transform, "Veil", new Color(0f, 0f, 0f, 0.45f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = false;

        var myIcon = MakeImage(res.transform, "MyIconImg", Color.white);
        SetRT(myIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-130f, 130f), V2(150f, 150f));
        myIcon.preserveAspect = true;
        myIcon.raycastTarget = false;

        var botIcon = MakeImage(res.transform, "BotIconImg", Color.white);
        SetRT(botIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(130f, 130f), V2(150f, 150f));
        botIcon.preserveAspect = true;
        botIcon.raycastTarget = false;

        var vs = MakeText(res.transform, "VsText", "VS", 48, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(vs.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(120f, 70f));

        var myLabel = MakeText(res.transform, "MyLabelText", "我·剑盾", 24, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(myLabel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-130f, 30f), V2(280f, 34f));

        var botLabel = MakeText(res.transform, "BotLabelText", "对手名字", 24, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(botLabel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(130f, 30f), V2(280f, 34f));

        var verdict = MakeText(res.transform, "VerdictText", "胜利！", 48, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(verdict.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -70f), V2(400f, 70f));

        var ptsIcon = MakeImage(res.transform, "PtsIconImg", Color.white);
        SetRT(ptsIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-110f, -150f), V2(64f, 64f));
        ptsIcon.preserveAspect = true;
        ptsIcon.raycastTarget = false;
        var ptsSprite = Resources.Load<Sprite>(PowerIconPath);
        if (ptsSprite != null) ptsIcon.sprite = ptsSprite;
        else Debug.LogWarning("[ArenaBuilder] 积分图标没加载到：" + PowerIconPath + "（主人确认 .meta 已生成后可手拖）");

        var pts = MakeText(res.transform, "ResultPtsText", "+15 积分", 48, TextAnchor.MiddleLeft, TxtGold, font);
        SetRT(pts.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-60f, -150f), V2(300f, 64f));

        var flavor = MakeText(res.transform, "FlavorText", "这波运气在线", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(flavor.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -220f), V2(500f, 34f));

        var gold = MakeText(res.transform, "GoldText", "金币 +200", 24, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(gold.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -280f), V2(440f, 34f));

        var ok = MakeButton(res.transform, "OkBtn", "继续", 24, BtnAmber, buttonSprite, font);
        SetRT(ok.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -350f), V2(240f, 64f));

        res.gameObject.SetActive(false);
    }

    // ───────────────────────── 建树小工具（编辑器域；颜色/坐标与运行时同一套） ─────────────────────────

    struct UiButton { public Image image; public Button button; }

    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = type;
        }
        return img;
    }

    static Text MakeText(Transform parent, string name, string content, int size, TextAnchor align, Color color, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static UiButton MakeButton(Transform parent, string name, string label, int size, Color color, Sprite sprite, Font font)
    {
        var img = MakeImage(parent, name, color, sprite, Image.Type.Sliced);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (!string.IsNullOrEmpty(label))
        {
            var t = MakeText(img.transform, "Label", label, size, TextAnchor.MiddleCenter, Color.white, font);
            Stretch(t.rectTransform);
        }
        return new UiButton { image = img, button = btn };
    }

    static void SetRT(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
