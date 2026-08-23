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
        TryClaimTownOfflineReward();
        TutorialDirector.Instance?.NotifyTownReady();
    }

    static bool _offlineClaimedThisTownVisit;

    /// <summary>进城镇一次最多弹一次离线收益（农场金）。</summary>
    static void TryClaimTownOfflineReward()
    {
        if (_offlineClaimedThisTownVisit) return;
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
