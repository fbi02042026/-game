using UnityEngine;

/// <summary>
/// GameConfig 的 刷怪数量公式 / 渐进式怪物解锁 部分（2026-09-26 从 GameConfig.cs 按域拆出，partial 同类型）。
/// 只搬位置，**数值与调用点一字未改**；改这里的常量 = 改原 GameConfig。
/// </summary>
public static partial class GameConfig
{
    [Header("刷怪数量公式（首关10~15，后期随机顶到30~35）")]
    /// <summary>单波最少怪物数</summary>
    public const int WAVE_MONSTER_MIN = 2;
    /// <summary>单波最多怪物数</summary>
    public const int WAVE_MONSTER_MAX = 4;
    /// <summary>一关最少波次</summary>
    public const int STAGE_WAVE_MIN = 3;
    /// <summary>一关最多波次</summary>
    public const int STAGE_WAVE_MAX = 9;

    // —— 2026-09-29 主人拍板重做总怪数曲线 ——
    // 旧曲线（GetStageMonsterTotal）是「第 1 关 15.6 只、第 2 关 10.4 只」，
    // 也就是**不单调**：波次跟着变成 5 波 → 3 波，玩家打到第 2 关会觉得「怎么变简单了」。
    // 改成从第一关起**严格单调递增**：10 → 28，每关 +2，区间内 ±10% 随机保留手感差异。
    // 关键点：**一章总怪量与旧曲线基本持平（190 vs 189）** ——
    // 只把怪从「前重后轻」挪成「前轻后重」，金币总收入不变，
    // 商店 / 天赋那套已调好的节奏不会被推倒重来。
    /// <summary>第 1 关的总怪数（全章最少）。</summary>
    public const int MONSTER_TOTAL_BASE = 10;
    /// <summary>
    /// 每推进一关多几只怪。
    /// 【2026-10-23 批 E1】大关从 10 小关缩到 5 小关，2 → 4.5f（float）保持曲线端点 10 → 28 不变
    /// （10 / 14.5 / 19 / 23.5 / 28）。⚠️ 只影响非正式关：主线正式关怪量已被 v4.1
    /// 「每波独立摇 10-15」整体覆盖，不走本公式。
    /// </summary>
    public const float MONSTER_TOTAL_STEP = 4.5f;
    /// <summary>总怪数在基准值上下浮动的幅度（0.1 = ±10%）。</summary>
    public const float MONSTER_TOTAL_JITTER = 0.1f;
    /// <summary>无刷怪点时，波与波之间的世界距离（兼容旧逻辑）</summary>
    public const float VIRTUAL_WAVE_SPACING = 4.2f;

    // —— 波次近远编成（2026-10-20 主人拍板随机化；【2026-10-22 主人拍板】收紧：
    //    取消「全远程」档（主人报「第一波全是远程的 而且攻击还很高」，没法打），
    //    远程占比改按「近战:远程 ≈ 3:1~4:1」——全近战档保留，混合波每槽 1/4 抽远程）——
    /// <summary>单波摇成「全近战」的概率（正式轨普通小怪波）；其余全为混合波。</summary>
    public const float WAVE_COMP_ALL_MELEE_P = 0.3f;
    /// <summary>混合波每槽独立抽成远程的概率（0.25 = 近战:远程 ≈ 3:1；嫌远程还多就再降这个数）。</summary>
    public const float WAVE_COMP_MIXED_RANGED_P = 0.25f;

    // —— 进关血量（2026-10-20 主人拍板·方案C）——
    /// <summary>
    /// 进关基础回复（占最大生命 30%）。血量跨关延续、不再进关满血，
    /// 恢复关回血 / 抽奖「回血N」卡 / 老虎机回血才有意义。
    /// 只作用于 LoadStage 进关瞬间（英雄 + 在场佣兵）；阵亡佣兵复活比例
    /// （RunLoadout.MercReviveHpRatio）单独结算，不受此常量影响。
    /// </summary>
    public const float STAGE_ENTRY_HEAL_PCT = 0.3f;

    /// <summary>清完一波后，下一波倒计时秒数</summary>
    public const float WAVE_SPAWN_INTERVAL = 4f;
    /// <summary>
    /// 【2026-11-13 主人拍板】波流式衔接间隔：上一波**出完**后隔这么多秒直接刷下一波（不等清场、允许场上叠怪），
    /// 把原来「清一波 → 来一波」的两拨合成一波连续怪潮，保持足够怪量给玩家升级成长感 + 紧张感。
    /// 只作用于非 Boss 波；Boss 波仍等清场后走「首领来袭」过场（2026-11-12 拍板保留）。
    /// 唯一消费方 BattleManager.CoWaveStreamNext。
    /// </summary>
    public const float WAVE_STREAM_GAP_SEC = 1f;
    /// <summary>第一章第一关：波间隔（已减半）</summary>
    public const float OPENING_WAVE_SPAWN_INTERVAL = 7f;
    /// <summary>点击加速出兵：剩余每秒兑换金币</summary>
    public const float WAVE_SKIP_GOLD_PER_SEC = 1f;
    /// <summary>连杀判定窗口（秒）</summary>
    public const float COMBO_WINDOW = 3.2f;
    /// <summary>连杀≥3 时每杀额外金币：实际 = COMBO_BONUS_GOLD × 连击数（封顶 20 连）。基准击杀约 5~10 金，该值=2 时 10 连击击杀≈+20 金（≈2~4 倍单杀），作为"不挨打、会连段"的硬实力奖励；天赋换算 GOLD_PER_TALENT_POINT=100，单章多拿约 1~3 天赋点，可观但不破环经济。</summary>
    /// 2026-09-15 产出去零：2 → 1（再÷10 会变 0，连击奖励必须保留最小值，否则"会连段"就没有正反馈了）。
    public const int COMBO_BONUS_GOLD = 1;

    // —— 佣兵币产出（2026-10-07 主人拍板：「佣兵币击杀精英1个 击杀Boss两个」）——
    // 唯一的发放点在 BattleManager.OnMonsterDead（按 Boss 优先、不与精英叠加）。
    /// <summary>
    /// 佣兵币产出总开关。【2026-10-11 主人拍板】主人原话「现在暂时先不用给佣兵币 等以后再说」→ 关掉。
    /// false = 击杀精英 / Boss 都不再发佣兵币（已攒的不清）。要恢复：改回 true 即可，下面两个数值不用动。
    /// </summary>
    public static bool MERC_GOLD_GRANT_ENABLED = false;
    /// <summary>击杀 1 只精英发的佣兵币。</summary>
    public const int MERC_GOLD_PER_ELITE = 1;
    /// <summary>击杀 1 个 Boss 发的佣兵币。</summary>
    public const int MERC_GOLD_PER_BOSS = 2;

    // —— 击杀发局内抽奖币（2026-10-21 主人拍板：击杀怪物给抽奖币，局内币池带不出本局）——
    // 三档固定值直发（不走 StageGoldDefs.MONSTER_KILL_GOLD_MUL 模板公式，那条继续保持 0 死着）。
    // 发放唯一出口 = BattleManager.OnMonsterDead → AddRunGold(countsAsEarned:false) → currentGold（局内抽奖币池）。
    // 金币副本（IsGoldDungeon）有自己的击杀掉金经济，不重复发。
    /// <summary>击杀普通怪发的局内抽奖币（可调）。</summary>
    public const int KILL_GOLD_NORMAL = 10;
    /// <summary>击杀精英发的局内抽奖币（可调；Boss 优先、不与精英叠加）。</summary>
    public const int KILL_GOLD_ELITE = 25;
    /// <summary>击杀 Boss 发的局内抽奖币（可调）。</summary>
    public const int KILL_GOLD_BOSS = 50;

