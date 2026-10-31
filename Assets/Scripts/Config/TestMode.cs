using UnityEngine;

/// <summary>
/// 【2026-10-31 主人拍板】测试开关：让新号能测所有功能，打包上线时关闭。
///
/// - 开关本体存 PlayerPrefs（key "test.mode"），在设置弹窗「测试模式」行拨动，重启后保持。
/// - 总闸只有一个：ALLOW_TEST_MODE 常量。
///   【2026-10-31 二次修正】不再叠加 Debug.isDebugBuild 保险——打包走编辑器 GUI（build_android.bat
///   在本机挂死不可用），出的是非 Development 包，isDebugBuild 恒 false 会把测试包里的开关也掐死。
///   现在所有包行为一致：常量 = true 时开关可用，false 时整行消失、开关失效。
///   🔴 上线正式包打包前必须把它改成 false（唯一动作）。
/// - 生效时：FeatureUnlock 功能门控全部直通；体力不扣（StaminaSystem.TrySpend 直通）。
/// </summary>
public static class TestMode
{
    /// <summary>🔴 上线正式包前改成 false（总闸，设置弹窗里的开关行也会隐藏）。</summary>
    public const bool ALLOW_TEST_MODE = true;

    const string KEY = "test.mode";

    /// <summary>测试模式当前是否生效（总闸常量 + 设置里的拨动，两者都开才算开）。</summary>
    public static bool Enabled
    {
        get => ALLOW_TEST_MODE && PlayerPrefs.GetInt(KEY, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(KEY, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>设置弹窗里是否显示「测试模式」行（只看总闸常量；上线改 false 后整行消失）。</summary>
    public static bool Visible => ALLOW_TEST_MODE;
}
