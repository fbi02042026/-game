using UnityEngine;

/// <summary>
/// 玩家技能「自动 / 手动」释放开关（2026-11-24 主人拍板）。
///
/// <para>勾选 = 自动（默认，与历史行为逐帧一致）；取消 = 手动（点了才放，不点不过期）。</para>
///
/// <para>UI 真源 = BattleUI 预制体 <c>SkillBar/自动</c>：外观是标准 Toggle 结构
/// （<c>AgreeToggle/Background</c> + <c>AgreeToggle/Checkmark</c>），但<b>没有 Toggle 组件</b>，
/// 点击层由 <see cref="BattleUI"/> 运行时补（同 WireSkillSlotClick 范式，不改成预制体）。
/// 勾选态的视觉 = <c>AgreeToggle/Checkmark</c> 的 SetActive。</para>
///
/// <para>🔴 唯一状态出口：其他文件禁止直读 PlayerPrefs 键，一律走 <see cref="IsManual"/>。</para>
/// </summary>
public static class PlayerSkillCastMode
{
    const string PrefsKey = "settings.player_skill_manual";

    /// <summary>true = 手动（点了才放）；false = 自动（默认）。</summary>
    public static bool IsManual => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

    public static void SetManual(bool manual)
    {
        PlayerPrefs.SetInt(PrefsKey, manual ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>清档复位：回到默认「自动」。由 <see cref="NewGameReset"/> 调用。</summary>
    public static void ResetToDefault()
    {
        PlayerPrefs.SetInt(PrefsKey, 0);
    }
}
