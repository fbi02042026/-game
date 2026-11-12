using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：选怪与选精灵（引导步进参数、近远编成、风格过滤、波次模板挑选）
public partial class WavePlanner
{

    // ---- extracted from BattleManager L2157-L2682 ----
    /// <summary>引导关刷怪前：从 tutorial_battle 表读取近战/远程精灵编号与 HP 档。</summary>
    public void ApplyTutorialBattleStep(int order)
    {
        var step = TutorialBattleTable.GetStepOrDefault(order);
        _tutorialSpriteMelee = step.spriteMelee > 0 ? step.spriteMelee : 1;
        _tutorialSpriteRanged = step.spriteRanged > 0 ? step.spriteRanged : 2;
        _tutorialEliteCount = step.eliteCount > 0 ? step.eliteCount : 0;
        _tutorialRangedCount = step.rangedCount;
        _tutorialHpFromTable = step.HasHp;
        _tutorialHpMin = step.hpMin;
        _tutorialHpMax = step.hpMax;
        _tutorialEliteHpMin = step.eliteHpMin;
        _tutorialEliteHpMax = step.eliteHpMax;
        _tutorialRangedHpMin = step.rangedHpMin;
        _tutorialRangedHpMax = step.rangedHpMax;
    }

    /// <summary>单波内交替近战/远程；引导关强制混刷弓/法球与近战。</summary>
    int PickTutorialSpriteIndex(System.Collections.Generic.List<int> availableSprites, int slotIndex)
    {
        if (availableSprites == null || availableSprites.Count == 0) return 1;

        // 引导编成：表内 rangedCount >= 0 时按表来（可出纯近战/纯远程波）；
        // 否则沿用旧的固定比例 近战:远程 ≈ 4:2（按本波人数折算，前段近战、后段远程）
        int waveN = Mathf.Max(1, _tutorialWaveMonsterCount);
        int rangedSlots = _tutorialRangedCount >= 0
            ? Mathf.Clamp(_tutorialRangedCount, 0, waveN)
            : Mathf.Max(1, Mathf.RoundToInt(waveN * 2f / 6f));
        int meleeSlots = Mathf.Max(0, waveN - rangedSlots);
        bool wantRanged = slotIndex >= meleeSlots;
        int monsterChapter = GameConfig.GetMonsterChapter(CurrentChapter);
        int fallbackMelee = _tutorialSpriteMelee > 0 ? _tutorialSpriteMelee : 1;
        int fallbackRanged = _tutorialSpriteRanged > 0 ? _tutorialSpriteRanged : 2;

        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            if (MonsterAttackStyleTable.IsRanged(style))
                fallbackRanged = idx;
            else
                fallbackMelee = idx;
        }

