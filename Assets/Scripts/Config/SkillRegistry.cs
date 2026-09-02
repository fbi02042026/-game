using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能表：Ally=玩家+佣兵共用；Monster=敌人主动技
/// </summary>
public class SkillRegistry : Singleton<SkillRegistry>
{
    private Dictionary<string, SkillConfig> _dict = new Dictionary<string, SkillConfig>();
    private Dictionary<string, SkillConfig> _runtimeMerc = new Dictionary<string, SkillConfig>();

    public const string DefaultPlayerSkillId = "ally_heal";
    public const string DefaultMercMeleeSkillId = "ally_shield";
    public const string DefaultMercRangedSkillId = "ally_thunder";
    public const string DefaultMercHealSkillId = "ally_heal";

    public const string MonsterEliteMeleeSkillId = "mon_slam_multi";
    public const string MonsterEliteRangedSkillId = "mon_magic_burst";

    protected override void Awake()
    {
        base.Awake();
        LoadAll();
    }

    public void LoadAll()
    {
        _dict.Clear();
        _runtimeMerc.Clear();
        LoadFolder(ContentPaths.Config.SkillsAlly);
        LoadFolder(ContentPaths.Config.SkillsMonster);
        LoadFolder(ContentPaths.Config.SkillsPlayerLegacy);
        LoadFolder(ContentPaths.Config.SkillsMercLegacy);
        Debug.Log($"[SkillRegistry] 已加载 {_dict.Count} 个技能配置");
        GameDataHub.ReportSkills(_dict);
    }

