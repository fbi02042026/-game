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
    /// 【2026-11-23】背包扩容节点 id（与 TalentDefs.CountBagRowUnlocks 读的 "R_BAG" 同源）。
    /// 换职业**保留**该节点、不清不退：清它会连带把背包可用行从 6 掉回 3，
    /// 放在下部行的道具被网格过滤掉（不丢档，但玩家会当成装备没了）。
    /// 洗点仍照旧清掉（见 TryRespecRight 注释：洗点 = 重置右列）。
    /// </summary>
    const string BagNodeId = "R_BAG";

    /// <summary>
    /// 【2026-11-20 主人拍板】钻石换职业的花费（账号级）。UI 确认文案与扣费都读这一处，禁止另写数字。
    /// 换职业 = 扣钻石 + 清右列退 80% 天赋石（左列金币天赋不动）+ 切职业真源。
    /// </summary>
    public const long CHANGE_JOB_DIAMOND_COST = 500;

    /// <summary>
    /// 【2026-11-20 主人拍板】换职业的关卡门槛：通关「第 1 章第 5 关」后才允许换。
    /// 判定走主线进度真源 <c>SaveData.HasStageCleared(chapter, stageIndex0)</c>；stageIndex0 为 0 基，故第 5 关 = 4。
    /// 🔴 主人要求「可以改，到时看测试进度」——测试期要调门槛只改这两个数，不动判定代码。
    /// 🔴 只判进度、不清进度：换职业全程不碰 clearedStages，关卡记录天然保留。
    /// </summary>
    public const int CHANGE_JOB_UNLOCK_CHAPTER = 1;
    public const int CHANGE_JOB_UNLOCK_STAGE_INDEX0 = 4;

    /// <summary>换职业关卡门槛是否已达成（唯一判定出口）。未达标 reason 给 UI 直接 toast。</summary>
    public static bool CanChangeJobByProgress(out string reason)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || !data.HasStageCleared(CHANGE_JOB_UNLOCK_CHAPTER, CHANGE_JOB_UNLOCK_STAGE_INDEX0))
        {
            reason = $"切换职业\n通过第{CHANGE_JOB_UNLOCK_STAGE_INDEX0 + 1}关后解锁";
            return false;
        }
        reason = null;
        return true;
    }

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
    /// 【2026-11-19 主人拍板「只要有可解锁的就在角色和天赋图标加红点 每次登录游戏显示一次 玩家打开后关闭
    /// 注意要在金币天赋石够的情况下」】天赋入口红点（**唯一出口**，「天赋图标」与「角色入口」两个 key 一起刷）：
    /// · 亮 = 本次登录还没打开过天赋页（TalentDotSeenThisSession=false）且存在「可解锁/可升级且货币够」的天赋；
    ///   左列金币（CanUnlockLeft）、右列解锁（CanUnlockRightNode）、右列升级（CanUpgradeRightNode，
    ///   【2026-11-14 拍板】升级也算亮）——三个判定都是唯一口径函数，全部自带货币条件。
    /// · 玩家打开天赋页（TalentUI.Show）→ TalentDotSeenThisSession=true → 关闭，本次登录内不再亮；
    ///   下次登录（进程重启，静态清零）重新判定。旧口径（11-11 已看等级 / 11-14 已读 id 表）不再用于入口红点，
    ///   存档字段与右列行内「新」字逻辑保留不动。
    /// </summary>
    public static bool TalentDotSeenThisSession = false;   // 本次登录是否已打开过天赋页（进程级，不进存档）

    public static void RefreshTalentEntryRedDot(SaveData data)
    {
        bool on = !TalentDotSeenThisSession && HasAnyAffordableUnlockable(data);
        RedDot.Set(RedDot.Talent, on);
        RedDot.Set(RedDot.Character, on);
    }

    /// <summary>
    /// 是否存在「可解锁/可升级且货币够」的天赋（红点判定，2026-11-19 口径）：
    /// 左列按下一个未解锁节点判（顺序/人物等级/金币）；右列逐节点判未解锁（门槛/互斥/双修前置/天赋石）
    /// 或已解锁可升级（含天赋石）。只读判定，不改任何状态。
    /// </summary>
    public static bool HasAnyAffordableUnlockable(SaveData data)
    {
        if (data == null) return false;
        int unlocked = TalentDefs.LeftUnlockedCount(data.talents);
        if (CanUnlockLeft(unlocked + 1, out _)) return true;
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var n = TalentDefs.RightNodes[i];
            if (n == null || string.IsNullOrEmpty(n.id)) continue;
            if (TalentDefs.IsHiddenRightNode(n.id)) continue;   // 隐藏节点不计入（与旧口径一致）
            if (TalentDefs.GetRightNodeLevel(data.talents, n.id) <= 0)
            {
                if (CanUnlockRightNode(n.id, out _)) return true;
            }
            else if (CanUpgradeRightNode(n.id, out _)) return true;
        }
        return false;
    }

    /// <summary>
    /// 当前**可升级**（已解锁 / 未满级 / 等级上限够 / 天赋石够）的右列节点 id，逗号分隔；空串 = 无。
    /// 判定复用 <see cref="CanUpgradeRightNode"/>（唯一口径，不抄第二份）。
    /// 打开天赋页时由 TalentUI.Show 写入 SaveData.talentUpgradeSeenIds。
    /// </summary>
    public static string CollectUpgradeableRightIds(SaveData data)
    {
        if (data == null) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var n = TalentDefs.RightNodes[i];
            if (n == null || string.IsNullOrEmpty(n.id)) continue;
            if (TalentDefs.IsHiddenRightNode(n.id)) continue;   // 【2026-11-19】隐藏节点不计入红点/已读表
            if (!CanUpgradeRightNode(n.id, out _)) continue;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(n.id);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 是否存在「可升级但不在已读表」的右列节点（红点条件 ②）。
    /// 已读表 = SaveData.talentUpgradeSeenIds（打开天赋页时记下当时全部可升级节点）。
    /// </summary>
    public static bool HasUnseenUpgradeableRight(SaveData data)
    {
        if (data == null) return false;
        string seen = "," + (data.talentUpgradeSeenIds ?? "") + ",";
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var n = TalentDefs.RightNodes[i];
            if (n == null || string.IsNullOrEmpty(n.id)) continue;
            if (TalentDefs.IsHiddenRightNode(n.id)) continue;   // 【2026-11-19】隐藏节点不计入红点/已读表
            if (!CanUpgradeRightNode(n.id, out _)) continue;
            if (!seen.Contains("," + n.id + ",")) return true;
        }
        return false;
    }

    /// <summary>
    /// 【2026-11-05 主人拍板「右边天赋达到解锁条件时弹个 toast」】人物升级后由 LevelSystem 调用
    ///（AddStageClearExp / AddDirectExp / AddExpForTown 三个经验入口）：右列门槛（=人物等级）刚跨过的节点
    /// → 弹 toast + 刷入口红点；天赋页开着就顺带播右列开通演出（TalentUI.OnLevelUpFx）。
    /// 判定口径：已解锁 / 隐藏节点(R_F1/R_F2) 的不算「新解锁」（2026-11-19 R_DUAL 前置已改挂等级门槛）。
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
            if (TalentDefs.IsHiddenRightNode(node.id)) continue;                          // 【2026-11-19】隐藏节点不提示
            if (TalentDefs.GetRightNodeLevel(data.talents, node.id) > 0) continue;      // 已解锁
            if (TalentDefs.IsRightNodeMutexLocked(node, data.talents)) continue;        // 互斥：已选其它流派
            // 【2026-11-19 主人拍板】R_DUAL 原「主流派满5级」前置已改挂等级门槛（自身 unlockLeftIndex=9（2026-11-19 起））
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
        var data = SaveSystem.Instance?.Data;
        return CanUnlockRightNode(data != null ? data.talents : null,
                                  data != null ? data.talentPoints : 0, id, out reason);
    }

    /// <summary>
    /// 【2026-11-20 一键升级】带快照判定的唯一口径：真实购买与一键升级预览共用这一份规则。
    /// 预览传克隆出来的 talents / 余额，不改存档；正常调用仍走上面的两参重载。
    /// </summary>
    public static bool CanUnlockRightNode(IDictionary<string, int> talents, int talentStones, string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        if (talents == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(talents, id);
        if (level > 0) { reason = "已解锁"; return false; }

        // 任务4：互斥组已有其它节点则点不了
        if (TalentDefs.IsRightNodeMutexLocked(node, talents)) { reason = "互斥：已选其它流派"; return false; }

        // 【2026-11-19 主人拍板】R_DUAL 原「主流派满5级」前置已改挂等级门槛
        //（自身 unlockLeftIndex=45（2026-11-23 起），见下方等级判断）；流派节点已界面隐藏，不再依赖 R_F1/R_F2。

        // 【2026-11-05 主人拍板「右侧天赋改人物等级开启，跟左列取消挂钩」】门槛 = 人物等级
        //（配表 unlockLeftIndex：2026-11-23 起可见 10 格按 5~50 级每 5 级开一格，语义由「左列已点数」改为「人物等级」）。
        if (LevelSystem.CurrentLevel < node.unlockLeftIndex)
        { reason = $"等级达到{node.unlockLeftIndex}级后解锁"; return false; }

        if (node.costByLevel == null || node.costByLevel.Length < 1) { reason = "配置错误"; return false; }
        if (talentStones < node.costByLevel[0]) { reason = ReasonNoStones; return false; }
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
        var data = SaveSystem.Instance?.Data;
        return CanUpgradeRightNode(data != null ? data.talents : null,
                                   data != null ? data.talentPoints : 0, id, out reason);
    }

    /// <summary>
    /// 【2026-11-20 一键升级】带快照判定的唯一口径：真实购买与一键升级预览共用这一份规则。
    /// </summary>
    public static bool CanUpgradeRightNode(IDictionary<string, int> talents, int talentStones, string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        if (talents == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(talents, id);
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
        if (talentStones < node.costByLevel[level]) { reason = ReasonNoStones; return false; }
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
    /// 【洗点 / 换职业共用】清右列节点并按 80% 算出应退天赋石（向下取整）——退费计算的唯一出口，禁止第二份 0.8。
    /// 只清右列（左列金币天赋不动）；含 JobChoice 的 _job 后缀键。调用方负责 Add 退款与 Save/刷新。
    /// keepBagRows=true 时**跳过背包扩容节点**（换职业用：保留扩容、其石头也不退）；洗点仍传 false 全清。
    /// ⚠ 换职业务必在 SetSelected **之前**调用：RightNodes 按当前职业取视图，切了职业就读不到旧职业节点。
    /// </summary>
    static int ClearRightAndRefundCore(SaveData data, bool keepBagRows = false)
    {
        int refund = CalcRightRefundCore(data, keepBagRows);   // 先算（要用清除前的等级）
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            if (keepBagRows && node.id == BagNodeId) continue;   // 背包扩容保留（见 BagNodeId 注释）
            data.talents.Remove(node.id);
            data.talents.Remove(node.id + "_job");
        }
        return refund;
    }

    /// <summary>
    /// 【2026-11-23】80% 退款的**纯计算**出口（只读，不改存档）：已投总额 × 0.8 向下取整。
    /// 洗点 / 换职业都从 ClearRightAndRefundCore 走它，换职业确认窗的「返还 N」预览也用它 ——
    /// 全项目仍然只有这一处 0.8，UI 不许再算第二份。
    /// </summary>
    public static int CalcRightRefundCore(SaveData data, bool keepBagRows = false)
    {
        if (data == null || data.talents == null) return 0;
        int totalSpent = 0;
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            if (keepBagRows && node.id == BagNodeId) continue;
            int lv = TalentDefs.GetRightNodeLevel(data.talents, node.id);
            if (lv > 0) totalSpent += TalentDefs.RightNodeTotalSpent(node, lv);
        }
        return (int)(totalSpent * 0.8f); // 向下取整
    }

    /// <summary>【2026-11-23】换职业确认窗用：预计返还天赋石（只读，不改任何状态）。
    /// 口径与 TryChangeJob 完全一致（含「背包扩容保留、不退」）。</summary>
    public static int PreviewChangeJobRefund()
        => CalcRightRefundCore(SaveSystem.Instance?.Data, keepBagRows: true);

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

        int refund = ClearRightAndRefundCore(data);
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

    /// <summary>
    /// 【2026-11-20 主人拍板】钻石换职业事务（账号级，**不是**每局进战选职业）。
    /// 收口在这里做，UI 只需先走 DiamondSpendGate.Request 二次确认、确认回调里调本方法。
    ///
    /// <para>顺序不可换：</para>
    /// <para>① TrySpendDiamond **先**扣费（唯一扣费出口）—— 失败直接 return，内存一个字节没动
    ///（2026-11-23 修：原来先清右列再扣费，扣费失败会留下「天赋已清、未退款、未落盘」的半截内存）；</para>
    /// <para>② 再按**旧职业视图**清右列 + 算退 80% 天赋石（ClearRightAndRefundCore 必须在 SetSelected 之前，
    /// RightNodes 读的是当前职业视图，切了就读不到旧节点）；③ 退款（save:false）；</para>
    /// <para>④ SetSelected 切职业真源（内部 Save 一次，此时状态已完整，避免半截盘；换表由 RightNodes 按新职业过滤自动生效）；</para>
    /// <para>⑤ RecalcAttr 重算 + 落盘兜底 + HUD/红点/天赋页刷新。</para>
    /// <para>左列金币天赋、以及背包扩容节点（R_BAG）全程不动。失败在任何一步之前 return，不产生半截状态。</para>
    /// </summary>
    public static bool TryChangeJob(PlayerJobId target, out string reason)
    {
        reason = null;
        if (!PlayerJobDefs.IsPlayable(target))
        {
            reason = "该职业暂未开放";
            return false;
        }
        // 【2026-11-20 主人拍板】关卡门槛：没通第1章第5关就不许换（事务级 fail closed，防 UI 绕过）。
        if (!CanChangeJobByProgress(out reason)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            reason = "无存档";
            return false;
        }
        PlayerJobId old = PlayerJobDefs.GetSelected();
        if (old == target)
        {
            reason = "当前已是该职业";
            return false;
        }
        if (ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond) < CHANGE_JOB_DIAMOND_COST)
        {
            reason = "钻石不足";
            return false;
        }

        // ① 先扣钻石（Diamond 唯一扣费出口，fail closed）—— 失败时内存零改动，不留半截状态
        if (!ResourceWallet.TrySpendDiamond(CHANGE_JOB_DIAMOND_COST, "更换职业", save: false, notify: false))
        {
            reason = "钻石扣除失败";
            return false;
        }

        // ② 旧职业视图下清右列 + 算退款（此时还没切职业，RightNodes = 旧职业表）。
        // keepBagRows：背包扩容节点保留不清（清它会让背包可用行掉回 3、下部道具看不见），其石头也不退。
        int refund = ClearRightAndRefundCore(data, keepBagRows: true);

        // ③ 退款（save:false，先攒齐所有内存改动）
        if (refund > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, refund, save: false, notify: false);
        SyncBackpackRows(data);   // R_BAG 保留 → 背包行数不变（幂等写回当前值）

        // ④ 切职业真源（单一出口）。SetSelected 内部会 Save 一次 ——
        // 此时钻石/天赋/退款/背包已全部改完，这次落盘就是完整状态，不留半截盘。
        PlayerJobDefs.SetSelected(target);

        // ⑤ 重算 + 落盘兜底 + 刷新（换表由 RightNodes 按新职业过滤自动生效）
        Hero.Instance?.RecalcAttr();
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        RefreshTalentEntryRedDot(data);   // 【2026-11-23】清右列后天赋入口红点按新状态重算（原来没人刷）
        // 【2026-11-23】RefreshForJobChange：职业变了先重建天赋页左右列再刷新，
        // 否则右列还是旧职业那套节点（点击按 id 落到新职业节点，名实错配）。
        if (TalentUI.Instance != null) TalentUI.Instance.RefreshForJobChange();
        UIManager.Instance?.ShowToast(refund > 0
            ? $"已转为{PlayerJobDefs.Get(target).DisplayName}，返还天赋石 {refund}"
            : $"已转为{PlayerJobDefs.Get(target).DisplayName}");
        Debug.Log($"[TalentSystem] 换职业 {old} → {target}，花钻石 {CHANGE_JOB_DIAMOND_COST}，返还天赋石 {refund}");
        return true;
    }

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
