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
    /// <summary>Resources/UI/StoryBg 文件名；空则沿用上一句，整段都空则不盖战场。</summary>
    public string backgroundId;
    /// <summary>单人立绘居中（会长/前台小姐等）</summary>
    public bool soloCentered;
    /// <summary>剧情道具叠图（如委托书）；Resources/UI/StoryProps 文件名。</summary>
    public string propId;

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
}

/// <summary>播 DialogueUI 台词序列。城镇/战斗共用。</summary>
public class StoryDirector : Singleton<StoryDirector>
{
    DialogueUI _ui;
    Coroutine _play;
    float _savedTimeScale = 1f;
    bool _pausedTime;
    string _introducedBg;

    public bool IsPlaying { get; private set; }

    public static StoryDirector Ensure()
    {
        return Instance;
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

    /// <summary>
    /// 剧情窗必须盖住大厅：改 Overlay，不绑相机。预制体 Camera 模式 + scale=0 会完全看不见。
    /// </summary>
    static void PrepareCanvas(DialogueUI ui)
    {
        if (ui == null) return;
        var go = ui.gameObject;
        go.SetActive(true);

        var rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
        }
        else
            go.transform.localScale = Vector3.one;

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        canvas.pixelPerfect = false;
        canvas.enabled = true;

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        if (go.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameFonts.ApplyToHierarchy(go.transform);
        Debug.Log($"[StoryDirector] DialogueUI 已就绪 overlay sort={canvas.sortingOrder} scale={go.transform.localScale}");
    }

    public void Play(IList<StoryBeat> beats, Action onDone, Action<int> onChoice = null)
    {
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

    IEnumerator PlayRoutine(IList<StoryBeat> beats, Action onDone, Action<int> onChoice)
    {
        IsPlaying = true;
        SpeechBubbleTalker.SetSuppressed(true);
        PauseGame();
        var ui = EnsureUi();
        if (ui == null || beats == null || beats.Count == 0)
        {
            Debug.LogError("[StoryDirector] 无法播放：DialogueUI 或台词为空");
            Finish(onDone);
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
                string loc = StoryBackgrounds.DisplayName(b.backgroundId);
                if (!string.IsNullOrEmpty(loc))
                    yield return ui.PlayLocationReveal(loc);
                else
                {
                    // 无地点名也压暗再出窗
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
                onChoice?.Invoke(picked);
                yield break;
            }

            while (!advanced) yield return null;
            if (skipRest) break;
        }

        ui.Hide();
        Finish(onDone);
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

    void Finish(Action onDone)
    {
        IsPlaying = false;
        _play = null;
        ResumeGame();
        SpeechBubbleTalker.SetSuppressed(false);
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
