#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 编辑器工具：自动创建并填充 BattleBackgroundRegistry
/// 菜单：Tools → 生成战斗背景注册表
///
/// 功能：
/// 1. 扫描 Assets/Art/UI/background/ 下的所有章节文件夹
/// 2. 将每个文件夹中的 1.png(前), 2.png(中), 3.png(后) 加载为Sprite
/// 3. 根据文件夹名映射到游戏章节号（与 ChapterMonsterFolders 一致）
/// 4. 生成 BattleBackgroundRegistry.asset 到 Resources/Config/
/// </summary>
public class BattleBackgroundRegistrySetup : EditorWindow
{
    [MenuItem("Tools/生成战斗背景注册表")]
    public static void Setup()
    {
        string bgDir = "Assets/Art/UI/background";
        if (!AssetDatabase.IsValidFolder(bgDir))
        {
            EditorUtility.DisplayDialog("错误", $"未找到背景图片目录: {bgDir}", "确定");
            return;
        }

        // 章节文件夹映射（与 GameConfig.ChapterMonsterFolders 一致）
        // 游戏章节 → 怪物文件夹名
        var chapterFolders = new Dictionary<int, string>
        {
            { 1, "4 Forest" },
            { 2, "1 Undead" },
            { 3, "2 Jungle" },
            { 4, "3 Sea" },
            { 5, "5 Field" },
            { 6, "6 Cave" },
            { 7, "7 Devil" },
            { 8, "8 Ice" }
        };

        // 创建或加载注册表
        string registryPath = "Assets/Resources/Config/BattleBackgroundRegistry.asset";
        BattleBackgroundRegistry registry = AssetDatabase.LoadAssetAtPath<BattleBackgroundRegistry>(registryPath);

        if (registry == null)
        {
            registry = CreateInstance<BattleBackgroundRegistry>();
            AssetDatabase.CreateAsset(registry, registryPath);
            Debug.Log("[BattleBackgroundRegistry] 创建新注册表");
        }

        registry.backgrounds.Clear();

        int found = 0;
        foreach (var kvp in chapterFolders)
        {
            int chapter = kvp.Key;
            string folderName = kvp.Value;
            string folderPath = $"{bgDir}/{folderName}";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.Log($"[BattleBackgroundRegistry] 章节{chapter}({folderName}) 无背景文件夹，跳过");
                continue;
            }

            // 加载三层背景精灵
            Sprite front = LoadSprite(folderPath, "1.png");
            Sprite mid = LoadSprite(folderPath, "2.png");
            Sprite back = LoadSprite(folderPath, "3.png");

            var bg = new BattleBackgroundRegistry.ChapterBackground
            {
                chapter = chapter,
                folderName = folderName,
                frontSprite = front,
                midSprite = mid,
                backSprite = back
            };

            registry.backgrounds.Add(bg);
            found++;

            Debug.Log($"[BattleBackgroundRegistry] 章节{chapter}({folderName}): 前={front!=null} 中={mid!=null} 后={back!=null}");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            $"战斗背景注册表生成完成！\n共{found}个章节有背景\n保存到: {registryPath}", "确定");
    }

    /// <summary>从指定文件夹加载精灵</summary>
    static Sprite LoadSprite(string folderPath, string fileName)
    {
        string fullPath = $"{folderPath}/{fileName}";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);

        if (sprite == null)
        {
            // 尝试加载为Texture并转换为Sprite
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (tex != null)
            {
                // 设置Texture的导入类型为Sprite
                string assetPath = fullPath;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                }
            }
        }

        return sprite;
    }
}
#endif
