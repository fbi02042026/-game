using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 云存档适配层：本地文件镜像 + 微信云预留接口。
/// </summary>
public static class CloudSaveBridge
{
    const string CloudMirrorKey = "cloud_save_mirror_json";

    public static bool UseWeChatCloud { get; set; }

    public static void UploadJson(string json, Action<bool> onDone = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            onDone?.Invoke(false);
            return;
        }

        // 本地镜像，便于联调与断网回退
        try
        {
            PlayerPrefs.SetString(CloudMirrorKey, json);
            PlayerPrefs.Save();
            string path = Path.Combine(Application.persistentDataPath, "cloud_mirror.json");
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CloudSaveBridge] 本地镜像失败: " + e.Message);
        }

        if (UseWeChatCloud)
        {
            // TODO: 微信云开发 database / storage 上传
            Debug.Log("[CloudSaveBridge] UseWeChatCloud=true，等待微信 SDK 绑定");
            onDone?.Invoke(false);
            return;
        }

        Debug.Log("[CloudSaveBridge] 已写入本地云镜像（定版前）");
        onDone?.Invoke(true);
    }

    public static void DownloadJson(Action<string> onDone)
    {
        if (UseWeChatCloud)
        {
            // TODO: 微信云下载
            onDone?.Invoke(null);
            return;
        }

        string json = PlayerPrefs.GetString(CloudMirrorKey, null);
        if (string.IsNullOrEmpty(json))
        {
            string path = Path.Combine(Application.persistentDataPath, "cloud_mirror.json");
            if (File.Exists(path))
            {
                try { json = File.ReadAllText(path); }
                catch { /* ignore */ }
            }
        }
        onDone?.Invoke(string.IsNullOrEmpty(json) ? null : json);
    }
}
