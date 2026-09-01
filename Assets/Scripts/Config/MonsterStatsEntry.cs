using UnityEngine;

/// <summary>monster_stats 表一行；可转成运行时 MonsterConfig。</summary>
public class MonsterStatsEntry
{
    public string id;
    public int monsterChapter;
    public int spriteIndex;
    public string monsterName;
    public int minWave;
    public bool isBoss;
    public int unlockClearCount;
    public float baseHp;
    public float baseAttack;
    public float baseAttackSpeed;
    public float attackRange;
    public float baseMoveSpeed;
    public int baseGoldDrop;
    public int expDrop;
    public float spriteScale = 1f;

    public MonsterConfig ToRuntimeConfig()
    {
        var m = ScriptableObject.CreateInstance<MonsterConfig>();
        m.id = id ?? "";
        m.monsterName = monsterName ?? id;
        m.minWave = minWave;
        m.isBoss = isBoss;
        m.unlockClearCount = unlockClearCount;
        m.baseHp = baseHp;
        m.baseAttack = baseAttack;
        m.baseAttackSpeed = baseAttackSpeed;
        m.attackRange = attackRange;
        m.baseMoveSpeed = baseMoveSpeed;
        m.baseGoldDrop = baseGoldDrop;
        m.expDrop = expDrop;
        m.spriteIndex = spriteIndex;
        m.spriteScale = spriteScale > 0.01f ? spriteScale : 1f;
        return m;
    }
}
