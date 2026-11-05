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
                // 打关：切到冒险页并定位到任务所在章节（按任务自身难度落位：普通任务跳普通）
                return GoAdventure(def.chapter, def.difficulty);

            case MainQuestType.ClearStages:
                // 密度行（2026-10-26）：任意主线关都算进度，同样送冒险页定位当前章
                return GoAdventure(def.chapter, def.difficulty);

            case MainQuestType.TalkNpc:
                // 找老板娘：人就在酒馆，切过去再开口
                if (!GoTavern()) return false;
                Play(MainQuestDialogues.NpcTalk(def.param, def.chapter),
                    () => MainQuestSystem.NotifyTalk(def.param));
                return true;

            // WatchStory（回顾剧情）已整条删除（2026-11-01 主人拍板「不要回顾剧情这个模式，
            // 改成关卡里的或者酒馆对话里的」）：任务行全部出表，5 段剧情并入各章战前剧情
            // ChapterStoryBeats.PlayPre1/3/4/6/8，随进关自动播，不再走任务跳转。

            case MainQuestType.RecruitMerc:
                // 雇人：酒馆的三选一招募是**唯一**雇佣入口
                // （2026-10-31 主人拍板「酒馆没有名册解锁功能」→ TavernUnlockUI 已删，这里改开招募弹窗；
                //   开启门槛与酒馆招募按钮同源 FeatureUnlock.MercRecruitOpen，别各判各的）
                if (!GoTavern()) return false;
                if (!FeatureUnlock.MercRecruitOpen(SaveSystem.Instance?.Data))
                {
                    UIManager.Instance?.ShowToast(FeatureUnlock.MercRecruitLockHint());
                    return true;   // 已切到酒馆并告知原因，不算跳转失败
                }
                MercenaryRecruitPopupUI.Show();
                return true;

            case MainQuestType.UpgradeTalent:
                // 点天赋：角色页 → 天赋界面
                return GoTalent();
        }
        return false;
    }

    /// <summary>每日任务 → 对应功能界面（2026-10-26 主人拍板「按任务类型前往」：战斗→冒险、升级天赋→角色界面、升一级→冒险）。</summary>
    public static bool GoDaily(DailyQuestView d)
    {
        if (d.id == "daily_login")
        {
            DailyLoginUI.Show();
            return true;
        }
        // 类型路由：读表里这行日常的 target_type（表缺失/行缺失按战斗类兜进冒险页——日常本质都是打关，不会白点）
        if (QuestTable.TryDaily(d.id, out var row))
        {
            // 竞技场对战（2026-10-30 新增 daily_arena_battle）：直接开竞技场弹窗——这类跳冒险页没有入口，等于白点
            if (row.targetType == "daily_arena_battle")
            {
                ArenaRules.EnsurePeriods(SaveSystem.Instance?.Data);
                ArenaPopupUI.Show();
                return true;
            }
            if (row.targetType == "upgrade_talent")
                return GoTalent();   // 点天赋/升级类 → 角色界面+天赋
        }
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

    /// <summary>冒险页 + 定位章节。【2026-10-26 主人拍板】任务前往即使章没开也定位过去（看锁态），ignoreLock=true。
    /// 【2026-11-05】按任务自身难度落位：普通任务跳普通、困难任务跳困难、噩梦任务跳噩梦。
    /// 🔴 难度任务（diff&gt;0）必须先过 <see cref="ChapterRouteTable.CanEnterAtDiff"/>：
    /// 没解锁就只给提示、不静默切页（普通任务保持 ignoreLock 口径，允许看锁态）。</summary>
    static bool GoAdventure(int chapter, int difficulty = 0)
    {
        var hub = TownHubController.Instance;
        if (hub == null) return false;

        int ch = chapter > 0 ? chapter : MainQuestSystem.CurrentChapter();
        int diff = Mathf.Clamp(difficulty, 0, 2);
        if (diff > 0 && !ChapterRouteTable.CanEnterAtDiff(ch, diff, SaveSystem.Instance?.Data))
        {
            UIManager.Instance?.ShowToast(AdventureUI.DiffLockHintText(diff));
            return true;   // 已告知原因，不算跳转失败
        }

        hub.OpenAdventure();

        // 页面已在 OpenAdventure 里预加载好，这里只把选中章拨过去
        var adv = AdventureUI.Instance;
        if (adv != null)
            adv.FocusChapter(ch, ignoreLock: true, difficulty: diff);
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
