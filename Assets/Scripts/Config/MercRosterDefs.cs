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
    }

    static readonly Def[] Table =
    {
        D("H001", "马库斯", "老盾", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 220, 18, 30, 0.90f, 3.2f, null, "SK006", 0, true),
        D("H002", "洛恩", "铁皮", "dunbing102", "剑盾卫士", MercRarity.Rare, 1.15f, 240, 22, 35, 0.95f, 3.3f, "SK007", null, 1500, true),
        D("H003", "塔克", "重盾", "dunbing201", "剑盾卫士", MercRarity.Rare, 1.15f, 280, 20, 40, 0.80f, 2.8f, "SK008", null, 1800, false),
        D("H004", "维克", "钢盾", "dunbing202", "剑盾卫士", MercRarity.Legendary, 1.30f, 300, 28, 48, 1.00f, 3.2f, "SK010", "SK009", 5000, false),
        D("H005", "米娅", "小红", "gongshou101", "游侠", MercRarity.Common, 1.00f, 120, 32, 12, 1.50f, 4.2f, null, "SK002", 500, true),
        D("H006", "希尔", "鹰眼", "gongshou201", "游侠", MercRarity.Rare, 1.15f, 135, 38, 14, 1.40f, 4.0f, "SK001", null, 2000, false),
        D("H007", "布罗克", "大锤", "kuangzhan101", "狂战士", MercRarity.Common, 1.00f, 180, 35, 18, 1.00f, 3.5f, null, "SK002", 600, true),
        D("H008", "古恩", "斩铁", "kuangzhan102", "狂战士", MercRarity.Rare, 1.15f, 200, 42, 20, 0.95f, 3.4f, "SK003", null, 1600, true),
        D("H009", "莫丁", "碎岩", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 220, 40, 24, 1.00f, 3.3f, "SK001", null, 1900, false),
        D("H010", "凯恩", "狂牙", "kuangzhan202", "狂战士", MercRarity.Legendary, 1.30f, 250, 50, 22, 1.10f, 3.8f, "SK005", "SK004", 6000, false),
        D("H011", "索菲", "小白", "naima101", "牧师", MercRarity.Common, 1.00f, 100, 15, 10, 1.20f, 3.6f, null, "SK012", 500, true),
        D("H012", "塞拉", "小蓝", "naima102", "水系法师", MercRarity.Rare, 1.15f, 110, 28, 20, 1.00f, 3.3f, "SK011", null, 1700, false),
        D("H013", "莫娜", "紫晶", "naima201", "雷系法师", MercRarity.Rare, 1.15f, 120, 30, 24, 1.00f, 3.2f, "SK013", null, 2000, false),
        D("H014", "伊芙", "火舞", "naima202", "火系法师", MercRarity.Legendary, 1.30f, 125, 48, 12, 1.20f, 3.6f, "SK020", "SK019", 8000, false),
        D("H015", "艾拉", "风羽", "gongshou101", "游侠", MercRarity.Common, 1.00f, 115, 30, 10, 1.60f, 4.3f, null, "SK002", 500, true),
        D("H016", "杜娅", "怒角", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 200, 40, 20, 0.95f, 3.4f, "SK003", null, 1600, false),
        D("H017", "莉娜", "圣光", "naima102", "牧师", MercRarity.Rare, 1.15f, 110, 18, 12, 1.20f, 3.5f, "SK011", null, 1700, false),
        D("H018", "布朗", "铁壁", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 210, 17, 28, 0.85f, 3.0f, null, "SK006", 700, true),
        D("H019", "艾琳", "星火", "fashi101", "法师", MercRarity.Common, 1.00f, 105, 34, 8, 1.35f, 3.6f, null, "SK017", 600, true),
        D("H020", "凯尔", "谜面", "fashi102", "法师", MercRarity.Rare, 1.15f, 115, 42, 10, 1.30f, 3.5f, "SK018", null, 1800, false),
        D("H021", "格拉克斯", "懒鬼", "zhongzhan101", "重武者", MercRarity.Common, 1.00f, 230, 20, 26, 0.80f, 2.9f, null, "SK002", 800, true),
        D("H022", "索尔", "铁面", "zhongzhan201", "重武者", MercRarity.Rare, 1.15f, 260, 26, 32, 0.85f, 2.8f, "SK003", null, 2200, false),
    };

    static Dictionary<string, Def> _byHire;
    static Dictionary<string, Def> _byAsset;

    static Def D(
        string hireId, string name, string nick, string asset, string job,
        MercRarity rarity, float growth, float hp, float atk, float def,
        float atkSpd, float move, string activeSkill, string passiveSkill, int gold, bool initial)
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
            InInitialPool = initial
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
