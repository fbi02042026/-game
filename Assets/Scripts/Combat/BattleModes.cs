using System;
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

    /// <summary>
    /// 本模式通关/阵亡是否先播「战斗结果」界面（2026-10-24 主人拍板，口径详见
    /// <see cref="IBattleMode.UsesBattleResultScreen"/>）。默认播；不需要的模式在自己的类里覆写 false。
    /// </summary>
    public virtual bool UsesBattleResultScreen => true;

    /// <summary>
    /// 阵亡要不要弹「复活/返回」选择窗（含看广告复活档）。
    /// 【2026-10-29 主人拍板】与 UsesBattleResultScreen 解耦，默认跟随它（正式关/副本行为不变）；
    /// 无限裂隙覆写 true —— 通关不播结果屏，但死亡要有复活机会。
    /// </summary>
    public virtual bool OffersRevive => UsesBattleResultScreen;

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
    /// 当前已开放（2026-10-23 主人拍板「活动副本一拆二」：冒险界面「活动副本」标签放开，
    /// 右侧活动列表金币/徽记两横条可选）。要再锁回去把这里改 false 即可，其他代码不用动。
    /// </summary>
    public override bool IsPlayable => true;

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
        // 【2026-10-23 主人拍板「打通前一难度才开下一难度」】通关即记录最高难度（死亡不通关不标记）
        ActivityDungeonRules.MarkCleared(bm.BattleDifficulty);
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }
}

/// <summary>
/// 徽记副本（2026-10-23 主人拍板「活动副本一拆二」）：单场战斗，通关只掉随机职业徽记
/// （发放走 <c>MercGrowInventory.GrantBadgeDungeonDrops</c>），不推进主线、不发金币/碎片/道具。
/// 结构照抄 <see cref="GoldDungeonBattleMode"/>；不占冒险界面按钮位（SlotTable 不加位），
/// 入口 = <c>AdventureUI.EnterBadgeDungeon()</c>（本期不接按钮，留给以后的 UI）。
/// </summary>
public sealed class BadgeDungeonBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.BadgeDungeon;
    public override string DisplayName => "徽记副本";

    /// <summary>
    /// 徽记本不经 SlotTable 按钮（SlotTable[4] = 金币本），进本由 AdventureUI 活动列表置
    /// PendingBadgeDungeon 决定，本属性无消费点；保持 false 不代表入口被锁。
    /// </summary>
    public override bool IsPlayable => false;

    public override bool DropsEquipment => false;

    /// <summary>开局免费抽 5 次（2026-10-23 主人拍板「活动副本和无限都是一开始抽五次」，与金币本同框架，进局清零）。</summary>
    public override int FreeEntryDraws => SlotMachineDefs.BADGE_DUNGEON_FREE_DRAWS;

    /// <summary>照抄金币本：打完就结算回城、币随局清零，不发局内抽奖币（🔴 徽记副本不给金币）。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 徽记副本 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartBadgeDungeon(chapter);
    }

    /// <summary>照抄金币本结算：弹胜利结算面板，确认后回城（不推进主线）。</summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 徽记副本 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        // 【2026-10-23 主人拍板】与金币本同款：通关记录最高难度（金币/徽记共用活动标签进度）
        ActivityDungeonRules.MarkCleared(bm.BattleDifficulty);
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }
}

/// <summary>
/// 卷轴副本（2026-10-24 主人拍板「第三章通关开启 · 每周五 · 每次15体力 · 每日2次」）：
/// 单场战斗，通关只掉招募卷轴（发放走 <c>MercGrowInventory.GrantScrollDungeonDrops</c>），
/// 不推进主线、不发金币/碎片/道具/装备。
/// 结构照抄 <see cref="BadgeDungeonBattleMode"/>；不占 SlotTable 位，
/// 入口 = 冒险页活动列表「卷轴」横条（prefab 已摆，Info 文案「招募卷轴每周五开启」）。
/// </summary>
public sealed class ScrollDungeonBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.ScrollDungeon;
    public override string DisplayName => "卷轴副本";

    /// <summary>不经 SlotTable 按钮（无按钮位），进本由 AdventureUI 活动列表选中态置 PendingScrollDungeon 决定；本属性无消费点。</summary>
    public override bool IsPlayable => false;

    public override bool DropsEquipment => false;

    /// <summary>开局免费抽 5 次（与金币/徽记同框架「活动副本开局抽五次」，进局清零）。</summary>
    public override int FreeEntryDraws => SlotMachineDefs.SCROLL_DUNGEON_FREE_DRAWS;

    /// <summary>照抄金币/徽记本：打完就结算回城、币随局清零，不发局内抽奖币（🔴 卷轴副本不给金币）。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 卷轴副本 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartScrollDungeon(chapter);
    }

    /// <summary>照抄金币/徽记本结算：弹胜利结算面板，确认后回城（不推进主线）。</summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 卷轴副本 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        // 与金币/徽记本同款：通关记录最高难度（三本共用活动标签进度）
        ActivityDungeonRules.MarkCleared(bm.BattleDifficulty);
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }
}

