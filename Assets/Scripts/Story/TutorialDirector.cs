using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新手引导：城镇开场 → 短战斗（选职/牧师救援/强制撤离）→ 回城收尾。
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

    public bool ShowMercHud { get; private set; }
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
    /// 负责时这里的旧「弱提示 / 硬提示」一律让位，不然同一个位置会叠两层指引。
    /// </summary>
    static bool GuideCovers(FirstRunGuide.GuideStep step)
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
        ShowMercHud = false;
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
            StoryDirector.Solo("咨询台小姐",
                "第一次下裂隙？三件事：\n1. 你只管走路，打架会自动打。\n2. 裂隙很危险，尽量组队进入。\n3. 见好就收，活着才有收益。",
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

        TutorialHintUI.Ensure().Show("装备和材料死亡也能带回，但本局金币死了会清零。", null, 6f);

        // 收尾对话要用的立绘/背景先读进缓存，否则开场会卡一下才出画面
        StoryAssetLoader.Warmup(StoryAssetLoader.Backgrounds, StoryBackgrounds.GuildHall);
        yield return null;
        StoryPortraits.Warmup(
            StoryPortraits.Player, StoryProgress.TutorialMercHireId, StoryPortraits.Receptionist);
        yield return null;

        yield return new WaitForSecondsRealtime(2.2f);

        bool done = false;
        DialogueUI.Instance?.PrepareForStoryBeat();
        // 小白 = H011（牧师），禁止再用 LaoDun(H001 盾兵) 立绘
        string xiaobaiId = StoryProgress.TutorialMercHireId;
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            // 只有小白一个人说话：不要摆玩家立绘，直接让她单人居中说
            //（约定：单人说台词一律用 Solo，别摆两个立绘站着不说话）
            StoryDirector.Solo("小白",
                "大难不死……回城歇歇吧。需要治疗的话，来酒馆找我。",
                xiaobaiId)
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

        // —— V6 P4：回城引导点一次天赋（金币不够会自己跳过）——
        yield return CoGuideTalentUpgrade();

        var nav = MainBottomNav.Instance;
        RectTransform highlight = null;
        if (nav != null && nav.characterButton != null)
            highlight = nav.characterButton.GetComponent<RectTransform>();
        // 软引导：指 BottomNav 角色入口。引导还在管这一步时让位，别两层一起指同一个按钮。
        if (!GuideCovers(FirstRunGuide.GuideStep.Talent))
            TutorialHintUI.Ensure().Show("可以去角色界面查看属性",
                highlight, 12f);

        StoryProgress.MarkTutorialDone();
        // 2026-09-29 主人拍板：引导一结束就把小白清掉，不让他跟到正式关卡。
        // 引导关撤离走的是 SkipMercHireClearOnEvacuate=true（不清雇佣），所以必须在这里主动清。
        ClearTutorialMerc();
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
        ShowMercHud = false;
        AllowBattleSkillClick = false;
        WaitingEvacuate = false;

        // —— 0) 教摇杆 + 自动技能（选职已在冒险页完成）——
        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
        yield return CoTeachControls(bm, hint, ui, preloadStep: 1);

        // —— 1) 首波清场后进入宝箱剧情（不再刷第二小波）——
        // 2026-09-27 主人反馈「引导到自动攻击时怪还没出来」→ **先刷怪、再弹提示**。
        // 2026-09-28 再提前：第 1 波已在 CoTeachControls 里刷（preloadStep:1），
        // 借「技能能量满会自动释放」那 3.2 秒读字时间走完进场，这里不再重复刷
        // （EnsureTutorialStep 不看是否已刷过，重复调用会多排一波）。
        TutorialBattleTable.EnsureLoaded();
        hint.Show("靠近怪物会自动攻击。", null, 8f);
        if (bm != null) bm.UnitsCanAct = true;
        yield return WaitFieldClear();

        yield return WaitFieldClear(strict: true);
        if (bm != null && bm.GetAliveMonsterCount() > 0)
        {
            hint.Show("先把剩下的怪清掉。", null, 3f);
            yield return WaitFieldClear(strict: true);
        }

        // —— 1b) 第二拍：混编波 ——
        // ⚠【2026-10-05 主人拍板删除】原本文案是「后面那个会射你，先冲上去解决它。」
        //   现在是**自动攻击**，玩家没有「选择打谁」的操作，这条引导教了个不存在的动作 —— 删掉。
        //   第二拍照常刷怪（tutorial_battle.csv order=2 的混编波），只是不再显示这条提示。
        yield return EnsureTutorialStep(bm, 2);
        yield return WaitFieldClear(strict: true);

        // —— 1c) 第二次抽（佣兵）：走正式入口 CoMidBattleDraft（冻场 → 弹面板 → 抽一次 → 解冻）。
        // 抽奖是主玩法，不是引导脚本 —— 这里的节拍只是「什么时候弹」，规则在 SlotMachineSystem。
        // 引导局这一发给的是小白的本命碎片（她在第 4 拍走剧情入队）。
        // 引导三拍的钱**只有开局那 240**（每拍一抽 80，正好三抽）；不再有任何中途补贴 ——
        // 2026-10-05 主人拍板「清零 + 不要总打补丁」，TUTORIAL_WAVE_BONUS_COINS 整条链路已删。
        if (bm != null)
            yield return bm.CoMidBattleDraft("打完两波，再抽一次：这次是伙伴。");

        // —— 2) 宝箱陷阱：发现 → 左右埋伏 → 清场 → 开箱拿剑 ——
        hint.Hide();
        var chestDir = StageClearRewardDirector.Instance;
        if (chestDir == null)
        {
            var go = new GameObject("StageClearRewardDirector");
            chestDir = go.AddComponent<StageClearRewardDirector>();
        }
        chestDir.CacheSceneRefs();

        // 2026-10-05 主人拍板：宝箱不再掉装备（掉天赋石，见 GrantTutorialChestTalentStones），
        // 装备由开局抽奖保底给。开箱演出照旧，只是箱子里不再吐一件装备。
        hint.Hide();
        // 2026-09-27 主人拍板：清完上一波不要马上进宝箱剧情 —— 先让玩家往前走两步，
        // 宝箱再「突然出现」。UnitsCanAct 保持 true（WaitFieldClear 结尾已放开，场上无怪 → 向右推图），
        // 走够距离才冻结进剧情。只等，绝不改写 Hero 坐标（2026-09-18 教训）。
        yield return CoWaitHeroAdvance(2.4f, 4f);
        // 2026-09-28：最后一只怪的尸体彻底消失之后再让宝箱冒出来（等 deathAnimDuration 走完）
        yield return CoWaitMonstersGone(bm);
        if (bm != null) bm.UnitsCanAct = false;
        yield return chestDir.CoTutorialPlaceChest(4f, waitForHeroApproach: false);
        chestDir.SnapHeroBeforeChest();

        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "有个宝箱，真是好运！", 0.85f));

        // 说完第一句 → 两边怪进场停稳 → 再「！」→ 开战
        float chestX = chestDir.ChestWorldX;
        if (bm != null)
        {
            bm.UnitsCanAct = false;
            bm.AllowMonsterMapEnter = true; // 对白已结束：只放行怪走进，战斗仍冻
        }
        bm?.QueueTutorialStep(3, chestX);
        {
            int guard = 0;
            while (bm != null && bm.GetAliveMonsterCount() <= 0 && guard < 8)
            {
                guard++;
                yield return null;
                yield return null;
                if (bm.GetAliveMonsterCount() <= 0)
                    bm.QueueTutorialStep(3, chestX);
            }
        }
        yield return WaitMonstersFinishedEnter(bm);
        if (bm != null)
            bm.AllowMonsterMapEnter = false; // 「！」对白期间全部暂停

        try
        {
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(Hero.Instance, "糟了，是陷阱！", 1.0f));
        }
        finally
        {
            if (bm != null)
                bm.AllowMonsterMapEnter = false;
        }

        if (bm != null)
        {
            bm.AllowMonsterMapEnter = false;
            bm.UnitsCanAct = true;
        }
        yield return WaitFieldClear(strict: true);

        if (bm != null)
        {
            bm.UnitsCanAct = false;
            HaltUnit(Hero.Instance);
        }

        hint.Hide();
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "打开看看里面有什么。", 0.7f));
        GameObject groundIcon = null;
        // 2026-10-05 主人拍板：宝箱改掉**天赋石**，不再掉装备 → 这里传 null（箱子里不吐装备），
        // 开箱演出与「清掉埋伏 → 开箱」的节拍原样保留。
        yield return chestDir.CoTutorialOpenChestAndDropEquip(null, g => groundIcon = g);

        GrantTutorialChestTalentStones();

        if (groundIcon != null)
            Object.Destroy(groundIcon);

        if (bm != null)
        {
            bm.UnitsCanAct = true;
            bm.BeginTutorialPowerFantasy();
        }
        BattleUI.Instance?.UpdateBackpackGrid();
        Hero.Instance?.costumeManager?.RefreshCostume();

        hint.Show("天赋石可以在城镇里点天赋。", null, 1.8f);
        yield return new WaitForSecondsRealtime(0.2f);

        // —— 4) 救援戏：牧师先在前方眩晕被围殴 ——
        hint.Hide();
        ShowMercHud = false;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        var rescueStep = TutorialBattleTable.GetStepOrDefault(4);
        string rescueMercId = string.IsNullOrEmpty(rescueStep.mercId)
            ? StoryProgress.TutorialMercId
            : rescueStep.mercId;
        float rescueHpRatio = rescueStep.mercHpRatio > 0f ? rescueStep.mercHpRatio : 0.35f;
        float rescueAhead = rescueStep.aheadDist > 0f ? rescueStep.aheadDist : 5.5f;
        var merc = bm.SpawnTutorialMercAt(rescueMercId, rescueHpRatio, rescueAhead, stunned: rescueStep.stunned);
        if (rescueStep.eliteCount > 0)
        {
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(Hero.Instance, "有个块头更大的！", 0.75f));
            // 2026-09-28 主人要求：这段佣兵救援剧情说完先砸「首领来袭」预告，再开始战斗
            // （怪在下一行 QueueTutorialStep 才刷，预告播完正好开打；精英血条由 BattleBossHpBar 显示）
            yield return BattleWaveAnnounceUI.CoPlay(BattleWaveAnnounceUI.Kind.Boss,
                $"精英 ×{rescueStep.eliteCount} 来袭 — 先清围殴她的怪");
        }
        bm?.QueueTutorialStep(4, forcedTarget: merc);
        hint.Show("前方有人被怪物围住了，上前帮忙。", null, 4f);

        // 不冻结战斗：玩家可随时上前清怪。
        // 2026-09-18：这里原本会直接改写 Hero 坐标把玩家往前推（最高 18/秒），
        // 触发瞬间看起来就是「被瞬移」。改为只等待，绝不动画家坐标。
        if (bm != null) bm.UnitsCanAct = true;
        float approach = 0f;
        const float approachTimeout = 4f;
        while (approach < approachTimeout)
        {
            approach += Time.unscaledDeltaTime;
            if (Hero.Instance != null && merc != null)
            {
                float dist = Mathf.Abs(UnitBase.GetCombatX(Hero.Instance) - UnitBase.GetCombatX(merc));
                if (dist <= 4.2f) break;
            }
            yield return null;
        }

        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "先把围殴她的怪清掉！", 0.75f));

        // 解冻开打；清完立刻再冻，防止自动往前跑错过入队
        if (bm != null) bm.UnitsCanAct = true;
        bm.RetargetAllMonsters(Hero.Instance);
        hint.Show("怪物冲过来了，靠近它们会自动攻击。", null, 5f);
        yield return WaitFieldClear(strict: true);
        // 清场兜底：WaitFieldClear 靠「存活数 + 未刷出波次」连读 0.45s 判定已清，
        // 遇到刷怪空窗 / 存活数同帧缓存会提前返回，导致围殴怪还没打完就进小白入队剧情。
        // 这里再按现成的存活怪计数确认一次，确保场上真的清空了才继续（上限 30s，不会卡死流程）。
        float clearGuard = 0f;
        while (bm != null && clearGuard < 30f
               && (bm.GetAliveMonsterCount() > 0 || bm.HasPendingWaves))
        {
            clearGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        bm.ClearMonsterForcedTargets();

        // 围殴怪已清：若玩家提前打完，也要进对话
        if (merc == null || merc.isDead)
            merc = bm.SpawnTutorialMercAt(StoryProgress.TutorialMercId, 0.6f, 2.0f, stunned: false);
        if (merc != null)
            merc.StopTutorialStunAnim();

        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
        if (merc != null) HaltUnit(merc);
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(merc, "咳……谢了，我差点交代在这儿。", 1.4f),
            new TalkLine(Hero.Instance, "还能走吗？跟我一起撤。", 1.1f),
            new TalkLine(merc, "我叫小白，是个牧师。行，我跟你。", 1.3f));

        string joinName = StoryProgress.TutorialMercNickname;
        if (merc != null)
        {
            merc.SetTutorialStunned(false);
            if (Hero.Instance != null)
            {
                float frontX = UnitCrowd.GetMercDesiredCombatX(Hero.Instance, merc, 0);
                Vector3 front = new Vector3(frontX, UnitBase.GROUND_Y, Hero.Instance.transform.position.z);
                GameConfig.SetWorldPosition(merc.gameObject, front);
            }
            merc.Face(1);
            merc.SetPartyIndex(0);
            EnsureTutorialMercPermanent(StoryProgress.TutorialMercId, StoryProgress.TutorialMercDisplayName);
            Debug.Log("[Tutorial] 牧师入队完成");
        }
        else
            Debug.LogError("[Tutorial] 牧师入队失败：merc 为空");

        ShowMercHud = true;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();
        if (ui != null)
            ui.StartCoroutine(CoRefreshMercHudNextFrame(ui));
        hint.Show($"{joinName}加入了队伍。", null, 2.0f);
        UIManager.Instance?.ShowToast($"{joinName}加入队伍！");

        // 牧师入队：为玩家疗伤
        if (Hero.Instance != null && Hero.Instance.attr != null)
        {
            float maxHp = Hero.Instance.attr.GetAttr(AttrType.MaxHp);
            Hero.Instance.currentHp = maxHp;
            UIManager.Instance?.ShowToast("牧师为你疗伤");
            ui?.UpdateCharacterSlots();
        }

        yield return new WaitForSecondsRealtime(0.6f);

        if (merc != null && !merc.isDead)
        {
            merc.currentHp = merc.attr.GetAttr(AttrType.MaxHp);
            // 血回满了也要把低血红/受击闪的残留染色清掉，否则小白一身红跟着队伍走
            // （unitAnim 是 protected，外部只能 GetComponent）
            var anim = merc.GetComponent<UnitAnimation>();
            if (anim != null) anim.ForceClearTint();
        }
        if (Hero.Instance != null)
        {
            var hAnim = Hero.Instance.GetComponent<UnitAnimation>();
            if (hAnim != null) hAnim.ForceClearTint();
        }

        ui?.UpdateCharacterSlots();
        AllowBattleSkillClick = false;
        if (bm != null) bm.UnitsCanAct = true;

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "我在后面托着，一起上。", 0.75f));
        headTalk?.HideNow();
        hint.Hide();

        // —— 第三次抽（技能）：走正式入口 CoMidBattleDraft ——
        // 2026-10-05 主人拍板「技能就是初始带的那个，只不过这回是抽奖给的」。
        // 保底在正式系统里：本局第一次抽到技能 = 该职业的初始技能（SlotMachineSystem.BuildGuaranteedSkillCard）。
        // ⚠ 原来这里的「固定三选一 + 拖拽定释放顺序」教学拍（CoTutorialSkillDraft）已被这一抽顶掉并删除。
        //   拖拽教学改由 SkillOrderGuide 做软引导：玩家凑够 2 个技能时弹一条会自己消失的气泡，不挡操作。
        // 第三抽的钱来自开局 240 的最后 80（240 − 80×3 = 0），中途没有任何补贴。
        if (bm != null)
            yield return bm.CoMidBattleDraft("再抽一次：这次是本命技能。");
        if (bm != null) bm.UnitsCanAct = true;

        hint.Show("组队后佣兵会自动战斗。", null, 3f);
        yield return EnsureTutorialStep(bm, 5);
        // —— V6 P3：这一波充满能量，让玩家看见技能自动放出去 ——
        yield return CoWatchPlayerSkill(bm, hint);
        yield return WaitFieldClear(strict: true);

        // —— 压轴：夹击 + 2 精英，正常打不完 —— 把「撤离」教成玩家自己的判断 ——
        yield return CoFinalPressureBeat(bm, hint, headTalk, merc);
        headTalk?.HideNow();

        // 撤离引导：冻住单位，和佣兵原地等玩家点撤离；超时自动撤
        if (bm != null)
        {
            bm.UnitsCanAct = false;
            HaltUnit(Hero.Instance);
            HaltUnit(merc);
        }

        WaitingEvacuate = true;
        RectTransform settingsRt = ui != null && ui.settingsButton != null
            ? ui.settingsButton.GetComponent<RectTransform>() : null;
        // 软引导：硬遮罩挖空容易对不齐设置钮，导致点不到、设置弹窗出不来
        hint.Show("点「撤离」回城；若未看到设置，稍等会自动打开。", settingsRt, -1f);
        yield return null;
        ui?.OnOpenSettings();
        if (TutorialHintUI.Instance != null && TutorialHintUI.Instance.IsVisible
            && SettingsPopupUI.Instance != null && SettingsPopupUI.Instance.EvacuateButton != null)
        {
            hint.ShowHard("选择「撤离」，回城结算。",
                SettingsPopupUI.Instance.EvacuateButton.GetComponent<RectTransform>());
        }

        while (WaitingEvacuate && BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun)
        {
            HaltUnit(Hero.Instance);
            HaltUnit(merc);
            yield return null;
        }

        hint.Hide();
        _battleTutorialFlowStarted = false;
        _flow = null;
    }

    static void HaltUnit(UnitBase u)
    {
        if (u == null || u.rb == null) return;
        u.rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// P3：最后一波把所有技能充满，让玩家亲眼看到技能自动放出去。
    /// 「技能真放出去了」或「这一波打完」哪个先到就结束；最多等 8 秒，不拖流程。
    /// </summary>
    IEnumerator CoWatchPlayerSkill(BattleManager bm, TutorialHintUI hint)
    {
        if (bm == null) yield break;

        _sawPlayerSkillCast = false;
        bm.FillPlayerSkillEnergy();
        RunSkillBarUI.Refresh();

        var bar = RunSkillBarUI.Instance;
        hint.Show("技能各自倒计时，冷却好了会自动释放——看技能条。",
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

    /// <summary>教程入队写入永久花名册，回城酒馆也能看见。</summary>
    static void EnsureTutorialMercPermanent(string mercId, string displayName)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(mercId)) return;
        if (data.permanentMercs == null)
            data.permanentMercs = new System.Collections.Generic.List<MercenaryData>();
        for (int i = 0; i < data.permanentMercs.Count; i++)
        {
            if (data.permanentMercs[i] != null && data.permanentMercs[i].mercId == mercId)
                return;
        }
        MercRosterDefs.GetSkillIds(mercId, out string active, out string passive);
        string nick = StoryProgress.TutorialMercNickname;
        string shown = string.IsNullOrEmpty(displayName) ? StoryProgress.TutorialMercDisplayName : displayName;
        var entry = new MercenaryData
        {
            mercId = mercId,
            displayName = shown,
            nickname = nick,
            hireId = StoryProgress.TutorialMercHireId,
            uid = "tutorial_" + mercId,
            favorLevel = 1,
            level = 1,
            star = 1,
            skillId = active,
            passiveSkillId = passive
        };
        // 教程：写入本局雇佣（引导期临时）；图鉴仍 MarkMercSeen
        data.hiredMercs ??= new System.Collections.Generic.List<MercenaryData>();
        data.hiredMercs.Add(entry);
        // 2026-09-29 主人拍板：小白是**引导期佣兵**，引导完就清空，不跟到正式关卡。
        // 原来这里还会额外写一条 permanentMercs（跨局永久），导致他每局自动进队 —— 已去掉。
        // 正式关卡里要佣兵，只能靠进关抽奖（DraftPool 从 unlockedMercIds 抽）。
        SaveSystem.Instance.Save();
        Debug.Log($"[Tutorial] 牧师已写入本局雇佣（引导期临时，不跨局）id={mercId}");
        AdventureCodex.MarkMercSeen(StoryProgress.TutorialMercHireId);
        AdventureCodex.MarkMercSeen(mercId);
    }

    /// <summary>
    /// 2026-09-29 主人拍板：小白是**引导期佣兵**，引导结束就清空，不跟到正式关卡。
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

    /// <summary>进战后先教摇杆与自动技能。</summary>
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

        hint.Show("技能能量满会自动释放，无需点击。", null, 3.5f);
        yield return new WaitForSecondsRealtime(3.2f);
        hint.Hide();
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

    static IEnumerator CoRefreshMercHudNextFrame(BattleUI ui)
    {
        yield return null;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();
        // 救援佣兵出现后补绑技能点击（SOLO 下原先会跳过）
        ui?.RebindAfterSystemsReady();
    }

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

    /// <summary>
    /// 引导局抽奖抽到「佣兵」那一发的产出：**小白(H011) 的本命碎片**。
    /// 2026-10-05 主人拍板「保留救援戏，抽奖给小白碎片」—— 她走第 4 拍剧情救援入队，
    /// 抽奖不再重复招人。数量改 <see cref="SlotMachineDefs.TUTORIAL_MERC_FRAG_COUNT"/> 一个数。
    /// 存档口径 `frag:{hireId}`（见 MercGrowInventory），背包里能直接看到。
    /// </summary>
    public static bool TryGrantTutorialMercFragment(out string msg)
    {
        msg = null;
        string hireId = StoryProgress.TutorialMercHireId;
        string fragId = MercGrowInventory.FragmentId(hireId);
        int n = SlotMachineDefs.TUTORIAL_MERC_FRAG_COUNT;
        if (string.IsNullOrEmpty(fragId) || n <= 0) return false;

        MercGrowInventory.Add(fragId, n);
        SaveSystem.Instance?.Save();
        // 头像栏那一格的「灰像」判据就是碎片数（BattleUI.TutorialMercDrawn），
        // 抽完必须立刻刷一次，否则要等下一次全量刷新才亮出来 —— 主人会以为没抽到。
        BattleUI.Instance?.UpdateCharacterSlots();
        msg = $"{StoryProgress.TutorialMercNickname}的本命碎片 ×{n}";
        return true;
    }

    /// <summary>
    /// 引导宝箱的开箱产出（2026-10-05 主人拍板）：**天赋石**。
    /// 原来掉一把武器，但开局抽奖已经保底给了装备（木制圆盾），宝箱再掉装备就重复了；
    /// 天赋石是城镇点天赋的硬通货，正好把「开箱 = 长期成长」这一课补上。
    /// 数量就改这一个常量。
    /// </summary>
    const int TutorialChestTalentStones = 3;

    static void GrantTutorialChestTalentStones()
    {
        if (TutorialChestTalentStones <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, TutorialChestTalentStones,
                           save: true, notify: true);
        UIManager.Instance?.ShowToast($"获得天赋石 ×{TutorialChestTalentStones}");
    }

    static EquipInstance CreateTutorialEquipDrop()
    {
        // 宝箱只掉武器，避免误给防具
        // 破旧木剑(equip_training_sword)走「起步武器 70% 普通档」，稀有度强制不生效，
        // 玩家拿到手反而比初始武器弱，爽点会塌。引导宝箱改为优先给常规武器。
        // 2026-09-26：教程宝箱按当前职业发 —— 第一优先读 player_job_base_stats 的「主手模板ID」，
        // 拿到后仍过一遍 PlayerJobDefs.TemplateMatchesJob（按职业/武器类型过滤），
        // 不再让法师、牧师开出剑、斧。
        var job = PlayerJobDefs.GetSelected();
        var prefer = new List<string>();
        PlayerJobBaseStats.TryGet(job, out PlayerJobBaseStats.Row jobRow);
        if (!string.IsNullOrEmpty(jobRow.StarterMainTemplateId))
            prefer.Add(jobRow.StarterMainTemplateId);
        prefer.Add("equip_sword_1");
        prefer.Add("equip_axesmall1");
        prefer.Add("equip_training_sword");
        for (int i = 0; i < prefer.Count; i++)
        {
            EquipTemplate tpl = ConfigManager.Instance != null
                ? ConfigManager.Instance.GetEquipTemplate(prefer[i])
                : null;
            if (tpl == null)
                tpl = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/" + prefer[i]);
            if (tpl == null) continue;
            // 只发本职业能用（武器类型匹配 / 配表有映射）的武器
            if (!PlayerJobDefs.TemplateMatchesJob(tpl, job)) continue;
            tpl.ResolveIcon();
            int lv = Hero.Instance != null ? Hero.Instance.level : 1;
            // 强制稀有：初始武器是普通档(9~12)，稀有档(32~40)才撑得起「换装后 1~2 刀」的爽点
            var eq = EquipInstance.GenerateFromTemplate(tpl, 0, lv, true, Rarity.Rare);
            if (eq != null)
            {
                eq.requireLevel = 1;
                if (eq.icon == null && tpl.icon != null) eq.icon = tpl.icon;
                if (eq.icon == null) eq.icon = EquipIcons.Get(tpl.iconFileName);
                AlignWeaponToHeroAttackHand(eq);
                // 2026-09-27：传模板进去 —— 只给槽位的话，弓/斧也会被随机成剑名（名字和图对不上）。
                eq.equipName = EquipNameGen.RandomWeaponName(tpl, eq.slotType);
                return eq;
            }
        }

        var list = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetRandomEquipInstances(1, 1, attrBonus: GameConfig.RIFT_DROP_ATTR_BONUS)
            : null;
        if (list != null && list.Count > 0)
        {
            var eq = list[0];
            if (eq != null) eq.requireLevel = 1;
            eq?.template?.ResolveIcon();
            if (eq != null && eq.icon == null && eq.template != null)
                eq.icon = eq.template.icon;
            if (eq != null && WeaponLoadoutRules.IsLoadoutItem(eq))
                AlignWeaponToHeroAttackHand(eq);
            if (eq != null && (string.IsNullOrEmpty(eq.equipName) || LooksLikeEnglishFileName(eq.equipName)))
                eq.equipName = EquipNameGen.RandomWeaponName(eq.template, eq.slotType);
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
