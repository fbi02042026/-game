using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局内构筑导演（肉鸽影子的落地点）：
/// 1) 进战把 <see cref="RunLoadout"/> 的技能/佣兵灌进战斗；
/// 2) 升级时弹「方向 → 三选一」（技能 / 佣兵 / 装备）；
/// 3) 技佣兵与技能全部走本局构筑，不写城镇酒馆存档；本局结束即作废，金币/天赋石照常保留。
/// 佣兵不再随关卡自动升级，只能靠抽卡升。
/// </summary>
public class RunDraftDirector : MonoBehaviour
{
    public static RunDraftDirector Instance { get; private set; }

    /// <summary>局内佣兵相对玩家脚下的生成偏移。</summary>
    const float MercSpawnOffsetX = 1.6f;

    public static RunDraftDirector Ensure(BattleManager bm)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("RunDraftDirector");
        if (bm != null) go.transform.SetParent(bm.transform, false);
        return go.AddComponent<RunDraftDirector>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 进战
    // ============================================================

    public void OnRunStart()
    {
        var job = PlayerJobDefs.GetSelected();

        // 引导局：走内存模式构筑，不读不写 PlayerPrefs，撤离后整包丢掉
        bool tutorial = BattleManager.Instance != null
            && BattleManager.Instance.Rules != null
            && BattleManager.Instance.Rules.EnableRunDraft;

        bool resumed = false;
        if (tutorial)
            RunLoadout.BeginMemoryMode(job);
        else
            resumed = RunLoadout.ResumeOrBegin(job);

        if (resumed) RestoreHeroProgress();
        SyncHeroLevel();

        RebuildPlayerSkills();
        RestoreRunMercs();
        RunLoadout.Save();

        RunSkillBarUI.Ensure();
        RunSkillBarUI.Refresh();

        if (tutorial) return;

        if (resumed && RunLoadout.Data != null)
        {
            GlobalToastUI.Show(
                $"继续上一局：第{RunLoadout.Data.chapter}章　Lv{RunLoadout.HeroLevel}　" +
                $"技能{RunLoadout.SkillIds().Count}　佣兵{RunLoadout.Mercs().Count}");
        }
        else
        {
            GlobalToastUI.Show($"本局构筑已生成：{PlayerJobDefs.Get(job).DisplayName} · 技能{RunLoadout.SkillIds().Count}");
        }
    }

    /// <summary>
    /// 续关等级恢复 —— 等级系统已停用（属性成长只走城镇天赋），保留空实现供续关流程调用。
    /// </summary>
    void RestoreHeroProgress()
    {
        var hero = Hero.Instance;
        if (hero != null) hero.pendingLevelUps = 0;
    }

    void SyncHeroLevel()
    {
        var hero = Hero.Instance;
        if (hero != null) RunLoadout.SyncHeroLevel(hero.level);
    }

    /// <summary>每帧检查升级队列（由 BattleManager.Update 调用）。</summary>
    public void Tick()
    {
        if (LevelUpDraftUI.IsShowing) return;

        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle) return;
        if (bm.IsTutorialRun)
        {
            // 引导关的三选一由 TutorialDirector 脚本化触发（打完精英后那一次），
            // 这里不再自然插卡；升级队列要清掉，否则引导一结束会连弹好几次。
            ClearTutorialLevelUpQueue();
            return;
        }
        if (!bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        // 传送门已开（等玩家走进去）时不再插卡，避免打断撤离节奏
        if (bm.PortalWalkMode) return;

        var hero = Hero.Instance;
        if (hero == null || hero.isDead) return;

        SyncHeroLevel();
        // 等级系统已停用：不再有升级抽卡，队列直接清零（属性成长只走城镇天赋）
        if (hero.pendingLevelUps > 0) hero.pendingLevelUps = 0;
        return;
    }

    /// <summary>
    /// 引导局：清掉自然升级队列。等级与属性在 <c>LevelSystem.OnLevelUp</c> 里已经加过了，
    /// 这里丢的只是"要不要弹抽卡"的计数，不会让玩家少拿属性。
    /// </summary>
    static void ClearTutorialLevelUpQueue()
    {
        var hero = Hero.Instance;
        if (hero == null || hero.pendingLevelUps <= 0) return;
        hero.pendingLevelUps = 0;
    }

