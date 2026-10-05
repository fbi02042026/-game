/// <summary>
/// 战斗模式插槽。核心循环（BattleManager）只认这个接口，不再认识具体玩法。
///
/// <para>设计边界：<b>核心不动，玩法可插拔。</b></para>
/// 核心负责：单位、波次推进、伤害、胜负判定的公共部分。
/// 模式负责：这一局「打什么关、有什么特例、掉不掉装备、怎么结算」。
/// 模式之间互不知晓，加一个新玩法不会碰到别的玩法。
///
/// <para>为什么用普通 C# 类而不是 ScriptableObject：</para>
/// 模式差异是<b>行为</b>（调哪个建关方法、走哪套规则包），不是可调数值；
/// 做成 SO 反而要新增 .asset 资源、多一层引用，且本项目规则禁止手改 .asset。
/// 等真出现「策划要在 Inspector 里配模式参数」时，再让模式持有 SO 配置即可。
/// </summary>
public interface IBattleMode
{
    /// <summary>模式标识。</summary>
    BattleModeId Id { get; }

    /// <summary>模式展示名（唯一真源；不再从按钮上的中文文本反推身份）。</summary>
    string DisplayName { get; }

    /// <summary>是否已开放。未开放的模式在<b>入口处</b>拦下，不进战斗。</summary>
    bool IsPlayable { get; }

    /// <summary>本模式绑定的规则包（引导的那一堆特例旗标都在这里）。</summary>
    TutorialRules Rules { get; }

    /// <summary>本模式是否掉落装备（金币本 = false）。</summary>
    bool DropsEquipment { get; }

    /// <summary>
    /// 本模式开局送几次**免费**抽奖（不扣抽奖币）。0 = 没有。
    /// <para>2026-10-05 主人拍板：金币本「开始的时候可以先抽几次，不用耗金币，暂定 5 次」。
    /// 金币本是**单关**玩法，没有「这关攒着下关抽定向」的节奏，局内币经济在它身上不成立 ——
    /// 所以直接给 N 次免费抽，让玩家一进门就把构筑搭起来，再拿这套构筑去打。</para>
    /// ⚠ 次数写在模式上（<c>SlotMachineDefs.GOLD_DUNGEON_FREE_DRAWS</c>），核心只问这个数。
    /// </summary>
    int FreeEntryDraws { get; }

    /// <summary>
    /// 本模式通关要不要发**局内抽奖币**（<c>SlotCoin</c>）。
    /// <para>金币本 = false：它打完就直接结算回城、币随局清零（<c>SlotMachineSystem.ClearRunCoins</c>），
    /// 通关再发一笔马上就被清掉的钱，只会让玩家看到一条「+100」然后归零的假收益。</para>
    /// </summary>
    bool GrantsRunCoins { get; }

    /// <summary>
    /// 建关：决定这一局打哪些关卡。
    /// 核心只调这一个方法，不再写 <c>if (IsGoldDungeon) ... else ...</c>。
    /// </summary>
    void BuildChapter(ChapterManager cm, int chapter);

    /// <summary>
    /// 通关结算：发什么、之后去哪。<b>这是行为，不是开关。</b>
    ///
    /// <para>为什么这里不能用 bool：</para>
    /// 活动副本以后会有佣兵副本（发碎片）、武器副本（发武器）、技能副本（发技能道具），
    /// 差异是「做什么」。用 <c>EndsRunAfterClear</c> 这类开关，每加一种副本就要在核心
    /// 多一个分支——那不过是把 <c>IsGoldDungeon</c> 换个地方住，病没治。
    /// 用方法的话：加一种副本 = 加一个类，核心一行不动。
    /// </summary>
    void SettleStageClear(BattleManager bm);
}
