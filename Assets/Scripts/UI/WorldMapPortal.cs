using UnityEngine;

/// <summary>
/// 世界地图传送门：打完第 1 章后才出现在城镇里，进入（点击）后弹出世界地图选区域。
///
/// 用法：把这个脚本挂到城镇场景里的传送门对象上（需要有 Collider2D，或挂一个 UI Button）。
/// 第 1 章未通关时自动隐藏；通关后自动显形并可点。
/// </summary>
public class WorldMapPortal : MonoBehaviour
{
    [Header("交互")]
    [Tooltip("可选：传送门上的 UI 按钮；不填则用 Collider2D + 点击")]
    public UnityEngine.UI.Button portalButton;

    [Tooltip("可选：挂 PortalAnimator 的子节点，没有就留空")]
    public Transform visualRoot;

    [Tooltip("进入后是否先播一次传送特效")]
    public bool playEnterVfx = true;

    bool _wired;

    void OnEnable()
    {
        Wire();
        RefreshVisible();
    }

    void OnMouseDown()
    {
        TryOpen();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // 不用 CompareTag("Hero")：工程里没定义这个 tag 会直接抛 UnityException
        if (other.GetComponent<Hero>() == null) return;
        TryOpen();
    }

    void Wire()
    {
        if (_wired) return;
        _wired = true;

        if (portalButton == null)
            portalButton = GetComponent<UnityEngine.UI.Button>();
        if (portalButton != null)
        {
            portalButton.onClick.RemoveAllListeners();
            portalButton.onClick.AddListener(TryOpen);
        }

        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    /// <summary>传送门是否应该出现：已通关第 1 章。</summary>
    public static bool ShouldAppear()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data != null && data.HasClearedChapter(1);
    }

    /// <summary>按存档显隐传送门。</summary>
    public void RefreshVisible()
    {
        bool show = ShouldAppear();
        if (gameObject.activeSelf != show)
            gameObject.SetActive(show);
        if (show && visualRoot != null && !visualRoot.gameObject.activeSelf)
            visualRoot.gameObject.SetActive(true);
    }

    /// <summary>进入传送门：弹出世界地图。</summary>
    public void TryOpen()
    {
        if (!ShouldAppear()) return;
        if (WorldMapPopup.Current != null) return; // 已经开着

        if (playEnterVfx) PlayVfx();

        WorldMapPopup.Show(OnRegionPicked);
    }

    void OnRegionPicked(int chapter)
    {
        var ui = AdventureUI.Instance;
        if (ui != null)
        {
            ui.OnWorldMapEnter(chapter);
            return;
        }
        Debug.LogWarning("[WorldMapPortal] 找不到 AdventureUI，无法开战");
    }

    void PlayVfx()
    {
        var prefab = Resources.Load<GameObject>("VFX/other/world/传送");
        if (prefab == null) return;
        var go = Instantiate(prefab, transform.position, Quaternion.identity);
        go.name = "WorldMapPortalVfx";
        Destroy(go, 2f);
    }
}
