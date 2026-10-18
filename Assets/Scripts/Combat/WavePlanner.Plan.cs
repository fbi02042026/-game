using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：铺波与关卡事件（普通/精英/Boss 关铺波、压力阀、事件层 V1.0 及结算/清理）
public partial class WavePlanner
{

    // ---- extracted from BattleManager L1805-L1975 ----
    internal void SetupNormalWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: false);
    }

    /// <summary>
    /// 精英关：同随机总量；属性倍率在 SpawnWave 里乘 ELITE_SCALE。
    /// </summary>
    internal void SetupEliteWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: true);
    }

    void BuildCombatWaves(int stageIdx, bool elite)
    {
        // 无限裂隙：怪量随绝对层数涨（映射进主线怪量曲线输入域），关内序号恒 0 不能反映层数
        if (BattleManager.Instance != null && BattleManager.Instance.IsEndlessRift)
            stageIdx = GameConfig.EndlessMonsterCountStageIdx(BattleManager.Instance.EndlessRiftStage);

        _lastWaveSprites.Clear();
        float startX = GetStageStartX();
        var points = GetSpawnPointsSortedByX();
        var usable = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].position.x >= startX - 2f)
                usable.Add(points[i]);
        }

        StageType spawnType = elite ? StageType.Elite : StageType.Normal;

        // —— V3.0：模式只给「原型池 / 波数范围 / 人数倍率」，每一波在池里现抽 ——
        // 关卡可以重复出现，但同一模式的两次进关，波次组成与波次种类都不一样。
        // 抽不到（没配表 / 该章没候选）就整段跳过，行为与 V3.0 之前完全一致。
        var mode = StageModeTable.Draw(CurrentChapter, stageIdx, spawnType);
        int total;
        int waveCount;
        int[] perWave;
        List<string> archIds = null;
        _stageModeName = "";
        _stageModeTelegraph = "";
        if (mode != null && WaveArchetypeTable.HasData && mode.HasPool)
        {
            // 波数上限 = 刷怪点数量（多出来的波会共用一个 anchor，触发 X 撞车）
            int hardMax = usable.Count > 0 ? Mathf.Clamp(usable.Count, 1, GameConfig.STAGE_WAVE_MAX) : 0;
            waveCount = StageModeTable.RollWaveCount(mode, ComputeWaveBonus(mode), hardMax);

            // 逐波摇原型（受 maxRun 软约束），同一个模式也摇不出一模一样的序列
            archIds = new List<string>(waveCount);
            for (int i = 0; i < waveCount; i++)
                archIds.Add(StageModeTable.RollArchetype(mode, archIds));

            // 每波人数独立摇，总怪量 = Σ，不再先定总量再切分（避免被 cap 截断）
            perWave = new int[waveCount];
            total = 0;
            for (int i = 0; i < waveCount; i++)
            {
                perWave[i] = RollWaveMonsterCount(archIds[i], mode, stageIdx);
                total += perWave[i];
            }
            _stageModeName = mode.name;
            _stageModeTelegraph = mode.telegraph;
        }
        else
        {
            total = elite
                ? GameConfig.GetEliteStageMonsterTotal(stageIdx)
                : GameConfig.GetNormalStageMonsterTotal(stageIdx);
            int waveCountMin = GameConfig.STAGE_WAVE_MIN;
            int waveCountMax = GameConfig.STAGE_WAVE_MAX;
            if (StageSpawnTable.TryResolve(CurrentChapter, stageIdx, spawnType, out var spawnRule))
            {
                if (!spawnRule.useFormulaForTotal && spawnRule.monsterTotal > 0)
                    total = spawnRule.monsterTotal;
                waveCountMin = Mathf.Max(1, spawnRule.waveCountMin);
                waveCountMax = Mathf.Max(waveCountMin, spawnRule.waveCountMax);
            }
            waveCount = GameConfig.GetSuggestedWaveCount(total, usable.Count);
            waveCount = Mathf.Clamp(waveCount, waveCountMin, waveCountMax);
            perWave = GameConfig.DistributeMonstersToWaves(total, waveCount);
        }

        // 精英关本波精英追加额（2026-10-14 主人拍板口径，见下方 elite 段）
        int eliteWaveBonus = 0;
        for (int i = 0; i < waveCount; i++)
        {
            if (elite)
            {
                // 【2026-10-14 主人拍板】精英关每波 = 普通怪 + 2~4 只精英，不再是整波全精英：
                //   ① 每波下限抬到 3 只，保证「2 精英 + 1 普通」成立；
                //   ② 总精英数在 2~4 里摇（多点少点随缘，不固定），原型自带精英（A06/A07 的 elite=1）
                //      算在这个总盘子里，不叠加；
                //   ③ 真正「哪些槽位是精英」仍走 CoSpawnWaveMonsters 的 eliteQuota 一处真源。
                perWave[i] = Mathf.Max(3, perWave[i]);
                int archElite = 0;
                if (archIds != null && i < archIds.Count)
                {
                    var a0 = WaveArchetypeTable.Get(archIds[i]);
                    if (a0 != null) archElite = a0.elite;
                }
                int wantElite = Mathf.Clamp(UnityEngine.Random.Range(2, 5), 1, perWave[i] - 1);
                eliteWaveBonus = Mathf.Max(0, wantElite - archElite);
            }
            Transform anchor = (usable.Count > 0) ? usable[Mathf.Min(i, usable.Count - 1)] : null;
            float triggerX;
            if (anchor != null)
                triggerX = anchor.position.x;
            else
                triggerX = startX + 3.5f + i * GameConfig.VIRTUAL_WAVE_SPACING;

            var wave = new WaveData
            {
                triggerX = triggerX,
                spawnAnchor = anchor,
                monsterCount = perWave[i],
                isBossWave = false,
                spawned = false,
                aliveCount = 0
            };
            // 原型接管进场方向 / 出怪间隔 / 人数上限 / 近远编成 / 精英额度
            ApplyArchetypeToWave(wave, archIds != null ? archIds[i] : "");
            // 【2026-10-14 主人拍板】精英关每波 2~4 只精英（旧值固定 +1，且 SpawnWave 曾整波乘精英缩放）
            if (elite) wave.eliteBonus = eliteWaveBonus;
            _waves.Add(wave);
        }

        PlanStageEvents();
        _totalWaves = _waves.Count;
        string tag = elite ? "精英关" : "普通关";
        string modeTag = string.IsNullOrEmpty(_stageModeName) ? "无模式(旧逻辑)" : $"模式={_stageModeName}[{_stageModeTelegraph}]";
        if (elite)
        {
            // 每波下限抬到 3 只后重算总怪量，保持日志与实际刷怪一致
            int sum = 0;
            for (int i = 0; i < perWave.Length; i++) sum += perWave[i];
            total = sum;
        }
        GamePerf.Log($"[BattleManager] {tag} stage={stageIdx + 1} {modeTag} 总怪={total} → {_totalWaves}波 [{string.Join(",", perWave)}] 刷怪点={usable.Count} 起点={startX:F1}");
        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];
            string an = w.spawnAnchor != null ? w.spawnAnchor.name : "virtual";
            float ax = w.spawnAnchor != null ? w.spawnAnchor.position.x : w.triggerX;
            string archTag = "";
            if (!string.IsNullOrEmpty(w.archetypeId))
            {
                var a = w.Archetype;
                string enter = a != null ? a.enter.ToString() : "?";
                archTag = $" 原型={w.archetypeId}/{enter}/stagger={w.staggerOverride:F2}";
            }
            string eliteTag = w.eliteBonus > 0 ? $" 精英追加={w.eliteBonus}" : "";
            GamePerf.Log($"[BattleManager]   第{i + 1}波 anchor={an} worldX={ax:F2} count={w.monsterCount}{archTag}{eliteTag}");
        }
    }

    /// <summary>把模式的原型序列铺成 waveCount 长；序列比波数短则循环，长则截断。</summary>
    // ===== V3.0：随机波次（模式可重复，波次永不重复）=====

    int _pressureStreak;
    bool _lastStageDied;

    /// <summary>
    /// 战斗结果回报（压力阀输入）。died = 本关死过；hpRatio = 通关时剩余血量比例。
    /// 由 BattleManager 在通关结算时调用；未接线时压力阀恒为 0，行为与 V3.0 之前一致。
    /// </summary>
    internal void NotifyStageResult(bool died, float hpRatio)
    {
        _lastStageDied = died;
        if (died) { _pressureStreak = 0; return; }
        _pressureStreak = hpRatio > GameConfig.EnemyTuning.PressureHpRatio ? _pressureStreak + 1 : 0;
    }

    /// <summary>换章时清零压力阀。</summary>
    internal void ResetPressure()
    {
        _pressureStreak = 0;
        _lastStageDied = false;
    }

    // ============================================================
    // 关卡事件层 V1.0（A 部分）：波与波之间随机插入 1~2 个有取舍的事件
    // —— 完全「额外」：关闭 StageEventTable.EventsEnabled / 第 1~2 章 / 无候选 时一律不触发，
    //    行为与接入前完全一致（不碰战斗核心公式、不改默认人数/血量缩放）。
    // ============================================================
    int _pendingStones;            // 精英小队累计的强化石，通关时发放
    bool _hazardActive;
    float _hazardMoveBase;

    /// <summary>
    /// 本关波次铺好后调用：按章概率抽 0~2 个事件挂到「非首波、非 Boss 波」上。
    /// 第 1~2 章直接拦截（硬约束）。关掉事件层 / 缺表 时整段跳过。
    /// </summary>
    internal void PlanStageEvents()
    {
        // 每关重置事件层累计状态，保证「关掉即与改动前一致」
        _pendingStones = 0;
        _hazardActive = false;
        if (bm != null) bm.stageEventGoldMul = 1f;

        if (!StageEventTable.EventsEnabled) return;
        if (CurrentChapter <= 2) return;            // 硬约束：保护新手期手感
        if (_waves == null || _waves.Count == 0) return;

        // 候选波：非 Boss、且不是第一波（事件插在波与波之间）
        var candidate = new List<int>();
        for (int i = 0; i < _waves.Count; i++)
        {
            if (_waves[i] != null && !_waves[i].isBossWave && i > 0)
                candidate.Add(i);
        }
        if (candidate.Count == 0) return;

        int want = RollEventCount(CurrentChapter);
        if (want <= 0) return;

        // 洗牌候选，避免事件总挂在固定波
        for (int i = candidate.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int t = candidate[i]; candidate[i] = candidate[j]; candidate[j] = t;
        }

        int applied = 0;
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        foreach (int wi in candidate)
        {
            if (applied >= want) break;
            var ev = StageEventTable.Draw(CurrentChapter, stageIdx);
            if (ev == null) continue;
            ApplyStageEvent(ev, _waves[wi]);
            applied++;
        }
        if (applied > 0)
            GamePerf.Log($"[StageEvent] 本关插入 {applied} 个事件（章{CurrentChapter} 目标{want}）");
    }

    /// <summary>单关事件数：第 3 章起概率渐增，最多 2 个；1~2 章恒 0。</summary>
    int RollEventCount(int chapter)
    {
        if (chapter <= 2) return 0;
        // 至少一个事件的概率随章上升：ch3≈20% → ch7+≈60%
        float p1 = Mathf.Clamp(0.20f + (chapter - 3) * 0.10f, 0.20f, 0.60f);
        if (UnityEngine.Random.value > p1) return 0;
        // 第二个事件概率较低：ch3≈5% → ch7+≈35%
        float p2 = Mathf.Clamp(0.05f + (chapter - 3) * 0.075f, 0.05f, 0.35f);
        return UnityEngine.Random.value < p2 ? 2 : 1;
    }

    /// <summary>把一个事件挂到目标波上，落实其「预效果」参数。</summary>
    void ApplyStageEvent(StageEventTable.EventDef ev, WaveData wave)
    {
        if (ev == null || wave == null) return;
        wave.stageEventId = ev.id;
        wave.stageEventTelegraph = ev.telegraph;

        switch (ev.effectType)
        {
            case StageEventTable.EffectType.REINFORCE:
                // 风险换收益：下一波怪数 +30%，本关金币 +20%
                wave.monsterCount = Mathf.Max(1, Mathf.CeilToInt(wave.monsterCount * ev.countMul));
                if (bm != null) bm.stageEventGoldMul *= ev.goldMul;
                break;
            case StageEventTable.EffectType.ELITE:
                // 难度换材料：下一波多 1 名精英，通关发强化石
                wave.eliteBonus += Mathf.Max(0, ev.eliteAdd);
                _pendingStones += Mathf.Max(0, ev.stone);
                break;
            case StageEventTable.EffectType.SUPPLY:
                // 纯喘息（稀有）：出兵前全队回血，参数记下、spawn 时结算
                wave.stageEventHealPct = ev.healPct;
                break;
            case StageEventTable.EffectType.HAZARD:
                // 纯压力：英雄移速惩罚，通关给高金币补偿
                wave.stageEventMovePenalty = Mathf.Clamp01(ev.movePenalty);
                if (bm != null) bm.stageEventGoldMul *= ev.goldMul;
                break;
        }
    }

    /// <summary>出兵前结算本波的「补给」回血（全队）。</summary>
    void HealTeamOnEvent(WaveData wave)
    {
        float pct = wave.stageEventHealPct;
        if (pct <= 0f) return;
        HealUnitPct(hero, pct);
        if (MercenaryManager.Instance != null)
        {
            var mercs = MercenaryManager.Instance.GetActiveMercs();
            if (mercs != null)
                for (int i = 0; i < mercs.Count; i++)
                    HealUnitPct(mercs[i], pct);
        }
        if (!string.IsNullOrEmpty(wave.stageEventTelegraph))
            GlobalToastUI.Show(wave.stageEventTelegraph);
    }

    void HealUnitPct(UnitBase u, float pct)
    {
        if (u == null || u.attr == null) return;
        float maxHp = u.attr.GetAttr(AttrType.MaxHp);
        if (maxHp <= 0f) return;
        u.currentHp = Mathf.Min(maxHp, u.currentHp + maxHp * pct);
    }

    /// <summary>出兵时给英雄挂移速惩罚（环境灾害）。先清上一段残留。</summary>
    void ApplyHazardPenalty(WaveData wave)
    {
        RestoreHazardPenalty();
        if (wave.stageEventMovePenalty <= 0f) return;
        if (hero != null && hero.attr != null)
        {
            _hazardMoveBase = hero.attr.GetAttr(AttrType.MoveSpeed);
            hero.attr.SetAttr(AttrType.MoveSpeed, _hazardMoveBase * (1f - wave.stageEventMovePenalty));
            _hazardActive = true;
        }
        if (!string.IsNullOrEmpty(wave.stageEventTelegraph))
            GlobalToastUI.Show(wave.stageEventTelegraph);
    }

    /// <summary>恢复英雄移速（下一波出兵前 / 通关时调用）。</summary>
    void RestoreHazardPenalty()
    {
        if (_hazardActive && hero != null && hero.attr != null)
            hero.attr.SetAttr(AttrType.MoveSpeed, _hazardMoveBase);
        _hazardActive = false;
    }

    /// <summary>通关结算：发放精英小队累计的强化石。</summary>
    internal void GrantStageEventRewards()
    {
        if (_pendingStones > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.EnchantStone, _pendingStones, save: false, notify: true);
            _pendingStones = 0;
        }
        RestoreHazardPenalty();
    }

    /// <summary>换关 / 通关 / 事件层关闭时清场，保证不泄漏状态到下一关。</summary>
    internal void ClearStageEventState()
    {
        _pendingStones = 0;
        RestoreHazardPenalty();
        // Boss 关变体的涌怪协程：换关 / 通关时一并停掉，避免泄漏到下一关
        StopCoroutine(_bossPressureCo);
        _bossPressureCo = null;
        if (bm != null) bm.stageEventGoldMul = 1f;
    }

    /// <summary>Boss关：若干波小怪 + 最后 1 波 Boss</summary>
    internal void SetupBossWave(int stageIdx)
    {
        StopCoroutine(_bossPressureCo);
        _bossPressureCo = null;

        float startX = GetStageStartX();
        int minions = GameConfig.GetBossStageMinionTotal(stageIdx);
        int bossCount = GameConfig.GetBossStageMonsterTotal();

        // Boss 关小怪波：按总数抬高波次上限，最多 7 波小怪 + 1 Boss
        int minionWaves = GameConfig.GetSuggestedWaveCount(minions, 0);
        minionWaves = Mathf.Clamp(minionWaves, 3, 7);
        int[] perWave = GameConfig.DistributeMonstersToWaves(minions, minionWaves);

        // —— 2026-09-18：Boss 关的小怪波原来固定「右侧进场 + 均分」，每波长得一模一样。
        // 这里让小怪波也进模式池逐波摇原型（Boss 本体那波不动）。
        // 抽不到模式/原型时行为与改动前完全一致（ApplyArchetypeToWave 对空 id 直接返回）。
        var mode = StageModeTable.Draw(CurrentChapter, stageIdx, StageType.Normal);
        List<string> archIds = null;
        if (mode != null && WaveArchetypeTable.HasData && mode.HasPool)
        {
            archIds = new List<string>(minionWaves);
            for (int i = 0; i < minionWaves; i++)
                archIds.Add(StageModeTable.RollArchetype(mode, archIds));
            _stageModeName = mode.name;
            _stageModeTelegraph = mode.telegraph;
        }

        for (int i = 0; i < minionWaves; i++)
        {
            var wave = new WaveData
            {
                triggerX = startX + 3.5f + i * GameConfig.VIRTUAL_WAVE_SPACING,
                spawnAnchor = null,
                monsterCount = perWave[i],
                isBossWave = false,
                spawned = false,
                aliveCount = 0
            };
            if (archIds != null) ApplyArchetypeToWave(wave, archIds[i]);
            _waves.Add(wave);
        }

        // —— Boss 关变体 V1.0：最后一波不再是「永远孤零零一个 Boss」。
        // 只加两类外挂参数：GUARD 亲卫（占本波最前 N 个名额）/ TIMED 软性限时涌怪（不判负，只加压）。
        // 缺表 / 关闭 / 抽不到时 variant = null → 与接入前完全一致。
        var variant = BossStageVariantTable.Draw(CurrentChapter);
        int guardCount = 0;
        float pressureDelay = 0f;
        float pressureInterval = 0f;
        int pressureCap = 0;
        string variantId = "";
        string variantTelegraph = "";
        if (variant != null)
        {
            variantId = variant.id;
            variantTelegraph = variant.telegraph;
            if (variant.effectType == BossStageVariantTable.EffectType.GUARD)
                guardCount = Mathf.Max(0, variant.guardCount);
            else if (variant.effectType == BossStageVariantTable.EffectType.TIMED)
            {
                pressureDelay = variant.delaySec;
                pressureInterval = variant.intervalSec;
                pressureCap = Mathf.Max(0, variant.cap);
            }
        }

        float bossX = endPoint != null ? endPoint.position.x - 2f : startX + 3.5f + minionWaves * GameConfig.VIRTUAL_WAVE_SPACING + 2f;
        _waves.Add(new WaveData
        {
            triggerX = bossX - 1f,
            monsterCount = bossCount + guardCount,
            isBossWave = true,
            spawned = false,
            aliveCount = 0,
            bossVariantId = variantId,
            bossVariantTelegraph = variantTelegraph,
            bossGuardCount = guardCount,
            bossPressureDelay = pressureDelay,
            bossPressureInterval = pressureInterval,
            bossPressureCap = pressureCap
        });
        PlanStageEvents();
        _totalWaves = _waves.Count;
        string bossModeTag = string.IsNullOrEmpty(_stageModeName) ? "无模式(旧逻辑)" : $"模式={_stageModeName}";
        string bossVariantTag = string.IsNullOrEmpty(variantId)
            ? "无变体(旧逻辑)"
            : $"变体={variantId}(亲卫{guardCount}/涌怪{pressureCap}@{pressureDelay:F0}s)";
        GamePerf.Log($"[BattleManager] Boss关 stage={stageIdx + 1} {bossModeTag} {bossVariantTag} " +
                     $"小怪={minions}×{minionWaves}波 + Boss={bossCount} X={bossX:F1}");
    }
}
