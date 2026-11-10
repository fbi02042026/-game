using System.Collections;
using UnityEngine;

/// <summary>
/// 《跑酷小游戏》V1 —— T6 结算 + 每日次数（EndlessRunGame 分部，2026-11-09）。
///
/// 规格来源：Docs/《跑酷小游戏》_待办任务清单.md §2.4 / §2.5 / §6 / §10。
/// 【双计数器】runExp（升级用）/ RunGold（带出用）局内内存分开计，吃 1 枚各 +1；不进存档。
/// 【带出 A 方案】死亡结算与中途退出走同一条 ResourceWallet.Add(Gold, runGold, false, false) + 显式 Save()。
/// 【tick】T2/T3/T4 已占 Update/LateUpdate/FixedUpdate，本分部零 Unity Update 家族、零输入 API；
///   轮询协程挂在文件内嵌的 SettleHost（DDOL）上——T2 Close() 会 SetActive(false) 杀掉本体协程，
///   中途退出检测必须活在体外（嵌套类可访问外层私有成员；挂点方式偏差已在回报注明）。
/// 【等级】Lv = 1 + ⌊runExp / exp_per_level⌋，跨级触发 LeveledUp（T5 飘字）；ATK 公式本期不落（T4 攻击是一刀清）。
/// </summary>
public partial class EndlessRunGame : MonoBehaviour
{
    // ============================================================
    // 对外契约（逐字固定，供 T5 HUD）
    // ============================================================

    /// <summary>本局带出金币（HUD 显示它，不是账户余额）。</summary>
    public long RunGold { get; private set; }

    /// <summary>当前等级：1 + ⌊runExp/100⌋（表 key exp_per_level）。</summary>
    public int RunLevel { get; private set; }

    /// <summary>等级变化时触发一次（T5 做头顶飘字）。</summary>
    public event System.Action LeveledUp;

    // ============================================================
    // 局内状态（不进存档）
    // ============================================================

    long _runExp;          // 升级用计数
    bool _runStarted;      // 本局已开始（TryConsumeEnter 成功后置位）
    bool _settled;         // 本局已结算（死亡 / 中途退出）
    bool _settleHooked;    // PlayerDied 已订阅

    const float SETTLE_POLL_SEC = 0.5f;   // 中途退出轮询周期
    const float COIN_PICKUP_PX = 60f;     // 金币拾取判定距离（表现参数，表内无 key）

    // ============================================================
    // 每日次数（照 EndlessRiftRules.EnsureDay / TryConsumeEnter 样板）
    // ============================================================

