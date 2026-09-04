using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主英雄类
/// </summary>
public class Hero : UnitBase
{
    public static Hero Instance;
    public Transform endPoint;
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 50;

    [Header("SPUM换装")]
    public HeroCostumeManager costumeManager;

    protected override void Awake()
    {
        // 【关键】必须在base.Awake()之前设置，因为base.Awake()中会调用EnsureHitPoint/EnsureFirePoint
        firePointOffset = new Vector3(0.3f, 0.32f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        Instance = this;
        isAlly = true;
        // SPUM 资源在 +scale.x 下实际朝左，故标记为 false，facingDir=1 时会镜像成朝右
        spriteDefaultFacesRight = false;
        // 不改 SPUM 人物 Sorting，保留预制体层级；换装走 SPUM 规范

        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        KillComboAfterimage.Ensure(this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void InitNewRun()
    {
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.InitNewRun();
        else
            Debug.LogWarning("[Hero] InitNewRun: GridBackpackSystem 为空，跳过背包重置");

        attr.ResetToBase();
        attr.RecalcAllAttr(); // 现在可以安全调用（Awake已完成）
        level = 1;
        currentExp = 0;
        expToNextLevel = LevelSystem.GetExpForLevel(level);
        currentHp = attr.GetAttr(AttrType.MaxHp);
        // 优先用场景 SpawnPoint；无则回退硬编码 X + GROUND_Y
        Vector3 spawnPos = new Vector3(-7f, GROUND_Y, 0f);
        if (BattleManager.Instance != null)
        {
            float z = BattleManager.Instance.unitRoot != null
                ? BattleManager.Instance.unitRoot.position.z
                : 0f;
            if (BattleManager.Instance.spawnPoint != null)
            {
                var sp = BattleManager.Instance.spawnPoint.position;
                spawnPos = new Vector3(sp.x, GROUND_Y, z);
            }
            else
            {
                spawnPos = new Vector3(spawnPos.x, GROUND_Y, z);
            }
        }
        GameConfig.SetWorldPosition(gameObject, spawnPos);
        GameConfig.AttachToUnitRoot(transform);
        transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;
        facingDir = 1;
        ApplyFacing(facingDir); // 默认资源朝左 → 朝右时 scale.x 为负
        gameObject.SetActive(true);

        // 重置死亡状态和动画
        ResetForReuse();

        // 初始化换装后再按武器刷新攻击距离
        if (costumeManager != null)
            costumeManager.RefreshCostume();
        RecalcAttr();

        Debug.Log($"[Hero] InitNewRun完成 | pos={transform.position} | scale={transform.localScale} | facingDir={facingDir} | range={attr.GetAttr(AttrType.AttackRange)}");
    }

    public void AddExp(int exp)
    {
        currentExp += exp;
        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelSystem.OnLevelUp(this);
            expToNextLevel = LevelSystem.GetExpForLevel(level);
        }
    }

    public void RecalcAttr()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            Debug.LogWarning("[Hero] RecalcAttr: GridBackpackSystem 为空，仅重算基础属性");
            attr.RecalcAllAttr(null);
            currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));
            return;
        }

        List<AttrBonusData> allBonus = EquipStatRollup.BuildBonusList(bag);
        if (BattleManager.Instance != null && BattleManager.Instance.tempBuffs != null)
            allBonus.AddRange(BattleManager.Instance.tempBuffs);
        attr.RecalcAllAttr(allBonus);

        // 主手优先，无主手则读副手（教程默认左手剑）
        float weaponRange = GameConfig.BASE_ATTACK_RANGE;
        EquipTemplate weaponTpl = TryGetEquippedWeaponTemplate(bag);
        if (weaponTpl != null)
            weaponRange = GameConfig.ResolveWeaponAttackRange(weaponTpl);
        attr.SetAttr(AttrType.AttackRange, weaponRange);
        if (weaponTpl != null)
        {
            float swordSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.WeaponKind.Sword);
            float kindSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.ResolveKind(weaponTpl));
            float mul = swordSpd > 0.01f ? kindSpd / swordSpd : 1f;
            attr.SetAttr(AttrType.AttackSpeed, Mathf.Max(0.2f, attr.GetAttr(AttrType.AttackSpeed) * mul));
        }
        currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));

        // 属性重算后同步外观（通关穿装 / 战前遗产等路径未必都走 EquipItem）
        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        costumeManager?.RefreshCostume();
    }

    protected override void Update()
    {
        base.Update();
        // 通关只由 BattleManager 在「传送门已激活」后检测，避免未清怪就结算、每帧刷爆 OnStageClear
    }

    /// <summary>
    /// 根据当前装备的武器返回攻击类型
    /// </summary>
    protected override WeaponAttackType GetAttackType()
    {
        if (GridBackpackSystem.Instance == null) return WeaponAttackType.Physical;
        var tpl = TryGetEquippedWeaponTemplate(GridBackpackSystem.Instance);
        if (tpl != null)
        {
            foreach (var item in GridBackpackSystem.Instance.GetEquippedItems())
            {
                if (item.template == tpl) return item.weaponAttackType;
            }
        }
        return WeaponAttackType.Physical;
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        var tpl = GridBackpackSystem.Instance != null
            ? TryGetEquippedWeaponTemplate(GridBackpackSystem.Instance)
            : null;
        return SkillNaming.KitFromWeaponKind(WeaponCombatTable.ResolveKind(tpl));
    }

    /// <summary>攻击特效以逻辑主手武器为准；无主手则不看副手剑。</summary>
    static EquipTemplate TryGetEquippedWeaponTemplate(GridBackpackSystem bag)
    {
        if (bag == null) return null;
        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        if (main?.template != null && main.weaponType != WeaponType.None)
            return main.template;
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        if (off?.template != null && off.weaponType != WeaponType.None)
            return off.template;
        return null;
    }

    /// <summary>对外：当前主手武器对应的攻击特效套（技能回退也用）。</summary>
    public AttackVfxKit GetWeaponVfxKit() => GetAttackVfxKit();

    protected override void Die(bool isCritKill = false)
    {
        base.Die(isCritKill);
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnHeroDead();
        else
            Debug.LogWarning("[Hero] Die: BattleManager 为空，跳过 OnHeroDead");
    }

    /// <summary>
    /// Hero不回对象池，死亡动画播完后直接隐藏
    /// </summary>
    protected override void OnDeathRelease()
    {
        gameObject.SetActive(false);
    }
}