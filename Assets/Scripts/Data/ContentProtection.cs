/// <summary>
/// 内容保护总开关。开发期必须为 false（用户要求：做完项目再用稳妥方案加密）。
/// 禁止 Agent 未经用户同意改为 true。见 Docs/软著后开发备忘.md、.cursor/rules/content-protection-dev-off.mdc
/// </summary>
public static class ContentProtection
{
    /// <summary>false = 明文读写；仍可解密读取旧 PAT1 存档/表。用 readonly 避免编译器把关着时的分支标成「无法访问」。</summary>
    public static readonly bool Enabled = false;

    /// <summary>
    /// 【2026-10-31】存档加密独立开关（与上面的「表加密 + 指纹校验」解耦）。
    /// true = player.dat / 云存档载荷走 <see cref="SecureCodec"/>（AES-256-CBC + HMAC-SHA256）；
    /// 旧明文存档仍能正常读（<c>SecureCodec.TryReadPayload</c> 有 UTF8 回退），不会触发清档。
    /// 为什么单独开一个：<see cref="Enabled"/> 一旦为 true 会连带启用
    /// <see cref="ConfigFingerprint.VerifyLoaded"/>（要求重新 Cook 指纹表），
    /// 只想防改档时不该被表管线绑住。
    /// 🔴 默认 false：本文件顶部规矩是「未经主人同意不得改 true」，此处同口径。
    /// 主人说「开」时只改这一行，不需要动表管线。
    /// </summary>
    public static readonly bool SaveEncryption = false;
}
