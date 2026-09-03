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

    public bool IsPlaying { get; private set; }

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
        SpeechBubbleTalker.SetSuppressed(false);
        GameBgm.EndBattleStory();
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
        var ui = EnsureUi();
        if (ui == null || beats == null || beats.Count == 0)
        {
            Debug.LogError("[StoryDirector] 无法播放：DialogueUI 或台词为空");
            Finish(onDone, battleStoryBgm);
            yield break;
        }
        PrepareCanvas(ui);
        ui.SetDialogueChromeVisible(false);

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
                        yield return ui.PlayLocationReveal(loc);
                    else
                        yield return ui.PlayLocationReveal("");
                }
                _introducedBg = b.backgroundId;
            }

            bool skipRest = false;
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
                soloCentered: b.soloCentered);
            if (b.speaker == 0)
                ui.SetSpeakerHighlight(0);

            if (hasChoices)
            {
                int picked = -1;
                ui.ShowChoices(b.choices, idx => { picked = idx; });
                while (picked < 0) yield return null;
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

            while (!advanced) yield return null;
            if (skipRest) break;
        }

        if (_keepSceneArt)
        {
            ui.SetDialogueChromeVisible(false);
            ui.SetStoryProp(null);
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
        Time.timeScale = _savedTimeScale;
        _pausedTime = false;
    }

    void Finish(Action onDone, bool battleStoryBgm = false)
    {
        IsPlaying = false;
        _play = null;
        ResumeGame();
        SpeechBubbleTalker.SetSuppressed(false);
        if (battleStoryBgm)
            GameBgm.EndBattleStory();
        onDone?.Invoke();
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
}
