using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城镇商店的购买与发货逻辑（2026-09-15 新增）。
/// 只负责「扣钱 + 发货」，不管 UI。UI 调 <see cref="CanBuy"/> 拿原因、调 <see cref="Buy"/> 执行。
///
/// 设计红线：商店**不卖独占内容**，只卖时间。
/// 技能货架上所有商品都能通过推图免费拿到（章节兜底），买断只是让你更早拥有。
/// </summary>
public static class ShopSystem
{
    public struct BuyResult
    {
        public bool ok;
        public string message;
        /// <summary>Kind=Gacha 时，本次抽到的技能 id。</summary>
        public List<string> drawnSkillIds;
        /// <summary>Kind=Gacha 时，抽到的技能名（已拼好，可直接显示）。</summary>
        public List<string> drawnSkillNames;
    }

    /// <summary>是否达到上架章节门槛。</summary>
    public static bool IsOnShelf(ShopDefs.Item item, SaveData data)
    {
        if (item == null) return false;
        if (item.showFromChapter <= 0) return true;
        if (data == null) return false;
        if (data.clearedChapterIds != null && data.clearedChapterIds.Contains(item.showFromChapter)) return true;
        return data.maxUnlockedChapter > item.showFromChapter;
    }

    /// <summary>今天还能买几次（-1 = 不限）。</summary>
    public static int RemainingToday(ShopDefs.Item item, SaveData data)
    {
        if (item == null || item.dailyLimit <= 0) return -1;
        if (data == null) return item.dailyLimit;
        data.shopPurchases.TryGetValue(item.id, out int used);
        int left = item.dailyLimit - used;
        return left < 0 ? 0 : left;
    }

    public static bool CanBuy(ShopDefs.Item item, SaveData data, out string reason)
    {
        reason = "";
        if (item == null) { reason = "商品不存在"; return false; }
        if (data == null) { reason = "存档未就绪"; return false; }
        if (!IsOnShelf(item, data)) { reason = $"通关第{item.showFromChapter}章后上架"; return false; }

        int left = RemainingToday(item, data);
        if (left == 0) { reason = "今日已售罄"; return false; }

        if (item.kind == ShopDefs.Kind.Skill || item.kind == ShopDefs.Kind.Fragment)
        {
            var def = PlayerSkillDefs.GetById(item.skillId);
            if (def == null) { reason = "技能不存在"; return false; }
            if (PlayerSkillDefs.IsUnlocked(def, data)) { reason = "已拥有"; return false; }
            // 近战专属技能对远程职业整条隐藏：买了也用不了，不如别让他看见
            if (def.meleeOnly && !PlayerJobDefs.IsSelectedMelee())
            { reason = "当前职业不可用"; return false; }
            if (item.kind == ShopDefs.Kind.Fragment && !SkillFragmentDefs.CanBuyFragment(SkillDraftMeta.Rarity(item.skillId)))
            { reason = "传说碎片不出售"; return false; }
        }
        else if (item.kind == ShopDefs.Kind.Gacha)
        {
            if (CollectGachaPool(data).Count == 0 && CollectOwnedPool(data).Count == 0)
            { reason = "暂无可抽取的技能"; return false; }
        }

        long have = ResourceWallet.Get(data, item.currency);
        if (have < item.price)
        {
            reason = $"{ShopDefs.CurrencyName(item.currency)}不足（{have}/{item.price}）";
            return false;
        }
        return true;
    }

