using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务 → 功能界面 跳转路由（2026-09-19 新增）。
///
/// 数据目标：在线时长 ↑ / 流失 ↓。
///   玩家看到「当前目标」后最贵的成本是「找入口在哪」，这里把这一步压成一次点击。
///
/// 设计约定：
///   · 只做「切页 + 打开对应子界面」，不复制任何页面逻辑。
///     切页一律复用现成的 TownHubController.OpenAdventure / OpenTavern / OpenCharacter
///     （内部就是 MainBottomNav.SetSelected + SwitchTab），不另造一套切页。
///   · 跳转失败一律返回 false，由调用方回退（展开任务面板 + 一行提示），绝不让点击没反应。
/// </summary>
public static class QuestJumpRouter
{
    /// <summary>主线任务 → 对应功能界面。成功 true，目标界面拿不到 false。</summary>
    public static bool GoMain(MainQuestDef def)
    {
        switch (def.type)
        {
            case MainQuestType.ClearStage:
                // 打关：切到冒险页并定位到任务所在章节
                return GoAdventure(def.chapter);

            case MainQuestType.TalkNpc:
                // 找老板娘：人就在酒馆，切过去再开口
                if (!GoTavern()) return false;
                Play(MainQuestDialogues.NpcTalk(def.param, def.chapter),
                    () => MainQuestSystem.NotifyTalk(def.param));
                return true;

            case MainQuestType.WatchStory:
                // 看剧情：就地播，不切页——把玩家踢出当前页反而打断节奏
                return Play(MainQuestDialogues.Story(def.param),
                    () => MainQuestSystem.NotifyStory(def.param));

            case MainQuestType.RecruitMerc:
                // 雇人：酒馆里的「佣兵名册」就是雇佣入口
                if (!GoTavern()) return false;
                TavernUnlockUI.Show();
                return true;

            case MainQuestType.UpgradeTalent:
                // 点天赋：角色页 → 天赋界面
                return GoTalent();
        }
        return false;
    }

    /// <summary>每日任务 → 对应功能界面。</summary>
    public static bool GoDaily(DailyQuestView d)
    {
        if (d.id == "daily_login")
        {
            DailyLoginUI.Show();
            return true;
        }
        // 其余每日（通关 N 关等）都是打关，送进冒险页
        return GoAdventure(MainQuestSystem.CurrentChapter());
    }

    /// <summary>章节级牵引（本章还没配主线任务时的兜底目标）：切到冒险页定位当前章。</summary>
    public static bool GoChapter(int chapter)
    {
        return GoAdventure(chapter > 0 ? chapter : MainQuestSystem.CurrentChapter());
    }

    // ============================================================
    // 具体落点
    // ============================================================

    /// <summary>冒险页 + 定位章节。</summary>
    static bool GoAdventure(int chapter)
    {
        var hub = TownHubController.Instance;
        if (hub == null) return false;

        hub.OpenAdventure();

        // 页面已在 OpenAdventure 里预加载好，这里只把选中章拨过去
        var adv = AdventureUI.Instance;
        if (adv != null)
            adv.FocusChapter(chapter > 0 ? chapter : MainQuestSystem.CurrentChapter());
        return true;
    }

    /// <summary>酒馆页。被老板娘禁入期间进不去，返回 false 交给调用方回退。</summary>
    static bool GoTavern()
    {
        if (TavernLandladyTease.IsBanned) return false;

        var hub = TownHubController.Instance;
        if (hub == null) return false;

        hub.OpenTavern();
        return true;
    }

    /// <summary>角色页 + 天赋界面。</summary>
    static bool GoTalent()
    {
        var hub = TownHubController.Instance;
        if (hub == null) return false;

        hub.OpenCharacter();

        var character = CharacterUI.Instance;
        if (character == null) return false;

        character.OpenTalent();
        return true;
    }

    /// <summary>
    /// 播一段对话/剧情，播完标记完成。复用 StoryDirector（与开章/战后剧情同一套表现）。
    /// StoryDirector 或文案缺失时返回 false，让调用方回退，避免「点了没反应」。
    /// </summary>
    static bool Play(List<StoryBeat> beats, Action onDone)
    {
        var dir = StoryDirector.Ensure();
        if (dir == null || beats == null || beats.Count == 0) return false;

        dir.Play(beats, () =>
        {
            MainQuestSystem.Tick();
            onDone?.Invoke();
        });
        return true;
    }

    // ============================================================
    // 首次强制软引导（FirstRunGuide）专用出口
    // 目的：引导只在「目标控件按名字找不到」时用它兜一层跳转，
    //       日常仍走正常点击链路，避免把导航逻辑复制成两套。
    // ============================================================

    /// <summary>引导用：切到酒馆页。禁入期同样进不去，返回 false。</summary>
    public static bool GuideOpenTavern() => GoTavern();

    /// <summary>引导用：切到角色页并打开天赋界面。</summary>
    public static bool GuideOpenTalent() => GoTalent();

    /// <summary>引导用：切到冒险页并定位章节。</summary>
    public static bool GuideOpenAdventure(int chapter) => GoAdventure(chapter);
}
