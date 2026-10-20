using System.Collections;
using UnityEngine;

/// <summary>
/// 场景流程（三场景）：
/// · Boot — 持久化系统、登录界面，登录后进 Town
/// · Town — 公会/酒馆/角色/日志等全部在此，切页不 LoadScene
/// · Battle — 仅战斗；异步加载 + Loading 遮罩（含进场景后初始化，100% 再关）
/// </summary>
public class GameSceneManager : Singleton<GameSceneManager>
{
    public const string BOOT_SCENE = "Boot";
    public const string TOWN_SCENE = "Town";
    public const string BATTLE_SCENE = "Battle";

    const float PostLoadWaitTimeout = 20f;

    bool _loadingBattle;
    bool _loadingTown;

    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// 切后台 / 回到前台。微信小游戏侧 wx.onHide→pause=true，wx.onShow→pause=false。
    /// 切后台即结束本会话（计时 + flush）；回前台重新开一个会话。
    /// </summary>
    void OnApplicationPause(bool pause)
    {
        if (pause) Analytics.SessionEnd();
        else Analytics.SessionStart();
    }

    /// <summary>进程退出：结束会话并 flush，避免丢失尾数据。</summary>
    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        Analytics.SessionEnd();
    }

    public void LoadTownScene() => LoadTownSceneAsync();

    public void GoMainHub() => LoadTownSceneAsync();

    /// <summary>清档后回登录界面（Boot 场景）：玩家从「开始冒险」重新开始游戏。
    /// Boot 重入安全（BootManager 幂等），持久系统挂在 DontDestroyOnLoad 不受影响。</summary>
    public void GoLogin()
    {
        StoryDirector.Instance?.NotifySceneChanged();
        UnityEngine.SceneManagement.SceneManager.LoadScene(BOOT_SCENE);
    }

    /// <summary>Battle → Town，异步避免主线程假死</summary>
    public void ReturnToTown()
    {
        LoadTownSceneAsync();
    }

    void LoadTownSceneAsync()
    {
        if (_loadingTown) return;
        _loadingBattle = false;
        // 【2026-10-10 主人拍板】回城后角色界面默认回到主角立绘（看板残留上次选的佣兵会顶掉主角）
        CharacterUI.ResetBoardToPlayer();
        if (!isActiveAndEnabled)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TOWN_SCENE);
            return;
        }
        StartCoroutine(LoadTownAsync());
    }

    IEnumerator LoadTownAsync()
    {
        _loadingTown = true;
        StoryDirector.Instance?.NotifySceneChanged();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool showOverlay = scene.name != TOWN_SCENE;
        if (showOverlay)
        {
            SceneLoadingCoordinator.Begin(SceneLoadingCoordinator.LoadTarget.Town);
            TownSceneBootstrap.ResetForSceneLoad();
        }

        yield return null;
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TOWN_SCENE);
        if (op == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TOWN_SCENE);
            _loadingTown = false;
            if (showOverlay && !TownSceneBootstrap.IsLoadComplete)
                SceneLoadingCoordinator.Finish();
            yield break;
        }

        while (!op.isDone)
        {
            if (showOverlay) SceneLoadingCoordinator.ReportSceneAsync(op);
            yield return null;
        }

        if (showOverlay)
        {
            SceneLoadingCoordinator.ReportSceneLoaded();
            yield return WaitForTownBootstrapComplete();
        }

        TutorialDirector.Instance?.NotifyTownReady();
        if (!StoryProgress.TutorialBattleCleared && !StoryProgress.TutorialDone)
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
        _loadingTown = false;
    }

    /// <summary>仅 Town → Battle 切场景；Town 内底栏切页不走这里</summary>
    public void LoadBattleScene()
    {
        if (_loadingBattle) return;
        if (!isActiveAndEnabled)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
            return;
        }
        StartCoroutine(LoadBattleAsync());
    }

    IEnumerator LoadBattleAsync()
    {
        _loadingBattle = true;
        if (!StoryProgress.TutorialDone && !StoryProgress.TutorialBattleCleared)
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
        StoryDirector.Instance?.NotifySceneChanged();
        AutoGameInitializer.ResetForSceneLoad();
        // 2026-10-11：本次进战斗的章号只算一次（唯一出口）—— loading 背景图与进战斗章节卡共用它。
        // 原来章节卡自己算一份，现在两处同值，别再各算各的。
        int battleChapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        SceneLoadingCoordinator.Begin(SceneLoadingCoordinator.LoadTarget.Battle);
        // 2026-10-11 主人拍板：loading 背景图按章取（Resources/UI/loading/beijing0N）。
        BattleLoadingOverlay.SetChapterBackdrop(battleChapter);
        // 【2026-10-20 主人拍板】进战斗顺序改为「Loading 装载完 → 章节文字 → 开打」：
        // 章节卡挪到 Loading 关闭之后演（不再盖在 Loading 上面靠层级遮）。
        // 无限裂隙照旧不演正式关的章节剧情卡 —— 刷图玩法直接进战斗。
        ChapterSplashOverlay.BattleSplashPending = !AdventureUI.PendingEndlessRift;
        yield return null;

        // 战斗场景先在 Loading 底下静默加载（allowSceneActivation=false 时 progress 停在 0.9，到位即放行）。
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(BATTLE_SCENE);
        if (op != null)
        {
            op.allowSceneActivation = false;
            // 加载就绪等待；超时强放（放行后激活可能掉帧，但卡死等待更糟）
            float loadGuard = 0f;
            while (op.progress < 0.89f && loadGuard < 30f)
            {
                loadGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
                SceneLoadingCoordinator.ReportSceneAsync(op);
                yield return null;
            }
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                SceneLoadingCoordinator.ReportSceneAsync(op);
                yield return null;
            }
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
        }

        SceneLoadingCoordinator.ReportSceneLoaded();
        // 进场景后的初始化由 AutoGameInitializer 上报 45~100% 并 Finish
        yield return WaitForBattleInitComplete();

        // 等 Loading 真正关掉（100% 停留 0.4s 后 Hide）再演章节文字 —— 顺序：Loading → 章节卡。
        // 万一 Coordinator 异常没收，超时兜底照样往下走：章节卡是不透明黑底，会盖住残留的 Loading。
        {
            float hideGuard = 0f;
            while ((SceneLoadingCoordinator.IsActive || BattleLoadingOverlay.IsShowing) && hideGuard < 5f)
            {
                hideGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
                yield return null;
            }
        }

        // Loading 关了才出章节文字；战斗侧 BattleStartSequenceCoroutine 会等它演完才让队伍进场开刷。
        ChapterSplashOverlay.BattleSplashPending = false;
        bool splashTutorial = StoryProgress.ShouldStartTutorialBattle();
        // isTutorial 同 BattleManager 原口径，但切场景那一刻 Rules 还没赋值，
        // 必须用 StoryProgress 自己判断，不要读 BattleManager.Rules。
        var splash = AdventureUI.PendingEndlessRift
            ? null
            : ChapterSplashOverlay.ShowBattleChapter(battleChapter, splashTutorial);

        // 等章节卡演完（点击可跳过）。超时强杀兜底，别把玩家闷死在黑卡里。
        if (splash != null)
        {
            float waitGuard = 0f;
            const float splashTimeout = 12f;
            while (splash != null && !splash.IsFinished && waitGuard < splashTimeout)
            {
                waitGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
                yield return null;
            }
            if (splash != null && !splash.IsFinished)
            {
                Debug.LogWarning("[GameSceneManager] 章节过场超时，强制关闭后继续进战斗");
                Object.Destroy(splash.gameObject);
            }
        }

        _loadingBattle = false;
    }

    static IEnumerator WaitForTownBootstrapComplete()
    {
        float t = 0f;
        while (!TownSceneBootstrap.IsLoadComplete && t < PostLoadWaitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!TownSceneBootstrap.IsLoadComplete && SceneLoadingCoordinator.IsActive)
            SceneLoadingCoordinator.Finish();
    }

    static IEnumerator WaitForBattleInitComplete()
    {
        float t = 0f;
        while (!AutoGameInitializer.IsBattleLoadComplete && t < PostLoadWaitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!AutoGameInitializer.IsBattleLoadComplete && SceneLoadingCoordinator.IsActive)
            SceneLoadingCoordinator.Finish();
    }

    public void EnterAdventure() => LoadBattleScene();

    public void ReloadCurrentScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
