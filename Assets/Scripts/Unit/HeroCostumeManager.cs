using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 英雄换装管理器：监听装备变化→自动更新玩家SPUM形象
///
/// 核心对应关系（装备槽 → SPUM SpriteList属性 → SPUM资源文件夹）：
///   头(Head)   → _hairList  → 4_Helmet / 0_Hair / 2_Hair
///   胸(Chest)  → _armorList → 5_Armor
///   手(Hands)  → _clothList → 2_Cloth
///   脚(Feet)   → _pantList  → 3_Pant
///   披风(Cape) → _backList  → 7_Back / 10_Back
///   武器(Weapons) → _weaponList → 6_Weapons/{子文件夹}
///   wanjia：ArmR=主手/攻击(R_Weapon)，ArmL=副手(L_Weapon)，盾仅 L_Shield
///
/// 效率优化：启动时扫描一次全部SPUM资源，建立 spumName→路径 缓存字典
/// 之后换装直接查字典，避免每次 Resources.LoadAll 遍历
/// </summary>
public class HeroCostumeManager : MonoBehaviour
{
    public static HeroCostumeManager Instance;

    [Header("SPUM组件引用")]
    public SPUM_Prefabs spumPrefabs;
    public SPUM_SpriteList spriteList;

    // SPUM Resources 基础路径（相对于 Assets/SPUM/Resources/）
    private const string SPUM_SPRITE_BASE = "Addons/{0}/0_Unit/0_Sprite";

    /// <summary>
    /// 三个Addon版本（按优先级搜索：Legacy资源最多，优先）
    /// </summary>
    private static readonly string[] AddonVersions = { "Legacy", "Ver121", "Ver300" };

    /// <summary>
    /// 槽位 → SPUM SpriteList属性名（用于GetPartList获取SpriteRenderer列表）
    /// 注意：Head用"Hair"因为SPUM没有_helmetList，头盔通过hairList显示
    /// </summary>
    private static readonly Dictionary<EquipSlotType, string> SlotToPartType = new Dictionary<EquipSlotType, string>
    {
        { EquipSlotType.Head, "Hair" },
        { EquipSlotType.Chest, "Armor" },
        { EquipSlotType.Hands, "Cloth" },
        { EquipSlotType.Feet, "Pant" },
        { EquipSlotType.Cape, "Back" },
        { EquipSlotType.MainHand, "Weapons" },
        { EquipSlotType.OffHand, "Weapons" }
    };

    /// <summary>
    /// 槽位 → 所有可能的SPUM资源文件夹名称（不同版本文件夹名可能不同）
    /// Head: Legacy/Ver121用4_Helmet和0_Hair，Ver300用2_Hair
    /// Cape: Legacy用7_Back，Ver300用10_Back
    /// </summary>
    private static readonly Dictionary<EquipSlotType, string[]> SlotToAllFolders = new Dictionary<EquipSlotType, string[]>
    {
        { EquipSlotType.Head,      new[] { "4_Helmet", "0_Hair", "2_Hair" } },
        { EquipSlotType.Chest,     new[] { "5_Armor" } },
        { EquipSlotType.Hands,     new[] { "2_Cloth" } },
        { EquipSlotType.Feet,      new[] { "3_Pant" } },
        { EquipSlotType.Cape,      new[] { "7_Back", "10_Back" } },
        { EquipSlotType.MainHand,  new[] { "6_Weapons" } },
        { EquipSlotType.OffHand,   new[] { "6_Weapons" } }
    };

    /// <summary>
    /// 所有可能的武器子文件夹（Legacy和Ver121的子文件夹不同）
    /// Legacy: 0_Sword, 1_Axe, 2_Bow, 3_Shield, 4_Spear, 5_Wand, 6_Hammer
    /// Ver121: 2_Axe, 7_Shield
    /// </summary>
    private static readonly string[] AllWeaponSubFolders =
    {
        "0_Sword", "1_Axe", "2_Bow", "3_Shield", "4_Spear", "5_Wand", "6_Hammer",
        "2_Axe", "7_Shield"
    };

    /// <summary>
    /// 多切片sprite的子精灵名称（Cloth/Armor可能包含Body/Left/Right三个切片）
    /// </summary>
    private static readonly string[] MultiSliceNames = { "Body", "Left", "Right" };

    /// <summary>
    /// 缓存字典：spumName → 完整Resources路径
    /// 启动时扫描一次，之后换装直接查字典，O(1)复杂度
    /// </summary>
    private Dictionary<string, string> _spumPathCache = new Dictionary<string, string>();

    /// <summary>
    /// 缓存字典：路径 → 已加载的Sprite数组（避免重复Resources.LoadAll）
    /// </summary>
    private Dictionary<string, Sprite[]> _spriteCache = new Dictionary<string, Sprite[]>();

    /// <summary>
    /// 缓存是否已初始化
    /// </summary>
    private bool _cacheInitialized = false;
    private bool _rigReady;
    private SPUM_Prefabs _spum;
    private SPUM_MatchingList[] _matchingLists;
    private HeroWeaponRig.HandRig _handRig;
    SpriteRenderer _attackWeaponSr;
    SpriteRenderer _secondaryWeaponSr;
    string _equippedAttackSpum;
    string _equippedSecondarySpum;
    readonly List<SpriteRenderer> _embeddedWeaponSrs = new List<SpriteRenderer>(4);

