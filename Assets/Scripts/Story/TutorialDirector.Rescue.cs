using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Rescue.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：塔克天降救援拍（压场波 → 被围 → 抛物线入场 → 幻影一击全清）

public partial class TutorialDirector
{
    // ============================================================
    // 埋伏拍：撑不住 → 塔克天降一击清场（2026-10-07 主人拍板）
    // ============================================================

    /// <summary>天降那一击的伤害：直接判死，不走数值（演出用，不是战斗平衡）。</summary>
    const float TutorialRescueDamage = 999999f;

    /// <summary>天降起点：玩家左侧这么远 = 屏幕外（2026-10-10 主人拍板：塔克从左侧跳进来）。</summary>
    const float RescueEnterDist = 9.5f;

    /// <summary>
    /// 天降落点：玩家身边这么远（约一个身位）。
    /// 【2026-10-10 主人拍板】2.0 → 1.2：原来落得太远，看着像跳进怪堆里。
    /// 【2026-10-12 主人拍板】落点从「玩家右前方」改到<b>玩家同侧身边</b> ——
    /// 旧行为是越过玩家头顶落到他右前方，主人判定为「调到玩家对面」，明确要求「调到玩家身边」。
    /// </summary>
    const float RescueLandDist = 1.2f;

    /// <summary>清场挥击的攻击动画倍速（2026-10-10 主人拍板：动作加快，不要只是位移）。</summary>
    const float RescueAttackAnimSpeed = 3f;

    /// <summary>【2026-10-13 主人拍板】玩家「！」气泡停留时长（秒）。</summary>
    const float RescueExclaimHold = 0.7f;

    /// <summary>【2026-10-13 主人拍板】一道幻影的滑行时长（秒）：残影划过，真身同时落位。</summary>
    const float RescueGhostSlideDur = 0.25f;

    /// <summary>【2026-10-13 主人拍板】全场刀光齐出后到判死的停顿（秒）。</summary>
    const float RescueSlashHold = 0.15f;

