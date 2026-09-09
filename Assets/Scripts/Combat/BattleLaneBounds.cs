using UnityEngine;

/// <summary>
/// 战斗可行走区：Gameplay 固定为 ±GameConfig.BATTLE_LANE_HALF。
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
        float h = GameConfig.BATTLE_LANE_HALF;
        minOffset = -h;
        maxOffset = h;
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
