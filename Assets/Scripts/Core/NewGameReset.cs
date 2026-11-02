/// <summary>
/// 【2026-11-02 · 清档统一复位入口】
/// 背景：设置 →「清除数据」走 <see cref="SaveSystem.DeleteLocalSaveAndReset"/> + GoLogin，
/// **不重启进程**。所以除了存档文件，还有一批「进程级」状态会活到新档：
/// PlayerPrefs 备份（PA.FirstGuide.*）、DDOL 单例里的字段（FirstRunGuide._stepIndex）、
/// 各类 static 旗子。清档只清存档 = 新档带着旧档的进度跑。
///
/// 实测事故（2026-11-02 主人报，三张截图）：删数据重跑新手教程时，
/// FirstRunGuide 单例的游标还停在 Done → IsFinished 恒真 →
/// TutorialDirector 收尾链里所有「等引导收完」的等待全部秒过
/// → 每日登录在引导还没说完就弹、赛琳的引导气泡一句都不出、结尾停在角色页。
/// 首次安装没事，是因为冷启动 Instance 为空、走 Awake 的 LoadProgress 重新读档。
///
/// 规矩：以后任何「清档 / 开新档」要复位的进程级状态，都在本类加一行，
/// 不要在调用点散着写（散着写必定漏）。只清状态，不销毁 DDOL 宿主。
/// 存档侧已经在清、无需重复的：SaveSystem 里的 StoryProgress.ResetRuntimeFlags、
/// RunLoadout.Clear、BattleStateSaver.ClearSavedState。
/// </summary>
public static class NewGameReset
{
    public static void ApplyAll()
    {
        FirstRunGuide.ResetForNewGame();            // 引导游标 + PlayerPrefs 备份 + 本会话出师标记
        DailyLoginUI.ResetRuntimeStateForNewGame(); // 领过登录 / 指路过 / 等主界面待弹
        TownSceneBootstrap.ResetRuntimeStateForNewGame(); // 进镇只查一次的两把闸
        NewMailToast.ResetRuntimeStateForNewGame(); // 「稍后再弹邮件」登记

        // 引导提示横幅：清档前那句话不该停在新档屏幕上（Hide 幂等，实例没建过就跳过）
        if (TutorialHintUI.Instance != null) TutorialHintUI.Instance.Hide();

        UnityEngine.Debug.Log("[NewGameReset] 清档：进程级状态已复位（引导游标/登录弹窗/进镇闸/邮件提示）");
    }
}
