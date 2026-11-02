using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面「看视频领钻」入口（2026-11-02 主人拍板：分享功能下线，主界面加个领钻按钮，点击看视频给钻）。
///
/// 【为什么不改预制体】照 <see cref="ArenaEntryUI"/> / <see cref="MiniGameEntryUI"/> 同款套路：
/// 运行时克隆 <c>DailyLoginButton</c>（铁律 3：UI 运行时补节点，别改预制体），
/// 锚点 / 尺寸 / 字号 / 层级全部随每日登录按钮走，只换图标和文字。
/// 【位置】右列列尾：取 RightBar 下所有**可见且带 Button** 的节点里最低的 y，再下一格（-140，与列内标准格距一致）。
/// 每次进主界面都重算 → 广告佣兵折叠上移、竞技场未解锁隐藏等情况都自动跟得上（幂等，不会越加越多）。
/// 【发奖逻辑不在这里】唯一出口 = <see cref="ResourceAdRewards.TryClaimDiamond"/>：
/// 每日 2 次、50 → 100 钻递进，日键复用 <c>adRewardDayKey</c> / 计数 <c>adDiamondClaimCount</c>（不新增存档字段）。
/// 【广告未接入期间】<see cref="RewardedAdBridge.PlatformClosed"/>=true → 点了提示「暂未开放」、不消耗次数。
/// 接微信广告时：填 <see cref="WeChatMiniGameConfig.RewardedAdUnitId"/> → PlatformClosed=false → 本入口自动生效，本类零改动。
/// </summary>
public static class DiamondAdEntryUI
{
    /// <summary>运行时生成的入口节点名（RightBar 下，与每日登录同父）。</summary>
    public const string NodeName = "DiamondAdButton";

    /// <summary>列内标准格距（与 ArenaEntryUI / MiniGameEntryUI 同档）。</summary>
    const float OffsetY = -140f;

    /// <summary>图标：主人 2026-11-02 指定的「看视频领钻」专用图（播放键 + 宝箱 + 钻石 + 横幅）。</summary>
    const string IconPath = "UI/Art/Common/看视频领钻";

    /// <summary>入口文字（5 字，克隆来的 Label 按 2~4 字排的 → 下面按实际宽度自适应缩字号，不写死字号）。</summary>
    const string LabelText = "看视频领钻";

    /// <summary>在右列补「看视频领钻」入口。可重复调用（已有则只重新接线 + 重算位置，不重建）。</summary>
    public static void EnsureInGuildHall(Button dailyLoginButton)
    {
        if (dailyLoginButton == null)
        {
            Debug.LogError("[DiamondAdEntry] 每日登录按钮为空，无法定位右列，领钻入口未生成");
            return;
        }
        var parent = dailyLoginButton.transform.parent;
        if (parent == null)
        {
            Debug.LogError("[DiamondAdEntry] 每日登录按钮没有父节点，领钻入口未生成");
            return;
        }

        var exist = parent.Find(NodeName);
        if (exist != null)
        {
            exist.gameObject.SetActive(true);
            PlaceAtColumnBottom(exist as RectTransform, dailyLoginButton, parent);
            Wire(exist.GetComponent<Button>());
            GuildHallUI.Instance?.FitRightBarBgToButtons();   // 列内按钮数可能变了（竞技场显隐），黑底跟着对
            return;
        }

        var go = Object.Instantiate(dailyLoginButton.gameObject, parent);
        go.name = NodeName;
        go.SetActive(true);

        // 克隆会带走每日登录的子节点（红点等）。入口只需要 Icon + Label，其余一律清掉。
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            var child = go.transform.GetChild(i);
            if (child.name != "Icon" && child.name != "Label")
                Object.Destroy(child.gameObject);
        }

        PlaceAtColumnBottom(go.GetComponent<RectTransform>(), dailyLoginButton, parent);
        ApplyIcon(go.transform);
        ApplyLabel(go.transform);
        Wire(go.GetComponent<Button>());

        // 黑底按「现在列里有几颗按钮」重新拉长（主人 2026-10-30 手调过 270 高，这里只做跟随，不换图不换宽）
        GuildHallUI.Instance?.FitRightBarBgToButtons();
    }

    /// <summary>
    /// 摆到列尾：x 跟每日登录一致，y = 列内所有可见按钮里最低的那个再下移一格。
    /// 只认带 Button 的节点（bg / 装饰节点没有 Button，不会被算进去）。
    /// </summary>
    static void PlaceAtColumnBottom(RectTransform rt, Button dailyLoginButton, Transform parent)
    {
        if (rt == null) return;
        var src = dailyLoginButton.GetComponent<RectTransform>();
        if (src == null) return;

        float lowestY = src.anchoredPosition.y;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == rt || !child.gameObject.activeSelf) continue;
            if (child.GetComponent<Button>() == null) continue;   // bg / 装饰跳过
            var crt = child as RectTransform;
            if (crt == null) continue;
            if (crt.anchoredPosition.y < lowestY) lowestY = crt.anchoredPosition.y;
        }

        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta;
        rt.anchoredPosition = new Vector2(src.anchoredPosition.x, lowestY + OffsetY);
    }

    /// <summary>换图标（取不到 LogError 不静默，铁律 14）。</summary>
    static void ApplyIcon(Transform root)
    {
        var iconNode = root.Find("Icon");
        if (iconNode == null)
        {
            Debug.LogError("[DiamondAdEntry] 克隆体里没有 Icon 节点，入口图标没处放");
            return;
        }
        var img = iconNode.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("[DiamondAdEntry] Icon 节点上没有 Image，入口图标没处放");
            return;
        }
        var sp = Resources.Load<Sprite>(IconPath);
        if (sp == null)
        {
            Debug.LogError("[DiamondAdEntry] 入口图标加载失败，Resources 路径：" + IconPath);
            return;
        }
        img.sprite = sp;
        img.preserveAspect = true;   // 图标进方槽不拉伸
    }

    /// <summary>写文字并按实际宽度自适应缩字号（克隆来的 Label 是按 2~4 字排的，5 字会溢出）。</summary>
    static void ApplyLabel(Transform root)
    {
        var labelNode = root.Find("Label");
        if (labelNode == null)
        {
            Debug.LogError("[DiamondAdEntry] 克隆体里没有 Label 节点，入口文字没处放");
            return;
        }
        var txt = labelNode.GetComponent<Text>();
        if (txt == null)
        {
            Debug.LogError("[DiamondAdEntry] Label 节点上没有 Text，入口文字没处放");
            return;
        }
        txt.text = LabelText;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;   // 宁可溢出一点，不换行（换行会顶出黑底）
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // 自适应缩字号：preferredWidth 依赖字体，首帧可能为 0 → 只在拿到有效宽度时缩，绝不放大
        var rt = labelNode as RectTransform;
        if (rt != null && rt.rect.width > 1f && txt.preferredWidth > rt.rect.width)
        {
            int scaled = Mathf.FloorToInt(txt.fontSize * rt.rect.width / txt.preferredWidth);
            txt.fontSize = Mathf.Max(12, scaled);
        }
    }

    static void Wire(Button btn)
    {
        if (btn == null)
        {
            Debug.LogError("[DiamondAdEntry] 入口按钮上没有 Button 组件，点了也没反应");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    static void OnClicked()
    {
        // 发奖 / 日限 / 广告拉起全在 ResourceAdRewards 唯一出口里，这里只转发点击
        ResourceAdRewards.TryClaimDiamond();
    }
}
