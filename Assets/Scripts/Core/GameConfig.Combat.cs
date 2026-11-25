using UnityEngine;

/// <summary>
/// GameConfig 的 战斗数值 / 射程 / 怪物 / 难度 部分（2026-09-26 从 GameConfig.cs 按域拆出，partial 同类型）。
/// 只搬位置，**数值与调用点一字未改**；改这里的常量 = 改原 GameConfig。
/// </summary>
public static partial class GameConfig
{
    [Header("武器攻击范围(像素) — 对齐《像素冒险_数值表》武器属性表")]
    public const float RANGE_PX_SWORD = 96f;      // 单手剑
    // 【2026-10-11 主人拍板】原 144 → 120：主人实测狂战 / 重武「攻击范围有点远」，拍板统一压到 120。
    // ⚠ 本常量只是**表读不到时的兜底**，真源是 attack_range 表的 weapon_greatsword —— 两边必须同值，
    // 别让兜底和表各说一套（同 MONSTER_RANGED_RANGE_MUL 的口径）。
    public const float RANGE_PX_GREATSWORD = 120f; // 大剑/双手斧/双手锤
    public const float RANGE_PX_POLEARM = 180f;   // 长柄
    public const float RANGE_PX_STAFF = 156f;     // 单手杖
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
        /// <summary>
        /// 【2026-10-19 主人报：第 2 章剧情被进关抽奖面板 4 个按钮盖住且点不动 → timeScale=0 全场定格，只能退出重进】
        /// 【2026-10-20 主人拍板「剧情永远置顶」：1500 实测仍被技能槽(955)挡 —— 不纠结同层 order，
        /// 直接挪进全项目最高 sorting layer「Effects」+ order 13000（高于 FullscreenFx 12000），
        /// 整层压过 Default 层的一切（战斗 HUD/技能槽/抽奖面板/Toast/Loading 全在 Default）。
        /// 配套：剧情期间战斗底部技能槽由 StoryDirector 隐藏、收窗还原。】
        /// </summary>
        public const int StoryDialogue = 13000;
        /// <summary>全项目最高 sorting layer（TagManager 顺序：Default &lt; Background &lt; Ground &lt; Monster &lt; Player &lt; Effects）。剧情窗专用。</summary>
        public const string TopLayer = "Effects";
        /// <summary>Unity 默认 sorting layer：战斗人物气泡降层用（2026-11-24）。</summary>
        public const string DefaultLayer = "Default";
        public const int StoryNaming = 560;
        public const int TutorialHint = 600;
        /// <summary>战斗人物头顶气泡（BattleHeadTalkUI）：2026-11-24 主人拍板「不要比佣兵信息等弹窗层级高」，
        /// 从 Effects/13001 降到 Default/899 —— 低于一切弹窗（TownPopup 900 起），弹窗开着时由弹窗压住气泡。</summary>
        public const int BattleHeadTalk = 899;
        public const int TownPopup = 900;
        /// 通用「获得奖励」弹窗：必须高于 TownPopup(900)——否则与每日登录界面同为 900 时排序不定、会被盖住；低于 BattlePopup(920) 与 Toast(11000)。
        public const int RewardPopup = 905;
        /// <summary>
        /// 【2026-10-19 主人报：设置弹窗点「清除数据」后确认框看得见点不动】
        /// 通用确认弹窗（清除数据 / 体力领取等，StaminaConfirmPopup）：必须严格高于 TownPopup(900) 与 RewardPopup(905)——
        /// 从设置弹窗（同为 900）里打开时排序不定，点击射线被设置弹窗全屏 Dim 吃掉；低于 BattlePopup(920)。
        /// </summary>
        public const int TownConfirm = 910;
        public const int BattleStageMap = 880;
        public const int BattlePopup = 920;
        /// <summary>
        /// 【2026-11-11 主人拍板「一进战斗的半透剧情提示」】战斗开场氛围横幅（BattleAmbienceBanner）：
        /// 半透遮罩 + 本章铺垫句，须盖住战斗 HUD(950) 与抽奖面板(965)，仍低于剧情窗(13000)——
        /// 横幅播完才弹战前剧情窗，两层不重叠出现。取 928：弹窗段之内、EquipReplaceConfirm(925) 之上。
        /// </summary>
        public const int BattleAmbience = 928;
        /// <summary>
        /// 【2026-11-20 主人拍板「黑夜不能压面板，面板要独立」】昼夜径向暗图专用层：
        /// 只压场景（map 10 / 单位 15 / 特效 <=82），停在底部面板(100)之下 → 头像、抽奖按钮、装备栏夜战不被压暗。
        /// 面板独立靠的是暗图让路；不再抬面板 Canvas 去躲暗图（两条嵌套 Canvas 同为 935 会排序冲突、吞掉头像与抽奖内容）。
        /// </summary>
        public const int BattleNightVignette = 90;
        /// <summary>
        /// 【2026-10-20 主人报「技能槽怎么又跑到前面来了」】恢复关「生命恢复」弹窗。
        /// 技槽嵌套 Canvas 固定抬在 BattlePopup 之上（=921），弹窗用 920 会被技槽穿到前面；
        /// 且恢复关已改为在关卡界面上直接弹（不进战斗），须同时盖住关卡图(880)与老虎机(900)。
        /// 取 930：高于技槽(921)与 EquipReplaceConfirm(925)，仍属弹窗段。
        /// </summary>
        public const int RestStagePopup = 930;
        /// <summary>
        /// 【2026-10-06 主人拍板】替换装备确认弹窗（Resources/Prefabs/Battle/EquipReplaceConfirmPopup）。
        /// 必须高于 <see cref="BattlePopup"/>（抽奖面板那一层），否则宣布白拿的弹窗会被压在面板下面看不见。
        /// </summary>
        public const int EquipReplaceConfirm = 925;
        /// <summary>
        /// 【2026-10-11】抽奖概率公示弹窗（Resources/Prefabs/Battle/DraftOddsPopup）。
        /// 从抽奖面板（BattlePopup 920）上的「!」按钮打开 → 必须高于 920，否则被面板压住看不见。
        /// </summary>
        public const int DraftOddsPopup = 926;
        public const int BattleLegacyPool = 940;
        public const int BattleHud = 950;
        /// <summary>
        /// 进关抽奖面板 / 老虎机（BattleEntryDraftPanel / SlotMachineUI）：2026-10-15 改摄像机模式后，
        /// 层级须高于 <see cref="BattleHud"/>(950) 才盖得住 HUD（2026-09-30 主人实测「看不见抽奖按钮」），
        /// 低于 BattleMilestone(970)。常量名沿用旧面板 SlotOfferUI（2026-10-17 已删）。
        /// </summary>
        public const int SlotOffer = 965;
        public const int BattleLegacyChoose = 960;
        public const int BattleMilestone = 970;
        public const int BattleEvacuate = 980;
        /// <summary>
        /// 【2026-10-24 主人拍板】战斗结果界面（预制体 Resources/Prefabs/Battle/战斗结果，BattleResultScreen 驱动）：
        /// 胜利/失败大图 + 特效，战斗结束先播它再进各自结算。高于 BattleHud(950)/BattleEvacuate(980)，
        /// 低于 BattleSettlement(990)（播完即毁，不与结算面板叠）。
        /// ⚠ 与预制体粒子 Sorting Order 耦合：画布=985 →「界面下」粒子 984、「界面上」粒子 986（主人已在预制体调好）。
        /// </summary>
        public const int BattleResult = 985;
        /// <summary>
        /// 通用分享界面（SharePanelUI，2026-10-25 主人拍板）：盖过战斗结果(985)（从胜利界面点「分享」弹出）、
        /// 低于结算面板(990)。预制体 meta 写的 985 由 ApplyPopup 覆盖到此值。
        /// </summary>
        public const int SharePanel = 987;
        public const int BattleSettlement = 990;
        /// <summary>
        /// 「佣兵入队」横幅（MercJoinBannerUI，2026-10-12 新增）：抽中佣兵瞬间播一次，
        /// 须盖住抽奖面板(965)/结算(990)等一切 → 抬嵌套 Canvas 到结算之上。
        /// 【2026-10-18】原写死 995 魔数，收敛为常量（铁律：不许魔数）。
        /// </summary>
        public const int MercJoinBanner = 995;
        /// <summary>
        /// 「恭喜升级」弹窗（LevelUpPopup，2026-10-25 主人拍板「接玩家升级」）：
        /// 战斗结算挂起、回到冒险界面（Town 就绪）才弹出，须盖住主界面各功能页，低于 Toast(11000)。
        /// </summary>
        public const int LevelUpPopup = 996;
        public const int Loading = 10000;
        public const int Toast = 11000;
        /// <summary>「获得佣兵」全屏演出（2026-10-20 主人拍板）：盖过 Toast 与奖励弹窗，低于剧情(Effects 层 13000)。</summary>
        public const int MercGainCinematic = 11500;
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
    /// <summary>普通小怪整体放大 20%（主人 2026-11-20 拍板）。只作用于普通怪 rootScale，精英/Boss 走各自倍率不变。
    /// 放在 MONSTER_SCALE_MIN/MAX 之上乘，不写死新数值。</summary>
    public const float MONSTER_NORMAL_SCALE_MUL = 1.2f;
    /// <summary>Boss 是否参与 MONSTER_SMALL_SHRINK 缩小。
    /// 【2026-10-20 主人拍板「boss有点大 锁小30%」】打开此开关 = Boss 与小怪同吃 ×0.7（覆盖
    /// 2026-09-26「Boss 先不缩」的旧口径）。true = 不再把 0.7 除回去，Boss 视觉体型整体缩 30%。
    /// 只动这一处：BOSS_SCALE_MULTIPLIER（表值 2.6）保持不动 —— 它还兼作 Monster.Init 判
    /// Boss/精英档位的阈值，改表值会把精英误判成 Boss。</summary>
    public const bool MONSTER_BOSS_APPLY_SMALL_SHRINK = true;
    public const float MONSTER_SCALE_MIN = 3.75f * MONSTER_SMALL_SHRINK;
    public const float MONSTER_SCALE_MAX = 4.5f * MONSTER_SMALL_SHRINK;
    public const float MONSTER_CHILD_REF_SCALE = 1f;
    /// <summary>Monstersmoban / ani 片段已按 scale=1 录制，不再做 Canvas→战斗缩放</summary>
    public const float MONSTER_PREFAB_MONSTERS_SCALE = 1f;
    public const float MONSTER_ANCHOR_SCALE_FACTOR = 1f;
    /// <summary>
    /// 【2026-11-10 主人拍板「空中单位要和阴影有一段空隙 不用太大 能看出来就行」】
    /// 空中怪（monster_stats.airUnit=1）身体相对移动根的上抬高度（本体 local 单位），
    /// 脚下阴影由 Monster.Init.ApplyAirHoverOffset 反向补偿留在地面。
    /// 只影响视觉（本体 localPosition），移动/判定仍走 _bodyRoot。量级参考：怪根 scale≈2.6~4.5、阴影宽≈0.9。
    /// 【2026-11-15 主人口径「浮空的距离减一半」】0.3 → 0.15。
    /// </summary>
    /// 空中怪按档上抬（主人 2026-11-20 拍板、17:55 再拍板「减少一半」）：这是【世界单位】的视觉抬升，
    /// 三档 0.1 / 0.25 / 0.4（= 原 0.2/0.5/0.8 减半，对应约 10/25/40 屏幕像素）。
    /// 【2026-11-20 修】ApplyAirHoverOffset 会先除以 MoveRoot 根缩放再写 localPosition.y —— 否则被 3~8 倍
    /// 根缩放放大成 Boss 抬 6.5 世界单位（飞出屏幕、视觉与判定脱节、打不到）。
    /// 阴影由 ApplyAirHoverOffset 反向补偿留地面，不跟着抬。</summary>
    public const float MONSTER_AIR_HOVER = 0.1f;
    public const float MONSTER_AIR_HOVER_ELITE = 0.25f;
    public const float MONSTER_AIR_HOVER_BOSS = 0.4f;
    /// <summary>空中怪漂浮缓动（2026-11-20 主人「站立和行走加上下缓动表示漂浮」）：
    /// 幅度是【世界单位】，绕 hover 基线上下正弦浮动，各体型视觉一致（写 local 前除以根缩放）。
    /// 周期 = 2π / MONSTER_AIR_BOB_SPEED ≈ 3.5s；每只怪按实例 id 错开相位，避免整齐划一。</summary>
    public const float MONSTER_AIR_BOB_AMP_WORLD = 0.06f;
    public const float MONSTER_AIR_BOB_SPEED = 1.8f;
    // 2026-09-21：精英/Boss 体型倍率已迁 combat_tuning 表（可运行时调）。
    // 【2026-10-10 主人拍板】精英 1.3 → 2.0（「精英的大小应该放大几倍」）；Boss 等比 1.6 → 2.6 保持 Boss > 精英。
    public static float ELITE_SCALE_MULTIPLIER => CombatTuningTable.Get("ELITE_SCALE_MULTIPLIER", 2.0f);
    public static float BOSS_SCALE_MULTIPLIER => CombatTuningTable.Get("BOSS_SCALE_MULTIPLIER", 2.6f);
    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】第 5 关小 Boss 降配：正式关 currentStageIndex（0-based）==4 的
    /// Boss 波，Boss 模板血/攻 ×0.6；第 10 关大 Boss（==9）不乘，精英/普通怪不乘。
    /// 消费点唯一：Monster.Init 的 Boss 血攻装配处。
    /// </summary>
    /// <summary>
    /// 【2026-10-24 主人拍板「章末大Boss 独立档」】正式关章末 Boss（B1 后 stageIdx0==4 就是章末大 Boss）
    /// 的血/攻倍率。旧 MINI_BOSS_STAT_MUL=0.6 是给「第 5 关小 Boss」配的，B1 后小 Boss 位不存在、
    /// 大 Boss 一直被误吃 ×0.6 —— 本档恢复满配 1.0（想再压/抬只改这一个数，如 1.2 = 大 Boss 再厚两成）。
    /// 无限裂隙不吃（走 EndlessDifficultyMul / ENDLESS_BIGBOSS_HP_MUL 自己的两条线）。
    /// </summary>
    public const float BOSS_FINAL_STAT_MUL = 1.0f;
    /// <summary>
    /// 【2026-11-05 主人拍板 / 2026-11-23 攻改 0.8】1-5 及其后所有 Boss：攻击 -20%（0.8）、血量 x2。
    /// 真源只此一处，消费点唯一 = Monster.Init 最终写 attr 处（物理 / 魔法两条攻击线都吃）；
    /// 普通怪 / 精英不受影响，副本 / 裂隙里的 Boss 同样生效。
    /// </summary>
    public const float BOSS_ATTACK_MUL = 0.8f;
    public const float BOSS_HP_MUL = 2.0f;
    /// <summary>
    /// 【2026-11-23 主人拍板】章末大 Boss（stageIdx0=9）专属血量倍率，只乘血、不动攻。
    /// 消费点唯一 = Monster.Init 的 chapterFirstHpRule 与 bossStatMul 两处 baseHp 写入。
    /// 1.3 = 章末大 Boss 再厚三成，配合 BOSS_ATTACK_MUL 0.8 让收尾扛得住四人齐射。
    /// </summary>
    public const float BOSS_FINAL_HP_MUL = 1.3f;
    /// <summary>
    /// 【2026-11-23 主人拍板「每关稳定出、由少到多」】正式主线关每关词条怪配额档（章内关序 0 基）：
    ///   第 1 关 = 0（只有 1/2 号普通小怪，1 近 1 远）；
    ///   第 2 关起逐关递增 1~2 / 2~3 / 3~4 … 第 7 关起封顶 6~7（11-15「每两关+1」会让第2、3关同档，由本次覆盖）。
    /// 真源只此一处；消费点唯一 = WavePlanner.InitAffixQuota。
    /// </summary>
    public static void AffixQuotaRange(int stageIdx, out int min, out int max)
    {
        if (stageIdx <= 0) { min = 0; max = 0; }
        else
        {
            min = Mathf.Clamp(stageIdx, 1, 6);              // 1,2,3,4,5,6,6,6,6（逐关+1，封顶6）
            max = Mathf.Min(7, min + 1);                    // 2,3,4,5,6,7,7,7,7
        }
    }
    /// <summary>主线每关词条怪编号池上限：第 2 关起逐关 +1，封顶 6（3~6 为词条段）。
    /// 【2026-11-23 改】旧「每两关+1」让第2/3关都只有编号3一种词条；现在第2关编号3、
    /// 第3关编号3/5（两种）、第4关起编号3/5/6（三种），让玩家由少到多认全词条。
    /// 4 号 minWave=9 只在末节入池，故前期实际是 3/5/6 三条。</summary>
    public static int AffixSpriteMaxForStage(int stageIdx)
        => Mathf.Min(6, 3 + Mathf.Max(0, stageIdx));
    /// <summary>主线每波词条怪上限：第 2 关 1 只 / 第 3 关 2 只 / 第 4 关起 3 只。
    /// 【2026-11-23 改】旧「每3关+1」让第2~4关都只 1 只（第3关因开场波排除只剩末波1只，玩家看不到），
    /// 现在逐关 +1 封顶 3，配合配额曲线兑现「由少到多」。裂隙不吃这里。</summary>
    public static int AffixWaveCapForStage(int stageIdx, int waveMonsterCount)
        => Mathf.Min(waveMonsterCount, Mathf.Clamp(stageIdx, 1, 3));
    /// <summary>2026-11-12 主人拍板：每关词条怪上限 = 本关正常波总怪数 × 比例。
    /// 【2026-11-15 改】5% → 25%：5% 会把 AffixQuotaRange 的递增曲线压成恒 1 只（本关总怪数 28~100，
    /// 5% 只有 1~5 只），「数量由少变多」兑现不了；25% 下封顶 ≥ qMax(6)，配额完全由曲线决定，
    /// 实际投放仍受 AffixWaveCapForStage 每波上限约束，不会满屏词条。</summary>
    public const float AFFIX_QUOTA_RATIO = 0.25f;
    /// <summary>引导关精英固定精灵编号（1 Forest 第 7 号 = 树精）。主人 2026-10-10 反馈精英不许是蘑菇，表可调。</summary>
    public static int TUTORIAL_ELITE_SPRITE_ID => Mathf.RoundToInt(CombatTuningTable.Get("TUTORIAL_ELITE_SPRITE_ID", 7f));
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
    /// 仅玩家单人战斗（不生成/显示佣兵）。正式局默认 false；
    /// 引导关的佣兵（塔克 H003）由第 2 抽招募入队，不再单独刷（2026-10-06 主人拍板）。
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
    /// <summary>
    /// 怪物远程射程倍率（相对数值表弓射程）；**只在 attack_range 表读不到时兜底**。
    /// 真源是表：表内 monster_ranged / monster_bow 现在写 230（2026-10-07 主人拍板拉开远程射程），
    /// 300 × 0.767 ≈ 230 —— 与表同值，别让兜底和表各说一套。
    /// </summary>
    public const float MONSTER_RANGED_RANGE_MUL = 0.767f;
    /// <summary>【2026-11-13 主人拍板】远程普通小怪攻击距离减 20%；唯一消费点 Monster.Init.cs 的 atkRange。</summary>
    public const float MONSTER_NORMAL_RANGED_RANGE_MUL = 0.8f;
    /// <summary>普通（非精英/非Boss）远程小怪的技能伤害折扣：技能只是为了看得到子弹，不该秒人</summary>
    public static float MONSTER_NORMAL_SKILL_DAMAGE_MUL => CombatTuningTable.Get("MONSTER_NORMAL_SKILL_DAMAGE_MUL", 0.55f);
    /// <summary>
    /// 【2026-11-16 主人拍板「不要一上来就攒槽，用血量触发」】精英 / Boss 的主动技能改为血量档位触发：
    /// 血量每下降这么一档触发一次（0.2 = 每掉 20% 一次，满血进场不放，一场最多 4 次）；
    /// 触发后照常走 _skillCooldown，CD 期间继续掉血不补放，CD 结束若血量还压在下一档之下再补一次。
    /// 普通小怪仍按时间攒槽（0.28/s），不受本常量影响。
    /// </summary>
    public const float MONSTER_HP_SKILL_STEP = 0.2f;
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
    /// <summary>【2026-11-13 主人拍板】所有敌人（怪物）最终移速减 20%；唯一消费点 Monster.Init.cs 的 moveSpd；jitter / 词缀基于降后的值，入场速度另由 MONSTER_ENTER_SPEED_MUL 补回。</summary>
    public const float MONSTER_GLOBAL_MOVE_SPEED_MUL = 0.8f;
    /// <summary>
    /// 【2026-10-11 主人拍板】入场速度 = 怪物自身 MoveSpeed × 本系数。
    /// 旧实现是死的 1.0（`MONSTER_ENTER_SPEED`），而怪物常态移速只有 0.3456 → 入场比自己走路快 2.9 倍，
    /// 看着像「飞过来」再急刹。挂自身移速后又直除太慢（屏外到交战点约 3 个世界单位，
    /// 纯自身速度要 9 秒以上，一波到齐严重拖节奏），故保留一个「比走路略快」的倍率。
    /// 2.5 → 约 0.69 世界单位/秒：11-13 常态移速降到 0.27648 后用它补回改动前的入场速度（仍是他走路的 2.5 倍，观感是跑不是飞）。
    /// <b>调快慢只改这一个出口。</b>
    /// </summary>
    public const float MONSTER_ENTER_SPEED_MUL = 2.5f;
    /// <summary>
    /// 【2026-10-05 主人拍板「多点出生点」】同波第 i 只怪的入场起点比第 0 只再往外错开多少（世界单位）。
    /// 一整波不再从屏外同一个点排队进场，而是从远近不同的位置一起涌进来。
    /// 不能复用 MONSTER_WAVE_SPACING(0.72)：入场要走的绝对距离有 3 个世界单位上下，按 0.72 累加到最后几只
    /// 会晚到十几秒，观感变成「一只一只慢慢来」而不是「一波压过来」。
    /// 0.18 时最远那只比第一只晚约 (i×0.18)/入场速度 秒抵达（入场速度见 `MONSTER_ENTER_SPEED_MUL`）。
    /// </summary>
    public const float MONSTER_ENTER_DEPTH_STEP = 0.18f;
    /// <summary>入场起点比交战点再远多少（世界单位）；过大容易出场「往前窜」</summary>
    public const float MONSTER_ENTER_DISTANCE = 1.2f;
    /// <summary>
    /// 【2026-10-20 主人拍板「多出生点随机抽」】出生点池的车道槽数：把 walk 可行走区切成这么多格。
    /// 取 5 是<b>槽密度与贴边风险的取舍</b>：walk 框实测只有 1.335 高，上下各内缩
    /// `MONSTER_SPAWN_LANE_EDGE_PAD`(0.18) 后可用高度只剩 0.975 —— 切 9 槽每槽仅 0.108，
    /// 怪挤成一坨且仍然贴边；切 5 槽每槽 0.195，既不贴边也不会全叠在一起。
    /// 槽数不再等于一波人数上限（同槽靠 `TryTakeSpawnPoint` 的间距校验兜底）。
    /// <b>调车道密度只改这一个出口。</b>
    /// </summary>
    public const int MONSTER_SPAWN_LANE_SLOTS = 5;
    /// <summary>
    /// 【2026-10-20 主人拍板「多出生点随机抽」】车道槽上下各往里收多少（世界单位）：
    /// 用来防止最上 / 最下槽贴到 walk 框的上下边，导致怪站到地面外面（主人实测「有的怪跑到上面去了」）。
    /// 走 `BattleLaneBounds.GetLaneSlots` 生效；不内缩时最上槽中心的世界 Y 约 1.082，已顶到框顶 1.156。
    /// <b>调上下留白只改这一个出口。</b>
    /// </summary>
    public const float MONSTER_SPAWN_LANE_EDGE_PAD = 0.18f;
    /// <summary>
    /// 【2026-10-20 主人拍板「多出生点随机抽」】进场深度三档：近 / 中 / 远（世界单位，累加在
    /// `SpawnMonsterOffscreenEnter` 的屏外余量之上）。
    /// 故意比原 `MONSTER_ENTER_DEPTH_STEP`(0.18) × 8 = 1.44 更近：最远只到 0.6，是<b>刻意保守</b>，
    /// 保证最远那只也不会明显晚到，不破坏「一波压过来」的观感。
    /// 【2026-10-22 主人拍板「出怪有点慢，把出生点提前点」】{0,0.45,0.9} → {0,0.3,0.6}，
    /// 与 `SpawnMonsterOffscreenEnter` 的屏外余量(1.35→0.8)一起收紧，出生点整体前移。
    /// <b>调远近只改这一个出口。</b>
    /// </summary>
    public static readonly float[] MONSTER_SPAWN_DEPTH_TIERS = { 0f, 0.3f, 0.6f };
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
    /// 吸血：【2026-10-09 主人拍板】玩家/我方**不再自带吸血**，本基础比例归零。
    /// 只有装备词缀 / 技能真的带「吸血」（AttrType.LifeSteal，配表 0.02~0.05 的比例值）时才回血；
    /// 一点吸血都没带就是 0，不回血。
    /// 想恢复旧行为（一上来人人 10% 吸血）：把这个常量改回 0.1f 即可。
    /// </summary>
    public const float LIFESTEAL_RATIO = 0f;

