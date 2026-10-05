using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备替换确认弹窗（2026-10-05 主人拍板）。
///
/// 主人原话：「只会抽中高级的直接替换低级的，然后低级的变成材料，不过需要弹出个弹窗告诉玩家是否替换」。
/// → 拿到新装备、且同部位已经有旧件时，**先把新旧摆在一起给玩家看**，由玩家决定：
///   · 换上新的 → 新件入包，旧件走 <see cref="GridBackpackSystem.ScrapEquip"/> 折成强化石（低级的变成材料）
///   · 留着旧的 → 新件同样折成强化石（不占背包，也不白白吞掉这次奖励）
///
/// <para>全程**运行时建树，不碰任何 prefab**。沿用项目惯例：挂在 BattleUI 下做子 Canvas
/// （<see cref="GameConfig.SORT_POPUP"/> + overrideSorting），并按铁律补 <c>GraphicRaycaster</c>。
/// 强弱判据只有 <see cref="EquipCompare"/> 一处，这里只负责显示。</para>
///
/// <para>玩家一直不点时**默认「留着旧的」** —— 保守方向，绝不会弄丢玩家身上已有的装备。</para>
/// </summary>
public class EquipReplaceConfirmUI : MonoBehaviour
{
    /// <summary>玩家多久不点就按「留着旧的」处理（防协程永远挂着）。</summary>
    const float TimeoutSec = 60f;

    const float PanelW = 640f;
    const float PanelH = 470f;

    static EquipReplaceConfirmUI _inst;
    GameObject _root;
    System.Action<bool> _onDone;
    bool _answered;
    bool _answer;

