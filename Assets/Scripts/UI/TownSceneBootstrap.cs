using System.Collections;
using UnityEngine;

/// <summary>
/// Town 场景启动：预热字体、预实例化各功能页（隐藏），保证底栏点击无延迟。
/// Boot / Battle / Town 三场景；切场景时由 SceneLoadingCoordinator 等待本脚本完成后再关 Loading。
/// </summary>
public class TownSceneBootstrap : MonoBehaviour
{
    static bool _done;

    /// <summary>本次进 Town 后预热是否完成</summary>
    public static bool IsLoadComplete { get; private set; }

    public static void ResetForSceneLoad()
    {
        IsLoadComplete = false;
        _done = false;
        _offlineClaimedThisTownVisit = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttachIfTown()
    {
        if (!GameSceneGate.IsTown) return;
        if (_done) return;

        var hall = Object.FindObjectOfType<GuildHallUI>();
        if (hall != null)
        {
            if (hall.GetComponent<TownSceneBootstrap>() == null)
                hall.gameObject.AddComponent<TownSceneBootstrap>();
            return;
        }

        // GuildHallUI 尚未就绪时挂到场景任意存活对象，避免 Loading 卡在 55%
        Component host = Object.FindObjectOfType<TownSceneManager>();
        if (host == null) host = Object.FindObjectOfType<TownHubController>();
        if (host == null)
        {
            var go = new GameObject("TownSceneBootstrap");
            go.AddComponent<TownSceneBootstrap>();
            return;
        }
        if (host.GetComponent<TownSceneBootstrap>() == null)
            host.gameObject.AddComponent<TownSceneBootstrap>();
    }

    void Start()
    {
        if (!GameSceneGate.IsTown) return;
        if (_done) return;
        StartCoroutine(BootstrapRoutine());
    }

    IEnumerator BootstrapRoutine()
    {
        IsLoadComplete = false;
        TownSharedChrome.InvalidateCache();

        // 1/3 字体
        try
        {
            GameFonts.GetChinese();
            GameFonts.GetNumber();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 字体预热异常: " + e.Message);
        }
        SceneLoadingCoordinator.ReportPostLoadStep(1, 3);
        yield return null;

        // 2/3 常用预制体
        try
        {
            Resources.Load<GameObject>("Prefabs/Town/TavernUI");
            Resources.Load<GameObject>("Prefabs/Town/AdventureUI");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 预制体预热异常: " + e.Message);
        }
        SceneLoadingCoordinator.ReportPostLoadStep(2, 3);
        yield return null;

        // 3/3 功能页预实例化
        try
        {
            TownHubController hub = TownHubController.Instance;
            if (hub == null) hub = FindObjectOfType<TownHubController>();
            if (hub != null)
                hub.PreloadAllPages();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TownBootstrap] 功能页预加载异常: " + e);
        }

        SceneLoadingCoordinator.ReportPostLoadStep(3, 3);
        yield return null;

        _done = true;
        IsLoadComplete = true;
        Debug.Log("[TownBootstrap] Town 功能页预加载完成（切页应无 Instantiate 延迟）");
        if (SceneLoadingCoordinator.IsActive)
            SceneLoadingCoordinator.Finish();
        yield return null;
        yield return null;
        TownHubController.ConsumePendingAdventure();
        // 上次战斗被强杀 → 判撤离失败并结算；弹了面板就跳过本轮离线收益与登录奖励，
        // 避免两个半透明弹窗叠在一起
        bool settled = SettleInterruptedRunOnce();
        if (!settled)
        {
            TryClaimTownOfflineReward();
            TryDailyLoginOnce();
        }
        TutorialDirector.Instance?.NotifyTownReady();
        // 主界面「当前目标」条：进 Town 就挂上，玩家不用点开冒险日志才知道干嘛
        try
        {
            QuestHudBar.Ensure();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 任务条挂载异常: " + e.Message);
        }
    }

    static bool _loginCheckedThisTownVisit;

    /// <summary>
    /// 登录奖励：进 Town 检查一次，有可领的才弹。
    /// 累计天数在 OnEnterGame 里按自然日累加（同一天重进不加）。
    /// </summary>
    static void TryDailyLoginOnce()
    {
        if (_loginCheckedThisTownVisit) return;
        // 引导未完成不弹，避免半透明遮罩挡「点冒险」
        if (!StoryProgress.TutorialDone) return;
        if (SaveSystem.Instance?.Data == null) return;
        _loginCheckedThisTownVisit = true;

        DailyLoginSystem.OnEnterGame();
        if (DailyLoginSystem.HasClaimable())
            DailyLoginUI.Show();
    }

    static bool _resumeChecked;

    /// <summary>每次进 Town 只检查一次中断结算（真结算了返回 true）</summary>
    static bool SettleInterruptedRunOnce()
    {
        if (_resumeChecked) return false;
        _resumeChecked = true;
        try
        {
            return BattleStateSaver.SettleInterruptedRun();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 中断结算异常: " + e.Message);
            return false;
        }
    }

    static bool _offlineClaimedThisTownVisit;

    /// <summary>进城镇一次最多弹一次离线收益（农场金）。</summary>
    static void TryClaimTownOfflineReward()
    {
        if (_offlineClaimedThisTownVisit) return;
        // 新手引导未完成时不弹，避免半透明遮罩挡「点冒险」
        if (!StoryProgress.TutorialDone) return;

        var save = SaveSystem.Instance;
        if (save?.Data == null) return;

        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long secs = System.Math.Max(0, now - save.Data.lastSaveTime);
        if (secs < 60) return; // 不足 1 分钟不弹

        int farm = save.Data.townLevel != null ? save.Data.townLevel.farm : 0;
        long gold = save.CalcOfflineGold();
        _offlineClaimedThisTownVisit = true;
        if (gold <= 0)
        {
            save.Save();
            return;
        }

        ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: false);
        double maxMin = (8 + farm * 2) * 60.0;
        OfflineRewardPopup.Show(gold, System.Math.Min(secs / 60.0, maxMin));
    }

    void OnDestroy()
    {
        _done = false;
        IsLoadComplete = false;
        TownSharedChrome.InvalidateCache();
    }
}