    /// <summary>
    /// 魔法防御的**基础真源**：属性系统初始化时写入 AttrType.MagicDefense，之后由装备/词缀往上叠。
    /// 2026-09-27 主人拍板：防御拆成物理(Defense)与魔法(MagicDefense)两条，魔法防御是**独立属性**，
    /// 不再由物理防御换算（原 MAGIC_DEFENSE_FALLBACK_RATIO 兜底已删除）。
    /// 主人还没给数值 → 先 0（目标魔法防御为 0 时，魔法伤害打上去就是全额）。
    /// </summary>
    public const float BASE_MAGIC_DEFENSE = 0f;
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
    /// 全局怪物 HP 倍率（主线/引导线；【2026-10-22 主人拍板「无限裂隙先排除了 分成两条线」】
    /// 无尽裂隙不吃这条，裂隙强度只随层数走）。1.0 = 直接用 monster_stats 表的 baseHp。
    /// 曾设为 0.6（减 40%），导致图鉴写「生命 78」而实战只有 46（当时图鉴没跟着乘，现已乘回）。
    /// 【2026-10-22 主人拍板「把敌人血量也砍50%」】1.0 × 0.5 = 0.5：主线全体敌人
    /// （普通/精英/Boss/引导）血量整体砍半；冒险图鉴血量预览走同一常量，显示与实战一致。
    /// 要整体调难/调易只改这里即可。
    /// </summary>
    public const float MONSTER_HP_GLOBAL_MUL = 0.5f;

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
    /// 【0.65 → 0.39】2026-09-28 主人要求「敌人的攻击频率有点快，再减少 40%」：0.65 × 0.6 = 0.39。
    /// 只动这一个数；下面远程怪那档 ÷0.6 会一起跟着降，远近都比原来慢 40%。
    public const float MONSTER_ATK_SPEED_MUL = 0.39f;
    /// <summary>
    /// 2026-09-28 旧口径「远程敌人的攻击要比近战高」。
    /// 【2026-10-22 主人拍板「攻击再减50%」】主人报首波全远程且攻击还很高 → 1.35 × 0.5 = 0.675，
    /// 覆盖旧口径（远程不再上浮，反而比近战低一截）。只作用于敌方远程，Boss 不吃（Boss 有自己的 BOSS_TTK_ATK_MUL）。
    /// 嫌远程太痛/不够痛只改这一个数。
    /// </summary>
    public const float MONSTER_RANGED_ATK_MUL = 0.675f;
    /// <summary>
    /// 仅敌方弓/法球普攻频率倍率（1=表值；0.5=再降一半）。
    /// 我方（玩家职业表 AttackInterval、佣兵花名册 AtkSpeed）不再叠这个，否则 0.5 秒间隔会变成 1 秒。
    /// 【0.5 → 1.0】2026-10-12 主人报「一大波远程敌人攻速太慢，半天打不了一次」：
    /// 叠满后远程怪实际 = 0.39 ÷0.6 ×0.5 ≈ 0.33 次/秒（3 秒一发）。改 1.0 = 撤掉这半档，
    /// 远程回到 0.65 次/秒（约 1.5 秒一发），仍保留 9-28「全体降 40%」的口径。要再调只动这一个数。
    /// </summary>
    public const float PROJECTILE_ATK_SPEED_MUL = 1.0f;

