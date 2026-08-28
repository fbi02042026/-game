using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局设置弹窗宿主场景。同一套预制体，按宿主显隐底部按钮。
/// </summary>
public enum SettingsHost
{
    /// <summary>按当前场景自动推断（战斗 / 登录 / 城镇）。</summary>
    Auto = 0,
    Login = 1,
    Town = 2,
    Battle = 3,
}

/// <summary>
/// 可扩展开关行 ID。V1.0 只启用 MasterAudio；日后拆音乐/音效、天气特效时往这里加，不必改布局代码。
/// </summary>
public enum SettingsToggleId
{
    MasterAudio = 0,
    Music = 1,
    Sfx = 2,
    WeatherFx = 3,
    MercSkillAuto = 4,
}

/// <summary>
/// 登录 / 城镇 / 战斗共用设置弹窗。
/// 预制体：Resources/Prefabs/UI/SettingsPopup；缺失时运行时搭一份同结构。
/// 策划口径（软著 V1.0）：声音总开关；战斗多「撤离 / 继续战斗」；城镇与登录为「关闭」。
/// GDD 远期：音乐/音效分控、天气特效开关 —— 用 ToggleList 扩展，勿再各场景各搓一套。
/// </summary>
public class SettingsPopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/UI/SettingsPopup";

    public static SettingsPopupUI Instance { get; private set; }

    [Header("绑定（预制体按名字也可自动找）")]
    public GameObject root;
    public Button closeButton;
    public Button audioToggleButton;
    public Text audioToggleLabel;
    public Button evacuateButton;
    public Button primaryButton;
    public Text primaryLabel;
    public Transform toggleList;

    [Tooltip("预留：日后「音乐」「音效」「天气」等行挂在这里，运行时按启用表显隐")]
    public List<SettingsToggleRow> extraToggleRows = new List<SettingsToggleRow>();

    SettingsHost _host = SettingsHost.Auto;
    float _prevTimeScale = 1f;
    bool _wired;

    public bool IsOpen => root != null && root.activeSelf;
    public Button EvacuateButton => evacuateButton;
    public SettingsHost CurrentHost => ResolveHost(_host);

    [Serializable]
    public class SettingsToggleRow
    {
        public SettingsToggleId id;
        public GameObject root;
        public Button toggleButton;
        public Text stateLabel;
    }

    public static SettingsPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "SettingsPopup";
        }
        else
        {
            Debug.LogWarning($"[SettingsPopup] 未找到预制体 {PrefabPath}，改用代码搭建。请跑 Tools/UI/生成设置弹窗预制体");
            go = new GameObject("SettingsPopup");
            var ui = go.AddComponent<SettingsPopupUI>();
            ui.BuildFallbackHierarchy();
            DontDestroyOnLoad(go);
            return ui;
        }

        DontDestroyOnLoad(go);
        var panel = go.GetComponent<SettingsPopupUI>();
        if (panel == null) panel = go.AddComponent<SettingsPopupUI>();
        return panel;
    }

    void Awake()
    {
        Instance = this;
        Bind();
        Wire();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Toggle(SettingsHost host = SettingsHost.Auto)
    {
        var p = Ensure();
        if (p.IsOpen) p.Close();
        else p.Open(host);
    }

    public void Open(SettingsHost host = SettingsHost.Auto)
    {
        Bind();
        Wire();
        _host = host;
        _prevTimeScale = Time.timeScale;
        bool battle = CurrentHost == SettingsHost.Battle;
        if (battle) Time.timeScale = 0f;

        if (root == null) BuildFallbackHierarchy();
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        transform.SetAsLastSibling();

        RefreshToggles();
        RefreshHostChrome();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
    }

    static SettingsHost ResolveHost(SettingsHost host)
    {
        if (host != SettingsHost.Auto) return host;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == GameSceneManager.BATTLE_SCENE) return SettingsHost.Battle;
        if (scene == GameSceneManager.BOOT_SCENE || scene == "Login")
            return SettingsHost.Login;
        return SettingsHost.Town;
    }

    /// <summary>当前是否战斗场景（兼容旧调用）。</summary>
    public static bool IsInBattleScene() => ResolveHost(SettingsHost.Auto) == SettingsHost.Battle;

    void RefreshHostChrome()
    {
        var host = CurrentHost;
        bool battle = host == SettingsHost.Battle;

        if (evacuateButton != null)
            evacuateButton.gameObject.SetActive(battle);

        if (!battle)
            Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;

        if (primaryLabel == null && primaryButton != null)
            primaryLabel = primaryButton.GetComponentInChildren<Text>(true);
        if (primaryLabel != null)
        {
            switch (host)
            {
                case SettingsHost.Battle:
                    primaryLabel.text = "继续战斗";
                    break;
                case SettingsHost.Login:
                    primaryLabel.text = "关闭";
                    break;
                default:
                    primaryLabel.text = "关闭";
                    break;
            }
        }

        if (primaryButton != null)
        {
            var rt = primaryButton.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(0f, battle ? 34f : 80f);
        }

        // 扩展行：仅显示当前版本启用的
        ApplyExtraToggleVisibility();
    }

    void ApplyExtraToggleVisibility()
    {
        if (extraToggleRows == null) return;
        for (int i = 0; i < extraToggleRows.Count; i++)
        {
            var row = extraToggleRows[i];
            if (row == null || row.root == null) continue;
            row.root.SetActive(IsToggleEnabledInBuild(row.id));
        }
    }

    /// <summary>V1.0 只开总声音；GDD/天气文档里的分项先关着，改这里即可上线。</summary>
    public static bool IsToggleEnabledInBuild(SettingsToggleId id)
    {
        switch (id)
        {
            case SettingsToggleId.MasterAudio: return true;
            case SettingsToggleId.Music: return false;
            case SettingsToggleId.Sfx: return false;
            case SettingsToggleId.WeatherFx: return false;
            case SettingsToggleId.MercSkillAuto: return true;
            default: return false;
        }
    }

    void OnEvacuate()
    {
        DoEvacuate();
    }

    void DoEvacuate()
    {
        Time.timeScale = 1f;
        if (root != null) root.SetActive(false);
        var tutorial = TutorialDirector.Instance;
        if (tutorial != null && tutorial.WaitingEvacuate)
            tutorial.WaitingEvacuate = false;
        bool tutorialEvacuate = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        if (!tutorialEvacuate)
            TownHubController.PendingOpenAdventure = true;
        BattleManager.Instance?.TriggerEvacuation();
    }

    void OnToggleAudio()
    {
        GameAudio.AudioEnabled = !GameAudio.AudioEnabled;
        RefreshToggles();
    }

    void OnToggleExtra(SettingsToggleId id)
    {
        switch (id)
        {
            case SettingsToggleId.MercSkillAuto:
                MercSkillMigrate.SetMercSkillAutoCast(!MercSkillMigrate.IsMercSkillAutoCast());
                break;
            case SettingsToggleId.WeatherFx:
                PlayerPrefs.SetInt("fx.weather.on", PlayerPrefs.GetInt("fx.weather.on", 1) != 0 ? 0 : 1);
                PlayerPrefs.Save();
                break;
        }
        RefreshToggles();
    }

    void RefreshToggles()
    {
        ApplyToggleLook(audioToggleButton, audioToggleLabel, GameAudio.AudioEnabled);
        if (extraToggleRows == null) return;
        for (int i = 0; i < extraToggleRows.Count; i++)
        {
            var row = extraToggleRows[i];
            if (row == null || !IsToggleEnabledInBuild(row.id)) continue;
            bool on = ReadToggleState(row.id);
            ApplyToggleLook(row.toggleButton, row.stateLabel, on);
        }
    }

    static bool ReadToggleState(SettingsToggleId id)
    {
        switch (id)
        {
            case SettingsToggleId.MasterAudio: return GameAudio.AudioEnabled;
            case SettingsToggleId.Music: return GameAudio.MusicEnabled;
            case SettingsToggleId.Sfx: return GameAudio.SfxEnabled;
            case SettingsToggleId.WeatherFx: return PlayerPrefs.GetInt("fx.weather.on", 1) != 0;
            case SettingsToggleId.MercSkillAuto: return MercSkillMigrate.IsMercSkillAutoCast();
            default: return true;
        }
    }

    static void ApplyToggleLook(Button btn, Text label, bool on)
    {
        if (label != null) label.text = on ? "开启" : "关闭";
        var img = btn != null ? btn.targetGraphic as Image : null;
        if (img != null)
            img.color = on ? new Color(0.25f, 0.45f, 0.32f, 1f) : new Color(0.4f, 0.26f, 0.26f, 1f);
    }

    void Bind()
    {
        if (root == null)
            root = FindDeep(transform, "Root")?.gameObject ?? gameObject;

        if (closeButton == null) closeButton = FindButton("CloseButton");
        if (evacuateButton == null) evacuateButton = FindButton("EvacuateButton");
        if (primaryButton == null) primaryButton = FindButton("PrimaryButton") ?? FindButton("ResumeButton");
        if (primaryLabel == null && primaryButton != null)
            primaryLabel = primaryButton.GetComponentInChildren<Text>(true);

        if (toggleList == null)
            toggleList = FindDeep(transform, "ToggleList");

        if (audioToggleButton == null)
        {
            var audioRow = FindDeep(transform, "AudioRow");
            if (audioRow != null)
            {
                audioToggleButton = FindDeep(audioRow, "Toggle")?.GetComponent<Button>();
                audioToggleLabel = audioToggleButton != null
                    ? audioToggleButton.GetComponentInChildren<Text>(true)
                    : null;
            }
        }

        CollectExtraRows();

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 540;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
    }

    void CollectExtraRows()
    {
        if (extraToggleRows == null) extraToggleRows = new List<SettingsToggleRow>();
        if (extraToggleRows.Count > 0) return;
        if (toggleList == null) return;

        for (int i = 0; i < toggleList.childCount; i++)
        {
            var child = toggleList.GetChild(i);
            if (child == null || child.name == "AudioRow") continue;
            var id = ParseToggleIdFromName(child.name);
            if (id == SettingsToggleId.MasterAudio) continue;
            var btn = FindDeep(child, "Toggle")?.GetComponent<Button>();
            extraToggleRows.Add(new SettingsToggleRow
            {
                id = id,
                root = child.gameObject,
                toggleButton = btn,
                stateLabel = btn != null ? btn.GetComponentInChildren<Text>(true) : null
            });
        }
    }

    static SettingsToggleId ParseToggleIdFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return SettingsToggleId.MasterAudio;
        string n = name.ToLowerInvariant();
        if (n.Contains("music")) return SettingsToggleId.Music;
        if (n.Contains("sfx") || n.Contains("sound")) return SettingsToggleId.Sfx;
        if (n.Contains("weather")) return SettingsToggleId.WeatherFx;
        if (n.Contains("merc") || n.Contains("佣兵")) return SettingsToggleId.MercSkillAuto;
        return SettingsToggleId.MasterAudio;
    }

    void Wire()
    {
        if (_wired) return;
        _wired = true;
        WireOnce(closeButton, Close);
        WireOnce(primaryButton, Close);
        WireOnce(audioToggleButton, OnToggleAudio);
        WireOnce(evacuateButton, OnEvacuate);
        WireExtraToggles();
    }

    void WireExtraToggles()
    {
        if (extraToggleRows == null) return;
        for (int i = 0; i < extraToggleRows.Count; i++)
        {
            var row = extraToggleRows[i];
            if (row == null || row.toggleButton == null) continue;
            var id = row.id;
            row.toggleButton.onClick.RemoveAllListeners();
            row.toggleButton.onClick.AddListener(() => OnToggleExtra(id));
        }
    }

    static void WireOnce(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    Button FindButton(string name) => FindDeep(transform, name)?.GetComponent<Button>();

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (string.Equals(parent.name, name, StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    /// <summary>编辑器生成预制体 / 运行时兜底共用。</summary>
    public void BuildFallbackHierarchy()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 540;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage(root.transform, "Panel", new Color(0.11f, 0.1f, 0.15f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 440f);

        var title = CreateText(panel.transform, "Title", "设置", 34, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -32f);
        trt.sizeDelta = new Vector2(400f, 48f);

        closeButton = CreateButton(panel.transform, "CloseButton", "×",
            new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(56f, 56f),
            new Color(0.42f, 0.22f, 0.22f, 1f));

        var listGo = new GameObject("ToggleList", typeof(RectTransform));
        listGo.transform.SetParent(panel.transform, false);
        toggleList = listGo.transform;
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.5f, 1f);
        listRt.anchorMax = new Vector2(0.5f, 1f);
        listRt.pivot = new Vector2(0.5f, 1f);
        listRt.anchoredPosition = new Vector2(0f, -100f);
        listRt.sizeDelta = new Vector2(480f, 200f);

        audioToggleButton = CreateRowToggle(toggleList, "AudioRow", "声音", 0f, out audioToggleLabel);

        // 预留行（默认隐藏，IsToggleEnabledInBuild 打开后即显示）
        CreateReservedToggle(toggleList, "MusicRow", "音乐", SettingsToggleId.Music, -76f);
        CreateReservedToggle(toggleList, "SfxRow", "音效", SettingsToggleId.Sfx, -152f);
        CreateReservedToggle(toggleList, "WeatherRow", "天气特效", SettingsToggleId.WeatherFx, -228f);
        CreateReservedToggle(toggleList, "MercSkillAutoRow", "佣兵技能自动", SettingsToggleId.MercSkillAuto, -304f);

        evacuateButton = CreateButton(panel.transform, "EvacuateButton", "撤离",
            new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(300f, 68f),
            new Color(0.55f, 0.3f, 0.22f, 1f));

        primaryButton = CreateButton(panel.transform, "PrimaryButton", "关闭",
            new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 64f),
            new Color(0.24f, 0.4f, 0.3f, 1f));
        primaryLabel = primaryButton.GetComponentInChildren<Text>(true);

        GameFonts.ApplyToHierarchy(transform);
        root.SetActive(false);
        _wired = false;
        Bind();
        Wire();
    }

    void CreateReservedToggle(Transform parent, string rowName, string label, SettingsToggleId id, float y)
    {
        var btn = CreateRowToggle(parent, rowName, label, y, out var state);
        if (extraToggleRows == null) extraToggleRows = new List<SettingsToggleRow>();
        extraToggleRows.Add(new SettingsToggleRow
        {
            id = id,
            root = btn.transform.parent.gameObject,
            toggleButton = btn,
            stateLabel = state
        });
        btn.transform.parent.gameObject.SetActive(false);
    }

    Button CreateRowToggle(Transform parent, string name, string label, float y, out Text stateLabel)
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
            new Color(0.25f, 0.45f, 0.32f, 1f));
        stateLabel = btn.GetComponentInChildren<Text>();
        return btn;
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
        Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var img = CreateImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var text = CreateText(img.transform, "Label", label, 28, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return btn;
    }
}

/// <summary>兼容旧名：战斗/城镇入口仍可调 BattleSettingsPanel.Ensure()。</summary>
public static class BattleSettingsPanel
{
    public static SettingsPopupUI Instance => SettingsPopupUI.Instance;
    public static Button EvacuateButton => SettingsPopupUI.Instance != null ? SettingsPopupUI.Instance.EvacuateButton : null;
    public static bool IsInBattleScene() => SettingsPopupUI.IsInBattleScene();
    public static SettingsPopupUI Ensure() => SettingsPopupUI.Ensure();
    public static void Toggle() => SettingsPopupUI.Toggle();
}
