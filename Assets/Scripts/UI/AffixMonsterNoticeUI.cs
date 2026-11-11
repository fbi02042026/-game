using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 【2026-11-03 主人拍板】表定词条怪（monster_stats.affix 列）首次登场提示：
/// - 该怪在正式主线关第一次出现时，屏幕左缘出现它的头像 + 大号黄色感叹号；
/// - 点击后弹出与冒险日志图鉴<b>同一个</b>怪物信息弹窗（共用 MonsterTipPanel，主人拍板「用的是一个就行」；
///   2026-11-11 主人拍板「怪物信息用通用窗体」→ 弹窗皮已换 Common/通用窗体1 九宫格），
///   正文为该怪的词条介绍；点弹窗任意处关闭 = 已看过 → 写存档标记，之后永久消失；
/// - 不强制观看：不点也照常打，没看过的怪下次进战斗还会再提示；
/// - 同一场战斗（场景）同一种怪只提示一次，场景重载后自动重置。
/// Canvas 走统一出口 UICanvasSetup.ApplyPopup（铁律 4.6：Screen Space - Camera + 自动适配）；
/// 怪物头像复用 AdventureUI.LoadMonsterSprite（唯一出口，不另抄路径表）。
/// </summary>
public static class AffixMonsterNoticeUI
{
    class Entry
    {
        public MonsterConfig Cfg;
        public GameObject Slot;
    }

    static Canvas _canvas;
    static RectTransform _stack;
    static readonly List<Entry> _entries = new List<Entry>();
    static readonly HashSet<string> _shownThisScene = new HashSet<string>();

    /// <summary>战斗 UI 之上的高位排序（弹窗类按项目惯例取 400 段）。</summary>
    const int SortOrder = 420;
    const float IconSize = 88f;
    const float IconGap = 14f;
    /// <summary>
    /// 【2026-11-11 主人报「手机上图标偏下、和下面的面板重叠」】图标栈顶距屏幕顶的距离（逻辑像素）。
    /// 根因：原写法锚画布中心 (0,0.5)+y120 —— 逻辑宽恒 720、瘦屏逻辑高 = 720÷屏比（≤1600，铁律 13），
    /// 画布越高中心越靠下，图标栈跟着往下漂（1600 高屏比 1280 设计屏低 160px）；
    /// 再加上 4 种词条怪叠满 4 格（4×88+3×14 = 394），底端直接压进底部战斗背包面板（锚底、高 600）顶边。
    /// 改为锚顶 + 固定距顶（与 2026-10-22 BossBar 改贴顶锚同一口径：贴顶锚不随画布中心漂）。
    /// 取值依据：BossBar 锚顶 y=-173、美术半高约 60 → 顶栏区底约 233（再叠加刘海 inset，约 293），
    /// 取 340 既躲开血条又不贴刘海；手机 1600 逻辑高上 4 格栈底 = 734，仍远在背包面板顶 1000 之上。
    /// 要整体上下微调只动这一个数（每 −1 上移 1，每 +1 下移 1）。
    /// </summary>
    const float NoticeTopOffset = 340f;
    static readonly Color SlotBg = new Color(0.10f, 0.08f, 0.06f, 0.95f);
    static readonly Color YellowBang = new Color(1f, 0.84f, 0.10f);

    /// <summary>
    /// Monster.Init 在表定词缀挂接成功后调用（仅正式主线关、非精英/Boss 普通怪）。
    /// 已看过（存档）或同场景提示过的怪直接静默返回。
    /// </summary>
    public static void Notify(MonsterConfig cfg, List<MonsterAffixId> affixes)
    {
        if (cfg == null || affixes == null || affixes.Count == 0) return;
        if (string.IsNullOrEmpty(cfg.id)) return;

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || data.HasSeenAffixMonster(cfg.id)) return;   // 看过一次 → 永久消失
        if (!_shownThisScene.Add(cfg.id)) return;                        // 同场景同种怪只提示一次

