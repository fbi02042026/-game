using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物贴图不透明像素包围盒中心（相对整图 0~1，原点左下）。
/// 画布多为 32×32 透明填充，不能直接用整图中心当受击点。
/// </summary>
public static class MonsterSpriteOpaqueTable
{
    public struct Entry
    {
        public float CenterNX;
        public float CenterNY;
        public float BoxNW;
        public float BoxNH;
    }

    static Dictionary<string, Entry> _map;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _map = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _map = new Dictionary<string, Entry>();
        string raw = GameTableStore.LoadText(ContentPaths.Data.MonsterSpriteOpaque);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MonsterSpriteOpaque] 未找到表，受击点回退 SpriteRenderer.bounds");
            return;
        }
        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int ok = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("spriteId")) continue;
            string[] cols = line.Split(',');
            if (cols.Length < 5) continue;
            string id = cols[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!float.TryParse(cols[3], out float nx)) continue;
            if (!float.TryParse(cols[4], out float ny)) continue;
            float nw = cols.Length > 5 && float.TryParse(cols[5], out float a) ? a : 0.5f;
            float nh = cols.Length > 6 && float.TryParse(cols[6], out float b) ? b : 0.5f;
            _map[id] = new Entry { CenterNX = nx, CenterNY = ny, BoxNW = nw, BoxNH = nh };
            ok++;
        }
        Debug.Log($"[MonsterSpriteOpaque] 已加载 {ok} 条");
    }

    public static bool TryGet(string spriteId, out Entry e)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(spriteId))
        {
            e = default;
            return false;
        }
        if (_map.TryGetValue(spriteId, out e)) return true;
        // 文件名无扩展
        int slash = spriteId.LastIndexOf('/');
        string leaf = slash >= 0 ? spriteId.Substring(slash + 1) : spriteId;
        if (leaf.EndsWith(".png")) leaf = leaf.Substring(0, leaf.Length - 4);
        return _map.TryGetValue(leaf, out e);
    }

    /// <summary>
    /// 把不透明像素中心换算到 SpriteRenderer 世界坐标。
    /// pivot 按 Sprite 设置（怪物多为底边中点）。
    /// </summary>
    public static bool TryGetOpaqueCenterWorld(SpriteRenderer sr, out Vector3 world)
    {
        world = default;
        if (sr == null || sr.sprite == null) return false;
        Sprite sp = sr.sprite;
        if (!TryGet(sp.name, out Entry e)) return false;

        Rect rect = sp.rect;
        float ppu = sp.pixelsPerUnit;
        if (ppu < 0.01f) ppu = 100f;
        // 不透明中心相对 pivot 的本地偏移（sprite 空间）
        float cxPx = e.CenterNX * rect.width;
        float cyPx = e.CenterNY * rect.height;
        Vector2 pivot = sp.pivot; // 像素，相对 rect 左下
        float localX = (cxPx - pivot.x) / ppu;
        float localY = (cyPx - pivot.y) / ppu;
        world = sr.transform.TransformPoint(new Vector3(localX, localY, 0f));
        return true;
    }
}
