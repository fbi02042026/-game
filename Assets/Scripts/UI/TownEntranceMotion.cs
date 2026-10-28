using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-10-25 主人需求】主界面各布局入场动效：左侧从左滑入、右上从右滑入、底部从下滑入。
/// 【2026-10-25 主人拍板「每次登录的时候出现一次」】一次登录会话只播一次：
/// 冷启动（或退出登录再登录）进主界面播一轮；之后战斗回城等再进 Town 场景不重播。
/// 登录重置出口 = <see cref="ResetForLogin"/>（BootManager.EnterTown 唯一调用）。
///
/// 方向分配（对应主人「左上角从左边进来 右上角从右边进来 下面的从下面进来 类似这种的」）：
///   · TopBar（顶条+资源条，资源条现贴右上角，见 ResourceStrip 右缘对齐）→ 从右滑入（只动 x）；
///   · RightBar（排行/每日登录/商店列）→ 从右滑入；LeftBar（公告/活动列）→ 从左滑入；
///   · BottomNav（底部五入口整条，含背景与垫底）→ 从下滑入。
///
/// 实现（不碰预制体、不写布局——铁律 20）：
///   · 只临时偏移 localPosition + CanvasGroup.alpha 淡入，播完精确写回捕获的 base 值；
///     不动 anchor / pivot / sizeDelta，任何锚点类型（含被 SafeAreaFitter 改成全拉伸的）都安全。
///   · TopBar 从右滑入只动 x：顶条挂着 SafeAreaFitter(Offset) 每帧管辖 y（x 一律不动，见其
///     ApplyByPosition 注释），x 动画与它正交不打架；BottomNav 根也是 SafeAreaFitter(Offset)
///     【2026-10-28 由 Shift 改 Offset，与 TownSharedChrome.EnsureBottomOffset 口径对齐】，
///     只在 Awake/Start/尺寸变化时施加 y，x 动画与它正交不打架。
///   · 缓动 OutBack 轻微回弹（滑到位轻轻一弹），错峰进入：顶条 → 左右列 → 底栏。
///
/// 时机（保证动效被看见）：
///   · Loading 全屏盖着主界面时不播——等 BattleLoadingOverlay 收掉再开始；
///     出场位（偏移+透明）在等待前就摆好，不会出现「先见原位再闪一下重播」。
///   · 片头/开场剧情期间大厅被隐藏（SetTownChromeVisible(false)）——记 pending，
///     等 SetTownChromeVisible(true) 揭开时才播（协程等待 + pending 双通道，见 GuildHallUI）。
///   · 回城 KeepVisible 剧情按住 Loading 时同理：剧情收完、Loading 关掉才播。
/// </summary>
public static class TownEntranceMotion
{
    public enum SlideDir { FromLeft, FromRight, FromBottom }

    const float Duration = 0.4f;    // 单个容器滑入时长（秒）
    const float DistX = 240f;       // 左右滑入距离（720 逻辑宽的 1/3，动效参数非布局尺寸）
    const float DistY = 200f;       // 底部滑入距离
    const float StaggerLeftRight = 0.08f;   // 左右列错峰（秒）
    const float StaggerBottom = 0.16f;      // 底栏错峰（秒）
    /// <summary>单帧时长上限：进主界面首帧常有 Canvas 构建/预热卡顿，单帧 unscaledDeltaTime
    /// 可能巨大到把整段动画一帧跳完（视觉=「没动效直接出现」）。钳住后卡顿恢复动画从进度处
    /// 继续流畅播完，而不是瞬间跳到终态。</summary>
    const float MaxFrameDt = 0.05f;
    /// <summary>OutBack 回弹强度（标准 1.70158，取轻量档：滑到位轻弹一下即可）</summary>
    const float BackOvershoot = 1.0f;

    static GuildHallUI _pendingHost;   // 片头期间记下，等大厅揭开再播
    static bool _playedThisLogin;      // 【主人拍板「每次登录出现一次」】本次登录会话是否已播过
    static bool _running;              // 入场动效是否正在播（任务栏逐行弹入要等它结束，见 QuestBarBinder）

    /// <summary>
    /// 入场动效是否正在播（2026-10-28：主界面左侧任务栏的逐行弹入要等它播完，
    /// 否则「整列从左滑入」与「逐行弹入」两段位移同时跑会打架）。
    /// </summary>
    public static bool IsRunning => _running;

