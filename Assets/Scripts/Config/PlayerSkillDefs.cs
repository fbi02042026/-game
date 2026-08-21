using System;
using UnityEngine;

/// <summary>
/// 玩家可携带技能表（与 Docs/像素冒险：裂缝之刃_玩家技能设计.md 一致）。
/// 每次战斗只能带 1 个；手动点击释放；解锁只跟通关章节，没有玩家等级。
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

    public static readonly Def[] All =
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

    public static Def Get(int index)
    {
        if (index < 0 || index >= All.Length) return All[0];
        return All[index];
    }

    public static Def GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return All[0];
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return All[0];
    }

    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return i;
        return 0;
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
