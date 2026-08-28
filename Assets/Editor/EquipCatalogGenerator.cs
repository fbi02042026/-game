#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描 EquipIcons，生成占格对照表，并按规则批量创建 EquipTemplate。
/// </summary>
public static class EquipCatalogGenerator
{
    const string CatalogPathPrimary = "Assets/Docs/装备占格对照表.md";
    const string CatalogPathFallback = "Docs/装备占格对照表.md";
    const string EquipDir = "Assets/Resources/Config/Equips";

    static string ResolveCatalogPath()
    {
        string primary = Path.GetFullPath(CatalogPathPrimary);
        if (File.Exists(primary)) return primary;
        string fallback = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CatalogPathFallback));
        return fallback;
    }

    [MenuItem("Tools/_归档/装备/打开占格对照表")]
    public static void OpenCatalogInProject()
    {
        string path = ResolveCatalogPath();
        if (!File.Exists(path))
        {
            GenerateCatalogMarkdown();
            path = ResolveCatalogPath();
        }
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPathPrimary);
        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return;
        }
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/_归档/装备/生成占格对照表")]
    public static void GenerateCatalogMarkdown()
    {
        var names = EquipIcons.GetAllFileNames();
        var sb = new StringBuilder();
        sb.AppendLine("# 装备占格对照表");
        sb.AppendLine();
        sb.AppendLine("图标目录：`Assets/Art/UI/Icons/EquipIcons/`");
        sb.AppendLine();
        sb.AppendLine("**用法**：在「确认宽」「确认高」列填入最终占格（留空=用建议值）。修改后运行 **Tools/装备/从对照表生成装备模板**。");
        sb.AppendLine();
        sb.AppendLine("| 图标文件名 | 模板ID | 槽位 | 建议宽 | 建议高 | **确认宽** | **确认高** | 武器类型 | weaponHand | 备注 |");
        sb.AppendLine("|-----------|--------|------|--------|--------|-----------|-----------|----------|------------|------|");

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            var spec = EquipGridRules.Infer(name);
            string tid = EquipGridRules.MakeTemplateId(name);
            string wt = spec.weaponType.ToString();
            string hand = spec.weaponHand != WeaponHandSlot.None ? spec.weaponHand.ToString() : "";
            string note = spec.slot == EquipSlotType.MainHand && spec.weaponType == WeaponType.OneHand
                ? "短武器默认1×2" : "";
            sb.AppendLine($"| {name} | {tid} | {spec.slot} | {spec.width} | {spec.height} |  |  | {wt} | {hand} | {note} |");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CatalogPathPrimary)) ?? ".");
        File.WriteAllText(Path.GetFullPath(CatalogPathPrimary), sb.ToString(), Encoding.UTF8);
        // 同步一份到项目根 Docs（方便用 Cursor / Excel 打开）
        string fallbackDir = Path.Combine(Application.dataPath, "..", "Docs");
        Directory.CreateDirectory(fallbackDir);
        File.WriteAllText(Path.Combine(fallbackDir, "装备占格对照表.md"), sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"[EquipCatalog] 已生成 {names.Length} 行 → {CatalogPathPrimary}");
        EditorUtility.DisplayDialog("完成", $"占格对照表已生成：\n{CatalogPathPrimary}\n共 {names.Length} 个图标", "确定");
    }

    [MenuItem("Tools/_归档/装备/从对照表生成装备模板")]
    public static void GenerateTemplatesFromCatalog()
    {
        string full = ResolveCatalogPath();
        if (!File.Exists(full))
        {
            EditorUtility.DisplayDialog("缺少对照表", "请先运行 Tools/装备/生成占格对照表", "确定");
            return;
        }

        EnsureEquipDir();
        var rows = ParseCatalog(File.ReadAllLines(full, Encoding.UTF8));
        int created = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrEmpty(r.iconFileName)) continue;
            if (CreateOrUpdateTemplate(r))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EquipIcons.ClearCache();
        EditorUtility.DisplayDialog("完成", $"已更新/创建 {created} 个装备模板\n目录：{EquipDir}", "确定");
    }

    [MenuItem("Tools/_归档/装备/按规则批量生成全部装备模板")]
    public static void GenerateAllFromRules()
    {
        EnsureEquipDir();
        var names = EquipIcons.GetAllFileNames();
        int n = 0;
        for (int i = 0; i < names.Length; i++)
        {
            var spec = EquipGridRules.Infer(names[i]);
            if (CreateOrUpdateTemplate(new CatalogRow
            {
                iconFileName = names[i],
                templateId = EquipGridRules.MakeTemplateId(names[i]),
                slot = spec.slot.ToString(),
                width = spec.width,
                height = spec.height,
                weaponType = spec.weaponType.ToString()
            }))
                n++;
        }
        AssetDatabase.SaveAssets();
        EquipIconBinder.BindAllTemplates();
        EditorUtility.DisplayDialog("完成", $"已按文件名规则生成 {n} 个模板并绑定图标", "确定");
    }

    static void EnsureEquipDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Config/Equips"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Config"))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            AssetDatabase.CreateFolder("Assets/Resources/Config", "Equips");
        }
    }

    struct CatalogRow
    {
        public string iconFileName;
        public string templateId;
        public string slot;
        public int width;
        public int height;
        public string weaponType;
        public string weaponHand;
    }

    static List<CatalogRow> ParseCatalog(string[] lines)
    {
        var list = new List<CatalogRow>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith("|") || line.Contains("图标文件名") || line.Contains("---")) continue;
            string[] parts = line.Split('|');
            if (parts.Length < 9) continue;
            string icon = parts[1].Trim();
            if (string.IsNullOrEmpty(icon)) continue;

            int.TryParse(string.IsNullOrEmpty(parts[6].Trim()) ? parts[4].Trim() : parts[6].Trim(), out int w);
            int.TryParse(string.IsNullOrEmpty(parts[7].Trim()) ? parts[5].Trim() : parts[7].Trim(), out int h);
            if (w < 1) w = 1;
            if (h < 1) h = 1;

            list.Add(new CatalogRow
            {
                iconFileName = icon,
                templateId = parts[2].Trim(),
                slot = parts[3].Trim(),
                width = w,
                height = h,
                weaponType = parts.Length > 8 ? parts[8].Trim() : "None",
                weaponHand = parts.Length > 9 ? parts[9].Trim() : ""
            });
        }
        return list;
    }

    static bool CreateOrUpdateTemplate(CatalogRow row)
    {
        if (string.IsNullOrEmpty(row.templateId)) row.templateId = EquipGridRules.MakeTemplateId(row.iconFileName);
        string path = $"{EquipDir}/{row.templateId}.asset";
        var tpl = AssetDatabase.LoadAssetAtPath<EquipTemplate>(path);
        if (tpl == null)
        {
            tpl = ScriptableObject.CreateInstance<EquipTemplate>();
            AssetDatabase.CreateAsset(tpl, path);
        }

        tpl.templateId = row.templateId;
        tpl.iconFileName = row.iconFileName;
        tpl.equipName = row.iconFileName;
        tpl.gridWidth = row.width;
        tpl.gridHeight = row.height;
        tpl.minLevel = 1;
        tpl.baseRarity = Rarity.Common;
        if (tpl.baseAttr == null || tpl.baseAttr.Count == 0)
            tpl.baseAttr = new List<AttrBonusData> { new AttrBonusData { attrType = AttrType.Attack, value = 5 } };

        if (System.Enum.TryParse(row.slot, out EquipSlotType slot))
            tpl.slotType = slot;
        if (System.Enum.TryParse(row.weaponType, out WeaponType wt))
            tpl.weaponType = wt;

        var inferred = EquipGridRules.Infer(row.iconFileName);
        tpl.weaponHand = inferred.weaponHand;
        if (!string.IsNullOrEmpty(row.weaponHand) && System.Enum.TryParse(row.weaponHand, out WeaponHandSlot parsedHand))
            tpl.weaponHand = parsedHand;
        if (tpl.weaponHand == WeaponHandSlot.None && tpl.weaponType != WeaponType.None)
            tpl.weaponHand = WeaponLoadoutRules.InferHandFromIcon(row.iconFileName, tpl.weaponType);

        if (tpl.slotType == EquipSlotType.MainHand)
            tpl.attackRange = GameConfig.ResolveWeaponAttackRange(tpl) * GameConfig.PIXEL_PER_UNIT;

        tpl.ResolveIcon();
        EditorUtility.SetDirty(tpl);
        return true;
    }
}
#endif