    struct WeaponSpriteBinding
    {
        public SpriteRenderer Renderer;
        public Sprite Expected;
        public string Dir;
    }

    readonly List<WeaponSpriteBinding> _weaponSpriteBindings = new List<WeaponSpriteBinding>(12);

    /// <summary>普攻武器应装备的槽位（由 SPUM 预制体攻击手检测）。</summary>
    public EquipSlotType AttackWeaponSlot
    {
        get
        {
            EnsureRig();
            return _handRig.AttackSlot;
        }
    }

    public HeroWeaponRig.HandRig HandRig
    {
        get
        {
            EnsureRig();
            return _handRig;
        }
    }

    void Awake()
    {
        Instance = this;

        if (spumPrefabs == null)
            spumPrefabs = GetComponent<SPUM_Prefabs>();

        if (spriteList == null)
            spriteList = GetComponent<SPUM_SpriteList>();

        if (spriteList == null)
            spriteList = GetComponentInChildren<SPUM_SpriteList>();
    }

    void OnEnable()
    {
        EnsureSubscribed();
        if (_rigReady)
        {
            StripPrefabDefaultWeaponVisuals();
            RefreshWeaponLoadout(_matchingLists != null && _matchingLists.Length > 0);
        }
    }

    void Start()
    {
        BuildPathCache();
        EnsureSubscribed();
        EnsureRig();
        StripPrefabDefaultWeaponVisuals();
        ClearAllWeaponVisuals();
        RefreshCostume();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    bool _subscribed;

    /// <summary>供 GridBackpack 等在穿戴前调用，避免 HandRig 未就绪时主手武器错挂到副手 SPUM 方向。</summary>
    public void EnsureRigReady()
    {
        EnsureRig();
    }

    void EnsureRig()
    {
        if (_rigReady) return;
        if (spumPrefabs == null)
            spumPrefabs = GetComponent<SPUM_Prefabs>();
        if (spumPrefabs == null)
            spumPrefabs = GetComponentInChildren<SPUM_Prefabs>(true);
        _spum = spumPrefabs;

        if (spriteList == null)
            spriteList = GetComponent<SPUM_SpriteList>();
        if (spriteList == null)
            spriteList = GetComponentInChildren<SPUM_SpriteList>(true);

        _matchingLists = GetComponentsInChildren<SPUM_MatchingList>(true);
        _handRig = HeroWeaponRig.Build(_spum, _matchingLists);
        CacheWeaponRenderers();
        CacheEmbeddedWeaponRenderers();
        StripPrefabDefaultWeaponVisuals();
        _rigReady = true;
        GridBackpackSystem.Instance?.RemapWeaponWearSlots(_handRig);
        Debug.Log($"[HeroCostumeManager] Rig就绪 attack={_handRig.AttackDir}/{_handRig.AttackSlot} secondary={_handRig.SecondaryDir}/{_handRig.SecondarySlot} matchingLists={_matchingLists?.Length ?? 0}");
    }

    void EnsureSubscribed()
    {
        if (_subscribed) return;
        if (GridBackpackSystem.Instance == null) return;
        GridBackpackSystem.Instance.OnCostumeChanged += RefreshCostume;
        _subscribed = true;
    }

    /// <summary>把 spumName 解析成 Resources 相对路径（无扩展名）。</summary>
    public bool TryResolveSpumPath(string spumName, out string resourcePath)
    {
        resourcePath = null;
        if (string.IsNullOrEmpty(spumName)) return false;
        if (!_cacheInitialized) BuildPathCache();
        if (_spumPathCache.TryGetValue(spumName, out resourcePath))
            return true;
        return false;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.OnCostumeChanged -= RefreshCostume;
        _subscribed = false;
    }

    /// <summary>
    /// 启动时扫描全部SPUM资源，建立 spumName→路径 缓存字典
    /// 只执行一次，之后所有换装都查字典
    /// </summary>
    private void BuildPathCache()
    {
        if (_cacheInitialized) return;
        _cacheInitialized = true;

        Debug.Log("[HeroCostumeManager] 开始扫描SPUM资源，建立路径缓存...");

        int count = 0;

        foreach (string version in AddonVersions)
        {
            string basePath = string.Format(SPUM_SPRITE_BASE, version);

            foreach (var kvp in SlotToAllFolders)
            {
                EquipSlotType slot = kvp.Key;
                string[] folders = kvp.Value;
                bool isWeapon = (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand);

                foreach (string folder in folders)
                {
                    if (isWeapon)
                    {
                        // 武器：遍历所有子文件夹
                        foreach (string subFolder in AllWeaponSubFolders)
                        {
                            string path = $"{basePath}/{folder}/{subFolder}";
                            count += ScanAndCacheDirectory(path, subFolder);
                        }
                    }
                    else
                    {
                        // 非武器：直接扫描文件夹
                        count += ScanAndCacheDirectory($"{basePath}/{folder}", null);
                    }
                }
            }
        }

        Debug.Log($"[HeroCostumeManager] 路径缓存建立完成，共缓存 {count} 个SPUM精灵图");
    }

    /// <summary>
    /// 扫描单个Resources目录，将文件名→路径存入缓存
    /// 使用 Resources.LoadAll 加载目录下所有Sprite
    /// </summary>
    private int ScanAndCacheDirectory(string resourcePath, string context)
    {
        // 加载该目录下所有Sprite
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0) return 0;

        int count = 0;
        // 收集该目录下所有不重复的文件名（去掉切片后缀）
        HashSet<string> fileNames = new HashSet<string>();

        foreach (Sprite sp in sprites)
        {
            // sprite.name 可能是 "Helmet_1" 或 "Body"/"Left"/"Right"（多切片）
            // 多切片的父文件名需要从sprite名推导
            string fileName = sp.name;

            // 多切片的子精灵（Body/Left/Right）跳过，它们属于父文件
            if (System.Array.IndexOf(MultiSliceNames, fileName) >= 0) continue;

            if (!fileNames.Contains(fileName))
            {
                fileNames.Add(fileName);
                // 文件名 → 完整Resources路径（不含扩展名）
                string fullPath = $"{resourcePath}/{fileName}";
                if (!_spumPathCache.ContainsKey(fileName))
                {
                    _spumPathCache[fileName] = fullPath;
                    count++;
                }
            }
        }

        // 缓存该目录的Sprite数组（供后续直接使用）
        if (!_spriteCache.ContainsKey(resourcePath))
        {
            _spriteCache[resourcePath] = sprites;
        }

        return count;
    }

