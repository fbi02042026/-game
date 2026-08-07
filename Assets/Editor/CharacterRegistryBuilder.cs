#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 角色注册表生成器
/// 菜单：Tools/生成角色注册表
/// 自动扫描 Art/UI/Icons/Heads 的头像图标和 SPUM/Resources/Units 的预制体，
/// 生成 CharacterRegistry.asset 到 Resources/Config 下，无需手动拖拽配置
/// </summary>
public class CharacterRegistryBuilder : EditorWindow
{
    private const string HEADS_DIR = "Assets/Art/UI/Icons/Heads";
    private const string UNITS_DIR = "Assets/SPUM/Resources/Units";
    private const string REGISTRY_PATH = "Assets/Resources/Config/CharacterRegistry.asset";

    /// <summary>拼音前缀 → 职业名</summary>
    private static readonly Dictionary<string, string> JobNameMap = new Dictionary<string, string>
    {
        { "wanjia", "玩家" },
        { "dunbing", "盾兵" },
        { "gongshou", "弓手" },
        { "kuangzhan", "狂战" },
        { "naima", "奶妈" },
        { "qita", "骑士" },
    };

    [MenuItem("Tools/生成角色注册表")]
    public static void ShowWindow()
    {
        Build();
    }

    public static void Build()
    {
        // 1. 收集 Units 下所有预制体名（跳过 SPUM_ 自动生成的临时预制体）
        HashSet<string> prefabNames = new HashSet<string>();
        if (Directory.Exists(UNITS_DIR))
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UNITS_DIR });
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(p);
                if (name.StartsWith("SPUM_")) continue;
                prefabNames.Add(name);
            }
        }
        else
        {
            Debug.LogWarning($"[CharacterRegistryBuilder] 预制体目录不存在: {UNITS_DIR}");
        }

        // 2. 扫描 Heads 下的头像图标，以图标为准建立条目
        List<CharacterRegistry.CharacterEntry> entries = new List<CharacterRegistry.CharacterEntry>();
        if (Directory.Exists(HEADS_DIR))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { HEADS_DIR });
            foreach (string g in guids)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(g);
                string fileName = Path.GetFileNameWithoutExtension(texPath);
                if (!fileName.StartsWith("icon_")) continue;

                // characterId = 文件名去掉 icon_ 前缀（与预制体名一致）
                string characterId = fileName.Substring("icon_".Length);

                // 确保图片为Sprite类型（UI Image需要Sprite）
                EnsureSpriteImport(texPath);

                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                if (icon == null)
                {
                    Debug.LogWarning($"[CharacterRegistryBuilder] 无法加载头像Sprite: {texPath}");
                    continue;
                }

                bool hasPrefab = prefabNames.Contains(characterId);

                entries.Add(new CharacterRegistry.CharacterEntry
                {
                    characterId = characterId,
                    prefabName = hasPrefab ? characterId : "",
                    iconSprite = icon,
                    jobName = GetJobName(characterId),
                    isPlayer = characterId == "wanjia"
                });
            }
        }
        else
        {
            Debug.LogWarning($"[CharacterRegistryBuilder] 头像目录不存在: {HEADS_DIR}");
        }

        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("生成角色注册表", "未扫描到任何头像图标，请检查目录:\n" + HEADS_DIR, "确定");
            return;
        }

        // 3. 创建或更新 registry.asset
        CharacterRegistry registry = AssetDatabase.LoadAssetAtPath<CharacterRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            string dir = Path.GetDirectoryName(REGISTRY_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
            registry = ScriptableObject.CreateInstance<CharacterRegistry>();
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }
        registry.entries = entries;
        registry.InvalidateCache();
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 4. 输出结果
        string log = $"[CharacterRegistryBuilder] 角色注册表生成完成，共 {entries.Count} 个角色\n路径: {REGISTRY_PATH}";
        foreach (var e in entries)
        {
            log += $"\n  - {e.characterId} | 职业:{e.jobName} | 预制体:{(string.IsNullOrEmpty(e.prefabName) ? "无" : e.prefabName)} | 玩家:{e.isPlayer}";
        }
        Debug.Log(log);

        EditorUtility.DisplayDialog("生成角色注册表",
            $"角色注册表生成完成！\n共 {entries.Count} 个角色\n路径: {REGISTRY_PATH}", "确定");
    }

    /// <summary>确保图片导入类型为Sprite（UI Image需要Sprite类型）</summary>
    static void EnsureSpriteImport(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.SaveAndReimport();
        }
    }

    /// <summary>根据拼音前缀推断职业名</summary>
    static string GetJobName(string characterId)
    {
        foreach (var kvp in JobNameMap)
        {
            if (characterId.StartsWith(kvp.Key))
                return kvp.Value;
        }
        return characterId;
    }
}
#endif
