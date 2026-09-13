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

    public void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _row = _rt != null ? _rt.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rt == null || _row == null) return;
        _dragging = true;
        // 置顶避免被兄弟节点盖住
        _rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rt == null || _row == null) return;
        if (SlotStep <= 0f) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _row, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        int count = _row.childCount;
        if (count <= 1) return;

        // 行内横向居中排布：第 i 格中心相对行中心为 (i - (n-1)/2) * step
        int target = Mathf.Clamp(
            Mathf.RoundToInt(local.x / SlotStep + (count - 1) * 0.5f), 0, count - 1);

        int cur = _rt.GetSiblingIndex();
        if (target == cur) return;

        _rt.SetSiblingIndex(target);
        DraggedOnce = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
    }

    /// <summary>当前 chip 在行里的序号（0 起）。</summary>
    public int SiblingIndex => _rt != null ? _rt.GetSiblingIndex() : -1;
}
