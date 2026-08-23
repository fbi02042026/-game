using UnityEditor;
using UnityEngine;

/// <summary>像素冒险:裂缝之刃 — 主路径手测清单（V0.3.5）。</summary>
public static class MainPathPlaytestChecklist
{
    const string Checklist =
        "【像素冒险:裂缝之刃 V0.3.5 主路径验收 — 团结 1.10.0】\n\n" +
        "资源\n" +
        "1. 大厅顶栏金币/体力与存档一致\n" +
        "2. 金币+ / 体力+ 可见；激励模拟发放并 Toast\n" +
        "3. Toast 在大厅/酒馆可见\n" +
        "4. 离线≥1 分钟回城：弹「已到账」确定窗\n\n" +
        "城镇\n" +
        "5. 五底栏：公会/角色/冒险/酒馆/日志\n" +
        "6. 标题/日志文案为「像素冒险:裂缝之刃」\n" +
        "7. 冒险仅主线（活动副本隐藏）；酒馆「佣兵招募」为三选一（同形象可异名/级/星/技能）\n" +
        "8. 武器库/执照/公告栏仍隐藏\n\n" +
        "战斗闭环\n" +
        "9. Boot→Town→引导战→撤离回城\n" +
        "10. 冒险开战耗体力；战前三选一后遗产池减少\n" +
        "11. 清关→宝箱→三选一/折金 Toast→chuansongmen→石墩轮盘\n" +
        "12. 轮盘仅出现：普通/精英/恢复/Boss\n" +
        "13. 恢复关：用 Resources/RestStagePopup 预制体回血+材料\n" +
        "14. 佣兵按名册出战（等级/技能来自存档）；无「即将开放」误点\n" +
        "15. 角色页 SPUM 与战斗换装一致；装备均有 spumName\n" +
        "16. 重开客户端：金/体/遗产/佣兵仍在\n\n" +
        "软著：Docs/软著申请材料_像素冒险裂缝之刃_V0.3.5.md\n" +
        "源码鉴别：Docs/软著源码鉴别/\n" +
        "功能截图待办：Docs/功能截图待办/README.md\n" +
        "APK：Tools/Build/Android Release APK";

    [MenuItem("Tools/自检/主路径验收清单")]
    public static void Show()
    {
        EditorUtility.DisplayDialog("主路径验收 V0.3.5", Checklist, "好的");
        Debug.Log("[Checklist]\n" + Checklist);
    }

    [MenuItem("Tools/_归档/自检/P0 主路径验收清单")]
    public static void ShowLegacy() => Show();

    [MenuItem("Tools/Build/Android Release APK")]
    public static void BuildRelease() => CliAndroidBuild.BuildReleaseApk();

    [MenuItem("Tools/Build/Android Dev APK")]
    public static void BuildDev() => CliAndroidBuild.BuildDevApk();
}
