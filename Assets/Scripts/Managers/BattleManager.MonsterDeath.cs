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
        // 引导中的宿敌精英也带精英标记，走同一条判定；掉了走 ResourceWallet 弹「获得天赋石」提示。
        if (m.IsBossUnit)
        {
            if (Random.value < GameConfig.BOSS_TALENT_DROP_CHANCE)
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, GameConfig.BOSS_TALENT_DROP_AMOUNT, save: false, notify: true);
        }
        else if (m.IsEliteWave && Random.value < GameConfig.ELITE_TALENT_DROP_CHANCE)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, GameConfig.ELITE_TALENT_DROP_AMOUNT, save: false, notify: true);
        }
        // 【2026-10-21 主人拍板】击杀发局内抽奖币：普通 10 / 精英 25 / Boss 50（Boss 优先、不与精英叠加，
        // 与上面佣兵币同款判定结构）。发放唯一出口 = AddRunGold(countsAsEarned:false) → 局内抽奖币池
        // currentGold（带不出本局，与 SlotMachineSystem 同口径）；HUD 抽奖币数字由
        // AddRunGold → NotifyPoolGoldChanged 自动刷新，这里不手动刷。
        // 金币副本有自己的击杀掉金经济（killGoldMul=1 旧路径原样），不发防双发；引导局照发保持一致。
        // 表现复用 DamageTextSystem.SpawnGoldText 现成入口，怪头顶飘「+N金」。
        if (!IsGoldDungeon)
        {
            int killGold = m.IsBossUnit ? GameConfig.KILL_GOLD_BOSS
                         : m.IsEliteWave ? GameConfig.KILL_GOLD_ELITE
                         : GameConfig.KILL_GOLD_NORMAL;
            AddRunGold(killGold, countsAsEarned: false);
            DamageTextSystem.Instance?.SpawnGoldText(m.transform.position, killGold);
        }
        // 2026-09-26 主人拍板：累计本关物理/魔法击杀数，用于掉落按类型分池（主导类型 = 数量多者）。
        if (m.IsMagicType) _magicKills++;
        else _physicalKills++;
        if (m.LastDamageSource != null && m.LastDamageSource.isAlly)
            RecordAllyKill(m.LastDamageSource);
        AdventureLogAchievements.OnMonsterKilled(m, CurrentChapter);
        // 英雄可见等级已停用（属性成长只走城镇天赋），只保留隐藏经验用于裂缝掉落稀有度
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
        HeroThunderUltimate.Instance?.OnMonsterKilled(m);

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
        AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

        // 图鉴：击败解锁完整描述；未见过则顺带记遭遇并发里程
        if (m.config != null)
            AdventureCodex.MarkMonsterDefeated(m.config.id);

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

        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null || mercIndex >= mercs.Count) return false;
        Mercenary merc = mercs[mercIndex];
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
