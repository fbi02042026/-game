using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 战斗状态保存/恢复系统
/// 玩家退出时保存当前战斗进度，上线时恢复
/// 核心规则：退出=暂停，角色停留在当前关卡，不推进、不自动选路线
/// </summary>
public class BattleStateSaver : MonoBehaviour
{
    public static BattleStateSaver Instance;

    // PlayerPrefs 键名
    private const string KEY_BATTLE_STATE = "BattleState";
    private const string KEY_PAUSE_START_TIME = "PauseStartTime";

    /// <summary>
    /// 战斗状态数据（可序列化）
    /// </summary>
    [Serializable]
    public class BattleStateData
    {
        public bool hasActiveBattle;      // 是否有进行中的战斗
        public int chapterId;             // 当前章节
        public int stageId;               // 当前关卡索引
        public string stageType;          // 关卡类型（普通/精英/BOSS等）
        public float heroCurrentHp;       // 玩家当前血量
        public int heroLevel;             // 玩家等级
        public long currentGold;          // 当前金币
        public int currentExp;            // 当前经验
        public string[] equippedItemIds;  // 身上装备的templateId列表
        public string[] backpackItemIds;  // 背包中的装备templateId列表
        public string exitTime;           // 退出时间（ISO 8601）
    }

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 保存当前战斗状态
    /// 在玩家退出/切后台/断网时调用
    /// </summary>
    public void SaveBattleState()
    {
        if (BattleManager.Instance == null || Hero.Instance == null)
        {
            PlayerPrefs.DeleteKey(KEY_BATTLE_STATE);
            return;
        }

        var bm = BattleManager.Instance;
        var cm = ChapterManager.Instance;

        BattleStateData state = new BattleStateData
        {
            hasActiveBattle = true,
            chapterId = cm?.currentChapter ?? 1,
            stageId = bm.currentStage != null ? bm.currentStage.stageIndex : 0,
            stageType = bm.currentStage != null ? bm.currentStage.type.ToString() : "Normal",
            heroCurrentHp = Hero.Instance.currentHp,
            heroLevel = Hero.Instance.level,
            currentGold = bm.currentGold,
            currentExp = Hero.Instance.currentExp,
            exitTime = DateTime.UtcNow.ToString("O")
        };

        // 保存装备状态
        if (GridBackpackSystem.Instance != null)
        {
            // 身上装备
            var equipped = GridBackpackSystem.Instance.GetEquippedItems();
            state.equippedItemIds = new string[equipped.Count];
            for (int i = 0; i < equipped.Count; i++)
            {
                state.equippedItemIds[i] = equipped[i]?.template?.templateId ?? "";
            }

            // 背包装备（只存templateId，不存随机词条，重新生成）
            var backpack = GridBackpackSystem.Instance.GetAllBackpackItems();
            state.backpackItemIds = new string[backpack.Count];
            for (int i = 0; i < backpack.Count; i++)
            {
                state.backpackItemIds[i] = backpack[i]?.equip?.template?.templateId ?? "";
            }
        }

        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(KEY_BATTLE_STATE, json);
        PlayerPrefs.Save();

        Debug.Log($"[BattleStateSaver] 战斗状态已保存: 章节{state.chapterId}-关卡{state.stageId}, 血量{state.heroCurrentHp}");
    }

    /// <summary>
    /// 检查是否有保存的战斗状态
    /// </summary>
    public bool HasSavedBattle()
    {
        return PlayerPrefs.HasKey(KEY_BATTLE_STATE);
    }

    /// <summary>清除未完成战斗存档（避免 Restore 不刷怪）</summary>
    public void ClearBattleState()
    {
        PlayerPrefs.DeleteKey(KEY_BATTLE_STATE);
        PlayerPrefs.DeleteKey(KEY_PAUSE_START_TIME);
        PlayerPrefs.Save();
        Debug.Log("[BattleStateSaver] 已清除战斗存档");
    }

    /// <summary>
    /// 恢复战斗状态
    /// 游戏启动时调用，如果有保存的状态则恢复，否则开始新游戏
    /// 返回值：true=成功恢复，false=无保存状态或已超时
    /// </summary>
    public bool RestoreBattleState()
    {
        if (!PlayerPrefs.HasKey(KEY_BATTLE_STATE))
        {
            Debug.Log("[BattleStateSaver] 无保存的战斗状态，开始新游戏");
            return false;
        }

        string json = PlayerPrefs.GetString(KEY_BATTLE_STATE);
        BattleStateData state = JsonUtility.FromJson<BattleStateData>(json);

        if (state == null || !state.hasActiveBattle)
        {
            ClearSavedState();
            return false;
        }

        // 检查暂停超时（30分钟）
        DateTime exitTime = DateTime.Parse(state.exitTime);
        TimeSpan offlineDuration = DateTime.UtcNow - exitTime;
        if (offlineDuration.TotalMinutes >= 30)
        {
            Debug.Log($"[BattleStateSaver] 暂停超时 {offlineDuration.TotalMinutes:F0} 分钟，触发撤离");
            TriggerEvacuation(state);
            ClearSavedState();
            return false;
        }

        // 恢复战斗状态
        RestoreBattle(state);

        // 计算离线收益（农场离线宝箱）
        CalculateOfflineReward(offlineDuration);

        Debug.Log($"[BattleStateSaver] 战斗状态已恢复: 章节{state.chapterId}-关卡{state.stageId}, 离线{offlineDuration.TotalMinutes:F1}分钟");
        return true;
    }

