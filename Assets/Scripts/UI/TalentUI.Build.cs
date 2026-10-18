using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：编辑器建树与通用小件（BuildHierarchyForPrefab/Build*/Create*/LoadArtSprites）
public partial class TalentUI
{

    /// <summary>编辑器首次建树；已换美术的预制体勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        LoadArtSprites();
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var dim = CreateImage(transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var panel = CreateImage(transform, "Panel", Color.white, sprPanelBg, false);
        SetAnchored(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700f, 1240f));

        var titleBar = CreateRect(panel.transform, "TitleBar").GetComponent<RectTransform>();
        SetAnchored(titleBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -58f), new Vector2(280f, 52f));
        var title = CreateText(titleBar, "TitleText", "天赋", 36, new Color(1f, 0.93f, 0.72f));
        Stretch(title.rectTransform);

        var close = CreateImage(panel.transform, "CloseButton", Color.white, sprClose, true);
        SetAnchored(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -18f), new Vector2(64f, 64f));
        close.gameObject.AddComponent<Button>().targetGraphic = close;

        // Resource row
        var res = CreateRect(panel.transform, "ResourceRow");
        SetAnchored(res.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -118f), new Vector2(520f, 40f));
        var goldIcon = CreateImage(res.transform, "GoldIcon", Color.white, sprCheckOn, true);
        SetAnchored(goldIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(32f, 32f));
        var goldTxt = CreateText(res.transform, "GoldText", "999999+", 24, new Color(1f, 0.95f, 0.7f));
        goldTxt.alignment = TextAnchor.MiddleLeft;
        SetAnchored(goldTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(52f, 0f), new Vector2(140f, 36f));

        var stoneIcon = CreateImage(res.transform, "StoneIcon", Color.white, sprDiamondOn, true);
        SetAnchored(stoneIcon.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(36f, 36f));
        var stoneTxt = CreateText(res.transform, "StoneText", "999+", 24, new Color(0.9f, 0.8f, 1f));
        stoneTxt.alignment = TextAnchor.MiddleLeft;
        SetAnchored(stoneTxt.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(44f, 0f), new Vector2(100f, 36f));
        var stonePlus = CreateImage(res.transform, "StonePlus", new Color(0.4f, 0.7f, 0.35f, 1f));
        SetAnchored(stonePlus.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(150f, 0f), new Vector2(36f, 36f));
        stonePlus.gameObject.AddComponent<Button>().targetGraphic = stonePlus;
        var plusTxt = CreateText(stonePlus.transform, "Label", "+", 28, Color.white);
        Stretch(plusTxt.rectTransform);

        // Columns
        var columns = CreateRect(panel.transform, "Columns");
        var colRt = columns.GetComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0f, 0f);
        colRt.anchorMax = new Vector2(1f, 1f);
        colRt.offsetMin = new Vector2(36f, 168f);
        colRt.offsetMax = new Vector2(-36f, -168f);

        BuildLeftColumn(columns.transform);
        BuildRightColumn(columns.transform);
        BuildFooter(panel.transform);
        BuildTemplates(transform);
        BuildChoicePopup(transform);

        AutoBindFromHierarchy();
        closeButton = panel.transform.Find("CloseButton")?.GetComponent<Button>();
        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildLeftColumn(Transform columns)
    {
        var left = CreateImage(columns, "LeftColumn", new Color(1f, 1f, 1f, 0.04f));
        var lrt = left.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.offsetMin = new Vector2(0f, 0f);
        lrt.offsetMax = new Vector2(-4f, 0f);

        var head = CreateImage(left.transform, "Header", new Color(0.55f, 0.5f, 0.45f, 1f));
        SetAnchored(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(260f, 40f));
        var headTxt = CreateText(head.transform, "Label", "属性天赋", 24, Color.white);
        Stretch(headTxt.rectTransform);

        var scrollGo = CreateRect(left.transform, "LeftScroll");
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(4f, 50f);
        srt.offsetMax = new Vector2(-4f, -46f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateImage(scrollGo.transform, "Viewport", new Color(1f, 1f, 1f, 0.02f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = CreateRect(viewport.transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 800f);
        scroll.viewport = viewport.rectTransform;
        scroll.content = crt;

        var goldBar = CreateImage(left.transform, "GoldBar", Color.white, sprGoldBar, true);
        SetAnchored(goldBar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 8f), new Vector2(90f, 36f));
        var tip = CreateText(left.transform, "LeftTip", "消耗金币解锁属性天赋", 16, new Color(0.45f, 0.22f, 0.12f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(10f, 8f), new Vector2(-100f, 32f));
        var cost = CreateText(goldBar.transform, "LeftCostText", "0", 18, new Color(1f, 0.93f, 0.55f));
        cost.alignment = TextAnchor.MiddleCenter;
        Stretch(cost.rectTransform);
    }

    void BuildRightColumn(Transform columns)
    {
        var right = CreateImage(columns, "RightColumn", new Color(1f, 1f, 1f, 0.04f));
        var rrt = right.rectTransform;
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.offsetMin = new Vector2(4f, 0f);
        rrt.offsetMax = new Vector2(0f, 0f);

        var head = CreateImage(right.transform, "Header", new Color(0.45f, 0.35f, 0.6f, 1f));
        SetAnchored(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(280f, 40f));
        var headTxt = CreateText(head.transform, "Label", "辅助/专精天赋", 22, Color.white);
        Stretch(headTxt.rectTransform);

        var scrollGo = CreateRect(right.transform, "RightScroll");
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(4f, 50f);
        srt.offsetMax = new Vector2(-4f, -46f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateImage(scrollGo.transform, "Viewport", new Color(1f, 1f, 1f, 0.02f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = CreateRect(viewport.transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 800f);
        scroll.viewport = viewport.rectTransform;
        scroll.content = crt;

        var stoneBar = CreateImage(right.transform, "StoneBar", Color.white, sprStoneBar, true);
        SetAnchored(stoneBar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 8f), new Vector2(90f, 36f));
        var tip = CreateText(right.transform, "RightTip", "消耗天赋石解锁辅助/专精天赋", 15, new Color(0.32f, 0.18f, 0.5f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(10f, 8f), new Vector2(-100f, 32f));
        var cost = CreateText(stoneBar.transform, "RightCostText", "0", 18, new Color(0.92f, 0.82f, 1f));
        cost.alignment = TextAnchor.MiddleCenter;
        Stretch(cost.rectTransform);
    }

    void BuildFooter(Transform panel)
    {
        var footer = CreateImage(panel, "Footer", Color.white, sprFooter, false);
        footer.type = Image.Type.Sliced;
        SetAnchored(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(620f, 118f));

        var label = CreateText(footer.transform, "SumLabel", "已获得属性加成", 20, new Color(0.75f, 0.95f, 0.7f));
        label.alignment = TextAnchor.MiddleLeft;
        SetAnchored(label.rectTransform, new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -8f), new Vector2(0f, 28f));

        // 2026-09-29：左列要能看到**两条攻击**，故 5 格 → 6 格，间距 0.18→0.15（CreateSum 内宽度同步 0.16→0.145 防重叠）。
        // 2026-10-10：左列第 4 槽「智力」已改成第二个「攻击」槽，且攻击天赋是**双攻**（Attack + MagicAttack 都加），
        // 所以下面 SumAttack / SumMagic 两格现在**都会**被攻击类节点累加，不再是「力量只喂物攻、智力只喂魔攻」。
        CreateSum(footer.transform, "SumAttack", "+0", 0f);      // 力量 → 物攻
        CreateSum(footer.transform, "SumMagic", "+0", 0.15f);    // 智力 → 魔攻
        CreateSum(footer.transform, "SumHp", "+0", 0.30f);
        CreateSum(footer.transform, "SumDef", "+0", 0.45f);
        CreateSum(footer.transform, "SumCrit", "+0%", 0.60f);
        CreateSum(footer.transform, "SumAtkSpd", "+0%", 0.75f);

        var reset = CreateImage(footer.transform, "ResetButton", Color.white, sprReset, false);
        SetAnchored(reset.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, -6f), new Vector2(196f, 70f));
        reset.gameObject.AddComponent<Button>().targetGraphic = reset;
        var resetTxt = CreateText(reset.transform, "Label", "重置天赋", 24, new Color(1f, 0.92f, 0.75f));
        Stretch(resetTxt.rectTransform);
    }

    static void CreateSum(Transform footer, string name, string value, float xNorm)
    {
        var t = CreateText(footer, name, value, 20, new Color(0.7f, 0.95f, 0.65f));
        t.alignment = TextAnchor.MiddleCenter;
        SetAnchored(t.rectTransform, new Vector2(xNorm, 0f), new Vector2(xNorm + 0.145f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    void BuildTemplates(Transform root)
    {
        var left = CreateImage(root, "LeftNodeTemplate", Color.white, sprLeftCard, false);
        left.gameObject.SetActive(false);
        SetAnchored(left.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 100f));
        left.gameObject.AddComponent<Button>().targetGraphic = left;

        var line = CreateImage(left.transform, "Line", Color.white, sprLeftLinkOn, false);
        SetAnchored(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(46f, -28f), new Vector2(12f, 56f));
        line.raycastTarget = false;

        var icon = CreateImage(left.transform, "Icon", Color.white, sprLeftHexOff, true);
        // 左对齐内缩，避免被卡片 Mask/边框裁掉一半
        SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(52f, 0f), new Vector2(56f, 56f));
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        var name = CreateText(left.transform, "NameText", "力量 I", 22, new Color(0.25f, 0.15f, 0.1f));
        name.alignment = TextAnchor.MiddleLeft;
        SetAnchored(name.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
            new Vector2(88f, 0f), new Vector2(-128f, 0f));
        var effect = CreateText(left.transform, "EffectText", "攻击 +3", 18, new Color(0.4f, 0.28f, 0.18f));
        effect.alignment = TextAnchor.MiddleLeft;
        SetAnchored(effect.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Vector2(0f, 0.5f),
            new Vector2(88f, 0f), new Vector2(-128f, 0f));
        var check = CreateImage(left.transform, "Check", Color.white, sprCheckOff, true);
        SetAnchored(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f), new Vector2(32f, 28f));
        check.raycastTarget = false;

        var right = CreateImage(root, "RightRowTemplate", Color.white, sprRightCard, false);
        right.gameObject.SetActive(false);
        SetAnchored(right.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 118f));

        var rline = CreateImage(right.transform, "Line", Color.white, sprRightLinkOn, false);
        SetAnchored(rline.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(18f, -16f), new Vector2(10f, 72f));
        rline.raycastTarget = false;
        var diamond = CreateImage(right.transform, "Diamond", Color.white, sprDiamondOff, true);
        SetAnchored(diamond.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(18f, 8f), new Vector2(30f, 26f));
        diamond.raycastTarget = false;

        var title = CreateText(right.transform, "TitleText", "武器专精", 18, new Color(0.22f, 0.12f, 0.32f));
        title.alignment = TextAnchor.MiddleLeft;
        SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.72f, 1f), new Vector2(0f, 1f),
            new Vector2(38f, -4f), new Vector2(0f, 26f));
        var cost = CreateText(right.transform, "CostText", "12", 16, new Color(0.42f, 0.22f, 0.62f));
        cost.alignment = TextAnchor.MiddleRight;
        SetAnchored(cost.rectTransform, new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-36f, -4f), new Vector2(0f, 26f));
        var lockImg = CreateImage(right.transform, "Lock", Color.white, sprLock, true);
        SetAnchored(lockImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-6f, 8f), new Vector2(28f, 40f));
        lockImg.raycastTarget = false;

        for (int o = 0; o < 3; o++)
        {
            var opt = CreateImage(right.transform, "Opt_" + o, Color.white, o == 1 ? sprRightHexAlt : sprRightHex, true);
            SetAnchored(opt.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f + o * 68f, 8f), new Vector2(62f, 62f));
            opt.gameObject.AddComponent<Button>().targetGraphic = opt;
            var oi = CreateImage(opt.transform, "Icon", new Color(1f, 1f, 1f, 0.15f));
            SetAnchored(oi.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(36f, 36f));
            oi.raycastTarget = false;
            var ol = CreateText(opt.transform, "Label", "选项", 12, Color.white);
            SetAnchored(ol.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero);
        }
    }

    void BuildChoicePopup(Transform root)
    {
        var pop = CreateImage(root, "ChoicePopup", new Color(0f, 0f, 0f, 0.65f));
        Stretch(pop.rectTransform);
        pop.gameObject.SetActive(false);
        var box = CreateImage(pop.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(480f, 420f));
        var title = CreateText(pop.transform, "Title", "选择天赋", 28, new Color(0.25f, 0.15f, 0.1f));
        // Title under popup root for AutoBind path ChoicePopup/Title
        SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 160f), new Vector2(400f, 40f));
        for (int i = 0; i < 3; i++)
        {
            var c = CreateImage(pop.transform, "Choice_" + i, new Color(0.55f, 0.35f, 0.55f, 1f));
            SetAnchored(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f - i * 90f), new Vector2(400f, 72f));
            c.gameObject.AddComponent<Button>().targetGraphic = c;
            var lab = CreateText(c.transform, "Label", "选项", 22, Color.white);
            Stretch(lab.rectTransform);
        }
        var cancel = CreateImage(pop.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -170f), new Vector2(200f, 48f));
        cancel.gameObject.AddComponent<Button>().targetGraphic = cancel;
        var ct = CreateText(cancel.transform, "Label", "取消", 24, Color.white);
        Stretch(ct.rectTransform);
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        return CreateImage(parent, name, color, null, false);
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite, bool preserveAspect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : color;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = true;
        return img;
    }

    void LoadArtSprites()
    {
        EnsureSprite(ref sprPanelBg, "天赋_0020_bg");
        EnsureSprite(ref sprClose, "天赋_0007_关闭");
        EnsureSprite(ref sprLeftCard, "天赋_0006_属性底");
        EnsureSprite(ref sprRightCard, "天赋_0000_技能底");
        EnsureSprite(ref sprFooter, "天赋_0000s_0002_底条");
        EnsureSprite(ref sprReset, "重置天赋亮");
        EnsureSprite(ref sprGoldBar, "天赋_0001_金币升级");
        EnsureSprite(ref sprStoneBar, "天赋_0013_天赋石升级");
        EnsureSprite(ref sprLeftHexOff, "天赋_0004_基础属性未解锁");
        EnsureSprite(ref sprLeftHexOn, "天赋_0005_基础属性解锁");
        EnsureSprite(ref sprCheckOff, "天赋_0011_不可升级");
        EnsureSprite(ref sprCheckOn, "天赋_0012_可升级");
        EnsureSprite(ref sprLeftLinkOff, "天赋_0009_链接2");
        EnsureSprite(ref sprLeftLinkOn, "天赋_0008_lianjie3");
        EnsureSprite(ref sprRightHex, "天赋_0002_图层-2");
        EnsureSprite(ref sprRightHexAlt, "天赋_0003_图层-3");
        EnsureSprite(ref sprDiamondOff, "天赋_0016_技能可用-拷贝");
        EnsureSprite(ref sprDiamondOn, "天赋_0018_技能可用");
        EnsureSprite(ref sprRightLinkOff, "天赋_0017_技能链接-拷贝");
        EnsureSprite(ref sprRightLinkOn, "天赋_0019_技能链接");
        EnsureSprite(ref sprLock, "图层 3");
        if (sprLock == null)
            sprLock = Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (sprLock == null)
                sprLock = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
        }
#endif
        EnsureSprite(ref sprArrow, "天赋_0015_箭头");
    }

    static void EnsureSprite(ref Sprite field, string fileStem)
    {
        if (field != null) return;
        field = Resources.Load<Sprite>("UI/Talent/" + fileStem);
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (field == null)
                field = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/Talent/" + fileStem + ".png");
        }
#endif
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void SetAnchored(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
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
