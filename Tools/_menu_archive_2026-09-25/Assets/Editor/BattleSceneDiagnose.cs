using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// Battle场景一键诊断工具
/// 用法：菜单 Tools/调试/①打开Battle场景并诊断
/// 会自动：打开Battle场景 -> 进入Play -> 等待6秒 -> 截图 + 输出对象树 -> 退出Play
/// 截图和日志保存到项目根目录：BattleCapture.png / BattleDiagnose.log
/// </summary>
public static class BattleSceneDiagnose
{
    static double _startTime;
    static double _shotTime;
    static string _logPath;
    static StringBuilder _sb;

    [MenuItem("Tools/_归档/调试/①打开Battle场景并诊断(截图+层级)")]
    public static void RunDiagnose()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[诊断] 请先退出Play模式再运行");
            return;
        }

        _logPath = Path.Combine(Application.dataPath, "../BattleDiagnose.log");
        _sb = new StringBuilder();
        _sb.AppendLine("===== Battle场景诊断 开始: " + System.DateTime.Now + " =====");

        // 打开Battle场景
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity");
        _sb.AppendLine("场景已打开: " + scene.name + " | 根对象数: " + scene.rootCount);
        AppendHierarchy();
        File.WriteAllText(_logPath, _sb.ToString());

        Debug.Log("[诊断] 进入Play模式，等待初始化...");
        _startTime = EditorApplication.timeSinceStartup;
        _shotTime = _startTime + 6.0;
        EditorApplication.update += DiagnoseTick;

        // 【关键】必须真正进入Play模式，否则update回调永远不会触发
        EditorApplication.isPlaying = true;
    }

    static void DiagnoseTick()
    {
        if (!Application.isPlaying) return;

        if (EditorApplication.timeSinceStartup >= _shotTime)
        {
            EditorApplication.update -= DiagnoseTick;

            // 1) 截图
            string shotPath = Path.Combine(Application.dataPath, "../BattleCapture.png");
            ScreenCapture.CaptureScreenshot(shotPath);
            _sb.AppendLine("");
            _sb.AppendLine("===== 截图已保存: " + shotPath + " =====");

            // 2) 对象树 + SpriteRenderer状态
            _sb.AppendLine("");
            _sb.AppendLine("===== 运行时对象树 =====");
            AppendRuntimeHierarchy();
            _sb.AppendLine("");
            _sb.AppendLine("===== 所有SpriteRenderer状态 =====");
            AppendSpriteRendererInfo();

            // 3) 相机信息
            if (Camera.main != null)
            {
                var cam = Camera.main;
                _sb.AppendLine("");
                _sb.AppendLine("===== 相机信息 =====");
                _sb.AppendLine($"pos={cam.transform.position} rot={cam.transform.eulerAngles} ortho={cam.orthographic} size={cam.orthographicSize} far={cam.farClipPlane} aspect={cam.aspect}");
            }

            File.WriteAllText(_logPath, _sb.ToString());
            Debug.Log("[诊断] 完成，日志: " + _logPath);

            // 退出Play
            EditorApplication.isPlaying = false;
        }
    }

    static void AppendHierarchy()
    {
        _sb.AppendLine("--- 编辑器场景层级(未运行) ---");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            AppendNode(root.transform, 0);
    }

    static void AppendRuntimeHierarchy()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.transform.parent == null)
                AppendNode(go.transform, 0);
        }
    }

    static void AppendNode(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        string extra = "";
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
            extra = $" [SR: layer={sr.sortingLayerName}/{sr.sortingOrder} sprite={sr.sprite?.name} alpha={sr.color.a} enabled={sr.enabled}]";
        var canvas = t.GetComponent<Canvas>();
        if (canvas != null)
            extra += $" [Canvas: mode={canvas.renderMode} override={canvas.overrideSorting} layer={canvas.sortingLayerName}/{canvas.sortingOrder}]";
        var rect = t as RectTransform;
        string pos = rect != null ? $"anchorPos={rect.anchoredPosition}" : $"pos={t.position}";
        _sb.AppendLine($"{indent}{t.gameObject.name} <{t.gameObject.GetInstanceID()}> active={t.gameObject.activeSelf} scale={t.localScale} {pos}{extra}");
        for (int i = 0; i < t.childCount; i++)
            AppendNode(t.GetChild(i), depth + 1);
    }

    static void AppendSpriteRendererInfo()
    {
        var srs = Object.FindObjectsOfType<SpriteRenderer>();
        _sb.AppendLine($"共 {srs.Length} 个SpriteRenderer");
        foreach (var sr in srs)
        {
            if (sr.sprite == null) continue;
            var rendererGO = sr.gameObject;
            _sb.AppendLine($"{GetPath(rendererGO.transform)} | worldPos={rendererGO.transform.position} | scale={rendererGO.transform.localScale} | layer={sr.sortingLayerName}/{sr.sortingOrder} | sprite={sr.sprite.name} | flipX={sr.flipX} | bounds.center={sr.bounds.center} bounds.size={sr.bounds.size} | alpha={sr.color.a} | enabled={sr.enabled} | mat={sr.sharedMaterial?.name}");
        }
    }

    static string GetPath(Transform t)
    {
        if (t.parent == null) return t.name;
        return GetPath(t.parent) + "/" + t.name;
    }
}
