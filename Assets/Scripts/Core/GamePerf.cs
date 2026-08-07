using UnityEngine;

/// <summary>
/// 小游戏性能开关：默认关日志、锁 30FPS、低开销渲染。
/// </summary>
public static class GamePerf
{
    /// <summary>开发时改为 true 才打详细日志</summary>
    public static bool VerboseLog = false;

    public static void ApplyStartup()
    {
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        // 不改阴影/AA，避免影响现有渲染管线导致角色/怪物看不见
    }

    public static void Log(string msg)
    {
        if (VerboseLog) Debug.Log(msg);
    }

    public static void LogWarning(string msg)
    {
        if (VerboseLog) Debug.LogWarning(msg);
    }
}
