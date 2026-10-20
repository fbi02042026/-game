using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 登录界面。资源请在预制体里直接替换 Sprite，不要用生成器覆盖。
/// 当前：开始游戏 + 公告 + 设置（共用 SettingsPopup）；协议默认不勾选；用户中心/快捷登录仍隐藏。
/// 公告（2026-10-20）：右侧「公告」按钮打开 LoginNoticePopupUI，首次登录按期号自动弹一次。
/// </summary>
public class LoginUI : MonoBehaviour
{
    const string AgreeTip = "请先阅读并同意用户协议与隐私政策";

    // 用户协议与隐私政策合并页。
    // 【当前启用】TapTap 渠道强制要求：上架时必须展示渠道自己的隐私协议页。
    const string PrivacyPolicyUrl = "https://www.taptap.cn/app/959893/common-privacy";
    // 【备用·已注释】自有隐私政策（腾讯文档过渡链接）。
    // 后期换成自己的正式域名后，把下面这行的常量名改回 PrivacyPolicyUrl 并注释掉上面 TapTap 那行即可。
    // const string PrivacyPolicyUrl = "https://docs.qq.com/aio/DQWxJRWRoYkJ5SWZN";

    [Header("可替换贴图（预制体里拖）")]
    [Tooltip("全屏底图：预制体根节点命名为 BgStretch")]
    public Image backgroundImage;
    public Image ageRatingImage;
    public Image logoImage;
    public Image startButtonImage;
    public Image guestButtonImage;
    public Image noticeIcon;
    public Image settingsIcon;
    public Image userCenterIcon;
    public Image wechatIcon;
    public Image qqIcon;
    public Image appleIcon;

    [Header("按钮")]
    public Button startButton;
    public Button guestButton;
    public Button noticeButton;
    public Button settingsButton;
    public Button userCenterButton;
    public Button wechatButton;
    public Button qqButton;
    public Button appleButton;
    public Button userAgreementButton;
    public Button privacyPolicyButton;

    [Header("协议")]
    public Toggle agreeToggle;
    public Text agreeText;

    [Header("事件（可选）")]
    public UnityEvent onStartGame;
    public UnityEvent onOpenUserAgreement;
    public UnityEvent onOpenPrivacyPolicy;

    bool _wired;
    bool _agreeSetup;
    bool _presentationApplied;
    bool _footerLayoutReady;
    bool _autoNoticeStarted;
    System.Action _enterTown;
    RectTransform _healthNoticeRt;
    Graphic _agreeCheckGraphic;
    Transform _agreeCheckTf;
    Text _versionLabel;

    /// <summary>Boot 等入口绑定：开始游戏进城镇</summary>
    public void BindEnterTown(System.Action enterTown)
    {
        _enterTown = enterTown;
    }

    /// <summary>软著截图等：是否正在显示带底板的全局提示</summary>
    public bool IsToastShowing => GlobalToastUI.IsShowing;
    public string ToastMessage => GlobalToastUI.CurrentMessage;

    void Awake()
    {
        if (backgroundImage == null || startButton == null)
            AutoBindFromHierarchy();
        if (backgroundImage != null)
            UiLayoutStretch.ApplyBgStretch(backgroundImage.rectTransform, backgroundImage);
        EnsureAgreeToggle();
        ApplyPresentation();
        EnsureBottomFooterLayout();
        EnsureLogoMotion();
        WireClicks();
        EnsureDebugClearSaveButton();
        EnsureVersionLabel();
        EnsureSafeArea();
        EnsureTapLoginRow();
    }

    void OnEnable()
    {
        EnsureAgreeToggle();
        ApplyPresentation();
        EnsureBottomFooterLayout();
        EnsureLogoMotion();
        WireClicks();
        EnsureDebugClearSaveButton();
        EnsureVersionLabel();
        EnsureSafeArea();
        EnsureTapLoginRow();
        EnsureAutoNotice();
        GameBgm.Play(GameBgm.Track.Login);
    }

