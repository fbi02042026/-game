using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 战斗编排器：本局状态、胜负/结算、能量与开场过场。
/// 波次规划/出怪 → <see cref="WavePlanner"/>；技能施放 → <see cref="SkillCastService"/>。
/// </summary>
public partial class BattleManager : Singleton<BattleManager>, ICombatBoundSingleton
{
    #region 场景引用与单位
    [Header("场景引用")]
    public Hero hero;
    public Transform spawnPoint;
    public Transform endPoint;
    public Transform[] monsterSpawnPoints;
    /// <summary>用户摆好的 unit 节点：所有人物/怪物挂在其下，位置即草地站立线</summary>
    public Transform unitRoot;

    public List<UnitBase> allyUnits = new List<UnitBase>();
    public List<UnitBase> monsters = new List<UnitBase>();
    public StageData currentStage;
    #endregion
    #region 生命周期与开局
    protected override void Awake()
    {
        base.Awake();
    }

    public void StartNewRun()
    {
        StartNewRunInternal();
    }

    /// <summary>
    /// 定本局的玩法模式。<b>这是全项目唯一决定「这是哪一种玩法」的地方。</b>
    /// 以后加无限关卡 / 世界BOSS / PVP，只在这里多加一条返回，其余代码一行不动。
    /// </summary>
    IBattleMode ResolveMode()
    {
        // 引导优先：它由剧情触发，跟玩家在冒险界面选哪个按钮无关。
        // 注意要在 StoryProgress.ConsumeTutorialBattleFlag() 之前判。
        if (StoryProgress.ShouldStartTutorialBattle()) return BattleModes.Tutorial;

        // 活动副本（冒险界面下标 4）。这是一个「类」，不是一种玩法：
        // 现在只开了金币本；以后佣兵副本 / 武器副本 / 技能副本上了，
        // 在这里按「当前开放的是哪一个活动」返回对应模式即可。
        if (AdventureUI.PendingGoldDungeon) return BattleModes.GoldDungeon;

        // 无限裂隙（冒险界面下标 2）
        if (AdventureUI.PendingEndlessRift) return BattleModes.Endless;

        return BattleModes.Normal;
    }

    void StartNewRunInternal()
    {
        Debug.Log("[BattleManager] ===== StartNewRun 开始 =====");
        StopBattleSpawnCoroutines();
        // 新一局：清空「章内不重复」的关卡模式记录，重新洗牌（V3.0 起模式允许重复，此处仅清压力阀）
        StageModeTable.ResetRun();
        Planner?.ResetPressure();
        // 【2026-10-10 主人拍板】战斗钱包局内化：**城镇金币不带进战斗**，开局只有本局启动金。
        // 启动金由 RunDraftDirector.OnRunStart（非续关）走 SlotMachineSystem.GrantRunStarterGold 补 ——
        // 那里才读得到「初始资金」天赋等级。这里先归零，_goldAtRunStart 会随那笔一起抬起来，
        // 于是结算差额 = 本局净赚（抽奖花掉的钱也扣在里面），启动金既不会被白刷也不会被重复写回。
        currentGold = 0;
        _goldAtRunStart = 0;
        _runStageGoldPending = 0;
        _runStagesCleared = 0;
        _earnedPaidGold = 0;   // 【2026-10-18 双钱包拆分】已付水位同步归零
        var data = SaveSystem.Instance?.Data;
        _enchantAtRunStart = data != null ? data.enchantStones : 0;
        _matsAtRunStart = data != null ? data.decomposeMats : 0;
        _stageQuestGoldGranted = false;
        _defeatedMonsterKills = 0;
        RunStats.Reset();
        RunStats.Chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        {
            var save = SaveSystem.Instance?.Data;
            string pn = save != null && !string.IsNullOrEmpty(save.playerDisplayName)
                ? save.playerDisplayName : "冒险者";
            RunStats.EnsureAlly(BattleRunStats.PlayerMvpKey, pn);
        }
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        ResetPortalGateState();
        UnitsCanAct = false;
        AllowMonsterMapEnter = false;
        isAutoBattle = false;
        MonsterAttackStyleTable.Reload();
        // 纯冷却制：清能量换成「解除间隔 + 按槽位错峰上初始 CD」
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();
        // 上一局/上一关残留的攻击/防御/暴击增益（定时增益层）必须清掉，否则会跨关带着走
        Hero.Instance?.attr?.ClearTimedBuffs();
        ResetMpForNewStage();
        MercenaryManager.Instance?.ClearAllMercs();
        allyUnits.RemoveAll(u => u == null || u is Mercenary);

        HidePortal();
        EnsureRewardDirector();
        StageClearRewardDirector.Instance?.CacheSceneRefs();
        StageClearRewardDirector.Instance?.HideClearProps();
        EnsurePortalAnimatorReady(endPoint);

        if (hero == null)
            hero = Hero.Instance != null ? Hero.Instance : FindObjectOfType<Hero>();
        if (hero == null)
        {
            Debug.LogError("[BattleManager] StartNewRun 失败：找不到 Hero，无法开战/刷怪");
            return;
        }

        if (!allyUnits.Contains(hero))
            allyUnits.Add(hero);

        try
        {
            hero.InitNewRun();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] hero.InitNewRun 异常（继续开战）: {e}");
        }

