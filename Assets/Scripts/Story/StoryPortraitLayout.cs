/// <summary>
/// 剧情对话立绘裁切与屏幕占比规则。
/// 图源：全身、统一画布尺寸（会长 / 咨询台 / 玩家等）。
/// 单/双人共用同一尺寸、裁切与纵向位置；双人仅水平靠左/右。
/// </summary>
public static class StoryPortraitLayout
{
    public enum Mode
    {
        /// <summary>引导、会长/咨询台独白：立绘屏幕正中。</summary>
        SoloCentered,
        /// <summary>双人对话：左发起方、右对方，面对面。</summary>
        DualSplit
    }

    public struct Profile
    {
        /// <summary>保留 Sprite 上方高度比例；0.6 = 仅显示上 3/5（裁掉下方约 2/5）。</summary>
        public float clipHeightFrac;
        /// <summary>立绘最大高度占画布比例（显示缩放，与裁切无关）。</summary>
        public float screenHeightFrac;
        /// <summary>立绘最大宽度占画布比例。</summary>
        public float screenWidthFrac;
        /// <summary>底边 Y 占画布高度；&gt;0 时覆盖默认纵向位置（仅位移，不缩小）。</summary>
        public float bottomScreenFrac;
        /// <summary>底边额外像素偏移；负值 = 往下。</summary>
        public float bottomOffsetPx;
        /// <summary>单人/双人立绘底边与对话框上沿的间距（像素）。</summary>
        public float dialogueTopGapPx;
    }

    /// <summary>引导期主要 NPC（全身立绘，单人时居中）。</summary>
    public static readonly string[] TutorialCastIds =
    {
        StoryPortraits.GuildMaster,
        StoryPortraits.Receptionist,
        StoryPortraits.Player,
        StoryPortraits.LaoDun,
    };

    /// <summary>剧情立绘标准（1171×1345 全身画布；宽图先吃满高度，再放宽宽度上限）。</summary>
    public static Profile Standard => new Profile
    {
        clipHeightFrac = 0.72f,
        screenHeightFrac = 0.92f,
        screenWidthFrac = 1.45f,
        bottomScreenFrac = 0.17f,
        bottomOffsetPx = -88f,
        dialogueTopGapPx = 2f
    };

    /// <summary>双人分屏：与 Standard 同尺寸，仅水平锚点分左右。</summary>
    public static Profile Dual => new Profile
    {
        clipHeightFrac = 0.72f,
        screenHeightFrac = 0.92f,
        screenWidthFrac = 1.45f,
        bottomScreenFrac = 0.17f,
        bottomOffsetPx = -88f,
        dialogueTopGapPx = 2f
    };

    public static Profile GetProfile(Mode mode) =>
        mode == Mode.DualSplit ? Dual : Standard;

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}