/// <summary>
/// 礼物副本（2026-10-26 主人拍板「新加一个掉落礼物的活动副本周六开启」）：
/// 单场战斗，通关只掉好感礼物（发放走 <c>MercGrowInventory.GrantGiftDungeonDrops</c>，难度越高掉越多），
/// 不推进主线、不发金币/碎片/道具/装备。
/// 结构照抄 <see cref="ScrollDungeonBattleMode"/>；不占 SlotTable 位，
/// 入口 = 冒险页活动列表「礼物」横条（代码克隆卷轴条生成，AdventureUI.EnsureGiftRow）。
/// </summary>
public sealed class GiftDungeonBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.GiftDungeon;
    public override string DisplayName => "礼物副本";

    /// <summary>不经 SlotTable 按钮（无按钮位），进本由 AdventureUI 活动列表选中态置 PendingGiftDungeon 决定；本属性无消费点。</summary>
    public override bool IsPlayable => false;

    public override bool DropsEquipment => false;

    /// <summary>开局免费抽 5 次（与金币/徽记/卷轴同框架「活动副本开局抽五次」，进局清零）。</summary>
    public override int FreeEntryDraws => SlotMachineDefs.GIFT_DUNGEON_FREE_DRAWS;

    /// <summary>照抄前三本：打完就结算回城、币随局清零，不发局内抽奖币（🔴 礼物副本不给金币）。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 礼物副本 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartGiftDungeon(chapter);
    }

    /// <summary>照抄前三本结算：弹胜利结算面板，确认后回城（不推进主线）。</summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 礼物副本 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        // 与前三本同款：通关记录最高难度（四本共用活动标签进度）
        ActivityDungeonRules.MarkCleared(bm.BattleDifficulty);
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }
}

/// <summary>
/// 羁绊副本（2026-10-27 主人拍板「照礼物副本框架」）：好感满的佣兵专属单场 Elite 关。
/// 该佣兵强制进玩家队伍（RunDraftDirector.EnsureBondMercInLoadout），难度固定（不开放选择，≈5~7 章强度），
/// 一次性通关后该佣兵全属性永久 ×1.5（Mercenary.ApplyBondClearBonus 自动生效）+ 奖励钻石（见 SettleStageClear）。
/// 结构照抄 <see cref="GiftDungeonBattleMode"/>；不占 SlotTable 位，入口 = 好感界面「羁绊地图」按钮
/// （MercFavorUI.OnBondClicked 置 PendingBondDungeon / PendingBondHireId）。
/// </summary>
public sealed class BondDungeonBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.BondDungeon;
    public override string DisplayName => "羁绊副本";

    /// <summary>不经 SlotTable 按钮（无按钮位），进本由好感界面按钮置 PendingBondDungeon 决定；本属性无消费点。</summary>
    public override bool IsPlayable => false;

    public override bool DropsEquipment => false;

    /// <summary>羁绊副本是专属剧情关，不送局内抽奖币（照礼物本口径）。</summary>
    public override int FreeEntryDraws => 0;

    /// <summary>打完就结算回城、币随局清零，不发局内抽奖币。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 羁绊副本 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartBondDungeon(chapter);
    }

    /// <summary>
    /// 通关结算：首次通关 → 标记羁绊已建立 + 发钻石；随后弹胜利结算回城。
    /// 属性 ×1.5 是永久存档乘区，由 Mercenary.ApplyBondClearBonus 在战斗内自动生效，这里只负责"一次性标记 + 钻石"。
    /// 二次进入（已通关）不应再发奖——但入口按钮已置灰，正常走不到这里。
    /// </summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 羁绊副本 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        string hid = bm.BondMercHireId;
        var data = SaveSystem.Instance?.Data;
        if (!string.IsNullOrEmpty(hid) && data != null && !data.IsMercBondCleared(hid))
        {
            data.MarkMercBondCleared(hid);
            ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, MercFavorDefs.BondClearDiamond, save: true, notify: true);
            Debug.Log($"[BondDungeon] 羁绊通关：{hid} 已标记 + 钻石{MercFavorDefs.BondClearDiamond}");
        }
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }
}

