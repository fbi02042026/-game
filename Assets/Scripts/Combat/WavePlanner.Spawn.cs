using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：出怪小件（埋伏/围殴入口、屏外进场、交战点同侧解算、引导调血）
public partial class WavePlanner
{

    // ---- extracted from BattleManager L736-L852 ----
    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。走 SpawnWave 参数，不再另开刷怪轨。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        if (victim == null) return;
        float vx = UnitBase.GetCombatX(victim);
        QueueTutorialWaveCore(Mathf.Max(1, count), vx, victim, true, true, 0f);
    }

    /// <summary>诱饵埋伏：从锚点左右两侧刷怪。走 SpawnWave 参数（bilateralEnter）。</summary>
    public void SpawnTutorialFlankAmbush(int count, float? anchorX = null)
    {
        if (hero == null) return;
        QueueTutorialWaveCore(Mathf.Max(2, count), anchorX ?? UnitBase.GetCombatX(hero), hero, true, false, 0f);
    }

    /// <summary>兜底：在锚点左右对称刷怪（避免只刷右侧）。</summary>
    void EmergencySpawnFlankAmbush(int count, float anchorX)
    {
        if (hero == null) return;
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        GetBattleVisibleX(out float visMin, out float visMax);
        float engageCenter = Mathf.Clamp(anchorX, visMin + 1f, visMax - 1f);
        int n = Mathf.Max(2, count);
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        var usedSprites = new System.Collections.Generic.HashSet<int>();
        var usedLanes = new System.Collections.Generic.List<float>();
        for (int i = 0; i < n; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;
            float ox = engageCenter + side * Random.Range(1.0f, 2.8f) + Random.Range(-0.4f, 0.4f);
            float lane = BattleLaneBounds.PickSpreadLane(usedLanes);
            usedLanes.Add(lane);
            Vector3 engagePos = new Vector3(ox, UnitBase.GROUND_Y + lane, z);
            if (!TryPickWaveMonster(stageIdx, monsters.Count + i, false, usedSprites, out MonsterConfig cfg, out int spriteIdx))
                continue;
            bool fromLeft = engagePos.x < engageCenter;
            Monster monster = SpawnMonsterOffscreenEnter(engagePos, lane, 1f, cfg, stageIdx, spriteIdx, null, fromLeft);
            if (monster == null) continue;
            monster.SetForcedTarget(hero);
            ApplyTutorialMonsterTuning(monster);
            ForceEnableMonsterRenderers(monster.transform);
        }
    }

    /// <summary>埋伏/教程：在交战点刷怪，从屏外走入。</summary>
    Monster SpawnAmbushMonsterAt(
        Vector3 engagePos, float laneY, float scaleMultiplier = 1f,
        System.Collections.Generic.HashSet<int> usedSprites = null)
    {
        EnsureMonsterPrefabReady();
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        if (!TryPickWaveMonster(stageIdx, usedSprites != null ? usedSprites.Count : monsters.Count, false, usedSprites,
                out MonsterConfig cfg, out int spriteIdx))
            return null;

        // 宝箱/佣兵伏击：落点在左则从左进场，在右则从右进场（普通波仍默认右侧）
        float heroX = hero != null ? UnitBase.GetCombatX(hero) : engagePos.x;
        bool fromLeft = engagePos.x < heroX;
        Monster monster = SpawnMonsterOffscreenEnter(
            engagePos, laneY, scaleMultiplier, cfg, stageIdx, spriteIdx, null, fromLeft);
        if (monster == null) return null;
        ApplyTutorialMonsterTuning(monster);
        ForceEnableMonsterRenderers(monster.transform);
        return monster;
    }

    /// <param name="enterDepthOffset">
    /// 【2026-10-05 主人拍板「多点出生点」】本只在同波里的序号 × MONSTER_ENTER_DEPTH_STEP：
    /// 入场起点按序号往外错开，一整波从远近不同的位置一起涌进来，而不是全挤在屏外同一个点。
    /// 传 0 = 退回原来的单点进场（单只补刷 / 兜底路径用）。
    /// </param>
    Monster SpawnMonsterOffscreenEnter(
        Vector3 engagePos, float laneY, float scaleMultiplier,
        MonsterConfig cfg, int stageIdx, int spriteIdx, UnitBase forcedTarget = null,
        bool? fromLeftOverride = null, float enterDepthOffset = 0f)
    {
        if (cfg == null) return null;
        float z = unitRoot != null ? unitRoot.position.z : engagePos.z;
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        // 相对镜头左右交替进场（可覆盖）
        bool fromLeft = fromLeftOverride ?? false; // 默认右侧进场；仅显式 override 才从左
        // 【2026-10-22 主人拍板「出怪有点慢，把出生点提前点」】1.35 → 0.8：
        // GetBattleVisibleX(0.35) 已向屏内收了 0.35，实际出生点距真屏幕边约 1.15（原 1.7），
        // 不会穿帮；配合 MONSTER_SPAWN_DEPTH_TIERS 一起收紧，最近/最远都提前约 0.8~1.2 秒到场。
        const float offscreenMargin = 0.8f;
        float depth = offscreenMargin + Mathf.Max(0f, enterDepthOffset);
        float enterX = fromLeft ? visMin - depth : visMax + depth;
        // 交战点落在进场同侧，避免左侧怪跑到玩家右边再回头
        float heroX = hero != null ? UnitBase.GetCombatX(hero) : engagePos.x;
        engagePos.x = ResolveEngageXForEnterSide(heroX, fromLeft, engagePos.x, visMin, visMax);
        engagePos.y = UnitBase.GROUND_Y + laneY;
        Vector3 enterFrom = new Vector3(enterX, engagePos.y, z);

        Monster monster = SpawnMonster(cfg, stageIdx, enterFrom, scaleMultiplier, spriteIdx);
        if (monster == null) return null;
        monster.SetLaneY(laneY);
        if (forcedTarget != null)
            monster.SetForcedTarget(forcedTarget);
        monster.BeginMapEnter(engagePos, fromLeft ? 1 : -1);
        return monster;
    }

    /// <summary>
    /// 左进场停在英雄左侧，右进场停在英雄右侧；preferredX 仅作同侧微调参考。
    /// </summary>
    float ResolveEngageXForEnterSide(float heroX, bool fromLeft, float preferredX, float visMin, float visMax)
    {
        float ahead = Rules.ResolveEngageAhead(GameConfig.MONSTER_ENGAGE_OFFSET);
        float sideX = fromLeft ? heroX - ahead : heroX + ahead;
        // 同侧优先用 preferred（波次间距），但不得跨过英雄
        if (fromLeft)
        {
            if (preferredX < heroX - 0.6f)
                sideX = preferredX;
            sideX = Mathf.Min(sideX, heroX - 0.85f);
        }
        else
        {
            if (preferredX > heroX + 0.6f)
                sideX = preferredX;
            sideX = Mathf.Max(sideX, heroX + 0.85f);
        }
        return Mathf.Clamp(sideX, visMin + 0.45f, visMax - 0.45f);
    }

    // ---- extracted from BattleManager L876-L910 ----
    void ApplyTutorialMonsterTuning(Monster monster)
    {
        if (!Rules.ApplyTableMonsterHp || !_tutorialHpFromTable || monster == null || monster.attr == null)
            return;

        float min;
        float max;
        if (monster.IsEliteWave)
        {
            min = _tutorialEliteHpMin;
            max = _tutorialEliteHpMax;
        }
        else if (monster.IsRangedStyle && (_tutorialRangedHpMin > 0f || _tutorialRangedHpMax > 0f))
        {
            // 远程怪单独降血：与近战同血会让「远程更脆」的手感不成立（近战 3~4 刀 / 远程 2~3 刀）。
            min = _tutorialRangedHpMin;
            max = _tutorialRangedHpMax;
        }
        else
        {
            min = _tutorialHpMin;
            max = _tutorialHpMax;
        }
        if (min <= 0f && max <= 0f) return;
        if (max <= 0f) max = min;
        if (min <= 0f) min = max;

        // 整型档沿用旧 Random.Range(minInclusive, maxExclusive)；否则闭区间 float。
        float rolled;
        bool intRange = Mathf.Approximately(min, Mathf.Round(min)) && Mathf.Approximately(max, Mathf.Round(max));
        if (intRange && max > min)
            rolled = Random.Range((int)min, (int)max);
        else if (Mathf.Approximately(min, max))
            rolled = min;
        else
            rolled = Random.Range(min, max);

        float hp = rolled * GameConfig.MONSTER_HP_GLOBAL_MUL;
        monster.attr.SetAttr(AttrType.MaxHp, hp);
        monster.currentHp = hp;
        // [BALANCE-TEMP] 教学关血量覆盖后的真实满血（一眼看出是不是被压到个位数）
        string cfgId = monster.config != null ? monster.config.id : "-";
        float cfgBaseHp = monster.config != null ? monster.config.baseHp : -1f;
        Debug.Log($"[BALANCE] mon={monster.name} cfg={cfgId} cfgBaseHp={cfgBaseHp} " +
                  $"elite={monster.IsEliteWave} rolled={rolled} MUL={GameConfig.MONSTER_HP_GLOBAL_MUL} " +
                  $"finalMaxHp={hp:F1}");
    }

    // ============================================================
    // 【2026-10-20 主人拍板】多出生点随机抽
    // 车道(MONSTER_SPAWN_LANE_SLOTS=5) × 深度档(3) = 15 个候选，洗牌后逐只取用，
    // 替掉原来「按序号等距铺车道」的死板排布。进场侧【2026-10-20 主人拍板】恒为右侧，不再左右随机。
    // ============================================================

    /// <summary>
    /// 【2026-10-20 主人拍板】「车道过密放宽间距」警告的节流标记：<b>每波只打一条</b>。
    /// 一波最多 5 只怪、可行走区才 0.975 高，几乎每波都会触发，不节流会刷屏。
    /// 在 <see cref="BuildSpawnPointPool"/> 里重置。
    /// </summary>
    bool _spawnLaneWarned;

    /// <summary>一个候选出生点：车道 Y + 进场深度档（进场侧恒为右侧，不再逐点带）。</summary>
    struct SpawnPointSlot
    {
        public float laneY;
        public float depth;
    }

    /// <summary>
    /// 生成本波的出生点候选池并洗牌（Fisher-Yates）。
    /// 车道 Y 一律来自 <see cref="BattleLaneBounds.GetLaneSlots"/>（可行走区唯一出口）。
    /// </summary>
    List<SpawnPointSlot> BuildSpawnPointPool()
    {
        _spawnLaneWarned = false;
        var lanes = new List<float>(GameConfig.MONSTER_SPAWN_LANE_SLOTS);
        BattleLaneBounds.GetLaneSlots(GameConfig.MONSTER_SPAWN_LANE_SLOTS, lanes,
            GameConfig.MONSTER_SPAWN_LANE_EDGE_PAD);

        var tiers = GameConfig.MONSTER_SPAWN_DEPTH_TIERS;
        var pool = new List<SpawnPointSlot>(lanes.Count * (tiers != null ? tiers.Length : 1));
        if (tiers == null) return pool;

        for (int l = 0; l < lanes.Count; l++)
        {
            for (int d = 0; d < tiers.Length; d++)
            {
                pool.Add(new SpawnPointSlot
                {
                    laneY = lanes[l],
                    depth = tiers[d],
                });
            }
        }

        // Fisher-Yates 洗牌（UnityEngine.Random.Range(int,int) 上界不含）
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }
        return pool;
    }

    /// <summary>
    /// 从候选池里取一个出生点并移出池子。要求车道与 <paramref name="usedLanes"/> 里
    /// 每一条已用车道的距离都 ≥ <see cref="BattleLaneBounds.MinLaneGap"/>。
    /// <para>两遍扫描：第一遍严格校验；找不到说明剩余候选全挤在已用车道附近，
    /// 第二遍放宽间距直接取用（宁可轻微重叠也不能不刷怪），并打一条 LogWarning（每波一条）。</para>
    /// <para>池空 → 返回 false（调用方兜底）。</para>
    /// </summary>
    bool TryTakeSpawnPoint(List<SpawnPointSlot> pool,
                           System.Collections.Generic.IList<float> usedLanes, out SpawnPointSlot sp)
    {
        if (pool != null)
        {
            // 第一遍：车道间距满足
            for (int i = 0; i < pool.Count; i++)
            {
                var cand = pool[i];
                bool farEnough = true;
                if (usedLanes != null)
                {
                    for (int k = 0; k < usedLanes.Count; k++)
                    {
                        if (Mathf.Abs(cand.laneY - usedLanes[k]) < BattleLaneBounds.MinLaneGap)
                        {
                            farEnough = false;
                            break;
                        }
                    }
                }
                if (!farEnough) continue;
                pool.RemoveAt(i);
                sp = cand;
                return true;
            }

            // 第二遍：剩余候选车道过密，放宽间距直接取用（不刷怪比轻微重叠更糟）
            for (int i = 0; i < pool.Count; i++)
            {
                var cand = pool[i];
                if (!_spawnLaneWarned)
                {
                    _spawnLaneWarned = true;
                    Debug.LogWarning($"[WavePlanner] 出生点池剩余候选车道过密，放宽间距（laneY={cand.laneY:F2}）");
                }
                pool.RemoveAt(i);
                sp = cand;
                return true;
            }
        }
        sp = default;
        return false;
    }
}
