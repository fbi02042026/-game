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
    Canvas _canvas;
    float _headY;          // 读条开始那一刻定格的头顶高度
    float _headFootY;      // 定格时脚下的 Y（识别换车道用）
    bool _headYCached;

    /// <summary>施法者是否正在读治疗条（读条期间禁止移动 / 禁止再放技能 / 防重入）。</summary>
    public static bool IsCasting(UnitBase caster)
    {
        if (caster == null) return false;
        var b = caster.GetComponent<HealCastBar>();
        return b != null && !b._done;
    }

    /// <summary>启动读条。读满触发 onComplete 一次。</summary>
    /// <remarks>
    /// 【2026-11-25 主人报「头顶条一截一截退涨」】原先「重复调用先取消上一条再新建」，新建会把 _elapsed 清 0，
    /// 画面上就是条突然缩回去一截再从头涨。现在改成：已经在读条就<b>什么都不做</b>（保留进度与已完成回调）。
    /// 调用方本来就都有 IsCasting 闸（玩家链 / 佣兵链各一处），这里只是单一出口的最后一道保险，
    /// 不让任何后来的调用点把进度清零；真要重开走 Cancel 再 Begin 两步显式表达。
    /// </remarks>
    public static void Begin(UnitBase caster, Action onComplete)
    {
        if (caster == null || caster.isDead) return;
        var old = caster.GetComponent<HealCastBar>();
        if (old != null)
        {
            if (!old._done) return;          // 正在读条：不重建、不清进度
            old.CancelInternal();            // 已读完但组件还没销毁的残壳：先清掉再建
        }
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
        _canvas = canvas;
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

    /// <summary>
    /// 跟头：X / Z 跟随施法者，Y 用读条开始那一刻<b>定格</b>的头顶高度。
    /// 【2026-11-25 主人报「只有受击时读条才会缩一块」】原来每帧取
    /// <c>GetComponentInChildren&lt;SpriteRenderer&gt;().bounds.max.y</c>：受击播 DAMAGED 时 SPUM 会换部件贴图，
    /// 包围盒高度逐帧变 → 整条 WorldSpace 条上下跳，跳进身体/前景遮挡区就被挡掉一截，看上去就是「条被缩减」。
    /// 读条期间施法者本来就定身（<see cref="IsCasting"/>），高度定格最稳；只有脚下 Y 大幅变化（换车道/传送）才重取一次。
    /// </summary>
    void FollowHead()
    {
        if (_rt == null || _caster == null) return;
        float footY = _caster.transform.position.y;
        if (!_headYCached || Mathf.Abs(footY - _headFootY) > 0.5f) CacheHeadHeight(footY);
        _rt.position = new Vector3(_caster.transform.position.x, _headY + HEAD_GAP, _caster.transform.position.z);
        // 单位遮挡按世界 Y 排序（GameConfig.ComputeUnitSortOrder），低处站位的单位 order 会超过 SORT_VFX+9，
        // 那样自己的身子会盖住头顶条。取两者最大值：既能盖住本体，也不掉到特效层之下（2026-11-25）。
        if (_canvas != null)
            _canvas.sortingOrder = Mathf.Max(GameConfig.SORT_VFX + 9,
                GameConfig.ComputeUnitSortOrder(footY) + 10);
    }

    void CacheHeadHeight(float footY)
    {
        var sr = _caster.GetComponentInChildren<SpriteRenderer>();
        _headY = sr != null ? sr.bounds.max.y : _caster.transform.position.y + 1.1f;
        _headFootY = footY;
        _headYCached = true;
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
