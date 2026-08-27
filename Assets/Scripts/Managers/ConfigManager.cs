using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 配置管理器
/// </summary>
public class ConfigManager : Singleton<ConfigManager>
{
    private Dictionary<string, EquipTemplate> _equipTemplateDict = new Dictionary<string, EquipTemplate>();
    private Dictionary<string, MonsterConfig> _monsterDict = new Dictionary<string, MonsterConfig>();
    private Dictionary<string, TalentConfig> _talentDict = new Dictionary<string, TalentConfig>();

    private List<EquipTemplate> _allEquipTemplates = new List<EquipTemplate>();
    private List<MonsterConfig> _allMonsters = new List<MonsterConfig>();

    protected override void Awake()
    {
        base.Awake();
        LoadAllConfig();
    }

    private void LoadAllConfig()
    {
        _allEquipTemplates = Resources.LoadAll<EquipTemplate>(ContentPaths.Config.Equips).ToList();
        foreach (var t in _allEquipTemplates)
        {
            if (t == null) continue;
            t.ResolveIcon();
            _equipTemplateDict[t.templateId] = t;
        }

        _allMonsters = Resources.LoadAll<MonsterConfig>(ContentPaths.Config.Monsters).ToList();
        foreach (var m in _allMonsters)
        {
            _monsterDict[m.id] = m;
        }

        try
        {
            var allTalents = Resources.LoadAll<TalentConfig>(ContentPaths.Config.Talents);
            if (allTalents != null)
            {
                for (int i = 0; i < allTalents.Length; i++)
                {
                    var t = allTalents[i];
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    _talentDict[t.id] = t;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ConfigManager] 天赋配置加载失败（可忽略，不影响战斗）: " + e.Message);
        }

        Debug.Log($"配置加载完成：{_allEquipTemplates.Count}个装备模板，{_allMonsters.Count}种怪物，{_talentDict.Count}个天赋");
        GameDataHub.ReportConfigs(_allEquipTemplates, _allMonsters, _talentDict);
        SpecialWeapons.EnsureTwilightTemplate();
    }

    /// <summary>运行时注册（如暮火之杖兜底实例）。</summary>
    public void RegisterRuntimeEquip(EquipTemplate tpl)
    {
        if (tpl == null || string.IsNullOrEmpty(tpl.templateId)) return;
        _equipTemplateDict[tpl.templateId] = tpl;
        if (!_allEquipTemplates.Contains(tpl))
            _allEquipTemplates.Add(tpl);
    }

    public EquipTemplate GetEquipTemplate(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id == SpecialWeapons.TwilightStaffId)
            SpecialWeapons.EnsureTwilightTemplate();
        return _equipTemplateDict.ContainsKey(id) ? _equipTemplateDict[id] : null;
    }

    public MonsterConfig GetMonster(string id)
    {
        return _monsterDict.ContainsKey(id) ? _monsterDict[id] : null;
    }

    public TalentConfig GetTalent(string id)
    {
        return _talentDict.ContainsKey(id) ? _talentDict[id] : null;
    }

    /// <summary>
    /// 获取随机装备实例
    /// </summary>
    public List<EquipInstance> GetRandomEquipInstances(int count, int blacksmithLevel, int bonusStar = 0)
    {
        return GetRandomEquipInstances(count, blacksmithLevel, bonusStar, StageType.Normal);
    }

    public List<EquipInstance> GetRandomEquipInstances(int count, int blacksmithLevel, int bonusStar, StageType stageType)
    {
        Rarity maxRarity = (Rarity)Mathf.Min(blacksmithLevel + 1, (int)Rarity.Legendary);
        List<EquipTemplate> available = _allEquipTemplates.Where(t => t != null).ToList();
        List<EquipTemplate> weapons = available
            .Where(t => t.slotType == EquipSlotType.MainHand || t.slotType == EquipSlotType.OffHand)
            .ToList();
        List<EquipInstance> result = new List<EquipInstance>();
        if (available.Count == 0) return result;
        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        for (int i = 0; i < count; i++)
        {
            EquipTemplate template;
            if (weapons.Count > 0 && Random.value < 0.45f)
                template = weapons[Random.Range(0, weapons.Count)];
            else
                template = available[Random.Range(0, available.Count)];

            Rarity rolled = EquipDropRules.RollRarity(stageType, maxRarity);
            result.Add(EquipInstance.GenerateFromTemplate(template, bonusStar, lv, true, rolled));
        }
        return result;
    }

    /// <summary>冒险界面预览：该章全部怪（小怪在前，Boss 在后），不按小波次过滤。</summary>
    public List<MonsterConfig> GetChapterPreviewMonsters(int gameChapter)
    {
        int monsterChapter = GameConfig.GetMonsterChapter(gameChapter);
        return _allMonsters
            .Where(m => ExtractChapterFromId(m.id) == monsterChapter)
            .OrderBy(m => m.isBoss ? 1 : 0)
            .ThenBy(m => m.spriteIndex)
            .ToList();
    }

