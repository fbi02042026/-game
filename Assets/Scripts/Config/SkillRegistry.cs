using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能表：Ally=玩家+佣兵共用；Monster=敌人主动技
/// </summary>
public class SkillRegistry : Singleton<SkillRegistry>, ICombatBoundSingleton
{
    private Dictionary<string, SkillConfig> _dict = new Dictionary<string, SkillConfig>();
    private Dictionary<string, SkillConfig> _runtimeMerc = new Dictionary<string, SkillConfig>();

    /// <summary>仅文档/旧存档对照。战斗路径禁止再静默回退此 id。</summary>
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
        if (string.IsNullOrEmpty(selected))
        {
            Debug.LogError("[SkillRegistry] 未选择玩家技能，拒绝映射默认 ally_heal");
            return null;
        }
        var def = PlayerSkillDefs.GetById(selected);
        if (def == null)
        {
            Debug.LogError("[SkillRegistry] 未知玩家技能 id，拒绝释放: " + selected);
            return null;
        }
        if (string.IsNullOrEmpty(def.allyConfigId) || Get(def.allyConfigId) == null)
        {
            Debug.LogError("[SkillRegistry] 玩家技能缺 Ally 配置，拒绝释放: "
                + selected + " → " + def.allyConfigId);
            return null;
        }
        return def.allyConfigId;
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
        if (data != null && !string.IsNullOrEmpty(data.passiveSkillId)
            && (Get(data.passiveSkillId) != null || MercSkillTable.IsPassive(data.passiveSkillId)))
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
    /// 1) 专属 prefab（配置拖入 或 VFX/Skills/.../{id}）——远程施法者跳过近战 slash 专属，改走弹道
    /// 2) 否则 SkillNaming.ResolveSkillVfxKit → 共用套；Bow/Orb 从 from→to 飞行
    /// </summary>
    public void PlaySkillVfx(string skillId, Vector3 pos, bool isAllyCaster, int facingDir = 1, Transform attach = null)
    {
        PlaySkillVfx(skillId, pos, pos, isAllyCaster, facingDir, attach, AttackVfxKit.None);
    }

    /// <param name="fromPos">弹道起点（施法者开火点）</param>
    /// <param name="toPos">弹道/命中点</param>
    /// <param name="casterBasicKit">施法者普攻套；远程用来纠正物攻技能误配的刀光</param>
    public void PlaySkillVfx(
        string skillId,
        Vector3 fromPos,
        Vector3 toPos,
        bool isAllyCaster,
        int facingDir = 1,
        Transform attach = null,
        AttackVfxKit casterBasicKit = AttackVfxKit.None)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        var cfg = Get(skillId);
        VfxFaction faction = isAllyCaster ? VfxFaction.Ally : VfxFaction.Enemy;
        bool rangedCaster = casterBasicKit == AttackVfxKit.Bow || casterBasicKit == AttackVfxKit.Orb;

        AttackVfxKit kit = SkillNaming.ResolveSkillVfxKit(cfg, skillId, casterBasicKit);

        GameObject prefab = GetSkillVfxPrefab(skillId);
        // 远程单位放物攻技能时：专属多为近战 slash（挂在自身），跳过改走弓/法球弹道
        if (prefab != null && !(rangedCaster && kit != AttackVfxKit.Heal))
        {
            float life = 2.5f;
            if (skillId != null && skillId.IndexOf("shield", System.StringComparison.OrdinalIgnoreCase) >= 0)
                life = 6.2f;
            else if (cfg != null && cfg.duration > 0.5f)
                life = Mathf.Max(2.5f, cfg.duration + 0.2f);

            if (BattleVFXSystem.Instance != null)
                BattleVFXSystem.Instance.PlaySkillPrefab(prefab, toPos, facingDir, life, attach);
            else
            {
                GameObject go = Object.Instantiate(prefab, toPos, prefab.transform.rotation);
                if (attach != null)
                {
                    go.transform.SetParent(attach, true);
                    go.transform.position = toPos;
                }
                // Merc 手做特效禁止改 localScale X/Y；其它技能可按朝向翻 X
                bool merc = ResolveSkillVfxFolder(skillId) == "Merc";
                if (!merc && facingDir < 0)
                {
                    var s = go.transform.localScale;
                    s.x = -Mathf.Abs(s.x);
                    go.transform.localScale = s;
                }
                Object.Destroy(go, life);
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

        if (kit == AttackVfxKit.Heal)
        {
            BattleVFXSystem.Instance.PlayHeal(toPos, faction);
            return;
        }

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, fromPos, toPos, facingDir, attach);
            return;
        }

        BattleVFXSystem.Instance.PlayAttackKit(kit, faction, fromPos, toPos, facingDir, attach);
    }
}
