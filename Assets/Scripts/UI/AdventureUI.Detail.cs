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
// 职责：关卡详情面板与怪物图鉴（详情文案刷新、敌人图标、怪物信息弹窗及定位）
public partial class AdventureUI
{

    void RefreshDetailPanel()
    {
        bool main = _selectedMode == 0;
        bool activity = IsActivityMode(_selectedMode);
        bool endless = IsEndlessMode(_selectedMode);
        bool playable = main || activity || endless;
        string title = GameConfig.GetChapterMapName(_selectedChapter);
        if (stageNameLabel != null)
        {
            if (activity) stageNameLabel.text = _selectedActivityBadge ? "徽记副本" : "金币副本";
            else if (endless) stageNameLabel.text = "无限裂隙";
            else if (main) stageNameLabel.text = title;
            else stageNameLabel.text = ModeLabel(_selectedMode);
        }
        if (stageDescLabel != null)
        {
            if (activity)
            {
                // 【2026-10-23 主人拍板「活动副本一拆二」】金币=周一三五固定金；徽记=周二四每日 2 次
                int gold = GameConfig.GetGoldDungeonClearGold(_selectedChapter, _selectedDiff);
                stageDescLabel.text = _selectedActivityBadge
                    ? "通关获得随机职业徽记。每周二、四开放，每日 2 次。"
                    : $"怪物只掉金币。通关获得 {gold} 金币。每周一、三、五开放。";
            }
            else if (endless)
            {
                // 无限裂隙详情：剩余次数 / 记录点 / 下次起始层（2026-10-17 主人拍板）
                var data = SaveSystem.Instance?.Data;
                int left = EndlessRiftRules.TriesLeft(data);
                int checkpoint = EndlessRiftRules.CheckpointOf(data?.endlessRiftClearedStage ?? 0);
                stageDescLabel.text = $"今日剩余 {left}/{GameConfig.ENDLESS_DAILY_TRIES} 次，记录点第 {checkpoint} 层，下次从第 {checkpoint + 1} 层开始。";
            }
            else if (main)
                stageDescLabel.text = GetChapterIntro(_selectedChapter);
            else
                stageDescLabel.text = $"{ModeLabel(_selectedMode)}即将开放。";
        }

        if (staminaCostLabel != null)
        {
            string cost = (endless ? StaminaSystem.ENDLESS_RIFT_COST : StaminaSystem.ADVENTURE_COST).ToString();
            string t = staminaCostLabel.text;
            if (!string.IsNullOrEmpty(t) && t.IndexOf("体力", System.StringComparison.Ordinal) >= 0)
            {
                var sb = new System.Text.StringBuilder();
                bool replaced = false;
                for (int i = 0; i < t.Length; i++)
                {
                    if (char.IsDigit(t[i]))
                    {
                        if (!replaced) { sb.Append(cost); replaced = true; }
                        while (i + 1 < t.Length && char.IsDigit(t[i + 1])) i++;
                    }
                    else sb.Append(t[i]);
                }
                staminaCostLabel.text = replaced ? sb.ToString() : ("消耗体力  " + cost);
            }
            else
                staminaCostLabel.text = "消耗体力  " + cost;
        }

        if (remainChancesLabel != null)
            remainChancesLabel.text = FormatRemainChances(playable, main, activity);

        EnsureStaminaCostIcon();
        RefreshEnemyIcons();
        RefreshDropIcons();
    }

    /// <summary>
    /// 「剩余次数」栏（DetailPanel/you/Chances）。
    /// 主线不限；金币副本不限次数（2026-10-23 主人只拍了徽记每日 2 次）；徽记副本显示今日真值 x/2。
    /// </summary>
    string FormatRemainChances(bool playable, bool main, bool activity)
    {
        if (!playable) return "0";
        if (main) return "不限";
        if (activity)
        {
            if (_selectedActivityBadge)
                return $"{BadgeDungeonRules.TriesLeft(SaveSystem.Instance?.Data)}/{BadgeDungeonRules.DAILY_TRIES}";
            return "不限";
        }
        return "0";
    }
    void EnsureStaminaCostIcon()
    {
        if (_staminaCostIcon == null && staminaCostLabel != null)
        {
            _staminaCostIcon = staminaCostLabel.transform.Find("tiliicon")?.GetComponent<Image>();
            if (_staminaCostIcon == null)
                _staminaCostIcon = staminaCostLabel.GetComponentInChildren<Image>(true);
        }
        if (_staminaCostIcon == null) return;
        var sp = LoadStaminaIconSprite();
        if (sp == null) return;
        _staminaCostIcon.sprite = sp;
        _staminaCostIcon.preserveAspect = true;
        _staminaCostIcon.color = Color.white;
    }

