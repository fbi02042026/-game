using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：换场与黑幕（场景背景挂载、地名大字揭示、黑幕/压暗层、 chrome 显隐）
public partial class DialogueUI
{

    /// <summary>
    /// 全屏剧情背景（会长办公室 / 公会大厅）。sprite 为空则隐藏，露出城镇或战场。
    /// 不改预制体：运行时在 Canvas 最底层生成。
    /// </summary>
    public void SetSceneBackground(Sprite sprite)
    {
        EnsureSceneBackground();
        if (sceneBackgroundImage == null) return;

        Transform host = sceneBackgroundImage.transform.parent != null
            && sceneBackgroundImage.transform.parent != transform
            ? sceneBackgroundImage.transform.parent
            : sceneBackgroundImage.transform;

        if (sprite == null)
        {
            sceneBackgroundImage.sprite = null;
            sceneBackgroundImage.enabled = false;
            host.gameObject.SetActive(false);
            return;
        }

        host.gameObject.SetActive(true);
        sceneBackgroundImage.enabled = true;
        sceneBackgroundImage.sprite = sprite;
        sceneBackgroundImage.color = Color.white;
        sceneBackgroundImage.preserveAspect = true;
        var fitter = sceneBackgroundImage.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = sceneBackgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        float w = sprite.rect.width;
        float h = Mathf.Max(1f, sprite.rect.height);
        fitter.aspectRatio = w / h;
        EnsureBgDim();
        SetBgDim(0f);
        var dim = transform.Find("Dim");
        if (dim != null)
            dim.gameObject.SetActive(false);
    }

    void EnsureSceneBackground()
    {
        if (sceneBackgroundImage != null) return;

        var existing = transform.Find("SceneBackgroundHost/SceneBackground")
                       ?? transform.Find("SceneBackground");
        if (existing != null)
        {
            sceneBackgroundImage = existing.GetComponent<Image>();
            if (sceneBackgroundImage != null) return;
        }

        var hostGo = new GameObject("SceneBackgroundHost", typeof(RectTransform), typeof(RectMask2D));
        hostGo.transform.SetParent(transform, false);
        hostGo.transform.SetAsFirstSibling();
        var hostRt = hostGo.GetComponent<RectTransform>();
        hostRt.anchorMin = Vector2.zero;
        hostRt.anchorMax = Vector2.one;
        hostRt.offsetMin = Vector2.zero;
        hostRt.offsetMax = Vector2.zero;

        var imgGo = new GameObject("SceneBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        imgGo.transform.SetParent(hostGo.transform, false);
        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.anchoredPosition = Vector2.zero;
        imgRt.sizeDelta = Vector2.zero;

        sceneBackgroundImage = imgGo.GetComponent<Image>();
        sceneBackgroundImage.raycastTarget = false;
        sceneBackgroundImage.preserveAspect = true;
        var fitter = imgGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        hostGo.SetActive(false);
    }

    /// <summary>
    /// 换场景：先亮背景 + 地点名，字淡出，背景压暗，再出立绘和对话框。
    /// <para><paramref name="textTimeScale"/> = 地名大字（停留 + 淡出）的时间缩放。
    /// 【2026-10-14 主人拍板】会长办公室/公会大厅传 0.7（字的时间减 30%），
    /// 判定真源 <see cref="StoryBackgrounds.IsFastTextScene"/>；其他场景传 1（默认）。</para>
    /// </summary>
    public IEnumerator PlayLocationReveal(string title, float textTimeScale = 1f)
    {
        _revealing = true;
        _skipReveal = false;
        SetDialogueChromeVisible(false);
        // 换场景前清掉上一句道具（如委托书），否则会盖住「公会大厅」地点名
        ClearStoryPropForReveal();
        EnsureLocationCaption();
        EnsureBgDim();
        EnsureRevealBlack();
        SetBgDim(0f);
        SetRevealBlack(1f);
        SetSceneBackgroundAlpha(0f);
        if (!string.IsNullOrEmpty(title))
        {
            float blackInT = 0f;
            while (blackInT < LocBlackInDur && !_skipReveal)
            {
                blackInT += Time.unscaledDeltaTime;
                SetRevealBlack(Mathf.Clamp01(blackInT / LocBlackInDur));
                yield return null;
            }
            SetRevealBlack(1f);
            RaiseLocationCaption();
            SetLocationCaption(title, 1f);
            float holdDur = LocHoldDur * textTimeScale;
            yield return WaitUnscaled(holdDur);
            float textFadeDur = LocTextFadeDur * textTimeScale;
            float fadeT = 0f;
            while (fadeT < textFadeDur && !_skipReveal)
            {
                fadeT += Time.unscaledDeltaTime;
                SetLocationCaption(title, 1f - Mathf.Clamp01(fadeT / textFadeDur));
                yield return null;
            }
        }
        SetLocationCaption(title, 0f);
        // 背景先在黑幕后面开满，再单独淡出黑幕；不做交叉淡入，
        // 否则中途两层都半透明会漏出下面明亮的城镇，看着像闪白。
        SetSceneBackgroundAlpha(1f);
        yield return null;
        float bgFadeT = 0f;
        while (bgFadeT < LocBgFadeDur && !_skipReveal)
        {
            bgFadeT += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(bgFadeT / LocBgFadeDur);
            SetRevealBlack(1f - p);
            yield return null;
        }
        SetSceneBackgroundAlpha(1f);
        SetRevealBlack(0f);

        float dimT = 0f;
        const float dimTarget = 0.42f;
        while (dimT < LocDimDur && !_skipReveal)
        {
            dimT += Time.unscaledDeltaTime;
            SetBgDim(Mathf.Lerp(0f, dimTarget, Mathf.Clamp01(dimT / LocDimDur)));
            yield return null;
        }
        SetBgDim(dimTarget);
        _revealing = false;
        _skipReveal = false;
    }

    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds && !_skipReveal)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // 【2026-10-24 主人拍板「剧情弹出加黑幕过渡 + 字幕」】开播短黑幕节奏（比地点揭示快一档）
    const float IntroBlackInDur = 0.25f;
    const float IntroHoldDur = 1.0f;
    const float IntroTextFadeDur = 0.5f;
    const float IntroBgFadeDur = 0.6f;