    public void RefreshCostume()
    {
        EnsureRig();
        if (GridBackpackSystem.Instance == null) return;
        if (!_cacheInitialized) BuildPathCache();

        bool useMatching = _matchingLists != null && _matchingLists.Length > 0;
        if (!useMatching && spriteList == null)
        {
            Debug.LogWarning("[HeroCostumeManager] 无 SPUM_MatchingList / SpriteList，跳过换装");
            return;
        }

        GridBackpackSystem.Instance.RemapWeaponWearSlots(_handRig);

        EquipSlotType[] order =
        {
            EquipSlotType.Head, EquipSlotType.Chest, EquipSlotType.Hands,
            EquipSlotType.Feet, EquipSlotType.Cape
        };

        for (int i = 0; i < order.Length; i++)
        {
            EquipSlotType slot = order[i];
            EquipInstance equip = GridBackpackSystem.Instance.GetEquippedInSlot(slot);
            string spumName = equip?.template != null ? equip.template.spumName : null;

            if (string.IsNullOrEmpty(spumName))
            {
                if (useMatching) ClearEmptyArmorSlot(slot);
                continue;
            }

            Rarity rarity = equip != null ? equip.rarity : Rarity.Common;
            if (useMatching)
                ApplyArmorViaMatching(slot, spumName, rarity);
            else if (SlotToPartType.TryGetValue(slot, out string partType))
                ApplySpriteToPart(partType, slot, spumName, rarity);
        }

        RefreshWeaponLoadout(useMatching);

        var ua = GetComponent<UnitAnimation>() ?? GetComponentInParent<UnitAnimation>();
        ua?.RestoreSpumFlashColors();

        Debug.Log("[HeroCostumeManager] 换装刷新完成");
    }

    /// <summary>攻击动画期间 SPUM 可能改 ItemPath/贴图：只比对缓存挂点，不做全量换装。</summary>
    public void ReapplyWeaponVisuals()
    {
        if (!_rigReady) return;
        RestoreEquippedWeaponItemPaths();
        for (int i = 0; i < _weaponSpriteBindings.Count; i++)
        {
            var b = _weaponSpriteBindings[i];
            FixWeaponRendererIfStale(b.Renderer, b.Expected, b.Dir);
        }
    }

    void RestoreEquippedWeaponItemPaths()
    {
        RestoreWeaponItemPathForDir(_handRig.AttackDir, _equippedAttackSpum);
        RestoreWeaponItemPathForDir(_handRig.SecondaryDir, _equippedSecondarySpum);
    }

    void RestoreWeaponItemPathForDir(string dir, string spumName)
    {
        if (string.IsNullOrEmpty(spumName))
        {
            ClearWeaponItemPathsForDir(dir);
            SyncImageElementWeapon(dir, null);
            return;
        }

        if (!TryResolveSpumPath(spumName, out string path)) return;
        SyncImageElementWeapon(dir, spumName);
        SetPrimaryMatchingItemPath(dir, path);
    }

