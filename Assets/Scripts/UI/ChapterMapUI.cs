using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 章节地图界面：冒险主入口
/// 显示各章节节点，点击选择进入战斗
/// 战斗结束后（通关/撤离/死亡）回到此界面
/// </summary>
public class ChapterMapUI : MonoBehaviour
{
    public static ChapterMapUI Instance;

    [Header("章节节点")]
    public Transform chapterContainer;      // 章节节点容器（横向滚动）
    public GameObject chapterNodePrefab;    // 章节节点预制体
    public List<ChapterNodeUI> chapterNodes; // 章节节点列表

    [Header("信息面板")]
    public Text chapterTitle;               // "第X章"
    public Text chapterDesc;                // 章节描述
    public Text bestRecord;                 // 最佳记录 "最高通关: 第3章"
    public Button startBattleButton;        // 开始战斗按钮

    [Header("当前选择")]
    public int selectedChapter = 1;         // 当前选中的章节
    public int maxUnlockedChapter = 1;      // 最大解锁章节

    [Header("其他按钮")]
    public Button backToTownButton;         // 返回城镇按钮
    public Button settingsButton;           // 设置按钮

    void Awake()
    {
        Instance = this;
        GameFonts.ApplyToHierarchy(transform);
    }

    void Start()
    {
        // 从存档读取最大解锁章节
        if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
        {
            maxUnlockedChapter = SaveSystem.Instance.Data.maxUnlockedChapter;
            if (maxUnlockedChapter < 1) maxUnlockedChapter = 1;
        }

        // 绑定按钮
        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(OnStartBattle);
        if (backToTownButton != null)
            backToTownButton.onClick.AddListener(OnBackToTown);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnOpenSettings);

        RefreshChapterMap();
    }

    /// <summary>
    /// 刷新章节地图显示
    /// </summary>
    public void RefreshChapterMap()
    {
        // TODO: 根据实际章节数量动态生成节点
        // 这里先简化，假设显示10章
        for (int i = 0; i < chapterNodes.Count; i++)
        {
            ChapterNodeUI node = chapterNodes[i];
            if (node == null) continue;

            int chapterId = i + 1;
            bool unlocked = chapterId <= maxUnlockedChapter;
            bool selected = chapterId == selectedChapter;

            node.Setup(chapterId, unlocked, selected);
            node.onClick = () => OnSelectChapter(chapterId);
        }

        UpdateInfoPanel();
    }

    /// <summary>
    /// 选择章节
    /// </summary>
    void OnSelectChapter(int chapterId)
    {
        if (chapterId > maxUnlockedChapter) return; // 未解锁不能选

        selectedChapter = chapterId;
        RefreshChapterMap();
    }

    /// <summary>
    /// 点击开始战斗
    /// </summary>
    void OnStartBattle()
    {
        // 未完成战斗存档：清掉后重新开战（旧 Restore 不 LoadStage，会导致整关无怪）
        if (BattleStateSaver.Instance != null && BattleStateSaver.Instance.HasSavedBattle())
        {
            Debug.LogWarning("[ChapterMapUI] 发现未完成战斗存档 → 清除并重新 StartNewRun（避免无刷怪）");
            BattleStateSaver.Instance.ClearBattleState();
        }

        if (ChapterManager.Instance != null)
            ChapterManager.Instance.SetChapter(selectedChapter);
        if (BattleManager.Instance != null)
            BattleManager.Instance.StartNewRun();

        Hide();
        Debug.Log($"[ChapterMapUI] 进入第{selectedChapter}章战斗");
    }

    /// <summary>
    /// 战斗结束后调用，显示章节地图
    /// </summary>
    public void ShowAfterBattle()
    {
        // 检查是否解锁了新章节
        if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
        {
            int newMax = SaveSystem.Instance.Data.maxUnlockedChapter;
            if (newMax > maxUnlockedChapter)
            {
                maxUnlockedChapter = newMax;
                // TODO: 播放解锁动画
            }
        }

        Show();
        RefreshChapterMap();
    }

    /// <summary>
    /// 返回城镇
    /// </summary>
    void OnBackToTown()
    {
        Hide();
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadTownScene();
        }
    }

    void OnOpenSettings()
    {
        // TODO: 打开设置面板
    }

    /// <summary>
    /// 更新信息面板
    /// </summary>
    void UpdateInfoPanel()
    {
        if (chapterTitle != null)
            chapterTitle.text = $"第{selectedChapter}章";
        if (chapterDesc != null)
            chapterDesc.text = GetChapterDesc(selectedChapter);
        if (bestRecord != null)
            bestRecord.text = $"最高通关: 第{maxUnlockedChapter}章";

        // 开始按钮状态
        if (startBattleButton != null)
        {
            bool canStart = selectedChapter <= maxUnlockedChapter;
            startBattleButton.interactable = canStart;
            var btnText = startBattleButton.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = canStart ? "开始冒险" : "未解锁";
        }
    }

    string GetChapterDesc(int chapter)
    {
        // TODO: 从配置读取章节描述
        switch (chapter)
        {
            case 1: return "新手村外围，普通怪物出没";
            case 2: return "幽暗森林，精英怪开始出现";
            case 3: return "废弃矿坑，陷阱与宝藏并存";
            default: return "未知的冒险区域...";
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        RefreshChapterMap();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

/// <summary>
/// 章节节点UI
/// </summary>
[System.Serializable]
public class ChapterNodeUI
{
    public GameObject root;             // 根对象
    public Image chapterIcon;           // 章节图标
    public Text chapterNumber;          // 章节数字 "1"
    public Image lockIcon;              // 锁定图标
    public Image selectedFrame;         // 选中边框
    public Image clearedMark;           // 已通关标记

    [System.NonSerialized]
    public System.Action onClick;

    /// <summary>
    /// 设置节点状态
    /// </summary>
    public void Setup(int chapterId, bool unlocked, bool selected)
    {
        if (root == null) return;
        root.SetActive(true);

        if (chapterNumber != null)
            chapterNumber.text = chapterId.ToString();

        if (lockIcon != null)
            lockIcon.gameObject.SetActive(!unlocked);

        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(selected);

        // 按钮交互
        var btn = root.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = unlocked;
            btn.onClick.RemoveAllListeners();
            if (unlocked)
            {
                btn.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        // 已通关标记
        if (clearedMark != null)
        {
            // TODO: 从存档判断是否已通关
            clearedMark.gameObject.SetActive(false);
        }
    }
}
