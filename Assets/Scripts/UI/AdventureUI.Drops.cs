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
// 职责：掉落预览图标（刷新、缩放、图标加载与缓存；2026-10-29 主人拍板「不要下面那个名字」，
//       图标下的小字名牌废除，图标回预制体原始尺寸）
public partial class AdventureUI
{

    void RefreshDropIcons()
    {
        if (dropIconContainer == null) return;

        var slots = new List<DropSlot>();
        // 【2026-10-19 主人拍板】预制体掉落槽扩到 8 个（Iconkuang0..7 / Icon0..7），扫描跟着扩，
        // 多出来的槽没有对应掉落种类时仍会隐藏
        for (int i = 0; i < 8; i++)
        {
            var kuang = dropIconContainer.Find($"Iconkuang{i}");
            var icon = dropIconContainer.Find($"Icon{i}");
            if (kuang == null && icon == null) continue;
            slots.Add(new DropSlot { kuang = kuang, icon = icon });
        }

        CollectPreviewDrops(_previewDrops);
        int showCount = Mathf.Min(slots.Count, _previewDrops.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            bool on = i < showCount;
            // 掉落图标缩放（当前 =1 原始尺寸；2026-10-29 主人拍板改大回预制体原始值 —— 见 ApplyDropIconShrink）
            ApplyDropIconShrink(slots[i].kuang);
            ApplyDropIconShrink(slots[i].icon);
            if (slots[i].kuang != null) slots[i].kuang.gameObject.SetActive(on);
            if (slots[i].icon != null) slots[i].icon.gameObject.SetActive(on);
            if (!on || slots[i].icon == null) continue;
            var img = slots[i].icon.GetComponent<Image>();
            if (img == null) continue;
            var sp = LoadPreviewDropSprite(_previewDrops[i]);
            if (sp == null)
            {
                Debug.LogWarning($"[AdventureUI] 可能掉落图标加载失败 kind={_previewDrops[i]}");
                continue;
            }
            img.sprite = sp;
            img.color = Color.white;
            img.preserveAspect = true;

            // 【2026-10-29 主人拍板「可能掉落的图标…不要下面那个名字了」】名牌废除：
            // 不再创建；本会话早前已建过的 DropName 一并清掉
            var staleTag = (slots[i].kuang != null ? slots[i].kuang : slots[i].icon)?.Find("DropName");
            if (staleTag != null) Destroy(staleTag.gameObject);
        }
    }

    /// <summary>掉落图标（框 + 图）整体缩放。2026-10-11 主人拍板缩 10%；【2026-10-29 主人拍板
    /// 「可能掉落的图标改大点」】回到预制体原始尺寸（=1）。槽位 pitch 60px，再大要动预制体间距。</summary>
    const float DropIconScale = 1f;

    /// <summary>
    /// 掉落图标/框的原始尺寸（每个节点第一次读到就记下）。
    /// 缩小走 sizeDelta 而不是 localScale —— 名字是图标框的子节点，用 localScale 会把名字一起缩掉。
    /// </summary>
    readonly Dictionary<int, Vector2> _dropIconBaseSize = new Dictionary<int, Vector2>();

    /// <summary>把掉落图标/框缩到 <see cref="DropIconScale"/>（按原始尺寸算，重复调用不会越缩越小）。</summary>
    void ApplyDropIconShrink(Transform t)
    {
        if (t == null) return;
        var rt = t as RectTransform;
        if (rt == null) return;
        int key = rt.GetInstanceID();
        Vector2 baseSize;
        if (!_dropIconBaseSize.TryGetValue(key, out baseSize))
        {
            baseSize = rt.sizeDelta;
            _dropIconBaseSize[key] = baseSize;
        }
        rt.sizeDelta = baseSize * DropIconScale;
    }

