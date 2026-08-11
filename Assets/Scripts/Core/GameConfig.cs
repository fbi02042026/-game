using UnityEngine;

/// <summary>
/// 全局游戏配置，所有常量放这里，数值调优直接改这里
/// </summary>
public static class GameConfig
{
    [Header("分辨率配置")]
    public const float DESIGN_WIDTH = 720f;
    public const float DESIGN_HEIGHT = 1280f;
    /// <summary>CanvasScaler.matchWidthOrHeight 默认；实际以 BattleViewportFit 按屏比例覆盖</summary>
    public const float UI_MATCH = 1f;
    public const float PIXEL_PER_UNIT = 100f; // 数值表：100像素=1世界单位

    /// <summary>像素 → 世界单位（对齐数值表「攻击范围(像素)」）</summary>
    public static float PixelsToUnits(float pixels) => pixels / PIXEL_PER_UNIT;

    /// <summary>
    /// 归一化攻击距离：数值表用像素（通常≥64）；旧配置若已是世界单位（通常&lt;10）则原样返回。
    /// </summary>
    public static float NormalizeAttackRange(float raw)
    {
        if (raw > 10f) return PixelsToUnits(raw);
        return raw;
    }

    /// <summary>是否远程攻击射程（弓）</summary>
    public static bool IsRangedAttackRange(float rangeWorld)
    {
        return rangeWorld >= RangeBow - 0.15f;
    }

    /// <summary>
    /// 由攻击射程推算索敌范围（英雄/怪物/佣兵共用）。
    /// 远程索敌≥近战；近战额外加缓冲防擦肩而过。
    /// </summary>
    public static float GetDetectRangeFromAttackRange(float attackRangeRaw)
    {
        float range = Mathf.Max(0.1f, NormalizeAttackRange(attackRangeRaw));

        if (IsRangedAttackRange(range))
            return range + 0.5f;

        if (range >= RangePolearm - 0.1f)
            return range + 1.2f;

        if (range >= RangeStaff - 0.05f)
            return range + 0.8f;

        if (range >= RangeGreatsword - 0.05f)
            return range + 1.5f;

        // 单手剑等近战
        return Mathf.Max(range + 2.2f, range * 2.5f);
    }

    /// <summary>从装备模板解析攻击射程（像素表 + 武器类型兜底）</summary>
    public static float ResolveWeaponAttackRange(EquipTemplate tpl)
    {
        if (tpl == null) return BASE_ATTACK_RANGE;

        float raw = tpl.attackRange;
        if (raw > 10f)
            return NormalizeAttackRange(raw);

        if (raw > 0.1f && raw <= 10f && Mathf.Abs(raw - RangeSword) > 0.02f)
            return raw;

        string hint = ((tpl.spumName ?? "") + " " + (tpl.equipName ?? "")).ToLower();
        if (hint.Contains("bow") || hint.Contains("arrow") || hint.Contains("弓"))
            return RangeBow;
        if (hint.Contains("staff") || hint.Contains("wand") || hint.Contains("杖"))
            return RangeStaff;
        if (hint.Contains("spear") || hint.Contains("pole") || hint.Contains("枪") || hint.Contains("halberd"))
            return RangePolearm;
        if (hint.Contains("great") || hint.Contains("大剑") || hint.Contains("twohand"))
            return RangeGreatsword;

        if (tpl.weaponAttackType == WeaponAttackType.Magic)
            return RangeStaff;
        if (tpl.weaponType == WeaponType.TwoHand)
            return RangeGreatsword;

        return RangeSword;
    }

    [Header("武器攻击范围(像素) — 对齐《像素冒险_数值表》武器属性表")]
    public const float RANGE_PX_SWORD = 96f;      // 单手剑
    public const float RANGE_PX_GREATSWORD = 144f; // 大剑
    public const float RANGE_PX_POLEARM = 180f;   // 长柄
    public const float RANGE_PX_STAFF = 120f;     // 单手杖
    public const float RANGE_PX_BOW = 300f;       // 弓箭
    public const float RANGE_PX_SHIELD = 64f;     // 盾

