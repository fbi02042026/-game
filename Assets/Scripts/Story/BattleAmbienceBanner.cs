using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗开场氛围横幅（2026-11-11 主人拍板「一进战斗就出现剧情，背景可以用半透遮罩」）：
/// 会播战前剧情的主线正式关，进战斗先叠一层半透遮罩 + 本章铺垫句——让玩家知道这关不一样。
/// 不暂停战斗、不需要点击、不挡操作：淡入 → 停留 → 淡出自动结束，播完才弹正式战前剧情窗。
/// 文案真源 = 本文件 Lines（叙事文案走 C# 常量）；触发条件随战前剧情（每章首节、看过不重播）。
/// 对象不 DDOL：生在战斗场景、退战随场景销毁，不存在残留挡屏。
/// 【2026-11-12】另提供 ShowPersistent / HidePersistent：Boss 关 boss 线剧情的常驻半透底。
/// </summary>
public class BattleAmbienceBanner : MonoBehaviour
{
    const float FadeInDur = 0.6f;
    const float HoldDur = 2.2f;
    const float FadeOutDur = 0.8f;
    const float DimAlpha = 0.45f;   // 半透：战场可见，又有「异样」压迫感
    /// <summary>【2026-11-12 主人拍板】战前剧情常驻底：直接叠在战斗界面上，
    /// 不给剧情配插画背景图（包体太大）。只影响 ShowPersistent，开场氛围横幅仍走 DimAlpha。
    /// 【2026-11-13 主人拍板「战前选择的背景底透明度改成 50%」】0.70 → 0.50；
    /// 战前选择只用这一层底（对话窗自带的 Dim 由调用方关掉），每次进浓度一致。</summary>
    const float PersistentDimAlpha = 0.50f;
    const int FontSize = 36;        // 与剧情对话正文同号（fusion-pixel 12 倍数）

    /// <summary>
    /// 每章一句进关铺垫（文案真源，只此处可改）。
    /// 【2026-11-11 主人拍板「可以搞笑吐槽」】调性按 10-16 拍板（有趣流行适合年轻人），
    /// 每句踩在本章战前剧情的意象/台词上吐槽：1 吐「练手好地方」、2 吐「地脉波动」、
    /// 6 吐「如果你读到这」没写完、7 吐艾丽娅不等、8 吐三枚铜板的账还没结。
    /// </summary>
    static readonly string[] Lines =
    {
        "公会管这叫『练手的好地方』。\n那斜着照不进来的光是什么，福利吗？",     // 1 暮影森林
        "对外说是『地脉波动』。\n哪家的地脉波动还带腐土味的？",                 // 2 幽冥墓园
        "路标是新的，藤蔓比路还老。\n前面那位哥们，你倒是留个姓名啊。",         // 3 翡翠秘境
        "风停了，草伏得整整齐齐。\n连风都开始打卡下班了，这关能正常吗。",       // 4 晨曦草原
        "潮水退得比平时远。\n礁石里那艘『最多三个月』的船，可真新啊。",         // 5 海岛遗迹
        "石壁上的字一层压一层，全是同一句。\n『如果你读到这』——读到什么，写完啊！", // 6 巨岩深窟
        "这里的热不是火，是裂缝本身在烧。\n她跑得倒快，连个『等』字都不留。",   // 7 赤焰炼狱
        "雪落下来就不化了。\n都到这步了，账还没结——稳住，最后一程。",           // 8 永霜雪境
    };

    CanvasGroup _group;

    static string LineOf(int chapter)
    {
        if (chapter < 1 || chapter > Lines.Length) return null;
        return Lines[chapter - 1];
    }

    /// <summary>
    /// 播本章氛围横幅，播完（淡出结束）才返回——调用方在其后弹战前剧情窗。
    /// 由 BattleManager 协程驱动；中途中退战 → 本对象随场景销毁 → 假 null 检测自动终止，不残留挡屏。
    /// </summary>
    public static IEnumerator PlayRoutine(int chapter)
    {
        string line = LineOf(chapter);
        if (string.IsNullOrEmpty(line)) yield break;

        var go = new GameObject("BattleAmbienceBanner");
        var banner = go.AddComponent<BattleAmbienceBanner>();
        banner.Build(line);
        yield return banner.RunFade();
        if (banner != null)
            Object.Destroy(banner.gameObject);
    }

    void Build(string line)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleAmbience, UICanvasSetup.ResolveUiCamera());

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;   // 不挡操作：玩家此时可自由走位
        _group.interactable = false;

        AddDim(DimAlpha);

        var labelGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(transform, false);
        var label = labelGo.GetComponent<Text>();
        label.text = line;
        label.fontSize = FontSize;
        label.color = new Color(1f, 1f, 1f, 0.92f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(640f, 200f);
        rt.anchoredPosition = new Vector2(0f, 80f);   // 正中略偏上，避开顶部血条与底部技能栏

        GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>全屏黑色半透底（alpha 由调用方给：横幅 0.45 / 战前剧情常驻底 0.50），不挡点击。</summary>
    void AddDim(float alpha)
    {
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        dim.transform.SetParent(transform, false);
        dim.color = new Color(0f, 0f, 0f, alpha);
        dim.raycastTarget = false;
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 【2026-11-12 主人报「昨天加的战前剧情没有黑色半透底」】Boss 关 boss 线剧情专用：
    /// 铺一层**常驻** 50% 半透黑底（不淡出、不带字），让没挂背景图的剧情窗压在暗底上。
    /// 排序取 StoryDialogue-1(12999)：Default 层最高，压住战斗 HUD(950)/抽奖(965)/Toast(11000)；
    /// 剧情窗在 Effects 层(13000)天然在上；blocksRaycasts=false，不影响对话按钮。
    /// 调用方在选完 / 异常 / 超时后必须调 HidePersistent 撤掉；中途退战随场景销毁，不残留。
    /// </summary>
    public static GameObject ShowPersistent()
    {
        var go = new GameObject("BattleAmbienceDim");
        var holder = go.AddComponent<BattleAmbienceBanner>();
        holder.BuildDimOnly();
        return go;
    }

    public static void HidePersistent(GameObject go)
    {
        if (go != null) Object.Destroy(go);
    }

    void BuildDimOnly()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue - 1, UICanvasSetup.ResolveUiCamera());
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        AddDim(PersistentDimAlpha);
    }

    IEnumerator RunFade()
    {
        float t = 0f;
        while (t < FadeInDur)
        {
            if (_group == null) yield break;   // 中途退战：对象随场景销毁，停止即可
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / FadeInDur);
            yield return null;
        }
        if (_group == null) yield break;
        _group.alpha = 1f;

        float hold = 0f;
        while (hold < HoldDur)
        {
            if (_group == null) yield break;
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        float outT = 0f;
        while (outT < FadeOutDur)
        {
            if (_group == null) yield break;
            outT += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(outT / FadeOutDur);
            yield return null;
        }
    }
}
