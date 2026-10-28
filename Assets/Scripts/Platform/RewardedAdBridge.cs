using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 激励视频广告桥：玩法层<b>唯一出口</b>（复活/体力/金币加号都走这里，不直接碰具体 SDK）。
/// 【2026-10-25 T2】TapADN（聚合）SDK 已集成（Dirichlet.Mediation，实现见 TapAdBridge）：
/// HasRealSdk 默认 true → Android 真机走真广告；编辑器/开发包回落本地模拟。
/// 注意 GameConfig.ADS_ENABLED=false 期间本类不会被任何业务调用（广告档折叠，线上行为不变）。
/// SpotlightBuild.ApplyRuntimeGuards 保留：聚光灯包（SPOTLIGHT_BUILD 宏）仍强制压回模拟路径。
///
/// 【2026-10-28 商业化加固（主人拍板「影响商业化的都改，不留隐患」）】
/// ① busy 并发守卫：受理中拒绝新请求 —— 修「初始化等待期连点 N 次 → 初始化完成后
///    并发拉起 N 个广告连播」与「快速双击并发调 ShowRewardVideoAutoAd（SDK 行为未定义）」。
///    守卫放在本层（玩法层唯一出口），一处覆盖复活/金币/体力及以后所有新点位。
/// ② 超时兜底：SDK 静默丢回调（无 OnError/OnAdClose/OnRewardVerify）时受理永久占用
///    = 复活按钮哑火（_reviveAdPending 永不复位）/ 加号点不动。现 BusyTimeoutSeconds 内
///    未收到回调 → 强制按失败收尾（回调 false、复位 busy），迟到的真回调被一次性包装丢弃。
/// ③ 一次性回调包装：先到者赢（真实回调 vs 超时兜底竞态），绝不双发奖/双 toast。
/// ④ 模拟路径收紧：正式包（非编辑器、非 Development Build）且非 Android → 回调 false 不发奖
///    （原先无条件模拟成功，微信小游戏包等非安卓正式包广告档等于白给）。
/// </summary>
public static class RewardedAdBridge
{
    /// <summary>是否接入真实广告 SDK。【2026-10-25 主人拍板「聚光灯已结束，转商业广告版」】默认 true 走 TapAdBridge。</summary>
    public static bool HasRealSdk { get; set; } = true;

    /// <summary>
    /// 广告受理中（拉起 → 回调结束）。busy 期间新请求直接忽略（不回调、不排队）——
    /// 正在受理的那次广告回调归来会自然复位；连点被当作「没点」。
    /// </summary>
    static bool _busy;

    /// <summary>本次受理开始的时间戳（unscaled；超时兜底用）。</summary>
    static float _busySince;

    /// <summary>
    /// 从受理到强制判失败的上限（秒）。覆盖最坏链路：SDK 初始化等待 45s（TapAdBridge 上限）
    /// + 广告加载 30s + 完整观看激励视频 30~60s + 回调泵回主线程。设宽防「误杀正常看完广告的玩家」；
    /// 超时只发生在 SDK 完全静默（丢回调）的异常场景。
    /// </summary>
    const float BusyTimeoutSeconds = 180f;

    /// <summary>协程宿主（纯跑超时守护协程，无逻辑；DDOL 防场景切换杀协程）。</summary>
    static MonoBehaviour Host
    {
        get
        {
            if (_host == null)
            {
                var go = new GameObject("RewardedAdBridgeHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<RewardedAdHost>();
            }
            return _host;
        }
    }
    static MonoBehaviour _host;

    sealed class RewardedAdHost : MonoBehaviour { }

    /// <summary>
    /// 请求播放激励视频；完成后回调 success（true = 核验通过可发奖）。
    /// 保证：onComplete 至多回调一次；busy 期间的重复点击不回调（也不产生第二次广告）。
    /// </summary>
    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
        AdKillSwitch.EnsureStarted();   // 兜底：任何路径到广告点都保证发起过拉取（幂等）

        if (SpotlightBuild.Enabled)
        {
            Debug.Log($"[RewardedAdBridge] Spotlight：拒绝广告 placement={placement}");
            onComplete?.Invoke(false);
            return;
        }

        // 【2026-10-28 远程急停】主人改 Gitee ads.json 即全网停广告，无需发版；
        // 玩家侧由各点位给「广告维护中」提示（本层只负责拒绝与埋点）。
        if (AdKillSwitch.AdsOff)
        {
            Debug.LogWarning($"[RewardedAdBridge] 广告已被远程急停（ads_off=true），拒绝 placement={placement}");
            Analytics.AdFail(placement, "remote_disabled");
            onComplete?.Invoke(false);
            return;
        }

        if (_busy)
        {
            // 并发/连点防护：受理中的那次广告会回调并复位 busy，本次点击静默忽略
            Debug.LogWarning($"[RewardedAdBridge] 广告受理中，忽略本次请求 placement={placement}（防并发，自 {Time.unscaledTime - _busySince:F0}s 前受理）");
            return;
        }

        // 【2026-10-26 修】只在安卓真机走真广告；编辑器/其它平台回落本地模拟。
        // 原先 HasRealSdk=true 直接进 TapAdBridge，其 #else 分支当帧回调 false，
        // 导致编辑器广告档 100% 失败，本类注释里的「回落模拟」实为死路。
        if (HasRealSdk && TapAdBridge.IsRealAdPlatform)
        {
            _busy = true;
            _busySince = Time.unscaledTime;
            var guarded = Once(placement, onComplete);
            Host.StartCoroutine(TimeoutGuard(placement, guarded));   // 超时兜底（SDK 丢回调时强收）
            TapAdBridge.ShowRewarded(placement, guarded);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RewardedAdBridge] 模拟激励视频成功: {placement}");
        onComplete?.Invoke(true);
#else
        // 【2026-10-28 商业化加固】正式包且非 Android（如未来的微信小游戏包）没有真广告实现：
        // 必须回调 false 不发奖 —— 原先无条件模拟成功 = 广告档奖励白给。
        Debug.LogError($"[RewardedAdBridge] 当前平台无广告实现（真广告仅 Android），拒绝发奖 placement={placement}");
        onComplete?.Invoke(false);
#endif
    }

    /// <summary>
    /// 一次性回调包装：先到者赢（真实回调 vs 超时兜底），后到的直接丢弃；首位回调同时复位 busy。
    /// </summary>
    static Action<bool> Once(string placement, Action<bool> onComplete)
    {
        return ok =>
        {
            if (!_busy) return;              // 已被超时兜底收尾 → 迟到的真回调丢弃，绝不双发奖
            _busy = false;
            Debug.Log($"[RewardedAdBridge] 广告收尾 placement={placement} ok={ok}");
            onComplete?.Invoke(ok);
        };
    }

    /// <summary>
    /// 超时守护：受理后 BusyTimeoutSeconds 内没收到任何 SDK 回调 → 强制按失败收尾
    /// （回调 false + 复位 busy），修复「SDK 静默丢回调 → 复活钮/加号永久哑火」。
    /// 正常路径 busy 先被真实回调复位，本协程空转到条件退出后不做任何事。
    /// </summary>
    static IEnumerator TimeoutGuard(string placement, Action<bool> guarded)
    {
        while (_busy && Time.unscaledTime - _busySince < BusyTimeoutSeconds)
            yield return null;
        if (_busy)
        {
            Debug.LogError($"[RewardedAdBridge] 广告 {BusyTimeoutSeconds}s 无任何 SDK 回调（静默丢回调），强制按失败收尾 placement={placement}");
            guarded?.Invoke(false);
        }
    }
}
