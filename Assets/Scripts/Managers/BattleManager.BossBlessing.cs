using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleManager partial —— Boss 关进场 boss 线 + 三选一（2026-11-11 主人拍板
/// 「章节线要加上 boss 线，用 boss 线去丰富剧情」「三选一就是战前剧情之后出现的」）。
/// 主线正式关 Boss 关、章节线战前剧情之后弹：先两句 boss 线引子（BossStoryBeats，用图鉴外号），
/// 再三选一选一条本场加成，引导语按章收口外号。
/// 「单关生效」天然成立：这些加成全走 BattleManager 现成 run 修饰字段，每次进新一关都会重置，
/// 不写任何存档（难度局隔离天然满足）。复用 DialogueUI 选项链（与第一章石碑三选一同一条 UI），
/// 60 秒无输入看门狗自动选第一项放行，绝不把 Boss 关卡死在进场。
/// </summary>
public partial class BattleManager
{
    /// <summary>单条 Boss 关加成：展示文案 + 应用动作。只允许用现成 run 修饰字段/安全操作，不碰伤害公式。</summary>
    struct BossBlessing
    {
        public string Label;
        public Action<BattleManager> Apply;
    }

    /// <summary>Boss 关加成池。每次进场洗牌取 3 条展示（DialogueUI.MaxChoices=3）。</summary>
    static readonly BossBlessing[] BossBlessingPool =
    {
        new BossBlessing { Label = "背水：我方受到伤害 -10%", Apply = bm => bm.runAllyDamageTakenMul = 0.9f },
        new BossBlessing { Label = "迟滞：怪物攻速 -10%",    Apply = bm => bm.runMonsterAtkSpeedMul = 0.9f },
        new BossBlessing { Label = "悬赏：本关金币收益 +25%", Apply = bm => bm.runGoldGainMul = 1.25f },
        new BossBlessing { Label = "回魂：我方立即回满血",   Apply = bm => bm.HealAllAlliesToFull() },
    };

    /// <summary>是否弹 Boss 关三选一（门槛收口在这一处）。</summary>
    bool ShouldOfferBossBlessing()
    {
        if (!IsFormalMainRun) return false;                            // 副本/竞技场/裂隙/支线岛一律不弹
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return false; // 难度局不发加成
        if (Rules.Active || Rules.DirectorOwnsWaves) return false;     // 引导局有自己的节奏
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    /// <summary>弹三选一并应用所选加成。任何异常都放行开打，绝不把 Boss 关卡死在进场。</summary>
    IEnumerator CoBossStageBlessingChoice()
    {
        if (!ShouldOfferBossBlessing()) yield break;

        // 实时抽改版后 CoFirstWaveHardFallback 等 _preBattleStoryPlaying 落 false 才兜底刷怪：
        // 选卡期间置位压住；【2026-11-11】不在这里复位 —— 面板已在走进场前提前 BeginLive，
        // 压制统一由 DoStartFight（点「开始战斗」）解除，中途置 false 会让一次性闸门脱离、2.2s 后硬刷抢跑。
        // 常显抽卡面板不收：它在 Canvas(100)，三选一剧情窗在 13000，压不到选项按钮；
        // 收掉反而会让 Boss 关整场没有抽奖按钮（主人拍板「关卡一上来就显示」）。
        _preBattleStoryPlaying = true;

        // 洗牌取 3 条（池子 4 条，DialogueUI 最多显示 3 个选项）
        var pool = new List<BossBlessing>(BossBlessingPool);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }
        var labels = new[]
        {
            pool[0].Label,
            pool[1].Label,
            pool[2].Label
        };

        bool done = false;
        try
        {
            var beats = new List<StoryBeat>();
            // 【2026-11-11 主人拍板「章节线要加上 boss 线」】两句按章的 boss 线引子先播，
            // 再进三选一；外号在引子里埋、在引导语里收，选完 toast 落账。
            BossStoryBeats.AppendIntro(beats, CurrentChapter);
            beats.Add(
                // 与 Chapter1Story.PlayEnding 的选项 beat 同构（speaker=-1 + 左侧玩家 + choices）
                new StoryBeat
                {
                    leftName = "你",
                    leftPortraitId = StoryPortraits.Player,
                    speaker = -1,
                    text = BossStoryBeats.GetBlessingLead(CurrentChapter),
                    choices = labels
                });
            StoryDirector.Ensure().Play(beats, null, choice =>
            {
                int idx = Mathf.Clamp(choice, 0, 2);
                pool[idx].Apply(this);
                UIManager.Instance?.ShowToast("本关加成：" + pool[idx].Label);   // toast 人设由 GlobalToastUI 统一套
                done = true;
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleManager] Boss 关三选一异常，跳过直接开打: " + e.Message);
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            done = true;
        }

        // 看门狗兜底（StoryDirector 自带 30s 单行 / 60s 选项看门狗，这里是双保险）。
        // 【2026-11-11】加了 boss 线两句引子后最坏情况 = 2×30s + 60s = 120s，
        // 外层必须比它宽，否则点击被吞时会先强制放行、选项窗还挂在屏上。
        float guard = 0f;
        while (!done && guard < 150f)
        {
            guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        if (!done)
        {
            Debug.LogWarning("[BattleManager] Boss 关三选一 150 秒无结果，强制放行");
            StoryDirector.Ensure().StopPlaying();
            var dlg2 = DialogueUI.Instance;
            if (dlg2 != null) dlg2.Hide();
            pool[0].Apply(this);
            UIManager.Instance?.ShowToast("本关加成：" + pool[0].Label);
        }
        // 【2026-11-11】不在此复位 _preBattleStoryPlaying：与上方置位配对，统一由 DoStartFight 解除，
        // 避免进关抽奖面板已提前开出时，硬性保险在 2.2s 后抢刷首波（Editor.log 42151 实证）。
    }

    /// <summary>我方全体（主角+在队佣兵）回满血（「回魂」加成用）。阵亡单位不拉起。</summary>
    void HealAllAlliesToFull()
    {
        if (hero != null && hero.attr != null && hero.currentHp > 0f)
            hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || m.attr == null || m.currentHp <= 0f) continue;
            m.currentHp = m.attr.GetAttr(AttrType.MaxHp);
        }
        BattleUI.Instance?.ForceRefreshLiveBars();
    }
}
