using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物近战/远程表：读 Resources/Config/MonsterAttackStyle.csv
/// style = Melee | Ranged
/// </summary>
public enum MonsterAttackStyle
{
    Melee = 0,
    Ranged = 1
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

            string styleStr = cols[2].Trim();
            MonsterAttackStyle style = styleStr.Equals("Ranged", System.StringComparison.OrdinalIgnoreCase)
                ? MonsterAttackStyle.Ranged
                : MonsterAttackStyle.Melee;
            _map[Key(ch, idx)] = style;
            ok++;
        }
        Debug.Log($"[MonsterAttackStyle] 已加载 {ok} 条");
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
        // 数值表无怪物独立射程：近战按单手剑 96px，远程按弓箭 300px
        return style == MonsterAttackStyle.Ranged ? GameConfig.RangeBow : GameConfig.RangeSword;
    }

    public static AttackVfxKit GetVfxKit(MonsterAttackStyle style)
    {
        return style == MonsterAttackStyle.Ranged ? AttackVfxKit.Orb : AttackVfxKit.MeleeSlash;
    }
}
