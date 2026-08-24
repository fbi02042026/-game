using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背景音乐：双声道交叉淡入淡出。
/// Resources 路径（不带扩展名）：
/// · Audio/BGM/bgm_login    —— 登录界面
/// · Audio/BGM/bgm_town     —— 主场景（公会大厅等）
/// · Audio/BGM/bgm_tavern   —— 酒吧/酒馆
/// · Audio/BGM/bgm_battle   —— 普通/精英战斗
/// · Audio/BGM/bgm_boss     —— 首领战
/// · Audio/BGM/bgm_special  —— 恢复/锻造/附魔关
/// · Audio/BGM/bgm_story    —— 战斗中剧情对话
/// Loading / 片头等演出期间强制静音；结束后再按当前场景/关卡切回来。
/// </summary>
public static class GameBgm
{
    public enum Track
    {
        None = 0,
        Login,
        Town,
        Tavern,
        Battle,
        Boss,
        Special,
        Story
    }

    const string ResRoot = "Audio/BGM/";
    const float DefaultFade = 0.85f;
    const float DefaultVolume = 0.72f;

    static readonly Dictionary<Track, string> Paths = new Dictionary<Track, string>
    {
        { Track.Login, "bgm_login" },
        { Track.Town, "bgm_town" },
        { Track.Tavern, "bgm_tavern" },
        { Track.Battle, "bgm_battle" },
        { Track.Boss, "bgm_boss" },
        { Track.Special, "bgm_special" },
        { Track.Story, "bgm_story" }
    };

    static AudioSource _a;
    static AudioSource _b;
    static AudioSource _active;
    static Track _current = Track.None;
    static Track _pendingAfterLoading = Track.None;
    static Track _restoreAfterStory = Track.None;
    static int _storyHold;
    static Coroutine _endStoryCo;
    static bool _loadingMuted;
    static bool _cutsceneMuted;
    static Coroutine _fadeCo;
    static BgmRunner _runner;
    static readonly Dictionary<Track, AudioClip> _cache = new Dictionary<Track, AudioClip>();

    public static Track Current => _current;
    public static bool IsLoadingMuted => _loadingMuted;
    public static bool IsCutsceneMuted => _cutsceneMuted;

    /// <summary>按关卡类型选曲：战斗/首领/功能关。</summary>
    public static Track TrackForStage(StageType type)
    {
        switch (type)
        {
            case StageType.Boss: return Track.Boss;
            case StageType.Rest: return Track.Special;
            default: return Track.Battle;
        }
    }

    /// <summary>淡入指定曲目；同曲且正在播就不重复切。</summary>
    public static void Play(Track track, float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        if (_loadingMuted || _cutsceneMuted)
        {
            _pendingAfterLoading = track;
            return;
        }
        if (!GameAudio.MusicEnabled)
        {
            _pendingAfterLoading = track;
            StopImmediate();
            return;
        }
        if (track == Track.None)
        {
            Stop(fadeSeconds);
            return;
        }
        if (track == _current && _active != null && _active.isPlaying)
            return;

        AudioClip clip = LoadClip(track);
        if (clip == null)
        {
            Debug.LogWarning($"[GameBgm] 找不到曲目 {track}，请确认 Resources/{ResRoot}{Paths[track]} 已导入");
            return;
        }

        AudioSource next = (_active == _a) ? _b : _a;
        AudioSource prev = _active;

        next.clip = clip;
        next.loop = true;
        next.volume = 0f;
        next.mute = false;
        next.Play();

        _current = track;
        _pendingAfterLoading = track;
        _active = next;

        if (_fadeCo != null) _runner.StopCoroutine(_fadeCo);
        _fadeCo = _runner.StartCoroutine(CrossFade(prev, next, Mathf.Max(0.05f, fadeSeconds)));
    }

    public static void PlayForStage(StageType type, float fadeSeconds = DefaultFade)
    {
        Play(TrackForStage(type), fadeSeconds);
    }

