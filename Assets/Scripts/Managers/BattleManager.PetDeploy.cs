using UnityEngine;

/// <summary>
/// 上阵宠物战斗出场（2026-11-21）：正式主线局开战时，把存档里上阵的宠物生成到玩家身边。
///
/// 挂点 = StartNewRun 开场处 SpawnMercenaries 调用之后的一行 DeployDeployedPet()；
/// 只认「正式主线 + 非引导」（IsFormalMainRun && !IsTutorialRun），竞技场/裂隙/各活动副本不在此批
/// （规格第一节：竞技场露脸是后续，且要按排名分档，单独一批落地）。
///
/// 真源：上阵 id = SaveData.petDeployedId，解锁判定 = IsPetUnlocked，属性/资源 = PetDefs。
/// 已在场上有同 id 宠物则不重复生成（换关幂等）。Prefab 走 Resources.Load("UI/Pets/"+prefabPath)，
/// 不写死 guid、不改 prefab（铁律 16/17）。
/// </summary>
public partial class BattleManager
{
    /// <summary>宠物跟随玩家的横向偏移（身侧偏后）。生成与队伍进场摆位共用同一真源。</summary>
    const float PET_FOLLOW_OFFSET = 0.8f;

    /// <summary>营救登场落点（2026-11-23 主人「狗要已经在和怪战斗，不是从玩家左面进来」）：
    /// 落在玩家前方 → 至少比玩家靠前这么远，绝不站到玩家身后。</summary>
    const float PET_RESCUE_AHEAD_MIN = 0.9f;
    /// <summary>【2026-11-24】第一波刚清完（场上还没怪）时，狗不落玩家眼前，改落画面右缘【外】：
    /// 越过 visMax（已含 0.35 内收）这么远，玩家走过去才看见，不在清波瞬间凭空出现。
    /// 【2026-11-24 拉近 1.5→0.5】日志实测玩家第二波只走约 0.6 单位（怪在面前拦住），
    /// 1.5 会让狗一直停在屏外、玩家没看到就触发；0.5 使狗出生时刚出屏、玩家走 0.6 即入镜。</summary>
    const float PET_RESCUE_OFFSCREEN_AHEAD = 0.5f;
    /// <summary>屏外预生成时狗至少离玩家这么远（玩家走过去才看见，不在清波瞬间凭空出现）。
    /// 【2026-11-24 拉近 5.5→3.0】配合上方 AHEAD，保证 max() 取 visMax+0.5 而非 heroX+5.5，
    /// 否则狗会被 5.5 拉到屏外深处、永远入不了镜。</summary>
    const float PET_RESCUE_OFFSCREEN_HERO_GAP = 3.0f;

    /// <summary>取场上当前上阵宠物（宠物不在 MercenaryManager 列表里，单独从 allyUnits 找）。</summary>
    Pet GetDeployedPet()
    {
        for (int i = 0; i < allyUnits.Count; i++)
            if (allyUnits[i] is Pet p && p != null && !p.isDead)
                return p;
        return null;
    }

    void DeployDeployedPet()
    {
        if (!IsFormalMainRun || IsTutorialRun) return;

        var save = SaveSystem.Instance?.Data;
        if (save == null || string.IsNullOrEmpty(save.petDeployedId)) return;
        SpawnBattlePet(save.petDeployedId, "上阵宠物出场");
    }

    /// <summary>
    /// 关 3 营救剧情播完后狗中途归队（2026-11-22 补 PetRescueStoryBeats 注释承诺的
    /// 「末波精英战狗回归助战」）。与开战上阵共用 SpawnBattlePet 单一出口，不新增第二套生成逻辑；
    /// 只认正式主线非引导，场上已有同 id 宠物则幂等早退（不叠加第二只）。
    /// 不写 petDeployedId：上阵真源仍归玩家手动选择，引导流程不被跳过。
    /// </summary>
    /// <summary>spawnX 有值用指定落点（归队从左侧进场）；不传=首登场（第一波清完）：无怪时落画面右缘外预生成、有怪时落怪群。</summary>
    void DeployRescuedPet(float? spawnX = null)
    {
        if (!IsFormalMainRun || IsTutorialRun) return;
        SpawnBattlePet(PetRescueStoryBeats.DogId, "营救回归", spawnX ?? ResolveRescueDogSpawnX());
        // 只给首次登场（第一波清完、屏外预生成）的营救狗开护送：免拴绳、只咬右侧怪、一直朝右，
        // 等玩家走近触发气泡；传了 spawnX 的精英归队走正常 AI，不开护送。
        // 营救狗全程不跑（2026-11-24 主人「狗别跑了，跟着玩家一直打」）：血空回血续战，不退场。
        var dog = FindRescueDog();
        if (dog != null)
        {
            dog.SetRescueNoFlee(true);
            if (spawnX == null) dog.SetRescueEscort(true);
        }
    }

