using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 怪物模板「运行尺寸」探针 —— <b>2026-10-06 主人要求</b>：
/// 「Monstersmoban 这个预制体各个节点大小有问题，把所有节点按运行中的大小给我复制一次，我好调试」。
///
/// <para>做法：战斗里第一只怪生成完成后，把整棵节点树<b>运行时的真实数值</b>抄一份出来 ——</para>
/// <para>· <c>localScale</c> / <c>lossyScale</c>（含 <c>GameConfig.UNIT_SCALE</c> 与 SPUM 自带缩放）<br/>
/// · <c>RectTransform</c> 的 <c>sizeDelta</c> / <c>anchoredPosition</c> / 锚点<br/>
/// · 每个 <c>SpriteRenderer</c> 的 sortingOrder / layer / alpha / 贴图名 / enabled</para>
///
/// <para>输出两处：编辑器 Console + <c>{项目根}/Docs/Monstersmoban_运行尺寸.txt</c>，
/// 主人照着这个表去调预制体即可（<b>绝不由代码改 .prefab</b>，铁律 3）。</para>
///
/// <para>⚠ 本类<b>只读</b>：不改任何节点、不动任何数值；每局只 dump 一次（第一只怪）。</para>
/// </summary>
public static class MonsterTemplateProbe
{
    static bool _dumped;

    /// <summary>把 <paramref name="root"/> 整棵树运行时的实际尺寸抄一份（每局只做一次）。</summary>
    public static void DumpOnce(Transform root)
    {
        if (_dumped || root == null) return;
        _dumped = true;

        var sb = new StringBuilder();
        sb.AppendLine("怪物模板「运行尺寸」对照 —— 由 MonsterTemplateProbe 自动导出（只读，未改动任何节点）");
        sb.AppendLine("根节点=" + root.name + "  lossyScale=" + Fmt(root.lossyScale) +
                      "  帧=" + Time.frameCount);
        sb.AppendLine();
        Walk(root, sb, 0);

        string text = sb.ToString();
        Debug.Log(text);

#if UNITY_EDITOR
        try
        {
            var assetsDir = new DirectoryInfo(Application.dataPath);
            var projectRoot = assetsDir.Parent;
            if (projectRoot != null)
            {
                string dir = Path.Combine(projectRoot.FullName, "Docs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Monstersmoban_运行尺寸.txt"), text, new UTF8Encoding(false));
                Debug.Log("[MonsterTemplateProbe] 运行尺寸对照已写到 Docs/Monstersmoban_运行尺寸.txt");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MonsterTemplateProbe] 写对照文件失败：" + e.Message);
        }
#endif
    }

    static void Walk(Transform t, StringBuilder sb, int depth)
    {
        if (t == null) return;

        sb.Append(new string(' ', depth * 2)).Append("- ").Append(t.name)
          .Append("  localScale=").Append(Fmt(t.localScale))
          .Append("  lossyScale=").Append(Fmt(t.lossyScale));

        var rt = t as RectTransform;
        if (rt != null)
        {
            sb.Append("  sizeDelta=").Append(Fmt(rt.sizeDelta))
              .Append("  anchoredPos=").Append(Fmt(rt.anchoredPosition))
              .Append("  anchor=").Append(Fmt(rt.anchorMin)).Append('~').Append(Fmt(rt.anchorMax));
        }

        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sb.Append("  [SR] order=").Append(sr.sortingOrder)
              .Append(" layer=").Append(sr.sortingLayerName)
              .Append(" alpha=").Append(sr.color.a.ToString("F2"))
              .Append(" sprite=").Append(sr.sprite != null ? sr.sprite.name : "(null)")
              .Append(" enabled=").Append(sr.enabled)
              .Append(" active=").Append(t.gameObject.activeSelf);
        }

        sb.AppendLine();
        for (int i = 0; i < t.childCount; i++)
            Walk(t.GetChild(i), sb, depth + 1);
    }

    static string Fmt(Vector2 v) => $"({v.x:F3},{v.y:F3})";
    static string Fmt(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
}
