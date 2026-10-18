using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：初始化与挂点（Awake、排序层、发射点/受击点自动创建与躯干中心计算）
public partial class UnitBase
{

    protected virtual void Awake()
    {
        BindAttrOwnerKind();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (anim == null) anim = GetComponent<Animator>();

        // 初始化动画控制器（没有就自动添加）
        unitAnim = GetComponent<UnitAnimation>();
        if (unitAnim == null)
            unitAnim = gameObject.AddComponent<UnitAnimation>();

        // 自动创建发射点和受击点
        EnsureFirePoint();
        EnsureHitPoint();
    }

    /// <summary>
    /// 尊重预制体原有的SortingLayer/SortingOrder，不再强制覆盖
    /// 只在子节点没有设置层级时，才根据 isAlly 赋予默认值
    /// 这样用户可以在预制体中自由调整层级关系
    /// </summary>
    public void ApplySortingLayer()
    {
        GameConfig.ApplyUnitSorting(transform);
    }

    void SetSortingLayerRecursive(Transform t, string defaultLayerName, int defaultOrder)
    {
        SpriteRenderer childSr = t.GetComponent<SpriteRenderer>();
        if (childSr != null)
        {
            // 如果当前SortingLayer是Default(0)或空，才使用默认值
            // 否则保留预制体中设置好的层级
            if (string.IsNullOrEmpty(childSr.sortingLayerName) || childSr.sortingLayerName == "Default")
            {
                childSr.sortingLayerName = defaultLayerName;
                childSr.sortingOrder = defaultOrder;
            }
        }
        foreach (Transform child in t)
        {
            SetSortingLayerRecursive(child, defaultLayerName, defaultOrder);
        }
    }

    /// <summary>
    /// 确保发射点存在（法球/弹幕从这里飞出）
    /// 兼容预制体中已有的 "FirePoint" 或 "fire" 节点
    /// </summary>
    protected void EnsureFirePoint()
    {
        if (firePoint != null) return;

        // 查找已有子物体（兼容多种命名）
        Transform existing = transform.Find("FirePoint");
        if (existing == null) existing = transform.Find("fire");
        if (existing != null)
        {
            firePoint = existing;
            return;
        }

        // 自动创建
        GameObject fp = new GameObject("FirePoint");
        fp.transform.SetParent(transform, false);
        fp.transform.localPosition = firePointOffset;
        firePoint = fp.transform;
    }

    /// <summary>
    /// 确保受击点存在（法球飞到这里后爆炸，伤害数字从这里弹出）
    /// 兼容预制体中已有的 "HitPoint" 或 "beattack" 节点
    /// 自动计算精灵的视觉中心位置（而非图片原点/pivot）
    /// </summary>
    protected void EnsureHitPoint()
    {
        if (hitPoint != null) return;

        // 查找已有子物体（兼容多种命名）
        Transform existing = transform.Find("HitPoint");
        if (existing == null) existing = transform.Find("beattack");
        if (existing != null)
        {
            hitPoint = existing;
            // 预制体挂点位置不可信（常年贴脚），一律重算到躯干中心
            StartCoroutine(CalcHitPointCenter(existing));
            return;
        }

        // 创建VfxCenter父节点，位置在精灵视觉中心
        GameObject hp = new GameObject("HitPoint");
        hp.transform.SetParent(transform, false);

        // 延迟到下一帧计算中心（精灵可能还没加载）
        StartCoroutine(CalcHitPointCenter(hp.transform));
        // 先用偏移兜底
        hp.transform.localPosition = hitPointOffset;
        hitPoint = hp.transform;
    }

