using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// 批处理模式截图工具
/// 用法：
/// Tuanjie.exe -batchmode -projectpath <项目> -executeMethod BattleBatchCapture.Run -quit
/// 自动：打开Battle场景 -> 进入Play -> 等5秒 -> 截图+日志 -> 退出Play -> 退出
/// 输出：项目根目录 BattleCapture.png / BattleBatch.log
/// </summary>
public static class BattleBatchCapture
{
    static double _t0;
    static double _tShot;
    static StringBuilder _sb = new StringBuilder();
    static string _logPath;

    public static void Run()
    {
        _logPath = Path.Combine(Application.dataPath, "../BattleBatch.log");
        _sb = new StringBuilder();
        _sb.AppendLine("===== Batch诊断开始 " + System.DateTime.Now + " =====");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity");
        _sb.AppendLine("场景: " + scene.name + " 根对象: " + scene.rootCount);

        _sb.AppendLine("进入Play...");
        _t0 = EditorApplication.timeSinceStartup;
        _tShot = _t0 + 6.0;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!Application.isPlaying) return;

        if (EditorApplication.timeSinceStartup >= _tShot)
        {
            EditorApplication.update -= Tick;

            string shot = Path.Combine(Application.dataPath, "../BattleCapture.png");
            ScreenCapture.CaptureScreenshot(shot);
            _sb.AppendLine("截图: " + shot);

            _sb.AppendLine("");
            _sb.AppendLine("===== 运行时根对象 =====");
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.transform.parent == null) DumpNode(go.transform, 0);
            }

            _sb.AppendLine("");
            _sb.AppendLine("===== SpriteRenderer(有精灵) =====");
            foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr.sprite == null) continue;
                var g = sr.gameObject;
                _sb.AppendLine($"{Path2(g.transform)} | worldPos={g.transform.position} | scale={g.transform.localScale} | layer={sr.sortingLayerName}/{sr.sortingOrder} | sprite={sr.sprite.name} | flipX={sr.flipX} | boundsC={sr.bounds.center} boundsS={sr.bounds.size} | enabled={sr.enabled}");
            }

            if (Camera.main != null)
            {
                var c = Camera.main;
                _sb.AppendLine("");
                _sb.AppendLine($"相机: pos={c.transform.position} ortho={c.orthographic} size={c.orthographicSize} far={c.farClipPlane} aspect={c.aspect}");
            }

            File.WriteAllText(_logPath, _sb.ToString());
            Debug.Log("[Batch] 完成，日志: " + _logPath);

            EditorApplication.isPlaying = false;
            // 给一点时间让Play退出，然后结束进程
            EditorApplication.update += QuitTick;
            _t0 = EditorApplication.timeSinceStartup;
        }
    }

    static void QuitTick()
    {
        if (!Application.isPlaying && EditorApplication.timeSinceStartup - _t0 > 1.0)
        {
            EditorApplication.update -= QuitTick;
            EditorApplication.Exit(0);
        }
    }

    static void DumpNode(Transform t, int depth)
    {
        string ind = new string(' ', depth * 2);
        string extra = "";
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) extra = $" [SR:{sr.sortingLayerName}/{sr.sortingOrder} spr={sr.sprite?.name}]";
        var cv = t.GetComponent<Canvas>();
        if (cv != null) extra += $" [Canvas:{cv.renderMode} ov={cv.overrideSorting} {cv.sortingLayerName}/{cv.sortingOrder}]";
        var rt = t as RectTransform;
        string pos = rt != null ? $"anchor={rt.anchoredPosition}" : $"pos={t.position}";
        _sb.AppendLine($"{ind}{t.name} <{t.gameObject.GetInstanceID()}> active={t.gameObject.activeSelf} scale={t.localScale} {pos}{extra}");
        for (int i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i), depth + 1);
    }

    static string Path2(Transform t)
    {
        if (t.parent == null) return t.name;
        return Path2(t.parent) + "/" + t.name;
    }
}
