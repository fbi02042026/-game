using System.Collections.Generic;
using UnityEngine;

public static class MonsterUnlockTierTable
{
    public struct TierRow
    {
        public int clearCountMin;
        public int spriteIndexMax;
        public int stageIndexBonus;
    }

    static readonly List<TierRow> _rows = new List<TierRow>();
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.MonsterUnlockTier);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 3) continue;
            if (!GameTableCsv.TryInt(c[0], out int clearMin)) continue;
            if (!GameTableCsv.TryInt(c[1], out int maxSprite)) continue;

            _rows.Add(new TierRow
            {
                clearCountMin = clearMin,
                spriteIndexMax = maxSprite,
                stageIndexBonus = GameTableCsv.TryInt(c[2], out int bonus) ? bonus : 2
            });
        }
        _rows.Sort((a, b) => a.clearCountMin.CompareTo(b.clearCountMin));
        Debug.Log($"[MonsterUnlockTier] 已加载 {_rows.Count} 条");
    }

    public static int GetSpriteIndexMax(int clearCount)
    {
        EnsureLoaded();
        if (_rows.Count == 0)
        {
            if (clearCount >= GameConfig.TIER2_UNLOCK_CLEARS) return GameConfig.TIER2_MAX_SPRITE;
            if (clearCount >= GameConfig.TIER1_UNLOCK_CLEARS) return GameConfig.TIER1_MAX_SPRITE;
            return GameConfig.TIER0_MAX_SPRITE;
        }

        int max = _rows[0].spriteIndexMax;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (clearCount >= _rows[i].clearCountMin)
                max = _rows[i].spriteIndexMax;
        }
        return max;
    }

    public static int GetStageIndexBonus()
    {
        EnsureLoaded();
        if (_rows.Count == 0) return 2;
        return _rows[0].stageIndexBonus > 0 ? _rows[0].stageIndexBonus : 2;
    }
}