    /// <summary>
    /// 关卡预览掉落（临时按章节规则）。待 <c>stage_preview_drop</c> 表接入后改读表。
    /// </summary>
    void CollectPreviewDrops(List<PreviewDropKind> outList)
    {
        outList.Clear();
        if (!IsModePlayable(_selectedMode)) return;
        if (IsActivityMode(_selectedMode))
        {
            // 【2026-10-23 拍板「活动副本一拆二」+ 2026-10-24 加卷轴】
            // 金币横条→金币；徽记横条→随机职业徽记（预览用普通档代表图）；
            // 卷轴横条→按所选难度预览（2026-10-24 主人拍板：普通=普通卷3-5 / 困难=普通3-5+高级0-1 / 噩梦=高级1-3 只掉高级）
            if (_selectedActivityKind == ActivityDungeonKind.Badge)
                outList.Add(PreviewDropKind.Badge);
            else if (_selectedActivityKind == ActivityDungeonKind.Gift)
                outList.Add(PreviewDropKind.Gift);   // 礼物副本：随机好感礼物，难度越高数量越多（2/3/4）
            else if (_selectedActivityKind == ActivityDungeonKind.Scroll)
            {
                if (_selectedDiff >= 2)
                    outList.Add(PreviewDropKind.ScrollLegendary);   // 噩梦：只掉高级
                else
                {
                    outList.Add(PreviewDropKind.ScrollCommon);      // 普通/困难：都掉普通 3~5
                    if (_selectedDiff >= 1)
                        outList.Add(PreviewDropKind.ScrollLegendary);   // 困难：追加高级 0~1
                }
            }
            else
                outList.Add(PreviewDropKind.Gold);
            return;
        }
        // 无限裂隙：宝箱金/首通金都是战斗内抽奖币（2026-10-18 货币口径），首通发天赋石，每 10 层固定宝箱发普通卷轴
        if (IsEndlessMode(_selectedMode))
        {
            outList.Add(PreviewDropKind.DraftCoin);
            outList.Add(PreviewDropKind.TalentStone);
            outList.Add(PreviewDropKind.ScrollCommon);
            return;
        }
        outList.Add(PreviewDropKind.Gold);
        outList.Add(PreviewDropKind.TalentStone);
        // 2026-10-17 卷轴改版：第 3 章起预览普通卷轴（招稀有佣兵）、第 7 章起预览高级招募卷轴（招传奇佣兵）；
        // 旧稀有招募卷已废弃，不再预览
        if (_selectedChapter >= 3) outList.Add(PreviewDropKind.ScrollCommon);
        if (_selectedChapter >= 7) outList.Add(PreviewDropKind.ScrollLegendary);
    }

    Sprite LoadPreviewDropSprite(PreviewDropKind kind)
    {
        switch (kind)
        {
            case PreviewDropKind.Gold:
                return LoadGoldDropSprite();
            case PreviewDropKind.DraftCoin:
                return LoadDraftCoinDropSprite();
            case PreviewDropKind.TalentStone:
                return LoadTalentDropSprite();
            case PreviewDropKind.ScrollCommon:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Common);
            case PreviewDropKind.ScrollRare:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Rare);
            case PreviewDropKind.ScrollLegendary:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Legendary);
            case PreviewDropKind.Badge:
                return LoadBadgeDropSprite();
            case PreviewDropKind.Gift:
                // 礼物副本掉落预览：拿礼物图组里的面包当代表图（随机礼物池见 MercFavorDefs.Gifts）
                return Resources.Load<Sprite>("UI/Art/礼物/32px-item301");
            default:
                return null;
        }
    }

    struct DropSlot
    {
        public Transform kuang;
        public Transform icon;
    }

    const string CommonDropArtRoot = "Assets/Art/UI/Icons/Common/";
    const string CommonDropResourcesPath = "UI/Icons/Common/";

    /// <summary>
    /// 可能掉落图标：优先 Resources/UI/Icons/Common，编辑器回退 Art/UI/Icons/Common。
    /// </summary>
    static Sprite LoadCommonDropIcon(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;

        string resPath = CommonDropResourcesPath + fileNameWithoutExt;
        var sp = Resources.Load<Sprite>(resPath);
        if (sp != null) return sp;

        var all = Resources.LoadAll<Sprite>(resPath);
        if (all != null && all.Length > 0)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == fileNameWithoutExt)
                    return all[i];
            }
            return all[0];
        }

        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null)
        {
            var made = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            made.name = fileNameWithoutExt;
            return made;
        }

