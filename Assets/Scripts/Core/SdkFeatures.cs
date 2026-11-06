/// <summary>
/// SDK 运行时只读开关出口（见 Docs/SDK统一开关规范.md 2.4）。
/// 真值来自 Scripting Define Symbol；零引用 Editor 程序集类型与任何 SDK 类型，避免 CS0246。
/// 宏名约定与 Editor 侧登记表重复，靠规范与出包日志校验。
/// </summary>
public static class SdkFeatures
{
#if TAPSDK_ENABLED
    public const bool TapTapEnabled = true;
#else
    public const bool TapTapEnabled = false;
#endif
}
