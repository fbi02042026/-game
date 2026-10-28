using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 邮件列表窗（2026-10-16 邮件 UI 改造 T5）：主界面信件入口两级弹窗的第一级。
/// 无标签、无行内领取按钮、无全选；底部「一键领取」「删除已读」；点击行进 <see cref="MailDetailUI"/>。
/// 行 = 主人在预制体里摆的两条横条模板（Window/未领取、Window/已领取），运行时按状态克隆进 Content 滚动：
/// 未领 → 未领取横条（看过没领不变灰，只消红点）；已领 → 已领取横条 + 整行乘灰。
/// 横条 icon（底框下）：有可领取物品 → icon礼物，没有 → icon信件（ApplyRowVisual 切 sprite）。
/// 纯时间倒序，最新的在最上（不按领取状态分组）。布局常量集中本文件顶部，真机对位偏差在这里调。
/// </summary>
public class MailListUI : MonoBehaviour
{
    public static MailListUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/MailListWindow";   // MailPrefabBuilder 生成（T4），几何以预制体为准

    // ---- 美术资源（Resources 路径，不带扩展名）----
    const string ArtDir = "UI/Art/邮件";
    const string SpriteWindow = ArtDir + "/_0010_底框";
    const string SpriteClose = ArtDir + "/_0005_关闭";
    const string SpriteBtnClaimAll = ArtDir + "/_0006_一键领取";
    const string SpriteBtnDeleteRead = ArtDir + "/_0007_删除已读";
    const string SpriteIconGift = ArtDir + "/icon礼物";      // 礼物：有可领取物品
    const string SpriteIconLetter = ArtDir + "/icon信件";    // 信件：无可领取物品（公告/已领完）

    // ---- 配色（仅代码兜底分支用；预制体分支颜色以预制体为准）----
    static readonly Color ColDim = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color ColInkDim = new Color(0.47f, 0.36f, 0.24f, 1f);    // 空态提示字
    static readonly Color ColBtnTextOrange = new Color(0.42f, 0.22f, 0.06f, 1f); // 橙按钮字
    static readonly Color ColBtnTextBlue = new Color(0.95f, 0.93f, 0.88f, 1f);   // 蓝按钮字

    // ---- 布局（底框 _0010_ 真实 636×949，窗体等比 → preserveAspect 正好铺满，坐标所见即所得）----
    // 底图分区：顶部蓝边 0~10%（标题/关闭），羊皮纸区 10%~85%，底部蓝条带 85%~100%（两个按钮）。
    const float WINDOW_W = 636f;
    const float WINDOW_H = 949f;
    const float ROW_W = 576f;    // 代码兜底分支滚动区宽（预制体分支几何以预制体为准）
    // 行几何按主人预制体模板实测：底框 512×100 → 行高 100；行根随列表区拉伸，底框居中自带留边
    const float ROW_H = 100f;
    const float ROW_GAP = 8f;
    const float COL_CLAIMED_GRAY = 0.6f;   // 已领取整行乘灰系数（0~1，越小越灰）

    readonly List<MailRow> _rows = new List<MailRow>();
    RectTransform _listContent;
    Text _emptyText;
    Sprite _spWindow, _spClose, _spClaimAll, _spDeleteRead, _spIconGift, _spIconLetter;
    bool _spritesResolved;

    RectTransform _tplUnclaimed;   // 未领取行模板（2026-10-22 起优先用主人新加的「未领取按钮」模板，带 lingqu 领取按钮）
    RectTransform _tplClaimed;     // 「已领取」行模板（含印章节点）
    RectTransform _tplLegacyUnclaimed;   // 旧「未领取」模板（没有领取按钮）：不参与克隆，但要在 RebuildRows 里一并隐藏防串场