    void SetPrimaryMatchingItemPath(string dir, string path)
    {
        if (_matchingLists == null) return;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                if (!HeroWeaponRig.IsPrimaryWeaponRenderer(me.renderer)) continue;
                me.ItemPath = path;
            }
        }
    }

    void BuildWeaponSpriteBindings()
    {
        _weaponSpriteBindings.Clear();
        AddWeaponSpriteBinding(_attackWeaponSr, _handRig.AttackDir, _attackWeaponSr != null ? _attackWeaponSr.sprite : null);
        AddWeaponSpriteBinding(_secondaryWeaponSr, _handRig.SecondaryDir, _secondaryWeaponSr != null ? _secondaryWeaponSr.sprite : null);
        if (TryGetShieldRenderer(_handRig.AttackDir, out var attackShield))
            AddWeaponSpriteBinding(attackShield, _handRig.AttackDir, attackShield.sprite);
        if (TryGetShieldRenderer(_handRig.SecondaryDir, out var secondaryShield))
            AddWeaponSpriteBinding(secondaryShield, _handRig.SecondaryDir, secondaryShield.sprite);
    }

    bool TryGetShieldRenderer(string dir, out SpriteRenderer renderer)
    {
        renderer = null;
        if (_matchingLists == null) return false;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me?.renderer == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                if (!HeroWeaponRig.IsShieldRenderer(me.renderer)) continue;
                renderer = me.renderer;
                return true;
            }
        }
        return false;
    }

    void AddWeaponSpriteBinding(SpriteRenderer sr, string dir, Sprite expected)
    {
        if (sr == null) return;
        for (int i = 0; i < _weaponSpriteBindings.Count; i++)
        {
            if (_weaponSpriteBindings[i].Renderer == sr)
                return;
        }
        _weaponSpriteBindings.Add(new WeaponSpriteBinding
        {
            Renderer = sr,
            Expected = expected,
            Dir = dir
        });
    }

    void CacheWeaponRenderers()
    {
        _attackWeaponSr = null;
        _secondaryWeaponSr = null;
        if (_matchingLists == null) return;
        HeroWeaponRig.TryGetPrimaryWeaponRenderer(_matchingLists, _handRig.AttackDir, out _attackWeaponSr);
        HeroWeaponRig.TryGetPrimaryWeaponRenderer(_matchingLists, _handRig.SecondaryDir, out _secondaryWeaponSr);
    }

    void CacheEmbeddedWeaponRenderers()
    {
        _embeddedWeaponSrs.Clear();
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || sr.gameObject == null) continue;
            string n = sr.gameObject.name;
            if (n.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0)
                _embeddedWeaponSrs.Add(sr);
        }
    }

    static void FixWeaponRendererIfStale(SpriteRenderer sr, Sprite expected, string dir, in HeroWeaponRig.HandRig rig)
    {
        if (sr == null) return;
        if (expected == null)
        {
            if (sr.sprite != null) sr.sprite = null;
            return;
        }
        if (sr.sprite == expected) return;
        sr.sprite = expected;
        HeroWeaponRig.ApplyWeaponPresentation(sr, dir, rig);
    }

    void FixWeaponRendererIfStale(SpriteRenderer sr, Sprite expected, string dir)
        => FixWeaponRendererIfStale(sr, expected, dir, _handRig);

    /// <summary>
    /// 武器按 HandRig 攻击手/副手刷新：先清掉预制体默认武器，再只挂当前装备（避免同手叠两把剑）。
    /// </summary>
    /// <summary>清掉 wanjia 预制体 ImageElement / Matching 里残留的默认武器路径与贴图（编辑器去手武器后仍可能留 ItemPath）。</summary>
    void StripPrefabDefaultWeaponVisuals()
    {
        if (_spum?.ImageElement != null)
        {
            for (int i = 0; i < _spum.ImageElement.Count; i++)
            {
                var ie = _spum.ImageElement[i];
                if (ie == null || ie.PartType != "Weapons") continue;
                ie.ItemPath = "";
            }
        }

        if (_matchingLists != null)
        {
            for (int m = 0; m < _matchingLists.Length; m++)
            {
                var tables = _matchingLists[m]?.matchingTables;
                if (tables == null) continue;
                for (int t = 0; t < tables.Count; t++)
                {
                    var me = tables[t];
                    if (me == null || me.PartType != "Weapons") continue;
                    me.ItemPath = "";
                    if (me.renderer != null)
                    {
                        me.renderer.sprite = null;
                        HeroWeaponRig.ApplyWeaponPresentation(me.renderer, me.Dir, _handRig);
                    }
                }
            }
        }

        StripEmbeddedWeaponSprites();
    }

    /// <summary>清掉预制体上 L_Weapon / R_Weapon / Shield 节点残留的默认贴图（Matching 表外也可能有）。</summary>
    void StripEmbeddedWeaponSprites()
    {
        for (int i = 0; i < _embeddedWeaponSrs.Count; i++)
        {
            var sr = _embeddedWeaponSrs[i];
            if (sr != null) sr.sprite = null;
        }
    }

    void RefreshWeaponLoadout(bool useMatching)
    {
        GridBackpackSystem.Instance.RemapWeaponWearSlots(_handRig);

        EquipInstance attackEquip = _handRig.IsValid
            ? GridBackpackSystem.Instance.GetEquippedInSlot(_handRig.AttackSlot)
            : GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        EquipInstance secondaryEquip = _handRig.IsValid
            ? GridBackpackSystem.Instance.GetEquippedInSlot(_handRig.SecondarySlot)
            : GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        bool twoHandEquipped = attackEquip != null && attackEquip.weaponType == WeaponType.TwoHand;
        if (twoHandEquipped)
            secondaryEquip = null;

        string attackSpum = attackEquip?.template != null ? attackEquip.template.spumName : null;
        string secondarySpum = secondaryEquip?.template == null
            ? null
            : secondaryEquip.template.spumName;
        if (!string.IsNullOrEmpty(attackSpum) && attackSpum == secondarySpum)
            secondarySpum = null;
        _equippedAttackSpum = attackSpum;
        _equippedSecondarySpum = secondarySpum;

        Rarity attackRarity = attackEquip != null ? attackEquip.rarity : Rarity.Common;
        Rarity secondaryRarity = secondaryEquip != null ? secondaryEquip.rarity : Rarity.Common;

        ClearAllWeaponVisuals();
        if (useMatching)
        {
            ApplyWeaponToMatchingDir(_handRig.AttackDir, attackSpum, attackRarity);
            ApplyWeaponToMatchingDir(_handRig.SecondaryDir, secondarySpum, secondaryRarity);
        }
        else
        {
            ApplyWeaponSpritesToDir(_handRig.AttackDir, attackSpum, attackRarity);
            ApplyWeaponSpritesToDir(_handRig.SecondaryDir, secondarySpum, secondaryRarity);
        }

        CacheWeaponRenderers();
        BuildWeaponSpriteBindings();
    }

    void ApplyWeaponToMatchingDir(string dir, string spumName, Rarity rarity)
    {
        ClearWeaponItemPathsForDir(dir);
        SyncImageElementWeapon(dir, spumName);
        if (string.IsNullOrEmpty(spumName)) return;

        if (!TryResolveSpumPath(spumName, out string path))
        {
            Debug.LogWarning($"[HeroCostumeManager] 武器路径未缓存: {spumName}");
            return;
        }

        Sprite sprite = LoadSpriteFromResourcePath(path, spumName);
        bool isShield = HeroWeaponRig.IsShieldSpumName(spumName);
        string partSubType = ResolveWeaponPartSubType(spumName, isShield);
        ApplyMatchingWeaponDir(dir, path, sprite, partSubType, isShield, rarity);
        Debug.Log($"[HeroCostumeManager] Matching武器 dir={dir} sub={partSubType} ← {spumName}");
    }

    void ApplyArmorViaMatching(EquipSlotType slot, string spumName, Rarity rarity)
    {
        string partType = ResolveMatchingPartType(slot, spumName);
        if (string.IsNullOrEmpty(partType)) return;
        if (!TryResolveSpumPath(spumName, out string path)) return;
        Sprite[] slices = Resources.LoadAll<Sprite>(path);
        if (slices == null || slices.Length == 0) return;

        if (_spum != null && _spum.ImageElement != null)
        {
            for (int i = 0; i < _spum.ImageElement.Count; i++)
            {
                var ie = _spum.ImageElement[i];
                if (ie == null || ie.PartType != partType) continue;
                ie.ItemPath = path;
            }
        }

        if (_matchingLists == null) return;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != partType || me.renderer == null) continue;
                Sprite slice = PickSlice(slices, spumName, partType, me.Structure, me.Dir);
                if (slice == null) continue;
                me.ItemPath = path;
                me.renderer.sprite = slice;
                EquipRarityMaterials.Apply(me.renderer, rarity);
            }
        }
    }

    void ClearEmptyArmorSlot(EquipSlotType slot)
    {
        // 只清预制体默认就是空的部位，避免把默认头发/衣服清成裸模
        if (slot == EquipSlotType.Chest) ClearMatchingPartSprites("Armor");
        else if (slot == EquipSlotType.Cape) ClearMatchingPartSprites("Back");
        else if (slot == EquipSlotType.Head) ClearMatchingPartSprites("Helmet");
    }

    void ClearMatchingPartSprites(string partType)
    {
        if (_matchingLists == null || string.IsNullOrEmpty(partType)) return;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != partType) continue;
                me.ItemPath = "";
                if (me.renderer == null) continue;
                me.renderer.sprite = null;
                EquipRarityMaterials.Apply(me.renderer, Rarity.Common);
            }
        }
    }

    string ResolveMatchingPartType(EquipSlotType slot, string spumName)
    {
        if (slot == EquipSlotType.Head)
            return IsHelmetAsset(spumName) ? "Helmet" : "Hair";
        return SlotToPartType.TryGetValue(slot, out string partType) ? partType : null;
    }

    bool IsHelmetAsset(string spumName)
    {
        if (TryResolveSpumPath(spumName, out string path) && path != null
            && path.IndexOf("4_Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (string.IsNullOrEmpty(spumName)) return false;
        return spumName.IndexOf("Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void SyncImageElementWeapon(string dir, string spumName)
    {
        if (_spum == null || _spum.ImageElement == null) return;
        string path = null;
        if (!string.IsNullOrEmpty(spumName))
            TryResolveSpumPath(spumName, out path);

        for (int i = 0; i < _spum.ImageElement.Count; i++)
        {
            var ie = _spum.ImageElement[i];
            if (ie == null || ie.PartType != "Weapons" || ie.Dir != dir) continue;
            ie.ItemPath = path ?? "";
        }
    }

    void ApplyMatchingWeaponDir(string dir, string path, Sprite sprite, string partSubType, bool isShield, Rarity rarity)
    {
        if (_matchingLists == null) return;
        SpriteRenderer target = null;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                if (!ShouldTouchWeaponMatchingEntry(me, isShield)) continue;

                me.ItemPath = path;
                if (me.renderer == null) continue;
                if (target == null)
                {
                    me.renderer.sprite = sprite;
                    HeroWeaponRig.ApplyWeaponPresentation(me.renderer, dir, _handRig);
                    EquipRarityMaterials.Apply(me.renderer, rarity);
                    target = me.renderer;
                }
                else if (me.renderer != target)
                {
                    me.renderer.sprite = null;
                    EquipRarityMaterials.Apply(me.renderer, Rarity.Common);
                }
            }
        }

        if (target == null)
            TryApplyPrimaryWeaponRenderer(dir, sprite, rarity);
    }

    void ClearWeaponItemPathsForDir(string dir)
    {
        if (_spum?.ImageElement != null)
        {
            for (int i = 0; i < _spum.ImageElement.Count; i++)
            {
                var ie = _spum.ImageElement[i];
                if (ie == null || ie.PartType != "Weapons" || ie.Dir != dir) continue;
                ie.ItemPath = "";
            }
        }

        if (_matchingLists == null) return;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                me.ItemPath = "";
            }
        }
    }

    void ClearMatchingWeaponDir(string dir)
    {
        ClearWeaponItemPathsForDir(dir);
        SyncImageElementWeapon(dir, null);
        if (_matchingLists == null) return;
        for (int m = 0; m < _matchingLists.Length; m++)
        {
            var tables = _matchingLists[m]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                me.ItemPath = "";
                if (me.renderer == null) continue;
                me.renderer.sprite = null;
                HeroWeaponRig.ApplyWeaponPresentation(me.renderer, dir, _handRig);
                EquipRarityMaterials.Apply(me.renderer, Rarity.Common);
            }
        }
        TryApplyPrimaryWeaponRenderer(dir, null, Rarity.Common);
    }

    void ClearAllWeaponVisuals()
    {
        if (!_rigReady) EnsureRig();
        ClearMatchingWeaponDir(_handRig.AttackDir);
        ClearMatchingWeaponDir(_handRig.SecondaryDir);
        StripEmbeddedWeaponSprites();
        if (spriteList?._weaponList == null) return;
        for (int i = 0; i < spriteList._weaponList.Count; i++)
        {
            var sr = spriteList._weaponList[i];
            if (sr != null) sr.sprite = null;
        }
    }

    static bool ShouldTouchWeaponMatchingEntry(MatchingElement me, bool isShield)
    {
        if (me == null) return false;
        if (isShield) return HeroWeaponRig.IsShieldRenderer(me.renderer);
        if (HeroWeaponRig.IsShieldRenderer(me.renderer)) return false;
        return HeroWeaponRig.IsPrimaryWeaponRenderer(me.renderer);
    }

    void TryApplyPrimaryWeaponRenderer(string dir, Sprite sprite, Rarity rarity)
    {
        if (!HeroWeaponRig.TryGetPrimaryWeaponRenderer(_matchingLists, dir, out var sr) || sr == null)
            return;
        sr.sprite = sprite;
        HeroWeaponRig.ApplyWeaponPresentation(sr, dir, _handRig);
        EquipRarityMaterials.Apply(sr, rarity);
    }

    static string ResolveWeaponPartSubType(string spumName, bool isShield)
    {
        if (isShield) return "Shield";
        if (string.IsNullOrEmpty(spumName)) return "Sword";
        string n = spumName.ToLowerInvariant();
        if (n.Contains("bow")) return "Bow";
        if (n.Contains("wand") || n.Contains("ward") || n.Contains("staff")) return "Wand";
        if (n.Contains("spear")) return "Spear";
        if (n.Contains("axe")) return "Axe";
        if (n.Contains("hammer")) return "Hammer";
        if (n.Contains("dagger")) return "Dagger";
        return "Sword";
    }

    static Sprite LoadSpriteFromResourcePath(string resourcePath, string spumName)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0) return null;
        Sprite found = System.Array.Find(sprites, s => s.name == spumName);
        return found != null ? found : sprites[0];
    }

    static Sprite PickSlice(Sprite[] all, string fileName, string partType, string structure, string dir)
    {
        if (all == null || all.Length == 0) return null;

        bool hasBody = ContainsSlice(all, "Body");
        bool hasLeft = ContainsSlice(all, "Left");
        bool hasRight = ContainsSlice(all, "Right");
        bool hasSlices = hasBody || hasLeft || hasRight;

        if (hasSlices)
        {
            if (string.Equals(structure, "Body", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(structure, "Left", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(structure, "Right", System.StringComparison.OrdinalIgnoreCase))
                return FindSlice(all, structure);
            return FindSlice(all, fileName) ?? all[0];
        }

        Sprite single = FindSlice(all, fileName) ?? all[0];
        if (string.Equals(partType, "Helmet", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(dir, "Back", System.StringComparison.OrdinalIgnoreCase))
                return FindSlice(all, "Back");
            return single;
        }

        if (string.Equals(structure, "Left", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(structure, "Right", System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(partType, "Pant", System.StringComparison.OrdinalIgnoreCase))
                return single;
            return null;
        }

        return single;
    }

    static bool ContainsSlice(Sprite[] all, string name)
    {
        return FindSlice(all, name) != null;
    }

    static Sprite FindSlice(Sprite[] all, string name)
    {
        if (all == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name) return all[i];
        }
        return null;
    }

    /// <summary>
    /// 加载SPUM精灵图并应用到指定部件
    /// 自动处理单切片和多切片（Body/Left/Right）情况
    /// 使用缓存，避免重复 Resources.LoadAll
    /// </summary>
    private void ApplySpriteToPart(string partType, EquipSlotType slot, string spumName, Rarity rarity)
    {
        if (string.IsNullOrEmpty(spumName)) return;

        List<SpriteRenderer> targetList = GetPartList(partType);
        if (targetList == null || targetList.Count == 0)
        {
            Debug.LogWarning($"[HeroCostumeManager] {partType} 的 SpriteRenderer 列表为空");
            return;
        }

        // 从缓存获取Sprite数组
        Sprite[] sprites = GetCachedSprites(spumName);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"[HeroCostumeManager] 未找到精灵: slot={slot}, spumName={spumName}");
            return;
        }

        // 主手/副手共用 _weaponList：只改对应侧，禁止整表覆盖把另一只手冲掉
        if (partType == "Weapons")
        {
            ApplyWeaponSprites(slot, sprites, spumName, rarity);
            return;
        }

        if (sprites.Length == 1)
        {
            // 单切片：设置列表中所有SpriteRenderer
            foreach (var sr in targetList)
            {
                if (sr != null)
                {
                    sr.sprite = sprites[0];
                    EquipRarityMaterials.Apply(sr, rarity);
                }
            }
        }
        else
        {
            // 多切片：按名称匹配 Body/Left/Right → 对应索引 0/1/2
            for (int i = 0; i < targetList.Count && i < MultiSliceNames.Length; i++)
            {
                if (targetList[i] == null) continue;
                Sprite sub = System.Array.Find(sprites, s => s.name == MultiSliceNames[i]);
                if (sub != null)
                {
                    targetList[i].sprite = sub;
                    EquipRarityMaterials.Apply(targetList[i], rarity);
                }
            }
        }

        // 更新路径字符串，防止ResyncData覆盖
        UpdatePathString(partType, spumName);

        Debug.Log($"[HeroCostumeManager] 换装成功: {partType} ← {spumName} ({sprites.Length}切片)");
    }

    void ApplyWeaponSprites(EquipSlotType slot, Sprite[] sprites, string spumName, Rarity rarity)
    {
        ApplyWeaponSpritesToDir(HeroWeaponRig.DirForSlot(slot, _handRig), sprites, spumName, rarity);
    }

    void ApplyWeaponSpritesToDir(string spumDir, string spumName, Rarity rarity)
    {
        if (string.IsNullOrEmpty(spumName)) return;
        Sprite[] sprites = GetCachedSprites(spumName);
        if (sprites == null || sprites.Length == 0) return;
        ApplyWeaponSpritesToDir(spumDir, sprites, spumName, rarity);
    }

    void ApplyWeaponSpritesToDir(string spumDir, Sprite[] sprites, string spumName, Rarity rarity)
    {
        if (spriteList == null || sprites == null || sprites.Length == 0) return;
        var list = spriteList._weaponList;
        if (list == null || list.Count == 0) return;

        Sprite pick = sprites[0];
        bool wantRight = spumDir == HeroWeaponRig.DirRight;
        bool applied = false;
        for (int i = 0; i < list.Count; i++)
        {
            var sr = list[i];
            if (sr == null || sr.gameObject == null) continue;
            string n = sr.gameObject.name;
            bool isPrimary = wantRight
                ? n.IndexOf("R_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
                : n.IndexOf("L_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isPrimary) continue;
            if (applied)
            {
                sr.sprite = null;
                EquipRarityMaterials.Apply(sr, Rarity.Common);
                continue;
            }
            sr.sprite = pick;
            EquipRarityMaterials.Apply(sr, rarity);
            HeroWeaponRig.ApplyWeaponPresentation(sr, spumDir, _handRig);
            applied = true;
        }

        UpdatePathString("Weapons", spumName);
        Debug.Log($"[HeroCostumeManager] 武器换装: dir={spumDir} ← {spumName} applied={applied}");
    }

    void ClearWeaponDir(string spumDir)
    {
        if (spriteList == null || spriteList._weaponList == null) return;
        bool wantRight = spumDir == HeroWeaponRig.DirRight;
        var list = spriteList._weaponList;
        for (int i = 0; i < list.Count; i++)
        {
            var sr = list[i];
            if (sr == null) continue;
            string n = sr.gameObject != null ? sr.gameObject.name : sr.name;
            bool isRight = n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || n.IndexOf("R_", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLeft = n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || n.IndexOf("L_", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isRight && !isLeft)
            {
                if ((wantRight && i == 0) || (!wantRight && i == list.Count - 1))
                {
                    sr.sprite = null;
                    EquipRarityMaterials.Apply(sr, Rarity.Common);
                }
                continue;
            }
            if (wantRight && isRight) { sr.sprite = null; EquipRarityMaterials.Apply(sr, Rarity.Common); HeroWeaponRig.ApplyWeaponPresentation(sr, spumDir, _handRig); }
            if (!wantRight && isLeft) { sr.sprite = null; EquipRarityMaterials.Apply(sr, Rarity.Common); HeroWeaponRig.ApplyWeaponPresentation(sr, spumDir, _handRig); }
        }
    }

    void ClearWeaponSide(EquipSlotType slot)
    {
        ClearWeaponDir(HeroWeaponRig.DirForSlot(slot, _handRig));
    }

    /// <summary>
    /// 从缓存获取Sprite数组
    /// 1. 先查 _spumPathCache 获取路径（O(1)）
    /// 2. 再查 _spriteCache 获取已加载的Sprite（O(1)）
    /// 3. 若sprite未缓存，按路径加载一次并存入缓存
    /// </summary>
    private Sprite[] GetCachedSprites(string spumName)
    {
        // 1. 查路径缓存
        if (!_spumPathCache.TryGetValue(spumName, out string fullPath))
        {
            // 缓存未命中：可能扫描时遗漏，尝试直接加载
            return FallbackLoad(spumName);
        }

        // 2. 查sprite缓存（按目录路径）
        // fullPath 格式: "Addons/Legacy/0_Unit/0_Sprite/4_Helmet/Helmet_1"
        // 目录路径 = 去掉最后的文件名
        int lastSlash = fullPath.LastIndexOf('/');
        if (lastSlash <= 0) return FallbackLoad(spumName);

        string dirPath = fullPath.Substring(0, lastSlash);

        // 3. 目录的Sprite数组已缓存就直接用
        if (_spriteCache.TryGetValue(dirPath, out Sprite[] cached))
        {
            // 从目录的所有Sprite中筛选属于该文件的切片
            return FilterSpritesByFile(cached, spumName);
        }

        // 4. 目录未缓存：加载一次
        Sprite[] sprites = Resources.LoadAll<Sprite>(dirPath);
        if (sprites != null && sprites.Length > 0)
        {
            _spriteCache[dirPath] = sprites;
            return FilterSpritesByFile(sprites, spumName);
        }

        return null;
    }

    /// <summary>
    /// 从目录的所有Sprite中筛选属于指定文件的切片
    /// 单切片：返回文件名相同的Sprite
    /// 多切片：返回 Body/Left/Right 三个切片
    /// </summary>
    private Sprite[] FilterSpritesByFile(Sprite[] allSprites, string fileName)
    {
        if (allSprites == null || allSprites.Length == 0) return null;

        // 先找和文件名完全相同的Sprite（单切片情况）
        foreach (Sprite sp in allSprites)
        {
            if (sp.name == fileName) return new Sprite[] { sp };
        }

        // 没找到同名，说明是多切片，收集 Body/Left/Right
        List<Sprite> result = new List<Sprite>();
        foreach (string sliceName in MultiSliceNames)
        {
            Sprite sub = System.Array.Find(allSprites, s => s.name == sliceName);
            if (sub != null) result.Add(sub);
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    /// <summary>
    /// 缓存未命中时的兜底加载
    /// 直接用spumName作为路径尝试加载
    /// </summary>
    private Sprite[] FallbackLoad(string spumName)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(spumName);
        if (sprites != null && sprites.Length > 0)
        {
            _spumPathCache[spumName] = spumName;
            return sprites;
        }
        return null;
    }

    /// <summary>
    /// 获取部件对应的SpriteRenderer列表
    /// </summary>
    private List<SpriteRenderer> GetPartList(string partType)
    {
        if (spriteList == null) return null;

        switch (partType)
        {
            case "Hair":    return spriteList._hairList;
            case "Cloth":   return spriteList._clothList;
            case "Armor":   return spriteList._armorList;
            case "Pant":    return spriteList._pantList;
            case "Back":    return spriteList._backList;
            case "Weapons": return spriteList._weaponList;
            default: return null;
        }
    }

    /// <summary>
    /// 更新路径字符串列表，防止SPUM的ResyncData用旧路径覆盖换装结果
    /// 直接从缓存字典获取路径
    /// </summary>
    private void UpdatePathString(string partType, string spumName)
    {
        if (spriteList == null) return;

        // 从缓存获取实际路径
        string path;
        if (!_spumPathCache.TryGetValue(spumName, out path))
        {
            return; // 缓存中没有，无法更新
        }

        switch (partType)
        {
            case "Hair":
                SetPathStringList(spriteList._hairListString, path);
                break;
            case "Cloth":
                SetPathStringList(spriteList._clothListString, path);
                break;
            case "Armor":
                SetPathStringList(spriteList._armorListString, path);
                break;
            case "Pant":
                SetPathStringList(spriteList._pantListString, path);
                break;
            case "Back":
                SetPathStringList(spriteList._backListString, path);
                break;
            case "Weapons":
                SetPathStringList(spriteList._weaponListString, path);
                break;
        }
    }

    /// <summary>
    /// 安全设置路径字符串列表的第一个元素
    /// </summary>
    private void SetPathStringList(List<string> list, string path)
    {
        if (list == null) return;
        if (list.Count == 0) list.Add(path);
        else list[0] = path;
    }
}
