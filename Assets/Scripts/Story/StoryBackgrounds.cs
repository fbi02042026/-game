using UnityEngine;

/// <summary>剧情场景背景 ID。资源在 Resources/Story/Backgrounds。</summary>
public static class StoryBackgrounds
{
    public const string GuildOffice = "guild_office";
    public const string GuildHall = "guild_hall";

    public static string DisplayName(string id)
    {
        if (id == GuildOffice) return "会长办公室";
        if (id == GuildHall) return "公会大厅";
        return "";
    }

    public static Sprite Get(string id)
    {
        var sp = StoryAssetLoader.Load(StoryAssetLoader.Backgrounds, id);
        if (sp != null) return sp;

        // 缺背景就顶一张占位图，避免整屏黑。
        var ph = PlaceholderArt.Background(id);
        if (ph != null) PlaceholderArt.ReportMissing("背景", id);
        return ph;
    }
}
