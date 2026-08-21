using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Battle场景初始化器：挂在Battle场景的空GameObject上
/// 查找或创建场景所需的所有对象（Camera、Ground、SpawnPoint、Hero、Monster等）
///
/// 双入口设计：
/// 1. 主入口：场景中AutoGameInitializer组件的Awake()（如果脚本引用正常）
/// 2. 后备入口：BattleUI.Awake()调用AutoGameInitializer.Initialize()
///    （当场景中脚本GUID断裂导致Missing Script时，由BattleUI触发初始化）
/// </summary>
public class AutoGameInitializer : MonoBehaviour
{
    /// <summary>防重入标志：确保初始化只执行一次</summary>
    private static bool _initialized = false;

    /// <summary>本次进 Battle 场景后初始化是否完成（供 Loading 等待）</summary>
    public static bool IsBattleLoadComplete { get; private set; }

    public static void ResetForSceneLoad()
    {
        IsBattleLoadComplete = false;
    }

    static void ReportInitStep(int step)
    {
        SceneLoadingCoordinator.ReportPostLoadStep(step, 8);
    }

    void Awake()
    {
        if (!GameSceneGate.IsBattle) return;
        Initialize();
    }

    /// <summary>
    /// Battle场景初始化入口（静态方法，可从外部调用）
    /// 当场景中的AutoGameInitializer脚本引用断裂时，BattleUI.Awake()会作为后备调用此方法
    /// </summary>
    public static void Initialize()
    {
        if (!GameSceneGate.IsBattle)
        {
            GamePerf.Log($"[AutoInit] 跳过：当前场景「{GameSceneGate.ActiveName}」不是 Battle");
            return;
        }
        // GameRoot / BattleManager 是 DontDestroyOnLoad：二次进战斗不能整段跳过，否则不刷怪、引用已毁
        bool systemsReady = _initialized && GameObject.Find("GameRoot") != null
                            && BattleManager.Instance != null;

        if (systemsReady)
        {
            GamePerf.Log("[AutoInit] 系统已存在 → 仅重绑场景引用并重新开战");
            RebindSceneAndRestartBattle();
            return;
        }

        IsBattleLoadComplete = false;
        _initialized = true;
        GamePerf.ApplyStartup();

        GamePerf.Log("[AutoInit] ===== Battle场景初始化开始 =====");

        // 确保跨场景持久根节点存在（由BootManager创建，但如果直接Play Battle场景则可能没有）
        EnsurePersistentRoot();
        GamePerf.Log("[AutoInit] 1/8 PersistentRoot就绪");
        ReportInitStep(1);

        // 查找或创建场景基础对象
        Camera cam = EnsureCamera();
        GamePerf.Log("[AutoInit] 2/8 相机就绪");
        ReportInitStep(2);

        // 修复场景中所有RectTransform scale=0的问题
        // BattleUI预制体根节点(Root)的scale可能为0，导致整个UI和子节点不可见
        FixAllScaleZero();
        GamePerf.Log("[AutoInit] 3/8 Scale修复完成");
        ReportInitStep(3);

        // 查找或创建出生点/终点/怪物刷新点（先摆 Spawn，再定 unit 站立线）
        Transform worldRoot = EnsureWorldRoot();
        Transform spawnPoint = EnsureSpawnPoint(worldRoot);
        Transform endPoint = EnsureEndPoint(worldRoot);
        Transform[] monsterSpawnPoints = EnsureMonsterSpawnPoints(worldRoot);
        Transform unitRoot = EnsureUnitRoot(worldRoot);

        // 站立线 = 用户调好的 unit.y（禁止再加偏移）
        UnitBase.GROUND_Y = unitRoot.position.y;
        GamePerf.Log($"[AutoInit] 4/8 站立线Y={UnitBase.GROUND_Y:F2} (=unit.y，无偏移)");
        ReportInitStep(4);

        // Ground 仅保证存在碰撞（可选），不参与站位
        EnsureGround(worldRoot);

        // 确保EventSystem
        EnsureEventSystem();

        // 创建GameRoot和所有系统
        GameObject gameRoot = EnsureGameRoot();
        GamePerf.Log("[AutoInit] 5/8 GameRoot就绪");
        ReportInitStep(5);

        // 初始化BattleManager引用（优先用单例，避免拿到将被销毁的重复组件）
        BattleManager bm = BattleManager.Instance != null
            ? BattleManager.Instance
            : gameRoot.GetComponent<BattleManager>();
        bm.spawnPoint = spawnPoint;
        bm.endPoint = endPoint;
        bm.monsterSpawnPoints = monsterSpawnPoints;
        bm.unitRoot = unitRoot;

        // 加载Hero（挂到 unit 下）
        Hero hero = EnsureHero(unitRoot, bm);
        hero.endPoint = endPoint;
        GamePerf.Log("[AutoInit] 6/8 Hero就绪");
        ReportInitStep(6);

        // 相机只跟 X，不改用户调好的 Y（禁止 AlignCamera 改站位）
        BattleViewportFit.Apply(cam);
        EnsureCameraFollow(cam, hero.transform, spawnPoint, endPoint);

        // BattleUI：map=10，HUD=100，CharacterBar=110（头像必须在 map 之上）
        FixBattleUICanvas(cam);
        EnsureCharacterBarVisibleRuntime();
        EnsureParallaxOnMaproot();

        // 加载Monster预制体到对象池并预热，减轻首波卡顿
        EnsureMonsterPrefab();
        PoolManager.Instance?.Warm("Monster", 6);
        GamePerf.Log("[AutoInit] 7/8 怪物池预热完成");
        ReportInitStep(7);

        // 加载战斗特效（内部有缓存，不重复 Load）
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.AutoLoadPrefabs();
        MonsterAttackStyleTable.Reload();

        // 开始新一局
        bm.StartNewRun();
        if (hero != null)
        {
            GameConfig.AttachToUnitRoot(hero.transform);
            // 只用 unit 的 Y/Z（用户草地），不强制 z=0
            float z = unitRoot.position.z;
            GameConfig.SetWorldPosition(hero.gameObject,
                new Vector3(hero.transform.position.x, UnitBase.GROUND_Y, z));
            float s = GameConfig.UNIT_SCALE;
            float sign = hero.transform.localScale.x < 0 ? -1f : 1f;
            hero.transform.localScale = new Vector3(sign * s, s, s);
            // 不改 SPUM Sorting
            ForceEnableRenderers(hero.gameObject);
            Debug.Log($"[AutoInit] Hero parent={hero.transform.parent?.name} pos={hero.transform.position} lossy={hero.transform.lossyScale}");
        }
        GamePerf.Log("[AutoInit] 8/8 StartNewRun完成");
        ReportInitStep(8);

        // 刷新UI角色栏（血条、头像、进度条）— 系统已就绪后再绑一次
        BattleUI.Instance?.RebindAfterSystemsReady();
        BattleUI.Instance?.UpdateTopBarResources();
        BattleSideHud.EnsureOn(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        EnsureCharacterBarVisibleRuntime();

        GamePerf.Log("[AutoInit] Battle场景初始化完成");
        IsBattleLoadComplete = true;
        SceneLoadingCoordinator.Finish();
    }

    /// <summary>二次进战斗：场景锚点/Hero 已随旧场景销毁，必须重绑再 StartNewRun</summary>
    static void RebindSceneAndRestartBattle()
    {
        IsBattleLoadComplete = false;
        FixAllScaleZero();
        Camera cam = EnsureCamera();
        Transform worldRoot = EnsureWorldRoot();
        Transform spawnPoint = EnsureSpawnPoint(worldRoot);
        Transform endPoint = EnsureEndPoint(worldRoot);
        Transform[] monsterSpawnPoints = EnsureMonsterSpawnPoints(worldRoot);
        Transform unitRoot = EnsureUnitRoot(worldRoot);
        UnitBase.GROUND_Y = unitRoot.position.y;
        ReportInitStep(4);

        EnsureGround(worldRoot);
        EnsureEventSystem();

        BattleManager bm = BattleManager.Instance;
        if (bm == null)
        {
            var root = EnsureGameRoot();
            bm = BattleManager.Instance != null ? BattleManager.Instance : root.GetComponent<BattleManager>();
        }
        if (bm != null && !bm.gameObject.activeSelf)
        {
            Debug.LogWarning("[AutoInit] BattleManager 宿主未激活 → 强制激活");
            bm.gameObject.SetActive(true);
        }
        bm.spawnPoint = spawnPoint;
        bm.endPoint = endPoint;
        bm.monsterSpawnPoints = monsterSpawnPoints;
        bm.unitRoot = unitRoot;

        // 旧 Hero 已随 Battle 卸载销毁（Unity 假 null）
        if (!bm.hero)
            bm.hero = null;
        Hero hero = EnsureHero(unitRoot, bm);
        hero.endPoint = endPoint;
        ReportInitStep(6);

        BattleViewportFit.Apply(cam);
        EnsureCameraFollow(cam, hero.transform, spawnPoint, endPoint);
        FixBattleUICanvas(cam);
        EnsureCharacterBarVisibleRuntime();
        EnsureParallaxOnMaproot();
        EnsureMonsterPrefab();
        PoolManager.Instance?.Warm("Monster", 6);
        ReportInitStep(7);

        bm.ClearAllMonsters();
        bm.StartNewRun();

        GameConfig.AttachToUnitRoot(hero.transform);
        float z = unitRoot.position.z;
        GameConfig.SetWorldPosition(hero.gameObject,
            new Vector3(hero.transform.position.x, UnitBase.GROUND_Y, z));
        float s = GameConfig.UNIT_SCALE;
        float sign = hero.transform.localScale.x < 0 ? -1f : 1f;
        hero.transform.localScale = new Vector3(sign * s, s, s);
        ForceEnableRenderers(hero.gameObject);

        BattleUI.Instance?.RebindAfterSystemsReady();
        BattleUI.Instance?.UpdateTopBarResources();
        BattleSideHud.EnsureOn(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        EnsureCharacterBarVisibleRuntime();
        GamePerf.Log("[AutoInit] 二次进战斗重绑完成，已 StartNewRun");
        ReportInitStep(8);
        IsBattleLoadComplete = true;
        SceneLoadingCoordinator.Finish();
    }

    // ===== 修复方法 =====

    /// <summary>
    /// 修复场景中所有RectTransform scale=0的问题
    /// BattleUI预制体根节点(Root)可能因为编辑器操作导致scale归零，
    /// 这会导致整个UI和所有子节点(SpawnPoint/MonsterSpawn等)不可见
    /// 必须遍历所有RectTransform，不仅仅是Canvas组件上的
    /// </summary>
    static void FixAllScaleZero()
    {
        RectTransform[] allRTs = Object.FindObjectsOfType<RectTransform>();
        foreach (RectTransform rt in allRTs)
        {
            // 真正归零必须修；SSC 的 ~0.01 正常值不要动
            if (rt.localScale != Vector3.zero) continue;

            Canvas canvas = rt.GetComponent<Canvas>();
            string n = rt.gameObject.name.ToLower();
            bool isUiRoot = canvas != null ||
                            n.Contains("battleui") || n == "root" ||
                            rt.gameObject.transform.parent == null;
            if (isUiRoot)
            {
                rt.localScale = Vector3.one;
                Debug.Log($"[AutoInit] 修复scale归零: {rt.gameObject.name} → (1,1,1)");
            }
        }
    }

    /// <summary>
    /// Ground 仅作可选碰撞：不恢复贴图、不强制显示（用户用 Maproot 草地）。
    /// </summary>
    static void EnsureGround(Transform worldRoot)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            return; // 不需要则不创建

        // 若误挂在 Canvas 下，挪到 WorldRoot，但不改位置数值意图
        if (worldRoot != null && ground.transform.IsChildOf(worldRoot) == false)
            ReparentToWorldRoot(ground, worldRoot);

        // 隐藏 Ground 精灵（用户反馈 Ground 不应再出现）
        SpriteRenderer sr = ground.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;
            sr.enabled = false;
        }
        Debug.Log("[AutoInit] Ground 已隐藏贴图（仅保留碰撞若存在）");
    }

