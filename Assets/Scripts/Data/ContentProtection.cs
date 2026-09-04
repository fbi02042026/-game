/// <summary>
/// 内容保护总开关。开发期必须为 false（用户要求：做完项目再用稳妥方案加密）。
/// 禁止 Agent 未经用户同意改为 true。见 Docs/软著后开发备忘.md、.cursor/rules/content-protection-dev-off.mdc
/// </summary>
public static class ContentProtection
{
    /// <summary>false = 明文读写；仍可解密读取旧 PAT1 存档/表。用 readonly 避免编译器把关着时的分支标成「无法访问」。</summary>
    public static readonly bool Enabled = false;
}
