using UnityEngine;

/// <summary>剧情立绘：从 Resources/UI/Portraits 加载。</summary>
public static class StoryPortraits
{
    public const string Player = "player";
    public const string Receptionist = "receptionist";
    public const string GuildMaster = "guildmaster";
    public const string Hunter = "hunter";
    /// <summary>老盾剧情立绘（立绘_老盾）</summary>
    public const string LaoDun = "laodun";

    static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out var cached) && cached != null)
            return cached;

        Sprite sp = Resources.Load<Sprite>("UI/Portraits/" + id);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>("UI/Portraits/" + id);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#if UNITY_EDITOR
        if (sp == null && id == LaoDun)
        {
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/剧情/立绘_老盾.png");
            if (sp == null)
            {
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/剧情/立绘_老盾.png");
                if (tex != null)
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
#endif
        if (sp != null) Cache[id] = sp;
        return sp;
    }
}
