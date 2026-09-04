using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 通关宝箱流程：按关卡换箱皮 → open1→open2 → 金币飞入 → 三选一 → chuansongmen。
/// 箱皮：普通 mubox（小概率 yinbox）；精英 yinbox（小概率 jinbox）；Boss jinbox。
/// 尺寸：yin=mu×1.2，jin=yin×1.2；粒子跟缩放变大，动画曲线/本地坐标不变。
/// </summary>
public class StageClearRewardDirector : MonoBehaviour
{
    public static StageClearRewardDirector Instance { get; private set; }

    const float BoxUpgradeChance = 0.12f;
    const float YinScaleMul = 1.2f;
    const float JinScaleMul = 1.2f * 1.2f; // 相对木箱

    Transform _boxRoot;
    Transform _boxAnimHost;
    Transform _effectRoot;
    SpriteRenderer _closeSr;
    SpriteRenderer _openSr;
    Animator _boxAnim;
    Transform _chuansongmen;
    Vector3 _boxBaseScale = Vector3.one;
    Vector3 _effectBaseScale = Vector3.one;
    Vector3 _boxScenePos;
    bool _boxScenePosCached;
    bool _running;

    public bool IsRunning => _running;
    public Transform ChuanSongMen => _chuansongmen;

    /// <summary>教程宝箱世界 X（未放置时退回玩家前方）。</summary>
    public float ChestWorldX
    {
        get
        {
            if (_boxRoot != null && _boxRoot.gameObject.activeInHierarchy)
                return _boxRoot.position.x;
            var hero = Hero.Instance;
            return hero != null ? UnitBase.GetCombatX(hero) + 4f : 0f;
        }
    }

    public bool IsBoxVisible =>
        _boxRoot != null && _boxRoot.gameObject.activeInHierarchy
        && _closeSr != null && _closeSr.enabled;

    /// <summary>清场后把玩家拉回宝箱前，面向宝箱。</summary>
    public void SnapHeroBeforeChest(float standOffset = 2.35f)
    {
        var hero = Hero.Instance;
        if (hero == null || _boxRoot == null) return;
        float boxX = _boxRoot.position.x;
        float targetX = boxX - standOffset;
        float hx = UnitBase.GetCombatX(hero);
        if (Mathf.Abs(hx - targetX) > 1.5f)
        {
            var p = hero.transform.position;
            p.x = targetX;
            GameConfig.SetWorldPosition(hero.gameObject, p);
        }
        hero.Face(1);
        if (hero.rb != null) hero.rb.velocity = Vector2.zero;
    }

    public void HideBoxVisual()
    {
        StopBoxEffect();
        if (_boxRoot != null) _boxRoot.gameObject.SetActive(false);
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void CacheSceneRefs()
    {
        // 每次进战必须按当前场景重采，避免 DDOL 导演沿用上一局坐标/缩放
        InvalidateSceneCache();

        var wr = GameObject.Find("WorldRoot");
        Transform root = wr != null ? wr.transform : null;
        // 外层 WorldRoot/box（缩放挂这里，不动动画本地曲线）
        _boxRoot = null;
        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (string.Equals(c.name, "box", System.StringComparison.OrdinalIgnoreCase))
                {
                    _boxRoot = c;
                    break;
                }
            }
        }
        if (_boxRoot == null)
            _boxRoot = FindChildIgnoreCase(null, "box");

        _chuansongmen = FindChildIgnoreCase(root, "chuansongmen") ?? FindChildIgnoreCase(null, "chuansongmen");

