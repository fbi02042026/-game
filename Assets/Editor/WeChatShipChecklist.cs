using UnityEditor;
using UnityEngine;

/// <summary>P5 微信定版前检查项（不改 plan）。</summary>
public static class WeChatShipChecklist
{
    const string Text =
        "【P5 微信小游戏定版前】\n\n" +
        "1. 分辨率：GameConfig 720×1280；Boot/战斗启动调用 WeChatMiniGameConfig.EnsureDesignResolution\n" +
        "2. 云存档：SaveSystem.UploadToCloud / DownloadFromCloud → CloudSaveBridge（先本地镜像）\n" +
        "3. 激励广告：PreLevel 刷新 → RewardedAdBridge（上架前设 HasRealSdk）\n" +
        "4. 分包：主包软上限 WeChatMiniGameConfig.MainPackageSoftLimitMb=4；大图/音频进分包\n" +
        "5. 性能：GamePerf.VerboseLog=false；VFX 逐步入 PoolManager\n\n" +
        "菜单可再跑 Tools/Build/Android Release APK 做包体对照。";

    [MenuItem("Tools/_归档/自检/P5 微信上架清单")]
    public static void Show()
    {
        EditorUtility.DisplayDialog("P5 微信上架", Text, "好的");
        Debug.Log("[P5 Checklist]\n" + Text);
    }
}
