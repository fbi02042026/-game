using UnityEngine;

/// <summary>
/// 视差滚动背景（v14）
/// - 挂在 BattleUI/map 上；只移动 map[/main] 下的 1/2/3 层
/// - 无缝：复制一层后 scale.x=-1 拼接；主片 +1、衔接片 -1 交替
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("三层配置（节点名 3=远景 2=中景 1=近景）")]
    public ParallaxLayer backLayer = new ParallaxLayer { name = "Back", parallaxFactor = 0.1f, autoFindByName = "3" };
    public ParallaxLayer midLayer = new ParallaxLayer { name = "Mid", parallaxFactor = 0.3f, autoFindByName = "2" };
    public ParallaxLayer frontLayer = new ParallaxLayer { name = "Front", parallaxFactor = 0.6f, autoFindByName = "1" };

    [Header("背景注册表")]
    public BattleBackgroundRegistry backgroundRegistry;

    [System.Serializable]
    public class ParallaxLayer
    {
        public string name;
        [Range(0f, 1f)] public float parallaxFactor;
        public string autoFindByName;
        [System.NonSerialized] public RectTransform rt;
        [System.NonSerialized] public RectTransform mirrorRT;
        [System.NonSerialized] public float width;
        [System.NonSerialized] public float initialX;
        [System.NonSerialized] public float baseScaleX = 1f;
        [System.NonSerialized] public UnityEngine.UI.Image layerImage;
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

        // 固定设计 PPU，避免 Canvas/ortho 波动导致视差突然飙车
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

        Debug.Log($"[Parallax v14] root={LayerSearchRoot.name} ppu={_ppu:F1} flipTile=+1/-1 w={frontLayer.width:F0}");
    }

    static void RefreshLayerWidth(ref ParallaxLayer layer)
    {
        if (layer.rt == null) return;
        float w = layer.rt.rect.width;
        if (w > 1f) layer.width = w;
        else if (layer.width < 1f) layer.width = 2020f;
        ApplyTilePair(ref layer, layer.rt.anchoredPosition.x);
    }

    void ClearOldTiles()
    {
        Transform root = LayerSearchRoot;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.EndsWith("_tile"))
                Destroy(all[i].gameObject);
        }
    }

    void InitLayer(ref ParallaxLayer layer)
    {
        Transform t = FindDirectOrNested(LayerSearchRoot, layer.autoFindByName);
        if (t == null) return;
        layer.rt = t as RectTransform;
        if (layer.rt == null) return;

        layer.layerImage = layer.rt.GetComponent<UnityEngine.UI.Image>();
        layer.initialX = layer.rt.anchoredPosition.x;
        layer.width = layer.rt.rect.width > 1 ? layer.rt.rect.width : 2020f;
        layer.baseScaleX = Mathf.Abs(layer.rt.localScale.x) < 0.01f ? 1f : Mathf.Abs(layer.rt.localScale.x);

        // 主片 +1
        Vector3 mainScale = layer.rt.localScale;
        mainScale.x = layer.baseScaleX;
        layer.rt.localScale = mainScale;

        // 复制衔接片：x scale = -1，贴右侧
        GameObject tile = Instantiate(layer.rt.gameObject, layer.rt.parent);
        tile.name = layer.name + "_tile";
        var extras = tile.GetComponents<ParallaxBackground>();
        for (int i = 0; i < extras.Length; i++) Destroy(extras[i]);

        layer.mirrorRT = tile.GetComponent<RectTransform>();
        Vector3 flipScale = layer.mirrorRT.localScale;
        flipScale.x = -layer.baseScaleX;
        layer.mirrorRT.localScale = flipScale;
        layer.mirrorRT.anchoredPosition = new Vector2(layer.initialX + layer.width, layer.rt.anchoredPosition.y);
    }

    /// <summary>主片 scale=+1，衔接片 scale=-1，位置差一个宽度</summary>
    static void ApplyTilePair(ref ParallaxLayer layer, float baseX)
    {
        if (layer.rt == null) return;

        Vector2 ap = layer.rt.anchoredPosition;
        ap.x = baseX;
        layer.rt.anchoredPosition = ap;
        Vector3 ms = layer.rt.localScale;
        ms.x = layer.baseScaleX;
        layer.rt.localScale = ms;

        if (layer.mirrorRT != null)
        {
            ap = layer.mirrorRT.anchoredPosition;
            ap.x = baseX + layer.width;
            layer.mirrorRT.anchoredPosition = ap;
            Vector3 fs = layer.mirrorRT.localScale;
            fs.x = -layer.baseScaleX;
            layer.mirrorRT.localScale = fs;
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
        if (frontLayer.rt == null && midLayer.rt == null && backLayer.rt == null)
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
        if (layer.rt == null) return;
        if (layer.layerImage == null)
            layer.layerImage = layer.rt.GetComponent<UnityEngine.UI.Image>();

        ApplySprite(layer.layerImage, layer.rt.gameObject, sprite, layerName);
        if (layer.mirrorRT != null)
        {
            var img = layer.mirrorRT.GetComponent<UnityEngine.UI.Image>();
            ApplySprite(img, layer.mirrorRT.gameObject, sprite, layerName + "_tile");
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
        if (layer.rt == null || layer.width <= 0.01f) return;

        float offset = heroDelta * layer.parallaxFactor * _ppu;
        float w = layer.width;
        float raw = layer.initialX - offset;
        // 取模一个宽度：主片(+1) 与 衔接片(-1) 始终首尾相接；再往后循环又是 +1
        float baseX = layer.initialX - Mathf.Repeat(layer.initialX - raw, w);
        ApplyTilePair(ref layer, baseX);
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
