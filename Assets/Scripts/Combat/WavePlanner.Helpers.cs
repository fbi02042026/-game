using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：规划小工具（波数修正/人数摇取/播报文案/原型应用/权重分波/刷怪点排序）
public partial class WavePlanner
{

    /// <summary>本关波数修正：压力阀 + 职业 × 模式弱势补偿，只调波数、不改数值。</summary>
    int ComputeWaveBonus(StageModeTable.Mode mode)
    {
        int bonus = 0;
        if (GameConfig.EnemyTuning.PressureEnabled)
        {
            if (_lastStageDied)
            {
                if (GameConfig.EnemyTuning.PressureMercyOnDeath) bonus -= 1;
            }
            else if (_pressureStreak >= GameConfig.EnemyTuning.PressureStreakThreshold)
            {
                bonus += Mathf.Min(GameConfig.EnemyTuning.PressureWaveCap,
                    _pressureStreak - GameConfig.EnemyTuning.PressureStreakThreshold + 1);
            }
        }
        if (GameConfig.EnemyTuning.ModeHandicapByJob && mode != null && StageModeTable.IsHardForJob(mode.id, PlayerJobDefs.GetSelected()))
            bonus -= 1;
        return bonus;
    }

    /// <summary>
    /// 每波人数：原型基准 × 模式倍率 × 章节系数 × 关卡进度系数 × 随机浮动，夹到 [countMin, cap]。
    /// 章内随关卡序号<b>严格单调上升</b>（V3.0 修掉了旧公式第 1 关比第 2 关怪多的倒挂）。
    /// </summary>
    int RollWaveMonsterCount(string archetypeId, StageModeTable.Mode mode, int stageIdx)
    {
        var a = WaveArchetypeTable.Get(archetypeId);
        if (a == null) return GameConfig.WAVE_MONSTER_MIN;

        float chapterMul = 1f + GameConfig.EnemyTuning.ChapterCountStep * (Mathf.Max(1, CurrentChapter) - 1);
        float progressMul = 1f + GameConfig.EnemyTuning.StageCountStep * Mathf.Max(0, stageIdx);
        float jitter = UnityEngine.Random.Range(GameConfig.EnemyTuning.CountJitterMin, GameConfig.EnemyTuning.CountJitterMax);
        int count = Mathf.RoundToInt(a.countBase * mode.countMul * chapterMul * progressMul * jitter);
        return Mathf.Clamp(count, a.countMin, Mathf.Max(a.countMin, a.cap));
    }

    /// <summary>V3.0 播报：本波原型的预告文案（读时保障，UI 未接时不报错）。</summary>
    public string WaveTelegraph(int waveIndex)
    {
        var a = GetWaveArchetype(waveIndex);
        return a != null ? a.telegraph : "";
    }

    /// <summary>V3.0 播报：本波原型的应对提示。</summary>
    public string WaveTacticHint(int waveIndex)
    {
        var a = GetWaveArchetype(waveIndex);
        return a != null ? a.tacticHint : "";
    }

    /// <summary>
    /// 波次预告条上那一行：「第N波 · 原型名：播报 — 应对提示」。
    /// 没有原型（旧均分逻辑 / 无表）时普通波返回空（只放预告图），Boss 波至少给「首领」。
    /// </summary>
    public string WaveAnnounceText(int waveIndex)
    {
        if (_waves == null || waveIndex < 0 || waveIndex >= _waves.Count) return "";
        bool boss = _waves[waveIndex] != null && _waves[waveIndex].isBossWave;
        string head = boss ? "首领" : "第" + (waveIndex + 1) + "波";

        var we = _waves[waveIndex];
        string ev = (we != null && !string.IsNullOrEmpty(we.stageEventTelegraph)) ? we.stageEventTelegraph : "";
        string bv = (we != null && !string.IsNullOrEmpty(we.bossVariantTelegraph)) ? we.bossVariantTelegraph : "";

        var a = GetWaveArchetype(waveIndex);
        if (a == null && string.IsNullOrEmpty(ev) && string.IsNullOrEmpty(bv)) return boss ? head : "";

        string line = head;
        if (a != null)
        {
            line += " · " + a.name;
            if (!string.IsNullOrEmpty(a.telegraph)) line += "：" + a.telegraph;
            if (!string.IsNullOrEmpty(a.tacticHint)) line += " — " + a.tacticHint;
        }
        // 事件层预告：紧随波次播报，让玩家提前知道下一波会怎样
        if (!string.IsNullOrEmpty(ev)) line += "  ◆ " + ev;
        // Boss 关变体预告：进 Boss 波前告诉玩家这关的首领「多了点什么」
        if (!string.IsNullOrEmpty(bv)) line += "  ◆ " + bv;
        return line;
    }

    /// <summary>把原型写进 WaveData：进场方向 / 出怪间隔 / 编成标记。</summary>
    static void ApplyArchetypeToWave(WaveData wave, string archetypeId)
    {
        if (wave == null || string.IsNullOrEmpty(archetypeId)) return;
        var a = WaveArchetypeTable.Get(archetypeId);
        if (a == null) return;

        wave.archetypeId = a.id;
        if (a.enter == WaveArchetypeTable.EnterMode.Bilateral)
            wave.bilateralEnter = true;
        else if (a.enter == WaveArchetypeTable.EnterMode.Around)
            wave.aroundAnchor = true;
        // stagger < 0 = 用规则包默认；>= 0 才接管（0 = 同一帧出齐）
        if (a.stagger >= 0f)
            wave.staggerOverride = a.stagger;
    }

