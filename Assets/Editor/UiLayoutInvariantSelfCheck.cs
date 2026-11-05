#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 布局不变式自检（2026-11-05 根治「反复打补丁」）。
///
/// 背景：邮件标题重叠、头像列表偏移、恭喜升级属性行不齐、任务弹窗排版，历史上反复出现，
/// 根因都是「预制体摆一套几何/字体，运行时代码再覆盖一套」+ 没有任何自动校验。
/// 项目既有口径见 Docs/模块化与防复发方案_2026-09-26.md：R1 单一写入者、R4 修复即断言。
///
/// 本自检把「已经根治的真源」和「不许回来的运行时纠正层」钉成断言：
///   ① 邮件三个行模板标题右缘必须落在 lingqu 按钮视觉左缘 -12 之内，且三模板几何完全一致；
///   ② 恭喜升级「第二行」所有 Text 的字体/字号/对齐/AlignByGeometry 必须一致；
///   ③ MailListUI / LevelUpPopup 不得再出现运行时几何或字体纠正层；
///   ④ 头像视口必须等于 panl 全宽、必须竖向滚动；主线/支线任务弹窗必须各带 taskLayout:true；
///   ⑤ 展示位图标缩放真源必须是 BackpackGridVisual.IconScaleByKind 一张表，且碎片卡不越 82 格。
///
/// 只走引擎 API 读预制体真值（不解析 YAML，不受 CRLF / Unicode 转义节点名 / fileID 影响）；
/// 只 LogError + DisplayDialog，不抛异常、不阻断出包（与现有 Tools/自检 口径一致）。
/// </summary>
public static class UiLayoutInvariantSelfCheck
{
    const string MailPrefab = "Assets/Resources/Prefabs/UI/MailListWindow.prefab";
    const string LevelUpPrefab = "Assets/Resources/Prefabs/Town/恭喜升级.prefab";
    const string MailScript = "Assets/Scripts/UI/MailListUI.cs";
    const string LevelUpScript = "Assets/Scripts/UI/LevelUpPopup.cs";
    const string AvatarScript = "Assets/Scripts/UI/AvatarSelectPopup.cs";
    const string QuestScript = "Assets/Scripts/UI/AdventureLogUI.cs";
    const string GridVisualScript = "Assets/Scripts/UI/BackpackGridVisual.cs";

    // 碎片卡根 100×100，随身仓库格 82×82 + IconPad 2 → 基缩放 0.78；格子无 Mask、spacing=0。
    const float CardBaseScale = 0.78f;
    const float CellRatio = 0.82f;

    [MenuItem("Tools/自检/UI 布局不变式")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var errs = new StringBuilder();

        CheckMail(errs, sb);
        CheckLevelUp(errs, sb);
        CheckSingleWriter(errs, sb);
        CheckIconScale(errs, sb);

        bool ok = errs.Length == 0;
        if (ok) Debug.Log("[UiLayoutInvariantSelfCheck] OK\n" + sb);
        else Debug.LogError("[UiLayoutInvariantSelfCheck] FAIL\n" + errs + "\n" + sb);

        EditorUtility.DisplayDialog(
            ok ? "UI 布局不变式自检通过" : "UI 布局不变式自检失败",
            (ok ? "" : errs + "\n") + sb,
            "好的");
    }

    /// <summary>① 邮件：静态几何本身就不能压到领取按钮，三个模板必须同一套排版。</summary>
    static void CheckMail(StringBuilder errs, StringBuilder sb)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(MailPrefab);
        if (go == null) { errs.AppendLine("找不到邮件预制体：" + MailPrefab); return; }

