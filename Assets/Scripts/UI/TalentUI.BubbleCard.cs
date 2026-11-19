using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 左右列共用的「参考图气泡卡」建树（2026-11-19 主人拍板：点击天赋图标弹参考图同款弹窗，
/// 只改排版、素材全用项目现成——主人原话「我只是说排版 素材用的还是项目里的」）。
/// <para>结构：全屏 Dim 真模态 → 浅色羊皮纸气泡卡（无尾巴）→ 浅色胶囊标题（属性名）
/// → 描述（属性效果）→ 确定按钮（动作词）。</para>
/// <para>素材映射（2026-11-19 主人拍板「底框换浅色 按钮用确定按钮」）：
/// 卡底=AdventureLog/Frames/字底（浅米色羊皮纸九宫格）、胶囊=天赋_0006_属性底、
/// 按钮底=Common/确定按钮（左右列同一张绿底确定按钮）、
/// 货币图标=Icons/Common/icon_gold（左列金币）/天赋_0018_技能可用（右列天赋石，顶栏同款）。</para>
/// <para>口径对齐 EnsureJobChoicePopup：摄像机模式 Canvas + TownPopup 层级；只管展示与确认回调，
/// 购买/解锁执行体不在这（左列 DoBuyLeft / 右列 TryUnlockRightNode·TryUpgradeRightNode）。</para>
/// </summary>
public partial class TalentUI
{
    /// <summary>气泡卡节点引用（左/右列各持一份缓存，复用不重建）。</summary>
    struct CostBubbleRefs
    {
        public GameObject root;
        public Text name;      // 胶囊标题（属性名）
        public Text desc;      // 描述（属性效果）
        public Text okLabel;   // 按钮文字（解锁/升级）
        public Text cost;      // 资源行（持有/需求，不足标红）
        public Image head;     // 顶部大图标（与节点同款）
        public RectTransform box;   // 卡片本体（贴图标定位时移动）
    }

    // 【参考图排版】卡尺寸与尾巴落位只在这里改一次，建树与贴图标定位共用（禁止第二处硬编码）。
    const float BubbleCardW = 340f;
    const float BubbleCardH = 400f;
    // 【2026-11-19 主人拍板】气泡卡整体缩小一半：只缩 box 的 localScale，内部相对布局（字号/间距）原样保留。
    const float BubbleCardScale = 0.55f;
    // 菱形尾巴已去掉；卡心距被点图标顶 = 缩放后半高（卡底仍贴图标上方 8px 间隙，与缩前同口径）。
    const float BubbleTailDrop = BubbleCardH * BubbleCardScale * 0.5f;

    // 素材懒加载缓存（实例字段，域重载自动清空后重载）
    Sprite _bubbleCardSpr;      // 卡底（字底）
    Sprite _bubblePillSpr;      // 胶囊（属性底）
    Sprite _bubbleGoldBtnSpr;   // 金币升级按钮
    Sprite _bubbleGoldIconSpr;  // 金币图标
    Sprite _bubbleStoneBtnSpr;  // 天赋石升级按钮
    Sprite _bubbleStoneIconSpr; // 天赋石图标（顶栏同款 sprDiamondOn）

    // 卡片上的字色（卡底换浅色羊皮纸 → 卡内字统一深墨；沿用 BubbleTextOnNavy 引用名，值改为深墨，胶囊标题同色）
    static readonly Color BubbleInk = new Color(0.42f, 0.32f, 0.20f, 1f);
    static readonly Color BubbleTextOnNavy = BubbleInk;
    static readonly Color BubbleCostRed = new Color(1f, 0.32f, 0.28f, 1f);

    Sprite BubbleSprite(ref Sprite field, string resPath, string label)
    {
        if (field != null) return field;
        field = Resources.Load<Sprite>(resPath);
        if (field == null) Debug.LogError("[TalentUI] 气泡卡素材缺失: " + label + " (" + resPath + ")");
        return field;
    }

