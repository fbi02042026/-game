using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Elite.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：精英宿敌拍（找精英/挑衅/遗言）、技能自动释放观察、压轴拍

public partial class TutorialDirector
{
    // ============================================================
    // 精英宿敌拍（2026-10-07 主人拍板）
    // ============================================================

    /// <summary>精英血量跌到这个比例就说遗言（说完好让塔克收掉他）。</summary>
    const float EliteLastWordsHpRatio = 0.30f;

    /// <summary>
    /// 等精英真刷出来（波次是异步进场的），找到就回调出去。
    /// 找精英只看 <c>Monster.IsEliteWave</c> 一处真源，找不到再按满血最高的那只兜 ——
    /// 兜不住就回 null，由调用方 LogError，绝不拿普通小怪冒充精英说台词。
    /// </summary>
    IEnumerator CoFindWaveElite(BattleManager bm, System.Action<Monster> onFound)
    {
        onFound?.Invoke(null);
        if (bm == null) yield break;

        float t = 0f;
        Monster elite = null;
        while (t < 8f)
        {
            elite = FindElite(bm);
            if (elite != null) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (elite == null)
        {
            // 再按「满血最高」兜一次：精英血档本来就比小怪高一截
            float best = -1f;
            for (int i = 0; i < bm.monsters.Count; i++)
            {
                var m = bm.monsters[i] as Monster;
                if (m == null || m.isDead) continue;
                float maxHp = m.attr != null ? m.attr.GetAttr(AttrType.MaxHp) : 0f;
                if (maxHp > best) { best = maxHp; elite = m; }
            }
        }

        // 【2026-10-14 主人报「精英还没出来就先出现对话框了」】波次是异步进场的，对象刷出 ≠ 人到了。
        // 找到精英后必须等他**进场走完**（IsEnteringMap=false）再回调弹宿敌对白，
        // 否则对白弹在半路上、精英还堵在屏外，观感就是「小怪先出、精英磨蹭、对话框抢跑」。
        // 上限 15s 防卡死（超时放行，不拖死流程）。
        // 【2026-10-16 主人又报「精英还没出来又先出现对话了」】光等 IsEnteringMap 挡不住抢跑：
        // Monster.Update 有「进场途中贴近英雄 → 提前结束进场并开打」的分支，会把 IsEnteringMap
        // 提前翻 false —— 对白就在精英还堵在屏外时弹了。真值 = **精英整个人进到镜头里**：
        // X 落在可视范围内才算「出来」，人没露面绝不弹对白（15s 超时放行防卡死）。
        if (elite != null)
        {
            float enterWait = 0f;
            while (enterWait < 15f && !elite.isDead)
            {
                BattleManager.GetBattleVisibleX(out float vMin, out float vMax, 0.35f);
                // Visual 与 MonsterBody 同 X（Init 里 localPosition 归零），直接读 transform 即可
                float ex = elite.transform.position.x;
                bool onScreen = ex >= vMin && ex <= vMax;
                if (!elite.IsEnteringMap && onScreen) break;
                enterWait += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        onFound?.Invoke(elite);
    }

    static Monster FindElite(BattleManager bm)
    {
        if (bm == null || bm.monsters == null) return null;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i] as Monster;
            if (m != null && !m.isDead && m.IsEliteWave) return m;
        }
        return null;
    }

    /// <summary>
    /// 精英残血遗言：血跌到 <see cref="EliteLastWordsHpRatio"/> 就冻场让他把话说完再死。
    /// 已经死了 / 超时没打到残血 → 直接返回，不拖流程（不倒回来补台词）。
    /// </summary>
    IEnumerator CoEliteLastWords(BattleManager bm, BattleHeadTalkUI headTalk, Monster elite)
    {
        if (bm == null || elite == null || elite.isDead) yield break;

        float maxHp = elite.attr != null ? elite.attr.GetAttr(AttrType.MaxHp) : 0f;
        if (maxHp <= 0f) yield break;

        float t = 0f;
        while (t < 25f)
        {
            if (elite == null || elite.isDead) yield break;
            if (elite.currentHp / maxHp <= EliteLastWordsHpRatio) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (elite == null || elite.isDead) yield break;

        yield return TalkBlock(bm, headTalk,
            new TalkLine(elite, "我不过是个小杂鱼……", 2.2f),
            new TalkLine(elite, "可你前面的路，将会是一片黑暗。", 2.6f));
    }

    /// <summary>
    /// 【2026-10-14 已删】原「塔克收头」演出（遗言后冲身侧挥剑判死）—— 主人拍板
    /// 「精英播放完剧情后立即播放死亡动画 不要让塔克再打一下」，遗言后直接判死（见调用处）。
    /// </summary>

    /// <summary>
    /// P3：最后一波把所有技能充满，让玩家亲眼看到技能自动放出去。
    /// 「技能真放出去了」或「这一波打完」哪个先到就结束；最多等 8 秒，不拖流程。
    /// </summary>
    IEnumerator CoWatchPlayerSkill(BattleManager bm, TutorialHintUI hint)
    {
        if (bm == null) yield break;

        // 【2026-10-14 主人拍板】玩家**获得两个技能**才弹这条「自动释放/顺序」引导 ——
        // 主人原话「为什么没有获得两个技能就弹这个引导了」。引导局只抽 1 个技能 → 不弹；
        // 技能数口径 = RunLoadout.SkillIds()（开局不带初始技能，列表全是本局获得的）。
        var ownedSkills = RunLoadout.SkillIds();
        if (ownedSkills == null || ownedSkills.Count < 2) yield break;

        _sawPlayerSkillCast = false;
        bm.FillPlayerSkillEnergy();
        RunSkillBarUI.Refresh();

        // 2026-10-06 主人拍板：说清「顺序＝优先级」。
        // 【2026-10-18】左栏「本局构筑」面板已整体下线（主人拍板「把这个都去掉」），
        // 提示不再锚它，改为无目标居中气泡；①②③④ 口径与底部技能槽 / SkillOrderGuide 一致。
        hint.Show("技能各自倒计时，冷却好了自动释放，顺序看技能槽 ①②③④。", null, 8f);

        float t = 0f;
        const float wait = 8f;
        while (t < wait)
        {
            if (_sawPlayerSkillCast) break;
            if (Hero.Instance == null || Hero.Instance.isDead) break;
            if (bm.GetAliveMonsterCount() <= 0) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_sawPlayerSkillCast)
        {
            hint.Show("就是这样：技能按顺序自动放，你只管走位。", null, 3f);
            yield return new WaitForSecondsRealtime(1.2f);
        }
        hint.Hide();
    }

    /// <summary>
    /// 压轴拍（tutorial_battle order=6）：夹击 + 2 精英，正常打不完。
    /// 这里<b>不能</b>等清场——清不完会白等 WaitFieldClear 的 90s 超时。
    /// 收尾条件是三选一：血量跌破阈值 / 撑满时长 / 场上被打空（再补一波援军后仍空）。
    /// 设计意图不是必死，而是让玩家自己产生「该撤了」的判断，撤离才不是被流程按头。
    /// </summary>
    IEnumerator CoFinalPressureBeat(BattleManager bm, TutorialHintUI hint,
        BattleHeadTalkUI headTalk, UnitBase merc)
    {
        if (bm == null) yield break;

        yield return EnsureTutorialStep(bm, 6);
        // 【2026-10-17 主人拍板】压轴波同样解锁：按距离打玩家/塔克，不全程锁英雄。
        bm.ClearMonsterForcedTargets();
        hint.Show("撑不住就撤——活着才能把东西带回去。", null, 4f);

        const float maxHold = 20f;
        const float hpBailRatio = 0.4f;
        float t = 0f;
        bool reinforced = false;

        while (t < maxHold)
        {
            if (bm == null || Hero.Instance == null || Hero.Instance.isDead) break;

            float maxHp = Hero.Instance.attr != null ? Hero.Instance.attr.GetAttr(AttrType.MaxHp) : 0f;
            if (maxHp > 0f && Hero.Instance.currentHp / maxHp <= hpBailRatio) break;

            if (bm.GetAliveMonsterCount() <= 0 && !bm.HasPendingWaves)
            {
                // 玩家太强、压轴被打穿：补一波援军，保证「该撤了」的体感仍然成立
                if (!reinforced)
                {
                    reinforced = true;
                    yield return EnsureTutorialStep(bm, 6);
                    // 【2026-10-17 主人拍板】援军同样按距离索敌，不锁英雄。
                    bm.ClearMonsterForcedTargets();
                }
                else break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        hint.Hide();
        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "撑不住了，先撤？", 1.2f),
            new TalkLine(merc, "嗯，回城包扎管够。", 1.2f)); // 【2026-10-16 主人拍板】「疗个痛快」→「包扎管够」
    }
}