    void RefreshEnemyIcons()
    {
        _previewMonsters.Clear();
        if (enemyIconContainer == null) return;

        if (_bossTag == null)
        {
            var bt = enemyIconContainer.Find("BOSS");
            if (bt != null) _bossTag = bt.GetComponent<Text>();
        }

        var slots = new List<Transform>();
        for (int i = 0; i < enemyIconContainer.childCount; i++)
        {
            var c = enemyIconContainer.GetChild(i);
            if (c.name == "BOSS" || c.GetComponent<Text>() != null && c.name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            slots.Add(c);
        }

        List<MonsterConfig> all = null;
        if (IsModePlayable(_selectedMode) && ConfigManager.Instance != null)
            all = ConfigManager.Instance.GetChapterPreviewMonsters(_selectedChapter);
        if (all == null) all = new List<MonsterConfig>();

        int showCount = Mathf.Min(slots.Count, all.Count);
        for (int i = 0; i < showCount; i++)
            _previewMonsters.Add(all[i]);

        Transform bossSlot = null;
        for (int i = 0; i < slots.Count; i++)
        {
            bool on = i < _previewMonsters.Count;
            slots[i].gameObject.SetActive(on);
            if (!on) continue;
            var cfg = _previewMonsters[i];
            var portrait = FindPortraitImage(slots[i]);
            Sprite sp = LoadMonsterSprite(cfg);
            if (portrait != null && sp != null)
                LayoutEnemyPortrait(portrait, sp);
            if (cfg.isBoss) bossSlot = slots[i];
        }

        if (_bossTag != null)
        {
            bool hasBoss = bossSlot != null;
            _bossTag.gameObject.SetActive(hasBoss);
            if (hasBoss)
            {
                _bossTag.text = "BOSS";
                _bossTag.transform.SetParent(bossSlot, false);
                var rt = _bossTag.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -2f);
                rt.sizeDelta = new Vector2(0f, 24f);
            }
        }
    }

    static Image FindPortraitImage(Transform slot)
    {
        Image[] imgs = slot.GetComponentsInChildren<Image>(true);
        if (imgs == null || imgs.Length == 0) return null;
        if (imgs.Length >= 2) return imgs[imgs.Length - 1];
        return imgs[0];
    }

    static void LayoutEnemyPortrait(Image portrait, Sprite sp, float scale = 1.35f)
    {
        portrait.sprite = sp;
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        var prt = portrait.rectTransform;
        if (prt == null) return;
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = Vector2.zero;
        prt.localScale = new Vector3(scale, scale, 1f);
    }

