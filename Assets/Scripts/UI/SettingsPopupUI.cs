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
/// 策划口径：音乐/音效/自动技能分控且全局持久；战斗显「撤离 / 取消」；登录与城镇显「确定」。
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
    public Button confirmButton;
    public Text primaryLabel;
    public Transform toggleList;

    [Tooltip("预留：日后「音乐」「音效」「天气」等行挂在这里，运行时按启用表显隐")]
    public List<SettingsToggleRow> extraToggleRows = new List<SettingsToggleRow>();

    SettingsHost _host = SettingsHost.Auto;
    float _prevTimeScale = 1f;

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
        if (Instance != null)
        {
            Instance.PrepareVisible();
            return Instance;
        }

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
        panel.PrepareVisible();
        return panel;
    }

    void Awake()
    {
        Instance = this;
        PrepareVisible();
        Wire();
        if (root != null) root.SetActive(false);
    }

    /// <summary>预制体根 scale 可能为 0，不修复则弹窗永远不可见。</summary>
    public void PrepareVisible()
    {
        if (transform.localScale == Vector3.zero)
            transform.localScale = Vector3.one;
        Bind();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, ResolvePopupSort(CurrentHost));
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
        PrepareVisible();
        Wire();
        _host = host;
        _prevTimeScale = Time.timeScale;
        bool battle = CurrentHost == SettingsHost.Battle;
        if (battle) Time.timeScale = 0f;

        if (root == null) BuildFallbackHierarchy();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, ResolvePopupSort(CurrentHost));
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        transform.SetAsLastSibling();

        RefreshToggles();
        RefreshHostChrome();
        GameFonts.ApplyToHierarchy(transform);

        // #region agent log
        DebugAgentLog.Log("H11", "SettingsPopupUI.Open", "settings_open",
            $"{{\"host\":\"{CurrentHost}\",\"sort\":{ResolvePopupSort(CurrentHost)},\"rootActive\":{(root != null && root.activeSelf ? "true" : "false")}}}");
        // #endregion
    }

    static int ResolvePopupSort(SettingsHost host)
    {
        return ResolveHost(host) == SettingsHost.Battle
            ? GameConfig.UiSort.BattleEvacuate
            : GameConfig.UiSort.TownPopup;
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

        if (!battle)
            Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;

        if (confirmButton != null)
            confirmButton.gameObject.SetActive(!battle);
        if (evacuateButton != null)
            evacuateButton.gameObject.SetActive(battle);
        if (primaryButton != null)
            primaryButton.gameObject.SetActive(battle);

        ApplyPanelAndButtonLayout();
        ApplyExtraToggleVisibility();
    }

    const float PanelWidth = 560f;
    const float PanelHeight = 720f;
    const float ButtonLiftY = 80f;

    void ApplyPanelAndButtonLayout()
    {
        var panel = FindDeep(transform, "Panel") as RectTransform;
        if (panel != null)
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        LiftButton(confirmButton, 172f + ButtonLiftY);
        LiftButton(evacuateButton, 223f + ButtonLiftY);
        LiftButton(primaryButton, 115f + ButtonLiftY);
    }

    static void LiftButton(Button btn, float y)
    {
        if (btn == null) return;
        var rt = btn.GetComponent<RectTransform>();
        if (rt == null) return;
        var p = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(p.x, y);
    }

    void ApplyExtraToggleVisibility()
    {
        bool battle = CurrentHost == SettingsHost.Battle;
        if (extraToggleRows != null)
        {
            for (int i = 0; i < extraToggleRows.Count; i++)
            {
                var row = extraToggleRows[i];
                if (row == null || row.root == null) continue;
                bool on = IsToggleEnabledInBuild(row.id);
                if (row.id == SettingsToggleId.MercSkillAuto)
                    on = on && battle;
                row.root.SetActive(on);
            }
        }

        SetNamedActive("CombatSectionHeader", battle);
        SetNamedActive("CombatDivider", battle);
        SetNamedActive("OtherSectionHeader", battle);
        SetNamedActive("OtherDivider", battle);
        SetSectionHeaderActiveByText("战斗设置", battle);
        SetSectionHeaderActiveByText("其他设置", battle);
    }

    void SetNamedActive(string name, bool on)
    {
        var t = FindDeep(transform, name);
        if (t != null) t.gameObject.SetActive(on);
    }

    void SetSectionHeaderActiveByText(string label, bool on)
    {
        if (toggleList == null) return;
        for (int i = 0; i < toggleList.childCount; i++)
        {
            var child = toggleList.GetChild(i);
            if (child == null) continue;
            var texts = child.GetComponentsInChildren<Text>(true);
            for (int t = 0; t < texts.Length; t++)
            {
                if (texts[t] != null && texts[t].text == label)
                {
                    child.gameObject.SetActive(on);
                    break;
                }
            }
        }
    }

    /// <summary>V1.0：音乐/音效/佣兵自动技能；总声音行已废弃。</summary>
    public static bool IsToggleEnabledInBuild(SettingsToggleId id)
    {
        switch (id)
        {
            case SettingsToggleId.MasterAudio: return false;
            case SettingsToggleId.Music: return true;
            case SettingsToggleId.Sfx: return true;
            case SettingsToggleId.WeatherFx: return false;
            case SettingsToggleId.MercSkillAuto: return true;
            default: return false;
        }
    }

    void OnEvacuate()
    {
        Time.timeScale = 1f;
        if (root != null) root.SetActive(false);
        EvacuateConfirmPopupUI.Show(ConfirmEvacuate, OnEvacuateCancelled);
    }

    void OnEvacuateCancelled()
    {
        Time.timeScale = 1f;
        if (root != null) root.SetActive(true);
    }

    void ConfirmEvacuate()
    {
        DoEvacuate();
    }

    void DoEvacuate()
    {
        // #region agent log
        DebugAgentLog.Log("H11", "SettingsPopupUI.DoEvacuate", "evacuate_clicked",
            $"{{\"tutorialRun\":{(BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun ? "true" : "false")},\"waitingEvac\":{(TutorialDirector.Instance != null && TutorialDirector.Instance.WaitingEvacuate ? "true" : "false")}}}");
        // #endregion
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
            case SettingsToggleId.Music:
                GameAudio.MusicEnabled = !GameAudio.MusicEnabled;
                break;
            case SettingsToggleId.Sfx:
                GameAudio.SfxEnabled = !GameAudio.SfxEnabled;
                break;
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
        if (label != null) label.text = on ? "开" : "关";
        if (btn == null) return;

        var knob = FindDeep(btn.transform, "Knob");
        if (knob != null)
        {
            var krt = knob as RectTransform;
            if (krt != null)
            {
                krt.anchorMin = krt.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
                krt.pivot = new Vector2(on ? 1f : 0f, 0.5f);
                krt.anchoredPosition = new Vector2(on ? -4f : 4f, 0f);
            }
            var knobImg = knob.GetComponent<Image>();
            if (knobImg != null)
            {
                var knSp = LoadSettingsSprite(on ? "开" : "关");
                if (knSp != null)
                {
                    knobImg.sprite = knSp;
                    knobImg.color = Color.white;
                    knobImg.preserveAspect = true;
                }
            }
            return;
        }

        var img = btn.targetGraphic as Image;
        if (img == null) return;
        var sp = LoadSettingsSprite(on ? "开" : "关");
        if (sp != null)
        {
            img.sprite = sp;
            img.color = Color.white;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            return;
        }
        img.color = on ? new Color(0.45f, 0.32f, 0.62f, 1f) : new Color(0.28f, 0.22f, 0.32f, 1f);
    }

    const string SettingsArtRoot = "Assets/Art/UI/设置/";

    static Sprite _cachedOn;
    static Sprite _cachedOff;
    static bool _spriteCacheTried;

    static Sprite LoadSettingsSprite(string fileName)
    {
        var fromRes = Resources.Load<Sprite>("UI/Settings/" + fileName);
        if (fromRes != null) return fromRes;
#if UNITY_EDITOR
        var ed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SettingsArtRoot + fileName + ".png");
        if (ed != null) return ed;
