using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志：怪物/Boss/装备/佣兵/剧情/成就/探索统计。
/// 走底栏「冒险日志」入口，符合 IA。
/// </summary>
public class AdventureLogUI : MonoBehaviour, ITownPage
{
    public static AdventureLogUI Instance { get; private set; }

    public MainNavTab Tab => MainNavTab.Log;

    enum LogTab
    {
        Explore = 0,
        Monster = 1,
        Boss = 2,
        Equip = 3,
        Merc = 4,
        Story = 5,
        Achievement = 6,
        World = 7
    }

    static readonly string[] TabNames =
    {
        "探索", "怪物", "Boss", "装备", "佣兵", "剧情", "成就", "世界"
    };

    GameObject _root;
    Text _title;
    Text _body;
    readonly List<Button> _tabBtns = new List<Button>();
    readonly List<Image> _tabBgs = new List<Image>();
    LogTab _tab = LogTab.Explore;
    bool _preloaded;
    ScrollRect _scroll;

    public void PreloadOnce()
    {
        if (_preloaded) return;
        BuildIfNeeded();
        HidePage();
        _preloaded = true;
    }

    public void ShowPage()
    {
        BuildIfNeeded();
        TownSaveAlign.AlignAll();
        if (_root != null) _root.SetActive(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SelectTab(_tab, force: true);
        GameFonts.ApplyToHierarchy(transform);
    }

    public void HidePage()
    {
        if (_root != null) _root.SetActive(false);
        gameObject.SetActive(false);
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        UICanvasSetup.Apply(canvas);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 40;

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateUi("Dim", _root.transform, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.1f, 0.92f);

        var panel = CreateUi("Panel", _root.transform, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.04f, 0.12f);
        prt.anchorMax = new Vector2(0.96f, 0.9f);
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.16f, 0.98f);

        _title = CreateText(panel.transform, "Title", "冒险日志", 32, TextAnchor.MiddleLeft);
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0.04f, 0.9f);
        trt.anchorMax = new Vector2(0.7f, 0.98f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var hint = CreateText(panel.transform, "Hint", "数据来自存档与配置", 16, TextAnchor.MiddleRight);
        hint.color = new Color(0.7f, 0.65f, 0.55f);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0.55f, 0.9f);
        hrt.anchorMax = new Vector2(0.96f, 0.98f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        var tabRow = CreateUi("Tabs", panel.transform);
        var tabRt = tabRow.GetComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0.03f, 0.78f);
        tabRt.anchorMax = new Vector2(0.97f, 0.88f);
        tabRt.offsetMin = tabRt.offsetMax = Vector2.zero;

