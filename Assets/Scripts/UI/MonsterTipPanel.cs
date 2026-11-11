using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物信息弹窗唯一组件（冒险日志怪物 Tip + 战斗内词条怪首次登场提示共用）：
/// 宽 470、高度按正文自动算，点弹窗任意处关闭。
///
/// 【2026-11-03 主人拍板「弹窗跟图鉴那个用的是一个就行」】合并成共享组件。
/// 【2026-11-11 主人拍板】放弃纯黑底手搓版，改用冒险日志那套「信息框」窗体皮
/// （Resources/UI/Art/冒险日志/信息框，九宫格原尺寸铺，**不加缩放**——它的顶部标题板是凸起造型，
///  一缩放就被压扁、标题板与宝石错位；见 <see cref="FrameBorderScale"/> 注释）。
/// 【2026-11-11 主人拍板「注意排版 主要说明词条和弱点」+「为什么一行还没写完就在第二行写了」】
/// 版式重做：正文左对齐、段间空行、宽度放到 470（正文净宽 398），
/// 22 号字一行放得下 18 个汉字——最难的那句「荆棘：被近战攻击反弹 15% 伤害」（297px）也在一行内，
/// 不再出现「前半句一行、半个词掉下一行」的断行。高度仍按正文实测行数自动加高。
/// - 静态单例挂宿主 Transform 下；宿主随场景销毁后自动重建（Unity 伪空判定）；
/// - onClose：点弹窗关闭时的回调（图鉴为 null；词条提示用它做「看过永久消失」标记）。
/// </summary>
public static class MonsterTipPanel
{
    const string LogTag = "[MonsterTip] ";

    // === 窗体皮（2026-11-11 主人指定「信息框.png」；56 字符 guid 不走引用，按 Resources 路径加载）===
    const string SpriteWindow = "UI/Art/冒险日志/信息框";

    /// <summary>
    /// 九宫格边宽缩放系数 = 1（不缩放）。
    /// 信息框原生 505×400，spriteBorder 63/36/65/113；顶部 113px 那块凸起的标题板就是标题底板，
    /// 一旦调小（>1 就是缩小边宽）标题板会被纵向压扁、顶部宝石与金饰错位——所以整张图按原尺寸铺，
    /// 面板宽度靠 <see cref="Width"/> 控制，只拉伸中间的平色区域。
    /// </summary>
    const float FrameBorderScale = 1f;

    static GameObject _root;
    static Text _title;
    static Text _body;
    static Action _onClose;
    static Transform _host;

    // === 版式（2026-11-11：宽度固定、高度按正文自动算）===
    const float Width = 470f;        // 弹窗固定宽（图鉴与战斗提示同宽）
    const float MinHeight = 236f;    // 正文极短时的下限，不让窗体缩成一条
    const float BodyTop = 122f;      // 窗体顶 → 正文起点（顶部 113px 是标题板，留 9px 缝）
    const float BodyBottom = 50f;    // 正文底 → 窗体底（下边框 36px + 14px 呼吸）
    const float BodyPadX = 36f;      // 正文左右内缩（与标题板左右边对齐；20 号字正文净宽 = 470-72 = 398）

    /// <summary>当前弹窗根（可能为已销毁伪空，调用方自行判空）。</summary>
    public static RectTransform RootRt => _root != null ? _root.transform as RectTransform : null;

