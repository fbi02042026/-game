using UnityEngine;

/// <summary>
/// 竞技场敌方佣兵生成器（2026-10-30 主人拍板「竞技场真战斗」）：
/// 敌方 = Mercenary 实体 + isAlly=false + Add 进 bm.monsters（🔴 不进 allyUnits/_activeMercs、
/// 不挂友军 OnMercenaryDead，死亡清理走 BattleManager.Waves 的 monsters 遍历，对任意 UnitBase 生效）。
/// 阵容 = <see cref="ArenaRules.BuildBotBattleSquad"/>（power+seed 确定性，与详情弹窗 BuildBotLoadout 同真源）；
/// 对手上下文 = <see cref="ArenaRules.PrepareChallenge"/> 进本前快照。
/// 生成时机：开战过场结束后的首波刷怪口（BattleManager.TrySpawnFirstWaveOnce 的 Arena 分支），替代常规怪波。
/// </summary>
public static class ArenaEnemySpawner
{
    /// <summary>本场已生成过（首波口会被硬刷协程重试多次，幂等挡重复）。</summary>
    static bool _spawnedThisStage;

    /// <summary>新一关重置（LoadStage 竞技场分支调；竞技场单场，进本即一次）。</summary>
    public static void ResetForStage() => _spawnedThisStage = false;