    /// <summary>
    /// 首次登录自动弹公告（2026-10-20 主人拍板「首次登录自动弹」）。
    /// 等两帧：登录界面刚显示，Canvas 尺寸与 SafeArea 偏移还没算完，抢跑会让公告板在刘海屏上偏一帧。
    /// 幂等：_autoNoticeStarted 保证本次会话只触发一次；弹不弹由 LoginNoticePopupUI 的已读期号决定。
    /// </summary>
    void EnsureAutoNotice()
    {
        if (_autoNoticeStarted) return;
        _autoNoticeStarted = true;
        StartCoroutine(AutoNoticeRoutine());
    }

    System.Collections.IEnumerator AutoNoticeRoutine()
    {
        yield return null;
        yield return null;
        LoginNoticePopupUI.TryAutoShow();
    }

    /// <summary>
    /// 刘海 / 挖孔 / 底部手势条避让（2026-10-13 主人拍板「要1」：登录页接 SafeArea）。
    /// 顶侧：RightMenu（公告/设置竖列）走 ApplyRightMenuLayout 统一排版（见该方法注释）；
    ///   「清除存档」按钮在 EnsureDebugClearSaveButton 里同样挂 Offset。
    ///   NoticeButton / SettingsButton 作为 RightMenu 子节点整体跟随，
    ///   公告按钮与设置按钮拿到的是同一份避让，**不要在 NoticeButton 上再挂 fitter**（会双重下推）。
    /// Logo：LoginLogoMotion 每帧写 anchoredPosition，挂 fitter 会互相覆盖，
    ///   改为把顶部内缩量并进入场基准位（见 LoginLogoMotion.Awake）。
    /// 底侧：忠告 + 适龄图标在 ApplyBottomFooterLayout 里把 bottomPad 加上底部内缩量。
    /// ActionPanel（距底 136）/ LegalBar 手势条盖不到，不动。
    /// 幂等：可重复调。
    /// </summary>
    void EnsureSafeArea()
    {
        // RightMenu 不再挂 SafeAreaFitter：排版收口到 ApplyRightMenuLayout 单一出口
        // （同一节点两个写入口会互相覆盖，见 ApplyRightMenuLayout 注释）。
        ApplyRightMenuLayout();
    }

    // ── RightMenu（公告/设置按钮竖列）排版：单一出口 ──
    // 预制体真源：LoginUI.prefab RightMenu anchor/pivot=(1,1)、anchoredPosition=(-10,-153)
    // （git 全历史恒为此值）。2026-10-20 主人真机截图实证：包内 RightMenu 被多挪了
    // 左 ≈118 / 下 ≈168 逻辑单位，量值正好等于 safeArea 的左内缩+顶内缩 ——
    // 即有环节把 safeArea.left 也算进了位移；项目口径是「左右永不缩」（竖屏设计宽锁死）。
    // 因此这里运行时强制：X 恒等于预制体基准（绝不吃左右内缩），Y = 基准 - 顶部安全区；
    // LateUpdate 按帧守卫（值没变不写），任何别的写入口挪走后下一帧都会被压回贴角位。
    RectTransform _rightMenuRt;
    Vector2 _rightMenuBase;
    bool _rightMenuBaseCaptured;

    void ApplyRightMenuLayout()
    {
        if (_rightMenuRt == null)
        {
            _rightMenuRt = transform.Find("RightMenu") as RectTransform;
            if (_rightMenuRt == null)
            {
                Debug.LogError("[LoginUI] 未找到 RightMenu，公告/设置按钮无法排版");
                return;
            }
        }
        if (!_rightMenuBaseCaptured)
        {
            _rightMenuBase = _rightMenuRt.anchoredPosition;
            _rightMenuBaseCaptured = true;
            // 基准应为 (-10,-153)；若日志里偏离，说明出包用的预制体和仓库不一致，先对齐仓库再排查
            Debug.Log($"[LoginUI] RightMenu 预制体基准 anchoredPosition={_rightMenuBase}");
        }
        _rightMenuRt.anchorMin = new Vector2(1f, 1f);
        _rightMenuRt.anchorMax = new Vector2(1f, 1f);
        _rightMenuRt.pivot = new Vector2(1f, 1f);
        var want = new Vector2(_rightMenuBase.x, _rightMenuBase.y - SafeAreaFitter.TopInsetUnits(_rightMenuRt));
        if (_rightMenuRt.anchoredPosition != want)
            _rightMenuRt.anchoredPosition = want;
    }

