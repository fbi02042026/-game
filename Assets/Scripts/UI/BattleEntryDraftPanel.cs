using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进关抽奖面板（2026-09-30 主人改版，替代原 SlotOfferUI 居中面板）。
///
/// <b>放哪</b>：战斗 HUD 底部的 <c>BackpackPanel</c> 里，不再单独弹居中遮罩面板。
/// <b>怎么出来</b>：BackpackPanel 高度 600（收起）↔ 750（展开），同时把背包内容 + 场景 map + BossBar
/// 作为一个整体向上平移 150，底部空出给抽奖按钮。TopBar、QuestPanel、Background 不动。
///
/// <b>节拍</b>：
/// · 抽奖阶段自动展开到 750，露出 4 个抽奖按钮（普通/佣兵/装备/技能）+「继续」；
/// · 点「继续」→ 收回 600，隐藏抽奖按钮与「继续」，开战；
/// · 战斗中「背包」按钮保留，玩家点它可以再展开看装备（此时没有抽奖按钮）。
///
/// <b>职责边界</b>：本类只管这一块的展开/收起与按钮交互；
/// 抽奖业务与流程编排仍在 <c>BattleManager.CoStageEntryDraft</c>（与旧 SlotOfferUI 的回调契约一致）。
///
/// <b>预制体</b>：主人已摆好节点骨架、icon 图、name/金额 的位置与字体。
/// 代码只做三件事：给节点补 Button 让它能点、改 name/金额 的 text、控 600↔750 的高度。
/// 项目约定「UI 运行时补节点，别改预制体」—— 不往 .prefab 里落任何东西。
/// </summary>
public class BattleEntryDraftPanel : MonoBehaviour
{
    public static BattleEntryDraftPanel Instance { get; private set; }

    // 与预制体 BackpackPanel 一致的两种高度（RectTransform.sizeDelta.y）
    const float HeightCollapsed = 600f;
    const float HeightExpanded = 750f;
    /// <summary>
    /// 内容额外上移的距离。
    /// 子节点 anchor 全在父中心 (0.5,0.5)，面板 600→750 时父中心 300→375，
    /// 它们<b>会自动跟着上移 75</b>；这里再补 75，内容总位移 = 150 = 展开新增的高度，
    /// 底部正好整块让给抽奖按钮。改高度常量时这个值自动跟着变，不用手动调。
    /// </summary>
    const float ContentShiftUp = (HeightExpanded - HeightCollapsed) * 0.5f;
    const float AnimSec = 0.25f;

    /// <summary>
    /// 展开后，抽奖按钮中心距<b>屏幕底部</b>的高度。
    /// 按钮 100 高 → 占 75~175：底边 75 躲开底部安全区，顶边 175 与背包按钮底边(180) 留 5px 不打架，
    /// 而且正好落进"内容上移 150"腾出来的那块空地。
    /// </summary>
    const float DraftShownScreenY = 125f;
    /// <summary>
    /// 抽奖容器展开时的 pos.y。面板展开 750 → 父中心 375，所以 125 - 375 = -250。
    /// 改 HeightExpanded 时这个自动跟着变，不用手动调。
    /// </summary>
    const float DraftShownY = DraftShownScreenY - HeightExpanded * 0.5f;
    /// <summary>
    /// 回弹强度（easeOutBack 的 overshoot 系数）。
    /// 实测冲过头比例：0.8 → 2.3%（位移 150 只弹 3.5px，几乎看不出来）；
    /// <b>1.2 → 5.3%（约 8px）</b>，有手感又不夸张；1.7 是业界标准值 → 10%（约 15px，偏跳）。
    /// </summary>
    const float DraftOvershoot = 1.2f;

    // BackpackPanel 下的节点名（2026-09-30 主人授权：节点名统一英文，方便 grep 与脚本批量处理）
    const string NodePanel = "BackpackPanel";
    const string NodeDraftRoot = "DraftRoot";
    const string NodeContinue = "BtnContinue";
    const string NodeBackpack = "BtnBackpack";
    /// <summary>按钮下的价格 Text 节点。</summary>
    const string NodeCost = "Cost";
    /// <summary>这三个是按钮 / 抽奖容器，位置由美术定死，不参与展开时的内容上移。</summary>
    static readonly HashSet<string> FixedNodes =
        new HashSet<string> { NodeDraftRoot, NodeBackpack, NodeContinue };

