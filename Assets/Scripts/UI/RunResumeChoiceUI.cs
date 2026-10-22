using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-22 主人原话「进了冒险界面就弹出上一局意外退出 是否继续 然后两个按钮确定和取消」】
/// 进冒险界面时弹的「中断局」询问弹窗：上一局战斗中途意外退出没打完 → 是否继续。
/// · 纯代码搭壳，挂在当前场景现有 Canvas 下（不新建 Canvas，不建预制体，预制体归主人）；
/// · 「确定」= 继续上一局（清掉中断标记，之后进本照旧静默续局）；
/// · 「取消」= 重新开始（丢弃上一局，下次进本发启动金、从本章第 1 关打起）。
/// 触发条件与调用点在 AdventureUI.ShowPage → TryPromptInterruptedRun。
/// </summary>
public static class RunResumeChoiceUI
{
    static GameObject _root;

    /// <param name="chapter">中断局所属章（提示文案用）。</param>
    /// <param name="onConfirm">确定：继续上一局。</param>
    /// <param name="onCancel">取消：重新开始（调用方负责 RunLoadout.Clear）。</param>
    public static void Show(int chapter, Action onConfirm, Action onCancel)
    {
        Close();   // 防重入：旧弹窗直接拆，绝不留两张

        var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            // 无 Canvas 弹不出来时不静默：按改动前的行为放行（继续上一局）并把错报出来（铁律 14 fail closed）
            Debug.LogError("[RunResumeChoice] 场景里没有 Canvas，中断局询问弹窗弹不出来，默认按「继续上一局」放行");
            onConfirm?.Invoke();
            return;
        }

        _root = new GameObject("RunResumeChoice", typeof(RectTransform));
        _root.transform.SetParent(canvas.transform, false);
        var rootRt = (RectTransform)_root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // 暗幕：挡住底下点击；不给点击关闭 —— 必须二选一
        var dim = Mk(_root.transform, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var panel = Mk(_root.transform, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(520f, 300f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.11f, 0.16f, 0.98f);

        Label(panel, "Title", "上一局意外退出", 32, new Vector2(0f, -44f), new Vector2(460f, 48f));
        Label(panel, "Body",
            $"第 {chapter} 章的上一局没打完就退出了，要继续这一局吗？\n点「取消」将重新开始，上一局构筑与金币不保留。",
            22, new Vector2(0f, -108f), new Vector2(460f, 100f), TextAnchor.UpperCenter);

        // 左：取消（重新开始）
        var cancel = Mk(panel, "CancelButton");
        cancel.anchorMin = cancel.anchorMax = new Vector2(0.28f, 0f);
        cancel.pivot = new Vector2(0.5f, 0f);
        cancel.anchoredPosition = new Vector2(0f, 32f);
        cancel.sizeDelta = new Vector2(190f, 56f);
        cancel.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.28f, 0.22f, 1f);
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.transition = Selectable.Transition.None;
        Label(cancel, "Label", "取消", 24, Vector2.zero, new Vector2(190f, 56f), TextAnchor.MiddleCenter, true);

        // 右：确定（继续上一局）
        var confirm = Mk(panel, "ConfirmButton");
        confirm.anchorMin = confirm.anchorMax = new Vector2(0.72f, 0f);
        confirm.pivot = new Vector2(0.5f, 0f);
        confirm.anchoredPosition = new Vector2(0f, 32f);
        confirm.sizeDelta = new Vector2(190f, 56f);
        confirm.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.42f, 0.3f, 1f);
        var confirmBtn = confirm.gameObject.AddComponent<Button>();
        confirmBtn.transition = Selectable.Transition.None;
        Label(confirm, "Label", "确定", 24, Vector2.zero, new Vector2(190f, 56f), TextAnchor.MiddleCenter, true);

        cancelBtn.onClick.AddListener(() =>
        {
            Close();
            onCancel?.Invoke();
        });
        confirmBtn.onClick.AddListener(() =>
        {
            Close();
            onConfirm?.Invoke();
        });

        _root.transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(_root.transform);
    }

    static void Close()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
    }

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = Mk(parent, name);
        if (stretch)
            Stretch(rt);
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
    }
}
