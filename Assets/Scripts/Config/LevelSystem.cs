using UnityEngine;

/// <summary>
/// 等级系统（2026-10-25 复活，主人拍板「升级提示 + 每个职业提升不同的一个属性」）：
/// · 经验来源 = 【2026-10-25 主人拍板「玩家升级按完成的一次关卡给一次经验，目前只有主线和支线给」】
///   完成一次关卡给一次（35 + 章递增，与 HiddenLevelSystem 同源同量）；击杀不喂经验（旧 AddKillExp 已删，
///   HiddenLevelSystem 隐藏账照旧只管裂缝掉落稀有度）；白名单 = 主线 Normal / 终章追查 Finale / 支线岛 SideIsland，
///   活动副本（金币/徽记/卷轴）、无限裂隙、引导局一律不给（单一出口见 StageFeedsExp）；
/// · 账本 = 【2026-10-25 矫正】真源在存档（SaveData.playerLevel/playerExp），跨关卡/跨章累计。
///   原挂在 RunLoadout（局内）上，但 10-24 拍板「只要打完就清」后每关都是新局 → 等级每关归 1、永远升不了级；
/// · 升级收益 = 每级「本职业倾向」+1 点（映射见 <see cref="JobTendencyAttr"/>），
///   进战斗按「基底 + (level-1)」一次性重放（见 <see cref="RestoreHeroLevel"/>），不在局内逐级结算；
/// · 升级反馈 = Level Up 特效（结算时刻英雄在场）+ 恭喜升级弹窗（LevelUpPopup，升级前→升级后，
///   点黑幕关闭；【2026-10-25 主人拍板】弹窗不在战斗里弹，挂起到打完回冒险界面（Town 就绪）才触发）；
/// </summary>
public static class LevelSystem
{
    public static int GetExpForLevel(int level) => 50 + level * 20;

    /// <summary>玩家当前等级（存档真源；无存档回落 1）。名牌/抽卡/蓝量等只读消费统一走这里。</summary>
    public static int CurrentLevel
    {
        get
        {
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            return data != null ? Mathf.Max(1, data.playerLevel) : 1;
        }
    }

