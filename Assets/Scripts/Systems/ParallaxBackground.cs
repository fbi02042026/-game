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

    [Header("背景注册表（2026-10-27 起仅留档，换图走路径加载，见 ChapterBackgroundPaths）")]
    public BattleBackgroundRegistry backgroundRegistry;

    // 【2026-10-27 主人拍板「改」】战斗背景改路径加载新图（56字符guid拖不进prefab/registry，按10-23口径全走Resources.Load）。
    // 路径相对任意 Resources/ 目录，不带扩展名；下标=章节号（0空占位）。
    // 目前新图只有 1森林/2墓园/4草原 三张；3/5-8 缺图 → LogError 可见，不兜底不静默。
    static readonly string[] ChapterBackgroundPaths = new string[]
    {
        "",                                                    // 0 占位
        "UI/Art/background/新战斗背景/1森林",                   // 第1章
        "UI/Art/background/新战斗背景/2墓园",                   // 第2章
        "",                                                    // 第3章 暂无新图
        "UI/Art/background/新战斗背景/4草原",                   // 第4章
        "", "", "", "", "",                                    // 第5-8章 暂无新图
    };

    [Header("2026-10-26 主人拍板：中景2/远景3先隐藏，战斗背景只用近景1（拼接单元）")]
    /// <summary>true=整层不建、源节点直接关掉；要恢复中远景改回 false 即可。</summary>
    public bool hideMidBackLayers = true;

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
        /// <summary>拼接单元（本体+镜像子图）＝自带镜像，整片不许再按奇偶翻转（翻转会产生空洞）。</summary>
        [System.NonSerialized] public bool compositeUnit;
    }

    private Transform _layerRoot;
    private Camera _cam;
    private float _initialHeroX;
    private float _ppu = 100f;
    private bool _ready;
    private int _pendingChapter = -1;
    private bool _layersInited;
    Vector3 _layerRootBaseScale = Vector3.one;
    bool _baseScaleCaptured;
    // 【2026-10-27 主人拍板：map 按预制体大小显示（map 节点 scale 1.13、图 2020×768），运行时不改缩放】
    // 原视口缩放 _viewportZoomMul 与长屏 Y 补缝 _verticalCoverMul 已停用（ApplyViewportZoom /
    // ApplyVerticalCoverToBand 均改为 no-op），层根只保留击杀镜头的临时缩放。
    float _killCamZoomMul = 1f;

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

        // 层就绪后应用击杀镜头缩放（视口缩放已于 2026-10-27 停用，见 ApplyCombinedScale）
        ApplyCombinedScale();

        Debug.Log($"[Parallax v18] root={LayerSearchRoot.name} ppu={_ppu:F1} tiles={TileCount} " +
                  $"w={frontLayer.width:F0} scale={frontLayer.baseScaleX:F2} step={EffectiveStep(frontLayer):F0}");
    }

    void RefreshLayerWidth(ref ParallaxLayer layer)
    {
        if (layer.tiles == null || layer.tiles.Length == 0 || layer.tiles[0] == null) return;
        // 2026-10-26：map「1」节点是「本体图+镜像子图」拼接单元（子图挂在 rect 外侧），
        // 宽度必须量整个拼接单元，否则步长减半、每片叠掉半张图
        layer.width = MeasureCompositeWidth(layer.tiles[0]);
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

    /// <summary>
    /// 拼接单元总宽：取自身 + 全部子孙 RectTransform 四角在本节点本地空间下的 X 范围并集。
    /// 2026-10-26 主人把 map 的「1」拼成「本体图 + 镜像子图」（子图锚在本体 rect 右外侧、
    /// scale.x=-1），整单元才是完整重复片；只量本体 rect.width 会把步长算成一半。
    /// 测不出来（<1）时回退旧的单片量法。
    /// </summary>
    static float MeasureCompositeWidth(RectTransform root)
    {
        if (root == null) return 2020f;
        float minX = float.MaxValue, maxX = float.MinValue;
        var all = root.GetComponentsInChildren<RectTransform>(true);
        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            t.GetWorldCorners(corners);
            for (int k = 0; k < 4; k++)
            {
                float lx = root.InverseTransformPoint(corners[k]).x;
                if (lx < minX) minX = lx;
                if (lx > maxX) maxX = lx;
            }
        }
        float w = maxX - minX;
        if (w > 1f) return w;
        return MeasureTileWidth(root, root.GetComponent<UnityEngine.UI.Image>());
    }

    /// <summary>
    /// 拼接单元判定：自身之外还有挂 Image 的子节点（本体+镜像子图结构，2026-10-26 主人改版）。
    /// 拼接单元自带镜像，PlaceLayerTiles 不再做旧版奇偶整片翻转。
    /// </summary>
    static bool HasNestedImage(RectTransform root)
    {
        if (root == null) return false;
        var imgs = root.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] != null && imgs[i].transform != root) return true;
        }
        return false;
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
        // 2026-10-26 主人拍板：中景2/远景3先隐藏只用近景1——整层不克隆，源节点直接关掉。
        // tiles 保持 null，后面的 MoveLayer/PlaceLayerTiles/SwitchBackground 对该层全部自动跳过。
        if (hideMidBackLayers && !ReferenceEquals(layer, frontLayer))
        {
            Transform hide = FindDirectOrNested(LayerSearchRoot, layer.autoFindByName);
            if (hide != null) hide.gameObject.SetActive(false);
            return;
        }

        Transform t = FindDirectOrNested(LayerSearchRoot, layer.autoFindByName);
        if (t == null)
        {
            Debug.LogWarning($"[Parallax] 未找到层节点 '{layer.autoFindByName}' under {LayerSearchRoot.name}");
            return;
        }
        // 视差只许挪背景片；若误命中战斗单位节点则跳过，避免主角移动拖走全场
        if (t.GetComponent<UnitBase>() != null || t.GetComponentInParent<UnitBase>() != null)
        {
            Debug.LogWarning($"[Parallax] 层节点 '{layer.autoFindByName}' 命中战斗单位，跳过");
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
        // 拼接单元（本体+镜像子图）整宽；子图挂在 rect 外侧，只量 rect.width 会步长减半
        layer.width = MeasureCompositeWidth(src);
        // 是否拼接单元：决定 PlaceLayerTiles 还要不要做旧版奇偶整片翻转
        layer.compositeUnit = HasNestedImage(src);

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

            // 镜像按世界槽位奇偶；用 Floor 避免 Round 在接缝处抖动成同向。
            // 【2026-10-27 主人报「战斗地图还是丢失」】拼接单元（本体+镜像子图）绕 pivot 不对称：
            // 本体占 -1024..+1024、镜像占 +1024..+3072。旧逻辑按槽位奇偶整片翻转（单图条带时代的手法），
            // 奇数片翻完占位变 -3072..+1024，片与片之间留下 2048 宽空洞 → 屏幕上「左半有图右半全白」。
            // 拼接单元自带镜像（本体|镜像 已是完整重复片），整片不许再翻；仅无子图的单图条带保留旧翻转。
            bool mirror = !layer.compositeUnit &&
                          (Mathf.FloorToInt((x - origin) / step + 0.0001f) & 1) == 1;

            Vector3 s = rt.localScale;
            float sx = Mathf.Abs(layer.baseScaleX) < 0.01f ? 1f : Mathf.Abs(layer.baseScaleX);
            s.x = mirror ? -sx : sx;
            rt.localScale = s;

            if (!rt.gameObject.activeSelf)
                rt.gameObject.SetActive(true);
        }
    }

    /// <summary>前景层（map 的「1」节点）视差系数，供传送门等「贴层联动」演出读取。</summary>
    public float FrontParallaxFactor => frontLayer.parallaxFactor;

    /// <summary>
    /// 前景层（map 的「1」节点）当前的连续滚动偏移（像素；值越大 = 层在屏幕上越往左）。
    /// 与 MoveLayer 完全同源（同一 heroDelta / 系数 / ppu），传送门贴层联动用：
    /// 开门时记一份快照，之后每帧取差值同步挪门，即可与「1」层保持同速。
    /// </summary>
    public float FrontLayerOffsetPx
    {
        get
        {
            if (!_layersInited) EnsureLayers();
            if (!_ready) TryBindHeroOrigin();
            if (!_ready || Hero.Instance == null) return 0f;
            return (Hero.Instance.transform.position.x - _initialHeroX) * frontLayer.parallaxFactor * _ppu;
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

        // 【2026-10-27 主人拍板「改」】按路径加载新战斗背景；旧 registry 引用全部悬空（56字符guid），
        // 不再走注册表（backgroundRegistry 仅留档）。缺图 = LogError 可见，保留旧图不静默兜底。
        Sprite chapterSprite = LoadChapterBackgroundSprite(chapter);
        if (chapterSprite == null)
        {
            Debug.LogError($"[Parallax] 章节{chapter}新战斗背景缺失（ChapterBackgroundPaths 未配置或图不在 Resources 下），保留旧图");
            return;
        }

        SetLayerSprite(ref frontLayer, chapterSprite, "Front");
        SetLayerSprite(ref midLayer, null, "Mid");
        SetLayerSprite(ref backLayer, null, "Back");
        RefreshLayerWidth(ref frontLayer);
        RefreshLayerWidth(ref midLayer);
        RefreshLayerWidth(ref backLayer);
    }

    /// <summary>
    /// 按章节号从 Resources 路径加载新战斗背景精灵（2026-10-27 起）。
    /// 取不到返回 null（调用方 LogError 可见），不做任何兜底。
    /// </summary>
    static Sprite LoadChapterBackgroundSprite(int chapter)
    {
        if (chapter < 1 || chapter >= ChapterBackgroundPaths.Length) return null;
        string path = ChapterBackgroundPaths[chapter];
        if (string.IsNullOrEmpty(path)) return null;
        var sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"[Parallax] Resources.Load<Sprite> 失败: {path}（检查图是否在 Resources 下且 textureType=Sprite）");
        return sprite;
    }

    void SetLayerSprite(ref ParallaxLayer layer, Sprite sprite, string layerName)
    {
        if (layer.tiles == null) return;
        for (int i = 0; i < layer.tiles.Length; i++)
        {
            if (layer.tiles[i] == null) continue;
            string tag = layerName + (i == 0 ? "" : "_tile" + i);
            // 2026-10-26：瓦片可能是拼接单元（「1」= 本体图 + 镜像子图），
            // 换章节图必须覆盖瓦片内全部 Image（含子孙），否则子图留旧章节的图
            var imgs = layer.tiles[i].GetComponentsInChildren<UnityEngine.UI.Image>(true);
            if (imgs != null && imgs.Length > 0)
            {
                for (int k = 0; k < imgs.Length; k++)
                    ApplySprite(imgs[k], imgs[k].gameObject, sprite, tag);
            }
            else if (layer.images == null || layer.images[i] == null)
            {
                if (layer.images == null) layer.images = new UnityEngine.UI.Image[layer.tiles.Length];
                layer.images[i] = layer.tiles[i].GetComponent<UnityEngine.UI.Image>();
                ApplySprite(layer.images[i], layer.tiles[i].gameObject, sprite, tag);
            }
            else
                ApplySprite(layer.images[i], layer.tiles[i].gameObject, sprite, tag);
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

    /// <summary>击杀镜头：放大 map 视差以匹配世界 ortho zoom。</summary>
    public void ApplyKillCamZoomMul(float zoomMul)
    {
        if (_layerRoot == null)
            EnsureLayers();
        if (_layerRoot == null) return;
        _killCamZoomMul = Mathf.Clamp(zoomMul, 0.55f, 1f);
        ApplyCombinedScale();
    }

    public void ResetKillCamZoom()
    {
        if (_layerRoot == null) return;
        _killCamZoomMul = 1f;
        ApplyCombinedScale();
    }

    /// <summary>
    /// 【2026-10-27 主人拍板：map 按预制体大小，不改缩放】视口缩放停用。
    /// 保留接口避免调用方（BattleViewportFit.Apply）报错，实现置空。
    /// </summary>
    public void ApplyViewportZoom(float mul)
    {
    }

    /// <summary>
    /// 【2026-10-27 主人拍板】map 按预制体大小显示（map 节点 scale 1.13、图 2020×768），
    /// 运行时不再整体缩放：视口缩放 / 长屏 Y 补缝已停用（ApplyViewportZoom、
    /// ApplyVerticalCoverToBand 均为 no-op）。本方法只保留击杀镜头的临时缩放
    /// （ResetKillCamZoom 后层根回到预制体基准，map 日常恒为预制体大小）。
    /// </summary>
    void ApplyCombinedScale()
    {
        if (_layerRoot == null) return;
        EnsureBaseScaleCaptured();
        float inv = 1f / Mathf.Max(0.0001f, _killCamZoomMul);
        _layerRoot.localScale = new Vector3(
            _layerRootBaseScale.x * inv,
            _layerRootBaseScale.y * inv,
            _layerRootBaseScale.z);
    }

    void EnsureBaseScaleCaptured()
    {
        if (_baseScaleCaptured) return;
        _layerRootBaseScale = _layerRoot.localScale;
        _baseScaleCaptured = true;
    }

    // ===== 长屏补缝（2026-10-17）→【2026-10-27 主人拍板停用】=====

    /// <summary>
    /// 【2026-10-27 主人拍板：map 按预制体大小（map 节点 scale 1.13、图 2020×768），不改缩放】
    /// 长屏 Y 向补缝停用——条带再高也不再放大视差层。保留接口避免调用方
    /// （BattleViewportFit.FitParallaxVerticalCover）报错，实现置空。
    /// </summary>
    public void ApplyVerticalCoverToBand(float bandHeight)
    {
    }
}
