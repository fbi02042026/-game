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
    public bool IsFinished { get; private set; }

    CanvasGroup _group;
    VideoPlayer _videoPlayer;
    AudioSource _videoAudio;
    RawImage _rawImage;
    RenderTexture _renderTexture;
    AspectRatioFitter _aspectFitter;
    bool _skipRequested;
    bool _bgmMutedForCutscene;

    public static OpeningIntroOverlay Show(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
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

        var root = new GameObject("OpeningIntroOverlay");
        var driver = root.AddComponent<OpeningIntroOverlay>();
        driver.Build(videoPath);
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

    void Build(string videoPath)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
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

        RecreateRenderTexture(720, 1280);

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = videoPath;
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

    void OnDestroy()
    {
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