    class MailRow
    {
        public GameObject go;
        public Text title;           // 模板 Text 节点（标题）
        public Text time;            // 模板 Time 节点（仅未领取模板有）
        public GameObject unreadDot; // 模板 红点 节点（只在未领取模板绑；已领取模板同名节点是印章，不绑不动）
        public Image barIcon;        // 模板 icon 节点（底框下）：有可领物品 → 礼物，没有 → 信件
        public readonly List<Graphic> graphics = new List<Graphic>();   // 行内全部 Graphic
        public readonly List<Color> baseColors = new List<Color>();     // 克隆时的原始色（变灰基准，防叠加）
        public MailEntry mail;
    }

    public static void Show() => Ensure().Open();

    /// <summary>
    /// 🆕 2026-10-18 主人反馈「关了再开才看到新邮件」：新邮件入箱时调用本方法——
    /// 邮箱正开着就立刻重建列表，不用关掉重开。关着时 no-op（下次 Open 本就会全量刷新）。
    /// 调用方：MailSystem.Enqueue / EnqueueResourceOverflow。
    /// </summary>
    public static void NotifyInboxChanged()
    {
        if (Instance == null) return;
        if (!Instance.gameObject.activeInHierarchy) return;
        Instance.Refresh();
    }

    static MailListUI Ensure()
    {
        if (Instance != null) return Instance;

        // 预制体优先（T4）：几何以 MailListWindow.prefab 为准，代码只绑定+填数据；缺失走代码兜底（同 DailyLoginUI 模式）
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "MailListUI";
            DontDestroyOnLoad(go);
            var ui = go.GetComponent<MailListUI>();
            if (ui == null) ui = go.AddComponent<MailListUI>();
            ui.Build();
            return ui;
        }

        var fb = new GameObject("MailListUI", typeof(RectTransform));
        DontDestroyOnLoad(fb);
        var ui2 = fb.AddComponent<MailListUI>();
        ui2.Build();
        return ui2;
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

    public void Open()
    {
        gameObject.SetActive(true);
        // 【2026-10-22 主人拍板】SeedDemoMailIfEmpty 演示信已删——主人从没见过演示信（被欢迎信占位挡住），无兼容顾虑。
        // 欢迎信触发已改挂：① 玩完新手引导（StoryProgress.MarkTutorialDone）；② 已通关老玩家回主界面补发（TownSceneBootstrap）。
        // 这里只做列表刷新。
        Refresh();
    }

