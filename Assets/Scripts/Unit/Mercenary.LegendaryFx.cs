
using UnityEngine;

/// <summary>
/// 传奇佣兵战斗特效（2026-11-05 主人指令「显示的不一样些」）分册：
///
/// <para>① <b>形象流光已停用（2026-11-09 主人拍板）</b>：战斗形象不挂流光；以下仅为历史记录——
/// 原实现：全身 SpriteRenderer 挂 Equip_shine_gold（SSU Shine，金色 HDR，
/// Linear_Default 档吃 UnscaledShaderTime 全局时间，结算 timeScale=0 也继续扫）。
/// 现仅 ② 脚下光环生效，光环是发光效果不是流光。</para>
/// <para>② <b>脚下光环</b>：程序化环形金色发光地台，sortingOrder 3（同组内身体之上），
/// MercHaloPulse 呼吸脉动（unscaled time）。</para>
/// <para>传奇判定与名字颜色同源（RarityPalette.ResolveMercRarity）。受击闪白走 sr.color 顶点色，
/// 与本特效（sharedMaterial）互不干扰。共用 asset 的佣兵（如 H005/H015 共用 gongshou101）
/// 稀有度可能不同 → Init 与 SetHireId 都重判，非传奇幂等回滚到原始材质。</para>
/// </summary>
public partial class Mercenary
{
    const string HaloNodeName = "LegendaryHalo";

    SpriteRenderer _haloSr;
    SpriteRenderer[] _figureSrs;
    Material[] _figureMatBaseline;

    /// <summary>Init / SetHireId 末尾调用：重判传奇并刷新光环（形象流光已停用）。</summary>
    void ApplyLegendaryFx()
    {
        string rid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        bool legendary = !string.IsNullOrEmpty(rid)
            && RarityPalette.ResolveMercRarity(rid) == MercRosterDefs.MercRarity.Legendary;
        // 2026-11-09 口径：战斗形象不挂流光，只保留脚下光环；保留回滚以防旧状态残留。
        RestoreFigureMaterial();
        EnsureHalo(legendary);
    }

    // ── ① 形象流光 ──

    // 2026-11-09 起停用（战斗形象不挂流光），保留实现便于回退；当前无调用点。
    void EnsureFigureShine()
    {
        var shine = EquipRarityMaterials.LegendaryShineMaterial();
        if (shine == null) return; // 缺材质已在出口 LogError，不静默兜底
        if (_figureSrs == null || _figureMatBaseline == null)
        {
            _figureSrs = GetComponentsInChildren<SpriteRenderer>(true);
            _figureMatBaseline = new Material[_figureSrs.Length];
            for (int i = 0; i < _figureSrs.Length; i++)
                _figureMatBaseline[i] = _figureSrs[i] != null ? _figureSrs[i].sharedMaterial : null;
        }
        for (int i = 0; i < _figureSrs.Length; i++)
        {
            var r = _figureSrs[i];
            if (r == null || IsFxExcluded(r)) continue;
            r.sharedMaterial = shine;
        }
    }

    void RestoreFigureMaterial()
    {
        if (_figureSrs == null || _figureMatBaseline == null) return; // 从没上过特效 = 无操作
        for (int i = 0; i < _figureSrs.Length; i++)
            if (_figureSrs[i] != null) _figureSrs[i].sharedMaterial = _figureMatBaseline[i];
    }