    // 按钮底色与文字颜色一律不动：icon 的图、name/金额 的字体和颜色都是主人在预制体里调好的，
    // 代码只负责改 text 和 interactable（置灰由 Button 的 disabledColor 作用在 icon 上）。

    /// <summary>
    /// 一个抽奖按钮：占位节点（主人摆好的 RectTransform）+ 运行时补的 Button。
    /// 名字与金额的两个 Text 是<b>主人在预制体里调好位置和字体的</b>，这里只改它们的 text，
    /// 绝不重建、绝不改字体颜色 —— 否则会把美术调好的东西冲掉。
    /// </summary>
    class DraftSlot
    {
        public RectTransform Root;
        public Button Btn;
        /// <summary>预制体里 btn/name 上的 Text：显示「普通 / 佣兵 / 装备 / 技能」。</summary>
        public Text NameText;
        /// <summary>预制体里 btn/name/金额 上的 Text：显示价格数字。</summary>
        public Text CostText;
        /// <summary>null = 普通（随机抽奖）；有值 = 该类型的定向抽奖。</summary>
        public DraftCategory? Cat;
    }

    /// <summary>需要跟着整体上移的节点：BackpackPanel 下内容 + 场景/形象等外部节点。</summary>
    class ShiftNode
    {
        public RectTransform Rt;
        public float BaseY;
    }

    readonly List<DraftSlot> _slots = new List<DraftSlot>();
    readonly List<ShiftNode> _shiftNodes = new List<ShiftNode>();
    RectTransform _panel;
    Transform _draftRoot;
    /// <summary>抽奖容器的 RectTransform —— 展示/收起时单独挪它，不跟着内容整体上移。</summary>
    RectTransform _draftRt;
    /// <summary>抽奖容器的"藏身位"：直接读预制体里摆的值，收起时回这里。</summary>
    float _draftHiddenY;
    Coroutine _animDraftCo;
    Button _continueBtn;
    Button _backpackBtn;

    Action<DraftCategory?> _onPick;
    Action _onFight;
    Coroutine _animCo;
    bool _expanded;
    bool _built;
    /// <summary>抽奖阶段（此时抽奖按钮可用）；开战后为 false。</summary>
    bool _draftActive;

    /// <summary>「普通」（随机）按钮的 RectTransform —— 引导关要圈住它做高亮。</summary>
    public RectTransform NormalButtonRect =>
        _slots.Count > 0 && _slots[0].Root != null ? _slots[0].Root : null;

    // ============================================================
    // 对外接口（与旧 SlotOfferUI 的签名保持一致，BattleManager 只需换类名）
    // ============================================================

    /// <summary>
    /// 展开面板并显示抽奖按钮。
    /// <paramref name="onPick"/>：null = 点了普通（随机），有值 = 点了该类型定向。
    /// <paramref name="onFight"/>：点了「继续」。
    /// </summary>
    public static void Show(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
                            Action<DraftCategory?> onPick, Action onFight)
    {
        var ui = Ensure();
        if (ui == null)
        {
            // 拿不到面板就直接开战，绝不把玩家卡在抽奖阶段（与旧 SlotOfferUI 一致）
            onFight?.Invoke();
            return;
        }
        ui.Begin(randomPrice, focusPrice, cats, coins, onPick, onFight);
    }

