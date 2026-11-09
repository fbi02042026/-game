using System.Collections.Generic;
using UnityEngine;

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
        StoryPortraits.GuildMasterSitting,
        StoryPortraits.ReceptionistSitting,
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
        bottomOffsetPx = -48f,   // 【2026-11-04 主人报「全身立绘剧情时人物偏下」】-88→-48，全体立绘基准上移 40 设计像素；坐姿 +100 修正表未动，坐姿净位置同步上移 40
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
        { StoryPortraits.GuildMasterSitting, 100f },
        { StoryPortraits.ReceptionistSitting, 100f },
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
    /// 【2026-11-07 主人当场「引导时会长和前台小姐的坐姿立绘往下20」】上抬 80 → 60，
    /// 即引导期这两张坐姿立绘净下移 20 设计像素；其他人物、引导结束后的剧情一律不受影响。
    /// </summary>
    public const float TutorialDeskLiftPx = 60f;

    public static float GetTutorialDeskLiftPx(string portraitId)
    {
        if (StoryProgress.TutorialDone) return 0f;
        if (string.IsNullOrEmpty(portraitId)) return 0f;
        return DeskPortraitBottomOffsetPx.ContainsKey(portraitId) ? TutorialDeskLiftPx : 0f;
    }

    /// <summary>
    /// 【仅剧情对话立绘（DialogueUI → StoryPortraitPresenter 链路）适用；别处遮罩不受本条约束、照旧不要动】
    /// 立绘「扩窗前基准窗高」比例：坐姿（DeskPortraitBottomOffsetPx 名单内）=1 整图，其余（站立全身）=0.72。
    /// 0.72 是 2026-10-15 主人审定的可见高度基准，仍决定图片顶边初始位置、ResolveBottomY 的顶部封顶
    /// 和坐姿下移补偿（Presenter 里几处都按这个值算），别随便改。
    /// 【2026-11-09 主人拍板「上和左右都不要有裁边，只有下面有裁边」，只覆盖剧情立绘这块的 10-15 旧规】
    /// StoryPortraitPresenter.ExpandClipEdges 会把裁切窗的上 / 左 / 右三边推到画布外，只留底边一条切线；
    /// 底边 y 由 ResolveBottomY 定（与 11-04 定稿一致），一律不要挪。
    /// 所以这里返回的不再是「实际可见裁切比例」，只是扩窗前的基准窗高。
    /// 调人物上下只动图片自己的 anchoredPosition.y：站立 GetStandingLiftPx(+50)，
    /// 坐姿 DeskPortraitBottomOffsetPx / TutorialDeskLiftPx；不要挪遮罩底边、也不要另开遮罩尺寸旋钮凑位置。
    /// 其它界面（角色看板 / 酒馆 / 主界面等）的遮罩与立绘排版不在此列，一律没动也不要动。
    /// </summary>
    public static float GetClipHeightFrac(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return 0.72f;
        return DeskPortraitBottomOffsetPx.ContainsKey(portraitId) ? 1f : 0.72f;
    }

    /// <summary>
    /// 站立（全身）立绘在剧情里往上挪的量（设计画布像素）。
    /// 2026-11-09 主人「让所有站立的立绘在剧情的时候往上 50」：只加在立绘图片自己的 anchoredPosition.y 上，
    /// 遮罩底边不动；裁切窗的上 / 左 / 右三边由 StoryPortraitPresenter.ExpandClipEdges 推到画布外。
    /// 引导期两张坐姿（DeskPortraitBottomOffsetPx 名单）返回 0，位置保持原位不动。
    /// 判定与 GetClipHeightFrac / GetTutorialDeskLiftPx 同源（同一张坐姿名单）。
    /// </summary>
    public const float StandingPortraitLiftPx = 50f;

    /// <summary>查某个立绘的站立上抬量；坐姿 0，其余（含 id 为空的默认站立）50。</summary>
    public static float GetStandingLiftPx(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return StandingPortraitLiftPx;
        return DeskPortraitBottomOffsetPx.ContainsKey(portraitId) ? 0f : StandingPortraitLiftPx;
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

    /// <summary>
    /// 各立绘「头顶透明留白」实测像素（PNG 原图像素，数据来源同 PortraitContentFits 注释的实测值）。
    /// 2026-10-23 主人拍板：瘦屏立绘再上抬 —— 顶部封顶按各图头顶留白个体化：
    /// 留白大的图（玩家 213px）可以吃满上移量，留白小的图（独眼 25px）维持原封顶防裁发梢。
    /// 不在表内的立绘一律按 <see cref="TallScreenTopMaxLiftPx"/> 兜底；标准屏（k=1）行为不变。
    /// </summary>
    public static readonly Dictionary<string, float> PortraitHeadWhitespacePx = new Dictionary<string, float>
    {
        { "npc_duyan", 25f },
        { "player", 213f },
        { "landlady", 0f },
        // 【2026-11-04】随内容表补齐的头顶留白实测（原始像素）；hunter/guildmaster_sitting/receptionist_sitting top=0 与兜底 48 等效不列
        { "laodun", 147f },
        { "xiaomei", 241f },
        { "npc_xiaomei", 241f },
        { "altor", 147f },
        { "grey", 107f },
        { "npc_grey", 107f },
        { "melissa", 143f },
        { "guildmaster_hidden", 45f },
        { "guildmaster", 74f },       // 【2026-11-04 站姿改造】站姿图 top=74（原 guildmaster_open）
        { "receptionist", 170f },     // 【2026-11-04 站姿改造】站姿图 top=170
    };

    /// <summary>
    /// 瘦屏顶部封顶个体化查询：头顶留白 × 屏幕系数 − 8px 安全边，与 <see cref="TallScreenTopMaxLiftPx"/> 取大者。
    /// scale 必须与 ApplySlot 定尺寸用的 <see cref="StoryPortraitPresenter.ResolvePortraitScale"/> 同源同值。
    /// </summary>
    public static float GetTopMaxLiftPx(string portraitId, float scale)
    {
        if (string.IsNullOrEmpty(portraitId)) return TallScreenTopMaxLiftPx;
        if (!PortraitHeadWhitespacePx.TryGetValue(portraitId, out var headPx)) return TallScreenTopMaxLiftPx;
        return Mathf.Max(TallScreenTopMaxLiftPx, headPx * Mathf.Max(1f, scale) - 8f);
    }

    public static readonly Dictionary<string, PortraitContentFit> PortraitContentFits = new Dictionary<string, PortraitContentFit>
    {
        { "npc_duyan", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },   // 【2026-11-04 校正】C004 现图已裁成整图无留白（旧实测 y[25..993] 过期），与 hunter 同图同值
        { "landlady", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },
        { "player", new PortraitContentFit { charHeightFrac = 1064f / 1345f, belowFrac = 68f / 1345f } },
        // 【2026-11-04 主人拍板「剧情立绘统一 968 + 主线也归一化」】以下按 PIL 实测 alpha 包围盒补齐：
        // laodun=H001、xiaomei=C001、altor=C002、grey=C003、hunter=C004（现图 687×1035 整图无留白）、
        // melissa、guildmaster_hidden 均为 1171×1345 标准图；guildmaster/receptionist 为 2026-11-04 站姿改造后的
        // 站姿图（同 1171×1345 规格），guildmaster_sitting 579×522 / receptionist_sitting 496×456 为引导期坐姿整图
        // 整图即内容（bbox 无留白）——两图此前不在表内且主线不走归一化，是「会长前台剧情偏小且互不一致」的根因。
        { "laodun", new PortraitContentFit { charHeightFrac = 1149f / 1345f, belowFrac = 49f / 1345f } },
        { "xiaomei", new PortraitContentFit { charHeightFrac = 1031f / 1345f, belowFrac = 73f / 1345f } },
        { "npc_xiaomei", new PortraitContentFit { charHeightFrac = 1031f / 1345f, belowFrac = 73f / 1345f } },
        { "altor", new PortraitContentFit { charHeightFrac = 1154f / 1345f, belowFrac = 44f / 1345f } },
        { "grey", new PortraitContentFit { charHeightFrac = 1191f / 1345f, belowFrac = 47f / 1345f } },
        { "npc_grey", new PortraitContentFit { charHeightFrac = 1191f / 1345f, belowFrac = 47f / 1345f } },
        { "hunter", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },
        { "melissa", new PortraitContentFit { charHeightFrac = 1176f / 1345f, belowFrac = 26f / 1345f } },
        { "guildmaster_hidden", new PortraitContentFit { charHeightFrac = 1262f / 1345f, belowFrac = 38f / 1345f } },
        { "guildmaster", new PortraitContentFit { charHeightFrac = 1233f / 1345f, belowFrac = 38f / 1345f } },   // 站姿实测 y[74..1307]（原 guildmaster_open）
        { "receptionist", new PortraitContentFit { charHeightFrac = 1149f / 1345f, belowFrac = 26f / 1345f } },  // 站姿实测 y[170..1319]（Art portrait_receptionist）
        { "guildmaster_sitting", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },   // 579×522 坐姿整图（引导期）
        { "receptionist_sitting", new PortraitContentFit { charHeightFrac = 1f, belowFrac = 0f } },  // 496×456 坐姿整图（引导期）
    };

    /// <summary>
    /// 个别立绘整体额外缩放（乘在原图像素尺寸上）。2026-11-05 主人报「引导的这两个立绘太大」：
    /// 11-04「剧情立绘统一 968」归一化后，引导坐姿两图（charHeightFrac=1 整图）被放大到 968 高，
    /// 引导里显得过大 → 统一缩 20%。只缩显示尺寸，遮罩/位置旋钮（DeskPortraitBottomOffsetPx /
    /// TutorialDeskLiftPx）一律不动。表外立绘 1 = 不缩。
    /// 【2026-11-06 主人再要求小 10%】0.8 × 0.9 = 0.72；遮罩/位置旋钮仍一律不动。
    /// </summary>
    public static readonly Dictionary<string, float> PortraitExtraScale = new Dictionary<string, float>
    {
        { StoryPortraits.GuildMasterSitting, 0.72f },
        { StoryPortraits.ReceptionistSitting, 0.72f },
    };

    /// <summary>查某个立绘的额外缩放；不在表内返回 1（不缩）。</summary>
    public static float GetExtraScale(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return 1f;
        return PortraitExtraScale.TryGetValue(portraitId, out var s) ? s : 1f;
    }

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

