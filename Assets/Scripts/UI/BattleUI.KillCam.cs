using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 击杀镜头期间对 HUD 的缩放补偿（静态方法）。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<Transform, Vector3> _killCamHudBaseScales =
        new System.Collections.Generic.Dictionary<Transform, Vector3>();
    // 【2026-11-24】队伍动态视野每帧调本方法：缓存已解析节点，避免每帧 8 次深度遍历。
    static Transform[] _killCamHudNodes;
    static BattleUI _killCamHudOwner;   // 解析时的 BattleUI 实例：只在跨局重建时整批重解析

    // 【2026-11-24 修「底部面板被放大 1.5 倍」只列「互不嵌套」的最外层：
    // BackpackPanel 已包住 zhuangshi>CharacterBar>SkillBar，父子同时补偿会把 1/zoom
    // 连乘（1.15³≈1.52）→ 底部面板越补越大、子节点挤位（技能栏「自动」被挤/被挡）。
    static readonly string[] KillCamHudNodeNames =
    {
        "TopBar", "TopStatus", "ProgressBar",
        "QuestText", "QuestPanel", "BackpackPanel",
        "BottomBar", "PausePanel", "SettingsPanel"
    };

    /// <summary>击杀镜头 / 队伍动态视野：HUD 反缩放（乘 ortho 倍率），保持界面原尺寸。</summary>
    public static void ApplyKillCamHudCompensation(float zoomMul)
    {
        if (Instance == null || zoomMul <= 0.01f) return;
        // 【2026-11-24 修「底部面板无限放大」】旧逻辑只要任一缓存节点为 null 就整批重建并清 base：
        // 名单里 TopStatus/QuestText/BottomBar/PausePanel/SettingsPanel 在战斗 prefab 中不存在，
        // 这些常驻 null 让每帧都重建 → 把上一帧已乘过补偿的 localScale 重新记成 base → 逐帧放大。
        // 现改为：只在 BattleUI 实例变化（跨局重建）时整批重解析；个别节点缺失只单独补查，不碰其它 base。
        bool needResolve = _killCamHudNodes == null || _killCamHudOwner != Instance;
        if (needResolve)
        {
            _killCamHudOwner = Instance;
            _killCamHudNodes = new Transform[KillCamHudNodeNames.Length];
            for (int i = 0; i < KillCamHudNodeNames.Length; i++)
                _killCamHudNodes[i] = FindChildIgnoreCase(Instance.transform, KillCamHudNodeNames[i]);
            _killCamHudBaseScales.Clear();
        }
        // 【2026-11-24 定论：HUD 不需要按 ortho 补偿，倍率恒为 1】
        // 战斗 Canvas 是 ScreenSpaceCamera + CanvasScaler(ScaleWithScreenSize)（BattleViewportFit.Apply 里设的），
        // Unity 会把 Canvas 重新铺满视锥，所以 UI 的像素尺寸与 camera.orthographicSize 无关；
        // 会随 ortho 缩放的只有世界里的角色/场景（地图 map 也是 UI，同样不动）。
        // 这里乘任何倍率都只是把面板整体改大/改小（实测乘 0.87 面板就变小），所以恒乘 1：
        // 缓存仍然保留，作用是把历史上被乘坏过的 localScale 还原成基准值。
        float hudScale = 1f;
        for (int i = 0; i < _killCamHudNodes.Length; i++)
        {
            Transform t = _killCamHudNodes[i];
            if (t == null)
            {
                // 事后才出现 / 旧实例被销毁：只补这一个槽位，绝不清 base（清了就会把当前缩放当新基准）。
                t = FindChildIgnoreCase(Instance.transform, KillCamHudNodeNames[i]);
                _killCamHudNodes[i] = t;
                if (t == null) continue;
            }
            if (!_killCamHudBaseScales.ContainsKey(t))
                _killCamHudBaseScales[t] = t.localScale;
            Vector3 b = _killCamHudBaseScales[t];
            t.localScale = new Vector3(b.x * hudScale, b.y * hudScale, b.z * hudScale);
        }
    }

    public static void ResetKillCamHudCompensation()
    {
        foreach (var kv in _killCamHudBaseScales)
        {
            if (kv.Key != null)
                kv.Key.localScale = kv.Value;
        }
        _killCamHudBaseScales.Clear();
        _killCamHudNodes = null;
        _killCamHudOwner = null;
    }
}
