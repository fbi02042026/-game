using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 佣兵头像/立绘统一加载：H001~H022、C001~C004、player，以及剧情 NPC（前台/会长）。
/// 优先 Resources/Icons/MercHead|MercStand；Editor 可直读 Art 目录。
/// </summary>
public static class MercPortraitSprites
{
    static readonly Dictionary<string, Sprite> HeadCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> StandCache = new Dictionary<string, Sprite>();

    static readonly Dictionary<string, string> AliasToHireId = new Dictionary<string, string>
    {
        { "player", "player" },
        { "wanjia", "player" },
        { "laodun", "H001" },
        { "xiaomei", "C001" },
        { "altor", "C002" },
        { "grey", "C003" },
        // 剧情 NPC → 佣兵立绘目录中的稳定英文 ID（见 Sync / LoadEditorStand）
        { "receptionist", "receptionist" },
        { "guildmaster", "guildmaster" },
        { "guildmaster_hidden", "guildmaster_hidden" },
        { "hunter", "C004" },
        { "duyan", "C004" },
        { "npc_duyan", "C004" },
        // 酒馆老板娘暂无独立佣兵立绘，沿用前台
        { "landlady", "receptionist" },
        { "boss_niang", "receptionist" },
    };

    public static string NormalizeHireId(string hireIdOrAlias)
    {
        if (string.IsNullOrEmpty(hireIdOrAlias)) return null;
        string key = hireIdOrAlias.Trim();
        if (AliasToHireId.TryGetValue(key, out var mapped))
            return mapped;
        if (key.StartsWith("H", System.StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("C", System.StringComparison.OrdinalIgnoreCase)
            || key == "player")
            return key;
        return null;
    }

    /// <summary>H/C 编号或 assetId → hireId（多 H 共用 asset 时取首条）。</summary>
    public static string ResolveHireId(string hireIdOrAssetId)
    {
        string normalized = NormalizeHireId(hireIdOrAssetId);
        if (!string.IsNullOrEmpty(normalized)) return normalized;
        if (string.IsNullOrEmpty(hireIdOrAssetId)) return null;

        if (MercSkillMapping.TryGetByAssetId(hireIdOrAssetId, out var row)
            && !string.IsNullOrEmpty(row.HireId))
            return row.HireId;

        if (MercRosterDefs.TryGetByAssetId(hireIdOrAssetId, out var def)
            && !string.IsNullOrEmpty(def.HireId))
            return def.HireId;

        return null;
    }

    public static Sprite GetHead(string hireIdOrAssetId)
    {
        string hireId = ResolveHireId(hireIdOrAssetId) ?? hireIdOrAssetId;
        if (string.IsNullOrEmpty(hireId)) return null;
        if (HeadCache.TryGetValue(hireId, out var cached) && cached != null)
            return cached;

        Sprite sp = LoadFromResources(ContentPaths.Icons.MercHead, hireId);
        if (sp == null)
            sp = LoadEditorHead(hireId);
        if (sp != null)
            HeadCache[hireId] = sp;
        return sp;
    }

    public static Sprite GetStand(string hireIdOrAssetId)
    {
        string hireId = ResolveHireId(hireIdOrAssetId) ?? NormalizeHireId(hireIdOrAssetId) ?? hireIdOrAssetId;
        if (string.IsNullOrEmpty(hireId)) return null;
        if (StandCache.TryGetValue(hireId, out var cached) && cached != null)
            return cached;

        Sprite sp = LoadFromResources(ContentPaths.Icons.MercStand, hireId);
        if (sp == null)
            sp = LoadEditorStand(hireId);
        if (sp != null)
            StandCache[hireId] = sp;
        return sp;
    }

    public static void ClearCache()
    {
        HeadCache.Clear();
        StandCache.Clear();
    }

    static Sprite LoadFromResources(string folder, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string path = folder + "/" + id;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        if (all != null && all.Length > 0) return all[0];
        var tex = Resources.Load<Texture2D>(path);
        return SpriteFromTexture(tex, id);
    }

    static Sprite SpriteFromTexture(Texture2D tex, string name)
    {
        if (tex == null || tex.width < 2 || tex.height < 2) return null;
        var made = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        made.name = name;
        return made;
    }

#if UNITY_EDITOR
    const string ArtHeadDir = "Assets/Art/UI/Icons/佣兵头像";
    const string ArtStandDir = "Assets/Art/UI/Icons/佣兵立绘";

    static Sprite LoadEditorHead(string hireId)
    {
        if (hireId == "player")
            return LoadEditorSprite(ArtHeadDir + "/玩家.png");
        return LoadEditorSprite(ArtHeadDir + "/" + hireId + ".png");
    }

    static Sprite LoadEditorStand(string hireId)
    {
        if (hireId == "player")
            return LoadEditorSprite(ArtStandDir + "/佣兵立绘_玩家.png");
        if (hireId == "receptionist")
            return LoadEditorSprite(ArtStandDir + "/前台小姐.png");
        if (hireId == "guildmaster")
            return LoadEditorSprite(ArtStandDir + "/会长——大众.png");
        if (hireId == "guildmaster_hidden")
            return LoadEditorSprite(ArtStandDir + "/会长——阴暗.png");
        return LoadEditorSprite(ArtStandDir + "/佣兵立绘_" + hireId + ".png");
    }

    static Sprite LoadEditorSprite(string assetPath)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        return SpriteFromTexture(tex, System.IO.Path.GetFileNameWithoutExtension(assetPath));
    }
#else
    static Sprite LoadEditorHead(string hireId) => null;
    static Sprite LoadEditorStand(string hireId) => null;
#endif
}
