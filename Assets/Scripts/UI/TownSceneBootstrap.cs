using System.Collections;
using UnityEngine;

/// <summary>
/// Town 场景启动：预热字体、预实例化各功能页（隐藏），保证底栏点击无延迟。
/// Boot / Battle / Town 三场景；切场景时由 SceneLoadingCoordinator 等待本脚本完成后再关 Loading。
/// </summary>
public class TownSceneBootstrap : MonoBehaviour
{
    static bool _done;

    /// <summary>本次进 Town 后预热是否完成</summary>
    public static bool IsLoadComplete { get; private set; }

    public static void ResetForSceneLoad()
    {
        IsLoadComplete = false;
        _done = false;
        _offlineClaimedThisTownVisit = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttachIfTown()
    {
        if (!GameSceneGate.IsTown) return;
        if (_done) return;

        var hall = Object.FindObjectOfType<GuildHallUI>();
        if (hall != null)
        {
            if (hall.GetComponent<TownSceneBootstrap>() == null)
                hall.gameObject.AddComponent<TownSceneBootstrap>();
            return;
        }

        // GuildHallUI 尚未就绪时挂到场景任意存活对象，避免 Loading 卡在 55%
        Component host = Object.FindObjectOfType<TownSceneManager>();
        if (host == null) host = Object.FindObjectOfType<TownHubController>();
        if (host == null)
        {
            var go = new GameObject("TownSceneBootstrap");
            go.AddComponent<TownSceneBootstrap>();
            return;
        }
        if (host.GetComponent<TownSceneBootstrap>() == null)
            host.gameObject.AddComponent<TownSceneBootstrap>();
    }

    // ——【2026-10-11 主人拍板】主线任务轮询驱动 ——
    // 任务条（QuestHudBar）停用之前，MainQuestSystem.Tick() 的 0.5s 轮询**唯一**驱动点是
    // QuestHudBar.Update（它每 0.5s 结算「雇人 / 点天赋」这类达标即结算的主线任务）。
    // 任务条去掉后若没有替代驱动，这类主线会永远卡住不推进 → 驱动挪到这里。
    // 条件沿用 QuestHudBar 原来的生效条件（城镇 + 引导已完成 + 存档已读）。
    const float QuestTickInterval = 0.5f;
    float _questTickTimer = QuestTickInterval;

    void Update()
    {
        if (!GameSceneGate.IsTown) return;
        if (!StoryProgress.TutorialDone) return;
        if (SaveSystem.Instance == null || SaveSystem.Instance.Data == null) return;

        _questTickTimer -= Time.unscaledDeltaTime;
        if (_questTickTimer > 0f) return;
        _questTickTimer = QuestTickInterval;
        MainQuestSystem.Tick();
    }

    void Start()
    {
        if (!GameSceneGate.IsTown) return;
        if (_done) return;
        StartCoroutine(BootstrapRoutine());
    }

    IEnumerator BootstrapRoutine()
    {
        IsLoadComplete = false;
        TownSharedChrome.InvalidateCache();

        // 1/3 字体
        try
        {
            GameFonts.GetChinese();
            GameFonts.GetNumber();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 字体预热异常: " + e.Message);
        }
        SceneLoadingCoordinator.ReportPostLoadStep(1, 3);
        yield return null;

        // 2/3 常用预制体
        try
        {
            Resources.Load<GameObject>("Prefabs/Town/TavernUI");
            Resources.Load<GameObject>("Prefabs/Town/AdventureUI");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 预制体预热异常: " + e.Message);
        }
        SceneLoadingCoordinator.ReportPostLoadStep(2, 3);
        yield return null;

        // 3/3 功能页预实例化
        try
        {
            TownHubController hub = TownHubController.Instance;
            if (hub == null) hub = FindObjectOfType<TownHubController>();
            if (hub != null)
                hub.PreloadAllPages();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TownBootstrap] 功能页预加载异常: " + e);
        }

        SceneLoadingCoordinator.ReportPostLoadStep(3, 3);
        yield return null;

        _done = true;
        IsLoadComplete = true;
        Debug.Log("[TownBootstrap] Town 功能页预加载完成（切页应无 Instantiate 延迟）");
        if (SceneLoadingCoordinator.IsActive)
            SceneLoadingCoordinator.Finish();
        yield return null;
        yield return null;
        TownHubController.ConsumePendingAdventure();
        // 【2026-10-25 主人拍板「战斗结束回到冒险界面才触发」】战斗结算挂起的恭喜升级弹窗，
        // 回到冒险界面（Town 就绪、Loading 已收）才弹；无挂起 no-op（见 LevelUpPopup.FlushPending）。
        LevelUpPopup.FlushPending();
        // 上次战斗被强杀 → 判撤离失败并结算。
        // 2026-09-22 起中断结算改为静默（不弹面板），所以这里恒为 false，
        // 登录奖励等本轮弹窗照常走（保留返回值的判断，将来恢复弹窗时不用改调用方）。
        bool settled = SettleInterruptedRunOnce();
        // 【2026-10-22 施工单 T6】旧存档 mercGrowItems 的 frag:{hireId} 存量一次性迁进背包
        //（溢出走邮件；badge: 不动；标记 mercFragMigrated 防重复执行）。进城镇后跑，此时背包系统已就绪。
        MercGrowInventory.MigrateLegacyFragmentsToBag();
        // 【2026-10-20 补偿】「Lv2 已领但没拿到暮火之杖」的存量玩家进镇自动补发
        //（幂等：GrantKey 已写就跳过；失败不写标记，下次进镇重试）。
        SpecialWeapons.RepairMissingLv2Staff();
        // 【2026-10-24 主人拍板】开发期福利邮件（默认3佣兵+塔克碎片×80、两种卷轴×10、钻石/天赋石×1000、金币×9999）：
        // 每次启动进主界面发一轮；正式包（Build Settings 不勾 Development Build）自动关闭，打包不用改代码。
        // 唯一调用点，开关与数量都在 DevGiftMail 内。
        DevGiftMail.TrySendOncePerLaunch();
        if (!settled)
        {
            // 2026-10-15 主人拍板：登录检查改成「每次进主界面都查」——_loginCheckedThisTownVisit 以前
            // 只置 true 从不重置，实际变成「整个进程只查一次」：引导途中首次进镇时天赋页开着，
            // 等主界面的协程死在等待里，之后进镇再也不查 → 每日登录弹不出来。
            // BootstrapRoutine 每次跑（= 每次回主界面）先重置，配合 DailyLoginUI 的等待机制照常工作。
            _loginCheckedThisTownVisit = false;
            // 2026-09-20 主人要求：离线金币暂停，等重新设计后再启用（进城镇不再自动结算/弹窗）。
            // TryClaimTownOfflineReward();
            TryDailyLoginOnce();
        }
        TutorialDirector.Instance?.NotifyTownReady();
        // 主界面「当前目标」条：进 Town 就挂上，玩家不用点开冒险日志才知道干嘛
        // 【2026-10-10 主人拍板】去掉 —— 任务条在天赋页等弹层上还顶着、点也关不掉（主人原话：
        // 「天赋左上角还有任务框 而且也关不掉 先去掉吧 等后面在做」）。想恢复把这块注释打开即可。
        // ⚠ 恢复时**不要**把 MainQuestSystem.Tick() 搬回 QuestHudBar：轮询驱动在本文件 Update（见上），
        //   两处都 Tick 会出现「时机不一致 + 重复结算」，保持这一个出口。
        // try
        // {
        //     QuestHudBar.Ensure();
        // }
        // catch (System.Exception e)
        // {
        //     Debug.LogWarning("[TownBootstrap] 任务条挂载异常: " + e.Message);
        // }
    }

