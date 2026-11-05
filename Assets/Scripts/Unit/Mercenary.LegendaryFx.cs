
using UnityEngine;

/// <summary>
/// 传奇佣兵战斗特效（2026-11-05 主人指令「显示的不一样些」）分册：
///
/// <para>① <b>形象流光</b>：全身 SpriteRenderer 挂 Equip_shine_gold（SSU Shine，金色 HDR，
/// Linear_Default 档吃 UnscaledShaderTime 全局时间，结算 timeScale=0 也继续扫）。</para>
/// <para>② <b>脚下光环</b>：程序化软圆金色光晕，sortingOrder -19（脚影 -20 之上、身体之下），
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

    /// <summary>Init / SetHireId 末尾调用：按当前 mercId/hireId 重判传奇并应用或回滚（幂等）。</summary>
    void ApplyLegendaryFx()
    {
        string rid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        bool legendary = !string.IsNullOrEmpty(rid)
            && RarityPalette.ResolveMercRarity(rid) == MercRosterDefs.MercRarity.Legendary;
        if (legendary) EnsureFigureShine();
        else RestoreFigureMaterial();
        EnsureHalo(legendary);
    }

    // ── ① 形象流光 ──

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
                _haloSr.sortingOrder = -19;
                _haloSr.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                _haloSr.transform.localRotation = Quaternion.identity;

                float targetW = 1.1f;
                if (sr != null && sr.sprite != null)
                    targetW = Mathf.Max(0.6f, sr.bounds.size.x * 1.35f);
                float nativeW = _haloSr.sprite != null ? Mathf.Max(0.01f, _haloSr.sprite.bounds.size.x) : 1f;
                float lossyX = Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x));
                float s = targetW / (nativeW * lossyX);
                var baseScale = new Vector3(s, s * 0.4f, 1f);
                _haloSr.transform.localScale = baseScale;

                var pulse = go.AddComponent<MercHaloPulse>();
                pulse.sr = _haloSr;
                pulse.baseScale = baseScale;
            }
        }
        _haloSr.gameObject.SetActive(legendary);
    }

    static Sprite _haloSprite;
    static Material _haloMat;

    /// <summary>程序化软圆（径向 alpha 渐变，白底）——玩法需求主动生成，与 Monster 视觉的兜底圆同一思路。</summary>
    static Sprite HaloSprite()
    {
        if (_haloSprite != null) return _haloSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "LegendaryHaloSoftCircle";
        var pixels = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep 软边
                a = Mathf.Pow(a, 1.6f);      // 边缘再收虚一档
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
}

/// <summary>传奇佣兵脚下光环呼吸脉动（unscaled time，结算定格也继续）。</summary>
public class MercHaloPulse : MonoBehaviour
{
    public SpriteRenderer sr;
    public Vector3 baseScale = Vector3.one;
    float _phase;

    void OnEnable() => _phase = Random.value * Mathf.PI * 2f;

    void Update()
    {
        if (sr == null) return;
        float t = Time.unscaledTime * 2.2f + _phase;
        float k = 0.9f + 0.1f * Mathf.Sin(t);
        sr.color = new Color(1f, 0.82f, 0.38f, 0.4f + 0.1f * Mathf.Sin(t));
        transform.localScale = baseScale * k;
    }
}
