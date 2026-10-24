using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 体力通用确认弹窗（2026-10-18 主人拍板）：邮件领取体力 / 背包使用体力药水 共用。
/// 场景：体力将超出上限时，提示「多余的会浪费」，玩家确认后才执行。
///
/// 【2026-10-18 主人拍板「EvacuateConfirmPopup 设置成默认的弹窗形式，类似窗体都用它」】
/// → 本弹窗弃用旧的纯色块运行时建树，改挂全项目默认弹窗预制体
/// <see cref="EvacuateConfirmPopupUI.PrefabPath"/>（Root/Dim/Panel/Title/Body/双按钮 同一套美术）。
/// 与战斗撤离弹窗的两点差别：① 不动 Time.timeScale（体力确认在城镇流程里，撤离才定格战斗）；
/// ② 层级走 TownConfirm(910)——严格高于 TownPopup(900)/RewardPopup(905)，否则从设置弹窗（900）里打开时
/// 排序不定、点击被设置弹窗全屏 Dim 吃掉（2026-10-19 清除数据确认框点不动事故）。标题钉死「体力提示」，正文/确认键文案由调用方传。
/// </summary>
public static class StaminaConfirmPopup
{
    static GameObject _host;
    static GameObject _rootPanel;
    static Text _title;
    static Text _body;
    static Text _okLabel;
    static Text _cancelLabel;
    static Button _okBtn;
    static Button _cancelBtn;
    static Button _dimBtn;
    static Action _onOk;

    // 单按钮告知模式的几何快照（第一次切单按钮前从预制体捕获，切回双按钮时原样还原）
    static RectTransform _okRt;
    static bool _okGeoSaved;
    static Vector2 _okAnchorMin;
    static Vector2 _okAnchorMax;
    static Vector2 _okPos;
    static bool _singleButton;

    // 底框自适应：面板 → 首次收紧前捕获的预制体原始尺寸（2026-10-24 主人拍板）。
    // 换实例/重建后是新引用自动重捕；旧实例的残留条目随域重载清掉，量级个位数无害。
    // 【2026-10-24】原 2026-10-21 的告知模式排版快照（_bodyAlignPrefab 等四件套）已随
    // 「全弹窗统一排版」拍板删除：两套排版合成一套，无需再快照还原。
    static readonly Dictionary<Transform, Vector2> _panelPrefabSizes = new Dictionary<Transform, Vector2>();

    /// <summary>弹确认框：body 提示文案（含会浪费多少点），okLabel 确认键文字，onConfirm 点确认后回调。
    /// 标题沿用「体力提示」。</summary>
    public static void Show(string body, string okLabel, Action onConfirm)
        => Show("体力提示", body, okLabel, onConfirm);

