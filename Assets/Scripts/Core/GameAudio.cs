using UnityEngine;

/// <summary>
/// 音乐 / 音效分开关。存 PlayerPrefs，切场景后仍生效。
/// 项目里还没有统一的 AudioManager，所以这里按 AudioSource 的特征分类：
/// loop 或名字带 bgm/music 的算音乐，其余算音效。
/// </summary>
public static class GameAudio
{
    const string AudioKey = "audio.on";
    const string MusicKey = "audio.music.on";
    const string SfxKey = "audio.sfx.on";

    static bool _loaded;
    static bool _musicEnabled = true;
    static bool _sfxEnabled = true;
    static bool _loadingMuted;
    static AudioSource _sfxPlayer;

    public static bool IsLoadingMuted => _loadingMuted;

    /// <summary>音乐 + 音效任一开启即为开（兼容旧调用）。</summary>
    public static bool AudioEnabled
    {
        get { Load(); return _musicEnabled || _sfxEnabled; }
        set
        {
            MusicEnabled = value;
            SfxEnabled = value;
        }
    }

    public static bool MusicEnabled
    {
        get { Load(); return _musicEnabled; }
        set { SetMusic(value); }
    }

    public static bool SfxEnabled
    {
        get { Load(); return _sfxEnabled; }
        set { SetSfx(value); }
    }

    static void SetMusic(bool value)
    {
        Load();
        if (_musicEnabled == value) return;
        _musicEnabled = value;
        PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
        SyncMasterPref();
        PlayerPrefs.Save();
        Apply();
        GameBgm.OnMusicToggleChanged();
    }

    static void SetSfx(bool value)
    {
        Load();
        if (_sfxEnabled == value) return;
        _sfxEnabled = value;
        PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
        SyncMasterPref();
        PlayerPrefs.Save();
        Apply();
    }

    static void SyncMasterPref()
    {
        PlayerPrefs.SetInt(AudioKey, (_musicEnabled || _sfxEnabled) ? 1 : 0);
    }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        if (PlayerPrefs.HasKey(MusicKey) || PlayerPrefs.HasKey(SfxKey))
        {
            _musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
            _sfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) != 0;
            return;
        }

        // 旧存档：只有总开关
        if (PlayerPrefs.HasKey(AudioKey))
        {
            bool on = PlayerPrefs.GetInt(AudioKey, 1) != 0;
            _musicEnabled = on;
            _sfxEnabled = on;
        }
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
        // 【2026-10-06 主人拍板】主人反馈「登录进主城 Loading 没走完就有城镇曲」→ 先只加诊断日志、不改行为。
        // 关键点：ApplyLoadingMute() 会 continue 掉所有 isMusic 源（只静音音效），
        // 所以 Loading 期间凡是「被判成音乐」的源都不受静音管。这里把它们全列出来，
        // 若场景/预制体里冒出第二个会响的源，下次复现一眼就能看见。
        bool diag = _loadingMuted;
        var sb = diag ? new System.Text.StringBuilder() : null;
        int musicCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var src = all[i];
            if (src == null) continue;
            // GameBgm 双声道自己管淡入淡出 / Loading 静音，别在这里 Pause/UnPause 打架
            if (src.gameObject.name == "GameBgm") continue;

            bool isMusic = IsMusicSource(src);
            if (diag && isMusic)
            {
                musicCount++;
                // ⚠ C# 老限制：插值字符串的 {...} 洞内**不许用 \" 转义引号**（编译器报 CS1073）。
                // 所以 clip 名先在洞外算好，洞里只放变量 —— 别再改回洞内写三元。
                string clipName = src.clip != null ? src.clip.name : "null";
                sb.Append($"\n  · [{src.gameObject.name}] clip={clipName} " +
                          $"loop={src.loop} isPlaying={src.isPlaying} mute={src.mute} vol={src.volume:F2}");
            }
            bool on = isMusic ? _musicEnabled : _sfxEnabled;
            if (!isMusic && _loadingMuted) on = false;
            src.mute = !on;
            if (isMusic && !on && src.isPlaying)
                src.Pause();
            else if (isMusic && on && !src.isPlaying && src.clip != null)
                src.UnPause();
        }

        if (diag)
            Debug.Log($"[AudioDiag] Loading 期间 Apply()：共 {all.Length} 个 AudioSource，" +
                      $"其中判为音乐的 {musicCount} 个（ApplyLoadingMute 不管它们）{sb}");
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
        if (_loadingMuted || !_sfxEnabled || clip == null) return;
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
