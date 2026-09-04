using UnityEngine;

/// <summary>构建后配置校验入口，由 ConfigManager / SkillRegistry 填入后再核。</summary>
public static class GameDataHub
{
    static System.Collections.Generic.List<EquipTemplate> _equips;
    static System.Collections.Generic.List<MonsterConfig> _monsters;
    static System.Collections.Generic.Dictionary<string, TalentConfig> _talents;
    static System.Collections.Generic.Dictionary<string, SkillConfig> _skills;
    static bool _equipsReady;

    public static void ReportConfigs(
        System.Collections.Generic.List<EquipTemplate> equips,
        System.Collections.Generic.List<MonsterConfig> monsters,
        System.Collections.Generic.Dictionary<string, TalentConfig> talents)
    {
        _equips = equips;
        _monsters = monsters;
        _talents = talents;
        _equipsReady = true;
        TryVerify();
    }

    public static void ReportSkills(System.Collections.Generic.Dictionary<string, SkillConfig> skills)
    {
        _skills = skills;
        TryVerify();
    }

    static void TryVerify()
    {
        if (!_equipsReady) return;
        ConfigFingerprint.VerifyLoaded(_equips, _monsters, _talents, _skills);
    }
}
