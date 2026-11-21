using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性脚手架：按参考图生成宠物界面预制体 Assets/Resources/Prefabs/Town/PetUI.prefab。
/// 菜单：Tools/宠物/生成宠物界面预制体。
///
/// 🔴 项目铁律：主人手改过预制体之后【禁止重跑本生成器】——
/// 重跑 = 排版打回旧版 + 手工节点被抹掉。本脚本只第一次用。
///
/// 📐 契约（2026-11-21 参考图；与《宠物玩法》规格说明书同步）：
/// PetUI(root: Canvas+CanvasScaler+GraphicRaycaster, 保存为 inactive)
/// └─ Root
///    └─ Panel (660×1160)
///       ├─ TitlePlate → TitleText「宠物」 / CloseBtn{Label}
///       ├─ PetList → UpArrow / DownArrow / Slot0..4{Portrait, LockIcon}
///       │            （运行时按 5 只宠物填头像、按解锁态显示 LockIcon）
///       ├─ Stage → NamePlate{ElemIcon, NameText} / LvText /
///       │          PetArt（宠物立绘，运行时按本体/皮肤填，先空） / DeployBtn{Label「上阵」}
///       ├─ InfoPanel → InfoTitle / HpIcon+HpText / AtkIcon+AtkText / DefIcon+DefText /
///       │              PowerIcon+PowerText / StarIcon / Star0..Star4
///       └─ SkillPanel → SkillTitle / SkillSlot0{Icon}
///                       （每只宠物只有 1 个技能：17:53 主人拍板；图标由美术生成后拖入）
///
/// 美术来源：面板/按钮 9-slice 从 EvacuateConfirmPopup 现拉；星/锁/属性图标走 Resources.Load；
/// 拉不到就 LogWarning + 纯色顶替（主人可在 prefab 里手换），不猜 guid。
/// 字号只用 24/36/48（fusion-pixel 12px 点阵整倍数）；字体 GameFonts.GetChinese()。
/// 颜色只做骨架区分，运行时脚本按解锁/星级覆盖；本生成器不挂任何自定义组件（避免 Missing Script）。
/// </summary>
public static class PetUIPrefabBuilder
{
    const string SavePath = "Assets/Resources/Prefabs/Town/PetUI.prefab";
    const string RefPrefabPath = "Prefabs/Battle/EvacuateConfirmPopup";

    const float PanelW = 660f;
    const float PanelH = 1160f;

    // 骨架配色（深紫面板 + 金字，主人可在 prefab 里手换）
    static readonly Color PanelColor = new Color(0.14f, 0.13f, 0.20f, 0.98f);
    static readonly Color PlateWood = new Color(0.46f, 0.31f, 0.18f, 1f);
    static readonly Color PlatePurple = new Color(0.26f, 0.23f, 0.40f, 1f);
    static readonly Color SlotBg = new Color(0.20f, 0.19f, 0.28f, 1f);
    static readonly Color SubPanel = new Color(0.22f, 0.20f, 0.30f, 0.98f);
    static readonly Color SkillSlotBg = new Color(0.17f, 0.22f, 0.34f, 1f);
    static readonly Color BtnAmber = new Color(0.86f, 0.60f, 0.16f, 1f);
    static readonly Color BtnRed = new Color(0.70f, 0.18f, 0.16f, 1f);
    static readonly Color TxtGold = new Color(1f, 0.82f, 0.36f, 1f);
    static readonly Color TxtMain = Color.white;
    static readonly Color TxtSub = new Color(0.80f, 0.82f, 0.92f, 1f);

    [MenuItem("Tools/宠物/生成宠物界面预制体")]
    public static void MenuBuild()
    {
        if (File.Exists(SavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "宠物界面预制体已存在",
                "PetUI.prefab 已存在。\n\n" +
                "⚠️ 项目铁律：主人手改过预制体后重跑生成器 = 排版打回旧版、手工节点被抹掉。\n\n" +
                "只在第一次生成时用本菜单；已手调过就取消，别覆盖。确定覆盖？",
                "覆盖重来", "取消");
            if (!overwrite) return;
        }
        Build();
    }

