using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 冒险日志成就 A001–A015：条件达成 → 可领奖（与战斗 AchievementSystem 分离）。
/// 达成时发日志里程；资源奖在日志成就页领取。
/// </summary>
public static class AdventureLogAchievements
{
    public const string ProgressForestKills = "forest_kills";
    public const string ProgressRiftEntries = "rift_entries";
    public const string ProgressLaodunBattles = "laodun_battles";
    public const string ProgressEquipPick = "equip_pick";
    public const string ProgressEnhanced = "enhanced_once";

    public struct AchReward
    {
        public int Gold;
        public int Mats;
        public int Talent;
        public int BackpackSlots;
        /// <summary>体力上限加成。【2026-10-31 主人拍板「不要体力上限+1」后已无成就使用此字段】
        /// 发放分支保留（走 SaveData.staminaBonusMax），将来若真要开，记得 StaminaSystem.Max 会认这个加成。</summary>
        public int StaminaMax;
        public string TitleId;
        public string FrameId;
        public string SkinId;
        public string Label;
    }

    /// <summary>
    /// 【2026-10-31 主人拍板「先改文案不承诺」】成就页对玩家的奖励文案：系统没做出来的奖励一律显示这个，
    /// 不再承诺「背包扩容+1 / 限定头像框 / 皮肤」。发放分支保留（写存档字段），
    /// 等真做出系统再把对应 Label 换回真奖励名即可。
    /// <para>称号（A012）已在 2026-10-31 做完并恢复真文案，见 <see cref="TitleSystem"/>。</para>
    /// </summary>
    public const string NoRewardLabel = "（暂无奖励）";

    /// <summary>背包扩容奖励开关：系统还没做（backpackExtraSlots 只写不读）→ false，汇总 Toast 不提它。
    /// 系统做出来后置 true，并把 A007 的 Label 从 NoRewardLabel 换回「背包扩容+1」。</summary>
    const bool BackpackSlotRewardLive = false;

    public static AchReward GetReward(string id)
    {
        switch (id)
        {
            case "A001": return new AchReward { Gold = 10, Label = "金币×10" };
            case "A002": return new AchReward { Gold = 5, Label = "金币×5" };
            // 【2026-10-17 主人拍板】强化石暂无用途：A003/A008 原本实际发的是分解材料、文案却写强化石，
            // 现统一改成天赋石（发与文案一致，走下方 r.Talent 发放分支）。
            case "A003": return new AchReward { Talent = 3, Label = "天赋石×3" };
            // 【2026-10-31 主人拍板「改发放对齐文案」】A004/A005 当年产出去零只改了代码没改目录文案
            // （目录写 200/100、实发 20/10），现按目录文案把发放改回 200/100。
            case "A004": return new AchReward { Gold = 200, Label = "金币×200" };
            case "A005": return new AchReward { Gold = 100, Label = "金币×100" };
            case "A006": return new AchReward { Talent = 3, Label = "天赋石×3" };
            // 【2026-10-31 主人拍板「先改文案不承诺」】A007 背包扩容+1 / A014 限定头像框 / A015 老盾皮肤：
            // 这三套系统都还没做 —— 存档字段 backpackExtraSlots / unlockedFrameIds / unlockedSkinIds
            // 全工程**只写不读**（背包真实容量走 GameConfig.GetUnlockedBackpackRows + HeroVaultSystem；
            // 玩家头像框走 MercHireSession.PlayerFrameRarity），发出去玩家什么也看不到。
            // 文案先不承诺，发放分支原样保留。（A012 称号已做完，文案已恢复真奖励名。）
            case "A007": return new AchReward { BackpackSlots = 1, Label = NoRewardLabel };
            case "A008": return new AchReward { Talent = 5, Label = "天赋石×5" };
            case "A009": return new AchReward { Gold = 15, Label = "金币×15" };
            // 【2026-10-31 主人拍板「不要体力上限+1」】A010（进裂隙 10 次）原发体力上限 +1，
            // 现改发天赋石 ×5（与 A008/A013 同档；若后续要换别的奖励，只改这一行 + Label）。
            case "A010": return new AchReward { Talent = 5, Label = "天赋石×5" };
            case "A011": return new AchReward { Gold = 50, Label = "金币×50" };
            case "A012": return new AchReward { TitleId = "forest_untouched", Label = "称号「森林无伤者」" };
            case "A013": return new AchReward { Talent = 5, Label = "天赋石×5" };
            case "A014": return new AchReward { FrameId = "frame_nightmare", Label = NoRewardLabel };
            case "A015": return new AchReward { SkinId = "skin_laodun_rust", Label = NoRewardLabel };
            default: return new AchReward { Label = "" };
        }
    }

