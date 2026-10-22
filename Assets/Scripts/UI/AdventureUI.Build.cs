using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 冒险界面 partial —— 2026-10-18 由 AdventureUI.cs 纯位移拆出（无逻辑改动）
// 职责：构建 UI 树（Build/左侧栏/右内容/地图/详情面板及 Create*、Build* 小件）
public partial class AdventureUI
{
    // ────────────────────────────────────────────────────
    // 构建 UI 树
    // ────────────────────────────────────────────────────

    /// <summary>倒序销毁 parent 下名为 name 的旧子节点及其所有子孙（防两套叠加）。</summary>
    static void DestroyNamedChild(Transform parent, string name)
    {
        if (parent == null) return;
        var old = parent.Find(name);
        if (old == null) return;
        for (int i = old.childCount - 1; i >= 0; i--)
            Destroy(old.GetChild(i).gameObject);
        Destroy(old.gameObject);
    }

    void Build()
    {
        // 防两套叠加：运行时 prefab 可能已把生成树烘焙进资源；若走到重建路径
        // （PreloadOnce 的 Find 未命中 LeftSidebar/RightContent），先按实际父级倒序
        // 销毁同名旧节点再重建，避免「prefab 旧残留 + 代码新生成」两份叠加（界面变宽/重影）。
        DestroyNamedChild(transform, "LeftSidebar");
        DestroyNamedChild(transform, "RightContent");
        // MapRoot/StageNodes 挂在 RightContent 下，DetailPanel 挂在 RightContent 下：
        // 销毁 RightContent 已级联销毁它们；此处按实际父级兜底清一次（父缺失则为 no-op）。
        DestroyNamedChild(transform.Find("RightContent"), "MapRoot");
        DestroyNamedChild(transform.Find("MapRoot"), "StageNodes");
        DestroyNamedChild(transform.Find("RightContent"), "DetailPanel");

        // ── 根 RectTransform ──
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        Stretch(rt);

        // ── 半透明遮罩覆盖主大厅（统一 TownPageDim）──
        TownPageDim.Ensure(transform);

        // ── 主体：左侧副本列表 + 右侧内容 ──
        // 内容区上下留出 TopBar 和 BottomNav 的空间
        float contentTop = -TOP_H;
        float contentBot = BOT_H;

        BuildLeftSidebar(contentTop, contentBot);
        BuildRightContent(contentTop, contentBot);

        _built = true;
    }

    // ── 左侧副本类型竖列 ──
    void BuildLeftSidebar(float top, float bot)
    {
        var go = new GameObject("LeftSidebar", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 0.5f);
        rt.offsetMin = new Vector2(0, bot);
        rt.offsetMax = new Vector2(LEFT_W, top);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.07f, 0.03f, 0.95f);

        float btnH = 100f;
        float gap   = 8f;
        float startY = -top - 20f;   // 从顶部偏移开始

        string[] iconPlaceholders = { "⚔", "★", "◎", "✦", "☆" };

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var btn = CreateSidebarButton(go.transform,
                $"ModeBtn_{i}", ModeNames[i], iconPlaceholders[i],
                startY + i * (btnH + gap), btnH);

            if (modeButtons.Length > i) modeButtons[i] = btn;
            var label = btn.GetComponentInChildren<Text>();
            if (modeButtonLabels.Length > i) modeButtonLabels[i] = label;

