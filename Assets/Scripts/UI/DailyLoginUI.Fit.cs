using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：竖屏背景适配（ApplyFit cover 缩放、标头锚点归一、瘦屏判定）
public partial class DailyLoginUI
{

    // ============================================================
    // 竖屏背景等比缩放适配（主人要求：搬进界面自身，不通用）
    // ============================================================

    /// <summary>是否更瘦屏：逻辑 UI 高 > 设计高 + 1 时为真（9:16 标准屏逻辑高恒 1280，必须保持原样）。</summary>
    static bool IsThinnerScreen()
    {
        float uiLogicalH = DesignAspectLetterbox.ResolveUiMatch() < 0.5f
            ? GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height)
            : GameConfig.DESIGN_HEIGHT;
        return uiLogicalH > GameConfig.DESIGN_HEIGHT + 1f;
    }

    /// <summary>
    /// 每日登录弹窗竖屏适配（2026-10-17 主人反馈两件事：①背景没全屏 ②左上角「每日登录」标头自己上移）。
    ///   - **Bg_Full 中心锚点 + 严格居中**：锚点/pivot 一律 (0.5,0.5)，anchoredPosition 归零。
    ///     旧版首次运行会把「中心偏移」按【当前屏幕】捕获——预制体偏移本是设计稿居中噪声 (-1,-2)，
    ///     在瘦屏（逻辑高 ~1561）上被放大记成 -142，背景被钉在屏幕中心下方，
    ///     cover 放大后顶部露出约 142px（主城顶栏从上面透出来 = 背景没全屏的根因）。现直接严格居中。
    ///   - 只在「更瘦屏」(IsThinnerScreen) 放大，且**只放大不缩小**：标准 9:16 保持预制体原 sizeDelta 原样；
    ///       倍数按「预制体实际渲染尺寸 = sizeDelta × localScale」算 cover，
    ///       避免与美术自带的 1.16 缩放叠加导致底图放过头（旧代码就是这么放飞的）。
    ///   - **Title 改挂中心锚**（2026-10-17 主人反馈：标头自己往上移）：预制体里它是顶中锚 (0.5,1)，
    ///     瘦屏画布变高时它钉在屏幕顶不动，而 CellsPanel / InfoBar 全是中心锚跟着中心下沉 →
    ///     标头视觉上脱离内容上飘。按设计稿 720×1280 换算成等价中心锚站位，与内容组同参考系（见 NormalizeTitleToCenterAnchor）。
    ///   - 幂等：首次记录 Bg_Full 原始 sizeDelta，重复调用都从基准重算；
    ///       EnableDailyLoginFit=false / 标准屏 → 倍率恒 1（回到预制体原样）。
    /// 铁律：运行时改 rect，绝不碰 .prefab。找不到节点、或父级还没量出尺寸则 no-op 不报错。
    /// </summary>
    void ApplyFit()
    {
        if (_fitApplying) return;
        _fitApplying = true;
        try
        {
            // ⓪ 标头与内容组统一参考系：先于背景处理，节点缺失不阻塞背景适配
            NormalizeTitleToCenterAnchor();

            var bg = FindChildDeep(transform, "Bg_Full") as RectTransform;
            if (bg == null) return;
            var parent = bg.parent as RectTransform;
            if (parent == null) return;
            if (parent.rect.width <= 0f || parent.rect.height <= 0f) return;   // 尺寸还没量好，下次 Open 再算

            // ① 首次记录基准：原始 sizeDelta（之后等比缩放都从它重算）
            if (!_fitCaptured)
            {
                _fitBaseSize = bg.sizeDelta;
                if (_fitBaseSize.x <= 0f || _fitBaseSize.y <= 0f)
                    _fitBaseSize = new Vector2(DAILY_LOGIN_BG_BASE_W, DAILY_LOGIN_BG_BASE_H);
                _fitCaptured = true;
            }

            // ② 中心锚点 + 严格居中：cover 放大后四边无缝，不再保留任何随分辨率漂移的中心偏移
            bg.anchorMin = new Vector2(0.5f, 0.5f);
            bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.pivot = new Vector2(0.5f, 0.5f);
            bg.anchoredPosition = Vector2.zero;

            // ③ 整体等比放大到盖住整屏（只放大、不缩小）
            float k = 1f;
            if (EnableDailyLoginFit && IsThinnerScreen())
            {
                // 以「实际渲染尺寸」为基准（把预制体自带的 localScale 算进去），否则会和美术缩放叠加放过头
                float renderedW = Mathf.Max(1f, _fitBaseSize.x * bg.localScale.x);
                float renderedH = Mathf.Max(1f, _fitBaseSize.y * bg.localScale.y);
                float screenW = GameConfig.DESIGN_WIDTH;
                float screenH = ResolveLogicalScreenHeight();
                k = Mathf.Max(1f, Mathf.Max(screenW / renderedW, screenH / renderedH));
            }
            bg.sizeDelta = new Vector2(_fitBaseSize.x * k, _fitBaseSize.y * k);
        }
        finally
        {
            _fitApplying = false;
        }
    }

    /// <summary>
    /// 「每日登录」标头锚点归一：预制体顶中锚 (0.5,1) → 中心锚 (0.5,0.5)，
    /// 视觉位置按设计稿 720×1280 换算——9:16 标准屏落点与预制体分毫不差，
    /// 瘦屏上则与 CellsPanel / InfoBar 同围绕屏幕中心站位，不再上飘。幂等：非顶中锚直接返回。
    /// </summary>
    void NormalizeTitleToCenterAnchor()
    {
        var title = FindChildDeep(transform, "Title") as RectTransform;
        if (title == null) return;
        if (Mathf.Abs(title.anchorMax.y - 1f) > 0.001f) return;   // 已改过（或本来就不是顶中锚），绝不二次挪动

        // 顶中锚是挂「设计稿顶边」的点锚：先还原它在设计稿坐标系里的 pivot 落点与矩形中心
        Vector2 pivotPos = new Vector2(
            title.anchorMin.x * GameConfig.DESIGN_WIDTH + title.anchoredPosition.x,
            title.anchorMax.y * GameConfig.DESIGN_HEIGHT + title.anchoredPosition.y);
        Vector2 center = pivotPos + (new Vector2(0.5f, 0.5f) - title.pivot) * title.rect.size;

        title.anchorMin = new Vector2(0.5f, 0.5f);
        title.anchorMax = new Vector2(0.5f, 0.5f);
        title.pivot = new Vector2(0.5f, 0.5f);
        title.anchoredPosition = center - new Vector2(GameConfig.DESIGN_WIDTH * 0.5f, GameConfig.DESIGN_HEIGHT * 0.5f);
    }

    /// <summary>屏幕逻辑尺寸（与项目一致口径）：宽恒为 DESIGN_WIDTH，高按 IsThinnerScreen 同算法解析。</summary>
    static float ResolveLogicalScreenHeight()
    {
        float match = DesignAspectLetterbox.ResolveUiMatch();
        if (match < 0.5f)
            return GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height);
        return GameConfig.DESIGN_HEIGHT;
    }
}
