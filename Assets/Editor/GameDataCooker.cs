using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>把源表打成加密 bytes，并生成配置指纹。出包前自动跑。</summary>
public class GameDataCooker : IPreprocessBuildWithReport
{
    public int callbackOrder => 10;

    const string SourceCsv = ContentPaths.Source.Tables + "/monster_attack_style.csv";
    const string OutDir = "Assets/Resources/Data/Tables";

    [MenuItem("Tools/Data/Cook Encrypted Tables")]
    public static void CookMenu()
    {
        CookAll();
        EditorUtility.DisplayDialog("Data", "已写入加密表与配置指纹。", "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        CookAll();
    }

    public static void CookAll()
    {
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(ContentPaths.Source.Tables);
        CookMonsterAttackStyle();
        CookFingerprint();
        AssetDatabase.Refresh();
        Debug.Log("[Data] Cook Encrypted Tables 完成");
    }

    static void CookMonsterAttackStyle()
    {
        string csv = null;
        if (File.Exists(SourceCsv))
            csv = File.ReadAllText(SourceCsv, Encoding.UTF8);
        else
        {
            var ta = Resources.Load<TextAsset>("Config/MonsterAttackStyle");
            if (ta != null) csv = ta.text;
        }
        if (string.IsNullOrEmpty(csv))
        {
            Debug.LogWarning("[Data] 没有 monster_attack_style 源表");
            return;
        }
        if (!File.Exists(SourceCsv))
            File.WriteAllText(SourceCsv, csv, new UTF8Encoding(false));

        WriteEncrypted(OutDir + "/monster_attack_style.bytes", csv);
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
        WriteEncrypted(OutDir + "/config_fingerprint.bytes", sb.ToString());
    }

    static void WriteEncrypted(string assetPath, string utf8)
    {
        byte[] blob = SecureCodec.EncryptUtf8(utf8);
        File.WriteAllBytes(assetPath, blob);
    }
}