        var matched = new System.Collections.Generic.List<int>();
        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            bool isRanged = MonsterAttackStyleTable.IsRanged(style);
            if (wantRanged == isRanged)
                matched.Add(idx);
        }
        if (matched.Count == 0)
            return wantRanged ? fallbackRanged : fallbackMelee;

        if (wantRanged)
        {
            for (int i = 0; i < matched.Count; i++)
            {
                var s = MonsterAttackStyleTable.Get(monsterChapter, matched[i]);
                if (s == MonsterAttackStyle.Bow) return matched[i];
            }
        }
        return matched[slotIndex % matched.Count];
    }

    /// <summary>单波内交替近战/远程；同波尽量不重复精灵（走配置表，不用预制体占位图）</summary>
    int PickWaveSpriteIndex(System.Collections.Generic.List<int> availableSprites, int stageIdx,
        System.Collections.Generic.List<MonsterConfig> pool, int slotIndex,
        System.Collections.Generic.HashSet<int> usedThisWave = null,
        int waveIndex = 0, StageType stageType = StageType.Normal)
    {
        if (availableSprites == null || availableSprites.Count == 0) return 1;

        if (Rules.UseTutorialSprites)
            return PickTutorialSpriteIndex(availableSprites, slotIndex);

        int chapter = CurrentChapter;

        // —— 波次原型接管近/远编成：优先于 wave_slot 的「奇偶 = 近/远」——
        // 这是「箭雨全是远程」「铁卫全是近战」这类模式形状能成立的前提。
        var arch = GetWaveArchetype(waveIndex);
        if (arch != null && arch.ControlsComposition)
        {
            int waveN = GetWaveMonsterCount(waveIndex);
            int rangedN = arch.ResolveRanged(waveN);
            // 远程排在最后几个槽位：spawn 顺序靠后 = 站位更靠右 = 站在近战后面
            bool archRanged = slotIndex >= Mathf.Max(0, waveN - rangedN);
            return PickSpriteByStyle(availableSprites, chapter, archRanged, usedThisWave, slotIndex, stageIdx);
        }

        // —— 【2026-10-20 主人拍板】波次近远编成随机化：铺波时每波摇定（全近战/混合），
        // 覆盖 wave_slot 的奇偶近远表。【2026-10-22 主人拍板】全远程档已取消，
        // 混合波每槽按 WAVE_COMP_MIXED_RANGED_P（1/4）抽远程 → 近战:远程 ≈ 3:1~4:1。
        // 池内没有对应风格怪时 PickSpriteByStyle 自带回落原池，不会空刷。
        if (_activeWaveComposition > 0)
        {
            bool slotWantRanged = _activeWaveComposition == 2
                && Random.value < GameConfig.WAVE_COMP_MIXED_RANGED_P;
            return PickSpriteByStyle(availableSprites, chapter, slotWantRanged, usedThisWave, slotIndex, stageIdx);
        }

        WaveSlotTable.EnsureLoaded();
        if (WaveSlotTable.TryGetSlot(chapter, stageIdx, stageType, waveIndex, slotIndex, out var slotRule))
        {
            if (slotRule.spriteIndex > 0 && availableSprites.Contains(slotRule.spriteIndex))
                return slotRule.spriteIndex;

            var styleFiltered = FilterSpritesByStyle(availableSprites, chapter, slotRule.styleFilter);
            if (styleFiltered.Count > 0)
            {
                if (!slotRule.allowDuplicate && usedThisWave != null && usedThisWave.Count > 0)
                {
                    var unused = styleFiltered.Where(idx => !usedThisWave.Contains(idx)).ToList();
                    if (unused.Count > 0) styleFiltered = unused;
                }
                if (ConfigManager.Instance != null)
                    return ConfigManager.Instance.PickWeightedSpriteIndex(styleFiltered, stageIdx);
                return styleFiltered[slotIndex % styleFiltered.Count];
            }
        }

        bool wantRanged = slotIndex % 2 == 1;
        return PickSpriteByStyle(availableSprites, chapter, wantRanged, usedThisWave, slotIndex, stageIdx);
    }

    /// <summary>
    /// 上一波实际用到过的精灵。只记 1 波，用来让相邻两波的「脸」不一样
    /// （2026-09-18：波次每波变一下）。同样是从同一个池里抽，但优先挑上一波没出现过的；
    /// 全是老面孔时才退回原池，不会强行换素材、不碰任何数值。
    /// </summary>
    readonly System.Collections.Generic.HashSet<int> _lastWaveSprites =
        new System.Collections.Generic.HashSet<int>();

    /// <summary>按「要近战 / 要远程」过滤精灵池并加权抽 1 个（同波尽量不重复，邻波尽量换脸）。</summary>
    int PickSpriteByStyle(System.Collections.Generic.List<int> availableSprites, int chapter, bool wantRanged,
        System.Collections.Generic.HashSet<int> usedThisWave, int slotIndex, int stageIdx)
    {
        var filtered = new System.Collections.Generic.List<int>();
        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        for (int k = 0; k < availableSprites.Count; k++)
        {
            int idx = availableSprites[k];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            bool isRanged = MonsterAttackStyleTable.IsRanged(style);
            if (wantRanged == isRanged)
                filtered.Add(idx);
        }
        if (filtered.Count == 0)
            filtered.AddRange(availableSprites);

        if (usedThisWave != null && usedThisWave.Count > 0)
        {
            var unused = filtered.Where(idx => !usedThisWave.Contains(idx)).ToList();
            if (unused.Count > 0)
                filtered = unused;
        }

        // 邻波换脸：挑得到「上一波没用过」的就优先挑它，挑不到保持原样（只影响外观种类）
        if (_lastWaveSprites.Count > 0 && filtered.Count > 1)
        {
            var fresh = filtered.Where(idx => !_lastWaveSprites.Contains(idx)).ToList();
            if (fresh.Count > 0) filtered = fresh;
        }

        if (ConfigManager.Instance == null)
            return filtered[slotIndex % filtered.Count];

        return ConfigManager.Instance.PickWeightedSpriteIndex(filtered, stageIdx);
    }

    static System.Collections.Generic.List<int> FilterSpritesByStyle(
        System.Collections.Generic.List<int> availableSprites, int chapter, string styleFilter)
    {
        var result = new System.Collections.Generic.List<int>();
        if (availableSprites == null || availableSprites.Count == 0) return result;
        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        string filter = string.IsNullOrEmpty(styleFilter) ? "Any" : styleFilter.Trim();

        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            if (filter.Equals("Any", System.StringComparison.OrdinalIgnoreCase))
                result.Add(idx);
            else if (filter.Equals("Bow", System.StringComparison.OrdinalIgnoreCase) && style == MonsterAttackStyle.Bow)
                result.Add(idx);
            else if (filter.Equals("Ranged", System.StringComparison.OrdinalIgnoreCase) && MonsterAttackStyleTable.IsRanged(style))
                result.Add(idx);
            else if (filter.Equals("Melee", System.StringComparison.OrdinalIgnoreCase) && !MonsterAttackStyleTable.IsRanged(style))
                result.Add(idx);
        }
        return result;
    }

    /// <summary>
    /// 挑一只本槽位的怪。
    /// <paramref name="forceNonBoss"/>：Boss 波的「亲卫」槽位用 —— 走普通怪分支，避免抢到 Boss 模板。
    /// </summary>
    bool TryPickWaveMonster(int stageIdx, int slotIndex, bool isBossWave,
        System.Collections.Generic.HashSet<int> usedSprites,
        out MonsterConfig template, out int spriteIndexOverride,
        int waveIndex = 0, StageType stageType = StageType.Normal, bool forceNonBoss = false)
    {
        template = null;
        spriteIndexOverride = 1;
        if (ConfigManager.Instance == null) return false;

        bool wantBoss = isBossWave && !forceNonBoss;

        // 【2026-10-19 修A】无限裂隙每层 stageIndex 恒为 0（ChapterManager.StartEndlessRift），
        // 按 stageIdx 过滤会把 minWave=9 的真 Boss 行全滤掉，Boss 波只能拿普通怪模板走 800/55 兜底。
        // Boss 波取池按大关末关（STAGES_PER_GRAND-1）取，真 Boss 行必入池；
        // 主线 Boss 关 stageIdx 本来就是末关，行为不变。小怪波（wantBoss=false）仍按原 stageIdx。
        // 【2026-11-01 批 B3】Boss 波取池按本关末节（StagesPerGrandOf(章)-1，节数 5/8/10 按章取）。
        int poolIdx = wantBoss ? GameConfig.StagesPerGrandOf(CurrentChapter) - 1 : stageIdx;
        var pool = ConfigManager.Instance.GetWaveMonsterPool(CurrentChapter, poolIdx);
        if (pool == null || pool.Count == 0) return false;

        var availableSprites = ConfigManager.Instance.GetAvailableSpriteIndices(CurrentChapter, poolIdx, wantBoss);
        if (!wantBoss)
        {
            var nonBossSprites = pool.Where(m => !m.isBoss && m.spriteIndex > 0)
                .Select(m => m.spriteIndex).Distinct().OrderBy(s => s).ToList();
            if (nonBossSprites.Count > 0)
            {
                availableSprites = availableSprites.Where(nonBossSprites.Contains).OrderBy(s => s).ToList();
                if (availableSprites.Count == 0)
                    availableSprites = nonBossSprites;
            }
        }
        if (!wantBoss && availableSprites.Count == 0)
            availableSprites.Add(1);

        if (wantBoss)
        {
            // 优先真 Boss 行（sprite 11/12）；避免高阶小怪 isBoss 误抢模板
            var trueBosses = pool.Where(m => m.isBoss && m.spriteIndex >= GameConfig.BOSS_SPRITE_START).ToList();
            if (trueBosses.Count > 0)
                template = trueBosses[Random.Range(0, trueBosses.Count)];
            else
                template = pool.Find(m => m.isBoss) ?? pool[0];
            if (template != null && template.spriteIndex >= GameConfig.BOSS_SPRITE_START)
                spriteIndexOverride = template.spriteIndex;
            else
                spriteIndexOverride = availableSprites.Count > 0 ? availableSprites[0] : GameConfig.BOSS_SPRITE_START;
        }
        else
        {
            spriteIndexOverride = PickWaveSpriteIndex(availableSprites, stageIdx, pool, slotIndex, usedSprites,
                waveIndex, stageType);
            int pickedSprite = spriteIndexOverride;
            template = pool.Find(m => !m.isBoss && m.spriteIndex == pickedSprite);
            if (template == null)
            {
                template = pool.Where(m => !m.isBoss).OrderBy(m => m.spriteIndex).FirstOrDefault() ?? pool[0];
                spriteIndexOverride = Mathf.Max(1, template.spriteIndex > 0 ? template.spriteIndex : 1);
            }
        }

        usedSprites?.Add(spriteIndexOverride);
        return template != null;
    }

    // —— 【2026-11-05 主人拍板】词条怪每关配额：本关可用词条怪编号池（InitAffixQuota 按当关可用性算好）。
    // 配额语义：词条怪从每章第 3 小关登场，之后每关都「时不时出现」且越往后越多（1-2 只 → 3-4 → 5-6）；
    // 只影响正式主线关（引导局 / 金币本 / 徽记本 / 卷轴本 / 裂隙一律不插，行为与改动前一致）。
    readonly List<int> _affixSpritePool = new List<int>();

    /// <summary>
    /// 铺波末尾调用：按 <see cref="GameConfig.AffixQuotaRange"/> 曲线定本关配额，按各波怪数加权摊成
    /// WaveData.affixQuotaInWave（Boss 本体波不摊）；同时按本关可用性（minWave / 通关解锁 / 编号上限 /
    /// 非 Boss / 表定词缀非空）算出本关词条怪编号池。重复调用先清旧状态（普通关 / Boss 关各铺一次）。
    /// </summary>
    void InitAffixQuota(int stageIdx)
    {
        _affixSpritePool.Clear();
        for (int i = 0; i < _waves.Count; i++)
            if (_waves[i] != null) _waves[i].affixQuotaInWave = 0;
        if (bm == null || !bm.IsFormalMainRun) return;
        if (ConfigManager.Instance == null) return;
        GameConfig.AffixQuotaRange(stageIdx, out int qMin, out int qMax);
        if (qMax <= 0) return;

        var pool = ConfigManager.Instance.GetWaveMonsterPool(CurrentChapter, stageIdx);
        if (pool == null || pool.Count == 0) return;
        var indices = ConfigManager.Instance.GetAvailableSpriteIndices(CurrentChapter, stageIdx, false);
        var affixSprites = pool.Where(m => !m.isBoss && !string.IsNullOrEmpty(m.tableAffix) && indices.Contains(m.spriteIndex))
            .Select(m => m.spriteIndex).Distinct().OrderBy(s => s).ToList();
        if (affixSprites.Count == 0) return;
        _affixSpritePool.AddRange(affixSprites);

        int quota = UnityEngine.Random.Range(qMin, qMax + 1);
        var spawnable = new List<int>();
        int totalNormal = 0;
        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];
            if (w == null || w.isBossWave || w.monsterCount <= 0) continue;   // Boss 本体波不插
            spawnable.Add(i);
            totalNormal += w.monsterCount;
        }
        if (spawnable.Count == 0) return;
        quota = Mathf.Clamp(quota, 0, Mathf.Max(0, Mathf.FloorToInt(totalNormal * GameConfig.AFFIX_QUOTA_RATIO)));   // 2026-11-12 主人拍板：封顶 = 本关正常波总怪数 × 5%，取代旧 totalNormal/3；波数压缩后自动跟着降

        // 按怪数占比加权摊到波（大数随机法），再修整差额
        var perWaveQuota = new Dictionary<int, int>();
        int assigned = 0;
        foreach (int wi in spawnable)
        {
            float exact = (float)_waves[wi].monsterCount * quota / totalNormal;
            int n = Mathf.FloorToInt(exact);
            if (UnityEngine.Random.value < exact - n) n++;
            n = Mathf.Min(n, _waves[wi].monsterCount);
            perWaveQuota[wi] = n;
            assigned += n;
        }
        int guard = 64;
        while (assigned != quota && guard-- > 0)
        {
            int wi = spawnable[UnityEngine.Random.Range(0, spawnable.Count)];
            if (assigned < quota && perWaveQuota[wi] < _waves[wi].monsterCount) { perWaveQuota[wi]++; assigned++; }
            else if (assigned > quota && perWaveQuota[wi] > 0) { perWaveQuota[wi]--; assigned--; }
        }
        foreach (var kv in perWaveQuota)
            if (kv.Value > 0) _waves[kv.Key].affixQuotaInWave = kv.Value;
        GamePerf.Log($"[WavePlanner] 词条怪配额 ch{CurrentChapter} 第{stageIdx + 1}关 quota={assigned} 池=[{string.Join(",", _affixSpritePool)}] 每波=[{string.Join(",", _waves.Select(w => w.affixQuotaInWave))}]");
    }

    /// <summary>
    /// 词条怪槽：从本关可用词条怪编号池随机抽一只（表定词缀怪，头顶词条 / 左缘首次登场提示照常走
    /// Monster.Init 表定路径）。池空或池里找不到对应 config 返回 false，调用方回落普通选怪，不吞怪。
    /// </summary>
    bool TryPickAffixMonster(int stageIdx, out MonsterConfig template, out int spriteIndexOverride)
    {
        template = null;
        spriteIndexOverride = 1;
        if (ConfigManager.Instance == null || _affixSpritePool.Count == 0) return false;
        int sprite = _affixSpritePool[UnityEngine.Random.Range(0, _affixSpritePool.Count)];
        var pool = ConfigManager.Instance.GetWaveMonsterPool(CurrentChapter, stageIdx);
        if (pool == null || pool.Count == 0) return false;
        template = pool.Find(m => !m.isBoss && m.spriteIndex == sprite && !string.IsNullOrEmpty(m.tableAffix));
        if (template == null) return false;
        spriteIndexOverride = sprite;
        return true;
    }
}