        float w = 1f / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            var btnGo = CreateUi("Tab" + i, tabRow.transform, typeof(Image), typeof(Button));
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(i * w, 0f);
            brt.anchorMax = new Vector2((i + 1) * w, 1f);
            brt.offsetMin = new Vector2(2f, 2f);
            brt.offsetMax = new Vector2(-2f, -2f);
            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.28f, 1f);
            _tabBgs.Add(img);
            CreateText(btnGo.transform, TabNames[i], 15, TextAnchor.MiddleCenter);
            int idx = i;
            btnGo.GetComponent<Button>().onClick.AddListener(() => SelectTab((LogTab)idx));
            _tabBtns.Add(btnGo.GetComponent<Button>());
        }

        var scrollGo = CreateUi("Scroll", panel.transform, typeof(Image), typeof(ScrollRect));
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.04f, 0.05f);
        srt.anchorMax = new Vector2(0.96f, 0.76f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.09f, 0.12f, 1f);
        _scroll = scrollGo.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateUi("Viewport", scrollGo.transform, typeof(RectMask2D));
        Stretch(viewport.GetComponent<RectTransform>());
        _scroll.viewport = viewport.GetComponent<RectTransform>();

        var content = CreateUi("Content", viewport.transform);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 800f);
        _scroll.content = crt;

        _body = CreateText(content.transform, "", 20, TextAnchor.UpperLeft);
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;
        _body.verticalOverflow = VerticalWrapMode.Overflow;
        var brtBody = _body.rectTransform;
        brtBody.anchorMin = new Vector2(0f, 1f);
        brtBody.anchorMax = new Vector2(1f, 1f);
        brtBody.pivot = new Vector2(0.5f, 1f);
        brtBody.anchoredPosition = new Vector2(0f, -8f);
        brtBody.sizeDelta = new Vector2(-24f, 780f);

        var claim = CreateUi("ClaimAch", panel.transform, typeof(Image), typeof(Button));
        var claimRt = claim.GetComponent<RectTransform>();
        claimRt.anchorMin = claimRt.anchorMax = new Vector2(0.5f, 0.02f);
        claimRt.pivot = new Vector2(0.5f, 0f);
        claimRt.anchoredPosition = new Vector2(0f, 8f);
        claimRt.sizeDelta = new Vector2(220f, 40f);
        claim.GetComponent<Image>().color = new Color(0.28f, 0.5f, 0.35f, 1f);
        CreateText(claim.transform, "领取成就里程", 20, TextAnchor.MiddleCenter);
        claim.GetComponent<Button>().onClick.AddListener(() =>
        {
            AchievementMilestoneUI.Show();
            RefreshBody();
        });
    }

    void SelectTab(LogTab tab, bool force = false)
    {
        if (!force && _tab == tab)
        {
            RefreshBody();
            return;
        }
        _tab = tab;
        for (int i = 0; i < _tabBgs.Count; i++)
        {
            _tabBgs[i].color = i == (int)_tab
                ? new Color(0.35f, 0.45f, 0.32f, 1f)
                : new Color(0.25f, 0.22f, 0.28f, 1f);
        }
        RefreshBody();
    }

    void RefreshBody()
    {
        if (_body == null) return;
        string text;
        switch (_tab)
        {
            case LogTab.Explore: text = BuildExplore(); break;
            case LogTab.Monster: text = BuildMonsters(false); break;
            case LogTab.Boss: text = BuildMonsters(true); break;
            case LogTab.Equip: text = BuildEquip(); break;
            case LogTab.Merc: text = BuildMerc(); break;
            case LogTab.Story: text = BuildStory(); break;
            case LogTab.Achievement: text = BuildAchievement(); break;
            default: text = BuildWorld(); break;
        }
        _body.text = text;
        float h = Mathf.Max(400f, 28f + text.Length * 0.55f);
        var brt = _body.rectTransform;
        brt.sizeDelta = new Vector2(brt.sizeDelta.x, h);
        if (_scroll != null && _scroll.content != null)
            _scroll.content.sizeDelta = new Vector2(0f, h + 24f);
        if (_scroll != null)
            _scroll.verticalNormalizedPosition = 1f;
    }

    static string BuildExplore()
    {
        var data = SaveSystem.Instance?.Data;
        var sb = new StringBuilder();
        int maxCh = data != null ? Mathf.Max(1, data.maxUnlockedChapter) : 1;
        sb.AppendLine("【探索统计】");
        sb.AppendLine($"最高解锁章节：第 {maxCh} 章");
        sb.AppendLine($"公会等级：{(data != null ? data.guildLevel : 1)}");
        sb.AppendLine($"遗产池：{(data?.legacyEquipPool != null ? data.legacyEquipPool.Count : 0)} 件");
        sb.AppendLine($"成就点数：{(data != null ? data.totalAchievementPoints : 0)}");
        sb.AppendLine($"永久佣兵：{(data?.permanentMercs != null ? data.permanentMercs.Count : 0)} 名");
        sb.AppendLine();
        sb.AppendLine("章节通关次数：");
        if (data?.chapterClearCounts != null && data.chapterClearCounts.Count > 0)
        {
            for (int i = 0; i < data.chapterClearCounts.Count; i++)
            {
                var e = data.chapterClearCounts[i];
                if (e == null) continue;
                sb.AppendLine($"  · 第{e.chapter}章 ×{e.clearCount}");
            }
        }
        else
            sb.AppendLine("  （尚无通关记录）");
        return sb.ToString();
    }

    static string BuildMonsters(bool bossOnly)
    {
        var sb = new StringBuilder();
        sb.AppendLine(bossOnly ? "【Boss 图鉴】" : "【怪物图鉴】");
        sb.AppendLine("按章节列出常见敌人（配置名）。");
        sb.AppendLine();
        var cfg = ConfigManager.Instance;
        int shown = 0;
        if (cfg != null)
        {
            for (int ch = 1; ch <= 8 && shown < 40; ch++)
            {
                var list = cfg.GetChapterPreviewMonsters(ch);
                if (list == null || list.Count == 0) continue;
                bool any = false;
                for (int i = 0; i < list.Count && shown < 40; i++)
                {
                    var m = list[i];
                    if (m == null) continue;
                    if (bossOnly != m.isBoss) continue;
                    if (!any)
                    {
                        sb.AppendLine($"— 第{ch}章 —");
                        any = true;
                    }
                    string name = !string.IsNullOrEmpty(m.monsterName) ? m.monsterName : m.id;
                    sb.AppendLine($"  · {name}  HP{m.baseHp:0} ATK{m.baseAttack:0}");
                    shown++;
                }
            }
        }
        if (shown == 0)
            sb.AppendLine("暂无怪物配置可读，通关后会在此归档。");
        return sb.ToString();
    }

    static string BuildEquip()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【装备 / 传说】");
        var data = SaveSystem.Instance?.Data;
        int legend = data?.unlockedLegendaryWeapons != null ? data.unlockedLegendaryWeapons.Count : 0;
        sb.AppendLine($"已解锁传说武器：{legend}");
        if (data?.unlockedLegendaryWeapons != null)
        {
            foreach (var id in data.unlockedLegendaryWeapons)
                sb.AppendLine("  · " + id);
        }
        sb.AppendLine();
        sb.AppendLine("遗产池预览：");
        var pool = data?.legacyEquipPool;
        if (pool == null || pool.Count == 0)
            sb.AppendLine("  （空）");
        else
        {
            int n = Mathf.Min(20, pool.Count);
            for (int i = 0; i < n; i++)
            {
                var e = pool[i];
                if (e == null) continue;
                sb.AppendLine($"  · {e.equipId} 品质{e.rarity} ★{e.star}");
            }
            if (pool.Count > n) sb.AppendLine($"  …共 {pool.Count} 件");
        }
        return sb.ToString();
    }

    static string BuildMerc()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【佣兵档案】");
        var list = SaveSystem.Instance?.Data?.permanentMercs;
        if (list == null || list.Count == 0)
        {
            sb.AppendLine("尚未招募永久佣兵。");
            return sb.ToString();
        }
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            sb.AppendLine($"  · {(string.IsNullOrEmpty(m.displayName) ? m.mercId : m.displayName)}（{m.mercId}） Lv{m.level} ★{Mathf.Max(1, m.star)} 技能:{m.skillId}");
        }
        int deploy = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetActiveMercIds().Count : 0;
        sb.AppendLine();
        sb.AppendLine($"当前可出战：{deploy} 名（受酒馆等级限制）");
        return sb.ToString();
    }

    static string BuildStory()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【剧情进度】");
        sb.AppendLine($"开场演出：{(StoryProgress.OpeningIntroPlayed ? "已看" : "未看")}");
        sb.AppendLine($"引导介绍：{(StoryProgress.TutorialIntroDone ? "完成" : "进行中")}");
        sb.AppendLine($"引导战斗：{(StoryProgress.TutorialBattleCleared ? "完成" : "未完成")}");
        sb.AppendLine($"新手引导：{(StoryProgress.TutorialDone ? "全部完成" : "未完成")}");
        sb.AppendLine($"第1章厅内剧情：{(StoryProgress.Chapter1IntroDone ? "完成" : "未完成")}");
        sb.AppendLine($"第1章选择：{(StoryProgress.Chapter1ChoiceDone ? "完成" : "未完成")}");
        string c1 = StoryProgress.GetChoice(1);
        if (!string.IsNullOrEmpty(c1))
            sb.AppendLine($"第1章抉择：{c1}");
        return sb.ToString();
    }

    static string BuildAchievement()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【成就】");
        var data = SaveSystem.Instance?.Data;
        sb.AppendLine($"成就点数：{(data != null ? data.totalAchievementPoints : 0)}");
        int done = data?.completedAchievements != null ? data.completedAchievements.Count : 0;
        sb.AppendLine($"已完成成就：{done}");
        if (data?.completedAchievements != null)
        {
            int n = 0;
            foreach (var id in data.completedAchievements)
            {
                sb.AppendLine("  · " + id);
                if (++n >= 25) { sb.AppendLine("  …"); break; }
            }
        }
        sb.AppendLine();
        sb.AppendLine("里程碑：点下方「领取成就里程」打开领取界面。");
        return sb.ToString();
    }

    static string BuildWorld()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【世界设定 · 摘要】");
        sb.AppendLine("像素冒险:裂缝之刃 — 冒险者从公会大厅出发，进入裂缝章节战斗。");
        sb.AppendLine("局内可强化、附魔、休息；撤离或死亡可带回遗产装备。");
        sb.AppendLine("城镇侧养成：酒馆佣兵、角色装备、天赋与成就里程。");
        sb.AppendLine();
        sb.AppendLine("章节印象：");
        for (int i = 1; i <= 8; i++)
            sb.AppendLine($"  {i}. {GameConfig.GetChapterTitleText(i)}");
        return sb.ToString();
    }

    static GameObject CreateUi(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        for (int i = 0; i < comps.Length; i++)
            go.AddComponent(comps[i]);
        return go;
    }

    static Text CreateText(Transform parent, string content, int size, TextAnchor align)
    {
        var go = CreateUi("T", parent, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = CreateUi(name, parent, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
