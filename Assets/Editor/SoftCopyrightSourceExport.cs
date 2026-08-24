using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 软著源程序鉴别材料导出：按清单拼接前后各约 30 页（每页 50 行）。
/// 菜单：Tools/软著/导出源程序鉴别材料
/// </summary>
public static class SoftCopyrightSourceExport
{
    const int LinesPerPage = 50;
    const int PagesEach = 30;

    static readonly string[] PreferredOrder =
    {
        "Assets/Scripts/Core/SaveData.cs",
        "Assets/Scripts/Systems/MercenaryOfferGenerator.cs",
        "Assets/Scripts/UI/TavernRosterPanel.cs",
        "Assets/Scripts/UI/TownHeroCostumePreview.cs",
        "Assets/Scripts/Unit/HeroCostumeManager.cs",
        "Assets/Scripts/Systems/MercenaryManager.cs",
        "Assets/Scripts/Config/SkillRegistry.cs",
        "Assets/Scripts/Systems/StageRoller.cs",
        "Assets/Scripts/UI/RestStagePopupUI.cs",
        "Assets/Scripts/Core/GameConfig.cs",
        "Assets/Scripts/Core/GameAudio.cs",
        "Assets/Scripts/Core/GameBgm.cs",
        "Assets/Scripts/Story/TutorialDirector.cs",
        "Assets/Scripts/UI/BattleHeadTalkUI.cs",
        "Assets/Scripts/Systems/SaveSystem.cs",
        "Assets/Scripts/Systems/ResourceWallet.cs",
        "Assets/Scripts/Systems/StaminaSystem.cs",
        "Assets/Scripts/Systems/StageClearRewardDirector.cs",
        "Assets/Scripts/Systems/PreLevelSystem.cs",
        "Assets/Scripts/Systems/OfflineGoldCalc.cs",
        "Assets/Scripts/Systems/TownSaveAlign.cs",
        "Assets/Scripts/Managers/BattleManager.cs",
        "Assets/Scripts/Managers/ChapterManager.cs",
        "Assets/Scripts/UI/TownHubController.cs",
        "Assets/Scripts/UI/AdventureUI.cs",
        "Assets/Scripts/UI/GuildHallUI.cs",
        "Assets/Scripts/UI/AdventureLogUI.cs",
        "Assets/Scripts/UI/CharacterUI.cs",
        "Assets/Scripts/UI/BattleStageMapUI.cs",
        "Assets/Scripts/UI/OfflineRewardPopup.cs",
        "Assets/Scripts/Combat/DamageFormula.cs",
        "Assets/Scripts/Platform/CloudSaveBridge.cs",
        "Assets/Scripts/Platform/RewardedAdBridge.cs",
    };

    [MenuItem("Tools/软著/导出源程序鉴别材料")]
    public static void Export()
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(root))
        {
            EditorUtility.DisplayDialog("软著导出", "无法定位工程根目录", "确定");
            return;
        }

        var allLines = new List<string>(20000);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string rel in PreferredOrder)
        {
            string full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;
            AppendFile(allLines, rel, full);
            used.Add(rel.Replace('\\', '/'));
        }

        // 不足页数时按文件名补 Scripts 下 .cs
        string scriptsDir = Path.Combine(root, "Assets", "Scripts");
        if (Directory.Exists(scriptsDir))
        {
            var extras = new List<string>(Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories));
            extras.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string full in extras)
            {
                if (allLines.Count >= PagesEach * 2 * LinesPerPage) break;
                string rel = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                if (used.Contains(rel)) continue;
                AppendFile(allLines, rel, full);
                used.Add(rel);
            }
        }

        // 同时写英文目录，避免部分环境下中文路径编码错乱导致「Docs 里找不到」
        string outDirCn = Path.Combine(root, "Docs", "软著源码鉴别");
        string outDirEn = Path.Combine(root, "Docs", "SoftCopyrightSource");
        Directory.CreateDirectory(outDirCn);
        Directory.CreateDirectory(outDirEn);

        int need = PagesEach * LinesPerPage;
        WriteBoth(outDirCn, outDirEn, "源程序_前30页.txt", "source_front_30pages.txt", allLines, 0, need, "前");
        int backStart = Math.Max(0, allLines.Count - need);
        WriteBoth(outDirCn, outDirEn, "源程序_后30页.txt", "source_back_30pages.txt", allLines, backStart, need, "后");

        string note =
            "导出时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
            "登记版本: V0.3.5（最新）\n" +
            "每页行数: " + LinesPerPage + "\n" +
            "前后页数: " + PagesEach + "\n" +
            "总拼接行数: " + allLines.Count + "\n" +
            "文件数: " + used.Count + "\n" +
            "输出目录:\n" +
            "  Docs/软著源码鉴别/\n" +
            "  Docs/SoftCopyrightSource/  （英文路径，资源管理器更好找）\n" +
            "优先收录: SaveData / MercenaryOfferGenerator / GameAudio / GameBgm / TutorialDirector / BattleHeadTalkUI 等\n" +
            "用法: 将 txt 按页排版为 PDF（≥50 行/页）提交鉴别材料。\n" +
            "也可在团结编辑器执行 Tools/软著/导出源程序鉴别材料 覆盖本目录。\n";
        File.WriteAllText(Path.Combine(outDirCn, "导出说明.txt"), note, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDirEn, "导出说明.txt"), note, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDirEn, "README_export.txt"), note, Encoding.UTF8);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("软著导出",
            "已写入：\nDocs/软著源码鉴别/\nDocs/SoftCopyrightSource/\n\n前30页 + 后30页 + 导出说明", "确定");
        Debug.Log("[SoftCopyright] 导出完成 → Docs/软著源码鉴别/ 与 Docs/SoftCopyrightSource/");
    }

    static void WriteBoth(string dirCn, string dirEn, string cnName, string enName,
        List<string> allLines, int start, int count, string tag)
    {
        string text = BuildPagedText(allLines, start, count, tag);
        File.WriteAllText(Path.Combine(dirCn, cnName), text, Encoding.UTF8);
        File.WriteAllText(Path.Combine(dirEn, cnName), text, Encoding.UTF8);
        File.WriteAllText(Path.Combine(dirEn, enName), text, Encoding.UTF8);
    }

    static string BuildPagedText(List<string> all, int start, int count, string tag)
    {
        var sb = new StringBuilder(count * 80);
        int end = Math.Min(all.Count, start + count);
        int page = 1;
        int lineInPage = 0;
        sb.AppendLine("======== 源程序鉴别材料 · " + tag + "30页 · 每页" + LinesPerPage + "行 ========");
        sb.AppendLine();
        for (int i = start; i < end; i++)
        {
            if (lineInPage == 0)
                sb.AppendLine("---------- 第 " + page + " 页 ----------");
            sb.AppendLine(all[i]);
            lineInPage++;
            if (lineInPage >= LinesPerPage)
            {
                lineInPage = 0;
                page++;
                sb.AppendLine();
            }
        }
        if (lineInPage > 0)
            sb.AppendLine();
        return sb.ToString();
    }

    static void AppendFile(List<string> all, string rel, string full)
    {
        all.Add("// ===== FILE: " + rel + " =====");
        try
        {
            foreach (string line in File.ReadAllLines(full, Encoding.UTF8))
                all.Add(line);
        }
        catch (Exception e)
        {
            all.Add("// READ ERROR: " + e.Message);
        }
        all.Add("");
    }
}
