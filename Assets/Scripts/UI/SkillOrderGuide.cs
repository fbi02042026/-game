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
    const string Text = "拖动技能可改释放顺序：① 最先放。";
    const float ShowSeconds = 5f;

    /// <summary>已经教过了（换局也不再弹）。</summary>
    public static bool Shown => PlayerPrefs.GetInt(PrefsKey, 0) != 0;

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
