#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Play 模式下轮询 UI 状态，识别到软著清单界面后自动截图。
/// 由 Editor 菜单「Tools/软著/开启自动截图」在进入 Play 时挂载。
/// </summary>
public class SoftCopyrightAutoCaptureRunner : MonoBehaviour
{
    const string OutputDir = "Docs/软著附图";
    const string Prefix = "SC_像素冒险裂隙之刃_V1.0_";

    struct Rule
    {
        public int Id;
        public string Slug;
        public string Title;
        public Func<bool> Match;
        public float HoldSeconds;
    }

    readonly List<Rule> _rules = new List<Rule>();
    readonly HashSet<int> _captured = new HashSet<int>();
    readonly Dictionary<int, float> _hold = new Dictionary<int, float>();
    float _statusFlash;
    string _lastCaptureTitle = "";

    public static SoftCopyrightAutoCaptureRunner Instance { get; private set; }
    public static bool Enabled { get; private set; }

    public int TotalRules => _rules.Count;
    public int CapturedCount => _captured.Count;

    public static SoftCopyrightAutoCaptureRunner Spawn()
    {
        if (Instance != null) return Instance;
        Enabled = true;
        var go = new GameObject("SoftCopyrightAutoCapture");
        DontDestroyOnLoad(go);
        return go.AddComponent<SoftCopyrightAutoCaptureRunner>();
    }

    public static void Shutdown()
    {
        Enabled = false;
        if (Instance != null)
            Destroy(Instance.gameObject);
    }

    public void ResetProgress()
    {
        _captured.Clear();
        _hold.Clear();
        _lastCaptureTitle = "";
        Debug.Log("[软著自动截图] 已重置进度，可重新识别截图。");
    }

    void Awake()
    {
        Instance = this;
        BuildRules();
        Debug.Log($"[软著自动截图] 已启动，共 {_rules.Count} 项。正常玩游戏即可，识别到界面会自动保存到 {OutputDir}/");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Enabled = false;
    }

    void BuildRules()
    {
        _rules.Clear();
        // 只截文档「必需」项；建议/重复占位不跑。文件名序号与文档鉴别材料一致。

        Add(1, "01_启动_健康忠告", "健康游戏忠告", () =>
            SoftCopyrightUiProbe.IsHealthNoticeVisible, 0.35f);

        Add(2, "02_登录_登录界面", "登录界面", () =>
        {
            var login = SoftCopyrightUiProbe.Login;
            return SoftCopyrightUiProbe.IsBootScene
                   && !SoftCopyrightUiProbe.IsHealthNoticeVisible
                   && login != null && login.gameObject.activeInHierarchy
                   && login.startButton != null && login.startButton.gameObject.activeInHierarchy
                   && !login.IsToastShowing;
        }, 0.6f);

        Add(4, "04_登录_协议提示", "协议未勾选提示", () =>
        {
            var login = SoftCopyrightUiProbe.Login;
            return login != null && login.IsToastShowing
                   && SoftCopyrightUiProbe.TextHas(login.ToastMessage, "请先阅读并同意");
        }, 0.12f);

        Add(5, "05_城镇_城镇大厅", "城镇大厅", () => SoftCopyrightUiProbe.IsGuildHallVisible, 1.0f);

        Add(13, "13_引导_会长对话", "会长开场", () =>
            SoftCopyrightUiProbe.DialogueContains("森林层最近"), 0.9f);

        Add(17, "17_冒险_章节选择", "冒险选章", () =>
            SoftCopyrightUiProbe.IsAdventurePageVisible
            && AdventureUI.Instance.chapterTitle != null
            && !string.IsNullOrEmpty(AdventureUI.Instance.chapterTitle.text),
            0.7f);

        Add(18, "18_战斗_裂缝关卡", "裂缝关卡（怪+Boss）", () =>
            SoftCopyrightUiProbe.IsChapterBattleWithMobAndBoss, 0.7f);

        Add(19, "19_恢复关_生命恢复", "恢复关弹窗", () =>
        {
            var r = SoftCopyrightUiProbe.RestPopup;
            return r != null && r.IsOpen
                   && r.flavorText != null
                   && SoftCopyrightUiProbe.TextHas(r.flavorText.text, "仙泉");
        }, 0.55f);

        Add(20, "20_日志_怪物图鉴", "冒险日志怪物页", () =>
            SoftCopyrightUiProbe.IsAdventureLogMonsterTab, 0.7f);

        Add(23, "23_教学_战斗开场", "教学战斗开场", () =>
            SoftCopyrightUiProbe.IsBattleScene
            && SoftCopyrightUiProbe.HintContains("靠近怪物"), 0.55f);

        Add(24, "24_教学_装备掉落", "装备掉落", () =>
            SoftCopyrightUiProbe.HintContains("地上有装备"), 0.45f);

        Add(25, "25_教学_装备对比", "装备对比弹窗", () =>
        {
            var e = SoftCopyrightUiProbe.EquipDrop;
            return e != null && e.IsOpen && e.Mode == EquipDropMode.ReplaceWorn;
        }, 0.55f);

        Add(26, "26_教学_老盾入队", "老盾入队", () =>
            SoftCopyrightUiProbe.HintContains("老盾加入了队伍"), 0.55f);

        Add(27, "27_教学_技能引导", "技能释放引导", () =>
            SoftCopyrightUiProbe.HintContains("点你的头像放技能"), 0.55f);

        Add(28, "28_教学_战斗设置", "战斗设置面板", () =>
            BattleSettingsPanel.Instance != null && BattleSettingsPanel.Instance.IsOpen, 0.45f);

        Add(29, "29_关卡_轮盘结果", "关卡轮盘", () =>
        {
            var r = SoftCopyrightUiProbe.Roulette;
            return r != null && r.IsResultReady
                   && SoftCopyrightUiProbe.TextHas(r.SubTitle, "命运已定");
        }, 0.55f);

        Add(34, "34_角色_基础属性", "角色属性页", () =>
            SoftCopyrightUiProbe.IsCharacterPageVisible
            && CharacterUI.Instance.attrHpText != null
            && !string.IsNullOrEmpty(CharacterUI.Instance.attrHpText.text),
            0.7f);

        Add(38, "38_酒馆_佣兵招募", "酒馆三选一", () =>
        {
            var t = SoftCopyrightUiProbe.TavernRecruit;
            return t != null && t.IsOpen;
        }, 0.7f);
    }

