using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TapTap 平台接入唯一入口（SDK 初始化 + 登录 + 游客身份 + 埋点转发）。
///
/// 【为什么用守卫】TapSDK 是 UPM 依赖（Packages/manifest.json）。这台开发机与微信小游戏端
/// 可能尚未拉取依赖，缺失时整块代码不参与编译，避免 CS0246 阻塞全工程。
/// 依赖到位后在 Player Settings → Scripting Define Symbols 添加 TAPSDK_ENABLED。
///
/// 【调用时机铁律】Init() 必须在「用户已同意隐私政策」之后调用。
/// 本项目由原生 PrivacySplashActivity 两段式启动保证：同意后才创建 UnityPlayer，
/// 因此只要挂在这里（Unity 侧）就天然合规；严禁移入原生 Activity。
///
/// 【失败口径】一律 Debug.LogError + 玩家可见提示，不做静默兜底（项目纪律第12 条）。
/// 但登录失败不得阻塞单机玩法——这是产品设计，不是兜底。
/// </summary>
public sealed class TapTapBootstrap : MonoBehaviour
{
    // ==================== 配置（商业项目：集中一处便于轮换） ====================

    // 【怎么填】TapTap 开发者中心 → 你的游戏（像素冒险：裂隙之刃）→ 游戏服务 → 应用配置
    //   1) 点「立即开启」，拿到基本信息里的 Client ID 与 Client Token；
    //   2) 同一页配置签名证书：包名填 com.PixelAdventure.RiftBlade，
    //      签名填 keystore 证书的 MD5（32 位大写、无冒号）；
    //      取值跑 Tools/get_signature_md5.py（项目脚本，勿手拼）。
    // Client ID 每应用唯一；测试服与正式服需各自创建应用，不能共用。
    // 适用地区选「中国大陆」→对应代码里的 TapTapRegionType.CN。
    //
    // ⚠️ clientToken 是客户端凭据（随包可见，非服务端密钥）；后台若有「服务端密钥」，
    //    那种**绝不能**写进客户端。Release 出包前把 EnableLogInDevelopment 改 false。
    const string ClientId = "qaj4uk9rzvldhcozlf";
    const string ClientToken = "togyc8QdNEI8RrckreXRQhn5NwQhCBjPNb5swZjQ";

    const string RegionChannel = "TapTap";
    const bool EnableLogInDevelopment = true; // Release 出包前改 false

    const string GuestIdPrefKey = "TapTapGuestId";

    // ==================== 状态 ====================

    public static TapTapBootstrap Instance { get; private set; }

    /// <summary>SDK 初始化完成（成功或失败都会置 true，避免业务反复等待）。</summary>
    public bool Initialized { get; private set; }

    /// <summary>当前是否为 TapTap 正式账号（false = 游客或未登录）。</summary>
    public bool IsTapAccount { get; private set; }

    /// <summary>当前上报用的用户 ID：TapTap openId 或游客 UUID。</summary>
    public string CurrentUserId { get; private set; } = string.Empty;

    public string Nickname { get; private set; } = string.Empty;
    public string AvatarUrl { get; private set; } = string.Empty;

    event Action<bool, string> _onInitCompleted;
    event Action<bool, string> _onLoginCompleted;

    /// <summary>初始化终态：null=进行中，true=成功，false=失败。TapLogin 必须等成功后再发起登录。</summary>
    bool? _initOk;
    string _initError = string.Empty;
    bool _loginWaitingInit;

    // ==================== 生命周期 ====================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 初始化 ====================