/// <summary>
/// 支线岛（2026-10-23 合并方案「岛壳用 2 岛依次解锁 + 岛内容用单 Boss 特殊关」）：
/// 单场 Boss 战，10 体力/次，产出 = 固定局外金币 + 天赋石（SideIslandDefs 按岛定）
/// + 装备掉落（DropsEquipment=true 走既有宝箱三选一链，Boss 关 bonusStar=2）。
/// 不推进主线、不发关卡任务金/关卡金（各自 gate 见 Settlement/Waves 的 IsSideIsland 分支）。
/// 通关 → MarkSideIslandCleared + EnsureForkRoll 派第二岛（洗牌袋）。
/// 结构照抄 <see cref="BadgeDungeonBattleMode"/>；不占 SlotTable 位，入口 = 冒险页支线岛点击。
/// </summary>
public sealed class SideIslandBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.SideIsland;
    public override string DisplayName => "支线岛";

    /// <summary>不经 SlotTable 按钮（无按钮位），进本由支线岛点击置 PendingSideIsland 决定；本属性无消费点。</summary>
    public override bool IsPlayable => false;

    /// <summary>岛掉装备（与主线同链：Boss 关 +1 件、bonusStar=2，三选一展示）。</summary>
    public override bool DropsEquipment => true;

    /// <summary>单场结算回城、币随局清零 → 不发局内抽奖币（照金币/徽记本口径）；击杀掉币照常（局内构筑用）。</summary>
    public override bool GrantsRunCoins => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 支线岛 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartSideIsland(AdventureUI.PendingSideIsland);
    }

    /// <summary>岛结算：发岛奖励（金币/天赋石/标记通关/派第二岛）→ 胜利结算面板 → 回城。</summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 支线岛 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        SettleIslandRewards(bm);
        // 【2026-10-27 主人拍板】战斗结果已在统一门播过，不再二次弹「战斗结算」——直接回城
        bm.EndRunAndReturnToTown();
    }

    /// <summary>
    /// 岛通关奖励（2026-10-23 主人拍板「10 体力 + 金币/石/装备」）：装备走宝箱链已发，
    /// 这里发固定局外金币 + 天赋石 + 标记通关 + 派第二岛。缺真值（岛定义/存档）→ LogError 不发（铁律 14）。
    /// </summary>
    static void SettleIslandRewards(BattleManager bm)
    {
        var isl = SideIslandDefs.Get(bm.SideIslandId);
        var data = SaveSystem.Instance?.Data;
        if (isl == null || data == null)
        {
            Debug.LogError($"[支线岛] 结算失败：岛定义或存档缺失（islandId={bm.SideIslandId}）——只回城不发奖（缺真值不兜底）");
            return;
        }

        // 固定局外金币：走局内金币落账链（EndRunAndReturnToTown 的 PersistBattleGold 一次性写回城镇钱包，
        // 与金币本通关金/裂隙首通金同一条链，会计口径已验证）
        if (isl.goldReward > 0)
        {
            bm.GrantRunGold(isl.goldReward);
            GlobalToastUI.Show($"攻克{isl.name}！金币 +{isl.goldReward}", true);
        }
        // 天赋石（照裂隙首通石同款入口）
        if (isl.talentReward > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, isl.talentReward, save: false, notify: true);
        }
        // 标记通关（一次性打完锁，跨周目持久）+ 派第二岛（洗牌袋；已派满时内部幂等直接返回）
        data.MarkSideIslandCleared(isl.id);
        ChapterRouteTable.EnsureForkRoll(data);
        SaveSystem.Instance?.Save();
        Debug.Log($"[支线岛] 「{isl.name}」通关结算：金币 +{isl.goldReward} 天赋石 +{isl.talentReward}（装备走宝箱链）");
    }
}

