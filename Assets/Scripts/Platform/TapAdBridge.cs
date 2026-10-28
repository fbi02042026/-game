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
/// 【2026-10-26 修真机「激励未完成」】初始化改状态机：Pending 时挂起等回调再请求，
/// Failed 后下次点击重试初始化（原先当帧查 IsInitialized 必失败且永不重试）。
/// </summary>
public static class TapAdBridge
{
#if UNITY_ANDROID && !UNITY_EDITOR
    static Dirichlet.Mediation.DirichletAdNative _adNative;

    /// <summary>SDK 初始化状态机。【2026-10-26 修】原先 _initRequested 只置一次，
    /// 初始化是异步的（Android 桥 initializeAsyncWithTimeouts），而 ShowRewarded 当帧就查
    /// DirichletSdk.IsInitialized —— 首次请求必撞「未初始化」直接失败；且失败后永不重试，
    /// 真机上金币/体力第 3 次点击始终拉不起广告即此因。现改为：等待中挂起等回调，
    /// 失败后允许下次点击重新初始化（网络恢复可自愈）。</summary>
    enum InitState { NotStarted, Pending, Ok, Failed }
    static InitState _initState = InitState.NotStarted;
    static MonoBehaviour _host;

    /// <summary>协程宿主（纯跑等待协程，无逻辑）。</summary>
    static MonoBehaviour Host
    {
        get
        {
            if (_host == null)
            {
                var go = new GameObject("TapAdBridgeHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<TapAdHost>();
            }
            return _host;
        }
    }

    sealed class TapAdHost : MonoBehaviour { }

    /// <summary>等待初始化完成的上限（秒）：SDK 自身 init 超时 5s + 配置拉取 35s 的最坏情况。</summary>
    const float InitWaitTimeoutSeconds = 45f;

    static void InitIfNeeded()
    {
        if (_initState == InitState.Pending || _initState == InitState.Ok) return;
        if (string.IsNullOrEmpty(TapAdConfig.MediaKey))
        {
            // 配置缺失属永久性错误，不重试（重试也不可能成功）
            Debug.LogError("[TapAd] MediaKey 未配置（TapAdConfig），SDK 不初始化 —— 广告不可用");
            _initState = InitState.Failed;
            return;
        }
        _initState = InitState.Pending;
        var config = new Dirichlet.Mediation.DirichletAdConfig.Builder()
            .WithMediaId(TapAdConfig.MediaId)
            .WithMediaName(TapAdConfig.MediaName)
            .WithMediaKey(TapAdConfig.MediaKey)
            .EnableDebug(Debug.isDebugBuild)   // 调试包开日志，正式包（非 debug）自动关
            .Build();
        Dirichlet.Mediation.DirichletSdk.Init(config,
            onSuccess: r =>
            {
                Debug.Log($"[TapAd] 初始化成功: {r.Message}");
                _initState = InitState.Ok;
            },
            onFailure: e =>
            {
                Debug.LogError($"[TapAd] 初始化失败: {e.Code} {e.Message}（下次请求将重试初始化）");
                _initState = InitState.Failed;
            });
    }

    /// <summary>初始化就绪后真正发起激励视频请求（含加载+展示合一的 ShowRewardVideoAutoAd）。</summary>
    static void RequestRewarded(string placement, Action<bool> onComplete)
    {
        if (_adNative == null)
            _adNative = Dirichlet.Mediation.DirichletAdManager.CreateAdNative();
        var request = new Dirichlet.Mediation.DirichletAdRequest.Builder()
            .WithSpaceId(TapAdConfig.RewardVideoSpaceId)
            .WithUserId(SystemInfo.deviceUniqueIdentifier)
            .WithRewardName("奖励")
            .WithRewardAmount(1)
            .Build();
        Debug.Log($"[TapAd] 请求激励视频 placement={placement} spaceId={TapAdConfig.RewardVideoSpaceId}");
        Analytics.AdAvailable(placement);   // 埋点 T5：广告请求已发出（可展示尝试，漏斗第 1 格）
        _adNative.ShowRewardVideoAutoAd(request, new RewardListener(placement, onComplete));
    }

    /// <summary>初始化进行中：挂起等待回调就绪后再请求（SDK 回调由其内部泵回 Unity 主线程）。</summary>
    static System.Collections.IEnumerator WaitInitThenRequest(string placement, Action<bool> onComplete)
    {
        float waited = 0f;
        while (_initState == InitState.Pending && waited < InitWaitTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_initState == InitState.Ok)
        {
            RequestRewarded(placement, onComplete);
        }
        else
        {
            // 【2026-10-26 补】超时时若仍 Pending（SDK 回调彻底丢失），转 Failed
            // —— 否则状态永久卡死，之后每次点击都白等 45s 且永不重试。
            if (_initState == InitState.Pending)
            {
                Debug.LogError("[TapAd] 初始化等待超时且回调未到达 → 转入 Failed，下次点击重试初始化");
                _initState = InitState.Failed;
            }
            Debug.LogError($"[TapAd] 等待 SDK 初始化超时/失败，激励视频不可用 placement={placement} state={_initState} waited={waited:F1}s");
            onComplete?.Invoke(false);
        }
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
        switch (_initState)
        {
            case InitState.Ok:
                RequestRewarded(placement, onComplete);
                return;

            case InitState.Pending:
                // 【2026-10-26 修】初始化是异步的，等待回调就绪后再请求，不再当帧直接判失败
                Debug.Log($"[TapAd] SDK 初始化中，等待完成后请求激励视频 placement={placement}");
                Host.StartCoroutine(WaitInitThenRequest(placement, onComplete));
                return;

            default: // Failed / NotStarted（Failed 时下次点击会重新 Init，见 InitIfNeeded）
                Debug.LogError($"[TapAd] SDK 初始化失败，激励视频不可用（下次点击重试初始化）placement={placement}");
                onComplete?.Invoke(false);
                return;
        }
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
            Analytics.AdFail(_placement, $"{error.Code} {error.Message}");   // 埋点 T5：失败原因
            Complete(false);
        }

        public void OnAdShow()
        {
            Debug.Log($"[TapAd] 激励视频展示 placement={_placement}");
            Analytics.AdShow(_placement);   // 埋点 T5：展示成功（漏斗第 2 格）
        }

        public void OnAdClick() { }

        public void OnRewardVerify(Dirichlet.Mediation.DirichletRewardVerificationEventArgs args)
        {
            if (args.IsVerified)
            {
                Debug.Log($"[TapAd] 激励核验通过 placement={_placement}（发奖）");
                Analytics.AdReward(_placement);   // 埋点 T5：激励完成核验通过（漏斗第 3 格，转化终点）
                Complete(true);
            }
            else
            {
                Debug.LogWarning($"[TapAd] 激励核验未通过 placement={_placement}（不发奖）");
                Analytics.AdFail(_placement, "verify_not_passed");   // 埋点 T5：核验未通过
                Complete(false);
            }
        }

        public void OnAdClose()
        {
            // 关闭时还没走到核验（跳过/异常路径）= 未完成；核验先到则这里只是收尾
            Debug.Log($"[TapAd] 激励视频关闭 placement={_placement}");
            if (!_done)
                Analytics.AdFail(_placement, "closed_before_verify");   // 埋点 T5：未核验先关闭（跳过/中途退出）
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
