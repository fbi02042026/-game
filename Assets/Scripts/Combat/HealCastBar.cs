using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 治疗读条（2026-11-18 主人拍板）：玩家奶 / 佣兵奶读满 <see cref="GameConfig.HEAL_CAST_SEC"/> 秒才结算，
/// 特效与左侧通知条都等读满才出；读条期间施法者定身，头顶挂同款攒槽条。
/// 眩晕（近战 Boss 砸地）或死亡 → 取消：回调不触发、不扣蓝、不进 CD。
/// 冻场（UnitsCanAct=false）只暂停读条、不取消 —— 与技能 CD 同口径。
///
/// 【单一出口】Begin / IsCasting / Cancel 全在本类，玩家链与佣兵链共用，不各写一份。
/// 组件挂在施法者 gameObject 上，随其生命周期走；切关复用走 Cancel 清理。
/// </summary>
public class HealCastBar : MonoBehaviour
{
    static Sprite _white;
    const float BAR_W = 0.9f;
    const float BAR_H = 0.09f;
    const float HEAD_GAP = 0.22f;
    static readonly Color FILL_COLOR = new Color(1f, 0.72f, 0.42f, 1f); // 沿用 Boss 攒槽色

    UnitBase _caster;
    Action _onComplete;
    float _elapsed;
    bool _done;
    RectTransform _rt;
    Image _fill;
    GameObject _root;

    /// <summary>施法者是否正在读治疗条（读条期间禁止移动 / 禁止再放技能 / 防重入）。</summary>
    public static bool IsCasting(UnitBase caster)
    {
        if (caster == null) return false;
        var b = caster.GetComponent<HealCastBar>();
        return b != null && !b._done;
    }

    /// <summary>启动读条；同一施法者重复调用会先取消上一条（防重入）。读满触发 onComplete 一次。</summary>
    public static void Begin(UnitBase caster, Action onComplete)
    {
        if (caster == null || caster.isDead) return;
        var old = caster.GetComponent<HealCastBar>();
        if (old != null) old.CancelInternal();
        var b = caster.gameObject.AddComponent<HealCastBar>();
        b._caster = caster;
        b._onComplete = onComplete;
        b._elapsed = 0f;
        b._done = false;
        b.BuildUi();
    }

    /// <summary>取消读条：回调不触发（不扣蓝 / 不进 CD / 不出特效）。</summary>
    public static void Cancel(UnitBase caster)
    {
        if (caster == null) return;
        var b = caster.GetComponent<HealCastBar>();
        if (b != null) b.CancelInternal();
    }

    void BuildUi()
    {
        var go = new GameObject("HealCastBar", typeof(RectTransform), typeof(Canvas));
        _root = go;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        canvas.sortingOrder = GameConfig.SORT_VFX + 9;
        var rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(BAR_W, BAR_H);
        _rt = rt;

        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bg = bgGo.GetComponent<Image>();
        bg.sprite = White();
        bg.color = new Color(0.06f, 0.06f, 0.09f, 0.85f);
        bg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fRt = fillGo.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = fRt.offsetMax = Vector2.zero;
        _fill = fillGo.GetComponent<Image>();
        _fill.sprite = White();                       // Filled 必须有 sprite，fillAmount 才生效
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 0f;
        _fill.color = FILL_COLOR;
        _fill.raycastTarget = false;
        FollowHead();
    }

    static Sprite White()
    {
        if (_white == null)
            _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return _white;
    }

    void FollowHead()
    {
        if (_rt == null || _caster == null) return;
        float topY;
        var sr = _caster.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) topY = sr.bounds.max.y;
        else topY = _caster.transform.position.y + 1.1f;
        _rt.position = new Vector3(_caster.transform.position.x, topY + HEAD_GAP, _caster.transform.position.z);
    }

    void Update()
    {
        if (_done) return;
        if (_caster == null || _caster.isDead) { CancelInternal(); return; }
        if (_caster.IsStunned) { CancelInternal(); return; }              // 砸地眩晕打断：作废、不扣蓝
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct) return; // 冻场暂停，不取消

        _elapsed += Time.deltaTime;
        FollowHead();
        if (_fill != null) _fill.fillAmount = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, GameConfig.HEAL_CAST_SEC));
        if (_elapsed < GameConfig.HEAL_CAST_SEC) return;

        var cb = _onComplete;
        _onComplete = null;
        _done = true;
        if (_root != null) Destroy(_root);
        Destroy(this);
        if (cb != null) cb();
    }

    void CancelInternal()
    {
        if (_done) return;
        _done = true;
        _onComplete = null;
        if (_root != null) { Destroy(_root); _root = null; }
        Destroy(this);
    }

    void OnDestroy()
    {
        // 外部直接 Destroy 组件（切关 / 单位回收）时，保证头顶条不残留
        if (_root != null) Destroy(_root);
    }
}