    public static float RangeSword => PixelsToUnits(RANGE_PX_SWORD);
    public static float RangeGreatsword => PixelsToUnits(RANGE_PX_GREATSWORD);
    public static float RangePolearm => PixelsToUnits(RANGE_PX_POLEARM);
    public static float RangeStaff => PixelsToUnits(RANGE_PX_STAFF);
    public static float RangeBow => PixelsToUnits(RANGE_PX_BOW);
    public static float RangeShield => PixelsToUnits(RANGE_PX_SHIELD);
    /// <summary>Screen Space Camera 的 planeDistance</summary>
    public const float UI_PLANE_DISTANCE = 100f;

    [Header("场景缩放")]
    /// <summary>玩家与佣兵本地缩放。unit 在 BattleUI 下时由 Compensate 保证 lossy≈1，故此处用 1</summary>
    public const float UNIT_SCALE = 1f;
    /// <summary>
    /// 普通怪根缩放。用户说的 250~300 是 Canvas≈0.01 下的观感值；
    /// 迁到 WorldRoot 后等价为 2.5~3.0，并把 Monsters 子节点归一为 1。
    /// </summary>
    public const float MONSTER_SCALE_MIN = 3.75f;
    public const float MONSTER_SCALE_MAX = 4.5f;
    public const float MONSTER_CHILD_REF_SCALE = 1f;
    /// <summary>Monstersmoban 预制体 Monsters 子节点默认 scale（归一前）</summary>
    public const float MONSTER_PREFAB_MONSTERS_SCALE = 100f;
    /// <summary>锚点坐标从预制体 Canvas 空间换算到世界 unit 的系数（100→1）</summary>
    public const float MONSTER_ANCHOR_SCALE_FACTOR = MONSTER_CHILD_REF_SCALE / MONSTER_PREFAB_MONSTERS_SCALE;
    public const float ELITE_SCALE_MULTIPLIER = 1.3f;
    public const float BOSS_SCALE_MULTIPLIER = 1.6f;
    public const float ELITE_UNIT_SCALE = MONSTER_SCALE_MIN * ELITE_SCALE_MULTIPLIER;
    public const float BOSS_UNIT_SCALE = MONSTER_SCALE_MIN * BOSS_SCALE_MULTIPLIER;
    public const float MONSTER_BASE_SCALE = 4.125f;
    /// <summary>怪物血条脚下 Y（预制体 -2.2 按锚点系数换算后的世界 unit 本地坐标）</summary>
    public const float MONSTER_HP_BAR_FOOT_LOCAL_Y = -2.2f * MONSTER_ANCHOR_SCALE_FACTOR;
    /// <summary>站立线 = unit 节点 Y，不再额外抬高</summary>
    public const float UNIT_STAND_Y_OFFSET = 0f;
    public const float CAMERA_ORTHO_SIZE = 5.4f;

    [Header("佣兵解锁")]
    public const int ADVANCED_MERC_GUILD_LEVEL = 5; // 优秀=公会5；稀有10/传奇20 另见 MercQuality

    [Header("战斗排序")]
    public const string BATTLE_SORTING_LAYER = "Default";
    /// <summary>map 战斗背景 Canvas（用户约定：BattleUI=0，map=10，单位=15，特效=50）</summary>
    public const int SORT_MAPROOT = 10;
    /// <summary>BattleUI 根 Canvas（顶栏/背包/角色栏）</summary>
    public const int SORT_BATTLE_UI = 0;
    /// <summary>人物/怪物/血条</summary>
    public const int SORT_UNIT = 15;
    /// <summary>攻击特效</summary>
    public const int SORT_VFX = 50;

    /// <summary>默认解锁的背包行数（底行 y=3 需天赋）</summary>
    public const int BACKPACK_DEFAULT_ROWS = 3;
    /// <summary>解锁第 4 行背包的天赋 ID</summary>
    public const string TALENT_BACKPACK_ROW4 = "backpack_row4";

