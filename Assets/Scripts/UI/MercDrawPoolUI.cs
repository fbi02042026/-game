using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-13 主人拍板】酒馆「佣兵雇佣队列」（原「佣兵抽奖池/招募池」，2026-10-17 主人拍板改名；
/// 按主人设计图 v3）：
/// 上区 = 雇佣队列 6 格（初期 4 格，通关第 2/3 章各开 1 格），在队头像右上角红「−」= 出队（系统自动补位）；
/// 中区 = 已获得佣兵（**统一用头像**，按稀有度分组，在队的头像右上角绿「已入队」标）；
/// 下区 = 未获得佣兵（灰头像 + 真名，2026-10-18 主人拍板：不再显示「未获得」三个字），点卡直接弹详情。
///
/// <para>交互（2026-10-18 主人拍板）：队列<b>最少要补全、会自动补全</b>——出队后系统立即从未入队的
/// 已解锁佣兵按稀有度补满（<see cref="SaveData.RemoveFromDrawPool"/>）；玩家入队走一条路：
/// 点列表卡（已获得/未获得都是）一次直接弹「获得佣兵」详情弹窗，未入队的已获得佣兵
/// 弹窗右下带「替换」钮 —— 点「替换」池未满直接入队、池满不弹窗：直接点上方队列里要换下的
/// 佣兵完成替换（2026-10-17 二次拍板：废除替换弹窗；不做拖拽）。
/// 已上阵（在队列）的已获得卡灰显 + 绿「已入队」标，弹窗不带替换钮。
/// <b>不能预入队</b>（主人否了点子）。队列内容按稀有度自动排列。</para>
///
/// <para>所有改动<b>即时生效</b>（出队 / 入队 / 替换当场写存档），无「确定」步骤。</para>
///
/// <para>实现约定：运行时建树、不落预制体；全身立绘 / 稀有度头像框 / 职业徽记全部复用现成素材
/// （<see cref="MercPortraitSprites.GetStand"/> / <see cref="MercHireSession.LoadPortraitFrame"/> /
/// <see cref="MercGrowSprites.LoadJobBadge"/>）；佣兵详情复用战斗的 <see cref="MercObtainPopupUI"/>（信息最全）。</para>
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
    readonly List<GameObject> _ownedCards = new List<GameObject>();
    readonly List<GameObject> _lockedCards = new List<GameObject>();

    // 替换弹窗（旧方案：池满弹面板选换下谁；2026-10-17 二次拍板废弃不弹，改为直接点队列格子，
    // 节点与绑定保留不删，仅不再 Show）
    GameObject _replacePanel;
    Text _replaceTitle;
    RectTransform _replaceContent;
    readonly List<GameObject> _replaceCards = new List<GameObject>();
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

    void Awake() => _instance = this;

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

        int qxCount = 0;
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
                qxCount++;
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
        Debug.Log("[MercDrawPoolUI] 事件重绑：池格=" + _poolSlots.Count + " quxiao=" + qxCount
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

        if (_slotHireIds == null || _slotHireIds.Length != _poolSlots.Count)
            _slotHireIds = new string[_poolSlots.Count];
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            bool open = i < slots;
            string hireId = i < ordered.Count ? ordered[i] : null;
            _slotHireIds[i] = hireId;
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
        ClearCards(_ownedCards);
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
        var content = _masterContent != null ? _masterContent : _ownedContent;
        float end = BuildTemplateCards(content, ordered, false, _flowLockedCardsY);
        LayoutContent(content, end);
    }

    // ============================================================
    // 交互（全部即时生效：改存档 → Save → 重读 → 刷新）
    // ============================================================

    /// <summary>点池格头像 = 看图鉴详情（下架走右上角红「−」，主人设计图口径）；
    /// 替换点选态下点格 = 换下这个佣兵（2026-10-17 二次拍板）。</summary>
    void OnPoolSlotClick(int index)
    {
        // 【2026-10-17 二次拍板】替换点选态：点队列格子 = 换下这个，不弹任何窗体
        if (_swapSelecting)
        {
            HandleSwapSelectClick(index);
            return;
        }
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        Debug.Log("[MercDrawPoolUI] 点击池格 " + index + "（开放格数=" + slots + "）");
        if (index >= slots)
        {
            // 【2026-10-20 主人拍板】点锁格必须弹提示 —— force 弹，防被酒馆气泡/对话压制吞掉
            UIManager.Instance?.ShowToast(LockHintOf(index) + "后开启", true);
            return;
        }
        var ordered = _picked;
        if (index >= ordered.Count) return;
        if (MercRosterDefs.TryGetByHireId(ordered[index], out var def))
            ShowMercDetail(ordered[index], def);
    }

    /// <summary>红「−」= 下架 → 系统立即自动补位（主人拍板「池子最少要补全 会自动补全」）。</summary>
    void OnRemoveClick(string hireId)
    {
        Debug.Log("[MercDrawPoolUI] 点击下架 " + hireId);
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.RemoveFromDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast("已出队 " + def.Name + "，系统自动补位");
            Commit();
        }
        else
        {
            Debug.LogError("[MercDrawPoolUI] 下架失败：" + hireId + "（存档池里没有这个 id，池=" + string.Join(",", new List<string>(_picked)) + "）");
        }
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】点已获得卡 = 直接弹「获得佣兵」详情弹窗：
    /// 未入队的佣兵 = 「替换雇佣队列」场景，弹窗右下带「替换」钮（回调 <see cref="OnCardSwap"/>：
    /// 池有空位直接入队，池满进入点选态点队列格子换下谁）；已上阵（在队列）的只看详情，只带确认钮。
    /// </summary>
    void ShowOwnedDetail(string hireId)
    {
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) return;
        bool inQueue = _picked.Contains(hireId);
        ShowMercDetail(hireId, def, !inQueue, inQueue ? null : (System.Action)(() => OnCardSwap(hireId)));
    }

    /// <summary>
    /// 卡上「替换」钮：池有空位直接入队；池满【2026-10-17 二次拍板】不弹替换面板——进入点选态，
    /// 上方队列已上阵格子亮「换下」，直接点要换下的那个（同一张卡再点「替换」= 取消）。
    /// </summary>
    void OnCardSwap(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[MercDrawPoolUI] 点替换时存档为空，未处理");
            return;
        }

        // 点选态中再点同一张「替换」= 取消本次替换
        if (_swapSelecting && _replacingInId == hireId)
        {
            ExitSwapSelecting();
            UIManager.Instance?.ShowToast("已取消替换");
            return;
        }

        if (data.AddToDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast(def.Name + " 已入队");
            Commit();
            return;
        }

        if (_picked.Count < data.DrawPoolSlots)
        {
            Debug.LogError("[MercDrawPoolUI] 入队未成且池未满：hireId=" + hireId
                + "（查 AddToDrawPool 返回 false 的原因）");
            return;
        }

        // 池满：进入点选态（不弹窗），Refresh 重刷队列格并打「换下」高亮，格子左右晃
        _replacingInId = hireId;
        _swapSelecting = true;
        UIManager.Instance?.ShowToast("请点上方队列中要换下的佣兵");
        Refresh();
        StartSwapSway();
    }

    /// <summary>点选态下点队列格：点谁换下谁，换完即退点选态（改动即时落盘）。</summary>
    void HandleSwapSelectClick(int index)
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        if (index >= slots)
        {
            UIManager.Instance?.ShowToast("这一格还没开启，点已上阵的佣兵");
            return;
        }
        var ordered = _picked;
        if (index >= ordered.Count)
        {
            UIManager.Instance?.ShowToast("这是空格，点要换下的佣兵");
            return;
        }
        string outId = ordered[index];
        if (data != null && data.SwapInDrawPool(_replacingInId, outId))
        {
            UIManager.Instance?.ShowToast("替换成功");
            _swapSelecting = false;
            _replacingInId = null;
            StopSwapSway();   // 先还原格子角度再刷新
            Commit();   // 刷新时点选态已退，SwapSel 标记由 RefreshPrefabPoolSlot 清掉
        }
        else
        {
            Debug.LogError("[MercDrawPoolUI] 替换失败：换上=" + _replacingInId + " 换下=" + outId);
        }
    }

    /// <summary>
    /// 点选态标记：队列已上阵格子盖黄「换下」（raycast 不拦点击，点格即换）；
    /// 选择期间收掉红「−」，防误触出队触发自动补位打乱替换。
    /// </summary>
    void ApplySwapSelectMarks()
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        var ordered = _picked;
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            if (_poolSlots[i] == null) continue;
            var tr = _poolSlots[i].transform;
            var qx = tr.Find("quxiao");
            if (qx != null) qx.gameObject.SetActive(false);
            if (i >= slots || i >= ordered.Count) continue;
            var mark = NewImage(tr, "SwapSel", new Color(1f, 0.80f, 0.25f, 0.45f));
            Stretch(mark.rectTransform);
            mark.transform.SetAsLastSibling();   // 压在立绘最上层
            MakeText(mark.transform, "Label", "换下", 22);
        }
    }

    /// <summary>退点选态：停晃动、清状态并 Refresh 还原红「−」等常规态（非点选态调用直接返回）。</summary>
    void ExitSwapSelecting()
    {
        if (!_swapSelecting) return;
        _swapSelecting = false;
        _replacingInId = null;
        StopSwapSway();
        Refresh();
    }

    // ---- 点选态晃动动画（绕中心左右旋转，主人 2026-10-18 拍板：不要位移）----

    /// <summary>开始晃队列已上阵格子；重复调用先还原再重采基准角度（切换替换目标时会带着偏转二次进入）。
    /// 2026-10-18 主人拍板：每张卡独立晃（各自相位），不再整排同摆；绕各自中心左右旋转、不位移。</summary>
    void StartSwapSway()
    {
        StopSwapSway();
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        var ordered = _picked;
        var targets = new List<GameObject>();
        for (int i = 0; i < _poolSlots.Count && i < slots && i < ordered.Count; i++)
        {
            var go = _poolSlots[i];
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;
            _swapSwayBase[go] = rt.localEulerAngles.z;   // 基准 = 格子自身旋转 Z（预制体摆的角度）
            _swapSwayPhase[go] = i * 1.7f + 0.6f;      // 每卡错开相位（不整除 2π，避免肉眼同步）
            targets.Add(go);
        }
        if (targets.Count == 0) return;
        _swapSwayCo = StartCoroutine(CoSwapSway(targets));
    }

    /// <summary>停晃动并把格子旋转还原到基准（幂等：没在晃时调用只清缓存）。</summary>
    void StopSwapSway()
    {
        if (_swapSwayCo != null)
        {
            StopCoroutine(_swapSwayCo);
            _swapSwayCo = null;
        }
        foreach (var kv in _swapSwayBase)
        {
            if (kv.Key == null) continue;
            var rt = kv.Key.GetComponent<RectTransform>();
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, kv.Value);
        }
        _swapSwayBase.Clear();
        _swapSwayPhase.Clear();
    }

    /// <summary>左右旋转协程：每格绕自身中心正弦摆旋转 Z（不位移），相位独立（2026-10-18 主人拍板）；
    /// 仅在点选态运行；用 unscaled 时间防 timescale 异常卡死。</summary>
    System.Collections.IEnumerator CoSwapSway(List<GameObject> targets)
    {
        float t = 0f;
        while (_swapSelecting)
        {
            t += Time.unscaledDeltaTime * SwapSwaySpeed;
            for (int i = 0; i < targets.Count; i++)
            {
                var go = targets[i];
                if (go == null || !_swapSwayBase.TryGetValue(go, out var baseZ)) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                float phase = _swapSwayPhase.TryGetValue(go, out float ph) ? ph : 0f;
                rt.localRotation = Quaternion.Euler(0f, 0f, baseZ + Mathf.Sin(t + phase) * SwapSwayAmpDeg);
            }
            yield return null;
        }
    }

    void RefreshReplacePanel()
    {
        for (int i = 0; i < _replaceCards.Count; i++)
            if (_replaceCards[i] != null) Destroy(_replaceCards[i]);
        _replaceCards.Clear();

        var ordered = _picked;
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

    /// <summary>
    /// 佣兵详情：直接用战斗里的「获得佣兵」弹窗（huodeyongbing，信息最全，2026-10-16 主人拍板）。
    /// 已拥有的佣兵带存档里的等级/星级；未获得的按 1 级 1 星展示，缺数据时弹窗自己 Close，不另写第二份。
    /// 【2026-10-18 主人拍板】showReplace=true 时弹窗右下显示「替换」钮（仅佣兵池的未入队已获得佣兵用）。
    /// </summary>
    static void ShowMercDetail(string hireId, MercRosterDefs.Def def,
        bool showReplace = false, System.Action onReplace = null)
    {
        int level = 1, star = 1;
        var data = SaveSystem.Instance?.Data;
        if (data?.permanentMercs != null)
        {
            for (int i = 0; i < data.permanentMercs.Count; i++)
            {
                var m = data.permanentMercs[i];
                if (m == null) continue;
                if (m.mercId == hireId || m.mercId == def.AssetId)
                {
                    level = Mathf.Max(1, m.level);
                    star = Mathf.Clamp(m.star < 1 ? 1 : m.star, 1, 5);
                    break;
                }
            }
        }
        MercObtainPopupUI.ShowFromEntry(hireId, def.AssetId, level, star, showReplace, onReplace);
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

    /// <summary>
    /// 稀有度降序（传说 → 稀有 → 普通），同档双重排序【2026-10-20 主人拍板】：
    /// 第二键 = 获得顺序（<see cref="SaveData.mercUnlockOrder"/>，早获得在前）；
    /// 无获得记录的（旧档存量 / 未获得列表）按花名册表序排在该档末尾。
    /// 仅用于下方图鉴列表（已获得/未获得段）；雇佣队列【2026-10-20 主人拍板】不再走本方法，
    /// 改为按玩家放置顺序（SaveData.mercDrawPool 保序 List）展示。
    /// </summary>
    static List<string> SortByRarity(HashSet<string> ids)
    {
        // 获得顺序表：HireId → 首次下标（越早越小）；重复条目取最小下标（幂等记录不会重复，防御）
        var orderMap = new Dictionary<string, int>();
        var order = SaveSystem.Instance?.Data?.mercUnlockOrder;
        if (order != null)
            for (int i = 0; i < order.Count; i++)
            {
                var e = order[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (!orderMap.TryGetValue(e.id, out int prev) || i < prev) orderMap[e.id] = i;
            }

        var result = new List<string>();
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        for (int pass = 0; pass < 3; pass++)
        {
            var tier = pass == 0 ? MercRosterDefs.MercRarity.Legendary
                : pass == 1 ? MercRosterDefs.MercRarity.Rare
                : MercRosterDefs.MercRarity.Common;
            // 同档键：有获得记录用记录下标；无记录 = 大数偏移保持表序（i 越大键越大，稳定垫底不互相乱序）
            var keyed = new List<KeyValuePair<string, long>>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Rarity != tier || !ids.Contains(all[i].HireId)) continue;
                long key = orderMap.TryGetValue(all[i].HireId, out int idx)
                    ? idx
                    : long.MaxValue - (all.Count - i);
                keyed.Add(new KeyValuePair<string, long>(all[i].HireId, key));
            }
            keyed.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < keyed.Count; i++) result.Add(keyed[i].Key);
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
