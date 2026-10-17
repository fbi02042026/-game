using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 玩家技能能量
    public float[] playerSkillEnergy = new float[Mathf.Max(1, RunLoadout.MaxSkillSlots)];
    public const float MAX_SKILL_ENERGY = 1f;

    /// <summary>读某个技能槽的能量（越界返回 0）。</summary>
    public float GetPlayerSkillEnergy(int slot)
    {
        if (playerSkillEnergy == null) return 0f;
        if (slot < 0 || slot >= playerSkillEnergy.Length) return 0f;
        return playerSkillEnergy[slot];
    }

    /// <summary>写某个技能槽的能量（自动钳到 0~1，并通知 HUD）。</summary>
    public void SetPlayerSkillEnergy(int slot, float value)
    {
        if (playerSkillEnergy == null) return;
        if (slot < 0 || slot >= playerSkillEnergy.Length) return;
        playerSkillEnergy[slot] = Mathf.Clamp(value, 0f, MAX_SKILL_ENERGY);
        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy[slot]);
    }

    /// <summary>全部技能槽能量清零（开战/波次重置用）。</summary>
    public void ClearAllPlayerSkillEnergy()
    {
        if (playerSkillEnergy == null) return;
        for (int i = 0; i < playerSkillEnergy.Length; i++)
            playerSkillEnergy[i] = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
    }

    /// <summary>
    /// 找出一个可以释放的技能槽：能量已满，且该技能不在冷却中。返回 -1 表示没有。
    /// 槽序即优先级 —— 多个同时就绪时取最靠前的那个。
    /// </summary>
    public int FindReadySkillSlot()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return -1;
        // 用 SkillSystem 的本体列表：返回引用不分配（RunLoadout.SkillIds() 每次 new List，
        // 而本方法每帧都会被调用，不能在这里产生 GC）。
        var skills = sys.GetPlayerSkills();
        if (skills == null) return -1;

        int count = Mathf.Min(playerSkillEnergy.Length, skills.Count);
        for (int i = 0; i < count; i++)
        {
            // 纯冷却制下不再看能量，只看冷却（PLAYER_SKILL_USE_ENERGY 置 true 可退回原行为）
            if (GameConfig.PLAYER_SKILL_USE_ENERGY && playerSkillEnergy[i] < MAX_SKILL_ENERGY - 0.001f) continue;
            var s = skills[i];
            if (s == null || string.IsNullOrEmpty(s.skillId)) continue;
            if (sys.IsOnCooldown(s.skillId)) continue;
            // 2026-09-17：冷却好≠该放，还要等技能自己的战场状态触发条件（血线/怪群等）。
            if (!PlayerSkillPassive.IsTriggerMet(s.skillId)) continue;
            // 2026-10-06 主人拍板：蓝不足 = 这一槽本轮不可释放，**顺位给下一个**（与触发条件不满足同一口径）。
            // 无蓝技能（耗蓝 0）永远过得去 = 地板节奏；耗蓝技能才吃稀缺窗口，见设计文档 §9。
            // 2026-10-10：s.skillId 是 ally_* 别名，MpCostOf 按真 id 查表（查不到返回 -1 = 不存在），
            // 用别名查会恒得 -1 → 蓝不够也照样选中该槽，SkillCastService 里扣蓝失败整轮空转，
            // 顺位在后面的无蓝技能（0 蓝）跟着一起被卡死。这里解析成真 id 再判定。
            float mpCost = PlayerSkillDefs.MpCostOf(PlayerSkillDefs.ResolveRealSkillId(s.skillId));
            if (mpCost > 0f && _playerMp + 0.001f < mpCost) continue;
            return i;
        }
        return -1;
    }

    // ============================================================
    // 玩家技能：纯冷却制调度（2026-09-15）
    // 开局按槽位错峰给初始 CD；放完一个后全局间隔 PLAYER_SKILL_GCD 才能放下一个。
    // 用实例字段而非静态字段：随 BattleManager 生命周期自然重置，不会跨局残留。
    // ============================================================

    /// <summary>下次允许释放玩家技能的时间（Time.time 轴）。</summary>
    public float NextPlayerSkillCastAt { get; private set; } = -1f;

    /// <summary>全局释放间隔是否已过（true = 现在可以放）。</summary>
    public bool IsPlayerSkillGcdReady => Time.time >= NextPlayerSkillCastAt;

    /// <summary>释放成功后调用：上膛，接下来 PLAYER_SKILL_GCD 秒内不再放。</summary>
    public void ArmPlayerSkillGcd() => NextPlayerSkillCastAt = Time.time + GameConfig.PLAYER_SKILL_GCD;

    /// <summary>立即解除间隔限制（开战/换关时用）。</summary>
    public void ResetPlayerSkillGcd() => NextPlayerSkillCastAt = Time.time;

    /// <summary>
    /// 开局错峰：按槽位给每个玩家技能挂一段初始冷却，避免 4 个技能同时就绪一起炸出来。
    /// 第 1 槽固定 0.5 秒（最快登场），2~4 槽 = 自身 CD × 0.33 / 0.66 / 1.0。
    /// 只在这关刚开始时调用一次（LoadStage / StartNewRun），不要在升星或拖拽排序时调用
    /// ——那会把战斗中已经跑掉的冷却重置。
    /// </summary>
    public void PrimePlayerSkillOpeningCooldowns()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return;
        var skills = sys.GetPlayerSkills();
        if (skills == null) return;

        for (int i = 0; i < skills.Count && i < GameConfig.PLAYER_SKILL_OPENING_CD_MUL.Length; i++)
        {
            var s = skills[i];
            if (s == null || string.IsNullOrEmpty(s.skillId)) continue;
            if (sys.IsOnCooldown(s.skillId)) continue;   // 已有更长冷却的不覆盖

            // 2026-09-27 主人拍板：**每个技能进战都要先走一遍自己的完整冷却**，不许一上来就放。
            // 原实现：第 1 槽固定 0.5s、2~4 槽只跑自身 CD 的 0.33/0.66/1.0 倍 —— 等于刚进战就能放，
            // 主人反馈「玩家的技能不要一上来就释放」。现在统一 = 技能自身 CD；
            // 各技能 CD 本来就不一样，天然错峰，不再需要 PLAYER_SKILL_OPENING_CD_MUL 那套倍率
            // （常量保留不删，但本函数已不使用，别再拿它改回错峰）。
            float cd = s.cooldown;
            if (cd > 0.01f) sys.RegisterCooldown(s.skillId, cd);
        }
    }

    /// <summary>
    /// 佣兵蓝条 MP（最多 2 槽）。2026-10-06 主人拍板：由「技能能量」改为真正的蓝条，
    /// 按<b>槽位</b>存（换佣兵不回满，防「换人刷蓝」）。
    /// </summary>
    readonly float[] mercMp = new float[2];
    /// <summary>本关该槽是否已灌满过：每关开局回满一次，之后只靠自然回复。</summary>
    readonly bool[] _mercMpFilled = new bool[2];
    #endregion
    #region 佣兵技能与施放
    internal Coroutine _spawnWaveCo;
    internal int _offscreenEnterSideToggle;
    SkillCastService _skillCast;
    internal SkillCastService SkillCast => _skillCast ?? (_skillCast = new SkillCastService(this));

    /// <summary>取第 index 个在场佣兵（越界 / 无管理器 → null）。</summary>
    static Mercenary MercAt(int index)
    {
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null || index < 0 || index >= mercs.Count) return null;
        return mercs[index];
    }

    /// <summary>该槽 MP 池上限（该佣兵是「无蓝」技能 → 0，蓝条不显示）。</summary>
    public static float MercMpPool(int index)
    {
        var m = MercAt(index);
        if (m == null) return 0f;
        return MpProfile.Pool(MpProfile.OfMercSkill(m.equippedSkillId), m.mercLevel, m.mercStar);
    }

    /// <summary>该槽每秒自然回复（该佣兵是「无蓝」技能 → 0）。</summary>
    public static float MercMpRegen(int index)
    {
        var m = MercAt(index);
        if (m == null) return 0f;
        return MpProfile.Regen(MpProfile.OfMercSkill(m.equippedSkillId), m.mercStar);
    }

    /// <summary>
    /// 佣兵槽蓝条比例 0~1（当前蓝 ÷ 池上限），喂 UI 用。
    /// 槽未启用 / 该佣兵技能无蓝 → 0（蓝条由 UI 侧隐藏）。
    /// </summary>
    public float GetMercMp(int index)
    {
        if (index < 0 || index >= mercMp.Length) return 0f;
        float pool = MercMpPool(index);
        if (pool <= 0f) return 0f;
        return Mathf.Clamp01(mercMp[index] / pool);
    }

    /// <summary>
    /// 扣蓝的<b>唯一出口</b>：蓝够才扣并返回 true；蓝不够 / 槽位非法 → false（调用方不放技能）。
    /// cost &lt;= 0 视为无蓝技能，直接放行、不扣蓝。
    /// </summary>
    public bool TrySpendMercMp(Mercenary merc, float cost)
    {
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null || merc == null) return false;
        int slot = mercs.IndexOf(merc);
        if (slot < 0 || slot >= mercMp.Length) return false;
        if (cost <= 0f) return true;
        if (mercMp[slot] + 0.001f < cost) return false;
        mercMp[slot] = Mathf.Max(0f, mercMp[slot] - cost);
        BattleUI.Instance?.UpdateSkillEnergy(slot + 1, GetMercMp(slot));
        return true;
    }

    // ============================================================
    // 玩家蓝条（2026-10-06 主人拍板：与佣兵同一套 MpProfile，4 个技能共用一条）
    // ============================================================

    /// <summary>玩家当前蓝量。</summary>
    float _playerMp;
    /// <summary>本关是否已灌满过（每关开局回满，与佣兵同口径）。</summary>
    bool _playerMpFilled;

    /// <summary>
    /// 玩家本局有没有蓝：4 个技能里<b>只要有一个耗蓝 &gt; 0</b> 就有。
    /// 与佣兵同口径 —— 按「技能本身」判，不按职业，避免「职业有蓝但技能不耗蓝」的孤儿配置。
    /// </summary>
    public static bool PlayerHasMp()
    {
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        if (ids == null) return false;
        for (int i = 0; i < ids.Count; i++)
            if (MpProfile.OfPlayerSkill(ids[i]) != MpArchetype.None) return true;
        return false;
    }

    /// <summary>玩家蓝条星级：4 技共用一条蓝 → 按已装备技能里<b>最高</b>的那个星算。</summary>
    static int PlayerMpStar()
    {
        int star = 1;
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        if (ids == null) return star;
        for (int i = 0; i < ids.Count; i++)
            star = Mathf.Max(star, RunLoadout.StarOf(ids[i]));
        return star;
    }

    /// <summary>
    /// 实时读取当前职业使用的「攻击」：法系读 MagicAttack，其余职业读 Attack，真源就是 AttrSystem 那一份。
    /// 每次算上限 / 每次回蓝都<b>现读一遍</b> → 局内升级和装备变化立刻生效，不必等下一关。
    /// 城镇非战斗期可能拿不到 Hero / attr：记录错误后返回 0，保留原有 null 宽容度、不引入崩溃。
    /// </summary>
    static float PlayerAttackPower()
    {
        var hero = Hero.Instance;
        if (hero == null)
        {
            Debug.LogError("[BattleManager] PlayerAttackPower 失败：找不到 Hero，攻击换算项按 0 处理");
            return 0f;
        }
        if (hero.attr == null)
        {
            Debug.LogError("[BattleManager] PlayerAttackPower 失败：Hero.attr 为空，攻击换算项按 0 处理");
            return 0f;
        }
        return hero.attr.GetAttr(PlayerJobBaseStats.CurrentAttackAttr());
    }

    /// <summary>
    /// 实时读取「智力」基础属性：真源同样是 AttrSystem 那一份，与 PlayerAttackPower 同口径。
    /// 【2026-10-10 追加澄清·恢复】装备的 INTELLIGENCE 词条仍然有效：既走 ApplyDerivedAttributes 派生魔攻，
    /// 也通过 PlayerMpPool / PlayerMpRegen 喂蓝条（攻击项 + 智力项双叠加）。
    /// 只是<b>天赋左列</b>不再加智力（改投 Attack / MagicAttack），所以这里的来源主要是基础值 / 等级 / 披风 / 装备词条。
    /// 城镇非战斗期可能拿不到 Hero / attr：记录错误后返回 0，保留原有 null 宽容度、不引入崩溃。
    /// </summary>
    static float PlayerIntelligence()
    {
        var hero = Hero.Instance;
        if (hero == null)
        {
            Debug.LogError("[BattleManager] PlayerIntelligence 失败：找不到 Hero，智力换算项按 0 处理");
            return 0f;
        }
        if (hero.attr == null)
        {
            Debug.LogError("[BattleManager] PlayerIntelligence 失败：Hero.attr 为空，智力换算项按 0 处理");
            return 0f;
        }
        return hero.attr.GetAttr(AttrType.Intelligence);
    }

    /// <summary>
    /// 玩家 MP 池上限（本局没有耗蓝技能 → 0，蓝条不显示、不做闸门）。
    /// 【2026-10-10 主人拍板】攻击项<b>叠加</b>在 MpProfile 的星级/等级曲线之上（不是替换）；
    /// 【2026-10-10 追加澄清】智力项再叠加一层（装备智力词条续效）：
    /// 完整公式 = MpProfile.Pool(Player, HeroLevel, PlayerMpStar())
    ///          + 当前职业攻击 × MP_POOL_PER_ATTACK
    ///          + 智力 × MP_POOL_PER_INTELLIGENCE。
    /// </summary>
    public static float PlayerMpPool()
    {
        if (!PlayerHasMp()) return 0f;
        return MpProfile.Pool(MpArchetype.Player, RunLoadout.HeroLevel, PlayerMpStar())
             + PlayerAttackPower() * GameConfig.MP_POOL_PER_ATTACK
             + PlayerIntelligence() * GameConfig.MP_POOL_PER_INTELLIGENCE;
    }

    /// <summary>
    /// 玩家每秒自然回复（本局没有耗蓝技能 → 0）。
    /// 【2026-10-10 主人拍板】攻击项<b>叠加</b>在 MpProfile 的星级曲线之上（不是替换）；
    /// 【2026-10-10 追加澄清】智力项再叠加一层（装备智力词条续效）：
    /// 完整公式 = MpProfile.Regen(Player, PlayerMpStar())
    ///          + 当前职业攻击 × MP_REGEN_PER_ATTACK
    ///          + 智力 × MP_REGEN_PER_INTELLIGENCE。
    /// </summary>
    public static float PlayerMpRegen()
    {
        if (!PlayerHasMp()) return 0f;
        return MpProfile.Regen(MpArchetype.Player, PlayerMpStar())
             + PlayerAttackPower() * GameConfig.MP_REGEN_PER_ATTACK
             + PlayerIntelligence() * GameConfig.MP_REGEN_PER_INTELLIGENCE;
    }

    /// <summary>玩家蓝够不够这一发（<b>只看不扣</b>；扣蓝唯一出口仍是 TrySpendPlayerMp）。</summary>
    public bool CanAffordPlayerMp(float cost)
    {
        if (cost <= 0f) return true;
        return _playerMp + 0.001f >= cost;
    }

    /// <summary>玩家蓝条比例 0~1（喂 UI 用）。</summary>
    public float GetPlayerMp()
    {
        float pool = PlayerMpPool();
        if (pool <= 0f) return 0f;
        return Mathf.Clamp01(_playerMp / pool);
    }

    /// <summary>
    /// 扣玩家蓝的<b>唯一出口</b>：够才扣并返回 true；不够 → false（不放技能）。
    /// cost &lt;= 0 视为无蓝技能，直接放行、不扣蓝。
    /// </summary>
    public bool TrySpendPlayerMp(float cost)
    {
        if (cost <= 0f) return true;
        if (_playerMp + 0.001f < cost) return false;
        _playerMp = Mathf.Max(0f, _playerMp - cost);
        return true;
    }

    /// <summary>玩家蓝条每帧：本关首次进场灌满 → 之后按自然回复回蓝（面板冻结时 deltaTime=0 不回）。</summary>
    void TickPlayerMp(float dt)
    {
        float pool = PlayerMpPool();
        if (pool <= 0f)
        {
            if (_playerMp > 0f) _playerMp = 0f;
            return;
        }
        if (!_playerMpFilled)
        {
            _playerMp = pool;
            _playerMpFilled = true;
        }
        else
        {
            _playerMp = Mathf.Min(pool, _playerMp + PlayerMpRegen() * dt);
        }
        // 上限（曲线 + 攻击项 + 智力项）随局内状态实时变化：万一上限回落（更换 / 卸下装备、攻击增益消失等），
        // 当前蓝量 clamp 回上限内，别出现「当前蓝 > 上限」的显示（GetPlayerMp 的 Clamp01 也会跟着失真）。
        if (_playerMp > pool) _playerMp = pool;
    }

    // === 传送门 ===
    /// <summary>传送门是否已激活（所有怪清完后激活，玩家进入后通关）</summary>
    #endregion
}
