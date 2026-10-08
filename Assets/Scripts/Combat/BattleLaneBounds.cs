using UnityEngine;

/// <summary>
/// 战斗可行走区。
///
/// <b>真源（2026-10-05 主人拍板）</b>：<c>BattleUI ▸ map ▸ walk</c> 这个 UI 节点的矩形。
/// 主人在团结里把 walk 摆成「地图上能走的那块地面」，世界可行走范围就由它投影决定，
/// <b>不再用代码里的常量</b>。walk 的父节点是 map，所以地图缩放 / 位移（比如展开背包时）
/// 时可行走区自动跟着一起变 —— 这就是「行走区域没跟着 map 走」的修法。
///
/// 计算链：walk 的 UI 世界矩形（上下边）→ 主相机投影到 z = 地面平面 → 世界 Y，
///        减去站立线 <see cref="UnitBase.GROUND_Y"/> = 相对站立线的上下偏移。
///
/// <b>本类是可行走区唯一的读出口</b>：角色移动钳制、敌人铺车道、宝箱落地全部经
/// <see cref="GetLaneOffsetRange"/> / <see cref="ClampLaneOffset"/>，别处不许再算一遍。
///
/// ⚠ fail closed：找不到 walk / 主相机不可用 / 框高为 0 → LogError 一次并返回 (0,0)，
/// 所有单位锁在站立线上（一眼能看出资源没摆好），绝不静默沿用旧常量。
/// </summary>
public static class BattleLaneBounds
{
    /// <summary>可视化标记节点名（仅编辑器可见，运行时隐藏）。</summary>
    public const string AreaName = "BattleLaneArea";
    /// <summary>可行走区真源节点名 —— map 的子节点，主人在团结里摆。</summary>
    public const string WalkNodeName = "walk";
    /// <summary>承载 walk 的地图节点名。</summary>
    public const string MapNodeName = "map";

    static Transform _area;
    static Transform _walk;
    static bool _resolved;
    /// <summary>日志节流：只有「值真的变了」才打一条，避免每帧刷屏。</summary>
    static float _lastLoggedMin = float.NaN;
    static float _lastLoggedMax = float.NaN;

    static bool _warned;
    static bool _logged;

    public static void Invalidate()
    {
        _resolved = false;
        _area = null;
        _walk = null;
        _warned = false;
        _logged = false;
        _lastLoggedMin = _lastLoggedMax = float.NaN;
    }

    /// <summary>战斗开始时调用：把可视化标记摆成 walk 框的世界矩形（运行时默认隐藏）。</summary>
    public static void EnsureInScene(Transform unitRoot, bool hideVisualInPlay = true)
    {
        Invalidate();
        _area = ResolveOrCreateVisual(unitRoot);
        _resolved = true;

        if (hideVisualInPlay && Application.isPlaying)
            SetVisualVisible(false);
    }

    /// <summary>
    /// 相对站立线的可行走上下偏移（世界单位）；拿不到真源时返回 (0,0)。
    /// <para><b>每调必算、不缓存</b>：map / walk 会随背包展开（<c>BattleEntryDraftPanel</c> 的
    /// 位置+缩放动画）逐帧改变，缓存会让可行走区慢一帧、动画期间看着「不跟手」。
    /// 计算量只有两次矩阵变换 + 一次相机投影，整局每帧调用完全可以承受
    /// （2026-10-05 主人报「map 动了可行走区不跟」，去掉逐帧缓存）。</para>
    /// </summary>
    public static void GetLaneOffsetRange(out float minOffset, out float maxOffset)
    {
        // fail closed：读不到 walk 就锁在站立线，让问题一眼可见（绝不沿用旧常量）
        if (!TryReadWalkRange(out float min, out float max)) min = max = 0f;
        minOffset = min;
        maxOffset = max;
    }

    /// <summary>
    /// 每帧对齐用（幂等、可反复调）：把可视化标记 <c>BattleLaneArea</c> 重新贴回 walk 的世界矩形。
    /// <para>walk 是 <c>map</c> 的子节点，map 一动（背包展开的位置+缩放动画）它的世界矩形就变了；
    /// 可视化标记挂在世界空间不会自己跟，颜色/位置就会与真实可行走区脱节。
    /// 在 <c>BattleManager.Update</c> 每帧调一次（默认标记不可见、开销可忽略）。</para>
    /// </summary>
    public static void Tick()
    {
        if (_area == null) return;
        var sr = _area.GetComponent<SpriteRenderer>();
        var walk = ResolveWalk() as RectTransform;
        if (sr != null && walk != null) AlignVisualToWalk(_area, sr, walk);
    }

    public static float ClampLaneOffset(float offset)
    {
        GetLaneOffsetRange(out float min, out float max);
        return Mathf.Clamp(offset, min, max);
    }

