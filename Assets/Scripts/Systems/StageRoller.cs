using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 下一关抽取规则。
///
/// 每章的关卡类型不再开局一次性排好，而是每打完一关现抽一次，抽的结果交给
/// NextStageRouletteUI 滚动展示。规则：
/// - 恢复关：越往后几率越大，给的强化材料也越多；每章最少 1 次、最多 2 次
/// - 锻造 / 附魔：每章只会出现其中一种，且只出现 1 次；必须先打够几个战斗关才可能出
/// - 首领关：只在倒数三关内出现，越往后几率越大，最后一关必出
/// - 其余落到普通 / 精英，精英越往后越多
/// </summary>
public static class StageRoller
{
    /// <summary>首领关最早可能出现的位置：倒数第 3 关</summary>
    public const int BossWindow = 3;
    /// <summary>锻造/附魔关之前至少要打过几个战斗关</summary>
    public const int MinCombatBeforeCraft = 3;
    /// <summary>恢复关每章上限</summary>
    public const int MaxRestPerChapter = 2;

    /// <summary>一章之内的抽取状态：随章节重置</summary>
    public class ChapterRollState
    {
        /// <summary>本章已经出过几次恢复关</summary>
        public int restCount;
        /// <summary>本章的「工坊类」关卡是锻造还是附魔（开章定死一种）</summary>
        public StageType craftKind = StageType.Forge;
        /// <summary>工坊类关卡是否已经用掉</summary>
        public bool craftUsed;
        /// <summary>已经打完的战斗关（普通/精英/首领）数量</summary>
        public int combatStagesDone;
        /// <summary>首领关是否已经排出去了</summary>
        public bool bossPlaced;

        public void Reset()
        {
            restCount = 0;
            craftUsed = false;
            combatStagesDone = 0;
            bossPlaced = false;
            // 每章随机决定这章是锻造还是附魔
            craftKind = Random.value < 0.5f ? StageType.Forge : StageType.Enchant;
        }

        /// <summary>刚打完的关卡计入统计</summary>
        public void RecordCleared(StageType type)
        {
            switch (type)
            {
                case StageType.Rest: restCount++; break;
                case StageType.Forge:
                case StageType.Enchant: craftUsed = true; break;
                case StageType.Boss: bossPlaced = true; goto case StageType.Normal;
                case StageType.Normal:
                case StageType.Elite:
                    combatStagesDone++;
                    break;
            }
        }
    }

    /// <summary>
    /// 抽下一关的类型。stageIndex 是即将要打的那一关的下标（0 起）。
    /// </summary>
    public static StageType Roll(ChapterRollState state, int stageIndex, int totalStages)
    {
        if (state == null) return StageType.Normal;

        int last = Mathf.Max(0, totalStages - 1);
        int remainingAfter = last - stageIndex; // 这关之后还剩几关

        // 最后一关必定首领
        if (stageIndex >= last) return StageType.Boss;

        var weights = new List<KeyValuePair<StageType, float>>(5);

        // 首领：只在倒数三关内，越往后越大
        if (!state.bossPlaced && stageIndex >= last - (BossWindow - 1))
        {
            int stepsIntoWindow = stageIndex - (last - (BossWindow - 1)); // 0,1
            weights.Add(new KeyValuePair<StageType, float>(StageType.Boss, 0.22f + stepsIntoWindow * 0.24f));
        }

        // 恢复关：越往后越大；一章最多两次
        if (state.restCount < MaxRestPerChapter)
        {
            // 一章一次都没出过，而且快到首领窗口了 → 直接锁定，保证「最少一次」
            bool mustRest = state.restCount == 0 && remainingAfter <= BossWindow;
            if (mustRest) return StageType.Rest;
            float w = 0.10f + stageIndex * 0.035f;
            if (state.restCount == 0) w *= 1.6f; // 还没出过就更想出
            weights.Add(new KeyValuePair<StageType, float>(StageType.Rest, w));
        }

        // 锻造/附魔：打够战斗关才解锁，一章只出一种一次，越往后越大
        if (!state.craftUsed && state.combatStagesDone >= MinCombatBeforeCraft)
        {
            bool mustCraft = remainingAfter <= 1; // 再不出就没机会了
            if (mustCraft) { return state.craftKind; }
            weights.Add(new KeyValuePair<StageType, float>(state.craftKind, 0.12f + stageIndex * 0.04f));
        }

        // 战斗关兜底：精英越往后越多
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

    /// <summary>
    /// 给轮盘用的滚动条内容：随机填一串候选，最后一格放真正抽到的结果。
    /// 候选只从「这一关有可能出现的类型」里挑，避免玩家看到不可能出现的关卡。
    /// </summary>
    public static List<StageType> BuildReel(ChapterRollState state, int stageIndex, int totalStages,
        StageType winner, int length)
    {
        var pool = CandidatePool(state, stageIndex, totalStages);
        var reel = new List<StageType>(Mathf.Max(4, length));
        for (int i = 0; i < length; i++)
        {
            StageType t = pool[Random.Range(0, pool.Count)];
            // 别连着出两个一样的，滚起来才有变化感
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
        if (!state.craftUsed && state.combatStagesDone >= MinCombatBeforeCraft) pool.Add(state.craftKind);
        if (!state.bossPlaced && stageIndex >= last - (BossWindow - 1)) pool.Add(StageType.Boss);
        return pool;
    }

    /// <summary>
    /// 恢复关给的强化材料：越往后越多。
    /// </summary>
    public static int RestMaterialReward(int stageIndex, int chapter)
    {
        int baseAmount = 2 + stageIndex; // 第 1 关 2 个，第 10 关 11 个
        float chapterMul = 1f + 0.25f * Mathf.Max(0, chapter - 1);
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * chapterMul));
    }

