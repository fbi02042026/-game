#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 测试档专用：把「已领取」的固定 id 幂等邮件重置成可再领一次。
/// 只走既有链路（SaveSystem / MailSystem.Ensure*），不改任何玩法逻辑；Assets/Editor 下不进包。
/// 用法：Tools → 邮件 → 重置为可领取（测试档），在 Play 模式下点一次即可立即生效。
/// </summary>
public static class MailResetTool
{
    // 与 MailSystem 里固定 id 幂等邮件一一对应；随机 id 的溢出邮件一律不动。
    static readonly HashSet<string> FixedIds = new HashSet<string>
    {
        "welcome_2026_v3",
        "compensation_2026_10_v1",
        "starup_test_mail_v2_H011",
        "starup_test_mail_v2_H001",
        "starup_test_mail_v2_H005",
        "starup_test_mail_v2_H013",
        "starup_test_mail_v2_H017",
        "starup_test_mail_v2_fund",
    };

    [MenuItem("Tools/邮件/重置为可领取（测试档）", false, 300)]
    public static void ResetClaimedMails()
    {
        if (!EditorApplication.isPlaying || SaveSystem.Instance == null || SaveSystem.Instance.Data == null)
        {
            EditorUtility.DisplayDialog("邮件重置",
                "请先进入 Play 模式、等存档就绪后再点本菜单。\n本工具改的是运行中的存档，点完立即生效。", "确定");
            return;
        }

        var data = SaveSystem.Instance.Data;
        data.mailInbox ??= new List<MailEntry>();
        int removed = Purge(data.mailInbox);
        SaveSystem.Instance.Save();

        // 按固定 id 幂等重发：奖励走真源重算，内部已落盘 + 刷红点 + 通知列表
        MailSystem.EnsureWelcomeMail(notifyToast: false);
        MailSystem.EnsureCompensationMail(notifyToast: false);
        MailSystem.EnsureStarUpTestMail(notifyToast: false);
        RedDot.RefreshCommon();
        MailListUI.NotifyInboxChanged();

        int unclaimed = MailSystem.UnclaimedCount();
        Debug.Log($"[MailReset] 清掉 {removed} 封已领空壳并重发，当前未领={unclaimed}");
        EditorUtility.DisplayDialog("邮件重置",
            $"已清掉 {removed} 封已领邮件并重新发放。\n现在未领取 {unclaimed} 封，可直接领。", "确定");
    }

    static int Purge(List<MailEntry> inbox)
    {
        int removed = 0;
        for (int i = inbox.Count - 1; i >= 0; i--)
        {
            var m = inbox[i];
            if (m == null || !m.claimed || !FixedIds.Contains(m.id)) continue;
            inbox.RemoveAt(i);
            removed++;
        }
        return removed;
    }
}
#endif
