using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页「职业装备栏」——绑定主人手摆在 CharacterUI 预制体里的 <c>zhuangbei</c> 节点
/// （3 个职业标签 + 战斗同款 6 槽装备栏：主手/副手/头/胸/手/脚）。
///
/// <para>绑定方式照 <c>BattleUI.WidgetFactory.BindEquipQuickSlots</c>：抓槽位节点、
/// 记下美术放的占位图（空槽还原）、icon 层填装备图标。不碰预制体文件，只挂运行时组件/节点。</para>
///
/// <para>🔴 数据口径（2026-11-13 主人拍板「我要的是站立的一套，直接复制过去」）：
/// · <b>当前职业</b>（存档选中）→ 显示<b>已穿装备</b>（<see cref="BattleBagSystem.GetEquippedInSlot"/>，
///   与战斗装备栏同一数据源，随 OnBackpackChanged/OnCostumeChanged 实时刷新）；
/// · <b>其他职业</b>标签 → 显示该职业<b>起步套</b>预览（<see cref="PlayerJobDefs.GetJobStarterTemplateIds"/>，
///   严格按 job 取，三套互不串——红线）。起步无护甲 → 头/胸/手/脚空槽（还原占位图）。</para>
///
/// <para>交互：点标签切职业；左右箭头（运行时补建，预制体无）也可切换；点已装备槽弹
/// <see cref="EquipDetailPopupUI"/> 属性框。</para>
/// </summary>
public class CharacterEquipBar : MonoBehaviour
{
    static CharacterEquipBar _inst;

    // 槽名 → 槽位类型（与战斗 zhuangbei 同名；节点名在 prefab 里是转义中文，运行时即正常中文）
    struct SlotDef { public string name; public EquipSlotType slot; }
    static readonly SlotDef[] SlotDefs =
    {
        new SlotDef { name = "主手", slot = EquipSlotType.MainHand },
        new SlotDef { name = "副手", slot = EquipSlotType.OffHand },
        new SlotDef { name = "头",   slot = EquipSlotType.Head },
        new SlotDef { name = "胸",   slot = EquipSlotType.Chest },
        new SlotDef { name = "手",   slot = EquipSlotType.Hands },
        new SlotDef { name = "脚",   slot = EquipSlotType.Feet },
    };

    class SlotView
    {
        public GameObject root;
        public Image icon;            // 图标层（美术 icon/ItemIcon/Icon 子节点，回退槽自身）
        public Sprite placeholder;    // 美术占位图（空槽还原，战斗同款）
        public Button button;
        public EquipSlotType slot;
        public EquipInstance equip;   // 当前绑的实例（详情弹窗数据源）
        public Transform frame;       // 稀有度框（幂等建，空槽收起）
    }

    Transform _container;                 // 装备容器（6 槽的父）
    readonly List<SlotView> _slots = new List<SlotView>();
    readonly List<Text> _tabTexts = new List<Text>();
    readonly List<PlayerJobId> _tabJobs = new List<PlayerJobId>();
    Button _leftArrow, _rightArrow;       // 运行时补建
    Button _entryBtn;                     // 右侧装备图标入口（主人手摆的「装备Button」）
    bool _collapsed;
    bool _wired;
    int _idx;

    /// <summary>三职业起步套缓存：Playable[job] → [主手, 副手, null, null, null, null]。严格按 job 隔离。</summary>
    readonly Dictionary<PlayerJobId, EquipInstance[]> _kitCache =
        new Dictionary<PlayerJobId, EquipInstance[]>();

    // ============================================================
    // 绑定入口
    // ============================================================

    /// <summary>绑定 zhuangbei 节点 + 右侧装备图标入口（重复调用同一节点返回既有实例）。</summary>
    public static CharacterEquipBar Bind(Transform root, Transform entryButton = null)
    {
        if (_inst != null && _inst.transform == root) return _inst;
        if (_inst != null) UnityEngine.Object.Destroy(_inst.gameObject);
        var bar = root.GetComponent<CharacterEquipBar>();
        if (bar == null) bar = root.AddComponent<CharacterEquipBar>();
        bar.DoBind(root, entryButton);
        _inst = bar;
        return bar;
    }

