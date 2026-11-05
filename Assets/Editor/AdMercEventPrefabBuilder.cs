using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ⚠️ 一次性脚手架：生成看广告送佣兵活动面板预制体
/// Assets/Resources/Prefabs/UI/AdMercEventPanel.prefab。
/// 菜单：Tools → 看广告活动 → 生成活动面板预制体。
///
/// 🔴 项目铁律（2026-10-12 踩坑定稿）：主人手改过预制体之后【禁止重跑本生成器】——
/// 重跑 = 排版打回旧版 + 手工节点被抹掉。改排版一律直接改 prefab。
///
/// 🎨 美术来源（2026-10-31 主人导入，Resources/UI/广告送佣兵/，全部按路径加载不依赖 guid）：
///   界面元素拆分_0006_图层-1        597×552  面板整底图（人物+标题+副标题+框，右上自带 + 号关闭位）
///   粉金幻想游戏界面素材集_0001_图层-3  195×74  「立即观看」金色按钮（含播放三角，无字）
///   界面元素拆分_0000_背景-拷贝       206×15  进度条填充（橙黄条）
///   界面元素拆分-(1)_0000_背景-拷贝    60×68  红礼盒
///   界面元素拆分-(1)_0001_背景-拷贝    60×65  紫礼盒
///   界面元素拆分_0003_图层-3         53×61  进度条两端金色星饰
///   礼物底光                        200×200  礼盒底下金色光效（每个礼盒一层，纯装饰）
///   （主界面图标.png 144×102 是主界面入口用图，本预制体不消费——入口归 AdMercEventEntryUI）
///
/// 📐 契约（与 Assets/Scripts/UI/AdMercEventUI.cs 绑定代码一一对应）：
/// AdMercEventPanel(root: Canvas+CanvasScaler(Match Height 1280)+GraphicRaycaster+AdMercEventUI, 保存为 inactive)
/// └─ Root
///    ├─ Dim（全屏遮罩，点它关窗——按钮由运行时代码补挂监听）
///    └─ Panel（680×629 = 底图 597×552 等比放大 1.1387，整体缩放不 stretch）
///       ├─ CloseBtn（右上透明热区 64×64，+ 号美术在底图上；Label 省略）
///       ├─ DaysText「累计看广告 0/10 天」（进度条上方，运行时刷新）
///       ├─ ProgressFill（进度条填充，Filled Horizontal；fillAmount=Days/MaxDay 运行时刷）
///       ├─ StarL / StarR（进度条两端星饰，纯装饰可点穿）
///       ├─ GiftRow
///       │  ├─ Gift0{GiftIcon, DayText「3天」}（透明热区，点击→详情/直领由运行时绑定）
///       │  ├─ Gift1{...} / Gift2{...} / Gift3{...}
///       └─ WatchAdBtn{Label「立即观看」}（运行时【不改文案】只调透明度：今日记满/攒满变灰，
///          变灰仍可点 → 弹 toast；2026-10-31 主人拍板）
///       └─ HintText（底部说明）
/// RewardText / ClaimMarkText / TitleText / MercImage / MercNameText 均为可选节点（绑定代码
/// null 安全），设计稿不含这些元素，本生成器省略；缺失只影响对应展示，不报错（ProgressFill 除外，
/// 缺它 LogWarning 提示进度条不显示）。
/// 礼盒红点由运行时 RedDot.Bind 挂（键 admerc.m0..m3），预制体不放红点节点。
/// 拉不到图 = LogWarning + 纯色/占位顶替（主人在 prefab 里手换），不做运行时兜底。
/// 字号 18/24/26（fusion-pixel 12px 点阵整倍数）；字体 GameFonts.GetChinese()。
/// </summary>
public static class AdMercEventPrefabBuilder
{
    const string SavePath = "Assets/Resources/Prefabs/UI/AdMercEventPanel.prefab";
    const string ArtDir = "UI/广告送佣兵/";
    const string BgPath = ArtDir + "界面元素拆分_0006_图层-1";
    const string WatchBtnPath = ArtDir + "粉金幻想游戏界面素材集_0001_图层-3";
    const string ProgressFillPath = ArtDir + "界面元素拆分_0000_背景-拷贝";
    const string GiftRedPath = ArtDir + "界面元素拆分-(1)_0000_背景-拷贝";
    const string GiftPurplePath = ArtDir + "界面元素拆分-(1)_0001_背景-拷贝";
    const string StarPath = ArtDir + "界面元素拆分_0003_图层-3";
    const string GiftGlowPath = ArtDir + "礼物底光";
    const string GiftFallbackPath = "UI/Art/邮件/icon礼物";   // 礼盒图缺省时的占位（仅日志提示，非运行时兜底）

