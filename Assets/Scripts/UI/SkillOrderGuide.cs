using System.Collections;
using UnityEngine;

/// <summary>
/// 「拖动技能可以改释放顺序」的**软引导**（2026-10-05 主人拍板）。
///
/// <para>触发时机：**玩家本局技能数凑够 2 个的那一刻**（通常是抽中第二个技能）弹一次。</para>
///
/// <para>口径 = 软引导，不是强引导：只是一条会自己消失的气泡（<c>TutorialHintUI.Show</c>，
/// <c>hard:false</c> → 不挖空高亮、不挡点击、不等玩家操作、没有超时兜底，绝不打断战斗）。
/// 与旧的 <c>TutorialDirector.CoTutorialSkillDraft</c> P2 那一拍（硬遮罩 + 超时替玩家拖）完全不同，别混。</para>
///
/// <para>全局只弹一次（PlayerPrefs 记标记），而且**只有真的弹出来了才记**——
/// 万一那一刻 UI 还没建好，下次刷新会再试一次，不会白白吃掉这次教学。</para>
///
/// <para>指向的就是真正能拖的那排技槽：<see cref="BattleUI.skillSlotRoot"/>（<see cref="SkillOrderChip"/> 挂在这排上）。
/// 注意拖拽只在「整理阶段」（<see cref="BattleLootMode.Active"/>）打开，战斗中不可拖，气泡只是提前告知有这回事。</para>
///
/// 唯一调用点：<see cref="RunSkillBarUI.Refresh"/>（技能栏每次重建都会走到，
/// 所以「新技能到手」这件事只有一个入口需要关心，别的调用点不用各自记得调）。
/// </summary>
public static class SkillOrderGuide
{
    const string PrefsKey = "RiftGuide_SkillOrderHint_v1";

    /// <summary>
    /// 这条提示的**唯一文案来源**。引导局那一拍（抽中技能后）也读它，别在别处再抄一份。
    /// 【2026-10-07 主人要求】把原来开局就弹的「技能冷却好会自动释放，无需点击；顺序看左上角 ①②③④。」
    /// 并到这一条里 —— 那句话要在**抽中技能、能调顺序**的那一拍才出现，不是一进战斗就出来。
    /// </summary>
    public const string Text = "技能冷却好会自动放，不用手点。拖动可以改顺序，① 最先放——记不住就多看两眼。";

    const float ShowSeconds = 5f;

    /// <summary>已经教过了（换局也不再弹）。</summary>
    public static bool Shown => PlayerPrefs.GetInt(PrefsKey, 0) != 0;

    /// <summary>延迟等待协程是否在跑（防 Refresh 多次触发开多条协程）。</summary>
    static bool _delayRunning;

    /// <summary>调试用：清掉标记，下次凑够两个技能会再弹一次。</summary>
    public static void ResetForDebug()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 技能栏刷新时调一次。条件不满足就安静返回（不报错、不建 UI）。
    /// </summary>
    public static void NotifySkillsChanged()
    {
        if (Shown) return;
        if (!RunLoadout.IsActive) return;

        var ids = RunLoadout.SkillIds();
        if (ids == null || ids.Count < 2) return;   // 主人口径：凑够两个技能才教排序

        var hint = TutorialHintUI.Instance;
        if (hint == null) return;                   // UI 还没建好：这次不算，下次再试

        // 【2026-10-18 时机优化·主人问「软引导别和引导冲突」】引导局里三抽技能紧贴精英波对白，
        // TalkBlock 每句开播都会 Hide 提示——台词占屏时这一拍不弹、不记标记，
        // 开协程等台词收了再弹（最多等 20s），别让软引导刚露头就被吞。
        if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing)
        {
            if (!_delayRunning)
            {
                _delayRunning = true;
                hint.StartCoroutine(CoWaitTalkThenShow());
            }
            return;
        }

        DoShow();
    }

    /// <summary>等战斗台词播完再弹（协程宿主 = TutorialHintUI 实例）。</summary>
    static IEnumerator CoWaitTalkThenShow()
    {
        float t = 0f;
        while (t < 20f)
        {
            if (Shown) { _delayRunning = false; yield break; }        // 已经弹过了：放弃
            if (!GameSceneGate.IsBattle) { _delayRunning = false; yield break; }  // 离开战斗：不弹了
            var h = TutorialHintUI.Instance;
            if (h == null) { _delayRunning = false; yield break; }
            var talk = BattleHeadTalkUI.Instance;
            if (talk == null || !talk.IsShowing) break;               // 台词收了：可以弹
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _delayRunning = false;
        DoShow();
    }

    static void DoShow()
    {
        var hint = TutorialHintUI.Instance;
        if (hint == null) return;

        // 真正能拖的是技能槽那一排（BattleUI.skillSlotRoot），不是战力条
        RectTransform target = null;
        var bui = BattleUI.Instance;
        if (bui != null && bui.skillSlotRoot != null)
            target = bui.skillSlotRoot as RectTransform;

        hint.Show(Text, target, ShowSeconds);

        PlayerPrefs.SetInt(PrefsKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[SkillOrderGuide] 软引导：拖动技能改释放顺序（已弹出，标记只弹一次）");
    }
}
