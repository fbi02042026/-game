using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇角色页离屏换装预览（给背包 HandRig）。不往 Portrait 挂 RawImage，立绘走静态 Sprite。
/// </summary>
public class TownHeroCostumePreview : MonoBehaviour
{
    const string PrefabPath = "Units/wanjia";
    const int RtW = 256;
    const int RtH = 384;

    public RawImage targetImage;
    public RectTransform host;

    Camera _cam;
    RenderTexture _rt;
    GameObject _heroGo;
    HeroCostumeManager _costume;
    bool _built;

    /// <summary>城镇角色页预览的 CostumeManager（战斗 Hero 不存在时给背包 HandRig 用）。</summary>
    public static HeroCostumeManager ActiveCostumeManager
    {
        get
        {
            if (CharacterUI.Instance == null) return null;
            var p = CharacterUI.Instance.GetComponent<TownHeroCostumePreview>();
            return p != null ? p._costume : null;
        }
    }

    public static TownHeroCostumePreview EnsureOn(CharacterUI ui)
    {
        if (ui == null) return null;
        var existing = ui.GetComponent<TownHeroCostumePreview>();
        if (existing != null)
        {
            existing.BindHost(ui);
            return existing;
        }
        var p = ui.gameObject.AddComponent<TownHeroCostumePreview>();
        p.BindHost(ui);
        return p;
    }

    void BindHost(CharacterUI ui)
    {
        if (ui.portraitImage != null)
            host = ui.portraitImage.rectTransform;
        if (host == null)
        {
            var t = ui.transform.Find("Content/Stage/Portrait");
            if (t != null) host = t as RectTransform;
        }
    }

    /// <summary>离屏构建换装，不盖住 Portrait。</summary>
    public void EnsureOffscreenCostume()
    {
        if (host != null)
        {
            var junk = host.Find("SpumPreview");
            if (junk != null)
                Destroy(junk.gameObject);
            targetImage = null;
            var img = host.GetComponent<Image>();
            if (img != null) img.enabled = true;
        }
        BuildIfNeeded();
        if (_heroGo != null) _heroGo.SetActive(true);
        if (_cam != null) _cam.enabled = false;
        RefreshCostume();
    }

    public void Show() => EnsureOffscreenCostume();

    public void Hide()
    {
        if (_cam != null) _cam.enabled = false;
    }

    public void RefreshCostume()
    {
        if (_costume != null)
            _costume.RefreshCostume();
    }

    void OnEnable()
    {
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.OnCostumeChanged += RefreshCostume;
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.OnBackpackChanged += RefreshCostume;
    }

    void OnDisable()
    {
        if (GridBackpackSystem.Instance != null)
        {
            GridBackpackSystem.Instance.OnCostumeChanged -= RefreshCostume;
            GridBackpackSystem.Instance.OnBackpackChanged -= RefreshCostume;
        }
    }

    void OnDestroy()
    {
        if (_rt != null)
        {
            if (_cam != null) _cam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
        if (_heroGo != null) Destroy(_heroGo);
        if (_cam != null) Destroy(_cam.gameObject);
    }

    void BuildIfNeeded()
    {
        if (_built) return;

        _rt = new RenderTexture(RtW, RtH, 16, RenderTextureFormat.ARGB32);
        _rt.Create();

        var camGo = new GameObject("TownHeroPreviewCam");
        DontDestroyOnLoad(camGo);
        _cam = camGo.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = 1.15f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        _cam.cullingMask = ~0;
        _cam.targetTexture = _rt;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 20f;
        _cam.depth = -80;
        _cam.enabled = false;
        camGo.transform.position = new Vector3(4800f, 4800f, -10f);

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[TownHeroCostumePreview] 未找到 Units/wanjia");
            _built = true;
            return;
        }

        _heroGo = Instantiate(prefab);
        _heroGo.name = "TownHeroPreview";
        DontDestroyOnLoad(_heroGo);
        _heroGo.transform.position = camGo.transform.position + new Vector3(0f, -0.2f, 8f);
        _heroGo.transform.localScale = Vector3.one * 1.2f;
        _heroGo.transform.rotation = Quaternion.identity;

        foreach (var rb in _heroGo.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb.simulated = false;
            rb.velocity = Vector2.zero;
        }
        var hero = _heroGo.GetComponent<Hero>();
        if (hero != null) hero.enabled = false;
        foreach (var ub in _heroGo.GetComponentsInChildren<UnitBase>(true))
            ub.enabled = false;

        _costume = _heroGo.GetComponent<HeroCostumeManager>();
        if (_costume == null) _costume = _heroGo.AddComponent<HeroCostumeManager>();
        if (_costume.spumPrefabs == null)
            _costume.spumPrefabs = _heroGo.GetComponentInChildren<SPUM_Prefabs>(true);
        if (_costume.spriteList == null)
            _costume.spriteList = _heroGo.GetComponentInChildren<SPUM_SpriteList>(true);
        if (HeroCostumeManager.Instance == _costume)
            HeroCostumeManager.Instance = null;

        int layer = LayerMask.NameToLayer("Default");
        SetLayerRecursive(_heroGo, layer);

        _built = true;
        RefreshCostume();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }
}
