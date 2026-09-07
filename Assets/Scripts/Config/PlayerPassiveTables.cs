using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家职业被动表（player_passives）。</summary>
public static class PlayerPassiveTables
{
    public class Row
    {
        public string Id;
        public string JobConfigId;
        public string Name;
        public string TriggerType;
        public string Desc;
        public string ValueRaw;
        public string CooldownRaw;
        public string Note;
        public float ValueNumber;
        public float CooldownSeconds;
    }

    static readonly Dictionary<string, Row> _byId = new Dictionary<string, Row>();
    static readonly Dictionary<PlayerJobId, List<Row>> _byJob = new Dictionary<PlayerJobId, List<Row>>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.PlayerPassives);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[PlayerPassive] 未找到 player_passives 表");
            return;
        }
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 7) continue;
            var row = new Row
            {
                Id = c[0].Trim(),
                JobConfigId = c[1].Trim(),
                Name = c[3].Trim(),
                TriggerType = c[4].Trim(),
                Desc = c[5].Trim(),
                ValueRaw = c[6].Trim(),
                CooldownRaw = c.Length > 7 ? c[7].Trim() : "",
                Note = c.Length > 8 ? c[8].Trim() : ""
            };
            row.ValueNumber = ParseLeadingNumber(row.ValueRaw);
            row.CooldownSeconds = ParseLeadingNumber(row.CooldownRaw);
            _byId[row.Id] = row;
            if (TryMapJob(row.JobConfigId, out PlayerJobId job))
            {
                if (!_byJob.TryGetValue(job, out var list))
                {
                    list = new List<Row>();
                    _byJob[job] = list;
                }
                list.Add(row);
            }
        }
    }

    public static bool TryGet(string id, out Row row)
    {
        EnsureLoaded();
        return _byId.TryGetValue(id, out row);
    }

    /// <summary>本轮启用的常驻被动 ID（每职业一个）。</summary>
    public static string GetPrimaryPassiveId(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.SwordShield: return "PS001";
            case PlayerJobId.Heavy: return "PS004";
            case PlayerJobId.Berserker: return "PS005";
            case PlayerJobId.Ranger: return "PS008";
            case PlayerJobId.Mage: return "PS009";
            case PlayerJobId.Priest: return "PS012";
            default: return null;
        }
    }

    public static bool TryGetPrimary(PlayerJobId job, out Row row)
    {
        string id = GetPrimaryPassiveId(job);
        return TryGet(id, out row);
    }

    static float ParseLeadingNumber(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        int end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-'))
            end++;
        if (end <= 0) return 0f;
        GameTableCsv.TryFloat(s.Substring(0, end), out float v);
        return v;
    }

    static bool TryMapJob(string configId, out PlayerJobId job)
    {
        switch (configId)
        {
            case "P001": job = PlayerJobId.SwordShield; return true;
            case "P002": job = PlayerJobId.Heavy; return true;
            case "P003": job = PlayerJobId.Berserker; return true;
            case "P004": job = PlayerJobId.Ranger; return true;
            case "P005": job = PlayerJobId.Mage; return true;
            case "P006": job = PlayerJobId.Priest; return true;
            default: job = PlayerJobId.SwordShield; return false;
        }
    }
}
