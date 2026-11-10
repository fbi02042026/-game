using UnityEngine;

/// <summary>
/// 怪物配置，ScriptableObject可视化配置
/// </summary>
[CreateAssetMenu(fileName = "MonsterConfig", menuName = "Config/Monster")]
public class MonsterConfig : ScriptableObject
{
    public string id;
    public string monsterName;
    public int minWave;
    public bool isBoss;

    [Header("渐进式解锁")]
    [Tooltip("需要该章节通关多少次后才解锁此怪物（0=首次即可出现）")]
    public int unlockClearCount = 0;

    [Header("基础属性")]
    public float baseHp;
    public float baseAttack;
    public float baseAttackSpeed;
    public float attackRange;
    public float baseMoveSpeed;
    public int baseGoldDrop;
    public int expDrop;

    [Header("魔法侧（2026-09-26）")]
    [Tooltip("魔法攻击。0 = 等比沿用 baseAttack（GameConfig.MONSTER_MAGIC_ATK_RATIO）")]
    public float baseMagicAttack;
    [Tooltip("魔法防御。0 = 等比沿用物理防御 baseDef（GameConfig.MONSTER_MAGIC_DEF_RATIO）")]
    public float baseMagicDefense;

    [Header("精灵配置")]
    [Tooltip("在章节文件夹中的精灵编号（1-12），0表示随机")]
    public int spriteIndex = 0;
    [Tooltip("精灵缩放比例")]
    public float spriteScale = 1f;

    [Header("词缀怪（2026-10-26）")]
    [Tooltip("表定词缀：monster_stats.affix 列（| 分隔的 MonsterAffixId 名，如 Bulwark|Thorns）。空 = 普通怪。仅正式主线关对非精英小怪生效；精英/Boss/签名怪仍走掷骰词缀。")]
    public string tableAffix = "";

    [Header("站位与攻击形态（2026-11-10 主人名单）")]
    [Tooltip("空中单位：monster_stats.airUnit 列。1 = 战斗里身体上抬 MONSTER_AIR_HOVER、脚下阴影留在地面（留一段可见空隙）；0 = 陆地贴地。")]
    public bool airUnit;
    [Tooltip("远程单位：monster_stats.ranged 列。1 = 远程、0 = 近战。当前仅表意记录，攻击逻辑仍走 attackRange 列。")]
    public bool ranged;
}