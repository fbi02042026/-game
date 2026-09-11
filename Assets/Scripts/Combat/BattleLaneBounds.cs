using UnityEngine;

/// <summary>
/// 战斗可行走区：Gameplay 为非对称上下界（GameConfig.BATTLE_LANE_MIN/MAX）。
/// 场景 <c>BattleLaneArea</c> 仅作可视化标记，不参与上下界计算。
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
        minOffset = GameConfig.BATTLE_LANE_MIN;
        maxOffset = GameConfig.BATTLE_LANE_MAX;
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
        // 仅可视化参考；实际范围由 BATTLE_LANE_HALF 决定
        go.transform.localScale = new Vector3(24f, 0.25f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        sr.color = new Color(0.2f, 0.85f, 0.35f, 0.22f);
        sr.sortingOrder = -20;
        return go.transform;
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
