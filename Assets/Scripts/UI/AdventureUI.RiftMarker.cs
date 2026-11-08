using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 冒险界面 partial —— 无限裂隙「出生点」头像指示（2026-11-02 主人拍板）
//
// 主人原话：「还有无限关卡也要玩家头像指向所在的层 也就是出生点」。
// 出生点 = 下次起始层 = 保存点 + 1（EndlessRiftRules.StartStage 唯一出口）= 无限页最底下那张层数牌。
//
// 实现走「借」而不是「造」：复用主线那颗预制体「头像指示」（同一套头像图/箭头图/镜像朝向/瞄准角算法），
// 进裂隙时把它改挂到 无限/关卡 下、钉在底槽左侧，箭头尖指向底槽。
// 为什么不会和主线打架：
//   · 主线的每帧跟随 UpdateStageMarkerFollow 挂在 AdventureUI.Update 上，而 Update 开头
//     `_guankaRoot == null || !activeInHierarchy` 就早退 —— 裂隙模式下主线关卡列整组隐藏，跟随不会跑；
//   · 另外 UpdateStageMarkerFollow 里加了一道「父节点不是滑动视口就放手」的保险（见 ChestNodes）。
//   · 切回主线时 RefreshChestNodes → UpdateStageMarker 会把节点挂回滑动视口，位置重新由每帧跟随接管。
public partial class AdventureUI
{
    /// <summary>箭头尖与层数牌左缘的净间距（不让箭头压住「第N层」的字）。</summary>
    const float RiftMarkerGap = 12f;
    /// <summary>底槽岛屿宽度兜底（预制体「岛」= 151×155；取不到子节点时用这个半宽）。</summary>
    const float RiftMarkerCardHalfW = 75.5f;

    /// <summary>
    /// 裂隙页刷新出生点头像（RefreshRiftPage 调，幂等）：
    /// 头像钉在底槽（出生点）左侧、y 与底槽同高，箭头绕头像中心旋转瞄准底槽。
    /// </summary>
    /// <param name="guanka">无限/关卡 容器（头像的父节点；容器本身不被适配移动）。</param>
    /// <param name="spawnSlot">底槽 = 第 StartStage 层那张牌（层数牌为空时传 null → 整颗标识隐藏）。</param>
    void UpdateRiftStageMarker(RectTransform guanka, RectTransform spawnSlot)
    {
        if (_stageMarker == null) EnsureStageMarkerNode();   // 只建/绑一次（内部会先挂到滑动视口，下面再挪走）
        if (_stageMarker == null) return;

        if (guanka == null || spawnSlot == null)
        {
            if (_stageMarker.gameObject.activeSelf) _stageMarker.gameObject.SetActive(false);
            return;
        }
        if (_stageMarker.parent != guanka) _stageMarker.SetParent(guanka, false);

        // 层数牌的实际半宽取「岛」子节点（151 宽），取不到用兜底常量
        var island = spawnSlot.Find("岛") as RectTransform;
        float cardHalfW = island != null && island.rect.width > 1f ? island.rect.width * 0.5f : RiftMarkerCardHalfW;

        // 箭头是从头像中心往「指向方向」伸出去的（预制体「箭头」在头像中心下方 96，旋转 90° 后变成横向），
        // 头像贴太近箭头就会盖住「第N层」的字 → 把这截伸展长度也算进间距。
        float arrowReach = 0f;
        if (_stageMarkerArrow != null)
            arrowReach = Mathf.Abs(_stageMarkerArrow.anchoredPosition.y)
                         + _stageMarkerArrow.rect.height * 0.5f;

        float cardLeft = spawnSlot.anchoredPosition.x - cardHalfW;
        float x = cardLeft - RiftMarkerGap - arrowReach;
        float y = spawnSlot.anchoredPosition.y;
        _stageMarker.anchoredPosition = new Vector2(x, y);

        // 箭头瞄准底槽：与主线同一套镜像补偿（标识根 localScale.x = -1，旋转视觉反角）
        if (_stageMarkerArrowRot != null)
        {
            float dx = spawnSlot.anchoredPosition.x - x;
            float dy = spawnSlot.anchoredPosition.y - y;
            if (dx * dx + dy * dy > 1f)
                _stageMarkerArrowRot.localEulerAngles =
                    new Vector3(0f, 0f, Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg);
        }

        // 裂隙页没有「跨章灰锁」概念，箭头恒显示
        if (_stageMarkerArrow != null && !_stageMarkerArrow.gameObject.activeSelf)
            _stageMarkerArrow.gameObject.SetActive(true);
        if (!_stageMarker.gameObject.activeSelf) _stageMarker.gameObject.SetActive(true);
        _stageMarker.SetAsLastSibling();   // 压在层数牌之上，别被牌面遮住
        // 头像带 Button（主线里点它 = 滚回当前关）；裂隙页没有可滚的列 → 置 -1 让点击成为 no-op，
        // 免得拿着主线残留的槽位号去滚一张已经隐藏的列表。
        _markerCurrentSlot = -1;
    }

