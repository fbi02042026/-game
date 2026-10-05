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
    Transform _fxMu;   // 开箱特效：普通木箱（effect 下节点 "1"）
    Transform _fxYin;  // 开箱特效：稀有银箱（effect 下节点 "2"）
    Transform _fxJin;  // 开箱特效：传奇金箱（effect 下节点 "3"）
    SpriteRenderer _closeSr;
    SpriteRenderer _openSr;
    /// <summary>箱底投影（box 下 "shadow"）。2026-09-28 主人反馈「阴影都不见了」—— 以前没人管它的排序。</summary>
    SpriteRenderer _shadowSr;
    Animator _boxAnim;
    Transform _chuansongmen;
    Vector3 _boxBaseScale = Vector3.one;
    Vector3 _effectBaseScale = Vector3.one;
    Vector3 _boxScenePos;
    bool _boxSnapErrorLogged;
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
        string[] boxNames = { "box", "chest", "treasure", "宝箱", "rewardbox", "clearbox", "chestbox" };
        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                foreach (var bn in boxNames)
                {
                    if (string.Equals(c.name, bn, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _boxRoot = c;
                        break;
                    }
                }
                if (_boxRoot != null) break;
            }
        }
        if (_boxRoot == null)
        {
            foreach (var bn in boxNames)
            {
                _boxRoot = FindChildIgnoreCase(null, bn);
                if (_boxRoot != null) break;
            }
        }
        if (_boxRoot == null)
        {
            string childNames = root != null ? "" : "(无 WorldRoot)";
            if (root != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < root.childCount; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(root.GetChild(i).name);
                }
                childNames = sb.ToString();
            }
            Debug.LogError($"[StageClearReward] 未找到宝箱节点（候选名: {string.Join("/", boxNames)}）。WorldRoot 子节点: {childNames}。请在该场景 WorldRoot 下放置名为 box 的宝箱节点（含 close/open 子精灵）。");
        }

        _chuansongmen = FindChildIgnoreCase(root, "chuansongmen") ?? FindChildIgnoreCase(null, "chuansongmen");

        if (_boxRoot != null)
        {
            _boxAnim = _boxRoot.GetComponentInChildren<Animator>(true);
            _boxAnimHost = _boxAnim != null ? _boxAnim.transform : _boxRoot;
            // 箱皮兜底：_boxRoot 在但 close/open 精灵整体缺失（预制体丢失）时，运行时补建渲染节点。
            // 必须在 _closeSr/_openSr/_effectRoot 赋值之前调用，使后面的查找与贴地采样能拿到刚建好的节点。
            EnsureBoxVisualFallback();
            var closeT = FindChildIgnoreCase(_boxAnimHost, "close");
            var openT = FindChildIgnoreCase(_boxAnimHost, "open");
            _closeSr = closeT != null ? closeT.GetComponent<SpriteRenderer>() : null;
            _openSr = openT != null ? openT.GetComponent<SpriteRenderer>() : null;
            // 2026-09-28 主人反馈「阴影都不见了」：root 下还有个 "shadow" 从来没被采样过，
            // 于是它一直留在预制体的默认分层/order 上，被地图整块盖掉。这里补缓存。
            var shadowT = FindChildIgnoreCase(_boxAnimHost, "shadow")
                          ?? FindChildIgnoreCase(_boxRoot, "shadow");
            _shadowSr = shadowT != null ? shadowT.GetComponent<SpriteRenderer>() : null;
            if (_shadowSr == null && shadowT != null)
                _shadowSr = shadowT.GetComponentInChildren<SpriteRenderer>(true);
            _effectRoot = FindChildIgnoreCase(_boxAnimHost, "effect");
            // 开箱特效按稀有度分节点（effect 下 "1"/"2"/"3"）：缓存并默认全关，
            // 由 ShowTierEffect 按 tier 在播 open1 时只开对应编号节点。
            _fxMu = _fxYin = _fxJin = null;
            if (_effectRoot != null)
            {
                _fxMu = FindChildIgnoreCase(_effectRoot, "1");
                _fxYin = FindChildIgnoreCase(_effectRoot, "2");
                _fxJin = FindChildIgnoreCase(_effectRoot, "3");
                if (_fxMu != null) _fxMu.gameObject.SetActive(false);
                if (_fxYin != null) _fxYin.gameObject.SetActive(false);
                if (_fxJin != null) _fxJin.gameObject.SetActive(false);
            }
            _boxBaseScale = _boxRoot.localScale;
            if (_boxBaseScale == Vector3.zero) _boxBaseScale = Vector3.one;
            _effectBaseScale = _effectRoot != null ? _effectRoot.localScale : Vector3.one;
            if (_effectBaseScale == Vector3.zero) _effectBaseScale = Vector3.one;
            _boxScenePos = _boxRoot.position;
            EnsureBoxController();
            _boxRoot.gameObject.SetActive(false);
        }
        if (_chuansongmen != null)
            _chuansongmen.gameObject.SetActive(false);
    }

    /// <summary>切场景 / 重开战后调用，强制下次 CacheSceneRefs 重新采样。</summary>
    public void InvalidateSceneCache()
    {
        _boxRoot = null;
        _boxAnimHost = null;
        _effectRoot = null;
        _fxMu = null;
        _fxYin = null;
        _fxJin = null;
        _closeSr = null;
        _openSr = null;
        _shadowSr = null;
        _boxAnim = null;
        _chuansongmen = null;
        StopAllCoroutines();
        _running = false;
    }

    /// <summary>宝箱在 map 之上、与角色同层，避免被背景挡住。</summary>
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

    /// <summary>
    /// 宝箱层级：**与单位同一套规则** —— SortingGroup 的 order 随脚下 Y 变化（越靠下越靠前），
    /// 这样箱子会按它在场上的位置参与前后遮挡，而不是永远压在所有东西前面。
    /// （2026-09-18 修：之前写死 SORT_MAPROOT+5=15，与单位同档且恒定，导致宝箱永远在最前。）
    /// close/open 精灵的 12/13 只是**组内相对**次序，不再当作绝对 order 用。
    /// </summary>
    void ApplyBoxSorting()
    {
        if (_boxRoot == null) return;

        // 用脚底 Y：有地面精灵就取地面精灵，否则取 boxRoot 自身
        float footY = _boxRoot.position.y;
        var groundSr = GetBoxGroundSprite();
        if (groundSr != null) footY = groundSr.transform.position.y;

        var sg = _boxRoot.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = _boxRoot.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = _boxRoot.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        // 2026-09-27 主人反馈「宝箱还是看不见」：这一行原来会算出**低于地图**的 order。
        // 公式 15 - footY*40 在 footY 为正（地面/箱子在原点上方）时会掉到 10 以下，
        // 而地图根是 SORT_MAPROOT=10 → 箱子被地图整块盖掉，等于隐形。
        // 加下限：至少压在地图之上（SORT_MAPROOT + 5 = 15，与单位同档起点），
        // 同时保留按 Y 参与前后遮挡的原意。要再往上只调这个下限。
        int minBoxOrder = GameConfig.SORT_MAPROOT + 5;
        sg.sortingOrder = Mathf.Max(minBoxOrder, GameConfig.SORT_UNIT + Mathf.RoundToInt(-footY * 40f));

        if (_closeSr != null)
        {
            _closeSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _closeSr.sortingOrder = GameConfig.SORT_MAPROOT + 2; // 12
        }
        if (_openSr != null)
        {
            _openSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _openSr.sortingOrder = GameConfig.SORT_MAPROOT + 3; // 13
        }
        // 2026-09-28 主人反馈「阴影都不见了」：影子必须抬到地图之上、箱皮之下（地图根 10 / 影子 11 / close 12）。
        // 只在这里统一铺排层级，别再靠 ForceBoxRenderersVisible 顺手把它打成 order 0。
        if (_shadowSr != null)
        {
            _shadowSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _shadowSr.sortingOrder = GameConfig.SORT_MAPROOT + 1; // 11
        }
    }

    void EnsureBoxController()
    {
        EnsureBoxPhysicsDisabled();
        ApplyBoxSorting();
        if (_boxAnim == null) return;
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (_boxAnim.runtimeAnimatorController == null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Art/Effects/Ani/box/box.controller");
                if (ctrl != null) _boxAnim.runtimeAnimatorController = ctrl;
            }
        }
