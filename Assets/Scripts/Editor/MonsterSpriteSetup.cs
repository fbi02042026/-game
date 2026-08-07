#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 编辑器工具：一键设置怪物精灵图片导入参数 + 自动填充MonsterSpriteRegistry
/// 菜单：Tools → 设置怪物精灵
///
/// 功能：
/// 1. 将所有怪物PNG图片的Texture Type设为Sprite
/// 2. 设置Pivot为BottomCenter（alignment=7）
/// 3. 自动将所有精灵填入MonsterSpriteRegistry.asset
/// </summary>
public class MonsterSpriteSetup : EditorWindow
{
    [MenuItem("Tools/设置怪物精灵")]
    public static void SetupAll()
    {
        string baseDir = "Assets/Resources/Config/MonsterSpriteRegistry";
        if (!AssetDatabase.IsValidFolder(baseDir))
        {
            EditorUtility.DisplayDialog("错误", $"未找到怪物精灵目录: {baseDir}", "确定");
            return;
        }

        // 章节文件夹映射
        var chapterFolders = new Dictionary<int, string>
        {
            { 1, "1 Undead" },
            { 2, "2 Jungle" },
            { 3, "3 Sea" },
            { 4, "4 Forest" },
            { 5, "5 Field" },
            { 6, "6 Cave" },
            { 7, "7 Devil" },
            { 8, "8 Ice" }
        };

        // 加载或创建 MonsterSpriteRegistry
        string registryPath = "Assets/Resources/Config/MonsterSpriteRegistry.asset";
        MonsterSpriteRegistry registry = AssetDatabase.LoadAssetAtPath<MonsterSpriteRegistry>(registryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<MonsterSpriteRegistry>();
            AssetDatabase.CreateAsset(registry, registryPath);
            Debug.Log("[MonsterSpriteSetup] 创建了新的MonsterSpriteRegistry");
        }

        int totalProcessed = 0;

        foreach (var kvp in chapterFolders)
        {
            int chapter = kvp.Key;
            string folderName = kvp.Value;
            string folderPath = $"{baseDir}/{folderName}";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[MonsterSpriteSetup] 跳过：文件夹不存在 {folderPath}");
                continue;
            }

            // 查找所有PNG文件
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            var sprites = new List<Sprite>();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // 设置导入参数
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }

                    // BottomCenter pivot: alignment=7 (下边缘中心)
                    if (importer.spritePivot != new Vector2(0.5f, 0f))
                    {
                        importer.spritePivot = new Vector2(0.5f, 0f);
                        changed = true;
                    }

                    // 确保是单精灵（不是多图切割）
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    // 过滤alpha，保留透明度
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }

                // 加载Sprite
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                    totalProcessed++;
                }
            }

            // 按名称排序（forest_401, forest_402, ...）
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // 赋值到注册表对应章节
            switch (chapter)
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

            Debug.Log($"[MonsterSpriteSetup] 章节{chapter} ({folderName}): {sprites.Count}个精灵已填充");
        }

        // 保存注册表
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            $"怪物精灵设置完成！\n共处理 {totalProcessed} 个精灵\n\nMonsterSpriteRegistry已自动填充。", "确定");

        Debug.Log($"[MonsterSpriteSetup] 全部完成！共处理 {totalProcessed} 个精灵");
    }
}
#endif
