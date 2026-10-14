using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物索敌策略（2026-10-14 主人拍板）：把「谁近打谁」升级成三种目标偏好——
/// 近战按距离、远程先打脆皮职业、一部分怪挑血少的收割。
/// 评分制：每种策略折算成一个 float 分数，分越小越优先；平手一律比距离。
/// 只改「选谁当目标」，不动移动/并道/攻击闸门那套链路。
/// </summary>
public enum MonsterTargetingPolicy
{
    /// <summary>按距离：谁近打谁（近战默认；与旧行为完全等价）。</summary>
    Distance = 0,
    /// <summary>按职业：先打脆皮（远程职业优先），同档比距离（远程默认）。</summary>
    SquishyJob = 1,
    /// <summary>按血量：挑当前血最少的收割，平手比距离。</summary>
    LowHp = 2,
}

public static class MonsterTargeting
{
    /// <summary>策略评分权重：脆皮档位差一档 = 1000 距离（脆皮永远压倒距离差，除非隔半个屏幕）。</summary>
    const float RankWeight = 1000f;

    /// <summary>
    /// 按本单位策略从 allies 里挑目标；无存活/视口内目标返回 null（调用方自己兜底）。
    /// Distance 策略与旧 FindNearestEnemyOnField 完全等价（同过滤、同二维距离）。
    /// </summary>
    public static UnitBase Pick(Monster self, List<UnitBase> allies)
    {
        if (self == null || allies == null) return null;
        UnitBase best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < allies.Count; i++)
        {
            var enemy = allies[i];
            if (enemy == null || enemy.isDead) continue;
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            float dist = UnitBase.GetCombatDist(self, enemy);
            float score;
            switch (self.TargetingPolicy)
            {
                case MonsterTargetingPolicy.SquishyJob:
                    score = SquishRank(enemy) * RankWeight + dist;
                    break;
                case MonsterTargetingPolicy.LowHp:
                    // 「挑血少的打」＝当前血量少者优先；血量相同比距离
                    score = enemy.currentHp * RankWeight + dist;
                    break;
                default:
                    score = dist;
                    break;
            }
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        return best;
    }

    /// <summary>脆皮分档：0=脆皮优先打（远程职业），1=近战肉盾。玩家按进战职业、佣兵按 mercId 前缀/职业名。</summary>
    static int SquishRank(UnitBase u)
    {
        if (u is Hero)
        {
            switch (PlayerJobDefs.GetSelected())
            {
                case PlayerJobId.Mage:
                case PlayerJobId.Priest:
                case PlayerJobId.Ranger:
                    return 0;   // 法师/牧师/游侠 = 脆皮
                default:
                    return 1;   // 剑盾/狂战/重武 = 肉盾
            }
        }
        if (u is Mercenary merc && !Mercenary.IsMeleeRoleId(merc.mercId))
            return 0;           // 佣兵侧：奶妈/法师/牧师/弓手 = 脆皮（口径与 IsMeleeRoleId 一致）
        return 1;
    }

    /// <summary>
    /// 出场时抽一次策略：默认近战=按距离、远程=先打脆皮；
    /// 另有 <see cref="GameConfig.MONSTER_LOW_HP_TARGET_RATIO"/> 比例的怪被抽成「挑血少的打」（远近通吃）。
    /// </summary>
    public static MonsterTargetingPolicy RollPolicy(bool isRanged)
    {
        if (Random.value < GameConfig.MONSTER_LOW_HP_TARGET_RATIO)
            return MonsterTargetingPolicy.LowHp;
        return isRanged ? MonsterTargetingPolicy.SquishyJob : MonsterTargetingPolicy.Distance;
    }
}
