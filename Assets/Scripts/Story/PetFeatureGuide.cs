using System.Collections;
using UnityEngine;

/// <summary>
/// 宠物功能新手引导（2026-11-22；2026-11-23 主人拍板「胜利界面先报喜、回城只指路」）：
/// 整数制关 3（精英）正式主线首通 → 胜利结果界面奖励展示完弹「宠物功能已开启」toast（ShowUnlockToastOnVictory）→
/// 回城 → 底栏气泡指角色页 → 玩家自己进角色页后气泡指宠物入口 → 上阵 → 喂养（随机属性 +X + 亲密度）→ 宠物用战斗对话框叫两声。
/// 【2026-11-23 主人拍板】不替玩家自动切页（旧版 OpenCharacter 的「下滑转场」观感差），一律酒馆气泡模式：
/// 贴目标 + 尖角 + 无手指滑动 + 点击即收，见 ShowBubble()。
///
/// 真源与边界：
///  · 宠物功能「已解锁」真源 = SaveData.HasIntruderStorySeen(PetRescueStoryBeats.SeenKey)，
///    本类只读不写；未解锁时宠物入口自己拦截（CharacterUI.PetEntry），本引导不启动。
///  · 完成标记 = 新建 key petFeatureGuide_1_3，走现成 MarkIntruderStorySeen 出口，重进不重播。
///  · 全程软提示（TutorialHintUI.Show，可点掉 / 自动消失），不强制遮罩、不锁操作；
///    每一步都有看门狗超时，玩家不按提示走也绝不卡死。
///  · toast 走 GlobalToastUI.ShowPersona 单一出口（铁律25），文案自带口吻字不重复叠尾巴。
///  · 气泡叫两声走 BattleHeadTalkUI.PlayLineAt 固定 UI 锚点，与战斗气泡共用协程互斥。
/// </summary>
public static class PetFeatureGuide
{
    const string DoneKey = "petFeatureGuide_1_3";
    internal const float StepTimeoutSeconds = 120f;   // 单步等待玩家操作的看门狗（秒），超时放行不卡死
    internal const float HintSeconds = 9f;            // 软提示自动消失时长
    /// <summary>【2026-11-25 主人拍板】第一章第三关首通：结算界面下方指路气泡文案。</summary>
    public const string SettlementGuideText = "回角色界面开启宠物上阵";
    /// <summary>结算指路气泡层级：压过战斗结果(985)，低于升级窗(996)/Toast(11000)。</summary>
    const int SettlementGuideSort = GameConfig.UiSort.BattleResult + 1;

    /// <summary>本引导普通气泡 250×120（文案已精简到两行内）：底栏角色 / 上阵 / 喂养三条沿用原样。
    /// 【2026-11-24 主人「把箭头旋转90对着图标，在图标左边或右边生成引导框」】
    /// 宠物入口贴屏幕左下角（逻辑 X≈-260、距底仅约 72px），横放气泡尖角被出屏夹取推到人物腿上；
    /// 该条改用 SideGuideBubbleLayout：整框旋转 ±90° 后非等比拉伸成横向宽扁（屏幕上约 270 宽 × 130 高），
    /// 尖角转到侧边正对图标、气泡落在图标右侧，整框留在屏内。其余引导/全局口径、字号色值一律不动。
    /// internal：使用点在另一个类 PetFeatureGuideDriver.ShowBubble（同文件同程序集）。</summary>
    internal static readonly TutorialHintUI.HintLayout GuideBubbleLayout = new TutorialHintUI.HintLayout
    {
        BubbleWidth = 250f,
        BubbleHeight = 120f,
    };

    /// <summary>【2026-11-25 主人拍板「框可出屏、字不要出屏」】第三拍指角色页宠物入口专用：
    /// 入口贴左屏边，250 宽普通气泡被夹取推偏（主人报「位置偏了」）→ 开 BubbleAllowOffscreenKeepTextIn，
    /// 气泡本体出屏、尖角正对图标、文字由 textShiftX 补偿留屏内；高度 120→138 顺带防两行字顶框。
    /// 只此一条使用；底栏角色 / 上阵 / 喂养三拍仍用 GuideBubbleLayout，观感零变化。</summary>
    internal static readonly TutorialHintUI.HintLayout PetEntryBubbleLayout = new TutorialHintUI.HintLayout
    {
        BubbleWidth = 250f,
        BubbleHeight = 138f,
        BubbleAllowOffscreenKeepTextIn = true,
    };