    /// <summary>狗从左侧进场的生成点（2026-11-23 主人「狗没有从左边跑进来，还是突然出现的」）：
    /// 落在画面左缘【外】——生成那一刻玩家看不到 pop 出现，随后由演出协程 CoRunDogIntoScene
    /// 跑到目标点，观感是"从屏幕左侧跑进来"。第一次登场与精英战归队共用此起点。</summary>
    const float DOG_RUN_IN_SPAWN_MARGIN = 0.9f;
    float ResolveRescueDogLeftSpawnX()
    {
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        return visMin - DOG_RUN_IN_SPAWN_MARGIN;
    }

    /// <summary>
    /// 营救狗的横向落点：
    ///   · 有活怪（精英归队 / 跑进怪群的目标点）→ 取活怪平均 X，钳到画面内（下限玩家前方
    ///     PET_RESCUE_AHEAD_MIN、上限 visMax−1.2），狗在怪群里开打；
    ///   · 无活怪（第一波刚清完、第二波还没刷）→ 【2026-11-24】不落玩家眼前，改走
    ///     ResolveRescueDogOffscreenAheadX 落画面右缘外，玩家走过去才看见，避免清波瞬间凭空出现。
    /// </summary>
    float ResolveRescueDogSpawnX()
    {
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        float heroX = hero != null ? hero.transform.position.x
                    : (spawnPoint != null ? spawnPoint.position.x : 0f);

        float sum = 0f;
        int n = 0;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || m.isDead) continue;
            sum += m.transform.position.x;
            n++;
        }
        if (n == 0)
        {
            // 第一波刚清完、第二波怪还没刷：不套用下方 hi=屏内 的钳制，
            // 直接落画面右缘外（玩家走过去才看见），并钳到相机右界内防甩出关卡。
            return ResolveRescueDogOffscreenAheadX(visMax, heroX);
        }
        float x = sum / n;

        float lo = Mathf.Max(visMin + 0.6f, heroX + PET_RESCUE_AHEAD_MIN);
        float hi = visMax - 1.2f;
        if (lo > hi) return hi;
        return Mathf.Clamp(x, lo, hi);
    }

    /// <summary>
    /// 第一波清完狗的屏外预生成落点（2026-11-24 主人「屏幕外先生成，玩家再走过去」）：
    /// 取「越过画面右缘」与「离玩家足够远」的较大值，再钳到相机右界内
    /// （follow.maxX 默认 spawnX+80，足够远；钳制只防个别短关卡把狗甩到界外）。
    /// </summary>
    float ResolveRescueDogOffscreenAheadX(float visMax, float heroX)
    {
        float x = Mathf.Max(visMax + PET_RESCUE_OFFSCREEN_AHEAD, heroX + PET_RESCUE_OFFSCREEN_HERO_GAP);
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null) x = Mathf.Min(x, follow.maxX - 1f);
        return x;
    }

    /// <summary>生成宠物战斗单位到玩家身侧；开战上阵与营救中途归队共用此唯一出口。
    /// spawnX 有值 = 用指定横向落点（营救登场落在玩家前方怪群里），不传 = 站玩家身侧偏后。</summary>
    void SpawnBattlePet(string id, string reason, float? spawnX = null)
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;

        var def = PetDefs.Get(id);
        if (def == null)
        {
            Debug.LogError("[PetDeploy] 上阵宠物 id 不在 PetDefs 真源: " + id + "（不静默兜底）");
            return;
        }
        if (!save.IsPetUnlocked(id))
        {
            Debug.LogWarning("[PetDeploy] 上阵宠物尚未解锁: " + id + "，本局不出场");
            return;
        }
        // 幂等：场上已有同 id 宠物（换关/重入）不重复生成
        for (int i = 0; i < allyUnits.Count; i++)
        {
            if (allyUnits[i] is Pet p && p != null && p.petDefId == id)
                return;
        }

        GameObject prefab = Resources.Load<GameObject>("UI/Pets/" + def.prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[PetDeploy] 宠物 prefab 加载失败: UI/Pets/" + def.prefabPath);
            return;
        }

        Vector3 pos = hero != null ? hero.transform.position
                    : (spawnPoint != null ? spawnPoint.position : Vector3.zero);
        pos.y = UnitBase.GROUND_Y;
        if (unitRoot != null) pos.z = unitRoot.position.z;
        // 【2026-11-23 主人「狗要已经在和怪战斗，不是从玩家左面进来」】营救传 spawnX → 前方怪群；
        // 开战上阵不传 → 仍站玩家身侧偏后，不挡主角。
        pos.x = spawnX.HasValue ? spawnX.Value : pos.x - PET_FOLLOW_OFFSET;

        GameObject go = Instantiate(prefab);
        go.SetActive(true);
        GameConfig.AttachToUnitRoot(go.transform);
        GameConfig.SetWorldPosition(go, pos);
        go.transform.rotation = Quaternion.identity;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var pet = go.GetComponent<Pet>();
        if (pet == null) pet = go.AddComponent<Pet>();
        pet.InitPet(id);

        allyUnits.Add(pet);
        PetBattleBanter.EnsureOn(this);
        Debug.Log("[PetDeploy] " + reason + ": " + id + " pos=" + pos);
    }
}
