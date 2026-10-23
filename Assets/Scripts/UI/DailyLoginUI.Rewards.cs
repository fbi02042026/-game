using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：奖励弹窗与图标（ShowRewardPopup/条目构造/X2 翻倍文案/图标加载与缓存）
public partial class DailyLoginUI
{

    /// <summary>
    /// B12: 把一份奖励（含第二份）塞进通用「获得奖励」弹窗。主人反馈：领完没有任何弹窗。
    /// 图标复用已有的 LoadRewardIcon；文案去稀有度前缀（详见 MakeRewardEntry：碎片类已改走主人碎片预制体）。
    /// factor = X2 双倍日的实发倍率，用来把文案里的数量同步翻倍（否则弹窗写 ×500、实际到账 ×1000）。
    /// </summary>
    static void ShowRewardPopup(DailyLoginDefs.Reward r, int factor)
    {
        var items = new List<RewardPopupUI.Entry>
        {
            MakeRewardEntry(r, factor)
        };
        if (r.HasSecond)
        {
            var r2 = new DailyLoginDefs.Reward
            {
                name = r.name2, grant = r.grant2, type = r.type2, amount = r.amount2, hireId = r.hireId2
            };
            items.Add(MakeRewardEntry(r2, factor));
        }
        RewardPopupUI.Show(items);
    }

    /// <summary>
    /// C2: 构造一条弹窗条目。碎片（MercFragment / LegendaryFragment）走主人的碎片预制体
    /// （RewardPopupUI.Entry.Fragment，渲染成 yongbingsuipian 卡片而不是一张底版图），
    /// 其余奖励保持老构造（图标 + 文案）。ScaleAmountInName 的 X2 翻倍逻辑照旧。
    /// </summary>
    static RewardPopupUI.Entry MakeRewardEntry(DailyLoginDefs.Reward r, int factor)
    {
        string label = ScaleAmountInName(StripRarity(r.name), factor);
        bool isFrag = r.grant == DailyLoginDefs.Grant.MercFragment
                   || r.grant == DailyLoginDefs.Grant.LegendaryFragment;
        if (isFrag)
            return RewardPopupUI.Entry.Fragment(RarityOfGrant(r.grant, r.hireId), r.hireId, label);
        return new RewardPopupUI.Entry(LoadRewardIcon(r), label);
    }

    /// <summary>
    /// B12: 把奖励名里「×」后的数字按 factor 翻倍。X2 双倍日实发翻倍（DailyLoginDefs.Doubled）只改 amount 不改 name，
    /// 弹窗文案必须自己同步，否则会出现「弹窗写 ×500、实际到账 ×1000」。名字里没有 ×数字 时原样返回。
    /// </summary>
    static string ScaleAmountInName(string name, int factor)
    {
        if (factor <= 1 || string.IsNullOrEmpty(name)) return name;
        int k = name.IndexOf('×');
        if (k < 0) return name;
        int start = k + 1, end = start;
        while (end < name.Length && char.IsDigit(name[end])) end++;
        if (end == start) return name;
        int v;
        if (!int.TryParse(name.Substring(start, end - start), out v)) return name;
        return name.Substring(0, start) + (v * factor) + name.Substring(end);
    }

    static void SetIcon(Cell cell, Sprite sp, string rewardName)
    {
        if (cell?.icon == null) return;
        var img = cell.icon.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sp;
            img.preserveAspect = true;
            // 白框兜底：sprite 为空时把 Image 调透明，避免露出纯白块（找不到图就只显首字兜底）。
            img.color = sp != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        if (cell.iconFallback != null)
        {
            cell.iconFallback.gameObject.SetActive(sp == null);
            if (sp == null && !string.IsNullOrEmpty(rewardName))
                cell.iconFallback.text = rewardName.Substring(0, 1);
        }
    }

    /// <summary>
    /// B6: 奖励名去稀有度前缀。主人反馈：奖励名不该带「稀有佣兵·」「传说·」这类前缀。
    /// 同时把多行奖励里的 \n 换成 ·，便于单行紧凑显示（以预制体文案为准，这里只清前缀）。
    /// </summary>
    static string StripRarity(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // 前缀列表（覆盖 普通/稀有/史诗/传说 × 佣兵/通用 的常见写法）
        string[] prefixes = {
            "稀有佣兵·", "传说佣兵·", "史诗佣兵·", "普通佣兵·",
            "稀有·", "传说·", "史诗·", "普通·"
        };
        foreach (var p in prefixes)
            if (name.StartsWith(p))
                return name.Substring(p.Length).Replace("\n", "·");
        return name.Replace("\n", "·");
    }

