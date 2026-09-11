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

    public bool ShowMercHud { get; private set; }
    public bool WaitingEvacuate { get; set; }
    public bool SkillUsedThisStep { get; private set; }
    /// <summary>引导战斗：仅 heal 步骤允许点头像放技能。</summary>
    public bool AllowBattleSkillClick { get; private set; }

    bool _townFlowBusy;
    bool _battleTutorialFlowStarted;
    bool _extraHintShown;
    Coroutine _flow;

    public static bool IsTutorialBattle =>
        BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;

    public void NotifyTownReady()
    {
        if (_townFlowBusy) return;
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
        TutorialHintUI.Ensure().ShowHard("点下方「开始冒险」，选择职业后进入裂隙。", highlight);
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
                "新人，森林层最近有些怪物躁动。去吧，证明你有资格留下。",
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
        string fullPath = Path.Combine(Application.dataPath, OpeningIntroRelativePath);
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
            StoryDirector.Line("你", "小白",
                "大难不死……回城歇歇吧。需要治疗的话，来酒馆找我。",
                StoryPortraits.Player, xiaobaiId, 1)
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

        var nav = MainBottomNav.Instance;
        RectTransform highlight = null;
        if (nav != null && nav.characterButton != null)
            highlight = nav.characterButton.GetComponent<RectTransform>();
        // 硬引导：指 BottomNav 角色入口
        TutorialHintUI.Ensure().Show("可以去角色界面查看属性",
            highlight, 12f);

        StoryProgress.MarkTutorialDone();
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
        yield return CoTeachControls(bm, hint, ui);

        // —— 1) 首波清场后进入宝箱剧情（不再刷第二小波）——
        hint.Show("靠近怪物会自动攻击。", null, 8f);
        if (bm != null) bm.UnitsCanAct = true;
        TutorialBattleTable.EnsureLoaded();
        yield return EnsureTutorialStep(bm, 1);
        yield return WaitFieldClear();

        yield return WaitFieldClear(strict: true);
        if (bm != null && bm.GetAliveMonsterCount() > 0)
        {
            hint.Show("先把剩下的怪清掉。", null, 3f);
            yield return WaitFieldClear(strict: true);
        }

        // —— 2) 宝箱陷阱：发现 → 左右埋伏 → 清场 → 开箱拿剑 ——
        hint.Hide();
        var chestDir = StageClearRewardDirector.Instance;
        if (chestDir == null)
        {
            var go = new GameObject("StageClearRewardDirector");
            chestDir = go.AddComponent<StageClearRewardDirector>();
        }
        chestDir.CacheSceneRefs();

        var drop = CreateTutorialEquipDrop();
        if (bm != null) bm.UnitsCanAct = false;
        hint.Hide();
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
                new TalkLine(Hero.Instance, "！", 0.55f));
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
        yield return chestDir.CoTutorialOpenChestAndDropEquip(drop, g => groundIcon = g);

        if (drop != null)
        {
            bool closed = false;
            bool lootDone = false;
            BattleLootMode.Enter(() => lootDone = true);
            EquipDropPopupUI.ShowSingle(drop, (_, equipped) =>
            {
                closed = true;
                if (groundIcon != null)
                {
                    Object.Destroy(groundIcon);
                    groundIcon = null;
                }
            });
            while (!closed) yield return null;
            // 替换已自动 Confirm；若丢弃/关窗未 Confirm 则等确定或超时放行
            float waitLoot = 0f;
            while (!lootDone && waitLoot < 120f)
            {
                waitLoot += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!lootDone)
                BattleLootMode.Confirm();
            if (bm != null)
            {
                bm.UnitsCanAct = true;
                bm.BeginTutorialPowerFantasy();
            }
            BattleUI.Instance?.UpdateBackpackGrid();
            Hero.Instance?.costumeManager?.RefreshCostume();
        }
        if (groundIcon != null)
            Object.Destroy(groundIcon);

        hint.Show("属性更好就装备，旧的会变成强化材料。", null, 1.8f);
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
        }
        bm?.QueueTutorialStep(4, forcedTarget: merc);
        hint.Show("前方有人被怪物围住了，上前帮忙。", null, 4f);

        // 不冻结战斗：玩家可随时上前清怪
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
                if (approach > 0.08f)
                {
                    float hx = UnitBase.GetCombatX(Hero.Instance);
                    float mx = UnitBase.GetCombatX(merc);
                    float step = Mathf.Sign(mx - hx) * Mathf.Min(18f * Time.unscaledDeltaTime, Mathf.Max(0f, dist - 3.6f));
                    if (Mathf.Abs(step) > 0.001f)
                    {
                        var p = Hero.Instance.transform.position;
                        p.x += step;
                        GameConfig.SetWorldPosition(Hero.Instance.gameObject, p);
                    }
                }
            }
            yield return null;
        }

        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "先把围殴她的怪清掉！", 0.75f));

        // 解冻开打；清完立刻再冻，防止自动往前跑错过入队
        if (bm != null) bm.UnitsCanAct = true;
        bm.RetargetAllMonsters(Hero.Instance);
        hint.Show("怪物冲过来了，靠近它们会自动攻击。", null, 5f);
        yield return WaitFieldClear();
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
            merc.currentHp = merc.attr.GetAttr(AttrType.MaxHp);

        ui?.UpdateCharacterSlots();
        AllowBattleSkillClick = false;
        if (bm != null) bm.UnitsCanAct = true;

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "我在后面托着，一起上。", 0.75f));
        headTalk?.HideNow();
        hint.Hide();
        if (bm != null) bm.UnitsCanAct = true;

        hint.Show("组队后佣兵会自动战斗。", null, 3f);
        yield return EnsureTutorialStep(bm, 5);
        yield return WaitFieldClear(strict: true);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "这波清完了，先撤？", 1.2f),
            new TalkLine(merc, "嗯，回城我给你疗个痛快。", 1.2f));
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
        // 教程：写入临时雇佣；图鉴仍 MarkMercSeen
        data.hiredMercs ??= new System.Collections.Generic.List<MercenaryData>();
        data.hiredMercs.Add(entry);
        // 兼容旧逻辑：也记一条 permanent（不用于出战优先）
        data.permanentMercs.Add(entry);
        SaveSystem.Instance.Save();
        Debug.Log($"[Tutorial] 牧师已写入 permanentMercs id={mercId}");
        AdventureCodex.MarkMercSeen(StoryProgress.TutorialMercHireId);
        AdventureCodex.MarkMercSeen(mercId);
    }

    /// <summary>进战后先教摇杆与自动技能。</summary>
    static IEnumerator CoTeachControls(BattleManager bm, TutorialHintUI hint, BattleUI ui)
    {
        if (bm != null) bm.UnitsCanAct = true;
        BattleJoystick.EnsureOn(ui != null ? ui.transform : null);
        BattleJoystick.Instance?.SetVisible(true);
        RectTransform stickRt = BattleJoystick.Instance != null
            ? BattleJoystick.Instance.StickHighlight
            : null;

        hint.Show("下方滑动移动，松手自动锁敌。", stickRt, -1f);
        float waitJoy = 0f;
        while (BattleJoystick.Instance == null || !BattleJoystick.Instance.IsHeld)
        {
            waitJoy += Time.unscaledDeltaTime;
            if (waitJoy > 15f) break;
            yield return null;
        }
        hint.Hide();

        hint.Show("技能能量满会自动释放，无需点击。", null, 3.5f);
        yield return new WaitForSecondsRealtime(3.2f);
        hint.Hide();
        BattleJoystick.Instance?.ResetStickIdle();

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

    static EquipInstance CreateTutorialEquipDrop()
    {
        // 宝箱只掉武器，避免误给防具
        string[] prefer =
        {
            "equip_training_sword", "equip_sword_1", "equip_axesmall1"
        };
        for (int i = 0; i < prefer.Length; i++)
        {
            EquipTemplate tpl = ConfigManager.Instance != null
                ? ConfigManager.Instance.GetEquipTemplate(prefer[i])
                : null;
            if (tpl == null)
                tpl = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/" + prefer[i]);
            if (tpl == null) continue;
            tpl.ResolveIcon();
            int lv = Hero.Instance != null ? Hero.Instance.level : 1;
            var eq = EquipInstance.GenerateFromTemplate(tpl, 0, lv);
            if (eq != null)
            {
                eq.requireLevel = 1;
                if (eq.icon == null && tpl.icon != null) eq.icon = tpl.icon;
                if (eq.icon == null) eq.icon = EquipIcons.Get(tpl.iconFileName);
                AlignWeaponToHeroAttackHand(eq);
                eq.equipName = EquipNameGen.RandomWeaponName(eq.slotType);
                return eq;
            }
        }

        var list = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetRandomEquipInstances(1, 1)
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
                eq.equipName = EquipNameGen.RandomWeaponName(eq.slotType);
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
                    if (mon.IsEnteringMap)
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
            if (alive > 0)
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
