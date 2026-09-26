using UnityEngine;

/// <summary>
/// 怪物「物理 / 魔法」类型判定（主人 2026-09-26 拍板：怪物只分物理或魔法，不混搭）。
///
/// 2026-09-26 主人拍板（推翻上一轮固定名单做法）：
///   物理/魔法怪**按概率随机参杂**，每只怪生成时按 monster_attack_style.csv 的
///   magicChance 列掷一次骰子决定，而非写死名单。
///     - magicChance=0   → 必定物理
///     - magicChance=1   → 必定魔法
///     - magicChance=0.33 → 约 1/3 概率是魔法型
///   Boss 同样走此掷骰（见下），每次生成都重掷 → 反复刷同章 Boss 也会变化。
///
/// 数据来源：Assets/Data/Source/Tables/monster_attack_style.csv（magicChance 列），
///   运行时读 Resources/Data/Tables/monster_attack_style.bytes（由 csv Cook 而来），
///   改 csv 后必须重跑 Tools/Data/Cook Tables 才会生效，光保存 csv 不够。
///
/// ⚠ 关键约定：本方法内部用 Random.value 掷骰，**只在怪物 Init 时调用一次**，
///   结果由 Monster._isMagicType 缓存，终身固定；绝不每帧重掷（避免属性反复横跳）。
///   代码里没有任何硬编码的怪物 id 名单——判定完全由表的 magicChance 驱动。
///
/// 魔法型判定出来后：Monster 会强制把 _attackStyle 改 Ranged（法球弹道 + 魔法伤害），
/// 物理型保持表内 style（Melee/Bow）。详情见 Monster.cs Init 处注释。
/// </summary>
public static class MonsterAttackTypeResolver
{
    /// <summary>
    /// 该怪物是否为魔法型单位。内部按表内 magicChance 掷一次 Random.value 决定。
    /// 必须在怪物初始化时调用一次，结果由调用方（Monster）缓存；不要每帧调用。
    /// </summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex)
    {
        float chance = MonsterAttackStyleTable.GetMagicChance(monsterChapter, spriteIndex);
        return Random.value < chance;
    }

    /// <summary>
    /// 关卡级重载（2026-09-26 主人拍板：刷怪配置整合——数量/波数/魔法占比都放 stage_spawn.csv）。
    /// 优先用 StageSpawnTable 本关配的 magicChance（含 Boss 0.5）；本关没配才回退单怪级
    /// （monster_attack_style.csv 的 magicChance，按怪逐个掷骰）。
    /// stageIndex 传 -1 表示"按章+类型取通配行"，关卡级占比本来就跟关卡号无关。
    /// 掷骰时机约束同 IsMagicMonster：只在怪物 Init 时调用一次，不每帧重掷。
    /// </summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex, int gameChapter, StageType stageType)
    {
        if (StageSpawnTable.TryResolve(gameChapter, -1, stageType, out var rule) && rule.magicChance >= 0f)
            return Random.value < rule.magicChance;
        float chance = MonsterAttackStyleTable.GetMagicChance(monsterChapter, spriteIndex);
        return Random.value < chance;
    }
}
