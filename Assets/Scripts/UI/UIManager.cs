using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// UI管理器：战斗结算 / 关卡选择 / 章节二选一
/// </summary>
public class UIManager : Singleton<UIManager>
{
    Font _font;
    GameObject _chapterChoicePanel;
    GameObject _stageSelectPanel;

    Font UIFont
    {
        get
        {
            if (_font == null)
                _font = GameFonts.GetChinese();
            return _font;
        }
    }

    public void ShowToast(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (ShouldSuppressToastForBubble())
            return;
        Debug.Log("[Toast] " + msg);
        GlobalToastUI.Show(msg);
    }

    /// <summary>有对话/头顶气泡/看板娘气泡时，不叠屏幕 Toast。</summary>
    static bool ShouldSuppressToastForBubble() => GlobalToastUI.AnyBubbleShowing;

    /// <summary>
    /// 传送门后：石墩关卡图（锁晃动解锁）→ 滚盘 → 旗落到石墩 + btn06 描边 → 点石墩进关。
    /// </summary>
    public void ShowStageSelectUI(List<StageData> stages, Action onClose = null)
    {
        CloseStageSelect();

        if (stages == null || stages.Count == 0)
            stages = ChapterManager.Instance?.availableNextStages;

        if (stages == null || stages.Count == 0)
        {
            Debug.LogWarning("[UIManager] 没有下一关可选，回城");
            MercenaryManager.Instance?.ClearAllMercs();
            GameSceneManager.Instance?.ReturnToTown();
            onClose?.Invoke();
            return;
        }

        Debug.Log($"[UIManager] 关卡图流程 候选{stages.Count}条");

        BattleStageMapUI.BeginFlow(stages, stage =>
        {
            if (stage != null)
                ChapterManager.Instance?.SelectStage(stage);
            onClose?.Invoke();
        });
    }

    void CloseStageSelect()
    {
        Time.timeScale = 1f;
        if (_stageSelectPanel != null)
        {
            Destroy(_stageSelectPanel);
            _stageSelectPanel = null;
        }
    }

    static string StageTypeLabel(StageType t)
    {
        switch (StageRoller.NormalizeDisplayType(t))
        {
            case StageType.Elite: return "精英";
            case StageType.Boss: return "Boss";
            case StageType.Rest: return "恢复";
            default: return "普通";
        }
    }

    public void ShowStageClearUI(List<EquipInstance> rewards, int bonusGold, System.Action<EquipInstance> onSelect)
    {
        // 兼容旧回调：新 UI 走 StageClearEquipUI（装备/丢弃）
        StageClearEquipUI.Show(rewards, bonusGold, (picked, equipOrReplace) =>
        {
            if (equipOrReplace)
                onSelect?.Invoke(picked);
            else
                onSelect?.Invoke(null);
        });
    }

    public void ShowChapterClearChoice(Action onReturnTown, Action onNextChapter)
    {
        if (_chapterChoicePanel != null)
            Destroy(_chapterChoicePanel);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[UIManager] 无 Canvas，直接回城");
            onReturnTown?.Invoke();
            return;
        }

        _chapterChoicePanel = new GameObject("ChapterClearChoice", typeof(RectTransform));
        _chapterChoicePanel.transform.SetParent(canvas.transform, false);
        var root = _chapterChoicePanel.GetComponent<RectTransform>();
        StretchFull(root);

        var dim = CreateUiImage(_chapterChoicePanel.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
        StretchFull(dim.rectTransform);

        var panel = CreateUiImage(_chapterChoicePanel.transform, "Panel", new Color(0.12f, 0.1f, 0.08f, 0.95f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520f, 280f);
        prt.anchoredPosition = Vector2.zero;

        var title = CreateUiText(panel.transform, "Title", "章节结算", 36, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -48f);
        trt.sizeDelta = new Vector2(480f, 50f);

        var tip = CreateUiText(panel.transform, "Tip", "选择回城休整，或继续下一章冒险", 22, TextAnchor.MiddleCenter);
        tip.color = new Color(0.85f, 0.8f, 0.7f);
        var tipRt = tip.rectTransform;
        tipRt.anchorMin = new Vector2(0.5f, 0.5f);
        tipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tipRt.anchoredPosition = new Vector2(0f, 20f);
        tipRt.sizeDelta = new Vector2(460f, 40f);

        CreateChoiceButton(panel.transform, "回城", new Vector2(-120f, -70f), () =>
        {
            Destroy(_chapterChoicePanel);
            _chapterChoicePanel = null;
            onReturnTown?.Invoke();
        });
        CreateChoiceButton(panel.transform, "下一章", new Vector2(120f, -70f), () =>
        {
            Destroy(_chapterChoicePanel);
            _chapterChoicePanel = null;
            onNextChapter?.Invoke();
        });

        Time.timeScale = 0f;
    }

    void CreateChoiceButton(Transform parent, string label, Vector2 pos, Action onClick)
    {
        var img = CreateUiImage(parent, "Btn_" + label, new Color(0.45f, 0.32f, 0.18f, 1f));
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(220f, 56f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            onClick?.Invoke();
        });

        var txt = CreateUiText(img.transform, "Label", label, 26, TextAnchor.MiddleCenter);
        StretchFull(txt.rectTransform);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image CreateUiImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Text CreateUiText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UIFont;
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public void ShowLegacyChooseUI(List<EquipInstance> allEquips, Action<EquipInstance> onSelect)
    {
        LegacyChooseUI.Show(allEquips, onSelect);
    }

    public void ShowMerchantUI(List<EquipInstance> goods, Action onClose)
    {
        ShowToast("本版本未开放商人关");
        onClose?.Invoke();
    }

    public void ShowEnchantUI(Action<GridBackpackSystem.BackpackItem, EnchantData> onSelect)
    {
        // 附魔关由 CraftStagePopupUI + CraftStageApply 处理；兼容旧回调
        if (CraftStageApply.TryEnchantRandom(out string msg))
            ShowToast(msg);
        else if (!string.IsNullOrEmpty(msg))
            ShowToast(msg);
        onSelect?.Invoke(null, null);
    }

    public void ShowCurseUI(List<CurseBuff> options, Action<CurseBuff> onSelect)
    {
        ShowToast("本版本未开放诅咒关");
        onSelect?.Invoke(null);
    }

    public void ShowRestUI(Action onHeal, Action onDecompose)
    {
        // 恢复关：弹「生命恢复」窗，回 50% 血；分解入口暂不开放
        RestStagePopupUI.Show(() => onHeal?.Invoke());
    }

    public void ShowDecomposeUI(Action<GridBackpackSystem.BackpackItem> onSelect)
    {
        onSelect?.Invoke(null);
    }
}
