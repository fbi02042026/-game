using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Debug 模式会话日志（NDJSON → debug-36365b.log）</summary>
public static class DebugAgentLog
{
    const string SessionId = "36365b";
    /// <summary>默认关闭：同步写盘会在战斗热路径（受击闪白等）造成卡顿。</summary>
    static readonly bool Enabled = false;
    static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "debug-36365b.log"));

    public static void Log(string hypothesisId, string location, string message, string dataJson = "{}")
    {
        if (!Enabled) return;
        try
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"sessionId\":\"").Append(SessionId);
            sb.Append("\",\"hypothesisId\":\"").Append(Escape(hypothesisId));
            sb.Append("\",\"location\":\"").Append(Escape(location));
            sb.Append("\",\"message\":\"").Append(Escape(message));
            sb.Append("\",\"data\":").Append(string.IsNullOrEmpty(dataJson) ? "{}" : dataJson);
            sb.Append(",\"timestamp\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            sb.Append("}\n");
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DebugAgentLog] write failed: " + e.Message);
        }
    }

    static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
