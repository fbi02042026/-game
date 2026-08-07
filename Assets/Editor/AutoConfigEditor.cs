#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 自动配置工具：一键创建所有需要的ScriptableObject配置
/// 菜单：Tools/自动配置游戏资源
/// </summary>
public class AutoConfigEditor : EditorWindow
{
    [MenuItem("Tools/自动配置游戏资源")]
    public static void ShowWindow()
    {
        GetWindow<AutoConfigEditor>("自动配置");
    }

    void OnGUI()
    {
        GUILayout.Label("游戏资源自动配置", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("点击下方按钮自动创建所有配置文件：", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("1. 创建怪物精灵注册表", GUILayout.Height(40)))
        {
            CreateMonsterSpriteRegistry();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("2. 创建示例装备模板", GUILayout.Height(40)))
        {
            CreateSampleEquipTemplates();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("3. 创建示例怪物配置", GUILayout.Height(40)))
        {
            CreateSampleMonsterConfigs();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("4. 生成战斗UI预制体", GUILayout.Height(40)))
        {
            BattlePrefabGenerator.ShowWindow();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("5. 一键全部配置", GUILayout.Height(50)))
        {
            CreateMonsterSpriteRegistry();
            CreateSampleEquipTemplates();
            CreateSampleMonsterConfigs();
            BattlePrefabGenerator.ShowWindow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AutoConfig] 全部配置完成！");
            EditorUtility.DisplayDialog("配置完成", "所有配置文件及战斗UI预制体已自动创建！", "确定");
        }
    }

    /// <summary>
    /// 创建怪物精灵注册表，自动扫描怪物文件夹
    /// </summary>
    static void CreateMonsterSpriteRegistry()
    {
        // 确保目录存在
        string configDir = "Assets/Resources/Config";
        if (!AssetDatabase.IsValidFolder(configDir))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Config");
        }

        string assetPath = configDir + "/MonsterSpriteRegistry.asset";

        // 加载或创建
        MonsterSpriteRegistry registry = AssetDatabase.LoadAssetAtPath<MonsterSpriteRegistry>(assetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<MonsterSpriteRegistry>();
            AssetDatabase.CreateAsset(registry, assetPath);
        }

        // 怪物精灵根路径
        string basePath = "Assets/2D Pixel RPG Monster Pack/Icons/default size/no shadow/";

        // 8个章节文件夹
        string[] chapterFolders = {
            "1 Undead", "2 Jungle", "3 Sea", "4 Forest",
            "5 Field", "6 Cave", "7 Devil", "8 Ice"
        };

        for (int ch = 0; ch < chapterFolders.Length; ch++)
        {
            string folderPath = basePath + chapterFolders[ch];
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
            List<Sprite> sprites = new List<Sprite>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    sprites.Add(sprite);
            }

            // 按名称排序
            sprites = sprites.OrderBy(s => s.name).ToList();

            // 赋值到对应章节列表
            switch (ch + 1)
            {
                case 1: registry.chapter1_Undead = sprites; break;
                case 2: registry.chapter2_Jungle = sprites; break;
                case 3: registry.chapter3_Sea = sprites; break;
                case 4: registry.chapter4_Forest = sprites; break;
                case 5: registry.chapter5_Field = sprites; break;
                case 6: registry.chapter6_Cave = sprites; break;
                case 7: registry.chapter7_Devil = sprites; break;
                case 8: registry.chapter8_Ice = sprites; break;
            }

