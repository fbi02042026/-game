using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 开箱模式下背包物品拖拽换位；点击：道具弹出「使用/丢弃」浮层，装备无操作。
/// </summary>
public class BattleBackpackItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public EquipInstance Equip;
    /// <summary>这一格装的是道具时非 null；与 Equip 互斥。</summary>
    public ItemInstance Item;
    public int GridX;
    public int GridY;
    public int Width = 1;
    public int Height = 1;

    RectTransform _rt;
    Transform _origParent;
    Vector2 _origPos;
    bool _dragging;
    bool _moved;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!BattleLootMode.Active) return;
        _dragging = true;
        _moved = false;
        _origParent = transform.parent;
        _origPos = _rt.anchoredPosition;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rt == null) return;
        _moved = true;
        RectTransform parent = _rt.parent as RectTransform;
        if (parent == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
            _rt.anchoredPosition = local;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        var bag = GridBackpackSystem.Instance;
        var ui = BattleUI.Instance;
        if (bag == null || ui == null || (Equip == null && Item == null))
        {
            ResetPos();
            return;
        }

        if (!ui.TryScreenToBackpackCell(eventData.position, eventData.pressEventCamera, out int cx, out int cy))
        {
            ResetPos();
            return;
        }

        // 用「实例」回查条目，避免 overlay 重建后引用失效
        GridBackpackSystem.BackpackItem item = Equip != null
            ? bag.FindItem(Equip)
            : bag.FindItemByItem(Item);
        if (item == null)
        {
            ResetPos();
            return;
        }

        if (!bag.TryMoveItem(item, cx, cy))
        {
            UIManager.Instance?.ShowToast("这里放不下");
            ResetPos();
            return;
        }
        // UpdateBackpackGrid 会重建 overlay
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_moved) return;   // 刚拖完不要顺带弹面板
        if (Item == null) return;

        var bag = GridBackpackSystem.Instance;
        var ui = BattleUI.Instance;
        if (bag == null || ui == null) return;

        var entry = bag.FindItemByItem(Item);
        if (entry == null) return;

        var popup = BackpackItemActionUI.Ensure(ui.transform);
        popup?.Show(entry, eventData.position);
    }

    void ResetPos()
    {
        if (_rt == null) return;
        if (_origParent != null)
            transform.SetParent(_origParent, false);
        _rt.anchoredPosition = _origPos;
    }
}