    void LateUpdate()
    {
        // 保险：真机上 RightMenu 位置被其它环节挪走过；每帧守卫（只是几次比较，不变不写），
        // 谁挪了都在当帧末尾被压回贴角位。
        ApplyRightMenuLayout();
    }

    /// <summary>
    /// TapTap 登录按钮（2026-10-13 接入 TapTap 平台）。
    /// 【2026-10-13 主人拍板】按钮节点 = 预制体 ActionPanel/TapLoginButton（主人手动添加，原名 StartButton (1)），
    /// 代码只绑点击、排版贴图全权归预制体；游客继续按钮已整条删除（与「开始冒险」语义重复）。
    /// </summary>
    void EnsureTapLoginRow()
    {
        var panel = transform.Find("ActionPanel") as RectTransform;
        if (panel == null)
        {
            Debug.LogError("[LoginUI] 未找到 ActionPanel，TapTap 登录按钮无法挂载");
            return;
        }
        LoginTapRowBuilder.Ensure(panel, OnClickTapLogin);
    }

    void OnClickTapLogin()
    {
        if (!EnsureAgreed()) return;
        TapTapBootstrap.Ensure().TapLogin((ok, msg) =>
        {
            if (ok)
            {
                GameAnalytics.ReportAppLaunch();
                // 【2026-10-17 主人拍板】登录成功后按钮互换：TapTap 登录按钮消失、「开始冒险」出现，
                // 两个按钮任何时刻不同时出现；进游戏仍走「开始冒险」（OnClickStart）这一条路。
                SetActivePath($"ActionPanel/{LoginTapRowBuilder.TapButtonName}", false);
                if (startButton != null) startButton.gameObject.SetActive(true);
                else SetActivePath("ActionPanel/StartButton", true);
                // 【2026-10-18 修「欢迎语换行别扭」】昵称后显式换行，避免 Toast 按宽度硬折行截断「开始冒险」
                ShowTip($"欢迎回来，{TapTapBootstrap.Instance.Nickname}，\n点「开始冒险」进入游戏");
                Debug.Log("[LoginUI] TapTap 登录成功，已换出「开始冒险」按钮");
            }
            else
            {
                ShowTip($"TapTap 登录失败：{msg}");
            }
        });
    }

    void OnRectTransformDimensionsChange()
    {
        ApplyBottomFooterLayout();
        ApplyRightMenuLayout();
        if (backgroundImage != null)
            UiLayoutStretch.ApplyBgStretch(backgroundImage.rectTransform, backgroundImage);
    }

    /// <summary>
    /// 隐藏暂未使用的入口；协议默认不勾选（仅首次）；设置按钮已开放。
    /// 公告按钮同步开放（2026-10-20）：与设置按钮同在 RightMenu 竖列，美术与引用预制体里都有，
    /// 只是此前被这里显式关掉；开放后随 RightMenu 一起做刘海避让（见 EnsureSafeArea）。
    /// </summary>
    void ApplyPresentation()
    {
        SetActivePath("ActionPanel/GuestButton", false);
        SetActivePath("ActionPanel/OtherLoginLabel", false);
        SetActivePath("ActionPanel/SocialRow", false);
        SetActivePath("RightMenu/UserCenterButton", false);
        SetActivePath("RightMenu/NoticeButton", true);
        SetActivePath("RightMenu", true);
        SetActivePath("RightMenu/SettingsButton", true);

        // 【2026-10-17 主人拍板】真机包只留 TapTap 登录：「开始冒险」是游客入口
        // （LoginTapRowBuilder 头注：TapTap 大陆区要求无游客模式入口），编辑器保留便于调试。
        if (!Application.isEditor)
        {
            SetActivePath("ActionPanel/StartButton", false);
            if (startButton != null) startButton.gameObject.SetActive(false);
        }

        if (guestButton != null) guestButton.gameObject.SetActive(false);
        if (userCenterButton != null) userCenterButton.gameObject.SetActive(false);
        if (noticeButton != null) noticeButton.gameObject.SetActive(true);
        if (settingsButton != null) settingsButton.gameObject.SetActive(true);
        if (wechatButton != null) wechatButton.gameObject.SetActive(false);
        if (qqButton != null) qqButton.gameObject.SetActive(false);
        if (appleButton != null) appleButton.gameObject.SetActive(false);

        // 聚光灯：再次强制关掉第三方登录壳（含误绑引用）
        if (SpotlightBuild.Enabled)
        {
            SetActivePath("ActionPanel/GuestButton", false);
            SetActivePath("ActionPanel/SocialRow", false);
            SetActivePath("ActionPanel/OtherLoginLabel", false);
            SetActivePath("RightMenu/UserCenterButton", false);
            if (guestButton != null) guestButton.gameObject.SetActive(false);
            if (wechatButton != null) wechatButton.gameObject.SetActive(false);
            if (qqButton != null) qqButton.gameObject.SetActive(false);
            if (appleButton != null) appleButton.gameObject.SetActive(false);
            if (userCenterButton != null) userCenterButton.gameObject.SetActive(false);
        }

        if (!_presentationApplied)
        {
            _presentationApplied = true;
            SetAgreed(false);
        }

        // 协议条提到最前，避免被 ActionPanel 等挡住点击
        var legal = transform.Find("LegalBar");
        if (legal != null)
            legal.SetAsLastSibling();
    }