    /// <summary>
    /// 玩家（Hero）攻击速度倍率，0.8 = 出手间隔变慢 20%。
    /// 只作用于 Hero，佣兵与怪物不受影响（在 UnitBase.GetAttackCooldown 的 is Hero 分支里乘）。
    /// 与武器种族系数、连杀加速、被动攻速是乘法叠加。
    /// </summary>
    public const float PLAYER_ATTACK_SPEED_MUL = 0.8f;

    /// <summary>
    /// 佣兵（Mercenary）攻击速度倍率，0.29 = 累计出手间隔变慢 71%。
    /// 【2026-11-05 主人拍板「佣兵攻速比玩家快太多」】只给佣兵降，怪物节奏不变
    /// （在 UnitBase.GetAttackCooldown 的 is Mercenary 分支里乘，与玩家 0.8 同口径）。
    /// 【2026-11-23 主人「再减，感觉佣兵比玩家攻速还快」】0.7×0.7=0.49。
    /// 【2026-11-23 主人「我和佣兵都是游侠，我比佣兵慢」→ 拍板方案A压倍率】0.49→0.29：
    ///   玩家游侠稳定态 ≈0.466 次/秒（表间隔1.24→0.806×弓0.85×玩家0.8×游侠0.85）；
    ///   游侠佣兵基础 1.40~1.60 直接是频率，取最高艾拉1.60 → 1.60×0.29=0.464 < 0.466，同游侠玩家略快。
    ///   宠物走 PET_ATTACK_SPEED_MUL 独立分流，不受本值牵连。
    /// 与连杀加速、团队攻速增益技能是乘法叠加。
    /// </summary>
    public const float MERC_ATTACK_SPEED_MUL = 0.29f;

    /// <summary>
    /// 宠物攻击速度倍率：宠物继承 Mercenary 但不吃佣兵的再减，保持 11-23 改前口径 0.7。
    /// 【2026-11-23 主人「我只是让你改佣兵的」】只在 UnitBase.GetAttackCooldown 的 is Mercenary 分支按 Pet 分流。
    /// </summary>
    public const float PET_ATTACK_SPEED_MUL = 0.7f;


    /// <summary>
    /// 玩家（Hero）行走/移动速度倍率。2026-09-28 主人要求「玩家行走速度再加快 10%」→ 1.1。
    /// 只作用于 Hero（在 UnitBase.GetCombatMoveSpeed 里乘），佣兵与怪物不受影响，
    /// 避免把整场推进节奏一起带快。再要调只改这一个数。
    /// 注意：摇杆手动移动是在这个值之上再乘 HERO_MANUAL_MOVE_X_MUL，所以手动/自动会同步变快。
    /// 【2026-11-12 更正】摇杆已停用（零活引用），上面这句只剩历史意义；集结走的是 manual 通道
    /// 但现在<b>不乘</b> HERO_MANUAL_MOVE_X_MUL（乘了会滑步，见 Hero.cs 441 行注释）。
    /// </summary>
    public const float PLAYER_MOVE_SPEED_MUL = 1.1f;
    /// <summary>英雄基础攻击（BaseAtk）全局偏移。负值=整体削弱。-10 即各职业基础攻击 -10，用于手感/平衡微调。</summary>
    public const float HERO_BASE_ATTACK_OFFSET = 0f; // 2026-09-14：不再二次扣 10，职业表 baseAtk 即真源

    /// <summary>
    /// 2026-09-29 主人拍板：天赋「流派·物理专精 / 魔法专精」的**跨系折扣**。
    /// 点**本系**流派满额生效；点**另一系**按此系数打折（0.5 = 只拿一半）。
    /// 设计意图：给玩家一个明确的培养目标（专一养物理或法系），但跨系**不是不能玩、只是要花代价**。
    /// 例：法师点满「物理专精 +25%」实际只拿到 +12.5%。
    /// </summary>
    public const float TALENT_CROSS_PATH_RATIO = 0.5f;

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
    /// 技能冷却总倍率。2026-10-10 主人拍板：<b>以配表 CD 为准 → 值为 1f，配表值即实机值，不要再乘</b>。
    /// （历史：2026-09-26 曾要求 CD 延长 1 倍，后又反馈「恢复还是太快」再延长 1 倍 → ×4；现已回退到 ×1。）
    /// 单点旋钮：玩家技能在 PlayerSkillDefs 装载完统一乘（表 / Fallback / UI 文案同源）。
    /// ⚠️ 佣兵技能<b>不再</b>走本常量 —— 2026-10-10 起改走 <see cref="MERC_SKILL_COOLDOWN_MUL"/>（维持 ×4）。
    /// 决定「玩家/佣兵技能多久能再放一次」的就是它（叠加在 CSV/表的基础 CD 之上）。
    /// </summary>
    public const float SKILL_COOLDOWN_MUL = 1f;

    /// <summary>
    /// 佣兵技能冷却总倍率。<b>与 SKILL_COOLDOWN_MUL 已分家</b>：
    /// 玩家技能 CD 以配表为准 → SKILL_COOLDOWN_MUL = 1f（表值即实机值，不要再乘）；
    /// 佣兵技能 CD 维持历史 ×4 → 本常量 = 4f（表值 × 4），不跟随玩家侧回退。
    /// 2026-10-10 主人拍板：玩家侧回退到 1f 后「佣兵 CD 不要减」，故拆出本常量单独保住 ×4。
    /// 唯一消费点：MercSkillCaster.Bind 的 _normalCooldown。要调佣兵 CD 只改这一个值。
    /// </summary>
    public const float MERC_SKILL_COOLDOWN_MUL = 4f;

    /// <summary>
    /// 佣兵开场首发技能冷却（秒）。2026-11-12 主人拍板：不再按满冷却进场，
    /// 开打（UnitsCanAct 解冻）后先走这么久再放第一发；之后每轮仍走技能固有 CD。
    /// 唯一消费点：MercSkillCaster.Bind。
    /// </summary>
    public const float MERC_SKILL_OPENING_CD = 5f;

    // ============================================================
    // 蓝条（MP）数值 —— 2026-10-06 主人拍板
    // 口径：自然回复 / 耗蓝进技能表 / CD+MP 双门槛 / 每关开局回满。
    // 全部数值只此一处，业务里不许再写数字；公式见 MpProfile。
    // 设计演算：Docs/蓝条数值设计_职业与佣兵_2026-10-06.md §2 / §5
    // ============================================================

    /// <summary>治疗型（类型分类=恢复）基础 MP 池。满池只存 1.25~1.5 发，★1 连喷是不可能的 —— 紧张感的根。</summary>
    public const float MP_POOL_HEAL = 150f;

    /// <summary>法术型（类型分类=法术）基础 MP 池。</summary>
    public const float MP_POOL_MAGIC = 150f;

    /// <summary>玩家（牧师 / 法师，4 个技能共用一条）基础 MP 池。只有佣兵的 2 倍却要供 4 个技能 → 天然更紧。</summary>
    public const float MP_POOL_PLAYER = 300f;

    /// <summary>治疗型每秒回复。回满一发要 83~100 秒（比一关还长）—— 主人拍板「回蓝不要太快」。</summary>
    public const float MP_REGEN_HEAL = 1.2f;

    /// <summary>法术型每秒回复。</summary>
    public const float MP_REGEN_MAGIC = 1.3f;

    /// <summary>玩家每秒回复。</summary>
    public const float MP_REGEN_PLAYER = 1.8f;

    /// <summary>
    /// 【2026-10-10 主人拍板】玩家 MP 池随「攻击」成长的系数：每点攻击 +1 点上限。
    /// 公式：玩家 MP 池上限 = MpProfile.Pool(星级, 等级) + 当前职业攻击 × MP_POOL_PER_ATTACK + 智力 × MP_POOL_PER_INTELLIGENCE。
    /// <b>与智力项叠加（不是替换）</b>：攻击项与智力项各算一份，同时在最终池里生效。
    /// 攻击属性键由 PlayerJobBaseStats.CurrentAttackAttr() 按职业决定，读法见 BattleManager.PlayerAttackPower。
    /// 参考量级（曲线+攻击项）：★1 Lv1 攻击 25 → 325；★5 Lv40 攻击 346 → 1024。
    /// ⚠️ 只对玩家生效：佣兵走 Heal / Magic 原型，仍吃 MP_GROWTH_PER_STAR，与本常量无关。
    /// 叠加在 MpProfile 星级/等级曲线之上，不是替换：攻击项只是附加项，不得整体替换原曲线。
    /// </summary>
    public const float MP_POOL_PER_ATTACK = 1.0f;

    /// <summary>
    /// 【2026-10-10 主人拍板】玩家回蓝随「攻击」成长的系数：每点攻击 +0.015 点/秒。
    /// 公式：玩家回蓝速度 = MpProfile.Regen(星级) + 当前职业攻击 × MP_REGEN_PER_ATTACK + 智力 × MP_REGEN_PER_INTELLIGENCE。
    /// <b>与智力项叠加（不是替换）</b>：攻击项与智力项各算一份，同时在最终回蓝里生效。
    /// 参考量级（曲线+攻击项）：★1 Lv1 攻击 25 → 2.18；★5 Lv40 攻击 346 → 7.85 点/秒。
    /// ⚠️ 只对玩家生效：佣兵回蓝仍走 Heal / Magic 基值 × MP_GROWTH_PER_STAR，与本常量无关。
    /// 叠加在 MpProfile 星级/等级曲线之上，不是替换：攻击项只是附加项；回复曲线当前只随星级成长。
    /// </summary>
    public const float MP_REGEN_PER_ATTACK = 0.015f;

    /// <summary>
    /// 【2026-10-10 追加澄清·恢复】玩家 MP 池随「智力」成长的系数：每点智力 +8 点上限。
    /// 主人澄清：装备上的「智力」词条仍然有效，继续派生魔攻（AttrSystem.ApplyDerivedAttributes 里
    /// MagicPower = intel × 0.01，本轮不动），<b>并且继续喂蓝条</b>；天赋左列简化成攻击只是天赋侧的口径变化。
    /// 智力的来源 = 基础 5 + 每级 +1 + 披风(2 + rarity) + 装备 INTELLIGENCE 词条
    /// （⚠️ 天赋左列自 2026-10-10 起已不再喂智力，改投 Attack / MagicAttack）。
    /// <b>叠加项</b>：最终玩家 MP 池 = MpProfile.Pool(星级, 等级) + 攻击 × MP_POOL_PER_ATTACK + 智力 × 本常量，
    /// 攻击项与智力项<b>各算一份</b>，谁也不替换谁，两者都不是 MpProfile 曲线的替代品。
    /// </summary>
    public const float MP_POOL_PER_INTELLIGENCE = 8f;

    /// <summary>
    /// 【2026-10-10 追加澄清·恢复】玩家回蓝随「智力」成长的系数：每点智力 +0.03 点/秒。
    /// <b>叠加项</b>：最终玩家回蓝 = MpProfile.Regen(星级) + 攻击 × MP_REGEN_PER_ATTACK + 智力 × 本常量。
    /// ⚠️ 为什么恢复时是 0.03 而<b>不是</b>原值 0.1：耗蓝事实（player_skills.csv「耗蓝」列）里 6 个玩家技能
    /// 只有 2 个耗蓝 —— 治愈之泉 100（CD 12s）、天雷裁决 120（CD 25s），其余 4 个耗蓝为 0；
    /// 两个都装时平均需求 ≈ (100+120)/25 = 8.8 蓝/秒。攻击项已经贡献了大部分回蓝，
    /// 若智力仍按 0.1，Lv40★5 法系回蓝会到 ≈14.85/秒，远超 8.8/秒 的实际需求
    /// → 蓝条彻底不再是瓶颈、CD 一好就能放。取 0.03 后 ≈9.95/秒，略高于需求，
    /// 体感是「后期不卡蓝但不溢出」。（14.85 / 9.95 均含：曲线 + 攻击项 + 智力项）
    /// </summary>
    public const float MP_REGEN_PER_INTELLIGENCE = 0.03f;

    /// <summary>每升 1 级，MP 池 +这么多（回复不随等级涨）。</summary>
    public const float MP_POOL_PER_LEVEL = 6f;

    /// <summary>每升 1 星，池与回复各 +12%（对基础值线性叠加，★5 = ×1.48）。</summary>
    public const float MP_GROWTH_PER_STAR = 0.12f;

