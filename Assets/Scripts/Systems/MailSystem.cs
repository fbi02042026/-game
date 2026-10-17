using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简易邮件：资源溢出等奖励暂存，领取时再走 ResourceWallet。
/// </summary>
public static class MailSystem
{
    public static IReadOnlyList<MailEntry> GetInbox()
    {
        Ensure();
        return SaveSystem.Instance.Data.mailInbox;
    }

    public static int UnclaimedCount()
    {
        Ensure();
        int n = 0;
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && !list[i].claimed) n++;
        return n;
    }

    public static void EnqueueResourceOverflow(ResourceWallet.ResourceType type, long amount)
    {
        if (amount <= 0) return;
        Ensure();
        var mail = new MailEntry
        {
            id = Guid.NewGuid().ToString("N"),
            title = $"{ResourceWallet.DisplayName(type)}已满·溢出补偿",
            body = $"因{ResourceWallet.DisplayName(type)}已达上限，溢出部分已存入邮件，领取后仍受上限限制。",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            claimed = false
        };
        ApplyAmount(mail, type, amount);
        SaveSystem.Instance.Data.mailInbox.Add(mail);
        SaveSystem.Instance.Save();
        Debug.Log($"[Mail] 溢出入邮 {type}+{amount}，未领={UnclaimedCount()}");
        RedDot.RefreshCommon();
    }

    public static bool TryClaim(string mailId, bool notify = true)
    {
        Ensure();
        var list = SaveSystem.Instance.Data.mailInbox;
        MailEntry mail = null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == mailId)
            {
                mail = list[i];
                break;
            }
        }
        if (mail == null || mail.claimed) return false;

        // 领取时仍走上限：装不下的继续留在邮件
        long leftGold = ClaimOne(ResourceWallet.ResourceType.Gold, mail.gold);
        long leftDiamond = ClaimOne(ResourceWallet.ResourceType.Diamond, mail.diamond);
        long leftStamina = ClaimOne(ResourceWallet.ResourceType.Stamina, mail.stamina);
        long leftEnchant = ClaimOne(ResourceWallet.ResourceType.EnchantStone, mail.enchantStones);
        long leftMats = ClaimOne(ResourceWallet.ResourceType.DecomposeMat, mail.decomposeMats);
        long leftTalent = ClaimOne(ResourceWallet.ResourceType.TalentPoint, mail.talentPoints);

        // 结构化附件（2026-10-16 邮件 UI 改造）：同样走上限，装不下的留在附件
        if (mail.attachments != null)
        {
            for (int i = 0; i < mail.attachments.Count; i++)
            {
                var att = mail.attachments[i];
                if (att == null || att.amount <= 0) continue;
                if (att.type == "merc")   // 🆕 佣兵奖励：解锁（并尽量进抽奖池），不占资源上限
                {
                    GrantMerc(att.hireId);
                    att.amount = 0;
                    continue;
                }
                var rt = AttachmentResourceType(att.type);
                if (rt == null) continue;   // 未知类型不吞不报，留给 UI 展示
                att.amount = ClaimOne(rt.Value, att.amount);
            }
        }

        mail.gold = leftGold;
        mail.diamond = (int)leftDiamond;
        mail.stamina = (int)leftStamina;
        mail.enchantStones = (int)leftEnchant;
        mail.decomposeMats = (int)leftMats;
        mail.talentPoints = (int)leftTalent;

        bool flatEmpty = mail.gold <= 0 && mail.diamond <= 0 && mail.stamina <= 0
                     && mail.enchantStones <= 0 && mail.decomposeMats <= 0 && mail.talentPoints <= 0;
        bool empty = flatEmpty && AttachmentsEmpty(mail);
        if (empty)
        {
            mail.claimed = true;
            if (notify) UIManager.Instance?.ShowToast("邮件已领取");
        }
        else if (notify)
            UIManager.Instance?.ShowToast("部分资源仍达上限，已留在邮件");

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return empty;
    }

    static long ClaimOne(ResourceWallet.ResourceType type, long amount)
    {
        if (amount <= 0) return 0;
        // 领取不重复入邮，避免死循环
        var r = ResourceWallet.Add(type, amount, save: false, notify: false, overflowToMail: false);
        return amount - r.added;
    }

    /// <summary>🆕 发放佣兵奖励（type=="merc"）：解锁到花名册并尽量补进战斗抽奖池，保证玩家可招募；不占资源上限。</summary>
    static void GrantMerc(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        var d = SaveSystem.Instance?.Data;
        if (d == null) return;
        d.UnlockMerc(hireId);
        d.EnsurePoolFilled();   // 解锁后自动补全抽奖池（主人拍板「池子最少要补全」），让礼物立刻可招募
        Debug.Log($"[Mail] 邮件发放佣兵 {hireId}");
    }

    // ── 2026-10-16 邮件 UI 改造新增（T2）─────────────────────────────────

    /// <summary>富邮件入队（调用方自行构造 MailEntry：sender/type/detailImagePath/attachments）。
    /// 【2026-10-16 主人拍板】notifyToast=false 时只入邮不弹提示，由调用方择机弹
    /// （引导收尾链：欢迎信 → 弹登录 → 玩家关登录 → DailyLoginUI.Hide 里才冲刷提示）。</summary>
    public static void Enqueue(MailEntry mail, bool notifyToast = true)
    {
        if (mail == null) return;
        if (string.IsNullOrEmpty(mail.id)) mail.id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(mail.sender)) mail.sender = "系统";
        Ensure();
        SaveSystem.Instance.Data.mailInbox.Add(mail);
        SaveSystem.Instance.Save();
        Debug.Log($"[Mail] 富邮件入邮 {mail.title}，未领={UnclaimedCount()}");
        RedDot.RefreshCommon();
        if (notifyToast)
            NewMailToast.Notify();   // 🆕 新邮件：右侧滑入黑底白字提示
        else
            NewMailToast.NotifyLater();   // 只入邮不弹：登记待弹，等登录界面关闭时冲刷
    }

    /// <summary>
    /// 首登欢迎邮件（给玩家的正式/测试信）：含 200 金币 + 一位伙伴「莉娜(H017)」+ 致谢与加群邀请。
    /// 配图走表情包「高兴」（2026-10-16 主人原话：「表情用表情包里的图片 用高兴的那个」——不用佣兵头像顶表情位）。
    /// 用固定 id 去重，整份存档只发一次；要重新发放（改文案/加奖励/换配图）只需把 id 升版（如 welcome_2026_v3）。
    /// 触发点：① 玩完新手引导（StoryProgress.MarkTutorialDone，notifyToast=false：提示延后到关登录时）；
    /// ② 已通关老玩家回主界面补发（TownSceneBootstrap，幂等）。
    /// </summary>
    public static void EnsureWelcomeMail(bool notifyToast = true)
    {
        Ensure();
        const string WelcomeId = "welcome_2026_v2";
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].id == WelcomeId) return;   // 已发过，不重复

        Enqueue(new MailEntry
        {
            id = WelcomeId,
            title = "欢迎来到《像素冒险：裂隙之刃》！",
            sender = "制作组",
            body = "感谢您下载并试玩《像素冒险：裂隙之刃》！\n\n" +
                   "这是制作组的一点小心意——200 金币与一位伙伴「莉娜」请收下，祝您冒险顺利。\n\n" +
                   "我们是个小团队，正努力把游戏做得更好玩。如果您在游戏里有任何想法、吐槽或建议，" +
                   "非常欢迎加入玩家交流群（QQ 群号 1126152907），和我们一起聊游戏制作。\n\n" +
                   "我们一定会认真听取大家合理的建议，让游戏变得更有趣。再次感谢您的支持！\n\n" +
                   "—— 像素冒险制作组",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
            detailImagePath = "Art/UI/Stickers/高兴",   // 详情页表情：表情包「高兴」（2026-10-16 主人拍板，路径同 MailDetailUI.StickerDir 口径）
            gold = 200,
            attachments = new List<MailAttachment>
            {
                new MailAttachment
                {
                    type = "merc",
                    amount = 1,
                    hireId = "H017",
                    iconOverride = ContentPaths.Icons.MercHead + "/H017",
                },
            },
        }, notifyToast);
    }

    /// <summary>打开详情即记已读（只标记，不动领取状态）。</summary>
    public static void MarkAsRead(string mailId)
    {
        Ensure();
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.id != mailId || m.read) continue;
            m.read = true;
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
            return;
        }
    }

    /// <summary>
    /// 删除已读：公告类已读即可删；奖励类必须已领取（claimed，此时 flat 字段与附件均已清空）才可删。
    /// 未领取的奖励邮件即使已读也不删，防误删奖励。
    /// </summary>
    public static int DeleteRead()
    {
        Ensure();
        var list = SaveSystem.Instance.Data.mailInbox;
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (m == null || !m.read) continue;
            bool deletable = m.type == MailType.Announcement || m.claimed;
            if (!deletable) continue;
            list.RemoveAt(i);
            removed++;
        }
        if (removed > 0)
        {
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
        }
        return removed;
    }

    /// <summary>一键领取所有未领邮件，返回实际领完的封数（TryClaim 内部已落盘+刷新红点）。</summary>
    public static int ClaimAll()
    {
        Ensure();
        var list = SaveSystem.Instance.Data.mailInbox;
        int claimed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.claimed) continue;
            if (TryClaim(m.id, notify: false)) claimed++;
        }
        if (claimed > 0) RedDot.RefreshCommon();
        return claimed;
    }

    /// <summary>附件内部 key → 资源类型映射；未知 key 返回 null（不吞不报）。</summary>
    static ResourceWallet.ResourceType? AttachmentResourceType(string key)
    {
        switch (key)
        {
            case "gold": return ResourceWallet.ResourceType.Gold;
            case "diamond": return ResourceWallet.ResourceType.Diamond;
            case "stamina": return ResourceWallet.ResourceType.Stamina;
            case "enchant": return ResourceWallet.ResourceType.EnchantStone;
            case "mat": return ResourceWallet.ResourceType.DecomposeMat;
            case "talent": return ResourceWallet.ResourceType.TalentPoint;
            default: return null;
        }
    }

    /// <summary>附件是否已全部领空（null/空列表视为空）。</summary>
    static bool AttachmentsEmpty(MailEntry mail)
    {
        if (mail.attachments == null) return true;
        for (int i = 0; i < mail.attachments.Count; i++)
            if (mail.attachments[i] != null && mail.attachments[i].amount > 0) return false;
        return true;
    }

    static void ApplyAmount(MailEntry mail, ResourceWallet.ResourceType type, long amount)
    {
        switch (type)
        {
            case ResourceWallet.ResourceType.Gold: mail.gold += amount; break;
            case ResourceWallet.ResourceType.Diamond: mail.diamond += (int)Mathf.Min(amount, int.MaxValue); break;
            case ResourceWallet.ResourceType.Stamina: mail.stamina += (int)Mathf.Min(amount, int.MaxValue); break;
            case ResourceWallet.ResourceType.EnchantStone: mail.enchantStones += (int)Mathf.Min(amount, int.MaxValue); break;
            case ResourceWallet.ResourceType.DecomposeMat: mail.decomposeMats += (int)Mathf.Min(amount, int.MaxValue); break;
            case ResourceWallet.ResourceType.TalentPoint: mail.talentPoints += (int)Mathf.Min(amount, int.MaxValue); break;
        }
    }

    static void Ensure()
    {
        if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null)
            throw new InvalidOperationException("SaveSystem 未就绪");
        SaveSystem.Instance.Data.mailInbox ??= new List<MailEntry>();
    }
}

