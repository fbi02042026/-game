using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 下一关抽取规则（本版：普通 / 精英 / 恢复 / 首领）。
/// 权重系数优先读 stage_roller_weights 表。
/// </summary>
public static class StageRoller
{
    public const int BossWindowFallback = 3;
    public const int MaxRestPerChapterFallback = 2;

    public static int BossWindow
    {
        get
        {
            StageRollerWeightsTable.EnsureLoaded();
            return StageRollerWeightsTable.HasData
                ? StageRollerWeightsTable.GetInt("bosswindow", BossWindowFallback)
                : BossWindowFallback;
        }
    }

    public static int MaxRestPerChapter
    {
        get
        {
            StageRollerWeightsTable.EnsureLoaded();
            return StageRollerWeightsTable.HasData
                ? StageRollerWeightsTable.GetInt("maxrestperchapter", MaxRestPerChapterFallback)
                : MaxRestPerChapterFallback;
        }
    }

    public class ChapterRollState
    {
        public int restCount;
        public int combatStagesDone;
        public bool bossPlaced;

        public StageType craftKind = StageType.Forge;
        public bool craftUsed;

        public void Reset()
        {
            restCount = 0;
            combatStagesDone = 0;
            bossPlaced = false;
            craftUsed = false;
        }

        public void RecordCleared(StageType type)
        {
            switch (type)
            {
                case StageType.Rest: restCount++; break;
                case StageType.Boss: bossPlaced = true; goto case StageType.Normal;
                case StageType.Normal:
                case StageType.Elite:
                    combatStagesDone++;
                    break;
            }
        }
    }

    public static StageType NormalizeDisplayType(StageType t)
    {
        switch (t)
        {
            case StageType.Elite:
            case StageType.Rest:
            case StageType.Boss:
                return t;
            default:
                return StageType.Normal;
        }
    }

    static float W(string key, float fallback) =>
        StageRollerWeightsTable.GetFloat(key, fallback);

    public static StageType Roll(ChapterRollState state, int stageIndex, int totalStages)
    {
        if (state == null) return StageType.Normal;

        int bossWindow = BossWindow;
        int maxRest = MaxRestPerChapter;
        int last = Mathf.Max(0, totalStages - 1);
        int remainingAfter = last - stageIndex;

        if (stageIndex >= last) return StageType.Boss;

        var weights = new List<KeyValuePair<StageType, float>>(4);

        float bossBase = W("bossweightbase", 0.22f);
        float bossStep = W("bossweightstep", 0.24f);
        if (!state.bossPlaced && stageIndex >= last - (bossWindow - 1))
        {
            int stepsIntoWindow = stageIndex - (last - (bossWindow - 1));
            weights.Add(new KeyValuePair<StageType, float>(StageType.Boss, bossBase + stepsIntoWindow * bossStep));
        }

        if (state.restCount < maxRest)
        {
            bool mustRest = state.restCount == 0 && remainingAfter <= bossWindow;
            if (mustRest) return StageType.Rest;
            float restBase = W("restweightbase", 0.10f);
            float restPerIdx = W("restweightperstageindex", 0.035f);
            float restFirstMul = W("restfirstchaptermultiplier", 1.6f);
            float w = restBase + stageIndex * restPerIdx;
            if (state.restCount == 0) w *= restFirstMul;
            weights.Add(new KeyValuePair<StageType, float>(StageType.Rest, w));
        }

        float eliteBase = W("eliteweightbase", 0.15f);
        float elitePerIdx = W("eliteweightperstageindex", 0.05f);
        float normalFloor = W("normalweightfloor", 0.2f);
        float normalComplement = W("normalweightcomplement", 1f);
        float eliteW = eliteBase + stageIndex * elitePerIdx;
        weights.Add(new KeyValuePair<StageType, float>(StageType.Elite, eliteW));
        weights.Add(new KeyValuePair<StageType, float>(StageType.Normal, Mathf.Max(normalFloor, normalComplement - eliteW)));

        return WeightedPick(weights);
    }

    static StageType WeightedPick(List<KeyValuePair<StageType, float>> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i].Value);
        if (total <= 0.0001f) return StageType.Normal;

        float r = Random.value * total;
        for (int i = 0; i < weights.Count; i++)
        {
            r -= Mathf.Max(0f, weights[i].Value);
            if (r <= 0f) return weights[i].Key;
        }
        return weights[weights.Count - 1].Key;
    }

    public static List<StageType> BuildReel(ChapterRollState state, int stageIndex, int totalStages,
        StageType winner, int length)
    {
        winner = NormalizeDisplayType(winner);
        var pool = CandidatePool(state, stageIndex, totalStages);
        var reel = new List<StageType>(Mathf.Max(4, length));
        for (int i = 0; i < length; i++)
        {
            StageType t = pool[Random.Range(0, pool.Count)];
            if (reel.Count > 0 && reel[reel.Count - 1] == t && pool.Count > 1)
                t = pool[(pool.IndexOf(t) + 1) % pool.Count];
            reel.Add(t);
        }
        if (reel.Count > 0) reel[reel.Count - 1] = winner;
        else reel.Add(winner);
        return reel;
    }

    static List<StageType> CandidatePool(ChapterRollState state, int stageIndex, int totalStages)
    {
        var pool = new List<StageType> { StageType.Normal, StageType.Elite };
        if (state == null) return pool;

        int bossWindow = BossWindow;
        int maxRest = MaxRestPerChapter;
        int last = Mathf.Max(0, totalStages - 1);
        if (state.restCount < maxRest) pool.Add(StageType.Rest);
        if (!state.bossPlaced && stageIndex >= last - (bossWindow - 1)) pool.Add(StageType.Boss);
        return pool;
    }

    public static int RestMaterialReward(int stageIndex, int chapter)
    {
        int baseAmount = 2 + stageIndex;
        float chapterMul = 1f + 0.25f * Mathf.Max(0, chapter - 1);
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * chapterMul));
    }

    public static string Label(StageType t)
    {
        switch (NormalizeDisplayType(t))
        {
            case StageType.Normal: return "普通关卡";
            case StageType.Elite: return "精英关卡";
            case StageType.Rest: return "恢复关卡";
            case StageType.Boss: return "首领关卡";
            default: return "普通关卡";
        }
    }

    public static string Desc(StageType t, int stageIndex, int chapter)
    {
        switch (NormalizeDisplayType(t))
        {
            case StageType.Normal: return "常规敌人，稳定掉落";
            case StageType.Elite: return "精英敌人，装备品质更高";
            case StageType.Rest: return "回复生命 50%";
            case StageType.Boss: return "章节首领，必掉高品质装备";
            default: return "常规敌人，稳定掉落";
        }
    }

    public static Color Tint(StageType t)
    {
        switch (NormalizeDisplayType(t))
        {
            case StageType.Normal: return new Color(0.32f, 0.42f, 0.60f);
            case StageType.Elite: return new Color(0.55f, 0.34f, 0.70f);
            case StageType.Rest: return new Color(0.30f, 0.58f, 0.36f);
            case StageType.Boss: return new Color(0.68f, 0.22f, 0.22f);
            default: return new Color(0.32f, 0.42f, 0.60f);
        }
    }

    public static string IconName(StageType t)
    {
        switch (NormalizeDisplayType(t))
        {
            case StageType.Normal: return "stage_normal";
            case StageType.Elite: return "stage_elite";
            case StageType.Rest: return "stage_rest";
            case StageType.Boss: return "stage_boss";
            default: return "stage_normal";
        }
    }
}
