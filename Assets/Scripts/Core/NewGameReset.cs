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
        PlayerSkillCastMode.ResetToDefault();      // 技能「自动/手动」开关回默认自动（2026-11-24）

        // 引导提示横幅：清档前那句话不该停在新档屏幕上（Hide 幂等，实例没建过就跳过）
        if (TutorialHintUI.Instance != null) TutorialHintUI.Instance.Hide();

        // 【2026-11-07】BGM 是 DDOL 双声道 + 静态 pending，不随删档文件消失。
        // 清档后 GoLogin 同步切场景、不走 Loading 流程，旧曲会一路拖到登录页：
        // 立刻切断并把待恢复曲目钉成登录曲（单一出口在 GameBgm，别在调用点散着写）。
        GameBgm.ResetForNewGame();
        // 【2026-11-09】点击集结是进程级静态状态，清档必须一起复位
        BattleRally.Reset();
        // 【2026-11-09】羁绊副本回城重开旗子（进程级静态），清档必须一起复位
        MercFavorUI.PendingOpenAfterBattleHireId = null;
        // 竞技场回城重开旗子：与上面同款（回城 ConsumePendingOpen 才消费），清档后不能让新档误弹竞技场
        ArenaPopupUI.PendingOpenAfterBattle = false;
        // 【2026-11-28】失败界面「去提升」的回城开角色页旗子同款：清档后不能让新档一回城就弹角色页
        TownHubController.PendingOpenCharacter = false;
        // 裂隙「新纪录」角标：回冒险页刷新才消费，清档后不能让新档误显上一档纪录
        EndlessRiftRules.NewRecordPendingShow = false;
        // 2026-11-12 升级弹窗队列是进程级 static，清档必须清
        LevelUpPopup.ResetForNewGame();
        // 2026-11-22 城镇入口大地图是 DDOL 静态 Canvas，清档必须一起销毁重建
        SlgMapUI.ResetForNewGame();
        SlgKingdomUI.ResetForNewGame();   // 天空王国城堡地图界面同款 DDOL Canvas
        // 【2026-11-20 天赋自检】天赋入口红点「本次登录已看」也是进程级 static：
        // 不清的话新档在本次登录内不再亮红点（定义 TalentSystem.cs:126，置位 TalentUI.cs:287）。
        TalentSystem.TalentDotSeenThisSession = false;
        // 【2026-11-25】角色页宠物/天赋箭头气泡的 3 天额度（存档字段）与本进程标记一起复位
        PetTalentHint.ResetForNewGame();

        UnityEngine.Debug.Log("[NewGameReset] 清档：进程级状态已复位（引导游标/登录弹窗/进镇闸/邮件提示/BGM/升级弹窗队列/天赋红点/竞技场回城旗/裂隙新纪录旗）");
    }
}