    // ============================================================
    // 佣兵阵亡惩罚（2026-10-06 主人拍板）
    // 旧行为：阵亡只是从 allyUnits 摘掉，下一关 RestoreRunMercs 重新 spawn → 满血满蓝复活，
    // 等于阵亡零成本。现在改成带惩罚复活：血 20% 起步、每过一关 +30%，最多 3 关养回满血。
    // ⚠ 蓝不动（每关照常灌满）：蓝池只够 1.2~1.5 发，再砍蓝治疗佣兵复活后连一发奶都放不出 → 全队雪崩。
    // ============================================================

    /// <summary>阵亡佣兵下一关的复活血比例（20%）。</summary>
    public const float MERC_REVIVE_HP_BASE = 0.2f;

    /// <summary>阵亡后每通过一关，复活血比例 +30%。</summary>
    public const float MERC_REVIVE_HP_PER_STAGE = 0.3f;

    /// <summary>阵亡后需要通过几关才养回满血（0.2 + 0.3×3 = 1.1 → 封顶 1.0）。</summary>
    public const int MERC_REVIVE_STAGES_TO_FULL = 3;

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

    /// <summary>
    /// 护盾抵扣比例唯一真源：玩家 holy_barrier 每次受伤只吸收 75% 伤害（剩 25% 照常扣血），
    /// 塔克 SK008 完全吸收 100%（盾没破不掉血）。盾值按实际吸收量减少，过期/耗尽逻辑不变。
    /// </summary>
    public const float HOLY_BARRIER_SHIELD_ABSORB = 0.75f;
    public const float MERC_TEAM_SHIELD_ABSORB = 1f;

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

    /// <summary>5 小关 → 10 档的等距映射（旧写死版，仅剩无章上下文的存量调用在用）。
    /// 【2026-11-03 方案A】有章上下文的调用一律走 <see cref="GetStageStatScale(int,int)"/> 按节数摊档
    /// —— B3 改节数 5/8/10 后旧表只盖 5 节，第 2 章小关 6-8、第 3 章起小关 6-10 全吃尾档 1.50 无缓坡。</summary>
    static readonly int[] StageSubToRampRow = { 0, 2, 4, 7, 9 };

    /// <summary>
    /// 按「章内第几关」取章内爬坡倍率（读 stage_stat_scale；缺表回退 Fallback）。
    /// 2026-10-05 新增：主人要求「每章里面的关卡也是如此」递增。
    /// <para>【2026-10-24 主人拍板「后期爬坡按报告方案」】把 5 小关等距摊到 10 档。
    /// ⚠ 超出 0~4 的序号按尾档 1.50 结算 —— B3 后 8/10 节章的 6+ 节都会命中此分支，
    /// 有章上下文的调用请改走 <see cref="GetStageStatScale(int,int)"/>。</para>
    /// </summary>
    /// <param name="stageIndex0Based">章内第几小关（0 起，0~4）。</param>
    public static float GetStageStatScale(int stageIndex0Based)
    {
        if (stageIndex0Based < 0) stageIndex0Based = 0;
        if (stageIndex0Based < StageSubToRampRow.Length)
            return StageStatScaleTable.Get0Based(StageSubToRampRow[stageIndex0Based]);
        return StageStatScaleTable.Get0Based(StageSubToRampRow.Length - 1);
    }

    /// <summary>
    /// 按「章内第几节（0 起）+ 本章节数」取章内爬坡倍率（【2026-11-03 方案A】B3 遗留修复）。
    /// 第 1 章（subs=5）缓坡 1.00/1.03/1.06/1.11/1.18（2026-11-11 主人拍板「第一章不要太卡」）；
    /// 8/10 节章仍摊满 10 档：首节恒行0（1.00）、末节 Boss 恒行9（1.50 卡点3），
    /// 中间节等距取样（Mathf.RoundToInt 半偶舍入）。
    /// </summary>
    public static float GetStageStatScale(int stageIndex0Based, int subsPerGrand)
    {
        if (stageIndex0Based < 0) stageIndex0Based = 0;
        if (subsPerGrand < 1) subsPerGrand = 1;
        if (stageIndex0Based >= subsPerGrand) stageIndex0Based = subsPerGrand - 1;
        // 【2026-11-11 主人拍板「第一章不要太卡 最后boss可以稍微卡一下」】第 1 章章内缓坡：
        //   1.00/1.03/1.06/1.11/1.18（旧行 {0,2,4,7,9} 让每座大关都冲到 1.50，10 个 Boss 全是墙）。
        //   仅第 1 章（subs=5 唯一对应）；8/10 节章的等距摊档不动。
        //   副本/竞技场/支线岛进本 currentStageIndex 恒 0 → 落行 0，两条映射结果相同，零影响。
        if (subsPerGrand == 5)
        {
            return StageStatScaleTable.Get0Based(Mathf.Clamp(stageIndex0Based, 0, 4));
        }
        // ⚠ 尾行号必须取 stage_stat_scale 表的行数（10 档，行9 = 1.50）——
        // 不能拿 StageSubToRampRow.Length（那是 5 元素旧映射）当行数，否则全部钳到 1.18。
        int lastRow = StageStatScaleTable.Fallback.Length - 1;
        int row = Mathf.RoundToInt((float)stageIndex0Based * lastRow / (subsPerGrand - 1));
        row = Mathf.Clamp(row, 0, lastRow);
        return StageStatScaleTable.Get0Based(row);
    }

    /// <summary>
    /// <b>装备产出专用</b>：章内爬坡的<b>单调版</b>（取本章到此为止的最高档）。
    /// <para>【2026-11-24 主人拍板「曲线要有起伏」】stage_stat_scale 改成波浪（峰/谷交替）后，
    /// 装备若直接读波浪值，谷关（关4/6/9）掉的装备会比刚打完的峰关更差 ——
    /// 玩家在关4 看到的三选一全是垃圾，观感是「越打装备越烂」，与「打完精英变强了」直接打架。</para>
    /// <para>语义：装备水平 = <b>你已经到达过的最高难度</b>。打过关3 精英之后，关4 起掉的装备
    /// 一律按 1.22 算，不会因为进了谷就回退；到关5 再抬到 1.30，依此类推。</para>
    /// <para>只给 <c>RiftEquipGenerator</c> 用；怪物侧走波浪版 <see cref="GetStageStatScale(int,int)"/>。</para>
    /// </summary>
    public static float GetStageStatScaleMonotonic(int stageIndex0Based, int subsPerGrand)
    {
        if (stageIndex0Based < 0) stageIndex0Based = 0;
        float best = 0f;
        for (int i = 0; i <= stageIndex0Based; i++)
        {
            float v = GetStageStatScale(i, subsPerGrand);
            if (v > best) best = v;
        }
        return best > 0f ? best : GetStageStatScale(stageIndex0Based, subsPerGrand);
    }

    /// <summary>
    /// <b>怪物属性总倍率的唯一出口</b> = 章倍率 × 章内倍率。
    /// <para>2026-10-05 主人拍板：难度曲线「每章都上升、有高有低，章内关卡也是如此」。
    /// 拆成两张表（chapter_stat_scale / stage_stat_scale）后，<b>相乘只有这一处</b> ——
    /// 谁要算最终倍率就调这里，绝不许在别处自己把两个数乘一遍（那才是「多入口」）。</para>
    /// </summary>
    /// <param name="gameChapter">第几章（1 起）</param>
    /// <param name="stageIndex0Based">章内第几小关（0 起，0~4；B1 后一章=一座大关）</param>
    public static float GetStatScale(int gameChapter, int stageIndex0Based)
    {
        return GetChapterStatScale(gameChapter) * GetStageStatScale(stageIndex0Based);
    }

    /// <summary>
    /// 【2026-11-24 主人拍板「第一关对齐引导关、缓坡过渡，不要 1-1 就 1 刀」】
    /// 第 1 章前 2 关的<b>血量过渡系数</b>，乘在 <see cref="MONSTER_HP_GLOBAL_MUL"/> 之上；
    /// 只作用于主线正式关普通小怪（门禁在 Monster.Init：formalMainRun &amp;&amp; !endlessRift &amp;&amp; !isTutorialRun）。
    /// <para>目标：引导普通怪 38~40 血，正式关 1-1 对齐同一水平 —— 玩家进战攻 21~29、
    /// 一击 20.6~28.4，≈40 血正好稳定 2 刀，断崖变坡道；1-2 回落到 ≈28~30 血（1~2 刀）；
    /// 1-3 起交给既有 <see cref="LATE_MOB_HP_MUL"/> 1.3 与原曲线，1-4 及以后完全恢复原值。</para>
    /// 实测微调只改这两个数；两者设成 1f 即完全恢复改动前手感。
    /// </summary>
    public static float EarlyChapterHpCurveMul(int gameChapter, int stageIndex0Based)
    {
        if (gameChapter != 1) return 1f;
        switch (stageIndex0Based)
        {
            case 0: return 3.0f;   // 1-1：≈39~41 血，稳定 2 刀（对齐引导）
            case 1: return 2.0f;   // 1-2：≈28~30 血，1~2 刀
            default: return 1f;    // 1-3 起：交回 LATE_MOB_HP_MUL / 原曲线
        }
    }

    // ===== 【2026-10-31 B2 落地】篇内多座大关的爬坡 =====
    /// <summary>
    /// 每多打一座大关，怪物属性额外 +6%（1 + 0.06×(大关-1)，封顶 ×1.60）。
    /// 为什么需要：B2 后一篇有 10~20 座大关，章倍率只按「篇」变，
    /// 若不加这层，同篇 20 座大关的怪数值完全一样（体检报告 F1/F2 的「同质化」问题会放大 20 倍）。
    /// 【2026-11-14 主人拍板 0.02 → 0.06】主人报「2-1 难度跟 1-1 差不多」：+2%/座体感为零，
    /// 提到 +6%/座（第 2 座 ×1.06、第 5 座 ×1.24、第 10 座 ×1.54）。
    /// 想调手感只改这两个常量；设 GRAND_STAT_STEP=0 即恢复「篇内全平」的旧口径。
    /// </summary>
    public const float GRAND_STAT_STEP = 0.06f;
    /// <summary>大关爬坡封顶（防止第 20 座大关叠到离谱）。</summary>
    public const float GRAND_STAT_MAX = 1.60f;

    /// <summary>篇内第几座大关的额外倍率（1 起；1 = 无加成）。</summary>
    public static float GetGrandStatScale(int grandIndex1Based)
    {
        if (grandIndex1Based <= 1) return 1f;
        return Mathf.Min(GRAND_STAT_MAX, 1f + GRAND_STAT_STEP * (grandIndex1Based - 1));
    }

    // ===== 【2026-11-03 主人拍板「每章最少 2 关卡玩家」】章级卡点关 =====
    /// <summary>
    /// 卡点关额外倍率：每章两关 —— <b>第 5 关（stageIdx=4）小 Boss</b> 与
    /// <b>第 10 关（stageIdx=9）大 Boss</b>，在正常爬坡之上再 +10%，做成「要多试几次」的明显墙。
    /// 【路线 B】grand 恒 1，原「grand==5||grand==10」判定失效，改按关号 stageIdx 判；只加成 Boss 关本身。
    /// 非主线（副本/裂隙/支线岛 currentStageIndex 恒 0、无限裂隙走独立分支）不命中，零影响。
    /// 想调手感只改 GRAND_WALL_STEP；设 0 = 关闭章级卡点，回到纯线性爬坡。
    /// </summary>
    public const float GRAND_WALL_STEP = 0.10f;

    /// <summary>
    /// 【2026-11-18 主人拍板「尽量血少攻高，这样玩家才有就差一点的感觉，只是运气不好、再多打一下就能过」】
    /// 第一章逐关递进倍率（下标 = 0 基关号）——<b>本表只管血量侧的卡点</b>：
    ///   关 1~4 不额外加；关 5（小 Boss）1.10 与第 2~8 章的半程卡点同档（+10%）；
    ///   关 6~9 顺次抬（1.14/1.18/1.22/1.27）保证单调递增，不出现「后一关比前一关容易」的倒挂；
    ///   <b>关 10 回到 1.00 —— 章末大 Boss 血量不加</b>，血薄才打得快、才看得到血条一路掉。
    /// 关 10 的难度不靠血，靠攻：见 <see cref="CH1_FINAL_BOSS_ATK_DEV"/>（只作用于攻击维度）。
    /// 关 5 不做硬墙的原因：佣兵招募（FeatureUnlock.MercRecruitOpen）的门槛就是「通第 5 关」，
    ///   这一战之前玩家还招不了新人，设太硬会卡死在「无法变强」的死循环里；真墙留给关 10（那时已能招人）。
    /// 只作用于第一章主线；2~8 章仍走 GRAND_WALL_STEP，副本/裂隙/支线 stageIdx 恒 0 取 [0]=1f 零影响。
    /// 想调手感只改本数组；实跑后按「Boss 挥几刀/剩多少血」回标。
    /// </summary>
    // 【2026-11-24 主人拍板「曲线要有峰有谷，且下个高潮比上一个更高」】关 6~9 的逐关递增归 1：
    //   旧值 1.14/1.18/1.22/1.27 是给「直线爬坡」补味道的，现在 stage_stat_scale 已经是波浪
    //   （关6 谷2 1.14 / 关7 1.20 / 关8 峰3 1.38 / 关9 谷3 1.26），两层同向叠乘后实算：
    //     关8 精英 1.38×1.22 = 1.684、关9 普通 1.26×1.27 = 1.600，而关10 大 Boss 只有 1.50×1.00 = 1.500
    //   → 关 9 比章末大 Boss 还厚，「大高潮收尾」直接失效（关9 成最高点）。
    //   归 1 之后：关3 精英 1.22 → 关8 精英 1.38（精英渐强）、关5 小Boss 1.30 → 关10 大Boss 1.50（Boss 渐强），
    //   每个峰后面都紧接谷（关4 1.08 / 关6 1.14 / 关9 1.26），关10 重新成为全章最高。
    //   本数组只保留关 5（半程小 Boss）的 +10% 卡点；关 10 维持 1f ——
    //   11-18「Boss 血薄攻高、看得到血条一路掉」口径不变，关10 的难度仍走 CH1_FINAL_BOSS_ATK_DEV（只加攻击）。
    //   要恢复旧手感把 1f 按旧值填回即可。
    static readonly float[] CH1_STAGE_WALL = { 1f, 1f, 1f, 1f, 1.10f, 1f, 1f, 1f, 1f, 1f };

