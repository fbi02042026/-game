using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「开始游戏」居中大字（2026-09-30 新增）。
///
/// 用法（2026-09-30 加入）：抽完奖点「继续」之后，屏幕上先出现「开始游戏」的字，再正式开打。
/// ⚠【2026-10-07 停用·并纠正归因】本类已无调用点（BattleManager 里的调用已移除），保留待清理。
///    原注释写着「主人要求」，但主人明确否认要求过加这个字 —— 是当时的误记，已撤掉该说法。
/// 动画复用项目现成的 <see cref="UiBannerPopAnim.CoPlayWaveIncoming"/>（砸入 → 停留 → 淡出 ≈1.26 秒），
/// 不新造动画。运行时建树、播完自毁，不依赖预制体。
///
/// 🔴 两层防挡点击：不加 GraphicRaycaster（Canvas 层不参与射线），且底图 raycastTarget=false。
///    否则这 1.26 秒会把玩家的点击全吞掉。
/// </summary>
public class StageStartBannerUI : MonoBehaviour
{
    const int FontSize = 76;
    const float BoardW = 560f;
    const float BoardH = 160f;
    const float BoardY = 60f;

    /// <summary>播一次「开始游戏」；传入其它文案也能用。</summary>
    public static System.Collections.IEnumerator CoPlay(string text)
    {
        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null)
        {
            Debug.LogError("[StageStartBanner] 找不到 UI 父节点，开始游戏提示不显示");
            yield break;
        }

        var go = new GameObject("StageStartBanner", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(parent, false);

        // 必须高于抽奖面板(300) 与战斗 HUD(105)，否则会被盖住
        var canvasSelf = go.GetComponent<Canvas>();
        canvasSelf.overrideSorting = true;
        canvasSelf.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        canvasSelf.sortingOrder = 400;

        var root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(CanvasRenderer),
                                     typeof(Image), typeof(CanvasGroup));
        boardGo.transform.SetParent(go.transform, false);
        var boardRt = boardGo.GetComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = new Vector2(0f, BoardY);
        boardRt.sizeDelta = new Vector2(BoardW, BoardH);

        var board = boardGo.GetComponent<Image>();
        board.color = new Color(0.06f, 0.05f, 0.08f, 0.55f);
        board.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(boardGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var label = textGo.GetComponent<Text>();
        label.text = text;
        label.fontSize = FontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.94f, 0.78f, 1f);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var font = GameFonts.GetChinese();
        if (font != null) label.font = font;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        var group = boardGo.GetComponent<CanvasGroup>();
        yield return UiBannerPopAnim.CoPlayWaveIncoming(board, group);

        Destroy(go);
    }
}