    /// <summary>今日是否还能进（上限 = daily_tries + runAdBonusToday）；T7 入口置灰用。</summary>
    public static bool CanEnterRun
    {
        get
        {
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data == null) return false;
            EnsureDay(data);
            return data.runTriesToday < RunTrackTable.GetInt("daily_tries", 3) + Mathf.Min(1, data.runAdBonusToday);
        }
    }

    /// <summary>进入一局：扣 1 次（中途退出不退还）并重置局内计数；次数用尽弹提示返回 false。</summary>
    public static bool TryConsumeEnter()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return false;
        EnsureDay(data);
        int cap = RunTrackTable.GetInt("daily_tries", 3) + Mathf.Min(1, data.runAdBonusToday);
        if (data.runTriesToday >= cap)
        {
            GlobalToastUI.Show($"今天的跑酷次数已用完（{data.runTriesToday}/{cap}）", true);
            return false;
        }
        data.runTriesToday++;
        SaveSystem.Instance.Save();
        EnsureRuntimeHost();
        Ensure().BeginRun();
        return true;
    }

    /// <summary>跨天清零：本地日 DayKeyUtil 口径 + 旧 UTC 键平移兼容（平移立即落盘）。</summary>
    static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = DayKeyUtil.TodayKey();
        if (data.runDayKey == today) return;
        if (DayKeyUtil.LegacyUtcDayKeyStillToday(data.runDayKey))
        {
            data.runDayKey = today;
            SaveSystem.Instance?.Save();
            return;
        }
        data.runDayKey = today;
        data.runTriesToday = 0;
        data.runAdBonusToday = 0;
    }

    /// <summary>开局（进本成功后）：清双计数器、回 1 级、订阅死亡事件。</summary>
    void BeginRun()
    {
        RunGold = 0;
        _runExp = 0;
        RunLevel = 1;
        _settled = false;
        _runStarted = true;
        HookDeathIfNeeded();
    }

    void HookDeathIfNeeded()
    {
        if (_settleHooked) return;
        _settleHooked = true;
        PlayerDied += OnPlayerDied;
    }

    // ============================================================
    // 金币拾取（每帧协程；读 T3 _props / PropKind / ReleaseProp，同类分部 private 可见）
    // ============================================================

    /// <summary>Coin 类 prop 与玩家 x 距离 ≤ 60px 判定拾取：双计数器各 +1 并回收该 prop。
    /// MercHead 同款判定距离：回收后触发「新佣兵天降窗口」（满员）或直接入队 + 横条（未满员）。</summary>
    void PickupCoins()
    {
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            var p = _props[i];
            if (!p.alive) continue;
            if (p.kind != PropKind.Coin && p.kind != PropKind.MercHead) continue;
            if (Mathf.Abs(p.logicX - _playerXPx) > COIN_PICKUP_PX) continue;
            if (p.kind == PropKind.MercHead)
                OnPickMercHead(p.hireId);
            else
                AddCoin();
            ReleaseProp(p);
            _props.RemoveAt(i);
        }
    }

    /// <summary>双计数器：吃 1 枚 runExp 与 runGold 各 +1（无加成倍率、无擦身额外金币）。</summary>
    void AddCoin()
    {
        _runExp++;
        RunGold++;
        int lv = 1 + (int)(_runExp / Mathf.Max(1, RunTrackTable.GetInt("exp_per_level", 100)));
        if (lv == RunLevel) return;
        RunLevel = lv;
        LeveledUp?.Invoke();   // ATK = 40 × Lv 本期不落：T4 攻击是一刀清简化
    }

    // ============================================================
    // 结算（死亡 / 中途退出同一条带币链路）
    // ============================================================

    void OnPlayerDied() => SettleRun();

    /// <summary>结算：带出 RunGold 进主金币池（save:false 不自动存，显式补一次 Save）。</summary>
    void SettleRun()
    {
        if (_settled) return;
        _settled = true;
        _runStarted = false;
        _playing = false;   // 冻结 T2/T3/T4 的 tick
        // 结算必须连玩法相机一起关：该相机 depth=90、clearFlags=SolidColor，会清掉主相机上
        // 的所有 ScreenSpaceCamera UI（主界面 / 本玩法的 HUD / 结算弹窗），不关就是一片空白。
        Close();
        long gold = RunGold;
        int meters = Mathf.FloorToInt(Meters);
        RunGold = 0;
        _runExp = 0;
        RunLevel = 1;
        ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, false, false);
        SaveSystem.Instance?.Save();
        StaminaConfirmPopup.ShowNotice("跑酷结算",
            "跑了 " + meters + " 米 / 带出 " + gold + " 金币",
            "一键再来", ReplayFromSettle);
    }

    /// <summary>一键再来：再进一局重新扣每日次数；扣不到留在关闭态。</summary>
    void ReplayFromSettle()
    {
        if (TryConsumeEnter()) Open();   // Open 内部走 T2 ResetRun + _playing=true
        else Close();
    }

    // ============================================================
    // 协程宿主（T2 Close() 会 SetActive(false) 杀掉本体协程 → 用 DDOL 体外宿主轮询）
    // ============================================================

    static SettleHost _host;

    /// <summary>懒建宿主（进本时首次触发；幂等）。</summary>
    static void EnsureRuntimeHost()
    {
        if (_host != null) return;
        var go = new GameObject("EndlessRunSettleHost");
        Object.DontDestroyOnLoad(go);
        _host = go.AddComponent<SettleHost>();
    }

    /// <summary>协程宿主：只跑两条协程（0.5s 结算轮询 + 每帧拾取），无任何 Unity Update 家族方法。</summary>
    sealed class SettleHost : MonoBehaviour
    {
        bool _prevPlaying;   // 上次轮询看到的 _playing（中途退出检测基准）

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(SettleLoop());
            StartCoroutine(CoinLoop());
        }

        /// <summary>0.5s 轮询：_playing true→false 且本局已开始未结算 → 中途退出结算带币。</summary>
        IEnumerator SettleLoop()
        {
            var wait = new WaitForSeconds(SETTLE_POLL_SEC);
            while (true)
            {
                yield return wait;
                var g = Instance;
                if (g == null || !g._built) continue;
                g.HookDeathIfNeeded();
                if (_prevPlaying && !g._playing && g._runStarted && !g._settled)
                    g.SettleRun();   // 中途退出：同一条带币结算（次数不退还）
                _prevPlaying = g._playing;
            }
        }

        /// <summary>每帧拾取：闸门 !_built/_playing/_appPaused 早退（本体未激活时 Instance 仍可读，逻辑零副作用）。</summary>
        IEnumerator CoinLoop()
        {
            while (true)
            {
                yield return null;
                var g = Instance;
                if (g == null || !g._built || !g._playing || g._appPaused) continue;
                g.PickupCoins();
            }
        }
    }
}

/// <summary>
/// 跑酷广告加次（做进去但不开放）：placement = run_extra_try，只认 ShowRewarded 回调 ok==true 发奖；
/// 绝不订阅 RewardedSuccess 全局广播。两层闸（RewardedAdBridge.PlatformClosed + Enabled）任一关闭入口不可见。
/// </summary>
public static class RunExtraTryAd
{
    /// <summary>跑酷本地总闸：false = 本期不开放（微信广告接入后再开）。</summary>
    public const bool Enabled = false;

    /// <summary>广告点位名（字符串常量，非数值表 key）。</summary>
    public const string Placement = "run_extra_try";

    /// <summary>入口可见性：两层闸任一关闭即 false（本期恒 false，T7 供查询）。</summary>
    public static bool ExtraTryVisible => Enabled && !RewardedAdBridge.PlatformClosed;

    /// <summary>看广告 +1 次当日次数（runAdBonusToday 0→1，上限 1）；只认回调 ok==true。</summary>
    public static void TryShow(System.Action<bool> onDone)
    {
        if (!ExtraTryVisible) { onDone?.Invoke(false); return; }
        RewardedAdBridge.ShowRewarded(Placement, ok =>
        {
            if (!ok) { onDone?.Invoke(false); return; }
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (data == null) { onDone?.Invoke(false); return; }
            if (data.runAdBonusToday == 0) data.runAdBonusToday = 1;
            SaveSystem.Instance?.Save();
            onDone?.Invoke(true);
        });
    }
}