    /// <summary>
    /// 仅当坐标几乎在原点时视为「未摆放」。
    /// 注意：禁止用 y&gt;0 判废——用户把 Spawn 摆在 Ground 附近（Y 略大于 0）是合法的。
    /// </summary>
    static bool IsUnsetWorldAnchor(Vector3 worldPos)
    {
        return Mathf.Abs(worldPos.x) < 0.01f && Mathf.Abs(worldPos.y) < 0.01f;
    }

    /// <summary>从场景 Ground 采样默认站立 Y；找不到则回退 -3.5</summary>
    static float SampleDefaultGroundY(Transform worldRoot)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var col = ground.GetComponent<Collider2D>();
            if (col != null)
                return col.bounds.max.y;
            return ground.transform.position.y;
        }
        return -3.5f;
    }

    /// <summary>
    /// BattleUI 根=0；map 嵌套 Canvas override=10；背包等 HUD 提到 100，避免压住人/怪(15)/特效(50)。
    /// </summary>
    static void FixBattleUICanvas(Camera mainCam)
    {
        BattleUI battleUI = Object.FindObjectOfType<BattleUI>();
        if (battleUI == null)
        {
            Debug.LogWarning("[AutoInit] BattleUI未找到，跳过Canvas修复");
            return;
        }

        Canvas rootCanvas = battleUI.GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = battleUI.GetComponentInChildren<Canvas>();

        if (rootCanvas == null)
        {
            Debug.LogWarning("[AutoInit] BattleUI Canvas未找到");
            return;
        }

        UICanvasSetup.Apply(rootCanvas, mainCam);
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        rootCanvas.sortingOrder = GameConfig.SORT_BATTLE_UI;

        BattleViewportFit.Apply(mainCam, rootCanvas);

        // 只做嵌套 overrideSorting，绝不把子 Canvas 改成独立 SSC（否则脱离 Scaler → 左右裁切）
        EnsureNestedSortOrder(FindDeepChildIgnoreCase(battleUI.transform, "map")
            ?? FindDeepChildIgnoreCase(battleUI.transform, "Maproot")
            ?? FindDeepChildIgnoreCase(battleUI.transform, "Map"), GameConfig.SORT_MAPROOT);

        // CharacterBar 单独 110，保证头像/头像框不被 map 挡住
        string[] hudNames =
        {
            "TopBar", "TopStatus", "ProgressBar",
            "QuestText", "QuestPanel", "BackpackPanel",
            "BottomBar", "SkillBar", "PausePanel", "SettingsPanel"
        };
        for (int i = 0; i < hudNames.Length; i++)
            EnsureNestedSortOrder(FindDeepChildIgnoreCase(battleUI.transform, hudNames[i]), 100);

        EnsureNestedSortOrder(FindDeepChildIgnoreCase(battleUI.transform, "CharacterBar"), 110);
        BattleSideHud.EnsureOn(battleUI.transform);

        if (battleUI.GetComponent<ViewportFitDriver>() == null)
            battleUI.gameObject.AddComponent<ViewportFitDriver>();

        Debug.Log($"[AutoInit] BattleUI 适配 MatchWidth ortho={mainCam?.orthographicSize:F2} screen={Screen.width}x{Screen.height}");
    }

    /// <summary>
    /// 嵌套排序：只设 overrideSorting，绝不 Destroy Canvas/RectTransform（会把头像等弄没）。
    /// </summary>
    static void EnsureNestedSortOrder(Transform t, int order)
    {
        if (t == null) return;
        Canvas c = t.GetComponent<Canvas>();
        if (c == null)
            c = t.gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        c.sortingOrder = order;
        if (t.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            t.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    /// <summary>
    /// WorldRoot 必须在场景根（真世界坐标），与特效/相机同一空间。
    /// 挂进 BattleUI Canvas 会导致人物与特效错位、视差飙车。
    /// </summary>
    static Transform EnsureWorldRoot()
    {
        GameObject wr = GameObject.Find("WorldRoot");
        if (wr == null)
        {
            wr = new GameObject("WorldRoot");
            Debug.Log("[AutoInit] WorldRoot 已创建（场景根）");
        }
        // 若误挂在 Canvas 下，脱回场景根并保持世界坐标
        if (wr.GetComponentInParent<Canvas>() != null)
        {
            wr.transform.SetParent(null, true);
            wr.transform.localScale = Vector3.one;
            Debug.LogWarning("[AutoInit] WorldRoot 已从 Canvas 移回场景根");
        }
        else
        {
            wr.transform.localScale = Vector3.one;
        }
        return wr.transform;
    }

    /// <summary>
    /// 只用场景里的 unit（可在 Ground/unit）。不改缩放、不加 SortingGroup。
    /// 仅当误挂在 Canvas 下时迁出，保持世界坐标。
    /// </summary>
    static Transform EnsureUnitRoot(Transform worldRoot)
    {
        BattleUI battleUI = Object.FindObjectOfType<BattleUI>();
        Transform u = null;

        if (worldRoot != null)
            u = FindDeepChildIgnoreCase(worldRoot, "unit");
        if (u == null && battleUI != null)
            u = FindDeepChildIgnoreCase(battleUI.transform, "unit");
        if (u == null)
        {
            GameObject byName = GameObject.Find("unit") ?? GameObject.Find("Unit");
            if (byName != null) u = byName.transform;
        }

        if (u == null)
        {
            var go = new GameObject("unit");
            if (worldRoot != null) go.transform.SetParent(worldRoot, false);
            Camera cam = Camera.main;
            float x = cam != null ? cam.transform.position.x - 1.2f : -7f;
            float y = cam != null ? cam.transform.position.y - 2f : -3.5f;
            go.transform.position = new Vector3(x, y, 0f);
            u = go.transform;
            Debug.LogWarning("[AutoInit] 已新建 unit");
        }
        else if (u.GetComponentInParent<Canvas>() != null && worldRoot != null)
        {
            // 只迁出 Canvas；Ground/unit 结构保持不动
            Transform ground = worldRoot.Find("Ground");
            Transform parent = ground != null ? ground : worldRoot;
            u.SetParent(parent, true);
            Debug.Log("[AutoInit] unit 从 Canvas 迁到 " + parent.name + "（保持世界坐标，不改缩放）");
        }

        Debug.Log($"[AutoInit] unit 就绪 path={GetPath(u)} pos={u.position} lossy={u.lossyScale} parentScale={(u.parent != null ? u.parent.lossyScale.ToString() : "null")}");
        return u;
    }

    static string GetPath(Transform t)
    {
        if (t == null) return "";
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    static void ForceEnableRenderers(GameObject go)
    {
        if (go == null) return;
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            srs[i].enabled = true;
            Color c = srs[i].color;
            c.a = 1f;
            srs[i].color = c;
        }
        var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < mrs.Length; i++)
            mrs[i].enabled = true;
    }

    // ===== 持久根节点 =====

    static void EnsurePersistentRoot()
    {
        GameObject root = GameObject.Find("PersistentRoot");
        if (root == null)
        {
            root = new GameObject("PersistentRoot");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SaveSystem>();
            root.AddComponent<ConfigManager>();
            root.AddComponent<GameSceneManager>();
            Debug.Log("[AutoInit] PersistentRoot 已创建（直接Play Battle场景时）");
        }
        if (root.GetComponent<StoryDirector>() == null)
            root.AddComponent<StoryDirector>();
        if (root.GetComponent<TutorialDirector>() == null)
            root.AddComponent<TutorialDirector>();
    }

    // ===== 场景基础对象 =====

    /// <summary>
    /// 运行时保证 CharacterBar 在 BattleUI 下可见，sorting 高于 map。
    /// （不再塞进 BackpackPanel，避免被 Mask/WorldSpace 裁掉）
    /// </summary>
    static void EnsureCharacterBarVisibleRuntime()
    {
        BattleUI battleUI = Object.FindObjectOfType<BattleUI>();
        if (battleUI == null) return;
        Transform bar = FindDeepChildIgnoreCase(battleUI.transform, "CharacterBar");
        if (bar == null) return;

        // 禁止强行改父节点：会打乱美术锚点，造成头像「位置/分辨率」错乱
        // 仅当被 map（sorting=10）挡住时提高 overrideSorting
        bar.gameObject.SetActive(true);
        EnsureNestedSortOrder(bar, 110);

        for (int i = 0; i < bar.childCount; i++)
            bar.GetChild(i).gameObject.SetActive(true);

        Debug.Log("[AutoInit] CharacterBar 可见 sorting=110（保留原父节点）");
    }

    /// <summary>已废弃：把头像塞进背包会被裁切，改用 EnsureCharacterBarVisibleRuntime</summary>
    static void NestCharacterBarIntoBackpack()
    {
        EnsureCharacterBarVisibleRuntime();
    }

    /// <summary>
    /// 世界锚点规范化：禁止 Destroy RectTransform/Canvas（Unity 会报错且弄坏 UI）。
    /// 仅重置锚点便于读世界坐标。
    /// </summary>
    static void NormalizeToWorldTransform(GameObject go)
    {
        if (go == null) return;
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        Vector3 worldPos = go.transform.position;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        go.transform.position = worldPos;
    }

    /// <summary>
    /// 把世界对象归入 WorldRoot 父节点（保持世界坐标不变）
    /// </summary>
    static void ReparentToWorldRoot(GameObject go, Transform worldRoot)
    {
        if (go == null || worldRoot == null) return;
        if (go.transform.parent == worldRoot) return;
        // 如果挂在Canvas下，先分离到场景根，再挂到WorldRoot
        go.transform.SetParent(worldRoot, true); // worldPositionStays=true 保持世界坐标
    }

    static Camera EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 20000f; // 足够大，兼容用户预制体中 z=-16000 的子节点
            camGo.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(-7f, 0f, -10f);
            cam.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        // 相机已存在时不再覆盖用户配置，仅确保cullingMask和farClipPlane正确
        cam.cullingMask = ~0; // 所有层
        if (cam.farClipPlane < 20000f)
            cam.farClipPlane = 20000f;
        if (cam.orthographic)
            cam.orthographicSize = BattleViewportFit.ResolveOrthoSize();

        // 【修复】URP下相机ClearFlags为Skybox但无天空盒时，画面会变暗/黑
        // 强制改为纯色清屏 + 亮背景色，保证背景可见
        if (cam.clearFlags == CameraClearFlags.Skybox)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.78f, 0.95f, 1f); // 亮天蓝
            Debug.Log("[AutoInit] 相机ClearFlags已改为SolidColor(消除暗屏)");
        }

        Debug.Log($"[AutoInit] 相机就绪: pos={cam.transform.position}, ortho={cam.orthographicSize:F2}");

        return cam;
    }

    /// <summary>
    /// 给相机添加跟随脚本，确保角色始终在屏幕内可见
    /// </summary>
    static void EnsureCameraFollow(Camera cam, Transform heroTransform, Transform spawnPoint, Transform endPoint)
    {
        if (cam == null) return;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);
        follow.smoothTime = 0f; // 硬跟随，避免视差相对速度忽快忽慢
        float spawnX = spawnPoint != null ? spawnPoint.position.x : -7f;
        float endX = endPoint != null ? endPoint.position.x : 13f;
        follow.minX = spawnX - 0.5f;
        // 无限跑图：不要用短 EndPoint 锁死镜头，BattleManager 会随进度再 Extend
        follow.maxX = Mathf.Max(endX + 2f, spawnX + 80f);

        // Y 交给 AlignBattleViewport；这里只锁当前 Y/Z 并跟随 X
        follow.LockYZFromCurrent();
        if (heroTransform != null)
            follow.SetTarget(heroTransform);

        Debug.Log($"[AutoInit] CameraFollow X跟随就绪 minX={follow.minX:F1} maxX={follow.maxX:F1}");
    }

    static Transform EnsureSpawnPoint(Transform worldRoot)
    {
        GameObject sp = GameObject.Find("SpawnPoint");
        if (sp != null)
        {
            // 如果在Canvas下，分离到场景根级别（Canvas坐标系与世界坐标系不兼容）
            Canvas parentCanvas = sp.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                sp.transform.SetParent(null);
                Debug.Log("[AutoInit] SpawnPoint从Canvas中分离到场景根");
            }
        }
        else
        {
            sp = new GameObject("SpawnPoint");
        }
        // 【关键】把RectTransform/Canvas根节点转成普通Transform
        // 世界锚点必须用普通Transform，否则spawnPoint.position读取会受RectTransform换算干扰
        NormalizeToWorldTransform(sp);
        // 挂到WorldRoot父节点下统一控制层级（保持世界坐标）
        ReparentToWorldRoot(sp, worldRoot);
        // 尊重用户摆放的 SpawnPoint；仅未摆放时用默认左侧
        Vector3 cur = sp.transform.position;
        if (IsUnsetWorldAnchor(cur))
        {
            float gy = SampleDefaultGroundY(worldRoot);
            GameConfig.SetWorldPosition(sp, new Vector3(-7f, gy, 0f));
            Debug.Log($"[AutoInit] SpawnPoint未摆放，已兜底: pos={sp.transform.position}");
        }
        else
        {
            Debug.Log($"[AutoInit] SpawnPoint保留用户设定: pos={sp.transform.position}");
        }
        return sp.transform;
    }

    static Transform EnsureEndPoint(Transform worldRoot)
    {
        GameObject ep = GameObject.Find("EndPoint");
        if (ep != null)
        {
            Canvas parentCanvas = ep.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                ep.transform.SetParent(null);
                Debug.Log("[AutoInit] EndPoint从Canvas中分离到场景根");
            }
        }
        else
        {
            ep = new GameObject("EndPoint");
            SpriteRenderer eSr = ep.AddComponent<SpriteRenderer>();
            eSr.color = new Color(1f, 0.8f, 0.2f);
            eSr.sortingLayerName = "Ground";
            eSr.sortingOrder = 1;
            ep.transform.localScale = new Vector3(0.5f, 3f, 1f);
        }
        // 【关键】把RectTransform/Canvas根节点转成普通Transform
        NormalizeToWorldTransform(ep);
        // 挂到WorldRoot父节点下统一控制层级（保持世界坐标）
        ReparentToWorldRoot(ep, worldRoot);
        // 尊重用户设定：只在未摆放（接近原点）时兜底，禁止用 y>0 判废
        Vector3 cur = ep.transform.position;
        if (IsUnsetWorldAnchor(cur))
        {
            float gy = SampleDefaultGroundY(worldRoot);
            GameConfig.SetWorldPosition(ep, new Vector3(8f, gy, 0f));
            Debug.Log($"[AutoInit] EndPoint未摆放，已兜底: pos={ep.transform.position}");
        }
        else
        {
            Debug.Log($"[AutoInit] EndPoint保留用户设定: pos={ep.transform.position}");
        }
        return ep.transform;
    }

    static Transform[] EnsureMonsterSpawnPoints(Transform worldRoot)
    {
        // 收集场景中已有的 MonsterSpawn_*（支持用户摆放 0..N）
        var found = new List<Transform>();
        var all = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.gameObject == null) continue;
            string n = t.gameObject.name;
            if (!n.StartsWith("MonsterSpawn")) continue;
            // 跳过容器节点（如名为 Monster 的父物体），只收具体点
            if (n == "MonsterSpawn" || n == "MonsterSpawns") continue;

            Canvas parentCanvas = t.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                t.SetParent(null);
                Debug.Log($"[AutoInit] {n}从Canvas中分离到场景根");
            }
            NormalizeToWorldTransform(t.gameObject);
            // 先记下世界坐标（此时若仍挂在 Ground scale.x=30 下，position 已是正确世界值）
            Vector3 worldPos = t.position;
            ReparentToWorldRoot(t.gameObject, worldRoot);
            t.localScale = Vector3.one;
            GameConfig.SetWorldPosition(t, worldPos);

            Vector3 cur = t.position;
            if (IsUnsetWorldAnchor(cur))
            {
                float gy = SampleDefaultGroundY(worldRoot);
                GameConfig.SetWorldPosition(t, new Vector3(4f + found.Count * 1.5f, gy, 0f));
                Debug.Log($"[AutoInit] {n}未摆放，已兜底: pos={t.position}");
            }
            else
            {
                Debug.Log($"[AutoInit] {n}保留用户设定: pos={t.position}");
            }
            found.Add(t);
        }

        // 按编号数字排序（MonsterSpawn_0..N），再写入 BM
        found.Sort((a, b) =>
        {
            int Parse(string n)
            {
                int us = n.LastIndexOf('_');
                if (us >= 0 && int.TryParse(n.Substring(us + 1), out int v)) return v;
                return 0;
            }
            int c = Parse(a.name).CompareTo(Parse(b.name));
            if (c != 0) return c;
            return a.position.x.CompareTo(b.position.x);
        });

        // 场景一个都没有时，创建 3 个默认点
        if (found.Count == 0)
        {
            float gy = SampleDefaultGroundY(worldRoot);
            for (int i = 0; i < 3; i++)
            {
                var sp = new GameObject($"MonsterSpawn_{i}");
                ReparentToWorldRoot(sp, worldRoot);
                GameConfig.SetWorldPosition(sp, new Vector3(4f + i * 1.5f, gy, 0f));
                found.Add(sp.transform);
                Debug.Log($"[AutoInit] 创建默认 {sp.name}: pos={sp.transform.position}");
            }
        }

        Debug.Log($"[AutoInit] 怪物出生点共 {found.Count} 个 (按编号排序)");
        return found.ToArray();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    // ===== GameRoot 和系统组件 =====

    /// <summary>
    /// GameRoot：所有常驻系统的宿主。
    /// 系统单例可能已被 Singleton getter 提前创建在别的物体上，
    /// 此时必须复用那个物体，否则重复组件会被销毁、系统引用错乱。
    /// </summary>
    static GameObject EnsureGameRoot()
    {
        GameObject root = null;

        // 单例可能已被 getter 提前创建，甚至挂在未激活物体上；必须复用它
        BattleManager existing = BattleManager.Instance;
        if (existing != null) root = existing.gameObject;

        if (root == null) root = GameObject.Find("GameRoot");
        if (root == null) root = new GameObject("GameRoot");
        if (root.name != "GameRoot") root.name = "GameRoot";

        if (!root.activeSelf)
        {
            Debug.LogWarning("[AutoInit] GameRoot 处于未激活状态 → 已强制激活");
            root.SetActive(true);
        }

        Object.DontDestroyOnLoad(root);

        AddIfMissing<PoolManager>(root);
        AddIfMissing<ChapterManager>(root);
        AddIfMissing<GridBackpackSystem>(root);
        AddIfMissing<UIManager>(root);
        AddIfMissing<DamageTextSystem>(root);
        AddIfMissing<MonsterSpriteLoader>(root);
        AddIfMissing<SkillSystem>(root);
        AddIfMissing<SkillRegistry>(root);
        AddIfMissing<BattleStateSaver>(root);
        AddIfMissing<AchievementSystem>(root);
        AddIfMissing<PreLevelSystem>(root);
        AddIfMissing<TownSystem>(root);
        AddIfMissing<MercenaryManager>(root);
        AddIfMissing<BattleManager>(root);
        AddIfMissing<StoryDirector>(root);
        AddIfMissing<TutorialDirector>(root);

        return root;
    }

    /// <summary>场景里已存在该系统时复用，避免重复组件被 Singleton 销毁</summary>
    static void AddIfMissing<T>(GameObject root) where T : MonoBehaviour
    {
        T inScene = Object.FindObjectOfType<T>();
        if (inScene != null)
        {
            // 已在别处：把它并到 GameRoot 下便于统一管理（组件无法搬家，仅保证不再重复添加）
            return;
        }
        if (root.GetComponent<T>() == null)
            root.AddComponent<T>();
    }

    // ===== Hero =====

    static Hero EnsureHero(Transform parent, BattleManager bm)
    {
        Hero hero = Object.FindObjectOfType<Hero>();
        if (hero != null)
        {
            GameConfig.AttachToUnitRoot(hero.transform);
            float s = GameConfig.UNIT_SCALE;
            float sign = hero.transform.localScale.x < 0 ? -1f : 1f;
            hero.transform.localScale = new Vector3(sign * s, s, s);
            bm.hero = hero;
            return hero;
        }

        // 加载SPUM玩家预制体
        GameObject heroPrefab = Resources.Load<GameObject>("Units/wanjia");
        GameObject heroGo;
        Rigidbody2D heroRb;
        SpriteRenderer heroSr;

        if (heroPrefab != null)
        {
            // 实例化，不保持世界坐标（预制体有RectTransform和奇怪的初始位置）
            heroGo = Object.Instantiate(heroPrefab, parent, false);
            heroGo.name = "Hero";
            heroGo.tag = "Player";

            // 重置位置和旋转，由BattleManager设置最终位置
            // RectTransform 根节点用 anchoredPosition3D 设置，普通 Transform 用 position
            GameConfig.SetWorldPosition(heroGo, parent.position);
            heroGo.transform.localRotation = Quaternion.identity;
            // 统一缩放：普通单位 100
            heroGo.transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;

            // 【关键修复1】处理SPUM预制体根节点的RectTransform
            // SPUM预制体根是RectTransform（UI组件），但我们用在世界空间
            // 强制把pivot设为中心，同时保存/恢复世界位置，避免被重置到 (0,0)
            RectTransform rootRT = heroGo.GetComponent<RectTransform>();
            if (rootRT != null)
            {
                Vector3 worldPos = heroGo.transform.position;
                rootRT.anchorMin = new Vector2(0.5f, 0.5f);
                rootRT.anchorMax = new Vector2(0.5f, 0.5f);
                rootRT.pivot = new Vector2(0.5f, 0.5f);
                rootRT.sizeDelta = new Vector2(100, 100);
                GameConfig.SetWorldPosition(heroGo, worldPos);
                Debug.Log("[AutoInit] Hero根RectTransform已重置");
            }

            // 添加/获取Rigidbody2D
            heroRb = heroGo.GetComponent<Rigidbody2D>();
            if (heroRb == null) heroRb = heroGo.AddComponent<Rigidbody2D>();
            heroRb.gravityScale = 0;
            heroRb.freezeRotation = true;

            heroSr = null;
            SpriteRenderer[] allSrs = heroGo.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in allSrs)
            {
                if (sr.sprite != null)
                {
                    heroSr = sr;
                    break;
                }
            }
            if (heroSr == null && allSrs.Length > 0)
                heroSr = allSrs[0];
            foreach (var sr in allSrs)
            {
                Color c = sr.color;
                c.a = 1f;
                sr.color = c;
                sr.enabled = true;
            }

            hero = heroGo.AddComponent<Hero>();
            hero.rb = heroRb;
            hero.sr = heroSr;

            Vector3 heroPos = heroGo.transform.position;
            heroPos.z = parent != null ? parent.position.z : -5f;
            GameConfig.SetWorldPosition(heroGo, heroPos);

            Debug.Log($"[AutoInit] SPUM玩家已加载: SpriteRenderer={allSrs.Length}, " +
                      $"pos={heroGo.transform.position}, scale={heroGo.transform.localScale}, parent={heroGo.transform.parent?.name}");
        }
        else
        {
            heroGo = new GameObject("Hero");
            heroGo.transform.SetParent(parent, false);
            heroGo.tag = "Player";
            heroSr = heroGo.AddComponent<SpriteRenderer>();
            heroSr.color = new Color(0.2f, 0.4f, 0.8f);
            heroSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            heroSr.sortingOrder = GameConfig.SORT_UNIT;
            heroGo.transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;
            heroRb = heroGo.AddComponent<Rigidbody2D>();
            heroRb.gravityScale = 0;
            heroRb.freezeRotation = true;
            hero = heroGo.AddComponent<Hero>();
            hero.rb = heroRb;
            hero.sr = heroSr;
            Debug.LogWarning("[AutoInit] wanjia.prefab未找到，使用兜底方块");
        }

        // 换装管理器
        HeroCostumeManager costumeMgr = heroGo.GetComponent<HeroCostumeManager>();
        if (costumeMgr == null)
            heroGo.AddComponent<HeroCostumeManager>();

        bm.hero = hero;
        return hero;
    }

    // ===== 确保ParallaxBackground =====

    /// <summary>
    /// 在 BattleUI 的 map 节点上仅配置已有视差组件；不添加 Canvas / GraphicRaycaster 等层级组件。
    /// </summary>
    static void EnsureParallaxOnMaproot()
    {
        BattleUI battleUI = Object.FindObjectOfType<BattleUI>();
        if (battleUI == null) return;

        Transform mapTransform = FindDeepChildIgnoreCase(battleUI.transform, "map");
        if (mapTransform == null)
            mapTransform = FindDeepChildIgnoreCase(battleUI.transform, "Maproot");
        if (mapTransform == null)
            mapTransform = FindDeepChildIgnoreCase(battleUI.transform, "Map");
        if (mapTransform == null) return;

        // 只补视差逻辑组件，不改 map 的 Canvas/层级（用户已调好）
        ParallaxBackground parallax = mapTransform.GetComponent<ParallaxBackground>();
        if (parallax == null)
            parallax = mapTransform.gameObject.AddComponent<ParallaxBackground>();

        Transform layerRoot = FindDirectChildIgnoreCase(mapTransform, "main");
        if (layerRoot == null) layerRoot = mapTransform;
        parallax.SetLayerRoot(layerRoot);
        Debug.Log($"[AutoInit] map 视差组件就绪 layerRoot={layerRoot.name}");
    }

    static Transform FindDirectChildIgnoreCase(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    // ===== Monster预制体 =====

    static void EnsureMonsterPrefab()
    {
        if (PoolManager.Instance == null)
        {
            Debug.LogError("[AutoInit] PoolManager 未就绪，无法加载怪物预制体");
            return;
        }
        if (PoolManager.Instance._monsterPrefab != null) return;

        // 优先 Monstersmoban，其次 Monster.prefab
        GameObject monsterPrefabObj = Resources.Load<GameObject>("Prefabs/Monster/Monstersmoban");
        if (monsterPrefabObj == null)
            monsterPrefabObj = Resources.Load<GameObject>("Prefabs/Monster/Monster");
        if (monsterPrefabObj == null)
            monsterPrefabObj = Resources.Load<GameObject>("Monster");
#if UNITY_EDITOR
        if (monsterPrefabObj == null)
            monsterPrefabObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Monster/Monstersmoban.prefab");
        if (monsterPrefabObj == null)
            monsterPrefabObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Monster/Monster.prefab");
#endif

        if (monsterPrefabObj != null)
        {
            PoolManager.Instance.Preload("Monster", monsterPrefabObj, 8);
            PoolManager.Instance._monsterPrefab = monsterPrefabObj;
            Debug.Log($"[AutoInit] 怪物预制体已入池: {monsterPrefabObj.name} path=Prefabs/Monster/");
        }
        else
        {
            Debug.LogError("[AutoInit] 怪物预制体未找到：Resources/Prefabs/Monster/Monstersmoban 或 Monster");
        }
    }

    // ===== 辅助 =====

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// 大小写不敏感的深度查找子物体
    /// </summary>
    static Transform FindDeepChildIgnoreCase(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (string.Equals(child.gameObject.name, name, System.StringComparison.OrdinalIgnoreCase))
                return child;
            var result = FindDeepChildIgnoreCase(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