#endif
    }

    /// <summary>
    /// 运行时兜底补建箱皮：当 _boxRoot 存在但 close/open 精灵整体缺失（箱皮预制体丢失）时，
    /// 在 _boxRoot（或动画宿主）下补建 close/open 的 SpriteRenderer 与 effect 占位根，
    /// 使宝箱可见、可被换皮、可被开关。不补 Animator、不加载 box.controller ——
    /// 原动画绑定路径随预制体丢失，强行挂 controller 反而会把 close 的 alpha 驱动成透明。
    /// 仅在箱皮整体缺失（close/open 都没有）时动手；已有箱皮一律不动。
    /// </summary>
    void EnsureBoxVisualFallback()
    {
        if (_boxRoot == null) return;
        // 已有箱皮（close/open 任一存在）则不动
        if (_closeSr != null || _openSr != null) return;

        var host = _boxAnimHost != null ? _boxAnimHost : _boxRoot;
        // 防止运行时重复补建：host 下已有 close/open 子节点则跳过（重进 CacheSceneRefs 时场景里已存在）
        if (FindChildIgnoreCase(host, "close") != null || FindChildIgnoreCase(host, "open") != null)
            return;

        // close：空 GameObject + SpriteRenderer，sprite 留空由 ApplyBoxVisual 按稀有度填充
        var closeGo = new GameObject("close");
        closeGo.transform.SetParent(host, false); // 保持默认位置/缩放（零/一），不手动设置
        var closeSr = closeGo.AddComponent<SpriteRenderer>();
        closeSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        closeSr.sortingOrder = GameConfig.SORT_MAPROOT + 2; // 与 ApplyBoxSorting 中 close 写法一致

        // open：同上，排序 +1
        var openGo = new GameObject("open");
        openGo.transform.SetParent(host, false);
        var openSr = openGo.AddComponent<SpriteRenderer>();
        openSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        openSr.sortingOrder = GameConfig.SORT_MAPROOT + 3; // 与 ApplyBoxSorting 中 open 写法一致

        // effect：仅作粒子根占位，无组件
        var effectGo = new GameObject("effect");
        effectGo.transform.SetParent(host, false);

        // 赋回字段，供后续 CacheSceneRefs 的查找赋值与贴地采样使用
        _closeSr = closeSr;
        _openSr = openSr;
        _effectRoot = effectGo.transform;
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

    /// <summary>
    /// 关箱待机：先手动复位 close/open 显隐（兜底，万一动画绑定失效图还在），
    /// 再保持 Animator 启用并播 close。close.anim 是主人做的"从天上掉下来落稳"动画
    /// （0.5s 非循环，播完停末帧）。不再禁用 Animator（旧 prefab 无 close 动画时的兜底已不需要）。
    /// </summary>
    void HoldBoxClosedPose()
    {
        StopBoxEffect();
        if (_closeSr != null)
        {
            _closeSr.gameObject.SetActive(true);
            _closeSr.enabled = true;
            var c = _closeSr.color;
            c.a = 1f;
            _closeSr.color = c;
            // 2026-09-24：不再手动挪 close 的 localPosition（旧兜底的 (0,1,0) 已删）——
            // close 的位置/下落完全由主人做的 close.anim 驱动，prefab 里的摆放就是初始位。
        }
        if (_openSr != null)
        {
            _openSr.enabled = false;
            _openSr.gameObject.SetActive(true);
        }
        ApplyBoxSorting();
        // Animator 保持启用，播下落落稳动画
        if (_boxAnim != null)
        {
            _boxAnim.enabled = true;
            _boxAnim.Play("close", 0, 0f);
        }
    }

    /// <summary>当前显示在场上的那张箱皮（用于取底边贴地）。</summary>
    SpriteRenderer GetBoxGroundSprite()
    {
        if (_openSr != null && _openSr.enabled && _openSr.sprite != null) return _openSr;
        if (_closeSr != null && _closeSr.sprite != null) return _closeSr;
        if (_openSr != null && _openSr.sprite != null) return _openSr;
        return null;
    }

    /// <summary>
    /// 宝箱贴地：把当前箱皮的**底边**对齐到「可行走区域中心」的地面高度。
    /// 2026-09-27 主人拍板：宝箱悬在半空（偏上、没落地），应落在可行走区域中心。
    /// 旧的 2026-09-24「位置锁死、以美术手摆 y 为准」实现（连同 useOpenVisual 开关、+0.12 魔数）
    /// 已整段删除，不留回退分支 —— 落点真源只有一个：BattleLaneBounds 的车道中心。
    /// </summary>
    void SnapBoxRootToGround()
    {
        if (_boxRoot == null) return;

        var sr = GetBoxGroundSprite();
        if (sr == null)
        {
            // fail closed：没有箱皮就量不出底边，宁可不动位置也要把问题报出来。
            // 开箱那段会每帧调用本方法，只报一次，避免刷屏淹没别的错。
            if (!_boxSnapErrorLogged)
            {
                _boxSnapErrorLogged = true;
                Debug.LogError("[StageClearReward] 宝箱贴地失败：box 下没有可用箱皮 SpriteRenderer，位置保持不动");
            }
            return;
        }

        BattleLaneBounds.GetLaneOffsetRange(out float laneMin, out float laneMax);
        float groundY = UnitBase.GROUND_Y + (laneMin + laneMax) * 0.5f;

        float dy = groundY - sr.bounds.min.y;
        if (Mathf.Abs(dy) < 0.0005f) return;
        var p = _boxRoot.position;
        float beforeY = p.y;
        p.y += dy;
        _boxRoot.position = p;
        // #region agent log
        DebugAgentLog.Log("H6", "StageClearRewardDirector.SnapBoxRootToGround", "box_snap",
            $"{{\"beforeY\":{beforeY:F3},\"afterY\":{p.y:F3},\"groundY\":{groundY:F3},\"boundsMinY\":{sr.bounds.min.y:F3}}}");
        // #endregion
    }

    void PlaceBoxAt(float worldX, float worldZ)
    {
        if (_boxRoot == null) return;
        var p = _boxRoot.position;
        p.x = worldX;
        p.z = worldZ;
        _boxRoot.position = p;
        ApplyBoxSorting();
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
            // 2026-09-28 主人反馈「阴影都不见了」：影子以前落在这一行里被 Reset 成 order 0，
            // 结果被地图根（SORT_MAPROOT=10）整块盖掉。影子归 ApplyBoxSorting 管，这里跳过。
            if (srs[i] != _closeSr && srs[i] != _openSr && srs[i] != _shadowSr &&
                (_effectRoot == null || !srs[i].transform.IsChildOf(_effectRoot)))
                srs[i].sortingOrder = 0;
        }
        ApplyBoxSorting();
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

    /// <summary>
    /// 按稀有度只开对应编号的开箱特效节点，关掉另外两个（1=普通木箱、2=稀有银箱、3=传奇金箱）。
    /// 在播 open1 的同时调用；节点为 null 时静默跳过。
    /// </summary>
    void ShowTierEffect(ClearBoxTier tier)
    {
        if (_fxMu != null) _fxMu.gameObject.SetActive(tier == ClearBoxTier.Mu);
        if (_fxYin != null) _fxYin.gameObject.SetActive(tier == ClearBoxTier.Yin);
        if (_fxJin != null) _fxJin.gameObject.SetActive(tier == ClearBoxTier.Jin);
        if (tier == ClearBoxTier.Mu) ActivateAndPlayNode(_fxMu);
        else if (tier == ClearBoxTier.Yin) ActivateAndPlayNode(_fxYin);
        else ActivateAndPlayNode(_fxJin);
    }

    /// <summary>激活并播放某个开箱特效节点（含其下 SpriteRenderer 与 ParticleSystem），并设好 VFX 排序。</summary>
    void ActivateAndPlayNode(Transform node)
    {
        if (node == null) return;
        node.gameObject.SetActive(true);
        var srs = node.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].gameObject.SetActive(true);
            srs[i].enabled = true;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        var ps = node.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i] == null) continue;
            ps[i].gameObject.SetActive(true);
            ps[i].Clear(true);
            ps[i].Play(true);
            var pr = ps[i].GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                pr.sortingOrder = GameConfig.SORT_VFX;
            }
        }
    }

    /// <summary>t 是否为 node 自身或其后裔。</summary>
    static bool IsWithin(Transform t, Transform node)
    {
        if (node == null) return false;
        while (t != null)
        {
            if (t == node) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>粒子/精灵是否属于按稀有度分开关的 1/2/3 节点（这些由 ShowTierEffect 负责）。</summary>
    bool IsTierEffectNode(Transform t)
    {
        return IsWithin(t, _fxMu) || IsWithin(t, _fxYin) || IsWithin(t, _fxJin);
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
            // 1/2/3 按稀有度分开关的节点交由 ShowTierEffect 处理，这里跳过
            if (IsTierEffectNode(particles[i].transform)) continue;
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
            // 1/2/3 按稀有度分开关的节点交由 ShowTierEffect 处理，这里跳过
            if (IsTierEffectNode(srs[i].transform)) continue;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        ApplyBoxSorting();
    }

    static Sprite LoadBoxSprite(string fileNameNoExt)
    {
        Sprite sp = Resources.Load<Sprite>("UI/box/" + fileNameNoExt);
        if (sp != null) return sp;
        Texture2D tex = Resources.Load<Texture2D>("UI/box/" + fileNameNoExt);
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (sp == null)
                sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/box/{fileNameNoExt}.png");
            if (tex == null)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/UI/box/{fileNameNoExt}.png");
            if (sp != null) return sp;
        }
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
        // 摆放时按当前箱皮底边贴「可行走区域中心」（与正式关 CoReward 同一入口）。
        // 场景里 box 节点的 y 是美术手摆的，落点一律由 SnapBoxRootToGround 统一算，
        // 只改 x/z 的地方不再各自决定高度（2026-09-27：单一真源，避免又飘回半空）。
        SnapBoxRootToGround();

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
        // 先对齐开箱姿态地面，再播特效，避免 World 粒子留在旧高度显得偏下
        if (_closeSr != null) _closeSr.enabled = false;
        if (_openSr != null) _openSr.enabled = true;
        SnapBoxRootToGround();

        if (_boxAnim != null)
        {
            _boxAnim.enabled = true;
            PlayBoxOpenEffect();
            ShowTierEffect(ClearBoxTier.Mu); // 教程固定木箱 → 普通开箱特效 1
            _boxAnim.Play("open1", 0, 0f);
            yield return WaitAnimOrSeconds(_boxAnim, "open1", 0.9f);
        }
        else
            yield return new WaitForSecondsRealtime(0.4f);

        SnapBoxRootToGround();

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
        SnapBoxRootToGround();
        // 2026-09-28 主人反馈「特效也没有看到」：原来 open2 一 Play 立刻就跟一句 HideBoxVisual，
        // 整个 box（连同 effect 下 1/2/3 的 SparkleWhite / PowerupGlow 粒子）同帧被 SetActive(false)，
        // 粒子刚冒头就被掐灭。这里等 open2 播完，再留 0.6 秒给粒子飘一下，最后才收箱。
        if (_boxAnim != null)
            yield return WaitAnimOrSeconds(_boxAnim, "open2", 0.9f);
        yield return new WaitForSecondsRealtime(0.6f);
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
                ShowTierEffect(tier); // 按稀有度开对应开箱特效节点（1/2/3）
                _boxAnim.Play("open1", 0, 0f);
                yield return WaitAnimOrSeconds(_boxAnim, "open1", 1.15f);
                if (_closeSr != null) _closeSr.enabled = false;
                if (_openSr != null) _openSr.enabled = true;
                SnapBoxRootToGround();
                // open2 已在控制器里设为循环；播完 open1 后强制切入并保持循环
                _boxAnim.Play("open2", 0, 0f);
                SnapBoxRootToGround();
                float open2Snap = 0f;
                while (open2Snap < 2.5f)
                {
                    SnapBoxRootToGround();
                    open2Snap += Time.unscaledDeltaTime;
                    yield return null;
                }
                HideBoxVisual();
            }
            else
            {
                // 无 Animator（箱皮丢失兜底补建路径）：手动切到 open 显示并贴地，让玩家看到箱子开了
                if (_closeSr != null) _closeSr.enabled = false;
                if (_openSr != null) _openSr.enabled = true;
                SnapBoxRootToGround();
                PlayBoxOpenEffect();
                yield return new WaitForSecondsRealtime(0.8f);
            }
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

        // —— 宝箱装备折金：装备已并入「类型三选一」的装备方向，不再单独挑 ——
        Vector3 spawn = _boxRoot != null ? _boxRoot.position : Vector3.zero;
        spawn.y = UnitBase.GROUND_Y;
        ScrapRewardEquips(rewards);

        yield return new WaitForSecondsRealtime(0.35f);

        // —— 2026-09-29：关卡结算的免费三选一已停掉 ——
        // 构筑获取统一走「进关金币抽奖」（BattleManager.CoStageEntryDraft，老虎机定类型再三选一）。
        // 要恢复旧的战斗结束抽卡，把下面这行的注释去掉即可：
        // yield return StartCoroutine(CoStageClearDraft(stageType == StageType.Boss ? 2 : 1));

        // —— 局内构筑进度落档（供「继续上一局」）。升级抽卡统一在升级时发生，这里不再额外出三选一 ——
        RunDraftDirector.Instance?.NoteStageProgress();

        // —— 开箱整理：确定后再出传送门 / 摇杆 ——
        {
            bool lootDone = false;
            BattleLootMode.Enter(() => lootDone = true);
            UIManager.Instance?.ShowToast("整理装备后点「确定」");
            while (!lootDone) yield return null;
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

    /// <summary>宝箱掉落装备直接折金：装备已并入「类型三选一」的装备方向，不再单独挑。</summary>
    void ScrapRewardEquips(List<EquipInstance> rewards)
    {
        if (rewards == null || rewards.Count == 0) return;
        var bm = BattleManager.Instance;
        if (bm == null) return;

        int total = 0;
        for (int i = 0; i < rewards.Count; i++)
            total += ScrapGold(rewards[i]);
        if (total <= 0) return;

        bm.currentGold += total;
        BattleUI.Instance?.UpdateGold(bm.currentGold);
        UIManager.Instance?.ShowToast($"宝箱装备折金 +{total}");
    }

    /// <summary>
    /// 战斗结束抽卡：先出「技能 / 装备 / 佣兵」方向三选一，玩家点定方向后再出该方向三选一。
    /// 选完立即经 <see cref="RunDraftDirector"/> 生效。
    /// </summary>
    /// <param name="pickCount">本关抽卡次数（普通/精英 1 次，Boss 2 次）。</param>
    IEnumerator CoStageClearDraft(int pickCount = 1)
    {
        var dir = RunDraftDirector.Instance;
        if (dir == null && BattleManager.Instance != null)
            dir = RunDraftDirector.Ensure(BattleManager.Instance);
        if (dir == null)
        {
            Debug.LogWarning("[StageClearReward] RunDraftDirector 缺失，跳过战斗结束三选一");
            yield break;
        }

        for (int round = 0; round < Mathf.Max(1, pickCount); round++)
        {
            var cats = DraftPool.BuildCategories();
            if (cats == null || cats.Count == 0) yield break;

            bool done = false;
            // 2026-10-05：装备卡要弹「换不换」确认窗（异步），不能在回调里直接结算 ——
            // 这里只记下玩家选了哪张，真正的生效挪到等待循环之后（那里才能 yield）。
            DraftCard chosen = default;
            var ui = LevelUpDraftUI.ShowCategorized(
                cats,
                DraftPool.BuildCards,
                pickCount > 1 ? $"战斗结束！先选方向（{round + 1}/{pickCount}）" : "战斗结束！先选方向，再挑强化",
                card =>
                {
                    if (card.IsValid) chosen = card;
                    RunDraftDirector.RefreshSkillPower();
                    done = true;
                },
                manageFreeze: false);

            if (ui == null)
            {
                Debug.LogWarning("[StageClearReward] 抽卡弹层创建失败，跳过战斗结束三选一");
                yield break;
            }

            float guard = 0f;
            while (!done && guard < 180f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!done)
            {
                Debug.LogWarning("[StageClearReward] 战斗结束三选一超时未选择，已跳过");
                yield break;
            }

            if (chosen.IsValid)
            {
                if (chosen.Kind == DraftCardKind.Equip)
                {
                    // 与进关抽奖同口径：换不换要问玩家，换下来的旧件折强化石
                    yield return dir.CoApplyEquipCard(chosen, (ok, msg) =>
                    {
                        if (!ok && !string.IsNullOrEmpty(msg)) UIManager.Instance?.ShowToast(msg);
                    });
                }
                else if (!dir.TryApplyCard(chosen, out string applyMsg))
                {
                    Debug.LogError($"[StageClearReward] 战斗结束三选一结果无法生效：{applyMsg}");
                    UIManager.Instance?.ShowToast(applyMsg);
                }
                else if (!string.IsNullOrEmpty(applyMsg))
                {
                    UIManager.Instance?.ShowToast(applyMsg);
                }
                RunLoadout.Save();
            }

            if (round < pickCount - 1) yield return new WaitForSecondsRealtime(0.25f);
        }
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
        // 统一走 GameConfig.EquipScrapGold，避免多处各写一份折金公式。
        return GameConfig.EquipScrapGold(e.rarity, e.star);
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
