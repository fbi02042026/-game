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
    /// 【2026-10-22 主人拍板 v4.1】正式关精英关在 <see cref="BuildCombatWaves"/> 里按波分流：
    /// 除末波外用普通关编成，只有末波（随 v4.2 按章总波数自然后移）用精英编成。
    /// </summary>
    internal void SetupEliteWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: true);
    }

    /// <summary>
    /// 【2026-10-25 主人拍板】正式关每回合波数：全章**定表**（不随机摇）——
    /// ch1=1/2/3、ch2=2/3/4、ch3 起=3/4/5（GameConfig.FormalRoundWaveFixedCounts，公式 = 基数 + 回合下标）。
    /// 返回每回合波数（下标=回合），Σ 即该关小怪总波数；v4.2「按章区间随机摇」口径作废（Min/Max 已删）；
    /// v4.0「FORMAL_STAGE_WAVE_COUNT 定死 9」更早作废（常量保留退役）。
    /// </summary>
    static int[] RollFormalRoundWaveCounts(int chapter)
    {
        int rounds = GameConfig.FORMAL_STAGE_ROUND_COUNT;
        int[] counts = GameConfig.FormalRoundWaveFixedCounts(chapter);
        if (counts.Length != rounds)
            Debug.LogError($"[WavePlanner] 回合波数定表长度 {counts.Length} ≠ 回合数 {rounds}（ch{chapter}），请核对 FormalRoundWaveFixedCounts");
        return counts;
    }

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.2】按每回合波数表铺出回合末标记数组：
    /// 每回合的最后一波（累计下标压线处）置 true，供 WaveData.isRoundEnd 落位；总长 = Σ 波数。
    /// </summary>
    static bool[] FormalRoundEndFlags(int[] roundWaveCounts)
    {
        var flags = new bool[roundWaveCounts.Sum()];
        int acc = 0;
        for (int r = 0; r < roundWaveCounts.Length; r++)
        {
            acc += roundWaveCounts[r];
            flags[acc - 1] = true;
        }
        return flags;
    }

    void BuildCombatWaves(int stageIdx, bool elite)
    {
        // 无限裂隙：怪量随绝对层数涨（映射进主线怪量曲线输入域），关内序号恒 0 不能反映层数
        if (BattleManager.Instance != null && BattleManager.Instance.IsEndlessRift)
            stageIdx = GameConfig.EndlessMonsterCountStageIdx(BattleManager.Instance.EndlessRiftStage);

        _lastWaveSprites.Clear();
        // 【2026-10-26 词缀怪】每关重置吐槽去重：每关每种词缀介绍一次、荆棘吐槽一次
        MonsterAffixBanter.ResetForStage();
        float startX = GetStageStartX();
        var points = GetSpawnPointsSortedByX();
        var usable = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].position.x >= startX - 2f)
                usable.Add(points[i]);
        }

        // 【2026-10-23 主人拍板】活动副本（金币/徽记/卷轴三本共用）定死波次骨架：3 波 × 每波 10 只，按难度分层：
        // 初级(0) 精英 [0,1,2] 无 Boss；中级(1) 每波数量不变、精英每波 +1~2；
        // 高级(2) 每波数量不变、精英每波 80~90%，最后 2 波各带 1 个 Boss。
        // 徽记/卷轴副本波次布局与金币副本完全一致（主人 2026-10-23/10-24 拍板）。中级/高级不再走下方随机路径。
        if (bm != null && bm.IsActivityDungeon)
        {
            BuildActivityDungeonWaves(startX, usable);
            return;
        }

        StageType spawnType = elite ? StageType.Elite : StageType.Normal;

        bool formalMain = bm != null && bm.IsFormalMainRun;
        // 【2026-10-22 主人拍板】正式关（主线 Normal 模式）波数口径：
        // v4.0「定死 9 波」已作废（v4.2）——改按「3 回合 × 每回合按章取数」累加
        // （2026-10-25 主人拍板：全章定表 ch1=1/2/3=6 波、ch2=2/3/4=9 波、ch3 起=3/4/5=12 波；
        // Boss 关另加末位 1 Boss 波，见 SetupBossWave）。
        // 引导局（TutorialDirector 直刷）/金币本/无限裂隙不摇（IsFormalMainRun 过滤），口径一行不动。
        // 刷怪点 hardMax 钳制对正式关**不生效**（v4.0 同口径延续）：anchor 取 usable[Mathf.Min(i, count-1)]、
        // 无刷怪点走虚拟 X（VIRTUAL_WAVE_SPACING），波数与刷怪点数量解耦，不会出 0 波；
        // ch4+ 封顶 9 波 ≤ STAGE_WAVE_MAX，与 v4.0 定死 9 时同一钳制面，无需新钳制。
        int[] formalRoundWaves = null;
        bool[] formalRoundEnd = null;
        if (formalMain)
        {
            formalRoundWaves = RollFormalRoundWaveCounts(CurrentChapter);
            formalRoundEnd = FormalRoundEndFlags(formalRoundWaves);
        }
        int formalTotalWaves = formalRoundEnd != null ? formalRoundEnd.Length : 0;
        // 【2026-10-22 主人拍板 v4.1】正式关精英关按波分流：只有末波（总波数第末波）用精英编成，
        // 其余波走普通关编成。原整关全精英编成路径（elite 对全部波生效：模式池 + 每波精英追加）保留退役不删 —— 非正式关精英关（金币本等）仍走原路径。
        bool formalEliteSplit = formalMain && elite;

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
            // 【2026-10-25 主人拍板】正式关波数 = 3 回合 × 每回合按章定表（见上方 formalRoundWaves）；
            // v4.2「按章区间随机摇」口径作废。hardMax 钳制只作用于非正式关抽签（RollWaveCount），正式关与 v4.0 同样不钳。
            waveCount = formalMain
                ? formalTotalWaves
                : StageModeTable.RollWaveCount(mode, ComputeWaveBonus(mode), hardMax);

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
            // 【2026-10-25 主人拍板】正式关波数 = 3 回合 × 每回合按章定表（模式池缺失时的兜底路径与主路径同口径）。
            // v4.0 原注释：不走 DistributeMonstersToWaves：它有「每波下限 2」的钳制，会把 10-15 抬到 ≥18 = 私自改总量。
            if (formalMain)
                waveCount = formalTotalWaves;
            // 【2026-10-22 主人拍板 v4.1】DistributeMonstersKeepTotal（v4.0 均摊口径）对正式关停用、函数保留不删：
            // 正式关每波怪数改为「每波独立摇 10-15」，见下方统一覆盖段。
            if (formalMain)
                perWave = DistributeMonstersKeepTotal(total, waveCount);
            else
                perWave = GameConfig.DistributeMonstersToWaves(total, waveCount);
        }

        // 【2026-10-22 主人拍板 v4.1】正式关每波怪数独立摇 10-15，不分摊：
        // 无论模式池路径还是公式兜底路径，正式关统一每波 Random.Range(10,16)（新常量
        // GameConfig.FORMAL_WAVE_MONSTERS_MIN/MAX），不再把关卡总怪数均摊到波（上午的 DistributeMonstersKeepTotal 停用保留）。
        // 【2026-10-25 主人拍板】波数改按章定表（formalTotalWaves），覆盖段照新波数循环即可，怪数口径不变。
        // 总怪量随总波数浮动（ch1 6 波 ~60-90 / ch2 9 波 ~90-135 / ch3+ 12 波 ~120-180）；精英末波同口径（编成为精英）。
        // ⚠ 性能口径不变：怪是分波刷的、前波不死光不刷后波，同屏峰值仍 ~15 只，与此前性能测试口径一致。
        // 引导局 / 金币本 / 无限裂隙不走本段，怪数口径一行不动。
        // 【2026-10-23 主人拍板】第 1 波减半、第 2 波减 1/4（折扣真源 = GameConfig.FORMAL_WAVE1/2_DISCOUNT），
        // 在摇号处直接落地（唯一入口），不影响精英追加额 / Boss 波编成。
        if (formalMain)
        {
            total = 0;
            for (int i = 0; i < waveCount; i++)
            {
                perWave[i] = UnityEngine.Random.Range(GameConfig.FORMAL_WAVE_MONSTERS_MIN, GameConfig.FORMAL_WAVE_MONSTERS_MAX + 1);
                if (i == 0)
                    perWave[i] = Mathf.Max(1, Mathf.CeilToInt(perWave[i] * GameConfig.FORMAL_WAVE1_DISCOUNT));
                else if (i == 1)
                    perWave[i] = Mathf.Max(1, Mathf.CeilToInt(perWave[i] * GameConfig.FORMAL_WAVE2_DISCOUNT));
                total += perWave[i];
            }
        }

        // 精英关本波精英追加额（2026-10-14 主人拍板口径，见下方 elite 段）
        int eliteWaveBonus = 0;
        for (int i = 0; i < waveCount; i++)
        {
            // 【2026-10-22 主人拍板 v4.1】正式关精英关按波分流：只有末波（总波数末波）是精英波，
            // 其余波走普通关编成；非正式关保持原口径（整关精英）。
            bool waveIsElite = formalEliteSplit ? (i == waveCount - 1) : elite;
            if (waveIsElite)
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
                // 【2026-10-22 主人拍板 v4.2】每回合的最后一波标记回合末（波清链据此弹抽奖窗）；
                // 仅正式关铺波路径填写，其余路径默认 false
                isRoundEnd = formalRoundEnd != null && i < formalRoundEnd.Length && formalRoundEnd[i],
                spawned = false,
                aliveCount = 0
            };
            // 原型接管进场方向 / 出怪间隔 / 人数上限 / 近远编成 / 精英额度
            // 【2026-10-22 主人拍板 v4.1】正式精英关的第 1-8 波不挂精英模式原型（普通关编成同款：无原型）；
            // 末波照常挂精英池原型。原「全波挂精英池原型」路径保留退役。
            bool splitOffArch = formalEliteSplit && i < waveCount - 1;
            ApplyArchetypeToWave(wave, splitOffArch ? "" : (archIds != null ? archIds[i] : ""));
            // 【2026-10-20 主人拍板】普通小怪波近远编成随机摇定（全近战/混合/全远程）；
            // 精英波不摇 —— 精英关编成由模式原型接管（箭雨全远程等），保持既有拍板。
            // v4.1：正式精英关的普通波（1-8 波）按普通关口径摇编成。
            if (!waveIsElite) wave.compositionStyle = RollWaveComposition();
            // 【2026-10-14 主人拍板】精英关每波 2~4 只精英（旧值固定 +1，且 SpawnWave 曾整波乘精英缩放）
            if (waveIsElite) wave.eliteBonus = eliteWaveBonus;
            _waves.Add(wave);
        }

        // 【2026-10-26 前期优化·机枢】三档难度曲线（仅正式主线关 formalMain 生效）：
        //  Ch1 简单 → 无特殊处理；
        //  Ch2 普通有变化 → 本关某波(非首波)塞 1 只「签名怪」(指定精灵+词缀的迷你精英)，给每关不同的长相；
        //  Ch3+ 靠运气 → 在 Ch2 基础上再加「第3波双段爆发」(紧接加重段，段上挂签名怪) + 事件层(见 PlanStageEvents)。
        // 全部不动 stage_spawn 公式、不加 Normal 模式行；插入爆发段后重算 isRoundEnd，保证抽奖窗链路不错位。
        if (formalMain && stageIdx >= GameConfig.CLIMAX_MIN_STAGE)
        {
            int tier = GameConfig.GetChapterSpiceTier(CurrentChapter);
            if (tier >= GameConfig.SPICE_LUCK && _waves.Count > GameConfig.CLIMAX_WAVE_INDEX)
            {
                // Ch3+「靠运气」：第3波后插一段紧接加重波，签名怪挂在这段上 = 本关那只特殊怪
                var w3 = _waves[GameConfig.CLIMAX_WAVE_INDEX];
                int burstCount = Mathf.Max(1, Mathf.CeilToInt(w3.monsterCount * GameConfig.CLIMAX_W2_MUL));
                var burst = new WaveData
                {
                    triggerX = w3.triggerX + GameConfig.CLIMAX_GAP,
                    spawnAnchor = w3.spawnAnchor,
                    monsterCount = burstCount,
                    isBossWave = false,
                    isRoundEnd = false,
                    spawned = false,
                    aliveCount = 0,
                    compositionStyle = w3.compositionStyle,
                };
                GameConfig.GetSignatureMonster(CurrentChapter, stageIdx, out int sigSprite, out string sigBias);
                burst.signatureSpriteIndex = sigSprite;
                burst.signatureAffixBias = sigBias;
                _waves.Insert(GameConfig.CLIMAX_WAVE_INDEX + 1, burst);
                RecomputeClimaxRoundEnd(formalRoundWaves, GameConfig.CLIMAX_WAVE_INDEX);
                _totalWaves = _waves.Count;
                GamePerf.Log($"[WavePlanner] Ch{CurrentChapter} 双段爆发：插入爆发段 count={burstCount} sprite={sigSprite} affix={sigBias} @x={burst.triggerX:F1}");
            }
            else if (tier >= GameConfig.SIGNATURE_CHAPTER_MIN)
            {
                // Ch2「普通有变化」：本关某波(非首波)塞 1 只签名怪，给每关不同的长相+词缀
                int idx = Mathf.Min(_waves.Count - 1, GameConfig.CLIMAX_WAVE_INDEX);
                if (idx > 0)
                {
                    GameConfig.GetSignatureMonster(CurrentChapter, stageIdx, out int sigSprite, out string sigBias);
                    _waves[idx].signatureSpriteIndex = sigSprite;
                    _waves[idx].signatureAffixBias = sigBias;
                    GamePerf.Log($"[WavePlanner] Ch{CurrentChapter} 签名怪：挂第{idx + 1}波 sprite={sigSprite} affix={sigBias}");
                }
            }
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
            string compTag = CompositionTag(w.compositionStyle);
            GamePerf.Log($"[BattleManager]   第{i + 1}波 anchor={an} worldX={ax:F2} count={w.monsterCount}{archTag}{eliteTag}{compTag}");
        }
    }

    /// <summary>
    /// 【2026-10-26 前期优化】双段爆发插入后重算回合末标记：找到插入位置所属回合、该回合数 +1，
    /// 再按新回合波数表重铺 isRoundEnd，保证抽奖窗链路（BattleManager 波清链）不错位。
    /// roundCounts 即本方法执行时的 formalRoundWaves（每回合波数表），原地 +1 后长度 = 插入后总波数。
    /// </summary>
    void RecomputeClimaxRoundEnd(int[] roundCounts, int insertIndex)
    {
        if (roundCounts == null || _waves == null) return;
        int acc = 0, r = 0;
        for (; r < roundCounts.Length; r++)
        {
            if (insertIndex < acc + roundCounts[r]) break;
            acc += roundCounts[r];
        }
        if (r < roundCounts.Length) roundCounts[r] += 1;
        bool[] flags = FormalRoundEndFlags(roundCounts);
        for (int i = 0; i < _waves.Count && i < flags.Length; i++)
            _waves[i].isRoundEnd = flags[i];
    }

    /// <summary>
    /// 【2026-10-23 主人拍板】活动副本（金币/徽记共用）定死波次：3 波 × 每波 10 只，按难度分层：
    /// 初级(0)：精英 [0,1,2]、无 Boss（原「金币副本·初级」口径原样保留）；
    /// 中级(1)：每波数量不变，精英在初级基础上每波 +1~2（[1~2, 2~3, 3~4]）；
    /// 高级(2)：每波数量不变，精英每波 80~90%（8~9 只），最后 2 波各带 1 个 Boss（Boss 占波内最后名额，
    ///          精英/Boss 都算在 10 只里）。无限裂隙不选难度，不进本方法。
    /// 精英占波内最后名额（eliteBonus → CoSpawnWaveMonsters 的 eliteQuota 一处真源，精英体型/数值照常生效），
    /// Boss 槽走 bossCountInWave 尾槽口径（不吃精英额度）。
    /// 带精英/Boss 的波不摇近远编成（照既有精英波口径 compositionStyle=0）；纯小怪波按普通波口径摇。
    /// </summary>
    void BuildActivityDungeonWaves(float startX, List<Transform> usable)
    {
        const int perWaveCount = 10;
        int diff = bm != null ? bm.BattleDifficulty : 0;
        int[] baseElite = { 0, 1, 2 };
        int[] elitePerWave = new int[3];
        int[] bossPerWave = { 0, 0, 0 };
        for (int i = 0; i < elitePerWave.Length; i++)
        {
            if (diff >= 2)
            {
                // 80~90% 精英：10 × (0.8~0.9) → 每波 8~9 只；最后 2 波各再带 1 Boss
                elitePerWave[i] = UnityEngine.Random.Range(8, 10);
                if (i >= 1) bossPerWave[i] = 1;
            }
            else if (diff == 1)
            {
                elitePerWave[i] = baseElite[i] + UnityEngine.Random.Range(1, 3);   // 每波精英 +1~2
            }
            else
            {
                elitePerWave[i] = baseElite[i];
            }
        }
        for (int i = 0; i < elitePerWave.Length; i++)
        {
            Transform anchor = (usable.Count > 0) ? usable[Mathf.Min(i, usable.Count - 1)] : null;
            float triggerX = anchor != null
                ? anchor.position.x
                : startX + 3.5f + i * GameConfig.VIRTUAL_WAVE_SPACING;
            var wave = new WaveData
            {
                triggerX = triggerX,
                spawnAnchor = anchor,
                monsterCount = perWaveCount,
                isBossWave = false,
                isRoundEnd = false,
                spawned = false,
                aliveCount = 0,
                eliteBonus = elitePerWave[i],
                bossCountInWave = bossPerWave[i]
            };
            if (elitePerWave[i] <= 0 && bossPerWave[i] <= 0) wave.compositionStyle = RollWaveComposition();
            _waves.Add(wave);
        }
        PlanStageEvents();
        _totalWaves = _waves.Count;
        GamePerf.Log($"[BattleManager] 活动副本（{(bm != null && bm.IsBadgeDungeon ? "徽记" : bm.IsScrollDungeon ? "卷轴" : "金币")}）难度{diff} 定死波次：" +
                     $"{elitePerWave.Length}波×{perWaveCount}只 精英=[{string.Join(",", elitePerWave)}] Boss=[{string.Join(",", bossPerWave)}]");
        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];
            string an = w.spawnAnchor != null ? w.spawnAnchor.name : "virtual";
            float ax = w.spawnAnchor != null ? w.spawnAnchor.position.x : w.triggerX;
            GamePerf.Log($"[BattleManager]   第{i + 1}波 anchor={an} worldX={ax:F2} count={w.monsterCount} 精英={w.eliteBonus} Boss={w.bossCountInWave}{CompositionTag(w.compositionStyle)}");
        }
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】正式关固定波数摊总怪数：每波保底 1 只、余数给前几波，总量严格不变。
    /// （GameConfig.DistributeMonstersToWaves 有每波下限 2 的钳制，9 波会把 10-15 只抬到 ≥18。）
    /// 【2026-10-22 主人拍板 v4.1】对正式关**停用**（每波改独立摇 10-15，见 BuildCombatWaves 统一覆盖段）；
    /// 函数保留不删（下线≠删除），调用点仍留（其结果会被 v4.1 覆盖段整体替换）。
    /// </summary>
    static int[] DistributeMonstersKeepTotal(int total, int waveCount)
    {
        waveCount = Mathf.Max(1, waveCount);
        // 真怪量 < 波数时每波保底 1（最少补到波数，极边界，正常 10-15 ≥ 9 不触发）
        int remaining = Mathf.Max(total, waveCount);
        int baseCount = remaining / waveCount;
        int rem = remaining % waveCount;
        var counts = new int[waveCount];
        for (int i = 0; i < waveCount; i++)
            counts[i] = baseCount + (i < rem ? 1 : 0);
        return counts;
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
    /// 第 1~2 章拦截（硬约束：事件只在 Ch3+「靠运气」档触发）。关掉事件层 / 缺表 时整段跳过。
    /// </summary>
    internal void PlanStageEvents()
    {
        // 每关重置事件层累计状态，保证「关掉即与改动前一致」
        _pendingStones = 0;
        _hazardActive = false;
        if (bm != null) bm.stageEventGoldMul = 1f;

        if (!StageEventTable.EventsEnabled) return;
        if (CurrentChapter < GameConfig.CLIMAX_EVENT_MIN_CHAPTER) return;   // 硬约束：事件只在 Ch3+「靠运气」档触发（StageEventTable.Draw 也按 startChapter 过滤）
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

    /// <summary>单关事件数：仅 Ch3+「靠运气」档触发（硬约束见 PlanStageEvents），概率随章上升，最多 2 个。</summary>
    int RollEventCount(int chapter)
    {
        if (chapter < GameConfig.CLIMAX_EVENT_MIN_CHAPTER) return 0;
        // 至少一个事件的概率随章上升：ch3≈20% → ch7+≈60%
        float p1 = Mathf.Clamp(0.20f + (chapter - 3) * 0.10f, 0.20f, 0.60f);
        // 第二个事件概率较低：ch3≈5% → ch7+≈35%
        float p2 = Mathf.Clamp(0.05f + (chapter - 3) * 0.075f, 0.05f, 0.35f);
        if (UnityEngine.Random.value > p1) return 0;
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
        int[] perWave;
        // 【2026-10-25 主人拍板】正式关 Boss 关 = 3 回合小怪波（每回合按章定表：ch1=6…ch3+ =12 波）
        // + 末位 1 Boss 波；v4.2「按章区间随机摇」口径作废；v4.0「小怪波定死 9（FORMAL_STAGE_WAVE_COUNT）」更早作废（常量保留退役）。
        // Boss 波放末位（原「小怪波 + 末尾 isBossWave」结构不动），且 Boss 波本身不算回合末（isRoundEnd 恒 false，走通关链）。
        // 【2026-10-22 主人拍板 v4.1】小怪波每波怪数独立摇 10-15（不再用 DistributeMonstersKeepTotal 均摊，
        // 函数保留退役）；Boss 波（末位）维持现有 Boss+杂兵编成与怪数口径不变。
        // 引导局/金币本/裂隙（EndlessBossStage 也走这里）口径不动。
        bool[] formalBossRoundEnd = null;
        if (bm != null && bm.IsFormalMainRun)
        {
            var formalRoundWaves = RollFormalRoundWaveCounts(CurrentChapter);
            formalBossRoundEnd = FormalRoundEndFlags(formalRoundWaves);
            minionWaves = formalBossRoundEnd.Length;
            perWave = new int[minionWaves];
            for (int i = 0; i < minionWaves; i++)
                perWave[i] = UnityEngine.Random.Range(GameConfig.FORMAL_WAVE_MONSTERS_MIN, GameConfig.FORMAL_WAVE_MONSTERS_MAX + 1);
        }
        else
            perWave = GameConfig.DistributeMonstersToWaves(minions, minionWaves);

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
                // 【2026-10-22 主人拍板 v4.2】每回合末波标记（仅正式关 Boss 关小怪波；非正式关为 null → 全 false）
                isRoundEnd = formalBossRoundEnd != null && i < formalBossRoundEnd.Length && formalBossRoundEnd[i],
                spawned = false,
                aliveCount = 0
            };
            if (archIds != null) ApplyArchetypeToWave(wave, archIds[i]);
            // 【2026-10-20 主人拍板】Boss 关小怪波同样随机摇编成（Boss 本体波不摇，保持 0）
            wave.compositionStyle = RollWaveComposition();
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
            // 【2026-10-22 主人拍板 v4.2】Boss 波不算回合末（走通关链，清完直接结算，不弹回合抽奖窗）
            isRoundEnd = false,
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
