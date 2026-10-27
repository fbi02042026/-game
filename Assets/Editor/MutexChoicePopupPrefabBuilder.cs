#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 流派·专精二选一弹窗预制体生成器（2026-10-20 主人拍板「把这个界面正式化 并生成预制体」；
/// 【2026-10-20 追问「不能用通用的弹窗吗」】→ 套全项目默认弹窗（EvacuateConfirmPopup）同一套皮）。
///
/// 菜单：Tools/UI/生成流派专精弹窗预制体
/// 一键生成 Assets/Resources/Prefabs/Talent/MutexChoicePopup.prefab。
///
/// 通用确认弹窗（StaminaConfirmPopup.Show）只有「标题+正文+确认/取消」，
/// 塞不下两张可选卡与动态费用行，所以不能纯复用；本预制体做成默认弹窗家族成员：
///   底框   角色_0002s_0010_图层-7.png（= 默认弹窗 Panel 同一张图，九宫格 Sliced）
///   标题   同款样式：顶中 (0,-36) 白字 30 号（无标头横幅）
///   按钮   取消按钮.png / 确定按钮.png（默认弹窗同款，原生 100×33、底锚 0.28/0.72 y=28）
///   选项卡 通用窗体2.png（九宫格）；费用图标 天赋石.png；卡图标 物理专精/魔法专精
///
/// 节点层级与 TalentUI.Popups.EnsureMutexChoicePopup 的绑定路径**必须完全一致**：
///   MutexChoicePopup (root: Canvas + GraphicRaycaster，运行时再套 UICanvasSetup.ApplyPopup)
///   ├─ Dim            全屏半透明黑
///   └─ Box            默认弹窗同款底框 640×560
///      ├─ Title       「流派·专精」（默认弹窗同款顶中白字）
///      ├─ Desc        软引导三行
///      ├─ Opt_0       物理专精卡（Icon / Label，Button）
///      ├─ Opt_1       魔法专精卡（Icon / Label，Button）
///      ├─ CostIcon    天赋石小图
///      ├─ CostLine    消耗行（运行时填「消耗天赋石：N（持有 M…）」）
///      ├─ Cancel      取消按钮（Label「取消」，Button）
///      └─ Confirm     确定按钮（Label「确定解锁」，Button）
///
/// 🔴 一次性脚手架（铁律 22）：预制体一旦被主人手工调整过，**禁止再跑本菜单覆盖**
/// （会把手工排版打回旧版）。之后要调排版直接改预制体。
/// </summary>
public class MutexChoicePopupPrefabBuilder
{
    /// <summary>与 TalentUI.Popups.MutexChoicePopupPrefabPath **必须同值**。</summary>
    public const string PrefabPath = "Assets/Resources/Prefabs/Talent/MutexChoicePopup.prefab";

    const int SORT_ORDER = 900;   // GameConfig.UiSort.TownPopup（运行时 ApplyPopup 会再套一次同值）

    // ---- 美术（全部 hex32 guid，可直接被预制体按 guid 引用；56 字符资产一律不用）----
    const string ART_PANEL    = "Assets/Resources/UI/NavCharacter/角色_0002s_0010_图层-7.png"; // 默认弹窗 Panel 同款
    const string ART_CARD     = "Assets/Resources/UI/Art/Common/通用窗体2.png";
    const string ART_OK       = "Assets/Resources/UI/Art/Common/确定按钮.png";
    const string ART_CANCEL   = "Assets/Resources/UI/Art/Common/取消按钮.png";
    const string ART_STONE    = "Assets/Art/UI/Talent/天赋石.png";
    const string ART_OPT_PHYS = "Assets/Resources/UI/TalentIcons/物理专精.png";
    const string ART_OPT_MAGIC = "Assets/Resources/UI/TalentIcons/魔法专精.png";

    // ---- 版面（Box 640×560，中心坐标系；底框九宫格内区约 x±254 / y -248..248）----
    const float BOX_W = 640f;
    const float BOX_H = 560f;

