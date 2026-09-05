#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Art/UI/Icons/佣兵头像、佣兵立绘 同步到 Resources/Icons/MercHead、MercStand。
/// </summary>
public static class MercPortraitSyncTool
{
    const string ArtHeadDir = "Assets/Art/UI/Icons/佣兵头像";
    const string ArtStandDir = "Assets/Art/UI/Icons/佣兵立绘";
    const string ResHeadDir = "Assets/Resources/Icons/MercHead";
    const string ResStandDir = "Assets/Resources/Icons/MercStand";

    [MenuItem("Tools/UI/同步佣兵头像立绘到 Resources")]
    public static void SyncAll()
    {
        EnsureFolder("Assets/Resources/Icons");
        EnsureFolder(ResHeadDir);
        EnsureFolder(ResStandDir);

        int headCount = SyncHeads();
        int standCount = SyncStands();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("佣兵头像立绘",
            $"已同步到 Resources。\n头像：{headCount} 张\n立绘：{standCount} 张",
            "OK");
        Debug.Log($"[MercPortraitSyncTool] head={headCount} stand={standCount}");
    }

    [MenuItem("Tools/UI/修复佣兵立绘导入(防拉伸)")]
    public static void FixImportSettings()
    {
        int n = 0;
        n += FixImportsInFolder(ResStandDir);
        n += FixImportsInFolder(ResHeadDir);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("佣兵立绘导入",
            $"已检查并修复 {n} 张（Sprite + nPOT=None，禁止拉伸）。\n请等 Unity 重新导入后再进游戏。",
            "OK");
        Debug.Log($"[MercPortraitSyncTool] fixed imports={n}");
    }

    static int FixImportsInFolder(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        int n = 0;
        string root = Directory.GetCurrentDirectory().Replace('\\', '/');
        foreach (string file in Directory.GetFiles(dir, "*.png"))
        {
            string assetPath = file.Replace('\\', '/');
            if (assetPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                assetPath = assetPath.Substring(root.Length + 1);
            int idx = assetPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) assetPath = assetPath.Substring(idx);
            EnsureSpriteImport(assetPath);
            n++;
        }
        return n;
    }

    static int SyncHeads()
    {
        if (!Directory.Exists(ArtHeadDir)) return 0;
        int n = 0;
        foreach (string file in Directory.GetFiles(ArtHeadDir, "*.png"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string destId = fileName == "玩家" ? "player" : fileName;
            if (CopySprite(file, ResHeadDir + "/" + destId + ".png"))
                n++;
        }
        return n;
    }

    static readonly (string ArtName, string DestId)[] StoryStandExtras =
    {
        ("前台小姐", "receptionist"),
        ("会长——大众", "guildmaster"),
        ("会长——阴暗", "guildmaster_hidden"),
    };

    static int SyncStands()
    {
        if (!Directory.Exists(ArtStandDir)) return 0;
        int n = 0;
        foreach (string file in Directory.GetFiles(ArtStandDir, "*.png"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.StartsWith("佣兵立绘_")) continue;
            string destId = fileName.Substring("佣兵立绘_".Length);
            if (destId == "玩家") destId = "player";
            if (CopySprite(file, ResStandDir + "/" + destId + ".png"))
                n++;
        }
        for (int i = 0; i < StoryStandExtras.Length; i++)
        {
            string src = ArtStandDir + "/" + StoryStandExtras[i].ArtName + ".png";
            string dest = ResStandDir + "/" + StoryStandExtras[i].DestId + ".png";
            if (CopySprite(src, dest))
                n++;
        }
        return n;
    }

    static bool CopySprite(string src, string dest)
    {
        if (!File.Exists(src)) return false;
        string destDir = Path.GetDirectoryName(dest);
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(src, dest, true);
        string metaSrc = src + ".meta";
        string metaDest = dest + ".meta";
        if (File.Exists(metaSrc))
            File.Copy(metaSrc, metaDest, true);
        else
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);

        EnsureSpriteImport(dest);
        return true;
    }

    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        // NPOT→二次幂会把立绘非等比拉伸；导入必须 Sprite + nPOT=None
        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            dirty = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            dirty = true;
        }
        if (dirty)
            importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
