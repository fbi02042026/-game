using System;
using UnityEngine;

/// <summary>
/// TapADN（聚合）真广告桥（2026-10-25 T2）：封装 Dirichlet.Mediation SDK，是
/// <see cref="RewardedAdBridge"/>（玩法层唯一出口）背后的真实实现。
/// 设计：
/// · 所有 SDK API 引用都关在 #if UNITY_ANDROID && !UNITY_EDITOR 里 —— 编辑器/非安卓平台
///   零 SDK 依赖（编辑器下 SDK 本就是 Noop 桥，真实验收必须 Android 真机）；
/// · 发奖只认 OnRewardVerify 且 IsVerified=true（官方铁律）；关闭/跳过/失败一律 onComplete(false)；
/// · 激励视频用 ShowRewardVideoAutoAd（官方推荐，加载+展示合一，内部管理缓存）。
/// 已知约束：媒体/推广位目前是「测试」态；SDK 初始化放在首次请求广告时懒执行
/// （正式的隐私同意后再初始化由 T4 钩子接管，见 Docs/《TapADN激励视频接入》_待办任务清单.md）。
/// </summary>
public static class TapAdBridge
{
#if UNITY_ANDROID && !UNITY_EDITOR
    static Dirichlet.Mediation.DirichletAdNative _adNative;
    static bool _initRequested;

    static void InitIfNeeded()
    {
        if (_initRequested) return;
        _initRequested = true;
        if (string.IsNullOrEmpty(TapAdConfig.MediaKey))
        {
            Debug.LogError("[TapAd] MediaKey 未配置（TapAdConfig），SDK 不初始化 —— 广告不可用");
            return;
        }
        var config = new Dirichlet.Mediation.DirichletAdConfig.Builder()
            .WithMediaId(TapAdConfig.MediaId)
            .WithMediaName(TapAdConfig.MediaName)
            .WithMediaKey(TapAdConfig.MediaKey)
            .EnableDebug(Debug.isDebugBuild)   // 调试包开日志，正式包（非 debug）自动关
            .Build();
        Dirichlet.Mediation.DirichletSdk.Init(config,
            onSuccess: r => Debug.Log($"[TapAd] 初始化成功: {r.Message}"),
            onFailure: e => Debug.LogError($"[TapAd] 初始化失败: {e.Code} {e.Message}"));
    }
#endif

    /// <summary>当前平台是否走真广告（安卓真机 = 是；编辑器/其它平台 = 否，由调用方回落模拟路径）。</summary>
    public static bool IsRealAdPlatform
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 播激励视频（真 SDK）。onComplete(true) = 核验通过该发奖；false = 失败/未核验完成，绝不发奖。
    /// </summary>
    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InitIfNeeded();
        if (!Dirichlet.Mediation.DirichletSdk.IsInitialized)
        {
            Debug.LogError($"[TapAd] SDK 未初始化完成，激励视频不可用 placement={placement}");
            onComplete?.Invoke(false);
            return;
        }
        if (_adNative == null)
            _adNative = Dirichlet.Mediation.DirichletAdManager.CreateAdNative();
        var request = new Dirichlet.Mediation.DirichletAdRequest.Builder()
            .WithSpaceId(TapAdConfig.RewardVideoSpaceId)
            .WithUserId(SystemInfo.deviceUniqueIdentifier)
            .WithRewardName("奖励")
            .WithRewardAmount(1)
            .Build();
        Debug.Log($"[TapAd] 请求激励视频 placement={placement} spaceId={TapAdConfig.RewardVideoSpaceId}");
        _adNative.ShowRewardVideoAutoAd(request, new RewardListener(placement, onComplete));
#else
        Debug.Log($"[TapAd] 非 Android 真机（editor/其它平台），无真广告 placement={placement}");
        onComplete?.Invoke(false);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>激励视频回调集。_done 防双回调（核验与关闭竞态）；关闭时未核验 = 未完成不发奖。</summary>
    sealed class RewardListener : Dirichlet.Mediation.IDirichletRewardVideoAutoAdListener
    {
        readonly string _placement;
        readonly Action<bool> _onComplete;
        bool _done;

        public RewardListener(string placement, Action<bool> onComplete)
        {
            _placement = placement;
            _onComplete = onComplete;
        }

        public void OnError(Dirichlet.Mediation.DirichletError error)
        {
            Debug.LogError($"[TapAd] 激励视频失败 placement={_placement}: {error.Code} {error.Message}");
            Complete(false);
        }

        public void OnAdShow() => Debug.Log($"[TapAd] 激励视频展示 placement={_placement}");

        public void OnAdClick() { }

        public void OnRewardVerify(Dirichlet.Mediation.DirichletRewardVerificationEventArgs args)
        {
            if (args.IsVerified)
            {
                Debug.Log($"[TapAd] 激励核验通过 placement={_placement}（发奖）");
                Complete(true);
            }
            else
            {
                Debug.LogWarning($"[TapAd] 激励核验未通过 placement={_placement}（不发奖）");
                Complete(false);
            }
        }

        public void OnAdClose()
        {
            // 关闭时还没走到核验（跳过/异常路径）= 未完成；核验先到则这里只是收尾
            Debug.Log($"[TapAd] 激励视频关闭 placement={_placement}");
            Complete(false);
        }

        void Complete(bool ok)
        {
            if (_done) return;
            _done = true;
            _onComplete?.Invoke(ok);
        }
    }
#endif
}
