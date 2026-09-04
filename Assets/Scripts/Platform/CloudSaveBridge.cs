using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 云存档适配层：存载荷字符串（保护开启时为加密 Base64，关闭时为明文 JSON 的 Base64）。
/// </summary>
public static class CloudSaveBridge
{
    const string CloudMirrorKey = "cloud_save_mirror_v2";

    public static bool UseWeChatCloud { get; set; }

    public static void UploadPayload(string payload, Action<bool> onDone = null)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log("[CloudSaveBridge] Spotlight：跳过云上传");
            onDone?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(payload))
        {
            onDone?.Invoke(false);
            return;
        }

        try
        {
            PlayerPrefs.SetString(CloudMirrorKey, payload);
            PlayerPrefs.Save();
            string path = Path.Combine(Application.persistentDataPath, "cloud_mirror.dat");
            File.WriteAllText(path, payload);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CloudSaveBridge] 本地镜像失败: " + e.Message);
        }

        if (UseWeChatCloud)
        {
            Debug.Log("[CloudSaveBridge] UseWeChatCloud=true，等待微信 SDK 绑定");
            onDone?.Invoke(false);
            return;
        }

        onDone?.Invoke(true);
    }

    public static void DownloadPayload(Action<string> onDone)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log("[CloudSaveBridge] Spotlight：跳过云下载");
            onDone?.Invoke(null);
            return;
        }

        if (UseWeChatCloud)
        {
            onDone?.Invoke(null);
            return;
        }

        string payload = PlayerPrefs.GetString(CloudMirrorKey, null);
        if (string.IsNullOrEmpty(payload))
        {
            string path = Path.Combine(Application.persistentDataPath, "cloud_mirror.dat");
            if (File.Exists(path))
            {
                try { payload = File.ReadAllText(path); }
                catch { /* ignore */ }
            }
        }
        onDone?.Invoke(string.IsNullOrEmpty(payload) ? null : payload);
    }
}