    /// <summary>
    /// 【2026-11-18 主人拍板「尽量血少攻高，就差一点、再多打一下就能过」】
    /// 第一章关 10 大 Boss（forest_110 石灵）<b>专属攻击系数</b>，只乘攻击、不乘血量。
    /// 为什么单独开一条：全局 <see cref="BOSS_ATTACK_MUL"/> 现为 0.8（11-23 由 0.6 上调），
    ///   直接改它会连累 8 章所有 Boss；这里只补第一章关 10 这一个点。
    /// 现状换算（guildScale/diffScale=1、waveMul=1；近远真源 = monster_attack_style 风格表：
    ///   石灵 1-10 = <b>Melee 近战 ⇒ 物理型</b>，血量档走 BossPhysicalHpMul=18，不是法系的 12）：
    ///   血 = 26.7(本章1号小怪)×18(物理Boss)×1.782(章0.88×末节1.50×TTK1.35)×0.5(全局砍半)×2.0(Boss血倍) ≈ 856
    ///   攻 = 33.6×1.32(章0.88×末节1.50)×1.8(伤害倍)×1.35(Boss攻TTK)×0.35(章攻击减免)×0.6(Boss攻减免)×1.08(末节Boss攻偏差) ≈ 22.6
    ///   → 再乘本系数 1.8 ⇒ 攻 ≈ 44.0；血再乘 <see cref="CH1_FINAL_BOSS_HP_MUL"/> 0.85 ⇒ 血 ≈ 728。
    ///   相对改前（W=1.10 时血 942/攻 26.9）= 血 -23%、攻 +64%。
    /// ⚠ 别再拿 monster_stats 的 attackRange 判远近：Boss 的 atkRange 在 Monster.Init 里恒取 Ranged 世界值，
    ///   那列对 Boss 毫无意义；唯一真源是 monster_attack_style.csv 的 style。
    /// 调法：只改这一个数；实跑后按「玩家能扛几刀 / Boss 差多少血没死」回标。
    /// </summary>
    public const float CH1_FINAL_BOSS_ATK_DEV = 1.80f;

    /// <summary>
    /// 【2026-11-18 主人拍板「尽量血少攻高，就差一点、再多打一下就能过」】
    /// 第一章关 10 大 Boss（forest_110 石灵）<b>专属血量系数</b>，只乘血量、不乘攻击。
    /// 为什么单独开一条：<see cref="CH1_STAGE_WALL"/> 是 GetStatScale 的乘数，改它会
    ///   血量与攻击<b>同时</b>变化，满足不了「只降血、不动攻」；这里只削 Boss 血量这一个维度。
    /// 作用点：Monster.Init 的 MaxHp 写入式（bossHpMul 之后），判定 = 主线正式局 + 大 Boss + 第 1 章。
    /// 0.85 的依据：约 -15% 血，让血条掉得看得见、Boss 死前能留下「就差一点」的进度感，
    ///   同时又不至于瘦到没有 Boss 分量。想更瘦往下调，但别低于 0.7（会失去 Boss 体感）。
    /// 副本 / 裂隙 / 支线 / 竞技场零影响（条件含 IsFormalMainRun + BossKindOfStage==2）。
    /// </summary>
    public const float CH1_FINAL_BOSS_HP_MUL = 0.85f;

    public static float GetGrandWallBonus(int gameChapter, int grandIndex1Based, int stageIndex0Based)
    {
        // 【2026-11-18 主人拍板「不要硬加，前面几个可以有个提升」+「关 5 可以加」+「尽量血少攻高」】
        // 第一章改为逐关递进：关 1~4 不加、关 5（半程小 Boss）1.10 起逐关抬升、关 10 回到 1.00 血量不加。
        // 关 10 的卡点不在血量维度，而由 CH1_FINAL_BOSS_ATK_DEV 只加攻击 —— 「血薄打得快、一刀很痛」。
        // 只命中第一章，2~8 章逻辑原样保留。
        // 【路线 B】按关号 stageIdx 判（grand 恒 1 已失效）；副本/裂隙/支线 stageIdx 恒 0 → 取 [0]=1f，零影响。
        if (gameChapter == 1)
            return CH1_STAGE_WALL[Mathf.Clamp(stageIndex0Based, 0, CH1_STAGE_WALL.Length - 1)];
        if (stageIndex0Based != 4 && stageIndex0Based != 9) return 1f;   // 只有关5/关10
        return 1f + GRAND_WALL_STEP;
    }

    /// <summary>
    /// <b>怪物属性总倍率的唯一出口（带大关）</b> = 章倍率 × 大关爬坡 × 章内节台阶 × 章级卡点关。
    /// B2 落地后主线正式关一律走这个重载；副本/裂隙/支线岛传 grand=1（零影响）。
    /// 【2026-11-03 方案A】章内节台阶改按本章节数摊满 10 档（8/10 节章第 6 节起不再吃尾档）；
    /// 【2026-11-03 主人拍板】叠加每章 2 个卡点关（半程 Boss + 章末 Boss，各 +10%）。
    /// </summary>
    public static float GetStatScale(int gameChapter, int grandIndex1Based, int stageIndex0Based)
    {
        int subs = ActProgressionDefs.SubsPerGrandOf(gameChapter);
        return GetChapterStatScale(gameChapter)
             * GetGrandStatScale(grandIndex1Based)
             * GetStageStatScale(stageIndex0Based, subs)
             * GetGrandWallBonus(gameChapter, grandIndex1Based, stageIndex0Based);
    }

    // ===== 【2026-11-14 主人拍板「属性分维度爬坡 + 数个小高潮，不要一马平川」】=====
    // 正式主线关<b>逐关属性轮动偏差层</b>：叠在既有 章×座×节 曲线之上，让每一关都有
    // 「某一项明显涨了」的体感，而不是三项一起匀速爬：
    //   血 = 奇数关（第2/4关）+6% —— 肉度隔关涨，打得变久；
    //   攻 = 精英关 +10%（小高潮①）、末节 Boss 关 +8%（高潮②），其余关回落 —— 尖峰之间有呼吸；
    //   防 = 精英关 +5%、Boss 关 +6%。
    // 篇级大高潮不在这里（座5 半程墙 / 座10 章末墙 +10% 已有 GetGrandWallBonus）。
    // 唯一消费方 = Monster.Init 写 attr 三处（血/攻/防）；裂隙 / 副本 / 支线岛 / 引导不吃。
    // 【2026-11-24 主人拍板「要峰要谷，不要一马平川」】血量偏差<b>停用</b>（0）：
    //   原「奇数关 +6%」与关型无关，恰好把谷1(关4)/谷2(关6) 抬了回去、把峰3(关8)又推高 6%，
    //   等于削平新波浪。现在起伏<b>只在 stage_stat_scale 一张表里</b>（单点可调、可整体回退）。
    //   攻/防偏差<b>保留</b>：它们只在精英/Boss 关抬，方向与波浪一致（峰上加峰），不冲突。
    //   要恢复旧手感把 0f 改回 0.06f 即可。
    /// <summary>血偏差：奇数关 +6%（0 基 idx 1/3/5…）。2026-11-24 停用（0），起伏交给 stage_stat_scale。</summary>
    public const float STAGE_DEV_HP_ODD = 0f;
    /// <summary>攻偏差：精英关 +10%（小高潮①）。</summary>
    public const float STAGE_DEV_ATK_ELITE = 0.10f;
    /// <summary>攻偏差：末节 Boss 关 +8%（高潮②）。</summary>
    public const float STAGE_DEV_ATK_FINAL = 0.08f;
    /// <summary>防偏差：精英关 +5%。</summary>
    public const float STAGE_DEV_DEF_ELITE = 0.05f;
    /// <summary>防偏差：末节 Boss 关 +6%。</summary>
    public const float STAGE_DEV_DEF_FINAL = 0.06f;

    public static float GetStageStatDevHp(int stageIdx0Based)
        => Mathf.Max(0, stageIdx0Based) % 2 == 1 ? 1f + STAGE_DEV_HP_ODD : 1f;

    public static float GetStageStatDevAtk(bool eliteStage, bool finalStage)
        => 1f + (eliteStage ? STAGE_DEV_ATK_ELITE : 0f) + (finalStage ? STAGE_DEV_ATK_FINAL : 0f);

    public static float GetStageStatDevDef(bool eliteStage, bool finalStage)
        => 1f + (eliteStage ? STAGE_DEV_DEF_ELITE : 0f) + (finalStage ? STAGE_DEV_DEF_FINAL : 0f);
    // ===== 【2026-11-13 主人拍板「按章节的平均值加系数、必须比初始武器好」】装备攻击三档区间 =====
    /// <summary>档位系数下限（×本章普通怪平均 baseAttack）。真源 combat_tuning 表 EQUIP_ATK_TIER_*，缺表回退此处默认。</summary>
    public static float EquipAtkTierLo(string rarityId) =>
        rarityId == "UNCOMMON" ? CombatTuningTable.Get("EQUIP_ATK_TIER_UNCOMMON_LO", 2.05f)
        : rarityId == "RARE" ? CombatTuningTable.Get("EQUIP_ATK_TIER_RARE_LO", 2.6f)
        : rarityId == "LEGEND" ? CombatTuningTable.Get("EQUIP_ATK_TIER_LEGEND_LO", 4.8f)
        : CombatTuningTable.Get("EQUIP_ATK_TIER_NORMAL_LO", 1.2f);

    /// <summary>档位系数上限（×本章普通怪平均 baseAttack）。</summary>
    public static float EquipAtkTierHi(string rarityId) =>
        rarityId == "UNCOMMON" ? CombatTuningTable.Get("EQUIP_ATK_TIER_UNCOMMON_HI", 2.5f)
        : rarityId == "RARE" ? CombatTuningTable.Get("EQUIP_ATK_TIER_RARE_HI", 3.6f)
        : rarityId == "LEGEND" ? CombatTuningTable.Get("EQUIP_ATK_TIER_LEGEND_HI", 6.8f)
        : CombatTuningTable.Get("EQUIP_ATK_TIER_NORMAL_HI", 2.0f);

    // ===== 【2026-11-22 主人拍板「降数值 / 第一章不要有高过里程武器的」】第一章关卡掉落武器上限 =====
    // 真源 combat_tuning 表，缺表回退默认值（= 改动前不钳的上限值，行为不变可随时回退）。
    // 只在关卡结算掉落调用点（BattleManager.Settlement 传 capToMilestone:true）生效，
    // 抽奖 / 裂隙宝箱 / 局内商店 / 引导宝箱沿用 GenerateOne 默认 false，曲线一字不动。
    /// <summary>第一章关卡掉落武器主属性（攻 / 魔攻）上限。默认 10 = 里程暮火之杖模板魔攻 9.57×稀有系数附近。</summary>
    public static float Chapter1DropWeaponMainCap => CombatTuningTable.Get("CHAPTER1_DROP_WEAPON_MAIN_CAP", 10f);
    /// <summary>第一章关卡掉落武器火伤词条上限。默认 7 = 略低于里程暮火随机火伤 8 点。</summary>
    public static float Chapter1DropWeaponFireCap => CombatTuningTable.Get("CHAPTER1_DROP_WEAPON_FIRE_CAP", 7f);

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

    // ===== 【2026-11-09 主人拍板】红圈双圈 + 分类（精英小/3秒，Boss 大/4秒）=====
    /// <summary>浅色范围圈：预警第一秒就显示完整范围。</summary>
    public const float TELEGRAPH_RANGE_ALPHA = 0.28f;
    /// <summary>深色进度圈：从中心缩放 0 放大到与范围圈一样大，用时＝预警时长。</summary>
    public const float TELEGRAPH_PROGRESS_ALPHA = 0.85f;
    /// <summary>红圈视觉椭圆的 Y 压扁比例；伤害判定按同一比例折算，保证演出与判定同位。</summary>
    public const float TELEGRAPH_DISC_Y_RATIO = 0.35f;
    /// <summary>精英红圈半径上限（小一点）。</summary>
    public const float TELEGRAPH_ELITE_RADIUS = 2.6f;
    /// <summary>【已废弃 2026-11-18】Boss 红圈半径下限（大一点）。新口径见 TELEGRAPH_BOSS_RADIUS_MUL；本常量仅保留给尚未迁移的引用点，勿在新逻辑使用。</summary>
    public const float TELEGRAPH_BOSS_RADIUS = 4.5f;
    /// <summary>【2026-11-18 主人拍板】Boss 红圈半径 = 不透明宽 × 本值，替代原 4.5 硬下限。</summary>
    public const float TELEGRAPH_BOSS_RADIUS_MUL = 2f;
    /// <summary>【2026-11-18 主人拍板】Boss 二阶段红圈深血红。仅 RGB；alpha 仍由 TELEGRAPH_PROGRESS_ALPHA / TELEGRAPH_RANGE_ALPHA 决定。</summary>
    public static readonly Color TELEGRAPH_ENRAGED_COLOR = new Color(1f, 0.02f, 0.02f, 1f);
    /// <summary>精英预警时长（秒）。</summary>
    public const float TELEGRAPH_ELITE_SEC = 3f;
    /// <summary>Boss 预警时长（秒），阶段 2 与阶段切换同用。【2026-11-24 主人拍板】从 4s 减到 2.67s（-1/3）。</summary>
    public const float TELEGRAPH_BOSS_SEC = 2.6667f;
    /// <summary>Boss 主动技伤害倍率（相对精英 tier=1f 再乘一次）。【2026-11-24 主人拍板】+20% → 1.2f。</summary>
    public const float BOSS_SKILL_DAMAGE_MUL = 1.2f;
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：Boss 震屏幅度</summary>
    public static float BOSS_SKILL_RELEASE_SHAKE_AMP => CombatTuningTable.Get("BOSS_SKILL_RELEASE_SHAKE_AMP", 0.18f);
    /// <summary>精英/Boss 红圈/扇形预警结束、技能真正释放时的震屏：Boss 震屏时长</summary>
    public static float BOSS_SKILL_RELEASE_SHAKE_DUR => CombatTuningTable.Get("BOSS_SKILL_RELEASE_SHAKE_DUR", 0.26f);

