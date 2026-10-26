using System;
using System.Collections.Generic;

/// <summary>
/// 佣兵好感系统层（2026-10-26 主人拍板「佣兵好感界面」，预制体 Resources/Prefabs/Town/佣兵好感.prefab）。
///
/// 单一出口原则：
/// - 好感值本体 = SaveData.mercTrust（与酒馆闲话 +1 共用一套成长，加值只走 SaveData.AddMercTrust）。
/// - 每日对话（2026-10-26 晚拍板修订）：每名佣兵每天 <see cref="MercFavorDefs.TalksPerDay"/> 次机会、
///   一次选一个答案；分值不展示——每天轮换一个「特殊答案」（+<see cref="MercFavorDefs.SpecialFavor"/>），
///   其余为普通答案（+<see cref="MercFavorDefs.NormalFavor"/>）；跨天按本地日历日 0 点清零
///   （日键 <c>SaveData.favorTalkDayKey</c>，已聊记录 = "hireId:序号"，一次一条）。
/// - 送礼：扣 <c>SaveData.favorGifts</c> 加好感（礼物表 <see cref="MercFavorDefs.Gifts"/>）。
/// - 羁绊地图：好感满 <see cref="MercFavorDefs.FavorMax"/> 解锁；通关标记 <c>SaveData.favorBondClearedIds</c>
///   （二期专属双人关结算时写，属性乘区 ×1.5 也随二期挂）。
/// </summary>
public static class MercFavorSystem
{
    /// <summary>该佣兵当前好感值（唯一读口）。</summary>
    public static int GetFavor(SaveData d, string hireId)
        => d != null ? d.GetMercTrust(hireId) : 0;

    /// <summary>好感是否已满（羁绊地图解锁线）。</summary>
    public static bool IsBondOpen(SaveData d, string hireId) => GetFavor(d, hireId) >= MercFavorDefs.FavorMax;

    /// <summary>羁绊地图是否已通关（一次性）。</summary>
    public static bool IsBondCleared(SaveData d, string hireId) => d != null && d.IsMercBondCleared(hireId);

    // —— 每日对话 ——

    /// <summary>跨天清零已聊记录（UI 打开/刷新前调用一次；照 TavernGossipLeft 的懒检查口径）。</summary>
    public static void EnsureTalkDay(SaveData d)
    {
        if (d == null) return;
        string today = TodayKey().ToString();   // 日键 = yyyyMMdd 字符串（存档字段同格式）
        if (d.favorTalkDayKey != today)
        {
            d.favorTalkDayKey = today;
            d.favorTalkedToday = new HashSet<string>();
        }
    }

    /// <summary>该佣兵今天已聊次数（0..TalksPerDay）。</summary>
    public static int TalkedCount(SaveData d, string hireId)
    {
        if (d == null || string.IsNullOrEmpty(hireId)) return 0;
        EnsureTalkDay(d);
        string prefix = hireId + ":";
        int n = 0;
        foreach (var id in d.favorTalkedToday)
        {
            if (id != null && id.StartsWith(prefix)) n++;
        }
        return n;
    }

    /// <summary>今天还能不能聊（每佣兵每天 TalksPerDay 次）。</summary>
    public static bool CanTalk(SaveData d, string hireId)
        => TalkedCount(d, hireId) < MercFavorDefs.TalksPerDay;

    /// <summary>
    /// 今天的「特殊答案」在哪个槽（0..2）：按 天+佣兵 确定性轮换，同一天内不变、跨天换位，
    /// 玩家无法靠记位置刷分，只能猜哪个答案最对佣兵胃口。
    /// </summary>
    public static int SpecialSlotOf(SaveData d, string hireId)
    {
        int h = 0;
        for (int i = 0; i < hireId.Length; i++) h = h * 31 + hireId[i];
        return (Math.Abs(TodayKey() + h)) % MercFavorDefs.DialogueOptionCount;
    }

    /// <summary>指定答案槽本次加多少好感（特殊槽 = SpecialFavor，其余 = NormalFavor）。</summary>
    public static int FavorOfSlot(SaveData d, string hireId, int slot)
        => slot == SpecialSlotOf(d, hireId) ? MercFavorDefs.SpecialFavor : MercFavorDefs.NormalFavor;

