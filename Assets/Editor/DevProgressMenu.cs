using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仅编辑器测试用】测试进度开关：直接对 Application.persistentDataPath/player.dat 做文本级值替换，
/// 只在编辑器菜单里调用，不进包、不随运行时发布。
/// 进度真源是 clearedStageEntries（元素形如 {"id":"1_0"}，键 = 章_篇内小节 0 基下标）；
/// 启动时 SaveData.SyncRuntimeFromLists() 把它重建进 [NonSerialized] 的 clearedStages 集合，
/// clearedChapterEntries / trulyClearedChapterEntries / maxUnlockedChapter 等派生镜像也由启动逻辑重算。
/// 不写 grandProgress：它只是纯显示缓存，SaveData.MigrateGrandProgress() 启动时会从 clearedStages
/// 整体重算落值，手工写既多余又会在重算前留下脏值；同理 chapterClearCounts / branchRoll1 /
/// branchRoll2 / chosenBranch / clearedStageDiffEntries / clearedChapterDiffEntries（难度侧独立）
/// 以及剧情、成就、图鉴、宝箱、礼物等任何其他字段一律不碰，避免 220 个无关字段产生格式 diff。
/// clearedStageEntries 必须【整体替换】而不是追加：从高章退回低章（例如 8 退回 1）时，
/// 追加会残留 3_* / 4_* 等高章记录，启动重算出的解锁链便与预期不符。
/// 【引擎在跑会覆盖】团结引擎可能仍在运行，Unity 内存里若还活着 SaveSystem 单例，一次 Save()
/// 就会用内存旧档把手工改过的 player.dat 覆盖回去。故本工具在 Play 或将进入 Play 时直接拒绝执行，
/// 写入成功后也必须提示用户【完全退出并重启团结引擎】后再进 Play。
/// </summary>
public static class DevProgressMenu
{
    // 双引号字符：拼 JSON 片段时用，避免源码里到处写转义引号。
    private const char Q = '"';

    [MenuItem("Tools/测试进度/完成第一章")]
    public static void FinishChapter1() { Apply(1); }

    [MenuItem("Tools/测试进度/完成第二章")]
    public static void FinishChapter2() { Apply(2); }

    [MenuItem("Tools/测试进度/完成第三章")]
    public static void FinishChapter3() { Apply(3); }

    [MenuItem("Tools/测试进度/完成第四章")]
    public static void FinishChapter4() { Apply(4); }

    [MenuItem("Tools/测试进度/完成第五章")]
    public static void FinishChapter5() { Apply(5); }

    [MenuItem("Tools/测试进度/完成第六章")]
    public static void FinishChapter6() { Apply(6); }

    [MenuItem("Tools/测试进度/完成第七章")]
    public static void FinishChapter7() { Apply(7); }

    [MenuItem("Tools/测试进度/完成第八章")]
    public static void FinishChapter8() { Apply(8); }