    public void Close()
    {
        Destroy(gameObject);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Build
    // ────────────────────────────────────────────────────────────────────────

    void Build()
    {
        ResolveSprites();   // 窗体/按钮 sprite 只服务代码兜底分支；行美术全在预制体模板里

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        // 2026-10-16 修：MailPrefabBuilder 生成的树是 根 → Root → Window，
        // 以前 transform.Find("Window") 只找根的直接子节点，永远找不到 → 恒走代码兜底分支，
        // 主人在预制体里摆的「未领取/已领取」行模板全部没绑上（运行时报「行模板缺失」、列表恒空）。
        if (FindPrefabWindow() != null)
        {
            BindFromPrefab();   // 预制体分支：几何以预制体为准，只按名绑定（MailPrefabBuilder 生成的树）
        }
        else
        {
            var rootRt = (RectTransform)transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            BuildDim();
            BuildWindow();
        }

        UICanvasSetup.ApplyPopup(canvas, 600, UICanvasSetup.ResolveUiCamera());

        gameObject.SetActive(false);
    }

    /// <summary>找预制体窗体根：Root/Window 优先（MailPrefabBuilder 生成结构），裸 Window 兼容旧结构。</summary>
    Transform FindPrefabWindow()
    {
        Transform t = transform.Find("Root/Window");
        if (t != null) return t;
        return transform.Find("Window");
    }

    /// <summary>预制体分支：按节点名绑定（与 MailPrefabBuilder 生成的树一一对应；缺节点 LogError 不兜底建）。</summary>
    void BindFromPrefab()
    {
        Transform window = FindPrefabWindow();

        Transform content = FindDeep(window, "Content");
        _listContent = content as RectTransform;
        if (_listContent == null)
            Debug.LogError("[MailList] 预制体缺 Window/MailList/Content，列表无法填充");

        Transform empty = FindDeep(window, "EmptyText");
        _emptyText = empty != null ? empty.GetComponent<Text>() : null;
        if (_emptyText == null)
            Debug.LogError("[MailList] 预制体缺 Window/EmptyText，空态提示不显示");

        // 行模板（主人 2026-10-16 在预制体里摆好的「未领取」/「已领取」两条横条）：运行时隐藏，只作克隆源
        // 【2026-10-22 施工单 T12】主人在「未领取」行里加了领取按钮节点 —— 实际落在新节点「未领取按钮」
        //（完整行模板副本 + 子节点 lingqu，Image+Button 齐全）。克隆源优先用它，旧「未领取」只藏不用；
        // 缺模板/缺 lingqu 按钮 LogError 不兜底（与下方 BindPrefabButton 同口径）。
        _tplUnclaimed = window.Find("未领取按钮") as RectTransform;
        if (_tplUnclaimed != null)
        {
            var lingquBtn = FindDeep(_tplUnclaimed, "lingqu") != null
                ? FindDeep(_tplUnclaimed, "lingqu").GetComponent<Button>() : null;
            if (lingquBtn == null)
                Debug.LogError("[MailList] 预制体 未领取按钮 模板缺 lingqu 领取按钮（或无 Button），行内领取不可用");
        }
        else
        {
            _tplUnclaimed = window.Find("未领取") as RectTransform;
            Debug.LogError("[MailList] 预制体缺 Window/未领取按钮 行模板（含 lingqu 领取按钮），回退旧「未领取」模板——行内领取不可用");
        }
        _tplClaimed = window.Find("已领取") as RectTransform;
        if (_tplUnclaimed == null)
            Debug.LogError("[MailList] 预制体缺 Window/未领取 行模板，列表行无法生成");
        if (_tplClaimed == null)
            Debug.LogError("[MailList] 预制体缺 Window/已领取 行模板，已领行无法生成");
        // 旧「未领取」模板记住引用：RebuildRows 里与现行模板一起强制隐藏（防运行时叠在窗体中央）
        if (_tplUnclaimed != null && _tplLegacyUnclaimed == null && window.Find("未领取") is RectTransform legacy && legacy != _tplUnclaimed)
            _tplLegacyUnclaimed = legacy;
        if (_tplUnclaimed != null) _tplUnclaimed.gameObject.SetActive(false);
        if (_tplClaimed != null) _tplClaimed.gameObject.SetActive(false);
        if (_tplLegacyUnclaimed != null) _tplLegacyUnclaimed.gameObject.SetActive(false);

        // 【2026-10-17 修·主人报「删除已读/一键领取/关闭点击无反应」】根因：预制体分支只摆了节点没挂监听，
        // 监听只存在于代码兜底分支 BuildWindow 里。现按名补绑（节点由 MailPrefabBuilder 生成、自带 Button）。
        // ⚠️ 关闭按钮预制体里叫 BtnClose（不是 CloseButton）；Dim 遮罩按 builder 注释口径「点它关闭」一并绑。
        BindPrefabButton(window, "ClaimAllButton", OnClaimAll, "一键领取");
        BindPrefabButton(window, "DeleteReadButton", OnDeleteRead, "删除已读");
        BindPrefabButton(window, "BtnClose", Close, "关闭");
        if (window.parent != null)
            BindPrefabButton(window.parent, "Dim", Close, "遮罩关闭");
    }

    /// <summary>预制体分支按钮绑定：按名找节点挂监听；缺节点/缺 Button 只报错不兜底建（与 LoginTapRowBuilder 同口径）。</summary>
    void BindPrefabButton(Transform window, string name, UnityEngine.Events.UnityAction onClick, string label)
    {
        var t = FindDeep(window, name);
        if (t == null)
        {
            Debug.LogError($"[MailList] 预制体缺 {name}（{label}）按钮节点");
            return;
        }
        var btn = t.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError($"[MailList] 预制体 {name} 缺 Button 组件，无法绑定点击");
            return;
        }
        btn.onClick.RemoveListener(onClick);   // 防重复叠加
        btn.onClick.AddListener(onClick);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(parent, name);
    }

