using UnityEngine;

/// <summary>剧情场景背景：从 Resources/UI/StoryBg 加载。</summary>
public static class StoryBackgrounds
{
    public const string GuildOffice = "guild_office";
    public const string GuildHall = "guild_hall";

    static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    public static string DisplayName(string id)
    {
        if (id == GuildOffice) return "会长办公室";
        if (id == GuildHall) return "公会大厅";
        return "";
    }

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out var cached) && cached != null)
            return cached;

        Sprite sp = Resources.Load<Sprite>("UI/StoryBg/" + id);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>("UI/StoryBg/" + id);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        if (sp != null) Cache[id] = sp;
        return sp;
    }
}