    void DoBind(Transform root, Transform entryButton)
    {
        // 装备容器 = 「主手」槽的父节点（不依赖容器叫什么）
        var mainT = UiFind.Deep(root, "主手");
        if (mainT == null)
        {
            Debug.LogError("[CharacterEquipBar] zhuangbei 下找不到「主手」槽，装备栏未绑定（请核对节点名）");
            return;
        }
        _container = mainT.parent;

        // 6 槽
        _slots.Clear();
        for (int i = 0; i < SlotDefs.Length; i++)
        {
            var t = UiFind.Deep(root, SlotDefs[i].name);
            if (t == null)
            {
                Debug.LogError($"[CharacterEquipBar] zhuangbei 下找不到槽「{SlotDefs[i].name}」，该槽未绑定");
                continue;
            }
            _slots.Add(BuildSlotView(t, SlotDefs[i].slot));
        }

        BindTabs(root);

        if (_container != null) EnsureArrows(_container);
        BindEntry(entryButton);
        _idx = IndexOf(PlayerJobDefs.GetSelected());
        RefreshCurrent();
    }

    // ============================================================
    // 右侧装备图标入口（主人手摆的「装备Button」，预制体已带 Button 组件）
    // 点击 → 收起/展开整个装备栏；入口按钮不在 zhuangbei 层级下，收起后仍可点开
    // ============================================================

    void BindEntry(Transform entryButton)
    {
        if (entryButton == null)
        {
            Debug.LogError("[CharacterEquipBar] 找不到「装备Button」入口节点，装备栏开关未绑定");
            return;
        }
        _entryBtn = entryButton.GetComponent<Button>();
        if (_entryBtn == null) _entryBtn = entryButton.gameObject.AddComponent<Button>();
        _entryBtn.onClick.RemoveAllListeners();
        _entryBtn.onClick.AddListener(ToggleBar);
    }

    /// <summary>收起/展开：藏/显整个 zhuangbei（标签+容器+箭头都在其下，入口按钮在外层不受影响）。</summary>
    void ToggleBar()
    {
        _collapsed = !_collapsed;
        gameObject.SetActive(!_collapsed);
    }

    /// <summary>照战斗 ResolveSlotIcon：优先美术摆的 icon/ItemIcon/Icon 子节点，回退槽自身 Image。</summary>
    static SlotView BuildSlotView(Transform t, EquipSlotType slot)
    {
        var view = new SlotView { root = t.gameObject, slot = slot };
        view.icon = ResolveSlotIcon(t);
        // 先记下美术放的占位图，空槽时还原（战斗同款口径）
        view.placeholder = view.icon != null ? view.icon.sprite : null;
        view.button = t.GetComponent<Button>();
        if (view.button == null) view.button = t.gameObject.AddComponent<Button>();
        view.button.transition = Selectable.Transition.None;
        return view;
    }

    static Image ResolveSlotIcon(Transform slotRoot)
    {
        if (slotRoot == null) return null;
        for (int i = 0; i < slotRoot.childCount; i++)
        {
            var c = slotRoot.GetChild(i);
            if (c.name.Equals("icon", System.StringComparison.OrdinalIgnoreCase)
                || c.name.Equals("ItemIcon", System.StringComparison.OrdinalIgnoreCase)
                || c.name.Equals("Icon", System.StringComparison.OrdinalIgnoreCase))
            {
                var img = c.GetComponent<Image>();
                if (img != null)
                {
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    return img;
                }
            }
        }
        return slotRoot.GetComponent<Image>();
    }

    // ============================================================
    // 三个职业标签（按 Text 文本映射职业，不依赖节点名）
    // ============================================================

