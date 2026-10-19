using System;
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
        if (_title != null) _title.text = title ?? "体力提示";
        if (_body != null) _body.text = body ?? "";
        if (_okLabel != null) _okLabel.text = okLabel ?? "确认";
        if (_cancelLabel != null) _cancelLabel.text = "取消";
        _onOk = onConfirm;
        _rootPanel.SetActive(true);
        _host.transform.SetAsLastSibling();
        var canvas = _host.GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownConfirm);
    }

    public static void Close()
    {
        if (_rootPanel != null) _rootPanel.SetActive(false);
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
