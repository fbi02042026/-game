using UnityEngine;

/// <summary>
/// 剧情立绘 ID。全部走佣兵立绘（<see cref="MercPortraitSprites.GetStand"/>），
/// 不再读 Resources/Story/Portraits。
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
        var sp = MercPortraitSprites.GetStand(id);
        if (sp != null) return sp;
        Debug.LogWarning("[StoryPortraits] missing MercStand for id=" + id);
        return null;
    }

    /// <summary>预热立绘缓存，避免开场卡顿。</summary>
    public static void Warmup(params string[] ids)
    {
        if (ids == null) return;
        for (int i = 0; i < ids.Length; i++)
            Get(ids[i]);
    }
}
