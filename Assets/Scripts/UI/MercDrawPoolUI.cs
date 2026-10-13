using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-13 主人拍板】酒馆「佣兵招募池」（按主人设计图 v3）：
/// 上区 = 招募池 6 格（初期 4 格，通关第 2/3 章各开 1 格），在池头像右上角红「−」= 下架（系统自动补位）；
/// 中区 = 已获得佣兵（**统一用头像**，按稀有度分组，在池的头像右上角绿「已上架」标）；
/// 下区 = 未获得佣兵（灰头像 + 「未获得」，点卡看获取途径）。
///
/// <para>交互（主人原话口径）：池子<b>最少要补全、会自动补全</b>——下架后系统立即从未上架的
/// 已解锁佣兵按稀有度补满（<see cref="SaveData.RemoveFromDrawPool"/>）；玩家上架走两条路：
/// 池未满直接点卡上架；<b>池满时点已获得佣兵弹「替换」弹窗</b>点选换下谁（不做拖拽）。
/// <b>不能预上架</b>（主人否了点子）。池子内容按稀有度自动排列。</para>
///
/// <para>所有改动<b>即时生效</b>（下架 / 上架 / 替换当场写存档），无「确定」步骤。</para>
///
/// <para>实现约定：运行时建树、不落预制体；头像 / 稀有度头像框 / 职业徽记全部复用现成素材
/// （<see cref="MercPortraitSprites.GetHead"/> / <see cref="MercHireSession.LoadPortraitFrame"/> /
/// <see cref="MercGrowSprites.LoadJobBadge"/>）；图鉴详情复用 <see cref="CodexInfoPopupUI"/>。</para>
/// </summary>
public class MercDrawPoolUI : MonoBehaviour
{
    static MercDrawPoolUI _instance;

    GameObject _root;
    Text _poolCountText;
    readonly List<GameObject> _poolSlots = new List<GameObject>();
    RectTransform _ownedContent;
    RectTransform _lockedContent;
    readonly List<GameObject> _ownedCards = new List<GameObject>();
    readonly List<GameObject> _lockedCards = new List<GameObject>();

    // 替换弹窗（池满时点已获得佣兵 → 选池中谁换下）
    GameObject _replacePanel;
    Text _replaceTitle;
    RectTransform _replaceContent;
    readonly List<GameObject> _replaceCards = new List<GameObject>();
    string _replacingInId;

    /// <summary>本地编辑态（每次操作后从存档重读，改动即时写盘）。</summary>
    readonly HashSet<string> _picked = new HashSet<string>();

    const float PanelW = 690f;
    const float PanelH = 1210f;
    const int GridCols = 3;
    const int PoolCols = 6;
    const float PoolSlotW = 100f;
    const float PoolSlotH = 150f;
    const float CardW = 200f;
    const float CardH = 150f;
    const float GroupHeadH = 30f;
    const float GridGap = 10f;
    const float GridPad = 10f;

    public static MercDrawPoolUI Instance => _instance;
    public bool IsOpen => _root != null && _root.activeSelf;

    // ============================================================
    // 对外入口（唯一）
    // ============================================================

    public static void Show()
    {
        Ensure().Open();
    }

    static MercDrawPoolUI Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("MercDrawPoolUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MercDrawPoolUI>();
        return _instance;
    }

