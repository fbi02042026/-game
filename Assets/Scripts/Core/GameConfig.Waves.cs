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
    /// 【2026-10-25 主人拍板】正式关每回合波数**按章 × 回合位置定死**（不随机摇）：
    /// ch1 = 1/2/3；ch2 = 每回合 +1 = 2/3/4；ch3 = 再 +1 = 3/4/5；ch4 及以后固定 = 3/4/5。
    /// 主人原话：「第一回合1波 第二回合2波 第三回合3波」→「第二章每回合加一波
    /// 第三章在第二章基础上也加一波 然后以后的章节就固定成第三章的」。
    /// 旧「Min/Max 区间随机摇」整条退役已删（2026-10-25，此前唯一消费点就是定表覆盖处）。
    /// 唯一消费方 <see cref="WavePlanner.RollFormalRoundWaveCounts"/>（普通/精英/Boss 关共用，单一出口）。
    /// </summary>
    public static int[] FormalRoundWaveFixedCounts(int chapter)
    {
        // 每回合波数 = 基数 + 回合下标：基数 ch1=1、ch2=2、ch3 及以后=3
        int first = Mathf.Clamp(Mathf.Max(1, chapter), 1, 3);
        var counts = new int[FORMAL_STAGE_ROUND_COUNT];
        for (int r = 0; r < counts.Length; r++)
            counts[r] = first + r;
        return counts;
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
    /// </summary>
    public const int FORMAL_WAVE_MONSTERS_MIN = 10;
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】正式关每个普通波怪数上限（含）：Random.Range(MIN, MAX+1)。
    /// 总怪量由此 ~90-135/关；精英末波同口径（编成为精英）。同屏峰值仍 ~15（分波刷，性能口径不变）。
    /// </summary>
    public const int FORMAL_WAVE_MONSTERS_MAX = 15;
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

    /// <summary>雷击奥义总开关：关闭时不充能、不自动释放（等后期玩家装备技能后再开）。</summary>
    public static bool THUNDER_ULT_ENABLED = false;

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
    public static int GetStageMonsterTotal(int stageIndex0Based)
    {
        // 【2026-10-23 批 E1】输入域从 0..9 收到 0..4（大关 5 小关）
        int stageNo = Mathf.Clamp(stageIndex0Based + 1, 1, STAGES_PER_GRAND);
        float baseTotal = MONSTER_TOTAL_BASE + (stageNo - 1) * MONSTER_TOTAL_STEP;

        int lo = Mathf.RoundToInt(baseTotal * (1f - MONSTER_TOTAL_JITTER));
        int hi = Mathf.RoundToInt(baseTotal * (1f + MONSTER_TOTAL_JITTER));
        if (hi < lo) hi = lo;
        return Mathf.Max(1, Random.Range(lo, hi + 1));
    }

    /// <summary>普通关总怪数</summary>
    public static int GetNormalStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

    /// <summary>精英关总怪数（同随机曲线）</summary>
    public static int GetEliteStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

    // ============================================================
    // 无限裂隙（2026-10-17 主人拍板）——难度与奖励数值唯一真源
    // ============================================================

    /// <summary>
    /// 无限裂隙强度倍率：每层固定 +7%，记录点后第一层（缓冲关）打 9 折。
    /// 层数 1 → 1.00；层 6（缓冲）→ 1.275；层 40 → 3.43。只随层数单调涨，与循环映射章解耦。
    /// </summary>
    public static float EndlessDifficultyMul(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        float mul = 1f + ENDLESS_DIFFICULTY_STEP_PER_STAGE * (stage - 1);
        if (IsEndlessBufferStage(stage)) mul *= ENDLESS_BUFFER_DISCOUNT;
        return mul;
    }

    /// <summary>每层强度递增步长（占位基准，手感不对只改这里）</summary>
    public const float ENDLESS_DIFFICULTY_STEP_PER_STAGE = 0.07f;
    /// <summary>缓冲关（记录点后第一层）强度折扣</summary>
    public const float ENDLESS_BUFFER_DISCOUNT = 0.9f;
    /// <summary>每天进入次数</summary>
    public const int ENDLESS_DAILY_TRIES = 2;

    /// <summary>记录点间隔：每 5 层一个（第 5/10/15…层 = BOSS 记录点）</summary>
    public const int ENDLESS_CHECKPOINT_INTERVAL = 5;

    /// <summary>该层是否 BOSS 记录点层</summary>
    public static bool IsEndlessBossStage(int riftStage) => Mathf.Max(1, riftStage) % ENDLESS_CHECKPOINT_INTERVAL == 0;

    // —— 层类型排布（2026-10-19 主人拍板）——
    // 每 5 层小 BOSS、每 10 层大 BOSS；精英层每 10 层随机 2~4 个，越深越偏多。
    // 排布只由层号决定（种子哈希），同一层号永远同一结果，重进/换机不翻脸。

    /// <summary>该层是否大 BOSS 层（10 的倍数；小 BOSS 是 5 的倍数但非 10 的倍数）</summary>
    public static bool IsEndlessBigBossStage(int riftStage) => Mathf.Max(1, riftStage) % (ENDLESS_CHECKPOINT_INTERVAL * 2) == 0;

    /// <summary>大 BOSS 体型：小 BOSS 基础上再乘的系数（手感不对只改这里）</summary>
    public const float ENDLESS_BIGBOSS_SCALE_EXTRA = 1.15f;
    /// <summary>大 BOSS 血量：小 BOSS 基础上再乘的系数（手感不对只改这里）</summary>
    public const float ENDLESS_BIGBOSS_HP_MUL = 1.6f;

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
    /// 该层是否精英层：块内（每 10 层一块）从 8 个非 BOSS 槽位里随机抽 2~4 个，
    /// 同一块内洗牌结果固定（种子只跟块号走），同一层号永远同一结果。
    /// </summary>
    public static bool IsEndlessEliteStage(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        if (IsEndlessBossStage(stage)) return false; // 5/10 的倍数是 BOSS，不当精英
        int block = (stage - 1) / 10;
        int slot = (stage - 1) % 10;
        // 块内非 BOSS 槽位（4、9 是小/大 BOSS，排除）
        var candidates = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 5, 6, 7, 8 };
        var rng = new System.Random((int)(block * 83492791L + 297121507L));
        for (int i = candidates.Count - 1; i > 0; i--) // Fisher-Yates 洗牌
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        int count = EndlessEliteCountInBlock(block);
        for (int i = 0; i < count && i < candidates.Count; i++)
            if (candidates[i] == slot) return true;
        return false;
    }

    /// <summary>该层是否缓冲关（记录点后第一层，第 6/11/16…层；第 1 层不算）</summary>
    public static bool IsEndlessBufferStage(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        return stage > 1 && stage % ENDLESS_CHECKPOINT_INTERVAL == 1;
    }

    /// <summary>
    /// 无限裂隙怪量映射：绝对层数 → 主线怪量公式的输入域（0..STAGES_PER_GRAND-1）。
    /// 每 9 层走满一档，37 层走满整条 10→28 曲线后封顶，怪量只涨不跌。
    /// 【2026-10-23 批 E2】输入域从 0..9 收到 0..4，除数同步 4 → 9，
    /// 保证 37 层封顶时仍是 28 只 —— 与重构前裂隙曲线完全一致。
    /// </summary>
    public static int EndlessMonsterCountStageIdx(int riftStage)
        => Mathf.Min(STAGES_PER_GRAND - 1, Mathf.Max(0, (Mathf.Max(1, riftStage) - 1) / 9));

    /// <summary>层 → 背景章映射（每 5 层换一套：第 1-5 层森林 → 6-10 墓园 → … → 40 层后循环）</summary>
    public static int EndlessChapterForStage(int riftStage)
        => ((Mathf.Max(1, riftStage) - 1) / ENDLESS_CHECKPOINT_INTERVAL) % ChapterMonsterFolders.Length + 1;

    // —— 首通奖励（一次性；金币走局内钱包、天赋石直进钱包）——
    /// <summary>普通层首通金币 = 60 + 25×层</summary>
    public static int EndlessFirstClearGold(int riftStage) => 60 + 25 * Mathf.Max(1, riftStage);
    /// <summary>普通层首通天赋石 = 2 + 层/10（整除）</summary>
    public static int EndlessFirstClearTalent(int riftStage) => 2 + Mathf.Max(1, riftStage) / 10;
    /// <summary>BOSS 记录点层首通额外倍率（金币与天赋石同乘）</summary>
    public const int ENDLESS_BOSS_FIRSTCLEAR_MUL = 3;
    /// <summary>宝箱掉落（每次通关都发，可重复小额金币）= 15 + 8×层</summary>
    public static int EndlessChestGold(int riftStage) => 15 + 8 * Mathf.Max(1, riftStage);

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
    public static int GetBossStageMinionTotal(int stageIndex0Based)
        => Mathf.Max(8, GetStageMonsterTotal(stageIndex0Based) - GetBossStageMonsterTotal());

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
    /// <summary>BOSS类型的起始编号（11-12为BOSS，不会被当做小怪）</summary>
    public const int BOSS_SPRITE_START = 11;
    /// <summary>首次通关可出现的最大怪物类型编号（前4-5种）</summary>
    public const int TIER0_MAX_SPRITE = 5;
    /// <summary>通关2-3次后可出现的最大怪物类型编号（前7-8种）</summary>
    public const int TIER1_MAX_SPRITE = 8;
    /// <summary>通关4次以上可出现的最大怪物类型编号（前10种，11-12始终是BOSS）</summary>
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
