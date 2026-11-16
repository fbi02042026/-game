using UnityEngine;

/// <summary>
/// 怪物「物理 / 魔法」类型判定（怪物只分物理或魔法，不混搭）。
///
/// 🔴 **2026-11-16 主人拍板「就按数据表的设定来，不要那个随机远程近战的设定了」**：
///   掷骰**全部停用**，精英/Boss 与普通小怪同一条判定 ——
///   魔法型 = 表里 style 就是法师（Ranged）；Melee = 纯近战、Bow = 物理远程，两者恒物理。
///   `monster_attack_style.csv` 的 magicChance 列与 `stage_spawn.csv` 的关卡级 magicChance
///   **不再参与任何判定**（列与数据都保留，以后要恢复随机只改本类一处）。
///   效果：同一只怪每次生成都是同一种攻击方式，玩家能靠图鉴/外观预判远近。
///
/// 历史（已被上面覆盖，留档）：
///   2026-09-28 主人纠正「普通小怪不掷骰，近战就是近战、法师才是远程」；
///   当时只把掷骰限制在精英/Boss，11-16 进一步整体取消。
///
/// 数据来源：Assets/Data/Source/Tables/monster_attack_style.csv（style 列），
///   运行时读 Resources/Data/Tables/monster_attack_style.bytes（由 csv Cook 而来），
///   改 csv 后必须同步 .bytes（Tools/sync_table_bytes.py）才会生效。
///
/// 魔法型判定出来后：Monster 会强制把 _attackStyle 保持一致（Ranged 本就是法球），
/// 物理型保持表内 style（Melee/Bow）。详情见 Monster.Init 处注释。
/// </summary>
public static class MonsterAttackTypeResolver
{
    /// <summary>魔法型 = 表里 style 就是法师（Ranged）；Melee/Bow 恒物理。2026-11-16 起不再掷骰。</summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex)
    {
        return MonsterAttackStyleTable.Get(monsterChapter, spriteIndex) == MonsterAttackStyle.Ranged;
    }


    /// <summary>
    /// 【兼容旧调用点】2026-11-16 起与两参版本完全一致：不再掷骰，gameChapter / stageType 不参与判定。
    /// 保留签名只为不惊动既有调用点（Monster.Init 现在也直接走两参版本）。
    /// </summary>
    public static bool IsMagicMonster(int monsterChapter, int spriteIndex, int gameChapter, StageType stageType)
    {
        // 【2026-11-16 主人拍板「就按数据表的设定来，不要那个随机远程近战的设定了」】
        // 掷骰整体停用：精英/Boss 与普通小怪同口径 —— 魔法型 = 表里 style 就是法师(Ranged)，
        // 近战(Melee)/弓箭(Bow) 一律物理，不再有「同一只 Boss 这次法系、下次物理」。
        // 参数 gameChapter / stageType 保留只为兼容既有调用点，不再参与判定。
        return IsMagicMonster(monsterChapter, spriteIndex);
    }
}