    /// <summary>补齐 Toggle；尺寸以预制体为准，不改 AgreeToggle / Background rect。</summary>
    void EnsureAgreeToggle()
    {
        var t = transform.Find("LegalBar/AgreeToggle");
        if (t == null)
        {
            Debug.LogWarning("[LoginUI] 未找到 LegalBar/AgreeToggle");
            return;
        }

        var bg = t.Find("Background")?.GetComponent<Image>();
        if (bg != null)
            UiPrefabRectGuard.Attach(bg.rectTransform);

        _agreeCheckTf = t.Find("Checkmark");
        _agreeCheckGraphic = _agreeCheckTf != null ? _agreeCheckTf.GetComponent<Graphic>() : null;
        if (_agreeCheckTf is RectTransform checkRt)
            UiPrefabRectGuard.Attach(checkRt);

        if (bg != null)
            bg.raycastTarget = true;
        if (_agreeCheckGraphic != null)
            _agreeCheckGraphic.raycastTarget = false;

        if (agreeToggle == null)
            agreeToggle = t.GetComponent<Toggle>();
        if (agreeToggle == null)
            agreeToggle = t.gameObject.AddComponent<Toggle>();
        if (agreeToggle == null)
        {
            Debug.LogError("[LoginUI] 无法添加 Toggle");
            return;
        }

        if (bg != null)
            agreeToggle.targetGraphic = bg;

        agreeToggle.graphic = null;
        agreeToggle.toggleTransition = Toggle.ToggleTransition.None;
        agreeToggle.transition = Selectable.Transition.None;

        if (!_agreeSetup)
        {
            _agreeSetup = true;
            agreeToggle.onValueChanged.RemoveListener(OnAgreeChanged);
            agreeToggle.onValueChanged.AddListener(OnAgreeChanged);

            // 点协议文案也可勾选：用已有 Text 作 Graphic，不要再加 Image（会和 Text 冲突）
            var label = transform.Find("LegalBar/AgreeLabel");
            if (label != null)
            {
                var labelGraphic = label.GetComponent<Graphic>();
                if (labelGraphic != null)
                    labelGraphic.raycastTarget = true;

                var labelBtn = label.GetComponent<Button>();
                if (labelBtn == null)
                    labelBtn = label.gameObject.AddComponent<Button>();
                if (labelBtn != null)
                {
                    if (labelGraphic != null)
                        labelBtn.targetGraphic = labelGraphic;
                    labelBtn.transition = Selectable.Transition.None;
                    labelBtn.onClick.RemoveListener(ToggleAgree);
                    labelBtn.onClick.AddListener(ToggleAgree);
                    // 协议文案只做勾选：不按压缩放、不变暗（缩放会让整行字跳动）
                    UiButtonPressFeedback.Suppress(label.gameObject);
                }
            }

            SetAgreed(false);
        }

        t.SetAsLastSibling();
    }

    void OnAgreeChanged(bool on)
    {
        SyncCheckmark(on);
    }

