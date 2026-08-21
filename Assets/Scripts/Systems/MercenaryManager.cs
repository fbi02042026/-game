using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 佣兵管理器：头像查询、预制体加载、出战生成与回收。
/// 生成方式与 Hero 对齐：挂到 unit → 写世界坐标 → Init 朝向；不改大小/排序。
/// </summary>
public class MercenaryManager : Singleton<MercenaryManager>
{
    [Header("角色注册表")]
    public CharacterRegistry registry;

    private List<Mercenary> _activeMercs = new List<Mercenary>();

    public const string PLAYER_ID = "wanjia";

    protected override void Awake()
    {
        base.Awake();
        if (registry == null)
        {
            registry = Resources.Load<CharacterRegistry>("Config/CharacterRegistry");
            if (registry == null)
                Debug.LogWarning("[MercenaryManager] 未找到CharacterRegistry，请运行 Tools/生成角色注册表 菜单生成");
        }
    }

    #region 头像/配置查询

    public Sprite GetIcon(string characterId)
    {
        Sprite sp = registry != null ? registry.GetIcon(characterId) : null;
        if (sp != null) return sp;
        if (string.IsNullOrEmpty(characterId)) return null;
        sp = Resources.Load<Sprite>("UI/Heads/icon_" + characterId);
        if (sp != null) return sp;
        // 101/102 盾兵共用头像
        if (characterId.StartsWith("dunbing"))
            return Resources.Load<Sprite>("UI/Heads/icon_dunbing102");
        return null;
    }

    public string GetJobName(string characterId)
    {
        return registry != null ? registry.GetJobName(characterId) : characterId;
    }

    public string GetPrefabName(string characterId)
    {
        return registry != null ? registry.GetPrefabName(characterId) : characterId;
    }

    static GameObject LoadUnitPrefab(string prefabName, string mercId)
    {
        if (!string.IsNullOrEmpty(prefabName))
        {
            var p = Resources.Load<GameObject>("Units/" + prefabName);
            if (p != null) return p;
        }
        if (!string.IsNullOrEmpty(mercId) && mercId != prefabName)
        {
            var p = Resources.Load<GameObject>("Units/" + mercId);
            if (p != null) return p;
        }
        if (!string.IsNullOrEmpty(mercId) && mercId.Length >= 3)
        {
            char last = mercId[mercId.Length - 1];
            if (last == '2' || last == '3' || last == '4')
            {
                string fallback = mercId.Substring(0, mercId.Length - 1) + "1";
                var p = Resources.Load<GameObject>("Units/" + fallback);
                if (p != null)
                {
                    Debug.LogWarning($"[MercenaryManager] {mercId} 无预制体，回退 {fallback}");
                    return p;
                }
            }
        }
        return null;
    }

    public Sprite GetPlayerIcon() => GetIcon(PLAYER_ID);

    #endregion

    #region 出战佣兵

    public int GetMaxMercSlots()
    {
        int tavernLevel = SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            ? SaveSystem.Instance.Data.townLevel.tavern
            : 0;
        return Mathf.Clamp(tavernLevel, 0, 2);
    }

    public List<string> GetActiveMercIds()
    {
        var result = new List<string>();
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || data.permanentMercs == null) return result;