    /// <summary>战斗剧情对话开始：切到剧情曲；可嵌套，结束时再切回战斗曲。</summary>
    public static void BeginBattleStory(float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        if (_endStoryCo != null)
        {
            _runner.StopCoroutine(_endStoryCo);
            _endStoryCo = null;
        }
        _storyHold++;
        if (_current != Track.Story && _current != Track.None)
            _restoreAfterStory = _current;
        else if (_restoreAfterStory == Track.None)
            _restoreAfterStory = GuessTrackFromScene();
        Play(Track.Story, fadeSeconds);
    }

    /// <summary>战斗剧情对话结束：短延迟后恢复原战斗曲，避免句间来回切。</summary>
    public static void EndBattleStory(float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        _storyHold = Mathf.Max(0, _storyHold - 1);
        if (_storyHold > 0) return;
        if (_endStoryCo != null) _runner.StopCoroutine(_endStoryCo);
        _endStoryCo = _runner.StartCoroutine(RestoreAfterStoryDelay(fadeSeconds));
    }

    static IEnumerator RestoreAfterStoryDelay(float fadeSeconds)
    {
        yield return new WaitForSecondsRealtime(0.28f);
        _endStoryCo = null;
        if (_storyHold > 0 || _loadingMuted) yield break;
        Track want = _restoreAfterStory;
        _restoreAfterStory = Track.None;
        if (want == Track.None || want == Track.Story)
            want = GuessTrackFromScene();
        if (want != Track.None)
            Play(want, fadeSeconds);
    }

    public static void Stop(float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        _current = Track.None;
        if (_fadeCo != null) _runner.StopCoroutine(_fadeCo);
        _fadeCo = _runner.StartCoroutine(FadeOutAll(Mathf.Max(0.05f, fadeSeconds)));
    }

    /// <summary>Loading 期间预登记结束后要播的曲（切场景时用）。</summary>
    public static void SetPending(Track track)
    {
        _pendingAfterLoading = track;
    }

    /// <summary>Loading 开始：立刻淡出并静音，记住要恢复的曲目。</summary>
    public static void MuteForLoading(float fadeSeconds = 0.35f)
    {
        EnsureHost();
        _storyHold = 0;
        _restoreAfterStory = Track.None;
        if (_endStoryCo != null)
        {
            _runner.StopCoroutine(_endStoryCo);
            _endStoryCo = null;
        }
        _loadingMuted = true;
        if (_fadeCo != null) _runner.StopCoroutine(_fadeCo);
        _fadeCo = _runner.StartCoroutine(FadeOutAll(Mathf.Max(0.05f, fadeSeconds), pauseWhenDone: true));
    }

    /// <summary>Loading 结束：按 pending / 当前场景恢复播放。</summary>
    public static void UnmuteAfterLoading(float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        _loadingMuted = false;
        if (!GameAudio.MusicEnabled)
        {
            StopImmediate();
            return;
        }

        if (ShouldDeferTownBgmForIntro())
        {
            _cutsceneMuted = true;
            _pendingAfterLoading = Track.Town;
            StopImmediate();
            return;
        }

        Track want = _pendingAfterLoading;
        if (want == Track.None)
            want = GuessTrackFromScene();

        _current = Track.None; // 强制重新淡入
        if (want != Track.None)
            Play(want, fadeSeconds);
    }

    /// <summary>片头 / 剧情演出开始：淡出 BGM，记住待恢复曲目。</summary>
    public static void MuteForCutscene(float fadeSeconds = 0.25f)
    {
        EnsureHost();
        if (_cutsceneMuted) return;
        _cutsceneMuted = true;
        RememberPendingTrack();
        if (_fadeCo != null) _runner.StopCoroutine(_fadeCo);
        _fadeCo = _runner.StartCoroutine(FadeOutAll(Mathf.Max(0.05f, fadeSeconds), pauseWhenDone: true));
    }

    /// <summary>片头 / 剧情演出结束：按 pending 恢复 BGM。</summary>
    public static void UnmuteAfterCutscene(float fadeSeconds = DefaultFade)
    {
        EnsureHost();
        if (!_cutsceneMuted) return;
        _cutsceneMuted = false;
        if (_loadingMuted) return;
        if (!GameAudio.MusicEnabled)
        {
            StopImmediate();
            return;
        }

        Track want = _pendingAfterLoading;
        if (want == Track.None)
            want = GuessTrackFromScene();

        _current = Track.None;
        if (want != Track.None)
            Play(want, fadeSeconds);
    }

