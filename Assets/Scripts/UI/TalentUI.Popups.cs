using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：运行时弹窗（流派二选一、解锁/升级费用确认、洗点二次确认）
public partial class TalentUI
{

    // ===== 【2026-10-18 主人拍板】流派二选一（物理/魔法专精合并行）点击流 + 弹窗 =====

    /// <summary>右列节点首个选项的展示名（物理专精/魔法专精）；无选项回退节点名。</summary>
    static string OptionDisplayName(TalentDefs.TalentRightNode node)
    {
        if (node != null && node.options != null && node.options.Length > 0
            && !string.IsNullOrEmpty(node.options[0].name))
            return node.options[0].name;
        return RightNodeName(node);
    }

    /// <summary>
    /// 合并行整行点击：未选 → 二选一弹窗（软引导+费用）；已选 → 升级费用确认；门槛未到 → toast 说法。
    /// </summary>
    void OnClickMutexPairRow(RightNodeView v)
    {
        if (v == null || v.node == null || v.node2 == null) return;
        var talents = GetTalents();
        int lv1 = TalentDefs.GetRightNodeLevel(talents, v.node.id);
        int lv2 = TalentDefs.GetRightNodeLevel(talents, v.node2.id);

        if (lv1 <= 0 && lv2 <= 0)
        {
            if (TalentDefs.LeftUnlockedCount(talents) < v.node.unlockLeftIndex)
            {
                GlobalToastUI.Show($"左列开到第{v.node.unlockLeftIndex}个后开启", true);
                return;
            }
            OpenMutexChoice(v);
            return;
        }

        var picked = lv1 > 0 ? v.node : v.node2;
        int pickedLv = lv1 > 0 ? lv1 : lv2;
        if (!TalentSystem.CanUpgradeRightNode(picked.id, out string reason))
        {
            GlobalToastUI.Show(reason, true);
            return;
        }
        OpenCostConfirm(picked, pickedLv);
    }

    /// <summary>
    /// 合并行选项点击：点已选侧 → 升级费用确认；点未选侧（已有主流派）→ 互斥软引导；
    /// 两侧都没选 → 二选一弹窗。
    /// </summary>
    void OnClickMutexOption(RightNodeView v, int opt0)
    {
        if (v == null || v.node == null || v.node2 == null) return;
        var n = opt0 == 0 ? v.node : v.node2;
        var other = opt0 == 0 ? v.node2 : v.node;
        var talents = GetTalents();
        int lv = TalentDefs.GetRightNodeLevel(talents, n.id);
        int lvOther = TalentDefs.GetRightNodeLevel(talents, other.id);

        if (lv > 0)
        {
            // 点的是已选流派：升级费用确认
            if (!TalentSystem.CanUpgradeRightNode(n.id, out string upReason))
            {
                GlobalToastUI.Show(upReason, true);
                return;
            }
            OpenCostConfirm(n, lv);
            return;
        }
        if (lvOther > 0)
        {
            // 已有主流派：点另一侧 = 互斥，说人话引导（不是干巴巴报错）
            GlobalToastUI.Show("流派二选一：已选「" + OptionDisplayName(other) + "」，如需更换可洗点后重选", true);
            return;
        }
        // 两侧都没选 → 二选一弹窗
        if (TalentDefs.LeftUnlockedCount(talents) < v.node.unlockLeftIndex)
        {
            GlobalToastUI.Show($"左列开到第{v.node.unlockLeftIndex}个后开启", true);
            return;
        }
        OpenMutexChoice(v);
    }

    /// <summary>
    /// 打开流派二选一弹窗：软引导（二选一、只提升所选流派攻击加成、不影响职业使用）
    /// + 两个选项卡 + 费用行。确认走 TryUnlockRightNode（AnnounceRightGain 自带成功 toast）。
    /// </summary>
    void OpenMutexChoice(RightNodeView v)
    {
        EnsureMutexChoicePopup();
        if (_mutexChoicePopup == null) return;
        _pendingMutexA = v.node;
        _pendingMutexB = v.node2;
        SelectMutexOption(0);   // 默认选物理
        _mutexChoicePopup.SetActive(true);
        _mutexChoicePopup.transform.SetAsLastSibling();
    }