    public List<MonsterConfig> GetWaveMonsterPool(int chapter, int stageInChapter)
    {
        // 获取当前章节对应的怪物章节号
        int monsterChapter = GameConfig.GetMonsterChapter(chapter);

        // 获取该章节的通关次数（用于渐进式解锁）
        int clearCount = ChapterManager.Instance != null
            ? ChapterManager.Instance.GetChapterClearCount(chapter)
            : 0;

        List<MonsterConfig> pool = _allMonsters
            .Where(m => {
                int ch = ExtractChapterFromId(m.id);
                // 同章怪物，且关卡已达到出场门槛，且通关次数满足解锁条件
                return ch == monsterChapter
                    && stageInChapter >= m.minWave
                    && clearCount >= m.unlockClearCount;
            })
            .OrderBy(m => m.minWave) // 按出场顺序排序（初级→高级）
            .ToList();

        // 只有章节最后一关（index=9）才允许BOSS出现在池中
        bool isBossStage = stageInChapter == GameConfig.STAGES_PER_CHAPTER - 1;
        if (!isBossStage)
        {
            pool = pool.Where(m => !m.isBoss).ToList();
        }

        Debug.Log($"[ConfigManager] 章节{chapter}(怪物章{monsterChapter}) 第{stageInChapter}关 通关{clearCount}次: 池子={pool.Count}只怪物");
        return pool;
    }

    /// <summary>
    /// 获取当前章节+关卡+通关次数下可用的怪物精灵编号列表
    ///
    /// 渐进式解锁规则：
    /// - 通关0次: 精灵1-5可用（前4-5种）
    /// - 通关2-3次: 精灵1-8可用（新增2-3种）
    /// - 通关4+次: 精灵1-10可用（新增2-3种）
    /// - 精灵11-12: 始终是BOSS，不会出现在普通池中
    ///
    /// 关卡内渐进规则：
    /// - 第1关: 精灵1,2
    /// - 第2关: 精灵1,2,3
    /// - 第3关+: 精灵1-(3+stageIndex)，但不超过通关次数上限
    /// </summary>
    public List<int> GetAvailableSpriteIndices(int gameChapter, int stageIndex, bool isBossWave)
    {
        int clearCount = ChapterManager.Instance != null
            ? ChapterManager.Instance.GetChapterClearCount(gameChapter)
            : 0;

        // 1. 根据通关次数确定最大可用编号
        int tierMax;
        if (clearCount >= GameConfig.TIER2_UNLOCK_CLEARS)
            tierMax = GameConfig.TIER2_MAX_SPRITE;
        else if (clearCount >= GameConfig.TIER1_UNLOCK_CLEARS)
            tierMax = GameConfig.TIER1_MAX_SPRITE;
        else
            tierMax = GameConfig.TIER0_MAX_SPRITE;

        // 2. 根据关卡索引确定本关可用编号上限
        // 第1关(index=0): 1-2, 第2关(index=1): 1-3, 第3关(index=2): 1-4...
        int stageMax = Mathf.Min(2 + stageIndex, tierMax);

        // 3. BOSS波次返回BOSS编号
        if (isBossWave)
        {
            var bossIndices = new List<int>();
            for (int i = GameConfig.BOSS_SPRITE_START; i <= GameConfig.MONSTERS_PER_CHAPTER; i++)
                bossIndices.Add(i);
            return bossIndices;
        }

        // 4. 普通波次：返回1到stageMax的编号列表
        var indices = new List<int>();
        for (int i = 1; i <= stageMax; i++)
            indices.Add(i);

        Debug.Log($"[ConfigManager] 章节{gameChapter} 第{stageIndex + 1}关 通关{clearCount}次: 可用精灵={string.Join(",", indices)} (tierMax={tierMax})");
        return indices;
    }

    /// <summary>
    /// 加权选精灵：始终从低编号起偏重（1 权重最高），关卡越后高编号才逐渐增加。
    /// </summary>
    public int PickWeightedSpriteIndex(List<int> availableIndices, int stageIndex)
    {
        if (availableIndices == null || availableIndices.Count == 0) return 1;

        var sorted = availableIndices.OrderBy(x => x).ToList();
        var weights = new List<float>();
        for (int i = 0; i < sorted.Count; i++)
        {
            int idx = sorted[i];
            // 早期关：强烈偏向 1；后期：编号越大权重略增，但 1 仍保留保底权重
            float w;
            if (stageIndex <= 0)
                w = idx == 1 ? 5f : 1f;
            else if (stageIndex <= 2)
                w = idx == 1 ? 3f : (idx == 2 ? 2f : 1f);
            else
                w = Mathf.Max(1f, idx * 0.5f);
            weights.Add(w);
        }

        float total = 0f;
        foreach (var w in weights) total += w;
        float roll = Random.Range(0f, total);
        float cum = 0f;
        for (int i = 0; i < sorted.Count; i++)
        {
            cum += weights[i];
            if (roll <= cum)
                return sorted[i];
        }
        return sorted[0];
    }

    /// <summary>从怪物ID中提取章节号: "forest_401" → 4, "undead_101" → 1</summary>
    private static int ExtractChapterFromId(string id)
    {
        int underscoreIdx = id.IndexOf('_');
        if (underscoreIdx >= 0 && underscoreIdx + 2 < id.Length)
        {
            if (int.TryParse(id.Substring(underscoreIdx + 1, 1), out int ch))
                return ch;
        }
        return 0;
    }

    public List<TalentConfig> GetAllTalents()
    {
        return _talentDict.Values.ToList();
    }
}
