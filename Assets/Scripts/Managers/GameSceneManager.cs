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
    void OnApplicationQuit()
    {
        Analytics.SessionEnd();
    }

    public void LoadTownScene() => LoadTownSceneAsync();

    public void GoMainHub() => LoadTownSceneAsync();

    /// <summary>Battle → Town，异步避免主线程假死</summary>
    public void ReturnToTown()
    {
        LoadTownSceneAsync();
    }

    void LoadTownSceneAsync()
    {
        if (_loadingTown) return;
        _loadingBattle = false;
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
        SceneLoadingCoordinator.Begin(SceneLoadingCoordinator.LoadTarget.Battle);
        yield return null;

        // 进战斗前过场：章节卡在 Loading 期间演完再加载战斗场景（详见主人需求）。
        // 章节号/isTutorial 同 BattleManager 原口径，但切场景那一刻 Rules 还没赋值，
        // 必须用 StoryProgress 自己判断，不要读 BattleManager.Rules。
        int splashChapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        bool splashTutorial = StoryProgress.ShouldStartTutorialBattle();
        var splash = ChapterSplashOverlay.ShowBattleChapter(splashChapter, splashTutorial);
        {
            float waitGuard = 0f;
            const float splashTimeout = 8f;
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

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(BATTLE_SCENE);
        if (op == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
            _loadingBattle = false;
            yield return WaitForBattleInitComplete();
            yield break;
        }

        while (!op.isDone)
        {
            SceneLoadingCoordinator.ReportSceneAsync(op);
            yield return null;
        }

        SceneLoadingCoordinator.ReportSceneLoaded();
        // 进场景后的初始化由 AutoGameInitializer 上报 45~100% 并 Finish
        yield return WaitForBattleInitComplete();

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
