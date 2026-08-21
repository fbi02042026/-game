using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置弹窗（战斗内 / 城镇共用）：音乐、音效、右上角关闭。
/// 仅在战斗场景显示「撤离」；撤离后回城镇并打开冒险页。
/// </summary>
public class BattleSettingsPanel : MonoBehaviour
{
    public static BattleSettingsPanel Instance { get; private set; }

    Canvas _canvas;
    GameObject _root;
    Button _musicBtn;
    Text _musicLabel;
    Button _sfxBtn;
    Text _sfxLabel;
    Button _evacuateBtn;
    Button _closeBtn;
    Button _resumeBtn;
    Text _resumeLabel;
    float _prevTimeScale = 1f;

    public bool IsOpen => _root != null && _root.activeSelf;
    public Button EvacuateButton => _evacuateBtn;

    public static BattleSettingsPanel Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("BattleSettingsPanel");
        DontDestroyOnLoad(go);
        return go.AddComponent<BattleSettingsPanel>();
    }

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Toggle()
    {
        var p = Ensure();
        if (p.IsOpen) p.Close();
        else p.Open();
    }

    public void Open()
    {
        if (_root == null) Build();
        _prevTimeScale = Time.timeScale;
        bool inBattle = IsInBattleScene();
        // 只在战斗里暂停；城镇设置不影响 timeScale
        if (inBattle) Time.timeScale = 0f;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        RefreshToggles();
        RefreshMode();
        GameFonts.ApplyToHierarchy(_root.transform);
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
    }

    /// <summary>当前是否在战斗场景（有撤离按钮）</summary>
    public static bool IsInBattleScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return scene.name == GameSceneManager.BATTLE_SCENE;
    }

    void RefreshMode()
    {
        bool inBattle = IsInBattleScene();
        if (_evacuateBtn != null)
            _evacuateBtn.gameObject.SetActive(inBattle);

        // 战斗外不暂停游戏时间；战斗内暂停
        if (!inBattle)
            Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;

        if (_resumeLabel == null && _resumeBtn != null)
            _resumeLabel = _resumeBtn.GetComponentInChildren<Text>();
        if (_resumeLabel != null)
            _resumeLabel.text = inBattle ? "继续战斗" : "关闭";

        // 没撤离按钮时把「关闭」上移一点，别空一大块
        if (_resumeBtn != null)
        {
            var rt = _resumeBtn.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(0f, inBattle ? 34f : 80f);
        }
    }

    void RefreshEvacuateVisible() => RefreshMode();

    void OnEvacuate()
    {
        Time.timeScale = 1f;
        if (_root != null) _root.SetActive(false);
        var tutorial = TutorialDirector.Instance;
        if (tutorial != null && tutorial.WaitingEvacuate)
            tutorial.WaitingEvacuate = false;
        // 引导局回城有收尾对话，不要抢先打开冒险页
        bool tutorialEvacuate = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        if (!tutorialEvacuate)
            TownHubController.PendingOpenAdventure = true;
        BattleManager.Instance?.TriggerEvacuation();
    }

    void Build()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 540;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage(_root.transform, "Panel", new Color(0.11f, 0.1f, 0.15f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(560f, 480f);

        var title = CreateText(panel.transform, "Title", "设置", 34, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -32f);
        trt.sizeDelta = new Vector2(400f, 48f);

        // 右上角关闭
        _closeBtn = CreateButton(panel.transform, "CloseButton", "×",
            new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(56f, 56f),
            new Color(0.42f, 0.22f, 0.22f, 1f), OnClose);

        _musicBtn = CreateRowToggle(panel.transform, "MusicRow", "音乐", -140f, OnToggleMusic, out _musicLabel);
        _sfxBtn = CreateRowToggle(panel.transform, "SfxRow", "音效", -220f, OnToggleSfx, out _sfxLabel);

        _evacuateBtn = CreateButton(panel.transform, "EvacuateButton", "撤离",
            new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(300f, 68f),
            new Color(0.55f, 0.3f, 0.22f, 1f), OnEvacuate);

        _resumeBtn = CreateButton(panel.transform, "ResumeButton", "关闭",
            new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 64f),
            new Color(0.24f, 0.4f, 0.3f, 1f), OnClose);

        GameFonts.ApplyToHierarchy(_root.transform);
        _root.SetActive(false);
    }

    Button CreateRowToggle(Transform parent, string name, string label, float y,
        UnityEngine.Events.UnityAction onClick, out Text stateLabel)
    {
        var row = CreateImage(parent, name, new Color(0.17f, 0.16f, 0.22f, 1f));
        var rt = row.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(460f, 68f);

        var name0 = CreateText(row.transform, "Name", label, 28, TextAnchor.MiddleLeft);
        var nrt = name0.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 0.5f);
        nrt.pivot = new Vector2(0f, 0.5f);
        nrt.anchoredPosition = new Vector2(26f, 0f);
        nrt.sizeDelta = new Vector2(220f, 48f);

        var btn = CreateButton(row.transform, "Toggle", "开启",
            new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(150f, 52f),
            new Color(0.25f, 0.45f, 0.32f, 1f), onClick);
        stateLabel = btn.GetComponentInChildren<Text>();
        return btn;
    }

    void OnToggleMusic()
    {
        GameAudio.MusicEnabled = !GameAudio.MusicEnabled;
        RefreshToggles();
    }

    void OnToggleSfx()
    {
        GameAudio.SfxEnabled = !GameAudio.SfxEnabled;
        RefreshToggles();
    }

    void RefreshToggles()
    {
        ApplyToggleLook(_musicBtn, _musicLabel, GameAudio.MusicEnabled);
        ApplyToggleLook(_sfxBtn, _sfxLabel, GameAudio.SfxEnabled);
    }

    static void ApplyToggleLook(Button btn, Text label, bool on)
    {
        if (label != null) label.text = on ? "开启" : "关闭";
        var img = btn != null ? btn.targetGraphic as Image : null;
        if (img != null)
            img.color = on ? new Color(0.25f, 0.45f, 0.32f, 1f) : new Color(0.4f, 0.26f, 0.26f, 1f);
    }

    void OnClose()
    {
        Close();
    }

    // ===== 小工具 =====

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 pos, Vector2 size, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        var img = CreateImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var text = CreateText(img.transform, "Label", label, 28, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return btn;
    }
}