/// <summary>
/// 终章追查关（2026-10-23 主人拍板「最后一关后剧情二选一，选完再出几关进完整剧情」）：
/// 选「追查真相」后解锁的 2 个旁挂剧情关（FinaleDefs），打完接完整揭底演出。
/// 产出照主线普通关（材料/关卡金，act=8 曲线）；不掉装备（DropsEquipment=false）、
/// 不发局内抽奖币（单场结算回城）。不推进主线（ChapterManager.OnStageComplete 有 IsFinale 门）。
/// 结构照抄 <see cref="SideIslandBattleMode"/>；不占 SlotTable 位，
/// 入口 = 冒险页追查入口（AdventureUI 终章分支解锁后出现）。
/// </summary>
public sealed class FinaleBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.Finale;
    public override string DisplayName => "终章追查";

    /// <summary>不经 SlotTable 按钮（无按钮位），进本由追查入口置 PendingFinaleStage 决定；本属性无消费点。</summary>
    public override bool IsPlayable => false;

    /// <summary>剧情关不掉装备（2026-10-23 主人未定掉落 → 先关；要开改这一行）。</summary>
    public override bool DropsEquipment => false;

    /// <summary>单场结算回城、币随局清零 → 不发局内抽奖币（照金币/徽记/支线岛口径）。</summary>
    public override bool GrantsRunCoins => false;

    /// <summary>终章追查关不播战斗结果界面（2026-10-24 架构决策）：通关后紧接着播揭底剧情
    /// （<c>ChapterStoryBeats.PlayFinaleMid / PlayFinaleFinale</c>），插结果界面会打断剧情节奏；主人可点名开启。</summary>
    public override bool UsesBattleResultScreen => false;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 追查关 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        cm.StartFinaleStage(AdventureUI.PendingFinaleStage);
    }

    /// <summary>
    /// 追查关结算：标记通关 → 播关后剧情（第 1 关=揭底中段 / 第 2 关=完整揭底，战斗场景内播）
    /// → 胜利结算面板 → 回城。缺真值（关定义/存档）→ LogError 只回城（铁律 14）。
    /// </summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 追查关 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        var st = FinaleDefs.Get(bm.FinaleStageId);
        var data = SaveSystem.Instance?.Data;
        if (st == null || data == null)
        {
            Debug.LogError($"[终章追查] 结算失败：关定义或存档缺失（stageId={bm.FinaleStageId}）——只回城不标记（缺真值不兜底）");
            bm.ShowVictorySettlementThen(bm.EndRunAndReturnToTown);
            return;
        }

        data.MarkFinaleStageCleared(st.id);
        SaveSystem.Instance?.Save();
        Debug.Log($"[终章追查] 「{st.name}」通关（{data.finaleClearedIds.Count}/{FinaleDefs.Count}）");

        // 关后剧情：战斗场景内播（照主线战后剧情的位置），播完走胜利结算 → 回城
        Action after = () => bm.ShowVictorySettlementThen(bm.EndRunAndReturnToTown);
        if (st.id == 1) ChapterStoryBeats.PlayFinaleMid(after);
        else ChapterStoryBeats.PlayFinaleFinale(after);
    }
}

/// <summary>活动副本种类（2026-10-23 拆二：金币/徽记 + 2026-10-24 加卷轴 + 2026-10-26 加礼物）。四本共用难度进度，各算各的开放日/每日次数。</summary>
public enum ActivityDungeonKind
{
    /// <summary>金币副本：周一/三/六，每次 10 体力，只掉金币。</summary>
    Gold,
    /// <summary>徽记副本：周二/四/日，每次 10 体力，只掉随机职业徽记。</summary>
    Badge,
    /// <summary>卷轴副本：周五，每次 15 体力，只掉招募卷轴（2026-10-24 主人拍板）。</summary>
    Scroll,
    /// <summary>礼物副本：周六，每次 10 体力，只掉好感礼物（2026-10-26 主人拍板）。</summary>
    Gift
}

/// <summary>
/// 活动副本规则（2026-10-23 主人拍板「活动副本一拆二」+ 2026-10-24 主人拍板
/// 「金币是1 3 6 徽记是2 4 7 都是2次 每次10体力」+「卷轴：第三章通关开启 · 每周五 · 每次15体力 · 2次」）：
/// 开放日 = 金币副本 每周一/三/六；徽记副本 每周二/四/日；卷轴副本 每周五
/// （开放日口径 = 主人预制体横条 Info 文案，三本周一至周日每天恰有一本开放）。
/// 开放日按<b>本地时间</b>判定（玩家感知的「今天」）；
/// 次数日键仍走 UTC（照 <see cref="EndlessRiftRules.EnsureDay"/> 口径）。
/// 三本每日各 2 次（各自独立计数），进本 = 次数 + 体力一起扣（TryConsumeEnter 内含提示）。
/// </summary>
public static class ActivityDungeonRules
{
    /// <summary>
    /// 【2026-10-23 主人拍板「现在把活动副本都开启让我测试 等打包的时候再区分」】测试期总闸：
    /// true = 三本不区分开放日、进本不看章节通关门槛、三难度全开（难度逐级解锁旁路），
    /// 方便随时进任意副本/难度测试；**正式打包前改回 false** 即恢复全部区分口径（一处改回，全链生效）。
    /// 【2026-10-24 晚收窄】总闸<b>不再旁路显示层</b>：活动横条显隐/左列标签锁永远按真实存档章节门槛
    /// （主人「没达到开放条件的不要显示」）——未达门槛的横条/标签直接看不到、点不进。
    /// 注意：每日次数与体力消耗<b>不受</b>总闸影响（照常计）。
    /// </summary>
    public const bool TEST_OPEN_ALL = true;

    /// <summary>每日进入次数（2026-10-24 主人拍板「都是2次」，三本各自独立计数）。</summary>
    public const int DAILY_TRIES = 2;

    /// <summary>金币/徽记进本体力（2026-10-24 主人拍板「每次10体力」，两本同价）。</summary>
    public const int ENTRY_STAMINA_COST = 10;

    /// <summary>卷轴副本进本体力（2026-10-24 主人拍板「15体力」）。</summary>
    public const int SCROLL_ENTRY_STAMINA_COST = 15;

