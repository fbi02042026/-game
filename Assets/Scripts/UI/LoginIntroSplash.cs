using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 登录前动画：健康忠告之后、登录界面之前播放 Resources/UI/Boot 下视频。
/// 点击任意处跳过；播完或跳过后回调显示登录。纯运行时构建，不改预制体。
/// </summary>
public class LoginIntroSplash : MonoBehaviour
{
    static readonly string VideoResourcesPath = ContentPaths.Ui.LoginIntro;

    Action _onFinished;
    bool _finished;
    bool _skipRequested;
    bool _bgmMuted;

    CanvasGroup _group;
    VideoPlayer _videoPlayer;
    AudioSource _videoAudio;
    RawImage _rawImage;
    RenderTexture _renderTexture;
    AspectRatioFitter _aspectFitter;

    public static void Present(Action onFinished)
    {
        var existing = FindObjectOfType<LoginIntroSplash>();
        if (existing != null)
        {
            existing._onFinished = onFinished;
            return;
        }

        var clip = Resources.Load<VideoClip>(VideoResourcesPath);
        if (clip == null)
        {
            Debug.LogWarning("[LoginIntro] 未找到视频 Resources/" + VideoResourcesPath + "，直接进登录");
            onFinished?.Invoke();
            return;
        }

        var go = new GameObject("LoginIntroSplash");
        var splash = go.AddComponent<LoginIntroSplash>();
        splash._onFinished = onFinished;
        splash.Build(clip);
        splash.BeginCutsceneMute();
        splash.StartCoroutine(splash.RunRoutine());
    }

    void Build(VideoClip clip)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx, UICanvasSetup.ResolveUiCamera());
        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;

        var bgGo = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;
        Stretch(bg.rectTransform);

        var videoGo = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        videoGo.transform.SetParent(transform, false);
        _rawImage = videoGo.GetComponent<RawImage>();
        _rawImage.color = Color.white;
        _rawImage.raycastTarget = false;
        Stretch(_rawImage.rectTransform);
        _aspectFitter = videoGo.GetComponent<AspectRatioFitter>();
        _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        _aspectFitter.aspectRatio = 9f / 16f;

        int rw = clip != null && clip.width > 0 ? (int)clip.width : 720;
        int rh = clip != null && clip.height > 0 ? (int)clip.height : 1280;
        RecreateRenderTexture(rw, rh);

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.source = VideoSource.VideoClip;
        _videoPlayer.clip = clip;
        _videoPlayer.isLooping = false;
        _videoPlayer.skipOnDrop = true;

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
        BootManager.ReleaseBootVeil();

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
            Finish();
            yield break;
        }

        ApplyPreparedVideoSize();
        EnableVideoAudioTrack();
        if (_videoPlayer.texture != null)
            _rawImage.texture = _videoPlayer.texture;

        _videoPlayer.Play();
        while (_videoPlayer.isPlaying && !_skipRequested)
        {
            if (Clicked()) _skipRequested = true;
            yield return null;
        }

        if (_skipRequested)
            StopVideoPlayback();

        float fade = 0.35f;
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

        Finish();
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        StopVideoPlayback();
        ReleaseCutsceneMute();
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
        Destroy(gameObject);
    }

    void BeginCutsceneMute()
    {
        if (_bgmMuted) return;
        _bgmMuted = true;
        GameBgm.MuteForCutscene(0.15f);
    }

    void ReleaseCutsceneMute()
    {
        if (!_bgmMuted) return;
        _bgmMuted = false;
        GameBgm.UnmuteAfterCutscene(0.4f);
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
        if (_aspectFitter != null)
            _aspectFitter.aspectRatio = (float)w / h;
        RecreateRenderTexture((int)w, (int)h);
    }

    void RecreateRenderTexture(int width, int height)
    {
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);
        if (_renderTexture != null)
        {
            if (_videoPlayer != null) _videoPlayer.targetTexture = null;
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
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void OnDestroy()
    {
        ReleaseCutsceneMute();
        if (_videoPlayer != null)
            _videoPlayer.targetTexture = null;
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
