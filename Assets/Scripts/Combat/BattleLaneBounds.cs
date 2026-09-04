using UnityEngine;

/// <summary>
/// 战斗可行走区：读场景 <c>BattleLaneArea</c> 的世界 Y 范围（相对 GROUND_Y）。
/// 用户在 Hierarchy 缩放该标记调上下界；无标记时回退 GameConfig.BATTLE_LANE_HALF。
/// </summary>
public static class BattleLaneBounds
{
    public const string AreaName = "BattleLaneArea";

    static Transform _area;
    static SpriteRenderer _sr;
    static bool _resolved;

    public static void Invalidate()
    {
        _resolved = false;
        _area = null;
        _sr = null;
    }

    public static void EnsureInScene(Transform unitRoot, bool hideVisualInPlay = true)
    {
        Invalidate();
        Transform parent = unitRoot != null ? unitRoot : null;
        Transform found = FindArea(parent);
        if (found == null)
            found = CreateDefaultArea(parent);

        _area = found;
        _sr = found.GetComponent<SpriteRenderer>();
        _resolved = true;

        if (hideVisualInPlay && Application.isPlaying)
            SetVisualVisible(false);
    }

    public static void GetLaneOffsetRange(out float minOffset, out float maxOffset)
    {
        EnsureResolved();
        if (_area == null)
        {
            float h = GameConfig.BATTLE_LANE_HALF;
            minOffset = -h;
            maxOffset = h;
            return;
        }

        Bounds b = GetWorldBounds(_area);
        float gy = UnitBase.GROUND_Y;
        minOffset = b.min.y - gy;
        maxOffset = b.max.y - gy;
        if (maxOffset < minOffset)
        {
            float t = minOffset;
            minOffset = maxOffset;
            maxOffset = t;
        }
        // 防极端缩成一点
        if (maxOffset - minOffset < 0.05f)
        {
            float mid = 0.5f * (minOffset + maxOffset);
            minOffset = mid - 0.1f;
            maxOffset = mid + 0.1f;
        }
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

    static void EnsureResolved()
    {
        if (_resolved && _area != null) return;
        Transform parent = BattleManager.Instance != null ? BattleManager.Instance.unitRoot : null;
        _area = FindArea(parent);
        if (_area != null)
            _sr = _area.GetComponent<SpriteRenderer>();
        _resolved = true;
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

    static Transform CreateDefaultArea(Transform parent)
    {
        var go = new GameObject(AreaName);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        go.transform.localRotation = Quaternion.identity;
        // 默认较窄可行走带；用户可在 Hierarchy 再缩放
        go.transform.localScale = new Vector3(24f, 0.25f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        sr.color = new Color(0.2f, 0.85f, 0.35f, 0.22f);
        sr.sortingOrder = -20;
        return go.transform;
    }

    static Bounds GetWorldBounds(Transform t)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.bounds;
        var col = t.GetComponent<Collider2D>();
        if (col != null)
            return col.bounds;
        var mr = t.GetComponent<Renderer>();
        if (mr != null)
            return mr.bounds;
        // 无渲染：用 localScale 当高度
        Vector3 p = t.position;
        float halfH = Mathf.Abs(t.lossyScale.y) * 0.5f;
        float halfW = Mathf.Max(1f, Mathf.Abs(t.lossyScale.x) * 0.5f);
        return new Bounds(p, new Vector3(halfW * 2f, halfH * 2f, 0.1f));
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
}