        if (_boxRoot != null)
        {
            _boxAnim = _boxRoot.GetComponentInChildren<Animator>(true);
            _boxAnimHost = _boxAnim != null ? _boxAnim.transform : _boxRoot;
            var closeT = FindChildIgnoreCase(_boxAnimHost, "close");
            var openT = FindChildIgnoreCase(_boxAnimHost, "open");
            _closeSr = closeT != null ? closeT.GetComponent<SpriteRenderer>() : null;
            _openSr = openT != null ? openT.GetComponent<SpriteRenderer>() : null;
            _effectRoot = FindChildIgnoreCase(_boxAnimHost, "effect");
            _boxBaseScale = _boxRoot.localScale;
            if (_boxBaseScale == Vector3.zero) _boxBaseScale = Vector3.one;
            _effectBaseScale = _effectRoot != null ? _effectRoot.localScale : Vector3.one;
            if (_effectBaseScale == Vector3.zero) _effectBaseScale = Vector3.one;
            _boxScenePos = _boxRoot.position;
            _boxScenePosCached = true;
            EnsureBoxController();
            _boxRoot.gameObject.SetActive(false);
        }
        if (_chuansongmen != null)
            _chuansongmen.gameObject.SetActive(false);
    }

    /// <summary>切场景 / 重开战后调用，强制下次 CacheSceneRefs 重新采样。</summary>
    public void InvalidateSceneCache()
    {
        _boxScenePosCached = false;
        _boxRoot = null;
        _boxAnimHost = null;
        _effectRoot = null;
        _closeSr = null;
        _openSr = null;
        _boxAnim = null;
        _chuansongmen = null;
        StopAllCoroutines();
        _running = false;
    }

    /// <summary>宝箱在 map 之上、与角色同层，避免被背景挡住。</summary>
    const int BoxSortOrder = GameConfig.SORT_UNIT;

    void EnsureBoxPhysicsDisabled()
    {
        if (_boxRoot == null) return;
        var cols = _boxRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = false;
        var rbs = _boxRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null) continue;
            rbs[i].isKinematic = true;
            rbs[i].velocity = Vector3.zero;
        }
    }

    void ApplyBoxSorting()
    {
        if (_closeSr != null)
        {
            _closeSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _closeSr.sortingOrder = BoxSortOrder;
        }
        if (_openSr != null)
        {
            _openSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _openSr.sortingOrder = BoxSortOrder;
        }
    }

    void EnsureBoxController()
    {
        EnsureBoxPhysicsDisabled();
        ApplyBoxSorting();
        if (_boxAnim == null) return;
#if UNITY_EDITOR
        if (_boxAnim.runtimeAnimatorController == null)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Effects/Ani/box/box.controller");
            if (ctrl != null) _boxAnim.runtimeAnimatorController = ctrl;
        }