    /// <summary>
    /// 生成敌方佣兵小队替代常规怪波。返回 true = 已生成（调用方置 _firstWaveSpawned）。
    /// 缺真值（上下文/存档/机器人/阵容倒推为空）→ LogError 不生成（铁律 14，不静默兜底）。
    /// </summary>
    public static bool SpawnSquad(BattleManager bm)
    {
        if (bm == null || _spawnedThisStage) return false;
        var ctx = ArenaRules.CurrentChallenge;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (!ctx.Valid || data == null)
        {
            Debug.LogError($"[Arena] 敌方小队生成失败：挑战上下文无效（Valid={ctx.Valid}）或存档为空");
            return false;
        }
        if (ctx.LadderIdx < 0 || ctx.LadderIdx >= data.arenaBots.Count)
        {
            Debug.LogError($"[Arena] 敌方小队生成失败：ladderIdx={ctx.LadderIdx} 越界（bots={data.arenaBots.Count}）");
            return false;
        }
        var bot = data.arenaBots[ctx.LadderIdx];
        if (bot == null)
        {
            Debug.LogError("[Arena] 敌方小队生成失败：机器人条目为空");
            return false;
        }

        // 阀门战力（2026-10-31 主人拍板「根据玩家的抽奖战力抽取可赢或可输的模板」）：
        // 新手必赢/连输保底/低分保底 → 锚玩家抽奖战力 × 0.6（必可赢）；连赢熔断 → × 1.9（必可输）；
        // 无阀门 → 机器人榜位战力原样。🔴 只在组队路径生效（判定不进 IsChallengeable/CanEnter 等查询函数）；
        // 胜负仍由真实战斗产出，代码里无 Random 判胜负路径。
        int battlePower = ArenaRules.GetBotBattlePower(data, bot);
        // 阀门状态（放水=必赢门 / 加压=熔断门 / 1=势均力敌）：必赢门要连等级星级一起压，
        // 否则「玩家战力 × 0.35」算出来的 Lv1★2 照样打不过（2026-10-31 主人报「测试的时候总是输」）。
        float valve = ArenaRules.GetValvePowerMultiplier(data);
        // 玩家形象护甲外观清单（每部位一件，品质随战斗战力）：白模 wanjia 的护甲槽是空的，
        // 不装配就是「什么都没穿」（2026-10-31 主人报）。清单与详情弹窗的装备档同一次掷点。
        var figureOutfit = new System.Collections.Generic.List<EnemyFigureOutfit.Piece>();
        var squad = ArenaRules.BuildBotBattleSquad(bot, battlePower, figureOutfit, valve < 1f);
        if (squad.Count == 0)
        {
            Debug.LogError("[Arena] 敌方小队生成失败：阵容倒推为空（模板 AssetId 映射全失败，见上方逐条 LogError）");
            return false;
        }

        float baseX = bm.hero != null ? UnitBase.GetCombatX(bm.hero) : 0f;
        float z = bm.unitRoot != null ? bm.unitRoot.position.z : 0f;

        // 【2026-10-31 修「点继续后双方站桩」根因】全项目索敌统一带屏外过滤
        //（GameConfig.IsInCombatViewport，屏外目标不得被索敌/开打）—— 生成点固定 hero+6.5
        // 会落在开战镜头右缘之外 → 双方互相看不见 → 全员站桩（Editor.log 实锤：
        // heroX 恒 -2.69 / velX=0，活怪=2、最近怪 6.5，整场对峙）。这里按开战镜头右缘
        // 收生成点，保证队尾也进镜头；玩家照常能看到对面站在右半场。
        // 【2026-10-31 主人拍板「敌人从屏幕外走进来，不能突然出现」】交战位照旧按上面口径收进镜头，
        // 但出生点改放右屏外，靠 Mercenary.BeginMapEnter 走到各自交战位（开打前定住、解冻才走）。
        float startOffset = 6.5f;
        var cam = Camera.main;
        float limit = float.MaxValue;
        if (cam != null && cam.orthographic)
        {
            float halfW = cam.orthographicSize * cam.aspect;
            limit = cam.transform.position.x + halfW + GameConfig.COMBAT_VIEWPORT_MARGIN - 0.8f;
            float maxStart = limit - (squad.Count - 1) * 1.4f;
            if (baseX + startOffset > maxStart)
                startOffset = Mathf.Max(1.5f, maxStart - baseX);
        }

        int spawned = 0;
        for (int i = 0; i < squad.Count; i++)
        {
            var e = squad[i];
            // 玩家半场右侧列队：横向铺开（间隔 1.4 世界单位），朝向由生成后 Face(-1) 定
            var pos = new Vector3(baseX + startOffset + i * 1.4f, UnitBase.GROUND_Y, z);
            // 屏外出生点：镜头右缘（limit）再往外扩 1.2，纵向按序号再排开，保证整只都在屏外
            if (limit != float.MaxValue)
                pos.x = limit + 1.2f + i * 0.45f;
            // 【2026-10-31 主人拍板「机器人一定要有玩家的形象」】isHeroFigure 条目 = 玩家形象单位
            //（视觉 Units/wanjia，属性/技能按模板职业佣兵查表）；其余照旧敌方佣兵。
            var merc = MercenaryManager.Instance == null ? null
                : e.isHeroFigure
                    ? MercenaryManager.Instance.SpawnEnemyHeroFigure(e.mercId, pos, e.level, e.star, figureOutfit)
                    : MercenaryManager.Instance.SpawnEnemyMercenary(e.mercId, pos, e.level, e.star);
            if (merc == null) continue;   // 单个生成失败已在内部 LogError，其余照常
            // 【2026-10-31 主人拍板「只给玩家形象挂机器人名」】名字牌只挂形象位：佣兵也挂同名牌子
            // 会让场上出现「两个一样的玩家名」—— 这正是「敌人出现两个玩家形象」的观感来源。
            if (e.isHeroFigure) merc.SetDisplayName(ctx.BotName);
            merc.SetPartyIndex(i);              // 车道偏移按队内序错开
            merc.Face(-1);                      // 面向玩家半场（左）
            // 【2026-10-31 主人拍板】屏外走入：目标 = 各自的交战位（镜头内列队点），到位后交还普通 AI
            merc.BeginMapEnter(baseX + startOffset + i * 1.4f, -1);
            // 花名册默认技能（主动+被动）——敌方队伍照常放技能（牧师奶队友等）
            MercRosterDefs.GetSkillIds(e.mercId, out string activeId, out string passiveId);
            if (!string.IsNullOrEmpty(activeId) || !string.IsNullOrEmpty(passiveId))
                merc.SetupBattleSkills(activeId, passiveId);
            bm.monsters.Add(merc);              // 🔴 敌方阵营只进怪物列表
            bm._totalMonstersSpawnedThisStage++;
            bm.InvalidateAliveMonsterCache();
            spawned++;
            Debug.Log($"[Arena] 敌方单位入场：{(e.isHeroFigure ? "玩家形象" : e.hireId)}({e.mercId}) Lv{e.level}★{e.star} x={pos.x:F1}");
        }
        if (spawned == 0)
        {
            Debug.LogError("[Arena] 敌方小队全员生成失败（见上方逐条 LogError），本场无敌人");
            return false;
        }
        _spawnedThisStage = true;
        // 诊断日志（2026-10-31 主人报「总是输」）：一行看全 榜位战力 / 玩家抽奖战力 / 阀门 / 战斗战力，
        // 下次贴日志就能判断是敌人太强还是玩家侧太弱。
        Debug.Log($"[Arena] 敌方小队生成完成：{bot.name}（榜位战力 {ArenaRules.GetBotPower(data, ctx.LadderIdx)}" +
                  $" / 玩家抽奖战力 {RunLoadout.TotalPower()} / 阀门 ×{valve:F2} → 战斗战力 {battlePower}）× {spawned} 名");
        return true;
    }
}
