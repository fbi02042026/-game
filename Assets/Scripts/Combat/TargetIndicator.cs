using UnityEngine;

/// <summary>
/// 把 FocusMarkSystem 选中的攻击目标可视化：头顶箭头（上下浮动）+ 脚下贴地光圈。
/// 职责单一：只负责显示，不改任何战斗逻辑、不动 FocusMarkSystem 本身。
/// 指示器永远只显示 1 个目标（就是 FocusMarkSystem 选中的那个）。
/// </summary>
public class TargetIndicator : MonoBehaviour
{
    public static TargetIndicator Instance { get; private set; }

    [Header("箭头")]
    [Tooltip("三张箭头贴图(PT/JY/箭头01)本身都是尖朝下，直接显示即为正确朝向，故默认不翻转(false)。保留此开关以备将来换图需要翻转时使用")]
    public bool arrowFlipY = false;
    public float arrowFloatFreq = 2.8f;   // 上下浮动频率
    public float arrowFloatAmp = 0.12f;    // 上下浮动幅度（世界单位）
    public float arrowHeadOffset = 0.25f;  // 头顶间隙兜底值（拿不到包围盒时才用；正常走 ArrowHeadGapRatio 按身高比例算）
    /// <summary>头顶间隙 = 目标包围盒高度 × 该比例。怪大箭头就高一点、怪小就近一点，不再写死一个偏移。</summary>
    const float ArrowHeadGapRatio = 0.08f;
    /// <summary>箭头整体再往下挪多少（世界单位）。主人反馈"还是太高" → 调大这个值即可，往下挪就加大。</summary>
    const float ArrowHeadDropY = 0.30f;
    /// <summary>箭头设计基准「世界缩放」：改挂容器之前箭头挂在怪物自身下、localScale=1 时的视觉尺寸基准。</summary>
    const float ArrowBaseWorldScale = 1.0f;
    /// <summary>主人要求视觉缩小：先 ×0.7，2026-09-26 再缩小 20% → 0.7 × 0.8 = 0.56。
    /// 注意：这是世界缩放系数，不能直接写进 localScale（见 ApplyArrowScale）。</summary>
    const float ArrowShrink = 0.56f;

    [Header("脚下光圈")]
    public float ringScaleX = 1.9f;        // 目标约 1.2 世界单位宽 → 64px@PPU100 缩放
    public float ringScaleY = 0.8f;        // 目标约 0.5 世界单位高（扁椭圆）

    [Header("总开关（临时关掉看对比）")]
    public bool Enabled = true;

    SpriteRenderer _arrowSr;
    SpriteRenderer _ringSr;
    Sprite _ringSprite;

    // 攻击目标箭头三张备选贴图：普通怪(PT) / 精英怪(JY) / 兜底(箭头01)
    Sprite _sprNormal;
    Sprite _sprElite;
    Sprite _sprFallback;
    Sprite _appliedArrowSpr;       // 当前已应用的箭头 sprite，避免每帧重复赋值

    UnitBase _currentTarget;       // 当前跟随的目标，用于检测切换
    SpriteRenderer _targetBodySr;  // 缓存目标身体 Sprite，取头顶高度
    bool _shown;                   // 上一帧是否处于显示态
    bool _snap;                    // 本帧需要直接定位（首帧/目标切换，避免从旧位置 lerp 拖影）

    public static TargetIndicator Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TargetIndicator");
        DontDestroyOnLoad(go);
        return go.AddComponent<TargetIndicator>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildChildren();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>创建 Arrow / Ring 两个世界空间子节点（挂在自己 transform 下）。</summary>
    void BuildChildren()
    {
        // ---- Arrow（头顶箭头）----
        LoadArrowSprites();
        CreateArrow();

        // ---- Ring（脚下光圈，代码生成，不依赖新美术）----
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);
        _ringSr = ringGo.AddComponent<SpriteRenderer>();
        _ringSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        _ringSr.sortingOrder = GameConfig.SORT_VFX + 4;  // 54，略低于箭头免得盖住
        _ringSprite = GenerateRingSprite();
        _ringSr.sprite = _ringSprite;
        _ringSr.color = new Color(1f, 0.95f, 0.7f, 0.45f); // 暖白淡色，草地上不刺眼
        ringGo.transform.localScale = new Vector3(ringScaleX, ringScaleY, 1f);

