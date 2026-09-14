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

    int _questRewardGold = 100;

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

    static string StageTypeToDifficulty(StageType t)
    {
        switch (t)
        {
            case StageType.Elite: return "精英";
            case StageType.Boss: return "Boss";
            default: return "普通";
        }
    }

    /// <summary>
    /// 更新关卡信息（章节、难度、金币）并刷新资源条
    /// </summary>
    public void UpdateStageInfo(int chapter, int stage, string difficulty, long gold)
    {
        bool hideStageInfo = TutorialDirector.IsTutorialBattle;
        if (stageLabel != null)
        {
            // 连底框一起藏：文字挂在 StageIcon 上，只藏文字会留一个空壳
            SetLabelWithFrameVisible(stageLabel, !hideStageInfo);
            if (!hideStageInfo)
                stageLabel.text = GameConfig.GetChapterMapName(chapter);
        }
        if (difficultyLabel != null)
        {
            SetLabelWithFrameVisible(difficultyLabel, !hideStageInfo);
            if (!hideStageInfo)
                difficultyLabel.text = string.IsNullOrEmpty(difficulty) ? "普通" : difficulty;
        }
        UpdateGold(gold);
        UpdateTopBarResources();
    }

    /// <summary>
    /// 章节/难度标签连同它的底框一起显示或隐藏。
    /// 标签是底框（StageIcon / DifficultyIcon）的子节点，所以往上找一层带 Image 的父节点整块关掉。
    /// </summary>
    static void SetLabelWithFrameVisible(Text label, bool visible)
    {
        if (label == null) return;
        Transform parent = label.transform.parent;
        // 父节点自己有图（就是底框）时关父节点；否则退化成只关文字
        if (parent != null && parent.GetComponent<Image>() != null
            && parent.childCount <= 3)
        {
            parent.gameObject.SetActive(visible);
            return;
        }
        label.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 更新顶部资源：金币 / 天赋石 / 材料；附魔石有独立文本则另刷
    /// </summary>
    public void UpdateTopBarResources()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        if (talentStoneText != null)
            talentStoneText.text = data.talentPoints.ToString();
        else if (enchantStoneText != null)
            // 旧布局三资源：金 / 天赋石(占附魔位) / 材料
            enchantStoneText.text = data.talentPoints.ToString();

        if (talentStoneText != null && enchantStoneText != null)
            enchantStoneText.text = data.enchantStones.ToString();

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