    // ——【2026-10-22 主人拍板】正式关 v4 回合制（只对主线正式关生效；引导/金币本/裂隙不读）——
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.2】正式关回合数：普通/精英/Boss 关均 3 回合；
    /// Boss 关 = 3 回合小怪波 + 末位 1 Boss 波（Boss 波不算回合，走通关链）。
    /// </summary>
    public const int FORMAL_STAGE_ROUND_COUNT = 3;
    /// <summary>
    /// 【路线B·波数合并】总小波数按章递增 ch1=2 / ch2=3 / ch3+=4（每章+1，相对现状 3/4/6 减少）。
    /// FormalRoundSubWaves 恒 1 后本 cap 即本关总波数；怪量按总波数全波权重均摊（见 WavePlanner 分配段）。
    /// 唯一消费方 WavePlanner（正式关回合表 / 非 formal 波数钳制 / Boss 关小怪波 / 资源副本）。
    /// 副本与无限裂隙同按 CurrentChapter 判前后期。
    /// </summary>
    // 【路线B·波数合并】总小波数按章递增 ch1=2 / ch2=3 / ch3+=4
    public static int StageWaveCap(int chapter) => chapter <= 1 ? 2 : (chapter == 2 ? 3 : 4);
    /// <summary>
    /// 【2026-11-14 主人拍板「第一波的数量要保证玩家能抽 1-2 次奖，后期 2-3 次」】
    /// 每关<b>第 1 小波</b>的保底怪数。依据是击杀给币口径：普通怪每只 `KILL_GOLD_NORMAL=10`，
    /// 首抽 40 / 二抽 50 / 三抽 60 → 10 只 = 100 币 = 抽 2 次；15 只 = 150 = 抽 3 次。
    /// 后期总量大，第一波自然落在 12~15（2~3 抽），本保底只兜住前期/低关。
    /// 唯一消费方 WavePlanner.BuildCombatWaves 的 formalMain 分配段。
    /// </summary>
    public const int FORMAL_FIRST_WAVE_MIN = 10;
    /// <summary>
    /// 第 1 小波保底怪数按章取值：ch1/ch2 = 10 只（100 币 = 抽 2 次，对应「前期 1-2 次」上限）；
    /// ch3 起 = 12 只（120 币 = 抽 2 次并留 30 给下一波，对应「后期 2-3 次」）。
    /// 想让后期直接给到 3 抽（150 币）就改成 15 —— 代价是后面几波被摊薄，自己权衡。
    /// </summary>
    public static int FormalFirstWaveMin(int chapter)
        => chapter >= 3 ? 12 : FORMAL_FIRST_WAVE_MIN;
    /// <summary>
    /// 【2026-11-14 主人拍板「一次比一次的怪多，而不是下一波来袭的次数多」】
    /// 第一波之后各小波的权重步长：第 k 个尾波权重 = 1 + k × 本值。
    /// 0.15 时 5 个尾波 ≈ 15%/18%/20%/22%/25% —— 越后越厚，且峰值不会顶破 15。
    /// </summary>
    public const float FORMAL_WAVE_WEIGHT_STEP = 0.15f;
    /// <summary>
    /// 【2026-11-14 主人拍板「第2关波数涨太多、不要让玩家觉得打得久；参考植物大战僵尸：
    /// 下一波来袭 1-2 次就够，要的是每次来袭怪更多，不是来袭次数更多」】
    /// 正式主线关的<b>本关怪量总量</b>定表（唯一真源），随关卡序号递增。
    /// 路线 B 下波数 = StageWaveCap 按章 2/3/4（每回合 1 小波），均摊后单波必须 ≤ <see cref="FormalWaveMaxOf"/>。
    /// <para>【2026-11-14 二批拍板「难度随座爬坡」（主人报「2-1 难度跟 1-1 差不多」）】
    /// 第 1 篇 10 座大关原先每座重放同一条曲线（怪量公式只有章+大关内序号，座数没喂进来；
    /// 属性侧大关爬坡 GRAND_STAT_STEP=0.02 体感为零）→ 现把座数喂进定表：</para>
    /// <para>ch1 第 1 座 28/31/34/37/40（2026-11-14 原表不动）；第 2 座起 40+(座-2)×12+i×3，
    /// 4 小波（[2,2]）总量封顶 55、第 5 座起 6 小波封顶 85 —— 封顶值 = 首波保底 10 + 其余波 ×15，
    /// 超出会被分配段静默削掉（第一波钉死 10 不回填），所以封顶必须按这个口径算。</para>
    /// <para>ch2/ch3+ 的既有定表一字不动。</para>
    /// </summary>
    public static int FormalStageMonsterTotal(int chapter, int grandIndex1Based, int stageIndex0Based)
    {
        int i = Mathf.Max(0, stageIndex0Based);
        int grand = Mathf.Max(1, grandIndex1Based);
        if (chapter <= 1)
        {
            if (grand <= 1) return 28 + i * 3;                     // 第 1 座：原表 28/31/34/37/40
            int waves = grand >= 5 ? 6 : 4;                        // [2,2] / [2,2,2]
            int cap = (waves - 1) * FormalWaveMaxOf(chapter) + FORMAL_FIRST_WAVE_MIN;
            return Mathf.Min(40 + (grand - 2) * 12 + i * 3, cap);
        }
        if (chapter == 2) return 40 + i * 3;   // 40 / 43 / 46 / 48
        return 60 + i * 4;                     // 60 / 64 / 68 / 72
    }

    /// <summary>
    /// 【路线B·合波】每回合 1 小波（原「每回合 2 小波」合并；历史 [1,2]/[2,2]/[2,2,2] 座数分档退役），
    /// 总波数由 <see cref="StageWaveCap"/> 按章定 2/3/4。grand/chapter 参数保留兼容（路线 B grand 恒 1）。
    /// 唯一消费方 WavePlanner.RollFormalRoundWaveCounts。
    /// </summary>
    public static int FormalRoundSubWaves(int chapter, int grandIndex1Based, int roundIndex0Based)
    {
        if (roundIndex0Based < 0) return 0;
        // 【路线B·合波】每回合 1 小波（原「每回合 2 小波」合并）；总波数由 StageWaveCap 定 2/3/4。
        // grand/chapter 参数保留兼容（历史分档退役，路线 B grand 恒 1）。
        return 1;
    }
    /// <summary>
    /// 【2026-11-05 主人拍板】主线正式关每关削减的波数（从最后一回合扣，每回合保底 1 波）。
    /// 唯一消费方 WavePlanner.RollFormalRoundWaveCounts；引导局 / 活动副本 / 无限裂隙不走定表，不受影响。
    /// 2 = ch1 6→4、ch2 9→6（Boss 关 7 波）、ch3+ 9→7（Ch3+ 双段爆发补 1 波 → 8，净减 3 波）。
    /// Ch2 例外走 FormalRoundWaveCut，本常量只代表「其余章」。
    /// </summary>
    public const int FORMAL_ROUND_WAVE_CUT = 2;
    // 【2026-11-11 第二批主人拍板】第二章改成6和7波：Ch2 正式关普通关 6 波 / Boss 关 7 波（小怪波 6 + Boss 本体 1）
    public const int FORMAL_ROUND_WAVE_CUT_CH2 = 3;
    /// <summary>正式关本关削减波数的唯一真源；唯一消费方 WavePlanner.RollFormalRoundWaveCounts</summary>
    public static int FormalRoundWaveCut(int chapter)
        => chapter == 2 ? FORMAL_ROUND_WAVE_CUT_CH2 : FORMAL_ROUND_WAVE_CUT;

    /// <summary>
    /// 【2026-10-25 主人拍板】正式关每回合波数**按章 × 回合位置定死**（不随机摇）：
    /// ch1 = 1/2/3；ch2 = 每回合 +1 = 2/3/4；ch3 = 再 +1 = 3/4/5；ch4 及以后固定 = 3/4/5。
    /// 主人原话：「第一回合1波 第二回合2波 第三回合3波」→「第二章每回合加一波
    /// 第三章在第二章基础上也加一波 然后以后的章节就固定成第三章的」。
    /// 旧「Min/Max 区间随机摇」整条退役已删（2026-10-25，此前唯一消费点就是定表覆盖处）。
    /// 唯一消费方 <see cref="WavePlanner.RollFormalRoundWaveCounts"/>（普通/精英/Boss 关共用，单一出口）。
    /// 【2026-11-11 主人拍板】主人原话「21-2 打的时间太长了 往后的都减少三分之一波」→ 覆盖 2026-10-25 旧口径：
    /// Ch3+ 正式主线关（含 21-1）与 Boss 关小怪波一起减波，ch3+ 基数由 3 收到 2（与 ch2 同表 [2,3,4]）；
    /// 削减后 7 波 + Ch3+ 双段爆发补 1 波 = 8 波（原 3/4/5=12 → 扣 2=10 → 爆发=11 波口径作废）。
    /// Ch1 / Ch2 一个字不动（ch1 [1,2,3]→4 波、ch2 [2,3,4]→7 波结果完全不变），3 回合结构不变。【本段 ch2 部分已于同日第二批作废，见下一行；ch1 部分仍有效】
    /// 【2026-11-11 第二批主人拍板】「第二章改成6和7波」→ Ch2 经 FormalRoundWaveCut 额外再削 1 波，普通关 6 / Boss 关 7；Ch1 与 Ch3+ 仍不动。
    /// </summary>
    public static int[] FormalRoundWaveFixedCounts(int chapter)
    {
        // 每回合波数 = 基数 + 回合下标：基数 ch1=1、ch2=2、ch3 及以后=2（2026-11-11 拍板，与 ch2 同表）
        int first = Mathf.Clamp(Mathf.Max(1, chapter), 1, 2);
        var counts = new int[FORMAL_STAGE_ROUND_COUNT];
        for (int r = 0; r < counts.Length; r++)
            counts[r] = first + r;
        return counts;
    }

