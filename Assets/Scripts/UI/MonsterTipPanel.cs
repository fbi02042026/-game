using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-11-03 主人拍板「弹窗跟图鉴那个用的是一个就行」】怪物信息小弹窗唯一组件：
/// 冒险日志怪物 Tip（AdventureUI.ShowMonsterTip）与战斗内词条怪首次登场提示
/// （AffixMonsterNoticeUI）共用同一套运行时构建的弹窗，外观/交互完全一致：
/// 280×140 深色底 + 标题 + 正文，点弹窗任意处关闭。
/// - 静态单例挂宿主 Transform 下；宿主随场景销毁后自动重建（Unity 伪空判定）；
/// - Show 的 size 参数允许调用方按内容加高（词条行多时 280×196，避免溢出）；
/// - onClose：点弹窗关闭时的回调（图鉴为 null；词条提示用它做「看过永久消失」标记）。
/// </summary>
public static class MonsterTipPanel
{
    static GameObject _root;
    static Text _title;
    static Text _body;
    static Action _onClose;
    static Transform _host;

    static readonly Vector2 DefaultSize = new Vector2(280f, 140f);

    /// <summary>当前弹窗根（可能为已销毁伪空，调用方自行判空）。</summary>
    public static RectTransform RootRt => _root != null ? _root.transform as RectTransform : null;

    /// <summary>确保弹窗已构建（外观与原图鉴 EnsureTip 完全一致，只是搬进了共享组件）。
    /// 宿主校验：若旧弹窗还挂在别的宿主下（如城镇 DDOL 页 vs 战斗内 Canvas），就地销毁迁到新宿主重建。</summary>
    public static GameObject Ensure(Transform host)
    {
        if (_root != null && _host != host)
        {
            UnityEngine.Object.Destroy(_root);   // 场景/宿主变了：老树下不结果，迁址重建
            _root = null;
        }
        if (_root != null) return _root;
        if (host == null) return null;
        _host = host;

        var go = new GameObject("MonsterTip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = DefaultSize;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.92f);

        var titleGo = new GameObject("TipTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(go.transform, false);
        var tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1);
        tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -8);
        tr.sizeDelta = new Vector2(-16, 32);
        _title = titleGo.GetComponent<Text>();
        _title.fontSize = 22;
        _title.alignment = TextAnchor.MiddleCenter;
        _title.color = new Color(1f, 0.92f, 0.7f);
        _title.font = GameFonts.GetChinese();

        var bodyGo = new GameObject("TipBody", typeof(RectTransform), typeof(Text));
        bodyGo.transform.SetParent(go.transform, false);
        var br = bodyGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 1);
        br.offsetMin = new Vector2(12, 10);
        br.offsetMax = new Vector2(-12, -42);
        _body = bodyGo.GetComponent<Text>();
        _body.fontSize = 18;
        _body.alignment = TextAnchor.UpperCenter;
        _body.color = new Color(0.9f, 0.85f, 0.75f);
        _body.font = GameFonts.GetChinese();

        var closeBtn = go.AddComponent<Button>();
        closeBtn.targetGraphic = go.GetComponent<Image>();
        closeBtn.onClick.AddListener(OnClicked);

        GameFonts.ApplyToHierarchy(go.transform);
        _root = go;
        go.SetActive(false);
        return _root;
    }

    /// <summary>
    /// 显示弹窗（幂等：重复调用覆盖内容）。size 传 default 按默认 280×140。
    /// </summary>
    public static void Show(Transform host, string title, string body,
        Vector2 size = default, Action onClose = null)
    {
        var root = Ensure(host);
        if (root == null) return;
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = size == default ? DefaultSize : size;
        if (_title != null) _title.text = title ?? "";
        if (_body != null) _body.text = body ?? "";
        _onClose = onClose;
        root.SetActive(true);
    }

    public static void Hide()
    {
        if (_root != null) _root.SetActive(false);
        _onClose = null;
    }

    static void OnClicked()
    {
        var cb = _onClose;
        Hide();
        cb?.Invoke();   // 先 Hide 再回调，回调里再 Show 也不会被旧状态干扰
    }
}