    [MenuItem("Tools/UI/生成流派专精弹窗预制体")]
    public static void Generate()
    {
        // 资源前置检查：缺一张就整个不动盘（铁律 14：不静默跳过、不拿白块凑数）
        string[] required =
        {
            ART_PANEL, ART_CARD, ART_OK, ART_CANCEL,
            ART_STONE, ART_OPT_PHYS, ART_OPT_MAGIC
        };
        foreach (var p in required)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(p) == null)
            {
                Debug.LogError("[MutexChoicePopupPrefabBuilder] 缺少美术：" + p + "，已取消生成。");
                EditorUtility.DisplayDialog("生成取消", "缺少美术资源：\n" + p + "\n\n请先补图再生成。", "确定");
                return;
            }
        }

        EnsureFolders();

        var dir = Path.GetDirectoryName(PrefabPath);
        bool existed = File.Exists(PrefabPath);

        GameObject root = existed
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("MutexChoicePopup",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        // 增量口径：清掉旧子树重建（本生成器只服务首次落地；手工调整后不得再跑）
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        Build(root);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        if (existed) PrefabUtility.UnloadPrefabContents(root);
        else Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MutexChoicePopupPrefabBuilder] 流派专精弹窗预制体已生成：" + PrefabPath
                  + (existed ? "（覆盖重建）" : "（首次创建）"));
        EditorUtility.DisplayDialog("生成完成",
            "流派·专精弹窗预制体已生成！\n\n路径：" + PrefabPath
            + "\n\n节点名已与 TalentUI.Popups 绑定路径对齐；进游戏点「流派·物理/魔法专精」即可验收。", "确定");
    }

    static void Build(GameObject root)
    {
        // ---- 根：Canvas（ScreenSpaceCamera + 900 层）+ Scaler + GraphicRaycaster ----
        // 🔴 不用 `??`：UnityEngine.Object 的假 null 不走 ?? 的引用判空，真机出过
        // MissingComponentException（2026-10-20 主人跑菜单报错）。一律显式 == null（Unity 重载判定），
        // 与 QuestPanelPrefabGenerator 的成熟写法同口径。
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SORT_ORDER;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = 1f;
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();   // 铁律 10：每个 Canvas 必补
        Stretch(root.transform as RectTransform);

        // ---- Dim ----
        var dim = Img(root.transform, "Dim", null, false);
        dim.color = new Color(0f, 0f, 0f, 0.62f);
        Stretch(dim.rectTransform);

        // ---- Box 底框：默认弹窗 Panel 同一张图、同款半透明白染色 ----
        var box = Img(root.transform, "Box", Load(ART_PANEL), true);
        box.color = new Color(1f, 1f, 1f, 0.98f);
        Anch(box.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, new Vector2(BOX_W, BOX_H));

        // ---- 标题：默认弹窗同款（顶中 0,-36、pivot 顶、白字 30、无标头横幅）----
        var title = Txt(box.transform, "Title", "流派·专精", 30, Color.white);
        Anch(title.rectTransform, V(0.5f, 1f), V(0.5f, 1f), V(0.5f, 1f), new Vector2(0f, -36f), new Vector2(460f, 44f));

        // ---- 软引导（文案与旧版逐字一致；默认弹窗 Body 同为顶中往下挂）----
        var desc = Txt(box.transform, "Desc",
            "第一次开启流派专精：物理与魔法二选一。\n" +
            "· 只提升所选流派攻击的伤害加成，不影响你使用其它职业；\n" +
            "· 另一流派将锁定，想更换可洗点后重选。",
            20, new Color(0.92f, 0.9f, 0.84f));
        desc.alignment = TextAnchor.UpperLeft;
        desc.lineSpacing = 1.15f;
        Anch(desc.rectTransform, V(0.5f, 1f), V(0.5f, 1f), V(0.5f, 1f), new Vector2(0f, -100f), new Vector2(540f, 110f));

        // ---- 两张选项卡（物理=左 / 魔法=右）----
        MakeCard(box.transform, 0, "Opt_0", ART_OPT_PHYS, new Vector2(-120f, 20f));
        MakeCard(box.transform, 1, "Opt_1", ART_OPT_MAGIC, new Vector2(120f, 20f));

        // ---- 费用行：天赋石小图 + 左对齐文本（运行时只改 text）----
        var stone = Img(box.transform, "CostIcon", Load(ART_STONE), true);
        Anch(stone.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), new Vector2(-140f, -100f), new Vector2(36f, 36f));
        var cost = Txt(box.transform, "CostLine", "消耗天赋石：0（持有 0）", 22, new Color(0.92f, 0.9f, 0.84f));
        cost.alignment = TextAnchor.MiddleLeft;
        Anch(cost.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), new Vector2(-114f, -100f), new Vector2(420f, 40f));

        // ---- 取消 / 确定按钮：默认弹窗同款原生尺寸与摆位（底锚 0.28/0.72，y=28，100×33）----
        MakeButton(box.transform, "Cancel", ART_CANCEL, "取消", 0.28f);
        MakeButton(box.transform, "Confirm", ART_OK, "确定解锁", 0.72f);

        GameFonts.ApplyToHierarchy(root.transform);
    }

    static void MakeCard(Transform box, int index, string name, string iconPath, Vector2 pos)
    {
        var card = Img(box, name, Load(ART_CARD), true);
        Anch(card.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), pos, new Vector2(224f, 150f));
        var btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = card;

        var icon = Img(card.transform, "Icon", Load(iconPath), true);
        Anch(icon.rectTransform, V(0.5f, 1f), V(0.5f, 1f), V(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(56f, 56f));
        icon.raycastTarget = false;

        var label = Txt(card.transform, "Label", index == 0 ? "物理专精" : "魔法专精", 22, Color.white);
        Anch(label.rectTransform, V(0.5f, 0f), V(0.5f, 0f), V(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(210f, 30f));
    }

    /// <summary>默认弹窗同款按钮：原生 100×33、底锚按面板宽度比例（0.28 左 / 0.72 右）、y=28。</summary>
    static void MakeButton(Transform box, string name, string artPath, string caption, float anchorX)
    {
        var img = Img(box, name, Load(artPath), false);
        Anch(img.rectTransform, V(anchorX, 0f), V(anchorX, 0f), V(0.5f, 0f), new Vector2(0f, 28f), new Vector2(100f, 33f));
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var label = Txt(img.transform, "Label", caption, 22, Color.white);
        Stretch(label.rectTransform);
    }

    // ---- 小工具 ----
    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static Image Img(Transform parent, string name, Sprite sprite, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        if (sprite != null && sliced) img.type = Image.Type.Sliced;
        img.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.62f);
        img.raycastTarget = true;
        return img;
    }

    static Text Txt(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static void Anch(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
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

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Talent"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Talent");
        }
    }
}
#endif
