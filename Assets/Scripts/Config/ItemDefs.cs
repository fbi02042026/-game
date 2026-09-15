using System.Collections.Generic;
using UnityEngine;

/// <summary>道具大类。</summary>
public enum ItemType
{
    /// <summary>消耗品：可用掉。</summary>
    Consumable = 0,
    /// <summary>材料：本身不能用，靠合成/兑换消耗。</summary>
    Material = 1,
    /// <summary>任务道具：不可丢弃。</summary>
    Quest = 2,
}

/// <summary>道具作用目标。决定「使用」在什么时候可用、作用于谁。</summary>
public enum ItemTarget
{
    Self = 0,
    Ally = 1,
    Enemy = 2,
}

/// <summary>道具静态定义，来自 <c>Assets/Data/Source/Tables/item_defs.csv</c>。</summary>
public class ItemDef
{
    public string id = "";
    public string name = "";
    /// <summary>图标 Resources 路径（相对 Resources，不带扩展名）。</summary>
    public string icon = "";
    public ItemType type = ItemType.Material;
    /// <summary>堆叠上限。1 = 不可堆叠。</summary>
    public int stackMax = 1;
    /// <summary>战斗中是否允许使用。</summary>
    public bool canUseInBattle;
    /// <summary>是否允许丢弃（任务道具应为 false）。</summary>
    public bool canDrop = true;
    /// <summary>
    /// 使用效果串，如 <c>heal_hp:30</c> / <c>buff_atk:20:15</c>。
    /// 目前只定义格式、不实现效果——等具体道具定下来再在 ItemUseService 里接。
    /// </summary>
    public string useEffect = "";
    public ItemTarget target = ItemTarget.Self;
    public string desc = "";
    public int rarity;

    public bool IsUsable => type == ItemType.Consumable && !string.IsNullOrEmpty(useEffect);
}

/// <summary>背包里的一份道具（含堆叠数量）。</summary>
public class ItemInstance
{
    public string defId = "";
    public int count = 1;

    public ItemDef Def => ItemDefs.Get(defId);
    public string Name => Def != null ? Def.name : defId;

    public ItemInstance() { }
    public ItemInstance(string defId, int count = 1)
    {
        this.defId = defId;
        this.count = count;
    }
}

/// <summary>
/// 道具表。2026-09-15：背包 12 格已改为**只装道具**（装备直接穿、替换即分解），
/// 这里先把数据层与增删接口备好，具体道具内容等设计定稿后再填表。
/// </summary>
public static class ItemDefs
{
    static readonly Dictionary<string, ItemDef> _byId = new Dictionary<string, ItemDef>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.ItemDefs);
        if (string.IsNullOrEmpty(raw)) return;

        // 列顺序见 Assets/Data/Source/Tables/item_defs.csv 的表头
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c == null || c.Length < 2) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (id[0] == '#' || id.StartsWith("//")) continue;   // 表头前的说明注释
            if (id == "id") continue;                            // 表头

            var def = new ItemDef
            {
                id = id,
                name = Col(c, 1),
                icon = Col(c, 2),
                type = ParseType(Col(c, 3)),
                stackMax = ColInt(c, 4, 1),
                canUseInBattle = ColInt(c, 5, 0) != 0,
                canDrop = ColInt(c, 6, 1) != 0,
                useEffect = Col(c, 7),
                target = ParseTarget(Col(c, 8)),
                desc = Col(c, 9),
                rarity = ColInt(c, 10, 0),
            };
            if (def.stackMax <= 0) def.stackMax = 1;
            _byId[id] = def;
        }
    }

    public static ItemDef Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        _byId.TryGetValue(id, out var def);
        return def;
    }

    /// <summary>取道具图标；整图未切 Sprite 时退回 Texture 现搓一个。</summary>
    public static Sprite LoadIcon(string defId)
    {
        var def = Get(defId);
        if (def == null || string.IsNullOrEmpty(def.icon)) return null;
        var sp = Resources.Load<Sprite>(def.icon);
        if (sp != null) return sp;
        var tex = Resources.Load<Texture2D>(def.icon);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static string Col(string[] c, int i) => c != null && i < c.Length ? c[i].Trim() : "";

    static int ColInt(string[] c, int i, int fallback) =>
        GameTableCsv.TryInt(Col(c, i), out int v) ? v : fallback;

    static ItemType ParseType(string s)
    {
        if (string.IsNullOrEmpty(s)) return ItemType.Material;
        switch (s.Trim().ToLower())
        {
            case "consumable": return ItemType.Consumable;
            case "quest": return ItemType.Quest;
            default: return ItemType.Material;
        }
    }

    static ItemTarget ParseTarget(string s)
    {
        if (string.IsNullOrEmpty(s)) return ItemTarget.Self;
        switch (s.Trim().ToLower())
        {
            case "ally": return ItemTarget.Ally;
            case "enemy": return ItemTarget.Enemy;
            default: return ItemTarget.Self;
        }
    }
}