    /// <summary>
    /// 进本体力和开放日按种类分流（体力单一出口，Interaction/Detail/StaminaSystem 都问这里）。
    /// 礼物副本照金币/徽记同价 10 体力（2026-10-26 拍板未单独给价，先按同价口径，要改改这里）。
    /// </summary>
    public static int EntryStaminaCost(ActivityDungeonKind kind)
        => kind == ActivityDungeonKind.Scroll ? SCROLL_ENTRY_STAMINA_COST : ENTRY_STAMINA_COST;

    /// <summary>该种类副本今天是否开放（本地时间判定；测试总闸打开时恒开放）。</summary>
    public static bool IsOpenToday(ActivityDungeonKind kind)
    {
        if (TEST_OPEN_ALL) return true;   // 测试期总闸：随时可进
        var d = DateTime.Now.DayOfWeek;
        switch (kind)
        {
            case ActivityDungeonKind.Gold:
                return d == DayOfWeek.Monday || d == DayOfWeek.Wednesday || d == DayOfWeek.Saturday;
            case ActivityDungeonKind.Badge:
                return d == DayOfWeek.Tuesday || d == DayOfWeek.Thursday || d == DayOfWeek.Sunday;
            case ActivityDungeonKind.Scroll:
                return d == DayOfWeek.Friday;
            case ActivityDungeonKind.Gift:
                return d == DayOfWeek.Saturday;   // 2026-10-26 主人拍板：礼物副本周六开启
            default:
                return false;
        }
    }

