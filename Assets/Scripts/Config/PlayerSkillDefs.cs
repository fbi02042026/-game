using System;
using UnityEngine;

/// <summary>
/// 玩家可携带技能。元数据优先读 <see cref="PlayerSkillTable"/>（CSV Cook）；
/// 缺表回退本类 Fallback（与 player_skills.csv 1:1）。
/// 每次战斗只能带 1 个；受击回能、满条自动释放（引导可锁点击）。解锁只跟通关章节。
/// 未知 id 返回 null，禁止静默回退「治愈之泉」。
/// </summary>
public static class PlayerSkillDefs
{
    public const int Count = 6;

    public enum Kind
    {
        Heal,
        Shield,
        AtkBuff,
        AtkSpeedBuff,
        CritBuff,
        Aoe
    }

    [Serializable]
    public class Def
    {
        public string id;
        public string displayName;
        public Kind kind;
        public string desc;
        public string numbers;
        public float cooldown;
        public float duration;
        public string useHint;
        public int unlockChapter; // 通关该章后解锁（maxUnlockedChapter > unlockChapter）；0=初始
        public string allyConfigId;
        public Color tint;
    }

    /// <summary>缺表时的 1:1 种子；数值与 player_skills.csv / 设计文档一致。</summary>
    public static readonly Def[] Fallback =
    {
        new Def
        {
            id = "heal_spring",
            displayName = "治愈之泉",
            kind = Kind.Heal,
            desc = "给当前生命比例最低的我方单位回血（玩家或佣兵）。",
            numbers = "恢复目标 30% 最大生命",
            cooldown = 12f,
            duration = 0f,
            useHint = "血量危险时手动点击",
            unlockChapter = 0,
            allyConfigId = "ally_heal",
            tint = new Color(0.35f, 0.75f, 0.4f)
        },
        new Def
        {
            id = "holy_barrier",
            displayName = "圣盾壁垒",
            kind = Kind.Shield,
            desc = "获得护盾，持续期间免疫控制。",
            numbers = "获得 35% 最大生命的护盾，持续 5 秒",
            cooldown = 18f,
            duration = 5f,
            useHint = "精英/Boss 放大招前、或被包围时手动点击",
            unlockChapter = 1,
            allyConfigId = "ally_shield",
            tint = new Color(0.35f, 0.55f, 0.9f)
        },
        new Def
        {
            id = "battle_surge",
            displayName = "战意爆发",
            kind = Kind.AtkBuff,
            desc = "短时间内大幅提升攻击。",
            numbers = "攻击 +30%，持续 8 秒",
            cooldown = 18f,
            duration = 8f,
            useHint = "精英/Boss 战或大量小怪时手动点击",
            unlockChapter = 2,
            allyConfigId = "ally_atk_up",
            tint = new Color(0.9f, 0.45f, 0.25f)
        },
        new Def
        {
            id = "gale_stance",
            displayName = "疾风架势",
            kind = Kind.AtkSpeedBuff,
            desc = "进入疾风状态，攻速大幅提升。",
            numbers = "攻速 +35%，持续 6 秒",
            cooldown = 15f,
            duration = 6f,
            useHint = "输出窗口期手动点击",
            unlockChapter = 3,
            allyConfigId = "ally_atk_speed",
            tint = new Color(0.4f, 0.7f, 0.95f)
        },
        new Def
        {
            id = "deadly_focus",
            displayName = "致命专注",
            kind = Kind.CritBuff,
            desc = "集中精神，暴击率提升。",
            numbers = "暴击率 +25%，持续 8 秒",
            cooldown = 18f,
            duration = 8f,
            useHint = "Boss 战或精英怪出现时手动点击",
            unlockChapter = 4,
            allyConfigId = "ally_crit_up",
            tint = new Color(0.95f, 0.55f, 0.25f)
        },
        new Def
        {
            id = "thunder_verdict",
            displayName = "天雷裁决",
            kind = Kind.Aoe,
            desc = "召唤天雷轰击目标区域。",
            numbers = "造成 300% 攻击的范围伤害",
            cooldown = 25f,
            duration = 0f,
            useHint = "怪群聚集或 Boss 虚弱时手动点击",
            unlockChapter = 5,
            allyConfigId = "ally_thunder",
            tint = new Color(0.65f, 0.4f, 0.9f)
        }
    };

    static Def[] _all;
    static bool _loaded;

    public static Def[] All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _all = null;
        PlayerSkillTable.Reload();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded && _all != null) return;
        _loaded = true;
        _all = CloneFallback();

        if (!PlayerSkillTable.HasData) return;

        var rows = PlayerSkillTable.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int idx = IndexOfLoaded(row.Id);
            if (idx >= 0)
                Overlay(_all[idx], row);
            else
            {
                var extra = new Def { tint = Color.white };
                Overlay(extra, row);
                Append(extra);
            }
        }
    }

    static Def[] CloneFallback()
    {
        var copy = new Def[Fallback.Length];
        for (int i = 0; i < Fallback.Length; i++)
            copy[i] = CloneDef(Fallback[i]);
        return copy;
    }

    static Def CloneDef(Def src)
    {
        return new Def
        {
            id = src.id,
            displayName = src.displayName,
            kind = src.kind,
            desc = src.desc,
            numbers = src.numbers,
            cooldown = src.cooldown,
            duration = src.duration,
            useHint = src.useHint,
            unlockChapter = src.unlockChapter,
            allyConfigId = src.allyConfigId,
            tint = src.tint
        };
    }

    static void Overlay(Def dest, PlayerSkillTable.Row row)
    {
        dest.id = row.Id;
        dest.displayName = row.DisplayName;
        dest.kind = row.Kind;
        dest.desc = row.Desc;
        dest.numbers = row.Numbers;
        dest.cooldown = row.Cooldown;
        dest.duration = row.Duration;
        dest.useHint = row.UseHint;
        dest.unlockChapter = row.UnlockChapter;
        dest.allyConfigId = row.AllyConfigId;
    }

    static void Append(Def def)
    {
        var next = new Def[_all.Length + 1];
        Array.Copy(_all, next, _all.Length);
        next[_all.Length] = def;
        _all = next;
    }

    static int IndexOfLoaded(string id)
    {
        if (_all == null || string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return i;
        return -1;
    }

    public static Def Get(int index)
    {
        EnsureLoaded();
        if (index < 0 || index >= _all.Length) return null;
        return _all[index];
    }

    /// <summary>未知 id 返回 null（不再静默回退 All[0]）。</summary>
    public static Def GetById(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return _all[i];
        return null;
    }

    /// <summary>未找到返回 -1。</summary>
    public static int IndexOf(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].id == id) return i;
        return -1;
    }

    public static bool IsUnlocked(Def def, SaveData data)
    {
        if (def == null) return false;
        if (def.unlockChapter <= 0) return true;
        if (def.id == "holy_barrier" && data != null && data.chapter1ChoiceDone) return true;
        int chapter = data != null ? data.maxUnlockedChapter : 1;
        return chapter > def.unlockChapter;
    }

    public static string FormatDetail(Def def)
    {
        if (def == null) return "";
        return $"{def.numbers}。冷却 {def.cooldown:0} 秒。{def.useHint}。";
    }

    public static string FormatUnlockHint(Def def)
    {
        if (def == null) return "未解锁";
        if (def.unlockChapter <= 0) return "未解锁";
        return $"未解锁：通关第{def.unlockChapter}章后解锁";
    }
}
