using UnityEngine;

/// <summary>
/// 声音总开关（音乐 + 音效一起开/关）。存 PlayerPrefs，切场景后仍生效。
/// 项目里还没有统一的 AudioManager，所以这里按 AudioSource 的特征分类：
/// loop 或名字带 bgm/music 的算音乐，其余算音效。
/// </summary>
public static class GameAudio
{
    const string AudioKey = "audio.on";
    const string MusicKey = "audio.music.on";
    const string SfxKey = "audio.sfx.on";

    static bool _loaded;
    static bool _enabled = true;
    static bool _loadingMuted;
    static AudioSource _sfxPlayer;

    public static bool IsLoadingMuted => _loadingMuted;

    /// <summary>音乐 + 音效统一开关。</summary>
    public static bool AudioEnabled
    {
        get { Load(); return _enabled; }
        set { SetEnabled(value); }
    }

    public static bool MusicEnabled
    {
        get => AudioEnabled;
        set => AudioEnabled = value;
    }

    public static bool SfxEnabled
    {
        get => AudioEnabled;
        set => AudioEnabled = value;
    }

    static void SetEnabled(bool value)
    {
        Load();
        if (_enabled == value) return;
        _enabled = value;
        PlayerPrefs.SetInt(AudioKey, value ? 1 : 0);
        PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
        PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        GameBgm.OnMusicToggleChanged();
    }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        if (PlayerPrefs.HasKey(AudioKey))
        {
            _enabled = PlayerPrefs.GetInt(AudioKey, 1) != 0;
            return;
        }

        // 旧存档：音乐/音效曾分开存，任一关则整体关
        bool music = PlayerPrefs.GetInt(MusicKey, 1) != 0;
        bool sfx = PlayerPrefs.GetInt(SfxKey, 1) != 0;
        _enabled = music && sfx;
    }

    /// <summary>每次进场景后把存档里的开关重新刷一遍。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnSceneLoad()
    {
        Apply();
    }

    /// <summary>Loading 开始：立刻停掉场景内音效，期间 PlaySfx 也不响应。</summary>
    public static void MuteForLoading()
    {
        Load();
        if (_loadingMuted) return;
        _loadingMuted = true;
        ApplyLoadingMute();
    }

    /// <summary>Loading 结束：按声音开关恢复（新触发的音效从此时起可播）。</summary>
    public static void UnmuteAfterLoading()
    {
        if (!_loadingMuted) return;
        _loadingMuted = false;
        Apply();
    }

    /// <summary>把当前开关状态刷到场景里所有 AudioSource 上。</summary>
    public static void Apply()
    {
        Load();
        var all = Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var src = all[i];
            if (src == null) continue;
            // GameBgm 双声道自己管淡入淡出 / Loading 静音，别在这里 Pause/UnPause 打架
            if (src.gameObject.name == "GameBgm") continue;

            bool isMusic = IsMusicSource(src);
            bool on = _enabled;
            if (!isMusic && _loadingMuted) on = false;
            src.mute = !on;
            if (isMusic && !on && src.isPlaying)
                src.Pause();
            else if (isMusic && on && !src.isPlaying && src.clip != null)
                src.UnPause();
        }
    }

    static void ApplyLoadingMute()
    {
        var all = Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var src = all[i];
            if (src == null) continue;
            if (src.gameObject.name == "GameBgm") continue;
            if (IsMusicSource(src)) continue;

            src.mute = true;
            if (!src.isPlaying) continue;
            if (src.loop)
                src.Pause();
            else
                src.Stop();
        }

        if (_sfxPlayer != null)
        {
            _sfxPlayer.mute = true;
            _sfxPlayer.Stop();
        }
    }

    static bool IsMusicSource(AudioSource src)
    {
        if (src.loop) return true;
        string n = src.gameObject.name.ToLowerInvariant();
        if (n.Contains("bgm") || n.Contains("music")) return true;
        if (src.clip != null)
        {
            string c = src.clip.name.ToLowerInvariant();
            if (c.Contains("bgm") || c.Contains("music")) return true;
        }
        return false;
    }

    /// <summary>统一播一次性音效，受声音总开关控制。</summary>
    public static void PlaySfx(AudioClip clip, float volume = 1f)
    {
        Load();
        if (_loadingMuted || !_enabled || clip == null) return;
        if (_sfxPlayer == null)
        {
            var go = new GameObject("GameAudioSfx");
            Object.DontDestroyOnLoad(go);
            _sfxPlayer = go.AddComponent<AudioSource>();
            _sfxPlayer.playOnAwake = false;
            _sfxPlayer.loop = false;
        }
        _sfxPlayer.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
