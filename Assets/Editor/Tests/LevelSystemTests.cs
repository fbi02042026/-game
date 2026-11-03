using NUnit.Framework;

/// <summary>
/// LevelSystem 纯逻辑单测（EditMode，只测无单例依赖的部分）。
/// 锚定拍板口径：
///   - 经验曲线 GetExpForLevel = 50 + level × 20（与 HiddenLevelSystem 同源；通关经验 = 35 + 章递增，另算）
///   - 满级 40 封顶（2026-10-30「避免属性溢出」）、满级溢出经验 1:5 转金币
///   - 职业倾向映射（2026-10-25 拍板）：狂战=力量、法师/牧师=智力、游侠=敏捷、剑盾/重装=体质
///   - 倾向展示名唯一出口（弹窗第二行）：生命/攻速/魔攻/力量，不报四维源属性名
/// 升级落账（AddStageClearExp/AddDirectExp）依赖 SaveSystem/BattleManager 实例，归 PlayMode/人工验收（测试文档 §4.7 Q6/Q7）。
/// </summary>
public class LevelSystemTests
{
    [Test]
    public void ExpCurve_Formula()
    {
        Assert.AreEqual(70f, LevelSystem.GetExpForLevel(1), "1 级门槛 = 50 + 1×20");
        Assert.AreEqual(250f, LevelSystem.GetExpForLevel(10), "10 级门槛 = 50 + 10×20");
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
        Assert.AreEqual(40, LevelSystem.MaxLevel, "满级 40（2026-10-30 拍板封顶）");
        Assert.AreEqual(5, LevelSystem.ExpOverflowGoldPerExp, "满级后 1 点溢出经验 = 5 金币（2026-10-30 拍板）");
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
