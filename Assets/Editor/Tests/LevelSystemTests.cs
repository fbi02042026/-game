using NUnit.Framework;

/// <summary>
/// LevelSystem 纯逻辑单测（EditMode，只测无单例依赖的部分）。
/// 锚定拍板口径（【2026-11-23 满级 50】+【2026-11-25 抬高前期门槛】，曲线以实现为准）：
///   - 经验曲线 GetExpForLevel = 150 + 30×level + level²/2（1 级门槛 180、49→50 为 2,820；升到 50 级累计 64,300）
///   - 满级 50 封顶、满级溢出经验 1:5 转金币
///   - 通关经验 = 普通 57 + 2×(章-1)，与隐藏等级经验已解耦（隐藏账另见 HiddenLevelSystem）
///   - 职业倾向映射（2026-10-25 拍板）：狂战=力量、法师/牧师=智力、游侠=敏捷、剑盾/重装=体质
///   - 倾向展示名唯一出口（弹窗第二行）：生命/攻速/魔攻/力量，不报四维源属性名
/// 升级落账（AddStageClearExp/AddDirectExp）依赖 SaveSystem/BattleManager 实例，归 PlayMode/人工验收（测试文档 §4.7 Q6/Q7）。
/// </summary>
public class LevelSystemTests
{
    [Test]
    public void ExpCurve_Formula()
    {
        Assert.AreEqual(180, LevelSystem.GetExpForLevel(1), "1 级门槛 = 150 + 30×1 + 1²/2 = 180");
        Assert.AreEqual(500, LevelSystem.GetExpForLevel(10), "10 级门槛 = 150 + 30×10 + 10²/2 = 500");
    }

    [Test]
    public void ExpCurve_StrictlyIncreasing_UpToMaxLevel()
    {
        for (int level = 1; level < LevelSystem.MaxLevel; level++)
            Assert.Greater(LevelSystem.GetExpForLevel(level + 1), LevelSystem.GetExpForLevel(level),
                $"经验曲线应在 {level}→{level + 1} 严格递增");
    }

    [Test]
    public void Caps_Pinned()
    {
        Assert.AreEqual(50, LevelSystem.MaxLevel, "满级 50（2026-11-23 拍板）");
        Assert.AreEqual(5, LevelSystem.ExpOverflowGoldPerExp, "满级后 1 点溢出经验 = 5 金币（2026-10-30 拍板）");
    }

    /// <summary>
    /// 数值锚点（2026-11-23/11-25 口径）：经验曲线与关卡经验供给是配套的，单改一处会静默漂移。
    /// 累计升到 50 级 = 64,300；普通全清 80 关（路线 B：8 章 × 10 关，57+2×(章-1)）供给 5,120 → 只到 14 级，
    /// 差 59,180 到满级（靠日常/难度档/裂隙补，主线不再是满级主来源）。
    /// </summary>
    [Test]
    public void ExpCurve_TotalAndSupply_Anchored()
    {
        int cumToMax = 0;
        for (int level = 1; level < LevelSystem.MaxLevel; level++)
            cumToMax += LevelSystem.GetExpForLevel(level);
        Assert.AreEqual(64300, cumToMax, "升到 50 级累计经验 = 64,300（2026-11-25 拍板口径）");

        int stages = 0;
        int supply = 0;
        for (int act = 1; act <= ActProgressionDefs.ActCount; act++)
        {
            int subStages = ActProgressionDefs.StageCountOf(act);
            int perStage = GameConfig.PLAYER_EXP_STAGE_CLEAR
                + (act - 1) * GameConfig.PLAYER_EXP_STAGE_CLEAR_PER_CHAPTER;
            stages += subStages;
            supply += subStages * perStage;
        }
        Assert.AreEqual(80, stages, "普通难度全清关数 = 80（路线 B：8 章 × 1 座大关 × 10 关）");
        Assert.AreEqual(5120, supply, "普通难度全清总经验供给 = 5,120（57+2×(章-1)，2026-11-23 拍板）");

        // 用同一条曲线消化这笔供给：应停在 14 级
        int levelReached = 1;
        int expInLevel = supply;
        while (levelReached < LevelSystem.MaxLevel
               && expInLevel >= LevelSystem.GetExpForLevel(levelReached))
        {
            expInLevel -= LevelSystem.GetExpForLevel(levelReached);
            levelReached++;
        }
        Assert.AreEqual(14, levelReached, "普通全清供给 5,120 只能到 14 级");
        Assert.AreEqual(34, expInLevel, "14 级余量 = 34");
        Assert.AreEqual(59180, cumToMax - supply, "普通全清后距满级还差 59,180 经验（靠日常/难度档/裂隙补）");
    }

