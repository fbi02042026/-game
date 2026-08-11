using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物近战/远程表：读 Resources/Config/MonsterAttackStyle.csv
/// style = Melee | Ranged（法球） | Bow（弓箭）
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

        // txt 优先（Unity 稳定当 TextAsset）；csv 兼容
        TextAsset ta = Resources.Load<TextAsset>("Config/MonsterAttackStyle");
        if (ta == null)
        {
            Debug.LogWarning("[MonsterAttackStyle] 未找到 Config/MonsterAttackStyle.txt，默认近战/远程兜底");
            return;
        }

        string[] lines = ta.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
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
        if (!IsRanged(style)) return GameConfig.RangeSword;
        return GameConfig.RangeBow * GameConfig.MONSTER_RANGED_RANGE_MUL;
    }

    public static AttackVfxKit GetVfxKit(MonsterAttackStyle style)
    {
        switch (style)
        {
            case MonsterAttackStyle.Bow: return AttackVfxKit.Bow;
            case MonsterAttackStyle.Ranged: return AttackVfxKit.Orb;
            default: return AttackVfxKit.MeleeSlash;
        }
    }
}
