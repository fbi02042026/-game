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
        Debug.Log("[Toast] " + msg);
    }

    /// <summary>传送门后打开关卡选择（简易面板；有 ChapterMapUI 则优先）</summary>
    public void ShowStageSelectUI(List<StageData> stages, Action onClose = null)
    {
        Debug.Log($"显示关卡选择，可选关卡数：{stages?.Count ?? 0}");

        if (ChapterMapUI.Instance != null)
        {
            ChapterMapUI.Instance.ShowAfterBattle();
            onClose?.Invoke();
            return;
        }

        if (stages == null || stages.Count == 0)
        {
            onClose?.Invoke();
            return;
        }

        if (_stageSelectPanel != null)
            Destroy(_stageSelectPanel);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            ChapterManager.Instance?.SelectStage(stages[0]);
            onClose?.Invoke();
            return;
        }

        Time.timeScale = 0f;
        _stageSelectPanel = new GameObject("StageSelectPanel", typeof(RectTransform));
        _stageSelectPanel.transform.SetParent(canvas.transform, false);
        var root = _stageSelectPanel.GetComponent<RectTransform>();
        StretchFull(root);

        var dim = CreateUiImage(_stageSelectPanel.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
        StretchFull(dim.rectTransform);

        var panel = CreateUiImage(_stageSelectPanel.transform, "Panel", new Color(0.12f, 0.1f, 0.08f, 0.96f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 360f);

        var title = CreateUiText(panel.transform, "Title", "选择下一关", 34, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -40f);
        trt.sizeDelta = new Vector2(500f, 44f);

        float y = 40f;
        int show = Mathf.Min(stages.Count, 4);
        for (int i = 0; i < show; i++)
        {
            StageData st = stages[i];
            string label = $"第{st.stageIndex + 1}关 · {StageTypeLabel(st.type)}";
            float yy = y - i * 66f;
            CreateChoiceButton(panel.transform, label, new Vector2(0f, yy), () =>
            {
                CloseStageSelect();
                ChapterManager.Instance?.SelectStage(st);
                onClose?.Invoke();
            });
        }

        CreateChoiceButton(panel.transform, "回城", new Vector2(0f, -130f), () =>
        {
            CloseStageSelect();
            MercenaryManager.Instance?.ClearAllMercs();
            GameSceneManager.Instance?.ReturnToTown();
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
        switch (t)
        {
            case StageType.Elite: return "精英";
            case StageType.Boss: return "Boss";
            case StageType.Merchant: return "商人";
            case StageType.Enchant: return "附魔";
            case StageType.Curse: return "诅咒";
            case StageType.Rest: return "休息";
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
        Debug.Log("死亡选遗产");
        onSelect?.Invoke(null);
    }

    public void ShowMerchantUI(List<EquipInstance> goods, Action onClose)
    {
        Debug.Log("商人关");
        onClose?.Invoke();
    }

    public void ShowEnchantUI(Action<GridBackpackSystem.BackpackItem, EnchantData> onSelect)
    {
        Debug.Log("附魔关");
        onSelect?.Invoke(null, null);
    }

    public void ShowCurseUI(List<CurseBuff> options, Action<CurseBuff> onSelect)
    {
        Debug.Log("诅咒关");
        onSelect?.Invoke(options != null && options.Count > 0 ? options[0] : null);
    }

    public void ShowRestUI(Action onHeal, Action onDecompose)
    {
        Debug.Log("休息关，自动回血");
        onHeal?.Invoke();
    }

    public void ShowDecomposeUI(Action<GridBackpackSystem.BackpackItem> onSelect)
    {
        onSelect?.Invoke(null);
    }
}