    // ===== Boss 狂暴机制（只加这一种；仅改攻击间隔，不动伤害/属性缩放）=====
    /// <summary>Boss 狂暴总开关（安全阀）。false=完全关闭；true=二阶段按 BOSS_ENRAGE_ATK_SPEED_MUL 加速。</summary>
    public static bool BOSS_ENRAGE_ENABLED => CombatTuningTable.GetBool("BOSS_ENRAGE_ENABLED", true);
    /// <summary>【已废弃 2026-11-18】狂暴起始章节：章节门槛已取消，所有 Boss 二阶段均生效。常量保留兼容配置表，勿在新逻辑使用。</summary>
    public static int BOSS_ENRAGE_MIN_CHAPTER => CombatTuningTable.GetInt("BOSS_ENRAGE_MIN_CHAPTER", 5);
    /// <summary>触发狂暴的血量阈值（当前/最大 ≤ 此值触发）。0.40=低于 40%。</summary>
    public static float BOSS_ENRAGE_HP_RATIO => CombatTuningTable.Get("BOSS_ENRAGE_HP_RATIO", 0.40f);
    /// <summary>【2026-11-18 主人拍板】Boss 二阶段攻速倍率固定 1.3（原 1.5 线性曲线已废弃，见 HpScaledAttackSpeedMul）。攻击间隔 ÷1.3。</summary>
    public static float BOSS_ENRAGE_ATK_SPEED_MUL => CombatTuningTable.Get("BOSS_ENRAGE_ATK_SPEED_MUL", 1.3f);

    // =====================================================================
    // ⚠【2026-10-12 整段删除·主人「把关卡的怪物数量回复正常」】
    // 这里原本是 2026-09-23 加的**流程测试开关**，当时自己就写着「测完必须整段删掉」，
    // 结果一直留到今天：`TEST_BOSS_SOLO = true`（Boss 关不出小怪波）+ `TEST_BOSS_HP = 1`
    // （Boss 血量压到 1，一击通关）。它俩与 stage_spawn.csv 的「TEST 每关 1 只怪 1 波」
    // 是同一批测试数据（后者已一并还原，见该文件）。
    // 按铁律「不留 xxxEnabled 开关」，两个常量**连同调用点一起删干净**，不设为 false / 0 了事：
    //   · TEST_BOSS_SOLO  → 原调用点在 WavePlanner（Boss 小怪数归 0）
    //   · TEST_BOSS_HP    → 原调用点在 Monster.cs:756（Boss 血被设成 1）
    // 之后 Boss 关照常出小怪波、血量走正式公式。
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

    // ---- 精英蓄力跳演出（2026-10-17 主人拍板：蓄力压扁→高跳快落→红圈半透放大→玩家影子降透明）----
    /// <summary>蓄力末身体 Y 压到原高的该比例（1=不压）。主人原话「慢慢缩小Y轴」。</summary>
    public static float ELITE_LEAP_SQUASH => CombatTuningTable.Get("ELITE_LEAP_SQUASH", 0.72f);
    /// <summary>跳起高度（世界单位）。精英体型大，跳高点。</summary>
    public static float ELITE_LEAP_HEIGHT => CombatTuningTable.Get("ELITE_LEAP_HEIGHT", 3.2f);
    /// <summary>起跳上升到最高点用时（秒）。</summary>
    public static float ELITE_LEAP_RISE_SEC => CombatTuningTable.Get("ELITE_LEAP_RISE_SEC", 0.22f);
    /// <summary>最高点砸落到地面用时（秒）——比上升短，主人原话「落得迅速点」。</summary>
    public static float ELITE_LEAP_FALL_SEC => CombatTuningTable.Get("ELITE_LEAP_FALL_SEC", 0.15f);
    /// <summary>红圈起始缩放比例（相对全尺寸），预警期从该值线性放大到 1。</summary>
    public static float ELITE_TELEGRAPH_GROW_FROM => CombatTuningTable.Get("ELITE_TELEGRAPH_GROW_FROM", 0.35f);
    /// <summary>蓄力期间玩家影子 alpha 乘的系数（降 20% → ×0.8，落地还原）。</summary>
    public const float PLAYER_SHADOW_CHARGE_DIM = 0.8f;

    // ---- 远程专属反应技「瞬移遁走」（2026-10-20 主人拍板：被贴脸→竖向压扁消失→远处原地复原）----
    /// <summary>触发线：最近威胁者（玩家/佣兵）战斗距离 ≤ 该值触发瞬移（世界单位）。</summary>
    public static float MONSTER_BLINK_TRIGGER_DIST => CombatTuningTable.Get("MONSTER_BLINK_TRIGGER_DIST", 1.8f);
    /// <summary>瞬移冷却（秒）。冷却内被贴脸就硬扛着继续射（Boss「贴脸也射」不受影响）。
    /// 【2026-10-22 主人拍板】7→15：原 7s 在贴脸追打里像「连着放两次」，拉长冷却杜绝连发观感。</summary>
    public static float MONSTER_BLINK_COOLDOWN => CombatTuningTable.Get("MONSTER_BLINK_COOLDOWN", 15f);
    /// <summary>遁走位移下限（世界单位）。</summary>
    public static float MONSTER_BLINK_MIN_DIST => CombatTuningTable.Get("MONSTER_BLINK_MIN_DIST", 3.0f);
    /// <summary>遁走位移上限（世界单位）。实际取 [MIN, MAX] 随机，落点再钳回战场可视范围。</summary>
    public static float MONSTER_BLINK_MAX_DIST => CombatTuningTable.Get("MONSTER_BLINK_MAX_DIST", 4.5f);
    /// <summary>压扁遁地用时（秒）：scale.y 1→0，EaseIn 越压越快。</summary>
    public static float MONSTER_BLINK_SQUASH_SEC => CombatTuningTable.Get("MONSTER_BLINK_SQUASH_SEC", 0.30f);
    /// <summary>隐形挪移空窗（秒）：完全消失，中点换位。</summary>
    public static float MONSTER_BLINK_HIDE_SEC => CombatTuningTable.Get("MONSTER_BLINK_HIDE_SEC", 0.10f);
    /// <summary>原地复原用时（秒）：scale.y 0→1，EaseOut 先快后慢弹出。</summary>
    public static float MONSTER_BLINK_RESTORE_SEC => CombatTuningTable.Get("MONSTER_BLINK_RESTORE_SEC", 0.35f);
    /// <summary>复原后的收势停顿（秒）：停稳才恢复行动，主人要求节奏别太快。</summary>
    public static float MONSTER_BLINK_SETTLE_SEC => CombatTuningTable.Get("MONSTER_BLINK_SETTLE_SEC", 0.10f);

    // ==================== 【2026-10-20 主人拍板】远程精英/Boss 双弹幕技 ====================
    /// <summary>
    /// 【2026-11-13 主人拍板「boss 可以带，但是要有 cd 稍微长点，现在感觉一直在放」】
    /// 远程 Boss **依然恒带**扇形散射（10-20 原口径，不删不禁），只是给它单独一档更长的冷却：
    /// 沿用精英的 MONSTER_FAN_VOLLEY_COOLDOWN(20s) 在长时间的 Boss 战里显得一直在放，故 Boss 拉到 40s。
    /// 想调频率只改这一个数；精英仍走 MONSTER_FAN_VOLLEY_COOLDOWN，不受影响。
    /// 消费点唯一 = Monster.Skills.TryCastRangedVolleySkill 扇形起手处。
    /// </summary>
    public const float BOSS_FAN_VOLLEY_COOLDOWN = 40f;
    /// <summary>远程 Boss 扇形弹幕首发延迟（秒）：给玩家进射程前留缓冲，别一开打就齐射。</summary>
    public const float BOSS_FAN_VOLLEY_FIRST_DELAY = 8f;
    /// <summary>扇形弹幕冷却（秒）。</summary>
    public static float MONSTER_FAN_VOLLEY_COOLDOWN => CombatTuningTable.Get("MONSTER_FAN_VOLLEY_COOLDOWN", 20f);
    /// <summary>扇形弹幕一次齐射的弹道数（Boss 用）。
    /// 【2026-10-22 主人拍板】数量减负：精英 3 发、Boss 5 发，拆成两条配置。</summary>
    public static int MONSTER_FAN_VOLLEY_COUNT => Mathf.Max(2, Mathf.RoundToInt(CombatTuningTable.Get("MONSTER_FAN_VOLLEY_COUNT", 5f)));
    /// <summary>扇形弹幕一次齐射的弹道数（精英用，主人定 3）。</summary>
    public static int MONSTER_FAN_VOLLEY_COUNT_ELITE => Mathf.Max(2, Mathf.RoundToInt(CombatTuningTable.Get("MONSTER_FAN_VOLLEY_COUNT_ELITE", 3f)));
    /// <summary>扇形弹幕相邻弹道夹角（度）。5 发 × 9° → 总扇面 ±18°。</summary>
    public static float MONSTER_FAN_VOLLEY_ANGLE_STEP => CombatTuningTable.Get("MONSTER_FAN_VOLLEY_ANGLE_STEP", 9f);
    /// <summary>扇形弹幕弹道飞行距离（世界单位），飞满即在终点爆开结算。</summary>
    public static float MONSTER_FAN_VOLLEY_RANGE => CombatTuningTable.Get("MONSTER_FAN_VOLLEY_RANGE", 11f);
    /// <summary>扇形弹幕单发命中判定半径（世界单位），走位出爆点即躲开。</summary>
    public static float MONSTER_FAN_VOLLEY_HIT_RADIUS => CombatTuningTable.Get("MONSTER_FAN_VOLLEY_HIT_RADIUS", 1.4f);
    /// <summary>弹幕单发伤害 = 普攻 raw × 此系数（主人定 80%）。</summary>
    public const float MONSTER_VOLLEY_DAMAGE_MUL = 0.8f;
    /// <summary>追踪弹幕冷却（秒）。</summary>
    public static float MONSTER_HOMING_BARRAGE_COOLDOWN => CombatTuningTable.Get("MONSTER_HOMING_BARRAGE_COOLDOWN", 20f);
    /// <summary>追踪弹幕连发条数下限（主人定 3，与上限之间随机）。</summary>
    public const int MONSTER_HOMING_BARRAGE_MIN = 3;
    /// <summary>追踪弹幕连发条数上限（主人定 5）。</summary>
    public const int MONSTER_HOMING_BARRAGE_MAX = 5;
    /// <summary>追踪弹幕相邻两发间隔（秒），感知冻场。</summary>
    public static float MONSTER_HOMING_BARRAGE_GAP => CombatTuningTable.Get("MONSTER_HOMING_BARRAGE_GAP", 0.22f);
    /// <summary>追踪弹幕飞行速度倍率（乘 projectileSpeed 全局链路；要能追上走位的单位）。</summary>
    public static float MONSTER_HOMING_BARRAGE_SPEED_MUL => CombatTuningTable.Get("MONSTER_HOMING_BARRAGE_SPEED_MUL", 0.45f);

    /// <summary>公会等级系数：0.02×公会等级</summary>
    public static float GUILD_SCALE_PER => CombatTuningTable.Get("GUILD_SCALE_PER", 0.02f);

