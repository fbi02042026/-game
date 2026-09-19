using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首次启动「强制软引导」（2026-09-19）。
///
/// 数据目标：新手流失 ↓（首日 / 次日留存 ↑）。新玩家最容易在「不知道点哪」的前 3 分钟掉线，
/// 这里把「看目标 → 去冒险 → 打第一关 → 点天赋 → 认识老板娘」五条关键路径手把手走一遍，
/// 走完就自动消失，之后永不再出现。
///
/// 软：表现形式是「半透明遮罩（alpha=0.45）+ 高亮圈 + 文字气泡」，**绝不把 UI 完全盖死**
///     （项目铁律：任何全屏遮罩最多半透）。点高亮区＝直接作用到目标控件；
///     点遮罩区＝不响应，照着气泡上的说明做即可。
/// 强制：步骤必须**逐个**完成才写入下一步，没有「跳过全部」入口。
/// 可中断：完成一步立刻落盘，中途退出游戏，下次进来从当前步继续（走到哪算哪步）。
///
/// 与既有引导的关系：TutorialDirector 仍负责叙事节拍（会长/命名/咨询台/教学战/战后对白）与
/// 战斗内引导；本类只负责**城镇侧那几个关键入口**的分步指路，两边不会同时抢同一件事
/// （详见 TutorialDirector 里 IsStepDone 的让位判断）。
///
/// TODO(建议后续迁入存档)：进度先落 PlayerPrefs（本次不得改 SaveData.cs）。
/// 下一个存档版本请把 guideStep / guideDone 两个字段迁进 SaveData，再删掉这里的
/// PlayerPrefs 读写，改成 SaveSystem.Instance.Data 读写。
/// </summary>
public class FirstRunGuide : MonoBehaviour
{
    /// <summary>引导步骤。顺序即流程顺序，序号进存档，中间不允许插队。</summary>
    public enum GuideStep
    {
        /// <summary>欢迎 + 两句世界观。</summary>
        Welcome = 0,
        /// <summary>底栏「冒险」：切到冒险页。</summary>
        Adventure = 1,
        /// <summary>冒险页「开始冒险」：进战斗即算完成。</summary>
        FirstBattle = 2,
        /// <summary>角色页 → 天赋 → 点一个节点。</summary>
        Talent = 3,
        /// <summary>顶部「当前目标」条：告诉你现在该干嘛。</summary>
        /// 排在教学战之后是因为这条任务条的显示条件是 TutorialDone，太早喊它出来指不到实物。
        QuestBar = 4,
        /// <summary>酒馆 → 认识老板娘。</summary>
        Tavern = 5,
        /// <summary>游标抵达这里＝全部完成。</summary>
        Done = 6,
    }

    // ============================================================
    // 持久化（PlayerPrefs；后续建议迁入 SaveData）
    // ============================================================
    const string PrefStep = "PA.FirstGuide.Step";
    const string PrefDone = "PA.FirstGuide.Done";

    // ============================================================
    // 视觉参数
    // ============================================================
    const string RootName = "FirstRunGuideRoot";
    /// <summary>盖住 TutorialHintUI(600)，低于 TownPopup(900)。</summary>
    const int SortOrder = GameConfig.UiSort.TutorialHint + 20;
    /// <summary>遮罩必须是半透明（项目铁律：最多半透，不许完全盖住 UI）。</summary>
    const float MaskAlpha = 0.45f;
    const float TargetPad = 10f;
    const float RingWidth = 5f;
    const float BubbleHeight = 140f;
    const float BubbleSideInset = 20f;
    const float RingPulseHz = 1.6f;

    // ============================================================
    // 流程参数（超时一律放行 + 记日志，绝不卡死）
    // ============================================================
    const float BindTimeout = 8f;
    const float BindTimeoutBattle = 25f;
    const float TapStepTimeout = 60f;
    const float NavTimeout = 45f;
    const float NodeTimeout = 45f;
    const float TavernTimeout = 45f;