    /// <summary>batchmode 入口：Tuanjie.exe -batchmode -quit -projectPath ... -executeMethod PetUIPrefabBuilder.Build</summary>
    public static void Build()
    {
        var font = GameFonts.GetChinese();
        if (font == null) font = GameFonts.GetDefault();

        Sprite panelSprite = null, buttonSprite = null;
        var refGo = Resources.Load<GameObject>(RefPrefabPath);
        if (refGo != null)
        {
            var pt = UiFind.Deep(refGo.transform, "Panel");
            if (pt != null) { var img = pt.GetComponent<Image>(); if (img != null) panelSprite = img.sprite; }
            var bt = UiFind.Deep(refGo.transform, "EvacuateButton");
            if (bt != null) { var img = bt.GetComponent<Image>(); if (img != null) buttonSprite = img.sprite; }
        }
        if (panelSprite == null) Debug.LogWarning("[PetUIBuilder] 没拉到面板 9-slice，用纯色，主人可在 prefab 里手换");
        if (buttonSprite == null) Debug.LogWarning("[PetUIBuilder] 没拉到按钮图，用纯色，主人可在 prefab 里手换");

        var starOn = Load("UI/Art/Common/满星", "满星");
        var starOff = Load("UI/Art/Common/空星", "空星");
        var lockIcon = Load("UI/Common/锁", "锁");
        var hpIcon = Load("UI/Art/职业选择/血量", "血量");
        if (hpIcon == null) hpIcon = Load("UI/Art/Icons/表情包/爱心", "爱心");
        var atkIcon = Load("UI/Icons/Job/物攻", "物攻");
        var defIcon = Load("UI/Icons/Job/防御", "防御");
        var powerIcon = Load("UI/Art/Common/对战积分", "战力爪印"); // 资源缺失则 LogWarning，主人手换

        // ── 根 ──
        var root = new GameObject("PetUI", typeof(RectTransform));
        root.AddComponent<Canvas>();
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        Stretch(root.GetComponent<RectTransform>());

        var rootChild = MakeImage(root.transform, "Root", Color.clear);
        Stretch(rootChild.rectTransform);

        var dim = MakeImage(rootChild.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = MakeImage(rootChild.transform, "Panel", PanelColor, panelSprite, Image.Type.Sliced);
        SetRT(panel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(PanelW, PanelH));
        var p = panel.transform;

        // ── 标题牌 + 关闭 ──
        var titlePlate = MakeImage(p, "TitlePlate", PlateWood, panelSprite, Image.Type.Sliced);
        SetRT(titlePlate.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 530f), V2(300f, 72f));
        var titleText = MakeText(titlePlate.transform, "TitleText", "宠物", 48, TextAnchor.MiddleCenter, TxtGold, font);
        Stretch(titleText.rectTransform);
        var closeBtn = MakeButton(p, "CloseBtn", "×", 36, BtnRed, buttonSprite, font);
        SetRT(closeBtn.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(292f, 530f), V2(56f, 56f));

        // ── 左侧宠物列表（5 槽）──
        var list = new GameObject("PetList", typeof(RectTransform));
        list.transform.SetParent(p, false);
        SetRT(list.GetComponent<RectTransform>(), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-238f, 40f), V2(150f, 900f));
        var up = MakeText(list.transform, "UpArrow", "▲", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(up.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 428f), V2(60f, 32f));
        var down = MakeText(list.transform, "DownArrow", "▼", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(down.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -428f), V2(60f, 32f));
        float[] slotY = { 360f, 208f, 56f, -96f, -248f };
        for (int i = 0; i < 5; i++)
        {
            var slot = MakeImage(list.transform, "Slot" + i, SlotBg, panelSprite, Image.Type.Sliced);
            SetRT(slot.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, slotY[i]), V2(128f, 140f));
            var portrait = MakeImage(slot.transform, "Portrait", Color.white);
            SetRT(portrait.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 8f), V2(104f, 104f));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var lk = MakeImage(slot.transform, "LockIcon", Color.white, lockIcon);
            SetRT(lk.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(34f, -44f), V2(30f, 44f));
            lk.preserveAspect = true;
            lk.raycastTarget = false;
            slot.gameObject.SetActive(i < 4); // 参考图可见 4 个，第 5 个由运行时按需激活/滚动
        }

        // ── 中间展示区 ──
        var stage = new GameObject("Stage", typeof(RectTransform));
        stage.transform.SetParent(p, false);
        SetRT(stage.GetComponent<RectTransform>(), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(74f, 250f), V2(400f, 760f));

        var namePlate = MakeImage(stage.transform, "NamePlate", PlatePurple, panelSprite, Image.Type.Sliced);
        SetRT(namePlate.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 348f), V2(280f, 56f));
        var elemIcon = MakeImage(namePlate.transform, "ElemIcon", new Color(0.35f, 0.70f, 1f, 1f));
        SetRT(elemIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(-104f, 0f), V2(28f, 28f));
        elemIcon.raycastTarget = false;
        var nameText = MakeText(namePlate.transform, "NameText", "修勾", 36, TextAnchor.MiddleCenter, TxtMain, font);
        SetRT(nameText.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(58f, 0f), V2(280f, 44f));
        var lvText = MakeText(stage.transform, "LvText", "Lv.1", 24, TextAnchor.MiddleCenter, TxtSub, font);
        SetRT(lvText.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 306f), V2(120f, 32f));

        var petArt = MakeImage(stage.transform, "PetArt", Color.clear);
        SetRT(petArt.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 30f), V2(320f, 480f));
        petArt.preserveAspect = true;
        petArt.raycastTarget = false;

        var deployBtn = MakeButton(stage.transform, "DeployBtn", "上阵", 36, BtnAmber, buttonSprite, font);
        SetRT(deployBtn.image.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(140f, -232f), V2(132f, 72f));

        // ── 宠物信息面板 ──
        var infoPanel = MakeImage(p, "InfoPanel", SubPanel, panelSprite, Image.Type.Sliced);
        SetRT(infoPanel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -278f), V2(620f, 268f));
        var infoTitle = MakeText(infoPanel.transform, "InfoTitle", "宠物信息", 36, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(infoTitle.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 104f), V2(180f, 44f));
        BuildAttrRow(infoPanel.transform, "Hp", "生命", hpIcon, font, V2(-180f, 48f));
        BuildAttrRow(infoPanel.transform, "Atk", "攻击", atkIcon, font, V2(-180f, -8f));
        BuildAttrRow(infoPanel.transform, "Def", "防御", defIcon, font, V2(-180f, -64f));
        var powerIconImg = MakeImage(infoPanel.transform, "PowerIcon", Color.white, powerIcon);
        SetRT(powerIconImg.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(92f, 48f), V2(32f, 32f));
        powerIconImg.preserveAspect = true;
        powerIconImg.raycastTarget = false;
        var powerText = MakeText(infoPanel.transform, "PowerText", "战力 0", 24, TextAnchor.MiddleLeft, TxtGold, font);
        SetRT(powerText.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(176f, 48f), V2(200f, 36f));
        var starIcon = MakeImage(infoPanel.transform, "StarIcon", Color.white, starOn);
        SetRT(starIcon.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(96f, -24f), V2(28f, 28f));
        starIcon.preserveAspect = true;
        starIcon.raycastTarget = false;
        for (int i = 0; i < 3; i++)
        {
            var star = MakeImage(infoPanel.transform, "Star" + i, Color.white, i == 0 ? starOn : starOff);
            SetRT(star.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(152f + i * 44f, -24f), V2(36f, 36f));
            star.preserveAspect = true;
            star.raycastTarget = false;
        }

        // ── 技能面板 ──
        var skillPanel = MakeImage(p, "SkillPanel", SubPanel, panelSprite, Image.Type.Sliced);
        SetRT(skillPanel.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -492f), V2(620f, 172f));
        var skillTitle = MakeText(skillPanel.transform, "SkillTitle", "宠物技能", 36, TextAnchor.MiddleCenter, TxtGold, font);
        SetRT(skillTitle.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 60f), V2(180f, 44f));
        // 每只宠物只有 1 个技能（17:53 拍板），技能区只放 1 槽，居中；图标由美术生成后拖入 Icon
        var skillSlot = MakeImage(skillPanel.transform, "SkillSlot0", SkillSlotBg, panelSprite, Image.Type.Sliced);
        SetRT(skillSlot.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -14f), V2(104f, 104f));
        var skillIcon = MakeImage(skillSlot.transform, "Icon", Color.white);
        Stretch(skillIcon.rectTransform);
        skillIcon.preserveAspect = true;
        skillIcon.raycastTarget = false;

        // 保存为 inactive（与其他弹窗一致）
        root.SetActive(false);
        var dir = Path.GetDirectoryName(SavePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PetUIBuilder] 已生成 " + SavePath + "（inactive）。生成一次后禁重跑；技能图标请由美术生成后拖入 SkillSlot*/Icon。");
    }

    static void BuildAttrRow(Transform parent, string prefix, string label, Sprite icon, Font font, Vector2 pos)
    {
        var ic = MakeImage(parent, prefix + "Icon", Color.white, icon);
        SetRT(ic.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(pos.x - 112f, pos.y), V2(28f, 28f));
        ic.preserveAspect = true;
        ic.raycastTarget = false;
        var lb = MakeText(parent, prefix + "Label", label, 24, TextAnchor.MiddleLeft, TxtSub, font);
        SetRT(lb.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(pos.x - 74f, pos.y), V2(64f, 36f));
        var val = MakeText(parent, prefix + "Text", "0", 24, TextAnchor.MiddleLeft, TxtMain, font);
        SetRT(val.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(pos.x + 8f, pos.y), V2(140f, 36f));
    }

    static Sprite Load(string path, string label)
    {
        var s = Resources.Load<Sprite>(path);
        if (s == null) Debug.LogWarning("[PetUIBuilder] 图标没加载到 " + label + "：" + path + "（主人可手拖）");
        return s;
    }

    struct UiButton { public Image image; public Button button; }
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null) { img.sprite = sprite; img.type = type; }
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
