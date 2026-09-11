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
        public const string MercSkillMap = "Data/Tables/merc_skill_map";
        public const string MercLines = "Data/Tables/merc_lines";
        public const string IntelQuiz = "Data/Tables/intel_quiz";
        public const string IntelDaily = "Data/Tables/intel_daily";
        public const string MonsterSpriteOpaque = "Data/Tables/monster_sprite_opaque";
        public const string EquipAnchors = "Data/Tables/equip_anchors";
        public const string ConfigFingerprint = "Data/Tables/config_fingerprint";
        public const string MonsterStats = "Data/Tables/monster_stats";
        public const string ChapterThemeMap = "Data/Tables/chapter_theme_map";
        public const string MonsterUnlockTier = "Data/Tables/monster_unlock_tier";
        public const string StageSpawn = "Data/Tables/stage_spawn";
        public const string TutorialBattle = "Data/Tables/tutorial_battle";
        public const string BattleQuest = "Data/Tables/battle_quest";
        public const string StageRollerWeights = "Data/Tables/stage_roller_weights";
        public const string SpritePickWeight = "Data/Tables/sprite_pick_weight";
        /// <summary>未启用占位。空表时 WaveSlotTable 回退奇偶逻辑。</summary>
        public const string WaveSlot = "Data/Tables/wave_slot";
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
    }

    public static class Icons
    {
        public const string MercSkill = "Icons/MercSkill";
        public const string MercHead = "Icons/MercHead";
        public const string MercStand = "Icons/MercStand";
        public const string PlayerSkill = "Icons/SkillIcon";
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
