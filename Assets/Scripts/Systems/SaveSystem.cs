using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 存档系统：使用JSON存储，兼容云存档和跨平台
/// 最优方案：JSON明文存储 + 备份 + 加密可选
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
        savePath = Path.Combine(Application.persistentDataPath, "save.json");
        backupPath = Path.Combine(Application.persistentDataPath, "save_backup.json");
        Load();
    }

    public void Load()
    {
        if (!File.Exists(savePath))
        {
            // 尝试从备份恢复
            if (File.Exists(backupPath))
            {
                try
                {
                    string backupJson = File.ReadAllText(backupPath);
                    _data = JsonUtility.FromJson<SaveData>(backupJson);
                    if (_data != null)
                    {
                        Debug.Log("[SaveSystem] 从备份恢复存档成功");
                        Save();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[SaveSystem] 备份恢复失败：" + e.Message);
                }
            }
            _data = new SaveData();
            if (string.IsNullOrEmpty(_data.selectedPlayerSkillId))
                _data.selectedPlayerSkillId = "heal_spring";
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            _data = JsonUtility.FromJson<SaveData>(json);
            if (_data == null)
            {
                throw new Exception("反序列化返回null");
            }

            // 修复可能为null的字段
            _data.talents ??= new System.Collections.Generic.Dictionary<string, int>();
            if (string.IsNullOrEmpty(_data.selectedPlayerSkillId))
                _data.selectedPlayerSkillId = PlayerSkillDefs.All[0].id;
            _data.unlockedLegendaryWeapons ??= new System.Collections.Generic.HashSet<string>();
            _data.legacyEquipPool ??= new System.Collections.Generic.List<EquipmentData>();
            _data.townLevel ??= new TownLevel();
            _data.permanentMercs ??= new System.Collections.Generic.List<MercenaryData>();
            _data.mailInbox ??= new System.Collections.Generic.List<MailEntry>();
            if (_data.stamina <= 0) _data.stamina = GameConfig.STAMINA_START;
            if (_data.totalGold > ResourceWallet.DEFAULT_MAX) _data.totalGold = ResourceWallet.DEFAULT_MAX;
            if (_data.diamond > ResourceWallet.DEFAULT_MAX) _data.diamond = (int)ResourceWallet.DEFAULT_MAX;
            if (_data.stamina > GameConfig.STAMINA_MAX) _data.stamina = GameConfig.STAMINA_MAX;
            if (_data.lastStaminaUtc <= 0)
                _data.lastStaminaUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StaminaSystem.Tick(save: false);

            Debug.Log($"[SaveSystem] 存档加载成功，金币：{_data.totalGold}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] 存档损坏，尝试从备份恢复：" + e.Message);
            if (File.Exists(backupPath))
            {
                try
                {
                    string backupJson = File.ReadAllText(backupPath);
                    _data = JsonUtility.FromJson<SaveData>(backupJson);
                    if (_data != null)
                    {
                        Debug.Log("[SaveSystem] 备份恢复成功");
                        Save();
                        return;
                    }
                }
                catch { }
            }
            _data = new SaveData();
            if (string.IsNullOrEmpty(_data.selectedPlayerSkillId))
                _data.selectedPlayerSkillId = "heal_spring";
            Save();
        }
    }

    public void Save()
    {
        _data.lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            // 先备份旧存档
            if (File.Exists(savePath))
            {
                File.Copy(savePath, backupPath, true);
            }

            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(savePath, json);
            Debug.Log("[SaveSystem] 存档保存成功");
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 保存失败：" + e.Message);
        }
    }

    public long CalcOfflineGold()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long offlineSeconds = now - _data.lastSaveTime;
        float offlineHours = Mathf.Min(offlineSeconds / 3600f, GameConfig.MAX_OFFLINE_HOURS);
        int goldPerMinute = 10 * _data.townLevel.farm;
        long gold = (long)(goldPerMinute * offlineHours * 60);
        return gold;
    }

    public long ClaimOfflineGold()
    {
        long gold = CalcOfflineGold();
        ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: gold > 0);
        return gold;
    }

    public void UploadToCloud()
    {
        // 调用微信云开发/服务器API上传_data的JSON
        string json = JsonUtility.ToJson(_data);
        // TODO: 微信SDK接入后实现
    }

    public void DownloadFromCloud(Action<bool> onComplete)
    {
        // TODO: 微信SDK接入后实现
        onComplete?.Invoke(false);
    }
}