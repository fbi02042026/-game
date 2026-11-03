using NUnit.Framework;
using UnityEngine;

/// <summary>
/// DamageFormula 纯逻辑单测（EditMode，不开游戏、无随机依赖）。
/// 覆盖 2026-09-27~10-24 各次拍板的公式口径：
///   - 乘法减伤 K=100、减伤率封顶 0.75、总减伤封顶 0.85
///   - 物理/魔法两条防御彻底分开
///   - 类型克制双向 ×1.1
///   - 魔法普攻吃 (1+MagicPower) 乘区（T1）
///   - 伤害下限 1
/// 断言里的期望值全部来自公式常量推演，改常量会在这里红——这是故意的：
/// 提醒改数的人「拍板过的口径动了，要过一遍测试再提交」。
/// </summary>
public class DamageFormulaTests
{
    static AttrSystem NewAttr()
    {
        // Unspecified：不读职业表（避免 Resources.Load），基底全走 GameConfig 常量，EditMode 安全。
        var a = new AttrSystem(AttrOwnerKind.Unspecified);
        a.SetAttr(AttrType.CritRate, 0f); // 关掉暴击随机，保证用例确定性
        return a;
    }

    // ---------- 下限与空值 ----------

    [Test]
    public void BuildAttackRaw_NullAttacker_ReturnsMinDamage()
    {
        bool isCrit;
        Assert.AreEqual(DamageFormula.MinDamage, DamageFormula.BuildAttackRaw(null, out isCrit), 0.001f);
        Assert.IsFalse(isCrit);
    }

