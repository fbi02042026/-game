using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面闲置软引导（2026-10-15 主人拍板）：
/// 「软引导在玩家在主界面停留时间长时出现，当玩家点击过一次后不再出现该引导。」
///
/// <para>行为口径：</para>
/// <list type="number">
/// <item>玩家停在主界面（大厅页 <c>MainNavTab.Guild</c>）连续超过 <see cref="IdleSeconds"/> 秒，
///       且此刻没有加载/剧情/对白/强引导（FirstRunGuide）/别的提示占屏 →
///       按 <see cref="Entries"/> 优先级挑第一个「从没点过」的底栏入口，
///       弹一条软引导（<see cref="TutorialHintUI.Show"/>，不挡点击、到点自消失）。</item>
/// <item>玩家<b>真实点击</b>过该入口一次（<see cref="MainBottomNav.OnTabSelected"/>，程序跳转不算）→
///       该条引导写 PlayerPrefs 永久标记，从此不再出现。</item>
/// <item>一直不点 → 气泡 <see cref="HintSeconds"/> 秒自己收掉，重新闲置计时，到点再弹（换下一个没点过的）。</item>
/// </list>
///
/// <para>点击记录走 PlayerPrefs（<see cref="SkillOrderGuide"/> 同款口径：纯 UI 提示，不入云存档）。
/// 强引导（FirstRunGuide）没走完时整条让位，杜绝「上面一个引导 下面一个引导」再打架。</para>
///
/// <para>唯一启动点：<see cref="TutorialDirector.NotifyTownReady"/>（每次回城都走）→ <see cref="Ensure"/>，
/// 驱动器 DontDestroyOnLoad 全局唯一，场景/条件不满足时自己安静。</para>
/// </summary>
public static class TownIdleNudge
{
    // —— 手感参数（要调只动这三个数）——
    /// <summary>主界面连续停留多久才算「停留时间长」。</summary>
    public const float IdleSeconds = 15f;
    /// <summary>气泡展示时长（软引导自消失，不挡点击）。</summary>
    public const float HintSeconds = 6f;
    const string PrefsPrefix = "IdleNudge_Clicked_";

    /// <summary>
    /// 要引导的入口与文案：优先级从上到下，点过一次的跳过。
    /// Guild 是大厅本体（玩家已经在主界面），不需要引导，不在名单里。
    /// </summary>
    public static readonly (MainNavTab tab, string text)[] Entries =
    {
        (MainNavTab.Adventure, "点「冒险」去裂隙吧，打怪能赚金币和材料哟～"),
        (MainNavTab.Character, "点「角色」看看属性，用金币点天赋就会变强啦～"),
        (MainNavTab.Tavern,    "点「酒馆」招募佣兵吧～"),
        (MainNavTab.Log,       "点「日志」看看你的冒险记录和成就哟～"),
    };

    static TownIdleNudgeDriver _driver;

    /// <summary>城镇就绪时调一次；驱动器全局唯一，重复调用安全。</summary>
    public static void Ensure()
    {
        if (_driver != null) return;
        var go = new GameObject("TownIdleNudgeDriver");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _driver = go.AddComponent<TownIdleNudgeDriver>();
    }

    /// <summary>该入口是否已被玩家点过一次（点过 = 这条引导永久关闭）。</summary>
    public static bool IsClicked(MainNavTab tab) => PlayerPrefs.GetInt(PrefsPrefix + tab, 0) != 0;

    public static void MarkClicked(MainNavTab tab)
    {
        PlayerPrefs.SetInt(PrefsPrefix + tab, 1);
        PlayerPrefs.Save();
    }

    /// <summary>调试用：清掉全部点击记录，软引导会重新逐条出现。</summary>
    public static void ResetForDebug()
    {
        for (int i = 0; i < Entries.Length; i++)
            PlayerPrefs.DeleteKey(PrefsPrefix + Entries[i].tab);
        PlayerPrefs.Save();
    }
}

/// <summary>闲置计时 + 弹泡 + 点击标记的驱动器。逻辑全在 Update，见 <see cref="TownIdleNudge"/> 文档。</summary>
public class TownIdleNudgeDriver : MonoBehaviour
{
    float _idle;
    MainNavTab? _showing;
    MainBottomNav _subscribedNav;

