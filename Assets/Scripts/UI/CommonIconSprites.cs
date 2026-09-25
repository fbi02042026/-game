using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 通用图标加载器：Resources 优先 + 编辑器回退 Assets/Art/UI/Icons/Common。
/// 供每日登录等界面统一加载 Common 目录下、没有 Resources 副本的图标
/// （金币 icon_gold / 钻石 icon_diamond_blue / 天赋石 icon_talent_stone / 强化石 icon_enchant_stone /
///  分解材料 icon_decompose_mat / 体力等），避免各处重复造轮子。
/// 内部带静态缓存，重复调用不会重复加载。
/// </summary>
public static class CommonIconSprites
{
    /// <summary>Art 源目录（编辑器回退用，缺 Resources 副本时也能立刻看到效果）。</summary>
    const string ArtRoot = "Assets/Art/UI/Icons/Common/";
    /// <summary>Resources 目录（打包后直连，但当前无副本，主要走编辑器回退）。</summary>
    const string ResRoot = "UI/Icons/Common/";

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 按文件名（不含扩展名）加载 Common 目录图标。
    /// 加载顺序：Resources/UI/Icons/Common（Load → LoadAll → Texture2D 造 Sprite）；
    /// 编辑器下再回退 Assets/Art/UI/Icons/Common（AssetDatabase 逐级尝试）。
    /// 找不到返回 null；结果按文件名缓存。
    /// </summary>
    public static Sprite Load(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        Sprite cached;
        if (Cache.TryGetValue(fileNameWithoutExt, out cached)) return cached;

        Sprite sp = LoadFromResources(fileNameWithoutExt);
        if (sp == null)
        {
#if UNITY_EDITOR
            sp = LoadFromArt(fileNameWithoutExt);
#endif
        }
        Cache[fileNameWithoutExt] = sp;
        return sp;
    }

    static Sprite LoadFromResources(string name)
    {
        string resPath = ResRoot + name;
        var sp = Resources.Load<Sprite>(resPath);
        if (sp != null) return sp;

        var all = Resources.LoadAll<Sprite>(resPath);
        if (all != null && all.Length > 0)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return all[0];
        }

        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null) return MakeSprite(tex, name);
        return null;
    }

#if UNITY_EDITOR
    static Sprite LoadFromArt(string name)
    {
        string assetPath = ArtRoot + name + ".png";
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
        if (edTex != null) return MakeSprite(edTex, name);
        return null;
    }
#endif

    static Sprite MakeSprite(Texture2D tex, string name)
    {
        var made = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        made.name = name;
        return made;
    }
}
