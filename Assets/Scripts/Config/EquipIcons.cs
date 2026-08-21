using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 装备图标：统一从 Art/UI/Icons/EquipIcons 加载。
/// 装备模板填 iconFileName（不含 .png），或在编辑器运行「Tools/装备/绑定 EquipIcons」写入 icon。
/// </summary>
public static class EquipIcons
{
    public const string Root = "Assets/Art/UI/Icons/EquipIcons/";

    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        if (_cache.TryGetValue(fileNameWithoutExt, out var cached) && cached != null)
            return cached;

        string path = Root + fileNameWithoutExt + ".png";
        Sprite sp = Load(path);
        _cache[fileNameWithoutExt] = sp;
        return sp;
    }

    static Sprite Load(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        return Resources.Load<Sprite>("UI/EquipIcons/" + file);
#endif
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
