using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 佣兵养成素材统一加载：**职业徽记** + **本命碎片底版**。
///
/// 命名口径（美术已按此出图，不要改名）：
///   徽记   剑盾/狂战/游侠/法师/牧师/重武 + 普通/稀有/传奇  → 如「剑盾传奇.png」
///   碎片底版 普通/稀有/传奇 + 「碎片」                    → 如「普通碎片.png」
///
/// 两处放图（缺 Resources 那份打包后 Load 不到）：
///   Art 源      Assets/Art/UI/Icons/徽章、Assets/Art/UI/Icons/人物碎片
///   运行时副本  Assets/Resources/Icons/JobBadge、MercFragmentBase
///   同步入口    Tools/UI/同步佣兵养成素材到 Resources（MercGrowArtTool）
///
/// 编辑器优先读 Art，所以**没同步也能立刻看到效果**。
/// </summary>
public static class MercGrowSprites
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    // ============================ 键 ============================

    /// <summary>稀有度 → 图名前缀。徽记档位与碎片底版共用同一套（低/中/高 = 普通/稀有/传奇）。</summary>
    public static string TierKey(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return "传奇";
            case MercRosterDefs.MercRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    /// <summary>玩家职业 → 徽记文件前缀。</summary>
    public static string JobKey(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.SwordShield: return "剑盾";
            case PlayerJobId.Berserker: return "狂战";
            case PlayerJobId.Ranger: return "游侠";
            case PlayerJobId.Mage: return "法师";
            case PlayerJobId.Priest: return "牧师";
            case PlayerJobId.Heavy: return "重武";
            default: return null;
        }
    }

    /// <summary>
    /// 佣兵职业名 → 徽记文件前缀。
    /// 花名册里的 JobName 有「剑盾卫士 / 游侠 / 狂战士 / 牧师 / 法师 / 水系法师 / 雷系法师 / 火系法师 / 重武者」，
    /// 全部归到 6 个徽记大类。识别不出返回 null。
    /// </summary>
    public static string JobKey(string jobName)
    {
        if (string.IsNullOrEmpty(jobName)) return null;
        string s = jobName.Trim();
        // 先判容易混淆的重武者（含「武」），再判其余
        if (s.Contains("剑盾") || s.Contains("盾")) return "剑盾";
        if (s.Contains("重武")) return "重武";
        if (s.Contains("狂战")) return "狂战";
        if (s.Contains("游侠") || s.Contains("弓")) return "游侠";
        if (s.Contains("牧") || s.Contains("圣") || s.Contains("恢复")) return "牧师";
        if (s.Contains("法") || s.Contains("术")) return "法师";
        return null;
    }

    // ============================ 徽记 ============================

    public static Sprite LoadJobBadge(PlayerJobId job, MercRosterDefs.MercRarity rarity)
        => LoadBadge(JobKey(job), rarity);

    public static Sprite LoadJobBadge(string jobName, MercRosterDefs.MercRarity rarity)
        => LoadBadge(JobKey(jobName), rarity);

    /// <summary>按佣兵（H 编号或 assetId）取该职业的徽记。职业识别失败返回 null。</summary>
    public static Sprite LoadBadgeForMerc(string hireIdOrAssetId, MercRosterDefs.MercRarity rarity)
    {
        if (string.IsNullOrEmpty(hireIdOrAssetId)) return null;
        if (!MercRosterDefs.TryGetByHireId(hireIdOrAssetId, out var def)
            && !MercRosterDefs.TryGetByAssetId(hireIdOrAssetId, out def))
            return null;
        return LoadBadge(JobKey(def.JobName), rarity);
    }

    static Sprite LoadBadge(string jobKey, MercRosterDefs.MercRarity rarity)
    {
        if (string.IsNullOrEmpty(jobKey)) return null;
        string file = jobKey + TierKey(rarity);
        return Load(ContentPaths.Icons.JobBadge + "/" + file,
                    ArtBadgeDir + "/" + file + ".png",
                    "badge/" + file);
    }

    // ============================ 碎片底版 ============================

    public static Sprite LoadFragmentBase(MercRosterDefs.MercRarity rarity)
    {
        string file = TierKey(rarity) + "碎片";
        return Load(ContentPaths.Icons.MercFragmentBase + "/" + file,
                    ArtFragmentDir + "/" + file + ".png",
                    "frag/" + file);
    }

    public static void ClearCache() => Cache.Clear();

    // ============================ 底层 ============================

    const string ArtBadgeDir = "Assets/Art/UI/Icons/徽章";
    const string ArtFragmentDir = "Assets/Art/UI/Icons/人物碎片";

    static Sprite Load(string resPath, string artPath, string cacheKey)
    {
        if (Cache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;

        Sprite sp = null;
#if UNITY_EDITOR
        sp = LoadEditorSprite(artPath);
#endif
        if (sp == null) sp = LoadFromResources(resPath);
        if (sp != null) Cache[cacheKey] = sp;
        return sp;
    }

    static Sprite LoadFromResources(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        if (all != null && all.Length > 0) return all[0];
        return SpriteFromTexture(Resources.Load<Texture2D>(path), path);
    }

#if UNITY_EDITOR
    static Sprite LoadEditorSprite(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        return SpriteFromTexture(tex, System.IO.Path.GetFileNameWithoutExtension(assetPath));
    }
#endif

    static Sprite SpriteFromTexture(Texture2D tex, string name)
    {
        if (tex == null || tex.width < 2 || tex.height < 2) return null;
        var made = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        made.name = name;
        return made;
    }
}
