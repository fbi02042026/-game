using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竞技场赛季冠军限定武器（2026-10-29 晚主人拍板）：
/// · 每月结算第 1 名随结算邮件发一把「当月限定传奇武器」，12 个月轮换，只有竞技场能给；
/// · 模板纯运行时注册（<see cref="ConfigManager.RegisterRuntimeEquip"/>），不落 Resources 目录
///   —— 掉落/抽奖的模板池走 Resources.LoadAll，永远扫不到这批 id，天然「竞技场独占」；
/// · 拥有判定真源 = 随身仓库 legacyEquipPool 里有没有这批 id（邮件 equip 附件领取时写入，
///   单一事实来源，不另设存档字段）；出战时（PlayerJobDefs.ApplyForBattle）若拥有与当前职业
///   匹配的冠军武器 → 生成传奇品质实例替换初始主手（照「稀有·暮火之杖替换法师初始武器」先例）；
/// · 外观三者同款：图标 iconFileName 与手上贴图 spumName 用同一组现役武器外观对
///   （全部取自现有模板已在用的组合，不新增美术资源，铁律「不改 prefab/meta/asset」零触碰）。
/// </summary>
public static class ArenaSeasonWeapons
{
    /// <summary>单把冠军武器定义（月轮换表的一行）。</summary>
    public struct Def
    {
        public int month;            // 1~12，哪个月的赛季冠军发这把
        public string id;            // 模板 id（运行时注册，Resources 里没有 → 掉落池不可见）
        public string name;          // 武器纯名（显示名由词条系统统一加「传奇·」前缀）
        public PlayerJobId job;      // 归属职业（出战时只替换该职业的初始主手）
        public bool twoHand;         // true=双手（游侠弓），false=单手
        public WeaponAttackType attackType;
        public int weaponKind;       // WeaponCombatTable.WeaponKind（-1=按名字推断）
        public string art;           // 外观对：iconFileName == spumName（同款，不新增美术）
        public AttrBonusData[] baseAttr;   // 基础属性（主攻击会被传奇档表格 roll 覆盖，其余保留）
        public AttrBonusData globalBonus;  // 传奇专属全局加成
        public AttrBonusData passive;      // 传奇专属被动一条
    }