    /// <summary>确保弹窗已构建（窗体皮与冒险日志信息框一致）。
    /// 宿主校验：若旧弹窗还挂在别的宿主下（如城镇 DDOL 页 vs 战斗内 Canvas），就地销毁迁到新宿主重建。</summary>
    public static GameObject Ensure(Transform host)
    {
        if (_root != null && _host != host)
        {
            UnityEngine.Object.Destroy(_root);   // 场景/宿主变了：老树下不结果，迁址重建
            _root = null;
        }
        if (_root != null) return _root;
        if (host == null) return null;
        _host = host;

        // —— 底框：信息框（九宫格，原尺寸）——
        var go = new GameObject("MonsterTip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Width, MinHeight);
        rt.anchoredPosition = Vector2.zero;
        var bg = go.GetComponent<Image>();
        bg.sprite = LoadWindowSprite(SpriteWindow, "底框");
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = FrameBorderScale;
        bg.color = Color.white;      // 窗体皮自带配色，不再压深色

        // —— 标题：落在信息框自带的顶部标题板正中（板心离窗体顶约 60px）——
        var titleGo = new GameObject("TipTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(go.transform, false);
        var tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1);
        tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -43);          // 2026-11-11 主人「名字往下5」：-38 → -43（板心 ≈ y65）
        tr.sizeDelta = new Vector2(-BodyPadX * 2f, 44);      // 28 号字 × 行距 1.3 ≈ 36，框高 44 不会裁字
        _title = titleGo.GetComponent<Text>();
        _title.fontSize = 28;            // 2026-11-11 主人「怪物的名字可以大一点」：22→28，正好填满标题板且不顶板上沿
        _title.alignment = TextAnchor.MiddleCenter;
        _title.color = new Color(1f, 0.92f, 0.7f);
        _title.font = GameFonts.GetChinese();
        _title.raycastTarget = false;

        // —— 正文：留在窗体内框以内，左对齐（多行说明左对齐比居中好读）——
        var bodyGo = new GameObject("TipBody", typeof(RectTransform), typeof(Text));
        bodyGo.transform.SetParent(go.transform, false);
        var br = bodyGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 1);
        br.offsetMin = new Vector2(BodyPadX, BodyBottom);
        br.offsetMax = new Vector2(-BodyPadX, -BodyTop);
        _body = bodyGo.GetComponent<Text>();
        _body.fontSize = 22;             // 2026-11-11 主人两次「信息的字再大2号」：18 → 20 → 22（正文净宽 398 仍放得下最长句）
        _body.alignment = TextAnchor.UpperLeft;
        _body.lineSpacing = 1.25f;       // 段内行距；段间靠空行（内容侧统一，见 MonsterTipContent）
        _body.color = new Color(0.9f, 0.85f, 0.75f);
        _body.font = GameFonts.GetChinese();
        _body.raycastTarget = false;
        _body.supportRichText = true;    // 词条/弱点标签行用 <color> 标色（MonsterTipContent 唯一出口）
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;   // 超出正文净宽才换行（净宽 382，够放最长句）
        _body.verticalOverflow = VerticalWrapMode.Overflow;   // 高度由 Show 按实测行数撑，不裁字

        var closeBtn = go.AddComponent<Button>();
        closeBtn.targetGraphic = bg;
        closeBtn.onClick.AddListener(OnClicked);

        GameFonts.ApplyToHierarchy(go.transform);
        _root = go;
        go.SetActive(false);
        return _root;
    }

    /// <summary>按路径取窗体皮；取不到 LogError 不兜底（不看空图装没事）。</summary>
    static Sprite LoadWindowSprite(string path, string what)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError(LogTag + what + " 取不到：Resources/" + path + "（不兜底）");
        return sp;
    }

    /// <summary>
    /// 显示弹窗（幂等：重复调用覆盖内容）。
    /// 宽度固定 <see cref="Width"/>，**高度按正文实际行数自动算**——
    /// 调用方不再传尺寸，内容多（词条 2 条 + 弱点说明）也不会溢出窗体、内容少也不会留一大块空底。
    /// </summary>
    public static void Show(Transform host, string title, string body, Action onClose = null)
    {
        var root = Ensure(host);
        if (root == null) return;
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Width, MinHeight);   // 先定宽（正文量高只跟宽度有关），高待量完再定
        if (_title != null) _title.text = title ?? "";
        if (_body != null) _body.text = body ?? "";
        _onClose = onClose;
        root.SetActive(true);                            // 先显示再量（Text 量高不依赖激活态，但顺序更稳）
        if (_body != null)
        {
            float bodyH = _body.preferredHeight;
            rt.sizeDelta = new Vector2(Width, Mathf.Max(MinHeight, BodyTop + bodyH + BodyBottom));
        }
    }

    public static void Hide()
    {
        if (_root != null) _root.SetActive(false);
        _onClose = null;
    }

    static void OnClicked()
    {
        var cb = _onClose;
        Hide();
        cb?.Invoke();   // 先 Hide 再回调，回调里再 Show 也不会被旧状态干扰
    }
}
