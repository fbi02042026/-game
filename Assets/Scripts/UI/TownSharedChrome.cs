using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Town 共用壳：资源条、底部五入口一律从主界面（或共享预制体）复制/复用，禁止各页自建一套。
/// </summary>
public static class TownSharedChrome
{
    public const string ResourceBarPrefabPath = "Prefabs/Town/ResourceBar";
    public const string BottomNavPrefabPath = "Prefabs/Town/MainBottomNav";

    static Transform _cachedHallRoot;
    static Transform _cachedTop;
    static Transform _cachedBottom;
    static bool _topFontsDone;
    static bool _bottomFontsDone;

    public static void InvalidateCache()
    {
        _cachedHallRoot = null;
        _cachedTop = null;
        _cachedBottom = null;
        _topFontsDone = false;
        _bottomFontsDone = false;
        TavernUI.ClearGuildHideCache();
    }

    static void EnsureChromeCache(Transform hallRoot)
    {
        if (hallRoot == _cachedHallRoot && _cachedTop != null) return;
        _cachedHallRoot = hallRoot;
        _cachedTop = FindDeep(hallRoot, "TopBar");
        _cachedBottom = FindOuterBottomNav(hallRoot);
        _topFontsDone = false;
        _bottomFontsDone = false;
    }

    /// <summary>
    /// 把主界面 TopBar（金币/体力）与 BottomNav 提到最前，盖在当前功能页之上。
    /// 同源节点，不另造资源条。字体只 Apply 一次，切页不再扫整树。
    /// </summary>
    public static void RaiseSharedChrome(Transform hallRoot)
    {
        if (hallRoot == null) return;
        EnsureChromeCache(hallRoot);

        if (_cachedTop != null)
        {
            if (!_cachedTop.gameObject.activeSelf)
                _cachedTop.gameObject.SetActive(true);
            _cachedTop.SetAsLastSibling();
            if (!_topFontsDone)
            {
                GameFonts.ApplyToHierarchy(_cachedTop);
                _topFontsDone = true;
            }
        }

        if (_cachedBottom != null)
        {
            if (!_cachedBottom.gameObject.activeSelf)
                _cachedBottom.gameObject.SetActive(true);
            _cachedBottom.SetAsLastSibling();
            if (!_bottomFontsDone)
            {
                GameFonts.ApplyToHierarchy(_cachedBottom);
                _bottomFontsDone = true;
            }
            var nav = _cachedBottom.GetComponent<MainBottomNav>()
                      ?? _cachedBottom.GetComponentInChildren<MainBottomNav>(true);
            if (nav == null)
                nav = _cachedBottom.gameObject.AddComponent<MainBottomNav>();
        }
    }

    /// <summary>
    /// 在独立界面根下确保有资源条：优先实例化 ResourceBar 预制体，否则从 sourceRoot 克隆 TopBar。
    /// </summary>
    public static Transform EnsureResourceBar(Transform host, Transform sourceRoot)
    {
        if (host == null) return null;

        Transform existing = host.Find("TopBar") ?? FindDeep(host, "TopBar") ?? FindDeep(host, "SharedResourceBar");
        if (existing != null)
        {
            GameFonts.ApplyToHierarchy(existing);
            return existing;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourceBarPrefabPath);
        if (prefab != null)
        {
            GameObject go = Object.Instantiate(prefab, host, false);
            go.name = "TopBar";
            StretchTop(go.GetComponent<RectTransform>());
            GameFonts.ApplyToHierarchy(go.transform);
            return go.transform;
        }

        Transform src = sourceRoot != null ? (FindDeep(sourceRoot, "TopBar")) : null;
        if (src == null && GuildHallUI.Instance != null)
            src = FindDeep(GuildHallUI.Instance.transform, "TopBar");
        if (src == null) return null;

        GameObject clone = Object.Instantiate(src.gameObject, host, false);
        clone.name = "TopBar";
        clone.SetActive(true);
        GameFonts.ApplyToHierarchy(clone.transform);
        return clone.transform;
    }

    /// <summary>
    /// 在独立界面根下确保有底部五入口：优先 MainBottomNav 预制体，否则从主界面克隆。
    /// </summary>
    public static MainBottomNav EnsureBottomNav(Transform host, Transform sourceRoot, MainNavTab selected)
    {
        if (host == null) return null;

        var existing = host.GetComponentInChildren<MainBottomNav>(true);
        if (existing != null)
        {
            existing.Initialize(selected);
            GameFonts.ApplyToHierarchy(existing.transform);
            return existing;
        }

        GameObject prefab = Resources.Load<GameObject>(BottomNavPrefabPath);
        Transform src = null;
        if (prefab == null)
        {
            src = sourceRoot != null ? FindOuterBottomNav(sourceRoot) : null;
            if (src == null && GuildHallUI.Instance != null)
                src = FindOuterBottomNav(GuildHallUI.Instance.transform);
        }

        GameObject go;
        if (prefab != null)
            go = Object.Instantiate(prefab, host, false);
        else if (src != null)
            go = Object.Instantiate(src.gameObject, host, false);
        else
            return null;

        go.name = "BottomNav";
        go.SetActive(true);
        var nav = go.GetComponent<MainBottomNav>() ?? go.GetComponentInChildren<MainBottomNav>(true);
        if (nav == null) nav = go.AddComponent<MainBottomNav>();
        nav.Initialize(selected);
        GameFonts.ApplyToHierarchy(go.transform);
        return nav;
    }

    static void StretchTop(RectTransform rt)
    {
        if (rt == null) return;
        // 保持主界面 TopBar 的锚点习惯：贴顶
        if (rt.anchorMax.y < 0.9f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 120f);
        }
    }

    public static Transform FindOuterBottomNav(Transform root)
    {
        if (root == null) return null;
        // 优先带 MainBottomNav 的节点
        var nav = root.GetComponentInChildren<MainBottomNav>(true);
        if (nav != null)
        {
            Transform t = nav.transform;
            // 若组件挂在内层 BottomNav，外层也可能叫 BottomNav
            if (t.parent != null && t.parent.name == "BottomNav")
                return t.parent;
            return t;
        }
        return FindDeep(root, "BottomNav");
    }

    public static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
