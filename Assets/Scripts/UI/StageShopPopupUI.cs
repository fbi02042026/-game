using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗内关卡商店（2026-10-21 主人需求，<b>临时界面</b>——后续正式化再出美术/预制体）：
/// <para>· 三个商品格：重生护符 50% 概率占一格，其余出随机武器/装备（比玩家当前装备好一档）；</para>
/// <para>· 货币 = 战斗内抽奖币池（currentGold，即「抽奖卷」），购买走 BattleManager 回调；</para>
/// <para>· 用项目现有资源拼装（恢复底框 / 装备格 / 抽奖币图标），运行时建树挂 BattleUI 下，
///   无独立 Canvas（同 BackpackItemActionUI 范式，主 Canvas 本就摄像机模式）。</para>
/// </summary>
public class StageShopPopupUI : MonoBehaviour
{
    public static StageShopPopupUI Instance { get; private set; }

    /// <summary>一件商品。onBuy 返回 true = 购买生效（扣款由调用方在 onBuy 内做），格子随之置灰。</summary>
    public class ShopItem
    {
        public string title;
        public string info;
        public Sprite icon;
        public int price;
        public Rarity? rarity;      // 装备有稀有度（名字按稀有度上色），护符等道具为 null
        public Func<bool> onBuy;
        public bool sold;
    }

    const float PanelW = 720f, PanelH = 780f;
    const float CardW = 200f, CardH = 430f, CardGap = 24f;

    GameObject _dismiss;
    GameObject _panel;
    Text _goldText;
    readonly List<CardView> _cards = new List<CardView>();
    List<ShopItem> _items;
    int _gold;
    Action _onClosed;

    struct CardView
    {
        public GameObject root;
        public Image icon;
        public Text name;
        public Text price;
        public Button buy;
        public Text buyLabel;
        public Image soldMask;
    }

    /// <summary>挂 BattleUI 下（主 Canvas 内）。重复调用返回同一实例。</summary>
    public static StageShopPopupUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;
        var go = new GameObject("StageShopPopupUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Instance = go.AddComponent<StageShopPopupUI>();
        Instance.Build();
        return Instance;
    }

    /// <summary>弹出商店。onClosed 在商店关闭（点「继续冒险」或外部强收）时回调一次。</summary>
    public static void Show(List<ShopItem> items, int gold, Action onClosed)
    {
        var ui = Ensure(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        if (ui == null)
        {
            Debug.LogError("[StageShop] 商店面板构建失败（BattleUI 缺失）");
            onClosed?.Invoke();
            return;
        }
        ui._items = items;
        ui._gold = gold;
        // 换新回调：旧回调若还没触发（异常链路）直接丢弃，避免上一场的关闭信号串场
        ui._onClosed = onClosed;
        ui.transform.SetAsLastSibling();
        ui.RefreshAll();
        ui.gameObject.SetActive(true);
    }

    /// <summary>关闭商店并触发 onClosed（幂等：回调只发一次）。</summary>
    public void Close()
    {
        gameObject.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    void Build()
    {
        // 全屏暗遮罩：挡住底下战斗 HUD 的点击（不接关闭——防误触漏看商品）
        _dismiss = new GameObject("Dismiss", typeof(RectTransform), typeof(Image));
        _dismiss.transform.SetParent(transform, false);
        var dImg = _dismiss.GetComponent<Image>();
        dImg.color = new Color(0f, 0f, 0f, 0.6f);
        dImg.raycastTarget = true;
        Stretch(_dismiss.GetComponent<RectTransform>());

        // 面板本体（底图用背包面板同款「恢复底框」）
        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        pImg.sprite = BackpackGridVisual.PanelBgSprite();
        pImg.type = Image.Type.Sliced;
        pImg.color = Color.white;
        var pRt = _panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(PanelW, PanelH);

        // 标题
        var title = MakeText(_panel.transform, "Title", 40, TextAnchor.MiddleCenter, RarityPalette.Get(Rarity.Legendary));
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 1f); tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -28f);
        tRt.sizeDelta = new Vector2(400f, 56f);
        title.text = "关卡商店";

        // 余额（抽奖币池）
        _goldText = MakeText(_panel.transform, "Gold", 30, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.5f));
        var gRt = _goldText.GetComponent<RectTransform>();
        gRt.anchorMin = new Vector2(0.5f, 1f); gRt.anchorMax = new Vector2(0.5f, 1f);
        gRt.pivot = new Vector2(0.5f, 1f);
        gRt.anchoredPosition = new Vector2(0f, -92f);
        gRt.sizeDelta = new Vector2(400f, 40f);
        var goldIcon = MakeIcon(_panel.transform, CommonIconSprites.Load("icon_draft_coin"), 36f);
        var giRt = goldIcon.GetComponent<RectTransform>();
        giRt.anchorMin = new Vector2(0.5f, 1f); giRt.anchorMax = new Vector2(0.5f, 1f);
        giRt.pivot = new Vector2(0.5f, 1f);
        giRt.anchoredPosition = new Vector2(-160f, -94f);

        // 三张商品卡
        float totalW = 3 * CardW + 2 * CardGap;
        float startX = -totalW * 0.5f + CardW * 0.5f;
        for (int i = 0; i < 3; i++)
            _cards.Add(BuildCard(_panel.transform, new Vector2(startX + i * (CardW + CardGap), 30f)));

        // 底部「继续冒险」
        var goBtn = MakeButton(_panel.transform, "ContinueBtn", "继续冒险", new Vector2(0f, -PanelH * 0.5f + 66f), new Vector2(240f, 72f));
        goBtn.onClick.AddListener(Close);

