using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗内关卡石墩图。
///
/// 流程：进图 → 下一关石墩上的锁晃两下再放大消失 → 滚盘选类型
/// → 类型旗落到当前石墩并挂 btn06 描边 → 玩家点石墩进入。
///
/// 石墩三态：
/// · 当前可打：Banner + Icon + btn06 描边，可点
/// · 已打过：ClearedMark（变灰旗子），不可点
/// · 未解锁：只有石墩 + Lock，无旗子
///
/// 从下往上打：Pedestal_0 在最下面，默认无锁。
/// </summary>
public class BattleStageMapUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/BattleStageMap";
    public const int PedestalCount = 10;

    public static BattleStageMapUI Instance { get; private set; }

    [Header("可绑节点")]
    public GameObject root;
    public Image backdrop;
    public Text titleText;
    public Transform pedestalRoot;
    public List<PedestalRefs> pedestals = new List<PedestalRefs>();
    [Tooltip("当前关描边：拖 btn06。空则试 Resources/Materials/btn06")]
    public Material currentOutlineMaterial;

    [Serializable]
    public class PedestalRefs
    {
        public GameObject root;
        public Button button;
        public Image stone;
        public Image banner;
        public Image icon;
        /// <summary>已通关的灰旗（ClearedMark 节点本身就是灰旗图）</summary>
        public GameObject clearedMark;
        /// <summary>未解锁锁头；解锁动画播在这个节点上</summary>
        public GameObject lockIcon;
        public Image outlineTarget; // 挂 btn06 的 Image，默认用 Banner 或 Stone
    }

    StageData _next;
    Action<StageData> _onPick;
    readonly List<StageData> _candidates = new List<StageData>();
    readonly HashSet<int> _candidateIndices = new HashSet<int>();
    bool _branchSelectMode;
    float _prevTimeScale = 1f;
    Coroutine _flow;
    Coroutine _backdropPulse;
    Color _backdropBase = Color.white;
    Material _btn06;
    readonly Dictionary<Image, Material> _savedMats = new Dictionary<Image, Material>();
    /// <summary>右上角暂停按钮（运行时补建，不落预制体）。</summary>
    GameObject _pauseBtn;

    /// <summary>进关卡图并跑完整流程（解锁 → 滚盘 → 落旗 → 等点击）。</summary>
    public static void BeginFlow(StageData next, Action<StageData> onPick)
    {
        if (next == null)
        {
            onPick?.Invoke(null);
            return;
        }
        var list = new List<StageData> { next };
        BeginFlow(list, onPick);
    }

    /// <summary>多候选路线：分支时让玩家选石墩，再滚盘。</summary>
    public static void BeginFlow(List<StageData> candidates, Action<StageData> onPick)
    {
        if (candidates == null || candidates.Count == 0)
        {
            onPick?.Invoke(null);
            return;
        }
        if (candidates.Count == 1)
            Ensure().StartFlow(candidates[0], onPick);
        else
            Ensure().StartBranchFlow(candidates, onPick);
    }

    public static BattleStageMapUI Ensure()
    {
        // 【2026-10-11 主人拍板】关卡选择（关卡图 / 老虎机）放专用曲「战后小歇」。
        // 放在这里 = 每次进关卡图都切一次，静音期间 GameBgm.Play 自己会拦（不抢写 pending），不用再加开关。
        GameBgm.Play(GameBgm.Track.StageSelect);
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "BattleStageMap";
        }
        else
        {
            Debug.LogWarning($"[BattleStageMap] 未找到预制体 {PrefabPath}，代码搭建临时界面");
            go = new GameObject("BattleStageMap", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<BattleStageMapUI>();
        if (ui == null) ui = go.AddComponent<BattleStageMapUI>();
        return ui;
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (root == null || !root.activeSelf) return;

        // 【2026-10-17 主人要求加暂停】图开着期间战斗必须保持冻结：
        // 设置弹窗 Close() 会把 timeScale 恢复成 1（它记的 _prevTimeScale 是开图时的 0），
        // 这里按帧压回 0；图一收（CoPedestalAccept / Hide）本守卫即失效，不影响正常恢复。
        if (Time.timeScale != 0f)
            Time.timeScale = 0f;

        // 暂停按钮只在「等玩家点石墩」的静置期出现：滚盘 / 解锁 / 确认动画期间藏起，
        // 也顺带杜绝「老虎机没转完就开弹窗撤离」这种半路状态组合。
        if (_pauseBtn != null)
            _pauseBtn.SetActive(_flow == null);
    }

    /// <summary>
    /// 【2026-10-17 主人要求】右上角暂停按钮：运行时补建（不动预制体，铁律 3），
    /// 复用战斗右上角设置同款图（Resources/Art/UI/battle/设置.png）。
    /// 点击打开 SettingsPopupUI（Battle 宿主）＝战斗内暂停同款弹窗：继续 / 音乐音效 / 撤离。
    /// </summary>
    void EnsurePauseButton()
    {
        if (_pauseBtn != null) return;

        var host = root != null ? root.transform : transform;
        var go = new GameObject("PauseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -40f);
        rt.sizeDelta = new Vector2(110f, 110f);

        var img = go.GetComponent<Image>();
        var sp = Resources.Load<Sprite>("Art/UI/battle/设置");
        if (sp != null)
        {
            img.sprite = sp;
            img.preserveAspect = true;
        }
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnOpenPause);
        _pauseBtn = go;

        // 刘海 / 挖孔避让：只挪 anchoredPosition，锚点语义不动
        SafeAreaFitter.ApplyTopOffset(transform, "PauseButton");
    }

    void OnOpenPause()
    {
        SettingsPopupUI.Ensure().Open(SettingsHost.Battle);
    }

    void StartFlow(StageData next, Action<StageData> onPick)
    {
        BindRefs();
        _branchSelectMode = false;
        _candidates.Clear();
        _candidateIndices.Clear();
        _candidateIndices.Add(next.stageIndex);
        _next = next;
        _onPick = onPick;

        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        if (titleText != null)
            titleText.text = GameConfig.GetChapterMapName(chapter);

        RefreshPedestals(Phase.BeforeRoll);

        if (root != null) root.SetActive(true);
        EnsurePauseButton();
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);

        // 【2026-10-12 主人拍板】关卡图期间隐藏整个战斗 HUD —— 否则技槽（自带 Canvas 921）
        // 比本图(880)高，会穿在图上面（主人报「技能跑到老虎机界面了」）。唯一出口见 BattleUI.SetHiddenForStageMap。
        BattleUI.Instance?.SetHiddenForStageMap(true);

        StartBackdropPulse();
        if (_flow != null) StopCoroutine(_flow);
        _flow = StartCoroutine(CoFlow());
    }

    void StartBranchFlow(List<StageData> candidates, Action<StageData> onPick)
    {
        BindRefs();
        _branchSelectMode = true;
        _candidates.Clear();
        _candidateIndices.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;
            _candidates.Add(candidates[i]);
            _candidateIndices.Add(candidates[i].stageIndex);
        }
        _next = null;
        _onPick = onPick;

        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        if (titleText != null)
            titleText.text = GameConfig.GetChapterMapName(chapter);

        RefreshPedestals(Phase.BranchSelect);

        if (root != null) root.SetActive(true);
        EnsurePauseButton();
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);

        // 【2026-10-12 主人拍板】同上：分支选路也是关卡图，期间同样关掉战斗 HUD（防技槽穿层）。
        BattleUI.Instance?.SetHiddenForStageMap(true);

        StartBackdropPulse();
        if (_flow != null) StopCoroutine(_flow);
        _flow = StartCoroutine(CoBranchSelectFlow());

        GlobalToastUI.Show("选择下一关路线");
    }

    enum Phase
    {
        BeforeRoll,
        AfterRoll,
        BranchSelect
    }

    IEnumerator CoBranchSelectFlow()
    {
        foreach (int idx in _candidateIndices)
        {
            if (idx > 0)
                yield return PlayUnlockAnim(idx);
        }
        yield return WaitUnscaled(0.2f);
        RefreshPedestals(Phase.BranchSelect);
        _flow = null;
    }

    IEnumerator CoFlow()
    {
        int nextIdx = _next != null ? _next.stageIndex : 0;

        if (nextIdx > 0)
            yield return PlayUnlockAnim(nextIdx);
        else
            yield return WaitUnscaled(0.25f);

        yield return CoRouletteAndFinalize(nextIdx);
    }

    IEnumerator CoRouletteAndFinalize(int nextIdx)
    {
        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        bool rolled = false;
        StageData picked = _next;
        NextStageRouletteUI.Show(chapter, _next, p =>
        {
            if (p != null) picked = p;
            rolled = true;
        });
        // 【2026-10-11 主人报「老虎机轮到恢复关卡后弹不出界面 卡到这了」】
        // 这个等待原来<b>没有出口</b>：老虎机只要没回调（面板没起来 / 「进入关卡」按钮没绑上 / 结果没出来），
        // rolled 永远 false → _flow 一直不为 null → OnPedestalClicked 第一行就 return → 石墩全点不动 → 卡死。
        // 现在加超时保险：超时打 Error（把真因留给日志），同时放开流程让玩家还能点石墩继续。
        // 这是 fail closed，不是兜底：不假装转成功了，只是不再把玩家锁死在这一屏。
        const float RollTimeout = 20f;
        float waited = 0f;
        while (!rolled && waited < RollTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!rolled)
            Debug.LogError($"[BattleStageMap] 老虎机 {RollTimeout}s 内没有回调"
                + $"（本关类型={(_next != null ? _next.type.ToString() : "?")}）→ 已放开流程避免卡死。"
                + "查 NextStageRouletteUI：结果「进入关卡」按钮是不是没弹出来 / enterButton 没绑上");

        _next = picked;
        RefreshPedestals(Phase.AfterRoll);
        ApplyCurrentOutline(nextIdx);
        _flow = null;
    }

    IEnumerator PlayUnlockAnim(int index)
    {
        if (index < 0 || index >= pedestals.Count) yield break;
        var p = pedestals[index];
        if (p == null || p.lockIcon == null)
        {
            yield return WaitUnscaled(0.2f);
            yield break;
        }

        var lockTf = p.lockIcon.transform;
        p.lockIcon.SetActive(true);
        Vector3 baseScale = Vector3.one;
        Vector3 basePos = lockTf.localPosition;
        lockTf.localScale = baseScale;

        // 晃两下
        for (int shake = 0; shake < 2; shake++)
        {
            float t = 0f;
            const float dur = 0.22f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float w = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 12f;
                lockTf.localPosition = basePos + new Vector3(w, 0f, 0f);
                lockTf.localRotation = Quaternion.Euler(0f, 0f, w * 0.8f);
                yield return null;
            }
            lockTf.localPosition = basePos;
            lockTf.localRotation = Quaternion.identity;
            yield return WaitUnscaled(0.06f);
        }

        // 放大消失
        float u = 0f;
        const float pop = 0.35f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / pop;
            float e = Mathf.Clamp01(u);
            lockTf.localScale = baseScale * (1f + e * 1.4f);
            SetLockAlpha(p.lockIcon, 1f - e);
            yield return null;
        }

        p.lockIcon.SetActive(false);
        lockTf.localScale = baseScale;
        lockTf.localPosition = basePos;
        SetLockAlpha(p.lockIcon, 1f);
    }

    static void SetLockAlpha(GameObject lockGo, float a)
    {
        if (lockGo == null) return;
        var imgs = lockGo.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var c = imgs[i].color;
            c.a = a;
            imgs[i].color = c;
        }
    }

    void RefreshPedestals(Phase phase)
    {
        var cm = ChapterManager.Instance;
        int currentDone = cm != null ? cm.currentStageIndex : -1;
        int nextIdx = _next != null ? _next.stageIndex : currentDone + 1;

        ClearAllOutlines();

        for (int i = 0; i < pedestals.Count; i++)
        {
            var p = pedestals[i];
            if (p == null || p.root == null) continue;

            bool cleared = i <= currentDone;
            bool isCandidate = _candidateIndices.Contains(i);

            var hi = p.root.transform.Find("Highlight");
            if (hi != null) hi.gameObject.SetActive(false);

            // 旗子只走 SetFlagNone / SetFlagTurned / SetFlagDefeated 三个出口，
            // 下面任何分支都不许再直接 SetActive(banner/clearedMark)（2026-10-11 主人拍板）。
            if (phase == Phase.BranchSelect)
            {
                if (cleared)
                {
                    if (p.lockIcon != null) p.lockIcon.SetActive(false);
                    SetFlagDefeated(p);                 // 已打败 → Banner 暗旗 #646464
                    SetFlagTypeLabel(p, TypeOfStage(i)); // 看得出这一关原来是什么类型
                    if (p.stone != null) p.stone.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                    SetClickable(p, false, i);
                }
                else if (isCandidate)
                {
                    if (p.lockIcon != null) p.lockIcon.SetActive(false);
                    SetFlagNone(p);                     // 候选关还没转老虎机 → 不显示旗子
                    if (p.stone != null) p.stone.color = Color.white;
                    SetClickable(p, true, i);
                }
                else
                {
                    SetFlagNone(p);
                    if (p.lockIcon != null) p.lockIcon.SetActive(true);
                    if (p.stone != null) p.stone.color = new Color(0.55f, 0.55f, 0.55f, 0.95f);
                    SetClickable(p, false, i);
                }
                continue;
            }

            bool isNext = _next != null ? i == _next.stageIndex : i == nextIdx;

            if (isNext)
            {
                if (phase == Phase.BeforeRoll)
                {
                    bool showLock = nextIdx > 0;
                    SetFlagNone(p);                     // 没转老虎机 → 不显示旗子
                    if (p.lockIcon != null) p.lockIcon.SetActive(showLock);
                    if (p.stone != null) p.stone.color = Color.white;
                    SetClickable(p, false, i);
                }
                else
                {
                    if (p.lockIcon != null) p.lockIcon.SetActive(false);
                    SetFlagTurned(p);                   // 老虎机转到 → ClearedMark 旗（保留美术原色）
                    // 【2026-10-12 主人拍板】这里**不再贴类型图标**（原 ApplyTypeIcon 调用已删）。
                    // 原因：类型图标素材 Art/UI/关卡/stage_*.png 本身画的就是**一整面旗子**，
                    // 贴到旗子中央 = 旗子里再套一面旗（主人原话「现在变成旗子里面有个旗子了」）。
                    // 主人要的是「跟最下面的那个一样是个骷髅」= 所有旗子保持预制体原样。
                    // ⚠ 要恢复「旗子上显示**类型图标**」，必须先有**纯标志图**（只有骷髅/图标、不含旗面），
                    //   由美术出图后再接回 ApplyTypeIcon —— 现有 stage_* 一律不能当标志用。
                    //   在那之前用文字标签顶着（SetFlagTypeLabel），保证精英/恢复/首领看得出来。
                    SetFlagTypeLabel(p, _next != null ? _next.type : TypeOfStage(i));
                    if (p.stone != null) p.stone.color = Color.white;
                    SetClickable(p, true, i);
                }
            }
            else if (cleared)
            {
                if (p.lockIcon != null) p.lockIcon.SetActive(false);
                SetFlagDefeated(p);                     // 已打败 → Banner 暗旗
                SetFlagTypeLabel(p, TypeOfStage(i));
                if (p.stone != null) p.stone.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                SetClickable(p, false, i);
            }
            else
            {
                SetFlagNone(p);                         // 未解锁 → 不显示旗子
                if (p.lockIcon != null) p.lockIcon.SetActive(true);
                if (p.stone != null) p.stone.color = new Color(0.55f, 0.55f, 0.55f, 0.95f);
                SetClickable(p, false, i);
            }
        }
    }

    // ===== 旗子三态唯一出口（2026-10-11 主人拍板）=====
    // 旧口径（已通关=ClearedMark 灰旗 / 转到的下一关=Banner 白旗）作废。
    // 主人原话：「没有老虎机时不显示旗子 转到后出现旗子用 ClearedMark 节点
    //            打败后用 Banner 节点然后颜色调暗」（参考色 #646464）。

    /// <summary>旗子上的「关卡类型」文字标签节点名（运行时补建，不落预制体）。</summary>
    const string TypeLabelName = "StageTypeLabel";

    /// <summary>
    /// 【2026-10-12 主人拍板】旗子上必须看得出这一关是**什么类型**（主人原话「老虎机也会出现的精英关卡」）。
    ///
    /// <para>上一轮为了修「旗子里有个旗子」把类型**图标**整段去掉了 ——
    /// <c>Art/UI/关卡/stage_*.png</c> 本身画的就是**一整面旗子**，贴到旗面中央等于叠了两面旗。
    /// 但代价是：老虎机转出精英 / 恢复 / 首领关之后，旗子长得跟普通关一模一样，
    /// 玩家（含主人）根本看不出转到了什么 —— 老虎机演出等于白演。</para>
    ///
    /// <para>这里改成**文字标签**：不依赖任何新美术，也不再往旗面上贴图。
    /// 【2026-10-17 主人要求】字不再压在旗面上，挪到**旗子正上方**（贴旗顶居中悬浮），
    /// 旗面恢复干净原样。等美术给出**纯标志图**（只有骷髅 / 图标、不含旗面）再接回 <c>ApplyTypeIcon</c>，本方法随之删除。</para>
    /// </summary>
    void SetFlagTypeLabel(PedestalRefs p, StageType type)
    {
        // 挂到当前真正显示的那面旗下（转到=ClearedMark，已打败=Banner）
        Transform host = null;
        if (p.clearedMark != null && p.clearedMark.activeSelf) host = p.clearedMark.transform;
        else if (p.banner != null && p.banner.gameObject.activeSelf) host = p.banner.transform;
        if (host == null) return;

        var exist = host.Find(TypeLabelName);
        var t = exist != null ? exist.GetComponent<Text>() : null;
        if (t == null)
        {
            var go = new GameObject(TypeLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            t = go.GetComponent<Text>();
            t.fontSize = 20;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;                       // 不吃射线，别抢旗子按钮的点击
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = GameFonts.GetChinese();
            if (f != null) t.font = f;
            var ol = go.AddComponent<Outline>();           // 描边：悬在空中也看得清
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(1f, -1f);
        }
        // 【2026-10-17 主人要求】每次都重摆矩形：关卡图 DontDestroyOnLoad，
        // 同一场对局里按旧锚点（压旗面）建出来的节点也能被校正到旗顶上方。
        ApplyTypeLabelRect(t.rectTransform);
        t.text = StageRoller.Label(type);
        // 与老虎机结果名同款配色（Color.Lerp 到白 0.55）：精英紫 / 首领红 / 恢复绿 / 普通蓝
        t.color = Color.Lerp(StageRoller.Tint(type), Color.white, 0.55f);
        t.gameObject.SetActive(true);
    }

    /// <summary>类型标签矩形：贴旗顶（anchor 0.5,1）居中悬浮在旗子正上方，宽度放宽 + 不换行。</summary>
    static void ApplyTypeLabelRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 2f);
        rt.sizeDelta = new Vector2(220f, 32f);
    }

    /// <summary>取第 index 关在关卡表里的类型（取不到就按普通关，不另造一套真源）。</summary>
    static StageType TypeOfStage(int index)
    {
        var cm = ChapterManager.Instance;
        if (cm == null || cm.stageMap == null || index < 0 || index >= cm.stageMap.Count)
            return StageType.Normal;
        var s = cm.stageMap[index];
        return s != null ? s.type : StageType.Normal;
    }

    /// <summary>没转老虎机（下一关未定）→ 什么旗都不显示。</summary>
    void SetFlagNone(PedestalRefs p)
    {
        if (p.banner != null) p.banner.gameObject.SetActive(false);
        if (p.clearedMark != null) p.clearedMark.SetActive(false);
    }

    /// <summary>老虎机转到后 → 显示 ClearedMark 节点（颜色用预制体里美术摆的，不改色）。</summary>
    void SetFlagTurned(PedestalRefs p)
    {
        SetFlagNone(p);
        if (p.clearedMark != null) p.clearedMark.SetActive(true);
    }

    /// <summary>打败后 → 显示 Banner 节点，颜色调暗到 #646464（100,100,100）。</summary>
    void SetFlagDefeated(PedestalRefs p)
    {
        SetFlagNone(p);
        if (p.banner != null)
        {
            p.banner.gameObject.SetActive(true);
            p.banner.color = new Color(100f / 255f, 100f / 255f, 100f / 255f, 1f);
        }
    }

    void SetClickable(PedestalRefs p, bool on, int index)
    {
        // 【2026-10-12 主人报「点旗子无反应 只能点石墩」】
        // 预制体里 Pedestal_i 根 / Banner / ClearedMark **各挂了一个 Button**，旧代码只给根那个绑监听，
        // 而根 Button 的命中区 = 石墩那块（targetGraphic 是 Stone）→ 点到旗子（在石墩上方）就没反应。
        // 现在把**旗子上的 Button 也一起绑上**，点石墩和点旗子等价。不动预制体（铁律 3）。
        FlagButtons(p, _reusedBtnList);
        if (_reusedBtnList.Count == 0) return;

        int capture = index;
        for (int i = 0; i < _reusedBtnList.Count; i++)
        {
            var b = _reusedBtnList[i];
            b.onClick.RemoveAllListeners();
            b.interactable = on;
            if (!on) continue;

            // 【2026-10-11 主人拍板】石墩**不要跟着放大/缩放**（主人原话「点击石墩的时候旗子放大就行
            // 石墩不要跟着放大」）→ 屏蔽通用按压反馈（UiButtonPressFeedback 会把按钮压到 0.94 再弹回）。
            var f = b.GetComponent<UiButtonPressFeedback>();
            if (f == null) f = b.gameObject.AddComponent<UiButtonPressFeedback>();
            f.suppress = true;

            b.onClick.AddListener(() => OnPedestalClicked(capture));
        }
    }

    /// <summary>复用列表，避免每次刷新都 new（SetClickable 每拍都跑）。</summary>
    readonly List<Button> _reusedBtnList = new List<Button>();

    /// <summary>收集这个石墩上**所有**能点的按钮：根 + 两面旗（转到的看 ClearedMark，打败后看 Banner）。</summary>
    static void FlagButtons(PedestalRefs p, List<Button> dst)
    {
        dst.Clear();
        if (p == null) return;
        if (p.button != null) dst.Add(p.button);
        if (p.banner != null)
        {
            var b = p.banner.GetComponent<Button>();
            if (b != null) dst.Add(b);
        }
        if (p.clearedMark != null)
        {
            var b = p.clearedMark.GetComponent<Button>();
            if (b != null) dst.Add(b);
        }
    }

    /// <summary>
    /// 关卡类型图标 —— 【2026-10-11 主人：「石墩只有一个图没有类型 只有旗子有类型」】
    /// 石墩（Stone）就一张底图、不带类型；类型只画在<b>旗子</b>上。
    /// 预制体里旗子（ClearedMark / Banner）没有 Icon 子节点 → 代码在旗下补一个（不动预制体，铁律 3）。
    /// 两面旗都挂：转到时看 ClearedMark，打败后看 Banner（暗旗），类型一路跟着走。
    ///
    /// <para>⚠【2026-10-12 已停用】唯一调用点（RefreshPedestals 转到分支）已删 ——
    /// 本方法贴的是 <c>Art/UI/关卡/stage_*.png</c>，而那批图**本身就是一整面旗子**，
    /// 贴到旗子中央就是「旗子里套一面旗」（主人报「现在变成旗子里面有个旗子了」）。
    /// 主人要「跟最下面的那个一样是个骷髅」= 旗子保持预制体原样。
    /// <b>要恢复旗子显示类型，必须先用纯标志图（不含旗面）替换 stage_*，再接回调用点</b>；
    /// 方法本体留着是为了保留这段结论，不是要用它。别直接把它接回去。</para>
    /// </summary>
    void ApplyTypeIcon(PedestalRefs p, StageType type)
    {
        Sprite sp = LoadStageIcon(type);
        if (sp == null)
        {
            Debug.LogError($"[BattleStageMap] 关卡类型图取不到：Art/UI/关卡/{StageRoller.IconName(type)} —— 旗子上就没有类型了");
            return;
        }
        ApplyTypeIconToFlag(p.clearedMark, sp);
        ApplyTypeIconToFlag(p.banner != null ? p.banner.gameObject : null, sp);
    }

    static void ApplyTypeIconToFlag(GameObject flagGo, Sprite sp)
    {
        if (flagGo == null) return;
        var exist = flagGo.transform.Find("TypeIcon");
        Image img = exist != null ? exist.GetComponent<Image>() : null;
        if (img == null)
        {
            var go = new GameObject("TypeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(flagGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(48f, 48f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
        }
        img.sprite = sp;
        img.color = Color.white;
        img.preserveAspect = true;
        img.gameObject.SetActive(true);
    }

    void ApplyCurrentOutline(int index)
    {
        if (index < 0 || index >= pedestals.Count) return;
        var p = pedestals[index];
        if (p == null) return;

        Image target = p.outlineTarget;
        if (target == null) target = p.banner != null ? p.banner : p.stone;
        if (target == null) return;

        Material mat = ResolveBtn06();
        if (mat == null)
        {
            Debug.LogWarning("[BattleStageMap] 找不到 btn06 材质，请在组件上拖入 currentOutlineMaterial");
            return;
        }
        if (!_savedMats.ContainsKey(target))
            _savedMats[target] = target.material;
        target.material = mat;
    }

    void ClearAllOutlines()
    {
        foreach (var kv in _savedMats)
        {
            if (kv.Key != null)
                kv.Key.material = kv.Value;
        }
        _savedMats.Clear();
    }

    Material ResolveBtn06()
    {
        if (currentOutlineMaterial != null) return currentOutlineMaterial;
        if (_btn06 != null) return _btn06;
        _btn06 = Resources.Load<Material>("Materials/btn06");
        if (_btn06 == null) _btn06 = Resources.Load<Material>("btn06");
        return _btn06;
    }

    void OnPedestalClicked(int index)
    {
        if (_flow != null) return;

        if (_branchSelectMode && _next == null)
        {
            if (!_candidateIndices.Contains(index)) return;

            StageData picked = null;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] != null && _candidates[i].stageIndex == index)
                {
                    picked = _candidates[i];
                    break;
                }
            }
            if (picked == null) return;

            ChapterManager.Instance?.SelectBranchAndRoll(picked);
            _next = picked;
            _branchSelectMode = false;
            _candidateIndices.Clear();
            _candidateIndices.Add(index);

            if (_flow != null) StopCoroutine(_flow);
            _flow = StartCoroutine(CoRouletteAndFinalize(index));
            return;
        }

        if (_next == null || index != _next.stageIndex) return;

        // 【2026-10-11 主人拍板】点石墩 → **旗子弹跳放大**作为确认反馈，播完再进关（石墩本身不动）。
        if (_flow != null) StopCoroutine(_flow);
        _flow = StartCoroutine(CoPedestalAccept(index));
    }

    /// <summary>石墩被点中：旗子弹一下（1 → 1.28 → 1）→ 收图进关。期间 _flow 守卫挡重复点击。</summary>
    IEnumerator CoPedestalAccept(int index)
    {
        var p = pedestals[index];
        Transform flag = null;
        if (p != null)
        {
            if (p.banner != null) flag = p.banner.transform;
            else if (p.icon != null) flag = p.icon.transform;
        }
        if (flag != null)
        {
            Vector3 baseScale = flag.localScale;
            float t = 0f;
            const float dur = 0.34f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float k = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);   // 0→1→0 的正弦包络
                flag.localScale = baseScale * (1f + 0.28f * k);
                yield return null;
            }
            flag.localScale = baseScale;
        }

        StopBackdropPulse();
        ClearAllOutlines();
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
        if (root != null) root.SetActive(false);

        // 【2026-10-12 主人拍板】收图进关 → 必须先恢复战斗 HUD，再回调进关，
        // 否则下一关开打时 HUD（金币/技槽/角色栏）不显示。顺序不可调换。
        BattleUI.Instance?.SetHiddenForStageMap(false);

        var stage = _next;
        var cb = _onPick;
        _onPick = null;
        _next = null;
        _branchSelectMode = false;
        _candidates.Clear();
        _candidateIndices.Clear();
        _flow = null;
        cb?.Invoke(stage);
    }

    public void Hide()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }
        StopBackdropPulse();
        ClearAllOutlines();
        if (root != null) root.SetActive(false);
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;

        // 【2026-10-12 主人拍板】关图 → 恢复战斗 HUD（与开图时的隐藏配对，唯一出口）。
        BattleUI.Instance?.SetHiddenForStageMap(false);
    }

    // ===== Backdrop 灯光昏暗抖动 =====

    void StartBackdropPulse()
    {
        if (backdrop == null) return;
        _backdropBase = backdrop.color;
        if (_backdropPulse != null) StopCoroutine(_backdropPulse);
        _backdropPulse = StartCoroutine(CoBackdropPulse());
    }

    void StopBackdropPulse()
    {
        if (_backdropPulse != null)
        {
            StopCoroutine(_backdropPulse);
            _backdropPulse = null;
        }
        if (backdrop != null) backdrop.color = _backdropBase;
    }

    IEnumerator CoBackdropPulse()
    {
        // 【2026-10-11 主人拍板】主人原话「关卡地图闪的有点厉害 别闪了或者闪的慢点」→
        // 折中：保留氛围但**大幅放慢（旧节奏 ×3~4）+ 减小变暗幅度**，并去掉 0.12~0.18s 的快速频闪。
        while (true)
        {
            float deep = UnityEngine.Random.Range(0.88f, 0.96f);          // 旧 0.72~0.88，暗得更轻
            float toDark = UnityEngine.Random.Range(1.4f, 2.2f);          // 旧 0.35~0.7
            float toLight = UnityEngine.Random.Range(1.6f, 2.8f);         // 旧 0.4~0.9
            yield return LerpBackdrop(_backdropBase, _backdropBase * deep, toDark);
            yield return LerpBackdrop(_backdropBase * deep, _backdropBase, toLight);
        }
    }

    IEnumerator LerpBackdrop(Color from, Color to, float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0.05f, seconds);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            if (backdrop != null)
            {
                Color c = Color.Lerp(from, to, Mathf.Clamp01(t));
                c.a = _backdropBase.a;
                backdrop.color = c;
            }
            yield return null;
        }
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ===== 绑定 =====

    void BindRefs()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (backdrop == null)
        {
            var t = root.transform.Find("Backdrop");
            if (t != null) backdrop = t.GetComponent<Image>();
        }
        if (backdrop != null && backdrop.sprite != null)
            UiLayoutStretch.ApplyEnvelopeImage(backdrop.rectTransform, backdrop);
        if (titleText == null)
        {
            var t = FindDeep(root.transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (pedestalRoot == null)
        {
            var t = root.transform.Find("Pedestals");
            if (t != null) pedestalRoot = t;
        }

        if (pedestals == null) pedestals = new List<PedestalRefs>();
        // 【2026-10-11 主人报「关卡界面石墩全锁、点不动」】
        // 根因：BattleStageMap.prefab 里 pedestals 列表被手工填了 10 项，其中 9 项是 {fileID: 0} 空引用，
        // 于是 pedestals.Count 已经 == PedestalCount(10)，旧判定「Count < 10」永远不成立 →
        // 不再按 Pedestal_0~9 节点名重绑 → 10 个石墩一个都不认（全当锁死、不可点）。
        // 修法：只要列表里有空项 / root 丢了，就按节点名重绑一次，别再只看数量。
        bool needRebind = pedestals.Count < PedestalCount;
        if (!needRebind)
        {
            for (int i = 0; i < pedestals.Count; i++)
            {
                if (pedestals[i] == null || pedestals[i].root == null)
                {
                    needRebind = true;
                    break;
                }
            }
        }
        if (needRebind && pedestalRoot != null)
        {
            pedestals.Clear();
            for (int i = 0; i < PedestalCount; i++)
            {
                Transform p = pedestalRoot.Find($"Pedestal_{i}")
                    ?? pedestalRoot.Find($"Stage_{i}")
                    ?? pedestalRoot.Find($"Node_{i}");
                if (p == null) continue;
                pedestals.Add(BindOne(p.gameObject));
            }
        }
        else
        {
            for (int i = 0; i < pedestals.Count; i++)
            {
                if (pedestals[i] != null && pedestals[i].root != null
                    && pedestals[i].lockIcon == null)
                {
                    // 补绑 Lock
                    var L = pedestals[i].root.transform.Find("Lock")
                        ?? pedestals[i].root.transform.Find("LockIcon");
                    if (L != null) pedestals[i].lockIcon = L.gameObject;
                }
            }
        }
    }

    static PedestalRefs BindOne(GameObject go)
    {
        var r = new PedestalRefs { root = go };
        r.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        r.stone = go.transform.Find("Stone")?.GetComponent<Image>()
            ?? go.GetComponent<Image>();
        r.banner = go.transform.Find("Banner")?.GetComponent<Image>();
        r.icon = go.transform.Find("Icon")?.GetComponent<Image>()
            ?? go.transform.Find("Banner/Icon")?.GetComponent<Image>();
        var cleared = go.transform.Find("ClearedMark");
        if (cleared != null) r.clearedMark = cleared.gameObject;
        var lockT = go.transform.Find("Lock") ?? go.transform.Find("LockIcon");
        if (lockT != null) r.lockIcon = lockT.gameObject;
        r.outlineTarget = r.banner != null ? r.banner : r.stone;
        // Highlight 不再使用
        var hi = go.transform.Find("Highlight");
        if (hi != null) hi.gameObject.SetActive(false);
        return r;
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleStageMap);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        go.hideFlags = HideFlags.DontSave;
    }

    static Sprite LoadStageIcon(StageType type)
    {
        // 【2026-10-11 主人「老虎机的配图早就有了，在关卡文件夹里」】
        // 关卡类型图标真源 = Resources/Art/UI/关卡/stage_*.png（stage_normal / elite / rest / boss / forge / enchant）。
        // 旧的两条 StageIcons 路径全库根本没有对应资源，只会取到 null → 界面上一片空，删掉不留第二入口。
        string n = StageRoller.IconName(type);
        return Resources.Load<Sprite>($"Art/UI/关卡/{n}");
    }

    // ===== 预制体结构（从下往上：0 在最底）=====

    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;
        if (host.GetComponent<RectTransform>() == null)
            host.AddComponent<RectTransform>();

        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var bg = NewImage(rootGo.transform, "Backdrop", new Color(0.12f, 0.08f, 0.16f, 1f));
        Stretch(bg.rectTransform);
        // 【2026-10-11 主人「老虎机没有背景，背景应该是关卡界面」】
        // 关卡界面背景真源 = Resources/Art/UI/关卡/层级调整组件_0007_背景.png（主人美术目录里的那张）。
        var mapSp = Resources.Load<Sprite>("Art/UI/关卡/层级调整组件_0007_背景");
        if (mapSp != null)
        {
            bg.sprite = mapSp;
            bg.color = Color.white;
        }

        var title = NewText(rootGo.transform, "Title", "第X章", 36, TextAnchor.MiddleCenter);
        title.color = new Color(0.85f, 0.75f, 1f, 1f);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -80f);
        trt.sizeDelta = new Vector2(560f, 50f);

        var tip = NewText(rootGo.transform, "Tip", "点击石墩上的关卡继续冒险", 22, TextAnchor.MiddleCenter);
        tip.color = new Color(0.8f, 0.75f, 0.65f, 1f);
        var tipRt = tip.rectTransform;
        tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 1f);
        tipRt.pivot = new Vector2(0.5f, 1f);
        tipRt.anchoredPosition = new Vector2(0f, -130f);
        tipRt.sizeDelta = new Vector2(560f, 36f);

        var pedRoot = new GameObject("Pedestals", typeof(RectTransform));
        pedRoot.transform.SetParent(rootGo.transform, false);
        Stretch(pedRoot.GetComponent<RectTransform>());

        // Pedestal_0 在最下面，往上到 Pedestal_9
        Vector2[] pos =
        {
            new Vector2( 20f, -440f), // 0 最底
            new Vector2(-80f, -350f),
            new Vector2( 60f, -260f),
            new Vector2(-40f, -170f),
            new Vector2( 80f,  -80f),
            new Vector2(-70f,   10f),
            new Vector2( 50f,  100f),
            new Vector2(-90f,  190f),
            new Vector2( 70f,  280f),
            new Vector2(-30f,  380f), // 9 最上
        };

        var ui = host.GetComponent<BattleStageMapUI>() ?? host.AddComponent<BattleStageMapUI>();
        ui.root = rootGo;
        ui.backdrop = bg;
        ui.titleText = title;
        ui.pedestalRoot = pedRoot.transform;
        ui.pedestals = new List<PedestalRefs>();
        ui.currentOutlineMaterial = Resources.Load<Material>("Materials/btn06");

        for (int i = 0; i < PedestalCount; i++)
            ui.pedestals.Add(BuildPedestal(pedRoot.transform, i, pos[i]));

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static PedestalRefs BuildPedestal(Transform parent, int index, Vector2 anchored)
    {
        var go = new GameObject($"Pedestal_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(120f, 100f);
        rt.anchoredPosition = anchored;

        var stone = go.GetComponent<Image>();
        stone.color = new Color(0.45f, 0.4f, 0.38f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = stone;

        var banner = NewImage(rt, "Banner", Color.white);
        var brt = banner.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(72f, 88f);
        brt.anchoredPosition = new Vector2(0f, 8f);
        banner.gameObject.SetActive(false);

        var icon = NewImage(brt, "Icon", Color.white);
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(48f, 48f);
        irt.anchoredPosition = new Vector2(0f, 6f);
        icon.preserveAspect = true;

        // ClearedMark = 灰旗（换灰旗图）
        var cleared = NewImage(rt, "ClearedMark", new Color(0.4f, 0.4f, 0.4f, 1f));
        var crt = cleared.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(72f, 88f);
        crt.anchoredPosition = new Vector2(0f, 8f);
        cleared.gameObject.SetActive(false);

        // Lock
        var lockImg = NewImage(rt, "Lock", new Color(0.9f, 0.75f, 0.2f, 1f));
        var lrt = lockImg.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(48f, 48f);
        lrt.anchoredPosition = new Vector2(0f, 28f);
        lockImg.gameObject.SetActive(false);

        // 不生成 Highlight

        return new PedestalRefs
        {
            root = go,
            button = btn,
            stone = stone,
            banner = banner,
            icon = icon,
            clearedMark = cleared.gameObject,
            lockIcon = lockImg.gameObject,
            outlineTarget = banner
        };
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
