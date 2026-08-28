using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 校验所有 SkillConfig：必须有专属 VFX 或 attackKit，避免新技能静默无特效/播错套。
/// 菜单：Tools/VFX/Validate Skill VFX
/// </summary>
public static class SkillVfxValidator
{
    [MenuItem("Tools/VFX/Validate Skill VFX")]
    public static void ValidateMenu()
    {
        int issues = ValidateAll(out string report);
        if (issues <= 0)
            EditorUtility.DisplayDialog("Skill VFX", "全部技能特效配置 OK。\n\n" + report, "OK");
        else
            EditorUtility.DisplayDialog("Skill VFX", $"发现 {issues} 处问题：\n\n" + report, "OK");
        Debug.Log(report);
    }

    public static int ValidateAll(out string report)
    {
        var sb = new StringBuilder();
        int issues = 0;
        string[] folders =
        {
            ContentPaths.Config.SkillsAlly,
            ContentPaths.Config.SkillsMonster,
            ContentPaths.Config.SkillsPlayerLegacy,
            ContentPaths.Config.SkillsMercLegacy
        };

        for (int f = 0; f < folders.Length; f++)
        {
            var list = Resources.LoadAll<SkillConfig>(folders[f]);
            if (list == null) continue;
            for (int i = 0; i < list.Length; i++)
            {
                var cfg = list[i];
                if (cfg == null) continue;
                if (string.IsNullOrEmpty(cfg.id))
                {
                    issues++;
                    sb.AppendLine($"✗ {AssetDatabase.GetAssetPath(cfg)} id 为空");
                    continue;
                }

                bool hasPrefab = cfg.vfxPrefab != null;
                if (!hasPrefab)
                {
                    // 与 SkillRegistry.GetSkillVfxPrefab 一致：主目录 + Ally/Merc/Monster 兜底
                    string side = SkillRegistry.ResolveSkillVfxFolder(cfg.id);
                    hasPrefab = Resources.Load<GameObject>($"VFX/Skills/{side}/{cfg.id}") != null;
                    if (!hasPrefab)
                    {
                        string[] vfxFolders = { "Ally", "Merc", "Monster" };
                        for (int j = 0; j < vfxFolders.Length; j++)
                        {
                            if (vfxFolders[j] == side) continue;
                            if (Resources.Load<GameObject>($"VFX/Skills/{vfxFolders[j]}/{cfg.id}") != null)
                            {
                                hasPrefab = true;
                                break;
                            }
                        }
                    }
                }

                if (hasPrefab)
                {
                    sb.AppendLine($"✓ {cfg.id} 有专属 VFX");
                    continue;
                }

                if (cfg.attackKit == AttackVfxKit.None)
                {
                    issues++;
                    sb.AppendLine($"✗ {cfg.id} 无专属 VFX 且 attackKit=None → 运行时会乱兜底。请设 attackKit 或放 VFX/Skills/.../{cfg.id}");
                }
                else
                {
                    AttackVfxKit resolved = SkillNaming.ResolveSkillVfxKit(cfg, cfg.id);
                    sb.AppendLine($"✓ {cfg.id} 回退共用套 {resolved}（{SkillNaming.SharedKitResourceHint(resolved, VfxFaction.Ally)}）");
                    // 抽查 Ally Shared 是否存在
                    if (resolved == AttackVfxKit.MeleeSlash
                        && Resources.Load<GameObject>("VFX/Shared/Ally/MeleeSlash/vfx_melee_hit") == null)
                    {
                        issues++;
                        sb.AppendLine($"  ✗ 缺 Shared 刀光 vfx_melee_hit");
                    }
                }
            }
        }

        sb.Insert(0, issues <= 0
            ? "Skill VFX 校验通过。\n"
            : $"Skill VFX 校验失败（{issues}）：\n");
        report = sb.ToString();
        return issues;
    }
}