    [Header("战斗配置")]
    /// <summary>
    /// 怪物伤害倍率（数值表已校准）。
    /// 2026-09-26 主人反馈「玩家受击只掉 1 血、开不开护盾都是 -1」：
    /// monster_stats 的 baseAttack 只有 3.6~13.7，而玩家防御起步就有 10.5（剑盾）+ 体力 5 + 装备，
    /// DamageFormula.FinalHit 的减法结果长期 ≤ 0，被 MinDamage=1 钳成 1 —— 不是护盾没接，是根本没得可减。
    /// 【原值 1f → 3f】2026-09-26：第 1 章杂兵 raw 26~41，3f 下剑盾净伤 6~21。
    /// 【3f → 2f】2026-09-28 主人拍板：3f 连引导关都嫌痛，先降回 2f。
    /// 【2f → 2.1f】2026-10-12 主人拍板「怪物的伤害再高一点点」：+5%，全局生效（Boss 同步）。
    /// 这是唯一的全局旋钮（Monster.cs 攻击赋值处），嫌狠就往下调。
    /// 【2.1f → 1.8f】2026-10-17 主人拍板「开局卡关」回调：10-10 玩家全职业血×0.8 与 10-12 怪伤+5%
    /// 叠加后，新档无佣兵单挑第一关被围约 9 秒倒（玩家反馈「刚开局就卡关」）→ 回调到 1.8（-14%）。
    /// </summary>
    public const float MONSTER_DAMAGE_MULTIPLIER = 1.8f;
    /// <summary>
    /// 按章的敌人攻击/魔攻减免倍率（唯一真源，唯一入口 Monster.Init.cs 攻击赋值处）。
    /// 演进：2026-10-20「前期敌人的攻击再降30%」0.7 → 2026-10-22「近战也砍，前期要让玩家稍微
    /// 抽点奖就能过」第1~2章 0.35 → 同日主人拍板「第三章砍40% 后面的砍20%」→
    /// 【2026-10-25 主人拍板「断崖调缓 不要让玩家察觉 只是觉得运气不好」】三档阶梯废除，
    /// 改 8 章缓坡表（下表）：原第2→第5章（主线大关20→21）入口跳档 ×1.71 是全曲线唯一断崖
    /// （叠上章倍率 ×1.13 与 ch5 怪表 baseAtk ×1.35，实测玩家承受次数 48→21 下腰斩）；
    /// 现拆成每站 ×1.05~1.15 的连续爬坡，配合玩家天赋/装备同步成长，体感只剩「这关运气不好」。
    /// 验算（玩家 HP 含天赋进度 360→850）：ch1→ch8 承受次数 84 → 70 → 51 → 45 → 39 → 32，无单点跳变。
    /// 远程共用这条倍率（远程另有 MONSTER_RANGED_ATK_MUL 一档）。只压攻击/魔攻，血防不动；
    /// 无尽裂隙不参与（强度只随层数走，Monster.Init 已排除）、引导局不参与。
    /// ⚠ 外溢提示：活动副本场景章跟随本表（金币=1 不变 / 徽记=2 +3% 无感 / 卷轴=6 怪 ATK ×0.65 变弱，
    /// 2026-10-25 已报主人，若嫌简单后续给副本难度分层补独立乘区，未动）。
    /// </summary>
    static readonly float[] ChapterAtkMulTable = { 0.35f, 0.36f, 0.40f, 0.42f, 0.45f, 0.52f, 0.62f, 0.75f };

    public static float ChapterAtkMul(int chapter)
    {
        int ch = Mathf.Max(1, chapter);
        // 表内 8 章直查；第 9 章及以后（越界续算篇）按尾档 0.75（与 chapter_stat_scale 越界同口径：不抛错续算）
        return ch <= ChapterAtkMulTable.Length ? ChapterAtkMulTable[ch - 1] : ChapterAtkMulTable[ChapterAtkMulTable.Length - 1];
    }

    // 【2026-11-22 主人拍板】1章3关起（含全部玩法 + 裂隙）后期加强，判据与倍率的唯一出口。
    // 精英 / 小BOSS(10号) / 真Boss(11,12)：血×2 攻×1.5；其余小怪（含 3~6 号表定词条怪）血攻各 ×1.3。
    // 门槛：主线 chapter>1 或 (chapter==1 && stageIdx0>=2)（stageIdx0 0 起，2 = 第3关）；
    //       裂隙无「1章3关」映射，作为后期内容恒吃；活动副本/支线岛/竞技场等非主线玩法同样恒吃。
    // 引导局恒不吃（沿用既有「引导的不要动」口径）。
    public const float LATE_ELITE_BOSS_HP_MUL = 2f;
    public const float LATE_ELITE_BOSS_ATK_MUL = 1.5f;
    public const float LATE_MOB_HP_MUL = 1.3f;
    public const float LATE_MOB_ATK_MUL = 1.3f;

    public static bool LateGameBuffActive(int chapter, int stageIdx0, bool isTutorialRun, bool endlessRift, bool formalMainRun)
    {
        if (isTutorialRun) return false;
        if (endlessRift || !formalMainRun) return true;
        if (chapter > 1) return true;
        return chapter == 1 && stageIdx0 >= 2;
    }

    // 【2026-11-24 主人拍板「精英/Boss 的增长系数改成从小到大，第3关精英血太多」】
    // 旧口径：LATE_ELITE_BOSS_HP_MUL 恒 2f —— 1-3 一进门就 ×2（把 1-2 的精英衬得极薄），
    // 是「前重后轻」的硬台阶；11-23 又拿 stage3EliteHpMul=2/3 打补丁，1-3 净剩 ×1.333。
    // 新口径（2026-11-24 二次收口）：**只调第 1 章**。
    //   第 1 章：精英 1.00 / Boss 1.35，把「1-3 一进门 ×2」的硬台阶拆掉；
    //   第 2~8 章：一律回 2.00（= 改动前的旧值），避免「只想改 1-3、却把第 2 章精英砍掉 34~40%」的外溢。
    //   代价是「按章递增」只剩第 1 章这一段；要恢复递增，把后 7 项按
    //   1.15/1.30/1.42/1.55/1.68/1.85/2.05 填回即可。
    //   第 1 章关 8 精英另走 stageIdx 特调（见函数内注释），不在这张表里表达。
    //   Boss 第 1 章保 1.35 兜底，不让第一章大 Boss 被这一刀砍半。
    // 只给主线正式关（formalMainRun）；裂隙 / 活动副本 / 支线岛 / 竞技场仍走常量
    // LATE_ELITE_BOSS_HP_MUL，行为零变化。要整体更肉/更薄只动这两张表。
    static readonly float[] LateEliteHpRamp = { 1.00f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f };
    static readonly float[] LateBossHpRamp  = { 1.35f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f, 2.00f };
    public static float LateEliteBossHpMulForChapter(int chapter, bool isBoss, int stageIdx0 = -1)
    {
        var ramp = isBoss ? LateBossHpRamp : LateEliteHpRamp;
        int ch = Mathf.Clamp(chapter, 1, ramp.Length);
        float v = ramp[ch - 1];
        // 【2026-11-24 主人拍板·峰3】第 1 章关 8 精英单独抬厚 1.70 倍（294 → 500 血）：
        //   关 3 精英 260 → 关 8 精英 500，精英渐强才成立；普通怪 / Boss / 其他章一律 ×1。
        //   只乘精英这一层，不动 CH1_STAGE_WALL（那个会连普通怪一起抬，关 8 会变成 180 秒长关）。
        if (chapter == 1 && !isBoss && stageIdx0 == 7) v *= 1.70f;
        return v;
    }

    public const float STAGE_LENGTH = 20f; // 每关长度20单位，走到头通关
    public const int EQUIP_CHOOSE_COUNT = 3; // 每关结束三选一装备
    public const int MAX_EQUIP_SLOT = 7; // 身上装备槽位数量：头/胸/手/脚/披风/主手/副手（已改为包内同部位唯一，无穿戴槽）

    [Header("难度 / 金币副本")]
    /// <summary>通关满 N 章后开启困难（2026-10-23 主人拍板「困难的应该提示通关第一章开启」：3 → 1）</summary>
    public const int DIFF_HARD_NEED_CLEARS = 1;
    /// <summary>通关满 N 章后开启噩梦（2026-10-23 主人拍板「噩梦应该提示通关第三章开启」；
    /// 覆盖 2026-09-17「困难通关第 8 章开噩梦」旧规——SaveData.hardCleared 从此只写不读）。</summary>
    public const int DIFF_NIGHTMARE_NEED_CLEARS = 3;
    /// <summary>
    /// 难度档位数量：普通 / 困难 / 噩梦 = 3。
    /// 2026-09-17 改动：
    /// ① **删除地狱**——原「地狱」与噩梦共用同一个数值分支（<c>diff &gt;= 2</c>），是空壳，已彻底移除；
    /// ② **噩梦不再按通关章节数解锁**，改判「在困难难度下通关第 8 章」，
    ///    存 <c>SaveData.hardCleared</c>（原 DIFF_NIGHTMARE_NEED_CLEARS 已删除）。
    /// </summary>
    public const int DIFF_COUNT = 3;

    /// <summary>
    /// 【2026-10-06 主人拍板】难度名<b>全局唯一出处</b>：下标 = 难度档（0 普通 / 1 困难 / 2 噩梦），
    /// 与 <see cref="DIFF_COUNT"/> 一一对应。
    /// 收敛原因（主人铁律「同一语义只留一个出口」）：原先 AdventureUI 里有一份私有 DiffNames，
    /// 战斗内左上角 HUD 还要再显示同一个难度名 —— 两份各写必然漂移（改一处忘一处）。
    /// 现在谁要显示难度名都走 <see cref="GetDifficultyName"/>，别再抄一份字符串。
    /// 颜色不在这里：难度按钮配色是冒险页自己的美术色彩，留在 AdventureUI。
    /// </summary>
    public static readonly string[] DifficultyNames = { "普通", "困难", "噩梦" };

    /// <summary>
    /// 难度名。越界<b>不静默</b>：打 Error 并落到「普通」（铁律「认不出/越界 → LogError + 占位」）。
    /// </summary>
    public static string GetDifficultyName(int diff)
    {
        if (diff < 0 || diff >= DifficultyNames.Length)
        {
            Debug.LogError($"[GameConfig] 难度档越界：{diff}（合法 0~{DifficultyNames.Length - 1}）");
            return DifficultyNames[0];
        }
        return DifficultyNames[diff];
    }

    /// <summary>金币副本通关固定金：基数 × 章节 × 难度倍率。2026-09-15 产出去零：300 → 30。</summary>
    public const int GOLD_DUNGEON_CLEAR_BASE = 30;

    /// <summary>
    /// 金币副本通关固定金（2026-10-24 主人拍板「金币副本给的金币是500 800 1200」）：
    /// 三档固定值，只按难度、不随章节 / 难度倍率浮动。diff：0 普通 500 / 1 困难 800 / 2 噩梦 1200。
    /// chapter 参数保留（调用点：BattleQuestConfig / 冒险页掉落预览），数值不再依赖它。
    /// </summary>
    public static int GetGoldDungeonClearGold(int chapter, int diff)
    {
        _ = chapter;
        switch (Mathf.Clamp(diff, 0, 2))
        {
            case 1: return 800;
            case 2: return 1200;
            default: return 500;
        }
    }

    /// <summary>
    /// 难度属性倍率（怪物血/攻/防）。
    /// 【2026-11-14 主人拍板「困难在普通难度加20-50% 噩梦加80-100%」】区间<b>随章爬坡</b>：
    /// 第 1 章吃区间下沿（刚开档不劝退）、第 8 章吃上沿，第 9 章起按尾档续。
    /// 困难 1.20→1.50（+20%→+50%）；噩梦 1.80→2.00（+80%→+100%）。
    /// 防"无限难"的红线不变：只调这一个乘区，不新增；怪物抗性封顶 50%。
    /// 唯一消费方 = BattleManager.State.DifficultyStatScale（活动副本走 GetActivityDifficultyStatScale 独立表，不动）。
    /// </summary>
    public static float GetDifficultyStatScale(int diff, int chapter = 1)
    {
        if (diff <= 0) return 1f;
        int ch = Mathf.Clamp(Mathf.Max(1, chapter), 1, 8);
        float t = (ch - 1) / 7f;
        if (diff == 1) return Mathf.Lerp(1.20f, 1.50f, t);   // 困难 +20% → +50%
        return Mathf.Lerp(1.80f, 2.00f, t);                  // 噩梦 +80% → +100%
    }

    /// <summary>
    /// 活动副本（金币/徽记）难度属性倍率（2026-10-23 主人拍板）：每档 ×1.5 → 1.0 / 1.5 / 2.25。
    /// 无限裂隙不选难度（层数曲线 +7%/层），天然不适用本表；主线仍走 <see cref="GetDifficultyStatScale"/>。
    /// </summary>
    public static float GetActivityDifficultyStatScale(int diff)
    {
        if (diff >= 2) return 2.25f;   // 1.5 × 1.5
        if (diff == 1) return 1.5f;
        return 1f;
    }

