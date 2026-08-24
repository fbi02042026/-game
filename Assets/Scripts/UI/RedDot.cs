using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 红点键：谁需要提醒就 Set(key, true)。
/// 图标上挂 UIRedDot 并填 key，或运行时 RedDot.Bind(icon, key)。
/// </summary>
public static class RedDot
{
    static readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
    static readonly Dictionary<string, List<UIRedDot>> _views = new Dictionary<string, List<UIRedDot>>();
    static Sprite _sprite;

    public const string Mail = "mail";
    public const string Notice = "notice";
    public const string Activity = "activity";
    public const string Shop = "shop";
    public const string Rank = "rank";
    public const string Character = "nav.character";
    public const string Tavern = "nav.tavern";
    public const string Log = "nav.log";
    public const string Achievement = "log.achievement";
    public const string Guild = "nav.guild";
    public const string Adventure = "nav.adventure";

    public static Sprite Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;
            _sprite = Resources.Load<Sprite>("UI/RedDot");
            return _sprite;
        }
    }

    public static void Set(string key, bool on)
    {
        if (string.IsNullOrEmpty(key)) return;
        _flags[key] = on;
        if (_views.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null) { list.RemoveAt(i); continue; }
                list[i].Refresh();
            }
        }
    }

    public static bool Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return _flags.TryGetValue(key, out bool v) && v;
    }

    public static void Register(UIRedDot view)
    {
        if (view == null || string.IsNullOrEmpty(view.key)) return;
        if (!_views.TryGetValue(view.key, out var list))
        {
            list = new List<UIRedDot>();
            _views[view.key] = list;
        }
        if (!list.Contains(view)) list.Add(view);
        view.Refresh();
    }

    public static void Unregister(UIRedDot view)
    {
        if (view == null || string.IsNullOrEmpty(view.key)) return;
        if (_views.TryGetValue(view.key, out var list))
            list.Remove(view);
    }

    /// <summary>给任意图标右上角挂红点（没有则创建）</summary>
    public static UIRedDot Bind(Transform icon, string key, Vector2? offset = null, float size = 22f)
    {
        if (icon == null || string.IsNullOrEmpty(key)) return null;
        var existing = icon.GetComponentInChildren<UIRedDot>(true);
        if (existing != null && existing.key == key)
        {
            existing.Refresh();
            return existing;
        }

        var go = new GameObject("RedDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIRedDot));
        go.transform.SetParent(icon, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = offset ?? new Vector2(-6f, -6f);

        var img = go.GetComponent<Image>();
        img.sprite = Sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;

        var dot = go.GetComponent<UIRedDot>();
        dot.key = key;
        dot.autoCreate = false;
        Register(dot);
        dot.Refresh();
        return dot;
    }

    /// <summary>根据邮件、可领成就里程等刷新常用红点</summary>
    public static void RefreshCommon()
    {
        Set(Mail, MailSystem.UnclaimedCount() > 0);
        bool reward = AchievementSystem.Instance != null
                      && AchievementSystem.Instance.HasUnclaimedMilestone();
        Set(Activity, reward);
        Set(Log, reward);
        Set(Achievement, reward);
    }
}