    public static BuyResult Buy(ShopDefs.Item item)
    {
        var result = new BuyResult { ok = false, drawnSkillIds = new List<string>(), drawnSkillNames = new List<string>() };
        var saveSys = SaveSystem.Instance;
        if (saveSys == null || saveSys.Data == null) { result.message = "存档未就绪"; return result; }
        var data = saveSys.Data;

        if (!CanBuy(item, data, out string reason)) { result.message = reason; return result; }
        if (!ResourceWallet.TrySpend(item.currency, item.price, save: false))
        { result.message = "扣款失败"; return result; }

        switch (item.kind)
        {
            case ShopDefs.Kind.Skill:
            {
                var def = PlayerSkillDefs.GetById(item.skillId);
                PlayerSkillDefs.Unlock(item.skillId, data);
                result.message = def != null ? $"已解锁「{def.displayName}」" : "已解锁技能";
                break;
            }
            case ShopDefs.Kind.Fragment:
            {
                if (!SkillFragmentSystem.TryBuyFragments(item.skillId, item.fragmentCount, out string fm))
                { result.message = fm; return result; }
                var fd = PlayerSkillDefs.GetById(item.skillId);
                int have = SkillFragmentSystem.Get(item.skillId);
                int need = SkillFragmentSystem.Needed(item.skillId);
                string skillName = fd != null ? fd.displayName : item.skillId;
                result.message = $"{skillName} 碎片 {have}/{need}";
                break;
            }
            case ShopDefs.Kind.Gacha:
            {
                var pool = CollectGachaPool(data);
                bool fromOwned = pool.Count == 0;
                if (fromOwned) pool = CollectOwnedPool(data);

                int n = Mathf.Min(item.drawCount, pool.Count);
                for (int i = 0; i < n; i++)
                {
                    string pick = WeightedPick(pool, i == n - 1 && item.drawCount >= 10);
                    if (string.IsNullOrEmpty(pick)) break;
                    pool.Remove(pick);

                    var d = PlayerSkillDefs.GetById(pick);
                    string shown = d != null ? d.displayName : pick;

                    if (PlayerSkillDefs.IsUnlocked(d, data))
                    {
                        // 已拥有 → 转碎片，抽卡在后期仍有意义
                        int got = SkillFragmentSystem.GrantFromDuplicate(pick);
                        result.drawnSkillIds.Add(pick);
                        result.drawnSkillNames.Add($"{shown}碎片×{got}");
                    }
                    else
                    {
                        PlayerSkillDefs.Unlock(pick, data);
                        result.drawnSkillIds.Add(pick);
                        result.drawnSkillNames.Add(shown);
                    }
                }
                result.message = result.drawnSkillNames.Count > 0
                    ? "抽到：" + string.Join("、", result.drawnSkillNames)
                    : "没有可抽取的技能";
                break;
            }
            default:
            {
                ResourceWallet.Add(item.grantType, item.grantAmount, save: false, notify: false);
                result.message = $"获得 {ShopDefs.CurrencyName(item.grantType)} ×{item.grantAmount}";
                break;
            }
        }

        // 记限购
        if (item.dailyLimit > 0)
        {
            data.shopPurchases.TryGetValue(item.id, out int used);
            data.shopPurchases[item.id] = used + 1;
        }
        data.shopPurchaseDay = ShopDefs.TodayKey();
        saveSys.Save();
        result.ok = true;
        return result;
    }

    /// <summary>
    /// 抽卡池：当前**未解锁**、且当前职业用得上的技能。
    /// 近战专属技能对远程职业不进池（和局内三选一同一套过滤规则）。
    /// </summary>
    static List<string> CollectGachaPool(SaveData data) => FilterSkills(data, false);

    /// <summary>已解锁的技能池：未解锁抽完后走这里，抽到的是碎片。</summary>
    static List<string> CollectOwnedPool(SaveData data) => FilterSkills(data, true);

    static List<string> FilterSkills(SaveData data, bool wantUnlocked)
    {
        var list = new List<string>();
        var all = PlayerSkillDefs.All;
        if (all == null || data == null) return list;
        bool melee = PlayerJobDefs.IsSelectedMelee();
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            if (PlayerSkillDefs.IsUnlocked(def, data) != wantUnlocked) continue;
            if (!SkillDraftMeta.InDraftPool(def.id)) continue;
            if (def.meleeOnly && !melee) continue;
            list.Add(def.id);
        }
        return list;
    }

    /// <summary>按稀有度权重抽一个。guaranteeRare = 十连保底，强制从稀有及以上里出。</summary>
    static string WeightedPick(List<string> pool, bool guaranteeRare)
    {
        if (pool == null || pool.Count == 0) return null;
        var candidates = pool;
        if (guaranteeRare)
        {
            var better = pool.FindAll(id => SkillDraftMeta.Rarity(id) >= SkillRarity.Rare);
            if (better.Count > 0) candidates = better;
        }
        int total = 0;
        for (int i = 0; i < candidates.Count; i++)
            total += Mathf.Max(1, SkillRarityUtil.Weight(SkillDraftMeta.Rarity(candidates[i])));
        int roll = Random.Range(0, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= Mathf.Max(1, SkillRarityUtil.Weight(SkillDraftMeta.Rarity(candidates[i])));
            if (roll < 0) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }
}
