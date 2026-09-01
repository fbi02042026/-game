using UnityEngine;

/// <summary>
/// 战斗任务与清关金币：优先读 battle_quest 表，缺行回退内置常量。
/// 金币仅在完成任务（清关开箱）、金币副本通关等条件下发放，击杀不掉落。
/// </summary>
public static class BattleQuestConfig
{
    static readonly int[] ChapterBossGold =
    {
        200, 300, 400, 500, 600, 700, 800, 2000
    };

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

    public static StageQuest GetStageQuest(int chapter, StageType stageType, bool isGoldDungeon, int difficulty)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        BattleQuestTable.EnsureLoaded();

        if (BattleQuestTable.TryResolve(ch, stageType, isGoldDungeon, out var row))
        {
            if (isGoldDungeon)
            {
                return new StageQuest
                {
                    objective = string.IsNullOrEmpty(row.objective) ? "清剿金币副本敌人" : row.objective,
                    clearGold = GameConfig.GetGoldDungeonClearGold(ch, difficulty)
                };
            }

            string obj = row.objective;
            if (string.IsNullOrEmpty(obj))
                obj = stageType == StageType.Boss ? GetBossObjective(ch) : "击败所有敌人";

            return new StageQuest
            {
                objective = obj,
                clearGold = ResolveClearGold(ch, stageType, difficulty, row)
            };
        }

        if (isGoldDungeon)
        {
            return new StageQuest
            {
                objective = "清剿金币副本敌人",
                clearGold = GameConfig.GetGoldDungeonClearGold(ch, difficulty)
            };
        }

        return new StageQuest
        {
            objective = stageType == StageType.Boss ? GetBossObjective(ch) : "击败所有敌人",
            clearGold = GetClearGold(ch, stageType, difficulty)
        };
    }

    static int ResolveClearGold(int ch, StageType stageType, int difficulty, BattleQuestTable.Row row)
    {
        float diffMul = GameConfig.GetDifficultyGoldMul(difficulty);
        if (row.clearGold > 0)
            return Mathf.RoundToInt(row.clearGold * diffMul);

        if (row.useFormula)
        {
            int normal = Mathf.RoundToInt((row.normalBase + ch * row.normalChapterAdd) * diffMul);
            if (stageType == StageType.Elite)
                return Mathf.RoundToInt(normal * row.eliteGoldMul);
            return normal;
        }

        return GetClearGold(ch, stageType, difficulty);
    }

    public static string GetBossObjective(int chapter)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        BattleQuestTable.EnsureLoaded();
        if (BattleQuestTable.TryResolve(ch, StageType.Boss, false, out var row)
            && !string.IsNullOrEmpty(row.objective))
            return row.objective;

        int idx = Mathf.Clamp(ch - 1, 0, ChapterBossObjective.Length - 1);
        return ChapterBossObjective[idx];
    }

    public static int GetClearGold(int chapter, StageType stageType, int difficulty)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        float diffMul = GameConfig.GetDifficultyGoldMul(difficulty);

        BattleQuestTable.EnsureLoaded();
        if (BattleQuestTable.TryResolve(ch, stageType, false, out var row))
        {
            int fromTable = ResolveClearGold(ch, stageType, difficulty, row);
            if (row.clearGold > 0 || row.useFormula)
                return fromTable;
        }

        if (stageType == StageType.Boss)
        {
            int idx = Mathf.Clamp(ch - 1, 0, ChapterBossGold.Length - 1);
            return Mathf.RoundToInt(ChapterBossGold[idx] * diffMul);
        }

        int normal = GetNormalClearGold(ch, diffMul);
        if (stageType == StageType.Elite)
            return Mathf.RoundToInt(normal * 1.5f);

        return normal;
    }

    static int GetNormalClearGold(int chapter, float diffMul)
    {
        int baseGold = 25 + chapter * 10;
        return Mathf.RoundToInt(baseGold * diffMul);
    }
}
