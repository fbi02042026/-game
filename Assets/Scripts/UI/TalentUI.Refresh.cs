using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：刷新与共享助手（RefreshAll/货币/左列/右列/汇总刷新，取天赋字典与行外观小工具）
public partial class TalentUI
{

    /// <summary>左列连线暗态颜色（不改主人贴图，只压亮度表达未点亮）。</summary>
    static readonly Color NodeLinkOffTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    /// <summary>刷新货币、节点状态、底部加成（读档后可再调）</summary>
    public void RefreshAll()
    {
        RefreshCurrency();
        RefreshLeft();
        RefreshRight();
        RefreshSummary();
        RefreshResetButton();
    }

    public void RefreshCurrency()
    {
        long gold = 0;
        int stone = 0;
        try
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
            {
                gold = SaveSystem.Instance.Data.totalGold;
                stone = SaveSystem.Instance.Data.talentPoints;
            }
        }
        catch { /* 编辑器预览无存档 */ }

        if (goldText != null) goldText.text = FormatCompact(gold);
        if (stoneText != null) stoneText.text = FormatCompact(stone);
    }

    void RefreshLeft()
    {
        var talents = GetTalents();
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        int nextCost = 0;
        if (unlocked < TalentDefs.Left.Length)
            nextCost = TalentSystem.GetLeftGoldCost(TalentDefs.Left[unlocked], talents);

        for (int i = 0; i < _leftViews.Count; i++)
        {
            var v = _leftViews[i];
            var def = TalentDefs.Left[i];
            bool on = i < unlocked;
            bool can = TalentSystem.IsLeftUpgradeable(i, talents);
            bool locked = !on && !can;

            if (v.nameText != null) v.nameText.text = def.name;
            // 【2026-11-19】数值角标：只显示数值（+15 / +4%），不再整句「生命 +15」
            if (v.effectText != null) v.effectText.text = ValueBadgeOf(def.effect.display);
            if (v.icon != null)
            {
                var sp = TalentIcons.GetLeftAttr(i % 4);
                if (sp != null) ApplySprite(v.icon, sp, true);
                v.icon.preserveAspect = true;
                v.icon.type = Image.Type.Simple;
                v.icon.color = locked ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }
            if (v.check != null)
            {
                // Check 仅作可升级提示；已解锁/锁定时隐藏
                v.check.gameObject.SetActive(can);
                if (can) ApplySprite(v.check, sprCheckOn, true);
            }
            if (v.upgradeButton != null)
            {
                // 【2026-11-19 主人拍板】图标上不再叠「升级」按钮与金币价格牌——解锁入口只有
                // 「点图标 → 弹窗里点解锁」。绑定与字段保留（GetLeftNode 引导仍可用），这里统一隐藏。
                v.upgradeButton.SetActive(false);
            }
            // 锁定节点也保持可点（与右列同口径）：点了弹气泡卡看效果与花费缺口，不「点不动又没说法」。
            if (v.button != null) v.button.interactable = true;
            // 节点始终显示；仅灰态表示锁定
            if (v.root != null) v.root.SetActive(true);
            SetRowGray(v.root, locked);
            // 左列行右缘超出视口被 Mask 裁（左内边距 51 + 行宽 258.6 = 309.6 > 视口 302），
            // 默认贴角 (-12,-12) 的红点右侧约 11px 会被裁掉 —— 左列红点内收到 (-26,-12)，
            // 右缘 299 距裁切线留 3px。右列行居中不越界，仍走默认贴角。
            // 【2026-11-19 正方形格子】红点贴图标右上角（图标居中半径48、格子半宽58 → 右上角内缩约10）。
            // 【2026-11-19 主人拍板】红点离图标再近 2px：偏移 -12→-14（anchor 右上、负得越多越往图标靠）。
            SetRowRedDot(v.root, ref v.redDot, can, new Vector2(-14f, -14f));
            // 【2026-11-20 主人拍板】连线跟随蛇形点亮：横线连视觉左邻居、竖线只画在每行末端，
            // 拼成一条「左→右、右→左……」的连贯路径；顶行上方无节点才藏。详见下方两处判断。
            int row = i / TalentDefs.LeftPerRow;
            int col = i % TalentDefs.LeftPerRow;
            bool hasNodeAbove = i + TalentDefs.LeftPerRow < _leftViews.Count;
            if (v.line != null)
            {
                // 【2026-11-20 主人拍板】蛇形后奇数行视觉左右镜像：横线画在视觉非左端
                //（偶数行 data col>0 / 奇数行 data col<末列），几何仍按节点自身算，连左邻居。
                bool showH = (row % 2 == 0) ? (col > 0) : (col < TalentDefs.LeftPerRow - 1);
                v.line.gameObject.SetActive(showH);
                if (showH) v.line.color = on ? Color.white : NodeLinkOffTint;
            }
            if (v.lineV != null)
            {
                // 【2026-11-20 主人拍板】竖线统一画在每行 data 末列：蛇形后它恰是偶数行视觉右端 /
                // 奇数行视觉左端，正好把「上一行末端 → 下一行首端」接成一条连贯蛇形。
                bool showV = hasNodeAbove && col == TalentDefs.LeftPerRow - 1;
                v.lineV.gameObject.SetActive(showV);
                if (showV) v.lineV.color = on ? Color.white : NodeLinkOffTint;
            }
        }

        if (leftCostText != null)
            leftCostText.text = nextCost > 0 ? nextCost.ToString() : "0";
        if (leftTipText != null)
            leftTipText.text = "消耗金币解锁属性天赋";

        // GoldPanel 的 Plus：仅当前有可升级左天赋时显示
        var goldPlus = transform.Find("Panel/ResourceRow/GoldPanel/PlusButton");
        if (goldPlus != null)
            goldPlus.gameObject.SetActive(nextCost > 0 && TalentSystem.IsLeftUpgradeable(unlocked, talents));
    }

    void RefreshRight()
    {
        var talents = GetTalents();
        int playerLv = LevelSystem.CurrentLevel;   // 【2026-11-05 拍板】右列门槛/上限都看人物等级，与左列解耦

        for (int i = 0; i < _rightViews.Count; i++)
            RefreshOneRightNode(_rightViews[i], talents, playerLv);

        // 【2026-11-11 打开后灭】+【2026-11-14 能升级也亮、角色入口同步】：
        // 入口红点统一收口到 TalentSystem.RefreshTalentEntryRedDot（天赋 + 角色两个 key 一起刷）。
        var dotData = SaveSystem.Instance?.Data;
        TalentSystem.RefreshTalentEntryRedDot(dotData);

        if (rightTipText != null)
            rightTipText.text = "消耗天赋石解锁/升级天赋（分批开放）";
        if (rightCostValueText != null)
            rightCostValueText.text = "";
    }

    void RefreshOneRightNode(RightNodeView v, Dictionary<string, int> talents, int playerLv)
    {
        var node = v.node;
        if (node == null) return;

        // 【2026-10-18 主人拍板】物理/魔法专精合并行（二选一）：状态/置灰/红点/「新」走专门刷新
        if (v.node2 != null)
        {
            RefreshMutexPairRow(v, talents, playerLv);
            return;
        }

        var data = SaveSystem.Instance?.Data;
        int level = TalentDefs.GetRightNodeLevel(talents, node.id);
        bool unlocked = level > 0;
        bool mutexLocked = !unlocked && TalentDefs.IsRightNodeMutexLocked(node, talents);
        // 【2026-11-05 拍板】门槛/上限 = 人物等级（旧口径 = 左列已点数，已废除）
        bool thresholdLocked = !unlocked && playerLv < node.unlockLeftIndex;
        int cap = TalentDefs.RightNodeLevelCap(node, playerLv);

        string status;
        bool canAct = false;
        if (unlocked && level >= node.maxLevel)
            status = "MAX";
        else if (unlocked)
        {
            // 任务3：等级上限受左列已点数量限制
            if (level >= cap)
                // 【2026-11-05 拍板】门槛改人物等级 → 直接说升到几级能升
                status = "升到" + TalentDefs.LevelNeededForNextLevel(node, level) + "级可升级";
            else
            {
                int cost = node.costByLevel != null && level < node.costByLevel.Length ? node.costByLevel[level] : 0;
                status = "升级 " + cost;
                canAct = data != null && data.talentPoints >= cost;
            }
        }
        else if (thresholdLocked)
            status = "Lv" + node.unlockLeftIndex + " 解锁";   // 【2026-11-05 拍板】门槛 = 人物等级
        else if (mutexLocked)
            status = "互斥";                            // 任务4：互斥置灰
        else
        {
            int cost = node.costByLevel != null && node.costByLevel.Length > 0 ? node.costByLevel[0] : 0;
            status = "解锁 " + cost;
            canAct = data != null && data.talentPoints >= cost;
        }

        if (v.titleText != null)
            v.titleText.text = node.name + (unlocked ? "  Lv" + level : "");
        if (v.costText != null)
            v.costText.text = status;

        bool locked = !unlocked && (thresholdLocked || mutexLocked);
        EnsureRightLock(v);
        if (v.lockIcon != null)
        {
            v.lockIcon.gameObject.SetActive(locked);
            if (locked) ApplyLockSprite(v.lockIcon);
        }
        if (v.diamond != null)
            ApplySprite(v.diamond, unlocked ? sprDiamondOn : sprDiamondOff, true);
        // 【2026-10-13 主人拍板】右列小圆点的连线要「连满」：每行两段（冲上 / 冲下），
        // 最上一行没得连 → 隐藏冲上段；**最下一行**没得连 → 隐藏冲下段
        //（主人原话「最下面的第一个不用有冲下的线」）。
        // ⚠ 行序真源 = VerticalLayoutGroup.reverseArrangement（index 0 在**视觉最底**），别按「0 在最上」猜。
        bool showUp = v.visualIndex < _rightViews.Count - 1;
        bool showDown = v.visualIndex > 0;
        if (v.lineUp != null)
        {
            v.lineUp.gameObject.SetActive(showUp);
            if (showUp) ApplySprite(v.lineUp, unlocked ? sprRightLinkOn : sprRightLinkOff, false);
        }
        if (v.lineDown != null)
        {
            v.lineDown.gameObject.SetActive(showDown);
            if (showDown) ApplySprite(v.lineDown, unlocked ? sprRightLinkOn : sprRightLinkOff, false);
        }

        if (v.root != null) v.root.SetActive(true);
        SetRowGray(v.root, locked ? 0.72f : 1f);
        SetRowRedDot(v.root, ref v.redDot, false);   // 【2026-11-19 主人拍板】右列不要红点（节点保留，恒隐藏）
        // 【2026-11-19 主人拍板】右列红色「新」标记也退场（截图里右上角的红点即它）；节点保留恒隐藏。
        SetRowNewTag(v.root, ref v.newTag, false);
        // 【2026-10-17 主人要求】锁定行也保持可点：点了弹具体开启条件（等级达到几级 / 互斥），
        // 不再「点不动又没说法」。灰态与锁图标照旧 —— 灰度走 CanvasGroup，不依赖按钮禁用色。
        if (v.button != null) v.button.interactable = true;
    }

    void ApplyLockSprite(Image img)
    {
        if (img == null || img.sprite != null) return;
        var lockSp = sprLock ?? Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (lockSp == null)
                lockSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
        }
