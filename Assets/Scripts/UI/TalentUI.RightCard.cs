using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天赋右列「节点气泡卡」（2026-11-19 主人拍板：解锁/升级费用确认改成左列同款参考图气泡弹窗，
/// 按钮换紫色「天赋石升级」底 + 天赋石图标 x 数量——图标与顶栏同款）。
/// <para>替代 2026-10-21 起的 StaminaConfirmPopup 文字确认框（Popups.cs 的 OpenCostConfirm 已改调这里）；
/// JobChoice 6选1 / Mutex 二选一弹层不在这，保持原流程。</para>
/// <para>只负责「展示 + 确认」，解锁/升级执行体仍是 TryUnlockRightNode / TryUpgradeRightNode，不改购买规则。</para>
/// </summary>
public partial class TalentUI
{
    CostBubbleRefs _rightBubbleRefs;                 // 右列气泡卡引用缓存（复用不重建）
    TalentDefs.TalentRightNode _pendingRightNode;    // 当前气泡卡对应的右列节点
    int _pendingRightFromLevel = -1;                 // 打开时的等级（0=未解锁走解锁流，>0=升级流）

    /// <summary>
    /// 打开右列费用气泡卡（原 StaminaConfirmPopup 版口径保留）：
    /// 天赋石不足也照样弹（按钮数量标红看缺口），确定时校验不过再 toast —— 不「点一下直接弹不够」。
    /// </summary>
    void OpenRightCostBubble(TalentDefs.TalentRightNode node, int fromLevel, RectTransform anchorRect = null)
    {
        if (node == null) return;

        bool unlock = fromLevel <= 0;
        int cost = node.costByLevel != null && fromLevel < node.costByLevel.Length ? node.costByLevel[fromLevel] : 0;
        var data = SaveSystem.Instance?.Data;
        int have = data?.talentPoints ?? 0;
        int chosenJob = TalentDefs.GetRightNodeChosenJob(data != null ? data.talents : null, node.id);
        int effLevel = unlock ? 1 : fromLevel + 1;
        string eff = node.EffectSummary(effLevel, chosenJob);

        EnsureRightCard();
        if (_rightBubbleRefs.root == null) return;   // 建卡失败已 LogError，静默即止

        _pendingRightNode = node;
        _pendingRightFromLevel = fromLevel;

        // 参考图排版：胶囊标题（天赋名）→ 描述（解锁=效果；升级=Lv x → y + 效果）→「解锁/升级 + 天赋石x数量」
        if (_rightBubbleRefs.name != null)
            _rightBubbleRefs.name.text = !string.IsNullOrEmpty(node.name) ? node.name : "天赋";
        if (_rightBubbleRefs.desc != null)
            _rightBubbleRefs.desc.text = unlock ? eff : ("Lv" + fromLevel + " → " + effLevel + "\n" + eff);
        if (_rightBubbleRefs.okLabel != null) _rightBubbleRefs.okLabel.text = unlock ? "解锁" : "升级";
        if (_rightBubbleRefs.cost != null)
        {
            _rightBubbleRefs.cost.text = "x" + cost;
            _rightBubbleRefs.cost.color = have >= cost ? Color.white : BubbleCostRed;   // 不足标红
        }

        _rightBubbleRefs.root.SetActive(true);
        _rightBubbleRefs.root.transform.SetAsLastSibling();
        // 【第15轮·主人拍板】气泡卡贴到被点行正上方（尾巴指向该行），不再全屏居中
        PositionCostBubble(_rightBubbleRefs, anchorRect);
    }

    /// <summary>懒建右列气泡卡：紫色「天赋石升级」按钮底 + 天赋石图标（顶栏同款）。</summary>
    void EnsureRightCard()
    {
        EnsureBubbleSprites();
        _rightBubbleRefs = EnsureCostBubble("RightNodeCompareCard", _rightBubbleRefs, CloseRightCard, OnRightCardOk,
            _bubbleStoneBtnSpr, _bubbleStoneIconSpr);
    }

    void OnRightCardOk()
    {
        var node = _pendingRightNode;
        int fromLevel = _pendingRightFromLevel;
        CloseRightCard();
        if (node == null) return;

        bool unlock = fromLevel <= 0;
        string reason;
        bool ok = unlock
            ? TalentSystem.TryUnlockRightNode(node.id, 0, out reason)
            : TalentSystem.TryUpgradeRightNode(node.id, out reason);
        if (!ok)
        {
            Debug.Log("[TalentUI] " + reason);
            GlobalToastUI.Show(reason, true);
            return;
        }
        RefreshAll();
    }

    void CloseRightCard()
    {
        _pendingRightNode = null;
        _pendingRightFromLevel = -1;
        if (_rightBubbleRefs.root != null) _rightBubbleRefs.root.SetActive(false);
    }
}