    /// <summary>确保存在并初始化。可重复调用，内部只初始化一次。</summary>
    public static TapTapBootstrap Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("TapTapBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<TapTapBootstrap>();
        }
        Instance.Init();
        return Instance;
    }

    public void Init()
    {
        if (Initialized) return;
        Initialized = true;

#if TAPSDK_ENABLED
        if (string.IsNullOrEmpty(ClientId) || ClientId.StartsWith("REPLACE_"))
        {
            Debug.LogError("[TapTap] ClientId 未配置，数据分析不可用。请在TapTapBootstrap 顶部填入后台申请值。");
            BindGuestId();
            _initOk = false;
            _initError = "ClientId 未配置";
            _onInitCompleted?.Invoke(false, "ClientId 未配置");
            return;
        }

                TapSDK.Core.TapTapSdkOptions coreOptions = new TapSDK.Core.TapTapSdkOptions
        {
            clientId = ClientId,
            clientToken = ClientToken,
            region = TapSDK.Core.TapTapRegionType.CN,
            screenOrientation = 0,       // 0 = 竖屏
            enableLog = EnableLogInDevelopment,
            preferredLanguage = TapSDK.Core.TapTapLanguageType.Auto,
        };

        // 数据分析配置：字段在 TapTapEventOptions 里（v4.10.9 实测，与 coreOptions 分属两个类）
        TapSDK.Core.TapTapEventOptions eventOptions = new TapSDK.Core.TapTapEventOptions
        {
            channel = RegionChannel,
            propertiesJson = "{}",
            overrideBuiltInParameters = false,
            enableAutoIAPEvent = false,
            enableAdvertiserIDCollection = false, // 不采集广告标识
            disableReflectionOAID = true,         // 不反射 OAID（合规最保守）
        };

        TapSDK.Core.TapTapSdkBaseOptions[] otherOptions = new TapSDK.Core.TapTapSdkBaseOptions[] { eventOptions };

        try
        {
            TapSDK.Core.TapTapSDK.Init(coreOptions, otherOptions);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TapTap] SDK 初始化抛异常：{e.Message}");
            BindGuestId();
            _initOk = false;
            _initError = e.Message;
            _onInitCompleted?.Invoke(false, e.Message);
            return;
        }

        TapSDK.Core.TapTapSDK.AddInitCallback(new InitCallback(this));
        Debug.Log("[TapTap] Init 已发起，等待异步结果");
#else
        Debug.Log("[TapTap] 未定义 TAPSDK_ENABLED，跳过 SDK 初始化（依赖未安装或非 Android 包）。");
        BindGuestId();
        _initOk = false;
        _initError = "TAPSDK_ENABLED 未定义";
        _onInitCompleted?.Invoke(false, "TAPSDK_ENABLED 未定义");
#endif
    }

    /// <summary>在初始化回调里绑定身份：已有 Tap 账号则用它，否则落到游客 UUID。</summary>
    void HandleInitResult(bool ok, string errMsg)
    {
        _initOk = ok;
        _initError = errMsg ?? string.Empty;
        if (!ok)
        {
            Debug.LogError($"[TapTap] SDK 初始化失败：{errMsg}");
            BindGuestId();
            _onInitCompleted?.Invoke(false, errMsg);
            return;
        }

#if TAPSDK_ENABLED
        // 已有登录态则复用（TapTap 客户端 Token 仍在有效期内）
        // ⚠️ 官方 v4 返回 Task<TapTapAccount>，此处必须走异步；
        //   绝不能用 .GetAwaiter().GetResult() —— 主线程死锁。
        _ = TryReuseExistingAccountAsync();
#else
        // 游客：本地 UUID 直接作为上报用户 ID，不需要 TapTap 账号
        BindGuestId();
        Debug.Log("[TapTap] 初始化完成，当前为游客身份");
        _onInitCompleted?.Invoke(true, string.Empty);
#endif
    }

#if TAPSDK_ENABLED
    /// <summary>尝试复用已有 Tap 登录态；查不到或抛异常则落游客。</summary>
    async System.Threading.Tasks.Task TryReuseExistingAccountAsync()
    {
        try
        {
            TapSDK.Login.TapTapAccount cached = await TapSDK.Login.TapTapLogin.Instance.GetCurrentTapAccount();
            if (cached != null && !string.IsNullOrEmpty(cached.openId))
            {
                BindAccount(cached.openId, cached.name, cached.avatar);
                Debug.Log($"[TapTap] 复用已有登录态：{cached.openId}");
                _onInitCompleted?.Invoke(true, string.Empty);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TapTap] 查询已有登录态失败，按游客处理：{e.Message}");
        }

        // 游客：本地 UUID 直接作为上报用户 ID，不需要 TapTap 账号
        BindGuestId();
        Debug.Log("[TapTap] 初始化完成，当前为游客身份");
        _onInitCompleted?.Invoke(true, string.Empty);
    }
