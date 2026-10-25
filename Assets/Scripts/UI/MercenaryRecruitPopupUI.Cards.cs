using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡片渲染：单卡刷新、技能槽文案、稀有度标签、已拥有灰显与养成徽记。
/// 【2026-10-23 主人拍板】原 MercenaryRecruitPopupUI.cs 已 1461 行（项目约定 >600 行必须拆），
/// 按职责切成 5 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercenaryRecruitPopupUI : MonoBehaviour
{
    void RefreshCard(int i)
    {
        var c = cards[i];
        if (c == null || c.root == null) return;
        var offer = i < _offers.Count ? _offers[i] : null;
        bool on = offer != null;
        c.root.SetActive(on);
        if (!on)
        {
            ApplyHiredVisual(c, false);
            return;
        }

        var rarity = MercHireSession.OfferRarity(offer);
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(offer.mercId)
            : "";

        if (c.nicknameText != null)
            c.nicknameText.text = string.IsNullOrEmpty(offer.nickname) ? "—" : offer.nickname;
        if (c.nameText != null)
            c.nameText.text = string.IsNullOrEmpty(offer.displayName) ? offer.mercId : offer.displayName;
        if (c.rarityText != null)
            c.rarityText.text = RarityLabel(rarity);

        if (c.roleIcon != null)
        {
            // 招募卡的 Role 角标 = 佣兵职业四分类徽标（Icons/职业icon），唯一入口 JobIconResolver.CombatBadge
            var sp = JobIconResolver.CombatBadge(job);
            c.roleIcon.sprite = sp;
            c.roleIcon.enabled = sp != null;
            c.roleIcon.color = Color.white;
            // 【2026-10-19 主人拍板】职业徽章素材原生 42×50，显示不许改变图片大小：
            // 节点尺寸跟随精灵原生宽高，且不开 preserveAspect（那会按节点框改显示尺寸）
            if (sp != null)
            {
                c.roleIcon.rectTransform.sizeDelta = new Vector2(sp.rect.width, sp.rect.height);
                c.roleIcon.preserveAspect = false;
            }
        }

        if (c.portrait != null)
        {
            string portraitKey = !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : offer.mercId;
            var sp = MercPortraitSprites.GetStand(portraitKey)
                ?? (MercenaryManager.Instance != null ? MercenaryManager.Instance.GetIcon(offer.mercId) : null);
            c.portrait.sprite = sp;
            c.portrait.enabled = true;
            c.portrait.color = sp != null ? Color.white : new Color(0.35f, 0.32f, 0.38f, 1f);
            c.portrait.preserveAspect = true;
            if (sp != null)
                PortraitIdleMotion.EnsureOn(c.portrait.rectTransform, i * 0.31f);
            else
            {
                var idle = c.portrait.GetComponent<PortraitIdleMotion>();
                if (idle != null) idle.enabled = false;
            }
        }

        if (c.background != null)
        {
            // 【2026-10-19 主人拍板】Card0/1/2 是三种稀有度卡牌：卡面随佣兵稀有度切到对应档的
            // 卡牌美术 —— 优先用预制体自己的三张底图（MapCardFrames 归档），取不到再退 LoadRarityFrame
            Sprite frame = null;
            if (_cardFrameByRarity.TryGetValue(rarity, out var own) && own != null)
                frame = own;
            else
                frame = MercHireSession.LoadRarityFrame(rarity);
            if (frame != null)
            {
                c.background.sprite = frame;
                c.background.color = Color.white;
            }
        }

        BindSkillTexts(c, offer);

        // 【2026-10-25 主人拍板】已拥有的候选可重复购买 = 转本命碎片（获取碎片的途径，
        // 与名册解锁 TavernUnlockUI.TryUnlock 的重复口径同源）：卡面不灰显、货币钮照常显示价格，
        // 保留黄字「已拥有·转碎片」标提示这是重复购买。
        bool owned = MercHireSession.IsMercOwned(offer);
        ApplyHiredVisual(c, owned);

        RefreshGrowBadge(c, offer, rarity);

        int gold = MercHireSession.GoldCost(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        bool isLegendary = rarity == MercRosterDefs.MercRarity.Legendary;

        // 【2026-10-19 主人拍板】一卡一钮，货币跟稀有度走：普通=金币；稀有=普通卷；传奇=稀有卷（高级卷）。
        // 两钮显隐每次显式设置（旧版漏关卷钮 → 同一张卡同时出现金币+卷轴两种购买方式，主人报过）。
        // 卡上没摆卷钮节点的（Card0）由金币钮节点顶位显示卷轴。
        bool useScrollNode = !isCommon && c.scrollButton != null;
        if (c.goldButton != null)
            c.goldButton.gameObject.SetActive(!useScrollNode);
        if (c.scrollButton != null)
            c.scrollButton.gameObject.SetActive(useScrollNode);
        if (useScrollNode)
        {
            FillCurrencyButton(c.scrollButtonImage, c.scrollIcon, c.scrollLabel1,
                c.scrollIconDefault, isCommon, isLegendary, gold);
        }
        else if (c.goldButton != null)
        {
            FillCurrencyButton(c.goldButton.GetComponent<Image>(), c.goldIcon, c.goldLabel,
                c.goldIconDefault, isCommon, isLegendary, gold);
        }
    }

    /// <summary>
    /// 货币钮统一填充：普通=金币钮「600金币」（金币图）；稀有=「普通卷×1」；传奇=「稀有卷×1」（卷图）。
    /// 外观按快照统一（金币一种样子、卷轴一种样子）：底图/颜色/尺寸/Label颜色都从快照套用，
    /// 字号统一 13（预制体金币钮 Label 16 / 卷钮 Label 11 不一致，主人报「按钮的字大小不一致」）。
    /// </summary>
    void FillCurrencyButton(Image btnImg, Image icon, Text label, Sprite iconDefault,
        bool isCommon, bool isLegendary, int gold)
    {
        if (btnImg != null)
        {
            var matRarity = isLegendary ? MercRosterDefs.MercRarity.Legendary : MercRosterDefs.MercRarity.Rare;
            btnImg.material = !isCommon ? MercHireSession.LoadScrollButtonMaterial(matRarity) : null;

            // 外观快照套用： whichever 节点顶位，都长成对应货币的标准样子
            var lookSp = isCommon ? _goldBtnSprite : _scrollBtnSprite;
            if (lookSp != null) btnImg.sprite = lookSp;
            var lookSize = isCommon ? _goldBtnSize : _scrollBtnSize;
            var rt = btnImg.transform as RectTransform;
            if (rt != null && lookSize.x > 0f) rt.sizeDelta = lookSize;
            if (isCommon)
            {
                if (_goldBtnSprite != null) btnImg.color = _goldBtnColor;
            }
            else if (_scrollBtnSprite != null) btnImg.color = _scrollBtnColor;
        }

        if (icon != null)
        {
            Sprite sp;
            if (isCommon)
                sp = iconDefault;   // 金币图（预制体摆的）
            else
            {
                var scrollSp = isLegendary ? _legendaryScrollIcon : _commonScrollIcon;
                sp = scrollSp != null ? scrollSp : iconDefault;
            }
            icon.sprite = sp;
            icon.enabled = sp != null;
        }
        if (label != null)
        {
            label.fontSize = 13;
            if (isCommon)
            {
                if (_goldBtnSprite != null) label.color = _goldLabelColor;
                label.text = gold + "金币";
            }
            else
            {
                if (_scrollBtnSprite != null) label.color = _scrollLabelColor;
                label.text = isLegendary ? "稀有卷×1" : "普通卷×1";
            }
        }
    }

    static void BindSkillTexts(CardView c, MercenaryData offer)
    {
        // 【2026-10-19 主人拍板】卡上按实有技能数展示：有两个显示两个、只有一个显示一个、
        // 全没有显示「无技能」。取代 2026-10-18「主动槽只显示主动」的固定槽位口径（B9 修的
        // 「两槽显示同一技能」由槽2留白天然规避，不会复发）。
        string active = offer.skillId;
        string passive = offer.passiveSkillId;
        bool hasActive = !string.IsNullOrEmpty(active);
        bool hasPassive = !string.IsNullOrEmpty(passive);

        // 槽1 = 第一个实有技能（主动优先）；一个都没有 → 「无技能」
        FillSkillSlot(c.skill1Name, c.skill1Desc, c.skill1Icon,
            hasActive ? active : hasPassive ? passive : null, "无技能");
        // 槽2 = 第二个实有技能；只有一个技能时槽2整槽留白
        FillSkillSlot(c.skill2Name, c.skill2Desc, c.skill2Icon,
            hasActive && hasPassive ? passive : null, "");
    }

    /// <summary>单个技能槽填充：skillId 为空显示 emptyLabel、描述与图标清空（留白，不占位报错）。</summary>
    static void FillSkillSlot(Text name, Text desc, Image icon, string skillId, string emptyLabel)
    {
        bool has = !string.IsNullOrEmpty(skillId);
        if (name != null)
            name.text = has ? MercenaryOfferGenerator.SkillDisplayName(skillId) : emptyLabel;
        if (desc != null)
            desc.text = has && MercSkillTable.TryGet(skillId, out var row) ? row.RecruitDesc : "";
        if (icon != null)
        {
            var sp = has ? MercSkillTable.LoadIcon(skillId) : null;
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.preserveAspect = true;
        }
    }

    static string RarityLabel(MercRosterDefs.MercRarity r)
    {
        if (r == MercRosterDefs.MercRarity.Legendary) return "传奇";
        if (r == MercRosterDefs.MercRarity.Rare) return "稀有";
        return "普通";
    }

    static void SetButtonGray(Button btn, bool gray)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null)
            img.color = gray ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
    }


    static void ApplyHiredVisual(CardView c, bool hired)
    {
        if (c == null || c.root == null) return;
        var cg = c.root.GetComponent<CanvasGroup>();
        if (cg == null) cg = c.root.AddComponent<CanvasGroup>();
        // 【2026-10-25 主人拍板】已拥有卡可重复购买（转碎片），不再压暗灰显，只留角标提示
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        var badge = EnsureHiredBadge(c.root.transform);
        if (badge != null)
        {
            // 【2026-10-19 主人拍板】显示时压到卡内最上层，防被后创建的构件（MG_Badge 等）盖住
            if (hired)
            {
                badge.transform.SetAsLastSibling();
                badge.text = "已拥有·转碎片";
            }
            badge.gameObject.SetActive(hired);
        }
    }

    static Text EnsureHiredBadge(Transform cardRoot)
    {
        if (cardRoot == null) return null;
        var existing = cardRoot.Find("HiredBadge");
        Text t = existing != null ? existing.GetComponent<Text>() : null;
        if (t != null) return t;

        // 【2026-10-19 主人拍板】「已拥有」加大加粗、摆在卡面正中（原来是 22 号细字缩在中心偏下，
        // 被下方技能区/按钮区压住看不清）；【2026-10-25】文案改「已拥有·转碎片」（可重复购买转碎片）
        t = CreateTxt(cardRoot, "HiredBadge", "已拥有·转碎片", 34, TextAnchor.MiddleCenter);
        t.color = new Color(1f, 0.86f, 0.42f, 1f);
        t.fontStyle = FontStyle.Bold;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(320f, 60f);
        t.gameObject.SetActive(false);
        return t;
    }

    /// <summary>补挂「佣兵养成构件」：职业徽记角标 + 本命碎片数量（懒创建，reroll 时只刷新不重建）。</summary>
    static void RefreshGrowBadge(CardView c, MercenaryData offer, MercRosterDefs.MercRarity rarity)
    {
        if (c == null || c.root == null || offer == null) return;
        var cardRoot = c.root.transform;

        // —— 职业徽记角标（右上角）——
        Image badge = cardRoot.Find("MG_Badge")?.GetComponent<Image>();
        string key = !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : offer.mercId;
        if (badge == null)
        {
            // 2026-09-27：末位那个 40f 只是新建节点的初始占位，真实尺寸由
            // CreateBadgeForMerc 按**图片自身尺寸**（职业图 44×50）还原，代码不再压成正方形。
            badge = MercGrowUI.CreateBadgeForMerc(cardRoot, "MG_Badge", key, rarity, 40f);
            if (badge != null)   // 兜空：构件返回 null 或素材缺失都不加空节点、不改排版
            {
                var rt = badge.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-6f, -6f);
            }
        }
        else
        {
            MercGrowUI.RefreshBadge(badge, MercRosterDefs.GetJobName(key), rarity);
        }

        // —— 本命碎片数量（角标下方，0 时不显示）——
        Text frag = cardRoot.Find("MG_Fragment")?.GetComponent<Text>();
        if (frag == null)
        {
            frag = CreateTxt(cardRoot, "MG_Fragment", "", 16, TextAnchor.MiddleRight);
            var rt = frag.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-6f, -50f);
            rt.sizeDelta = new Vector2(160f, 24f);
            frag.color = new Color(1f, 0.86f, 0.42f, 1f);
        }
        if (frag != null)
        {
            int fragCount = MercGrowInventory.FragmentCount(offer.hireId);
            if (fragCount <= 0)
                frag.gameObject.SetActive(false);
            else
            {
                frag.gameObject.SetActive(true);
                frag.text = "碎片 x" + fragCount;
            }
        }
    }
}