    /// <summary>开放日不对的提示文案（Interaction 预检单一出口，不许各写一份）。</summary>
    public static string OpenDayHint(ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Gold:  return "金币副本只在每周一、三、六开放";
            case ActivityDungeonKind.Badge: return "徽记副本只在每周二、四、日开放";
            case ActivityDungeonKind.Scroll: return "卷轴副本只在每周五开放";
            case ActivityDungeonKind.Gift:   return "礼物副本只在每周六开放";
            default: return "该副本暂未开放";
        }
    }

    /// <summary>活动副本入口的章节解锁门槛（2026-10-24 主人拍板：卷轴=通关第 3 章开启；金币/徽记=通关第 1 章）。</summary>
    public static int GateChapter(ActivityDungeonKind kind)
        => kind == ActivityDungeonKind.Scroll ? 3 : 1;

    /// <summary>活动副本展示名（toast/详情单一出口，不许各写一份）。</summary>
    public static string DungeonName(ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Gold:   return "金币副本";
            case ActivityDungeonKind.Badge:  return "徽记副本";
            case ActivityDungeonKind.Scroll: return "卷轴副本";
            case ActivityDungeonKind.Gift:   return "礼物副本";
            default: return "活动副本";
        }
    }

    /// <summary>
    /// 战斗场景章（2026-10-24 主人拍板「金币用第一章森林的战斗场景 徽记用亡灵的 卷轴用洞穴的」）：
    /// 建关 currentChapter 与详情怪预览都取这里，对应 chapter_theme_map 题材（1 暮影森林 / 2 幽冥墓园 / 6 巨岩深窟）。
    /// </summary>
    public static int SceneChapter(ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Gold:   return 1;
            case ActivityDungeonKind.Badge:  return 2;
            case ActivityDungeonKind.Scroll: return 6;
            // 礼物本暂用洞穴同卷轴（无专属怪图回落题材章口径，2026-10-24 卷轴先例）；主人要换场景改这一行
            case ActivityDungeonKind.Gift:   return 6;
            default: return 1;
        }
    }

    // —— 难度逐级解锁（2026-10-23 主人拍板「打通前一难度才开下一难度」）——
    // 进度真源 = SaveData.activityDungeonClearedDiff（-1=未通任何难度）；金币/徽记/卷轴三本共用同一活动标签进度。

    /// <summary>活动副本难度是否已解锁：初级恒开放，中级需通初级，高级需通中级。</summary>
    public static bool IsDiffUnlocked(SaveData data, int diff)
    {
        if (TEST_OPEN_ALL) return true;   // 测试期总闸：三难度全开
        if (diff <= 0) return true;
        int cleared = data?.activityDungeonClearedDiff ?? -1;
        return cleared >= diff - 1;
    }

    /// <summary>通关时记录最高已通难度（只进不退）并落盘。死亡/撤离不通关，不会走到这里。</summary>
    public static void MarkCleared(int difficulty)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (difficulty > data.activityDungeonClearedDiff)
        {
            data.activityDungeonClearedDiff = difficulty;
            SaveSystem.Instance?.Save();
            Debug.Log($"[ActivityDungeon] 通关记录：活动副本最高难度 → {difficulty}");
        }
    }

    // —— 每日次数（2026-10-24 主人拍板「都是2次」：三本各自独立计数，同一套出口）——

    /// <summary>跨天清零每日次数（照 EndlessRiftRules.EnsureDay 的 dayKey 模式）。
    /// 【2026-10-28 改】UTC→本地日（0 点刷新，与每日登录/商店同口径）；
    /// 升级兼容：旧 UTC 键在本地 0~8 点 == 本地今天 → 平移保留次数，不清零加次数（主人拍板）。
    /// 平移立即落盘（Load 正常路径不写盘，防平移丢失后清零多领，理由同 ResourceAdRewards.EnsureDay）。</summary>
    static void EnsureDay(SaveData data, ref string dayKey, ref int tries)
    {
        if (data == null) return;
        string today = ShopDefs.TodayKey();
        if (dayKey == today) return;
        if (ShopDefs.LegacyUtcDayKeyStillToday(dayKey))
        {
            dayKey = today;
            SaveSystem.Instance?.Save();
            return;
        }
        dayKey = today;
        tries = 0;
    }

    /// <summary>今日剩余进入次数（三本各自独立计数）</summary>
    public static int TriesLeft(SaveData data, ActivityDungeonKind kind)
    {
        if (data == null) return DAILY_TRIES;
        switch (kind)
        {
            case ActivityDungeonKind.Gold:
                EnsureDay(data, ref data.goldDungeonDayKey, ref data.goldDungeonTriesToday);
                return Mathf.Max(0, DAILY_TRIES - data.goldDungeonTriesToday);
            case ActivityDungeonKind.Badge:
                EnsureDay(data, ref data.badgeDungeonDayKey, ref data.badgeDungeonTriesToday);
                return Mathf.Max(0, DAILY_TRIES - data.badgeDungeonTriesToday);
            case ActivityDungeonKind.Scroll:
                EnsureDay(data, ref data.scrollDungeonDayKey, ref data.scrollDungeonTriesToday);
                return Mathf.Max(0, DAILY_TRIES - data.scrollDungeonTriesToday);
            case ActivityDungeonKind.Gift:
                EnsureDay(data, ref data.giftDungeonDayKey, ref data.giftDungeonTriesToday);
                return Mathf.Max(0, DAILY_TRIES - data.giftDungeonTriesToday);
            default:
                return 0;
        }
    }

    /// <summary>
    /// 进本：次数 + 体力一起扣（体力 = <see cref="EntryStaminaCost"/>），成功计数落盘。
    /// 任何一步不满足都返回 false 并弹对应提示。
    /// </summary>
    public static bool TryConsumeEnter(ActivityDungeonKind kind)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (TriesLeft(data, kind) <= 0)
        {
            GlobalToastUI.Show($"今天的{DungeonName(kind)}次数已用完（{DAILY_TRIES}/{DAILY_TRIES}）", true);
            return false;
        }
        if (!StaminaSystem.TrySpendForActivityDungeon(kind))
            return false;   // 体力不足：ResourceWallet.TrySpend 内已弹「体力不足」
        switch (kind)
        {
            case ActivityDungeonKind.Gold:   data.goldDungeonTriesToday++;  break;
            case ActivityDungeonKind.Badge:  data.badgeDungeonTriesToday++; break;
            case ActivityDungeonKind.Scroll: data.scrollDungeonTriesToday++; break;
            case ActivityDungeonKind.Gift:   data.giftDungeonTriesToday++;  break;
        }
        SaveSystem.Instance?.Save();
        return true;
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

    // 【2026-10-14 主人拍板】不覆写 FreeEntryDraws（保持基类 0）：引导三拍照常**扣钱**，
    // 钱由引导启动金兜住（旧价 80：80+88+97=265；2026-10-23 改价 40 起、每抽 +10 后三拍 40+50+60=150，
    // 引导启动金同日 270 → 200，200 ≥ 150 仍兜得住，见 SlotMachineDefs.TUTORIAL_STARTER_COINS）。
    // 10-13 那版「引导三拍免费抽」已删，不留开关。
}

/// <summary>
/// 无限裂隙（UI「无限裂隙」，冒险界面下标 2，原占位名「迷宫探索」）。
/// 玩法（2026-10-29 主人拍板）：每天 3 次 × 15 体力（体力档 10-29 二改 20→15，见 StaminaSystem.ENDLESS_RIFT_COST）；
/// **每层开始抽 3 次奖**（免费额度层间重置，定向抽不开启）；抽奖价格按本局已抽数递增；
/// 难度走锯齿曲线（每节 +6%、节内每层 +4%、精英层 ×1.10，见 GameConfig.EndlessDifficultyMul；旧线性 +7%/缓冲 9 折已废）；
/// 每 5 层一个 BOSS 记录点；首通奖励（金币+天赋石）一次性、宝箱掉落每次都发；
/// 死亡/撤离当层不发、已通层保留；下次从记录点 +1 层开打；每 5 层换一套场景背景（轮换现有 8 章资源）；
/// 层间黑屏过场字幕「第 N 层」。佣兵/技能跨层保留，直到死亡退出。
/// 施工文档：Docs/无限裂隙副本_待办任务清单.md
/// </summary>
public sealed class EndlessBattleMode : BattleMode
{
    public override BattleModeId Id => BattleModeId.Endless;
    public override string DisplayName => "无限裂隙";
    public override bool IsPlayable => true;