    public static int GetProgress(string key)
    {
        var dict = SaveSystem.Instance?.Data?.logAchProgress;
        if (dict == null || string.IsNullOrEmpty(key)) return 0;
        return dict.TryGetValue(key, out int n) ? n : 0;
    }

    public static void AddProgress(string key, int add = 1, bool evaluate = true)
    {
        if (string.IsNullOrEmpty(key) || add == 0) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.logAchProgress ??= new Dictionary<string, int>();
        data.logAchProgress.TryGetValue(key, out int cur);
        data.logAchProgress[key] = cur + add;
        if (evaluate) EvaluateAll();
        else SaveSystem.Instance.Save();
    }

    public static void SetProgressAtLeast(string key, int value, bool evaluate = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.logAchProgress ??= new Dictionary<string, int>();
        data.logAchProgress.TryGetValue(key, out int cur);
        if (value <= cur) return;
        data.logAchProgress[key] = value;
        if (evaluate) EvaluateAll();
        else SaveSystem.Instance.Save();
    }

    public static bool IsCompleted(string id)
    {
        var set = SaveSystem.Instance?.Data?.completedLogAchIds;
        return set != null && !string.IsNullOrEmpty(id) && set.Contains(id);
    }

    public static bool IsClaimed(string id)
    {
        var set = SaveSystem.Instance?.Data?.claimedLogAchIds;
        return set != null && !string.IsNullOrEmpty(id) && set.Contains(id);
    }

    public static bool CanClaim(string id) => IsCompleted(id) && !IsClaimed(id);

    public static bool HasUnclaimed()
    {
        var list = AdventureLogCatalog.Achievements;
        for (int i = 0; i < list.Length; i++)
        {
            if (CanClaim(list[i].Id)) return true;
        }
        return false;
    }

    public static bool CheckCondition(string id)
    {
        var data = SaveSystem.Instance?.Data;
        switch (id)
        {
            case "A001": return StoryProgress.TutorialDone;
            case "A002": return GetProgress("evacuated") > 0;
            case "A003": return GetProgress("died") > 0;
            case "A004": return GetProgress(ProgressForestKills) >= 100;
            case "A005": return GetProgress("elite_kill") > 0;
            case "A006": return GetProgress("boss_ch1") > 0 || AdventureLogCatalog.ChapterCleared(1);
            case "A007": return GetProgress(ProgressEquipPick) >= 50
                               || AdventureLogCatalog.AchProgress("equip_collect_50") >= 50
                               || AdventureLogCatalog.AchDone("equip_collect_50");
            case "A008": return GetProgress(ProgressEnhanced) > 0;
            case "A009": return GetProgress("merc_recruited") > 0
                               || AdventureLogCatalog.HasMerc("dunbing101")
                               || AdventureLogCatalog.HasMerc("dunbing102")
                               || AdventureLogCatalog.HasMerc("gongshou101")
                               || AdventureLogCatalog.HasMerc("kuangzhan101")
                               || AdventureLogCatalog.HasMerc("naima101");
            case "A010": return GetProgress(ProgressRiftEntries) >= 10;
            case "A011": return GetProgress("gold_run_10k") > 0;
            case "A012": return GetProgress("perfect_ch1") > 0;
            case "A013": return (data?.ch1BestClearDifficulty ?? -1) >= 1;
            case "A014": return (data?.ch1BestClearDifficulty ?? -1) >= 2;
            case "A015": return GetProgress(ProgressLaodunBattles) >= 20;
            default: return false;
        }
    }