    /// <summary>
    /// 弹窗并等玩家选择。超时或异常都返回 false（= 留着旧的）。
    /// 调用方用 <c>yield return CoShow(...)</c> 接住。
    /// </summary>
    public static IEnumerator CoShow(EquipInstance newEq, EquipInstance oldEq, System.Action<bool> onDone)
    {
        var ui = Ensure();
        if (ui == null)
        {
            // UI 建不起来就按「留着旧的」放行 —— 绝不能卡住抽奖协程
            Debug.LogError("[EquipReplaceConfirm] 弹窗建不起来，按「留着旧的」处理");
            onDone?.Invoke(false);
            yield break;
        }

        ui._answered = false;
        ui._answer = false;
        ui.Build(newEq, oldEq);

        float t = 0f;
        while (!ui._answered && t < TimeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        bool result = ui._answered ? ui._answer : false;
        ui.Hide();
        onDone?.Invoke(result);
    }

    static EquipReplaceConfirmUI Ensure()
    {
        if (_inst != null) return _inst;

        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null)
        {
            Debug.LogError("[EquipReplaceConfirm] 找不到 UI 父节点");
            return null;
        }

        var go = new GameObject("EquipReplaceConfirm", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        _inst = go.AddComponent<EquipReplaceConfirmUI>();
        return _inst;
    }

    void Build(EquipInstance newEq, EquipInstance oldEq)
    {
        if (_root != null) Destroy(_root);
        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);

        var rootRt = _root.GetComponent<RectTransform>();
        Stretch(rootRt);
        // 独立排序层：压在所有战斗 HUD / 特效之上，否则按钮点不到
        var c = _root.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        c.sortingOrder = GameConfig.SORT_POPUP;
        if (_root.GetComponent<GraphicRaycaster>() == null)
            _root.AddComponent<GraphicRaycaster>();

        // 遮罩：吃掉点击，避免穿透到下面的抽奖面板
        var veil = CreateImage("Veil", _root.transform, new Color(0f, 0f, 0f, 0.62f));
        Stretch(veil.rectTransform);

        var panel = CreateImage("Panel", _root.transform, new Color(0.10f, 0.11f, 0.14f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(PanelW, PanelH);

        var title = CreateText("Title", panel.transform, "换上新装备吗？", 30, new Color(1f, 0.92f, 0.72f));
        SetRect(title.rectTransform, new Vector2(0f, -18f), new Vector2(PanelW - 40f, 40f));

        string verdict = EquipCompare.Verdict(newEq, oldEq);
        bool newBetter = EquipCompare.Compare(newEq, oldEq) > 0;
        var vd = CreateText("Verdict", panel.transform, verdict, 26,
            newBetter ? new Color(0.55f, 0.95f, 0.60f) : new Color(0.95f, 0.72f, 0.45f));
        SetRect(vd.rectTransform, new Vector2(0f, -62f), new Vector2(PanelW - 40f, 34f));

        // 两列对比：左 = 抽到的，右 = 现在穿的
        AddColumn(panel.transform, "New", -160f, "抽到的", newEq);
        AddColumn(panel.transform, "Old", 160f, "现在穿的", oldEq);

        var hint = CreateText("Hint", panel.transform, "不换的那件会拆成强化石，不会浪费", 20,
            new Color(0.72f, 0.72f, 0.76f));
        SetRect(hint.rectTransform, new Vector2(0f, -220f), new Vector2(PanelW - 40f, 30f));

        AddButton(panel.transform, "BtnReplace", -140f, "换上新的", new Color(0.20f, 0.55f, 0.30f), true);
        AddButton(panel.transform, "BtnKeep", 140f, "留着旧的", new Color(0.38f, 0.38f, 0.44f), false);

        GameFonts.ApplyToHierarchy(_root.transform);
        _root.SetActive(true);
    }

    void AddColumn(Transform panel, string nodeName, float x, string header, EquipInstance eq)
    {
        var col = CreateImage(nodeName, panel, new Color(1f, 1f, 1f, 0.05f));
        var rt = col.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 90f);
        rt.sizeDelta = new Vector2(300f, 290f);

        var h = CreateText("Head", col.transform, header, 24, new Color(0.85f, 0.82f, 0.70f));
        SetRect(h.rectTransform, new Vector2(0f, -10f), new Vector2(280f, 32f));

        string name = eq != null && !string.IsNullOrEmpty(eq.equipName) ? eq.equipName : "（空）";
        var nm = CreateText("Name", col.transform, name, 24, Color.white);
        SetRect(nm.rectTransform, new Vector2(0f, -48f), new Vector2(280f, 32f));

        string line = eq == null ? "-" : $"{RarityName(eq)}　★{Mathf.Max(1, eq.star)}　+{Mathf.Max(0, eq.enhanceLevel)}";
        var info = CreateText("Info", col.transform, line, 22, RarityColor(eq));
        SetRect(info.rectTransform, new Vector2(0f, -84f), new Vector2(280f, 30f));

        string desc = eq != null ? DraftPool.FormatEquipDesc(eq) : "";
        var d = CreateText("Desc", col.transform, desc, 19, new Color(0.80f, 0.80f, 0.84f));
        SetRect(d.rectTransform, new Vector2(0f, -120f), new Vector2(280f, 150f));
    }

    void AddButton(Transform panel, string nodeName, float x, string label, Color bg, bool answer)
    {
        var img = CreateImage(nodeName, panel, bg);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, -175f);
        rt.sizeDelta = new Vector2(240f, 74f);

        var btn = img.gameObject.GetComponent<Button>();
        if (btn == null) btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => Answer(answer));

        var t = CreateText("Label", img.transform, label, 26, Color.white);
        Stretch(t.rectTransform);
    }

    void Answer(bool replace)
    {
        _answer = replace;
        _answered = true;
    }

    void Hide()
    {
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
    }

    // ——— 小工具：与项目里其它运行时建 UI 的写法保持一致 ———

    static string RarityName(EquipInstance eq)
    {
        if (eq == null) return "无";
        return SkillRarityUtil.DisplayName(DraftPool.MapRarity(eq.rarity));
    }

    static Color RarityColor(EquipInstance eq)
    {
        if (eq == null) return new Color(0.6f, 0.6f, 0.6f);
        switch (DraftPool.MapRarity(eq.rarity))
        {
            case SkillRarity.Legendary: return new Color(1f, 0.62f, 0.25f);
            case SkillRarity.Epic: return new Color(0.80f, 0.55f, 1.00f);
            case SkillRarity.Rare: return new Color(0.42f, 0.72f, 0.98f);
            default: return new Color(0.85f, 0.85f, 0.88f);
        }
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(string name, Transform parent, string text, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>统一用「父级中心为基准」的中锚点（= Inspector 的 Pos Y 那把尺子），避免各处 y 正负号不一致。</summary>
    static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