        ringGo.SetActive(false);   // 箭头节点在 CreateArrow 内部已置为隐藏
    }

    /// <summary>
    /// 创建 Arrow 子节点（BuildChildren 首次创建 与 被外部销毁后自愈 共用同一套代码）。
    /// 箭头挂到父级（通常是 Monsters 容器）：不再挂在怪物自身之下，
    /// 免得怪物一播动画就带着箭头一起缩放/位移（表现就是箭头来回晃）。
    /// transform.parent 为 null（本组件挂在场景根）时保持原父级，不做无谓挪动。
    /// </summary>
    void CreateArrow()
    {
        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(transform, false);
        _arrowSr = arrowGo.AddComponent<SpriteRenderer>();
        _arrowSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        _arrowSr.sortingOrder = GameConfig.SORT_VFX + 5; // 55，画在怪物与伤害数字之上
        _arrowSr.enabled = false;                         // 默认隐藏，等加载到 sprite

        if (transform.parent != null)
            arrowGo.transform.SetParent(transform.parent, true); // worldPositionStays=true：保持当前世界位置
        ApplyArrowScale();                                       // 按世界尺寸算缩放，不写死 0.7

        // 初始用兜底图占位（LateUpdate 会按目标类型换成 PT/JY）
        if (_sprFallback != null)
        {
            _arrowSr.sprite = _sprFallback;
            _arrowSr.flipY = arrowFlipY;
            _arrowSr.enabled = true;
            _appliedArrowSpr = _sprFallback;
        }
        else
        {
            Debug.LogWarning("[TargetIndicator] 未找到 Resources/UI/Common/箭头01.png，箭头已隐藏（不报错、不空引用）");
        }

        arrowGo.SetActive(false);
    }

    /// <summary>
    /// 按「世界尺寸」校正箭头缩放。
    /// 为什么不能直接写死 0.7：世界缩放 = localScale × 父级 lossyScale。
    /// 之前箭头挂在怪物自身下，会连乘怪物节点的 localScale（通常 &lt;1，视觉上被缩小）；
    /// 挪到 Monsters 容器后不再乘那个小缩放，直接写 0.7 反而比改版前更大 —— 这就是"越改越大"的原因。
    /// 这里统一除以父级当前 lossyScale，保证世界缩放恒等于 基准 × ArrowShrink。
    /// 只在换父级 / 切目标 / 从隐藏转显示时调一次（容器缩放可能在运行中变化，但不必每帧校正）。
    /// </summary>
    void ApplyArrowScale()
    {
        if (!ArrowAlive()) return;

        float s = ArrowBaseWorldScale * ArrowShrink;   // 目标世界缩放（父级为空时的兜底值）
        var parentTf = _arrowSr.transform.parent;
        if (parentTf != null)
        {
            float parentLossy = parentTf.lossyScale.x;
            if (parentLossy > 0.0001f) s /= parentLossy;  // 关键一行：localScale = 基准 × ArrowShrink / 父级 lossyScale
        }
        _arrowSr.transform.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>箭头是否还活着。UnityEngine.Object 的 == null 对"已销毁（Fake Null）"对象同样成立。</summary>
    bool ArrowAlive() { return _arrowSr != null; }

    /// <summary>光圈是否还活着（挂在自身 transform 下，正常不会比本组件先死，仍统一判空防御）。</summary>
    bool RingAlive() { return _ringSr != null; }

    /// <summary>
    /// 加载箭头三张备选 sprite 并缓存。PT=普通怪，JY=精英怪，箭头01=兜底。
    /// 三张都加载失败时才打一条警告，不刷屏。
    /// </summary>
    void LoadArrowSprites()
    {
        _sprNormal = Resources.Load<Sprite>("UI/Common/PT");
        _sprElite = Resources.Load<Sprite>("UI/Common/JY");
        _sprFallback = Resources.Load<Sprite>("UI/Common/箭头01");
        if (_sprNormal == null && _sprElite == null && _sprFallback == null)
            Debug.LogWarning("[TargetIndicator] 未找到箭头 sprite（PT/JY/箭头01），攻击目标箭头将不可用");
    }

    /// <summary>
    /// 代码生成一个 64×64 的椭圆贴图（中心不透明、边缘透明），缓存复用，不每帧新建。
    /// </summary>
    static Sprite GenerateRingSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rx = size * 0.5f;
        float ry = size * 0.5f;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 归一化椭圆距离：0=中心，1=边缘
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 中心不透明，向边缘平滑淡出到透明
                float a = d <= 1f ? 1f - Mathf.SmoothStep(0.55f, 1.0f, d) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return sp;
    }

    void LateUpdate()
    {
        if (!Enabled)
        {
            HideAll();
            return;
        }

        var target = FocusMarkSystem.MarkedEnemy;

        // 目标可能中途被销毁（怪物死亡/波次清理/场景清理）：
        // 这里必须用 == null 先判掉 —— C# 的 is 类型模式不走 Unity 重载的 ==，销毁对象会误判通过。
        if (target == null)
        {
            HideAll();
            return;
        }

        // BOSS 直接隐藏整个指示器（头顶箭头 + 脚下光圈），Boss 已有屏幕血条
        if (target is Monster bossMon && bossMon.IsBossUnit)
        {
            HideAll();
            return;
        }

        var bm = BattleManager.InstanceQuiet;   // 战斗外为 null 且不打 LogError
        bool show = target != null
                    && !target.isDead
                    && bm != null
                    && bm.isInBattle
                    && bm.UnitsCanAct;

        if (!show)
        {
            HideAll();
            return;
        }

        // 自愈：箭头挂在 Monsters 容器下，容器被清理时它会跟着被 Destroy，
        // _arrowSr 字段随即变成 Fake Null（!= null 判定成立，但一访问就抛 MissingReferenceException）。
        // 这里走策略(b)——发现被销毁就立刻用 CreateArrow 重建同一套节点，
        // 不用等"下次切目标"，保证当前帧起箭头就恢复正常。
        if (!ArrowAlive())
        {
            CreateArrow();
            AttachArrowToTargetParent(target.transform);  // 重建后重新挂回目标所属容器
            _appliedArrowSpr = null;   // 新建的 SpriteRenderer 上还没贴图，强制下面重新赋值
            _snap = true;
        }
        if (!ArrowAlive() || !RingAlive()) return;   // 万一重建不出来（理论上不会），安全跳过本帧

        // 目标切换时刷新缓存（避免每帧 GetComponent 产生 GC）
        // _currentTarget 可能已被销毁：== null 对销毁对象成立，会自然走到刷新分支
        if (_currentTarget != target)
        {
            _currentTarget = target;
            _targetBodySr = target.GetComponentInChildren<SpriteRenderer>(true);
            _snap = true; // 目标变了，下一处直接定位
            AttachArrowToTargetParent(target.transform);
        }

        // GetBodyTransform 只定义在 Monster 上，UnitBase 没有 → 必须做类型判断再取
        Transform bodyTf = target is Monster mon ? mon.GetBodyTransform() : target.transform;
        Vector3 bodyPos = bodyTf != null ? bodyTf.position : target.transform.position;

        // 头顶高度：优先用身体 Sprite 的 bounds.max.y，拿不到退回 position.y + 1
        float headY = bodyPos.y + 1f;
        if (_targetBodySr != null && _targetBodySr.sprite != null)
            headY = _targetBodySr.bounds.max.y;

        // 头顶间隙：优先按目标包围盒高度比例算（怪大则高、怪小则近），拿不到包围盒才用 arrowHeadOffset 兜底
        float headGap = arrowHeadOffset;
        if (_targetBodySr != null && _targetBodySr.sprite != null)
            headGap = _targetBodySr.bounds.size.y * ArrowHeadGapRatio;

        // 箭头目标位置：头顶 + 间隙 - 整体下调量 + 上下浮动
        float floatY = Mathf.Sin(Time.unscaledTime * arrowFloatFreq) * arrowFloatAmp;
        Vector3 arrowTarget = new Vector3(bodyPos.x, headY + headGap - ArrowHeadDropY + floatY, bodyPos.z);

        // 光圈目标位置：脚下贴地（X 跟目标走，Y 用 FootY）
        float footY = target.FootY;
        Vector3 ringTarget = new Vector3(bodyPos.x, footY, bodyPos.z);

        if (!_shown || _snap)
        {
            // 首次出现 / 目标切换：直接定位，避免从旧位置 lerp 拖影
            // 顺带按父级当前 lossyScale 校正一次世界尺寸（容器缩放可能中途变过），不每帧校正省开销
            ApplyArrowScale();
            SnapTo(arrowTarget, ringTarget);
        }
        else
        {
            // 平滑跟随（Vector3.Lerp 返回 struct，无堆分配）
            if (ArrowAlive())
                _arrowSr.transform.position = Vector3.Lerp(_arrowSr.transform.position, arrowTarget, 0.25f);
            if (RingAlive())
                _ringSr.transform.position = Vector3.Lerp(_ringSr.transform.position, ringTarget, 0.25f);
        }
        _snap = false;

        // 按目标类型选择箭头贴图：精英=JY，普通/其他=PT，缺则回退箭头01
        Sprite wantSpr;
        if (target is Monster elMon && elMon.IsEliteWave)
            wantSpr = _sprElite;
        else
            wantSpr = _sprNormal;
        if (wantSpr == null) wantSpr = _sprFallback;

        if (wantSpr == null)
        {
            // 三张都缺：只隐藏箭头，光圈照常显示
            _arrowSr.gameObject.SetActive(false);
        }
        else
        {
            // 只在 sprite 真正变化时赋值，避免每帧重复设置
            if (_appliedArrowSpr != wantSpr)
            {
                _arrowSr.sprite = wantSpr;
                _arrowSr.flipY = arrowFlipY;
                _appliedArrowSpr = wantSpr;
            }
            _arrowSr.gameObject.SetActive(true);
            _arrowSr.enabled = true;   // 缺陷4：兜底图加载失败时 SpriteRenderer 仍 disabled，需补启用
        }
        _ringSr.gameObject.SetActive(true);
        _shown = true;
    }

    /// <summary>
    /// 把箭头挂到「目标所属容器」（通常是 Monsters）：之后箭头只按世界坐标每帧同步，
    /// 不再继承怪物自身动画的缩放与位移，怪播动画时箭头不会跟着抖。
    /// 拿不到容器时保持原父级，不强行挪（避免空引用、也避免把箭头甩丢）。
    /// </summary>
    void AttachArrowToTargetParent(Transform targetTf)
    {
        if (!ArrowAlive()) return;   // 箭头已被外部销毁：交给 LateUpdate 的自愈分支重建，这里直接跳过

        Transform host = null;
        if (targetTf != null && targetTf.parent != null) host = targetTf.parent;
        else if (transform.parent != null) host = transform.parent;
        if (host == null) return;

        var arrowTf = _arrowSr.transform;
        if (arrowTf.parent == host) return;                 // 已经挂对了，别重复 SetParent
        arrowTf.SetParent(host, true);                      // worldPositionStays=true：保持当前世界位置
        ApplyArrowScale();                                  // 换父级后父级缩放不同，必须按世界尺寸重算
    }

    /// <summary>直接把两个指示器放到目标位置（切换/首帧用）。</summary>
    void SnapTo(Vector3 arrowPos, Vector3 ringPos)
    {
        // 两个节点都可能已被外部销毁（Fake Null），访问前统一判空，绝不抛异常
        if (ArrowAlive()) _arrowSr.transform.position = arrowPos;
        if (RingAlive()) _ringSr.transform.position = ringPos;
    }

    void HideAll()
    {
        _currentTarget = null;
        _targetBodySr = null;
        _shown = false;
        if (ArrowAlive()) _arrowSr.gameObject.SetActive(false);
        if (RingAlive()) _ringSr.gameObject.SetActive(false);
    }

    /// <summary>
    /// 切场景 / 重开战斗时清状态并隐藏。照 FocusMarkSystem.ResetForBattle 的风格。
    /// 不强制要求被调用，方法存在且安全即可。
    /// </summary>
    public void ResetForBattle()
    {
        HideAll();
    }
}
