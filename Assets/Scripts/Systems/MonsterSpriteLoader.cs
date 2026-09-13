using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 怪物精灵加载器：通过MonsterSpriteRegistry ScriptableObject获取精灵
/// 在编辑器中配置好精灵引用后，运行时直接使用
///
/// 故意【不】实现 <see cref="ICombatBoundSingleton"/>：本类是纯资源查询服务，
/// 城镇/日志场景的 AdventureUI、AdventureCodex 也要用它取怪物立绘。
/// 若标记为战斗专属，Singleton getter 会拒绝自动创建并在城镇里刷 Error。
/// </summary>
public class MonsterSpriteLoader : Singleton<MonsterSpriteLoader>
{
    [Header("精灵注册表")]
    public MonsterSpriteRegistry registry;

    protected override void Awake()
    {
        base.Awake();
        if (registry == null)
        {
            registry = Resources.Load<MonsterSpriteRegistry>("Config/MonsterSpriteRegistry");
            if (registry == null)
            {
                Debug.LogWarning("[MonsterSpriteLoader] 未找到MonsterSpriteRegistry，请在编辑器中创建并赋值");
            }
        }
    }

    /// <summary>
    /// 根据章节和怪物编号加载精灵（索引从0开始）
    /// </summary>
    public Sprite LoadMonsterSprite(int chapter, int monsterIndex)
    {
        if (registry == null) return null;
        return registry.GetSprite(chapter, monsterIndex);
    }

    /// <summary>
    /// 获取章节的随机怪物精灵
    /// </summary>
    public Sprite GetRandomMonsterSprite(int chapter)
    {
        if (registry == null) return null;
        return registry.GetRandomSprite(chapter);
    }

    /// <summary>
    /// 获取章节的精灵数量
    /// </summary>
    public int GetSpriteCount(int chapter)
    {
        if (registry == null) return 0;
        return registry.GetSpritesForChapter(chapter).Count;
    }
}