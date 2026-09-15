using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波次原型表：把「进场方向 / 近远编成 / 出怪间隔 / 精英额度」抽成可复用的原型。
/// 正式关由 <see cref="StageModeTable"/> 决定每波用哪个原型；缺表时
/// <see cref="WavePlanner"/> 回退到旧的均分逻辑，关卡不会开不起来。
/// </summary>
public static class WaveArchetypeTable
{
    public enum EnterMode
    {
        Right = 0,
        Bilateral,
        Around,
    }

    public class Archetype
    {
        public string id = "";
        public string name = "";
        public EnterMode enter = EnterMode.Right;
        /// <summary>本波近战只数；&lt;0 = 用「总人数 − ranged」反推。</summary>
        public int melee = -1;
        /// <summary>本波远程只数；&lt;0 = 用「总人数 − melee」反推。</summary>
        public int ranged = -1;
        /// <summary>出怪间隔（秒）；&lt;0 = 用规则包默认；0 = 同帧出齐。</summary>
        public float stagger = -1f;
        /// <summary>本波精英只数（占 count 名额，取最后 N 个槽位）。</summary>
        public int elite = 0;
        /// <summary>词缀偏好，| 分隔；空 = 全随机。</summary>
        public string affixBias = "";
        public int cap = GameConfig.WAVE_MONSTER_MAX;
        /// <summary>每波人数基准：× 强度系数 × 随机浮动后 clamp 到 [countMin, cap]。</summary>
        public int countBase = 3;
        public string telegraph = "";
        public string tacticHint = "";
        /// <summary>V3.0：本波人数下限。</summary>
        public int countMin = 2;
        /// <summary>V3.0：在模式池里被抽中的权重；0 = 不进任何池。</summary>
        public int weight = 1;
        /// <summary>V3.0：同一原型连续出现的上限（1 = 绝不连续）。</summary>
        public int maxRun = 2;

        /// <summary>是否接管本波的近/远编成（接管后才跳过 wave_slot 的奇偶规则）。</summary>
        public bool ControlsComposition => melee >= 0 || ranged >= 0;

        /// <summary>按本波实际人数折算远程只数。</summary>
        public int ResolveRanged(int count)
        {
            if (count <= 0) return 0;
            if (ranged >= 0) return Mathf.Min(ranged, count);
            if (melee >= 0) return Mathf.Clamp(count - melee, 0, count);
            return 0;
        }
    }

    static readonly Dictionary<string, Archetype> _map = new Dictionary<string, Archetype>();
    static bool _loaded;

    public static bool HasData => _loaded && _map.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _map.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.WaveArchetype);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 2) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id) || id.StartsWith("#")) continue;

            var a = new Archetype
            {
                id = id,
                name = c[1].Trim(),
                enter = ParseEnter(c.Length > 2 ? c[2] : ""),
                melee = GameTableCsv.TryInt(c, 3, -1),
                ranged = GameTableCsv.TryInt(c, 4, -1),
                stagger = GameTableCsv.TryFloat(c, 5, -1f),
                elite = GameTableCsv.TryInt(c, 6, 0),
                affixBias = c.Length > 7 ? c[7].Trim() : "",
                cap = GameTableCsv.TryInt(c, 8, GameConfig.WAVE_MONSTER_MAX),
                countBase = GameTableCsv.TryInt(c, 9, 3),
                telegraph = c.Length > 10 ? c[10].Trim() : "",
                tacticHint = c.Length > 11 ? c[11].Trim() : "",
                // V3.0 新增列一律放末尾：旧表缺列时走默认值，行为与改动前一致
                countMin = GameTableCsv.TryInt(c, 12, 2),
                weight = GameTableCsv.TryInt(c, 13, 1),
                maxRun = GameTableCsv.TryInt(c, 14, 2)
            };
            if (a.cap <= 0) a.cap = GameConfig.WAVE_MONSTER_MAX;
            if (a.countMin <= 0) a.countMin = 1;
            if (a.countMin > a.cap) a.countMin = a.cap;
            if (a.maxRun <= 0) a.maxRun = 1;
            _map[id] = a;
        }
        Debug.Log($"[WaveArchetype] 已加载 {_map.Count} 条");
    }

    static EnterMode ParseEnter(string raw)
    {
        string s = string.IsNullOrEmpty(raw) ? "" : raw.Trim().ToLowerInvariant();
        if (s == "bilateral") return EnterMode.Bilateral;
        if (s == "around") return EnterMode.Around;
        return EnterMode.Right;
    }

    public static Archetype Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        return _map.TryGetValue(id, out var v) ? v : null;
    }
}
