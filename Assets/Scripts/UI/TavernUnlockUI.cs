using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆「佣兵名册」解锁页（2026-09-14 改版）。
///
/// 旧规则：酒馆直接招募佣兵 → 进 hiredMercs → 下本出战。
/// 新规则：**酒馆只做解锁**，花金币把佣兵解锁进「招募池」；
/// 真正的招募挪进了战斗内的「佣兵三选一」（见 DraftPool.CollectRecruitMercs）。
///
/// 解锁门槛见 <see cref="MercUnlockGate"/>：按「已解锁 N 名佣兵 / 通关第 N 章」判定，
/// 并且把条件直接印在卡面上当目标（酒馆本身没有等级功能，别再用等级当门槛）。
/// </summary>
public class TavernUnlockUI : MonoBehaviour
{
    public static TavernUnlockUI Instance { get; private set; }

    GameObject _root;
    Text _countText;
    Text _hintText;
    Transform _content;
    readonly List<Cell> _cells = new List<Cell>();
    readonly List<Button> _filterButtons = new List<Button>();
    readonly List<Text> _filterLabels = new List<Text>();

    class Cell
    {
        public GameObject go;
        public Image bg;
        public Image portrait;
        public Text nameText;
        public Text jobText;
        public Text rarityText;
        public Text statText;
        public Text skillText;
        public Button btn;
        public Text btnLabel;
        public string hireId;
    }

    /// <summary>筛选：0=全部　1=未解锁　2=已解锁。</summary>
    int _filter;

    public static void Show()
    {
        Ensure().Open();
    }

