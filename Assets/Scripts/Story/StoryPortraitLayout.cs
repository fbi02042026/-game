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

    /// <summary>
    /// 情报对话立绘归一化：角色「内容包围盒」相对整图的比例（PNG 像素实测，非估算）。
    /// 立绘显示尺寸原本 = 整张 sprite 像素 × 屏幕系数，但各图透明留白量差很多 ——
    /// 留白多的图角色实际就看着偏小、偏下。这就是「人物大小不一致 / 玩家偏小偏下」的根因。
    /// 实测：
    ///   独眼 C004   687×1035，角色 y[25..993]  → 角色高 968，脚下留白 42
    ///   玩家 player 1171×1345，角色 y[213..1277] → 角色高 1064，脚下留白 68，头顶留白 213
    ///   酒吧老板娘  440×1143，整张不透明（无留白，角色即全图）
    /// 2026-10-15 主人报「人物现在大小不一致」「玩家的形象偏小偏下」。
    /// </summary>
    public struct PortraitContentFit
    {
        /// <summary>角色内容高 / 整图高。</summary>
        public float charHeightFrac;
        /// <summary>角色脚底以下的留白 / 整图高（用于脚底对齐）。</summary>
        public float belowFrac;
    }

    /// <summary>归一化后角色统一像素高（设计基准 1280 屏）。取独眼实测 968 —— 情报商原尺寸不动，其余向他看齐。</summary>
    public const float PortraitTargetCharHeightPx = 968f;

    /// <summary>脚底对齐基准：角色脚底距图底的统一像素 = 独眼实测 42px。</summary>
    public const float PortraitFootBaselinePx = 42f;

    /// <summary>
    /// 瘦屏立绘自适应放大（2026-10-19 主人：大分辨率手机立绘偏下，放大+上移试试水）。
    /// 只对逻辑高 &gt; 1280 的瘦屏生效（matchWidthOrHeight=0 按宽匹配后逻辑高被拉长）：
    /// 实际 scale = k × (1 + (k-1) × 此值)，k = 逻辑高/1280。
    /// 0.5 → 20:9 屏（k=1.25）额外放大 12.5%；标准 9:16 屏 k=1 恒乘 1，效果与现在完全一致。
    /// 调参只改这一个数。
    /// </summary>
    public const float TallScreenScaleBoost = 0.5f;

    /// <summary>
    /// 瘦屏立绘自适应上移（设计 1280 基准像素）：bottomY += (k-1) × 此值。
    /// 440 → 20:9 屏（k=1.25）上移 110px。标准屏 k=1 恒 0。调参只改这一个数。
    /// </summary>
    public const float TallScreenLiftPx = 440f;

    /// <summary>
    /// 瘦屏顶部封顶线放宽上限（防裁头，2026-10-19）：立绘顶边最多出屏 24px。
    /// 瘦屏上立绘顶边基本都贴着封顶线，「再往上移」实际由封顶线放宽量决定；
    /// 但独眼这类头顶透明留白小的图（原始留白约 25px）放宽过大必裁发梢，故封顶。
    /// 48 → 顶边最多到 canvasH - 24 + 48，即出屏 24px &lt; 独眼放大后留白约 35px，头发不裁伤。
    /// </summary>
    public const float TallScreenTopMaxLiftPx = 48f;

    public static readonly Dictionary<string, PortraitContentFit> PortraitContentFits = new Dictionary<string, PortraitContentFit>
    {
        { "npc_duyan", new PortraitContentFit { charHeightFrac = 968f / 1035f, belowFrac = 42f / 1035f } },
        { "landlady", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },
        { "player", new PortraitContentFit { charHeightFrac = 1064f / 1345f, belowFrac = 68f / 1345f } },
    };

    /// <summary>查某个立绘 ID 的内容包围盒比例；不在表内返回 false（该立绘不做归一化）。</summary>
    public static bool TryGetContentFit(string portraitId, out PortraitContentFit fit)
    {
        fit = default;
        if (string.IsNullOrEmpty(portraitId)) return false;
        return PortraitContentFits.TryGetValue(portraitId, out fit);
    }

    /// <summary>兼容旧引用。</summary>
    public static Profile Standard => Unified;

    /// <summary>兼容旧引用。</summary>
    public static Profile Dual => Unified;

    public static Profile GetProfile(Mode mode) => Unified;

    public static Mode ModeFromSoloFlag(bool soloCentered) =>
        soloCentered ? Mode.SoloCentered : Mode.DualSplit;
}

