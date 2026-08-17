using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切 Battle/Town 场景时的 Loading：实例化 Prefabs/Loading/LoadingUI。
/// 无预制体时按运行时布局现场建树（不覆盖已有 prefab）。
/// </summary>
public static class BattleLoadingOverlay
{
    static LoadingUI _ui;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _ui = null;
    }

    public static void Show(string tip = null)
    {
        RecreateInstance();
        if (_ui == null) return;
        _ui.gameObject.SetActive(true);
        _ui.transform.SetAsLastSibling();
        _ui.PrepareOverlay();
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

    public static bool IsShowing => _ui != null && _ui.gameObject.activeSelf;

    public static void Hide()
    {
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
