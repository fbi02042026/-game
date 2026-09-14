using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>局内技能（本局有效，撤离/死亡后丢弃）。</summary>
[Serializable]
public class RunSkillEntry
{
    public string id;
    public int star = 1;
}

/// <summary>局内佣兵（本局有效，撤离/死亡后丢弃；不写城镇存档）。</summary>
[Serializable]
public class RunMercEntry
{
    public string hireId;
    public string mercId;
    public string displayName;
    public string nickname;
    public int level = 1;
    public int star = 1;
    public string skillId;
    public string passiveSkillId;

    public MercenaryData ToMercenaryData()
    {
        return new MercenaryData
        {
            mercId = mercId,
            hireId = hireId,
            displayName = string.IsNullOrEmpty(displayName) ? (hireId ?? mercId) : displayName,
            nickname = nickname,
            level = Mathf.Max(1, level),
            star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5),
            favorLevel = 1,
            skillId = skillId,
            passiveSkillId = passiveSkillId,
            uid = "run_" + (hireId ?? mercId) + "_" + level
        };
    }
}

/// <summary>可 JsonUtility 序列化的整局构筑快照（用于「继续上一局」）。</summary>
[Serializable]
public class RunLoadoutData
{
    public int version = 1;
    /// <summary>false = 本局已结束（撤离/死亡/通关），不应续关。</summary>
    public bool active;
    public int jobId;
    public int chapter = 1;
    public int stageIndex;
    public int heroLevel = 1;
    public int heroExp;
    public string savedUtc = "";
    public List<RunSkillEntry> skills = new List<RunSkillEntry>();
    public List<RunMercEntry> mercs = new List<RunMercEntry>();
    /// <summary>流派势能（Phase 2）：累积点，不换不退。</summary>
    public List<StringIntEntry> themeEntries = new List<StringIntEntry>();
}

/// <summary>
/// 本局构筑（技能 + 佣兵 + 势能）。与城镇存档完全隔离：
/// 只落 PlayerPrefs（离线可用），撤离/死亡即作废；金币与天赋石照常走 SaveSystem。
/// </summary>
public static class RunLoadout
{
    const string PrefsKey = "RiftRunLoadout_v1";

    /// <summary>本局最多携带的主动技能数（含职业初始技）。V6：3 → 4。</summary>
    public const int MaxSkillSlots = 4;
    /// <summary>本局最多同时出战的佣兵数（不受酒馆等级限制）。战斗 UI 只有 2 张佣兵卡，故上限 2。</summary>
    public const int MaxRunMercs = 2;

    static RunLoadoutData _data;
    static readonly Dictionary<string, int> _themeCache = new Dictionary<string, int>();

    public static RunLoadoutData Data => _data;
    public static bool IsActive => _data != null && _data.active;

    /// <summary>
    /// 内存模式（新手引导专用）：构筑只在内存里存在，不读也不写 PlayerPrefs。
    /// 引导局要用同一套技能/抽卡链路教学，但不能污染玩家真正的「继续上一局」存档。
    /// </summary>
    public static bool MemoryMode { get; private set; }

    /// <summary>开一局临时构筑（引导局）。会丢弃当前内存里的构筑。</summary>
    public static void BeginMemoryMode(PlayerJobId job)
    {
        MemoryMode = true;
        _data = null;
        _themeCache.Clear();
        _starRecord.Clear();
        BeginNew(job);
        Debug.Log("[RunLoadout] 进入内存模式（引导局构筑不与存档互通）");
    }

    /// <summary>结束内存模式：整包丢掉，不落盘。</summary>
    public static void EndMemoryMode()
    {
        if (!MemoryMode) return;
        MemoryMode = false;
        _data = null;
        _themeCache.Clear();
        _starRecord.Clear();
        Debug.Log("[RunLoadout] 退出内存模式，引导局构筑已丢弃");
    }

    // ============================================================
    // 开新局 / 续关 / 落档
    // ============================================================

    /// <summary>本局归属章节：优先冒险页实际选中的章，其次存档最远解锁章。</summary>
    static int ResolveRunChapter()
    {
        int pending = AdventureUI.PendingBattleChapter;
        if (pending >= 1) return pending;
        int max = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        return Mathf.Max(1, max);
    }

    /// <summary>开新局：职业初始技能进构筑，清空佣兵与势能。</summary>
    public static RunLoadoutData BeginNew(PlayerJobId job)
    {
        _data = new RunLoadoutData
        {
            active = true,
            jobId = (int)job,
            // 章节分叉后 maxUnlockedChapter 是「最远可达章」，玩家实际打的不一定是它；
            // 优先用冒险页真正选中的章（PendingBattleChapter）
            chapter = ResolveRunChapter(),
            stageIndex = 0,
            heroLevel = 1
        };
        string starter = SaveSystem.Instance?.Data?.selectedPlayerSkillId;
        if (string.IsNullOrEmpty(starter) || PlayerSkillDefs.GetById(starter) == null)
            starter = PlayerJobDefs.Get(job).DefaultSkillId;
        if (!string.IsNullOrEmpty(starter))
            AddSkillEntry(_data, starter, 1);
        SyncThemeCache();
        return _data;
    }

