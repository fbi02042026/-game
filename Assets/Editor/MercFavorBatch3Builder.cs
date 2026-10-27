using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵好感界面 第三批构建（2026-10-27 主人拍板）：
/// B1 礼物横排 ScrollRect（结构进 prefab：GiftScroll→Viewport(RectMask2D)→Content(HorizontalLayoutGroup+ContentSizeFitter)）
/// B2 左列 6 格各加 FavorHeart(爱心图标) + FavorPct(百分比文本)
/// 用引擎 API 构建（避免手写组件 YAML 出 Missing Script），显式方法执行，非 InitializeOnLoad。
/// 防重入：节点已存在则跳过，可安全重跑。
/// </summary>
public static class MercFavorBatch3Builder
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/佣兵好感.prefab";

    [MenuItem("Tools/好感界面/第三批构建(礼物横排+爱心百分比)")]
    public static void MenuBuild()
    {
        BuildBatch3();
    }

    /// <summary>batchmode 入口：Tuanjie.exe -batchmode -quit -projectPath ... -executeMethod MercFavorBatch3Builder.BuildBatch3</summary>
    public static void BuildBatch3()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int added = 0;
        try
        {
            added += BuildGiftScroll(root.transform);
            added += BuildRowBadges(root.transform);
            if (added > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[MercFavorBatch3] saved, added={added}");
            }
            else
            {
                Debug.Log("[MercFavorBatch3] nothing to add (already built), prefab untouched");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------- B1 礼物横排 ----------------

    static int BuildGiftScroll(Transform root)
    {
        var middle = root.Find("Panel/Right/Middle");
        if (middle == null) { Debug.LogError("[MercFavorBatch3] Panel/Right/Middle 不存在，中止 B1"); return 0; }
        if (middle.Find("GiftScroll") != null)
        {
            Debug.Log("[MercFavorBatch3] GiftScroll 已存在，跳过 B1");
            return 0;
        }
        var bg = middle.Find("bg") as RectTransform;
        if (bg == null) { Debug.LogError("[MercFavorBatch3] Middle/bg 不存在，B1 缺对齐基准"); return 0; }

        int count = 0;

        // GiftScroll：对齐 bg 的矩形区域
        var scrollGo = new GameObject("GiftScroll", typeof(RectTransform));
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.SetParent(middle, false);
        srt.anchorMin = bg.anchorMin;
        srt.anchorMax = bg.anchorMax;
        srt.anchoredPosition = bg.anchoredPosition;
        srt.sizeDelta = bg.sizeDelta;
        srt.pivot = bg.pivot;
        srt.localScale = Vector3.one;
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = true;
        sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 20;
        sr.elasticity = 0.1f;
        count++;
        Debug.Log("[MercFavorBatch3] + Middle/GiftScroll (ScrollRect)");

        // Viewport：满铺 GiftScroll，RectMask2D 裁剪（按矩形，不读 alpha，不需要新图）
        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        var vrt = vpGo.GetComponent<RectTransform>();
        vrt.SetParent(srt, false);
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        vrt.pivot = new Vector2(0.5f, 0.5f);
        vpGo.AddComponent<RectMask2D>();
        sr.viewport = vrt;
        count++;
        Debug.Log("[MercFavorBatch3] + GiftScroll/Viewport (RectMask2D)");

        // Content：横排，左对齐中，宽度由 ContentSizeFitter 撑开
        var contentGo = new GameObject("Content", typeof(RectTransform));
        var crt = contentGo.GetComponent<RectTransform>();
        crt.SetParent(vrt, false);
        crt.anchorMin = new Vector2(0f, 0.5f);
        crt.anchorMax = new Vector2(0f, 0.5f);
        crt.pivot = new Vector2(0f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 130f);
        crt.localScale = Vector3.one;
        var hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;                       // 80+10=90 步进，与美术值一致
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;           // 槽固定 80x130，不由布局改尺寸
        hlg.childControlHeight = false;
        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        sr.content = crt;
        count++;
        Debug.Log("[MercFavorBatch3] + GiftScroll/Viewport/Content (HLayout spacing=10 + CSF preferred)");

        // 模板 panl：80x130；Iconbg 56x56 y+26；加透明命中 Image + Button + UiButtonPressFeedback（prefab 化，主人可调）
        var panl = middle.Find("panl") as RectTransform;
        if (panl != null)
        {
            panl.sizeDelta = new Vector2(80f, 130f);
            var iconbg = panl.Find("Iconbg") as RectTransform;
            if (iconbg != null)
            {
                iconbg.sizeDelta = new Vector2(56f, 56f);
                iconbg.anchoredPosition = new Vector2(iconbg.anchoredPosition.x, 26f);
            }
            else Debug.LogError("[MercFavorBatch3] panl/Iconbg 不存在，尺寸未改");

            var img = panl.GetComponent<Image>();
            if (img == null)
            {
                img = panl.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);   // 透明命中层
                img.raycastTarget = true;
                count++;
                Debug.Log("[MercFavorBatch3] + panl 透明 Image 命中层");
            }
            var btn = panl.GetComponent<Button>();
            if (btn == null)
            {
                btn = panl.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                count++;
                Debug.Log("[MercFavorBatch3] + panl Button");
            }
            if (panl.GetComponent("UiButtonPressFeedback") == null)
            {
                panl.gameObject.AddComponent(System.Type.GetType("UiButtonPressFeedback, Assembly-CSharp"));
                count++;
                Debug.Log("[MercFavorBatch3] + panl UiButtonPressFeedback（prefab 化，绕开运行时 AttachUnder 的透明跳过规则）");
            }
            panl.gameObject.SetActive(false);        // 模板隐藏，克隆由代码 SetActive(true)
            Debug.Log("[MercFavorBatch3] panl 模板已设 inactive(克隆时由代码激活)");
        }
        else Debug.LogError("[MercFavorBatch3] Middle/panl 模板不存在，B1 槽体未改");

        return count;
    }

    // ---------------- B2 左列爱心 + 百分比 ----------------

    static int BuildRowBadges(Transform root)
    {
        var roster = root.Find("Panel/Left/头像");
        if (roster == null) { Debug.LogError("[MercFavorBatch3] Panel/Left/头像 不存在，中止 B2"); return 0; }
        var heartSrc = root.Find("Panel/Right/Top/好感进度/爱心1");
        Sprite heartSp = null;
        if (heartSrc != null)
        {
            var hImg = heartSrc.GetComponent<Image>();
            if (hImg != null) heartSp = hImg.sprite;
        }
        if (heartSp == null) Debug.LogError("[MercFavorBatch3] 取不到爱心1 的 sprite（FavorHeart 将无图，请检查好感进度/爱心1）");

        var fontSrc = root.Find("Panel/Right/bottom/Info");
        Font font = null;
        if (fontSrc != null)
        {
            var t = fontSrc.GetComponent<Text>();
            if (t != null) font = t.font;
        }
        if (font == null) Debug.LogError("[MercFavorBatch3] 取不到 Info 的字体（FavorPct 将用默认字体）");

        int count = 0;
        int idx = 0;
        foreach (Transform row in roster)
        {
            if (idx >= 6) break;
            idx++;
            if (row.Find("FavorHeart") != null || row.Find("FavorPct") != null)
            {
                Debug.Log($"[MercFavorBatch3] {row.name} 已有徽章，跳过");
                continue;
            }

            // 爱心图标：20x20，格子右下（初值，主人可在编辑器直接调）
            var heartGo = new GameObject("FavorHeart", typeof(RectTransform));
            var hrt = heartGo.GetComponent<RectTransform>();
            hrt.SetParent(row, false);
            hrt.anchorMin = new Vector2(0.5f, 0.5f);
            hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = new Vector2(24f, -40f);
            hrt.sizeDelta = new Vector2(20f, 20f);
            hrt.localScale = Vector3.one;
            var himg = heartGo.AddComponent<Image>();
            himg.sprite = heartSp;
            himg.raycastTarget = false;
            himg.preserveAspect = true;
            count++;

            // 百分比文本：44x24，爱心右侧（初值，主人可调）
            var pctGo = new GameObject("FavorPct", typeof(RectTransform));
            var prt = pctGo.GetComponent<RectTransform>();
            prt.SetParent(row, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(52f, -40f);
            prt.sizeDelta = new Vector2(44f, 24f);
            prt.localScale = Vector3.one;
            var txt = pctGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 1f, 1f, 1f);
            txt.text = "0%";
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            count++;

            Debug.Log($"[MercFavorBatch3] + {row.name}/FavorHeart + FavorPct (sprite={(heartSp != null ? heartSp.name : "null")}, font={(font != null ? font.name : "null")})");
        }
        return count;
    }
}
