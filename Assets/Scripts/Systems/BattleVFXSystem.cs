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
    public float projectileSpeed = 24f;
    /// <summary>飞行贴图默认朝向修正：贴图尖端朝右=0，朝左=180，朝上=-90</summary>
    public float projectileAngleOffset = 0f;
    public float minFlightTime = 0.02f;
    public float maxFlightTime = 1.2f;
    /// <summary>弓箭发射点相对 GetFirePosition 再往下偏（世界单位）；越大越靠上</summary>
    public float bowFireYOffset = -0.03f;

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
                    if (hit == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少共享特效: {faction}/{kit}/hit");
                        return;
                    }
                    PlaySlash(toPos, facingDir, faction, hit);
                }
                break;
            case AttackVfxKit.Bow:
                {
                    GameObject fly = LoadSharedKit(kit, faction, "fly");
                    GameObject hit = LoadSharedKit(kit, faction, "hit");
                    if (fly == null && hit == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少共享特效: {faction}/{kit}");
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
                    if (fly == null && hit == null)
                    {
                        Debug.LogWarning($"[VFX] 缺少共享特效: {faction}/{kit}");
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
        GameObject prefab = prefabOverride != null ? prefabOverride : vfxSlash;
        if (prefab == null)
        {
            Debug.LogWarning("[VFX] vfxSlash 未赋值");
            return;
        }
        // 刀光打在受击点世界坐标，不挂到攻击者/受击者身上；大小跟预制体
        GameObject go = SpawnVFX(prefab, position, defaultDuration, null);
        ApplyVfxFacing(go, facingDir);
        ApplyFactionLook(go, faction);
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
        GameObject fly = flyOverride != null ? flyOverride : (vfxFireball != null ? vfxFireball : vfxMagicImpact);
        GameObject impact = impactOverride != null ? impactOverride : (vfxMagicImpact != null ? vfxMagicImpact : vfxFireImpact);
        if (fly == null) return;
        StartCoroutine(ProjectileFlightCoroutine(fly, impact, fromPos, toPos, facingDir, target, faction));
    }

    public void PlayBowProjectile(Vector3 fromPos, Vector3 toPos, int facingDir = 1,
        Transform target = null, VfxFaction faction = VfxFaction.Ally,
        GameObject flyOverride = null, GameObject impactOverride = null)
    {
        GameObject fly = flyOverride != null ? flyOverride : (vfxFireball != null ? vfxFireball : vfxMagicImpact);
        GameObject impact = impactOverride != null ? impactOverride : (vfxSlash != null ? vfxSlash : vfxMagicImpact);
        if (fly == null) return;
        StartCoroutine(ProjectileFlightCoroutine(fly, impact, fromPos, toPos, facingDir, target, faction));
    }

    public void PlayProjectile(Vector3 fromPos, Vector3 toPos, int facingDir = 1, Transform target = null)
    {
        PlayOrbProjectile(fromPos, toPos, facingDir, target, VfxFaction.Ally);
    }

    IEnumerator ProjectileFlightCoroutine(GameObject projectilePrefab, GameObject impactPrefab,
        Vector3 fromPos, Vector3 toPos, int facingDir, Transform target, VfxFaction faction)
    {
        // 起飞时锁定终点，全程水平直线；匀速飞行（不再用 minTime 拉长近距，否则近处会变慢）
        Vector3 end = toPos;
        end.y = fromPos.y;
        Vector3 flightDir = end - fromPos;
        float distance = flightDir.magnitude;
        if (distance < 0.05f) distance = 0.05f;
        Vector3 dirN = flightDir.sqrMagnitude > 1e-8f ? flightDir.normalized : Vector3.right * (facingDir >= 0 ? 1f : -1f);
        float speed = Mathf.Max(0.1f, projectileSpeed);

        GameObject projectile = Instantiate(projectilePrefab, fromPos, Quaternion.identity);
        projectile.transform.SetParent(transform);
        projectile.transform.localScale = projectilePrefab.transform.localScale;

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

        if (impactPrefab == null) yield break;
        Vector3 impactPos = toPos;
        if (target != null) impactPos = target.position;
        GameObject impact = SpawnVFX(impactPrefab, impactPos, defaultDuration, null);
        ApplyFactionLook(impact, faction);
        ApplyVfxFacing(impact, facingDir);
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
        GameObject prefab = prefabOverride != null ? prefabOverride : vfxHeal;
        if (prefab == null) return;
        ApplyFactionLook(SpawnVFX(prefab, position, 1.5f, null), faction);
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
            main.startColor = Mul(main.startColor.color, tint);
        }
    }

    static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    #endregion

    #region 生成与排序

    GameObject SpawnVFX(GameObject prefab, Vector3 position, float lifetime, Transform parentTarget = null)
    {
        if (prefab == null) return null;
        // 保留预制体旋转与缩放，只改世界坐标
        GameObject go = Instantiate(prefab, position, prefab.transform.rotation);
        Vector3 prefabScale = prefab.transform.localScale;
        if (parentTarget != null)
            go.transform.SetParent(parentTarget, true);
        else
            go.transform.SetParent(transform, true);
        go.transform.localScale = prefabScale;

        SetVFXSortingLayer(go.transform);
        PlayAllParticles(go);
        Destroy(go, lifetime);
        return go;
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

    /// <summary>只改朝向，不改缩放——大小一律跟预制体</summary>
    static void ApplyVfxFacing(GameObject go, int facingDir)
    {
        if (go == null) return;
        if (facingDir < 0)
            go.transform.Rotate(0f, 180f, 0f, Space.Self);
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

        if (allyMelee != null) vfxSlash = allyMelee;
        if (allyOrbHit != null) vfxMagicImpact = allyOrbHit;
        if (allyHeal != null) vfxHeal = allyHeal;
        if (allyOrbFly != null) vfxFireball = allyOrbFly;

        // 兜底：Shared 没有时再读 Pixel Craft（仅缺省项）
        if (vfxSlash == null) vfxSlash = LoadVFX("Sword Slash");
        if (vfxMagicImpact == null) vfxMagicImpact = LoadVFX("Magic Impact");
        if (vfxHeal == null) vfxHeal = LoadVFX("Heal");
        if (vfxFireball == null) vfxFireball = LoadVFX("Fireball");

        // 低频特效按需加载，启动时不批量 Resources.Load
        vfxExplosionSmall = null;
        vfxExplosionBig = null;

        GamePerf.Log($"[BattleVFXSystem] VFX就绪 allyBow={allyBowFly != null}/{allyBowHit != null} enemyBow={enemyBowFly != null}/{enemyBowHit != null} orb={vfxFireball != null}");
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

        string[] fileNames = GetSharedKitFileNames(kit, stage);
        GameObject prefab = null;
        for (int i = 0; i < fileNames.Length; i++)
        {
            string path = $"VFX/Shared/{factionFolder}/{kitFolder}/{fileNames[i]}";
            prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                _sharedKitCache[cacheKey] = prefab;
                return prefab;
            }
        }

        // 敌方缺失：不再静默回退我方，避免「敌方也在用我方箭」
        Debug.LogWarning($"[VFX] 加载失败: {factionFolder}/{kitFolder}/{stage}（请把预制体放到 Resources/VFX/Shared/{factionFolder}/{kitFolder}/）");
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

    static string[] GetSharedKitFileNames(AttackVfxKit kit, string stage)
    {
        if (stage == "fly")
        {
            if (kit == AttackVfxKit.Bow) return new[] { "vfx_bow_fly" };
            if (kit == AttackVfxKit.Orb) return new[] { "vfx_orb_fly" };
        }
        if (stage == "hit")
        {
            if (kit == AttackVfxKit.MeleeSlash) return new[] { "vfx_melee_hit" };
            if (kit == AttackVfxKit.Bow) return new[] { "vfx_bow_hit" };
            if (kit == AttackVfxKit.Orb) return new[] { "vfx_orb_hit" };
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
