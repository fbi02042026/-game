using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-10-24 主人拍板】开发期福利邮件：默认每次启动都发，正式打包自动关闭。
///
/// · 发什么（每样一封分开入邮，共 9 封）：
///   默认 3 佣兵（真源 <see cref="SaveData.DefaultUnlockedMercIds"/>：路加 H011/马库斯 H001/米娅 H005）
///   碎片×80 + 塔克（真源 <see cref="DailyLoginDefs.STARTER_MERC_ID"/> H003）碎片×80
///   + 普通招募卷轴×10 + 高级招募卷轴×10 + 钻石×1000 + 天赋石×1000 + 金币×9999。
/// · 什么时候发：每次进程启动进主界面发一轮（_sentThisLaunch 进程级只发一次，
///   下次启动再发一轮，存档里会持续累积——这就是主人要的「每次都发」）。
/// · 正式包怎么关：Enabled = Debug.isDebugBuild —— 编辑器 / 勾了 Development Build
///   的测试包恒 true；Build Settings 不勾 Development Build 出的正式包恒 false，
///   打正式包不用改任何代码。唯一开关出口，不开第二个。
/// </summary>
public static class DevGiftMail
{
    // —— 数量（主人 2026-10-24 拍板，就这一处）——
    const int FragAmount = 80;
    const int ScrollAmount = 10;
    const int DiamondAmount = 1000;
    const int TalentStoneAmount = 1000;
    const int GoldAmount = 9999;
    const int BadgeAmount = 90;
    /// <summary>主人 2026-10-24 拍板：一封邮件最多 4 种附件（徽记 18 种按此拆 5 封）。</summary>
    const int MaxBadgeKindsPerMail = 4;

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

        int sent = 0;
        // 默认 3 佣兵碎片（直接引用真源名单，不复制 ID）
        var ids = SaveData.DefaultUnlockedMercIds;
        for (int i = 0; i < ids.Length; i++)
            if (SendFragmentsMail(ids[i])) sent++;
        // 塔克碎片（真源 DailyLoginDefs.STARTER_MERC_ID）
        if (SendFragmentsMail(DailyLoginDefs.STARTER_MERC_ID)) sent++;
        // 两种卷轴
        if (SendScrollMail("scrollCommon", "普通招募卷轴")) sent++;
        if (SendScrollMail("scrollLegendary", "高级招募卷轴")) sent++;
        // 三样资源（flat 字段：领取走 MailSystem.TryClaim 的 ClaimOne，图标走 MailDetailUI.AppendFlatRewardItems）
        if (SendResourceMail($"【开发福利】钻石×{DiamondAmount}", "开发测试福利：钻石×1000。", m => m.diamond = DiamondAmount)) sent++;
        if (SendResourceMail($"【开发福利】天赋石×{TalentStoneAmount}", "开发测试福利：天赋石×1000。", m => m.talentPoints = TalentStoneAmount)) sent++;
        if (SendResourceMail($"【开发福利】金币×{GoldAmount}", "开发测试福利：金币×9999。", m => m.gold = GoldAmount)) sent++;
        // 全职业全档位徽记（6 职业 × 普通/稀有/传奇 = 18 种 ×90，每封最多 4 种 → 5 封）
        sent += SendBadgeMails();