    void ToggleAgree()
    {
        if (agreeToggle == null) return;
        SetAgreed(!agreeToggle.isOn);
    }

    void SetAgreed(bool on)
    {
        if (agreeToggle != null)
            agreeToggle.isOn = on;
        SyncCheckmark(on);
    }

    void SyncCheckmark(bool on)
    {
        if (_agreeCheckTf != null)
            _agreeCheckTf.gameObject.SetActive(on);
        else if (_agreeCheckGraphic != null)
            _agreeCheckGraphic.enabled = on;
    }

    void SetActivePath(string path, bool active)
    {
        var t = transform.Find(path);
        if (t != null) t.gameObject.SetActive(active);
    }

    /// <summary>
    /// 底部健康忠告与 12+ 适龄图标分栏锚定，避免不同分辨率下重叠。
    /// </summary>
    void EnsureBottomFooterLayout()
    {
        if (ageRatingImage == null)
            ageRatingImage = FindImg("AgeRating");
        if (_healthNoticeRt == null)
            _healthNoticeRt = FindHealthNoticeRect();

        if (ageRatingImage == null || _healthNoticeRt == null)
            return;

        if (!_footerLayoutReady)
        {
            _footerLayoutReady = true;
            var ageImg = ageRatingImage;
            ageImg.preserveAspect = true;
            var noticeText = _healthNoticeRt.GetComponent<Text>();
            if (noticeText != null)
            {
                noticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
                noticeText.verticalOverflow = VerticalWrapMode.Overflow;
                noticeText.alignment = TextAnchor.LowerCenter;
            }
        }

        ApplyBottomFooterLayout();
    }

    void ApplyBottomFooterLayout()
    {
        if (ageRatingImage == null || _healthNoticeRt == null)
            return;

        var rootRt = transform as RectTransform;

        // 2026-10-13 主人拍板：底部 padding 加上安全区（手势条）内缩量，忠告/适龄图标不被遮挡。
        // 换算与 SafeAreaFitter.Offset 同源（屏幕像素 ÷ Canvas.scaleFactor），编辑器无安全区时为 0、外观不变。
        float bottomPad = 20f + SafeAreaFitter.BottomInsetUnits(rootRt);
        const float sidePad = 18f;
        const float gap = 12f;
        const float ageW = 70f;
        const float ageH = 89f;
        const float noticeHeight = 78f;
        const float maxNoticeWidth = 550f;

        float canvasW = rootRt != null ? rootRt.rect.width : 720f;
        float textW = Mathf.Min(maxNoticeWidth, canvasW - sidePad * 2f - ageW - gap);

        var ageRt = ageRatingImage.rectTransform;
        ageRt.anchorMin = new Vector2(1f, 0f);
        ageRt.anchorMax = new Vector2(1f, 0f);
        ageRt.pivot = new Vector2(1f, 0f);
        ageRt.sizeDelta = new Vector2(ageW, ageH);
        ageRt.anchoredPosition = new Vector2(-sidePad, bottomPad);

        // 文案居中，整体略向左偏半个图标宽，与右侧 12+ 形成视觉平衡且不重叠
        _healthNoticeRt.anchorMin = new Vector2(0.5f, 0f);
        _healthNoticeRt.anchorMax = new Vector2(0.5f, 0f);
        _healthNoticeRt.pivot = new Vector2(0.5f, 0f);
        _healthNoticeRt.sizeDelta = new Vector2(textW, noticeHeight);
        _healthNoticeRt.anchoredPosition = new Vector2(-(ageW + gap) * 0.5f, bottomPad);
    }

    RectTransform FindHealthNoticeRect()
    {
        var direct = transform.Find("用户协议");
        if (direct != null)
            return direct as RectTransform;

        foreach (var text in GetComponentsInChildren<Text>(true))
        {
            if (text == null || string.IsNullOrEmpty(text.text))
                continue;
            if (text.text.Contains("抵制不良游戏"))
                return text.rectTransform;
        }

        return null;
    }

    void EnsureLogoMotion()
    {
        if (logoImage == null)
            logoImage = FindImg("Logo");
        if (logoImage == null) return;
        if (logoImage.GetComponent<LoginLogoMotion>() == null)
            logoImage.gameObject.AddComponent<LoginLogoMotion>();
    }

