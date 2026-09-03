using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵花名册：设计文档 V1.0 面板 / 成长 / 职业 / 默认技能。
/// 战斗用 AssetId（如 fashi101）查表；冒险日志用 H 编号。
/// </summary>
public static class MercRosterDefs
{
    /// <summary>佣兵稀有度。与装备的全局 <c>Rarity</c> 取值不同，勿混用。</summary>
    public enum MercRarity
    {
        Common = 0,
        Rare = 1,
        Legendary = 2
    }

    public struct Def
    {
        public string HireId;
        public string Name;
        public string Nickname;
        public string AssetId;
        public string JobName;
        public MercRarity Rarity;
        public float Growth;
        public float BaseHp;
        public float BaseAtk;
        public float BaseDef;
        public float AtkSpeed;
        public float MoveSpeed;
        public string ActiveSkillId;
        public string PassiveSkillId;
        /// <summary>兼容旧调用：返回主动技，无则被动。</summary>
        public string DefaultSkillId => !string.IsNullOrEmpty(ActiveSkillId) ? ActiveSkillId : PassiveSkillId;
        public int RecruitGold;
        public bool InInitialPool;
        /// <summary>酒馆刷出/打开时说话（吐槽、自荐）。</summary>
        public string RecruitLine;
        /// <summary>上局跟过玩家时优先用的互动句。</summary>
        public string LastRunLine;
        /// <summary>战斗结算本场最佳 Toast。</summary>
        public string MvpLine;
    }