#endif
        EnsureToggleSpriteCache();
        if (fileName == "开") return _cachedOn;
        if (fileName == "关") return _cachedOff;
        return null;
    }

    static void EnsureToggleSpriteCache()
    {
        if (_spriteCacheTried) return;
        _spriteCacheTried = true;
        if (Instance == null) return;
        var knobs = Instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < knobs.Length; i++)
        {
            if (knobs[i] == null || knobs[i].name != "Knob") continue;
            var img = knobs[i].GetComponent<Image>();
            if (img == null || img.sprite == null) continue;
            if (_cachedOn == null) _cachedOn = img.sprite;
        }
    }

    void Bind()
    {
        if (root == null)
            root = FindDeep(transform, "Root")?.gameObject ?? gameObject;

        if (closeButton == null) closeButton = FindButton("CloseButton");
        confirmButton = FindButton("ConfirmButton") ?? FindButton("确定Button") ?? confirmButton;
        evacuateButton = FindButton("EvacuateButton") ?? evacuateButton;
        primaryButton = FindButton("PrimaryButton") ?? FindButton("ResumeButton") ?? primaryButton;
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
        UICanvasSetup.ApplyPopup(canvas, ResolvePopupSort(CurrentHost));

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
    }

    void CollectExtraRows()
    {
        if (extraToggleRows == null) extraToggleRows = new List<SettingsToggleRow>();
        if (toggleList == null) return;

        // 每次按 ToggleList 子节点重建，避免预制体序列化空/旧引用导致开关接不上
        extraToggleRows.Clear();
        for (int i = 0; i < toggleList.childCount; i++)
        {
            var child = toggleList.GetChild(i);
            if (child == null) continue;
            var id = ParseToggleIdFromName(child.name);
            if (id == SettingsToggleId.MasterAudio) continue;
            if (!IsToggleRowName(child.name)) continue;

            var btn = FindDeep(child, "Toggle")?.GetComponent<Button>();
            Text state = FindDeep(child, "StateLabel")?.GetComponent<Text>();
            if (state == null && btn != null)
                state = btn.GetComponentInChildren<Text>(true);

            extraToggleRows.Add(new SettingsToggleRow
            {
                id = id,
                root = child.gameObject,
                toggleButton = btn,
                stateLabel = state
            });
        }
    }

    static bool IsToggleRowName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("row");
    }

    static SettingsToggleId ParseToggleIdFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return SettingsToggleId.MasterAudio;
        string n = name.ToLowerInvariant();
        if (n.Contains("music")) return SettingsToggleId.Music;
        if (n.Contains("sfx") || n.Contains("sound")) return SettingsToggleId.Sfx;
        if (n.Contains("weather")) return SettingsToggleId.WeatherFx;
        if (n.Contains("merc") || n.Contains("skillauto") || n.Contains("佣兵") || n.Contains("自动"))
            return SettingsToggleId.MercSkillAuto;
        return SettingsToggleId.MasterAudio;
    }

    void Wire()
    {
        WireOnce(closeButton, Close);
        WireOnce(confirmButton, Close);
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
        UICanvasSetup.ApplyPopup(canvas, ResolvePopupSort(SettingsHost.Auto));

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage(root.transform, "Panel", new Color(0.08f, 0.06f, 0.1f, 0.98f));
        ApplySprite(panel, null);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(620f, 900f);

        var titlePlate = CreateImage(panel.transform, "TitlePlate", new Color(0.2f, 0.12f, 0.28f, 0.95f));
        var tprt = titlePlate.rectTransform;
        tprt.anchorMin = tprt.anchorMax = new Vector2(0.5f, 1f);
        tprt.pivot = new Vector2(0.5f, 1f);
        tprt.anchoredPosition = new Vector2(0f, -8f);
        tprt.sizeDelta = new Vector2(420f, 88f);

        var title = CreateText(titlePlate.transform, "Title", "设置", 36, TextAnchor.MiddleCenter);
        title.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        Stretch(title.rectTransform);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 1f);
        crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, -108f);
        crt.sizeDelta = new Vector2(540f, 520f);

        var listGo = new GameObject("ToggleList", typeof(RectTransform));
        listGo.transform.SetParent(content.transform, false);
        toggleList = listGo.transform;
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.5f, 1f);
        listRt.anchorMax = new Vector2(0.5f, 1f);
        listRt.pivot = new Vector2(0.5f, 1f);
        listRt.anchoredPosition = Vector2.zero;
        listRt.sizeDelta = new Vector2(540f, 520f);

        float y = 0f;
        CreateSectionHeader(toggleList, "SoundSectionHeader", "声音设置", ref y);
        CreateDivider(toggleList, "SoundDivider", ref y);
        CreateSettingsRow(toggleList, "MusicRow", "音乐", "音乐", SettingsToggleId.Music, ref y);
        CreateSettingsRow(toggleList, "SfxRow", "音效", "音效", SettingsToggleId.Sfx, ref y);

        CreateSectionHeader(toggleList, "CombatSectionHeader", "战斗设置", ref y);
        CreateDivider(toggleList, "CombatDivider", ref y);
        CreateSettingsRow(toggleList, "MercSkillAutoRow", "自动释放技能", "技能释放", SettingsToggleId.MercSkillAuto, ref y);

        CreateSectionHeader(toggleList, "OtherSectionHeader", "其他设置", ref y);
        CreateDivider(toggleList, "OtherDivider", ref y);

        CreateReservedToggle(toggleList, "WeatherRow", "天气特效", SettingsToggleId.WeatherFx, -900f);

        evacuateButton = CreateActionButton(panel.transform, "EvacuateButton", "撤离", "撤离",
            new Vector2(0.5f, 0f), new Vector2(0f, 224f), new Vector2(500f, 80f),
            new Color(0.55f, 0.18f, 0.16f, 1f));

        primaryButton = CreateActionButton(panel.transform, "PrimaryButton", "取消", "取消",
            new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(500f, 80f),
            new Color(0.28f, 0.16f, 0.38f, 1f));
        primaryLabel = primaryButton.GetComponentInChildren<Text>(true);

        confirmButton = CreateActionButton(panel.transform, "ConfirmButton", "确定", "取消",
            new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(500f, 80f),
            new Color(0.28f, 0.16f, 0.38f, 1f));

        closeButton = null;
        audioToggleButton = null;
        audioToggleLabel = null;

        GameFonts.ApplyToHierarchy(transform);
        root.SetActive(false);
        extraToggleRows.Clear();
        Bind();
        Wire();
    }

    void CreateSectionHeader(Transform parent, string name, string label, ref float y)
    {
        var header = CreateText(parent, name, label, 26, TextAnchor.MiddleLeft);
        header.color = new Color(0.92f, 0.78f, 0.42f, 1f);
        var rt = header.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(-20f, y);
        rt.sizeDelta = new Vector2(480f, 40f);
        y -= 48f;
    }

    void CreateDivider(Transform parent, string name, ref float y)
    {
        var div = CreateImage(parent, name, new Color(0.55f, 0.42f, 0.22f, 0.85f));
        ApplySprite(div, LoadSettingsSprite("条"));
        var rt = div.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(500f, 12f);
        y -= 28f;
    }

    void CreateSettingsRow(Transform parent, string rowName, string label, string iconFile,
        SettingsToggleId id, ref float y)
    {
        var row = new GameObject(rowName, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(500f, 64f);

        var icon = CreateImage(row.transform, "Icon", Color.white);
        ApplySprite(icon, LoadSettingsSprite(iconFile));
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(8f, 0f);
        irt.sizeDelta = new Vector2(44f, 44f);
        icon.preserveAspect = true;

        var nameText = CreateText(row.transform, "Name", label, 28, TextAnchor.MiddleLeft);
        nameText.color = new Color(0.92f, 0.78f, 0.42f, 1f);
        var nrt = nameText.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 0.5f);
        nrt.pivot = new Vector2(0f, 0.5f);
        nrt.anchoredPosition = new Vector2(64f, 0f);
        nrt.sizeDelta = new Vector2(240f, 48f);

        var btn = CreateToggleButton(row.transform, "Toggle", out var stateLabel);
        var brt = btn.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-12f, 0f);
        brt.sizeDelta = new Vector2(120f, 48f);

        if (extraToggleRows == null) extraToggleRows = new List<SettingsToggleRow>();
        extraToggleRows.Add(new SettingsToggleRow
        {
            id = id,
            root = row,
            toggleButton = btn,
            stateLabel = stateLabel
        });

        row.SetActive(IsToggleEnabledInBuild(id));
        y -= 72f;
    }

    Button CreateToggleButton(Transform parent, string name, out Text stateLabel)
    {
        var track = CreateImage(parent, name, new Color(0.35f, 0.22f, 0.48f, 1f));
        ApplySprite(track, LoadSettingsSprite("条"));
        var rt = track.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 40f);

        var btn = track.gameObject.AddComponent<Button>();
        btn.targetGraphic = track;
        btn.transition = Selectable.Transition.None;

        var knob = CreateImage(track.transform, "Knob", Color.white);
        ApplySprite(knob, LoadSettingsSprite("开"));
        var krt = knob.rectTransform;
        krt.anchorMin = krt.anchorMax = new Vector2(1f, 0.5f);
        krt.pivot = new Vector2(1f, 0.5f);
        krt.anchoredPosition = new Vector2(-4f, 0f);
        krt.sizeDelta = new Vector2(36f, 36f);
        knob.preserveAspect = true;
        knob.raycastTarget = false;

        stateLabel = CreateText(track.transform, "StateLabel", "开", 22, TextAnchor.MiddleLeft);
        stateLabel.color = new Color(0.92f, 0.78f, 0.42f, 1f);
        var lrt = stateLabel.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(8f, 0f);
        lrt.sizeDelta = new Vector2(40f, 36f);

        return btn;
    }

    Button CreateActionButton(Transform parent, string name, string label, string spriteFile,
        Vector2 anchor, Vector2 pos, Vector2 size, Color fallbackColor)
    {
        var img = CreateImage(parent, name, fallbackColor);
        ApplySprite(img, LoadSettingsSprite(spriteFile));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        img.preserveAspect = false;
        img.type = Image.Type.Sliced;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var text = CreateText(img.transform, "Label", label, 28, TextAnchor.MiddleCenter);
        text.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(72f, 0f);
        trt.offsetMax = new Vector2(-16f, 0f);
        return btn;
    }

    static void ApplySprite(Image img, Sprite sp)
    {
        if (img == null || sp == null) return;
        img.sprite = sp;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    void CreateReservedToggle(Transform parent, string rowName, string label, SettingsToggleId id, float y)
    {
        float rowY = y;
        CreateSettingsRow(parent, rowName, label, "条", id, ref rowY);
        var last = extraToggleRows[extraToggleRows.Count - 1];
        if (last.root != null)
            last.root.SetActive(false);
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
