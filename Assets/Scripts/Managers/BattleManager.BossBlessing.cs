using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleManager partial —— Boss 关进场 boss 线 + 三选一（2026-11-11 主人拍板
/// 「章节线要加上 boss 线，用 boss 线去丰富剧情」「三选一就是战前剧情之后出现的」）。
/// 主线正式关 Boss 关、章节线战前剧情之后弹：先两句 boss 线引子（BossStoryBeats，用图鉴外号），
/// 再三句「玩家的回答」选一句，选完才生成对应战意（本场加成），引导语按章收口外号。
/// 【2026-11-12 主人拍板】按钮只写三句「玩家的回答」，不写效果；按本章 boss 线换词。
/// 【2026-11-12 最新拍板「效果多些变化 / 循序也要变化」】战意池扩到 8 条，每场洗牌抽 3 条
/// （出的三条与排列都变），三句回答也洗牌 → 下标不再固定绑定战意，玩家每场都得试，
/// 「答案不尽如人意但效果可能是好的」由固定反差变成真随机。
/// 加成走 BattleManager 现成 run 修饰字段与 tempBuffs（石像赐福同款，主角+佣兵都吃）。
/// 🔴【2026-11-12 主人拍板「必须是单关的」】换关开场由 LoadStage 调 RevertBossStageBlessing
/// 撤销上一关战意（run 三件套回基线、tempBuffs 只按引用摘掉本关那一条），不写任何存档。
/// 复用 DialogueUI 选项链（与第一章石碑三选一同一条 UI），60 秒无输入看门狗自动选第一项放行，
/// 绝不把 Boss 关卡死在进场。
/// </summary>
public partial class BattleManager
{
    /// <summary>单条 Boss 关加成：展示文案 + 应用动作。只允许用现成 run 修饰字段/安全操作，不碰伤害公式。</summary>
    struct BossBlessing
    {
        public string Label;
        public Action<BattleManager> Apply;
    }

    /// <summary>
    /// Boss 关战意池（8 条）。【2026-11-12 最新拍板「效果多些变化 / 循序也要变化」】
    /// 每场 Fisher-Yates 洗牌取前 3 条：出的三条不固定、排列也不固定；三句回答同步洗牌，
    /// 所以「哪句回答 → 哪条战意」每场都不同（原「固定一一对应」口径作废）。
    /// Label 只用于选完的 toast 落账，不上按钮（按钮是三句回答）。
    /// 属性类走 ApplyTeamTempBuff（tempBuffs，主角+佣兵都吃）。【2026-11-12 主人「用以前的那 7 种，不要加新的」】
    /// 属性一律取 BuffHudUI 已登记 buff 图的 7 种（物攻/魔攻/生命/防御/暴击/攻速/移速），不引入新属性、不新增图标。
    /// 【2026-11-12】「回魂：立即回满血」撤下：Boss 关进场本就满血，效果玩家无感；
    /// HealAllAlliesToFull() 保留备用（当前无调用，见文件末注释）。
    /// </summary>
    static readonly BossBlessing[] BossBlessingPool =
    {
        new BossBlessing { Label = "迟滞：怪物攻速 -10%",    Apply = bm => bm.runMonsterAtkSpeedMul = 0.9f },
        new BossBlessing { Label = "悬赏：本关金币收益 +25%", Apply = bm => bm.runGoldGainMul = 1.25f },
        new BossBlessing { Label = "背水：我方受到伤害 -10%", Apply = bm => bm.runAllyDamageTakenMul = 0.9f },
        new BossBlessing { Label = "砺刃：我方攻击 +15%",      Apply = bm => bm.ApplyTeamTempBuff(PlayerJobBaseStats.CurrentAttackAttr(), 0.15f, true) },
        new BossBlessing { Label = "疾风：我方攻速 +15%",      Apply = bm => bm.ApplyTeamTempBuff(AttrType.AttackSpeed, 0.15f, true) },
        new BossBlessing { Label = "铁壁：我方防御 +20%",      Apply = bm => bm.ApplyTeamTempBuff(AttrType.Defense, 0.20f, true) },
        new BossBlessing { Label = "固本：我方生命 +20%",      Apply = bm => bm.ApplyTeamTempBuff(AttrType.MaxHp, 0.20f, true) },
        new BossBlessing { Label = "凝神：我方暴击 +8%",       Apply = bm => bm.ApplyTeamTempBuff(AttrType.CritRate, 0.08f, false) },
    };

