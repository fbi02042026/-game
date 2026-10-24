using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 节点兜底构建：AutoBind、钻石刷新节点、按钮外观采集、以及没有 Prefab 时手搭层级。
/// 【2026-10-23 主人拍板】原 MercenaryRecruitPopupUI.cs 已 1461 行（项目约定 >600 行必须拆），
/// 按职责切成 5 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercenaryRecruitPopupUI : MonoBehaviour
{

    void AutoBind()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (closeButton == null) closeButton = FindBtn("CloseButton");
        if (titleText == null) titleText = FindTxt("Title");
        if (subtitleText == null) subtitleText = FindTxt("Subtitle");
        if (remainText == null) remainText = FindTxt("RemainText");
        if (refreshButton == null) refreshButton = FindBtn("RefreshButton");
        if (refreshCostText == null) refreshCostText = FindTxt("RefreshCost");
        if (autoRefreshText == null) autoRefreshText = FindTxt("AutoRefresh");
        if (_scrollCommonCountText == null || _scrollLegendaryCountText == null)
        {
            // 左下角卷轴数量：预制体「招募卷」下「1」=普通卷、「2」=高级卷，各自子 Label 静态文案「X10000」
            var scrollRow = FindDeep(transform, "招募卷");
            if (scrollRow != null)
            {
                if (_scrollCommonCountText == null)
                    _scrollCommonCountText = scrollRow.Find("1/Label")?.GetComponent<Text>();
                if (_scrollLegendaryCountText == null)
                    _scrollLegendaryCountText = scrollRow.Find("2/Label")?.GetComponent<Text>();
            }
        }
        // 卡上卷钮图标源：普通卷/高级卷各取主人摆的展示位（2026-10-19 修「普通卷图标用错」）
        if (_commonScrollIcon == null)
            _commonScrollIcon = FindDeep(transform, "招募卷")?.Find("1/icon")?.GetComponent<Image>()?.sprite;
        if (_legendaryScrollIcon == null)
            _legendaryScrollIcon = FindDeep(transform, "招募卷")?.Find("2/icon")?.GetComponent<Image>()?.sprite;
        if (infoButton == null)
        {
            // 【2026-10-18 修·主人报「info 按钮不弹概率弹窗」】预制体节点名是「info」不是「信息」，
            // 以前只按「信息」找永远绑不上。两个名字都兼容（代码兜底壳建的节点叫「信息」）。
            var infoTf = FindDeep(transform, "info") ?? FindDeep(transform, "信息");
            if (infoTf != null) infoButton = infoTf.GetComponent<Button>() ?? infoTf.gameObject.AddComponent<Button>();
        }

        if (cards == null || cards.Length < 3) cards = new CardView[3];
        for (int i = 0; i < 3; i++)
        {
            if (cards[i] == null) cards[i] = new CardView();
            var cardTf = FindDeep(transform, "Card" + i);
            if (cardTf == null) continue;
            var c = cards[i];
            c.root = cardTf.gameObject;
            if (c.background == null) c.background = cardTf.GetComponent<Image>();
            if (c.portrait == null) c.portrait = FindDeep(cardTf, "Portrait")?.GetComponent<Image>();
            if (c.roleIcon == null) c.roleIcon = FindDeep(cardTf, "Role")?.GetComponent<Image>();
            // Name (1)=外号，Name=名字
            if (c.nicknameText == null)
            {
                var nick = FindDeep(cardTf, "Name (1)");
                if (nick != null) c.nicknameText = nick.GetComponent<Text>();
            }
            if (c.nameText == null) c.nameText = FindDeep(cardTf, "Name")?.GetComponent<Text>();
            if (c.rarityText == null) c.rarityText = FindDeep(cardTf, "Rarity")?.GetComponent<Text>();
            if (c.skill1Name == null) c.skill1Name = FindDeep(cardTf, "Skill1_name")?.GetComponent<Text>();
            if (c.skill2Name == null) c.skill2Name = FindDeep(cardTf, "Skill2_name")?.GetComponent<Text>();
            if (c.skill1Desc == null) c.skill1Desc = FindDeep(cardTf, "Skill1_内容")?.GetComponent<Text>();
            if (c.skill2Desc == null) c.skill2Desc = FindDeep(cardTf, "Skill2_内容")?.GetComponent<Text>();
            if (c.skill1Icon == null) c.skill1Icon = FindDeep(cardTf, "Skill1icon")?.GetComponent<Image>();
            if (c.skill2Icon == null) c.skill2Icon = FindDeep(cardTf, "Skill2icon")?.GetComponent<Image>();

            if (c.goldButton == null) c.goldButton = FindDeep(cardTf, "ConfirmButton")?.GetComponent<Button>();
            if (c.scrollButton == null) c.scrollButton = FindDeep(cardTf, "ConfirmButton1")?.GetComponent<Button>();
            if (c.scrollButtonImage == null && c.scrollButton != null)
                c.scrollButtonImage = c.scrollButton.GetComponent<Image>();
            if (c.goldLabel == null && c.goldButton != null)
            {
                var g = FindDeep(c.goldButton.transform, "Label");
                if (g != null) c.goldLabel = g.GetComponent<Text>();
            }
            // 卷轴按钮文字：节点名就是 Label（与金币按钮同名，各自按钮下查找）
            if (c.scrollLabel1 == null && c.scrollButton != null)
                c.scrollLabel1 = FindDeep(c.scrollButton.transform, "Label")?.GetComponent<Text>();
            // 按钮内 icon 槽（主人在预制体里已摆好图，代码只按状态切换、不重设默认图）
            if (c.goldIcon == null && c.goldButton != null)
                c.goldIcon = FindDeep(c.goldButton.transform, "icon")?.GetComponent<Image>();
            if (c.scrollIcon == null && c.scrollButton != null)
                c.scrollIcon = FindDeep(c.scrollButton.transform, "icon")?.GetComponent<Image>();
            if (c.scrollIcon != null && c.scrollIconDefault == null)
                c.scrollIconDefault = c.scrollIcon.sprite;
            // 金币钮图标快照（金币钮顶卷钮位切卷图后要还原金币图，2026-10-19）
            if (c.goldIcon != null && c.goldIconDefault == null)
                c.goldIconDefault = c.goldIcon.sprite;
        }
        CaptureButtonLooks();
        EnsureDiamondRefreshNodes();
    }

    /// <summary>
    /// 确保刷新按钮上有「钻石刷新」形态节点（2026-10-23 主人拍板，幂等）：
    /// 左侧钻石 icon（按钮宽 16% 处）+ 右侧价格文字（84% 处）+ 底部「今日剩N次」小字。
    /// 预制体和代码兜底壳两条路径都从这里补节点，不改预制体文件本身。
    /// </summary>
    void EnsureDiamondRefreshNodes()
    {
        if (refreshButton == null) return;
        var btnTf = refreshButton.transform;

        if (_diamondIcon == null)
        {
            var t = btnTf.Find("DiamondIcon");
            _diamondIcon = t != null ? t.GetComponent<Image>() : null;
        }
        if (_diamondIcon == null)
        {
            _diamondIcon = CreateImg(btnTf, "DiamondIcon", Color.white);
            var rt = _diamondIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.16f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(28f, 28f);
            _diamondIcon.sprite = CommonIconSprites.Load("icon_diamond_blue");
            _diamondIcon.preserveAspect = true;
            _diamondIcon.raycastTarget = false;
        }

        if (_diamondCostText == null)
        {
            var t = btnTf.Find("DiamondCost");
            _diamondCostText = t != null ? t.GetComponent<Text>() : null;
        }
        if (_diamondCostText == null)
        {
            _diamondCostText = CreateTxt(btnTf, "DiamondCost", "50", 18, TextAnchor.MiddleCenter);
            var rt = _diamondCostText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.84f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(48f, 24f);
        }

        if (_diamondLeftText == null)
        {
            var t = btnTf.Find("DiamondLeft");
            _diamondLeftText = t != null ? t.GetComponent<Text>() : null;
        }
        if (_diamondLeftText == null)
        {
            _diamondLeftText = CreateTxt(btnTf, "DiamondLeft", "今日剩2次", 11, TextAnchor.MiddleCenter);
            var rt = _diamondLeftText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 5f);
            rt.sizeDelta = new Vector2(160f, 16f);
            _diamondLeftText.color = new Color(1f, 0.92f, 0.6f, 1f);
        }

        // 节点刚建好是全显示状态，立即按当前冷却/次数刷一次形态，避免 icon 常显
        UpdateRefreshCostText();
    }

    /// <summary>
    /// 货币钮外观快照（绑定后、任何填充前调用）：金币钮(ConfirmButton)与卷钮(ConfirmButton1)
    /// 是两套预制体节点，底图/颜色/Label颜色/尺寸都不同 —— 各拍一份，填充时按货币套用，
    /// 保证「金币一种样子、卷轴一种样子」，三张卡看起来完全一致。
    /// </summary>
    void CaptureButtonLooks()
    {
        if (_btnLooksCaptured) return;
        _btnLooksCaptured = true;
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (c.goldButton != null && _goldBtnSprite == null)
            {
                var img = c.goldButton.GetComponent<Image>();
                if (img != null)
                {
                    _goldBtnSprite = img.sprite;
                    _goldBtnColor = img.color;
                    var rt = c.goldButton.transform as RectTransform;
                    if (rt != null) _goldBtnSize = rt.sizeDelta;
                }
                if (c.goldLabel != null) _goldLabelColor = c.goldLabel.color;
            }
            if (c.scrollButton != null && _scrollBtnSprite == null)
            {
                var img = c.scrollButtonImage != null ? c.scrollButtonImage : c.scrollButton.GetComponent<Image>();
                if (img != null)
                {
                    _scrollBtnSprite = img.sprite;
                    _scrollBtnColor = img.color;
                    var rt = c.scrollButton.transform as RectTransform;
                    if (rt != null) _scrollBtnSize = rt.sizeDelta;
                }
                if (c.scrollLabel1 != null) _scrollLabelColor = c.scrollLabel1.color;
            }
        }
    }

    Button FindBtn(string n) => FindDeep(transform, n)?.GetComponent<Button>();
    Text FindTxt(string n) => FindDeep(transform, n)?.GetComponent<Text>();

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    public void BuildFallbackHierarchy()
    {
        EnsureCanvas();
        for (int i = transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(transform.GetChild(i).gameObject);

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImg(root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        var panel = CreateImg(root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(700f, 1120f);

        titleText = CreateTxt(panel.transform, "Title", "招募佣兵", 36, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, 0.5f, 0.92f, 0f, 0f, 400f, 48f);
        closeButton = CreateBtn(panel.transform, "CloseButton", "X", new Vector2(300f, 500f), new Vector2(52f, 52f));
        infoButton = CreateBtn(panel.transform, "信息", "信息", new Vector2(-280f, 500f), new Vector2(80f, 48f));

        cards = new CardView[3];
        for (int i = 0; i < 3; i++)
        {
            var card = CreateImg(panel.transform, "Card" + i, new Color(0.18f, 0.16f, 0.2f, 1f));
            SetRect(card.rectTransform, 0.5f, 0.55f, (i - 1) * 220f, 0f, 200f, 560f);
            var nick = CreateTxt(card.transform, "Name (1)", "外号", 16, TextAnchor.MiddleCenter);
            SetRect(nick.rectTransform, 0.5f, 0.94f, 0f, 0f, 180f, 28f);
            var name = CreateTxt(card.transform, "Name", "名字", 16, TextAnchor.MiddleCenter);
            SetRect(name.rectTransform, 0.5f, 0.88f, 0f, 0f, 180f, 28f);
            var role = CreateImg(card.transform, "Role", Color.white);
            SetRect(role.rectTransform, 0.15f, 0.91f, 0f, 0f, 36f, 36f);
            var portrait = CreateImg(card.transform, "Portrait", new Color(0.3f, 0.3f, 0.32f));
            SetRect(portrait.rectTransform, 0.5f, 0.62f, 0f, 0f, 160f, 200f);
            var goldBtn = CreateBtn(card.transform, "ConfirmButton", "金币", new Vector2(0f, -200f), new Vector2(160f, 44f));
            var scrollBtn = CreateBtn(card.transform, "ConfirmButton1", "招募卷", new Vector2(0f, -250f), new Vector2(160f, 44f));
            var label1 = CreateTxt(scrollBtn.transform, "Label1", "0", 16, TextAnchor.MiddleCenter);
            Stretch(label1.rectTransform);
            cards[i] = new CardView
            {
                root = card.gameObject,
                background = card,
                portrait = portrait,
                roleIcon = role,
                nicknameText = nick,
                nameText = name,
                goldButton = goldBtn,
                scrollButton = scrollBtn,
                scrollButtonImage = scrollBtn.GetComponent<Image>(),
                scrollLabel1 = label1,
                goldLabel = goldBtn.GetComponentInChildren<Text>()
            };
        }

        refreshButton = CreateBtn(panel.transform, "RefreshButton", "刷新佣兵", new Vector2(0f, -480f), new Vector2(280f, 64f));
        refreshCostText = CreateTxt(refreshButton.transform, "RefreshCost", "", 14, TextAnchor.MiddleCenter);
        SetRect(refreshCostText.rectTransform, 0.5f, 0.2f, 0f, -8f, 200f, 20f);
        autoRefreshText = CreateTxt(panel.transform, "AutoRefresh", "每日 0 点自动刷新", 14, TextAnchor.MiddleCenter);
        SetRect(autoRefreshText.rectTransform, 0.5f, 0.06f, 0f, 0f, 360f, 24f);
        remainText = CreateTxt(panel.transform, "RemainText", "本局雇佣：0/1", 16, TextAnchor.MiddleCenter);
        SetRect(remainText.rectTransform, 0.5f, 0.12f, 0f, 0f, 300f, 28f);

        AutoBind();
        GameFonts.ApplyToHierarchy(transform);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image CreateImg(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static Button CreateBtn(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var img = CreateImg(parent, name, new Color(0.25f, 0.22f, 0.28f, 1f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}

