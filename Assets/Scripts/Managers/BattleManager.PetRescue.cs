using System;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（1-3 Boss 关）——【2026-11-23 统一编排器重构后】本文件只剩两件事：
///   ① TryPetRescueActivate：第一波清完做"本关是否符合"的激活判定（不插叙、不冻场）；
///   ② TickPetRescueRejoin：Boss 半血时狗归队帮忙（走 DeployRescuedPet 唯一出口，幂等）。
/// 演出主体（狗真参战上场 / 汪汪气泡 / 玩家喊话 / 等清波 / 点评 / 放行）已全部迁到
/// BattleManager.CutsceneDirector.cs，由单协程按 BattleCutsceneDefs.DogRescue 步骤表驱动。
/// 旧版 4 协程 + phase 0~4 + 纯视觉狗 + 狗位真源特判已删除（正是 8 轮抢时序的根源）。
/// 未通关时每次进 1-3 都演，通关后不再演（真源 = clearedStages）。
/// </summary>
public partial class BattleManager
{
    bool _petRescueStageActive;      // 本关是否符合（1-3 未通关 + 正式主线 + 普通难度）
    bool _petRescuePendingDeploy;    // Boss 半血归队标志
    const float PET_REJOIN_HP_RATIO = 0.5f;  // 【2026-11-23 主人拍板】Boss「打到一半」= 半血归队

    /// <summary>第一波清完调用：只判定+置位，不插叙不冻场（第一波照常打、玩家继续往前走）。</summary>
    void TryPetRescueActivate()
    {
        if (_petRescueStageActive) return;
        if (!IsFormalMainRun) return;                 // 只正式主线
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return;   // 只普通难度
        var cm = ChapterManager.Instance;
        if (cm == null || cm.currentChapter != 1 || cm.currentGrand != 1) return;
        if (cm.CurrentSubIndex0 != 2) return;         // 1-3（0 基 sub0=2）
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;
        if (save.HasStageCleared(1, 2)) return;      // 通关后不再演
        _petRescueStageActive = true;
        Debug.Log("[PetRescue] 1-3 未通关，狗剧情激活（等第二波单协程演出）");
    }

    /// <summary>【2026-11-23 主人拍板】Boss 打到半血时狗归队帮忙：只认 1-3 剧情已播的本关，一次性。</summary>
    /// 挂点 = BattleManager.Update（清死怪之后）；只找场上活着的正牌 Boss（IsBossUnit），
    /// 血量比例 <= 0.5 才走 DeployRescuedPet（与开战上阵共用 SpawnBattlePet 唯一出口）。
    /// 狗在第二波已真参战时在场上，SpawnBattlePet 幂等早退（不叠加第二只）——故本标志在那种情况空转，无害。
    void TickPetRescueRejoin()
    {
        if (!_petRescuePendingDeploy) return;
        if (!isInBattle || _stageCleared || _portalActive) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var boss = monsters[i] as Monster;
            if (boss == null || boss.isDead || !boss.IsBossUnit || boss.attr == null) continue;
            float maxHp = boss.attr.GetAttr(AttrType.MaxHp);
            if (maxHp <= 0f) continue;
            if (boss.currentHp / maxHp > PET_REJOIN_HP_RATIO) continue;
            _petRescuePendingDeploy = false;   // 一次性，归队后不再重入
            DeployRescuedPet();
            Debug.Log("[PetRescue] Boss 半血，狗归队帮忙 hpRatio=" + (boss.currentHp / maxHp).ToString("F2"));
            return;
        }
    }
}
