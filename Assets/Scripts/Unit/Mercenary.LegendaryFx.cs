
using UnityEngine;

/// <summary>
/// 传奇佣兵战斗特效（2026-11-05 主人指令「显示的不一样些」）分册：
///
/// <para>① <b>形象流光已停用（2026-11-09 主人拍板）</b>：战斗形象不挂流光；以下仅为历史记录——
/// 原实现：全身 SpriteRenderer 挂 Equip_shine_gold（SSU Shine，金色 HDR，
/// Linear_Default 档吃 UnscaledShaderTime 全局时间，结算 timeScale=0 也继续扫）。
/// 现仅 ② 脚下法阵生效，法阵是发光效果不是流光。</para>
/// <para>② <b>脚下法阵</b>：2026-11-12 主人拍板改用现成美术粒子 MagicFieldYellow（
/// Resources/VFX/Legendary/，实例旋转 X=-100），替换 11-05 程序化光环（实现已删除）。</para>
/// <para>传奇判定与名字颜色同源（RarityPalette.ResolveMercRarity）。受击闪白走 sr.color 顶点色，
/// 与本特效（sharedMaterial）互不干扰。共用 asset 的佣兵（如 H005/H015 共用 gongshou101）
/// 稀有度可能不同 → Init 与 SetHireId 都重判，非传奇幂等回滚到原始材质。</para>
/// </summary>
public partial class Mercenary
{
    const string FieldNodeName = "LegendaryField";
    const string FieldResourcePath = "VFX/Legendary/MagicFieldYellow";

    GameObject _fieldGo;
    SpriteRenderer[] _figureSrs;
    Material[] _figureMatBaseline;

    /// <summary>Init / SetHireId 末尾调用：重判传奇并刷新脚下法阵（形象流光已停用）。</summary>
    void ApplyLegendaryFx()
    {
        string rid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        bool legendary = !string.IsNullOrEmpty(rid)
            && RarityPalette.ResolveMercRarity(rid) == MercRosterDefs.MercRarity.Legendary;
        // 2026-11-09 口径：战斗形象不挂流光，只保留脚下法阵；保留回滚以防旧状态残留。
        RestoreFigureMaterial();
        EnsureLegendaryField(legendary);
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
        return n.IndexOf("FootShadow", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("NameLabel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ── ② 脚下法阵（2026-11-12 改用现成美术粒子，替换已删除的程序化光环）──

    /// <summary>
    /// 脚下法阵：Resources/VFX/Legendary/MagicFieldYellow（3D 粒子，实例旋转 X=-100）。
    /// 幂等：建一次缓存，之后只 SetActive；非传奇隐藏。取不到资源直接 LogError 不兜底。
    /// </summary>
    void EnsureLegendaryField(bool legendary)
    {
        if (_fieldGo == null)
        {
            var prefab = Resources.Load<GameObject>(FieldResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[Mercenary.LegendaryFx] 缺特效预制体 Resources/" + FieldResourcePath
                    + " —— 资源未入库或路径错，脚下无法阵");
                return;
            }

            // 挂进 SortingGroup host（与旧光环 / Monster.EnsureFootShadow 同层口径）
            Transform host = transform;
            var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg == null) sg = GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            if (sg == null)
            {
                GameConfig.ApplyUnitSorting(transform);
                sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
            }
            if (sg != null) host = sg.transform;

            // 清掉旧程序化光环残留（对象池复用旧实例时防叠加）
            Transform legacyHalo = host.Find("LegendaryHalo");
            if (legacyHalo != null) Destroy(legacyHalo.gameObject);

            _fieldGo = Instantiate(prefab, host);
            _fieldGo.name = FieldNodeName;
            _fieldGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            // 主人拍板：旋转 X=-100（prefab 源是 -90，不动美术源，实例上覆盖）
            _fieldGo.transform.localRotation = Quaternion.Euler(-100f, 0f, 0f);

            // 大小：法阵视觉直径 ≈ 角色身宽 ×1.4；prefab 发射半径 1、视觉直径约 2.5（含粒子溢出），
            // 按 host lossyScale 折算后 clamp（真机看效果可微调 2.5f 折算值）
            float targetW = 1.3f;
            if (sr != null && sr.sprite != null)
                targetW = Mathf.Max(0.8f, sr.bounds.size.x * 1.4f);
            float lossyX = Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x));
            float s = Mathf.Clamp(targetW / (2.5f * lossyX), 0.15f, 1f);
            _fieldGo.transform.localScale = new Vector3(s, s, s);

            // 层级/渲染：粒子进单位 SortingGroup，order 用组内相对值（沿用旧光环 3 = 身体之上可见值）
            var renderers = _fieldGo.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.renderMode == ParticleSystemRenderMode.None && r.sharedMaterial != null)
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                if (r.maxParticleSize < 2f) r.maxParticleSize = 100f;
                r.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                r.sortingOrder = 3;
            }
            // Local 模拟让法阵跟随单位移动（World 会拖尾留原地）；已是 Local 则无操作
            var pss = _fieldGo.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < pss.Length; i++)
            {
                if (pss[i] == null) continue;
                var main = pss[i].main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }
        _fieldGo.SetActive(legendary);
    }
}
