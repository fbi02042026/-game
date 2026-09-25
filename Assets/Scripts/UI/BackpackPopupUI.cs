using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 独立背包弹窗（角色界面「背包」入口打开）。
/// 复用现有组件：TownBackpackGrid（格子渲染） / BackpackGridVisual（铺图标） / BackpackItemActionUI（点击道具浮层）。
/// 两个分页：
///   【背包】格子网格，行数按 SaveData.backpackRows（经 GameConfig.GetUnlockedBackpackRows）决定；
///   【材料】遍历 7 类散落存档字段，给出道具总览（即使为空也不报错）。
/// 不改任何预制体，不新建美术资源，全程运行时代码建树。
/// </summary>
public class BackpackPopupUI : MonoBehaviour
{
    public static BackpackPopupUI Instance { get; private set; }

    const float PanelW = 560f;
    const float PanelH = 600f;

    GameObject _dismiss;
    GameObject _panel;
    Text _title;
    Button _tabBag;
    Button _tabMat;
    GameObject _bagPage;
    GameObject _matPage;
    Text _capacityText;
    TownBackpackGrid _grid;

    ScrollRect _matScroll;
    Transform _matList;

    int _currentTab;

    /// <summary>重复调用返回同一实例；挂在 parent 下（建议传 CharacterUI.transform）。</summary>
    public static BackpackPopupUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("BackpackPopupUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Instance = go.AddComponent<BackpackPopupUI>();
        Instance.Build();
        return Instance;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Build()
    {
        // 全屏透明遮罩：点它即取消
        _dismiss = new GameObject("Dismiss", typeof(RectTransform), typeof(Image));
        _dismiss.transform.SetParent(transform, false);
        var dImg = _dismiss.GetComponent<Image>();
        dImg.color = new Color(0f, 0f, 0f, 0.55f);
        dImg.raycastTarget = true;
        Stretch(_dismiss.GetComponent<RectTransform>());
        var dBtn = _dismiss.AddComponent<Button>();
        dBtn.transition = Selectable.Transition.None;
        dBtn.onClick.AddListener(Hide);

        // 弹窗底框：复用现有面板图（角色页背包同款），拿不到就退化为纯色面板
        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        var frame = LoadFrameSprite();
        if (frame != null)
        {
            pImg.sprite = frame;
            pImg.type = Image.Type.Sliced;
            pImg.color = Color.white;
        }
        else
        {
            // 退回战斗背包面板同款底框（恢复底框.png），再没有才用纯色面板。
            // 尺寸/配色沿用战斗预制体：Sliced + 白色，不改 Panel 的大小与层级。
            var panelArt = BackpackGridVisual.PanelBgSprite();
            if (panelArt != null)
            {
                pImg.sprite = panelArt;
                pImg.type = Image.Type.Sliced;
                pImg.color = Color.white;
            }
            else
            {
                pImg.color = new Color(0.13f, 0.12f, 0.16f, 0.98f);
            }
        }
        var pRt = _panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(PanelW, PanelH);

        // 标题
        _title = MakeText(_panel.transform, "Title", "背包", 26, TextAnchor.MiddleCenter);
        var tRt = _title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -28f);
        tRt.sizeDelta = new Vector2(0f, 40f);

        // 关闭
        var close = MakeButton(_panel.transform, "Close", "×", new Vector2(PanelW * 0.5f - 28f, -28f), new Vector2(40f, 40f));
        close.onClick.AddListener(Hide);

        // 分页行
        var tabRow = new GameObject("TabRow", typeof(RectTransform));
        tabRow.transform.SetParent(_panel.transform, false);
        var trt = tabRow.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -76f);
        trt.sizeDelta = new Vector2(0f, 44f);

        _tabBag = MakeTabButton(tabRow.transform, "TabBag", "背包", -PanelW * 0.25f + 4f);
        _tabMat = MakeTabButton(tabRow.transform, "TabMat", "材料", PanelW * 0.25f - 4f);
        _tabBag.onClick.AddListener(() => SwitchTo(0));
        _tabMat.onClick.AddListener(() => SwitchTo(1));

