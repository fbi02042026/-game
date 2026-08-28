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
        public const string MonsterSpriteOpaque = "Data/Tables/monster_sprite_opaque";
        public const string EquipAnchors = "Data/Tables/equip_anchors";
        public const string ConfigFingerprint = "Data/Tables/config_fingerprint";
    }

    public static class Icons
    {
        public const string MercSkill = "Icons/MercSkill";
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
        public const string Dialogue = "Prefabs/Dialogue/DialogueUI";
        public const string Login = "Prefabs/Login/LoginUI";
        public const string HealthNotice = "Prefabs/Boot/HealthNoticeUI";
    }

    public static class Source
    {
        public const string Tables = "Assets/Data/Source/Tables";
    }
}
