using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 登录页「TapTap 登录」按钮绑定。
///
/// 【节点真源 · 2026-10-13】Assets/Resources/Prefabs/Login/LoginUI.prefab → ActionPanel/TapLoginButton
/// （主人手动添加的节点，原名 "StartButton (1)"，按主人要求改名并改由代码绑定）。
/// 尺寸 / 位置 / 贴图一律以预制体为准，代码只绑点击、不写排版、不运行时构建。
///
/// 【TapTap 按钮规范（官方硬性，违反会被渠道打回）】
/// · 必须使用官方素材，不可改样式（禁描边 / 渐变 / 投影 / 纹理 / 点击闪动）；
/// · 默认高度 50 pt，仅允许在宽高比不变的前提下缩放；
/// · 变体只能用「最大圆角半径 / 默认圆角矩形」，禁纯直角。
/// 素材目录：Assets/Resources/UI/Icons/TapTapLogin/（主人已选「品牌蓝_最大圆角半径@2x」拖入节点）。
///
/// 【游客继续已移除 · 2026-10-13 主人拍板】「开始冒险」即游客登录（LoginUI.OnClickStart），
/// 游客继续与它语义重复且 TapTap 大陆区审核要求「无游客模式入口（强制实名）」，故整条删除。
/// </summary>
public static class LoginTapRowBuilder
{
    public const string TapButtonName = "TapLoginButton";

    /// <summary>
    /// 绑定预制体里的 TapTap 登录按钮。节点缺失只报错——
    /// 不运行时兜底构建（项目纪律：不兜底、同一语义只留一个出口）。
    /// </summary>
    public static void Ensure(RectTransform actionPanel, UnityAction onTapLogin)
    {
        if (actionPanel == null)
        {
            Debug.LogError("[LoginTapRow] ActionPanel 为空，无法绑定 TapTap 登录按钮");
            return;
        }
        if (onTapLogin == null)
        {
            Debug.LogError("[LoginTapRow] onTapLogin 为空，拒绝绑定");
            return;
        }

        var tapTf = actionPanel.Find(TapButtonName);
        if (tapTf == null)
        {
            Debug.LogError($"[LoginTapRow] 未找到 ActionPanel/{TapButtonName}：请在 LoginUI.prefab 的 ActionPanel 下挂 TapTap 登录按钮节点（官方素材，禁改样式）。");
            return;
        }

        var btn = tapTf.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError($"[LoginTapRow] {TapButtonName} 缺 Button 组件，无法绑定点击");
            return;
        }

        // 官方素材规范：禁点击闪动 / 染色（2026-10-13）
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveListener(onTapLogin); // 防重复叠加
        btn.onClick.AddListener(onTapLogin);
    }
}
