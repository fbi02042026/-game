#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从主手武器模板批量生成副手武器（weaponHand=OffHand，降低 Attack）。
/// </summary>
public static class OffHandWeaponGenerator
{
    const string EquipDir = "Assets/Resources/Config/Equips";
    const float AttackScale = 0.6f;

    [MenuItem("Tools/装备/生成副手武器模板")]
    public static void GenerateFromSelection()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("生成副手武器", "请在 Project 中选中一个或多个主手 EquipTemplate。", "确定");
            return;
        }

        int created = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            var src = selected[i] as EquipTemplate;
            if (src == null || src.weaponType == WeaponType.None || src.weaponType == WeaponType.TwoHand)
                continue;
            if (CreateOffHandFromMain(src))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"已生成/更新 {created} 个副手武器模板。", "确定");
    }

    static bool CreateOffHandFromMain(EquipTemplate src)
    {
        string baseId = src.templateId ?? src.name;
        if (baseId.StartsWith("equip_offhand_")) return false;

        string offId = "equip_offhand_" + baseId.Replace("equip_", "");
        string path = $"{EquipDir}/{offId}.asset";
        var tpl = AssetDatabase.LoadAssetAtPath<EquipTemplate>(path);
        if (tpl == null)
        {
            tpl = ScriptableObject.CreateInstance<EquipTemplate>();
            AssetDatabase.CreateAsset(tpl, path);
        }

        tpl.templateId = offId;
        tpl.iconFileName = src.iconFileName;
        tpl.icon = src.icon;
        tpl.equipName = (src.equipName ?? src.iconFileName ?? "副手") + "·副";
        tpl.gridWidth = src.gridWidth;
        tpl.gridHeight = src.gridHeight;
        tpl.tags = src.tags != null ? new List<string>(src.tags) : new List<string>();
        tpl.baseRarity = src.baseRarity;
        tpl.minLevel = src.minLevel;
        tpl.globalBonus = src.globalBonus;
        tpl.isLegendary = src.isLegendary;
        tpl.slotType = EquipSlotType.OffHand;
        tpl.weaponType = src.weaponType;
        tpl.weaponHand = WeaponHandSlot.OffHand;
        tpl.isAnchor = true;
        tpl.grantSkillId = src.grantSkillId;
        tpl.skillPassives = CopyPassives(src.skillPassives);
        tpl.weaponAttackType = src.weaponAttackType;
        tpl.attackRange = src.attackRange;
        tpl.weaponKindOverride = src.weaponKindOverride;
        tpl.armorPrefix = src.armorPrefix;
        tpl.spumName = src.spumName;

        tpl.baseAttr = new List<AttrBonusData>();
        if (src.baseAttr != null)
        {
            for (int i = 0; i < src.baseAttr.Count; i++)
            {
                var b = src.baseAttr[i];
                if (b == null) continue;
                float v = b.value;
                if (b.attrType == AttrType.Attack && !b.isPercent)
                    v = Mathf.Max(1f, Mathf.Round(v * AttackScale));
                tpl.baseAttr.Add(new AttrBonusData
                {
                    attrType = b.attrType,
                    value = v,
                    isPercent = b.isPercent
                });
            }
        }

        EditorUtility.SetDirty(tpl);
        return true;
    }

    static List<AttrBonusData> CopyPassives(List<AttrBonusData> src)
    {
        var dst = new List<AttrBonusData>();
        if (src == null) return dst;
        for (int i = 0; i < src.Count; i++)
        {
            var b = src[i];
            if (b == null) continue;
            dst.Add(new AttrBonusData { attrType = b.attrType, value = b.value, isPercent = b.isPercent });
        }
        return dst;
    }
}
#endif