    // ── 12 个月轮换表（数值档位对齐现网传奇武器：守誓长枪 ATK55/MaxHp160/Defense13+global86+被动32%爆伤）──
    // 职业轮转 1剑盾 2法牧 3游侠 循环，保证每个职业每季度轮到一把；
    // 外观全用现役模板同款对（剑盾=刀剑/枪/斧，法牧=法杖 New_Weapon_03，游侠=弓）。
    static readonly Def[] Roster =
    {
        new Def{ month=1,  id="weapon_arena_m01", name="裂空·冠军之刃", job=PlayerJobId.SwordShield, twoHand=false,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Sword, art="New_Weapon_06",
                 baseAttr=new[]{ A(AttrType.Attack,55f), A(AttrType.MaxHp,180f), A(AttrType.Defense,12f) },
                 globalBonus=A(AttrType.Attack,90f), passive=A(AttrType.CritDamage,0.35f) },
        new Def{ month=2,  id="weapon_arena_m02", name="誓约·冠军之杖", job=PlayerJobId.Mage, twoHand=false,
                 attackType=WeaponAttackType.Magic, weaponKind=(int)WeaponCombatTable.WeaponKind.Staff, art="New_Weapon_03",
                 baseAttr=new[]{ A(AttrType.MagicAttack,50f), A(AttrType.MaxHp,220f), A(AttrType.MagicDefense,14f) },
                 globalBonus=A(AttrType.MagicAttack,70f), passive=A(AttrType.MagicPower,0.30f) },
        new Def{ month=3,  id="weapon_arena_m03", name="赤焰·冠军猎弓", job=PlayerJobId.Ranger, twoHand=true,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Bow, art="New_Weapon_12",
                 baseAttr=new[]{ A(AttrType.Attack,60f), A(AttrType.MaxHp,120f) },
                 globalBonus=A(AttrType.Attack,85f), passive=A(AttrType.CritRate,0.08f) },
        new Def{ month=4,  id="weapon_arena_m04", name="黎明·冠军长枪", job=PlayerJobId.SwordShield, twoHand=false,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Polearm, art="New_Weapon_09",
                 baseAttr=new[]{ A(AttrType.Attack,55f), A(AttrType.MaxHp,180f), A(AttrType.Defense,12f) },
                 globalBonus=A(AttrType.Attack,90f), passive=A(AttrType.CritDamage,0.35f) },
        new Def{ month=5,  id="weapon_arena_m05", name="灰烬·冠军钉锤", job=PlayerJobId.Priest, twoHand=false,
                 attackType=WeaponAttackType.Magic, weaponKind=(int)WeaponCombatTable.WeaponKind.Staff, art="New_Weapon_03",
                 baseAttr=new[]{ A(AttrType.MagicAttack,50f), A(AttrType.MaxHp,220f), A(AttrType.MagicDefense,14f) },
                 globalBonus=A(AttrType.MagicAttack,70f), passive=A(AttrType.MagicPower,0.30f) },
        new Def{ month=6,  id="weapon_arena_m06", name="暗潮·冠军短弓", job=PlayerJobId.Ranger, twoHand=true,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Bow, art="New_Weapon_10",
                 baseAttr=new[]{ A(AttrType.Attack,60f), A(AttrType.MaxHp,120f) },
                 globalBonus=A(AttrType.Attack,85f), passive=A(AttrType.CritRate,0.08f) },
        new Def{ month=7,  id="weapon_arena_m07", name="守誓·冠军之枪", job=PlayerJobId.SwordShield, twoHand=false,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Polearm, art="New_Weapon_16",
                 baseAttr=new[]{ A(AttrType.Attack,55f), A(AttrType.MaxHp,180f), A(AttrType.Defense,12f) },
                 globalBonus=A(AttrType.Attack,90f), passive=A(AttrType.CritDamage,0.35f) },
        new Def{ month=8,  id="weapon_arena_m08", name="星辉·冠军法杖", job=PlayerJobId.Mage, twoHand=false,
                 attackType=WeaponAttackType.Magic, weaponKind=(int)WeaponCombatTable.WeaponKind.Staff, art="New_Weapon_03",
                 baseAttr=new[]{ A(AttrType.MagicAttack,50f), A(AttrType.MaxHp,220f), A(AttrType.MagicDefense,14f) },
                 globalBonus=A(AttrType.MagicAttack,70f), passive=A(AttrType.MagicPower,0.30f) },
        new Def{ month=9,  id="weapon_arena_m09", name="猎手·冠军长弓", job=PlayerJobId.Ranger, twoHand=true,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Bow, art="Bow_1",
                 baseAttr=new[]{ A(AttrType.Attack,60f), A(AttrType.MaxHp,120f) },
                 globalBonus=A(AttrType.Attack,85f), passive=A(AttrType.CritRate,0.08f) },
        new Def{ month=10, id="weapon_arena_m10", name="骑士·冠军战斧", job=PlayerJobId.SwordShield, twoHand=false,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Sword, art="New_Weapon_08",
                 baseAttr=new[]{ A(AttrType.Attack,55f), A(AttrType.MaxHp,180f), A(AttrType.Defense,12f) },
                 globalBonus=A(AttrType.Attack,90f), passive=A(AttrType.CritDamage,0.35f) },
        new Def{ month=11, id="weapon_arena_m11", name="虚空·冠军宝杖", job=PlayerJobId.Priest, twoHand=false,
                 attackType=WeaponAttackType.Magic, weaponKind=(int)WeaponCombatTable.WeaponKind.Staff, art="New_Weapon_03",
                 baseAttr=new[]{ A(AttrType.MagicAttack,50f), A(AttrType.MaxHp,220f), A(AttrType.MagicDefense,14f) },
                 globalBonus=A(AttrType.MagicAttack,70f), passive=A(AttrType.MagicPower,0.30f) },
        new Def{ month=12, id="weapon_arena_m12", name="霜寒·冠军猎弓", job=PlayerJobId.Ranger, twoHand=true,
                 attackType=WeaponAttackType.Physical, weaponKind=(int)WeaponCombatTable.WeaponKind.Bow, art="New_Weapon_12",
                 baseAttr=new[]{ A(AttrType.Attack,60f), A(AttrType.MaxHp,120f) },
                 globalBonus=A(AttrType.Attack,85f), passive=A(AttrType.CritRate,0.08f) },
    };

