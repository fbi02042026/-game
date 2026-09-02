using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新手引导：城镇开场 → 短战斗（诱饵/老盾/强制撤离）→ 回城收尾。
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
        // 新手首次点「冒险」直接进战斗，不在冒险页弹技能说明。
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

    /// <summary>首次引导：点底栏「冒险」跳过选关页，直接进入教程战斗。</summary>
    public bool TryEnterTutorialBattleFromNav()
    {
        if (StoryProgress.TutorialDone || StoryProgress.TutorialBattleCleared) return false;
        if (!StoryProgress.TutorialIntroDone) return false;

        TutorialHintUI.Ensure().Hide();

        if (!StaminaSystem.TrySpendForAdventure())
        {
            UIManager.Instance?.ShowToast("体力不足");
            return true;
        }

        StoryProgress.QueueTutorialBattle();
        StoryProgress.ResetTutorialRunInventoryIfNeeded();
        AdventureUI.PendingBattleChapter = 1;
        AdventureUI.PendingBattleDifficulty = 0;
        AdventureUI.PendingGoldDungeon = false;
        ChapterManager.Instance?.SetChapter(1);
        GameSceneManager.Instance?.LoadBattleScene();
        return true;
    }

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
        StoryAssetLoader.Warmup(StoryAssetLoader.Portraits,
            StoryPortraits.GuildMaster, StoryPortraits.Receptionist, StoryPortraits.Player);

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
                "第一次下裂隙？三件事：\n1. 你只管走路，打架会自动打。\n2. 进战斗前先选技能，亮起就能放。\n3. 见好就收，活着才有收益。",
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
        StoryAssetLoader.Warmup(StoryAssetLoader.Portraits,
            StoryPortraits.Player, StoryPortraits.LaoDun, StoryPortraits.Receptionist);
        yield return null;

        yield return new WaitForSecondsRealtime(2.2f);

        bool done = false;
        DialogueUI.Instance?.PrepareForStoryBeat();
        StoryDirector.Ensure().Play(new List<StoryBeat>
        {
            StoryDirector.Line("你", "老盾",
                "大难不死得去酒馆喝一杯才行，需要我的话来酒馆找我吧。",
                StoryPortraits.Player, StoryPortraits.LaoDun, 1)
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
        TutorialHintUI.Ensure().Show("可以先去人物界面看看属性。之后再进第一章，才是正式任务。",
            highlight, 8f);

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

        // —— 1) 首波 → 预告下一波 → 约 2 秒后第二小波 ——
        hint.Show("靠近怪物会自动攻击。", null, 8f);
        TutorialBattleTable.EnsureLoaded();
        bm?.ApplyTutorialBattleStep(1);
        yield return EnsureTutorialWave(bm, TutorialStepCount(1));
        yield return WaitFieldClear();
        yield return CoTutorialNextWave(bm, hint, 0f, 2);
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

        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "！", 0.55f));

        float chestX = chestDir.ChestWorldX;
        var flankStep = TutorialBattleTable.GetStepOrDefault(3);
        bm.ApplyTutorialBattleStep(3);
        bm.SpawnTutorialFlankAmbush(flankStep.count, chestX);
        {
            int guard = 0;
            while (bm.GetAliveMonsterCount() <= 0 && guard < 8)
            {
                guard++;
                yield return null;
                yield return null;
                if (bm.GetAliveMonsterCount() <= 0)
                    bm.SpawnTutorialFlankAmbush(flankStep.count, chestX);
            }
        }

        if (bm != null) bm.UnitsCanAct = true;
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
            EquipDropPopupUI.ShowSingle(drop, (_, __) => closed = true);
            while (!closed) yield return null;
            if (bm != null) bm.UnitsCanAct = true;
            BattleUI.Instance?.UpdateBackpackGrid();
            Hero.Instance?.costumeManager?.RefreshCostume();
        }
        if (groundIcon != null)
            Object.Destroy(groundIcon);

        hint.Show("属性更好就装备，旧的会变成强化材料。", null, 1.8f);
        yield return new WaitForSecondsRealtime(0.2f);

        // —— 4) 救援戏：老盾先在前方眩晕被围殴 ——
        hint.Hide();
        ShowMercHud = false;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        var rescueStep = TutorialBattleTable.GetStepOrDefault(4);
        bm.ApplyTutorialBattleStep(4);
        string rescueMercId = string.IsNullOrEmpty(rescueStep.mercId)
            ? StoryProgress.TutorialMercId
            : rescueStep.mercId;
        float rescueHpRatio = rescueStep.mercHpRatio > 0f ? rescueStep.mercHpRatio : 0.35f;
        float rescueAhead = rescueStep.aheadDist > 0f ? rescueStep.aheadDist : 5.5f;
        var merc = bm.SpawnTutorialMercAt(rescueMercId, rescueHpRatio, rescueAhead, stunned: rescueStep.stunned);
        int ambushCount = rescueStep.count > 0 ? rescueStep.count : 3;
        if (rescueStep.eliteCount > 0)
        {
            yield return TalkBlock(bm, headTalk, restoreAct: false,
                new TalkLine(Hero.Instance, "有个块头更大的！", 0.75f));
        }
        bm.SpawnTutorialAmbushAround(merc, ambushCount);
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
            new TalkLine(Hero.Instance, "先把围殴他的怪清掉！", 0.75f));

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
            new TalkLine(merc, "我叫老盾。行，我跟你。", 1.3f));

        string joinName = "老盾";
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
            EnsureTutorialMercPermanent(StoryProgress.TutorialMercId, "老盾");
            Debug.Log("[Tutorial] 老盾入队完成");
        }
        else
            Debug.LogError("[Tutorial] 老盾入队失败：merc 为空");

        ShowMercHud = true;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();
        if (ui != null)
            ui.StartCoroutine(CoRefreshMercHudNextFrame(ui));
        hint.Show($"{joinName}加入了队伍。", null, 2.0f);
        UIManager.Instance?.ShowToast($"{joinName}加入队伍！");
        yield return new WaitForSecondsRealtime(0.6f);

        // —— 治疗技能：软提示 + 短超时，不长期冻死全场 ——
        bm.FillPlayerSkillEnergy();
        SkillUsedThisStep = false;
        AllowBattleSkillClick = true;
        bm.UnitsCanAct = false;

        RectTransform skillTarget = ResolvePlayerSkillTarget(ui);
        // 有明确目标才硬挖空；没有就软提示，避免全屏黑罩把流程卡死
        if (skillTarget != null)
            hint.ShowHard("点你的头像放技能，给老盾回血。", skillTarget);
        else
            hint.Show("点左下角你的头像放技能，给老盾回血。", null, -1f);

        float wait = 0f;
        const float SkillGuideTimeout = 4f;
        while (!SkillUsedThisStep && wait < SkillGuideTimeout)
        {
            wait += Mathf.Max(0.008f, Time.unscaledDeltaTime);
            if (skillTarget == null || !skillTarget.gameObject.activeInHierarchy)
            {
                skillTarget = ResolvePlayerSkillTarget(ui);
                if (skillTarget != null && wait < 2f)
                    hint.ShowHard("点你的头像放技能，给老盾回血。", skillTarget);
            }
            if (wait >= 2.5f) break;
            yield return null;
        }
        if (!SkillUsedThisStep)
        {
            bm.FillPlayerSkillEnergy();
            bm.TryUsePlayerSkill();
        }
        AllowBattleSkillClick = false;
        hint.Hide();
        bm.UnitsCanAct = true;

        if (merc != null && !merc.isDead)
            merc.currentHp = merc.attr.GetAttr(AttrType.MaxHp);

        ui?.UpdateCharacterSlots();

        yield return TalkBlock(bm, headTalk,
            new TalkLine(merc, "舒服多了，前面我来挡。", 0.75f));
        headTalk?.HideNow();
        hint.Hide();
        if (bm != null) bm.UnitsCanAct = true;

        hint.Show("组队后佣兵会自动战斗。", null, 3f);
        bm.ApplyTutorialBattleStep(5);
        yield return EnsureTutorialWave(bm, TutorialStepCount(5));
        if (bm != null && bm.GetAliveMonsterCount() <= 0)
        {
            Debug.LogWarning("[Tutorial] 组队后首波未刷出，紧急补怪");
            var emergencyStep = TutorialBattleTable.GetStepOrDefault(6);
            bm.ApplyTutorialBattleStep(6);
            bm.SpawnTutorialFlankAmbush(emergencyStep.count);
        }
        yield return WaitFieldClear(strict: true);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "这波清完了，先撤？", 1.2f),
            new TalkLine(merc, "行，回城我请你喝一杯。", 1.2f));
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
        var entry = new MercenaryData
        {
            mercId = mercId,
            displayName = string.IsNullOrEmpty(displayName) ? "老盾" : displayName,
            nickname = "老盾",
            hireId = "H001",
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
        Debug.Log($"[Tutorial] 老盾已写入 permanentMercs id={mercId}");
        AdventureCodex.MarkMercSeen(
            AdventureLogCatalog.Mercs.Length > 0 ? "H001" : mercId);
        // 同时按 assetId 记
        AdventureCodex.MarkMercSeen(mercId);
    }

    static int TutorialStepCount(int order) => TutorialBattleTable.GetStepOrDefault(order).count;

    /// <summary>清场后：教程内直接刷下一波，不播正式关的波次预告（避免剩最后一只怪时卡住感）。</summary>
    static IEnumerator CoTutorialNextWave(BattleManager bm, TutorialHintUI hint, float delaySec, int order)
    {
        hint.Hide();
        bm?.ApplyTutorialBattleStep(order);
        yield return EnsureTutorialWave(bm, TutorialStepCount(order));
    }

    /// <summary>
    /// 教程刷怪兜底：最多重试 3 次，每次等两帧看有没有真出怪。
    /// 之前只 yield 一帧就判断，刷怪协程还没跑完就被当成「没刷出来」。
    /// </summary>
    static IEnumerator EnsureTutorialWave(BattleManager bm, int count)
    {
        if (bm == null) yield break;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bm.QueueTutorialWave(count);
            yield return null;
            yield return null;
            if (bm.GetAliveMonsterCount() > 0)
            {
                Debug.Log($"[Tutorial] 刷怪成功 attempt={attempt + 1} alive={bm.GetAliveMonsterCount()}");
                yield break;
            }
            Debug.LogWarning($"[Tutorial] 刷怪未出怪 attempt={attempt + 1}，重试");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Debug.LogError("[Tutorial] 连续 3 次刷怪失败，跳过本波以免卡流程");
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