    void BindTabs(Transform root)
    {
        _tabTexts.Clear();
        _tabJobs.Clear();
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == _container) continue;
            var t = child.GetComponentInChildren<Text>(true);
            if (t == null) continue;
            var job = JobFromLabel(t.text);
            if (job == null)
            {
                Debug.LogError($"[CharacterEquipBar] 标签文本「{t.text}」无法映射职业（需含 剑/法/游），该标签未绑定");
                continue;
            }
            var btn = child.GetComponent<Button>();
            if (btn == null) btn = child.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            // targetGraphic 用 Graphic：标签节点有底图用底图，没有就用文字本身（Text 不是 Image）
            Graphic target = child.GetComponent<Image>();
            if (target == null) target = t;
            btn.targetGraphic = target;
            var captured = job.Value;
            btn.onClick.AddListener(() => SelectJob(captured));
            _tabTexts.Add(t);
            _tabJobs.Add(captured);
        }
        if (_tabTexts.Count < PlayerJobDefs.Playable.Length)
            Debug.LogError($"[CharacterEquipBar] 职业标签只绑到 {_tabTexts.Count}/{PlayerJobDefs.Playable.Length} 个，请核对 zhuangbei 下的标签节点");
    }

    static PlayerJobId? JobFromLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        if (label.Contains("剑")) return PlayerJobId.SwordShield;
        if (label.Contains("法")) return PlayerJobId.Mage;
        if (label.Contains("游")) return PlayerJobId.Ranger;
        return null;
    }

    void SelectJob(PlayerJobId job)
    {
        int idx = IndexOf(job);
        if (idx == _idx) return;
        _idx = idx;
        RefreshCurrent();
    }

    /// <summary>切回「存档当前职业」（ShowPage 时调用）。</summary>
    public void RefreshToSelected()
    {
        _idx = IndexOf(PlayerJobDefs.GetSelected());
        RefreshCurrent();
    }

    int IndexOf(PlayerJobId job)
    {
        for (int i = 0; i < PlayerJobDefs.Playable.Length; i++)
            if (PlayerJobDefs.Playable[i] == job) return i;
        return 0;
    }

    // ============================================================
    // 左右箭头（预制体无 → 运行时补建，挂容器下与槽同坐标系）
    // ============================================================

    void EnsureArrows(Transform container)
    {
        _leftArrow = MakeArrow(container, "JobArrowLeft", "‹", new Vector2(-330f, 11f));
        _rightArrow = MakeArrow(container, "JobArrowRight", "›", new Vector2(330f, 11f));
        _leftArrow.onClick.AddListener(() => CycleJob(-1));
        _rightArrow.onClick.AddListener(() => CycleJob(+1));
    }

    static Button MakeArrow(Transform parent, string name, string glyph, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = glyph;
        t.fontSize = 44;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.85f, 0.40f, 1f);
        t.raycastTarget = true;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(44f, 44f);
        rt.anchoredPosition = pos;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = t;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ol.effectDistance = new Vector2(2f, -2f);
        return btn;
    }

    void CycleJob(int dir)
    {
        int n = PlayerJobDefs.Playable.Length;
        _idx = (_idx + dir + n) % n;
        RefreshCurrent();
    }

    // ============================================================
    // 刷新：当前职业 = 已穿装备；其他职业 = 起步套
    // ============================================================

    void RefreshCurrent()
    {
        var job = PlayerJobDefs.Playable[_idx];
        bool isCurrentJob = job == PlayerJobDefs.GetSelected();
        var kit = isCurrentJob ? null : GetKit(job);

        for (int i = 0; i < _slots.Count; i++)
        {
            EquipInstance eq;
            if (isCurrentJob)
            {
                // 站立的一套：与战斗装备栏同源（GetEquippedInSlot 内部已处理主/副手逻辑槽）
                eq = BattleBagSystem.Instance != null
                    ? BattleBagSystem.Instance.GetEquippedInSlot(_slots[i].slot)
                    : null;
            }
            else
            {
                eq = _slots[i].slot == EquipSlotType.MainHand ? kit[0]
                   : _slots[i].slot == EquipSlotType.OffHand ? kit[1]
                   : null;   // 起步无护甲 → 头/胸/手/脚空槽
            }
            BindSlot(_slots[i], eq);
        }

        // 标签高亮：当前职业白，其他灰（色值主人可在 Inspector 里的 Text 上自行改）
        for (int i = 0; i < _tabTexts.Count; i++)
        {
            if (_tabTexts[i] == null) continue;
            _tabTexts[i].color = _tabJobs[i] == job
                ? Color.white
                : new Color(0.55f, 0.55f, 0.55f, 1f);
        }
    }

    /// <summary>照 EquipQuickSlotUI.Bind 口径：有装上 icon+稀有度框；空槽还原占位图、收框。</summary>
    void BindSlot(SlotView sv, EquipInstance item)
    {
        sv.equip = item;
        sv.button.onClick.RemoveAllListeners();

        bool has = item != null;
        if (has)
        {
            item.template?.ResolveIcon();
            if (item.icon == null && item.template != null)
                item.icon = item.template.icon ?? EquipIcons.Get(item.template.iconFileName);
        }
        bool showIcon = has && item.icon != null;

        if (sv.icon != null)
        {
            sv.icon.gameObject.SetActive(true);
            sv.icon.preserveAspect = true;
            sv.icon.sprite = showIcon ? item.icon : sv.placeholder;
            sv.icon.enabled = sv.icon.sprite != null;
        }
        ApplyRarityFrame(sv.root != null ? sv.root.transform : null, has ? (Rarity?)item.rarity : null);

        if (has)
        {
            var captured = item;
            sv.button.onClick.AddListener(() => EquipDetailPopupUI.Show(captured));
        }
    }

    /// <summary>三职业起步套缓存（主手/副手，副手仅剑盾有盾）。</summary>
    EquipInstance[] GetKit(PlayerJobId job)
    {
        if (_kitCache.TryGetValue(job, out var cached)) return cached;
        var arr = new EquipInstance[SlotDefs.Length];
        PlayerJobDefs.GetJobStarterTemplateIds(job, out string mainId, out string offId);
        arr[0] = MakeStarterInstance(mainId, job);
        arr[1] = MakeStarterInstance(offId, job);
        _kitCache[job] = arr;
        return arr;
    }

    /// <summary>
    /// 由模板 id 生成起步装备实例（白装 0 星，复用全项目唯一口径 GenerateFromTemplate）；
    /// 仅校准主攻击属性键到该职业（生成器用全局当前职业攻击键，切职业预览会错位）。
    /// </summary>
    static EquipInstance MakeStarterInstance(string templateId, PlayerJobId job)
    {
        if (string.IsNullOrEmpty(templateId)) return null;
        try
        {
            var cfg = ConfigManager.Instance;
            var tpl = cfg != null ? cfg.GetEquipTemplate(templateId) : null;
            if (tpl == null)
            {
                Debug.LogError($"[CharacterEquipBar] 找不到起步装备模板 {templateId}");
                return null;
            }
            int lv = Hero.Instance != null ? Hero.Instance.level : 1;
            var inst = EquipInstance.GenerateFromTemplate(tpl, 0, lv, true, Rarity.Common);
            if (inst == null) return null;
            var want = PlayerJobBaseStats.AttackAttrOf(job);
            for (int i = 0; i < inst.attrBonus.Count; i++)
            {
                var b = inst.attrBonus[i];
                if (b == null) continue;
                if (b.attrType == AttrType.Attack && want == AttrType.MagicAttack)
                    b.attrType = AttrType.MagicAttack;
                else if (b.attrType == AttrType.MagicAttack && want == AttrType.Attack)
                    b.attrType = AttrType.Attack;
            }
            return inst;
        }
        catch (System.Exception e)
        {
            // 不兜底：出错要让主人看得见；但别让整条栏崩
            Debug.LogError($"[CharacterEquipBar] 生成起步装备实例失败 {templateId}: {e.Message}");
            return null;
        }
    }

    // ============================================================
    // 已穿装备变化 → 实时刷新当前职业那套
    // ============================================================

    void OnEnable()
    {
        if (BattleBagSystem.Instance != null && !_wired)
        {
            BattleBagSystem.Instance.OnBackpackChanged += RefreshCurrent;
            BattleBagSystem.Instance.OnCostumeChanged += RefreshCurrent;
            _wired = true;
        }
    }

    void OnDisable()
    {
        if (BattleBagSystem.Instance != null && _wired)
        {
            BattleBagSystem.Instance.OnBackpackChanged -= RefreshCurrent;
            BattleBagSystem.Instance.OnCostumeChanged -= RefreshCurrent;
        }
        _wired = false;
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    // ── 稀有度框（照 EquipQuickSlotUI.ApplyRarityFrame：幂等建、垫底、空槽收起）──
    const string RarityFrameNode = "EquipRarityFrame";

    static void ApplyRarityFrame(Transform host, Rarity? rarity)
    {
        if (host == null) return;
        EquipRarityRim.Apply(host, null);   // 旧描边幂等收起，两套稀有度表达不并存

        var frame = FindFrame(host);
        if (rarity == null)
        {
            if (frame != null) frame.gameObject.SetActive(false);
            return;
        }
        if (frame == null)
        {
            frame = BuildFrame(host);
            if (frame == null) return;
        }
        var sp = EquipRarityFrame.SpriteOf(rarity.Value);
        if (sp == null) { frame.gameObject.SetActive(false); return; }   // 缺图出口内已 LogError
        frame.gameObject.SetActive(true);
        var img = frame.GetComponent<Image>();
        img.sprite = sp;
        img.type = Image.Type.Simple;      // 150×150 无九宫格 border，禁 Sliced
        img.preserveAspect = true;
    }

    static Transform FindFrame(Transform host)
    {
        for (int i = 0; i < host.childCount; i++)
        {
            var c = host.GetChild(i);
            if (c != null && c.name == RarityFrameNode) return c;
        }
        return null;
    }

    static Transform BuildFrame(Transform host)
    {
        var go = new GameObject(RarityFrameNode, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        go.transform.SetAsFirstSibling();  // 垫底一次：图标/点击层都在其上
        return go.transform;
    }
}
