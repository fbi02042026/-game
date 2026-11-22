using System.Collections;
using UnityEngine;

/// <summary>
/// 宠物营救剧情钩子（2026-11-21）：1-3（第1篇第1座大关第3小关）第一波清完后插叙 3 拍。
///
/// 挂点 = BeginNextWaveCountdown（波清点权威出口）开头的一次性钩子 TryPetRescueInterlude；
/// 判定限定「正式主线 + 当前 sub0==2 + 未播过」，其它玩法（裂隙/竞技场/活动本/羁绊/支线岛）一律不触发，
/// 不改波数/怪数/怪物结构（铁律 40/41），只在两波之间插一段剧情。
///
/// 流式抢跑防护：插叙期间置 _preBattleStoryPlaying=true，TickWaveStreamSpawn:1401 见到就停，
/// 不会在剧情没播完时把下一波刷出来；播完回调里解锁狗 + 存档 + 放行（下一波由流式/波清点自然推进）。
/// </summary>
public partial class BattleManager
{
    bool _petRescueHookFired;   // 本关只尝试一次，防重入

    /// <summary>
    /// 波清点钩子：本关 1-3 且尚未播过营救插叙 → 启动插叙并返回 true（本次出兵取消）。
    /// 由 BeginNextWaveCountdown 调用；返回 false 时调用方照常出兵，行为不变。
    /// </summary>
    bool TryPetRescueInterlude()
    {
        if (_petRescueHookFired) return false;
        if (!IsFormalMainRun) return false;                 // 只正式主线
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return false;   // 剧情只普通难度：困难/噩梦不播（与项目主线剧情统一口径）
        var cm = ChapterManager.Instance;
        if (cm == null || cm.currentChapter != 1 || cm.currentGrand != 1) return false;
        if (cm.CurrentSubIndex0 != 2) return false;         // 1-3（0 基 sub0=2）
        var save = SaveSystem.Instance?.Data;
        if (save == null) return false;
        if (save.HasIntruderStorySeen(PetRescueStoryBeats.SeenKey)) return false;

        _petRescueHookFired = true;
        Debug.Log("[PetRescue] 1-3 第一波清完，触发营救插叙");
        StartCoroutine(CoPetRescueInterlude());
        return true;
    }

    IEnumerator CoPetRescueInterlude()
    {
        // 压住流式刷怪 + 波清点重入，剧情播完再放行
        bool prevGate = _preBattleStoryPlaying;
        _preBattleStoryPlaying = true;

        // 与 DailyLoginUI 全屏弹窗错峰（照 TownIdleNudge / TutorialDirector.Town 现成判据）
        float appearGuard = 0f;
        while (DailyLoginUI.PopupVisible && appearGuard < 3f)
        {
            appearGuard += Time.unscaledDeltaTime;
            yield return null;
        }

        bool done = false;
        float watchdog = 0f;
        PetRescueStoryBeats.TryPlay(() => done = true);
        while (!done && watchdog < 300f)
        {
            watchdog += Time.unscaledDeltaTime;
            yield return null;
        }
        if (watchdog >= 300f)
            Debug.LogError("[PetRescue] 营救插叙看门狗超时 300s，强制放行（StoryDirector 可能未回调）");

        // 营救成功即预解锁狗（哪怕本关失败也不回收，规格第一节：不锁在 1-3 难度墙后）
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            data.UnlockPet(PetRescueStoryBeats.DogId);
            data.MarkIntruderStorySeen(PetRescueStoryBeats.SeenKey);
            SaveSystem.Instance.Save();
            Debug.Log("[PetRescue] 营救成功，预解锁 " + PetRescueStoryBeats.DogId);

            // 【2026-11-22 补】兑现 PetRescueStoryBeats 头部承诺「末波精英战狗回归助战」：
            // 营救播完即让狗中途归队进 allyUnits（与开战上阵共用 SpawnBattlePet 出口），
            // 这样末波精英战时狗已在场、按 Pet/Skills 全套 AI 帮忙打。不写 petDeployedId。
            DeployRescuedPet();
        }

        _preBattleStoryPlaying = prevGate;

        // 放行：重新排 1 秒后的流式出兵（插叙期间 TickWaveStreamSpawn 被闸门挡掉并把 _waveStreamCo 清了）。
        // 只在还有未刷波、非通关态时排，避免多排协程；正常流程不因插叙改变波次结构。
        if (isInBattle && !_stageCleared && !_portalActive && !_allWavesSpawned
            && FindNextUnspawnedWaveIndex() >= 0)
            ScheduleWaveStreamNext();
    }
}
