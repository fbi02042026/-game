using System.Collections;
using UnityEngine;

/// <summary>
/// Town 场景启动：预热字体、预实例化各功能页（隐藏），保证底栏点击无延迟。
/// Boot / Battle / Town 三场景；仅 Battle 切换走 Loading。
/// </summary>
public class TownSceneBootstrap : MonoBehaviour
{
    static bool _done;

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
        TownSharedChrome.InvalidateCache();
        _done = false;

        // 字体与常用 Resources 先加载，避免首点酒馆卡一下
        GameFonts.GetChinese();
        GameFonts.GetNumber();
        Resources.Load<GameObject>("Prefabs/Town/TavernUI");
        yield return null;

        TownHubController hub = TownHubController.Instance;
        if (hub == null) hub = FindObjectOfType<TownHubController>();
        if (hub != null)
            hub.PreloadAllPages();

        yield return null;
        _done = true;
        Debug.Log("[TownBootstrap] Town 功能页预加载完成（切页应无 Instantiate 延迟）");
    }

    void OnDestroy()
    {
        _done = false;
        TownSharedChrome.InvalidateCache();
    }
}
