using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗调参表（combat_tuning）。把 GameConfig 里散落的战斗硬编码常量收编进表，改数值不用改代码。
/// 缺表 / key 缺失 / 构造期读表被拒时一律回退 defaultValue（= 原硬编码值），
/// 运行时行为与硬编码完全一致，可随时回退。
/// </summary>
public static class CombatTuningTable
{
    static readonly Dictionary<string, float> _byKey = new Dictionary<string, float>();
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _byKey.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw;
        try
        {
            // 大坑：构造期 / 反序列化期（MonoBehaviour 构造函数或字段初始化器）调用 Resources.Load
            // 会抛 UnityException: Load is not allowed。这里包一层，宁可回退 defaultValue 也不能抛，
            // 避免整个 MonoBehaviour 实例化失败导致场景打不开。
            raw = GameTableStore.LoadText(ContentPaths.Data.CombatTuning);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[CombatTuningTable] 表加载被拒（极可能在构造/反序列化期调用）: "
                + ContentPaths.Data.CombatTuning + " -> " + e.Message + "（回退 GameConfig 默认值）");
            return;
        }

        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[CombatTuningTable] 表加载失败或缺失: Resources/" + ContentPaths.Data.CombatTuning
                + " （空或缺失），回退 GameConfig 默认值。");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        if (rows.Count < 2) return;

        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 2) continue;
            string key = c[0].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            // 跳过表内注释行（# 开头）；否则注释里若出现英文逗号会被误当成一条 key
            if (key.StartsWith("#")) continue;
            float val = GameTableCsv.TryFloat(c[1], out float v) ? v : 0f;
            _byKey[key] = val;
        }

        if (_byKey.Count <= 0)
            Debug.LogWarning("[CombatTuningTable] 表解析 0 条: Resources/" + ContentPaths.Data.CombatTuning
                + "，回退 GameConfig 默认值。");
        else
            Debug.Log($"[CombatTuningTable] 已加载 {_byKey.Count} 条");
    }

    /// <summary>
    /// 取调参值；key 缺失 / 表缺失 / 加载异常都返回 defaultValue（= 原硬编码值），
    /// 保证表在不在、有没有那条 key，运行时行为完全一样（随时可回退）。
    /// 内部 try/catch：反序列化/构造期读表可能抛 UnityException: Load is not allowed，
    /// 宁可返回 defaultValue 也不能抛。
    /// </summary>
    public static float Get(string key, float defaultValue)
    {
        try
        {
            EnsureLoaded();
            if (_byKey.TryGetValue(key, out float v))
                return v;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[CombatTuningTable] 读取 key=" + key + " 异常，回退默认值: " + e.Message);
        }
        return defaultValue;
    }

    /// <summary>取整型调参值（表里存的是数值，四舍五入）。</summary>
    public static int GetInt(string key, int defaultValue)
    {
        return Mathf.RoundToInt(Get(key, defaultValue));
    }

    /// <summary>取布尔调参值（表里存 1/0，&gt;0.5 视为 true）。</summary>
    public static bool GetBool(string key, bool defaultValue)
    {
        return Get(key, defaultValue ? 1f : 0f) > 0.5f;
    }
}