    /// <summary>贴屏幕边的目标专用：整框旋转 90° 后非等比拉伸为屏幕 270 宽 × 130 高的横向气泡，
    /// 尖角朝左右正对图标（按目标在屏幕左右自动选边）。宽高是「旋转前」的局部尺寸（高窄、宽拉长）。</summary>
    internal static readonly TutorialHintUI.HintLayout SideGuideBubbleLayout = new TutorialHintUI.HintLayout
    {
        BubbleWidth = 130f,
        BubbleHeight = 270f,
        SideTipToTarget = true,
    };

    static bool _pending;                    // 跨场景 pending：关 3 通关置位，回城消费
    static PetFeatureGuideDriver _driver;

    /// <summary>整数制关 3 正式主线通关时调（Settlement 已做好章节/模式判定）。</summary>
    public static void MarkDogRescueCleared()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.HasIntruderStorySeen(DoneKey)) return;
        _pending = true;
        Debug.Log("[PetFeatureGuide] 狗营救关首通，pending 宠物功能引导");
    }

    /// <summary>
    /// 胜利结果界面「奖励展示完」时调（2026-11-23 主人拍板「胜利界面加上功能开启的 toast」）：
    /// 只弹一条前台小姐告知宠物功能已开启，不清 pending —— 回城引导（TryConsumeOnTown）照旧。
    /// </summary>
    public static void ShowUnlockToastOnVictory()
    {
        if (!_pending || IsDone) return;
        GlobalToastUI.ShowPersona("宠物功能开啦～回城找我，我可不等你哦～");
    }

    /// <summary>
    /// 【2026-11-25 主人拍板】结算界面下方加一条引导框写「回角色界面开启宠物上阵」：
    /// 只在第一关第三关首通、引导尚未完成时弹（与报喜 toast 同一 pending 判据）。
    /// 复用 TutorialHintUI + 现成气泡底框（BattleManager.DraftHintBubble），零新增 UI / 图片 / 预制体；
    /// 贴确定按钮下方、尖角朝上，点击即收；界面销毁时由 DismissSettlementGuide 收起，避免残留到城镇。
    /// </summary>
    public static void TryShowSettlementGuide(RectTransform anchor)
    {
        if (anchor == null || !_pending || IsDone) return;
        var hint = TutorialHintUI.Ensure();
        if (hint == null) return;
        // 【2026-11-25 主人拍板】换默认深蓝「引导底框」：撤掉米白气泡 override，走普通引导样式（深蓝横幅+浅黄字）。
        // 该底框无尖角，故不再 SetBubblePointUpOnce；位置仍贴确定按钮下方。
        hint.SetSortOrderOnce(SettlementGuideSort);
        hint.SetTapToHideOnce(true);
        // 【2026-11-25 主人拍板】结算界面出完即弹：头顶台词残留不再吞框，
        // 走 Show 的单次放行参数，台词照播不打断、其它引导守卫不变。
        hint.Show(SettlementGuideText, anchor, -1f, false, ignoreHeadTalkGuard: true);   // 常驻到结算界面关闭（OnDestroy 收），不自动消失
        if (!hint.IsVisible || hint.CurrentHint != SettlementGuideText)
        {
            // 被战斗头顶台词占用没真弹出：退掉一次性设置，防污染下一条引导（不静默兜底）
            hint.SetSortOrderOnce(0);
            hint.SetTapToHideOnce(false);
            Debug.LogError("[PetFeatureGuide] 结算指路气泡被占用未弹出（战斗头顶台词？）");
        }
    }

    /// <summary>结算界面销毁时收起指路气泡：只认本条文案，其它引导零影响。</summary>
    public static void DismissSettlementGuide()
    {
        var hint = TutorialHintUI.Instance;
        if (hint == null || !hint.IsVisible) return;
        if (hint.CurrentHint == SettlementGuideText) hint.Hide();
    }

    /// <summary>回城时调（TownHubController.Start）。启动全局唯一驱动器，条件不满足时它自己安静。</summary>
    public static void TryConsumeOnTown()
    {
        // 【2026-11-23 主人「打完中断再进也没有引导宠物上阵流程了」】_pending 是内存静态字段，
        // 关 3 通关置位后只要退出进程（含退出 Play）就丢，再回城永远不引导。
        // 缺失时从存档补回一次，判据与 MarkDogRescueCleared 的调用前提完全一致。
        if (_driver != null && _driver.IsRunning) return;   // 【2026-11-25 修】只在引导真在跑时不打断；重编译后 driver 复位为空闲须允许重启
        if (!_pending) _pending = ShouldGuideFromSave();
        if (!_pending) return;
        _pending = false;
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.HasIntruderStorySeen(DoneKey)) return;
        // 宠物功能还没解锁（营救剧情没播）→ 不引导，留待下次回城再由入口拦截提示去第 3 关
        if (!data.HasIntruderStorySeen(PetRescueStoryBeats.SeenKey)) return;

        Ensure().Begin();
    }

    /// <summary>
    /// 存档推导「关 3 首通该引导、但还没引导完」——中断后重进补 pending 的唯一判据：
    /// 宠物功能已解锁（营救剧情看过）+ 完成标记未写 + 关 3 已通关（与 Settlement 调 MarkDogRescueCleared 的前提一致）。
    /// </summary>
    static bool ShouldGuideFromSave()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (data.HasIntruderStorySeen(DoneKey)) return false;
        if (!data.HasIntruderStorySeen(PetRescueStoryBeats.SeenKey)) return false;
        return data.HasStageCleared(1, 2);
    }

    static PetFeatureGuideDriver Ensure()
    {
        if (_driver != null) return _driver;
        var go = new GameObject("PetFeatureGuideDriver");
        Object.DontDestroyOnLoad(go);
        _driver = go.AddComponent<PetFeatureGuideDriver>();
        return _driver;
    }

    static bool IsDone
    {
        get
        {
            var data = SaveSystem.Instance?.Data;
            return data != null && data.HasIntruderStorySeen(DoneKey);
        }
    }

    /// <summary>引导是否进行中（stage 1~5）。别的箭头气泡要礼让，别抢同一条横幅（2026-11-25）。</summary>
    internal static bool IsRunning => _driver != null && _driver.IsRunning;

    internal static void MarkDone()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (!data.HasIntruderStorySeen(DoneKey))
        {
            data.MarkIntruderStorySeen(DoneKey);
            SaveSystem.Instance.Save();
        }
        Debug.Log("[PetFeatureGuide] 引导完成，已写完成标记");
    }

    // —— 玩家操作通知（各 UI 动作点调用；只在引导进行中有效）——
    /// <summary>玩家自己点进角色页时由 TownHubController.SwitchTabNow 调（酒馆气泡模式：不自动切页）。</summary>
    public static void NotifyCharacterPageOpened() => _driver?.OnCharacterPageOpened();
    public static void NotifyPetUIOpened() => _driver?.OnPetUIOpened();
    public static void NotifyDeployed(string petId) => _driver?.OnDeployed(petId);
    public static void NotifyFed(string petId) => _driver?.OnFed(petId);

    /// <summary>喂养那拍的「叫两声」是否由引导接管（stage 4 待喂养 / 5 收尾中）。
    /// 供 PetUI.Actions.TryFeed 判断这次喂养要不要自己再播一声，避免同一只气泡协程打架。</summary>
    public static bool IsFeedGuideActive => _driver != null && _driver.IsAwaitingFeed;
}

