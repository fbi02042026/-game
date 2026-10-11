using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗 buff 条：挂在玩家头像「战斗力」右侧（中间隔开一段距离），让玩家一眼看到
/// 现在身上有什么 buff。2026-10-11 主人拍板新增。
///
/// <para>显示两类：</para>
/// <para>① 计时 buff（技能给的 攻/防/暴 倍率，<see cref="AttrSystem.CollectActiveTimedBuffs"/> 唯一数据出口）：
/// 图标下方带**数字倒计时**，到 0 消失；剩 20% 时间起**图标整体闪烁**提醒。</para>
/// <para>② 常驻 buff（<c>BattleManager.tempBuffs</c>：局内强化卡加的 体/速/攻）：只亮牌、无倒计时。</para>
///
/// <para>⚠ 图标资源：项目目前**没有 buff 美术图**，先用「色块 + 汉字（攻/防/暴/体/速）」顶位 ——
/// 主人给了图后只换 <see cref="BuffStyle.Sprite"/>，其余逻辑不动。</para>
/// </summary>
public class BuffHudUI : MonoBehaviour
{
    public static BuffHudUI Instance { get; private set; }

    const float IconSize = 44f;
    const float Gap = 6f;
    const float RefreshInterval = 0.25f;
    /// <summary>剩余时间 < total×这个比例 时开始整体闪烁（主人拍板 20%）。</summary>
    const float BlinkRemainK = 0.2f;

    class BuffIcon
    {
        public GameObject Root;
        public Image Bg;
        public Text Mark;
        public Text Cd;
        public CanvasGroup Cg;
    }

    readonly List<BuffIcon> _pool = new List<BuffIcon>();
    readonly List<AttrSystem.ActiveTimedBuff> _timed = new List<AttrSystem.ActiveTimedBuff>();
    readonly HashSet<AttrType> _seen = new HashSet<AttrType>();
    float _timer;

    struct BuffStyle
    {
        public string Mark;
        public Color Color;
        public Sprite Sprite;   // 主人补图后填这里；null = 色块 + 汉字
    }

    static BuffStyle StyleOf(AttrType t)
    {
        switch (t)
        {
            case AttrType.Attack:   return new BuffStyle { Mark = "攻", Color = new Color(0.85f, 0.32f, 0.28f), Sprite = null };
            case AttrType.Defense:  return new BuffStyle { Mark = "防", Color = new Color(0.36f, 0.60f, 0.92f), Sprite = null };
            case AttrType.CritRate: return new BuffStyle { Mark = "暴", Color = new Color(0.95f, 0.78f, 0.28f), Sprite = null };
            case AttrType.MaxHp:    return new BuffStyle { Mark = "体", Color = new Color(0.36f, 0.80f, 0.48f), Sprite = null };
            case AttrType.AttackSpeed: return new BuffStyle { Mark = "速", Color = new Color(0.36f, 0.78f, 0.78f), Sprite = null };
            default:                return new BuffStyle { Mark = "益", Color = new Color(0.7f, 0.7f, 0.75f), Sprite = null };
        }
    }

    /// <summary>
    /// 挂到玩家槽「战斗力」右侧。重复调用幂等。
    /// 战斗力节点从 <see cref="CharacterSlotUI.powerDigitSlots"/> 的父级取（预制体摆好的美术节点，代码不挪它）。
    /// </summary>
    public static void Ensure(CharacterSlotUI playerSlot)
    {
        if (Instance != null) return;
        if (playerSlot == null || playerSlot.root == null) return;
        if (playerSlot.powerDigitSlots == null || playerSlot.powerDigitSlots.Length == 0
            || playerSlot.powerDigitSlots[0] == null)
            return;   // 佣兵槽没有战力层；玩家槽没摆战力数字时不建（不兜底造节点）

        var powerNode = (RectTransform)playerSlot.powerDigitSlots[0].transform.parent;
        var go = new GameObject("BuffHud", typeof(RectTransform));
        go.transform.SetParent(powerNode.parent, false);
        var rt = go.GetComponent<RectTransform>();
        // 与战斗力同锚同参考，整体向右平移（战力节点宽的一半 + 60 的间隔，主人要「中间隔一段距离」）
        rt.anchorMin = powerNode.anchorMin;
        rt.anchorMax = powerNode.anchorMax;
        rt.pivot = powerNode.pivot;
        rt.anchoredPosition = powerNode.anchoredPosition
                              + new Vector2(powerNode.rect.width * 0.5f + 60f, 0f);
        rt.sizeDelta = new Vector2((IconSize + Gap) * 8f, IconSize);

        Instance = go.AddComponent<BuffHudUI>();
    }