        // 9 封只弹一次「您有新的邮件」（Toast 显示中再调会被忽略，见 NewMailToast.Notify）
        if (sent > 0) NewMailToast.Notify();
        Debug.Log($"[DevGift] 开发福利邮件已发送 {sent} 封（isDebugBuild={Enabled}）");
    }

    /// <summary>佣兵本命碎片福利邮件（附件 mercfrag，领取走 MercGrowInventory.GrantFragments 统一入口）。</summary>
    static bool SendFragmentsMail(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        string mercName = MercRosterDefs.TryGetByHireId(hireId, out var def) && !string.IsNullOrEmpty(def.Name)
            ? def.Name : hireId;
        var mail = NewDevMail(
            $"【开发福利】{mercName}碎片×{FragAmount}",
            $"开发测试福利：{mercName}的本命碎片×{FragAmount}。\n此邮件仅开发/测试版本发放。");
        mail.attachments = new List<MailAttachment>
        {
            new MailAttachment
            {
                type = "mercfrag",
                amount = FragAmount,
                hireId = hireId,
                iconOverride = ContentPaths.Icons.MercHead + "/" + hireId,   // 口径同 MailSystem.EnqueueMercFragments
            },
        };
        MailSystem.Enqueue(mail, notifyToast: false);
        return true;
    }

    /// <summary>招募卷轴福利邮件（scrollCommon / scrollLegendary，领取直接加存档计数，无上限概念）。</summary>
    static bool SendScrollMail(string attType, string displayName)
    {
        if (string.IsNullOrEmpty(attType)) return false;
        var mail = NewDevMail(
            $"【开发福利】{displayName}×{ScrollAmount}",
            $"开发测试福利：{displayName}×{ScrollAmount}。\n此邮件仅开发/测试版本发放。");
        mail.attachments = new List<MailAttachment>
        {
            new MailAttachment
            {
                type = attType,
                amount = ScrollAmount,
                iconOverride = "Art/UI/Icons/Common/icon_hire_scroll",   // 口径同 MailSystem.EnsureCompensationMail
            },
        };
        MailSystem.Enqueue(mail, notifyToast: false);
        return true;
    }

    /// <summary>
    /// 职业徽记福利邮件（2026-10-24 主人拍板）：6 职业（真源 MercGrowInventory.BadgeJobKeys）
    /// × 3 档位（普通/稀有/传奇，真源 MercStarDefs.BadgeTierOf）= 18 种各 ×90，
    /// 每封最多 MaxBadgeKindsPerMail 种附件拆开发。领取走 MailSystem 的 badge 分支 → MercGrowInventory.Add。
    /// 返回发出的封数。
    /// </summary>
    static int SendBadgeMails()
    {
        var rarities = new[]
        {
            MercRosterDefs.MercRarity.Common,
            MercRosterDefs.MercRarity.Rare,
            MercRosterDefs.MercRarity.Legendary,
        };
        var all = new List<MailAttachment>();
        for (int j = 0; j < MercGrowInventory.BadgeJobKeys.Length; j++)
        {
            string job = MercGrowInventory.BadgeJobKeys[j];
            for (int r = 0; r < rarities.Length; r++)
            {
                string tier = MercStarDefs.BadgeTierOf(rarities[r]);
                all.Add(new MailAttachment
                {
                    type = "badge",
                    amount = BadgeAmount,
                    defId = MercGrowInventory.BadgeId(job, tier),
                    iconOverride = ContentPaths.Icons.JobBadge + "/" + job + tier,   // 口径同 MercGrowSprites.LoadBadge 的 Resources 路径
                });
            }
        }

        int mails = 0;
        for (int i = 0; i < all.Count; i += MaxBadgeKindsPerMail)
        {
            int n = Mathf.Min(MaxBadgeKindsPerMail, all.Count - i);
            var chunk = all.GetRange(i, n);
            // 正文按本封实际内容列清单（名×数量）
            var names = new List<string>();
            for (int k = 0; k < chunk.Count; k++)
                names.Add(BadgeAttName(chunk[k].defId) + "×" + BadgeAmount);
            var mail = NewDevMail(
                $"【开发福利】职业徽记礼包（{mails + 1}）",
                "开发测试福利：" + string.Join("、", names) + "。\n此邮件仅开发/测试版本发放。");
            mail.attachments = chunk;
            MailSystem.Enqueue(mail, notifyToast: false);
            mails++;
        }
        return mails;
    }

    /// <summary>badge:{职业}:{档位} → 「剑盾徽记·普通」（口径同 MailDetailUI.BadgeDisplayName）。</summary>
    static string BadgeAttName(string badgeId)
    {
        if (string.IsNullOrEmpty(badgeId)) return "职业徽记";
        const string prefix = "badge:";
        if (!badgeId.StartsWith(prefix)) return "职业徽记";
        var parts = badgeId.Substring(prefix.Length).Split(':');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0])) return "职业徽记";
        return parts[0] + "徽记·" + parts[1];
    }

    /// <summary>资源福利邮件（flat 字段：gold / diamond / talentPoints）。</summary>
    static bool SendResourceMail(string title, string body, Action<MailEntry> fill)
    {
        if (fill == null) return false;
        var mail = NewDevMail(title, body + "\n此邮件仅开发/测试版本发放。");
        fill(mail);
        MailSystem.Enqueue(mail, notifyToast: false);
        return true;
    }

    static MailEntry NewDevMail(string title, string body)
    {
        return new MailEntry
        {
            id = Guid.NewGuid().ToString("N"),
            title = title,
            sender = "开发组",
            body = body,
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
        };
    }
}
