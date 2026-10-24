using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：单句展示（ShowLine、对话框放大与抬升、打字机、正文裁剪与滑动回看）
public partial class DialogueUI
{
    /// <summary>
    /// 【2026-10-24 修「长台词点击无反应」】正文溢出启用滚动回看后 dialogueText.raycastTarget=true，
    /// 点击落在正文上被 ScrollRect 的 OnPointerDown 接走 —— Unity 的 PointerClick 只派发给
    /// 「按下对象父链」上的 IPointerClickHandler，TextClip 链上没有 → 全屏推进层永远收不到点击
    /// （Editor.log 三次「单行 30 秒无进展」全是长台词：起名后「三件事」句、回城赛琳长句）。
    /// 挂在 TextClip 上：轻点（未拖动）→ 转发 OnClickAdvance 推进；拖动 → 仍走 ScrollRect 回看，互不干扰。
    /// </summary>
    public class TapAdvanceRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public System.Action OnTap;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData) => OnTap?.Invoke();
    }

    /// <summary>
    /// 开一场对话：发起方在左（面朝右），对方在右（面朝左）。
    /// speakerIsInitiator=true 表示当前说话的是发起方。
    /// 立绘按 Sprite 自身像素大小显示（不统一拉伸），仅做水平翻转。
    /// </summary>
    public void ShowLine(
        string initiatorName,
        string otherName,
        string content,
        Sprite initiatorPortrait,
        Sprite otherPortrait,
        Sprite propSprite,
        bool speakerIsInitiator = true,
        Action onAdvance = null,
        Action onSkip = null,
        bool soloCentered = false,
        string initiatorPortraitId = null,
        string otherPortraitId = null,
        bool normalizePortraitSize = false)
    {
        _initiatorName = initiatorName ?? "";
        _otherName = otherName ?? "";
        _onAdvance = onAdvance;
        _onSkip = onSkip;
        _onChoice = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StoryDialogue);
        Canvas.ForceUpdateCanvases();
        SetDialogueChromeVisible(true);
        ApplyDialogueBoxLift();
        FitDialogueBoxAspect(); // 只校正九宫格，不改框尺寸
        // 2026-10-09 主人拍板：对话框体放大 5% —— 只放框体尺寸，不改字号/文字/锚点逻辑
        // 【2026-10-16 主人拍板：字超出不再往外扩框】去掉「多行时上下拉长」逻辑，框体固定基准尺寸；
        // 超长台词由正文区 RectMask2D 裁剪遮挡，打字播完后自动放开上下滑动回看（RefreshDialogueScrollable）。
        if (dialogueBoxImage != null)
        {
            var drt = dialogueBoxImage.rectTransform;
            if (_dialogueBoxBaseSize.sqrMagnitude <= 0f) _dialogueBoxBaseSize = drt.sizeDelta;
            drt.sizeDelta = new Vector2(
                _dialogueBoxBaseSize.x * 1.05f,
                _dialogueBoxBaseSize.y * 1.05f);
        }

        if (leftNameText != null) leftNameText.text = _initiatorName;
        if (rightNameText != null) rightNameText.text = _otherName;
        ApplyNameTypography(leftNameText);
        ApplyNameTypography(rightNameText);
        BeginTyping(content ?? "");
        SetStoryProp(propSprite);
        if (propSprite != null)
        {
            HidePortraitsForStoryProp();
            RaiseStoryPropAboveDialogue();
        }

        _portraitProfile = StoryPortraitLayout.Unified;

        bool newPortraits = initiatorPortrait != null || otherPortrait != null || soloCentered;
        if (propSprite == null && (newPortraits || soloCentered))
        {
            var ctx = BuildPortraitContext();
            ctx.NormalizeSize = normalizePortraitSize;
            CacheLayoutsIfNeeded();
            if (soloCentered)
            {
                _soloMode = true;
                Sprite sp = otherPortrait != null ? otherPortrait : initiatorPortrait;
                if (sp == null)
                {
                    // 失败时清槽，禁止留旧图
                    if (leftPortraitImage != null)
                    {
                        leftPortraitImage.sprite = null;
                        leftPortraitImage.gameObject.SetActive(false);
                    }
                    if (rightPortraitImage != null)
                    {
                        rightPortraitImage.sprite = null;
                        rightPortraitImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    string soloPortraitId = !string.IsNullOrEmpty(otherPortraitId) ? otherPortraitId : initiatorPortraitId;
                    StoryPortraitPresenter.ApplySolo(
                        rightPortraitImage, leftPortraitImage, sp,
                        _rightPortraitLayout, _leftPortraitLayout,
                        _portraitProfile, ctx, soloPortraitId);
                }
                ApplySoloNamePlate(sp != null);
            }
            else
            {
                _soloMode = false;
                RestoreLayout(leftNamePlateImage, _leftPlateLayout);
                RestoreLayout(rightNamePlateImage, _rightPlateLayout);
                StoryPortraitPresenter.ApplyDual(
                    leftPortraitImage, rightPortraitImage,
                    initiatorPortrait, otherPortrait,
                    _leftPortraitLayout, _rightPortraitLayout,
                    _portraitProfile, ctx, initiatorPortraitId, otherPortraitId);
                SetNamePlateIconVisible(leftNameIcon, false);
                SetNamePlateIconVisible(rightNameIcon, false);
            }
        }

        // #region agent log
        if (newPortraits)
        {
            float lH = leftPortraitImage != null ? leftPortraitImage.rectTransform.sizeDelta.y : 0f;
            float rH = rightPortraitImage != null ? rightPortraitImage.rectTransform.sizeDelta.y : 0f;
            DebugAgentLog.Log("H14", "DialogueUI.ShowLine", "portrait_layout",
                $"{{\"solo\":{(soloCentered ? "true" : "false")},\"leftH\":{lH:F1},\"rightH\":{rH:F1},\"leftActive\":{(leftPortraitImage != null && leftPortraitImage.gameObject.activeSelf ? "true" : "false")},\"rightActive\":{(rightPortraitImage != null && rightPortraitImage.gameObject.activeSelf ? "true" : "false")},\"profileH\":{_portraitProfile.screenHeightFrac:F3}}}");
        }
        // #endregion

        if (_soloMode)
        {
            // ShowLine 默认按 speakerIsInitiator；旁白会再由 StoryDirector 调 SetSpeakerHighlight(0)
            ApplySoloHighlight(narration: false);
        }
        else
        {
            // 左右立绘朝向中间；右侧资源朝外时再强制翻一次
            ApplyFacing(leftFacingRight: true, rightFacingRight: false);
            ForceRightPortraitFaceInward();
            SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        }
        SyncStoryPortraitIdleMotion();
        FitDialogueBodyToBox();
        HideChoices();
        SetAdvanceInteractable(true);
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);
    }

    /// <summary>只校正九宫格切图；框的宽高始终用预制体，字多用框内滚动。</summary>
    void FitDialogueBoxAspect()
    {
        if (dialogueBoxImage == null) return;
        Vector4 border = dialogueBoxImage.sprite != null ? dialogueBoxImage.sprite.border : Vector4.zero;
        bool sliced = (border.x + border.y + border.z + border.w) > 0.01f;
        dialogueBoxImage.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        dialogueBoxImage.preserveAspect = !sliced;
    }

    void BeginTyping(string full)
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _fullLineText = full ?? "";
        _typeComplete = false;
        _typing = true;
        HideNextArrow();
        EnsureTextClip();
        RefreshDialogueScrollable(); // 打字开始：关掉回看，点击全部用于「迅速说完」
        _typeCo = StartCoroutine(TypeLineRoutine(_fullLineText));
    }

    IEnumerator TypeLineRoutine(string full)
    {
        if (dialogueText != null) dialogueText.text = "";
        float delay = 1f / Mathf.Max(1f, typeCharsPerSecond);
        for (int i = 1; i <= full.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text = full.Substring(0, i);
            ScrollDialogueText();
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        _typing = false;
        _typeComplete = true;
        _typeCo = null;
        ScrollDialogueText();
        RefreshDialogueScrollable(); // 播完：文本溢出就放开上下滑动回看
        ShowNextArrowBounce();
    }

    void CompleteTyping()
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _typing = false;
        _typeComplete = true;
        if (dialogueText != null)
            dialogueText.text = _fullLineText ?? "";
        ScrollDialogueText();
        RefreshDialogueScrollable(); // 「点击迅速说完」后同样要能上下滑动回看
        ShowNextArrowBounce();
    }

    void ShowNextArrowBounce()
    {
        if (nextArrowImage == null) return;
        nextArrowImage.gameObject.SetActive(true);
        _arrowBounce = true;
        nextArrowImage.rectTransform.anchoredPosition = _arrowBasePos;
    }

    void HideNextArrow()
    {
        _arrowBounce = false;
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(false);
    }

    void EnsureTextClip()
    {
        if (dialogueText == null || _textClip != null) return;
        if (dialogueText.transform.parent != null &&
            dialogueText.transform.parent.GetComponent<RectMask2D>() != null)
        {
            _textClip = dialogueText.transform.parent as RectTransform;
            PrepareDialogueTextForClip();
            return;
        }

        var src = dialogueText.rectTransform;
        var go = new GameObject("TextClip", typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(src.parent, false);
        _textClip = go.GetComponent<RectTransform>();
        _textClip.anchorMin = src.anchorMin;
        _textClip.anchorMax = src.anchorMax;
        _textClip.pivot = src.pivot;
        _textClip.offsetMin = src.offsetMin;
        _textClip.offsetMax = src.offsetMax;
        _textClip.anchoredPosition = src.anchoredPosition;
        _textClip.sizeDelta = src.sizeDelta;
        _textClip.SetSiblingIndex(src.GetSiblingIndex());
        dialogueText.transform.SetParent(_textClip, false);
        PrepareDialogueTextForClip();
    }

    void PrepareDialogueTextForClip()
    {
        if (dialogueText == null) return;
        dialogueText.alignment = TextAnchor.UpperLeft;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = dialogueText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        float h = _textClip != null ? Mathf.Max(1f, _textClip.rect.height) : 160f;
        rt.sizeDelta = new Vector2(0f, h);
    }

    void ScrollDialogueText()
    {
        EnsureTextClip();
        if (dialogueText == null || _textClip == null) return;
        float viewH = Mathf.Max(1f, _textClip.rect.height);
        float pref = Mathf.Max(viewH, dialogueText.preferredHeight);
        var rt = dialogueText.rectTransform;
        rt.sizeDelta = new Vector2(0f, pref);
        // 手动回看模式下位置由 ScrollRect 接管，这里不再抢 anchoredPosition（否则滑动会被拽回底部）
        if (_dialogueScrollRect != null && _dialogueScrollRect.enabled) return;
        float overflow = pref - viewH;
        rt.anchoredPosition = overflow > 1f ? new Vector2(0f, overflow) : Vector2.zero;
    }

    /// <summary>
    /// 【2026-10-15 主人】对话能上下滑动回看：正文超出对话框时，打字播完后放开手动拖拽。
    /// 打字期间必须关掉 —— 那时「点击迅速说完」优先，不能被滑动手势吞掉点击。
    /// </summary>
    void EnsureDialogueScrollRect()
    {
        if (_dialogueScrollRect != null) return;
        if (_textClip == null || dialogueText == null) return;
        _dialogueScrollRect = _textClip.GetComponent<ScrollRect>();
        if (_dialogueScrollRect != null) return;
        var sr = _textClip.gameObject.AddComponent<ScrollRect>();
        sr.content = dialogueText.rectTransform;
        sr.viewport = _textClip;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.scrollSensitivity = 24f;
        sr.enabled = false;
        _dialogueScrollRect = sr;
    }

    /// <summary>
    /// 【2026-10-24 修「长台词点击无反应」】TextClip 上挂轻点转发：溢出回看开启期间
    /// （dialogueText.raycastTarget=true），轻点正文 = 推进对话，拖动 = 滚动回看。
    /// 挂一次即可；未溢出时 raycast 关着，点击照旧穿透到全屏推进层，不会双触发。
    /// </summary>
    void EnsureTapAdvance()
    {
        if (_textClip == null) return;
        var tap = _textClip.GetComponent<TapAdvanceRelay>();
        if (tap == null) tap = _textClip.gameObject.AddComponent<TapAdvanceRelay>();
        tap.OnTap = OnClickAdvance;
    }

    void RefreshDialogueScrollable()
    {
        EnsureTextClip();
        EnsureDialogueScrollRect();
        EnsureTapAdvance();
        if (_dialogueScrollRect == null || dialogueText == null || _textClip == null) return;
        bool overflow = dialogueText.preferredHeight > _textClip.rect.height + 1f;
        bool on = _typeComplete && !_typing && overflow;
        if (_dialogueScrollRect.enabled != on)
            _dialogueScrollRect.enabled = on;
        // 回看时正文要能接到拖拽手势；打字期间关掉，点击一律落到 AdvanceClick 上推进
        dialogueText.raycastTarget = on;
        if (on)
            _dialogueScrollRect.verticalNormalizedPosition = 0f; // 0 = 贴底，停在最新一句
    }
}
