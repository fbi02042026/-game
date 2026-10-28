using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 节点递归查找 + 运行时建 UI 的<b>唯一真源</b>（2026-10-28 收敛）。
/// 起因：全库 38 份复制粘贴的 FindDeep / 9 份 NewText / 7 份 NewImage，各文件私有实现
/// 行为逐渐分叉（有的忽略大小写、有的带 IsNullOrEmpty 防护、有的不设字体）。
/// 本文件只收编<b>实现完全一致</b>的组；语义有差异的实现保持原样，禁止强行统一。
/// </summary>
public static class UiFind
{
    /// <summary>深度优先递归查找第一个同名节点（含自身）。root 或 name 为空直接返回 null；大小写敏感。</summary>
    public static Transform Deep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = Deep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}

public static class UiFactory
{
    /// <summary>运行时建 Image（不挡射线）。各 UI 弹窗通用。</summary>
    public static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>运行时建 Text（中文字体、白字、不挡射线、溢出模式）。关卡图 / 弹窗通用。</summary>
    public static Text NewText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>运行时建 Text（自定义颜色、关富文本，无溢出设置）。邮件系统通用。</summary>
    public static Text NewTextColored(Transform parent, string name, string content, int size, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.supportRichText = false;
        t.raycastTarget = false;
        return t;
    }
}
