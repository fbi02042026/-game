using System;
using UnityEngine;

/// <summary>
/// 【2026-10-24 主人拍板】所有花费钻石的地方都要二次确认 —— 确认门唯一出口。
/// 所有 UI 入口在扣钻石之前必须先调 <see cref="Request"/>：
/// 余额不足 → 直接 Toast「钻石不足」，不弹窗（点了也买不成，少一次空点击）；
/// 充足 → 走通用双按钮确认弹窗 StaminaConfirmPopup（硬规则：确认框一律走它，不新建弹窗皮），
/// 正文自动追加「花费钻石 ×N（持有 X → 剩余 Y）」；玩家点确认后才执行 onConfirmed（回调里再扣钻）。
/// 底层配套：ResourceWallet.TrySpendDiamond 是钻石扣费唯一出口，TrySpend(Diamond) 已 fail closed
/// （直接调会 LogError 并拒绝扣费），两头一起堵，以后新代码绕不过确认。
/// </summary>
public static class DiamondSpendGate
{
    /// <summary>
    /// 弹钻石消费二次确认。
    /// title 弹窗标题；body 业务正文（效果 / 商品说明，本方法会在末尾追加花费行）；
    /// cost 本次花费钻石数（必须 &gt; 0）；onConfirmed 玩家点「确认」后的回调（扣费与发货都在回调里做）；
    /// okLabel 确认键文字，缺省「确认」。
    /// </summary>
    public static void Request(string title, string body, long cost, Action onConfirmed, string okLabel = "确认")
    {
        if (cost <= 0 || onConfirmed == null)
        {
            // fail closed：无效调用喊出来，不静默执行
            Debug.LogError($"[DiamondSpendGate] 无效调用 cost={cost} onConfirmed={(onConfirmed == null ? "null" : "ok")}");
            return;
        }

        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            // 不静默兜底：没有存档就校验不了余额，必须喊出来
            Debug.LogError("[DiamondSpendGate] SaveData 为空，无法校验钻石余额");
            return;
        }

        long have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
        if (have < cost)
        {
            // 【2026-10-24 主人拍板】不足时直接告知缺口，不弹确认框
            GlobalToastUI.Show($"钻石不足（持有 {have} / 需要 {cost}）");
            return;
        }

        string fullBody = body + $"\n\n花费钻石 ×{cost}（持有 {have} → 剩余 {have - cost}）";
        StaminaConfirmPopup.Show(title, fullBody, okLabel, onConfirmed);
    }
}