    /// <summary>当前存档已解锁的背包行数（1~BACKPACK_HEIGHT）</summary>
    public static int GetUnlockedBackpackRows(SaveData data)
    {
        int rows = BACKPACK_DEFAULT_ROWS;
        if (data?.talents != null && data.talents.TryGetValue(TALENT_BACKPACK_ROW4, out int lv) && lv > 0)
            rows = BACKPACK_HEIGHT;
        return Mathf.Clamp(rows, 1, BACKPACK_HEIGHT);
    }

    /// <summary>投普通/精英/Boss 怪物根缩放（子节点已归一为 1）</summary>
    public static float RollMonsterRootScale(bool isElite, bool isBoss)
    {
        float visual = Random.Range(MONSTER_SCALE_MIN, MONSTER_SCALE_MAX);
        if (isBoss) visual *= BOSS_SCALE_MULTIPLIER;
        else if (isElite) visual *= ELITE_SCALE_MULTIPLIER;
        return visual;
    }

    public static float GetUnitScale(bool isElite = false, bool isBoss = false)
    {
        return RollMonsterRootScale(isElite, isBoss);
    }

    /// <summary>解析佣兵档位：npc / junior(1xx) / advanced(2xx)</summary>
    public static MercTier GetMercTier(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return MercTier.Junior;
        if (mercId.StartsWith("npc_", System.StringComparison.OrdinalIgnoreCase))
            return MercTier.Npc;
        // 取末尾连续数字：dunbing201 → 201
        int i = mercId.Length - 1;
        while (i >= 0 && char.IsDigit(mercId[i])) i--;
        string num = mercId.Substring(i + 1);
        if (num.Length > 0 && num[0] == '2') return MercTier.Advanced;
        return MercTier.Junior;
    }

    public static bool IsMercAvailable(string mercId, SaveData data)
    {
        MercTier tier = GetMercTier(mercId);
        if (tier == MercTier.Npc) return false; // 剧情 NPC，不进入可雇佣/出战池
        if (tier == MercTier.Advanced)
        {
            int guild = data != null ? data.guildLevel : 1;
            return guild >= ADVANCED_MERC_GUILD_LEVEL;
        }
        return true;
    }

    /// <summary>把单位挂到场景 unit 节点下（保持世界坐标），找不到则不改父级</summary>
    public static void AttachToUnitRoot(Transform t)
    {
        if (t == null) return;
        Transform root = BattleManager.Instance != null ? BattleManager.Instance.unitRoot : null;
        if (root == null)
        {
            GameObject go = GameObject.Find("unit");
            if (go == null) go = GameObject.Find("Unit");
            if (go != null) root = go.transform;
        }
        if (root == null || t.parent == root) return;
        t.SetParent(root, true);
    }

