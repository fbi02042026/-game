using UnityEngine;
using System;
using System.IO;

/// <summary>
/// 存档系统：开发期明文 JSON；可读旧加密档。上线前再开 ContentProtection。
/// </summary>
public class SaveSystem : Singleton<SaveSystem>
{
    private string savePath;
    private string backupPath;
    private SaveData _data;
    public SaveData Data => _data;

    /// <summary>
    /// 版本清档标记键（2026-10-20 主人要求「下载新版本删除本地数据」）：
    /// 记录上次启动时的 Application.version，与当前不一致即清除本地数据。
    /// </summary>
    const string VersionWipePrefKey = "save_wipe_app_version";

    /// <summary>
    /// 「下载新版本删除本地数据」总开关。
    /// 【2026-10-20 14:50 主人拍板：打开】配合登录页公告（「老玩家的存档已自动清空」）。
    /// 【2026-10-21 00:45 主人拍板：关掉】本次打包不清数据，玩家覆盖安装新版保留存档。
    /// 【2026-10-24 主人拍板：打开】本次打包清数据，玩家装上即从新档开始。
    /// 只清一次的保证：PlayerPrefs 记 Application.version，每次启动比对，一致即 return；
    /// 版本标记在删存档文件【之前】写入并 Save，即使删档失败也不会下次重复清。
    /// ⚠ 只对 bundleVersion（Application.version）变化生效——正式出包必须递增版本号才触发。
    /// </summary>
    public const bool WipeLocalDataOnNewVersion = true;

    protected override void Awake()
    {
        base.Awake();
        savePath = Path.Combine(Application.persistentDataPath, "player.dat");
        backupPath = Path.Combine(Application.persistentDataPath, "player.bak");
        if (WipeLocalDataOnNewVersion)
            WipeLocalDataIfNewVersion();
        Load();
    }

    /// <summary>
    /// 下载新版本后自动清除本地数据：比对 Application.version 与本地记录的版本号，
    /// 不一致（覆盖安装新版；首次安装无记录也走一遍，等效空操作）即删除存档文件 +
    /// 清空全部 PlayerPrefs（战斗中断档、引导进度、音频开关等一并清），再记录当前版本。
    /// TapTap 游客标识（与 TapTapBootstrap.GuestIdPrefKey 同键）属账号身份不属游戏数据，保留。
    /// </summary>
    void WipeLocalDataIfNewVersion()
    {
        string current = Application.version;
        string stored = PlayerPrefs.GetString(VersionWipePrefKey, "");
        if (stored == current) return;

        string guestId = PlayerPrefs.GetString("TapTapGuestId", "");
        PlayerPrefs.DeleteAll();
        if (!string.IsNullOrEmpty(guestId))
            PlayerPrefs.SetString("TapTapGuestId", guestId);
        PlayerPrefs.SetString(VersionWipePrefKey, current);
        PlayerPrefs.Save();

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
            Debug.LogError("[SaveSystem] 版本更新清档：删除存档文件失败：" + e.Message);
        }

        StoryProgress.ResetRuntimeFlags();
        NewGameReset.ApplyAll();   // 【2026-11-02】清档统一复位：把进程级状态（引导游标/登录弹窗/进镇闸…）一起归零