    /// <summary>
    /// 执行一次对话：加好感 + 记一次（2026-10-26 晚口径：每天 3 次、一次选一个）。
    /// 返回实际加的好感；-1 = 今天次数已用完（不可执行）。
    /// 【2026-10-26 主人拍板「对话完可以随机获得」】当日第 3 次聊完时随机回 1 个礼物
    /// （grantedGiftId 带出，Toast 由 GrantRandomGift 自己弹；无回礼 = null）。
    /// 调用方负责存档（SaveSystem.Instance.Save()）与刷新 UI。
    /// </summary>
    public static int Talk(SaveData d, string hireId, int slot, out string grantedGiftId)
    {
        grantedGiftId = null;
        if (!CanTalk(d, hireId)) return -1;
        if (slot < 0 || slot >= MercFavorDefs.DialogueOptionCount) return -1;

        d.favorTalkedToday.Add(TalkKey(hireId, TalkedCount(d, hireId)));
        int gain = d.AddMercTrust(hireId, FavorOfSlot(d, hireId, slot));

        // 今日 3 次全部聊完 → 随机回礼 1 个（发放口 = GrantRandomGift 唯一出口）
        if (TalkedCount(d, hireId) >= MercFavorDefs.TalksPerDay) grantedGiftId = GrantRandomGift();
        return gain;
    }

    static string TalkKey(string hireId, int ordinal) => hireId + ":" + ordinal;

    // —— 礼物 ——

    /// <summary>
    /// 随机发 1 个礼物（唯一发放口；【2026-10-26 主人拍板】来源 = 每日对话聊完回礼 + 礼物副本掉落）。
    /// 发放 + 落盘 + force Toast。返回礼物 id；null = 存档未就绪（不发不吞，照副本掉落口径）。
    /// </summary>
    public static string GrantRandomGift()
    {
        var granted = GrantRandomGifts(1);
        if (granted == null || granted.Count == 0) return null;
        foreach (var kv in granted) return kv.Key;   // count=1 只有一个
        return null;
    }

    /// <summary>
    /// 随机发 n 个礼物（批量发放唯一出口；礼物副本结算用，一次结算只弹一条 Toast）。
    /// 发放 + 落盘 + force Toast「获得礼物 面包×2、苹果×1」。返回 各礼物发放数（id→数量）；null = 存档未就绪。
    /// </summary>
    public static Dictionary<string, int> GrantRandomGifts(int count)
    {
        if (count <= 0) return null;
        var d = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (d == null) return null;
        var gifts = MercFavorDefs.Gifts;
        var granted = new Dictionary<string, int>();
        for (int i = 0; i < count; i++)
        {
            var def = gifts[UnityEngine.Random.Range(0, gifts.Length)];
            d.AddFavorGift(def.Id, 1);
            granted.TryGetValue(def.Id, out int cur);
            granted[def.Id] = cur + 1;
        }
        SaveSystem.Instance?.Save();
        var sb = new System.Text.StringBuilder("获得礼物 ");
        bool first = true;
        foreach (var kv in granted)
        {
            MercFavorDefs.TryGetGift(kv.Key, out var def);
            if (!first) sb.Append("、");
            sb.Append(def.Name).Append("×").Append(kv.Value);
            first = false;
        }
        GlobalToastUI.Show(sb.ToString(), true);
        return granted;
    }

    /// <summary>
    /// 送礼给指定佣兵：扣 1 个礼物加对应好感（礼物表查不到 id = false，fail closed）。
    /// 返回实际加的好感；-1 = 没有礼物 / 非法 id。
    /// 调用方负责存档与刷新 UI。
    /// </summary>
    public static int GiveGift(SaveData d, string hireId, string giftId)
    {
        if (d == null || string.IsNullOrEmpty(hireId)) return -1;
        if (!MercFavorDefs.TryGetGift(giftId, out var def)) return -1;
        if (!d.ConsumeFavorGift(giftId)) return -1;
        return d.AddMercTrust(hireId, def.Favor);
    }

    static int TodayKey() => int.Parse(DateTime.Now.ToString("yyyyMMdd"));
}
