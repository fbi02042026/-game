using System.Collections;
using UnityEngine;

/// <summary>
/// 宠物功能新手引导（2026-11-22）：1-3 正式主线首通 → 回城 → 告知开启宠物功能 →
/// 底栏气泡指角色页 → 玩家自己进角色页后气泡指宠物入口 → 上阵 → 喂养（随机属性 +X + 亲密度）→ 宠物用战斗对话框叫两声。
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

    static bool _pending;                    // 跨场景 pending：1-3 通关置位，回城消费
    static PetFeatureGuideDriver _driver;

    /// <summary>1-3 正式主线通关时调（Settlement 已做好章节/模式判定）。</summary>
    public static void MarkClearedStage13()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.HasIntruderStorySeen(DoneKey)) return;
        _pending = true;
        Debug.Log("[PetFeatureGuide] 1-3 首通，pending 宠物功能引导");
    }

    /// <summary>回城时调（TownHubController.Start）。启动全局唯一驱动器，条件不满足时它自己安静。</summary>
    public static void TryConsumeOnTown()
    {
        if (!_pending) return;
        _pending = false;
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.HasIntruderStorySeen(DoneKey)) return;
        // 宠物功能还没解锁（营救剧情没播）→ 不引导，留待下次回城再由入口拦截提示去第 3 关
        if (!data.HasIntruderStorySeen(PetRescueStoryBeats.SeenKey)) return;

        Ensure().Begin();
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
}

/// <summary>引导状态机驱动器：DontDestroyOnLoad，协程推进，软提示 + 看门狗，任何一步可跳过。</summary>
public class PetFeatureGuideDriver : MonoBehaviour
{
    Coroutine _co;
    int _stage;   // 0 idle / 1 待进角色页 / 2 待开宠物界面 / 3 待上阵 / 4 待喂养 / 5 收尾中
    string _petId;

    public void Begin()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoGuide());
    }

    IEnumerator CoGuide()
    {
        _stage = 1;
        // 等预热收起，避免 toast 被 Loading 吞（照 TutorialDirector.Town 现成判据）
        float warmupWait = 0f;
        while (!TownHubController.WarmupDone && warmupWait < 8f)
        {
            warmupWait += Time.unscaledDeltaTime;
            yield return null;
        }

        // 第一拍：告知开启宠物功能（前台小姐傲娇口吻，自带口吻字）
        GlobalToastUI.ShowPersona("第三关打完了，宠物功能给你开啦～去角色页找那个新图标，我可不等你哦～");
        yield return new WaitForSecondsRealtime(2.2f);

        // 第二拍【2026-11-23 酒馆气泡模式】：不自动切页，底栏「角色」按钮贴气泡指路，等玩家自己点进去。
        var nav = MainBottomNav.Instance;
        var navRect = nav != null && nav.characterButton != null
            ? nav.characterButton.transform as RectTransform : null;
        if (navRect != null)
            ShowBubble("先去底栏「角色」页，宠物功能藏在那儿～", navRect, false);
        else
            Debug.LogError("[PetFeatureGuide] 底栏角色按钮取不到，角色页指路气泡跳过（不静默兜底）");
        yield return WaitStage(2);   // 等玩家自己点进角色页（TownHubController 通知）

        // 第三拍：指宠物入口（玩家已在角色页，用普通气泡层即可）
        var charUi = CharacterUI.Instance;
        var entryRect = charUi != null ? CharacterUI.PetEntryRect : null;
        if (entryRect != null)
            ShowBubble("点这个「宠物」图标进去看看嘛～", entryRect, false);
        else
            Debug.LogError("[PetFeatureGuide] 角色页宠物入口取不到，指路气泡跳过");
        yield return WaitStage(3);   // 等玩家点开宠物界面

        // 第四拍：指上阵按钮（宠物弹窗在 TownPopup 900 层，气泡要抬层盖过去）
        ShowBubble("先点「上阵」把它带上，我不会亏待你的宠物哦～", PetUI.DeployBtnRect, true);
        yield return WaitStage(4);   // 等玩家上阵

        // 第五拍：指喂养按钮（同一个按钮，上阵后文案变「喂养」）
        ShowBubble("再点「喂养」一次，它会长属性、跟你更亲哦～", PetUI.DeployBtnRect, true);
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
    static void ShowBubble(string text, RectTransform target, bool overPopup)
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
        hint.SetTapToHideOnce(true);   // 点一下即收，不留屏
        hint.Show(text, target, PetFeatureGuide.HintSeconds, false);
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
        var cries = PetBattleBanter.GetTwoCries(_petId);
        for (int i = 0; i < cries.Length; i++)
        {
            yield return bubble.CoPlayLineAt(anchor, cries[i], 2.2f);
            if (!PetUI.IsOpen) break;   // 玩家中途关了宠物界面就收尾
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
