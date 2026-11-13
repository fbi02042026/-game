using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冒险日志里程：收集解锁发点 → Lv1–6 一次性等级奖。
/// 与战斗 AchievementSystem 成就点数分离；成就完成时折算进本系统。
/// </summary>
public static class AdventureLogMileage
{
    public const int MaxLevel = 6;

    /// <summary>各等级所需累计点数（Lv1=0）。</summary>
    public static readonly int[] LevelThresholds = { 0, 100, 300, 600, 1000, 1500 };

    public struct LevelReward
    {
        public int Gold;
        public int Diamond;
        public int TalentPoint;
        public string TitleId;
        public string Label;
    }

    /// <summary>Lv2–6 可领；Lv1 为起点无奖。【2026-10-20 主人拍板】钻石停发：Diamond 字段保留但一律 0，预览/发放自动跳过。</summary>
    public static readonly LevelReward[] LevelRewards =
    {
        new LevelReward { Label = "见习记载", Gold = 0 },
        new LevelReward { Label = "森林行者", Gold = 50, Diamond = 0 },   // 原 Diamond = 1
        // 【2026-10-17 主人拍板】强化石暂无用途 → 原 Lv3 的附魔石×3 改成天赋石×3（1:1 换资源名，数量不动）
        new LevelReward { Label = "探索者", Gold = 100, TalentPoint = 3 },
        new LevelReward { Label = "裂隙行者", Gold = 200, Diamond = 0 },  // 原 Diamond = 3
        new LevelReward { Label = "日志学者", Gold = 300, TalentPoint = 2 },
        new LevelReward { Label = "传说记录者", Gold = 500, Diamond = 0, TalentPoint = 3, TitleId = "log_chronicler" }  // 原 Diamond = 5
    };

    public static int Points => SaveSystem.Instance?.Data?.logMileagePoints ?? 0;

    /// <summary>
    /// 【2026-10-20 主人拍板】累计获得点数 = 等级唯一依据。
    /// Points 是可消费钱包（商店花钱只扣它）；等级/进度一律看这里，消费不掉级、不锁已达标等级奖。
    /// </summary>
    public static int TotalEarned => SaveSystem.Instance?.Data?.logMileageTotalEarned ?? 0;

    public static int GetLevel()
    {
        int pts = TotalEarned;
        int lv = 1;
        for (int i = 1; i < LevelThresholds.Length; i++)
        {
            if (pts >= LevelThresholds[i])
                lv = i + 1;
            else
                break;
        }
        return Mathf.Clamp(lv, 1, MaxLevel);
    }

    public static int NextThreshold()
    {
        int lv = GetLevel();
        if (lv >= MaxLevel) return LevelThresholds[MaxLevel - 1];
        return LevelThresholds[lv];
    }

    public static int PointsIntoCurrentSpan(out int spanNeed)
    {
        int pts = TotalEarned;   // 等级进度按累计算，消费不影响（2026-10-20 主人拍板）
        int lv = GetLevel();
        if (lv >= MaxLevel)
        {
            spanNeed = 0;
            return 0;
        }
        int lo = LevelThresholds[lv - 1];
        int hi = LevelThresholds[lv];
        spanNeed = hi - lo;
        return Mathf.Clamp(pts - lo, 0, spanNeed);
    }

    public static bool IsLevelClaimed(int level)
    {
        var set = SaveSystem.Instance?.Data?.claimedLogMileageLevels;
        return set != null && set.Contains(level);
    }

    public static bool CanClaimLevel(int level)
    {
        if (level < 2 || level > MaxLevel) return false;
        if (GetLevel() < level) return false;
        return !IsLevelClaimed(level);
    }

