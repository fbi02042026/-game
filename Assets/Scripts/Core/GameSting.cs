using UnityEngine;

/// <summary>
/// 胜负 jingle（一次性过场曲，非循环 BGM）【2026-10-31 主人拍板接入】：
/// · Resources/Audio/Sting/战斗胜利 —— 战斗结果界面胜利弹出时播（主人给的《战斗胜利.mp3》）
/// · Resources/Audio/Sting/战斗失败 —— 战斗结果界面失败弹出时播（chiptune 合成版，与胜利曲同为 F 大调体系 / 104 BPM）
///
/// 行为口径：
/// · 开播前把战斗 BGM 淡出（jingle 接管情绪）；clip 先验存在再停 BGM，缺曲 LogError 不静默、BGM 不动（铁律 14）；
/// · 场景切换自动停（sceneLoaded 单一出口）——新场景 BGM 由 GameBgm 既有 pending 流程接管；
/// · 复活成功回战斗（结果界面带复活选项销毁且玩家选了复活）由
///   <see cref="BattleResultScreenUI.OnDestroy"/> 调 <see cref="StopAndResumeBattleBgm"/> 恢复战斗曲；
/// · 宿主命名 MusicSting（含 music）→ GameAudio.IsMusicSource 判为音乐源，跟音乐总开关走；
/// · Loading / 片头静音期不播（与铁律 14「BGM 静音期不许 UI 级 Play」同口径）。
/// </summary>
public static class GameSting
{
    const string ResRoot = "Audio/Sting/";
    const string VictoryClip = "战斗胜利";
    const string DefeatClip = "战斗失败";

    static AudioSource _src;
    /// <summary>当前 jingle 是不是失败曲（失败曲循环，见 PlayBattleResult；StopDefeat 只停它，不误伤胜利曲）。</summary>
    static bool _defeatPlaying;
    /// <summary>当前 jingle 的归属者（结果界面实例）：Destroy 是帧末执行，旧窗口销毁时不能误停
    /// 同帧新窗口刚接上的失败曲（复活选择窗 → 返回 → 失败直出同帧切换的实况），靠它比对防误杀。</summary>
    static object _owner;

    /// <summary>战斗结果界面弹出时播对应胜负曲（victory=胜利曲 / 失败曲）。
    /// 【2026-10-31 主人报修「失败的背景音乐 2 秒后就没了」】失败曲改<b>循环</b>播放：
    /// 只要失败界面没关就一直有；停的唯一出口 = 结果界面销毁调 <see cref="StopDefeat"/>
    ///（或复活回战斗 StopAndResumeBattleBgm / 场景切换既有 Stop）。</summary>
    public static void PlayBattleResult(bool victory, object owner = null)
    {
        if (!Application.isPlaying) return;
        if (GameBgm.IsLoadingMuted || GameBgm.IsCutsceneMuted) return;
        if (!GameAudio.MusicEnabled) return;
        var clip = Resources.Load<AudioClip>(ResRoot + (victory ? VictoryClip : DefeatClip));
        if (clip == null)
        {
            Debug.LogError($"[GameSting] 找不到胜负曲 Resources/{ResRoot}{(victory ? VictoryClip : DefeatClip)}，请确认已导入");
            return;
        }
        EnsureHost();
        GameBgm.Stop(0.25f);   // BGM 让位给 jingle（clip 已验存在才停，缺曲不动 BGM）
        _src.Stop();
        _src.clip = clip;
        _src.loop = !victory;   // 失败曲循环（界面关了由 StopDefeat 停）；胜利曲照旧一次性
        _defeatPlaying = !victory;
        _owner = owner;
        _src.volume = GameBgm.DefaultVolume;   // 与 BGM 同响度档（曲子内部已按胜利曲参考响度混音）
        _src.Play();
    }

    /// <summary>停失败曲（结果界面销毁时调）：只停失败曲，胜利曲一次性不受影响；
    /// 只停<b>自己</b>接上的那首（owner 不匹配 = 曲子已被别处接管，不动）。</summary>
    public static void StopDefeat(object owner)
    {
        if (!_defeatPlaying) return;
        if (!ReferenceEquals(_owner, owner)) return;
        _defeatPlaying = false;
        if (_src != null)
        {
            _src.Stop();
            _src.clip = null;
        }
    }

    /// <summary>复活成功回战斗：停 jingle + 按当前关卡类型恢复战斗曲（TrackForStage 单一出口）。</summary>
    public static void StopAndResumeBattleBgm()
    {
        Stop();
        var st = BattleManager.Instance != null ? BattleManager.Instance.currentStage : null;
        if (st != null)
            GameBgm.Play(GameBgm.TrackForStage(st.type), 0.4f);
    }

    /// <summary>停 jingle（场景切换 / 复活恢复 BGM 时调）。</summary>
    public static void Stop()
    {
        _defeatPlaying = false;
        _owner = null;
        if (_src != null)
        {
            _src.Stop();
            _src.clip = null;
        }
    }

    static void EnsureHost()
    {
        if (_src != null) return;
        var go = new GameObject("MusicSting");
        Object.DontDestroyOnLoad(go);
        _src = go.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialBlend = 0f;
        _src.priority = 0;
        // 场景切换 = 胜负演出结束：停 jingle，新场景 BGM 交给既有流程（pending / GuessTrackFromScene）
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Stop();
    }
}
