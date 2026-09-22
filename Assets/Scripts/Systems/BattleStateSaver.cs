using UnityEngine;
using System;
using System.Collections;
using System.Linq;

/// <summary>
/// 战斗中断存档 / 撤离失败结算（2026-09-14 改版）
/// ------------------------------------------------------------------
/// 旧规则：中断后 3 分钟内重进自动续关（从该关开头重打）。
/// 新规则：
///   · 不杀进程 → 游戏一直在内存里，进度自然保留，不需要任何存档。
///   · 杀进程 / 崩溃 / 强退 → 下次进游戏判定为「撤离失败」：
///       本局金币保留、照发天赋石、技能/装备/佣兵清空、体力不退。
///       **不再续关，也没有时间窗口。**
///   · 2026-09-22 主人要求：这种「中断退出」**不弹结算面板**（隔很久再上线看到一屏结算很怪），
///       静默走同一套结算规则，只给一条轻提示；结算面板只在**在线撤离 / 在线失败**时弹（BattleManager）。
///
/// 判定依据：正常结束（通关 / 死亡 / 撤离）都会 ClearBattleState()，
/// 所以「启动时有残留档」== 上次不是正常收尾 == 被强杀。
///
/// 只存「结算需要的数据」（关卡坐标 + 金币基线 + 统计快照），
/// 不存装备/血量——进程死了它们本来就没了。
/// </summary>
public class BattleStateSaver : MonoBehaviour
{
    public static BattleStateSaver Instance;

    // PlayerPrefs 键名
    private const string KEY_BATTLE_STATE = "BattleState";
    private const string KEY_PAUSE_START_TIME = "PauseStartTime";

    /// <summary>心跳写入间隔（秒）：保证崩溃时快照不会太旧</summary>
    private const float HEARTBEAT_INTERVAL = 10f;

    private float _heartbeatTimer;

    /// <summary>
    /// 战斗状态数据（可序列化）
    /// </summary>
    [Serializable]
    public class BattleStateData
    {
        public bool hasActiveBattle;      // 是否有进行中的战斗
        public int chapterId;             // 当前章节
        public int stageId;               // 当前关卡索引（0 起）
        public string stageType;          // 关卡类型
        public int difficulty;            // 难度 0/1/2
        public bool isGoldDungeon;        // 是否金币副本
        public long currentGold;          // 战斗内钱包快照（含城镇底金）
        public long goldAtRunStart;       // 本局开始时的金币基线（算净赚用）
        public float heroCurrentHp;       // 仅记录，不用于恢复
        public int heroLevel;             // 等级系统已停用，仅占位
        public int currentExp;            // 仅记录
        public string exitTime;           // 退出/心跳时刻（ISO 8601 / UTC）

        // === 结算面板需要的统计快照 ===
        public int kills;
        public int eliteKills;
        public int bossKills;
        public float damageDealt;
        public float damageTaken;
        public float healingReceived;
        public float battleTimeSec;
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

    void Update()
    {
        // 心跳：只在真·战斗中写，保证崩溃时快照不过旧
        if (!IsInActiveBattle()) return;
        _heartbeatTimer += Time.unscaledDeltaTime;
        if (_heartbeatTimer < HEARTBEAT_INTERVAL) return;
        _heartbeatTimer = 0f;
        SaveBattleState();
    }

    /// <summary>当前是否处在可记录的战斗中（排除休息关 / 已结算 / 城镇）</summary>
    static bool IsInActiveBattle()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return false;
        if (!bm.isInBattle) return false;
        if (bm.currentStage == null) return false;
        if (bm.currentStage.type == StageType.Rest) return false;
        return true;
    }

    // ============================================================
    // 写入
    // ============================================================

