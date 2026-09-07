using UnityEngine;

/// <summary>
/// 玩家进战职业：玩家算队伍一员；决定默认技能与武器掉落池。
/// 展示字段对齐 Assets/Data/Source/Tables/player_jobs.csv。
/// </summary>
public enum PlayerJobId
{
    SwordShield = 0,
    Berserker = 1,
    Ranger = 2,
    Mage = 3,
    Priest = 4,
    Heavy = 5
}

public static class PlayerJobDefs
{
    public struct Def
    {
        public PlayerJobId Id;
        /// <summary>配置表职业ID，如 P001。</summary>
        public string ConfigId;
        public string DisplayName;
        public string DefaultSkillId;
        public WeaponCombatTable.WeaponKind PrimaryWeapon;
        /// <summary>对应职业 101 佣兵 AssetId（武器外观/种类以此为准）。</summary>
        public string MercAssetId;
        public string Blurb;
        public string LongDesc;
        public string HpLabel;
        public string AtkLabel;
        /// <summary>操作难度档（高/中/低），UI 显示为「操控」。</summary>
        public string DiffLabel;
        /// <summary>推荐星数 1~3。</summary>
        public int RecommendStars;
        /// <summary>攻击距离标签：近/中/远（展示用；实战射程仍由武器决定）。</summary>
        public string RangeLabel;
        /// <summary>攻击方式文案（展示用）。</summary>
        public string AttackStyleLabel;
        /// <summary>解锁条件文案。</summary>
        public string UnlockLabel;
        /// <summary>Resources 下图标路径，缺资源时保留 prefab 图。</summary>
        public string IconResourcePath;
    }

    public static readonly Def[] All =
    {
        new Def
        {
            Id = PlayerJobId.SwordShield,
            ConfigId = "P001",
            DisplayName = "剑盾卫士",
            DefaultSkillId = "holy_barrier",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Sword,
            MercAssetId = "dunbing101",
            Blurb = "近战防御，保护队友",
            LongDesc = "近战防御型，能吸引敌人注意并保护队友，适合新手稳扎稳打。",
            HpLabel = "高",
            AtkLabel = "中",
            DiffLabel = "低",
            RecommendStars = 3,
            RangeLabel = "近",
            AttackStyleLabel = "近战普攻+格挡光环",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/剑盾"
        },
        new Def
        {
            Id = PlayerJobId.Berserker,
            ConfigId = "P003",
            DisplayName = "狂战士",
            DefaultSkillId = "battle_surge",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Greatsword,
            MercAssetId = "kuangzhan101",
            Blurb = "近战爆发，贴身输出",
            LongDesc = "近战爆发型，攻击距离短但伤害极高，需要贴身输出。",
            HpLabel = "中",
            AtkLabel = "高",
            DiffLabel = "中",
            RecommendStars = 2,
            RangeLabel = "近",
            AttackStyleLabel = "近战普攻+嗜血被动",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/狂战"
        },
        new Def
        {
            Id = PlayerJobId.Ranger,
            ConfigId = "P004",
            DisplayName = "游侠",
            DefaultSkillId = "deadly_focus",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Bow,
            MercAssetId = "gongshou101",
            Blurb = "远程物理，机动输出",
            LongDesc = "远程物理型，灵活机动，能在战斗中保持安全距离持续输出。",
            HpLabel = "中",
            AtkLabel = "中高",
            DiffLabel = "中",
            RecommendStars = 2,
            RangeLabel = "远",
            AttackStyleLabel = "远程普攻+穿透箭",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/游侠"
        },
        new Def
        {
            Id = PlayerJobId.Mage,
            ConfigId = "P005",
            DisplayName = "法师",
            DefaultSkillId = "thunder_verdict",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Staff,
            MercAssetId = "fashi101",
            Blurb = "远程魔法，高伤脆弱",
            LongDesc = "远程魔法型，伤害高但身板脆，对走位和站位要求最高。",
            HpLabel = "低",
            AtkLabel = "高",
            DiffLabel = "高",
            RecommendStars = 1,
            RangeLabel = "远",
            AttackStyleLabel = "远程普攻+元素弹射",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/法师"
        },
        new Def
        {
            Id = PlayerJobId.Priest,
            ConfigId = "P006",
            DisplayName = "牧师",
            DefaultSkillId = "heal_spring",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Staff,
            MercAssetId = "naima101",
            Blurb = "远程支援，治疗保障",
            LongDesc = "远程支援型，拥有强大的治疗能力，是团队中不可或缺的生存保障。",
            HpLabel = "高",
            AtkLabel = "低",
            DiffLabel = "低",
            RecommendStars = 3,
            RangeLabel = "中",
            AttackStyleLabel = "远程普攻+治疗光环",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/牧师"
        },
        new Def
        {
            Id = PlayerJobId.Heavy,
            ConfigId = "P002",
            DisplayName = "重武者",
            DefaultSkillId = "gale_stance",
            PrimaryWeapon = WeaponCombatTable.WeaponKind.Polearm,
            MercAssetId = "zhongzhan101",
            Blurb = "近战控制，撞击眩晕",
            LongDesc = "近战控制型，移动撞击可眩晕敌人，擅长封锁路线和打断。",
            HpLabel = "高",
            AtkLabel = "中高",
            DiffLabel = "中",
            RecommendStars = 2,
            RangeLabel = "近",
            AttackStyleLabel = "近战普攻+撞击眩晕",
            UnlockLabel = "初始解锁",
            IconResourcePath = "UI/JobSelect/重武"
        }
    };

