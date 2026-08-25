using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 装备图标：优先 Resources/UI/EquipIcons（真机），编辑器再回退 Art/UI/Icons/EquipIcons。
/// </summary>
public static class EquipIcons
{
    public const string Root = "Assets/Art/UI/Icons/EquipIcons/";
    public const string ResourcesPath = "UI/EquipIcons/";

    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        if (_cache.TryGetValue(fileNameWithoutExt, out var cached))
        {
            if (cached != null) return cached;
            _cache.Remove(fileNameWithoutExt);
        }

        Sprite sp = Load(fileNameWithoutExt);
        if (sp != null)
            _cache[fileNameWithoutExt] = sp;
        return sp;
    }

    static Sprite Load(string file)
    {
        // 1) 直接当 Sprite
        var res = Resources.Load<Sprite>(ResourcesPath + file);
        if (res != null) return res;

        // 2) 图集/多子资源：LoadAll 再按名匹配
        var all = Resources.LoadAll<Sprite>(ResourcesPath + file);
        if (all != null && all.Length > 0)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == file)
                    return all[i];
            }
            return all[0];
        }

        // 3) 仅 Texture2D：运行时补 Sprite
        var tex = Resources.Load<Texture2D>(ResourcesPath + file);
        if (tex != null)
        {
            var made = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            made.name = file;
            return made;
        }

        // 4) 文件夹整体 LoadAll 再找同名（防路径大小写/后缀差异）
        var folderSprites = Resources.LoadAll<Sprite>("UI/EquipIcons");
        if (folderSprites != null)
        {
            for (int i = 0; i < folderSprites.Length; i++)
            {
                var s = folderSprites[i];
                if (s == null) continue;
                if (string.Equals(s.name, file, System.StringComparison.OrdinalIgnoreCase))
                    return s;
            }
        }

#if UNITY_EDITOR
        string assetPath = Root + file + ".png";
        var ed = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (ed != null) return ed;
        var edAll = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (edAll != null)
        {
            for (int i = 0; i < edAll.Length; i++)
            {
                if (edAll[i] is Sprite spEd) return spEd;
            }
        }
        var edTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (edTex != null)
        {
            var made = Sprite.Create(
                edTex,
                new Rect(0f, 0f, edTex.width, edTex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            made.name = file;
            return made;
        }
#endif
        Debug.LogWarning($"[EquipIcons] 未找到图标: {file}");
        return null;
    }

    public static void ClearCache() => _cache.Clear();

#if UNITY_EDITOR
    public static string[] GetAllFileNames()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') });
        var names = new List<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
            names.Add(System.IO.Path.GetFileNameWithoutExtension(p));
        }
        names.Sort();
        return names.ToArray();
    }
#endif
}
