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
        // 剧情立绘优先走 Story/Portraits，保证尺寸一致（Merc 立绘比例不同）
        var story = StoryAssetLoader.Load(StoryAssetLoader.Portraits, id);
        if (story != null) return story;
        var stand = MercPortraitSprites.GetStand(id);
        if (stand != null) return stand;
        return null;
    }
}
