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
public class TutorialDirector : Singleton<TutorialDirector>
{
    const string OpeningIntroRelativePath = "Art/Video/opening_intro.mp4";
    /// <summary>StreamingAssets 里的片头文件名（真机播放入口，见 ResolveOpeningIntroPath）。</summary>
    const string OpeningIntroStreamingName = "opening_intro.mp4";

    /// <summary>
    /// 2026-09-28 修复真机不播片头：原逻辑用 Application.dataPath + File.Exists，
    /// Android 上 dataPath 指向 base.apk，File.Exists 必失败 → 片头被静默跳过（编辑器正常，真机没了）。
    /// 真机改走 StreamingAssets（VideoPlayer 可直接播 jar:// URL）；编辑器仍回退工程内 Assets/Art/Video。
    /// 另注意：片头只在 StoryProgress.OpeningIntroPlayed==false 时播一次，老存档本就不会再播。
    /// </summary>
    static string ResolveOpeningIntroPath()
    {
        if (!Application.isEditor)
            return Path.Combine(Application.streamingAssetsPath, OpeningIntroStreamingName);
        return Path.Combine(Application.dataPath, OpeningIntroRelativePath);
    }

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




    /// <summary>P3 用：本段是否已经观察到玩家技能真的放出去了。</summary>
    bool _sawPlayerSkillCast;

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

    public void NotifyTownReady()
    {
        if (_townFlowBusy) return;

        // 首次启动的「分步强制软引导」起嗓子：城镇一就绪就从上次落盘的那一步继续
        //（自身负责高亮圈 + 半透明遮罩 + 气泡；已完成 / 已跳过则什么也不做）
        FirstRunGuide.NotifyTownReady();

        // 【2026-10-15 主人拍板】主界面闲置软引导起嗓子：玩家在大厅停留超过阈值 →
        // 弹一条「没点过的底栏入口」软引导，真实点过一次该入口就永久关闭（TownIdleNudge 文档）。
        // 强引导没走完时它整条让位，不会和上面这层打架。
        TownIdleNudge.Ensure();

        if (StoryProgress.TutorialOutroPending && StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying)
            StoryDirector.Instance.StopPlaying();
        else if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying)
            return;

        if (!GuildHallUI.ShouldHideTownForIntro)
            ClearTownBlockers();

        Debug.Log($"[Tutorial] TownReady tutorialDone={StoryProgress.TutorialDone} intro={StoryProgress.TutorialIntroDone} battle={StoryProgress.TutorialBattleCleared} outro={StoryProgress.TutorialOutroPending}");

        if (!StoryProgress.TutorialDone && StoryProgress.TutorialOutroPending)
        {
            _townFlowBusy = true;
            _flow = StartCoroutine(TownAfterBattleRoutine());
            return;
        }

        if (!StoryProgress.TutorialDone && !StoryProgress.TutorialIntroDone)
        {
            _townFlowBusy = true;
            _flow = StartCoroutine(TownFirstEntryRoutine());
            return;
        }

        // intro 已写入存档但教学战未打（上次半路退出/重复 Notify）：补「点冒险」并清遮挡
        if (!StoryProgress.TutorialDone && StoryProgress.TutorialIntroDone && !StoryProgress.TutorialBattleCleared)
        {
            _townFlowBusy = true;
            _flow = StartCoroutine(TownResumeAdventureHintRoutine());
            return;
        }

