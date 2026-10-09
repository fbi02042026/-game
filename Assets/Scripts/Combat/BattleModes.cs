using UnityEngine;

/// <summary>
/// 战斗模式基类：给出「普通玩法」的默认答案，子类只覆写自己不一样的那一项。
/// 这样新增玩法时，差异面被强制收窄到几个覆写点，不会散到核心里去。
/// </summary>
public abstract class BattleMode : IBattleMode
{
    public abstract BattleModeId Id { get; }

    public virtual string DisplayName => "冒险";

    public virtual bool IsPlayable => true;

    /// <summary>默认走正式关规则包；引导覆写为 <see cref="TutorialRules.Tutorial"/>。</summary>
    public virtual TutorialRules Rules => TutorialRules.Formal;

    public virtual bool DropsEquipment => true;

    /// <summary>默认不送免费抽 —— 主线靠「每关产出」走正常取舍，不该有无偿次数。</summary>
    public virtual int FreeEntryDraws => 0;

    /// <summary>默认通关发局内抽奖币（主线：每关 +<c>COIN_PER_*</c>）。</summary>
    public virtual bool GrantsRunCoins => true;

    public virtual void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartChapter(chapter);
    }

    /// <summary>
    /// 默认走主线结算（boss 弹通关选择 / 普通进下一关）。
    /// 需要别的结算的模式覆写本方法即可，核心不用改。
    /// </summary>
    public virtual void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] SettleStageClear 失败：BattleManager 为空");
            return;
        }
        bm.SettleNormalStageClear();
    }
}

/// <summary>主线冒险：正常章节推进，通关只发奖励道具（不掉装备）。</summary>
public sealed class NormalBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.Normal;
    public override string DisplayName => "主线冒险";

    /// <summary>
    /// 2026-10-09 主人拍板：关卡不再掉装备，通关只发奖励道具（金币 / 材料 / 抽奖币之类）；
    /// 装备的唯一产出来源改为抽奖（<c>DraftPool</c>）。
    /// 以后要开「武器副本」这类活动，就在那个模式里覆写 <c>DropsEquipment => true</c>，核心逻辑不用动。
    /// </summary>
    public override bool DropsEquipment => false;
}

/// <summary>
/// 金币副本：单场战斗，只掉金币，不推进主线，通关后清装回城。
/// 对应原来的 <c>IsGoldDungeon = true</c> 那一整套分支。
/// </summary>
public sealed class GoldDungeonBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.GoldDungeon;
    public override string DisplayName => "金币副本";

    /// <summary>
    /// 当前仍是「未开放」：冒险界面里只有主线可玩，金币本入口被锁着。
    /// 这里照抄现状，保持行为不变；要开放时把这里改 true 即可，其他代码不用动。
    /// </summary>
    public override bool IsPlayable => false;

    public override bool DropsEquipment => false;

    /// <summary>
    /// 金币本开局免费抽 <see cref="SlotMachineDefs.GOLD_DUNGEON_FREE_DRAWS"/> 次（主人暂定 5）。
    /// 想调次数只改那一个常量，这里不动。
    /// </summary>
    public override int FreeEntryDraws => SlotMachineDefs.GOLD_DUNGEON_FREE_DRAWS;

    /// <summary>金币本通关不发局内抽奖币 —— 打完就结算回城、币随局清零，发了等于白发。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 金币本 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartGoldDungeon(chapter);
    }

    /// <summary>
    /// 金币本结算：弹胜利结算面板，确认后清空本局所得并回城（不推进主线）。
    /// 以后「佣兵副本 / 武器副本 / 技能副本」照这个样子各写一个类：
    /// 发什么 + 之后去哪，都写在自己类里，核心一行不动。
    /// </summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 金币本 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        bm.ShowVictorySettlementThen(bm.EndRunAndReturnToTown);
    }
}

/// <summary>
/// 新手引导：不走 UI 入口，由剧情触发（<c>StoryProgress.ShouldStartTutorialBattle()</c>）。
/// 它那二十多个特例旗标已集中在 <see cref="TutorialRules.Tutorial"/>，本类只负责把它们挂到模式上。
/// 建关仍走 <c>StartChapter</c>（与现状一致：引导局 IsGoldDungeon 恒为 false）。
/// </summary>
public sealed class TutorialBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.Tutorial;
    public override string DisplayName => "新手引导";
    public override TutorialRules Rules => TutorialRules.Tutorial;
}