        // 内容区
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(_panel.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(12f, 12f);
        crt.offsetMax = new Vector2(-12f, -132f);

        BuildBagPage(content.transform);
        BuildMatPage(content.transform);

        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildBagPage(Transform parent)
    {
        _bagPage = new GameObject("BagPage", typeof(RectTransform), typeof(Image));
        _bagPage.transform.SetParent(parent, false);
        var img = _bagPage.GetComponent<Image>();
        // 复用战斗背包 GridContainer/bg 同款底板（图层 8.png，预制体里是 Simple + 白色）；
        // 拿不到就退回原来的纯色。只换图片，不动尺寸与层级。
        var gridArt = BackpackGridVisual.GridBgSprite();
        if (gridArt != null)
        {
            img.sprite = gridArt;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.1f, 0.09f, 0.13f, 0.6f);
        }
        Stretch(_bagPage.GetComponent<RectTransform>());

        _capacityText = MakeText(_bagPage.transform, "CapacityText", "0 / 0", 18, TextAnchor.MiddleRight);
        var crt2 = _capacityText.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(0f, 1f);
        crt2.anchorMax = new Vector2(1f, 1f);
        crt2.pivot = new Vector2(1f, 1f);
        crt2.anchoredPosition = new Vector2(-8f, -6f);
        crt2.sizeDelta = new Vector2(160f, 32f);

        // 独立网格实例（不复用角色界面内嵌的 backpackGrid，避免改底层）
        var g = new GameObject("BackpackPopupGrid", typeof(RectTransform));
        g.transform.SetParent(_bagPage.transform, false);
        _grid = g.AddComponent<TownBackpackGrid>();
        _grid.BuildGrid(_bagPage.transform);
    }

    void BuildMatPage(Transform parent)
    {
        _matPage = new GameObject("MatPage", typeof(RectTransform), typeof(Image), typeof(Mask));
        _matPage.transform.SetParent(parent, false);
        var img = _matPage.GetComponent<Image>();
        // 同 BagPage：战斗背包 GridContainer/bg 同款底板（图层 8.png）。
        // 这里 Image 同时是 Mask 的遮罩源，图层 8 中心区域不透明，列表不会被裁掉。
        var gridArt = BackpackGridVisual.GridBgSprite();
        if (gridArt != null)
        {
            img.sprite = gridArt;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.1f, 0.09f, 0.13f, 0.6f);
        }
        var mask = _matPage.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        Stretch(_matPage.GetComponent<RectTransform>());

        _matScroll = _matPage.AddComponent<ScrollRect>();
        _matScroll.horizontal = false;
        _matScroll.vertical = true;
        _matScroll.elasticity = 0.08f;

        var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(_matPage.transform, false);
        Stretch(list.GetComponent<RectTransform>());
        _matList = list.transform;

