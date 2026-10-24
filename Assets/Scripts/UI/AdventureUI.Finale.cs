using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 冒险界面 partial —— 2026-10-23 新增：终章追查入口（主人拍板「最后一关后剧情二选一，选完再出几关进完整剧情」）
//
// 【流程】主线最后一关通关 → N8 结局演出 → 终章余音剧情二选一（ChapterStoryBeats.TryPlayFinaleChoice）→
//         选「追查真相」（postFinalChoice=2）→ 本入口在活动副本区亮起 →
//         依次打 2 个追查关（FinaleDefs，第 1 关打完解锁第 2 关）→ 打完播完整揭底演出。
//
// 【实现】运行时克隆活动副本区「金币」横条生成第三条「追查」（结构 bg/icon/Name/Info 同源）——
//         不改 prefab、不动排版（照 AdventureUI.BranchIslands 的运行时补节点惯例）。
//         位置：徽记横条下方一行（行距 = 金币行 y − 徽记行 y，运行时实测不写死）。
//         点击 → 确认弹窗 → 进「第一个未通关」的追查关（线性推进，玩家不用选关）。
//         免体力、不掉装备（剧情关口径，文档里标待主人确认）。
public partial class AdventureUI
{
    /// <summary>追查横条节点名（运行时克隆生成，挂在 RightContent/活动副本 下）。</summary>
    const string FinaleRowName = "追查";

    Transform _finaleRow;
    bool _finaleRowMissingReported;

    /// <summary>
    /// 克隆金币横条生成追查入口（只做一次；找不到源横条 LogError 不重试防刷屏）。
    /// </summary>
    void EnsureFinaleRow()
    {
        if (_finaleRow != null) return;
        var gold = transform.Find("RightContent/" + ActivityNodeName + "/" + ActivityGoldRowName);
        if (gold == null)
        {
            if (!_finaleRowMissingReported)
            {
                _finaleRowMissingReported = true;
                Debug.LogError("[AdventureUI] 找不到 RightContent/活动副本/金币 横条，追查入口无从克隆（缺真值不兜底）");
            }
            return;
        }

        var listTf = gold.parent;
        var clone = Instantiate(gold, listTf);
        clone.name = FinaleRowName;
        _finaleRow = clone;

        // 摆位：贴在徽记条下方一行。行距 = 金币行 y − 徽记行 y（运行时实测，prefab 改行距自动跟）
        var badge = listTf.Find(ActivityBadgeRowName) as RectTransform;
        var crt = clone as RectTransform;
        if (badge != null)
        {
            float rowDist = (gold as RectTransform).anchoredPosition.y - badge.anchoredPosition.y;
            crt.anchoredPosition = new Vector2(badge.anchoredPosition.x, badge.anchoredPosition.y - rowDist);
        }

        // 文案：名字 + 进度（进度每次 RefreshFinaleRow 刷新）
        var nameTxt = clone.Find("Name")?.GetComponent<Text>();
        if (nameTxt != null) nameTxt.text = "追查真相";
        else Debug.LogError("[AdventureUI] 追查横条缺 Name 文本节点（缺真值不兜底）");

        // 源横条的「选中」高亮子节点不用于追查条（点击直接进本，无选中态）→ 关掉
        var selectedTf = clone.Find("选中");
        if (selectedTf != null) selectedTf.gameObject.SetActive(false);

        // 按钮：克隆体可能带着源条的 Button（无监听，UnityEvent 监听不随克隆走）→ 统一清掉重接
        var btn = clone.GetComponent<Button>();
        if (btn == null) btn = clone.gameObject.AddComponent<Button>();
        var bgImg = clone.Find("bg")?.GetComponent<Image>();
        if (bgImg != null)
        {
            btn.targetGraphic = bgImg;
            bgImg.raycastTarget = true;
        }
        btn.transition = Selectable.Transition.ColorTint;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickFinaleRow);
    }

    /// <summary>
    /// 追查入口显隐 + 进度刷新（RefreshAll 调）。
    /// 显示条件：主线最后一章已通关 && 已选「追查真相」 && 追查关没打满（打满 = 故事线完结，入口消失）。
    /// </summary>
    void RefreshFinaleRow()
    {
        EnsureFinaleRow();
        if (_finaleRow == null) return;
        var data = SaveSystem.Instance?.Data;
        bool show = data != null
                    && StoryEnding.HasCleared(data, 8)
                    && data.postFinalChoice == 2
                    && !data.HasClearedAllFinaleStages();
        if (_finaleRow.gameObject.activeSelf != show)
            _finaleRow.gameObject.SetActive(show);
        if (!show) return;

        int cleared = 0;
        for (int i = 1; i <= FinaleDefs.Count; i++)
            if (data.HasClearedFinaleStage(i)) cleared++;
        var infoTxt = _finaleRow.Find("Info")?.GetComponent<Text>();
        if (infoTxt != null) infoTxt.text = $"完整揭底 {cleared}/{FinaleDefs.Count}";
    }

    /// <summary>
    /// 点追查入口：进第一个未通关的追查关（线性推进）。确认弹窗走通用 StaminaConfirmPopup（硬规则）。
    /// 免体力（剧情关口径）；P0 前用第 8 篇背景与怪池（FinaleDefs.act）。
    /// </summary>
    void OnClickFinaleRow()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (!StoryEnding.HasCleared(data, 8))
        {
            Toast("通关主线最后一章后开启");
            return;
        }
        if (data.postFinalChoice != 2)
        {
            Toast("追查线未开启");
            return;
        }

        // 第一个未通关的追查关（线性；全通时入口应已隐藏，到这里就报异常态）
        int stageId = 0;
        for (int i = 1; i <= FinaleDefs.Count; i++)
        {
            if (!data.HasClearedFinaleStage(i)) { stageId = i; break; }
        }
        if (stageId == 0)
        {
            Toast("追查已经完结了");
            return;
        }
        var st = FinaleDefs.Get(stageId);
        if (st == null)
        {
            Debug.LogError($"[AdventureUI] 追查关定义缺失（stageId={stageId}）");
            return;
        }

        string body = $"不消耗体力 · 不掉装备\n打完接{(stageId == 1 ? "揭底中段" : "完整揭底")}剧情";
        StaminaConfirmPopup.Show($"终章追查·{st.name}", body, "进入", () =>
        {
            // 进本标记（照支线岛口径：Pending 全量重设，BattleManager 读后即清）
            AdventureUI.PendingFinaleStage = st.id;
            AdventureUI.PendingBattleChapter = st.act;
            AdventureUI.PendingBattleDifficulty = 0;
            AdventureUI.PendingGoldDungeon = false;
            AdventureUI.PendingBadgeDungeon = false;
            AdventureUI.PendingScrollDungeon = false;
            AdventureUI.PendingEndlessRift = false;
            AdventureUI.PendingSideIsland = 0;
            ChapterManager.Instance?.SetChapter(st.act);

            // 不弹职业选择（照支线岛/裂隙口径：刷图玩法用当前职业直接进本）
            HidePage();
            GameSceneManager.Instance?.LoadBattleScene();
        });
    }
}
