using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天赋解锁、消耗与存档写入。
/// 【2026-11-05 主人拍板】左右列解锁门槛统改人物等级（互相解耦）：左列 N 级开第 N 个（金币）；
/// 右列门槛/等级上限 = 人物等级（天赋石，可升级、互斥、可洗点），升级跨门槛由 NotifyLevelUp 弹提示。
/// </summary>
public static class TalentSystem
{
    public enum Branch
    {
        Left,
        Right
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】右列「天赋石不足」的统一原因串：
    /// UI 侧见此原因时**不再干巴巴 toast**，而是照样弹费用确认窗让玩家看到「需要多少」。
    /// 字面量统一引这里，防止文案漂移导致 UI 判断失灵。
    /// </summary>
    public const string ReasonNoStones = "天赋石不足";

    /// <summary>遗忘药水道具 id（item_defs.csv；2026-10-21 主人拍板：洗点消耗遗忘药水）。</summary>
    public const string ItemForgetPotion = "item_forget_potion";

    /// <summary>
    /// 下一次洗点消耗的遗忘药水数（2026-10-27 主人拍板阶梯计费）：
    /// 第 1-3 次各 1 个、第 4-6 次各 2 个、第 7 次起各 3 个（不再继续涨）。
    /// 次数真源 = <see cref="SaveData.respecCount"/>（已完成洗点次数，本类 TryRespecRight 是唯一写入口）。
    /// </summary>
    public static int NextRespecPotionCost(int respecDoneCount)
    {
        int nth = Mathf.Max(0, respecDoneCount) + 1;   // 下一次 = 第 nth 次
        if (nth <= 3) return 1;
        if (nth <= 6) return 2;
        return 3;
    }

    // ===== 左列（金币，保持不变）=====
    public static bool CanUnlockLeft(int index1Based, out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }
        if (index1Based < 1 || index1Based > TalentDefs.Left.Length) { reason = "无效节点"; return false; }

        int unlocked = TalentDefs.LeftUnlockedCount(data.talents);
        if (index1Based != unlocked + 1) { reason = "请按顺序解锁"; return false; }

        var node = TalentDefs.Left[index1Based - 1];
        // 【2026-11-05 主人拍板「左边的天赋按玩家升一级开一个」】解锁门槛 = 人物等级
        //（替换 10-21 的章节门槛；满级 80 ↔ 80 节点一一对应，见 TalentDefs.LeftNodeUnlockLevel）。
        int needLv = TalentDefs.LeftNodeUnlockLevel(index1Based);
        if (LevelSystem.CurrentLevel < needLv)
        {
            reason = $"等级达到{needLv}级后解锁";
            return false;
        }
        int cost = GetLeftGoldCost(node, data.talents);
        if (data.totalGold < cost) { reason = "金币不足"; return false; }
        return true;
    }