        int max = GetMaxMercSlots();
        for (int i = 0; i < data.permanentMercs.Count && result.Count < max; i++)
        {
            string id = data.permanentMercs[i].mercId;
            if (string.IsNullOrEmpty(id)) continue;
            if (!GameConfig.IsMercAvailable(id, data))
            {
                Debug.Log($"[MercenaryManager] 跳过不可用佣兵: {id} tier={GameConfig.GetMercTier(id)} guild={data.guildLevel}");
                continue;
            }
            result.Add(id);
        }
        return result;
    }

    public List<string> GetHireableMercIds()
    {
        var result = new List<string>();
        if (registry == null || registry.entries == null) return result;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        foreach (var e in registry.entries)
        {
            if (e == null || e.isPlayer || string.IsNullOrEmpty(e.characterId)) continue;
            if (!GameConfig.IsMercAvailable(e.characterId, data)) continue;
            result.Add(e.characterId);
        }
        return result;
    }

    public bool IsAdvancedMerc(string mercId) => GameConfig.GetMercTier(mercId) == MercTier.Advanced;
    public bool IsStoryNpc(string id) => GameConfig.GetMercTier(id) == MercTier.Npc;

    public List<Mercenary> GetActiveMercs()
    {
        _activeMercs.RemoveAll(m => m == null);
        return _activeMercs;
    }

    #endregion

    #region 生成/回收

    /// <summary>
    /// 与 Hero 同路径：Instantiate → 挂 unit → 世界坐标 → Init(朝向)。
    /// 不改预制体缩放、不改 Sorting、不改 RectTransform sizeDelta。
    /// </summary>
    public Mercenary SpawnMercenary(string mercId, Vector3 position, int level = 1)
    {
        if (string.IsNullOrEmpty(mercId)) return null;

        string prefabName = GetPrefabName(mercId);
        GameObject prefab = LoadUnitPrefab(prefabName, mercId);
        if (prefab == null)
        {
            Debug.LogWarning($"[MercenaryManager] 预制体不存在: Units/{prefabName} (id={mercId})");
            return null;
        }

        position.y = UnitBase.GROUND_Y;
        if (BattleManager.Instance != null && BattleManager.Instance.unitRoot != null)
            position.z = BattleManager.Instance.unitRoot.position.z;

        GameObject go = Instantiate(prefab);
        go.name = "Merc_" + mercId;
        go.SetActive(true);

        // 与玩家一样：挂到 unit，保持世界坐标
        GameConfig.AttachToUnitRoot(go.transform);
        GameConfig.SetWorldPosition(go, position);
        go.transform.rotation = Quaternion.identity;

        // 大小跟玩家对齐（只抄绝对值，朝向交给 Face）
        if (Hero.Instance != null)
        {
            Vector3 hs = Hero.Instance.transform.localScale;
            float ax = Mathf.Abs(hs.x); if (ax < 0.0001f) ax = 1f;
            float ay = Mathf.Abs(hs.y); if (ay < 0.0001f) ay = 1f;
            float az = Mathf.Abs(hs.z); if (az < 0.0001f) az = 1f;
            go.transform.localScale = new Vector3(ax, ay, az);
        }

        // 只补物理（SPUM 根常没有），不碰 Sorting
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        Mercenary merc = go.GetComponent<Mercenary>();
        if (merc == null) merc = go.AddComponent<Mercenary>();
        merc.Init(mercId, level); // 内部 Face(1)

        GameConfig.SetWorldPosition(go, position);

        Transform heroT = Hero.Instance != null ? Hero.Instance.transform : null;
        Debug.Log($"[MercenaryManager] {mercId} 已挂 unit parent={go.transform.parent?.name} " +
                  $"pos={go.transform.position} localScale={go.transform.localScale} " +
                  $"heroPos={(heroT != null ? heroT.position.ToString() : "null")}");

        _activeMercs.Add(merc);
        return merc;
    }

    public void ResetMercenaries(Vector3 basePosition)
    {
        var mercs = GetActiveMercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            if (mercs[i] == null) continue;
            GameConfig.AttachToUnitRoot(mercs[i].transform);
            GameConfig.SetWorldPosition(mercs[i].gameObject, basePosition + new Vector3(-0.85f * (i + 1), 0, 0));
            mercs[i].currentHp = mercs[i].attr.GetAttr(AttrType.MaxHp);
            mercs[i].gameObject.SetActive(true);
            mercs[i].ResetForReuse();
            mercs[i].Face(1);
        }
    }

    public void ClearAllMercs()
    {
        foreach (var m in _activeMercs)
        {
            if (m != null) Destroy(m.gameObject);
        }
        _activeMercs.Clear();
    }

    #endregion
}