    [Test]
    public void FinalHit_TinyRaw_ClampedToMinDamage()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 0f);
        Assert.AreEqual(1f, DamageFormula.FinalHit(0.1f, def), 0.001f);
    }

    [Test]
    public void FinalHit_IgnoreDefense_PassesRawThrough()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 999f);
        Assert.AreEqual(100f, DamageFormula.FinalHit(100f, def, ignoreDefense: true), 0.001f);
    }

    // ---------- 乘法减伤（K=100） ----------

    [Test]
    public void FinalHit_Defense100_HalvesPhysicalDamage()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 100f);
        // defRed = 100/(100+100) = 0.5，无装备减伤 → 100 * 0.5 = 50
        Assert.AreEqual(50f, DamageFormula.FinalHit(100f, def), 0.001f);
    }

    [Test]
    public void FinalHit_PhysicalAndMagicDefense_AreIndependent()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 100f);   // 物防 100
        def.SetAttr(AttrType.MagicDefense, 0f); // 魔防 0 = 全额吃

        float physical = DamageFormula.FinalHit(100f, def, isMagic: false);
        float magic = DamageFormula.FinalHit(100f, def, isMagic: true, targetIsMagic: true);

        Assert.AreEqual(50f, physical, 0.001f, "物理伤害应吃 0.5 物防减伤");
        Assert.AreEqual(100f, magic, 0.001f, "魔防 0 时魔法伤害应全额（不拿物防换算——2026-09-27 拍板）");
    }

    // ---------- 双层封顶（0.75 / 0.85） ----------

    [Test]
    public void FinalHit_HugeDefenseAndGear_CapsAt85Percent()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 10000f);     // 裸减伤率 0.99 → 封顶 0.75
        def.SetAttr(AttrType.DamageReduce, 0.5f);  // 装备减伤 50%，乘叠后 0.875 → 封顶 0.85
        // 100 * (1 - 0.85) = 15：至少 15% 必打进去（2026-10-21「要封顶」拍板）
        Assert.AreEqual(15f, DamageFormula.FinalHit(100f, def), 0.001f);
    }

    [Test]
    public void FinalHit_DefenseAlone_CapsAt75Percent()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 100000f); // 0.999 → 封顶 0.75
        Assert.AreEqual(25f, DamageFormula.FinalHit(100f, def), 0.001f);
    }

    // ---------- 类型克制（双向 ×1.1） ----------

    [Test]
    public void FinalHit_CrossType_BonusApplies_AfterReduction()
    {
        var def = NewAttr();
        def.SetAttr(AttrType.Defense, 0f);      // 物理侧也归零：AttrSystem 构造基底自带 BASE_DEFENSE
        def.SetAttr(AttrType.MagicDefense, 0f);
        // 魔攻打物理目标：0 减伤 → 100 × 1.1 = 110（2026-10-24 双向克制拍板）
        Assert.AreEqual(110f, DamageFormula.FinalHit(100f, def, isMagic: true, targetIsMagic: false), 0.001f);
        // 反向同理：物攻打魔法目标
        Assert.AreEqual(110f, DamageFormula.FinalHit(100f, def, isMagic: false, targetIsMagic: true), 0.001f);
        // 同型对轰无加成
        Assert.AreEqual(100f, DamageFormula.FinalHit(100f, def, isMagic: true, targetIsMagic: true), 0.001f);
    }

    // ---------- 攻击/技能基底 ----------

    [Test]
    public void BuildAttackRaw_Physical_EqualsAttack()
    {
        var atk = NewAttr();
        atk.SetAttr(AttrType.Attack, 42f);
        bool isCrit;
        Assert.AreEqual(42f, DamageFormula.BuildAttackRaw(atk, out isCrit), 0.001f);
        Assert.IsFalse(isCrit, "暴击率 0 时不应暴击");
    }

    [Test]
    public void BuildAttackRaw_Magic_TakesMagicAttackTimesMagicPower()
    {
        var atk = NewAttr();
        atk.SetAttr(AttrType.Attack, 100f);          // 干扰项：物理攻击不应被用到
        atk.SetAttr(AttrType.MagicAttack, 30f);
        atk.SetAttr(AttrType.MagicPower, 0.5f);
        bool isCrit;
        // 2026-10-24 T1 拍板：魔法普攻 = MagicAttack × (1 + MagicPower) = 30 × 1.5 = 45
        Assert.AreEqual(45f, DamageFormula.BuildAttackRaw(atk, out isCrit, magicAttack: true), 0.001f);
        Assert.IsFalse(isCrit);
    }

    [Test]
    public void BuildSkillBase_Physical_Formula()
    {
        var atk = NewAttr();
        atk.SetAttr(AttrType.Attack, 10f);
        atk.SetAttr(AttrType.PhyPower, 0.2f);
        atk.SetAttr(AttrType.MagicPower, 0.3f);
        // base + ATK × mul × (1 + 物强 + 魔强) = 5 + 10 × 2 × 1.5 = 35
        Assert.AreEqual(35f, DamageFormula.BuildSkillBase(5f, 2f, atk), 0.001f);
    }

    [Test]
    public void BuildSkillBase_Magic_UsesMagicAttackNotAttack()
    {
        var atk = NewAttr();
        atk.SetAttr(AttrType.Attack, 100f);      // 干扰项
        atk.SetAttr(AttrType.MagicAttack, 20f);
        // 2026-09-29 拍板：魔法技能基底取 MagicAttack，塌成 baseDamage 就是回归
        // 5 + 20 × 1 × (1+0+0) = 25
        Assert.AreEqual(25f, DamageFormula.BuildSkillBase(5f, 1f, atk, magic: true), 0.001f);
    }

    // ---------- 暴击倍率 ----------

    [Test]
    public void CritMultiplier_NullAttacker_UsesDefaultPlusBonus()
    {
        float baseMul = GameConfig.DefaultCritMultiplier;
        Assert.AreEqual(baseMul, DamageFormula.CritMultiplier(null), 0.001f);
        // 加算 bonus：额外暴击伤害直接叠加（不影响基底）
        Assert.AreEqual(baseMul + 0.5f, DamageFormula.CritMultiplier(null, 0.5f), 0.001f);
        // 负 bonus 不生效（钳到 0）
        Assert.AreEqual(baseMul, DamageFormula.CritMultiplier(null, -1f), 0.001f);
    }
}
