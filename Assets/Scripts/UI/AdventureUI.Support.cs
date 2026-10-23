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
// 职责：支撑工具（模式/难度标签与解锁判定、内容锁定、Toast 飘字、AutoBind/画布配置/通用构建小件）
public partial class AdventureUI
{

    string ModeLabel(int i)
    {
        if (modeButtonLabels != null && i >= 0 && i < modeButtonLabels.Length && modeButtonLabels[i] != null
            && !string.IsNullOrEmpty(modeButtonLabels[i].text))
            return modeButtonLabels[i].text;
        if (i >= 0 && i < ModeNames.Length) return ModeNames[i];
        return "副本";
    }

    string DiffLabel(int i)
    {
        if (difficultyLabels != null && i >= 0 && i < difficultyLabels.Length && difficultyLabels[i] != null
            && !string.IsNullOrEmpty(difficultyLabels[i].text))
            return difficultyLabels[i].text;
        if (i >= 0 && i < GameConfig.DIFF_COUNT) return GameConfig.GetDifficultyName(i);
        return "难度";
    }

    static string GetChapterIntro(int chapter)
    {
        switch (chapter)
        {
            case 1: return "暮影森林外围，哥布林与野兽出没。击败章末首领即可开启下一章。";
            case 2: return "幽冥墓园中亡灵苏醒，小心成群的骷髅与法师。";
            case 3: return "翡翠秘境潮湿闷热，毒虫与密林伏兵环伺。";
            case 4: return "晨曦草原看似开阔，潜伏的猎手并不少。";
            case 5: return "海岛遗迹潮水起伏，带来陌生的深海生物。";
            case 6: return "巨岩深窟黑暗潮湿，洞穴生物成群结队。";
            case 7: return "赤焰炼狱热浪灼人，魔族精锐在此驻守。";
            case 8: return "永霜雪境天寒地冻，冰原霸主等待挑战者。";
            default: return "未知的冒险区域。";
        }
    }

    /// <summary>
    /// 该入口是否已开放。改问模式注册表，不再写 <c>mode == 0</c> 这种魔数。
    /// 以后开放金币本 / 无限关卡 / 世界boss，只改对应 <see cref="BattleMode"/> 的 IsPlayable。
    /// </summary>
    bool IsModePlayable(int mode) => BattleModes.FromSlot(mode).IsPlayable;

    /// <summary>
    /// 是否金币本。
    /// 以前是「按钮文字里有没有'活动'两个字」再配一个魔数 <c>mode == 4</c> —— 文案一改就判错，
    /// 而且判错是静默的（直接走错建关分支）。现在直接问模式本身。
    /// </summary>
    bool IsActivityMode(int mode) => BattleModes.FromSlot(mode).Id == BattleModeId.GoldDungeon;

    /// <summary>是否主线冒险。模式身份问模式表，不写魔数。</summary>
    bool IsMainMode(int mode) => BattleModes.FromSlot(mode).Id == BattleModeId.Normal;

    /// <summary>是否无限裂隙（下标 2，2026-10-17 开放）</summary>
    bool IsEndlessMode(int mode) => BattleModes.FromSlot(mode).Id == BattleModeId.Endless;

    static int GetClearedChapterCount()
    {
        // 章节分叉后 maxUnlockedChapter-1 不再等于通关数，一律读权威集合
        var data = SaveSystem.Instance?.Data;
        return data != null ? data.ClearedChapterCount() : 0;
    }

    static bool IsDiffUnlocked(int diff)
    {
        if (diff <= 0) return true;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (diff == 1) return GetClearedChapterCount() >= GameConfig.DIFF_HARD_NEED_CLEARS;
        // 噩梦：在困难（及以上）难度下通关过第 8 章
        if (diff == 2) return data.hardCleared;
        return false;
    }

