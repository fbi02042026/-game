using UnityEngine;

/// <summary>
/// 玩家头像定义（2026-10-25 主人需求：头像选择框弹窗 + 主界面名牌头像）。
/// 【2026-11-03 主人拍板「默认是玩家」+ 弹窗改佣兵头像切换模式】头像号 1 = 玩家本人
/// （图源 Resources/UI/Icons/MercHead/player，MercPortraitSprites.GetHead("player")），
/// 2~25 = 佣兵头像 H001~H024（与竞技场对手头像袋同源同图）。
/// 「可以随意切换」的实际口径 = 弹窗只列玩家本人 + **已拥有**的佣兵头像（<see cref="DefaultSlot"/> 恒在列），
/// 数量随拥有变化，不是固定 25 格全可点；<see cref="CurrentSlot"/> 也按拥有校验，避免存档里留下未拥有头像号。
/// 旧档迁移：playerAvatar 旧语义是槽位 1~9（1=玩家图、2=H004…），存档标记 avatarMercModeV2=false 时
/// 由 <see cref="CurrentSlot"/> 按 LegacySlotToAvatar 一次性换算成新头像号并置位。
/// </summary>
public static class AvatarDefs
{
    public const int MinSlot = 1;
    public const int MaxSlot = 25;   // 1 = 玩家(player)，2~25 = 佣兵头像 H001~H024
    /// <summary>默认（未选择时）头像 = 1 号玩家本人（2026-11-03 主人拍板「默认是玩家」）。</summary>
    public const int DefaultSlot = 1;

    /// <summary>头像号 → 取图 ID：1 = "player"（玩家头像 player.png），2~25 = "H001"~"H024"。</summary>
    public static string HireIdOf(int avatar)
        => avatar <= 1 ? "player" : "H" + (avatar - 1).ToString("D3");

    /// <summary>该头像号是否有效（1~25 全有图：player.png + MercHead H001~H024）。</summary>
    public static bool HasSprite(int avatar) => avatar >= MinSlot && avatar <= MaxSlot;

    /// <summary>按头像号取头像图；取不到返回 null（调用方 LogError，不兜底）。</summary>
    public static Sprite LoadSprite(int avatar)
    {
        if (!HasSprite(avatar)) return null;
        return MercPortraitSprites.GetHead(HireIdOf(avatar));
    }

    /// <summary>旧槽位(1~9) → 新头像号：旧语义 1=玩家图 2=H004 3=H005 4=H013 5=H015 6=无图 7=H016 8=H019 9=H014。</summary>
    static readonly int[] LegacySlotToAvatar = { 1, 5, 6, 14, 16, 1, 17, 20, 15 };

    /// <summary>当前存档的头像号（唯一出口）。旧档槽位语义在此一次性迁移；非法值 LogError 回默认 1 号（玩家）。</summary>
    public static int CurrentSlot()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        int slot = data != null ? data.playerAvatar : DefaultSlot;
        if (data != null && !data.avatarMercModeV2)
        {
            // 【2026-11-03 弹窗改佣兵头像模式】旧档 playerAvatar 是槽位 1~9 语义，换算成新头像号。
            // 顺手写回存档（下次 Save() 落盘），迁移只做一次。
            int legacy = slot;
            slot = legacy >= 1 && legacy <= 9 ? LegacySlotToAvatar[legacy - 1] : legacy;
            data.playerAvatar = slot;
            data.avatarMercModeV2 = true;
            Debug.Log($"[AvatarDefs] 旧档头像槽位迁移：{legacy} 号槽 → 头像号 {slot}（一次性）");
        }
        if (slot < MinSlot || slot > MaxSlot)
        {
            Debug.LogError($"[AvatarDefs] 存档头像号非法：{slot}（应在 {MinSlot}..{MaxSlot}），回落默认 {DefaultSlot} 号");
            slot = DefaultSlot;
        }
        // 【2026-11-03 主人拍板「主界面的头像要是我拥有的佣兵」】未拥有的佣兵号回落玩家：
        // 弹窗只摆已拥有格子，存档残留未拥有号（改档/异常）时主界面不能显示没拥有的头像。
        if (slot > DefaultSlot && data != null && !data.IsMercUnlocked(HireIdOf(slot)))
        {
            Debug.LogError($"[AvatarDefs] 存档头像号 {slot}（{HireIdOf(slot)}）佣兵未拥有，回落默认 {DefaultSlot} 号（玩家）");
            slot = DefaultSlot;
            // 自愈写回：避免每次读都刷报错，也保证主界面/竞技场/角色页三处同源（下次 Save 落盘）
            data.playerAvatar = slot;
            data.avatarMercModeV2 = true;
        }
        return slot;
    }
}
