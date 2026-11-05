using NUnit.Framework;

/// <summary>
/// LevelSystem 纯逻辑单测（EditMode，只测无单例依赖的部分）。
/// 锚定拍板口径（【2026-11-05 拍板】满级 80，曲线以实现为准）：
///   - 经验曲线 GetExpForLevel = 60 + 30×level + level²/2（线性+二次项；1 级门槛 90、79→80 为 5,550）
///   - 满级 80 封顶、满级溢出经验 1:5 转金币
///   - 通关经验 = 普通 110 + 32×(章-1)，与隐藏等级经验已解耦（隐藏账另见 HiddenLevelSystem）
///   - 职业倾向映射（2026-10-25 拍板）：狂战=力量、法师/牧师=智力、游侠=敏捷、剑盾/重装=体质
///   - 倾向展示名唯一出口（弹窗第二行）：生命/攻速/魔攻/力量，不报四维源属性名
/// 升级落账（AddStageClearExp/AddDirectExp）依赖 SaveSystem/BattleManager 实例，归 PlayMode/人工验收（测试文档 §4.7 Q6/Q7）。
/// </summary>
public class LevelSystemTests
{
    [Test]
    public void ExpCurve_Formula()
    {
        Assert.AreEqual(90, LevelSystem.GetExpForLevel(1), "1 级门槛 = 60 + 30×1 + 1²/2 = 90");
        Assert.AreEqual(410, LevelSystem.GetExpForLevel(10), "10 级门槛 = 60 + 30×10 + 10²/2 = 410");
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
        Assert.AreEqual(80, LevelSystem.MaxLevel, "满级 80（2026-11-05 拍板）");
        Assert.AreEqual(5, LevelSystem.ExpOverflowGoldPerExp, "满级后 1 点溢出经验 = 5 金币（2026-10-30 拍板）");
    }

    /// <summary>
    /// 数值锚点（2026-11-05 口径）：经验曲线与关卡经验供给是配套的，单改一处会静默漂移。
    /// 累计 1→80 = 183,260；普通全清 730 小关供给 169,260 → 只到 77 级，差 14,000 到满级。
    /// </summary>
    [Test]
    public void ExpCurve_TotalAndSupply_Anchored()
    {
        int cumToMax = 0;
        for (int level = 1; level < LevelSystem.MaxLevel; level++)
            cumToMax += LevelSystem.GetExpForLevel(level);
        Assert.AreEqual(183260, cumToMax, "1→80 级累计经验 = 183,260（2026-11-05 拍板口径）");

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
        Assert.AreEqual(730, stages, "普通难度全清小关数 = 730（8 章 × 10 关 × 5/8/10…节）");
        Assert.AreEqual(169260, supply, "普通难度全清总经验供给 = 169,260");

        // 用同一条曲线消化这笔供给：应停在 77 级
        int levelReached = 1;
        int expInLevel = supply;
        while (levelReached < LevelSystem.MaxLevel
               && expInLevel >= LevelSystem.GetExpForLevel(levelReached))
        {
            expInLevel -= LevelSystem.GetExpForLevel(levelReached);
            levelReached++;
        }
        Assert.AreEqual(77, levelReached, "普通全清供给 169,260 只能到 77 级");
        Assert.AreEqual(2326, expInLevel, "77 级余量 = 2,326");
        Assert.AreEqual(14000, cumToMax - supply, "普通全清后距满级还差 14,000 经验（靠日常/难度档补）");
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
}