    static AttrBonusData A(AttrType t, float v) => new AttrBonusData { attrType = t, value = v, isPercent = false };

    static readonly List<EquipTemplate> _registered = new List<EquipTemplate>();

    /// <summary>把 12 把冠军武器模板注册进 ConfigManager（幂等；ArenaRules.EnsurePeriods 调）。</summary>
    public static void EnsureRegistered()
    {
        if (ConfigManager.Instance == null) return;
        for (int i = 0; i < Roster.Length; i++)
        {
            var d = Roster[i];
            if (ConfigManager.Instance.GetEquipTemplate(d.id) != null) continue;
            _registered.Add(BuildTemplate(d));
        }
    }

    static EquipTemplate BuildTemplate(Def d)
    {
        var tpl = ScriptableObject.CreateInstance<EquipTemplate>();
        tpl.templateId = d.id;
        tpl.equipName = d.name;
        tpl.iconFileName = d.art;          // ResolveIcon 按 EquipIcons 文件名加载
        tpl.gridWidth = d.twoHand ? 2 : 1; // 照 ApplyDefaultGridSize 口径：双手 2×3 / 单手 1×2
        tpl.gridHeight = d.twoHand ? 3 : 2;
        tpl.baseRarity = Rarity.Legendary;
        tpl.minLevel = 1;
        tpl.slotType = EquipSlotType.MainHand;
        tpl.weaponType = d.twoHand ? WeaponType.TwoHand : WeaponType.OneHand;
        tpl.weaponHand = WeaponHandSlot.MainHand;
        tpl.weaponAttackType = d.attackType;
        tpl.attackRange = 96f;
        tpl.weaponKindOverride = d.weaponKind;
        tpl.spumName = d.art;              // 外观三者同款：手上贴图 = 图标同一组
        tpl.baseAttr = new List<AttrBonusData>(d.baseAttr);
        tpl.globalBonus = d.globalBonus;
        tpl.skillPassives = new List<AttrBonusData> { d.passive };
        ConfigManager.Instance.RegisterRuntimeEquip(tpl);
        return tpl;
    }

    /// <summary>当前月份的冠军武器 id（自然月轮换；EnsureMonth 发结算邮件用）。</summary>
    public static string CurrentSeasonWeaponId()
    {
        return Roster[Mathf.Clamp(System.DateTime.Now.Month, 1, 12) - 1].id;
    }

    /// <summary>邮件附件图标路径（EquipIcons 的 Resources 路径，MailDetailUI iconOverride 用）。</summary>
    public static string MailIconPath(string weaponId)
    {
        var d = FindById(weaponId);
        return d.HasValue ? EquipIcons.ResourcesPath + d.Value.art : null;
    }

    /// <summary>当月限定武器的告知窗图标（弹窗打开前 EnsureRegistered 已跑；缺模板/缺图 LogError 不兜底）。</summary>
    public static UnityEngine.Sprite CurrentWeaponIcon()
    {
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(CurrentSeasonWeaponId()) : null;
        if (tpl == null)
        {
            Debug.LogError("[ArenaWeapons] 当月冠军武器模板未注册，图标取不到");
            return null;
        }
        tpl.ResolveIcon();
        if (tpl.icon == null) Debug.LogError("[ArenaWeapons] 当月冠军武器图标缺失：" + tpl.templateId);
        return tpl.icon;
    }