    /// <summary>
    /// B7: 按 hireId 查佣兵档位；查不到（如配置缺失）回退 Rare，避免碎片卡用错底色或空引用。
    /// </summary>
    static MercRosterDefs.MercRarity LookupRarity(string hireId)
    {
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out var def))
            return def.Rarity;
        return MercRosterDefs.MercRarity.Rare;
    }

    /// <summary>
    /// C1/C2 共用：按 grant 类型取稀有度。
    /// MercFragment → 用 hireId 查档位（查不到回退 Rare，见 LookupRarity）；LegendaryFragment → 固定 Legendary。
    /// 抽成一份是有意的：格子染色（C1）与弹窗条目（C2）必须同源，否则改一处漏一处、两边颜色对不上。
    /// </summary>
    static MercRosterDefs.MercRarity RarityOfGrant(DailyLoginDefs.Grant g, string hireId)
    {
        if (g == DailyLoginDefs.Grant.LegendaryFragment) return MercRosterDefs.MercRarity.Legendary;
        return LookupRarity(hireId);
    }

    /// <summary>奖励图标缓存：按奖励指纹复用，避免每次 Refresh 重复加载（底层 CommonIconSprites / MercGrowSprites 各自也有缓存，这里是界面层兜底）。</summary>
    static readonly Dictionary<string, Sprite> _rewardIconCache = new Dictionary<string, Sprite>();

    /// <summary>按奖励类型加载图标（佣兵头像 / 资源 / 碎片底版）。结果按奖励指纹缓存。</summary>
    static Sprite LoadRewardIcon(DailyLoginDefs.Reward r)
    {
        if (r.grant == DailyLoginDefs.Grant.None) return null;

        string key = RewardIconKey(r);
        if (key != null)
        {
            Sprite hit;
            if (_rewardIconCache.TryGetValue(key, out hit)) return hit;
        }

        Sprite sp;
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Merc:
                sp = MercPortraitSprites.GetHead(r.hireId);
                break;
            case DailyLoginDefs.Grant.Resource:
                sp = LoadResourceIcon(r.type);
                break;
            case DailyLoginDefs.Grant.MercFragment:
                // 塔克·重盾是稀有档，碎片底版取稀有；不叠佣兵头像（不改预制体 / 不新增节点，SetIcon 已支持范围足够）。
                sp = MercGrowSprites.LoadFragmentBase(MercRosterDefs.MercRarity.Rare);
                break;
            case DailyLoginDefs.Grant.Badge:
                // 【2026-10-23 D3】升星徽记图标：按 hireId 反查该职业+档位的徽记图（塔克=剑盾·稀有）。
                sp = MercGrowSprites.LoadBadgeForMerc(r.hireId, LookupRarity(r.hireId));
                break;
            case DailyLoginDefs.Grant.LegendaryFragment:
                sp = MercGrowSprites.LoadFragmentBase(MercRosterDefs.MercRarity.Legendary);
                break;
            default:
                sp = null;
                break;
        }

        if (key != null) _rewardIconCache[key] = sp;
        return sp;
    }

    /// <summary>资源类奖励图标统一走 Common 目录（金币/钻石/天赋石/强化石/分解材料）；体力奖励格用体力药水图（2026-10-17 主人拍板）。</summary>
    static Sprite LoadResourceIcon(ResourceWallet.ResourceType type)
    {
        switch (type)
        {
            case ResourceWallet.ResourceType.Stamina:
                // 主人 2026-10-17 放入的体力药水新图（Resources/Art/ 下，路径与 Art 源不同）
                return Resources.Load<Sprite>("Art/UI/Icons/Common/icon_stamina_potion");
            case ResourceWallet.ResourceType.Gold:
                return CommonIconSprites.Load("icon_gold");
            case ResourceWallet.ResourceType.Diamond:
                return CommonIconSprites.Load("icon_diamond_blue");
            case ResourceWallet.ResourceType.TalentPoint:
                return CommonIconSprites.Load("icon_talent_stone");
            case ResourceWallet.ResourceType.EnchantStone:
                return CommonIconSprites.Load("icon_enchant_stone");
            case ResourceWallet.ResourceType.DecomposeMat:
                return CommonIconSprites.Load("icon_decompose_mat");
            default:
                return null;
        }
    }

    /// <summary>构造奖励图标缓存 key（同奖励指纹相同即复用）；无法归类返回 null 不缓存。</summary>
    static string RewardIconKey(DailyLoginDefs.Reward r)
    {
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Merc:
                return "Merc_" + r.hireId;
            case DailyLoginDefs.Grant.MercFragment:
                // B7: 碎片卡现已带头像，不同佣兵不能共用缓存，key 必须含 hireId 与档位
                return "Frag_" + r.hireId + "_" + LookupRarity(r.hireId).ToString();
            case DailyLoginDefs.Grant.Badge:
                // 2026-10-23 D3 徽记：不同职业/档位的徽记图不同，key 含 hireId 与档位
                return "Badge_" + r.hireId + "_" + LookupRarity(r.hireId).ToString();
            case DailyLoginDefs.Grant.LegendaryFragment:
                return "Frag_" + r.hireId + "_" + MercRosterDefs.MercRarity.Legendary.ToString();
            case DailyLoginDefs.Grant.Resource:
                return "Res_" + r.type.ToString();
            default:
                return null;
        }
    }
}
