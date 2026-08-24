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
    bool _baseScaleCached;
    bool _running;

    public bool IsRunning => _running;
    public Transform ChuanSongMen => _chuansongmen;

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
            _baseScaleCached = true;
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
        _baseScaleCached = false;
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

    void EnsureBoxController()
    {
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
        EnsureEffectAlive();
    }

    void EnsureEffectAlive()
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
            particles[i].Play(true);
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

        // —— 宝箱：用场景里摆好的位置，只整体上移 0.5 ——
        if (_boxRoot != null)
        {
            Vector3 p = _boxScenePos;
            p.y += 0.5f;
            _boxRoot.position = p;
            _boxRoot.gameObject.SetActive(true);
            EnsureEffectAlive();
            EnsureBoxController();
            if (_boxAnim != null)
            {
                _boxAnim.enabled = true;
                _boxAnim.Play("open1", 0, 0f);
                yield return WaitAnimOrSeconds(_boxAnim, "open1", 1.15f);
                // open2 已在控制器里设为循环；播完 open1 后强制切入并保持循环
                _boxAnim.Play("open2", 0, 0f);
            }
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
            yield return new WaitForSeconds(0.5f);

        // —— 掉落金币飞入资源条 ——
        int goldDrop = Mathf.Max(0, bonusGold);
        if (goldDrop > 0)
        {
            int coinCount = Mathf.Clamp(goldDrop / 5, 6, 18);
            int goldPerCoin = Mathf.Max(1, goldDrop / coinCount);
            int goldRemain = goldDrop;
            Vector3 spawnGold = _boxRoot != null ? _boxRoot.position : Vector3.zero;
            spawnGold.y = UnitBase.GROUND_Y + 0.3f;

            for (int i = 0; i < coinCount; i++)
            {
                int add = (i == coinCount - 1) ? goldRemain : goldPerCoin;
                goldRemain -= add;
                Vector3 land = spawnGold + new Vector3(Random.Range(-0.6f, 0.6f), 0f, 0f);
                yield return StartCoroutine(CoFlyCoin(land, add));
                yield return new WaitForSeconds(0.04f);
            }
        }

        // —— 地上装备表现（不可点，仅视觉）——
        Vector3 spawn = _boxRoot != null ? _boxRoot.position : Vector3.zero;
        spawn.y = UnitBase.GROUND_Y + 0.3f;
        var show = new List<EquipInstance>();
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count && show.Count < 3; i++)
                if (rewards[i] != null) show.Add(rewards[i]);
        }
        var groundIcons = new List<GameObject>();
        for (int i = 0; i < show.Count; i++)
        {
            var go = CreateGroundDrop(spawn + new Vector3(-0.7f + i * 0.7f, 0.15f, 0f), show[i]);
            if (go != null) groundIcons.Add(go);
        }

        yield return new WaitForSeconds(0.35f);

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

        // —— 传送门 ——
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
            bm?.NotifyChuanSongMenOpened(_chuansongmen);
        }
        else
        {
            // 无传送门节点：直接结算选关
            bm?.FinishStageAfterPortalReached();
        }

        if (bm != null) bm.UnitsCanAct = true;
        _running = false;
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
            t += Time.deltaTime;
            if (anim != null)
            {
                var info = anim.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(state) && info.normalizedTime >= 0.98f)
                    yield break;
            }
            yield return null;
        }
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