/// <summary>
/// 无限关卡（UI「迷宫探索」）。主人已定位这条路是无限关卡，玩法待实现。
/// 现在只做一件事：占住位置 + 锁住入口，不让它漏进战斗核心。
/// 实现时改这里：IsPlayable → true，并覆写波次来源 / 结算。
/// </summary>
public sealed class EndlessBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.Endless;
    public override string DisplayName => "迷宫探索";
    public override bool IsPlayable => false;
}

/// <summary>
/// 世界 BOSS（UI「BOSS挑战」）。主人已定位这条路是世界 boss，玩法待实现。
/// 与无限关卡同理：先占位置、锁入口。
/// </summary>
public sealed class WorldBossBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.WorldBoss;
    public override string DisplayName => "BOSS挑战";
    public override bool IsPlayable => false;
}

/// <summary>
/// 占位模式：UI 上有按钮但还没定位成哪种玩法（目前是「每日副本」）。
/// 明确 <see cref="IsPlayable"/> = false，在入口处就拦下，不会漏进战斗核心。
/// </summary>
public sealed class PlaceholderBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.None;
    public override string DisplayName => "未开放";
    public override bool IsPlayable => false;
}

/// <summary>
/// 模式注册表。<b>加新玩法 = 这里加一行 + 新建一个 BattleMode 子类</b>，核心不动。
/// </summary>
public static class BattleModes
{
    public static readonly NormalBattleMode Normal = new NormalBattleMode();
    public static readonly GoldDungeonBattleMode GoldDungeon = new GoldDungeonBattleMode();
    public static readonly TutorialBattleMode Tutorial = new TutorialBattleMode();
    public static readonly EndlessBattleMode Endless = new EndlessBattleMode();
    public static readonly WorldBossBattleMode WorldBoss = new WorldBossBattleMode();
    public static readonly PlaceholderBattleMode Placeholder = new PlaceholderBattleMode();

    /// <summary>
    /// 冒险界面模式按钮下标 → 模式。<b>唯一真源</b>。
    /// 取代 AdventureUI 里「标签含'活动' → 金币本」的字符串解析和 <c>mode == 4</c> 魔数。
    /// 下标取自 <c>AdventureUI.ModeNames = { 主线冒险, 每日副本, 迷宫探索, BOSS挑战, 活动副本 }</c>。
    /// 下标 2/3 主人已定位为无限关卡 / 世界BOSS（玩法待实现，入口锁着）；下标 1「每日副本」还没定位，先占位。
    /// 注意：ModeNames 只管<b>按钮上显示什么字</b>，不再参与身份判断——改文案不会再改出 bug。
    /// </summary>
    static readonly BattleModeId[] SlotTable =
    {
        BattleModeId.Normal,       // 0 主线冒险
        BattleModeId.None,         // 1 每日副本 —— 还没定位成哪种玩法，先占位
        BattleModeId.Endless,      // 2 迷宫探索 = 无限关卡（玩法待实现）
        BattleModeId.WorldBoss,    // 3 BOSS挑战  = 世界 BOSS（玩法待实现）
        BattleModeId.GoldDungeon   // 4 活动副本 = 金币本
    };

    /// <summary>按模式标识取实例。认不出来时<b>报错并返回占位模式</b>（进不去战斗），
    /// 绝不悄悄当成主线跑——静默兜底正是 bug 复发的根源。</summary>
    public static IBattleMode Get(BattleModeId id)
    {
        switch (id)
        {
            case BattleModeId.Normal: return Normal;
            case BattleModeId.GoldDungeon: return GoldDungeon;
            case BattleModeId.Tutorial: return Tutorial;
            case BattleModeId.Endless: return Endless;
            case BattleModeId.WorldBoss: return WorldBoss;
            case BattleModeId.None: return Placeholder;
            default:
                Debug.LogError($"[BattleModes] 未注册的模式 {id}，已回退为占位模式（不可进入）");
                return Placeholder;
        }
    }

    /// <summary>按冒险界面按钮下标取模式。越界同样回占位模式。</summary>
    public static IBattleMode FromSlot(int uiIndex)
    {
        if (uiIndex < 0 || uiIndex >= SlotTable.Length)
        {
            Debug.LogError($"[BattleModes] 模式下标越界 {uiIndex}，已回退为占位模式（不可进入）");
            return Placeholder;
        }
        return Get(SlotTable[uiIndex]);
    }
}
