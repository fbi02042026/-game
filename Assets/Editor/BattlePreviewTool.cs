#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战斗场景编辑器预览工具
/// 菜单：Tools → 战斗场景预览
///
/// 在编辑模式下（非运行时）临时生成角色和怪物预览，方便在Scene视图中查看布局
/// - 显示玩家位置（蓝色方块）
/// - 显示佣兵位置（绿色方块）
/// - 显示怪物位置（红色方块，加载实际怪物精灵）
/// - 显示视差背景层
/// 一键清除预览
/// </summary>
public class BattlePreviewWindow : EditorWindow
{
    private const string PREVIEW_TAG = "BattlePreview";

    private int _previewChapter = 0;
    private static readonly string[] CHAPTER_NAMES = {
        "1 - Undead (亡灵)", "2 - Jungle (丛林)", "3 - Sea (海洋)",
        "4 - Forest (森林)", "5 - Field (田野)", "6 - Cave (洞穴)",
        "7 - Devil (恶魔)", "8 - Ice (冰霜)"
    };
    private static readonly string[] CHAPTER_FOLDERS = {
        "1 Undead", "2 Jungle", "3 Sea", "4 Forest",
        "5 Field", "6 Cave", "7 Devil", "8 Ice"
    };

    private GameObject _previewRoot;

    [MenuItem("Tools/战斗场景预览")]
    public static void ShowWindow()
    {
        GetWindow<BattlePreviewWindow>("战斗场景预览");
    }