        // 玩法身份只在这里定一次；规则包由模式自带，不再单独判一次 IsTutorial。
        Mode = ResolveMode();
        Rules = Mode.Rules;
        TutorialPowerFantasy = false;
        _tutorialHpFromTable = false;
        if (Rules.Active)
        {
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
            TutorialDirector.Instance?.ResetBattleTutorialRuntime();
            StoryProgress.ConsumeTutorialBattleFlag();
        }
        // 选职后一律发职业武器（含教程）；木剑兜底仅非教程
        if (GridBackpackSystem.Instance != null)
        {
            PlayerJobDefs.ApplyForBattle();
            if (!Rules.SkipStarterWeaponFallback
                && GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand) == null)
            {
                if (GridBackpackSystem.Instance.EnsureStarterWeapon())
                    hero.RecalcAttr();
            }
        }
        // 【2026-10-05】SuppressStageClear / SkipLegacyOnEvacuate 已改成只读直通规则包，
        // 这里不再赋值（原先只在这一处设，换关只走 LoadStage 时会残留上一关的值）。
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        if (Rules.Active)
            Debug.Log("[BattleManager] 新手引导战斗：短关+强制撤离（规则包 TutorialRules）");

        // === 局内构筑（肉鸽影子）：技能 + 佣兵全部走本局 loadout，不读城镇酒馆存档 ===
        // 新手引导局：V6 起也开构筑（走内存模式），让玩家在引导里就学会三选一与技能顺序
        if (!Rules.Active || Rules.EnableRunDraft)
            RunDraftDirector.Ensure(this).OnRunStart();

        // loadout 生效时不再走城镇酒馆雇佣；否则保留旧路径（异常/特殊规则兜底）
        if (!RunLoadout.IsActive && !GameConfig.SOLO_PLAYER_BATTLE && !Rules.SkipMercPrecall)
        {
            EnsureTestMercenaries();
            SpawnMercenaries();
        }

        int targetChapter = AdventureUI.PendingBattleChapter;
        if (targetChapter < 1)
            targetChapter = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (targetChapter < 1) targetChapter = 1;
        BattleDifficulty = Mathf.Clamp(AdventureUI.PendingBattleDifficulty, 0, 2);
        // IsGoldDungeon 不再在这里写入：它由 Mode 派生（见属性定义）。
        // PendingGoldDungeon 只保留「读完即清」，避免下一局被上一局的静态标志污染。
        AdventureUI.PendingBattleChapter = 0;
        AdventureUI.PendingBattleDifficulty = 0;
        AdventureUI.PendingGoldDungeon = false;

        // 无限裂隙：起始层 = 记录点 + 1（读完即清，防污染）；普通章号参数对它无意义
        if (AdventureUI.PendingEndlessRift)
        {
            EndlessRiftStage = EndlessRiftRules.StartStage();
            AdventureUI.PendingEndlessRift = false;
            targetChapter = GameConfig.EndlessChapterForStage(EndlessRiftStage);
        }

        if (ChapterManager.Instance == null)
        {
            Debug.LogError("[BattleManager] ChapterManager 为空，用临时 Normal 关强行 LoadStage");
            LoadStage(new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() });
            return;
        }

        // 建关交给模式自己：核心不需要知道一共有几种玩法。
        Mode.BuildChapter(ChapterManager.Instance, targetChapter);

        StageData first = null;
        if (ChapterManager.Instance.availableNextStages != null && ChapterManager.Instance.availableNextStages.Count > 0)
            first = ChapterManager.Instance.availableNextStages[0];
        else if (ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
            first = ChapterManager.Instance.stageMap[0];

        if (first == null)
        {
            Debug.LogError("[BattleManager] 关卡图为空，创建临时 Normal 关");
            first = new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() };
        }

        Debug.Log($"[BattleManager] StartNewRun → LoadStage type={first.type} idx={first.stageIndex}");
        if (!Rules.SkipRiftEnteredAchievement)
            AdventureLogAchievements.OnRiftEntered();
        LoadStage(first);
    }

    IEnumerator CoPreLevelThenLoad(StageData first)
    {
        // 遗产战前三选一已取消；保留方法避免外部误调编译失败
        LoadStage(first);
        yield break;
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();

        for (int i = data.permanentMercs.Count - 1; i >= 0; i--)
        {
            var m = data.permanentMercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId))
            {
                data.permanentMercs.RemoveAt(i);
                continue;
            }
            // 无效弓手预制体 id 纠正
            if (m.mercId.StartsWith("gongshou") && Resources.Load<GameObject>("Units/" + m.mercId) == null)
                m.mercId = "gongshou101";
            if (m.level < 1) m.level = 1;
            if (m.star < 1) m.star = 1;
            if (string.IsNullOrEmpty(m.displayName)) m.displayName = m.mercId;
            if (string.IsNullOrEmpty(m.uid)) m.uid = System.Guid.NewGuid().ToString("N");
            MercSkillMigrate.AlignMercenary(m);
        }

        // 软著版：佣兵由酒馆三选一反复招募，不再空档自动塞弓手。
        // 旧逻辑保留注释，便于日后调试：
        // if (data.permanentMercs.Count == 0)
        // {
        //     data.permanentMercs.Add(new MercenaryData {
        //         mercId = "gongshou101", displayName = "测试弓手",
        //         favorLevel = 1, level = 1, star = 1,
        //         skillId = SkillRegistry.DefaultMercRangedSkillId,
        //         uid = System.Guid.NewGuid().ToString("N")
        //     });
        // }

        if (data.townLevel.tavern < 1 && data.permanentMercs.Count > 0)
            data.townLevel.tavern = 1;
    }

    void SpawnMercenaries()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) return;

        var data = SaveSystem.Instance?.Data;
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : hero.transform.position;
        basePos.y = UnitBase.GROUND_Y;
        basePos.z = 0f;

        int max = mm.GetMaxMercSlots();
        int spawned = 0;
        bool countedLaodun = false;
        var party = mm.GetActiveMercData();
        for (int i = 0; i < party.Count && spawned < max; i++)
        {
            var md = party[i];
            if (md == null || string.IsNullOrEmpty(md.mercId)) continue;
            if (!GameConfig.IsMercAvailable(md.mercId, data)) continue;

            Vector3 pos = basePos;
            if (hero != null)
                pos.x = UnitCrowd.GetMercDesiredCombatX(hero, null, spawned);
            else
                pos.x = basePos.x + MERC_FRONT_SPACING * (spawned + 1);

            var merc = mm.SpawnMercenary(md.mercId, pos, Mathf.Max(1, md.level));
            if (merc != null)
            {
                MercSkillMigrate.AlignMercenary(md);
                string active = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercSkillId(md)
                    : md.skillId;
                string passive = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercPassiveSkillId(md)
                    : md.passiveSkillId;
                merc.SetupBattleSkills(active, passive);
                merc.SetPartyIndex(spawned);
                merc.SetDisplayName(md.displayName);
                if (!string.IsNullOrEmpty(md.hireId))
                    merc.SetHireId(md.hireId);
                else
                {
                    string resolved = MercPortraitSprites.ResolveHireId(md.mercId);
                    if (!string.IsNullOrEmpty(resolved))
                        merc.SetHireId(resolved);
                }
                if (!string.IsNullOrEmpty(md.displayName))
                    merc.gameObject.name = "Merc_" + md.displayName;
                allyUnits.Add(merc);
                merc.OnDead += OnMercenaryDead;
                RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
                spawned++;
                if (!Rules.SkipLaodunAchievement && !countedLaodun && md.mercId.StartsWith("dunbing"))
                {
                    AdventureLogAchievements.OnLaodunBattled();
                    countedLaodun = true;
                }
            }
        }
        Debug.Log($"[BattleManager] 已生成佣兵 {spawned} 个 (tavern槽={max})");
    }

    /// <summary>
    /// 【2026-10-17】阵亡佣兵下一关复活 —— 兑现 2026-10-06 拍板「阵亡下一关按 20% 血复活（每过一关养回 30%）」。
    /// <para>旧实现只有开新一局才 <see cref="SpawnMercenaries"/>，同一局内阵亡的佣兵永远回不来
    /// （玩家报「佣兵打着打着就消失了」）。进下一关时扫 <see cref="RunLoadout"/>：
    /// fallen=true 且场上没有实例的，按 <see cref="RunLoadout.MercReviveHpRatio"/> 的血量重新生成
    /// （血量 = 20% + 30%×已过关数，封顶 100%）。</para>
    /// <para>生成参数与 <see cref="RunDraftDirector.RecruitMerc"/> 对齐：星级 / 技能 / 名字 / hireId /
    /// OnDead 订阅一个不少，保证它再阵亡时走同一条死亡链路。</para>
    /// </summary>
    void ReviveFallenMercsForStage()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || !RunLoadout.IsActive) return;

        var onField = mm.GetActiveMercs();
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var e = mercs[i];
            if (e == null || !e.fallen) continue;
            string key = !string.IsNullOrEmpty(e.hireId) ? e.hireId : e.mercId;

            // 场上已有同 id 实例就不补（理论上 fallen=true 时实例已 Destroy，这里只防手抖双生成）
            bool alreadyOnField = false;
            for (int k = 0; k < onField.Count; k++)
            {
                var m = onField[k];
                if (m == null) continue;
                string mKey = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
                if (mKey == key) { alreadyOnField = true; break; }
            }
            if (alreadyOnField) continue;

            var heroRef = hero != null ? hero : Hero.Instance;
            int slot = mm.GetActiveMercs().Count;
            Vector3 pos;
            if (heroRef != null)
                pos = new Vector3(UnitCrowd.GetMercDesiredCombatX(heroRef, null, slot), UnitBase.GROUND_Y,
                    unitRoot != null ? unitRoot.position.z : 0f);
            else
                pos = new Vector3(transform.position.x + MERC_FRONT_SPACING * (slot + 1), UnitBase.GROUND_Y, 0f);

            var merc = mm.SpawnMercenary(e.mercId, pos, Mathf.Max(1, e.level));
            if (merc == null)
            {
                Debug.LogError($"[BattleManager] 阵亡佣兵复活失败：预制体缺失 mercId={e.mercId}");
                continue;
            }
            merc.mercStar = Mathf.Clamp(e.star, 1, 5);
            // SpawnMercenary → Init 会重置满血，惩罚必须在这里补砍回去（与 RunDraftDirector 同口径）
            float reviveRatio = RunLoadout.MercReviveHpRatio(e);
            if (reviveRatio < 1f && merc.attr != null)
            {
                float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
                merc.currentHp = Mathf.Max(1f, maxHp * reviveRatio);
            }
            merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
            merc.SetPartyIndex(slot);
            merc.SetDisplayName(e.displayName);
            if (!string.IsNullOrEmpty(e.hireId))
                merc.SetHireId(e.hireId);
            if (!string.IsNullOrEmpty(e.displayName))
                merc.gameObject.name = "Merc_" + e.displayName;
            if (!allyUnits.Contains(merc))
                allyUnits.Add(merc);
            merc.OnDead += OnMercenaryDead;
            merc.Face(1);
            Debug.Log($"[BattleManager] 阵亡佣兵复活：{e.displayName}({e.mercId}) 血量 {reviveRatio * 100f:0.}%（fallenStages={e.fallenStages}）");
        }
    }

    #endregion
}