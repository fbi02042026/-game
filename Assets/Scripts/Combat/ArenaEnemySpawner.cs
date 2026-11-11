using UnityEngine;

/// <summary>
/// 竞技场敌方佣兵生成器（2026-10-30 主人拍板「竞技场真战斗」）：
/// 敌方 = Mercenary 实体 + isAlly=false + Add 进 bm.monsters（🔴 不进 allyUnits/_activeMercs、
/// 不挂友军 OnMercenaryDead，死亡清理走 BattleManager.Waves 的 monsters 遍历，对任意 UnitBase 生效）。
/// 阵容 = <see cref="ArenaRules.BuildBotBattleSquad"/>（power+seed 确定性，与详情弹窗 BuildBotLoadout 同真源）；
/// 对手上下文 = <see cref="ArenaRules.PrepareChallenge"/> 进本前快照。
/// 生成时机：开战过场结束后的首波刷怪口（BattleManager.TrySpawnFirstWaveOnce 的 Arena 分支），替代常规怪波。
/// <para>【2026-11-11 主人拍板「排名最后不该遇到强敌 / 强度要按玩家战力匹配」】强度口径（唯一出口 <see cref="ResolveBattlePower"/>）：
/// ① 阀门局沿用 ArenaRules 既有系数（新手保护 / 连输保底 / 低分保底 = ×0.35 放水，连赢熔断 = ×1.9 加压），
///    放水局再把佣兵人数压到 <see cref="EaseMaxMercs"/>（0~1 个），不再「一上来满编强佣兵」；
/// ② 无阀门局不再吃机器人身上那份固定榜位战力（积分并列时名次与战力会错位），
///    改为「玩家当前战力 × 榜位系数」（<see cref="RankMatchFactor"/>：榜底 0.75 → 榜首 1.75，带 ±8% 确定性浮动），
///    越往前排名强度越高；其中 <see cref="EliteChancePercent"/> 概率抽到「偶发强敌」（系数再 ×<see cref="ElitePowerMul"/>，
///    封顶 <see cref="ArenaRules.ValveHardPowerMul"/>），确定性由机器人 seed 决定，同一对手每次进场强度一致。</para>
/// </summary>
public static class ArenaEnemySpawner
{
    // ── 对手强度匹配（2026-11-11 主人拍板：前几局让玩家赢 + 以玩家战力为基准匹配）──
    /// <summary>放水局（新手保护 / 连输保底 / 低分保底）敌方佣兵人数上限（不含玩家形象）。
    /// 取值依据 = 主人原话「前几局不要有厉害的佣兵或者只有 1 个或没有佣兵」。</summary>
    const int EaseMaxMercs = 1;
    /// <summary>放水局的「阵容展示战力」上限：<see cref="ArenaRules.BuildBotBattleSquad"/> 的人数/稀有度档读
    /// 机器人条目的 power，<c>&lt;800</c> 只出 1 名佣兵、<c>&lt;3000</c> 稀有度全普通（与 EaseMaxMercs 口径一致）。
    /// 取 700 = 800 门槛下留 100 余量，避免量化/取整踩线。</summary>
    const int EaseSquadPowerCeil = 700;
    /// <summary>榜底系数：榜底三名对手的总战力 ≈ 玩家当前战力 × 0.75。
    /// 定值依据（2026-11-11 追加「不要赢得太多、升得太快」后从 0.60 抬到 0.75）：
    /// 仍低于 1（垫底常规局该赢），但已越过「对面掉线」感——玩家战力 1000 时敌方预算 750 会出到
    /// 1 名佣兵 Lv2★2（撞偶发强敌 ×1.2 → 900 则升到 2 名佣兵），玩家得认真打；又高于既有必赢门
    /// <c>ValveEasePowerMul=0.35</c>，榜底常规局不比保底局弱。</summary>
    const float RankFactorBottom = 0.75f;
    /// <summary>榜首系数：榜首三名对手 ≈ 玩家当前战力 × 1.75。定值依据（同日从 1.60 抬到 1.75）：
    /// 逼近但不超过既有熔断门 <c>ValveHardPowerMul=1.9</c>（登顶要硬实力，仍留一线生机）。</summary>
    const float RankFactorTop = 1.75f;
    /// <summary>榜位系数确定性浮动幅度（按机器人 seed，±8%）：同一名次不同机器人强度有差异，
    /// 但同一个对手每次进场强度一致（可复现，不掷随机骰判胜负）。</summary>
    const float RankFactorJitter = 0.08f;
    /// <summary>「偶发强敌」概率（百分比，按机器人 seed 确定性判定，不进任何查询函数）：
    /// 20% = 每 5 场普通局撞 1 次，取主人给的 15%~20% 区间上限——再低玩家感知不到，再高就不算「偶发」。
    /// 只作用于无阀门局（新手保护/连输保底/低分保底/连赢熔断不叠加，新手局要保留原来的容易程度），
    /// 阀门局长期占约 1/3 场次，折算到总场次约 13%（20 场约撞 2.6 次）。</summary>
    const int EliteChancePercent = 20;
    /// <summary>「偶发强敌」战力倍数：在榜位系数上再 ×1.20（主人给的 1.15~1.25 区间中值），
    /// 撞上明显高一档、但不越过熔断门（乘积封顶 <see cref="ArenaRules.ValveHardPowerMul"/>=1.9）。</summary>
    const float ElitePowerMul = 1.20f;

