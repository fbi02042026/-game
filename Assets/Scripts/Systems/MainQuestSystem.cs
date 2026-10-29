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
    /// 持久化方案（2026-09-20 已迁移）：
    ///   非战斗任务的完成状态现在落在 SaveData 的 mainQuestDone / mainQuestCnt /
    ///   mainQuestBase（双写：List 镜像 + [NonSerialized] 运行时集合），随云存档走；
    ///   战斗类（ClearStage）仍复用存档已有的 clearedStages（chapter_stageIndex），不额外存。
    ///   老档（PlayerPrefs 的 mq_v1_*）首次启动由 EnsureMigrated 一次性搬入 SaveData，
    ///   PP 原值不删除，留作退路。对外接口不变。
/// </summary>
public static class MainQuestSystem
{
    const string PP_DONE = "mq_v1_done_";
    const string PP_CNT = "mq_v1_cnt_";
    const string PP_BASE = "mq_v1_base_";
    const string PP_MIGRATED = "mq_v1_migrated";

    /// <summary>任务状态变化（完成/领取）→ HUD 与面板刷新。</summary>
    public static event Action OnChanged;

    static SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    // 迁移一次性标记（本会话内）。存档未就绪时静默跳过，下次访问再试。
    static bool _migrated = false;

    static void Persist() => SaveSystem.Instance?.Save();

    static bool IsDoneId(string id)
    {
        var d = Data;
        if (d == null || d.mainQuestDone == null) return false;
        return d.mainQuestDone.Contains(id);
    }

    static void MarkDone(string id)
    {
        var d = Data;
        if (d == null) return;
        if (d.mainQuestDone == null) d.mainQuestDone = new HashSet<string>();
        if (d.mainQuestDone.Add(id)) Persist();
    }

    static int GetCnt(string id)
    {
        var d = Data;
        if (d == null || d.mainQuestCnt == null) return 0;
        return d.mainQuestCnt.TryGetValue(id, out int v) ? v : 0;
    }

    static void BumpCnt(string id, int need)
    {
        var d = Data;
        if (d == null) return;
        if (d.mainQuestCnt == null) d.mainQuestCnt = new Dictionary<string, int>();
        d.mainQuestCnt[id] = need;
        Persist();
    }

    static int ReadBase(string id, int current)
    {
        var d = Data;
        if (d == null) return current;
        if (d.mainQuestBase == null) d.mainQuestBase = new Dictionary<string, int>();
        if (d.mainQuestBase.TryGetValue(id, out int v)) return v;
        d.mainQuestBase[id] = current;
        Persist();
        return current;
    }

    /// <summary>
    /// 旧档迁移：把 PlayerPrefs 的 mq_v1_* 进度一次性搬到 SaveData（只搬一次，靠 PP 标记防重）。
    /// 存档未就绪时静默跳过，下次访问再试。不删除 PP 原值，留作退路。
    /// </summary>
    static void EnsureMigrated()
    {
        if (_migrated) return;
        var d = Data;
        if (d == null) return; // 存档还没好，等下次
        if (PlayerPrefs.GetInt(PP_MIGRATED, 0) > 0) { _migrated = true; return; }

        if (d.mainQuestDone == null) d.mainQuestDone = new HashSet<string>();
        if (d.mainQuestCnt == null) d.mainQuestCnt = new Dictionary<string, int>();
        if (d.mainQuestBase == null) d.mainQuestBase = new Dictionary<string, int>();

        for (int i = 0; i < MainQuestDefs.All.Length; i++)
        {
            var def = MainQuestDefs.All[i];
            if (string.IsNullOrEmpty(def.id)) continue;
            if (PlayerPrefs.GetInt(PP_DONE + def.id, 0) > 0)
                d.mainQuestDone.Add(def.id);
            if (PlayerPrefs.HasKey(PP_CNT + def.id))
                d.mainQuestCnt[def.id] = PlayerPrefs.GetInt(PP_CNT + def.id, 0);
            if (PlayerPrefs.HasKey(PP_BASE + def.id))
                d.mainQuestBase[def.id] = PlayerPrefs.GetInt(PP_BASE + def.id, 0);
        }

        PlayerPrefs.SetInt(PP_MIGRATED, 1);
        PlayerPrefs.Save();
        _migrated = true;
        Persist(); // 把搬迁结果落盘
    }

    // ============================================================
    // 章节
    // ============================================================

    /// <summary>玩家当前应推进的章节：可选章节里第一个未通关的（全通关返回最后一章）。</summary>
    public static int CurrentChapter()
    {
        var d = Data;
        if (d == null) return 1;

        var avail = ChapterRouteTable.AvailableChapters(d);
        if (avail != null && avail.Count > 0)
        {
            foreach (int c in avail)
                if (!d.HasClearedChapter(c)) return c;
            return avail[avail.Count - 1]; // 全通关返回最后一章
        }
        return 1;
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
        EnsureMigrated();
        if (string.IsNullOrEmpty(def.id)) return false;
        if (IsDoneId(def.id)) return true;

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
        EnsureMigrated();
        var d = Data;
        if (d == null) return 0;

        switch (def.type)
        {
            case MainQuestType.ClearStage:
            {
                int idx = MainQuestDefs.StageIndexOf(def);
                return (d.clearedStages != null && d.clearedStages.Contains(def.chapter + "_" + idx)) ? 1 : 0;
            }

            case MainQuestType.ClearStages:
            {
                // 密度行（2026-10-26）：基线 = 首次评估时本章已通关数，之后每多通一关 +1。
                // 不新增存档字段，复用 mainQuestBase 基线机制（Tick 轮询达标即结算）。
                int cur = d.ClearedStageCountInChapter(def.chapter);
                return Math.Max(0, cur - Baseline(def.id, cur));
            }

            case MainQuestType.TalkNpc:
            case MainQuestType.WatchStory:
                return GetCnt(def.id);

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
        return ReadBase(id, current);
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
        EnsureMigrated();
        var defs = MainQuestDefs.QuestsOf(CurrentChapter());
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (!match(def)) continue;
            if (IsDone(def)) continue;

            int need = NeedOf(def);
            BumpCnt(def.id, need);
            Complete(def);
            return;
        }
    }