        // 五坑⑤：运行时建树的弹窗 Build() 末尾必须收掉，等 Show 再开
        _panel.SetActive(false);
        _dismiss.SetActive(false);
        gameObject.SetActive(false);
    }

    CardView BuildCard(Transform parent, Vector2 x)
    {
        var cv = new CardView();
        var go = new GameObject($"Card_{x.x:F0}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var cImg = go.GetComponent<Image>();
        cImg.sprite = BackpackGridVisual.CellBgSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = new Color(1f, 1f, 1f, 0.9f);
        var cRt = go.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(x.x, x.y);
        cRt.sizeDelta = new Vector2(CardW, CardH);
        cv.root = go;

        // 图标 140×140 居中偏上
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iRt = iconGo.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0.5f, 1f); iRt.anchorMax = new Vector2(0.5f, 1f);
        iRt.pivot = new Vector2(0.5f, 1f);
        iRt.anchoredPosition = new Vector2(0f, -26f);
        iRt.sizeDelta = new Vector2(140f, 140f);
        var iImg = iconGo.GetComponent<Image>();
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;
        cv.icon = iImg;

        // 名字（按稀有度上色）
        var nameT = MakeText(go.transform, "Name", 26, TextAnchor.MiddleCenter, Color.white);
        var nRt = nameT.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.5f, 1f); nRt.anchorMax = new Vector2(0.5f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(0f, -176f);
        nRt.sizeDelta = new Vector2(CardW - 12f, 60f);
        cv.name = nameT;

        // 价格行（抽奖币图标 + 数字）
        var priceIcon = MakeIcon(go.transform, CommonIconSprites.Load("icon_draft_coin"), 30f);
        var piRt = priceIcon.GetComponent<RectTransform>();
        piRt.anchorMin = new Vector2(0.5f, 0f); piRt.anchorMax = new Vector2(0.5f, 0f);
        piRt.pivot = new Vector2(0.5f, 0f);
        piRt.anchoredPosition = new Vector2(-34f, 128f);
        var priceT = MakeText(go.transform, "Price", 28, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.5f));
        var prRt = priceT.GetComponent<RectTransform>();
        prRt.anchorMin = new Vector2(0.5f, 0f); prRt.anchorMax = new Vector2(0.5f, 0f);
        prRt.pivot = new Vector2(0f, 0f);
        prRt.anchoredPosition = new Vector2(-12f, 130f);
        prRt.sizeDelta = new Vector2(120f, 36f);
        cv.price = priceT;

        // 购买按钮
        var buyBtn = MakeButton(go.transform, "Buy", "购买", new Vector2(0f, 58f), new Vector2(150f, 56f));
        buyBtn.onClick.AddListener(() => OnBuyClicked(cv));
        cv.buy = buyBtn;
        cv.buyLabel = buyBtn.GetComponentInChildren<Text>(true);

        // 已购买遮罩
        var soldGo = new GameObject("SoldMask", typeof(RectTransform), typeof(Image));
        soldGo.transform.SetParent(go.transform, false);
        var sImg = soldGo.GetComponent<Image>();
        sImg.color = new Color(0f, 0f, 0f, 0.55f);
        sImg.raycastTarget = false;
        Stretch(soldGo.GetComponent<RectTransform>());
        cv.soldMask = sImg;

        return cv;
    }

    void OnBuyClicked(CardView cv)
    {
        if (_cards.IndexOf(cv) < 0 || _items == null) return;
        int idx = _cards.IndexOf(cv);
        if (idx >= _items.Count || _items[idx] == null) return;
        var item = _items[idx];
        if (item.sold) return;

        if (item.price > _gold)
        {
            UIManager.Instance?.ShowToast("抽奖币不够——打怪 / 通关攒一攒再来。");
            return;
        }
        if (item.onBuy == null || !item.onBuy()) return;

        item.sold = true;
        UIManager.Instance?.ShowToast($"已购买「{item.title}」");
        RefreshAll();
    }

    void RefreshAll()
    {
        if (_goldText != null) _goldText.text = $"抽奖币 {_gold}";
        for (int i = 0; i < _cards.Count; i++)
        {
            var cv = _cards[i];
            bool has = _items != null && i < _items.Count && _items[i] != null;
            cv.root.SetActive(has);
            if (!has) continue;
            var item = _items[i];

            if (cv.icon != null)
            {
                cv.icon.sprite = item.icon;
                cv.icon.enabled = item.icon != null;
            }
            if (cv.name != null)
            {
                cv.name.text = item.title;
                cv.name.color = item.rarity.HasValue ? RarityPalette.Get(item.rarity.Value) : Color.white;
            }
            if (cv.price != null) cv.price.text = $"{item.price}";
            if (cv.buyLabel != null) cv.buyLabel.text = item.sold ? "已购买" : "购买";
            if (cv.buy != null) cv.buy.interactable = !item.sold;
            if (cv.soldMask != null) cv.soldMask.gameObject.SetActive(item.sold);
        }
    }

    /// <summary>外部（协程超时 / 局收尾）强收：与 Close 同语义。</summary>
    public static void Hide()
    {
        if (Instance != null && Instance.gameObject.activeSelf)
            Instance.Close();
    }

    // ===== 构建辅助（与 BackpackItemActionUI 同款）=====

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Image MakeIcon(Transform parent, Sprite sprite, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        return img;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.35f, 0.5f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size ?? new Vector2(180f, 60f);

        var txt = MakeText(go.transform, "Text", 28, TextAnchor.MiddleCenter, Color.white);
        Stretch(txt.GetComponent<RectTransform>());
        txt.text = label;

        return go.AddComponent<Button>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