    public static bool TryUnlockLeft(int index1Based, out string reason)
    {
        if (!CanUnlockLeft(index1Based, out reason)) return false;
        var data = SaveSystem.Instance.Data;
        var node = TalentDefs.Left[index1Based - 1];
        int cost = GetLeftGoldCost(node, data.talents);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: true))
        {
            reason = "金币不足";
            return false;
        }
        data.talents[node.id] = 1;
        RefreshTalentEntryRedDot(data);

        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        // 【2026-11-05 拍板】右列门槛改挂人物等级、与左列购买解耦 → 左列购买不再附带右列提示/演出
        //（提示改由升级链路触发，见 NotifyLevelUp）。
        AnnounceTalentGain(node != null ? node.name : null, node != null ? node.effect : null);
        return true;
    }

    public static int GetLeftGoldCost(TalentDefs.LeftNode node, IDictionary<string, int> talents)
    {
        if (node == null) return 0;
        // 右列已无「左列金币折扣」节点，故不再有折扣分支。
        return Mathf.Max(1, Mathf.RoundToInt(node.goldCost));
    }

    /// <summary>左列天赋购买成功后的「恭喜获得」庆祝提示（2026-09-21 用户要求，样式对齐装备的 EquipDropPopupUI.ShowEquipGain）。
    /// 【2026-11-05 拍板】右列提示与左列解耦，原 rightHint 参数与 BuildRightUnlockHint 已删，右列提示改由 NotifyLevelUp 弹。</summary>
    static void AnnounceTalentGain(string nodeName, TalentDefs.Effect fx)
    {
        if (fx == null || string.IsNullOrEmpty(fx.display)) return;
        string text = string.IsNullOrEmpty(nodeName)
            ? $"恭喜获得【{fx.display}】，实力大增！"
            : $"恭喜获得【{nodeName}】，{fx.display}，实力大增！";
        UIManager.Instance?.ShowToast(text);
    }

    /// <summary>
    /// 2026-09-29 主人要求：左列推到某个右列门槛时**必须明确告诉玩家右边有东西可点**，
    /// 不能让玩家点了半天左列却不知道右列已经开了。
    /// 覆盖两种情况：① 节点跨过门槛、首次可解锁；② 已解锁节点的等级上限提高、可以继续升。
    /// <para>🔴 只**生成文案**、不自己弹：GlobalToastUI 是覆盖式的（Play 会 StopCoroutine 掉上一条），
    /// 连弹两条只会剩下后一条，所以必须由 <see cref="AnnounceTalentGain"/> 拼进同一条 toast。</para>
    /// </summary>
    /// <summary>2026-11-05 拍板：右列提示唯一触发点 = 人物升级跨过门槛（NotifyLevelUp），文案只说「有新的可解锁」。</summary>
    const string RightUnlockHint = "升级了！右侧有新的天赋可以解锁";

    /// <summary>
    /// 天赋入口红点 = **还有没有「新开通未点」的右列节点**。
    /// 2026-09-29 主人纠正：玩家点左列时本来就已经在天赋页里了，所以「打开天赋页就清」等于永远看不到红点；
    /// 正确口径是**点了右侧那个新天赋**才灭（level &gt; 0 → 不再是新）。
    /// </summary>
    static void RefreshTalentEntryRedDot(SaveData data)
    {
        RedDot.Set(RedDot.Talent, data != null && TalentDefs.AnyNewlyOpen(data.talents));
    }

    /// <summary>
    /// 【2026-11-05 主人拍板「右边天赋达到解锁条件时弹个 toast」】人物升级后由 LevelSystem 调用
    ///（AddStageClearExp / AddDirectExp / AddExpForTown 三个经验入口）：右列门槛（=人物等级）刚跨过的节点
    /// → 弹 toast + 刷入口红点；天赋页开着就顺带播右列开通演出（TalentUI.OnLevelUpFx）。
    /// 判定口径：已解锁 / 被互斥锁 / R_DUAL 前置没满足的不算「新解锁」。
    /// </summary>
    public static void NotifyLevelUp(int beforeLevel, int afterLevel)
    {
        if (afterLevel <= beforeLevel) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        bool any = false;
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            if (node == null || node.unlockLeftIndex <= beforeLevel || node.unlockLeftIndex > afterLevel) continue;
            if (TalentDefs.GetRightNodeLevel(data.talents, node.id) > 0) continue;      // 已解锁
            if (TalentDefs.IsRightNodeMutexLocked(node, data.talents)) continue;        // 互斥：已选其它流派
            if (node.id == "R_DUAL"
                && TalentDefs.GetRightNodeLevel(data.talents, "R_F1") < 5
                && TalentDefs.GetRightNodeLevel(data.talents, "R_F2") < 5) continue;    // 双修前置没满足
            any = true;
        }

        RefreshTalentEntryRedDot(data);
        if (!any) return;
        TalentUI.OnLevelUpFx(beforeLevel, afterLevel);
        UIManager.Instance?.ShowToast(RightUnlockHint);
    }

    static void AnnounceRightGain(TalentDefs.TalentRightNode node, int level, int chosenJob)
    {
        if (node == null) return;
        string text = node.name + " Lv" + level;
        if (level > 0)
        {
            string eff = node.EffectSummary(level, chosenJob);
            if (!string.IsNullOrEmpty(eff) && eff != node.name) text += " · " + eff;
        }
        UIManager.Instance?.ShowToast(text);
    }

    /// <summary>
    /// 把「背包可用行数」的真值写回存档（唯一权威写入点，2026-09-21 主人要求）。
    /// R_BAG（背包扩容）解锁 → 4 行；洗点把 R_BAG 清掉 → 回落默认 3 行。
    /// 这样 GameConfig.GetUnlockedBackpackRows 里的天赋字典兜底只承担旧存档兼容，
    /// 不再是「字段没落盘也能用」的主路径。
    /// </summary>
    static void SyncBackpackRows(SaveData data)
    {
        if (data == null) return;
        data.backpackRows = Mathf.Clamp(
            GameConfig.BACKPACK_DEFAULT_ROWS + TalentDefs.CountBagRowUnlocks(data.talents),
            1, GameConfig.BACKPACK_HEIGHT_MAX);
    }

    // ===== 右列（天赋石，重制后模型）=====

    /// <summary>能否解锁某右列节点（level 0 → 1）。互斥/分批/双修前置都在这里判定。</summary>
    public static bool CanUnlockRightNode(string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(data.talents, id);
        if (level > 0) { reason = "已解锁"; return false; }

        // 任务4：互斥组已有其它节点则点不了
        if (TalentDefs.IsRightNodeMutexLocked(node, data.talents)) { reason = "互斥：已选其它流派"; return false; }

        // R_DUAL 前置：主流派（R_F1/R_F2）需满 5 级
        if (node.id == "R_DUAL")
        {
            int p1 = TalentDefs.GetRightNodeLevel(data.talents, "R_F1");
            int p2 = TalentDefs.GetRightNodeLevel(data.talents, "R_F2");
            if (p1 < 5 && p2 < 5) { reason = "需主流派满 5 级"; return false; }
        }

        // 【2026-11-05 主人拍板「右侧天赋改人物等级开启，跟左列取消挂钩」】门槛 = 人物等级
        //（配表 unlockLeftIndex：10/20/30/40，语义由「左列已点数」改为「人物等级」）。
        if (LevelSystem.CurrentLevel < node.unlockLeftIndex)
        { reason = $"等级达到{node.unlockLeftIndex}级后解锁"; return false; }

        if (node.costByLevel == null || node.costByLevel.Length < 1) { reason = "配置错误"; return false; }
        if (data.talentPoints < node.costByLevel[0]) { reason = ReasonNoStones; return false; }
        return true;
    }

    public static bool TryUnlockRightNode(string id, int chosenJob1Based, out string reason)
    {
        if (!CanUnlockRightNode(id, out reason)) return false;
        var node = TalentDefs.GetRightNodeById(id);
        var data = SaveSystem.Instance.Data;

        if (node.IsJobChoice)
        {
            if (chosenJob1Based < 1 || chosenJob1Based > (node.options != null ? node.options.Length : 0))
            { reason = "请先选择职业"; return false; }
            data.talents[id + "_job"] = chosenJob1Based;
        }

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.TalentPoint, node.costByLevel[0], save: false, notify: true))
        { reason = ReasonNoStones; return false; }

        data.talents[id] = 1;
        data.talentClickTotal++;   // 【2026-11-04 主人拍板】主线「点 N 次天赋」独立计数：每次成功花天赋石 +1
        SyncBackpackRows(data);   // R_BAG 解锁 → SaveData.backpackRows 按等级 +行
        RefreshTalentEntryRedDot(data);   // 点了右侧新天赋 → 「新」消失，没有其它新节点就灭红点
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        AnnounceRightGain(node, 1, TalentDefs.GetRightNodeChosenJob(data.talents, id));
        return true;
    }

    /// <summary>能否把某右列节点从当前等级 +1。受等级上限（任务3）与天赋石限制。</summary>
    public static bool CanUpgradeRightNode(string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(data.talents, id);
        if (level <= 0) { reason = "请先解锁"; return false; }
        if (level >= node.maxLevel) { reason = "已满级"; return false; }

        // 任务3：等级上限随**人物等级**（2026-11-05 拍板：右列与左列解耦，门槛与上限都看人物等级）
        int cap = TalentDefs.RightNodeLevelCap(node, LevelSystem.CurrentLevel);
        if (level >= cap)
        {
            // 拦截话术带目标：直接报升到几级才能升下一级（2026-11-05 主人反馈「说是能升级但不知道怎么升」）。
            int need = TalentDefs.LevelNeededForNextLevel(node, level);
            reason = $"升到{need}级后可升级（当前上限 Lv{cap}）";
            return false;
        }

        if (node.costByLevel == null || level >= node.costByLevel.Length) { reason = "配置错误"; return false; }
        if (data.talentPoints < node.costByLevel[level]) { reason = ReasonNoStones; return false; }
        return true;
    }

    public static bool TryUpgradeRightNode(string id, out string reason)
    {
        if (!CanUpgradeRightNode(id, out reason)) return false;
        var node = TalentDefs.GetRightNodeById(id);
        var data = SaveSystem.Instance.Data;
        int level = TalentDefs.GetRightNodeLevel(data.talents, id);

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.TalentPoint, node.costByLevel[level], save: false, notify: true))
        { reason = ReasonNoStones; return false; }

        data.talents[id] = level + 1;
        data.talentClickTotal++;   // 【2026-11-04 主人拍板】主线「点 N 次天赋」独立计数
        SyncBackpackRows(data);
        RefreshTalentEntryRedDot(data);
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        AnnounceRightGain(node, level + 1, TalentDefs.GetRightNodeChosenJob(data.talents, id));
        return true;
    }

    // ===== 任务5：洗点（仅右列，退 80%）=====
    /// <summary>能否洗右列：有任意已投右列节点即可。</summary>
    public static bool CanRespecRight(out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            int lv = TalentDefs.GetRightNodeLevel(data.talents, TalentDefs.RightNodes[i].id);
            if (lv > 0) return true;
        }
        reason = "右列暂无可洗天赋";
        return false;
    }

    /// <summary>
    /// 遗忘药水够不够下一次洗点（2026-10-27 主人拍板：按阶梯消耗判定，见 <see cref="NextRespecPotionCost"/>）。
    /// 旧口径（10-21 固定 ×1）作废。
    /// </summary>
    public static bool HasForgetPotion(out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }
        int cost = NextRespecPotionCost(data.respecCount);
        int have = data.TownBagCount(ItemForgetPotion);
        if (have >= cost) return true;
        reason = "需要遗忘药水×" + cost + "（当前持有 " + have + "；里程商店 500 点兑换，每周限购 1）";
        return false;
    }

    /// <summary>
    /// 洗右列：清除所有右列节点的等级（含 JobChoice 的已选职业），退还已投入天赋石的 80%（向下取整）。
    /// 左列不可洗。
    /// 【2026-10-27 主人拍板】消耗走阶梯（1/1/1 → 2/2/2 → 3…），每成功洗点一次 respecCount +1 ——
    /// 本方法即 respecCount 唯一写入口（原「respecCount 由另一子代理写入」口径作废：全库并无写入者）。
    /// </summary>
    public static bool TryRespecRight(out string reason)
    {
        if (!CanRespecRight(out reason)) return false;
        var data = SaveSystem.Instance.Data;

        int potionCost = NextRespecPotionCost(data.respecCount);   // 先取价再计数，同一次调用内口径一致
        if (!HasForgetPotion(out reason)) return false;
        if (!data.TownBagTryConsume(ItemForgetPotion, potionCost))
        {
            reason = "遗忘药水扣除失败";
            return false;
        }
        data.respecCount++;

        int totalSpent = 0;
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            int lv = TalentDefs.GetRightNodeLevel(data.talents, node.id);
            if (lv > 0) totalSpent += TalentDefs.RightNodeTotalSpent(node, lv);
            data.talents.Remove(node.id);
            data.talents.Remove(node.id + "_job");
        }

        int refund = (int)(totalSpent * 0.8f); // 向下取整
        if (refund > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, refund, save: true, notify: true);

        SyncBackpackRows(data);   // 洗点清掉 R_BAG → SaveData.backpackRows 回落 3
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        UIManager.Instance?.ShowToast(refund > 0 ? $"洗点完成，返还天赋石 {refund}" : "洗点完成");
        return true;
    }

    // UI 复用：旧 CanReset / TryReset 改为洗右列入口
    public static bool CanReset(out string reason) => CanRespecRight(out reason);
    public static bool TryReset(out string reason) => TryRespecRight(out reason);

    public static bool IsLeftUpgradeable(int index0, IDictionary<string, int> talents)
    {
        if (talents == null) return false;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (index0 != unlocked) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        // 【2026-11-05 拍板】等级门槛并入可升级判定：等级不够的节点不出红点、整行置灰；
        // 点击走 CanUnlockLeft 弹「等级达到 N 级后解锁」，两处共用同一判定，不许各写一份。
        if (LevelSystem.CurrentLevel < TalentDefs.LeftNodeUnlockLevel(index0 + 1)) return false;
        return data.totalGold >= GetLeftGoldCost(TalentDefs.Left[index0], talents);
    }
}