    // ——【2026-10-26 前期优化·机枢】三档难度曲线：Ch1 简单 / Ch2 普通有变化 / Ch3 起靠运气 ——
    /// <summary>难度档位：0=简单(Ch1) 1=普通有变化(Ch2) 2=靠运气(Ch3+)。单调升，便于一处调参。</summary>
    public const int SPICE_SIMPLE = 0;
    public const int SPICE_VARIATION = 1;
    public const int SPICE_LUCK = 2;
    /// <summary>双段爆发 / 事件层起始章（含）：Ch3 起才进「靠运气」档（收敛回 3，贴合三档曲线）。</summary>
    public const int CLIMAX_CHAPTER_MIN = 3;
    /// <summary>签名怪（替换/新加一只带词缀的怪）起始章（含）：Ch2 起给「变化」。</summary>
    public const int SIGNATURE_CHAPTER_MIN = 2;
    /// <summary>在第 3 波（下标=2）之后插入双段爆发段。</summary>
    public const int CLIMAX_WAVE_INDEX = 2;
    /// <summary>爆发第二段相对第3波的 triggerX 间距（正常 VIRTUAL_WAVE_SPACING=4.2，缩短=连着来更压迫）。</summary>
    public const float CLIMAX_GAP = 2.5f;
    /// <summary>爆发第二段怪数倍率（×1.4 = 更猛）。</summary>
    public const float CLIMAX_W2_MUL = 1.4f;
    /// <summary>跳过纯教学第1关（stageIdx=0），护新手手感。</summary>
    public const int CLIMAX_MIN_STAGE = 1;
    /// <summary>事件层起始章（=3）：事件只在 Ch3+「靠运气」档触发（StageEventTable.Draw 也按 startChapter 过滤）。</summary>
    public const int CLIMAX_EVENT_MIN_CHAPTER = 3;

    /// <summary>每章难度档位：Ch1=简单(0) / Ch2=普通有变化(1) / Ch3+=靠运气(2)。</summary>
    public static int GetChapterSpiceTier(int chapter)
    {
        if (chapter <= 1) return SPICE_SIMPLE;
        if (chapter <= 2) return SPICE_VARIATION;
        return SPICE_LUCK;
    }

    /// <summary>
    /// 【2026-10-26 前期优化】每关「签名怪」：强制一只特定精灵 + 带 1 个词缀（做成迷你精英，复用现成 MonsterAffix）。
    /// 按(章,关)轮换，保证每关长得不同、还带词缀，解决「总是一样的怪」。
    /// 精灵锁 1~8（实际数组上限，不碰 10~12 Boss 段，否则会被当成 Boss 单位）；词缀从现有 6 种挑，
    /// Ch2「变化」档用温和 4 种（铁壁/荆棘/狂暴/迅捷），Ch3+「运气」档再加吸血/分裂。
    /// </summary>
    public static void GetSignatureMonster(int chapter, int stageIdx, out int spriteIndex, out string affixBias)
    {
        int[] spritesCh2 = { 2, 3, 4, 5 };
        int[] spritesCh3 = { 2, 3, 4, 5, 6, 7, 8 };
        string[] affixCh2 = { "Bulwark", "Thorns", "Frenzy", "Swift" };
        string[] affixCh3 = { "Bulwark", "Thorns", "Frenzy", "Swift", "Leech", "Split" };
        if (chapter <= 2)
        {
            int s = ((stageIdx % spritesCh2.Length) + spritesCh2.Length) % spritesCh2.Length;
            int a = ((stageIdx % affixCh2.Length) + affixCh2.Length) % affixCh2.Length;
            spriteIndex = spritesCh2[s];
            affixBias = affixCh2[a];
        }
        else
        {
            int s = ((stageIdx % spritesCh3.Length) + spritesCh3.Length) % spritesCh3.Length;
            int a = ((stageIdx % affixCh3.Length) + affixCh3.Length) % affixCh3.Length;
            spriteIndex = spritesCh3[s];
            affixBias = affixCh3[a];
        }
    }

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.2 退役】v4.0「正式关定死 9 波」口径作废：总波数改按
    /// 「FORMAL_STAGE_ROUND_COUNT 回合 × 每回合按章定表」累加
    /// （ch1=1/2/3=6、ch2=2/3/4=9、ch3 起=3/4/5=12，见 GameConfig.FormalRoundWaveFixedCounts /
    /// WavePlanner.Plan.RollFormalRoundWaveCounts）。
    /// 常量保留不删（下线≠删除），当前无引用。
    /// </summary>
    public const int FORMAL_STAGE_WAVE_COUNT = 9;
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.2 退役】v4.0「回合末 = 下一未刷波下标 % 3 == 0」判定作废：
    /// 波数按章可变后回合长不再固定 3，改读铺波时落在 WaveData.isRoundEnd 上的标记（见 BattleManager.Waves 波清链）。
    /// 常量保留不删（下线≠删除），当前无引用。
    /// </summary>
    public const int FORMAL_STAGE_ROUND_WAVES = 3;
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】正式关每个普通波怪数下限：每波独立摇，不再把关卡总怪数均摊到波。
    /// ⚠ 2026-11-14 起<b>正式关不再使用</b>（怪量改走 <see cref="FormalStageMonsterTotal"/> 定表均摊），
    /// 常量保留不删；现在只有注释里提到它。
    /// </summary>
    public const int FORMAL_WAVE_MONSTERS_MIN = 10;
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】正式关每个普通波怪数上限（含）：Random.Range(MIN, MAX+1)。
    /// 总怪量由此 ~90-135/关；精英末波同口径（编成为精英）。同屏峰值仍 ~15（分波刷，性能口径不变）。
    /// 【路线 B 起仅退役常量，函数 <see cref="FormalWaveMaxOf"/> 为真源】常量保留不删（铁律9），
    /// 分配段 / 守口径 / Boss 关小怪波等消费点已全部改读 FormalWaveMaxOf(chapter)。
    /// </summary>
    public const int FORMAL_WAVE_MONSTERS_MAX = 15;
    /// <summary>【路线B】单波怪量上限（真源）。合波后总波数按章 2/3/4、每回合 1 小波，
    /// 总怪量按全波权重均摊，各章末关单波峰值远低于本值（见报告复算表），取统一宽松值覆盖；
    /// 真机帧率不达标可调这里。所有原 FORMAL_WAVE_MONSTERS_MAX 消费点改读本函数。</summary>
    public static int FormalWaveMaxOf(int chapter)
        => 32;   // 统一宽松值覆盖所有章峰值，留余量
    /// <summary>
    /// 【2026-10-23 主人拍板】正式关第 1 波怪数折扣：减半（×0.5，向上取整，钳 ≥1）。
    /// 只作用于正式主线关（formalMain 覆盖段），引导局 / 金币本 / 裂隙不走。
    /// </summary>
    public const float FORMAL_WAVE1_DISCOUNT = 0.5f;
    /// <summary>
    /// 【2026-10-23 主人拍板】正式关第 2 波怪数折扣：减 1/4（×0.75，向上取整，钳 ≥1）。
    /// 作用域同 <see cref="FORMAL_WAVE1_DISCOUNT"/>。
    /// </summary>
    public const float FORMAL_WAVE2_DISCOUNT = 0.75f;

    // —— 精英/Boss 概率掉天赋石（2026-10-20 主人拍板：天赋石不再每关首通发放，
    // 改为精英/Boss 击杀有几率掉落：精英 30%×1、Boss 60%×2）——
    /// <summary>击杀精英掉天赋石的几率（0~1）。引导里的宿敌精英同样带精英标记，走同一条判定。</summary>
    public const float ELITE_TALENT_DROP_CHANCE = 0.30f;
    /// <summary>精英一次掉落的天赋石数量。</summary>
    public const int ELITE_TALENT_DROP_AMOUNT = 1;
    /// <summary>击杀 Boss 掉天赋石的几率（0~1）。Boss 波可能同时带精英标记，判定 Boss 优先、不与精英叠加。</summary>
    public const float BOSS_TALENT_DROP_CHANCE = 0.60f;
    /// <summary>Boss 一次掉落的天赋石数量。</summary>
    public const int BOSS_TALENT_DROP_AMOUNT = 2;

    // —— 连杀续杯（R2）：连杀窗口内击杀回血，让"连"成为可持续资源 ——
    /// <summary>连杀回血总开关（2026-10-09 主人拍板关掉）。false = 连杀不再回血。
    /// 想恢复：改回 true 即可，下面的数值不用动。</summary>
    public static bool COMBO_HEAL_ENABLED = false;
    /// <summary>连杀达到该值才开始回血（避免单杀白嫖续航）。</summary>
    public const int COMBO_HEAL_MIN_COMBO = 3;
    /// <summary>每次合格连杀的基础回血量。</summary>
    public const int COMBO_HEAL_PER_KILL = 5;
    /// <summary>每多 5 连击额外回血量（高连击续航更强，封顶见下方 /5 计算）。</summary>
    public const int COMBO_HEAL_STEP = 2;

    // —— 闪避（独立按钮 + 无敌帧 + 飘字，不新增美术）——
    /// <summary>闪避冷却（秒）</summary>
    public const float DODGE_COOLDOWN = 3f;
    /// <summary>闪避无敌帧时长（秒）：仅对敌方伤害生效，关卡机制伤害不免疫。0.4s 比 0.35s 多 50ms 容错，面向休闲/移动端更友好，仍属精准时机级窗口。</summary>
    public const float DODGE_IFRAME = 0.4f;

    // —— 低血加成（R1）：HP 低于阈值时攻速/暴击提升，制造残血反扑手感 ——
    /// <summary>触发阈值：当前 HP / 最大 HP 低于此值进入残血爆发状态。</summary>
    public const float LOW_HP_THRESHOLD = 0.3f;
    /// <summary>残血时攻击速度倍率（>1 即更快）。1.3 = 攻速 +30%。</summary>
    public const float LOW_HP_ATK_SPEED_MUL = 1.3f;
    /// <summary>残血时额外暴击率（绝对值，0.15 = +15%）。仅作用于普攻，不影响技能。</summary>
    public const float LOW_HP_CRIT_BONUS = 0.15f;

