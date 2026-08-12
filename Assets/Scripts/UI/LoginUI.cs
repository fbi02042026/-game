using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 登录界面。资源请在预制体里直接替换 Sprite，不要用生成器覆盖。
/// 当前：仅「开始游戏」；协议默认不勾选；用户中心隐藏；公告/设置暂不接；快捷登录节点保留隐藏，待接微信 SDK。
/// </summary>
public class LoginUI : MonoBehaviour
{
    const string AgreeTip = "请先阅读并同意用户协议与隐私政策";

    [Header("可替换贴图（预制体里拖）")]
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
    System.Action _enterTown;
    Text _toastText;
    RectTransform _toastRt;
    Coroutine _toastCo;
    Graphic _agreeCheckGraphic;
    Transform _agreeCheckTf;

    /// <summary>Boot 等入口绑定：开始游戏进城镇</summary>
    public void BindEnterTown(System.Action enterTown)
    {
        _enterTown = enterTown;
    }

    void Awake()
    {
        if (backgroundImage == null || startButton == null)
            AutoBindFromHierarchy();
        EnsureAgreeToggle();
        ApplyPresentation();
        WireClicks();
    }

    void OnEnable()
    {
        EnsureAgreeToggle();
        ApplyPresentation();
        WireClicks();
    }

    /// <summary>
    /// 隐藏暂未使用的入口；协议默认不勾选（仅首次）。
    /// </summary>
    void ApplyPresentation()
    {
        SetActivePath("ActionPanel/GuestButton", false);
        SetActivePath("ActionPanel/OtherLoginLabel", false);
        SetActivePath("ActionPanel/SocialRow", false);
        SetActivePath("RightMenu/UserCenterButton", false);

        if (guestButton != null) guestButton.gameObject.SetActive(false);
        if (userCenterButton != null) userCenterButton.gameObject.SetActive(false);
        if (wechatButton != null) wechatButton.gameObject.SetActive(false);
        if (qqButton != null) qqButton.gameObject.SetActive(false);
        if (appleButton != null) appleButton.gameObject.SetActive(false);

        // 公告 / 设置：显示且保持可点外观，逻辑暂不接

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

    /// <summary>补齐 Toggle / 点击热区，勾选图用显隐同步（预制体原先只有框图没有 Toggle）</summary>
    void EnsureAgreeToggle()
    {
        var t = transform.Find("LegalBar/AgreeToggle");
        if (t == null)
        {
            Debug.LogWarning("[LoginUI] 未找到 LegalBar/AgreeToggle");
            return;
        }

        var bg = t.Find("Background")?.GetComponent<Image>();
        _agreeCheckTf = t.Find("Checkmark");
        _agreeCheckGraphic = _agreeCheckTf != null ? _agreeCheckTf.GetComponent<Graphic>() : null;

        // 根节点加透明热区（无 Image 才加，避免和已有组件冲突）
        var hit = t.GetComponent<Image>();
        if (hit == null)
            hit = t.gameObject.AddComponent<Image>();
        if (hit != null)
        {
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;
            if (bg != null && bg.sprite != null && hit.sprite == null)
                hit.sprite = bg.sprite;
        }

        var rt = t as RectTransform;
        if (rt != null && rt.sizeDelta.x < 48f)
            rt.sizeDelta = new Vector2(48f, 48f);

        if (bg != null) bg.raycastTarget = true;
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

        if (hit != null)
            agreeToggle.targetGraphic = hit;
        else if (bg != null)
            agreeToggle.targetGraphic = bg;

        agreeToggle.graphic = null; // 自己管勾选显隐
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

    public void WireClicks()
    {
        if (_wired) return;
        _wired = true;

        Bind(startButton, OnClickStart);
        // 公告 / 设置 / 用户中心：暂不接，日后统一面板
        // 快捷登录：隐藏，待接微信 SDK
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
        EnsureToast();
        if (_toastText == null) return;
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(CoToast(msg));
    }

    void EnsureToast()
    {
        if (_toastText != null) return;

        var go = new GameObject("AgreeTipToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(transform, false);
        _toastRt = go.GetComponent<RectTransform>();
        _toastRt.anchorMin = new Vector2(0.5f, 0.5f);
        _toastRt.anchorMax = new Vector2(0.5f, 0.5f);
        _toastRt.pivot = new Vector2(0.5f, 0.5f);
        _toastRt.sizeDelta = new Vector2(620f, 72f);
        _toastRt.anchoredPosition = new Vector2(0f, 40f);

        _toastText = go.GetComponent<Text>();
        _toastText.font = GameFonts.GetChinese();
        _toastText.fontSize = 28;
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.color = new Color(1f, 0.95f, 0.75f, 1f);
        _toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _toastText.verticalOverflow = VerticalWrapMode.Overflow;
        _toastText.raycastTarget = false;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        go.SetActive(false);
    }

    IEnumerator CoToast(string msg)
    {
        _toastText.text = msg;
        _toastText.gameObject.SetActive(true);
        _toastText.transform.SetAsLastSibling();

        Color c = _toastText.color;
        c.a = 1f;
        _toastText.color = c;
        _toastRt.anchoredPosition = new Vector2(0f, 40f);

        yield return new WaitForSecondsRealtime(1.6f);

        const float dur = 0.45f;
        float t = 0f;
        Vector2 start = new Vector2(0f, 40f);
        Vector2 end = new Vector2(0f, 100f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            _toastRt.anchoredPosition = Vector2.Lerp(start, end, u);
            c.a = 1f - u;
            _toastText.color = c;
            yield return null;
        }

        _toastText.gameObject.SetActive(false);
        _toastCo = null;
    }

    void OnClickStart()
    {
        if (!EnsureAgreed()) return;
        onStartGame?.Invoke();
        Debug.Log("[LoginUI] 开始游戏");
        EnterTown();
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
        backgroundImage = FindImg("Background");
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