    /// <summary>
    /// 等「玩家撑不住」这个时刻 —— 掉血跌破阈值 / 打够时长 / 场上清了，哪个先到算哪个。
    ///
    /// <para>现在由 12 只压场波（tutorial_battle.csv order=7）复用；原本服务的埋伏拍（order=3）已删除。</para>
    ///
    /// <para>为什么不等 <c>WaitFieldClear</c>：主人要的是「被围殴到喊救命」，
    /// 让玩家自己打完就没有天降的理由了（12 只纯近战是照这个意图配的）。</para>
    /// </summary>
    /// <para>【2026-10-12 起停用】压场波（order=7）改为「怪进场站定后直接弹剧情」，
    /// 不再等玩家掉血 / 超时 / 打空 —— 主人原话「出现一大波的时候不用打直接弹剧情」。
    /// 方法体保留：把调用点（原 :529 附近）加回来即可恢复旧行为。</para>
    /// <para>历史判定（停用前）：掉血跌破 50% / 撑满 15s / 场上打空，哪个先到算哪个。</para>
    IEnumerator CoWaitAmbushPressure(BattleManager bm)
    {
        // 【2026-10-09 主人拍板】这个等待原本服务于埋伏拍（order=3），埋伏拍删掉后由 12 只压场波（order=7）接手复用：
        // 判定条件不变（掉血跌破阈值 / 撑满时长 / 场上打空，哪个先到算哪个），
        // 目的还是「让玩家在被围殴的时候喊救命」，不是等他打完。
        // 所以这里等的是「玩家扛不住」的时刻，不是等清场：掉血阈值收紧、时长缩短，
        // 保证天降那一刻场上还有怪可杀（玩家真把怪清完了才走打空那条分支）。
        // 【2026-10-14 主人拍板】掉血阈值 50% → **20%**（主人原话「打的玩家血再少点，20%左右再弹剧情」）：
        // 打得更狠、掉得更多，才喊救命；1.0 表示满血，0.2 = 只剩两成血。
        // 【2026-10-16 主人拍板】20% → **40%**（主人原话「把玩家打到40%的血塔克出现」）——
        // 20% 太极限，玩家经常撑不到就超时/打空，塔克迟迟不来。
        const float maxWait = 15f;
        const float bailHpRatio = 0.4f;
        float t = 0f;
        while (t < maxWait)
        {
            if (bm == null) break;
            var hero = Hero.Instance;
            if (hero == null || hero.isDead) break;
            if (bm.GetAliveMonsterCount() <= 0) break;

            float max = hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : 0f;
            if (max > 0f && hero.currentHp / max <= bailHpRatio) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 【2026-10-07 主人拍板】埋伏拍的高潮：<see cref="StoryProgress.TutorialMercDisplayName"/>（塔克·重盾，稀有）
    /// 从屏幕外跳进来，落地一击（每个怪身上落一道闪电）把场上小怪全清掉。
    ///
    /// <para>⚠ 出场这位是<b>演出替身</b>：真正入队走下一拍的抽奖招募（<c>RunDraftDirector.RecruitMerc</c>），
    /// 所以调用方要在弹抽奖面板之前 <c>MercenaryManager.DespawnMercenary</c> 收掉他 —— 不然场上两个塔克。</para>
    ///
    /// <para>只改坐标做抛物线跳跃，不碰动画器、不改预制体（以主人预制体效果为准）。</para>
    /// </summary>
    IEnumerator CoTutorialRescueBeat(BattleManager bm, System.Action<Mercenary> onSpawned, BattleHeadTalkUI talk = null)
    {
        onSpawned?.Invoke(null);
        if (bm == null) yield break;

        // 【2026-10-12 主人拍板】aheadDist 传<b>负值</b>：让塔克直接生在跳跃起点（玩家左侧场外），
        // 而不是先落到玩家右侧 9.5 的「对面」再被瞬移回来。
        // 顺带解决相机：SpawnTutorialMercAt 内部会 ExtendCameraMaxX(pos.x + 6f)，
        // 旧写法会把相机 maxX 顶到 heroX+15.5（画面被往对面拉），改成起点后该值远小于当前上限、不再生效。
        var merc = bm.SpawnTutorialMercAt(null, 1f, -RescueEnterDist, stunned: false);
        if (merc == null)
        {
            Debug.LogError("[Tutorial] 塔克天降失败：SpawnTutorialMercAt 返回空（救场演出整段跳过，流程继续）");
            yield break;
        }
        onSpawned?.Invoke(merc);

        float heroX = Hero.Instance != null ? UnitBase.GetCombatX(Hero.Instance) : 0f;
        float z = bm.unitRoot != null ? bm.unitRoot.position.z : merc.transform.position.z;
        // 【2026-10-12 主人拍板】「从场外调到玩家身边就行，不要调到玩家对面」
        // → 起点仍是屏幕左侧场外，落点改到玩家<b>同侧</b>（heroX − RescueLandDist），
        //   不再越过玩家头顶飞到他右前方。
        float fromX = heroX - RescueEnterDist;
        float toX = heroX - RescueLandDist;

        // ① 抛物线跳进来（0.55 秒）：只改世界坐标
        const float jumpDur = 0.55f;
        const float jumpHeight = 2.6f;
        float jt = 0f;
        while (jt < jumpDur)
        {
            jt += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(jt / jumpDur);
            float x = Mathf.Lerp(fromX, toX, k);
            float y = UnitBase.GROUND_Y + Mathf.Sin(k * Mathf.PI) * jumpHeight;
            GameConfig.SetWorldPosition(merc.gameObject, new Vector3(x, y, z));
            yield return null;
        }
        GameConfig.SetWorldPosition(merc.gameObject, new Vector3(toX, UnitBase.GROUND_Y, z));

        // 【2026-10-12 主人拍板】「朝向和玩家一样」→ 取玩家当前朝向，不再写死 −1。
        // 旧版写死 −1 是为了「塔克落在右前方时回头面向玩家」；落点已改到玩家同侧，
        // 再回头就变成背对战场。跟玩家同向 = 并肩朝怪，而且 AI 自动寻敌也是同向，不会被翻回去。
        // （Hero.facingDir 是 public，1 = 右 / −1 = 左，见 UnitBase.cs:163）
        merc.Face(Hero.Instance != null ? Hero.Instance.facingDir : 1);

        // ②【2026-10-13 主人拍板再改】落地 → 「躲在我后面。」→ 玩家「！」→ 塔克一道幻影闪到最后一只怪身后
        //   （朝向不变）→ 全场同时刀光 → 全体一起死。
        //   主人原话：「不要让塔克一个一个打了 直接让他从场上的位置直接一道幻影移到最后一个怪的后面
        //   朝向不要变 然后玩家出现一个！瞬间所有的怪都出现刀光后死亡」。
        //   ⚠ 全程只改塔克自己的世界坐标，其它单位一概不动（2026-09-18 教训：绝不推着英雄走 / 瞬移）。
        //   ⚠ 判死三件套：不飘字（suppressDamageText，去掉那一大片紫色暴击大数字）/
        //     不出受击闪光（showHitVfx=false，刀光已经够亮）/ 不记暴击（isCrit=false）。
        //     正式关走常规 TakeDamage 路径，这三个开关默认值零影响。
        //   ⚠ 朝向：全程只有落地那一次 Face(玩家朝向)（2026-10-12 口径），此后一次都不翻 ——
        //     判死后到抽奖面板全程 UnitsCanAct=false，AI 在 Mercenary.cs:628 直接 return，
        //     不会被待机逻辑（FollowHeroOrIdle 的 facingDir=1）翻向玩家。
        //   第 7 步「躲在我后面。」就放在这里（落地之后、起手之前）：主人定稿的顺序是
        //   「天降 → 丢话 → 清场」，先护住玩家再动手才顺 —— 调用方那一段里不再重复这句。
        if (talk != null)
            yield return TalkBlock(bm, talk, restoreAct: false,
                // 这句是唯一的护崽台词源，想改措辞改这里即可
                new TalkLine(merc, "躲在我后面。", 2.2f));

        // 【2026-10-14 主人拍板】加一句把「他是不是把每个人都打了一遍」改成「一击全清」的落差感。
        // 措辞按主人原话的意图优化：短、有对比、不解释，留给玩家自己品。
        if (talk != null)
            yield return TalkBlock(bm, talk, restoreAct: false,
                new TalkLine(merc, "一个都别想跑。", 1.5f));

        var vfx = BattleVFXSystem.Instance;
        var victims = new List<UnitBase>();
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m != null && !m.isDead) victims.Add(m);
        }
        if (victims.Count == 0)
        {
            // 场上已经没怪（玩家自己清完了）：演出直接收尾，别硬演
            Debug.LogWarning("[Tutorial] 塔克清场演出：场上已无存活怪，跳过幻影/刀光");
            yield return new WaitForSecondsRealtime(0.3f);
            yield break;
        }

        // 「最后一个怪」= 世界 X 最大的存活怪；落点 = 其右侧 0.6（= 怪的身后，与精英宿敌拍同一定位口径）
        float lastX = float.NegativeInfinity;
        for (int i = 0; i < victims.Count; i++)
        {
            float x = UnitBase.GetCombatX(victims[i]);
            if (x > lastX) lastX = x;
        }
        float landX = lastX + 0.6f;

        // ③ 玩家头顶「！」（既有头顶气泡 BattleHeadTalkUI，单字没有打字机延迟）
        if (talk != null)
            yield return TalkBlock(bm, talk, restoreAct: false,
                new TalkLine(Hero.Instance, "！", RescueExclaimHold));

        // ④ 一道幻影滑到怪身后 + 真身同时落位（朝向不变，此后不再调 Face）
        TutorialRescueAfterimage.SlideGhost(merc, landX, z, RescueGhostSlideDur);
        GameConfig.SetWorldPosition(merc.gameObject, new Vector3(landX, UnitBase.GROUND_Y, z));
        yield return new WaitForSecondsRealtime(RescueGhostSlideDur);

        // ⑤ 瞬间：全场同时出刀光（Ally MeleeSlash；attacker=塔克 → 走翻转根挂载 + 排序盖住敌人）
        for (int i = 0; i < victims.Count; i++)
        {
            var m = victims[i];
            if (m == null || m.isDead) continue;
            // 刀光从怪面对的方向劈过来（与 UnitBase.cs:1526 受击朝向同口径）
            int slashDir = -m.GetVfxFacingDir();
            if (slashDir == 0) slashDir = 1;
            vfx?.PlaySlash(m.transform.position, slashDir, VfxFaction.Ally, null, merc);
        }
        yield return new WaitForSecondsRealtime(RescueSlashHold);

        // ⑥ 全体一起死（不飘字 / 不出受击闪光 / 不记暴击）
        for (int i = 0; i < victims.Count; i++)
        {
            var m = victims[i];
            if (m == null || m.isDead) continue;
            m.TakeDamage(TutorialRescueDamage, false, true, false, 0, merc, suppressDamageText: true);
        }
        Debug.Log($"[Tutorial] 塔克幻影清场：{victims.Count} 只（同时刀光，统一判死，无飘字）");

        yield return new WaitForSecondsRealtime(0.45f);
    }

    /// <summary>
    /// 塔尔克演出用的位移补间：把指定单位在 <paramref name="dur"/> 秒内从 <paramref name="fromX"/> 挪到
    /// <paramref name="toX"/>，逐帧 <c>yield return null</c>，<b>绝不瞬移</b>。
    /// 只改这一个自己的世界坐标 —— 2026-09-18 教训：引导里任何"把英雄推过去/挪过去"的写法都禁止。
    /// </summary>
    static IEnumerator CoMoveUnitTo(GameObject go, float fromX, float toX, float z, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            GameConfig.SetWorldPosition(go, new Vector3(Mathf.Lerp(fromX, toX, k), UnitBase.GROUND_Y, z));
            yield return null;
        }
        GameConfig.SetWorldPosition(go, new Vector3(toX, UnitBase.GROUND_Y, z));
    }
}