    // —— 战斗打击感分步开关（不满意可单独 false 回滚）——
    public static bool COMBAT_JUICE_HIT_STOP = true;
    public static bool COMBAT_JUICE_CAMERA_SHAKE = true;
    public static bool COMBAT_JUICE_SFX = true;
    public static bool COMBAT_JUICE_DAMAGE_TEXT_BOOST = true;
    public static bool COMBAT_JUICE_KNOCKBACK = true;
    public static bool COMBAT_JUICE_COMBO = true;
    /// <summary>受击挤压回弹（squash &amp; stretch）：只压怪物，我方挨打不压，避免「往后顿」发飘。</summary>
    public static bool COMBAT_JUICE_SQUASH = true;

    /// <summary>顿帧时长（秒，unscaled）。NORMAL=1/20 秒（60fps 约 3 帧），是「明显一顿但不粘手」的甜点值。</summary>
    public const float HIT_STOP_NORMAL = 0.05f;
    public const float HIT_STOP_CRIT = 0.055f;
    public const float HIT_STOP_BOSS = 0.08f;
    public const float HIT_STOP_COMBO_ANNOUNCE = 0.04f;
    /// <summary>暴击落下瞬间顿帧（略高于 HIT_STOP_CRIT）</summary>
    public const float CRIT_STRIKE_HIT_STOP = 0.07f;

    /// <summary>我方近战命中时机：相对攻击动画时长比例（0.5=下劈中点）</summary>
    public const float ALLY_MELEE_HIT_NORM = 0.5f;
    /// <summary>
    /// 远程出手延迟（秒）：弓/法球（玩家、佣兵、怪物普攻与弹道技能）在举弓/抬杖后等这一拍再出弹。
    /// 近战刀光不走此值。旧名 BOW_FIRE_RELEASE_DELAY。建议 0.15~0.25。
    /// </summary>
    public const float RANGED_FIRE_RELEASE_DELAY = 0.2f;
    /// <summary>兼容旧引用：等同 <see cref="RANGED_FIRE_RELEASE_DELAY"/>。</summary>
    public const float BOW_FIRE_RELEASE_DELAY = RANGED_FIRE_RELEASE_DELAY;
    /// <summary>游侠起手前摇（秒）：比通用远程 0.2 更大，给游侠攻击/技能加前摇（P004 游侠专用）。</summary>
    public const float RANGED_FIRE_RELEASE_DELAY_RANGER = 0.35f;
    /// <summary>我方近战暴击动画幅度倍率（仅视觉子节点）</summary>
    public const float ALLY_MELEE_CRIT_AMP = 1f;

    /// <summary>受击击退距离（世界单位）</summary>
    public const float COMBAT_KNOCKBACK_NORMAL = 0.06f;
    public const float COMBAT_KNOCKBACK_CRIT = 0.12f;
    public const float COMBAT_KNOCKBACK_CRIT_KILL = 0.17f;

    /// <summary>暴击击杀死亡后倒视觉滑动（世界单位，仅程序化死亡 tween）</summary>
    public const float CRIT_KILL_DEATH_SLIDE = 0.15f;
    /// <summary>普通击杀死亡后倒滑动（短于暴击）</summary>
    public const float DEATH_SLIDE_NORMAL = 0.06f;
    // 【2026-10-20 主人拍板「不要这个了」】精英/Boss 致死前摇慢放（CRIT_WINDUP_*）、
    // 击杀镜头（KILL_CAM_*、COMBAT_JUICE_KILL_CAM）、处决压暗（KILL_FINISHER_DIM_*）
    // 常量随整套演出删除 —— 玩家看着一卡一卡，最后一击改为与普通击杀一致。

    /// <summary>击杀收刀顿帧/微震（独立于 COMBAT_JUICE_CAMERA_SHAKE 全局开关）</summary>
    public const float KILL_FINISHER_HIT_STOP = 0.04f;
    public const float KILL_FINISHER_HIT_STOP_BOSS_EXTRA = 0.02f;
    public const float KILL_FINISHER_SHAKE_AMP = 0.04f;
    public const float KILL_FINISHER_SHAKE_DUR = 0.1f;
    public static bool COMBAT_JUICE_KILL_FINISHER_SHAKE = true;

    /// <summary>【2026-11-13 改】只作 HUD 充能条的隐藏判据（恒 false），不再闸功能；雷击已改独立 CD 自动释放。</summary>
    public static bool THUNDER_ULT_ENABLED = false;
    /// <summary>雷击奥义独立冷却（秒）：CD 归零 + 战斗中可行动即自动释放，与玩家 4 槽技能互不干涉。</summary>
    public const float THUNDER_ULT_CD = 22f;

    /// <summary>雷击奥义：伤害倍率、镜头、压暗</summary>
    public const float THUNDER_ULT_DAMAGE_MUL = 2.2f;
    public const float THUNDER_ULT_ZOOM_MUL = 0.78f;
    public const float THUNDER_ULT_ZOOM_IN = 0.45f;
    public const float THUNDER_ULT_ZOOM_OUT = 0.12f;
    public const float THUNDER_ULT_DIM = 0.35f;
    public const int THUNDER_ULT_NEED_MIN = 10;
    public const int THUNDER_ULT_NEED_MAX = 36;

    /// <summary>连杀加速：每多一连 +5%，封顶 +50%（连杀 11）。</summary>
    public const float KILL_COMBO_HASTE_STEP = 0.05f;
    public const float KILL_COMBO_HASTE_MAX = 0.5f;

    /// <summary>雷击奥义所需击杀点数：前期约 10 小怪，后期抬高。</summary>
    public static int GetThunderUltNeedPoints(int chapter, int stageIndex0Based)
    {
        int ch = Mathf.Max(1, chapter);
        int st = Mathf.Max(0, stageIndex0Based);
        return Mathf.Clamp(10 + (ch - 1) * 4 + st * 2, THUNDER_ULT_NEED_MIN, THUNDER_ULT_NEED_MAX);
    }

    /// <summary>
    /// 雷击伤害倍率：引导/第1章前几关 2.8；之后随进度压到约 0.7（后期靠装备）。
    /// </summary>
    public static float GetThunderUltDamageMul(int chapter, int stageIndex0Based, bool tutorial)
    {
        if (tutorial) return 2.8f;
        int ch = Mathf.Max(1, chapter);
        int st = Mathf.Max(0, stageIndex0Based);
        if (ch <= 1 && st <= 2) return 2.8f;
        float t = Mathf.Clamp01((ch - 1) * 0.12f + st * 0.06f);
        return Mathf.Lerp(2.0f, 0.7f, t);
    }

    /// <summary>连杀加速倍率：1 + min(0.5, (combo-1)*0.05)。</summary>
    public static float GetKillComboSpeedMul(int killCombo)
    {
        if (killCombo <= 1) return 1f;
        float bonus = Mathf.Min(KILL_COMBO_HASTE_MAX, (killCombo - 1) * KILL_COMBO_HASTE_STEP);
        return 1f + bonus;
    }

    /// <summary>连杀加速幻影：倍率超过此值才刷残影。</summary>
    public const float COMBO_AFTERIMAGE_MUL_MIN = 1.02f;
    // 【2026-10-19 主人反馈残影再明显点】间隔 0.12→0.10、寿命 0.24→0.32、透明度 0.45→0.62
    public const float COMBO_AFTERIMAGE_INTERVAL_SLOW = 0.10f;
    public const float COMBO_AFTERIMAGE_INTERVAL_FAST = 0.05f;
    public const float COMBO_AFTERIMAGE_LIFE = 0.32f;
    public const float COMBO_AFTERIMAGE_ALPHA = 0.62f;
    public const int COMBO_AFTERIMAGE_MAX_PER_UNIT = 10;
    public const float COMBO_AFTERIMAGE_MOVE_EPS = 0.08f;

    /// <summary>第一章第 1 关（教学节奏：打得慢、打得少）</summary>
    public static bool IsOpeningStage()
    {
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int st = 0;
        if (BattleManager.Instance != null && BattleManager.Instance.currentStage != null)
            st = BattleManager.Instance.currentStage.stageIndex;
        return ch <= 1 && st <= 0;
    }

    public static float GetWaveSpawnInterval()
    {
        return IsOpeningStage() ? OPENING_WAVE_SPAWN_INTERVAL : WAVE_SPAWN_INTERVAL;
    }

    /// <summary>开局我方普攻最终伤害（2~5，暴击略高）；拿剑爽点后一刀一个。</summary>
    [System.Obsolete("引导关已改正式 ATK，勿再调用")]
    public static int RollOpeningAllyHitDamage(bool isCrit)
    {
        if (BattleManager.Instance != null && BattleManager.Instance.TutorialPowerFantasy)
            return isCrit ? Random.Range(35, 46) : Random.Range(25, 41);
        return isCrit ? Random.Range(4, 8) : Random.Range(2, 6);
    }

