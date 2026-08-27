using UnityEngine;

/// <summary>
/// 视差无限地图（v18）
/// - 前/中/后各自复制多片，奇数片左右镜像，接缝对齐不重复
/// - 步长 = rect 宽 × localScale.x（漏乘缩放是之前空一段地图的根因）
/// - 强制 pivot 居中：pivot 居中时负 scale 不改变占位，不会露缝
/// - 相邻片留 2px 交叠，吃掉亚像素缝
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("三层配置（节点名 3=远景 2=中景 1=近景）")]
    public ParallaxLayer backLayer = new ParallaxLayer { name = "Back", parallaxFactor = 0.1f, autoFindByName = "3" };
    public ParallaxLayer midLayer = new ParallaxLayer { name = "Mid", parallaxFactor = 0.3f, autoFindByName = "2" };
    public ParallaxLayer frontLayer = new ParallaxLayer { name = "Front", parallaxFactor = 0.6f, autoFindByName = "1" };

    [Header("背景注册表")]
    public BattleBackgroundRegistry backgroundRegistry;

    const int TileCount = 6;
    /// <summary>相邻片交叠像素，避免浮点误差露出竖缝。</summary>
    const float SeamOverlap = 2f;

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

        Debug.Log($"[Parallax v18] root={LayerSearchRoot.name} ppu={_ppu:F1} tiles={TileCount} " +
                  $"w={frontLayer.width:F0} scale={frontLayer.baseScaleX:F2} step={EffectiveStep(frontLayer):F0}");
    }

    void RefreshLayerWidth(ref ParallaxLayer layer)
    {
        if (layer.tiles == null || layer.tiles.Length == 0 || layer.tiles[0] == null) return;
        layer.width = MeasureTileWidth(layer.tiles[0], layer.images != null ? layer.images[0] : null);
        // 只在测量彻底失败时兜底；之前 <720 就强塞 2020，窄图会被撑出一大段空白
        if (layer.width < 8f) layer.width = 2020f;
        PlaceLayerTiles(ref layer, 0f);
    }

    static float MeasureTileWidth(RectTransform rt, UnityEngine.UI.Image img)
    {
        if (rt == null) return 2020f;
        float w = rt.rect.width;
        if (w < 1f) w = Mathf.Abs(rt.sizeDelta.x);
        if (img != null && img.sprite != null)
        {
            float sw = img.sprite.rect.width;
            // UI Image 常见：sizeDelta 就是设计宽
            if (sw > 1f && (w < 1f || w < sw * 0.5f))
                w = sw;
        }
        return w > 1f ? w : 2020f;
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
                Object.DestroyImmediate(all[i].gameObject);
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

        // pivot 必须居中：居中时 ±scale 占位完全一致，镜像才不会错位露缝
        layer.baseScaleX = Mathf.Abs(src.localScale.x) < 0.01f ? 1f : Mathf.Abs(src.localScale.x);
        src.localScale = new Vector3(layer.baseScaleX, src.localScale.y, src.localScale.z);

        // 改 pivot / 锚点会让图跳位，先记下世界坐标改完再放回去
        Vector3 worldBefore = src.position;
        src.pivot = new Vector2(0.5f, src.pivot.y);
        // 拉伸锚点下 rect.width 由父级决定，测量会漂；锁成中心锚点
        if (Mathf.Abs(src.anchorMax.x - src.anchorMin.x) > 0.001f)
        {
            float mid = (src.anchorMin.x + src.anchorMax.x) * 0.5f;
            float w = src.rect.width;
            src.anchorMin = new Vector2(mid, src.anchorMin.y);
            src.anchorMax = new Vector2(mid, src.anchorMax.y);
            src.sizeDelta = new Vector2(w, src.sizeDelta.y);
        }
        src.position = worldBefore;

        layer.initialX = src.anchoredPosition.x;
        layer.width = MeasureTileWidth(src, src.GetComponent<UnityEngine.UI.Image>());

        layer.tiles = new RectTransform[TileCount];
        layer.images = new UnityEngine.UI.Image[TileCount];
        layer.tiles[0] = src;
        layer.images[0] = src.GetComponent<UnityEngine.UI.Image>();

        for (int i = 1; i < TileCount; i++)
        {
            GameObject tile = Object.Instantiate(src.gameObject, src.parent);
            tile.name = layer.name + "_tile" + i;
            var extras = tile.GetComponents<ParallaxBackground>();
            for (int e = 0; e < extras.Length; e++) Object.Destroy(extras[e]);
            layer.tiles[i] = tile.GetComponent<RectTransform>();
            layer.images[i] = layer.tiles[i].GetComponent<UnityEngine.UI.Image>();
            if (layer.tiles[i] != null)
            {
                layer.tiles[i].pivot = new Vector2(0.5f, layer.tiles[i].pivot.y);
                layer.tiles[i].anchorMin = src.anchorMin;
                layer.tiles[i].anchorMax = src.anchorMax;
                layer.tiles[i].sizeDelta = src.sizeDelta;
                layer.tiles[i].localScale = new Vector3(layer.baseScaleX,
                    layer.tiles[i].localScale.y, 1f);
                layer.tiles[i].gameObject.SetActive(true);
            }
        }

        PlaceLayerTiles(ref layer, 0f);
    }

    /// <summary>实际占屏宽度：rect 宽必须乘上节点自身缩放，否则步长偏小会重叠、偏大会空一段。</summary>
    static float EffectiveStep(ParallaxLayer layer)
    {
        float scale = Mathf.Abs(layer.baseScaleX) < 0.01f ? 1f : Mathf.Abs(layer.baseScaleX);
        float step = layer.width * scale - SeamOverlap;
        return step > 1f ? step : 1f;
    }

    void PlaceLayerTiles(ref ParallaxLayer layer, float offset)
    {
        if (layer.tiles == null) return;
        if (layer.width < 1f) return;

        float step = EffectiveStep(layer);
        float period = step * TileCount;
        float origin = layer.initialX;
        float windowStart = origin - step * (TileCount / 2);

        for (int i = 0; i < layer.tiles.Length; i++)
        {
            var rt = layer.tiles[i];
            if (rt == null) continue;

            float x = origin + i * step - offset;
            x = Mathf.Repeat(x - windowStart, period) + windowStart;

            Vector2 ap = rt.anchoredPosition;
            ap.x = x;
            rt.anchoredPosition = ap;

            // 镜像按世界槽位奇偶；用 Floor 避免 Round 在接缝处抖动成同向
            int slot = Mathf.FloorToInt((x - origin) / step + 0.0001f);
            bool mirror = (slot & 1) == 1;

            Vector3 s = rt.localScale;
            float sx = Mathf.Abs(layer.baseScaleX) < 0.01f ? 1f : Mathf.Abs(layer.baseScaleX);
            s.x = mirror ? -sx : sx;
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
            backgroundRegistry = Resources.Load<BattleBackgroundRegistry>(ContentPaths.Config.BattleBackgrounds);
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
        RefreshLayerWidth(ref frontLayer);
        RefreshLayerWidth(ref midLayer);
        RefreshLayerWidth(ref backLayer);
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
        PlaceLayerTiles(ref layer, offset);
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
