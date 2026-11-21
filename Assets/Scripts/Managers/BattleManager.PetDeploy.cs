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
    void DeployDeployedPet()
    {
        if (!IsFormalMainRun || IsTutorialRun) return;

        var save = SaveSystem.Instance?.Data;
        if (save == null || string.IsNullOrEmpty(save.petDeployedId)) return;
        string id = save.petDeployedId;

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
        pos.x -= 0.8f;   // 宠物站在玩家身侧偏后，不挡主角

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
        Debug.Log("[PetDeploy] 上阵宠物出场: " + id + " pos=" + pos);
    }
}