    /// <summary>
    /// 关卡总怪数：**从第 1 关起严格单调递增** 10 → 28（每关 +2，±10% 随机）。
    /// 2026-09-29 重做，旧版的「第 1 关特意加量拉长战斗」会让波次在第 2 关倒退，已去掉。
    /// 波次数由 <see cref="GetSuggestedWaveCount"/> 按「总数 / 单波上限」推，所以跟着单调不减。
    /// </summary>
    public static int GetStageMonsterTotal(int stageIndex0Based, int chapter)
    {
        // 【2026-11-01 批 B3】输入域按章取（节数 5/8/10），曲线每关 +2 自然延长
        int stageNo = Mathf.Clamp(stageIndex0Based + 1, 1, StagesPerGrandOf(chapter));
        float baseTotal = MONSTER_TOTAL_BASE + (stageNo - 1) * MONSTER_TOTAL_STEP;

        int lo = Mathf.RoundToInt(baseTotal * (1f - MONSTER_TOTAL_JITTER));
        int hi = Mathf.RoundToInt(baseTotal * (1f + MONSTER_TOTAL_JITTER));
        if (hi < lo) hi = lo;
        return Mathf.Max(1, Random.Range(lo, hi + 1));
    }

    /// <summary>普通关总怪数</summary>
    public static int GetNormalStageMonsterTotal(int stageIndex0Based, int chapter)
        => GetStageMonsterTotal(stageIndex0Based, chapter);

    /// <summary>精英关总怪数（同随机曲线）</summary>
    public static int GetEliteStageMonsterTotal(int stageIndex0Based, int chapter)
        => GetStageMonsterTotal(stageIndex0Based, chapter);

    // ============================================================
    // 无限裂隙（2026-10-17 主人拍板）——难度与奖励数值唯一真源
    // ============================================================

    /// <summary>
    /// 无限裂隙强度倍率 —— 【2026-11-14 主人拍板·10 层一节的上升锯齿】
    /// 主人原话：「每打一层敌人的属性增加一部分 到第10层打到一个小高潮 然后11层再降一点
    /// 然后再慢慢涨到20打到一个小高潮 正式是升的曲线 每逢第6关也可以降一些有个过渡」。
    /// <para>形状（每 10 层一节，与记录点 / 大 BOSS 同节奏）：</para>
    ///   · 节内每层 +8%，第 10 层爬到本节**小高潮**（节首 ×1.72）；
    ///   · 第 11 层换节：节首基线比上一节节首高、但低于上一节峰值 → 回落一点给喘息；
    ///   · 第 6/16/26 层（每节相位 5）额外乘 <see cref="ENDLESS_DIFF_DIP_MUL"/> 降一档做过渡；
    ///   · 节基线逐节复利抬高（<see cref="EndlessSectionBase"/>），所以峰值 10 层 &lt; 20 层 &lt; 30 层，
    ///     **整体是升的曲线**，不是每节回到原点；
    ///   · 精英层（5/15/25…）留小尖峰，倍率压到不盖过本节第 10 层高潮。
    /// 消费点：Monster.Init 怪血量 / TTK；攻击侧走 <see cref="EndlessAttackMul"/> 只吃 45%。
    /// </summary>
    public static float EndlessDifficultyMul(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        int section = (stage - 1) / ENDLESS_SAVE_INTERVAL;   // 节序（0 基，每 10 层一节）
        int phase = (stage - 1) % ENDLESS_SAVE_INTERVAL;     // 节内相位 0..9（9 = 第 10 层小高潮）
        float mul = EndlessSectionBase(section) * (1f + ENDLESS_DIFF_INTRA_STEP * phase);
        // 每逢第 6 关（相位 5）降一档，作为爬升途中的过渡
        if (phase == ENDLESS_DIFF_DIP_PHASE) mul *= ENDLESS_DIFF_DIP_MUL;
        if (IsEndlessEliteStage(stage)) mul *= ENDLESS_DIFF_ELITE_BUMP;
        return mul * ENDLESS_DIFFICULTY_GLOBAL_MUL;
    }

    /// <summary>
    /// 每节（**10 层**，2026-11-14 起）**节首**的基线倍率。下标 = 节序（0 基），第 1 节 = 1-10 层。
    /// 相邻两节比值约 ×1.18~1.20（复利）；表本身单调升，是「整体为升的曲线」的保证。
    /// 表尾之外（121 层起）按 <see cref="ENDLESS_SECTION_GROWTH_TAIL"/> 每节续涨。
    /// </summary>
    static readonly float[] ENDLESS_SECTION_BASE_TABLE =
    {
        1.00f, 1.19f, 1.42f, 1.67f, 2.00f,   // 第 1-5 节：1-50 层
        2.40f, 2.86f, 3.40f, 4.02f, 4.69f,   // 第 6-10 节：51-100 层
        5.25f, 6.06f                          // 第 11-12 节：101-120 层
    };
    /// <summary>表尾之外每节复利（121 层后继续涨，不封顶）</summary>
    public const float ENDLESS_SECTION_GROWTH_TAIL = 1.155f;
    /// <summary>锯齿曲线：节内每层爬升（10 层节 → 第 10 层高潮 = 节首 ×1.72）</summary>
    public const float ENDLESS_DIFF_INTRA_STEP = 0.08f;

    /// <summary>【2026-11-14】每逢第 6 关（节内相位 5，即 6/16/26… 层）的过渡位</summary>
    public const int ENDLESS_DIFF_DIP_PHASE = 5;

    /// <summary>【2026-11-14】过渡位难度折扣：爬升途中降 10%，给一节喘息</summary>
    public const float ENDLESS_DIFF_DIP_MUL = 0.90f;
    /// <summary>
    /// 精英层（5/15/25…）小尖峰。【2026-11-14 由 1.45 收回 1.15】
    /// 10 层一节的新曲线要求「第 10 层是本节高潮」；1.45 会让第 5 层
    /// （节首 ×1.32×1.45 = 1.914）反而高过第 10 层（×1.72），高潮跑到精英层去了。
    /// 1.15 → 第 5 层 1.518 &lt; 第 10 层 1.72，仍留一个可感知的小尖峰但不抢高潮。
    /// </summary>
    public const float ENDLESS_DIFF_ELITE_BUMP = 1.15f;

    /// <summary>
    /// 【2026-11-15】无限裂隙精英怪血量补档（×4）。
    /// 裂隙不吃 chapterFirstHpRule 的「章首怪血 ×10/×15」，精英血量只走
    /// 「本章均值 × ELITE_HP_FROM_CHAPTER_AVG × 玻璃/坦克档」，实测第 5 层精英两下被打死。
    /// 只作用裂隙精英怪；主线 / 活动副本 / 支线岛 / 竞技场数值零变化。
    /// ⚠ 基准 = 未补档的原始值（第 5 层「两下打死」那一版），最终血量 = 原始 ×4。
    ///   这是全节共用系数，5/15/25… 所有精英层一起抬，不是只写死第 5 层。
    /// </summary>
    public const float ENDLESS_ELITE_HP_MUL = 4f;

    /// <summary>
    /// 【2026-11-14 主人拍板「整体再调高一档」】裂隙强度总倍率。
    /// 层类型改固定排布 + 10 层一节后，这里统一抬 20% 作为整体档位。要调软/调硬只改这一个常量。
    /// 锚点（乘后）：1 层 1.20、10 层 2.06、11 层 1.43、20 层 2.46；攻击侧按 45% 联动。
    /// </summary>
    public const float ENDLESS_DIFFICULTY_GLOBAL_MUL = 1.20f;

    // ⚠【2026-10-29 已删除】旧线性常量 ENDLESS_DIFFICULTY_STEP_PER_STAGE(0.07) 与
    //   缓冲关折扣 ENDLESS_BUFFER_DISCOUNT(0.9) + IsEndlessBufferStage() —— 职责被锯齿曲线取代
    //   （节内相位 0 本身就是缓冲位），按项目惯例删除不留开关，别再加回来。
    // ⚠【2026-11-02 已删除】旧线性锯齿常量 ENDLESS_DIFF_BASE_PER_SECTION(0.06) —— 被分档表取代。

    /// <summary>节序 → 节首基线倍率（表内直取，表外按尾段复利续涨）。</summary>
    public static float EndlessSectionBase(int section)
    {
        if (section < 0) section = 0;
        if (section < ENDLESS_SECTION_BASE_TABLE.Length) return ENDLESS_SECTION_BASE_TABLE[section];
        float b = ENDLESS_SECTION_BASE_TABLE[ENDLESS_SECTION_BASE_TABLE.Length - 1];
        for (int i = ENDLESS_SECTION_BASE_TABLE.Length - 1; i < section; i++)
            b *= ENDLESS_SECTION_GROWTH_TAIL;
        return b;
    }

    /// <summary>
    /// 无限裂隙**攻击侧**强度倍率 —— 【2026-11-02 主人拍板】血量吃满曲线，攻击只吃 45%。
    /// <para>为什么分开：血量决定「打不打得动」（该陡就陡，制造时间压力），
    /// 攻击决定「会不会被秒」。攻击只吃 45% 后深层仍属「会疼但能救」的区间，
    /// 不会被小怪一击带走而变成纯数值劝退。</para>
    /// 主线 / 副本不吃这条线（Monster.Init 里非裂隙分支 atkScale == scale，行为零变化）。
    /// </summary>
    public static float EndlessAttackMul(int riftStage)
        => 1f + (EndlessDifficultyMul(riftStage) - 1f) * ENDLESS_ATK_SHARE;

