using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 叙事 V2.0：缺图占位。
/// 美术没到位时程序化生成一张带 ID 文字的占位图，避免剧情里出现空白槽位、
/// 也避免"看起来像做完了其实图是空的"。
///
/// 用法：凡是拿不到 Sprite 的地方，退回这里拿一张。
/// 发版前把 <see cref="Enabled"/> 设成 false，缺图就会退回原来的「干脆不显示」。
/// </summary>
public static class PlaceholderArt
{
    /// <summary>总开关。默认开：开发期要能一眼看见哪些图还没做。</summary>
    public static bool Enabled = true;

    public enum Kind
    {
        /// <summary>剧情立绘，1171 × 1345（与真图同尺寸，保证布局不跳变）。</summary>
        Portrait,
        /// <summary>剧情背景，576 × 1024。</summary>
        Background,
        /// <summary>游戏内道具图标，128 × 128。</summary>
        Icon,
    }

    static readonly Color32 CBase = new Color32(46, 46, 58, 255);
    static readonly Color32 CStripe = new Color32(58, 58, 78, 255);
    static readonly Color32 CEdge = new Color32(232, 196, 106, 255);
    static readonly Color32 CShape = new Color32(78, 78, 102, 255);
    static readonly Color32 CText = new Color32(240, 240, 248, 255);
    static readonly Color32 CDim = new Color32(150, 150, 172, 255);

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Portrait(string id) => Build(Kind.Portrait, 1171, 1345, id);
    public static Sprite Background(string id) => Build(Kind.Background, 576, 1024, id);
    public static Sprite Icon(string id) => Build(Kind.Icon, 128, 128, id);

    /// <summary>占位图的中文标注（给 UI / 排查清单用）。未知 ID 直接回传 ID 本身。</summary>
    public static string DisplayName(string id)
    {
        switch (id)
        {
            case "melissa": return "梅莉莎";
            case "riftwill": return "裂缝意志";
            case "innkeeper": return "老板娘";
            case "laodun": return "劳顿";
            case "player": return "你";
            default: return id;
        }
    }

