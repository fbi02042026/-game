using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-10-24 主人拍板·本版口径】开发期福利邮件收敛为一封「测试用」邮件。
///
/// · 发什么（一封）：默认 3 佣兵（真源 <see cref="SaveData.DefaultUnlockedMercIds"/>：路加 H011/马库斯 H001/米娅 H005）
///   本命碎片各×10 + 钻石×200（钻石同为附件，不写 flat 字段），标题「测试用」。
/// · 先清后发：每次发送前把收件箱里旧的「开发组」测试邮件全部删掉
///   （欢迎信 / 补偿礼包是 MailSystem 正式链路发的，不在清理范围），
///   保证收件箱 = 原来两封正式邮件 + 至多一封未领「测试用」。
/// · 什么时候发：每次进程启动进主界面发一轮（_sentThisLaunch 进程级只发一次）。
/// · 正式包怎么关：Enabled = Debug.isDebugBuild —— 编辑器 / 勾了 Development Build
///   的测试包恒 true；不勾 Development Build 的正式包恒 false，不用改任何代码。唯一开关出口。
/// </summary>
public static class DevGiftMail
{
    // —— 数量（主人 2026-10-24 拍板，就这一处）——
    const int FragAmount = 10;
    const int DiamondAmount = 200;

    /// <summary>测试邮件发件人标识，同时也是清理旧测试邮件的判据（旧【开发福利】邮件同为此发件人）。</summary>
    const string SenderId = "开发组";

    /// <summary>唯一开关出口：编辑器 / Development Build = 发；正式包（不勾 Development Build）= 不发。</summary>
    static bool Enabled => Debug.isDebugBuild;

    static bool _sentThisLaunch;

    /// <summary>每次进主界面调用（TownSceneBootstrap 唯一调用点）；每个进程最多发一轮。</summary>
    public static void TrySendOncePerLaunch()
    {
        if (_sentThisLaunch) return;
        if (SaveSystem.Instance?.Data == null) return;   // 存档未就绪：下一轮 BootstrapRoutine 再判，不兜底不发
        _sentThisLaunch = true;
        if (!Enabled) return;

        PurgeDevMails();
        SendTestMail();

        NewMailToast.Notify();
        Debug.Log($"[DevGift] 测试邮件已发送 1 封（isDebugBuild={Enabled}）");
    }

    /// <summary>
    /// 清掉收件箱里所有旧测试邮件（发件人 == <see cref="SenderId"/>，即历史【开发福利】各封），
    /// 只保留欢迎信 / 补偿礼包等正式邮件。逐封倒序移除后统一落盘 + 刷新红点/列表。
    /// </summary>
    static void PurgeDevMails()
    {
        var list = SaveSystem.Instance.Data.mailInbox;
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (m == null || m.sender != SenderId) continue;
            list.RemoveAt(i);
            removed++;
        }
        if (removed > 0)
        {
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
            MailListUI.NotifyInboxChanged();   // 邮箱开着也能实时看到旧测试邮件被清掉
        }
        if (removed > 0) Debug.Log($"[DevGift] 已清理旧测试邮件 {removed} 封");
    }

    /// <summary>「测试用」福利邮件：默认 3 佣兵本命碎片各×10（mercfrag 附件，领取走 MercGrowInventory.GrantFragments）+ 钻石×200（diamond 附件，领取走 ClaimOne）。</summary>
    static void SendTestMail()
    {
        var mail = new MailEntry
        {
            id = Guid.NewGuid().ToString("N"),
            title = "测试用",
            sender = SenderId,
            body = $"测试用邮件：默认三佣兵本命碎片各×{FragAmount}、钻石×{DiamondAmount}。\n此邮件仅开发/测试版本发放。",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
        };
        var ids = SaveData.DefaultUnlockedMercIds;
        mail.attachments = new List<MailAttachment>(ids.Length + 1);
        for (int i = 0; i < ids.Length; i++)
        {
            mail.attachments.Add(new MailAttachment
            {
                type = "mercfrag",
                amount = FragAmount,
                hireId = ids[i],
                iconOverride = ContentPaths.Icons.MercHead + "/" + ids[i],   // 口径同 MailSystem.EnqueueMercFragments
            });
        }
        // 钻石也走附件（主人 2026-10-24 拍板）：类型 key 同 MailSystem.AttachmentResourceType，图标口径同 MailDetailUI.AppendFlatRewardItems
        mail.attachments.Add(new MailAttachment
        {
            type = "diamond",
            amount = DiamondAmount,
            iconOverride = "UI/Icons/Common/icon_diamond_blue",
        });
        MailSystem.Enqueue(mail, notifyToast: false);
    }
}
