using UnityEditor;
using UnityEngine;

/// <summary>聚光灯 × TapPlay 上架自检与构建入口。</summary>
public static class SpotlightShipChecklist
{
    const string Text =
        "【聚光灯 × TapPlay】\n\n" +
        "完整清单：Docs/聚光灯_TapPlay上架清单.md\n\n" +
        "工程\n" +
        "1. Tools/Build/Spotlight Android APK → Builds/Android/PixelAdventure-Spotlight-android.apk\n" +
        "2. （可选）Tools/Build/Spotlight Windows64 → 双端奖\n" +
        "3. 宏 SPOTLIGHT_BUILD：禁云/广告；登录仅开始游戏；战前免费刷新一次\n" +
        "4. Android：IL2CPP + ARM64，Target SDK 34，ForceInternetPermission=0\n\n" +
        "你需要在 TapTap 做\n" +
        "A. 活动页报名组队（至 9/30）\n" +
        "B. DC 建游戏页、上传 APK、开 TapPlay、过稳定性（建议 ≤10/18）\n" +
        "C. 开发者中心→平台活动 投稿（≤10/21 12:00）\n" +
        "D. 开发日志 ≥5；试玩期有效试玩 ≥50（全程参与奖）\n\n" +
        "包名勿改：com.PixelAdventure.RiftBlade";

    [MenuItem("Tools/Build/聚光灯 TapPlay 清单")]
    public static void Show()
    {
        EditorUtility.DisplayDialog("聚光灯 × TapPlay", Text, "好的");
        Debug.Log("[Spotlight Checklist]\n" + Text);
    }

    [MenuItem("Tools/Build/Spotlight Android APK")]
    public static void BuildAndroid()
    {
        if (!EditorUtility.DisplayDialog("Spotlight Android",
                "将打 ARM64 Release 参赛包（临时启用 SPOTLIGHT_BUILD）。\n继续？", "构建", "取消"))
            return;
        CliAndroidBuild.BuildSpotlightApk();
    }

    [MenuItem("Tools/Build/Spotlight Windows64")]
    public static void BuildWindows()
    {
        if (!EditorUtility.DisplayDialog("Spotlight Windows64",
                "将打 StandaloneWindows64 参赛包（临时启用 SPOTLIGHT_BUILD）。\n继续？", "构建", "取消"))
            return;
        CliAndroidBuild.BuildSpotlightWindows64();
    }
}