        if (StoryProgress.ConsumeChapter1TownReturn())
        {
            Chapter1Story.PlayReturnTown(null);
        }
    }

    /// <summary>撤掉进城镇后可能残留的遮挡（黑幕 / 对话窗 / 引导遮罩）。</summary>
    public static void ClearTownBlockers()
    {
        TownIntroVeil.ForceDestroy();
        GuildHallUI.SetTownChromeVisible(true);
        if (StoryDirector.Instance == null || !StoryDirector.Instance.IsPlaying)
            DialogueUI.Instance?.Hide();
        TutorialHintUI.Ensure().Hide();
        OfflineRewardPopup.HideIfOpen();
    }

    public void NotifyTownTab(MainNavTab tab)
    {
        // 任意非冒险 Tab：先收引导（战后 MarkTutorialDone 后 guard 会挡住下面的 Hide）
        if (tab != MainNavTab.Adventure)
            TutorialHintUI.Ensure().Hide();

        if (StoryProgress.TutorialDone || StoryProgress.TutorialBattleCleared) return;
        if (tab != MainNavTab.Tavern && tab != MainNavTab.Character) return;
        if (_extraHintShown) return;
        _extraHintShown = true;
        var beat = StoryDirector.Solo("咨询台小姐",
            "等回来再逛这些也来得及。",
            StoryPortraits.Receptionist)
            .Bg(StoryBackgrounds.GuildHall);
        StoryDirector.Ensure().PlayOne(beat, null);
    }

    public void NotifyAdventureOpened()
    {
        if (StoryProgress.TutorialDone || StoryProgress.TutorialBattleCleared) return;
        if (!StoryProgress.TutorialIntroDone) return;

        var adv = AdventureUI.Instance;
        if (adv == null) return;

        // 引导：锁定第一章普通难度，提示点开战
        adv.ForceSelectChapterForTutorial(1, 0);

        RectTransform highlight = null;
        if (adv.startBtn != null)
            highlight = adv.startBtn.GetComponent<RectTransform>();
        // 「开始冒险」交给首次强制软引导（FirstRunGuide.FirstBattle）统一呈现；
        // 那一步已经走过/跳过时才退回原来的硬提示，避免两层指引同时出现打架。
        if (!GuideCovers(FirstRunGuide.GuideStep.FirstBattle))
            TutorialHintUI.Ensure().ShowHard("点下方「开始冒险」，选择职业后进入裂隙。", highlight);
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

    /// <summary>引导战重进时重置运行时状态（背包另由 StoryProgress 清）。</summary>
    public void ResetBattleTutorialRuntime()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }
        _battleTutorialFlowStarted = false;
        // 2026-10-06 主人拍板：ShowMercHud 改成只读推导（SlotMachineSystem.TutorialMercDrawn），
        // 这里的赋值删掉 —— 抽数每局归零，等于自动重置。
        WaitingEvacuate = false;
        SkillUsedThisStep = false;
        AllowBattleSkillClick = false;
        _sawPlayerSkillCast = false;
    }

    /// <summary>已废弃：引导改为打开冒险页再开战，不再从底栏直进战斗。</summary>
    public bool TryEnterTutorialBattleFromNav() => false;

    public void NotifyBattleSplashFinished()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.IsTutorialRun) return;
        // 过场若重复回调，不要把进行中的引导协程掐断（会留下气泡/卡流程）
        if (_battleTutorialFlowStarted && _flow != null) return;
        if (_flow != null) StopCoroutine(_flow);
        _battleTutorialFlowStarted = true;
        _flow = StartCoroutine(BattleRoutine());
    }

    public void NotifyPlayerSkillUsed()
    {
        SkillUsedThisStep = true;
        _sawPlayerSkillCast = true;
    }

    IEnumerator TownFirstEntryRoutine()
    {
        yield return WaitNotLoading();
        TownIntroVeil.EnsureShown();
        GuildHallUI.SetTownChromeVisible(false);

        if (!StoryProgress.OpeningIntroPlayed)
        {
            yield return PlayOpeningIntroIfNeeded();
            StoryProgress.MarkOpeningIntroPlayed();
            TownIntroVeil.EnsureShown();
            GuildHallUI.SetTownChromeVisible(false);
        }

        Button adv = null;
        float bind = 0f;
        while (bind < 3f)
        {
            adv = ResolveAdventureButton();
            if (adv != null) break;
            bind += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        yield return PlayTownIntroDialogue();
        GameBgm.Play(GameBgm.Track.Town);
        StoryProgress.MarkTutorialIntroDone();
        GuildHallUI.SetTownChromeVisible(true);
        if (adv == null) adv = ResolveAdventureButton();
        if (adv != null) adv.interactable = true;
        yield return null;
        yield return DismissIntroVeil(0.4f);
        DialogueUI.Instance?.Hide();
        ClearTownBlockers();

        if (adv == null) adv = ResolveAdventureButton();
        if (!GuideCovers(FirstRunGuide.GuideStep.Adventure))
            TutorialHintUI.Ensure().ShowHard("点下方「冒险」，前往裂隙。",
                adv != null ? adv.GetComponent<RectTransform>() : null);
        _townFlowBusy = false;
        _flow = null;
    }

    static IEnumerator DismissIntroVeil(float fadeSeconds)
    {
        if (TownIntroVeil.Instance == null) yield break;
        yield return TownIntroVeil.FadeOutRoutine(fadeSeconds);
        TownIntroVeil.ForceDestroy();
    }

    IEnumerator TownResumeAdventureHintRoutine()
    {
        yield return WaitNotLoading();
        ClearTownBlockers();

        Button adv = null;
        float bind = 0f;
        while (bind < 3f)
        {
            adv = ResolveAdventureButton();
            if (adv != null) break;
            bind += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        if (!GuideCovers(FirstRunGuide.GuideStep.Adventure))
            TutorialHintUI.Ensure().ShowHard("点下方「冒险」，前往裂隙。",
                adv != null ? adv.GetComponent<RectTransform>() : null);
        _townFlowBusy = false;
        _flow = null;
    }

    IEnumerator PlayTownIntroDialogue()
    {
        StoryDirector.Instance?.NotifySceneChanged();
        GameBgm.Play(GameBgm.Track.Intro);

        StoryAssetLoader.Warmup(StoryAssetLoader.Props, StoryProps.QuestPaper);
        StoryPortraits.Warmup(
            StoryPortraits.GuildMaster, StoryPortraits.Receptionist, StoryPortraits.Player);
        StoryAssetLoader.Warmup(StoryAssetLoader.Backgrounds,
            StoryBackgrounds.GuildOffice, StoryBackgrounds.GuildHall);
        DialogueUI.Instance?.PrepareForStoryBeat();

        // 办公室：会长对话 → 签名起名 → 咨询台
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("会长",
                "新人，森林层那帮怪物最近闹得凶。下去走一趟，能不能留下，看你自己的本事。",
                StoryPortraits.GuildMaster)
                .Bg(StoryBackgrounds.GuildOffice),
            StoryDirector.Solo("会长",
                "你把这个签了后就可以出去了。",
                StoryPortraits.GuildMaster)
                .Bg(StoryBackgrounds.GuildOffice),
            StoryDirector.Narration("桌上摊着那份委托书还没有署名，会长催促着赶紧签了就可以出去了。")
                .Prop(StoryProps.QuestPaper),
        };
        bool done = false;
        StoryDirector.Ensure().Play(beats, () => done = true, keepSceneArt: true);
        while (!done) yield return null;

        if (!StoryProgress.HasPlayerName())
        {
            bool named = false;
            PlayerNamingUI.Show(() => named = true);
            while (!named) yield return null;
        }

        // 2026-09-28 主人要求：起名后会长补一句送客台词
        {
            string playerName = StoryProgress.GetPlayerName();
            var sendOff = new List<StoryBeat>
            {
                StoryDirector.Solo("会长",
                    $"{playerName}，你可以出去了。往后的路，自己当心。",
                    StoryPortraits.GuildMaster)
                    .Bg(StoryBackgrounds.GuildOffice),
            };
            done = false;
            StoryDirector.Ensure().Play(sendOff, () => done = true, keepSceneArt: true);
            while (!done) yield return null;
        }

        var afterNaming = new List<StoryBeat>
        {
            // 【2026-10-12 主人拍板】第一句带玩家名字（与会长送别句同一口径，起名后才播这段）
            StoryDirector.Solo("咨询台小姐",
                $"{StoryProgress.GetPlayerName()}，第一次下裂隙？三件事：\n1. 你只管走路，打架会自动打。\n2. 裂隙很危险，尽量组队进入。\n3. 见好就收，活着才有收益。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall)
        };
        done = false;
        StoryDirector.Ensure().Play(afterNaming, () => done = true, keepSceneArt: true);
        while (!done) yield return null;
    }

    IEnumerator PlayOpeningIntroIfNeeded()
    {
        string fullPath = ResolveOpeningIntroPath();
        var overlay = OpeningIntroOverlay.Show(fullPath);
        if (overlay == null)
        {
            GameBgm.UnmuteAfterCutscene();
            yield break;
        }
        while (overlay != null && !overlay.IsFinished)
            yield return null;
    }

    IEnumerator TownAfterBattleRoutine()
    {
        _townFlowBusy = true;
        yield return null;
        yield return WaitNotLoading();

        ClearTownBlockers();
        GuildHallUI.SetTownChromeVisible(true);

        // 收尾对话必须在公会主界面，不要叠在冒险页上
        TownHubController.PendingOpenAdventure = false;
        TownHubController.Instance?.OpenGuild();

        // 收尾对话要用的立绘/背景先读进缓存，否则开场会卡一下才出画面
        StoryAssetLoader.Warmup(StoryAssetLoader.Backgrounds, StoryBackgrounds.GuildHall);
        yield return null;
        StoryPortraits.Warmup(
            StoryPortraits.Player, StoryProgress.TutorialMercHireId, StoryPortraits.Receptionist);
        yield return null;

        // 【2026-10-14 主人拍板】「局内装备带不出裂隙」这条引导字挪到**塔克剧情之后**——
        // 主人原话「回城后立即播放塔克剧情 不要先弹引导再弹塔克剧情」（原位置在剧情前，抢了戏）。

        yield return new WaitForSecondsRealtime(2.2f);

        bool done = false;
        DialogueUI.Instance?.PrepareForStoryBeat();
        // 【2026-10-07 主人拍板】引导救场那位 = 每日登录送的塔克（H003 剑盾卫士）。
        // 立绘走花名册 hireId（H003），禁止再用 LaoDun(H001 盾兵) 顶替。
        string rescuerId = StoryProgress.TutorialMercHireId;
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            // 只有塔克一个人说话：不要摆玩家立绘，直接让他单人居中说
            //（约定：单人说台词一律用 Solo，别摆两个立绘站着不说话）
            // 台词把钩子3串起来：①「成功撤离」正反馈 ②「带回奖励」获得感 ③「我等你回来」次日留钩子
            // ④「每日签到别落下」指向紧接着弹的每日登录弹窗。
            // 【2026-10-10 主人拍板】原来 41 字一口气一句，对话框里挤成一坨、后半句看不清（主人问
            //「每日签到别落下那有我的 后面是什么」）—— 拆成两拍，签到钩子单独成句。
            // 【2026-10-16 主人拍板】原句「成功撤离！带回来的奖励够你歇口气。明儿见——我等你回来。」
            // 作废，改成塔克交代去向：他还要去找其他线索，让玩家有事去酒馆找他（指向酒馆招募佣兵入口）。
            StoryDirector.Solo(StoryProgress.TutorialMercDisplayName,
                "我还要去找其他线索，有事可以去酒馆找我。",
                rescuerId)
                .Bg(StoryBackgrounds.GuildHall)
                .SkipReveal()
        }, () => done = true);
        while (!done) yield return null;

        done = false;
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            StoryDirector.Solo(StoryProgress.TutorialMercDisplayName,
                // 【2026-10-10 主人拍板】最后一句改成「咱们后会有期」（原「每日签到别落下——那儿有我的份」作废）
                "咱们后会有期！",
                rescuerId)
                .Bg(StoryBackgrounds.GuildHall)
                .SkipReveal()
        }, () => done = true);
        while (!done) yield return null;

        done = false;
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            StoryDirector.Solo("咨询台小姐",
                "回来了？人物界面可以查看属性，酒馆能招募佣兵。\n先熟悉下公会大厅，之后再慢慢变强。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall)
                .SkipReveal()
        }, () => done = true);
        while (!done) yield return null;

        // 【2026-10-14 主人拍板】塔克/咨询台剧情播完才弹这条机制提示（原来抢在剧情前面）
        TutorialHintUI.Ensure().Show("局内拿到的装备带不出裂隙，金币和材料都能带回去。", null, 6f);

        // —— V6 P4：回城引导点一次天赋（金币不够会自己跳过）——
        yield return CoGuideTalentUpgrade();

        var nav = MainBottomNav.Instance;
        RectTransform highlight = null;
        if (nav != null && nav.characterButton != null)
            highlight = nav.characterButton.GetComponent<RectTransform>();
        // 软引导：指 BottomNav 角色入口。引导还在管这一步时让位，别两层一起指同一个按钮。
        if (!GuideCovers(FirstRunGuide.GuideStep.Talent))
        {
            // 【2026-10-16 主人拍板】CoGuideTalentUpgrade 走完时玩家刚在天赋面板里点完节点，
            // 面板还开着——先只收天赋面板（角色页不动），让 BottomNav 角色按钮露出来再发这条软引导；
            // 否则面板+Dim 盖住底栏，提示指个看不见的按钮（主人实测「被天赋界面挡住」）。
            var talentUi = TalentUI.Instance;
            if (talentUi != null && talentUi.IsOpen) talentUi.Hide();
            TutorialHintUI.Ensure().Show("可以去角色界面查看属性",
                highlight, 12f);
        }

        StoryProgress.MarkTutorialDone();
        // 2026-09-29 主人拍板：引导一结束就把塔克清掉，不让他跟到正式关卡。
        // 引导关撤离走的是 SkipMercHireClearOnEvacuate=true（不清雇佣），所以必须在这里主动清。
        ClearTutorialMerc();

        // 【2026-10-07 主人拍板】引导收尾 → 立刻弹每日登录，把「刚才救场的塔克」和
        //「签到才能领到塔克」串成一条线（主人原话：回城剧情完了后出现每日登录弹窗，这样能连起来）。
        // 入口只有一个 TownSceneBootstrap.TryDailyLoginOnce：进 Town 本来也走它，
        // 只是那时 StoryProgress.TutorialDone 还是 false 会跳过，这里 MarkTutorialDone 之后补一次。
        TownSceneBootstrap.TryDailyLoginOnce();

        _townFlowBusy = false;
        _flow = null;
    }

    IEnumerator BattleRoutine()
    {
        var bm = BattleManager.Instance;
        var hint = TutorialHintUI.Ensure();
        var ui = BattleUI.Instance;
        var headTalk = BattleHeadTalkUI.Ensure();
        SkillUsedThisStep = false;
        AllowBattleSkillClick = false;
        WaitingEvacuate = false;

        // —— 0) 第一波进场 + 自动技能（选职已在冒险页完成）——
        // 【2026-10-09】摇杆已于 2026-10-05 整条链路停用（BattleJoystick 永不创建，见 BattleUI.Backpack.cs:386），
        //   这里只剩「刷第一波 + 借 3.2 秒读字时间让怪走进场」，不再有教走路这一步。
        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
        // 【2026-10-12 主人拍板】「先打一波」这句**提到玩家一出场就播** ——
        //   主人原话「先打一波那个引导在玩家一出来就播」。
        //   原位置在 CoTeachControls 之后（刷怪 + 3.2 秒进场走完才弹），玩家已经在场上站了 3 秒多才看到，
        //   现在移到刷怪之前，出场即见；怪照常在下面那 3.2 秒里走上来。
        hint.Show("先打一波热热身。靠近怪物会自动攻击。", null, 8f);
        yield return CoTeachControls(bm, hint, ui, preloadStep: 1);

        // —— 1) 首波清场后进入宝箱剧情（不再刷第二小波）——
        // 2026-09-27 主人反馈「引导到自动攻击时怪还没出来」→ **先刷怪、再弹提示**。
        // 2026-09-28 再提前：第 1 波已在 CoTeachControls 里刷（preloadStep:1），
        // 借「技能能量满会自动释放」那 3.2 秒读字时间走完进场，这里不再重复刷
        // （EnsureTutorialStep 不看是否已刷过，重复调用会多排一波）。
        TutorialBattleTable.EnsureLoaded();
        // 【2026-10-08 主人拍板】第一波那句「热身」引导已于 2026-10-12 上移到 BattleRoutine 开场
        //   （玩家一出场就播，见上方 hint.Show）。文案唯一源仍是那一处，想改措辞改那里即可。
        if (bm != null) bm.UnitsCanAct = true;
        yield return WaitFieldClear();

        yield return WaitFieldClear(strict: true);
        if (bm != null && bm.GetAliveMonsterCount() > 0)
        {
            hint.Show("先把剩下的怪清掉。", null, 3f);
            yield return WaitFieldClear(strict: true);
        }

        // —— 1c) 第一抽：走正式入口 CoMidBattleDraft（冻场 → 弹面板 → 抽一次 → 解冻）。
        // 抽奖是主玩法，不是引导脚本 —— 这里的节拍只是「什么时候弹」，规则在 SlotMachineSystem。
        // 【2026-10-07 主人拍板】文案不剧透类别（主人原话「不要说这次抽的是佣兵，要给玩家惊喜」），
        // 三拍统一用 TutorialDrawBeatText —— 类别按 SlotMachineDefs.TutorialDrawOrder 定序。
        // 引导三拍的钱**只有开局那 240**（每拍一抽 80，正好三抽）；不再有任何中途补贴 ——
        // 2026-10-05 主人拍板「清零 + 不要总打补丁」，TUTORIAL_WAVE_BONUS_COINS 整条链路已删。
        //
        // 【2026-10-08 主人拍板】这一拍的<b>位置</b>：原来排在「打完两波（order=1 + order=2）」之后，
        //   玩家还没拿过装备、也还没体会过换装前后的差别就抽了 —— 看不出「换了装备」。
        //   现在<b>前移到第一波（order=1）清场之后</b>：先用初始武器打完一波 → 抽到装备换上 →
        //   紧接着 order=2 混编波，换装带来的变强立刻看得见。拍数不变（仍是三抽）。
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        // 2026-10-06 主人拍板：招募完立刻刷一次头像栏，否则要等下一次全量刷新才亮出来
        // （ShowMercHud 由本局抽数推导，抽数已在 CoInstantPick 里 +1）。
        ui?.UpdateCharacterSlots();

        // —— 1d) 【2026-10-08 主人拍板】抽中武器以为能大展身手，结果十几只小怪压上来 ——
        // 主人原话：「抽中一个武器 以为大展身手的时候到了 结果来了十几个小怪
        //   正在玩家感觉要死了的时候 佣兵闪亮登场」。
        // 这一波（tutorial_battle.csv order=7）只给**数量**压迫：12 只、单体血 20~24
        // 比主流波 38~40 低，换装后一刀一只也清不完一轮，被围住自然就「要死了」。
        yield return EnsureTutorialStep(bm, 7);
        // 【2026-10-09 主人拍板】这里**不等清场** —— 等的是「被围住快撑不住」这个时刻：
        // 主人要的节奏是「十几只压上来 → 玩家觉得要死了 → 这时候塔克才登场」，
        // 真等玩家把 12 只都打完（WaitFieldClear），塔克就变成「清完场才姗姗来迟」，整段演出废掉。
        // ① 先等 12 只全部进场站定（波次是异步进场的，不能怪还没露面就开始吐槽）；
        yield return WaitMonstersFinishedEnter(bm);
        // ② 【2026-10-14 主人拍板·恢复压力等待】「让玩家的血再掉少点再出现剧情」（主人原话
        //    「不是打的太狠 是打的再狠点」）—— 怪围殴把玩家血打到 **20% 左右**才进躺尸剧情，
        //    「要死了」的感觉才够。10-12 曾拍板「不用打直接弹剧情」作废；
        //    阈值/超时真源在 CoWaitAmbushPressure（50% → 20% 那一版），要调只调那里。
        yield return CoWaitAmbushPressure(bm);

        // —— 1e)【2026-10-09 主人拍板】第 4~9 步：十几只压上来 → 玩家躺平 → 塔克天降一趟清场 → 玩家看傻 ——
        // 主人 10-08 口述：抽到武器以为能大展身手，结果十几只压上来；正当玩家觉得要死了的时候，佣兵闪亮登场。
        // 这一段 Hero **只被冻结，绝不改写坐标**（只能等，不能推着英雄走 / 瞬移）。
        // 【2026-10-09 主人拍板删除】压场波（order=7）后面原本还接着一条 order=2 混编波拍，
        //   主人定稿的 14 步里这里是直接接塔克天降戏，中间没有「再打一波」的位置 —— 整段删掉；
        //   正常波由后面的 order=5 那一段负责。
        hint.Hide();
        if (bm != null)
        {
            bm.UnitsCanAct = false;
            HaltUnit(Hero.Instance);
        }
        // 第 4 步：吐槽一句（搞笑档：下面这两句是唯一文案源，想改措辞改这里即可）
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "我才刚握上新装备……这就结束了？", 2.4f),
            // 第 5 步：躺尸 —— Hero 没有倒地 / 躺下动画（只有死亡动画，不能用），
            // 所以这一步只用「冻结 + 自暴自弃的台词 + 停顿」表达放弃挣扎，不变动画、不碰 transform。
            new TalkLine(Hero.Instance, "算了，我躺一会儿——你们随意。", 2.4f));
        headTalk?.HideNow();
        yield return new WaitForSecondsRealtime(0.5f);

        // 第 6~8 步：塔克天降 → 丢下「躲在我后面。」→ 一趟跑完全场，直接停在怪的身后，全体一起死。
        // 这段之前必须把场上所有怪也冻住，否则它们会在塔克跑动时追着玩家改写坐标。
        if (bm != null && bm.monsters != null)
        {
            for (int i = 0; i < bm.monsters.Count; i++)
                HaltUnit(bm.monsters[i]);
        }
        Mercenary rescuer = null;
        // CoTutorialRescueBeat 内部：① 抛物线跳进来（原样保留）② 落地丢一句「躲在我后面。」
        // ③ 逛一圈扫完直接统一判死（2026-10-12 起不再回原位，见方法内注释）。
        // ⚠ 第 7 步「躲在我后面。」放在清场**之前**（写在 CoTutorialRescueBeat 里、落地之后、起跑之前）：
        //   主人定稿的顺序是「天降 → 丢话 → 清场」，先护住玩家再动手才顺 —— 所以这里**不再**重复说第二遍。
        yield return CoTutorialRescueBeat(bm, m => rescuer = m, headTalk);

        // 第 9 步：玩家惊呆（这一句是唯一文案源，想改措辞改这里即可）
        // 【2026-10-14 主人补拍板】演出已改成「幻影一击全清」（不再挨个打），旧词
        //「他刚才是不是把所有人打了一遍？」与画面矛盾 —— 换成没看清出手的落差感。
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "……他什么时候出的手？", 2.4f));
        headTalk?.HideNow();

        // —— 3b) 第二抽：抽中的就是刚才救场那位（塔克·重盾，稀有） ——
        // 【2026-10-07 主人拍板】天降那位 = 这一抽开出来的佣兵，抽完他正式入队。
        // ① 两波后（装备）→ ② 这里（按定序出佣兵）→ ③ 精英波清完（技能）。
        // 保底卡在 SlotMachineSystem.BuildGuaranteedMercCard（碎片那条口径已作废）。
        hint.Hide();
        // 【2026-10-10 主人拍板】天降替身**不再收掉** —— 主人原话口径「第二次抽奖时塔克消失了但名字还在，
        // 不要让塔克消失」。现在替身原地保留，第二抽招募时由 RunDraftDirector.SpawnRunMerc 把他**直接转正**
        //（复用同一个实例重绑属性），全程塔克不消失、也不会出现两个塔克。
        // if (rescuer != null) MercenaryManager.Instance?.DespawnMercenary(rescuer);
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        // 【2026-10-14 主人报「第二次抽奖继续后玩家和佣兵都不动，按说应该继续往前走才对」】
        // 根因：救援戏（本方法上方 :538-542）把 UnitsCanAct 冻在 false 后再没恢复；第二抽的
        // CoMidBattleDraft finally 还原的是它进入时的 false → 点完「继续」人/佣兵/怪全冻着，
        // 只能干等 WaitMonstersFinishedEnter 10s 超时（Editor.log:73601「等待怪进场超时」实锤）。
        // 与第三抽后的既有处理（本方法下方 CoMidBattleDraft 之后的同款一行）一致：抽完立刻恢复战斗。
        if (bm != null) bm.UnitsCanAct = true;
        ui?.UpdateCharacterSlots();

        // —— 3c)【2026-10-15 主人拍板】引导新一步：点佣兵头像看新伙伴 ——
        // 获得佣兵弹窗已不随抽奖默认弹出（改点头像弹，见 BattleUI.OnMercAvatarClicked），
        // 这里高亮佣兵头像教玩家点一次：点了弹详情，关掉后继续；60s 没点也放行不卡流程。
        yield return CoWaitMercAvatarTap(hint, ui);
        hint.Hide();

        // —— 4) 第三波：塔克已在队，跟着一起打 ——
        // 2026-10-07 主人拍板：塔克第 2 抽就招募入队了，原来这段
        //「牧师在前方眩晕被围殴 → 清场 → 三段对白 → 入队」**整段删除**，第 4 拍改成普通波次。
        hint.Hide();
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        // 塔克已经在队里了：这里只从佣兵管理器取场上那个单位（精英宿敌对白 / 压轴 / 撤离要用）
        var activeMercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        // 【2026-10-15 主人拍板】「跟紧我，别走散」必须**塔克**说：按引导佣兵 hireId（H003）
        // 从在场佣兵里点名，不再按槽位 [0] 取（防以后在场顺序变化把台词安到别人头上）。
        // 找不到 hireId 精确命中时退第一只（引导局在场的本来就是塔克）；一只都没有 → 报出来，台词由 TalkBlock 跳过。
        var merc = (Mercenary)null;
        if (activeMercs != null)
        {
            string tutHireId = StoryProgress.TutorialMercHireId;
            for (int i = 0; i < activeMercs.Count; i++)
            {
                var m0 = activeMercs[i];
                if (m0 == null) continue;
                if (merc == null) merc = m0;
                if (!string.IsNullOrEmpty(tutHireId) && m0.hireId == tutHireId) { merc = m0; break; }
            }
        }
        if (merc == null)
            Debug.LogError("[Tutorial] 「跟紧我，别走散」找不到在场塔克（引导佣兵没在场），台词会被跳过");

        // 【2026-10-07 主人拍板「数据表最好不要为了引导改动」】
        // 这一拍直接用表里现成的 **order=5 普通波**（4 只，含 1 远程），一个数字都不改；
        // 表上含 1 精英的 order=4 留给后面的宿敌戏（见下）。
        bm?.QueueTutorialStep(5);

        // 【2026-10-12 主人报「第三波还是看不到敌人就弹对话框」】先等这波怪进场站定再弹提示
        // （波次异步入场，原来 Queue 完立刻 hint.Show，玩家看到对话框时场上还没怪）。
        yield return WaitMonstersFinishedEnter(bm);

        // 【2026-10-14 主人拍板·方案 A】「跟紧我，别走散。」从「整波清完之后」**提到这里** ——
        //   原位置排在 WaitFieldClear + 30s 清场守卫之后，玩家得先把这一波怪全打完才看到这句，
        //   主人原话「打完之后才弹剧情跟紧我」。现在怪一进场站定就说。
        //   【2026-10-14 二次修正】解冻已提前到第二抽 CoMidBattleDraft 结束（本方法上方新增的那行）：
        //   主人报「继续后玩家和佣兵都不动，按说应该继续往前走」。现在点完继续人就恢复战斗，
        //   怪也能正常进场（不再干等 10s 超时）；本对话播的时候 TalkBlock 自行短暂冻结、播完即恢复。
        // 【2026-10-07 主人反馈「佣兵说的话太快了，没看到」】停留 0.75 → 1.9。
        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "跟紧我，别走散。", 1.9f));

        // 不冻结战斗：玩家可随时上前清怪。
        // 2026-09-18：这里原本会直接改写 Hero 坐标把玩家往前推（最高 18/秒），
        // 触发瞬间看起来就是「被瞬移」。只等待，绝不动画家坐标。
        if (bm != null) bm.UnitsCanAct = true;
        bm.RetargetAllMonsters(Hero.Instance);
        hint.Show("怪物冲过来了，靠近它们会自动攻击。", null, 5f);
        yield return WaitFieldClear(strict: true);
        // 清场兜底：WaitFieldClear 靠「存活数 + 未刷出波次」连读 0.45s 判定已清，
        // 遇到刷怪空窗 / 存活数同帧缓存会提前返回。
        // 这里再按现成的存活怪计数确认一次，确保场上真的清空了才继续（上限 30s，不会卡死流程）。
        float clearGuard = 0f;
        while (bm != null && clearGuard < 30f
               && (bm.GetAliveMonsterCount() > 0 || bm.HasPendingWaves))
        {
            clearGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        bm.ClearMonsterForcedTargets();

        AllowBattleSkillClick = false;

        // 【2026-10-14 主人拍板·方案 A】「跟紧我，别走散。」已提到上面 WaitMonstersFinishedEnter 之后，
        //   这里不再重复播 —— 原位置在清场守卫之后，玩家要打完一整波才看到这句话。
        headTalk?.HideNow();
        hint.Hide();

        // —— 第三次抽（技能）：走正式入口 CoMidBattleDraft ——
        // 2026-10-05 主人拍板「技能就是初始带的那个，只不过这回是抽奖给的」。
        // 【2026-10-11 主人拍板·口径已改】不再有「本局第一次抽到技能 = 白送职业初始技能」的保底
        //（原 SlotMachineSystem.BuildGuaranteedSkillCard 已删）—— 引导这一抽同样是**随机池**，
        // 抽到什么就是什么，全职业一个概率。
        // ⚠ 原来这里的「固定三选一 + 拖拽定释放顺序」教学拍（CoTutorialSkillDraft）已被这一抽顶掉并删除。
        //   拖拽教学改由 SkillOrderGuide 做软引导：玩家凑够 2 个技能时弹一条会自己消失的气泡，不挡操作。
        // 【2026-10-12 主人拍板「每次引导战力一样」】引导局这一抽**固定发「治愈之泉」**
        //   （SlotMachineDefs.TutorialFixedSkillId，见 DraftPool.CollectNewSkills 引导分支）；
        //   正式关仍是随机池，全职业一个概率。
        // 第三抽的钱来自开局 240 的最后 80（240 − 80×3 = 0），中途没有任何补贴。
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        if (bm != null) bm.UnitsCanAct = true;

        // —— 5) 精英宿敌：认出塔克 → 放狠话 → 打到残血留遗言 ——
        // 【2026-10-07 主人拍板】精英和塔克有过节：出场点名挑衅，被塔克打死前留一句
        //「我不过是个小杂鱼……你前面的路将会是一片黑暗」，然后塔克「……」「走吧」收尾。
        // 用表里现成的 **order=4**（本来就是「含 1 精英」那波，HP 档 55~65），不新增、不改数值。
        var stepElite = TutorialBattleTable.GetStepOrDefault(4);
        if (stepElite.eliteCount > 0)
        {
            // 【2026-10-07 主人反馈「玩家说的 boss 那句话也没看到」】先让玩家自己喊一句，
            // 再砸「精英来袭」预告 —— 两句之间要能看清字，停留一律 ≥1.9s。
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(Hero.Instance, "有个块头更大的！", 1.9f));
            // 2026-10-09 主人拍板：Boss 大图 wave_boss_incoming 本来就写着「首领来袭」，
            // 这里再传副标题 = 上面图片 + 下面文字两个提示（主人口中的"上下 2 个首领来袭"）。
            // 去掉文字，只留大图；Boss 波一律不叠副标题（与 BattleManager 2026-10-07 口径一致）。
            yield return BattleWaveAnnounceUI.CoPlay(BattleWaveAnnounceUI.Kind.Boss);
        }
        // 2026-10-09 主人拍板：order=4 是 around 波，QueueTutorialStep 里 around 缺 forcedTarget
        // 会直接 return（"跳过以免围空点"）→ 这波根本没排上，场上没有精英，主人才"没见到精英怪就结束了"。
        // 这里补上围杀目标（英雄），让这波真正刷出、含 1 只放大精英（eliteCount=1）。
        yield return EnsureTutorialStep(bm, 4, forcedTarget: Hero.Instance);
        if (bm != null) bm.UnitsCanAct = true;
        hint.Show("组队后佣兵会自动战斗。", null, 3f);

        Monster rival = null;
        yield return CoFindWaveElite(bm, m => rival = m);

        if (rival != null && !rival.isDead)
        {
            // 【2026-10-07 主人反馈「说的话太快了，没看到」】气泡停留一律往 2 秒以上给。
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(rival, "老大交代过：小心那个扛盾的。说的就是你吧？", 2.4f),
                new TalkLine(rival, "我看也没什么了不起——让我试试你的实力。", 2.4f));
            if (bm != null) bm.UnitsCanAct = true;
        }
        else
        {
            Debug.LogError("[Tutorial] 精英波没找到精英单位（order=4 的 eliteCount 是不是写 0 了？），宿敌对白跳过");
        }

        // —— V6 P3：这一波充满能量，让玩家看见技能自动放出去 ——
        yield return CoWatchPlayerSkill(bm, hint);

        if (rival != null)
        {
            yield return CoEliteLastWords(bm, headTalk, rival);
            // 【2026-10-14 主人拍板】遗言播完**立即播死亡动画**，塔克不再冲上来补一刀 ——
            // 原「塔克收头」演出（CoTuckerFinishElite：冻场 → 冲身侧 → 挥剑+闪电 → 判死）整段删除。
            // 现在遗言说完直接判死，精英走自己的死亡动画。
            if (rival != null && !rival.isDead)
            {
                bool canActBefore = bm.UnitsCanAct;
                bm.UnitsCanAct = false;
                rival.TakeDamage(TutorialRescueDamage, false, true, true, 0, merc, suppressDamageText: true);
                Debug.Log("[Tutorial] 精英宿敌：遗言完直接判死（不演塔克收头）");
                bm.UnitsCanAct = canActBefore;
            }
        }

        yield return WaitFieldClear(strict: true);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "……", 1.8f),
            new TalkLine(merc, "走吧。", 2.0f));
        headTalk?.HideNow();
        hint.Hide();

        // —— 14)【2026-10-15 主人拍板】引导结尾也走传送门：「所有打完一关后都出现传送门，
        // 进传送门的步骤」照旧（玩家先走、佣兵隔一个身位跟着进，各播一次传送特效）。
        // 全员进完经 BattleManager 的引导分支接撤离结算（BattleSettlement → 回城）。
        // 【2026-10-09 旧口径】塔克说完「走吧。」直接回城结算、没有门——已被上面 10-15 拍板覆盖。
        // 这里先停一下让最后两句台词读完，再开门。
        yield return new WaitForSecondsRealtime(0.6f);
        if (bm != null)
            bm.BeginTutorialPortalExit();
        else
            Debug.LogError("[Tutorial] 引导收尾失败：BattleManager 为空，无法触发传送门出口");

        _battleTutorialFlowStarted = false;
        _flow = null;
    }

    // ============================================================
    // 【2026-10-15 主人拍板】引导新一步：点佣兵头像看新伙伴
    // 获得佣兵弹窗改为点头像弹出（不再抽中默认弹，见 BattleUI.OnMercAvatarClicked /
    // MercObtainPopupUI.ShowFromEntry）。引导局在二抽（塔克入队）后高亮佣兵头像，
    // 等玩家点一次；BattleUI.OnMercAvatarClicked 点到人后回调 NotifyMercAvatarTapped()。
    // 60s 没点也放行（与引导既有超时口径一致），绝不卡死流程。
    // ============================================================
    bool _mercAvatarTapped;

    /// <summary>BattleUI.OnMercAvatarClicked 点佣兵头像后回调（引导这一步等的就是这一下）。</summary>
    public void NotifyMercAvatarTapped() => _mercAvatarTapped = true;

    IEnumerator CoWaitMercAvatarTap(TutorialHintUI hint, BattleUI ui, float timeoutSec = 60f)
    {
        _mercAvatarTapped = false;
        var slotRt = (ui != null && ui.mercSlot1 != null && ui.mercSlot1.root != null)
            ? ui.mercSlot1.root.transform as RectTransform : null;
        hint.ShowHard("点一点新伙伴的头像，看看他的底细。", slotRt);
        float t = 0f;
        while (!_mercAvatarTapped && t < timeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!_mercAvatarTapped)
            Debug.LogWarning("[Tutorial] 等玩家点佣兵头像超时（60s），放行继续流程");
    }

    static void HaltUnit(UnitBase u)
    {
        if (u == null || u.rb == null) return;
        u.rb.velocity = Vector2.zero;
    }

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
        const float maxWait = 15f;
        const float bailHpRatio = 0.2f;
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
        if (elite != null)
        {
            float enterWait = 0f;
            while (enterWait < 15f && !elite.isDead && elite.IsEnteringMap)
            {
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

        var bar = RunSkillBarUI.Instance;
        // 2026-10-06 主人拍板：说清「顺序＝优先级」，与底部槽左上角 ①②③④ 和
        // SkillOrderGuide 的「拖动技能可改释放顺序：① 最先放。」同一口径。
        hint.Show("技能各自倒计时，冷却好了自动释放，顺序看左上角 ①②③④。",
            bar != null ? bar.GetComponent<RectTransform>() : null, 8f);

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
                }
                else break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        hint.Hide();
        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "撑不住了，先撤？", 1.2f),
            new TalkLine(merc, "嗯，回城我给你疗个痛快。", 1.2f));
    }

    /// <summary>
    /// P4：回城后引导点一次天赋（左栏属性节点，消耗金币）。
    /// 金币不够 / 界面缺失 / 玩家直接关窗，都放行——引导不能把人卡在天赋页。
    /// </summary>
    IEnumerator CoGuideTalentUpgrade()
    {
        var hint = TutorialHintUI.Ensure();

        // 2026-09-19：天赋引导已并入「首次启动强制软引导」（FirstRunGuide.Talent）：
        // 角色页 → 天赋按钮 → 节点，三步都带高亮圈 + 半透明遮罩 + 气泡，且每步落盘可续。
        // 只有引导那一步已经结束（含跳过）时，才退回原来这条一次性弱提示兜底。
        if (GuideCovers(FirstRunGuide.GuideStep.Talent))
        {
            Debug.Log("[Tutorial] 天赋引导交给 FirstRunGuide.Talent，跳过旧提示");
            yield break;
        }

        int before = TalentUI.LeftUnlockedCount();
        if (!TalentSystem.CanUnlockLeft(before + 1, out string reason))
        {
            Debug.Log($"[Tutorial] 跳过天赋引导：{reason}");
            hint.Show("金币攒够后，可以在天赋页点满属性节点。", null, 5f);
            yield return new WaitForSecondsRealtime(1.5f);
            hint.Hide();
            yield break;
        }

        // 1) 不自动打开角色页：让玩家自己点 BottomNav 的「角色」（用户口径：都让玩家点）
        var nav = MainBottomNav.Instance;
        RectTransform charRt = nav != null && nav.characterButton != null
            ? nav.characterButton.GetComponent<RectTransform>() : null;

        float bind = 0f;
        const float navTimeout = 15f;
        while (bind < navTimeout)
        {
            var ch = CharacterUI.Instance;
            if (ch != null && ch.gameObject.activeInHierarchy) break;
            if (charRt != null)
                hint.ShowHard("先点下方「角色」，再点「天赋」。", charRt);
            bind += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        var character = CharacterUI.Instance;
        if (character == null || !character.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[Tutorial] 玩家未进入角色页，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        // 2) 指向「天赋」按钮；玩家不点就不往下走，**绝不替玩家点开**
        RectTransform talentRt = character.talentButton != null
            ? character.talentButton.GetComponent<RectTransform>() : null;
        float t = 0f;
        const float openTimeout = 15f;
        while (t < openTimeout)
        {
            var talent = TalentUI.Instance;
            if (talent != null && talent.IsOpen) break;
            if (talentRt != null)
                hint.ShowHard("点「天赋」，用金币点一个属性节点。", talentRt);
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen)
        {
            Debug.LogWarning("[Tutorial] 玩家未点开天赋页，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        var talentUi = TalentUI.Instance;
        if (talentUi == null || !talentUi.IsOpen)
        {
            Debug.LogWarning("[Tutorial] 天赋页没打开，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        // 3) 高亮下一个可点节点，等玩家点
        float t2 = 0f;
        const float pickTimeout = 25f;
        while (t2 < pickTimeout)
        {
            if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen) break;
            if (TalentUI.LeftUnlockedCount() > before) break;

            var rt = TalentUI.Instance.GetLeftNode(before);
            if (rt != null)
                hint.ShowHard("点这个节点：属性立刻生效。", rt);

            t2 += Time.unscaledDeltaTime;
            yield return null;
        }

        hint.Hide();
        if (TalentUI.LeftUnlockedCount() > before)
            UIManager.Instance?.ShowToast("天赋已生效，属性已更新");
    }

    /// <summary>放技能的点击目标：优先玩家头像槽，退回技能头像按钮。</summary>
    static RectTransform ResolvePlayerSkillTarget(BattleUI ui)
    {
        if (ui == null) return null;
        if (ui.playerSlot != null && ui.playerSlot.root != null
            && ui.playerSlot.root.activeInHierarchy)
            return ui.playerSlot.root.GetComponent<RectTransform>();
        if (ui.playerSkillAvatar != null && ui.playerSkillAvatar.root != null
            && ui.playerSkillAvatar.root.activeInHierarchy)
            return ui.playerSkillAvatar.root.GetComponent<RectTransform>();
        return null;
    }

    struct TalkLine
    {
        public UnitBase speaker;
        public string text;
        public float hold;
        public TalkLine(UnitBase s, string t, float h) { speaker = s; text = t; hold = h; }
    }

    /// <summary>
    /// 一段对话只冻一次全场；台词直接 yield CoPlayLine，点一下跳字、再点跳句。
    /// 说话人缺失则跳过该句，不卡流程。
    /// restoreAct=false 时对话结束后保持冻结（入队等关键段用）。
    /// </summary>
    static IEnumerator TalkBlock(BattleManager bm, BattleHeadTalkUI talk, params TalkLine[] lines)
    {
        yield return TalkBlock(bm, talk, true, lines);
    }

    static IEnumerator TalkBlock(BattleManager bm, BattleHeadTalkUI talk, bool restoreAct, params TalkLine[] lines)
    {
        if (talk == null || lines == null || lines.Length == 0)
            yield break;

        TutorialHintUI.Instance?.Hide();

        bool prev = bm == null || bm.UnitsCanAct;
        if (bm != null) bm.UnitsCanAct = false;

        try
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.speaker == null || line.speaker.isDead || string.IsNullOrEmpty(line.text))
                    continue;
                yield return talk.CoPlayLine(line.speaker, line.text, line.hold);
            }
        }
        finally
        {
            talk.HideNow();
            if (bm != null) bm.UnitsCanAct = restoreAct && prev;
        }
    }

    /// <summary>单句兼容入口。</summary>
    static IEnumerator TalkHeld(BattleManager bm, BattleHeadTalkUI talk, UnitBase speaker,
        string content, float hold)
    {
        yield return TalkBlock(bm, talk, new TalkLine(speaker, content, hold));
    }

    // 【2026-10-06 已删除】EnsureTutorialMercPermanent：它是第 4 拍「救援入队」的配套
    //   （把引导佣兵写进本局 hiredMercs + 图鉴 MarkMercSeen）。入队戏整段删掉后已无任何调用点，
    //   引导佣兵现在由正式招募链路 RunDraftDirector.RecruitMerc 入队 → 整方法删除，不留死代码。
    //   【2026-10-08 补记】当时的引导佣兵是 H011 路加（牧师）；2026-10-07 起那位已换成 H003 塔克。

    /// <summary>
    /// 2026-09-29 主人拍板：引导佣兵（现为塔克）是**引导期佣兵**，引导结束就清空，不跟到正式关卡。
    /// 同时清 hiredMercs（本局雇佣）与 permanentMercs（旧存档可能残留的那一条）。
    /// 之后想在正式关卡里带佣兵，只能靠进关抽奖。
    /// </summary>
    static void ClearTutorialMerc()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        string id = StoryProgress.TutorialMercId;
        if (string.IsNullOrEmpty(id)) return;

        RemoveMercFrom(data.hiredMercs, id);
        RemoveMercFrom(data.permanentMercs, id);
        SaveSystem.Instance.Save();
        Debug.Log($"[Tutorial] 引导期佣兵已清空 id={id}");
    }

    static void RemoveMercFrom(System.Collections.Generic.List<MercenaryData> list, string mercId)
    {
        if (list == null) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] != null && list[i].mercId == mercId)
                list.RemoveAt(i);
        }
    }

    /// <summary>
    /// 进战后的开场：刷第一波并借技能提示的空档让怪走进场。
    /// 【2026-10-09】原本这里还有一步「教摇杆」，摇杆已于 2026-10-05 停用，那一步不再存在。
    /// </summary>
    /// <param name="preloadStep">&gt;0 时在技能提示之前先把这一波刷出来（让怪借读字时间进场）。</param>
    static IEnumerator CoTeachControls(BattleManager bm, TutorialHintUI hint, BattleUI ui, int preloadStep = 0)
    {
        if (bm != null) bm.UnitsCanAct = true;
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // 原来这一段是「教摇杆」：建摇杆 → 显示「下方滑动移动，松手自动锁敌。」
        // → 死等玩家真的推一下（最多 15 秒）。摇杆没了，这一步绝不能留 —— 留着就是白等 15 秒。
        // var joyRoot = ui != null ? ui.transform
        //     : (BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        // BattleJoystick.EnsureOn(joyRoot);
        // BattleJoystick.Instance?.SetVisible(true);
        // RectTransform stickRt = BattleJoystick.Instance != null
        //     ? BattleJoystick.Instance.StickHighlight
        //     : null;
        // hint.Show("下方滑动移动，松手自动锁敌。", stickRt, -1f);
        // float waitJoy = 0f;
        // while (BattleJoystick.Instance == null || !BattleJoystick.Instance.IsHeld)
        // {
        //     waitJoy += Time.unscaledDeltaTime;
        //     if (waitJoy > 15f) break;
        //     yield return null;
        // }
        // hint.Hide();

        // 2026-09-28 主人反馈「第一波的敌人出来的还是晚，再早点」：
        // 把第 1 波的刷怪提到技能提示之前，利用这 3.2 秒读字时间让怪走进场，
        // 等下面「靠近怪物会自动攻击」这句出来时，怪已经站在脸上了。
        if (preloadStep > 0)
            yield return EnsureTutorialStep(bm, preloadStep);

        // 【2026-10-07 主人拍板】这句「技能自动释放 + 顺序」的提示**不再开局就弹** ——
        // 它属于「抽中技能、能调整顺序」的那一拍，出口收敛到 SkillOrderGuide.Text（唯一文案源），
        // 由 RunSkillBarUI.Refresh 在技能数凑够 2 个时弹。这里只保留原来的 3.2 秒空档：
        // 这段时间是给上面 preloadStep 刷出来的第一波怪走进场用的（主人口径「第一波要早点」），
        // 别把 yield 一起删掉，删了怪会站得更远。
        yield return new WaitForSecondsRealtime(3.2f);
        // 【2026-10-05 「不要摇杆了」→ 停用，先注释不删】
        // BattleJoystick.Instance?.ResetStickIdle();

        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
    }

    /// <summary>清场后：教程内直接刷下一波，不播正式关的波次预告（避免剩最后一只怪时卡住感）。</summary>
    static IEnumerator CoTutorialNextWave(BattleManager bm, TutorialHintUI hint, float delaySec, int order)
    {
        hint.Hide();
        yield return EnsureTutorialStep(bm, order);
    }

    /// <summary>
    /// 教程刷怪兜底：最多重试 3 次，每次等两帧看有没有真出怪。
    /// 导演只点步进号；人数/进场/HP 由 QueueTutorialStep 读表。
    /// </summary>
    static IEnumerator EnsureTutorialStep(BattleManager bm, int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        if (bm == null) yield break;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bm.QueueTutorialStep(order, anchorX, forcedTarget);
            yield return null;
            yield return null;
            if (bm.GetAliveMonsterCount() > 0)
            {
                Debug.Log($"[Tutorial] 刷怪成功 step={order} attempt={attempt + 1} alive={bm.GetAliveMonsterCount()}");
                yield break;
            }
            Debug.LogWarning($"[Tutorial] 刷怪未出怪 step={order} attempt={attempt + 1}，重试");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Debug.LogError($"[Tutorial] 连续 3 次刷怪失败 step={order}，跳过本波以免卡流程");
    }

    // 【2026-10-06 已删除】CoRefreshMercHudNextFrame：只被「救援入队」那一段调用，
    //   入队戏删除后无调用点 → 整方法删除（HUD 现在靠 SlotMachineSystem.TutorialMercDrawn 推导，抽完即亮）。

    IEnumerator OfferTutorialEquip()
    {
        var equip = CreateTutorialEquipDrop();
        if (equip == null)
        {
            UIManager.Instance?.ShowToast("地上有件装备。");
            yield break;
        }

        // 弹窗前不入包：等玩家点按钮再由 EquipDropPopupUI 入包
        bool closed = false;
        EquipDropPopupUI.ShowSingle(equip, (_, __) => closed = true);
        while (!closed) yield return null;

        BattleUI.Instance?.UpdateBackpackGrid();
    }

    // 【2026-10-06 已删除】TryGrantTutorialMercFragment（引导抽到佣兵发本命碎片）。
    //   主人当天拍板：第 2 抽 = **直接把引导佣兵招募入队**（SlotMachineSystem.BuildGuaranteedMercCard），
    //   碎片那条口径作废。方法 + 调用点整条删除，不留开关；
    //   「引导佣兵招没招」的判据只剩 SlotMachineSystem.TutorialMercDrawn 一处。
    //   【2026-10-08 补记】当时那位是 H011 路加（牧师）；2026-10-07 起引导佣兵为 H003 塔克。

    /// <summary>
    /// 引导宝箱的开箱产出（2026-10-05 主人拍板）：**天赋石**。
    /// 原来掉一把武器，但开局第一抽就会随机给一件装备（2026-10-06：装备没有保底，木制圆盾只是起步装备），
    /// 宝箱再掉装备就重复了；
    /// 天赋石是城镇点天赋的硬通货，正好把「开箱 = 长期成长」这一课补上。
    /// 数量就改这一个常量。
    /// </summary>
    const int TutorialChestTalentStones = 3;

    /// <summary>
    /// 开箱拿到东西后，留给玩家看清楚的停留秒数。
    /// 【2026-10-06 主人反馈「宝箱获得的东西太快了，没看到是啥就没了」】—— 要再长就调这一个值。
    /// </summary>
    const float ChestRewardReadSec = 1.6f;

    /// <summary>
    /// 引导三拍抽奖的统一文案（2026-10-07 主人拍板）。
    ///
    /// <para>① <b>不剧透类别</b>：原来写的是「这次是伙伴 / 这次是本命技能」，
    /// 主人原话「不是说过不要说这次抽的是佣兵吗，要给玩家惊喜」——抽到什么自己开出来才有惊喜感。</para>
    /// <para>② <b>三拍同一句</b>：文案只有这一个出口，别在三处各写一份（铁律「不多入口」）。</para>
    /// </summary>
    const string TutorialDrawBeatText = "清场！奖励你抽一次 —— 开出什么全看手气。";

    static void GrantTutorialChestTalentStones()
    {
        if (TutorialChestTalentStones <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, TutorialChestTalentStones,
                           save: true, notify: true);
        // 【2026-10-06 主人拍板：物品变动一律 force 弹出】普通 toast 会被引导遮罩/气泡吞掉，
        // 走 GlobalToastUI 的 force 通道，保证宝箱给的东西一定露脸。
        GlobalToastUI.Show($"获得天赋石 ×{TutorialChestTalentStones}", true);
        // 【2026-10-07 主人反馈「获得天赋石没有在顶条资源那显示」】
        // 顶栏只在 AutoGameInitializer 里刷过一次，中途拿到石头不会自己重画；
        // 这里拿到就刷一次，玩家能在顶条上看到数字从 0 变成 3。
        BattleUI.Instance?.UpdateTopBarResources();
    }

    static EquipInstance CreateTutorialEquipDrop()
    {
        // 宝箱只掉武器（forceWeapon 强制主手武器分支），避免误给防具。
        // 2026-10-12 主人拍板：引导宝箱不再「强制稀有」。
        // 旧写法 GenerateFromTemplate(tpl, 0, lv, true, Rarity.Rare) 把武器钉死在稀有档
        // （ATK 14~20 × 系数 ≈ 16~28，初始武器仅 4.5~6）→ 一抽就秒怪，体验崩。
        // 现统一走掉落路径（GetRandomEquipInstances，带 RIFT_DROP_ATTR_BONUS +10）：
        //   - 必为武器，且数值 > 初始武器（主人要求「初始武器最弱，随便一个都比它强」）；
        //   - 稀有度由 hidden_level_rules.csv 曲线决定：隐藏等级 1 = 100% 普通，
        //     随杀怪 / 通关慢慢提升稀有 / 传奇概率，前期普通为主。
        // 2026-10-12 主人拍板「每次引导战力一样」：宝箱这件也走确定性生成（fixedForTutorial: true）
        // —— 恒普通 / 必主手武器 / 属性取区间中值，与引导第一拍抽卡那件同口径。
        var list = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetRandomEquipInstances(1, 1, 0, StageType.Normal, GameConfig.RIFT_DROP_ATTR_BONUS, true, ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1, true)
            : null;
        if (list != null && list.Count > 0)
        {
            var eq = list[0];
            if (eq != null)
            {
                eq.requireLevel = 1;
                eq.template?.ResolveIcon();
                if (eq.icon == null && eq.template != null) eq.icon = eq.template.icon;
                if (WeaponLoadoutRules.IsLoadoutItem(eq))
                    AlignWeaponToHeroAttackHand(eq);
                if (string.IsNullOrEmpty(eq.equipName) || LooksLikeEnglishFileName(eq.equipName))
                    eq.equipName = WeaponAffixSystem.RarityPrefix(eq.rarity)
                                   + EquipNameGen.RandomWeaponName(eq.template, eq.slotType);
            }
            return eq;
        }
        return null;
    }

    /// <summary>教程武器固定逻辑主手；实际挂点仍由 HandRig 解析到普攻手。</summary>
    static void AlignWeaponToHeroAttackHand(EquipInstance eq)
    {
        if (eq == null || !WeaponLoadoutRules.IsLoadoutItem(eq)) return;
        eq.slotType = EquipSlotType.MainHand;
        eq.weaponHand = WeaponHandSlot.MainHand;
    }

    static bool LooksLikeEnglishFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c > 127) return false;
        }
        return name.IndexOf('_') >= 0 || name.StartsWith("equip", System.StringComparison.OrdinalIgnoreCase);
    }

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

    static Button ResolveAdventureButton()
    {
        if (MainBottomNav.Instance != null && MainBottomNav.Instance.adventureButton != null)
            return MainBottomNav.Instance.adventureButton;
        var hall = GuildHallUI.Instance;
        if (hall != null && hall.navAdventureButton != null)
            return hall.navAdventureButton;
        if (hall != null && hall.bottomNav != null && hall.bottomNav.adventureButton != null)
            return hall.bottomNav.adventureButton;
        return null;
    }

    IEnumerator WaitNotLoading()
    {
        float t = 0f;
        while (SceneLoadingCoordinator.IsActive && t < 8f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;
    }
}
