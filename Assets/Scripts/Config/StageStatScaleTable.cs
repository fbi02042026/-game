using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 章内关卡属性倍率（2026-10-05 新增）。
/// 主人原话：「难度曲线调整一下，尽量每章都是上升的，可以有的升高点有的低点，
/// 每章里面的关卡也是如此」。
///
/// <para>拆成两张表是有意的：<c>ChapterStatScaleTable</c> 管「章与章的高低」，
/// 本表只管「一章之内 10 关怎么爬」。想调章内手感只动本表，不会碰坏章间关系。</para>
///
/// <para><b>最终倍率 = 章倍率 × 章内倍率</b>，唯一出口是
/// <c>GameConfig.GetStatScale(chapter, stageIndex0Based)</c> —— 别在别处自己乘一遍。</para>
///
/// 缺表时回退 <see cref="Fallback"/>（与表 1:1），避免未 Cook 时手感跳变。
/// </summary>
public static class StageStatScaleTable
{
    /// <summary>与表 1:1：第 1 关 1.00 → 第 10 关（Boss）1.32。</summary>
    public static readonly float[] Fallback =
    {
        1.00f, 1.03f, 1.06f, 1.10f, 1.14f, 1.18f, 1.22f, 1.25f, 1.27f, 1.32f
    };

    static readonly Dictionary<int, float> _byStage = new Dictionary<int, float>();
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _byStage.Count > 0;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _byStage.Clear();
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byStage.Clear();

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageStatScale);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[StageStatScale] 表缺失，使用与表一致的 Fallback");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 2) continue;
            if (c[0] == "stageIndex" || c[0].StartsWith("#")) continue;
            if (!GameTableCsv.TryInt(c[0], out int st) || st <= 0) continue;
            if (!GameTableCsv.TryFloat(c[1], out float scale) || scale <= 0f) continue;
            _byStage[st] = scale;
            ok++;
        }

        if (ok <= 0)
            Debug.LogWarning("[StageStatScale] 解析 0 条，使用 Fallback");
        else
            Debug.Log($"[StageStatScale] 已加载 {ok} 条");
    }

    /// <summary>章内第几关（<b>1 起</b>，1~10）的属性倍率。越界自动夹到首尾，不报错。</summary>
    public static float Get(int stageIndex1Based)
    {
        EnsureLoaded();
        int st = Mathf.Max(1, stageIndex1Based);
        if (_byStage.TryGetValue(st, out float scale) && scale > 0f)
            return scale;

        int idx = Mathf.Clamp(st, 1, Fallback.Length) - 1;
        return Fallback[idx];
    }

    /// <summary>章内第几关（<b>0 起</b>，0~9）的属性倍率；<c>stageIndex0Based &lt; 0</c> 当作第 1 关。</summary>
    public static float Get0Based(int stageIndex0Based)
    {
        return Get(stageIndex0Based + 1);
    }
}
