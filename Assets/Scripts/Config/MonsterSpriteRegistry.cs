using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 怪物精灵注册表：在编辑器中拖入精灵，运行时通过章节和索引获取
/// 每个章节一个Sprite列表，索引对应怪物编号
/// </summary>
[CreateAssetMenu(fileName = "MonsterSpriteRegistry", menuName = "Config/MonsterSpriteRegistry")]
public class MonsterSpriteRegistry : ScriptableObject
{
    [Header("第1章 - Undead (12只)")]
    public List<Sprite> chapter1_Undead = new List<Sprite>();

    [Header("第2章 - Jungle (12只)")]
    public List<Sprite> chapter2_Jungle = new List<Sprite>();

    [Header("第3章 - Sea (12只)")]
    public List<Sprite> chapter3_Sea = new List<Sprite>();

    [Header("第4章 - Forest (12只)")]
    public List<Sprite> chapter4_Forest = new List<Sprite>();

    [Header("第5章 - Field (12只)")]
    public List<Sprite> chapter5_Field = new List<Sprite>();

    [Header("第6章 - Cave (12只)")]
    public List<Sprite> chapter6_Cave = new List<Sprite>();

    [Header("第7章 - Devil (12只)")]
    public List<Sprite> chapter7_Devil = new List<Sprite>();

    [Header("第8章 - Ice (12只)")]
    public List<Sprite> chapter8_Ice = new List<Sprite>();

    /// <summary>
    /// 根据游戏章节获取精灵列表。
    /// 字段名表示列表内题材（非游戏章号）；对齐 chapter_theme_map / Monster.LoadSpriteFromResources。
    /// </summary>
    public List<Sprite> GetSpritesForChapter(int chapter)
    {
        switch (chapter)
        {
            case 1: return chapter4_Forest;   // 1 Forest
            case 2: return chapter1_Undead;   // 2 Undead
            case 3: return chapter2_Jungle;   // 3 Jungle
            case 4: return chapter5_Field;    // 4 Field
            case 5: return chapter3_Sea;      // 5 Sea
            case 6: return chapter6_Cave;     // 6 Cave
            case 7: return chapter7_Devil;    // 7 Devil
            case 8: return chapter8_Ice;      // 8 Ice
            default: return chapter4_Forest;
        }
    }

    /// <summary>
    /// 获取指定章节的指定索引精灵
    /// </summary>
    public Sprite GetSprite(int chapter, int index)
    {
        var sprites = GetSpritesForChapter(chapter);
        if (sprites.Count == 0) return null;
        return sprites[Mathf.Clamp(index, 0, sprites.Count - 1)];
    }

    /// <summary>
    /// 获取随机精灵
    /// </summary>
    public Sprite GetRandomSprite(int chapter)
    {
        var sprites = GetSpritesForChapter(chapter);
        if (sprites.Count == 0) return null;
        return sprites[Random.Range(0, sprites.Count)];
    }
}