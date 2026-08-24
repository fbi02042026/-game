using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System;

/// <summary>配置指纹：进包时写入加密清单，运行时核对，防止直接改 .asset 数值。</summary>
public static class ConfigFingerprint
{
    const char Sep = '\t';

    public static string HashMonster(MonsterConfig m)
    {
        if (m == null) return "";
        return Sha(m.id, m.monsterName, m.minWave, m.isBoss, m.unlockClearCount,
            m.baseHp, m.baseAttack, m.baseAttackSpeed, m.attackRange, m.baseMoveSpeed,
            m.baseGoldDrop, m.expDrop, m.spriteIndex);
    }

    public static string HashEquip(EquipTemplate t)
    {
        if (t == null) return "";
        return Sha(t.templateId, t.equipName, (int)t.baseRarity, t.gridWidth, t.gridHeight,
            t.iconFileName ?? "");
    }

    public static string HashTalent(TalentConfig t)
    {
        if (t == null) return "";
        return Sha(t.id, t.talentName, t.maxLevel, t.costPerLevel, (int)t.attrType, t.valuePerLevel);
    }

    public static string HashSkill(SkillConfig s)
    {
        if (s == null) return "";
        return Sha(s.id, s.skillName, s.damageMultiplier, s.baseDamage, s.cooldown,
            s.aoeRadius, s.projectileCount, (int)s.skillType);
    }

    public static string Line(string kind, string id, string hash)
    {
        return kind + Sep + id + Sep + hash;
    }

    public static bool VerifyLine(string kind, string id, string actualHash, System.Collections.Generic.Dictionary<string, string> map)
    {
        if (map == null || string.IsNullOrEmpty(id)) return true;
        string key = kind + "/" + id;
        if (!map.TryGetValue(key, out string expect))
            return true;
        return string.Equals(expect, actualHash, System.StringComparison.Ordinal);
    }

    public static System.Collections.Generic.Dictionary<string, string> Parse(string text)
    {
        var map = new System.Collections.Generic.Dictionary<string, string>();
        if (string.IsNullOrEmpty(text)) return map;
        string[] lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("#")) continue;
            string[] p = line.Split(Sep);
            if (p.Length < 3) continue;
            map[p[0] + "/" + p[1]] = p[2];
        }
        return map;
    }

    public static void VerifyLoaded(
        System.Collections.Generic.IList<EquipTemplate> equips,
        System.Collections.Generic.IList<MonsterConfig> monsters,
        System.Collections.Generic.IDictionary<string, TalentConfig> talents,
        System.Collections.Generic.IDictionary<string, SkillConfig> skills)
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.ConfigFingerprint);
        if (string.IsNullOrEmpty(raw))
        {
#if !UNITY_EDITOR
            Debug.LogWarning("[Data] 缺少配置指纹，请在编辑器执行 Tools/Data/Cook Encrypted Tables");
#endif
            return;
        }

        var map = Parse(raw);
        int bad = 0;
        int checkedN = 0;
        if (equips != null)
        {
            for (int i = 0; i < equips.Count; i++)
            {
                var t = equips[i];
                if (t == null || string.IsNullOrEmpty(t.templateId)) continue;
                checkedN++;
                if (!VerifyLine("E", t.templateId, HashEquip(t), map)) bad++;
            }
        }
        if (monsters != null)
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                checkedN++;
                if (!VerifyLine("M", m.id, HashMonster(m), map)) bad++;
            }
        }
        if (talents != null)
        {
            foreach (var kv in talents)
            {
                if (kv.Value == null) continue;
                checkedN++;
                if (!VerifyLine("T", kv.Key, HashTalent(kv.Value), map)) bad++;
            }
        }
        if (skills != null)
        {
            foreach (var kv in skills)
            {
                if (kv.Value == null) continue;
                checkedN++;
                if (!VerifyLine("S", kv.Key, HashSkill(kv.Value), map)) bad++;
            }
        }

        if (bad > 0)
            Debug.LogError("[Data] 配置指纹失败 " + bad + "/" + checkedN + "（表可能被篡改，或未重新 Cook）");
        else if (checkedN > 0)
            Debug.Log("[Data] 配置指纹通过 " + checkedN);
    }

    static string Sha(params object[] parts)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append('|');
            if (parts[i] is float f)
                sb.Append(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            else if (parts[i] is double d)
                sb.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            else
                sb.Append(parts[i] != null ? Convert.ToString(parts[i], System.Globalization.CultureInfo.InvariantCulture) : "");
        }
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }
    }
}
