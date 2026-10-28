using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 疾风架势「手动窗口」UI（2026-10-28 主人拍板：改用预制体节点，运行时自建横幅整条删除 ——
/// 同一语义只留一个出口）。BattleUI 根下主人手工摆的「能量槽」节点组：
/// 内层「能量槽」（镜像）含「能量」= Filled Image 水平填充；旁边是「抬手」动画节点（带 Animator）。
/// 行为：开窗 = 整组显示 + 「能量」从满条按剩余时间收缩（时间走完条到底，方向由 prefab 的
/// fillOrigin 决定，代码只写 fillAmount 不碰布局）+「抬手」动画持续播放；关窗整组隐藏。
/// 引用一律按名字运行时绑（不依赖 prefab 里的 sprite/controller guid 引用）；
/// 缺节点 LogError 让主人看见（不兜底）。
/// 每帧推进入口：BattleManager.TickGaleWindow → ShowGaleWindow / UpdateGaleWindow / HideGaleWindow。
/// </summary>
public partial class BattleUI
{
    GameObject _galeEnergyRoot;     // 外层「能量槽」：整组显隐开关
    Image _galeEnergyFill;          // 「能量」填充条
    Animator _galeHandAnim;         // 「抬手」动画
    bool _galeNodesSearched;

    /// <summary>按名字绑「能量槽」节点组（幂等；找不到时每次开窗重找并报错，保持可见）。</summary>
    void EnsureGaleEnergyNodes()
    {
        if (_galeNodesSearched && _galeEnergyRoot != null) return;
        _galeNodesSearched = true;
        _galeEnergyRoot = null;
        _galeEnergyFill = null;
        _galeHandAnim = null;

        // 外层「能量槽」是根的直接子节点，内层同名节点只能经它到达 → DFS 必先命中外层
        Transform root = FindDeepChildIgnoreCase(transform, "能量槽");
        if (root == null)
        {
            Debug.LogError("[BattleUI] 疾风窗口「能量槽」节点缺失：BattleUI 下没找到 能量槽（应含 能量/抬手）→ 倒计时条不显示");
            return;
        }
        _galeEnergyRoot = root.gameObject;

        Transform fillT = FindDeepChildIgnoreCase(root, "能量");
        if (fillT != null) _galeEnergyFill = fillT.GetComponent<Image>();
        if (_galeEnergyFill == null)
            Debug.LogError("[BattleUI] 疾风窗口「能量」填充图缺失：能量槽 下没找到名为 能量 的 Image → 倒计时条画不出来");

        Transform handT = FindDeepChildIgnoreCase(root, "抬手");
        if (handT != null) _galeHandAnim = handT.GetComponent<Animator>();
        // 抬手没挂 Animator 不在此报错；控制器引用断链（56 字符 guid 旧问题）由主人按报告处理

        _galeEnergyRoot.SetActive(false);   // 平时整组隐藏，开窗才亮
    }

    public void ShowGaleWindow()
    {
        EnsureGaleEnergyNodes();
        if (_galeEnergyRoot == null) return;
        _galeEnergyRoot.SetActive(true);
        if (_galeEnergyFill != null) _galeEnergyFill.fillAmount = 1f;
        // 抬手动画：节点重新激活后 Animator 自动从默认状态起播（KeepAnimatorStateOnDisable=0）；
        // 片段没勾 Loop 时由 UpdateGaleWindow 播完续播，保证窗口期间一直播
    }

    public void UpdateGaleWindow(float remainRatio, float remainSec)
    {
        if (_galeEnergyRoot == null || !_galeEnergyRoot.activeSelf) return;
        if (_galeEnergyFill != null) _galeEnergyFill.fillAmount = Mathf.Clamp01(remainRatio);

        // 抬手一直播：片段未勾 Loop（m_LoopTime=0）时播完从头续；勾了 Loop 这段是低开销空转
        if (_galeHandAnim != null && _galeHandAnim.runtimeAnimatorController != null)
        {
            var st = _galeHandAnim.GetCurrentAnimatorStateInfo(0);
            if (!st.loop && st.normalizedTime >= 1f)
                _galeHandAnim.Play(st.shortNameHash, 0, 0f);
        }
    }

    public void HideGaleWindow()
    {
        if (_galeEnergyRoot != null) _galeEnergyRoot.SetActive(false);
    }
}
