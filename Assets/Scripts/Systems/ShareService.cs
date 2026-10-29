using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 系统分享 + 每日分享奖励（2026-10-25 主人拍板：做分享、不做邀请码、不加验证、每日一次发 100 钻）。
///
/// 流程：点分享钮 → #share_click → 唤起 Android 系统分享面板 →
/// 唤起成功记 #share_launch → 当日首次再发 100 钻（局外账，ResourceWallet.Add 唯一出口）并记 #share_reward。
///
/// 【带图分享 2026-10-29 主人拍板：统一竖图、不分途径】
/// 系统分享面板（createChooser）拿不到「用户选了哪个 App」，自动按途径切图做不到；
/// 统一带 <see cref="ShareImageResPath"/>（608×1080 竖版海报，适配聊天/手机看图）。
/// 链路：Resources 读图（meta isReadable=1）→ EncodeToJPG → 写 persistentDataPath/share_poster.jpg
/// → 经 <see cref="ShareFileProvider"/> 发 content:// Uri（Android 7+ 禁 file://，必须 provider 授权）。
/// 微信 ACTION_SEND 图文同发只收图丢字 —— 海报图上自带宣传语，可接受；QQ 等图文都收。
/// 图准备失败 = LogError 可见 + 降级纯文本照发（分享是外向功能，图挂不挡发奖链路；
/// 这是「分享功能降级」不是资源断链兜底，别拿铁律 14 混淆）。
///
/// 【口径】
/// 1. #share_launch = 分享面板唤起成功，**不是**「用户真的发出去了」（Android Intent 拿不到该回调，
///    主人拍板不加验证，按唤起成功即发奖）。
/// 2. 奖励日键 = SaveData.shareRewardDayKey（本地日 yyyyMMdd，跨天可再领，模式同 ResourceAdRewards.EnsureDay；
///    2026-10-28 由 UTC 改本地日，与每日登录/商店 0 点刷新同口径）。
/// 3. 非安卓真机（Editor/自测）：视为唤起成功（Debug.Log 注明测试口径），让奖励链路在 Editor 可验。
/// 4. 钻石入账失败（added<=0）只 LogError 不回滚日键 —— 防当天反复重试刷报错，次日自动恢复。
/// </summary>
public static class ShareService
{
    /// <summary>分享文案（纯文本带游戏名；主人未给落地页，不带链接）。</summary>
    public const string ShareText = "我正在玩《像素冒险：裂隙之刃》，割草爆装超解压，快来一起冒险吧！";

    /// <summary>分享竖图 Resources 相对路径（无扩展名；2026-10-29 主人拍板统一竖图）。</summary>
    public const string ShareImageResPath = "UI/Art/分享/分享竖";

    /// <summary>分享图落盘文件名（与 ShareFileProvider.java 的 SHARE_FILE_NAME 同值，改要一起改）。</summary>
    public const string ShareImageFileName = "share_poster.jpg";
    // authority 三处同值锚：ShareFileProvider.java 的 AUTHORITY ↔ AndroidManifest.xml 的 provider
    // authorities（"com.pixeladventure.riftblade.sharefile"）；带图 Intent 组装在 Java 侧，C# 无需引用。

    /// <summary>【2026-10-29 主人拍板】当日分享奖励「本次发放成功」后触发 ——
    /// 战斗结果界面订阅它切「分享过后」排版（隐藏分享按钮、确定钮居中）。
    /// 只在本次真的发了奖时触发；打开界面时已是已领态由界面自查 IsRewardClaimedToday，不走这里。</summary>
    public static event Action ShareRewardGranted;

    /// <summary>今日分享奖励是否已领（唯一判定出口；SharePanelUI 已领态共用，同语义单一出口）。
    /// 【2026-10-28 升级兼容】旧 UTC 日键在本地 0~8 点 == 本地今天 → 视同已领，防止升级当天多领 100 钻。</summary>
    public static bool IsRewardClaimedToday(SaveData data)
        => data != null && (data.shareRewardDayKey == DateTime.Now.ToString("yyyyMMdd")
                            || ShopDefs.LegacyUtcDayKeyStillToday(data.shareRewardDayKey));