/// <summary>邮件类型（2026-10-16 邮件 UI 改造）。Reward=0 为默认值，旧溢出邮件自动归奖励类。</summary>
public enum MailType
{
    Reward = 0,        // 奖励邮件：可领取
    Announcement = 1,  // 公告：无附件，已读即可删
}

[Serializable]
public class MailEntry
{
    public string id;
    public string title;
    public string body;
    public long createTime;
    public bool claimed;
    public long gold;
    public int diamond;
    public int stamina;
    public int enchantStones;
    public int decomposeMats;
    public int talentPoints;

    // ── 2026-10-16 邮件 UI 改造新增（T1）：旧档读出走默认值，兼容 ──
    public bool read;                        // 是否已读（打开过详情）
    public string sender;                    // 发件人（空则 UI 显示「系统」）
    public MailType type = MailType.Reward;  // 邮件类型
    public string detailImagePath;           // 详情配图（Resources 路径，可空）
    public List<MailAttachment> attachments; // 结构化附件（可空；与 flat 字段并存）
}

[Serializable]
public class MailAttachment
{
    public string type;         // 内部 key：gold/diamond/stamina/enchant/mat/talent/merc
    public long amount;
    public string iconOverride; // 图标 Resources 路径覆盖（可空）
    public string hireId;       // 🆕 type=="merc" 时的花名册 HireId（如 H017）
}
