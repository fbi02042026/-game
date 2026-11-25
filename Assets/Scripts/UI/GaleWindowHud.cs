using UnityEngine;

/// <summary>
/// 疾风架势「能量槽」节点组的收尾（2026-11-25 主人拍板：疾风改回纯攻速 buff，手动窗口机制整体下线）。
/// 预制体 BattleUI 根下那组「能量槽 / 能量 / 抬手」是 2026-10-28 为倒计时窗口摆的，
/// 窗口没了就必须整组隐藏，别让它以 prefab 默认 active 状态留在屏幕上。
/// 引用按名字运行时绑（不依赖 prefab 里的 sprite/controller guid 引用）。
/// </summary>
public partial class BattleUI
{
    GameObject _galeEnergyRoot;     // 外层「能量槽」：整组显隐开关
    bool _galeNodesSearched;

    /// <summary>把「能量槽」节点组整组隐藏（幂等；节点不存在就什么都不做 —— 窗口已下线，缺了不影响玩法）。</summary>
    void EnsureGaleEnergyNodes()
    {
        if (_galeNodesSearched && _galeEnergyRoot != null) return;
        _galeNodesSearched = true;
        _galeEnergyRoot = null;

        // 外层「能量槽」是根的直接子节点，内层同名节点只能经它到达 → DFS 必先命中外层
        Transform root = FindDeepChildIgnoreCase(transform, "能量槽");
        if (root == null) return;
        _galeEnergyRoot = root.gameObject;
        _galeEnergyRoot.SetActive(false);
    }
}