    void Awake() => _instance = this;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Open()
    {
        TownSaveAlign.AlignAll();
        BuildIfNeeded();
        ReloadPicked();
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void ReloadPicked()
    {
        _picked.Clear();
        var data = SaveSystem.Instance?.Data;
        if (data?.mercDrawPool == null) return;
        foreach (string hireId in data.mercDrawPool)
            if (!string.IsNullOrEmpty(hireId)) _picked.Add(hireId);
    }

    /// <summary>单次改动落盘 + 重读 + 全量刷新（改动即时生效，无「确定」步骤）。</summary>
    void Commit()
    {
        SaveSystem.Instance?.Save();
        ReloadPicked();
        Refresh();
    }

    void Close()
    {
        if (_replacePanel != null) _replacePanel.SetActive(false);
        if (_root != null) _root.SetActive(false);
    }

    // ============================================================
    // 建树
    // ============================================================

    void BuildIfNeeded()
    {
        if (_root != null) return;

        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        // 点暗处关闭
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        panel.GetComponent<Image>().color = new Color(0.13f, 0.10f, 0.08f, 0.98f);

        // 标题
        var title = MakeText(panel.transform, "Title", "酒馆 · 佣兵", 28);
        Anchor(title.rectTransform, new Vector2(0.04f, 0.955f), new Vector2(0.96f, 0.995f));

        // —— 区一：招募池（6 格一行，初期 4 格）——
        var poolHead = MakeText(panel.transform, "PoolHead", "招募池", 22);
        poolHead.alignment = TextAnchor.MiddleLeft;
        poolHead.color = new Color(1f, 0.9f, 0.6f);
        Anchor(poolHead.rectTransform, new Vector2(0.04f, 0.905f), new Vector2(0.5f, 0.948f));
        _poolCountText = MakeText(panel.transform, "PoolCount", "", 17);
        _poolCountText.alignment = TextAnchor.MiddleRight;
        Anchor(_poolCountText.rectTransform, new Vector2(0.5f, 0.905f), new Vector2(0.96f, 0.948f));

        var poolGrid = new GameObject("PoolGrid", typeof(RectTransform));
        poolGrid.transform.SetParent(panel.transform, false);
        var pgrt = poolGrid.GetComponent<RectTransform>();
        pgrt.anchorMin = new Vector2(0.5f, 1f);
        pgrt.anchorMax = new Vector2(0.5f, 1f);
        pgrt.pivot = new Vector2(0.5f, 1f);
        pgrt.anchoredPosition = new Vector2(0f, -122f);
        pgrt.sizeDelta = new Vector2(PoolCols * PoolSlotW + (PoolCols - 1) * GridGap, PoolSlotH);
        for (int i = 0; i < SaveData.DrawPoolMaxSlots; i++)
        {
            int col = i % PoolCols;
            var slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(poolGrid.transform, false);
            var srt = slot.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 0f);
            srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(col * (PoolSlotW + GridGap), 0f);
            srt.sizeDelta = new Vector2(PoolSlotW, PoolSlotH);
            slot.GetComponent<Image>().color = new Color(0.18f, 0.14f, 0.11f, 1f);
            int idx = i;
            slot.GetComponent<Button>().onClick.AddListener(() => OnPoolSlotClick(idx));
            _poolSlots.Add(slot);
        }

        // —— 区二：已获得（滚动，按稀有度分组）——
        var ownedHead = MakeText(panel.transform, "OwnedHead", "已获得佣兵", 22);
        ownedHead.alignment = TextAnchor.MiddleLeft;
        ownedHead.color = new Color(1f, 0.9f, 0.6f);
        Anchor(ownedHead.rectTransform, new Vector2(0.04f, 0.745f), new Vector2(0.96f, 0.79f));
        BuildScroll(panel.transform, "OwnedScroll",
            new Vector2(0.04f, 0.452f), new Vector2(0.96f, 0.742f), out _ownedContent);

        // —— 区三：未获得（滚动，按稀有度分组）——
        var lockedHead = MakeText(panel.transform, "LockedHead", "未获得佣兵", 22);
        lockedHead.alignment = TextAnchor.MiddleLeft;
        lockedHead.color = new Color(0.75f, 0.75f, 0.8f);
        Anchor(lockedHead.rectTransform, new Vector2(0.04f, 0.412f), new Vector2(0.96f, 0.452f));
        BuildScroll(panel.transform, "LockedScroll",
            new Vector2(0.04f, 0.075f), new Vector2(0.96f, 0.412f), out _lockedContent);

        // 底部：关闭（改动即时生效，无「确定」）
        MakeBtn(panel.transform, "Close", "关闭", new Vector2(0.5f, 0.028f), Close);

        // —— 替换弹窗（池满时点已获得佣兵 → 选池中谁换下）——
        BuildReplacePanel(panel.transform);

        _root.SetActive(false);
    }

    void BuildReplacePanel(Transform parent)
    {
        _replacePanel = new GameObject("ReplacePanel", typeof(RectTransform), typeof(Image));
        _replacePanel.transform.SetParent(parent, false);
        Stretch(_replacePanel.GetComponent<RectTransform>());
        _replacePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        // 遮罩不挡关闭主弹窗：点遮罩 = 取消替换
        var dimBtn = _replacePanel.AddComponent<Button>();
        dimBtn.onClick.AddListener(() => _replacePanel.SetActive(false));

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_replacePanel.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(520f, 560f);
        box.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.09f, 0.99f);
        // 挡住穿透：Box 自己不吃点击但要在遮罩 Button 之上拦截射线
        box.AddComponent<Button>().onClick.AddListener(() => { });

