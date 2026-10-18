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
// 职责：掉落预览图标（刷新、显示名、缩小处理、图标加载与缓存）
public partial class AdventureUI
{

    void RefreshDropIcons()
    {
        if (dropIconContainer == null) return;

        var slots = new List<DropSlot>();
        for (int i = 0; i < 4; i++)
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
            // 【2026-10-11 主人拍板】掉落图标缩小 10%（框 + 图都缩，名字挂框下不受影响 —— 见 ApplyDropIconShrink）
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

            // 【2026-10-11 主人拍板】图标下方写上名字（主人：「可能掉落的物品…或者在物品下方写上名字」）
            var tag = EnsureDropNameTag(slots[i].kuang != null ? slots[i].kuang : slots[i].icon);
            if (tag != null) tag.text = DropDisplayName(_previewDrops[i]);
        }
    }

    /// <summary>
    /// 可能掉落物品的显示名（图标下方那行小字）。叫法只在这一个出口维护。
    /// 【2026-10-11 主人拍板】尽量短、能认出来就行（槽宽约 70 逻辑像素，2~3 字最稳）。
    /// </summary>
    static string DropDisplayName(PreviewDropKind kind)
    {
        switch (kind)
        {
            case PreviewDropKind.Gold:            return "金币";
            case PreviewDropKind.TalentStone:     return "天赋石";
            case PreviewDropKind.ScrollCommon:    return "普卷";
            case PreviewDropKind.ScrollRare:      return "稀卷";
            case PreviewDropKind.ScrollLegendary: return "传卷";
            default:                              return "";
        }
    }

    /// <summary>
    /// 掉落名字字号（主人：「名字再大一号」→ 14 → 16）。
    /// 颜色与详情面板其它文字一致（预制体「敌人信息 / 可能掉落」都是这个深棕）
    /// —— 主人：「把掉落物品的名字的颜色改成和其他的字的颜色一致」。
    /// </summary>
    const int DropNameFontSize = 16;
    static readonly Color DetailTextColor = new Color(0.151f, 0.109f, 0.032f);

    /// <summary>掉落图标（框 + 图）整体缩放（主人：「可能掉落的图标缩小10%」）。</summary>
    const float DropIconScale = 0.9f;

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
    /// 掉落图标下方那行名字：挂在图标框（IconkuangN）下，位置跟着框走，不必猜容器布局；
    /// 节点运行时补建（预制体不动），重复调用复用同一个。
    /// </summary>
    static Text EnsureDropNameTag(Transform slot)
    {
        if (slot == null) return null;
        var existing = slot.Find("DropName");
        if (existing != null) return existing.GetComponent<Text>();

        var go = new GameObject("DropName", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(slot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -1f);
        rt.sizeDelta = new Vector2(80f, 18f);

        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = DropNameFontSize;
        t.alignment = TextAnchor.UpperCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.color = DetailTextColor;   // 与面板其它文字同色（深棕）
        t.text = "";
        return t;
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
            outList.Add(PreviewDropKind.Gold);
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
            case PreviewDropKind.TalentStone:
                return LoadTalentDropSprite();
            case PreviewDropKind.ScrollCommon:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Common);
            case PreviewDropKind.ScrollRare:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Rare);
            case PreviewDropKind.ScrollLegendary:
                return LoadScrollDropSprite(MercRosterDefs.MercRarity.Legendary);
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
            var adv = Resources.Load<Sprite>("Art/UI/Icons/Common/高级招募卷轴");
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
        _staminaIconSprite = Resources.Load<Sprite>("Art/UI/Icons/Common/icon_stamina");
        return _staminaIconSprite;
    }
}
