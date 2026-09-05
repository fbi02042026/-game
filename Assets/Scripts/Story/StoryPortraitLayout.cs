/// <summary>
/// 剧情对话立绘：原尺寸 + 上方高度比例裁切；单/双仅水平槽位不同。禁止按屏幕比例缩放。
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
        /// <summary>保留 Sprite 上方高度比例（原尺寸上裁切，不缩放图）。</summary>
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

    /// <summary>兼容旧引用。</summary>
    public static Profile Standard => Unified;

    /// <summary>兼容旧引用。</summary>
    public static Profile Dual => Unified;

    public static Profile GetProfile(Mode mode) => Unified;

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}