    static readonly Def[] Table =
    {
        D("H001", "马库斯", "老盾", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 220, 18, 30, 0.90f, 3.2f, null, "SK006", 0, true,
            "盾还在，人就在。有活吗？", "上回那刀我接着了。还要盾吗？", "本场最佳？盾挡得住就算赢。"),
        D("H002", "洛恩", "铁皮", "dunbing102", "剑盾卫士", MercRarity.Rare, 1.15f, 240, 22, 35, 0.95f, 3.3f, "SK007", null, 1500, true,
            "盔甲擦亮了，别撞树就行。", "上回盾举太高？这回我低头。", "看见没？盾举对了！"),
        D("H003", "塔克", "重盾", "dunbing201", "剑盾卫士", MercRarity.Rare, 1.15f, 280, 20, 40, 0.80f, 2.8f, "SK008", null, 1800, false,
            "先说好——回城得有饭。", "上回没吃饱。这趟加量。", "累是累，但没人推得动我。"),
        D("H004", "维克", "钢盾", "dunbing202", "剑盾卫士", MercRarity.Legendary, 1.30f, 300, 28, 48, 1.00f, 3.2f, "SK010", "SK009", 5000, false,
            "……有活就招。", "……上回活着回来了。还跟？", "活着回来就够了。"),
        D("H005", "米娅", "小红", "gongshou101", "游侠", MercRarity.Common, 1.00f, 120, 32, 12, 1.50f, 4.2f, null, "SK002", 500, true,
            "百步穿杨？包在我身上！", "上回射到盾上那箭——这次绝对不偏！", "箭都准！……盾不算靶子啦。"),
        D("H006", "希尔", "鹰眼", "gongshou201", "游侠", MercRarity.Rare, 1.15f, 135, 38, 14, 1.40f, 4.0f, "SK001", null, 2000, false,
            "少废话，指方向。", "上回目标清了。还缺眼力？", "目标清除。下个。"),
        D("H007", "布罗克", "大锤", "kuangzhan101", "狂战士", MercRarity.Common, 1.00f, 180, 35, 18, 1.00f, 3.5f, null, "SK002", 600, true,
            "斧子渴了！带我去砸！", "上回招式名还没喊完。再来！", "天崩地裂——嗯，这招名我刚编的！"),
        D("H008", "古恩", "斩铁", "kuangzhan102", "狂战士", MercRarity.Rare, 1.15f, 200, 42, 20, 0.95f, 3.4f, "SK003", null, 1600, true,
            "有壳的先让开。", "上回壳切完了。还有硬的？", "重甲？切完了。"),
        D("H009", "莫丁", "碎岩", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 220, 40, 24, 1.00f, 3.3f, "SK001", null, 1900, false,
            "像矿就砸。走。", "上回当矿砸的……还挺爽。再下？", "这趟矿脉……哦，是怪。砸爽了。"),
        D("H010", "凯恩", "狂牙", "kuangzhan202", "狂战士", MercRarity.Legendary, 1.30f, 250, 50, 22, 1.10f, 3.8f, "SK005", "SK004", 6000, false,
            "嗷——谁要冲锋？", "嗷，上回不是怕。这回还跟你。", "嗷呜！……不是怕，是庆祝。"),
        D("H011", "索菲", "小白", "naima101", "牧师", MercRarity.Common, 1.00f, 100, 15, 10, 1.20f, 3.6f, null, "SK012", 500, true,
            "受伤了喊我，别硬扛哦。", "上回你又硬扛。这次早点喊我。", "奶量在线！下次别挡我念咒。"),
        D("H012", "塞拉", "小蓝", "naima102", "水系法师", MercRarity.Rare, 1.15f, 110, 28, 20, 1.00f, 3.3f, "SK011", null, 1700, false,
            "别贴我太近。……夏天除外。", "上回别贴那么近。冰还在。", "冰住了。少吵。"),
        D("H013", "莫娜", "紫晶", "naima201", "雷系法师", MercRarity.Rare, 1.15f, 120, 30, 24, 1.00f, 3.2f, "SK013", null, 2000, false,
            "问题少问，酒可以多。", "上回问题还是多。酒呢？", "闪完了。别鼓掌。"),
        D("H014", "伊芙", "火舞", "naima202", "火系法师", MercRarity.Legendary, 1.30f, 125, 48, 12, 1.20f, 3.6f, "SK020", "SK019", 8000, false,
            "袍角别踩。跟紧。", "上回袍角又差点被踩。长记性。", "烧干净了。说谢谢。"),
        D("H015", "艾拉", "风羽", "gongshou101", "游侠", MercRarity.Common, 1.00f, 115, 30, 10, 1.60f, 4.3f, null, "SK002", 500, true,
            "那支羽箭还留着……先出任务。", "上回风顺。这趟还跟你。", "风顺。箭也顺。"),
        D("H016", "杜娅", "怒角", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 200, 40, 20, 0.95f, 3.4f, "SK003", null, 1600, false,
            "护腕绑好了。开干。", "上回护腕没松。再来一趟。", "旋风过完——下一个！"),
        D("H017", "莉娜", "圣光", "naima102", "牧师", MercRarity.Rare, 1.15f, 110, 18, 12, 1.20f, 3.5f, "SK011", null, 1700, false,
            "炖菜……咳，祷告准备好了。", "上回配方生效了。还要加菜？", "配方生效！你们还活着真好。"),
        D("H018", "布朗", "铁壁", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 210, 17, 28, 0.85f, 3.0f, null, "SK006", 700, true,
            "报销单还在，命也硬。", "上回单没丢。人更硬。", "盾在，单在，人都在。"),
        D("H019", "艾琳", "星火", "fashi101", "法师", MercRarity.Common, 1.00f, 105, 34, 8, 1.35f, 3.6f, null, "SK017", 600, true,
            "杖尖有点烫……我能控住！", "上回魔力溢出来了。这次更华丽。", "魔力溢出？那叫华丽！"),
        D("H020", "凯尔", "谜面", "fashi102", "法师", MercRarity.Rare, 1.15f, 115, 42, 10, 1.30f, 3.5f, "SK018", null, 1800, false,
            "……点过酒了。有活？", "……上回谜底你看见了。再来？", "谜底：我赢了。"),
        D("H021", "格拉克斯", "懒鬼", "zhongzhan101", "重武者", MercRarity.Common, 1.00f, 230, 20, 26, 0.80f, 2.9f, null, "SK002", 800, true,
            "哈欠……行吧，挪一步。", "上回累死了。这趟再偷会儿懒。", "累死了……但我出力了。"),
        D("H022", "索尔", "铁面", "zhongzhan201", "重武者", MercRarity.Rare, 1.15f, 260, 26, 32, 0.85f, 2.8f, "SK003", null, 2200, false,
            "铁面到位。有命令？", "上回阵地守住了。还要铁面？", "阵地守住了。收工。"),
    };

    static Dictionary<string, Def> _byHire;
    static Dictionary<string, Def> _byAsset;

    static Def D(
        string hireId, string name, string nick, string asset, string job,
        MercRarity rarity, float growth, float hp, float atk, float def,
        float atkSpd, float move, string activeSkill, string passiveSkill, int gold, bool initial,
        string recruitLine, string lastRunLine, string mvpLine)
    {
        return new Def
        {
            HireId = hireId,
            Name = name,
            Nickname = nick,
            AssetId = asset,
            JobName = job,
            Rarity = rarity,
            Growth = growth,
            BaseHp = hp,
            BaseAtk = atk,
            BaseDef = def,
            AtkSpeed = atkSpd,
            MoveSpeed = move,
            ActiveSkillId = activeSkill,
            PassiveSkillId = passiveSkill,
            RecruitGold = gold,
            InInitialPool = initial,
            RecruitLine = recruitLine,
            LastRunLine = lastRunLine,
            MvpLine = mvpLine
        };
    }

