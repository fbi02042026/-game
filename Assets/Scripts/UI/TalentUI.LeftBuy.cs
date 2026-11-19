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
        GlobalToastUI.Show(LeftUnlockToast(index0), true);
    }

    // ===== 一键升级（2026-11-19）=====
    // 按钮真源 = 预制体 Footer 的原「重置天赋」按钮位（ResetButton / ResetButton灰，Label 文案已改「一键升级」），
    // 排版主人在 Inspector 里改；点击绑定在 WireClicks（Interaction.cs）完成，本文件只留行为逻辑。
    void OnClickBuyAll()
    {
        var data = SaveSystem.Instance?.Data;
        var talents = data != null ? data.talents : null;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (unlocked >= TalentDefs.Left.Length)
        {
            GlobalToastUI.Show("左列天赋已经点满啦～", true);
            return;
        }

        // 前瞻：按当前金币算能连买几个（碰到等级门槛/金币不足就停），只用于确认文案预览。
        // 【2026-11-19 修】不能再调 CanUnlockLeft(i+1)：它强制「必须是当前下一个节点」，
        // 预览阶段没真正购买、存档 unlocked 不变，从第 2 个起必然返回「请按顺序解锁」而 break，
        // 导致确认框永远显示「1 个」、实际 DoBuyAll 却连买多个（玩家花的钱和预期不符）。
        // 改只校验两个真实终止条件：人物等级门槛 + 金币，与实际连买 DoBuyAll 完全同口径。
        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        long previewGold = gold;
        int playerLv = LevelSystem.CurrentLevel;
        int previewCount = 0;
        for (int i = unlocked; i < TalentDefs.Left.Length; i++)
        {
            if (playerLv < TalentDefs.LeftNodeUnlockLevel(i + 1)) break;   // 等级门槛
            int c = TalentSystem.GetLeftGoldCost(TalentDefs.Left[i], talents);
            if (previewGold < c) break;                                    // 金币不足
            previewGold -= c;
            previewCount++;
        }
        if (previewCount <= 0)
        {
            string why;
            TalentSystem.CanUnlockLeft(unlocked + 1, out why);
            GlobalToastUI.Show(string.IsNullOrEmpty(why) ? "金币不够啦～再攒攒！" : why, true);
            return;
        }

        string body = "一键购买 " + previewCount + " 个天赋\n"
                    + "预计消耗金币：" + (gold - previewGold) + "\n"
                    + "（持有 " + gold + "，买完剩 " + previewGold + "）";
        StaminaConfirmPopup.Show("一键升级", body, "确定购买", () => DoBuyAll());
    }

    /// <summary>连买的实际执行：复用 TryUnlockLeft，遇到等级门槛/金币不足自然停；最后统一刷新 + 汇总 toast。</summary>
    void DoBuyAll()
    {
        var data = SaveSystem.Instance?.Data;
        var talents = data != null ? data.talents : null;
        int start = TalentDefs.LeftUnlockedCount(talents);
        int bought = 0;
        for (int i = start; i < TalentDefs.Left.Length; i++)
        {
            if (!TalentSystem.CanUnlockLeft(i + 1, out _)) break;
            int c = TalentSystem.GetLeftGoldCost(TalentDefs.Left[i], talents);
            long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            if (gold < c) break;
            if (!TalentSystem.TryUnlockLeft(i + 1, out _)) break;
            bought++;
            onLeftUnlockRequested?.Invoke(i + 1);
        }
        if (bought > 0)
        {
            RefreshAll();
            GlobalToastUI.Show("一口气点了 " + bought + " 个天赋，实力大增啦！", true);
        }
    }

}
