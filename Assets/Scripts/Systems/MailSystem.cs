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
        // 【2026-11-01 主人拍板】领取成功改弹「获得奖励」弹窗（不再 toast）：
        // 清单必须在发放入账前收集 —— 下面会把附件 amount 置 0、flat 字段清零。
        var claimRewards = notify ? BuildClaimRewardPopupEntries(mail) : null;

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

        if (notify)
        {
            // 【2026-11-01 主人拍板】领取成功弹「获得奖励」弹窗（与每日登录/宝箱同款 RewardPopupUI）；
            // 清单为空（理论上领取按钮只在有奖励时出现）才退回原 toast。
            if (claimRewards != null && claimRewards.Count > 0)
                RewardPopupUI.Show(claimRewards);
            else
                UIManager.Instance?.ShowToast("邮件已领取", force: true);
        }

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

    // ── 领取成功弹「获得奖励」弹窗（2026-11-01 主人拍板：不再 toast）──

    /// <summary>领取前收集这封邮件的奖励清单。必须在发放入账前调用（发放会清零字段/附件 amount）。</summary>
    static List<RewardPopupUI.Entry> BuildClaimRewardPopupEntries(MailEntry mail)
    {
        var items = new List<RewardPopupUI.Entry>();
        if (mail == null) return items;

        // flat 货币：图标与详情窗物品格同口径（Common 目录；体力用体力药水图，2026-10-17 主人拍板）
        if (mail.gold > 0) items.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_gold"), $"金币 ×{mail.gold}"));
        if (mail.diamond > 0) items.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_diamond_blue"), $"钻石 ×{mail.diamond}"));
        if (mail.stamina > 0) items.Add(new RewardPopupUI.Entry(Resources.Load<Sprite>("UI/Art/Icons/Common/icon_stamina_potion"), $"体力 ×{mail.stamina}"));
        if (mail.enchantStones > 0) items.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_enchant_stone"), $"附魔石 ×{mail.enchantStones}"));
        if (mail.decomposeMats > 0) items.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_decompose_mat"), $"分解材料 ×{mail.decomposeMats}"));
        if (mail.talentPoints > 0) items.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_talent_stone"), $"天赋石 ×{mail.talentPoints}"));

        if (mail.attachments == null) return items;
        for (int i = 0; i < mail.attachments.Count; i++)
        {
            var att = mail.attachments[i];
            if (att == null || att.amount <= 0) continue;

            // 碎片走主人碎片预制体卡片（与每日登录弹窗同口径），名字与详情窗一致「XX碎片」（统一走 FragmentDisplayName 唯一出口）
            if (att.type == "mercfrag")
            {
                var rarity = MercRosterDefs.MercRarity.Rare;
                if (!string.IsNullOrEmpty(att.hireId) && MercRosterDefs.TryGetByHireId(att.hireId, out var fdef))
                    rarity = fdef.Rarity;
                string fragName = FragmentDisplayName(att.hireId);   // 唯一出口，别再自己拼
                items.Add(RewardPopupUI.Entry.Fragment(rarity, att.hireId, $"{fragName} ×{att.amount}"));
                continue;
            }

            var sp = !string.IsNullOrEmpty(att.iconOverride) ? Resources.Load<Sprite>(att.iconOverride) : null;
            items.Add(new RewardPopupUI.Entry(sp, $"{AttachmentPopupName(att)} ×{att.amount}"));
        }
        return items;
    }

    /// <summary>结构化附件的弹窗名称（与 MailDetailUI 详情格同名口径；mercfrag 已在上方单独处理）。</summary>
    static string AttachmentPopupName(MailAttachment att)
    {
        if (att == null) return "奖励";
        if (att.type == "merc" && !string.IsNullOrEmpty(att.hireId))
        {
            if (MercRosterDefs.TryGetByHireId(att.hireId, out var def) && !string.IsNullOrEmpty(def.Name))
                return def.Name;
            return att.hireId;
        }
        if (att.type == "badge")
        {
            const string prefix = "badge:";
            if (!string.IsNullOrEmpty(att.defId) && att.defId.StartsWith(prefix))
            {
                var parts = att.defId.Substring(prefix.Length).Split(':');
                if (parts.Length == 2 && !string.IsNullOrEmpty(parts[0]))
                {
                    // 【2026-11-08 主人拍板「把普通换一行」】档位词前插换行：
                    // 「剑盾徽记·」/「普通」各自成行，免得 Label 的 Wrap 把「普通」拆成「普」+「通」。
                    // 只影响领取弹窗名；详情格 BadgeDisplayName 与邮件正文仍是单行口径（铁律 23，未扩大范围）。
                    return parts[0] + "徽记·\n" + parts[1];
                }
            }
            return "职业徽记";
        }
        switch (att.type)
        {
            case "gold": return "金币";
            case "diamond": return "钻石";
            case "stamina": return "体力";
            case "enchant": return "附魔石";
            case "mat": return "分解材料";
            case "talent": return "天赋石";
            case "scrollCommon": return "普通招募卷轴";
            case "scrollLegendary": return "高级招募卷轴";
            default: return att.type;
        }
    }

    /// <summary>
    /// 【2026-11-08 主人拍板】碎片道具显示名的全局唯一出口：统一收成「XX碎片」（如「路加碎片」），
    /// 不再各处各拼一份「XX的本命碎片」。领取弹窗 / 邮件详情页附件格 / 邮件正文一律调这里。
    /// 查不到花名册或名字 → LogError 并退回「本命碎片」（fail closed，不猜、不静默兜底）。
    /// </summary>
    public static string FragmentDisplayName(string hireId)
    {
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def)
            || string.IsNullOrEmpty(def.Name))
        {
            Debug.LogError($"[Mail] 碎片显示名查不到花名册或名字：hireId={hireId}，退回「本命碎片」");
            return "本命碎片";
        }
        return def.Name + "碎片";
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

    /// <summary>🆕 发放佣兵奖励（type=="merc"）：解锁到花名册并尽量补进战斗抽奖池，保证玩家可招募；不占资源上限。
    /// 【2026-10-28 主人拍板】新获得（UnlockMerc 返回 true）时播「获得佣兵」全屏演出；已拥有空发不弹。</summary>
    static void GrantMerc(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        var d = SaveSystem.Instance?.Data;
        if (d == null) return;
        if (d.UnlockMerc(hireId))
            MercGainCinematicUI.Show(hireId, null);
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
            body = $"冒险者的包裹里放不下「{FragmentDisplayName(hireId)}」×{amount}，先寄存在这里。\n去角色界面腾出背包格子后再来领取。",
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
    /// 首登欢迎邮件（给玩家的正式/测试信）：含 200 金币 + 5 天赋石 + 致谢与加群邀请。
    /// 【2026-11-05 主人拍板】原附件佣兵「莉娜(H017)」删掉，换 5 天赋石；id 升版 v3 重发（老玩家也会收到新版一封）。
    /// 配图走表情包「高兴」（2026-10-16 主人原话：「表情用表情包里的图片 用高兴的那个」——不用佣兵头像顶表情位）。
    /// 用固定 id 去重，整份存档只发一次；要重新发放（改文案/加奖励/换配图）只需把 id 升版（如 welcome_2026_v3）。
    /// 触发点：① 玩完新手引导（StoryProgress.MarkTutorialDone，notifyToast=false：提示延后到关登录时）；
    /// ② 已通关老玩家回主界面补发（TownSceneBootstrap，幂等）。
    /// </summary>
    public static void EnsureWelcomeMail(bool notifyToast = true)
    {
        Ensure();
        const string WelcomeId = "welcome_2026_v3";
        var list = SaveSystem.Instance.Data.mailInbox;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].id == WelcomeId) return;   // 已发过，不重复

        Enqueue(new MailEntry
        {
            id = WelcomeId,
            title = "欢迎来到《像素冒险：裂隙之刃》！",
            sender = "制作组",
            body = "感谢您下载并试玩《像素冒险：裂隙之刃》！\n\n" +
                   "这是制作组的一点小心意——200 金币与 5 枚天赋石请收下，祝您冒险顺利。\n\n" +
                   "我们是个小团队，正努力把游戏做得更好玩。如果您在游戏里有任何想法、吐槽或建议，" +
                   "非常欢迎加入玩家交流群（QQ 群号 1126152907），和我们一起聊游戏制作。\n\n" +
                   "我们一定会认真听取大家合理的建议，让游戏变得更有趣。再次感谢您的支持！\n\n" +
                   "—— 像素冒险制作组",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
            detailImagePath = "UI/Art/Stickers/高兴",   // 详情页表情：表情包「高兴」（2026-10-16 主人拍板，路径同 MailDetailUI.StickerDir 口径）
            gold = 200,
            talentPoints = 5,   // 【2026-11-05 主人拍板】佣兵莉娜删掉，换 5 天赋石（flat 字段，详情页/领取弹窗均有现成展示）
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
            detailImagePath = "UI/Art/Icons/表情包/爱心",   // 详情页表情：表情包「爱心」（2026-10-17 主人拍板，图在 Icons/表情包/ 目录）
            gold = 500,
            attachments = new List<MailAttachment>
            {
                new MailAttachment
                {
                    type = "scrollCommon",
                    amount = 2,
                    iconOverride = "UI/Art/Icons/Common/icon_hire_scroll",
                },
            },
        }, notifyToast);
    }

    /// <summary>
    /// 里程奖励·猎手长弓补偿邮件（2026-11-02 主人拍板「已经购买但没有装备上的 做下补偿…直接发个邮件给个弓得了」）：
    /// 领过里程弓（logMileageGrantedKeys 含 special:rare_bow；该奖励条目已从 WeaponMilestones 下架）但因
    /// 「开局不重发」bug 实际没享受到的玩家 → 补一封带 equip_bow_1（稀有档）附件的邮件，领取进随身仓库
    /// （equip 附件口径同 ArenaRules 冠军武器邮件）。
    /// 防重 = SaveData.mileageBowMailSent（已发过 → return；没领过弓 → return 且不写档）。
    /// 触发点：TownSceneBootstrap.TryDailyLoginOnce（回主界面补发，与欢迎信/补偿信同链路）。
    /// 🔴 测试版临时，正式上线删除：调用行 + 本方法 + SaveData.mileageBowMailSent 一并删。
    /// </summary>
    public static void EnsureMileageBowCompensationMail(bool notifyToast = true)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        if (data.mileageBowMailSent) return;                                    // 已发过
        var keys = data.logMileageGrantedKeys;
        if (keys == null || !keys.Contains("special:rare_bow")) return;         // 没领过里程弓：不补（不写档）

        Enqueue(new MailEntry
        {
            title = "猎手长弓补偿",
            body = "亲爱的冒险者：\n\n" +
                   "「猎手长弓」已从里程奖励中下架。\n" +
                   "感谢你的支持，随信附上一把猎手长弓，请收下。\n\n" +
                   "—— 像素冒险制作组",
            sender = "制作组",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
            attachments = new List<MailAttachment>
            {
                new MailAttachment
                {
                    type = "equip",
                    amount = 1,
                    defId = "equip_bow_1",
                    rarity = 2,   // 邮件装备附件口径 0~4：2 = 稀有（与里程领取时 forcedRarity=Rare 同档）
                    iconOverride = EquipIcons.ResourcesPath + "Bow_1",   // equip_bow_1.iconFileName（照 ArenaSeasonWeapons.MailIconPath 先例）
                },
            },
        }, notifyToast);
        data.mileageBowMailSent = true;
        SaveSystem.Instance?.Save();
        Debug.Log("[Mail] 里程·猎手长弓补偿邮件已发（测试版临时，正式上线删除）");
    }

    /// <summary>
    /// 【2026-11-08 主人拍板·v2 拆分版】升星流程测试补给：拆成 6 封，每封最多 3 个附件格。
    /// 起因：v1 一封塞 14 格，MailDetailUI.RebuildRewardCells 是单排横排（96px×13≈1248 > 逻辑宽 720），
    /// 图标会横向溢出详情页。拆开后单封最多 3 格（192px），详情页/领取弹窗都不再溢出。
    /// 口径（主人原话「每次一个佣兵和碎片还有徽记」）：每封对应一个佣兵，含该佣兵的满星徽记 +
    /// 满星本命碎片；本次新发的 H013 莫娜 / H017 莉娜另加佣兵本体（已拥有的 3 白不重复发本体）。
    /// 另 1 封单独放资金：1000 钻 + 5000 金（2026-11-08 主人「金币改成5000吧」），不塞进佣兵封，
    /// 保持「一封一个佣兵」的口径。
    /// 徽记 id / 档位 / 数量 / 碎片数全部走真源（MercGrowSprites.JobKey + MercStarDefs.BadgeTierOf +
    /// MercGrowInventory.BadgeId + MercStarDefs.BadgeCost / FullStarFragTotal），不写死任何 id 与数量；
    /// 花名册或职业认不出 → LogError 并跳过该封（fail closed，不发不吞）。
    /// 附件口径沿用既有 TryClaim 分支：merc→解锁佣兵、badge→MercGrowInventory.Add、mercfrag→GrantFragments。
    /// 触发点：TownSceneBootstrap.TryDailyLoginOnce（与欢迎信/补偿信同链路，要求引导已完成）。
    /// 逐封固定 id 幂等（v2 前缀）；v1 那封旧大杂烩若还在测试档里先清掉，避免两版叠加把奖励发两遍。
    /// 🔴 测试版临时，正式上线删除：调用行 + 本方法。改奖励需升版 id 前缀（如 v3）才会重发。
    /// </summary>
    public static void EnsureStarUpTestMail(bool notifyToast = true)
    {
        Ensure();
        const string IdPrefix = "starup_test_mail_v2_";
        const string LegacyId = "starup_test_mail_v1";
        const int TestDiamond = 1000;
        const int TestGold = 5000;
        var inbox = SaveSystem.Instance.Data.mailInbox;

        // 旧版 v1（一封 14 格）已在部分测试档入箱：清掉，避免与 v2 叠加重复发奖励。
        int removed = 0;
        for (int i = inbox.Count - 1; i >= 0; i--)
        {
            var m = inbox[i];
            if (m != null && m.id == LegacyId) { inbox.RemoveAt(i); removed++; }
        }
        if (removed > 0)
        {
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
            MailListUI.NotifyInboxChanged();
        }

        // 本版每封独立幂等：一次收集现有 id（Enqueue 会往同一个 list 追加，本集合只用于查重）
        var existingIds = new HashSet<string>();
        for (int i = 0; i < inbox.Count; i++)
            if (inbox[i] != null && !string.IsNullOrEmpty(inbox[i].id)) existingIds.Add(inbox[i].id);

        const string LegendaryMage = "H013";   // 莫娜·雷系法师（传奇）
        const string RarePriest = "H017";      // 莉娜·牧师（稀有）
        var newMercs = new HashSet<string> { LegendaryMage, RarePriest };

        // 一封一个佣兵：默认已拥有 3 白 + 本次新发 2 人
        var badgeMercs = new List<string>(SaveData.DefaultUnlockedMercIds) { LegendaryMage, RarePriest };

        int sent = 0;
        for (int i = 0; i < badgeMercs.Count; i++)
        {
            string hireId = badgeMercs[i];
            string mailId = IdPrefix + hireId;
            if (existingIds.Contains(mailId)) continue;   // 该佣兵那封已发过

            if (!MercRosterDefs.TryGetByHireId(hireId, out var def))
            {
                Debug.LogError($"[Mail] 升星测试邮件：花名册查不到 {hireId}，该封未发放");
                continue;
            }
            string job = MercGrowSprites.JobKey(def.JobName);
            if (string.IsNullOrEmpty(job))
            {
                Debug.LogError($"[Mail] 升星测试邮件：{def.Name} 职业名「{def.JobName}」识别不出徽记大类，该封未发放");
                continue;
            }
            string tier = MercStarDefs.BadgeTierOf(def.Rarity);
            int badgeCount = 0;
            for (int s = 1; s <= MercStarDefs.MaxOutStar; s++)
                badgeCount += MercStarDefs.BadgeCost(def.Rarity, s);
            int fragCount = MercStarDefs.FullStarFragTotal(def.Rarity);
            bool isNew = newMercs.Contains(hireId);

            var attachments = new List<MailAttachment>();
            if (isNew)   // 默认 3 白已拥有，不重复发本体
            {
                attachments.Add(new MailAttachment
                {
                    type = "merc",
                    amount = 1,
                    hireId = hireId,
                    iconOverride = ContentPaths.Icons.MercHead + "/" + hireId,
                });
            }
            attachments.Add(new MailAttachment
            {
                type = "badge",
                amount = badgeCount,
                defId = MercGrowInventory.BadgeId(job, tier),
                iconOverride = ContentPaths.Icons.JobBadge + "/" + job + tier,
            });
            attachments.Add(new MailAttachment
            {
                type = "mercfrag",
                amount = fragCount,
                hireId = hireId,
                iconOverride = ContentPaths.Icons.MercHead + "/" + hireId,
            });

            string body = $"{def.Name}（{def.JobName}）的升星补给：\n" +
                          $"· {job}徽记·{tier} ×{badgeCount}\n" +
                          $"· {FragmentDisplayName(hireId)} ×{fragCount}\n" +
                          (isNew ? "· 附赠该佣兵本体\n" : "") +
                          "徽记与碎片够升满 3 星；升星仍需另扣金币/钻石。\n" +
                          "—— 像素冒险制作组";

            Enqueue(new MailEntry
            {
                id = mailId,
                title = "升星补给·" + def.Name,
                sender = "制作组",   // 不用「开发组」：DevGiftMail 清理判据就是它，会被当场删掉
                body = body,
                createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                type = MailType.Reward,
                attachments = attachments,
            }, notifyToast);
            existingIds.Add(mailId);
            sent++;
        }

        // 资金单独一封（2 个 flat 货币格），不塞进佣兵封
        string fundId = IdPrefix + "fund";
        if (!existingIds.Contains(fundId))
        {
            Enqueue(new MailEntry
            {
                id = fundId,
                title = "升星补给·资金",
                sender = "制作组",
                body = "升星要花不少钱，先给你备上：\n" +
                       $"· 钻石 ×{TestDiamond}\n· 金币 ×{TestGold}\n\n" +
                       "升星仍会按稀有度另扣金币/钻石。\n\n—— 像素冒险制作组",
                createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                type = MailType.Reward,
                diamond = TestDiamond,
                gold = TestGold,
            }, notifyToast);
            sent++;
        }

        if (sent > 0 || removed > 0)
            Debug.Log($"[Mail] 升星补给 v2：本次发 {sent} 封（{badgeMercs.Count} 封佣兵材料 + 1 封资金 {TestDiamond}钻/{TestGold}金），清理旧 v1 {removed} 封（测试版临时）");
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
