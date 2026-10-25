using UnityEngine;

/// <summary>
/// 玩家头像槽位定义（2026-10-25 主人需求：头像选择框弹窗 + 主界面名牌头像）。
/// 图源 = Resources/Art/UI/Main/头像/（主人 2026-10-25 拖入）；56 字符 guid 资源一律路径加载（AGENTS.md §2.3）。
/// 槽位号与头像选择框预制体节点「头像N」一一对应；6 号位主人预制体没摆节点、也没出图，留空。
/// 解锁规则：1 号默认解锁；其余槽位拥有对应佣兵（真源 SaveData.unlockedMercIds）即解锁。
/// </summary>
public static class AvatarDefs
{
    public const int MinSlot = 1;
    public const int MaxSlot = 8;
    /// <summary>默认（未选择时）头像槽位 = 玩家头像。</summary>
    public const int DefaultSlot = 1;

    const string Dir = "Art/UI/Main/头像/";

    /// <summary>槽位 → 头像图 Resources 相对路径。null = 该槽位无图（6 号位设计缺口）。</summary>
    static readonly string[] SpritePath =
    {
        Dir + "玩家",   // 1 玩家默认头像（主界面名牌 Icon 同款）
        Dir + "H004",  // 2 维克·钢盾（传奇剑盾）
        Dir + "H005",  // 3 米娅·小樱（普通游侠）
        Dir + "H013",  // 4 莫娜·紫晶（稀有雷法）
        Dir + "H015",  // 5 艾拉·风羽（普通游侠）
        null,          // 6 无节点无图，保留空位
        Dir + "H016",  // 7 杜娅·怒角（稀有狂战）
        Dir + "H019",  // 8 艾琳·星火（普通法师）
    };

    /// <summary>槽位 → 解锁所需佣兵 HireId。null = 默认解锁。</summary>
    static readonly string[] UnlockMercId =
    {
        null,          // 1 默认解锁
        "H004",        // 2 维克
        "H005",        // 3 米娅
        "H013",        // 4 莫娜
        "H015",        // 5 艾拉
        null,          // 6 无图
        "H016",        // 7 杜娅
        "H019",        // 8 艾琳
    };

    /// <summary>该槽位是否配了图（6 号位 false）。</summary>
    public static bool HasSprite(int slot)
    {
        if (slot < MinSlot || slot > MaxSlot) return false;
        return !string.IsNullOrEmpty(SpritePath[slot - MinSlot]);
    }

    /// <summary>该槽位当前是否已解锁（佣兵已拥有即解锁；无佣兵门槛 = 默认解锁）。</summary>
    public static bool IsUnlocked(int slot)
    {
        if (slot < MinSlot || slot > MaxSlot) return false;
        string merc = UnlockMercId[slot - MinSlot];
        if (string.IsNullOrEmpty(merc)) return true;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data != null && data.unlockedMercIds != null && data.unlockedMercIds.Contains(merc);
    }

    /// <summary>按槽位加载头像图；无图槽位返回 null（调用方 LogError，不兜底）。</summary>
    public static Sprite LoadSprite(int slot)
    {
        if (!HasSprite(slot)) return null;
        return Resources.Load<Sprite>(SpritePath[slot - MinSlot]);
    }

    /// <summary>当前存档的头像槽位（唯一出口）。存档值非法时 LogError 并回落默认 1 号。</summary>
    public static int CurrentSlot()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        int slot = data != null ? data.playerAvatar : DefaultSlot;
        if (slot < MinSlot || slot > MaxSlot || !HasSprite(slot))
        {
            Debug.LogError($"[AvatarDefs] 存档头像槽位非法：{slot}（应在 {MinSlot}..{MaxSlot} 且有图），回落默认 {DefaultSlot} 号");
            return DefaultSlot;
        }
        return slot;
    }
}
