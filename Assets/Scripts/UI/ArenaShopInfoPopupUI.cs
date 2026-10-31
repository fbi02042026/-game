using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技商店「商品信息」弹窗（2026-10-31 主人拍板「点击购买出现信息框 按照技能那个信息弹窗来」）。
///
/// <para>复用「获得技能」同一张预制体 <c>Resources/Prefabs/Battle/huodejineng</c>
///（同款皮：标题 + 图标 + 名字 + 说明 + 确认钮），与 <see cref="SkillObtainPopupUI"/> 完全同范式：
/// static Ensure + 唯一关闭出口 Close + 8 秒无操作自动关 + DDOL + 925 层。预制体一行不碰，
/// 只在实例上改字/填图：biaoti→「商品信息」、SkillName→商品名、
/// 「技能属性」静态标签→价格行（主人要求「弹窗上要加个价格」+「也要玩家的积分总量」，
/// 此行写「价格 X 积分 · 现有 Y」）、Skillxinxi→商品说明、KeepButton Label→「购买」。</para>
///
/// <para>购买判定/扣账仍在 <see cref="ArenaPopupUI"/>：确认钮回调 onBuy；okGray=true
///（积分不足/满星）时确认钮置灰，点了照样回调（由调用方 toast 拦截，不入账）——同旧
/// StaminaConfirmPopup.SetOkGray 口径。</para>
/// </summary>
public class ArenaShopInfoPopupUI : MonoBehaviour
{
    /// <summary>预制体路径（与「获得技能」弹窗同一张皮）。</summary>
    const string PrefabPath = "Prefabs/Battle/huodejineng";

    /// <summary>无操作自动关兜底（秒），与技能弹窗同档。</summary>
    const float AutoCloseSeconds = 8f;

    /// <summary>Canvas sortingOrder：925，压过竞技场弹窗（TownPopup 900）。</summary>
    const int SortOrder = 925;

    /// <summary>确认钮置灰色（同 StaminaConfirmPopup 置灰观感）。</summary>
    static readonly Color OkGray = new Color(0.55f, 0.55f, 0.55f, 1f);

    static ArenaShopInfoPopupUI _instance;
    public static ArenaShopInfoPopupUI Instance => _instance;

    GameObject _root;
    Button _dimBtn;
    Button _okBtn;
    Image _okImg;
    Text _okLabel;
    Text _title;
    Text _name;
    Text _priceLine;   // 预制体「技能属性」静态标签，本弹窗改写为价格行
    Text _body;
    Image _icon;

    System.Action _onBuy;
    Coroutine _autoCo;
    bool _okGray;   // 本次填充是否置灰（OpenRoot 里据此关掉确认钮按压反馈，防反馈把置灰色洗回白）

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 弹商品信息框。okGray=true 确认钮置灰（积分不足/满星），点了仍回调 onBuy（调用方拦截 toast）。
    /// </summary>
    public static void Show(string title, string goodsName, string priceLine, string body,
        Sprite icon, bool okGray, System.Action onBuy)
    {
        var ui = Ensure();
        if (ui == null) return;
        ui._onBuy = onBuy;
        if (!ui.Fill(title, goodsName, priceLine, body, icon, okGray)) { ui.Close(); return; }
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
    }

    /// <summary>唯一关闭出口（点确认/点暗带/自动兜底都走这）。</summary>
    void Close()
    {
        if (_root != null) _root.SetActive(false);
        _onBuy = null;
    }

    /// <summary>竞技场弹窗关闭时顺带收掉本弹窗（防 DDOL 残页）。</summary>
    public static void CloseIfOpen()
    {
        if (_instance != null && _instance._root != null && _instance._root.activeSelf)
            _instance.Close();
    }

    static ArenaShopInfoPopupUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ArenaShopInfo] 找不到预制体 Resources/Prefabs/Battle/huodejineng" +
                           "（与「获得技能」弹窗同一张皮，确认文件存在）");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "ArenaShopInfoPopup";
        var ui = go.GetComponent<ArenaShopInfoPopupUI>();
        if (ui == null) ui = go.AddComponent<ArenaShopInfoPopupUI>();
        // 与 SkillObtainPopupUI 同口径：Screen Space - Camera + 925 层 + 根铺满 + DDOL
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        UnityEngine.Object.DontDestroyOnLoad(go);
        return ui;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Bind();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ============================================================
    // 节点绑定（只认 huodejineng 预制体既有节点，缺了报错不静默）
    // ============================================================

    void Bind()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null)
        {
            Debug.LogError("[ArenaShopInfo] 预制体缺少节点 Root，商品信息弹窗不可用");
            return;
        }
        var panel = _root.transform.Find("Panel");
        _dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        _title = panel?.Find("biaoti")?.GetComponent<Text>();
        _name = panel?.Find("Skill/SkillName")?.GetComponent<Text>();
        _priceLine = panel?.Find("Skill/技能属性")?.GetComponent<Text>();
        _body = panel?.Find("Skill/Skillxinxi")?.GetComponent<Text>();
        _icon = panel?.Find("Skill/SkillIcon")?.GetComponent<Image>();
        _okBtn = panel?.Find("KeepButton")?.GetComponent<Button>();
        _okImg = panel?.Find("KeepButton")?.GetComponent<Image>();
        _okLabel = panel?.Find("KeepButton/Label")?.GetComponent<Text>();
        // 缺了报错不静默（本类头注释承诺的口径；Bind 静默失败 = 确认钮没接回调 = 点了无反应且无迹可查）
        if (panel == null) Debug.LogError("[ArenaShopInfo] 预制体缺少节点 Root/Panel（信息弹窗内容区）");
        if (_okBtn == null) Debug.LogError("[ArenaShopInfo] 缺少节点 Panel/KeepButton（确认钮没接回调，点了无反应）");
        if (_okLabel == null) Debug.LogError("[ArenaShopInfo] 缺少节点 Panel/KeepButton/Label（确认钮文案不显示）");
    }

    void Wire()
    {
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
        if (_okBtn != null)
        {
            _okBtn.onClick.RemoveAllListeners();
            _okBtn.onClick.AddListener(OnOkClicked);
        }
    }

    void OnOkClicked()
    {
        var buy = _onBuy;
        Close();
        buy?.Invoke();   // 先关弹窗再回调：toast / 获得演出不会被本弹窗压住
    }

    void OpenRoot()
    {
        if (_root == null) return;
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        var c = GetComponent<Canvas>();
        if (c != null) UICanvasSetup.RefreshPopup(c, SortOrder);
        // 置灰键（积分不足/满星）：关掉按压反馈。反馈组件是 RefreshPopup→AttachUnder 这一步才挂上的
        //（晚于 Fill），其 Restore 会把 targetGraphic 色洗回捕获时的白——置灰一按就变白。关掉后再补一次灰。
        if (_okGray && _okImg != null)
        {
            UiButtonPressFeedback.Suppress(_okImg.gameObject);
            _okImg.color = OkGray;
        }
    }

    void StopAutoClose()
    {
        if (_autoCo != null)
        {
            StopCoroutine(_autoCo);
            _autoCo = null;
        }
    }

    IEnumerator CoAutoClose()
    {
        float t = 0f;
        while (t < AutoCloseSeconds)
        {
            if (_root == null || !_root.activeSelf) yield break;
            t += Time.unscaledDeltaTime;   // unscaled：竞技场无暂停，但与技能弹窗同口径
            yield return null;
        }
        Close();
    }

    // ============================================================
    // 内容填充（只填数据，不碰排版）
    // ============================================================

    bool Fill(string title, string goodsName, string priceLine, string body,
        Sprite icon, bool okGray)
    {
        if (_root == null) return false;

        SetText(_title, "Panel/biaoti", title);
        SetText(_name, "Panel/Skill/SkillName", goodsName);
        SetText(_priceLine, "Panel/Skill/技能属性", priceLine);
        SetText(_body, "Panel/Skill/Skillxinxi", body);
        SetText(_okLabel, "Panel/KeepButton/Label", "购买");

        if (_icon == null)
            Debug.LogError("[ArenaShopInfo] 缺少节点 Panel/Skill/SkillIcon（商品图标不显示）");
        else
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _icon.preserveAspect = true;
        }

        if (_okImg == null)
            Debug.LogError("[ArenaShopInfo] 缺少节点 Panel/KeepButton（或无 Image，置灰无效）");
        else
        {
            _okGray = okGray;
            _okImg.color = okGray ? OkGray : Color.white;
        }

        return true;
    }

    static void SetText(Text t, string nodePath, string value)
    {
        if (t == null)
        {
            Debug.LogError("[ArenaShopInfo] 缺少文本节点 " + nodePath);
            return;
        }
        t.text = value ?? "";
    }
}
