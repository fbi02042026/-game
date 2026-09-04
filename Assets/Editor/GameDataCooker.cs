using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 把源表写入 Resources。开发期明文；ContentProtection.Enabled 时才加密。
/// 出包前仍可跑，但关闭保护时不会写 PAT1、也不强制指纹校验。
/// </summary>
public class GameDataCooker : IPreprocessBuildWithReport
{
    public int callbackOrder => 10;

    const string SourceMonsterCsv = ContentPaths.Source.Tables + "/monster_attack_style.csv";
    const string SourceMercSkillsCsv = ContentPaths.Source.Tables + "/merc_skills.csv";
    const string SourceMercSkillMapCsv = ContentPaths.Source.Tables + "/merc_skill_map.csv";
    const string SourceMercLinesCsv = ContentPaths.Source.Tables + "/merc_lines.csv";
    const string OutDir = "Assets/Resources/Data/Tables";

    [MenuItem("Tools/Data/Cook Tables (Plain while protection off)")]
    public static void CookMenu()
    {
        CookAll();
        string mode = ContentProtection.Enabled ? "加密" : "明文";
        EditorUtility.DisplayDialog("Data", "已写入表与指纹（当前模式：" + mode + "）。", "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        // 开发期也保证 Resources 表存在；不因保护关闭而跳过
        CookAll();
    }

    public static void CookAll()
    {
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(ContentPaths.Source.Tables);
        CookMonsterAttackStyle();
        CookMercSkills();
        CookMercSkillMap();
        CookMercLines();
        CookIntelQuiz();
        CookIntelDaily();
        CookMonsterSpriteOpaque();
        CookEquipAnchors();
        CookMonsterStats();
        CookChapterThemeMap();
        CookMonsterUnlockTier();
        CookStageSpawn();
        CookTutorialBattle();
        CookBattleQuest();
        CookStageRollerWeights();
        CookSpritePickWeight();
        CookWaveSlot();
        CookChapterBranch();
        CookChapterBranchRules();
        if (ContentProtection.Enabled)
            CookFingerprint();
        else
        {
            // 去掉旧加密指纹，避免误报
            string fp = OutDir + "/config_fingerprint.bytes";
            if (File.Exists(fp))
                File.Delete(fp);
            string fpMeta = fp + ".meta";
            if (File.Exists(fpMeta))
                File.Delete(fpMeta);
        }
        AssetDatabase.Refresh();
        Debug.Log("[Data] Cook Tables 完成（ContentProtection=" + ContentProtection.Enabled + ")");
    }

    static void CookMonsterAttackStyle()
    {
        CookTableFromSource(SourceMonsterCsv, "Config/MonsterAttackStyle", "monster_attack_style");
    }

    static void CookMercSkills()
    {
        CookTableFromSource(SourceMercSkillsCsv, null, "merc_skills");
    }

    static void CookMercSkillMap()
    {
        CookTableFromSource(SourceMercSkillMapCsv, null, "merc_skill_map");
    }

    static void CookMercLines()
    {
        CookTableFromSource(SourceMercLinesCsv, null, "merc_lines");
    }

    static void CookIntelQuiz()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/intel_quiz.csv", null, "intel_quiz");
    }

