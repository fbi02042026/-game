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

        mail.gold = leftGold;
        mail.diamond = (int)leftDiamond;
        mail.stamina = (int)leftStamina;
        mail.enchantStones = (int)leftEnchant;
        mail.decomposeMats = (int)leftMats;
        mail.talentPoints = (int)leftTalent;

        bool empty = mail.gold <= 0 && mail.diamond <= 0 && mail.stamina <= 0
                     && mail.enchantStones <= 0 && mail.decomposeMats <= 0 && mail.talentPoints <= 0;
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
}
