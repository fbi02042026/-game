using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音乐 / 音效总开关。存 PlayerPrefs，切场景后仍生效。
/// 项目里还没有统一的 AudioManager，所以这里按 AudioSource 的特征分类：
/// loop 或名字带 bgm/music 的算音乐，其余算音效。
/// </summary>
public static class GameAudio
{
    const string MusicKey = "audio.music.on";
    const string SfxKey = "audio.sfx.on";

    static bool _loaded;
    static bool _music = true;
    static bool _sfx = true;
    static AudioSource _sfxPlayer;

    public static bool MusicEnabled
    {
        get { Load(); return _music; }
        set
        {
            Load();
            if (_music == value) return;
            _music = value;
            PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            GameBgm.OnMusicToggleChanged();
        }
    }

    public static bool SfxEnabled
    {
        get { Load(); return _sfx; }
        set
        {
            Load();
            if (_sfx == value) return;
            _sfx = value;
            PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }
    }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _music = PlayerPrefs.GetInt(MusicKey, 1) != 0;
        _sfx = PlayerPrefs.GetInt(SfxKey, 1) != 0;
    }

    /// <summary>每次进场景后把存档里的开关重新刷一遍。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnSceneLoad()
    {
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
            bool on = isMusic ? _music : _sfx;
            src.mute = !on;
            if (isMusic && !on && src.isPlaying)
                src.Pause();
            else if (isMusic && on && !src.isPlaying && src.clip != null)
                src.UnPause();
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

    /// <summary>统一播一次性音效，受音效开关控制。</summary>
    public static void PlaySfx(AudioClip clip, float volume = 1f)
    {
        Load();
        if (!_sfx || clip == null) return;
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