    /// <summary>每层开始免费抽 3 次（走战斗内老虎机既有免费额度框架，层间重置，见 ENDLESS_RIFT_FREE_DRAWS）</summary>
    public override int FreeEntryDraws => SlotMachineDefs.ENDLESS_RIFT_FREE_DRAWS;

    /// <summary>局内金币照发：免费抽完后玩家用局内金币继续抽构筑</summary>
    public override bool GrantsRunCoins => true;

    /// <summary>无限裂隙不播战斗结果界面（2026-10-24 主人拍板）：每层通关即有累计奖励，
    /// 过层直接走 <see cref="BattleManager.SettleEndlessStageClear"/> 的宝箱/首通/续层链；阵亡也直接进遗产结算。</summary>
    public override bool UsesBattleResultScreen => false;

    /// <summary>【2026-10-29 主人拍板】裂隙死亡**要**弹复活选择窗（含看广告档，每日 2 广告+2 钻石总闸见
    /// <see cref="ReviveQuota"/>）—— 与通关结算屏解耦；放弃复活才走裂隙遗产结算线。</summary>
    public override bool OffersRevive => true;

    public override void BuildChapter(ChapterManager cm, int chapter)
    {
        if (cm == null)
        {
            Debug.LogError("[BattleMode] 无限裂隙 BuildChapter 失败：ChapterManager 为空");
            return;
        }
        // chapter 参数对无限裂隙无意义：起始层由存档进度（记录点+1）决定
        cm.StartEndlessRift(EndlessRiftRules.StartStage());
    }

    /// <summary>每层通关 → 无限裂隙专属结算（首通/宝箱/续层/记录点双选），核心不动</summary>
    public override void SettleStageClear(BattleManager bm)
    {
        if (bm == null)
        {
            Debug.LogError("[BattleMode] 无限裂隙 SettleStageClear 失败：BattleManager 为空");
            return;
        }
        bm.SettleEndlessStageClear();
    }
}