    public static Sprite TryLoadJobIcon(PlayerJobId id)
    {
        var def = Get(id);
        if (string.IsNullOrEmpty(def.IconResourcePath)) return null;
        var sp = Resources.Load<Sprite>(def.IconResourcePath);
        if (sp != null) return sp;
        // 整图未切 Sprite 时尝试 Texture → 临时 Sprite（仅兜底）
        var tex = Resources.Load<Texture2D>(def.IconResourcePath);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Def Get(PlayerJobId id)
    {
        int i = (int)id;
        if (i < 0 || i >= All.Length) return All[0];
        return All[i];
    }

    public static PlayerJobId GetSelected()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return PlayerJobId.SwordShield;
        int v = data.selectedPlayerJobId;
        if (v < 0 || v >= All.Length) return PlayerJobId.SwordShield;
        return (PlayerJobId)v;
    }

    public static void SetSelected(PlayerJobId id)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.selectedPlayerJobId = (int)id;
        var def = Get(id);
        if (!string.IsNullOrEmpty(def.DefaultSkillId))
            data.selectedPlayerSkillId = def.DefaultSkillId;
        SaveSystem.Instance?.Save();
    }

    public static bool TemplateMatchesJob(EquipTemplate tpl, PlayerJobId job)
    {
        if (tpl == null) return false;
        // 非武器：通用可掉
        if (tpl.slotType != EquipSlotType.MainHand && tpl.slotType != EquipSlotType.OffHand)
            return true;
        if (tpl.weaponType == WeaponType.None && IsShieldTemplate(tpl))
            return job == PlayerJobId.SwordShield;
        if (tpl.weaponType == WeaponType.None)
            return true;
        var kind = WeaponCombatTable.ResolveKind(tpl);
        var want = Get(job).PrimaryWeapon;
        if (kind == want) return true;
        // 剑盾也可掉盾；狂战也可掉双手斧类（已归 Greatsword）
        if (job == PlayerJobId.SwordShield && kind == WeaponCombatTable.WeaponKind.Shield)
            return true;
        return false;
    }

    static bool IsShieldTemplate(EquipTemplate t)
    {
        if (t == null || t.slotType != EquipSlotType.OffHand) return false;
        string n = (t.spumName ?? t.iconFileName ?? "").ToLowerInvariant();
        return n.Contains("shield") || n.Contains("盾");
    }

    /// <summary>
    /// 开战：按所选职业对应 101 佣兵只发武器（不含护甲），装备后 RefreshCostume。
    /// </summary>
    public static void ApplyForBattle()
    {
        PlayerJobBaseStats.EnsureLoaded();
        var job = GetSelected();
        var def = Get(job);
        var data = SaveSystem.Instance?.Data;
        if (data != null && !string.IsNullOrEmpty(def.DefaultSkillId))
        {
            data.selectedPlayerSkillId = def.DefaultSkillId;
            SaveSystem.Instance?.Save();
        }

        var bag = GridBackpackSystem.Instance;
        if (bag == null || ConfigManager.Instance == null) return;

        bool granted = false;
        if (!HasPrimaryWeaponEquippedOrInBag(bag, job))
        {
            EquipTemplate tpl = FindJobWeaponTemplate(job);
            if (tpl != null)
                granted |= GrantAndEquipWeapon(bag, tpl);
        }

        // 剑盾：副手盾（仍属武器位，不发护甲）
        if (job == PlayerJobId.SwordShield && !HasShieldEquippedOrInBag(bag))
        {
            EquipTemplate shield = ConfigManager.Instance.FindBasicShieldTemplate();
            if (shield != null)
                granted |= GrantAndEquipWeapon(bag, shield);
        }

        Hero.Instance?.RecalcAttr();
        Hero.Instance?.costumeManager?.RefreshCostume();
        if (Hero.Instance != null && Hero.Instance.attr != null)
            Hero.Instance.currentHp = Hero.Instance.attr.GetAttr(AttrType.MaxHp);
        PlayerPassiveCombat.EnsureOn(Hero.Instance);
        if (granted)
            UIManager.Instance?.ShowToast($"已获得{def.DisplayName}武器");
    }

    static bool GrantAndEquipWeapon(GridBackpackSystem bag, EquipTemplate tpl)
    {
        if (tpl == null) return false;
        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, lv, true, Rarity.Common);
        if (inst == null) return false;
        if (!bag.TryAddUniqueBySlot(inst, out _))
        {
            UIManager.Instance?.ShowToast("背包满，无法发放职业武器");
            return false;
        }
        return true;
    }

    static bool HasPrimaryWeaponEquippedOrInBag(GridBackpackSystem bag, PlayerJobId job)
    {
        var want = Get(job).PrimaryWeapon;
        var items = bag.GetAllBackpackItems();
        if (items == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]?.equip;
            if (e?.template == null) continue;
            if (e.slotType != EquipSlotType.MainHand && e.slotType != EquipSlotType.OffHand) continue;
            if (e.weaponType == WeaponType.None) continue;
            if (WeaponCombatTable.ResolveKind(e.template) == want)
                return true;
        }
        return false;
    }

    static bool HasShieldEquippedOrInBag(GridBackpackSystem bag)
    {
        var items = bag.GetAllBackpackItems();
        if (items == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i]?.equip;
            if (e?.template == null) continue;
            if (IsShieldTemplate(e.template))
                return true;
        }
        return false;
    }

    static EquipTemplate FindJobWeaponTemplate(PlayerJobId job)
    {
        var cfg = ConfigManager.Instance;
        if (cfg == null) return null;
        return cfg.FindWeaponTemplateByKind(Get(job).PrimaryWeapon);
    }
}
