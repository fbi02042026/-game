using UnityEngine;

/// <summary>
/// 宠物界面整体缩放自适应。
///
/// 只设 Panel.localScale 一层，不碰任何节点的 anchorMin/Max、anchoredPosition、sizeDelta——节点位置保持 prefab 原样。
/// 只允许缩小、不放大超过 prefab 原样：面板保持 1（手调正常），列表行 0.8 也是手调值。
/// 宠物模型立绘的缩放另由 PetUI.PetArt.cs 的 ART_FILL 控制，与本层无关。
/// 根 CanvasScaler 已是 720×1280 MatchHeight，本层在其之上只做面板级等比缩放。
/// </summary>
public partial class PetUI
{
    void FitPanel()
    {
        if (_panel == null) return;
        var rt = _panel as RectTransform;
        if (rt == null) return;

        // 以 prefab 设计尺寸为准（读 scale=1 时的 rect），只缩小不放大。
        // 2026-11-21 插曲：曾误把整块面板锁到 0.6，界面变得很小——已撤销，面板恢复原样。
        var prev = rt.localScale;
        rt.localScale = Vector3.one;
        Vector2 size = rt.rect.size;
        if (size.x < 1f || size.y < 1f) { rt.localScale = prev; return; }

        float s = Mathf.Min(GameConfig.DESIGN_WIDTH / size.x, GameConfig.DESIGN_HEIGHT / size.y);
        if (s <= 0f || float.IsNaN(s)) s = 1f;
        if (s > 1.1f) s = 1.1f;   // 主人 11-21「界面可以再大一点」：上限从原样 1 提到 1.1，列表行 0.8 不动
        rt.localScale = new Vector3(s, s, 1f);
    }
}
