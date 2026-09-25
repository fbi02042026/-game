#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor工具：生成 MonsterHealthBar.prefab（怪物血条预制体）
/// 菜单：Tools → 生成怪物血条预制体
/// 生成后可拖到Monster预制体下，调整位置
/// </summary>
public class MonsterHealthBarGenerator : EditorWindow
{
    [MenuItem("Tools/生成怪物血条预制体")]
    public static void GenerateHealthBarPrefab()
    {
        // 1. 确保目标目录存在（统一 Resources/Prefabs）
        string dir = "Assets/Resources/Prefabs/Monster";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Monster");

        // 2. 创建血条根节点
        GameObject barRoot = new GameObject("MonsterHealthBar");
        RectTransform rootRT = barRoot.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(0.3f, 0.03f);
        rootRT.localScale = Vector3.one;

        // 添加Canvas（世界空间）
        Canvas canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Effects";
        canvas.sortingOrder = 15;

        // 添加MonsterHealthBar组件
        MonsterHealthBar healthBar = barRoot.AddComponent<MonsterHealthBar>();
        healthBar.barWidth = 0.3f;
        healthBar.barHeight = 0.03f;
        healthBar.footDropWorld = -0.05f;

        // ---- 背景 Image ----
        GameObject bgGo = new GameObject("HPBg");
        bgGo.transform.SetParent(barRoot.transform, false);
        RectTransform bgRT = bgGo.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.sizeDelta = new Vector2(0.3f, 0.03f);
        bgRT.anchoredPosition = Vector2.zero;
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0f, 0f, 0.8f);
        bgImg.raycastTarget = false;

        // ---- 填充 Image ----
        GameObject fillGo = new GameObject("HPFill");
        fillGo.transform.SetParent(bgGo.transform, false);
        RectTransform fillRT = fillGo.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0.5f);
        fillRT.anchorMax = new Vector2(1f, 0.5f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        fillRT.sizeDelta = new Vector2(0, 0.03f);
        fillRT.anchoredPosition = Vector2.zero;
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
        fillImg.raycastTarget = false;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        // 3. 保存为预制体
        string prefabPath = dir + "/MonsterHealthBar.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(barRoot, prefabPath);

        // 4. 清理临时对象
        DestroyImmediate(barRoot);

        Debug.Log($"[MonsterHealthBarGenerator] 血条预制体已生成: {prefabPath}");
        EditorGUIUtility.PingObject(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif