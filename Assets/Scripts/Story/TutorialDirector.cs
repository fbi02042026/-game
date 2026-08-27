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
        if (StoryDirector.Ensure() != null && StoryDirector.Ensure().IsPlaying) return;

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
        if (StoryProgress.TutorialDone || StoryProgress.TutorialBattleCleared) return;
        // 点「角色/酒馆」等非冒险入口：立刻收掉指「冒险」的手势，避免残留在角色页上
        if (tab != MainNavTab.Adventure)
            TutorialHintUI.Ensure().Hide();
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
        StoryProgress.MarkTutorialIntroDone();
        GuildHallUI.SetTownChromeVisible(true);
        if (adv == null) adv = ResolveAdventureButton();
        if (adv != null) adv.interactable = true;
        yield return null;
        yield return DismissIntroVeil(0.4f);
        DialogueUI.Instance?.Hide();
        ClearTownBlockers();

        if (adv == null) adv = ResolveAdventureButton();
        TutorialHintUI.Ensure().ShowHard("点下方「冒险」，前往裂缝。",
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

        TutorialHintUI.Ensure().ShowHard("点下方「冒险」，前往裂缝。",
            adv != null ? adv.GetComponent<RectTransform>() : null);
        _townFlowBusy = false;
        _flow = null;
    }

    IEnumerator PlayTownIntroDialogue()
    {
        StoryDirector.Instance?.NotifySceneChanged();

        // 办公室 + 咨询台一次播完（换地点时播黑屏地点名）。
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
            StoryDirector.Solo("咨询台小姐",
                "第一次下裂缝？三件事：\n1. 你只管走路，打架会自动打。\n2. 进战斗前先选技能，亮起就能放。\n3. 见好就收，活着才有收益。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall)
        };
        bool done = false;
        StoryDirector.Ensure().Play(beats, () => done = true, keepSceneArt: true);
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

        // —— 1) 一小波清怪（缩短到老盾前的路程）——
        hint.Show("靠近怪物会自动攻击。", null, 8f);
        yield return EnsureTutorialWave(bm, 2);
        yield return WaitFieldClear();

        // —— 2) 空路 → 发现宝箱 ——
        hint.Hide();
        var chestDir = StageClearRewardDirector.Instance;
        if (chestDir == null)
        {
            var go = new GameObject("StageClearRewardDirector");
            chestDir = go.AddComponent<StageClearRewardDirector>();
        }

        var drop = CreateTutorialEquipDrop();
        yield return chestDir.CoTutorialChestDrop(drop, 4.5f);

        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "咦，前面有个宝箱？", 1.2f),
            new TalkLine(Hero.Instance, "打开看看里面有什么。", 1.0f));

        GameObject groundIcon = null;
        yield return chestDir.CoTutorialOpenChest(drop, g => groundIcon = g);

        // 弹窗前不入包：等玩家点「装备/放入背包」再由 EquipDropPopupUI 入包
        if (drop != null)
        {
            bool closed = false;
            if (bm != null) bm.UnitsCanAct = false;
            EquipDropPopupUI.ShowSingle(drop, (_, __) => closed = true);
            while (!closed) yield return null;
            if (bm != null) bm.UnitsCanAct = true;
            BattleUI.Instance?.UpdateBackpackGrid();
        }
        if (groundIcon != null)
            Object.Destroy(groundIcon);

        hint.Show("属性更好就装备，旧的会变成强化材料。", null, 4f);
        yield return new WaitForSecondsRealtime(0.8f);

        // —— 3) 诱饵提示 → 左右埋伏 ——
        hint.Show("小心！有些装备是怪物设下的诱饵。", null, 6f);
        yield return new WaitForSecondsRealtime(0.9f);

        bm.SpawnTutorialFlankAmbush(4);
        // 埋伏必须真出怪，否则 WaitFieldClear 会瞬间通过，后面老盾戏对不上
        {
            int guard = 0;
            while (bm.GetAliveMonsterCount() <= 0 && guard < 8)
            {
                guard++;
                yield return null;
                yield return null;
                if (bm.GetAliveMonsterCount() <= 0)
                    bm.SpawnTutorialFlankAmbush(4);
            }
        }
        yield return TalkBlock(bm, headTalk,
            new TalkLine(Hero.Instance, "糟了！中埋伏了！", 1.1f),
            new TalkLine(Hero.Instance, "只有上了！", 0.9f));

        hint.Show("左右都有怪物，靠近会自动攻击。", null, 5f);
        yield return WaitFieldClear();

        // —— 4) 救援戏：老盾先在前方眩晕被围殴 ——
        hint.Hide();
        ShowMercHud = false;
        ui?.ApplySoloBattleHudPublic();
        ui?.UpdateCharacterSlots();

        var merc = bm.SpawnTutorialMercAt(StoryProgress.TutorialMercId, 0.35f, 3.8f, stunned: true);
        bm.SpawnTutorialAmbushAround(merc, 3);
        hint.Show("前方有人被怪物围住了，走近看看。", null, 6f);

        // 走近老盾时冻住自动走，避免空跑很久/错过对话
        if (bm != null) bm.UnitsCanAct = false;
        float approach = 0f;
        while (approach < 12f)
        {
            approach += Time.unscaledDeltaTime;
            if (Hero.Instance != null && merc != null)
            {
                float dist = Mathf.Abs(UnitBase.GetCombatX(Hero.Instance) - UnitBase.GetCombatX(merc));
                if (dist <= 3.6f) break;
                if (dist > 3.6f && approach > 0.2f)
                {
                    float hx = UnitBase.GetCombatX(Hero.Instance);
                    float mx = UnitBase.GetCombatX(merc);
                    float step = Mathf.Sign(mx - hx) * Mathf.Min(10f * Time.unscaledDeltaTime, Mathf.Abs(mx - hx) - 3.4f);
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

        // 一整段只冻一次，说完再解冻；点一下跳字，再点跳句
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(Hero.Instance, "那是……有人被围住了？", 1.2f),
            new TalkLine(Hero.Instance, "先把这些怪清掉！", 1.0f));

        // 解冻开打；清完立刻再冻，防止自动往前跑错过入队
        if (bm != null) bm.UnitsCanAct = true;
        bm.RetargetAllMonsters(Hero.Instance);
        hint.Show("怪物冲过来了，靠近它们会自动攻击。", null, 5f);
        yield return WaitFieldClear();
        bm.ClearMonsterForcedTargets();
        if (bm != null) bm.UnitsCanAct = false;
        HaltUnit(Hero.Instance);
        HaltUnit(merc);

        // 围殴怪清完：停眩晕动画（仍原地等对话，对话后再入队解控）
        if (merc != null)
            merc.StopTutorialStunAnim();

        // 清完怪再跟老盾对话、入队
        if (merc == null || merc.isDead)
        {
            // 围殴中被清掉时补一只，避免入队戏跳过
            merc = bm.SpawnTutorialMercAt(StoryProgress.TutorialMercId, 0.6f, 2.0f, stunned: false);
        }
        yield return TalkBlock(bm, headTalk, restoreAct: false,
            new TalkLine(merc, "咳……谢了，我差点交代在这儿。", 1.4f),
            new TalkLine(Hero.Instance, "还能走吗？跟我一起撤。", 1.1f),
            new TalkLine(merc, "我叫老盾。行，我跟你。", 1.3f));

        if (merc != null)
        {
            merc.SetTutorialStunned(false);
            if (Hero.Instance != null)
            {
                Vector3 behind = Hero.Instance.transform.position + new Vector3(-1.0f, 0f, 0f);
                behind.y = UnitBase.GROUND_Y;
                GameConfig.SetWorldPosition(merc.gameObject, behind);
            }
            merc.Face(1);
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
        hint.Show("老盾加入了队伍。", null, 2.0f);
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
        const float SkillGuideTimeout = 8f;
        while (!SkillUsedThisStep && wait < SkillGuideTimeout)
        {
            wait += Mathf.Max(0.008f, Time.unscaledDeltaTime);
            if (skillTarget == null || !skillTarget.gameObject.activeInHierarchy)
            {
                skillTarget = ResolvePlayerSkillTarget(ui);
                if (skillTarget != null && wait < 3f)
                    hint.ShowHard("点你的头像放技能，给老盾回血。", skillTarget);
            }
            // 点任意处超过 5 秒仍未放技能 → 直接帮放，别干等
            if (wait >= 5f && (Input.GetMouseButtonDown(0) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
                break;
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
            new TalkLine(merc, "舒服多了！前面交给我挡一阵。", 1.1f),
            new TalkLine(Hero.Instance, "一起走。", 0.8f));
        headTalk?.HideNow();
        hint.Hide();
        if (bm != null) bm.UnitsCanAct = true;

        hint.Show("组队后佣兵会自动战斗，和你一起推进。", null, 8f);
        yield return EnsureTutorialWave(bm, 5);
        if (bm != null && bm.GetAliveMonsterCount() <= 0)
        {
            Debug.LogWarning("[Tutorial] 组队后首波未刷出，紧急补怪");
            bm.QueueTutorialWave(4);
        }
        yield return WaitFieldClear();

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
        hint.ShowHard("点右上角设置，选择「撤离」回城。", settingsRt);

        const float autoEvacuateSeconds = 6f;
        float evacWait = 0f;
        while (WaitingEvacuate && BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun)
        {
            HaltUnit(Hero.Instance);
            HaltUnit(merc);
            evacWait += Time.unscaledDeltaTime;
            if (evacWait >= autoEvacuateSeconds)
            {
                WaitingEvacuate = false;
                BattleManager.Instance?.TriggerEvacuation();
                break;
            }
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

        bool prev = bm == null || bm.UnitsCanAct;
        if (bm != null) bm.UnitsCanAct = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.speaker == null || line.speaker.isDead || string.IsNullOrEmpty(line.text))
                continue;
            yield return talk.CoPlayLine(line.speaker, line.text, line.hold);
        }

        talk.HideNow();
        if (bm == null) yield break;
        bm.UnitsCanAct = restoreAct && prev;
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
        string skill = MercRosterDefs.GetDefaultSkillId(mercId) ?? "ally_shield";
        data.permanentMercs.Add(new MercenaryData
        {
            mercId = mercId,
            displayName = string.IsNullOrEmpty(displayName) ? "老盾" : displayName,
            uid = "tutorial_" + mercId,
            favorLevel = 1,
            level = 1,
            star = 1,
            skillId = skill
        });
        SaveSystem.Instance.Save();
        Debug.Log($"[Tutorial] 老盾已写入 permanentMercs id={mercId}");
        AdventureCodex.MarkMercSeen(
            AdventureLogCatalog.Mercs.Length > 0 ? "H001" : mercId);
        // 同时按 assetId 记
        AdventureCodex.MarkMercSeen(mercId);
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
        // 优先给一件有图标的武器/防具，避免进包看不见
        string[] prefer =
        {
            "equip_sword_1", "equip_axesmall1", "equip_cloth_1", "equip_armor_1"
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
                if (eq.icon == null && tpl.icon != null) eq.icon = tpl.icon;
                if (eq.icon == null) eq.icon = EquipIcons.Get(tpl.iconFileName);
                // 临时中文名，命名规则后续再定
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
            eq?.template?.ResolveIcon();
            if (eq != null && eq.icon == null && eq.template != null)
                eq.icon = eq.template.icon;
            if (eq != null && (string.IsNullOrEmpty(eq.equipName) || LooksLikeEnglishFileName(eq.equipName)))
                eq.equipName = EquipNameGen.RandomWeaponName(eq.slotType);
            return eq;
        }
        return null;
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

    IEnumerator WaitFieldClear()
    {
        var bm = BattleManager.Instance;
        float t = 0f;
        const float timeout = 45f;
        while (bm != null && bm.GetAliveMonsterCount() > 0 && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;
        t = 0f;
        while (bm != null && bm.GetAliveMonsterCount() > 0 && t < 8f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (bm != null && bm.GetAliveMonsterCount() > 0)
            Debug.LogWarning($"[Tutorial] 清场超时仍有怪 alive={bm.GetAliveMonsterCount()}，继续流程");
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
