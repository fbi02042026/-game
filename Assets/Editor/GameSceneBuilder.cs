#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏场景生成工具
/// 菜单：Tools → 生成游戏场景
///
/// 生成3个场景文件并自动添加到Build Settings：
/// - Scenes/Boot.unity   : 启动场景，初始化存档后自动跳Town
/// - Scenes/Town.unity   : 城镇主菜单（标题+开始冒险按钮）
/// - Scenes/Battle.unity : 战斗场景（预置Camera/Ground/SpawnPoint等）
///
/// 使用方式：
/// 1. 切回Unity编辑器
/// 2. 点击菜单 Tools → 生成游戏场景
/// 3. 等待3个场景生成完成
/// 4. File → Build Settings 确认场景顺序（Boot/Town/Battle）
/// 5. 双击 Scenes/Boot.unity 打开启动场景
/// 6. 按Play运行
/// </summary>
public class GameSceneBuilder : EditorWindow
{
    [MenuItem("Tools/生成游戏场景")]
    public static void ShowWindow()
    {
        GetWindow<GameSceneBuilder>("场景生成器");
    }

    void OnGUI()
    {
        GUILayout.Label("游戏场景生成器", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("将生成以下3个场景：", EditorStyles.wordWrappedLabel);
        GUILayout.Label("  1. Scenes/Boot.unity   - 启动场景");
        GUILayout.Label("  2. Scenes/Town.unity   - 城镇主菜单");
        GUILayout.Label("  3. Scenes/Battle.unity - 战斗场景");
        GUILayout.Space(10);

        GUILayout.Label("生成后请确保 Build Settings 中的场景顺序正确：\nBoot (0) → Town (1) → Battle (2)", EditorStyles.wordWrappedLabel);
        GUILayout.Space(20);

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        if (GUILayout.Button("生成全部场景", GUILayout.Height(50)))
        {
            BuildAllScenes();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        if (GUILayout.Button("仅生成 Battle 场景"))
        {
            BuildBattleSceneOnly();
        }
    }

    static void BuildAllScenes()
    {
        EnsureScenesFolder();

        // 保存当前场景（如果有未保存的修改）
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[GameSceneBuilder] 用户取消了场景生成");
            return;
        }

        // 1. Boot 场景
        var bootScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateBootObjects();
        string bootPath = "Assets/Scenes/Boot.unity";
        EditorSceneManager.SaveScene(bootScene, bootPath);
        Debug.Log($"[GameSceneBuilder] Boot场景已保存: {bootPath}");

        // 2. Town 场景
        var townScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateTownObjects();
        string townPath = "Assets/Scenes/Town.unity";
        EditorSceneManager.SaveScene(townScene, townPath);
        Debug.Log($"[GameSceneBuilder] Town场景已保存: {townPath}");

        // 3. Battle 场景
        var battleScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateBattleObjects();
        string battlePath = "Assets/Scenes/Battle.unity";
        EditorSceneManager.SaveScene(battleScene, battlePath);
        Debug.Log($"[GameSceneBuilder] Battle场景已保存: {battlePath}");

        // 添加到 Build Settings
        AddToBuildSettings(new[] { bootPath, townPath, battlePath });

        // 打开 Boot 场景
        EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);

        Debug.Log("[GameSceneBuilder] ✅ 全部场景生成完成！按Play即可运行游戏。");
        EditorUtility.DisplayDialog("场景生成完成",
            "3个场景已生成并添加到Build Settings。\n\n" +
            "当前打开的是 Boot 场景，直接按 Play 即可运行游戏。\n\n" +
            "场景切换流程：Boot → Town → Battle",
            "确定");
    }

    static void BuildBattleSceneOnly()
    {
        EnsureScenesFolder();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var battleScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateBattleObjects();
        string battlePath = "Assets/Scenes/Battle.unity";
        EditorSceneManager.SaveScene(battleScene, battlePath);

        Debug.Log($"[GameSceneBuilder] Battle场景已保存: {battlePath}");
        EditorUtility.DisplayDialog("Battle场景生成完成",
            "Battle场景已生成。\n\n可以直接Play测试战斗。", "确定");
    }

    // ===== Boot 场景 =====

    static void CreateBootObjects()
    {
        // 1. Main Camera（Boot是过渡场景，但也需要相机渲染黑屏）
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f); // 深色背景
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0, 0, -10);

        // 2. Directional Light（保证场景不为全黑）
        GameObject lightGo = new GameObject("Directional Light");
        UnityEngine.Light light = lightGo.AddComponent<UnityEngine.Light>();
        light.type = UnityEngine.LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0);

        // 3. BootManager
        GameObject bootMgr = new GameObject("BootManager");
        bootMgr.AddComponent<BootManager>();

        Debug.Log("[GameSceneBuilder] Boot场景对象已创建: Camera, Light, BootManager");
    }

    // ===== Town 场景 =====

    static void CreateTownObjects()
    {
        // 1. Main Camera
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f); // 深蓝灰背景
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0, 0, -10);

        // 2. Directional Light
        GameObject lightGo = new GameObject("Directional Light");
        UnityEngine.Light light = lightGo.AddComponent<UnityEngine.Light>();
        light.type = UnityEngine.LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0);

        // 3. EventSystem（UI交互必需）
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 4. TownSceneManager（运行时自动创建Canvas/按钮等UI）
        GameObject townMgr = new GameObject("TownSceneManager");
        townMgr.AddComponent<TownSceneManager>();

        Debug.Log("[GameSceneBuilder] Town场景对象已创建: Camera, Light, EventSystem, TownSceneManager");
    }

    // ===== Battle 场景 =====

    static void CreateBattleObjects()
    {
        // 1. Main Camera
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0, 0, -10);

        // 注意：不创建Ground对象，地面由BattleUI的Map视差背景提供
        // 单位Y坐标由SpawnPoint的Y决定（AutoGameInitializer会读取）

        // 2. SpawnPoint（玩家出生点，Y坐标=地面高度，可在Inspector中调整）
        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(-7f, -3.5f, 0);

        // 3. EndPoint（终点/下一关传送点）
        GameObject endPoint = new GameObject("EndPoint");
        endPoint.transform.position = new Vector3(8f, -3.5f, 0);

        // 4. MonsterSpawnPoints（怪物刷新点）
        for (int i = 0; i < 3; i++)
        {
            GameObject sp = new GameObject($"MonsterSpawn_{i}");
            sp.transform.position = new Vector3(4f + i * 1.5f, -3.5f, 0);
        }

        // 5. EventSystem
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // 6. AutoGameInitializer
        GameObject autoInit = new GameObject("AutoGameInitializer");
        autoInit.AddComponent<AutoGameInitializer>();

        Debug.Log("[GameSceneBuilder] Battle场景对象已创建: Camera, SpawnPoint, EndPoint, MonsterSpawnPoints, EventSystem, AutoGameInitializer");
    }

    // ===== Build Settings =====

    static void AddToBuildSettings(string[] scenePaths)
    {
        var buildScenes = new List<EditorBuildSettingsScene>();

        foreach (string path in scenePaths)
        {
            buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log("[GameSceneBuilder] Build Settings 已更新");
    }

    static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
#endif
