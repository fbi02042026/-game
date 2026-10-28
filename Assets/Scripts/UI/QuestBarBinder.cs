using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面任务栏绑定器（2026-10-26 主人拍板：任务统一走主界面任务栏，旧 QuestHudBar 保持停用）。
///
/// 把美术搭的「任务栏」（GuildHallUI → LeftBar → 任务栏）接到 QuestBarData 上：
///   · 三行按名字找：主线 / 支线 / 日常 —— 找不到正文 Text 或缺行就 LogError + dump 子节点，不静默；
///   · 没有支线 → 隐藏支线行、日常行上移（SetSiblingIndex）、栏根黑底高度缩减；
///     支线回来 → 还原高度与行序（嵌套 Canvas 的渲染层级问题与本绑定无关，行序用 SetSiblingIndex 足够）；
///   · 行点击走 QuestJumpRouter（绝不让点击没反应；跳转失败回退展开 QuestPanelUI）；
///   · 折叠按钮（2026-10-26 主人提醒）：点一下把整栏收起来（三行全收 + 黑底缩到只剩按钮那截），
///     再点展开还原；折叠态纯会话内，不写存档；支线显隐联动与折叠态互不干扰；
///   · 0.5s 轮询刷新文案（与 MainQuestSystem.Tick 同频，纯读不结算）。
///
/// ⚠ 行结构（2026-10-26 主人发的层级图定稿）：任务栏 = pan / Dim / 折叠 / Tile / 主线 / 支线 / 日常，
///   每行 = Icon / Name（行名标签）/ Info（正文，绑这个刷文案）/ 前往（点击跳转，绑这个）。
///   「Info」「前往」找不到时退回按名字/首个 Text 的老判法兜美术改版，兜不上会 LogError dump。
/// </summary>
public class QuestBarBinder : MonoBehaviour
{
    const string BarNode = "任务栏";
    const string RowMain = "主线";
    const string RowSide = "支线";
    const string RowDaily = "日常";
    const float RefreshInterval = 0.5f;

    /// <summary>折叠按钮的候选节点名（美术叫哪个都能接上；全对不上会 LogError dump 让人看见）。</summary>
    static readonly string[] FoldNodeNames = { "折叠", "收起", "Fold", "Collapse", "箭头", "Arrow" };

    Transform _rowMain, _rowSide, _rowDaily;
    Text _mainText, _sideText, _dailyText;
    RectTransform _dimRt;            // 黑底真身 = panl/Dim（2026-10-27 修复折叠不收缩：原改「任务栏」节点无效）
    Vector2 _dimBaseSize, _dimBasePos;   // Dim 的预制体基准（从 base 重算，不累加）
    bool _dimBaseCaptured;
    RectTransform _foldRt;           // 折叠按钮（黑底收缩后的依附目标）
    float _foldH = -1f, _foldY;      // 折叠按钮高/纵向位置（panl 空间，与 Dim 同一坐标系）
    float _foldX = -1f;              // 折叠按钮展开态基准 x（panl 空间）；<0 = 未捕获
    float _foldEdgeX;               // 收起态贴边 x：按钮左缘留 FoldEdgeLeftInset，整体挪到屏幕左缘
    /// <summary>折叠按钮收起后贴屏幕左边的留白（按钮左缘离 panl 左=屏幕左的距离，单位逻辑像素）。</summary>
    const float FoldEdgeLeftInset = 8f;
    /// <summary>黑底上下留边总和：基准 300 高 vs 三行实跨 260，上下各 20（折叠后 = 按钮高 + 2×20）。</summary>
    const float DimEdgePadding = 20f;
    int _dailyBaseSibling = -1;      // 日常行原始次序（支线行回来时还原）
    bool _collapsed;                 // 折叠态（会话内，不写存档）
    bool _appliedCollapsed, _appliedSideVisible = true;   // 上次实际套到 UI 的状态（变了才动 UI）
    bool _lastSideVisible = true;    // 最近一次刷新算出的支线可见性（折叠按钮切换时用）
    bool _warnedNoSideRow;
    float _timer;

