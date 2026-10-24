using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 队列交互：池格点击、付费解锁末位、已拥有详情、换人（点选态 / 换下 / 摆动动效）。
/// 【2026-10-23 主人拍板】原 MercDrawPoolUI.cs 已 818 行（项目约定 >600 行必须拆），
/// 按职责再切一块，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercDrawPoolUI : MonoBehaviour
{

    /// <summary>点池格头像 = 看图鉴详情（下架走右上角红「−」，主人设计图口径）；
    /// 替换点选态下点格 = 换下这个佣兵（2026-10-17 二次拍板）。</summary>
    void OnPoolSlotClick(int index)
    {
        // 【2026-10-17 二次拍板】替换点选态：点队列格子 = 换下这个，不弹任何窗体
        if (_swapSelecting)
        {
            HandleSwapSelectClick(index);
            return;
        }
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        Debug.Log("[MercDrawPoolUI] 点击池格 " + index + "（开放格数=" + slots + "）");
        if (index >= slots)
        {
            // 【2026-10-20 主人拍板】点锁格必须弹提示 —— force 弹，防被酒馆气泡/对话压制吞掉
            // 【2026-10-23 主人拍板】第 5 格（最后一格）在通第 2 章后可花钻解锁：弹确认窗，确认扣钻开格；
            // 没通第 2 章时按顺序提示先通关（花钻开的一定是最后一格，不乱序）。
            if (index == SaveData.DrawPoolMaxSlots - 1)
            {
                if (data != null && data.CanPaidUnlockDrawPoolSlot)
                {
                    TryPaidUnlockLastSlot(data);
                    return;
                }
                if (data == null || !data.HasClearedChapter(2))
                {
                    UIManager.Instance?.ShowToast("通关第 2 章后开启", true);
                    return;
                }
            }
            UIManager.Instance?.ShowToast(LockHintOf(index) + "后开启", true);
            return;
        }
        var ordered = _picked;
        if (index >= ordered.Count) return;
        if (MercRosterDefs.TryGetByHireId(ordered[index], out var def))
            ShowMercDetail(ordered[index], def);
    }

    /// <summary>
    /// 第 5 格（最后一格）花钻解锁（2026-10-23 主人拍板：100 钻）：
    /// 通用确认弹窗（StaminaConfirmPopup，硬规则：双按钮确认框一律走它）→ 确认后扣钻 + 置位存档 + 刷新。
    /// </summary>
    void TryPaidUnlockLastSlot(SaveData data)
    {
        int cost = SaveData.DrawPoolPaidUnlockCost;
        StaminaConfirmPopup.Show("解锁佣兵队列",
            $"花费 {cost} 钻石解锁最后一个佣兵队列格子？",
            $"确定解锁（{cost}钻）",
            () =>
            {
                // 弹窗期间存档可能变化，确认时重查条件，防重复解锁/重复扣钻
                if (!data.CanPaidUnlockDrawPoolSlot) return;
                if (!ResourceWallet.TrySpendDiamond(cost, "佣兵队列解锁", save: false, notify: true))
                    return;   // 钻不足时 TrySpendDiamond 已弹「钻石不足」
                data.MarkDrawPoolPaidUnlock();
                SaveSystem.Instance.Save();
                UIManager.Instance?.ShowToast($"已解锁第 {SaveData.DrawPoolMaxSlots} 格佣兵队列", true);
                Refresh();
            });
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】点已获得卡 = 直接弹「获得佣兵」详情弹窗：
    /// 未入队的佣兵 = 「替换雇佣队列」场景，弹窗右下带「替换」钮（回调 <see cref="OnCardSwap"/>：
    /// 池有空位直接入队，池满进入点选态点队列格子换下谁）；已上阵（在队列）的只看详情，只带确认钮。
    /// </summary>
    void ShowOwnedDetail(string hireId)
    {
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) return;
        bool inQueue = _picked.Contains(hireId);
        ShowMercDetail(hireId, def, !inQueue, inQueue ? null : (System.Action)(() => OnCardSwap(hireId)));
    }

    /// <summary>
    /// 卡上「替换」钮：池有空位直接入队；池满【2026-10-17 二次拍板】不弹替换面板——进入点选态，
    /// 上方队列已上阵格子亮「换下」，直接点要换下的那个（同一张卡再点「替换」= 取消）。
    /// </summary>
    void OnCardSwap(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[MercDrawPoolUI] 点替换时存档为空，未处理");
            return;
        }

        // 点选态中再点同一张「替换」= 取消本次替换
        if (_swapSelecting && _replacingInId == hireId)
        {
            ExitSwapSelecting();
            UIManager.Instance?.ShowToast("已取消替换");
            return;
        }

        if (data.AddToDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast(def.Name + " 已入队");
            Commit();
            return;
        }

        if (_picked.Count < data.DrawPoolSlots)
        {
            Debug.LogError("[MercDrawPoolUI] 入队未成且池未满：hireId=" + hireId
                + "（查 AddToDrawPool 返回 false 的原因）");
            return;
        }

        // 池满：进入点选态（不弹窗），Refresh 重刷队列格并打「换下」高亮，格子左右晃
        _replacingInId = hireId;
        _swapSelecting = true;
        UIManager.Instance?.ShowToast("请点上方队列中要换下的佣兵");
        Refresh();
        StartSwapSway();
    }

    /// <summary>点选态下点队列格：点谁换下谁，换完即退点选态（改动即时落盘）。</summary>
    void HandleSwapSelectClick(int index)
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        if (index >= slots)
        {
            UIManager.Instance?.ShowToast("这一格还没开启，点已上阵的佣兵");
            return;
        }
        var ordered = _picked;
        if (index >= ordered.Count)
        {
            UIManager.Instance?.ShowToast("这是空格，点要换下的佣兵");
            return;
        }
        string outId = ordered[index];
        if (data != null && data.SwapInDrawPool(_replacingInId, outId))
        {
            UIManager.Instance?.ShowToast("替换成功");
            _swapSelecting = false;
            _replacingInId = null;
            StopSwapSway();   // 先还原格子角度再刷新
            Commit();   // 刷新时点选态已退，SwapSel 标记由 RefreshPrefabPoolSlot 清掉
        }
        else
        {
            Debug.LogError("[MercDrawPoolUI] 替换失败：换上=" + _replacingInId + " 换下=" + outId);
        }
    }

    /// <summary>
    /// 点选态标记：队列已上阵格子盖黄「换下」（raycast 不拦点击，点格即换）；
    /// 选择期间收掉红「−」，防误触出队触发自动补位打乱替换。
    /// </summary>
    void ApplySwapSelectMarks()
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        var ordered = _picked;
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            if (_poolSlots[i] == null) continue;
            var tr = _poolSlots[i].transform;
            var qx = tr.Find("quxiao");
            if (qx != null) qx.gameObject.SetActive(false);
            if (i >= slots || i >= ordered.Count) continue;
            var mark = NewImage(tr, "SwapSel", new Color(1f, 0.80f, 0.25f, 0.45f));
            Stretch(mark.rectTransform);
            mark.transform.SetAsLastSibling();   // 压在立绘最上层
            MakeText(mark.transform, "Label", "换下", 22);
        }
    }

    /// <summary>退点选态：停晃动、清状态并 Refresh 还原常规态（非点选态调用直接返回）。</summary>
    void ExitSwapSelecting()
    {
        if (!_swapSelecting) return;
        _swapSelecting = false;
        _replacingInId = null;
        StopSwapSway();
        Refresh();
    }

    // ---- 点选态晃动动画（绕中心左右旋转，主人 2026-10-18 拍板：不要位移）----

    /// <summary>开始晃队列已上阵格子；重复调用先还原再重采基准角度（切换替换目标时会带着偏转二次进入）。
    /// 2026-10-18 主人拍板：每张卡独立晃（各自相位），不再整排同摆；绕各自中心左右旋转、不位移。</summary>
    void StartSwapSway()
    {
        StopSwapSway();
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        var ordered = _picked;
        var targets = new List<GameObject>();
        for (int i = 0; i < _poolSlots.Count && i < slots && i < ordered.Count; i++)
        {
            var go = _poolSlots[i];
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;
            _swapSwayBase[go] = rt.localEulerAngles.z;   // 基准 = 格子自身旋转 Z（预制体摆的角度）
            _swapSwayPhase[go] = i * 1.7f + 0.6f;      // 每卡错开相位（不整除 2π，避免肉眼同步）
            targets.Add(go);
        }
        if (targets.Count == 0) return;
        _swapSwayCo = StartCoroutine(CoSwapSway(targets));
    }

    /// <summary>停晃动并把格子旋转还原到基准（幂等：没在晃时调用只清缓存）。</summary>
    void StopSwapSway()
    {
        if (_swapSwayCo != null)
        {
            StopCoroutine(_swapSwayCo);
            _swapSwayCo = null;
        }
        foreach (var kv in _swapSwayBase)
        {
            if (kv.Key == null) continue;
            var rt = kv.Key.GetComponent<RectTransform>();
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, kv.Value);
        }
        _swapSwayBase.Clear();
        _swapSwayPhase.Clear();
    }

    /// <summary>左右旋转协程：每格绕自身中心正弦摆旋转 Z（不位移），相位独立（2026-10-18 主人拍板）；
    /// 仅在点选态运行；用 unscaled 时间防 timescale 异常卡死。</summary>
    System.Collections.IEnumerator CoSwapSway(List<GameObject> targets)
    {
        float t = 0f;
        while (_swapSelecting)
        {
            t += Time.unscaledDeltaTime * SwapSwaySpeed;
            for (int i = 0; i < targets.Count; i++)
            {
                var go = targets[i];
                if (go == null || !_swapSwayBase.TryGetValue(go, out var baseZ)) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                float phase = _swapSwayPhase.TryGetValue(go, out float ph) ? ph : 0f;
                rt.localRotation = Quaternion.Euler(0f, 0f, baseZ + Mathf.Sin(t + phase) * SwapSwayAmpDeg);
            }
            yield return null;
        }
    }

    // 【2026-10-23 主人拍板】RefreshReplacePanel / LayoutReplace / _replaceCards 已删除：
    // 替换弹窗早在 2026-10-17 二次拍板废弃（改直接点队列格子），这两个方法零调用。
    // 节点字段 _replacePanel/_replaceContent/_replaceTitle/_replacingInId 仍在用（点选态与节点绑定），保留。

    /// <summary>
}
