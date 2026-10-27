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

    /// <summary>
    /// 【2026-10-20 主人拍板「把这个界面正式化 并生成预制体」】二选一弹窗改走预制体。
    /// 路径与 MutexChoicePopupPrefabBuilder.PrefabPath **必须同值**（改一边忘了另一边 = 运行时加载不到）。
    /// </summary>
    internal const string MutexChoicePopupPrefabPath = "Prefabs/Talent/MutexChoicePopup";

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
        // 【2026-10-20 修】把玩家点的那一侧带进弹窗做预选（0=物理 / 1=魔法），
        // 不再让弹窗一律默认物理 —— 否则玩家点魔法侧、确认时却开成物理。
        OpenMutexChoice(v, opt0);
    }

    /// <summary>
    /// 打开流派二选一弹窗：软引导（二选一、只提升所选流派攻击加成、不影响职业使用）
    /// + 两个选项卡 + 费用行。确认走 TryUnlockRightNode（AnnounceRightGain 自带成功 toast）。
    /// 【2026-10-20 修·玩家报「点的是法术专精结果开了物理专精」】新增 preferOpt0：
    /// 合并行里玩家点了哪一侧，弹窗就预选哪一侧（0=物理 / 1=魔法）。
    /// 旧写法写死 SelectMutexOption(0)，玩家点「魔法专精」那格弹出的却是物理预选，
    /// 玩家直接按「确定解锁」就开成了物理 —— 玩家的预选被吞掉，正是本次误开的根因。
    /// 整行点击（没点具体侧）仍默认预选物理。
    /// </summary>
    void OpenMutexChoice(RightNodeView v, int preferOpt0 = 0)
    {
        EnsureMutexChoicePopup();
        if (_mutexChoicePopup == null) return;
        _pendingMutexA = v.node;
        _pendingMutexB = v.node2;
        // 预选夹到合法区间：只认 0/1，越界回落物理（与选项卡 0/1 一一对应，不多入口）
        if (preferOpt0 != 0 && preferOpt0 != 1) preferOpt0 = 0;
        SelectMutexOption(preferOpt0);
        _mutexChoicePopup.SetActive(true);
        _mutexChoicePopup.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 懒建二选一弹窗。【2026-10-20 主人拍板「把这个界面正式化 并生成预制体」；
    /// 追问「不能用通用的弹窗吗」→ 套默认弹窗（EvacuateConfirmPopup）同一套皮：
    /// 同款底框/标题样式/按钮，仅内容区多两张选项卡——通用确认框（StaminaConfirmPopup）
    /// 只有标题+正文+双按钮，塞不下可选卡，无法纯复用。】
    /// 外观排版真值在预制体 Resources/Prefabs/Talent/MutexChoicePopup
    /// （由 Assets/Editor/MutexChoicePopupPrefabBuilder.cs 单点生成）；
    /// 运行时只负责：实例化 → 套 UICanvasSetup 规范 → 按节点名绑定 → 挂回调。
    /// 旧的运行时纯色块建树实现已删除（铁律 15：旧实现能删就删，不留开关）。
    /// </summary>
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

        var prefab = Resources.Load<GameObject>(MutexChoicePopupPrefabPath);
        if (prefab == null)
        {
            // 铁律 14：不静默跳过——没预制体就没得谈，报错让缺口现形
            Debug.LogError("[TalentUI] 找不到预制体 Resources/" + MutexChoicePopupPrefabPath +
                           "，请先在团结菜单跑 Tools → UI → 生成流派专精弹窗预制体");
            return;
        }

        var root = Instantiate(prefab, transform);
        root.name = "MutexChoicePopup";
        // 🔴 不用 `??`（Unity 假 null 不走 ?? 判空，MissingComponentException 雷同生成器 2026-10-20）
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();   // 铁律 10：每个 Canvas 必补
        Stretch(root.GetComponent<RectTransform>());

        // ---- 按节点名绑定（路径与预制体层级一一对应）----
        var box = root.transform.Find("Box");
        if (box == null)
        {
            Debug.LogError("[TalentUI] MutexChoicePopup 预制体缺少 Box 节点，绑定失败");
            return;
        }

        _mutexOptCards = new GameObject[2];
        _mutexOptLabels = new Text[2];
        _mutexOptIcons = new Image[2];
        for (int o = 0; o < 2; o++)
        {
            var card = box.Find("Opt_" + o);
            if (card == null)
            {
                Debug.LogError($"[TalentUI] MutexChoicePopup 预制体缺少 Opt_{o} 节点，绑定失败");
                return;
            }
            _mutexOptCards[o] = card.gameObject;
            _mutexOptIcons[o] = card.Find("Icon")?.GetComponent<Image>();
            _mutexOptLabels[o] = card.Find("Label")?.GetComponent<Text>();

            var cardBtn = card.GetComponent<Button>();
            if (cardBtn != null)
            {
                int opt0 = o;
                cardBtn.onClick.RemoveAllListeners();
                cardBtn.onClick.AddListener(() => SelectMutexOption(opt0));
            }
        }

        _mutexChoiceCost = box.Find("CostLine")?.GetComponent<Text>();

        var cancelBtn = box.Find("Cancel")?.GetComponent<Button>();
        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(CloseMutexChoice);
        }
        var okBtn = box.Find("Confirm")?.GetComponent<Button>();
        if (okBtn != null)
        {
            okBtn.onClick.RemoveAllListeners();
            okBtn.onClick.AddListener(ConfirmMutexChoice);
        }

        _mutexChoicePopup = root;
        root.SetActive(false);
    }

    /// <summary>
    /// 二选一弹窗：选卡（0=物理 / 1=魔法）+ 刷费用行（持有不足追加提醒）。
    /// 【2026-10-20 修】两张卡的名称/图标**每次都全填**（旧写法只填被选那张，
    /// 另一张开弹窗时是空白的，玩家看不到自己要选的是什么）；高亮只标被选卡。
    /// </summary>
    void SelectMutexOption(int opt0)
    {
        if (_pendingMutexA == null || _pendingMutexB == null) return;
        if (opt0 != 0 && opt0 != 1) return;
        _pendingMutexSelected = opt0;
        var node = opt0 == 0 ? _pendingMutexA : _pendingMutexB;

        // 卡内容：图标 + 选项名（两张卡都填，玩家打开弹窗就能看清两个选项各是什么）
        var nodes = new[] { _pendingMutexA, _pendingMutexB };
        for (int i = 0; i < 2; i++)
        {
            string optName = OptionDisplayName(nodes[i]);
            if (_mutexOptLabels != null && i < _mutexOptLabels.Length && _mutexOptLabels[i] != null)
                _mutexOptLabels[i].text = optName;
            if (_mutexOptIcons != null && i < _mutexOptIcons.Length && _mutexOptIcons[i] != null)
            {
                var sp = TalentIcons.GetTalent(optName);
                if (sp != null) ApplySprite(_mutexOptIcons[i], sp, true);
            }
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

    // ===== 【2026-10-18 主人拍板】解锁/升级费用确认（2026-10-21 起改走通用弹出 StaminaConfirmPopup）=====

    /// <summary>
    /// 打开费用确认窗（通用弹出）：让玩家看到「效果 + 需要多少天赋石」。
    /// 天赋石不足也照样弹（能看到缺口），确定时校验不过再 toast —— 不再「点一下直接弹不够」。
    /// </summary>
    void OpenCostConfirm(TalentDefs.TalentRightNode node, int fromLevel)
    {
        if (node == null) return;

        bool unlock = fromLevel <= 0;
        int cost = node.costByLevel != null && fromLevel < node.costByLevel.Length ? node.costByLevel[fromLevel] : 0;
        var data = SaveSystem.Instance?.Data;
        int have = data?.talentPoints ?? 0;
        int chosenJob = TalentDefs.GetRightNodeChosenJob(data != null ? data.talents : null, node.id);
        int effLevel = unlock ? 1 : fromLevel + 1;
        string eff = node.EffectSummary(effLevel, chosenJob);

        string head = unlock
            ? "解锁【" + node.name + "】"
            : "升级【" + node.name + "】  Lv" + fromLevel + " → Lv" + effLevel;
        string body = head
            + "\n效果：" + eff
            + "\n消耗天赋石：" + cost
            + (have >= cost ? "（持有 " + have + "）" : "（持有 " + have + "，天赋石不足）");

        StaminaConfirmPopup.Show("确认消耗", body, unlock ? "确定解锁" : "确定升级", () =>
        {
            string reason;
            bool ok = unlock
                ? TalentSystem.TryUnlockRightNode(node.id, 0, out reason)
                : TalentSystem.TryUpgradeRightNode(node.id, out reason);
            if (!ok)
            {
                Debug.Log("[TalentUI] " + reason);
                GlobalToastUI.Show(reason, true);
                return;
            }
            RefreshAll();
        });
    }

    void CloseCostConfirm()
    {
        StaminaConfirmPopup.Close();
    }

    // ===== 任务：洗点确认（2026-10-21 改走通用弹出 StaminaConfirmPopup）=====
    /// <summary>打开洗点确认框（通用弹出 = 默认弹窗预制体 EvacuateConfirmPopupUI），
    /// 并动态填入本次返还预览（口径与 TalentSystem.TryRespecRight 一致）。</summary>
    void OpenResetConfirm()
    {
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

        // 2026-10-21 主人拍板：洗点消耗遗忘药水，确认框里让玩家看到当前持有数
        // 【2026-10-27】消耗改阶梯（1/1/1 → 2/2/2 → 3…），按下一次的真实价显示
        int potionHave = SaveSystem.Instance?.Data != null
            ? SaveSystem.Instance.Data.TownBagCount(TalentSystem.ItemForgetPotion) : 0;
        int potionCost = SaveSystem.Instance?.Data != null
            ? TalentSystem.NextRespecPotionCost(SaveSystem.Instance.Data.respecCount) : 1;

        string body =
            "确认要洗点吗？\n" +
            "· 将清空全部右侧天赋（含已选职业）；\n" +
            "· 消耗遗忘药水×" + potionCost + "（当前持有 " + potionHave + "）；\n" +
            "· 返还已投入天赋石的 80%：本次预计返还 " + refund + "（已投入 " + totalSpent + "）；\n" +
            "· 左侧金币天赋不参与洗点，金币不返还。";

        // 2026-10-21 主人拍板：确认弹窗一律走通用弹出（StaminaConfirmPopup，底层默认弹窗预制体
        // EvacuateConfirmPopupUI），不再手搓运行时建树，排版随之统一。
        StaminaConfirmPopup.Show("洗点确认", body, "确定洗点", () =>
        {
            if (!TalentSystem.TryReset(out string reason))
            {
                Debug.Log("[TalentUI] " + reason);
                GlobalToastUI.Show(reason, true);
                RefreshAll();
                return;
            }
            // TryReset 内部已完成：退 80% 天赋石 + SyncBackpackRows + 存档 + HUD 刷新 + RecalcAttr + 成功 toast，这里只刷界面
            RefreshAll();
        });
    }

    void CloseResetConfirm()
    {
        StaminaConfirmPopup.Close();
    }
}
