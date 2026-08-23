using UnityEditor;
using UnityEngine;

/// <summary>Batch1 自检：天赋等集合经 JsonUtility 往返不丢。</summary>
public static class SaveDataRoundtripSelfCheck
{
    [MenuItem("Tools/自检/存档 JsonUtility 往返")]
    public static void Run()
    {
        var data = new SaveData();
        data.SyncRuntimeFromLists();
        data.talents["left_atk_1"] = 1;
        data.talents["choice_hp"] = 2;
        data.unlockedLegendaryWeapons.Add("legend_sword_01");
        data.achievementProgress["kill_100"] = 42;
        data.completedAchievements.Add("first_clear");
        data.claimedMilestoneIds.Add(1);
        data.totalGold = 12345;

        data.SyncListsFromRuntime();
        string json = JsonUtility.ToJson(data, true);

        var loaded = JsonUtility.FromJson<SaveData>(json);
        loaded.SyncRuntimeFromLists();

        bool ok =
            loaded.talents != null
            && loaded.talents.TryGetValue("left_atk_1", out int t1) && t1 == 1
            && loaded.talents.TryGetValue("choice_hp", out int t2) && t2 == 2
            && loaded.unlockedLegendaryWeapons.Contains("legend_sword_01")
            && loaded.achievementProgress.TryGetValue("kill_100", out int p) && p == 42
            && loaded.completedAchievements.Contains("first_clear")
            && loaded.claimedMilestoneIds.Contains(1)
            && loaded.totalGold == 12345
            && json.Contains("talentEntries")
            && !json.Contains("\"talents\":{");

        string msg = ok
            ? "通过：天赋/成就/传说武器经 JsonUtility 往返保留。\n\n样例片段：\n"
              + json.Substring(0, Mathf.Min(400, json.Length))
            : "失败：集合字段未正确持久化。\n\nJSON：\n" + json;

        Debug.Log(ok ? "[SaveDataRoundtrip] OK" : "[SaveDataRoundtrip] FAIL\n" + json);
        EditorUtility.DisplayDialog(ok ? "存档自检通过" : "存档自检失败", msg, "好的");
    }
}
