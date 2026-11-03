using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 看广告送佣兵活动主界面入口（2026-10-31 主人拍板：放主界面、每日登录按钮**上面**一格）。
///
/// 【2026-10-31 主人手摆】主人已在预制体 RightBar 手摆入口（节点名「广告佣兵」，图标/文案/排版
/// 均由主人手工调整，RightBar 位置参数与黑底高度也由主人手调）——运行时【不再克隆】【不再动
/// Icon/Label/几何/黑底】，只做两件事：Wire 按钮点击 + 挂红点（ArenaEntryUI.HandPlacedName 同款）。
/// 【折叠】GameConfig.ADS_ENABLED=false（广告未接入过渡期）整个活动折叠 → 入口隐藏（与面板同口径）。
/// 【红点】入口右上角挂 RedDot.AdMercEvent 键（有可领里程碑时亮，AdMercEventSystem 统一 Set）。
/// </summary>
public static class AdMercEventEntryUI
{
    /// <summary>主人手摆的入口节点名（GuildHall.prefab RightBar 下，与每日登录同父）。</summary>
    public const string HandPlacedName = "广告佣兵";

    /// <summary>旧克隆口径节点名（历史遗留；现在只用于 ADS_ENABLED=false 时收掉旧克隆体）。</summary>
    const string LegacyCloneName = "AdMercEventButton";

    /// <summary>
    /// 接线右列「广告佣兵」入口。可重复调用（幂等：接线前先 RemoveAllListeners）。
    /// dailyLoginButton 仅用于定位 RightBar 父节点。
    /// </summary>
    public static void EnsureInGuildHall(Button dailyLoginButton)
    {
        if (dailyLoginButton == null)
        {
            Debug.LogError("[AdMercEntry] 每日登录按钮为空，无法定位右列，入口未接线");
            return;
        }
        var parent = dailyLoginButton.transform.parent;
        if (parent == null)
        {
            Debug.LogError("[AdMercEntry] 每日登录按钮没有父节点，入口未接线");
            return;
        }

        // 过渡期（广告未接入）：活动整体折叠，入口隐藏（手摆节点 + 历史克隆体都收掉），
        // 下方两颗按钮（每日登录、对战）上移一格补位；黑底 bg 不动
        // （2026-11-01 主人指令当天曾让 bg 跟移，同日晚主人纠正「按钮上移后那个黑底不要移」→ bg 保持原位）
        if (!GameConfig.ADS_ENABLED)
        {
            var handFolded = parent.Find(HandPlacedName);
            if (handFolded != null) handFolded.gameObject.SetActive(false);
            var legacyFolded = parent.Find(LegacyCloneName);
            if (legacyFolded != null) legacyFolded.gameObject.SetActive(false);
            FoldUpBelowButtons(parent, dailyLoginButton.transform as RectTransform, handFolded);
            GuildHallUI.Instance?.FitRightBarBgToButtons();   // 【2026-11-03 主人】折叠后黑底跟着缩短
            return;
        }

        // 主人手摆节点：只接线 + 挂红点，Icon/Label/几何/黑底一律不碰（主人已手调好）
        var hand = parent.Find(HandPlacedName);
        if (hand != null)
        {
            hand.gameObject.SetActive(true);
            Wire(hand.GetComponent<Button>());
            RedDot.Bind(hand, RedDot.AdMercEvent, new Vector2(-8f, -8f), 26f);
            GuildHallUI.Instance?.FitRightBarBgToButtons();   // 【2026-11-03 主人】按钮在列黑底按按钮拉长
            return;
        }

        // 兜底：手摆节点没找到才报错指路（不做克隆兜底——主人已在预制体摆好，缺了要当场看见）
        Debug.LogError($"[AdMercEntry] 预制体 RightBar 下未找到手摆入口节点「{HandPlacedName}」，" +
                       "入口没有可点的按钮（请在 GuildHall.prefab 右列补上该节点）");
    }

    static void Wire(Button btn)
    {
        if (btn == null)
        {
            Debug.LogError($"[AdMercEntry] 入口节点「{HandPlacedName}」上没有 Button 组件，点了也没反应");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    /// <summary>标准格距：与 ArenaEntryUI / MiniGameEntryUI 的 OffsetY 同一档（每日登录上下格距）。</summary>
    const float FoldSlotY = 140f;

    /// <summary>已按标准格距补过位的每日登录按钮（实例 ID）——预制体缺手摆节点时防重复上移。</summary>
    static readonly System.Collections.Generic.HashSet<int> s_foldedDailyIds = new System.Collections.Generic.HashSet<int>();

    /// <summary>
    /// 入口隐藏后，下方两颗按钮（每日登录、对战）上移补位；右列黑底 bg 不动（2026-11-01 主人拍板「黑底不要移」）。
    /// 预制体摆了入口节点：位移量 = 入口原位 y − 每日登录现位 y（补位后差值为 0 → 幂等）。
    /// 预制体没摆入口节点（当前仓库实况）：按标准格距 FoldSlotY 上移一格，本实例只补一次。
    /// </summary>
    static void FoldUpBelowButtons(Transform rightBar, RectTransform dailyRt, Transform handNode)
    {
        if (dailyRt == null) return;
        float dy;
        var handRt = handNode as RectTransform;
        if (handRt != null)
        {
            dy = handRt.anchoredPosition.y - dailyRt.anchoredPosition.y;
            if (Mathf.Abs(dy) < 0.5f) return;   // 已补过位（幂等）
        }
        else
        {
            if (!s_foldedDailyIds.Add(dailyRt.GetInstanceID())) return;   // 本实例已补过位
            dy = FoldSlotY;
        }

        ShiftUp(dailyRt, dy);

        var arena = rightBar.Find("对战");
        if (arena == null) arena = rightBar.Find(ArenaEntryUI.NodeName);
        if (arena != null)
            ShiftUp(arena as RectTransform, dy);
        else
            Debug.LogError("[AdMercEntry] 未找到「对战」按钮，补位只挪了每日登录");
    }

    static void ShiftUp(RectTransform rt, float dy)
    {
        if (rt == null) return;
        rt.anchoredPosition += new Vector2(0f, dy);
    }

    static void OnClicked()
    {
        AdMercEventUI.Show();
    }
}
