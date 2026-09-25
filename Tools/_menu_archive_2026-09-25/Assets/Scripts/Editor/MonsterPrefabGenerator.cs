#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor工具：一键生成 Monster.prefab（怪物预制体）+ MonsterHealthBar.prefab（血条预制体）
/// 菜单：Tools → 生成怪物预制体
/// 
/// 生成后：
/// 1. Monster.prefab 包含 SpriteRenderer + Rigidbody2D + Monster + UnitAnimation + 血条子节点
/// 2. MonsterHealthBar.prefab 是独立血条预制体（也在 Monster 目录下）
/// 3. 在 Monster.prefab 中可直接调整血条位置
/// </summary>
public class MonsterPrefabGenerator : EditorWindow
{
    [MenuItem("Tools/生成怪物预制体")]
    public static void GenerateAll()
    {
        // 先生成血条预制体
        GameObject healthBarPrefab = GenerateHealthBarPrefab();
        // 再生成怪物预制体（包含血条）
        GenerateMonsterPrefab(healthBarPrefab);
    }

    static GameObject GenerateHealthBarPrefab()
    {
        string dir = "Assets/Resources/Prefabs/Monster";
        EnsureDirectory(dir);

        string prefabPath = dir + "/MonsterHealthBar.prefab";

        // 如果已存在，先删除
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.Refresh();
        }

        // 创建血条根节点
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

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(barRoot, prefabPath);
        DestroyImmediate(barRoot);

        Debug.Log($"[MonsterPrefabGenerator] 血条预制体已生成: {prefabPath}");
        return prefab;
    }

    static void GenerateMonsterPrefab(GameObject healthBarPrefab)
    {
        string dir = "Assets/Resources/Prefabs/Monster";
        EnsureDirectory(dir);

        string prefabPath = dir + "/Monster.prefab";

        // 如果已存在，先删除
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.Refresh();
        }

        // 创建Monster根节点
        GameObject monsterGo = new GameObject("Monster");

        // ---- SpriteRenderer ----
        SpriteRenderer sr = monsterGo.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Monster";
        sr.sortingOrder = 5;
        // 1x1白色占位精灵（BottomCenter pivot）
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 32f);
        sr.sprite.name = "Monster_Placeholder";
        sr.color = new Color(0.9f, 0.3f, 0.2f, 1f); // 红色怪物

        // ---- Rigidbody2D ----
        Rigidbody2D rb = monsterGo.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // ---- Monster组件 ----
        monsterGo.AddComponent<Monster>();

        // ---- UnitAnimation ----
        monsterGo.AddComponent<UnitAnimation>();

        // ---- 血条子物体（从预制体实例化） ----
        if (healthBarPrefab != null)
        {
            GameObject healthBar = PrefabUtility.InstantiatePrefab(healthBarPrefab) as GameObject;
            healthBar.transform.SetParent(monsterGo.transform, false);
            healthBar.transform.localPosition = new Vector3(0, -0.5f, 0); // 脚下位置，可在Inspector中调整
            healthBar.name = "MonsterHPBar";
        }

        // 保存为预制体
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(monsterGo, prefabPath);
        DestroyImmediate(monsterGo);

        Debug.Log($"[MonsterPrefabGenerator] 怪物预制体已生成: {prefabPath}");
        EditorGUIUtility.PingObject(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void EnsureDirectory(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        string parent = System.IO.Path.GetDirectoryName(dir).Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(dir);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureDirectory(parent);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif