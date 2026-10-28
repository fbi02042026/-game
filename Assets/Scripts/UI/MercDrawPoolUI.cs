using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵池（抽奖池）界面：上方 5 格上阵队列 + 下方已获得/未获得大列表。
/// <para>【2026-10-23 主人拍板】本文件只留：字段 / 生命周期 / 节点绑定与事件 / 刷新主流程。
/// 队列交互见 .Swap.cs，展示绘制见 .Art.cs，列表卡见 .Cards.cs，流式布局见 .Flow.cs，槽位见 .Slots.cs。</para>
/// </summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    static MercDrawPoolUI _instance;

    GameObject _root;
    Text _poolCountText;
    readonly List<GameObject> _poolSlots = new List<GameObject>();
    RectTransform _ownedContent;
    readonly List<GameObject> _ownedCards = new List<GameObject>();
    readonly List<GameObject> _lockedCards = new List<GameObject>();
    /// <summary>
    /// 【2026-10-23 主人拍板】列表卡复用池：刷新时不再 Destroy 几十张卡再重建，
    /// 隐藏后留着下次复用。安全前提：FillTemplateCard 每轮全量重填内容，
    /// 且按钮监听先 RemoveAllListeners 再绑（见 Cards.cs），复用不会累积回调。
    /// </summary>
    readonly List<GameObject> _ownedCardPool = new List<GameObject>();
    readonly List<GameObject> _lockedCardPool = new List<GameObject>();

    // 替换弹窗（旧方案：池满弹面板选换下谁；2026-10-17 二次拍板废弃不弹，改为直接点队列格子，
    // 节点与绑定保留不删，仅不再 Show）
    GameObject _replacePanel;
    Text _replaceTitle;
    RectTransform _replaceContent;
    string _replacingInId;
    /// <summary>【2026-10-17 二次拍板】替换点选态：true = 等玩家点上方队列格子换下谁（不弹任何窗体）。</summary>
    bool _swapSelecting;

    // 点选态动态效果：队列已上阵格子绕中心左右旋转（2026-10-18 主人拍板：不再左右位移），退出点选态必须还原角度
    Coroutine _swapSwayCo;
    readonly Dictionary<GameObject, float> _swapSwayBase = new Dictionary<GameObject, float>();    // 基准 = 格子自身旋转 Z
    readonly Dictionary<GameObject, float> _swapSwayPhase = new Dictionary<GameObject, float>();   // 每卡独立相位（2026-10-18）
    const float SwapSwayAmpDeg = 6f;  // 晃动角度幅度（度，绕格子中心左右摆）
    const float SwapSwaySpeed = 6f;   // 晃动角速度（弧度/秒，周期约 1 秒）

    // 列表卡模板（预制体 Panel/Slot；2026-10-18 主人拍板：点卡一次直接弹详情弹窗，
    // 卡上「查看/替换」双钮不再接线、仅保持隐藏；【2026-10-22 主人拍板】卡上新增「升阶」钮
    // 由代码在克隆路径绑定（见 Cards.cs FillTemplateCard））
    GameObject _listTemplate;

    /// <summary>本地编辑态（每次操作后从存档重读，改动即时写盘）。
    /// 【2026-10-20 主人拍板】保序 List：队列按玩家放置顺序展示，不再按稀有度自动排序。</summary>
    readonly List<string> _picked = new List<string>();

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

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Open()
    {
        TownSaveAlign.AlignAll();

        // 【2026-10-17 主人报「所有弹窗都有几率变大」】画布参数只在创建时（BindFromHierarchy）套过一次，
        // 场景切换的 RefreshDdolCanvases 只换 worldCamera 不重套缩放，城镇/战斗相机来回切后
        // 弹窗会带着烙死的旧映射渲染整块变大。按 UICanvasSetup.RefreshPopup 的既有口径
        // （「DDOL 弹窗每次 Show/Open 时调用」）每次开界面重套一遍，与 PlayerJobSelectUI 一同修复。
        var popupCanvas = GetComponent<Canvas>();
        if (popupCanvas != null)
            UICanvasSetup.RefreshPopup(popupCanvas, GameConfig.UiSort.TownPopup);

        // 【2026-10-16 重写时序】必须先激活、再收编、最后填数据：
        // 旧版先 Refresh（触发大滚动收编）后 SetActive(true)，未激活节点不参与布局，
        // 收编量到的世界矩形全错 → 内容被 RectMask2D 整体裁光（弹窗只剩标题/分割线）。
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        if (!_masterReady && _restructurePanel != null)
        {
            SetupMasterList(_restructurePanel);
            // 成功才清；失败（缺节点/未激活）保留，下次 Open 再试
            if (_masterReady) _restructurePanel = null;
        }

        ReloadPicked();
        Refresh();
        // 每次打开回到顶部（收编当帧收编流程里也设了一次；这里兜重开场景）
        if (_masterScroll != null) _masterScroll.verticalNormalizedPosition = 1f;
        GameFonts.ApplyToHierarchy(transform);

        // 【2026-10-24 主人拍板「提取顶条资源条，哪个界面要显示就显示」】招募队列弹窗也显示
        // 金币/钻石两列（全局唯一 ResourceStrip，开窗借来、关窗归还主界面；升阶嵌套见 RefreshForAscendClosed）。
        if (_root != null)
            ResourceStrip.Show(_root.transform, ResourceStrip.Mode.GoldDia);
    }

    void ReloadPicked()
    {
        _picked.Clear();
        var data = SaveSystem.Instance?.Data;
        if (data?.mercDrawPool == null) return;
        foreach (string hireId in data.mercDrawPool)
            if (!string.IsNullOrEmpty(hireId) && !_picked.Contains(hireId)) _picked.Add(hireId);
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
        ExitSwapSelecting();   // 替换点选态跟窗口一起收
        ResourceStrip.Release();   // 【2026-10-24】资源条归还主界面顶条（与 Open 的 Show 成对）
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

        _ownedContent = panel != null ? panel.Find("center/OwnedScroll/Content")?.GetComponent<RectTransform>() : null;

        _replacePanel = panel != null ? panel.Find("ReplacePanel")?.gameObject : null;
        if (_replacePanel != null)
        {
            var box = _replacePanel.transform.Find("Box");
            _replaceTitle = box != null ? box.Find("RTitle")?.GetComponent<Text>() : null;
            _replaceContent = box != null ? box.Find("RViewport/RContent")?.GetComponent<RectTransform>() : null;
        }

        // 列表卡模板（2026-10-17 主人拍板：Panel/Slot 自带「查看/替换」双钮，列表卡全克隆它）
        _listTemplate = panel != null ? panel.Find("Slot")?.gameObject : null;
        if (_listTemplate != null) _listTemplate.SetActive(false);
        else Debug.LogError("[MercDrawPoolUI] 列表卡模板 Panel/Slot 缺失 → 列表无法建卡");

        // 列表结构（2026-10-16 主人拍板）：top 招募池固定不动；OwnedScroll 是唯一大滚动，
        // 已获得段头/已获得卡/未获得段头/未获得卡全在它的 Content 里。搭建必须等 Root 激活后（见 Open）。
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

        // 【2026-10-22 主人拍板】窗口级「Btn进阶」入口作废：升阶按钮已由主人挪进佣兵卡片 Slot 里
        // （查看/替换/升阶三钮），绑定改到每张克隆卡上，见 Cards.cs FillTemplateCard。
        // 【2026-10-23 主人拍板】队列格右上角红「−」下架钮（quxiao）整体作废：队列中的佣兵不许取消，
        // 换人只能点已拥有列表卡详情弹窗的「替换」——防玩家下架让抽奖池变小。OnRemoveClick 一并删除。

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
        }
        Debug.Log("[MercDrawPoolUI] 事件重绑：池格=" + _poolSlots.Count
            + " 关闭钮=" + (closeBtn != null) + " 暗区钮=" + (dimBtn != null)
            + " 替换弹窗=" + (_replacePanel != null));

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

    // 列表结构字段（_restructurePanel/_masterReady/_masterContent/_masterScroll/
    // _flowLockedHeadNodes/_flowLockedCardsY/_hLockedHead 等）统一在 MercDrawPoolUI.Flow.cs

    // ============================================================
    // 刷新
    // ============================================================

    /// <summary>
    /// 升星弹窗（MercAscendUI）关闭后的回调入口：酒馆队列界面正开着才重刷，
    /// 让卡内「升阶」钮的亮/灰（材料+资金够不够）与最新星级同步（2026-10-23 二改口径）；未开界面时零开销。
    /// </summary>
    public static void RefreshForAscendClosed()
    {
        if (_instance != null && _instance.gameObject.activeInHierarchy)
        {
            _instance.Refresh();
            // 【2026-10-24】升阶关窗把资源条 Release 回了主界面顶条；队列弹窗还开着就把条借回来
            //（嵌套链：队列 Show → 升阶 Show 迁走 → 升阶 Release 归主界面 → 这里再 Show 回队列）。
            if (_instance._root != null && _instance._root.activeSelf)
                ResourceStrip.Show(_instance._root.transform, ResourceStrip.Mode.GoldDia);
        }
    }

    void Refresh()
    {
        // 大滚动收编已移到 Open()：先激活 Root，再收编，再首次 Refresh（见 Open 注释）。
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;

        if (_poolCountText != null)
            // 2026-10-15 主人拍板：文案用词与预制体手写演示文本统一（当前可配置 X/5 佣兵）
            _poolCountText.text = "当前可配置" + _picked.Count + "/" + SaveData.DrawPoolMaxSlots + "佣兵";

        // 队列不自动排序：按玩家放置顺序展示（2026-10-20 主人拍板，覆盖早前「按稀有度自动排列」口径）
        var ordered = _picked;

        for (int i = 0; i < _poolSlots.Count; i++)
        {
            bool open = i < slots;
            string hireId = i < ordered.Count ? ordered[i] : null;
            RefreshPoolSlot(_poolSlots[i], i, open, hireId);
        }

        // 替换点选态：队列已上阵格子盖「换下」高亮，点谁换谁（2026-10-17 二次拍板：不弹替换面板）
        if (_swapSelecting) ApplySwapSelectMarks();

        RefreshOwned();
        RefreshLocked();
#if UNITY_EDITOR
        CheckFlowLayout();   // 布局自检（每会话一次）：内容飞出视口当场报错，不许静默空白
#endif
    }

    void RefreshOwned()
    {
        RecycleCards(_ownedCards, _ownedCardPool);
        var data = SaveSystem.Instance?.Data;
        var content = _masterContent != null ? _masterContent : _ownedContent;

        // 已获得段头 = 大滚动第一个元素（跟随滚动，2026-10-16 主人拍板：top 以下整块都是大列表）
        PlaceCluster(_flowOwnedHeadNodes, 0f);
        float ownedEnd = _hOwnedHead;

        // 已获得卡紧跟段头之下
        if (data?.unlockedMercIds != null)
        {
            var ordered = SortByRarity(data.unlockedMercIds);
            if (ordered.Count > 0)
                ownedEnd = BuildTemplateCards(content, ordered, true, _hOwnedHead + HeadToCardsGap);
        }

        // 未获得段头跟着已获得段结束位置（卡数变化每刷重摆）
        PlaceCluster(_flowLockedHeadNodes, ownedEnd + GapSection);
        _flowLockedCardsY = ownedEnd + GapSection + _hLockedHead + HeadToCardsGap;
    }

    void RefreshLocked()
    {
        RecycleCards(_lockedCards, _lockedCardPool);
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
        var content = _masterContent != null ? _masterContent : _ownedContent;
        float end = BuildTemplateCards(content, ordered, false, _flowLockedCardsY);
        LayoutContent(content, end);
    }

    // ============================================================
    // 交互（全部即时生效：改存档 → Save → 重读 → 刷新）
    // ============================================================
}
