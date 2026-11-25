using System.Collections;
using UnityEngine;

/// <summary>
/// 登录期「天赋入口」软引导（2026-11-25 主人拍板）：
/// <para>只在累计登录前 3 天、且当前有「可解锁/可升级且货币够」的天赋时，每天登录各弹一次；
/// 气泡只指向主界面底栏「角色」入口（<see cref="MainBottomNav.characterButton"/>），
/// 玩家进了角色页之后<b>不再弹任何气泡</b>，那里靠既有红点
/// （<see cref="TalentSystem.RefreshTalentEntryRedDot"/>）指路。</para>
/// <para>与新手引导关（FirstRunGuide）彻底分离：引导关不动、引导期不弹、刚出师当次也不弹。
/// 与闲置软引导（TownIdleNudge）也无关，那条保持原样。</para>
/// <para>软引导口径：不挖空、不挡点击、3 秒自收、点一下立刻收；今天弹过就不再弹
/// （PlayerPrefs 按自然日去重，纯 UI 提示不入云存档）。</para>
/// 唯一调用点：<see cref="TownSceneBootstrap.TryDailyLoginOnce"/>。
/// </summary>
public static class TalentEntryLoginHint
{
    /// <summary>本条软引导文案的唯一来源。</summary>
    public const string Text = "有天赋可以点了！点下面「角色」进去点亮它～";

    const string PrefsPrefix = "TalentEntryLoginHint_";
    const float HoldSeconds = 3f;
    const float MaxWaitSeconds = 120f;
    const float PollInterval = 0.25f;

    /// <summary>今天是否已经弹过（按自然日）。</summary>
    public static bool ShownToday => PlayerPrefs.GetInt(PrefsPrefix + DayKeyUtil.TodayKey(), 0) != 0;

    /// <summary>调试用：清掉今天的标记，下次登录可再弹。</summary>
    public static void ResetForDebug()
    {
        PlayerPrefs.DeleteKey(PrefsPrefix + DayKeyUtil.TodayKey());
        PlayerPrefs.Save();
    }

    /// <summary>登录检查末尾调一次；条件不满足就安静返回，不建 UI、不写标记。</summary>
    public static void TryShow()
    {
        if (ShownToday) return;
        if (!GameSceneGate.IsTown) return;
        if (!StoryProgress.TutorialDone) return;     // 引导关不插手（调用方也挡了一层）

        var guide = FirstRunGuide.Instance;
        if (guide != null && (!guide.IsFinished || guide.IsOverlayVisible)) return;
        if (FirstRunGuide.FinishedThisSession) return;  // 出师当次让位收尾链，第二天起才弹

        int day = DailyLoginSystem.LoginDays;
        if (day < 1 || day > 3) return;               // 前 3 天（累计登录第 1~3 天）

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || !TalentSystem.HasAnyAffordableUnlockable(data)) return;

        var hint = TutorialHintUI.Instance;
        if (hint != null && hint.IsVisible) return;   // 别的提示在屏：让位，今天不再补

        // 登录弹窗这一刻还没开（RequestShowOnMainUi 是异步等主界面），开协程等它关掉再弹，
        // 免得气泡被 TownPopup(900) 盖住 = 等于没弹。宿主挂 TutorialHintUI（DDOL 常驻）。
        hint = TutorialHintUI.Ensure();
        if (hint == null) return;
        hint.StartCoroutine(CoWaitThenShow(hint));
        Debug.Log("[TalentEntryLoginHint] 已排队：等登录弹窗/剧情让位后弹天赋入口引导（前3天，每天一次）");
    }

    /// <summary>等屏幕空出来再真弹；最多等 <see cref="MaxWaitSeconds"/> 秒，超时放弃（不写标记，下次登录再试）。</summary>
    static IEnumerator CoWaitThenShow(TutorialHintUI hint)
    {
        float waited = 0f;
        while (waited < MaxWaitSeconds)
        {
            if (ShownToday) yield break;                        // 别处已经弹过了
            if (!GameSceneGate.IsTown) yield break;             // 玩家进战斗了：不追着弹

            var g = FirstRunGuide.Instance;
            if (g != null && (!g.IsFinished || g.IsOverlayVisible)) yield break;
            if (Busy()) { waited += PollInterval; yield return new WaitForSecondsRealtime(PollInterval); continue; }

            DoShow(hint);
            yield break;
        }
    }

    /// <summary>屏上是否还有会盖住气泡 / 该让位的东西（口径对齐 TownIdleNudge.CanRun）。</summary>
    static bool Busy()
    {
        if (SceneLoadingCoordinator.IsActive) return true;
        if (DailyLoginUI.PopupVisible) return true;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return true;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible) return true;
        if (TalentUI.Instance != null && TalentUI.Instance.IsOpen) return true;
        var h = TutorialHintUI.Instance;
        if (h != null && h.IsVisible) return true;

        // 任何 TownPopup(900) 及以上层级的弹窗都让位（奖励/升级/设置/邮件…）；自己的气泡层不算。
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            if (c.sortingOrder < GameConfig.UiSort.TownPopup) continue;
            if (c.GetComponentInParent<TutorialHintUI>() != null) continue;
            return true;
        }
        return false;
    }

    /// <summary>真弹：指向底栏「角色」；只有确实弹出来了才写今天的标记。</summary>
    static void DoShow(TutorialHintUI hint)
    {
        var nav = MainBottomNav.Instance;
        if (nav == null || nav.characterButton == null) return;
        var target = nav.characterButton.transform as RectTransform;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        hint.Show(Text, target, HoldSeconds);
        if (!hint.IsVisible || !string.Equals(hint.CurrentHint, Text)) return;  // 被占屏挡掉：不标记

        hint.SetTapToHideOnce(true);   // 点一下立刻收（走 TutorialHintUI 既有一次性出口）
        PlayerPrefs.SetInt(PrefsPrefix + DayKeyUtil.TodayKey(), 1);
        PlayerPrefs.Save();
        Debug.Log("[TalentEntryLoginHint] 软引导已弹：底栏「角色」入口（3秒/点击收起，今天不再弹）");
    }
}