    /// <summary>分享入口唯一出口：埋点 → 唤起系统分享面板 → 当日首次发奖。sceneTag = 埋点 #scene。</summary>
    public static void TryShareAndReward(string sceneTag)
    {
        GameAnalytics.ReportShareClick("battle_report", sceneTag);

        if (!LaunchSystemShare(ShareText))
            return;   // 唤起失败已 LogError，不发奖、不记 launch

        GameAnalytics.ReportShareLaunch("battle_report");

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[ShareService] SaveData 为空，分享奖励落不了账（不发不吞）");
            return;
        }

        if (IsRewardClaimedToday(data))
        {
            UIManager.Instance?.ShowToast("今日分享奖励已领过，明天再来吧");
            return;
        }

        data.shareRewardDayKey = DateTime.Now.ToString("yyyyMMdd");
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, GameConfig.SHARE_REWARD_DIAMOND,
                                   save: true, notify: false);
        if (r.added <= 0)
        {
            Debug.LogError("[ShareService] 分享奖励入账 0（钻石账户异常？），日键已写防止当天反复重试");
            return;
        }
        UIManager.Instance?.ShowToast($"分享成功：钻石 +{r.added}");
        GameAnalytics.ReportShareReward("diamond", (int)r.added);
        ShareRewardGranted?.Invoke();   // 【2026-10-29】通知订阅方（战斗结果界面）：分享过后 → 切居中排版
    }

    /// <summary>
    /// 准备分享图：Resources 读竖图 → EncodeToJPG(85) → 写 persistentDataPath/{ShareImageFileName}。
    /// 每次分享前覆盖写（图更新后不会 stale；几百 KB 开销可忽略）。
    /// 失败 LogError 可见并返回 false（调用方降级纯文本，见类注释「分享功能降级」）。
    /// </summary>
    static bool TryPrepareShareImage()
    {
        var tex = Resources.Load<Texture2D>(ShareImageResPath);
        if (tex == null)
        {
            Debug.LogError("[ShareService] 分享竖图取不到：Resources/" + ShareImageResPath +
                           "（图被移走/改名？meta isReadable 未开也一样能 Load 到但编码会炸，见下一条）");
            return false;
        }
        byte[] jpg;
        try
        {
            jpg = tex.EncodeToJPG(85);   // meta isReadable=0 时这里抛异常 —— LogError 提示查 meta
        }
        catch (Exception e)
        {
            Debug.LogError("[ShareService] 分享图编码 JPG 失败（八成是 分享竖.jpg.meta 的 isReadable 没开）：" + e.Message);
            return false;
        }
        if (jpg == null || jpg.Length == 0)
        {
            Debug.LogError("[ShareService] 分享图编码结果为空，降级纯文本分享");
            return false;
        }
        try
        {
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, ShareImageFileName), jpg);
        }
        catch (Exception e)
        {
            Debug.LogError("[ShareService] 分享图写盘失败（" + Application.persistentDataPath + "）：" + e.Message);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 唤起 Android 系统分享面板（ACTION_SEND + createChooser）。
    /// 【2026-10-29 带图版】图就绪 → Java 侧 <see cref="ShareFileProvider"/>.launchImageShare
    ///（带图 Intent 组装全在 Java 编译期确定，JNI 只跨一个 (String,String)->bool，见该文件头注释）；
    /// 返回 false / 图没就绪 → LogError 可见 + 降级旧纯文本，分享与发奖链路不断。
    /// 团结沿用 com.unity3d.player.UnityPlayer 取 currentActivity；真机唤不起时优先查这里（验收项）。
    /// </summary>
    static bool LaunchSystemShare(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            bool hasImg = TryPrepareShareImage();
            if (hasImg)
            {
                bool launched;
                using (var jp = new AndroidJavaClass("com.PixelAdventure.RiftBlade.ShareFileProvider"))
                    launched = jp.CallStatic<bool>("launchImageShare", text, "分享");
                if (launched) return true;
                Debug.LogError("[ShareService] 带图分享唤起失败（Java 侧返回 false，logcat 查 Throwable），降级纯文本");
            }
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "分享"))
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call<AndroidJavaObject>("startActivity", chooser);
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShareService] 唤起系统分享面板失败：{e.Message}");
            return false;
        }
#else
        Debug.Log("[ShareService] 非安卓真机（Editor 测试口径）：视为分享面板唤起成功");
        return true;
#endif
    }
}