    /// <summary>共享实现：把存档的 5 个进度字段改成「通关前 n 章」。</summary>
    private static void Apply(int n)
    {
        // 1. 防覆盖闸：Play 中或即将进入 Play 时内存旧档会覆盖手工改动，直接拒绝、文件零改动。
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "测试进度 / 无法执行",
                "请先退出 Play，并【完全关闭团结引擎】后再执行本菜单。\n" +
                "否则 Unity 内存里存活的 SaveSystem 单例会用旧档把手工改过的 player.dat 覆盖回去。",
                "知道了");
            return;
        }

        // 2. 存档路径；不存在就报错返回，绝不自动建新档、不兜底。
        string path = Path.Combine(Application.persistentDataPath, "player.dat");
        if (!File.Exists(path))
        {
            string msg = "[测试进度] 找不到存档文件：" + path;
            Debug.LogError(msg);
            EditorUtility.DisplayDialog("测试进度 / 失败",
                "找不到存档文件：\n" + path, "知道了");
            return;
        }

        // 3. 同目录时间戳备份；备份失败即中止，不写主档。
        string backupPath = path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        try
        {
            File.Copy(path, backupPath, true);
        }
        catch (Exception e)
        {
            Debug.LogError("[测试进度] 备份失败，已中止，未改动主档：" + e);
            return;
        }

        // 4. 明文 UTF-8 单行紧凑 JSON 读入。
        string json = File.ReadAllText(path, Encoding.UTF8);

        // 5. 构造 5 个新值。
        // 5a. clearedStageEntries：整体替换，按章升序、节升序生成 {"id":"ch_s0"}。
        var stagesSb = new StringBuilder();
        stagesSb.Append('[');
        bool first = true;
        int totalStages = 0;
        for (int ch = 1; ch <= n; ch++)
        {
            int stageCount = ActProgressionDefs.StageCountOf(ch);
            totalStages += stageCount;
            for (int s0 = 0; s0 < stageCount; s0++)
            {
                if (!first) stagesSb.Append(',');
                first = false;
                stagesSb.Append('{').Append(Q).Append("id").Append(Q).Append(':').Append(Q)
                        .Append(ch).Append('_').Append(s0).Append(Q).Append('}');
            }
        }
        stagesSb.Append(']');
        string stagesJson = stagesSb.ToString();

        // 5b. clearedChapterEntries / trulyClearedChapterEntries：[{"id":1},...,{"id":n}]
        var chaptersSb = new StringBuilder();
        chaptersSb.Append('[');
        for (int ch = 1; ch <= n; ch++)
        {
            if (ch > 1) chaptersSb.Append(',');
            chaptersSb.Append('{').Append(Q).Append("id").Append(Q).Append(':').Append(ch).Append('}');
        }
        chaptersSb.Append(']');
        string chaptersJson = chaptersSb.ToString();

        // 5c. 两个标量。
        string maxUnlocked = Mathf.Min(n + 1, ActProgressionDefs.ActCount).ToString();
        string tutorialDone = "true";

        // 6./7. 精确文本级值替换：搜索 "字段名":，数组括号配平、标量读到 , 或 }。
        //       每步都基于「更新后的字符串」重新定位（等价于规格建议的从后往前），避免下标失效；
        //       其余文本逐字符原样保留。
        var replacements = new (string field, string value, bool isArray)[]
        {
            ("clearedStageEntries",        stagesJson,   true),
            ("clearedChapterEntries",      chaptersJson, true),
            ("trulyClearedChapterEntries", chaptersJson, true),
            ("maxUnlockedChapter",         maxUnlocked,  false),
            ("tutorialDone",               tutorialDone, false),
        };

        string newJson = json;
        foreach (var r in replacements)
        {
            string replaced = ReplaceFieldOrNull(newJson, r.field, r.value, r.isArray);
            if (replaced == null)
            {
                string err = "[测试进度] 在 player.dat 中找不到字段 " + Q + r.field + Q + ":，已中止，未写入主档（备份已生成）。";
                Debug.LogError(err);
                EditorUtility.DisplayDialog("测试进度 / 失败",
                    "找不到字段 " + Q + r.field + Q + ":，已中止，未改动主档。\n" +
                    "不会追加字段、不会静默跳过、不会新建档。\n备份已生成：" + Path.GetFileName(backupPath),
                    "知道了");
                return;
            }
            newJson = replaced;
        }

        // 8. UTF-8 无 BOM 写回。
        File.WriteAllText(path, newJson, new UTF8Encoding(false));

        // 9. 成功日志 + 重启提示弹窗。
        Debug.Log("[测试进度] 已设为通关第 " + n + " 章，共 " + totalStages + " 节；备份：" + backupPath);
        EditorUtility.DisplayDialog(
            "测试进度 / 已写入",
            "已写入 player.dat：通关第 " + n + " 章（共 " + totalStages + " 节）。\n\n" +
            "请【完全退出并重启团结引擎】后再进 Play，否则内存旧档会覆盖回去。\n\n" +
            "本次只改这 5 个字段：\n" +
            "· clearedStageEntries（整体替换）\n" +
            "· clearedChapterEntries\n" +
            "· trulyClearedChapterEntries\n" +
            "· maxUnlockedChapter\n" +
            "· tutorialDone\n\n" +
            "备份文件：" + Path.GetFileName(backupPath),
            "知道了");
    }

    /// <summary>
    /// 在 src 中把 "field": 的值整体替换为 newValue，返回新字符串；字段缺失 / 值形态异常返回 null。
    /// 数组：从 [ 起做括号配平扫描（跳过字符串字面量及转义）到配对 ]；标量：读到下一个 , 或 }。
    /// </summary>
    private static string ReplaceFieldOrNull(string src, string field, string newValue, bool isArray)
    {
        string key = Q + field + Q + ":";
        int keyIndex = src.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        int valueStart = keyIndex + key.Length;
        int valueEnd;
        if (isArray)
        {
            if (valueStart >= src.Length || src[valueStart] != '[') return null;
            valueEnd = FindMatchingBracket(src, valueStart);
            if (valueEnd < 0) return null;
        }
        else
        {
            valueEnd = valueStart;
            while (valueEnd < src.Length && src[valueEnd] != ',' && src[valueEnd] != '}')
                valueEnd++;
            if (valueEnd == valueStart) return null;
        }

        var sb = new StringBuilder(src.Length + newValue.Length);
        sb.Append(src, 0, valueStart);
        sb.Append(newValue);
        sb.Append(src, valueEnd, src.Length - valueEnd);
        return sb.ToString();
    }

    /// <summary>从 arrayStart（指向 '['）扫描，返回配对 ']' 之后一个下标（exclusive）；未配平返回 -1。</summary>
    private static int FindMatchingBracket(string s, int arrayStart)
    {
        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = arrayStart; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == Q) inString = false;
                continue;
            }
            if (c == Q) { inString = true; continue; }
            if (c == '[') { depth++; continue; }
            if (c == ']')
            {
                depth--;
                if (depth == 0) return i + 1;
            }
        }
        return -1;
    }
}
