using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Town.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：城镇流程 —— Notify 入口、开场对白/片头、回城收尾、城镇按钮/Loading 等待

public partial class TutorialDirector
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
            TutorialHintUI.Ensure().ShowHard("点下面的「开始冒险」，选好职业就出发去裂隙啦～", highlight);
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
            TutorialHintUI.Ensure().ShowHard("点下面的「冒险」，出发去裂隙啦～",
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
            TutorialHintUI.Ensure().ShowHard("点下面的「冒险」，出发去裂隙啦～",
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
            // 【2026-10-16 主人拍板】旁白去重复、去拗口（原句与会长台词撞内容）
            StoryDirector.Narration("桌上的委托书还没署名，会长不耐烦地敲着桌子。")
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
        // 【2026-10-16 主人拍板「回城后直接出 不要出现主界面」】按住回城 Loading 不放：
        // 场景就绪、公会页打开、立绘预热的全过程都被 Loading 盖住，等塔克剧情真正弹出再收。
        BattleLoadingOverlay.KeepVisible = true;
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

        // 【2026-10-16 主人拍板·二次提速】「回城后直接出 不要出现主界面」——
        // 硬等 2.2s → 1.0s → 彻底删掉：WaitNotLoading 场景就绪 + 资源预热两帧后立刻弹塔克剧情，
        // 主界面在对话底下自己展开，不再让玩家先盯着主界面等。

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
        // Loading 还盖着 → 对话已同步弹出 → 渲染一帧后放行收 Loading，露出来的直接是塔克剧情
        yield return null;
        BattleLoadingOverlay.KeepVisible = false;
        BattleLoadingOverlay.Hide();
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

        // 【2026-10-17 主人拍板】「局内装备带不出裂隙」提示不再紧跟剧情弹出——
        // 此时首次引导（FirstRunGuide.Talent）正指着底栏角色按钮，提示横幅停在屏幕上方，
        // 两层同时抢屏玩家不知道看哪个。挪到引导收完之后再弹（见下方 MarkTutorialDone 之后）。

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
            TutorialHintUI.Ensure().Show("可以去角色界面看看自己的属性哟～",
                highlight, 12f);
        }

        StoryProgress.MarkTutorialDone();
        // 2026-09-29 主人拍板：引导一结束就把塔克清掉，不让他跟到正式关卡。
        // 引导关撤离走的是 SkipMercHireClearOnEvacuate=true（不清雇佣），所以必须在这里主动清。
        ClearTutorialMerc();

        // 【2026-10-07 主人拍板】引导收尾 → 弹每日登录，把「刚才救场的塔克」和
        //「签到才能领到塔克」串成一条线（主人原话：回城剧情完了后出现每日登录弹窗，这样能连起来）。
        // 入口只有一个 TownSceneBootstrap.TryDailyLoginOnce：进 Town 本来也走它，
        // 只是那时 StoryProgress.TutorialDone 还是 false 会跳过，这里 MarkTutorialDone 之后补一次。
        // 【2026-10-16 主人拍板】流程串行：先等首次引导真正收完（玩家点完「关闭引导」收尾气泡、
        // 游标到 Done）再弹登录；玩家关掉登录后才提示「您有新的邮件」（DailyLoginUI.Hide 冲刷）。
        // 收尾气泡自带 60s 超时放行，这里不会死等。
        while (FirstRunGuide.Instance != null && !FirstRunGuide.Instance.IsFinished)
            yield return new WaitForSeconds(0.25f);

        // 【2026-10-17 主人拍板】引导收完、登录弹窗之前，补弹刚才让位的装备机制提示——
        // 先等它自己消失（6s 自动收）再开每日登录，屏幕上始终只有一件事；
        // 引导早已收完的存档（老玩家）这里就是紧跟着剧情后弹，行为与原节奏一致。
        TutorialHintUI.Ensure().Show("小提示：裂隙里捡的装备带不出来，金币和材料都可以带回去哦～", null, 6f);
        yield return new WaitForSecondsRealtime(6.5f);
        TownSceneBootstrap.TryDailyLoginOnce();

        _townFlowBusy = false;
        _flow = null;
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
