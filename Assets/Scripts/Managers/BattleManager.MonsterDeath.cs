using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 怪物死亡与技能施放
    internal void OnMonsterDead(UnitBase monster)
    {
        InvalidateAliveMonsterCache();

        // ===== 修点②（2026-11-20 二次拍板⑤）：机器人死亡奖励分支 =====
        // Mercenary 不走下面的 Monster 分支（m==null 直接 return，只有缓存清理生效）：
        // 友方机器人（救场/入场救援生成）零奖励零计数，只走上面的缓存清理；
        // 敌方机器人（敌对乱入生成）当怪处理：击杀计数 + 击杀任务 + 掉卷轴（按难度分档）。
        if (monster is Mercenary)
        {
            Mercenary mc = (Mercenary)monster;
            if (mc.isAlly) return;   // 友方：什么都不发，直接 return（缓存清理已在开头做过）
            RunStats.KillCount++;
            SaveSystem.Instance?.Data?.CountKill(false);   // 机器人非 Boss，照下方 :29 Monster 分支口径传 false
            GrantIntruderScrollReward();
            return;
        }

        Monster m = monster as Monster;
        if (m == null) return;

        RunStats.KillCount++;
        if (m.IsEliteWave)
        {
            RunStats.EliteKillCount++;
            if (!_eliteToastShownThisStage)
            {
                _eliteToastShownThisStage = true;
                GlobalToastUI.ShowFlythrough("精英击破！");
            }
        }
        if (m.IsBossUnit) RunStats.BossKillCount++;

        // 【2026-10-29】每日任务击杀计数（除害/清剿 = 非 Boss，精英算小怪；弑君 = Boss）
        SaveSystem.Instance?.Data?.CountKill(m.IsBossUnit);

        // 佣兵币产出（2026-10-07 主人拍板：「击杀精英1个 击杀Boss两个」）。
        // Boss 优先、不与精英叠加（Boss 波可能同时带精英标记，叠加会变成 3）。
        // 发放只走 ResourceWallet.GrantMercGold 这一个出口（铁律 15）。
        // 【2026-10-11 主人拍板】「现在暂时先不用给佣兵币 等以后再说」→ 发放整条停掉。
        // 开关在 GameConfig.MERC_GOLD_GRANT_ENABLED，改回 true 即恢复，下面两行的数值不用动。
        if (GameConfig.MERC_GOLD_GRANT_ENABLED)
        {
            if (m.IsBossUnit)
                ResourceWallet.GrantMercGold(GameConfig.MERC_GOLD_PER_BOSS, "击杀Boss");
            else if (m.IsEliteWave)
                ResourceWallet.GrantMercGold(GameConfig.MERC_GOLD_PER_ELITE, "击杀精英");
        }
        // 【2026-10-20 主人拍板】天赋石不再每关首通发放，改精英/Boss 击杀概率掉落：
        // 精英 30%×1、Boss 60%×2（Boss 优先、不与精英叠加，与佣兵币同口径）。
        // 【2026-11-11 主人拍板】天赋石只从主线正式关 / 支线岛的精英、Boss 击杀掉落，
        // 活动副本 / 羁绊 / 竞技场 / 终章 / 裂隙 / 引导一律不掉（无限裂隙层首通宝箱另走 SettleEndlessStageClear）。
        bool talentDropAllowed = IsFormalMainRun || IsSideIsland;
        if (talentDropAllowed && m.IsBossUnit)
        {
            if (Random.value < GameConfig.BOSS_TALENT_DROP_CHANCE)
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, GameConfig.BOSS_TALENT_DROP_AMOUNT, save: false, notify: true);
        }
        else if (talentDropAllowed && m.IsEliteWave && Random.value < GameConfig.ELITE_TALENT_DROP_CHANCE)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, GameConfig.ELITE_TALENT_DROP_AMOUNT, save: false, notify: true);
        }
        // 【2026-10-21 主人拍板】击杀发局内抽奖币：普通 10 / 精英 25 / Boss 50（Boss 优先、不与精英叠加，
        // 与上面佣兵币同款判定结构）。发放唯一出口 = AddRunGold(countsAsEarned:false) → 局内抽奖币池
        // currentGold（带不出本局，与 SlotMachineSystem 同口径）；HUD 抽奖币数字由
        // AddRunGold → NotifyPoolGoldChanged 自动刷新，这里不手动刷。
        // 金币副本有自己的击杀掉金经济（killGoldMul=1 旧路径原样），不发防双发；引导局照发保持一致。
        // 徽记副本（2026-10-23 主人拍板「徽记副本不给金币」）：局内抽奖币同样不发，只发徽记。
        // 卷轴副本（2026-10-24）：照徽记口径，局内抽奖币不发，只发招募卷轴。
        // 礼物副本（2026-10-26）：照徽记/卷轴口径，局内抽奖币不发，只发好感礼物。
        // 无限裂隙（2026-10-23 主人拍板「活动副本和无限…这里不掉抽奖币」）：击杀同样不发，
        // 只有每层免费抽 3 次（2026-11-15 口径，唯一出口 SlotMachineDefs.EndlessFreeDrawsForRun）；
        // 通关补贴抽奖币已按同日口径对裂隙停发。
        // 表现复用 DamageTextSystem.SpawnGoldText 现成入口，怪头顶飘「+N金」。
        if (!IsGoldDungeon && !IsBadgeDungeon && !IsScrollDungeon && !IsGiftDungeon && !IsEndlessRift)
        {
            int killGold = m.IsBossUnit ? GameConfig.KILL_GOLD_BOSS
                         : m.IsEliteWave ? GameConfig.KILL_GOLD_ELITE
                         : GameConfig.KILL_GOLD_NORMAL;
            // 【2026-11-14 主人拍板】右列天赋「敛财」：击杀掉币 +10%/级（满 5 级 +50%），
            // 普通/精英/Boss 三档一起放大。加成值唯一真源 = TalentDefs R_GOLD 的 optValues，
            // 这里只乘一次，别在别处再写第二份。
            float goldBonus = SlotMachineSystem.KillGoldBonus();
            if (goldBonus > 0f)
                killGold = Mathf.Max(1, Mathf.RoundToInt(killGold * (1f + goldBonus)));
            AddRunGold(killGold, countsAsEarned: false);
            DamageTextSystem.Instance?.SpawnGoldText(m.transform.position, killGold);
        }
        // 【2026-11-09 主人拍板「金币按文案设定来发」】金币本金币改按文案总额逐怪分摊
        //（普通 500 / 困难 800 / 噩梦 1200，= 冒险页详情文案同一真源 GetGoldDungeonClearGold）：
        // 权重 普通1/精英2/Boss5（真源 ActivityDungeonDefs），按累计权重占比发放、末只补齐零头
        // → 顶栏金币条累计 = 结果界面总额 = 详情文案。旧的关卡固定金那笔对金币本已跳过
        //（BeginRewardSequence），不会双发。
        if (IsGoldDungeon)
            GrantGoldDungeonKillGold(m);
        // 2026-09-26 主人拍板：累计本关物理/魔法击杀数，用于掉落按类型分池（主导类型 = 数量多者）。
        if (m.IsMagicType) _magicKills++;
        else _physicalKills++;
        if (m.LastDamageSource != null && m.LastDamageSource.isAlly)
            RecordAllyKill(m.LastDamageSource);
        // 【2026-11-05 难度隔离】冒险日志属普通主线收集内容，难度局不写（唯一出口 NormalOnlyContentAllowed）
        if (NormalOnlyContentAllowed(BattleDifficulty))
            AdventureLogAchievements.OnMonsterKilled(m, CurrentChapter);
        // 隐藏经验照旧只管裂缝掉落稀有度；【2026-10-25 主人拍板「玩家升级按完成的一次关卡给一次经验」】
        // 击杀不再喂玩家可见等级（经验唯一入口 = LevelSystem.AddStageClearExp，主线/支线完成关卡才给）
        HiddenLevelSystem.AddKillExp(m);

        // 连杀：累计击杀连击数（≥3 起会在下方兑现额外金币，见 COMBO_BONUS_GOLD）
        float now = Time.time;
        if (now - _lastKillTime <= GameConfig.COMBO_WINDOW)
            _killCombo++;
        else
            _killCombo = 1;
        _lastKillTime = now;
        SyncKillComboHaste();
        if (_killCombo > RunStats.MaxKillCombo)
            RunStats.MaxKillCombo = _killCombo;
        // 【2026-10-19 主人拍板】右上角「击败」改成常驻累计计数（总数、不消失、跨关卡累计），
        // 不再显示连杀数；连杀本身的速度加成/金币奖励逻辑不动（_killCombo 照旧）。
        BattleSideHud.Instance?.SetKillTotal(RunStats.KillCount);
        CombatJuice.Instance?.OnKillCombo(_killCombo);
        CombatJuice.Instance?.OnKillFinisher(m);
        // 2026-11-13 雷击奥义改独立 CD：击杀充能回调整条移除，保留击杀计数/连杀等其它逻辑。

        // 连杀续杯（R2）：连杀≥阈值时回少量血，高连击回得更多，让"连"成为资源
        // 2026-10-09 主人拍板：连杀不再回血，总开关关掉（不是删逻辑，想恢复改开关即可）
        if (GameConfig.COMBO_HEAL_ENABLED && _killCombo >= GameConfig.COMBO_HEAL_MIN_COMBO && Hero.Instance != null)
        {
            var hero = Hero.Instance;
            float maxHp = hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : 0f;
            if (maxHp > 0f)
            {
                int heal = GameConfig.COMBO_HEAL_PER_KILL
                           + Mathf.FloorToInt(_killCombo / 5f) * GameConfig.COMBO_HEAL_STEP;
                hero.currentHp = Mathf.Min(maxHp, hero.currentHp + heal);
                DamageTextSystem.Instance?.SpawnHealText(hero.transform.position, heal);
            }
        }


        _defeatedMonsterKills++;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, defeated, goal, StageQuestClearGold);

        // 技能蓝条改由受击/攻击涨，击杀不再加能量
        // 【2026-11-05 难度隔离】击杀成就属普通成就，难度局不写；图鉴同属普通主线收集（唯一出口 NormalOnlyContentAllowed）
        if (NormalOnlyContentAllowed(BattleDifficulty))
        {
            AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

            // 图鉴：击败解锁完整描述；未见过则顺带记遭遇并发里程
            if (m.config != null)
                AdventureCodex.MarkMonsterDefeated(m.config.id);
        }

        BattleUI.Instance?.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
        BattleUI.Instance?.UpdateSkillEnergy(1, GetMercMp(0));
        BattleUI.Instance?.UpdateSkillEnergy(2, GetMercMp(1));

        // 本波清完 → 播「下一波来袭」
        if (CountAliveMonsters() <= 1) // 含即将移除的自己，下一帧会清；用 <=1 更稳
        {
            // 延迟到本帧列表清理后判断；用协程下一帧
            if (!Rules.SkipWaveAnnounce && _waveAnnounceCo == null && !_allWavesSpawned)
                StartCoroutine(CoCheckWaveClearNextFrame());
        }
    }

    // ===== 修点②（2026-11-20 二次拍板⑤）：敌对乱入机器人掉卷轴 =====
    /// <summary>掉卷轴数量按难度分档（0 普通 / 1 困难 / 2 噩梦），三档同类型（普通招募卷）数量递增；实测可调。</summary>
    static readonly int[] IntruderScrollCount = { 1, 2, 3 };

    /// <summary>
    /// 敌对乱入机器人死亡掉「普通招募卷」：不进金币、不新开奖励系统。
    /// 库存唯一出口 = SaveData.mercScrollCommon（SaveData.cs:876，与 Boss 掉卷 Settlement.cs:250 同字段；
    /// mercScrollRare 已折算废弃，mercScrollLegendary 是高级档不适用「同类型」口径）。
    /// 提示照现有掉落 toast 风格（Boss 掉卷 Settlement.cs:260 / 精英击破 MonsterDeath.cs:23）简短一行。
    /// </summary>
    void GrantIntruderScrollReward()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[BattleManager.MonsterDeath] 敌对乱入卷轴奖励：SaveSystem.Instance/Data 为空，未发放（BattleManager.MonsterDeath.cs GrantIntruderScrollReward）");
            return;
        }
        int diff = BattleDifficulty;
        if (diff < 0 || diff >= IntruderScrollCount.Length)
        {
            Debug.LogError($"[BattleManager.MonsterDeath] 敌对乱入卷轴奖励：未知难度 {diff}，未发放（BattleManager.MonsterDeath.cs GrantIntruderScrollReward）");
            return;
        }
        int count = IntruderScrollCount[diff];
        data.mercScrollCommon += count;
        GlobalToastUI.ShowFlythrough($"敌对乱入掉落 普通招募卷×{count}");
    }

    IEnumerator CoCheckWaveClearNextFrame()
    {
        yield return null;
        if (!isInBattle || _stageCleared || _portalActive) yield break;
        if (CountAliveMonsters() > 0) yield break;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            // 正式关：末波清完立刻进宝箱，不空等下一帧 Update
            if (!SuppressStageClear && !_rewardSequenceStarted && !_stageCleared && !_portalActive
                && _totalMonstersSpawnedThisStage > 0 && CountAliveMonsters() == 0)
                StartStageClearRewardSequence();
            yield break;
        }
        BeginNextWaveCountdown();
    }

    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>释放玩家技能（需要能量满）— 头像点击。执行在 SkillCastService。</summary>
    public bool TryUsePlayerSkill() => SkillCast.TryUsePlayerSkill();

    /// <summary>释放佣兵技能（槽位 index 0/1）— 手动模式</summary>
    public bool TryUseMercSkill(int mercIndex)
    {
        if (MercSkillMigrate.IsMercSkillAutoCast()) return false;
        if (mercIndex < 0 || mercIndex >= mercMp.Length) return false;

        // 【2026-11-11】与蓝条 / 扣蓝同源于槽位解析：场上列表阵亡后会压缩前移，按 index 取会点错人。
        Mercenary merc = MercAt(mercIndex);
        if (merc == null || merc.isDead) return false;
        if (merc.SkillCaster == null || !merc.SkillCaster.HasActiveSkill) return false;

        // 2026-10-06：手动模式的「能量满」判定换成 MP 闸门，扣蓝统一走 MercSkillCaster.TryCast
        // → BattleManager.TrySpendMercMp，这里不再自己清零（UI 由扣蓝出口负责刷）。
        return merc.SkillCaster.TryCast(manual: true);
    }

    /// <summary>佣兵主动技施放入口（自动/手动共用）。执行在 SkillCastService。</summary>
    public bool TryCastMercActiveSkill(Mercenary merc, string skillId, bool manual)
        => SkillCast.TryCastMercActiveSkill(merc, skillId, manual);

    SkillSystem.ActiveSkill ResolveSkill(string skillId) => SkillCast.ResolveSkill(skillId);

    // ============================================================
    // 关卡通关
    // ============================================================

    #endregion
}
