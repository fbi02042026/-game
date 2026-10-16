using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切 Battle/Town 场景时的 Loading：实例化 Prefabs/Loading/LoadingUI。
/// 无预制体时按运行时布局现场建树（不覆盖已有 prefab）。
/// </summary>
public static class BattleLoadingOverlay
{
    static LoadingUI _ui;

    /// <summary>
    /// 【2026-10-16 主人拍板「回城后直接出塔克剧情 不要出现主界面」】
    /// 置 true 时 SceneLoadingCoordinator 的自动 Hide 被按住（Loading 停在满格），
    /// 由回城收尾流程在塔克剧情真正弹出后再放行收掉 —— 主界面全程被 Loading 盖住不闪帧。
    /// 用完必须复位（正常路径在 TownAfterBattleRoutine 收尾；域重载由 ResetStatics 兜底）。
    /// </summary>
    public static bool KeepVisible = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _ui = null;
        KeepVisible = false;
    }

    public static void Show(string tip = null)
    {
        RecreateInstance();
        if (_ui == null) return;
        _ui.gameObject.SetActive(true);
        _ui.transform.SetAsLastSibling();
        _ui.PrepareCanvas();
        _ui.SetTip(string.IsNullOrEmpty(tip) ? "加载中…" : tip);
        _ui.SetProgress(0f);
    }

    public static void SetProgress(float progress01)
    {
        _ui?.SetProgress(progress01);
    }

    public static void SetTip(string tip)
    {
        _ui?.SetTip(tip);
    }

    /// <summary>
    /// 按章节给本次 Loading 换背景图（2026-10-11 主人拍板：每章一张，打哪章显示哪章）。
    /// 必须在 <see cref="Show"/> 之后调 —— Show 每次都会重建实例，图会回到预制体默认那张。
    /// </summary>
    public static void SetChapterBackdrop(int chapter)
    {
        _ui?.SetChapterBackdrop(chapter);
    }

    public static bool IsShowing => _ui != null && _ui.gameObject.activeSelf;

    public static string CurrentTip => _ui != null ? _ui.CurrentTip : "";

    public static void Hide()
    {
        // 【2026-10-16】回城过渡期间被 KeepVisible 按住：Loading 由收尾流程自己收，这里不销毁。
        if (KeepVisible) return;
        if (_ui == null) return;
        Object.Destroy(_ui.gameObject);
        _ui = null;
    }

    static void RecreateInstance()
    {
        if (_ui != null)
        {
            Object.Destroy(_ui.gameObject);
            _ui = null;
        }

        GameObject go = null;
        var prefab = Resources.Load<GameObject>(LoadingUI.ResourcePath);
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            go.name = "LoadingUI";
        }
        else
        {
            Debug.LogWarning("[BattleLoadingOverlay] 未找到 Resources/" + LoadingUI.ResourcePath + "，使用代码默认布局");
            go = new GameObject("LoadingUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var ui = go.AddComponent<LoadingUI>();
            ui.BuildHierarchyForPrefab();
        }

        Object.DontDestroyOnLoad(go);
        _ui = go.GetComponent<LoadingUI>();
        if (_ui == null) _ui = go.AddComponent<LoadingUI>();
        _ui.AutoBind();
    }
}
