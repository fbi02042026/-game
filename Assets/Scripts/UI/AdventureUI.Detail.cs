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
        if (stageNameLabel != null)
        {
            if (activity)
                stageNameLabel.text = _selectedActivityKind == ActivityDungeonKind.Badge ? "徽记副本"
                                    : _selectedActivityKind == ActivityDungeonKind.Scroll ? "卷轴副本"
                                    : _selectedActivityKind == ActivityDungeonKind.Gift ? "礼物副本"
                                    : "金币副本";
            else if (endless) stageNameLabel.text = "无限裂隙";
            else if (main)
            {
                // 【2026-10-24 主人拍板「冒险界面信息应该写1-1 不应该是第一章」】
                // 主线详情标题 = 当前关号（章-关，与岛上关卡编号同口径）
                // 【2026-10-29 主人拍板「给每个关卡加个名字」】编号后追加关名（StageNameDefs 唯一出口，
                // 关卡槽位牌仍只显示编号——牌面 125px 放不下）
                // 【2026-10-29 修「1-1 未通 1-2 已标领取后」】当前关 = 第一个未通关（精确查单关），
                // 整章通关时钳到最后一关显示；不再用「最大通关号 + 1」推断（跳关记录会标错关）。
                int chapterTotal = ChapterSubTotal(_selectedChapter, SaveSystem.Instance?.Data);
                int cur = Mathf.Clamp(FirstUnclearedStage(_selectedChapter, chapterTotal, SaveSystem.Instance?.Data, _selectedDiff), 1, chapterTotal);
                string stageName = StageNameDefs.Get(_selectedChapter, cur);
                stageNameLabel.text = string.IsNullOrEmpty(stageName)
                    ? ActProgressionDefs.GlobalStageLabel(_selectedChapter, cur - 1)
                    : $"{ActProgressionDefs.GlobalStageLabel(_selectedChapter, cur - 1)}  {stageName}";
            }
            else stageNameLabel.text = ModeLabel(_selectedMode);
        }
        if (stageDescLabel != null)
        {
            if (activity)
            {
                // 【2026-10-23 拍板「活动副本一拆二」+ 2026-10-24 主人拍板】
                // 金币=周一/三/六；徽记=周二/四/日；卷轴=周五（通关第 3 章开启）；
                // 三本每日各 2 次，金币/徽记每次 10 体力、卷轴 15 体力；
                // 产出只按难度定死不跟章节：金 500/800/1200、徽记按难度掷档、
                // 卷轴 普通3-5普通/困难+0-1高级/噩梦1-3高级（2026-10-24 主人拍板）
                int gold = GameConfig.GetGoldDungeonClearGold(_selectedChapter, _selectedDiff);
                stageDescLabel.text = _selectedActivityKind == ActivityDungeonKind.Badge
                    ? "通关获得随机职业徽记。每周二、四、日开放，每日 2 次。"
                    : _selectedActivityKind == ActivityDungeonKind.Scroll
                    ? "通关获得招募卷轴，难度越高出高级卷轴。每周五开放，每日 2 次。"
                    : _selectedActivityKind == ActivityDungeonKind.Gift
                    ? "通关获得好感礼物，难度越高掉得越多。每周六开放，每日 2 次。"
                    : $"怪物只掉金币。通关获得 {gold} 金币。每周一、三、六开放，每日 2 次。";
            }
            else if (endless)
            {
                // 无限裂隙详情：剩余次数 / 保存点 / 下次起始层（2026-10-17 主人拍板）
                // 【2026-11-02 主人拍板「每 10 层一个保存点」】文案点明存档粒度，否则玩家打完 5 层 BOSS
                // 会以为该存了却没存（小 BOSS 只当节奏爆点，不存档）。
                var data = SaveSystem.Instance?.Data;
                int left = EndlessRiftRules.TriesLeft(data);
                int checkpoint = EndlessRiftRules.CheckpointOf(data?.endlessRiftClearedStage ?? 0);
                stageDescLabel.text =
                    $"今日剩余 {left}/{GameConfig.ENDLESS_DAILY_TRIES} 次，每 {GameConfig.ENDLESS_SAVE_INTERVAL} 层存档。\n" +
                    $"保存点第 {checkpoint} 层，下次从第 {checkpoint + 1} 层开始。";
            }
            else if (main)
                stageDescLabel.text = GetChapterIntro(_selectedChapter);
            else
                stageDescLabel.text = $"{ModeLabel(_selectedMode)}即将开放。";
        }

        if (staminaCostLabel != null)
        {
            string cost = (endless ? StaminaSystem.ENDLESS_RIFT_COST
                         : activity ? ActivityDungeonRules.EntryStaminaCost(_selectedActivityKind)
                         : StaminaSystem.ADVENTURE_COST).ToString();
            if (_staminaCostText == null)
                _staminaCostText = staminaCostLabel.transform.Find("Cost")?.GetComponent<Text>();
            if (_staminaCostText != null)
            {
                // 【2026-10-24 主人新布局】消耗值独立在子节点 Cost（预制体默认 "X10"）：
                // 有数字原位替换（保留主人 "X" 前缀样式），没数字整写消耗值；父节点「消耗体力」标签不动
                string t = _staminaCostText.text;
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
                _staminaCostText.text = replaced ? sb.ToString() : cost;
            }
            else
                staminaCostLabel.text = "消耗体力  " + cost;   // 无 Cost 子节点（Build 自建 UI）走旧整写
        }

        if (remainChancesLabel != null)
            remainChancesLabel.text = FormatRemainChances(playable, main, activity);

        EnsureStaminaCostIcon();
        RefreshEnemyIcons();
        RefreshDropIcons();
        // 【2026-11-02 主人拍板】重置进度钮（只在无限裂隙详情显示，每周 1 次）
        RefreshEndlessResetButton(endless);
    }

    /// <summary>
    /// 「剩余次数」栏（DetailPanel/you/Chances）。
    /// 主线一关一次（2026-10-29 主人拍板「未通关和已通关」）：当前关没打过=未通关，
    /// 选中的章整章通关（当前关号被钳到最后一个已通关）=已通关；
    /// 金币/徽记/卷轴副本每日各 2 次（2026-10-24 主人拍板「都是2次」），显示今日真值 x/2。
    /// 无限裂隙（2026-11-10）：显示 TriesLeft 真值 x/2，不再硬编码 "0" —— 显示 0 却还能进本就是这么来的。
    /// 裂隙的次数只在打通一层时扣，见 EndlessRiftRules.ConsumeStageClear。
    /// </summary>
    string FormatRemainChances(bool playable, bool main, bool activity)
    {
        if (!playable) return "0";
        if (main)
        {
            // 面板标题永远指向「当前关 = 第一个未通关」（2026-10-29 修：精确查单关，不再用最大通关号推断）：
            // 标题那关已通关 ⇔ 该章全部通关（第一个未通关超出范围 = 整章通完，标题显示 1-5 且 5 是通关关）
            int chapterTotal = ChapterSubTotal(_selectedChapter, SaveSystem.Instance?.Data);
            int cur = FirstUnclearedStage(_selectedChapter, chapterTotal, SaveSystem.Instance?.Data, _selectedDiff);
            return cur > chapterTotal ? "已通关" : "未通关";
        }
        if (activity)
            return $"{ActivityDungeonRules.TriesLeft(SaveSystem.Instance?.Data, _selectedActivityKind)}/{ActivityDungeonRules.DAILY_TRIES}";
        if (IsEndlessMode(_selectedMode))
            return $"{EndlessRiftRules.TriesLeft(SaveSystem.Instance?.Data)}/{GameConfig.ENDLESS_DAILY_TRIES}";
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
        {
            // 活动副本（2026-10-24 主人拍板）：战斗场景固定题材章（金=森林/徽=亡灵/卷=洞穴），预览怪跟场景走
            int previewChapter = IsActivityMode(_selectedMode)
                ? ActivityDungeonRules.SceneChapter(_selectedActivityKind)
                : _selectedChapter;
            all = ConfigManager.Instance.GetChapterPreviewMonsters(previewChapter);
        }
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

    /// <summary>怪物头像加载唯一出口（2026-11-03 改 public：战斗内词条怪首次登场提示 UI 复用，不再另抄路径表）。</summary>
    public static Sprite LoadMonsterSprite(MonsterConfig cfg)
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
        if (cfg == null) return;
        // 【2026-11-11 主人拍板「注意排版 主要就是说明词条和弱点」】文案与排版权威搬到
        // MonsterTipContent 单一出口（与战斗内词条怪提示同一份内容）：战力速览 / 词条 / 弱点 三段，
        // 血量按章首实战值、弱点按战斗公式的物法分流如实写。高度由 MonsterTipPanel 按正文自动算。
        MonsterTipPanel.Show(transform,
            MonsterTipContent.Title(cfg),
            MonsterTipContent.BuildBody(cfg, _selectedChapter));
        PlaceTipAbove(iconAnchor);
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
        var rootRt = transform as RectTransform;
        var tipRt = MonsterTipPanel.RootRt;
        if (rootRt == null || tipRt == null || iconAnchor == null) return;

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
        // 【2026-11-03】弹窗构建/关闭统一走共享组件 MonsterTipPanel（与战斗内词条提示共用同一个）
        MonsterTipPanel.Hide();
    }
}
