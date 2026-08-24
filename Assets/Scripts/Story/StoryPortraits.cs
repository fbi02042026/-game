using UnityEngine;

/// <summary>剧情立绘 ID。资源在 Resources/Story/Portraits。</summary>
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

    public static Sprite Get(string id)
    {
        return StoryAssetLoader.Load(StoryAssetLoader.Portraits, id);
    }
}