    /// <summary>本场已生成过（首波口会被硬刷协程重试多次，幂等挡重复）。</summary>
    static bool _spawnedThisStage;

    /// <summary>新一关重置（LoadStage 竞技场分支调；竞技场单场，进本即一次）。</summary>
    public static void ResetForStage() => _spawnedThisStage = false;

    /// <summary>
    /// 本场敌方战斗战力（唯一出口；只服务组队，🔴 不得进 IsChallengeable/CanEnter 等查询函数）：
    /// · 阀门局：沿用 <see cref="ArenaRules.GetBotBattlePower"/> 既有系数（新手保护 / 连输保底 / 低分保底 ×0.35，
    ///   连赢熔断 ×1.9），锚点 = 玩家抽奖战力 <see cref="RunLoadout.TotalPower"/>；
    /// · 无阀门局：玩家当前战力 × <see cref="RankMatchFactor"/>（榜底 0.75 → 榜首 1.75，±8% 确定性浮动 + 偶发强敌）。
    /// 玩家战力取不到（≤0，异常局）→ 退回机器人榜位战力（旧口径；TotalPower 自带 LogError，不静默造数）。
    /// <paramref name="easeValve"/> = 本场是否走放水门（放水局限定佣兵人数，见 <see cref="EaseMaxMercs"/>）。
    /// </summary>
    static int ResolveBattlePower(SaveData data, ArenaBotEntry bot, int ladderIdx, out bool easeValve)
    {
        float valve = ArenaRules.GetValvePowerMultiplier(data);
        easeValve = valve < 1f;
        if (valve != 1f)
            return Mathf.Max(1, ArenaRules.GetBotBattlePower(data, bot));   // 阀门局：既有系数唯一真源

        int botPower = ArenaRules.GetBotPower(data, ladderIdx);   // 顺带懒补老档 power/seed（jitter 要用 seed）
        int playerPower = RunLoadout.TotalPower();
        int anchor = playerPower > 0 ? playerPower : botPower;
        int rank = ArenaRules.RankOfIndex(data, ladderIdx);
        return Mathf.Max(1, Mathf.RoundToInt(anchor * RankMatchFactor(rank, bot.seed)));
    }

