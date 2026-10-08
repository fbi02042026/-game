using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 章节属性倍率（**章基准**）。最终倍率 = 本表值 × <c>StageStatScaleTable</c>（章内关卡爬坡）。
/// 运行时只读本表；缺表时回退同一组常量，避免未 Cook 时手感跳变。
/// <para>2026-10-05 主人拍板重排：主线走 1→2→5→6→7→8，要求「每章都上升，有的升得高点有的低点」。
/// 旧值 1.0/1.3/1.6/1.7/1.4/2.0/2.4/2.8 是按<b>编号顺序</b>排的，主线跳号（3/4 是支线）后
/// 实际吃到 1.0→1.3→1.4→2.0→2.4→2.8，涨幅 +30%/+7.7%/+43%/+20%/+17% —— 第 2→5 章几乎没变难。
/// 新值主线涨幅 +25.0%/+12.7%/+21.0%/+14.7%/+23.3%，有高有低、全部上升。</para>
/// ⚠ 改这张表必须同时改 <c>Assets/Data/Source/Tables/chapter_stat_scale.csv</c> 并重新 Cook。
/// </summary>
public static class ChapterStatScaleTable
{
    /// <summary>与表 1:1：森林0.88 / 墓园1.10 / 雨林1.55(支线) / 草原1.72(支线) / 海岛1.24 / 洞穴1.50 / 熔岩1.72 / 冰川2.12</summary>
    public static readonly float[] Fallback =
    {
        0.88f, 1.10f, 1.55f, 1.72f, 1.24f, 1.50f, 1.72f, 2.12f
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