    // ============================================================
    // 通关：只记录进度（不再自动升级佣兵、不再额外出三选一）
    // ============================================================

    /// <summary>阶段推进时的落档：章节/关卡进度写进本局构筑，供「继续上一局」。</summary>
    public void NoteStageProgress()
    {
        if (!RunLoadout.IsActive || RunLoadout.Data == null) return;

        var ch = ChapterManager.Instance;
        if (ch != null) RunLoadout.Data.chapter = Mathf.Max(1, ch.currentChapter);

        var bm = BattleManager.Instance;
        if (bm != null && bm.currentStage != null)
            RunLoadout.Data.stageIndex = bm.currentStage.stageIndex;

        SyncHeroLevel();
        RunLoadout.Save();
        RestoreRunMercs();
        RefreshSkillPower();
    }

    // ============================================================
    // 选卡结算
    // ============================================================

    void OnCardPicked(DraftCard card)
    {
        string msg = ApplyCard(card);
        RunLoadout.Save();
        RunSkillBarUI.Refresh();
        if (!string.IsNullOrEmpty(msg))
            GlobalToastUI.Show(msg);
    }

    /// <summary>
    /// 按技能 id 造一张「获得新技能」卡（教程固定卡组用，不随机）。
    /// id 无效时返回 IsValid=false 的卡，调用方自己过滤。
    /// </summary>
    public static DraftCard BuildSkillCardById(string id)
    {
        var def = PlayerSkillDefs.GetById(id);
        if (def == null || string.IsNullOrEmpty(def.id)) return default;

        var rar = SkillDraftMeta.Rarity(id);
        var tag = SkillDraftMeta.Tag(id);
        string tagName = SynergyTagUtil.DisplayName(tag);
        string prefix = string.IsNullOrEmpty(tagName) ? "" : "[" + tagName + "] ";
        string body = string.IsNullOrEmpty(def.numbers) ? def.desc : def.numbers;

        return new DraftCard
        {
            Kind = DraftCardKind.SkillNew,
            Id = id,
            Title = def.displayName,
            Desc = prefix + body,
            Rarity = rar,
            Tag = tag,
            Star = 1,
            IsAffinity = SkillDraftMeta.IsAffinity(id, PlayerJobDefs.GetSelected()),
            PowerDelta = 90 + (int)rar * 90
        };
    }

    /// <summary>把一张卡的效果真正落进本局构筑（教程三选一也走这里，确保和正式局同一条链路）。</summary>
    public string ApplyCard(DraftCard card)
    {
        switch (card.Kind)
        {
            case DraftCardKind.SkillNew:
                if (!RunLoadout.TryAddSkill(card.Id)) return "技能槽已满";
                RebuildPlayerSkills();
                return $"获得技能：{card.Title}";

            case DraftCardKind.SkillUp:
                if (!RunLoadout.TryUpgradeSkill(card.Id)) return $"{card.Title} 已满星";
                RebuildPlayerSkills();
                return $"{card.Title} 升至 ★{card.Star}";

            case DraftCardKind.MercRecruit:
                return RecruitMerc(card);

            case DraftCardKind.MercLevelUp:
            {
                int lv = RunLoadout.TryLevelUpMerc(card.Id);
                if (lv <= 0) return "佣兵已离队";
                RefreshMercUnit(card.Id);
                return $"{card.Title} 升至 Lv{lv}";
            }

            case DraftCardKind.MercStarUp:
                if (!RunLoadout.TryUpgradeMercStar(card.Id)) return $"{card.Title} 已满星";
                RefreshMercUnit(card.Id);
                return $"{card.Title} 升至 ★{card.Star}（Lv 同时 +1）";

            case DraftCardKind.Equip:
                return ApplyEquip(card);

            case DraftCardKind.PowerUp:
                return ApplyPowerUp(card.Id, card.Title);

            default:
                return null;
        }
    }