    void BuildDim()
    {
        var go = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = ColDim;
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(Close);
    }

    void BuildWindow()
    {
        var go = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(WINDOW_W, WINDOW_H);

        var img = go.GetComponent<Image>();
        img.sprite = _spWindow;
        img.raycastTarget = true;
        img.preserveAspect = true;   // 窗体与底图 636×949 同比例 → 正好铺满，坐标所见即所得

        // 标题（顶部蓝边带 y≈+430；左上信封是底图自带装饰，标题居中不冲突）
        var title = NewText(go.transform, "Title", "邮件", 34, TextAnchor.MiddleCenter, ColBtnTextBlue);
        var trt = (RectTransform)title.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 430f);
        trt.sizeDelta = new Vector2(400f, 56f);
        title.fontStyle = FontStyle.Bold;

        // 关闭按钮（_0005_ 自带 X，不叠字）
        var closeGo = NewIconButton(go.transform, "CloseButton", _spClose, new Vector2(64f, 65f), Close, null, Color.white);
        var crt = (RectTransform)closeGo.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(250f, 424f);

        // 列表区（底图羊皮纸区 y ≈ -300~+380）
        _listContent = BuildList(go.transform);

        // 空态（羊皮纸中心，深棕字）
        _emptyText = NewText(go.transform, "EmptyText", "暂无邮件", 26, TextAnchor.MiddleCenter, ColInkDim);
        var ert = (RectTransform)_emptyText.transform;
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
        ert.anchoredPosition = new Vector2(0f, 40f);
        ert.sizeDelta = new Vector2(560f, 60f);
        _emptyText.raycastTarget = false;
        _emptyText.gameObject.SetActive(false);

        // 底部按钮（对齐底图蓝条带中心 y≈-390；_0006_/_0007_ 是无字空框 → 叠 Label）
        var claimGo = NewIconButton(go.transform, "ClaimAllButton", _spClaimAll, new Vector2(200f, 66f), OnClaimAll,
            "一键领取", ColBtnTextOrange);
        var clt = (RectTransform)claimGo.transform;
        clt.anchorMin = clt.anchorMax = new Vector2(0.5f, 0.5f);
        clt.anchoredPosition = new Vector2(-145f, -390f);