    protected System.Collections.IEnumerator CalcHitPointCenter(Transform hpTransform)
    {
        yield return null; // 等一帧，确保精灵已加载
        yield return null; // 再等一帧，SPUM可能需要两帧才完全加载
        if (hpTransform == null) yield break;

        // 怪物图多为 32×32 透明填充：优先用不透明像素包围盒中心，避免整图画布中心
        if (TryPlaceHitPointByOpaqueSprite(hpTransform))
            yield break;

        if (!TryGetBodyBounds(out Bounds body))
        {
            hpTransform.localPosition = hitPointOffset;
            yield break;
        }

        // 世界空间定位躯干中心。预制体根节点常带缩放（SPUM），
        // 在局部空间夹取会把挂点压到脚底，这里一律走 world position。
        hpTransform.position = GetBodyCenterWorld(body);
        Vector3 lp = hpTransform.localPosition;
        hpTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    /// <summary>按贴图不透明像素中心摆受击点（怪物表）；成功返回 true。</summary>
    protected virtual bool TryPlaceHitPointByOpaqueSprite(Transform hpTransform)
    {
        if (hpTransform == null) return false;
        if (!TryGetPrimaryBodySprite(out SpriteRenderer bodySr)) return false;
        if (!MonsterSpriteOpaqueTable.TryGetOpaqueCenterWorld(bodySr, out Vector3 world))
            return false;
        hpTransform.position = world;
        Vector3 lp = hpTransform.localPosition;
        hpTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
        return true;
    }

    /// <summary>躯干主精灵（怪物优先 Monsters 子节点）。</summary>
    protected virtual bool TryGetPrimaryBodySprite(out SpriteRenderer bodySr)
    {
        bodySr = null;
        Transform monsters = transform.Find("Monsters");
        if (monsters != null)
        {
            bodySr = monsters.GetComponent<SpriteRenderer>();
            if (bodySr != null && bodySr.sprite != null) return true;
        }
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        if (all == null) return false;
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (IsIgnoredHitPointRenderer(r)) continue;
            bodySr = r;
            return true;
        }
        return false;
    }

    /// <summary>躯干中心（世界坐标）：包围盒中线偏下一点，避免武器/头顶透明把点抬高。</summary>
    protected Vector3 GetBodyCenterWorld(Bounds body)
    {
        float y = body.min.y + body.size.y * 0.42f;
        return new Vector3(body.center.x, y, transform.position.z);
    }

    /// <summary>合并本单位躯干精灵的世界包围盒（排除影子/血条/特效）。</summary>
    protected bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        SpriteRenderer[] allSrs = GetComponentsInChildren<SpriteRenderer>(true);
        if (allSrs == null || allSrs.Length == 0) return false;

        bool hasValid = false;
        foreach (var r in allSrs)
        {
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (IsIgnoredHitPointRenderer(r)) continue;

            if (!hasValid)
            {
                bounds = r.bounds;
                hasValid = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return hasValid && bounds.size.y > 0.0001f;
    }

    /// <summary>世界空间：血条宽度（默认；怪物按精灵 bounds 覆盖）。</summary>
    public virtual float GetHpBarWorldWidth()
    {
        return 0.55f;
    }

    /// <summary>世界空间：脚底到血条锚点 Y 偏移；挂 Body 原点时用 0。</summary>
    public virtual float GetHpBarWorldYOffset()
    {
        return 0f;
    }

    static bool IsIgnoredHitPointRenderer(SpriteRenderer r)
    {
        if (r == null) return true;
        Transform p = r.transform;
        while (p != null)
        {
            string n = p.name;
            if (!string.IsNullOrEmpty(n))
            {
                string low = n.ToLowerInvariant();
                if (low.Contains("shadow") || low.Contains("阴影") || low == "hpbar"
                    || low.Contains("bar_bg") || low.Contains("damage") || low.Contains("vfx")
                    || low.Contains("weapon") || low.Contains("武器") || low.Contains("bow")
                    || low.Contains("fire") || low.Contains("beattack") || low.Contains("hitpoint"))
                    return true;
            }
            p = p.parent;
        }
        return false;
    }

    /// <summary>
    /// 获取发射点世界坐标（考虑朝向翻转）
    /// </summary>
    public virtual Vector3 GetFirePosition()
    {
        if (firePoint != null)
        {
            Vector3 p = firePoint.position;
            // 出手高度跟躯干中心走，避免弹道从脚下飞出
            if (hitPoint != null) p.y = hitPoint.position.y;
            return p;
        }
        float y = hitPoint != null
            ? hitPoint.position.y
            : transform.position.y + firePointOffset.y;
        return new Vector3(transform.position.x + firePointOffset.x * facingDir, y, transform.position.z);
    }

    /// <summary>
    /// 获取受击点世界坐标
    /// </summary>
    public virtual Vector3 GetHitPosition()
    {
        if (hitPoint != null) return hitPoint.position;
        return transform.position + hitPointOffset;
    }
}