    /// <summary>
    /// 当月限定武器的说明段（唯一出口）：显示名（传奇前缀，与实战命名同源）+ 基础属性写明；
    /// 随机词条/被动保持神秘感（2026-10-29 晚主人口径「随机属性保持神秘感」），只写「获得后揭晓」。
    /// </summary>
    public static string CurrentWeaponInfoText()
    {
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(CurrentSeasonWeaponId()) : null;
        if (tpl == null) return "";
        string text = WeaponAffixSystem.RarityPrefix(Rarity.Legendary) + tpl.equipName + "\n";
        if (tpl.baseAttr != null)
        {
            for (int i = 0; i < tpl.baseAttr.Count; i++)
            {
                var b = tpl.baseAttr[i];
                if (b == null) continue;
                if (i > 0) text += " / ";
                text += EquipUiText.Attr(b.attrType) + " +" + EquipUiText.Value(b.attrType, b.value, b.isPercent);
            }
            text += "\n";
        }
        text += "随机词条与武器被动：获得后揭晓";
        return text;
    }

    /// <summary>冠军武器纯名（邮件文案用；查无此 id 返回 null）。</summary>
    public static string WeaponName(string weaponId)
    {
        var d = FindById(weaponId);
        return d.HasValue ? d.Value.name : null;
    }

    static Def? FindById(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return null;
        for (int i = 0; i < Roster.Length; i++)
            if (Roster[i].id == weaponId) return Roster[i];
        return null;
    }

    /// <summary>
    /// 出战选装：在随身仓库（legacyEquipPool，邮件领取时写入）里找已拥有的冠军武器，
    /// 只认与当前职业匹配的；拥有多把取月份最大的（最新的那把）。没有返回 null。
    /// </summary>
    public static EquipTemplate OwnedTemplateForJob(SaveData data, PlayerJobId job)
    {
        var pool = data?.legacyEquipPool;
        if (pool == null || pool.Count == 0) return null;
        Def? best = null;
        var owned = new HashSet<string>();
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e == null || string.IsNullOrEmpty(e.equipId)) continue;
            owned.Add(e.equipId);
        }
        for (int i = 0; i < Roster.Length; i++)
        {
            var d = Roster[i];
            if (d.job != job || !owned.Contains(d.id)) continue;
            if (!best.HasValue || d.month > best.Value.month) best = d;
        }
        if (!best.HasValue) return null;

        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(best.Value.id) : null;
        if (tpl == null)
            Debug.LogError($"[ArenaWeapons] 仓库里有冠军武器 {best.Value.id} 但模板未注册（ConfigManager 未就绪？）");
        return tpl;
    }

    /// <summary>
    /// 生成出战用的传奇品质实例（照 PlayerJobDefs.GrantAndEquipWeapon 的钳制口径 + 传奇待遇）：
    /// 表格 roll 传奇主攻击 + 词缀 + 模板 globalBonus/被动；requireLevel 钳到当前等级必能穿上。
    /// </summary>
    public static EquipInstance CreateBattleInstance(EquipTemplate tpl, WeaponCombatTable.WeaponKind jobKind, int heroLevel)
    {
        if (tpl == null) return null;
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, Mathf.Max(1, heroLevel), true, Rarity.Legendary);
        inst.requireLevel = Mathf.Min(inst.requireLevel, Mathf.Max(1, heroLevel));
        if (inst.weaponHand == WeaponHandSlot.None) inst.weaponHand = WeaponHandSlot.MainHand;
        inst.slotType = EquipSlotType.MainHand;
        inst.weaponKindOverride = (int)jobKind;   // 与职业起步武器同口径：写死逻辑类型
        if (jobKind == WeaponCombatTable.WeaponKind.Staff)
            inst.weaponAttackType = WeaponAttackType.Magic;
        int unlocked = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        if (inst.gridHeight > unlocked) inst.gridHeight = Mathf.Max(1, unlocked);
        if (inst.gridWidth > GameConfig.BACKPACK_WIDTH) inst.gridWidth = GameConfig.BACKPACK_WIDTH;
        return inst;
    }
}