    // ── 左侧任务逐行弹入（2026-10-28 主人需求「像冒险界面的标签动效一样 一个一个出来」）──
    //   参照物 = 冒险页左侧章节标签的逐颗滑入（AdventureUI.Anim.cs：滑入 + 前 40% 淡入 + 行间错峰）。
    //   只动临时量：行节点的 anchoredPosition + CanvasGroup.alpha，播完精确写回基准值；
    //   不动 anchor / pivot / sizeDelta，不写预制体（铁律 20）。
    /// <summary>单行滑入时长（秒）：与冒险页左侧标签同值（AdventureUI.Anim.TabSlideDur）。</summary>
    const float RowPopDur = 0.114f;
    /// <summary>行间错峰间隔（秒）：与冒险页左侧标签同值（TabPopStagger）。</summary>
    const float RowPopStagger = 0.10f;
    /// <summary>起始位在屏幕左外侧的位移（行宽 100 + 余量 40）。任务栏贴在屏幕左缘，整行左移即滑出屏外。</summary>
    const float RowSlideOut = 140f;
    /// <summary>等主界面入场动效的上限（秒）：超过就不再等（防静态标志卡死）。</summary>
    const float RowWaitEntranceMax = 3f;
    /// <summary>等主界面可见的上限（秒）：片头/开场剧情可能盖很久，最多等这么久（超时照弹，不卡流程）。</summary>
    const float RowWaitVisibleMax = 60f;

    readonly List<Coroutine> _rowPopCos = new List<Coroutine>();
    readonly Dictionary<Transform, Vector2> _rowPopBasePos = new Dictionary<Transform, Vector2>();
    readonly Dictionary<Transform, CanvasGroup> _rowPopGroups = new Dictionary<Transform, CanvasGroup>();

    /// <summary>
    /// 进镇挂载入口（TownSceneManager 实例化 GuildHallUI 后调）。
    /// 找不到「任务栏」节点只报错不建树 —— 排版以美术预制体为准（铁律 9），缺了让美术补。
    /// </summary>
    public static void Attach(GameObject hall)
    {
        if (hall == null || hall.GetComponent<QuestBarBinder>() != null) return;
        var bar = FindDeep(hall.transform, BarNode);
        if (bar == null)
        {
            DumpChildren(hall.transform, 0);
            Debug.LogError($"[QuestBarBinder] GuildHallUI 下没找到「{BarNode}」节点（子节点见上），任务栏不挂");
            return;
        }
        var binder = hall.AddComponent<QuestBarBinder>();
        binder.Bind(bar);
    }

    void Bind(Transform bar)
    {
        // 【2026-10-26 主人问「任务栏窗口做了适配没有」→ 之前没做，这里补上】
        // 挂在整条 LeftBar（任务栏的父级，公告/活动/任务栏同列）上：Offset 模式只挪 anchoredPosition，
        // 与顶部资源条（TownSharedChrome）同一套 EnsureTopOffset——安全区 + 微信胶囊预留 + 固定下移 20，
        // 两边同量下移、相对排版不动；编辑器/Android 无胶囊时只避刘海。不碰预制体（项目口径）。
        var columnRt = bar.parent as RectTransform;
        if (columnRt != null)
            SafeAreaFitter.EnsureTopOffset(columnRt);

        _rowMain = FindDeep(bar, RowMain);
        _rowSide = FindDeep(bar, RowSide);
        _rowDaily = FindDeep(bar, RowDaily);

        if (_rowMain == null || _rowDaily == null)
        {
            DumpChildren(bar, 0);
            Debug.LogError($"[QuestBarBinder] 任务栏下找不到「{RowMain}」/「{RowDaily}」行（子节点见上），绑定失败");
            enabled = false;
            return;
        }
        if (_rowSide == null)
            Debug.Log("[QuestBarBinder] 任务栏下没有「支线」行，按常隐处理（数据有支线时会告警一次）");

        _mainText = ContentText(_rowMain, RowMain);
        if (_rowSide != null) _sideText = ContentText(_rowSide, RowSide);
        _dailyText = ContentText(_rowDaily, RowDaily);

        // 【2026-10-27 主人反馈「任务栏正文字太小、第一行字看不清」】
        // prefab 三行 Info 都是 fontSize 15，小到难辨认。这里抬高基础字号并统一框高
        //（铁律 20：字体可改；纵向高度属必要介入）：Info 框纵向 40→52，x 与位置一律不动。
        // 字号具体大小走 FitInfoFont 动态档位（字少大、字多小、超长截断），见 Refresh。
        ApplyInfoStyle(_mainText);
        ApplyInfoStyle(_sideText);
        ApplyInfoStyle(_dailyText);

        BindClick(_rowMain, OnClickMain);
        if (_rowSide != null) BindClick(_rowSide, OnClickSide);
        BindClick(_rowDaily, OnClickDaily);

        // 折叠按钮：候选名挨个找，找到就接切换；找不到报错 dump（主人说栏上有这个按钮，对不上就是名字不匹配）
        var fold = FindFoldButton(bar);
        if (fold == null)
        {
            DumpChildren(bar, 0);
            Debug.LogError($"[QuestBarBinder] 任务栏下没找到折叠按钮（候选名：{string.Join("/", FoldNodeNames)}，子节点见上），折叠功能不生效");
        }

        // 黑底真身：panl/Dim（折叠收缩的目标节点）。缺失报错不静默（铁律 10）。
        var dimT = FindDeep(bar, "Dim");
        _dimRt = dimT as RectTransform;
        if (_dimRt != null)
        {
            _dimBaseSize = _dimRt.sizeDelta;
            _dimBasePos = _dimRt.anchoredPosition;
            _dimBaseCaptured = true;
        }
        else
            Debug.LogError("[QuestBarBinder] 任务栏下没找到黑底节点 panl/Dim，折叠时黑底不会收缩");

        // 【2026-10-27 主人拍板「修」】任务栏 8 张悬空 sprite 按路径接回（AGENTS.md §2.3 口径），见 EnsureBarArt
        EnsureBarArt(_rowMain, _rowSide, _rowDaily, fold, dimT);

        // 显隐联动基线：以预制体当前实际状态为准（支线行若预制体里就是隐藏的，首刷数据来了会正确展开）
        _appliedSideVisible = _rowSide == null || _rowSide.gameObject.activeSelf;
        _lastSideVisible = _appliedSideVisible;

        _timer = RefreshInterval;   // 首刷延半拍：等存档与任务系统就绪
        Debug.Log($"[QuestBarBinder] 任务栏绑定完成（正文 Text：主线={(_mainText != null)} 支线={(_sideText != null)} 日常={(_dailyText != null)}；折叠按钮={fold != null}）");
    }