        EnsureCanvas();
        if (_stack == null) return;
        BuildSlot(cfg);
    }

    // ---------------------------------------------------------------- canvas

    static void EnsureCanvas()
    {
        if (_canvas != null) return;

        // 场景切换后旧 Canvas 已随场景销毁（Unity 伪空），把残留引用清干净再重建
        _entries.Clear();
        _shownThisScene.Clear();

        // 战斗场景若无 EventSystem（理论上战斗 UI 已带），补一个保证点击可用
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        var root = new GameObject("AffixMonsterNoticeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = root.GetComponent<Canvas>();
        // 统一出口：Camera 模式 + 排序 + 根铺满（AGENTS.md 4.6）
        UICanvasSetup.ApplyPopup(_canvas, SortOrder);

        // 左缘竖排图标栈：锚【左上】+ 固定距顶（不随画布中心漂，见 NoticeTopOffset 注释），手动向下排布
        var stackGo = new GameObject("NoticeStack", typeof(RectTransform));
        stackGo.transform.SetParent(_canvas.transform, false);
        _stack = (RectTransform)stackGo.transform;
        _stack.anchorMin = new Vector2(0f, 1f);
        _stack.anchorMax = new Vector2(0f, 1f);
        _stack.pivot = new Vector2(0f, 1f);
        _stack.sizeDelta = new Vector2(IconSize, 0f);
        _stack.anchoredPosition = new Vector2(10f, -NoticeTopOffset);
    }

    static void BuildSlot(MonsterConfig cfg)
    {
        int index = _entries.Count;
        var slot = new GameObject("AffixNotice_" + cfg.id,
            typeof(RectTransform), typeof(Image), typeof(Button));
        slot.transform.SetParent(_stack, false);
        var slotRt = (RectTransform)slot.transform;
        // 手动定位：第 index 个槽位向下排，等宽等距，不依赖 LayoutGroup
        slotRt.anchorMin = slotRt.anchorMax = new Vector2(0f, 1f);
        slotRt.pivot = new Vector2(0f, 1f);
        slotRt.anchoredPosition = new Vector2(0f, -index * (IconSize + IconGap));
        slotRt.sizeDelta = new Vector2(IconSize, IconSize);
        slot.GetComponent<Image>().color = SlotBg;

        // 怪物头像（居中留 6px 边距）
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(slotRt, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(IconSize - 12f, IconSize - 12f);
        var icon = iconGo.GetComponent<Image>();
        var sp = AdventureUI.LoadMonsterSprite(cfg);
        if (sp != null)
        {
            icon.sprite = sp;
            icon.preserveAspect = true;
            icon.color = Color.white;
        }

        // 大号黄色感叹号（右上角，黑描边保证浅色怪图上也看得清）
        var bangGo = new GameObject("Bang", typeof(RectTransform), typeof(Text));
        bangGo.transform.SetParent(slotRt, false);
        var bangRt = (RectTransform)bangGo.transform;
        bangRt.anchorMin = bangRt.anchorMax = bangRt.pivot = new Vector2(1f, 1f);
        bangRt.anchoredPosition = new Vector2(-4f, -4f);
        bangRt.sizeDelta = new Vector2(44f, 52f);
        var bang = bangGo.GetComponent<Text>();
        bang.text = "!";
        bang.fontSize = 46;
        bang.fontStyle = FontStyle.Bold;
        bang.alignment = TextAnchor.MiddleCenter;
        bang.color = YellowBang;
        bang.font = GameFonts.GetChinese();
        bang.raycastTarget = false;
        var outline = bangGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        var captured = cfg;
        slot.GetComponent<Button>().onClick.AddListener(() => ShowPopup(captured));

        _entries.Add(new Entry { Cfg = cfg, Slot = slot });
        GameFonts.ApplyToHierarchy(slot.transform);
    }

    // ---------------------------------------------------------------- popup

    /// <summary>
    /// 弹出怪物信息。弹窗即图鉴同一个 MonsterTipPanel（挂本 Canvas 下）；
    /// 内容走 MonsterTipContent 单一出口（战力速览 / 词条 / 弱点 三段，与冒险日志 Tip 完全一致），
    /// 点弹窗关闭 = 已看过 → 存档标记永久消失 + 移除左缘图标。
    /// </summary>
    static void ShowPopup(MonsterConfig cfg)
    {
        if (_canvas == null || cfg == null) return;
        int chapter = BattleManager.Instance != null ? BattleManager.Instance.CurrentChapter : 1;
        MonsterTipPanel.Show(_canvas.transform,
            MonsterTipContent.Title(cfg),
            MonsterTipContent.BuildBody(cfg, chapter),
            () => OnViewed(cfg));
    }

    static void OnViewed(MonsterConfig cfg)
    {
        if (cfg == null || string.IsNullOrEmpty(cfg.id)) return;

        // 存档标记：看过一次永久消失（镜像由 SyncListsFromRuntime 落盘，Save 即写）
        var save = SaveSystem.Instance;
        var data = save != null ? save.Data : null;
        if (data != null)
        {
            data.MarkAffixMonsterSeen(cfg.id);
            save.Save();
        }
        RemoveEntry(cfg.id);
    }

    static void RemoveEntry(string monsterId)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e == null || e.Cfg == null || e.Cfg.id != monsterId) continue;
            if (e.Slot != null) Object.Destroy(e.Slot);
            _entries.RemoveAt(i);
        }
    }
}