    /// <summary>写一次中断快照（进关时 + 每 10s 心跳 + 切后台 / 退出）。</summary>
    public void SaveBattleState()
    {
        if (!IsInActiveBattle())
            return;

        var bm = BattleManager.Instance;
        var cm = ChapterManager.Instance;
        var hero = Hero.Instance;
        var st = bm.RunStats;

        var data = new BattleStateData
        {
            hasActiveBattle = true,
            chapterId = cm != null ? cm.currentChapter : bm.CurrentChapter,
            stageId = bm.currentStage != null ? bm.currentStage.stageIndex : (cm != null ? cm.currentStageIndex : 0),
            stageType = bm.currentStage != null ? bm.currentStage.type.ToString() : StageType.Normal.ToString(),
            difficulty = bm.BattleDifficulty,
            isGoldDungeon = bm.IsGoldDungeon,
            currentGold = bm.currentGold,
            goldAtRunStart = bm.GoldAtRunStart,
            heroCurrentHp = hero != null ? hero.currentHp : 0f,
            heroLevel = hero != null ? hero.level : 1,
            currentExp = hero != null ? hero.currentExp : 0,
            exitTime = DateTime.UtcNow.ToString("o"),
            kills = st != null ? st.KillCount : 0,
            eliteKills = st != null ? st.EliteKillCount : 0,
            bossKills = st != null ? st.BossKillCount : 0,
            damageDealt = st != null ? st.DamageDealt : 0f,
            damageTaken = st != null ? st.DamageTaken : 0f,
            healingReceived = st != null ? st.HealingReceived : 0f,
            battleTimeSec = st != null ? st.BattleTimeSec : 0f
        };

        try
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(KEY_BATTLE_STATE, json);
            PlayerPrefs.SetString(KEY_PAUSE_START_TIME, data.exitTime);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleStateSaver] 写入战斗存档失败: " + e.Message);
        }
    }

    /// <summary>是否有中断残留档（不判时间——有档就说明上次没正常收尾）</summary>
    public bool HasSavedBattle()
    {
        return PeekInterrupted() != null;
    }

    /// <summary>清除中断存档</summary>
    public void ClearBattleState() => ClearSaveStatic();

    /// <summary>战斗正常结束（通关/死亡/撤离）时调用</summary>
    public void ClearSavedState() => ClearSaveStatic();

    // ============================================================
    // 读取（静态，城镇启动时不依赖实例）
    // ============================================================

    /// <summary>解析中断存档；无效/损坏返回 null</summary>
    public static BattleStateData PeekInterrupted()
    {
        if (!PlayerPrefs.HasKey(KEY_BATTLE_STATE)) return null;
        string json = PlayerPrefs.GetString(KEY_BATTLE_STATE, "");
        if (string.IsNullOrEmpty(json)) return null;
        BattleStateData data = null;
        try { data = JsonUtility.FromJson<BattleStateData>(json); }
        catch { data = null; }
        if (data == null || !data.hasActiveBattle) return null;
        return data;
    }

    /// <summary>无条件清掉中断存档（启动结算后 / 开新战斗前的兜底）</summary>
    public static void ClearInterruptedSave() => ClearSaveStatic();

    static void ClearSaveStatic()
    {
        PlayerPrefs.DeleteKey(KEY_BATTLE_STATE);
        PlayerPrefs.DeleteKey(KEY_PAUSE_START_TIME);
        PlayerPrefs.Save();
    }

    // ============================================================
    // 撤离失败结算（Town 场景启动完成后调用一次）
    // ============================================================

    /// <summary>
    /// 启动检查：有残留档 = 上次被强杀 → 按「撤离失败」结算（经济/清档规则同在线撤离失败）。
    /// 2026-09-22 起**不弹结算面板**，只静默结算 + 一条轻提示。
    /// 返回 true 表示弹了面板（调用方应跳过本轮其它弹窗）；本流程恒返回 false。
    /// </summary>
    public static bool SettleInterruptedRun()
    {
        var d = PeekInterrupted();
        if (d == null) return false;

        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            // 存档还没就绪，别瞎结算，等下次进城镇
            return false;
        }

        // 引导期强退：直接丢掉，不弹面板，避免打断剧情
        if (!StoryProgress.TutorialDone)
        {
            ClearSaveStatic();
            Debug.Log("[BattleStateSaver] 引导期中断，静默清档");
            return false;
        }

        ClearSaveStatic();

        int chapter = d.chapterId < 1 ? 1 : d.chapterId;
        int stageIdx = d.stageId < 0 ? 0 : d.stageId;
        string stageLabel = d.isGoldDungeon ? "金币副本" : $"{GameConfig.GetChapterMapName(chapter)} 第{stageIdx + 1}关";

        // === 结算：与撤离同经济（金币保留 + 天赋石照发；构筑清空；体力不退）===
        long delta = d.currentGold - d.goldAtRunStart;   // 注意：Mathf.Max 没有 long 重载，别用
        if (delta < 0) delta = 0;
        if (delta > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, delta, save: false, notify: false);

        // 2026-09-19：天赋石不再由战斗内金币换算产出，改由冒险日志里程碑发放。
        int talentGain = 0;

        // 本局构筑 / 城镇雇佣一律清空
        RunLoadout.Clear();
        if (data.hiredMercs == null) data.hiredMercs = new System.Collections.Generic.List<MercenaryData>();
        data.hiredMercs.Clear();
        MercHireSession.ClearHired();
        SaveSystem.Instance.Save();

        Debug.Log($"[BattleStateSaver] 上次战斗被中断 → 判撤离失败（静默，不弹结算面板）：{stageLabel}，"
                  + $"金币 +{delta}，天赋石 +{talentGain}，击杀 {d.kills}");

        // 2026-09-22 主人要求：中断退出不再弹结算面板，隔很久再上线看到一屏结算很怪。
        // 结算面板只在「在线撤离 / 在线失败」时弹（见 BattleManager）。
        // 这里的经济与清档规则**一字不改**，只把面板换成一条轻提示。
        GlobalToastUI.Show("上次战斗被中断，已按撤离失败结算，本局金币已保留");
        return false;
    }

    // ============================================================
    // 生命周期
    // ============================================================

    void OnApplicationPause(bool pause)
    {
        if (pause) SaveBattleState();
    }

    void OnApplicationQuit()
    {
        SaveBattleState();
    }
}
