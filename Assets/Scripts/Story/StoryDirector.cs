using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoryBeat
{
    public string leftName = "";
    public string rightName = "";
    public string text;
    public string leftPortraitId;
    public string rightPortraitId;
    /// <summary>-1 左 / 0 旁白 / 1 右</summary>
    public int speaker = 1;
    public string[] choices;
    /// <summary>Resources/Story/Backgrounds 资源 ID；空则沿用上一句。</summary>
    public string backgroundId;
    /// <summary>单人立绘居中（会长/前台小姐等）</summary>
    public bool soloCentered;
    /// <summary>剧情道具叠图（如委托书）；Resources/Story/Props 资源 ID。</summary>
    public string propId;
    /// <summary>换背景时跳过「地点揭示」黑场（开场已有黑幕时用，避免再等几秒）。</summary>
    public bool skipLocationReveal;
    /// <summary>结局终幕：全屏黑屏 + 一句字幕（无立绘 / 无对话框）。见 <see cref="StoryDirector.BlackLine"/>。</summary>
    public bool blackScreen;

    public StoryBeat Bg(string id)
    {
        backgroundId = id;
        return this;
    }

    public StoryBeat Prop(string id)
    {
        propId = id;
        return this;
    }

    public StoryBeat SkipReveal()
    {
        skipLocationReveal = true;
        return this;
    }
}

/// <summary>播 DialogueUI 台词序列。城镇/战斗共用。</summary>
public class StoryDirector : Singleton<StoryDirector>
{
    DialogueUI _ui;
    Coroutine _play;
    float _savedTimeScale = 1f;
    bool _pausedTime;
    string _introducedBg;
    bool _keepSceneArt;
    // 【2026-10-20 主人报】剧情播放时战斗底部 4 个技能框挤在对话窗下面 —— 开播藏掉、收窗还原。
    GameObject _battleSkillBar;
    bool _battleSkillBarWasActive;

    public bool IsPlaying { get; private set; }

    /// <summary>
    /// 剧情自动播放（AUTO，2026-10-19 主人拍板）：会话内记住，DialogueUI 右上按钮切换。
    /// 开启后每句打字播完停 <see cref="AutoAdvanceDelaySec"/> 秒自动下一句，末句播完自动收窗。
    /// 遇到选项仍停下等玩家点 —— 不替玩家做不可逆决定（第 7 章三选一决定结局）；
    /// 但选项等超 <see cref="ChoiceWatchdogSec"/> 秒会自动选第一项放行（见 PlayRoutine，防点击被吞卡死）。
    /// </summary>
    public static bool AutoPlay { get; set; }

    /// <summary>AUTO：打字播完后每句停留时长（unscaled）。</summary>
    const float AutoAdvanceDelaySec = 1.5f;
    /// <summary>AUTO：黑屏字幕行停留时长（unscaled）。</summary>
    const float AutoBlackHoldSec = 2.5f;
    /// <summary>安全网（任何模式）：单行 30 秒无进展强制推进 —— 防点击被异常吞掉时全场定格（主人主诉：卡剧情只能退出重进、体力白扣）。</summary>
    const float LineWatchdogSec = 30f;
    /// <summary>安全网（任何模式）：选项 60 秒无输入自动选第一项放行并告警。</summary>
    const float ChoiceWatchdogSec = 60f;

    public static StoryDirector Ensure()
    {
        _ = Instance;
        return Instance;
    }

    /// <summary>异常残留播放态时强制结束，便于回城收尾剧情再次触发。</summary>
    public void StopPlaying()
    {
        if (_play != null)
        {
            StopCoroutine(_play);
            _play = null;
        }
        IsPlaying = false;
        ResumeGame();
        RestoreBattleSkillBar();
        SpeechBubbleTalker.SetSuppressed(false);
        GameBgm.EndBattleStory();
        // 【2026-10-19 修复】强制终止剧情必须把对话窗收干净：此前只停协程不 Hide，
        // DialogueUI.IsVisible（activeSelf）常驻 true → FirstRunGuide.StoryBusy 误判「剧情在播」
        // → 引导让位分支无超时，在「点角色」等步骤静默卡死（切出回来触发城镇流程重入最易踩）。
        var ui = _ui != null ? _ui : DialogueUI.Instance;
        if (ui != null) ui.Hide();
    }