#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            string assetPath = CommonDropArtRoot + fileNameWithoutExt + ".png";
            var ed = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (ed != null) return ed;
            var edAll = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (edAll != null)
            {
                for (int i = 0; i < edAll.Length; i++)
                {
                    if (edAll[i] is Sprite spEd) return spEd;
                }
            }
            var edTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (edTex != null)
            {
                var made = Sprite.Create(
                    edTex,
                    new Rect(0f, 0f, edTex.width, edTex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                made.name = fileNameWithoutExt;
                return made;
            }
        }
#endif
        return null;
    }

    Sprite LoadGoldDropSprite()
    {
        if (_goldDropSprite != null) return _goldDropSprite;
        return _goldDropSprite = LoadCommonDropIcon("icon_gold");
    }

    /// <summary>徽记副本掉落预览图（2026-10-23）：随机职业在结算才揭晓，预览固定用剑盾·普通档徽记代表图。</summary>
    Sprite LoadBadgeDropSprite()
    {
        if (_badgeDropSprite != null) return _badgeDropSprite;
        return _badgeDropSprite = MercGrowSprites.LoadJobBadge("剑盾", MercRosterDefs.MercRarity.Common);
    }

    Sprite LoadDraftCoinDropSprite()
    {
        if (_draftCoinDropSprite != null) return _draftCoinDropSprite;
        return _draftCoinDropSprite = LoadCommonDropIcon("icon_draft_coin");
    }

    Sprite LoadTalentDropSprite()
    {
        if (_talentDropSprite != null) return _talentDropSprite;
        return _talentDropSprite = LoadCommonDropIcon("icon_talent_stone");
    }

    Sprite LoadScrollDropSprite(MercRosterDefs.MercRarity rarity)
    {
        // Common 目录仅一张雇佣卷轴，各稀有度共用；传奇（高级招募卷轴）优先用主人 2026-10-17 放入的新图
        if (rarity == MercRosterDefs.MercRarity.Legendary)
        {
            if (_scrollLegendarySprite != null) return _scrollLegendarySprite;
            // 主人 2026-10-17 放入的高级招募卷轴新图（在 Resources/Art/ 下，路径不同，直接加载）
            var adv = Resources.Load<Sprite>("UI/Art/Icons/Common/高级招募卷轴");
            return _scrollLegendarySprite = adv != null ? adv : LoadCommonDropIcon("icon_hire_scroll");
        }
        if (rarity == MercRosterDefs.MercRarity.Rare)
        {
            if (_scrollRareSprite != null) return _scrollRareSprite;
            return _scrollRareSprite = LoadCommonDropIcon("icon_hire_scroll");
        }
        if (_scrollCommonSprite != null) return _scrollCommonSprite;
        return _scrollCommonSprite = LoadCommonDropIcon("icon_hire_scroll");
    }

    Sprite LoadStaminaIconSprite()
    {
        if (_staminaIconSprite != null) return _staminaIconSprite;
        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : null;
        if (hall != null)
        {
            Transform staminaPanel = TownSharedChrome.FindDeep(hall, "体力Panel");
            if (staminaPanel != null)
            {
                Transform coin = staminaPanel.Find("CoinIcon");
                if (coin != null)
                {
                    var img = coin.GetComponent<Image>();
                    if (img != null && img.sprite != null)
                        return _staminaIconSprite = img.sprite;
                }
            }
        }
        // 主人 2026-10-17 放入的体力新图（Resources/Art/ 下，路径与 Art 源不同）
        _staminaIconSprite = Resources.Load<Sprite>("UI/Art/Icons/Common/icon_stamina");
        return _staminaIconSprite;
    }
}