    /// <summary>带标题版本（2026-10-19）：设置弹窗「清除数据」等通用确认复用同一套默认弹窗预制体，
    /// 体力线三个调用点走上面的旧签名不受影响。</summary>
    public static void Show(string title, string body, string okLabel, Action onConfirm)
    {
        Ensure();
        if (_rootPanel == null) return;
        ApplySingleButton(false);   // 双按钮确认框（体力/清除数据）：取消键必须在
        if (_title != null) _title.text = title ?? "体力提示";
        if (_body != null) _body.text = body ?? "";
        if (_okLabel != null) _okLabel.text = okLabel ?? "确认";
        if (_cancelLabel != null) _cancelLabel.text = "取消";
        ApplyUnifiedTextStyle(_title, _body);   // 2026-10-24 统一排版（先设行距再算底框自适应）
        _onOk = onConfirm;
        _rootPanel.SetActive(true);
        ApplyPanelFit();
        _host.transform.SetAsLastSibling();
        var canvas = _host.GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownConfirm);
    }

    /// <summary>
    /// 【2026-10-20 主人拍板「这个界面用通用弹窗」】纯告知型**单按钮**模式：
    /// 底框 / 标题 / 正文 / 层级全部沿用默认弹窗预制体（EvacuateConfirmPopup），
    /// 只把「继续（取消）」那个副按钮收起来、确认键从预制体的 0.28 位置挪到中轴 ——
    /// 告知型没有「取消」这个语义，留两个按钮会让人以为要选什么。
    /// 几何只在运行时挪，关闭时原样还原，绝不改预制体（铁律 3）。
    ///
    /// 【2026-10-21 主人拍板】告知型排版也走同一套快照：正文改居中 + 行距 1.2、标题改报喜金。
    /// 原因：长文案左对齐 + 行距 1.0 在 460 宽里挤成六行、正文底几乎贴确认键；
    /// 报喜型弹窗的武器名也要单独拎出来（品质色 + 放大），所以正文本身改成居中排。
    /// 【2026-10-24 主人拍板】这套排版已升级为全弹窗统一排版（见 <see cref="ApplyUnifiedTextStyle"/>），
    /// 双按钮/告知不再两套来回切，快照还原机制删除。
    /// </summary>
    public static void ShowNotice(string title, string body, string okLabel, Action onClose)
    {
        Ensure();
        if (_rootPanel == null) return;
        ApplySingleButton(true);
        if (_title != null) _title.text = title ?? "";
        if (_body != null) _body.text = body ?? "";
        if (_okLabel != null) _okLabel.text = okLabel ?? "确认";
        ApplyUnifiedTextStyle(_title, _body);   // 2026-10-24 统一排版（先设行距再算底框自适应）
        _onOk = onClose;
        _rootPanel.SetActive(true);
        ApplyPanelFit();
        _host.transform.SetAsLastSibling();
        var canvas = _host.GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownConfirm);
    }

    /// <summary>
    /// 单/双按钮切换的唯一出口。切单按钮：隐藏 ContinueButton、确认键居中（y 保持预制体值）；
    /// 切回双按钮：按首次捕获的预制体几何原样还原。重复调用同一状态直接返回（幂等）。
    /// </summary>
    static void ApplySingleButton(bool on)
    {
        if (_singleButton == on) return;
        _singleButton = on;

        if (_okRt != null && !_okGeoSaved)
        {
            _okAnchorMin = _okRt.anchorMin;
            _okAnchorMax = _okRt.anchorMax;
            _okPos = _okRt.anchoredPosition;
            _okGeoSaved = true;
        }
        if (_cancelBtn != null)
            _cancelBtn.gameObject.SetActive(!on);
        if (_okRt == null || !_okGeoSaved) return;

        if (on)
        {
            _okRt.anchorMin = _okRt.anchorMax = new Vector2(0.5f, 0f);
            _okRt.anchoredPosition = new Vector2(0f, _okPos.y);
        }
        else
        {
            _okRt.anchorMin = _okAnchorMin;
            _okRt.anchorMax = _okAnchorMax;
            _okRt.anchoredPosition = _okPos;
        }
    }

    /// <summary>
    /// 默认弹窗文字排版的唯一出口（2026-10-24 主人拍板「调用默认弹窗的里面文字都要用
    /// 统一的优化后的排版」）：标题报喜金 <see cref="RarityPalette.Gold"/>、正文居中 + 行距 1.2
    /// —— 即 2026-10-21 暮火之杖告知窗那套优化排版，如今全弹窗统一。
    /// 原双按钮/告知两套排版与配套快照还原机制（ApplyNoticeStyle）随之删除，不再来回切。
    /// 排版每次开窗直接设，无需关窗还原；体力/清除数据/解锁格子/告知窗/撤离弹窗共用。
    /// </summary>
    public static void ApplyUnifiedTextStyle(Text title, Text body)
    {
        if (title != null) title.color = RarityPalette.Gold;
        if (body != null)
        {
            body.alignment = TextAnchor.UpperCenter;
            body.lineSpacing = 1.2f;
        }
    }

    /// <summary>
    /// 【2026-10-24 主人拍板「默认弹窗排版空隙太多，底框可以缩放拉伸」】
    /// 预制体 Panel 高 440 是按告知窗长文案留的，一行确认语底下空一大块（截图实例：解锁背包格子）。
    /// 底框九宫格切片本就可拉伸 → 按正文实际高度收紧中间空档：标题/正文锚顶不动、按钮锚底随底边上移。
    /// 几何真值全部取自预制体节点本身（正文起点 y、按钮底缘 y + 实高），不写死尺寸；
    /// 只缩不涨 —— 长文案算出来比预制体高时钳回预制体值，告知窗六行排版不受影响。
    /// 默认弹窗底框自适应的唯一出口：本类全家（体力/清除数据/解锁格子/告知窗）与
    /// EvacuateConfirmPopupUI（撤离弹窗共用同一预制体）都走这里。
    /// 节点缺了就不动（保持预制体原样 440，弹窗功能不受影响）。
    /// </summary>
    public static void FitPanelToBody(RectTransform panel, Text body, RectTransform bottomButton)
    {
        if (panel == null || body == null || bottomButton == null) return;
        float topOffset = -body.rectTransform.anchoredPosition.y;      // 面板顶沿 → 正文起点（预制体 -100）
        float buttonZone = bottomButton.anchoredPosition.y
                         + bottomButton.rect.height;                   // 面板底沿 → 按钮顶缘（预制体 28 + 33×1.7 ≈ 84，rect 已含缩放）
        if (!_panelPrefabSizes.TryGetValue(panel, out var prefabSize))
        {
            prefabSize = panel.sizeDelta;                              // 首次见到这块面板 → 记预制体原始尺寸
            _panelPrefabSizes[panel] = prefabSize;
        }
        float fit = topOffset + body.preferredHeight + 36f + buttonZone;   // 正文与按钮顶缘之间留 36 设计像素呼吸
        panel.sizeDelta = new Vector2(prefabSize.x, Mathf.Min(prefabSize.y, fit));
    }

    /// <summary>本类入口的薄适配：用已绑定的 Panel/Body/确认键做底框自适应。</summary>
    static void ApplyPanelFit()
    {
        var panel = _rootPanel != null ? _rootPanel.transform.Find("Panel") as RectTransform : null;
        FitPanelToBody(panel, _body, _okRt);
    }

    public static void Close()
    {
        if (_rootPanel != null) _rootPanel.SetActive(false);
        ApplySingleButton(false);   // 收窗即还原，下次撤离/体力确认仍是双按钮
        _onOk = null;
    }

    static void Ensure()
    {
        // Unity 假 null：场景切换/域重载后 _host 被销毁会走重建
        if (_host != null) return;

        var prefab = Resources.Load<GameObject>(EvacuateConfirmPopupUI.PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过（铁律 14）：缺默认弹窗预制体必须喊出来
            Debug.LogError("[StaminaConfirm] 找不到默认弹窗预制体 Resources/" +
                           EvacuateConfirmPopupUI.PrefabPath + "，体力确认弹窗不可用");
            return;
        }

        _host = UnityEngine.Object.Instantiate(prefab);
        _host.name = "StaminaConfirmPopup";
        UnityEngine.Object.DontDestroyOnLoad(_host);
        var canvas = _host.GetComponent<Canvas>();
        if (canvas == null) canvas = _host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownConfirm, UICanvasSetup.ResolveUiCamera());

        Bind();
        Wire();
        if (_rootPanel != null) _rootPanel.SetActive(false);
    }

    static void Bind()
    {
        var rootT = _host.transform.Find("Root")?.gameObject;
        _rootPanel = rootT != null ? rootT : _host;
        _title = FindDeep(_rootPanel.transform, "Title")?.GetComponent<Text>();
        _body = FindDeep(_rootPanel.transform, "Body")?.GetComponent<Text>();
        _okBtn = FindDeep(_rootPanel.transform, "EvacuateButton")?.GetComponent<Button>();
        _cancelBtn = FindDeep(_rootPanel.transform, "ContinueButton")?.GetComponent<Button>();
        _dimBtn = FindDeep(_rootPanel.transform, "Dim")?.GetComponent<Button>();
        _okLabel = FindDeep(_rootPanel.transform, "EvacuateButton")?.Find("Label")?.GetComponent<Text>();
        _cancelLabel = FindDeep(_rootPanel.transform, "ContinueButton")?.Find("Label")?.GetComponent<Text>();
        _okRt = _okBtn != null ? _okBtn.transform as RectTransform : null;
        _okGeoSaved = false;   // 重建后几何要重新捕获（预制体永远是真值）
        _singleButton = false;
    }

    static void Wire()
    {
        // 每次重建/重开都重绑一次，防止监听丢失（与 EvacuateConfirmPopupUI.Wire 同口径）
        if (_okBtn != null)
        {
            _okBtn.onClick.RemoveAllListeners();
            _okBtn.onClick.AddListener(OnOkClicked);
        }
        if (_cancelBtn != null)
        {
            _cancelBtn.onClick.RemoveAllListeners();
            _cancelBtn.onClick.AddListener(Close);
        }
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
    }

    static void OnOkClicked()
    {
        var act = _onOk;
        Close();
        act?.Invoke();
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
