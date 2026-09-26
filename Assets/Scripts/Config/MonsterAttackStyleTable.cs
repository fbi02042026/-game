using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物近战/远程表：读 Resources/Data/Tables/monster_attack_style.bytes（由 csv Cook 而来）。
/// style = Melee | Ranged（法球） | Bow（弓箭），决定弹道视觉与射程；
/// magicChance（第 4 列，0~1）= 该怪被判为魔法型的概率，供 MonsterAttackTypeResolver 掷骰用。
/// 2026-09-26 主人拍板：物理/魔法按 magicChance 随机，不写死名单。
/// </summary>
public enum MonsterAttackStyle
{
    Melee = 0,
    Ranged = 1,
    Bow = 2
}

public static class MonsterAttackStyleTable
{
    static Dictionary<int, MonsterAttackStyle> _map;
    // 2026-09-26 主人拍板：物理/魔法随机参杂——每只怪被判为魔法型的概率（缺配回退 0.35）。
    // 补声明：上一轮只用了这个字段却漏了声明，导致 CS0103。
    static Dictionary<int, float> _chance;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _map = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _map = new Dictionary<int, MonsterAttackStyle>();
        _chance = new Dictionary<int, float>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.MonsterAttackStyle);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MonsterAttackStyle] 未找到攻击方式表，默认近战/远程兜底");
            return;
        }

        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int ok = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("monsterChapter"))
                continue;

            string[] cols = line.Split(',');
            if (cols.Length < 3) continue;
            if (!int.TryParse(cols[0].Trim(), out int ch)) continue;
            if (!int.TryParse(cols[1].Trim(), out int idx)) continue;

            _map[Key(ch, idx)] = ParseStyle(cols[2].Trim());
            // 第 4 列 magicChance（可选）：缺列/解析失败则留空，运行时回退默认 0.35
            if (cols.Length > 3 && float.TryParse(cols[3].Trim(), out float mc))
                _chance[Key(ch, idx)] = Mathf.Clamp01(mc);
            ok++;
        }
        Debug.Log($"[MonsterAttackStyle] 已加载 {ok} 条");
    }

    static MonsterAttackStyle ParseStyle(string s)
    {
        if (s.Equals("Bow", System.StringComparison.OrdinalIgnoreCase)
            || s.Equals("Archer", System.StringComparison.OrdinalIgnoreCase))
            return MonsterAttackStyle.Bow;
        if (s.Equals("Ranged", System.StringComparison.OrdinalIgnoreCase)
            || s.Equals("Orb", System.StringComparison.OrdinalIgnoreCase)
            || s.Equals("Magic", System.StringComparison.OrdinalIgnoreCase))
            return MonsterAttackStyle.Ranged;
        return MonsterAttackStyle.Melee;
    }

    /// <summary>弓箭和法球都算远程，射程与索敌一致</summary>
    public static bool IsRanged(MonsterAttackStyle style)
    {
        return style == MonsterAttackStyle.Ranged || style == MonsterAttackStyle.Bow;
    }

    static int Key(int monsterChapter, int spriteIndex) => monsterChapter * 100 + spriteIndex;

    /// <summary>该怪被判定为魔法型的概率(0~1)。缺配置时回退 0.35（约 1/3 魔法）。</summary>
    public static float GetMagicChance(int monsterChapter, int spriteIndex)
    {
        EnsureLoaded();
        // 2026-09-26 修编译错误：out 变量先声明再用，避免 CS0165（未赋值就 return）
        float c;
        if (_chance != null && _chance.TryGetValue(Key(monsterChapter, spriteIndex), out c)) return c;
        return 0.35f;
    }

    public static MonsterAttackStyle Get(int monsterChapter, int spriteIndex)
    {
        EnsureLoaded();
        if (_map != null && _map.TryGetValue(Key(monsterChapter, spriteIndex), out var style))
            return style;
        // 默认：偶数远程，奇数近战；Boss12 远程
        if (spriteIndex == 12) return MonsterAttackStyle.Ranged;
        return (spriteIndex % 2 == 0) ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
    }

    public static float GetAttackRange(MonsterAttackStyle style)
    {
        return AttackRangeTable.GetMonsterWorld(style);
    }

    /// <summary>
    /// 按表里的 style 分弹道：Bow=箭矢，Ranged/Orb/Magic=法球，其余近战刀光。
    /// → Enemy/Bow/vfx_enemy_bow_fly|hit 或 Enemy/Orb/vfx_enemy_orb_fly|hit
    /// </summary>
    public static AttackVfxKit GetVfxKit(MonsterAttackStyle style)
    {
        switch (style)
        {
            case MonsterAttackStyle.Bow:
                return AttackVfxKit.Bow;
            case MonsterAttackStyle.Ranged:
                return AttackVfxKit.Orb;
            default:
                return AttackVfxKit.MeleeSlash;
        }
    }
}
