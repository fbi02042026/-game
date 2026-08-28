using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 merc_skills 表生成 Config/Skills/Merc/SK*.asset（主动技）。
/// </summary>
public static class MercSkillConfigGenerator
{
    const string OutDir = "Assets/Resources/Config/Skills/Merc";

    [MenuItem("Tools/Data/生成佣兵技能 SkillConfig")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutDir);
        MercSkillTable.Reload();
        int count = 0;
        for (int i = 1; i <= 20; i++)
        {
            string id = $"SK{i:03d}";
            if (!MercSkillTable.TryGet(id, out var row) || row.IsPassive) continue;
            var cfg = MercSkillTable.BuildRuntimeConfig(id);
            if (cfg == null) continue;
            string path = $"{OutDir}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SkillConfig>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(cfg, path);
            }
            else
            {
                existing.id = cfg.id;
                existing.skillName = cfg.skillName;
                existing.desc = cfg.desc;
                existing.skillType = cfg.skillType;
                existing.attackKit = cfg.attackKit;
                existing.damageMultiplier = cfg.damageMultiplier;
                existing.cooldown = cfg.cooldown;
                existing.aoeRadius = cfg.aoeRadius;
                existing.buffAttr = cfg.buffAttr;
                existing.buffValue = cfg.buffValue;
                existing.buffIsPercent = cfg.buffIsPercent;
                existing.duration = cfg.duration;
                existing.healBase = cfg.healBase;
                existing.healPercentOfMax = cfg.healPercentOfMax;
                EditorUtility.SetDirty(existing);
            }
            count++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MercSkillConfigGenerator] 已同步 {count} 个主动技 asset → {OutDir}");
    }
}