    /// <summary>
    /// 职业 → 倾向属性（唯一出口）：狂战=力量、法师/牧师=智力、弓手=敏捷、剑盾/重装=体质。
    /// 四属性对六职业，魔法系共智力、坦克系共体质；主人想改职业映射只动这张表（一行一职业）。
    /// </summary>
    public static AttrType JobTendencyAttr(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.Berserker:   return AttrType.Strength;
            case PlayerJobId.Mage:        return AttrType.Intelligence;
            case PlayerJobId.Priest:      return AttrType.Intelligence;
            case PlayerJobId.Ranger:      return AttrType.Agility;
            case PlayerJobId.SwordShield: return AttrType.Vitality;
            case PlayerJobId.Heavy:       return AttrType.Vitality;
            default:                      return AttrType.Vitality;
        }
    }

    /// <summary>
    /// 职业倾向的**玩家口径展示名**（2026-10-25 主人拍板「剑盾加血 游侠加攻速 法师加魔攻」）。
    /// 弹窗/文案按效果说人话，不报四维源属性名（体质/敏捷/智力）；机制仍走四维源→派生：
    /// 剑盾=体质→每级+10血(+1防)、游侠=敏捷→每级+1%攻速(+0.5%暴击)、法师=智力→每级+2魔攻(+乘区)。
    /// 想改叫法只动这张表（一行一职业）。唯一出口。
    /// </summary>
    public static string JobTendencyLabel(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.SwordShield: return "生命";
            case PlayerJobId.Ranger:      return "攻速";
            case PlayerJobId.Mage:
            case PlayerJobId.Priest:      return "魔攻";
            case PlayerJobId.Heavy:       return "生命";
            case PlayerJobId.Berserker:   return "力量";
            default:                      return "生命";
        }
    }

    /// <summary>倾向属性设为绝对值（进战斗回填用：基底 + level-1）。</summary>
    static void SetTendencyPoint(AttrSystem attr, AttrType tendency, int value)
    {
        switch (tendency)
        {
            case AttrType.Strength:     attr.Strength = value; break;
            case AttrType.Intelligence: attr.Intelligence = value; break;
            case AttrType.Agility:      attr.Agility = value; break;
            default:                    attr.Vitality = value; break;
        }
    }

    /// <summary>
    /// 把「倾向属性 = 基底 + (level-1)」写到任意 AttrSystem 上（2026-10-26 新增公开出口）。
    /// 给 <see cref="PowerCalc.SimPlayerSegment"/> 的换装模拟副本用 —— 之前副本只写 BASE_* 基底，
    /// 高等级玩家的倾向属性（狂战力量/法师智力/游侠敏捷/剑盾体质）模拟侧吃不到，
    /// 暴击/攻速这类随属性放大的词条净差会偏低。level=1 时为 no-op（与 RestoreHeroLevel 同口径）。
    /// 写完由调用方自行 RecalcAllAttr。
    /// </summary>
    public static void ApplyTendencyToAttr(AttrSystem attr, int level)
    {
        if (attr == null || level <= 1) return;
        AttrType tendency = JobTendencyAttr(PlayerJobDefs.GetSelected());
        SetTendencyPoint(attr, tendency, BaseOf(tendency) + level - 1);
    }

    static int BaseOf(AttrType tendency)
    {
        switch (tendency)
        {
            case AttrType.Strength:     return GameConfig.BASE_STRENGTH;
            case AttrType.Intelligence: return GameConfig.BASE_INTELLIGENCE;
            case AttrType.Agility:      return GameConfig.BASE_AGILITY;
            default:                    return GameConfig.BASE_VITALITY;
        }
    }

    // ============================================================
    // 经验入口（唯一出口 = AddStageClearExp；调用点只有 BattleManager.BeginRewardSequence 一处）
    // ============================================================

    /// <summary>
    /// 当前模式是否喂玩家经验（白名单单一出口）：
    /// 主线 Normal / 终章追查 Finale / 支线岛 SideIsland 给，其余（金币/徽记/卷轴副本、无限裂隙、引导局）不给。
    /// 【2026-10-25 主人拍板「玩家升级按完成的一次关卡给一次经验，目前只有主线和支线给」】
    /// </summary>
    static bool StageFeedsExp
    {
        get
        {
            var bm = BattleManager.Instance;
            var mode = bm != null ? bm.Mode : null;
            if (mode == null) return false;
            var id = mode.Id;
            return id == BattleModeId.Normal
                || id == BattleModeId.SideIsland
                || id == BattleModeId.Finale;
        }
    }

    /// <summary>通关经验：与 HiddenLevelSystem.AddStageClearExp 同量（35 + 章递增）。每完成一次关卡调一次。</summary>
    public static void AddStageClearExp(int chapter)
    {
        if (!StageFeedsExp) return;

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[LevelSystem] AddStageClearExp 失败：SaveData 为空，本关经验不发（不兜底）");
            return;
        }

        int before = Mathf.Max(1, data.playerLevel);
        int exp = GameConfig.HIDDEN_EXP_STAGE_CLEAR
            + Mathf.Max(0, chapter - 1) * GameConfig.HIDDEN_EXP_STAGE_CLEAR_PER_CHAPTER;

        data.playerExp += exp;
        bool leveled = false;
        int guard = 0;
        while (data.playerExp >= GetExpForLevel(data.playerLevel) && guard++ < 64)
        {
            data.playerExp -= GetExpForLevel(data.playerLevel);   // 本级门槛扣掉，余量结转下一级
            data.playerLevel++;
            leveled = true;
        }

        if (!leveled) return;

        // 立刻落盘：升级是关键进度，不靠下一笔存档兜
        SaveSystem.Instance?.Save();

        // 反馈：英雄脚下 Level Up 特效（结算时刻英雄还在场）。恭喜升级弹窗不在这里弹——
        // 【2026-10-25 主人拍板「战斗结束回到冒险界面才触发」】只挂起，Town 就绪后由
        // TownSceneBootstrap 调 LevelUpPopup.FlushPending 弹出（见 LevelUpPopup.QueueForTown）。
        var hero = Hero.Instance;
        if (hero != null) BattleVFXSystem.Instance?.PlayLevelUp(hero.transform.position);
        LevelUpPopup.QueueForTown(before, Mathf.Max(1, data.playerLevel));
    }

    // ============================================================
    // 进战斗回填（RunDraftDirector.RestoreHeroProgress 调用）
    // ============================================================

    /// <summary>
    /// 把存档里的等级/经验回填到英雄，并把「倾向属性 = 基底 + (level-1)」重放到 attr
    /// （Hero.InitNewRun 已 ResetToBase 归 1，不重放则吃不到等级成长）。
    /// level=1 时为 no-op。等级是玩家永久属性，全模式同一本账（引导局新档自然 =1）。
    /// </summary>
    public static void RestoreHeroLevel(Hero hero)
    {
        if (hero == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        int level = data != null ? Mathf.Max(1, data.playerLevel) : 1;
        int exp = data != null ? Mathf.Max(0, data.playerExp) : 0;

        hero.level = level;
        hero.currentExp = exp;
        hero.expToNextLevel = GetExpForLevel(level);
        if (level <= 1) return;

        AttrType tendency = JobTendencyAttr(PlayerJobDefs.GetSelected());
        SetTendencyPoint(hero.attr, tendency, BaseOf(tendency) + level - 1);
        hero.RecalcAttr();
        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
    }
}