        var delGo = NewIconButton(go.transform, "DeleteReadButton", _spDeleteRead, new Vector2(200f, 66f), OnDeleteRead,
            "删除已读", ColBtnTextBlue);
        var dlt = (RectTransform)delGo.transform;
        dlt.anchorMin = dlt.anchorMax = new Vector2(0.5f, 0.5f);
        dlt.anchoredPosition = new Vector2(145f, -390f);
    }

    RectTransform BuildList(Transform window)
    {
        var sv = new GameObject("MailList", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        sv.transform.SetParent(window, false);
        var srt = (RectTransform)sv.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 36f);
        srt.sizeDelta = new Vector2(ROW_W, 640f);
        var svImg = sv.GetComponent<Image>();
        svImg.color = Color.clear;   // 背景透明：羊皮纸由底图提供，不再压暗出分界线
        svImg.raycastTarget = true;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(sv.transform, false);
        var vrt = (RectTransform)viewport.transform;
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        var vImg = viewport.GetComponent<Image>();
        vImg.color = Color.clear;
        vImg.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 10f);

        var scroll = sv.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        return crt;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 刷新
    // ────────────────────────────────────────────────────────────────────────

    void ResolveSprites()
    {
        if (_spritesResolved) return;
        _spritesResolved = true;
        _spWindow = LoadSprite(SpriteWindow);
        _spClose = LoadSprite(SpriteClose);
        _spClaimAll = LoadSprite(SpriteBtnClaimAll);
        _spDeleteRead = LoadSprite(SpriteBtnDeleteRead);
        _spIconGift = LoadSprite(SpriteIconGift);
        _spIconLetter = LoadSprite(SpriteIconLetter);
    }

    static Sprite LoadSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError("[MailList] 缺少美术资源: " + path);
        return sp;
    }

    public void Refresh()
    {
        ResolveSprites();
        RebuildRows();
        ApplyRowVisual();
    }

    void RebuildRows()
    {
        // 2026-10-16 加固：模板本体只作克隆源——重建前再强制隐藏一次，
        // 防止 Play 模式中改预制体等操作把「未领取/已领取」两条模板复活、叠在窗体中央
        if (_tplUnclaimed != null && _tplUnclaimed.gameObject.activeSelf)
            _tplUnclaimed.gameObject.SetActive(false);
        if (_tplClaimed != null && _tplClaimed.gameObject.activeSelf)
            _tplClaimed.gameObject.SetActive(false);
        // 【2026-10-22 T12】旧「未领取」模板（无领取按钮）一并压住，防串场
        if (_tplLegacyUnclaimed != null && _tplLegacyUnclaimed.gameObject.activeSelf)
            _tplLegacyUnclaimed.gameObject.SetActive(false);

        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r != null && r.go != null) Destroy(r.go);
        }
        _rows.Clear();

        if (_tplUnclaimed == null || _tplClaimed == null)
        {
            // 不兜底：模板缺了就要让人看见
            Debug.LogError("[MailList] 行模板缺失（未领取/已领取），列表行无法生成");
            if (_listContent != null) _listContent.sizeDelta = new Vector2(0f, 10f);
            if (_emptyText != null) _emptyText.gameObject.SetActive(true);
            return;
        }

        var inbox = MailSystem.GetInbox();
        var sorted = new List<MailEntry>();
        if (inbox != null)
        {
            for (int i = 0; i < inbox.Count; i++)
                if (inbox[i] != null) sorted.Add(inbox[i]);
        }
        // 纯时间倒序，最新的在最上（不按领取状态分组——主人 2026-10-16 拍板）
        sorted.Sort((a, b) => b.createTime.CompareTo(a.createTime));

        for (int i = 0; i < sorted.Count; i++)
            _rows.Add(CreateRowFromTemplate(sorted[i], i));

        _listContent.sizeDelta = new Vector2(0f, Mathf.Max(sorted.Count * (ROW_H + ROW_GAP), 10f));
        if (_emptyText != null)
            _emptyText.gameObject.SetActive(sorted.Count == 0);
    }

    MailRow CreateRowFromTemplate(MailEntry mail, int index)
    {
        // 按状态选横条：已领取 → 已领取模板；未领取（含看过没领）→ 未领取模板
        var tpl = mail.claimed ? _tplClaimed : _tplUnclaimed;
        var go = Instantiate(tpl.gameObject, _listContent);
        go.name = "MailRow_" + index;
        go.SetActive(true);   // 模板本体是隐藏的，克隆体要显式打开

        var rt = (RectTransform)go.transform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -(ROW_H + ROW_GAP) * index);
        rt.sizeDelta = new Vector2(0f, ROW_H);

        var row = new MailRow { go = go, mail = mail };

        // 基色快照：整体变灰在这里按基色乘系数（Refresh 全量重建行，不会越刷越灰）
        var graphics = go.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            row.graphics.Add(graphics[i]);
            row.baseColors.Add(graphics[i].color);
        }

        var titleT = FindDeep(go.transform, "Text");
        row.title = titleT != null ? titleT.GetComponent<Text>() : null;
        if (row.title == null) Debug.LogError("[MailList] 行模板缺 Text 节点，标题显示不了");

        var timeT = FindDeep(go.transform, "Time");
        row.time = timeT != null ? timeT.GetComponent<Text>() : null;

        // 只在未领取模板绑红点（已领取模板的同名节点是印章，不绑不动）
        if (!mail.claimed)
        {
            var dotT = FindDeep(go.transform, "红点");
            row.unreadDot = dotT != null ? dotT.gameObject : null;
        }

        // 横条 icon（底框下，主人 2026-10-16 加的）：sprite 在 ApplyRowVisual 按状态切 礼物/信件
        var iconT = FindDeep(go.transform, "icon");
        row.barIcon = iconT != null ? iconT.GetComponent<Image>() : null;

        // 模板文字是编辑器里随手摆的（内置 Arial），克隆后统一换中文字体 + 填真实数据
        if (row.title != null)
        {
            row.title.text = mail.title;
            row.title.font = GameFonts.GetChinese();
        }
        if (row.time != null)
        {
            row.time.text = FormatRelativeTime(mail.createTime);
            row.time.font = GameFonts.GetChinese();
        }

        // 点击整行 → 详情（射线命中底框/文字后沿层级上溯到行根的 Button）
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        string mailId = mail.id;
        btn.onClick.AddListener(() => OnRowClicked(mailId));

        // 【2026-10-22 施工单 T12】行内「领取」按钮（模板子节点 lingqu，监听代码绑、几何一个像素不动）：
        // 点击 = 直接领取该封（含体力超限确认，与详情窗 OnClaimClicked 同口径）；缺节点已在 BindFromPrefab 报过错。
        if (!mail.claimed)
        {
            var claimT = FindDeep(go.transform, "lingqu");
            var claimBtn = claimT != null ? claimT.GetComponent<Button>() : null;
            if (claimBtn != null)
                claimBtn.onClick.AddListener(() => OnRowClaimClicked(mailId));
        }

        return row;
    }

    /// <summary>
    /// 【2026-10-22 施工单 T12】行内领取：先过体力超限确认（与详情窗同口径），再 TryClaim。
    /// TryClaim 返回 false（如碎片邮件背包放不下）时 MailSystem 内部已 toast 原因、邮件保持未领，这里不重复报。
    /// </summary>
    void OnRowClaimClicked(string mailId)
    {
        MailEntry mail = null;
        var inbox = MailSystem.GetInbox();
        for (int i = 0; i < inbox.Count; i++)
        {
            if (inbox[i] != null && inbox[i].id == mailId) { mail = inbox[i]; break; }
        }
        if (mail == null || mail.claimed) return;

        int waste = MailSystem.GetStaminaWasteOnClaim(mail);
        if (waste > 0)
        {
            StaminaConfirmPopup.Show(
                $"体力将超出上限，多余的 {waste} 点会浪费。\n是否仍要领取？",
                "仍要领取",
                () => DoRowClaim(mailId));
            return;
        }
        DoRowClaim(mailId);
    }

    void DoRowClaim(string mailId)
    {
        if (!MailSystem.TryClaim(mailId)) return;
        GuildHallUI.RefreshAllHudStatic();
        Refresh();   // 全量重建：领完的行走「已领取」模板（灰态+印章），红点/图标同步
    }

    void ApplyRowVisual()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r == null || r.go == null || r.mail == null) continue;
            var m = r.mail;
            // 红点：未领取且没看过才亮；看过没领不变灰、横条不变，只消红点
            if (r.unreadDot != null) r.unreadDot.SetActive(!m.claimed && !m.read);
            // 横条 icon：有可领取物品 → 礼物；没有（公告/已领完）→ 信件
            if (r.barIcon != null) r.barIcon.sprite = HasReward(m) ? _spIconGift : _spIconLetter;
            // 已领取 → 整行（底图/文字/印章）乘灰
            SetRowGray(r, m.claimed);
        }
    }

    /// <summary>按克隆时快照的基色乘灰系数，不基于当前色（防多次刷新越乘越灰）。</summary>
    void SetRowGray(MailRow row, bool gray)
    {
        for (int i = 0; i < row.graphics.Count; i++)
        {
            var g = row.graphics[i];
            if (g == null) continue;
            var c = row.baseColors[i];
            g.color = gray ? new Color(c.r * COL_CLAIMED_GRAY, c.g * COL_CLAIMED_GRAY, c.b * COL_CLAIMED_GRAY, c.a)
                           : c;
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        // 【2026-10-17 调试辅助·主人要求】邮件列表开着时按 F9，直接弹第一封信的详情窗，
        // 免去逐封点开对布局（配图/附件格/领取按钮位置）。仅编辑器生效，真机包不含此代码。
        if (Input.GetKeyDown(KeyCode.F9))
        {
            var inbox = MailSystem.GetInbox();
            if (inbox != null && inbox.Count > 0)
            {
                Debug.Log($"[MailList] F9 预览第一封信：{inbox[0].title}（id={inbox[0].id}）");
                MailDetailUI.Show(inbox[0].id);
            }
            else
            {
                Debug.LogWarning("[MailList] 收件箱为空，F9 没有信可预览");
            }
        }
    }
#endif

    void OnRowClicked(string mailId)
    {
        MailSystem.MarkAsRead(mailId);
        MailDetailUI.Show(mailId);
        // 看过没领：不变灰、横条不变，只把红点当场消掉（领取后的灰态由详情窗关闭时 Refresh 全量重建）
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r == null || r.mail == null || r.mail.id != mailId) continue;
            if (r.unreadDot != null) r.unreadDot.SetActive(false);
            break;
        }
    }

    void OnClaimAll()
    {
        // 【2026-10-18 主人拍板】一键领取含体力且会超出上限：先确认「多余的会浪费」，确认后才领
        int waste = MailSystem.GetStaminaWasteOnClaimAll();
        if (waste > 0)
        {
            StaminaConfirmPopup.Show(
                $"有体力邮件领取后将超出上限，多余的 {waste} 点体力会浪费。\n是否仍要一键领取？",
                "仍要领取",
                DoClaimAll);
            return;
        }
        DoClaimAll();
    }

    void DoClaimAll()
    {
        int claimed = MailSystem.ClaimAll();
        if (claimed > 0)
            UIManager.Instance?.ShowToast($"已领取 {claimed} 封邮件");
        else
            UIManager.Instance?.ShowToast("没有可领取的邮件");
        GuildHallUI.RefreshAllHudStatic();
        Refresh();
    }

    void OnDeleteRead()
    {
        int removed = MailSystem.DeleteRead();
        if (removed > 0)
            UIManager.Instance?.ShowToast($"已删除 {removed} 封已读邮件");
        else
            UIManager.Instance?.ShowToast("没有可删除的已读邮件");
        Refresh();
    }

    // 【2026-10-22 主人拍板】SeedDemoMailIfEmpty 演示信已整段删除（主人从没见过演示信，无兼容顾虑）。

    /// <summary>
    /// 相对时间（模板样张口径「1天前」）：刚刚 / N分钟前 / N小时前 / N天前。
    /// </summary>
    static string FormatRelativeTime(long unixSeconds)
    {
        if (unixSeconds <= 0) return string.Empty;
        long diff = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unixSeconds;
        if (diff < 0) diff = 0;
        if (diff < 60) return "刚刚";
        if (diff < 3600) return (diff / 60) + "分钟前";
        if (diff < 86400) return (diff / 3600) + "小时前";
        return (diff / 86400) + "天前";
    }

    /// <summary>是否还有可领取物品（flat 字段或结构化附件仍有余量）。</summary>
    static bool HasReward(MailEntry m)
    {
        if (m.gold > 0 || m.diamond > 0 || m.stamina > 0
            || m.enchantStones > 0 || m.decomposeMats > 0 || m.talentPoints > 0)
            return true;
        if (m.attachments == null) return false;
        for (int i = 0; i < m.attachments.Count; i++)
            if (m.attachments[i] != null && m.attachments[i].amount > 0) return true;
        return false;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 构建工具
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 整图按钮：图片即按钮。美术给的「一键领取/删除已读」是无字空框 → 叠 label；传 label=null 则不叠（关闭钻自带 X）。
    /// </summary>
    GameObject NewIconButton(Transform parent, string name, Sprite sprite, Vector2 size, UnityEngine.Events.UnityAction onClick, string label, Color labelColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = true;
        img.preserveAspect = true;
        if (img.sprite == null)
            Debug.LogError("[MailList] 按钮缺图（sprite 为 null）: " + name);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;

        if (!string.IsNullOrEmpty(label))
        {
            var t = NewText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter, labelColor);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }
        return go;
    }

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align, Color color)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFactory.NewTextColored(parent, name, content, size, align, color);
    }
}
