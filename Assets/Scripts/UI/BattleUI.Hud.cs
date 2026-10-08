using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 顶部状态栏、任务面板、金币与关卡进度条。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    static Sprite _questGoldSprite;

    // 2026-09-15 产出去零：任务奖励金真实值来自 battle_quest.csv（普通关 3+章节，Boss 关 20~200），
    // 默认值同步降一档，避免首帧显示一个已经被砍掉的量级。
    int _questRewardGold = 10;

    /// <summary>任务奖励区：金币图标 + 数量（预制体默认是宝箱图）。</summary>
    public void RefreshQuestReward(int goldAmount = -1)
    {
        if (goldAmount >= 0) _questRewardGold = goldAmount;
        else if (BattleManager.Instance != null && BattleManager.Instance.StageQuestClearGold > 0)
            _questRewardGold = BattleManager.Instance.StageQuestClearGold;

        if (questRewardIcon != null)
        {
            Sprite sp = LoadQuestGoldSprite();
            if (sp != null)
            {
                questRewardIcon.sprite = sp;
                questRewardIcon.preserveAspect = true;
            }
        }
        if (questRewardAmount != null)
            questRewardAmount.text = $"×{_questRewardGold}";
    }

    static Sprite LoadQuestGoldSprite()
    {
        if (_questGoldSprite != null) return _questGoldSprite;
        if (Instance == null) return null;
        Transform goldIcon = FindDeepChildIgnoreCase(Instance.transform, "GoldIcon");
        if (goldIcon != null)
        {
            var img = goldIcon.GetComponent<Image>();
            if (img != null && img.sprite != null)
                _questGoldSprite = img.sprite;
        }
        return _questGoldSprite;
    }

    /// <summary>
    /// 更新关卡信息（章节地区、难度、金币）并刷新资源条。
    ///
    /// 【2026-10-06 主人拍板】两条口径定死：
    /// ① <b>地区 = 章节地点</b>（暮影森林 / 幽冥墓园 …），取自 <see cref="GameConfig.GetChapterMapName"/>；
    /// ② <b>难度 = 玩家选的难度</b>（普通/困难/噩梦），引导局锁普通 —— 真源是
    ///    <c>BattleManager.BattleDifficulty</c>（引导开局写 0），<b>不再</b>按关卡类型显示「精英/Boss」。
    ///    旧实现那段 <c>StageTypeToDifficulty</c> 已删：同一格子里「精英」和「困难」会读成两个意思。
    /// ③ <b>引导局也照常显示</b>：旧实现按 <c>TutorialDirector.IsTutorialBattle</c> 整块藏掉
    ///    （连底框一起藏），主人看到的是左上角空一片，以为根本没做这个 HUD —— 判据已彻底删除。
    /// </summary>
    public void UpdateStageInfo(int chapter, int stage, string difficulty, long gold)
    {
        if (stageLabel != null)
        {
            ShowLabelWithFrame(stageLabel);
            stageLabel.text = GameConfig.GetChapterMapName(chapter);
        }
        if (difficultyLabel != null)
        {
            ShowLabelWithFrame(difficultyLabel);
            difficultyLabel.text = string.IsNullOrEmpty(difficulty)
                ? GameConfig.DifficultyNames[0]
                : difficulty;
        }
        UpdateGold(gold);
        UpdateTopBarResources();
    }

    /// <summary>
    /// 把「地区 / 难度」标签连同它的底框一起点亮。
    /// 标签是底框（StageIcon / DifficultyIcon）的子节点，只点亮文字会留一个空壳底框，
    /// 所以往上找一层带 Image 的父节点整块点亮（父节点是共用的多子容器时不碰，只点亮文字）。
    /// </summary>
    static void ShowLabelWithFrame(Text label)
    {
        if (label == null) return;
        label.gameObject.SetActive(true);
        Transform parent = label.transform.parent;
        if (parent != null && parent.GetComponent<Image>() != null && parent.childCount <= 3)
            parent.gameObject.SetActive(true);
    }

    /// <summary>
    /// 更新顶部资源：金币 / 天赋石 / 材料（附魔石按需求移除，不再显示）。
    /// 金币走 UpdateGold（局内实时），这里只补另外两项。
    /// </summary>
    public void UpdateTopBarResources()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        // 【2026-10-07 主人拍板】顶栏**恢复显示天赋石，且没有时要写「0」**：
        // 主人原话「获得天赋石没有在顶条资源那显示；如果一开始没有的话应该写 0，不应该什么都不显示」。
        // （此前 2026-10-06 这里曾被整段停用、文本清空并隐藏节点 —— 与主人今天的口径冲突，按今天的来。
        //   注：那条旧注释里的「主人原话」未经二次确认，已按归因纪律改成事实陈述，不再挂在主人名下。）
        // 数值真源只有一个：ResourceWallet.TalentPoint，别再从别处另取一份。
        var talentTarget = talentStoneText != null ? talentStoneText : enchantStoneText;
        if (talentTarget != null)
        {
            talentTarget.gameObject.SetActive(true);
            talentTarget.text = ResourceWallet.Get(data, ResourceWallet.ResourceType.TalentPoint).ToString();
        }

        if (decomposeMatText != null)
            decomposeMatText.text = data.decomposeMats.ToString();
    }

    /// <summary>
    /// 更新任务信息
    /// </summary>
    public void UpdateQuest(string desc, int current, int total, int rewardGold = -1)
    {
        if (questDesc != null) questDesc.text = desc;
        if (questProgress != null) questProgress.text = $"({current}/{total})";
        RefreshQuestReward(rewardGold);
    }

    /// <summary>
    /// 更新金币显示
    /// </summary>
    public void UpdateGold(long gold)
    {
        if (goldText != null) goldText.text = gold.ToString();
    }

    /// <summary>
    /// 更新进度条：按关卡索引把 PlayerMarker 挂到对应 Node 下；Boss 通关后挂到 EndFlag
    /// </summary>
    public void UpdateProgress(float progress)
    {
        UpdateStageProgress(-1, progress, false);
    }

    /// <param name="stageIndex">0-based 关卡；&lt;0 时用 progress 0~1</param>
    /// <param name="atEndFlag">打完 Boss 后停在最右旗子</param>
    public void UpdateStageProgress(int stageIndex, float progress = -1f, bool atEndFlag = false)
    {
        if (progressContainer == null) BindProgressBar();
        if (playerMarker == null) return;

        RectTransform markerRT = playerMarker.rectTransform;
        if (atEndFlag && endFlag != null)
        {
            AttachMarkerUnder(endFlag.transform);
            return;
        }

        if (progressNodes != null && progressNodes.Count > 0 && stageIndex >= 0)
        {
            int idx = Mathf.Clamp(stageIndex, 0, progressNodes.Count - 1);
            if (progressNodes[idx] != null)
            {
                AttachMarkerUnder(progressNodes[idx].transform);
                return;
            }
        }

        // 兜底：仍挂在 ProgressBar 下，按 0~1 插值
        if (playerMarker.transform.parent != progressContainer)
            playerMarker.transform.SetParent(progressContainer, false);
        if (progress < 0f) progress = 0f;
        float containerWidth = ((RectTransform)progressContainer).rect.width;
        Vector2 pos = markerRT.anchoredPosition;
        pos.x = Mathf.Lerp(-containerWidth * 0.45f, containerWidth * 0.45f, Mathf.Clamp01(progress));
        markerRT.anchoredPosition = pos;
    }

    void AttachMarkerUnder(Transform parent)
    {
        if (playerMarker == null || parent == null) return;
        if (playerMarker.transform.parent != parent)
            playerMarker.transform.SetParent(parent, false);
        RectTransform markerRT = playerMarker.rectTransform;
        markerRT.anchorMin = new Vector2(0.5f, 0.5f);
        markerRT.anchorMax = new Vector2(0.5f, 0.5f);
        markerRT.pivot = new Vector2(0.5f, 0.5f);
        markerRT.anchoredPosition = Vector2.zero;
        markerRT.localScale = Vector3.one;
        playerMarker.transform.SetAsLastSibling();
    }
}
