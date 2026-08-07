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
        firePointOffset = new Vector3(0.3f, 0.45f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        Instance = this;
        isAlly = true;
        // SPUM 资源在 +scale.x 下实际朝左，故标记为 false，facingDir=1 时会镜像成朝右
        spriteDefaultFacesRight = false;
        // 不改 SPUM 人物 Sorting，保留预制体层级；换装走 SPUM 规范

        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
    }

    public void InitNewRun()
    {
        GridBackpackSystem.Instance.InitNewRun();
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
        List<AttrBonusData> allBonus = GridBackpackSystem.Instance.GetAllEquippedBonus();
        foreach (var equip in GridBackpackSystem.Instance.GetEquippedItems())
        {
            foreach (var enchant in equip.enchants)
            {
                allBonus.Add(new AttrBonusData
                {
                    attrType = enchant.attrType,
                    value = enchant.value,
                    isPercent = enchant.isPercent
                });
            }
        }
        allBonus.AddRange(BattleManager.Instance.tempBuffs);
        attr.RecalcAllAttr(allBonus);

        // 根据装备武器设置攻击范围（数值表：攻击范围(像素) / 100）
        float weaponRange = GameConfig.BASE_ATTACK_RANGE;
        foreach (var item in GridBackpackSystem.Instance.GetEquippedItems())
        {
            if (item.slotType == EquipSlotType.MainHand && item.template != null)
            {
                weaponRange = GameConfig.NormalizeAttackRange(item.template.attackRange);
                break;
            }
        }
        attr.SetAttr(AttrType.AttackRange, weaponRange);
        currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));
    }

    protected override void Update()
    {
        base.Update();
        if (!isDead && target == null && transform.position.x >= endPoint.position.x)
        {
            BattleManager.Instance.OnStageClear();
        }
    }

    /// <summary>
    /// 根据当前装备的武器返回攻击类型
    /// </summary>
    protected override WeaponAttackType GetAttackType()
    {
        if (GridBackpackSystem.Instance == null) return WeaponAttackType.Physical;
        foreach (var item in GridBackpackSystem.Instance.GetEquippedItems())
        {
            if (item.slotType == EquipSlotType.MainHand)
                return item.weaponAttackType;
        }
        return WeaponAttackType.Physical;
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        WeaponAttackType t = GetAttackType();
        if (t == WeaponAttackType.Magic) return AttackVfxKit.Orb;

        if (GridBackpackSystem.Instance != null)
        {
            foreach (var item in GridBackpackSystem.Instance.GetEquippedItems())
            {
                if (item.slotType != EquipSlotType.MainHand) continue;
                string n = item.equipName ?? "";
                if (item.template != null && !string.IsNullOrEmpty(item.template.spumName))
                    n += item.template.spumName;
                n = n.ToLower();
                if (n.Contains("bow") || n.Contains("arrow") || n.Contains("弓"))
                    return AttackVfxKit.Bow;
            }
        }
        float range = attr != null ? attr.GetAttr(AttrType.AttackRange) : 1.5f;
        if (range >= GameConfig.RangeBow - 0.05f) return AttackVfxKit.Bow;
        return AttackVfxKit.MeleeSlash;
    }

    protected override void Die()
    {
        base.Die();
        BattleManager.Instance.OnHeroDead();
    }

    /// <summary>
    /// Hero不回对象池，死亡动画播完后直接隐藏
    /// </summary>
    protected override void OnDeathRelease()
    {
        gameObject.SetActive(false);
    }
}