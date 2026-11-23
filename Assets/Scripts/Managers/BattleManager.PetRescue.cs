using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 宠物营救剧情钩子（2026-11-21）：1-3（第1篇第1座大关第3小关）第一波清完后插叙 3 拍。
///
/// 挂点 = BeginNextWaveCountdown（波清点权威出口）开头的一次性钩子 TryPetRescueInterlude；
/// 判定限定「正式主线 + 当前 sub0==2 + 未播过」，其它玩法（裂隙/竞技场/活动本/羁绊/支线岛）一律不触发，
/// 不改波数/怪数/怪物结构（铁律 40/41），只在两波之间插一段剧情。
///
/// 流式抢跑防护：插叙期间置 _preBattleStoryPlaying=true，TickWaveStreamSpawn 见到就停，
/// 不会在剧情没播完时把下一波刷出来；播完回调里解锁狗 + 存档 + 放行（下一波由流式/波清点自然推进）。
/// 【2026-11-23 主人拍板】插叙从全屏对白改为<b>战场内演出</b>（见 CoPetRescueBattleScene）：
/// 场上生成一只挨打的狗 + 头顶气泡 + 全场刀光 + 狗逃走，保持战斗视角；全程不生成真战斗单位、
/// 不进 monsters/allyUnits、不计 KillCount/掉落，纯视觉、零统计污染。
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
        StartCoroutine(CoPetRescueBattleScene(() => done = true));
        while (!done && watchdog < 300f)
        {
            watchdog += Time.unscaledDeltaTime;
            yield return null;
        }
        if (watchdog >= 300f)
            Debug.LogError("[PetRescue] 营救战场演出看门狗超时 300s，强制放行（演出协程可能未回调）");

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

        // 【2026-11-23 主人「下一波和剧情不要同时出现」】演出收窗后留一拍停顿，
        // 让气泡/狗逃走的画面收干净再放行，不与下一波刷怪撞在同一帧。
        yield return new WaitForSecondsRealtime(0.6f);

        _preBattleStoryPlaying = prevGate;

        // 放行：重新排 1 秒后的流式出兵（插叙期间 TickWaveStreamSpawn 被闸门挡掉并把 _waveStreamCo 清了）。
        // 只在还有未刷波、非通关态时排，避免多排协程；正常流程不因插叙改变波次结构。
        if (isInBattle && !_stageCleared && !_portalActive && !_allWavesSpawned
            && FindNextUnspawnedWaveIndex() >= 0)
            ScheduleWaveStreamNext();
    }

    // ============================================================
    // 【2026-11-23 主人拍板】宠物挨打剧情：战场内演出（不是全屏几句话带过）
    // ============================================================
    // 纯视觉、零统计污染：生成一只挨打的狗 prefab（不挂 Pet 组件、不进 allyUnits/monsters），
    // 用头顶气泡走对白、Animator 播 Hit 表现挨打、全场刀光表现玩家上前营救，最后狗逃走并销毁。
    // 不动波数/怪数/怪物结构（铁律 40/41）：这些临时物体不参与任何战斗结算。
    // 全程用 unscaled 时间，跟 StoryDirector 一样不依赖 timeScale。

    IEnumerator CoPetRescueBattleScene(Action onDone)
    {
        var talk = BattleHeadTalkUI.Ensure();
        var vfx = BattleVFXSystem.Instance;
        float heroX = hero != null ? UnitBase.GetCombatX(hero) : 0f;
        float z = unitRoot != null ? unitRoot.position.z : 0f;

        // 狗落在玩家前方偏右（玩家朝右打，狗被围在前方看得见）；不推英雄坐标（2026-09-18 铁律）
        float dogX = heroX + 2.4f;
        Vector3 dogPos = new Vector3(dogX, UnitBase.GROUND_Y, z);
        var dog = SpawnRescueDogVisual(dogPos);
        if (dog == null)
        {
            // 取不到狗 prefab：不静默兜底，报错后仍回调放行（避免卡死流程）
            Debug.LogError("[PetRescue] 挨打狗 prefab 取不到，战场演出跳过对白（流程照常放行）");
            onDone?.Invoke();
            yield break;
        }
        var dogAnim = dog.GetComponent<Animator>();
        var dogTf = dog.transform;

        // —— 第一拍：狗被围，呜咽 + 挨打动作（头顶气泡贴狗，不弹全屏窗）——
        if (talk != null)
        {
            talk.PlayLineAt(dogTf, PetRescueStoryBeats.DogWhimper, PetRescueStoryBeats.WhimperHold);
            // 播 Hit 帧表现「正在挨打」（prefab 自带 Animal_Hit 状态，.controller 无参数按状态名直切）
            if (dogAnim != null && dogAnim.HasState(0, Animator.StringToHash("Animal_Hit")))
                dogAnim.Play("Animal_Hit", 0, 0f);
        }
        yield return new WaitForSecondsRealtime(PetRescueStoryBeats.WhimperHold);

        // —— 第二拍：玩家喊话上前（头顶气泡贴玩家本体）——
        if (talk != null && hero != null)
            talk.PlayLine(hero, PetRescueStoryBeats.PlayerShout, PetRescueStoryBeats.ShoutHold);
        yield return new WaitForSecondsRealtime(0.5f);

        // —— 全场刀光齐出：玩家「上前营救、一击清场」的视觉（不推英雄、不改战斗）——
        // 刀光落点 = 狗周围三处（左/上/右），营造「围攻的怪被同时劈开」；纯特效，不结算伤害。
        if (vfx != null)
        {
            vfx.PlaySlash(dogPos + new Vector3(-0.5f, 0.35f, 0f), 1, VfxFaction.Ally);
            vfx.PlaySlash(dogPos + new Vector3(0.5f, 0.35f, 0f), -1, VfxFaction.Ally);
            vfx.PlaySlash(dogPos + new Vector3(0f, 0.55f, 0f), 1, VfxFaction.Ally);
        }
        yield return new WaitForSecondsRealtime(PetRescueStoryBeats.SlashHold);

        // —— 第三拍：狗脱困，回应一声 ——
        if (talk != null)
            talk.PlayLineAt(dogTf, PetRescueStoryBeats.DogThanks, PetRescueStoryBeats.ThanksHold);
        if (dogAnim != null && dogAnim.HasState(0, Animator.StringToHash("Animal_Idle")))
            dogAnim.Play("Animal_Idle", 0, 0f);
        yield return new WaitForSecondsRealtime(PetRescueStoryBeats.ThanksHold);

        // —— 狗逃走：掉头跑出屏幕（只挪狗自己的坐标，不碰英雄/其它单位）——
        if (talk != null)
            talk.PlayLineAt(dogTf, PetRescueStoryBeats.DogFlee, PetRescueStoryBeats.ThanksHold);
        if (dogAnim != null && dogAnim.HasState(0, Animator.StringToHash("Animal_Walk")))
            dogAnim.Play("Animal_Walk", 0, 0f);
        float fleeT = 0f;
        const float fleeDur = 0.9f;
        float fromX = dogTf.position.x;
        float toX = fromX - 9f;
        while (fleeT < fleeDur && dog != null)
        {
            fleeT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(fleeT / fleeDur);
            GameConfig.SetWorldPosition(dog, new Vector3(Mathf.Lerp(fromX, toX, k), UnitBase.GROUND_Y, z));
            yield return null;
        }

        if (dog != null) Destroy(dog);
        onDone?.Invoke();
    }

    /// <summary>生成「挨打狗」纯视觉实例：只加载 prefab + 挂到 unitRoot + 半高缩放，不挂 Pet/不进战斗列表。</summary>
    GameObject SpawnRescueDogVisual(Vector3 pos)
    {
        var def = PetDefs.Get(PetRescueStoryBeats.DogId);
        if (def == null)
        {
            Debug.LogError("[PetRescue] 狗 id 不在 PetDefs 真源：" + PetRescueStoryBeats.DogId);
            return null;
        }
        GameObject prefab = Resources.Load<GameObject>("UI/Pets/" + def.prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[PetRescue] 狗 prefab 加载失败：UI/Pets/" + def.prefabPath);
            return null;
        }
        GameObject go = Instantiate(prefab);
        go.SetActive(true);
        GameConfig.AttachToUnitRoot(go.transform);
        GameConfig.SetWorldPosition(go, pos);
        go.transform.rotation = Quaternion.identity;
        // 半高：与战斗宠物同口径（玩家 localScale=1 → 狗 0.5），纯视觉不创建血条/技能
        float hs = hero != null ? Mathf.Abs(hero.transform.localScale.x) : 1f;
        if (hs < 0.0001f) hs = 1f;
        // 11-23 与战斗宠物共用体型真源：宠物形象翻倍后剧情狗同步，避免演出与实际战斗大小不一致。
        float s = hs * Pet.PET_HEIGHT_RATIO;
        go.transform.localScale = new Vector3(s, s, s);
        Debug.Log("[PetRescue] 战场演出：挨打狗出场 pos=" + pos + " scale=" + s.ToString("F2"));
        return go;
    }
}