    void OnGUI()
    {
        GUILayout.Label("战斗场景预览工具", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 检查是否在Battle场景
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool isBattleScene = activeScene.name == "Battle";

        if (!isBattleScene)
        {
            EditorGUILayout.HelpBox("请先打开 Battle 场景！\n当前场景: " + activeScene.name, MessageType.Warning);
            if (GUILayout.Button("打开 Battle 场景", GUILayout.Height(30)))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            return;
        }

        EditorGUILayout.HelpBox(
            "此工具在编辑模式下临时生成角色和怪物预览。\n" +
            "预览对象仅为可视化参考，不会保存到场景文件。\n" +
            "清除预览后会恢复原始场景。",
            MessageType.Info);

        GUILayout.Space(10);

        _previewChapter = EditorGUILayout.Popup("预览怪物章节", _previewChapter, CHAPTER_NAMES);

        GUILayout.Space(10);

        // 预览按钮
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        if (GUILayout.Button("生成预览", GUILayout.Height(40)))
        {
            GeneratePreview();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("清除预览", GUILayout.Height(40)))
        {
            ClearPreview();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(15);

        // 位置信息
        GUILayout.Label("场景位置参考", EditorStyles.boldLabel);
        GUILayout.Label("  玩家(Hero):    (-7, -3.5)  蓝色", EditorStyles.miniLabel);
        GUILayout.Label("  佣兵1(Merc1):   (-1, -3.5)  绿色", EditorStyles.miniLabel);
        GUILayout.Label("  佣兵2(Merc2):   (-1.5, -3.5) 绿色", EditorStyles.miniLabel);
        GUILayout.Label("  怪物0(Monster0): (4, -3.5)   红色", EditorStyles.miniLabel);
        GUILayout.Label("  怪物1(Monster1): (5.5, -3.5)  红色", EditorStyles.miniLabel);
        GUILayout.Label("  怪物2(Monster2): (7, -3.5)   红色", EditorStyles.miniLabel);
        GUILayout.Label("  终点(EndPoint):  (8, -3.5)   黄色", EditorStyles.miniLabel);

        GUILayout.Space(10);

        if (GUILayout.Button("Frame全部预览对象 (F键)"))
        {
            if (_previewRoot != null)
            {
                Selection.activeGameObject = _previewRoot;
                SceneView.lastActiveSceneView.Frame(new Bounds(_previewRoot.transform.position, Vector3.one * 15), false);
            }
        }
    }

    void GeneratePreview()
    {
        ClearPreview();

        _previewRoot = new GameObject("[BattlePreview]");

        // === 玩家位置标记 ===
        CreatePositionMarker(_previewRoot.transform, "Hero_Pos", new Vector3(-7f, -3.5f, 0), new Color(0.2f, 0.4f, 0.8f), "Hero");

        // === 佣兵位置标记 ===
        CreatePositionMarker(_previewRoot.transform, "Merc1_Pos", new Vector3(-1f, -3.5f, 0), new Color(0.2f, 0.8f, 0.3f), "Merc1");
        CreatePositionMarker(_previewRoot.transform, "Merc2_Pos", new Vector3(-1.5f, -3.5f, 0), new Color(0.2f, 0.8f, 0.3f), "Merc2");

        // === 怪物预览（加载实际精灵）===
        string chapterFolder = CHAPTER_FOLDERS[_previewChapter];
        string spritePath = $"Assets/Resources/Config/MonsterSpriteRegistry/{chapterFolder}";

        // 尝试加载3个怪物精灵
        for (int i = 0; i < 3; i++)
        {
            Vector3 monsterPos = new Vector3(4f + i * 1.5f, -3.5f, 0);
            CreateMonsterPreview(_previewRoot.transform, $"Monster{i}_Preview", monsterPos, spritePath, i);
        }

        // === 尝试加载Hero预制体预览 ===
        TryCreateHeroPreview(_previewRoot.transform);

        // === 尝试加载BattleUI预览 ===
        TryCreateBattleUIPreview(_previewRoot.transform);

        // === 视差背景层标记 ===
        CreateParallaxLayerMarker(_previewRoot.transform, "Parallax_Back", new Color(0.3f, 0.5f, 0.7f, 0.3f), -2f);
        CreateParallaxLayerMarker(_previewRoot.transform, "Parallax_Mid", new Color(0.5f, 0.6f, 0.4f, 0.3f), 0f);
        CreateParallaxLayerMarker(_previewRoot.transform, "Parallax_Front", new Color(0.7f, 0.5f, 0.3f, 0.3f), 2f);

        // 选中预览根节点
        Selection.activeGameObject = _previewRoot;

        // Frame到预览区域
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, -2f, 0), new Vector3(20f, 8f, 1f)), false);
        }

        Debug.Log("[BattlePreview] 预览已生成！在Scene视图中查看。");
        ShowNotification(new GUIContent("预览已生成！"));
    }

    /// <summary>
    /// 创建位置标记方块
    /// </summary>
    void CreatePositionMarker(Transform parent, string name, Vector3 pos, Color color, string label)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.5f, 1.5f, 0.1f) * GameConfig.UNIT_SCALE;

        // 设置颜色
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            r.sharedMaterial = mat;
        }

        // 添加标签
        AddLabel(go, label);
    }

    /// <summary>
    /// 创建怪物预览（加载实际精灵）
    /// </summary>
    void CreateMonsterPreview(Transform parent, string name, Vector3 pos, string spritePath, int index)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        // 尝试加载怪物精灵
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spritePath });
        if (guids.Length > index)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.flipX = true; // 怪物面向左
                go.transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;
                Debug.Log($"[BattlePreview] 怪物预览加载: {path}");
            }
        }
        else
        {
            // 没有精灵就用红色方块
            sr.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            sr.transform.localScale = new Vector3(1f, 1.5f, 1f) * GameConfig.UNIT_SCALE;
            Debug.LogWarning($"[BattlePreview] 章节 {_previewChapter} 第 {index} 个怪物精灵未找到");
        }

        AddLabel(go, $"Monster{index}");
    }

    /// <summary>
    /// 尝试加载Hero预制体预览
    /// </summary>
    void TryCreateHeroPreview(Transform parent)
    {
        // 尝试从SPUM Resources加载
        GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SPUM/Resources/Units/wanjia.prefab");
        if (heroPrefab != null)
        {
            GameObject hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, parent);
            hero.name = "Hero_Preview";
            hero.transform.position = new Vector3(-7f, -3.5f, 0);
            hero.transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;

            // 翻转面向右
            SpriteRenderer sr = hero.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.flipX = false;

            Debug.Log("[BattlePreview] Hero预览已加载");
        }
        else
        {
            Debug.LogWarning("[BattlePreview] wanjia.prefab 未找到，使用占位标记");
        }
    }

    /// <summary>
    /// 尝试加载BattleUI预览
    /// </summary>
    void TryCreateBattleUIPreview(Transform parent)
    {
        GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Battle/BattleUI.prefab");
        if (uiPrefab != null)
        {
            GameObject ui = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, parent);
            ui.name = "BattleUI_Preview";
            Debug.Log("[BattlePreview] BattleUI预览已加载");
        }
    }

    /// <summary>
    /// 创建视差层标记
    /// </summary>
    void CreateParallaxLayerMarker(Transform parent, string name, Color color, float zOffset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0, 0, zOffset);
        go.transform.localScale = new Vector3(20f, 5f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingOrder = zOffset < 0 ? -10 : (zOffset > 0 ? 10 : 0);

        AddLabel(go, name);
    }

    /// <summary>
    /// 给GameObject添加文字标签
    /// </summary>
    void AddLabel(GameObject go, string text)
    {
        // 用Gizmo在OnGUI中不太方便，这里用一个子TextMesh
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = new Vector3(0, 1.5f, 0);
        TextMesh tm = labelGo.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.color = Color.white;
        tm.alignment = TextAlignment.Center;
        MeshRenderer mr = labelGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 100;
    }

    /// <summary>
    /// 清除预览
    /// </summary>
    void ClearPreview()
    {
        if (_previewRoot != null)
        {
            DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }

        // 也清除任何遗留的预览对象
        GameObject[] allObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in allObjects)
        {
            if (go.name == "[BattlePreview]")
            {
                DestroyImmediate(go);
            }
        }

        ShowNotification(new GUIContent("预览已清除"));
        Debug.Log("[BattlePreview] 预览已清除");
    }

    void OnDestroy()
    {
        // 窗口关闭时自动清除预览
        if (_previewRoot != null)
        {
            ClearPreview();
        }
    }
}
#endif
