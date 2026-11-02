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
}