        _replaceTitle = MakeText(box.transform, "RTitle", "替换池中的佣兵", 24);
        Anchor(_replaceTitle.rectTransform, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.97f));

        var tip = MakeText(box.transform, "RTip", "点选要换下的佣兵，换上刚点的这位", 16);
        tip.color = new Color(1f, 1f, 1f, 0.65f);
        Anchor(tip.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.9f));

        var viewport = new GameObject("RViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(box.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.04f, 0.14f);
        vrt.anchorMax = new Vector2(0.96f, 0.83f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 0.9f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var cont = new GameObject("RContent", typeof(RectTransform));
        cont.transform.SetParent(viewport.transform, false);
        var crt = cont.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(GridPad, 0f);
        crt.offsetMax = new Vector2(-GridPad, 0f);
        crt.sizeDelta = new Vector2(0f, 300f);
        _replaceContent = crt;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        MakeBtn(box.transform, "RCancel", "取消", new Vector2(0.5f, 0.055f), () => _replacePanel.SetActive(false));

        _replacePanel.SetActive(false);
    }

    /// <summary>滚动区（ScrollRect + Mask）。</summary>
    static void BuildScroll(Transform parent, string name, Vector2 min, Vector2 max, out RectTransform content)
    {
        var viewport = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(parent, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = min;
        vrt.anchorMax = max;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 0.9f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var cont = new GameObject("Content", typeof(RectTransform));
        cont.transform.SetParent(viewport.transform, false);
        var crt = cont.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(GridPad, 0f);
        crt.offsetMax = new Vector2(-GridPad, 0f);
        crt.sizeDelta = new Vector2(0f, 400f);
        content = crt;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    // ============================================================
    // 刷新
    // ============================================================

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;

        if (_poolCountText != null)
            _poolCountText.text = "当前可获得 " + _picked.Count + " / " + SaveData.DrawPoolMaxSlots + " 名佣兵";

        // 池子按稀有度自动排列（主人拍板）：传说 → 稀有 → 普通，同档按花名册表序
        var ordered = SortByRarity(_picked);

        for (int i = 0; i < _poolSlots.Count; i++)
        {
            bool open = i < slots;
            string hireId = i < ordered.Count ? ordered[i] : null;
            RefreshPoolSlot(_poolSlots[i], i, open, hireId);
        }

        RefreshOwned();
        RefreshLocked();
    }

    void RefreshPoolSlot(GameObject slot, int index, bool open, string hireId)
    {
        for (int c = slot.transform.childCount - 1; c >= 0; c--)
            Destroy(slot.transform.GetChild(c).gameObject);
        var bg = slot.GetComponent<Image>();
        var btn = slot.GetComponent<Button>();

        if (!open)
        {
            bg.color = new Color(0.11f, 0.09f, 0.08f, 1f);
            btn.interactable = true;   // 点了有提示，不让点击石沉大海
            var lockT = MakeText(slot.transform, "Locked", "暂未开启", 13);
            lockT.color = new Color(1f, 1f, 1f, 0.35f);
            var lockTip = MakeText(slot.transform, "LockTip", LockHintOf(index), 11);
            lockTip.color = new Color(1f, 1f, 1f, 0.25f);
            var lrt = lockTip.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0.32f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return;
        }

        if (string.IsNullOrEmpty(hireId))
        {
            bg.color = new Color(0.18f, 0.14f, 0.11f, 1f);
            btn.interactable = true;
            var empty = MakeText(slot.transform, "Empty", "空格", 13);
            empty.color = new Color(1f, 1f, 1f, 0.30f);
            return;
        }

        MercRosterDefs.TryGetByHireId(hireId, out var def);
        bg.color = new Color(0.24f, 0.19f, 0.13f, 1f);
        BuildMercFace(slot.transform, hireId, def, def.Name, true);

        // 红「−」下架按钮（右上角，主人设计图）
        var minus = MakeBtn(slot.transform, "Remove", "−", new Vector2(1f, 1f), () => OnRemoveClick(hireId));
        var mrt = RtOf(minus);
        mrt.anchorMin = new Vector2(1f, 1f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(1f, 1f);
        mrt.anchoredPosition = new Vector2(-2f, -2f);
        mrt.sizeDelta = new Vector2(24f, 24f);
        minus.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.22f, 1f);
        var mlabel = minus.transform.Find("Label")?.GetComponent<Text>();
        if (mlabel != null) mlabel.fontSize = 18;
    }

    /// <summary>锁格提示（第 5 格 = 通关第 2 章、第 6 格 = 通关第 3 章，条件在 SaveData.DrawPoolSlots）。</summary>
    static string LockHintOf(int index)
    {
        return index == 4 ? "通关第 2 章" : index == 5 ? "通关第 3 章" : "";
    }

    void RefreshOwned()
    {
        ClearCards(_ownedCards);
        var data = SaveSystem.Instance?.Data;
        if (data?.unlockedMercIds == null) return;

        // 按稀有度分组：传说 → 稀有 → 普通（组头 + 组内按花名册表序）
        float used = BuildGroup(_ownedContent, "传说", MercRosterDefs.MercRarity.Legendary, data.unlockedMercIds, true);
        used += BuildGroup(_ownedContent, "稀有", MercRosterDefs.MercRarity.Rare, data.unlockedMercIds, true);
        used += BuildGroup(_ownedContent, "普通", MercRosterDefs.MercRarity.Common, data.unlockedMercIds, true);
        LayoutContent(_ownedContent, used);
    }

    void RefreshLocked()
    {
        ClearCards(_lockedCards);
        var data = SaveSystem.Instance?.Data;
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        var ids = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (string.IsNullOrEmpty(def.HireId)) continue;
            if (data != null && data.unlockedMercIds != null && data.unlockedMercIds.Contains(def.HireId)) continue;
            // 剧情 NPC 不进图鉴视野（与局内池同口径）
            if (GameConfig.GetMercTier(def.AssetId) == MercTier.Npc) continue;
            ids.Add(def.HireId);
        }

        var lockedSet = new HashSet<string>(ids);
        float used = BuildGroup(_lockedContent, "传说", MercRosterDefs.MercRarity.Legendary, lockedSet, false);
        used += BuildGroup(_lockedContent, "稀有", MercRosterDefs.MercRarity.Rare, lockedSet, false);
        used += BuildGroup(_lockedContent, "普通", MercRosterDefs.MercRarity.Common, lockedSet, false);
        LayoutContent(_lockedContent, used);
    }

    /// <summary>
    /// 一组（组头 + 3 列头像卡）。<paramref name="memberIds"/> = 该组归属判定用的 id 集
    /// （已获得区传 unlockedMercIds，未获得区传「未解锁且非 NPC」集合）。
    /// owned = 已获得（可上架），false = 未获得（灰卡）。返回占用高度。
    /// </summary>
    float BuildGroup(RectTransform content, string headLabel, MercRosterDefs.MercRarity rarity,
        HashSet<string> memberIds, bool owned)
    {
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        var ids = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            if (d.Rarity != rarity || string.IsNullOrEmpty(d.HireId)) continue;
            if (!memberIds.Contains(d.HireId)) continue;
            ids.Add(d.HireId);
        }
        if (ids.Count == 0) return 0f;

        float used = 0f;

        Color headColor = RarityPalette.Get(
            rarity == MercRosterDefs.MercRarity.Legendary ? SkillRarity.Legendary
            : rarity == MercRosterDefs.MercRarity.Rare ? SkillRarity.Rare
            : SkillRarity.Common);

        var head = MakeText(content, "GroupHead_" + headLabel, "◆ " + headLabel, 19);
        head.alignment = TextAnchor.MiddleLeft;
        head.color = headColor;
        var hrt = head.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -used);
        hrt.sizeDelta = new Vector2(0f, GroupHeadH);

        float y = used + GroupHeadH;
        for (int i = 0; i < ids.Count; i++)
        {
            int col = i % GridCols;
            int row = i / GridCols;
            MercRosterDefs.TryGetByHireId(ids[i], out var def);
            GameObject card = owned ? BuildOwnedCard(ids[i], def) : BuildLockedCard(ids[i], def);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(col * (CardW + GridGap), -(y + row * (CardH + GridGap)));
        }
        int rows = (ids.Count + GridCols - 1) / GridCols;
        return y - used + rows * (CardH + GridGap);
    }

    // ============================================================
    // 卡片构建（统一头像，主人拍板）
    // ============================================================

    /// <summary>
    /// 头像脸：稀有度头像框（跟头像同区，不铺全卡防拉变形）+ 头像 + 名字 + 职业徽记（左上）。
    /// usedForPool = 池格（更高瘦）。
    /// </summary>
    void BuildMercFace(Transform parent, string hireId, MercRosterDefs.Def def, string nameLabel, bool usedForPool)
    {
        var frame = NewImage(parent, "Frame", Color.white);
        var frt = frame.rectTransform;
        frt.anchorMin = new Vector2(0.5f, 1f);
        frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        var frameSp = MercHireSession.LoadPortraitFrame(def.Rarity);
        frame.sprite = frameSp;
        frame.enabled = frameSp != null;
        var head = NewImage(parent, "Head", Color.white);
        var hrt = head.rectTransform;
        hrt.anchorMin = new Vector2(0.5f, 1f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        if (usedForPool)
        {
            frt.anchoredPosition = new Vector2(0f, -22f);
            frt.sizeDelta = new Vector2(88f, 88f);
            hrt.anchoredPosition = new Vector2(0f, -26f);
            hrt.sizeDelta = new Vector2(72f, 72f);
        }
        else
        {
            frt.anchoredPosition = new Vector2(0f, -6f);
            frt.sizeDelta = new Vector2(136f, 116f);
            hrt.anchoredPosition = new Vector2(0f, -10f);
            hrt.sizeDelta = new Vector2(120f, 100f);
        }
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        head.sprite = sp;
        head.preserveAspect = true;
        head.enabled = sp != null;

        var nameT = MakeText(parent, "Name", nameLabel, usedForPool ? 13 : 17);
        var nrt = nameT.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.offsetMin = new Vector2(2f, 4f);
        nrt.offsetMax = new Vector2(-2f, usedForPool ? 26f : 30f);

        var badge = NewImage(parent, "Badge", Color.white);
        var brt = badge.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(5f, -5f);
        brt.sizeDelta = new Vector2(30f, 30f);
        var badgeSp = MercGrowSprites.LoadJobBadge(def.JobName, def.Rarity);
        badge.sprite = badgeSp;
        badge.enabled = badgeSp != null;
    }

    GameObject BuildOwnedCard(string hireId, MercRosterDefs.Def def)
    {
        var card = new GameObject("OwnedCard", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(_ownedContent, false);
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.17f, 0.13f, 0.10f, 1f);
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;

        BuildMercFace(card.transform, hireId, def, def.Name, false);

        // 在池标识：头像右上角绿「已上架」（主人拍板）
        bool inPool = _picked.Contains(hireId);
        var mark = MakeText(card.transform, "InPool", "已上架", 13);
        var mrt = mark.rectTransform;
        mrt.anchorMin = new Vector2(1f, 1f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(1f, 1f);
        mrt.anchoredPosition = new Vector2(-4f, -4f);
        mrt.sizeDelta = new Vector2(52f, 22f);
        mark.color = inPool ? RarityPalette.PowerGain : new Color(1f, 1f, 1f, 0.12f);

        // 详情小按钮（右下）
        var info = MakeBtn(card.transform, "Info", "?", new Vector2(1f, 0f), () => ShowMercCodex(hireId, def));
        var irt = RtOf(info);
        irt.anchorMin = new Vector2(1f, 0f);
        irt.anchorMax = new Vector2(1f, 0f);
        irt.pivot = new Vector2(1f, 0f);
        irt.anchoredPosition = new Vector2(-4f, 6f);
        irt.sizeDelta = new Vector2(38f, 32f);

        // 升级入口（本期占位，主人拍板「先不升级 先留出位置和接口」）
        var grow = MakeBtn(card.transform, "Grow", "养成", new Vector2(0f, 0f), () =>
        {
            UIManager.Instance?.ShowToast("佣兵养成即将开放");
        });
        var grt = RtOf(grow);
        grt.anchorMin = new Vector2(0f, 0f);
        grt.anchorMax = new Vector2(0f, 0f);
        grt.pivot = new Vector2(0f, 0f);
        grt.anchoredPosition = new Vector2(4f, 6f);
        grt.sizeDelta = new Vector2(56f, 32f);
        grow.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.18f, 1f);

        string hid = hireId;
        btn.onClick.AddListener(() => OnOwnedCardClick(hid));
        _ownedCards.Add(card);
        return card;
    }

    GameObject BuildLockedCard(string hireId, MercRosterDefs.Def def)
    {
        var card = new GameObject("LockedCard", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(_lockedContent, false);
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.13f, 0.11f, 0.10f, 1f);
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;

        BuildMercFace(card.transform, hireId, def, "未获得", false);

        var head = card.transform.Find("Head")?.GetComponent<Image>();
        if (head != null) head.color = new Color(0.40f, 0.40f, 0.40f, 0.9f);
        var frame = card.transform.Find("Frame")?.GetComponent<Image>();
        if (frame != null) frame.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        string hid = hireId;
        btn.onClick.AddListener(() => ShowMercCodex(hid, def));
        _lockedCards.Add(card);
        return card;
    }

    // ============================================================
    // 交互（全部即时生效：改存档 → Save → 重读 → 刷新）
    // ============================================================

    /// <summary>点池格头像 = 看图鉴详情（下架走右上角红「−」，主人设计图口径）。</summary>
    void OnPoolSlotClick(int index)
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        if (index >= slots)
        {
            UIManager.Instance?.ShowToast(LockHintOf(index) + "后开启");
            return;
        }
        var ordered = SortByRarity(_picked);
        if (index >= ordered.Count) return;
        if (MercRosterDefs.TryGetByHireId(ordered[index], out var def))
            ShowMercCodex(ordered[index], def);
    }

    /// <summary>红「−」= 下架 → 系统立即自动补位（主人拍板「池子最少要补全 会自动补全」）。</summary>
    void OnRemoveClick(string hireId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.RemoveFromDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast("已下架 " + def.Name + "，系统自动补位");
            Commit();
        }
    }

    /// <summary>点已获得卡 = 上架；池满 → 弹替换弹窗（主人拍板「点佣兵弹出替换按钮」，不做拖拽）。</summary>
    void OnOwnedCardClick(string hireId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (_picked.Contains(hireId))
        {
            // 在池的卡点了不做事（下架走池格红「−」），避免误触把人摘下来
            return;
        }
        if (data.AddToDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast(def.Name + " 已上架");
            Commit();
            return;
        }
        if (_picked.Count >= data.DrawPoolSlots)
        {
            // 池满：弹替换弹窗，点选池中谁换下
            _replacingInId = hireId;
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            if (_replaceTitle != null)
                _replaceTitle.text = "替换池中的佣兵（换上 " + def.Name + "）";
            RefreshReplacePanel();
            if (_replacePanel != null)
            {
                _replacePanel.SetActive(true);
                _replacePanel.transform.SetAsLastSibling();
            }
        }
    }

    void RefreshReplacePanel()
    {
        for (int i = 0; i < _replaceCards.Count; i++)
            if (_replaceCards[i] != null) Destroy(_replaceCards[i]);
        _replaceCards.Clear();

        var ordered = SortByRarity(_picked);
        for (int i = 0; i < ordered.Count; i++)
        {
            string outId = ordered[i];
            MercRosterDefs.TryGetByHireId(outId, out var def);
            var card = new GameObject("RCard", typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(_replaceContent, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
            card.GetComponent<Image>().color = new Color(0.20f, 0.16f, 0.12f, 1f);
            var btn = card.GetComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();

            BuildMercFace(card.transform, outId, def, def.Name, false);
            var minus = MakeBtn(card.transform, "Swap", "换下", new Vector2(0.5f, 0f), null);
            var mrt = RtOf(minus);
            mrt.anchorMin = new Vector2(0.5f, 0f);
            mrt.anchorMax = new Vector2(0.5f, 0f);
            mrt.pivot = new Vector2(0.5f, 0f);
            mrt.anchoredPosition = new Vector2(0f, 4f);
            mrt.sizeDelta = new Vector2(84f, 30f);
            minus.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.22f, 0.9f);
            var mlabel = minus.transform.Find("Label")?.GetComponent<Text>();
            if (mlabel != null) mlabel.fontSize = 15;

            string captured = outId;
            UnityEngine.Events.UnityAction swap = () =>
            {
                var d = SaveSystem.Instance?.Data;
                if (d != null && d.SwapInDrawPool(_replacingInId, captured))
                {
                    UIManager.Instance?.ShowToast("替换成功");
                    Commit();
                    if (_replacePanel != null) _replacePanel.SetActive(false);
                }
            };
            btn.onClick.AddListener(swap);
            minus.onClick.AddListener(swap);
            _replaceCards.Add(card);
        }
        LayoutReplace();
    }

    void LayoutReplace()
    {
        if (_replaceContent == null) return;
        int n = _replaceCards.Count;
        int rows = Mathf.Max(1, (n + GridCols - 1) / GridCols);
        float h = GridPad * 2f + rows * CardH + (rows - 1) * GridGap;
        float viewH = _replaceContent.parent != null ? _replaceContent.parent.GetComponent<RectTransform>().rect.height : 0f;
        _replaceContent.sizeDelta = new Vector2(0f, Mathf.Max(h, viewH));
    }

    /// <summary>图鉴详情（未获得的 meta 里带获取途径）。</summary>
    static void ShowMercCodex(string hireId, MercRosterDefs.Def def)
    {
        var stand = MercPortraitSprites.GetHead(hireId);
        if (stand == null && !string.IsNullOrEmpty(def.AssetId)) stand = MercPortraitSprites.GetHead(def.AssetId);
        bool unlocked = SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            && SaveSystem.Instance.Data.unlockedMercIds != null
            && SaveSystem.Instance.Data.unlockedMercIds.Contains(hireId);
        string meta = def.JobName + " · "
            + SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(def.Rarity))
            + (unlocked ? " · 已解锁" : " · 未获得");
        string lore = unlocked ? "" : MercUnlockWayOf(def);
        string desc;
        // 图鉴条目按 catalogId 找：AssetId → catalogId
        string catalogId = !string.IsNullOrEmpty(def.AssetId) ? AdventureCodex.ResolveMercId(def.AssetId) : hireId;
        if (!string.IsNullOrEmpty(catalogId) && AdventureLogCatalog.TryFindMerc(catalogId, out var entry))
            desc = entry.Desc;
        else
            desc = "冒险日志里还没有这位的详细记录。";
        CodexInfoPopupUI.Show(def.Name, meta, desc, lore, stand);
    }

    static string MercUnlockWayOf(MercRosterDefs.Def def)
    {
        var tier = GameConfig.GetMercTier(def.AssetId);
        if (tier == MercTier.Advanced)
            return "获取途径：酒馆解锁，需要公会等级 " + GameConfig.ADVANCED_MERC_GUILD_LEVEL + "。";
        if (def.RecruitGold > 0)
            return "获取途径：酒馆解锁，需要 " + def.RecruitGold + " 金币。";
        return "获取途径：酒馆解锁。";
    }

    // ============================================================
    // 构件
    // ============================================================

    /// <summary>稀有度降序（传说 → 稀有 → 普通），同档按花名册表序。</summary>
    static List<string> SortByRarity(HashSet<string> ids)
    {
        var result = new List<string>();
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        for (int pass = 0; pass < 3; pass++)
        {
            var tier = pass == 0 ? MercRosterDefs.MercRarity.Legendary
                : pass == 1 ? MercRosterDefs.MercRarity.Rare
                : MercRosterDefs.MercRarity.Common;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Rarity == tier && ids.Contains(all[i].HireId))
                    result.Add(all[i].HireId);
        }
        return result;
    }

    void ClearCards(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }

    /// <summary>按占用高度（BuildGroup 返回值累计）算 content 高。</summary>
    static void LayoutContent(RectTransform content, float used)
    {
        if (content == null) return;
        float viewH = content.parent != null ? content.parent.GetComponent<RectTransform>().rect.height : 0f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(used, viewH));
    }

    static Text MakeText(Transform parent, string name, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Button MakeBtn(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 48f);
        go.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.20f, 1f);
        var t = MakeText(go.transform, "Label", label, 20);
        t.raycastTarget = false;
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(onClick);
        return go.GetComponent<Button>();
    }

    /// <summary>Button 没有 rectTransform 属性（Graphic 才有），从 transform 取（2026-10-13 编译教训）。</summary>
    static RectTransform RtOf(Component c)
    {
        return c != null ? c.transform as RectTransform : null;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
