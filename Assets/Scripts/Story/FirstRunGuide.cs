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
/// 进度持久化（2026-09-21 已迁入存档）：真值在 SaveData.guideStep / guideDone，随云存档走，
/// 清缓存 / 换机不再重播引导。PlayerPrefs 只作老存档迁移来源与写失败时的兜底，两处同时写。
/// </summary>
public class FirstRunGuide : MonoBehaviour
{
    /// <summary>引导步骤。顺序即流程顺序（由枚举整数值决定，序号进存档），中间不允许插队。</summary>
    public enum GuideStep
    {
        /// <summary>欢迎 + 两句世界观。</summary>
        Welcome = 0,
        /// <summary>顶部「当前目标」条：纯文字提示，告诉玩家顶部那条就是任务条、点它能直达。</summary>
        /// 方案 B：降级为纯文字、不指向实物——任务条（QuestHudBar）显示条件是 TutorialDone，第 2 步时它还没挂出，
        /// 硬高亮会空引用/死锁。所以提前到第 2 位只做文字告知，不依赖实物、不查它的 Rect。
        QuestBar = 1,
        /// <summary>底栏「冒险」：切到冒险页。</summary>
        Adventure = 2,
        /// <summary>冒险页「开始冒险」：进战斗即算完成。</summary>
        FirstBattle = 3,
        /// <summary>角色页 → 天赋 → 点一个节点。</summary>
        Talent = 4,
        /// <summary>酒馆 → 认识老板娘。</summary>
        Tavern = 5,
        /// <summary>游标抵达这里＝全部完成。</summary>
        Done = 6,
    }

    // ============================================================
    // 持久化（SaveData 为真值；PlayerPrefs 只作迁移来源与兜底）
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
    /// <summary>气泡框高。2026-10-10 主人拍板：正文 26→32 后欢迎那段要占 5 行，140 装不下（会溢出底框），故 140→240。</summary>
    const float BubbleHeight = 240f;
    /// <summary>引导气泡正文字号。2026-10-10 主人拍板：字太小看费劲，26→32，要再调改这一个常量。</summary>
    const int BubbleFontSize = 32;
    const float BubbleSideInset = 20f;
    const float RingPulseHz = 1.6f;
    /// <summary>气泡打字机速度（字/秒）。打完之前点屏幕不推进，防止玩家点太快看不到字。</summary>
    const float TypeCharsPerSecond = 28f;

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
    // 2026-10-16 主人拍板：正文超出引导框不再撑大/改边距——跟剧情对话框同款：
    // TextClip(RectMask2D) 裁剪隐藏 + 打字贴底滚动 + 播完放开上下拖回看
    RectTransform _bubbleTextClip;
    ScrollRect _bubbleScrollRect;
    bool _bubbleTypeDone;

    /// <summary>游标：下一个要跑的步骤下标（= 已完成步骤数）。</summary>
    int _stepIndex;
    /// <summary>已上报过 analytics 的步骤下标，避免 Drive 每帧重复上报。</summary>
    int _lastAnalyticsStep = -1;
    Coroutine _drive;

    RectTransform _target;
    bool _visible;
    bool _tapDone;
    bool _hasLastRect;
    Vector4 _lastRect;
    /// <summary>最近一次 CoPoint 的结果：true=达成（不论是玩家做成的还是已经满足）。</summary>
    bool _pointOk;

    // —— 2026-09-22 主人要求：气泡用以前的引导底框、去掉「继续」按钮改点任意处、打字机逐字显示 ——
    /// <summary>当前气泡是否「点任意处推进」（tapToAdvance 步骤）；等玩家实操的步骤不响应点击。</summary>
    bool _tapAdv;
    /// <summary>打字机是否正在逐字显示（没播完时点击无效）。</summary>
    bool _typing;
    Coroutine _typeCo;

    // —— 2026-10-14 修复：塔克收尾剧情开播时遮罩必须让位 ——
    /// <summary>CoPoint 等待期间因剧情/对话开播而隐藏过遮罩；剧情播完后要重新弹回。</summary>
    bool _hiddenByStory;

