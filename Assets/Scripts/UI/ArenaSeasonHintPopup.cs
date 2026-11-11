using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场「赛季结算倒计时」信息框（2026-11-11 主人拍板：距结算还剩 5 天起，打开竞技场自动弹一次，每天最多一次）。
///
/// 复用「获得技能」同款皮 <c>Resources/Prefabs/Battle/huodejineng</c>（与 ArenaShopInfoPopupUI 同范式）：
/// 预制体一行不碰，只在实例上改字——biaoti→标题、SkillName→「还剩 N 天」、技能属性→规则一句、
/// Skillxinxi→正文、SkillIcon 隐藏、KeepButton→「知道了」。
/// 不压 Time.timeScale（城镇界面无战斗定时）。文案统一走 <see cref="ArenaSeasonClock"/>。
/// </summary>
public class ArenaSeasonHintPopup : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/huodejineng";

    /// <summary>Canvas sortingOrder：910（城镇确认层），压过竞技场弹窗（900）。</summary>
    const int SortOrder = GameConfig.UiSort.TownConfirm;

    static ArenaSeasonHintPopup _instance;

    GameObject _root;
    Text _title;
    Text _headline;
    Text _ruleLine;
    Text _body;
    Image _icon;
    Button _okBtn;
    Text _okLabel;
    Button _dimBtn;

    /// <summary>弹出信息框（headline 通常是「还剩 N 天」，body 是补充说明）。</summary>
    public static void Show(string title, string headline, string body)
    {
        var ui = Ensure();
        if (ui == null) return;
        ui.Fill(title, headline, body);
        ui.OpenRoot();
    }

    /// <summary>竞技场弹窗关闭时顺带收掉（防 DDOL 残页）。</summary>
    public static void CloseIfOpen()
    {
        if (_instance != null && _instance._root != null && _instance._root.activeSelf)
            _instance.Close();
    }

    static ArenaSeasonHintPopup Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ArenaSeasonHint] 找不到预制体 Resources/{PrefabPath}（赛季结算提示弹不出来）");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "ArenaSeasonHintPopup";
        var ui = go.GetComponent<ArenaSeasonHintPopup>();
        if (ui == null) ui = go.AddComponent<ArenaSeasonHintPopup>();
        // 与 ArenaShopInfoPopupUI / 获得技能弹窗同口径：Screen Space - Camera + 城镇确认层 + 根铺满 + DDOL
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        UnityEngine.Object.DontDestroyOnLoad(go);
        return ui;
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Bind();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Bind()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null)
        {
            Debug.LogError("[ArenaSeasonHint] 预制体缺少节点 Root（赛季结算提示不可用）");
            return;
        }
        var panel = _root.transform.Find("Panel");
        _title = panel?.Find("biaoti")?.GetComponent<Text>();
        _headline = panel?.Find("Skill/SkillName")?.GetComponent<Text>();
        _ruleLine = panel?.Find("Skill/技能属性")?.GetComponent<Text>();
        _body = panel?.Find("Skill/Skillxinxi")?.GetComponent<Text>();
        _icon = panel?.Find("Skill/SkillIcon")?.GetComponent<Image>();
        _okBtn = panel?.Find("KeepButton")?.GetComponent<Button>();
        _okLabel = panel?.Find("KeepButton/Label")?.GetComponent<Text>();
        _dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();

        if (panel == null) Debug.LogError("[ArenaSeasonHint] 预制体缺少 Root/Panel（内容区）");
        if (_title == null || _headline == null || _body == null)
            Debug.LogError("[ArenaSeasonHint] 预制体缺少 biaoti / SkillName / Skillxinxi（提示框文字不全）");
        if (_okBtn == null) Debug.LogError("[ArenaSeasonHint] 预制体缺少 KeepButton（确认钮点了没反应）");
    }

    void Wire()
    {
        if (_okBtn != null) { _okBtn.onClick.RemoveAllListeners(); _okBtn.onClick.AddListener(Close); }
        if (_dimBtn != null) { _dimBtn.onClick.RemoveAllListeners(); _dimBtn.onClick.AddListener(Close); }
    }

    void Fill(string title, string headline, string body)
    {
        if (_title != null) _title.text = title ?? "";
        if (_headline != null) _headline.text = headline ?? "";
        if (_ruleLine != null) _ruleLine.text = ArenaSeasonClock.RuleLine;
        if (_body != null) _body.text = body ?? "";
        if (_icon != null) _icon.enabled = false;   // 纯文字提示，不显示技能图标
        if (_okLabel != null) _okLabel.text = "知道了";
        GameFonts.ApplyToHierarchy(transform);
    }

    void OpenRoot()
    {
        if (_root == null) return;
        _root.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = GetComponent<Canvas>();
        if (canvas != null) UICanvasSetup.RefreshPopup(canvas, SortOrder);
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }
}
