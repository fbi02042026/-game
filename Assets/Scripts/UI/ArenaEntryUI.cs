using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场主界面入口（2026-10-29）：挂在主界面右列，每日登录按钮下方一格（2026-10-30 纳福入口隐藏后）。
///
/// 【为什么不改预制体】照 <see cref="MiniGameEntryUI"/> 同款套路：运行时克隆 DailyLoginButton
/// （铁律 3：UI 运行时补节点，别改预制体），排版/锚点/字号全部随每日登录按钮走。
/// 【位置】竹篓纳福入口 y - 140（与其上方间距一致）；若竹篓纳福入口不存在（活动下线/已隐藏），
/// 退回「每日登录 y - 140」，即每日签到正下方一格（2026-10-30 起纳福入口已隐藏，走的就是这一档）。
/// </summary>
public static class ArenaEntryUI
{
    public const string NodeName = "ArenaButton";

    /// <summary>与每日登录/纳福入口相同的行距（见 MiniGameEntryUI.OffsetY 注释）。</summary>
    const float OffsetY = -140f;

    const string IconPath = "UI/Art/Main/对战";   // 主界面美术包里的对战图标（交叉双剑+蓝红火焰，2026-10-30 主人指定）

    /// <summary>在右列补「竞技场」入口。可重复调用（已有则只重新接线，不重建）。</summary>
    public static void EnsureInGuildHall(Button dailyLoginButton)
    {
        if (dailyLoginButton == null)
        {
            Debug.LogError("[ArenaEntry] 每日登录按钮为空，无法定位右列，入口未生成");
            return;
        }
        var parent = dailyLoginButton.transform.parent;
        if (parent == null)
        {
            Debug.LogError("[ArenaEntry] 每日登录按钮没有父节点，入口未生成");
            return;
        }

        var exist = parent.Find(NodeName);
        if (exist != null)
        {
            Wire(exist.GetComponent<Button>());
            return;
        }

        var go = Object.Instantiate(dailyLoginButton.gameObject, parent);
        go.name = NodeName;

        // 几何：锚点随每日登录按钮，y 取纳福入口再下一格（纳福隐藏时取每日登录下一格）
        var src = dailyLoginButton.GetComponent<RectTransform>();
        var rt = go.GetComponent<RectTransform>();
        if (src != null && rt != null)
        {
            float baseY = src.anchoredPosition.y;
            var mini = parent.Find(MiniGameEntryUI.NodeName);
            if (mini != null)
            {
                var miniRt = mini.GetComponent<RectTransform>();
                if (miniRt != null) baseY = miniRt.anchoredPosition.y;
            }
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.anchoredPosition = new Vector2(src.anchoredPosition.x, baseY + OffsetY);
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
                    Debug.LogError("[ArenaEntry] 入口图标加载失败，Resources 路径：" + IconPath);
                else
                {
                    img.sprite = sp;
                    img.preserveAspect = true;   // 职业头像进 100×100 槽位不拉伸
                }
            }
        }
        else
        {
            Debug.LogError("[ArenaEntry] 克隆体里没有 Icon 节点，入口图标没处放");
        }

        var labelNode = go.transform.Find("Label");
        if (labelNode != null)
        {
            var txt = labelNode.GetComponent<Text>();
            if (txt != null)
            {
                txt.text = "竞技场";
                // 每日登录 Label 是按 2~4 字排的，3 个字允许溢出宽度防换行
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }
        else
        {
            Debug.LogError("[ArenaEntry] 克隆体里没有 Label 节点，入口文字没处放");
        }

        Wire(go.GetComponent<Button>());
    }

    static void Wire(Button btn)
    {
        if (btn == null)
        {
            Debug.LogError("[ArenaEntry] 入口按钮上没有 Button 组件，点了也没反应");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    static void OnClicked()
    {
        // 跨天/跨月懒结算先对齐，再开弹窗
        ArenaRules.EnsurePeriods(SaveSystem.Instance?.Data);
        ArenaPopupUI.Show();
    }
}
