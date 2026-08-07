using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// UI管理器：战斗结算 / 章节二选一等（无预制体时运行时生成简易面板）
/// </summary>
public class UIManager : Singleton<UIManager>
{
    Font _font;
    GameObject _chapterChoicePanel;

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

    public void ShowStageSelectUI(List<StageData> stages, Action onClose = null)
    {
        Debug.Log($"显示关卡选择，可选关卡数：{stages?.Count ?? 0}");
        if (stages != null && stages.Count > 0)
            ChapterManager.Instance.SelectStage(stages[0]);
        onClose?.Invoke();
    }

    public void ShowStageClearUI(List<EquipInstance> rewards, int bonusGold, Action<EquipInstance> onSelect)
    {
        Debug.Log($"关卡通关，奖励装备数：{rewards?.Count ?? 0}，金币：{bonusGold}");
        // TODO: 正式宝箱/选装 UI；暂自动选第一件后继续流程
        EquipInstance pick = (rewards != null && rewards.Count > 0) ? rewards[0] : null;
        onSelect?.Invoke(pick);
    }

    /// <summary>
    /// Boss 结算后二选一：回城 / 下一章（新手引导或剧情可另开接口跳过）
    /// </summary>
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
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

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
        rt.sizeDelta = new Vector2(180f, 56f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            onClick?.Invoke();
        });

        var txt = CreateUiText(img.transform, "Label", label, 28, TextAnchor.MiddleCenter);
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
