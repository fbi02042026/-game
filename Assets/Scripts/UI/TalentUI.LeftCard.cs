using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天赋左列「节点气泡卡」（2026-11-19 主人拍板：点击图标弹参考图同款气泡弹窗——只改排版，素材全用项目现成）。
/// <para>结构：米色气泡卡 → 胶囊标题（属性名）→ 描述（属性名+数值连写，第三轮口径）
/// → 金棕按钮「解锁 / 金币图标 x 数量」；建树在 <see cref="EnsureCostBubble"/>（BubbleCard 左右列共用），
/// 这里只管填值与确认。</para>
/// <para>只负责「展示 + 确认」，购买执行体仍是 <c>DoBuyLeft</c>，不改任何购买/存档规则。</para>
/// </summary>
public partial class TalentUI
{
    CostBubbleRefs _leftBubbleRefs;   // 左列气泡卡引用缓存（复用不重建）
    int _pendingLeftIndex = -1;       // 当前气泡卡对应的左列节点（0-based），-1=未开

    /// <summary>点左列节点：弹气泡卡（展示效果 + 消耗），确认才买；金币不足也照样弹（按钮数量标红看缺口）。</summary>
    void ConfirmBuyLeft(int index0)
    {
        var def = index0 >= 0 && index0 < TalentDefs.Left.Length ? TalentDefs.Left[index0] : null;
        if (def == null) return;
        EnsureLeftCard();
        if (_leftBubbleRefs.root == null) return;   // 建卡失败已 LogError，静默即止（不退化成直接买）

        _pendingLeftIndex = index0;
        var data = SaveSystem.Instance?.Data;
        var talents = data != null ? data.talents : null;
        int cost = TalentSystem.GetLeftGoldCost(def, talents);
        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        bool canBuy = gold >= cost;

        // 参考图排版：胶囊标题（属性名）→ 描述（属性效果连属性名一起）→「解锁 + 金币x数量」
        if (_leftBubbleRefs.name != null) _leftBubbleRefs.name.text = def.name;
        if (_leftBubbleRefs.desc != null)
            _leftBubbleRefs.desc.text = string.IsNullOrEmpty(def.effect.display) ? "" : def.effect.display.Replace(" ", "");
        if (_leftBubbleRefs.okLabel != null) _leftBubbleRefs.okLabel.text = "解锁";
        if (_leftBubbleRefs.cost != null)
        {
            _leftBubbleRefs.cost.text = "x" + cost;
            _leftBubbleRefs.cost.color = canBuy ? Color.white : BubbleCostRed;   // 不足标红（项目口径：不可购买标红）
        }
        // 金币不足时按钮仍可点（点走 OnLeftCardOk → DoBuyLeft → TryUnlockLeft 给 toast），不禁用。

        _leftBubbleRefs.root.SetActive(true);
        _leftBubbleRefs.root.transform.SetAsLastSibling();
        // 【第15轮·主人拍板】气泡卡贴到被点图标正上方（尾巴指向图标），不再全屏居中
        var lv = index0 >= 0 && index0 < _leftViews.Count ? _leftViews[index0] : null;
        var anchorRect = lv == null ? null
            : lv.icon != null ? lv.icon.rectTransform
            : (lv.root != null ? lv.root.transform as RectTransform : null);
        PositionCostBubble(_leftBubbleRefs, anchorRect);
    }

    /// <summary>懒建左列气泡卡：金棕「金币升级」按钮底 + 金币图标（icon_gold）。</summary>
    void EnsureLeftCard()
    {
        EnsureBubbleSprites();
        _leftBubbleRefs = EnsureCostBubble("LeftNodeCompareCard", _leftBubbleRefs, CloseLeftCard, OnLeftCardOk,
            _bubbleGoldBtnSpr, _bubbleGoldIconSpr);
    }

    void OnLeftCardOk()
    {
        int idx = _pendingLeftIndex;
        CloseLeftCard();
        if (idx >= 0) DoBuyLeft(idx);
    }

    void CloseLeftCard()
    {
        _pendingLeftIndex = -1;
        if (_leftBubbleRefs.root != null) _leftBubbleRefs.root.SetActive(false);
    }
}