    /// <summary>无限裂隙攻击侧吃难度曲线的比例（0 = 攻击不涨，1 = 跟满血量曲线）</summary>
    public const float ENDLESS_ATK_SHARE = 0.45f;
    /// <summary>无限裂隙每日机会数（2026-11-10 口径：3 → 2）。
    /// 【2026-11-14】改为**进本扣 1 次**、通关不扣（见 EndlessRiftRules.TryConsumeEnter）。
    /// 一次 15 体力见 StaminaSystem.ENDLESS_RIFT_COST。</summary>
    public const int ENDLESS_DAILY_TRIES = 2;

    /// <summary>层类型模数 / 每 5 层换景间隔。⚠ 难度锯齿周期已改用 ENDLESS_SAVE_INTERVAL(10)，别混</summary>
    public const int ENDLESS_CHECKPOINT_INTERVAL = 5;

    /// <summary>
    /// 【2026-11-02 主人拍板「每 10 层一个保存点」】存档点间隔 —— 由 5 放宽到 10，
    /// 与大 BOSS 层（10 的倍数）重合：**大 BOSS = 存档点**，小 BOSS（5 的倍数）只当节奏爆点、不存。
    /// <para>⚠ 与 <see cref="ENDLESS_CHECKPOINT_INTERVAL"/> 分工，别混：</para>
    /// <list type="bullet">
    /// <item>CHECKPOINT(5) 管：BOSS 判定 / 难度分节 / 每节换景 —— 改它要重算整条难度曲线；</item>
    /// <item>SAVE(10) 管：快照存档粒度 + 记录点起始层 —— 改它不动难度曲线。</item>
    /// </list>
    /// 主人原话：「每 10 层一个保存点」。
    /// </summary>
    public const int ENDLESS_SAVE_INTERVAL = 10;

    /// <summary>该层是否存档点（每 10 层，与大 BOSS 同层）</summary>
    public static bool IsEndlessSaveStage(int riftStage)
        => Mathf.Max(1, riftStage) % ENDLESS_SAVE_INTERVAL == 0;

    /// <summary>
    /// 【2026-11-02 主人拍板「一键到历史最高出生点 中间的抽奖全部自动抽」】跳层补偿的免费抽**上限**。
    /// <para>口径：一键直达时，被跳过的那几层本该给的免费抽累加起来一次性补给玩家
    /// （见 <c>EndlessRiftRules.CatchUpDraws</c>），让「跳过去的构筑规模」≈「一层层打上去的构筑规模」。</para>
    /// <para>为什么要封顶：补偿按「每层至少 1 抽」累加，100 层就是 100 抽，构筑会直接撑爆、把
    /// 「逐层打」的设计废掉。40 抽 ≈ 40 层以内的常见跳层区间，够用且不失控。</para>
    /// </summary>
    public const int ENDLESS_CATCHUP_DRAW_CAP = 40;

    /// <summary>
    /// 【2026-11-14 已迁表】复刷金币倍率改由 endless_reward.csv 的 repeatMul 列驱动
    /// （见 <see cref="EndlessRepeatGoldMul"/>；表尾外 / 缺表回退 0.3）。常量保留不删，别再当真源引用。
    /// </summary>
    public const float ENDLESS_REPEAT_GOLD_RATE = 0.30f;

    /// <summary>
    /// 该层是否 BOSS 层。【2026-11-14 主人拍板「只有 5 是精英、10 是 boss，其他都是普通」】
    /// 由「5 的倍数」收窄为「10 的倍数」；5/15/25 改判精英（见 <see cref="IsEndlessEliteStage"/>）。
    /// ⚠ ENDLESS_CHECKPOINT_INTERVAL(5) 仍管每 5 层换景与层类型模数；难度锯齿周期用 ENDLESS_SAVE_INTERVAL(10)。
    /// </summary>
    public static bool IsEndlessBossStage(int riftStage)
        => Mathf.Max(1, riftStage) % (ENDLESS_CHECKPOINT_INTERVAL * 2) == 0;

    // —— 层类型排布（2026-11-14 主人拍板固定排布，旧随机洗牌整条下线）——
    // 5/15/25…（5 的倍数非 10 的倍数）= 精英层；10/20/30…（10 的倍数）= BOSS 层（也是存档点）；
    // 其余全普通。排布只由层号决定，同一层号永远同一结果，重进/换机不翻脸。

    /// <summary>该层是否大 BOSS 层（10 的倍数；旧「小 BOSS（5 的倍数非 10）」已改判精英，见 IsEndlessEliteStage）</summary>
    public static bool IsEndlessBigBossStage(int riftStage) => Mathf.Max(1, riftStage) % (ENDLESS_CHECKPOINT_INTERVAL * 2) == 0;

    /// <summary>
    /// 【2026-11-15 主人口径「不是每一层都开宝箱 只有每5和每10是开宝箱」】
    /// 5 的倍数层（精英 5/15/25… + BOSS 存档层 10/20/30…）才开宝箱；其余普通层
    /// 打完直接结算 → 弹一条金币 toast → 立刻进下一层，不摆宝箱、不播任何演出。
    /// 真源只此一处；消费点 = StageClearRewardDirector.CoReward（无限裂隙唯一结算出口）。
    /// </summary>
    public static bool IsEndlessChestStage(int riftStage)
        => Mathf.Max(1, riftStage) % ENDLESS_CHECKPOINT_INTERVAL == 0;

    // —— 裂隙表定词条怪投放（2026-11-15 主人口径）——
    // 「把普通关卡的词条怪按照一定比例分别加到每层里面 前面少后面多 越往后普通怪越少」：
    // 主线的 AffixQuotaRange(关序号) 对裂隙不适用（裂隙 layer 映射进 0..4 的输入域，前 18 层恒 0 配额），
    // 所以裂隙单独按**绝对层数**给一条连续比例曲线。真源只此一处，消费点 = WavePlanner.Picks.InitAffixQuota。
    public const float ENDLESS_AFFIX_RATIO_START = 0.05f;   // 第 1 层 5%（裂隙独立曲线；主线 AFFIX_QUOTA_RATIO 11-15 起改 25%，不联动）
    public const float ENDLESS_AFFIX_RATIO_STEP = 0.0075f;  // 每层 +0.75 个百分点
    public const float ENDLESS_AFFIX_RATIO_CAP = 0.40f;     // 47 层起封顶 40%，之后普通怪占六成
    public static float EndlessAffixRatio(int riftStage)
        => Mathf.Min(ENDLESS_AFFIX_RATIO_CAP,
                     ENDLESS_AFFIX_RATIO_START + (Mathf.Max(1, riftStage) - 1) * ENDLESS_AFFIX_RATIO_STEP);

    // 「小怪的属性也要增加」：普通怪变少，单体也要随层数变强。只作用裂隙的普通小怪槽
    // （含表定词条怪），精英 / BOSS 本体不吃 —— 11-15 已拍的 ENDLESS_ELITE_HP_MUL=4f /
    // ENDLESS_BIGBOSS_HP_MUL=0.96f 两条锚点保持原样，不叠乘、不重算。
    public const float ENDLESS_MOB_STAT_STEP = 0.01f;       // 每层 +1%
    public const float ENDLESS_MOB_STAT_CAP = 1.50f;        // 50 层起封顶 ×1.5
    public static float EndlessMobStatMul(int riftStage)
        => Mathf.Min(ENDLESS_MOB_STAT_CAP,
                     1f + (Mathf.Max(1, riftStage) - 1) * ENDLESS_MOB_STAT_STEP);

    /// <summary>大 BOSS 体型：小 BOSS 基础上再乘的系数（手感不对只改这里）</summary>
    public const float ENDLESS_BIGBOSS_SCALE_EXTRA = 1.15f;
    /// <summary>
    /// 大 BOSS 血量：小 BOSS 基础上再乘的系数（手感不对只改这里）。
    /// 【2026-11-15】1.6 → 0.96（降 40%）：第 10 层 Boss 相对第 5 层精英跨度过大，
    /// 与 ENDLESS_ELITE_HP_MUL 的抬升一起把 5→10 层落差压平。
    /// 这是全节共用系数，10/20/30… 所有大 BOSS 高潮同步降一档，但仍是本节最高点。
    /// </summary>
    public const float ENDLESS_BIGBOSS_HP_MUL = 0.96f;

    /// <summary>
    /// 每 10 层块内的精英层数（2~4）。块序号越深，摇到 3/4 的概率越大：
    /// 第 1 块基本 2 个，10 块以后大概率 3~4 个。
    /// </summary>
    public static int EndlessEliteCountInBlock(int blockIndex)
    {
        var rng = new System.Random((int)(blockIndex * 73856093L + 19349663L));
        int extra = 0;
        if ((float)rng.NextDouble() < Mathf.Min(0.9f, 0.25f + blockIndex * 0.05f)) extra++;
        if ((float)rng.NextDouble() < Mathf.Min(0.9f, 0.05f + blockIndex * 0.04f)) extra++;
        return 2 + Mathf.Min(2, extra);
    }

