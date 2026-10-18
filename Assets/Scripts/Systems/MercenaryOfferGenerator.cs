using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 酒馆招募：从佣兵形象池抽 3 个候选项；技能按 H 表与稀有度绑定。
/// </summary>
public static class MercenaryOfferGenerator
{
    public const int OfferCount = 3;

    static readonly string[] NamePool =
    {
        "阿雷", "薇拉", "石盾", "迅羽", "灰烬", "露娜", "铁腕", "影刺",
        "凯恩", "茉拉", "雷恩", "希雅", "布鲁", "诺拉", "达克", "艾琳",
        "霍克", "莉娜", "加仑", "苏尔", "米娅", "托尔", "茜茜", "沃克"
    };

    public static string SkillDisplayName(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return "—";
        if (MercSkillTable.TryGet(skillId, out var row))
            return row.DisplayName;
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(skillId) : null;
        if (cfg != null && !string.IsNullOrEmpty(cfg.skillName)) return cfg.skillName;
        return skillId;
    }

    public static List<MercenaryData> GenerateOffers()
    {
        // 【2026-10-19 主人拍板】三名候选全部随机（战斗内三选一同款稀有度权重），
        // 卡面稀有度由招募界面按佣兵稀有度实时换卡，与卡位顺序无关。
        var offers = new List<MercenaryData>(OfferCount);
        var usedSignatures = new HashSet<string>();

        for (int i = 0; i < OfferCount; i++)
        {
            MercenaryData offer = null;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                offer = RollOne();
                string sig = Signature(offer);
                if (!usedSignatures.Contains(sig))
                {
                    usedSignatures.Add(sig);
                    break;
                }
            }
            if (offer == null) offer = RollOne();
            EnsureUniqueName(offer, offers);
            offers.Add(offer);
        }
        return offers;
    }

    static void EnsureUniqueName(MercenaryData offer, List<MercenaryData> existing)
    {
        if (offer == null) return;
        bool clash;
        int guard = 0;
        do
        {
            clash = false;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] != null && existing[i] != offer && existing[i].displayName == offer.displayName)
                {
                    clash = true;
                    offer.displayName = NamePool[Random.Range(0, NamePool.Length)] + Random.Range(1, 99);
                    break;
                }
            }
            guard++;
        } while (clash && guard < 20);
    }

    static string Signature(MercenaryData m)
    {
        if (m == null) return "";
        return $"{m.mercId}|{m.displayName}|{m.level}|{m.star}|{m.skillId}|{m.passiveSkillId}";
    }

    static MercenaryData RollOne()
    {
        // 【2026-10-17 主人拍板】酒馆三选一改用战斗内三选一同款稀有度概率
        // （普通 8 / 稀有 5 / 传奇 2），不再按星级 1-5 均匀掷。
        var rarity = RollRarityByBattleWeight();
        int star = MercSkillMapping.RarityToStar(rarity);
        if (!MercSkillMapping.TryPickHireId(rarity, out var mapRow))
            return RollFallback(star);

        if (!MercRosterDefs.TryGetByHireId(mapRow.HireId, out var def))
            return RollFallback(star);

        if (star >= 4) star = Mathf.Max(star, rarity == MercRosterDefs.MercRarity.Legendary ? 5 : 4);
        int level = Random.Range(1, 11);
        if (star >= 4) level = Mathf.Max(level, Random.Range(5, 11));

        string name = def.Name;
        if (CountAssetUsers(def.AssetId) > 1)
            name = NamePool[Random.Range(0, NamePool.Length)];

        return new MercenaryData
        {
            mercId = def.AssetId,
            displayName = name,
            nickname = def.Nickname,
            hireId = mapRow.HireId,
            uid = System.Guid.NewGuid().ToString("N"),
            favorLevel = 1,
            level = level,
            star = star,
            skillId = mapRow.ActiveSkillId,
            passiveSkillId = mapRow.PassiveSkillId
        };
    }

    /// <summary>战斗内三选一同款稀有度权重：普通 8 / 稀有 5 / 传奇 2（总和 15 ≈ 53% / 33% / 13%）。</summary>
    // —— 稀有度掷点权重（2026-10-17 主人拍板 = 战斗内三选一同款）——
    // 公示弹窗（MercenaryRecruitPopupUI.ShowRecruitOdds）引用这组常量，改这里公示数字自动跟
    public const int RollTotal = 15;
    public const int CommonWeight = 8;
    public const int RareWeight = 5;
    public const int LegendaryWeight = 2;

    static MercRosterDefs.MercRarity RollRarityByBattleWeight()
    {
        int roll = Random.Range(0, RollTotal);
        if (roll < CommonWeight) return MercRosterDefs.MercRarity.Common;
        if (roll < CommonWeight + RareWeight) return MercRosterDefs.MercRarity.Rare;
        return MercRosterDefs.MercRarity.Legendary;
    }

    static int CountAssetUsers(string assetId)
    {
        int hits = 0;
        var all = MercRosterDefs.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].AssetId == assetId) hits++;
        }
        return hits;
    }

    static MercenaryData RollFallback(int star)
    {
        var pool = GetVisualPool();
        string mercId = pool[Random.Range(0, pool.Count)];
        MercSkillMapping.GetDefaultSkills(mercId, out string active, out string passive);
        string hireId = MercPortraitSprites.ResolveHireId(mercId);
        return new MercenaryData
        {
            mercId = mercId,
            hireId = hireId,
            displayName = NamePool[Random.Range(0, NamePool.Length)],
            uid = System.Guid.NewGuid().ToString("N"),
            favorLevel = 1,
            level = Random.Range(1, 11),
            star = star,
            skillId = active,
            passiveSkillId = passive
        };
    }

    static List<string> GetVisualPool()
    {
        var result = new List<string>();
        var mm = MercenaryManager.Instance;
        if (mm != null)
        {
            var hireable = mm.GetHireableMercIds();
            if (hireable != null)
            {
                for (int i = 0; i < hireable.Count; i++)
                {
                    if (!string.IsNullOrEmpty(hireable[i]))
                        result.Add(hireable[i]);
                }
            }
        }
        if (result.Count == 0)
        {
            result.AddRange(new[]
            {
                "dunbing101", "gongshou101", "kuangzhan101", "naima101", "fashi101",
                "dunbing102", "kuangzhan102", "naima102", "zhongzhan101", "zhongzhan201"
            });
        }
        return result;
    }

    public static string FormatCard(MercenaryData m)
    {
        if (m == null) return "空";
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(m.mercId)
            : m.mercId;
        string stars = new string('★', Mathf.Clamp(m.star, 1, 5));
        string active = SkillDisplayName(m.skillId);
        string passive = SkillDisplayName(m.passiveSkillId);
        return $"{m.displayName}\n{job}\nLv{Mathf.Max(1, m.level)}  {stars}\n主动：{active}\n被动：{passive}";
    }

    public static string FormatRosterLine(MercenaryData m, bool deploy)
    {
        if (m == null) return "";
        string name = string.IsNullOrEmpty(m.displayName) ? m.mercId : m.displayName;
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(m.mercId)
            : m.mercId;
        string stars = new string('★', Mathf.Clamp(m.star < 1 ? 1 : m.star, 1, 5));
        string active = SkillDisplayName(m.skillId);
        string passive = SkillDisplayName(m.passiveSkillId);
        return $"  {(deploy ? "★出战" : "·待命")} {name}（{job}） Lv{Mathf.Max(1, m.level)} {stars} [主:{active} 被:{passive}]";
    }
}
