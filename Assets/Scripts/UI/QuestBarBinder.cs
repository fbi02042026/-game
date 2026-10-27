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

    Transform _bar;
    RectTransform _barRt;
    Transform _rowMain, _rowSide, _rowDaily;
    Text _mainText, _sideText, _dailyText;
    float _barBaseHeight = -1f;      // 全行展开时的栏高（首次收起前量得）
    float _rowMainH, _rowSideH, _rowDailyH;   // 绑定时量好的行高（折叠/藏行的黑底缩减都用它）
    int _dailyBaseSibling = -1;      // 日常行原始次序（支线行回来时还原）
    bool _collapsed;                 // 折叠态（会话内，不写存档）
    bool _appliedCollapsed, _appliedSideVisible = true;   // 上次实际套到 UI 的状态（变了才动 UI）
    bool _lastSideVisible = true;    // 最近一次刷新算出的支线可见性（折叠按钮切换时用）
    bool _warnedNoSideRow;
    float _timer;

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
        _bar = bar;
        _barRt = bar as RectTransform;

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

        BindClick(_rowMain, OnClickMain);
        if (_rowSide != null) BindClick(_rowSide, OnClickSide);
        BindClick(_rowDaily, OnClickDaily);

        // 行高缓存（黑底缩减用；绑定时行多半是激活态，rect.height 对未激活节点也有效）
        _rowMainH = RowHeight(_rowMain);
        if (_rowSide != null) _rowSideH = RowHeight(_rowSide);
        if (_rowDaily != null) _rowDailyH = RowHeight(_rowDaily);

        // 折叠按钮：候选名挨个找，找到就接切换；找不到报错 dump（主人说栏上有这个按钮，对不上就是名字不匹配）
        var fold = FindFoldButton(bar);
        if (fold == null)
        {
            DumpChildren(bar, 0);
            Debug.LogError($"[QuestBarBinder] 任务栏下没找到折叠按钮（候选名：{string.Join("/", FoldNodeNames)}，子节点见上），折叠功能不生效");
        }

        // 显隐联动基线：以预制体当前实际状态为准（支线行若预制体里就是隐藏的，首刷数据来了会正确展开）
        _appliedSideVisible = _rowSide == null || _rowSide.gameObject.activeSelf;
        _lastSideVisible = _appliedSideVisible;

        _timer = RefreshInterval;   // 首刷延半拍：等存档与任务系统就绪
        Debug.Log($"[QuestBarBinder] 任务栏绑定完成（正文 Text：主线={(_mainText != null)} 支线={(_sideText != null)} 日常={(_dailyText != null)}；折叠按钮={fold != null}）");
    }

    static float RowHeight(Transform row)
    {
        var rt = row as RectTransform;
        return rt != null ? rt.rect.height : 0f;
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
        }

        bool sideVisible = snap.side != null;
        _lastSideVisible = sideVisible;
        if (_rowSide != null)
        {
            if (_sideText != null)
                _sideText.text = sideVisible ? snap.side.Value.desc : "";
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
        }
    }

    /// <summary>
    /// 把「折叠态 × 支线可见性」统一套到 UI 上（状态没变直接返回，绝不一拍一动）。
    ///   · 折叠 = 三行全收，黑底缩掉三行总高（只剩折叠按钮那截）；展开 = 按支线可见性正常摆；
    ///   · 支线隐藏（未派定/已通）= 藏支线行 + 日常上移 + 黑底缩掉支线行那截；
    ///   · 有 LayoutGroup/ContentSizeFitter 代管高度 → 只动显隐，高度交给自动布局。
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

        // 黑底高度（无自动布局才手动算；基高只量一次 = 全展开态）
        if (_barRt == null || HasAutoLayout()) return;
        if (_barBaseHeight < 0f) _barBaseHeight = _barRt.rect.height;
        float h = _barBaseHeight;
        if (_collapsed) h -= _rowMainH + _rowSideH + _rowDailyH;
        else if (!sideVisible) h -= _rowSideH;
        _barRt.sizeDelta = new Vector2(_barRt.sizeDelta.x, Mathf.Max(0f, h));
    }

    bool HasAutoLayout()
    {
        var t = _bar;
        while (t != null)
        {
            if (t.GetComponent<VerticalLayoutGroup>() != null ||
                t.GetComponent<HorizontalLayoutGroup>() != null ||
                t.GetComponent<GridLayoutGroup>() != null ||
                t.GetComponent<ContentSizeFitter>() != null)
                return true;
            t = t.parent;
        }
        return false;
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
    /// 行点击绑定：优先绑行内「前往」按钮（层级图定稿，玩家点的是它）；行根/行内其它 Button 次之；
    /// 都没有再考虑给行根补 Button（需要行根有 Graphic）。保证「绝不让点击没反应」。
    /// </summary>
    static void BindClick(Transform row, System.Action onClick)
    {
        var go = row.Find("前往");
        if (go != null)
        {
            var goBtn = go.GetComponent<Button>();
            if (goBtn != null)
            {
                goBtn.onClick.RemoveListener(onClick.Invoke);
                goBtn.onClick.AddListener(onClick.Invoke);
                return;
            }
        }
        var btn = row.GetComponent<Button>() ?? row.GetComponentInParent<Button>() ?? row.GetComponentInChildren<Button>(true);
        if (btn == null)
        {
            // 行内没有 Button：优先用行根自身 Graphic；没有再找行内最大的背景图（美术没放「前往」按钮时兜底，
            // 保证「绝不让点击没反应」）。两者都没有才是真缺件 → LogError 让美术补。
            var host = row.GetComponent<Graphic>() ?? FindBestClickGraphic(row);
            if (host == null)
            {
                DumpChildren(row, 0);
                Debug.LogError($"[QuestBarBinder] 「{row.name}」行没有「前往」按钮、没有 Button 也没有可点击 Graphic，点击不了（子节点见上）");
                return;
            }
            host.raycastTarget = true;   // 兜底点击载体必须可点
            btn = host.gameObject.AddComponent<Button>();
            Debug.LogWarning($"[QuestBarBinder] 「{row.name}」行没有「前往」按钮，已用行内背景图兜底点击（美术可在该行补「前往」按钮，点击更精准）");
        }
        btn.onClick.RemoveListener(onClick.Invoke);
        btn.onClick.AddListener(onClick.Invoke);
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
