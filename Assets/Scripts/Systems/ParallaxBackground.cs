using UnityEngine;

/// <summary>
/// 视差无限地图（v16）
/// - 只复制 map 下的 1/2/3 层
/// - 每层 4 片：scale.x 正反反正 +1/-1/-1/+1
/// - 按镜头/英雄位移取模循环，始终盖住视野，避免尽头蓝屏
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("三层配置（节点名 3=远景 2=中景 1=近景）")]
    public ParallaxLayer backLayer = new ParallaxLayer { name = "Back", parallaxFactor = 0.1f, autoFindByName = "3" };
    public ParallaxLayer midLayer = new ParallaxLayer { name = "Mid", parallaxFactor = 0.3f, autoFindByName = "2" };
    public ParallaxLayer frontLayer = new ParallaxLayer { name = "Front", parallaxFactor = 0.6f, autoFindByName = "1" };

    [Header("背景注册表")]
    public BattleBackgroundRegistry backgroundRegistry;

    static readonly float[] FlipSigns = { 1f, -1f, -1f, 1f };
    const int TileCount = 4;

    [System.Serializable]
    public class ParallaxLayer
    {
        public string name;
        [Range(0f, 1f)] public float parallaxFactor;
        public string autoFindByName;
        [System.NonSerialized] public RectTransform[] tiles;
        [System.NonSerialized] public float width;
        [System.NonSerialized] public float initialX;
        [System.NonSerialized] public float baseScaleX = 1f;
        [System.NonSerialized] public UnityEngine.UI.Image[] images;
    }

    private Transform _layerRoot;
    private Camera _cam;
    private float _initialHeroX;
    private float _ppu = 100f;
    private bool _ready;
    private int _pendingChapter = -1;
    private bool _layersInited;

    public void SetLayerRoot(Transform root)
    {
        if (root == null) return;
        if (_layerRoot == root) return;
        _layerRoot = root;
        if (_layersInited)
        {
            _layersInited = false;
            EnsureLayers();
        }
    }

    void Start()
    {
        _cam = Camera.main;
        EnsureLayers();
        TryBindHeroOrigin();

        if (_pendingChapter > 0)
        {
            int ch = _pendingChapter;
            _pendingChapter = -1;
            SwitchBackground(ch);
        }
    }

    Transform LayerSearchRoot => _layerRoot != null ? _layerRoot : transform;

    void EnsureLayers()
    {
        if (_layersInited) return;

        _ppu = GameConfig.PIXEL_PER_UNIT;
        if (_ppu < 1f) _ppu = 100f;

        ClearOldTiles();
        InitLayer(ref backLayer);
        InitLayer(ref midLayer);
        InitLayer(ref frontLayer);
        _layersInited = true;

        Canvas.ForceUpdateCanvases();
        RefreshLayerWidth(ref backLayer);
        RefreshLayerWidth(ref midLayer);
        RefreshLayerWidth(ref frontLayer);

        Debug.Log($"[Parallax v16] root={LayerSearchRoot.name} ppu={_ppu:F1} flip=+1/-1/-1/+1 tiles={TileCount} w={frontLayer.width:F0}");
    }

    void RefreshLayerWidth(ref ParallaxLayer layer)
    {
        if (layer.tiles == null || layer.tiles.Length == 0 || layer.tiles[0] == null) return;
        float w = layer.tiles[0].rect.width;
        if (w > 1f) layer.width = w;
        else if (layer.width < 1f) layer.width = 2020f;
        // 宽度至少盖住约 1.2 个设计屏宽，避免拼接缝露蓝
        if (layer.width < 720f) layer.width = 2020f;
        ApplyTiles(ref layer, layer.tiles[0].anchoredPosition.x);
    }

    void ClearOldTiles()
    {
        Transform root = LayerSearchRoot;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string n = all[i].name;
            if (n.Contains("_tile"))
                Destroy(all[i].gameObject);
        }
    }

    void InitLayer(ref ParallaxLayer layer)
    {
        Transform t = FindDirectOrNested(LayerSearchRoot, layer.autoFindByName);
        if (t == null)
        {
            Debug.LogWarning($"[Parallax] 未找到层节点 '{layer.autoFindByName}' under {LayerSearchRoot.name}");
            return;
        }
        var src = t as RectTransform;
        if (src == null) return;

        layer.initialX = src.anchoredPosition.x;
        layer.width = src.rect.width > 1f ? src.rect.width : 2020f;
        layer.baseScaleX = Mathf.Abs(src.localScale.x) < 0.01f ? 1f : Mathf.Abs(src.localScale.x);

        layer.tiles = new RectTransform[TileCount];
        layer.images = new UnityEngine.UI.Image[TileCount];
        layer.tiles[0] = src;
        layer.images[0] = src.GetComponent<UnityEngine.UI.Image>();

        for (int i = 1; i < TileCount; i++)
        {
            GameObject tile = Instantiate(src.gameObject, src.parent);
            tile.name = layer.name + "_tile" + i;
            var extras = tile.GetComponents<ParallaxBackground>();
            for (int e = 0; e < extras.Length; e++) Destroy(extras[e]);
            layer.tiles[i] = tile.GetComponent<RectTransform>();
            layer.images[i] = layer.tiles[i].GetComponent<UnityEngine.UI.Image>();
            if (layer.tiles[i] != null)
                layer.tiles[i].gameObject.SetActive(true);
        }

        ApplyTiles(ref layer, layer.initialX);
    }

    static void ApplyTiles(ref ParallaxLayer layer, float baseX)
    {
        if (layer.tiles == null) return;
        float w = layer.width;
        for (int i = 0; i < layer.tiles.Length; i++)
        {
            var rt = layer.tiles[i];
            if (rt == null) continue;

            Vector2 ap = rt.anchoredPosition;
            ap.x = baseX + i * w;
            rt.anchoredPosition = ap;

            Vector3 s = rt.localScale;
            s.x = FlipSigns[i] * layer.baseScaleX;
            rt.localScale = s;

            if (!rt.gameObject.activeSelf)
                rt.gameObject.SetActive(true);
        }
    }

    public void ResetHeroOrigin()
    {
        EnsureLayers();
        if (Hero.Instance == null) return;
        _initialHeroX = Hero.Instance.transform.position.x;
        _ready = true;
        MoveLayer(ref backLayer, 0f);
        MoveLayer(ref midLayer, 0f);
        MoveLayer(ref frontLayer, 0f);
    }

    void TryBindHeroOrigin()
    {
        if (Hero.Instance != null)
        {
            _initialHeroX = Hero.Instance.transform.position.x;
            _ready = true;
        }
        else _ready = false;
    }

    public void SwitchBackground(int chapter)
    {
        EnsureLayers();
        if ((frontLayer.tiles == null || frontLayer.tiles[0] == null) &&
            (midLayer.tiles == null || midLayer.tiles[0] == null) &&
            (backLayer.tiles == null || backLayer.tiles[0] == null))
        {
            _pendingChapter = chapter;
            return;
        }

        if (backgroundRegistry == null)
            backgroundRegistry = Resources.Load<BattleBackgroundRegistry>("Config/BattleBackgroundRegistry");
        if (backgroundRegistry == null)
        {
            Debug.LogWarning("[Parallax] BattleBackgroundRegistry未找到");
            return;
        }

        var bg = backgroundRegistry.GetBackground(chapter);
        if (bg == null)
        {
            Debug.LogWarning($"[Parallax] 章节{chapter}无背景配置");
            return;
        }

        SetLayerSprite(ref frontLayer, bg.frontSprite, "Front");
        SetLayerSprite(ref midLayer, bg.midSprite, "Mid");
        SetLayerSprite(ref backLayer, bg.backSprite, "Back");
    }

    void SetLayerSprite(ref ParallaxLayer layer, Sprite sprite, string layerName)
    {
        if (layer.tiles == null) return;
        for (int i = 0; i < layer.tiles.Length; i++)
        {
            if (layer.tiles[i] == null) continue;
            if (layer.images == null || layer.images[i] == null)
            {
                if (layer.images == null) layer.images = new UnityEngine.UI.Image[layer.tiles.Length];
                layer.images[i] = layer.tiles[i].GetComponent<UnityEngine.UI.Image>();
            }
            ApplySprite(layer.images[i], layer.tiles[i].gameObject, sprite, layerName + (i == 0 ? "" : "_tile" + i));
        }
    }

    void ApplySprite(UnityEngine.UI.Image img, GameObject go, Sprite sprite, string tag)
    {
        if (img == null) return;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.enabled = true;
            go.SetActive(true);
        }
        else
            Debug.LogWarning($"[Parallax] {tag} 精灵缺失，保留旧图");
    }

    void LateUpdate()
    {
        if (!_layersInited) EnsureLayers();
        if (!_ready)
        {
            TryBindHeroOrigin();
            if (!_ready) return;
        }
        if (Hero.Instance == null) return;

        float heroDelta = Hero.Instance.transform.position.x - _initialHeroX;
        MoveLayer(ref backLayer, heroDelta);
        MoveLayer(ref midLayer, heroDelta);
        MoveLayer(ref frontLayer, heroDelta);
    }

    void MoveLayer(ref ParallaxLayer layer, float heroDelta)
    {
        if (layer.tiles == null || layer.tiles[0] == null || layer.width <= 0.01f) return;

        float offset = heroDelta * layer.parallaxFactor * _ppu;
        float period = layer.width * TileCount;
        if (period < 1f) period = layer.width;
        float raw = layer.initialX - offset;
        // 周期内回绕：正反反正完整循环后再接，不会“跳回开头”
        float wrapped = Mathf.Repeat(layer.initialX - raw, period);
        float baseX = layer.initialX - wrapped;
        ApplyTiles(ref layer, baseX);
    }

    static Transform FindDirectOrNested(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.gameObject.name == name) return c;
        }
        return FindChildRecursive(parent, name);
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.name == name) return child;
            var r = FindChildRecursive(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
