#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 战斗UI预制体生成器
/// 菜单：Tools/生成战斗预制体
/// 一键生成 BattleUI.prefab，包含完整的战斗界面布局
/// 用户只需替换图片资源即可
/// </summary>
public class BattlePrefabGenerator : EditorWindow
{
    // 像素风配色（占位用，用户后续替换图片）
    static readonly Color COLOR_BG_DARK = new Color(0.12f, 0.10f, 0.08f, 0.9f);
    static readonly Color COLOR_PANEL = new Color(0.18f, 0.15f, 0.12f, 0.95f);
    static readonly Color COLOR_PANEL_LIGHT = new Color(0.25f, 0.22f, 0.18f, 0.95f);
    static readonly Color COLOR_GOLD = new Color(1f, 0.8f, 0.2f);
    static readonly Color COLOR_TEXT = new Color(0.95f, 0.92f, 0.85f);
    static readonly Color COLOR_TEXT_DIM = new Color(0.6f, 0.55f, 0.45f);
    static readonly Color COLOR_HP_BG = new Color(0.2f, 0.1f, 0.1f);
    static readonly Color COLOR_HP_FILL = new Color(0.8f, 0.2f, 0.2f);
    static readonly Color COLOR_LOCKED = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    static readonly Color COLOR_GRID_CELL = new Color(0.15f, 0.12f, 0.10f, 0.8f);
    static readonly Color COLOR_GRID_BORDER = new Color(0.35f, 0.3f, 0.25f, 0.6f);
    static readonly Color COLOR_PROGRESS_NODE = new Color(0.4f, 0.35f, 0.3f);
    static readonly Color COLOR_PROGRESS_DONE = new Color(0.3f, 0.7f, 0.3f);
    static readonly Color COLOR_PLAYER_MARKER = new Color(0.3f, 0.6f, 1f);

    [MenuItem("Tools/生成战斗预制体")]
    public static void ShowWindow()
    {
        GenerateBattleUIPrefab();
    }

    /// <summary>
    /// 生成完整的战斗UI预制体
    /// </summary>
    static void GenerateBattleUIPrefab()
    {
        // 统一放在 Resources/Prefabs/Battle（运行时 Resources.Load）
        string prefabDir = "Assets/Resources/Prefabs/Battle";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Battle");

        // === 创建根Canvas（统一规范：Camera / 720×1280 / Match Height）===
        GameObject rootObj = new GameObject("BattleUI");
        Canvas canvas = rootObj.AddComponent<Canvas>();
        canvas.sortingOrder = GameConfig.SORT_BATTLE_UI;
        UICanvasSetup.Apply(canvas, Camera.main);
        rootObj.AddComponent<BattleUI>();

        // 背景全屏暗色
        Image bgImage = CreateImage(rootObj.transform, "Background");
        bgImage.color = COLOR_BG_DARK;
        StretchToFill(bgImage.rectTransform);

        // === 1. 顶部状态栏 ===
        GameObject topBar = CreatePanel(rootObj.transform, "TopBar", 720, 80, COLOR_PANEL);
        SetAnchored(topBar.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -40), new Vector2(720, 80));

        // 关卡标识 "1-1"
        Text stageLabel = CreateText(topBar.transform, "StageLabel", "1-1", 36, Color.white);
        SetAnchored(stageLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(40, 0), new Vector2(133, 40));
        stageLabel.alignment = TextAnchor.MiddleLeft;
        stageLabel.fontStyle = FontStyle.Bold;

        // 难度标识 "★普通"
        Text difficultyLabel = CreateText(topBar.transform, "DifficultyLabel", "★普通", 28, COLOR_GOLD);
        SetAnchored(difficultyLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(40, -23), new Vector2(133, 27));
        difficultyLabel.alignment = TextAnchor.MiddleLeft;