    /// <summary>由 BattleUI.Update 的 0.1s 节流段驱动；内部再 0.25s 节流刷新内容。</summary>
    public static void Tick()
    {
        if (Instance == null) return;
        // 上一局残留：Hero/战斗管理器都没了就整条收掉
        if (Hero.Instance == null || BattleManager.Instance == null)
        {
            Instance.gameObject.SetActive(false);
            return;
        }
        Instance.gameObject.SetActive(true);
        Instance._timer += Time.unscaledDeltaTime;
        if (Instance._timer < RefreshInterval) return;
        Instance._timer = 0f;
        Instance.Refresh();
    }

    /// <summary>局内 buff 集合变了（抽了强化卡等）由外部喊一嗓子，下一拍立刻重排常驻牌。</summary>
    public static void MarkTempDirty() { /* tempBuffs 只增不减，每拍直接重读，无需缓存失效 */ }

    void Refresh()
    {
        int idx = 0;

        // ① 计时 buff（带倒计时 / 快到期闪烁）
        var hero = Hero.Instance;
        if (hero != null && hero.attr != null)
        {
            hero.attr.CollectActiveTimedBuffs(_timed);
            for (int i = 0; i < _timed.Count; i++)
            {
                var b = _timed[i];
                var icon = GetIcon(idx++);
                var st = StyleOf(b.type);
                icon.Root.SetActive(true);
                icon.Bg.color = st.Color;
                icon.Mark.text = st.Mark;
                icon.Cd.gameObject.SetActive(true);
                icon.Cd.text = Mathf.CeilToInt(Mathf.Max(0f, b.remain)).ToString();

                // 剩 20% 时间 → 图标整体闪烁（主人拍板）；归 0 由上面 Collect 自动剔除 = 消失
                bool blink = b.total > 0f && b.remain <= b.total * BlinkRemainK;
                float a = blink ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)) : 1f;
                icon.Cg.alpha = a;
            }
        }

        // ② 常驻 buff（本局强化卡）：只亮牌、无倒计时
        var bm = BattleManager.Instance;
        if (bm != null && bm.tempBuffs != null)
        {
            _seen.Clear();
            for (int i = 0; i < bm.tempBuffs.Count; i++)
            {
                var b = bm.tempBuffs[i];
                if (b == null || !_seen.Add(b.attrType)) continue;   // 同类只亮一张牌
                var icon = GetIcon(idx++);
                var st = StyleOf(b.attrType);
                icon.Root.SetActive(true);
                icon.Bg.color = st.Color;
                icon.Mark.text = st.Mark;
                icon.Cd.gameObject.SetActive(false);
                icon.Cg.alpha = 1f;
            }
        }

        // 多余的收起来
        for (int i = idx; i < _pool.Count; i++)
            _pool[i].Root.SetActive(false);
    }

    BuffIcon GetIcon(int index)
    {
        while (_pool.Count <= index)
        {
            var go = new GameObject("Buff" + _pool.Count, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(_pool.Count * (IconSize + Gap), 0f);
            rt.sizeDelta = new Vector2(IconSize, IconSize);

            var bg = go.AddComponent<Image>();
            bg.raycastTarget = false;

            var markGo = new GameObject("Mark", typeof(RectTransform), typeof(Text), typeof(Outline));
            markGo.transform.SetParent(go.transform, false);
            var mark = markGo.GetComponent<Text>();
            mark.fontSize = 22;
            mark.alignment = TextAnchor.MiddleCenter;
            mark.color = Color.white;
            mark.raycastTarget = false;
            mark.font = GameFonts.GetChinese();
            markGo.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var mrt = markGo.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.05f, 0.22f);
            mrt.anchorMax = new Vector2(0.95f, 0.95f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;

            var cdGo = new GameObject("Cd", typeof(RectTransform), typeof(Text), typeof(Outline));
            cdGo.transform.SetParent(go.transform, false);
            var cd = cdGo.GetComponent<Text>();
            cd.fontSize = 18;
            cd.alignment = TextAnchor.MiddleCenter;
            cd.color = new Color(1f, 0.96f, 0.8f);
            cd.raycastTarget = false;
            cd.font = GameFonts.GetChinese();
            cdGo.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            var crt = cdGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(-0.15f, -0.42f);
            crt.anchorMax = new Vector2(1.15f, 0.12f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            _pool.Add(new BuffIcon
            {
                Root = go,
                Bg = bg,
                Mark = mark,
                Cd = cd,
                Cg = go.GetComponent<CanvasGroup>()
            });
        }
        return _pool[index];
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