    static bool _loginCheckedThisTownVisit;

    /// <summary>
    /// 登录奖励：进 Town 检查一次，有可领的才弹。
    /// 累计天数在 OnEnterGame 里按自然日累加（同一天重进不加）。
    /// </summary>
    public static void TryDailyLoginOnce()
    {
        if (_loginCheckedThisTownVisit) return;
        // 引导未完成不弹，避免半透明遮罩挡「点冒险」
        if (!StoryProgress.TutorialDone) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        _loginCheckedThisTownVisit = true;

        MailSystem.EnsureWelcomeMail();   // 🆕 已通关老玩家回主界面补发欢迎信（幂等：已发过不再入邮、不再弹提示）
        MailSystem.EnsureCompensationMail();   // 🆕 全服补偿邮件（2026-10-17：500金币+卷轴，幂等同欢迎信链路）
        DailyLoginSystem.OnEnterGame();
        // 【2026-10-20 修】OnEnterGame 跨天累加 LoginDays 后必须立刻同步红点：
        // GuildHallUI.Awake 的 RefreshCommon 跑在本协程之前，按的是昨天的天数；
        // 不补这一行的话，跨天当次进镇新格子已解锁但主界面红点要等下次刷新才亮。
        RedDot.RefreshCommon();
        // 【2026-10-16 主人拍板】「已经领过每日登录的就不要每次再弹」：
        // 一天最多自动弹一次——今天弹过（或领过）就不再自动弹，避免回主界面反复弹；
        // 手动入口（主界面每日登录按钮）仍可随时打开。
        if (DailyLoginSystem.HasClaimable() && data.loginPopupShownDay != ShopDefs.TodayKey())
        {
            data.loginPopupShownDay = ShopDefs.TodayKey();
            SaveSystem.Instance.Save();
            DailyLoginUI.RequestShowOnMainUi();
        }
    }

