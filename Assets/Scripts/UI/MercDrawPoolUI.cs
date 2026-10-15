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
public partial class MercDrawPoolUI : MonoBehaviour
{
    static MercDrawPoolUI _instance;

    GameObject _root;
    Text _poolCountText;
    readonly List<GameObject> _poolSlots = new List<GameObject>();
    /// <summary>每格当前佣兵 HireId（Refresh 时记录；quxiao 下架钮按格取用）。</summary>
    string[] _slotHireIds;
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

    const int GridCols = 5;
    const float CardW = 200f;
    const float CardH = 150f;
    const float GridGap = 10f;
    const float GridPad = 10f;

    public static MercDrawPoolUI Instance => _instance;
    public bool IsOpen => _root != null && _root.activeSelf;

    /// <summary>预制体路径（Resources 相对）。2026-10-15 主人拍板：抽奖池改预制体实例化；缺失时回落运行时建树并报错（不静默）。</summary>
    const string PrefabPath = "Prefabs/Town/MercDrawPoolUI";

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

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var inst = Object.Instantiate(prefab);
            inst.name = "MercDrawPoolUI";
            DontDestroyOnLoad(inst);
            var ui = inst.GetComponent<MercDrawPoolUI>();
            if (ui != null)
            {
                ui.BindFromHierarchy();
                _instance = ui;
                return _instance;
            }
            Debug.LogError("[MercDrawPoolUI] 预制体缺 MercDrawPoolUI 组件，回落运行时建树");
            Object.Destroy(inst);
        }
        else
        {
            Debug.LogError("[MercDrawPoolUI] 预制体缺失（Resources/" + PrefabPath + "），回落运行时建树");
        }

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
    // 预制体实例化：层级回绑 + 事件重绑
    // ============================================================

    /// <summary>
    /// 预制体实例化后调用：从层级按名回填字段 + 重绑画布（摄像机模式）+ 重绑按钮事件。
    /// 2026-10-15 主人拍板改预制体路线；私有 List 字段不进 prefab，全靠按名回找。
    /// </summary>
    void BindFromHierarchy()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = transform.Find("Root")?.gameObject;
        var panel = _root != null ? _root.transform.Find("Panel") : null;

        _poolCountText = panel != null ? panel.Find("top/PoolCount")?.GetComponent<Text>() : null;

        var poolGrid = panel != null ? panel.Find("top/PoolGrid") : null;
        if (poolGrid != null)
        {
            for (int i = 0; i < poolGrid.childCount; i++)
                _poolSlots.Add(poolGrid.GetChild(i).gameObject);
        }
        _slotHireIds = new string[_poolSlots.Count];

        _ownedContent = panel != null ? panel.Find("center/OwnedScroll/Content")?.GetComponent<RectTransform>() : null;
        _lockedContent = panel != null ? panel.Find("center/down/LockedScroll/Content")?.GetComponent<RectTransform>() : null;

        _replacePanel = panel != null ? panel.Find("ReplacePanel")?.gameObject : null;
        if (_replacePanel != null)
        {
            var box = _replacePanel.transform.Find("Box");
            _replaceTitle = box != null ? box.Find("RTitle")?.GetComponent<Text>() : null;
            _replaceContent = box != null ? box.Find("RViewport/RContent")?.GetComponent<RectTransform>() : null;
        }

        // 一条大滚动（2026-10-15 主人拍板）：top 池区的字和装饰、已拥有、未获得 全在同一滚动里。
        // 【教训】不能在实例化当帧收编 —— Canvas 布局还没跑，世界坐标量出来全是错的，
        // 整条列表会被摆飞裁没（主人实测「什么也没有了」）。推迟到首次 Refresh 再收编。
        _restructurePanel = panel;

        WireClicks();
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>按钮事件重绑（预制体实例化路；prefab 序列化不存匿名监听，必须代码补绑）。</summary>
    void WireClicks()
    {
        if (_root == null) return;
        var panel = _root.transform.Find("Panel");
        if (panel == null) return;

        var dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        if (dimBtn != null)
        {
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(Close);
        }

        var closeBtn = panel.Find("BtnCloseX")?.GetComponent<Button>();
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(Close);
        }

        for (int i = 0; i < _poolSlots.Count; i++)
        {
            // 预制体 SlotN 只有 Image（无 Button），运行时补：点格 = 看图鉴 / 锁格提示
            var slotImg = _poolSlots[i].GetComponent<Image>();
            if (slotImg != null) slotImg.raycastTarget = true;
            var btn = _poolSlots[i].GetComponent<Button>();
            if (btn == null) btn = _poolSlots[i].AddComponent<Button>();
            if (btn.targetGraphic == null && slotImg != null) btn.targetGraphic = slotImg;
            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPoolSlotClick(idx));

            // quxiao = 下架钮（预制体也只有 Image，运行时补 Button；hireId 每次刷新记在 _slotHireIds）
            var qx = _poolSlots[i].transform.Find("quxiao");
            if (qx != null)
            {
                var qImg = qx.GetComponent<Image>();
                if (qImg != null) qImg.raycastTarget = true;
                var qBtn = qx.GetComponent<Button>();
                if (qBtn == null) qBtn = qx.gameObject.AddComponent<Button>();
                if (qBtn.targetGraphic == null && qImg != null) qBtn.targetGraphic = qImg;
                qBtn.onClick.RemoveAllListeners();
                qBtn.onClick.AddListener(() =>
                {
                    string hid = idx < _slotHireIds.Length ? _slotHireIds[idx] : null;
                    if (!string.IsNullOrEmpty(hid)) OnRemoveClick(hid);
                });
            }
        }

        if (_replacePanel != null)
        {
            var rpDim = _replacePanel.GetComponent<Button>();
            if (rpDim != null)
            {
                rpDim.onClick.RemoveAllListeners();
                rpDim.onClick.AddListener(() => _replacePanel.SetActive(false));
            }
            var rpBox = _replacePanel.transform.Find("Box")?.GetComponent<Button>();
            if (rpBox != null)
            {
                rpBox.onClick.RemoveAllListeners();
                rpBox.onClick.AddListener(() => { });
            }
            var rpCancel = _replacePanel.transform.Find("Box/RCancel")?.GetComponent<Button>();
            if (rpCancel != null)
            {
                rpCancel.onClick.RemoveAllListeners();
                rpCancel.onClick.AddListener(() => _replacePanel.SetActive(false));
            }
        }
    }

    readonly List<FlowNode> _flowTopNodes = new List<FlowNode>();
    readonly List<FlowNode> _flowOwnedHeadNodes = new List<FlowNode>();
    readonly List<FlowNode> _flowLockedHeadNodes = new List<FlowNode>();
    RectTransform _masterContent;
    Transform _restructurePanel;     // 待收编的 Panel（推迟到首次 Refresh，等布局跑过）
    bool _masterReady;
    float _flowOwnedCardsY;
    float _flowLockedCardsY;
    float _hTop;
    float _hOwnedHead;
    float _hLockedHead;
    const float GapSection = 24f;        // 大滚动里段与段的间隔
    const float HeadToCardsGap = 8f;     // 段头与其下卡片区的间隔
    const float ScrollSidePad = 16f;     // 滚动视口左右内缩（要调贴边改这里）
    const float ScrollBottomPad = 16f;   // 滚动视口底部内缩

    // ============================================================
    // 刷新
    // ============================================================

    void Refresh()
    {
        // 首次刷新才收编大滚动（此时至少跑过一次 Canvas 布局，世界坐标才可信）
        if (!_masterReady && _restructurePanel != null)
        {
            RestructureMasterScroll(_restructurePanel);
            _restructurePanel = null;
        }

        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;

        if (_poolCountText != null)
            // 2026-10-15 主人拍板：文案用词与预制体手写演示文本统一（当前可配置 X/5 佣兵）
            _poolCountText.text = "当前可配置" + _picked.Count + "/" + SaveData.DrawPoolMaxSlots + "佣兵";

        // 池子按稀有度自动排列（主人拍板）：传说 → 稀有 → 普通，同档按花名册表序
        var ordered = SortByRarity(_picked);

        if (_slotHireIds == null || _slotHireIds.Length != _poolSlots.Count)
            _slotHireIds = new string[_poolSlots.Count];
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            bool open = i < slots;
            string hireId = i < ordered.Count ? ordered[i] : null;
            _slotHireIds[i] = hireId;
            RefreshPoolSlot(_poolSlots[i], i, open, hireId);
        }

        RefreshOwned();
        RefreshLocked();
