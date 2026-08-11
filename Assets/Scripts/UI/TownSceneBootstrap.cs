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
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttachIfTown()
    {
        if (!GameSceneGate.IsTown) return;
        if (_done) return;
        var hall = Object.FindObjectOfType<GuildHallUI>();
        if (hall != null && hall.GetComponent<TownSceneBootstrap>() == null)
            hall.gameObject.AddComponent<TownSceneBootstrap>();
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
        GameFonts.GetChinese();
        GameFonts.GetNumber();
        SceneLoadingCoordinator.ReportPostLoadStep(1, 3);
        yield return null;

        // 2/3 常用预制体
        Resources.Load<GameObject>("Prefabs/Town/TavernUI");
        SceneLoadingCoordinator.ReportPostLoadStep(2, 3);
        yield return null;

        // 3/3 功能页预实例化
        TownHubController hub = TownHubController.Instance;
        if (hub == null) hub = FindObjectOfType<TownHubController>();
        if (hub != null)
            hub.PreloadAllPages();

        SceneLoadingCoordinator.ReportPostLoadStep(3, 3);
        yield return null;

        _done = true;
        IsLoadComplete = true;
        Debug.Log("[TownBootstrap] Town 功能页预加载完成（切页应无 Instantiate 延迟）");
        SceneLoadingCoordinator.Finish();
    }

    void OnDestroy()
    {
        _done = false;
        IsLoadComplete = false;
        TownSharedChrome.InvalidateCache();
    }
}