    /// <summary>
    /// 榜位系数：以玩家当前战力为基准，按<b>对手绝对名次</b>做强度递增（越往前越强）。
    /// t = 1 - (rank-1)/BotCount（0 = 榜底、1 = 榜首），凸度复用 <see cref="ArenaRules.PowerCurveExp"/>
    /// （底平顶陡，与既有榜位战力曲线同一套形状）；再按机器人 seed 叠加 ±<see cref="RankFactorJitter"/> 确定性浮动，
    /// 并按 <see cref="EliteChancePercent"/> 抽「偶发强敌」（再 ×<see cref="ElitePowerMul"/>，封顶熔断门）。
    /// 全部是 seed 的纯函数：同一对手每次进场强度一致，不掷随机数判胜负。
    /// </summary>
    static float RankMatchFactor(int rank, int seed)
    {
        float t = 1f - Mathf.Clamp01((rank - 1) / (float)ArenaRules.BotCount);
        float factor = Mathf.Lerp(RankFactorBottom, RankFactorTop, Mathf.Pow(t, ArenaRules.PowerCurveExp));
        float jitter = ((Mathf.Abs(seed) % 1000) / 1000f * 2f - 1f) * RankFactorJitter;
        factor *= 1f + jitter;
        // 偶发强敌：明显高于当前名次档位，但封顶既有「必可输」熔断门，不越过阀门上限
        if (IsEliteOpponent(seed))
            factor = Mathf.Min(factor * ElitePowerMul, ArenaRules.ValveHardPowerMul);
        return Mathf.Max(0.1f, factor);
    }

    /// <summary>偶发强敌判定：纯机器人 seed 函数（同一对手恒定，不每场重摇）；
    /// 取 seed 的千位以上做 0~99 均匀取样，与 ±8% 浮动用的 seed%1000 互不耦合。</summary>
    static bool IsEliteOpponent(int seed) => (Mathf.Abs(seed) / 1000) % 100 < EliteChancePercent;

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

