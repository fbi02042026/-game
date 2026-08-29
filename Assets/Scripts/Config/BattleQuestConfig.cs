using UnityEngine;

/// <summary>
/// 战斗任务与清关金币：数据来自《剧情文案与任务设计》与《完整策划案》。
/// 金币仅在完成任务（清关开箱）、金币副本通关等条件下发放，击杀不掉落。
/// </summary>
public static class BattleQuestConfig
{
    /// <summary>各章 Boss 通关赏金（策划：章节任务奖励金币）</summary>
    static readonly int[] ChapterBossGold =
    {
        200, 300, 400, 500, 600, 700, 800, 2000
    };

    /// <summary>Boss 关任务文案（表面目标第三步）</summary>
    static readonly string[] ChapterBossObjective =
    {
        "击败 Boss 森之守护者",
        "击败 Boss 墓园守卫",
        "击败 Boss 雨林巨蟒",
        "击败 Boss 海妖蟹",
        "击败 Boss 时之风车精灵",
        "击败 Boss 晶石巨像",
        "击败 Boss 裂隙化身 · 小美",
        "击败 Boss 裂缝意志"
    };

    public struct StageQuest
    {
        public string objective;
        public int clearGold;
    }

    /// <summary>进关时 HUD 任务区展示</summary>
    public static StageQuest GetStageQuest(int chapter, StageType stageType, bool isGoldDungeon, int difficulty)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        if (isGoldDungeon)
        {
            return new StageQuest
            {
                objective = "清剿金币副本敌人",
                clearGold = GameConfig.GetGoldDungeonClearGold(ch, difficulty)
            };
        }

        string objective = stageType == StageType.Boss
            ? GetBossObjective(ch)
            : "击败所有敌人";

        return new StageQuest
        {
            objective = objective,
            clearGold = GetClearGold(ch, stageType, difficulty)
        };
    }

    public static string GetBossObjective(int chapter)
    {
        int idx = Mathf.Clamp(chapter - 1, 0, ChapterBossObjective.Length - 1);
        return ChapterBossObjective[idx];
    }

    /// <summary>清关开箱发放的金币（不含三选一折金）</summary>
    public static int GetClearGold(int chapter, StageType stageType, int difficulty)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        float diffMul = GameConfig.GetDifficultyGoldMul(difficulty);

        if (stageType == StageType.Boss)
        {
            int idx = Mathf.Clamp(ch - 1, 0, ChapterBossGold.Length - 1);
            return Mathf.RoundToInt(ChapterBossGold[idx] * diffMul);
        }

        int normal = GetNormalClearGold(ch, diffMul);
        if (stageType == StageType.Elite)
            return Mathf.RoundToInt(normal * 1.5f); // 策划：精英关金币 +50%

        return normal;
    }

    /// <summary>普通关「基础金币」：随章节略涨</summary>
    static int GetNormalClearGold(int chapter, float diffMul)
    {
        int baseGold = 25 + chapter * 10;
        return Mathf.RoundToInt(baseGold * diffMul);
    }
}
