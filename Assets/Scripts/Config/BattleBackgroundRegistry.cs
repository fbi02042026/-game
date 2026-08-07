using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗背景注册表：按章节映射视差背景精灵
/// 背景图片路径: Assets/Art/UI/background/{文件夹名}/1.png(前), 2.png(中), 3.png(后)
/// 文件夹名与怪物文件夹名一致（如 "4 Forest", "1 Undead"）
/// 如果某个层缺失（如2 Jungle没有2.png），则该层保持空白
/// </summary>
[CreateAssetMenu(fileName = "BattleBackgroundRegistry", menuName = "Config/BattleBackgroundRegistry")]
public class BattleBackgroundRegistry : ScriptableObject
{
    [System.Serializable]
    public class ChapterBackground
    {
        [Tooltip("游戏章节号（1-8）")]
        public int chapter;
        [Tooltip("怪物文件夹名（如 '4 Forest'）")]
        public string folderName;
        [Tooltip("前层背景（layer 1，近景）")]
        public Sprite frontSprite;
        [Tooltip("中层背景（layer 2，中景），可为空")]
        public Sprite midSprite;
        [Tooltip("后层背景（layer 3，远景）")]
        public Sprite backSprite;
    }

    [Header("章节背景映射")]
    public List<ChapterBackground> backgrounds = new List<ChapterBackground>();

    /// <summary>
    /// 获取指定章节的背景配置
    /// </summary>
    public ChapterBackground GetBackground(int chapter)
    {
        return backgrounds.Find(b => b.chapter == chapter);
    }

    /// <summary>
    /// 获取指定章节的某层背景精灵
    /// </summary>
    /// <param name="layerName">"front" / "mid" / "back"</param>
    public Sprite GetLayerSprite(int chapter, string layerName)
    {
        var bg = GetBackground(chapter);
        if (bg == null) return null;

        switch (layerName.ToLower())
        {
            case "front": return bg.frontSprite;
            case "mid": return bg.midSprite;
            case "back": return bg.backSprite;
            default: return null;
        }
    }

    /// <summary>
    /// 是否有指定章节的背景
    /// </summary>
    public bool HasBackground(int chapter)
    {
        var bg = GetBackground(chapter);
        return bg != null && (bg.frontSprite != null || bg.midSprite != null || bg.backSprite != null);
    }
}