    /// <summary>懒加载气泡卡全套素材（缺图只 LogError 不兜底——按项目规矩露白框或干脆不建卡）。</summary>
    void EnsureBubbleSprites()
    {
        // 【2026-11-19 主人拍板】卡底=浅色羊皮纸框（AdventureLog/Frames/字底），按钮左右列统一=确定按钮
        _bubbleCardSpr = BubbleSprite(ref _bubbleCardSpr, "UI/AdventureLog/Frames/字底", "气泡卡底");
        _bubblePillSpr = BubbleSprite(ref _bubblePillSpr, "UI/Talent/天赋_0006_属性底", "标题胶囊");
        _bubbleGoldBtnSpr = BubbleSprite(ref _bubbleGoldBtnSpr, "UI/Art/Common/确定按钮", "确定按钮");
        _bubbleGoldIconSpr = BubbleSprite(ref _bubbleGoldIconSpr, "UI/Icons/Common/icon_gold", "金币图标");
        _bubbleStoneBtnSpr = BubbleSprite(ref _bubbleStoneBtnSpr, "UI/Art/Common/确定按钮", "确定按钮");
        if (_bubbleStoneIconSpr == null)
            _bubbleStoneIconSpr = sprDiamondOn != null
                ? sprDiamondOn
                : Resources.Load<Sprite>("UI/Talent/天赋_0018_技能可用");
        if (_bubbleStoneIconSpr == null)
            Debug.LogError("[TalentUI] 气泡卡素材缺失: 天赋石图标 (UI/Talent/天赋_0018_技能可用)");
    }

    /// <summary>
    /// 懒建气泡卡：已建过直接复用（Unity 判空，销毁后 root == null）；域重载后字段归零但节点可能残留，
    /// 先探测旧节点销毁再重建（原 LeftNodeCompareCard 防重影口径）。
    /// 建完自动 SetActive(false)，显隐由调用方管。
    /// </summary>
    CostBubbleRefs EnsureCostBubble(string rootName, CostBubbleRefs cached, UnityAction onDimClose, UnityAction onOk,
        Sprite btnSpr, Sprite coinSpr)
    {
        if (cached.root != null) return cached;   // 已建，复用（不重建不重绑）

        // 防重影：域重载后引用归零但节点可能残留
        var existing = transform.Find(rootName);
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
                Destroy(existing.GetChild(i).gameObject);
            Destroy(existing.gameObject);
        }

        EnsureBubbleSprites();
        if (_bubbleCardSpr == null)
        {
            Debug.LogError("[TalentUI] 气泡卡卡底素材缺失，弹不出气泡卡: " + rootName);
            return cached;   // root == null，调用方判空静默（不退化成直接买）
        }

        var root = CreateRect(transform, rootName);
        Stretch(root.GetComponent<RectTransform>());
        // 全屏遮罩（真模态：raycast=1，点空白处关闭）——全透明，不压暗背景。
        // 【2026-11-19 主人拍板「不要黑底」】Dim 必须建在 ApplyPopup 之前：
        // ApplyPopup → EnsureFitBackdrop 会给「无背景画布 + 瘦屏」垫全屏黑底，
        // 先建 Dim（节点名命中背景词）检测即跳过，黑底不再出现。
        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(onDimClose);

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        // 【2026-11-19 主人拍板】下方菱形尾巴去掉（节点不建；PositionCostBubble 内 tail 写值已有判空）。

