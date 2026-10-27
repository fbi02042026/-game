using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 展示与绘制：佣兵详情弹窗跳转、图标缓存与控件工厂（Text/Image/Button/Rect 工具）。
/// 【2026-10-23 主人拍板】原 MercDrawPoolUI.cs 已 818 行（项目约定 >600 行必须拆），
/// 按职责再切一块，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    /// 佣兵详情：直接用战斗里的「获得佣兵」弹窗（huodeyongbing，信息最全，2026-10-16 主人拍板）。
    /// 已拥有的佣兵带存档里的等级/星级；未获得的按 1 级 1 星展示，缺数据时弹窗自己 Close，不另写第二份。
    /// 【2026-10-18 主人拍板】showReplace=true 时弹窗右下显示「替换」钮（仅佣兵池的未入队已获得佣兵用）。
    /// </summary>
    static void ShowMercDetail(string hireId, MercRosterDefs.Def def,
        bool showReplace = false, System.Action onReplace = null)
    {
        int level = 1, star = 1;
        var data = SaveSystem.Instance?.Data;
        if (data?.permanentMercs != null)
        {
            for (int i = 0; i < data.permanentMercs.Count; i++)
            {
                var m = data.permanentMercs[i];
                if (m == null) continue;
                if (m.mercId == hireId || m.mercId == def.AssetId)
                {
                    level = Mathf.Max(1, m.level);
                    star = Mathf.Clamp(m.star < 1 ? 1 : m.star, 1, 5);
                    break;
                }
            }
        }
        MercObtainPopupUI.ShowFromEntry(hireId, def.AssetId, level, star, showReplace, onReplace);
    }

    // ============================================================
    // 构件
    // ============================================================

    /// <summary>复用美术资源目录（Resources 相对路径前缀）。2026-10-15 主人拍板：优先用项目能复用的资源。</summary>
    const string ArtCommon = "UI/Art/Common/";

    static readonly Dictionary<string, Sprite> _artCache = new Dictionary<string, Sprite>();

    /// <summary>按 Resources 相对路径取 sprite（带缓存；取不到返回 null，调用方回落纯色，不静默）。</summary>
    static Sprite Art(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;
        if (_artCache.TryGetValue(resPath, out var c) && c != null) return c;
        var sp = Resources.Load<Sprite>(resPath);
        _artCache[resPath] = sp;
        return sp;
    }

    /// <summary>给 MakeBtn 生成的按钮换复用素材钮底（Simple + 白底；取不到保持原木色）。</summary>
    static void SetBtnSprite(Button btn, string resPath)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img == null) return;
        var sp = Art(resPath);
        if (sp == null) return;
        img.sprite = sp;
        img.type = Image.Type.Simple;
        img.color = Color.white;
    }

    /// <summary>
    /// 稀有度降序（传说 → 稀有 → 普通），同档双重排序【2026-10-20 主人拍板】：
    /// 第二键 = 获得顺序（<see cref="SaveData.mercUnlockOrder"/>，早获得在前）；
    /// 无获得记录的（旧档存量 / 未获得列表）按花名册表序排在该档末尾。
    /// 仅用于下方图鉴列表（已获得/未获得段）；雇佣队列【2026-10-20 主人拍板】不再走本方法，
    /// 改为按玩家放置顺序（SaveData.mercDrawPool 保序 List）展示。
    /// </summary>
    static List<string> SortByRarity(HashSet<string> ids)
    {
        // 获得顺序表：HireId → 首次下标（越早越小）；重复条目取最小下标（幂等记录不会重复，防御）
        var orderMap = new Dictionary<string, int>();
        var order = SaveSystem.Instance?.Data?.mercUnlockOrder;
        if (order != null)
            for (int i = 0; i < order.Count; i++)
            {
                var e = order[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (!orderMap.TryGetValue(e.id, out int prev) || i < prev) orderMap[e.id] = i;
            }

        var result = new List<string>();
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        for (int pass = 0; pass < 3; pass++)
        {
            var tier = pass == 0 ? MercRosterDefs.MercRarity.Legendary
                : pass == 1 ? MercRosterDefs.MercRarity.Rare
                : MercRosterDefs.MercRarity.Common;
            // 同档键：有获得记录用记录下标；无记录 = 大数偏移保持表序（i 越大键越大，稳定垫底不互相乱序）
            var keyed = new List<KeyValuePair<string, long>>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Rarity != tier || !ids.Contains(all[i].HireId)) continue;
                long key = orderMap.TryGetValue(all[i].HireId, out int idx)
                    ? idx
                    : long.MaxValue - (all.Count - i);
                keyed.Add(new KeyValuePair<string, long>(all[i].HireId, key));
            }
            keyed.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < keyed.Count; i++) result.Add(keyed[i].Key);
        }
        return result;
    }

    static Text MakeText(Transform parent, string name, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Button MakeBtn(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 48f);
        go.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.20f, 1f);
        var t = MakeText(go.transform, "Label", label, 20);
        t.raycastTarget = false;
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(onClick);
        return go.GetComponent<Button>();
    }

    /// <summary>Button 没有 rectTransform 属性（Graphic 才有），从 transform 取（2026-10-13 编译教训）。</summary>
    static RectTransform RtOf(Component c)
    {
        return c != null ? c.transform as RectTransform : null;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