    static readonly Color RingColor = new Color(1f, 0.84f, 0.42f, 1f);
    static readonly Color MaskColor = new Color(0.02f, 0.02f, 0.05f, MaskAlpha);
    static readonly Color BubbleColor = new Color(0.08f, 0.09f, 0.14f, 0.92f);
    static readonly Color TextColor = new Color(0.99f, 0.95f, 0.85f, 1f);
    static readonly Color OkColor = new Color(1f, 0.84f, 0.42f, 1f);

    public static FirstRunGuide Instance { get; private set; }

    Canvas _canvas;
    CanvasGroup _group;
    readonly RectTransform[] _maskRt = new RectTransform[4];
    readonly Image[] _ring = new Image[4];
    readonly RectTransform[] _ringRt = new RectTransform[4];
    RectTransform _holeRt;
    Button _holeBtn;
    RectTransform _bubbleRt;
    Text _bubbleText;
    Button _okBtn;

    /// <summary>游标：下一个要跑的步骤下标（= 已完成步骤数）。</summary>
    int _stepIndex;
    Coroutine _drive;

    RectTransform _target;
    bool _visible;
    bool _tapDone;
    bool _hasLastRect;
    Vector4 _lastRect;
    /// <summary>最近一次 CoPoint 的结果：true=达成（不论是玩家做成的还是已经满足）。</summary>
    bool _pointOk;

