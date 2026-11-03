using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-11-03 主人拍板「把测试用的那封邮件删掉」】测试福利邮件（旧【测试用】/ 更早的【开发福利】）已<b>停发</b>；
/// 本类只保留收尾清理：把存量测试档里历史测试邮件（发件人 == <see cref="SenderId"/>）清掉，
/// 玩家下次进主界面自动消失。欢迎信 / 补偿礼包 / 里程弓补偿是 MailSystem 正式链路发的（发件人「制作组」），不在清理范围。
/// 清理不分正式/测试包：历史测试邮件在任何包里都该消失（Debug.isDebugBuild 开关已随停发一并移除）。
/// 🔴 等存量测试档清完（或正式上线前），可连同 TownSceneBootstrap 调用行一起整体删除本类。
/// </summary>
public static class DevGiftMail
{
    /// <summary>历史测试邮件的发件人标识，同时也是清理判据（旧【开发福利】各封同为此发件人）。</summary>
    const string SenderId = "开发组";

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
    /// 清掉收件箱里所有历史测试邮件（发件人 == <see cref="SenderId"/>，即旧【开发福利】/【测试用】各封），
    /// 只保留欢迎信 / 补偿礼包 / 里程弓补偿等正式邮件。逐封倒序移除后统一落盘 + 刷新红点/列表。
    /// </summary>
    static void PurgeDevMails()
    {
        var list = SaveSystem.Instance.Data.mailInbox;
        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (m == null || m.sender != SenderId) continue;
            list.RemoveAt(i);
            removed++;
        }
        if (removed > 0)
        {
            SaveSystem.Instance.Save();
            RedDot.RefreshCommon();
            MailListUI.NotifyInboxChanged();   // 邮箱开着也能实时看到旧测试邮件被清掉
            Debug.Log($"[DevGift] 已清理历史测试邮件 {removed} 封（2026-11-03 停发）");
        }
    }
}
