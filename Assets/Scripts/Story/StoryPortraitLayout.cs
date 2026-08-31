/// <summary>
/// 剧情对话立绘裁切与屏幕占比规则。
/// 图源：全身、统一画布尺寸（会长 / 咨询台 / 玩家等）。
/// - 引导 / 单人（StoryDirector.Solo）：居中，全身展示
/// - 双人对话（StoryDirector.Line）：左右分屏，统一上方裁切
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
        /// <summary>保留 Sprite 高度比例；1 = 不裁切（全身）。</summary>
        public float clipHeightFrac;
        /// <summary>立绘最大高度占画布比例。</summary>
        public float screenHeightFrac;
        /// <summary>立绘最大宽度占画布比例。</summary>
        public float screenWidthFrac;
    }

    /// <summary>引导期主要 NPC（全身立绘，单人时居中）。</summary>
    public static readonly string[] TutorialCastIds =
    {
        StoryPortraits.GuildMaster,
        StoryPortraits.Receptionist,
        StoryPortraits.Player,
    };

    public static Profile GetProfile(Mode mode)
    {
        switch (mode)
        {
            case Mode.SoloCentered:
                return new Profile
                {
                    clipHeightFrac = 1f,
                    screenHeightFrac = 0.74f,
                    screenWidthFrac = 0.92f
                };
            case Mode.DualSplit:
            default:
                return new Profile
                {
                    clipHeightFrac = 0.72f,
                    screenHeightFrac = 0.46f,
                    screenWidthFrac = 0.46f
                };
        }
    }

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}
