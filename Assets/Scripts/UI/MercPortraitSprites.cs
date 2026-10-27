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

    // 【2026-10-23 主人拍板】ID 解析（NormalizeHireId / ResolveHireId / 别名表）已收口到
    // MercRosterDefs，本类只负责按 hireId 取图，不再自己解析 —— 别在这里补第二份。

    public static Sprite GetHead(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId) ?? hireIdOrAssetId;
        if (string.IsNullOrEmpty(hireId)) return null;
        if (HeadCache.TryGetValue(hireId, out var cached) && cached != null)
            return cached;

        Sprite sp = null;
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再优先读美术源目录，改走 Resources，缺图露白框。
        if (DeviceParity.EditorFallbackEnabled)
            sp = LoadEditorHead(hireId);
#endif
        if (sp == null)
            sp = LoadFromResources(ContentPaths.Icons.MercHead, hireId);
#if UNITY_EDITOR
        if (sp == null && DeviceParity.EditorFallbackEnabled)
            sp = LoadEditorHead(hireId);
#endif
        if (sp == null)
            DeviceParity.ReportMissing(ContentPaths.Icons.MercHead + "/" + hireId, "佣兵头像");
        if (sp != null)
            HeadCache[hireId] = sp;
        return sp;
    }

    public static Sprite GetStand(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId) ?? MercRosterDefs.NormalizeHireId(hireIdOrAssetId) ?? hireIdOrAssetId;
        if (string.IsNullOrEmpty(hireId)) return null;
        if (StandCache.TryGetValue(hireId, out var cached) && cached != null)
            return cached;

        // 2026-09-28 主人拍板：编辑器不再优先读美术源目录，改走 Resources，缺图露白框。
        Sprite sp = null;
#if UNITY_EDITOR
        if (DeviceParity.EditorFallbackEnabled)
            sp = LoadEditorStand(hireId);
#endif
        // 老板娘专属立绘在 Resources/Art/UI/Tavern/，不在 MercStand 目录（2026-10-15 接上）
        if (sp == null && hireId == "landlady")
            sp = LoadFromResources("UI/Art/Tavern", "酒吧老板娘");
        if (sp == null)
            sp = LoadFromResources(ContentPaths.Icons.MercStand, hireId);
        if (sp == null)
            DeviceParity.ReportMissing(
                hireId == "landlady" ? "UI/Art/Tavern/酒吧老板娘" : ContentPaths.Icons.MercStand + "/" + hireId,
                "佣兵立绘");
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
        // 老板娘专属立绘（2026-10-15）：只在 Resources/Art/UI/Tavern/，不复制进美术源目录
        if (hireId == "landlady")
            return LoadEditorSprite("Assets/Resources/UI/Art/Tavern/酒吧老板娘.png");
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
