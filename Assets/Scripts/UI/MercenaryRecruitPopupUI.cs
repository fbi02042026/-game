using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 招募佣兵弹窗：绑定手做 Prefab（Card0/1/2）。
/// 每卡货币钮：ConfirmButton/ConfirmButton1 二选一显示（2026-10-19 主人拍板：普通=金币、
/// 稀有=普通卷、传奇=稀有卷；Card0 没摆卷钮节点时由金币钮节点顶位显示卷轴）。
/// 已拥有的候选也可购买（2026-10-25 主人拍板：重复获得转本命碎片），卡面带「已拥有·转碎片」标。
/// 雇佣 = 首次购买永久解锁进已拥有（2026-10-19 主人拍板）；图鉴 MarkMercSeen。
/// <para>【2026-10-23 主人拍板】本文件只留：字段 / 生命周期 / 候选批次存取 / 刷新成本与倒计时 / 关闭与适配。
/// 卡片渲染见 .Cards.cs、台词气泡（含 Update）见 .Banter.cs、交互结算见 .Actions.cs、兜底构建见 .Build.cs。</para>
/// </summary>
public partial class MercenaryRecruitPopupUI : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/Town/MercenaryRecruitPopup";

    public static MercenaryRecruitPopupUI Instance { get; private set; }

    /// <summary>是否开着（软著自动截图等探针用；与旧 TavernRosterPanel.IsOpen 同语义）。</summary>
    public bool IsOpen => root != null && root.activeSelf;

    [Header("壳")]
    public GameObject root;
    public Button closeButton;
    public Text titleText;
    public Text subtitleText;
    public Button infoButton;

    [Header("三选一")]
    public CardView[] cards = new CardView[3];

    [Header("底栏")]
    public Text remainText;
    public Button refreshButton;
    public Text refreshCostText;
    public Text autoRefreshText;

    GameObject _infoRoot;

    // 左下角两种卷轴数量 Label（2026-10-18 主人要求绑数据）：招募卷/1=普通卷轴，招募卷/2=高级招募卷轴
    Text _scrollCommonCountText;
    Text _scrollLegendaryCountText;

    // 卡上卷钮的卷轴图快照（2026-10-19 修「普通卷图标用错」）：招募卷/1/icon=普通卷、招募卷/2/icon=高级卷
    Sprite _commonScrollIcon;
    Sprite _legendaryScrollIcon;

    // —— 刷新按钮「钻石刷新」形态节点（2026-10-23 主人拍板：冷却中可花钻立即刷，每天 2 次 50/100）——
    // 【2026-10-24 主人拍板】按钮文字直接写「50刷新」（价格并入 Label），独立价格浮标不再显示；
    // 钻石 icon = 主人手建预制体节点，钻石刷新形态显示、免费形态隐藏；
    // 「剩余次数」小字 = 主人手建预制体节点，字号改 20；代码只绑引用、只改文案/字号，不写大小/颜色/坐标。
    Image _diamondIcon;
    Text _diamondLeftText;

    // —— 顶条金币+钻石资源条克隆（2026-10-24 主人拍板「这个界面加钻石和金币的资源条」）——
    // 口径与 MercAscendUI / CharacterUI.ResBar 完全一致：TownSharedChrome 唯一克隆口（禁各窗自建），
    // 只留金币/钻石两列；数字真源唯一 = ResourceWallet、格式唯一出口 = GuildHallUI.FormatResource。
    Transform _resBar;
    Text _resGoldText;
    Text _resDiaText;

    // —— 货币钮外观快照（2026-10-19 主人报「按钮还不一样」）——
    // 金币钮(ConfirmButton)与卷钮(ConfirmButton1)是两套预制体节点：底图/颜色/Label颜色/尺寸都不同。
    // 绑定时各拍一份快照，填充时按货币套用对应外观 —— 三张卡的货币钮看起来完全一致。
    Sprite _goldBtnSprite; Color _goldBtnColor; Color _goldLabelColor; Vector2 _goldBtnSize;
    Sprite _scrollBtnSprite; Color _scrollBtnColor; Color _scrollLabelColor; Vector2 _scrollBtnSize;
    bool _btnLooksCaptured;

    // —— 佣兵台词气泡（2026-10-17 主人拍板：三选一里佣兵说话用气泡框，不再走顶部 Toast）——
    // 样式对齐看板娘 SpeechBubbleTalker：speech_bubble 底图 + 打字机 + SpeechBubbleFit 文字自适应。
    GameObject _mercBubbleRoot;
    Text _mercBubbleText;
    Coroutine _mercBubbleCo;
    Coroutine _refreshCountdownCo;

    [System.Serializable]
    public class CardView
    {
        public GameObject root;
        public Image background;
        public Image portrait;
        public Image roleIcon;
        public Text nicknameText;
        public Text nameText;
        public Text rarityText;
        public Text skill1Name;
        public Text skill1Desc;
        public Text skill2Name;
        public Text skill2Desc;
        public Image skill1Icon;
        public Image skill2Icon;
        public Button goldButton;
        public Button scrollButton;
        public Text goldLabel;
        public Text scrollLabel1;
        public Image scrollButtonImage;
        public Image goldIcon;            // ConfirmButton/icon（主人在预制体里已摆好）
        public Image scrollIcon;          // ConfirmButton1/icon（主人在预制体里已摆好）
        [System.NonSerialized] public Sprite scrollIconDefault; // 主人摆的卷轴图快照：切图后要还原
        [System.NonSerialized] public Sprite goldIconDefault;   // 金币图快照：金币钮顶卷钮位切卷图后要还原
    }

    List<MercenaryData> _offers = new List<MercenaryData>();
    /// <summary>
    /// 稀有度 → 卡牌底图（【2026-10-19 主人拍板】预制体 Card0/1/2 是普通/稀有/传奇三种稀有度卡牌，
    /// 三套美术就用预制体自己的三张底图：首刷前按底图名归档缓存，RefreshCard 按佣兵稀有度取用）。
    /// </summary>
    readonly Dictionary<MercRosterDefs.MercRarity, Sprite> _cardFrameByRarity =
        new Dictionary<MercRosterDefs.MercRarity, Sprite>();
    bool _cardFramesMapped;
    bool _wired;
    string _lastBanterKey;
    float _nextIdleBanterUnscaled;

    public static void Show()
    {
        Ensure().Open();
    }

    public static MercenaryRecruitPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(ResourcePath);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            go.name = "MercenaryRecruitPopup";
        }
        else
        {
            Debug.LogWarning("[MercenaryRecruit] 未找到预制体 " + ResourcePath + "，使用代码兜底壳");
            go = new GameObject("MercenaryRecruitPopup", typeof(RectTransform));
            var ui = go.AddComponent<MercenaryRecruitPopupUI>();
            ui.BuildFallbackHierarchy();
            Object.DontDestroyOnLoad(go);
            return ui;
        }

        Object.DontDestroyOnLoad(go);
        var host = go.GetComponent<MercenaryRecruitPopupUI>();
        if (host == null) host = go.AddComponent<MercenaryRecruitPopupUI>();
        return host;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        AutoBind();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open()
    {
        AutoBind();
        EnsureCanvas();
        // 主人预制体里的「对话框」默认带着占位字激活 → 打开先绑好并收起，等台词来了再亮
        EnsureMercBubble();
        HideMercBubble();
        FitPanelScale();
        TownSaveAlign.AlignAll();
        EnsureResBar();   // 顶条金币+钻石资源条（2026-10-24 主人拍板二次）：全局唯一 ResourceStrip，数字由其内部轮询刷
        MercHireSession.EnsureDailyOfferRefresh();
        var data = SaveSystem.Instance?.Data;
        bool dayRefresh = data != null && data.mercOfferDirty;
        if (dayRefresh || _offers == null || _offers.Count == 0)
        {
            // 【2026-10-19 主人拍板】招募候选只认 0 点和手动刷新，App 重启也不许换批：
            // 非日切、且存档里有当前批次 → 原样恢复（不算刷新）；否则重掷并写回存档。
            if (!dayRefresh && data != null && TryLoadOffersFromSave(data))
            {
                data.mercOfferDirty = false;
            }
            else
            {
                RerollOffers();
                if (dayRefresh)
                    MercHireSession.MarkRefreshed();
                else if (data != null)
                {
                    data.mercOfferDirty = false;
                    SaveSystem.Instance.Save();   // 首掷批次立刻落盘，杀进程也不丢
                }
            }
        }
        RefreshAll();
        WireOnce();
        PlaceRefreshCostAboveButton();
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        MaybeToastAppearBanter(force: false, avoidLast: false);
        ScheduleNextIdleBanter();
        StartRefreshCountdown();
    }

    void RerollOffers()
    {
        // 【2026-10-19 主人拍板】三名候选全部随机；卡面（普通/稀有/传奇三套卡牌美术）
        // 由 RefreshCard 按佣兵稀有度实时切换（见 _cardFrameByRarity）。
        _offers = MercenaryOfferGenerator.GenerateOffers();
        PersistOffers(SaveSystem.Instance?.Data);
    }

    /// <summary>
    /// 候选批次写回存档（2026-10-19 主人拍板：只认 0 点和手动刷新，重启不许换批）。
    /// 只写内存字段；落盘由调用方的 Save()/MarkRefreshed 负责。
    /// </summary>
    void PersistOffers(SaveData data)
    {
        if (data == null || _offers == null) return;
        data.mercOffers ??= new List<MercenaryData>();
        data.mercOffers.Clear();
        for (int i = 0; i < _offers.Count; i++)
            data.mercOffers.Add(_offers[i]);
    }

    /// <summary>
    /// 从存档恢复当前批次（重启后弹窗首开用）。批次缺卡或佣兵数据无效视为存档损坏，
    /// 返回 false 走重掷，绝不开天窗。
    /// </summary>
    bool TryLoadOffersFromSave(SaveData data)
    {
        var saved = data?.mercOffers;
        if (saved == null || saved.Count != MercenaryOfferGenerator.OfferCount) return false;
        for (int i = 0; i < saved.Count; i++)
        {
            if (saved[i] == null || string.IsNullOrEmpty(saved[i].mercId)) return false;
        }
        _offers = new List<MercenaryData>(saved);
        return true;
    }

    /// <summary>
    /// 首刷前把三张卡的预制体底图按稀有度归档（Card0/1/2 是三种稀有度卡牌，底图名带
    /// frame_common/rare/legendary 后缀）。必须在任何换框发生前调用，晚了底图已被覆盖。
    /// 识别不出不打扰：留空走 LoadRarityFrame 兜底。
    /// </summary>
    void MapCardFrames()
    {
        if (_cardFramesMapped) return;
        _cardFramesMapped = true;
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            var img = c.root.GetComponent<Image>();
            var sp = img != null ? img.sprite : null;
            if (sp == null) continue;
            MercRosterDefs.MercRarity? r = null;
            if (sp.name.Contains("legendary")) r = MercRosterDefs.MercRarity.Legendary;
            else if (sp.name.Contains("rare")) r = MercRosterDefs.MercRarity.Rare;
            else if (sp.name.Contains("common")) r = MercRosterDefs.MercRarity.Common;
            if (r.HasValue && !_cardFrameByRarity.ContainsKey(r.Value))
                _cardFrameByRarity[r.Value] = sp;
        }
    }

    void RefreshAll()
    {
        MapCardFrames();   // 必须先归档三套卡面，再做任何换框
        int hired = MercHireSession.HiredCount();
        int max = MercenaryManager.Instance != null
            ? Mathf.Max(1, MercenaryManager.Instance.GetMaxMercSlots())
            : 1;
        if (remainText != null)
            remainText.text = $"本局雇佣：{hired}/{max}";

        if (autoRefreshText != null)
            autoRefreshText.text = "每日 0 点自动刷新";

        // 左下角两种卷轴数量绑存档（2026-10-18 主人拍板）：沿用静态文案的「X{数量}」格式
        var data = SaveSystem.Instance?.Data;
        if (_scrollCommonCountText != null)
            _scrollCommonCountText.text = "X" + (data != null ? data.mercScrollCommon : 0);
        if (_scrollLegendaryCountText != null)
            _scrollLegendaryCountText.text = "X" + (data != null ? data.mercScrollLegendary : 0);

        UpdateRefreshCostText();   // 含按钮灰显/点亮与顶上倒计时文字
        // 资源条数字不在这里刷：ResourceStrip 内部轮询统一刷（2026-10-24 二次口径，与 MercAscendUI 一致）

        for (int i = 0; i < cards.Length; i++)
            RefreshCard(i);
    }

    /// <summary>
    /// 刷新按钮两态（2026-10-23 主人拍板；2026-10-24 主人改口径）：
    /// 免费可刷 → Label「刷新佣兵」，按钮下方常显今日剩余次数；
    /// 冷却中且还有钻刷次数 → Label 直接写「50刷新」（价格并入文字，钻石图标随形态显示，不挂独立价格浮标），按钮保持可点；
    /// 冷却中且次数用完 → 回「刷新佣兵」灰显但保持可点：顶上照旧倒计时、点击弹「已无刷新次数 请明天再试」。
    /// </summary>
    void UpdateRefreshCostText()
    {
        bool canFree = MercHireSession.CanManualRefresh(out int remainSec);
        bool canDiamond = MercHireSession.CanDiamondRefresh(out int usedToday, out int diamondCost);
        int leftToday = MercHireSession.DiamondRefreshPerDay - usedToday;
        bool diamondMode = !canFree && canDiamond;

        if (refreshCostText != null)
            refreshCostText.text = canFree ? "" : $"{remainSec / 3600:D2}:{remainSec / 60 % 60:D2}:{remainSec % 60:D2}";
        if (refreshButton != null)
        {
            // 【2026-10-24 主人拍板】次数用光也保持可点（仅灰显）：点了走 OnRefresh 弹「已无刷新次数 请明天再试」
            refreshButton.interactable = true;
            var img = refreshButton.targetGraphic as Image;
            if (img != null)
                img.color = (canFree || canDiamond) ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
            var label = refreshButton.GetComponentInChildren<Text>();
            if (label != null && label != _diamondLeftText && label != refreshCostText)
                label.text = diamondMode ? $"{diamondCost}刷新" : "刷新佣兵";
        }
        if (_diamondIcon != null)
            _diamondIcon.gameObject.SetActive(diamondMode);   // 2026-10-24 主人拍板：钻石刷新形态显示钻石图标（免费形态无价格不显图）
        if (_diamondLeftText != null)
            _diamondLeftText.text = leftToday > 0 ? $"今日剩{leftToday}次" : "今日已用完";
    }

    /// <summary>把 RefreshCost 文字挪到刷新按钮顶边上方（按钮内原位会跟按钮文字打架）。幂等。</summary>
    void PlaceRefreshCostAboveButton()
    {
        if (refreshCostText == null || refreshButton == null) return;
        var rt = refreshCostText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        rt.sizeDelta = new Vector2(200f, 30f);
    }

    void StartRefreshCountdown()
    {
        if (_refreshCountdownCo != null) StopCoroutine(_refreshCountdownCo);
        _refreshCountdownCo = StartCoroutine(CoRefreshCountdown());
    }

    void StopRefreshCountdown()
    {
        if (_refreshCountdownCo != null)
        {
            StopCoroutine(_refreshCountdownCo);
            _refreshCountdownCo = null;
        }
    }

    IEnumerator CoRefreshCountdown()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            UpdateRefreshCostText();
            yield return wait;
        }
    }

    void Close()
    {
        if (_infoRoot != null) _infoRoot.SetActive(false);
        HideMercBubble();
        StopRefreshCountdown();
        if (root != null) root.SetActive(false);
        ResourceStrip.Release();   // 资源条归还主界面顶条（与 EnsureResBar 的 Show 成对，2026-10-24 二次口径）
    }

    // ============================================================
    // 顶条资源条（2026-10-24 主人拍板二次）：全局唯一 ResourceStrip，开窗 Show / 关窗 Release
    // ============================================================

    /// <summary>
    /// 开窗显示资源条（金币+钻石两列，口径与 MercAscendUI 完全一致）。
    /// 主界面未注册（GuildHallUI 未初始化）时 Show 返回 false，本窗不显示资源条（不兜底）。
    /// 数字由 ResourceStrip 内部轮询统一刷，本窗不自管。
    /// </summary>
    void EnsureResBar()
    {
        ResourceStrip.Show(transform, ResourceStrip.Mode.GoldDia);
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    /// <summary>
    /// 【2026-10-17 主人拍板】按预制体布局整体缩放：不逐节点改坐标，把 Panel 连同全部子节点
    /// 等比缩到当前画布的可用区内（顶部资源条 + 底部导航各留出空间）。
    /// 预制体 Panel 714×1133 在瘦屏（match=0 时画布高可达 1364+）几乎占满全屏并压住顶栏/底栏，
    /// 只缩 Panel 的 localScale，预制体内部布局一个像素不动。
    /// </summary>
    void FitPanelScale()
    {
        var canvasRt = transform as RectTransform;
        if (canvasRt == null || canvasRt.rect.width <= 0f || canvasRt.rect.height <= 0f) return;
        var panelTf = root != null ? root.transform.Find("Panel") : null;
        if (panelTf == null) panelTf = transform.Find("Panel");
        var prt = panelTf as RectTransform;
        if (prt == null) return;

        // 预留：顶部资源条 ~120 + 底部五入口 ~150 + 余量 30 = 300；左右各留 20。
        float availW = canvasRt.rect.width - 40f;
        float availH = canvasRt.rect.height - 300f;
        float w = prt.sizeDelta.x;
        float h = prt.sizeDelta.y;
        if (w <= 0f || h <= 0f) return;
        float s = Mathf.Min(1f, Mathf.Min(availW / w, availH / h));
        prt.localScale = new Vector3(s, s, 1f);
    }
}