    static void CookIntelDaily()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/intel_daily.csv", null, "intel_daily");
    }

    static void CookEquipAnchors()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/equip_anchors.csv", null, "equip_anchors");
    }

    static void CookMonsterSpriteOpaque()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/monster_sprite_opaque.csv", null, "monster_sprite_opaque");
    }

    static void CookMonsterStats()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/monster_stats.csv", null, "monster_stats");
    }

    static void CookChapterThemeMap()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/chapter_theme_map.csv", null, "chapter_theme_map");
    }

    static void CookMonsterUnlockTier()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/monster_unlock_tier.csv", null, "monster_unlock_tier");
    }

    static void CookStageSpawn()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/stage_spawn.csv", null, "stage_spawn");
    }

    static void CookTutorialBattle()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/tutorial_battle.csv", null, "tutorial_battle");
    }

    static void CookBattleQuest()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/battle_quest.csv", null, "battle_quest");
    }

    static void CookStageRollerWeights()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/stage_roller_weights.csv", null, "stage_roller_weights");
    }

    static void CookSpritePickWeight()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/sprite_pick_weight.csv", null, "sprite_pick_weight");
    }

    static void CookWaveSlot()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/wave_slot.csv", null, "wave_slot");
    }

    static void CookChapterBranch()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/chapter_branch.csv", null, "chapter_branch");
    }

    static void CookChapterBranchRules()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/chapter_branch_rules.csv", null, "chapter_branch_rules");
    }

    static void CookTableFromSource(string sourceCsv, string legacyResourcesPath, string outName)
    {
        string csv = null;
        if (File.Exists(sourceCsv))
            csv = File.ReadAllText(sourceCsv, Encoding.UTF8);
        else if (!string.IsNullOrEmpty(legacyResourcesPath))
        {
            var ta = Resources.Load<TextAsset>(legacyResourcesPath);
            if (ta != null) csv = ta.text;
        }
        if (string.IsNullOrEmpty(csv))
        {
            Debug.LogWarning("[Data] 没有 " + outName + " 源表");
            return;
        }
        if (!File.Exists(sourceCsv))
            File.WriteAllText(sourceCsv, csv, new UTF8Encoding(false));

        WriteTable(OutDir + "/" + outName + ".bytes", csv);
    }

    static void CookFingerprint()
    {
        var sb = new StringBuilder();
        var equips = Resources.LoadAll<EquipTemplate>(ContentPaths.Config.Equips);
        for (int i = 0; i < equips.Length; i++)
        {
            var t = equips[i];
            if (t == null || string.IsNullOrEmpty(t.templateId)) continue;
            sb.AppendLine(ConfigFingerprint.Line("E", t.templateId, ConfigFingerprint.HashEquip(t)));
        }
        var monsters = Resources.LoadAll<MonsterConfig>(ContentPaths.Config.Monsters);
        for (int i = 0; i < monsters.Length; i++)
        {
            var m = monsters[i];
            if (m == null || string.IsNullOrEmpty(m.id)) continue;
            sb.AppendLine(ConfigFingerprint.Line("M", m.id, ConfigFingerprint.HashMonster(m)));
        }
        var talents = Resources.LoadAll<TalentConfig>(ContentPaths.Config.Talents);
        for (int i = 0; i < talents.Length; i++)
        {
            var t = talents[i];
            if (t == null || string.IsNullOrEmpty(t.id)) continue;
            sb.AppendLine(ConfigFingerprint.Line("T", t.id, ConfigFingerprint.HashTalent(t)));
        }
        string[] skillFolders =
        {
            ContentPaths.Config.SkillsAlly,
            ContentPaths.Config.SkillsMonster,
            ContentPaths.Config.SkillsPlayerLegacy,
            ContentPaths.Config.SkillsMercLegacy
        };
        for (int f = 0; f < skillFolders.Length; f++)
        {
            var skills = Resources.LoadAll<SkillConfig>(skillFolders[f]);
            for (int i = 0; i < skills.Length; i++)
            {
                var s = skills[i];
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                sb.AppendLine(ConfigFingerprint.Line("S", s.id, ConfigFingerprint.HashSkill(s)));
            }
        }
        WriteTable(OutDir + "/config_fingerprint.bytes", sb.ToString());
    }

    static void WriteTable(string assetPath, string utf8)
    {
        if (ContentProtection.Enabled)
            File.WriteAllBytes(assetPath, SecureCodec.EncryptUtf8(utf8));
        else
            File.WriteAllText(assetPath, utf8 ?? "", new UTF8Encoding(false));
    }
}
