using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>主线任务运行时视图（给 UI 用，不进存档）。</summary>
public class MainQuestView
{
    public MainQuestDef def;
    public int progress;
    public int need;
    public bool done;
    /// <summary>本章当前该做的那一条。</summary>
    public bool isCurrent;

    public string Desc => MainQuestDefs.AutoDesc(def);

    public string ProgressText => need > 1 ? progress + "/" + need : "";
}

/// <summary>每日任务运行时视图。</summary>
public struct DailyQuestView
{
    public string id;
    public string title;
    public string desc;
    public int progress;
    public int need;
    public string rewardText;
    public bool done;
    public bool claimable;
}

/// <summary>
/// 主线任务系统（2026-09-19 新增）。
///
/// 只做三件事：读进度 → 判完成 → 发反馈。不碰战斗，不改章节推进规则。
///
/// ⚠ 持久化方案（重要，后续要迁）：
///   本次**禁止改 SaveData.cs**，所以「非战斗任务的完成状态」落在 PlayerPrefs
///   （key 前缀 mq_v1_），而**战斗类（ClearStage）直接复用存档已有的
///   clearedStages**（chapter_stageIndex），不额外存一份。
///   代价：PlayerPrefs 不随云存档/换设备走，清数据会丢非战斗任务的打勾。
///   TODO(后续)：SaveData 加 mainQuestDoneEntries / mainQuestCntEntries 两个
///   List&lt;StringIdEntry&gt; / List&lt;StringIntEntry&gt;，把 PP_DONE / PP_CNT / PP_BASE
///   三处读写换成 HashSet/Dictionary 即可，MainQuestSystem 对外接口不变。
/// </summary>
public static class MainQuestSystem
{
    const string PP_DONE = "mq_v1_done_";
    const string PP_CNT = "mq_v1_cnt_";
    const string PP_BASE = "mq_v1_base_";

    /// <summary>任务状态变化（完成/领取）→ HUD 与面板刷新。</summary>
    public static event Action OnChanged;

    static SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    // ============================================================
    // 章节
    // ============================================================

    /// <summary>玩家当前所在的章节（取已解锁的最大章）。</summary>
    public static int CurrentChapter()
    {
        var d = Data;
        if (d == null) return 1;

        int ch = d.maxUnlockedChapter > 0 ? d.maxUnlockedChapter : 1;
        var avail = ChapterRouteTable.AvailableChapters(d);
        if (avail != null && avail.Count > 0)
            ch = avail[avail.Count - 1];
        return ch > 0 ? ch : 1;
    }

    /// <summary>本章已首次通关的关卡数（长线牵引："快到头了"）。</summary>
    public static int ChapterClearedCount(int chapter)
    {
        var d = Data;
        return d == null ? 0 : d.ClearedStageCountInChapter(chapter);
    }

    public static int ChapterStageTotal => GameConfig.STAGES_PER_CHAPTER;

    public static bool IsChapterCleared(int chapter)
    {
        var d = Data;
        return d != null && d.HasClearedChapter(chapter);
    }

    // ============================================================
    // 主线任务视图
    // ============================================================

