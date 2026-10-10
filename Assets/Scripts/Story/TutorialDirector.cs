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
        // 【2026-10-07 主人拍板】引导救场那位 = 每日登录送的塔克（H003 剑盾卫士）。
        // 立绘走花名册 hireId（H003），禁止再用 LaoDun(H001 盾兵) 顶替。
        string rescuerId = StoryProgress.TutorialMercHireId;
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            // 只有塔克一个人说话：不要摆玩家立绘，直接让他单人居中说
            //（约定：单人说台词一律用 Solo，别摆两个立绘站着不说话）
            // 台词把钩子3串起来：①「成功撤离」正反馈（不再是「大难不死」的挫败感框架）
            // ②「带回奖励」让撤离有获得感 ③「我等你回来」种次日留钩子
            // ④「每日签到别落下」继续指向紧接着弹出的每日登录弹窗。
            StoryDirector.Solo(StoryProgress.TutorialMercDisplayName,
                "成功撤离！带回来的奖励够你歇口气。明儿见——我等你回来。每日签到别落下，那儿有我的份。",
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
        yield return CoTeachControls(bm, hint, ui, preloadStep: 1);

        // —— 1) 首波清场后进入宝箱剧情（不再刷第二小波）——
        // 2026-09-27 主人反馈「引导到自动攻击时怪还没出来」→ **先刷怪、再弹提示**。
        // 2026-09-28 再提前：第 1 波已在 CoTeachControls 里刷（preloadStep:1），
        // 借「技能能量满会自动释放」那 3.2 秒读字时间走完进场，这里不再重复刷
        // （EnsureTutorialStep 不看是否已刷过，重复调用会多排一波）。
        TutorialBattleTable.EnsureLoaded();
        // 【2026-10-08 主人拍板】第一波先给一句「热身」的引导，再教自动攻击 ——
        // 主人原话「先打一波热热手什么的」；这句是唯一文案源，想改措辞改这里即可。
        hint.Show("先打一波热热身。靠近怪物会自动攻击。", null, 8f);
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
        // ② 再复用埋伏拍那个「撑不住」的判定（掉血跌破阈值 / 打够时长 / 场上被打空，哪个先到算哪个），
        //    复用而不是新建 —— CoWaitAmbushPressure 原本服务于已删除的埋伏拍，现在是孤儿，正好接手这份活。
        // 【2026-10-09 主人拍板】② 这一步作废：主人原话「当所有怪出场后就出剧情，不要全都上来打了一会才触发」，
        //    即 12 只全部进场站定（上面 ① 的判据）就立刻往下走触发塔克天降，不再等那 5 秒 / 掉血 62% 阈值。
        //    CoWaitAmbushPressure 停用但**函数保留不删**（备用），故此处只注释不删。
        // yield return CoWaitAmbushPressure(bm);

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

        // 第 6~8 步：塔克天降 → 丢下「躲在我后面。」→ 一趟跑完全场，回原位后全体一起死。
        // 这段之前必须把场上所有怪也冻住，否则它们会在塔克跑动时追着玩家改写坐标。
        if (bm != null && bm.monsters != null)
        {
            for (int i = 0; i < bm.monsters.Count; i++)
                HaltUnit(bm.monsters[i]);
        }
        Mercenary rescuer = null;
        // CoTutorialRescueBeat 内部：① 抛物线跳进来（原样保留）② 落地丢一句「躲在我后面。」
        // ③ 逛一圈回原位再统一判死（见下面【改动 E】的改造）。
        // ⚠ 第 7 步「躲在我后面。」放在清场**之前**（写在 CoTutorialRescueBeat 里、落地之后、起跑之前）：
        //   主人定稿的顺序是「天降 → 丢话 → 清场」，先护住玩家再动手才顺 —— 所以这里**不再**重复说第二遍。
        yield return CoTutorialRescueBeat(bm, m => rescuer = m, headTalk);

        // 第 9 步：玩家惊呆（这一句是唯一文案源，想改措辞改这里即可）
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "……他刚才是不是把所有人打了一遍？", 2.4f));
        headTalk?.HideNow();

        // —— 3b) 第二抽：抽中的就是刚才救场那位（塔克·重盾，稀有） ——
        // 【2026-10-07 主人拍板】天降那位 = 这一抽开出来的佣兵，抽完他正式入队。
        // ① 两波后（装备）→ ② 这里（按定序出佣兵）→ ③ 精英波清完（技能）。
        // 保底卡在 SlotMachineSystem.BuildGuaranteedMercCard（碎片那条口径已作废）。
        hint.Hide();
        // 演出替身收掉再弹面板：正式入队走 RunDraftDirector.RecruitMerc，不收会变成两个塔克。
        // 收的时机正好是抽奖面板盖上来的时候，玩家看不到空场。
        if (rescuer != null) MercenaryManager.Instance?.DespawnMercenary(rescuer);
        if (bm != null)
            yield return bm.CoMidBattleDraft(TutorialDrawBeatText);
        ui?.UpdateCharacterSlots();

        // —— 4) 第三波：塔克已在队，跟着一起打 ——
        // 2026-10-07 主人拍板：塔克第 2 抽就招募入队了，原来这段
        //「牧师在前方眩晕被围殴 → 清场 → 三段对白 → 入队」**整段删除**，第 4 拍改成普通波次。
        hint.Hide();
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        // 塔克已经在队里了：这里只从佣兵管理器取场上那个单位（精英宿敌对白 / 压轴 / 撤离要用）
        var activeMercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        var merc = (activeMercs != null && activeMercs.Count > 0) ? activeMercs[0] : null;

        // 【2026-10-07 主人拍板「数据表最好不要为了引导改动」】
        // 这一拍直接用表里现成的 **order=5 普通波**（4 只，含 1 远程），一个数字都不改；
        // 表上含 1 精英的 order=4 留给后面的宿敌戏（见下）。
        bm?.QueueTutorialStep(5);

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

        // 【2026-10-07 主人反馈「佣兵说的话太快了，没看到」】0.75 → 1.9（同上：气泡停留拉长）。
        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "跟紧我，别走散。", 1.9f));
        headTalk?.HideNow();
        hint.Hide();

        // —— 第三次抽（技能）：走正式入口 CoMidBattleDraft ——
        // 2026-10-05 主人拍板「技能就是初始带的那个，只不过这回是抽奖给的」。
        // 保底在正式系统里：本局第一次抽到技能 = 该职业的初始技能（SlotMachineSystem.BuildGuaranteedSkillCard）。
        // ⚠ 原来这里的「固定三选一 + 拖拽定释放顺序」教学拍（CoTutorialSkillDraft）已被这一抽顶掉并删除。
        //   拖拽教学改由 SkillOrderGuide 做软引导：玩家凑够 2 个技能时弹一条会自己消失的气泡，不挡操作。
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
            yield return CoEliteLastWords(bm, headTalk, rival);

        yield return WaitFieldClear(strict: true);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "……", 1.8f),
            new TalkLine(merc, "走吧。", 2.0f));
        headTalk?.HideNow();
        hint.Hide();

        // —— 14)【2026-10-09 主人拍板】结局合并成一波：塔克说完「走吧。」直接回城结算 ——
        // 原来这里还有一拍「压轴夹击 + 让玩家自己点撤离」的教学（tutorial_battle.csv order=6 的那一波），
        // 主人拍板整段删掉 —— 引导的最后一波就是上面的精英宿敌波。
        // 这里先停一下让最后两句台词读完，再自动触发撤离结算（走给引导用的那条 BattleSettlement 结算路线）。
        yield return new WaitForSecondsRealtime(0.6f);
        if (bm != null)
            bm.TriggerEvacuation();
        else
            Debug.LogError("[Tutorial] 引导收尾失败：BattleManager 为空，无法触发回城结算");

        _battleTutorialFlowStarted = false;
        _flow = null;
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
    /// 天降落点：玩家右前方这么远（站到玩家和怪之间）。
    /// 【2026-10-10 主人拍板】2.0 → 1.2（约一个身位）：原来落得太远，看着像跳进怪堆里。
    /// </summary>
    const float RescueLandDist = 1.2f;

    /// <summary>清场挥击的攻击动画倍速（2026-10-10 主人拍板：动作加快，不要只是位移）。</summary>
    const float RescueAttackAnimSpeed = 3f;

    /// <summary>
    /// 等「玩家撑不住」这个时刻 —— 掉血跌破阈值 / 打够时长 / 场上清了，哪个先到算哪个。
    ///
    /// <para>现在由 12 只压场波（tutorial_battle.csv order=7）复用；原本服务的埋伏拍（order=3）已删除。</para>
    ///
    /// <para>为什么不等 <c>WaitFieldClear</c>：主人要的是「被围殴到喊救命」，
    /// 让玩家自己打完就没有天降的理由了（12 只纯近战是照这个意图配的）。</para>
    /// </summary>
    IEnumerator CoWaitAmbushPressure(BattleManager bm)
    {
        // 【2026-10-09 主人拍板】这个等待原本服务于埋伏拍（order=3），埋伏拍删掉后由 12 只压场波（order=7）接手复用：
        // 判定条件不变（掉血跌破阈值 / 撑满时长 / 场上打空，哪个先到算哪个），
        // 目的还是「让玩家在被围殴的时候喊救命」，不是等他打完。
        // 所以这里等的是「玩家扛不住」的时刻，不是等清场：掉血阈值收紧、时长缩短，
        // 保证天降那一刻场上还有怪可杀（玩家真把怪清完了才走打空那条分支）。
        const float maxWait = 5f;
        const float bailHpRatio = 0.62f;
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

        var merc = bm.SpawnTutorialMercAt(null, 1f, RescueEnterDist, stunned: false);
        if (merc == null)
        {
            Debug.LogError("[Tutorial] 塔克天降失败：SpawnTutorialMercAt 返回空（救场演出整段跳过，流程继续）");
            yield break;
        }
        onSpawned?.Invoke(merc);

        float heroX = Hero.Instance != null ? UnitBase.GetCombatX(Hero.Instance) : 0f;
        float z = bm.unitRoot != null ? bm.unitRoot.position.z : merc.transform.position.z;
        // 2026-10-10 主人拍板：从屏幕左侧跳进来（落点不变，仍站玩家右前方护住玩家）。
        float fromX = heroX - RescueEnterDist;
        float toX = heroX + RescueLandDist;

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

        // ②【2026-10-09 主人拍板改造】落地 → 丢一句「躲在我后面。」→ 一趟跑完全场 → 回原位 → 全体一起死。
        //   主人原话：「玩家和怪都没动的情况下，几秒把所有怪打一遍，然后回到原地，然后所有怪一起死亡」。
        //   所以不再是站在原地逐个判死，而是：冲刺到每只怪身侧 → 只落一道闪电（先不判死）→
        //   全部走完跑回起始点 → 停 0.3 秒 → 统一 TakeDamage 判死（尸体动画 / 掉落照旧）。
        //   ⚠ 全程只改塔克自己的世界坐标，其它单位一概不动（2026-09-18 教训：绝不推着英雄走 / 瞬移）。
        //   第 7 步「躲在我后面。」就放在这里（落地之后、起跑之前）：主人定稿的顺序是
        //   「天降 → 丢话 → 清场」，先护住玩家再动手才顺 —— 调用方那一段里不再重复这句。
        if (talk != null)
            yield return TalkBlock(bm, talk, restoreAct: false,
                // 这句是唯一的护崽台词源，想改措辞改这里即可
                new TalkLine(merc, "躲在我后面。", 2.2f));

        var vfx = BattleVFXSystem.Instance;
        var victims = new List<UnitBase>();
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m != null && !m.isDead) victims.Add(m);
        }
        // 按世界 X 从大到小排：塔克从右到左一趟扫过去，不折返。
        victims.Sort((a, b) => UnitBase.GetCombatX(b).CompareTo(UnitBase.GetCombatX(a)));

        // 起始点记下来：清完场要跑回这个点（主人原话「然后回到原地」）。
        float homeX = UnitBase.GetCombatX(merc);
        // 单段冲刺 / 停顿时长按怪数摊：12 只时全程约 2~3 秒，不拖到 5 秒以上。
        int vc = victims.Count > 0 ? victims.Count : 1;
        float dashDur = Mathf.Clamp(1.6f / vc, 0.09f, 0.20f);
        float holdDur = Mathf.Clamp(0.6f / vc, 0.02f, 0.06f);

        // 【2026-10-10 主人拍板】冲刺要有拖尾/残影：起跑前开，回原位后立刻停（到点自动收干净，不留永久对象）。
        float trailDur = (victims.Count + 1) * dashDur + victims.Count * holdDur;
        if (victims.Count > 0) TutorialRescueAfterimage.RunTrail(merc, trailDur);

        float curX = homeX;
        for (int i = 0; i < victims.Count; i++)
        {
            var m = victims[i];
            if (m == null || m.isDead) continue;
            float targetX = UnitBase.GetCombatX(m) + 0.6f;
            yield return CoMoveUnitTo(merc.gameObject, curX, targetX, z, dashDur);
            curX = targetX;
            // 2026-10-10 主人拍板：到位先面向受害者挥一剑（加速攻击动画），再落闪电 —— 不要只是位移。
            merc.Face(UnitBase.GetCombatX(m) >= UnitBase.GetCombatX(merc) ? 1 : -1);
            merc.PlayAttackAnimOnly(AttackVfxKit.MeleeSlash, false, RescueAttackAnimSpeed, forceRestart: true);
            // 到位只播一道闪电，**先不判死**（统一留到回原位之后）
            vfx?.PlayLightning(m.transform.position, VfxFaction.Ally);
            yield return new WaitForSecondsRealtime(holdDur);
        }

        // 回原位（同样补间过去，不瞬移）→ 停 0.3 秒 → 全体一起死
        yield return CoMoveUnitTo(merc.gameObject, curX, homeX, z, dashDur);
        TutorialRescueAfterimage.StopTrail();
        yield return new WaitForSecondsRealtime(0.3f);

        for (int i = 0; i < victims.Count; i++)
        {
            var m = victims[i];
            if (m == null || m.isDead) continue;
            m.TakeDamage(TutorialRescueDamage, true, true, true, 0, merc);
        }
        Debug.Log($"[Tutorial] 塔克天降清场：{victims.Count} 只");

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
