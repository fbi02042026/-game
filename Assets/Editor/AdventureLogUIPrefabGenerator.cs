#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 冒险日志预制体已改为手做资源，禁止生成器覆盖。
/// </summary>
public static class AdventureLogUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/AdventureLogUI.prefab";

    [MenuItem("Tools/_归档/UI/生成冒险日志界面预制体")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            EditorUtility.DisplayDialog("冒险日志",
                "已存在手做 AdventureLogUI.prefab，不会覆盖。\n布局与资源以预制体为准。",
                "OK");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) Selection.activeObject = prefab;
            return;
        }

        EditorUtility.DisplayDialog("冒险日志",
            "未找到 Resources/Prefabs/Town/AdventureLogUI.prefab。\n请使用现有预制体，不要用代码重建。",
            "OK");
    }
}
#endif
