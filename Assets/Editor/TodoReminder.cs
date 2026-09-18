using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================================
// 本脚本【只读】性质声明（重要）：
//   本文件只在编辑器加载时“读” Docs/待办清单 并提醒开发者，不修改任何资源、
//   预制体、场景、设置；不创建/删除任何 GameObject 或 Asset；不调用任何写入类
//   API（如 AssetDatabase.SaveAssets / DeleteAsset / CreateAsset 等）。
//   请勿“顺手”在本脚本内添加任何写操作。
//
// 历史教训：本工程曾有编辑器自动脚本在编译后把预制体改坏，因此：
//   1) 严禁使用 [InitializeOnLoadMethod]（类方法级自动执行，难以控制时机）
//   2) 改用 [InitializeOnLoad] 类 + EditorApplication.delayCall 延迟一次执行，
//      且全程只读。
// ============================================================================

/// <summary>
/// 编辑器加载时，把「待办清单」里“还没做 / 等你拍板”一节提醒开发者。
/// 只读，不改动任何资源；每个编辑器会话只弹一次。
/// </summary>
[InitializeOnLoad]
public static class TodoReminder
{
    // 待办清单相对工程根的定位（不写死绝对路径）。
    private const string TodoRelativePath = "Docs/待办清单_2026-09-18.md";

    // 每会话去重的 SessionState 键（改版请递增 v 后缀）。
    private const string SessionShownKey = "TodoReminder.Shown.v1";

    static TodoReminder()
    {
        // 延迟到编辑器就绪后执行一次，避免编译期/加载期阻塞。
        EditorApplication.delayCall += ShowOnceOnLoad;
    }

    // 编辑器加载时自动提示（受“每个会话只一次”限制，且只读）。
    private static void ShowOnceOnLoad()
    {
        if (SessionState.GetBool(SessionShownKey, false))
        {
            return;
        }

        string body;
        if (!TryReadTodoBody(out body))
        {
            // 文件读不到，或没提取到内容：静默 return，不刷屏。
            return;
        }

        SessionState.SetBool(SessionShownKey, true);
        LogReminder(body);

        // 批量/无头模式下不弹窗（DisplayDialog 会阻塞），且不在进入播放态时弹。
        if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("待办提醒（2026-09-18）", ToDialogText(body), "知道了");
        }
    }

    // 菜单项：手动点击时【总是】显示，不受“只提示一次”限制（仍只读）。
    [MenuItem("Tools/查看当前待办（2026-09-18）")]
    public static void ShowNow()
    {
        string body;
        if (!TryReadTodoBody(out body))
        {
            Debug.LogWarning("[TodoReminder] 未找到待办清单，或其中没有「## 2.」一节。");
            return;
        }

        LogReminder(body);
        EditorUtility.DisplayDialog("待办提醒（2026-09-18）", ToDialogText(body), "知道了");
    }

    // 读取待办清单，并提取「## 2.」到「## 3.」之间的正文（含表格）。
    private static bool TryReadTodoBody(out string body)
    {
        body = null;

        string path = Path.Combine(Application.dataPath, "..", TodoRelativePath);
        if (!File.Exists(path))
        {
            return false;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return false;
        }

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int start = text.IndexOf("## 2.");
        if (start < 0)
        {
            return false;
        }

        // 从「## 2.」之后开始截取，到「## 3.」之前结束。
        int contentStart = text.IndexOf('\n', start);
        if (contentStart < 0)
        {
            contentStart = start;
        }
        else
        {
            contentStart += 1; // 跳过换行符
        }

        int end = text.IndexOf("## 3.", contentStart);
        if (end < 0)
        {
            end = text.Length;
        }

        body = text.Substring(contentStart, end - contentStart).Trim();
        return !string.IsNullOrEmpty(body);
    }

    // 在 Console 打印醒目提醒（前后加分隔线）。
    private static void LogReminder(string body)
    {
        const string sep = "================================================================";
        Debug.LogWarning(
            sep + "\n" +
            "========== 待办提醒 2026-09-18 ==========\n" +
            "（取自 Docs/待办清单_2026-09-18.md → §2 还没做 / 等你拍板）\n\n" +
            body + "\n" +
            sep);
    }

    // 弹窗正文精简版：去掉表格分隔行，保留可读内容。
    private static string ToDialogText(string body)
    {
        var lines = body.Split('\n');
        var kept = new System.Collections.Generic.List<string>();
        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();
            // 跳过 markdown 表格的分隔行（|---|---| 形式）。
            if (line.StartsWith("|") && line.Replace("-", "").Replace("|", "").Replace(":", "").Trim().Length == 0)
            {
                continue;
            }
            kept.Add(line);
        }
        string text = string.Join("\n", kept).Trim();
        const int maxLen = 1500;
        return text.Length > maxLen ? text.Substring(0, maxLen) + "\n…（详见 Console 完整内容）" : text;
    }
}
