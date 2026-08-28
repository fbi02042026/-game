using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 战斗特效：共用套装（刀光/暴击/弓箭/法球/治疗）+ 我方/敌方阵营色。
/// 弓箭与法球必须从 FirePoint 飞到 HitPoint，禁止瞬移命中。
/// </summary>
public class BattleVFXSystem : Singleton<BattleVFXSystem>
{
    [Header("特效预制体引用（在Inspector中拖入）")]
    public GameObject vfxSlash;
    public GameObject vfxMagicImpact;
    public GameObject vfxCritical;
    public GameObject vfxHeal;
    public GameObject vfxShield;
    public GameObject vfxFireball;
    public GameObject vfxFireImpact;
    public GameObject vfxIceImpact;
    public GameObject vfxLightning;
    public GameObject vfxExplosionBig;
    public GameObject vfxExplosionSmall;
    public GameObject vfxDust;
    public GameObject vfxLevelUp;

    [Header("设置")]
    public float defaultDuration = 2f;
    /// <summary>已废弃：特效一律用预制体自身缩放，请直接改 prefab</summary>
    public float defaultScale = 1f;
    /// <summary>已废弃：特效一律用预制体自身缩放</summary>
    public float sharedKitScale = 1f;

    [Header("阵营染色（暂无独立敌方资源时靠颜色区分谁放的）")]
    public Color allyTint = new Color(1f, 0.95f, 0.75f, 1f);
    public Color enemyTint = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("法球/弓箭飞行")]
    public float projectileSpeed = 48f;
    /// <summary>飞行贴图默认朝向修正：贴图尖端朝右=0，朝左=180，朝上=-90</summary>
    public float projectileAngleOffset = 0f;
    public float minFlightTime = 0.02f;
    public float maxFlightTime = 1.2f;
    /// <summary>弓箭发射点相对 GetFirePosition 的 Y 偏移（世界单位，正值向上）</summary>
    public float bowFireYOffset = 0.07f;

    private Dictionary<string, Queue<GameObject>> _pool = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<string, GameObject> _sharedKitCache = new Dictionary<string, GameObject>();
    protected override void Awake()
    {
        base.Awake();
        AutoLoadPrefabs();
    }

    #region 统一入口

    /// <summary>按套装+阵营播放。弓/球走飞行，刀光打在命中点。暴击只用飘字，不播暴击特效。</summary>
    public void PlayAttackKit(AttackVfxKit kit, VfxFaction faction,
        Vector3 fromPos, Vector3 toPos, int facingDir = 1, Transform hitTarget = null, bool isCrit = false)
    {
        switch (kit)
        {
            case AttackVfxKit.MeleeSlash:
                {
                    GameObject hit = LoadSharedKit(kit, faction, "hit");
                    if (hit == null && faction == VfxFaction.Ally)
                        hit = vfxSlash;
                    if (hit == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少刀光: {faction}/{kit}/hit → 请放 Resources/VFX/Shared/Ally/MeleeSlash/vfx_melee_hit");
                        return;
                    }
                    PlaySlash(toPos, facingDir, faction, hit);
                }
                break;
            case AttackVfxKit.Bow:
                {
                    GameObject fly = LoadSharedKit(kit, faction, "fly");
                    GameObject hit = LoadSharedKit(kit, faction, "hit");
                    if (fly == null)
                    {
                        // 禁止回退我方箭；无飞行体则只播命中（近距）或告警
                        Debug.LogWarning($"[VFX] 缺少飞行体: {faction}/{kit}/fly，不回退 Ally");
                        if (hit != null)
                            ApplyFactionLook(SpawnVFX(hit, toPos, defaultDuration, null), faction);
                        return;
                    }
                    Vector3 bowFrom = fromPos;
                    bowFrom.y += bowFireYOffset;
                    PlayBowProjectile(bowFrom, toPos, facingDir, hitTarget, faction, fly, hit);
                }
                break;
            case AttackVfxKit.Orb:
                {
                    GameObject fly = LoadSharedKit(kit, faction, "fly");
                    GameObject hit = LoadSharedKit(kit, faction, "hit");
                    if (fly == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少飞行体: {faction}/{kit}/fly，不回退 Ally");
                        if (hit != null)
                            ApplyFactionLook(SpawnVFX(hit, toPos, defaultDuration, null), faction);
                        return;
                    }
                    PlayOrbProjectile(fromPos, toPos, facingDir, hitTarget, faction, fly, hit);
                }
                break;
            case AttackVfxKit.Heal:
                {
                    GameObject hit = LoadSharedKit(kit, faction, "hit");
                    if (hit == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少共享特效: {faction}/{kit}/hit");
                        return;
                    }
                    PlayHeal(toPos, faction, hit);
                }
                break;
        }
    }

    public void PlayAttackVFX(WeaponAttackType attackType, Vector3 position, int facingDir = 1, Transform target = null)
    {
        AttackVfxKit kit = SkillNaming.KitFromAttackType(attackType);
        if (kit == AttackVfxKit.MeleeSlash)
            PlaySlash(position, facingDir, VfxFaction.Ally);
        else
            PlayMagicImpact(position, target, VfxFaction.Ally);
    }

    #endregion

    #region 普攻套装

    public void PlaySlash(Vector3 position, int facingDir = 1, VfxFaction faction = VfxFaction.Ally, GameObject prefabOverride = null)
    {
        if (!_prefabsLoaded) AutoLoadPrefabs();

        GameObject prefab = prefabOverride;
        if (prefab == null)
            prefab = LoadSharedKit(AttackVfxKit.MeleeSlash, faction, "hit");
        if (prefab == null)
            prefab = vfxSlash;
        if (prefab == null)
        {
            Debug.LogWarning($"[VFX] PlaySlash 无预制体 faction={faction}，期望 Resources/VFX/Shared/Ally/MeleeSlash/vfx_melee_hit");
            return;
        }

        GameObject go = AcquireVfxInstance(prefab, position);
        if (go == null) return;

        float slashScale = faction == VfxFaction.Ally ? 2.5f : 2.0f;
        Vector3 baseScale = prefab.transform.localScale;
        go.transform.localScale = new Vector3(
            Mathf.Abs(baseScale.x) * slashScale * (facingDir < 0 ? -1f : 1f),
            baseScale.y * slashScale,
            baseScale.z * slashScale);

        PrepareSlashParticles(go);
        StretchSlashLifetime(go, 0.5f);
        ResetTintableColors(go);
        ApplyFactionLook(go, faction);
        PlayAllParticles(go);
        ScheduleRelease(go, defaultDuration);
    }

    /// <summary>从池取出实例并摆到世界坐标，不提前 Play（刀光须先改缩放/材质）。</summary>
    GameObject AcquireVfxInstance(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;
        GameObject go = null;
        if (PoolManager.Instance != null)
        {
            string poolKey = "vfx:" + prefab.name;
            if (!_vfxPoolWarmed.Contains(poolKey))
            {
                PoolManager.Instance.Preload(poolKey, prefab, 3);
                _vfxPoolWarmed.Add(poolKey);
            }
            go = PoolManager.Instance.Get(poolKey, position, prefab.transform.rotation);
        }
        if (go == null)
            go = Instantiate(prefab, position, prefab.transform.rotation);

        float z = position.z;
        if (BattleManager.Instance != null && BattleManager.Instance.unitRoot != null)
            z = BattleManager.Instance.unitRoot.position.z - 0.15f;
        GameConfig.SetWorldPosition(go, new Vector3(position.x, position.y, z));
        go.transform.SetParent(transform, true);
        go.SetActive(true);
        SetVFXSortingLayer(go.transform);
        ResetTintableColors(go);
        StopAllParticles(go);
        return go;
    }

    void ScheduleRelease(GameObject go, float lifetime)
    {
        if (go == null) return;
        if (PoolManager.Instance != null && _vfxPoolWarmed.Count > 0)
            StartCoroutine(CoReleaseVfx(go, lifetime));
        else
            Destroy(go, lifetime);
    }

    static void StopAllParticles(GameObject go)
    {
        if (go == null) return;
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
            pss[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static void PrepareSlashParticles(GameObject go)
    {
        if (go == null) return;
        Material sharedMat = null;
        var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != null)
            {
                sharedMat = renderers[i].sharedMaterial;
                break;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (r.sharedMaterial == null && sharedMat != null)
                r.sharedMaterial = sharedMat;
            if (r.renderMode == ParticleSystemRenderMode.None && r.sharedMaterial != null)
                r.renderMode = ParticleSystemRenderMode.Billboard;
            if (r.maxParticleSize < 2f)
                r.maxParticleSize = 100f;
            r.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            r.sortingOrder = GameConfig.SORT_VFX + 5;
            r.enabled = true;
        }

        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var ps = pss[i];
            if (ps == null) continue;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null && pr.renderMode == ParticleSystemRenderMode.None && pr.sharedMaterial == null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    static void StretchSlashLifetime(GameObject go, float minLife)
    {
        if (go == null) return;
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var main = pss[i].main;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant
                && main.startLifetime.constant < minLife)
                main.startLifetime = minLife;
        }
    }

    public void PlayMagicImpact(Vector3 position, Transform target = null, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxMagicImpact == null) return;
        ApplyFactionLook(SpawnVFX(vfxMagicImpact, position, defaultDuration, null), faction);
    }

    public void PlayOrbProjectile(Vector3 fromPos, Vector3 toPos, int facingDir = 1,
        Transform target = null, VfxFaction faction = VfxFaction.Ally,
        GameObject flyOverride = null, GameObject impactOverride = null)
    {
        // 仅使用调用方传入的阵营资源；禁止静默回退 Ally 球
        GameObject fly = flyOverride;
        GameObject impact = impactOverride;
        if (fly == null)
        {
            Debug.LogWarning($"[VFX] PlayOrbProjectile 无飞行体 faction={faction}");
            return;
        }
        StartCoroutine(ProjectileFlightCoroutine(fly, impact, fromPos, toPos, facingDir, target, faction));
    }

    public void PlayBowProjectile(Vector3 fromPos, Vector3 toPos, int facingDir = 1,
        Transform target = null, VfxFaction faction = VfxFaction.Ally,
        GameObject flyOverride = null, GameObject impactOverride = null)
    {
        GameObject fly = flyOverride;
        GameObject impact = impactOverride;
        if (fly == null)
        {
            Debug.LogWarning($"[VFX] PlayBowProjectile 无飞行体 faction={faction}");
            return;
        }
        StartCoroutine(ProjectileFlightCoroutine(fly, impact, fromPos, toPos, facingDir, target, faction));
    }

    public void PlayProjectile(Vector3 fromPos, Vector3 toPos, int facingDir = 1, Transform target = null)
    {
        PlayOrbProjectile(fromPos, toPos, facingDir, target, VfxFaction.Ally);
    }

    /// <summary>
    /// 技能子弹：从施法者飞到目标，飞到了才结算（onImpact）。
    /// 远程怪放技能必须走这里，否则伤害瞬发、玩家看不到子弹。
    /// 飞行体缺失时不能把伤害吞掉，直接立刻回调。
    /// </summary>
    public void PlaySkillProjectile(VfxFaction faction, Vector3 fromPos, Vector3 toPos,
        int facingDir, Transform target, AttackVfxKit kit = AttackVfxKit.Orb,
        GameObject impactOverride = null, float scaleMul = 1f, float speedMul = 1f,
        System.Action onImpact = null)
    {
        if (kit != AttackVfxKit.Bow && kit != AttackVfxKit.Orb)
            kit = AttackVfxKit.Orb;

        GameObject fly = LoadSharedKit(kit, faction, "fly");
        GameObject impact = impactOverride != null ? impactOverride : LoadSharedKit(kit, faction, "hit");

        if (fly == null)
        {
            Debug.LogWarning($"[VFX] 技能子弹缺少飞行体: {faction}/{kit}/fly，伤害直接结算");
            onImpact?.Invoke();
            if (impact != null)
                ApplyFactionLook(SpawnVFX(impact, toPos, defaultDuration, null), faction);
            return;
        }

        Vector3 from = fromPos;
        if (kit == AttackVfxKit.Bow) from.y += bowFireYOffset;
        StartCoroutine(ProjectileFlightCoroutine(fly, impact, from, toPos, facingDir, target, faction,
            scaleMul, speedMul, onImpact));
    }

    IEnumerator ProjectileFlightCoroutine(GameObject projectilePrefab, GameObject impactPrefab,
        Vector3 fromPos, Vector3 toPos, int facingDir, Transform target, VfxFaction faction,
        float scaleMul = 1f, float speedMul = 1f, System.Action onImpact = null)
    {
        // 飞向受击点（躯干中心），不要压成水平贴地
        Vector3 end = toPos;
        Vector3 flightDir = end - fromPos;
        float distance = flightDir.magnitude;
        if (distance < 0.05f) distance = 0.05f;
        Vector3 dirN = flightDir.sqrMagnitude > 1e-8f
            ? flightDir.normalized
            : Vector3.right * (facingDir >= 0 ? 1f : -1f);
        float speed = Mathf.Max(0.1f, projectileSpeed * Mathf.Max(0.05f, speedMul));
        float duration = Mathf.Clamp(distance / speed, minFlightTime, maxFlightTime);
        speed = distance / Mathf.Max(0.0001f, duration);

        GameObject projectile = Instantiate(projectilePrefab, fromPos, Quaternion.identity);
        projectile.transform.SetParent(transform);
        projectile.transform.localScale = projectilePrefab.transform.localScale
            * Mathf.Max(0.05f, scaleMul);

        ApplyVfxFacing(projectile, 1);
        ApplyFactionLook(projectile, faction);
        SetVFXSortingLayer(projectile.transform);
        PlayAllParticles(projectile);

        var sr = projectile.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.flipX = false;

        AlignProjectileToward(projectile.transform, dirN);

        float traveled = 0f;
        while (traveled < distance)
        {
            float step = speed * Time.deltaTime;
            traveled += step;
            float t = Mathf.Clamp01(traveled / distance);
            projectile.transform.position = Vector3.Lerp(fromPos, end, t);
            yield return null;
        }

        projectile.transform.position = end;
        Destroy(projectile);

        // 命中结算放在特效之前：目标已死也要让调用方知道子弹到了
        onImpact?.Invoke();

        if (impactPrefab == null) yield break;
        // 命中点用传入的 toPos（GetHitPosition），禁止改成脚底 transform.position
        GameObject impact = SpawnVFX(impactPrefab, toPos, defaultDuration, null);
        if (impact == null) yield break;
        ApplyFactionLook(impact, faction);
        ApplyVfxFacing(impact, facingDir);
        if (scaleMul > 1.01f)
            impact.transform.localScale *= Mathf.Min(scaleMul, 2f);
    }

    /// <summary>让飞行物尖端冲向飞行方向（贴图默认朝向用 projectileAngleOffset 校正）</summary>
    void AlignProjectileToward(Transform t, Vector3 dir)
    {
        if (t == null || dir.sqrMagnitude < 1e-8f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + projectileAngleOffset;
        t.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    #endregion

    #region 其它技能/通用

    public void PlaySkillVFX(SkillEffectType effectType, Vector3 position, int facingDir = 1, VfxFaction faction = VfxFaction.Ally)
    {
        switch (effectType)
        {
            case SkillEffectType.Slash: PlaySlash(position, facingDir, faction); break;
            case SkillEffectType.MagicImpact: PlayMagicImpact(position, null, faction); break;
            case SkillEffectType.Fireball: PlayFireball(position, facingDir, faction); break;
            case SkillEffectType.IceImpact: PlayIceImpact(position, faction); break;
            case SkillEffectType.Lightning: PlayLightning(position, faction); break;
            case SkillEffectType.Heal: PlayHeal(position, faction); break;
            case SkillEffectType.Shield: PlayShield(position, faction); break;
            case SkillEffectType.ExplosionBig: PlayExplosionBig(position, faction); break;
            case SkillEffectType.ExplosionSmall: PlayExplosionSmall(position, faction); break;
            case SkillEffectType.Critical: PlayCritical(position, null, faction); break;
            case SkillEffectType.LevelUp: PlayLevelUp(position); break;
        }
    }

    public void PlayFireball(Vector3 position, int facingDir = 1, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxFireball == null) return;
        GameObject go = SpawnVFX(vfxFireball, position, 3f);
        ApplyVfxFacing(go, facingDir);
        ApplyFactionLook(go, faction);
    }

    public void PlayFireImpact(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxFireImpact == null) vfxFireImpact = LoadVFX("Projectile Fire Impact");
        if (vfxFireImpact == null) return;
        ApplyFactionLook(SpawnVFX(vfxFireImpact, position, defaultDuration), faction);
    }

    public void PlayIceImpact(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxIceImpact == null) vfxIceImpact = LoadVFX("Projectile Ice Impact");
        if (vfxIceImpact == null) return;
        ApplyFactionLook(SpawnVFX(vfxIceImpact, position, defaultDuration), faction);
    }

    public void PlayLightning(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxLightning == null) vfxLightning = LoadVFX("Lightning");
        if (vfxLightning == null) return;
        ApplyFactionLook(SpawnVFX(vfxLightning, position, defaultDuration), faction);
    }

    public void PlayHeal(Vector3 position, VfxFaction faction = VfxFaction.Ally, GameObject prefabOverride = null)
    {
        if (!_prefabsLoaded) AutoLoadPrefabs();
        GameObject prefab = prefabOverride != null ? prefabOverride : (vfxHeal != null ? vfxHeal : LoadSharedKit(AttackVfxKit.Heal, faction, "hit"));
        if (prefab == null) return;
        GameObject go = PlayPreparedVfx(prefab, position, 1, 1.5f, 1f, prepareParticles: true);
        ApplyFactionLook(go, faction);
    }

    public void PlayShield(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxShield == null) vfxShield = LoadVFX("Shield");
        if (vfxShield == null) return;
        ApplyFactionLook(SpawnVFX(vfxShield, position, 3f), faction);
    }

    public void PlayCritical(Vector3 position, Transform target = null, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxCritical == null) vfxCritical = LoadVFX("Critical Attack");
        if (vfxCritical == null) return;
        ApplyFactionLook(SpawnVFX(vfxCritical, position, 1f, target), faction);
    }

    public void PlayExplosionBig(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxExplosionBig == null) return;
        ApplyFactionLook(SpawnVFX(vfxExplosionBig, position, 2f), faction);
    }

    public void PlayExplosionSmall(Vector3 position, VfxFaction faction = VfxFaction.Ally)
    {
        if (vfxExplosionSmall == null) return;
        ApplyFactionLook(SpawnVFX(vfxExplosionSmall, position, 1.5f), faction);
    }

    public void PlayLevelUp(Vector3 position)
    {
        if (vfxLevelUp == null) vfxLevelUp = LoadVFX("Level Up");
        if (vfxLevelUp == null) return;
        SpawnVFX(vfxLevelUp, position, 2f);
    }

    public void PlayDust(Vector3 position)
    {
        if (vfxDust == null) vfxDust = LoadVFX("Dust");
        if (vfxDust == null) return;
        SpawnVFX(vfxDust, position, 0.5f);
    }

    void ApplyFactionLook(GameObject go, VfxFaction faction)
    {
        if (go == null) return;
        Color tint = faction == VfxFaction.Ally ? allyTint : enemyTint;

        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            srs[i].color = Mul(srs[i].color, tint);

        var imgs = go.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        for (int i = 0; i < imgs.Length; i++)
            imgs[i].color = Mul(imgs[i].color, tint);

        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var main = pss[i].main;
            // 池化复用前须 ResetTintableColors；这里只乘一次阵营色，禁止反复累乘变透明
            main.startColor = Mul(main.startColor.color, tint);
        }
    }

    static void ResetTintableColors(GameObject go)
    {
        if (go == null) return;
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            srs[i].color = Color.white;

        var imgs = go.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        for (int i = 0; i < imgs.Length; i++)
            imgs[i].color = Color.white;

        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var main = pss[i].main;
            main.startColor = Color.white;
        }
    }

    /// <summary>
    /// Ally 刀光根 PS 常为 RenderMode=None（只给子 Slash 当容器）；
    /// 有材质却被设成 None / MaxParticleSize 过小的子项，强制 Billboard 并放开尺寸上限。
    /// </summary>
    static void EnsureSlashParticleVisible(GameObject go)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            bool hasMat = r.sharedMaterial != null;
            if (hasMat && r.renderMode == ParticleSystemRenderMode.None)
                r.renderMode = ParticleSystemRenderMode.Billboard;
            if (r.maxParticleSize < 2f)
                r.maxParticleSize = 100f;
            r.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            r.sortingOrder = GameConfig.SORT_VFX;
        }
    }

    static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    #endregion

    #region 生成与排序

    HashSet<string> _vfxPoolWarmed = new HashSet<string>();

    /// <summary>技能等世界特效：统一安全播放（先摆位/朝向/修粒子，再 Play）。</summary>
    public GameObject PlayWorldPrefab(GameObject prefab, Vector3 position, float lifetime = 2.5f)
    {
        return PlayWorldPrefab(prefab, position, lifetime, 1);
    }

    public GameObject PlayWorldPrefab(GameObject prefab, Vector3 position, float lifetime, int facingDir)
    {
        return PlayPreparedVfx(prefab, position, facingDir, lifetime, 1f, prepareParticles: true);
    }

    /// <summary>
    /// 所有世界特效唯一生成入口：取池 → 世界坐标 → 2D 朝向 →（可选）修粒子 → 染色复位 → Play → 回收。
    /// 禁止在调用方再手搓 Instantiate + Rotate Y。
    /// </summary>
    public GameObject PlayPreparedVfx(GameObject prefab, Vector3 position, int facingDir, float lifetime,
        float scaleMul = 1f, bool prepareParticles = true)
    {
        if (!_prefabsLoaded) AutoLoadPrefabs();
        GameObject go = AcquireVfxInstance(prefab, position);
        if (go == null) return null;

        Vector3 baseScale = prefab.transform.localScale;
        float mul = Mathf.Max(0.01f, scaleMul);
        go.transform.localScale = new Vector3(
            Mathf.Abs(baseScale.x) * mul * (facingDir < 0 ? -1f : 1f),
            baseScale.y * mul,
            baseScale.z * mul);

        if (prepareParticles)
            PrepareSlashParticles(go);
        else
            SetVFXSortingLayer(go.transform);

        ResetTintableColors(go);
        PlayAllParticles(go);
        ScheduleRelease(go, lifetime);
        return go;
    }

    GameObject SpawnVFX(GameObject prefab, Vector3 position, float lifetime, Transform parentTarget = null)
    {
        // 兼容旧调用：统一走安全播放；parentTarget 仅在非空时重挂
        GameObject go = PlayPreparedVfx(prefab, position, 1, lifetime, 1f, prepareParticles: true);
        if (go != null && parentTarget != null)
            go.transform.SetParent(parentTarget, true);
        return go;
    }

    IEnumerator CoReleaseVfx(GameObject go, float lifetime)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, lifetime));
        if (go == null) yield break;
        if (PoolManager.Instance != null)
            PoolManager.Instance.Release(go);
        else
            Destroy(go);
    }

    static void PlayAllParticles(GameObject go)
    {
        if (go == null) return;
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            pss[i].Clear(true);
            pss[i].Play(true);
        }
    }

    /// <summary>2D 侧视：用 scale.x 翻转，禁止 Rotate Y（会把 Billboard 侧对镜头）。</summary>
    static void ApplyVfxFacing(GameObject go, int facingDir)
    {
        if (go == null) return;
        Vector3 ls = go.transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = absX * (facingDir < 0 ? -1f : 1f);
        go.transform.localScale = ls;
    }

    void SetVFXSortingLayer(Transform t)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_VFX;
        }
        var psr = t.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            psr.sortingOrder = GameConfig.SORT_VFX;
        }
        foreach (Transform child in t)
            SetVFXSortingLayer(child);
    }

    #endregion

    #region 自动加载

    bool _prefabsLoaded;

    public void AutoLoadPrefabs()
    {
        if (_prefabsLoaded) return;
        _prefabsLoaded = true;
        _sharedKitCache.Clear();

        // 优先 Resources/VFX/Shared（用户配置的套装），覆盖旧 Pixel Craft 引用
        GameObject allyMelee = LoadSharedKit(AttackVfxKit.MeleeSlash, VfxFaction.Ally, "hit");
        GameObject allyBowFly = LoadSharedKit(AttackVfxKit.Bow, VfxFaction.Ally, "fly");
        GameObject allyBowHit = LoadSharedKit(AttackVfxKit.Bow, VfxFaction.Ally, "hit");
        GameObject allyOrbFly = LoadSharedKit(AttackVfxKit.Orb, VfxFaction.Ally, "fly");
        GameObject allyOrbHit = LoadSharedKit(AttackVfxKit.Orb, VfxFaction.Ally, "hit");
        GameObject allyHeal = LoadSharedKit(AttackVfxKit.Heal, VfxFaction.Ally, "hit");
        // 敌方套装：开战前各加载一次进缓存，避免战斗中首次命中同步 Load
        LoadSharedKit(AttackVfxKit.MeleeSlash, VfxFaction.Enemy, "hit");
        GameObject enemyBowFly = LoadSharedKit(AttackVfxKit.Bow, VfxFaction.Enemy, "fly");
        GameObject enemyBowHit = LoadSharedKit(AttackVfxKit.Bow, VfxFaction.Enemy, "hit");
        LoadSharedKit(AttackVfxKit.Orb, VfxFaction.Enemy, "fly");
        LoadSharedKit(AttackVfxKit.Orb, VfxFaction.Enemy, "hit");

        // 刀光必须用 Ally Shared；禁止留下旧 Pixel Craft / 敌方资源
        if (allyMelee != null) vfxSlash = allyMelee;
        else Debug.LogError("[BattleVFXSystem] Ally MeleeSlash 缺失: Resources/VFX/Shared/Ally/MeleeSlash/vfx_melee_hit");
        if (allyOrbHit != null) vfxMagicImpact = allyOrbHit;
        if (allyHeal != null) vfxHeal = allyHeal;
        if (allyOrbFly != null) vfxFireball = allyOrbFly;

        // Shared 缺失时再读 Pixel Craft，并打 Error（避免静默用错刀光）
        if (vfxSlash == null)
        {
            vfxSlash = LoadVFX("Sword Slash");
            if (vfxSlash != null)
                Debug.LogError("[BattleVFXSystem] 刀光回退到 Pixel Craft「Sword Slash」——请补 Shared/Ally/MeleeSlash/vfx_melee_hit");
        }
        if (vfxMagicImpact == null) vfxMagicImpact = LoadVFX("Magic Impact");
        if (vfxHeal == null) vfxHeal = LoadVFX("Heal");
        if (vfxFireball == null) vfxFireball = LoadVFX("Fireball");

        ValidateSharedKitPresence();

        // 低频特效按需加载，启动时不批量 Resources.Load
        vfxExplosionSmall = null;
        vfxExplosionBig = null;

        GamePerf.Log($"[BattleVFXSystem] VFX就绪 allyBow={allyBowFly != null}/{allyBowHit != null} enemyBow={enemyBowFly != null}/{enemyBowHit != null} orb={vfxFireball != null}");
    }

    /// <summary>开战自检：缺共用套立刻报错，避免战斗中才发现。</summary>
    void ValidateSharedKitPresence()
    {
        void Need(AttackVfxKit kit, VfxFaction faction, string stage)
        {
            if (LoadSharedKit(kit, faction, stage) != null) return;
            Debug.LogError($"[BattleVFXSystem] 缺共用特效: {SkillNaming.SharedKitResourceHint(kit, faction)} ({stage})");
        }
        Need(AttackVfxKit.MeleeSlash, VfxFaction.Ally, "hit");
        Need(AttackVfxKit.Bow, VfxFaction.Ally, "fly");
        Need(AttackVfxKit.Bow, VfxFaction.Ally, "hit");
        Need(AttackVfxKit.Orb, VfxFaction.Ally, "fly");
        Need(AttackVfxKit.Orb, VfxFaction.Ally, "hit");
        Need(AttackVfxKit.Heal, VfxFaction.Ally, "hit");
        Need(AttackVfxKit.Bow, VfxFaction.Enemy, "fly");
        Need(AttackVfxKit.Bow, VfxFaction.Enemy, "hit");
        Need(AttackVfxKit.Orb, VfxFaction.Enemy, "fly");
        Need(AttackVfxKit.Orb, VfxFaction.Enemy, "hit");
        Need(AttackVfxKit.MeleeSlash, VfxFaction.Enemy, "hit");
    }

    /// <summary>加载共用普攻套：Resources/VFX/Shared/{Ally|Enemy}/{Kit}/vfx_*</summary>
    public GameObject LoadSharedKit(AttackVfxKit kit, VfxFaction faction, string stage)
    {
        string kitFolder = GetKitFolderName(kit);
        if (string.IsNullOrEmpty(kitFolder)) return null;

        string factionFolder = faction == VfxFaction.Enemy ? "Enemy" : "Ally";
        string cacheKey = $"{factionFolder}/{kitFolder}/{stage}";
        if (_sharedKitCache.TryGetValue(cacheKey, out GameObject cached) && cached != null)
            return cached;

        string[] fileNames = GetSharedKitFileNames(kit, faction, stage);
        GameObject prefab = null;
        for (int i = 0; i < fileNames.Length; i++)
        {
            // 敌方弓箭写死：VFX/Shared/Enemy/Bow/vfx_enemy_bow_fly|hit
            string path = $"VFX/Shared/{factionFolder}/{kitFolder}/{fileNames[i]}";
            prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                _sharedKitCache[cacheKey] = prefab;
                return prefab;
            }
        }

        // 敌方缺失：不再静默回退我方，避免「敌方也在用我方箭」
        Debug.LogWarning($"[VFX] 加载失败: {factionFolder}/{kitFolder}/{stage} paths=[{string.Join(",", fileNames)}]（请放 Resources/VFX/Shared/{factionFolder}/{kitFolder}/）");
        return null;
    }

    static string GetKitFolderName(AttackVfxKit kit)
    {
        switch (kit)
        {
            case AttackVfxKit.MeleeSlash: return "MeleeSlash";
            case AttackVfxKit.Bow: return "Bow";
            case AttackVfxKit.Orb: return "Orb";
            case AttackVfxKit.Heal: return "Heal";
            default: return null;
        }
    }

    /// <summary>
    /// 文件名按阵营区分。敌方弓箭必须用 vfx_enemy_bow_*，勿与 Ally 的 vfx_bow_* 同名。
    /// </summary>
    static string[] GetSharedKitFileNames(AttackVfxKit kit, VfxFaction faction, string stage)
    {
        bool enemy = faction == VfxFaction.Enemy;
        if (stage == "fly")
        {
            if (kit == AttackVfxKit.Bow)
                return enemy
                    ? new[] { "vfx_enemy_bow_fly", "vfx_bow_fly" }
                    : new[] { "vfx_bow_fly" };
            if (kit == AttackVfxKit.Orb)
                return enemy
                    ? new[] { "vfx_enemy_orb_fly", "vfx_orb_fly" }
                    : new[] { "vfx_orb_fly" };
        }
        if (stage == "hit")
        {
            if (kit == AttackVfxKit.MeleeSlash)
                return enemy
                    ? new[] { "vfx_enemy_melee_hit", "vfx_melee_hit" }
                    : new[] { "vfx_melee_hit" };
            if (kit == AttackVfxKit.Bow)
                return enemy
                    ? new[] { "vfx_enemy_bow_hit", "vfx_bow_hit" }
                    : new[] { "vfx_bow_hit" };
            if (kit == AttackVfxKit.Orb)
                return enemy
                    ? new[] { "vfx_enemy_orb_hit", "vfx_orb_hit" }
                    : new[] { "vfx_orb_hit" };
            if (kit == AttackVfxKit.Heal) return new[] { "vfx_heal" };
        }
        return new string[0];
    }

    GameObject LoadVFX(string vfxFolderName)
    {
        string key = vfxFolderName.Replace(" ", "_");
        string[] patterns = {
            $"VFX/{vfxFolderName}/Particles/VFX_{key}_01_Color",
            $"VFX/{vfxFolderName}/Particles/VFX_{key}_01_Color.prefab",
        };
        foreach (string pattern in patterns)
        {
            GameObject prefab = Resources.Load<GameObject>(pattern);
            if (prefab != null) return prefab;
        }
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            $"t:Prefab VFX_{key}_01_Color",
            new[] { "Assets/Art/Effects/Pixel Craft VFX URP/VFX" });
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        Debug.LogWarning($"[BattleVFXSystem] 未找到VFX: {vfxFolderName}");
        return null;
    }

    #endregion
}