        // 【2026-11-11 主人拍板「排名最后却遇到强敌 / 强度要按玩家战力匹配」】本场强度唯一出口：
        // 阀门局（新手保护/连输保底/低分保底/连赢熔断）沿用 ArenaRules 既有系数；无阀门局改为
        // 玩家当前战力 × 榜位系数（见 ResolveBattlePower），不再直接吃机器人身上的固定榜位战力。
        int battlePower = ResolveBattlePower(data, bot, ctx.LadderIdx, out bool easeValve);
        // 阀门状态（放水=必赢门 / 加压=熔断门 / 1=势均力敌）：必赢门要连等级星级一起压，
        // 否则「玩家战力 × 0.35」算出来的 Lv1★2 照样打不过（2026-10-31 主人报「测试的时候总是输」）。
        float valve = ArenaRules.GetValvePowerMultiplier(data);
        // 阵容人数/稀有度档由 BuildBotBattleSquad 内的 bot.power 决定：用一份副本喂「阵容展示战力」——
        // 放水局压到 EaseSquadPowerCeil（=1 名普通佣兵），其余与战斗战力同值（不再满编强佣兵）。
        var matchBot = new ArenaBotEntry
        {
            name = bot.name,
            avatar = bot.avatar,
            points = bot.points,
            seed = bot.seed,
            power = easeValve ? Mathf.Min(battlePower, EaseSquadPowerCeil) : battlePower,
        };
        // 玩家形象护甲外观清单（每部位一件，品质随战斗战力）：白模 wanjia 的护甲槽是空的，
        // 不装配就是「什么都没穿」（2026-10-31 主人报）。清单与详情弹窗的装备档同一次掷点。
        var figureOutfit = new System.Collections.Generic.List<EnemyFigureOutfit.Piece>();
        var squad = ArenaRules.BuildBotBattleSquad(matchBot, battlePower, figureOutfit, easeValve);
        if (easeValve && squad.Count > 1 + EaseMaxMercs)
        {
            // 兜底闸：展示战力档没压住时（模板/花名册异常），放水局也只保留 1 名佣兵
            squad.RemoveRange(1 + EaseMaxMercs, squad.Count - 1 - EaseMaxMercs);
        }
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
        // 【2026-11-01 主人报「机器人拿着弓还是走的很近」】入场列队位按射程排：射程越远越靠后。
        // 根因：列队位原按 i 顺序排、不看职业，而第 0 位（机器人「玩家形象」）在游侠两套模板里
        // 就是弓手（gongshou101），加 startOffset 钳制下限 1.5 → 弓手恒排最前排、贴脸站桩平射。
        // 只改列队位顺序（AI 解冻后仍按各自射程自行走位），阵容/属性/屏外出生点一概不动。
        var drawOrder = new System.Collections.Generic.List<int>(squad.Count);
        for (int i = 0; i < squad.Count; i++) drawOrder.Add(i);
        drawOrder.Sort((a, b) => MercRosterDefs.ResolveRange(squad[b].mercId)
                               .CompareTo(MercRosterDefs.ResolveRange(squad[a].mercId)));
        int slot = 0;
        foreach (int si in drawOrder)
        {
            var e = squad[si];
            // 玩家半场右侧列队：横向铺开（间隔 1.4 世界单位），朝向由生成后 Face(-1) 定
            var pos = new Vector3(baseX + startOffset + slot * 1.4f, UnitBase.GROUND_Y, z);
            // 屏外出生点：镜头右缘（limit）再往外扩 1.2，纵向按序号再排开，保证整只都在屏外
            if (limit != float.MaxValue)
                pos.x = limit + 1.2f + slot * 0.45f;
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
            // 【2026-11-01 主人拍板「对方弓手要和我方用同一种子弹，不要怪物那套」】
            // 敌方镜像小队弹道/远程 VFX 走我方套装（消费点唯一出口 UnitBase.ResolveProjectileFaction）
            merc.projectileVfxAsAlly = true;
            merc.SetPartyIndex(slot);           // 车道偏移按队内序错开
            merc.Face(-1);                      // 面向玩家半场（左）
            // 【2026-10-31 主人拍板】屏外走入：目标 = 各自的交战位（镜头内列队点），到位后交还普通 AI
            merc.BeginMapEnter(baseX + startOffset + slot * 1.4f, -1);
            // 花名册默认技能（主动+被动）——敌方队伍照常放技能（牧师奶队友等）
            MercRosterDefs.GetSkillIds(e.mercId, out string activeId, out string passiveId);
            if (!string.IsNullOrEmpty(activeId) || !string.IsNullOrEmpty(passiveId))
                merc.SetupBattleSkills(activeId, passiveId);
            bm.monsters.Add(merc);              // 🔴 敌方阵营只进怪物列表
            bm._totalMonstersSpawnedThisStage++;
            bm.InvalidateAliveMonsterCache();
            spawned++;
            slot++;
            Debug.Log($"[Arena] 敌方单位入场：{(e.isHeroFigure ? "玩家形象" : e.hireId)}({e.mercId}) Lv{e.level}★{e.star} x={pos.x:F1}");
        }
        if (spawned == 0)
        {
            Debug.LogError("[Arena] 敌方小队全员生成失败（见上方逐条 LogError），本场无敌人");
            return false;
        }
        _spawnedThisStage = true;
        // 诊断日志（2026-11-11 主人报「排名最下面还遇到强敌」）：一行看全 名次 / 榜位战力 / 玩家当前战力 /
        // 阀门 / 放水标记 / 偶发强敌 / 战斗战力，下次贴日志就能判断是敌人太强还是玩家侧太弱。
        bool elite = valve == 1f && IsEliteOpponent(bot.seed);
        Debug.Log($"[Arena] 敌方小队生成完成：{bot.name}（排名 {ArenaRules.RankOfIndex(data, ctx.LadderIdx)}" +
                  $" / 榜位战力 {ArenaRules.GetBotPower(data, ctx.LadderIdx)}" +
                  $" / 玩家当前战力 {RunLoadout.TotalPower()} / 阀门 ×{valve:F2}{(easeValve ? "（放水局）" : "")}" +
                  $"{(elite ? "（偶发强敌）" : "")} → 战斗战力 {battlePower}）× {spawned} 名");
        return true;
    }
}
