using System.Collections.Generic;
using UnityEngine;

/// <summary>剧情道具图：从 Resources/UI/StoryProps 加载。</summary>
public static class StoryProps
{
    public const string QuestPaper = "quest_paper";

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out var cached) && cached != null)
            return cached;

        Sprite sp = Resources.Load<Sprite>("UI/StoryProps/" + id);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>("UI/StoryProps/" + id);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        if (sp != null) Cache[id] = sp;
        return sp;
    }
}
