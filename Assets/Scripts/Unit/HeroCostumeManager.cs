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
    }

    void Start()
    {
        BuildPathCache();
        EnsureSubscribed();
        // 进场时按当前穿戴刷一次，避免只改了数值没改外观
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
        _rigReady = true;
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

            if (!string.IsNullOrEmpty(spumName))
            {
                if (useMatching)
                    ApplyArmorViaMatching(slot, spumName);
                else if (SlotToPartType.TryGetValue(slot, out string partType))
                    ApplySpriteToPart(partType, slot, spumName);
            }
        }

        RefreshWeaponLoadout(useMatching);

        Debug.Log("[HeroCostumeManager] 换装刷新完成");
    }

    /// <summary>
    /// 武器按 HandRig 攻击手/副手刷新：先清掉预制体默认武器，再只挂当前装备（避免同手叠两把剑）。
    /// </summary>
    void RefreshWeaponLoadout(bool useMatching)
    {
        EquipInstance attackEquip = GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        EquipInstance secondaryEquip = GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        bool twoHandEquipped = attackEquip != null && attackEquip.weaponType == WeaponType.TwoHand;

        string attackSpum = attackEquip?.template != null ? attackEquip.template.spumName : null;
        string secondarySpum = twoHandEquipped || secondaryEquip?.template == null
            ? null
            : secondaryEquip.template.spumName;

        if (useMatching)
        {
            ClearMatchingWeaponDir(_handRig.AttackDir);
            ClearMatchingWeaponDir(_handRig.SecondaryDir);
            ApplyWeaponToMatchingDir(_handRig.AttackDir, attackSpum);
            ApplyWeaponToMatchingDir(_handRig.SecondaryDir, secondarySpum);
        }
        else
        {
            ClearWeaponDir(_handRig.AttackDir);
            ClearWeaponDir(_handRig.SecondaryDir);
            ApplyWeaponSpritesToDir(_handRig.AttackDir, attackSpum);
            ApplyWeaponSpritesToDir(_handRig.SecondaryDir, secondarySpum);
        }
    }

    void ApplyWeaponToMatchingDir(string dir, string spumName)
    {
        SyncImageElementWeapon(dir, spumName);
        if (string.IsNullOrEmpty(spumName)) return;

        if (!TryResolveSpumPath(spumName, out string path))
        {
            Debug.LogWarning($"[HeroCostumeManager] 武器路径未缓存: {spumName}");
            return;
        }

        Sprite sprite = LoadSpriteFromResourcePath(path, spumName);
        ApplyMatchingWeaponDir(dir, path, sprite);
        Debug.Log($"[HeroCostumeManager] Matching武器 dir={dir} ← {spumName}");
    }

    void ApplyArmorViaMatching(EquipSlotType slot, string spumName)
    {
        if (!SlotToPartType.TryGetValue(slot, out string partType)) return;
        if (!TryResolveSpumPath(spumName, out string path)) return;
        Sprite sprite = LoadSpriteFromResourcePath(path, spumName);
        if (sprite == null) return;

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
                if (me == null || me.PartType != partType) continue;
                me.ItemPath = path;
                if (me.renderer != null)
                    me.renderer.sprite = PickSlice(sprite, me.Structure);
            }
        }
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

    void ApplyMatchingWeaponDir(string dir, string path, Sprite sprite)
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
                me.ItemPath = path;
                if (me.renderer != null)
                {
                    me.renderer.sprite = sprite;
                    HeroWeaponRig.ApplyWeaponPresentation(me.renderer, dir, _handRig);
                }
            }
        }
    }

    void ClearMatchingWeaponDir(string dir)
    {
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
                if (me.renderer != null)
                {
                    me.renderer.sprite = null;
                    HeroWeaponRig.ApplyWeaponPresentation(me.renderer, dir, _handRig);
                }
            }
        }
    }

    static Sprite LoadSpriteFromResourcePath(string resourcePath, string spumName)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0) return null;
        Sprite found = System.Array.Find(sprites, s => s.name == spumName);
        return found != null ? found : sprites[0];
    }

    static Sprite PickSlice(Sprite primary, string structure)
    {
        if (primary == null) return null;
        if (string.IsNullOrEmpty(structure)) return primary;
        // 多切片由 LoadSpriteFromResourcePath 已返回单张时直接用
        return primary;
    }

    /// <summary>
    /// 加载SPUM精灵图并应用到指定部件
    /// 自动处理单切片和多切片（Body/Left/Right）情况
    /// 使用缓存，避免重复 Resources.LoadAll
    /// </summary>
    private void ApplySpriteToPart(string partType, EquipSlotType slot, string spumName)
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
            ApplyWeaponSprites(slot, sprites, spumName);
            return;
        }

        if (sprites.Length == 1)
        {
            // 单切片：设置列表中所有SpriteRenderer
            foreach (var sr in targetList)
            {
                if (sr != null) sr.sprite = sprites[0];
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
                }
            }
        }

        // 更新路径字符串，防止ResyncData覆盖
        UpdatePathString(partType, spumName);

        Debug.Log($"[HeroCostumeManager] 换装成功: {partType} ← {spumName} ({sprites.Length}切片)");
    }

    void ApplyWeaponSprites(EquipSlotType slot, Sprite[] sprites, string spumName)
    {
        ApplyWeaponSpritesToDir(HeroWeaponRig.DirForSlot(slot, _handRig), sprites, spumName);
    }

    void ApplyWeaponSpritesToDir(string spumDir, string spumName)
    {
        if (string.IsNullOrEmpty(spumName)) return;
        Sprite[] sprites = GetCachedSprites(spumName);
        if (sprites == null || sprites.Length == 0) return;
        ApplyWeaponSpritesToDir(spumDir, sprites, spumName);
    }

    void ApplyWeaponSpritesToDir(string spumDir, Sprite[] sprites, string spumName)
    {
        if (spriteList == null || sprites == null || sprites.Length == 0) return;
        var list = spriteList._weaponList;
        if (list == null || list.Count == 0) return;

        Sprite pick = sprites[0];
        bool wantRight = spumDir == HeroWeaponRig.DirRight;
        int matched = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var sr = list[i];
            if (sr == null) continue;
            string n = sr.gameObject != null ? sr.gameObject.name : sr.name;
            bool isRight = n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || n.IndexOf("R_", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLeft = n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || n.IndexOf("L_", System.StringComparison.OrdinalIgnoreCase) >= 0;
            // 分不出左右时：主手用列表前半，副手用后半
            if (!isRight && !isLeft)
            {
                if (wantRight && i == 0) { sr.sprite = pick; matched++; }
                if (!wantRight && i == list.Count - 1) { sr.sprite = pick; matched++; }
                continue;
            }
            if (wantRight && isRight) { sr.sprite = pick; matched++; HeroWeaponRig.ApplyWeaponPresentation(sr, spumDir, _handRig); }
            if (!wantRight && isLeft) { sr.sprite = pick; matched++; HeroWeaponRig.ApplyWeaponPresentation(sr, spumDir, _handRig); }
        }

        UpdatePathString("Weapons", spumName);
        Debug.Log($"[HeroCostumeManager] 武器换装: dir={spumDir} ← {spumName} matched={matched}");
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
                    sr.sprite = null;
                continue;
            }
            if (wantRight && isRight) sr.sprite = null;
            if (!wantRight && isLeft) sr.sprite = null;
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
