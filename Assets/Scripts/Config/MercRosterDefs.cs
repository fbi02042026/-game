using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵花名册：设计文档 V1.0 面板 / 成长 / 职业 / 默认技能。
/// 战斗用 AssetId（如 fashi101）查表；冒险日志用 H 编号。
/// 台词见 MercLineTable（merc_lines）。
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
        /// <summary>
        /// true = 只在商店解锁，酒馆名册里不列出。
        /// 2026-09-17 口径：**按稀有度切** —— 普通档进商店（直接挂卖、无门槛），
        /// 稀有 + 传说留在酒馆（好货只在酒馆，带解锁条件）。当前 8 / 14。
        /// </summary>
        public bool SoldInShop;
    }

    // 默认 = 硬编码数组（缺表/解析失败时回退用）；读表成功后整体替换为表数据。
    // 【2026-10-07 主人拍板】稀有度战力差距拉大：原「稀有≈普通 1.2 倍、传说≈普通 1.5 倍」太接近，
    // 主人原话「怎么稀有跟普通攻击差不多 把差距拉大点才能体现传奇的优势 这不谁还买」。
    // 现口径（同职业内 Base 值倍率，最终战力还要再乘 Growth 1.00 / 1.15 / 1.30）：
    //   普通 = 基准
    //   稀有 = 血 ×1.30 / 攻 ×1.45 / 防 ×1.35   → 最终约 攻 1.67×、血 1.50×、防 1.55×
    //   传说 = 血 ×1.70 / 攻 ×2.00 / 防 ×1.85   → 最终约 攻 2.60×、血 2.21×、防 2.41×
    // 改数值必须 CSV（Assets/Data/Source/Tables/merc_roster.csv）+ .bytes + 本数组 三处同值，不许只改一处。
    // 三处是否一致用菜单「Tools → 佣兵 → 花名册三处同值校验」（Assets/Editor/MercRosterConsistencyCheck.cs）跑一次，
    // 不一致会逐行报错。数值真源 = CSV，发现不同一律以 CSV 为准改另外两处。
    // 【2026-10-23 主人拍板】InInitialPool 口径：只有「普通档 + 商店售卖」的 H001/H007/H011/H019 为 true，
    // 其余 18 人一律 false。校验器首跑查出本数组有 6 行（H002/H005/H008/H015/H018/H021）写成 true 与 CSV 不符，已对齐。
    static Def[] Table =
    {
        D("H001", "马库斯", "老盾", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 335.5f,34.6f,39.1f, 0.90f, 3.15f, null, "SK006", 0, true, true),
        D("H002", "洛恩", "铁皮", "dunbing102", "剑盾卫士", MercRarity.Rare, 1.15f, 424.3f,50.1f,56.7f, 0.95f, 3.15f, "SK007", null, 1500, false),
        D("H003", "塔克", "重盾", "dunbing201", "剑盾卫士", MercRarity.Rare, 1.15f, 424.3f,50.1f,56.7f, 0.80f, 3.15f, "SK008", null, 1800, false),
        D("H004", "维克", "钢盾", "dunbing202", "剑盾卫士", MercRarity.Legendary, 1.30f, 563.1f,59.1f,66.9f, 1.00f, 3.15f, "SK010", "SK009", 5000, false, false),
        // 【2026-10-18 主人拍板】「小红/小蓝」这类占位昵称改成知名角色名；真名与其余昵称一律不动，
        // 显示处均带真名前缀（米娅·小樱）。CSV + .bytes + 本数组三处同值。
        D("H005", "米娅", "小樱", "gongshou101", "游侠", MercRarity.Common, 1.00f, 244.8f,34.4f,23.9f, 1.50f, 4.2f, null, "SK002", 500, false, true),
        D("H006", "希尔", "鹰眼", "gongshou201", "游侠", MercRarity.Rare, 1.15f, 309.6f,49.8f,34.6f, 1.40f, 4.2f, "SK001", null, 2000, false, false),
        D("H007", "布罗克", "大锤", "kuangzhan101", "狂战士", MercRarity.Common, 1.00f, 269.5f,34f,24.7f, 1.00f, 4.03f, null, "SK002", 600, true, true),
        D("H008", "古恩", "斩铁", "kuangzhan102", "狂战士", MercRarity.Rare, 1.15f, 340.9f,49.3f,35.7f, 0.95f, 4.03f, "SK003", null, 1600, false),
        D("H009", "莫丁", "碎岩", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 340.9f,49.3f,35.7f, 1.00f, 4.03f, "SK001", null, 1900, false, false),
        D("H010", "凯恩", "狂牙", "kuangzhan202", "狂战士", MercRarity.Legendary, 1.30f, 452.3f,58.2f,42.2f, 1.10f, 4.03f, "SK005", "SK012", 6000, false, false),
        // 【2026-10-08 主人纠正】他是**男的**（见立绘 Assets/Art/UI/Icons/佣兵立绘/佣兵立绘_H011.png：
        //   光头 + 佛珠 + 锡杖 + 僧袍，标准东方僧侣）。H011 早前用过一个女性向的名字，既和这幅立绘、
        //   也和设定文档「初级光头牧师」的记载矛盾 → 由主人定名「路加」（圣路加为医者主保，贴合治疗）。
        // 【教训】给角色定性别 / 改名之前必须先看立绘，不许凭名字凭空猜。
        // 他是完成新手引导后可在酒馆招募的牧师，名字要好记、好认；头像框 / 对白 / 日志现在都显示同一个名字。
        // 【2026-10-08 补记】引导局的那位佣兵已换成 H003 塔克，H011 不再自动入队，只走酒馆招募。
        D("H011", "路加", "路加", "naima101", "牧师", MercRarity.Common, 1.00f, 302.5f,34.4f,25.7f, 1.20f, 3.5f, "SK004", "SK011", 500, true, true),
        D("H012", "塞拉", "蓝之泪", "naima102", "水系法师", MercRarity.Rare, 1.15f, 267.8f,53.2f,32.6f, 1.00f, 3.5f, "SK004", null, 1700, false),
        D("H013", "莫娜", "紫晶", "naima201", "雷系法师", MercRarity.Rare, 1.15f, 267.8f,53.2f,32.6f, 1.00f, 3.5f, "SK013", null, 2000, false, false),
        D("H014", "伊芙", "火舞", "naima202", "火系法师", MercRarity.Legendary, 1.30f, 355.4f,62.8f,38.5f, 1.20f, 3.5f, "SK020", "SK019", 8000, false, false),
        D("H015", "艾拉", "风羽", "gongshou101", "游侠", MercRarity.Common, 1.00f, 244.8f,34.4f,23.9f, 1.60f, 4.2f, null, "SK002", 500, false, true),
        D("H016", "杜娅", "怒角", "kuangzhan201", "狂战士", MercRarity.Rare, 1.15f, 340.9f,49.3f,35.7f, 0.95f, 4.03f, "SK003", null, 1600, false),
        D("H017", "莉娜", "圣光", "naima102", "牧师", MercRarity.Rare, 1.15f, 382.6f,49.8f,37.3f, 1.20f, 3.5f, "SK004", null, 1700, false),
        D("H018", "布朗", "铁壁", "dunbing101", "剑盾卫士", MercRarity.Common, 1.00f, 335.5f,34.6f,39.1f, 0.85f, 3.15f, null, "SK006", 700, false, true),
        D("H019", "艾琳", "星火", "fashi101", "法师", MercRarity.Common, 1.00f, 211.8f,36.7f,22.5f, 1.35f, 3.5f, null, "SK017", 600, true, true),
        D("H020", "凯尔", "谜面", "fashi102", "法师", MercRarity.Rare, 1.15f, 267.8f,53.2f,32.6f, 1.30f, 3.5f, "SK018", null, 1800, false),
        D("H021", "格拉克斯", "懒鬼", "zhongzhan101", "重武者", MercRarity.Common, 1.00f, 368.5f,37.8f,27.9f, 0.80f, 2.8f, null, "SK002", 800, false, true),
        D("H022", "索尔", "铁面", "zhongzhan201", "重武者", MercRarity.Rare, 1.15f, 466.1f,54.8f,40.4f, 0.85f, 2.8f, "SK003", null, 2200, false, false),
    };

    static Dictionary<string, Def> _byHire;
    static Dictionary<string, Def> _byAsset;
    /// <summary>是否已尝试过读 merc_roster 表（只试一次，避免每次 Ensure 都 Load）。</summary>
    static bool _csvTried;

    /// <summary>
    /// 优先读 merc_roster 表（Resources/Data/Tables/merc_roster.bytes，明文 CSV）。
    /// 缺表 / 行数不足 / 解析为空 → 保留 <see cref="Table"/> 的硬编码初值（不崩）。
    /// 列顺序见 merc_roster.csv 表头：HireId,Name,Nickname,AssetId,JobName,Rarity,Growth,
    /// BaseHp,BaseAtk,BaseDef,AtkSpeed,MoveSpeed,ActiveSkillId,PassiveSkillId,RecruitGold,InInitialPool,SoldInShop
    /// </summary>
    static void LoadTableFromCsv()
    {
        if (_csvTried) return;
        _csvTried = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.MercRoster);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MercRoster] 缺 merc_roster 表（回退硬编码数组）");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        if (rows.Count < 2)
        {
            Debug.LogWarning("[MercRoster] merc_roster 行数不足（回退硬编码数组）");
            return;
        }

        // rows[0] 是表头，数据从 index 1 起。
        var list = new List<Def>();
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 12) continue; // 至少到 MoveSpeed 才有意义

            string activeStr = c.Length > 12 ? c[12].Trim() : null;
            string passiveStr = c.Length > 13 ? c[13].Trim() : null;

            var d = new Def
            {
                HireId = c[0].Trim(),
                Name = c[1].Trim(),
                Nickname = c[2].Trim(),
                AssetId = c[3].Trim(),
                JobName = c[4].Trim(),
                Rarity = (MercRarity)GameTableCsv.TryInt(c, 5, 0),
                Growth = GameTableCsv.TryFloat(c, 6, 1f),
                BaseHp = GameTableCsv.TryFloat(c, 7, 0f),
                BaseAtk = GameTableCsv.TryFloat(c, 8, 0f),
                BaseDef = GameTableCsv.TryFloat(c, 9, 0f),
                AtkSpeed = GameTableCsv.TryFloat(c, 10, 1f),
                MoveSpeed = GameTableCsv.TryFloat(c, 11, 3.5f),
                ActiveSkillId = string.IsNullOrEmpty(activeStr) ? null : activeStr,
                PassiveSkillId = string.IsNullOrEmpty(passiveStr) ? null : passiveStr,
                RecruitGold = GameTableCsv.TryInt(c, 14, 0),
                InInitialPool = GameTableCsv.TryBool(c, 15, false),
                SoldInShop = GameTableCsv.TryBool(c, 16, false),
            };
            if (string.IsNullOrEmpty(d.HireId)) continue;
            list.Add(d);
        }

        if (list.Count == 0)
        {
            Debug.LogWarning("[MercRoster] merc_roster 解析为空（回退硬编码数组）");
            return;
        }

        Table = list.ToArray();
    }

    static Def D(
        string hireId, string name, string nick, string asset, string job,
        MercRarity rarity, float growth, float hp, float atk, float def,
        float atkSpd, float move, string activeSkill, string passiveSkill, int gold, bool initial,
        bool soldInShop = false)
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
            SoldInShop = soldInShop
        };
    }

    static void Ensure()
    {
        if (_byHire != null) return;
        LoadTableFromCsv(); // 表优先；失败则保留硬编码 Table
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

    /// <summary>酒馆名册：SoldInShop=false 的那部分（当前 15 名）。</summary>
    public static IReadOnlyList<Def> TavernRoster
    {
        get { Ensure(); return FilterByShop(false); }
    }

    /// <summary>商店货架：SoldInShop=true 的那部分（当前 7 名，解锁价 ≥1900）。</summary>
    public static IReadOnlyList<Def> ShopRoster
    {
        get { Ensure(); return FilterByShop(true); }
    }

    static IReadOnlyList<Def> _tavernCache;
    static IReadOnlyList<Def> _shopCache;

    static IReadOnlyList<Def> FilterByShop(bool soldInShop)
    {
        var cache = soldInShop ? _shopCache : _tavernCache;
        if (cache != null) return cache;

        var list = new List<Def>();
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].SoldInShop == soldInShop)
                list.Add(Table[i]);

        if (soldInShop) _shopCache = list; else _tavernCache = list;
        return list;
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

    // =====================================================================
    // ID 解析 · 唯一出口（2026-10-23 主人拍板）
    // 旧实现散在 5 处：MercPortraitSprites / MercStarSystem / MercGrowSystem /
    // MercLineTable / Mercenary，口径已分叉（查不到时有的返回 null、有的返回原串）。
    // 现在全部走下面 ResolveHireId / ResolveAssetId，别处不许再写第二份。
    // =====================================================================

    /// <summary>剧情 NPC / 昵称 → 佣兵立绘目录里的稳定 ID（原在 MercPortraitSprites，随解析一起收口）。</summary>
    static readonly Dictionary<string, string> AliasToHireId = new Dictionary<string, string>
    {
        { "player", "player" },
        { "wanjia", "player" },
        { "laodun", "H001" },
        { "xiaomei", "C001" },
        { "npc_xiaomei", "C001" },
        { "altor", "C002" },
        { "npc_shengdian", "C002" },
        { "grey", "C003" },
        { "npc_grey", "C003" },
        { "xiaobai", "H011" },
        { "naima101", "H011" },
        // 剧情 NPC → 佣兵立绘目录中的稳定英文 ID（见 Sync / LoadEditorStand）
        { "receptionist", "receptionist" },
        { "guildmaster", "guildmaster" },
        { "guildmaster_hidden", "guildmaster_hidden" },
        { "hunter", "C004" },
        { "duyan", "C004" },
        { "npc_duyan", "C004" },
        // 老板娘专属立绘（2026-10-15 接上，不再复用前台小姐）：Resources/Art/UI/Tavern/酒吧老板娘.png
        { "landlady", "landlady" },
        { "boss_niang", "landlady" },
        { "innkeeper", "landlady" },
    };

    /// <summary>别名 / H 编号 / C 编号 / player → 标准 hireId；认不出返回 null。</summary>
    public static string NormalizeHireId(string hireIdOrAlias)
    {
        if (string.IsNullOrEmpty(hireIdOrAlias)) return null;
        string key = hireIdOrAlias.Trim();
        if (AliasToHireId.TryGetValue(key, out var mapped))
            return mapped;
        if (key.StartsWith("H", System.StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("C", System.StringComparison.OrdinalIgnoreCase)
            || key == "player")
            return key;
        return null;
    }

    /// <summary>hireId / assetId / 别名 → hireId（多 H 共用 asset 时取首条）；查不到返回 null。</summary>
    public static string ResolveHireId(string hireIdOrAssetId)
    {
        string normalized = NormalizeHireId(hireIdOrAssetId);
        if (!string.IsNullOrEmpty(normalized)) return normalized;
        if (string.IsNullOrEmpty(hireIdOrAssetId)) return null;

        if (MercSkillMapping.TryGetByAssetId(hireIdOrAssetId, out var row)
            && !string.IsNullOrEmpty(row.HireId))
            return row.HireId;

        if (TryGetByAssetId(hireIdOrAssetId, out var def)
            && !string.IsNullOrEmpty(def.HireId))
            return def.HireId;

        return null;
    }

    /// <summary>hireId / assetId → assetId；查不到原样返回输入（不做兜底，空串/空值交给调用方判）。</summary>
    public static string ResolveAssetId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (TryGetByHireId(id, out var byHire)) return byHire.AssetId ?? id;
        if (TryGetByAssetId(id, out var byAsset)) return byAsset.AssetId ?? id;
        return id;
    }

    // =====================================================================
    // 职业形态 · 唯一出口（2026-10-23 主人拍板）
    // 前缀判定（gongshou / naima / fashi / mushi / zhongzhan / qita）原先散在
    // Mercenary.IsMeleeRoleId、Mercenary.GetAttackVfxKit、MercRosterDefs.ResolveRange 三处，
    // 现在只写一份；加职业改前缀只动这里。
    // =====================================================================
    public enum MercJobKind
    {
        /// <summary>近战（剑盾等，短射程、物攻、挥砍特效）。</summary>
        Melee,
        /// <summary>弓手（长射程、物攻、弓箭特效）。</summary>
        Bow,
        /// <summary>法系（牧师/法师/术士，长射程、魔攻、法球特效）。</summary>
        Magic,
        /// <summary>长柄（重战/其它，中射程、物攻、挥砍特效）。</summary>
        Polearm,
        /// <summary>
        /// 狂战（大剑/双手武器，中射程、物攻、挥砍特效）。
        /// 【2026-10-24 主人拍板】从 Melee 拆出独立档位：原先狂战与盾兵同吃 weapon_sword 96px，
        /// 而玩家狂战士走 weapon_greatsword 120px（2026-10-11 拍板「狂战和重武改成120」）——
        /// 同一职业佣兵比玩家近一档，不合理。特效仍与 Melee 同款（GetAttackVfxKit 的 default）。
        /// </summary>
        Berserker
    }

    public static MercJobKind JobKindOf(string idOrAssetId)
    {
        string asset = ResolveAssetId(idOrAssetId);
        if (string.IsNullOrEmpty(asset)) return MercJobKind.Melee;
        if (asset.StartsWith("gongshou")) return MercJobKind.Bow;
        if (asset.StartsWith("naima") || asset.StartsWith("fashi") || asset.StartsWith("mushi")) return MercJobKind.Magic;
        if (asset.StartsWith("zhongzhan") || asset.StartsWith("qita")) return MercJobKind.Polearm;
        // 【2026-10-24 主人拍板】狂战单独一档（射程对齐玩家狂战士 weapon_greatsword 120px）。
        if (asset.StartsWith("kuangzhan")) return MercJobKind.Berserker;
        // 前缀认不出时看职业名（原 IsMeleeRoleId 的兜底口径，行为不变）
        string job = GetJobName(asset) ?? GetJobName(idOrAssetId);
        if (!string.IsNullOrEmpty(job))
        {
            if (job.Contains("游侠")) return MercJobKind.Bow;
            if (job.Contains("狂战士")) return MercJobKind.Berserker;
            if (job.Contains("法师") || job.Contains("牧师")) return MercJobKind.Magic;
        }
        return MercJobKind.Melee;
    }

    /// <summary>
    /// 解锁价格（酒馆名册与商店货架共用同一口径）：用花名册的 RecruitGold，为 0 时按稀有度兜底。
    /// </summary>
    public static int UnlockCost(Def def)
    {
        if (def.RecruitGold > 0) return def.RecruitGold;
        switch (def.Rarity)
        {
            case MercRarity.Legendary: return 5000;
            case MercRarity.Rare: return 1500;
            default: return 500;
        }
    }

    /// <summary>
    /// 佣兵技能显示名：优先读 merc_skills 表；表没 Cook 或查不到时退回技能 id，不留空。
    /// </summary>
    public static string SkillDisplayName(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return "";
        if (MercSkillTable.TryGet(skillId, out var row) && !string.IsNullOrEmpty(row.DisplayName))
            return row.DisplayName;
        return skillId;
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

    public static string GetAppearLine(string hireId)
        => MercLineTable.Pick(hireId, MercLineTable.Scene.TavernRefresh);

    // 【2026-10-23 主人拍板】GetRecruitLine（零调用别名）与 GetLastRunLine（恒返 null）已删除；
    // 酒馆台词一律走 GetAppearLine / PickTavernAppearLine。
    /// <summary>酒馆刷出/闲置：读表「酒馆刷新」（不再优先 LastRunLine）。</summary>
    public static string PickTavernAppearLine(string hireId, bool wasInLastRun = false)
        => MercLineTable.Pick(hireId, MercLineTable.Scene.TavernRefresh);

    public static string GetMvpLine(string hireId)
        => MercLineTable.Pick(hireId, MercLineTable.Scene.Mvp);

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
        // 花名册 BaseAtk 即战斗 ATK，禁止再叠佣兵伤害系数。
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
        // 2026-10-23 主人拍板：前缀判定统一走 JobKindOf，射程档位只在这一处映射。
        // 【2026-10-24 主人拍板】Berserker 单独映射 weapon_greatsword（120px），与玩家狂战士同档。
        switch (JobKindOf(assetId))
        {
            case MercJobKind.Bow: return AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Bow);
            case MercJobKind.Magic: return AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Staff);
            case MercJobKind.Polearm: return AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Polearm);
            case MercJobKind.Berserker: return AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Greatsword);
            default: return AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Sword);
        }
    }
}

