using UnityEngine;

/// <summary>
/// 怪物「物理 / 魔法」类型判定（怪物只分物理或魔法，不混搭）。
///
/// 2026-09-28 主人纠正（推翻 2026-09-26 的「所有怪都掷骰」做法）：
///   🔴 **普通小怪不掷骰**，表里写什么就是什么——近战就是近战，法师才是远程。
///      之前所有怪都按 magicChance 掷骰，导致美术上明明是近战的小蘑菇跑去丢魔法球。
///   ✅ 普通小怪：魔法型 = 表里 style 就是法师（Ranged）。Bow = 物理远程（丢物理子弹，
///      黄色小球那套），Melee = 纯近战，两者都不掷骰、都不发飞行物/法球。
///   ✅ 精英 / Boss：**只有它们**才有几率遇到不同攻击方式的单位，按 magicChance 掷骰
///      （每次生成都重掷 → 反复刷同章 Boss 也会变化）。
///     - magicChance=0   → 必定物理
///     - magicChance=1   → 必定魔法
///     - magicChance=0.5 → 一半概率是魔法型
///
/// 数据来源：Assets/Data/Source/Tables/monster_attack_style.csv（style / magicChance 列），
///   运行时读 Resources/Data/Tables/monster_attack_style.bytes（由 csv Cook 而来），
///   改 csv 后必须重跑 Tools/Data/Cook Tables 才会生效，光保存 csv 不够。
///
/// ⚠ 关键约定：掷骰内部用 Random.value，**只在怪物 Init 时调用一次**，
///   结果由 Monster._isMagicType 缓存，终身固定；绝不每帧重掷（避免属性反复横跳）。
///   代码里没有任何硬编码的怪物 id 名单——判定完全由表的 style / magicChance 驱动。
///
/// 魔法型判定出来后：Monster 会强制把 _attackStyle 改 Ranged（法球弹道 + 魔法伤害），
/// 物理型保持表内 style（Melee/Bow）。详情见 Monster.cs Init 处注释。
/// </summary>
public static class MonsterAttackTypeResolver
{
    /// <summary>
    /// 【普通小怪专用】不掷骰：魔法型 = 表里 style 就是法师（Ranged）。
    /// 2026-09-28 主人纠正：近战小怪绝不能变成丢魔法球的法师，只有表里写明法师的才是法师。
    /// </summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex)
    {
        return MonsterAttackStyleTable.Get(monsterChapter, spriteIndex) == MonsterAttackStyle.Ranged;
    }

    /// <summary>
    /// 【精英 / Boss 专用】它们才有几率遇到不同攻击方式的单位，按 magicChance 掷骰。
    /// 2026-09-26 主人拍板：刷怪配置整合——数量/波数/魔法占比都放 stage_spawn.csv。
    /// 优先用 StageSpawnTable 本关配的 magicChance（含 Boss 0.5）；本关没配才回退单怪级
    /// （monster_attack_style.csv 的 magicChance）。
    /// stageIndex 传 -1 表示"按章+类型取通配行"，关卡级占比本来就跟关卡号无关。
    /// 掷骰时机约束同 IsMagicMonster：只在怪物 Init 时调用一次，不每帧重掷。
    /// ⚠ 普通小怪别调这个重载，会掷骰 —— 走两参数的 IsMagicMonster(chapter, spriteIndex)。
    /// </summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex, int gameChapter, StageType stageType)
    {
        if (StageSpawnTable.TryResolve(gameChapter, -1, stageType, out var rule) && rule.magicChance >= 0f)
            return Random.value < rule.magicChance;
        float chance = MonsterAttackStyleTable.GetMagicChance(monsterChapter, spriteIndex);
        return Random.value < chance;
    }
}
