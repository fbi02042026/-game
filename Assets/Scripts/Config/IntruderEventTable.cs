using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 乱入事件配置表（intruder_event.csv -> .bytes）。按剧情节点挂点取事件：
/// preBattle:章 / postBoss:章 / island:岛id（80 关制：8 章 x 每章 10 关，配置不写死关号）。
/// 构造期禁读表，统一在 EnsureLoaded 懒加载；缺表 / 解析失败一律 LogError，不兜底造数据。
/// </summary>
public static class IntruderEventTable
{
    public struct Row
    {
        public string eventId;
        public string type;
        public string storyNode;
        public int chapter;
        public int islandId;
        public string triggerNode;
        public int unitCount;
        public int replaceCount;
        public string storyKey;
        public string rewardId;
        public string onceScope;
        public string squadId;
        public float startHpRatio;
    }

    static readonly List<Row> _rows = new List<Row>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.IntruderEvent);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[IntruderEvent] 缺 intruder_event 表（Data/Tables/intruder_event），乱入事件不可用");
            return;
        }

        var lines = GameTableCsv.ParseRows(raw);   // 已跳过 # 注释与空行；lines[0] = 表头
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 13)
            {
                Debug.LogError($"[IntruderEvent] 数据行 {i} 列数不足（{c.Length}/13），跳过：{string.Join(",", c)}");
                continue;
            }

            string eventId = c[0];
            string type = c[1];
            string storyNode = c[2];
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(storyNode))
            {
                Debug.LogError($"[IntruderEvent] 数据行 {i} 的 eventId/type/storyNode 有空值，跳过：{string.Join(",", c)}");
                continue;
            }
            if (!GameTableCsv.TryInt(c[6], out int unitCount)
                || !GameTableCsv.TryInt(c[7], out int replaceCount))
            {
                Debug.LogError($"[IntruderEvent] 数据行 {i} 的 unitCount/replaceCount 非法：{c[6]}/{c[7]}");
                continue;
            }

            // chapter / islandId / startHpRatio 允许留空：空串 Try* 返回 false，out 即默认 0 / 0f。
            GameTableCsv.TryInt(c[3], out int chapter);
            GameTableCsv.TryInt(c[4], out int islandId);
            GameTableCsv.TryFloat(c[12], out float startHpRatio);

            _rows.Add(new Row
            {
                eventId = eventId,
                type = type,
                storyNode = storyNode,
                chapter = chapter,
                islandId = islandId,
                triggerNode = c[5],
                unitCount = unitCount,
                replaceCount = replaceCount,
                storyKey = c[8],
                rewardId = c[9],
                onceScope = c[10],
                squadId = c[11],
                startHpRatio = startHpRatio
            });
        }
        Debug.Log($"[IntruderEvent] 已加载 {_rows.Count} 条");
    }

    /// <summary>按 storyNode 精确匹配取第一条；一个节点挂多条时用 GetByStoryNode。</summary>
    public static bool TryGetByStoryNode(string storyNode, out Row row)
    {
        var found = GetByStoryNode(storyNode);
        if (found.Count > 0)
        {
            row = found[0];
            return true;
        }
        row = default;
        return false;
    }

    /// <summary>按 storyNode 精确匹配取全部事件（同一节点可能挂多条）。</summary>
    public static List<Row> GetByStoryNode(string storyNode)
    {
        EnsureLoaded();
        var result = new List<Row>();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (string.Equals(_rows[i].storyNode, storyNode, System.StringComparison.Ordinal))
                result.Add(_rows[i]);
        }
        return result;
    }

    public static List<Row> GetAll()
    {
        EnsureLoaded();
        return new List<Row>(_rows);
    }

    public static bool TryGet(string eventId, out Row row)
    {
        EnsureLoaded();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (string.Equals(_rows[i].eventId, eventId, System.StringComparison.Ordinal))
            {
                row = _rows[i];
                return true;
            }
        }
        row = default;
        return false;
    }
}
