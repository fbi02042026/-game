/// <summary>
/// 战斗模式标识。<b>模式身份的唯一真源</b>——不得再用中文标签解析（如"活动"）
/// 或魔数（如 mode == 4）来反推这是哪种玩法。
///
/// <para>为什么要有这个枚举：</para>
/// 目前「金币本」是靠 <c>IsGoldDungeon</c> 一个 bool 表达的，而这个 bool 散落在
/// BattleManager（建关 / 关卡任务 / 结算 / 通关流程）、ChapterManager、Monster、
/// BattleStateSaver 共 8 处。以后每加一种玩法（无限关卡 / 世界boss / PVP），
/// 就要在同样这 8 处各加一个分支和又一个 bool —— 这正是「改一处牵连另一处出错」的来源。
///
/// <para>以后加玩法只需三步（核心一行不动）：</para>
/// 1. 本枚举加一个成员；
/// 2. 新建一个 <see cref="BattleMode"/> 子类，把它这一套差异写在自己类里；
/// 3. 在 <see cref="BattleModes"/> 注册表挂上去。
/// </summary>
public enum BattleModeId
{
    /// <summary>占位：该入口尚未实现玩法（UI 上显示"即将开放"）。</summary>
    None = 0,

    /// <summary>主线冒险：正常章节推进，掉装备。</summary>
    Normal,

    /// <summary>金币副本：单场战斗，只掉金币，不推进主线。</summary>
    GoldDungeon,

    /// <summary>
    /// 新手引导：不走 UI 入口，由 <c>StoryProgress.ShouldStartTutorialBattle()</c> 触发。
    /// 特例旗标集中在 <see cref="TutorialRules.Tutorial"/>。
    /// </summary>
    Tutorial,

    /// <summary>
    /// 无限关卡（冒险界面按钮「迷宫探索」，下标 2）。
    /// 2026-09-27 主人定位：这条路就是无限关卡。玩法还没做，先立位置、锁住入口。
    /// </summary>
    Endless,

    /// <summary>
    /// 世界 BOSS（冒险界面按钮「BOSS挑战」，下标 3）。
    /// 2026-09-27 主人定位：这条路就是世界 boss。玩法还没做，先立位置、锁住入口。
    /// </summary>
    WorldBoss
}
