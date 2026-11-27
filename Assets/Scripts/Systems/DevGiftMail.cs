using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-11-27 主人「把测试邮件都去掉，只留一个欢迎邮件」】测试邮件彻底停发
///（含原 5000 金币 + 2000 钻石那封编辑器/Development 包测试邮件），本类只剩收尾清理：
/// 存量档里的旧测试邮件进主界面自动消失。清理六类：
/// ① 发件人「开发组」的历史福利邮件；② 升星补给（starup_test_mail_v1 / starup_test_mail_v2_*）；
/// ③ 标题「猎手长弓补偿」（发信未写固定 id，按标题精确匹配）；④ 旧版欢迎信 welcome_2026_v3；
/// ⑤ 补偿礼包 compensation_2026_10_v1；⑥ 开发测试邮件 dev_test_mail_2026_11_25（原仅编辑器/开发包发，现停发）。
/// 溢出补偿、竞技场结算、碎片溢出、看广告送佣兵等正式功能邮件不清理。
/// 🔴 等存量测试档清完（或正式上线前），可连同 TownSceneBootstrap 调用行一起整体删除本类。
/// </summary>
public static class DevGiftMail
{
    /// <summary>历史测试邮件的发件人标识，同时也是清理判据（旧【开发福利】各封同为此发件人）。</summary>
    const string SenderId = "开发组";

    /// <summary>里程弓补偿邮件标题（2026-11-02 发信未写固定 id，只能按标题精确匹配；正式邮件无此标题不误删）。</summary>
    const string MileageBowMailTitle = "猎手长弓补偿";

    /// <summary>开发测试邮件固定 id（原编辑器/开发包去重发放用，现仅作清理判据：各包一律清掉存量）。</summary>
    const string DevTestMailId = "dev_test_mail_2026_11_25";

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
    /// 清掉收件箱里所有历史测试 / 停发邮件（六类判据见类注释），只保留欢迎信 / 溢出补偿 / 竞技场结算等正式邮件。
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
            // 【2026-11-27】开发测试邮件彻底停发 → 各包（含编辑器/开发包）一律清存量，不再区分编译目标。
            bool hit = m.id == DevTestMailId
                || m.sender == SenderId
                || m.id == "starup_test_mail_v1"
                || (m.id != null && m.id.StartsWith("starup_test_mail_v2_"))
                || m.title == MileageBowMailTitle
                || m.id == "welcome_2026_v3"
                || m.id == "compensation_2026_10_v1";
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
