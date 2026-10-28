using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 首次进城镇播放的片头视频遮罩。点击可跳过，播完自毁；视频自带音轨，期间仅关 BGM。
/// </summary>
public class OpeningIntroOverlay : MonoBehaviour
{
    public static OpeningIntroOverlay Instance { get; private set; }

    public bool IsFinished { get; private set; }

    public static bool IsPlaying =>
        Instance != null && Instance.gameObject.activeInHierarchy && !Instance.IsFinished;

    CanvasGroup _group;
    VideoPlayer _videoPlayer;
    AudioSource _videoAudio;
    RawImage _rawImage;
    RenderTexture _renderTexture;
    AspectRatioFitter _aspectFitter;
    bool _skipRequested;
    bool _bgmMutedForCutscene;
    bool _fpsBoosted;
    int _prevTargetFps;
    int _prevVSync;

    // 黑屏优化：后台预热的播放器（APIOnly 不渲染），Show 时整体接管
    static VideoPlayer _prewarmPlayer;
    static GameObject _prewarmRoot;

    /// <summary>
    /// 提前建播放器后台 Prepare，把 Prepare 耗时藏进进城镇 Loading 的时间里，
    /// 消除首次进城镇片头前的纯黑秒数。路径不合法直接不动。
    /// </summary>
    public static void Prewarm(string videoPath)
    {
        if (_prewarmPlayer != null) return;
        bool pathOk = !string.IsNullOrEmpty(videoPath)
                      && (videoPath.Contains("://") || File.Exists(videoPath));
        if (!pathOk) return;
        _prewarmRoot = new GameObject("OpeningIntroPrewarm");
        DontDestroyOnLoad(_prewarmRoot);
        var player = _prewarmRoot.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.APIOnly;
        player.source = VideoSource.Url;
        player.url = videoPath;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.Stop();
        player.Prepare();
        _prewarmPlayer = player;
    }