    static void Complete(MainQuestDef def)
    {
        EnsureMigrated();
        if (string.IsNullOrEmpty(def.id)) return;
        if (IsDoneId(def.id)) return;

        MarkDone(def.id);

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
        Analytics.QuestComplete(def.id); // 埋点：主线任务完成
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

        // 文案/参数来自 quest.csv（2026-10-26 任务体系单独成模块）；缺行 LogError 不兜底。
        // 【2026-10-29 扩编】表驱动：全部日常行按 order 升序输出（简单→困难）。
        // 今日通关保留专属链路（ChapterManager 发石）；其余全走通用计数视图（含每日登录：
        // 2026-10-29 主人拍板「不要强制玩家点击每日登录领取 只要每天打开游戏就给奖励」→
        // 进度=GetTaskCounter(daily_login)=loginLastDay==今天，DailyTaskSystem 自动发奖，
        // 不再有手动领取入口、也不再判 DailyLoginSystem.ClaimedToday），
        // 计数器真源 = SaveData.GetTaskCounter(targetType)，奖励由 DailyTaskSystem 达标自动发。
        var rows = QuestTable.RowsOfCategory("日常");
        bool sawLogin = false, sawClear = false;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // ① 每日登录走通用计数视图（2026-10-29 主人拍板「不要强制玩家点击每日登录领取
            //    只要每天打开游戏就给奖励」：进度=GetTaskCounter(daily_login)=loginLastDay==今天，
            //    打开游戏 1 秒后 OnEnterGame 落 loginLastDay 即达标，DailyTaskSystem 自动发经验，
            //    不再有手动领取入口、也不再判 ClaimedToday）。这里只记 sawLogin 供缺行断言。
            if (row.id == "daily_login") sawLogin = true;

            // ② 通关 3 关：ChapterManager.OnStageComplete 里达标自动发 5 石，这里只显示进度。
            //    need 真源 = SaveData.DAILY_CLEAR_TASK_NEED（发奖同一处）；表里 target_count 仅为参照。
            if (row.id == "daily_clear")
            {
                sawClear = true;
                int need = SaveData.DAILY_CLEAR_TASK_NEED;
                int cur = Math.Min(need, TodayClearCount());
                bool clearClaimed = d != null && d.dailyClearTaskClaimDayKey == ShopDefs.TodayKey();
                list.Add(new DailyQuestView
                {
                    id = row.id,
                    title = row.title,
                    desc = string.Format(row.desc, cur, need),
                    progress = cur,
                    need = need,
                    rewardText = row.rewardText,
                    done = clearClaimed || cur >= need,
                    claimable = false
                });
                continue;
            }

            // ③ 其余日常（每日登录/财源广进/除害/招贤纳士/清剿/讨伐首领）：通用计数视图
            int cntNeed = row.targetCount > 0 ? row.targetCount : 1;
            int cnt = Math.Min(cntNeed, d != null ? d.GetTaskCounter(row.targetType) : 0);
            bool cntClaimed = d != null && d.IsTaskRewardClaimed(row.id, int.Parse(ShopDefs.TodayKey()));
            list.Add(new DailyQuestView
            {
                id = row.id,
                title = row.title,
                desc = string.Format(row.desc, cnt, cntNeed),
                progress = cnt,
                need = cntNeed,
                rewardText = row.rewardText,
                done = cntClaimed || cnt >= cntNeed,
                claimable = false
            });
        }

        if (!sawLogin) Debug.LogError("[MainQuest] quest.csv 缺 daily_login 行（日常列表少一条）");
        if (!sawClear) Debug.LogError("[MainQuest] quest.csv 缺 daily_clear 行（日常列表少一条）");
        return list;
    }

    /// <summary>
    /// 周常任务视图（2026-10-29 扩编：周一刷新，进度读周计数器，奖励 DailyTaskSystem 达标自动发钻石）。
    /// 与日常同结构；任务面板在日常页签尾部带小节头追加展示，任务栏（FirstOpenDaily）不含周常。
    /// </summary>
    public static List<DailyQuestView> WeeklyViews()
    {
        var list = new List<DailyQuestView>();
        var d = Data;
        var rows = QuestTable.RowsOfCategory("周常");
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int need = row.targetCount > 0 ? row.targetCount : 1;
            int cur = Math.Min(need, d != null ? d.GetTaskCounter(row.targetType) : 0);
            bool claimed = d != null && d.IsTaskRewardClaimed(row.id, SaveData.WeekMondayKey());
            list.Add(new DailyQuestView
            {
                id = row.id,
                title = row.title,
                desc = string.Format(row.desc, cur, need),
                progress = cur,
                need = need,
                rewardText = row.rewardText,
                done = claimed || cur >= need,
                claimable = false
            });
        }
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

    /// <summary>第三级兜底：是否还有未领取的成就（里程）奖励。只新增，不改既有行为。</summary>
    public static bool HasOpenAchievement()
    {
        var sys = AchievementSystem.Instance;
        return sys != null && sys.HasUnclaimedMilestone();
    }

    /// <summary>第三级兜底：是否还有未首次查看/记录的图鉴条目。只新增，不改既有行为。</summary>
    public static bool HasUnrecordedCodex()
    {
        return AdventureCodex.HasUnviewedCodex();
    }
}
