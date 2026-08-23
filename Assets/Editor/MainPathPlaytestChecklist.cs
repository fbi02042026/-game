using UnityEditor;
using UnityEngine;

/// <summary>软著演示版主路径手测清单。</summary>
public static class MainPathPlaytestChecklist
{
    const string Checklist =
        "【软著演示版 V0.3.2 主路径验收 — 团结 1.10.0】\n\n" +
        "资源\n" +
        "1. 大厅顶栏金币/体力与存档一致\n" +
        "2. 金币+ / 体力+ 可见；点后激励模拟发放并刷新顶栏\n" +
        "3. Toast 在大厅/酒馆可见（非仅 Console）\n" +
        "4. 离线≥1 分钟回城：弹「已到账」确定窗（非「领取」）\n\n" +
        "城镇\n" +
        "5. 五底栏：公会/角色/冒险/酒馆/日志均有内容页\n" +
        "6. 酒馆「招募弓手」可用；日志无「临时版」字样\n" +
        "7. 武器库可浏览遗产池\n\n" +
        "战斗闭环\n" +
        "8. Boot→Town→引导战→撤离回城\n" +
        "9. 冒险开战耗体力；战前三选一后遗产池减少\n" +
        "10. 清关→宝箱→三选一/丢弃 Toast→chuansongmen→石墩轮盘\n" +
        "11. 休息发材料 / 锻造或附魔有反馈\n" +
        "12. 佣兵出战；无商人/诅咒空壳过关；无「即将开放」误点\n" +
        "13. 重开客户端：金/体/遗产/佣兵仍在\n\n" +
        "APK：Tools/Build/Android Release APK\n" +
        "软著附图清单：Docs/软著附图/README.md";

    [MenuItem("Tools/自检/主路径验收清单")]
    public static void Show()
    {
        EditorUtility.DisplayDialog("软著主路径验收", Checklist, "好的");
        Debug.Log("[Checklist]\n" + Checklist);
    }

    [MenuItem("Tools/自检/P0 主路径验收清单")]
    public static void ShowLegacy() => Show();

    [MenuItem("Tools/Build/Android Release APK")]
    public static void BuildRelease() => CliAndroidBuild.BuildReleaseApk();

    [MenuItem("Tools/Build/Android Dev APK")]
    public static void BuildDev() => CliAndroidBuild.BuildDevApk();
}
