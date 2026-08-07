using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 怪物世界空间血条（预制体模式）
/// 优先从 Assets/Resources/Prefabs/Monster/MonsterHealthBar.prefab 加载
/// 找不到时自动动态创建（兜底）
/// 在Unity编辑器中可通过 Tools → 生成怪物血条预制体 创建
/// </summary>
public class MonsterHealthBar : MonoBehaviour
{
    [Header("血条样式")]
    [Tooltip("血条宽度（世界单位）")]
    public float barWidth = 0.3f;
    [Tooltip("血条高度（世界单位）")]
    public float barHeight = 0.03f;
    [Tooltip("血条距离脚底的Y偏移（负值=脚下），修改预制体中的Y位置即可")]
    public float yOffset = -0.5f;
    [Tooltip("血条背景色")]
    public Color bgColor = new Color(0.15f, 0f, 0f, 0.8f);
    [Tooltip("血条填充色（红色）")]
    public Color fillColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [Tooltip("受到伤害时闪烁色")]
    public Color damageFlashColor = new Color(1f, 0.5f, 0f, 1f);

    /// <summary>关联的单位</summary>
    [SerializeField] private UnitBase _unit;
    /// <summary>填充Image（在预制体Inspector中拖入）</summary>
    [SerializeField] private Image _fillImage;
    /// <summary>血条RectTransform</summary>
    [SerializeField] private RectTransform _barRect;
    /// <summary>上一次血量比例（用于检测变化）</summary>
    private float _lastRatio = 1f;
    /// <summary>闪烁计时</summary>
    private float _flashTimer = 0f;

    // 缓存的预制体引用
    private static GameObject _cachedPrefab;
    private static bool _prefabChecked = false;

    void Awake()
    {
        // 自动查找填充Image（如果Inspector未赋值）
        if (_fillImage == null)
        {
            Transform fill = transform.Find("HPBg/HPFill");
            if (fill != null)
            {
                _fillImage = fill.GetComponent<Image>();
                if (_fillImage != null)
                    _fillImage.type = Image.Type.Filled;
            }
        }
        if (_barRect == null)
            _barRect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 为指定单位创建血条（静态工厂方法）
    /// 优先从预制体加载，找不到时动态创建
    /// </summary>
    public static MonsterHealthBar Create(UnitBase unit)
    {
        if (unit == null) return null;

        // 检查单位下是否已有血条（预制体中可能已放置）
        MonsterHealthBar existing = unit.GetComponentInChildren<MonsterHealthBar>();
        if (existing != null)
        {
            existing._unit = unit;
            existing.gameObject.SetActive(true);
            existing.ResetBar();
            existing.ApplyCompensatedPosition();
            return existing;
        }

        // 尝试从预制体加载
        GameObject prefab = GetHealthBarPrefab();
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, unit.transform, false);
            go.name = "MonsterHPBar";

            MonsterHealthBar bar = go.GetComponent<MonsterHealthBar>();
            if (bar != null)
            {
                bar._unit = unit;
                bar.ResetBar();
                bar.ApplyCompensatedScale();
                bar.ApplyCompensatedPosition();
                return bar;
            }
        }