    /// <summary>懒建二选一弹窗。独立 Canvas 层级 TownPopup(900)，高于天赋面板（TownVeil 200）。</summary>
    void EnsureMutexChoicePopup()
    {
        // 防重影：与洗点弹窗同口径，先探测旧节点销毁再重建（域重载后字段归零但节点可能残留）
        var existing = transform.Find("MutexChoicePopup");
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
                Destroy(existing.GetChild(i).gameObject);
            Destroy(existing.gameObject);
            _mutexChoicePopup = null;
        }
        if (_mutexChoicePopup != null) return;

        var root = CreateRect(transform, "MutexChoicePopup");
        Stretch(root.GetComponent<RectTransform>());

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var box = CreateImage(root.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(620f, 540f));

        var title = CreateText(box.transform, "Title", "流派·专精", 30, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -26f), new Vector2(520f, 44f));

        // 软引导（主人拍板：说清「二选一不是封死别的职业，只是提升某一种攻击的加成」）
        var desc = CreateText(box.transform, "Desc",
            "第一次开启流派专精：物理与魔法二选一。\n" +
            "· 只提升所选流派攻击的伤害加成，不影响你使用其它职业；\n" +
            "· 另一流派将锁定，想更换可洗点后重选。",
            20, new Color(0.2f, 0.12f, 0.08f));
        desc.alignment = TextAnchor.UpperLeft;
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 158f), new Vector2(560f, 92f));

        _mutexOptCards = new GameObject[2];
        _mutexOptLabels = new Text[2];
        _mutexOptIcons = new Image[2];
        for (int o = 0; o < 2; o++)
        {
            var card = CreateImage(box.transform, "Opt_" + o,
                o == 0 ? new Color(0.80f, 0.86f, 0.97f, 1f) : new Color(0.85f, 0.78f, 0.97f, 1f));
            SetAnchored(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(o == 0 ? -138f : 138f, 22f), new Vector2(240f, 120f));
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card;
            int opt0 = o;
            cardBtn.onClick.AddListener(() => SelectMutexOption(opt0));
            _mutexOptCards[o] = card.gameObject;

            var icon = CreateImage(card.transform, "Icon", Color.white);
            SetAnchored(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -14f), new Vector2(60f, 60f));
            icon.raycastTarget = false;
            _mutexOptIcons[o] = icon;

            var label = CreateText(card.transform, "Label", "", 22, new Color(0.22f, 0.13f, 0.08f));
            SetAnchored(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f), new Vector2(200f, 32f));
            _mutexOptLabels[o] = label;
        }

        var cost = CreateText(box.transform, "CostLine", "", 22, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -66f), new Vector2(560f, 36f));
        _mutexChoiceCost = cost;

        var cancel = CreateImage(box.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, 28f), new Vector2(200f, 56f));
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancel;
        cancelBtn.onClick.AddListener(CloseMutexChoice);
        var cancelTxt = CreateText(cancel.transform, "Label", "取消", 26, Color.white);
        Stretch(cancelTxt.rectTransform);

        var ok = CreateImage(box.transform, "Confirm", new Color(0.55f, 0.35f, 0.2f, 1f));
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(110f, 28f), new Vector2(200f, 56f));
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(ConfirmMutexChoice);
        var okTxt = CreateText(ok.transform, "Label", "确定解锁", 26, Color.white);
        Stretch(okTxt.rectTransform);

        _mutexChoicePopup = root;
        root.SetActive(false);
    }

    /// <summary>二选一弹窗：选卡（0=物理 / 1=魔法）+ 刷费用行（持有不足追加提醒）。</summary>
    void SelectMutexOption(int opt0)
    {
        if (_pendingMutexA == null || _pendingMutexB == null) return;
        if (opt0 != 0 && opt0 != 1) return;
        _pendingMutexSelected = opt0;
        var node = opt0 == 0 ? _pendingMutexA : _pendingMutexB;

        // 卡内容：图标 + 选项名（首次打开填充）
        string optName = OptionDisplayName(node);
        if (_mutexOptLabels != null && opt0 < _mutexOptLabels.Length && _mutexOptLabels[opt0] != null)
            _mutexOptLabels[opt0].text = optName;
        if (_mutexOptIcons != null && opt0 < _mutexOptIcons.Length && _mutexOptIcons[opt0] != null)
        {
            var sp = TalentIcons.GetTalent(optName);
            if (sp != null) ApplySprite(_mutexOptIcons[opt0], sp, true);
        }

        if (_mutexOptCards != null)
            for (int i = 0; i < _mutexOptCards.Length; i++)
                if (_mutexOptCards[i] != null)
                    SetGraphicAlpha(_mutexOptCards[i], i == opt0 ? 1f : 0.55f);

        int cost = node.costByLevel != null && node.costByLevel.Length > 0 ? node.costByLevel[0] : 0;
        int have = SaveSystem.Instance?.Data?.talentPoints ?? 0;
        if (_mutexChoiceCost != null)
            _mutexChoiceCost.text = "消耗天赋石：" + cost
                + (have >= cost ? "（持有 " + have + "）" : "（持有 " + have + "，天赋石不足）");
    }

    void ConfirmMutexChoice()
    {
        if (_pendingMutexA == null || _pendingMutexB == null) return;
        if (_pendingMutexSelected != 0 && _pendingMutexSelected != 1) return;
        var node = _pendingMutexSelected == 0 ? _pendingMutexA : _pendingMutexB;
        if (!TalentSystem.TryUnlockRightNode(node.id, 0, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            GlobalToastUI.Show(reason, true);
            return;
        }
        CloseMutexChoice();
        RefreshAll();
    }

    void CloseMutexChoice()
    {
        if (_mutexChoicePopup != null) _mutexChoicePopup.SetActive(false);
        _pendingMutexA = null;
        _pendingMutexB = null;
        _pendingMutexSelected = -1;
    }

    // ===== 【2026-10-20 修·玩家报「职业专精点不了」】R_JOB 6选1 职业弹窗（运行时建树）=====
    // 根因：手工版预制体的 ChoicePopup 只有 1 张 Choice_0 卡（视觉宽 415，Box 480 根本排不下 6 张），
    // 且没有「确定」按钮——旧代码克隆布局让卡重叠+飞出 Box，玩家点了没法选、选了没法确认。
    // 口径对齐 EnsureMutexChoicePopup：TownPopup(900)、软引导 + 选项卡 + 费用行 + 取消/确定。
    // 6 卡 2 列 × 3 行，图标/名字走 TalentIcons.GetTalent（剑盾卫士→剑盾专精 等既有映射）。

    /// <summary>懒建职业 6选1 弹窗。独立 Canvas 层级 TownPopup(900)，与二选一弹窗同层。</summary>
    void EnsureJobChoicePopup()
    {
        // 防重影：与洗点/二选一弹窗同口径，先探测旧节点销毁再重建
        var existing = transform.Find("JobChoicePopup");
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
                Destroy(existing.GetChild(i).gameObject);
            Destroy(existing.gameObject);
            _jobChoicePopup = null;
        }
        if (_jobChoicePopup != null) return;

        var root = CreateRect(transform, "JobChoicePopup");
        Stretch(root.GetComponent<RectTransform>());

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var box = CreateImage(root.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(620f, 680f));

        var title = CreateText(box.transform, "Title", "职业专精", 30, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -26f), new Vector2(520f, 44f));

        // 软引导：说清「6选1 只是给所选职业加伤，不锁其它职业」
        var desc = CreateText(box.transform, "Desc",
            "选择一名职业作为专属专精（6选1）。\n" +
            "· 所选职业造成的伤害 +4%×专精等级，满级 +20%；\n" +
            "· 不影响你使用其它职业，想更换可洗点后重选。",
            20, new Color(0.2f, 0.12f, 0.08f));
        desc.alignment = TextAnchor.UpperLeft;
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 216f), new Vector2(560f, 76f));

        // 6 张职业卡：2 列 × 3 行（x=±145，y=122/10/-102），卡 260×96
        const int jobSlotCount = 6;
        _jobOptCards = new GameObject[jobSlotCount];
        _jobOptLabels = new Text[jobSlotCount];
        _jobOptIcons = new Image[jobSlotCount];
        for (int o = 0; o < jobSlotCount; o++)
        {
            var card = CreateImage(box.transform, "JobOpt_" + o, new Color(0.85f, 0.78f, 0.97f, 1f));
            float cx = (o % 2 == 0) ? -145f : 145f;
            float cy = 122f - (o / 2) * 112f;
            SetAnchored(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(cx, cy), new Vector2(260f, 96f));
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card;
            int opt0 = o;
            cardBtn.onClick.AddListener(() => SelectChoicePopupOption(opt0));
            _jobOptCards[o] = card.gameObject;

            var icon = CreateImage(card.transform, "Icon", Color.white);
            SetAnchored(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -34f), new Vector2(52f, 52f));
            icon.raycastTarget = false;
            _jobOptIcons[o] = icon;

            var label = CreateText(card.transform, "Label", "", 22, new Color(0.22f, 0.13f, 0.08f));
            SetAnchored(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(240f, 30f));
            _jobOptLabels[o] = label;
        }

        // 选中职业的效果 + 费用行（选中时刷新）
        var cost = CreateText(box.transform, "CostLine", "", 20, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -192f), new Vector2(560f, 56f));
        _jobChoiceCost = cost;

        var cancel = CreateImage(box.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, -254f), new Vector2(200f, 56f));
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancel;
        cancelBtn.onClick.AddListener(CloseChoicePopup);
        var cancelTxt = CreateText(cancel.transform, "Label", "取消", 26, Color.white);
        Stretch(cancelTxt.rectTransform);

        var ok = CreateImage(box.transform, "Confirm", new Color(0.55f, 0.35f, 0.2f, 1f));
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(110f, -254f), new Vector2(200f, 56f));
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(ConfirmChoicePopup);
        var okTxt = CreateText(ok.transform, "Label", "确定解锁", 26, Color.white);
        Stretch(okTxt.rectTransform);

        _jobChoicePopup = root;
        root.SetActive(false);
    }

    /// <summary>把职业卡内容填进 6选1 弹窗（数量按配表，激活/隐藏多余槽）。</summary>
    void FillJobChoiceCards(TalentDefs.TalentRightNode node)
    {
        var opts = node.options ?? new TalentDefs.TalentRightOption[0];
        if (_jobOptCards == null) return;
        for (int i = 0; i < _jobOptCards.Length; i++)
        {
            bool on = i < opts.Length;
            if (_jobOptCards[i] != null) _jobOptCards[i].SetActive(on);
            if (!on) continue;
            if (_jobOptLabels != null && i < _jobOptLabels.Length && _jobOptLabels[i] != null)
                _jobOptLabels[i].text = opts[i].name;
            if (_jobOptIcons != null && i < _jobOptIcons.Length && _jobOptIcons[i] != null)
            {
                var sp = TalentIcons.GetTalent(opts[i].name);
                if (sp != null) ApplySprite(_jobOptIcons[i], sp, true);
            }
        }
    }

    // ===== 【2026-10-18 主人拍板】解锁/升级费用确认弹窗（运行时建树，不碰 prefab）=====

    /// <summary>
    /// 打开费用确认窗：让玩家看到「效果 + 需要多少天赋石」。
    /// 天赋石不足也照样弹（能看到缺口），确定时校验不过再 toast —— 不再「点一下直接弹不够」。
    /// </summary>
    void OpenCostConfirm(TalentDefs.TalentRightNode node, int fromLevel)
    {
        if (node == null) return;
        EnsureCostConfirmPopup();
        if (_costConfirmPopup == null) return;

        bool unlock = fromLevel <= 0;
        int cost = node.costByLevel != null && fromLevel < node.costByLevel.Length ? node.costByLevel[fromLevel] : 0;
        var data = SaveSystem.Instance?.Data;
        int have = data?.talentPoints ?? 0;
        int chosenJob = TalentDefs.GetRightNodeChosenJob(data != null ? data.talents : null, node.id);
        int effLevel = unlock ? 1 : fromLevel + 1;
        string eff = node.EffectSummary(effLevel, chosenJob);

        if (_costConfirmDesc != null)
        {
            string head = unlock
                ? "解锁【" + node.name + "】"
                : "升级【" + node.name + "】  Lv" + fromLevel + " → Lv" + effLevel;
            _costConfirmDesc.text = head
                + "\n\n效果：" + eff
                + "\n\n消耗天赋石：" + cost
                + (have >= cost ? "（持有 " + have + "）" : "（持有 " + have + "，天赋石不足）");
        }
        if (_costConfirmOkLabel != null)
            _costConfirmOkLabel.text = unlock ? "确定解锁" : "确定升级";

        _costConfirmAction = () =>
        {
            string reason;
            bool ok = unlock
                ? TalentSystem.TryUnlockRightNode(node.id, 0, out reason)
                : TalentSystem.TryUpgradeRightNode(node.id, out reason);
            if (!ok)
            {
                Debug.Log("[TalentUI] " + reason);
                GlobalToastUI.Show(reason, true);
                CloseCostConfirm();
                return;
            }
            CloseCostConfirm();
            RefreshAll();
        };

        _costConfirmPopup.SetActive(true);
        _costConfirmPopup.transform.SetAsLastSibling();
    }

    /// <summary>懒建费用确认弹窗。层级与洗点/二选一弹窗同为 TownPopup(900)。</summary>
    void EnsureCostConfirmPopup()
    {
        // 防重影：与洗点弹窗同口径
        var existing = transform.Find("CostConfirmPopup");
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
                Destroy(existing.GetChild(i).gameObject);
            Destroy(existing.gameObject);
            _costConfirmPopup = null;
        }
        if (_costConfirmPopup != null) return;

        var root = CreateRect(transform, "CostConfirmPopup");
        Stretch(root.GetComponent<RectTransform>());

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var box = CreateImage(root.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(560f, 420f));

        var title = CreateText(box.transform, "Title", "确认消耗", 30, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -24f), new Vector2(480f, 44f));

        var desc = CreateText(box.transform, "Desc", "", 21, new Color(0.2f, 0.12f, 0.08f));
        desc.alignment = TextAnchor.UpperLeft;
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f), new Vector2(500f, 240f));
        _costConfirmDesc = desc;

        var cancel = CreateImage(box.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, 28f), new Vector2(200f, 56f));
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancel;
        cancelBtn.onClick.AddListener(CloseCostConfirm);
        var cancelTxt = CreateText(cancel.transform, "Label", "取消", 26, Color.white);
        Stretch(cancelTxt.rectTransform);

        var ok = CreateImage(box.transform, "Confirm", new Color(0.55f, 0.35f, 0.2f, 1f));
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(110f, 28f), new Vector2(200f, 56f));
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(ConfirmCostPopup);
        var okTxt = CreateText(ok.transform, "Label", "确定解锁", 26, Color.white);
        Stretch(okTxt.rectTransform);
        _costConfirmOkLabel = okTxt;

        _costConfirmPopup = root;
        root.SetActive(false);
    }

    void ConfirmCostPopup()
    {
        var act = _costConfirmAction;
        _costConfirmAction = null;
        act?.Invoke();
    }

    void CloseCostConfirm()
    {
        if (_costConfirmPopup != null) _costConfirmPopup.SetActive(false);
        _costConfirmAction = null;
    }

    // ===== 任务：洗点二次确认弹窗（运行时建树，沿用 CreateImage/CreateText 风格）=====
    /// <summary>打开洗点确认框，并动态填入本次返还预览（口径与 TalentSystem.TryRespecRight 一致）。</summary>
    void OpenResetConfirm()
    {
        EnsureResetConfirmPopup();
        if (_resetConfirmPopup == null) return;

        // 与 TryRespecRight 口径一致：统计右列已投入天赋石，返还 80%（向下取整）
        var talents = GetTalents();
        int totalSpent = 0;
        if (talents != null)
        {
            for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
            {
                var node = TalentDefs.RightNodes[i];
                int lv = TalentDefs.GetRightNodeLevel(talents, node.id);
                if (lv > 0) totalSpent += TalentDefs.RightNodeTotalSpent(node, lv);
            }
        }
        int refund = (int)(totalSpent * 0.8f); // 向下取整，与洗点逻辑一致

        if (_resetConfirmDesc != null)
        {
            _resetConfirmDesc.text =
                "确认要洗点吗？\n\n" +
                "· 将清空全部右侧天赋（含已选职业），可重新分配。\n" +
                "· 返还已投入天赋石的 80%（向下取整）：本次预计返还 " + refund +
                    "（已投入 " + totalSpent + "）。\n" +
                "· 左侧金币天赋不参与洗点，已花费的金币不返还。";
        }

        _resetConfirmPopup.SetActive(true);
        _resetConfirmPopup.transform.SetAsLastSibling();
    }

    void CloseResetConfirm()
    {
        if (_resetConfirmPopup != null) _resetConfirmPopup.SetActive(false);
    }

    /// <summary>点「确定」才真正执行洗点（即原 OnClickReset 的洗点动作，逻辑未改）。</summary>
    void ConfirmReset()
    {
        CloseResetConfirm();
        onResetRequested?.Invoke();
        RefreshAll();
    }

    /// <summary>懒建洗点确认弹窗。独立 Canvas，层级 TownPopup(900)：高于天赋面板 TownVeil(200)，
    /// 低于最顶层弹窗（Loading/Toast/FullscreenFx）。</summary>
    void EnsureResetConfirmPopup()
    {
        // 防两套叠加：域重载 / 重新实例化后内存字段 _resetConfirmPopup 归零，
        // 但 prefab 重建时旧 ResetConfirmPopup 节点可能已残留；只判字段会重影。
        // 先按名字探测，存在则倒序销毁旧节点再重建，并把字段置空避免悬空引用。
        var existing = transform.Find("ResetConfirmPopup");
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
                Destroy(existing.GetChild(i).gameObject);
            Destroy(existing.gameObject);
            _resetConfirmPopup = null;
        }

        if (_resetConfirmPopup != null) return;

        var root = CreateRect(transform, "ResetConfirmPopup");
        Stretch(root.GetComponent<RectTransform>());

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var box = CreateImage(root.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(560f, 380f));

        var title = CreateText(box.transform, "Title", "洗点确认", 30, new Color(0.25f, 0.15f, 0.1f));
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -24f), new Vector2(480f, 44f));

        var desc = CreateText(box.transform, "Desc", "", 20, new Color(0.2f, 0.12f, 0.08f));
        desc.alignment = TextAnchor.UpperLeft;
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 10f), new Vector2(500f, 220f));
        _resetConfirmDesc = desc;

        var cancel = CreateImage(box.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-110f, 28f), new Vector2(200f, 56f));
        cancel.raycastTarget = true;
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancel;
        cancelBtn.onClick.AddListener(CloseResetConfirm);
        var cancelTxt = CreateText(cancel.transform, "Label", "取消", 26, Color.white);
        Stretch(cancelTxt.rectTransform);

        var ok = CreateImage(box.transform, "Confirm", new Color(0.55f, 0.35f, 0.2f, 1f));
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(110f, 28f), new Vector2(200f, 56f));
        ok.raycastTarget = true;
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(ConfirmReset);
        var okTxt = CreateText(ok.transform, "Label", "确定洗点", 26, Color.white);
        Stretch(okTxt.rectTransform);

        _resetConfirmPopup = root;
        root.SetActive(false);
    }
}
