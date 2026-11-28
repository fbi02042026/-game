using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天赋左列「点击确认卡 + 一键升级」（2026-11-19 主人拍板：
/// 点节点先弹「当前→升级后 + 花费」对比卡，确认才买；底部一键升级弹一次确认连买到顶）。
/// <para>购买的实际执行体 DoBuyLeft 与旧 OnClickLeft 完全等价（TryUnlockLeft + 刷新 + toast），
/// 只是把「点击即买」拆成「确认后买」，供对比卡与一键升级共用，不碰购买规则/存档。</para>
/// 确认弹窗统一走 StaminaConfirmPopup（项目规矩，不手搓）。
/// </summary>
public partial class TalentUI
{
    // 【2026-11-19 主人拍板】点击对比卡改走参考图式独立卡片（TalentUI.LeftCard.cs 的 ConfirmBuyLeft），
    // 原「通用文字确认框 StaminaConfirmPopup.Show」版本已迁走，购买执行体 DoBuyLeft 保持原样。

    /// <summary>左列购买的实际执行体（原 OnClickLeft 的内容，去掉重复的日志）。供对比卡与一键升级共用。</summary>
    void DoBuyLeft(int index0)
    {
        if (!TalentSystem.TryUnlockLeft(index0 + 1, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            GlobalToastUI.Show(reason, true);
            return;
        }
        onLeftUnlockRequested?.Invoke(index0 + 1);
        RefreshAll();
        // 【2026-11-20】单点解锁粒子：只此一条调用，一键 DoBuyAll 不经过这里。
        if (index0 >= 0 && index0 < _leftViews.Count && _leftViews[index0] != null)
            PlayUnlockFx(_leftViews[index0].unlockFx);
        GlobalToastUI.Show(LeftUnlockToast(index0), true);
    }

    // ===== 一键升级（2026-11-19；2026-11-20 扩展左右两侧）=====
    // 按钮真源 = 预制体 Footer 的 shengji；点击绑定在 WireClicks（Interaction.cs）完成。
    // 【2026-11-20 主人拍板】一键升级含右列天赋石解锁/升级；预览与执行同口径，
    // 通用弹窗里金币、天赋石两笔消耗和买完余额都写清楚。
    void OnClickBuyAll()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[TalentUI] 一键升级读不到存档，已拒绝购买");
            GlobalToastUI.Show("没有存档，先开一把吧～", true);
            return;
        }
        var talents = data.talents;

        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        long previewGold = gold;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        int playerLv = LevelSystem.CurrentLevel;
        int leftCount = 0;
        for (int i = unlocked; i < TalentDefs.Left.Length; i++)
        {
            if (playerLv < TalentDefs.LeftNodeUnlockLevel(i + 1)) break;   // 等级门槛
            int c = TalentSystem.GetLeftGoldCost(TalentDefs.Left[i], talents);
            if (previewGold < c) break;                                    // 金币不足
            previewGold -= c;
            leftCount++;
        }

        int stone = data.talentPoints;
        int previewStone = stone;
        int rightCount = 0;
        string rightWhy = null;
        PreviewBuyAllRight(talents, ref previewStone, ref rightCount, ref rightWhy);

        if (leftCount <= 0 && rightCount <= 0)
        {
            string leftWhy = null;
            if (unlocked < TalentDefs.Left.Length)
                TalentSystem.CanUnlockLeft(unlocked + 1, out leftWhy);
            // 不按「先看左再看右」硬取：等级/互斥这类可行动原因优先于天赋石不足，
            // 「已满级 / 请先解锁」这类非行动项最后才用（原写法会拿遍历到的第一条误导玩家）。
            string why = PickBuyAllReason(leftWhy, rightWhy);
            if (string.IsNullOrEmpty(why)) why = "暂时没有能升的天赋～";
            GlobalToastUI.Show(why, true);
            return;
        }