    // ============================================================
    // 【2026-11-02 主人拍板】「选择重新打的时候，岛可以快速滑动到最高的保存点，
    // 头像的箭头也一直指向那个保存点」
    //
    // 裂隙页是 5 张**固定布局**的层数牌（无 ScrollRect，底槽 = 起始层），没有真的可滚列表，
    // 所以「滑动」实现为**编号快速翻滚**：点「直达第 N 层」的瞬间，5 张牌的编号从当前起始层
    // 一路快速滚到 N，停住 —— 二次确认期间玩家亲眼看到「自己要跳到哪」。
    // 箭头不用挪：底槽位置固定，翻滚全程它都指着底槽（= 目标窗口的出生点），天然「一直指向」。
    // 收尾时箭头做一次缩放脉冲，把视线钉在目标保存点上。
    // 还原走「瞬间刷回」（返回/确认/弹窗关闭），滑出去要仪式感、滑回来要快。
    // ============================================================

    /// <summary>编号翻滚总时长（20 层 ≈ 0.45s，主人要「快速」；要调手感只改这一个数）。</summary>
    const float RiftPreviewSlideSec = 0.45f;
    /// <summary>箭头收尾脉冲的放大倍率（基于预制体原始 scale 均匀缩放，不破坏 x=-1 镜像）。</summary>
    const float RiftPreviewPulseScale = 1.22f;

    /// <summary>预览窗口的起始层（0 = 无预览，层数牌按真实 StartStage 显示）。翻滚协程与 RefreshRiftPage 共写。</summary>
    int _riftPreviewStart;
    Coroutine _riftPreviewCo;

    /// <summary>
    /// 点「直达第 N 层」时调：层数牌编号从当前窗口快速翻滚到 targetStart 窗口并停住。
    /// 重复点击/连续翻滚安全（先停上一条协程，从当前预览值接着滚）。
    /// </summary>
    void PreviewRiftSlideTo(int targetStart)
    {
        var guanka = FindRiftStageContainer();
        if (guanka == null) return;
        var slots = CollectNumberedSlots(guanka);
        if (slots.Count == 0) return;
        if (_riftPreviewCo != null) { StopCoroutine(_riftPreviewCo); _riftPreviewCo = null; }

        int from = _riftPreviewStart > 0 ? _riftPreviewStart : EndlessRiftRules.StartStage();
        _riftPreviewStart = targetStart;
        if (targetStart == from)
        {
            ApplyRiftPlateNumbers(slots, targetStart);   // 同窗口重复点：直接对齐，不空转动画
            return;
        }
        _riftPreviewCo = StartCoroutine(CoRiftPreviewSlide(slots, from, targetStart));
    }

    IEnumerator CoRiftPreviewSlide(System.Collections.Generic.List<RectTransform> slots, int from, int to)
    {
        float t = 0f;
        while (t < RiftPreviewSlideSec)
        {
            t += Time.unscaledDeltaTime;
            // 线性递增的编号翻滚（步长按剩余距离均摊，保证最后一帧正好落在 to）
            float k = Mathf.Clamp01(t / RiftPreviewSlideSec);
            ApplyRiftPlateNumbers(slots, Mathf.RoundToInt(Mathf.Lerp(from, to, k)));
            yield return null;
        }
        ApplyRiftPlateNumbers(slots, to);
        _riftPreviewCo = null;
        yield return CoPulseStageMarker();
    }

    /// <summary>
    /// 箭头缩放脉冲（捕获原始 scale 均匀缩放，结束原样还原；页面隐藏时直接跳过）。
    /// <paramref name="pulses"/> = 连续脉冲几下（波形 |sin|，每半波一下，之间回到原大 ——
    /// 死亡回城「箭头脉冲两下把视线引到出生点」走 2）。
    /// </summary>
    IEnumerator CoPulseStageMarker(int pulses = 1)
    {
        if (_stageMarker == null || !gameObject.activeInHierarchy) yield break;
        Vector3 baseScale = _stageMarker.localScale;   // 预制体真值（含 x=-1 镜像），只乘系数不覆盖
        const float PulseSec = 0.24f;
        float total = PulseSec * Mathf.Max(1, pulses);
        float t = 0f;
        while (t < total && _stageMarker != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / total);
            // |sin(k·pulses·π)|：一下 = 一个半波（升到峰值回落），两下 = 两个半波（中间回 1 分隔）
            float wave = Mathf.Abs(Mathf.Sin(k * pulses * Mathf.PI));
            float s = 1f + (RiftPreviewPulseScale - 1f) * wave;
            _stageMarker.localScale = baseScale * s;
            yield return null;
        }
        if (_stageMarker != null) _stageMarker.localScale = baseScale;
    }

    /// <summary>
    /// 【2026-11-02 晚主人拍板】死亡回城后的引导脉冲：箭头连脉冲两下，把视线引到出生点。
    /// RefreshRiftPage 读到 <see cref="EndlessRiftRules.PulseMarkerOnDeathReturn"/> 时调（读后即清）。
    /// </summary>
    void PlayDeathReturnPulse()
    {
        if (_riftPreviewCo != null) return;   // 直达翻滚进行中不叠加（翻滚自己带收尾脉冲）
        StartCoroutine(CoPulseStageMarker(2));
    }

    /// <summary>
    /// 收掉直达预览、把层数牌刷回真实起始层窗口。走「瞬间还原」不播动画：
    /// 返回上层选择/确认进本时玩家要的是继续操作，不是再看一遍翻滚。
    /// </summary>
    void RestoreRiftPreview()
    {
        if (_riftPreviewCo != null) { StopCoroutine(_riftPreviewCo); _riftPreviewCo = null; }
        if (_riftPreviewStart <= 0) return;
        _riftPreviewStart = 0;
        RefreshRiftPage();   // 编号/排行榜/箭头一把刷回真实状态
    }
}
