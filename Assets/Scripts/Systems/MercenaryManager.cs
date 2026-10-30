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
            registry = Resources.Load<CharacterRegistry>(ContentPaths.Config.CharacterRegistry);
            if (registry == null)
                Debug.LogWarning("[MercenaryManager] 未找到CharacterRegistry，请运行 Tools/生成角色注册表 菜单生成");
        }
    }

    #region 头像/配置查询

    public Sprite GetIcon(string characterId)
    {
        var sp = MercPortraitSprites.GetHead(characterId);
        if (sp != null) return sp;

        sp = registry != null ? registry.GetIcon(characterId) : null;
        if (sp != null) return sp;
        if (string.IsNullOrEmpty(characterId)) return null;

        sp = LoadHeadSprite("icon_" + characterId);
        if (sp != null) return sp;

        // 101/102 盾兵共用头像（老盾=101）
        if (characterId.StartsWith("dunbing"))
        {
            sp = LoadHeadSprite("icon_dunbing101");
            if (sp != null) return sp;
            sp = LoadHeadSprite("icon_dunbing102");
            if (sp != null) return sp;
            sp = LoadHeadSprite("icon_dunbing201");
            if (sp != null) return sp;
        }
        if (characterId.StartsWith("fashi"))
        {
            sp = LoadHeadSprite("icon_naima202");
            if (sp != null) return sp;
            sp = LoadHeadSprite("icon_naima101");
            if (sp != null) return sp;
        }
        if (characterId.StartsWith("zhongzhan"))
        {
            sp = LoadHeadSprite("icon_kuangzhan201");
            if (sp != null) return sp;
            sp = LoadHeadSprite("icon_dunbing201");
            if (sp != null) return sp;
        }
        return null;
    }

    // 【2026-10-23 主人拍板】头像加载结果缓存：兜底分支会 Sprite.Create 现造 Sprite，
    // 每次调用都新建一个、既不缓存也不销毁 → 反复刷新界面就是持续泄漏。按文件名缓存一次。
    // ⚠️ 只缓存「取到图」的结果：缺图不进缓存，补上图后下次刷新即可生效（不掩盖问题）。
    static readonly Dictionary<string, Sprite> _headSpriteCache = new Dictionary<string, Sprite>();

    static Sprite LoadHeadSprite(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        if (_headSpriteCache.TryGetValue(fileNameWithoutExt, out var cached)) return cached;

        Sprite result = LoadHeadSpriteUncached(fileNameWithoutExt);
        if (result != null) _headSpriteCache[fileNameWithoutExt] = result;
        return result;
    }

    static Sprite LoadHeadSpriteUncached(string fileNameWithoutExt)
    {
        var sp = Resources.Load<Sprite>("UI/Heads/" + fileNameWithoutExt);
        if (sp != null) return sp;

        var all = Resources.LoadAll<Sprite>("UI/Heads/" + fileNameWithoutExt);
        if (all != null && all.Length > 0) return all[0];

        var tex = Resources.Load<Texture2D>("UI/Heads/" + fileNameWithoutExt);
        if (tex != null)
        {
            var made = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            made.name = fileNameWithoutExt;
            return made;
        }

#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            string artPath = "Assets/Art/UI/Icons/Heads/" + fileNameWithoutExt + ".png";
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            if (sp != null) return sp;
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
            if (tex != null)
            {
                var made = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                made.name = fileNameWithoutExt;
                return made;
            }
        }