    /// <summary>任务栏正文基础样式：只定框高（x/位置不碰），字号由 FitInfoFont 按文案长度动态定。</summary>
    static void ApplyInfoStyle(Text t)
    {
        if (t == null) return;
        var rt = t.rectTransform;
        if (rt != null)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 52f);
    }

    /// <summary>
    /// 任务栏正文动态字号（2026-10-27 主人拍板「字少的时候大点、多的时候小点，限定最多字数」）：
    /// 框宽 160 / 高 52 / 行距 1.3（GameFonts 下限），按全角字宽≈字号估算容量，档位——
    ///   ≤7 字 → 20 号（单行大字）；≤14 字 → 18 号（两行）；≤20 字 → 16 号（两行）；
    ///   超过 → 15 号并截断到 19 字 + 「…」（最多展示 20 字），最小 15 号保证看得清。
    /// </summary>
    const int InfoMaxChars = 20;   // 每条任务最多展示字数（含省略号），超出截断
    static void FitInfoFont(Text t)
    {
        if (t == null) return;
        string s = t.text ?? "";
        int len = s.Length;
        t.fontSize = len <= 7 ? 20 : len <= 14 ? 18 : len <= InfoMaxChars ? 16 : 15;
        if (len > InfoMaxChars)
            t.text = s.Substring(0, InfoMaxChars - 1) + "…";
    }

    /// <summary>按候选名找折叠按钮；顺带检查它别挂在三行里面（挂行里 = 折叠时连按钮一起藏，展开不了）。</summary>
    Transform FindFoldButton(Transform bar)
    {
        foreach (var n in FoldNodeNames)
        {
            var t = FindDeep(bar, n);
            if (t == null) continue;
            if (_rowMain != null && t.IsChildOf(_rowMain) ||
                _rowSide != null && t.IsChildOf(_rowSide) ||
                _rowDaily != null && t.IsChildOf(_rowDaily))
            {
                Debug.LogWarning($"[QuestBarBinder] 折叠按钮「{n}」挂在任务行里面，折叠时会被一起藏掉——请把它挪到「{BarNode}」直子级（或行容器外）");
            }
            if (t.GetComponent<Button>() == null && t.GetComponent<Graphic>() == null)
            {
                Debug.LogWarning($"[QuestBarBinder] 节点「{n}」既没 Button 也没 Graphic，点不了——折叠按钮请用有底图/按钮的节点");
                continue;
            }
            BindClick(t, ToggleCollapse);
            _foldRt = t as RectTransform;   // 黑底收缩用：按钮的高与纵位（panl 空间，与 Dim 同坐标系）
            if (_foldRt != null)
            {
                _foldH = _foldRt.rect.height;
                _foldY = _foldRt.anchoredPosition.y;
                _foldX = _foldRt.anchoredPosition.x;   // 展开态基准 x（panl 空间），收起时改、展开时还原
                // 收起后贴屏幕左边：按钮左缘离 panl 左缘（=屏幕左缘）留 FoldEdgeLeftInset。
                // 用「旋转后的实际包围盒宽」推算（折叠箭头旋了 -90°，视觉宽=rect.height 而非 rect.width），
                // 再乘 |localScale|——scale 改了也能自动贴边（2026-10-28 主人需求）。
                float ang = _foldRt.localEulerAngles.z * Mathf.Deg2Rad;
                float effW = (Mathf.Abs(Mathf.Cos(ang)) * _foldRt.rect.width
                            + Mathf.Abs(Mathf.Sin(ang)) * _foldRt.rect.height) * Mathf.Abs(_foldRt.localScale.x);
                _foldEdgeX = FoldEdgeLeftInset + effW * 0.5f;
            }
            return t;
        }
        return null;
    }

    /// <summary>折叠/展开切换：只翻状态，实际显隐与高度统一走 ApplyLayout（下一拍刷新或立即套一次）。</summary>
    void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        ApplyLayout(_lastSideVisible);
        Debug.Log($"[QuestBarBinder] 任务栏{(_collapsed ? "折叠" : "展开")}");
    }

    void Update()
    {
        if (!GameSceneGate.IsTown) return;
        if (!StoryProgress.TutorialDone) return;
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = RefreshInterval;
        Refresh();
    }

    // ============================================================
    // 刷新
    // ============================================================

    void Refresh()
    {
        if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null) return;
        var snap = QuestBarData.Build();

        if (_mainText != null)
        {
            var m = snap.main;
            _mainText.text = m != null
                ? m.Desc + (string.IsNullOrEmpty(m.ProgressText) ? "" : "（" + m.ProgressText + "）")
                : "本章主线已完成";
            FitInfoFont(_mainText);   // 按最终文案长度定字号/截断
        }

        bool sideVisible = snap.side != null;
        _lastSideVisible = sideVisible;
        if (_rowSide != null)
        {
            if (_sideText != null)
            {
                _sideText.text = sideVisible ? snap.side.Value.desc : "";
                if (sideVisible) FitInfoFont(_sideText);
            }
        }
        else if (sideVisible)
        {
            WarnOnce(ref _warnedNoSideRow,
                $"[QuestBarBinder] 有待打支线（岛{snap.side.Value.islandId}「{snap.side.Value.name}」）但任务栏没有「支线」行，展示不了");
        }
        ApplyLayout(sideVisible);   // 状态没变时内部直接返回，不会每 0.5s 动 UI

        if (_dailyText != null)
        {
            var d = snap.daily;   // DailyQuestView? 可空结构体：判空用 HasValue，取值必须 .Value（CS1061 教训）
            _dailyText.text = d.HasValue
                ? d.Value.desc + (d.Value.need > 1 ? $"（{d.Value.progress}/{d.Value.need}）" : "")
                : "今日已完成";
            FitInfoFont(_dailyText);   // 按最终文案长度定字号/截断
        }
    }

    /// <summary>
    /// 把「折叠态 × 支线可见性」统一套到 UI 上（状态没变直接返回，绝不一拍一动）。
    ///   · 折叠 = 三行全收，黑底（panl/Dim）缩到只剩折叠按钮那截；展开 = 还原预制体基准；
    ///   · 支线隐藏（未派定/已通）= 藏支线行（行序 SetSiblingIndex 对无布局组的 panl 不挪位，
    ///     「日常上移 + 黑底缩一截」目前实际未生效——要做真位移等主人拍板）。
    /// </summary>
    void ApplyLayout(bool sideVisible)
    {
        if (_appliedCollapsed == _collapsed && _appliedSideVisible == sideVisible) return;
        _appliedCollapsed = _collapsed;
        _appliedSideVisible = sideVisible;

        _rowMain.gameObject.SetActive(!_collapsed);
        if (_rowDaily != null) _rowDaily.gameObject.SetActive(!_collapsed);
        if (_rowSide != null) _rowSide.gameObject.SetActive(!_collapsed && sideVisible);

        // 行序只在展开态摆：支线隐藏时日常上移顶位；支线回来还原原始次序（折叠态全藏了，不用管）
        if (_rowDaily != null && _rowSide != null && !_collapsed)
        {
            if (!sideVisible)
            {
                if (_dailyBaseSibling < 0) _dailyBaseSibling = _rowDaily.GetSiblingIndex();
                if (_rowDaily.GetSiblingIndex() > _rowSide.GetSiblingIndex())
                    _rowDaily.SetSiblingIndex(_rowSide.GetSiblingIndex());
            }
            else if (_dailyBaseSibling >= 0)
            {
                _rowDaily.SetSiblingIndex(_dailyBaseSibling);
            }
        }

        // 黑底（panl/Dim）纵向收缩【2026-10-27 修复「点折叠黑底没收」】：
        // 原实现改的是「任务栏」节点的 sizeDelta——该节点 0×0、没有任何子节点拉伸跟随它，
        // 写了视觉永远不动。黑底真身是 panl/Dim：折叠 = 缩到只剩折叠按钮那一截
        // （按钮/箭头必须留着，不然没法再展开）；展开 = 还原预制体基准。
        // 黑底只动纵向（sizeDelta.y / anchoredPosition.y），x 保持基准宽（铁律 20 纵向必要介入）；
        // 折叠按钮的横向（anchoredPosition.x）由 2026-10-28 主人需求单独处理（贴屏幕左边，见下方）。
        // 全部从 Bind 时量得的 base 重算，不累加。
        if (_dimRt == null || !_dimBaseCaptured) return;
        if (_collapsed)
        {
            float fh = _foldH > 0f ? _foldH : 0f;
            _dimRt.sizeDelta = new Vector2(_dimBaseSize.x, fh + DimEdgePadding * 2f);
            _dimRt.anchoredPosition = new Vector2(_dimBasePos.x, _foldY);
            // 【2026-10-28 主人需求】收起后折叠按钮移到屏幕左边（贴边），不要停在条中间原位：
            // 横向挪到 _foldEdgeX（按钮左缘留 FoldEdgeLeftInset），纵向保持 _foldY；展开态再还原到 _foldX。
            // 注：此举需改按钮 anchoredPosition.x，属铁律20「布局尺寸代码禁写」的显式必要介入例外（主人拍板）。
            if (_foldRt != null && _foldX >= 0f)
                _foldRt.anchoredPosition = new Vector2(_foldEdgeX, _foldY);
        }
        else
        {
            _dimRt.sizeDelta = _dimBaseSize;
            _dimRt.anchoredPosition = _dimBasePos;
            if (_foldRt != null && _foldX >= 0f)
                _foldRt.anchoredPosition = new Vector2(_foldX, _foldY);
        }
    }

    // ============================================================
    // 左侧任务逐行弹入（2026-10-28 主人需求）
    // ============================================================

    void Start()
    {
        if (_rowMain == null) return;
        StartCoroutine(CoPlayRowsPopInWhenVisible());
    }

    void OnDisable()
    {
        // 场景切换/撤离时把行归位，别把半程位移留在预制体实例上
        RestoreRowPopAnims();
    }

    /// <summary>
    /// 等主界面真的露出来再逐行弹（顺序即「看得见」的三道闸）：
    ///   ① 片头遮罩揭开（GuildHallUI.IsChromeVisible）——遮罩期间弹了玩家看不见；
    ///   ② Loading 全屏收掉（BattleLoadingOverlay.IsShowing）——盖着时弹等于没弹；
    ///   ③ 主界面入场动效播完（TownEntranceMotion.IsRunning）——整列从左滑入与逐行弹入同时跑会打架。
    /// 任一闸最多等 RowWaitEntranceMax 秒，超时照弹（不卡流程）。
    /// </summary>
    System.Collections.IEnumerator CoPlayRowsPopInWhenVisible()
    {
        float waited = 0f;
        while (!GuildHallUI.IsChromeVisible && waited < RowWaitVisibleMax)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        waited = 0f;
        while (BattleLoadingOverlay.IsShowing && waited < RowWaitEntranceMax)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        waited = 0f;
        while (TownEntranceMotion.IsRunning && waited < RowWaitEntranceMax)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_rowMain == null) yield break;
        // 弹之前先刷一次：正文/支线显隐按当前存档算准，别让支线行先弹后消失
        // （引导未完成时与 Update 同一口径不刷——那时任务数据还没起）
        if (StoryProgress.TutorialDone) Refresh();
        PlayRowsPopIn();
    }

    /// <summary>三行任务逐个弹入（主线 → 支线 → 日常；支线行隐藏时跳过，不占错峰位）。</summary>
    void PlayRowsPopIn()
    {
        if (!gameObject.activeInHierarchy) return;
        RestoreRowPopAnims();
        int slot = 0;
        var rows = new[] { _rowMain, _rowSide, _rowDaily };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;
            if (!row.gameObject.activeSelf) continue;
            var rt = row as RectTransform;
            if (rt == null) continue;
            _rowPopCos.Add(StartCoroutine(CoRowPopIn(rt, slot * RowPopStagger)));
            slot++;
        }
        Debug.Log($"[QuestBarBinder] 任务栏逐行弹入：{slot} 行");
    }

    /// <summary>
    /// 单行滑入：起始摆在屏幕左外侧（透明），滑到基准位（ease-out-cubic），前 40% 进度淡入。
    /// 与冒险页左侧标签同一套手感（AdventureUI.Anim.CoPopOnce）。
    /// </summary>
    System.Collections.IEnumerator CoRowPopIn(RectTransform rt, float delay)
    {
        Vector2 basePos = GetRowPopBasePos(rt);
        CanvasGroup cg = GetRowPopGroup(rt);
        Vector2 fromPos = new Vector2(basePos.x - RowSlideOut, basePos.y);
        rt.anchoredPosition = fromPos;
        cg.alpha = 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float time = 0f;
        while (time < RowPopDur)
        {
            time += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(time / RowPopDur);
            float ease = 1f - (1f - u) * (1f - u) * (1f - u);   // ease-out-cubic
            rt.anchoredPosition = new Vector2(Mathf.Lerp(fromPos.x, basePos.x, ease), basePos.y);
            cg.alpha = Mathf.Clamp01(u / 0.4f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
        cg.alpha = 1f;
    }

    /// <summary>行滑入的基准位置（每次播现抓，防缓存钉死旧值）。</summary>
    Vector2 GetRowPopBasePos(Transform t)
    {
        if (!_rowPopBasePos.TryGetValue(t, out var basePos))
        {
            basePos = ((RectTransform)t).anchoredPosition;
            _rowPopBasePos[t] = basePos;
        }
        return basePos;
    }

    CanvasGroup GetRowPopGroup(Transform t)
    {
        if (!_rowPopGroups.TryGetValue(t, out var cg) || cg == null)
        {
            cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = true;      // 只借 alpha，不动交互（行根有 Button，点击照常）
            cg.blocksRaycasts = true;
            _rowPopGroups[t] = cg;
        }
        return cg;
    }

    /// <summary>停协程并把行归位（动画开头与 OnDisable 都走这里）。</summary>
    void RestoreRowPopAnims()
    {
        for (int i = 0; i < _rowPopCos.Count; i++)
        {
            if (_rowPopCos[i] != null) StopCoroutine(_rowPopCos[i]);
        }
        _rowPopCos.Clear();
        foreach (var kv in _rowPopBasePos)
        {
            if (kv.Key == null) continue;
            if (kv.Key is RectTransform rt) rt.anchoredPosition = kv.Value;
        }
        foreach (var kv in _rowPopGroups)
        {
            if (kv.Value == null) continue;
            kv.Value.alpha = 1f;
        }
    }

    // ============================================================
    // 点击路由（跳转失败回退展开任务面板，绝不让点击没反应）
    // ============================================================

    void OnClickMain()
    {
        var m = QuestBarData.Build().main;
        if (m == null) { QuestPanelUI.Show(true); return; }
        if (!QuestJumpRouter.GoMain(m.def))
            QuestPanelUI.Show(true);
    }

    void OnClickSide()
    {
        // 支线岛槽在冒险页主线窗口旁：切过去定位当前章即可
        if (!QuestJumpRouter.GoChapter(0))
            QuestPanelUI.Show(false);
    }

    void OnClickDaily()
    {
        var d = QuestBarData.Build().daily;
        if (d == null) { QuestPanelUI.Show(false); return; }
        if (!QuestJumpRouter.GoDaily(d.Value))
            QuestPanelUI.Show(false);
    }

    // ============================================================
    // 绑定工具
    // ============================================================

    /// <summary>
    /// 行内正文 Text：【2026-10-26 主人发的层级图定稿】每行结构 = Icon / Name（行名标签）/ Info（正文）/ 前往。
    /// 优先取名为「Info」的子节点；没有再退回老判法（文本 ≠ 行名的第一个 Text，兜美术改版）。
    /// </summary>
    static Text ContentText(Transform row, string rowName)
    {
        var info = row.Find("Info");
        if (info != null)
        {
            var t = info.GetComponent<Text>();
            if (t != null) return t;
        }
        var texts = row.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (texts[i].text == rowName) continue;   // 行名标签（Name），不动
            return texts[i];
        }
        DumpChildren(row, 0);
        Debug.LogError($"[QuestBarBinder] 「{rowName}」行下没找到正文 Text（应叫 Info，子节点见上）");
        return null;
    }

    /// <summary>
    /// 行点击绑定【2026-10-27 主人拍板「整个任务横条都可点击前往」】：
    ///   · 行内「前往」按钮照旧绑（点按钮本尊走它，它是最高层命中、只触发一次）；
    ///   · 行根补挂 Button：点在行内任何子件（正文/图标/底图）上的点击会沿层级冒泡到行根，
    ///     同样触发前往 —— 整条横条任意位置可点。Button 不需要 Graphic 也能收到冒泡上来的点击。
    /// 「前往」缺失时行为不变（全靠行根 Button 接住），「绝不让点击没反应」。
    /// </summary>
    static void BindClick(Transform row, System.Action onClick)
    {
        var go = row.Find("前往");
        if (go != null)
        {
            var goBtn = go.GetComponent<Button>();
            if (goBtn == null && go.GetComponent<Graphic>() != null)
            {
                // 【2026-10-27】预制体三行都摆了「前往」节点（有 Image 但漏挂 Button），
                // 按铁律 2「预制体空→代码兜底」给「前往」补挂 Button；美术之后在 prefab 补了
                // Button 的话这里 GetComponent 直接命中，不冲突。
                goBtn = go.gameObject.AddComponent<Button>();
            }
            if (goBtn != null)
            {
                goBtn.onClick.RemoveListener(onClick.Invoke);
                goBtn.onClick.AddListener(onClick.Invoke);
            }
        }

        // 行根 Button：整条横条的点击落点（点在子件上的点击沿层级冒泡上来触发）
        var rowBtn = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
        rowBtn.onClick.RemoveListener(onClick.Invoke);
        rowBtn.onClick.AddListener(onClick.Invoke);

        // 行根没图就用行内最大背景图兜命中载体：底图若关了 raycast，空白区点击会穿过去没反应
        var host = row.GetComponent<Graphic>() ?? FindBestClickGraphic(row);
        if (host != null)
            host.raycastTarget = true;
        else
            Debug.LogWarning($"[QuestBarBinder] 「{row.name}」行内没有任何 Graphic，行根 Button 只能靠子件冒泡接点击");
    }

    /// <summary>行内兜底点击载体：取面积最大的非文本 Graphic（背景图优先），没有再退任意 Graphic。</summary>
    static Graphic FindBestClickGraphic(Transform row)
    {
        var candidates = row.GetComponentsInChildren<Graphic>(true);
        Graphic best = null;
        float bestArea = -1f;
        for (int i = 0; i < candidates.Length; i++)
        {
            var g = candidates[i];
            if (g is Text) continue;   // 优先背景 Image，Text 只作兜底
            var rt = g.rectTransform;
            float area = rt != null ? rt.rect.width * rt.rect.height : 0f;
            if (area > bestArea) { bestArea = area; best = g; }
        }
        if (best != null) return best;
        bestArea = -1f;
        for (int i = 0; i < candidates.Length; i++)
        {
            var g = candidates[i];
            var rt = g.rectTransform;
            float area = rt != null ? rt.rect.width * rt.rect.height : 0f;
            if (area > bestArea) { bestArea = area; best = g; }
        }
        return best;
    }

    // ============================================================
    // 悬空美术补链（2026-10-27 主人拍板「修」）
    // ============================================================
    // 团结引擎 56 字符 guid 重签后，任务栏 prefab 里 8 张 sprite 的 hex32 引用全部悬空
    // （prefab 引用的 guid 在库里已不存在；图本体一直在 Assets/Resources/UI/Art/ 下没丢过）。
    // 按 AGENTS.md §2.3 口径（2026-10-23 主人拍板「56 字符 guid 一律不管，全部走路径加载，
    // 代码不得依赖 prefab 里的 sprite guid 引用」），这里按节点 + 路径主动设图——
    // 与 LoadChestSprite / 名牌 Icon 同款口径，不是「图丢了才补」的运行时兜底。
    // 加载失败 LogError 不静默（铁律 10）；只换 sprite，颜色/尺寸/raycast 一律不碰（铁律 20）。

    const string ArtTaskDir = "UI/Art/Main/任务栏/";
    const string ArtFoldPath = "UI/Battle/折叠箭头";     // 88×49 与折叠节点同尺寸
    const string ArtDimPath = "UI/Art/Battle/任务底框";  // 234×99 半透明深色面板，Dim 原图

    /// <summary>悬空节点 → 补图路径对照（guid 已废，路径是唯一真源）。</summary>
    static readonly Dictionary<string, Sprite> _artCache = new Dictionary<string, Sprite>();

    static Sprite LoadArt(string path, string label)
    {
        if (_artCache.TryGetValue(path, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(path);
        if (s == null)
            Debug.LogError($"[QuestBarBinder] 路径加载失败：Resources/{path}（{label}）——图不在 Resources 下或未按 Sprite 导入，任务栏该图会空");
        else
            _artCache[path] = s;
        return s;
    }

    static void SetArt(Transform node, string path, string label)
    {
        if (node == null) return;   // 节点缺失 Bind 里已各自 LogError，这里不重复报
        var img = node.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError($"[QuestBarBinder] 节点「{node.name}」上没有 Image（{label}），路径图挂不上");
            return;
        }
        var s = LoadArt(path, label);
        if (s != null) img.sprite = s;
    }

    static Transform ChildOrWarn(Transform row, string child, string label)
    {
        var t = row.Find(child);
        if (t == null)
            Debug.LogError($"[QuestBarBinder] 「{row.name}」行下没找到「{child}」子节点（{label}），该图挂不上");
        return t;
    }

    /// <summary>
    /// 把 8 张悬空图按路径接回（节点/映射均为 2026-10-27 对 prefab 与素材逐张核对的结果）：
    ///   三行行底（美术放在各行 Icon 节点上）：主线←素材表1（金）、支线←素材表2（蓝）、日常←素材表3（金，与1同图）；
    ///   三行「前往」箭头：主线前往 / 支线前往 / 日常前往（120×171 三色）；
    ///   折叠按钮箭头：UI/Battle/折叠箭头；黑底：panl/Dim ← UI/Art/Battle/任务底框。
    /// </summary>
    static void EnsureBarArt(Transform rowMain, Transform rowSide, Transform rowDaily, Transform fold, Transform dim)
    {
        SetArt(ChildOrWarn(rowMain, "Icon", "主线行底"), ArtTaskDir + "奇幻游戏三色任务界面素材表1", "主线行底");
        if (rowSide != null) SetArt(ChildOrWarn(rowSide, "Icon", "支线行底"), ArtTaskDir + "奇幻游戏三色任务界面素材表2", "支线行底");
        SetArt(ChildOrWarn(rowDaily, "Icon", "日常行底"), ArtTaskDir + "奇幻游戏三色任务界面素材表3", "日常行底");

        SetArt(ChildOrWarn(rowMain, "前往", "主线前往"), ArtTaskDir + "主线前往", "主线前往");
        if (rowSide != null) SetArt(ChildOrWarn(rowSide, "前往", "支线前往"), ArtTaskDir + "支线前往", "支线前往");
        SetArt(ChildOrWarn(rowDaily, "前往", "日常前往"), ArtTaskDir + "日常前往", "日常前往");

        SetArt(fold, ArtFoldPath, "折叠箭头");
        SetArt(dim, ArtDimPath, "黑底 Dim");
    }

    // ============================================================
    // 自诊断（找不到东西时 dump 层级，一次看清缺什么）
    // ============================================================

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void DumpChildren(Transform t, int depth)
    {
        if (depth > 4) return;
        var indent = new string(' ', depth * 2);
        Debug.Log($"[QuestBarBinder] {indent}└ {t.name}（{t.childCount} 子）");
        for (int i = 0; i < t.childCount; i++)
            DumpChildren(t.GetChild(i), depth + 1);
    }

    static void WarnOnce(ref bool flag, string msg)
    {
        if (flag) return;
        flag = true;
        Debug.LogWarning(msg);
    }
}
