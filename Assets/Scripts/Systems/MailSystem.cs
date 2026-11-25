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
        // 【2026-11-16 主人报「没看过的邮件一键领取后，删除已读按钮不起作用，必须看过后才能删」】
        // 领取即视为已读：ClaimAll/行内领取/详情领取三条路都只设 claimed 不设 read，
        // 领过的邮件 read 仍为 false，被 DeleteRead 的 !m.read 挡住删不掉。领取成功统一补标 read。
        // 公告类不走领取（点开详情 MarkAsRead），不受影响；红点本就按 claimed 灭，无副作用。
        mail.read = true;

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
            var entry = new RewardPopupUI.Entry(sp, $"{AttachmentPopupName(att)} ×{att.amount}");
            // 【2026-11-14 主人拍板「获得奖励弹窗获得传奇佣兵的名字应该跟稀有度走」】
            // 佣兵本体条目名字按花名册稀有度上色（RarityPalette 唯一色表，FillCell 的 nameColor 分支自带黑描边），
            // 与邮件详情格（MailDetailUI 2026-11-08 口径）、装备条目同一套；查不到花名册 LogError 不上色（fail closed）。
            if (att.type == "merc")
            {
                if (!string.IsNullOrEmpty(att.hireId) && MercRosterDefs.TryGetByHireId(att.hireId, out var mdef))
                    entry.nameColor = RarityPalette.Get(mdef.Rarity);
                else
                    Debug.LogError("[Mail] 佣兵附件条目上色：花名册查不到 hireId=" + att.hireId);
            }
            // 2026-11-11 主人报「装备图标框不能换成金的吗」：装备附件条目按模板稀有度上色（传奇=橙金），
            // 与邮件详情格同口径；模板查不到 LogError 不上色（fail closed）。
            if (att.type == "equip")
            {
                ArenaSeasonWeapons.EnsureRegistered();
                var eqTpl = ConfigManager.Instance != null
                    ? ConfigManager.Instance.GetEquipTemplate(string.IsNullOrEmpty(att.defId) ? att.hireId : att.defId)
                    : null;
                if (eqTpl != null)
                {
                    entry.nameColor = RarityPalette.Get(eqTpl.baseRarity);   // 名字金色
                    entry.frameSprite = EquipRarityFrame.SpriteOf(eqTpl.baseRarity);   // 框走装备框三档图
                }
                else Debug.LogError("[Mail] 装备附件条目上色：模板查不到 defId=" + att.defId);
            }
            items.Add(entry);
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
        // 2026-11-11 主人报「名字怎么是个英文的」：装备附件原走 default 返回原始 key「equip」。
        // 改走模板真源取中文名（竞技场冠军武器/里程补偿弓同一出口）；查不到 LogError 退「装备」，不再露英文 key。
        if (att.type == "equip")
        {
            ArenaSeasonWeapons.EnsureRegistered();   // 运行时模板兜底注册（照 ItemGrantSystem.GrantEquip 先例，幂等）
            var eqTpl = ConfigManager.Instance != null
                ? ConfigManager.Instance.GetEquipTemplate(string.IsNullOrEmpty(att.defId) ? att.hireId : att.defId)
                : null;
            if (eqTpl != null && !string.IsNullOrEmpty(eqTpl.equipName)) return eqTpl.equipName;
            Debug.LogError("[Mail] 装备附件显示名：模板查不到 defId=" + att.defId);
            return "装备";
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
    /// 首登欢迎邮件（给玩家的正式信）：含 800 金币 + 100 钻石 + 致谢与加群邀请。
    /// 【2026-11-23 主人拍板「只留欢迎信 给800金币 100钻石」】原 200 金币+5 天赋石改 800 金币+100 钻石，
    /// id 升版 v4 重发（老玩家也会收到新版一封；旧版 v3 与停发的补偿礼包由 DevGiftMail 清理）。
    /// 配图走表情包「高兴」（2026-10-16 主人原话：「表情用表情包里的图片 用高兴的那个」——不用佣兵头像顶表情位）。
    /// 【2026-11-25 修】改用存档标记 welcomeMailIssued 去重：整份存档只发一次，删了信也不重发；
    /// 要重新发放（改文案/加奖励/换配图）需同时升 id 并清标记（如 welcome_2026_v5）。
    /// 触发点：① 玩完新手引导（StoryProgress.MarkTutorialDone，notifyToast=false：提示延后到关登录时）；
    /// ② 已通关老玩家回主界面补发（TownSceneBootstrap，幂等）。
    /// </summary>
    public static void EnsureWelcomeMail(bool notifyToast = true)
    {
        Ensure();
        const string WelcomeId = "welcome_2026_v4";
        var data = SaveSystem.Instance.Data;
        var list = data.mailInbox;

        // 【2026-11-25 修·主人报「欢迎信领了删掉，打一关回城又出现」】
        // 旧去重只扫收件箱：删除已领邮件后 id 消失，回城 TryDailyLoginOnce 再调本方法
        // 就重发一封（连 800金币/100钻石 一起重发，可无限刷）。改用存档级持久标记，
        // 发过一次即永久不再发，与邮件是否被删除无关。
        if (data.welcomeMailIssued) return;

        // 老档兼容：以前发过（箱里还有欢迎信，含已领未删）但没标记 → 补标记，不重发。
        // 箱里已删的老档无法区分，会补发一次，之后被标记挡住。
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == WelcomeId)
            {
                data.welcomeMailIssued = true;
                SaveSystem.Instance.Save();
                return;
            }
        }

        data.welcomeMailIssued = true;   // 先置标记，Enqueue 内部 Save 会一起落盘
        Enqueue(new MailEntry
        {
            id = WelcomeId,
            title = "欢迎来到《像素冒险：裂隙之刃》！",
            sender = "制作组",
            body = "感谢您下载并试玩《像素冒险：裂隙之刃》！\n\n" +
                   "这是制作组的一点小心意——800 金币与 100 钻石请收下，祝您冒险顺利。\n\n" +
                   "我们是个小团队，正努力把游戏做得更好玩。如果您在游戏里有任何想法、吐槽或建议，" +
                   "非常欢迎加入玩家交流群（QQ 群号 1126152907），和我们一起聊游戏制作。\n\n" +
                   "我们一定会认真听取大家合理的建议，让游戏变得更有趣。再次感谢您的支持！\n\n" +
                   "—— 像素冒险制作组",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = MailType.Reward,
            detailImagePath = "UI/Art/Stickers/高兴",   // 详情页表情：表情包「高兴」（2026-10-16 主人拍板，路径同 MailDetailUI.StickerDir 口径）
            gold = 800,
            diamond = 100,   // 【2026-11-23 主人拍板】奖励改 800 金币 + 100 钻石（flat 字段，详情页/领取弹窗均有现成展示）
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
            // 2026-11-18 主人拍板：招募卷轴无上限（9999999），极端溢出仍走既有附件通道——
            // 卷轴在邮件系统里本就是 attachments（见 TryClaim 的 scrollCommon/scrollLegendary 分支），
            // 不在 MailEntry 上加 flat 字段（存档零迁移、不新增序列化字段）。
            case ResourceWallet.ResourceType.ScrollCommon:
            case ResourceWallet.ResourceType.ScrollLegendary:
                mail.attachments ??= new List<MailAttachment>();
                mail.attachments.Add(new MailAttachment
                {
                    type = type == ResourceWallet.ResourceType.ScrollCommon ? "scrollCommon" : "scrollLegendary",
                    amount = amount
                });
                break;
        }
    }

    static void Ensure()
    {
        if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null)
            throw new InvalidOperationException("SaveSystem 未就绪");
        SaveSystem.Instance.Data.mailInbox ??= new List<MailEntry>();
        // 2026-11-11 主人报「发的邮件排到最下面」：历史邮件有漏写 createTime 的（如竞技场赛季结算信），
        // 默认 0 = 列表时间倒序恒排最底、时间列空白。读档口统一补当前时间（补过即 >0 不再动，幂等）。
        var inbox = SaveSystem.Instance.Data.mailInbox;
        bool patched = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < inbox.Count; i++)
        {
            var m = inbox[i];
            if (m == null || m.createTime > 0) continue;
            m.createTime = now;
            patched = true;
        }
        if (patched) SaveSystem.Instance.Save();
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
