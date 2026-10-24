using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：立绘与名牌编排（朝向翻转、单人/双人模式、说话高亮、待机动画、选项显隐、布局缓存）
public partial class DialogueUI
{

    void HidePortraitsForStoryProp()
    {
        if (leftPortraitImage != null) leftPortraitImage.gameObject.SetActive(false);
        if (rightPortraitImage != null) rightPortraitImage.gameObject.SetActive(false);
        if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(false);
        if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(false);
        SetNamePlateIconVisible(leftNameIcon, false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    /// <summary>
    /// 清掉左右立绘与名牌（sprite 置空 + 隐藏）。
    /// 【2026-10-14 主人报「回到城后剧情立绘不消失，一直挡住主界面」】
    /// 走 <c>keepSceneArt</c> 分支收尾时，<see cref="Hide"/> 不会被调用（那是整个对话层的隐藏），
    /// 立绘就留在场景上挡住主界面 —— 这里给收尾流程一个专清立绘的出口。
    /// </summary>
    public void ClearPortraits()
    {
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
        if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(false);
        if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(false);
        SetNamePlateIconVisible(leftNameIcon, false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    StoryPortraitPresenter.Context BuildPortraitContext()
    {
        return new StoryPortraitPresenter.Context
        {
            CanvasRt = transform as RectTransform,
            DialogueBox = dialogueBoxImage,
            MobileLiftY = GetMobileLiftY(),
            PlaceBehindDialogueBox = PlacePortraitBehindDialogueBox
        };
    }

    void ApplySoloNamePlate(bool hasPortrait)
    {
        string name = !string.IsNullOrEmpty(_otherName) ? _otherName : _initiatorName;
        if (leftNamePlateImage != null)
        {
            leftNamePlateImage.gameObject.SetActive(hasPortrait && !string.IsNullOrEmpty(name));
            leftNamePlateImage.color = Color.white;
            // 单人：只用左名牌，X 归零；Y / 字号 / 对齐沿用预制体
            var rt = leftNamePlateImage.rectTransform;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(0f, p.y);
        }
        if (rightNamePlateImage != null)
            rightNamePlateImage.gameObject.SetActive(false);
        if (leftNameText != null)
        {
            leftNameText.text = name ?? "";
            leftNameText.color = new Color(1f, 0.95f, 0.85f);
            ApplyNameTypography(leftNameText);
        }
        SetNamePlateIconVisible(leftNameIcon, false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    void CacheLayoutsIfNeeded()
    {
        if (_layoutCached) return;
        _leftPortraitLayout = StoryPortraitPresenter.Capture(leftPortraitImage);
        _rightPortraitLayout = StoryPortraitPresenter.Capture(rightPortraitImage);
        _leftPlateLayout = Capture(leftNamePlateImage);
        _rightPlateLayout = Capture(rightNamePlateImage);
        _layoutCached = true;
    }

    static RtLayout Capture(Image img)
    {
        var lay = new RtLayout();
        if (img == null) return lay;
        var rt = img.rectTransform;
        lay.amin = rt.anchorMin;
        lay.amax = rt.anchorMax;
        lay.pivot = rt.pivot;
        lay.pos = rt.anchoredPosition;
        lay.scale = rt.localScale;
        return lay;
    }

    static void RestoreLayout(Image img, RtLayout lay)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.anchorMin = lay.amin;
        rt.anchorMax = lay.amax;
        rt.pivot = lay.pivot;
        rt.anchoredPosition = lay.pos;
        rt.localScale = lay.scale;
        img.gameObject.SetActive(true);
    }

    static RectTransform GetPortraitHostRt(Image portrait)
        => StoryPortraitPresenter.GetHostRtPublic(portrait);

    /// <param name="narration">旁白/非角色台词：立绘暗掉；角色说话则亮起。</param>
    void ApplySoloHighlight(bool narration = false)
    {
        bool propOn = storyPropImage != null && storyPropImage.gameObject.activeSelf && storyPropImage.sprite != null;
        // 道具在场时立绘本就藏/暗；旁白时强制暗掉仍在场的立绘
        bool dim = propOn || narration;
        SetPortraitDim(rightPortraitImage, dim);
        SetPortraitDim(leftPortraitImage, dim);
        bool hasName = !string.IsNullOrEmpty(_otherName) || !string.IsNullOrEmpty(_initiatorName);
        if (leftNamePlateImage != null)
            leftNamePlateImage.gameObject.SetActive(hasName && !narration);
        SetPlateActive(leftNamePlateImage, leftNameText, !narration && hasName);
        // 名牌图自带头像位，子 Icon 保持关闭
        SetNamePlateIconVisible(leftNameIcon, false);
        if (rightNamePlateImage != null)
            rightNamePlateImage.gameObject.SetActive(false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    /// <summary>兼容旧调用；speakerSide: -1 左(发起方) / 0 旁白 / 1 右(对方)</summary>
    public void Show(string leftName, string rightName, string content, int speakerSide = -1, Action onAdvance = null)
    {
        ShowLine(leftName, rightName, content, null, null, null, speakerIsInitiator: speakerSide != 1, onAdvance: onAdvance);
        if (speakerSide == 0)
            SetSpeakerHighlight(0);
    }

    public void SetContent(string content, bool speakerIsInitiator = true)
    {
        BeginTyping(content ?? "");
        SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        HideChoices();
        SetAdvanceInteractable(true);
    }

    /// <summary>
    /// 显示选项（1~3 条）。有选项时隐藏继续箭头，点空白不推进。
    /// </summary>
    public void ShowChoices(string[] labels, Action<int> onChoice)
    {
        _onChoice = onChoice;
        if (labels == null || labels.Length == 0)
        {
            HideChoices();
            return;
        }

        int count = Mathf.Min(labels.Length, MaxChoices);
        _choicesVisible = true;
        SetAdvanceInteractable(false);
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(false);

        var panel = transform.Find("ChoicePanel");
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            EnsureChoiceDim(panel);
        }

        for (int i = 0; i < MaxChoices; i++)
        {
            bool on = i < count && !string.IsNullOrEmpty(labels[i]);
            if (choiceButtons != null && i < choiceButtons.Length && choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(on);
            if (on && choiceTexts != null && i < choiceTexts.Length && choiceTexts[i] != null)
                choiceTexts[i].text = labels[i];
        }
    }

    /// <summary>选项区背后半透明黑底（运行时加，不写回 prefab）。</summary>
    void EnsureChoiceDim(Transform choicePanel)
    {
        if (choicePanel == null) return;
        Transform existing = choicePanel.Find("ChoiceDim");
        if (existing != null)
        {
            existing.SetAsFirstSibling();
            existing.gameObject.SetActive(true);
            return;
        }

        var go = new GameObject("ChoiceDim", typeof(RectTransform));
        go.transform.SetParent(choicePanel, false);
        go.transform.SetAsFirstSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-24f, -16f);
        rt.offsetMax = new Vector2(24f, 16f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        img.raycastTarget = false;
    }

    public void HideChoices()
    {
        _choicesVisible = false;
        var panel = transform.Find("ChoicePanel");
        if (panel != null) panel.gameObject.SetActive(false);
        if (choiceButtons == null) return;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 左右立绘朝向。发起方在左默认朝右；对方在右默认朝左（水平翻转）。
    /// 若当前说话方在右，仍保持面对面；仅高亮变化。需要「对调方向」时可再调此方法。
    /// </summary>
    public void ApplyFacing(bool leftFacingRight, bool rightFacingRight)
    {
        SetPortraitFacing(leftPortraitImage, leftFacingRight);
        SetPortraitFacing(rightPortraitImage, rightFacingRight);
    }

    /// <summary>
    /// 对方开口时：对调双方朝向（左改朝左、右改朝右），形成「转头看对方」的感觉；
    /// 也可只用于高亮，朝向仍面对面——默认在 SetSpeakerHighlight 里保持面对面。
    /// </summary>
    public void FaceEachOther()
    {
        ApplyFacing(leftFacingRight: true, rightFacingRight: false);
    }

    void SetPortraitFacing(Image img, bool faceRight)
    {
        if (img == null) return;
        bool flip = portraitArtFacesRight ? !faceRight : faceRight;
        var s = img.rectTransform.localScale;
        float ax = Mathf.Abs(s.x);
        if (ax < 0.01f) ax = 1f;
        s.x = ax * (flip ? -1f : 1f);
        s.y = Mathf.Abs(s.y) < 0.01f ? 1f : Mathf.Abs(s.y);
        s.z = 1f;
        img.rectTransform.localScale = s;
    }

    /// <summary>右侧立绘强制朝左（画面中心），避免背对玩家。</summary>
    void ForceRightPortraitFaceInward()
    {
        if (rightPortraitImage == null) return;
        var s = rightPortraitImage.rectTransform.localScale;
        float ay = Mathf.Abs(s.y);
        if (ay < 0.01f) ay = 1f;
        s.x = -Mathf.Abs(s.x < 0.01f ? 1f : s.x);
        s.y = ay;
        s.z = 1f;
        rightPortraitImage.rectTransform.localScale = s;
    }

    /// <summary>立绘用 Sprite 原始像素尺寸，禁止拉伸进固定框。</summary>
    public static void ApplyPortraitNativeSize(Image img)
    {
        if (img == null) return;
        img.preserveAspect = true;
        img.type = Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f;
        if (img.sprite == null)
        {
            img.enabled = false;
            return;
        }
        img.enabled = true;
        var sp = img.sprite;
        float w = sp.rect.width;
        float h = sp.rect.height;
        if (w < 1f && sp.texture != null) w = sp.texture.width;
        if (h < 1f && sp.texture != null) h = sp.texture.height;
        img.rectTransform.sizeDelta = new Vector2(w, h);
    }

    /// <summary>正文按对话框可视区域自动换行，避免挤成一行或溢出。</summary>
    void FitDialogueBodyToBox()
    {
        EnsureTextClip();
        if (dialogueText == null) return;
        dialogueText.alignment = TextAnchor.UpperLeft;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        dialogueText.resizeTextForBestFit = false;
        if (DialogueBodyFontSize > 0)
            dialogueText.fontSize = DialogueBodyFontSize;
        ScrollDialogueText();
    }

    public void SetSpeakerHighlight(int speakerSide)
    {
        if (_soloMode)
        {
            // 0=旁白暗立绘；角色说话（-1/1）亮起
            ApplySoloHighlight(narration: speakerSide == 0);
            SyncStoryPortraitIdleMotion();
            return;
        }
        // -1 左说话 / 1 右说话 / 0 旁白两边都暗
        bool leftSpeak = speakerSide == -1;
        bool rightSpeak = speakerSide == 1;
        bool narration = speakerSide == 0;
        SetPlateActive(leftNamePlateImage, leftNameText, leftSpeak);
        SetPlateActive(rightNamePlateImage, rightNameText, rightSpeak);
        SetNamePlateIconVisible(leftNameIcon, leftSpeak && !string.IsNullOrEmpty(_initiatorName));
        SetNamePlateIconVisible(rightNameIcon, rightSpeak && !string.IsNullOrEmpty(_otherName));

        if (speakerSide == -1)
        {
            SetPortraitDim(leftPortraitImage, false);
            SetPortraitDim(rightPortraitImage, true);
            FaceEachOther();
        }
        else if (speakerSide == 1)
        {
            SetPortraitDim(leftPortraitImage, true);
            SetPortraitDim(rightPortraitImage, false);
            // 对方说话：对调朝向，右侧说话者面朝左看发起方（仍面对面），并保证翻转正确
            FaceEachOther();
        }
        else
        {
            // 旁白：两边立绘都暗（非对方/非发起方在说）
            SetPortraitDim(leftPortraitImage, true);
            SetPortraitDim(rightPortraitImage, true);
            FaceEachOther();
        }
        if (narration)
        {
            if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(false);
            if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(false);
        }
        SyncStoryPortraitIdleMotion();
    }

    /// <summary>剧情立绘待机：呼吸 + 浮动 + 微倾（与酒馆老板娘同款）。</summary>
    void SyncStoryPortraitIdleMotion()
    {
        SyncOnePortraitIdleMotion(leftPortraitImage, 0f);
        SyncOnePortraitIdleMotion(rightPortraitImage, 0.5f);
    }

    static void SyncOnePortraitIdleMotion(Image img, float phaseSeed)
    {
        if (img == null) return;
        var host = GetPortraitHostRt(img);
        if (!img.gameObject.activeSelf || img.sprite == null)
        {
            if (host != null)
            {
                var idleHost = host.GetComponent<PortraitIdleMotion>();
                if (idleHost != null) idleHost.enabled = false;
            }
            var idleImg = img.GetComponent<PortraitIdleMotion>();
            if (idleImg != null) idleImg.enabled = false;
            return;
        }
        var onImg = img.GetComponent<PortraitIdleMotion>();
        if (onImg != null) onImg.enabled = false;
        if (host == null) return;
        host.pivot = new Vector2(0.5f, 0f);
        PortraitIdleMotion.EnsureOn(host, phaseSeed, breathAmount: 0.006f);
    }

    static void SetPortraitDim(Image img, bool dim)
    {
        if (img == null) return;
        img.color = dim ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
    }

    static void SetPlateActive(Image plate, Text name, bool active)
    {
        if (plate != null)
            plate.color = active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        if (name != null)
        {
            name.color = active ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.65f, 0.55f, 0.7f);
            ApplyNameTypography(name);
        }
    }

    static void ApplyNameTypography(Text name)
    {
        if (name == null) return;
        // 字号 / 对齐 / 样式以预制体为准，仅补缺省中文字体
        if (name.font == null) name.font = GameFonts.GetChinese();
    }

    static void SetNamePlateIconVisible(Image icon, bool visible)
    {
        if (icon == null) return;
        // 需求：对话名牌头像 icon 统一关闭。
        icon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 立绘盖在背景上、但必须在对话框下面。
    /// 注意：若立绘本来就在框下方，再 SetSiblingIndex(框的下标) 会把框挤下去，立绘反而跑到前面。
    /// </summary>
    void PlacePortraitBehindDialogueBox(Transform portrait)
    {
        if (portrait == null || dialogueBoxImage == null) return;
        if (portrait.parent != dialogueBoxImage.transform.parent) return;
        int boxIdx = dialogueBoxImage.transform.GetSiblingIndex();
        int pIdx = portrait.GetSiblingIndex();
        if (pIdx >= boxIdx)
            portrait.SetSiblingIndex(boxIdx);
    }

    float GetMobileLiftY()
    {
        var canvasRt = transform as RectTransform;
        float h = canvasRt != null ? canvasRt.rect.height : 0f;
        if (h < 64f) h = GameConfig.DESIGN_HEIGHT;
        // 【2026-10-24 主人「对话框往上点」】抬升系数 0.08→0.11（720×1600 下约多抬 48px）。要再调改这一个系数。
        return h * 0.11f;
    }

    void ApplyDialogueBoxLift()
    {
        if (dialogueBoxImage == null) return;
        var rt = dialogueBoxImage.rectTransform;
        float baseY = _dialogueBoxBaseY >= 0f ? _dialogueBoxBaseY : rt.anchoredPosition.y;
        _dialogueBoxBaseY = baseY;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + GetMobileLiftY());
    }

    float _dialogueBoxBaseY = -1f;

    void SetAdvanceInteractable(bool on)
    {
        if (advanceButton != null)
            advanceButton.interactable = on;
    }
}
