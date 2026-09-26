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
    /// <summary>无刷怪点时，波与波之间的世界距离（兼容旧逻辑）</summary>
    public const float VIRTUAL_WAVE_SPACING = 4.2f;

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

    // —— 连杀续杯（R2）：连杀窗口内击杀回血，让"连"成为可持续资源 ——
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

    /// <summary>暴击前摇慢放：timeScale 与真实等待秒数（暴击 / Boss·精英致死前摇）</summary>
    public const float CRIT_WINDUP_TIME_SCALE = 0.1f;
    public const float CRIT_WINDUP_UNSCALED = 0.5f;
    /// <summary>暴击前摇跳起时玩家根节点统一放大倍率，下落/命中瞬间还原。</summary>
    public const float CRIT_WINDUP_HERO_SCALE = 1.5f;
    /// <summary>暴击击杀死亡后倒视觉滑动（世界单位，仅程序化死亡 tween）</summary>
    public const float CRIT_KILL_DEATH_SLIDE = 0.15f;
    /// <summary>普通击杀死亡后倒滑动（短于暴击）</summary>
    public const float DEATH_SLIDE_NORMAL = 0.06f;
    /// <summary>精英/Boss 击杀全屏压暗时长（unscaled）</summary>
    public const float KILL_FINISHER_DIM_DUR = 0.25f;
    public const float KILL_FINISHER_DIM_ALPHA = 0.55f;

    /// <summary>击杀镜头拉近（orthoSize 倍率，&lt;1 拉近）；独立开关 COMBAT_JUICE_KILL_CAM</summary>
    public const float KILL_CAM_ZOOM_MUL = 0.82f;
    public const float KILL_CAM_ZOOM_IN = 2.0f;
    public const float KILL_CAM_ZOOM_OUT = 0.07f;
    /// <summary>远程（弓/法球）击杀短前摇真实秒数</summary>
    public const float KILL_CAM_RANGED_WINDUP = 0.14f;
    public static bool COMBAT_JUICE_KILL_CAM = false;

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
    public const float COMBO_AFTERIMAGE_INTERVAL_SLOW = 0.12f;
    public const float COMBO_AFTERIMAGE_INTERVAL_FAST = 0.05f;
    public const float COMBO_AFTERIMAGE_LIFE = 0.24f;
    public const float COMBO_AFTERIMAGE_ALPHA = 0.45f;
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
    /// 关卡总怪数：第一章第一关略多、拉长战斗；前两关 10~15；
    /// 之后按进度抬高，并在区间内随机，章末附近可到 30~35。
    /// </summary>
    public static int GetStageMonsterTotal(int stageIndex0Based)
    {
        int stageNo = Mathf.Max(1, stageIndex0Based + 1);
        if (IsOpeningStage() || (stageNo == 1 && (ChapterManager.Instance == null || ChapterManager.Instance.currentChapter <= 1)))
            return Mathf.Max(1, Mathf.RoundToInt(Random.Range(16, 23) * 0.8f));
        if (stageNo <= 2)
            return Mathf.Max(1, Mathf.RoundToInt(Random.Range(10, 16) * 0.8f));

        float t = Mathf.Clamp01((stageNo - 1) / 9f);
        int lo = Mathf.RoundToInt(Mathf.Lerp(14, 28, t));
        int hi = Mathf.RoundToInt(Mathf.Lerp(18, 35, t));
        if (hi < lo) hi = lo;
        return Mathf.Max(1, Mathf.RoundToInt(Random.Range(lo, hi + 1) * 0.8f));
    }

    /// <summary>普通关总怪数</summary>
    public static int GetNormalStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

    /// <summary>精英关总怪数（同随机曲线）</summary>
    public static int GetEliteStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

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
        int waveMin = IsOpeningStage() ? 5 : STAGE_WAVE_MIN;
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
