using UnityEngine;
#if UNITY_WECHATMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 【2026-11-27 主人拍板「③加游戏圈」】微信游戏圈入口桥（官方玩家社区，合规替代 QQ 群导流）。
///
/// 口径：
/// - 游戏圈 = 微信官方玩家社区（wx.createGameClubButton），基础库 2.0.3+ 原生支持，
///   **不需要在 MP 后台单独申请开通**；官方鼓励接入，圈内容管理在 MP 后台「能力地图 → 游戏圈」。
/// - 原生按钮（非 Unity UI）：挂在画面最上层，坐标是**屏幕 CSS 像素、左上角原点**，
///   不会跟随 Unity 布局 —— 由调用方给一个 RectTransform 锚点，本类每次换算重建。
///   （采用「销毁重建」而非缓存实例：弹窗每次打开重算坐标，规避旧实例漂移与回调悬挂。）
/// - 仅 UNITY_WECHATMINIGAME 生效；其它平台 Available=false，调用方不应显示入口。
/// - 创建失败（基础库 &lt; 2.0.3 / 客户端不支持）官方返回 undefined → 本类收到 null 时 LogError 不静默。
///
/// 用法（SettingsPopupUI）：弹窗 Open 后下一帧 ShowAt(锚点)，Close/OnDestroy 时 Hide()。
/// </summary>
public static class WeChatGameClub
{
    /// <summary>是否处于微信小游戏运行时（编译期宏，非微信构建恒 false）。</summary>
    public static bool Available
    {
        get
        {
#if UNITY_WECHATMINIGAME
            return true;
#else
            return false;
#endif
        }
    }

#if UNITY_WECHATMINIGAME
    /// <summary>当前存活的原生游戏圈按钮（单实例；Hide/重建前先销毁旧的）。</summary>
    static WXGameClubButton _btn;
#endif

    /// <summary>
    /// 在 anchor（Unity RectTransform）覆盖的屏幕位置重建一个原生游戏圈按钮。
    /// 弹窗布局刚激活时 Canvas 布局未跑完，调用方应在下一帧再调本方法。
    /// </summary>
    public static void ShowAt(RectTransform anchor)
    {
#if UNITY_WECHATMINIGAME
        if (anchor == null)
        {
            Debug.LogError("[WeChatGameClub] ShowAt 的锚点为空，游戏圈入口不建");
            return;
        }
        DestroyButton();

        // Unity 屏幕物理像素（左下原点）→ 微信 CSS 像素（左上原点）
        var cam = UICanvasSetup.ResolveUiCamera();
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        var sys = WX.GetSystemInfoSync();
        // sys.windowWidth/Height 在 SDK 里是 double，除法/减法结果是 double —— 显式强转 float（CS0266）
        float sx = (float)sys.windowWidth / Screen.width;
        float sy = (float)sys.windowHeight / Screen.height;
        float left = bl.x * sx;
        float width = Mathf.Max(40f, (tr.x - bl.x) * sx);
        float height = Mathf.Max(28f, (tr.y - bl.y) * sy);
        float top = (float)sys.windowHeight - tr.y * sy;   // 顶边向下偏移（左上原点）

        _btn = WX.CreateGameClubButton(new WXCreateGameClubButtonParam
        {
            type = GameClubButtonType.text,
            text = "游戏圈",
            // 样式对齐游戏「深底金框金黄字」口径（同结算新功能框/设置弹窗），圆角胶囊条
            style = new GameClubButtonStyle
            {
                left = (int)left,
                top = (int)top,
                width = (int)width,
                height = (int)height,
                backgroundColor = "#1c2340",
                borderColor = "#5a6b9e",
                borderWidth = 1,
                borderRadius = 8,
                color = "#ffe66e",
                fontSize = 20,
                textAlign = GameClubButtonTextAlign.center,
                lineHeight = (int)height,
            },
        });
        if (_btn == null)
        {
            Debug.LogError("[WeChatGameClub] CreateGameClubButton 返回 null（基础库 < 2.0.3 或客户端不支持），游戏圈入口没建出来");
            return;
        }
        _btn.Show();
#endif
    }

    /// <summary>收起并销毁当前按钮（弹窗关闭 / 场景切换时必须调，防原生按钮残留在画面上）。</summary>
    public static void Hide()
    {
#if UNITY_WECHATMINIGAME
        DestroyButton();
#endif
    }

#if UNITY_WECHATMINIGAME
    static void DestroyButton()
    {
        if (_btn == null) return;
        try
        {
            _btn.Hide();
            _btn.Destroy();
        }
        catch (System.Exception e)
        {
            // 官方按钮回调时机不可控，销毁异常只记录不再抛（按钮生命周期由微信侧持有）
            Debug.LogError("[WeChatGameClub] 销毁游戏圈按钮异常: " + e);
        }
        _btn = null;
    }
#endif
}