    /// <summary>
    /// 剧情开播黑幕过渡（整段开头补一道；背景没换、走不到 <see cref="PlayLocationReveal"/> 时用）：
    /// 黑幕快速压上 → 字幕短留（地点名或调用方传入的提示语）→ 揭开并压暗到 0.42（与地点揭示同收口）。
    /// 点击跳过与地点揭示同一出口（_skipReveal，见 DialogueUI.Wire）。
    /// </summary>
    public IEnumerator PlayIntroVeil(string caption)
    {
        _revealing = true;
        _skipReveal = false;
        SetDialogueChromeVisible(false);
        EnsureLocationCaption();
        EnsureBgDim();
        EnsureRevealBlack();
        SetBgDim(0f);
        SetRevealBlack(0f);
        SetSceneBackgroundAlpha(1f);
        // 黑幕压上
        float inT = 0f;
        while (inT < IntroBlackInDur && !_skipReveal)
        {
            inT += Time.unscaledDeltaTime;
            SetRevealBlack(Mathf.Clamp01(inT / IntroBlackInDur));
            yield return null;
        }
        SetRevealBlack(1f);
        // 字幕短留
        if (!string.IsNullOrEmpty(caption))
        {
            RaiseLocationCaption();
            SetLocationCaption(caption, 1f);
            yield return WaitUnscaled(IntroHoldDur);
            float fadeT = 0f;
            while (fadeT < IntroTextFadeDur && !_skipReveal)
            {
                fadeT += Time.unscaledDeltaTime;
                SetLocationCaption(caption, 1f - Mathf.Clamp01(fadeT / IntroTextFadeDur));
                yield return null;
            }
        }
        SetLocationCaption("", 0f);
        // 揭开
        yield return null;
        float bgFadeT = 0f;
        while (bgFadeT < IntroBgFadeDur && !_skipReveal)
        {
            bgFadeT += Time.unscaledDeltaTime;
            SetRevealBlack(1f - Mathf.Clamp01(bgFadeT / IntroBgFadeDur));
            yield return null;
        }
        SetRevealBlack(0f);
        SetBgDim(0.42f);
        _revealing = false;
        _skipReveal = false;
    }

    public void SetDialogueChromeVisible(bool on)
    {
        if (dialogueBoxImage != null) dialogueBoxImage.gameObject.SetActive(on);
        if (leftPortraitImage != null && !on) leftPortraitImage.gameObject.SetActive(false);
        if (rightPortraitImage != null && !on) rightPortraitImage.gameObject.SetActive(false);
        if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(on && !string.IsNullOrEmpty(_initiatorName));
        if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(on && !string.IsNullOrEmpty(_otherName));
        if (skipButton != null) skipButton.gameObject.SetActive(on);
        if (_autoButton != null) _autoButton.gameObject.SetActive(on);
        if (nextArrowImage != null) nextArrowImage.gameObject.SetActive(on);
        if (advanceButton != null)
        {
            advanceButton.gameObject.SetActive(on);
            advanceButton.interactable = on;
            var img = advanceButton.targetGraphic as Image;
            if (img != null) img.raycastTarget = on;
        }
        var choice = transform.Find("ChoicePanel");
        if (choice != null && !on) choice.gameObject.SetActive(false);
        if (!on) SetLocationCaption("", 0f);
    }