    /// <summary>统一写入战斗单位排序（Default / 15）</summary>
    public static void ApplyUnitSorting(Transform root)
    {
        if (root == null) return;
        var sg = root.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = root.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
        if (sg != null)
        {
            sg.sortingLayerName = BATTLE_SORTING_LAYER;
            sg.sortingOrder = SORT_UNIT;
        }
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            // 血条节点保持预制体层级，避免被压到看不见
            if (IsHpBarSprite(srs[i])) continue;
            srs[i].sortingLayerName = BATTLE_SORTING_LAYER;
            if (srs[i].sortingOrder < SORT_UNIT || srs[i].sortingOrder > SORT_VFX)
                srs[i].sortingOrder = SORT_UNIT;
        }
    }

    static bool IsHpBarSprite(SpriteRenderer sr)
    {
        if (sr == null) return false;
        Transform t = sr.transform;
        while (t != null)
        {
            if (t.name == "HPBar") return true;
            t = t.parent;
        }
        return false;
    }

    [Header("基础属性（对齐数值表·玩家 Lv1）")]
    public const float BASE_MOVE_SPEED = 1.2f;
    /// <summary>进战斗后首波刷怪延迟（秒）</summary>
    public const float FIRST_WAVE_SPAWN_DELAY = 1.5f;
    /// <summary>怪刷在英雄前方多远（原地等玩家走过来）</summary>
    public const float MONSTER_ENGAGE_OFFSET = 4.0f;
    /// <summary>怪物远程射程倍率（相对数值表弓射程）</summary>
    public const float MONSTER_RANGED_RANGE_MUL = 1.45f;
    /// <summary>怪物远程额外索敌缓冲</summary>
    public const float MONSTER_RANGED_DETECT_BONUS = 1.0f;
    /// <summary>小怪默认移速（比玩家慢，避免擦肩而过）</summary>
    public const float MONSTER_DEFAULT_MOVE_SPEED = 0.45f;
    /// <summary>从右侧缓步入场速度</summary>
    public const float MONSTER_ENTER_SPEED = 0.55f;
    /// <summary>入场起点比交战点再远多少（世界单位）</summary>
    public const float MONSTER_ENTER_DISTANCE = 2.5f;
    /// <summary>玩家出生相对 SpawnPoint 再往左偏（世界单位）</summary>
    public const float SPAWN_X_LEFT_BIAS = -0.5f;
    /// <summary>SPUM 移动动画播放速率（再 +20%）</summary>
    public const float MOVE_ANIM_SPEED_SCALE = 0.4853f;
    /// <summary>镜头相对主角 X 偏移（过大易把身后佣兵挤出左缘）</summary>
    public const float CAMERA_FOLLOW_OFFSET_X = 0.85f;
    /// <summary>默认近战攻击距离（单手剑 96px @ PPU100）</summary>
    public const float BASE_ATTACK_RANGE = 0.96f; // 96/100，对齐数值表
    public const float BASE_ATTACK_SPEED = 1.428f; // 单手剑间隔 0.7s → 1/0.7
    public const int BASE_ATTACK = 30;
    public const int BASE_HP = 200;
    public const int BASE_DEFENSE = 8;
    public const float BASE_CRIT_RATE = 0.05f;
    public const float BASE_CRIT_DAMAGE = 0.5f; // 额外暴击伤害（总倍率 1.5+该值）
    public const float BASE_HP_REGEN_RATE = 0.005f; // MaxHP×0.5%/秒
    public const int BASE_STRENGTH = 5;
    public const int BASE_INTELLIGENCE = 5;
    public const int BASE_AGILITY = 5;
    public const int BASE_VITALITY = 5;

    [Header("怪物基础（对齐数值表·未缩放）")]
    public const float MONSTER_NORMAL_HP = 60f;
    public const float MONSTER_NORMAL_ATK = 12f;
    public const float MONSTER_NORMAL_DEF = 2f;
    public const float MONSTER_NORMAL_ATK_INTERVAL = 1.5f;
    public const float MONSTER_ELITE_HP = 180f;
    public const float MONSTER_ELITE_ATK = 24f;
    public const float MONSTER_ELITE_DEF = 6f;
    public const float MONSTER_ELITE_ATK_INTERVAL = 1.7f;
    public const float MONSTER_BOSS_HP = 3000f;
    public const float MONSTER_BOSS_ATK = 45f;
    public const float MONSTER_BOSS_DEF = 12f;
    public const float MONSTER_BOSS_ATK_INTERVAL = 2.2f;
    /// <summary>
    /// 怪物攻速总倍率（最终攻速 = 1/间隔 × 本值）。
    /// 前期先压低；以后难度高了往 1 调（甚至 &gt;1）。
    /// </summary>
    public const float MONSTER_ATK_SPEED_MUL = 0.65f;
    /// <summary>弓/法球等子弹单位攻速倍率（1=不变；0.5=发射频率降 50%）</summary>
    public const float PROJECTILE_ATK_SPEED_MUL = 0.5f;
    /// <summary>章节系数：0.15×(n-1)</summary>
    public const float CHAPTER_SCALE_PER = 0.15f;
    /// <summary>公会等级系数：0.02×公会等级</summary>
    public const float GUILD_SCALE_PER = 0.02f;

    [Header("战斗配置")]
    /// <summary>怪物伤害倍率（数值表已校准，默认 1）</summary>
    public const float MONSTER_DAMAGE_MULTIPLIER = 1f;
    public const float STAGE_LENGTH = 20f; // 每关长度20单位，走到头通关
    public const int EQUIP_CHOOSE_COUNT = 3; // 每关结束三选一装备
    public const int MAX_EQUIP_SLOT = 7; // 身上装备槽位数量：头/胸/手/脚/披风/主手/副手
    public const int BACKPACK_WIDTH = 7; // 与 BattleUI GridContainer 列数一致（Cell_0~6）
    public const int BACKPACK_HEIGHT = 4; // 背包高4格；最底行 y=3 默认锁定，天赋解锁
    public const int STAGES_PER_CHAPTER = 10; // 每章10关，最后一关是BOSS
    public const int SPECIAL_STAGES_PER_CHAPTER = 2; // 每章最多2个特殊关卡（商人/附魔/诅咒/休息）
    public const int MAX_OFFLINE_HOURS = 8; // 最多8小时离线收益
    public const int GOLD_PER_TALENT_POINT = 100; // 每100金币给1天赋点

    [Header("资源上限")]
    /// <summary>通用资源软上限（金币/钻石等）；超出不累加，进邮件</summary>
    public const long RESOURCE_MAX = ResourceWallet.DEFAULT_MAX;
    /// <summary>体力特殊上限</summary>
    public const int STAMINA_MAX = 100;
    /// <summary>新号初始体力</summary>
    public const int STAMINA_START = 100;
    /// <summary>每次点「冒险」消耗体力</summary>
    public const int STAMINA_ADVENTURE_COST = StaminaSystem.ADVENTURE_COST;
    /// <summary>回复 1 点体力所需秒数</summary>
    public const int STAMINA_REGEN_SECONDS = StaminaSystem.REGEN_SECONDS_PER_POINT;

    [Header("怪物章节文件夹映射")]
    /// <summary>
    /// 章节对应的怪物文件夹名（在 Icons/default size/no shadow/ 下）
    /// 同时也是战斗背景文件夹名（在 Assets/Art/UI/background/ 下）
    /// </summary>
    public static readonly string[] ChapterMonsterFolders = new string[]
    {
        "4 Forest",   // 第1章
        "1 Undead",   // 第2章
        "2 Jungle",   // 第3章
        "3 Sea",      // 第4章
        "5 Field",    // 第5章
        "6 Cave",     // 第6章
        "7 Devil",    // 第7章
        "8 Ice"       // 第8章
    };

    /// <summary>与 ChapterMonsterFolders 一一对应的地图显示名</summary>
    public static readonly string[] ChapterMapNames = new string[]
    {
        "暮影森林",     // Forest
        "幽冥墓园",     // Undead
        "翡翠秘境",     // Jungle
        "深蓝遗迹海域", // Sea
        "晨曦原野",     // Field
        "巨岩深窟",     // Cave
        "赤焰炼狱",     // Devil
        "永霜雪境"      // Ice
    };

    public static string GetChapterMapName(int gameChapter)
    {
        int idx = Mathf.Clamp(gameChapter - 1, 0, ChapterMapNames.Length - 1);
        return ChapterMapNames[idx];
    }

    public static string GetChapterTitleText(int gameChapter)
    {
        string[] cn = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        int idx = Mathf.Clamp(gameChapter - 1, 0, cn.Length - 1);
        return $"第{cn[idx]}章  {GetChapterMapName(gameChapter)}";
    }

    [Header("刷怪数量公式（首关10~15，后期随机顶到30~35）")]
    /// <summary>单波最少怪物数</summary>
    public const int WAVE_MONSTER_MIN = 2;
    /// <summary>单波最多怪物数</summary>
    public const int WAVE_MONSTER_MAX = 4;
    /// <summary>一关最少波次</summary>
    public const int STAGE_WAVE_MIN = 3;
    /// <summary>一关最多波次</summary>
    public const int STAGE_WAVE_MAX = 9;
    /// <summary>无刷怪点时，波与波之间的世界距离（兼容旧逻辑）</summary>
    public const float VIRTUAL_WAVE_SPACING = 4.2f;

    /// <summary>清完一波后，下一波倒计时秒数</summary>
    public const float WAVE_SPAWN_INTERVAL = 8f;
    /// <summary>点击加速出兵：剩余每秒兑换金币</summary>
    public const float WAVE_SKIP_GOLD_PER_SEC = 3f;
    /// <summary>连杀判定窗口（秒）</summary>
    public const float COMBO_WINDOW = 2.2f;
    /// <summary>连杀≥3 时每次额外金币</summary>
    public const int COMBO_BONUS_GOLD = 1;

    /// <summary>
    /// 关卡总怪数：前两关固定爽感区间 10~15；
    /// 之后按进度抬高，并在区间内随机，章末附近可到 30~35。
    /// </summary>
    public static int GetStageMonsterTotal(int stageIndex0Based)
    {
        int stageNo = Mathf.Max(1, stageIndex0Based + 1);
        if (stageNo <= 2)
            return Random.Range(10, 16); // 10~15

        float t = Mathf.Clamp01((stageNo - 1) / 9f);
        // 中后期：下限/上限都随关卡抬高，并保留随机宽度
        int lo = Mathf.RoundToInt(Mathf.Lerp(14, 28, t));
        int hi = Mathf.RoundToInt(Mathf.Lerp(18, 35, t));
        if (hi < lo) hi = lo;
        return Mathf.Clamp(Random.Range(lo, hi + 1), 10, 35);
    }

    /// <summary>普通关总怪数</summary>
    public static int GetNormalStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

    /// <summary>精英关总怪数（同随机曲线）</summary>
    public static int GetEliteStageMonsterTotal(int stageIndex0Based)
        => GetStageMonsterTotal(stageIndex0Based);

    /// <summary>BOSS 本体数量</summary>
    public static int GetBossStageMonsterTotal() => 1;

    /// <summary>BOSS 关小怪数 = 同关随机总数 − 1</summary>
    public static int GetBossStageMinionTotal(int stageIndex0Based)
        => Mathf.Max(8, GetStageMonsterTotal(stageIndex0Based) - GetBossStageMonsterTotal());

    /// <summary>
    /// 建议波次数：先按「总数 / 单波上限」估，再受刷怪点数量与 [MIN,MAX] 约束。
    /// </summary>
    public static int GetSuggestedWaveCount(int totalMonsters, int spawnPointCount)
    {
        int byTotal = Mathf.CeilToInt(totalMonsters / (float)WAVE_MONSTER_MAX);
        byTotal = Mathf.Clamp(byTotal, STAGE_WAVE_MIN, STAGE_WAVE_MAX);
        if (spawnPointCount <= 0)
            return byTotal;
        int byPoints = Mathf.Clamp(spawnPointCount, STAGE_WAVE_MIN, STAGE_WAVE_MAX);
        return Mathf.Clamp(Mathf.Min(byPoints, byTotal), STAGE_WAVE_MIN, STAGE_WAVE_MAX);
    }

    /// <summary>
    /// 把总怪数尽量均匀分到各波；余数优先给前几波。
    /// </summary>
    public static int[] DistributeMonstersToWaves(int totalMonsters, int waveCount)
    {
        waveCount = Mathf.Clamp(waveCount, 1, STAGE_WAVE_MAX);
        int minTotal = waveCount * WAVE_MONSTER_MIN;
        int maxTotal = waveCount * WAVE_MONSTER_MAX;
        totalMonsters = Mathf.Clamp(totalMonsters, minTotal, maxTotal);

        int[] counts = new int[waveCount];
        int remaining = totalMonsters;
        for (int i = 0; i < waveCount; i++)
        {
            int wavesLeft = waveCount - i;
            int minLeave = (wavesLeft - 1) * WAVE_MONSTER_MIN;
            int maxGive = Mathf.Min(WAVE_MONSTER_MAX, remaining - minLeave);
            int want = Mathf.CeilToInt(remaining / (float)wavesLeft);
            counts[i] = Mathf.Clamp(want, WAVE_MONSTER_MIN, maxGive);
            remaining -= counts[i];
        }
        return counts;
    }

    [Header("渐进式怪物解锁")]
    /// <summary>每章怪物总类型数</summary>
    public const int MONSTERS_PER_CHAPTER = 12;
    /// <summary>BOSS类型的起始编号（11-12为BOSS，不会被当做小怪）</summary>
    public const int BOSS_SPRITE_START = 11;
    /// <summary>首次通关可出现的最大怪物类型编号（前4-5种）</summary>
    public const int TIER0_MAX_SPRITE = 5;
    /// <summary>通关2-3次后可出现的最大怪物类型编号（前7-8种）</summary>
    public const int TIER1_MAX_SPRITE = 8;
    /// <summary>通关4次以上可出现的最大怪物类型编号（前10种，11-12始终是BOSS）</summary>
    public const int TIER2_MAX_SPRITE = 10;
    /// <summary>Tier1解锁所需的通关次数</summary>
    public const int TIER1_UNLOCK_CLEARS = 2;
    /// <summary>Tier2解锁所需的通关次数</summary>
    public const int TIER2_UNLOCK_CLEARS = 4;

    /// <summary>
    /// 获取章节对应的怪物精灵路径前缀
    /// </summary>
    public static string GetMonsterSpritePath(int chapter)
    {
        int idx = Mathf.Clamp(chapter - 1, 0, ChapterMonsterFolders.Length - 1);
        return "2D Pixel RPG Monster Pack/Icons/default size/no shadow/" + ChapterMonsterFolders[idx] + "/";
    }

    /// <summary>
    /// 游戏章节 → 怪物章节号映射
    /// 因为 ChapterMonsterFolders 可以重排，需要映射回怪物ID中的章节号
    /// 例如: 游戏第1章用"4 Forest" → 怪物章节号=4
    /// </summary>
    public static int GetMonsterChapter(int gameChapter)
    {
        // 从 ChapterMonsterFolders 提取章节号
        // "4 Forest" → 4, "1 Undead" → 1, "2 Jungle" → 2, etc.
        int idx = Mathf.Clamp(gameChapter - 1, 0, ChapterMonsterFolders.Length - 1);
        string folder = ChapterMonsterFolders[idx];
        // 提取开头的数字
        int spaceIdx = folder.IndexOf(' ');
        if (spaceIdx > 0 && int.TryParse(folder.Substring(0, spaceIdx), out int ch))
            return ch;
        return gameChapter; // 兜底
    }

    /// <summary>
    /// 强制设置 GameObject 的世界坐标
    /// 兼容 RectTransform 根节点（用户预制体常见）：通过 anchoredPosition3D 设置，避免 transform.position 不生效
    /// </summary>
    public static void SetWorldPosition(GameObject go, Vector3 worldPos)
    {
        if (go == null) return;
        SetWorldPosition(go.transform, worldPos);
    }

    /// <summary>
    /// 强制设置 Transform 的世界坐标（RectTransform 用 anchoredPosition3D）
    /// </summary>
    public static void SetWorldPosition(Transform t, Vector3 worldPos)
    {
        if (t == null) return;

        // 挂在战斗 unit 世界根下时：即使有 RectTransform 也直接写 position，
        // 避免 anchoredPosition 与 Rigidbody2D 不同步导致「看得见却打不到」
        bool underCanvas = IsUnderCanvas(t);
        if (t is RectTransform rt && underCanvas)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector3 localPos = t.parent != null ? t.parent.InverseTransformPoint(worldPos) : worldPos;
            rt.anchoredPosition3D = localPos;
        }
        else
        {
            t.position = worldPos;
        }

        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.position = new Vector2(worldPos.x, worldPos.y);
        }
    }

    static bool IsUnderCanvas(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            if (p.GetComponent<Canvas>() != null) return true;
            p = p.parent;
        }
        return false;
    }
}

/// <summary>佣兵/角色档位</summary>
public enum MercTier
{
    Junior,   // 1xx 初级
    Advanced, // 2xx 高级
    Npc       // npc_* 剧情
}

/// <summary>关卡类型</summary>
public enum StageType
{
    Normal, // 普通关：普通怪，基础奖励
    Elite, // 精英关：精英怪，更多装备/高概率蓝紫装
    Merchant, // 商人关：可以用金币买装备/道具/回血
    Enchant, // 附魔关：给已有装备加随机附魔词条
    Curse, // 诅咒关：三选一buff，每个buff带一个debuff，高风险高收益
    Rest, // 休息关：回血/分解装备得材料
    Boss // BOSS关：每章最后一关，必掉紫/橙装，解锁下一章
}