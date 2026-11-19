using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 左右列共用的「参考图气泡卡」建树（2026-11-19 主人拍板：点击天赋图标弹参考图同款弹窗，
/// 只改排版、素材全用项目现成——主人原话「我只是说排版 素材用的还是项目里的」）。
/// <para>结构：全屏 Dim 真模态 → 米色圆角气泡卡（底部 45° 小方块尾巴）→ 浅色胶囊标题（属性名）
/// → 描述（属性效果）→ 彩色按钮（按钮内两行：动作词 + 货币图标 x 数量）。</para>
/// <para>素材映射：卡底=AdventureLog/Frames/字底（自带九宫格可任意拉）、胶囊=天赋_0006_属性底、
/// 按钮底=天赋_0001_金币升级（左列）/天赋_0013_天赋石升级（右列）、
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
        public Text okLabel;   // 按钮第一行（动作词：解锁/升级）
        public Text cost;      // 按钮第二行（x 数量，不足标红）
        public RectTransform box;   // 卡片本体（贴图标定位时移动）
        public RectTransform tail;  // 气泡尾巴（随卡片一起移动）
    }

    // 素材懒加载缓存（实例字段，域重载自动清空后重载）
    Sprite _bubbleCardSpr;      // 卡底（字底）
    Sprite _bubblePillSpr;      // 胶囊（属性底）
    Sprite _bubbleGoldBtnSpr;   // 金币升级按钮
    Sprite _bubbleGoldIconSpr;  // 金币图标
    Sprite _bubbleStoneBtnSpr;  // 天赋石升级按钮
    Sprite _bubbleStoneIconSpr; // 天赋石图标（顶栏同款 sprDiamondOn）

    // 卡片上的深棕字 / 不足标红 / 尾巴米色（近字底中段）
    static readonly Color BubbleInk = new Color(0.42f, 0.32f, 0.20f, 1f);
    static readonly Color BubbleCostRed = new Color(1f, 0.32f, 0.28f, 1f);
    static readonly Color BubbleTail = new Color(0.89f, 0.82f, 0.66f, 1f);

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
        _bubbleCardSpr = BubbleSprite(ref _bubbleCardSpr, "UI/AdventureLog/Frames/字底", "气泡卡底");
        _bubblePillSpr = BubbleSprite(ref _bubblePillSpr, "UI/Talent/天赋_0006_属性底", "标题胶囊");
        _bubbleGoldBtnSpr = BubbleSprite(ref _bubbleGoldBtnSpr, "UI/Talent/天赋_0001_金币升级", "金币升级按钮");
        _bubbleGoldIconSpr = BubbleSprite(ref _bubbleGoldIconSpr, "UI/Icons/Common/icon_gold", "金币图标");
        _bubbleStoneBtnSpr = BubbleSprite(ref _bubbleStoneBtnSpr, "UI/Talent/天赋_0013_天赋石升级", "天赋石升级按钮");
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
        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        root.AddComponent<GraphicRaycaster>();

        // 全屏遮罩（真模态：raycast=1，点空白处关闭）——【第15轮·主人拍板「底太黑」】改全透明，不压暗背景
        var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(onDimClose);

        // 气泡尾巴：45° 小方块，先建画在卡底下层，上半被卡片盖住、只露下半小尖（参考图底部小尾巴）
        var tail = CreateImage(root.transform, "Tail", BubbleTail);
        SetAnchored(tail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -160f), new Vector2(44f, 44f));
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.raycastTarget = false;

        // 卡片底：字底九宫格拉成竖卡（米黄圆角带描边，参考图观感）
        var box = CreateImage(root.transform, "Box", Color.white, _bubbleCardSpr, false);
        box.type = Image.Type.Sliced;
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(340f, 280f));

        // 浅色胶囊标题（属性名）
        var pill = CreateImage(box.transform, "Pill", Color.white, _bubblePillSpr, false);
        SetAnchored(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -46f), new Vector2(210f, 52f));
        pill.raycastTarget = false;
        var pillText = CreateText(pill.transform, "Label", "", 26, BubbleInk);
        Stretch(pillText.rectTransform);

        // 描述（属性效果；右列升级时两行「Lv1 → Lv2」+效果）
        var desc = CreateText(box.transform, "DescText", "", 22, BubbleInk);
        SetAnchored(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -118f), new Vector2(300f, 66f));

        // 彩色按钮（按钮内两行：动作词 / 货币图标 x 数量——参考图排法）
        var ok = CreateImage(box.transform, "UnlockButton", Color.white, btnSpr, false);
        SetAnchored(ok.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 70f), new Vector2(232f, 96f));
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = ok;
        okBtn.onClick.AddListener(onOk);
        var okLabel = CreateText(ok.transform, "Label", "", 24, Color.white);
        SetAnchored(okLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -18f), new Vector2(200f, 32f));
        var costIcon = CreateImage(ok.transform, "CostIcon", Color.white, coinSpr, true);
        SetAnchored(costIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-36f, 28f), new Vector2(28f, 28f));
        costIcon.raycastTarget = false;
        var cost = CreateText(ok.transform, "CostText", "", 22, Color.white);
        SetAnchored(cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(10f, 28f), new Vector2(160f, 30f));
        cost.alignment = TextAnchor.MiddleLeft;

        cached.root = root;
        cached.box = box.rectTransform;
        cached.tail = tail.rectTransform;
        cached.name = pillText;
        cached.desc = desc;
        cached.okLabel = okLabel;
        cached.cost = cost;
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

        // 卡 340×280；尾巴中心在卡心下 160，旋转 45° 后尖端再探 ≈31 → 尖端距卡心 ≈191
        const float cardHalfW = 170f, cardHalfH = 140f, tailDrop = 191f, gap = 8f, margin = 12f;
        Vector2 want = new Vector2(top.x, top.y + gap + tailDrop);
        Rect cv = rootRt.rect;
        want.x = Mathf.Clamp(want.x, -cv.width * 0.5f + cardHalfW + margin, cv.width * 0.5f - cardHalfW - margin);
        want.y = Mathf.Clamp(want.y, -cv.height * 0.5f + cardHalfH + margin, cv.height * 0.5f - cardHalfH - margin);
        refs.box.anchoredPosition = want;
        if (refs.tail != null) refs.tail.anchoredPosition = want + new Vector2(0f, -160f);
    }
}
