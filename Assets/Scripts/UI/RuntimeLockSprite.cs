using UnityEngine;

/// <summary>
/// 运行时生成的「锁定」图标（像素风）。
/// 工程里目前没有现成的锁图资源，直接用 Image 且 sprite 为空会渲染成白块，
/// 所以这里用代码画一张 32×32 的挂锁。
/// 美术后续补了真图，只要放到 Resources/UI/Icons/LockIcon 就会自动优先使用。
/// </summary>
public static class RuntimeLockSprite
{
    /// <summary>美术真图的覆盖路径（Resources 下）。</summary>
    const string ResPath = "UI/Icons/LockIcon";

    static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached != null) return _cached;

        var external = Resources.Load<Sprite>(ResPath);
        if (external != null)
        {
            _cached = external;
            return _cached;
        }

        _cached = Build();
        return _cached;
    }

    static Sprite Build()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;      // 像素风，不要插值糊掉
        tex.wrapMode = TextureWrapMode.Clamp;

        var clear = new Color32(0, 0, 0, 0);
        var body = new Color32(236, 228, 198, 255);   // 锁体：浅金
        var edge = new Color32(120, 108, 84, 255);    // 描边：暗金
        var hole = new Color32(70, 62, 48, 255);      // 钥匙孔

        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        // 锁体：x[5..26] y[5..21]
        FillRect(px, n, 5, 5, 26, 21, body);
        // 锁体描边一圈
        OutlineRect(px, n, 5, 5, 26, 21, edge);

        // 锁梁 U 形：左右竖 + 顶部横
        FillRect(px, n, 9, 22, 11, 28, body);
        FillRect(px, n, 20, 22, 22, 28, body);
        FillRect(px, n, 9, 26, 22, 28, body);
        OutlineVBars(px, n, edge);

        // 钥匙孔：圆 + 下方竖条
        FillCircle(px, n, 16, 15, 2, hole);
        FillRect(px, n, 15, 11, 17, 15, hole);

        tex.SetPixels32(px);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static void FillRect(Color32[] px, int n, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
        {
            if (y < 0 || y >= n) continue;
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= n) continue;
                px[y * n + x] = c;
            }
        }
    }

    static void OutlineRect(Color32[] px, int n, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int x = x0; x <= x1; x++)
        {
            SetPx(px, n, x, y0, c);
            SetPx(px, n, x, y1, c);
        }
        for (int y = y0; y <= y1; y++)
        {
            SetPx(px, n, x0, y, c);
            SetPx(px, n, x1, y, c);
        }
    }

    /// <summary>给 U 形锁梁补一圈描边，只在竖向段和顶横外侧勾边。</summary>
    static void OutlineVBars(Color32[] px, int n, Color32 c)
    {
        for (int y = 22; y <= 28; y++)
        {
            SetPx(px, n, 8, y, c);
            SetPx(px, n, 12, y, c);
            SetPx(px, n, 19, y, c);
            SetPx(px, n, 23, y, c);
        }
        for (int x = 9; x <= 22; x++)
        {
            SetPx(px, n, x, 29, c);
        }
    }

    static void FillCircle(Color32[] px, int n, int cx, int cy, int r, Color32 c)
    {
        int r2 = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= n) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= n) continue;
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= r2) px[y * n + x] = c;
            }
        }
    }

    static void SetPx(Color32[] px, int n, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= n || y >= n) return;
        px[y * n + x] = c;
    }
}