    /// <summary>难度掉金倍率。2026-09-17：噩梦 3.0 → **3.5**。</summary>
    public static float GetDifficultyGoldMul(int diff)
    {
        if (diff >= 2) return 3.5f;
        if (diff == 1) return 1.8f;
        return 1f;
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

    /// <summary>
    /// 难度装备属性系数（困难 +30% / 噩梦 +60%；第十节主人拍板，让困境装备能破局普通卡关；T5 重标锚点时重新标定）。
    /// 0=普通 1.0f / 1=困难 1.30f / 2=噩梦 1.60f，越界按 clamp 到 0~2。
    /// </summary>
    public static float GetDifficultyEquipMul(int difficulty)
    {
        const float NormalMul = 1.0f;
        const float HardMul = 1.30f;
        const float NightmareMul = 1.60f;
        switch (Mathf.Clamp(difficulty, 0, 2))
        {
            case 1: return HardMul;
            case 2: return NightmareMul;
            default: return NormalMul;
        }
    }

    // —— 替换装备折抽奖币（【2026-10-31 主人拍板「替换掉的道具转换成抽奖币 要整十数根据稀有度和部位来」】）——
    /// <summary>稀有度基础折币（整十）：白10 / 绿20 / 蓝40 / 紫60 / 橙100。下标 = (int)Rarity（1~5）。</summary>
    static readonly int[] ScrapDraftCoinByRarity = { 0, 10, 20, 40, 60, 100 };

    /// <summary>
    /// 替换/分解装备折成<b>抽奖币</b>的金额（战斗内替换链路专用出口，唯一调用方 =
    /// <see cref="WeaponLoadoutRules.GrantScrapGold"/>；城镇主动分解/结算折金仍走 <see cref="EquipScrapGold"/>）。
    /// 金额 = 稀有度基础值（整十）× 部位系数（整数）→ 结果<b>恒为整十数</b>：白10 起、武器组 ×2、
    /// 满档（橙·武器）200。数值要调只改 ScrapDraftCoinByRarity 与部位系数，出口不动。
    /// </summary>
    public static int EquipScrapDraftCoin(Rarity rarity, EquipSlotType slot)
    {
        int r = (int)rarity;
        int v = (r >= 1 && r < ScrapDraftCoinByRarity.Length) ? ScrapDraftCoinByRarity[r] : 10;
        if (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand)
            v *= 2;   // 武器组部位系数 ×2（双手武器经 ResolveLogicalSlot 归 MainHand，同样吃到）
        return v < 10 ? 10 : v;   // 整十保底，绝不出现零头
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


    // ============================================================
    // 职业自带技能（2026-11-13 主人拍板）：独立攒槽通道，不占 4 槽 / 不进抽卡池 / 无 UI。
    // 攒满靠普攻命中次数，表里 cooldown 只作「防连喷的短保底间隔」，长 CD 不在这里。
    // ============================================================
    public const float CAREER_SKILL_START_CHARGE_RATIO = 0.30f;  // 开局送 30% 起步槽
    public const int   CAREER_CHARGE_HITS  = 8;   // 剑盾：攒满所需普攻命中数
    // 【2026-11-24 主人拍板「把剑盾的冲锋伤害+10%」】只作用于破阵冲锋（CareerSkillExecutor.CoCharge），
    // 散射箭/法球职业技不受影响。原配表 damageMultiplier × 1.1 = 最终冲锋伤害。
    public const float CAREER_CHARGE_DAMAGE_MUL = 1.1f;
    public const int   CAREER_SCATTER_HITS = 10;  // 游侠
    public const int   CAREER_ORB_HITS     = 9;   // 法师

    public const float CAREER_SKILL_KNOCKBACK = 1.6f;   // 职业技命中的强击退距离（普通怪），精英/Boss 仍免退
    // 【2026-11-16 主人「让小怪被击飞的距离再大一点」】只放大剑盾破阵冲锋撞开的那一档：
    // 1.6 → 2.4（+50%）；散射箭 / 法球仍读 CAREER_SKILL_KNOCKBACK，不受影响。
    // 击退时长固定 KNOCKBACK_DURATION(0.09s)，改这里只改推出去多远、不拖慢节奏；精英/Boss 照旧免退。
    public const float CAREER_CHARGE_KNOCKBACK = 2.4f;
    // 【2026-11-14 主人「距离可以再长点」】5.0 → 7.0（上限，仍受 CAMERA 视野约束，冲出屏外由镜头跟随兜）
    public const float CAREER_CHARGE_DIST = 4.67f;      // 冲锋最大横移距离（只动 X，不动 lane 的 Y）【2026-11-16 主人「距离缩短三分之一」】7.0 × 2/3 = 4.667
    public const float CAREER_CHARGE_DURATION = 0.12f;  // ⚠ 2026-11-14 起停用（改按 CAREER_CHARGE_SPEED_MUL 反推时长），常量保留不删
    public const float CAREER_CHARGE_STOP_GAP = 0.8f;   // ⚠ 2026-11-14 起不再使用：冲锋改为越过目标继续冲（见 CAREER_CHARGE_OVERSHOOT），常量保留不删
    // 【2026-11-14 主人拍板「冲目标往前迅速冲一段距离，不是冲到怪物跟前就不动了」】
    // 冲锋终点 = 目标位置 + 这段越过量：贴着目标起手也要冲出一段，冲过它再停。
    // 【2026-11-14 主人「往前冲的距离短」】1.4 → 2.4，贴身出手也冲出 3.3 左右。
    // 【2026-11-14 主人「距离可以再长点」】2.4 → 3.2。
    public const float CAREER_CHARGE_OVERSHOOT = 2.13f;   // 【2026-11-16 主人「距离缩短三分之一」】3.2 × 2/3 = 2.133（越过量同步缩短，冲锋总长才真的少三分之一）
    // 【2026-11-14 主人报「冲撞速度太快了，比走快 40%」】旧值是按固定 0.12 秒冲 3.2 距离算的
    // （= 走速的 27 倍，看着像瞬移）。改成按移速倍率反推时长：dur = dist / (当前移速 × 本倍率)。
    // 【2026-11-14 主人「速度可以再快点」】1.4 → 1.9。
    // 【2026-11-15 主人「速度再快 30%」】1.9 × 1.3 = 2.47；dur = dist / (当前移速 × 本倍率)，只改这一处。
    public const float CAREER_CHARGE_SPEED_MUL = 2.72f;   // 【2026-11-16 主人「速度快 10%」】2.47 × 1.1 = 2.717；仍只改这一处（dur = dist / 移速×本倍率）
    // 【2026-11-14 主人拍板「撞到敌人时要顿一下，把敌人撞开再继续往前」】分段破阵冲锋的节奏参数。
    // 顿帧只停英雄自己的位移、不碰 timeScale（10-20 已砍掉全场慢放，主人嫌「一卡一卡」）。
    public const float CAREER_CHARGE_HITSTOP = 0.10f;        // 每次撞到敌人的停顿秒数
    public const int   CAREER_CHARGE_HITSTOP_MAX = 4;        // 整段冲锋最多顿几次（多了拖沓，其余一路冲过去）
    public const float CAREER_CHARGE_CONTACT_GAP = 0.35f;    // 停在敌人身前这么远就顿（不穿进身体里）
    // 【2026-11-16 主人「玩家能推着精英往后退一段距离，但退 Boss 推不动，玩家也只是在 Boss 跟前，保持推的状态一段时间」】
    // 真 Boss：冲锋到它身前 CONTACT_GAP 就停住 + 保持顶住状态这么久，不穿过它（MarkCharging 心跳 0.6s 盖得住）。
    public const float CAREER_CHARGE_BOSS_PIN = 0.35f;
    public const float CAREER_CHARGE_BURST_RADIUS = 1.2f;    // ⚠ 2026-11-16 起停用：撞击判定改矩形，尺寸由盾的世界大小推导（见 CAREER_CHARGE_HIT_RANGE_MUL），常量保留不删
    // 【2026-11-16 主人「让剑盾的冲锋伤害范围再小一半，应该是盾的一半大小的范围」】
    // 【2026-11-19 主人「剑盾冲锋的时候面积还是大」】再收一档：0.5 → 0.4（约 1.92 × 1.35 的矩形）。
    // 判定框 = 盾的世界尺寸 × 本倍率（0.5 = shield_block 148×104 帧 / 100ppu × 预制体 3.6 × 0.9 缩放
    // -> 盾 4.795 × 3.370，一半即 2.40 × 1.69 的矩形），撞击点居中、横纵各取一半作半径。
    // 想再收/再放只改这一个倍率，不要另写第二份尺寸数值。
    public const float CAREER_CHARGE_HIT_RANGE_MUL = 0.4f;
    // 【2026-11-15 主人「尘土这个特效在玩家冲锋的时候加到脚后面」】脚后偏移（世界单位，只动 X、Y 恒用 FootY）。
    public const float CAREER_CHARGE_DUST_BACK = 0.4f;
    // 【2026-11-15 主人「盾在小 10% 再往下 20 / 尘土小一半往上 10」】
    // 设计像素 → 世界：正交相机全高 2*orthoSize 对应 DESIGN_HEIGHT 个设计像素（1px = 0.0084375 世界）。
    // 缩放走倍率、prefab 原值不动；尺寸真源仍是 prefab，本处只承担「再小多少」这一份口径。
    public const float CAREER_CHARGE_SHIELD_SCALE_MUL = 0.9f;
    public const float CAREER_CHARGE_SHIELD_DROP_WORLD = 20f * (2f * GameConfig.CAMERA_ORTHO_SIZE / GameConfig.DESIGN_HEIGHT);
    public const float CAREER_CHARGE_DUST_SCALE_MUL = 0.5f;
    public const float CAREER_CHARGE_DUST_UP_WORLD = 10f * (2f * GameConfig.CAMERA_ORTHO_SIZE / GameConfig.DESIGN_HEIGHT);
    // 【2026-11-14 主人报「没看到剑盾的冲撞」】剑盾是近战，攒满 8 次普攻时目标早已贴身，
    // 旧算法 reach = |到目标 X| - STOP_GAP 只剩 0.1（Editor.log 实测 dist=0.10）→ 原地不动、看不出冲锋。
    // 给一个最小冲锋量：贴身时也至少冲出 1.6，让突进 + 落点刀光看得见；上限仍受 CAREER_CHARGE_DIST 约束。
    public const float CAREER_CHARGE_MIN_DIST = 1.07f;  // 【2026-11-16 主人「距离缩短三分之一」】1.6 × 2/3 = 1.067（贴身兜底同比缩，免得缩小后最小量反超总长）

    public const float CAREER_SCATTER_ANGLE_STEP = 9f;  // 每条箭道的角度间隔（度）
    public const float CAREER_SCATTER_RANGE = 9f;       // 箭道飞行距离上限
    public const float CAREER_SCATTER_HIT_RADIUS = 1.4f;
    public const int   CAREER_SCATTER_MAX_HITS_PER_ENEMY = 2;

    // 【2026-11-14 主人「我是对比原来普通的法球的比例」】倍数指相对普通法球的视觉比例，
    // 不是直接乘在 3 号素材上：普通法球运行时 scaleMul=1 / prefab localScale=1，从未改过大小；
    // 3 号素材自身更大（fly3 主粒子 startSize 1.4，Base 1）。
    // 【2026-11-14 主人「改成2吧」】3.5 → 1.75 → 2（飞行 1.4×2≈2.8 倍普通法球）。
    // 只作用职业技能大法球（仅 CoOrb 读本常量）；普攻法球不吃本常量，不受影响。
    public const float CAREER_ORB_SCALE_MUL = 2f;       // 法球放大倍数（飞行体）
    // 【2026-11-15 主人「法球爆炸后的烟雾有点大 小20%吧」】只缩命中的爆炸烟云，
    // 飞行体仍按 CAREER_ORB_SCALE_MUL=2 放大不变；1 = 与飞行体同倍，0.8 = 小 20%。
    // 仅 CoOrb 的 impactScaleMul 读本常量，其它技能 / 佣兵法球命中特效不受影响。
    public const float CAREER_ORB_IMPACT_SCALE_MUL = 0.8f;
    // 【2026-11-14 主人「要往前飞一段距离后爆炸」】落点从「最近目标身上」改为沿朝向固定前飞这么远再自爆；
    // 取值沿用本技能无目标时的既有兜底 8f（散射箭 CAREER_SCATTER_RANGE 9f 同尺度），想调只改这里。
    public const float CAREER_ORB_FLIGHT_DIST = 8f;
    public const float CAREER_ORB_SPEED_MUL = 0.55f;    // 大法球慢速飞行，压迫感
    // 【2026-11-14 主人「法师的大法球技能发射点再往前点」】只作用于大法球起手点，
    // 不动全局 firePointOffset（英雄普攻/散射箭共用，改了会一起挪）。想再调直接改这个值。
    public const float CAREER_ORB_FIRE_FORWARD = 0.8f;  // 沿当前朝向额外前移的世界单位

    // ===== 三职业自带技「技能精通」形态差异（2026-11-20 主人拍板）=====
    // 精通判定 = R_DUAL（技能精通）右列节点等级 > 0，见 CareerSkillExecutor.IsCareerMastered。
    // 精通后一律走现有正式数值（箭数/命中读 player_skills.csv，盾/法球大小读上文倍率），不削弱现状；
    // 未精通读下列弱化值 —— 玩家点了「技能精通」天赋，形态从弱化版切到现在的完整版。
    // 【主人原话】「没精通前盾小，精通后盾大了一倍就是现在的大小」「游侠一开始射3个箭，精通后6个」
    // 「法师一开始小法球打3个，精通后大的打5个」；盾/法球大小以现值为精通后基准，只加未精通弱化档。
    public const int   CAREER_SCATTER_UNMASTERED_ARROWS = 3;       // 未精通散射箭数（精通后读 CSV projectileCount=5）
    public const int   CAREER_ORB_UNMASTERED_MAX_TARGETS = 3;      // 未精通法球命中数（精通后读 CSV aoeMaxTargets=4）
    public const float CAREER_ORB_UNMASTERED_SCALE_MUL = 1f;       // 未精通法球大小（精通后 = CAREER_ORB_SCALE_MUL=2）
    public const float CAREER_CHARGE_UNMASTERED_SHIELD_MUL = 0.5f; // 未精通盾 = 精通后尺寸 × 0.5（撞击框随盾尺寸自动推导）

    // ---- 受击后退（2026-11-05 主人拍板：普通怪受击小退；精英/Boss 免击退；它们打玩家/俇兵把人打退）----
    public const float KNOCKBACK_MONSTER = 0.35f; // 普通怪受击退移（世界单位）
    public const float KNOCKBACK_ALLY = 0.7f;     // 精英/Boss 命中玩家/俇兵的击退移（世界单位）
    public const float KNOCKBACK_RETRIGGER_INTERVAL = 0.8f; // 同一单位两次击退的最小间隔（2026-11-06）：留出「走回来再交手」的时间；原 0.15s 时连续受击的怪会被钉在击退落点，永远够不着攻击距离
    /// <summary>击退位移时长（秒）。距离不变、时间越短退得越快；2026-11-07 主人「被击退的速度要快一点」0.15→0.09。</summary>
    public const float KNOCKBACK_DURATION = 0.09f;

    // 【2026-11-16 主人「被重斩劈死的怪要有击退效果」】重斩致死那一刀的击退距离（世界单位）。
    // 走独立"位置直推"通道（见 UnitBase.Damage.ApplyDeathKnockback），避开 Die() 的 rb.velocity 清零；
    // 精英/Boss 免退沿用全局规则。用职业技强击退同档，保证肉眼可见。
    public const float HEAVY_CLEAVE_DEATH_KNOCKBACK = 1.6f;
}