    // ── 尺寸/坐标（720×1280 竖屏逻辑坐标；底图 597×552 → 面板 680×629，s=1.1387）──
    const float PanelW = 680f;
    const float PanelH = 629f;

    // ── 颜色（对齐 ArenaPopupUI 暗面板口径，仅缺图占位用）──
    static readonly Color PanelFallback = new Color(0.13f, 0.12f, 0.18f, 0.97f);
    static readonly Color BtnAmber = new Color(0.82f, 0.52f, 0.16f, 1f);
    static readonly Color FillFallback = new Color(1f, 0.72f, 0.20f, 1f);
    static readonly Color TxtMain = Color.white;
    static readonly Color TxtGold = new Color(1f, 0.85f, 0.45f, 1f);

    public static void Build()
    {
        if (File.Exists(SavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "活动面板预制体已存在",
                "AdMercEventPanel.prefab 已存在。\n\n" +
                "⚠️ 本菜单是生成器脚手架：主人手改过预制体后再重跑 = 排版打回旧版、手工节点被抹掉（项目铁律）。\n\n" +
                "确定要覆盖？",
                "覆盖重来", "取消");
            if (!overwrite) return;
        }

        // ── 美术现取（Resources 路径加载，不猜 guid）──
        var bg = Resources.Load<Sprite>(BgPath);
        if (bg == null) Debug.LogWarning("[AdMercBuilder] 底图没加载到：" + BgPath + "（面板先用纯色，主人在 prefab 里手换图）");
        var btnSprite = Resources.Load<Sprite>(WatchBtnPath);
        if (btnSprite == null) Debug.LogWarning("[AdMercBuilder] 观看按钮图没加载到：" + WatchBtnPath + "（先用纯色，主人在 prefab 里手换图）");
        var fillSprite = Resources.Load<Sprite>(ProgressFillPath);
        if (fillSprite == null) Debug.LogWarning("[AdMercBuilder] 进度条填充图没加载到：" + ProgressFillPath + "（先用纯色，主人在 prefab 里手换图）");
        var giftRed = Resources.Load<Sprite>(GiftRedPath);
        var giftPurple = Resources.Load<Sprite>(GiftPurplePath);
        if (giftRed == null || giftPurple == null)
            Debug.LogWarning("[AdMercBuilder] 礼盒图没加载到：" + GiftRedPath + " / " + GiftPurplePath + "（回落 " + GiftFallbackPath + "，主人在 prefab 里手换图）");
        var star = Resources.Load<Sprite>(StarPath);
        if (star == null) Debug.LogWarning("[AdMercBuilder] 星饰图没加载到：" + StarPath + "（两端星饰省略，主人在 prefab 里手补）");
        var giftGlow = Resources.Load<Sprite>(GiftGlowPath);
        if (giftGlow == null) Debug.LogWarning("[AdMercBuilder] 礼盒底光没加载到：" + GiftGlowPath + "（礼盒底光省略，主人在 prefab 里手补）");

        var font = GameFonts.GetChinese();
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Sprite GiftSprite(int i) => i % 2 == 0 ? giftPurple : giftRed;

        // ── 根：Canvas + AdMercEventUI ──
        var root = new GameObject("AdMercEventPanel", typeof(RectTransform));
        root.AddComponent<Canvas>();
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<AdMercEventUI>();
        Stretch(root.GetComponent<RectTransform>());

        var rootChild = MakeImage(root.transform, "Root", Color.clear);
        Stretch(rootChild.rectTransform);

        // ── Dim ──
        var dim = MakeImage(rootChild.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 挡住下层点击；点击关窗由 AdMercEventUI 运行时补挂 Button 监听
        dim.gameObject.AddComponent<Button>();

        // ── Panel（底图整铺：人物+标题+副标题+框 全在图上）──
        var panel = MakeImage(rootChild.transform, "Panel", bg != null ? Color.white : PanelFallback, bg, Image.Type.Simple);
        SetRT(panel.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, 0f), V2(PanelW, PanelH));
        panel.raycastTarget = false;   // 底图不挡点击（礼盒/按钮是独立热区）
        var p = panel.transform;

        // 右上关闭热区（+ 号美术在底图上，这里是透明点击区）
        var close = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(p, false);
        var closeImg = close.GetComponent<Image>();
        closeImg.color = Color.clear;
        closeImg.raycastTarget = true;
        SetRT(close.GetComponent<RectTransform>(), V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(303f, 178f), V2(64f, 64f));

        // 累计进度文字（进度条上方；设计稿副标题已在底图，这里只补动态数字）
        var days = MakeText(p, "DaysText", "累计看广告 0/10 天", 24, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(days.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, -150f), V2(560f, 34f));

        // 进度条填充（Filled Horizontal，fillAmount 运行时刷；槽底在底图上）
        var fill = MakeImage(p, "ProgressFill", fillSprite != null ? Color.white : FillFallback, fillSprite, Image.Type.Filled);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        SetRT(fill.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, -269f), V2(520f, 20f));