    /// <summary>
    /// 难度解锁按模式分流（2026-10-23 主人拍板）：活动副本（金币/徽记）按「本标签已通难度」
    /// 逐级解锁（<see cref="ActivityDungeonRules.IsDiffUnlocked"/>）；其余模式走全局章节数口径。
    /// </summary>
    bool IsDiffUnlockedForSelectedMode(int diff)
        => IsActivityMode(_selectedMode)
            ? ActivityDungeonRules.IsDiffUnlocked(SaveSystem.Instance?.Data, diff)
            : IsDiffUnlocked(diff);

    void RefreshContentLock()
    {
        bool locked = !IsModePlayable(_selectedMode);
        ApplyCanvasLock(transform.Find("RightContent"), locked);
        var detail = transform.Find("DetailPanel");
        if (detail == null)
        {
            var right = transform.Find("RightContent");
            if (right != null) detail = right.Find("DetailPanel");
        }
        ApplyCanvasLock(detail, locked);
    }

    static void ApplyCanvasLock(Transform t, bool locked)
    {
        if (t == null) return;
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = locked ? 0.42f : 1f;
        cg.interactable = !locked;
        cg.blocksRaycasts = !locked;
    }

    static void SetGraphicDim(Transform root, bool dim)
    {
        if (root == null) return;
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            Color c = images[i].color;
            c.a = dim ? 0.4f : 1f;
            images[i].color = c;
        }
        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            Color c = texts[i].color;
            c.a = dim ? 0.45f : 1f;
            texts[i].color = c;
        }
    }

    void Toast(string msg)
    {
        Debug.Log("[AdventureUI] " + msg);
        ShowFloatToast(msg);
    }

    void ShowFloatToast(string msg)
    {
        EnsureFloatToast();
        if (_floatToast == null) return;
        if (_floatToastCo != null) StopCoroutine(_floatToastCo);
        _floatToastCo = StartCoroutine(CoFloatToast(msg));
    }

    void HideFloatToast()
    {
        if (_floatToastCo != null)
        {
            StopCoroutine(_floatToastCo);
            _floatToastCo = null;
        }
        if (_floatToast != null)
            _floatToast.gameObject.SetActive(false);
    }

    void EnsureFloatToast()
    {
        if (_floatToast != null) return;
        var go = new GameObject("FloatToast", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(transform, false);
        _floatToastRt = go.GetComponent<RectTransform>();
        _floatToastRt.anchorMin = new Vector2(0.5f, 0.5f);
        _floatToastRt.anchorMax = new Vector2(0.5f, 0.5f);
        _floatToastRt.pivot = new Vector2(0.5f, 0.5f);
        _floatToastRt.sizeDelta = new Vector2(560f, 80f);
        _floatToastRt.anchoredPosition = Vector2.zero;
        _floatToast = go.GetComponent<Text>();
        _floatToast.font = GameFonts.GetChinese();
        _floatToast.fontSize = 32;
        _floatToast.alignment = TextAnchor.MiddleCenter;
        _floatToast.color = new Color(1f, 0.94f, 0.72f, 1f);
        _floatToast.horizontalOverflow = HorizontalWrapMode.Overflow;
        _floatToast.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    IEnumerator CoFloatToast(string msg)
    {
        _floatToast.text = msg;
        _floatToast.gameObject.SetActive(true);
        _floatToast.transform.SetAsLastSibling();
        Color c = _floatToast.color;
        c.a = 1f;
        _floatToast.color = c;
        _floatToastRt.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(2f);

        const float dur = 0.65f;
        float t = 0f;
        Vector2 start = Vector2.zero;
        Vector2 end = new Vector2(0f, 120f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            _floatToastRt.anchoredPosition = Vector2.Lerp(start, end, u);
            c.a = 1f - u;
            _floatToast.color = c;
            yield return null;
        }

        _floatToast.gameObject.SetActive(false);
        _floatToastCo = null;
    }

    // ────────────────────────────────────────────────────
    // 工具方法
    // ────────────────────────────────────────────────────

    /// <summary>从预制体节点回填公开引用（生成后改过树也能尽量绑上）</summary>
    void AutoBindFromHierarchy()
    {
        var left = transform.Find("LeftSidebar");
        if (left != null)
        {
            for (int i = 0; i < 5; i++)
            {
                var btnT = left.Find($"ModeBtn_{i}");
                if (btnT == null) continue;
                var btn = btnT.GetComponent<Button>();
                if (modeButtons.Length > i) modeButtons[i] = btn;
                var label = btnT.Find("Label")?.GetComponent<Text>();
                if (modeButtonLabels.Length > i) modeButtonLabels[i] = label;
                var iconImg = btnT.Find("Icon")?.GetComponent<Image>();
                if (modeButtonIcons.Length > i && iconImg != null) modeButtonIcons[i] = iconImg;
            }

            // 模式标签字号统一出口（2026-10-22 主人拍板，见 ApplyModeLabelStyle）
            ApplyModeLabelStyle();
        }

        var right = transform.Find("RightContent");
        // 【2026-10-23 主人新布局】StageNodes 直接挂在界面根下（不再在 RightContent/MapRoot 下），
        // 背景/整图节点 = StageNodes/Node_1..8 + 无限裂隙 + 金币副本；RightContent 下是 主线/无限/活动副本 三个页面节点。
        if (stageNodeContainer == null)
            stageNodeContainer = transform.Find("StageNodes");
        var mapRoot = right != null ? right.Find("MapRoot") : null;   // 旧布局残留路径，prefab 已无 MapRoot，留作向后兼容
        if (mapRoot != null && stageNodeContainer == null)
            stageNodeContainer = mapRoot.Find("StageNodes");
        if (mapRoot != null)
        {
            if (mapBg == null) mapBg = mapRoot.Find("MapBg")?.GetComponent<Image>();
        }
        // 新布局没有 MapBg（背景 = StageNodes 各节点的 Image），mapBg 保持 null，相关刷新全部 null-safe 跳过。
        // 标题栏在「主线」节点下（2026-10-23 新布局）；活动/无限节点也有各自 TitleBar，绑定取主线那份。
        var titleBar = right != null ? right.Find(MainNodeName + "/TitleBar") : null;
        if (titleBar == null && mapRoot != null)
        {
            titleBar = mapRoot.Find("TitleBar");
        }
        if (titleBar != null)
        {
            if (chapterTitle == null) chapterTitle = titleBar.Find("ChapterTitle")?.GetComponent<Text>();
            if (prevChapterBtn == null) prevChapterBtn = titleBar.Find("PrevBtn")?.GetComponent<Button>();
            if (nextChapterBtn == null) nextChapterBtn = titleBar.Find("NextBtn")?.GetComponent<Button>();
        }

        var detail = transform.Find("DetailPanel");
        if (detail == null && right != null) detail = right.Find("DetailPanel");
        if (detail == null) return;
        stageNameLabel = detail.Find("StageName")?.GetComponent<Text>();
        stageDescLabel = detail.Find("StageDesc")?.GetComponent<Text>();
        dropTitleLabel = detail.Find("DropTitle")?.GetComponent<Text>();
        ApplyDetailTextStyle();
        enemyIconContainer = detail.Find("EnemyIcons");
        dropIconContainer = detail.Find("DropIcons");
        staminaCostLabel = detail.Find("StaminaCost")?.GetComponent<Text>();
        remainChancesLabel = detail.Find("Chances")?.GetComponent<Text>();
        addChancesBtn = detail.Find("AddChances")?.GetComponent<Button>();
        startBtn = detail.Find("StartBtn")?.GetComponent<Button>();
        sweepBtn = detail.Find("SweepBtn")?.GetComponent<Button>();
        for (int i = 0; i < 4; i++)
        {
            string[] names = { "Diff_普通", "Diff_困难", "Diff_噩梦", "Diff_地狱" };
            var d = detail.Find(names[i]);
            if (d == null) continue;
            // 2026-09-17：地狱已删除；预制体里若还留着「Diff_地狱」节点就隐藏，避免界面上多出一个按钮
            if (i >= GameConfig.DIFF_COUNT)
            {
                d.gameObject.SetActive(false);
                continue;
            }
            if (difficultyButtons.Length > i) difficultyButtons[i] = d.GetComponent<Button>();
            if (difficultyLabels.Length > i) difficultyLabels[i] = d.Find("Lbl")?.GetComponent<Text>();
        }
    }

    /// <summary>
    /// 关卡详情文字样式（字号 / 溢出）的唯一出口 —— Build 与 AutoBind 两条路径都走这里。
    /// 【2026-10-11 主人拍板】章节名 27 / 介绍 21 / 小标题 25；介绍框溢出改为可见，长句不再被吃掉后半截。
    /// 【2026-10-19 主人改排版】介绍框宽度**跟随预制体**（主人已把信息框边界拉大），
    /// 不再在运行时把宽度压回 340 —— 代码只管字号和溢出，排版以预制体为准。
    /// </summary>
    void ApplyDetailTextStyle()
    {
        if (stageNameLabel != null)
            stageNameLabel.fontSize = DetailNameFontSize;
        if (stageDescLabel != null)
        {
            stageDescLabel.fontSize = DetailDescFontSize;
            stageDescLabel.verticalOverflow = VerticalWrapMode.Overflow;
        }
        // 「可能掉落」字号对齐「敌人信息」；关掉换行避免折成两行
        if (dropTitleLabel != null)
        {
            dropTitleLabel.fontSize = DetailSubTitleFontSize;
            dropTitleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    /// <summary>
    /// 左侧模式按钮标签字号（2026-10-22 主人拍板：主线/无限标签有点小 → 30）。
    /// 只动字号；按钮宽高/位置以预制体为准（铁律 32）。
    /// 30 号下「主线冒险」4 字基本贴满标签框，关掉折行防止溢出时断成两行。
    /// </summary>
    void ApplyModeLabelStyle()
    {
        if (modeButtonLabels == null) return;
        for (int i = 0; i < modeButtonLabels.Length; i++)
        {
            var lbl = modeButtonLabels[i];
            if (lbl == null) continue;
            lbl.fontSize = ModeLabelFontSize;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    void EnsureVisibleTransform()
    {
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
    }

    bool _canvasConfigured;

    void ConfigureHostCanvasOnce()
    {
        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        TownPageCanvas.Configure(gameObject, 5, stripCanvasWhenNested: true);
        // 页根自铺（与 CharacterUI.Stage.ConfigureHostCanvasOnce 同一修法）：
        // AdventureUI.prefab 根节点 RectTransform 序列化为 anchor(0,0)/(0,0)、sizeDelta(0,0)、scale(0,0,0)，
        // 过去靠本页自带 Canvas 走 UICanvasSetup.Apply 时被 EnsureRootStretch 拉满全屏；
        // 嵌在大厅下时 TownPageCanvas.Configure 的 nested 分支会把这个 Canvas 销毁（改由大厅 Canvas 驱动），
        // 之后没人铺页根 ⇒ 整页缩成 0×0、子节点全挤到画布原点
        //（表现：章节地图缩在屏幕左下角、右侧/上方露出大厅、「开始冒险」按钮被屏幕左缘切掉一半）。
        // 这里自己补一次拉伸：只定页根的边框，不碰任何子节点坐标，删掉本行即可一键回退。
        UICanvasSetup.EnsureRootStretch(transform as RectTransform);
        _canvasConfigured = true;
    }

    /// <summary>未挂在大厅下时，才在本页挂一份资源条+底栏（预制体/独立打开）。</summary>
    void EnsureStandaloneChrome(Transform hall)
    {
        var nested = GetComponentInParent<GuildHallUI>();
        if (nested != null && nested.gameObject != gameObject) return;
        TownSharedChrome.EnsureResourceBar(transform, hall);
        TownSharedChrome.EnsureBottomNav(transform, hall, MainNavTab.Adventure);
    }

    static Image CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Text CreateTextGO(Transform parent, string name, string content,
        int size, TextAnchor align, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.text      = content;
        t.fontSize  = size;
        t.alignment = align;
        t.color     = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        return t;
    }
}