    /// <summary>
    /// 该层是否精英层：块内（每 10 层一块）从 6 个候选槽位里随机抽 2~4 个，
    /// 同一块内洗牌结果固定（种子只跟块号走），同一层号永远同一结果。
    /// 【2026-11-01 主人拍板「不要一上来就是精英/Boss，隔几关来一次」】节首层恒普通：
    /// 槽 0（第 1/11/21…层，开局层）与槽 5（第 6/16/26…层，记录点 Boss 后第一层）
    /// 不进精英候选 —— 开局和每个记录点之后必是普通关热身，精英夹在节中间。
    /// </summary>
    public static bool IsEndlessEliteStage(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        // 【2026-11-14 主人拍板】固定排布：5/15/25…（5 的倍数且非 10 的倍数）= 精英，
        // 10/20/30… = BOSS，其余全普通。旧的「每 10 层块随机洗牌挑 2~4 个」整条下线
        // （主人原话「我以为只有5是精英10是boss 其他的都是普通」）—— 固定种子洗牌会让
        // 第 2、3 层就变精英，进而因「精英/首领共用 bgm_boss」在第 2 层响起 boss 曲。
        // EndlessEliteCountInBlock 保留不删（下线≠删除），已无调用点。
        return stage % ENDLESS_CHECKPOINT_INTERVAL == 0 && !IsEndlessBossStage(stage);
    }

    /// <summary>
    /// 无限裂隙怪量映射：绝对层数 → 主线怪量公式的输入域（0..STAGES_PER_GRAND-1）。
    /// 每 9 层走满一档，37 层走满整条 10→28 曲线后封顶，怪量只涨不跌。
    /// 【2026-10-23 批 E2】输入域从 0..9 收到 0..4，除数同步 4 → 9，
    /// 保证 37 层封顶时仍是 28 只 —— 与重构前裂隙曲线完全一致。
    /// </summary>
    public static int EndlessMonsterCountStageIdx(int riftStage)
        => Mathf.Min(STAGES_PER_GRAND - 1, Mathf.Max(0, (Mathf.Max(1, riftStage) - 1) / 9));

    /// <summary>
    /// 【2026-11-15 主人口径「每20层换个地图和怪物 按照章节里的来」】层 → 章映射，
    /// 每 20 层按顺序换下一章（第 1-20 层森林 → 21-40 墓园 → … → 8 章走完循环）。
    /// 单一真源：ChapterManager.StartEndlessRift 的 currentChapter（怪物池）、续层 Loading 背景、
    /// 进本 targetChapter 三处都读它，别在别处再算一遍。
    /// </summary>
    public const int ENDLESS_CHAPTER_INTERVAL = 20;
    public static int EndlessChapterForStage(int riftStage)
        => ((Mathf.Max(1, riftStage) - 1) / ENDLESS_CHAPTER_INTERVAL) % ChapterMonsterFolders.Length + 1;

    // —— 首通奖励（金币走局内钱包、天赋石直进钱包）——
    // 【2026-11-02 主人拍板·奖励随层数加速】设计意图（主人原话口径）：
    //   「前期可以打好几次，后期每次最多 1-2 层，但获得奖励会比前期多」
    //   → 奖励必须**超线性**增长，否则深层一层给的还没浅层一层多，玩家没理由往深处冒险。
    // 曲线 = 30 + 2×层 + 0.25×层²（二次项负责拉开深潜收益）：
    //   10 层 75 / 20 层 170 / 30 层 315 / 50 层 755（实际发多少由 endless_reward.csv 的 firstGoldMul
    //   倍率决定：普通 ×1、精英 ×2、BOSS 层 0 = 不发金币，2026-11-14 主人拍板「boss层不给金币」）。
    //   锚点校验：50 层普通层单层 755+106 宝箱 = 861 ≈ 前期一次推 10 层的累计 917 ——
    //   「后期打一层 ≈ 前期打一整次」，正是主人要的节奏对价。
    // 总量控制（按表倍率折算后）：普通层全额 / 精英层双倍 / BOSS 层 0，经济不失控。
    //   对照基准：主线全通 8 章 ≈ 4,580 金、左列天赋树全树 ≈ 75,180 金
    //   → 30 层（普通玩家天花板）≈ 主线 1.6 倍；50 层（顶级）≈ 天赋树 32%，不失控。
    // ⚠ 旧公式「60+25×层」实测 50 层发 6.08 万（叠加成约 8 万）= 一局点满天赋树，已作废。
    // ⚠ 天赋石不跟着改：50 层 295 颗 vs 右列天赋全解锁 2,298 颗 = 13%，量级正常，别顺手动。
    /// <summary>首通金币公式 = 30 + 2×层 + 0.25×层²（50 层 755）；实际发放 = 公式 × 表倍率
    /// （endless_reward.csv firstGoldMul，见 <see cref="EndlessRewardMul"/>）</summary>
    public static int EndlessFirstClearGold(int riftStage)
    {
        int s = Mathf.Max(1, riftStage);
        return 30 + 2 * s + Mathf.RoundToInt(0.25f * s * s);
    }
    /// <summary>普通层首通天赋石 = 2 + 层/10（整除）</summary>
    public static int EndlessFirstClearTalent(int riftStage) => 2 + Mathf.Max(1, riftStage) / 10;
    /// <summary>
    /// 裂隙首通**金币**倍率 —— 表驱动（endless_reward.csv 的 firstGoldMul 列，2026-11-14 主人拍板
    /// 「直接配个表 以后直接改表就行」），缺表回退公式行：普通 ×1 / 精英 ×2 / BOSS 层 **0（不发金币）**。
    /// 乘公式 30+2×层+0.25×层² 得绝对值。
    /// </summary>
    public static int EndlessRewardMul(int riftStage)
        => Mathf.RoundToInt(EndlessRewardTable.RowOf(riftStage).firstGoldMul);

    /// <summary>
    /// 裂隙首通**天赋石**倍率 —— 表驱动（talentMul 列）：普通 ×0 / 精英 ×0（精英只发金币+徽记）/ BOSS ×3。
    /// 【2026-11-15 主人口径「只有每5层和每10层的奖励特殊，其他的都是金币」】普通层 1 → 0，
    /// 天赋石只归 10 层档；乘公式 2+层/10 得绝对值。
    /// </summary>
    public static int EndlessRewardTalentMul(int riftStage)
        => Mathf.RoundToInt(EndlessRewardTable.RowOf(riftStage).talentMul);

    /// <summary>
    /// 裂隙**复刷金币**倍率 —— 表驱动（repeatMul 列）：普通/精英 0.3、BOSS 层 0（复刷同样不给金币）。
    /// 乘公式 30+2×层+0.25×层² 得绝对值。
    /// </summary>
    public static float EndlessRepeatGoldMul(int riftStage)
        => EndlessRewardTable.RowOf(riftStage).repeatMul;

    /// <summary>
    /// BOSS 存档层（10 的倍数）钻石 —— 表驱动（diamond 列）：10 层 10 → 20 层 15 → 30 层 20…（表尾外每档 +5）。
    /// 主线 / 活动本的 Boss 关不走这条（维持 StageClearRewardDirector.BossChestDiamond=10）。
    /// </summary>
    public static int EndlessBossDiamond(int riftStage)
        => EndlessRewardTable.RowOf(riftStage).diamond;

    /// <summary>
    /// BOSS 存档层加发的普通招募卷 —— 表驱动（scroll 列）。
    /// 【2026-11-15 主人口径「每10层掉2个」】每 10 层固定 2 个，不再随档递增。
    /// </summary>
    public static int EndlessBossScrollCount(int riftStage)
        => EndlessRewardTable.RowOf(riftStage).scroll;

    /// <summary>
    /// 【2026-11-15 主人口径「招募卷越往后越高级」「50层以后再加1个高级的」】
    /// BOSS 存档层额外加发的高级（传奇）招募卷 —— 表驱动（scrollAdvanced 列）：50 层起每 10 层 1 个。
    /// 深层想再加档直接改表，不在代码里写第二套口径。
    /// </summary>
    public static int EndlessBossAdvancedScrollCount(int riftStage)
        => EndlessRewardTable.RowOf(riftStage).scrollAdvanced;

    /// <summary>
    /// 精英层（5/15/25…）徽记轮换 —— 表驱动（badgeJob/badgeTier 列）：
    /// 每 10 层换一个职业（剑盾→游侠→法师→牧师→重武→狂战循环），每轮换完一遍升档
    /// （普通→稀有→传奇封顶）。数量走 <see cref="EndlessEliteBadgeCount"/>。
    /// </summary>
    public static void EndlessEliteBadge(int riftStage, out string jobKey, out string tier)
    {
        var row = EndlessRewardTable.RowOf(riftStage);
        jobKey = row.badgeJob;
        tier = row.badgeTier;
    }

    /// <summary>精英层徽记每层发放数量 —— 表驱动（badgeCount 列，当前 5）。</summary>
    public static int EndlessEliteBadgeCount(int riftStage)
        => EndlessRewardTable.RowOf(riftStage).badgeCount;

