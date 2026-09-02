/// <summary>
/// 剧情对话立绘裁切与屏幕占比规则。
/// 单/双人共用 Unified：同高同裁切；仅水平槽位 Left/Right/Center 不同。
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
        /// <summary>保留 Sprite 上方高度比例。</summary>
        public float clipHeightFrac;
        /// <summary>立绘显示高度占画布比例（单/双统一）。</summary>
        public float screenHeightFrac;
        /// <summary>双人左右槽位水平裁切宽度占画布比例。</summary>
        public float slotClipWidthFrac;
        /// <summary>单人居中槽位水平裁切宽度占画布比例。</summary>
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

    /// <summary>剧情立绘标准（1171×1345 全身画布）。</summary>
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

    /// <summary>兼容旧引用。</summary>
    public static Profile Standard => Unified;

    /// <summary>兼容旧引用。</summary>
    public static Profile Dual => Unified;

    public static Profile GetProfile(Mode mode) => Unified;

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}
