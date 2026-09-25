/// <summary>
/// 运行时资源与数据路径。调用方只走这里，不写散落的中文路径。
/// ScriptableObject 仍用 Unity 序列化（编辑器可改）；进包表与存档走加密字节。
/// </summary>
public static class ContentPaths
{
    public static class Data
    {
        public const string TablesRoot = "Data/Tables";
        public const string MonsterAttackStyle = "Data/Tables/monster_attack_style";
        public const string MercSkills = "Data/Tables/merc_skills";
        /// <summary>玩家技能元数据 + 战斗数值。Ally SO 仅 VFX。</summary>
        public const string PlayerSkills = "Data/Tables/player_skills";
        public const string MercSkillMap = "Data/Tables/merc_skill_map";
        public const string MercLines = "Data/Tables/merc_lines";
        public const string IntelQuiz = "Data/Tables/intel_quiz";
        public const string IntelDaily = "Data/Tables/intel_daily";
        public const string MonsterSpriteOpaque = "Data/Tables/monster_sprite_opaque";
        public const string EquipAnchors = "Data/Tables/equip_anchors";
        public const string ConfigFingerprint = "Data/Tables/config_fingerprint";
        public const string MonsterStats = "Data/Tables/monster_stats";
        public const string ChapterThemeMap = "Data/Tables/chapter_theme_map";
        /// <summary>章节路线：主线 1→2→5→6→7→8，第 3/4 章是通关第 8 章后开放的支线。</summary>
        public const string ChapterRoute = "Data/Tables/chapter_route";
        /// <summary>章节属性倍率（与旧 CHAPTER_STAT_SCALE 1:1）。</summary>
        public const string ChapterStatScale = "Data/Tables/chapter_stat_scale";
        public const string MonsterUnlockTier = "Data/Tables/monster_unlock_tier";
        /// <summary>道具定义表（2026-09-15 新增；背包格子已改为只装道具）。</summary>
        public const string ItemDefs = "Data/Tables/item_defs";
        public const string StageSpawn = "Data/Tables/stage_spawn";
        /// <summary>关卡掉落：徽记掉落率 / 本命碎片来源（2026-09-18 新增，见 stage_drop.csv）。</summary>
        public const string StageDrop = "Data/Tables/stage_drop";
        public const string TutorialBattle = "Data/Tables/tutorial_battle";
        public const string BattleQuest = "Data/Tables/battle_quest";
        public const string StageRollerWeights = "Data/Tables/stage_roller_weights";
        public const string SpritePickWeight = "Data/Tables/sprite_pick_weight";
        /// <summary>波次槽：奇偶近战/远程（spriteIndex=0 走加权）。未匹配 slot 仍回退奇偶。</summary>
        public const string WaveSlot = "Data/Tables/wave_slot";
        /// <summary>波次原型：进场方向 / 近远编成 / 出怪间隔 / 精英额度。优先于 wave_slot。</summary>
        public const string WaveArchetype = "Data/Tables/wave_archetype";
        /// <summary>关卡模式：每关随机抽一个，决定本关的波次原型序列（章内不重复）。</summary>
        public const string StageMode = "Data/Tables/stage_mode";
        /// <summary>关卡事件层：波间随机插入的有取舍事件（id/权重/起始章节/预告/效果参数）。</summary>
        public const string StageEvent = "Data/Tables/stage_event";
        public const string ChapterBranch = "Data/Tables/chapter_branch";
        public const string ChapterBranchRules = "Data/Tables/chapter_branch_rules";
        public const string EquipSlotPools = "Data/Tables/equip_slot_pools";
        public const string EquipRarityRules = "Data/Tables/equip_rarity_rules";
        public const string EquipAttrRanges = "Data/Tables/equip_attr_ranges";
        /// <summary>未启用：无运行时读取，已移出 CookAll。</summary>
        public const string RiftEquipGenSteps = "Data/Tables/rift_equip_gen_steps";
        public const string PlayerJobBaseStats = "Data/Tables/player_job_base_stats";
        public const string AttackRange = "Data/Tables/attack_range";
        public const string JobWeapons = "Data/Tables/job_weapons";
        public const string PlayerPassives = "Data/Tables/player_passives";
        public const string HiddenLevelRules = "Data/Tables/hidden_level_rules";
        public const string EquipAppearanceMap = "Data/Tables/equip_appearance_map";
        /// <summary>右列天赋重制表（CSV → .bytes，见 talent_right.csv）。缺表时 TalentDefs 回退硬编码。</summary>
        public const string TalentRight = "Data/Tables/talent_right";
        /// <summary>佣兵花名册（CSV → .bytes，见 merc_roster.csv）。缺表时 MercRosterDefs 回退硬编码。</summary>
        public const string MercRoster = "Data/Tables/merc_roster";
        /// <summary>战斗调参表（CSV → .bytes，见 combat_tuning.csv）。缺表时 GameConfig 回退硬编码。</summary>
        public const string CombatTuning = "Data/Tables/combat_tuning";
        /// <summary>Boss 关变体：Boss 波的亲卫 / 软性限时涌怪（CSV → .bytes，见 boss_stage_variant.csv）。缺表时回退「只有 Boss 本体」。</summary>
        public const string BossStageVariant = "Data/Tables/boss_stage_variant";
        /// <summary>每日登录奖励（CSV → .bytes，见 daily_login.csv）。starter/cycle/streak 三表合一，用 table 列区分。缺表 / 空表时 DailyLoginDefs 回退硬编码数组（D4=佣兵、D8=碎片由表覆盖）。</summary>
        public const string DailyLogin = "Data/Tables/daily_login";
        /// <summary>节日 / 特定日登录奖励覆盖（CSV → .bytes，见 daily_login_special.csv）。仅覆盖新手格当天奖励，命中当天整条替换；date 支持 MM-DD（年年生效）与 YYYY-MM-DD（仅该年）。</summary>
        public const string DailyLoginSpecial = "Data/Tables/daily_login_special";
    }