    string ApplyEquip(DraftCard card)
    {
        var eq = card.Equip;
        if (eq == null) return "装备数据缺失";

        var bag = GridBackpackSystem.Instance;
        if (bag != null && bag.TryEquipFromReward(eq))
        {
            var hero = Hero.Instance;
            if (hero != null) hero.RecalcAttr();
            return $"获得装备：{card.Title}";
        }
        return $"背包已满，{card.Title} 未能入包";
    }

    string ApplyPowerUp(string id, string title)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return null;

        switch (id)
        {
            case "pow_hp":
                bm.tempBuffs.Add(new AttrBonusData { attrType = AttrType.MaxHp, value = 0.12f, isPercent = true });
                break;
            case "pow_spd":
                bm.tempBuffs.Add(new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.10f, isPercent = true });
                break;
            default:
                bm.tempBuffs.Add(new AttrBonusData { attrType = AttrType.Attack, value = 0.14f, isPercent = true });
                break;
        }

        var hero = Hero.Instance;
        if (hero != null) hero.RecalcAttr();
        return $"{title} 已生效";
    }

    string RecruitMerc(DraftCard card)
    {
        var data = BuildMercData(card.Id, card.HireId, card.MercLevel, card.Star);
        if (data == null) return "佣兵数据缺失";

        var entry = new RunMercEntry
        {
            hireId = data.hireId,
            mercId = data.mercId,
            displayName = data.displayName,
            nickname = data.nickname,
            level = data.level,
            star = data.star,
            skillId = data.skillId,
            passiveSkillId = data.passiveSkillId
        };
        if (!RunLoadout.TryAddMerc(entry)) return "佣兵位已满";
        SpawnRunMerc(entry);
        return $"佣兵加入：{entry.displayName}（Lv{entry.level} ★{entry.star}）";
    }

    static MercenaryData BuildMercData(string assetId, string hireId, int level, int star)
    {
        if (string.IsNullOrEmpty(assetId)) return null;
        if (!MercRosterDefs.TryGetByAssetId(assetId, out var def))
            return null;

        var data = new MercenaryData
        {
            mercId = string.IsNullOrEmpty(def.AssetId) ? assetId : def.AssetId,
            hireId = string.IsNullOrEmpty(hireId) ? def.HireId : hireId,
            displayName = def.Name,
            nickname = def.Nickname,
            level = Mathf.Max(1, level),
            star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5),
            favorLevel = 1,
            skillId = def.ActiveSkillId,
            passiveSkillId = def.PassiveSkillId
        };
        if (!string.IsNullOrEmpty(data.hireId) && string.IsNullOrEmpty(data.displayName))
            data.displayName = data.hireId;
        MercSkillMigrate.AlignMercenary(data);
        return data;
    }

    // ============================================================
    // 技能装配
    // ============================================================

    /// <summary>
    /// 按 RunLoadout 当前技能顺序重建 SkillSystem。
    /// 对外：V6 拖拽排序（LevelUpDraftUI / 构筑面板）改完顺序后要立刻刷新释放优先级。
    /// </summary>
    public void RebuildPlayerSkills()
    {
        var sys = SkillSystem.Instance;
        if (sys == null) return;

        var job = PlayerJobDefs.GetSelected();
        var ids = RunLoadout.SkillIds();
        var list = new List<SkillSystem.ActiveSkill>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            var sk = BuildRunSkill(ids[i], job);
            if (sk != null) list.Add(sk);
        }
        sys.SetPlayerSkills(list);
    }

    /// <summary>星级 + 流派势能 + 职业亲和 → 本局实际技能数值。</summary>
    public static SkillSystem.ActiveSkill BuildRunSkill(string skillId, PlayerJobId job)
    {
        var active = SkillRegistry.Instance != null ? SkillRegistry.Instance.GetActiveSkill(skillId) : null;
        if (active == null) return null;

        int star = RunLoadout.StarOf(skillId);
        var tag = SkillDraftMeta.Tag(skillId);
        float dmgMul = SkillDraftMeta.StarDamageMul(star) * RunLoadout.ThemeDamageMul(tag);
        float cdMul = SkillDraftMeta.StarCooldownMul(star);
        if (SkillDraftMeta.IsAffinity(skillId, job))
        {
            dmgMul *= SkillDraftMeta.AffinityDamageMul;
            cdMul *= SkillDraftMeta.AffinityCooldownMul;
        }

        active.damageMultiplier *= dmgMul;
        active.baseDamage *= dmgMul;
        active.cooldown = Mathf.Max(0.6f, active.cooldown * cdMul);
        return active;
    }

    // ============================================================
    // 佣兵装配
    // ============================================================

    void RestoreRunMercs()
    {
        var bm = BattleManager.Instance;
        var mm = MercenaryManager.Instance;
        if (bm == null || mm == null) return;

        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var e = mercs[i];
            if (e == null) continue;
            if (IsMercAlreadyOut(e)) continue;
            SpawnRunMerc(e);
        }
    }

    static bool IsMercAlreadyOut(RunMercEntry e)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || e == null) return false;
        var active = mm.GetActiveMercs();
        for (int i = 0; i < active.Count; i++)
        {
            var m = active[i];
            if (m == null) continue;
            if (!string.IsNullOrEmpty(e.hireId) && m.hireId == e.hireId) return true;
            if (m.mercId == e.mercId) return true;
        }
        return false;
    }

    static Mercenary FindOutMerc(string hireIdOrMercId)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || string.IsNullOrEmpty(hireIdOrMercId)) return null;
        var active = mm.GetActiveMercs();
        for (int i = 0; i < active.Count; i++)
        {
            var m = active[i];
            if (m == null) continue;
            if (m.hireId == hireIdOrMercId || m.mercId == hireIdOrMercId) return m;
        }
        return null;
    }

    /// <summary>抽卡升级/升星后：把场上该佣兵按新等级重算（Init 会重置满血，视为顺带补血）。</summary>
    void RefreshMercUnit(string hireIdOrMercId)
    {
        var merc = FindOutMerc(hireIdOrMercId);
        var entry = RunLoadout.FindMerc(hireIdOrMercId);
        if (merc == null || entry == null) return;

        merc.Init(entry.mercId, Mathf.Max(1, entry.level));
        merc.SetupBattleSkills(entry.skillId, entry.passiveSkillId);
        merc.SetDisplayName(entry.displayName, entry.nickname);
        if (!string.IsNullOrEmpty(entry.hireId)) merc.SetHireId(entry.hireId);
        if (!string.IsNullOrEmpty(entry.displayName))
            merc.gameObject.name = "Merc_" + entry.displayName;
        Debug.Log($"[RunDraft] 佣兵刷新：{entry.displayName} Lv{entry.level} ★{entry.star}");
    }

    void SpawnRunMerc(RunMercEntry e)
    {
        var bm = BattleManager.Instance;
        var mm = MercenaryManager.Instance;
        if (bm == null || mm == null || e == null || string.IsNullOrEmpty(e.mercId)) return;

        var hero = bm.hero != null ? bm.hero : Hero.Instance;
        float baseX = hero != null ? UnitBase.GetCombatX(hero) + MercSpawnOffsetX : MercSpawnOffsetX;
        float z = bm.unitRoot != null ? bm.unitRoot.position.z : 0f;
        int slot = mm.GetActiveMercs().Count;
        if (hero != null)
            baseX = UnitCrowd.GetMercDesiredCombatX(hero, null, slot);

        var merc = mm.SpawnMercenary(e.mercId, new Vector3(baseX, UnitBase.GROUND_Y, z), Mathf.Max(1, e.level));
        if (merc == null) return;

        merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
        merc.SetPartyIndex(slot);
        merc.SetDisplayName(e.displayName, e.nickname);
        if (!string.IsNullOrEmpty(e.hireId))
            merc.SetHireId(e.hireId);
        if (!string.IsNullOrEmpty(e.displayName))
            merc.gameObject.name = "Merc_" + e.displayName;

        if (!bm.allyUnits.Contains(merc))
            bm.allyUnits.Add(merc);
        merc.OnDead += bm.OnMercenaryDead;
        merc.Face(1);

        Debug.Log($"[RunDraft] 局内佣兵出场：{e.displayName}({e.mercId}) Lv{e.level} ★{e.star}");
    }

    // ============================================================
    // 战力可视化
    // ============================================================

    public static void RefreshSkillPower()
    {
        RunSkillBarUI.Refresh();
    }
}
