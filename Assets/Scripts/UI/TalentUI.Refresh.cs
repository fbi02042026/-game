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
            if (v.effectText != null) v.effectText.text = def.effect.display;
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
                // 【2026-10-18 修·主人报「到第10个节点不显示按钮」】金币不足时 can=false 把按钮整个藏了。
                // 改为：下一个待解锁节点（i == unlocked）按钮始终显示并始终标花费；
                // 金币不足时点击有 OnClickLeft 的「金币不足」提示兜底，不需要靠藏按钮表达。
                bool showBtn = can || i == unlocked;
                v.upgradeButton.SetActive(showBtn);
                if (showBtn && v.upgradeCostText != null && nextCost > 0 && i == unlocked)
                    v.upgradeCostText.text = nextCost.ToString();
            }
            if (v.button != null) v.button.interactable = can;
            // 节点始终显示；仅灰态表示锁定
            if (v.root != null) v.root.SetActive(true);
            SetRowGray(v.root, locked);
            SetRowRedDot(v.root, ref v.redDot, can);
            if (v.line != null)
            {
                // 最底 L1（index 0）不画向下连接线；其余连线
                bool showLine = i > 0;
                v.line.gameObject.SetActive(showLine);
                if (showLine) ApplySprite(v.line, on ? sprLeftLinkOn : sprLeftLinkOff, false);
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
        int leftUnlocked = TalentDefs.LeftUnlockedCount(talents);

        for (int i = 0; i < _rightViews.Count; i++)
            RefreshOneRightNode(_rightViews[i], talents, leftUnlocked);

        // 2026-09-29：入口红点 = 还有没有「新开通未点」的右列节点。
        // 玩家点了右侧那个新天赋 → 它不再是新 → 没有其它新节点时红点自动灭。
        RedDot.Set(RedDot.Talent, TalentDefs.AnyNewlyOpen(talents));

        if (rightTipText != null)
            rightTipText.text = "消耗天赋石解锁/升级天赋（分批开放）";
        if (rightCostValueText != null)
            rightCostValueText.text = "";
    }

    void RefreshOneRightNode(RightNodeView v, Dictionary<string, int> talents, int leftUnlocked)
    {
        var node = v.node;
        if (node == null) return;

        // 【2026-10-18 主人拍板】物理/魔法专精合并行（二选一）：状态/置灰/红点/「新」走专门刷新
        if (v.node2 != null)
        {
            RefreshMutexPairRow(v, talents, leftUnlocked);
            return;
        }

        var data = SaveSystem.Instance?.Data;
        int level = TalentDefs.GetRightNodeLevel(talents, node.id);
        bool unlocked = level > 0;
        bool mutexLocked = !unlocked && TalentDefs.IsRightNodeMutexLocked(node, talents);
        bool thresholdLocked = !unlocked && leftUnlocked < node.unlockLeftIndex;
        int cap = TalentDefs.RightNodeLevelCap(node, leftUnlocked);

        string status;
        bool canAct = false;
        if (unlocked && level >= node.maxLevel)
            status = "MAX";
        else if (unlocked)
        {
            // 任务3：等级上限受左列已点数量限制
            if (level >= cap)
                status = "Lv" + cap + " (左列↑)";
            else
            {
                int cost = node.costByLevel != null && level < node.costByLevel.Length ? node.costByLevel[level] : 0;
                status = "升级 " + cost;
                canAct = data != null && data.talentPoints >= cost;
            }
        }
        else if (thresholdLocked)
            status = "左列 L" + node.unlockLeftIndex;   // 任务3：分批开放提示
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
        SetRowRedDot(v.root, ref v.redDot, canAct);
        // 2026-09-29 主人要求：新开通且还没点过的节点右上角标红「新」；玩家一点就消失。
        // 判定统一走 TalentDefs.IsNewlyOpen（与入口红点同一口径），不要在这里另写一套条件。
        SetRowNewTag(v.root, ref v.newTag, TalentDefs.IsNewlyOpen(node, talents, leftUnlocked));
        // 【2026-10-17 主人要求】锁定行也保持可点：点了弹具体开启条件（左列开到第几个 / 互斥），
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

    void RefreshResetButton()
    {
        bool canReset = TalentSystem.CanReset(out _);
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(canReset);
            resetButton.interactable = canReset;
        }
        if (resetButtonGray != null)
        {
            resetButtonGray.gameObject.SetActive(!canReset);
            resetButtonGray.interactable = false;
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

        if (sumAttackText != null) sumAttackText.text = "物攻 +" + atk.ToString("0");
        if (sumMagicText != null) sumMagicText.text = "魔攻 +" + mag.ToString("0");
        if (sumHpText != null) sumHpText.text = "+" + hp.ToString("0");
        if (sumDefText != null) sumDefText.text = "+" + def.ToString("0");
        if (sumCritText != null) sumCritText.text = "+" + crit.ToString("0.##") + "%";
        if (sumAtkSpdText != null) sumAtkSpdText.text = "+" + spd.ToString("0.##") + "%";
    }

    static Dictionary<string, int> GetTalents()
    {
        try
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
                return SaveSystem.Instance.Data.talents;
        }
        catch { }
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

    static void SetRowRedDot(GameObject rowRoot, ref GameObject dot, bool show)
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
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            var img = dot.GetComponent<Image>();
            img.sprite = RedDot.Sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
        dot.SetActive(true);
    }

    /// <summary>
    /// 2026-09-29：节点右上角红色「新」字（<see cref="RightNodeView.newTag"/>）。
    /// 位置刻意让开右上角那个「可点/可升级」小红点（-6,-6），故往左挪到 (-34,-12)。
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
