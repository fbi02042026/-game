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

        // 开局启动抽奖币：**每局只补一次**（2026-10-05 主人拍板「开始金币是每局的，不是每关的」）。
        // 续关（resumed=true）不再补。
        // ⚠ 2026-10-05 主人最终口径：**战斗内的都是局内的** —— 所以「每关通关 +100」发的也是
        //   抽奖币 SlotCoin（见 SlotMachineSystem.GrantStageCoins），「每关都能抽」就是靠它。
        //   局外金币 Gold **只有**结算界面按通过关卡数发（StageGoldDefs），跟抽奖无关。
        //   抽奖币的全部来源（**就这三条，别处不再有第二个给币口径**）：
        //     ① 每局携带 240（EnsureStarterCoins）+ 初始资金天赋每级 80
        //     ② 每关通关 +100（GrantStageCoins，金币本不发 —— Mode.GrantsRunCoins=false）
        //     ③ 抽中「金币」安慰奖（一抽价 × 0.5 = 40）
        //   **局末清零**（回城 / 死亡 / 撤离）；章末选「进下一章」不清零，币带过去。
        //   一章 10 关 ≈ 14 抽（3 + 1×9 + 安慰奖回血约 +1）；开局 240 = 3 抽。
        if (!resumed) SlotMachineSystem.EnsureStarterCoins();

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
        // 2026-10-10 主人反馈：抽中佣兵的 toast 弹出时，底部头像槽 / 佣兵技能槽还是空的。
        // 根因是「先弹 toast、后刷 UI」—— 这里改成先刷底部 HUD，再弹 toast，玩家一眼能同时看到两处。
        if (BattleUI.Instance != null)
        {
            BattleUI.Instance.UpdateCharacterSlots();
            BattleUI.Instance.UpdateMercSkillSlots();
            BattleUI.Instance.UpdateSkillAvatars();
        }
        if (!string.IsNullOrEmpty(msg))
            GlobalToastUI.Show(msg, true);   // 2026-10-06 主人拍板：物品/技能/佣兵获得，force 弹出
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
        TryApplyCard(card, out string msg);
        return msg;
    }

    /// <summary>
    /// 同 <see cref="ApplyCard"/>，但<b>明确告诉调用方「到底生效了没有」</b>。
    ///
    /// <para>2026-10-05 补：抽奖是<b>先扣钱、后发奖</b>的，原来只有一句文案，
    /// 调用方分不清「技能槽已满 / 背包已满 / 佣兵位已满」是失败还是成功 ——
    /// 于是出现「钱扣了、东西没拿到」的漏洞。现在判据只有这一个出口，
    /// 抽奖侧拿到 false 就<b>不扣钱、不推进保底定序</b>。</para>
    ///
    /// <para><paramref name="msg"/>：成功 = 获得文案，失败 = 失败原因（可直接弹给玩家）。</para>
    /// </summary>
    public bool TryApplyCard(DraftCard card, out string msg)
    {
        msg = null;
        switch (card.Kind)
        {
            case DraftCardKind.SkillNew:
                if (!RunLoadout.TryAddSkill(card.Id)) { msg = "技能槽已满"; return false; }
                RebuildPlayerSkills();
                // 【2026-10-06 主人拍板】新获得的技能打「新」标记，点「继续」才消失。
                NewLootMarks.Mark(NewLootMarks.KindSkill, card.Id);
                msg = $"获得技能：{card.Title}";
                return true;

            case DraftCardKind.SkillUp:
                if (!RunLoadout.TryUpgradeSkill(card.Id)) { msg = $"{card.Title} 已满星"; return false; }
                RebuildPlayerSkills();
                msg = $"{card.Title} 升至 ★{card.Star}";
                return true;

            case DraftCardKind.MercRecruit:
                return RecruitMerc(card, out msg);

            case DraftCardKind.MercLevelUp:
            {
                int lv = RunLoadout.TryLevelUpMerc(card.Id);
                if (lv <= 0) { msg = "佣兵已离队"; return false; }
                RefreshMercUnit(card.Id);
                msg = $"{card.Title} 升至 Lv{lv}";
                return true;
            }

            case DraftCardKind.MercStarUp:
                if (!RunLoadout.TryUpgradeMercStar(card.Id)) { msg = $"{card.Title} 已满星"; return false; }
                RefreshMercUnit(card.Id);
                msg = $"{card.Title} 升至 ★{card.Star}（Lv 同时 +1）";
                return true;

            case DraftCardKind.Equip:
                // 2026-10-05 主人拍板：装备替换必须弹窗问玩家，不能直接塞进去。
                // 弹窗是异步的，所以装备一律走 CoApplyEquipCard（协程）；
                // 这里 fail closed —— 谁在协程外偷偷调用都会立刻暴露，绝不会静默走无确认的旧路径。
                msg = "装备奖励必须走替换确认流程（CoApplyEquipCard）";
                return false;

            case DraftCardKind.Gold:
            {
                // 主人拍板「抽中的金币是当局抽奖用的」→ 给的是抽奖币（SlotCoin），不是城镇那套通用金币
                if (card.Amount <= 0) { msg = "金币奖励数值异常"; return false; }
                ResourceWallet.Add(ResourceWallet.ResourceType.SlotCoin, card.Amount, save: true, notify: true);
                msg = $"获得金币 ×{card.Amount}";
                return true;
            }

            // ⚠【2026-10-05 已删除】case DraftCardKind.Material —— 主人拍板「强化石不能抽奖得到」，
            // 强化石只剩铁匠铺 / 分解两条路。走到 default 会 LogError 报错，不会静默当成功。

            case DraftCardKind.PowerUp:
            {
                // 强化卡要写 BattleManager.tempBuffs，不在战斗中就什么都没加上 —— 算失败，别当成功吞掉
                string m = ApplyPowerUp(card.Id, card.Title);
                if (string.IsNullOrEmpty(m)) { msg = "强化未能生效（不在战斗中）"; return false; }
                msg = m;
                return true;
            }

            default:
                msg = "无法识别的奖励";
                return false;
        }
    }

    /// <summary>
    /// 装备卡的生效流程 —— **异步**，因为要先问玩家换不换。
    ///
    /// <para>2026-10-05 主人拍板：「只会抽中高级的直接替换低级的，然后低级的变成材料，
    /// 不过需要弹出个弹窗告诉玩家是否替换」。</para>
    ///
    /// 两条路都算「生效了」（玩家拿到了东西，就该扣钱）：
    /// <list type="bullet">
    /// <item>换上新的 → 新件入包，旧件由 <see cref="GridBackpackSystem.TryEquipFromReward"/> 折成强化石；</item>
    /// <item>留着旧的 → 新件同样折成强化石（不占背包，也不白抽）。</item>
    /// </list>
    /// 同部位没有旧件时不弹窗，直接入包。
    /// </summary>
    /// <param name="done">(是否生效, 给玩家看的文案)。</param>
    public IEnumerator CoApplyEquipCard(DraftCard card, System.Action<bool, string> done)
    {
        var eq = card.Equip;
        if (eq == null)
        {
            done?.Invoke(false, "装备数据缺失");
            yield break;
        }

        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            done?.Invoke(false, "背包系统未就绪");
            yield break;
        }

        // 谁会被顶掉 —— 判据只有 GridBackpackSystem.FindSameSlotEquip 一处
        var old = bag.FindSameSlotEquip(eq);
        bool replace = true;
        if (old != null && old != eq)
        {
            bool chose = false;
            yield return EquipReplaceConfirmUI.CoShow(eq, old, r => chose = r);
            replace = chose;
        }

        if (replace)
        {
            // 【2026-10-06 主人报「抽到的钉锤没换到装备栏、也没换到玩家形象上」——根因修复】
            //
            // 旧写法 <c>TryEquipFromReward</c> → <c>TryAddUniqueBySlot</c> → <c>TryAcquireLoadoutItem</c>，
            // 那条链的注释白纸黑字写着「**只入包不穿槽**」：武器只落进背包格子，
            // 不写 <c>_equippedBySlot</c>（=装备栏），<c>NotifyCostumeChanged</c> 这条刷新 SPUM 的
            // 链路自然也就读不到它 —— 装备栏与玩家形象两处都纹丝不动，符合主人看到的现象。
            //
            // 真正「穿上」的出口是 <c>TryEquipDirect</c>：走 <c>EquipItem</c> → 写穿戴槽 +
            // <c>NotifyCostumeChanged()</c>（内含 <c>HeroCostumeManager.RefreshCostume</c> 刷 SPUM 时装），
            // 顺带把顶下来的旧件 <c>ScrapEquip</c> 成强化石 —— 正好就是主人定的「低级的变成材料」。
            if (!bag.TryEquipDirect(eq))
            {
                done?.Invoke(false, $"{NameOf(card, eq)} 穿戴失败");
                yield break;
            }
            var hero = Hero.Instance;
            if (hero != null) hero.RecalcAttr();
            // 【2026-10-06 主人拍板】换上的新装备打「新」标记，点「继续」才消失。
            NewLootMarks.Mark(NewLootMarks.KindEquip, eq.templateId);
            // 2026-10-10 主人拍板：装备到账文案统一「恭喜获得 XXX」（原「获得装备：XXX」）
            done?.Invoke(true, $"恭喜获得 {NameOf(card, eq)}");
            yield break;
        }

        // 玩家选择留着旧的：新件拆成强化石（ScrapEquip 自带提示与发料）
        bag.ScrapEquip(eq);
        done?.Invoke(true, $"{NameOf(card, eq)} 已拆成强化石");
    }

    static string NameOf(DraftCard card, EquipInstance eq)
    {
        if (!string.IsNullOrEmpty(card.Title)) return card.Title;
        if (eq != null && !string.IsNullOrEmpty(eq.equipName)) return eq.equipName;
        return "装备";
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
                // 2026-09-29：攻击% 按职业分流（法师/牧师 → MagicAttack）
                bm.tempBuffs.Add(new AttrBonusData { attrType = PlayerJobBaseStats.CurrentAttackAttr(), value = 0.14f, isPercent = true });
                break;
        }

        var hero = Hero.Instance;
        if (hero != null) hero.RecalcAttr();
        return $"{title} 已生效";
    }

    bool RecruitMerc(DraftCard card, out string msg)
    {
        msg = null;
        var data = BuildMercData(card.Id, card.HireId, card.MercLevel, card.Star);
        if (data == null) { msg = "佣兵数据缺失"; return false; }

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
        if (!RunLoadout.TryAddMerc(entry)) { msg = "佣兵位已满"; return false; }
        SpawnRunMerc(entry);
        // 【2026-10-06 主人拍板】佣兵技能一样要标「新」（主人点名：不管是佣兵技能还是装备）。
        NewLootMarks.Mark(NewLootMarks.KindSkill, entry.skillId);
        // 【2026-10-07 主人拍板】抽中佣兵要**报喜**，不要干巴巴一句「佣兵加入：XXX」。
        // 主人原话：「显示恭喜抽中史诗佣兵 实力大增什么的」—— 引导第 2 抽开出来的就是这位。
        // 稀有度名字只有 SkillRarityUtil 一个真源（与 SlotMachineSystem.BuildGuaranteedMercCard 同款写法），
        // 不许在这里另写一套「Rare→稀有」的枚举映射。
        string rarityName = MercRosterDefs.TryGetByHireId(entry.hireId, out var rd)
            ? SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(rd.Rarity))
            : "";
        string fullName = string.IsNullOrEmpty(entry.nickname)
            ? entry.displayName
            : $"{entry.displayName}·{entry.nickname}";
        msg = string.IsNullOrEmpty(rarityName)
            ? $"恭喜抽中佣兵·{fullName}，实力大增！"
            : $"恭喜抽中{rarityName}佣兵·{fullName}，实力大增！";
        return true;
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

        // 治疗 / 增益也吃星级：以前只涨冷却，治疗量与增益幅度永远停在 1 星数值。
        // 治疗 = 目标最大生命 × healPercentOfMax + 施法者攻击 × healAtkMul，两段一起涨。
        float buffMul = SkillDraftMeta.StarBuffMul(star) * RunLoadout.ThemeDamageMul(tag);
        if (SkillDraftMeta.IsAffinity(skillId, job))
            buffMul *= SkillDraftMeta.AffinityDamageMul;
        active.healBase *= buffMul;
        active.healPercentOfMax *= buffMul;
        active.healAtkMul *= buffMul;
        active.buffValue *= buffMul;
        if (active.duration > 0f)
            active.duration *= SkillDraftMeta.StarDurationMul(star);
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
        merc.mercStar = Mathf.Clamp(entry.star, 1, 5);   // 2026-10-06：MP 池/回复的星级真源
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

        merc.mercStar = Mathf.Clamp(e.star, 1, 5);       // 2026-10-06：MP 池/回复的星级真源

        // 2026-10-06 主人拍板：阵亡过的佣兵**带惩罚复活**（血 20% 起步，每通过一关 +30%）。
        // SpawnMercenary → Init 会重置满血，所以惩罚必须在这里补砍回去。
        float reviveRatio = RunLoadout.MercReviveHpRatio(e);
        if (reviveRatio < 1f && merc.attr != null)
        {
            float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
            merc.currentHp = Mathf.Max(1f, maxHp * reviveRatio);
            Debug.Log($"[RunDraft] 佣兵 {e.displayName} 阵亡复活：血量 {reviveRatio * 100f:0.}%（蓝条照常灌满）");
        }

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