    static bool _resumeChecked;

    /// <summary>每次进 Town 只检查一次中断结算（真结算了返回 true）</summary>
    static bool SettleInterruptedRunOnce()
    {
        if (_resumeChecked) return false;
        _resumeChecked = true;
        try
        {
            return BattleStateSaver.SettleInterruptedRun();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TownBootstrap] 中断结算异常: " + e.Message);
            return false;
        }
    }

    static bool _offlineClaimedThisTownVisit;

    /// <summary>进城镇一次最多弹一次离线收益（农场金）。</summary>
    // 2026-09-20 主人要求：离线金币暂停，等重新设计后再启用。
    // 自动结算（ResourceWallet.Add 金币）与自动入口/弹窗均停止；方法保留以便重设计时一行打开。
    static void TryClaimTownOfflineReward()
    {
        // 2026-09-20 离线金币暂停：直接返回，不结算、不弹窗、不建入口。
        // 恢复方式：删掉下面这行 return，并取消注释原逻辑块即可。
        return;

        // —— 以下为暂停前的原逻辑（已注释，待重设计后恢复）——
        // if (_offlineClaimedThisTownVisit) return;
        // if (!StoryProgress.TutorialDone) return;
        // var save = SaveSystem.Instance;
        // if (save?.Data == null) return;
        // long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // long secs = System.Math.Max(0, now - save.Data.lastSaveTime);
        // if (secs < 60) return;
        // int farm = save.Data.townLevel != null ? save.Data.townLevel.farm : 0;
        // long gold = save.CalcOfflineGold();
        // _offlineClaimedThisTownVisit = true;
        // if (gold <= 0) { save.Save(); return; }
        // ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: false);
        // double minutes = System.Math.Min(secs / 60.0, (8 + farm * 2) * 60.0);
        // GuildHallUI.OfflineRewardGold = gold;
        // GuildHallUI.OfflineRewardMinutes = minutes;
        // GuildHallUI.Instance?.TryShowOfflineRewardEntry();
    }

    void OnDestroy()
    {
        _done = false;
        IsLoadComplete = false;
        TownSharedChrome.InvalidateCache();
    }
}
