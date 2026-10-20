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

    public void ShowToast(string msg) => ShowToast(msg, false);

    /// <summary>
    /// 【2026-10-06 主人拍板】物品变动（获得 / 分解 / 丢弃）走 <paramref name="force"/> = true，
    /// 不被对话 / 引导遮罩 / 气泡压制 —— 主人要求「操作玩家物品的都要弹提示，不管获得还是消失」。
    /// 普通提示仍走默认压制，避免盖住对话。底层仍只走 <see cref="GlobalToastUI.Show"/>。
    /// </summary>
    public void ShowToast(string msg, bool force)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (!force && ShouldSuppressToastForBubble())
            return;
        Debug.Log("[Toast] " + msg);
        GlobalToastUI.Show(msg, force);
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

    public void ShowChapterClearChoice(Action onReturnTown, Action onNextChapter)
        => ShowChoicePanel("章节结算", "选择回城休整，或继续下一章冒险", "回城", "下一章", onReturnTown, onNextChapter);

    /// <summary>
    /// 无限裂隙记录点双选（2026-10-17）：BOSS 记录点层通关后弹「撤离结算 / 继续深入」。
    /// 与章节结算同一套面板，只换标题 / 提示 / 按钮文案。
    /// </summary>
    public void ShowRiftCheckpointChoice(Action onEvacuate, Action onContinue)
        => ShowChoicePanel("裂隙记录点", "本层记录点已保存：撤离带走奖励，或继续深入", "撤离结算", "继续深入", onEvacuate, onContinue);

    void ShowChoicePanel(string titleText, string tipText, string leftLabel, string rightLabel, Action onLeft, Action onRight)
    {
        if (_chapterChoicePanel != null)
            Destroy(_chapterChoicePanel);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[UIManager] 无 Canvas，直接执行左侧回调");
            onLeft?.Invoke();
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

        var title = CreateUiText(panel.transform, "Title", titleText, 36, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -48f);
        trt.sizeDelta = new Vector2(480f, 50f);

        var tip = CreateUiText(panel.transform, "Tip", tipText, 22, TextAnchor.MiddleCenter);
        tip.color = new Color(0.85f, 0.8f, 0.7f);
        var tipRt = tip.rectTransform;
        tipRt.anchorMin = new Vector2(0.5f, 0.5f);
        tipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tipRt.anchoredPosition = new Vector2(0f, 20f);
        tipRt.sizeDelta = new Vector2(460f, 40f);

        CreateChoiceButton(panel.transform, leftLabel, new Vector2(-120f, -70f), () =>
        {
            Destroy(_chapterChoicePanel);
            _chapterChoicePanel = null;
            onLeft?.Invoke();
        });
        CreateChoiceButton(panel.transform, rightLabel, new Vector2(120f, -70f), () =>
        {
            Destroy(_chapterChoicePanel);
            _chapterChoicePanel = null;
            onRight?.Invoke();
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

    // ---------------------------------------------------------------------
    // 稀有·暮火之杖领取确认窗（2026-10-20 主人拍板）：单按钮告知型弹窗。
    // 领取动作已在 AdventureLogMileage.ClaimLevel 内完成（防弹窗期间杀进程丢杖），
    // 本窗只做「获得告知 + 替换法师初始武器说明 + 确认」；不动 timeScale（城镇场景）。
    // ---------------------------------------------------------------------
    GameObject _twilightConfirmPanel;
    static Sprite _twilightRoundedSprite;

    public void ShowTwilightStaffConfirm()
    {
        if (_twilightConfirmPanel != null)
            Destroy(_twilightConfirmPanel);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[UIManager] 无 Canvas，暮火之杖确认窗降级为 Toast");
            ShowToast($"获得{SpecialWeapons.InstanceName}：法师初始武器已替换");
            return;
        }

        // 嵌套 Canvas 抬到最上层，盖过冒险日志 / 酒馆等一切城镇弹窗
        var panelGo = new GameObject("TwilightStaffConfirm", typeof(RectTransform), typeof(Canvas));
        panelGo.transform.SetParent(canvas.transform, false);
        var nested = panelGo.GetComponent<Canvas>();
        nested.overrideSorting = true;
        nested.sortingOrder = 3000;
        _twilightConfirmPanel = panelGo;
        StretchFull(panelGo.GetComponent<RectTransform>());

        var dim = CreateUiImage(panelGo.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        StretchFull(dim.rectTransform);

        var panel = CreateUiImage(panelGo.transform, "Panel", new Color(0.12f, 0.1f, 0.08f, 0.96f));
        panel.sprite = TwilightRoundedSprite();
        panel.type = Image.Type.Sliced;
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 300f);
        prt.anchoredPosition = Vector2.zero;

        var title = CreateUiText(panel.transform, "Title", "恭喜！获得法师限定初始武器", 32, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -50f);
        trt.sizeDelta = new Vector2(520f, 50f);

        var trait = CreateUiText(panel.transform, "Trait",
            $"「{SpecialWeapons.InstanceName}」 随机特性："
            + (string.IsNullOrEmpty(SpecialWeapons.LastGrantedTraitText) ? "见装备详情" : SpecialWeapons.LastGrantedTraitText),
            24, TextAnchor.MiddleCenter);
        trait.color = new Color(1f, 0.82f, 0.45f);
        var grt = trait.rectTransform;
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0f, 28f);
        grt.sizeDelta = new Vector2(520f, 36f);

        var desc = CreateUiText(panel.transform, "Desc",
            "实力大增，是时候深入裂隙大显身手了！\n此后法师每局开局自动装备它，原初始法杖不再出现。", 21, TextAnchor.MiddleCenter);
        desc.color = new Color(0.85f, 0.8f, 0.7f);
        var drt = desc.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -26f);
        drt.sizeDelta = new Vector2(500f, 70f);

        // 确认按钮：黑底白字圆角（主人指定风格，同剧情 AUTO 按钮）
        var btnImg = CreateUiImage(panel.transform, "BtnConfirm", new Color(0f, 0f, 0f, 0.88f));
        btnImg.sprite = TwilightRoundedSprite();
        btnImg.type = Image.Type.Sliced;
        var brt = btnImg.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, -104f);
        brt.sizeDelta = new Vector2(220f, 56f);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.transition = Selectable.Transition.ColorTint;
        btn.onClick.AddListener(() =>
        {
            Destroy(_twilightConfirmPanel);
            _twilightConfirmPanel = null;
            ShowToast("法师初始武器已替换：稀有·暮火之杖");
        });
        var btnLabel = CreateUiText(btnImg.transform, "Label", "确认", 26, TextAnchor.MiddleCenter);
        StretchFull(btnLabel.rectTransform);
    }

    /// <summary>程序生成圆角矩形（白 alpha 蒙版 + 九宫格 border），确认窗面板与按钮共用。</summary>
    static Sprite TwilightRoundedSprite()
    {
        if (_twilightRoundedSprite != null) return _twilightRoundedSprite;
        const int size = 64;
        const float radius = 18f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x);
                float dy = Mathf.Min(y, size - 1 - y);
                float a;
                if (dx >= radius || dy >= radius)
                    a = 1f;
                else
                {
                    float ex = radius - dx;
                    float ey = radius - dy;
                    a = Mathf.Clamp01(radius - Mathf.Sqrt(ex * ex + ey * ey));
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _twilightRoundedSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return _twilightRoundedSprite;
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