        Debug.Log($"[SaveSystem] 检测到版本变化（{stored} → {current}），本地数据已清除，从新档开始");
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
        bool primaryUnreadable = string.IsNullOrEmpty(json) && File.Exists(savePath);
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
            // 【2026-10-31】文件在但读不出来（截断 / 非 JSON / 解密失败）≠ 首次安装：
            // 先把坏档留证，再建新档。首次安装（文件不存在）不会走到留证分支。
            if (File.Exists(savePath) || File.Exists(backupPath))
                PreserveCorruptSaves("主档与备份都无法解析");
            _data = CreateFreshSave();
            RunLoadout.Clear();   // 【2026-10-29 堵「清档后中断快照残留」】同 DeleteLocalSaveAndReset
            MercSkillMigrate.ApplyAutoCastPrefsToSave();
            Save();
            return;
        }

        // 【2026-10-31】主档读不出来、靠备份救回来的情况：必须把坏主档挪走。
        // 否则下一次 Save() 的 File.Copy(savePath → backupPath) 会把坏主档复制成"备份"，好备份当场没了。
        if (primaryUnreadable)
            PreserveCorruptSaves("主档不可读，已用备份恢复", onlyPrimary: true);

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
            // 【2026-10-31】留证后再建新档：原实现直接覆盖，坏档与备份当场消失，客服无法定位、玩家无法申诉。
            PreserveCorruptSaves("反序列化失败：" + e.Message);
            _data = CreateFreshSave();
            RunLoadout.Clear();   // 【2026-10-29 堵「清档后中断快照残留」】存档损坏重建 = 新档，旧局快照一并作废
            MercSkillMigrate.ApplyAutoCastPrefsToSave();
            Save();
        }
    }

    /// <summary>
    /// 【2026-10-31】坏档留证：把读不出来的存档改名成 <c>player.dat.corrupt_yyyyMMdd_HHmmss</c>，
    /// 再让上层建新档。只改名、不删除，出问题能拿原始文件复现；改名失败也只报错不阻断启动。
    /// </summary>
    void PreserveCorruptSaves(string reason, bool onlyPrimary = false)
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int moved = 0;
            if (File.Exists(savePath))
            {
                File.Move(savePath, savePath + ".corrupt_" + stamp);
                moved++;
            }
            if (!onlyPrimary && File.Exists(backupPath))
            {
                File.Move(backupPath, backupPath + ".corrupt_" + stamp);
                moved++;
            }
            if (moved > 0)
                Debug.LogError($"[SaveSystem] 存档损坏（{reason}），已保留 {moved} 个原始文件为 *.corrupt_{stamp}");
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 坏档留证失败（继续建新档，原始文件可能被覆盖）：" + e.Message);
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
            byte[] blob = ContentProtection.SaveEncryption
                ? SecureCodec.EncryptUtf8(json)
                : System.Text.Encoding.UTF8.GetBytes(json);
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
        // 2026-09-29：新档给启动抽奖币。抽奖与金币已分开，金币维持 0，不动城镇经济。
        // 【2026-10-10 主人拍板】抽奖币已退场：启动金改由 SlotMachineSystem.GrantRunStarterGold
        //   在开新局时直接发进局内钱包 BattleManager.currentGold（含「初始资金」天赋加成）。
        //   这一行现在只是给老存档字段 slotCoins 一个初值，不再有人消费它。
        // ⚠ 2026-10-05 主人拍板：「开始金币是每局的 不是每关的」—— 只在开新局那一关补一次，
        //   续关不再补；每关要能抽靠的是通关产出（已抬到 ≥ 下一关一抽的价格）。
        data.slotCoins = SlotMachineDefs.STARTER_COINS;
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
        // 【2026-10-31】上限口径与 StaminaSystem.Max / ResourceWallet.GetMax 对齐（基础 + 成就加成），
        // 原来只按 STAMINA_MAX 截断，会把带加成的体力静默削掉。
        long staminaCap = GameConfig.STAMINA_MAX + Mathf.Max(0, data.staminaBonusMax);
        if (data.stamina > staminaCap) data.stamina = (int)staminaCap;
        if (data.lastStaminaUtc <= 0)
            data.lastStaminaUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ResourceAdRewards.EnsureDay(data);
        ReviveQuota.EnsureDay(data);        // 复活每日档位跨天清零（2026-10-25 主人拍板）
        EndlessRiftRules.EnsureDay(data);   // 无限裂隙每日次数跨天重置兜底
        // 【2026-11-02】老档迁移：新增 endlessRiftBestStage（生涯最高）时它还是 0，
        // 但 clearedStage 已经有值 —— 不抬上来，老玩家重打旧层会被判成"生涯首通"发全额奖励（等于白送一轮）。
        if (data.endlessRiftBestStage < data.endlessRiftClearedStage)
            data.endlessRiftBestStage = data.endlessRiftClearedStage;
        ArenaRules.EnsurePeriods(data);     // 竞技场：跨天清次数+机器人漂移，跨月赛季结算发奖（2026-10-29）
        StaminaSystem.Tick(save: false);
        MercSkillMigrate.SyncAutoCastPrefsFromSave();
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
        if (SpotlightBuild.Enabled)
        {
            Debug.LogWarning("[SaveSystem] Spotlight：禁止云上传");
            UIManager.Instance?.ShowToast(SpotlightBuild.OfflineToast);
            return;
        }
        _data.SyncListsFromRuntime();
        string json = JsonUtility.ToJson(_data);
        string payload = ContentProtection.SaveEncryption
            ? Convert.ToBase64String(SecureCodec.EncryptUtf8(json))
            : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        CloudSaveBridge.UploadPayload(payload, ok =>
        {
            if (ok) Debug.Log("[SaveSystem] 云存档上传成功（或本地镜像）");
            else Debug.LogWarning("[SaveSystem] 云存档上传未完成（等待微信 SDK）");
        });
    }

    public void DownloadFromCloud(Action<bool> onComplete)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.LogWarning("[SaveSystem] Spotlight：禁止云下载");
            UIManager.Instance?.ShowToast(SpotlightBuild.OfflineToast);
            onComplete?.Invoke(false);
            return;
        }
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
                MercSkillMigrate.AlignSave(_data);
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
        RunLoadout.Clear();   // 【2026-10-29 堵「清档后中断快照残留」】快照存 PlayerPrefs 不随存档文件消失，
                              // 残留会弹「上一局意外退出」→ 点继续按旧快照跳关重打（1-1/1-2 领取后事故源头之一）
        StoryProgress.ResetRuntimeFlags();
        _data = CreateFreshSave();
        Save();
        NewGameReset.ApplyAll();   // 【2026-11-02】清档统一复位：把进程级状态（引导游标/登录弹窗/进镇闸…）一起归零

        Debug.Log("[SaveSystem] 已清除存档，新档 tutorialDone=" + _data.tutorialDone);
    }
}
