using UnityEngine;

/// <summary>
/// 运行时生成的基础图形：空心圆环（技能就绪光边）/ 实心圆盘（无头像占位）。
///
/// 为什么不是直接用 Image：没有 sprite 的 Image 会渲染成一块纯色方块，
/// 光边就会变成「金色方块盖在头像上」。所以这里用代码画一张白色蒙版，
/// 颜色交给 Image.color 控制（策划改色不用重画图）。
///
/// 美术补了真图之后，只要放到对应 Resources 路径就会自动优先命中：
///   光边    -> Resources/UI/Common/SkillGlowRing
///   占位底  -> Resources/UI/Common/PortraitPlaceholder
/// </summary>
public static class RuntimeUiArt
{
    const string RingResPath = "UI/Common/SkillGlowRing";
    const string DiscResPath = "UI/Common/PortraitPlaceholder";

    static Sprite _ring;
    static Sprite _disc;

    /// <summary>空心圆环（像素风、不插值）。</summary>
    public static Sprite Ring()
    {
        if (_ring != null) return _ring;
        var external = Resources.Load<Sprite>(RingResPath);
        if (external != null) { _ring = external; return _ring; }
        _ring = BuildRing(64, 5);
        return _ring;
    }

    /// <summary>实心圆盘：无头像时垫在框里，不至于露出空白。</summary>
    public static Sprite Disc()
    {
        if (_disc != null) return _disc;
        var external = Resources.Load<Sprite>(DiscResPath);
        if (external != null) { _disc = external; return _disc; }
        _disc = BuildDisc(64);
        return _disc;
    }

    static Sprite BuildRing(int n, int thickness)
    {
        var tex = NewTexture(n);
        var px = NewClear(n);
        float c = (n - 1) * 0.5f;
        float outer = c;
        float inner = c - thickness;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= outer && d >= inner)
                    px[y * n + x] = Color.white;
            }
        }
        return Finish(tex, px, n);
    }

    static Sprite BuildDisc(int n)
    {
        var tex = NewTexture(n);
        var px = NewClear(n);
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - c;
                float dy = y - c;
                if (dx * dx + dy * dy <= c * c)
                    px[y * n + x] = Color.white;
            }
        }
        return Finish(tex, px, n);
    }

    static Texture2D NewTexture(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;   // 像素风，不要插值糊掉
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Color32[] NewClear(int n)
    {
        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        return px;
    }

    static Sprite Finish(Texture2D tex, Color32[] px, int n)
    {
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