    /// <summary>音乐总开关变化时调用：关=静音，开=按 pending 恢复。</summary>
    public static void OnMusicToggleChanged()
    {
        EnsureHost();
        if (!GameAudio.MusicEnabled)
        {
            if (_a != null) { _a.mute = true; _a.volume = 0f; }
            if (_b != null) { _b.mute = true; _b.volume = 0f; }
            return;
        }
        if (_loadingMuted || _cutsceneMuted) return;
        Track want = _pendingAfterLoading != Track.None ? _pendingAfterLoading : _current;
        if (want == Track.None) want = GuessTrackFromScene();
        _current = Track.None;
        if (want != Track.None) Play(want, 0.4f);
    }

    static Track GuessTrackFromScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name == GameSceneManager.BOOT_SCENE)
            return Track.Login;
        if (scene.name == GameSceneManager.TOWN_SCENE)
            return Track.Town;
        if (scene.name == GameSceneManager.BATTLE_SCENE)
        {
            var st = BattleManager.Instance?.currentStage;
            if (st != null) return TrackForStage(st.type);
            return Track.Battle;
        }
        return Track.None;
    }

    static void RememberPendingTrack()
    {
        if (_pendingAfterLoading != Track.None) return;
        if (_current != Track.None)
            _pendingAfterLoading = _current;
        else
            _pendingAfterLoading = GuessTrackFromScene();
    }

    static bool ShouldDeferTownBgmForIntro()
    {
        if (StoryProgress.TutorialDone || StoryProgress.TutorialIntroDone) return false;
        if (StoryProgress.OpeningIntroPlayed) return false;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return scene.name == GameSceneManager.TOWN_SCENE;
    }

    static IEnumerator CrossFade(AudioSource from, AudioSource to, float seconds)
    {
        float t = 0f;
        float fromStart = from != null ? from.volume : 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float e = Mathf.Clamp01(t);
            if (to != null) to.volume = DefaultVolume * e;
            if (from != null && from != to) from.volume = fromStart * (1f - e);
            yield return null;
        }
        if (to != null) to.volume = DefaultVolume;
        if (from != null && from != to)
        {
            from.Stop();
            from.clip = null;
            from.volume = 0f;
        }
        _fadeCo = null;
    }

    static IEnumerator FadeOutAll(float seconds, bool pauseWhenDone = false)
    {
        float a0 = _a != null ? _a.volume : 0f;
        float b0 = _b != null ? _b.volume : 0f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float e = 1f - Mathf.Clamp01(t);
            if (_a != null) _a.volume = a0 * e;
            if (_b != null) _b.volume = b0 * e;
            yield return null;
        }
        if (pauseWhenDone)
        {
            if (_a != null) { _a.Pause(); _a.volume = 0f; }
            if (_b != null) { _b.Pause(); _b.volume = 0f; }
        }
        else
            StopImmediate();
        _fadeCo = null;
    }

    static void StopImmediate()
    {
        if (_a != null) { _a.Stop(); _a.clip = null; _a.volume = 0f; }
        if (_b != null) { _b.Stop(); _b.clip = null; _b.volume = 0f; }
        _active = null;
        _current = Track.None;
    }

    static AudioClip LoadClip(Track track)
    {
        if (_cache.TryGetValue(track, out var cached) && cached != null)
            return cached;
        if (!Paths.TryGetValue(track, out string name)) return null;
        var clip = Resources.Load<AudioClip>(ResRoot + name);
        if (clip != null) _cache[track] = clip;
        return clip;
    }

    static void EnsureHost()
    {
        if (_runner != null) return;
        var go = new GameObject("GameBgm");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<BgmRunner>();
        _a = go.AddComponent<AudioSource>();
        _b = go.AddComponent<AudioSource>();
        SetupSource(_a);
        SetupSource(_b);
        _active = _a;
    }

    static void SetupSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.priority = 0;
    }

    sealed class BgmRunner : MonoBehaviour { }
}