    /// <summary>
    /// 宝箱金币（每次通关都发，可重复小额）—— 表驱动（chestGold 列）：
    /// 0 = 本档不发（BOSS 层拍板不给金币）；&gt;0 = 每层固定发这个数；-1 = 走公式 6+2×层（普通/精英默认）。
    /// </summary>
    public static int EndlessChestGold(int riftStage)
    {
        int cfg = EndlessRewardTable.RowOf(riftStage).chestGold;
        if (cfg == 0) return 0;
        if (cfg > 0) return cfg;
        return 6 + 2 * Mathf.Max(1, riftStage);
    }

    /// <summary>
    /// 【2026-11-15 主人拍板「每10层金币+10%」】按 10 层一档给金币奖励加成：
    /// 1-10 层 ×1.0，11-20 层 ×1.1，21-30 层 ×1.2 …（线性叠加，不封顶）。
    /// 单一出口，只由 SettleEndlessStageClear 在**发放前**乘，保证入账额 = 提示额。
    /// BOSS 层按 11-14「boss层不给金币」口径本身不发通关金币，本系数作用于会发金币的层。
    /// </summary>
    public static float EndlessGoldDecadeMul(int riftStage)
        => 1f + 0.1f * ((Mathf.Max(1, riftStage) - 1) / ENDLESS_SAVE_INTERVAL);

    // —— Boss 掉落卷轴（2026-10-17 主人拍板：Boss 有几率掉卷轴，高级卷轴几率很低）——
    /// <summary>Boss 击杀掉「普通卷轴」的几率（招募稀有佣兵用）</summary>
    public const float BOSS_SCROLL_DROP_RATE = 0.10f;
    /// <summary>Boss 击杀掉「高级招募卷轴」的几率（招募传奇佣兵用，几率很低）</summary>
    public const float BOSS_ADV_SCROLL_DROP_RATE = 0.02f;
    /// <summary>无限裂隙每 10 层固定宝箱加发的卷轴数量</summary>
    public const int ENDLESS_CHEST_SCROLL_INTERVAL = 10;

    /// <summary>BOSS 本体数量</summary>
    public static int GetBossStageMonsterTotal() => 1;

    /// <summary>
    /// BOSS 关小怪数 = 同关随机总数 − 1。
    /// ⚠ 这只是「兜底」：WavePlanner.ResolveBossMinionTotal 优先读 stage_spawn.csv 的 Boss 行，
    /// 表里配了正数就走表，表没配（0 / 空）才回来用这个公式。
    /// </summary>
    public static int GetBossStageMinionTotal(int stageIndex0Based, int chapter)
        => Mathf.Max(8, GetStageMonsterTotal(stageIndex0Based, chapter) - GetBossStageMonsterTotal());

    /// <summary>
    /// 建议波次数：先按「总数 / 单波上限」估，再受刷怪点数量与 [MIN,MAX] 约束。
    /// </summary>
    public static int GetSuggestedWaveCount(int totalMonsters, int spawnPointCount)
    {
        int byTotal = Mathf.CeilToInt(totalMonsters / (float)WAVE_MONSTER_MAX);
        // 2026-09-29：开头不再强拉 5 波 —— 教学关就该是全场最短（10 只 / 3 波），
        // 旧的「开局 5 波」是跟着「第 1 关怪最多」一起来的，那条曲线已经废了。
        int waveMin = STAGE_WAVE_MIN;
        byTotal = Mathf.Clamp(byTotal, waveMin, STAGE_WAVE_MAX);
        if (spawnPointCount <= 0)
            return byTotal;
        int byPoints = Mathf.Clamp(spawnPointCount, STAGE_WAVE_MIN, STAGE_WAVE_MAX);
        return Mathf.Clamp(Mathf.Min(byPoints, byTotal), STAGE_WAVE_MIN, STAGE_WAVE_MAX);
    }

    /// <summary>
    /// 把总怪数尽量均匀分到各波；余数优先给前几波。
    /// </summary>
    public static int[] DistributeMonstersToWaves(int totalMonsters, int waveCount)
    {
        waveCount = Mathf.Clamp(waveCount, 1, STAGE_WAVE_MAX);
        int minTotal = waveCount * WAVE_MONSTER_MIN;
        int maxTotal = waveCount * WAVE_MONSTER_MAX;
        totalMonsters = Mathf.Clamp(totalMonsters, minTotal, maxTotal);

        int[] counts = new int[waveCount];
        int remaining = totalMonsters;
        for (int i = 0; i < waveCount; i++)
        {
            int wavesLeft = waveCount - i;
            int minLeave = (wavesLeft - 1) * WAVE_MONSTER_MIN;
            int maxGive = Mathf.Min(WAVE_MONSTER_MAX, remaining - minLeave);
            int want = Mathf.CeilToInt(remaining / (float)wavesLeft);
            counts[i] = Mathf.Clamp(want, WAVE_MONSTER_MIN, maxGive);
            remaining -= counts[i];
        }
        return counts;
    }

    [Header("渐进式怪物解锁")]
    /// <summary>每章怪物总类型数</summary>
    public const int MONSTERS_PER_CHAPTER = 12;
    /// <summary>BOSS类型的起始编号（10-12为BOSS，不会被当做小怪）</summary>
    /// <remarks>【2026-11-15 主人拍板·怪物结构定稿】每章 1~2 普通小怪 / 3~6 词条怪 / 7~9 精英 / 10~12 Boss，
    /// 8 章结构完全同构。10 号由原「高阶小怪」并入 Boss 段，唯一真源只此一处。
    /// 【同日 xiaoboss 定稿】10 号 = xiaoboss（小 Boss）：minWave=0 ⇒ 5/8 节短章末关也有 Boss 模板；
    /// 11/12 = 真 Boss：minWave=9 ⇒ 仅 10 节章末关可取。分层由 GetWaveMonsterPool 的 minWave 过滤天然实现，
    /// 不新增判定、不新增类别标记；主线用不到的 11/12 形象由副本/无限裂隙 Boss 波复用。</remarks>
    public const int BOSS_SPRITE_START = 10;
    /// <summary>首次通关可出现的最大怪物类型编号（前4-5种）</summary>
    public const int TIER0_MAX_SPRITE = 5;
    /// <summary>通关2-3次后可出现的最大怪物类型编号（前7-8种）</summary>
    public const int TIER1_MAX_SPRITE = 8;
    /// <summary>通关4次以上可出现的最大怪物类型编号（前10种；10=xiaoboss，11/12=真Boss，均被普通池过滤）</summary>
    public const int TIER2_MAX_SPRITE = 10;
    /// <summary>Tier1解锁所需的通关次数</summary>
    public const int TIER1_UNLOCK_CLEARS = 2;
    /// <summary>Tier2解锁所需的通关次数</summary>
    public const int TIER2_UNLOCK_CLEARS = 4;

    /// <summary>
    /// 获取章节对应的怪物精灵路径前缀
    /// </summary>
    public static string GetMonsterSpritePath(int chapter)
    {
        string folder = ChapterThemeMapTable.HasData
            ? ChapterThemeMapTable.GetFolderName(chapter)
            : ChapterMonsterFolders[Mathf.Clamp(chapter - 1, 0, ChapterMonsterFolders.Length - 1)];
        return "2D Pixel RPG Monster Pack/Icons/default size/no shadow/" + folder + "/";
    }

    /// <summary>
    /// 游戏章节 → 怪物素材章节号（恒等映射：游戏章 = 素材章 = 文件夹序号）。
    /// </summary>
    public static int GetMonsterChapter(int gameChapter)
    {
        if (ChapterThemeMapTable.HasData)
            return ChapterThemeMapTable.GetMonsterChapter(gameChapter);

        int idx = Mathf.Clamp(gameChapter - 1, 0, ChapterMonsterFolders.Length - 1);
        string folder = ChapterMonsterFolders[idx];
        int spaceIdx = folder.IndexOf(' ');
        if (spaceIdx > 0 && int.TryParse(folder.Substring(0, spaceIdx), out int ch))
            return ch;
        return gameChapter;
    }

    /// <summary>
    /// 强制设置 GameObject 的世界坐标
    /// 兼容 RectTransform 根节点（用户预制体常见）：通过 anchoredPosition3D 设置，避免 transform.position 不生效
    /// </summary>
    public static void SetWorldPosition(GameObject go, Vector3 worldPos)
    {
        if (go == null) return;
        SetWorldPosition(go.transform, worldPos);
    }

    /// <summary>
    /// 强制设置 Transform 的世界坐标（RectTransform 用 anchoredPosition3D）
    /// </summary>
    public static void SetWorldPosition(Transform t, Vector3 worldPos)
    {
        if (t == null) return;

        // 挂在战斗 unit 世界根下时：即使有 RectTransform 也直接写 position，
        // 避免 anchoredPosition 与 Rigidbody2D 不同步导致「看得见却打不到」
        bool underCanvas = IsUnderCanvas(t);
        if (t is RectTransform rt && underCanvas)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector3 localPos = t.parent != null ? t.parent.InverseTransformPoint(worldPos) : worldPos;
            rt.anchoredPosition3D = localPos;
        }
        else
        {
            t.position = worldPos;
        }

        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.position = new Vector2(worldPos.x, worldPos.y);
        }
    }

    static bool IsUnderCanvas(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            if (p.GetComponent<Canvas>() != null) return true;
            p = p.parent;
        }
        return false;
    }
}
