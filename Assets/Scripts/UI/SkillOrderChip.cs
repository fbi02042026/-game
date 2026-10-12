using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 技能顺序条上的一枚 chip：可横向拖拽，决定技能的<b>自动释放优先级</b>。
/// 拖拽骨架复用 <see cref="BattleBackpackItemDrag"/>（工程内唯一有完整 Begin/Drag/End 三段的实现）。
///
/// 说明：不做「跟手位移」。横向列表重排用 SetSiblingIndex + HorizontalLayoutGroup 自动回流，
/// 拖动时chip 直接跳到目标位置并带走后时的布局，比做跟手漂移更稳（无需临时脱离父节点、
/// 也不会出现释放后飘回原位的幽灵帧）。
/// </summary>
public class SkillOrderChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform _rt;
    RectTransform _row;
    bool _dragging;

    /// <summary>本 chip 代表的技能 id。</summary>
    public string SkillId = "";

    /// <summary>
    /// 单格步进宽度（chip 宽 + spacing）。用于把指针本地 x 换算成目标下标，
    /// 不依赖 LayoutGroup 的当前布局，避免「回流慢一帧导致算错位置」。
    /// </summary>
    public float SlotStep = 1f;

    /// <summary>玩家拖过一次就置 true，供引导判断「这一步是不是真的完成了」。</summary>
    public bool DraggedOnce { get; private set; }

    /// <summary>此刻是不是正在被拖。技槽点击弹简介要据此排除「拖完顺带触发点击」。</summary>
    public bool IsDragging => _dragging;

    /// <summary>
    /// 是否允许拖拽。战斗 UI 里只在「整理阶段」（BattleLootMode.Active）打开，
    /// 避免战斗中误触改掉释放顺序。
    /// </summary>
    public bool DragEnabled = true;

    /// <summary>
    /// true = 位置由 HorizontalLayoutGroup 自动回流（三选一排序行，原始用法）。
    /// false = 槽位是美术手摆的 anchoredPosition、没有 LayoutGroup（战斗底部 4 技槽），
    ///         此时 SetSiblingIndex **不会**让节点移动，必须走「跟手位移 + 松手重排」。
    /// </summary>
    public bool UseLayoutGroup = true;

    /// <summary>顺序变化回调 (from, to)。挂载方负责写回 RunLoadout.MoveSkill 并刷新。</summary>
    public System.Action<int, int> OnOrderChanged;

    int _beginIndex = -1;
    Vector2 _origPos;
    Vector2 _dragLocal;

    public void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _row = _rt != null ? _rt.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!DragEnabled) return;
        if (_rt == null || _row == null) return;
        _dragging = true;
        _beginIndex = _rt.GetSiblingIndex();
        _origPos = _rt.anchoredPosition;
        // 置顶避免被兄弟节点盖住
        _rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!DragEnabled || !_dragging || _rt == null || _row == null) return;
        if (SlotStep <= 0f) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _row, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        int count = _row.childCount;
        if (count <= 1) return;
        _dragLocal = local;

        if (UseLayoutGroup)
        {
            // 行内横向居中排布：第 i 格中心相对行中心为 (i - (n-1)/2) * step
            int target = Mathf.Clamp(
                Mathf.RoundToInt(local.x / SlotStep + (count - 1) * 0.5f), 0, count - 1);

            int cur = _rt.GetSiblingIndex();
            if (target == cur) return;

            _rt.SetSiblingIndex(target);
            DraggedOnce = true;
            return;
        }

        // 手摆位置：直接跟手位移。顺序不在这一步改，等松手由挂载方写回并刷新。
        _rt.anchoredPosition = local;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        if (!UseLayoutGroup)
        {
            int target = NearestSlotIndex(_dragLocal.x);
            if (_beginIndex >= 0 && target >= 0 && target != _beginIndex)
            {
                DraggedOnce = true;
                OnOrderChanged?.Invoke(_beginIndex, target);
            }
            // 位置不在这里还原：挂载方写完 RunLoadout 后会刷新技槽，届时按美术原位置重排
            _rt.anchoredPosition = _origPos;
            // 【2026-10-12 主人报「能拖但不生效」】OnBeginDrag 的 SetAsLastSibling 只为拖拽时不被兄弟
            // 盖住，但它把本槽的 sibling 下标**永久**顶到最后一位 → 槽位下标与技能顺序从此错位
            // （下一次拖动 _beginIndex 就指错人）。这里放回原位；真生效时挂载方紧接着会刷新重建。
            if (_beginIndex >= 0 && _beginIndex < _row.childCount)
                _rt.SetSiblingIndex(_beginIndex);
        }
        _beginIndex = -1;
    }

    /// <summary>
    /// 找出离指针 x 最近的槽位下标。比用 SlotStep 换算更稳——
    /// 手摆的槽位未必严格等距，按「最近」取不会因为美术微调而算偏。
    /// </summary>
    int NearestSlotIndex(float localX)
    {
        if (_row == null) return -1;
        int count = _row.childCount;
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var c = _row.GetChild(i) as RectTransform;
            if (c == null) continue;
            // 【2026-10-12 主人报「能拖但不生效」→ 根因】必须排除**正在拖的这个**：
            // OnDrag 已把它的 anchoredPosition 设成指针位置，它到指针的距离恒为 0 →
            // 不排除的话「最近槽位」永远是它自己，而它又被 SetAsLastSibling 顶到最后一位，
            // 于是 target 恒等于最后那个槽：2 个技能时 to=3 越界，OnSkillSlotReordered 直接 return
            // —— 表现就是「能跟手拖，松手顺序不变」。
            if (c == _rt) continue;
            float d = Mathf.Abs(c.anchoredPosition.x - localX);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>当前 chip 在行里的序号（0 起）。</summary>
    public int SiblingIndex => _rt != null ? _rt.GetSiblingIndex() : -1;
}