#endif

    // ==================== 身份绑定 ====================

    void BindGuestId()
    {
        IsTapAccount = false;
        CurrentUserId = LoadOrCreateGuestId();
        Nickname = "冒险者";
        AvatarUrl = string.Empty;
        GameAnalytics.SetUserId(CurrentUserId, false);
    }

    void BindAccount(string openId, string nickname, string avatar)
    {
        IsTapAccount = true;
        CurrentUserId = openId;
        Nickname = string.IsNullOrEmpty(nickname) ? "冒险者" : nickname;
        AvatarUrl = avatar ?? string.Empty;
        GameAnalytics.SetUserId(openId, true);
    }

    /// <summary>游客 ID：PlayerPrefs 存一次 UUID，重装即新号（单机游戏无需更持久）。</summary>
    string LoadOrCreateGuestId()
    {
        string cached = PlayerPrefs.GetString(GuestIdPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(cached)) return cached;

        // System.Guid 仅用于业务层本地 ID，不进资源引用链路（符合 AGENTS.md §1.4）
        string guid = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(GuestIdPrefKey, guid);
        PlayerPrefs.Save();
        return guid;
    }

    // ==================== 登录 ====================

    public void OnInitCompleted(Action<bool, string> cb)
    {
        if (cb == null) return;
        if (Initialized) { cb(true, string.Empty); return; }
        _onInitCompleted += cb;
    }

    /// <summary>
    /// TapTap 一键登录（官方按钮）。失败/取消均走 onLoginCompleted(false, msg)。
    /// 【2026-10-17 修复首次点击必失败】SDK Init 是异步的，以前不等初始化完成就发登录，
    /// 冷启动第一次点击必报 "SDK not initialized"。现在等初始化回调成功后再发登录。
    /// </summary>
    public void TapLogin(Action<bool, string> onDone)
    {
        if (onDone != null) _onLoginCompleted += onDone;
        Init();

#if TAPSDK_ENABLED
        if (_initOk == true)
        {
            // Unity 的 await 会自动回到主线程，不需要额外的线程泵
            _ = TapLoginAsync();
            return;
        }
        if (_initOk == false)
        {
            // 初始化已失败（终态，如 1001 包名/签名不符），直接报失败，不再空等
            Debug.LogError($"[TapTap] 登录前检查：SDK 初始化已失败：{_initError}");
            _onLoginCompleted?.Invoke(false, "SDK 初始化失败：" + _initError);
            _onLoginCompleted = null;
            return;
        }
        // 初始化进行中：挂到初始化回调上，成功后再发登录（修首次点击竞态）
        if (_loginWaitingInit) return;
        _loginWaitingInit = true;
        _onInitCompleted += (ok, err) =>
        {
            _loginWaitingInit = false;
            if (ok)
            {
                _ = TapLoginAsync();
                return;
            }
            Debug.LogError($"[TapTap] 登录前 SDK 初始化失败：{err}");
            _onLoginCompleted?.Invoke(false, "SDK 初始化失败：" + err);
            _onLoginCompleted = null;
        };
#else
        Debug.LogError("[TapTap] 未定义 TAPSDK_ENABLED，TapTap 登录不可用。请检查依赖是否安装。");
        _onLoginCompleted?.Invoke(false, "TAPSDK_ENABLED 未定义");
        _onLoginCompleted = null;
#endif
    }

#if TAPSDK_ENABLED
    async System.Threading.Tasks.Task TapLoginAsync()
    {
        try
        {
            var scopes = new[] { TapSDK.Login.TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE };
            TapSDK.Login.TapTapAccount acc = await TapSDK.Login.TapTapLogin.Instance.LoginWithScopes(scopes);
            if (acc == null || string.IsNullOrEmpty(acc.openId))
            {
                Debug.LogError("[TapTap] 登录返回空账号");
                _onLoginCompleted?.Invoke(false, "登录返回空账号");
                _onLoginCompleted = null;
                return;
            }

            BindAccount(acc.openId, acc.name, acc.avatar);
            Debug.Log($"[TapTap] 登录成功：{acc.openId}");
            _onLoginCompleted?.Invoke(true, string.Empty);
            _onLoginCompleted = null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TapTap] 登录失败：{e.Message}");
            _onLoginCompleted?.Invoke(false, e.Message);
            _onLoginCompleted = null;
        }
    }
#endif

    /// <summary>登出：清 SDK 登录态 + 清上报用户 ID，回到游客身份。</summary>
    public void TapLogout()
    {
#if TAPSDK_ENABLED
        try
        {
            TapSDK.Login.TapTapLogin.Instance.Logout();
        }
        catch (Exception e)
        {
            Debug.LogError($"[TapTap] 登出异常：{e.Message}");
        }
        GameAnalytics.ClearUser();
#endif
        BindGuestId();
        Debug.Log("[TapTap] 已登出，回到游客身份");
    }

    // ==================== 初始化回调实现 ====================

#if TAPSDK_ENABLED
    sealed class InitCallback : TapSDK.Core.ITapInitCallback
    {
        readonly TapTapBootstrap _owner;
        public InitCallback(TapTapBootstrap owner) { _owner = owner; }

        public void OnInitSuccess() => _owner.HandleInitResult(true, string.Empty);

        public void OnInitFail(int errorCode, string errorMsg)
        {
            string extra = errorCode == 1001
                ? "（包名/签名与 TapTap 后台配置不一致，必须改后台后重新发布，重试无效）"
                : string.Empty;
            _owner.HandleInitResult(false, $"[{errorCode}] {errorMsg}{extra}");
        }
    }
#endif
}