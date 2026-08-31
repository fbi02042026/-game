using UnityEngine;

/// <summary>
/// 主角身份文案。显示名优先读存档（引导签名），否则用默认「莱恩」。
/// </summary>
public static class PlayerIdentity
{
    public const string DefaultName = "\u83b1\u6069";
    public const string Title = "\u89c1\u4e60";
    public const string FullTitle = "\u89c1\u4e60\u5192\u9669\u8005";

    public static string DisplayName => StoryProgress.GetPlayerName();
}