    static void Ensure()
    {
        if (_byHire != null) return;
        _byHire = new Dictionary<string, Def>();
        _byAsset = new Dictionary<string, Def>();
        for (int i = 0; i < Table.Length; i++)
        {
            var d = Table[i];
            _byHire[d.HireId] = d;
            // 同 AssetId 保留首次（主角色）面板
            if (!_byAsset.ContainsKey(d.AssetId))
                _byAsset[d.AssetId] = d;
        }
    }

    public static IReadOnlyList<Def> All
    {
        get { Ensure(); return Table; }
    }

    public static bool TryGetByHireId(string hireId, out Def def)
    {
        Ensure();
        return _byHire.TryGetValue(hireId ?? "", out def);
    }

    public static bool TryGetByAssetId(string assetId, out Def def)
    {
        Ensure();
        return _byAsset.TryGetValue(assetId ?? "", out def);
    }

    public static string GetJobName(string assetId)
    {
        return TryGetByAssetId(assetId, out var d) ? d.JobName : null;
    }

    public static string GetDefaultSkillId(string assetId)
    {
        return TryGetByAssetId(assetId, out var d) ? d.DefaultSkillId : null;
    }

    public static void GetSkillIds(string assetId, out string activeId, out string passiveId)
    {
        activeId = passiveId = null;
        if (TryGetByAssetId(assetId, out var d))
        {
            activeId = d.ActiveSkillId;
            passiveId = d.PassiveSkillId;
        }
    }

    public static string GetRecruitLine(string hireId) => GetAppearLine(hireId);

    public static string GetAppearLine(string hireId)
    {
        if (TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.RecruitLine))
            return d.RecruitLine;
        string nick = !string.IsNullOrEmpty(d.Nickname) ? d.Nickname : (hireId ?? "佣兵");
        return $"{nick}：有活吗？";
    }

    public static string GetLastRunLine(string hireId)
    {
        if (TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.LastRunLine))
            return d.LastRunLine;
        return null;
    }

    /// <summary>上局跟过则优先 LastRunLine，否则刷出自荐/吐槽。</summary>
    public static string PickTavernAppearLine(string hireId, bool wasInLastRun)
    {
        if (wasInLastRun)
        {
            string last = GetLastRunLine(hireId);
            if (!string.IsNullOrEmpty(last)) return last;
        }
        return GetAppearLine(hireId);
    }

    public static string GetMvpLine(string hireId)
    {
        if (TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.MvpLine))
            return d.MvpLine;
        string nick = !string.IsNullOrEmpty(d.Nickname) ? d.Nickname : (hireId ?? "佣兵");
        return $"{nick}：这趟还行。";
    }

    /// <summary>可出现在酒馆形象池的 AssetId（去重）。</summary>
    public static List<string> GetHireableAssetIds()
    {
        Ensure();
        var list = new List<string>();
        var seen = new HashSet<string>();
        for (int i = 0; i < Table.Length; i++)
        {
            string id = Table[i].AssetId;
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
            list.Add(id);
        }
        return list;
    }

    /// <summary>
    /// 按等级套用成长系数：先乘稀有度成长，再按等级抬血/攻击
    /// </summary>
    public static void ApplyCombatStats(string assetId, int level, out float hp, out float atk, out float def, out float atkSpeed, out float moveSpeed, out float atkRange)
    {
        level = Mathf.Max(1, level);
        if (!TryGetByAssetId(assetId, out var d))
        {
            hp = atk = def = 0f;
            atkSpeed = moveSpeed = atkRange = 0f;
            return;
        }

        float g = Mathf.Max(0.5f, d.Growth);
        float baseHp = d.BaseHp * g;
        float baseAtk = d.BaseAtk * g;
        float baseDef = d.BaseDef * g;
        float hpMul = 1f + (level - 1) * 0.1f;
        float atkAdd = (level - 1) * 2f * g;
        hp = baseHp * hpMul;
        atk = baseAtk + atkAdd;
        def = baseDef;
        // 设计表攻速≈攻击频率系数；移速为相对值（基准为 3.5），换算到世界单位
        atkSpeed = Mathf.Max(0.2f, d.AtkSpeed);
        const float designMoveRef = 3.5f;
        moveSpeed = d.MoveSpeed > 0.1f
            ? GameConfig.BASE_MOVE_SPEED * (d.MoveSpeed / designMoveRef)
            : GameConfig.BASE_MOVE_SPEED;
        atkRange = ResolveRange(assetId);
    }

    public static float ResolveRange(string assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return GameConfig.RangeSword;
        if (assetId.StartsWith("gongshou")) return GameConfig.RangeBow;
        if (assetId.StartsWith("naima") || assetId.StartsWith("fashi") || assetId.StartsWith("mushi"))
            return GameConfig.RangeStaff;
        if (assetId.StartsWith("zhongzhan") || assetId.StartsWith("qita"))
            return GameConfig.RangePolearm;
        return GameConfig.RangeSword;
    }
}
