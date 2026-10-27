using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 顶部状态栏、任务面板、金币与关卡进度条。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    static Sprite _questGoldSprite;

    // 2026-10-18 任务奖励金真实值来自 battle_quest.csv（普通关 40+5×章节，Boss 关 60~400，主人拍板提额），
    // 默认值同步同档，避免首帧显示一个过期的量级。
    int _questRewardGold = 45;

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
        // 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】任务奖励金进「本局获得金币」那本账
        //（GrantRunGold → 顶栏），图标跟随顶栏用金币图（icon_gold）；
        // 抽奖币池（icon_draft_coin）另有显示位（抽奖面板旁），两本账不混图。
        // 缺图时返回 null（保持预制体原图并报错，不静默拿别的图顶）。
        if (Instance == null) return null;
        var sp = Resources.Load<Sprite>("UI/Art/Icons/Common/icon_gold");
        if (sp == null)
            Debug.LogError("[BattleUI] 金币图标取不到：Art/UI/Icons/Common/icon_gold —— 任务奖励维持原图");
        _questGoldSprite = sp;
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
            // 【2026-10-18 主人反馈】左上角地区名/难度字偏大，运行时收一档（不动预制体）
            stageLabel.fontSize = 19;
        }
        if (difficultyLabel != null)
        {
            ShowLabelWithFrame(difficultyLabel);
            // 【2026-10-18 主人拍板】无限裂隙：左上角难度格改显「当前层数」，每打一层自动更新
            // （本方法由每层 LoadStage → RefreshBattleHud 驱动）。裂隙没有难度选择（恒普通），
            // 这格显示难度没信息量，拿来显示层数正好。普通关仍显难度名，不受影响。
            var bm = BattleManager.Instance;
            if (bm != null && bm.IsEndlessRift)
            {
                difficultyLabel.text = $"第{Mathf.Max(1, bm.EndlessRiftStage)}层";
                // 预制体这格宽 64 是按难度名（2~3 字）摆的；层数打到三位数别换行裁字，放开横向溢出
                difficultyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
            {
                difficultyLabel.text = string.IsNullOrEmpty(difficulty)
                    ? GameConfig.DifficultyNames[0]
                    : difficulty;
            }
            difficultyLabel.fontSize = 16;
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

        // 【2026-10-26 主人拍板「只留一个抽奖券，把金币和天赋石隐藏」】
        // 金币条 / 天赋石条整条隐藏，顶栏资源区只留 DraftDisplay 抽奖券条。
        // （覆盖 2026-10-07「天赋石顶栏显示且没有写 0」的旧口径，以主人最新指令为准。）
        // 放在刷新开头重申一遍：AutoGameInitializer / TutorialDirector 也会单独调本方法，防被点亮复活。
        ApplyTopBarResourceVisibility();

        if (decomposeMatText != null)
            decomposeMatText.text = data.decomposeMats.ToString();
    }

    /// <summary>
    /// 【2026-10-26 主人拍板「顶栏只留一个抽奖券」】金币条与天赋石条整条隐藏。
    /// 从资源数字文本（goldText / talentStoneText / enchantStoneText）往上找第一个带 Image 的祖先
    ///（= 图标+数字共用的胶囊底框，即整条资源条节点，如 GoldDisplay / EnchantDisplay），
    /// SetActive(false)；上溯最多 3 层、撞到 TopBar 本体就停，绝不误伤整条顶栏。幂等，可反复调用。
    /// UpdateGold / UpdateDraftCoin 只写文本、不点亮节点，隐藏一次即长期生效。
    /// </summary>
    public void ApplyTopBarResourceVisibility()
    {
        HideTopBarEntryByLabel(goldText);          // 金币条（GoldDisplay）
        HideTopBarEntryByLabel(talentStoneText);   // 天赋石（新布局直绑的 TalentText 等）
        HideTopBarEntryByLabel(enchantStoneText);  // 天赋石（旧布局第三资源位 EnchantText，新顶栏借它显示天赋石）
    }

    /// <summary>按资源数字文本隐藏其所在资源条（见 <see cref="ApplyTopBarResourceVisibility"/>）。</summary>
    static void HideTopBarEntryByLabel(Text label)
    {
        if (label == null) return;
        Transform cur = label.transform.parent;
        Transform entry = null;
        for (int i = 0; i < 3 && cur != null; i++)
        {
            if (cur.name == "TopBar") break;
            if (cur.GetComponent<UnityEngine.UI.Image>() != null) { entry = cur; break; }
            cur = cur.parent;
        }
        if (entry != null && entry.gameObject.activeSelf)
            entry.gameObject.SetActive(false);
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
    long _lastGoldShown = -1;
    Coroutine _goldPulseCo;
    Color _goldBaseColor;
    bool _goldBaseColorCached;

    public void UpdateGold(long gold)
    {
        if (goldText == null) return;
        // 数字没变就不碰（频繁刷新也不闪），只在「钱变了」时跳动一次。
        if (gold == _lastGoldShown) return;
        _lastGoldShown = gold;
        goldText.text = gold.ToString();
        if (!_goldBaseColorCached) { _goldBaseColor = goldText.color; _goldBaseColorCached = true; }
        if (_goldPulseCo != null) StopCoroutine(_goldPulseCo);
        _goldPulseCo = StartCoroutine(CoGoldPulse());
    }

    /// <summary>【2026-10-13 #9】金币变化时 scale 1→1.18→1（180ms）+ 短暂变 #FFB833，让「钱在涨」被看见。</summary>
    IEnumerator CoGoldPulse()
    {
        if (goldText == null) { _goldPulseCo = null; yield break; }
        Color baseCol = _goldBaseColorCached ? _goldBaseColor : goldText.color;
        goldText.color = new Color(1f, 0.72f, 0.2f); // #FFB833
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.18f);
            goldText.transform.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        goldText.transform.localScale = Vector3.one;
        goldText.color = baseCol;
        _goldPulseCo = null;
    }

    /// <summary>
    /// 顶栏 DraftDisplay 抽奖币资源条（主人 2026-10-18 摆的节点）：数字 = BattleManager.currentGold
    ///（抽奖唯一能花的那本账，启动金/补贴/抽中金币，带不出本局）。
    /// 变化统一走 BattleEntryDraftPanel.NotifyPoolGoldChanged（AddRunGold/SpendBattleGold 调），
    /// 别处不许直接改文本。没做跳动特效 —— 抽奖扣钱/进钱都在抽奖面板上有完整反馈，这里只对数。
    /// </summary>
    public void UpdateDraftCoin(long coins)
    {
        if (draftCoinText == null) return;
        draftCoinText.text = coins.ToString();
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

        // 【2026-10-26 主人拍板】顶部进度条整条去掉：所有战斗都不再显示
        //（覆盖 2026-10-16「引导战才藏、正式关再开」口径）。绑定/推进逻辑保留不动，
        // 只在显隐处统一关死，BossHpBar 让顶逻辑也不会再点亮它。
        if (progressContainer.gameObject.activeSelf)
            progressContainer.gameObject.SetActive(false);

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

    /// <summary>
    /// 【2026-10-21 主人拍板】顶部进度条改按「当前关波次」推进：clearedWaves = 已清完波数，totalWaves = 本关总波数。
    /// 内部走 UpdateStageProgress 的 0~1 插值兜底分支（stageIndex 传 -1，progressNodes 挂点分支不触发），
    /// 复用同一条进度条和 playerMarker，不新增 UI 节点。
    /// </summary>
    public void UpdateWaveProgress(int clearedWaves, int totalWaves)
    {
        UpdateStageProgress(-1, totalWaves > 0 ? Mathf.Clamp01((float)clearedWaves / totalWaves) : 0f, false);
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
