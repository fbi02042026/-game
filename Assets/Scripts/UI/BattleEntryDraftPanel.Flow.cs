using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 进关抽奖面板 partial —— 2026-10-18 由 BattleEntryDraftPanel.cs 纯位移拆出（无逻辑改动）
// 职责：对外流程与生命周期（Show/Hide/Ensure/Prewarm、Begin/Finish、5 秒软引导、map 对照与归位、LateUpdate）
public partial class BattleEntryDraftPanel
{
    // ============================================================
    // 对外接口（与旧 SlotOfferUI 的签名保持一致，BattleManager 只需换类名）
    // ============================================================

    /// <summary>
    /// 展开面板并显示抽奖按钮。
    /// <paramref name="onPick"/>：null = 点了普通（随机），有值 = 点了该类型定向。
    /// <paramref name="onFight"/>：点了「继续」。
    /// <paramref name="freeLeft"/>：本关还剩几次**免费**抽（金币本开局送的那几次）。0 = 没有，正常按价扣币。
    /// </summary>
    public static void Show(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
                            Action<DraftCategory?> onPick, Action onFight, int freeLeft = 0)
    {
        // 【2026-10-28 主人报「不是说过抽奖时把这个弹窗都关了吗」】抽奖面板弹出前，
        // 把还开着的「获得」弹窗关掉（获得佣兵详情 MercObtainPopupUI / 获得技能 SkillObtainPopupUI）：
        //   引导里「点新伙伴头像看底细」那一步（引导第 15 步）开的佣兵详情窗是带暗幕的模态，
        //   玩家看完没关就进下一次抽奖 → 它会一直盖在抽奖面板上，还挡住抽奖按钮的点击。
        //   主人原话「抽奖时把这个弹窗都关了」：抽奖这一屏上不允许有它。
        //   CloseIfOpen 走的是正常关闭出口（照常回调 onClosed，等价于玩家点了确认），抽奖协程不会卡。
        MercObtainPopupUI.CloseIfOpen();
        SkillObtainPopupUI.CloseIfOpen();

        var ui = Ensure();
        if (ui == null)
        {
            // 拿不到面板就直接开战，绝不把玩家卡在抽奖阶段（与旧 SlotOfferUI 一致）
            onFight?.Invoke();
            return;
        }
        // 【2026-10-20 玩家反馈修复】复用路径的第二道守卫：Instance 是上一关留下的，
        // 若此刻宿主父链未激活（背包抽屉被隐藏 / BattleUI 未就绪），Begin 里的 StartCoroutine
        // 同样会炸并把开战协程带死 → 站原地 + 没按钮。按同一口径直接开战，不冻场。
        if (!ui.gameObject.activeInHierarchy)
        {
            Debug.LogError("[BattleEntryDraftPanel] 复用面板失败：宿主父链未激活，本次跳过抽奖直接开战");
            onFight?.Invoke();
            return;
        }
        ui.Begin(randomPrice, focusPrice, cats, coins, onPick, onFight, freeLeft);
    }

