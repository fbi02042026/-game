using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 《竹篓纳福》主界面入口（2026-10-28 主人拍板：主界面右列、每日登录按钮**下方**先加入口）。
///
/// 玩法本体尚未施工，所以点击先弹通用告知框（<see cref="StaminaConfirmPopup.ShowNotice"/>）；
/// 玩法做完之后，只改 <see cref="OnClicked"/> 一处接线即可，入口本身不用动。
///
/// 【为什么不改预制体】入口按钮**运行时克隆** DailyLoginButton（铁律 3：UI 运行时补节点，别改预制体）：
/// 排版/锚点/字号全部随每日登录按钮走，以后右列整体挪位跟着走，不会产生第二套坐标。
/// 【位置】RightBar 内按钮间距实测 110（RankButton y=0 / ShopButton y=-110，见 GuildHallUI.prefab），
/// 所以本入口取「每日登录 y + (-110)」。⚠️ 商城按钮（ShopButton, y=-110）当前被 HideUnfinishedHallButtons 隐藏，
/// 与本入口同格；若以后商城放出来，把 OffsetY 改成 -250。
/// </summary>
public static class MiniGameEntryUI
{
    /// <summary>
    /// 活动期开关（2026-10-28 主人拍板：入口由活动开关控制）。
    /// 当前为 false —— 2026-10-30 主人拍板：主界面先隐藏小游戏入口（玩法本体保留，随时可开回来）。
    /// 置 true 即恢复：入口回到每日登录下方一格，对战入口自动再往下顺延一格（见 ArenaEntryUI）。
    /// </summary>
    public const bool EntryEnabled = false;

    public const string NodeName = "MiniGameButton";

    const string IconPath = "UI/Art/邮件/icon礼物";   // 40×44 礼物图（竹篓图到位后换这里）
    // 每日登录图标槽 100 高（超出按钮本体 72），y=-110 时两颗图标只隔 10px——主人反馈太近（10-28），
    // 拉到 -140：图标间隙 10→40px。按钮本体/图标槽与每日登录完全一致（克隆来的），大小天生一样。
    const float OffsetY = -140f;

    /// <summary>在每日登录按钮下方补一个「纳福」入口。可重复调用（已有则只接线不重建）。</summary>
    public static void EnsureInGuildHall(Button dailyLoginButton)
    {
        if (!EntryEnabled) return;

        if (dailyLoginButton == null)
        {
            Debug.LogError("[MiniGameEntry] 每日登录按钮为空，无法定位右列，入口未生成");
            return;
        }
        var parent = dailyLoginButton.transform.parent;
        if (parent == null)
        {
            Debug.LogError("[MiniGameEntry] 每日登录按钮没有父节点，入口未生成");
            return;
        }

        var exist = parent.Find(NodeName);
        if (exist != null)
        {
            Wire(exist.GetComponent<Button>(), exist.gameObject);
            return;
        }

        var go = Object.Instantiate(dailyLoginButton.gameObject, parent);
        go.name = NodeName;

        // 几何：完全跟着每日登录按钮，只在 y 上往下挪一格
        var src = dailyLoginButton.GetComponent<RectTransform>();
        var rt = go.GetComponent<RectTransform>();
        if (src != null && rt != null)
        {
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, OffsetY);
        }

        // 克隆会带走每日登录的子节点（红点等）。入口只需要 Icon + Label，其余一律清掉。
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            var child = go.transform.GetChild(i);
            if (child.name != "Icon" && child.name != "Label")
                Object.Destroy(child.gameObject);
        }

        var iconNode = go.transform.Find("Icon");
        if (iconNode != null)
        {
            var img = iconNode.GetComponent<Image>();
            if (img != null)
            {
                var sp = Resources.Load<Sprite>(IconPath);
                if (sp == null)
                    Debug.LogError("[MiniGameEntry] 入口图标加载失败，Resources 路径：" + IconPath);
                else
                {
                    img.sprite = sp;
                    img.preserveAspect = true;   // 40×44 进 100×100 槽位不拉伸
                }
            }
        }
        else
        {
            Debug.LogError("[MiniGameEntry] 克隆体里没有 Icon 节点，入口图标没处放");
        }

        var labelNode = go.transform.Find("Label");
        if (labelNode != null)
        {
            var txt = labelNode.GetComponent<Text>();
            if (txt != null) txt.text = "纳福";
        }
        else
        {
            Debug.LogError("[MiniGameEntry] 克隆体里没有 Label 节点，入口文字没处放");
        }

        Wire(go.GetComponent<Button>(), go);
    }

    static void Wire(Button btn, GameObject go)
    {
        if (btn == null)
        {
            Debug.LogError("[MiniGameEntry] 入口按钮上没有 Button 组件，点了也没反应");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    /// <summary>
    /// true = 点击直接开《竹篓纳福》玩法界面（v1 第一步：掉落 + 滑动 + 接取 + 60s 结算，尚未接美术与堆叠）；
    /// false = 退回占位告知框。主人验收玩法觉得还不够看时，把这里改回 false 即可，入口其它逻辑不动。
    /// </summary>
    const bool OpenRealGame = true;

    /// <summary>
    /// 入口点击。玩法未上线时走全项目通用告知框（默认弹窗预制体 + TownConfirm 层级）。
    /// </summary>
    static void OnClicked()
    {
        if (OpenRealGame)
        {
            BambooBasketGame.Show();
            return;
        }
        StaminaConfirmPopup.ShowNotice(
            "竹篓纳福",
            "道具从天而降，接住就是你的。\n元旦开席，先别急哈～",
            "知道了",
            null);
    }
}
