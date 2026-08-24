using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 酒馆招募：从佣兵形象池抽 3 个候选项。
/// 形象（prefab/mercId）可相同，但姓名、等级、星级、技能必须做出差异。
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

    static readonly string[] AllySkillPool =
    {
        "ally_heal", "ally_shield", "ally_atk_up", "ally_atk_speed", "ally_crit_up", "ally_thunder"
    };

    static readonly string[] SkillDisplayNames =
    {
        "治愈之泉", "圣盾壁垒", "战意爆发", "疾风架势", "致命专注", "天雷裁决"
    };

    public static string SkillDisplayName(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return "未知技能";
        for (int i = 0; i < AllySkillPool.Length; i++)
        {
            if (AllySkillPool[i] == skillId)
                return SkillDisplayNames[i];
        }
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(skillId) : null;
        if (cfg != null && !string.IsNullOrEmpty(cfg.skillName)) return cfg.skillName;
        return skillId;
    }

    /// <summary>生成三选一；池为空时回退初级模板列表。</summary>
    public static List<MercenaryData> GenerateOffers()
    {
        var pool = GetVisualPool();
        var offers = new List<MercenaryData>(OfferCount);
        var usedSignatures = new HashSet<string>();

        for (int i = 0; i < OfferCount; i++)
        {
            MercenaryData offer = null;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                offer = RollOne(pool);
                string sig = Signature(offer);
                if (!usedSignatures.Contains(sig))
                {
                    usedSignatures.Add(sig);
                    break;
                }
            }
            if (offer == null) offer = RollOne(pool);
            // 强制本批姓名互异
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
                if (existing[i] != null && existing[i].displayName == offer.displayName)
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
        return $"{m.mercId}|{m.displayName}|{m.level}|{m.star}|{m.skillId}";
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
            // 回退：初级形象模板（非全弓手）
            result.AddRange(new[]
            {
                "dunbing101", "gongshou101", "kuangzhan101", "naima101", "qita101",
                "dunbing102", "kuangzhan102", "naima102"
            });
        }
        return result;
    }

    static MercenaryData RollOne(List<string> pool)
    {
        string mercId = pool[Random.Range(0, pool.Count)];
        int level = Random.Range(1, 11); // 1～10
        int star = Random.Range(1, 6);   // 1～5
        string skillId = AllySkillPool[Random.Range(0, AllySkillPool.Length)];
        string name = NamePool[Random.Range(0, NamePool.Length)];
        // 同批再抽时略微拉开：星级高的略抬等级
        if (star >= 4) level = Mathf.Max(level, Random.Range(5, 11));

        return new MercenaryData
        {
            mercId = mercId,
            displayName = name,
            uid = System.Guid.NewGuid().ToString("N"),
            favorLevel = 1,
            level = level,
            star = star,
            skillId = skillId
        };
    }

    public static string FormatCard(MercenaryData m)
    {
        if (m == null) return "空";
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(m.mercId)
            : m.mercId;
        string stars = new string('★', Mathf.Clamp(m.star, 1, 5));
        return $"{m.displayName}\n{job}\nLv{Mathf.Max(1, m.level)}  {stars}\n技能：{SkillDisplayName(m.skillId)}";
    }

    public static string FormatRosterLine(MercenaryData m, bool deploy)
    {
        if (m == null) return "";
        string name = string.IsNullOrEmpty(m.displayName) ? m.mercId : m.displayName;
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(m.mercId)
            : m.mercId;
        string stars = new string('★', Mathf.Clamp(m.star < 1 ? 1 : m.star, 1, 5));
        return $"  {(deploy ? "★出战" : "·待命")} {name}（{job}） Lv{Mathf.Max(1, m.level)} {stars} [{SkillDisplayName(m.skillId)}]";
    }
}
