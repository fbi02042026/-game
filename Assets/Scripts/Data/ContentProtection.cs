/// <summary>
/// 内容保护总开关。开发期必须为 false（用户要求：做完项目再用稳妥方案加密）。
/// 禁止 Agent 未经用户同意改为 true。见 Docs/软著后开发备忘.md、.cursor/rules/content-protection-dev-off.mdc
/// </summary>
public static class ContentProtection
{
    /// <summary>false = 明文读写；仍可解密读取旧 PAT1 存档/表。</summary>
    public const bool Enabled = false;
}
