using System;
using UnityEngine;
#if UNITY_WECHATMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 【2026-11-02 P2-B】微信小游戏激励视频桥（C# 门面 → 官方 WX SDK）。
/// 上午版本是自写 jslib；主人当天装了官方 SDK（com.qq.weixin.minigame v0.1.32，团结转换版），
/// 经确认后改为直接调官方 API，jslib 已退役删除——单例管理、回调生命周期、isEnded 判定全由官方封装。
///
/// 设计：
/// - 仅微信小游戏构建（UNITY_WECHATMINIGAME）内走真实拉起；其它平台（编辑器/安卓/开发包）
///   本类 Available=false，调用方（RewardedAdBridge）会回落到既有的「暂未开放 / 模拟」逻辑，本类不重复实现。
/// - 广告实例只创建一次并缓存（param.multiton=false，官方单例口径）；
///   onClose 只挂一次、用 _pending 指针指向本次回调——规避官方文档警示的「监听器堆积 → 多次回调 → 双发奖」。
/// - 流程：Load 成功 → Show（show 失败官方口径重试一次 load+show）；收口只认 OnClose 的 isEnded
///   （res 为 null 或 isEnded=false 一律不发奖）。
///
/// 注意（接真机前必看）：
/// 1. adUnitId 由主人在 WeChatMiniGameConfig.RewardedAdUnitId 填入（流量主开通后拿到的广告位 id）。
///    为空时本类直接回调 false（不发奖），不会拉起空广告。
/// 2. 首次真机/微信开发者工具构建后，必须验证：广告能拉起、中途关闭不发奖、看完 isEnded 判 true 发奖。
/// </summary>
public static class WeChatAd
{
    /// <summary>是否处于微信小游戏运行时（编译期宏，非微信构建恒 false）。</summary>
    public static bool Available
    {
        get
        {
#if UNITY_WECHATMINIGAME
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>未配置广告位 id 时的提示（仅日志用）。</summary>
    const string NoUnitIdLog = "[WeChatAd] adUnitId 未配置（WeChatMiniGameConfig.RewardedAdUnitId 待流量主开通后填入），拒绝拉起";

#if UNITY_WECHATMINIGAME
    /// <summary>show 失败时按官方口径重试次数（load+show 整体重来一次）。</summary>
    const int MaxShowAttempts = 2;

    /// <summary>缓存的官方广告实例（单例口径，随 adUnitId 变更才重建）。</summary>
    private static WXRewardedVideoAd _ad;
    private static string _cachedUnitId;
    private static bool _closeHooked;
    private static int _showAttempt;

    /// <summary>本次拉起的发奖回调指针；非 null 表示一次拉起尚未收口。</summary>
    private static Action<bool> _pending;

    /// <summary>统一收口：回调只发一次，_pending 置空防重入。</summary>
    static void Complete(bool ok, string why)
    {
        var cb = _pending;
        _pending = null;
        if (cb == null) return; // 已被 show 失败路径收口过，OnClose 迟到时忽略
        if (!string.IsNullOrEmpty(why)) Debug.Log("[WeChatAd] " + why);
        try { cb(ok); }
        catch (Exception e) { Debug.LogError("[WeChatAd] 发奖回调异常: " + e); }
    }

    static void DoShow()
    {
        _ad.Show(
            onSuccess: _ =>
            {
                // 拉起成功；收口只认 OnClose(res.isEnded)，此处不做任何回调。
            },
            onFail: _ =>
            {
                // show 失败按官方建议重试一次：重新 load 再 show。
                if (++_showAttempt < MaxShowAttempts)
                {
                    Debug.Log("[WeChatAd] show 失败，重试 load+show（第 " + _showAttempt + " 次）");
                    _ad.Load(_ => DoShow(), _ => Complete(false, "show 失败且重试 load 失败，不发奖"));
                }
                else
                {
                    Complete(false, "show 失败（重试后仍失败），不发奖");
                }
            });
    }
#endif

    /// <summary>
    /// 拉起激励视频；ended=true 表示看完应发奖，false 表示中途关闭/失败/异常。
    /// 仅在 Available 时调用；非微信构建调用直接回调 false（不发奖）。
    /// </summary>
    public static void ShowRewarded(string adUnitId, Action<bool> onResult)
    {
#if UNITY_WECHATMINIGAME
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning(NoUnitIdLog);
            onResult?.Invoke(false);
            return;
        }
        if (_pending != null)
        {
            // 上游 RewardedAdBridge 有 busy 守卫，正常到不了这里；双保险防双发奖。
            Debug.LogWarning("[WeChatAd] 上一条激励视频尚未收口，忽略本次调用");
            onResult?.Invoke(false);
            return;
        }
        try
        {
            if (_ad == null || _cachedUnitId != adUnitId)
            {
                _ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
                {
                    adUnitId = adUnitId,
                    multiton = false, // 官方单例口径：小游戏端激励视频全局唯一，避免实例/监听器堆积
                });
                _cachedUnitId = adUnitId;
                _closeHooked = false;
            }
            if (!_closeHooked)
            {
                _ad.OnClose(res =>
                {
                    bool ok = res != null && res.isEnded;
                    Complete(ok, ok ? "看完（isEnded=true），发奖" : "中途关闭/未看完，不发奖");
                });
                _closeHooked = true;
            }
            _pending = onResult;
            _showAttempt = 0;
            // 官方推荐顺序：先 load 再 show。
            _ad.Load(_ => DoShow(), _ => Complete(false, "load 失败，不发奖"));
        }
        catch (Exception e)
        {
            Debug.LogError("[WeChatAd] 拉起激励视频异常: " + e);
            _pending = null;
            onResult?.Invoke(false);
        }
#else
        Debug.Log("[WeChatAd] 非微信构建，激励视频不可用，回调 false");
        onResult?.Invoke(false);
#endif
    }
}