        // 金币图标+数值
        GameObject goldObj = CreatePanel(topBar.transform, "GoldDisplay", 167, 33, COLOR_PANEL_LIGHT);
        SetAnchored(goldObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 0), new Vector2(167, 33));

        Image goldIcon = CreateImage(goldObj.transform, "GoldIcon");
        goldIcon.color = COLOR_GOLD;
        SetAnchored(goldIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(20, 0), new Vector2(27, 27));

        Text goldText = CreateText(goldObj.transform, "GoldText", "0", 28, COLOR_GOLD);
        SetAnchored(goldText.rectTransform, new Vector2(0, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(20, 0), new Vector2(120, 27));
        goldText.alignment = TextAnchor.MiddleLeft;
        goldText.fontStyle = FontStyle.Bold;

        // 设置按钮
        Button settingsBtn = CreateButton(topBar.transform, "SettingsButton", 40, 40, COLOR_PANEL_LIGHT);
        SetAnchored(settingsBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-33, 0), new Vector2(40, 40));
        Text btnText = settingsBtn.GetComponentInChildren<Text>();
        btnText.text = "⚙";
        btnText.fontSize = 32;

        // === 2. 进度条 ===
        GameObject progressBar = CreatePanel(rootObj.transform, "ProgressBar", 640, 40, COLOR_PANEL);
        SetAnchored(progressBar.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -107), new Vector2(640, 40));

        Transform progressContainer = progressBar.transform;

        // 进度条背景线
        Image progressLine = CreateImage(progressBar.transform, "ProgressLine");
        progressLine.color = COLOR_PROGRESS_NODE;
        SetAnchored(progressLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 0), new Vector2(600, 4));

        // 关卡节点圆点（15个节点）
        List<Image> progressNodes = new List<Image>();
        int nodeCount = 15;
        for (int i = 0; i < nodeCount; i++)
        {
            Image node = CreateImage(progressBar.transform, $"Node_{i}");
            node.color = i == 0 ? COLOR_PROGRESS_DONE : COLOR_PROGRESS_NODE;
            float t = (float)i / (nodeCount - 1);
            float x = Mathf.Lerp(-280, 280, t);
            SetAnchored(node.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0), new Vector2(11, 11));
            progressNodes.Add(node);
        }

        // 玩家位置标记
        Image playerMarker = CreateImage(progressBar.transform, "PlayerMarker");
        playerMarker.color = COLOR_PLAYER_MARKER;
        SetAnchored(playerMarker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-280, 0), new Vector2(16, 24));
        playerMarker.rectTransform.SetAsLastSibling();

        // 终点旗帜
        Image endFlag = CreateImage(progressBar.transform, "EndFlag");
        endFlag.color = Color.red;
        SetAnchored(endFlag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(280, 7), new Vector2(20, 27));

        // === 3. 任务面板 ===
        GameObject questPanel = CreatePanel(rootObj.transform, "QuestPanel", 640, 53, COLOR_PANEL);
        SetAnchored(questPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -160), new Vector2(640, 53));

        Text questTitle = CreateText(questPanel.transform, "QuestTitle", "任务", 24, COLOR_GOLD);
        SetAnchored(questTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(20, 0), new Vector2(67, 27));
        questTitle.alignment = TextAnchor.MiddleLeft;
        questTitle.fontStyle = FontStyle.Bold;

        Text questDesc = CreateText(questPanel.transform, "QuestDesc", "击败所有敌人", 24, COLOR_TEXT);
        SetAnchored(questDesc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 0), new Vector2(333, 27));
        questDesc.alignment = TextAnchor.MiddleLeft;

        Text questProgress = CreateText(questPanel.transform, "QuestProgress", "(0/3)", 24, COLOR_TEXT_DIM);
        SetAnchored(questProgress.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20, 0), new Vector2(100, 27));
        questProgress.alignment = TextAnchor.MiddleRight;

        // === 4. 角色栏 ===
        GameObject charBar = CreatePanel(rootObj.transform, "CharacterBar", 720, 133, new Color(0, 0, 0, 0));
        SetAnchored(charBar.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 40), new Vector2(720, 133));

        HorizontalLayoutGroup charLayout = charBar.AddComponent<HorizontalLayoutGroup>();
        charLayout.spacing = 20;
        charLayout.padding = new RectOffset(40, 40, 13, 13);
        charLayout.childAlignment = TextAnchor.MiddleCenter;
        charLayout.childControlWidth = true;
        charLayout.childControlHeight = true;
        charLayout.childForceExpandWidth = true;
        charLayout.childForceExpandHeight = true;

        // 玩家槽位
        CharacterSlotUI playerSlot = CreateCharacterSlot(charBar.transform, "PlayerSlot", false);
        // 佣兵槽位1
        CharacterSlotUI mercSlot1 = CreateCharacterSlot(charBar.transform, "MercSlot1", true);
        // 佣兵槽位2
        CharacterSlotUI mercSlot2 = CreateCharacterSlot(charBar.transform, "MercSlot2", true);

        // === 5. 网格背包 ===
        GameObject backpackPanel = CreatePanel(rootObj.transform, "BackpackPanel", 640, 373, COLOR_PANEL);
        SetAnchored(backpackPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 200), new Vector2(640, 373));

        // 背包标题
        Text backpackTitle = CreateText(backpackPanel.transform, "BackpackTitle", "背包", 28, COLOR_GOLD);
        SetAnchored(backpackTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -13), new Vector2(133, 27));
        backpackTitle.alignment = TextAnchor.MiddleCenter;
        backpackTitle.fontStyle = FontStyle.Bold;

        // 网格容器
        GameObject gridContainer = new GameObject("GridContainer");
        gridContainer.transform.SetParent(backpackPanel.transform, false);
        RectTransform gridRT = gridContainer.AddComponent<RectTransform>();
        SetAnchored(gridRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -13), new Vector2(587, 307));

        GridLayoutGroup gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(87, 87);
        gridLayout.spacing = new Vector2(7, 7);
        gridLayout.padding = new RectOffset(7, 7, 7, 7);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 6;
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        // 创建24个格子
        List<GridCellUI> gridCells = new List<GridCellUI>();
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                GridCellUI cell = CreateGridCell(gridContainer.transform, $"Cell_{x}_{y}", x, y);
                gridCells.Add(cell);
            }
        }

        // === 6. 自动攻击/技能按钮区（底部右侧）===
        GameObject skillBar = CreatePanel(rootObj.transform, "SkillBar", 267, 80, new Color(0, 0, 0, 0));
        SetAnchored(skillBar.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40, 200), new Vector2(267, 80));

        HorizontalLayoutGroup skillLayout = skillBar.AddComponent<HorizontalLayoutGroup>();
        skillLayout.spacing = 10;
        skillLayout.childAlignment = TextAnchor.MiddleRight;
        skillLayout.childControlWidth = false;
        skillLayout.childControlHeight = false;
        skillLayout.childForceExpandWidth = false;
        skillLayout.childForceExpandHeight = false;

        // 技能按钮1
        Button skillBtn1 = CreateButton(skillBar.transform, "SkillBtn1", 60, 60, COLOR_PANEL_LIGHT);
        skillBtn1.GetComponentInChildren<Text>().text = "技能1";
        skillBtn1.GetComponentInChildren<Text>().fontSize = 20;

        // 技能按钮2
        Button skillBtn2 = CreateButton(skillBar.transform, "SkillBtn2", 60, 60, COLOR_PANEL_LIGHT);
        skillBtn2.GetComponentInChildren<Text>().text = "技能2";
        skillBtn2.GetComponentInChildren<Text>().fontSize = 20;

        // 自动战斗按钮
        Button autoBtn = CreateButton(skillBar.transform, "AutoButton", 60, 60, new Color(0.2f, 0.3f, 0.2f));
        autoBtn.GetComponentInChildren<Text>().text = "自动";
        autoBtn.GetComponentInChildren<Text>().fontSize = 24;
        autoBtn.GetComponentInChildren<Text>().color = COLOR_HP_FILL;

        // === 7. 暂停/返回按钮（左下角）===
        Button pauseBtn = CreateButton(rootObj.transform, "PauseButton", 53, 53, COLOR_PANEL_LIGHT);
        SetAnchored(pauseBtn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40, 200), new Vector2(53, 53));
        pauseBtn.GetComponentInChildren<Text>().text = "‖";
        pauseBtn.GetComponentInChildren<Text>().fontSize = 40;

        // === 绑定BattleUI组件引用 ===
        BattleUI battleUI = rootObj.GetComponent<BattleUI>();

        // 顶部状态栏
        battleUI.stageLabel = stageLabel;
        battleUI.difficultyLabel = difficultyLabel;
        battleUI.goldText = goldText;
        battleUI.settingsButton = settingsBtn;

        // 进度条
        battleUI.progressContainer = progressContainer;
        battleUI.progressNodes = progressNodes;
        battleUI.playerMarker = playerMarker;
        battleUI.endFlag = endFlag;

        // 任务面板
        battleUI.questPanel = questPanel;
        battleUI.questTitle = questTitle;
        battleUI.questDesc = questDesc;
        battleUI.questProgress = questProgress;

        // 角色栏
        battleUI.playerSlot = playerSlot;
        battleUI.mercSlot1 = mercSlot1;
        battleUI.mercSlot2 = mercSlot2;

        // 网格背包
        battleUI.gridLayout = gridLayout;
        battleUI.gridCells = gridCells;

        // 技能头像（用Button占位，运行时由SkillAvatarUI管理）
        battleUI.playerSkillAvatar = new SkillAvatarUI { root = skillBtn1.gameObject, avatarImage = skillBtn1.GetComponent<UnityEngine.UI.Image>() };
        battleUI.merc1SkillAvatar = new SkillAvatarUI { root = skillBtn2.gameObject, avatarImage = skillBtn2.GetComponent<UnityEngine.UI.Image>() };

        // 战斗操作按钮
        battleUI.autoButton = autoBtn;
        battleUI.pauseButton = pauseBtn;

        // === 保存为预制体 ===
        GameFonts.ApplyToHierarchy(rootObj.transform);
        string prefabPath = prefabDir + "/BattleUI.prefab";
        PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);

        // 清理场景中的临时对象
        DestroyImmediate(rootObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BattlePrefabGenerator] 战斗UI预制体已生成: {prefabPath}");
        EditorUtility.DisplayDialog("生成完成",
            $"战斗UI预制体已生成！\n\n" +
            "路径: Prefabs/Battle/BattleUI.prefab\n\n" +
            "你可以直接替换其中的图片资源。\n" +
            "所有UI元素已按截图布局创建完毕。", "确定");
    }

    // ======================== 辅助方法 ========================

    /// <summary>
    /// 创建角色槽位
    /// </summary>
    static CharacterSlotUI CreateCharacterSlot(Transform parent, string name, bool startLocked)
    {
        GameObject slot = new GameObject(name);
        slot.transform.SetParent(parent, false);
        RectTransform slotRT = slot.AddComponent<RectTransform>();

        // 背景
        Image slotBg = slot.AddComponent<Image>();
        slotBg.color = COLOR_PANEL;

        LayoutElement le = slot.AddComponent<LayoutElement>();
        le.preferredWidth = 187;
        le.preferredHeight = 107;

        // 头像背景
        Image portrait = CreateImage(slot.transform, "Portrait");
        portrait.color = COLOR_PANEL_LIGHT;
        SetAnchored(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(47, 0), new Vector2(67, 67));

        // 头像占位文字
        Text portraitText = CreateText(portrait.transform, "PortraitPlaceholder", "头像", 18, COLOR_TEXT_DIM);
        SetAnchored(portraitText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(67, 67));
        portraitText.alignment = TextAnchor.MiddleCenter;

        // 等级标签
        Text levelLabel = CreateText(slot.transform, "LevelLabel", "Lv.1", 22, COLOR_GOLD);
        SetAnchored(levelLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-13, -10), new Vector2(80, 23));
        levelLabel.alignment = TextAnchor.MiddleRight;
        levelLabel.fontStyle = FontStyle.Bold;

        // HP条背景
        Image hpBarBg = CreateImage(slot.transform, "HPBarBg");
        hpBarBg.color = COLOR_HP_BG;
        SetAnchored(hpBarBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(33, 13), new Vector2(120, 16));

        // HP条填充
        Image hpBarFill = CreateImage(hpBarBg.transform, "HPBarFill");
        hpBarFill.color = COLOR_HP_FILL;
        hpBarFill.type = Image.Type.Filled;
        hpBarFill.fillMethod = Image.FillMethod.Horizontal;
        hpBarFill.fillAmount = 1f;
        SetAnchored(hpBarFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            Vector2.zero, Vector2.zero);
        StretchToFill(hpBarFill.rectTransform);

        // HP文字
        Text hpText = CreateText(hpBarBg.transform, "HPText", "100/100", 18, Color.white);
        SetAnchored(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(120, 16));
        hpText.alignment = TextAnchor.MiddleCenter;

        // 锁定遮罩
        GameObject lockedOverlay = new GameObject("LockedOverlay");
        lockedOverlay.transform.SetParent(slot.transform, false);
        RectTransform lockedRT = lockedOverlay.AddComponent<RectTransform>();
        StretchToFill(lockedRT);
        Image lockedImg = lockedOverlay.AddComponent<Image>();
        lockedImg.color = COLOR_LOCKED;

        Text lockedText = CreateText(lockedOverlay.transform, "LockedText", "🔒\n需酒馆解锁", 22, COLOR_TEXT_DIM);
        SetAnchored(lockedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(133, 53));
        lockedText.alignment = TextAnchor.MiddleCenter;

        lockedOverlay.SetActive(startLocked);

        // 返回CharacterSlotUI数据
        CharacterSlotUI slotUI = new CharacterSlotUI
        {
            root = slot,
            portrait = portrait,
            levelLabel = levelLabel,
            hpBarFill = hpBarFill,
            hpText = hpText,
            lockedOverlay = lockedOverlay
        };

        return slotUI;
    }

    /// <summary>
    /// 创建网格格子
    /// </summary>
    static GridCellUI CreateGridCell(Transform parent, string name, int gridX, int gridY)
    {
        GameObject cell = new GameObject(name);
        cell.transform.SetParent(parent, false);
        RectTransform cellRT = cell.AddComponent<RectTransform>();

        // 品质边框（外层）
        Image rarityFrame = cell.AddComponent<Image>();
        rarityFrame.color = COLOR_GRID_BORDER;

        // 内层背景
        Image cellBg = CreateImage(cell.transform, "CellBg");
        cellBg.color = COLOR_GRID_CELL;
        SetAnchored(cellBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(83, 83));

        // 装备图标（默认隐藏）
        Image itemIcon = CreateImage(cellBg.transform, "ItemIcon");
        itemIcon.color = Color.white;
        itemIcon.gameObject.SetActive(false);
        SetAnchored(itemIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(73, 73));

        return new GridCellUI
        {
            root = cell,
            itemIcon = itemIcon,
            rarityFrame = rarityFrame,
            gridX = gridX,
            gridY = gridY
        };
    }

    // ======================== UI基础创建方法 ========================

    static GameObject CreatePanel(Transform parent, string name, float width, float height, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        if (color.a > 0)
        {
            Image img = obj.AddComponent<Image>();
            img.color = color;
        }
        return obj;
    }

    static Image CreateImage(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);
        Image img = obj.AddComponent<Image>();
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);
        Text txt = obj.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.font = GameFonts.GetChinese();
        return txt;
    }

    static Button CreateButton(Transform parent, string name, float width, float height, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        Image img = obj.AddComponent<Image>();
        img.color = color;
        Button btn = obj.AddComponent<Button>();

        // 按钮文字
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        StretchToFill(textRT);
        Text txt = textObj.AddComponent<Text>();
        txt.text = "";
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = GameFonts.GetChinese();

        // 按钮过渡效果
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        return btn;
    }

    static void SetAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }

    static void StretchToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
#endif
