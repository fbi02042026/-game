using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：层级绑定与查找（AutoBindFromHierarchy、货币绑定、Find 系列工具）
public partial class TalentUI
{

    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    public void AutoBindFromHierarchy()
    {
        panelImage = FindImg("Panel");
        closeButton = transform.Find("Panel/TitleBar/CloseButton")?.GetComponent<Button>()
                      ?? transform.Find("Panel/CloseButton")?.GetComponent<Button>();
        titleText = FindTxt("Panel/TitleBar/TitleText");
        BindCurrencyTexts();
        stonePlusButton = transform.Find("Panel/ResourceRow/StonePlus")?.GetComponent<Button>()
                          ?? transform.Find("Panel/ResourceRow/GoldPanel/PlusButton")?.GetComponent<Button>();

        leftScroll = transform.Find("Panel/Columns/LeftColumn/LeftScroll")?.GetComponent<ScrollRect>();
        leftContent = transform.Find("Panel/Columns/LeftColumn/LeftScroll/Viewport/Content") as RectTransform;
        leftTipText = FindTxt("Panel/Columns/LeftColumn/LeftTip");
        leftNodeTemplate = transform.Find("LeftNodeTemplate")?.gameObject;

        rightScroll = transform.Find("Panel/Columns/RightColumn/RightScroll")?.GetComponent<ScrollRect>();
        rightContent = transform.Find("Panel/Columns/RightColumn/RightScroll/Viewport/Content") as RectTransform;
        rightTipText = FindTxt("Panel/Columns/RightColumn/RightTip");
        rightCostValueText = FindTxt("Panel/Columns/RightColumn/RightCostText")
                             ?? FindTxt("Panel/Columns/RightColumn/StoneBar/RightCostText");
        rightRowTemplate = transform.Find("RightRowTemplate")?.gameObject;
        rightExtraRowTemplate = transform.Find("RightRowTemplate (1)")?.gameObject;

        // 【2026-10-26 主人全文字版】底栏属性格以节点名为准：
        // 物攻=SumAttack 魔攻=SumAttack1 血量=SumHp 物防=SumDef 魔防=SumDef1 暴击=SumCrit 攻速=SumAtkSpd
        sumAttackText = FindTxt("Panel/Footer/SumAttack");
        sumMagicText = FindTxt("Panel/Footer/SumAttack1")
                       ?? FindTxt("Panel/Footer/SumMagic");   // 旧版节点名兜底
        sumHpText = FindTxt("Panel/Footer/SumHp");
        sumDefText = FindTxt("Panel/Footer/SumDef");
        sumMagicDefText = FindTxt("Panel/Footer/SumDef1");
        sumCritText = FindTxt("Panel/Footer/SumCrit");
        sumAtkSpdText = FindTxt("Panel/Footer/SumAtkSpd");
        resetButton = transform.Find("Panel/Footer/ResetButton")?.GetComponent<Button>();
        resetButtonGray = transform.Find("Panel/Footer/ResetButton灰")?.GetComponent<Button>()
                          ?? transform.Find("Panel/Footer/ResetButtonGray")?.GetComponent<Button>();
        // 【2026-10-27 主人新增节点】Footer/Icon/数量：遗忘药水持有数（Text，预制体静态 "X0"），RefreshResetButton 写值
        resetPotionCountText = FindTxt("Panel/Footer/Icon/数量");
        if (resetPotionCountText == null)
            Debug.LogError("[TalentUI] 找不到遗忘药水计数节点 Panel/Footer/Icon/数量，重置按钮旁的持有数不显示");

        choicePopup = transform.Find("ChoicePopup")?.gameObject;
        choiceTitleText = FindTxt("ChoicePopup/Title")
                          ?? FindTxt("ChoicePopup/标头/Title");
        choiceDescText = FindTxt("ChoicePopup/Choice_0/Label");
        choiceCancelButton = transform.Find("ChoicePopup/Cancel")?.GetComponent<Button>();
        choiceConfirmButton = transform.Find("ChoicePopup/确定")?.GetComponent<Button>()
                              ?? transform.Find("ChoicePopup/Confirm")?.GetComponent<Button>();
        choiceOptionTemplate = transform.Find("ChoicePopup/Choice_0")?.gameObject;
        choiceButtons = new Button[3];
        choiceLabels = new Text[3];
        choiceIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            choiceButtons[i] = transform.Find($"ChoicePopup/Choice_{i}")?.GetComponent<Button>();
            if (choiceButtons[i] != null)
            {
                choiceLabels[i] = choiceButtons[i].GetComponentInChildren<Text>(true);
                var iconT = choiceButtons[i].transform.Find("Icon") ?? choiceButtons[i].transform.Find("icon");
                choiceIcons[i] = iconT != null ? iconT.GetComponent<Image>() : null;
            }
        }
    }

    /// <summary>
    /// panelImage 已序列化时 Awake 会跳过 AutoBind，导致手做 GoldPanel 文案未挂上。
    /// </summary>
    void EnsureCurrencyBindings()
    {
        BindCurrencyTexts();
        WireGoldPlusIfNeeded();
    }

    /// <summary>
    /// 【2026-10-26 主人全文字版补绑】底栏属性格按预制体节点名纠偏。
    /// 主人手工改版后的现状：sumDefText 槽位被拖到了新加的魔防 SumDef1 上（原物防 SumDef 断绑）、
    /// sumMagicText 为空，而 panelImage 已序列化导致 AutoBindFromHierarchy 不跑——
    /// 即使跑也只认旧节点名 SumMagic，不认识 SumAttack1/SumDef1。这里统一纠偏：
    /// 只修「为空」和「指错节点」的字段，Inspector 里已拖对的不动。
    /// </summary>
    void EnsureFooterBindings()
    {
        if (sumMagicText == null)
            sumMagicText = FindTxt("Panel/Footer/SumAttack1");
        if (sumMagicDefText == null)
            sumMagicDefText = FindTxt("Panel/Footer/SumDef1");
        // sumDefText 历史上被拖到魔防节点上：指向的节点名不是 SumDef 时按名重绑物防
        if (sumDefText == null || !sumDefText.transform.name.Equals("SumDef"))
            sumDefText = FindTxt("Panel/Footer/SumDef");
    }

    void BindCurrencyTexts()
    {
        // 手做：ResourceRow/GoldText 常为隐藏白模；真金显示在 GoldPanel/GoldText
        if (goldText == null || !goldText.gameObject.activeInHierarchy)
        {
            goldText = FindTxt("Panel/ResourceRow/GoldPanel/GoldText")
                       ?? FindTxt("Panel/ResourceRow/GoldText")
                       ?? goldText;
        }
        if (stoneText == null || !stoneText.gameObject.activeInHierarchy)
        {
            stoneText = FindDeepTextByParentName("天赋石Panel", "GoldText")
                        ?? FindTxt("Panel/ResourceRow/StoneText")
                        ?? stoneText;
        }
        if (leftCostText == null)
        {
            leftCostText = FindTxt("Panel/Columns/LeftColumn/LeftCostText")
                           ?? FindTxt("Panel/Columns/LeftColumn/GoldBar/LeftCostText")
                           ?? FindTxt("Panel/ResourceRow/GoldPanel/LeftCostText");
        }
        rightCostValueText = rightCostValueText
                             ?? FindTxt("Panel/Columns/RightColumn/RightCostText")
                             ?? FindTxt("Panel/Columns/RightColumn/StoneBar/RightCostText");
    }

    void WireGoldPlusIfNeeded()
    {
        var plus = transform.Find("Panel/ResourceRow/GoldPanel/PlusButton")?.GetComponent<Button>();
        if (plus == null) return;
        plus.onClick.RemoveAllListeners();
        plus.onClick.AddListener(OnClickGoldPlusUpgrade);
    }

    void OnClickGoldPlusUpgrade()
    {
        var talents = GetTalents();
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (unlocked >= TalentDefs.Left.Length) return;
        OnClickLeft(unlocked);
    }

    Image FindImg(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    Text FindDeepTextByParentName(string parentName, string childName)
    {
        if (string.IsNullOrEmpty(parentName)) return null;
        var parent = FindDeepNamed(transform, parentName);
        if (parent == null) return null;
        var child = parent.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    static Transform FindDeepNamed(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
            var nested = FindDeepNamed(c, name);
            if (nested != null) return nested;
        }
        return null;
    }

    static Image FindDeepImage(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name)
            {
                var img = c.GetComponent<Image>();
                if (img != null) return img;
            }
            var nested = FindDeepImage(c, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
