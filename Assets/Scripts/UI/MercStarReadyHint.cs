using UnityEngine;

/// <summary>
/// 「可升星」首次提示（2026-11-09 主人拍板：用箭头对话框贴在酒馆图标上，冒三个字就行）。
///
/// 视觉全复用现成件：底框 = 抽奖引导那张「像素风空白对话框指针」
/// （BattleManager.DraftHintBubble，静态缓存一份，不另开第二条加载路径）；
/// 载体 = TutorialHintUI 软引导（SetBannerSpriteOnce + Show）：气泡自带尖角指向目标、
/// 不叠手指、不挡点击、autoHideSeconds 到点自己收。零新增 UI / 图片 / 预制体 / 美术排版。
///
/// 触发只由 TownHubController 在「进镇预热收起」后持续轮询调用（每 0.25s）：
/// 回城后停在任意功能页都算，只要底部导航的酒馆菜单此刻可见；不绑死在回城那一瞬或主界面。
/// 不挂页面打开、不挂资源到账、不挂 RedDot.RefreshCommon（那些会变成主动弹）。
/// 账号全局只弹一次，标记存 SaveData.mercStarReadyHintShown（随云存档换机一致、
/// 清档自动归零）；只有气泡真的 Show 出来才写标记（照 SkillOrderGuide 口径）。
/// </summary>
public static class MercStarReadyHint
{
    public const string Text = "可升星";
    const float HoldSeconds = 3f;

    /// <summary>城镇轮询调用（任意功能页）。条件不满足安静返回；true = 本次真的弹出来了。</summary>
    public static bool TryShow(RectTransform tavernIcon)
    {
        var saveSys = SaveSystem.Instance;
        var data = saveSys != null ? saveSys.Data : null;
        if (data == null || data.mercStarReadyHintShown) return false;
        // 回城后停在哪一页都行：只要底部导航那个酒馆菜单此刻还挂在屏幕上就判。
        // 页面把底栏收起来时这次不弹、也不写标记，等玩家切回带底栏的页再试。
        if (tavernIcon == null || !tavernIcon.gameObject.activeInHierarchy) return false;
        if (!MercStarRedDot.HasAnyStarUpReady()) return false;

        // 别的引导正占着这条横幅：这次不弹、不记标记，等下次轮询
        var existing = TutorialHintUI.Instance;
        if (existing != null && existing.IsVisible) return false;

        var bubble = BattleManager.DraftHintBubble;
        if (bubble == null)
        {
            Debug.LogError("[MercStarReadyHint] 取不到底框 Sprite：UI/Art/Common/像素风空白对话框指针");
            return false;
        }

        var hint = TutorialHintUI.Ensure();
        if (hint == null) return false;
        hint.SetBannerSpriteOnce(bubble);
        hint.Show(Text, tavernIcon, HoldSeconds);

        // Show 内部遇到战斗头顶台词会直接 return：只有真显示出来才记档
        if (!hint.IsVisible || hint.CurrentHint != Text) { hint.SetBannerSpriteOnce(null); return false; }   // 没弹出就退掉一次性底框，防污染下一次引导

        data.mercStarReadyHintShown = true;
        saveSys.Save();
        Debug.Log("[MercStarReadyHint] 首次可升星提示已弹出（酒馆图标上方箭头气泡）");
        return true;
    }
}