    void LoadFolder(string resourcesPath)
    {
        var list = Resources.LoadAll<SkillConfig>(resourcesPath);
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null || string.IsNullOrEmpty(list[i].id)) continue;
            _dict[list[i].id] = list[i];
        }
    }

    public SkillConfig Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_dict.TryGetValue(id, out var c)) return c;
        if (_runtimeMerc.TryGetValue(id, out c)) return c;
        if (MercSkillTable.IsMercSkillId(id))
        {
            c = MercSkillTable.BuildRuntimeConfig(id);
            if (c != null) _runtimeMerc[id] = c;
            return c;
        }
        return null;
    }

    public SkillSystem.ActiveSkill GetActiveSkill(string id)
    {
        var cfg = Get(id);
        return cfg != null ? cfg.ToActiveSkill() : null;
    }

    /// <summary>玩家当前携带技能（角色页选择；映射到现有 Ally SkillConfig）</summary>
    public string GetPlayerSkillId()
    {
        string fromEquip = EquipStatRollup.GetEquippedGrantSkillId(GridBackpackSystem.Instance);
        if (!string.IsNullOrEmpty(fromEquip) && Get(fromEquip) != null)
            return fromEquip;

        string selected = SaveSystem.Instance?.Data?.selectedPlayerSkillId;
        var def = PlayerSkillDefs.GetById(selected);
        if (def != null && !string.IsNullOrEmpty(def.allyConfigId))
            return def.allyConfigId;
        return DefaultPlayerSkillId;
    }

    public string GetMercDefaultSkillId(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return null;
        MercRosterDefs.GetSkillIds(mercId, out string active, out _);
        if (!string.IsNullOrEmpty(active)) return active;
        MercSkillMapping.GetDefaultSkills(mercId, out active, out _);
        return active;
    }

    public string GetMercPassiveSkillId(MercenaryData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.passiveSkillId) && Get(data.passiveSkillId) != null)
            return data.passiveSkillId;
        if (data != null)
        {
            MercRosterDefs.GetSkillIds(data.mercId, out _, out string passive);
            if (!string.IsNullOrEmpty(passive)) return passive;
        }
        return null;
    }

    public string GetMercPassiveSkillId(string mercId)
    {
        MercRosterDefs.GetSkillIds(mercId, out _, out string passive);
        if (!string.IsNullOrEmpty(passive)) return passive;
        MercSkillMapping.GetDefaultSkills(mercId, out _, out passive);
        return passive;
    }

    /// <summary>优先用存档佣兵佩戴技能；空则回退职业默认。</summary>
    public string GetMercSkillId(MercenaryData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.skillId) && Get(data.skillId) != null && !MercSkillTable.IsPassive(data.skillId))
            return data.skillId;
        return GetMercDefaultSkillId(data != null ? data.mercId : null);
    }

    public bool MercHasActiveSkill(MercenaryData data)
    {
        string id = GetMercSkillId(data);
        return !string.IsNullOrEmpty(id) && MercSkillTable.IsMercSkillId(id) && !MercSkillTable.IsPassive(id);
    }

    public string GetMercSkillId(string mercId, string preferredSkillId)
    {
        if (!string.IsNullOrEmpty(preferredSkillId) && Get(preferredSkillId) != null)
            return preferredSkillId;
        return GetMercDefaultSkillId(mercId);
    }

    /// <summary>
    /// 怪物主动技：仅精英/Boss 使用（近战重击 / 远程魔法弹）。
    /// 普通/初级小怪无主动技能、只有普攻。
    /// </summary>
    public string GetMonsterSkillId(MonsterConfig template, bool isEliteWave, bool isBossUnit, MonsterAttackStyle primaryStyle)
    {
        bool ranged = MonsterAttackStyleTable.IsRanged(primaryStyle);
        if (isBossUnit || (template != null && template.isBoss))
        {
            // Boss 两种技能轮换偏好：表内主风格决定首发
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        }
        if (isEliteWave)
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        // 普通/初级小怪无主动技能，只有普攻
        return null;
    }

    /// <summary>
    /// Resources/VFX/Skills 子目录：mon_*→Monster，SK*→Merc，其余→Ally。
    /// 找不到时再扫 Ally/Merc/Monster，避免历史放错目录静默丢特效。
    /// </summary>
    public static string ResolveSkillVfxFolder(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return "Ally";
        if (skillId.StartsWith("mon_", System.StringComparison.OrdinalIgnoreCase)) return "Monster";
        if (skillId.Length >= 2
            && (skillId[0] == 'S' || skillId[0] == 's')
            && (skillId[1] == 'K' || skillId[1] == 'k'))
            return "Merc";
        return "Ally";
    }

    /// <summary>技能专属特效预制体；给「子弹命中点用技能特效」这类场景取原始 prefab。</summary>
    public GameObject GetSkillVfxPrefab(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var cfg = Get(skillId);
        if (cfg != null && cfg.vfxPrefab != null) return cfg.vfxPrefab;

        string primary = ResolveSkillVfxFolder(skillId);
        var go = Resources.Load<GameObject>($"VFX/Skills/{primary}/{skillId}");
        if (go != null) return go;

        // 兜底：目录放错时仍能找到（开发期补洞）
        string[] folders = { "Ally", "Merc", "Monster" };
        for (int i = 0; i < folders.Length; i++)
        {
            if (folders[i] == primary) continue;
            go = Resources.Load<GameObject>($"VFX/Skills/{folders[i]}/{skillId}");
            if (go != null) return go;
        }
        return null;
    }

    /// <summary>兼容旧调用</summary>
    public string GetMonsterSkillId(MonsterConfig template, bool isEliteWave, float attackRange)
    {
        var style = attackRange >= GameConfig.RangeBow - 0.05f ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
        bool boss = template != null && template.isBoss;
        return GetMonsterSkillId(template, isEliteWave, boss, style);
    }

    /// <summary>
    /// 播放技能特效（唯一对外入口）。规则固定：
    /// 1) 专属 prefab（配置拖入 或 VFX/Skills/.../{id}）
    /// 2) 否则 SkillNaming.ResolveSkillVfxKit → 共用套
    /// 不会再用玩家武器套盖掉技能。
    /// </summary>
    public void PlaySkillVfx(string skillId, Vector3 pos, bool isAllyCaster, int facingDir = 1, Transform attach = null)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        var cfg = Get(skillId);
        VfxFaction faction = isAllyCaster ? VfxFaction.Ally : VfxFaction.Enemy;

        GameObject prefab = GetSkillVfxPrefab(skillId);
        if (prefab != null)
        {
            if (BattleVFXSystem.Instance != null)
                BattleVFXSystem.Instance.PlayWorldPrefab(prefab, pos, 2.5f, facingDir);
            else
            {
                GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
                if (facingDir < 0)
                {
                    var s = go.transform.localScale;
                    s.x = -Mathf.Abs(s.x);
                    go.transform.localScale = s;
                }
                Object.Destroy(go, 2.5f);
            }
            return;
        }

        if (BattleVFXSystem.Instance == null)
        {
            Debug.LogError($"[SkillRegistry] 无 BattleVFXSystem，技能特效跳过: {skillId}");
            return;
        }

        if (cfg != null && cfg.attackKit == AttackVfxKit.None && cfg.vfxPrefab == null)
        {
            Debug.LogError(
                $"[SkillRegistry] 技能「{skillId}」无专属 VFX，且 attackKit=None。" +
                "请拖 vfxPrefab / 放 Resources/VFX/Skills/.../{id}.prefab，或设 attackKit。" +
                "本次用 ResolveSkillVfxKit 兜底，可能不是预期效果。");
        }

        AttackVfxKit kit = SkillNaming.ResolveSkillVfxKit(cfg, skillId);
        if (kit == AttackVfxKit.Heal)
            BattleVFXSystem.Instance.PlayHeal(pos, faction);
        else
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, pos, pos, facingDir);
    }
}