        // 【2026-11-20 主人拍板「太繁琐，简化」】正文只留 2 行对称短句：
        // 左列解锁几个 / 右列解锁几个 / 各花多少。通用弹窗正文居中，底框随行数收紧。
        // 左列=金币、右列=天赋石，语义天然对齐；两行字数接近，居中后视觉对称。
        string body = "左列解锁 " + leftCount + " 个 · 花费金币 " + (gold - previewGold) + "\n"
                    + "右列解锁 " + rightCount + " 个 · 花费天赋石 " + (stone - previewStone);
        StaminaConfirmPopup.Show("一键升级", body, "确定升级", () => DoBuyAll());
    }

    /// <summary>
    /// 失败原因优先级（2026-11-20 自检修）：等级/互斥这类玩家能动手的原因优先，
    /// 天赋石不足次之，「已满级 / 请先解锁」这类非行动项最后才兜底。
    /// </summary>
    static int BuyAllReasonPriority(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return 0;
        if (reason.Contains("无存档")) return 9;
        if (reason.Contains("级后解锁") || reason.Contains("级后可升级")) return 6;
        if (reason.Contains("互斥")) return 5;
        if (reason == TalentSystem.ReasonNoStones) return 4;
        if (reason.Contains("满")) return 2;
        return 1;
    }

    static void NoteBuyAllReason(ref string why, string reason)
    {
        if (BuyAllReasonPriority(reason) > BuyAllReasonPriority(why)) why = reason;
    }

    static string PickBuyAllReason(string a, string b)
    {
        return BuyAllReasonPriority(a) >= BuyAllReasonPriority(b) ? a : b;
    }

    /// <summary>
    /// 右列预览：用克隆的 talents + 余额走 TalentSystem 的快照判定（真值同一份），
    /// 不碰存档。隐藏节点不参与；未选职业的 JobChoice 不替玩家做选择。
    /// </summary>
    static void PreviewBuyAllRight(Dictionary<string, int> talents, ref int stones, ref int count, ref string why)
    {
        // 【自检修】原来 stones<=0 直接写死「天赋石不足」就返回，右列全满时也报缺石头。
        // 现在只挡 talents=null，余额为 0 仍让每个节点走 Can* 拿自己的真实原因。
        if (talents == null)
        {
            NoteBuyAllReason(ref why, "无存档");
            return;
        }

        var previewTalents = new Dictionary<string, int>(talents);
        bool changed = true;
        int guard = 0;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
            {
                var node = TalentDefs.RightNodes[i];
                if (node == null || string.IsNullOrEmpty(node.id)) continue;
                if (TalentDefs.IsHiddenRightNode(node.id)) continue;

                int level = TalentDefs.GetRightNodeLevel(previewTalents, node.id);
                bool unlock = level <= 0;
                int chosenJob = TalentDefs.GetRightNodeChosenJob(previewTalents, node.id);
                if (unlock && node.IsJobChoice && chosenJob <= 0)
                {
                    NoteBuyAllReason(ref why, "职业选择天赋请单独点选");
                    continue;
                }

                bool can;
                string reason;
                if (unlock)
                    can = TalentSystem.CanUnlockRightNode(previewTalents, stones, node.id, out reason);
                else
                    can = TalentSystem.CanUpgradeRightNode(previewTalents, stones, node.id, out reason);
                if (!can)
                {
                    NoteBuyAllReason(ref why, reason);
                    continue;
                }

                int cost = unlock ? node.costByLevel[0] : node.costByLevel[level];
                stones -= cost;
                previewTalents[node.id] = unlock ? 1 : level + 1;
                count++;
                changed = true;
            }
            if (++guard > 256)
            {
                Debug.LogError("[TalentUI] 一键升级右列预览超过安全轮次，已停止");
                break;
            }
        }
    }

    /// <summary>一键升级按钮亮/灰共用：左列能买或右列能升都算亮。</summary>
    static bool HasBuyAllTarget(SaveData data)
    {
        if (data == null) return false;
        var talents = data.talents;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (unlocked < TalentDefs.Left.Length && TalentSystem.CanUnlockLeft(unlocked + 1, out _))
            return true;
        int stones = data.talentPoints;
        int count = 0;
        string why = null;
        PreviewBuyAllRight(talents, ref stones, ref count, ref why);
        return count > 0;
    }

    /// <summary>
    /// 【2026-11-28 主人「有两个及以上的天赋可升的时候在一键升级按钮上也加个红点」】
    /// 一键升级「现在能升几个」的计数：左列能连买的个数 + 右列能连升的次数。
    /// 口径与 <see cref="OnClickBuyAll"/> 的预览完全一致（金币/天赋石按顺序逐笔扣），不另写一套判定；
    /// 只有红点用它，亮/灰仍走 <see cref="HasBuyAllTarget"/>（那个还认「等级到了但钱不够」的解锁态）。
    /// </summary>
    static int CountBuyAllTargets(SaveData data)
    {
        if (data == null) return 0;
        var talents = data.talents;

        int leftCount = 0;
        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        int playerLv = LevelSystem.CurrentLevel;
        for (int i = unlocked; i < TalentDefs.Left.Length; i++)
        {
            if (playerLv < TalentDefs.LeftNodeUnlockLevel(i + 1)) break;
            int c = TalentSystem.GetLeftGoldCost(TalentDefs.Left[i], talents);
            if (gold < c) break;
            gold -= c;
            leftCount++;
        }

        int stones = data.talentPoints;
        int rightCount = 0;
        string why = null;
        PreviewBuyAllRight(talents, ref stones, ref rightCount, ref why);
        return leftCount + rightCount;
    }

    /// <summary>
    /// 连买的实际执行：左列复用 TryUnlockLeft，右列复用 TryUnlockRightNode / TryUpgradeRightNode；
    /// 顺序与预览一致（左列 → 右列逐节点多轮），遇到门槛/互斥/货币不足自然停；最后统一刷新 + 汇总 toast。
    /// </summary>
    void DoBuyAll()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[TalentUI] 一键升级执行时读不到存档，已拒绝购买");
            GlobalToastUI.Show("没有存档，先开一把吧～", true);
            return;
        }
        var talents = data.talents;

        int leftBought = 0;
        int start = TalentDefs.LeftUnlockedCount(talents);
        for (int i = start; i < TalentDefs.Left.Length; i++)
        {
            if (!TalentSystem.CanUnlockLeft(i + 1, out _)) break;
            int c = TalentSystem.GetLeftGoldCost(TalentDefs.Left[i], talents);
            long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            if (gold < c) break;
            if (!TalentSystem.TryUnlockLeft(i + 1, out _)) break;
            leftBought++;
            onLeftUnlockRequested?.Invoke(i + 1);
        }

        int rightBought = 0;
        bool changed = true;
        int guard = 0;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
            {
                var node = TalentDefs.RightNodes[i];
                if (node == null || string.IsNullOrEmpty(node.id)) continue;
                if (TalentDefs.IsHiddenRightNode(node.id)) continue;

                int level = TalentDefs.GetRightNodeLevel(talents, node.id);
                bool unlock = level <= 0;
                int chosenJob = TalentDefs.GetRightNodeChosenJob(talents, node.id);
                if (unlock && node.IsJobChoice && chosenJob <= 0) continue;   // 不替玩家选职业

                bool can;
                string reason;
                if (unlock)
                    can = TalentSystem.CanUnlockRightNode(node.id, out reason);
                else
                    can = TalentSystem.CanUpgradeRightNode(node.id, out reason);
                if (!can) continue;   // 等级/互斥/满级/货币不够：正常终止，不刷日志

                bool ok;
                if (unlock)
                    ok = TalentSystem.TryUnlockRightNode(node.id, chosenJob, out reason);
                else
                    ok = TalentSystem.TryUpgradeRightNode(node.id, out reason);
                if (!ok)
                {
                    Debug.LogError("[TalentUI] 一键升级右列 " + node.id + " 判定可买但执行失败：" + reason);
                    continue;
                }
                rightBought++;
                changed = true;
            }
            if (++guard > 256)
            {
                Debug.LogError("[TalentUI] 一键升级右列执行超过安全轮次，已停止");
                break;
            }
        }

        if (leftBought > 0 || rightBought > 0)
        {
            RefreshAll();
            GlobalToastUI.Show("一键升级完成：左列 " + leftBought + " 个，右列 " + rightBought + " 次！", true);
        }
    }

}