            Debug.Log($"[AutoConfig] 章节{ch + 1} ({chapterFolders[ch]}) 加载了 {sprites.Count} 个精灵");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        Debug.Log("[AutoConfig] 怪物精灵注册表创建完成: " + assetPath);
    }

    /// <summary>
    /// 创建示例装备模板
    /// </summary>
    static void CreateSampleEquipTemplates()
    {
        string equipDir = "Assets/Resources/Config/Equips";
        if (!AssetDatabase.IsValidFolder(equipDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Config"))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            AssetDatabase.CreateFolder("Assets/Resources/Config", "Equips");
        }

        // 创建各槽位的示例装备
        CreateEquipTemplate(equipDir, "equip_head_001", "铁头盔", EquipSlotType.Head, Rarity.Common,
            new AttrBonusData { attrType = AttrType.Defense, value = 5, isPercent = false }, "head_iron");

        CreateEquipTemplate(equipDir, "equip_chest_001", "皮胸甲", EquipSlotType.Chest, Rarity.Common,
            new AttrBonusData { attrType = AttrType.Defense, value = 8, isPercent = false }, "chest_leather");

        CreateEquipTemplate(equipDir, "equip_hands_001", "布衣", EquipSlotType.Hands, Rarity.Common,
            new AttrBonusData { attrType = AttrType.MaxHp, value = 20, isPercent = false }, "cloth_basic");

        CreateEquipTemplate(equipDir, "equip_feet_001", "皮靴", EquipSlotType.Feet, Rarity.Common,
            new AttrBonusData { attrType = AttrType.MoveSpeed, value = 0.1f, isPercent = true }, "pant_boots");

        CreateEquipTemplate(equipDir, "equip_cape_001", "普通披风", EquipSlotType.Cape, Rarity.Uncommon,
            new AttrBonusData { attrType = AttrType.Vitality, value = 3, isPercent = false }, "back_cape");

        CreateEquipTemplate(equipDir, "equip_weapon_001", "铁剑", EquipSlotType.MainHand, Rarity.Common,
            new AttrBonusData { attrType = AttrType.Attack, value = 5, isPercent = false }, "weapon_sword",
            WeaponType.OneHand, WeaponAttackType.Physical);

        CreateEquipTemplate(equipDir, "equip_weapon_002", "法杖", EquipSlotType.MainHand, Rarity.Uncommon,
            new AttrBonusData { attrType = AttrType.MagicPower, value = 0.15f, isPercent = true }, "weapon_staff",
            WeaponType.TwoHand, WeaponAttackType.Magic);

        CreateEquipTemplate(equipDir, "equip_shield_001", "木盾", EquipSlotType.OffHand, Rarity.Common,
            new AttrBonusData { attrType = AttrType.Defense, value = 10, isPercent = false }, "weapon_shield",
            WeaponType.None, WeaponAttackType.Physical);

        AssetDatabase.SaveAssets();
        Debug.Log("[AutoConfig] 示例装备模板创建完成");
    }

    static void CreateEquipTemplate(string dir, string id, string name, EquipSlotType slot, Rarity rarity,
        AttrBonusData baseAttr, string spumName, WeaponType weaponType = WeaponType.None, WeaponAttackType attackType = WeaponAttackType.Physical)
    {
        string path = dir + "/" + id + ".asset";
        EquipTemplate tpl = AssetDatabase.LoadAssetAtPath<EquipTemplate>(path);
        if (tpl == null)
        {
            tpl = ScriptableObject.CreateInstance<EquipTemplate>();
            AssetDatabase.CreateAsset(tpl, path);
        }

        tpl.templateId = id;
        tpl.equipName = name;
        tpl.slotType = slot;
        tpl.baseRarity = rarity;
        tpl.spumName = spumName;
        tpl.weaponType = weaponType;
        tpl.weaponAttackType = attackType;
        tpl.gridWidth = 1;
        tpl.gridHeight = 1;
        tpl.minLevel = 1;
        tpl.baseAttr = new List<AttrBonusData> { baseAttr };

        // 防具随机前缀
        if (slot != EquipSlotType.MainHand && slot != EquipSlotType.OffHand && slot != EquipSlotType.Cape)
        {
            ArmorPrefix[] prefixes = { ArmorPrefix.Berserk, ArmorPrefix.Arcane, ArmorPrefix.Holy, ArmorPrefix.Steadfast, ArmorPrefix.Sage, ArmorPrefix.Swift };
            tpl.armorPrefix = prefixes[Random.Range(0, prefixes.Length)];
        }

        EditorUtility.SetDirty(tpl);
    }

    /// <summary>
    /// 创建示例怪物配置
    /// </summary>
    static void CreateSampleMonsterConfigs()
    {
        string monsterDir = "Assets/Resources/Config/Monsters";
        if (!AssetDatabase.IsValidFolder(monsterDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Config"))
                AssetDatabase.CreateFolder("Assets/Resources", "Config");
            AssetDatabase.CreateFolder("Assets/Resources/Config", "Monsters");
        }

        // 为8个章节各创建几种怪物
        string[][] chapterMonsters = {
            new[] { "undead_101", "undead_102", "undead_103", "undead_104" },  // 第1章
            new[] { "jungle_201", "jungle_202", "jungle_203", "jungle_204" },  // 第2章
            new[] { "sea_301", "sea_302", "sea_303", "sea_304" },              // 第3章
            new[] { "forest_401", "forest_402", "forest_403", "forest_404" },  // 第4章
            new[] { "field_501", "field_502", "field_503", "field_504" },      // 第5章
            new[] { "cave_601", "cave_602", "cave_603", "cave_604" },          // 第6章
            new[] { "devil_701", "devil_702", "devil_703", "devil_704" },      // 第7章
            new[] { "ice_801", "ice_802", "ice_803", "ice_804" }               // 第8章
        };

        for (int ch = 0; ch < chapterMonsters.Length; ch++)
        {
            for (int i = 0; i < chapterMonsters[ch].Length; i++)
            {
                string monsterId = chapterMonsters[ch][i];
                string path = monsterDir + "/" + monsterId + ".asset";

                MonsterConfig mc = AssetDatabase.LoadAssetAtPath<MonsterConfig>(path);
                if (mc == null)
                {
                    mc = ScriptableObject.CreateInstance<MonsterConfig>();
                    AssetDatabase.CreateAsset(mc, path);
                }

                mc.id = monsterId;
                mc.monsterName = $"怪物_{ch + 1}_{i + 1}";
                mc.minWave = ch * 15 + 1;
                mc.isBoss = (i == chapterMonsters[ch].Length - 1);
                mc.spriteIndex = i + 1;
                mc.spriteScale = mc.isBoss ? 1.5f : 1f;

                // 基础属性随章节递增
                float chapterMult = 1 + ch * 0.5f;
                mc.baseHp = (50 + i * 20) * chapterMult;
                mc.baseAttack = (5 + i * 2) * chapterMult;
                mc.baseAttackSpeed = 1f;
                mc.attackRange = 1.5f;
                mc.baseMoveSpeed = 0f; // 怪物不移动
                mc.baseGoldDrop = 10 + ch * 5 + i * 3;
                mc.expDrop = 5 + ch * 3 + i * 2;

                EditorUtility.SetDirty(mc);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[AutoConfig] 示例怪物配置创建完成");
    }
}
#endif