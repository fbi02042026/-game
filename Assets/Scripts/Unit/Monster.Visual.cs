using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：血条查找/规范化/排序、世界血条、足印查询、脚下阴影
public partial class Monster
{
    // ????????SpriteRenderer ???
    private SpriteRenderer _hpBarFill;
    private SpriteRenderer _hpBarBg;
    private Transform _hpBarRoot;
    private MonsterHealthBar _worldHpBar;
    static int s_hpBarFrontBoost;
    private float _hpBarFillBaseWidth = 1f;

    /// <summary>????????HPBar ???Init ????????</summary>
    void FindHPBar()
    {
        _hpBarRoot = transform.Find("HPBar");
        if (_hpBarRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "HPBar") { _hpBarRoot = child; break; }
            }
        }
        if (_hpBarRoot == null) return;

        Transform fill = _hpBarRoot.Find("HPBarFill");
        if (fill != null)
            _hpBarFill = fill.GetComponent<SpriteRenderer>();

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            var bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr != null)
            {
                bgSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                bgSr.sortingOrder = GameConfig.SORT_UNIT;
            }
        }
    }

    void NormalizeHPBarLayout(float monsterRootScale)
    {
        if (_hpBarRoot == null) return;

        float spriteWidth = 0.32f;
        if (sr != null && sr.sprite != null)
            spriteWidth = sr.sprite.bounds.size.x;

        // ????????????? 85%?????????? scale
        float monsterWorldWidth = spriteWidth * monsterRootScale;
        float barSpriteWidth = 1.01f;
        if (_hpBarFill != null && _hpBarFill.sprite != null)
            barSpriteWidth = Mathf.Max(0.01f, _hpBarFill.sprite.bounds.size.x);
        float barLocalScale = (monsterWorldWidth * 0.65f) / (barSpriteWidth * Mathf.Max(0.5f, monsterRootScale));
        barLocalScale = Mathf.Clamp(barLocalScale, 0.22f, 2f);
        _hpBarRoot.localScale = Vector3.one * barLocalScale;

        ApplyAnchorPosition(_hpBarRoot, 0f, ResolveHpBarFootLocalY());

        if (_hpBarFill != null)
        {
            _hpBarFillBaseWidth = _hpBarFill.sprite != null
                ? _hpBarFill.sprite.bounds.size.x
                : 1f;
            var fillT = _hpBarFill.transform;
            fillT.localScale = Vector3.one;
            fillT.localPosition = Vector3.zero;
        }
    }

    float ResolveHpBarFootLocalY()
    {
        float footY = GameConfig.MONSTER_HP_BAR_FOOT_LOCAL_Y;
        if (sr != null && sr.sprite != null)
            footY = Mathf.Min(footY, sr.sprite.bounds.min.y + 0.02f);
        // 锚在脚面略上，禁止被旧 clamp 抬到头部
        return Mathf.Clamp(footY + 0.04f, -3.5f, 0.06f);
    }

    System.Collections.IEnumerator RefreshHpBarLayoutAfterSpriteReady(float rootScale)
    {
        yield return null;
        yield return null;
        if (_worldHpBar != null)
        {
            _worldHpBar.ApplyBarMetricsFromUnit();
        }
    }

    void DisableEmbeddedHpBar()
    {
        FindHPBar();
        if (_hpBarRoot != null)
            _hpBarRoot.gameObject.SetActive(false);

        var embeddedUi = GetComponentsInChildren<MonsterHealthBar>(true);
        for (int i = 0; i < embeddedUi.Length; i++)
        {
            if (embeddedUi[i] == null || embeddedUi[i] == _worldHpBar) continue;
            if (embeddedUi[i].transform.IsChildOf(transform))
                embeddedUi[i].gameObject.SetActive(false);
        }
    }

    void EnsureWorldHealthBar()
    {
        // 仅 Boss/精英用屏幕 BossBar；小怪不再创建头顶世界血条
        DisableEmbeddedHpBar();
        _worldHpBar = null;
    }

    public override float GetHpBarWorldWidth()
    {
        if (sr != null && sr.sprite != null)
            return Mathf.Max(0.16f, sr.bounds.size.x * GameConfig.MONSTER_HP_BAR_WIDTH_MUL);
        return base.GetHpBarWorldWidth();
    }

    /// <summary>??????? footprint??????AABB?????????????/summary>
    public void GetFootprintBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        float halfW = GetOpaqueFootprintHalfWidth();
        float cx = UnitBase.GetCombatX(this);
        float footY = MoveRoot.position.y;
        float height = halfW * 2.2f;
        if (sr != null && sr.sprite != null
            && MonsterSpriteOpaqueTable.TryGet(sr.sprite.name, out MonsterSpriteOpaqueTable.Entry e))
        {
            height = Mathf.Max(0.35f, sr.bounds.size.y * Mathf.Clamp(e.BoxNH, 0.2f, 1f));
        }
        minX = cx - halfW;
        maxX = cx + halfW;
        minY = footY;
        maxY = footY + height;
    }

    /// <summary>?????????????????????bounds???/summary>
    public float GetOpaqueFootprintHalfWidth()
    {
        if (sr == null || sr.sprite == null) return UnitCrowd.MonsterFallbackHalfWidth;
        if (MonsterSpriteOpaqueTable.TryGet(sr.sprite.name, out MonsterSpriteOpaqueTable.Entry e))
        {
            float boxW = Mathf.Clamp(e.BoxNW, 0.12f, 1f);
            float worldW = sr.bounds.size.x * boxW;
            return Mathf.Max(UnitCrowd.MonsterFallbackHalfWidth, worldW * 0.5f);
        }
        return UnitCrowd.MonsterFallbackHalfWidth;
    }

    /// <summary>
    /// ?? HPBar ??SpriteRenderer ????
    /// ????????????????Default??????Effects ????
    /// </summary>
    void SetupHPBarSorting()
    {
        if (_hpBarRoot == null) return;

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            _hpBarBg = bg.GetComponent<SpriteRenderer>();
            if (_hpBarBg != null)
            {
                _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _hpBarBg.sortingOrder = GameConfig.SORT_VFX - 2;
            }
        }

        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = GameConfig.SORT_VFX - 1;
        }

        // ???????????
        if (sr != null)
        {
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_UNIT;
        }
    }

    void BringHpBarFront()
    {
        s_hpBarFrontBoost = (s_hpBarFrontBoost + 2) % 40;
        if (_worldHpBar != null)
        {
            _worldHpBar.BringToFront(s_hpBarFrontBoost);
            return;
        }

        int bgOrder = GameConfig.SORT_VFX - 2 + s_hpBarFrontBoost;
        int fillOrder = bgOrder + 1;
        if (_hpBarBg != null)
        {
            _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarBg.sortingOrder = bgOrder;
        }
        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = fillOrder;
        }
    }

    /// <summary>
    /// 怪脚下半透椭圆阴影。关键点：阴影必须挂到「承载身体部件的 SortingGroup」之下，
    /// 与躯干/SPUM 部件处于同一组内，sortingOrder=-20 才能稳定排在躯干(>=0)之后。
    /// 否则作为 _bodyRoot 下的独立节点时，-20 会低于地图(SORT_MAPROOT=10)，
    /// 既可能被背景遮住，也可能因身体部件自身顺序更负而叠到身上（即“阴影跑到身子上面”）。
    /// </summary>
    void EnsureFootShadow()
    {
        Transform body = GetBodyTransform();
        if (body == null) return;

        // 找到承载身体部件的 SortingGroup（ApplyUnitSorting 通常加在 transform 或其子节点上）
        Transform host = transform;
        var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
        if (sg == null)
        {
            GameConfig.ApplyUnitSorting(transform);
            sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
        }
        if (sg != null) host = sg.transform;

        Transform existing = FindFootShadowNode(transform);
        SpriteRenderer shadowSr;
        // 【2026-10-06 诊断日志 · 主人报「给敌人加了 shadow 节点但看不到」】
        // 只打日志、不改任何节点：把「影子是从预制体拿的还是代码新建的」以及最终数值全打出来，
        // 主人一眼就能看出影子是不是被谁关了 / 盖住 / 透明了。绝不删节点、绝不静默跳过。
        float prefabAlpha = -1f;
        bool prefabActive = false;
        if (existing != null)
        {
            // 【2026-10-06 主人拍板「不要随意动我摆的节点」】只有「影子当前这条父链上没有 SortingGroup、
            // 且它不是挂在 host 下」时才挪位置（否则组内 order -20 会被地图整块盖掉 = 看不见）；
            // 已经在 SortingGroup 里就保持主人的层级原样不动。
            if (existing.parent != host
                && existing.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() == null)
                existing.SetParent(host, false);

            shadowSr = existing.GetComponent<SpriteRenderer>();
            if (shadowSr != null)
            {
                prefabAlpha = shadowSr.color.a;
                prefabActive = shadowSr.gameObject.activeInHierarchy && shadowSr.enabled;
            }
            if (shadowSr == null) shadowSr = existing.gameObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            var go = new GameObject("FootShadow");
            go.transform.SetParent(host, false);
            shadowSr = go.AddComponent<SpriteRenderer>();
        }

        // 【2026-10-06 主人拍板】预制体里**已经摆了 shadow 节点**（Monstersmoban 下那个）→
        // 一律保留主人在预制体里调好的外观：贴图 / 透明度 / 缩放 / 位置**一个都不动**，
        // 代码只统一「分层 + 组内顺序」（那才是「能不能被看见」的关键）。
        // 只有「预制体里压根没有 shadow」时才由代码新建并给一套默认值
        //（铁律 2：预制体有值 → 代码不写；预制体空 → 代码兜底）。
        if (prefabAlpha < 0f)
        {
            Sprite shadowSp = LoadPlayerShadowSprite() ?? MakeCircleSprite();
            shadowSr.sprite = shadowSp;
            shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
            shadowSr.sharedMaterial = GetFootShadowMaterial();

            float targetWorldW = 0.9f;
            if (sr != null && sr.sprite != null)
                targetWorldW = Mathf.Max(0.45f, sr.bounds.size.x * 0.56f);
            // 整体再缩小 20%
            targetWorldW *= 0.8f;
            float nativeW = shadowSp != null ? Mathf.Max(0.01f, shadowSp.bounds.size.x) : 1f;
            float lossyX = Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x));
            float sx = targetWorldW / (nativeW * lossyX);
            // 本地 Y=0.02；椭圆再压扁 20%（0.35→0.28）
            shadowSr.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shadowSr.transform.localRotation = Quaternion.identity;
            shadowSr.transform.localScale = new Vector3(sx, sx * 0.28f, 1f);
        }

        // 【2026-10-06 主人拍板】分层 / 组内顺序也一律听预制体的：
        // 主人已经在 Monstersmoban 里把 shadow 摆好并调好显示了，代码**一个值都不许覆盖**
        //（铁律 2：预制体有值 → 代码不写）。只有「预制体里压根没有 shadow」时才由代码给默认值。
        if (prefabAlpha < 0f)
        {
            shadowSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            shadowSr.sortingOrder = -20;
        }
    }

    static Transform FindFootShadowNode(Transform root)
    {
        if (root == null) return null;
        if (root.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindFootShadowNode(root.GetChild(i));
            if (f != null) return f;
        }
        return null;
    }

    static Material _footShadowMat;

    static Material GetFootShadowMaterial()
    {
        if (_footShadowMat != null) return _footShadowMat;
        var sh = Shader.Find("Battle/FootShadow");
        if (sh != null)
            _footShadowMat = new Material(sh);
        else
        {
            var fallback = Shader.Find("Sprites/Default");
            if (fallback != null)
                _footShadowMat = new Material(fallback);
        }
        return _footShadowMat;
    }

    static Sprite _cachedPlayerShadow;

    static Sprite LoadPlayerShadowSprite()
    {
        if (_cachedPlayerShadow != null) return _cachedPlayerShadow;
        _cachedPlayerShadow = Resources.Load<Sprite>("SPUM/Shadow");
        if (_cachedPlayerShadow != null) return _cachedPlayerShadow;
        var tex = Resources.Load<Texture2D>("SPUM/Shadow");
        if (tex != null)
        {
            _cachedPlayerShadow = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedPlayerShadow;
        }
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            var ed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SPUM/Core/Basic_Resources/Ect/Shadow.png");
            if (ed != null)
            {
                _cachedPlayerShadow = ed;
                return _cachedPlayerShadow;
            }
        }
#endif
        return null;
    }
}
