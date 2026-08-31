using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcBondEntry
{
    public string npcId;
    public int value;
}

[Serializable]
public class StoryChoiceEntry
{
    public int chapter;
    public string choice;
}

/// <summary>
/// 剧情存档读写。羁绊/选择用 List，兼容 JsonUtility。
/// </summary>
public static class StoryProgress
{
    public const string NpcXiaomei = "xiaomei";
    public const string NpcAltor = "altor";
    public const string NpcGrey = "grey";
    public const string NpcEileen = "eileen";
    public const string NpcMaster = "master";

    // 老盾（H001）对应 dunbing101；教程战与花名册一致
    public const string TutorialMercId = "dunbing101";

    static bool _pendingTutorialBattle;
    static bool _pendingChapter1TownReturn;

    public static bool TutorialDone => Save()?.tutorialDone ?? false;
    public static bool OpeningIntroPlayed => Save()?.openingIntroPlayed ?? false;
    public static bool TutorialIntroDone => Save()?.tutorialIntroDone ?? false;
    public static bool TutorialBattleCleared => Save()?.tutorialBattleCleared ?? false;
    public static bool TutorialOutroPending => Save()?.tutorialOutroPending ?? false;
    public static bool Chapter1IntroDone => Save()?.chapter1IntroDone ?? false;
    public static bool Chapter1ChoiceDone => Save()?.chapter1ChoiceDone ?? false;

    public static bool HasPlayerName()
    {
        var data = Save();
        return data != null && data.playerNameChosen && !string.IsNullOrEmpty(data.playerDisplayName);
    }

    public static string GetPlayerName()
    {
        var data = Save();
        if (data != null && !string.IsNullOrEmpty(data.playerDisplayName))
            return data.playerDisplayName;
        return PlayerIdentity.DefaultName;
    }

    public static void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var data = Save();
        if (data == null) return;
        data.playerDisplayName = name;
        data.playerNameChosen = true;
        SaveSystem.Instance?.Save();
        Debug.Log($"[Story] \u73a9\u5bb6\u59d3\u540d\u5df2\u8bbe\u7f6e: {name}");
    }

    public static bool ShouldStartTutorialBattle()
    {
        // QueueTutorialBattle 显式排队优先（城镇引导入口）
        if (_pendingTutorialBattle)
            return true;
        return !TutorialDone && !TutorialBattleCleared;
    }

    static SaveData Save() => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    public static void EnsureLists(SaveData data)
    {
        if (data == null) return;
        data.npcBonds ??= new List<NpcBondEntry>();
        data.storyChoices ??= new List<StoryChoiceEntry>();
    }

    public static void ResetRuntimeFlags()
    {
        _pendingTutorialBattle = false;
        _pendingChapter1TownReturn = false;
    }

    public static void QueueChapter1TownReturn() => _pendingChapter1TownReturn = true;

    public static void QueueTutorialBattle() => _pendingTutorialBattle = true;

    public static bool ConsumeChapter1TownReturn()
    {
        bool v = _pendingChapter1TownReturn;
        _pendingChapter1TownReturn = false;
        return v;
    }

    public static bool ConsumeTutorialBattleFlag()
    {
        bool v = _pendingTutorialBattle;
        _pendingTutorialBattle = false;
        return v;
    }

    /// <summary>引导未完成时清空战斗背包，避免中断后再进战斗带着上次掉落。</summary>
    public static void ResetTutorialRunInventoryIfNeeded()
    {
        if (TutorialDone || TutorialBattleCleared) return;
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.InitNewRun();
    }

    public static void MarkTutorialIntroDone()
    {
        var data = Save();
        if (data == null) return;
        data.tutorialIntroDone = true;
        SaveSystem.Instance.Save();
    }

    public static void MarkOpeningIntroPlayed()
    {
        var data = Save();
        if (data == null) return;
        data.openingIntroPlayed = true;
        SaveSystem.Instance.Save();
    }

    public static void MarkTutorialBattleCleared()
    {
        var data = Save();
        if (data == null) return;
        data.tutorialBattleCleared = true;
        data.tutorialOutroPending = true;
        SaveSystem.Instance.Save();
    }

    public static void MarkTutorialDone()
    {
        var data = Save();
        if (data == null) return;
        data.tutorialDone = true;
        data.tutorialIntroDone = true;
        data.tutorialBattleCleared = true;
        data.tutorialOutroPending = false;
        SaveSystem.Instance.Save();
        AdventureCodex.CompleteMain("P0");
        AdventureCodex.UnlockWorld("W006");
        AdventureCodex.UnlockWorld("W007");
        AdventureCodex.UnlockWorld("W003");
        AdventureLogAchievements.OnTutorialDone();
        SpecialWeapons.TryGrantTwilightStaff(showToast: true);
    }

    public static void MarkChapter1IntroDone()
    {
        var data = Save();
        if (data == null) return;
        data.chapter1IntroDone = true;
        SaveSystem.Instance.Save();
    }

    public static void MarkChapter1Choice(string choice)
    {
        var data = Save();
        if (data == null) return;
        EnsureLists(data);
        data.chapter1ChoiceDone = true;
        SetChoice(1, choice);
        SaveSystem.Instance.Save();
    }

    public static int GetBond(string npcId)
    {
        var data = Save();
        if (data?.npcBonds == null) return 0;
        for (int i = 0; i < data.npcBonds.Count; i++)
        {
            if (data.npcBonds[i] != null && data.npcBonds[i].npcId == npcId)
                return data.npcBonds[i].value;
        }
        return 0;
    }

    public static void AddBond(string npcId, int delta)
    {
        if (string.IsNullOrEmpty(npcId) || delta == 0) return;
        var data = Save();
        if (data == null) return;
        EnsureLists(data);
        NpcBondEntry entry = null;
        for (int i = 0; i < data.npcBonds.Count; i++)
        {
            if (data.npcBonds[i] != null && data.npcBonds[i].npcId == npcId)
            {
                entry = data.npcBonds[i];
                break;
            }
        }
        if (entry == null)
        {
            entry = new NpcBondEntry { npcId = npcId, value = 0 };
            data.npcBonds.Add(entry);
        }
        entry.value = Mathf.Clamp(entry.value + delta, -100, 100);
        SaveSystem.Instance.Save();
        Debug.Log($"[Story] 羁绊 {npcId} {delta:+#;-#;0} → {entry.value}");
    }

    public static void SetChoice(int chapter, string choice)
    {
        var data = Save();
        if (data == null) return;
        EnsureLists(data);
        for (int i = 0; i < data.storyChoices.Count; i++)
        {
            if (data.storyChoices[i] != null && data.storyChoices[i].chapter == chapter)
            {
                data.storyChoices[i].choice = choice;
                return;
            }
        }
        data.storyChoices.Add(new StoryChoiceEntry { chapter = chapter, choice = choice });
    }

    public static string GetChoice(int chapter)
    {
        var data = Save();
        if (data?.storyChoices == null) return null;
        for (int i = 0; i < data.storyChoices.Count; i++)
        {
            if (data.storyChoices[i] != null && data.storyChoices[i].chapter == chapter)
                return data.storyChoices[i].choice;
        }
        return null;
    }
}
