using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面「跑酷」入口（T7，2026-11-09）：照 <see cref="ArenaEntryUI"/> / <see cref="DiamondAdEntryUI"/> 套路，
/// 运行时克隆 DailyLoginButton 落到右列列尾，不改预制体（铁律 3：UI 运行时补节点）。
/// 【独立新入口】竹篓纳福入口的开关与节点保持原样、互不共用；也不按竹篓按钮的节点名找图/找节点。
/// 【位置】右列列尾：取 RightBar 下所有可见且带 Button 的节点里最低的 y，再下一格；
/// 每次进主界面都重算，前序入口折叠/隐藏自动跟上（幂等，不会越加越多）。
/// 【点击单一出口】EndlessRunGame.CanEnterRun 置灰判定 → TryConsumeEnter 扣每日次数 → Show() 进本。
/// </summary>
public static class RunEntryUI
{
    /// <summary>运行时生成的入口节点名（RightBar 下，与每日登录同父）。</summary>
    public const string NodeName = "RunButton";

    /// <summary>列内标准格距（与 ArenaEntryUI / DiamondAdEntryUI 同档）。</summary>
    const float OffsetY = -140f;

    /// <summary>入口图标（跑酷美术包首图；T8 口径：按路径加载，取不到直接 LogError，不兜底）。</summary>
    const string IconPath = "UI/Art/小游戏/跑酷/1";

    /// <summary>入口文字（2 字，克隆来的 Label 按 2~4 字排的，宽度够用）。</summary>
    const string LabelText = "跑酷";

    /// <summary>在右列补「跑酷」入口。可重复调用（已有则只重新接线 + 重算位置，不重建）。</summary>
    public static void EnsureInGuildHall(Button dailyLoginButton)
    {
        if (dailyLoginButton == null)
        {
            Debug.LogError("[RunEntry] 每日登录按钮为空，无法定位右列，跑酷入口未生成");
            return;
        }
        var parent = dailyLoginButton.transform.parent;
        if (parent == null)
        {
            Debug.LogError("[RunEntry] 每日登录按钮没有父节点，跑酷入口未生成");
            return;
        }

        var exist = parent.Find(NodeName);
        if (exist != null)
        {
            exist.gameObject.SetActive(true);
            PlaceAtColumnBottom(exist as RectTransform, dailyLoginButton, parent);
            Wire(exist.GetComponent<Button>());
            GuildHallUI.Instance?.FitRightBarBgToButtons();   // 列内按钮数可能变了（前序入口显隐），黑底跟着对
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

        // 黑底按「现在列里有几颗按钮」重新拉长（跟随既有排版，不换图不换宽）
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

    static void ApplyIcon(Transform root)
    {
        var iconNode = root.Find("Icon");
        if (iconNode == null)
        {
            Debug.LogError("[RunEntry] 克隆体里没有 Icon 节点，跑酷图标没处放");
            return;
        }
        var img = iconNode.GetComponent<Image>();
        if (img == null) return;
        var sp = Resources.Load<Sprite>(IconPath);
        if (sp == null)
            Debug.LogError("[RunEntry] 跑酷图标加载失败，Resources 路径：" + IconPath);
        else
        {
            img.sprite = sp;
            img.preserveAspect = true;   // 原比例进槽位不拉伸
        }
    }

    static void ApplyLabel(Transform root)
    {
        var labelNode = root.Find("Label");
        if (labelNode == null)
        {
            Debug.LogError("[RunEntry] 克隆体里没有 Label 节点，入口文字没处放");
            return;
        }
        var txt = labelNode.GetComponent<Text>();
        if (txt != null) txt.text = LabelText;
    }

    static void Wire(Button btn)
    {
        if (btn == null)
        {
            Debug.LogError("[RunEntry] 入口按钮上没有 Button 组件，点了也没反应");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    /// <summary>点击单一出口：置灰判定 → 扣每日次数 → 进本。人设尾巴由 GlobalToastUI.Persona 统一套，这里只写平文案。</summary>
    static void OnClicked()
    {
        if (!EndlessRunGame.CanEnterRun)
        {
            GlobalToastUI.Show("今日跑酷次数用完啦");
            return;
        }
        if (EndlessRunGame.TryConsumeEnter())
            EndlessRunGame.Show();
        else
            GlobalToastUI.Show("这局没能开始，次数没扣成");
    }
}