    static bool IsFxExcluded(SpriteRenderer r)
    {
        string n = r.gameObject.name;
        return n == HaloNodeName
            || n.IndexOf("FootShadow", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("NameLabel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ── ② 脚下光环 ──

    void EnsureHalo(bool legendary)
    {
        if (_haloSr == null)
        {
            Transform existing = transform.Find(HaloNodeName);
            if (existing != null) _haloSr = existing.GetComponent<SpriteRenderer>();
            if (_haloSr == null)
            {
                // 挂进 SortingGroup host（与 Monster.EnsureFootShadow 同层口径，避免被地图整块盖住）
                Transform host = transform;
                var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (sg == null) sg = GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
                if (sg == null)
                {
                    GameConfig.ApplyUnitSorting(transform);
                    sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
                }
                if (sg != null) host = sg.transform;

                var go = new GameObject(HaloNodeName);
                go.transform.SetParent(host, false);
                _haloSr = go.AddComponent<SpriteRenderer>();
                _haloSr.sprite = HaloSprite();
                _haloSr.sharedMaterial = HaloMaterial();
                _haloSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _haloSr.sortingOrder = 3;
                _haloSr.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                _haloSr.transform.localRotation = Quaternion.identity;

                float targetW = 1.1f;
                if (sr != null && sr.sprite != null)
                    targetW = Mathf.Max(0.6f, sr.bounds.size.x * 1.6f);
                float nativeW = _haloSr.sprite != null ? Mathf.Max(0.01f, _haloSr.sprite.bounds.size.x) : 1f;
                float lossyX = Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x));
                float s = targetW / (nativeW * lossyX);
                var baseScale = new Vector3(s, s * 0.75f, 1f);
                _haloSr.transform.localScale = baseScale;

                var pulse = go.AddComponent<MercHaloPulse>();
                pulse.sr = _haloSr;
                pulse.baseScale = baseScale;

                // 2026-11-09 主人「可以带光效（粒子/发光）」：3 颗金色星尘，挂在 halo 节点下，
                // 跟随 halo 节点同一句显隐开关，天然幂等、不新增 MonoBehaviour。
                const int moteCount = 3;
                var moteSrs = new SpriteRenderer[moteCount];
                var moteBase = new Vector3[moteCount];
                for (int i = 0; i < moteCount; i++)
                {
                    var moteGo = new GameObject("LegendaryMote" + i);
                    moteGo.transform.SetParent(go.transform, false);
                    var msr = moteGo.AddComponent<SpriteRenderer>();
                    msr.sprite = MoteSprite();
                    msr.sharedMaterial = HaloMaterial();
                    msr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                    msr.sortingOrder = 4;                       // 同组，略高于地台环，别被身体整块吞掉
                    moteGo.transform.localScale = Vector3.one * 0.28f;
                    float ang = i * (Mathf.PI * 2f / moteCount);
                    moteBase[i] = new Vector3(Mathf.Cos(ang) * 0.34f, 0.02f, (float)i / moteCount);
                    moteGo.transform.localPosition = new Vector3(moteBase[i].x, moteBase[i].y, 0f);
                    msr.color = new Color(1f, 0.82f, 0.38f, 0f);
                    moteSrs[i] = msr;
                }
                pulse.moteSrs = moteSrs;
                pulse.moteBase = moteBase;
            }
        }
        _haloSr.gameObject.SetActive(legendary);
    }

    static Sprite _haloSprite;
    static Material _haloMat;
    static Sprite _moteSprite;

    /// <summary>程序化环形发光地台（双阈值 ring 亮环 + 中心极淡底光，白底）——玩法需求主动生成，与 Monster 视觉的兜底圆同一思路。</summary>
    static Sprite HaloSprite()
    {
        if (_haloSprite != null) return _haloSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "LegendaryHaloRing";
        var pixels = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 环形发光地台：中心低亮、0.52~0.66R 渐入、0.66~0.78R 满亮金色亮环、0.78~0.94R 外缘柔和淡出（不再是实心渐变圆）
                float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.66f, d))
                           * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.94f, d)));
                float inner = 0.08f * Mathf.Clamp01(1f - d / 0.5f);   // 中心极淡底光，不糊住脚
                float a = Mathf.Clamp01(ring + inner);
                byte v = (byte)Mathf.RoundToInt(a * 255f);
                pixels[y * S + x] = new Color32(255, 255, 255, v);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        _haloSprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _haloSprite;
    }

    static Material HaloMaterial()
    {
        if (_haloMat != null) return _haloMat;
        var shader = Shader.Find("Sprites/Default");
        _haloMat = shader != null ? new Material(shader) : null;
        if (_haloMat == null)
            Debug.LogError("[Mercenary.LegendaryFx] 找不到 Sprites/Default，脚下光环材质建不起来");
        return _haloMat;
    }

    /// <summary>星尘粒子贴图：程序化软圆（径向 alpha 渐变，白底），复用 HaloMaterial 的发光材质。</summary>
    static Sprite MoteSprite()
    {
        if (_moteSprite != null) return _moteSprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "LegendaryMoteSoftCircle";
        var pixels = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a *= a;                              // 中心亮、边缘软的小光点
                byte v = (byte)Mathf.RoundToInt(a * 255f);
                pixels[y * S + x] = new Color32(255, 255, 255, v);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        _moteSprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _moteSprite;
    }
}

/// <summary>传奇佣兵脚下光环呼吸脉动（unscaled time，结算定格也继续）。</summary>
public class MercHaloPulse : MonoBehaviour
{
    public SpriteRenderer sr;
    public Vector3 baseScale = Vector3.one;
    public SpriteRenderer[] moteSrs;     // 星尘渲染器（EnsureHalo 建一次缓存，避免逐帧 GetComponent）
    public Vector3[] moteBase;           // 每个星尘：x=水平偏移, y=起始高度, z=相位 0~1
    float _phase;

    void OnEnable() => _phase = Random.value * Mathf.PI * 2f;

    void Update()
    {
        if (sr == null) return;
        float t = Time.unscaledTime * 2.2f + _phase;
        float k = 0.9f + 0.1f * Mathf.Sin(t);
        sr.color = new Color(1f, 0.82f, 0.38f, 0.85f + 0.15f * Mathf.Sin(t));   // alpha 提高，静止帧也看得清亮环
        transform.localScale = baseScale * k;

        if (moteSrs == null || moteBase == null) return;
        for (int i = 0; i < moteSrs.Length; i++)
        {
            var m = moteSrs[i];
            if (m == null) continue;
            float p = Mathf.Repeat(Time.unscaledTime * 0.35f + moteBase[i].z + _phase, 1f);   // 缓慢上升、循环复位
            m.transform.localPosition = new Vector3(moteBase[i].x, moteBase[i].y + p * 0.5f, 0f);
            m.color = new Color(1f, 0.82f, 0.38f, 0.9f * Mathf.Sin(p * Mathf.PI));   // 起身淡入、到顶淡出
        }
    }
}
