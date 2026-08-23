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

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 保存当前战斗状态（本版关闭：Restore 未接 LoadStage，写入会造成假续关）
    /// </summary>
    public void SaveBattleState()
    {
        // 仅清理脏档，不写入可恢复状态
        if (HasSavedBattle())
            ClearBattleState();
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
    /// 恢复战斗状态（本版未接入；保留实现供后续续关）
    /// </summary>
    public bool RestoreBattleState()
    {
        ClearBattleState();
        Debug.Log("[BattleStateSaver] 本版不支持中断续关，已清档");
        return false;
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
        int farmLevel = SaveSystem.Instance?.Data?.townLevel?.farm ?? 0;
        long offlineGold = OfflineGoldCalc.FromDuration(duration, farmLevel);

        if (offlineGold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, offlineGold, save: true, notify: true);

        if (offlineGold > 0)
        {
            Debug.Log($"[BattleStateSaver] 离线收益: {offlineGold} 金币 (离线{duration.TotalMinutes:F0}分钟)");
            OfflineRewardPopup.Show(offlineGold, Math.Min(duration.TotalMinutes, (8 + farmLevel * 2) * 60.0));
        }
    }

    /// <summary>
    /// 触发撤离（死亡规则）
    /// 暂停超时时调用，按死亡遗产流程处理
    /// </summary>
    private void TriggerEvacuation(BattleStateData state)
    {
        // 暂停超时按死亡经济：只保留进局前城镇金，禁止整额 Add
        var save = SaveSystem.Instance?.Data;
        if (save != null && state != null)
        {
            // state.currentGold 是战斗内钱包快照；用差额同步到城镇
            long delta = state.currentGold - save.totalGold;
            // 超时撤离按死亡：丢弃本局增量
            if (delta > 0)
            {
                // 不写入本局增量
            }
            else if (delta < 0)
                ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, -delta, save: true, notify: false);
        }
        ClearBattleState();
        Debug.Log("[BattleStateSaver] 暂停超时撤离：本局金币增量已丢弃，战斗存档已清");
    }

    /// <summary>
    /// 清除保存的战斗状态
    /// 战斗正常结束（通关/死亡）时调用
    /// </summary>
    public void ClearSavedState()
    {
        ClearBattleState();
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