    public static class Icons
    {
        public const string MercSkill = "Icons/MercSkill";
        public const string MercHead = "Icons/MercHead";
        public const string MercStand = "Icons/MercStand";
        public const string PlayerSkill = "Icons/SkillIcon";
        /// <summary>职业徽记：6 职业 × 普通/稀有/传奇。Art 源 Assets/Art/UI/Icons/徽章。</summary>
        public const string JobBadge = "Icons/JobBadge";
        /// <summary>佣兵本命碎片底版：普通/稀有/传奇。Art 源 Assets/Art/UI/Icons/人物碎片。</summary>
        public const string MercFragmentBase = "Icons/MercFragmentBase";
    }

    public static class Config
    {
        public const string Equips = "Config/Equips";
        public const string Monsters = "Config/Monsters";
        public const string Talents = "Config/Talents";
        public const string SkillsAlly = "Config/Skills/Ally";
        public const string SkillsMonster = "Config/Skills/Monster";
        public const string SkillsPlayerLegacy = "Config/Skills/Player";
        public const string SkillsMercLegacy = "Config/Skills/Merc";
        public const string CharacterRegistry = "Config/CharacterRegistry";
        public const string BattleBackgrounds = "Config/BattleBackgroundRegistry";
        public const string MonsterSprites = "Config/MonsterSpriteRegistry";
    }

    public static class Story
    {
        public const string Root = "Story";
        public const string Portraits = "Portraits";
        public const string Backgrounds = "Backgrounds";
        public const string Props = "Props";
    }

    public static class Prefab
    {
        public const string GuildHall = "Prefabs/Town/GuildHallUI";
        public const string PlayerNaming = "Prefabs/Town/PlayerNamingUI";
        public const string Dialogue = "Prefabs/Dialogue/DialogueUI";
        public const string Login = "Prefabs/Login/LoginUI";
        public const string HealthNotice = "Prefabs/Boot/HealthNoticeUI";
    }

    public static class Ui
    {
        public const string PlayerNaming = "UI/PlayerNaming";
        /// <summary>开机工作室 Logo（Resources，无扩展名）。</summary>
        public const string StudioLogo = "UI/Boot/studio_logo";
        /// <summary>登录前动画视频（Resources/UI/Boot/登录动画）。</summary>
        public const string LoginIntro = "UI/Boot/登录动画";
    }

    public static class Source
    {
        public const string Tables = "Assets/Data/Source/Tables";
    }
}
