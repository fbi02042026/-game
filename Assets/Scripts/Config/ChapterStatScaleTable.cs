using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 章节属性倍率。数值与旧 GameConfig.CHAPTER_STAT_SCALE 1:1。
/// 运行时只读本表；缺表时回退同一组常量，避免未 Cook 时手感跳变。
/// </summary>
public static class ChapterStatScaleTable
{
    /// <summary>与历史数组一致：森林1.0 / 墓园1.3 / 雨林1.6 / 草原1.7 / 海岛1.4 / 洞穴2.0 / 熔岩2.4 / 冰川2.8</summary>
    public static readonly float[] Fallback =
    {
        1.0f, 1.3f, 1.6f, 1.7f, 1.4f, 2.0f, 2.4f, 2.8f
    };

    static readonly Dictionary<int, float> _byChapter = new Dictionary<int, float>();
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _byChapter.Count > 0;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _byChapter.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byChapter.Clear();

        string raw = GameTableStore.LoadText(ContentPaths.Data.ChapterStatScale);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[ChapterStatScale] 表缺失，使用与旧数组一致的 Fallback");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 2) continue;
            if (c[0] == "gameChapter" || c[0].StartsWith("#")) continue;
            if (!GameTableCsv.TryInt(c[0], out int ch) || ch <= 0) continue;
            if (!GameTableCsv.TryFloat(c[1], out float scale) || scale <= 0f) continue;
            _byChapter[ch] = scale;
            ok++;
        }

        if (ok <= 0)
            Debug.LogWarning("[ChapterStatScale] 解析 0 条，使用 Fallback");
        else
            Debug.Log($"[ChapterStatScale] 已加载 {ok} 条");
    }

    public static float Get(int gameChapter)
    {
        EnsureLoaded();
        int ch = Mathf.Max(1, gameChapter);
        if (_byChapter.TryGetValue(ch, out float scale) && scale > 0f)
            return scale;

        int idx = Mathf.Clamp(ch, 1, Fallback.Length) - 1;
        return Fallback[idx];
    }
}