#endif
    }

    public static ClearBoxTier ResolveBoxTier(StageType stageType)
    {
        float roll = Random.value;
        switch (stageType)
        {
            case StageType.Boss:
                return ClearBoxTier.Jin;
            case StageType.Elite:
                return roll < BoxUpgradeChance ? ClearBoxTier.Jin : ClearBoxTier.Yin;
            default:
                // 普通及其它功能关：木箱，小概率银箱
                return roll < BoxUpgradeChance ? ClearBoxTier.Yin : ClearBoxTier.Mu;
        }
    }

    static float TierScale(ClearBoxTier tier)
    {
        switch (tier)
        {
            case ClearBoxTier.Yin: return YinScaleMul;
            case ClearBoxTier.Jin: return JinScaleMul;
            default: return 1f;
        }
    }

    static string TierPrefix(ClearBoxTier tier)
    {
        switch (tier)
        {
            case ClearBoxTier.Yin: return "yinbox";
            case ClearBoxTier.Jin: return "jinbox";
            default: return "mubox";
        }
    }

    void ApplyBoxVisual(ClearBoxTier tier)
    {
        if (_boxRoot == null) return;

        float s = TierScale(tier);
        // 缩放挂在外层 box：动画曲线/close·open 本地坐标不变，粒子跟着变大
        _boxRoot.localScale = _boxBaseScale * s;
        if (_effectRoot != null)
            _effectRoot.localScale = _effectBaseScale;

        Sprite closeSp = LoadBoxSprite(TierPrefix(tier) + "_close");
        Sprite openSp = LoadBoxSprite(TierPrefix(tier) + "_open");
        if (_closeSr != null && closeSp != null) _closeSr.sprite = closeSp;
        if (_openSr != null && openSp != null) _openSr.sprite = openSp;
        StopBoxEffect();
    }

    /// <summary>关箱待机：禁用 Animator（open1 第 0 帧会把 close 透明），手动复位 close。</summary>
    void HoldBoxClosedPose()
    {
        StopBoxEffect();
        if (_boxAnim != null)
            _boxAnim.enabled = false;
        if (_closeSr != null)
        {
            _closeSr.gameObject.SetActive(true);
            _closeSr.enabled = true;
            var c = _closeSr.color;
            c.a = 1f;
            _closeSr.color = c;
            _closeSr.transform.localPosition = new Vector3(0f, 1f, 0f);
        }
        if (_openSr != null)
        {
            _openSr.enabled = false;
            _openSr.gameObject.SetActive(true);
        }
        ApplyBoxSorting();
    }

    /// <summary>关箱待机姿态，便于按 close 贴图底边算地面。</summary>
    void PrepareBoxClosedPose()
    {
        HoldBoxClosedPose();
    }

    SpriteRenderer GetBoxGroundSprite()
    {
        if (_closeSr != null && _closeSr.sprite != null) return _closeSr;
        if (_openSr != null && _openSr.sprite != null) return _openSr;
        return null;
    }

    /// <summary>按 close/open 精灵底边贴 GROUND_Y，避免根节点 y=GROUND_Y 导致悬空或沉入地下。</summary>
    void SnapBoxRootToGround(bool useOpenVisual = false)
    {
        if (_boxRoot == null) return;
        if (useOpenVisual)
        {
            if (_openSr != null)
            {
                _openSr.gameObject.SetActive(true);
                _openSr.enabled = true;
            }
            if (_closeSr != null)
                _closeSr.enabled = false;
            if (_boxAnim != null)
                _boxAnim.Update(0f);
        }
        else
        {
            PrepareBoxClosedPose();
        }

        var sr = GetBoxGroundSprite();
        if (sr == null)
        {
            if (_boxScenePosCached)
            {
                var fallback = _boxRoot.position;
                fallback.y = _boxScenePos.y;
                _boxRoot.position = fallback;
            }
            return;
        }

        float dy = UnitBase.GROUND_Y - sr.bounds.min.y;
        if (useOpenVisual)
            dy += 0.12f;
        if (Mathf.Abs(dy) < 0.0005f) return;
        var p = _boxRoot.position;
        float beforeY = p.y;
        p.y += dy;
        _boxRoot.position = p;
        // #region agent log
        DebugAgentLog.Log("H6", "StageClearRewardDirector.SnapBoxRootToGround", "box_snap",
            $"{{\"useOpen\":{(useOpenVisual ? "true" : "false")},\"beforeY\":{beforeY:F3},\"afterY\":{p.y:F3},\"groundY\":{UnitBase.GROUND_Y:F3},\"boundsMinY\":{sr.bounds.min.y:F3}}}");
        // #endregion
    }

    void PlaceBoxAt(float worldX, float worldZ)
    {
        if (_boxRoot == null) return;
        var p = _boxRoot.position;
        p.x = worldX;
        p.z = worldZ;
        _boxRoot.position = p;
        SnapBoxRootToGround();
        ForceBoxRenderersVisible();
    }

    void ForceBoxRenderersVisible()
    {
        if (_boxRoot == null) return;
        _boxRoot.gameObject.SetActive(true);
        var srs = _boxRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].gameObject.SetActive(true);
            srs[i].enabled = true;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            if (srs[i].sortingOrder < BoxSortOrder)
                srs[i].sortingOrder = BoxSortOrder;
        }
        if (_closeSr != null)
        {
            _closeSr.enabled = true;
            _closeSr.gameObject.SetActive(true);
        }
    }

    /// <summary>关箱/待机：不播烟花。</summary>
    void StopBoxEffect()
    {
        if (_effectRoot == null)
            _effectRoot = FindChildIgnoreCase(_boxAnimHost != null ? _boxAnimHost : _boxRoot, "effect");
        if (_effectRoot == null) return;
        var particles = _effectRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>开箱瞬间：撒烟花/粒子（仅 open 时调用）。</summary>
    void PlayBoxOpenEffect()
    {
        if (_boxRoot == null) return;
        if (_effectRoot == null)
            _effectRoot = FindChildIgnoreCase(_boxAnimHost != null ? _boxAnimHost : _boxRoot, "effect");
        if (_effectRoot == null) return;
        if (!_effectRoot.gameObject.activeSelf)
            _effectRoot.gameObject.SetActive(true);
        var particles = _effectRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            if (!particles[i].gameObject.activeSelf)
                particles[i].gameObject.SetActive(true);
            particles[i].Clear(true);
            particles[i].Play(true);
            var pr = particles[i].GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                pr.sortingOrder = GameConfig.SORT_VFX;
            }
        }
        var srs = _effectRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        if (_closeSr != null)
        {
            _closeSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _closeSr.sortingOrder = BoxSortOrder;
        }
        if (_openSr != null)
        {
            _openSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _openSr.sortingOrder = BoxSortOrder;
        }
    }

    static Sprite LoadBoxSprite(string fileNameNoExt)
    {
        Sprite sp = Resources.Load<Sprite>("UI/box/" + fileNameNoExt);
        if (sp != null) return sp;
        Texture2D tex = Resources.Load<Texture2D>("UI/box/" + fileNameNoExt);
#if UNITY_EDITOR
        if (sp == null)
            sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/box/{fileNameNoExt}.png");
        if (tex == null)
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/UI/box/{fileNameNoExt}.png");
        if (sp != null) return sp;
#endif
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public void HideClearProps()
    {
        if (_boxRoot != null) _boxRoot.gameObject.SetActive(false);
        if (_chuansongmen != null) _chuansongmen.gameObject.SetActive(false);
        _running = false;
    }

    /// <summary>
    /// 引导：在玩家前方放置并显示宝箱，等走近（超时则轻推）。
    /// </summary>
    public IEnumerator CoTutorialPlaceChest(float aheadDist = 4f, bool waitForHeroApproach = true)
    {
        CacheSceneRefs();
        if (_boxRoot == null)
        {
            Debug.LogWarning("[StageClearReward] 教程宝箱：场景无 box 节点");
            yield break;
        }

        var hero = Hero.Instance;
        float hx = hero != null ? UnitBase.GetCombatX(hero) : 0f;
        float z = _boxRoot.position.z;
        BattleManager.GetBattleVisibleX(out float visMin, out float visMax);
        float boxX = Mathf.Clamp(hx + aheadDist, visMin + 0.8f, visMax - 0.35f);
        _boxRoot.gameObject.SetActive(true);
        ApplyBoxVisual(ClearBoxTier.Mu);
        PlaceBoxAt(boxX, z);
        ForceBoxRenderersVisible();
        // #region agent log
        DebugAgentLog.Log("H10", "StageClearReward.CoTutorialPlaceChest", "box placed",
            $"{{\"boxX\":{boxX:F2},\"boxActive\":{IsBoxVisible.ToString().ToLower()},\"closeSr\":{(_closeSr != null).ToString().ToLower()}}}");
        // #endregion
        StopBoxEffect();
        EnsureBoxController();
        HoldBoxClosedPose();
        if (_closeSr != null) { _closeSr.enabled = true; _closeSr.gameObject.SetActive(true); }
        if (_openSr != null) { _openSr.enabled = false; _openSr.gameObject.SetActive(true); }

        float wait = 0f;
        const float maxWalkWait = 5f;
        if (waitForHeroApproach)
        while (wait < maxWalkWait)
        {
            wait += Time.unscaledDeltaTime;
            if (hero != null)
            {
                float dist = Mathf.Abs(UnitBase.GetCombatX(hero) - boxX);
                if (dist <= 2.6f) break;
                if (wait > 0.8f && dist > 3f)
                {
                    float step = Mathf.Sign(boxX - UnitBase.GetCombatX(hero))
                        * Mathf.Min(14f * Time.unscaledDeltaTime, dist - 2.2f);
                    if (Mathf.Abs(step) > 0.001f)
                    {
                        var p = hero.transform.position;
                        p.x += step;
                        GameConfig.SetWorldPosition(hero.gameObject, p);
                    }
                }
                if (wait >= maxWalkWait - 0.05f) break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 引导：开箱 → 武器从小变大弹出 → 落地并上下晃动 → 返回地面图标（弹窗在此之前不要开）。
    /// </summary>
    public IEnumerator CoTutorialOpenChestAndDropEquip(EquipInstance drop, System.Action<GameObject> onGroundIcon)
    {
        CacheSceneRefs();
        if (_boxRoot == null)
        {
            onGroundIcon?.Invoke(null);
            yield break;
        }

        _boxRoot.gameObject.SetActive(true);
        EnsureBoxController();
        if (_boxAnim != null)
        {
            _boxAnim.enabled = true;
            _boxAnim.Rebind();
            _boxAnim.Update(0f);
            PlayBoxOpenEffect();
            _boxAnim.Play("open1", 0, 0f);
            yield return WaitAnimOrSeconds(_boxAnim, "open1", 0.9f);
        }
        else
            yield return new WaitForSecondsRealtime(0.4f);

        if (_closeSr != null) _closeSr.enabled = false;
        if (_openSr != null) _openSr.enabled = true;
        SnapBoxRootToGround(useOpenVisual: true);

        GameObject ground = null;
        if (drop != null)
        {
            Vector3 popStart = _boxRoot.position + new Vector3(0.25f, 1.05f, 0f);
            popStart.y = UnitBase.GROUND_Y + 1.05f;
            ground = CreateGroundDrop(popStart, drop);
            if (ground != null)
            {
                Vector3 land = _boxRoot.position + new Vector3(0.85f, 0f, 0f);
                land.y = UnitBase.GROUND_Y;
                var sr = ground.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    land.y += sr.bounds.extents.y;
                yield return CoTutorialEquipPopAndBounce(ground.transform, popStart, land);
            }
        }

        onGroundIcon?.Invoke(ground);
        yield return new WaitForSecondsRealtime(0.25f);
        if (_boxAnim != null) _boxAnim.Play("open2", 0, 0f);
        SnapBoxRootToGround(useOpenVisual: true);
        HideBoxVisual();
    }

    static IEnumerator CoTutorialEquipPopAndBounce(Transform icon, Vector3 start, Vector3 land)
    {
        if (icon == null) yield break;

        const float popDur = 0.38f;
        const float dropDur = 0.34f;
        const float bounceDur = 1.05f;
        Vector3 mid = start + new Vector3(0.15f, 0.45f, 0f);

        float t = 0f;
        while (t < popDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popDur));
            icon.position = Vector3.Lerp(start, mid, u);
            float s = Mathf.Lerp(0.08f, 0.52f, u);
            icon.localScale = Vector3.one * s;
            yield return null;
        }

        t = 0f;
        while (t < dropDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dropDur);
            float arc = Mathf.Sin(u * Mathf.PI) * 0.35f;
            icon.position = Vector3.Lerp(mid, land, u) + new Vector3(0f, arc, 0f);
            yield return null;
        }

        t = 0f;
        float baseY = land.y;
        while (t < bounceDur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / bounceDur;
            float amp = 0.18f * (1f - u);
            icon.position = new Vector3(land.x, baseY + Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5f)) * amp, land.z);
            yield return null;
        }
        icon.position = land;
        icon.localScale = Vector3.one * 0.48f;
    }

    [System.Obsolete("Use CoTutorialPlaceChest + CoTutorialOpenChestAndDropEquip")]
    public IEnumerator CoTutorialChestDrop(EquipInstance drop, float aheadDist = 6.5f)
    {
        yield return CoTutorialPlaceChest(aheadDist);
    }

    [System.Obsolete("Use CoTutorialOpenChestAndDropEquip")]
    public IEnumerator CoTutorialOpenChest(EquipInstance drop, System.Action<GameObject> onGroundIcon)
    {
        yield return CoTutorialOpenChestAndDropEquip(drop, onGroundIcon);
    }

    public void Begin(List<EquipInstance> rewards, int bonusGold, StageType stageType = StageType.Normal)
    {
        if (_running) return;
        CacheSceneRefs();
        StartCoroutine(CoReward(rewards, bonusGold, stageType));
    }

    IEnumerator CoReward(List<EquipInstance> rewards, int bonusGold, StageType stageType)
    {
        _running = true;
        var bm = BattleManager.Instance;
        if (bm != null) bm.UnitsCanAct = false;

        ClearBoxTier tier = ResolveBoxTier(stageType);
        ApplyBoxVisual(tier);
        Debug.Log($"[StageClearReward] 宝箱品质={tier} stage={stageType}");

        // —— 宝箱：用场景脚底高度，不再额外抬高 ——
        if (_boxRoot != null)
        {
            _boxRoot.gameObject.SetActive(true);
            var p = _boxScenePos;
            _boxRoot.position = p;
            SnapBoxRootToGround();
            EnsureBoxController();
            if (_boxAnim != null)
            {
                _boxAnim.enabled = true;
                PlayBoxOpenEffect();
                _boxAnim.Play("open1", 0, 0f);
                yield return WaitAnimOrSeconds(_boxAnim, "open1", 1.15f);
                if (_closeSr != null) _closeSr.enabled = false;
                if (_openSr != null) _openSr.enabled = true;
                SnapBoxRootToGround(useOpenVisual: true);
                // open2 已在控制器里设为循环；播完 open1 后强制切入并保持循环
                _boxAnim.Play("open2", 0, 0f);
                SnapBoxRootToGround(useOpenVisual: true);
                float open2Snap = 0f;
                while (open2Snap < 2.5f)
                {
                    SnapBoxRootToGround(useOpenVisual: true);
                    open2Snap += Time.unscaledDeltaTime;
                    yield return null;
                }
                HideBoxVisual();
            }
            else
                yield return new WaitForSecondsRealtime(0.8f);
        }
        else
        {
            Debug.LogWarning("[StageClearReward] 场景缺少 box 节点，跳过开箱动画");
            yield return new WaitForSecondsRealtime(0.35f);
        }

        // —— 掉落金币飞入资源条 ——
        int goldDrop = Mathf.Max(0, bonusGold);
        if (goldDrop > 0)
        {
            int coinCount = Mathf.Clamp(goldDrop / 5, 6, 18);
            int goldPerCoin = Mathf.Max(1, goldDrop / coinCount);
            int goldRemain = goldDrop;
            Vector3 spawnGold = _boxRoot != null ? _boxRoot.position : Vector3.zero;
            spawnGold.y = UnitBase.GROUND_Y;

            for (int i = 0; i < coinCount; i++)
            {
                int add = (i == coinCount - 1) ? goldRemain : goldPerCoin;
                goldRemain -= add;
                Vector3 land = spawnGold + new Vector3(Random.Range(-0.6f, 0.6f), 0f, 0f);
                yield return StartCoroutine(CoFlyCoin(land, add));
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        // —— 地上装备表现（不可点，仅视觉）——
        Vector3 spawn = _boxRoot != null ? _boxRoot.position : Vector3.zero;
        spawn.y = UnitBase.GROUND_Y;
        var show = new List<EquipInstance>();
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count && show.Count < 3; i++)
                if (rewards[i] != null) show.Add(rewards[i]);
        }
        var groundIcons = new List<GameObject>();
        for (int i = 0; i < show.Count; i++)
        {
            float ox = (i - (show.Count - 1) * 0.5f) * 0.85f;
            var icon = CreateGroundDrop(spawn + new Vector3(ox, 0f, 0f), show[i]);
            if (icon != null)
            {
                var sr = icon.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    var p = icon.transform.position;
                    p.y = UnitBase.GROUND_Y + sr.bounds.extents.y;
                    icon.transform.position = p;
                }
                groundIcons.Add(icon);
            }
        }

        yield return new WaitForSecondsRealtime(0.35f);

        // —— 三选一 ——
        if (show.Count > 0)
        {
            bool uiDone = false;
            EquipInstance picked = null;
            bool doEquip = false;
            StageClearEquipUI.Show(show, bonusGold, (sel, equip) =>
            {
                picked = sel;
                doEquip = equip;
                uiDone = true;
            });
            while (!uiDone) yield return null;

            // 清理地面表现
            for (int i = 0; i < groundIcons.Count; i++)
                if (groundIcons[i] != null) Destroy(groundIcons[i]);

            ApplyEquipChoice(show, picked, doEquip);
        }
        else
        {
            for (int i = 0; i < groundIcons.Count; i++)
                if (groundIcons[i] != null) Destroy(groundIcons[i]);
        }

        // —— 传送门 + 传送特效 ——
        if (_chuansongmen != null)
        {
            float portalX = bm != null && bm.hero != null
                ? UnitBase.GetCombatX(bm.hero) + 4.5f
                : spawn.x + 4f;
            var p = _chuansongmen.position;
            p.x = portalX;
            p.y = UnitBase.GROUND_Y;
            _chuansongmen.position = p;
            _chuansongmen.gameObject.SetActive(true);
            EnsurePortalFx(_chuansongmen);
            PlayPortalOpenVfx(_chuansongmen.position);
            bm?.NotifyChuanSongMenOpened(_chuansongmen);
        }
        else
        {
            Debug.LogWarning("[StageClearReward] 场景缺少 chuansongmen，跳过传送门直接结算");
            bm?.FinishStageAfterPortalReached();
        }

        if (bm != null) bm.UnitsCanAct = true;
        _running = false;
    }

    /// <summary>给 chuansongmen 挂脉动动画（与旧 EndPoint PortalAnimator 同款）。</summary>
    static void EnsurePortalFx(Transform portal)
    {
        if (portal == null) return;
        var anim = portal.GetComponent<PortalAnimator>();
        if (anim == null) anim = portal.GetComponentInChildren<PortalAnimator>(true);
        if (anim == null) anim = portal.gameObject.AddComponent<PortalAnimator>();
        anim.enabled = true;
        anim.Warm();
        // 确保子节点可见
        for (int i = 0; i < portal.childCount; i++)
        {
            var c = portal.GetChild(i);
            if (c != null && !c.gameObject.activeSelf)
                c.gameObject.SetActive(true);
        }
    }

    void ApplyEquipChoice(List<EquipInstance> show, EquipInstance picked, bool doEquip)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        if (picked != null && doEquip)
        {
            if (GridBackpackSystem.Instance != null && GridBackpackSystem.Instance.TryEquipFromReward(picked))
            {
                AchievementSystem.Instance?.OnObtainEquip(picked.rarity);
                AdventureLogAchievements.OnEquipPicked();
                UIManager.Instance?.ShowToast($"已装备：{picked.equipName ?? picked.templateId}");
            }
            else
            {
                int g = ScrapGold(picked);
                bm.currentGold += g;
                BattleUI.Instance?.UpdateGold(bm.currentGold);
                UIManager.Instance?.ShowToast($"穿装失败，折合金币 +{g}");
            }
        }
        else if (picked != null)
        {
            int g = ScrapGold(picked);
            bm.currentGold += g;
            BattleUI.Instance?.UpdateGold(bm.currentGold);
            UIManager.Instance?.ShowToast($"已丢弃，折合金币 +{g}");
        }

        if (show != null)
        {
            int scrapTotal = 0;
            for (int i = 0; i < show.Count; i++)
            {
                var e = show[i];
                if (e == null || e == picked) continue;
                scrapTotal += ScrapGold(e);
            }
            if (scrapTotal > 0)
            {
                bm.currentGold += scrapTotal;
                BattleUI.Instance?.UpdateGold(bm.currentGold);
                UIManager.Instance?.ShowToast($"其余折合金币 +{scrapTotal}");
            }
        }
    }

    static int ScrapGold(EquipInstance e)
    {
        if (e == null) return 0;
        return (int)e.rarity * 5 * (1 + e.star);
    }

    IEnumerator CoFlyCoin(Vector3 from, int goldAdd)
    {
        var coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coin.name = "DropCoin";
        coin.transform.position = from;
        coin.transform.localScale = Vector3.one * 0.18f;
        var col = coin.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = coin.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? rend.material.shader);
            rend.material.color = new Color(1f, 0.85f, 0.2f, 1f);
        }

        Vector3 to = from + new Vector3(0f, 2.5f, 0f);
        var goldText = BattleUI.Instance != null ? BattleUI.Instance.goldText : null;
        if (goldText != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                RectTransform rt = goldText.rectTransform;
                Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, rt.position);
                // Canvas overlay: use screen point as approximate world ahead of camera
                to = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
                to.z = from.z;
            }
        }

        float t = 0f;
        float dur = 0.45f;
        Vector3 start = from;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float ease = u * u * (3f - 2f * u);
            Vector3 p = Vector3.Lerp(start, to, ease);
            p.y += Mathf.Sin(u * Mathf.PI) * 0.4f;
            coin.transform.position = p;
            yield return null;
        }

        Destroy(coin);
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.currentGold += goldAdd;
            BattleUI.Instance?.UpdateGold(BattleManager.Instance.currentGold);
        }
    }

    GameObject CreateGroundDrop(Vector3 pos, EquipInstance eq)
    {
        var go = new GameObject("EquipDrop");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        if (eq != null)
        {
            eq.template?.ResolveIcon();
            if (eq.icon == null && eq.template != null)
                eq.icon = eq.template.icon;
            if (eq.icon == null && eq.template != null)
                eq.icon = EquipIcons.Get(eq.template.iconFileName);
            if (eq.icon != null) sr.sprite = eq.icon;
        }
        sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        sr.sortingOrder = GameConfig.SORT_VFX;
        go.transform.localScale = Vector3.one * 0.45f;
        return go;
    }

    static IEnumerator WaitAnimOrSeconds(Animator anim, string state, float fallback)
    {
        float t = 0f;
        while (t < fallback)
        {
            t += Time.unscaledDeltaTime;
            if (anim != null)
            {
                var info = anim.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(state) && info.normalizedTime >= 0.98f)
                    yield break;
            }
            yield return null;
        }
    }

    /// <summary>播放 Resources/VFX/other/world/传送（开启传送门时的出现特效）。</summary>
    static void PlayPortalOpenVfx(Vector3 worldPos)
    {
        var prefab = Resources.Load<GameObject>("VFX/other/world/传送")
                  ?? Resources.Load<GameObject>("VFX/other/world/portal_open");
        if (prefab == null)
        {
            Debug.LogWarning("[StageClearReward] 未找到特效 Resources/VFX/other/world/传送");
            return;
        }
        var go = Object.Instantiate(prefab, worldPos + new Vector3(0f, 0.15f, 0f), Quaternion.identity);
        go.name = "PortalOpenVfx";
        // 抬到战斗 VFX 层，避免被地图/单位挡住
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        var prs = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < prs.Length; i++)
        {
            if (prs[i] == null) continue;
            prs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            prs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        Object.Destroy(go, 4.5f);
    }

    static Transform FindChildIgnoreCase(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (root == null)
        {
            var all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindChildIgnoreCase(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