    // ============================================================
    // 生命周期 / 对外入口
    // ============================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadProgress();
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static FirstRunGuide Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(RootName, typeof(RectTransform));
        DontDestroyOnLoad(go);
        var ui = go.AddComponent<FirstRunGuide>();
        return ui;
    }

    void LoadProgress()
    {
        _stepIndex = PlayerPrefs.GetInt(PrefStep, 0);
        if (_stepIndex < 0) _stepIndex = 0;
        if (_stepIndex > (int)GuideStep.Done) _stepIndex = (int)GuideStep.Done;
        // 老版本只写了 Done 标记：补齐游标，避免重复播放
        if (_stepIndex < (int)GuideStep.Done && PlayerPrefs.GetInt(PrefDone, 0) == 1)
            _stepIndex = (int)GuideStep.Done;
    }

    void Persist()
    {
        PlayerPrefs.SetInt(PrefStep, _stepIndex);
        PlayerPrefs.Save();
    }

    /// <summary>城镇就绪 / 每次回城调用：启动或继续引导流程（重复调用安全）。</summary>
    public static void NotifyTownReady()
    {
        if (Instance != null && Instance.IsFinished) return;
        var ui = Ensure();
        if (ui == null) return;
        ui.StartDrive();
    }

    void StartDrive()
    {
        if (IsFinished) return;
        if (_drive != null) return;
        if (!gameObject.activeInHierarchy) return;
        _drive = StartCoroutine(Drive());
    }

    public bool IsFinished => _stepIndex >= (int)GuideStep.Done;

    /// <summary>该步骤是否已经走完（已完成或已跳过都算「不用再管」）。</summary>
    public bool IsStepDone(GuideStep step) => _stepIndex > (int)step;

    /// <summary>遮罩/气泡此刻是否占着屏幕（别的提示层可以据此让位）。</summary>
    public bool IsOverlayVisible => _visible;

    // ============================================================
    // 步骤驱动
    // ============================================================

    IEnumerator Drive()
    {
        yield return null;

        while (!IsFinished)
        {
            var step = (GuideStep)_stepIndex;
            if (!CanRun(step))
            {
                if (_visible) Hide();
                yield return null;
                continue;
            }

            switch (step)
            {
                case GuideStep.Welcome: yield return CoWelcome(); break;
                case GuideStep.QuestBar: yield return CoQuestBar(); break;
                case GuideStep.Adventure: yield return CoAdventure(); break;
                case GuideStep.FirstBattle: yield return CoFirstBattle(); break;
                case GuideStep.Talent: yield return CoTalent(); break;
                case GuideStep.Tavern: yield return CoTavern(); break;
                default: yield break;
            }

            yield return null;
        }

        Hide();
        if (PlayerPrefs.GetInt(PrefDone, 0) != 1)
        {
            PlayerPrefs.SetInt(PrefDone, 1);
            PlayerPrefs.Save();
        }
        Debug.Log("[Guide] 首次引导全部完成，之后不再出现");
        _drive = null;
    }

    /// <summary>环境是否适合跑这一步：不加载、在城镇、大厅可见、没有剧情在播。</summary>
    static bool CanRun(GuideStep step)
    {
        if (SceneLoadingCoordinator.IsActive) return false;
        if (!GameSceneGate.IsTown) return false;
        if (GuildHallUI.Instance == null || !GuildHallUI.IsChromeVisible) return false;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return false;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible) return false;

        switch (step)
        {
            case GuideStep.Welcome:
            case GuideStep.Adventure:
            case GuideStep.FirstBattle:
                return StoryProgress.TutorialIntroDone;
            case GuideStep.Talent:
            case GuideStep.QuestBar:
            case GuideStep.Tavern:
                return StoryProgress.TutorialBattleCleared;
            default:
                return false;
        }
    }

    // ---- 0) 欢迎 + 世界观（无目标：居中气泡 + 半透明全屏遮罩，点「继续」推进）----

    IEnumerator CoWelcome()
    {
        const string text = "欢迎来到《像素冒险：裂隙之刃》。\n"
                          + "裂隙每隔一阵就会裂开一道口子，怪物从里面涌出来。\n"
                          + "你是刚签完委托书的新人——跟着我走一遍就懂了。";

        yield return CoPoint(text, null, null, true, TapStepTimeout);
        FinishStep(GuideStep.Welcome, "欢迎页");
    }

    // ---- 1) 底栏「冒险」 ----

    IEnumerator CoAdventure()
    {
        TryFallbackNav(MainNavTab.Adventure);
        yield return CoPoint("点「冒险」去裂隙地图——第一关很短，先把操作跑顺。",
            () => NavRect(MainNavTab.Adventure),
            AdventurePageOpen,
            false, NavTimeout);
        FinishStep(GuideStep.Adventure, "打开冒险页");
    }

    // ---- 2) 冒险页「开始冒险」：进战斗即算完成 ----

    IEnumerator CoFirstBattle()
    {
        yield return CoPoint("选好职业，点「开始冒险」——进到裂隙里这一步就算完成。",
            () => AdventurePageOpen() ? ButtonRect(AdventureStartBtn()) : NavRect(MainNavTab.Adventure),
            InBattleOrLeaving,
            false, 90f,
            bindTimeoutOverride: BindTimeoutBattle);
        FinishStep(GuideStep.FirstBattle, "进入首场战斗");
    }

    // ---- 3) 角色页 → 天赋 → 点一个节点 ----

    IEnumerator CoTalent()
    {
        int before = TalentUI.LeftUnlockedCount();

        var character = CharacterUI.Instance;
        if (character == null || !IsVisibleHierarchy(character.gameObject))
        {
            TryFallbackNav(MainNavTab.Character);
            // ① 底栏「角色」
            yield return CoPoint("打完第一关了，先变强：点「角色」，用金币点亮一个天赋节点。",
                () => NavRect(MainNavTab.Character), CharacterPageOpen, false, NavTimeout);
        }

        if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen)
        {
            // ② 角色页里的「天赋」按钮
            yield return CoPoint("点「天赋」，进去点一个属性节点——属性会立刻生效。",
                TalentButtonRect, TalentPageOpen, false, NavTimeout);
        }

        if (TalentUI.Instance != null && TalentUI.Instance.IsOpen)
        {
            // ③ 具体的天赋节点
            yield return CoPoint("点这个高亮的节点：金币够的话属性马上加上去。",
                () => TalentNodeRect(before),
                () => TalentUI.LeftUnlockedCount() > before,
                false, NodeTimeout);
        }

        Hide();
        if (TalentUI.LeftUnlockedCount() > before)
        {
            Advance(GuideStep.Talent, null);
            UIManager.Instance?.ShowToast("天赋已生效，属性已更新");
        }
        else
        {
            SkipStep(GuideStep.Talent, "玩家未点亮天赋节点（金币不足或窗口未打开）");
        }
    }

    // ---- 4) 顶部「当前目标」条（排在首战之后：这条这时才常驻）----

    IEnumerator CoQuestBar()
    {
        // QuestHudBar 的显示条件是 TutorialDone，正常到这里它已经在屏幕顶部了。
        // 万一还没挂出来（切页瞬时 / 面板没建好）就用无目标气泡把「位置 + 用途」讲清楚。
        if (QuestBarRect() != null)
        {
            yield return CoPoint("屏幕顶部这条会告诉你「现在该做什么」，点一下直达对应玩法。",
                QuestBarRect, null, true, TapStepTimeout);
        }
        else
        {
            yield return CoPoint("记一下屏幕顶部：这里会出现「当前目标」条，"
                              + "随时告诉你下一步去哪，点一下直达。",
                null, null, true, TapStepTimeout);
        }
        FinishStep(GuideStep.QuestBar, "任务条说明");
    }

    // ---- 5) 酒馆 → 认识老板娘 ----

    IEnumerator CoTavern()
    {
        if (TavernUI.Instance == null || !IsVisibleHierarchy(TavernUI.Instance.gameObject))
        {
            yield return CoPoint("最后一站：酒馆。找老板娘聊两句，招募佣兵也在这里。",
                () => NavRect(MainNavTab.Tavern), TavernPageOpen, false, TavernTimeout);
        }

        bool entered = TavernPageOpen();
        if (!entered)
        {
            // 底栏按不到（整条导航都不在）：交给 QuestJumpRouter 跳转，保证这一步还能走完
            entered = QuestJumpRouter.GuideOpenTavern();
            yield return null;
        }

        Hide();
        if (!entered)
        {
            SkipStep(GuideStep.Tavern, "酒馆入口不可用（可能被禁入或界面未就绪）");
            yield break;
        }

        yield return CoPlayLandlady();
        Advance(GuideStep.Tavern, null);
    }

    /// <summary>认识老板娘：直接复用主线的老板娘文案（与任务面板里点出来的那一套同源）。</summary>
    static IEnumerator CoPlayLandlady()
    {
        var beats = MainQuestDialogues.NpcTalk(MainQuestDefs.NpcInnkeeper, 1);
        var dir = StoryDirector.Ensure();
        if (dir == null || beats == null || beats.Count == 0) yield break;

        bool done = false;
        dir.Play(beats, () => done = true);
        float t = 0f;
        while (!done && t < 30f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ============================================================
    // 单步执行器
    // ============================================================

    /// <summary>
    /// 显示「遮罩 + 高亮圈 + 气泡」→ 等待完成条件（或点「继续」）→ 收起。
    /// 不碰步骤游标，结果写在 <see cref="_pointOk"/> 里，由上一步／外层决定推进还是跳过。
    /// 目标控件拿不到、或超时未达成，一律返回 false 并打日志，**不会卡死**。
    /// </summary>
    IEnumerator CoPoint(string text, Func<RectTransform> target, Func<bool> isDone,
        bool tapToAdvance, float timeout, float bindTimeoutOverride = BindTimeout)
    {
        _pointOk = false;

        // 条件已经满足（比如目标页这会儿就是开着的）→ 不用再指一遍
        if (!tapToAdvance && Safe(isDone))
        {
            _pointOk = true;
            yield break;
        }

        float bindTimeout = bindTimeoutOverride > 0f ? bindTimeoutOverride : BindTimeout;
        RectTransform follow = null;
        float bind = 0f;
        bool needTarget = target != null;
        while (needTarget && follow == null && bind < bindTimeout)
        {
            follow = Safe(target);
            if (follow == null)
            {
                bind += Time.unscaledDeltaTime;
                yield return null;
                continue;
            }
            break;
        }

        if (needTarget && follow == null)
        {
            Debug.LogWarning("[Guide] 目标控件拿不到，跳过本次引导展示");
            Hide();
            yield break;
        }

        if (!tapToAdvance && Safe(isDone))
        {
            _pointOk = true;
            yield break;
        }

        _tapDone = false;
        Show(follow, text, tapToAdvance);

        float t = 0f;
        while (true)
        {
            // 切页会把整页内容重建，旧引用会失效：每帧重新定位，丢的那几帧沿用上一次量到的矩形
            if (needTarget) SetTarget(Safe(target));

            bool done = tapToAdvance ? _tapDone : Safe(isDone);
            if (done)
            {
                Hide();
                _pointOk = true;
                yield break;
            }

            // 场景已经切走（比如点了开始冒险）→ 收起遮罩，别把 loading 盖住
            if (tapToAdvance == false && !GameSceneGate.IsTown)
            {
                Hide();
                _pointOk = Safe(isDone);
                yield break;
            }

            t += Time.unscaledDeltaTime;
            if (t >= timeout)
            {
                Hide();
                Debug.LogWarning("[Guide] 引导步骤等待超时，放行不卡流程");
                yield break;
            }
            yield return null;
        }
    }

    void FinishStep(GuideStep step, string label)
    {
        Hide();
        if (_pointOk) Advance(step, null);
        else SkipStep(step, label + "未完成");
    }

    void Advance(GuideStep step, string note)
    {
        if (_stepIndex != (int)step) return;      // 游标已经动过：别重复推进
        _stepIndex = (int)step + 1;
        Persist();
        Debug.Log("[Guide] 完成 " + step + " → " + (GuideStep)_stepIndex
                  + (string.IsNullOrEmpty(note) ? "" : " (" + note + ")"));
    }

    void SkipStep(GuideStep step, string reason)
    {
        if (_stepIndex != (int)step) return;
        _stepIndex = (int)step + 1;
        Persist();
        Debug.LogWarning("[Guide] 跳过 " + step + "：" + reason);
    }

    static bool Safe(Func<bool> f) => f != null && f();

    static RectTransform Safe(Func<RectTransform> f) => f != null ? f() : null;

    static bool IsVisibleHierarchy(GameObject go) => go != null && go.activeInHierarchy;

    // ============================================================
    // 条件 / 目标控件（一律运行时按名查找，拿不到返回 null）
    // ============================================================

    static bool InBattleOrLeaving() => GameSceneGate.IsBattle || !GameSceneGate.IsTown;

    static bool AdventurePageOpen() => IsVisibleHierarchy(AdventureUI.Instance?.gameObject);

    static bool CharacterPageOpen() => IsVisibleHierarchy(CharacterUI.Instance?.gameObject);

    static bool TavernPageOpen() => IsVisibleHierarchy(TavernUI.Instance?.gameObject);

    static bool TalentPageOpen() => TalentUI.Instance != null && TalentUI.Instance.IsOpen;

    static Button AdventureStartBtn()
    {
        var adv = AdventureUI.Instance;
        return adv != null ? adv.startBtn : null;
    }

    /// <summary>顶部任务条本体（QuestHudBar 运行时建的「Bar」节点）。</summary>
    static RectTransform QuestBarRect()
    {
        var hud = QuestHudBar.Instance;
        if (hud == null) return null;
        var bar = TownSharedChrome.FindDeep(hud.transform, "Bar") as RectTransform;
        return IsVisibleHierarchy(bar != null ? bar.gameObject : null) ? bar : null;
    }

    static RectTransform NavRect(MainNavTab tab)
    {
        var nav = MainBottomNav.Instance;
        if (nav == null) return null;
        Button btn = null;
        if (tab == MainNavTab.Adventure) btn = nav.adventureButton;
        else if (tab == MainNavTab.Character) btn = nav.characterButton;
        else if (tab == MainNavTab.Tavern) btn = nav.tavernButton;
        return ButtonRect(btn);
    }

    /// <summary>
    /// 兜底导航：按名字找不到底栏按钮时（整个底栏都不在），退回 QuestJumpRouter 的跳转能力，
    /// 保证这一步还能走完。**只做「切页」，不替玩家点最终按钮。**
    /// </summary>
    static bool TryFallbackNav(MainNavTab tab)
    {
        if (NavRect(tab) != null) return false;   // 能指到按钮就让玩家自己点
        if (tab == MainNavTab.Adventure) return QuestJumpRouter.GuideOpenAdventure(1);
        if (tab == MainNavTab.Tavern) return QuestJumpRouter.GuideOpenTavern();
        if (tab == MainNavTab.Character) return QuestJumpRouter.GuideOpenTalent();
        return false;
    }

    static RectTransform ButtonRect(Button btn)
    {
        if (btn == null) return null;
        if (!IsVisibleHierarchy(btn.gameObject)) return null;
        return btn.GetComponent<RectTransform>();
    }

    static RectTransform TalentButtonRect()
    {
        var ch = CharacterUI.Instance;
        return ButtonRect(ch != null ? ch.talentButton : null);
    }

    static RectTransform TalentNodeRect(int index0)
    {
        var ui = TalentUI.Instance;
        if (ui == null || !ui.IsOpen) return null;
        var rt = ui.GetLeftNode(index0);
        return IsVisibleHierarchy(rt != null ? rt.gameObject : null) ? rt : null;
    }

    // ============================================================
    // 遮罩 / 高亮圈 / 气泡
    // ============================================================

    void Build()
    {
        if (_canvas != null) return;

        // Screen Space Camera 的根必须是 RectTransform（Ensure() 建物体时已经带上，这里兜个底）
        if (GetComponent<RectTransform>() == null)
            gameObject.AddComponent<RectTransform>();
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(_canvas, SortOrder);
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // ① 半透明遮罩四条：中间给目标留「洞」
        string[] maskNames = { "MaskTop", "MaskBottom", "MaskLeft", "MaskRight" };
        for (int i = 0; i < 4; i++)
        {
            var img = NewImage(transform, maskNames[i], MaskColor);
            img.raycastTarget = true;
            img.gameObject.SetActive(false);
            _maskRt[i] = img.rectTransform;
        }

        // ② 高亮圈：四条细边，呼吸
        string[] ringNames = { "RingTop", "RingBottom", "RingLeft", "RingRight" };
        for (int i = 0; i < 4; i++)
        {
            var img = NewImage(transform, ringNames[i], RingColor);
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            _ring[i] = img;
            _ringRt[i] = img.rectTransform;
        }

        // ③ 洞：透明 Button，点它等于点目标按钮
        var hole = NewImage(transform, "Hole", Color.clear);
        hole.raycastTarget = true;
        hole.gameObject.SetActive(false);
        _holeRt = hole.rectTransform;
        _holeBtn = hole.gameObject.AddComponent<Button>();
        _holeBtn.transition = Selectable.Transition.None;
        _holeBtn.onClick.AddListener(OnHoleClicked);

        BuildBubble();
        _bubbleRt.SetAsLastSibling();

        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildBubble()
    {
        var bg = NewImage(transform, "Bubble", BubbleColor);
        bg.raycastTarget = false;
        _bubbleRt = bg.rectTransform;
        _bubbleRt.anchorMin = _bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
        _bubbleRt.pivot = new Vector2(0.5f, 0.5f);
        _bubbleRt.sizeDelta = new Vector2(0f, BubbleHeight);
        bg.gameObject.SetActive(false);

        _bubbleText = NewText(_bubbleRt, "Text", string.Empty, 26, TextAnchor.MiddleLeft);
        _bubbleText.color = TextColor;
        _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;

        var okImg = NewImage(_bubbleRt, "Ok", OkColor);
        okImg.raycastTarget = true;
        var okRt = okImg.rectTransform;
        okRt.anchorMin = okRt.anchorMax = new Vector2(1f, 0f);
        okRt.pivot = new Vector2(1f, 0f);
        okRt.anchoredPosition = new Vector2(-20f, 18f);
        okRt.sizeDelta = new Vector2(148f, 54f);
        _okBtn = okImg.gameObject.AddComponent<Button>();
        _okBtn.onClick.AddListener(OnOkClicked);
        var okLabel = NewText(okRt, "Label", "继续", 24, TextAnchor.MiddleCenter);
        Stretch(okLabel.rectTransform);
        okLabel.color = new Color(0.14f, 0.11f, 0.05f, 1f);
    }

    void SetTarget(RectTransform target)
    {
        _target = target;
    }

    void Show(RectTransform target, string text, bool tapToAdvance)
    {
        Build();
        if (_canvas != null) UICanvasSetup.RefreshPopup(_canvas, SortOrder);

        SetTarget(target);
        _hasLastRect = false;
        _tapDone = false;
        _visible = true;

        if (_bubbleText != null) _bubbleText.text = text ?? string.Empty;
        if (_okBtn != null) _okBtn.gameObject.SetActive(tapToAdvance);

        // 文字给「继续」按钮让位：有按钮时下边距抬起
        if (_bubbleText != null)
        {
            var lrt = _bubbleText.rectTransform;
            Stretch(lrt);
            lrt.offsetMin = new Vector2(24f, tapToAdvance ? 82f : 20f);
            lrt.offsetMax = new Vector2(-24f, -20f);
        }

        if (_group != null)
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
        }
        if (_bubbleRt != null) _bubbleRt.gameObject.SetActive(true);

        RefreshLayout();
    }

    void Hide()
    {
        _visible = false;
        _target = null;
        _hasLastRect = false;
        _tapDone = false;

        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        SetAllActive(_maskRt, false);
        SetAllActive(_ringRt, false);
        if (_holeRt != null) _holeRt.gameObject.SetActive(false);
        if (_bubbleRt != null) _bubbleRt.gameObject.SetActive(false);
        if (_canvas != null) _canvas.sortingOrder = SortOrder;
    }

    void OnHoleClicked()
    {
        if (!_visible || _target == null) return;
        var btn = _target.GetComponent<Button>();
        if (btn == null) btn = _target.GetComponentInParent<Button>();
        if (btn != null && btn.interactable)
            btn.onClick.Invoke();
    }

    void OnOkClicked()
    {
        _tapDone = true;
    }

    void Update()
    {
        if (!_visible) return;
        // 场景已经切走（点了开始冒险等）：立刻收掉遮罩，别把战斗 Loading 盖住
        if (!GameSceneGate.IsTown)
        {
            Hide();
            return;
        }
        RefreshLayout();
        PulseRing();
    }

    void PulseRing()
    {
        float a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.283185f * RingPulseHz));
        for (int i = 0; i < _ring.Length; i++)
        {
            if (_ring[i] == null) continue;
            _ring[i].color = new Color(RingColor.r, RingColor.g, RingColor.b, a);
        }
    }

    void RefreshLayout()
    {
        var root = transform as RectTransform;
        if (root == null) return;
        if (root.rect.width <= 0f || root.rect.height <= 0f) return;

        float minX, maxX, minY, maxY;
        bool measured = TryMeasure(out minX, out maxX, out minY, out maxY);
        if (measured)
        {
            _lastRect = new Vector4(minX, maxX, minY, maxY);
            _hasLastRect = true;
        }
        else if (_hasLastRect)
        {
            minX = _lastRect.x;
            maxX = _lastRect.y;
            minY = _lastRect.z;
            maxY = _lastRect.w;
        }

        if (!measured && !_hasLastRect)
        {
            // 没有目标（欢迎步骤）：全屏半透明遮罩 + 居中气泡
            SetAllActive(_ringRt, false);
            if (_holeRt != null) _holeRt.gameObject.SetActive(false);
            SetAllActive(_maskRt, false);
            PlaceStrip(_maskRt[0], root.rect.xMin, root.rect.xMax, root.rect.yMin, root.rect.yMax);
            PlaceBubbleCenter(root);
            return;
        }

        float pad = TargetPad;
        float x0 = minX - pad;
        float x1 = maxX + pad;
        float y0 = minY - pad;
        float y1 = maxY + pad;
        float left = root.rect.xMin;
        float right = root.rect.xMax;
        float bot = root.rect.yMin;
        float top = root.rect.yMax;

        // 遮罩：四条留出中间的洞
        PlaceStrip(_maskRt[0], left, right, y1, top);
        PlaceStrip(_maskRt[1], left, right, bot, y0);
        PlaceStrip(_maskRt[2], left, x0, y0, y1);
        PlaceStrip(_maskRt[3], x1, right, y0, y1);

        // 高亮圈：抠在洞口边上
        PlaceStrip(_ringRt[0], x0 - RingWidth, x1 + RingWidth, y1, y1 + RingWidth);
        PlaceStrip(_ringRt[1], x0 - RingWidth, x1 + RingWidth, y0 - RingWidth, y0);
        PlaceStrip(_ringRt[2], x0 - RingWidth, x0, y0, y1);
        PlaceStrip(_ringRt[3], x1, x1 + RingWidth, y0, y1);

        // 洞：点它＝点目标控件
        PlaceStrip(_holeRt, x0, x1, y0, y1);
        if (_holeRt != null && !measured) _holeRt.gameObject.SetActive(false);

        if (!measured) SetAllActive(_ringRt, false);

        PlaceBubbleNearTarget(root, y0, y1);
    }

    bool TryMeasure(out float minX, out float maxX, out float minY, out float maxY)
    {
        if (Measure(out minX, out maxX, out minY, out maxY)) return true;
        if (_target != null)
        {
            // 目标节点常被整体重建，那一帧 LayoutGroup 还没回流，强刷一次再量
            LayoutRebuilder.ForceRebuildLayoutImmediate(_target);
            return Measure(out minX, out maxX, out minY, out maxY);
        }
        return false;
    }

    bool Measure(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0f;
        var root = transform as RectTransform;
        if (root == null || _target == null) return false;
        if (!IsVisibleHierarchy(_target.gameObject)) return false;

        var followCanvas = _target.GetComponentInParent<Canvas>();
        Camera hintCam = _canvas != null ? _canvas.worldCamera : UICanvasSetup.ResolveUiCamera();
        Camera followCam = followCanvas != null ? followCanvas.worldCamera : hintCam;
        if (followCam == null) followCam = hintCam;
        if (hintCam == null) return false;

        var corners = new Vector3[4];
        _target.GetWorldCorners(corners);
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(followCam, corners[i]);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hintCam, out local))
                return false;
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }
        return maxX > minX && maxY > minY;
    }

    void PlaceBubbleCenter(RectTransform root)
    {
        if (_bubbleRt == null) return;
        SizeBubble(root);
        _bubbleRt.anchoredPosition = new Vector2(0f, root.rect.height * 0.18f);
    }

    /// <summary>气泡贴着目标放：优先下方，下方塞不下就翻到上方，最后夹在屏幕内。</summary>
    void PlaceBubbleNearTarget(RectTransform root, float holeMinY, float holeMaxY)
    {
        if (_bubbleRt == null) return;
        SizeBubble(root);

        const float gap = 20f;
        float half = BubbleHeight * 0.5f;
        float y = holeMinY - gap - half;
        if (y - half < root.rect.yMin + 8f)
            y = holeMaxY + gap + half;
        y = Mathf.Clamp(y, root.rect.yMin + half + 8f, root.rect.yMax - half - 8f);
        _bubbleRt.anchoredPosition = new Vector2(0f, y);
    }

    void SizeBubble(RectTransform root)
    {
        if (_bubbleRt == null) return;
        _bubbleRt.sizeDelta = new Vector2(Mathf.Max(280f, root.rect.width - BubbleSideInset * 2f), BubbleHeight);
    }

    // ============================================================
    // UI 小工具
    // ============================================================

    static void SetAllActive(RectTransform[] arr, bool on)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null) arr[i].gameObject.SetActive(on);
        }
    }

    static void PlaceStrip(RectTransform rt, float xmin, float xmax, float ymin, float ymax)
    {
        if (rt == null) return;
        float w = Mathf.Max(0f, xmax - xmin);
        float h = Mathf.Max(0f, ymax - ymin);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((xmin + xmax) * 0.5f, (ymin + ymax) * 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.gameObject.SetActive(w > 0.5f && h > 0.5f);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
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
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }
}