/// <summary>引导状态机驱动器：DontDestroyOnLoad，协程推进，软提示 + 看门狗，任何一步可跳过。</summary>
public class PetFeatureGuideDriver : MonoBehaviour
{
    Coroutine _co;
    int _stage;   // 0 idle / 1 待进角色页 / 2 待开宠物界面 / 3 待上阵 / 4 待喂养 / 5 收尾中
    string _petId;

    /// <summary>引导是否进行中（0 = 空闲/已完成）。供 PetTalentHint 礼让判断，不写任何状态。</summary>
    public bool IsRunning => _stage > 0;

    /// <summary>是否正处于「待喂养 / 喂养后收尾」两拍（叫声气泡由这里统一播）。</summary>
    internal bool IsAwaitingFeed => _stage == 4 || _stage == 5;

    public void Begin()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoGuide());
    }

    IEnumerator CoGuide()
    {
        _stage = 1;
        // 【2026-11-25 修】回城后战斗头顶台词（DDOL）可能还在播：TutorialHintUI.Show 的 IsShowing 守卫
        // 会静默拒弹全部气泡。回城后战斗已结束，先等它收起（最多 8s），
        // 超时强制 HideNow——回城了，战斗台词不该残留。
        float talkWait = 0f;
        while (talkWait < 8f)
        {
            var talk = BattleHeadTalkUI.Instance;
            if (talk == null || !talk.IsShowing) break;
            talkWait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (talkWait >= 8f)
        {
            BattleHeadTalkUI.Instance?.HideNow();
            Debug.LogWarning("[PetFeatureGuide] 回城后战斗头顶台词 8s 未收，已强制收起以保证引导气泡能弹");
        }
        // 等预热收起，避免 toast 被 Loading 吞（照 TutorialDirector.Town 现成判据）
        float warmupWait = 0f;
        while (!TownHubController.WarmupDone && warmupWait < 8f)
        {
            warmupWait += Time.unscaledDeltaTime;
            yield return null;
        }

        // 第一拍：回城指路（功能开启的 toast 已在胜利界面说过了，这里不再重复报喜）
        GlobalToastUI.ShowPersona("宠物在角色页等你，快去看看嘛～");
        yield return new WaitForSecondsRealtime(0.8f);   // 【2026-11-25 主人「出来的有点慢」】2.2→0.8s

        // 第二拍【2026-11-23 酒馆气泡模式】：不自动切页，底栏「角色」按钮贴气泡指路，等玩家自己点进去。
        var nav = MainBottomNav.Instance;
        var navRect = nav != null && nav.characterButton != null
            ? nav.characterButton.transform as RectTransform : null;
        if (navRect != null)
            // 【2026-11-25 主人「玩家要是点击图标就消失」】这条不自动收、不点任意处收，点角色图标才收
            // （OnCharacterPageOpened / 点 UI 按钮收口）；出来快一点由下方 toast 等待缩短实现。
            ShowBubble("点底栏「角色」页找我～", navRect, false, PetFeatureGuide.GuideBubbleLayout,
                      untilTargetClick: true);
        else
            Debug.LogError("[PetFeatureGuide] 底栏角色按钮取不到，角色页指路气泡跳过（不静默兜底）");
        yield return WaitStage(2);   // 等玩家自己点进角色页（TownHubController 通知）

        // 第三拍：指宠物入口（玩家已在角色页，用普通气泡层即可）
        var charUi = CharacterUI.Instance;
        var entryRect = charUi != null ? CharacterUI.PetEntryRect : null;
        if (entryRect != null)
            // 【2026-11-25 主人「角色界面那个宠物图标也是」】同第二拍：点宠物图标才收
            ShowBubble("点「宠物」图标看看嘛～", entryRect, false, PetFeatureGuide.PetEntryBubbleLayout,
                      untilTargetClick: true);
        else
            Debug.LogError("[PetFeatureGuide] 角色页宠物入口取不到，指路气泡跳过");
        yield return WaitStage(3);   // 等玩家点开宠物界面

        // 第四拍：指上阵按钮（宠物弹窗在 TownPopup 900 层，气泡要抬层盖过去）
        ShowBubble("点「上阵」带上它～", PetUI.DeployBtnRect, true, PetFeatureGuide.GuideBubbleLayout);
        yield return WaitStage(4);   // 等玩家上阵

        // 第五拍：指喂养按钮（同一个按钮，上阵后文案变「喂养」）
        ShowBubble("喂一次可获得属性哦", PetUI.DeployBtnRect, true, PetFeatureGuide.GuideBubbleLayout);
        yield return WaitStage(5);   // 等玩家喂养

        // 第六拍：宠物用战斗对话框叫两声（固定锚点浮在宠物模型上方）
        TutorialHintUI.Ensure().Hide();
        yield return PlayTwoCries();

        PetFeatureGuide.MarkDone();
        _stage = 0;
        _co = null;
    }

    /// <summary>等到 _stage 达到目标，或看门狗超时；软提示自动消失不阻塞流程。</summary>
    IEnumerator WaitStage(int target)
    {
        float t = 0f;
        while (_stage < target && t < PetFeatureGuide.StepTimeoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (t >= PetFeatureGuide.StepTimeoutSeconds)
            Debug.LogWarning("[PetFeatureGuide] 第 " + target + " 步等待超时，跳过（不卡死引导）");
    }

    /// <summary>酒馆气泡模式统一出口：贴目标底框 + 尖角 + 点击即收 + 无手指滑动（不去下滑）；
    /// overPopup=气泡要盖到 TownPopup(900) 弹窗里控件时抬层（照 MercSwapHint.TryShow 口径）。</summary>
    static void ShowBubble(string text, RectTransform target, bool overPopup,
        TutorialHintUI.HintLayout? layout = null, bool untilTargetClick = false)
    {
        var bubble = BattleManager.DraftHintBubble;
        if (bubble == null)
        {
            Debug.LogError("[PetFeatureGuide] 气泡底框取不到（像素风空白对话框指针），提示跳过（不兜底静默）");
            return;
        }
        var hint = TutorialHintUI.Ensure();
        hint.SetBannerSpriteOnce(bubble);
        if (overPopup) hint.SetSortOrderOnce(GameConfig.UiSort.TownPopup + 40);
        // 【2026-11-25 主人「玩家要是点击图标就消失」】指路拍：不自动收、不点任意处收，
        // 只有玩家点了目标图标（OnCharacterPageOpened / OnPetUIOpened）才收；其余三拍保持原口径。
        // 显式传 false 顺带重置武装门限，防上一条 SetTapToHideOnce(true) 残留导致点空处秒收。
        hint.SetTapToHideOnce(!untilTargetClick);
        hint.Show(text, target, untilTargetClick ? -1f : PetFeatureGuide.HintSeconds, false, layout);
        if (!hint.IsVisible || hint.CurrentHint != text)
        {
            // Show 被战斗头顶台词占用没真弹出：退掉一次性设置，防污染下一条
            hint.SetBannerSpriteOnce(null);
            hint.SetSortOrderOnce(0);
        }
    }

    IEnumerator PlayTwoCries()
    {
        var bubble = BattleHeadTalkUI.Ensure();
        var anchor = PetUI.PetArtAnchor;
        if (anchor == null)
        {
            // 缺真值直接报错跳过，不静默沿用上一场战斗的旧位置（气泡停在左下角就是这个原因）
            Debug.LogError("[PetFeatureGuide] 宠物展示节点取不到，叫声气泡跳过（不兜底）");
            yield break;
        }
        var cries = PetBattleBanter.GetTwoCries(_petId);
        for (int i = 0; i < cries.Length; i++)
        {
            if (!PetUI.IsOpen) break;
            bubble.SetSortOrderBoostOnce(GameConfig.UiSort.TownPopup + 40);
            var co = bubble.StartCoroutine(bubble.CoPlayLineAt(anchor, cries[i], 2.2f, -50f));   // 【2026-11-28】宠物气泡往下 50
            // 【2026-11-25 主人报「喂养后退出，狗的气泡跑到角色页主角头上」】
            // 每帧盯宠物界面开合：一关就停当前这句并收气泡，不等 2.2s 播完，
            // 免得 anchor 已隐藏还把气泡钉在角色页。
            while (PetUI.IsOpen && bubble.IsShowing) yield return null;
            if (!PetUI.IsOpen)
            {
                bubble.StopCoroutine(co);
                bubble.HideNow();
                break;
            }
        }
    }

    /// <summary>玩家自己进角色页：stage 1→2，收掉指路气泡，下一步指宠物入口。</summary>
    public void OnCharacterPageOpened()
    {
        if (_stage == 1) { _stage = 2; TutorialHintUI.Instance?.Hide(); }
    }

    public void OnPetUIOpened()
    {
        if (_stage == 2) { _stage = 3; TutorialHintUI.Instance?.Hide(); }
    }

    public void OnDeployed(string petId)
    {
        if (_stage != 3) return;
        _petId = petId;
        _stage = 4;
        TutorialHintUI.Instance?.Hide();
    }

    public void OnFed(string petId)
    {
        if (_stage != 4) return;
        _petId = petId;
        _stage = 5;
    }

    void OnDestroy()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
    }
}
