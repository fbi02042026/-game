using UnityEngine;

/// <summary>
/// GameConfig 的 战斗数值 / 射程 / 怪物 / 难度 部分（2026-09-26 从 GameConfig.cs 按域拆出，partial 同类型）。
/// 只搬位置，**数值与调用点一字未改**；改这里的常量 = 改原 GameConfig。
/// </summary>
public static partial class GameConfig
{
    [Header("武器攻击范围(像素) — 对齐《像素冒险_数值表》武器属性表")]
    public const float RANGE_PX_SWORD = 96f;      // 单手剑
    public const float RANGE_PX_GREATSWORD = 144f; // 大剑
    public const float RANGE_PX_POLEARM = 180f;   // 长柄
    public const float RANGE_PX_STAFF = 120f;     // 单手杖
    public const float RANGE_PX_BOW = 300f;       // 弓箭
    public const float RANGE_PX_SHIELD = 64f;     // 盾

    public static float RangeSword => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Sword);
    public static float RangeGreatsword => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Greatsword);
    public static float RangePolearm => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Polearm);
    public static float RangeStaff => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Staff);
    public static float RangeBow => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Bow);
    public static float RangeShield => AttackRangeTable.GetWeaponWorld(WeaponCombatTable.WeaponKind.Shield);
    /// <summary>Screen Space Camera 的 planeDistance</summary>
    public const float UI_PLANE_DISTANCE = 100f;

    /// <summary>UI Canvas sortingOrder 分层（Screen Space - Camera 统一用此表）。</summary>
    public static class UiSort
    {
        public const int TownPage = 5;
        public const int TownBootstrap = 100;
        public const int TownVeil = 200;
        public const int StoryDialogue = 500;
        public const int StoryNaming = 560;
        public const int TutorialHint = 600;
        public const int TownPopup = 900;
        /// 通用「获得奖励」弹窗：必须高于 TownPopup(900)——否则与每日登录界面同为 900 时排序不定、会被盖住；低于 BattlePopup(920) 与 Toast(11000)。
        public const int RewardPopup = 905;
        public const int BattleStageMap = 880;
        public const int BattlePopup = 920;
        public const int BattleLegacyPool = 940;
        public const int BattleHud = 950;
        public const int BattleLegacyChoose = 960;
        public const int BattleMilestone = 970;
        public const int BattleEvacuate = 980;
        public const int BattleSettlement = 990;
        public const int Loading = 10000;
        public const int Toast = 11000;
        public const int FullscreenFx = 12000;
    }

    [Header("场景缩放")]
    /// <summary>玩家与佣兵本地缩放。unit 在 BattleUI 下时由 Compensate 保证 lossy≈1，故此处用 1</summary>
    public const float UNIT_SCALE = 1f;
    /// <summary>
    /// 普通怪根缩放。用户说的 250~300 是 Canvas≈0.01 下的观感值；
    /// 迁到 WorldRoot 后等价为 2.5~3.0，并把 Monsters 子节点归一为 1。
    /// </summary>
    /// <summary>小怪整体缩小 30%（主人 2026-09-26 定）：在原有 MIN/MAX 之上乘 0.7，不写死新数值。
    /// 精英仍乘 ELITE_SCALE_MULTIPLIER（Normal : Elite 保持 1 : 1.3）；Boss 除外，见下。</summary>
    public const float MONSTER_SMALL_SHRINK = 0.7f;
    /// <summary>Boss 是否参与 MONSTER_SMALL_SHRINK 缩小（主人 2026-09-26 拍板：Boss 先不缩，只缩小怪）。
    /// false = Boss 保持原尺寸，在 Monster 设置根缩放时把 0.7 除回去。</summary>
    public const bool MONSTER_BOSS_APPLY_SMALL_SHRINK = false;
    public const float MONSTER_SCALE_MIN = 3.75f * MONSTER_SMALL_SHRINK;
    public const float MONSTER_SCALE_MAX = 4.5f * MONSTER_SMALL_SHRINK;
    public const float MONSTER_CHILD_REF_SCALE = 1f;
    /// <summary>Monstersmoban / ani 片段已按 scale=1 录制，不再做 Canvas→战斗缩放</summary>
    public const float MONSTER_PREFAB_MONSTERS_SCALE = 1f;
    public const float MONSTER_ANCHOR_SCALE_FACTOR = 1f;
    // 2026-09-21：精英/Boss 体型倍率已迁 combat_tuning 表（可运行时调）。
    // 原来把它们卡住的 ELITE_UNIT_SCALE / BOSS_UNIT_SCALE（const 派生）经查全工程零引用 = 死代码，已删。
    public static float ELITE_SCALE_MULTIPLIER => CombatTuningTable.Get("ELITE_SCALE_MULTIPLIER", 1.3f);
    public static float BOSS_SCALE_MULTIPLIER => CombatTuningTable.Get("BOSS_SCALE_MULTIPLIER", 1.6f);
    public const float MONSTER_BASE_SCALE = 4.125f;
    /// <summary>怪物血条脚下 Y（与预制体 Monstersmoban 本地坐标一致）</summary>
    public const float MONSTER_HP_BAR_FOOT_LOCAL_Y = -2.2f;
    /// <summary>站立线 = unit 节点 Y，不再额外抬高</summary>
    public const float UNIT_STAND_Y_OFFSET = 0f;
    public const float CAMERA_ORTHO_SIZE = 5.4f;

    [Header("基础属性（对齐数值表·玩家 Lv1）")]
    public const float BASE_MOVE_SPEED = 0.96f;
    /// <summary>进战斗后首波刷怪延迟（秒）</summary>
    public const float FIRST_WAVE_SPAWN_DELAY = 1.5f;
    /// <summary>
    /// 仅玩家单人战斗（不生成/显示佣兵）。正式局默认 false；引导关仍单独刷救援佣兵。
    /// </summary>
    public static bool SOLO_PLAYER_BATTLE = false;
    /// <summary>怪刷在英雄前方多远（原地等玩家走过来），约 3~4 身位</summary>
    public const float MONSTER_ENGAGE_OFFSET = 3.2f;
    /// <summary>剧情前置暂停（AllowMonsterMapEnter）期间，怪物只走到屏幕边缘内多远即停（不再深入英雄）。配合 MONSTER 进场逻辑使用。</summary>
    public const float MONSTER_PAUSE_STOP_MARGIN = 0.6f;
    /// <summary>同波怪物横向间距（世界单位）；需大于精灵半宽，避免首波叠在同一点</summary>
    public const float MONSTER_WAVE_SPACING = 0.72f;
    /// <summary>怪物近战射程倍率（相对单手剑；勿超过玩家近战体感）</summary>
    public const float MONSTER_MELEE_RANGE_MUL = 0.85f;
    /// <summary>怪物远程射程倍率（相对数值表弓射程）；累计再缩）</summary>
    public const float MONSTER_RANGED_RANGE_MUL = 0.588f;
    /// <summary>普通（非精英/非Boss）远程小怪的技能伤害折扣：技能只是为了看得到子弹，不该秒人</summary>
    public static float MONSTER_NORMAL_SKILL_DAMAGE_MUL => CombatTuningTable.Get("MONSTER_NORMAL_SKILL_DAMAGE_MUL", 0.55f);
    /// <summary>怪物普攻弹道速度倍率（勿随意改快）</summary>
    public const float MONSTER_BASIC_PROJECTILE_SPEED_MUL = 0.196f;
    /// <summary>怪物技能弹道速度倍率（勿随意改快）</summary>
    public static float MONSTER_SKILL_PROJECTILE_SPEED_MUL => CombatTuningTable.Get("MONSTER_SKILL_PROJECTILE_SPEED_MUL", 0.138f);
    /// <summary>敌方远程普攻：落点与目标当前受击点距离超过此值则 miss（可躲开）。</summary>
    public const float PROJECTILE_IMPACT_MISS_DIST = 0.55f;

    /// <summary>怪物血条宽度 = 精灵宽 × 此系数</summary>
    public const float MONSTER_HP_BAR_WIDTH_MUL = 0.72f;
    /// <summary>怪物血条高度（世界单位）</summary>
    public const float MONSTER_HP_BAR_HEIGHT = 0.09f;
    /// <summary>怪物血条相对脚底下沉（世界单位，负=更低）</summary>
    public const float MONSTER_HP_BAR_FOOT_DROP = -0.05f;
    /// <summary>小怪默认移速（再降 20%）</summary>
    public const float MONSTER_DEFAULT_MOVE_SPEED = 0.6912f;
    /// <summary>
    /// 怪物表移速(baseMoveSpeed) → 世界移速换算系数。
    /// 推导：历史硬编码 MONSTER_DEFAULT_MOVE_SPEED(0.6912) 实际对应「表值 2.2」的世界速度，
    /// 即 0.6912 = 2.2 × k → k = 0.6912 / 2.2 ≈ 0.3142。
    /// monster_stats 表 moveSpeed 已全体减半（2.2→1.1），乘同一系数得 1.1 × 0.3142 = 0.3456（世界单位）。
    /// </summary>
    public const float MONSTER_MOVE_SPEED_TO_WORLD = 0.3142f;
    /// <summary>从右侧缓步入场速度</summary>
    public const float MONSTER_ENTER_SPEED = 0.4f;
    /// <summary>入场起点比交战点再远多少（世界单位）；过大容易出场「往前窜」</summary>
    public const float MONSTER_ENTER_DISTANCE = 1.2f;
    /// <summary>玩家出生相对 SpawnPoint 再往左偏（世界单位）</summary>
    public const float SPAWN_X_LEFT_BIAS = -0.5f;
    /// <summary>SPUM 移动动画播放速率（再 +20%）</summary>
    public const float MOVE_ANIM_SPEED_SCALE = 0.4853f;
    /// <summary>受击动画播放速率（相对 1.0 快 20%）</summary>
    public const float DAMAGED_ANIM_SPEED = 1.2f;
    /// <summary>玩家/佣兵低血警告阈值（含等于）</summary>
    public const float LOW_HP_WARN_RATIO = 0.2f;
    /// <summary>镜头相对主角 X 偏移（过大易把身后佣兵挤出左缘）</summary>
    public const float CAMERA_FOLLOW_OFFSET_X = 0.85f;
    /// <summary>默认近战攻击距离（单手剑 96px @ PPU100）</summary>
    public const float BASE_ATTACK_RANGE = 0.864f; // 剑基准再缩 10%（原 0.96）
    public const float BASE_ATTACK_SPEED = 1.428f; // 单手剑间隔 0.7s → 1/0.7
    public const int BASE_ATTACK = 30;
    public const int BASE_HP = 200;
    public const int BASE_DEFENSE = 8;
    public const float BASE_CRIT_RATE = 0.05f;
    public const float BASE_CRIT_DAMAGE = 0.5f; // 额外暴击伤害（无职业表时总倍率 1.5+该值）
    /// <summary>
    /// 暴击倍率（主人 2026-09-26 拍板 = 2，统一口径）。
    /// 原值：DefaultCritMultiplier = 1.5f + BASE_CRIT_DAMAGE = 2.0f，但 player_job_base_stats 表的
    /// 「暴击伤害」列（150%~180%）会把实战倍率覆盖成 1.5~1.8，所以现在统一写本常量，
    /// 不再读表列（见 PlayerJobBaseStats.WriteCombat）。
    /// </summary>
    public const float CRIT_MULTIPLIER = 2f;
    /// <summary>无职业/表暴击伤害时的默认暴击倍率。</summary>
    public static float DefaultCritMultiplier => CRIT_MULTIPLIER;

    /// <summary>
    /// 吸血（主人 2026-09-26 拍板）：造成伤害后按本比例给「造成伤害的一方」回血（10%）。
    /// 装备词缀「吸血」（AttrType.LifeSteal，配表 0.02~0.05 的比例值）在其上叠加；没穿吸血装就是 10%。
    /// </summary>
    public const float LIFESTEAL_RATIO = 0.1f;

    /// <summary>
    /// 魔法防御兜底倍率：目标没写 MagicDefense（玩家/佣兵目前没配魔法防御）时，
    /// 按「物理 Defense × 本值」当魔法防御。主人没给魔法防御数值 → 等比沿用物理防御（1.0），
    /// 等配表补了真值再改这里。
    /// </summary>
    public const float MAGIC_DEFENSE_FALLBACK_RATIO = 1f;
    /// <summary>怪物魔法攻击 = baseAttack × 本值（主人没给数 → 等比沿用物理攻击）。</summary>
    public const float MONSTER_MAGIC_ATK_RATIO = 1f;
    /// <summary>怪物魔法防御 = 物理防御 baseDef × 本值（主人没给数 → 等比沿用物理防御）。</summary>
    public const float MONSTER_MAGIC_DEF_RATIO = 1f;

    /// <summary>
    /// 火/冰附加伤害「每点词缀 = 百分之几」（主人 2026-09-26 拍板「冰火附加按百分比」）。
    /// 附加伤害 = 最终伤害 × 词缀值 × 本值。默认 0.01f 即 1%/点（词缀值 5 → +5% 最终伤害）。
    /// 后期调数值只改这里：想让词缀值 5 变成 +50%，把本值改成 0.1f 即可。
    /// </summary>
    public const float FIRE_BONUS_PER_POINT = 0.01f;
    /// <summary>同上，冰霜附加每点词缀 = 百分之几（默认 1%/点）。</summary>
    public const float ICE_BONUS_PER_POINT = 0.01f;
    public const float BASE_HP_REGEN_RATE = 0.005f; // MaxHP×0.5%/秒
    // === 毒 DOT（游侠武器「中毒」词缀结算）2026-09-26 主人拍板：之前毒根本没落地，本次补上 ===
    // 节奏参数全部集中在这里，后期想「调快/调痛」只改本段，不用动 PoisonDotRunner。
    /// <summary>毒 DOT 跳伤间隔（秒）。之前没有 DOT，本值即首版默认。</summary>
    public const float POISON_TICK_INTERVAL = 0.5f;
    /// <summary>毒 DOT 总持续时间（秒）。照抄流血 3s。</summary>
    public const float POISON_DURATION = 3f;
    /// <summary>毒每跳伤害系数：每跳伤害 = 攻击者攻击力 × 中毒词缀值(如0.10) × 本值。</summary>
    public const float POISON_DPS_RATIO = 0.5f;
    public const int BASE_STRENGTH = 5;
    public const int BASE_INTELLIGENCE = 5;
    public const int BASE_AGILITY = 5;
    public const int BASE_VITALITY = 5;

    [Header("怪物基础（对齐数值表·未缩放）")]
    public const float MONSTER_NORMAL_HP = 60f;
    /// <summary>
    /// 全局怪物 HP 倍率。1.0 = 直接用 monster_stats 表的 baseHp（与冒险图鉴显示的数值一致）。
    /// 曾设为 0.6（减 40%），导致图鉴写「生命 78」而实战只有 46，教学关再叠一层只剩 5~7 被一刀秒。
    /// 要整体调难/调易只改这里即可。
    /// </summary>
    public const float MONSTER_HP_GLOBAL_MUL = 1.0f;

    /// <summary>
    /// 怪物重叠时头顶是否显示 ×N 角标。
    /// 关闭原因：①实际没有重叠也会亮（不同车道只比 X 不比 Y）；②每个角标 = 5 个 TextMesh
    /// （1 填充 + 4 描边），手机上 DrawCall 与 GC 都吃不消，小游戏端更是直接掉帧。
    /// 需要该提示时改为 true 即可（判定已修：同时比较 X 与车道 Y，且只有真正同簇才亮）。
    /// </summary>
    public const bool SHOW_MONSTER_STACK_LABEL = false;

    // 2026-09-18：数值一律以表为准，这里保持原值不再跟表一起缩放。
    // 注意 Monster.cs 的 Boss 兜底：表内 Boss 攻击 < 本值×0.5(27.5) 会被抬回 55，
    // 所以 monster_stats 里 Boss 的 baseAttack 必须 >= 28（已按此填表）。
    public const float MONSTER_NORMAL_ATK = 12f;
    public const float MONSTER_NORMAL_DEF = 2f;
    public const float MONSTER_NORMAL_ATK_INTERVAL = 1.5f;
    public const float MONSTER_ELITE_HP = 105f;   // 2026-09-14：精英 91~133（玻璃/坦克分档），初始装备 3~6 刀、稀有武器 2~4 刀
    public const float MONSTER_ELITE_ATK = 30f;
    public const float MONSTER_ELITE_DEF = 6f;
    public const float MONSTER_ELITE_ATK_INTERVAL = 1.7f;
    public const float MONSTER_BOSS_HP = 800f;    // 兜底/下限：表内 Boss 血量低于 50%(400) 才抬到本值；第一章 Boss 由 monster_stats 表给 400/525
    public const float MONSTER_BOSS_ATK = 55f;    // 2026-09-14：Boss 伤害上浮，保证坦克（防 35）也会掉血
    public const float MONSTER_BOSS_DEF = 12f;
    public const float MONSTER_BOSS_ATK_INTERVAL = 2.2f;
    /// <summary>
    /// 怪物攻速总倍率（最终攻速 = 1/间隔 × 本值 × MonsterConfig.baseAttackSpeed）。
    /// 前期先压低；以后难度高了往 1 调（甚至 &gt;1）。
    /// </summary>
    public const float MONSTER_ATK_SPEED_MUL = 0.65f;
    /// <summary>
    /// 仅敌方弓/法球普攻频率倍率（1=表值；0.5=再降一半）。
    /// 我方（玩家职业表 AttackInterval、佣兵花名册 AtkSpeed）不再叠这个，否则 0.5 秒间隔会变成 1 秒。
    /// </summary>
    public const float PROJECTILE_ATK_SPEED_MUL = 0.5f;

    /// <summary>
    /// 玩家（Hero）攻击速度倍率，0.8 = 出手间隔变慢 20%。
    /// 只作用于 Hero，佣兵与怪物不受影响（在 UnitBase.GetAttackCooldown 的 is Hero 分支里乘）。
    /// 与武器种族系数、连杀加速、被动攻速是乘法叠加。
    /// </summary>
    public const float PLAYER_ATTACK_SPEED_MUL = 0.8f;
    /// <summary>英雄基础攻击（BaseAtk）全局偏移。负值=整体削弱。-10 即各职业基础攻击 -10，用于手感/平衡微调。</summary>
    public const float HERO_BASE_ATTACK_OFFSET = 0f; // 2026-09-14：不再二次扣 10，职业表 baseAtk 即真源

    /// <summary>
    /// 裂缝「掉落」装备属性整体加成：掉落生成时每个「非百分比」词条数值 +此值。
    /// 只作用于掉落路径（关卡结算 / 教程宝箱），备战初始装备走默认 0 不受影响。
    /// 百分比/速率类词条（暴击、攻速、吸血、射程等）不加，避免数值爆炸。
    /// </summary>
    public const float RIFT_DROP_ATTR_BONUS = 10f;

    /// <summary>
    /// 装备强化（+1~+10）开关。局内构筑改造后关闭：装备只在单局内有效，
    /// 跨局成长改由「天赋树 + 局内技能/佣兵构筑」承担，避免双轨数值膨胀。
    /// 关闭时 <see cref="CraftStageApply.TryForgeUpgrade"/> 不再走强化石路径，退回升星。
    /// </summary>
    public const bool EquipEnhanceEnabled = false;

    // ============================================================
    // 局内构筑 V6：技能能量（每个技能独立充能）
    // ============================================================

    /// <summary>
    /// 技能能量注入倍率。技能能量由「共享单条」改为「每个技能一条」后，同一份受击回充会分摊到多条，
    /// 总释放频率约为改造前的 3~4 倍，用它把整体频率压回来。
    /// <b>这是本机的手感主旋钮</b>：放得太频繁往下调（0.35），太冷往上调。
    /// 只作用于 BattleManager.AddCombatSkillEnergy 的玩家分支，佣兵能量不受影响。
    /// </summary>
    public const float SKILL_ENERGY_CHARGE_MUL = 0.5f;

    /// <summary>
    /// 技能冷却缩减读取的属性上限（防天赋堆叠到 100% 冷却）。
    /// 见 AttrSystem.SkillCooldown → AttrType.CooldownReduce，SkillSystem.UseSkill 消费。
    /// </summary>
    public const float SKILL_COOLDOWN_REDUCE_CAP = 0.5f;

    /// <summary>
    /// 技能冷却总倍率（2026-09-26 主人要求：技能 CD 延长 1 倍；后又反馈「恢复还是太快」，再延长 1 倍 → ×4）。
    /// 单点旋钮：玩家技能在 PlayerSkillDefs 装载完统一乘（表 / Fallback / UI 文案同源），
    /// 佣兵技能在 MercSkillCaster.Bind 乘。要调回来只改这一个值。
    /// 决定「玩家/佣兵技能多久能再放一次」的就是它（叠加在 CSV/表的基础 CD 之上）。
    /// </summary>
    public const float SKILL_COOLDOWN_MUL = 4f;

    /// <summary>
    /// 圣盾壁垒（holy_barrier）护盾量 = 最大生命 × 本系数（抵扣型护盾：先扣盾、再扣血）。
    /// 2026-09-26 主人拍板：holy_barrier 从「防御+35%」改成抵扣型护盾。后期升级只改这个系数。
    /// </summary>
    public const float HOLY_BARRIER_SHIELD_RATIO = 0.35f;

    /// <summary>
    /// 圣盾壁垒（holy_barrier）护盾持续秒数。2026-09-26 主人拍板：原「防御+35%」改用护盾后，
    /// 覆盖时长单独提成常量，后期升级可改这里延长/缩短。
    /// </summary>
    public const float HOLY_BARRIER_SHIELD_DURATION = 5f;

    // ============================================================
    // 玩家被动技能：纯冷却制（2026-09-15 改版）
    // 去掉能量槽，改为「开局错峰给初始 CD → CD 好按槽序放一个 → 放完重进自己 CD」。
    // 想整包退回能量制：PLAYER_SKILL_USE_ENERGY = true 且 PLAYER_SKILL_GCD = 0。
    // ============================================================

    /// <summary>玩家技能是否走能量制。false = 纯冷却制（当前方案）。</summary>
    public const bool PLAYER_SKILL_USE_ENERGY = false;

    /// <summary>
    /// 全局释放间隔 GCD（秒）：放完任意一个玩家技能后，至少间隔这么久才能放下一个。
    /// 防止多个技能同时冷却完毕时在同一瞬间全部炸出来。
    /// 时间轴用 Time.time（与 SkillSystem 冷却递减的 Time.deltaTime 同轴），
    /// 顿帧/暂停时两者同步停走，解冻后不会补放。
    /// </summary>
    // 2026-09-26：随技能 CD 一起 ×2（原 1.2），间隔与冷却同口径延长
    public const float PLAYER_SKILL_GCD = 2.4f;

    /// <summary>
    /// 开局错峰：第 1 槽固定秒数（让它最快登场）。
    /// 0.5 秒实测等于「一进战斗就放技能」，改成 3 秒，先让普攻打几下。
    /// </summary>
    public const float PLAYER_SKILL_SLOT0_OPENING_CD = 3f;

    /// <summary>开局错峰系数（下标 = 槽位）：0 槽用上面的固定值，1~3 槽 = 自身 CD × 系数。</summary>
    public static readonly float[] PLAYER_SKILL_OPENING_CD_MUL = { 0f, 0.33f, 0.66f, 1f };

    // ============================================================
    // 敌人集中调参区：public static 便于实机热改与单点回退。
    // 敌人数值只能靠手感，出问题改这里的值即可，不要散落到各处硬编码。
    // ============================================================
    public static class EnemyTuning
    {
        // 2026-09-21：本节 12 项已迁 combat_tuning 表，`=> CombatTuningTable.Get(...)`；
        // 默认值 = 原硬编码值，表缺失/构造期读表被拒时行为与硬编码完全一致。
        // 2026-09-21 删除 5 个「全工程零引用」死字段（主人：「没用的都删掉」），真值去向已记录：
        //   · StageGrowthPerStage        → 真值在 Monster.cs:702 `waveMul = 1f + waveNum * 0.05f`
        //   · WaveCapBonusPerChapter / WaveCapMax → 真值走 STAGE_WAVE_MAX + StageModeTable.RollWaveCount（WavePlanner:643/666-677）
        //   · BossPhase2HpRatio / BossPhase2DmgMul → 真值 = BOSS_PHASE2_HP_RATIO / BOSS_PHASE2_DAMAGE_MUL（已迁表）
        // 2026-09-20 删除：MonsterHpGlobalMul / MonsterDmgGlobalMul 全工程零引用（死代码）。
        // 怪物强度现在一律由 monster_stats 表决定，不再提供全局倍率（主人：「让怪硬什么已经没用了，现在都读表了」）。

        /// <summary>精英/Boss 词缀：第几章开始有几率出 2 个词缀。</summary>
        public static int AffixSecondFromChapter => CombatTuningTable.GetInt("AFFIX_SECOND_FROM_CHAPTER", 4);
        /// <summary>精英/Boss 词缀：出第 2 个词缀的概率。</summary>
        public static float AffixSecondChance => CombatTuningTable.Get("AFFIX_SECOND_CHANCE", 0.3f);

        // ===== V3.0 随机波次：只调波数与每波人数，不改敌人数值 =====
        /// <summary>压力阀总开关。关掉后波数完全由模式表决定。</summary>
        public static bool PressureEnabled => CombatTuningTable.GetBool("PRESSURE_ENABLED", true);
        /// <summary>连续 N 关「无死亡且通关血量 &gt; PressureHpRatio」后，下一关波数 +1。</summary>
        public static int PressureStreakThreshold => CombatTuningTable.GetInt("PRESSURE_STREAK_THRESHOLD", 2);
        /// <summary>加压上限：最多额外加几波。</summary>
        public static int PressureWaveCap => CombatTuningTable.GetInt("PRESSURE_WAVE_CAP", 2);
        /// <summary>上一关死亡过 → 下一关波数 −1（且首波延后）。</summary>
        public static bool PressureMercyOnDeath => CombatTuningTable.GetBool("PRESSURE_MERCY_ON_DEATH", true);
        /// <summary>判定「轻松通关」的血量线。</summary>
        public static float PressureHpRatio => CombatTuningTable.Get("PRESSURE_HP_RATIO", 0.70f);
        /// <summary>职业 × 模式矩阵标「高」时，该关波数 −1。</summary>
        public static bool ModeHandicapByJob => CombatTuningTable.GetBool("MODE_HANDICAP_BY_JOB", true);
        /// <summary>每章每波人数的递增步长（第 1 章 1.00 → 第 8 章 1.21）。</summary>
        public static float ChapterCountStep => CombatTuningTable.Get("CHAPTER_COUNT_STEP", 0.03f);
        /// <summary>章内每关每波人数的递增步长（第 1 关 1.00 → 第 10 关 1.45）。</summary>
        public static float StageCountStep => CombatTuningTable.Get("STAGE_COUNT_STEP", 0.05f);
        /// <summary>每波人数随机浮动下限 / 上限。</summary>
        public static float CountJitterMin => CombatTuningTable.Get("COUNT_JITTER_MIN", 0.85f);
        public static float CountJitterMax => CombatTuningTable.Get("COUNT_JITTER_MAX", 1.15f);
    }

    /// <summary>
    /// 所有弹道飞行速度倍率，0.8 = 全弹道放慢 20%（飞行时间 ×1.25）。
    /// 作用在 BattleVFXSystem.ProjectileFlightCoroutine 的速度计算上，六种弹道统一生效。
    /// 注意：飞行时间不再被 maxFlightTime 截断，否则怪物慢速弹道会顶格、降速对它无效。
    /// 2026-09-18：由 0.8 改为 0.4，弹道（箭/法球等六种）再降速 50%。
    /// </summary>
    public const float PROJECTILE_SPEED_GLOBAL_MUL = 0.4f;

    /// <summary>
    /// 章节属性倍率已迁到 chapter_stat_scale 表。此数组仅作缺表 Fallback，数字必须与表 1:1。
    /// </summary>
    public static readonly float[] CHAPTER_STAT_SCALE = ChapterStatScaleTable.Fallback;

    /// <summary>按游戏章取属性倍率（读 chapter_stat_scale；缺表回退 Fallback）。</summary>
    public static float GetChapterStatScale(int gameChapter)
    {
        return ChapterStatScaleTable.Get(gameChapter);
    }
    // ===== 2026-09-21：以下 15 项已迁 combat_tuning 表，默认值 = 原硬编码值（零行为变化）=====
    /// <summary>精英额外 TTK 血量倍率（叠在章节系数上）</summary>
    public static float ELITE_TTK_HP_MUL => CombatTuningTable.Get("ELITE_TTK_HP_MUL", 1.15f);
    /// <summary>Boss 额外 TTK 血量倍率</summary>
    public static float BOSS_TTK_HP_MUL => CombatTuningTable.Get("BOSS_TTK_HP_MUL", 1.35f);
    /// <summary>
    /// Boss 额外 TTK【攻击】倍率。原先只有血量加成没有攻击加成，
    /// 第 8 章 Boss 攻击反而低于自家远程杂兵，故补上（2026-09-18 用户拍板）。
    /// 剧情 Boss 想更狠就单独调 monster_stats 表里的 baseAtk，不要把这里往上堆。
    /// </summary>
    public static float BOSS_TTK_ATK_MUL => CombatTuningTable.Get("BOSS_TTK_ATK_MUL", 1.35f);
    /// <summary>
    /// 精英血量 = 本章普通怪平均 baseHp × 本值。
    /// 原先写死 MONSTER_ELITE_HP=105，第 3 章起精英比本章杂兵还脆（第 8 章只剩 19%），
    /// 改为按章推导后自动跟随数值表（2026-09-18 用户拍板）。表缺数据时回退旧常量。
    /// </summary>
    public static float ELITE_HP_FROM_CHAPTER_AVG => CombatTuningTable.Get("ELITE_HP_FROM_CHAPTER_AVG", 2.6f);
    /// <summary>精英攻击 = 本章普通怪平均 baseAttack × 本值</summary>
    public static float ELITE_ATK_FROM_CHAPTER_AVG => CombatTuningTable.Get("ELITE_ATK_FROM_CHAPTER_AVG", 1.6f);
    /// <summary>Boss 进入阶段 2 的血量比例（≤ 则换招）</summary>
    public static float BOSS_PHASE2_HP_RATIO => CombatTuningTable.Get("BOSS_PHASE2_HP_RATIO", 0.7f);
    public static float BOSS_PHASE2_DAMAGE_MUL => CombatTuningTable.Get("BOSS_PHASE2_DAMAGE_MUL", 1.18f);
    public static float BOSS_PHASE2_RADIUS_MUL => CombatTuningTable.Get("BOSS_PHASE2_RADIUS_MUL", 1.35f);
    public static float BOSS_PHASE2_TELEGRAPH => CombatTuningTable.Get("BOSS_PHASE2_TELEGRAPH", 2.2f);
    /// <summary>Boss 一阶段技能预警时长</summary>
    public static float BOSS_PHASE1_TELEGRAPH => CombatTuningTable.Get("BOSS_PHASE1_TELEGRAPH", 3f);
    /// <summary>Boss 阶段切换时的预警时长</summary>
    public static float BOSS_PHASE_SHIFT_TELEGRAPH => CombatTuningTable.Get("BOSS_PHASE_SHIFT_TELEGRAPH", 2.5f);
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：Boss 震屏幅度</summary>
    public static float BOSS_SKILL_RELEASE_SHAKE_AMP => CombatTuningTable.Get("BOSS_SKILL_RELEASE_SHAKE_AMP", 0.18f);
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：Boss 震屏时长</summary>
    public static float BOSS_SKILL_RELEASE_SHAKE_DUR => CombatTuningTable.Get("BOSS_SKILL_RELEASE_SHAKE_DUR", 0.26f);

    // ===== Boss 狂暴机制（只加这一种；仅改攻击间隔，不动伤害/属性缩放）=====
    /// <summary>Boss 狂暴总开关。false=完全关闭；true=第 BOSS_ENRAGE_MIN_CHAPTER 章起启用。</summary>
    public static bool BOSS_ENRAGE_ENABLED => CombatTuningTable.GetBool("BOSS_ENRAGE_ENABLED", true);
    /// <summary>狂暴起始章节（含）：前 4 章 Boss 保持原样，第 5 章起才触发。</summary>
    public static int BOSS_ENRAGE_MIN_CHAPTER => CombatTuningTable.GetInt("BOSS_ENRAGE_MIN_CHAPTER", 5);
    /// <summary>触发狂暴的血量阈值（当前/最大 ≤ 此值触发）。0.40=低于 40%。</summary>
    public static float BOSS_ENRAGE_HP_RATIO => CombatTuningTable.Get("BOSS_ENRAGE_HP_RATIO", 0.40f);
    /// <summary>狂暴攻速倍率（>1 即更快）。1.5=攻击速度 +50%（攻击间隔 ÷1.5）。</summary>
    public static float BOSS_ENRAGE_ATK_SPEED_MUL => CombatTuningTable.Get("BOSS_ENRAGE_ATK_SPEED_MUL", 1.5f);

    // =====================================================================
    // ⚠⚠ 流程测试开关（2026-09-23 加）—— 测完必须整段注释掉 / 删掉 ⚠⚠
    // 只影响 Boss 关，纯代码侧，不碰任何配表。
    // ---------------------------------------------------------------------
    /// <summary>Boss 关只出 Boss 本体，不出小怪波（小怪数按 0 处理）。</summary>
    public const bool TEST_BOSS_SOLO = true;
    /// <summary>Boss 血量压到固定值，方便一击通关验证流程。0 或负 = 关闭此开关。</summary>
    public const int TEST_BOSS_HP = 1;
    // =====================================================================

    /// <summary>精英血厚档</summary>
    public static float ELITE_TANK_HP_MUL => CombatTuningTable.Get("ELITE_TANK_HP_MUL", 1.1f);
    public static float ELITE_TANK_ATK_MUL => CombatTuningTable.Get("ELITE_TANK_ATK_MUL", 0.92f);
    public static float ELITE_TANK_TELEGRAPH => CombatTuningTable.Get("ELITE_TANK_TELEGRAPH", 3.2f);
    /// <summary>精英血薄档</summary>
    public static float ELITE_GLASS_HP_MUL => CombatTuningTable.Get("ELITE_GLASS_HP_MUL", 0.75f);
    public static float ELITE_GLASS_ATK_MUL => CombatTuningTable.Get("ELITE_GLASS_ATK_MUL", 1.35f);
    public static float ELITE_GLASS_TELEGRAPH => CombatTuningTable.Get("ELITE_GLASS_TELEGRAPH", 2.0f);
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：精英震屏幅度</summary>
    public static float ELITE_SKILL_RELEASE_SHAKE_AMP => CombatTuningTable.Get("ELITE_SKILL_RELEASE_SHAKE_AMP", 0.12f);
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：精英震屏时长</summary>
    public static float ELITE_SKILL_RELEASE_SHAKE_DUR => CombatTuningTable.Get("ELITE_SKILL_RELEASE_SHAKE_DUR", 0.20f);
    /// <summary>公会等级系数：0.02×公会等级</summary>
    public static float GUILD_SCALE_PER => CombatTuningTable.Get("GUILD_SCALE_PER", 0.02f);

    [Header("战斗配置")]
    /// <summary>
    /// 怪物伤害倍率（数值表已校准）。
    /// 2026-09-26 主人反馈「玩家受击只掉 1 血、开不开护盾都是 -1」：
    /// monster_stats 的 baseAttack 只有 3.6~13.7，而玩家防御起步就有 10.5（剑盾）+ 体力 5 + 装备，
    /// DamageFormula.FinalHit 的减法结果长期 ≤ 0，被 MinDamage=1 钳成 1 —— 不是护盾没接，是根本没得可减。
    /// 【原值 1f → 新值 3f】第 1 章杂兵 raw 26~41：剑盾（防≈20）净伤 6~21，法师净伤 16~31。
    /// 这是唯一的全局旋钮（Monster.cs 攻击赋值处），Boss 同步放大（原净伤≈18 → 现≈93），嫌狠就往下调。
    /// </summary>
    public const float MONSTER_DAMAGE_MULTIPLIER = 3f;
    public const float STAGE_LENGTH = 20f; // 每关长度20单位，走到头通关
    public const int EQUIP_CHOOSE_COUNT = 3; // 每关结束三选一装备
    public const int MAX_EQUIP_SLOT = 7; // 身上装备槽位数量：头/胸/手/脚/披风/主手/副手（已改为包内同部位唯一，无穿戴槽）

    [Header("难度 / 金币副本")]
    /// <summary>通关满 N 章后开启困难</summary>
    public const int DIFF_HARD_NEED_CLEARS = 3;
    /// <summary>
    /// 难度档位数量：普通 / 困难 / 噩梦 = 3。
    /// 2026-09-17 改动：
    /// ① **删除地狱**——原「地狱」与噩梦共用同一个数值分支（<c>diff &gt;= 2</c>），是空壳，已彻底移除；
    /// ② **噩梦不再按通关章节数解锁**，改判「在困难难度下通关第 8 章」，
    ///    存 <c>SaveData.hardCleared</c>（原 DIFF_NIGHTMARE_NEED_CLEARS 已删除）。
    /// </summary>
    public const int DIFF_COUNT = 3;
    /// <summary>金币副本通关固定金：基数 × 章节 × 难度倍率。2026-09-15 产出去零：300 → 30。</summary>
    public const int GOLD_DUNGEON_CLEAR_BASE = 30;

    /// <summary>
    /// 难度属性倍率（怪物血/攻/防）。2026-09-17：噩梦 1.8 → **2.0**（地狱删除后噩梦就是顶档）。
    /// 防"无限难"的红线：只调这一个数，不新增乘区；怪物抗性封顶 50%。
    /// </summary>
    public static float GetDifficultyStatScale(int diff)
    {
        if (diff >= 2) return 2.0f;
        if (diff == 1) return 1.35f;
        return 1f;
    }

    /// <summary>难度掉金倍率。2026-09-17：噩梦 3.0 → **3.5**。</summary>
    public static float GetDifficultyGoldMul(int diff)
    {
        if (diff >= 2) return 3.5f;
        if (diff == 1) return 1.8f;
        return 1f;
    }

    public static int GetGoldDungeonClearGold(int chapter, int diff)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        return Mathf.RoundToInt(GOLD_DUNGEON_CLEAR_BASE * ch * GetDifficultyGoldMul(diff));
    }

    /// <summary>
    /// 装备折金（唯一口径：多余奖励件折金 / 穿装失败折金 / 主动分解都走这里）。
    /// 2026-09-15 产出去零：原 rarity * 5 * (1 + star) → 整体 ÷10。
    /// 档位（满星）：白1 / 绿3 / 蓝6 / 紫10 / 橙15 金。
    /// 设计理由：分解金是"装备塞不下"的兜底，必须显著低于卖一件装备的商店价，
    /// 否则后期刷装备分解会反超打怪，把战斗产出挤成零头。
    /// </summary>
    public static int EquipScrapGold(Rarity rarity, int star)
    {
        int s = star < 0 ? 0 : star;
        int v = (int)rarity * (1 + s) / 2;
        return v < 1 ? 1 : v;
    }

    [Header("怪物章节文件夹映射")]
    /// <summary>
    /// 章节对应的怪物文件夹名（在 Icons/default size/no shadow/ 下）
    /// 同时也是战斗背景文件夹名（在 Assets/Art/UI/background/ 下）
    /// </summary>
    public static readonly string[] ChapterMonsterFolders = new string[]
    {
        "1 Forest",   // 第1章 森林
        "2 Undead",   // 第2章 墓园
        "3 Jungle",   // 第3章 雨林
        "4 Field",    // 第4章 草原
        "5 Sea",      // 第5章 海岛
        "6 Cave",     // 第6章 洞穴
        "7 Devil",    // 第7章 熔岩
        "8 Ice"       // 第8章 冰川
    };

    /// <summary>与 ChapterMonsterFolders 一一对应的地图显示名</summary>
    public static readonly string[] ChapterMapNames = new string[]
    {
        "暮影森林", // Forest
        "幽冥墓园", // Undead
        "翡翠秘境", // Jungle
        "晨曦草原", // Field
        "海岛遗迹", // Sea
        "巨岩深窟", // Cave
        "赤焰炼狱", // Devil
        "永霜雪境"  // Ice
    };

    public static string GetChapterMapName(int gameChapter)
    {
        if (ChapterThemeMapTable.HasData)
            return ChapterThemeMapTable.GetMapName(gameChapter);
        int idx = Mathf.Clamp(gameChapter - 1, 0, ChapterMapNames.Length - 1);
        return ChapterMapNames[idx];
    }

    public static string GetChapterTitleText(int gameChapter)
    {
        string[] cn = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        int idx = Mathf.Clamp(gameChapter - 1, 0, cn.Length - 1);
        return $"第{cn[idx]}章  {GetChapterMapName(gameChapter)}";
    }

}