    DialogueUI EnsureUi()
    {
        if (_ui != null)
        {
            PrepareCanvas(_ui);
            return _ui;
        }
        _ui = DialogueUI.Instance;
        if (_ui == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Dialogue/DialogueUI");
            GameObject go = prefab != null ? Instantiate(prefab) : new GameObject("DialogueUI");
            go.name = "DialogueUI";
            DontDestroyOnLoad(go);
            _ui = go.GetComponent<DialogueUI>();
            if (_ui == null) _ui = go.AddComponent<DialogueUI>();
            if (prefab == null)
            {
                Debug.LogWarning("[StoryDirector] 未找到 DialogueUI 预制体，运行时建树");
                _ui.BuildHierarchyForPrefab();
            }
        }
        PrepareCanvas(_ui);
        return _ui;
    }

    static void PrepareCanvas(DialogueUI ui)
    {
        if (ui == null) return;
        var go = ui.gameObject;
        go.SetActive(true);

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue, UICanvasSetup.ResolveUiCamera());
        // 【2026-10-20 主人拍板「剧情永远置顶」】ApplyPopup 只改 order 不改 layer，剧情窗一直留在 Default 层，
        // 被同层高 order 的东西压（实测技能槽 955 挡住剧情）。挪进全项目最高层 Effects，整层压过一切 Default UI。
        canvas.sortingLayerName = GameConfig.UiSort.TopLayer;
        canvas.pixelPerfect = false;
        canvas.enabled = true;

