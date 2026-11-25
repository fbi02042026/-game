using UnityEngine;

/// <summary>
/// 动态难度单一出口（2026-11-22）。
///
/// 把主人的四项诉求收敛成一条倍率公式，<b>只有这里算倍率</b>，其它地方只改状态、不算数值：
///   ① 整体强度 +10%          —— 静态基准，全玩法（引导局除外，沿用「引导的不要动」）。
///   ② 每死一次就放水          —— 【2026-11-25 主人改】门槛 3→1、单档 -20%→每死 -12% 叠加（封顶 3 层），
///                               「不能让玩家死的次数太多，容易流失」：第 1 次死就 ×0.88，
///                               第 2 次 ×0.77，第 3 次 ×0.68 封顶，第 4 次必过。
///   ③ 过关后橡皮筋            —— 下两关「提升少点」×0.95，第三关起「提升更大」×1.12（反超，净更难）。
///   ④ 得厉害武器后放水        —— 按战力涨幅判定，接下来两局冒险 ×0.90。
///
/// 【单一出口纪律】倍率只能由 <see cref="GetScale"/> 产生；挂点（OnXxx）只写存档状态。
/// 【范围】竞技场敌人不经 Monster.Init 生成（走 ArenaEnemySpawner + 自己的阀门），
///         本类不叠加到竞技场，避免双重放水；引导局恒 ×1，避免破坏不死人铁律。
/// 【状态落点】SaveData 的 adaptive* 字段；老档默认 0 = 无动态修正，行为与改动前一致。
/// </summary>
public static class AdaptiveDifficulty
{
    // ===== 全部可调参数集中在此，改手感只动这里 =====
    /// <summary>① 整体强度基准（主人 2026-11-22 拍板 +10%）。</summary>
    public const float BASE_MUL = 1.10f;
    /// <summary>② 该关死过几次才开始放水。【2026-11-25 主人改 3 → 1】第一次死就放水，不留「连死 3 次」的劝退窗口。</summary>
    public const int FAIL_EASE_THRESHOLD = 1;
    /// <summary>② 每死一次的单层放水倍率（-12%），按失败次数叠加，不是固定一档。</summary>
    public const float FAIL_EASE_MUL = 0.88f;
    /// <summary>② 放水最多叠几层（3 层 = ×0.681）；再死不再更软，第 4 次基本必过，防「永远打不过」。</summary>
    public const int FAIL_EASE_MAX_STACK = 3;
    /// <summary>③ 过关后缓坡倍率（「提升少点」）。</summary>
    public const float RUBBER_EASE_MUL = 0.95f;
    /// <summary>③ 缓坡持续关数（「下两关」）。</summary>
    public const int RUBBER_EASE_STAGES = 2;
    /// <summary>③ 陡坡倍率（「第三关起提升更大」，反超到净更难）。</summary>
    public const float RUBBER_STEEP_MUL = 1.12f;
    /// <summary>③ 陡坡持续关数。</summary>
    public const int RUBBER_STEEP_STAGES = 3;
    /// <summary>④ 战力涨幅达此比例视为「厉害武器」（换装战力增量 ÷ 当前总战力）。</summary>
    public const float WEAPON_GAIN_RATIO = 0.15f;
    /// <summary>④ 强武器放水倍率（-10%）。</summary>
    public const float WEAPON_EASE_MUL = 0.90f;
    /// <summary>
    /// ④ 放水局数。设为 3 = 「当局残段（即时见效）+ 之后两次<span>完整</span>进本」：
    /// 获得时置 3 → 当局残段吃到 1 份 → 下一次进本扣成 2 → 再下一次扣成 1 → 第三次进本已扣成 0。
    /// 2026-11-22 主人拍板「按两整局算」，故为 3 而非 2（2 只能覆盖当局残段 + 一局）。
    /// </summary>
    public const int WEAPON_EASE_RUNS = 3;

    // 橡皮筋阶段：0 无 / 1 缓坡 / 2 陡坡
    const int PhaseNone = 0;
    const int PhaseEase = 1;
    const int PhaseSteep = 2;