        var vlg = list.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = list.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _matScroll.content = list.GetComponent<RectTransform>();
        _matScroll.viewport = _matPage.GetComponent<RectTransform>();
    }

    void SwitchTo(int tab)
    {
        _currentTab = tab;
        _bagPage.SetActive(tab == 0);
        _matPage.SetActive(tab == 1);
        TintTab(_tabBag, tab == 0);
        TintTab(_tabMat, tab == 1);
        if (tab == 0) RefreshBag();
        else RefreshMaterials();
    }

    void Refresh()
    {
        SwitchTo(_currentTab);
    }

    void RefreshBag()
    {
        if (_grid == null) return;
        _grid.Refresh();

        int used = 0;
        var bag = GridBackpackSystem.Instance;
        if (bag != null)
        {
            var items = bag.GetAllBackpackItems();
            if (items != null) used = items.Count;
        }
        int unlocked = _grid.UnlockedSlotCount();
        if (_capacityText != null)
            _capacityText.text = used + " / " + unlocked;

        WireGridClicks();
    }

    /// <summary>
    /// 网格上的道具点击 → 打开现有 BackpackItemActionUI 浮层。
    /// 注意：网格自带的 BattleBackpackItemDrag 仅在 BattleUI.Instance 存在时弹层（战斗中），
    /// 角色界面不在战斗，所以这里额外挂一个 Button 兜底，复用 BackpackItemActionUI。
    /// </summary>
    void WireGridClicks()
    {
        if (_grid == null || _grid.gridContainer == null) return;
        var layer = _grid.gridContainer.Find("ItemOverlayLayer");
        if (layer == null) return;
        var drags = layer.GetComponentsInChildren<BattleBackpackItemDrag>(true);
        for (int i = 0; i < drags.Length; i++)
        {
            var drag = drags[i];
            if (drag.Item == null) continue;
            // drag.Item 是 ItemInstance；BackpackItemActionUI 需要 GridBackpackSystem.BackpackItem，
            // 用 FindItemByItem 回查（与 BattleBackpackItemDrag.OnPointerClick 同口径）。
            var itemRef = drag.Item;
            var btn = drag.GetComponent<Button>();
            if (btn == null)
            {
                btn = drag.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                var bag = GridBackpackSystem.Instance;
                var entry = bag != null ? bag.FindItemByItem(itemRef) : null;
                if (entry != null) OpenItemAction(entry, drag.gameObject.transform.position);
            });
        }
    }

    void OpenItemAction(GridBackpackSystem.BackpackItem item, Vector3 screenPos)
    {
        if (item == null || item.item == null) return;
        var action = BackpackItemActionUI.Ensure(transform);
        action?.Show(item, (Vector2)screenPos);
    }

    void RefreshMaterials()
    {
        if (_matList == null) return;
        for (int i = _matList.childCount - 1; i >= 0; i--)
            Destroy(_matList.GetChild(i).gameObject);

        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            AddRow("（存档未就绪）", "");
            return;
        }

        // 1) 分解材料
        AddRow("分解材料", data.decomposeMats.ToString());

        // 2) 本命碎片
        // TODO（2026-09-19）：若后续新增专门养成仓库 MercGrowInventory，直接接过来显示；
        // 当前本命碎片存于 mercGrowItems，键形如 "frag:{hireId}"，沿用此口径展示。
        bool fragShown = false;
        if (data.mercGrowItems != null)
        {
            foreach (var kv in data.mercGrowItems)
            {
                if (!kv.Key.StartsWith("frag:")) continue;
                string hireId = kv.Key.Length > 5 ? kv.Key.Substring(5) : kv.Key;
                AddRow("本命碎片·" + hireId, kv.Value.ToString(), dim: kv.Value <= 0, icon: FragmentIcon(hireId));
                fragShown = true;
            }
        }
        if (!fragShown) AddRow("本命碎片", "0", dim: true);

        // 3) 技能碎片
        if (data.skillFragments != null && data.skillFragments.Count > 0)
        {
            foreach (var kv in data.skillFragments)
                AddRow("技能碎片·" + kv.Key, kv.Value.ToString(), dim: kv.Value <= 0);
        }
        else
            AddRow("技能碎片", "0", dim: true);

        // 4) 日志碎片
        if (data.logFragments != null && data.logFragments.Count > 0)
        {
            foreach (var kv in data.logFragments)
                AddRow("日志碎片·" + kv.Key, kv.Value.ToString(), dim: kv.Value <= 0);
        }
        else
            AddRow("日志碎片", "0", dim: true);

        // 5) 佣兵招募卷（普通 / 稀有 / 传说）
        AddRow("佣兵招募卷·普通", data.mercScrollCommon.ToString());
        AddRow("佣兵招募卷·稀有", data.mercScrollRare.ToString());
        AddRow("佣兵招募卷·传说", data.mercScrollLegendary.ToString());

        // 6) 佣兵养成道具（徽记等，排除 frag: 本命碎片）
        bool growShown = false;
        if (data.mercGrowItems != null)
        {
            foreach (var kv in data.mercGrowItems)
            {
                if (kv.Key.StartsWith("frag:")) continue;
                AddRow(BadgeLabel(kv.Key), kv.Value.ToString(), dim: kv.Value <= 0, icon: BadgeIcon(kv.Key));
                growShown = true;
            }
        }
        if (!growShown) AddRow("佣兵养成道具", "0", dim: true);

        // 7) 遗产装备（显示数量即可）
        int legacy = data.legacyEquipPool != null ? data.legacyEquipPool.Count : 0;
        AddRow("遗产装备", legacy.ToString());
    }

    /// <summary>本命碎片图标：按雇佣兵稀有度取碎片底版；查不到退回普通底版。</summary>
    static Sprite FragmentIcon(string hireId)
    {
        var rarity = MercRosterDefs.MercRarity.Common;
        if (MercRosterDefs.TryGetByHireId(hireId, out var def)) rarity = def.Rarity;
        return MercGrowSprites.LoadFragmentBase(rarity);
    }

    /// <summary>徽记 id "badge:{职业}:{档位}" → 徽记图标。</summary>
    static Sprite BadgeIcon(string badgeId)
    {
        var parts = badgeId.Split(':');
        if (parts.Length < 3) return null;
        return MercGrowSprites.LoadJobBadge(parts[1], RarityFromTier(parts[2]));
    }

    /// <summary>徽记 id → 大白话名字，例 badge:剑盾:传奇 → 「职业徽记·剑盾·传奇」。</summary>
    static string BadgeLabel(string badgeId)
    {
        var parts = badgeId.Split(':');
        if (parts.Length < 3) return "职业徽记";
        return "职业徽记·" + parts[1] + "·" + parts[2];
    }

    static MercRosterDefs.MercRarity RarityFromTier(string tier)
    {
        if (tier == "传奇") return MercRosterDefs.MercRarity.Legendary;
        if (tier == "稀有") return MercRosterDefs.MercRarity.Rare;
        return MercRosterDefs.MercRarity.Common;
    }

    void AddRow(string label, string value, bool dim = false, Sprite icon = null)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(_matList, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 40f);
        var img = row.GetComponent<Image>();
        img.color = new Color(0.2f, 0.18f, 0.24f, 0.9f);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.flexibleWidth = 1f;

        // 图标（可选）：有图标时标签整体右移让出位置；没图标时保持原样不动
        if (icon != null)
        {
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ig.transform.SetParent(rt, false);
            var iImg = ig.GetComponent<Image>();
            iImg.sprite = icon;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;
            var irt = ig.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(8f, 0f);
            irt.sizeDelta = new Vector2(30f, 30f);
        }

        var lt = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        lt.transform.SetParent(rt, false);
        var ltxt = lt.GetComponent<Text>();
        ltxt.text = label;
        ltxt.font = GameFonts.GetChinese();
        ltxt.fontSize = 18;
        ltxt.alignment = TextAnchor.MiddleLeft;
        ltxt.color = dim ? new Color(0.6f, 0.6f, 0.65f, 1f) : Color.white;
        ltxt.raycastTarget = false;
        var lrt = lt.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(icon != null ? 44f : 10f, 2f);
        lrt.offsetMax = new Vector2(-90f, -2f);

        var vt = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        vt.transform.SetParent(rt, false);
        var vtxt = vt.GetComponent<Text>();
        vtxt.text = value;
        vtxt.font = GameFonts.GetChinese();
        vtxt.fontSize = 18;
        vtxt.alignment = TextAnchor.MiddleRight;
        vtxt.color = dim ? new Color(0.6f, 0.6f, 0.65f, 1f) : new Color(1f, 0.92f, 0.6f, 1f);
        vtxt.raycastTarget = false;
        var vrt = vt.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0f, 0f);
        vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = new Vector2(-80f, 2f);
        vrt.offsetMax = new Vector2(-10f, -2f);
    }

    static void TintTab(Button tab, bool on)
    {
        if (tab == null) return;
        var img = tab.GetComponent<Image>();
        if (img != null)
            img.color = on ? new Color(0.45f, 0.4f, 0.25f, 1f) : new Color(0.28f, 0.26f, 0.32f, 1f);
        var t = tab.GetComponentInChildren<Text>(true);
        if (t != null)
            t.color = on ? Color.white : new Color(0.8f, 0.8f, 0.85f, 1f);
    }

    static Button MakeTabButton(Transform parent, string name, string label, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.26f, 0.32f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(PanelW * 0.5f - 10f, 40f);

        var txt = MakeText(go.transform, "Text", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.GetComponent<RectTransform>());
        txt.text = label;

        return go.AddComponent<Button>();
    }

    static Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.28f, 0.34f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size ?? new Vector2(100f, 42f);

        var txt = MakeText(go.transform, "Text", label, 22, TextAnchor.MiddleCenter);
        Stretch(txt.GetComponent<RectTransform>());
        txt.text = label;

        return go.AddComponent<Button>();
    }

    static Sprite LoadFrameSprite()
    {
        // 复用角色页背包面板同款底框（运行时代码加载，不改美术资源）
        var sp = Resources.Load<Sprite>("UI/NavCharacter/角色_0002s_0009_图层-6-拷贝");
        if (sp != null) return sp;
        return null;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