    void Update()
    {
        TrackNavInstance();

        // 不在主界面 / 有加载、剧情、对白、强引导、别的提示占屏 → 收掉自己的气泡，闲置清零重来
        if (!CanRun())
        {
            if (_showing != null)
            {
                TutorialHintUI.Instance?.Hide();
                _showing = null;
            }
            _idle = 0f;
            return;
        }

        // 气泡在展示中：等它自消失（或被点击回调收掉），期间不累计闲置
        if (_showing != null)
        {
            var h = TutorialHintUI.Instance;
            if (h == null || !h.IsVisible) _showing = null;
            return;
        }

        _idle += Time.unscaledDeltaTime;
        if (_idle < TownIdleNudge.IdleSeconds) return;
        _idle = 0f;   // 弹一条就重新计时；没点过的话，再闲置一会儿还会弹（换下一个没点过的）

        var nav = MainBottomNav.Instance;
        if (nav == null) return;

        var entries = TownIdleNudge.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (TownIdleNudge.IsClicked(entries[i].tab)) continue;
            RectTransform rt = ButtonRect(nav, entries[i].tab);
            if (rt == null) continue;   // 按钮没绑上：换下一个，不硬等也不报错（底栏可能在别的界面被裁掉）

            TutorialHintUI.Ensure().Show(entries[i].text, rt, TownIdleNudge.HintSeconds);
            _showing = entries[i].tab;
            Debug.Log($"[TownIdleNudge] 闲置软引导：{entries[i].tab}（点过一次后永久不再出现）");
            return;
        }
        // 四个入口全点过一遍了：这条引导从此无事可做，安静待机
    }

    /// <summary>底栏实例随场景重建，事件订阅跟着换（旧实例上退订，新实例上订阅）。</summary>
    void TrackNavInstance()
    {
        var nav = MainBottomNav.Instance;
        if (_subscribedNav == nav) return;
        if (_subscribedNav != null) _subscribedNav.OnTabSelected -= OnTabSelected;
        _subscribedNav = nav;
        if (_subscribedNav != null) _subscribedNav.OnTabSelected += OnTabSelected;
    }

    void OnTabSelected(MainNavTab tab)
    {
        // 只有真实点击才走到这（SetSelected(notify:true)），程序跳转不算「玩家点过」
        TownIdleNudge.MarkClicked(tab);
        if (_showing == tab)
        {
            TutorialHintUI.Instance?.Hide();
            _showing = null;
        }
    }

    bool CanRun()
    {
        if (!GameSceneGate.IsTown) return false;

        // 只在「主界面」（大厅页）计时；玩家切去了别的页签就算离开主界面
        var nav = MainBottomNav.Instance;
        if (nav == null || nav.Current != MainNavTab.Guild) return false;

        if (SceneLoadingCoordinator.IsActive) return false;

        // 强引导还在管屏幕（没走完 / 遮罩可见）时整条让位，别两层引导打架
        var g = FirstRunGuide.Instance;
        if (g != null && (!g.IsFinished || g.IsOverlayVisible)) return false;

        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return false;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible) return false;

        // 别的提示正在屏上（剧情后的机制提示等）：不叠层
        var hint = TutorialHintUI.Instance;
        if (hint != null && hint.IsVisible && _showing == null) return false;

        // 【2026-10-18 主人拍板】邮件窗口开着时不弹闲置引导：「点酒馆」气泡会盖住邮件底部
        // 的「一键领取」催玩家走。玩家关掉邮件重新闲置 15 秒后才轮到这条软引导——
        // 即引导顺序排到邮件领取之后。列表窗 / 详情窗任一开着都让位。
        if (MailListUI.Instance != null && MailListUI.Instance.gameObject.activeInHierarchy) return false;
        if (MailDetailUI.Instance != null && MailDetailUI.Instance.gameObject.activeInHierarchy) return false;

        return true;
    }

    static RectTransform ButtonRect(MainBottomNav nav, MainNavTab tab)
    {
        Button b;
        switch (tab)
        {
            case MainNavTab.Adventure: b = nav.adventureButton; break;
            case MainNavTab.Character: b = nav.characterButton; break;
            case MainNavTab.Tavern:    b = nav.tavernButton; break;
            case MainNavTab.Log:       b = nav.logButton; break;
            default: return null;
        }
        return b != null ? b.transform as RectTransform : null;
    }
}