    public static Sprite Build(Kind kind, int w, int h, string id)
    {
        if (!Enabled) return null;
        if (w < 8 || h < 8) return null;

        string key = (int)kind + "_" + w + "x" + h + "_" + id;
        Sprite cached;
        if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "PH_" + kind + "_" + id;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        var buf = new Color32[w * h];
        int border = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(w, h) * 0.012f));

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                bool onEdge = x < border || y < border || x >= w - border || y >= h - border;
                bool stripe = ((x + y) / 12) % 2 == 0;
                Color32 c = onEdge ? CEdge : (stripe ? CStripe : CBase);

                if (!onEdge && InShape(kind, x, y, w, h))
                    c = CShape;

                buf[row + x] = c;
            }
        }

        DrawText(buf, w, h, id, w / 2, Mathf.RoundToInt(h * 0.62f), GlyphScale(h), CText);
        string sub = kind == Kind.Portrait ? "MISSING PORTRAIT"
            : kind == Kind.Background ? "MISSING BACKGROUND" : "MISSING ICON";
        DrawText(buf, w, h, sub, w / 2, Mathf.RoundToInt(h * 0.62f) + GlyphScale(h) * 10,
            Mathf.Max(1, GlyphScale(h) / 2), CDim);
        DrawText(buf, w, h, w + "X" + h, w / 2, h - border - GlyphScale(h) * 5,
            Mathf.Max(1, GlyphScale(h) / 2), CDim);

        tex.SetPixels32(buf);
        tex.Apply(false, true);

        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sp.name = tex.name;
        Cache[key] = sp;
        return sp;
    }

    static int GlyphScale(int h)
    {
        if (h >= 1000) return 7;
        if (h >= 512) return 5;
        return 2;
    }

    /// <summary>剪影：让人一眼看出这张图"本该是什么"。</summary>
    static bool InShape(Kind kind, int x, int y, int w, int h)
    {
        if (kind == Kind.Portrait)
        {
            // 头（圆）+ 肩（梯形）——立绘是半身像，只画到胸
            float fx = x / (float)w;
            float fy = y / (float)h;
            float dx = (fx - 0.5f) * 2.35f;
            float dy = (fy - 0.26f) * 1.55f;
            if (dx * dx + dy * dy < 0.052f) return true;
            if (fy > 0.44f && fy < 0.95f)
            {
                float half = 0.10f + (fy - 0.44f) * 0.62f;
                if (half > 0.30f) half = 0.30f;
                if (fx > 0.5f - half && fx < 0.5f + half) return true;
            }
            return false;
        }
        if (kind == Kind.Background)
        {
            // 两座山 + 一个太阳
            float fx = x / (float)w;
            float fy = y / (float)h;
            float sdx = (fx - 0.72f) * 1.0f;
            float sdy = (fy - 0.26f) * 1.75f;
            if (sdx * sdx + sdy * sdy < 0.010f) return true;
            float ground = 0.78f;
            float peak1 = ground - Mathf.Abs(fx - 0.30f) * 0.95f;
            float peak2 = ground - Mathf.Abs(fx - 0.62f) * 1.35f;
            if (fy > ground) return true;
            if (fy > peak1) return true;
            if (fy > peak2) return true;
            return false;
        }
        // 图标：中间一个方框
        float ix = Mathf.Abs(x - w * 0.5f);
        float iy = Mathf.Abs(y - h * 0.5f);
        float edge = w * 0.22f;
        return ix < edge && iy < edge && (ix > edge * 0.55f || iy > edge * 0.55f);
    }

    // ---------- 5×7 点阵字 ----------

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'A', new[]{ ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'B', new[]{ "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." } },
        { 'C', new[]{ ".####", "#....", "#....", "#....", "#....", "#....", ".####" } },
        { 'D', new[]{ "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." } },
        { 'E', new[]{ "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
        { 'F', new[]{ "#####", "#....", "#....", "####.", "#....", "#....", "#...." } },
        { 'G', new[]{ ".###.", "#...#", "#....", "#..##", "#...#", "#...#", ".###." } },
        { 'H', new[]{ "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'I', new[]{ ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." } },
        { 'J', new[]{ "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." } },
        { 'K', new[]{ "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" } },
        { 'L', new[]{ "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'M', new[]{ "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" } },
        { 'N', new[]{ "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" } },
        { 'O', new[]{ ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'P', new[]{ "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
        { 'Q', new[]{ ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" } },
        { 'R', new[]{ "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
        { 'S', new[]{ ".####", "#....", "#....", ".###.", "....#", "....#", "####." } },
        { 'T', new[]{ "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." } },
        { 'U', new[]{ "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'V', new[]{ "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." } },
        { 'W', new[]{ "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" } },
        { 'X', new[]{ "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
        { 'Y', new[]{ "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." } },
        { 'Z', new[]{ "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" } },
        { '0', new[]{ ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." } },
        { '1', new[]{ "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." } },
        { '2', new[]{ ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" } },
        { '3', new[]{ "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." } },
        { '4', new[]{ "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." } },
        { '5', new[]{ "#####", "#....", "####.", "....#", "....#", "#...#", ".###." } },
        { '6', new[]{ "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." } },
        { '7', new[]{ "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." } },
        { '8', new[]{ ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." } },
        { '9', new[]{ ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." } },
        { '-', new[]{ ".....", ".....", ".....", "#####", ".....", ".....", "....." } },
        { '_', new[]{ ".....", ".....", ".....", ".....", ".....", ".....", "#####" } },
        { '.', new[]{ ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." } },
        { ':', new[]{ ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." } },
        { '/', new[]{ "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." } },
        { '?', new[]{ ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." } },
        { ' ', new[]{ ".....", ".....", ".....", ".....", ".....", ".....", "....." } },
    };

    static void DrawText(Color32[] buf, int w, int h, string s, int cx, int cy, int scale, Color32 col)
    {
        if (string.IsNullOrEmpty(s) || scale < 1) return;

        s = s.ToUpperInvariant();
        int charW = 6 * scale;
        int totalW = s.Length * charW - scale;
        int startX = cx - totalW / 2;
        int startY = cy - (7 * scale) / 2;

        for (int i = 0; i < s.Length; i++)
        {
            string[] g;
            if (!Glyphs.TryGetValue(s[i], out g)) g = Glyphs['?'];

            int gx0 = startX + i * charW;
            for (int gy = 0; gy < 7; gy++)
            {
                string row = g[gy];
                for (int gx = 0; gx < 5; gx++)
                {
                    if (row[gx] != '#') continue;
                    int px0 = gx0 + gx * scale;
                    int py0 = startY + gy * scale;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        int py = py0 + dy;
                        if (py < 0 || py >= h) continue;
                        int rowBase = py * w;
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int px = px0 + dx;
                            if (px < 0 || px >= w) continue;
                            buf[rowBase + px] = col;
                        }
                    }
                }
            }
        }
    }

    static readonly HashSet<string> Reported = new HashSet<string>();

    /// <summary>开发期自查：把还没真图的 ID 打进 Console。同一个 ID 只报一次，免得刷屏。</summary>
    public static void ReportMissing(string group, string id)
    {
        if (!Enabled) return;
        if (!Reported.Add(group + "/" + id)) return;
        Debug.LogWarning("[占位图] 缺 " + group + "：" + id
            + "（" + DisplayName(id) + "）——先用占位图顶着，美术补齐后自动换真图");
    }
}
