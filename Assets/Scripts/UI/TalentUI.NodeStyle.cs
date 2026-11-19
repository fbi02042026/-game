using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 天赋左列节点的文案工具（2026-11-19）。
/// <para>排版真源 = <c>TalentUI.prefab</c> 的 LeftNodeTemplate（【2026-11-19 主人拍板】排版直接改预制体、
/// 方便主人在 Inspector 里继续微调；NameText 上排小字 / EffectText 下排大数值 / Icon 放大均已落进预制体）。
/// 本文件不再做运行时排版覆盖，只保留「数值角标」文案提取，供 RefreshLeft 使用。</para>
/// </summary>
public partial class TalentUI
{
    /// <summary>角标连属性名一起显示（「生命+15」/「攻击+4%」）；取不到原样返回。【2026-11-19 主人拍板】</summary>
    static string ValueBadgeOf(string display)
    {
        if (string.IsNullOrEmpty(display)) return "";
        // 参考图对比卡写「主角攻击+9」这种「属性名+数值」，角标跟随同一口径：整句显示，
        // 只把配表 display 里的空格去掉（「生命 +15」→「生命+15」）让角标紧凑，% 号保留。
        return display.Replace(" ", "");
    }
}