    static SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    /// <summary>
    /// <b>唯一倍率出口</b>：怪物最终血/攻/防都乘这里返回的值。
    /// 引导局恒 1；无存档时只吃静态基准（保证老档/异常档也有 +10%，行为可预期）。
    /// </summary>
    public static float GetScale()
    {
        var bm = BattleManager.Instance;
        // 引导局恒 1：护住「引导不死人」铁律（与现有引导豁免同口径）
        if (bm != null && bm.IsTutorialRun) return 1f;
        var d = Data;
        if (d == null) return BASE_MUL;

        float mul = BASE_MUL;

        // ② 每死一次叠一层放水（按关卡 key 存，过了这关就清）；封顶 FAIL_EASE_MAX_STACK 层，之后再死不再更软
        int fails = GetFail(d, CurrentKey());
        if (fails >= FAIL_EASE_THRESHOLD)
            mul *= Mathf.Pow(FAIL_EASE_MUL, Mathf.Min(fails, FAIL_EASE_MAX_STACK));

        // ③ 过关后的橡皮筋：先缓两关，再陡三关。
        // 【2026-11-22 主人拍板「按 mode 隔离」】按当前玩法各存一份，活动副本/裂隙通关不会推进主线橡皮筋。
        int rubberPhase = GetRubberPhase(d, CurrentMode());
        if (rubberPhase == PhaseEase) mul *= RUBBER_EASE_MUL;
        else if (rubberPhase == PhaseSteep) mul *= RUBBER_STEEP_MUL;

        // ④ 强武器送暖：剩余放水局数 > 0
        if (d.adaptiveWeaponEaseRuns > 0) mul *= WEAPON_EASE_MUL;

        return mul;
    }

    /// <summary>
    /// <b>图鉴 / 敌人预告口径</b>：只吃静态基准（+10%），不含按局的动态修正（失败放水 / 橡皮筋 / 送暖）。
    /// 理由：图鉴在城镇/编队界面查看，此刻没有「当前是哪一关」的上下文，把动态倍率算进去会随玩家卡关史乱跳。
    /// 图鉴本来就是示意值（攻击列仍取原始表值，不含波次/偏差/后期/词缀），这里只把静态基准对齐，不改结构。
    /// </summary>
    public static float GetPreviewScale() => BASE_MUL;

    /// <summary>
    /// 当前关卡上下文：<b>唯一组装处</b>，与 Monster.Init 取章/节的口径一致。
    /// 挂点无参调用，避免死亡/通关两处各拼一次 key 将来漂移。
    /// </summary>
    static string CurrentKey()
    {
        var bm = BattleManager.Instance;
        var cm = ChapterManager.Instance;
        bool endlessRift = bm != null && bm.IsEndlessRift;
        if (endlessRift) return "rift_" + (bm != null ? bm.EndlessRiftStage : 0);
        int chapter = cm != null ? cm.currentChapter : (bm != null ? bm.CurrentChapter : 1);
        int stageIdx0 = cm != null ? cm.currentStageIndex : 0;
        int diff = bm != null ? bm.BattleDifficulty : 0;
        return CurrentMode() + "_" + diff + "_" + chapter + "_" + stageIdx0;
    }

    /// <summary>
    /// 当前玩法身份（<see cref="IBattleMode.Id"/>）：橡皮筋状态的隔离键。
    /// 城镇无 BattleManager 时回 "mode0"，与 <see cref="CurrentKey"/> 同一口径，不新增第二套判定。
    /// </summary>
    static string CurrentMode()
    {
        var bm = BattleManager.Instance;
        return bm != null && bm.Mode != null ? bm.Mode.Id.ToString() : "mode0";
    }

    static int GetFail(SaveData d, string key)
    {
        if (d == null || d.adaptiveFailStreak == null || string.IsNullOrEmpty(key)) return 0;
        for (int i = 0; i < d.adaptiveFailStreak.Count; i++)
            if (d.adaptiveFailStreak[i] != null && d.adaptiveFailStreak[i].id == key)
                return d.adaptiveFailStreak[i].value;
        return 0;
    }

    static void SetFail(SaveData d, string key, int value)
    {
        if (d == null || string.IsNullOrEmpty(key)) return;
        if (d.adaptiveFailStreak == null) d.adaptiveFailStreak = new System.Collections.Generic.List<StringIntEntry>();
        for (int i = 0; i < d.adaptiveFailStreak.Count; i++)
            if (d.adaptiveFailStreak[i] != null && d.adaptiveFailStreak[i].id == key)
            {
                d.adaptiveFailStreak[i].value = value;
                return;
            }
        d.adaptiveFailStreak.Add(new StringIntEntry { id = key, value = value });
    }

    // ===== 橡皮筋状态：按玩法 mode 隔离（活动副本/裂隙通关不推进主线）=====

