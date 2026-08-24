using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 剧情运行时资源入口。调用方只传稳定英文 ID。
/// </summary>
public static class StoryAssetLoader
{
    public const string Root = ContentPaths.Story.Root;
    public const string Portraits = ContentPaths.Story.Portraits;
    public const string Backgrounds = ContentPaths.Story.Backgrounds;
    public const string Props = ContentPaths.Story.Props;

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Load(string group, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string key = group + "/" + id;
        if (Cache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        string path = ContentPaths.Story.Root + "/" + group + "/" + id;
        Sprite sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            var listed = Resources.LoadAll<Sprite>(ContentPaths.Story.Root + "/" + group);
            if (listed != null)
            {
                for (int i = 0; i < listed.Length; i++)
                {
                    if (listed[i] != null && listed[i].name == id)
                    {
                        sp = listed[i];
                        break;
                    }
                }
            }
        }
        if (sp == null)
            sp = SpriteFromTexture(path);
#if UNITY_EDITOR
        if (sp == null && group == Portraits)
            sp = LoadEditorPortrait(id);
        if (sp == null && group == Backgrounds)
            sp = LoadEditorSprite("Assets/Art/UI/Story/bg_" + id + ".png");
        if (sp == null && group == Props)
        {
            if (id == "speech_bubble")
                sp = LoadEditorSprite("Assets/Art/UI/Story/ui_speech_bubble.png");
            else if (id == "quest_paper")
                sp = LoadEditorSprite("Assets/Art/UI/Story/ui_quest_paper.png");
        }
#endif
        if (sp != null)
            Cache[key] = sp;
        else
            Debug.LogWarning("[StoryAsset] missing " + path);
        return sp;
    }

    static Sprite SpriteFromTexture(string resourcesPath)
    {
        var tex = Resources.Load<Texture2D>(resourcesPath);
        if (tex == null || tex.width < 2 || tex.height < 2)
            return null;
        try
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

#if UNITY_EDITOR
    static Sprite LoadEditorPortrait(string id)
    {
        return LoadEditorSprite("Assets/Art/UI/Story/portrait_" + id + ".png");
    }

    static Sprite LoadEditorSprite(string assetPath)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null || tex.width < 2) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
    }
#endif
}
