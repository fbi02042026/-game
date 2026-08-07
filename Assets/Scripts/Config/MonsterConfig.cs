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

    [Header("精灵配置")]
    [Tooltip("在章节文件夹中的精灵编号（1-12），0表示随机")]
    public int spriteIndex = 0;
    [Tooltip("精灵缩放比例")]
    public float spriteScale = 1f;
}