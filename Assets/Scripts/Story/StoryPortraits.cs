using System.Collections.Generic;
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

    // —— 叙事 V2.0 新增角色 ——
    /// <summary>梅莉莎（M）：公会装备科记录员，装箱的人。第 3 章出场。立绘已到位。</summary>
    public const string Melissa = "melissa";
    /// <summary>裂缝意志：第 8 章 HAPPY 结局出场。已定案不出正式立绘，长期用占位图。</summary>
    public const string RiftWill = "riftwill";
    /// <summary>老板娘：酒馆 NPC。立绘复用前台小姐（MercPortraitSprites 已 alias 到 receptionist）。</summary>
    public const string Innkeeper = "innkeeper";

    /// <summary>
    /// 尚未制作正式立绘的角色。缺图时不打「疑似配错」的警告（占位图照常给）。
    /// 资源补齐后把对应 ID 从这里移除。
    /// </summary>
    static readonly HashSet<string> AwaitingArt = new HashSet<string>
    {
        RiftWill
    };

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var sp = MercPortraitSprites.GetStand(id);
        if (sp != null) return sp;

        // 没图就顶一张占位图：标着角色 ID，美术补齐后自动换真图，不用改代码。
        var ph = PlaceholderArt.Portrait(id);
        if (ph != null)
        {
            PlaceholderArt.ReportMissing("立绘", id);
            return ph;
        }

        if (AwaitingArt.Contains(id)) return null;
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