#if UNITY_EDITOR
        CheckFlowLayout();   // 布局自检（每会话一次）：内容飞出视口当场报错，不许静默空白
#endif
    }

    void RefreshOwned()
    {
        ClearCards(_ownedCards);
        var data = SaveSystem.Instance?.Data;
        if (data?.unlockedMercIds == null) return;

        // 按稀有度排序（传说 → 稀有 → 普通），不再插组头（2026-10-15 主人拍板）
        var ordered = SortByRarity(data.unlockedMercIds);
        float end = BuildTemplateCards(_masterContent != null ? _masterContent : _ownedContent,
            ordered, true, _flowOwnedCardsY);
        // 一条大滚动：未获得段头跟着已拥有卡的结束位置往下挪（卡数变化每刷重摆）
        if (_masterReady)
        {
            PlaceFlowCluster(_flowLockedHeadNodes, end + GapSection);
            _flowLockedCardsY = end + GapSection + _hLockedHead + HeadToCardsGap;
        }
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

        var ordered = SortByRarity(new HashSet<string>(ids));
        float end = BuildTemplateCards(_masterContent != null ? _masterContent : _lockedContent,
            ordered, false, _flowLockedCardsY);
        LayoutContent(_masterContent != null ? _masterContent : _lockedContent, end);
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

    /// <summary>复用美术资源目录（Resources 相对路径前缀）。2026-10-15 主人拍板：优先用项目能复用的资源。</summary>
    const string ArtCommon = "Art/UI/Common/";

    static readonly Dictionary<string, Sprite> _artCache = new Dictionary<string, Sprite>();

    /// <summary>按 Resources 相对路径取 sprite（带缓存；取不到返回 null，调用方回落纯色，不静默）。</summary>
    static Sprite Art(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;
        if (_artCache.TryGetValue(resPath, out var c) && c != null) return c;
        var sp = Resources.Load<Sprite>(resPath);
        _artCache[resPath] = sp;
        return sp;
    }

    /// <summary>给 MakeBtn 生成的按钮换复用素材钮底（Simple + 白底；取不到保持原木色）。</summary>
    static void SetBtnSprite(Button btn, string resPath)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img == null) return;
        var sp = Art(resPath);
        if (sp == null) return;
        img.sprite = sp;
        img.type = Image.Type.Simple;
        img.color = Color.white;
    }

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
