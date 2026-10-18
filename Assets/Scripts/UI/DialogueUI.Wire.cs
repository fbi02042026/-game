using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：点击接线（WireClicks、推进/跳过/选项回调）
public partial class DialogueUI
{

    public void WireClicks()
    {
        if (_wired) return;
        _wired = true;

        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(OnClickAdvance);
            advanceButton.onClick.AddListener(OnClickAdvance);
        }
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnClickSkip);
            skipButton.onClick.AddListener(OnClickSkip);
        }
        for (int i = 0; i < MaxChoices; i++)
        {
            int idx = i;
            if (choiceButtons == null || idx >= choiceButtons.Length || choiceButtons[idx] == null)
                continue;
            choiceButtons[idx].onClick.RemoveAllListeners();
            choiceButtons[idx].onClick.AddListener(() => OnClickChoice(idx));
        }
    }

    void OnClickAdvance()
    {
        if (_revealing)
        {
            _skipReveal = true;
            return;
        }
        if (_typing)
        {
            CompleteTyping();
            return;
        }
        if (!_typeComplete) return;
        if (_choicesVisible) return; // 有选项时不靠点空白继续
        onAdvance?.Invoke();
        _onAdvance?.Invoke();
    }

    void OnClickSkip()
    {
        if (_revealing)
        {
            _skipReveal = true;
            return;
        }
        onSkip?.Invoke();
        _onSkip?.Invoke();
    }

    void OnClickChoice(int index)
    {
        onChoiceSelected?.Invoke(index);
        _onChoice?.Invoke(index);
    }
}