    /// <summary>剧情/对话此刻是否占着屏幕（遮罩必须让位，否则会吞掉对话的推进点击）。</summary>
    static bool StoryBusy() =>
        (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying)
        || (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible);

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
        // 旧来源（老存档 / 存档尚未就绪时用）
        int step = PlayerPrefs.GetInt(PrefStep, 0);
        bool done = PlayerPrefs.GetInt(PrefDone, 0) == 1;

        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            // 一次性迁移：云存档还是空但本地 PlayerPrefs 有进度 → 搬进存档
            if (data.guideStep <= 0 && step > 0)
            {
                data.guideStep = step;
                if (done) data.guideDone = true;
                SaveSystem.Instance.Save();
            }
            step = data.guideStep;
            done = data.guideDone;
        }

        // 【2026-10-16 修·主人报「引导到点天赋那块出现每日登录」】引导档与新手教程必须同生共死：
        // 删档重打教程时，本地 PlayerPrefs 残留（PA.FirstGuide.Done=1）会被上面的迁移搬进新存档，
        // 变成 guideDone=true 但教程还没出师 → 引导全程静默（「出师啦」收尾气泡不出），
        // 教程一点完天赋每日登录立刻补弹（串行等待秒过）。真值判别：教程未出师 **且**
        // 开场剧情都还没标记完成（= 教程正从头重打，此时引导必在 Awake 这里先于开场剧情创建）
        // → 引导档清零，跟着教程一起重跑。正常老玩家（教程已出师）与教程中途退出的续跑
        // （intro 已标记）都不命中，不受影响。
        if (!StoryProgress.TutorialDone && !StoryProgress.TutorialIntroDone && (done || step > 0))
        {
            step = 0;
            done = false;
            PlayerPrefs.DeleteKey(PrefStep);
            PlayerPrefs.DeleteKey(PrefDone);
            PlayerPrefs.Save();
            if (data != null)
            {
                data.guideStep = 0;
                data.guideDone = false;
                SaveSystem.Instance.Save();
            }
            Debug.Log("[Guide] 教程重打（未出师但引导档已完成）→ 引导档清零，跟教程一起重跑");
        }

        _stepIndex = step;
        if (_stepIndex < 0) _stepIndex = 0;
        if (_stepIndex > (int)GuideStep.Done) _stepIndex = (int)GuideStep.Done;
        // 老版本只写了 Done 标记：补齐游标，避免重复播放
        if (_stepIndex < (int)GuideStep.Done && done)
            _stepIndex = (int)GuideStep.Done;
    }

    void Persist()
    {
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            data.guideStep = _stepIndex;
            SaveSystem.Instance.Save();
        }
        // 兜底：存档不可用时仍写一份本地，避免进度直接丢
        PlayerPrefs.SetInt(PrefStep, _stepIndex);
        PlayerPrefs.Save();
    }

    /// <summary>全部走完的标记：存档 + 本地各写一份（存档为准）。</summary>
    void MarkDone()
    {
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            data.guideStep = (int)GuideStep.Done;
            data.guideDone = true;
            SaveSystem.Instance.Save();
        }
        PlayerPrefs.SetInt(PrefStep, (int)GuideStep.Done);
        PlayerPrefs.SetInt(PrefDone, 1);
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
            if (_stepIndex != _lastAnalyticsStep && step != GuideStep.Done)
            {
                _lastAnalyticsStep = _stepIndex;
                Analytics.TutorialStep(step.ToString(), (int)step); // 埋点：新手引导每步进入
            }
            if (!CanRun(step))
            {
                if (_visible) Hide();
                yield return null;
                continue;
            }

            switch (step)
            {
                case GuideStep.Welcome: yield return CoWelcome(); break;
                case GuideStep.QuestBar:
                    // 【2026-10-17 主人拍板】任务条说明步骤取消：到这一槽位直接跳过推进游标。
                    // 同酒馆步骤口径——覆盖存量档（guideStep 停在 QuestBar 的玩家重进游戏不死循环）。
                    // CoQuestBar 原样保留不删，以后想恢复再说。
                    SkipStep(GuideStep.QuestBar, "任务条说明已取消（2026-10-17 主人拍板）");
                    break;
                case GuideStep.Adventure: yield return CoAdventure(); break;
                case GuideStep.FirstBattle: yield return CoFirstBattle(); break;
                case GuideStep.Talent: yield return CoTalent(); break;
                case GuideStep.Tavern:
                    // 【2026-10-16 主人拍板】酒馆步骤取消：到这一槽位直接跳过推进游标。
                    // 覆盖两条路径——新玩家（天赋收尾后游标走到这）和存量档（guideStep 停在
                    // Tavern 的玩家重进游戏），后者若只是 CanRun 拦住会死循环卡游标。
                    // CoTavern/CoPlayLandlady 原样保留不删，以后想恢复酒馆步骤再说。
                    SkipStep(GuideStep.Tavern, "酒馆步骤已取消（2026-10-16 主人拍板）");
                    break;
                default: yield break;
            }

            yield return null;
        }

        Hide();
        MarkDone();
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
            case GuideStep.Tavern:
                return StoryProgress.TutorialBattleCleared;
            case GuideStep.QuestBar:
                // 方案 B：纯文字第 2 步，不需要任务条实物存在，故不依赖 TutorialBattleCleared。
                return true;
            default:
                return false;
        }
    }

    // ---- 0) 欢迎 + 世界观（无目标：居中气泡 + 半透明全屏遮罩，点任意处推进）----

    IEnumerator CoWelcome()
    {
        // 【2026-10-17 主人拍板】开头白砍到一句：欢迎 + 出师承诺，别繁琐
        // 【2026-10-18 主人拍板】引导全程换前台小姐口吻（可爱版），文案照旧一句
        const string text = "欢迎来到《像素冒险：裂隙之刃》～我是前台小姐姐，第一次来也没关系，人家会手把手教你的哟！";

        yield return CoPoint(text, null, null, true, TapStepTimeout);
        FinishStep(GuideStep.Welcome, "欢迎页");
    }

    // ---- 1) 底栏「冒险」 ----

    IEnumerator CoAdventure()
    {
        TryFallbackNav(MainNavTab.Adventure);
        yield return CoPoint("先点下面的「冒险」去裂隙地图吧～第一关很短的，跟着我走就好啦！",
            () => NavRect(MainNavTab.Adventure),
            AdventurePageOpen,
            false, NavTimeout);
        FinishStep(GuideStep.Adventure, "打开冒险页");
    }

    // ---- 2) 冒险页「开始冒险」：进战斗即算完成 ----

    IEnumerator CoFirstBattle()
    {
        yield return CoPoint("选个喜欢的职业，点「开始冒险」～进了裂隙，这一步就算完成啦！",
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
            yield return CoPoint("第一关就打完啦？好厉害！来变强：点「角色」，用金币点亮一个天赋节点～",
                () => NavRect(MainNavTab.Character), CharacterPageOpen, false, NavTimeout);
        }

        if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen)
        {
            // ② 角色页里的「天赋」按钮
            yield return CoPoint("点「天赋」，进去点亮一个属性节点，属性马上就生效哟～",
                TalentButtonRect, TalentPageOpen, false, NavTimeout);
        }

        if (TalentUI.Instance != null && TalentUI.Instance.IsOpen)
        {
            // ③ 具体的天赋节点
            yield return CoPoint("戳这个亮晶晶的节点！金币够的话，属性立刻就加上去啦～",
                () => TalentNodeRect(before),
                () => TalentUI.LeftUnlockedCount() > before,
                false, NodeTimeout);
        }

        Hide();
        if (TalentUI.LeftUnlockedCount() > before)
        {
            Advance(GuideStep.Talent, null);
            UIManager.Instance?.ShowToast("天赋点亮啦，属性已更新～");

            // 【2026-10-16 主人拍板】引导到天赋就到头了：给一步「关闭引导」收尾——
            // 先收掉还开着的天赋面板（让玩家看着主界面），再弹收尾气泡等玩家点一下；
            // 点完 CoTalent 返回，Drive 下一轮走到 Tavern 槽位 → 按当日拍板直接跳过 → Done
            // 收引导。之后 TownFlow 才弹登录、关登录才提示新邮件，三件事严格串行不交叉。
            var talentUi = TalentUI.Instance;
            if (talentUi != null && talentUi.IsOpen) talentUi.Hide();
            // 【2026-10-18 主人拍板】收尾拆两拍：① 先指引玩家点左下角「公会」回主界面；
            // ② 回到主界面后再弹「教完啦你真棒」夸奖气泡，点任意位置关闭引导。
            // （60s 没点超时放行，夸奖照弹，绝不卡流程。）
            yield return CoPoint("最后点左下角的「公会」，回主界面吧～",
                () => NavRect(MainNavTab.Guild),
                () => MainBottomNav.Instance != null
                    && MainBottomNav.Instance.Current == MainNavTab.Guild,
                false, TapStepTimeout);
            yield return CoPoint("教完啦，你真棒！点一下任意位置，关闭引导吧～有事随时来咨询台找我哟！",
                null, null, true, TapStepTimeout);
        }
        else
        {
            SkipStep(GuideStep.Talent, "玩家未点亮天赋节点（金币不足或窗口未打开）");
        }
    }

    // ---- 4) 顶部「当前目标」条（纯文字第 2 步：只告知，不指向实物）----

    IEnumerator CoQuestBar()
    {
        // 方案 B：此时任务条（QuestHudBar）因显示条件 TutorialDone 还未挂出，不做实物高亮，
        // 也不去查找它的 Rect（找不到会 null）。只弹一行纯文字气泡讲清「顶部任务条」的用途。
        // target 传 null → CoPoint 走「无目标」分支：全屏半透遮罩 + 居中气泡，代码可容忍 null，不会空引用崩。
        yield return CoPoint("屏幕顶部会出现一条「任务条」，写着你现在该做什么——点一下就能直接跳过去，超方便的～",
            null, null, true, TapStepTimeout);
        FinishStep(GuideStep.QuestBar, "任务条说明");
    }

    // ---- 5) 酒馆 → 认识老板娘 ----

    IEnumerator CoTavern()
    {
        if (TavernUI.Instance == null || !IsVisibleHierarchy(TavernUI.Instance.gameObject))
        {
            yield return CoPoint("最后一站是酒馆哟～去和老板娘聊两句吧，招募佣兵也在这里哦。",
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
        _hiddenByStory = false;   // 上一步可能残留（完成/超时/切场景路径不会走到恢复分支）

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
            // 【2026-10-14 修复】塔克收尾剧情开播时收遮罩让位：遮罩(620)会盖住 DialogueUI(500)
            // 的 AdvanceClick，剧情点不动、TownAfterBattleRoutine 的 while(!done) 死等 → 全屏卡死。
            // 让位期间本步骤不推进、不计时；剧情播完重新弹回继续等玩家。
            if (StoryBusy())
            {
                if (_visible) { Hide(); _hiddenByStory = true; }
                yield return null;
                continue;
            }
            if (_hiddenByStory)
            {
                // 剧情播完了：把遮罩弹回来（Show 内部会清 _tapDone / 重开打字机 / 重新定位）
                _hiddenByStory = false;
                Show(follow, text, tapToAdvance);
            }

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
        if (tab == MainNavTab.Guild) btn = nav.guildButton;
        else if (tab == MainNavTab.Adventure) btn = nav.adventureButton;
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
        // 2026-09-22 主人要求：气泡背景用回以前的「引导底框」图（与 TutorialHintUI 顶部横幅同一张）。
        // 图本体在 Assets/Art/UI/引导/（不在 Resources 下），这里从 TutorialHintUI 的 prefab 上取
        // 它的 sprite 引用（prefab 在 Resources/Prefabs/UI/，打包后也能加载），不改任何美术文件。
        var plate = LoadGuidePlateSprite();
        if (plate != null)
        {
            bg.sprite = plate;
            bg.type = Image.Type.Simple;
            bg.color = Color.white;
        }
        _bubbleRt = bg.rectTransform;
        _bubbleRt.anchorMin = _bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
        _bubbleRt.pivot = new Vector2(0.5f, 0.5f);
        _bubbleRt.sizeDelta = new Vector2(0f, BubbleHeight);
        bg.gameObject.SetActive(false);

        _bubbleText = NewText(_bubbleRt, "Text", string.Empty, BubbleFontSize, TextAnchor.MiddleLeft);
        _bubbleText.color = TextColor;
        _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // 2026-09-22 主人要求：去掉「继续」按钮，点屏幕任意处进入下一步。
        // 节点保留但永不激活（最小增量，不删预制体结构），推进改为 Update 里全屏点击检测。
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
        okImg.gameObject.SetActive(false);
    }

    /// <summary>从 TutorialHintUI 的 prefab 上借「引导底框」sprite（只借引用，实例立刻销毁）。</summary>
    static Sprite LoadGuidePlateSprite()
    {
        var prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialHintUI");
        if (prefab == null) return null;
        var tmp = UnityEngine.Object.Instantiate(prefab);
        var banner = tmp.transform.Find("Banner");
        var img = banner != null ? banner.GetComponent<Image>() : null;
        var sp = img != null ? img.sprite : null;
        UnityEngine.Object.Destroy(tmp);
        return sp;
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
        _tapAdv = tapToAdvance;
        _visible = true;

        // 2026-09-22：文字改为打字机逐字显示；「继续」按钮去掉，推进走 Update 里的全屏点击
        StartTyping(text);
        if (_okBtn != null) _okBtn.gameObject.SetActive(false);

        if (_bubbleText != null)
        {
            // 跟剧情对话框同款：Text 包进固定 580×150 @(0,-7) 的 RectMask2D 裁剪窗，
            // 字顶部对齐，装不下的隐藏，播完可上滑回看
            EnsureBubbleTextClip();
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

    /// <summary>只收视觉不碰步骤状态：Drive 协程继续等完成条件，下一步 CoPoint 会重新弹气泡。</summary>
    public void Hide()
    {
        _visible = false;
        _target = null;
        _hasLastRect = false;
        _tapDone = false;
        StopTyping();

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

    /// <summary>
    /// 下一帧把当前步骤气泡重新弹回（2026-10-17 加）。
    /// 用途：职业选择界面取消关闭时，把 Open() 暂收掉的「点开始冒险」气泡请回来——
    /// CoPoint 协程还在等完成条件，扳 _hiddenByStory 开关即可，文案/目标由协程局部参数恢复。
    /// </summary>
    public void NudgeReshow()
    {
        if (_drive == null || IsFinished) return;
        _hiddenByStory = true;
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

    // ============================================================
    // 打字机 + 点任意处推进（2026-09-22）
    // ============================================================

    void StartTyping(string full)
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _fullGuideText = full ?? string.Empty;
        _typing = true;
        _bubbleTypeDone = false;
        if (_bubbleScrollRect != null && _bubbleScrollRect.enabled)
        {
            _bubbleScrollRect.enabled = false; // 打字期间关掉回看，点击全部用于推进
            _bubbleText.raycastTarget = false;
        }
        if (_bubbleText != null) _bubbleText.text = string.Empty;
        _typeCo = StartCoroutine(CoType(_fullGuideText));
    }

    string _fullGuideText;

    IEnumerator CoType(string full)
    {
        if (_bubbleText == null)
        {
            _typing = false;
            yield break;
        }
        float delay = 1f / Mathf.Max(1f, TypeCharsPerSecond);
        for (int i = 1; i <= full.Length; i++)
        {
            _bubbleText.text = full.Substring(0, i);
            ScrollBubbleText();
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        _bubbleText.text = full;
        _typing = false;
        _typeCo = null;
        _bubbleTypeDone = true;
        ScrollBubbleText();
        RefreshBubbleScrollable(); // 播完：正文溢出就放开上下滑动回看
    }

    void StopTyping()
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _typing = false;
        if (_bubbleScrollRect != null && _bubbleScrollRect.enabled)
        {
            _bubbleScrollRect.enabled = false;
            if (_bubbleText != null) _bubbleText.raycastTarget = false;
        }
    }

    // ============================================================
    // 正文裁剪 + 上滑回看（2026-10-16 主人拍板：跟剧情对话框 DialogueUI 同款）
    // 气泡尺寸 / text 内边距都不动：Text 包进 RectMask2D 裁剪窗，
    // 字顶部对齐，装不下的隐藏；打字时贴底滚，播完溢出才放开上下拖回看。
    // ============================================================

    void EnsureBubbleTextClip()
    {
        if (_bubbleText == null || _bubbleTextClip != null) return;
        var src = _bubbleText.rectTransform;
        if (src.parent != null && src.parent.GetComponent<RectMask2D>() != null)
        {
            _bubbleTextClip = src.parent as RectTransform;
            PrepareBubbleTextForClip();
            return;
        }
        var go = new GameObject("TextClip", typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(src.parent, false);
        _bubbleTextClip = go.GetComponent<RectTransform>();
        // 【2026-10-16 主人给参】裁剪窗固定 580×150、中心 (0, -7)，不再拉伸继承气泡
        _bubbleTextClip.anchorMin = new Vector2(0.5f, 0.5f);
        _bubbleTextClip.anchorMax = new Vector2(0.5f, 0.5f);
        _bubbleTextClip.pivot = new Vector2(0.5f, 0.5f);
        _bubbleTextClip.anchoredPosition = new Vector2(0f, -7f);
        _bubbleTextClip.sizeDelta = new Vector2(580f, 150f);
        _bubbleTextClip.SetSiblingIndex(src.GetSiblingIndex());
        _bubbleText.transform.SetParent(_bubbleTextClip, false);
        PrepareBubbleTextForClip();
    }

    void PrepareBubbleTextForClip()
    {
        if (_bubbleText == null || _bubbleTextClip == null) return;
        _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        _bubbleText.resizeTextForBestFit = false;
        var rt = _bubbleText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        float h = Mathf.Max(1f, _bubbleTextClip.rect.height);
        rt.sizeDelta = new Vector2(0f, h);
    }

    void ScrollBubbleText()
    {
        EnsureBubbleTextClip();
        if (_bubbleText == null || _bubbleTextClip == null) return;
        float viewH = Mathf.Max(1f, _bubbleTextClip.rect.height);
        float pref = Mathf.Max(viewH, _bubbleText.preferredHeight);
        var rt = _bubbleText.rectTransform;
        rt.sizeDelta = new Vector2(0f, pref);
        // 手动回看模式下位置交给 ScrollRect 拖拽，这里不再抢
        if (_bubbleScrollRect != null && _bubbleScrollRect.enabled) return;
        float overflow = pref - viewH;
        rt.anchoredPosition = overflow > 1f ? new Vector2(0f, overflow) : Vector2.zero;
    }

    void RefreshBubbleScrollable()
    {
        EnsureBubbleTextClip();
        if (_bubbleTextClip == null || _bubbleText == null) return;
        if (_bubbleScrollRect == null)
        {
            var sr = _bubbleTextClip.gameObject.AddComponent<ScrollRect>();
            sr.content = _bubbleText.rectTransform;
            sr.viewport = _bubbleTextClip;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = true;
            sr.scrollSensitivity = 24f;
            sr.enabled = false;
            _bubbleScrollRect = sr;
        }
        bool overflow = _bubbleText.preferredHeight > _bubbleTextClip.rect.height + 1f;
        bool on = _bubbleTypeDone && overflow;
        if (_bubbleScrollRect.enabled != on)
            _bubbleScrollRect.enabled = on;
        _bubbleText.raycastTarget = on; // 回看时正文接拖拽手势；平时不挡全屏点击推进
        if (on)
            _bubbleScrollRect.verticalNormalizedPosition = 0f; // 贴底停在最新内容
    }

    static bool ScreenTapped()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
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

        // 2026-09-22：点任意处进入下一步（只对原本用「继续」按钮的步骤生效）。
        // 打字机没播完时点击**无效**——防止玩家点太快字还没看完就翻页。
        // 【2026-10-14】剧情/对话在播时不吃点击：玩家点的是剧情推进，不该顺带推掉引导步骤。
        if (_tapAdv && !_typing && !StoryBusy() && ScreenTapped())
            _tapDone = true;

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