#endif
        if (lockSp != null) ApplySprite(img, lockSp, true);
    }

    /// <summary>
    /// 【2026-11-19 主人拍板】重置天赋退场，Footer 重置按钮位换成「一键升级」，本函数改为刷一键升级的亮/灰：
    /// 亮 = 左列能买或右列能解锁/升级（真源 HasBuyAllTarget）；灰 = 两边都买不动，仍可点，
    /// 点了走 OnClickBuyAll 弹具体原因（金币不够 / 天赋石不足 / 等级不够 / 已点满）。函数名保留（调用点只有 RefreshAll）。
    /// 药水计数节点（Footer/icon）随重置退场已在预制体隐藏，补绑写值降级为无害兜底（找不到不报错）。
    /// </summary>
    void RefreshResetButton()
    {
        var data = SaveSystem.Instance?.Data;
        if (resetPotionCountText == null)
            resetPotionCountText = FindTxt("Panel/Footer/Icon/数量")
                                   ?? FindTxt("Panel/Footer/icon/数量");
        if (resetPotionCountText != null)
            resetPotionCountText.text = "×" + (data != null ? data.TownBagCount(TalentSystem.ItemForgetPotion) : 0);

        // 【2026-11-20 主人拍板】一键升级含右列：左列能买或右列能解锁/升级都算亮；
        // 真值共用 HasBuyAllTarget（内部复用 TalentSystem 快照判定），不在这里另写一套。
        bool canBuy = HasBuyAllTarget(data);
        // 【2026-11-20 主人拍板】亮/灰两个按钮已合并为单个 shengji：底图用预制体自带的
        //（主人已在 Inspector 换好），代码不再按路径覆盖；只按「能不能买」压暗颜色表达灰态。
        // 按钮恒显示、恒可点：灰态点进去由 OnClickBuyAll 弹具体原因（金币不够 / 天赋石不足 / 等级不够 / 已点满）。
        if (resetButton != null)
        {
            var img = resetButton.GetComponent<Image>();
            if (img != null)
                img.color = canBuy ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            resetButton.gameObject.SetActive(true);
            resetButton.interactable = true;
        }
    }

    void RefreshSummary()
    {
        float atk = 0, mag = 0, hp = 0, def = 0, crit = 0, spd = 0;
        var talents = GetTalents();
        if (talents != null)
        {
            int leftN = TalentDefs.LeftUnlockedCount(talents);
            for (int i = 0; i < leftN; i++)
            {
                var e = TalentDefs.Left[i].effect;
                switch (e.kind)
                {
                    // 2026-10-10：左列第 4 槽已改走 Attack；攻击类节点同时增加 Attack 和 MagicAttack，
                    // 与 CharacterUI 同口径：同一份天赋值同时累加进 atk 与 mag，面板两侧都显示 +N。
                    case TalentDefs.AttrKind.Attack: atk += e.value; mag += e.value; break;
                    case TalentDefs.AttrKind.Hp: hp += e.value; break;
                    case TalentDefs.AttrKind.Defense: def += e.value; break;
                    case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                    case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
                }
            }
            // 右列节点：攻击/生命/防御/暴击/攻速 计入面板汇总
            for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
            {
                var node = TalentDefs.RightNodes[i];
                int lv = TalentDefs.GetRightNodeLevel(talents, node.id);
                if (lv <= 0) continue;
                int job = TalentDefs.GetRightNodeChosenJob(talents, node.id);
                var opt = node.IsJobChoice ? node.ChosenOption(job)
                                           : (node.options != null && node.options.Length > 0 ? node.options[0] : null);
                if (opt == null) continue;
                switch (opt.kind)
                {
                    // 右列喂**战斗属性**；与 CharacterUI 同口径、与 AttrSystem.ApplyTalentEffect 同口径：
                    // AttrKind.Attack 是「固定值攻击」，物攻 / 魔攻**两侧都加**（与 Defense 同口径，左右列一致）。
                    // 区分度在 PhysDamage / MagicDamage 专精节点上，不在 Attack。
                    case TalentDefs.AttrKind.Attack: atk += node.EffectValue(lv, job); mag += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.Hp: hp += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.Defense: def += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.CritRate: crit += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.AtkSpeed: spd += node.EffectValue(lv, job); break;
                }
            }
        }

        // 【2026-10-26 主人全文字版】底栏改纯文字后每格自带属性名，输出统一带前缀，
        // 覆盖格式与预制体占位（物攻+9999 / 血量+9999 …）一致；魔防与物防同源——
        // 天赋「防御」按双防同加口径生效（AttrSystem.ApplyTalentEffect Defense 分支）。
        if (sumAttackText != null) sumAttackText.text = "物攻+" + atk.ToString("0");
        if (sumMagicText != null) sumMagicText.text = "魔攻+" + mag.ToString("0");
        if (sumHpText != null) sumHpText.text = "血量+" + hp.ToString("0");
        if (sumDefText != null) sumDefText.text = "物防+" + def.ToString("0");
        if (sumMagicDefText != null) sumMagicDefText.text = "魔防+" + def.ToString("0");
        if (sumCritText != null) sumCritText.text = "暴击+" + crit.ToString("0.##") + "%";
        if (sumAtkSpdText != null) sumAtkSpdText.text = "攻速+" + spd.ToString("0.##") + "%";
    }

    static Dictionary<string, int> GetTalents()
    {
        try
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
                return SaveSystem.Instance.Data.talents;
        }
        catch (System.Exception ex)
        {
            // 读不到存档天赋 = 天赋面板全空（玩家点了像没点），必须留痕而不是静默吞
            Debug.LogWarning("[TalentUI] GetTalents 读存档失败: " + ex.Message);
        }
        return null;
    }

    /// <summary>当前存档天赋字典（对外只读入口，引导用来判断"点了没有"）。</summary>
    public static Dictionary<string, int> CurrentTalents()
    {
        return GetTalents();
    }

    /// <summary>已解锁的左栏节点数（0..TalentDefs.Left.Length）。</summary>
    public static int LeftUnlockedCount()
    {
        return TalentDefs.LeftUnlockedCount(GetTalents());
    }

    /// <summary>
    /// 取左栏第 index0 个节点的高亮目标（优先升级按钮，退回节点根）。
    /// 新手引导「回城点一次天赋」用；列表还没建好就返回 null，引导自己要能容错。
    /// </summary>
    public RectTransform GetLeftNode(int index0)
    {
        EnsureLists();
        if (index0 < 0 || index0 >= _leftViews.Count) return null;
        var v = _leftViews[index0];
        if (v == null) return null;
        if (v.upgradeButton != null)
        {
            var rt = v.upgradeButton.GetComponent<RectTransform>();
            if (rt != null && v.upgradeButton.gameObject.activeInHierarchy) return rt;
        }
        return v.root != null ? v.root.GetComponent<RectTransform>() : null;
    }

    /// <summary>界面当前是否打开（引导等待"玩家关掉天赋页"用）。</summary>
    public bool IsOpen => gameObject.activeInHierarchy;

    static string FormatCompact(long v)
    {
        // 2026-10-24 主人拍板：资源数字格式统一出口 = GuildHallUI.FormatResource（铁律 15 同一语义只留一个出口），
        // 不再在此另写一套。
        return GuildHallUI.FormatResource(v);
    }

    static void ApplySprite(Image img, Sprite sp, bool preserveAspect)
    {
        if (img == null || sp == null) return;
        img.sprite = sp;
        img.color = Color.white;
        img.preserveAspect = preserveAspect;
        img.type = Image.Type.Simple;
    }

    static void SetGraphicAlpha(GameObject go, float a)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = a;
    }

    static void SetRowGray(GameObject root, bool gray)
    {
        SetGraphicAlpha(root, gray ? 0.72f : 1f);
    }

    static void SetRowGray(GameObject root, float alpha)
    {
        SetGraphicAlpha(root, Mathf.Clamp01(alpha));
    }

    /// <param name="offset">红点相对行右上角的偏移；null = 默认贴角 (-12,-12)。
    /// 行右缘被视口 Mask 裁的列（当前是左列）传内收值，避免红点右半被裁。</param>
    static void SetRowRedDot(GameObject rowRoot, ref GameObject dot, bool show, Vector2? offset = null)
    {
        if (rowRoot == null) return;
        if (!show)
        {
            if (dot != null) dot.SetActive(false);
            return;
        }
        if (dot == null)
        {
            dot = new GameObject("UpgradeRedDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dot.transform.SetParent(rowRoot.transform, false);
            var rt = dot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RedDot.DefaultSize, RedDot.DefaultSize);
            rt.anchoredPosition = offset ?? new Vector2(-12f, -12f);
            var img = dot.GetComponent<Image>();
            img.sprite = RedDot.Sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
        else if (offset.HasValue)
        {
            // 已建过的红点也按调用方偏移校正（幂等），防止旧实例残留默认位置
            dot.GetComponent<RectTransform>().anchoredPosition = offset.Value;
        }
        dot.SetActive(true);
    }

    /// <summary>
    /// 2026-09-29：节点右上角红色「新」字（<see cref="RightNodeView.newTag"/>）。
    /// 位置刻意让开右上角那个「可点/可升级」小红点（-12,-12），故往左挪到 (-34,-12)。
    /// </summary>
    static void SetRowNewTag(GameObject rowRoot, ref Text tag, bool show)
    {
        if (rowRoot == null) return;
        if (!show)
        {
            if (tag != null) tag.gameObject.SetActive(false);
            return;
        }
        if (tag == null)
        {
            // 用现成的 CreateText：已带中文字体 / 居中 / 不挡射线
            tag = CreateText(rowRoot.transform, "NewTag", "新", 20, new Color(1f, 0.22f, 0.18f));
            var rt = tag.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 24f);
            rt.anchoredPosition = new Vector2(-34f, -12f);
        }
        tag.gameObject.SetActive(true);
    }
}