    void Add(int id, string slug, string title, Func<bool> match, float hold)
    {
        _rules.Add(new Rule { Id = id, Slug = slug, Title = title, Match = match, HoldSeconds = hold });
    }

    void Update()
    {
        if (!Enabled) return;
        _statusFlash -= Time.unscaledDeltaTime;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (_captured.Contains(rule.Id)) continue;

            bool ok = false;
            try { ok = rule.Match(); }
            catch (Exception ex) { Debug.LogWarning($"[软著自动截图] 规则 {rule.Id} 检测异常: {ex.Message}"); }

            if (!ok)
            {
                _hold[rule.Id] = 0f;
                continue;
            }

            float t = _hold.TryGetValue(rule.Id, out float v) ? v : 0f;
            t += dt;
            _hold[rule.Id] = t;
            if (t < rule.HoldSeconds) continue;

            Capture(rule);
            _captured.Add(rule.Id);
            _hold[rule.Id] = 0f;
        }
    }

    void Capture(Rule rule)
    {
        EnsureOutputDir();
        string fileName = Prefix + rule.Slug + ".png";
        string absPath = Path.GetFullPath(Path.Combine(OutputDir, fileName));
        ScreenCapture.CaptureScreenshot(absPath);
        _lastCaptureTitle = rule.Title;
        _statusFlash = 3f;
        Debug.Log($"[软著自动截图] #{rule.Id:D2} {rule.Title} → {absPath} （{_captured.Count + 1}/{_rules.Count}）");
    }

    static void EnsureOutputDir()
    {
        string abs = Path.GetFullPath(OutputDir);
        if (!Directory.Exists(abs))
            Directory.CreateDirectory(abs);
    }

    void OnGUI()
    {
        if (!Enabled) return;
        // 只绘制，不处理鼠标，避免挡住 Game 视图里的 UI 点击
        var e = Event.current;
        if (e == null) return;
        if (e.type != EventType.Repaint && e.type != EventType.Layout) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };
        string flash = _statusFlash > 0f ? $"\n✓ 刚截：{_lastCaptureTitle}" : "";
        GUI.Box(new Rect(8f, 8f, 420f, 72f),
            $"软著自动截图 {_captured.Count}/{_rules.Count}{flash}", style);
    }
}
#endif