    /// <summary>域重载兜底：进程启动（或编辑器关域重载时）清标记。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _playedThisLogin = false;
        _running = false;
    }

    /// <summary>每次登录时重置（BootManager.EnterTown 唯一调用）：登录进主界面才允许播一轮。</summary>
    public static void ResetForLogin() => _playedThisLogin = false;

    /// <summary>进主界面时调（GuildHallUI.Awake 末尾）。片头未完成则先记 pending。</summary>
    public static void Schedule(GuildHallUI host)
    {
        if (host == null) return;
        if (_playedThisLogin)
        {
            Debug.Log("[TownEntrance] 本次登录已播过 → 跳过");
            return;
        }
        if (GuildHallUI.ShouldHideTownForIntro)
        {
            _pendingHost = host;
            Debug.Log("[TownEntrance] Schedule → 片头期间转 pending，等大厅揭开再播");
            return;
        }
        _playedThisLogin = true;
        Debug.Log("[TownEntrance] Schedule → 直接排播");
        _running = true;
        host.StartCoroutine(CoPlayWhenReady(host));
    }

    /// <summary>片头结束、大厅揭开时调（GuildHallUI.SetTownChromeVisible(true)）。</summary>
    public static void PlayPending()
    {
        var host = _pendingHost;
        _pendingHost = null;
        if (host == null) return;
        _playedThisLogin = true;
        Debug.Log("[TownEntrance] PlayPending → 片头结束，开始排播");
        _running = true;
        host.StartCoroutine(CoPlayWhenReady(host));
    }

    static IEnumerator CoPlayWhenReady(GuildHallUI host)
    {
        // 【2026-10-25 修复「主界面布局崩坏」】先等布局系统跑完，再捕获基准+摆出场位：
        // GuildHallUI.Awake 同帧/随后还有 TownSceneManager 的二次 UICanvasSetup.ApplyOn、
        // sceneLoaded 补跑的竖屏重锚定（UiLayoutStretch.ApplyVerticalSplit 按「元素当前矩形」
        // 反推贴顶/贴底 offset）——出场位若摆得太早，会被这些布局逻辑当成真实摆位，
        // 布局基线被出场位污染（表现为背景/顶条/左右列错位、底栏悬空）。
        // 此时主界面仍被 Loading 盖着，等几帧无任何观感影响。
        for (int i = 0; i < 3; i++) yield return null;

        List<Item> items = Prepare(host);
        Debug.Log($"[TownEntrance] 出场位已摆好 items={items.Count} overlay={BattleLoadingOverlay.IsShowing}");
        if (items.Count == 0)
        {
            _running = false;
            yield break;
        }

        // Loading 全程盖着主界面：等它收掉再开始（出场位已在 Prepare 摆好，不会闪原位）
        while (BattleLoadingOverlay.IsShowing) yield return null;

        Debug.Log("[TownEntrance] Loading 已收，开始播放");
        float total = Duration + StaggerBottom;
        float t = 0f;
        int frames = 0;
        float startReal = Time.realtimeSinceStartup;
        while (t < total)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameDt);
            frames++;
            for (int i = 0; i < items.Count; i++)
            {
                float k = Mathf.Clamp01((t - items[i].delay) / Duration);
                items[i].Apply(k);
            }
            yield return null;
        }
        // 终态兜底：逐项精确写回 base（与捕获值完全一致，布局零残留）
        for (int i = 0; i < items.Count; i++) items[i].Finish();
        _running = false;
        Debug.Log($"[TownEntrance] 播放完成 frames={frames} 实际耗时={(Time.realtimeSinceStartup - startReal):F2}s " +
                  $"（frames≤2 说明被卡帧跳过动画）");
    }

    /// <summary>收集四个容器并立即摆好出场位（偏移 + 全透明）。</summary>
    static List<Item> Prepare(GuildHallUI host)
    {
        var list = new List<Item>();
        AddSlide(list, host.transform.Find("TopBar"), SlideDir.FromRight, 0f);
        AddSlide(list, host.transform.Find("LeftBar"), SlideDir.FromLeft, StaggerLeftRight);
        AddSlide(list, host.transform.Find("RightBar"), SlideDir.FromRight, StaggerLeftRight);
        AddSlide(list, host.transform.Find("BottomNav"), SlideDir.FromBottom, StaggerBottom);
        return list;
    }

    static void AddSlide(List<Item> list, Transform node, SlideDir dir, float delay)
    {
        if (node == null)
        {
            // 布局容器缺真值：报错并跳过该容器（fail closed，不造替身节点）
            Debug.LogError("[TownEntrance] 主界面布局容器找不到：" + dir + " —— 该布局入场动效跳过");
            return;
        }
        var rt = node as RectTransform;
        if (rt == null) return;

        var item = new Item
        {
            rt = rt,
            baseLocalPos = rt.localPosition,
            delay = delay,
        };
        switch (dir)
        {
            case SlideDir.FromLeft: item.offset = new Vector3(-DistX, 0f, 0f); break;
            case SlideDir.FromRight: item.offset = new Vector3(DistX, 0f, 0f); break;
            case SlideDir.FromBottom: item.offset = new Vector3(0f, -DistY, 0f); break;
        }

        item.group = node.GetComponent<CanvasGroup>();
        if (item.group == null) item.group = node.gameObject.AddComponent<CanvasGroup>();

        rt.localPosition = item.baseLocalPos + item.offset;   // 出场位：Loading 盖屏期间摆好
        item.group.alpha = 0f;
        list.Add(item);
    }

    class Item
    {
        public RectTransform rt;
        public CanvasGroup group;
        public Vector3 baseLocalPos;
        public Vector3 offset;
        public float delay;

        /// <summary>k=0~1；前 60% 进度淡入到全亮，位移走 OutBack（末段轻微回弹）。</summary>
        public void Apply(float k)
        {
            if (rt == null) return;
            k = Mathf.Clamp01(k);
            if (group != null) group.alpha = Mathf.Clamp01(k / 0.6f);
            float e = EaseOutBack(k);
            rt.localPosition = baseLocalPos + offset * (1f - e);
        }

        public void Finish()
        {
            if (rt != null) rt.localPosition = baseLocalPos;
            if (group != null) group.alpha = 1f;
        }
    }

    /// <summary>OutBack：1 + (c1+1)·(k-1)^3 + c1·(k-1)^2，k=1 时精确回到 1（回弹不残留）。</summary>
    static float EaseOutBack(float k)
    {
        float p = k - 1f;
        return 1f + (BackOvershoot + 1f) * p * p * p + BackOvershoot * p * p;
    }
}
