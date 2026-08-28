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
        CookMonsterSpriteOpaque();
        CookEquipAnchors();
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

    static void CookEquipAnchors()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/equip_anchors.csv", null, "equip_anchors");
    }

    static void CookMonsterSpriteOpaque()
    {
        CookTableFromSource(ContentPaths.Source.Tables + "/monster_sprite_opaque.csv", null, "monster_sprite_opaque");
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
