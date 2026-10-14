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
    /// <summary>气泡根节点放大时，底图子节点是否同步放大；关闭后恢复为改动前行为。</summary>
    public static bool EnableBubbleBgFollow = true;

    sealed class BubbleBgBase
    {
        public RectTransform rectTransform;
        public Vector2 sizeDelta;
        public Image image;
        public Image.Type imageType;
        public bool preserveAspect;
    }

    static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<BubbleBgBase>> _bubbleBgBases =
        new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<BubbleBgBase>>();

    static TextGenerator _gen;

    /// <param name="baseSize">预制体/默认气泡尺寸（宽高比以此为准）</param>
    /// <param name="maxScale">相对 baseSize 的最大等比放大倍数</param>
    /// <param name="minScale">
    /// 相对 baseSize 的最小等比缩小倍数。
    /// 【2026-10-14 新增】默认 1f = 只放大不缩小，**旧调用方行为一字未变**；
    /// 传 &lt; 1（例如 0.72f）才开启「按字数双向自适应」：短句收小、长句放大，全程等比不拉扁。
    /// </param>
    /// <returns>实际写入的文案（极端超长时可能带省略号）</returns>
    public static string Apply(
        RectTransform bubbleRt,
        Text text,
        string content,
        Vector2 baseSize,
        // 2026-10-10 主人拍板：1.75 → 2.0。等比放大上限放宽，超长句才不会被迫截断成「…」。
        float maxScale = 2f,
        // 2026-10-14 主人拍板「根据字的多少实时调整对话框大小」：新开口，默认 1 = 不缩小。
        float minScale = 1f)
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

        // 【2026-10-14 新增·仅 minScale < 1 时生效，默认 1 时整段跳过 → 旧调用方行为一字未变】
        // 主人要求「根据字的多少实时调整对话框大小」：短句收小、长句放大。
        // 收小判据 —— 把整句按「不换行」量一次天然宽度，只要单行就塞得进基准框，
        // 就按内容等比缩小气泡（**字号不变**，只缩框，短句不再挂着一个大气泡）。
        if (minScale < 0.999f)
        {
            float oneLineW = MeasureUnwrapped(text, fitted, preferWidth: true);
            if (oneLineW > 0f)
            {
                // +4 余量：量出来的宽度与实际 Wrap 判定有整数取整误差，卡死线上会被迫折行。
                float tightW = oneLineW + padL + padR + 4f;
                if (tightW <= baseSize.x)
                {
                    float oneLineH = MeasureUnwrapped(text, fitted, preferWidth: false);
                    float tightH = oneLineH + padT + padB + 2f;
                    float tight = Mathf.Max(tightW / baseSize.x, tightH / baseSize.y);
                    scale = Mathf.Clamp(tight, minScale, 1f);
                }
            }
        }

        if (needH > availH + 0.5f)
            scale = Mathf.Clamp(Mathf.Max(scale, needH / availH), 1f, Mathf.Max(1f, maxScale));

        if (Mathf.Abs(scale - 1f) > 0.001f)
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

        // 选择方案 A：只同步底图尺寸，保留现有字号与自动换行观感；若缩放根节点会连文字一起放大。
        ApplyBubbleBgScale(bubbleRt, text.rectTransform, scale);

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
        RestoreBubbleBgs(bubbleRt);
    }

    /// <summary>
    /// 【2026-10-14 新增】按「不换行、不缩放」的排版量一句字的自然尺寸 ——
    /// 用来判断「这句话单行能不能塞进基准气泡」，进而决定气泡要不要收小。
    /// preferWidth=true 量宽，false 量高（高 = 整句单行高度）。
    /// </summary>
    static float MeasureUnwrapped(Text text, string content, bool preferWidth)
    {
        if (_gen == null) _gen = new TextGenerator();
        var s = text.GetGenerationSettings(new Vector2(99999f, 99999f));
        s.horizontalOverflow = HorizontalWrapMode.Overflow;
        s.verticalOverflow = VerticalWrapMode.Overflow;
        s.resizeTextForBestFit = false;
        _gen.Populate(content, s);
        return preferWidth
            ? _gen.GetPreferredWidth(content, s)
            : _gen.GetPreferredHeight(content, s);
    }

    static void ApplyBubbleBgScale(RectTransform bubbleRt, RectTransform textRt, float scale)
    {
        if (!EnableBubbleBgFollow)
        {
            RestoreBubbleBgs(bubbleRt);
            return;
        }

        int id = bubbleRt.GetInstanceID();
        if (!_bubbleBgBases.TryGetValue(id, out var bases))
        {
            bases = CaptureBubbleBgs(bubbleRt, textRt);
            _bubbleBgBases[id] = bases;
        }

        for (int i = 0; i < bases.Count; i++)
        {
            var item = bases[i];
            if (item.rectTransform == null || item.image == null) continue;
            item.rectTransform.sizeDelta = item.sizeDelta * scale;
            EnsureImageNotStretched(item.image);
        }
    }

    static System.Collections.Generic.List<BubbleBgBase> CaptureBubbleBgs(
        RectTransform bubbleRt, RectTransform textRt)
    {
        var named = new System.Collections.Generic.List<BubbleBgBase>();
        BubbleBgBase first = null;
        for (int i = 0; i < bubbleRt.childCount; i++)
        {
            var child = bubbleRt.GetChild(i);
            if (child == textRt) continue;
            var childRt = child as RectTransform;
            var image = child.GetComponent<Image>();
            if (childRt == null || image == null) continue;

            var item = new BubbleBgBase
            {
                rectTransform = childRt,
                sizeDelta = childRt.sizeDelta,
                image = image,
                imageType = image.type,
                preserveAspect = image.preserveAspect,
            };
            if (first == null) first = item;
            if (IsBubbleBgName(child.name)) named.Add(item);
        }

        // 预制体未采用常见底图命名时，只兜底跟随第一个直接 Image 子节点，避免误改图标。
        if (named.Count == 0 && first != null) named.Add(first);
        return named;
    }

    static bool IsBubbleBgName(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return false;
        return nodeName.IndexOf("Bg", System.StringComparison.OrdinalIgnoreCase) >= 0
               || nodeName.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0
               || nodeName.IndexOf("Frame", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void RestoreBubbleBgs(RectTransform bubbleRt)
    {
        int id = bubbleRt.GetInstanceID();
        if (!_bubbleBgBases.TryGetValue(id, out var bases)) return;
        for (int i = 0; i < bases.Count; i++)
        {
            var item = bases[i];
            if (item.rectTransform != null)
                item.rectTransform.sizeDelta = item.sizeDelta;
            if (item.image != null)
            {
                item.image.type = item.imageType;
                item.image.preserveAspect = item.preserveAspect;
            }
        }
        _bubbleBgBases.Remove(id);
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
        EnsureImageNotStretched(bubbleRt.GetComponent<Image>());
    }

    static void EnsureImageNotStretched(Image img)
    {
        if (img == null) return;
        img.type = Image.Type.Simple;
        // Simple + 已按原图比例设 sizeDelta：不要用 preserveAspect 再裁切，否则文字区会对不齐
        img.preserveAspect = false;
    }
}
