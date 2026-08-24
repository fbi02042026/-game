using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 存档系统：加密二进制 + 备份；兼容旧 save.json。
/// </summary>
public class SaveSystem : Singleton<SaveSystem>
{
    private string savePath;
    private string backupPath;
    private SaveData _data;
    public SaveData Data => _data;

    protected override void Awake()
    {
        base.Awake();
        savePath = Path.Combine(Application.persistentDataPath, "player.dat");
        backupPath = Path.Combine(Application.persistentDataPath, "player.bak");
        Load();
    }

    string ReadSaveJson(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        byte[] blob = File.ReadAllBytes(path);
        if (SecureCodec.TryReadPayload(blob, out string json))
            return json;
        return null;
    }

    public void Load()
    {
        string json = ReadSaveJson(savePath);
        if (string.IsNullOrEmpty(json))
            json = ReadSaveJson(backupPath);
        if (string.IsNullOrEmpty(json))
        {
            string legacy = Path.Combine(Application.persistentDataPath, "save.json");
            string legacyBak = Path.Combine(Application.persistentDataPath, "save_backup.json");
            json = ReadSaveJson(legacy);
            if (string.IsNullOrEmpty(json))
                json = ReadSaveJson(legacyBak);
        }

        if (string.IsNullOrEmpty(json))
        {
            _data = CreateFreshSave();
            Save();
            return;
        }

        try
        {
            _data = JsonUtility.FromJson<SaveData>(json);
            if (_data == null)
                throw new Exception("反序列化返回null");
            FinalizeLoadedData(_data);
            Debug.Log($"[SaveSystem] 存档加载成功，金币：{_data.totalGold}，天赋数：{_data.talents.Count}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] 存档损坏：" + e.Message);
            _data = CreateFreshSave();
            Save();
        }
    }

    public void Save()
    {
        if (_data == null)
            _data = CreateFreshSave();

        _data.lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            _data.SyncListsFromRuntime();
            string json = JsonUtility.ToJson(_data, false);
            byte[] blob = SecureCodec.EncryptUtf8(json);
            if (File.Exists(savePath))
                File.Copy(savePath, backupPath, true);
            File.WriteAllBytes(savePath, blob);
            Debug.Log("[SaveSystem] 存档保存成功");
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 保存失败：" + e.Message);
        }
    }

    static SaveData CreateFreshSave()
    {
        var data = new SaveData();
        data.SyncRuntimeFromLists();
        if (string.IsNullOrEmpty(data.selectedPlayerSkillId))
            data.selectedPlayerSkillId = "heal_spring";
        return data;
    }

    static void FinalizeLoadedData(SaveData data)
    {
        data.SyncRuntimeFromLists();
        if (string.IsNullOrEmpty(data.selectedPlayerSkillId))
            data.selectedPlayerSkillId = PlayerSkillDefs.All[0].id;
        if (data.maxUnlockedChapter > 1)
            data.tutorialDone = true;
        // 体力合法可为 0，勿在加载时灌满；仅钳制上限
        if (data.stamina < 0) data.stamina = 0;
        if (data.totalGold > ResourceWallet.DEFAULT_MAX) data.totalGold = ResourceWallet.DEFAULT_MAX;
        if (data.diamond > ResourceWallet.DEFAULT_MAX) data.diamond = (int)ResourceWallet.DEFAULT_MAX;
        if (data.stamina > GameConfig.STAMINA_MAX) data.stamina = GameConfig.STAMINA_MAX;
        if (data.lastStaminaUtc <= 0)
            data.lastStaminaUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ResourceAdRewards.EnsureDay(data);
        StaminaSystem.Tick(save: false);
    }

    public long CalcOfflineGold()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long offlineSeconds = Math.Max(0, now - _data.lastSaveTime);
        return OfflineGoldCalc.FromSeconds(offlineSeconds, _data.townLevel?.farm ?? 0);
    }

    public long ClaimOfflineGold()
    {
        long gold = CalcOfflineGold();
        if (gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: true);
        else
            Save(); // 刷新 lastSaveTime，避免短离线反复 Calc
        return gold;
    }

    public void UploadToCloud()
    {
        _data.SyncListsFromRuntime();
        string json = JsonUtility.ToJson(_data);
        string payload = Convert.ToBase64String(SecureCodec.EncryptUtf8(json));
        CloudSaveBridge.UploadPayload(payload, ok =>
        {
            if (ok) Debug.Log("[SaveSystem] 云存档上传成功（或本地镜像）");
            else Debug.LogWarning("[SaveSystem] 云存档上传未完成（等待微信 SDK）");
        });
    }

    public void DownloadFromCloud(Action<bool> onComplete)
    {
        CloudSaveBridge.DownloadPayload(payload =>
        {
            if (string.IsNullOrEmpty(payload))
            {
                onComplete?.Invoke(false);
                return;
            }
            try
            {
                string json = payload;
                byte[] raw = null;
                try { raw = Convert.FromBase64String(payload); }
                catch { raw = null; }
                if (raw != null && SecureCodec.TryReadPayload(raw, out string dec))
                    json = dec;
                var loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded == null)
                {
                    onComplete?.Invoke(false);
                    return;
                }
                loaded.SyncRuntimeFromLists();
                _data = loaded;
                Save();
                onComplete?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveSystem] 云存档解析失败: " + e.Message);
                onComplete?.Invoke(false);
            }
        });
    }

    /// <summary>调试用：删除本地存档和备份，内存换成新档。测剧情请清档后再进城镇。</summary>
    public void DeleteLocalSaveAndReset()
    {
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Application.persistentDataPath, "player.dat");
        if (string.IsNullOrEmpty(backupPath))
            backupPath = Path.Combine(Application.persistentDataPath, "player.bak");

        try
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            string legacy = Path.Combine(Application.persistentDataPath, "save.json");
            string legacyBak = Path.Combine(Application.persistentDataPath, "save_backup.json");
            if (File.Exists(legacy)) File.Delete(legacy);
            if (File.Exists(legacyBak)) File.Delete(legacyBak);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 删除存档失败：" + e.Message);
        }

        BattleStateSaver.Instance?.ClearSavedState();
        StoryProgress.ResetRuntimeFlags();
        _data = CreateFreshSave();
        Save();
        Debug.Log("[SaveSystem] 已清除存档，新档 tutorialDone=" + _data.tutorialDone);
    }
}