    /// <summary>
    /// 进战入口：有可续的本局则续，否则按当前职业开新局。
    /// 返回 true 表示这是「继续上一局」。
    /// </summary>
    public static bool ResumeOrBegin(PlayerJobId job)
    {
        var saved = LoadFromPrefs();
        if (saved != null && saved.active && saved.skills != null && saved.skills.Count > 0)
        {
            _data = saved;
            Normalize(_data);
            SyncThemeCache();
            Debug.Log($"[RunLoadout] 继续上一局：chapter={_data.chapter} stage={_data.stageIndex} " +
                      $"skills={_data.skills.Count} mercs={_data.mercs.Count}");
            return true;
        }
        BeginNew(job);
        Debug.Log($"[RunLoadout] 开新局：job={(int)job}");
        return false;
    }

    public static bool HasResumable()
    {
        var saved = LoadFromPrefs();
        return saved != null && saved.active && saved.skills != null && saved.skills.Count > 0;
    }

    /// <summary>写 PlayerPrefs（离线，不联网）。</summary>
    public static void Save()
    {
        // 内存模式（引导局）：只留在内存，绝不写 PlayerPrefs
        if (_data == null || MemoryMode) return;
        _data.savedUtc = DateTimeOffset.UtcNow.ToString("o");
        FlushThemeCache();
        try
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RunLoadout] 落档失败: " + e.Message);
        }
    }

    /// <summary>本局结束（死亡/撤离/通关）：作废构筑。</summary>
    public static void Clear()
    {
        if (MemoryMode)
        {
            EndMemoryMode();
            return;
        }
        _data = null;
        _themeCache.Clear();
        _starRecord.Clear();
        try
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RunLoadout] 清档失败: " + e.Message);
        }
        Debug.Log("[RunLoadout] 本局构筑已作废");
    }

    static RunLoadoutData LoadFromPrefs()
    {
        try
        {
            if (MemoryMode) return null;
            if (!PlayerPrefs.HasKey(PrefsKey)) return null;
            string json = PlayerPrefs.GetString(PrefsKey);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<RunLoadoutData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RunLoadout] 读档失败，忽略: " + e.Message);
            return null;
        }
    }

    static void Normalize(RunLoadoutData d)
    {
        d.skills ??= new List<RunSkillEntry>();
        d.mercs ??= new List<RunMercEntry>();
        d.themeEntries ??= new List<StringIntEntry>();
        if (d.chapter < 1) d.chapter = 1;
        if (d.heroLevel < 1) d.heroLevel = 1;
        if (d.jobId < 0 || d.jobId >= PlayerJobDefs.All.Length) d.jobId = (int)PlayerJobId.SwordShield;
        for (int i = d.skills.Count - 1; i >= 0; i--)
        {
            var s = d.skills[i];
            if (s == null || string.IsNullOrEmpty(s.id))
            {
                d.skills.RemoveAt(i);
                continue;
            }
            if (s.star < 1) s.star = 1;
        }
        for (int i = d.mercs.Count - 1; i >= 0; i--)
        {
            var m = d.mercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId))
            {
                d.mercs.RemoveAt(i);
                continue;
            }
            if (m.level < 1) m.level = 1;
            if (m.star < 1) m.star = 1;
        }
    }

    // ============================================================
    // 技能
    // ============================================================

    public static List<string> SkillIds()
    {
        var list = new List<string>();
        if (_data?.skills == null) return list;
        for (int i = 0; i < _data.skills.Count; i++)
            if (_data.skills[i] != null && !string.IsNullOrEmpty(_data.skills[i].id))
                list.Add(_data.skills[i].id);
        return list;
    }

    public static bool HasSkill(string id)
    {
        if (string.IsNullOrEmpty(id) || _data?.skills == null) return false;
        for (int i = 0; i < _data.skills.Count; i++)
            if (_data.skills[i] != null && _data.skills[i].id == id) return true;
        return false;
    }

    public static int StarOf(string id)
    {
        if (string.IsNullOrEmpty(id) || _data?.skills == null) return 0;
        for (int i = 0; i < _data.skills.Count; i++)
            if (_data.skills[i] != null && _data.skills[i].id == id) return _data.skills[i].star;
        return 0;
    }

    /// <summary>历史最高星级记录表：技能被遗忘/丢弃时存一下，日后重抽到可回到最高星（不重新从 1 星起）。</summary>
    static readonly Dictionary<string, int> _starRecord = new Dictionary<string, int>();

    /// <summary>记录某技能的历史最高星级（只升不降）。内存模式随 Loadout 一起清空。</summary>
    static void RecordSkillStar(string id, int star)
    {
        if (string.IsNullOrEmpty(id) || star < 1) return;
        int prev;
        if (!_starRecord.TryGetValue(id, out prev) || star > prev)
            _starRecord[id] = star;
    }

    public static bool IsSkillFull => _data?.skills != null && _data.skills.Count >= MaxSkillSlots;

    /// <summary>获得新技能；槽位满或已拥有则失败。</summary>
    public static bool TryAddSkill(string id)
    {
        if (_data == null || string.IsNullOrEmpty(id)) return false;
        if (HasSkill(id) || IsSkillFull) return false;
        AddSkillEntry(_data, id, 1);
        AddThemePoint(SkillDraftMeta.Tag(id), 1);
        return true;
    }

    /// <summary>
    /// 遗忘（丢弃）一个技能，腾出空槽。返回被移除的条目，失败返回 null。
    /// 只删本局条目，不动技能星级记录表 —— 日后重新抽到会回到历史最高星级。
    /// </summary>
    public static RunSkillEntry DismissSkillAt(int index)
    {
        if (_data?.skills == null) return null;
        if (index < 0 || index >= _data.skills.Count) return null;

        var old = _data.skills[index];
        if (old != null && !string.IsNullOrEmpty(old.id))
            RecordSkillStar(old.id, Mathf.Max(1, old.star));
        _data.skills.RemoveAt(index);
        return old;
    }

    /// <summary>
    /// 调整技能槽顺序，成功返回 true。槽序即自动释放优先级：越靠前（①）越优先释放。
    /// 由三选一界面里的拖拽排序条调用；调用方负责随后重建技能 + 刷新 HUD。
    /// </summary>
    public static bool MoveSkill(int from, int to)
    {
        if (_data?.skills == null) return false;
        var list = _data.skills;
        if (from < 0 || from >= list.Count) return false;
        if (to < 0) to = 0;
        if (to >= list.Count) to = list.Count - 1;
        if (from == to) return false;

        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return true;
    }

    /// <summary>技能升星；未拥有或已满星则失败。</summary>
    public static bool TryUpgradeSkill(string id)
    {
        if (_data?.skills == null || string.IsNullOrEmpty(id)) return false;
        int cap = SkillRarityUtil.StarCap(SkillDraftMeta.Rarity(id));
        for (int i = 0; i < _data.skills.Count; i++)
        {
            var s = _data.skills[i];
            if (s == null || s.id != id) continue;
            if (s.star >= cap) return false;
            s.star++;
            AddThemePoint(SkillDraftMeta.Tag(id), 1);
            return true;
        }
        return false;
    }

    /// <summary>本局已有的全部技能中，是否还有可升星的。</summary>
    public static bool HasUpgradableSkill()
    {
        if (_data?.skills == null) return false;
        for (int i = 0; i < _data.skills.Count; i++)
        {
            var s = _data.skills[i];
            if (s == null) continue;
            if (s.star < SkillRarityUtil.StarCap(SkillDraftMeta.Rarity(s.id))) return true;
        }
        return false;
    }

    static void AddSkillEntry(RunLoadoutData d, string id, int star)
    {
        d.skills ??= new List<RunSkillEntry>();
        d.skills.Add(new RunSkillEntry { id = id, star = Mathf.Max(1, star) });
    }

    // ============================================================
    // 佣兵
    // ============================================================

    public static List<RunMercEntry> Mercs() => _data?.mercs ?? new List<RunMercEntry>();

    public static bool HasMerc(string hireIdOrMercId)
    {
        if (string.IsNullOrEmpty(hireIdOrMercId) || _data?.mercs == null) return false;
        for (int i = 0; i < _data.mercs.Count; i++)
        {
            var m = _data.mercs[i];
            if (m == null) continue;
            if (m.hireId == hireIdOrMercId || m.mercId == hireIdOrMercId) return true;
        }
        return false;
    }

    public static bool IsMercFull => _data?.mercs != null && _data.mercs.Count >= MaxRunMercs;

    public static bool TryAddMerc(RunMercEntry entry)
    {
        if (_data == null || entry == null || string.IsNullOrEmpty(entry.mercId)) return false;
        if (IsMercFull || HasMerc(entry.hireId ?? entry.mercId)) return false;
        _data.mercs ??= new List<RunMercEntry>();
        _data.mercs.Add(entry);
        return true;
    }

    public static RunMercEntry FindMerc(string hireIdOrMercId)
    {
        if (string.IsNullOrEmpty(hireIdOrMercId) || _data?.mercs == null) return null;
        for (int i = 0; i < _data.mercs.Count; i++)
        {
            var m = _data.mercs[i];
            if (m == null) continue;
            if (m.hireId == hireIdOrMercId || m.mercId == hireIdOrMercId) return m;
        }
        return null;
    }

    /// <summary>佣兵升级（只能由抽卡触发；不再随关卡自动升级）。返回升级后的等级，失败返回 0。</summary>
    public static int TryLevelUpMerc(string hireIdOrMercId)
    {
        var m = FindMerc(hireIdOrMercId);
        if (m == null) return 0;
        m.level = Mathf.Max(1, m.level) + 1;
        return m.level;
    }

    /// <summary>佣兵升星：★+1 且等级 +1。已满星返回 false。</summary>
    public static bool TryUpgradeMercStar(string hireIdOrMercId)
    {
        var m = FindMerc(hireIdOrMercId);
        if (m == null) return false;
        if (m.star >= MaxMercStar) return false;
        m.star++;
        m.level = Mathf.Max(1, m.level) + 1;
        return true;
    }

    /// <summary>本局佣兵星级上限。</summary>
    public const int MaxMercStar = 5;

    /// <summary>本局等级与战斗英雄等级对齐（战力显示与「续关」都用它）。</summary>
    public static void SyncHeroLevel(int level)
    {
        if (_data == null) return;
        _data.heroLevel = Mathf.Max(1, level);
        if (Hero.Instance != null)
        {
            _data.heroExp = Hero.Instance.currentExp;
        }
    }

    // ============================================================
    // 流派势能（Phase 2）：只累积，不换不退
    // ============================================================

    public static void AddThemePoint(SynergyTag tag, int amount)
    {
        if (tag == SynergyTag.None || amount <= 0) return;
        string key = tag.ToString();
        _themeCache[key] = ThemePoints(tag) + amount;
    }

    public static int ThemePoints(SynergyTag tag)
    {
        if (tag == SynergyTag.None) return 0;
        return _themeCache.TryGetValue(tag.ToString(), out int v) ? v : 0;
    }

    /// <summary>势能等级：2/5/9 点分别到 1/2/3 级。</summary>
    public static int ThemeLevel(SynergyTag tag)
    {
        int p = ThemePoints(tag);
        if (p >= 9) return 3;
        if (p >= 5) return 2;
        if (p >= 2) return 1;
        return 0;
    }

    /// <summary>势能加成倍率（作用于该流派全部技能伤害）。</summary>
    public static float ThemeDamageMul(SynergyTag tag)
    {
        switch (ThemeLevel(tag))
        {
            case 1: return 1.08f;
            case 2: return 1.18f;
            case 3: return 1.32f;
            default: return 1f;
        }
    }

    static void SyncThemeCache()
    {
        _themeCache.Clear();
        if (_data?.themeEntries == null) return;
        for (int i = 0; i < _data.themeEntries.Count; i++)
        {
            var e = _data.themeEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            _themeCache[e.id] = e.value;
        }
    }

    static void FlushThemeCache()
    {
        if (_data == null) return;
        _data.themeEntries = new List<StringIntEntry>(_themeCache.Count);
        foreach (var kv in _themeCache)
            _data.themeEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });
    }

    // ============================================================
    // 本局战力（给玩家看的「数字在长大」）
    // ============================================================

    public static int HeroLevel
    {
        get => _data != null ? _data.heroLevel : 1;
        set { if (_data != null) _data.heroLevel = Mathf.Max(1, value); }
    }

    /// <summary>本局战力：等级 + 技能星级/稀有度 + 佣兵等级/星级，越滚越大。</summary>
    public static int TotalPower()
    {
        if (_data == null) return 0;
        int power = 0;
        power += Mathf.Max(1, _data.heroLevel) * 100;

        if (_data.skills != null)
        {
            for (int i = 0; i < _data.skills.Count; i++)
            {
                var s = _data.skills[i];
                if (s == null) continue;
                power += (int)SkillDraftMeta.Rarity(s.id) * 90 + Mathf.Max(1, s.star) * 140;
            }
        }
        if (_data.mercs != null)
        {
            for (int i = 0; i < _data.mercs.Count; i++)
            {
                var m = _data.mercs[i];
                if (m == null) continue;
                power += Mathf.Max(1, m.level) * 70 + Mathf.Max(1, m.star) * 110;
            }
        }
        foreach (var kv in _themeCache)
            power += kv.Value * 30;
        return power;
    }

    /// <summary>本局战力相对上一档的提升（UI 弹跳用），无则返回 0。</summary>
    public static int ConsumeLastDelta()
    {
        int now = TotalPower();
        int delta = now - _lastPower;
        _lastPower = now;
        return delta;
    }

    static int _lastPower;
}
