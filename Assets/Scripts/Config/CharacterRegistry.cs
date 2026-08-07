using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色注册表：配置每个角色/佣兵的头像Sprite、预制体名、职业名
/// 沿用 MonsterSpriteRegistry 模式，放 Resources/Config 下运行时加载
///
/// 命名规则：
///   预制体名 xxx（如 wanjia, dunbing101）位于 Assets/SPUM/Resources/Units/
///   头像图标 icon_xxx（如 icon_wanjia）位于 Assets/Art/UI/Icons/Heads/
///   characterId = 预制体名（去掉 icon_ 前缀），与存档 MercenaryData.mercId 一致
///
/// 用 Tools/生成角色注册表 菜单自动扫描生成，无需手动拖拽
/// </summary>
[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "Config/CharacterRegistry")]
public class CharacterRegistry : ScriptableObject
{
    [Header("角色配置列表")]
    public List<CharacterEntry> entries = new List<CharacterEntry>();

    /// <summary>运行时字典缓存：characterId → entry，O(1)查询</summary>
    private Dictionary<string, CharacterEntry> _cache;

    [System.Serializable]
    public class CharacterEntry
    {
        [Tooltip("角色ID，与预制体名一致（如 wanjia, dunbing101）")]
        public string characterId;
        [Tooltip("SPUM预制体名，位于 Resources/Units/ 下；空表示无预制体（仅头像）")]
        public string prefabName;
        [Tooltip("头像Sprite")]
        public Sprite iconSprite;
        [Tooltip("职业名")]
        public string jobName;
        [Tooltip("是否玩家本体")]
        public bool isPlayer;
    }

    /// <summary>初始化字典缓存（懒加载，首次查询时触发）</summary>
    private void EnsureCache()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, CharacterEntry>();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (e != null && !string.IsNullOrEmpty(e.characterId) && !_cache.ContainsKey(e.characterId))
                _cache[e.characterId] = e;
        }
    }

    /// <summary>编辑器修改 entries 后清缓存（构建时自动调用）</summary>
    public void InvalidateCache()
    {
        _cache = null;
    }

    public CharacterEntry GetEntry(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        EnsureCache();
        return _cache.TryGetValue(characterId, out var e) ? e : null;
    }

    /// <summary>获取头像Sprite</summary>
    public Sprite GetIcon(string characterId)
    {
        var e = GetEntry(characterId);
        return e != null ? e.iconSprite : null;
    }

    /// <summary>获取预制体名（无配置时回退为characterId本身）</summary>
    public string GetPrefabName(string characterId)
    {
        var e = GetEntry(characterId);
        if (e == null || string.IsNullOrEmpty(e.prefabName)) return characterId;
        return e.prefabName;
    }

    /// <summary>获取职业名</summary>
    public string GetJobName(string characterId)
    {
        var e = GetEntry(characterId);
        return e != null ? e.jobName : characterId;
    }
}