        RectTransform btn = null;
        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == "lingqu") { btn = rt; break; }
        if (btn == null) { errs.AppendLine("邮件预制体缺 lingqu 领取按钮节点"); return; }

        float btnLeft = btn.anchoredPosition.x - btn.sizeDelta.x * btn.localScale.x * 0.5f;
        float limit = btnLeft - 12f;
        string[] tplNames = { "未领取按钮", "已领取", "未领取" };

        int found = 0;
        float firstX = 0f, firstW = 0f;
        foreach (var t in go.GetComponentsInChildren<Text>(true))
        {
            if (t.name != "Text" || t.transform.parent == null) continue;
            bool isRow = false;
            foreach (var n in tplNames) if (t.transform.parent.name == n) { isRow = true; break; }
            if (!isRow) continue;

            var rt = t.rectTransform;
            float right = rt.anchoredPosition.x + rt.sizeDelta.x * 0.5f;
            found++;
            if (right > limit + 0.01f)
                errs.AppendLine("邮件模板「" + t.transform.parent.name + "」标题右缘 " + right.ToString("0.0")
                    + " 超过允许值 " + limit.ToString("0.0") + "，会压到领取按钮（真源在预制体，别在代码里收窄）");
            if (found == 1) { firstX = rt.anchoredPosition.x; firstW = rt.sizeDelta.x; }
            else if (Mathf.Abs(rt.anchoredPosition.x - firstX) > 0.01f || Mathf.Abs(rt.sizeDelta.x - firstW) > 0.01f)
                errs.AppendLine("邮件三个行模板标题几何不一致：「" + t.transform.parent.name + "」("
                    + rt.anchoredPosition.x + ", " + rt.sizeDelta.x + ") != (" + firstX + ", " + firstW
                    + ")，同一列表会出现两种排版");
        }
        if (found != 3) errs.AppendLine("邮件行模板标题 Text 应有 3 个，实为 " + found);
        sb.AppendLine("邮件：按钮视觉左缘 " + btnLeft.ToString("0.0") + "，标题允许右缘 " + limit.ToString("0.0") + "，三模板一致");
    }

    /// <summary>② 恭喜升级：同一行所有文字必须同一套字体度量，prefab 是唯一真源。</summary>
    static void CheckLevelUp(StringBuilder errs, StringBuilder sb)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(LevelUpPrefab);
        if (go == null) { errs.AppendLine("找不到升级预制体：" + LevelUpPrefab); return; }

        Transform row2 = null;
        foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            if (tr.name == "第二行") { row2 = tr; break; }
        if (row2 == null) { errs.AppendLine("升级预制体缺「第二行」节点"); return; }

        var texts = row2.GetComponentsInChildren<Text>(true);
        if (texts.Length < 3) errs.AppendLine("升级「第二行」应有 3 个文本，实为 " + texts.Length);

        string font0 = null;
        int size0 = -1;
        TextAnchor align0 = TextAnchor.MiddleCenter;
        bool geo0 = false;
        bool first = true;
        foreach (var t in texts)
        {
            string fp = t.font != null ? AssetDatabase.GetAssetPath(t.font) : "<null>";
            if (first)
            {
                font0 = fp; size0 = t.fontSize; align0 = t.alignment; geo0 = t.alignByGeometry; first = false;
                continue;
            }
            if (fp != font0) errs.AppendLine("升级「第二行」字体不统一：「" + t.name + "」用 " + fp + "，首个用 " + font0);
            if (t.fontSize != size0) errs.AppendLine("升级「第二行」字号不统一：「" + t.name + "」" + t.fontSize + " != " + size0);
            if (t.alignment != align0) errs.AppendLine("升级「第二行」对齐不统一：「" + t.name + "」" + t.alignment);
            if (t.alignByGeometry != geo0) errs.AppendLine("升级「第二行」AlignByGeometry 不统一：「" + t.name + "」");
        }
        if (!first) sb.AppendLine("升级第二行：" + texts.Length + " 个文本同字体/同字号 " + size0 + "/同对齐");
    }

    /// <summary>③④ R1 单一写入者：运行时纠正层不许回来，拍板过的排版口径不许被改回去。</summary>
    static void CheckSingleWriter(StringBuilder errs, StringBuilder sb)
    {
        string mail = ReadScript(MailScript);
        if (mail.Contains("GetWorldCorners") && mail.Contains("sizeDelta"))
            errs.AppendLine("MailListUI.cs 又出现「世界坐标算差写进 sizeDelta」的运行时几何纠正层（不同分辨率必然复发）");
        if (mail.Contains("UnifyRowTextStyle"))
            errs.AppendLine("MailListUI.cs 出现字体纠正层");

        string levelUp = ReadScript(LevelUpScript);
        if (levelUp.Contains("UnifyRowTextStyle") || levelUp.Contains("t.fontSize = 36"))
            errs.AppendLine("LevelUpPopup.cs 又出现运行时字体/字号纠正层；真源应只在 恭喜升级.prefab");

        string avatar = ReadScript(AvatarScript);
        if (avatar.Contains("ViewLeftInset") || avatar.Contains("ListShiftX"))
            errs.AppendLine("AvatarSelectPopup.cs 又出现左右偏移常量（11-03 / 11-05 反复三次），视口应恒等于 panl 全宽");
        if (!avatar.Contains("float viewW = panlRt.rect.width;"))
            errs.AppendLine("头像视口宽不再等于 panl 全宽，左右空隙会不对称");
        if (!avatar.Contains("_scroll.horizontal = false;") || !avatar.Contains("_scroll.vertical = true;"))
            errs.AppendLine("头像列表滑动方向被改回横向（11-05 拍板为上下滑动）");

        string quest = ReadScript(QuestScript);
        int taskLayoutCount = CountOf(quest, "taskLayout: true");
        if (taskLayoutCount != 2)
            errs.AppendLine("主线/支线任务弹窗的 taskLayout: true 应有 2 处，实为 " + taskLayoutCount + "（排版会被改回通用布局）");

        sb.AppendLine("单一写入者：邮件/升级纠正层已删、头像视口全宽+竖向、任务弹窗 taskLayout×" + taskLayoutCount);
    }

    /// <summary>⑤ 图标缩放真源一张表；碎片卡放大后不得越过 82 格（spacing=0 且无 Mask，越格即真溢出）。</summary>
    static void CheckIconScale(StringBuilder errs, StringBuilder sb)
    {
        string code = ReadScript(GridVisualScript);
        if (!code.Contains("static readonly float[] IconScaleByKind"))
            errs.AppendLine("BackpackGridVisual.cs 缺展示位图标缩放真源表 IconScaleByKind");
        if (code.Contains("displayIconScaleDelta"))
            errs.AppendLine("ItemPlacement 仍带像素增量字段 displayIconScaleDelta（收集层不该背视觉参数）");

        var m = Regex.Match(code, @"IconScaleByKind\s*=\s*\{\s*1f\s*,\s*([\d.]+)f");
        if (!m.Success)
            errs.AppendLine("IconScaleByKind 解析不到本命碎片倍率（表格式应恒为 { 1f, <碎片>, <徽记>, ... }）");
        else
        {
            float frag = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            float finalScale = CardBaseScale * frag;
            if (finalScale > CellRatio + 0.0001f)
                errs.AppendLine("碎片卡最终缩放 " + finalScale.ToString("0.000") + " 超过 " + CellRatio
                    + "（82 格），spacing=0 且无 Mask 会压到邻格");
            sb.AppendLine("图标缩放：真源表碎片 " + frag.ToString("0.00") + " 倍 → 卡根 "
                + (finalScale * 100f).ToString("0.0") + " / 格 82");
        }
    }

    static string ReadScript(string assetPath)
    {
        string rel = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
        string full = Path.Combine(Application.dataPath, rel);
        return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
    }

    static int CountOf(string hay, string needle)
    {
        int n = 0, i = 0;
        while ((i = hay.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
#endif
