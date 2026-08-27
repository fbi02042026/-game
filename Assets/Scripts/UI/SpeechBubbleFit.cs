using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话气泡排版统一规则：
/// 1) 默认尺寸保持比例，禁止单独拉宽/拉高把气泡拉扁；
/// 2) 文字先 Wrap 成多行（禁止单行撑出框外）；
/// 3) 多行仍放不下时，再等比放大气泡。
/// </summary>
public static class SpeechBubbleFit
{
    static TextGenerator _gen;

    /// <param name="baseSize">预制体/默认气泡尺寸（宽高比以此为准）</param>
    /// <param name="maxScale">相对 baseSize 的最大等比放大倍数</param>
    /// <returns>实际写入的文案（极端超长时可能带省略号）</returns>
    public static string Apply(
        RectTransform bubbleRt,
        Text text,
        string content,
        Vector2 baseSize,
        float maxScale = 1.75f)
    {
        if (bubbleRt == null || text == null)
            return content ?? "";

        string raw = Sanitize(content);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;

        if (baseSize.x < 8f || baseSize.y < 8f)
            baseSize = bubbleRt.sizeDelta.sqrMagnitude > 1f
                ? bubbleRt.sizeDelta
                : new Vector2(180f, 107f);

        bubbleRt.sizeDelta = baseSize;
        ResolvePads(bubbleRt, text.rectTransform, out float padL, out float padR, out float padT, out float padB);

        float wrapW = Mathf.Max(40f, baseSize.x - padL - padR);
        float availH = Mathf.Max(20f, baseSize.y - padT - padB);

        if (_gen == null) _gen = new TextGenerator();
        var settings = text.GetGenerationSettings(new Vector2(wrapW, 0f));
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.resizeTextForBestFit = false;

        string fitted = raw;
        _gen.Populate(fitted, settings);
        float needH = _gen.GetPreferredHeight(fitted, settings);

        float scale = 1f;
        if (needH > availH + 0.5f)
            scale = Mathf.Clamp(needH / availH, 1f, Mathf.Max(1f, maxScale));

        if (scale > 1.001f)
        {
            Vector2 sized = baseSize * scale;
            bubbleRt.sizeDelta = sized;
            wrapW = Mathf.Max(40f, sized.x - padL - padR);
            availH = Mathf.Max(20f, sized.y - padT - padB);
            settings = text.GetGenerationSettings(new Vector2(wrapW, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            _gen.Populate(fitted, settings);
            needH = _gen.GetPreferredHeight(fitted, settings);
        }

        // 放大到上限仍塞不下：裁切并加省略号（仍保持 Wrap，不改成单行溢出）
        if (needH > availH + 0.5f && fitted.Length > 1)
        {
            while (fitted.Length > 1)
            {
                fitted = fitted.Substring(0, fitted.Length - 1).TrimEnd('，', '。', '、', ' ', '…');
                string trial = fitted + "…";
                _gen.Populate(trial, settings);
                if (_gen.GetPreferredHeight(trial, settings) <= availH + 0.5f)
                {
                    fitted = trial;
                    break;
                }
            }
        }

        text.text = fitted;
        EnsureImageNotStretched(bubbleRt);
        return fitted;
    }

    public static void ResetSize(RectTransform bubbleRt, Vector2 baseSize)
    {
        if (bubbleRt == null) return;
        if (baseSize.x > 1f && baseSize.y > 1f)
            bubbleRt.sizeDelta = baseSize;
    }

    static string Sanitize(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        string raw = content.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        // 去掉人为硬换行，交给 Wrap；保留用户有意的短暂停顿意义不大，统一走自动折行
        raw = raw.Replace("\n", "");
        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        return raw;
    }

    static void ResolvePads(RectTransform bubble, RectTransform textRt,
        out float padL, out float padR, out float padT, out float padB)
    {
        padL = padR = 12f;
        padT = 10f;
        padB = 16f;
        if (bubble == null || textRt == null) return;

        // 拉伸铺满父节点时：用 offset 读内边距
        if (Approximately(textRt.anchorMin, Vector2.zero) && Approximately(textRt.anchorMax, Vector2.one))
        {
            padL = Mathf.Max(0f, textRt.offsetMin.x);
            padB = Mathf.Max(0f, textRt.offsetMin.y);
            padR = Mathf.Max(0f, -textRt.offsetMax.x);
            padT = Mathf.Max(0f, -textRt.offsetMax.y);
            // sizeDelta 负数常见（GuildHall BubbleText）
            if (padL + padR < 1f && textRt.sizeDelta.x < 0f)
            {
                float insetX = -textRt.sizeDelta.x;
                padL = padR = insetX * 0.5f;
            }
            if (padT + padB < 1f && textRt.sizeDelta.y < 0f)
            {
                float insetY = -textRt.sizeDelta.y;
                padT = padB = insetY * 0.5f;
            }
        }
    }

    static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
    }

    static void EnsureImageNotStretched(RectTransform bubbleRt)
    {
        var img = bubbleRt.GetComponent<Image>();
        if (img == null) return;
        img.type = Image.Type.Simple;
        // Simple + 已按原图比例设 sizeDelta：不要用 preserveAspect 再裁切，否则文字区会对不齐
        img.preserveAspect = false;
    }
}
