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
        // 【2026-10-18 主人反馈「邮件关了再开才多一封」】溢出邮件原来是静默入箱（比如体力满时领
        // 每日登录奖励）——玩家完全不知情，只看到收件箱数莫名多一封。现与普通邮件同口径：
        // 弹「您有新的邮件」提示 + 邮箱开着就实时刷新列表。
        NewMailToast.Notify();
        MailListUI.NotifyInboxChanged();
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

        // 【2026-10-22 施工单 T12】碎片邮件预检：mercfrag 附件在背包仍装不下 → 整封不领、不标已领
        //（碎片不许像资源那样「装不下作废」），提示玩家腾格子。放在最前面，避免其它资源先入账。
        if (mail.attachments != null && !MercFragAttachmentsFit(mail))
        {
            UIManager.Instance?.ShowToast("背包已满，请先腾出格子再领取碎片", true);
            Debug.Log($"[Mail] 碎片邮件「{mail.title}」背包放不下，保持未领");
            return false;
        }

        // 【2026-10-18 修·主人报「体力溢出领取不了也关不掉」】领取口径改为：装得下的入账，装不下的随领取作废，
        // 整封邮件标已领。旧口径「装不下的留在邮件」在满体力时一笔都进不来 → 邮件永远领不完、红点消不掉；
        // 邮件文案口径本就是「领取后仍受上限限制」，与领取即清空的预期一致。
        // （实际只有体力有真实上限，金币等上限 9999999 打不满，此改动无副作用面）
        ClaimOne(ResourceWallet.ResourceType.Gold, mail.gold);
        ClaimOne(ResourceWallet.ResourceType.Diamond, mail.diamond);
        ClaimOne(ResourceWallet.ResourceType.Stamina, mail.stamina);
        ClaimOne(ResourceWallet.ResourceType.EnchantStone, mail.enchantStones);
        ClaimOne(ResourceWallet.ResourceType.DecomposeMat, mail.decomposeMats);
        ClaimOne(ResourceWallet.ResourceType.TalentPoint, mail.talentPoints);

        // 结构化附件（2026-10-16 邮件 UI 改造）：资源类同样先装后弃；佣兵/卷轴不占上限直接发放
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
                // 🆕 佣兵本命碎片（2026-10-22 施工单 T12）：走统一发碎片入口入包。
                // 进到这里前 MercFragAttachmentsFit 已预检过容量，正常必然全进背包；
                // 万一仍溢出（预检后被并发占用），GrantFragments 会把余量转回邮件 —— 不作废。
                if (att.type == "mercfrag")
                {
                    MercGrowInventory.GrantFragments(att.hireId, (int)att.amount, "mail-claim");
                    att.amount = 0;
                    continue;
                }
                // 🆕 招募卷轴奖励（2026-10-17）：无上限概念，直接入账
                if (att.type == "scrollCommon" || att.type == "scrollLegendary")
                {
                    var d = SaveSystem.Instance.Data;
                    if (att.type == "scrollCommon") d.mercScrollCommon += (int)att.amount;
                    else d.mercScrollLegendary += (int)att.amount;
                    Debug.Log($"[Mail] 邮件发放卷轴 {att.type} x{att.amount}");
                    att.amount = 0;
                    continue;
                }
                // 🆕 职业徽记（2026-10-24 主人拍板）：defId 存完整 badge id（badge:{职业}:{档位}），
                // 走 MercGrowInventory.Add（与徽记副本掉落/每日登录同仓同读 mercGrowItems）。
                // 缺 defId = 配置错，LogError 不发不吞（铁律 fail closed）。
                if (att.type == "badge")
                {
                    if (string.IsNullOrEmpty(att.defId))
                        Debug.LogError("[Mail] 徽记附件缺 defId（应为 badge:{职业}:{档位}），本附件未发放");
                    else
                    {
                        MercGrowInventory.Add(att.defId, (int)att.amount);
                        Debug.Log($"[Mail] 邮件发放徽记 {att.defId} ×{att.amount}");
                    }
                    att.amount = 0;
                    continue;
                }
                // 🆕 通用道具（2026-10-24 主人拍板）：进随身仓库 townBagItems（无容量上限），出战斗可见。
                if (att.type == "item")
                {
                    ItemGrantSystem.GrantItem(att.defId ?? att.hireId, (int)att.amount);
                    att.amount = 0;
                    continue;
                }
                // 🆕 装备（2026-10-24 主人拍板）：进仓库 legacyEquipPool，角色页可见。
                if (att.type == "equip")
                {
                    ItemGrantSystem.GrantEquip(att.defId ?? att.hireId, att.rarity, 0);
                    att.amount = 0;
                    continue;
                }
                var rt = AttachmentResourceType(att.type);
                if (rt == null) continue;   // 未知类型不吞不报，留给 UI 展示
                ClaimOne(rt.Value, att.amount);
                att.amount = 0;
            }
        }

        mail.gold = 0;
        mail.diamond = 0;
        mail.stamina = 0;
        mail.enchantStones = 0;
        mail.decomposeMats = 0;
        mail.talentPoints = 0;
        mail.claimed = true;

        if (notify) UIManager.Instance?.ShowToast("邮件已领取", force: true);

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return true;
    }

    static long ClaimOne(ResourceWallet.ResourceType type, long amount)
    {
        if (amount <= 0) return 0;
        // 领取不重复入邮，避免死循环
        var r = ResourceWallet.Add(type, amount, save: false, notify: false, overflowToMail: false);
        return amount - r.added;
    }

    // ── 体力浪费预检（2026-10-18 主人拍板：领取会超出上限先弹确认「多余的会浪费」）──

    /// <summary>领取这封邮件会浪费多少点体力（含结构化附件里的体力，不真正入账）；不超上限返回 0。</summary>
    public static int GetStaminaWasteOnClaim(MailEntry mail)
    {
        if (mail == null) return 0;
        long stamina = mail.stamina;
        if (mail.attachments != null)
        {
            for (int i = 0; i < mail.attachments.Count; i++)
            {
                var att = mail.attachments[i];
                if (att != null && att.amount > 0 && att.type == "stamina") stamina += att.amount;
            }
        }
        if (stamina <= 0) return 0;
        long max = ResourceWallet.GetMax(ResourceWallet.ResourceType.Stamina);
        long waste = StaminaSystem.Current + stamina - max;
        return waste > 0 ? (int)waste : 0;
    }

    /// <summary>一键领取：所有未领邮件的体力一起算，会浪费多少点（逐封装满再算下一封，结果与逐封领取一致）；不超上限返回 0。</summary>
    public static int GetStaminaWasteOnClaimAll()
    {
        Ensure();
        long total = 0;
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.claimed) continue;
            total += m.stamina;
            if (m.attachments != null)
            {
                for (int j = 0; j < m.attachments.Count; j++)
                {
                    var att = m.attachments[j];
                    if (att != null && att.amount > 0 && att.type == "stamina") total += att.amount;
                }
            }
        }
        if (total <= 0) return 0;
        long max = ResourceWallet.GetMax(ResourceWallet.ResourceType.Stamina);
        long waste = StaminaSystem.Current + total - max;
        return waste > 0 ? (int)waste : 0;
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

    // ── 佣兵本命碎片邮件（2026-10-22 施工单 T5/T12）──────────────────────────

    /// <summary>
    /// 碎片邮件（统一发碎片入口的溢出兜底）：标题/正文「XXX抽奖碎片」（XXX=佣兵名），
    /// 附件类型 mercfrag + 头像 iconOverride。只由 MercGrowInventory.GrantFragments 调用，别处禁止直接入邮。
    /// </summary>
    public static void EnqueueMercFragments(string hireId, int amount)
    {
        if (string.IsNullOrEmpty(hireId) || amount <= 0) return;
        string mercName = MercRosterDefs.TryGetByHireId(hireId, out var def) && !string.IsNullOrEmpty(def.Name)
            ? def.Name : hireId;
        Ensure();
        var mail = new MailEntry
        {
            id = Guid.NewGuid().ToString("N"),
            title = $"{mercName}抽奖碎片",
            body = $"冒险者的包裹里放不下「{mercName}的本命碎片」×{amount}，先寄存在这里。\n去角色界面腾出背包格子后再来领取。",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            claimed = false,
            type = MailType.Reward,
            sender = "系统",
            attachments = new List<MailAttachment>
            {
                new MailAttachment
                {
                    type = "mercfrag",
                    amount = amount,
                    hireId = hireId,
                    iconOverride = ContentPaths.Icons.MercHead + "/" + hireId,
                },
            },
        };
        SaveSystem.Instance.Data.mailInbox.Add(mail);
        SaveSystem.Instance.Save();
        Debug.Log($"[Mail] 碎片入邮 {mercName}({hireId}) ×{amount}，未领={UnclaimedCount()}");
        RedDot.RefreshCommon();
        NewMailToast.Notify();
        MailListUI.NotifyInboxChanged();
    }

    /// <summary>
    /// 该邮件里的 mercfrag 附件现在能否全部装进背包（领取前预检，不真正入账）。
    /// 【2026-10-23 主人拍板】碎片一律进战斗外背包（townBagItems，无容量上限），
    /// 不再依赖局内背包格子 —— 预检恒过，领取必然全部入账、不作废。
    /// </summary>
    static bool MercFragAttachmentsFit(MailEntry mail)
    {
        return true;
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
        MailListUI.NotifyInboxChanged();   // 🆕 邮箱开着时收到新邮件 → 实时刷新，不用关了重开
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

    /// <summary>
    /// 全服补偿邮件（2026-10-17）：500 金币 + 普通招募卷轴×2。
    /// 固定 id 去重，整份存档只发一次；要调整奖励/文案只改 id 升版（如 compensation_2026_10_v2）。
    /// 触发点：TownSceneBootstrap 回主界面补发（幂等，与欢迎信同链路）。
    /// </summary>
    public static void EnsureCompensationMail(bool notifyToast = true)
    {
        Ensure();
        const string CompId = "compensation_2026_10_v1";
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].id == CompId) return;   // 已发过，不重复

        Enqueue(new MailEntry
        {
            id = CompId,
            title = "致玩家的补偿礼包",
            sender = "制作组",
            body = "亲爱的冒险者：\n\n" +
                   "近期我们修复了游戏中的若干问题，感谢大家在试玩过程中的耐心与反馈。\n\n" +
                   "为表歉意，特送上补偿礼包：500 金币、普通招募卷轴×2，请收下。\n\n" +
                   "普通招募卷轴可在酒馆招募稀有佣兵。\n" +
                   "如有任何建议，欢迎加入玩家交流群（QQ 群号 1126152907）告诉我们。\n\n" +
                   "—— 像素冒险制作组",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
            detailImagePath = "Art/UI/Icons/表情包/爱心",   // 详情页表情：表情包「爱心」（2026-10-17 主人拍板，图在 Icons/表情包/ 目录）
            gold = 500,
            attachments = new List<MailAttachment>
            {
                new MailAttachment
                {
                    type = "scrollCommon",
                    amount = 2,
                    iconOverride = "Art/UI/Icons/Common/icon_hire_scroll",
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
    public string type;         // 内部 key：gold/diamond/stamina/enchant/mat/talent/merc/mercfrag/scrollCommon/scrollLegendary/badge/item/equip
    public long amount;
    public string iconOverride; // 图标 Resources 路径覆盖（可空）
    public string hireId;       // 🆕 type=="merc" 时的花名册 HireId（如 H017）
    public string defId;        // 🆕 type=="item"/"equip" 时的道具/装备模板 id（item_defs / EquipTemplate）；type=="badge" 时存完整徽记 id（badge:{职业}:{档位}）
    public int rarity;          // 🆕 type=="equip" 时的稀有度 0~4
}