    public static bool HasUnclaimedLevel()
    {
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            if (CanClaimLevel(lv)) return true;
        }
        return false;
    }

    public static int FirstClaimableLevel()
    {
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            if (CanClaimLevel(lv)) return lv;
        }
        return 0;
    }

    // notify=false：批量领取时不弹窗，由 ClaimAllAvailable 汇总弹一次（2026-11-12 主人拍板统一领奖弹窗）
    public static bool ClaimLevel(int level, bool notify = true)
    {
        if (!CanClaimLevel(level)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;

        // 【2026-10-20 主人报玩家反馈「里程商店法杖领取不了、没有兑换成功提示」→ 修】
        // 旧顺序：先把 Lv2 记为已领 → 再发杖 → 发放失败（背包未就绪 / 穿戴失败）被静默吞掉
        //（TryGrantTwilightStaff 内 showToast:false 压掉了失败 Toast，背包未就绪只 LogWarning），
        // 且 claimed 已写入并 Save → 玩家永久丢杖、界面无任何成功提示。
        // 改为「先发货、后记账」：发放失败不入账、不保存，玩家可重试；失败必须可见（LogError + Toast）。
        if (level == 2 && !SpecialWeapons.TryGrantTwilightStaff(showToast: false))
        {
            Debug.LogError("[Mileage] Lv2 暮火之杖发放失败（背包未就绪或穿戴失败），本次领取不入账，玩家可重试");
            UIManager.Instance?.ShowToast("奖励发放失败，请稍后重试");
            return false;
        }

        data.claimedLogMileageLevels ??= new HashSet<int>();
        if (!data.claimedLogMileageLevels.Add(level)) return false;

        var reward = LevelRewards[level - 1];
        if (reward.Gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, reward.Gold, save: false, notify: true);
        if (reward.Diamond > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, reward.Diamond, save: false, notify: true);
        if (reward.TalentPoint > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, reward.TalentPoint, save: false, notify: true);
        if (!string.IsNullOrEmpty(reward.TitleId))
        {
            data.logMileageTitleId = reward.TitleId;          // 老字段保留（旧档/文案引用）
            // 2026-10-31 称号系统：走唯一出口解锁 + 按稀有度自动切换战斗头顶展示（Lv6 是传说档，
            // 拿到就会顶掉之前的稀有称号）。
            // notify:true —— 里程领取本身没有「领到了什么」的汇总 Toast，静默发放玩家会完全不知道
            // 自己拿到了称号（Lv2 那把杖有专属弹窗，Lv6 没有）。
            TitleSystem.Unlock(reward.TitleId, notify: true);
        }

        // 设计：里程 Lv2「森林行者」可领暮火之杖（与教程发放共用防重 key）。
        // 领取即写入存档（防弹窗期间杀进程丢杖，杖已在上方发放成功），随后弹告知窗
        // 「获得稀有·暮火之杖 + 将替换法师职业初始武器」，玩家点确认关闭；
        // 法师开局替换见 PlayerJobDefs.ApplyForBattle（HasTwilightStaffOwned 判定）。
        // 【2026-10-20 主人拍板「这个界面用通用弹窗，并且这个界面点确认无反应」】
        // 旧实现在 UIManager.ShowTwilightStaffConfirm 里手搓（自建嵌套 Canvas 没补 GraphicRaycaster
        // → EventSystem 射线收不到这个 Canvas 下的图形，点确认毫无反应；外观也是自己画的圆角块）。
        // 已整段删除，改走全项目默认弹窗皮（EvacuateConfirmPopup 预制体）+ 单按钮告知模式。
        if (level == 2 && notify)
        {
            string trait = string.IsNullOrEmpty(SpecialWeapons.LastGrantedTraitText)
                ? "见装备详情"
                : SpecialWeapons.LastGrantedTraitText;
            // 【2026-10-21 主人拍板：这个弹窗重新排版】旧版正文 = 一整段左对齐长文案（25 号 / 行距 1.0），
            // 武器名埋在第一行、和后面的说明挤成六行、底边几乎贴确认键。改成分层富文本：
            // ① 武器名单独一行、放大 + 品质蓝（稀有度色唯一出口 RarityPalette，现算 hex，不手抄色值）；
            // ② 随机特性独立一行；③ 空一行；④ 两行补充说明降为灰白，弱化。
            // 弹窗皮本身的居中 / 行距 1.2 / 标题金色由 StaminaConfirmPopup.ApplyUnifiedTextStyle 统一设
            // （2026-10-24 全弹窗统一排版，开窗直设、无需关窗还原）。
            string rareHex = ColorUtility.ToHtmlStringRGB(RarityPalette.Get(Rarity.Rare));
            string noticeBody =
                $"<size=30><color=#{rareHex}>「{SpecialWeapons.InstanceName}」</color></size>\n"
                + $"<size=22>随机特性：{trait}</size>\n"
                + "\n"
                + "<size=21><color=#CFCFCF>实力大增，是时候深入裂隙大显身手了！\n"
                + "此后法师每局开局自动装备它，原初始法杖不再出现。</color></size>";
            StaminaConfirmPopup.ShowNotice(
                "恭喜！获得法师限定初始武器",
                noticeBody,
                "确认",
                () =>
                {
                    UIManager.Instance?.ShowToast($"法师初始武器已替换：{SpecialWeapons.InstanceName}");
                    // 告知窗关掉后再弹通用「获得奖励」窗，两个模态不叠加（2026-11-12 主人拍板）
                    RewardPopupUI.ShowClaimRewards(reward.Gold, 0, reward.TalentPoint, reward.Diamond,
                        new string[] { SpecialWeapons.InstanceName });
                });
        }
        else if (notify)
        {
            // Lv3+：直接弹「获得奖励」窗；称号这类非资源奖励单列一格
            RewardPopupUI.ShowClaimRewards(reward.Gold, 0, reward.TalentPoint, reward.Diamond,
                string.IsNullOrEmpty(reward.TitleId) ? null : new string[] { reward.Label });
        }

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return true;
    }

    public static int ClaimAllAvailable()
    {
        int n = 0;
        long gold = 0, talent = 0, diamond = 0;
        var extras = new List<string>();
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            // 逐条静默入账，最后汇总弹一次「获得奖励」窗（不连弹 N 个）
            if (!ClaimLevel(lv, notify: false)) continue;
            n++;
            var r = LevelRewards[lv - 1];
            gold += r.Gold;
            talent += r.TalentPoint;
            diamond += r.Diamond;
            if (lv == 2) extras.Add(SpecialWeapons.InstanceName);
            else if (!string.IsNullOrEmpty(r.TitleId)) extras.Add(r.Label);
        }
        if (n > 0) RewardPopupUI.ShowClaimRewards(gold, 0, talent, diamond, extras.ToArray());
        return n;
    }

    // =====================================================================
    // 武器里程奖励（2026-10-26 主人拍板：与法师暮火之杖同款「攒够累计点数领一次、
    // 直接穿上」；门槛微微不一样：盾 100）。与等级奖励并行：不走 claimedLogMileageLevels，
    // 防重发走 logMileageGrantedKeys（同暮火之杖 GrantKey 口径）。
    // 【2026-11-02 主人拍板】里程奖励的弓（special:rare_bow / 猎手长弓）整条移除，只保留盾；
    // 盾只在剑盾职业生效（开局重发见 PlayerJobDefs.ApplyForBattle）。
    // =====================================================================

    /// <summary>稀有·钢骨盾的防重发 key（领取写入 logMileageGrantedKeys；剑盾职业开局据此重发）。</summary>
    public const string ShieldGrantKey = "special:rare_shield";

    public struct WeaponMilestone
    {
        public string GrantKey;    // 防重发 key（logMileageGrantedKeys）
        public string TemplateId;  // Config/Equips 模板 id（复用现有掉落模板，发稀有版实例）
        public string Label;       // 列表显示名（不含稀有度前缀；前缀按稀有统一加）
        public int NeedPoints;     // 领取门槛：累计获得点数（TotalEarned，消费不掉）
        // 2026-10-26 主人拍板：占格子机制已移除，里程奖励属性再给高一点——
        // 在模板基础值之上固定叠一条加成（固定值不随星级/稀有度再乘，数值一眼可查；只影响里程发放，不掉落池）
        public AttrType BonusAttr;
        public float BonusAttrValue;
    }

    public static readonly WeaponMilestone[] WeaponMilestones =
    {
        // 盾：模板防御+7（稀有版 ×1.15×星级 ≈ 8~9）+ 固定防御 5 → 总防御约 13~14
        //（2026-10-26 主人拍板：+10 太高压到 5，减 50%）
        new WeaponMilestone { GrantKey = ShieldGrantKey, TemplateId = "equip_new_shield_01", Label = "钢骨盾", NeedPoints = 100,
            BonusAttr = AttrType.Defense, BonusAttrValue = 5f },
        // 【2026-11-02 主人拍板】里程奖励的弓（special:rare_bow / equip_bow_1 / 猎手长弓）整条移除，只保留盾。
    };

    public static bool IsWeaponMilestoneClaimed(string grantKey)
    {
        var keys = SaveSystem.Instance?.Data?.logMileageGrantedKeys;
        return keys != null && keys.Contains(grantKey);
    }

    /// <summary>是否已领稀有·钢骨盾（领取时写入；剑盾职业开局据此替换初始副手，同暮火之杖口径）。</summary>
    public static bool HasShieldMilestoneOwned()
    {
        var keys = SaveSystem.Instance?.Data?.logMileageGrantedKeys;
        return keys != null && keys.Contains(ShieldGrantKey);
    }

    public static bool CanClaimWeaponMilestone(WeaponMilestone m)
    {
        if (string.IsNullOrEmpty(m.GrantKey) || IsWeaponMilestoneClaimed(m.GrantKey)) return false;
        return TotalEarned >= m.NeedPoints;
    }

    /// <summary>
    /// 里程奖励稀有版实例的<b>唯一生成出口</b>（领取 / 剑盾职业开局重发共用，属性口径不分叉）：
    /// 稀有版必须走 forcedRarity 生成 —— 模板底稀有度是 Uncommon，先生成后翻 rarity 的话
    /// 属性仍按 Uncommon（×1.08）算、词条也按 Uncommon（0 条）滚，稀有名不符实；
    /// forcedRarity=Rare：属性 ×1.15、按稀有滚 1 条武器词条（与掉落稀有装同一套生成链），
    /// 再叠加奖励固定加成条，requireLevel 钳到生成等级必能穿上。模板缺失返回 null（LogError 不兜底）。
    /// </summary>
    public static EquipInstance CreateMilestoneInstance(WeaponMilestone m, int heroLevel)
    {
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(m.TemplateId) : null;
        if (tpl == null)
        {
            Debug.LogError($"[Mileage] 武器里程奖励模板缺失：{m.TemplateId}");
            return null;
        }
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, Mathf.Max(1, heroLevel), overrideRarity: true, forcedRarity: Rarity.Rare);
        inst.equipName = WeaponAffixSystem.RarityPrefix(Rarity.Rare) + tpl.equipName;
        inst.templateId = tpl.templateId;
        // requireLevel 钳到生成等级，确保领取 / 开局必能穿上（同暮火之杖口径，防随机到 +1 穿不上）
        inst.requireLevel = Mathf.Min(inst.requireLevel, Mathf.Max(1, heroLevel));
        // 固定加成条（2026-10-26 主人拍板：占格子移除后属性上调；叠加在模板基础值与随机词条之上）
        inst.attrBonus.Add(new AttrBonusData { attrType = m.BonusAttr, value = m.BonusAttrValue, isPercent = false });
        return inst;
    }

    /// <summary>
    /// 生成出战用的稀有·钢骨盾（剑盾职业开局重发用，PlayerJobDefs.ApplyForBattle 调；
    /// 与领取同一生成出口，随机词条随局重掷，照 SpecialWeapons.CreateTwilightStaff 先例）。
    /// 未领取返回 null（不兜底）。
    /// </summary>
    public static EquipInstance CreateShieldBattleInstance(int heroLevel)
    {
        if (!HasShieldMilestoneOwned()) return null;
        for (int i = 0; i < WeaponMilestones.Length; i++)
        {
            if (WeaponMilestones[i].GrantKey != ShieldGrantKey) continue;
            return CreateMilestoneInstance(WeaponMilestones[i], heroLevel);
        }
        Debug.LogError($"[Mileage] ShieldGrantKey（{ShieldGrantKey}）在 WeaponMilestones 里找不到（表被改？）");
        return null;
    }

    /// <summary>
    /// 领武器里程奖励（先发货后记账，同 Lv2 暮火之杖修好的顺序：发放失败不入账、可重试）。
    /// 发稀有版实例并直接穿上（换下的旧件自动分解）；grantedName 供 Toast 展示。
    /// </summary>
    public static bool ClaimWeaponMilestone(int index, out string grantedName)
    {
        grantedName = null;
        if (index < 0 || index >= WeaponMilestones.Length) return false;
        var m = WeaponMilestones[index];
        if (!CanClaimWeaponMilestone(m)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (BattleBagSystem.Instance == null)
        {
            UIManager.Instance?.ShowToast("奖励发放失败，请稍后重试");
            return false;
        }

        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        var inst = CreateMilestoneInstance(m, lv);
        if (inst == null)
        {
            UIManager.Instance?.ShowToast("奖励发放失败，请稍后重试");
            return false;
        }

        if (!BattleBagSystem.Instance.TryEquipDirect(inst))
        {
            UIManager.Instance?.ShowToast($"无法装备「{inst.equipName}」");
            return false;
        }

        data.logMileageGrantedKeys ??= new HashSet<string>();
        data.logMileageGrantedKeys.Add(m.GrantKey);
        grantedName = inst.equipName;
        // 【2026-11-12】装备领奖走「获得奖励」窗，名字上稀有度色 + 框体走唯一出口
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(m.TemplateId) : null;
        RewardPopupUI.ShowClaimEquip(grantedName, tpl != null ? tpl.icon : null, Rarity.Rare);
        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return true;
    }

    /// <summary>防重复发点。返回实际增加点数（0=已发过）。</summary>
    public static int TryGrantOnce(string sourceKey, int points, bool save = true)
    {
        if (string.IsNullOrEmpty(sourceKey) || points <= 0) return 0;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return 0;
        data.logMileageGrantedKeys ??= new HashSet<string>();
        if (!data.logMileageGrantedKeys.Add(sourceKey)) return 0;
        data.logMileagePoints = Mathf.Max(0, data.logMileagePoints + points);
        // 累计获得同步上涨（等级依据），消费不扣这里（2026-10-20 主人拍板：消费不掉级）
        data.logMileageTotalEarned = Mathf.Max(0, data.logMileageTotalEarned + points);
        // 【2026-11-13 主人拍板】每次实际获得里程点数都弹 toast（Plain 原样文案）。
        // 唯一入账出口在此弹：图鉴遇怪/结识佣兵/成就/世界/主线/支线及未来新增来源全覆盖；
        // 防重复（sourceKey 已发过直接 return 0）天然保证不双弹。
        UIManager.Instance?.ShowToast($"里程点数 +{points}");
        if (save) SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return points;
    }

    /// <summary>里程商店消费点数。</summary>
    public static bool TrySpendPoints(int points, bool save = false)
    {
        if (points <= 0) return true;
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.logMileagePoints < points) return false;
        data.logMileagePoints -= points;
        if (save) SaveSystem.Instance.Save();
        return true;
    }

    public static int PointsForMonsterKind(string kind)
    {
        if (string.IsNullOrEmpty(kind)) return 5;
        if (kind.Contains("首领") || kind.Contains("Boss") || kind.Contains("boss"))
            return 20;
        if (kind.Contains("精英"))
            return 10;
        return 5;
    }

    public static int PointsForMercRarity(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 50;
            case MercRosterDefs.MercRarity.Rare: return 20;
            default: return 10;
        }
    }

    public static int PointsForAchievementPoints(int achievementPoints)
    {
        if (achievementPoints >= 50) return 50;
        if (achievementPoints >= 25) return 20;
        return 10;
    }

    public static int GrantMonsterSeen(string catalogId, string kind)
    {
        return TryGrantOnce("monster:" + catalogId, PointsForMonsterKind(kind));
    }

    public static int GrantMercSeen(string catalogId, MercRosterDefs.MercRarity rarity)
    {
        return TryGrantOnce("merc:" + catalogId, PointsForMercRarity(rarity));
    }

    public static int GrantAchievement(string achievementId, int achievementPoints)
    {
        return TryGrantOnce("ach:" + achievementId, PointsForAchievementPoints(achievementPoints));
    }

    public static int GrantWorld(string worldId)
    {
        return TryGrantOnce("world:" + worldId, 5);
    }

    public static int GrantMain(string mainId)
    {
        return TryGrantOnce("main:" + mainId, 15);
    }

    public static int GrantSide(string sideId)
    {
        return TryGrantOnce("side:" + sideId, 20);
    }

    public static string FormatStatusLine()
    {
        int lv = GetLevel();
        int pts = TotalEarned;   // 等级行展示累计进度，不随商店消费回落（2026-10-20 主人拍板）
        string name = LevelRewards[lv - 1].Label;
        if (lv >= MaxLevel)
            return $"日志里程 Lv{lv} {name}  {pts}点（满级）";
        int need = NextThreshold();
        return $"日志里程 Lv{lv} {name}  {pts}/{need}";
    }

    public static string FormatRewardPreview(int level)
    {
        if (level < 1 || level > MaxLevel) return "";
        var r = LevelRewards[level - 1];
        var parts = new List<string>();
        if (r.Gold > 0) parts.Add($"金币×{r.Gold}");
        if (r.Diamond > 0) parts.Add($"钻石×{r.Diamond}");
        if (r.TalentPoint > 0) parts.Add($"天赋石×{r.TalentPoint}");
        if (!string.IsNullOrEmpty(r.TitleId)) parts.Add("称号·传说记录者");
        // 2026-10-11：奖励预览的名字跟实际发到手上的装备同名（带「稀有·」前缀）
        if (level == 2) parts.Add(SpecialWeapons.InstanceName);
        if (parts.Count == 0) return r.Label;
        return r.Label + "：" + string.Join("，", parts);
    }
}