    public static OpeningIntroOverlay Show(string videoPath)
    {
        // 2026-09-28：Android 的 StreamingAssets 是 jar:// URL，File.Exists 对 URL 恒 false，
        // 会把真机片头静默跳掉 —— URL 一律视为存在，交给 VideoPlayer 自己报错。
        bool pathOk = !string.IsNullOrEmpty(videoPath)
                      && (videoPath.Contains("://") || File.Exists(videoPath));
        if (!pathOk)
        {
            Debug.LogWarning("[OpeningIntro] 片头文件不存在: " + videoPath);
            return null;
        }

        var leftovers = Object.FindObjectsOfType<OpeningIntroOverlay>();
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] != null)
                Object.Destroy(leftovers[i].gameObject);
        }

        // 接管预热播放器；URL 对不上就废弃预热走老路
        VideoPlayer adopted = _prewarmPlayer;
        _prewarmPlayer = null;
        _prewarmRoot = null;
        if (adopted != null && adopted.url != videoPath)
        {
            Destroy(adopted.gameObject);
            adopted = null;
        }

        GameObject root = adopted != null ? adopted.gameObject : new GameObject("OpeningIntroOverlay");
        root.name = "OpeningIntroOverlay";
        var driver = root.AddComponent<OpeningIntroOverlay>();
        driver.Build(videoPath, adopted);
        driver.BeginCutsceneMute();
        driver.StartCoroutine(driver.RunRoutine());
        return driver;
    }

    void BeginCutsceneMute()
    {
        if (_bgmMutedForCutscene) return;
        _bgmMutedForCutscene = true;
        GameBgm.MuteForCutscene(0.2f);
    }

    void ReleaseCutsceneMute()
    {
        if (!_bgmMutedForCutscene) return;
        _bgmMutedForCutscene = false;
        GameBgm.UnmuteAfterCutscene(0.55f);
    }

    void Build(string videoPath, VideoPlayer adopted)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        BoostFramePaceForVideo();

        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;

        var bgGo = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.GetComponent<Image>();
        // 保留纯黑依据：首次进城镇的片头视频遮罩（垫在视频 RawImage 下，仅 prepare/淡隐瞬间可见），
        // 开场演出期间城镇 UI 全部隐藏，底下无 UI 可遮；纯黑是为避免露出未加载完的空场景。
        bg.color = Color.black;
        bg.raycastTarget = true;
        Stretch(bg.rectTransform);

        var videoGo = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        videoGo.transform.SetParent(transform, false);
        _rawImage = videoGo.GetComponent<RawImage>();
        _rawImage.color = Color.white;
        _rawImage.raycastTarget = false;
        UiLayoutStretch.ApplyEnvelopeRawImage(_rawImage.rectTransform, 9f / 16f);
        _aspectFitter = videoGo.GetComponent<AspectRatioFitter>();
        _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        _aspectFitter.aspectRatio = 9f / 16f;

        RecreateRenderTexture(720, 1280);

        if (adopted != null)
        {
            // 复用后台已 Prepare 的播放器，只把渲染目标接上
            _videoPlayer = adopted;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
        }
        else
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = videoPath;
            _videoPlayer.isLooping = false;
            _videoPlayer.skipOnDrop = true;
        }

        _videoAudio = gameObject.AddComponent<AudioSource>();
        _videoAudio.playOnAwake = false;
        _videoAudio.loop = false;
        _videoAudio.spatialBlend = 0f;
        _videoAudio.volume = GameAudio.AudioEnabled ? 1f : 0f;
        _videoAudio.mute = !GameAudio.AudioEnabled;

        _videoPlayer.controlledAudioTrackCount = 1;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _videoPlayer.SetTargetAudioSource(0, _videoAudio);
        EnableVideoAudioTrack();
    }

    IEnumerator RunRoutine()
    {
        IsFinished = false;
        _videoPlayer.Prepare();
        float prepareT = 0f;
        while (!_videoPlayer.isPrepared && prepareT < 10f && !_skipRequested)
        {
            prepareT += Time.unscaledDeltaTime;
            if (Clicked()) _skipRequested = true;
            yield return null;
        }

        if (_skipRequested || !_videoPlayer.isPrepared)
        {
            if (_rawImage != null)
                _rawImage.color = new Color(1f, 1f, 1f, 0f);
            yield return null;
            FinishNow();
            yield break;
        }

        ApplyPreparedVideoSize();
        EnableVideoAudioTrack();
        if (_videoPlayer.texture != null)
            _rawImage.texture = _videoPlayer.texture;

        _videoPlayer.Play();

        // 起播预滚（卡顿修复）：头 0.6 秒是解码器预热 + 城镇首帧渲染/弹窗初始化重活的高发期，
        // 直接出画面就会一顿一顿。先藏画面并静音，让纯黑垫住预滚窗口，窗口过后画面、声音一起出，
        // 音视频时钟不受影响。窗口内点按仍可跳过（跳过直接收尾，不再走淡出）。
        if (!_skipRequested)
        {
            Color vidCol = _rawImage != null ? _rawImage.color : Color.white;
            if (_rawImage != null)
                _rawImage.color = new Color(vidCol.r, vidCol.g, vidCol.b, 0f);
            if (_videoAudio != null)
                _videoAudio.mute = true;

            float settle = 0.6f;
            float st = 0f;
            while (st < settle && !_skipRequested && _videoPlayer.isPlaying)
            {
                st += Time.unscaledDeltaTime;
                if (Clicked()) _skipRequested = true;
                yield return null;
            }

            if (_videoAudio != null)
                _videoAudio.mute = !GameAudio.AudioEnabled; // 还原音频开关状态
            if (_rawImage != null && !_skipRequested)
                _rawImage.color = vidCol;

            if (_skipRequested)
            {
                FinishNow();
                yield break;
            }
        }

        while (_videoPlayer.isPlaying && !_skipRequested)
        {
            if (Clicked()) _skipRequested = true;
            yield return null;
        }

        if (_skipRequested)
            StopVideoPlayback();

        // 视频淡出，黑底留下交给 TownIntroVeil，避免露出空场景。
        float fade = 0.55f;
        float t = 0f;
        Color videoCol = _rawImage != null ? _rawImage.color : Color.white;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fade);
            if (_rawImage != null)
                _rawImage.color = new Color(videoCol.r, videoCol.g, videoCol.b, a);
            yield return null;
        }

        FinishNow();
    }

    void FinishNow()
    {
        StopVideoPlayback();
        ReleaseCutsceneMute();
        IsFinished = true;
        Destroy(gameObject);
    }

    void StopVideoPlayback()
    {
        if (_videoPlayer == null) return;
        if (_videoPlayer.isPlaying)
            _videoPlayer.Stop();
        if (_videoAudio != null)
            _videoAudio.Stop();
    }

    void EnableVideoAudioTrack()
    {
        if (_videoPlayer == null) return;
        if (_videoPlayer.audioTrackCount <= 0) return;

        _videoPlayer.EnableAudioTrack(0, GameAudio.AudioEnabled);
        if (_videoAudio != null)
        {
            _videoAudio.mute = !GameAudio.AudioEnabled;
            _videoAudio.volume = GameAudio.AudioEnabled ? 1f : 0f;
        }
    }

    void ApplyPreparedVideoSize()
    {
        if (_videoPlayer == null) return;

        uint w = _videoPlayer.width;
        uint h = _videoPlayer.height;
        if (w == 0 || h == 0)
        {
            var tex = _videoPlayer.texture;
            if (tex != null)
            {
                w = (uint)tex.width;
                h = (uint)tex.height;
            }
        }

        if (w == 0 || h == 0) return;

        float ar = (float)w / h;
        if (_aspectFitter != null)
            _aspectFitter.aspectRatio = ar;
        if (_rawImage != null)
            UiLayoutStretch.ApplyEnvelopeRawImage(_rawImage.rectTransform, ar);
        if (_renderTexture == null || _renderTexture.width != (int)w || _renderTexture.height != (int)h)
            RecreateRenderTexture((int)w, (int)h); // 尺寸没变就不重建，消掉起播第一下顿卡
    }

    void RecreateRenderTexture(int width, int height)
    {
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        if (_renderTexture != null)
        {
            _videoPlayer.targetTexture = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        _renderTexture.Create();
        if (_rawImage != null) _rawImage.texture = _renderTexture;
        if (_videoPlayer != null) _videoPlayer.targetTexture = _renderTexture;
    }

    static bool Clicked()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 卡顿修复：全局锁 30fps 且关垂直同步时，主循环帧间隔抖动，视频播放会一顿一顿。
    /// 播放期间临时提速帧节奏（编辑器下 vSync 锁显示器刷新，真机下走 60fps 目标），播完在 OnDestroy 还原。
    /// 只影响片头这几秒，全局 30fps 性能锁不动。
    /// </summary>
    void BoostFramePaceForVideo()
    {
        if (_fpsBoosted) return;
        _fpsBoosted = true;
        _prevTargetFps = Application.targetFrameRate;
        _prevVSync = QualitySettings.vSyncCount;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
    }

    void RestoreFramePace()
    {
        if (!_fpsBoosted) return;
        _fpsBoosted = false;
        Application.targetFrameRate = _prevTargetFps;
        QualitySettings.vSyncCount = _prevVSync;
    }

    void OnDestroy()
    {
        RestoreFramePace();
        if (Instance == this) Instance = null;
        ReleaseCutsceneMute();
        if (_videoPlayer != null)
        {
            _videoPlayer.targetTexture = null;
        }
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
        IsFinished = true;
    }
}