    WaveArchetypeTable.Archetype GetWaveArchetype(int waveIndex)
    {
        if (_waves == null || waveIndex < 0 || waveIndex >= _waves.Count) return null;
        var w = _waves[waveIndex];
        return w == null ? null : w.Archetype;
    }

    int GetWaveMonsterCount(int waveIndex)
    {
        if (_waves == null || waveIndex < 0 || waveIndex >= _waves.Count) return 0;
        var w = _waves[waveIndex];
        return w != null ? w.monsterCount : 0;
    }

    /// <summary>
    /// 按原型的 countBase 权重把总怪量分到各波，并夹在每波的 [min(2,cap), cap] 区间内。
    /// 与 <see cref="GameConfig.DistributeMonstersToWaves"/> 的区别：旧版均分，
    /// 这里让「围杀」这种大头波真的吃到大头，模式才有形状。
    /// </summary>
    static int[] DistributeMonstersToWavesWeighted(int total, int[] bases, int[] caps)
    {
        int n = Mathf.Min(bases.Length, caps.Length);
        var counts = new int[n];
        var mins = new int[n];
        var maxs = new int[n];
        int sumMin = 0, sumMax = 0, sumBase = 0;
        for (int i = 0; i < n; i++)
        {
            maxs[i] = Mathf.Max(1, caps[i]);
            mins[i] = Mathf.Min(GameConfig.WAVE_MONSTER_MIN, maxs[i]);
            sumMin += mins[i];
            sumMax += maxs[i];
            sumBase += Mathf.Max(1, bases[i]);
        }
        // 先夹总量，保证一定分得下去（不会死循环）
        total = Mathf.Clamp(total, sumMin, sumMax);

        var rem = new float[n];
        int assigned = 0;
        float extra = total - sumMin;
        for (int i = 0; i < n; i++)
        {
            float want = mins[i] + extra * (Mathf.Max(1, bases[i]) / (float)sumBase);
            int floor = Mathf.FloorToInt(want);
            counts[i] = Mathf.Clamp(floor, mins[i], maxs[i]);
            rem[i] = want - floor;
            assigned += counts[i];
        }
        // 余数从大到小补足（只补还有空间的波）
        for (int guard = 0; assigned < total && guard < 512; guard++)
        {
            int best = -1; float bestRem = -1f;
            for (int i = 0; i < n; i++)
            {
                if (counts[i] >= maxs[i]) continue;
                if (rem[i] > bestRem) { bestRem = rem[i]; best = i; }
            }
            if (best < 0) break;
            counts[best]++; assigned++;
            rem[best] -= 1f; // 补过一轮后降权，避免总砸在同一波
        }
        // 分多了就从余数最小的波上扣（不动已经触底的波）
        for (int guard = 0; assigned > total && guard < 512; guard++)
        {
            int worst = -1; float worstRem = 2f;
            for (int i = 0; i < n; i++)
            {
                if (counts[i] <= mins[i]) continue;
                if (rem[i] < worstRem) { worstRem = rem[i]; worst = i; }
            }
            if (worst < 0) break;
            counts[worst]--; assigned--;
            rem[worst] += 1f;
        }
        return counts;
    }

    List<Transform> GetSpawnPointsSortedByX()
    {
        var list = new List<Transform>();
        if (monsterSpawnPoints == null) return list;
        for (int i = 0; i < monsterSpawnPoints.Length; i++)
        {
            if (monsterSpawnPoints[i] != null)
                list.Add(monsterSpawnPoints[i]);
        }
        // 按 X 排序；同 X 时按编号数字排（避免 MonsterSpawn_15 排在 _2 前面）
        list.Sort((a, b) =>
        {
            int cx = a.position.x.CompareTo(b.position.x);
            if (cx != 0) return cx;
            return ParseSpawnIndex(a.name).CompareTo(ParseSpawnIndex(b.name));
        });
        return list;
    }

    /// <summary>只取起点前方的刷怪点，避免刷在身后导致「看不见怪」</summary>
    List<Transform> GetSpawnPointsAheadOf(float startX)
    {
        var all = GetSpawnPointsSortedByX();
        var ahead = new List<Transform>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].position.x >= startX - 0.5f)
                ahead.Add(all[i]);
        }
        return ahead;
    }

    static int ParseSpawnIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        int us = name.LastIndexOf('_');
        if (us >= 0 && us + 1 < name.Length && int.TryParse(name.Substring(us + 1), out int n))
            return n;
        return 0;
    }

    /// <summary>每波触发线：主角前方；有刷怪点则用点左侧，否则按间距铺开</summary>
    static float ResolveWaveTriggerX(int waveIndex, int waveCount, List<Transform> points, float startX, float endX)
    {
        if (points != null && points.Count > 0)
        {
            if (waveIndex < points.Count)
                return Mathf.Max(startX - 0.5f, points[waveIndex].position.x - 2.5f);

            float lastX = points[points.Count - 1].position.x;
            int extra = waveIndex - points.Count + 1;
            int extraTotal = Mathf.Max(1, waveCount - points.Count);
            float t = extra / (float)(extraTotal + 1);
            return Mathf.Lerp(lastX, endX, t) - 1f;
        }

        // 无可用刷怪点：开局即可触发第一波，怪刷在前方
        return startX - 0.5f + waveIndex * 5f;
    }
}