            if (modeButtonSpriteIcons != null && modeButtonSpriteIcons.Length > i && modeButtonSpriteIcons[i] != null)
            {
                var iconImg = btn.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = modeButtonSpriteIcons[i];
                    iconImg.color = Color.white;
                    var iconTxt = btn.transform.Find("IconText")?.GetComponent<Text>();
                    if (iconTxt != null) iconTxt.gameObject.SetActive(false);
                }
            }
        }

        // 字号与预制体绑定路径同一出口（2026-10-22 主人拍板，见 ApplyModeLabelStyle）
        ApplyModeLabelStyle();
    }

    Button CreateSidebarButton(Transform parent, string name, string label,
        string iconChar, float yOffset, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -yOffset);
        rt.sizeDelta = new Vector2(0, h);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.13f, 0.06f, 1f);
        var btn = go.GetComponent<Button>();

        // icon 占位（Image + Text 两套：你替换 Sprite 时只保留 Image）
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconRt.sizeDelta = new Vector2(52, 52);
        iconRt.anchoredPosition = Vector2.zero;
        iconGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        // 默认 icon 字符（你未填 sprite 时可见）
        var iconTxtGo = new GameObject("IconText", typeof(RectTransform), typeof(Text));
        iconTxtGo.transform.SetParent(go.transform, false);
        var iconTxtRt = iconTxtGo.GetComponent<RectTransform>();
        iconTxtRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconTxtRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconTxtRt.sizeDelta = new Vector2(52, 52);
        iconTxtRt.anchoredPosition = Vector2.zero;
        var iconTxt = iconTxtGo.GetComponent<Text>();
        iconTxt.text      = iconChar;
        iconTxt.fontSize  = 28;
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.color     = Color.white;

        // label
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0, 0);
        labelRt.anchorMax = new Vector2(1, 0.45f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var txt = labelGo.GetComponent<Text>();
        txt.text      = label;
        txt.fontSize  = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.9f, 0.82f, 0.65f);
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;

        return btn;
    }

    // ── 右侧内容区（地图 + 详情）──
    void BuildRightContent(float top, float bot)
    {
        var go = new GameObject("RightContent", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(LEFT_W, bot);
        rt.offsetMax = new Vector2(0, top);

        // ── 地图背景区 ──
        // 当前 UI 逻辑高度：瘦屏（9:19~9:21）按宽度匹配时 > 1280，标准 720×1280 屏恒为 1280。
        // 直接复用 CanvasScaler 的取值逻辑，避免把地图高度写死成设计分辨率。
        float uiLogicalH = DesignAspectLetterbox.ResolveUiMatch() < 0.5f
            ? GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height)
            : GameConfig.DESIGN_HEIGHT;
        float mapH = uiLogicalH - TOP_H - BOT_H - DETAIL_H - 16f;
        var mapRoot = new GameObject("MapRoot", typeof(RectTransform));
        mapRoot.transform.SetParent(go.transform, false);
        var mapRt = mapRoot.GetComponent<RectTransform>();
        mapRt.anchorMin = new Vector2(0, 1);
        mapRt.anchorMax = new Vector2(1, 1);
        mapRt.pivot     = new Vector2(0.5f, 1f);
        mapRt.anchoredPosition = Vector2.zero;
        mapRt.sizeDelta = new Vector2(0, mapH);

        // 地图背景图（占位：深棕色框，替换成 Sprite）
        var mapBgImg = CreateImg(mapRoot.transform, "MapBg",
            new Color(0.18f, 0.26f, 0.14f, 1f));
        Stretch(mapBgImg.rectTransform);
        mapBg = mapBgImg;
        if (mapBackgroundSprite != null)
        {
            mapBg.sprite = mapBackgroundSprite;
            mapBg.color = Color.white;
        }

        // 章节标题栏
        BuildMapTitleBar(mapRoot.transform, mapH);

        // 节点容器
        var nodeContainer = new GameObject("StageNodes", typeof(RectTransform));
        nodeContainer.transform.SetParent(mapRoot.transform, false);
        var ncRt = nodeContainer.GetComponent<RectTransform>();
        ncRt.anchorMin = new Vector2(0, 0);
        ncRt.anchorMax = new Vector2(1, 1);
        ncRt.offsetMin = new Vector2(0, 50f);
        ncRt.offsetMax = new Vector2(0, -54f);
        stageNodeContainer = nodeContainer.transform;

        // 生成示例节点（1-1 到 1-7 + BOSS，布局参考截图）
        BuildStageNodes(nodeContainer.transform);

        // 底部进度条 + 按钮行
        BuildMapBottomBar(mapRoot.transform);

        // ── 关卡详情面板 ──
        BuildDetailPanel(go.transform, mapH);
    }

    void BuildMapTitleBar(Transform parent, float mapH)
    {
        var bar = new GameObject("TitleBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 54f);

        var barBg = bar.AddComponent<Image>();
        barBg.color = new Color(0.10f, 0.06f, 0.02f, 0.88f);

        // ← 按钮
        prevChapterBtn = CreateIconBtn(bar.transform, "PrevBtn", "◀", new Vector2(30, -27), 44f);

        // 章节名
        var titleGo = new GameObject("ChapterTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(bar.transform, false);
        var tRt = titleGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.1f, 0);
        tRt.anchorMax = new Vector2(0.9f, 1);
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        chapterTitle = titleGo.GetComponent<Text>();
        chapterTitle.text      = "";
        chapterTitle.fontSize  = 26;
        chapterTitle.alignment = TextAnchor.MiddleCenter;
        chapterTitle.color     = new Color(1f, 0.92f, 0.7f);

        // → 按钮
        nextChapterBtn = CreateIconBtn(bar.transform, "NextBtn", "▶", new Vector2(-30, -27), 44f);
    }

    Button CreateIconBtn(Transform parent, string name, string icon,
        Vector2 anchoredPos, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        bool left = anchoredPos.x > 0;
        rt.anchorMin = new Vector2(left ? 0 : 1, 0.5f);
        rt.anchorMax = new Vector2(left ? 0 : 1, 0.5f);
        rt.pivot     = new Vector2(left ? 0 : 1, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        go.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.08f, 0.9f);
        var btn = go.GetComponent<Button>();

        var txtGo = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        Stretch(txtGo.GetComponent<RectTransform>());
        var t = txtGo.GetComponent<Text>();
        t.text = icon; t.fontSize = 22;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return btn;
    }

    // 生成关卡节点（位置参考截图：散点布局，7个普通+1个Boss）
    void BuildStageNodes(Transform container)
    {
        // 相对于容器中心的偏移（归一化：容器宽550×高约480，可自由调整）
        // 参考截图顺序：1-1左上，1-2中上，1-3右上，BOSS中，1-4右中，1-5右下，1-6右下，1-6左，1-7左下
        var positions = new Vector2[]
        {
            new Vector2(-180,  140),  // 1-1
            new Vector2( -40,  175),  // 1-2
            new Vector2(  90,  185),  // 1-3（高亮）
            new Vector2(   0,   50),  // BOSS
            new Vector2( 160,   80),  // 1-4
            new Vector2( 145,  -40),  // 1-5
            new Vector2(  50, -130),  // 1-6 右
            new Vector2(-130, -130),  // 1-6 左（截图有两个1-6，一个应为1-7的占位）
            new Vector2(-175,  -45),  // 1-7
        };
        string[] labels = { "1-1","1-2","1-3","BOSS","1-4","1-5","1-6","1-6","1-7" };
        bool[]   isBoss = { false,false,false,true,false,false,false,false,false };
        int[]    stars  = { 3,3,4,4,3,3,3,3,3 };

        _stageNodeBtns.Clear();
        for (int i = 0; i < positions.Length; i++)
        {
            int idx = i;
            var node = BuildStageNode(container, labels[i], positions[i],
                stars[i], isBoss[i], i == 2);
            _stageNodeBtns.Add(node);
            int chapter = Mathf.Clamp(idx + 1, 1, 8);
            node.onClick.AddListener(() => OnSelectChapter(chapter));
        }
        _selectedChapter = 1;
    }

    Button BuildStageNode(Transform parent, string label, Vector2 pos,
        int starCount, bool isBoss, bool selected)
    {
        float size = isBoss ? 80f : 64f;
        var go = new GameObject("Node_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);

        Color bg = isBoss
            ? new Color(0.55f, 0.10f, 0.08f, 1f)
            : selected
                ? new Color(0.80f, 0.65f, 0.15f, 1f)
                : new Color(0.22f, 0.36f, 0.22f, 1f);
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();

        // 关卡编号文字
        var numGo = new GameObject("Num", typeof(RectTransform), typeof(Text));
        numGo.transform.SetParent(go.transform, false);
        var nRt = numGo.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0, 0.35f);
        nRt.anchorMax = new Vector2(1, 1);
        nRt.offsetMin = nRt.offsetMax = Vector2.zero;
        var numTxt = numGo.GetComponent<Text>();
        numTxt.text      = label;
        numTxt.fontSize  = isBoss ? 16 : 18;
        numTxt.alignment = TextAnchor.MiddleCenter;
        numTxt.color     = Color.white;
        numTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 星星行（最多4颗，放在节点下方）
        if (starCount > 0 && !isBoss)
        {
            var starBar = new GameObject("Stars", typeof(RectTransform), typeof(Text));
            starBar.transform.SetParent(go.transform, false);
            var sRt = starBar.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0, 0);
            sRt.anchorMax = new Vector2(1, 0.38f);
            sRt.offsetMin = sRt.offsetMax = Vector2.zero;
            var sTxt = starBar.GetComponent<Text>();
            sTxt.text      = new string('★', starCount);
            sTxt.fontSize  = 12;
            sTxt.alignment = TextAnchor.MiddleCenter;
            sTxt.color     = new Color(1f, 0.85f, 0.2f);
        }

        return btn;
    }

    void BuildMapBottomBar(Transform parent)
    {
        var bar = new GameObject("MapBottomBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 52f);

        var barBg = bar.AddComponent<Image>();
        barBg.color = new Color(0.08f, 0.06f, 0.02f, 0.85f);

        // ★ 进度 "24/24"
        progressLabel = CreateTextGO(bar.transform, "Progress", "24/24",
            20, TextAnchor.MiddleLeft, new Vector2(10, 0), new Vector2(80, 36));
        progressLabel.color = new Color(1f, 0.85f, 0.2f);

        // 进度条
        var fillBg = CreateImg(bar.transform, "ProgressBarBg",
            new Color(0.15f, 0.12f, 0.05f, 1f));
        var fBgRt = fillBg.rectTransform;
        fBgRt.anchorMin = new Vector2(0, 0.5f);
        fBgRt.anchorMax = new Vector2(0, 0.5f);
        fBgRt.pivot     = new Vector2(0, 0.5f);
        fBgRt.anchoredPosition = new Vector2(96, 0);
        fBgRt.sizeDelta = new Vector2(160, 14);

        var fillImg = CreateImg(fillBg.transform, "Fill",
            new Color(0.9f, 0.75f, 0.2f, 1f));
        Stretch(fillImg.rectTransform);
        progressFill = fillImg;

        // 章节奖励 / 冒险日志按钮
        chapterRewardBtn = CreateSmallBtn(bar.transform, "ChapterReward", "章节奖励",
            new Vector2(-140, 0));
        adventureLogBtn  = CreateSmallBtn(bar.transform, "AdventureLog",  "冒险日志",
            new Vector2(-30,  0));
    }

    Button CreateSmallBtn(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot     = new Vector2(1, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(100, 40);
        go.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.10f, 1f);

        var t = CreateTextGO(go.transform, "Lbl", label, 17, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        t.color = new Color(0.9f, 0.82f, 0.6f);
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    // ── 关卡详情面板 ──
    void BuildDetailPanel(Transform parent, float mapH)
    {
        var panel = new GameObject("DetailPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, DETAIL_H);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.13f, 0.06f, 0.98f);

        // ── 上半：关卡名 + 描述 + 敌人图标 ──
        float y = -14f;

        stageNameLabel = CreateTextGO(panel.transform, "StageName",
            "1-3  哥布林营地", DetailNameFontSize, TextAnchor.MiddleLeft,
            new Vector2(16, y - 12), new Vector2(400, 32));
        stageNameLabel.color = new Color(1f, 0.92f, 0.7f);

        stageDescLabel = CreateTextGO(panel.transform, "StageDesc",
            "哥布林们在山洞前搭建了营地，\n小心他们的埋伏和投掷的石块。",
            DetailDescFontSize, TextAnchor.UpperLeft,
            new Vector2(16, y - 52), new Vector2(280, 56));
        stageDescLabel.color = new Color(0.85f, 0.78f, 0.65f);

        // 敌人图标行（占位：4个）
        var enemyCont = new GameObject("EnemyIcons", typeof(RectTransform));
        enemyCont.transform.SetParent(panel.transform, false);
        var eRt = enemyCont.GetComponent<RectTransform>();
        eRt.anchorMin = new Vector2(0, 1);
        eRt.anchorMax = new Vector2(0, 1);
        eRt.pivot     = new Vector2(0, 1);
        eRt.anchoredPosition = new Vector2(16, y - 112);
        eRt.sizeDelta = new Vector2(220, 48);
        enemyIconContainer = enemyCont.transform;
        for (int i = 0; i < 4; i++) BuildIconSlot(enemyCont.transform, i, 52f);
        // 可选：用你替换的敌人 Sprite 填充占位格
        if (enemySprites != null)
        {
            for (int i = 0; i < 4 && i < enemySprites.Length; i++)
            {
                var slot = enemyCont.transform.Find($"Icon{i}")?.GetComponent<Image>();
                if (slot != null && enemySprites[i] != null)
                {
                    slot.sprite = enemySprites[i];
                    slot.color = Color.white;
                }
            }
        }

        // 可能掉落标题（字号与「敌人信息」一致，见 DetailSubTitleFontSize）
        dropTitleLabel = CreateTextGO(panel.transform, "DropTitle",
            "可能掉落", DetailSubTitleFontSize, TextAnchor.MiddleLeft,
            new Vector2(310, y - 14), new Vector2(100, 28));
        dropTitleLabel.color = new Color(0.85f, 0.78f, 0.65f);

        // 掉落图标（占位：4个）
        var dropCont = new GameObject("DropIcons", typeof(RectTransform));
        dropCont.transform.SetParent(panel.transform, false);
        var dRt = dropCont.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(1, 1);
        dRt.anchorMax = new Vector2(1, 1);
        dRt.pivot     = new Vector2(1, 1);
        dRt.anchoredPosition = new Vector2(-16, y - 44);
        dRt.sizeDelta = new Vector2(220, 48);
        dropIconContainer = dropCont.transform;
        for (int i = 0; i < 4; i++) BuildIconSlot(dropCont.transform, i, 52f);
        // 可选：用你替换的掉落 Sprite 填充占位格
        if (dropSprites != null)
        {
            for (int i = 0; i < 4 && i < dropSprites.Length; i++)
            {
                var slot = dropCont.transform.Find($"Icon{i}")?.GetComponent<Image>();
                if (slot != null && dropSprites[i] != null)
                {
                    slot.sprite = dropSprites[i];
                    slot.color = Color.white;
                }
            }
        }

        // ── 中：体力 / 次数 ──
        float midY = y - 172;
        staminaCostLabel = CreateTextGO(panel.transform, "StaminaCost",
            "⚡ 10", 20, TextAnchor.MiddleLeft,
            new Vector2(16, midY), new Vector2(150, 30));
        staminaCostLabel.color = new Color(0.9f, 0.85f, 0.3f);

        remainChancesLabel = CreateTextGO(panel.transform, "Chances",
            "3/3", 20, TextAnchor.MiddleLeft,
            new Vector2(230, midY), new Vector2(80, 30));
        remainChancesLabel.color = Color.white;

        addChancesBtn = CreateSmallBtn2(panel.transform, "AddChances", "+",
            new Vector2(310, midY - 14), new Vector2(36, 30));

        // ── 难度选择行 ──
        float diffY = midY - 46;
        Color[] diffCols = { ColNormal, ColHard, ColNight };
        float[] diffX    = { 0, 1f/4, 2f/4, 3f/4 };
        for (int i = 0; i < GameConfig.DIFF_COUNT; i++)
        {
            int idx = i;
            var dBtn = BuildDiffBtn(panel.transform, GameConfig.GetDifficultyName(i), diffCols[i], i, diffY);
            difficultyButtons[i] = dBtn;
            var lbls = dBtn.GetComponentsInChildren<Text>(true);
            if (lbls.Length > 0) difficultyLabels[i] = lbls[0];
            dBtn.onClick.AddListener(() => OnSelectDiff(idx));
        }

        // ── 底部：开始冒险 + 扫荡 ──
        float btnY = -DETAIL_H + 60f;
        startBtn = BuildWideBtn(panel.transform, "StartBtn", "⚔  开始冒险",
            new Vector2(0, btnY + 24), new Vector2(-240, 56), ColNormal);
        sweepBtn = BuildWideBtn(panel.transform, "SweepBtn", "▶▶  扫荡",
            new Vector2(0, btnY + 24), new Vector2(110, 56), ColHard);

        // 字号 / 溢出与预制体绑定路径同一出口（见 ApplyDetailTextStyle）
        ApplyDetailTextStyle();
    }

    void BuildIconSlot(Transform parent, int idx, float size)
    {
        var go = new GameObject($"Icon{idx}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot     = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(idx * (size + 6), 0);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.10f, 1f);
    }

    Button BuildDiffBtn(Transform parent, string label, Color col, int idx, float y)
    {
        var go = new GameObject("Diff_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(idx / 4f,          0);
        rt.anchorMax = new Vector2((idx + 1) / 4f,    0);
        rt.pivot     = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(-8, 42);
        go.GetComponent<Image>().color = col;

        var t = CreateTextGO(go.transform, "Lbl", label, 20, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    Button BuildWideBtn(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 sizeDelta, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (name == "StartBtn")
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0.6f, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(8, 12);
            rt.sizeDelta = new Vector2(-16, 56);
        }
        else
        {
            rt.anchorMin = new Vector2(0.6f, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(4, 12);
            rt.sizeDelta = new Vector2(-12, 56);
        }
        go.GetComponent<Image>().color = col;

        var t = CreateTextGO(go.transform, "Lbl", label, 24, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    Button CreateSmallBtn2(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.85f, 0.65f, 0.15f, 1f);

        var t = CreateTextGO(go.transform, "Lbl", label, 22, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
