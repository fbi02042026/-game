using UnityEngine;

/// <summary>
/// 角色页箭头气泡指路（2026-11-25 主人需求）：
/// 「能解锁宠物」或「有天赋可点」时，在对应图标旁弹一条箭头气泡；
/// 两条同时满足也只弹一条（宠物优先），不会连着弹两个。
///
/// 口径：
///  · 唯一调用点 = TownHubController 的 0.25s 节流轮询，只在角色页（MainNavTab.Character）判定；
///    _warmupDone 闸门顺带避开进镇预热点亮（预热也会走 CharacterUI.ShowPage，但那时不弹、不记标记）。
///  · 频率【2026-11-25 主人「1天只弹1次 只弹3天」】：按自然日算，同一天最多弹一条，
///    累计弹满 3 天（3 次）后永久不再弹；记账走存档（`SaveData.petTalentHintDayKey/Days`，
///    随云存档换机一致、清档自动归零），日期取 DayKeyUtil.TodayKey()（唯一日期出口）。
///    条件不满足 / 目标不可用 / 横幅被占用 → 这次不弹、不记账，等下次轮询。
///  · 视觉全复用现成件（照 MercStarReadyHint）：底框 = 像素风空白对话框指针
///    （BattleManager.DraftHintBubble）+ TutorialHintUI 软引导，不新增 UI / 图片 / 预制体 / 排版。
/// </summary>
public static class PetTalentHint
{
    const float HoldSeconds = 3f;
    public const string PetText = "能解锁宠物";
    public const string TalentText = "能点天赋";

    /// <summary>【2026-11-25 主人「1天只弹1次 只弹3天」】累计最多弹 3 天，之后永久不再弹。</summary>
    public const int MaxDays = 3;

    /// <summary>本进程已弹过的日期（省得每 0.25s 轮询都读存档；跨天自动失效）。</summary>
    static string _shownDay;

    /// <summary>今天还能不能弹：累计没满 3 天，且今天还没弹过（日期真源 = DayKeyUtil.TodayKey）。</summary>
    static bool CanShowToday(SaveData data)
    {
        string today = DayKeyUtil.TodayKey();
        if (string.CompareOrdinal(_shownDay, today) == 0) return false;
        if (data.petTalentHintDays >= MaxDays) return false;
        return data.petTalentHintDayKey != today;
    }

    /// <summary>弹成功才记账：天数 +1、记下今天（存档字段，换机/重进都算一天一次）。</summary>
    static void RecordShown(SaveData data)
    {
        string today = DayKeyUtil.TodayKey();
        _shownDay = today;
        data.petTalentHintDays++;
        data.petTalentHintDayKey = today;
        SaveSystem.Instance.Save();
    }

    /// <summary>
    /// 尝试弹一条。true = 本次真的弹出来了（调用方不需要做别的）。
    /// 宠物条件优先；宠物这次弹不了（已弹过/入口不可用/没条件）才看天赋。
    /// </summary>
    public static bool TryShow(RectTransform petEntry, RectTransform talentEntry)
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return false;
        if (!CanShowToday(save)) return false;
        // 宠物功能引导进行中：它自己会弹气泡指路，这里让位（不写标记，引导完还能弹）
        if (PetFeatureGuide.IsRunning) return false;

        // 别的引导正占着这条横幅：这次不弹、不记标记，等下次轮询
        var existing = TutorialHintUI.Instance;
        if (existing != null && existing.IsVisible) return false;

        RectTransform target = null;
        string text = null;
        if (PetUsable(petEntry) && PetUI.HasUnlockable(save))
        {
            target = petEntry; text = PetText;
        }
        else if (Usable(talentEntry) && TalentSystem.HasAnyAffordableUnlockable(save))
        {
            target = talentEntry; text = TalentText;
        }
        if (target == null) return false;

        var bubble = BattleManager.DraftHintBubble;
        if (bubble == null)
        {
            Debug.LogError("[PetTalentHint] 取不到底框 Sprite：UI/Art/Common/像素风空白对话框指针");
            return false;
        }

        var hint = TutorialHintUI.Ensure();
        if (hint == null) return false;
        hint.SetBannerSpriteOnce(bubble);
        hint.Show(text, target, HoldSeconds);
        // Show 内部遇到战斗头顶台词会直接 return：只有真显示出来才记标记
        if (!hint.IsVisible || hint.CurrentHint != text)
        {
            hint.SetBannerSpriteOnce(null);   // 没弹出就退掉一次性底框，防污染下一次引导
            return false;
        }

        RecordShown(save);
        Debug.Log("[PetTalentHint] 角色页箭头气泡：" + text);
        return true;
    }

    /// <summary>宠物入口可用：节点在、且宠物功能已解锁（未解锁时按钮整体隐藏，不指）。</summary>
    static bool PetUsable(RectTransform rect)
    {
        if (!Usable(rect)) return false;
        var save = SaveSystem.Instance?.Data;
        return save != null && save.HasStageCleared(1, 2);
    }

    static bool Usable(RectTransform rect)
    {
        return rect != null && rect.gameObject.activeInHierarchy;
    }

    /// <summary>清档复位：本进程标记 + 存档里的 3 天额度一起归零（NewGameReset.ApplyAll 调）。</summary>
    public static void ResetForNewGame()
    {
        _shownDay = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;   // 存档还没换新：新档默认值本身就是 0
        data.petTalentHintDays = 0;
        data.petTalentHintDayKey = "";
    }
}