    public static string Label(StageType t)
    {
        switch (t)
        {
            case StageType.Normal: return "普通关卡";
            case StageType.Elite: return "精英关卡";
            case StageType.Rest: return "恢复关卡";
            case StageType.Forge: return "锻造关卡";
            case StageType.Enchant: return "附魔关卡";
            case StageType.Boss: return "首领关卡";
            case StageType.Merchant: return "商人关卡";
            case StageType.Curse: return "诅咒关卡";
            default: return "未知关卡";
        }
    }

    public static string Desc(StageType t, int stageIndex, int chapter)
    {
        switch (t)
        {
            case StageType.Normal: return "常规敌人，稳定掉落";
            case StageType.Elite: return "精英敌人，装备品质更高";
            case StageType.Rest: return "回复生命 50%";
            case StageType.Forge: return "打造 / 强化一件装备";
            case StageType.Enchant: return "为装备附加随机词条";
            case StageType.Boss: return "章节首领，必掉高品质装备";
            case StageType.Merchant: return "花金币购买装备";
            case StageType.Curse: return "三选一，高风险高收益";
            default: return string.Empty;
        }
    }

    /// <summary>关卡卡面主色：没有美术资源时靠颜色区分</summary>
    public static Color Tint(StageType t)
    {
        switch (t)
        {
            case StageType.Normal: return new Color(0.32f, 0.42f, 0.60f);
            case StageType.Elite: return new Color(0.55f, 0.34f, 0.70f);
            case StageType.Rest: return new Color(0.30f, 0.58f, 0.36f);
            case StageType.Forge: return new Color(0.66f, 0.44f, 0.20f);
            case StageType.Enchant: return new Color(0.28f, 0.52f, 0.66f);
            case StageType.Boss: return new Color(0.68f, 0.22f, 0.22f);
            case StageType.Merchant: return new Color(0.62f, 0.56f, 0.24f);
            case StageType.Curse: return new Color(0.40f, 0.24f, 0.48f);
            default: return new Color(0.35f, 0.35f, 0.35f);
        }
    }

    /// <summary>Resources 里对应的图标名（放 Art/UI/StageIcons/ 下同名图即可自动用上）</summary>
    public static string IconName(StageType t)
    {
        switch (t)
        {
            case StageType.Normal: return "stage_normal";
            case StageType.Elite: return "stage_elite";
            case StageType.Rest: return "stage_rest";
            case StageType.Forge: return "stage_forge";
            case StageType.Enchant: return "stage_enchant";
            case StageType.Boss: return "stage_boss";
            case StageType.Merchant: return "stage_merchant";
            case StageType.Curse: return "stage_curse";
            default: return "stage_normal";
        }
    }
}
