using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新手引导：城镇开场 → 短战斗（选职 / 三段抽奖：装备 → 招募塔克 → 本命技能 / 强制撤离）→ 回城收尾。
/// 2026-10-06 主人拍板：引导佣兵在第 2 抽直接入队，原来第 4 拍「牧师被围 → 救援入队」整段删除改成普通波次。
/// 【2026-10-07 主人拍板】这位引导佣兵 = H003 塔克（重盾 / 剑盾卫士 / Rare），不再是 H011 路加（牧师）。
/// 【2026-10-08 补记】早前正文写的那位引导佣兵旧名已过时，与上述口径冲突，已统一为塔克。
/// 只编排节拍（停手、对白、提示、何时刷第 N 步）；刷怪/HP 走 BattleManager.QueueTutorialStep + tutorial_battle。
/// 正式第一章不走这里。
/// </summary>
public partial class TutorialDirector : Singleton<TutorialDirector>
{

    /// <summary>
    /// 引导局：引导佣兵（塔克，男）的头像栏要不要走「真人头像 + 职业 icon + 血条」（= 他已经在队里了）。
    /// <para>2026-10-06 主人拍板：<b>只读推导</b>，不再是一个可以随便写的 bool ——
    /// 原来散在四处赋值（175/428/568/660），跟「本局抽数」那套判据会对不上。
    /// 真源只有 <see cref="SlotMachineSystem.TutorialMercDrawn"/> 一处；
    /// 抽数每局归零 = 自动重置，不需要手动置 false/true。</para>
    /// </summary>
    public bool ShowMercHud => SlotMachineSystem.TutorialMercDrawn;
    public bool WaitingEvacuate { get; set; }
    public bool SkillUsedThisStep { get; private set; }
    /// <summary>引导战斗：仅 heal 步骤允许点头像放技能。</summary>
    public bool AllowBattleSkillClick { get; private set; }

    bool _townFlowBusy;
    bool _battleTutorialFlowStarted;
    bool _extraHintShown;
    Coroutine _flow;

    // 2026-09-22：改用 FindObjectOfType 直查，不走 Singleton<BattleManager>.Instance ——
    // BattleUI.Awake 早于 AutoGameInitializer 装配 GameRoot，此时 getter 会命中
    // ICombatBoundSingleton 分支，每次进战斗都刷一条
    // "[Singleton] 战斗必需系统未挂载，拒绝自动创建空对象: BattleManager"。
    // 这里只是探测"当前是不是教学战"，拿不到就当 false，不该报错也不该建空物体。
    public static bool IsTutorialBattle
    {
        get
        {
            var bm = FindObjectOfType<BattleManager>();
            return bm != null && bm.IsTutorialRun;
        }
    }

    /// <summary>
    /// 首次强制软引导（FirstRunGuide）是否还在负责这一步。
    /// 负责时旧的「弱提示 / 硬提示」一律让位，不然同一个位置会叠两层指引
    /// （2026-10-15 主人报「上面一个引导 下面一个引导」后，PlayerJobSelectUI 的硬提示也改挂这个闸门）。
    /// </summary>
    public static bool GuideCovers(FirstRunGuide.GuideStep step)
    {
        var guide = FirstRunGuide.Instance;
        return guide != null && !guide.IsFinished && !guide.IsStepDone(step);
    }
}