    public static string FormatProgress(string id)
    {
        switch (id)
        {
            case "A004": return $"{Mathf.Min(100, GetProgress(ProgressForestKills))}/100";
            case "A007": return $"{Mathf.Min(50, GetProgress(ProgressEquipPick))}/50";
            case "A010": return $"{Mathf.Min(10, GetProgress(ProgressRiftEntries))}/10";
            case "A015": return $"{Mathf.Min(20, GetProgress(ProgressLaodunBattles))}/20";
            default:
                if (IsCompleted(id)) return "已完成";
                if (IsClaimed(id)) return "已领取";
                return "进行中";
        }
    }

    /// <summary>扫描全部成就，新达成则记完成并发里程、Toast。</summary>
    public static void EvaluateAll()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.completedLogAchIds ??= new HashSet<string>();
        var list = AdventureLogCatalog.Achievements;
        bool any = false;
        for (int i = 0; i < list.Length; i++)
        {
            string id = list[i].Id;
            if (data.completedLogAchIds.Contains(id)) continue;
            if (!CheckCondition(id)) continue;
            data.completedLogAchIds.Add(id);
            AdventureLogMileage.GrantAchievement(id, MileageTier(id));
            any = true;
            // 【2026-10-27 主人拍板】「见习冒险者」（完成新手引导）不再弹达成提示：
            // 它必然在引导收尾瞬间自动达成，toast 会跟「教完了」气泡/每日登录弹窗抢屏（主人截图实锤）。
            // 只静默入账，冒险日志成就页照常可领。
            if (id != "A001")
                UIManager.Instance?.ShowToast($"成就达成：{list[i].Name}（打开冒险日志领取）");
        }
        if (any)
        {
            SaveSystem.Instance.Save();
            RefreshRedDots();
        }
        else
            SaveSystem.Instance.Save();
        TryAutoCompleteSides();
    }

    static void TryAutoCompleteSides()
    {
        var list = AdventureLogCatalog.Side;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            if (!AdventureLogCatalog.SideUnlocked(e)) continue;
            AdventureCodex.CompleteSide(e.Id);
        }
    }

    static int MileageTier(string id)
    {
        // 挑战/社交偏稀有
        if (id == "A012" || id == "A013" || id == "A014" || id == "A015") return 50;
        if (id == "A006" || id == "A007" || id == "A010" || id == "A011") return 20;
        return 10;
    }

    public static bool Claim(string id) => Claim(id, notify: true);

    /// <summary>
    /// 单个领取。<paramref name="notify"/> = false 时不弹单条 Toast —— 批量领取用，
    /// 避免 N 条 Toast 被最后一条汇总覆盖（2026-10-31 主人报「只说了领取了4个成就奖励，没说是什么」）。
    /// </summary>
    public static bool Claim(string id, bool notify)
    {
        if (!CanClaim(id)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.claimedLogAchIds ??= new HashSet<string>();
        if (!data.claimedLogAchIds.Add(id)) return false;

        var r = GetReward(id);
        if (r.Gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, r.Gold, save: false, notify: true);
        if (r.Mats > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, r.Mats, save: false, notify: true);
        if (r.Talent > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, r.Talent, save: false, notify: true);
        if (r.BackpackSlots > 0)
            data.backpackExtraSlots += r.BackpackSlots;
        if (r.StaminaMax > 0)
            data.staminaBonusMax += r.StaminaMax;
        // 称号：走 TitleSystem 唯一出口（解锁 + 按稀有度自动切换头顶展示，规则在那边）。
        // 这里 notify:false —— 单条 / 汇总 Toast 由本类统一弹，别重复刷屏。
        if (!string.IsNullOrEmpty(r.TitleId))
            TitleSystem.Unlock(r.TitleId, notify: false);
        if (!string.IsNullOrEmpty(r.FrameId))
        {
            data.unlockedFrameIds ??= new HashSet<string>();
            data.unlockedFrameIds.Add(r.FrameId);
        }
        if (!string.IsNullOrEmpty(r.SkinId))
        {
            data.unlockedSkinIds ??= new HashSet<string>();
            data.unlockedSkinIds.Add(r.SkinId);
        }

        SaveSystem.Instance.Save();
        RefreshRedDots();
        if (notify)
            UIManager.Instance?.ShowToast($"领取成就奖励：{r.Label}");
        return true;
    }

    /// <summary>
    /// 一键领取。2026-10-31 主人报「只说了领取 4 个成就奖励，没说是什么」：
    /// 旧版逐条 Claim 各弹一条（瞬时被下一条顶掉），最后只剩个数量。现在改成
    /// 逐条静默入账 → 汇总成一条「领到了什么」，同类数量合并（金币/天赋石…），
    /// 称号/头像框/皮肤这类唯一奖励按名字列出。
    /// </summary>
    public static int ClaimAll(out string summary)
    {
        summary = "";
        int n = 0;
        int gold = 0, mats = 0, talent = 0, slots = 0, stamina = 0;
        int none = 0;                 // 领了但系统没做、实际发不出东西的条数（Label = NoRewardLabel）
        var special = new List<string>();

        var list = AdventureLogCatalog.Achievements;
        for (int i = 0; i < list.Length; i++)
        {
            string id = list[i].Id;
            if (!Claim(id, notify: false)) continue;
            n++;
            var r = GetReward(id);
            gold += r.Gold;
            mats += r.Mats;
            talent += r.Talent;
            slots += r.BackpackSlots;
            stamina += r.StaminaMax;
            if (r.Label == NoRewardLabel) { none++; continue; }
            if (!string.IsNullOrEmpty(r.TitleId) || !string.IsNullOrEmpty(r.FrameId)
                || !string.IsNullOrEmpty(r.SkinId))
            {
                string label = string.IsNullOrEmpty(r.Label) ? "限定奖励" : r.Label;
                if (!special.Contains(label)) special.Add(label);
            }
        }
        if (n <= 0) return 0;

        var sb = new StringBuilder();
        sb.Append("领取 ").Append(n).Append(" 个成就奖励：");
        bool first = true;
        void Append(string part)
        {
            if (!first) sb.Append('、');
            sb.Append(part);
            first = false;
        }
        if (gold > 0) Append("金币×" + gold);
        if (mats > 0) Append("分解材料×" + mats);
        if (talent > 0) Append("天赋石×" + talent);
        // 2026-10-31：背包扩容系统未做（backpackExtraSlots 只写不读），先不写进汇总免得骗人；
        // 系统真做出来后把 BackpackSlotRewardLive 置 true，同时把 A007 的 Label 改回真奖励名。
        if (BackpackSlotRewardLive && slots > 0) Append("背包扩容+" + slots);
        if (stamina > 0) Append("体力上限+" + stamina);
        for (int i = 0; i < special.Count; i++) Append(special[i]);
        if (first)
            sb.Append(NoRewardLabel);                       // 一条真东西都没有
        else if (none > 0)
            sb.Append("、另 ").Append(none).Append(" 项暂无奖励");
        summary = sb.ToString();
        return n;
    }

    public static int ClaimAll() => ClaimAll(out _);

    public static void RefreshRedDots()
    {
        // 单一出口：成就页红点只看「成就可领」，里程等级 / 碎片合成走世界页红点。
        // 2026-10-31 主人报「领取后标签上的提示还在」—— 旧版这里用宽口径
        // （成就可领 || 里程可领），成就是领完了但里程等级还能领，标签红点就一直不灭。
        AdventureCodex.RefreshRedDots();
    }

    // ---- 事件钩子 ----

    public static void OnTutorialDone()
    {
        EvaluateAll();
    }

    public static void OnEvacuated(bool tutorial)
    {
        AddProgress("evacuated", 1);
    }

    public static void OnDied()
    {
        AddProgress("died", 1);
    }

    public static void OnRiftEntered()
    {
        AddProgress(ProgressRiftEntries, 1);
    }

    public static void OnMonsterKilled(Monster m, int chapter)
    {
        if (m == null) return;
        if (chapter <= 1)
            AddProgress(ProgressForestKills, 1, evaluate: false);
        if (m.IsEliteWave)
            AddProgress("elite_kill", 1, evaluate: false);
        if (m.IsBossUnit && chapter <= 1)
            AddProgress("boss_ch1", 1, evaluate: false);
        EvaluateAll();
    }

    public static void OnEquipPicked()
    {
        AddProgress(ProgressEquipPick, 1);
    }

    public static void OnEnhanced()
    {
        SetProgressAtLeast(ProgressEnhanced, 1);
    }

    public static void OnMercRecruited()
    {
        SetProgressAtLeast("merc_recruited", 1);
    }

    /// <summary>
    /// 【2026-10-24 主人拍板「商店未开启」】金币消费累计唯一出口（S005 ≥1000 / S010 ≥3000 喂食口）。
    /// 口径：**金币消费**（商店 + 酒馆招募普通佣兵）——商城按钮 2026-10-15 起隐藏（GuildHallUI.HideUnfinishedHallButtons），
    /// 只算商店这两条永远够不着，故把当前唯一活跃的金币出口（酒馆招募）一并计入。
    /// 喂食点：Systems/ShopSystem.Buy（商店扣费结算）与 UI/MercenaryRecruitPopupUI.Actions.TryHireOffer（招募金币扣费）。
    /// 局内抽奖币池（战斗内金币镜像，出局清零）不算——那是另一套账（铁律 33）。
    /// </summary>
    public static void OnShopGoldSpent(long amount)
    {
        if (amount <= 0) return;
        AddProgress(AdventureLogCatalog.ProgressShopSpend,
            (int)System.Math.Min(amount, int.MaxValue), evaluate: false);
    }

    /// <summary>
    /// 【2026-10-24 主人拍板「钩子肯定需要」】佣兵参战计数喂食口（V2.0 落地清单 N-1 欠账）：
    /// S006 米娅(H005) / S007 布罗克(H007) / S008 路加(H011) 的「累计参战 10 次」条件。
    /// 唯一出口：两条入场路径（常规肉鸽局 Systems/RunDraftDirector.RecruitMerc 入队、
    /// 城镇酒馆旧路径 Managers/BattleManager.SpawnMercenaries）都调这里，别处不许直接写进度键。
    /// </summary>
    public static void OnMercBattled(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        string resolved = MercRosterDefs.ResolveHireId(hireId) ?? hireId;
        AddProgress(AdventureLogCatalog.MercBattleKey(resolved), 1, evaluate: false);
    }

    public static void OnLaodunBattled()
    {
        AddProgress(ProgressLaodunBattles, 1);
        // 【2026-10-24 主人拍板「钩子肯定需要」】S015「老盾的盾纹（好感度达到 20）」喂食口：
        // NpcLaoDun 羁绊此前 0 来源、S015 永锁。口径：与老盾并肩每打一场好感 +1（与参战计数同源同拍，
        // 20 场解锁），语义 = 「一起打出来的交情」。若主人要改来源（如酒馆互动）只改这里。
        StoryProgress.AddBond(StoryProgress.NpcLaoDun, 1);
    }

    public static void OnRunGoldPeak(long gold)
    {
        // 2026-09-15 产出去零：10000 → 1000。
        // 去零后单局峰值：第1章满关 ≈690 金，第8章满关 ≈1070 金，困难/噩梦 ×1.35/1.8 才更容易破千。
        // 阈值定 1000 = 「第 5 章困难或第 8 章普通打满一局」，是个需要真打的中期成就，不是白送。
        if (gold >= 1000)
            SetProgressAtLeast("gold_run_10k", 1);
    }

    public static void OnChapterCleared(int chapter, int difficulty, bool perfectNoDamage)
    {
        if (chapter != 1) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (difficulty > data.ch1BestClearDifficulty)
            data.ch1BestClearDifficulty = difficulty;
        if (perfectNoDamage && difficulty == 0)
            SetProgressAtLeast("perfect_ch1", 1, evaluate: false);
        AdventureCodex.CompleteMain("C1Z");
        AdventureCodex.CompleteMain("C1G");
        EvaluateAll();
    }
}