        // 卡片底：字底九宫格拉成竖卡（米黄圆角带描边，参考图观感）
        var box = CreateImage(root.transform, "Box", Color.white, _bubbleCardSpr, false);
        box.type = Image.Type.Sliced;
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(BubbleCardW, BubbleCardH));
        // 【2026-11-19 主人拍板】框整体缩小一半：缩放整卡节点（内部全部子元素等比跟着缩，相对布局不动）。
        box.rectTransform.localScale = Vector3.one * BubbleCardScale;

        // ① 顶部大图标（参考图：卡片顶端放节点同款大图标）
        var head = CreateImage(box.transform, "HeadIcon", Color.white, null, true);
        SetAnchored(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -54f), new Vector2(96f, 96f));
        head.raycastTarget = false;

        // ② 浅色胶囊标题（属性名）
        var pill = CreateImage(box.transform, "Pill", Color.white, _bubblePillSpr, false);
        SetAnchored(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -128f), new Vector2(210f, 48f));
        pill.raycastTarget = false;
        var pillText = CreateText(pill.transform, "Label", "", 32, BubbleInk);
        Stretch(pillText.rectTransform);

        // ③ 描述（属性效果；右列升级时两行「Lv1 → Lv2」+效果）
        var desc = CreateText(box.transform, "DescText", "", 28, BubbleTextOnNavy);
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -196f), new Vector2(300f, 64f));

        // ④ 横向分隔线（参考图：描述与资源行之间一条细线）
        var divider = CreateImage(box.transform, "Divider", new Color(BubbleTextOnNavy.r, BubbleTextOnNavy.g, BubbleTextOnNavy.b, 0.30f));
        SetAnchored(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -240f), new Vector2(280f, 2f));
        divider.raycastTarget = false;

        // ⑤ 资源行：货币图标 + 「持有/需求」（参考图数字排法，不足由调用方标红）
        var costIcon = CreateImage(box.transform, "CostIcon", Color.white, coinSpr, true);
        SetAnchored(costIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(-74f, -268f), new Vector2(34f, 34f));
        costIcon.raycastTarget = false;
        var cost = CreateText(box.transform, "CostText", "", 30, BubbleTextOnNavy);
        SetAnchored(cost.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(12f, -268f), new Vector2(150f, 34f));

        // ⑥ 彩色按钮（确定按钮）：【2026-11-19 主人拍板「按钮太大」】260×84 → 180×60（贴合确定按钮 100×33 的 3:1 比例）
        // 【2026-11-19 主人拍板「按钮挡住金币数」】按钮从底部 y=104 下移到 y=55：按钮顶边不再压到金币行底边。
        var ok = CreateImage(box.transform, "UnlockButton", Color.white, btnSpr, false);
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f), new Vector2(180f, 60f));
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(onOk);
        var okLabel = CreateText(ok.transform, "Label", "", 38, Color.white);
        Stretch(okLabel.rectTransform);

        cached.root = root;
        cached.box = box.rectTransform;
        cached.name = pillText;
        cached.desc = desc;
        cached.okLabel = okLabel;
        cached.cost = cost;
        cached.head = head;
        root.SetActive(false);
        return cached;
    }

    /// <summary>
    /// 【第15轮·主人拍板】气泡卡贴到 anchorRect（被点的图标/行）正上方：尾巴尖指向其顶边上方 8px、
    /// 横向中心对齐；越界夹回画布内（极端时可能盖住图标，保完整可见优先）。anchorRect 为空 = 保持居中兜底。
    /// </summary>
    void PositionCostBubble(CostBubbleRefs refs, RectTransform anchorRect)
    {
        if (refs.root == null || refs.box == null || anchorRect == null) return;
        var rootRt = refs.root.transform as RectTransform;
        if (rootRt == null) return;

        // 被点节点顶边中点 → 气泡根局部坐标（同一 Canvas 世界空间换算，面板缩放/摄像机自动正确）
        Rect r = anchorRect.rect;
        var worldTop = anchorRect.TransformPoint(new Vector3(r.width * (0.5f - anchorRect.pivot.x), r.yMax, 0f));
        Vector2 top = rootRt.InverseTransformPoint(worldTop);

        // 卡尺寸与落位同源（BubbleCardW/H + BubbleTailDrop），不再各写一份；
        // 【2026-11-19 主人拍板】框整体缩小一半 → 越界钳制按缩放后的实际尺寸算，别把小卡推离图标。
        const float gap = 8f, margin = 12f;
        float halfW = BubbleCardW * BubbleCardScale * 0.5f;
        float halfH = BubbleCardH * BubbleCardScale * 0.5f;
        Vector2 want = new Vector2(top.x, top.y + gap + BubbleTailDrop);
        Rect cv = rootRt.rect;
        want.x = Mathf.Clamp(want.x, -cv.width * 0.5f + halfW + margin, cv.width * 0.5f - halfW - margin);
        want.y = Mathf.Clamp(want.y, -cv.height * 0.5f + halfH + margin, cv.height * 0.5f - halfH - margin);
        refs.box.anchoredPosition = want;
    }
}