/// <summary>
/// 无限裂隙规则：每日次数、进度读写、层数推导。纯静态，不碰战斗核心。
/// 进度真源 = SaveData.endlessRiftClearedStage（已首通最高层）；记录点 = 本值 / 5 × 5。
/// </summary>
public static class EndlessRiftRules
{
    /// <summary>跨天清零每日次数（照 ResourceAdRewards.EnsureDay 的 dayKey 模式）。
    /// 【2026-10-28 改】UTC→本地日（0 点刷新，与每日登录/商店同口径）；
    /// 升级兼容：旧 UTC 键在本地 0~8 点 == 本地今天 → 平移保留次数，不清零加次数（主人拍板）。
    /// 平移立即落盘（Load 正常路径不写盘，防平移丢失后清零多领，理由同 ResourceAdRewards.EnsureDay）。</summary>
    public static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = ShopDefs.TodayKey();
        if (data.endlessRiftDayKey == today) return;
        if (ShopDefs.LegacyUtcDayKeyStillToday(data.endlessRiftDayKey))
        {
            data.endlessRiftDayKey = today;
            SaveSystem.Instance?.Save();
            return;
        }
        data.endlessRiftDayKey = today;
        data.endlessRiftTriesToday = 0;
    }

    /// <summary>今日剩余进入次数</summary>
    public static int TriesLeft(SaveData data)
    {
        EnsureDay(data);
        return Mathf.Max(0, GameConfig.ENDLESS_DAILY_TRIES - (data?.endlessRiftTriesToday ?? 0));
    }

    /// <summary>
    /// 进本：次数 + 体力一起扣，成功计数落盘。任何一步不满足都返回 false 并弹对应提示。
    /// </summary>
    public static bool TryConsumeEnter()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        EnsureDay(data);
        if (data.endlessRiftTriesToday >= GameConfig.ENDLESS_DAILY_TRIES)
        {
            GlobalToastUI.Show($"今天的裂隙挑战次数已用完（{GameConfig.ENDLESS_DAILY_TRIES}/{GameConfig.ENDLESS_DAILY_TRIES}）", true);
            return false;
        }
        if (!StaminaSystem.TrySpendForEndlessRift())
            return false;   // 体力不足：ResourceWallet.TrySpend 内已弹「体力不足」
        data.endlessRiftTriesToday++;
        // 虚拟排行榜的闲置衰减锚点（2026-10-24）：只有真实进本才写，隔了几天没玩排名会回落
        data.endlessRiftLastPlayDayKey = DateTime.UtcNow.ToString("yyyyMMdd");
        SaveSystem.Instance?.Save();
        return true;
    }

    /// <summary>本次进本的起始层 = 记录点 + 1（主人拍板：不重打记录层）</summary>
    public static int StartStage()
    {
        var data = SaveSystem.Instance?.Data;
        return CheckpointOf(data?.endlessRiftClearedStage ?? 0) + 1;
    }

    /// <summary>记录点 = 已首通最高层向下取整到 5 的倍数，只进不退</summary>
    public static int CheckpointOf(int clearedStage) => Mathf.Max(0, clearedStage) / GameConfig.ENDLESS_CHECKPOINT_INTERVAL * GameConfig.ENDLESS_CHECKPOINT_INTERVAL;

    /// <summary>标记该层首通完成并落盘；返回是否真的推进了进度（= 是否本次首通）</summary>
    public static bool MarkCleared(int riftStage)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (riftStage <= data.endlessRiftClearedStage) return false;
        data.endlessRiftClearedStage = riftStage;
        SaveSystem.Instance?.Save();
        return true;
    }

    // ── 虚拟排行榜（2026-10-24 主人拍板「简单点」，无真实服务器，纯本机公式）──
    // 领先百分比 = 100 × (1 − e^(−有效层 / 8))；有效层 = 已首通最高层 − 2 × 闲置天数（下限 1）。
    // 参考曲线（刚打完，闲置 0 天）：
    //   1 层≈12%、5 层≈47%、10 层≈63%、15 层≈77%、20 层≈86%、30 层≈92%（越高越难涨，自然封手）
    // 闲置衰减（−2 层/天）：15 层打完隔 4 天 ≈58%、隔 7 天 ≈12% —— 「隔了几天排名就靠后了」。
    // 要调手感只动下面两个常量；参数 meanings 见常量注释。
    /// <summary>每闲置一天，有效层数扣多少（主人口径「隔了几天排名就靠后了」的衰减速度）</summary>
    const int RankIdleFloorLossPerDay = 2;
    /// <summary>曲线尺度：有效层 = 8 时领先约 63%；越大 → 同层数百分比越低、涨得越慢</summary>
    const float RankCurveScale = 8f;

    /// <summary>距上次真实进入无限裂隙过了几个自然日（UTC；从没玩过=0 不衰减）</summary>
    public static int IdleDaysOf(SaveData data)
    {
        string key = data?.endlessRiftLastPlayDayKey;
        if (string.IsNullOrEmpty(key)) return 0;
        if (!DateTime.TryParseExact(key, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTime last))
            return 0;   // 日键解析失败按没衰减算（不惩罚玩家）
        return Mathf.Max(0, (DateTime.UtcNow.Date - last).Days);
    }

    /// <summary>
    /// 虚拟排行榜：当前进度层数领先的玩家百分比（1~99 整数）。
    /// 只显示「领先了多少」不给具体名次（主人拍板）；从没玩过的玩家由 UI 侧另行给引导文案。
    /// </summary>
    public static int RankPercentOf(SaveData data)
    {
        int best = Mathf.Max(0, data?.endlessRiftClearedStage ?? 0);
        int eff = Mathf.Max(1, best - RankIdleFloorLossPerDay * IdleDaysOf(data));
        int pct = Mathf.RoundToInt((1f - Mathf.Exp(-eff / RankCurveScale)) * 100f);
        return Mathf.Clamp(pct, 1, 99);
    }
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
    public static readonly BadgeDungeonBattleMode BadgeDungeon = new BadgeDungeonBattleMode();   // 2026-10-23 徽记副本（不占 SlotTable 位）
    public static readonly ScrollDungeonBattleMode ScrollDungeon = new ScrollDungeonBattleMode(); // 2026-10-24 卷轴副本（不占 SlotTable 位）
    public static readonly GiftDungeonBattleMode GiftDungeon = new GiftDungeonBattleMode();       // 2026-10-26 礼物副本（不占 SlotTable 位）
    public static readonly BondDungeonBattleMode BondDungeon = new BondDungeonBattleMode();       // 2026-10-27 羁绊副本（好感满佣兵专属，不占 SlotTable 位）
    public static readonly SideIslandBattleMode SideIsland = new SideIslandBattleMode();         // 2026-10-23 支线岛（不占 SlotTable 位）
    public static readonly FinaleBattleMode Finale = new FinaleBattleMode();                     // 2026-10-23 终章追查关（不占 SlotTable 位）
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
            case BattleModeId.BadgeDungeon: return BadgeDungeon;   // 2026-10-23 徽记副本
            case BattleModeId.SideIsland: return SideIsland;       // 2026-10-23 支线岛
            case BattleModeId.Finale: return Finale;               // 2026-10-23 终章追查关
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