        ui.PrepareForStoryBeat();
        GameFonts.ApplyToHierarchy(go.transform);
        Debug.Log($"[StoryDirector] DialogueUI 已就绪 camera sort={canvas.sortingOrder} scale={go.transform.localScale}");
    }

    public void Play(IList<StoryBeat> beats, Action onDone, Action<int> onChoice = null, bool keepSceneArt = false)
    {
        _keepSceneArt = keepSceneArt;
        if (_play != null) StopCoroutine(_play);
        _play = StartCoroutine(PlayRoutine(beats, onDone, onChoice));
    }

    public void PlayOne(StoryBeat beat, Action onDone)
    {
        Play(new[] { beat }, onDone);
    }

    public void NotifySceneChanged()
    {
        _introducedBg = null;
    }

    static bool IsBattleScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameSceneManager.BATTLE_SCENE;
    }

    IEnumerator PlayRoutine(IList<StoryBeat> beats, Action onDone, Action<int> onChoice)
    {
        IsPlaying = true;
        bool battleStoryBgm = IsBattleScene();
        if (battleStoryBgm)
            GameBgm.BeginBattleStory();
        SpeechBubbleTalker.SetSuppressed(true);
        PauseGame();
        HideBattleSkillBar();
        var ui = EnsureUi();
        if (ui == null || beats == null || beats.Count == 0)
        {
            Debug.LogError("[StoryDirector] 无法播放：DialogueUI 或台词为空");
            Finish(onDone, battleStoryBgm);
            yield break;
        }
        PrepareCanvas(ui);
        ui.SetDialogueChromeVisible(false);

        // 2026-10-10 修（主人报：起完名字后会长那句话背后是黑的）：
        // 命名界面打开时会调 DialogueUI.Hide()，Hide() 里 SetSceneBackground(null) 把场景背景清掉了；
        // 而送客那句的背景 ID 没变（还是会长办公室），下面「ID 相同就跳过」的分支不会重装背景，
        // 于是露出底下的城镇/战场 —— 首次进游戏那会儿底下也是空的，看着就是一片黑。
        // 场景没换，所以这里只补装画面，不重播地点揭示黑场。
        if (!string.IsNullOrEmpty(_introducedBg) && !ui.HasSceneBackground())
        {
            ui.SetSceneBackground(StoryBackgrounds.Get(_introducedBg));
            ui.SetRevealBlack(0f);
            ui.SetBgDim(0.42f);
            ui.SetSceneBackgroundAlpha(1f);
        }

        for (int i = 0; i < beats.Count; i++)
        {
            var b = beats[i];
            if (b == null) continue;
            bool advanced = false;
            bool hasChoices = b.choices != null && b.choices.Length > 0;

            if (!string.IsNullOrEmpty(b.backgroundId) && b.backgroundId != _introducedBg)
            {
                // 换场景前先去掉上一句道具，再播地点揭示（委托书 → 公会大厅）
                ui.SetStoryProp(null);
                ui.SetSceneBackground(StoryBackgrounds.Get(b.backgroundId));
                if (b.skipLocationReveal)
                {
                    // 只跳过「地点名黑场」，办公室/大厅插图和立绘仍要留下
                    ui.SetRevealBlack(0f);
                    ui.SetBgDim(0.42f);
                    ui.SetSceneBackgroundAlpha(1f);
                    ui.SetDialogueChromeVisible(true);
                }
                else
                {
                    string loc = StoryBackgrounds.DisplayName(b.backgroundId);
                    if (!string.IsNullOrEmpty(loc))
                    {
                        // 2026-10-14 主人拍板：会长办公室/公会大厅的地名大字时间减 30%（缩放真源在 StoryBackgrounds）
                        yield return ui.PlayLocationReveal(loc,
                            StoryBackgrounds.IsFastTextScene(b.backgroundId) ? StoryBackgrounds.FastTextTimeScale : 1f);
                    }
                    else
                        yield return ui.PlayLocationReveal("");
                }
                _introducedBg = b.backgroundId;
            }

            bool skipRest = false;

            if (b.blackScreen)
            {
                // 终幕黑屏：不铺背景、不出立绘，只有黑底 + 一行字幕
                bool blackAdvanced = false;
                ui.ShowEndBlackLine(b.text, () => blackAdvanced = true);
                // AUTO：停留几秒自动放行；任何模式 30 秒无进展强制放行（黑屏也点不动时不能定格全场）
                float blackT = 0f, blackAutoT = 0f;
                while (!blackAdvanced)
                {
                    float dt = Time.unscaledDeltaTime;
                    blackT += dt;
                    if (AutoPlay)
                    {
                        blackAutoT += dt;
                        if (blackAutoT >= AutoBlackHoldSec) break;
                    }
                    if (blackT >= LineWatchdogSec)
                    {
                        Debug.LogWarning("[StoryDirector] 黑屏字幕 30 秒无进展，强制放行（点击可能被异常吞掉）");
                        break;
                    }
                    yield return null;
                }
                continue;
            }

            ui.ShowLine(
                b.leftName,
                b.rightName,
                b.text,
                StoryPortraits.Get(b.leftPortraitId),
                StoryPortraits.Get(b.rightPortraitId),
                StoryProps.Get(b.propId),
                speakerIsInitiator: b.speaker != 1,
                onAdvance: () => { if (!hasChoices) advanced = true; },
                onSkip: () =>
                {
                    if (hasChoices) return;
                    advanced = true;
                    skipRest = true;
                },
                soloCentered: b.soloCentered,
                initiatorPortraitId: b.leftPortraitId,
                otherPortraitId: b.rightPortraitId);
            if (b.speaker == 0)
                ui.SetSpeakerHighlight(0);

            if (hasChoices)
            {
                int picked = -1;
                ui.ShowChoices(b.choices, idx => { picked = idx; });
                // AUTO 也不自动选：选项是玩家互动时刻（第 7 章三选一决定结局，不能替玩家拍板）。
                // 但 60 秒无输入视为点击被异常吞掉 → 自动选第一项放行并告警，绝不把战斗永久定格。
                float pickT = 0f;
                while (picked < 0)
                {
                    pickT += Time.unscaledDeltaTime;
                    if (pickT >= ChoiceWatchdogSec)
                    {
                        Debug.LogWarning("[StoryDirector] 选项 60 秒无输入，自动选第一项放行（点击可能被异常吞掉）");
                        picked = 0;
                        break;
                    }
                    yield return null;
                }
                ui.Hide();
                IsPlaying = false;
                _play = null;
                ResumeGame();
                SpeechBubbleTalker.SetSuppressed(false);
                if (battleStoryBgm)
                    GameBgm.EndBattleStory();
                onChoice?.Invoke(picked);
                yield break;
            }

            // 等待推进：AUTO = 打字播完停 1.5s 自动下一句；任何模式 30 秒无进展强制推进（安全网）。
            float lineT = 0f, autoT = 0f;
            while (!advanced)
            {
                float dt = Time.unscaledDeltaTime;
                lineT += dt;
                if (AutoPlay && ui.IsTypeComplete)
                {
                    autoT += dt;
                    if (autoT >= AutoAdvanceDelaySec)
                    {
                        advanced = true;
                        break;
                    }
                }
                if (lineT >= LineWatchdogSec)
                {
                    Debug.LogWarning("[StoryDirector] 单行 30 秒无进展，强制推进下一句（点击可能被异常吞掉）");
                    advanced = true;
                    break;
                }
                yield return null;
            }
            if (skipRest) break;
        }

        ui.ClearEndBlackLine();
 if (_keepSceneArt)
        {
            ui.SetDialogueChromeVisible(false);
            ui.SetStoryProp(null);
            // 【2026-10-14 主人报「回到城后剧情立绘不消失，一直挡住主界面」】
            // keepSceneArt 保留了场景美术（背景/道具），立绘也一并留着就一直挡在主界面前面。
            // 背景要留、立绘不留 —— 这里只清立绘与名牌，其余走上面两行。
            ui.ClearPortraits();
        }
        else
            ui.Hide();
        Finish(onDone, battleStoryBgm);
    }

    void PauseGame()
    {
        if (_pausedTime) return;
        _savedTimeScale = Time.timeScale;
        if (_savedTimeScale < 0.01f) _savedTimeScale = 1f;
        Time.timeScale = 0f;
        _pausedTime = true;
    }

    void ResumeGame()
    {
        if (!_pausedTime) return;
        // 【2026-10-20 卡顿修复】剧情若在击杀顿帧(0.01)/击杀前摇慢放(0.1)窗口内启动，
        // PauseGame 会把 0.01/0.1 误存进来（保存端的 <0.01f 恰好放行 0.01），恢复后全场
        // timeScale=0.01 → 「打完怪/进图后一卡一卡基本不动」。剧情恢复后战斗只应回全速：
        // 低于 1 一律按 1 恢复，顿帧/慢放由 CombatJuice 自己的协程收尾，剧情不代管。
        Time.timeScale = _savedTimeScale >= 1f ? _savedTimeScale : 1f;
        _pausedTime = false;
    }

    void Finish(Action onDone, bool battleStoryBgm = false)
    {
        IsPlaying = false;
        _play = null;
        ResumeGame();
        RestoreBattleSkillBar();
        SpeechBubbleTalker.SetSuppressed(false);
        if (battleStoryBgm)
            GameBgm.EndBattleStory();
        onDone?.Invoke();
    }

    // —— 剧情期间藏/还原战斗底部技能槽（SkillUI）——
    // 城镇剧情没有 BattleUI：静默跳过。恢复时节点可能已随战斗场景销毁（Unity 假 null），直接放过。
    void HideBattleSkillBar()
    {
        var ui = BattleUI.Instance;
        if (ui == null) return;
        var t = FindChildDeep(ui.transform, "SkillUI");
        if (t == null) return;
        // 已记录过就不再覆盖原状态：剧情被打断重播（Play 里只停旧协程、不走 Finish）时，
        // 若把「已隐藏」记成原状态，播完还原就会把技能槽永远留在隐藏态。
        if (_battleSkillBar != null) return;
        _battleSkillBar = t.gameObject;
        _battleSkillBarWasActive = _battleSkillBar.activeSelf;
        if (_battleSkillBarWasActive) _battleSkillBar.SetActive(false);
    }

    void RestoreBattleSkillBar()
    {
        if (_battleSkillBar == null) return;
        _battleSkillBar.SetActive(_battleSkillBarWasActive);
        _battleSkillBar = null;
    }

    static Transform FindChildDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var r = FindChildDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    public static StoryBeat Line(string left, string right, string text, string leftId, string rightId, int speaker)
    {
        return new StoryBeat
        {
            leftName = left,
            rightName = right,
            text = text,
            leftPortraitId = leftId,
            rightPortraitId = rightId,
            speaker = speaker
        };
    }

    public static StoryBeat Narration(string text)
    {
        return new StoryBeat
        {
            leftName = "",
            rightName = "",
            text = text,
            speaker = 0
        };
    }

    /// <summary>单人对话：只显示一名角色，立绘居中。</summary>
    public static StoryBeat Solo(string speakerName, string text, string portraitId)
    {
        return new StoryBeat
        {
            leftName = "",
            rightName = speakerName ?? "",
            text = text,
            leftPortraitId = null,
            rightPortraitId = portraitId,
            speaker = 1,
            soloCentered = true
        };
    }

    /// <summary>
    /// 结局终幕：全屏黑屏 + 居中一句（无背景、无立绘、无对话框），点屏幕继续。
    /// 放在整段剧情最后一句用；播放收尾时黑屏会被自动收掉。
    /// </summary>
    public static StoryBeat BlackLine(string text)
    {
        return new StoryBeat
        {
            leftName = "",
            rightName = "",
            text = text,
            speaker = 0,
            blackScreen = true
        };
    }
}
