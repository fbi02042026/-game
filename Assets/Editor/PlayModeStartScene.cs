using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play 默认从当前打开的场景进入；需要完整流程时手动设为 Boot。
/// </summary>
[InitializeOnLoad]
public static class PlayModeStartScene
{
    const string BootScenePath = "Assets/Scenes/Boot.unity";

    static PlayModeStartScene()
    {
        // 不再自动改 Play 起始场景，避免「打开 Battle/Town 点 Play 却被拉到 Boot」
    }

    [MenuItem("Tools/流程/设为从 Boot 启动（主界面流程）")]
    public static void ApplyBootStart()
    {
        var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (boot == null)
        {
            EditorUtility.DisplayDialog("启动流程", "找不到 " + BootScenePath, "确定");
            return;
        }
        EditorSceneManager.playModeStartScene = boot;
        Debug.Log("[PlayModeStartScene] Play 起始场景 = Boot → Town");
        EditorUtility.DisplayDialog("启动流程",
            "Play 将从 Boot 进入 → 主界面(Town)。\n点底部「冒险」进入战斗。",
            "确定");
    }

    [MenuItem("Tools/流程/从当前场景启动（不强制 Boot）")]
    public static void ClearPlayStartScene()
    {
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[PlayModeStartScene] Play 起始场景 = 当前编辑器打开的场景");
        EditorUtility.DisplayDialog("启动流程",
            "Play 将从当前打开的场景直接进入。\n适合单独调试 Town / Battle。",
            "确定");
    }
}
