using System.Collections.Generic;

/// <summary>
/// 剧情对话立绘：原尺寸 × 屏幕系数（720×1280 基准，只放大不缩小）+ 上方高度比例裁切；单/双仅水平槽位不同。
/// 2026-09-28 主人推翻旧规「禁止按屏幕比例缩放」：大分辨率机型立绘等比放大，标准屏效果不变。
/// </summary>
public static class StoryPortraitLayout
{
    public enum Mode
    {
        SoloCentered,
        DualSplit
    }

    public struct Profile
    {
        /// <summary>
        /// 底部锚定布局的调参基准：0.72 时代审定效果时的可见高度比例。
        /// 实际裁切窗走 <see cref="StoryPortraitLayout.GetClipHeightFrac"/>（坐姿=1 整图，其余=0.72 原位）；
        /// 仅坐姿（窗=整图）由 Presenter 按差值下移底边、保住审定过的可见顶边（见 StoryPortraitPresenter.ResolveBottomY）。
        /// </summary>
        public float clipHeightFrac;
        /// <summary>已废弃：立绘不再按屏幕比例缩放，保留字段避免旧引用报错。</summary>
        public float screenHeightFrac;
        /// <summary>已废弃：裁切宽=原图宽。</summary>
        public float slotClipWidthFrac;
        /// <summary>已废弃：裁切宽=原图宽。</summary>
        public float centerClipWidthFrac;
        public float bottomScreenFrac;
        public float bottomOffsetPx;
        public float dialogueTopGapPx;
    }

    /// <summary>引导期主要 NPC（全身立绘）。</summary>
    public static readonly string[] TutorialCastIds =
    {
        StoryPortraits.GuildMaster,
        StoryPortraits.Receptionist,
        StoryPortraits.Player,
        StoryPortraits.LaoDun,
    };

    /// <summary>剧情立绘标准：原图像素 + 上方裁切，不缩放。</summary>
    public static Profile Unified => new Profile
    {
        clipHeightFrac = 0.72f,
        screenHeightFrac = 0.92f,
        slotClipWidthFrac = 0.52f,
        centerClipWidthFrac = 0.92f,
        bottomScreenFrac = 0.17f,
        bottomOffsetPx = -88f,
        dialogueTopGapPx = 2f
    };

    /// <summary>
    /// 坐姿桌面场景立绘专属附加位移（设计画布像素；负 = 更靠下，正 = 往上回抬）。
    /// 2026-10-15 主人拍板：会长/前台小姐换坐姿图后，整张图底边要贴在对话框顶
    /// （图整体在对话框上方，不内藏进框）；其他角色不在此表（偏移 0，照旧）。
    /// 微调位置只改这一个字典的数值。现值 +100 = 抵消标准公式自带的 -88 内藏与 +2 间隙后，
    /// 底边落在对话框顶上方约 14px（留小缝），框变高时自动跟随。
    /// 2026-10-15 补记：裁切窗开到整图后，Presenter 已按 (1-0.72)×图高把真底边下移（「遮罩往下还原」），
    /// 新露出的坐姿下半身垂到对话框后面由框遮挡；本表 +100 仍按 0.72 时代审定过的「可见底边」口径调参。
    /// </summary>
    public static readonly Dictionary<string, float> DeskPortraitBottomOffsetPx = new Dictionary<string, float>
    {
        { StoryPortraits.GuildMaster, 100f },
        { StoryPortraits.Receptionist, 100f },
    };

    /// <summary>查某个立绘 ID 的专属下移量；不在表内返回 0（其他角色照旧）。</summary>
    public static float GetBottomOffsetPx(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return 0f;
        return DeskPortraitBottomOffsetPx.TryGetValue(portraitId, out var off) ? off : 0f;
    }

    /// <summary>
    /// 引导局坐姿立绘额外上抬（2026-10-15 主人拍板：只有引导时会长/前台小姐的 RightPortrait
    /// 往上移 80，其他人物剧情照旧）。门槛＝引导档案位未完成（StoryProgress.TutorialDone=false）
    /// 且 id 在坐姿名单 DeskPortraitBottomOffsetPx 内；引导结束后（含主线会长对话）一律 0。
    /// RightPortrait 节点在「单人居中」（Slot.Center 复用右槽图）与「双人右槽」（Slot.Right）两种摆法都会命中。
    /// </summary>
    public const float TutorialDeskLiftPx = 80f;

    public static float GetTutorialDeskLiftPx(string portraitId)
    {
        if (StoryProgress.TutorialDone) return 0f;
        if (string.IsNullOrEmpty(portraitId)) return 0f;
        return DeskPortraitBottomOffsetPx.ContainsKey(portraitId) ? TutorialDeskLiftPx : 0f;
    }

    /// <summary>
    /// 立绘裁切比例（2026-10-15 主人立规矩：遮罩永远不要动，动的只是立绘的上下）：
    /// 坐姿桌面立绘（DeskPortraitBottomOffsetPx 名单内）=1 整图，桌面场景全露、底边由对话框遮挡；
    /// 其余（站立全身）=0.72 审定原位遮罩——切线一直藏在对话框后面，脚从来是遮罩在框后裁掉的。
    /// 10-15 主人报「脚露出来」即窗被开到整图所致，回 0.72 原位即愈；禁止再动遮罩位置/尺寸，
    /// 调立绘位置只动 DeskPortraitBottomOffsetPx / TutorialDeskLiftPx 这类立绘偏移旋钮。
    /// </summary>
    public static float GetClipHeightFrac(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return 0.72f;
        return DeskPortraitBottomOffsetPx.ContainsKey(portraitId) ? 1f : 0.72f;
    }

    /// <summary>兼容旧引用。</summary>
    public static Profile Standard => Unified;

    /// <summary>兼容旧引用。</summary>
    public static Profile Dual => Unified;

    public static Profile GetProfile(Mode mode) => Unified;

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}