    /// <summary>收起面板并隐藏抽奖按钮（开战/超时都走这里）。</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.Finish();
    }

    /// <summary>
    /// 【2026-11-10 主人拍板「只有抽奖面板收回，整个面板下移」】竞技场点「开始战斗」后调用：
    /// 整块面板一步落位回收起态（600 / 内容归位 / DraftRoot -400），<b>不隐藏抽奖按钮</b>——
    /// 与 Hide()/Finish() 的「彻底隐藏」区分开，按钮跟着面板一起下移。
    /// 正式关 / 金币本 / 资源副本仍走 BeginLive 的常显实时抽，不受影响。
    /// 只由 BattleManager.DoStartFight 在竞技场分支调（「对战」动画藏回之后）。
    /// </summary>
    public static void CollapseLive()
    {
        var ui = Instance;
        if (ui == null || !ui._liveMode) return;
        if (ui._animCo != null) { ui.StopCoroutine(ui._animCo); ui._animCo = null; }
        ui._expanded = false;
        ui._animK = 0f;
        ui.ApplyPanelState(0f);   // 与 BeginLive 的 ApplyPanelState(1f) 对称，不走滑动动画
        // 面板收回，战斗内「币够了没抽」引导不该再飘在画面上（同 Finish 口径）
        ui._liveDrawnThisStage = true;
        ui._liveDrawHintTimer = 0f;
        if (ui._liveDrawHintShown)
        {
            ui._liveDrawHintShown = false;
            TutorialHintUI.Instance?.Hide();
        }
        ui.LogState("CollapseLive");
    }

    // ============================================================
    // 🔴【2026-10-29 主人拍板·实时抽改版】LiveMode（常显实时抽）
    // ============================================================

    /// <summary>
    /// 实时抽进关阶段：DraftRoot 用展开态布局<b>常驻</b>（无展开动画）+「开始战斗」按钮。
    /// 正式关/金币本进关后调（裂隙与引导局走旧面板，不进这里）。
    /// <paramref name="onFight"/> = 玩家点「开始战斗」（DoContinue 的 LiveMode 分支）→ 回调 BattleManager 刷首波。
    /// 拿不到面板直接回调开打（与 Show 的防卡死口径一致，绝不把玩家卡在进关阶段）。
    /// </summary>
    public static void EnterLiveStart(Action onFight)
    {
        var ui = Ensure();
        if (ui == null || !ui.gameObject.activeInHierarchy)
        {
            if (ui == null)
                Debug.LogError("[BattleEntryDraftPanel] EnterLiveStart 拿不到面板，直接开打");
            onFight?.Invoke();
            return;
        }
        ui.BeginLive(onFight);
    }

    void BeginLive(Action onFight)
    {
        Build();
        _liveMode = true;
        _draftActive = false;
        _drawLocked = false;
        _cdActive = false;
        _onPick = null;
        _onFight = onFight;
        // 【2026-10-31】每场战斗进关重置「币够了没抽」引导：本场没抽过、还没弹过、计时归零
        _liveDrawnThisStage = false;
        _liveDrawHintShown = false;
        _liveDrawHintTimer = 0f;
        // ⚠ 不走动画：主人原话「不会出现抽奖时那个界面动画了」—— 直接按展开态真值表一步落位
        //（面板 750 / zhuangshi +80 / DraftRoot -250），与旧面板 Begin 的 SetExpanded(true) 同一套数值，
        // 只是跳过 CoAnimState 插值。【2026-11-09 起 map/世界层恒在终态，不再有任何过渡】
        if (_animCo != null) { StopCoroutine(_animCo); _animCo = null; }
        if (_settleCo != null) { StopCoroutine(_settleCo); _settleCo = null; }
        SetDraftVisible(true);
        _expanded = true;
        // 【2026-10-31】面板 UI 一步落位不变（主人拍板「不会出现抽奖时那个界面动画了」）。
        // 【2026-11-09 主人拍板】0.25s 滑动整套去掉：map / 世界层从第一帧起就恒在终态，
        // 预制体已摆到该位置，运行时不再有「收起→展开」的位移。
        ApplyPanelState(1f);
        RefreshLive();
        ShowLiveEntryDrawHint();
        LogState("BeginLive");
    }

    /// <summary>
    /// LiveMode 的按钮刷新：价格 / 余额 / 免费额度 / 可抽类别全部走 SlotMachineSystem 与
    /// BattleManager 的唯一出口现算，与 TryLiveDraw 的扣费判定自动同源，不另设参数。
    /// 每次抽完（含 reroll / 失败）与 CD 起止沿各调一次。
    /// </summary>
    public void RefreshLive()
    {
        var bm = BattleManager.Instance;
        if (bm == null || !_liveMode) return;
        var cats = SlotMachineSystem.AvailableCategories();
        Refresh(SlotMachineSystem.DrawPrice(), SlotMachineSystem.DrawFocusPrice(),
                cats, bm.currentGold, SlotMachineSystem.FreeDrawsLeft());
        // 【2026-10-31 竞技场额度模式】顶栏「剩余N次」跟着每抽刷新（金币位改次数显示，
        // 见 BattleUI.UpdateDraftCoin；竞技场金币不动 → 不会走 NotifyPoolGoldChanged 链）。
        if (bm.IsArena)
            BattleUI.Instance?.UpdateDraftCoin(bm.currentGold);
    }

    /// <summary>
    /// LiveMode 的 CD 遮罩每帧同步（LateUpdate 调）。真源 = <c>BattleManager.LiveDrawCdRemaining</c>；
    /// 本地 <c>_cdActive</c> 只做<b>沿检测</b> —— 起止各触发一次 RefreshLive（按钮含 CD 的置灰判定重算），
    /// 进行中只写遮罩 fillAmount 与秒数，不整表重刷。
    /// </summary>
    void TickLiveCooldown()
    {
        var bm = BattleManager.Instance;
        float cd = bm != null ? bm.LiveDrawCdRemaining : 0f;
        bool active = cd > 0f;
        if (active != _cdActive)
        {
            _cdActive = active;
            RefreshLive();   // CD 起止沿：按钮置灰 / 恢复
        }
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.CdMask == null) continue;
            if (s.CdMask.gameObject.activeSelf != active)
                s.CdMask.gameObject.SetActive(active);
            if (!active) continue;
            s.CdMask.fillAmount = Mathf.Clamp01(cd / BattleManager.LIVE_DRAW_CD_SEC);
            if (s.CdText != null) s.CdText.text = Mathf.CeilToInt(cd).ToString();
        }
    }

    // 抽奖引导气泡「关卡闸门」：已通关主线小关数 < 该值才允许弹两条引导（第 4 关起全关）。
    // 口径真源唯一 = SaveSystem.Instance?.Data.clearedStages（只记主线 Normal 小关），用 Count 判定；
    // 不用 ChapterManager.currentChapter / CurrentSubIndex0 / 难度集合，也不自算累计。
    const int DrawHintEarlyStageLimit = 3;

    // 系统未就绪静默不弹（不写 LogError：TickLiveDrawHint 每帧会调，避免刷屏）。
    bool IsDrawHintStageAllowed()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.clearedStages == null) return false;
        return data.clearedStages.Count < DrawHintEarlyStageLimit;
    }

    /// <summary>
    /// 🔴【2026-10-31 主人拍板「关卡战斗中如果玩家抽奖币够了 玩家没抽的时候要加上引导」】
    /// LiveMode（正式关 / 金币本）战斗内：<b>开打后</b>连续 <see cref="LiveDrawHintDelaySec"/> 秒
    /// 「抽奖按钮可点但本场一次都没抽」→ 用主人给的「像素风空白对话框指针」指随机抽奖按钮
    /// （底框一次性替换，Hide 后自动还原默认引导底框）。
    /// <para>触发口径（全部满足才弹，宁缺勿滥）：<br/>
    /// ① LiveMode 且 <c>UnitsCanAct</c> = true（真的开打了；进关「开始战斗」阶段不弹）；<br/>
    /// ② 不在实时抽 CD 中；<br/>
    /// ③ 至少一个抽奖按钮 <c>interactable</c>（币够 / 有免费额度 / 竞技场还有次数，真源同 Refresh）；<br/>
    /// ④ 本场没点过抽奖按钮；⑤ 本场还没弹过（一场一次）。</para>
    /// <para>消失：引导自己 5 秒后自动收（<see cref="TutorialHintUI.Show"/> 的 autoHide），
    /// 玩家点抽奖按钮立刻收（见 <c>OnDraftClicked</c>），面板收起时一并收（见 <c>Finish</c>）。</para>
    /// </summary>
    void TickLiveDrawHint()
    {
        if (!IsDrawHintStageAllowed())
        {
            _liveDrawHintTimer = 0f;
            return;
        }
        if (_liveDrawHintShown || _liveDrawnThisStage)
        {
            _liveDrawHintTimer = 0f;
            return;
        }

        var bm = BattleManager.InstanceQuiet;
        // 首波没刷 / 剧情 / 结算链期间 UnitsCanAct 恒 false —— 天然把进关阶段排除在外
        if (bm == null || !bm.UnitsCanAct || _cdActive)
        {
            _liveDrawHintTimer = 0f;
            return;
        }

        // 可抽与否的唯一真源 = Refresh 刷出来的 Btn.interactable（币不够 / 额度用完都置灰）
        bool anyClickable = false;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Btn != null && _slots[i].Btn.interactable) { anyClickable = true; break; }
        }
        if (!anyClickable)
        {
            _liveDrawHintTimer = 0f;
            return;
        }

        // 设置弹窗开着不计时（2026-11-01 主人：点设置弹出的时候不要出现抽奖提示），关掉后接着数
        if (SettingsPopupUI.IsBlockingSoftGuide) return;

        _liveDrawHintTimer += Time.unscaledDeltaTime;
        if (_liveDrawHintTimer < LiveDrawHintDelaySec) return;

        _liveDrawHintTimer = 0f;
        _liveDrawHintShown = true;   // 先置位：取不到按钮也不再反复尝试
        var drawRt = NormalButtonRect;
        if (drawRt == null) return;

        var hint = TutorialHintUI.Ensure();
        hint.SetBannerSpriteOnce(BattleManager.DraftHintBubble);   // 一次性：Hide 后还原默认底框
        // 竞技场不吃金币（四类按钮共用 5 次额度），文案不能写「抽奖币够了」，否则玩家会去找币
        hint.Show(DrawHintText(bm.IsArena), drawRt, 5f);
        hint.ApplyBattleTalkTextStyle();   // 框用主人给的，字与「战斗佣兵说话」同款
    }

    /// <summary>「币够了没抽」引导气泡文案唯一出口（前台小姐傲娇口吻，实用信息不丢）。</summary>
    internal static string DrawHintText(bool isArena)
        => isArena
            ? "还有次数，先抽一次嘛！"
            : "币够啦，先抽一次嘛！";

    /// <summary>
    /// 【2026-11-06】正式关/金币本刚进关、玩家还没点「开始战斗」时，在「随机抽奖」按钮上
    /// 立即弹一次抽奖引导气泡（口径 A = 进关就弹）。底框/文案与 TickLiveDrawHint 同源；
    /// 只有 Show 真显示成功才置 _liveDrawHintShown，压住开打后那条不重复弹。
    /// </summary>
    void ShowLiveEntryDrawHint()
    {
        if (!IsDrawHintStageAllowed()) return;
        // 可抽与否的唯一真源 = Refresh 刷出来的 Btn.interactable（币够/免费额度/竞技场次数）
        bool anyClickable = false;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Btn != null && _slots[i].Btn.interactable) { anyClickable = true; break; }
        }
        if (!anyClickable) return;
        if (SettingsPopupUI.IsBlockingSoftGuide) return;
        var drawRt = NormalButtonRect;
        if (drawRt == null) return;

        var hint = TutorialHintUI.Ensure();
        hint.SetBannerSpriteOnce(BattleManager.DraftHintBubble);
        var bm = BattleManager.InstanceQuiet;
        hint.Show(DrawHintText(bm != null && bm.IsArena), drawRt, 5f);
        hint.ApplyBattleTalkTextStyle();   // 框用主人给的，字与「战斗佣兵说话」同款
        // Show 内部被 BattleHeadTalkUI.IsShowing 闸门静默吞掉时不置位，开打后还能补弹
        if (hint.IsVisible) _liveDrawHintShown = true;
    }

    static BattleEntryDraftPanel Ensure()
    {
        if (Instance != null) return Instance;
        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] BattleUI 不存在，抽奖面板无法挂载");
            return null;
        }
        var panel = FindDeep(ui.transform, NodePanel);
        if (panel == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到节点 BackpackPanel");
            return null;
        }
        var go = new GameObject("BattleEntryDraftPanel", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        // 【2026-10-20 玩家反馈修复】挂载后必须校验宿主整条父链已激活：BackpackPanel / BattleUI
        // 若还处于未激活（战斗 UI 异步初始化没走完），下面的 Begin → SetExpanded → StartCoroutine
        // 会直接抛 "game object is inactive" → Show 同步炸 → 开战协程整条死 →
        // 玩家看到的就是「英雄站原地、抽奖按钮永不出现」（过完引导第一场正式战斗高发，
        // 因为引导局不走进关抽奖，这一关是玩家第一次踩这条链）。
        // 这里 fail closed：报 Error 后返回 null，Show 按既有口径直接开战，绝不冻场。
        if (!go.activeInHierarchy)
        {
            Debug.LogError("[BattleEntryDraftPanel] 挂载失败：BackpackPanel 所在父链未激活（BattleUI 未就绪），本次跳过抽奖直接开战");
            UnityEngine.Object.Destroy(go);
            return null;
        }
        var c = go.AddComponent<BattleEntryDraftPanel>();
        c._panel = panel as RectTransform;
        // 必须在任何一次 ApplyState 之前：此时 UI 适配已跑完、面板还没被本类动过，
        // 打出来的对照值才是「适配给的基准」。
        c._mapRt = c.ResolveMap();
        c.LogMapBaseOnce();
        // 【2026-11-09 主人拍板】map 不再二次调整：预制体已直接摆成战斗中第一帧的常驻位置
        //（中心Y 350 / scale 1.4），Build 里一次性落位后全程不动。
        // 原「组件一出生就把 map 平滑滑回收起态 250/1.13」整套停用 —— 主人实测那段滑动
        // 根本没效果，反而让 map 在玩家眼前先跳一下再滑。SettleMapToCollapsed 方法保留便于回退。
        return c;
    }

    /// <summary>
    /// 【2026-10-06 主人拍板 · 方案 A】战斗 UI 就绪后<b>立刻</b>把本组件建出来，不再等 <c>Show()</c>。
    /// <para>为什么非要这一步：<c>Ensure()</c> 原本只在 <c>Show()</c>（抽奖弹面板）里调，
    /// 于是「战斗一开始还没抽奖」的那段时间内 <b>本类实例根本不存在</b> →
    /// <c>LateUpdate</c> 不跑 → 没有任何人把 map 从适配给的展开位 361 拉回收起位 250
    /// → 主人看到的「map 一上来就在上面」。</para>
    /// <para>本方法<b>只建组件、不显示任何东西</b>（显隐仍归 <c>Begin/Finish</c>），
    /// 创建出口仍是 <c>Ensure()</c> 一个，这里只是提前调它。</para>
    /// </summary>
    public static void Prewarm()
    {
        if (BattleUI.Instance == null)
        {
            // 【2026-10-19 防卡死】原来这里静默 return：面板没人拍回收起态，会停在预制体展开态
            // （4 个黑金按钮），剧情一播就被盖住、点击全被吃 → 全场定格（第 2 章事故链一环）。
            // 改成：告警让问题看得见 + 挂一个重试器，BattleUI 就绪后补拍收起态。
            Debug.LogWarning("[BattleEntryDraftPanel] Prewarm 时 BattleUI 未就绪，0.5 秒后重试");
            var host = new GameObject("BattleEntryDraftPanel.PrewarmRetry");
            host.AddComponent<PrewarmRetry>();
            return;
        }
        // 【2026-10-09 主人拍板】这里必须补一次 Build()：Build 只在 Begin()（抽奖开始）里跑，
        // 于是「还没抽奖的那段时间」面板一直停在预制体存的展开态（750 / zhuangshi +80），
        // 且 BtnBackpack 的 onClick 还没绑 —— 就是主人报的「一上来背包是打开的」「点了关不掉」。
        // Build() 末尾的 SetDraftVisible(false) + ApplyPanelState(0f) 正好把面板拍回收起态，
        // 只建不显示，与本方法「只建组件、不显示任何东西」的语义一致；它有 _built 守卫，重复调无害。
        var pre = Ensure();
        if (pre != null) pre.Build();
    }

    /// <summary>Prewarm 的重试器：每 0.5 秒看一眼 BattleUI 就绪没有，最多重试 20 次（10 秒），成败都自毁。</summary>
    class PrewarmRetry : MonoBehaviour
    {
        float _timer;
        int _tries;

        void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.5f) return;
            _timer = 0f;
            if (BattleUI.Instance != null)
            {
                Prewarm();
                Destroy(gameObject);
                return;
            }
            if (++_tries >= 20)
            {
                Debug.LogError("[BattleEntryDraftPanel] 重试 10 秒后 BattleUI 仍未就绪，放弃 Prewarm" +
                               "（抽奖面板将停留展开态，可能盖住剧情，请截图上报）");
                Destroy(gameObject);
            }
        }
    }

    RectTransform ResolveMap()
    {
        var root = _panel != null ? _panel.parent : null;
        if (root == null) return null;
        var map = root.Find(NodeMap) as RectTransform;
        if (map == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到场景节点 " + NodeMap + "，展开时场景不动");
        return map;
    }

    /// <summary>
    /// map 的两态：<b>中心 Y = 250 → 350</b>（主人给的绝对值），缩放两态恒 <b>1.4</b>（2026-10-27 主人拍板）。
    ///
    /// <para>读位置恒用 <c>anchoredPosition.y</c> —— 点锚点与拉伸锚点下它都是「中心相对父级中心的偏移」，
    /// 是同一把尺子（见 <c>MapCollapsedPosY</c> 处注释）。</para>
    ///
    /// <para>写位置分两种锚点：<br/>
    /// · 竖向拉伸（<c>anchorMin.y != anchorMax.y</c>）：按差值推 <c>offsetMin/offsetMax</c> 整体平移，
    ///   绝不写 <c>anchoredPosition.y</c>（它是派生的，写了会被反算覆盖）；<br/>
    /// · 点锚点：直接写 <c>anchoredPosition.y</c>。</para>
    ///
    /// <para>每帧都按「目标 - 当前」算差值，所以适配/别处什么时候改了 map，下一帧都会被拉回主人给的两态，
    /// 不需要缓存基准，也不会有累积漂移。</para>
    /// </summary>
    void ApplyMapState(float k)
    {
        WriteMapTransform(
            Mathf.Lerp(MapCollapsedPosY, MapExpandedPosY, k),
            Mathf.Lerp(MapCollapsedScale, MapExpandedScale, k));
    }

    /// <summary>
    /// 写 map 的<b>唯一出口</b>：给一对绝对值（中心 Y + 缩放），由它按<b>当时的锚点</b>落笔。
    /// <para>· 点锚点：直接写 <c>anchoredPosition.y</c>；</para>
    /// <para>· 竖向拉伸锚点（<c>UiLayoutStretch.ApplyBattleMapWidth</c> 会把它改成 (0,0)~(1,1)）：
    ///   <c>anchoredPosition.y</c> 是派生的，写了会被反算覆盖 —— 必须按差值推
    ///   <c>offsetMin/offsetMax</c> 整体平移。</para>
    /// 每帧都按「目标 − 当前」算差值，所以别处什么时候改了 map，下一帧都会被拉回本类给的值，
    /// 不需要缓存基准，也不会有累积漂移。
    /// </summary>
    void WriteMapTransform(float targetY, float scale)
    {
        if (_mapRt == null) return;

        float d = targetY - _mapRt.anchoredPosition.y;
        if (Mathf.Abs(d) > 0.01f)
        {
            if (!Mathf.Approximately(_mapRt.anchorMin.y, _mapRt.anchorMax.y))
            {
                _mapRt.offsetMin = new Vector2(_mapRt.offsetMin.x, _mapRt.offsetMin.y + d);
                _mapRt.offsetMax = new Vector2(_mapRt.offsetMax.x, _mapRt.offsetMax.y + d);
            }
            else
            {
                _mapRt.anchoredPosition = new Vector2(_mapRt.anchoredPosition.x, targetY);
            }
        }
        _mapRt.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 【2026-11-09 已停用，方法保留便于回退】原【2026-10-06 主人拍板】map 落到收起态时<b>不许瞬移</b> —— 主人原话「往上移动或往下移动加个效果，不要突然出来」。
    /// <para>为什么需要它：竖屏适配把 map 摆在 <b>361 / scale 1.0</b>（展开位），
    /// 而主人定的收起态是 <b>250 / scale 1.13</b>。直接写目标值就会「啪」地跳一下。
    /// 这里从<b>当前实际位置</b>起算，用与展开/收起同一段时长（<c>AnimSec</c>）平滑滑回收起位。</para>
    /// <para>已经在收起位（差值小于半个像素）就不需要动，直接落位，不起协程。</para>
    /// </summary>
    void SettleMapToCollapsed()
    {
        if (_mapRt == null) return;

        bool atRest = Mathf.Abs(_mapRt.anchoredPosition.y - MapCollapsedPosY) < 0.5f
                      && Mathf.Abs(_mapRt.localScale.x - MapCollapsedScale) < 0.001f;
        if (atRest)
        {
            ApplyMapState(0f);
            return;
        }
        if (_settleCo != null) StopCoroutine(_settleCo);
        _settleCo = StartCoroutine(CoSettleMap());
    }

    IEnumerator CoSettleMap()
    {
        float fromY = _mapRt.anchoredPosition.y;
        float fromS = _mapRt.localScale.x;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / AnimSec;
            float e = Mathf.Clamp01(t);
            WriteMapTransform(Mathf.Lerp(fromY, MapCollapsedPosY, e),
                              Mathf.Lerp(fromS, MapCollapsedScale, e));
            yield return null;
        }
        _settleCo = null;
        ApplyMapState(0f);
    }

    /// <summary>
    /// 【2026-11-09 已停用，方法保留便于回退】原【2026-11-06 主人报「进战斗背景卡一下」】map/世界层两态<b>短过渡</b>入口：
    /// LiveMode 的 BeginLive 展开时，面板 UI 一步落位（2026-10-31 拍板不变），
    /// 但 map 与世界层从当前 <c>_animK</c> 平滑滑到目标态（时长 <c>AnimSec</c>、ease-out）——
    /// 背景瞬跳就是主人看到的「卡一下」。收起（Finish → SetExpanded(false)）本就走
    /// <c>CoAnimState</c> 动画，不经这里。
    /// <para>占用 <c>_animCo</c> 槽位：LateUpdate 静止写回（<c>ApplyMapState(1f)</c>）
    /// 在 _animCo 运行期间自动让路，同一时刻只有一处写 map；跑完置 null 归还槽位
    /// （与 <c>CoSettleMap</c> 同款收尾）。</para>
    /// </summary>
    void StartMapGlide(float to)
    {
        if (_animCo != null) { StopCoroutine(_animCo); _animCo = null; }
        if (_settleCo != null) { StopCoroutine(_settleCo); _settleCo = null; }
        // 宿主未激活 / 没 map：与 SetExpanded 同一口径，跳过动画直接落位，绝不起协程炸调用方
        if (!gameObject.activeInHierarchy || _mapRt == null)
        {
            _animK = to;
            ApplyMapState(to);
            ApplyWorldLift(to);
            return;
        }
        _animCo = StartCoroutine(CoGlideMap(to));
    }

    IEnumerator CoGlideMap(float to)
    {
        float from = _animK;
        if (Mathf.Approximately(from, to))
        {
            _animK = to;
            ApplyMapState(to);
            ApplyWorldLift(to);
            _animCo = null;
            yield break;
        }
        float t = 0f;
        while (t < AnimSec)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / AnimSec);
            _animK = Mathf.Lerp(from, to, EaseOut(p));
            ApplyMapState(_animK);
            ApplyWorldLift(_animK);
            yield return null;
        }
        _animK = to;
        ApplyMapState(to);
        ApplyWorldLift(to);
        _animCo = null;
    }

    /// <summary>
    /// 第一次碰 map 之前打一行<b>对照日志</b>：主人拍板的默认态是 <b>中心Y 250 / scale 1.13</b>。
    /// 这里报的是「适配刚跑完、本类还没动过」时的真实值，用来对账：
    /// 若 <c>PosY</c> 明显大于 250，就说明适配是拿展开态（面板 750）算的 —— 正是主人报
    /// 「怎么一开始就上去了」的那次；现在代码一律按 250/350 绝对值写，不再受它影响。
    /// <remarks>只报不改，<b>代码绝不私自补偿</b>。</remarks>
    /// </summary>
    void LogMapBaseOnce()
    {
        if (_mapBaseLogged || _mapRt == null) return;
        _mapBaseLogged = true;
        bool stretch = !Mathf.Approximately(_mapRt.anchorMin.y, _mapRt.anchorMax.y);
        Debug.Log($"[BattleEntryDraftPanel] map 对照：主人定 收起 中心Y={MapCollapsedPosY:F0}/scale {MapCollapsedScale}" +
                  $" 展开 中心Y={MapExpandedPosY:F0}/scale {MapExpandedScale}；" +
                  $"适配后实测 anchorMin.y={_mapRt.anchorMin.y:F2} anchorMax.y={_mapRt.anchorMax.y:F2}" +
                  $"{(stretch ? "（拉伸锚点）" : "（点锚点）")}" +
                  $" 中心Y(PosY)={_mapRt.anchoredPosition.y:F0}" +
                  $" 底={_mapRt.offsetMin.y:F0} 顶={-_mapRt.offsetMax.y:F0}" +
                  $" scale={_mapRt.localScale.x:F3}");
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】map 的纵向位置与缩放<b>由本类独占</b>：静止时按当前进度 <c>_animK</c> 写回去。
    ///
    /// <para>为什么非要这一步：竖屏适配 <c>BattleViewportFit.Apply</c>（实际是
    /// <c>UiLayoutStretch.ApplyBattleMapWidth</c>）会把 map 重锚成「顶栏底 ~ 背包顶」的拉伸锚点，
    /// <b>它只认跑的那一刻面板多高</b>；那一刻面板常常还停在展开态 750（预制体就是这么摆的），
    /// 于是算出来的 map 落点本身就是展开位 —— 主人报「map 一上来就在展开位置、跑到上面了」的根因。
    /// 而这套适配<b>随时可能再跑一次</b>（<c>BattleUI.Start</c> 与
    /// <c>AutoGameInitializer</c> 都在调），跑完 map 就又飘去展开位，
    /// 要等下一次 <c>SetExpanded</c> 才被拉回来。</para>
    ///
    /// <para>所以静止态每帧按当前 k 写一次 —— 与本类同一个出口（<c>ApplyMapState</c>）、
    /// 同一组真值（250/1.13 ↔ 350/1），不另立一套；动画正在跑的那一帧交给动画协程，
    /// 绝不两个地方同时写。</para>
    /// </summary>
    void LateUpdate()
    {
        // 倒计时每帧走：与本类两态动画、入场归位互不干涉，各自独立。
        TickContinueCountdown();
        // 【2026-11-09 主人拍板】「继续」按钮 1.45→1.55 呼吸缩放，4 秒一个来回、循环。
        TickContinueBreath();

        // 【2026-10-29 实时抽 CD】LiveMode 下每帧同步 CD 遮罩与秒数（含起止沿的按钮刷新）。
        if (_liveMode)
        {
            TickLiveCooldown();
            // 【2026-10-31】战斗内「抽奖币够了但没抽」引导（一场一次，开打后才计时）
            TickLiveDrawHint();
        }

        // 【2026-10-09 主人拍板】开打那一刻自动收起背包：只认 UnitsCanAct 的 false→true 跳变，
        // 且必须是玩家手动点开的（抽屉开着、不在抽奖阶段 _draftActive）。
        // ⚠ 绝不写成「战斗中每帧强制收起」——那会把主人 10-08 拍板保留的「战斗中点开背包查看」废掉。
        // 用 InstanceQuiet 静默取实例，系统未就绪时不刷 Error。
        // ⚠ 判据从 _expanded 换成 BattleUI.Instance.BagDrawerOpen：背包按钮现在开的是网格背包抽屉，
        // 不再是 600↔750 两态（_expanded 只反映抽奖展开）。
        var bm = BattleManager.InstanceQuiet;
        bool canAct = bm != null && bm.UnitsCanAct;
        if (_built && canAct && !_prevUnitsCanAct && !_draftActive
            && BattleUI.Instance != null && BattleUI.Instance.BagDrawerOpen)
            BattleUI.Instance?.CloseBagDrawer();
        // 每帧无条件更新，不放在上面的早退之后
        _prevUnitsCanAct = canAct;

        // 【2026-11-09】只写恒定终态：竖屏适配随时会重锚 map，这里按同一出口把它拉回
        // 350/1.4；不再按 _animK 插值 —— 两态动画只动面板 UI，map 全程不动。
        if (_mapRt == null) return;
        ApplyMapState(1f);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 内部
    // ============================================================

    void Begin(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
               Action<DraftCategory?> onPick, Action onFight, int freeLeft = 0)
    {
        // 【2026-10-29 实时抽改版】旧面板与 LiveMode 互斥：裂隙/引导弹旧面板时清掉常显态，
        // Begin 内部的「先收背包再展开」逻辑按收起态起点正常走（有收→展动画，旧面板既有体验）。
        if (_liveMode) { _liveMode = false; _cdActive = false; }
        Build();
        // 【2026-10-08 主人拍板】玩家手动点开的「看背包」态：抽奖一开始先收回去，
        // 再由下面 SetExpanded(true) 从收起态平滑展开显示抽奖 —— 不让背包和抽奖叠在一起。
        // ⚠ 判据必须放在 _draftActive = true 之前：否则连抽中途回面板也会被当成「玩家开的」。
        if (_expanded && !_draftActive)
        {
            SetExpanded(false);
            ApplyState(0f);   // 立刻归位到收起态（不等动画走完），下面展开才有「收 → 展」的观感
        }
        // 2026-11-09 口径调整：这里<b>不再起 60 秒倒计时</b>。原「面板一露头就开始走、
        // 归零当作点了继续」整套停用 —— 面板只由玩家点「继续/开始战斗」或正常流程收起，
        // 挂机多久都不会自己收。倒计时数字节点随之不再创建（唯一创建点是 StartContinueCountdown）。
        // 历史口径（60s 固定预算、抽完只解冻不补满）已随起表点一起作废，这里只留结论，免后人改回去。
        _drawLocked = false;
        // 2026-11-09 口径调整：所有模式都不再起 60 秒倒计时，面板只由玩家点继续/开始战斗或正常流程收起。
        _onPick = onPick;
        _onFight = onFight;
        _draftActive = true;
        _comboCount = 0;   // 【2026-10-13 #9】每次开面板重置连抽计数
        // 2026-11-09 口径调整：停用 5 秒无操作软引导（去掉抽奖按钮上的手指）。

        SetDraftVisible(true);
        SetExpanded(true);
        Refresh(randomPrice, focusPrice, cats, coins, freeLeft);
        RefreshCombo();    // 【2026-10-13 #9】重置后同步连抽提示（隐藏）
        LogState("Begin");
    }

    void Finish()
    {
        // 面板收了，倒计时也得停 —— 不然协程还挂在已经隐藏的面板上，归零会再触发一次「继续」
        StopContinueCountdown();
        StopIdleGuide();   // 5 秒无操作引导一并收掉（面板都没了不该留手指）
        // 【2026-10-31】战斗内「币够了没抽」引导：面板收起（开战/通关/撤离）时一并收掉，
        // 不让它跟着飘进结算或城镇（它是 DDOL 常驻组件，只能在这里收）。
        if (_liveDrawHintShown)
        {
            _liveDrawHintShown = false;
            TutorialHintUI.Instance?.Hide();
        }
        _onPick = null;
        _onFight = null;
        _draftActive = false;
        // 【2026-10-29 实时抽改版】常显态一并收：通关链/剧情入口的 Hide() 会走到这里，
        // 抽奖按钮与「开始战斗」整组消失；CD 状态同步清。
        _liveMode = false;
        _cdActive = false;
        SetDraftVisible(false);
        SetExpanded(false);
        // 面板收了，概率弹窗也得跟着收 —— 不然战斗画面里会留一个孤儿弹窗
        DraftOddsPopupUI.Hide();
    }

    // ────────────────────────────────────────────────────────────────────
    // 5 秒无操作软引导（2026-10-18 主人拍板）：正式战斗中 5 秒没行动 →
    // 手指指抽奖按钮（没抽过）；抽完后又间隔 5 秒无操作 → 指「继续」；软引导不挡点击。
    // ────────────────────────────────────────────────────────────────────

    /// <summary>启动/重置 5 秒计时；引导局直接不启（TutorialDirector 已有脚本化硬引导，别叠第二套）。</summary>
    void RestartIdleGuide()
    {
        StopIdleGuide();
        _hasDrawnThisPanel = false;
        var bm = BattleManager.Instance;
        if (bm != null && bm.IsTutorialRun) return;
    }

    /// <summary>停计时并收掉引导手指（幂等）。</summary>
    void StopIdleGuide()
    {
        if (_idleGuideCo != null)
        {
            StopCoroutine(_idleGuideCo);
            _idleGuideCo = null;
        }
        if (_idleGuideShown)
        {
            TutorialHintUI.Instance?.Hide();
            _idleGuideShown = false;
        }
    }

    /// <summary>5 秒无操作 → 按玩家进度亮软引导：没抽过指抽奖按钮，抽过指「继续」。</summary>
    IEnumerator CoIdleGuide()
    {
        float t = 0f;
        while (t < IdleGuideSec && _draftActive)
        {
            // 设置弹窗开着不计时（2026-11-01 主人：点设置弹出的时候不要出现抽奖提示），
            // 关掉后从暂停处继续数，不重置。
            if (!SettingsPopupUI.IsBlockingSoftGuide)
                t += Time.unscaledDeltaTime;
            yield return null;
        }
        _idleGuideCo = null;
        if (!_draftActive) yield break;
        ShowIdleGuideNow();
    }

    void ShowIdleGuideNow()
    {
        if (_hasDrawnThisPanel)
        {
            // 【2026-10-18 二次拍板】抽完后又间隔 5 秒无操作 → 指「继续」
            var contRt = ContinueButtonRect;
            if (contRt == null) return;
            TutorialHintUI.Ensure().Show("抽好了，点这里开始战斗", contRt, -1f);
        }
        else
        {
            // 【2026-10-20 主人拍板】抽奖币不够（所有抽奖按钮都置灰）时，长时间没动
            // 不再弹「点这里抽一次奖」的指引——指着一个点不了的按钮只会误导玩家。
            // 可点与否的唯一真源 = Refresh 刷出来的 Btn.interactable。
            bool anyClickable = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Btn != null && _slots[i].Btn.interactable) { anyClickable = true; break; }
            }
            if (!anyClickable) return;

            // 没抽过 → 指第一个抽奖按钮（普通/随机）
            var drawRt = _slots.Count > 0 && _slots[0].Root != null ? _slots[0].Root : null;
            if (drawRt == null) return;
            TutorialHintUI.Ensure().Show("点这里抽一次奖", drawRt, -1f);
        }
        _idleGuideShown = true;
    }

    // ── 「继续」按钮呼吸缩放（2026-11-09 主人拍板）──────────────────────────
    // 预制体 BtnContinue 默认 m_LocalScale = 1.45（BattleUI.prefab 实测），放大到 1.55，
    // 一个完整来回 4 秒、循环；按钮隐藏时归位，下次亮起从 1.45 重新起步。
    // 只写 localScale，不动锚点 / 位置 / 尺寸，也不改预制体；与 ApplyPanelState 两态位移互不干涉。
    const float ContinueBaseScale = 1.45f;
    const float ContinueMaxScale = 1.55f;
    const float ContinueBreathSec = 4f;
    float _continueBreathT;

    /// <summary>每帧驱动 BtnContinue 的等比缩放（只在按钮可见时推进计时）。</summary>
    void TickContinueBreath()
    {
        var rt = ContinueButtonRect;
        if (rt == null) return;
        if (!rt.gameObject.activeInHierarchy)
        {
            _continueBreathT = 0f;
            rt.localScale = Vector3.one * ContinueBaseScale;
            return;
        }
        _continueBreathT += Time.unscaledDeltaTime;
        float phase = (_continueBreathT % ContinueBreathSec) / ContinueBreathSec;
        float k = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * phase);   // 0→1→0，4 秒一个来回
        rt.localScale = Vector3.one * Mathf.Lerp(ContinueBaseScale, ContinueMaxScale, k);
    }

}
