using UnityEngine;

/// <summary>
/// 等级系统（2026-10-25 复活，主人拍板「升级提示 + 每个职业提升不同的一个属性」）：
/// · 经验来源 = 【2026-10-25 主人拍板「玩家升级按完成的一次关卡给一次经验，目前只有主线和支线给」】
///   完成一次关卡给一次（90 + 章递增 12；2026-11-05 主人拍板「关卡经验给太少」与隐藏等级经验解耦、单独调参）；
///   击杀不喂经验（旧 AddKillExp 已删，
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
    /// <summary>升到 level 级所需经验【2026-11-05 主人拍板「前期生的快后期慢」】60+30*lv+lv*lv/2（线性+二次项）：
    /// 1→2 只要 90（打完 1-1 升 1 级），后期二次变贵（79→80 要 5,550）；累计到 80 级总需 183,260，
    /// 对齐主线供给 169,260 + 任务 ≈13,400（见 GameConfig.Progression）→ 主线全通+几天日常正好 80。</summary>
    public static int GetExpForLevel(int level) => 60 + level * 30 + level * level / 2;

    public const int MaxLevel = 80;                 // 【2026-11-05 主人拍板「玩家可以满80级」】40→80：配合左列 80 节点严格一级开一个；
                                                    // 倾向属性每级 +1 → 满级 +79（后期难度变软，怪物数值待实测校准）；
                                                    // 经验曲线 60+30lv+lv²/2 → 满级总需 183,260；
                                                    // 主线 169,260+日常 ≈13,400 即可满级（不打困难噩梦也到 80），困难噩梦=加速项（见 GameConfig.Progression）。
    public const int ExpOverflowGoldPerExp = 5;      // 满级后每点溢出经验转金币数（可调）

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

    /// <summary>
    /// 倾向属性每升 1 级的**实际收益**文案（升级弹窗「升级后」格用，delta = 本次升级级数）。
    /// 与 AttrSystem.ApplyDerivedAttributes 派生系数同源，改派生系数时同步这里：
    /// 剑盾/重装=体质→每级+10血、游侠=敏捷→每级+1%攻速、法师/牧师=智力→每级+2魔攻、狂战=力量点本身+1。
    /// 2026-11-11 主人「把文案对齐一下」：弹窗原来显示的是倾向点数（+1），被误读成只加 1 点血，改显真实收益。
    /// </summary>
    public static string JobTendencyGainText(PlayerJobId job, int delta)
    {
        int d = Mathf.Max(1, delta);
        switch (job)
        {
            case PlayerJobId.SwordShield:
            case PlayerJobId.Heavy:       return "+" + 10 * d;   // 血
            case PlayerJobId.Ranger:      return "+" + d + "%";  // 攻速
            case PlayerJobId.Mage:
            case PlayerJobId.Priest:      return "+" + 2 * d;    // 魔攻
            case PlayerJobId.Berserker:   return "+" + d;        // 力量点
            default:                      return "+" + 10 * d;
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
    // 经验入口（AddStageClearExp=关卡经验，白名单见 StageFeedsExp；AddDirectExp=任务奖励经验，2026-10-29）
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

    /// <summary>通关经验：每打完**一个小关**（1-1 这种）调一次（BeginRewardSequence 每小关走一遍）。
    /// 【2026-11-05 主人拍板「1-1升1级、1-2升1级、1-3和1-4合计升1级」】普通 110+32×(章-1)（第1大关 110=恰好升1级），
    /// 困难 150+42×(章-1)、噩梦 190+52×(章-1)（每档都高于前一难度同章）；
    /// 返回本次实际发放经验量，供结算展示；调用方可忽略返回值
    /// 主线 169,260+日常 13,400 ≈ 曲线 cum(80)=183,260 → 不打困难噩梦也能满级，困难噩梦只是加速项。</summary>
    public static int AddStageClearExp(int chapter)
    {
        if (!StageFeedsExp) return 0;

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[LevelSystem] AddStageClearExp 失败：SaveData 为空，本关经验不发（不兜底）");
            return 0;
        }

        int before = Mathf.Max(1, data.playerLevel);
        data.stageLevelUpCapLevel = before + 1;   // 本次通关的等级额度：回城补发每日经验也要受它约束
        // 本局难度：0 普通 / 1 困难 / 2 噩梦（与 ChapterManager 难度隔离同一真源）
        int diff = BattleManager.Instance != null ? BattleManager.Instance.BattleDifficulty : 0;
        int baseExp, perChapter;
        if (diff <= 0)
        {
            baseExp = GameConfig.PLAYER_EXP_STAGE_CLEAR;
            perChapter = GameConfig.PLAYER_EXP_STAGE_CLEAR_PER_CHAPTER;
        }
        else if (diff == 1)
        {
            baseExp = GameConfig.PLAYER_EXP_HARD_STAGE_CLEAR;
            perChapter = GameConfig.PLAYER_EXP_HARD_STAGE_CLEAR_PER_CHAPTER;
        }
        else
        {
            baseExp = GameConfig.PLAYER_EXP_NIGHTMARE_STAGE_CLEAR;
            perChapter = GameConfig.PLAYER_EXP_NIGHTMARE_STAGE_CLEAR_PER_CHAPTER;
        }
        int exp = baseExp + Mathf.Max(0, chapter - 1) * perChapter;

        data.playerExp += exp;
        bool leveled = false;
        int guard = 0;
        // 【2026-11-12 主人拍板】前期每关最多升 1 级；超出门槛的经验留 playerExp 结转下一关，不吞不折金币；
        // 后期升级门槛远高于单笔关卡经验（最高 554，噩梦第 8 章），此封顶自然不触发。
        while (data.playerLevel < MaxLevel && data.playerLevel < before + 1 && data.playerExp >= GetExpForLevel(data.playerLevel) && guard++ < 64)
        {
            data.playerExp -= GetExpForLevel(data.playerLevel);   // 本级门槛扣掉，余量结转下一级
            data.playerLevel++;
            leveled = true;
        }

        // 满级后溢出经验 → 金币（2026-10-30 主人拍板：满级后经验转金币，避免属性溢出）
        if (data.playerLevel >= MaxLevel && data.playerExp > 0)
        {
            int overflowGold = data.playerExp * ExpOverflowGoldPerExp;
            data.playerExp = 0;
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, overflowGold, save: false, notify: true);
            SaveSystem.Instance?.Save();   // 溢出金币要落盘（即便未升级）
        }

        if (!leveled) return exp;

        // 立刻落盘：升级是关键进度，不靠下一笔存档兜
        SaveSystem.Instance?.Save();

        // 【2026-11-05 主人拍板】升级联动天赋：右列门槛（=人物等级）刚跨过 → toast + 红点 + 天赋页开通演出
        TalentSystem.NotifyLevelUp(before, Mathf.Max(1, data.playerLevel));

        // 反馈：英雄脚下 Level Up 特效（结算时刻英雄还在场）。恭喜升级弹窗不在这里弹——
        // 【2026-10-25 主人拍板「战斗结束回到冒险界面才触发」】只挂起，Town 就绪后由
        // TownSceneBootstrap 调 LevelUpPopup.FlushPending 弹出（见 LevelUpPopup.QueueForTown）。
        var hero = Hero.Instance;
        if (hero != null) BattleVFXSystem.Instance?.PlayLevelUp(hero.transform.position);
        LevelUpPopup.QueueForTown(before, Mathf.Max(1, data.playerLevel));
        return exp;
    }

    /// <summary>
    /// 直接加经验（2026-10-29 新增：每日任务奖励经验出口，主人拍板「有些奖励可以给些经验」）。
    /// 与 AddStageClearExp 同一本账（存档 playerExp/playerLevel），但不走关卡白名单、不放英雄 VFX；
    /// 调用时机都在城镇内（DailyTaskSystem.Settle 由任务栏轮询驱动），升级弹窗立即 Flush，不挂到回城。
    /// 可选参数 capLevel = 升级到的绝对等级上限：回城补发每日通关任务传本次通关额度，其它日常任务传 playerLevel + 1（单个最多升 1 级）；0 = 不限制。
    /// </summary>
    public static void AddDirectExp(int exp, int capLevel = 0)
    {
        if (exp <= 0) return;

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[LevelSystem] AddDirectExp 失败：SaveData 为空，经验不发（不兜底）");
            return;
        }

        int before = Mathf.Max(1, data.playerLevel);
        data.playerExp += exp;
        bool leveled = false;
        int guard = 0;
        while (data.playerLevel < MaxLevel && (capLevel <= 0 || data.playerLevel < capLevel) && data.playerExp >= GetExpForLevel(data.playerLevel) && guard++ < 64)
        {
            data.playerExp -= GetExpForLevel(data.playerLevel);   // 本级门槛扣掉，余量结转下一级
            data.playerLevel++;
            leveled = true;
        }

        // 满级后溢出经验 → 金币（2026-10-30 主人拍板：满级后经验转金币，避免属性溢出）
        if (data.playerLevel >= MaxLevel && data.playerExp > 0)
        {
            int overflowGold = data.playerExp * ExpOverflowGoldPerExp;
            data.playerExp = 0;
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, overflowGold, save: false, notify: true);
            SaveSystem.Instance?.Save();   // 溢出金币要落盘（即便未升级）
        }

        if (!leveled)
        {
            if (capLevel > 0) SaveSystem.Instance?.Save();   // 被每关升级额度截留的经验也要落盘，别只留内存
            return;
        }

        // 立刻落盘：升级是关键进度，不靠下一笔存档兜
        SaveSystem.Instance?.Save();

        // 【2026-11-05 主人拍板】升级联动天赋：右列门槛（=人物等级）刚跨过 → toast + 红点 + 天赋页开通演出
        TalentSystem.NotifyLevelUp(before, Mathf.Max(1, data.playerLevel));
        LevelUpPopup.QueueForTown(before, Mathf.Max(1, data.playerLevel));
        LevelUpPopup.FlushPending();   // 城镇已就绪（任务栏轮询只在城镇跑），立即弹，不挂起到下次回城
        return;
    }

    /// <summary>
    /// 在战斗/结算上下文加经验（2026-10-30 新增）：不立即弹升级窗，挂起到回城由 Town 触发，
    /// 避免结算期误弹。满级溢出同样转金币。供 ChapterManager 发「每日通关任务」经验等结算期调用。
    /// 2026-11-12 起无调用方（每日通关任务经验改走 DailyTaskSystem.Settle → AddDirectExp），保留备用。
    /// </summary>
    public static void AddExpForTown(int exp)
    {
        if (exp <= 0) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[LevelSystem] AddExpForTown 失败：SaveData 为空，经验不发（不兜底）");
            return;
        }
        int before = Mathf.Max(1, data.playerLevel);
        data.playerExp += exp;
        bool leveled = false;
        int guard = 0;
        while (data.playerLevel < MaxLevel && data.playerExp >= GetExpForLevel(data.playerLevel) && guard++ < 64)
        {
            data.playerExp -= GetExpForLevel(data.playerLevel);
            data.playerLevel++;
            leveled = true;
        }
        if (data.playerLevel >= MaxLevel && data.playerExp > 0)
        {
            int overflowGold = data.playerExp * ExpOverflowGoldPerExp;
            data.playerExp = 0;
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, overflowGold, save: false, notify: true);
            SaveSystem.Instance?.Save();
        }
        SaveSystem.Instance?.Save();
        if (leveled)
        {
            LevelUpPopup.QueueForTown(before, Mathf.Max(1, data.playerLevel));
            // 【2026-11-05 主人拍板】升级联动天赋：右列门槛（=人物等级）刚跨过 → toast + 红点 + 天赋页开通演出
            TalentSystem.NotifyLevelUp(before, Mathf.Max(1, data.playerLevel));
        }
        return;
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