#endif
        return null;
    }

    public string GetJobName(string characterId)
    {
        string fromRoster = MercRosterDefs.GetJobName(characterId);
        if (!string.IsNullOrEmpty(fromRoster)) return fromRoster;
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

    public Sprite GetStandPortrait(string hireIdOrAssetId)
        => MercPortraitSprites.GetStand(hireIdOrAssetId);

    public Sprite GetHeadPortrait(string hireIdOrAssetId)
        => MercPortraitSprites.GetHead(hireIdOrAssetId);

    public Sprite GetPlayerIcon()
    {
        var sp = MercPortraitSprites.GetHead("player");
        return sp != null ? sp : GetIcon(PLAYER_ID);
    }

    #endregion

    #region 出战佣兵

    /// <summary>
    /// 已解锁的佣兵槽数（0~2）。
    /// 新流程：佣兵靠战斗结束/升级的「三选一」招募解锁 —— 招到几位就解锁几位。
    /// 旧存档 / 未开局时回退到酒馆等级，保证城镇与旧流程仍可用。
    /// </summary>
    public int GetMaxMercSlots()
    {
        if (RunLoadout.IsActive)
        {
            int recruited = RunLoadout.Mercs() != null ? RunLoadout.Mercs().Count : 0;
            // 引导局脚本化出佣兵时可能还没落进构筑，至少给 1 个槽
            if (recruited <= 0 && TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud)
                recruited = 1;
            return Mathf.Clamp(recruited, 0, 2);
        }

        int tavernLevel = SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            ? SaveSystem.Instance.Data.townLevel.tavern
            : 0;
        return Mathf.Clamp(tavernLevel, 0, 2);
    }

    public List<MercenaryData> GetActiveMercData()
    {
        var result = new List<MercenaryData>();
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return result;
        var source = data.hiredMercs;
        if (source == null || source.Count == 0)
            source = data.permanentMercs;
        if (source == null) return result;
        int max = GetMaxMercSlots();
        for (int i = 0; i < source.Count && result.Count < max; i++)
        {
            var m = source[i];
            if (m == null || string.IsNullOrEmpty(m.mercId)) continue;
            if (!GameConfig.IsMercAvailable(m.mercId, data)) continue;
            result.Add(m);
        }
        return result;
    }

    public List<string> GetActiveMercIds()
    {
        var result = new List<string>();
        var list = GetActiveMercData();
        for (int i = 0; i < list.Count; i++)
            result.Add(list[i].mercId);
        return result;
    }

    public List<string> GetActiveMercHireIds()
    {
        var result = new List<string>();
        var list = GetActiveMercData();
        for (int i = 0; i < list.Count; i++)
            result.Add(list[i].hireId);
        return result;
    }

    public List<string> GetHireableMercIds()
    {
        var result = new List<string>();
        var seen = new HashSet<string>();
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

        void TryAdd(string id)
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) return;
            if (!GameConfig.IsMercAvailable(id, data)) return;
            string prefab = GetPrefabName(id);
            if (LoadUnitPrefab(prefab, id) == null) return;
            result.Add(id);
        }

        if (registry != null && registry.entries != null)
        {
            foreach (var e in registry.entries)
            {
                if (e == null || e.isPlayer) continue;
                TryAdd(e.characterId);
            }
        }

        var roster = MercRosterDefs.GetHireableAssetIds();
        for (int i = 0; i < roster.Count; i++)
            TryAdd(roster[i]);

        return result;
    }

    public bool IsAdvancedMerc(string mercId) => GameConfig.GetMercTier(mercId) == MercTier.Advanced;
    public bool IsStoryNpc(string id) => GameConfig.GetMercTier(id) == MercTier.Npc;

    public List<Mercenary> GetActiveMercs()
    {
        // 【2026-10-23 主人拍板】原 List.RemoveAll(m => m == null) 内部每次调用都 new List，
        // 而牧师 AI（AllyNeedsHeal）在 CD 好但蓝不足时每帧走到这里，持续产生 GC。
        // 改为原地压缩（零分配），对外语义不变（仍是同一份列表、死亡单位被剔除）。
        int w = 0;
        for (int i = 0; i < _activeMercs.Count; i++)
        {
            if (_activeMercs[i] != null)
                _activeMercs[w++] = _activeMercs[i];
        }
        if (w < _activeMercs.Count)
            _activeMercs.RemoveRange(w, _activeMercs.Count - w);
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

    /// <summary>
    /// 敌方佣兵生成入口（2026-10-30 主人拍板「竞技场真战斗」，照 <see cref="SpawnMercenary"/> 加载链）。
    /// 与 SpawnMercenary 唯二差异 —— ① isAlly=false（敌方阵营，Init 内默认 true，生成后改判）；
    /// ② 不进 _activeMercs（不参与友军编队/波清回血/复活链/OnMercenaryDead）；调用方（ArenaEnemySpawner）
    /// 负责 Add 进 BattleManager.monsters。Init 走 (id, level, star) 三参重载——lv 与 ★ 两维都进属性
    /// （PowerCalc lv×70+★×110 口径）。🔴 不调 SetHireId：防止吃到玩家局外升星/养成/羁绊加成。
    /// </summary>
    public Mercenary SpawnEnemyMercenary(string mercId, Vector3 position, int level, int star)
    {
        if (string.IsNullOrEmpty(mercId)) return null;

        string prefabName = GetPrefabName(mercId);
        GameObject prefab = LoadUnitPrefab(prefabName, mercId);
        if (prefab == null)
        {
            Debug.LogError($"[MercenaryManager] 敌方佣兵预制体不存在: Units/{prefabName} (id={mercId})（不静默）");
            return null;
        }

        position.y = UnitBase.GROUND_Y;
        if (BattleManager.Instance != null && BattleManager.Instance.unitRoot != null)
            position.z = BattleManager.Instance.unitRoot.position.z;

        GameObject go = Instantiate(prefab);
        go.name = "EnemyMerc_" + mercId;
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
        merc.Init(mercId, level, star);   // 三参重载：lv+★ 两维进属性
        merc.isAlly = false;              // 🔴 敌方阵营（索敌/AI/治疗全按 isAlly 分流）

        GameConfig.SetWorldPosition(go, position);

        Debug.Log($"[MercenaryManager] 敌方佣兵 {mercId} 已生成 Lv{level}★{star} isAlly=false pos={go.transform.position}");
        return merc;
    }

    public void ResetMercenaries(Vector3 basePosition)
    {
        var mercs = GetActiveMercs();
        Hero hero = Hero.Instance;
        for (int i = 0; i < mercs.Count; i++)
        {
            if (mercs[i] == null) continue;
            GameConfig.AttachToUnitRoot(mercs[i].transform);
            float x = hero != null
                ? UnitCrowd.GetMercDesiredCombatX(hero, mercs[i], i)
                : basePosition.x + 0.85f * (i + 1);
            GameConfig.SetWorldPosition(mercs[i].gameObject, new Vector3(x, UnitBase.GROUND_Y, basePosition.z));
            mercs[i].SetPartyIndex(i);
            // 【2026-10-20 主人拍板·方案C】进关不再满血：与英雄同口径基础回复 30%（GameConfig.STAGE_ENTRY_HEAL_PCT），
            // 血量跨关延续。阵亡佣兵复活比例由 BattleManager.ReviveFallenMercsForStage 在本步之后单独补砍，不受影响。
            float mMaxHp = mercs[i].attr.GetAttr(AttrType.MaxHp);
            mercs[i].currentHp = Mathf.Min(mMaxHp, mercs[i].currentHp + mMaxHp * GameConfig.STAGE_ENTRY_HEAL_PCT);
            mercs[i].gameObject.SetActive(true);
            mercs[i].ResetForReuse();
            mercs[i].Face(1);
        }
    }

    public void ClearAllMercs()
    {
        // 【2026-10-17 诊断日志】清场是佣兵退场出口之一，与 OnMercenaryDead（阵亡）日志配对：
        // 复现「佣兵消失」时，两条日志都没有 → 另有路径，再深挖。
        Debug.LogWarning($"[MercenaryManager] ClearAllMercs：清掉 {GetActiveMercs().Count} 个在场佣兵");
        foreach (var m in _activeMercs)
        {
            if (m != null) Destroy(m.gameObject);
        }
        _activeMercs.Clear();
    }

    /// <summary>
    /// 【2026-10-07 主人拍板】收掉某一位**演出用**佣兵（引导「塔克天降救场」那位）。
    ///
    /// <para>为什么要单独一个出口：天降那位只是剧情替身，真正入队走正式招募链路
    /// （战斗中抽奖 → <c>RunDraftDirector.RecruitMerc</c>）。替身不收，抽完场上就是两个塔克。</para>
    ///
    /// <para>这里同时清 <c>_activeMercs</c> 与 <c>BattleManager.allyUnits</c>，
    /// 别在调用方各清一半（铁律「同一语义只留一个出口」）。</para>
    /// </summary>
    public void DespawnMercenary(Mercenary merc)
    {
        if (merc == null) return;
        _activeMercs.Remove(merc);
        var bm = BattleManager.Instance;
        if (bm != null) bm.allyUnits.Remove(merc);
        if (Application.isPlaying) Destroy(merc.gameObject);
        else DestroyImmediate(merc.gameObject);
    }

    #endregion
}
