using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-11-03「把测试用的那封邮件删掉」→ 2026-11-23「把奖励邮件那几封测试的邮件去掉」「只留欢迎信」】
/// 测试邮件与停发的补偿邮件已全部停发，本类只保留收尾清理：存量档里的旧邮件进主界面自动消失。清理五类：
/// ① 发件人「开发组」的历史福利邮件；② 升星补给（starup_test_mail_v1 / starup_test_mail_v2_*）；
/// ③ 标题「猎手长弓补偿」（发信未写固定 id，按标题精确匹配）；④ 旧版欢迎信 welcome_2026_v3（v4 已改奖励重发）；
/// ⑤ 补偿礼包 compensation_2026_10_v1（2026-11-23 只留新版欢迎信，停发清存量）。溢出补偿是正式邮件不清理。
/// 🔴 等存量测试档清完（或正式上线前），可连同 TownSceneBootstrap 调用行一起整体删除本类。
/// </summary>
public static class DevGiftMail
{
    /// <summary>历史测试邮件的发件人标识，同时也是清理判据（旧【开发福利】各封同为此发件人）。</summary>
    const string SenderId = "开发组";

    /// <summary>里程弓补偿邮件标题（2026-11-02 发信未写固定 id，只能按标题精确匹配；正式邮件无此标题不误删）。</summary>
    const string MileageBowMailTitle = "猎手长弓补偿";

    static bool _purgedThisLaunch;

    /// <summary>每次进主界面调用（TownSceneBootstrap 唯一调用点）；每个进程最多清一轮。</summary>
    public static void RunOncePerLaunch()
    {
        if (_purgedThisLaunch) return;
        if (SaveSystem.Instance?.Data == null) return;   // 存档未就绪：下一轮 BootstrapRoutine 再判，不兜底不发
        _purgedThisLaunch = true;

        PurgeDevMails();
    }

    /// <summary>
    /// 清掉收件箱里所有历史测试邮件（三类判据见类注释），只保留欢迎信 / 补偿礼包 / 溢出补偿等正式邮件。
    /// 逐封倒序移除后统一落盘 + 刷新红点/列表。
    /// </summary>
    static void PurgeDevMails()
    {
        var list = SaveSystem.Instance.Data.mailInbox;
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (m == null) continue;
            bool hit = m.sender == SenderId
                || m.id == "starup_test_mail_v1"
                || (m.id != null && m.id.StartsWith("starup_test_mail_v2_"))
                || m.title == MileageBowMailTitle
                || m.id == "welcome_2026_v3"           // 旧版欢迎信（v4 已改奖励重发，2026-11-23 只留新版）
                || m.id == "compensation_2026_10_v1";  // 补偿礼包停发清存量（2026-11-23 只留欢迎信）
            if (!hit) continue;
            list.RemoveAt(i);
            removed++;
        }
        if (removed > 0)
        {
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
            MailListUI.NotifyInboxChanged();   // 邮箱开着也能实时看到旧测试邮件被清掉
            Debug.Log($"[DevGift] 已清理历史测试邮件 {removed} 封");
        }
    }
}