        // 兜底：动态创建
        return CreateDynamic(unit);
    }

    /// <summary>
    /// 补偿父级缩放：使血条在世界空间保持预设大小
    /// 怪物MONSTER_SCALE=5，不补偿的话血条会变大5倍
    /// 幂等操作：始终设为 1/父级缩放，可安全重复调用
    /// </summary>
    public void ApplyCompensatedScale()
    {
        Vector3 parentLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        if (Mathf.Abs(parentLossy.x) < 0.01f) parentLossy.x = 1f;
        if (Mathf.Abs(parentLossy.y) < 0.01f) parentLossy.y = 1f;
        if (Mathf.Abs(parentLossy.z) < 0.01f) parentLossy.z = 1f;

        // 始终设为1/父级缩放（血条Canvas的sizeDelta控制实际大小，localScale只需保证世界缩放为1）
        transform.localScale = new Vector3(
            1f / parentLossy.x,
            1f / parentLossy.y,
            1f / parentLossy.z
        );
    }

    /// <summary>
    /// 补偿父级缩放后的位置：使血条在世界空间出现在正确的Y偏移
    /// yOffset是世界单位，需除以父级缩放转为本地单位
    /// </summary>
    public void ApplyCompensatedPosition()
    {
        Vector3 parentLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        if (Mathf.Abs(parentLossy.y) < 0.01f) parentLossy.y = 1f;
        transform.localPosition = new Vector3(0, yOffset / parentLossy.y, 0);
    }

    /// <summary>重置血条状态（怪物从对象池复用时调用）</summary>
    public void ResetBar()
    {
        _lastRatio = 1f;
        _flashTimer = 0f;
        if (_fillImage != null)
        {
            _fillImage.fillAmount = 1f;
            _fillImage.color = fillColor;
        }
    }

    /// <summary>
    /// 获取血条预制体（缓存）
    /// </summary>
    static GameObject GetHealthBarPrefab()
    {
        if (!_prefabChecked)
        {
            _prefabChecked = true;
#if UNITY_EDITOR
            _cachedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Monster/MonsterHealthBar.prefab");
#endif
            if (_cachedPrefab == null)
                _cachedPrefab = Resources.Load<GameObject>("Prefabs/Monster/MonsterHealthBar");
        }
        return _cachedPrefab;
    }

    /// <summary>
    /// 兜底动态创建血条（无预制体时使用）
    /// </summary>
    static MonsterHealthBar CreateDynamic(UnitBase unit)
    {
        GameObject canvasGo = new GameObject("MonsterHPBar");
        canvasGo.transform.SetParent(unit.transform, false);

        MonsterHealthBar bar = canvasGo.AddComponent<MonsterHealthBar>();
        bar._unit = unit;

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        canvas.sortingOrder = GameConfig.SORT_UNIT;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(bar.barWidth, bar.barHeight);
        canvasRect.localScale = Vector3.one;

        // Canvas设置完成后再补偿父级缩放（否则会被canvasRect.localScale=one覆盖）
        bar.ApplyCompensatedScale();
        bar.ApplyCompensatedPosition();

        bar._barRect = canvasRect;

        float pixelWidth = bar.barWidth;
        float pixelHeight = bar.barHeight;

        // 背景
        GameObject bgGo = new GameObject("HPBg");
        bgGo.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(pixelWidth, pixelHeight);
        bgRect.anchoredPosition = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = bar.bgColor;
        bgImg.raycastTarget = false;

        // 填充
        GameObject fillGo = new GameObject("HPFill");
        fillGo.transform.SetParent(bgGo.transform, false);
        RectTransform fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(0, pixelHeight);
        fillRect.anchoredPosition = Vector2.zero;
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = bar.fillColor;
        fillImg.raycastTarget = false;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        bar._fillImage = fillImg;

        Debug.Log($"[MonsterHealthBar] 动态创建血条（无预制体），请在Unity中运行 Tools → 生成怪物血条预制体");

        return bar;
    }

    void LateUpdate()
    {
        if (_unit == null || _unit.isDead)
        {
            if (_barRect != null)
                _barRect.gameObject.SetActive(false);
            return;
        }

        float maxHp = _unit.attr.GetAttr(AttrType.MaxHp);
        float ratio = maxHp > 0 ? _unit.currentHp / maxHp : 0;

        if (_fillImage != null)
        {
            _fillImage.fillAmount = Mathf.Clamp01(ratio);

            if (ratio < _lastRatio - 0.01f)
                _flashTimer = 0.2f;
            _lastRatio = ratio;

            if (_flashTimer > 0)
            {
                _flashTimer -= Time.deltaTime;
                _fillImage.color = Color.Lerp(damageFlashColor, fillColor, 1f - (_flashTimer / 0.2f));
            }
            else
            {
                _fillImage.color = fillColor;
            }
        }
    }

    public void DestroyBar()
    {
        if (_barRect != null)
            Destroy(_barRect.gameObject);
    }
}