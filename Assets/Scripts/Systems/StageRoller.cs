using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 下一关抽取规则（本版：普通 / 精英 / 恢复 / 首领）。
/// 每打完一关现抽，结果交给 NextStageRouletteUI 滚动展示。
/// </summary>
public static class StageRoller
{
    /// <summary>首领关最早可能出现的位置：倒数第 3 关</summary>
    public const int BossWindow = 3;
    /// <summary>恢复关每章上限</summary>
    public const int MaxRestPerChapter = 2;

    /// <summary>一章之内的抽取状态：随章节重置</summary>
    public class ChapterRollState
    {
        public int restCount;
        public int combatStagesDone;
        public bool bossPlaced;

        // 保留字段供旧存档/日志兼容，本版不参与抽选
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

    /// <summary>轮盘/石墩只展示四类；其余枚举映射为普通。</summary>
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

    public static StageType Roll(ChapterRollState state, int stageIndex, int totalStages)
    {
        if (state == null) return StageType.Normal;

        int last = Mathf.Max(0, totalStages - 1);
        int remainingAfter = last - stageIndex;

        if (stageIndex >= last) return StageType.Boss;

        var weights = new List<KeyValuePair<StageType, float>>(4);

        if (!state.bossPlaced && stageIndex >= last - (BossWindow - 1))
        {
            int stepsIntoWindow = stageIndex - (last - (BossWindow - 1));
            weights.Add(new KeyValuePair<StageType, float>(StageType.Boss, 0.22f + stepsIntoWindow * 0.24f));
        }

        if (state.restCount < MaxRestPerChapter)
        {
            bool mustRest = state.restCount == 0 && remainingAfter <= BossWindow;
            if (mustRest) return StageType.Rest;
            float w = 0.10f + stageIndex * 0.035f;
            if (state.restCount == 0) w *= 1.6f;
            weights.Add(new KeyValuePair<StageType, float>(StageType.Rest, w));
        }

        float eliteW = 0.15f + stageIndex * 0.05f;
        weights.Add(new KeyValuePair<StageType, float>(StageType.Elite, eliteW));
        weights.Add(new KeyValuePair<StageType, float>(StageType.Normal, Mathf.Max(0.2f, 1f - eliteW)));

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

        int last = Mathf.Max(0, totalStages - 1);
        if (state.restCount < MaxRestPerChapter) pool.Add(StageType.Rest);
        if (!state.bossPlaced && stageIndex >= last - (BossWindow - 1)) pool.Add(StageType.Boss);
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
