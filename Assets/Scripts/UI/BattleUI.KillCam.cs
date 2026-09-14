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

    static readonly string[] KillCamHudNodeNames =
    {
        "TopBar", "TopStatus", "ProgressBar",
        "QuestText", "QuestPanel", "BackpackPanel",
        "BottomBar", "SkillBar", "PausePanel", "SettingsPanel",
        "CharacterBar"
    };

    /// <summary>击杀镜头：HUD 反缩放，避免 ortho zoom 把界面一起放大。</summary>
    public static void ApplyKillCamHudCompensation(float zoomMul)
    {
        if (Instance == null || zoomMul <= 0.01f) return;
        float inv = 1f / zoomMul;
        for (int i = 0; i < KillCamHudNodeNames.Length; i++)
        {
            Transform t = FindChildIgnoreCase(Instance.transform, KillCamHudNodeNames[i]);
            if (t == null) continue;
            if (!_killCamHudBaseScales.ContainsKey(t))
                _killCamHudBaseScales[t] = t.localScale;
            Vector3 b = _killCamHudBaseScales[t];
            t.localScale = new Vector3(b.x * inv, b.y * inv, b.z * inv);
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
    }
}
