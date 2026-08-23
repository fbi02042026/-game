using UnityEngine;

/// <summary>
/// 城镇 IA 与存档字段对齐：公会等级、酒馆出战栏、遗产池可见性。
/// 各页打开时调用，避免 UI 与 SaveData 漂移。
/// </summary>
public static class TownSaveAlign
{
    public static void AlignAll()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        if (data.townLevel == null)
            data.townLevel = new TownLevel();

        if (data.guildLevel < 1)
            data.guildLevel = 1;

        if (data.maxUnlockedChapter < 1)
            data.maxUnlockedChapter = 1;

        if (data.permanentMercs == null)
            data.permanentMercs = new System.Collections.Generic.List<MercenaryData>();

        if (data.legacyEquipPool == null)
            data.legacyEquipPool = new System.Collections.Generic.List<EquipmentData>();

        // 酒馆出战栏：槽位数 = townLevel.tavern（与 MercenaryManager 一致）
        int slots = Mathf.Clamp(data.townLevel.tavern, 0, 2);
        data.townLevel.tavern = slots;

        // 好兵好感下限，避免 UI 显示异常
        for (int i = 0; i < data.permanentMercs.Count; i++)
        {
            var m = data.permanentMercs[i];
            if (m == null) continue;
            if (m.favorLevel < 0) m.favorLevel = 0;
            if (m.level < 1) m.level = 1;
        }

        StoryProgress.EnsureLists(data);
    }

    public static int LegacyPoolCount()
    {
        AlignAll();
        return SaveSystem.Instance?.Data?.legacyEquipPool?.Count ?? 0;
    }

    public static int DeployMercCount()
    {
        AlignAll();
        return MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetActiveMercIds().Count
            : 0;
    }
}