    static Sprite LoadMonsterSprite(MonsterConfig cfg)
    {
        if (cfg == null) return null;
        int gameChapter = 1;
        int idCh = 0;
        if (!string.IsNullOrEmpty(cfg.id))
        {
            int us = cfg.id.IndexOf('_');
            if (us >= 0 && us + 1 < cfg.id.Length && int.TryParse(cfg.id.Substring(us + 1, 1), out int n))
                idCh = n;
        }
        // 怪物章号 → 游戏章
        for (int i = 1; i <= 8; i++)
        {
            if (GameConfig.GetMonsterChapter(i) == idCh) { gameChapter = i; break; }
        }
        int monsterChapter = idCh > 0 ? idCh : GameConfig.GetMonsterChapter(gameChapter);
        int spriteIndex = cfg.spriteIndex > 0 ? cfg.spriteIndex : 1;

        var loader = MonsterSpriteLoader.Instance;
        if (loader != null)
        {
            var sp = loader.LoadMonsterSprite(monsterChapter, spriteIndex - 1);
            if (sp != null) return sp;
        }

        string folder = null, prefix = null;
        switch (monsterChapter)
        {
            case 1: folder = "1 Forest"; prefix = "forest_1"; break;
            case 2: folder = "2 Undead"; prefix = "undead_2"; break;
            case 3: folder = "3 Jungle"; prefix = "jungle_3"; break;
            case 4: folder = "4 Field"; prefix = "field_4"; break;
            case 5: folder = "5 Sea"; prefix = "sea_5"; break;
            case 6: folder = "6 Cave"; prefix = "cave_6"; break;
            case 7: folder = "7 Devil"; prefix = "devil_7"; break;
            case 8: folder = "8 Ice"; prefix = "ice_8"; break;
        }
        if (folder == null) return null;
        string path = $"Config/MonsterSpriteRegistry/{folder}/{prefix}{spriteIndex:D2}";
        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void ShowMonsterTip(MonsterConfig cfg, RectTransform iconAnchor = null)
    {
        EnsureTip();
        if (_tipRoot == null || cfg == null) return;
        _tipRoot.SetActive(true);
        PlaceTipAbove(iconAnchor);
        if (_tipTitle != null) _tipTitle.text = string.IsNullOrEmpty(cfg.monsterName) ? cfg.id : cfg.monsterName;
        int mch = GameConfig.GetMonsterChapter(_selectedChapter);
        var style = MonsterAttackStyleTable.Get(mch, cfg.spriteIndex);
        string styleName = style == MonsterAttackStyle.Ranged ? "远程" : "近战";
        if (_tipBody != null)
        {
            // 图鉴要显示「实战血量」而非裸配置值：Monster.Init 会把 baseHp
            // 乘上章节系数与 MONSTER_HP_GLOBAL_MUL，这里跟着乘回来，避免面板写 78、实战只有 46。
            // 2026-10-05：难度新增「章内爬坡」后，图鉴是静态展示、没有"当前第几关"的概念，
            // 统一按**章首（章内第 1 关）**算 —— 也就是该章的基准血量。
            // ⚠ 倍率仍走唯一出口 GetStatScale，只是 stage 固定传 0，不在别处另算一遍。
            float hpScale = GameConfig.GetStatScale(_selectedChapter, 0) * GameConfig.MONSTER_HP_GLOBAL_MUL;
            float shownHp = cfg.baseHp * hpScale;
            _tipBody.text = cfg.isBoss
                ? $"BOSS\n攻击 {cfg.baseAttack:0}\n生命 {shownHp:0}\n{styleName}"
                : $"攻击 {cfg.baseAttack:0}\n生命 {shownHp:0}\n{styleName}";
        }
    }

    /// <summary>弹窗下沿离图标中心的距离（图标 52 见方，一半 26 + 8 的呼吸空隙）。</summary>
    const float TipRiseAboveIcon = 34f;

    /// <summary>
    /// 【2026-10-11 主人拍板】把怪物信息弹窗摆到「当前点击的敌人图标」正上方。
    /// 图标与弹窗在同一棵 Canvas 树里，直接换算本地坐标，不去猜预制体的锚点/尺寸；
    /// 再按界面矩形做一次钳制，免得靠边的那只怪把弹窗顶出屏幕。
    /// </summary>
    void PlaceTipAbove(RectTransform iconAnchor)
    {
        if (_tipRoot == null || iconAnchor == null) return;
        var rootRt = transform as RectTransform;
        var tipRt = _tipRoot.transform as RectTransform;
        if (rootRt == null || tipRt == null) return;

        Vector3 world = iconAnchor.TransformPoint(iconAnchor.rect.center);
        Vector2 local = rootRt.InverseTransformPoint(world);
        // anchor 取 (0,0) 时参考点是父左下角，而 InverseTransformPoint 以父 pivot 为原点 → 补回 pivot 偏移
        Vector2 pivotOffset = new Vector2(rootRt.pivot.x * rootRt.rect.width,
                                         rootRt.pivot.y * rootRt.rect.height);

        tipRt.anchorMin = tipRt.anchorMax = Vector2.zero;
        tipRt.pivot = new Vector2(0.5f, 0f);

        float x = local.x + pivotOffset.x;
        float y = local.y + pivotOffset.y + TipRiseAboveIcon;

        float w = rootRt.rect.width;
        float h = rootRt.rect.height;
        float halfW = tipRt.sizeDelta.x * 0.5f;
        float tipH = tipRt.sizeDelta.y;
        if (w > halfW * 2f) x = Mathf.Clamp(x, halfW + 8f, w - halfW - 8f);
        if (h > tipH) y = Mathf.Clamp(y, 8f, h - tipH - 8f);

        tipRt.anchoredPosition = new Vector2(x, y);
    }

    void HideTip()
    {
        if (_tipRoot != null) _tipRoot.SetActive(false);
    }

    void EnsureTip()
    {
        if (_tipRoot != null) return;
        // 【2026-10-11 主人拍板】弹窗要显示在「当前点击的敌人上面」——位置会落在 DetailPanel 之外，
        // 所以挂在界面根节点上（面板内会被面板边界/装饰压住）。页面整体隐藏时它会跟着隐藏。
        var host = transform;
        var go = new GameObject("MonsterTip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 140f);
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.92f);

        var titleGo = new GameObject("TipTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(go.transform, false);
        var tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1);
        tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -8);
        tr.sizeDelta = new Vector2(-16, 32);
        _tipTitle = titleGo.GetComponent<Text>();
        _tipTitle.fontSize = 22;
        _tipTitle.alignment = TextAnchor.MiddleCenter;
        _tipTitle.color = new Color(1f, 0.92f, 0.7f);
        _tipTitle.font = GameFonts.GetChinese();

        var bodyGo = new GameObject("TipBody", typeof(RectTransform), typeof(Text));
        bodyGo.transform.SetParent(go.transform, false);
        var br = bodyGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 1);
        br.offsetMin = new Vector2(12, 10);
        br.offsetMax = new Vector2(-12, -42);
        _tipBody = bodyGo.GetComponent<Text>();
        _tipBody.fontSize = 18;
        _tipBody.alignment = TextAnchor.UpperCenter;
        _tipBody.color = new Color(0.9f, 0.85f, 0.75f);
        _tipBody.font = GameFonts.GetChinese();

        var closeBtn = go.AddComponent<Button>();
        closeBtn.targetGraphic = go.GetComponent<Image>();
        closeBtn.onClick.AddListener(HideTip);

        _tipRoot = go;
        GameFonts.ApplyToHierarchy(go.transform);
        go.SetActive(false);
    }
}
