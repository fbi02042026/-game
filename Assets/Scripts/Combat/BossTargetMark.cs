using UnityEngine;

/// <summary>
/// Boss 当前目标头顶标记（2026-10-14 主人拍板）：Boss 换索敌策略 / 换目标时，
/// 在新目标头顶挂一把红色「锁」，代表 Boss 现在盯着谁打。
/// 图标用现成 Resources/UI/Common/锁.png（不新增美术）；跟随 / 弹跳 / 呼吸全程序驱动。
/// 生命周期：Boss 死亡 / 目标死亡 / 切场景（单位被销毁）→ 自动隐藏。
/// </summary>
public class BossTargetMark : MonoBehaviour
{
    const string IconResource = "UI/Common/锁";
    /// <summary>盖过玩家集火箭头(SORT_VFX+5=55)，画在怪物与伤害数字之上。</summary>
    const int SortOrder = GameConfig.SORT_VFX + 6;
    /// <summary>图标基准世界高度（单位）。角色身高约 1.2，锁约半人高醒目又不遮脸。</summary>
    const float BaseWorldScale = 0.5f;
    /// <summary>换目标弹跳：从 1.8 倍缩回 1.0 的时长。</summary>
    const float PopDuration = 0.25f;
    const float BobAmplitude = 0.08f;
    const float BobHz = 3.2f;
    const float HeadGap = 0.18f;

    static readonly Color MarkColor = new Color(1f, 0.28f, 0.22f, 0.95f);

    public static BossTargetMark Instance { get; private set; }

    SpriteRenderer _sr;
    Transform _iconTf;
    UnitBase _target;
    Monster _boss;
    SpriteRenderer[] _targetSrs;   // 头顶高度用的身体 sprite 列表（Show 时缓存，目标变更才重取）
    float _popT;
    bool _popping;

    public static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("BossTargetMark");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<BossTargetMark>();
    }

    /// <summary>Boss 锁定新目标：显示/移动标记并弹一下。同一目标重复调用不重弹。</summary>
    public static void Show(UnitBase target, Monster boss)
    {
        if (target == null || boss == null) return;
        Ensure();
        if (Instance == null) return;
        bool changed = Instance._target != target || Instance._boss != boss;
        Instance._target = target;
        Instance._boss = boss;
        if (changed)
        {
            Instance._popT = 0f;
            Instance._popping = true;
            Instance._targetSrs = null;   // 换目标才重扫身体 sprite
        }
        Instance.EnsureIcon();
        Instance.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (Instance == null) return;
        Instance._target = null;
        Instance._boss = null;
        Instance.gameObject.SetActive(false);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void EnsureIcon()
    {
        if (_iconTf != null) return;
        var go = new GameObject("Icon");
        _iconTf = go.transform;
        _iconTf.SetParent(transform, false);
        _sr = go.AddComponent<SpriteRenderer>();
        _sr.sprite = Resources.Load<Sprite>(IconResource);
        if (_sr.sprite == null)
            Debug.LogError($"[BossTargetMark] 未找到 Resources/{IconResource}，Boss 锁定标记没有图（不兜底，让我直接看见）");
        _sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        _sr.sortingOrder = SortOrder;
        _sr.color = MarkColor;
    }

    void Update()
    {
        if (_boss == null || _boss.isDead || _target == null || _target.isDead)
        {
            Hide();
            return;
        }
        if (_iconTf == null || _sr == null) return;

        // 头顶跟随 + 上下呼吸浮动
        float bob = Mathf.Sin(Time.unscaledTime * 6.283185f * BobHz) * BobAmplitude;
        Vector3 pos = HeadTop();
        pos.y += HeadGap + bob;
        _iconTf.position = pos;

        // 换目标弹跳：1.8 倍缩回 1.0
        float s = BaseWorldScale;
        if (_popping)
        {
            _popT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_popT / PopDuration);
            s *= 1f + 0.8f * (1f - k);
            if (k >= 1f) _popping = false;
        }
        _iconTf.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>目标头顶世界点：取目标所有 sprite 的最高点（头发顶），没有则 position.y + 1.2 兜底。</summary>
    Vector3 HeadTop()
    {
        float top = _target.transform.position.y + 1.2f;
        if (_targetSrs == null)
            _targetSrs = _target.GetComponentsInChildren<SpriteRenderer>(true);
        float best = float.MinValue;
        for (int i = 0; i < _targetSrs.Length; i++)
        {
            var sr = _targetSrs[i];
            if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
            if (sr.bounds.max.y > best) best = sr.bounds.max.y;
        }
        if (best > float.MinValue) top = best;
        return new Vector3(_target.transform.position.x, top, _target.transform.position.z);
    }
}
