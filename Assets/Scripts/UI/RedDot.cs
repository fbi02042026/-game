using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 红点键：谁需要提醒就 Set(key, true)。
/// 图标上挂 UIRedDot 并填 key，或运行时 RedDot.Bind(icon, key)。
/// 【2026-11-09 主人「必须统一 以后都统一」】全工程红点只有两个出口：
/// 贴图 = <see cref="Sprite"/>、尺寸 = <see cref="DefaultSize"/>。
/// 新增红点一律走这两个，禁止另设 sprite、禁止写死尺寸、禁止再引入第三张红点图；
/// prefab 里手摆的红点图也由代码按本出口覆盖，不另开加载路径。
/// </summary>
public static class RedDot
{
    const string ArtPath = "Assets/Art/UI/Common/红点.png";
    const string ResourcesPath = "UI/Common/红点";

    /// <summary>入口红点统一尺寸：以「冒险日志」入口 30.8 为准
    /// （2026-11-09 主人「把其他地方的大小按冒险日志统一」）。
    /// 单一真源：RedDot.Bind 默认尺寸与 UIRedDot.size 都读它，调用处不再写死数字。</summary>
    public const float DefaultSize = 30.8f;

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
    /// <summary>2026-10-20 主人报「成就领完了按钮红点不灭」：成就页「领取」按钮专属红点键。
    /// 只跟成就可领走（AdventureLogAchievements.HasUnclaimed），与按钮置灰同口径。
    /// 2026-10-31 主人报「领取后标签提示还在」：Achievement 这个侧栏 Tab 键也收窄成只跟成就可领，
    /// 里程等级改由 <see cref="LogWorld"/>（世界页 Tab）承载；2026-11-13 碎片合成整体下线。</summary>
    public const string AchClaim = "log.ach.claim";
    /// <summary>2026-10-31：世界页 Tab 红点 —— 日志里程等级可领
    /// （2026-11-13 碎片合成整体下线，不再有「碎片可合成」这一项）。
    /// 与成就可领分开，避免成就领完后成就 Tab 红点被里程撑住不灭。</summary>
    public const string LogWorld = "log.world";
    public const string LogMonster = "log.monster";
    public const string LogMerc = "log.merc";
    public const string Guild = "nav.guild";
    public const string Adventure = "nav.adventure";
    /// <summary>2026-09-29：天赋入口红点 —— 右列有「新开通未点」节点时点亮
    ///（2026-11-05 拍板门槛=人物等级，升级跨门槛由 TalentSystem.NotifyLevelUp 刷，玩家点开对应节点才灭）。</summary>
    public const string Talent = "nav.talent";
    /// <summary>2026-10-16：每日登录入口红点 —— 有可领的登录奖励时亮起。</summary>
    public const string DailyLogin = "dailylogin";
    /// <summary>2026-10-31：看广告送佣兵活动入口红点（艾拉）—— 有可领里程碑时亮起；
    /// 各礼盒的红点键见 AdMercEventSystem.MilestoneRedDotKey(i)（admerc.m0..m3）。</summary>
    public const string AdMercEvent = "admerc";

    public static Sprite Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;
            _sprite = LoadSprite();
            return _sprite;
        }
    }

    static Sprite LoadSprite()
    {
        var sp = Resources.Load<Sprite>(ResourcesPath);
        if (sp != null) return sp;

        var tex = Resources.Load<Texture2D>(ResourcesPath);
        if (tex != null)
        {
            sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "红点";
            return sp;
        }

#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            sp = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (sp != null) return sp;
            var edTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
            if (edTex != null)
            {
                sp = Sprite.Create(edTex, new Rect(0f, 0f, edTex.width, edTex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = "红点";
                return sp;
            }
        }
#endif
        Debug.LogWarning("[RedDot] 未找到红点图: " + ArtPath);
        return null;
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
    public static UIRedDot Bind(Transform icon, string key, Vector2? offset = null, float size = DefaultSize)
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
        rt.anchoredPosition = offset ?? new Vector2(-12f, -7f);

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

    /// <summary>根据邮件、可领成就里程、图鉴未读等刷新常用红点</summary>
    public static void RefreshCommon()
    {
        Set(Mail, MailSystem.UnclaimedCount() > 0);
        bool achReward = AdventureLogAchievements.HasUnclaimed();
        // 2026-11-13：碎片合成整体下线，worldReward 只看里程等级可领。
        bool worldReward = AdventureLogMileage.HasUnclaimedLevel();
        // 2026-10-31：成就页 Tab 只跟「成就可领」走，里程等级挪到世界页 Tab
        // （主人报：成就领完了成就标签红点还亮着，其实是里程等级能领）。
        Set(Activity, achReward || worldReward);
        Set(Achievement, achReward);
        // 2026-11-12 主人拍板「世界页点开后红点就灭」：加「已看」闸门，打开即记 worldTabSeen，之后不再亮。
        bool worldSeen = SaveSystem.Instance?.Data?.worldTabSeen ?? false;
        Set(LogWorld, worldReward && !worldSeen);
        // 天赋入口红点：登录 / 回城就刷，不用等玩家打开天赋页。
        // 【2026-11-11 主人拍板「打开界面后红点就可以消失了」】只对门槛等级高于「已看等级」的新批次亮，
        // 判定唯一口径 = TalentDefs.EntryDotShouldLight（打开天赋页即记已看，见 TalentUI.Show）。
        var data = SaveSystem.Instance?.Data;
        Set(Talent, data != null && TalentDefs.EntryDotShouldLight(data.talents, data.talentNewSeenLevel));
        Set(DailyLogin, DailyLoginSystem.HasClaimable());
        // 2026-11-08：酒馆入口红点 = 有佣兵能升星（底部导航「酒馆」+ 酒馆页「佣兵队列」入口共用 Tavern 键）。
        // 判定/刷新收口在 MercStarRedDot，别在这里抄第二份 CanStarUp 遍历。
        MercStarRedDot.Refresh();
        // 2026-10-31：看广告送佣兵活动（艾拉）——入口键 + 4 个礼盒键一起刷（内部已判存档未就绪）。
        AdMercEventSystem.RefreshRedDots();
        AdventureCodex.RefreshRedDots();
    }
}