    /// <summary>某章全部任务视图；第一条未完成的标 isCurrent。</summary>
    public static List<MainQuestView> ChapterViews(int chapter)
    {
        var list = new List<MainQuestView>();
        var defs = MainQuestDefs.QuestsOf(chapter);
        bool currentAssigned = false;

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            int need = NeedOf(def);
            int prog = Math.Min(need, ProgressOf(def));
            bool done = IsDone(def);

            var v = new MainQuestView
            {
                def = def,
                need = need,
                progress = done ? need : prog,
                done = done,
                isCurrent = false
            };
            if (!done && !currentAssigned)
            {
                v.isCurrent = true;
                currentAssigned = true;
            }
            list.Add(v);
        }
        return list;
    }

    /// <summary>当前该做的那一条；本章全做完（或未配表）返回 null。</summary>
    public static MainQuestView CurrentView()
    {
        var list = ChapterViews(CurrentChapter());
        for (int i = 0; i < list.Count; i++)
            if (list[i].isCurrent) return list[i];
        return null;
    }

    public static bool IsDone(MainQuestDef def)
    {
        if (string.IsNullOrEmpty(def.id)) return false;
        if (PlayerPrefs.GetInt(PP_DONE + def.id, 0) > 0) return true;

        // 整章已通关 → 该章任务一律算完成，避免老玩家被非战斗任务卡住
        if (IsChapterCleared(def.chapter)) return true;

        return ProgressOf(def) >= NeedOf(def);
    }

    static int NeedOf(MainQuestDef def)
    {
        if (def.type == MainQuestType.ClearStage) return 1;
        return def.targetCount > 0 ? def.targetCount : 1;
    }

    /// <summary>
    /// 各类任务的完成判定：
    ///   ClearStage    → 存档 clearedStages 里有没有 "章_关卡下标"（现成字段，不新增持久化）
    ///   TalkNpc       → 播完对话后 NotifyTalk 写计数
    ///   WatchStory    → 播完剧情后 NotifyStory 写计数
    ///   RecruitMerc   → 已解锁佣兵数 - 接任务时的基线（不入侵 TavernUnlockUI，只轮询）
    ///   UpgradeTalent → 接任务时的天赋石基线 - 当前天赋石（不入侵 TalentUI，只轮询）
    /// </summary>
    static int ProgressOf(MainQuestDef def)
    {
        var d = Data;
        if (d == null) return 0;

        switch (def.type)
        {
            case MainQuestType.ClearStage:
            {
                int idx = MainQuestDefs.StageIndexOf(def);
                return (d.clearedStages != null && d.clearedStages.Contains(def.chapter + "_" + idx)) ? 1 : 0;
            }

            case MainQuestType.TalkNpc:
            case MainQuestType.WatchStory:
                return PlayerPrefs.GetInt(PP_CNT + def.id, 0);

            case MainQuestType.RecruitMerc:
            {
                int cur = d.unlockedMercIds != null ? d.unlockedMercIds.Count : 0;
                return Math.Max(0, cur - Baseline(def.id, cur));
            }

            case MainQuestType.UpgradeTalent:
            {
                long cur = ResourceWallet.Get(d, ResourceWallet.ResourceType.TalentPoint);
                int curInt = (int)Math.Min(cur, int.MaxValue);
                return Math.Max(0, Baseline(def.id, curInt) - curInt);
            }
        }
        return 0;
    }

    /// <summary>首次评估时记下基线（雇佣数 / 天赋石），之后 progress = 变化量。</summary>
    static int Baseline(string id, int current)
    {
        string key = PP_BASE + id;
        if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetInt(key, current);
        PlayerPrefs.SetInt(key, current);
        PlayerPrefs.Save();
        return current;
    }

    // ============================================================
    // 完成
    // ============================================================

    /// <summary>每 0.5s 由 QuestHudBar 驱动：轮询类任务（雇人/点天赋）达标即结算。</summary>
    public static void Tick()
    {
        var views = ChapterViews(CurrentChapter());
        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            if (v.done) continue;
            if (ProgressOf(v.def) >= v.need)
                Complete(v.def);
        }
    }

    /// <summary>对话播完：把本章对应的 TalkNpc 任务计数 +1。</summary>
    public static void NotifyTalk(string npcId)
    {
        BumpCount(def => def.type == MainQuestType.TalkNpc && def.param == npcId);
    }

    /// <summary>剧情播完：把本章对应的 WatchStory 任务计数 +1。</summary>
    public static void NotifyStory(string storyId)
    {
        BumpCount(def => def.type == MainQuestType.WatchStory && def.param == storyId);
    }

    static void BumpCount(Func<MainQuestDef, bool> match)
    {
        var defs = MainQuestDefs.QuestsOf(CurrentChapter());
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (!match(def)) continue;
            if (IsDone(def)) continue;

            int need = NeedOf(def);
            PlayerPrefs.SetInt(PP_CNT + def.id, need);
            PlayerPrefs.Save();
            Complete(def);
            return;
        }
    }

    static void Complete(MainQuestDef def)
    {
        if (string.IsNullOrEmpty(def.id)) return;
        if (PlayerPrefs.GetInt(PP_DONE + def.id, 0) > 0) return;

        PlayerPrefs.SetInt(PP_DONE + def.id, 1);
        PlayerPrefs.Save();

        if (def.rewardStones > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, def.rewardStones,
                save: true, notify: true);
            GlobalToastUI.Show("\u4e3b\u7ebf\u4efb\u52a1\u5b8c\u6210\uff1a" + def.title
                               + "  \u83b7\u5f97 " + def.rewardStones + " \u5929\u8d4b\u77f3");
        }
        else
        {
            GlobalToastUI.Show("\u4e3b\u7ebf\u4efb\u52a1\u5b8c\u6210\uff1a" + def.title);
        }

        Debug.Log("[MainQuest] done: " + def.id);
        OnChanged?.Invoke();
    }

    // ============================================================
    // 每日任务
    // ============================================================

    /// <summary>今日已通关关数（跨天归零；存档只在通关时刷新日期键，这里补一次判定）。</summary>
    public static int TodayClearCount()
    {
        var d = Data;
        if (d == null) return 0;
        string today = ShopDefs.TodayKey();
        if (d.dailyStageClearDayKey != today) return 0;
        return d.dailyStageClearCount;
    }

    public static List<DailyQuestView> DailyViews()
    {
        var list = new List<DailyQuestView>();
        var d = Data;

        // ① 每日登录：走现成的 DailyLoginSystem（手动领，不是自动发放）
        bool loginClaimed = DailyLoginSystem.CycleClaimedToday;
        string loginReward = "";
        int idx = DailyLoginSystem.CycleIndex;
        if (idx >= 0 && idx < DailyLoginDefs.Cycle.Length)
            loginReward = DailyLoginDefs.Cycle[idx].DisplayName.Replace("\n", "\u3001");

        list.Add(new DailyQuestView
        {
            id = "daily_login",
            title = "\u6bcf\u65e5\u767b\u5f55",
            desc = "\u9886\u53d6\u4eca\u65e5\u767b\u5f55\u5956\u52b1",
            progress = loginClaimed ? 1 : 0,
            need = 1,
            rewardText = loginReward,
            done = loginClaimed,
            claimable = !loginClaimed
        });

        // ② 通关 3 关：ChapterManager.OnStageComplete 里达标自动发 5 石，这里只显示进度
        int need = SaveData.DAILY_CLEAR_TASK_NEED;
        int cur = Math.Min(need, TodayClearCount());
        bool clearClaimed = d != null && d.dailyClearTaskClaimDayKey == ShopDefs.TodayKey();

        list.Add(new DailyQuestView
        {
            id = "daily_clear",
            title = "\u4eca\u65e5\u901a\u5173",
            desc = "\u4eca\u65e5\u901a\u5173 " + cur + "/" + need + " \u5173",
            progress = cur,
            need = need,
            rewardText = "\u5929\u8d4b\u77f3 \u00d7" + SaveData.DAILY_CLEAR_TASK_STONES,
            done = clearClaimed || cur >= need,
            claimable = false
        });

        return list;
    }

    /// <summary>每日还有没做完的（条上切到每日时用）。</summary>
    public static DailyQuestView? FirstOpenDaily()
    {
        var list = DailyViews();
        for (int i = 0; i < list.Count; i++)
            if (!list[i].done) return list[i];
        return null;
    }

    public static void NotifyChanged() => OnChanged?.Invoke();
}
