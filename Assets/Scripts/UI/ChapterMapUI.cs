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
    public int displayChapterCount = 10;    // 动态节点数量上限
    int _prevMaxUnlocked = 1;

    [Header("其他按钮")]
    public Button backToTownButton;         // 返回城镇按钮
    public Button settingsButton;           // 设置按钮

    static readonly string[] ChapterDescs =
    {
        "",
        "新手村外围，普通怪物出没",
        "幽暗森林，精英怪开始出现",
        "废弃矿坑，陷阱与宝藏并存",
        "裂谷边缘，气流紊乱",
        "古城遗迹，机关与亡灵",
        "霜原关隘，寒冷侵蚀",
        "熔岩甬道，高温考验",
        "迷雾沼泽，视线受阻",
        "星落高塔，强敌环伺",
        "深渊裂口，最终试炼"
    };

    void Awake()
    {
        Instance = this;
        if (chapterNodes == null) chapterNodes = new List<ChapterNodeUI>();
        GameFonts.ApplyToHierarchy(transform);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // 从存档读取最大解锁章节
        if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
        {
            maxUnlockedChapter = SaveSystem.Instance.Data.maxUnlockedChapter;
            if (maxUnlockedChapter < 1) maxUnlockedChapter = 1;
            _prevMaxUnlocked = maxUnlockedChapter;
        }

        // 绑定按钮
        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(OnStartBattle);
        if (backToTownButton != null)
            backToTownButton.onClick.AddListener(OnBackToTown);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnOpenSettings);

        EnsureDynamicNodes();
        RefreshChapterMap();
    }

    /// <summary>
    /// 刷新章节地图显示
    /// </summary>
    public void RefreshChapterMap()
    {
        EnsureDynamicNodes();
        for (int i = 0; i < chapterNodes.Count; i++)
        {
            ChapterNodeUI node = chapterNodes[i];
            if (node == null) continue;

            int chapterId = i + 1;
            bool unlocked = chapterId <= maxUnlockedChapter;
            bool selected = chapterId == selectedChapter;
            bool cleared = IsChapterCleared(chapterId);

            node.Setup(chapterId, unlocked, selected, cleared);
            node.onClick = () => OnSelectChapter(chapterId);
        }

        UpdateInfoPanel();
    }

    void EnsureDynamicNodes()
    {
        int want = Mathf.Clamp(displayChapterCount, 1, 20);
        if (chapterNodes == null) chapterNodes = new List<ChapterNodeUI>();

        // 已有序列化节点：补齐缺失的动态节点
        while (chapterNodes.Count < want)
        {
            ChapterNodeUI created = CreateRuntimeNode(chapterNodes.Count + 1);
            if (created == null) break;
            chapterNodes.Add(created);
        }

        for (int i = 0; i < chapterNodes.Count; i++)
        {
            if (chapterNodes[i]?.root == null) continue;
            chapterNodes[i].root.SetActive(i < want);
        }
    }

    ChapterNodeUI CreateRuntimeNode(int chapterId)
    {
        Transform parent = chapterContainer != null ? chapterContainer : transform;
        GameObject go;
        if (chapterNodePrefab != null)
        {
            go = Instantiate(chapterNodePrefab, parent);
            go.name = "ChapterNode_" + chapterId;
        }
        else
        {
            go = new GameObject("ChapterNode_" + chapterId, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(96f, 96f);
            rt.anchoredPosition = new Vector2((chapterId - 1) * 110f, 0f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.35f, 1f);

            var numGo = new GameObject("Num", typeof(RectTransform), typeof(Text));
            numGo.transform.SetParent(go.transform, false);
            var nrt = numGo.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            var t = numGo.GetComponent<Text>();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 28;
            t.color = Color.white;
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        var node = new ChapterNodeUI { root = go };
        node.chapterNumber = go.GetComponentInChildren<Text>(true);
        node.lockIcon = null;
        node.selectedFrame = null;
        node.clearedMark = null;
        return node;
    }

    static bool IsChapterCleared(int chapterId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        // 解锁到 N+1 意味着第 N 章已通关
        if (data.maxUnlockedChapter > chapterId) return true;
        if (data.chapterClearCounts == null) return false;
        for (int i = 0; i < data.chapterClearCounts.Count; i++)
        {
            var e = data.chapterClearCounts[i];
            if (e != null && e.chapter == chapterId && e.clearCount > 0)
                return true;
        }
        return false;
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
    /// 点击开始战斗（封禁：勿直调 StartNewRun，统一走冒险页入口）
    /// </summary>
    [System.Obsolete("请从 AdventureUI 进战，勿使用 ChapterMapUI 直开战")]
    void OnStartBattle()
    {
        UIManager.Instance?.ShowToast("请从冒险页选择章节开战");
        Debug.LogWarning("[ChapterMapUI] OnStartBattle 已封禁，避免绕过 TryStartNewRunOnce");
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
                PlayUnlockPulse(newMax);
            }
            _prevMaxUnlocked = maxUnlockedChapter;
        }

        Show();
        RefreshChapterMap();
    }

    void PlayUnlockPulse(int chapterId)
    {
        int idx = chapterId - 1;
        if (idx < 0 || idx >= chapterNodes.Count || chapterNodes[idx]?.root == null) return;
        var rt = chapterNodes[idx].root.transform;
        StartCoroutine(CoUnlockPulse(rt));
        UIManager.Instance?.ShowToast($"解锁第{chapterId}章！");
    }

    System.Collections.IEnumerator CoUnlockPulse(Transform t)
    {
        if (t == null) yield break;
        Vector3 baseScale = t.localScale;
        float dur = 0.45f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = 1f + Mathf.Sin((elapsed / dur) * Mathf.PI) * 0.25f;
            t.localScale = baseScale * k;
            yield return null;
        }
        t.localScale = baseScale;
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
        BattleSettingsPanel.Ensure().Open(SettingsHost.Town);
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
        if (chapter > 0 && chapter < ChapterDescs.Length && !string.IsNullOrEmpty(ChapterDescs[chapter]))
            return ChapterDescs[chapter];
        return $"第{chapter}章冒险区域";
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
    public void Setup(int chapterId, bool unlocked, bool selected, bool cleared = false)
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
                btn.onClick.AddListener(() => onClick?.Invoke());
        }

        if (clearedMark != null)
            clearedMark.gameObject.SetActive(cleared && unlocked);
    }
}