    public static float RandomLaneOffset()
    {
        GetLaneOffsetRange(out float min, out float max);
        return Random.Range(min, max);
    }

    /// <summary>按波次人数均匀铺开车道，带轻微抖动，减少 Y 重叠。</summary>
    public static float LaneSlot(int index, int count, float jitter = 0.06f)
    {
        GetLaneOffsetRange(out float min, out float max);
        if (count <= 1)
            return ClampLaneOffset((min + max) * 0.5f + Random.Range(-jitter, jitter));
        float t = Mathf.Clamp01(index / (float)(count - 1));
        // 交错：偶数偏下、奇数微调，避免整列对齐
        float stagger = ((index & 1) == 0) ? -jitter * 0.5f : jitter * 0.5f;
        return ClampLaneOffset(Mathf.Lerp(min, max, t) + stagger + Random.Range(-jitter, jitter));
    }

    /// <summary>在已占用车道中挑间距最大的候选，尽量不叠。</summary>
    public static float PickSpreadLane(System.Collections.Generic.IList<float> usedLanes, float minGap = 0.32f)
    {
        GetLaneOffsetRange(out float min, out float max);
        float best = Random.Range(min, max);
        float bestNearest = -1f;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            float cand = Random.Range(min, max);
            float nearest = float.MaxValue;
            if (usedLanes != null)
            {
                for (int i = 0; i < usedLanes.Count; i++)
                    nearest = Mathf.Min(nearest, Mathf.Abs(cand - usedLanes[i]));
            }
            else
            {
                nearest = max - min;
            }
            if (nearest > bestNearest)
            {
                bestNearest = nearest;
                best = cand;
            }
            if (nearest >= minGap)
                break;
        }
        return ClampLaneOffset(best);
    }

    public static void SetVisualVisible(bool visible)
    {
        EnsureResolved();
        if (_area == null) return;
        var srs = _area.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) srs[i].enabled = visible;
        var mr = _area.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = visible;
    }

    /// <summary>走动范围的世界 Y 区间（含站立线），仅供排查用。true = walk 真源读到了。</summary>
    public static bool TryGetWorldRange(out float minY, out float maxY)
    {
        bool ok = TryReadWalkRange(out float min, out float max);
        if (!ok) min = max = 0f;
        minY = UnitBase.GROUND_Y + min;
        maxY = UnitBase.GROUND_Y + max;
        return ok;
    }

    // ============================================================
    // walk 框 → 世界范围
    // ============================================================

    static bool TryReadWalkRange(out float min, out float max)
    {
        min = max = 0f;

        var walk = ResolveWalk() as RectTransform;
        if (walk == null)
        {
            WarnOnce("[BattleLaneBounds] 找不到 " + MapNodeName + "/" + WalkNodeName + " 节点，可行走区按 0 处理");
            return false;
        }

        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            WarnOnce("[BattleLaneBounds] 主相机不可用或不是正交相机，无法把 walk 框投到世界");
            return false;
        }

        var unit = BattleManager.Instance != null ? BattleManager.Instance.unitRoot : null;
        float planeZ = unit != null ? unit.position.z : 0f;

        Vector3 bottom = walk.TransformPoint(new Vector3(walk.rect.xMin, walk.rect.yMin, 0f));
        Vector3 top = walk.TransformPoint(new Vector3(walk.rect.xMax, walk.rect.yMax, 0f));
        float yA = ToGroundPlaneY(cam, bottom, planeZ);
        float yB = ToGroundPlaneY(cam, top, planeZ);

        float ground = UnitBase.GROUND_Y;
        min = Mathf.Min(yA, yB) - ground;
        max = Mathf.Max(yA, yB) - ground;

        if (max - min < 0.001f)
        {
            WarnOnce("[BattleLaneBounds] walk 框高度为 0，可行走区无效");
            min = max = 0f;
            return false;
        }

        // 只在「范围真的变了」时打日志：map 展开 / 收回会逐帧改这个范围，
        // 主人就是要看到它在动（原来只打第一条，看不出跟没跟）。
        if (!_logged || Mathf.Abs(min - _lastLoggedMin) > 0.02f || Mathf.Abs(max - _lastLoggedMax) > 0.02f)
        {
            _logged = true;
            _lastLoggedMin = min;
            _lastLoggedMax = max;
            Debug.Log($"[BattleLaneBounds] walk 可行走区：站立线 Y={ground:F3}，" +
                      $"世界 Y={ground + min:F3} ~ {ground + max:F3}，相对偏移 {min:F3} ~ {max:F3}" +
                      $" | walk 世界缩放={walk.lossyScale.x:F3} 框高={walk.rect.height:F1}");
        }
        return true;
    }

    /// <summary>
    /// 把 UI 平面上的世界点沿主相机投影到 z = <paramref name="planeZ"/> 平面，取它的 Y。
    /// 正交相机下这一步是线性缩放，所以 walk 的矩形边正好对应世界里的可行走上下界。
    /// </summary>
    static float ToGroundPlaneY(Camera cam, Vector3 uiWorldPoint, float planeZ)
    {
        Vector3 sp = cam.WorldToScreenPoint(uiWorldPoint);
        sp.z = planeZ - cam.transform.position.z;   // ScreenToWorldPoint 的 z = 距相机的距离
        return cam.ScreenToWorldPoint(sp).y;
    }

    /// <summary>带缓存的查找：UI 重建后旧引用会被 Unity 判假，自动重新找一次。</summary>
    static Transform ResolveWalk()
    {
        if (_walk != null) return _walk;
        _walk = FindWalk();
        return _walk;
    }

    static Transform FindWalk()
    {
        var ui = BattleUI.Instance;
        if (ui != null)
        {
            var map = FindDeep(ui.transform, MapNodeName);
            if (map != null)
            {
                var direct = map.Find(WalkNodeName);
                if (direct != null) return direct;
                var nested = FindDeep(map, WalkNodeName);   // 允许包一层容器
                if (nested != null) return nested;
            }
        }
        var go = GameObject.Find(WalkNodeName);
        return go != null ? go.transform : null;
    }

    static void WarnOnce(string msg)
    {
        if (_warned) return;
        _warned = true;
        Debug.LogError(msg);
    }

    // ============================================================
    // 可视化标记（BattleLaneArea）：位置与大小直接取 walk 的世界矩形
    // ============================================================

    static void EnsureResolved()
    {
        if (_resolved && _area != null) return;
        Transform parent = BattleManager.Instance != null ? BattleManager.Instance.unitRoot : null;
        _area = ResolveOrCreateVisual(parent);
        _resolved = true;
    }

    static Transform ResolveOrCreateVisual(Transform unitRoot)
    {
        var existing = FindArea(unitRoot);
        if (existing != null)
        {
            AlignExistingToWalk(existing);
            return existing;
        }

        var walk = FindWalk() as RectTransform;
        if (walk == null) return null;   // 没真源就不建可视化（范围解析那边已经报错）

        var go = new GameObject(AreaName);
        if (unitRoot != null) go.transform.SetParent(unitRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        sr.color = new Color(0.2f, 0.85f, 0.35f, 0.22f);
        sr.sortingOrder = -20;
        AlignVisualToWalk(go.transform, sr, walk);
        return go.transform;
    }

    static void AlignExistingToWalk(Transform area)
    {
        if (area == null) return;
        var sr = area.GetComponent<SpriteRenderer>();
        var walk = FindWalk() as RectTransform;
        if (sr != null && walk != null) AlignVisualToWalk(area, sr, walk);
    }

    /// <summary>把可视化标记摆成 walk 框在世界里的矩形（同源，不是另一套数值）。</summary>
    static void AlignVisualToWalk(Transform area, SpriteRenderer sr, RectTransform walk)
    {
        if (area == null || sr == null || walk == null || sr.sprite == null) return;
        Bounds b;
        if (!TryGetWorldBounds(walk, out b)) return;

        var sprite = sr.sprite;
        Vector2 unit = sprite.bounds.size;
        if (unit.x < 1e-4f || unit.y < 1e-4f) return;

        area.position = new Vector3(b.center.x, b.center.y, area.position.z);
        area.localScale = new Vector3(b.size.x / unit.x, b.size.y / unit.y, 1f);
    }

    static bool TryGetWorldBounds(RectTransform rt, out Bounds bounds)
    {
        bounds = new Bounds();
        if (rt == null) return false;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float minX = Mathf.Min(Mathf.Min(c[0].x, c[1].x), Mathf.Min(c[2].x, c[3].x));
        float maxX = Mathf.Max(Mathf.Max(c[0].x, c[1].x), Mathf.Max(c[2].x, c[3].x));
        float minY = Mathf.Min(Mathf.Min(c[0].y, c[1].y), Mathf.Min(c[2].y, c[3].y));
        float maxY = Mathf.Max(Mathf.Max(c[0].y, c[1].y), Mathf.Max(c[2].y, c[3].y));
        bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                            new Vector3(maxX - minX, maxY - minY, 0.01f));
        return true;
    }

    static Transform FindArea(Transform unitRoot)
    {
        if (unitRoot != null)
        {
            var t = unitRoot.Find(AreaName);
            if (t != null) return t;
        }
        var go = GameObject.Find(AreaName);
        return go != null ? go.transform : null;
    }

    static Sprite _white;
    static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = Texture2D.whiteTexture;
        _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        _white.name = "BattleLaneAreaWhite";
        return _white;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