    /// <summary>收起面板并隐藏抽奖按钮（开战/超时都走这里）。</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.Finish();
    }

    static BattleEntryDraftPanel Ensure()
    {
        if (Instance != null) return Instance;
        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] BattleUI 不存在，抽奖面板无法挂载");
            return null;
        }
        var panel = FindDeep(ui.transform, NodePanel);
        if (panel == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到节点 BackpackPanel");
            return null;
        }
        var go = new GameObject("BattleEntryDraftPanel", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var c = go.AddComponent<BattleEntryDraftPanel>();
        c._panel = panel as RectTransform;
        return c;
    }

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 内部
    // ============================================================

    void Begin(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
               Action<DraftCategory?> onPick, Action onFight)
    {
        Build();
        _onPick = onPick;
        _onFight = onFight;
        _draftActive = true;

        SetDraftVisible(true);
        SetExpanded(true);
        Refresh(randomPrice, focusPrice, cats, coins);
    }

    void Finish()
    {
        _onPick = null;
        _onFight = null;
        _draftActive = false;
        SetDraftVisible(false);
        SetExpanded(false);
    }

    /// <summary>
    /// 建按钮索引：4 个占位节点（主人摆好的 RectTransform，图在子节点 icon 上）
    /// 各补一个 Button，并把 name / 金额 两个 Text 的引用抓到手。
    /// 不新建 Image、不新建 Text —— 那些是主人在预制体里调好的美术。
    /// </summary>
    void Build()
    {
        if (_built) return;
        _built = true;

        if (_panel == null) return;
        _draftRoot = FindDeep(_panel, NodeDraftRoot);
        if (_draftRoot == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到抽奖容器 BackpackPanel/" + NodeDraftRoot);
        }
        else
        {
            // 预制体里它<b>藏在面板下方</b>（收起状态绝不能露边）。
            // 展示位不写死在这里 —— 由 DraftShownY 算，所以主人在预制体里怎么挪它都不会打架。
            _draftRt = _draftRoot as RectTransform;
            if (_draftRt != null)
            {
                _draftHiddenY = _draftRt.anchoredPosition.y;
                // 面板末尾挂着 zhezhao（58.8% 黑遮罩）和"战斗中无法调整"提示条，
                // 兄弟顺序靠后 = 画在上面，会把抽奖按钮压得发灰。
                // 运行时把容器抬到最上层，按钮才清亮；预制体的层级不动。
                _draftRt.SetSiblingIndex(_panel.childCount - 1);
            }
            // 顺序与预制体里主人摆的一致：普通 / 佣兵 / 装备 / 技能
            AddSlot("BtnNormal", null);
            AddSlot("BtnMerc", DraftCategory.Merc);
            AddSlot("BtnEquip", DraftCategory.Equip);
            AddSlot("BtnSkill", DraftCategory.Skill);
        }

        var cont = FindDeep(_panel, NodeContinue);
        if (cont == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「继续」按钮节点");
        }
        else
        {
            _continueBtn = EnsureClickable(cont);
            // 2026-10-04 主人拍板：「继续」按钮不要文字，只留美术图标。
            // 不管预制体自带还是别处补的 Text，一律关掉 —— 不留第二个文字入口。
            var contTexts = cont.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < contTexts.Length; i++)
                contTexts[i].gameObject.SetActive(false);
            if (_continueBtn != null) _continueBtn.onClick.AddListener(OnContinueClicked);
        }

        var bp = FindDeep(_panel, NodeBackpack);
        if (bp == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「背包」按钮节点");
        }
        else
        {
            // 背包是纯图标按钮，不加文字
            _backpackBtn = EnsureClickable(bp);
            if (_backpackBtn != null) _backpackBtn.onClick.AddListener(OnBackpackClicked);
        }

        CollectShiftNodes();
    }

    /// <summary>
    /// 收集需要整体上移的节点：BackpackPanel 下除固定按钮/抽奖容器外的所有子节点，
    /// 加上场景 map 和 BossBar（都在 BackpackPanel 的兄弟层）。展开时一起向上动 150，
    /// 给底部抽奖按钮腾出空地；收起时还原。
    /// </summary>
    void CollectShiftNodes()
    {
        if (_panel == null) return;
        for (int i = 0; i < _panel.childCount; i++)
        {
            var child = _panel.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (child == transform) continue;               // 自己
            if (FixedNodes.Contains(child.name)) continue;    // 按钮与抽奖容器不动
            _shiftNodes.Add(new ShiftNode { Rt = child, BaseY = child.anchoredPosition.y });
        }

        // 场景和 Boss 血条也要跟着整体往上走，否则只有背包区动、画面上半部分不动会很怪。
        var battleRoot = _panel.parent as RectTransform;
        if (battleRoot != null)
        {
            var map = battleRoot.Find("map") as RectTransform;
            if (map != null) _shiftNodes.Add(new ShiftNode { Rt = map, BaseY = map.anchoredPosition.y });
            else Debug.LogWarning("[BattleEntryDraftPanel] 找不到场景节点 map");

            var boss = battleRoot.Find("BossBar") as RectTransform;
            if (boss != null) _shiftNodes.Add(new ShiftNode { Rt = boss, BaseY = boss.anchoredPosition.y });
            else Debug.LogWarning("[BattleEntryDraftPanel] 找不到 BossBar");
        }
    }

    /// <summary>
    /// 在一个占位节点上补出可点的抽奖按钮。
    /// 名字 / 金额两个 Text 走预制体里主人调好的，这里只拿到引用，不重建。
    /// </summary>
    void AddSlot(string nodeName, DraftCategory? cat)
    {
        var t = FindDeep(_draftRoot, nodeName);
        if (t == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 抽奖容器下找不到占位节点: " + nodeName);
            return;
        }
        var slot = new DraftSlot { Root = t as RectTransform, Cat = cat };
        int idx = _slots.Count;

        slot.Btn = EnsureClickable(t);

        var nameNode = FindDeep(t, "name");
        if (nameNode != null) slot.NameText = nameNode.GetComponent<Text>();
        // 价格节点是按钮的<b>直接</b>子节点，跟 icon / name 平级 —— 不是在 name 下面。
        // 早期写成 FindDeep(nameNode, ...) 导致永远拿不到引用、价格数字不显示。
        var costNode = FindDeep(t, NodeCost);
        if (costNode != null) slot.CostText = costNode.GetComponent<Text>();

        if (slot.Btn != null)
        {
            int captured = idx;
            slot.Btn.onClick.AddListener(() => OnDraftClicked(captured));
        }
        _slots.Add(slot);
    }

    /// <summary>
    /// 给已有节点补 Button，让它能点。
    /// 优先用节点自己的 Image 做点击反馈；本体没有 Image 时（抽奖按钮就是这种，
    /// 图在子节点 icon 上）用 icon 的 Image —— <b>绝不在本体上硬加 Image</b>，
    /// 否则会多出一块白底盖住美术。
    /// 注意：不创建任何 Text，名字/金额都是主人在预制体里调好的。
    /// </summary>
    static Button EnsureClickable(Transform t)
    {
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        var g = t.GetComponent<Image>();
        if (g == null)
        {
            var icon = FindDeep(t, "icon");
            if (icon != null) g = icon.GetComponent<Image>();
        }
        if (g != null) btn.targetGraphic = g;
        return btn;
    }

    /// <summary>刷新价格与可用状态：池子空或币不够 → 置灰，绝不自动扣钱。</summary>
    void Refresh(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.Btn == null) continue;
            bool isRandom = !s.Cat.HasValue;
            bool has = isRandom || (cats != null && cats.Contains(s.Cat.Value));
            int price = isRandom ? randomPrice : focusPrice;
            bool on = has && coins >= price;
            // 置灰交给 Button.interactable —— 它会按 disabledColor 作用在 icon 上；
            // 不去改 name/金额 的文字颜色，那是主人调好的美术。
            s.Btn.interactable = on;
            if (s.NameText != null) s.NameText.text = Name(s.Cat);
            if (s.CostText != null) s.CostText.text = has ? price.ToString() : "-";
        }
    }

    void OnDraftClicked(int idx)
    {
        if (!_draftActive || idx < 0 || idx >= _slots.Count) return;
        var cb = _onPick;
        var cat = _slots[idx].Cat;
        cb?.Invoke(cat);
    }

    void OnContinueClicked()
    {
        if (!_draftActive) return;
        var cb = _onFight;
        cb?.Invoke();
    }

    /// <summary>「背包」按钮：任何时候都能点，切换展开/收起（抽奖阶段也能收起来看战斗画面）。</summary>
    void OnBackpackClicked()
    {
        SetExpanded(!_expanded);
    }

    /// <summary>抽奖按钮容器 + 「继续」按钮的显隐。开战后彻底隐藏，战斗画面里不留抽奖入口。</summary>
    void SetDraftVisible(bool on)
    {
        if (_draftRoot != null) _draftRoot.gameObject.SetActive(on);
        if (_continueBtn != null) _continueBtn.gameObject.SetActive(on);
    }

    /// <summary>600 ↔ 750。BackpackPanel 锚屏幕底部、轴心在底边，改高度就是向上弹。</summary>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        if (_panel == null) return;
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(CoAnimHeight(expanded ? HeightExpanded : HeightCollapsed));
        // 抽奖容器单独挪：展开时从藏身位挪到展示位，收起时挪回藏身位
        AnimateDraft(expanded ? DraftShownY : _draftHiddenY);
    }

    /// <summary>启动抽奖容器的位移动画（同一时刻只跑一条）。</summary>
    void AnimateDraft(float target)
    {
        if (_draftRt == null) return;
        if (_animDraftCo != null) StopCoroutine(_animDraftCo);
        _animDraftCo = StartCoroutine(CoAnimDraft(target));
    }

    /// <summary>
    /// 整体偏移所有需要上移的节点：展开时 +ContentShiftUp，收起时 0。
    /// 注意 BackpackPanel 自身长高时父中心还会自动上移 75，所以内容总位移 = 75（自动）+ 75（这里）= 150。
    /// </summary>
    void ApplyShift(float dy)
    {
        for (int i = 0; i < _shiftNodes.Count; i++)
        {
            var n = _shiftNodes[i];
            if (n.Rt == null) continue;
            n.Rt.anchoredPosition = new Vector2(n.Rt.anchoredPosition.x, n.BaseY + dy);
        }
    }

    IEnumerator CoAnimHeight(float target)
    {
        float from = _panel.sizeDelta.y;
        if (Mathf.Approximately(from, target))
        {
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, target);
            ApplyShiftForHeight();
            yield break;
        }
        float t = 0f;
        while (t < AnimSec)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / AnimSec);
            float e = 1f - (1f - k) * (1f - k);          // ease-out
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, Mathf.Lerp(from, target, e));
            ApplyShiftForHeight();
            yield return null;
        }
        _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, target);
        ApplyShiftForHeight();
    }

    /// <summary>
    /// 按面板<b>当前实际高度</b>算内容该上移多少（2026-10-04 主人拍板）。
    /// 内容锚在父中心 → 面板长高 (h-600) 时会自动带出 (h-600)*0.5；这里再手动补同样多，
    /// 总位移 = h-600（展开到 750 正好 150 = 底部让出的整块空地）。
    /// ⚠ 绝不再用 "target &gt; from" 反推方向：面板高度若已是 750，反推得到 expand=false
    /// → ApplyShift(0) → 内容完全不上移、抽奖按钮落进内容里被遮住（主人实测的 bug）。
    /// 改成按高度算之后，面板此刻是 600 还是 750，结果都对。
    /// </summary>
    void ApplyShiftForHeight()
    {
        float dy = Mathf.Max(0f, (_panel.sizeDelta.y - HeightCollapsed) * 0.5f);
        ApplyShift(dy);
    }

    /// <summary>
    /// 抽奖容器的位移：藏身位 ↔ 展示位，末段带一点点向上回弹。
    /// 与面板高度动画同时长，所以"面板弹出来"和"按钮冒上来"是同一个动作。
    /// </summary>
    IEnumerator CoAnimDraft(float target)
    {
        float from = _draftRt.anchoredPosition.y;
        if (Mathf.Approximately(from, target))
        {
            _draftRt.anchoredPosition = new Vector2(_draftRt.anchoredPosition.x, target);
            yield break;
        }
        float t = 0f;
        while (t < AnimSec)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / AnimSec);
            _draftRt.anchoredPosition = new Vector2(_draftRt.anchoredPosition.x,
                                                    Mathf.Lerp(from, target, EaseOutBack(k)));
            yield return null;
        }
        _draftRt.anchoredPosition = new Vector2(_draftRt.anchoredPosition.x, target);
    }

    /// <summary>
    /// easeOutBack：末段冲过头一点点再回落，形成"弹一下"的手感。
    /// DraftOvershoot 越小幅度越小；0.8 大约冲过头 6%（位移 150 → 约 8px）。
    /// </summary>
    static float EaseOutBack(float x)
    {
        float c1 = DraftOvershoot, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    static string Name(DraftCategory? c)
    {
        if (!c.HasValue) return "普通";
        switch (c.Value)
        {
            case DraftCategory.Merc: return "佣兵";
            case DraftCategory.Equip: return "装备";
            case DraftCategory.Skill: return "技能";
            default: return "普通";
        }
    }

    // ============================================================
    // 小工具
    // ============================================================

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        var direct = root.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    static Text CreateText(Transform parent, string text, int size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return t;
    }
}
