using UnityEngine;

/// <summary>
/// 剧情立绘 ID。优先佣兵立绘 H/C 编号，回退 Resources/Story/Portraits。
/// 引导三人（会长 / 咨询台 / 玩家）为统一尺寸全身图；布局见 <see cref="StoryPortraitLayout"/>。
/// </summary>
public static class StoryPortraits
{
    public const string Player = "player";
    public const string Receptionist = "receptionist";
    public const string GuildMaster = "guildmaster";
    public const string GuildMasterHidden = "guildmaster_hidden";
    public const string Hunter = "hunter";
    public const string Xiaomei = "xiaomei";
    public const string Altor = "altor";
    public const string LaoDun = "laodun";
    public const string Grey = "grey";

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var story = StoryAssetLoader.Load(StoryAssetLoader.Portraits, id);
        if (story != null) return story;
        Debug.LogWarning("[StoryPortraits] missing Resources/Story/Portraits/" + id);
        return null;
    }
}
