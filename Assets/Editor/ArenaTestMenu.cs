#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 竞技场测试工具菜单（2026-11-11 主人拍板：5 分钟赛季周期测试用）。
/// 一键把玩家设成榜一，验证赛季结算奖励到账。
/// 随时可撤（文件很小、条件编译只在 Editor）。
/// </summary>
public static class ArenaTestMenu
{
    const string TopRankKey = "ArenaForceTopRank";

    /// <summary>
    /// 点这个菜单 → 写 PlayerPrefs 标记 → 进游戏（或游戏中开竞技场）自动把玩家积分设成"榜首+10"。
    /// 触发后会弹确认框告诉你下一步看什么。
    /// </summary>
    [MenuItem("Tools/竞技场/玩家上榜一（测试用）", false, 100)]
    static void ForceTopRank()
    {
        PlayerPrefs.SetInt(TopRankKey, 1);
        PlayerPrefs.Save();

        string msg = "已标记：进游戏后自动把玩家设成榜一。\n\n" +
                     "验证步骤：\n" +
                     "1. 开游戏 → 进竞技场 → 看榜单第一条是不是你\n" +
                     "2. 等 5 分钟（或把系统时钟往前调）触发结算\n" +
                     "3. 收邮件：金币 3000 / 钻石 300 / 冠军武器\n" +
                     "4. 新赛季开启 → 积分清零、榜单重建\n\n" +
                     "标记一次性消费，不会重复触发。";
        EditorUtility.DisplayDialog("竞技场测试", msg, "好");
    }

    /// <summary>清掉标记（不触发任何改动）。</summary>
    [MenuItem("Tools/竞技场/清除上榜一标记", false, 101)]
    static void ClearTopRank()
    {
        PlayerPrefs.DeleteKey(TopRankKey);
        PlayerPrefs.Save();
    }

    [MenuItem("Tools/竞技场/清除上榜一标记", true)]
    static bool ClearTopRankValidate() => PlayerPrefs.HasKey(TopRankKey);
}
#endif