    void EnsureRevealBlack()
    {
        if (_revealBlack != null) return;
        var go = new GameObject("RevealBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _revealBlack = go.GetComponent<Image>();
        // 新铁律（2026-09-19）：游戏内揭示黑幕也不给纯黑，封顶 0.8（见 SetRevealBlack）。
        _revealBlack.color = new Color(0f, 0f, 0f, 0.8f);
        _revealBlack.raycastTarget = false;
        go.transform.SetSiblingIndex(1);
    }

    void EnsureBgDim()
    {
        if (_bgDim != null) return;
        EnsureSceneBackground();
        if (sceneBackgroundImage == null) return;
        Transform host = sceneBackgroundImage.transform.parent != null
            ? sceneBackgroundImage.transform.parent
            : sceneBackgroundImage.transform;
        var t = host.Find("BgDim");
        if (t != null)
        {
            _bgDim = t.GetComponent<Image>();
            return;
        }
        var go = new GameObject("BgDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _bgDim = go.GetComponent<Image>();
        _bgDim.color = new Color(0f, 0f, 0f, 0f);
        _bgDim.raycastTarget = false;
        go.transform.SetAsLastSibling();
    }

    public void SetRevealBlack(float a)
    {
        EnsureRevealBlack();
        if (_revealBlack == null) return;
        _revealBlack.gameObject.SetActive(a > 0.01f);
        _revealBlack.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
    }

    public void SetBgDim(float a)
    {
        EnsureBgDim();
        if (_bgDim == null) return;
        _bgDim.gameObject.SetActive(a > 0.01f);
        _bgDim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
    }

    /// <summary>
    /// 当前是否还挂着场景背景（图在 + 节点活着）。
    /// 2026-10-10 修：命名界面调 Hide() 会 SetSceneBackground(null) 清掉背景，
    /// 后续台词若背景 ID 没变就不会重装 —— 靠这个查询判断要不要补装。
    /// </summary>
    public bool HasSceneBackground()
    {
        if (sceneBackgroundImage == null) return false;
        if (sceneBackgroundImage.sprite == null) return false;
        var host = sceneBackgroundImage.transform.parent != null
            && sceneBackgroundImage.transform.parent != transform
            ? sceneBackgroundImage.transform.parent
            : sceneBackgroundImage.transform;
        return host.gameObject.activeInHierarchy && sceneBackgroundImage.gameObject.activeInHierarchy;
    }

    public void SetSceneBackgroundAlpha(float a)
    {
        if (sceneBackgroundImage == null) return;
        var c = sceneBackgroundImage.color;
        c.a = Mathf.Clamp01(a);
        sceneBackgroundImage.color = c;
        sceneBackgroundImage.enabled = a > 0.01f && sceneBackgroundImage.sprite != null;
    }

    void EnsureLocationCaption()
    {
        if (_locationCaption != null) return;
        var go = new GameObject("LocationCaption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.15f, 0.44f);
        rt.anchorMax = new Vector2(0.85f, 0.56f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        _locationCaption = go.GetComponent<Text>();
        _locationCaption.alignment = TextAnchor.MiddleCenter;
        _locationCaption.fontSize = 52;
        _locationCaption.fontStyle = FontStyle.Bold;
        _locationCaption.color = new Color(1f, 0.95f, 0.82f, 1f);
        _locationCaption.raycastTarget = false;
        _locationCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
        _locationCaption.verticalOverflow = VerticalWrapMode.Overflow;
        var font = GameFonts.GetChinese();
        if (font != null) _locationCaption.font = font;
        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
        shadow.effectDistance = new Vector2(4f, -4f);
        go.transform.SetSiblingIndex(2);
    }

    void SetLocationCaption(string title, float alpha)
    {
        EnsureLocationCaption();
        if (_locationCaption == null) return;
        _locationCaption.text = title ?? "";
        var c = _locationCaption.color;
        c.a = Mathf.Clamp01(alpha);
        _locationCaption.color = c;
        bool show = alpha > 0.02f && !string.IsNullOrEmpty(title);
        _locationCaption.gameObject.SetActive(show);
        if (show) RaiseLocationCaption();
    }

    /// <summary>地点名压在黑幕 / 残留道具之上。</summary>
    void RaiseLocationCaption()
    {
        if (_locationCaption == null) return;
        if (_revealBlack != null)
            _locationCaption.transform.SetSiblingIndex(_revealBlack.transform.GetSiblingIndex() + 1);
        _locationCaption.transform.SetAsLastSibling();
    }

    void ClearStoryPropForReveal()
    {
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }
        SetStoryPropImmediate(null);
    }
}