    [Test]
    public void JobTendencyAttr_SixJobs_Mapped()
    {
        Assert.AreEqual(AttrType.Strength, LevelSystem.JobTendencyAttr(PlayerJobId.Berserker), "狂战=力量");
        Assert.AreEqual(AttrType.Intelligence, LevelSystem.JobTendencyAttr(PlayerJobId.Mage), "法师=智力");
        Assert.AreEqual(AttrType.Intelligence, LevelSystem.JobTendencyAttr(PlayerJobId.Priest), "牧师共智力（魔法系共源）");
        Assert.AreEqual(AttrType.Agility, LevelSystem.JobTendencyAttr(PlayerJobId.Ranger), "游侠=敏捷");
        Assert.AreEqual(AttrType.Vitality, LevelSystem.JobTendencyAttr(PlayerJobId.SwordShield), "剑盾=体质");
        Assert.AreEqual(AttrType.Vitality, LevelSystem.JobTendencyAttr(PlayerJobId.Heavy), "重装共体质（坦克系共源）");
    }

    [Test]
    public void JobTendencyAttr_UnknownJob_FailsClosedToVitality()
    {
        // 非法值不崩，落体质（fail-closed 默认项）
        Assert.AreEqual(AttrType.Vitality, LevelSystem.JobTendencyAttr((PlayerJobId)999));
    }

    [Test]
    public void JobTendencyLabel_PlayerFacingNames()
    {
        // 2026-10-25 拍板：弹窗第二行按效果说人话（剑盾加血、游侠加攻速、法师加魔攻）
        Assert.AreEqual("生命", LevelSystem.JobTendencyLabel(PlayerJobId.SwordShield));
        Assert.AreEqual("生命", LevelSystem.JobTendencyLabel(PlayerJobId.Heavy));
        Assert.AreEqual("攻速", LevelSystem.JobTendencyLabel(PlayerJobId.Ranger));
        Assert.AreEqual("魔攻", LevelSystem.JobTendencyLabel(PlayerJobId.Mage));
        Assert.AreEqual("魔攻", LevelSystem.JobTendencyLabel(PlayerJobId.Priest));
        Assert.AreEqual("力量", LevelSystem.JobTendencyLabel(PlayerJobId.Berserker));
    }

    [Test]
    public void ExtraVitalityPerLevel_OnlyMageAndRanger()
    {
        // 2026-11-13 主人拍板「让法师和弓手每级也加血量」：每级 +1 体质 = +10 血 +1 防
        Assert.AreEqual(1, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.Mage), "法师每级加血");
        Assert.AreEqual(1, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.Ranger), "游侠每级加血");
        Assert.AreEqual(0, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.SwordShield), "剑盾倾向已是体质，不重复加");
        Assert.AreEqual(0, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.Heavy), "重武倾向已是体质，不重复加");
        Assert.AreEqual(0, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.Berserker), "狂战本次没提");
        Assert.AreEqual(0, LevelSystem.ExtraVitalityPerLevel(PlayerJobId.Priest), "牧师本次没提");
    }

    [Test]
    public void JobTendencyGainText_OneAttrPerLevel()
    {
        // 2026-11-13 主人拍板「每级只涨一个职业一个属性 / 就跟以前一样，只不过加了法师和弓手的血量」：
        // 弹窗文案回到单属性短格式；额外加血由 ExtraVitalityPerLevel_OnlyMageAndRanger 单独断言
        Assert.AreEqual("+2", LevelSystem.JobTendencyGainText(PlayerJobId.Mage, 1));
        Assert.AreEqual("+1%", LevelSystem.JobTendencyGainText(PlayerJobId.Ranger, 1));
        Assert.AreEqual("+10", LevelSystem.JobTendencyGainText(PlayerJobId.SwordShield, 1));
        Assert.AreEqual("+2", LevelSystem.JobTendencyGainText(PlayerJobId.Priest, 1));
        Assert.AreEqual("+1", LevelSystem.JobTendencyGainText(PlayerJobId.Berserker, 1));
        Assert.AreEqual("+4", LevelSystem.JobTendencyGainText(PlayerJobId.Mage, 2), "连升两级按 delta 缩放");
        Assert.IsFalse(LevelSystem.JobTendencyGainText(PlayerJobId.Mage, 1).Contains("血"), "加血不并进弹窗这一行");
    }
}