    static AdaptiveRubberEntry FindRubber(SaveData d, string mode)
    {
        if (d == null || d.adaptiveRubber == null || string.IsNullOrEmpty(mode)) return null;
        for (int i = 0; i < d.adaptiveRubber.Count; i++)
            if (d.adaptiveRubber[i] != null && d.adaptiveRubber[i].mode == mode)
                return d.adaptiveRubber[i];
        return null;
    }

    static int GetRubberPhase(SaveData d, string mode)
    {
        var e = FindRubber(d, mode);
        return e != null ? e.phase : PhaseNone;
    }

    static void SetRubber(SaveData d, string mode, int phase, int left)
    {
        if (d == null || string.IsNullOrEmpty(mode)) return;
        if (d.adaptiveRubber == null) d.adaptiveRubber = new System.Collections.Generic.List<AdaptiveRubberEntry>();
        var e = FindRubber(d, mode);
        if (e == null)
        {
            d.adaptiveRubber.Add(new AdaptiveRubberEntry { mode = mode, phase = phase, left = left });
            return;
        }
        e.phase = phase;
        e.left = left;
    }

    // ===== 挂点：只写状态，不算倍率 =====

    /// <summary>② 死亡结算：当前关连续失败 +1（由「非引导局死亡」唯一出口调用）。</summary>
    public static void OnStageFailed()
    {
        var d = Data;
        if (d == null) return;
        string key = CurrentKey();
        SetFail(d, key, GetFail(d, key) + 1);
    }

    /// <summary>
    /// ③ 通关一关：先按本关失败数决定是否启动橡皮筋，再推进既有阶段。
    /// 由每关通关统一门调用（在 ChapterManager 推进 currentStageIndex 之前取 key）。
    /// </summary>
    public static void OnStageCleared()
    {
        var d = Data;
        if (d == null) return;
        string key = CurrentKey();
        string mode = CurrentMode();
        int fails = GetFail(d, key);
        if (fails > 0) SetFail(d, key, 0);   // 过了这关，失败计数清零（放水只针对卡住的那一关）

        if (fails >= FAIL_EASE_THRESHOLD)
        {
            // 死过至少一次才过关 → 下两关缓坡，之后陡坡反超（只写本玩法那一份）
            SetRubber(d, mode, PhaseEase, RUBBER_EASE_STAGES);
            return;
        }

        // 正常推进橡皮筋倒计时（按本玩法自己的阶段，不串味）
        int phase = GetRubberPhase(d, mode);
        if (phase == PhaseEase)
        {
            var e = FindRubber(d, mode);
            int left = (e != null ? e.left : 0) - 1;
            if (left <= 0) SetRubber(d, mode, PhaseSteep, RUBBER_STEEP_STAGES);
            else SetRubber(d, mode, PhaseEase, left);
        }
        else if (phase == PhaseSteep)
        {
            var e = FindRubber(d, mode);
            int left = (e != null ? e.left : 0) - 1;
            if (left <= 0) SetRubber(d, mode, PhaseNone, 0);
            else SetRubber(d, mode, PhaseSteep, left);
        }
    }

    /// <summary>
    /// ④ 换装<b>前</b>取战力增量（必在 EquipItem 之前调用：新件穿上后当前穿戴已含它，差值会归零）。
    /// 只在战斗局内取（RunLoadout.IsActive），城镇换装不算「下两局」。战力真源仍是 PowerCalc。
    /// </summary>
    public static int PowerGainBeforeEquip(EquipInstance equip)
    {
        if (equip == null || !RunLoadout.IsActive) return 0;
        return PowerCalc.EquipSwapPowerDelta(equip);
    }

    /// <summary>④ 换装成功后：战力涨幅达标 → 接下来两局冒险上送暖。</summary>
    public static void OnEquipGained(int powerGain)
    {
        if (powerGain <= 0) return;
        var d = Data;
        if (d == null) return;

        float total = Mathf.Max(1, RunLoadout.TotalPower());
        if (powerGain < total * WEAPON_GAIN_RATIO) return;

        d.adaptiveWeaponEaseRuns = WEAPON_EASE_RUNS;   // 幂等：连续拿两件厉害武器也只重置为 2
    }

    /// <summary>④ 每次进本消耗一局送暖额度（新局建立时调用）。</summary>
    public static void OnRunStarted()
    {
        var d = Data;
        if (d == null || d.adaptiveWeaponEaseRuns <= 0) return;
        d.adaptiveWeaponEaseRuns--;
    }
}