    public static TavernUnlockUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TavernUnlockUI", typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(go);
        var ui = go.AddComponent<TavernUnlockUI>();
        ui.Build();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 构建（纯代码，不依赖预制体）
    // ============================================================

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        // prefab 里残留上一版运行时节点（Root/Frame/Panel…），Unity 保存时把运行时状态序列化进去了。
        // 不清理会与本帧生成的界面叠加，导致面板双份、宽度溢出跑到屏幕外。只删名为 "Root" 的，避免误删美术节点。
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "Root") Destroy(child.gameObject);
        }

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateImg(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        // 读取当前 canvas 逻辑尺寸（不写死常数，兼容更瘦屏逻辑高度 1600）：
        // 当前走 match width，逻辑宽恒为参考分辨率宽，逻辑高 = 屏高 × 参考宽 / 屏宽
        var scaler = canvas.GetComponent<CanvasScaler>();
        float refW = scaler != null ? scaler.referenceResolution.x : 720f;
        float logicalH = (scaler != null && scaler.matchWidthOrHeight < 0.5f)
            ? Screen.height * refW / Mathf.Max(Screen.width, 1)
            : (scaler != null ? scaler.referenceResolution.y : 1280f);
        // 面板收窄到 660（左右各留 30 边距）；高度随屏高动态：9:16 约 1200，9:20 约 1500
        float panelH = Mathf.Min(logicalH - 80f, 1500f);
        var panel = CreateImg(_root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        SetRect(panel.rectTransform, 0.5f, 0.5f, 0f, 0f, 660f, panelH);

        var title = CreateTxt(panel.transform, "Title", "佣兵名册", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.93f, 0f, 0f, 520f, 52f);

        _countText = CreateTxt(panel.transform, "Count", "已解锁 0/0", 24, TextAnchor.MiddleCenter);
        SetRect(_countText.rectTransform, 0.5f, 0.885f, 0f, 0f, 520f, 34f);
        _countText.color = new Color(1f, 0.86f, 0.5f);

        _hintText = CreateTxt(panel.transform, "Hint",
            "解锁后的佣兵会出现在战斗内的「佣兵」三选一中", 20, TextAnchor.MiddleCenter);
        SetRect(_hintText.rectTransform, 0.5f, 0.85f, 0f, 0f, 600f, 30f);
        _hintText.color = new Color(0.72f, 0.78f, 0.9f);

        int shopCount = MercRosterDefs.ShopRoster.Count;
        if (shopCount > 0)
        {
            var shopHint = CreateTxt(panel.transform, "ShopHint",
                $"另有 {shopCount} 名高价佣兵在商店解锁", 18, TextAnchor.MiddleCenter);
            SetRect(shopHint.rectTransform, 0.5f, 0.822f, 0f, 0f, 600f, 26f);
            shopHint.color = new Color(0.85f, 0.74f, 0.45f);
        }

        BuildFilterTabs(panel.transform);

        // 关闭按钮移到面板右上内侧（面板半宽 330，按钮宽 110 → x 约 250 留 25 边距）
        var close = CreateBtn(panel.transform, "CloseButton", "关闭", new Vector2(250f, 545f), new Vector2(110f, 52f));
        close.onClick.AddListener(Hide);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        SetRect(scrollRt, 0.5f, 0.5f, 0f, -110f, 620f, 800f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        Stretch(viewportGo.GetComponent<RectTransform>());
        var vpImg = viewportGo.GetComponent<Image>();
        vpImg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = true;

        _content = new GameObject("Content", typeof(RectTransform)).transform;
        _content.SetParent(viewportGo.transform, false);
        var contentRt = _content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(280f, 360f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewportGo.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportGo.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        BuildCells();
        _root.SetActive(false);
    }

    /// <summary>筛选页签：全部 / 未解锁 / 已解锁。放在提示行下方、滚动区上方。</summary>
    void BuildFilterTabs(Transform panel)
    {
        string[] names = { "全部", "未解锁", "已解锁" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var btn = CreateBtn(panel.transform, "Filter_" + idx, names[i],
                new Vector2(-170f + idx * 170f, 340f), new Vector2(150f, 44f));
            btn.onClick.AddListener(() => OnClickFilter(idx));
            _filterButtons.Add(btn);
            _filterLabels.Add(btn.GetComponentInChildren<Text>());
        }
    }

    void OnClickFilter(int idx)
    {
        if (_filter == idx) return;
        _filter = idx;
        Refresh();
        PaintFilterTabs();
    }

    void PaintFilterTabs()
    {
        for (int i = 0; i < _filterLabels.Count; i++)
        {
            var label = _filterLabels[i];
            if (label == null) continue;
            bool on = i == _filter;
            label.color = on ? new Color(1f, 0.86f, 0.45f) : new Color(0.7f, 0.72f, 0.8f);
            label.fontSize = on ? 22 : 19;
        }
    }

    void BuildCells()
    {
        var roster = MercRosterDefs.TavernRoster;
        for (int i = 0; i < roster.Count; i++)
            _cells.Add(CreateCell(_content, roster[i]));
    }

    Cell CreateCell(Transform parent, MercRosterDefs.Def def)
    {
        var bg = CreateImg(parent, "Merc_" + def.HireId, new Color(0.17f, 0.15f, 0.20f, 1f));
        var c = new Cell { go = bg.gameObject, bg = bg, hireId = def.HireId };

        // 整卡点击区：透明铺满，排在最底层，不挡上面的名字/技能/解锁按钮
        var click = CreateImg(bg.transform, "ClickArea", new Color(1f, 1f, 1f, 0f));
        Stretch(click.rectTransform);
        click.transform.SetAsFirstSibling();
        var cardBtn = click.gameObject.AddComponent<Button>();
        cardBtn.targetGraphic = click;
        cardBtn.transition = Selectable.Transition.None;
        cardBtn.onClick.AddListener(() => OnClickCard(c));

        // 立绘：稀有度头像框 + 头像（复用战斗角色栏同一套资源）
        var frame = CreateImg(bg.transform, "Frame", Color.white);
        SetRect(frame.rectTransform, 0.5f, 0.5f, 0f, 116f, 104f, 104f);
        frame.raycastTarget = false;
        var frameSp = MercHireSession.LoadRarityFrame(def.Rarity);
        frame.sprite = frameSp;
        frame.color = frameSp != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        c.portrait = CreateImg(bg.transform, "Portrait", Color.white);
        SetRect(c.portrait.rectTransform, 0.5f, 0.5f, 0f, 116f, 82f, 82f);
        c.portrait.preserveAspect = true;
        c.portrait.raycastTarget = false;
        var head = LoadMercHead(def.HireId);
        c.portrait.sprite = head;
        c.portrait.color = head != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        c.nameText = CreateTxt(bg.transform, "Name", $"{def.Name}·{def.Nickname}", 24, TextAnchor.MiddleCenter);
        SetRect(c.nameText.rectTransform, 0.5f, 0.5f, 0f, 44f, 250f, 32f);

        // 职业与稀有度同一行左右分列
        c.jobText = CreateTxt(bg.transform, "Job", def.JobName, 19, TextAnchor.MiddleLeft);
        SetRect(c.jobText.rectTransform, 0.28f, 0.5f, 0f, 12f, 130f, 26f);
        c.jobText.color = new Color(0.78f, 0.84f, 0.95f);

        c.rarityText = CreateTxt(bg.transform, "Rarity", RarityName(def.Rarity), 19, TextAnchor.MiddleRight);
        SetRect(c.rarityText.rectTransform, 0.75f, 0.5f, 0f, 12f, 110f, 26f);
        c.rarityText.color = RarityPalette.Get(def.Rarity);

        c.statText = CreateTxt(bg.transform, "Stats",
            $"HP {def.BaseHp}　攻 {def.BaseAtk}　防 {def.BaseDef}", 19, TextAnchor.MiddleCenter);
        SetRect(c.statText.rectTransform, 0.5f, 0.5f, 0f, -24f, 260f, 28f);
        c.statText.color = new Color(0.92f, 0.90f, 0.82f);

        string act = MercRosterDefs.SkillDisplayName(def.ActiveSkillId);
        string pas = MercRosterDefs.SkillDisplayName(def.PassiveSkillId);
        c.skillText = CreateTxt(bg.transform, "Skills", SkillLine(act, pas), 18, TextAnchor.MiddleCenter);
        SetRect(c.skillText.rectTransform, 0.5f, 0.5f, 0f, -70f, 260f, 56f);
        c.skillText.color = new Color(0.80f, 0.84f, 0.92f);

        c.btn = CreateBtn(bg.transform, "UnlockBtn", "解锁", new Vector2(0f, -140f), new Vector2(220f, 48f));
        c.btnLabel = c.btn.GetComponentInChildren<Text>();
        c.btn.onClick.AddListener(() => OnClickUnlock(c));
        return c;
    }

    static string SkillLine(string act, string pas)
    {
        if (string.IsNullOrEmpty(act) && string.IsNullOrEmpty(pas)) return "无技能";
        if (string.IsNullOrEmpty(act)) return "被动：" + pas;
        if (string.IsNullOrEmpty(pas)) return "主动：" + act;
        return "主动：" + act + "\n被动：" + pas;
    }

    /// <summary>头像走冒险日志图鉴同一入口（AdventureCodex），带 AssetId 兜底。</summary>
    static Sprite LoadMercHead(string hireId)
    {
        if (AdventureLogCatalog.TryFindMerc(hireId, out var entry))
        {
            var sp = AdventureCodex.LoadMercHead(entry);
            if (sp != null) return sp;
        }
        return MercPortraitSprites.GetHead(hireId);
    }

    /// <summary>
    /// 二级详情：**复用冒险日志的佣兵图鉴弹窗**（CodexInfoPopupUI），不另造一套。
    /// 字段映射：title=名字·绰号 / meta=职业·稀有度·完整数值 / desc=技能+简介 / lore=Lore。
    /// 不调 MarkMercViewed——在商店里看图不该消耗图鉴红点。
    /// </summary>
    void OnClickCard(Cell c)
    {
        if (!MercRosterDefs.TryGetByHireId(c.hireId, out var def)) return;
        AdventureLogCatalog.TryFindMerc(def.HireId, out var entry);

        string meta = $"{def.JobName} · {RarityName(def.Rarity)}　"
                    + $"HP {def.BaseHp}　攻 {def.BaseAtk}　防 {def.BaseDef}　"
                    + $"攻速 {def.AtkSpeed:0.00}　移速 {def.MoveSpeed:0.00}";

        string desc = BuildSkillBlock(def);
        if (!string.IsNullOrEmpty(entry.Desc))
            desc += (desc.Length > 0 ? "\n\n" : "") + entry.Desc;

        Sprite stand = null;
        if (AdventureLogCatalog.TryFindMerc(def.HireId, out var e2))
            stand = AdventureCodex.LoadMercStand(e2);

        // 2026-09-18 Q2：未解锁且门槛已开 → 详情弹窗里直接给「解锁 N」按钮，不用退回卡面再点一次
        string unlockText = null;
        System.Action onUnlock = null;
        var data = SaveSystem.Instance?.Data;
        if (data != null && !data.IsMercUnlocked(def.HireId) && IsGateOpen(def, data))
        {
            int cost = MercRosterDefs.UnlockCost(def);
            long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            unlockText = gold >= cost ? $"解锁 {cost}" : $"解锁 {cost}（金币不足）";
            onUnlock = () =>
            {
                if (!TryUnlock(def)) return;
                Refresh();
                CodexInfoPopupUI.HideActive();
            };
        }

        CodexInfoPopupUI.Show($"{def.Name}·{def.Nickname}", meta, desc, entry.Lore, stand,
            unlockText, onUnlock);
    }

    /// <summary>技能段：主动 / 被动的名字 + 效果 + 冷却（merc_skills 表）。</summary>
    static string BuildSkillBlock(MercRosterDefs.Def def)
    {
        var sb = new StringBuilder();
        AppendSkillDetail(sb, "主动", def.ActiveSkillId);
        AppendSkillDetail(sb, "被动", def.PassiveSkillId);
        return sb.Length > 0 ? sb.ToString() : "无技能";
    }

    static void AppendSkillDetail(StringBuilder sb, string tag, string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(tag).Append("：").Append(MercRosterDefs.SkillDisplayName(skillId));
        if (MercSkillTable.TryGet(skillId, out var row))
        {
            if (!string.IsNullOrEmpty(row.EffectDesc)) sb.Append("　").Append(row.EffectDesc);
            if (row.Cooldown > 0f) sb.Append("　冷却 ").Append(row.Cooldown.ToString("0.#")).Append("s");
        }
    }

    // ============================================================
    // 打开 / 刷新
    // ============================================================

    void Open()
    {
        if (_root == null) Build();
        Refresh();
        _root.SetActive(true);
        transform.SetAsLastSibling();
        PaintFilterTabs();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.unlockedMercIds == null) data.unlockedMercIds = new HashSet<string>();

        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        int total = MercRosterDefs.All.Count;

        if (_countText != null)
            _countText.text = $"已解锁 {data.unlockedMercIds.Count}/{total}　金币 {gold}";

        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            if (!MercRosterDefs.TryGetByHireId(c.hireId, out var def)) continue;

            bool unlocked = data.IsMercUnlocked(def.HireId);

            // 筛选：0 全部 / 1 未解锁 / 2 已解锁
            bool show = _filter == 0 || ((_filter == 1) != unlocked);
            c.go.SetActive(show);
            if (!show) continue;

            bool gateOpen = IsGateOpen(def, data);
            int cost = MercRosterDefs.UnlockCost(def);

            if (unlocked)
            {
                // 已解锁也能再买：重复 → 转本命碎片（2026-09-18）
                c.bg.color = new Color(0.14f, 0.22f, 0.16f, 1f);
                bool dupAfford = gold >= cost;
                int frag = MercGrowInventory.DupFragCountOf(def);
                SetBtn(c, $"再招募 → 本命碎片 {frag}", dupAfford,
                    dupAfford ? new Color(0.95f, 0.78f, 0.35f, 1f) : new Color(0.55f, 0.5f, 0.4f, 1f));
            }
            else if (!gateOpen)
            {
                // 条件是目标而不是死路：把"要做什么"直接印在按钮上
                c.bg.color = new Color(0.15f, 0.14f, 0.17f, 1f);
                string cond = MercUnlockGate.ConditionText(def.HireId, data) ?? "未开放";
                string prog = MercUnlockGate.ProgressText(def.HireId, data);
                SetBtn(c, string.IsNullOrEmpty(prog) ? cond : $"{cond}（{prog}）",
                    false, new Color(0.62f, 0.60f, 0.68f, 1f));
            }
            else
            {
                c.bg.color = new Color(0.17f, 0.15f, 0.20f, 1f);
                bool afford = gold >= cost;
                SetBtn(c, $"解锁 {cost}", afford,
                    afford ? new Color(0.95f, 0.78f, 0.35f, 1f) : new Color(0.55f, 0.5f, 0.4f, 1f));
            }
        }

        Reorder(data, gold);
    }

    /// <summary>
    /// 排序（P0-2）：买得起 → 买不起 → 条件未达成 → 已解锁置底；同档内按价格升序。
    /// 只改 sibling 顺序，GridLayoutGroup 自动重排；隐藏的卡不参与排序。
    /// </summary>
    void Reorder(SaveData data, long gold)
    {
        var list = new List<Cell>();
        for (int i = 0; i < _cells.Count; i++)
            if (_cells[i].go.activeSelf) list.Add(_cells[i]);

        list.Sort((a, b) =>
        {
            if (!MercRosterDefs.TryGetByHireId(a.hireId, out var da)) return 1;
            if (!MercRosterDefs.TryGetByHireId(b.hireId, out var db)) return -1;
            int ra = RankOf(da, data, gold);
            int rb = RankOf(db, data, gold);
            if (ra != rb) return ra.CompareTo(rb);
            return MercRosterDefs.UnlockCost(da).CompareTo(MercRosterDefs.UnlockCost(db));
        });

        for (int i = 0; i < list.Count; i++)
            list[i].go.transform.SetSiblingIndex(i);
    }

    /// <summary>0=买得起　1=买不起　2=条件未达成　3=已解锁。</summary>
    static int RankOf(MercRosterDefs.Def def, SaveData data, long gold)
    {
        if (data.IsMercUnlocked(def.HireId)) return 3;
        if (!IsGateOpen(def, data)) return 2;
        return gold >= MercRosterDefs.UnlockCost(def) ? 0 : 1;
    }

    void SetBtn(Cell c, string label, bool interactable, Color textColor)
    {
        if (c.btn != null) c.btn.interactable = interactable;
        if (c.btnLabel != null)
        {
            c.btnLabel.text = label;
            c.btnLabel.color = textColor;
        }
    }

    void OnClickUnlock(Cell c)
    {
        if (!MercRosterDefs.TryGetByHireId(c.hireId, out var def)) return;
        if (!TryUnlock(def)) return;

        // 解锁反馈闭环（P0-3）：告诉玩家「三选一池」是什么、什么时候能抽到
        if (_hintText != null)
            _hintText.text = $"「{def.Name}」已加入三选一池，本局冒险即可在战斗中抽到";
        Refresh();
        StartCoroutine(CoFlash(c));
    }

    /// <summary>解锁执行：门槛校验 → 扣金币 → 写存档 → 图鉴 → toast。名册卡与详情弹窗共用。</summary>
    public static bool TryUnlock(MercRosterDefs.Def def)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;

        if (!IsGateOpen(def, data))
        {
            GlobalToastUI.Show("解锁条件：" + MercUnlockGate.FullText(def.HireId));
            return false;
        }

        // 2026-09-18 用户拍板：买到**已解锁**的佣兵 = 重复，直接转本命碎片（不再重复进池）
        if (data.IsMercUnlocked(def.HireId))
        {
            int dupCost = MercRosterDefs.UnlockCost(def);
            if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, dupCost, save: false, notify: false))
            {
                GlobalToastUI.Show("金币不足");
                return false;
            }
            int got = MercGrowInventory.ConvertDuplicateMerc(def);
            SaveSystem.Instance.Save();
            GlobalToastUI.Show($"「{def.Name}」已在名册中 → 转为本命碎片 ×{got}");
            Debug.Log($"[TavernUnlock] 重复佣兵 {def.HireId} {def.Name} → 本命碎片 ×{got}，花费 {dupCost} 金币");
            return true;
        }

        int cost = MercRosterDefs.UnlockCost(def);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: false))
        {
            GlobalToastUI.Show("金币不足");
            return false;
        }

        data.UnlockMerc(def.HireId);
        AdventureCodex.MarkMercSeen(def.HireId);
        SaveSystem.Instance.Save();
        GlobalToastUI.Show($"{def.Name} 已加入三选一池");
        Debug.Log($"[TavernUnlock] 解锁佣兵 {def.HireId} {def.Name}，花费 {cost} 金币");
        return true;
    }

    /// <summary>解锁后卡片闪一下，从高亮淡回「已解锁」底色。</summary>
    System.Collections.IEnumerator CoFlash(Cell c)
    {
        var from = new Color(0.32f, 0.55f, 0.34f, 1f);
        var to = new Color(0.14f, 0.22f, 0.16f, 1f);
        for (int i = 0; i <= 8; i++)
        {
            if (c == null || c.bg == null) yield break;
            c.bg.color = Color.Lerp(from, to, i / 8f);
            yield return new WaitForSecondsRealtime(0.05f);
        }
    }

    // ============================================================
    // 规则
    // ============================================================

    /// <summary>解锁价格：口径搬到 MercRosterDefs（酒馆与商店共用），这里只做转发。</summary>
    public static int UnlockCost(MercRosterDefs.Def def)
    {
        return MercRosterDefs.UnlockCost(def);
    }

    /// <summary>
    /// 门槛判定：**酒馆没有等级这个功能**，所以改走 <see cref="MercUnlockGate"/>。
    /// 条件是明确可达成的（解锁 N 名佣兵 / 通关第 N 章），能直接印在卡面上当目标。
    /// </summary>
    public static bool IsGateOpen(MercRosterDefs.Def def, SaveData data)
    {
        return MercUnlockGate.IsOpen(def.HireId, data);
    }

    static string RarityName(MercRosterDefs.MercRarity r)
    {
        switch (r)
        {
            case MercRosterDefs.MercRarity.Legendary: return "传说";
            case MercRosterDefs.MercRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    // 2026-09-17 删掉本地 RarityColor：它原先自成一套
    // （传说=橙 1/0.72/0.32、稀有=淡蓝 0.58/0.78/1、普通=灰白 0.82/0.86/0.92），
    // 与中央色表不一致 —— 同一个佣兵在酒馆是橙、在图鉴是金。色值统一由 RarityPalette 提供。

    // ============================================================
    // UI 小工具
    // ============================================================

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
        SetRect(img.rectTransform, 0.5f, 0.5f, pos.x, pos.y, size.x, size.y);
        var btn = img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}
