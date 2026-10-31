using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-31 主人拍板】战斗形象头顶的<b>称号</b>：同时只显示一个，颜色按稀有度。
///
/// <para><b>挂哪</b>：挂在 <see cref="PlayerPowerHud"/> 的头顶世界画布根下 —— 复用同一套
/// 「跟住 Hero 头顶」的跟随逻辑，不另建 Canvas、不另算头顶高度。
/// 画布根中心 = 头顶 + HeadGap，称号从那里往上长；「战力提升」飘字锚在画布顶部，
/// 两者一个贴头、一个在上方，不打架。</para>
///
/// <para><b>显示哪一个 / 什么颜色</b>：全由 <see cref="TitleSystem"/> 决定（规则：稀有度最高优先、
/// 同级取最新），本类只负责把结果画出来。没有称号就整节点隐藏，不显示空框。</para>
/// </summary>
public class PlayerTitleHud : MonoBehaviour
{
    static PlayerTitleHud _instance;

    Text _text;

    /// <summary>字号（像素；头顶画布是 100px = 1 世界单位）。</summary>
    const int FontSize = 26;
    /// <summary>称号底边离画布根中心（= 头顶锚点）的额外抬高（像素）。</summary>
    const float BottomLift = 4f;
    /// <summary>文本框宽（像素，只是排版用，超长靠 Overflow 溢出）。</summary>
    const float BoxWidth = 280f;

    /// <summary>建/接称号节点。可重复调用：已建好只换父节点 + 刷新文案。</summary>
    public static void Ensure(RectTransform headRoot)
    {
        if (headRoot == null) return;

        if (_instance == null)
        {
            var go = new GameObject("PlayerTitle",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(headRoot, false);
            _instance = go.AddComponent<PlayerTitleHud>();
            _instance.Build();
        }
        else if (_instance.transform.parent != headRoot)
        {
            _instance.transform.SetParent(headRoot, false);
        }

        TitleSystem.EnsureActive();   // 老档自愈：解锁过称号但没记 activeTitleId 的存档
        Refresh();
    }

    void Build()
    {
        _text = GetComponent<Text>();
        _text.fontSize = FontSize;
        _text.alignment = TextAnchor.MiddleCenter;
        _text.raycastTarget = false;
        _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        var f = GameFonts.GetChinese();
        if (f != null) _text.font = f;

        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);          // 底边贴锚点，往上长
        rt.anchoredPosition = new Vector2(0f, BottomLift);
        rt.sizeDelta = new Vector2(BoxWidth, FontSize + 12f);

        // 描边用全局稀有度描边色，保证任何底色上都读得清
        var ol = GetComponent<Outline>();
        ol.effectColor = RarityPalette.Outline;
        ol.effectDistance = new Vector2(2.2f, -2.2f);

        gameObject.SetActive(false);
    }

    void Apply()
    {
        if (_text == null) return;
        if (!TitleSystem.HasActive)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }
        _text.text = TitleSystem.ActiveName;
        _text.color = TitleSystem.ActiveColor;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    /// <summary>称号变了（解锁 / 切换 / 老档自愈）→ 刷新头顶。不在战斗里就空操作。</summary>
    public static void Refresh()
    {
        if (_instance != null) _instance.Apply();
    }

    /// <summary>离开战斗时收掉（跟 <see cref="PlayerPowerHud"/> 的画布一起隐藏）。</summary>
    public static void Hide()
    {
        if (_instance != null && _instance.gameObject.activeSelf)
            _instance.gameObject.SetActive(false);
    }
}
