/// <summary>
/// TapADN（聚合）接入配置 —— 唯一真源（2026-10-25 T2）。
/// 后台：ssp.dirichlet.cn（Dirichlet 媒体平台，测试媒体）。
/// ⚠ MediaKey 属敏感配置：只允许出现在本文件，禁止复制进文档 / 提交说明 / 日志输出。
/// SDK 包：dirichlet_mediation_unity_5.3.1.6.unitypackage（命名空间 Dirichlet.Mediation）。
/// </summary>
public static class TapAdConfig
{
    /// <summary>媒体 ID（流量管理→媒体管理，测试媒体「像素冒险，裂隙之刃」）。</summary>
    public const long MediaId = 1112901L;

    /// <summary>媒体密钥（后台媒体管理行内查看；客户端 SDK 初始化必填，本就随包分发，非服务端机密）。</summary>
    public const string MediaKey = "RFZvN7bXVuKfCoSRovOEfIqAjDqeAGQK244fc8DESKoNv0zelvRA38JQQojXUeNo";

    /// <summary>媒体名称（与后台媒体管理显示一致）。</summary>
    public const string MediaName = "像素冒险，裂隙之刃";

    /// <summary>激励视频推广位 ID（推广位管理，「激励视频」测试位，Android 竖屏）。</summary>
    public const long RewardVideoSpaceId = 1072474L;
}