    /// <summary>
    /// 恢复战斗场景和角色状态
    /// </summary>
    private void RestoreBattle(BattleStateData state)
    {
        // 设置章节和关卡
        if (ChapterManager.Instance != null)
        {
            ChapterManager.Instance.SetChapter(state.chapterId);
            ChapterManager.Instance.StartChapter(state.chapterId);
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.currentGold = state.currentGold;
        }

        // 恢复角色状态
        if (Hero.Instance != null)
        {
            Hero.Instance.currentHp = state.heroCurrentHp;
            Hero.Instance.level = state.heroLevel;
            Hero.Instance.currentExp = state.currentExp;
        }

        // 恢复装备（简化版：根据templateId重新生成实例）
        if (GridBackpackSystem.Instance != null && ConfigManager.Instance != null)
        {
            GridBackpackSystem.Instance.InitNewRun();

            // 恢复背包装备
            if (state.backpackItemIds != null)
            {
                foreach (string templateId in state.backpackItemIds)
                {
                    if (string.IsNullOrEmpty(templateId)) continue;
                    EquipTemplate template = ConfigManager.Instance.GetEquipTemplate(templateId);
                    if (template != null)
                    {
                        EquipInstance inst = EquipInstance.GenerateFromTemplate(template);
                        GridBackpackSystem.Instance.TryAddItem(inst, out _);
                    }
                }
            }

            // 恢复身上装备（先放入背包再穿戴）
            if (state.equippedItemIds != null)
            {
                foreach (string templateId in state.equippedItemIds)
                {
                    if (string.IsNullOrEmpty(templateId)) continue;
                    EquipTemplate template = ConfigManager.Instance.GetEquipTemplate(templateId);
                    if (template != null)
                    {
                        EquipInstance inst = EquipInstance.GenerateFromTemplate(template);
                        if (GridBackpackSystem.Instance.TryAddItem(inst, out var backpackItem))
                        {
                            GridBackpackSystem.Instance.EquipItem(backpackItem);
                        }
                    }
                }
            }
        }

        // 更新UI
        BattleUI.Instance?.UpdateStageInfo(state.chapterId, state.stageId, state.stageType, state.currentGold);
        BattleUI.Instance?.UpdateCharacterSlots();
    }

    /// <summary>
    /// 计算离线收益（农场离线宝箱）
    /// 收益按农场等级计算，和是否在战斗中无关
    /// </summary>
    private void CalculateOfflineReward(TimeSpan duration)
    {
        // 限制最大离线时间（农场等级决定）
        int farmLevel = SaveSystem.Instance?.Data?.townLevel?.farm ?? 0;
        int maxOfflineHours = 8 + farmLevel * 2; // 基础8小时，每级农场+2小时
        double effectiveMinutes = Math.Min(duration.TotalMinutes, maxOfflineHours * 60);

        // 金币/分钟 = 10 + 农场等级 * 10
        int goldPerMinute = 10 + farmLevel * 10;
        long offlineGold = (long)(effectiveMinutes * goldPerMinute);

        // 添加到城镇总金币（上限溢出进邮件）
        if (offlineGold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, offlineGold, save: true, notify: true);

        // 显示离线收益提示
        if (offlineGold > 0)
        {
            Debug.Log($"[BattleStateSaver] 离线收益: {offlineGold} 金币 (离线{effectiveMinutes:F0}分钟)");
            // TODO: 弹窗显示离线收益
        }
    }

    /// <summary>
    /// 触发撤离（死亡规则）
    /// 暂停超时时调用，按死亡遗产流程处理
    /// </summary>
    private void TriggerEvacuation(BattleStateData state)
    {
        // 保存金币到城镇（走上限）
        if (state != null && state.currentGold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, state.currentGold, save: true, notify: false);

        Debug.Log("[BattleStateSaver] 已触发撤离，金币已保存");
    }

    /// <summary>
    /// 清除保存的战斗状态
    /// 战斗正常结束（通关/死亡）时调用
    /// </summary>
    public void ClearSavedState()
    {
        PlayerPrefs.DeleteKey(KEY_BATTLE_STATE);
        PlayerPrefs.Save();
        Debug.Log("[BattleStateSaver] 战斗状态已清除");
    }

    /// <summary>
    /// 应用退出时保存
    /// 监听应用退出事件
    /// </summary>
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            // 应用进入后台，保存战斗状态
            SaveBattleState();
        }
    }

    void OnApplicationQuit()
    {
        SaveBattleState();
    }
}