    /// <summary>
    /// 本关 Boss 三选一加进 tempBuffs 的那一条（AttrBonusData 是 class，换关按引用精确摘除）。
    /// 只记这一条，绝不整份 tempBuffs.Clear() —— 石像赐福 / 局内强化卡是整局 buff，不能误删。
    /// </summary>
    AttrBonusData _bossStageTempBuff;

    /// <summary>Fisher-Yates 洗牌（每场战意组合 / 回答排列都靠它）。</summary>
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    /// <summary>
    /// 给主角 + 全体存活佣兵挂一条本局临时属性（石像赐福 BattleStatueEventUI:272 同款口径）：
    /// 同一份 tempBuffs 列表，主角 RecalcAttr、佣兵各自 RecalcAttr；阵亡的不重算（复活链会重新带上）。
    /// 属性类战意走这里，不碰伤害公式。
    /// </summary>
    void ApplyTeamTempBuff(AttrType attr, float value, bool isPercent)
    {
        var buff = new AttrBonusData { attrType = attr, value = value, isPercent = isPercent };
        tempBuffs.Add(buff);
        _bossStageTempBuff = buff;   // 记下本关这一条，换关时按引用撤销
        if (Hero.Instance != null) Hero.Instance.RecalcAttr();
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                var m = mercs[i];
                if (m == null || m.isDead || m.attr == null) continue;
                m.RecalcAttr();
            }
        }
        BattleUI.Instance?.ForceRefreshLiveBars();
    }

    /// <summary>
    /// 【2026-11-12 主人拍板「必须是单关的」】换关开场撤销上一关 Boss 三选一战意。
    /// 由 LoadStage（BattleManager.Waves.cs）调用，所有换关路径都走它，是唯一收口。
    /// run 三件套按 StartNewRun 同款基线重套（1f + ApplyChapter1RunModifiersFromSave），
    /// 不抹玩家第一章存档选边；tempBuffs 只按引用移除本关那一条，石像赐福 / 强化卡照留。
    /// 上一关结算在 LoadStage 之前已完成，撤销不会回追上一关的金币。
    /// </summary>
    void RevertBossStageBlessing()
    {
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        runAllyDamageTakenMul = 1f;
        ApplyChapter1RunModifiersFromSave();

        if (_bossStageTempBuff == null) return;
        // StartNewRun 可能已整份 Clear（新一局）：Remove 返回 false 就不必重算，只把引用清掉。
        if (tempBuffs.Remove(_bossStageTempBuff))
        {
            if (Hero.Instance != null) Hero.Instance.RecalcAttr();
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.isDead || m.attr == null) continue;
                    m.RecalcAttr();
                }
            }
            BattleUI.Instance?.ForceRefreshLiveBars();
        }
        _bossStageTempBuff = null;
    }

    /// <summary>是否弹 Boss 关三选一（门槛收口在这一处）。</summary>
    bool ShouldOfferBossBlessing()
    {
        if (!IsFormalMainRun) return false;                            // 副本/竞技场/裂隙/支线岛一律不弹
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return false; // 难度局不发加成
        if (Rules.Active || Rules.DirectorOwnsWaves) return false;     // 引导局有自己的节奏
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    /// <summary>
    /// 【2026-11-12 主人「战斗前加的，要给个 toast 告诉玩家加了什么属性」】
    /// 属性类战意从本关实际挂上的 _bossStageTempBuff 取属性名与数值生成文案，不写死在 Label 里 ——
    /// 提示与真正生效的加成永远一致（也杜绝「+0%」这类文案/实际对不上）；
    /// run 字段类（迟滞/悬赏/背水）不是属性，沿用原 Label。
    /// force=true 对齐石像赐福 BattleStatueEventUI:287，剧情窗/气泡还在时也不吞这条提示。
    /// </summary>
    void ShowBossBlessingToast(string label)
    {
        string msg;
        if (_bossStageTempBuff != null)
        {
            var a = _bossStageTempBuff;
            msg = "本关属性加成：" + EquipUiText.Attr(a.attrType) + " +"
                  + EquipUiText.Value(a.attrType, a.value, a.isPercent);
        }
        else
        {
            msg = "本关加成：" + label;
        }
        UIManager.Instance?.ShowToast(msg, true);
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

        // 【2026-11-12 最新拍板「效果多些变化 / 循序也要变化」】8 条池子洗牌取 3 条，
        // 组合与排列每场都不同；三句按章回答也洗牌，玩家不能靠位置背答案，每场都值得再试一次。
        var pool = new List<BossBlessing>(BossBlessingPool);
        Shuffle(pool);
        var picked = new[] { pool[0], pool[1], pool[2] };

        var answers = new List<string>(BossStoryBeats.GetBlessingAnswers(CurrentChapter));
        Shuffle(answers);

        // boss 线整段没挂背景图，剧情窗会直接浮在战斗画面上 → 铺一层常驻黑色半透底，
        // 排序 12999（Default 层最高）在剧情窗 Effects 层 13000 之下、不挡点击；选完 / 异常 / 超时统一撤掉。
        GameObject dimGo = BattleAmbienceBanner.ShowPersistent();

        bool done = false;
        try
        {
            var beats = new List<StoryBeat>();
            // 【2026-11-11 主人拍板「章节线要加上 boss 线」】两句按章的 boss 线引子先播，
            // 再进三选一；外号在引子里埋、在引导语里收。
            // 【2026-11-12】选项 = 三句玩家回答（同 Chapter1Story.PlayEnding 石碑三选一），
            // 按钮上不出现任何效果/数值；选完才按洗牌后的下标生成战意并 toast 落账。
            BossStoryBeats.AppendIntro(beats, CurrentChapter);
            beats.Add(
                // 与 Chapter1Story.PlayEnding 的选项 beat 同构（speaker=-1 + 左侧玩家 + choices）
                new StoryBeat
                {
                    leftName = "你",
                    leftPortraitId = StoryPortraits.Player,
                    speaker = -1,
                    text = BossStoryBeats.GetBlessingLead(CurrentChapter),
                    choices = answers.ToArray()
                });
            StoryDirector.Ensure().Play(beats, null, choice =>
            {
                try
                {
                    int idx = Mathf.Clamp(choice, 0, 2);
                    picked[idx].Apply(this);
                    ShowBossBlessingToast(picked[idx].Label);
                }
                finally
                {
                    // 半透底与 done 必须无论加成是否落地都收干净，不能把 Boss 关卡在暗屏里
                    BattleAmbienceBanner.HidePersistent(dimGo);
                    dimGo = null;
                    done = true;
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleManager] Boss 关三选一异常，跳过直接开打: " + e.Message);
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            BattleAmbienceBanner.HidePersistent(dimGo);
            dimGo = null;
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
            picked[0].Apply(this);
            ShowBossBlessingToast(picked[0].Label);
            BattleAmbienceBanner.HidePersistent(dimGo);
            dimGo = null;
        }
        // 【2026-11-11】不在此复位 _preBattleStoryPlaying：与上方置位配对，统一由 DoStartFight 解除，
        // 避免进关抽奖面板已提前开出时，硬性保险在 2.2s 后抢刷首波（Editor.log 42151 实证）。
    }

    /// <summary>
    /// 我方全体（主角+在队佣兵）回满血（原「回魂」加成用）。【2026-11-12】回魂撤下后
    /// 当前无调用，方法保留备用（不删现有代码）；阵亡单位不拉起。
    /// </summary>
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
