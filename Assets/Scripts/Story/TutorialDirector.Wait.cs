using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Wait.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：战场等待工具（清场/怪进场/尸体离场/玩家前进）

public partial class TutorialDirector
{
    /// <summary>等场上活怪都走完进场（停稳），再播惊吓台词。</summary>
    static IEnumerator WaitMonstersFinishedEnter(BattleManager bm, float timeout = 10f)
    {
        if (bm == null) yield break;
        // 等至少刷出一只并进入进场状态（或已停稳）
        float t = 0f;
        while (t < 1.5f && bm.GetAliveMonsterCount() <= 0)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < timeout)
        {
            bool anyEntering = false;
            var list = bm.monsters;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var mon = list[i] as Monster;
                    if (mon == null || mon.isDead) continue;
                    // 剧情暂停窗里怪只走到屏幕边缘就站定（_isEnteringMap 仍为 true，
                    // 要等「！」后才继续推进），所以必须把 EnterAtPauseStop 也算作业已入场，
                    // 否则这里会一直空转到 timeout（10 秒）才弹对白。
                    if (mon.IsEnteringMap && !mon.EnterAtPauseStop)
                    {
                        anyEntering = true;
                        break;
                    }
                }
            }
            if (!anyEntering && bm.GetAliveMonsterCount() > 0)
            {
                // 停稳后再短顿一拍，让玩家看清
                yield return new WaitForSecondsRealtime(0.2f);
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning("[Tutorial] 等待怪进场超时，继续播「！」");
    }

    IEnumerator WaitFieldClear(bool strict = false)
    {
        var bm = BattleManager.Instance;
        float t = 0f;
        const float timeout = 90f;
        float zeroHold = 0f;
        while (bm != null && t < timeout)
        {
            int alive = bm.GetAliveMonsterCount();
            // 已排队但未刷出的波次也算「未清」：否则刷怪空窗 alive==0 会被误判为已清、提前进对话
            bool pending = bm.HasPendingWaves;
            if (alive > 0 || pending)
                zeroHold = 0f;
            else
            {
                zeroHold += Time.unscaledDeltaTime;
                if (zeroHold >= (strict ? 0.45f : 0.35f))
                    yield break;
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (bm != null && bm.GetAliveMonsterCount() > 0)
            Debug.LogWarning($"[Tutorial] 清场超时仍有怪 alive={bm.GetAliveMonsterCount()} strict={strict}，继续流程");

        if (bm != null && strict)
            bm.UnitsCanAct = true;
    }

    /// <summary>
    /// 2026-09-28 主人要求「宝箱剧情前打死最后一个敌人，要等这个敌人消失了再出现宝箱」：
    /// WaitFieldClear 只看 isDead —— 怪一断气就算清场，尸体还在播 0.8 秒死亡动画（deathAnimDuration），
    /// 宝箱会当着尸体冒出来。这里补一步：等场上再没有任何「还在场上的怪（活着的 + 尸体）」才放行。
    /// 纯等待，不改写任何坐标；超时直接放行，绝不卡流程。
    /// </summary>
    static IEnumerator CoWaitMonstersGone(BattleManager bm, float timeout = 3f)
    {
        if (bm == null) yield break;
        float t = 0f;
        while (t < timeout)
        {
            bool anyOnField = false;
            var all = Object.FindObjectsOfType<Monster>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                anyOnField = true;
                break;
            }
            if (!anyOnField) yield break;

            t += 0.12f;
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    /// <summary>
    /// 2026-09-27 主人要求「清完一波走两步再出宝箱」：等玩家自己往前走够距离。
    /// 只等不推 —— 绝不改写 Hero 坐标（2026-09-18 主人明确：推坐标看起来像瞬移）。
    /// 超时只是放行继续流程（不是拿别的值顶替），并把实际走了多远打出来，便于定位「为什么不走」。
    /// </summary>
    IEnumerator CoWaitHeroAdvance(float dist, float timeout = 4f)
    {
        var bm = BattleManager.Instance;
        if (bm != null) bm.UnitsCanAct = true;

        var hero = Hero.Instance;
        if (hero == null || dist <= 0f) yield break;

        float startX = UnitBase.GetCombatX(hero);
        float t = 0f;
        while (t < timeout)
        {
            t += Time.unscaledDeltaTime;
            if (hero == null) yield break;
            if (UnitBase.GetCombatX(hero) - startX >= dist) yield break;
            yield return null;
        }

        if (hero != null && UnitBase.GetCombatX(hero) - startX < dist)
            Debug.LogWarning($"[Tutorial] 等待玩家前进超时：只走了 {(UnitBase.GetCombatX(hero) - startX):F2}/{dist:F2}，继续流程");
    }
}