        // 两端星饰（纯装饰）
        if (star != null)
        {
            foreach (var (name, x) in new[] { ("StarL", -292f), ("StarR", 292f) })
            {
                var sImg = MakeImage(p, name, Color.white, star, Image.Type.Simple);
                sImg.raycastTarget = false;
                sImg.preserveAspect = true;
                SetRT(sImg.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(x, -269f), V2(60f, 69f));
            }
        }

        // ── 礼盒行：4 个里程碑（底图槽位上摆图+热区，天数文字在槽下）──
        var giftRow = new GameObject("GiftRow", typeof(RectTransform));
        giftRow.transform.SetParent(p, false);
        SetRT(giftRow.GetComponent<RectTransform>(), V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f),
            V2(0f, -230f), V2(660f, 160f));

        float[] giftX = { -235f, -90f, 75f, 230f };
        float[] giftSize = { 68f, 68f, 68f, 96f };   // 第 4 档大礼盒（设计稿口径）
        string[] dayLabels = { "3天", "5天", "7天", "10天" };
        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
        {
            // 透明热区（点击→详情/直领由运行时绑定；红点也挂这）
            var cell = new GameObject("Gift" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(giftRow.transform, false);
            var cellImg = cell.GetComponent<Image>();
            cellImg.color = Color.clear;
            cellImg.raycastTarget = true;
            SetRT(cell.GetComponent<RectTransform>(), V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f),
                V2(giftX[i], 0f), V2(giftSize[i] + 36f, giftSize[i] + 44f));

            // 底光（先建 = 垫在礼盒图标后面，纯装饰可点穿）
            if (giftGlow != null)
            {
                var glow = MakeImage(cell.transform, "GiftGlow", Color.white, giftGlow, Image.Type.Simple);
                glow.raycastTarget = false;
                glow.preserveAspect = true;
                SetRT(glow.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f),
                    V2(0f, -6f), V2(giftSize[i] + 30f, giftSize[i] + 30f));
            }

            var icon = MakeImage(cell.transform, "GiftIcon", Color.white,
                GiftSprite(i) != null ? GiftSprite(i) : Resources.Load<Sprite>(GiftFallbackPath), Image.Type.Simple);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRT(icon.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, 0f), V2(giftSize[i], giftSize[i] + 4f));

            var dayText = MakeText(cell.transform, "DayText", dayLabels[i], 24, TextAnchor.MiddleCenter, TxtMain, font);
            SetRT(dayText.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, -70f), V2(110f, 28f));
        }

        // ── 立即观看按钮 ──
        var watch = MakeButton(p, "WatchAdBtn", "立即观看", 26, BtnAmber, btnSprite, font);
        SetRT(watch.image.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, -355f), V2(222f, 84f));

        // 底部说明
        var hint = MakeText(p, "HintText",
            "每天看满 3 个广告计 1 天；达标后点礼盒领取，奖励发到邮箱。", 18, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(hint.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(0f, -412f), V2(600f, 26f));

        // ── 保存（保存为 inactive：实例化即隐藏，由 AdMercEventUI 控制显隐）──
        root.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
        }
        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AdMercBuilder] 活动面板预制体已生成：{SavePath}（.meta 记得随文件一起入库）");
    }

    // ===== 构建辅助（与 ArenaPopupPrefabBuilder 同款最小件，本文件自持一份）=====

    static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.type = type;
        return img;
    }

    static Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, int fontSize, Color color, Sprite sprite, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
        var text = MakeText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, TxtMain, font);
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    static void SetRT(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
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

    static Vector2 V(float x, float y) => new Vector2(x, y);
    static Vector2 V2(float x, float y) => new Vector2(x, y);
}
