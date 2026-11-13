using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：终幕与道具（黑屏字幕终幕、整体 Hide、剧情道具叠图与淡出）
public partial class DialogueUI
{

    // ---------- 结局终幕：黑屏字幕（无立绘 / 无对话框） ----------

    /// <summary>
    /// 结局终幕：全屏纯黑 + 居中一句字幕，点屏幕任意处继续。
    /// 收起立绘、对话框、名牌、跳过，只留黑幕和一行字。
    /// （2026-09-21 定案：裂缝意志不出正式立绘，结局收尾改成黑屏 + 一句话。）
    /// </summary>
    public void ShowEndBlackLine(string text, Action onAdvance)
    {
        _onAdvance = onAdvance;
        _onSkip = null;
        _onChoice = null;
        _typing = false;
        _typeComplete = true;
        _revealing = false;
        _choicesVisible = false;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StoryDialogue);
        Canvas.ForceUpdateCanvases();

        SetDialogueChromeVisible(false);
        SetStoryProp(null);
        SetSceneBackground(null);
        SetBgDim(0f);
        SetRevealBlack(1f);
        // 字幕复用「地点名」层：它天生居中，且会被抬到黑幕之上
        SetLocationCaption(text ?? "", 1f);

        // 黑屏也要能点继续：把全屏点击层重新打开（chrome 关闭时被关掉了）
        if (advanceButton != null)
        {
            advanceButton.gameObject.SetActive(true);
            advanceButton.interactable = true;
            var img = advanceButton.targetGraphic as Image;
            if (img != null) img.raycastTarget = true;
        }
    }

    /// <summary>收掉终幕黑屏字幕（走 keepSceneArt 分支时也必须调，否则黑屏会留在场景上）。</summary>
    public void ClearEndBlackLine()
    {
        // 没建过节点说明这段剧情根本没用过黑屏，不在这里凭空造节点
        if (_revealBlack != null) SetRevealBlack(0f);
        if (_locationCaption != null) SetLocationCaption("", 0f);
    }

    public void Hide()
    {
        _onAdvance = null;
        _onSkip = null;
        _onChoice = null;
        _revealing = false;
        _typing = false;
        _typeComplete = false;
        _arrowBounce = false;
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        HideNextArrow();
        HideChoices();
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }
        SetStoryPropImmediate(null);
        SetLocationCaption("", 0f);
        SetSceneBackground(null);
        SetRevealBlack(0f);
        SetBgDim(0f);
        HideFullDim();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 关掉对话窗自带的全屏黑底（prefab 的 Dim，0.7）。
    /// 【2026-11-13 主人报「第二回进的时候剧情底又变透明了一些」】DialogueUI 是 DDOL 单例，
    /// Dim 在预制体里初始是开着的，Hide() 关掉后全项目没有任何地方再打开它 —— 于是首次弹剧情
    /// 叠两层黑底（DialogueUI.Dim + 战前选择常驻底），第二回只剩一层，看着就更透。
    /// 战前选择统一只留 BattleAmbienceBanner.ShowPersistent 那一层 50% 底：开播前调本方法收口，
    /// 幂等，保证每次进的浓度一致；其它剧情不受影响（不写入口就不会动它的 Dim）。
    /// </summary>
    public void HideFullDim()
    {
        var dim = transform.Find("Dim");
        if (dim != null) dim.gameObject.SetActive(false);
    }

    /// <summary>
    /// 【2026-11-13 主人拍板「所有剧情用黑色半透兜底」】每次剧情开播先把 prefab 的 Dim 拉起来：
    /// 有剧情背景时 <see cref="SetSceneBackground"/>(非 null) 会把它关掉（用背景图）；
    /// 没背景就留这层黑底，剧情窗不再裸浮在战斗画面上。
    /// 与 Hide() 的关系：Hide 关掉、下一场 Play 开头由本方法拉起 —— 一开一关都有唯一出口。
    /// 战前选择（hideFullDim=true）不走本方法：只留 ShowPersistent 的 50% 常驻底。
    /// </summary>
    public void ShowFullDim()
    {
        var dim = transform.Find("Dim");
        if (dim != null) dim.gameObject.SetActive(true);
    }

    /// <summary>显示或清除剧情道具叠图（null = 立刻隐藏）。</summary>
    public void SetStoryProp(Sprite sprite)
    {
        EnsureStoryProp();
        if (storyPropImage == null) return;
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }

        if (sprite == null)
        {
            // 下一句无道具：立刻关掉，避免盖住立绘 / 地点名
            SetStoryPropImmediate(null);
            return;
        }

        storyPropImage.sprite = sprite;
        storyPropImage.color = Color.white;
        storyPropImage.preserveAspect = true;
        storyPropImage.type = Image.Type.Simple;
        storyPropImage.gameObject.SetActive(true);
        ClampStoryPropSize();
        RaiseStoryPropAboveDialogue();
        HidePortraitsForStoryProp();
    }

    /// <summary>道具图必须在对话框之上，否则委托书等会被 DialogueBox 挡住。</summary>
    void RaiseStoryPropAboveDialogue()
    {
        if (storyPropImage == null) return;
        var skip = skipButton != null ? skipButton.transform : null;
        if (skip != null)
            storyPropImage.transform.SetSiblingIndex(skip.GetSiblingIndex());
        else
            storyPropImage.transform.SetAsLastSibling();
    }

    void SetStoryPropImmediate(Sprite sprite)
    {
        EnsureStoryProp();
        if (storyPropImage == null) return;
        if (sprite == null)
        {
            storyPropImage.sprite = null;
            storyPropImage.gameObject.SetActive(false);
            return;
        }
        storyPropImage.sprite = sprite;
        storyPropImage.color = Color.white;
        storyPropImage.preserveAspect = true;
        storyPropImage.type = Image.Type.Simple;
        storyPropImage.gameObject.SetActive(true);
        ClampStoryPropSize();
        RaiseStoryPropAboveDialogue();
        HidePortraitsForStoryProp();
    }

    void ClampStoryPropSize()
    {
        if (storyPropImage == null || storyPropImage.sprite == null) return;
        Canvas.ForceUpdateCanvases();

        var sprite = storyPropImage.sprite;
        storyPropImage.SetNativeSize();
        var rt = storyPropImage.rectTransform;

        Vector2 n = rt.sizeDelta;
        if (n.x < 1f || n.y < 1f)
        {
            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 100f;
            n = new Vector2(sprite.rect.width / ppu, sprite.rect.height / ppu);
            rt.sizeDelta = n;
            if (n.x < 1f || n.y < 1f) return;
        }

        var canvasRt = transform as RectTransform;
        float canvasW = canvasRt != null ? canvasRt.rect.width : 0f;
        float canvasH = canvasRt != null ? canvasRt.rect.height : 0f;
        if (canvasW < 64f) canvasW = GameConfig.DESIGN_WIDTH;
        if (canvasH < 64f) canvasH = GameConfig.DESIGN_HEIGHT;

        float maxW = canvasW * 0.72f;
        float maxH = canvasH * 0.55f;
        if (maxW < 1f || maxH < 1f)
        {
            rt.localScale = Vector3.one;
            return;
        }

        float k = Mathf.Min(1f, maxW / n.x, maxH / n.y);
        if (k < 1f)
            rt.sizeDelta = n * k;
        rt.localScale = Vector3.one;
    }

    IEnumerator FadeOutStoryProp()
    {
        var img = storyPropImage;
        if (img == null) yield break;
        float dur = 0.55f;
        float t = 0f;
        Color c = img.color;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / dur);
            img.color = c;
            yield return null;
        }
        img.sprite = null;
        img.gameObject.SetActive(false);
        img.color = Color.white;
        _propFadeCo = null;
    }
}
