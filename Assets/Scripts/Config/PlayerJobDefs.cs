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
        if (t == null) return false;
        if (t.slotType == EquipSlotType.OffHand && t.weaponType == WeaponType.None) return true;
        string n = (t.spumName ?? t.iconFileName ?? t.templateId ?? "").ToLowerInvariant();
        return n.Contains("shield") || n.Contains("盾");
    }

    struct JobWeaponKit
    {
        public string MainTemplateId;
        public string OffTemplateId;
        /// <summary>无模板时强制的 SPUM 名（法师 Ward_1）。</summary>
        public string MainSpumOverride;
        public bool ForceMainOneHand;
        /// <summary>游侠弓等双手武器。</summary>
        public bool ForceMainTwoHand;
        public bool ForceOffHand;
    }

    /// <summary>各职业对应 *101 佣兵预制体上的武器套（写死）。</summary>
    static JobWeaponKit GetJobWeaponKit(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.SwordShield:
                return new JobWeaponKit
                {
                    MainTemplateId = "equip_new_weapon_04",
                    OffTemplateId = "equip_steelshield1",
                    ForceMainOneHand = true
                };
            case PlayerJobId.Berserker:
                return new JobWeaponKit
                {
                    MainTemplateId = "equip_new_weapon_06",
                    OffTemplateId = "equip_new_weapon_09",
                    ForceMainOneHand = true,
                    ForceOffHand = true
                };
            case PlayerJobId.Ranger:
                return new JobWeaponKit
                {
                    MainTemplateId = "equip_new_weapon_12",
                    ForceMainTwoHand = true
                };
            case PlayerJobId.Mage:
                return new JobWeaponKit
                {
                    MainTemplateId = "weapon_twilight_staff",
                    MainSpumOverride = "Ward_1",
                    ForceMainOneHand = true
                };
            case PlayerJobId.Priest:
                return new JobWeaponKit
                {
                    MainTemplateId = "equip_new_weapon_03",
                    ForceMainOneHand = true
                };
            case PlayerJobId.Heavy:
                return new JobWeaponKit { MainTemplateId = "equip_axenormal1" };
            default:
                return new JobWeaponKit { MainTemplateId = "equip_new_weapon_04" };
        }
    }

    /// <summary>
    /// 开战：按所选职业对应 101 佣兵武器套写死发装（不含护甲），装备后 RefreshCostume。
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
        if (bag == null)
        {
            Debug.LogWarning("[PlayerJobDefs] ApplyForBattle: GridBackpackSystem 为空");
            return;
        }

        var kit = GetJobWeaponKit(job);
        bool granted = false;
        var jobKind = def.PrimaryWeapon;

        // 先发主手（会清对应槽）；再发副手
        if (!string.IsNullOrEmpty(kit.MainTemplateId) || !string.IsNullOrEmpty(kit.MainSpumOverride))
        {
            var mainTpl = ResolveKitTemplate(kit.MainTemplateId, kit.MainSpumOverride);
            if (mainTpl != null)
                granted |= GrantAndEquipWeapon(bag, mainTpl, kit.ForceMainOneHand, kit.ForceMainTwoHand, forceOffHand: false, kit.MainSpumOverride, forceKind: jobKind);
            else
                Debug.LogWarning($"[PlayerJobDefs] 主手模板缺失: id={kit.MainTemplateId} spum={kit.MainSpumOverride}");
        }

        if (!string.IsNullOrEmpty(kit.OffTemplateId))
        {
            var offTpl = ResolveKitTemplate(kit.OffTemplateId, null);
            if (offTpl != null)
                granted |= GrantAndEquipWeapon(bag, offTpl, forceOneHand: true, forceTwoHand: false, forceOffHand: kit.ForceOffHand || IsShieldTemplate(offTpl), spumOverride: null);
            else
                Debug.LogWarning($"[PlayerJobDefs] 副手模板缺失: id={kit.OffTemplateId}");
        }

        Hero.Instance?.RecalcAttr();
        var costume = Hero.Instance?.costumeManager;
        if (costume != null)
        {
            costume.EnsureRigReady();
            costume.RefreshCostume();
        }
        if (Hero.Instance != null && Hero.Instance.attr != null)
            Hero.Instance.currentHp = Hero.Instance.attr.GetAttr(AttrType.MaxHp);
        PlayerPassiveCombat.EnsureOn(Hero.Instance);
        if (granted)
            UIManager.Instance?.ShowToast($"已获得{def.DisplayName}武器");
    }

    static EquipTemplate ResolveKitTemplate(string templateId, string spumFallback)
    {
        if (!string.IsNullOrEmpty(templateId))
        {
            var t = LoadEquipTemplate(templateId);
            if (t != null) return t;
        }
        // 法师 Ward_1 无独立模板：用任意 Staff 壳
        if (!string.IsNullOrEmpty(spumFallback))
        {
            var cfg = ConfigManager.Instance;
            if (cfg != null)
            {
                var staff = cfg.FindWeaponTemplateByKind(WeaponCombatTable.WeaponKind.Staff);
                if (staff != null) return staff;
            }
            return LoadEquipTemplate("equip_new_weapon_03");
        }
        return null;
    }

    static EquipTemplate LoadEquipTemplate(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;
        var cfg = ConfigManager.Instance;
        if (cfg != null)
        {
            var t = cfg.GetEquipTemplate(templateId);
            if (t != null) return t;
        }
        var loaded = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/" + templateId);
        if (loaded != null)
        {
            loaded.ResolveIcon();
            cfg?.RegisterRuntimeEquip(loaded);
        }
        return loaded;
    }

    static bool GrantAndEquipWeapon(
        GridBackpackSystem bag,
        EquipTemplate tpl,
        bool forceOneHand,
        bool forceTwoHand,
        bool forceOffHand,
        string spumOverride,
        WeaponCombatTable.WeaponKind? forceKind = null)
    {
        if (tpl == null || bag == null) return false;
        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, lv, true, Rarity.Common);
        if (inst == null)
        {
            Debug.LogWarning($"[PlayerJobDefs] GenerateFromTemplate 失败: {tpl.templateId}");
            return false;
        }

        if (forceTwoHand)
            inst.weaponType = WeaponType.TwoHand;
        else if (forceOneHand && inst.weaponType == WeaponType.TwoHand)
            inst.weaponType = WeaponType.OneHand;
        // 资源里部分武器 weaponType 未标；非盾则按单手武器处理，保证入包即生效
        if (!forceTwoHand && inst.weaponType == WeaponType.None && !IsShieldTemplate(tpl) && !WeaponLoadoutRules.IsShield(inst))
            inst.weaponType = WeaponType.OneHand;
        if (forceOneHand && !forceTwoHand && inst.weaponType != WeaponType.TwoHand)
            inst.weaponType = WeaponType.OneHand;

        if (forceOffHand)
        {
            inst.weaponHand = WeaponHandSlot.OffHand;
            inst.slotType = EquipSlotType.OffHand;
        }
        else
        {
            if (inst.weaponHand == WeaponHandSlot.None)
                inst.weaponHand = WeaponHandSlot.MainHand;
            inst.slotType = EquipSlotType.MainHand;
        }
        if (!string.IsNullOrEmpty(spumOverride))
            inst.spumNameOverride = spumOverride;

        // 职业起步武器：模板显示名/射程常标错（如游侠弓叫「短刃」），写死逻辑类型到实例
        if (forceKind.HasValue)
        {
            inst.weaponKindOverride = (int)forceKind.Value;
            if (forceKind.Value == WeaponCombatTable.WeaponKind.Staff)
                inst.weaponAttackType = WeaponAttackType.Magic;
        }

        // 格子过高塞不进默认行：压矮到可放入
        int unlocked = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        if (inst.gridHeight > unlocked)
            inst.gridHeight = Mathf.Max(1, unlocked);
        if (inst.gridWidth > GameConfig.BACKPACK_WIDTH)
            inst.gridWidth = GameConfig.BACKPACK_WIDTH;

        if (!bag.TryAddUniqueBySlot(inst, out GridBackpackSystem.BackpackItem item))
        {
            Debug.LogWarning($"[PlayerJobDefs] 入包失败: {inst.equipName} ({tpl.templateId}) {inst.gridWidth}x{inst.gridHeight}");
            UIManager.Instance?.ShowToast("背包满，无法发放职业武器");
            return false;
        }
        if (item != null)
            bag.EquipItem(item);
        return true;
    }
}
