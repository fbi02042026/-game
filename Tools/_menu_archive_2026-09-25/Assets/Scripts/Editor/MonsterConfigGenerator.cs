#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 编辑器工具：更新怪物配置的渐进式解锁参数
/// 菜单：Tools → 更新怪物解锁配置
///
/// 功能：
/// 1. 扫描 Resources/Config/Monsters/ 下所有 MonsterConfig
/// 2. 根据 spriteIndex 自动设置 unlockClearCount：
///    - spriteIndex 1-5: unlockClearCount=0（首次即可出现）
///    - spriteIndex 6-8: unlockClearCount=2（通关2-3次后解锁）
///    - spriteIndex 9-10: unlockClearCount=4（通关4+次后解锁）
///    - spriteIndex 11-12 (BOSS): unlockClearCount=0（BOSS不受通关次数限制）
/// 3. 根据 spriteIndex 自动设置 minWave（控制出场顺序）
/// </summary>
public class MonsterConfigGenerator : EditorWindow
{
    [MenuItem("Tools/更新怪物解锁配置")]
    public static void UpdateAll()
    {
        string monstersDir = "Assets/Resources/Config/Monsters";
        if (!AssetDatabase.IsValidFolder(monstersDir))
        {
            EditorUtility.DisplayDialog("错误", $"未找到怪物配置目录: {monstersDir}", "确定");
            return;
        }

        // 查找所有 MonsterConfig
        string[] guids = AssetDatabase.FindAssets("t:MonsterConfig", new[] { monstersDir });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "未找到任何 MonsterConfig 资产", "确定");
            return;
        }

        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonsterConfig config = AssetDatabase.LoadAssetAtPath<MonsterConfig>(path);

            if (config == null) continue;

            bool changed = false;

            // 根据 spriteIndex 设置 unlockClearCount
            int newUnlockCount;
            if (config.isBoss || config.spriteIndex >= GameConfig.BOSS_SPRITE_START)
            {
                // BOSS 不受通关次数限制
                newUnlockCount = 0;
            }
            else if (config.spriteIndex <= 0)
            {
                // spriteIndex=0 表示随机，保持 unlockClearCount=0
                newUnlockCount = 0;
            }
            else if (config.spriteIndex <= GameConfig.TIER0_MAX_SPRITE)
            {
                // 精灵1-5：首次即可出现
                newUnlockCount = 0;
            }
            else if (config.spriteIndex <= GameConfig.TIER1_MAX_SPRITE)
            {
                // 精灵6-8：通关2次后解锁
                newUnlockCount = GameConfig.TIER1_UNLOCK_CLEARS;
            }
            else
            {
                // 精灵9-10：通关4次后解锁
                newUnlockCount = GameConfig.TIER2_UNLOCK_CLEARS;
            }

            if (config.unlockClearCount != newUnlockCount)
            {
                config.unlockClearCount = newUnlockCount;
                changed = true;
            }

            // 根据 spriteIndex 设置 minWave（控制出场顺序）
            // spriteIndex 1-2: minWave=0（第1关即可出现）
            // spriteIndex 3-4: minWave=1（第2关开始出现）
            // spriteIndex 5+: minWave=2（第3关开始出现）
            // BOSS: minWave=9（最后一关）
            int newMinWave;
            if (config.isBoss)
            {
                newMinWave = GameConfig.STAGES_PER_CHAPTER - 1;
            }
            else if (config.spriteIndex <= 2)
            {
                newMinWave = 0;
            }
            else if (config.spriteIndex <= 4)
            {
                newMinWave = 1;
            }
            else
            {
                newMinWave = 2;
            }

            if (config.minWave != newMinWave)
            {
                config.minWave = newMinWave;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(config);
                updated++;
                Debug.Log($"[MonsterConfigGenerator] 更新 {config.id}: spriteIndex={config.spriteIndex}, unlockClearCount={config.unlockClearCount}, minWave={config.minWave}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            $"怪物解锁配置更新完成！\n共更新{updated}个配置（共{guids.Length}个）", "确定");
    }

    /// <summary>
    /// 检查每个章节是否有足够的怪物配置
    /// 菜单：Tools → 检查怪物配置完整性
    /// </summary>
    [MenuItem("Tools/检查怪物配置完整性")]
    public static void CheckCompleteness()
    {
        string monstersDir = "Assets/Resources/Config/Monsters";
        string[] guids = AssetDatabase.FindAssets("t:MonsterConfig", new[] { monstersDir });

        var byChapter = new Dictionary<int, List<MonsterConfig>>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonsterConfig config = AssetDatabase.LoadAssetAtPath<MonsterConfig>(path);
            if (config == null) continue;

            int ch = ExtractChapterFromId(config.id);
            if (!byChapter.ContainsKey(ch))
                byChapter[ch] = new List<MonsterConfig>();
            byChapter[ch].Add(config);
        }

        string report = "=== 怪物配置完整性报告 ===\n\n";

        for (int gameChapter = 1; gameChapter <= 8; gameChapter++)
        {
            int monsterChapter = GameConfig.GetMonsterChapter(gameChapter);
            report += $"游戏第{gameChapter}章 (怪物章{monsterChapter}):\n";

            if (!byChapter.ContainsKey(monsterChapter))
            {
                report += "  ❌ 无任何怪物配置！\n\n";
                continue;
            }

            var configs = byChapter[monsterChapter];
            bool hasBoss = configs.Any(c => c.isBoss);

            report += $"  配置数: {configs.Count}\n";
            report += $"  有BOSS配置: {(hasBoss ? "✓" : "❌")}\n";

            foreach (var c in configs.OrderBy(c => c.spriteIndex))
            {
                report += $"    - {c.id}: sprite={c.spriteIndex}, unlock={c.unlockClearCount}, minWave={c.minWave}, boss={c.isBoss}\n";
            }
            report += "\n";
        }

        Debug.Log(report);
        EditorUtility.DisplayDialog("检查完成", report, "确定");
    }

    static int ExtractChapterFromId(string id)
    {
        int underscoreIdx = id.IndexOf('_');
        if (underscoreIdx >= 0 && underscoreIdx + 2 < id.Length)
        {
            if (int.TryParse(id.Substring(underscoreIdx + 1, 1), out int ch))
                return ch;
        }
        return 0;
    }
}
#endif