    public void WireClicks()
    {
        if (_wired) return;
        _wired = true;

        // 用户协议 / 隐私政策按钮：打开同一份合并文档（腾讯文档过渡链接，长期请替换为自有官网域名）
        onOpenPrivacyPolicy.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));
        onOpenUserAgreement.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));

        Bind(startButton, OnClickStart);
        Bind(settingsButton, () => SettingsPopupUI.Ensure().Open(SettingsHost.Login));
        Bind(noticeButton, () => LoginNoticePopupUI.Open());
        Bind(userAgreementButton, () => onOpenUserAgreement?.Invoke());
        Bind(privacyPolicyButton, () => onOpenPrivacyPolicy?.Invoke());
    }

    void Bind(Button btn, UnityAction action)
    {
        if (btn == null || action == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    bool EnsureAgreed()
    {
        if (agreeToggle == null || agreeToggle.isOn) return true;
        ShowTip(AgreeTip);
        return false;
    }

    void ShowTip(string msg)
    {
        // 统一：深色半透明底板 + 文案（GlobalToastUI）
        GlobalToastUI.Show(msg);
    }

    void OnClickStart()
    {
        if (!EnsureAgreed()) return;
        // 2026-10-13：TapTap 接入后，游客也视为已选择身份（本地 UUID），
        // TapTapBootstrap.Ensure() 幂等，未初始化时在此兜一次初始化。
        TapTapBootstrap.Ensure();
        GlobalToastUI.Hide();
        // 2026-10-04 主人拍板：从点「开始游戏」这一刻起就停登录 BGM，不等 Loading 出现才静音。
        // 用 Stop（只淡出、不置 _loadingMuted）—— 若走 GameSceneManager 那条
        // 「!isActiveAndEnabled 直接 LoadScene」的旁路，不会经过 SceneLoadingCoordinator，
        // 用 MuteForLoading 会把 _loadingMuted 永久留 true，进 Town 后再也不出声。
        GameBgm.Stop(0.05f);
        onStartGame?.Invoke();
        Debug.Log("[LoginUI] 开始游戏");
        EnterTown();
    }

    void EnsureDebugClearSaveButton()
    {
        // 【2026-10-17 主人拍板】真机包不放「清除存档」调试按钮，只留 TapTap 登录；编辑器保留。
        if (!Application.isEditor) return;

        var existing = transform.Find("DebugClearSaveButton");
        if (existing != null)
        {
            existing.SetAsLastSibling();
            // 2026-10-13 主人拍板：刘海避让（Offset 只挪位置，幂等）
            SafeAreaFitter.EnsureTopOffset(existing as RectTransform);
            return;
        }

        var go = new GameObject("DebugClearSaveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(220f, 56f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.55f, 0.18f, 0.16f, 0.92f);
        img.raycastTarget = true;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var txt = labelGo.GetComponent<Text>();
        txt.text = "清除存档";
        txt.font = GameFonts.GetChinese();
        txt.fontSize = 26;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 0.95f, 0.85f);
        txt.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnClickClearSave);
        go.transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(go.transform);
        // 2026-10-13 主人拍板：刘海避让（Offset 只挪位置，幂等）
        SafeAreaFitter.EnsureTopOffset(rt);
    }

    /// <summary>
    /// 【2026-10-20 主人要求：登录页左上角显示版本号】
    /// 运行时补节点（项目口径「UI 用运行时补节点，别改预制体」）。
    /// 版本号真源 = Application.version（ProjectSettings.bundleVersion，出包改号自动跟随），不写死字符串。
    /// 位置：左上角贴角；编辑器里 DebugClearSaveButton 也占左上角，版本号排到它下方避让（真机无此按钮，不受影响）。
    /// 刘海避让走 SafeAreaFitter.EnsureTopOffset（Offset 模式只挪 anchoredPosition，与全页口径一致）。
    /// 幂等：可重复调。
    /// </summary>
    void EnsureVersionLabel()
    {
        if (_versionLabel == null)
        {
            var existing = transform.Find("VersionLabel");
            _versionLabel = existing != null ? existing.GetComponent<Text>() : null;
            if (_versionLabel == null)
            {
                var go = new GameObject("VersionLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(transform, false);
                _versionLabel = go.GetComponent<Text>();
            }

            var rt = _versionLabel.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(200f, 36f);

            // 编辑器：排在「清除存档」调试按钮下方避让（该方法已先跑，位置含刘海内缩后的现值）
            float topY = -8f;
            if (Application.isEditor)
            {
                var dbg = transform.Find("DebugClearSaveButton") as RectTransform;
                if (dbg != null)
                    topY = dbg.anchoredPosition.y - dbg.sizeDelta.y - 6f;
            }
            rt.anchoredPosition = new Vector2(16f, topY);

            _versionLabel.font = GameFonts.GetChinese();
            _versionLabel.fontSize = 24;
            _versionLabel.fontStyle = FontStyle.Bold;
            _versionLabel.alignment = TextAnchor.UpperLeft;
            _versionLabel.color = new Color(1f, 1f, 1f, 0.9f);
            _versionLabel.raycastTarget = false;
            _versionLabel.text = "v" + Application.version;
            // 2026-10-20：左上角属于刘海高发区，接 Offset 避让（只挪位置，幂等）
            SafeAreaFitter.EnsureTopOffset(rt);
        }
    }

    void OnClickClearSave()
    {
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.DeleteLocalSaveAndReset();
        else
        {
            string dir = Application.persistentDataPath;
            try
            {
                string a = System.IO.Path.Combine(dir, "player.dat");
                string b = System.IO.Path.Combine(dir, "player.bak");
                string c = System.IO.Path.Combine(dir, "save.json");
                string d = System.IO.Path.Combine(dir, "save_backup.json");
                if (System.IO.File.Exists(a)) System.IO.File.Delete(a);
                if (System.IO.File.Exists(b)) System.IO.File.Delete(b);
                if (System.IO.File.Exists(c)) System.IO.File.Delete(c);
                if (System.IO.File.Exists(d)) System.IO.File.Delete(d);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LoginUI] 清除存档失败：" + e.Message);
            }
        }
        ShowTip("存档已清除，点开始游戏会走新手剧情");
        Debug.Log("[LoginUI] 已清除存档");
    }

    void EnterTown()
    {
        if (_enterTown != null)
        {
            _enterTown.Invoke();
            return;
        }
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.GoMainHub();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSceneManager.TOWN_SCENE);
    }

    public void AutoBindFromHierarchy()
    {
        backgroundImage = FindImg("BgStretch");
        ageRatingImage = FindImg("AgeRating");
        logoImage = FindImg("Logo");
        startButtonImage = FindImg("ActionPanel/StartButton");
        guestButtonImage = FindImg("ActionPanel/GuestButton");
        noticeIcon = FindImg("RightMenu/NoticeButton/Icon");
        settingsIcon = FindImg("RightMenu/SettingsButton/Icon");
        userCenterIcon = FindImg("RightMenu/UserCenterButton/Icon");
        wechatIcon = FindImg("ActionPanel/SocialRow/WechatButton/Icon");
        qqIcon = FindImg("ActionPanel/SocialRow/QQButton/Icon");
        appleIcon = FindImg("ActionPanel/SocialRow/AppleButton/Icon");

        startButton = FindBtn("ActionPanel/StartButton");
        guestButton = FindBtn("ActionPanel/GuestButton");
        noticeButton = FindBtn("RightMenu/NoticeButton");
        settingsButton = FindBtn("RightMenu/SettingsButton");
        userCenterButton = FindBtn("RightMenu/UserCenterButton");
        wechatButton = FindBtn("ActionPanel/SocialRow/WechatButton");
        qqButton = FindBtn("ActionPanel/SocialRow/QQButton");
        appleButton = FindBtn("ActionPanel/SocialRow/AppleButton");
        userAgreementButton = FindBtn("LegalBar/UserAgreementBtn");
        privacyPolicyButton = FindBtn("LegalBar/PrivacyPolicyBtn");

        agreeToggle = transform.Find("LegalBar/AgreeToggle")?.GetComponent<Toggle>();
        agreeText = transform.Find("LegalBar/AgreeLabel")?.GetComponent<Text>();
    }

    Image FindImg(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    Button FindBtn(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
