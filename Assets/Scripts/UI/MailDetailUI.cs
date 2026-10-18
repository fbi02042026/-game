using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 信件详情窗（2026-10-16 邮件 UI 改造 T7）：羊皮纸风格，两级弹窗的第二级，叠在 <see cref="MailListUI"/> 之上。
/// 显示 标题 / 发件人 / 正文（[sticker:xxx] 标签解析为正文下方横排贴纸）/ 可选配图 / 附件清单；
/// 附件（2026-10-16 主人要求「给的奖励都要有图标」）：flat 货币 + 结构化附件统一克隆 RewardCellTemplate 物品格，
/// 全部带图标 + 名称 + 数量，文本只留「附件：」表头；已领取整格变灰盖印章。
/// 底部单按钮：公告类只「确定」；奖励类未领「领取」、已领变灰并盖「已领取」印章。
/// 纯运行时 Build（底图用 Art/UI/邮件/ 已就位美术）；布局常量集中本文件顶部，真机对位偏差在这里调。
/// </summary>
public class MailDetailUI : MonoBehaviour
{
    public static MailDetailUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/MailDetailWindow";   // MailPrefabBuilder 生成（T4），几何以预制体为准

    // ---- 美术资源（Resources 路径，不带扩展名）----
    const string ArtDir = "Art/UI/邮件";
    const string SpritePaper = ArtDir + "/_0000s_0002_信件";
    const string SpriteClose = ArtDir + "/_0005_关闭";
    const string SpriteStampClaimed = ArtDir + "/信件已领取2";
    const string StickerDir = "Art/UI/Stickers";
    const int MaxStickers = 5;

    /// <summary>正文贴纸标签：[sticker:生气] → 贴纸名「生气」。</summary>
    static readonly Regex StickerRegex = new Regex(@"\[sticker:([^\]]+)\]");

    // ---- 配色（羊皮纸浅底 → 深棕文字）----
    static readonly Color ColDim = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color ColInk = new Color(0.28f, 0.18f, 0.10f, 1f);
    static readonly Color ColInkDim = new Color(0.45f, 0.34f, 0.22f, 1f);
    static readonly Color ColBtnPaper = new Color(0.55f, 0.38f, 0.20f, 1f);
    static readonly Color ColBtnDisabled = new Color(0.55f, 0.50f, 0.42f, 1f);

    // ---- 布局（羊皮纸 _0000s_0002_ 真实 720×965，窗体等比 → preserveAspect 正好铺满）----
    // 装饰在四角（左上信封 / 右上罗盘 / 右下叶饰），中心书写区 y ∈ (-390, +330)。
    const float WINDOW_W = 720f;
    const float WINDOW_H = 965f;

    MailEntry _mail;
    Text _title, _sender, _body, _rewardText;
    RectTransform _stickerRow;
    Image _detailImage;          // 配图：detailImagePath 优先，否则第一项附件的 iconOverride（共用一个图位，避免与附件文本重叠）
    Button _actionBtn;
    Text _actionLabel;
    Image _actionBtnImg;
    Button _claimBtn;            // lingqu：领取按钮（与 ActionButton 同锚点，互斥显隐）
    Text _claimLabel;
    Image _claimBtnImg;
    Transform _rewardCellTemplate;          // RewardCellTemplate：物品格原型，运行时当模板
    Transform _rewardCellTemplateMerc;      // RewardCellTemplate (1)：佣兵头像格原型（2026-10-16 主人拆分）
    List<GameObject> _rewardCells = new List<GameObject>();
    Sprite _spPaper, _spClose, _spStamp;
    bool _spritesResolved;

    public static void Show(string mailId) => Ensure().OpenWith(mailId);

    static MailDetailUI Ensure()
    {
        if (Instance != null) return Instance;

        // 预制体优先（T4）：几何以 MailDetailWindow.prefab 为准，代码只绑定+填数据；缺失走代码兜底（同 DailyLoginUI 模式）
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "MailDetailUI";
            DontDestroyOnLoad(go);
            var ui = go.GetComponent<MailDetailUI>();
            if (ui == null) ui = go.AddComponent<MailDetailUI>();
            ui.Build();
            return ui;
        }

        var fb = new GameObject("MailDetailUI", typeof(RectTransform));
        DontDestroyOnLoad(fb);
        var ui2 = fb.AddComponent<MailDetailUI>();
        ui2.Build();
        return ui2;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void OpenWith(string mailId)
    {
        _mail = FindMail(mailId);
        if (_mail == null)
        {
            Debug.LogError("[MailDetail] 找不到邮件: " + mailId);
            Close();
            return;
        }
        gameObject.SetActive(true);
        MailSystem.MarkAsRead(_mail.id);
        RefreshAll();
    }

    public void Close()
    {
        var list = MailListUI.Instance;   // 先取引用再销毁（OnDestroy 会置空 Instance）
        Destroy(gameObject);
        if (list != null) list.Refresh(); // 领取/已读状态回列表即时可见
    }

    // ────────────────────────────────────────────────────────────────────────
    // Build
    // ────────────────────────────────────────────────────────────────────────

    void Build()
    {
        ResolveSprites();

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        // 2026-10-16 修：MailPrefabBuilder 生成的树是 根 → Root → PaperWindow，
        // 以前 transform.Find("PaperWindow") 只找根的直接子节点，永远找不到 → 恒走代码兜底分支，
        // 主人在预制体里摆的 lingqu / RewardCellTemplate 等节点全部没绑上。
        if (FindPrefabPaper() != null)
        {
            BindFromPrefab();   // 预制体分支：几何以预制体为准，只按名绑定（MailPrefabBuilder 生成的树）
        }
        else
        {
            var rootRt = (RectTransform)transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            BuildDim();
            BuildPaper();
        }

        UICanvasSetup.ApplyPopup(canvas, 610, UICanvasSetup.ResolveUiCamera());

        gameObject.SetActive(false);
    }

    /// <summary>找预制体羊皮纸窗：Root/PaperWindow 优先（MailPrefabBuilder 生成结构），裸 PaperWindow 兼容旧结构。</summary>
    Transform FindPrefabPaper()
    {
        Transform t = transform.Find("Root/PaperWindow");
        if (t != null) return t;
        return transform.Find("PaperWindow");
    }

    /// <summary>预制体分支：按节点名绑定（与 MailPrefabBuilder 生成的树一一对应；缺节点 LogError 不兜底建）。</summary>
    void BindFromPrefab()
    {
        Transform paper = FindPrefabPaper();

        _title = BindText(paper, "TitleText");
        _sender = BindText(paper, "SenderText");
        _body = BindText(paper, "BodyText");
        _rewardText = BindText(paper, "RewardText");

        Transform row = FindDeep(paper, "StickerRow");
        _stickerRow = row as RectTransform;
        if (_stickerRow == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/StickerRow，贴纸无法生成");

        Transform dimg = FindDeep(paper, "DetailImage");
        _detailImage = dimg != null ? dimg.GetComponent<Image>() : null;
        if (_detailImage == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/DetailImage，配图不显示");

        Transform action = FindDeep(paper, "ActionButton");
        if (action != null)
        {
            _actionBtn = action.GetComponent<Button>();
            _actionBtnImg = action.GetComponent<Image>();
            Transform label = action.Find("Label");
            _actionLabel = label != null ? label.GetComponent<Text>() : null;
        }
        if (_actionBtn == null || _actionBtnImg == null || _actionLabel == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/ActionButton(+Label)，底部按钮不工作");

        // 🆕 领取按钮（lingqu）：与 ActionButton 同锚点，互斥显隐
        Transform claim = FindDeep(paper, "lingqu");
        if (claim != null)
        {
            _claimBtn = claim.GetComponent<Button>();
            _claimBtnImg = claim.GetComponent<Image>();
            Transform label = claim.Find("Label");
            _claimLabel = label != null ? label.GetComponent<Text>() : null;
        }
        if (_claimBtn == null || _claimBtnImg == null || _claimLabel == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/lingqu(+Label)，领取按钮不工作");

        // 🆕 物品格原型：运行时当模板，绑定后隐藏（按需克隆）
        _rewardCellTemplate = FindDeep(paper, "RewardCellTemplate");
        if (_rewardCellTemplate == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/RewardCellTemplate");
        else
            _rewardCellTemplate.gameObject.SetActive(false);

        // 2026-10-16 主人拆分：RewardCellTemplate (1) = 佣兵头像格
        _rewardCellTemplateMerc = FindDeep(paper, "RewardCellTemplate (1)");
        if (_rewardCellTemplateMerc == null)
        {
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/RewardCellTemplate (1)（佣兵头像格），佣兵附件暂用物品格");
            _rewardCellTemplateMerc = _rewardCellTemplate;
        }
        else
            _rewardCellTemplateMerc.gameObject.SetActive(false);

        // 关键修复：预制体绑定后补按钮监听（先 RemoveAll 防持久化调用重复）
        if (_actionBtn != null)
        {
            _actionBtn.onClick.RemoveAllListeners();
            _actionBtn.onClick.AddListener(Close);        // 确定=关闭
        }
        if (_claimBtn != null)
        {
            _claimBtn.onClick.RemoveAllListeners();
            _claimBtn.onClick.AddListener(OnClaimClicked); // 领取=领取
        }

        // 【2026-10-17 修·主人报「满体力邮件领取不了也关不掉」】预制体分支漏绑 BtnClose/Dim：
        // 领取被上限卡住时（如体力满、部分奖励留在邮件）claimed 恒为 false → lingqu 常驻、
        // ActionButton 永不出现，窗上没有任何可点的关闭途径 → 补绑右上 X 与遮罩关闭（与 MailListUI 同口径）。
        var closeT = FindDeep(paper, "BtnClose");
        if (closeT == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/BtnClose，无法绑定关闭");
        else
        {
            var closeBtn = closeT.GetComponent<Button>();
            if (closeBtn == null)
                Debug.LogError("[MailDetail] 预制体 BtnClose 缺 Button 组件，无法绑定关闭");
            else
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(Close);
            }
        }

        var dimT = paper.parent != null ? paper.parent.Find("Dim") : null;   // builder 口径：Dim 在 Root 下
        if (dimT == null)
            Debug.LogError("[MailDetail] 预制体缺 Root/Dim，无法绑定遮罩关闭");
        else
        {
            var dimBtn = dimT.GetComponent<Button>();
            if (dimBtn == null)
                Debug.LogError("[MailDetail] 预制体 Dim 缺 Button 组件，无法绑定遮罩关闭");
            else
            {
                dimBtn.onClick.RemoveAllListeners();
                dimBtn.onClick.AddListener(Close);
            }
        }
    }

    static Text BindText(Transform paper, string name)
    {
        Transform t = FindDeep(paper, name);
        var text = t != null ? t.GetComponent<Text>() : null;
        if (text == null)
            Debug.LogError("[MailDetail] 预制体缺 PaperWindow/" + name);
        return text;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    void BuildDim()
    {
        var go = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = ColDim;
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(Close);
    }

    void BuildPaper()
    {
        var go = new GameObject("PaperWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(WINDOW_W, WINDOW_H);

        var img = go.GetComponent<Image>();
        img.sprite = _spPaper;
        img.raycastTarget = true;
        img.preserveAspect = true;   // 窗体与底图 720×965 同比例 → 正好铺满

        // 标题（书写区顶，左侧信封装饰在 x -360~-180，标题居中避让）
        _title = NewText(go.transform, "TitleText", string.Empty, 30, TextAnchor.MiddleCenter, ColInk);
        var trt = (RectTransform)_title.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 368f);
        trt.sizeDelta = new Vector2(620f, 50f);
        _title.fontStyle = FontStyle.Bold;

        // 发件人
        _sender = NewText(go.transform, "SenderText", string.Empty, 20, TextAnchor.MiddleLeft, ColInkDim);
        var srt = (RectTransform)_sender.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 324f);
        srt.sizeDelta = new Vector2(620f, 28f);

        // 正文
        _body = NewText(go.transform, "BodyText", string.Empty, 22, TextAnchor.UpperLeft, ColInk);
        var brt = (RectTransform)_body.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, 190f);
        brt.sizeDelta = new Vector2(620f, 240f);

        // 贴纸行（正文下方横排）
        var row = new GameObject("StickerRow", typeof(RectTransform));
        row.transform.SetParent(go.transform, false);
        _stickerRow = (RectTransform)row.transform;
        _stickerRow.anchorMin = _stickerRow.anchorMax = new Vector2(0.5f, 0.5f);
        _stickerRow.pivot = new Vector2(0f, 0.5f);
        _stickerRow.anchoredPosition = new Vector2(-300f, 16f);
        _stickerRow.sizeDelta = new Vector2(600f, 88f);
        _stickerRow.gameObject.SetActive(false);

        // 配图（detailImagePath 优先，否则第一项附件 iconOverride —— 共用一个图位）
        var dimg = new GameObject("DetailImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimg.transform.SetParent(go.transform, false);
        var drt = (RectTransform)dimg.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -120f);
        drt.sizeDelta = new Vector2(380f, 230f);
        _detailImage = dimg.GetComponent<Image>();
        _detailImage.raycastTarget = false;
        _detailImage.preserveAspect = true;
        _detailImage.gameObject.SetActive(false);

        // 附件表头（「附件：」与预制体同值；具体奖励在 y -330 一排物品格，表头抬到其上方）
        _rewardText = NewText(go.transform, "RewardText", "附件：", 22, TextAnchor.UpperLeft, ColInk);
        var rrt = (RectTransform)_rewardText.transform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(-290f, -262f);
        rrt.sizeDelta = new Vector2(200f, 40f);

        // 底部双按钮（兜底分支）：ActionButton=确定(关闭) / lingqu=领取
        _actionBtn = NewTextButton(go.transform, "ActionButton", "确定", 28, new Vector2(220f, 70f), Close);
        var abrt = (RectTransform)_actionBtn.transform;
        abrt.anchorMin = abrt.anchorMax = new Vector2(0.5f, 0.5f);
        abrt.anchoredPosition = new Vector2(0f, -425f);
        _actionBtnImg = _actionBtn.GetComponent<Image>();
        _actionLabel = _actionBtn.GetComponentInChildren<Text>();

        // 🆕 领取按钮（lingqu，与 ActionButton 同锚点）
        _claimBtn = NewTextButton(go.transform, "lingqu", "领取", 28, new Vector2(220f, 70f), OnClaimClicked);
        var cbrt = (RectTransform)_claimBtn.transform;
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0.5f, 0.5f);
        cbrt.anchoredPosition = new Vector2(0f, -425f);
        _claimBtnImg = _claimBtn.GetComponent<Image>();
        _claimLabel = _claimBtn.GetComponentInChildren<Text>();

        // 🆕 物品格模板占位（兜底分支；prefab 分支以预制体为准）
        _rewardCellTemplate = BuildRewardCellTemplate(go.transform);
        _rewardCellTemplateMerc = _rewardCellTemplate; // 兜底没有独立佣兵格，复用同一模板

        // 关闭按钮（_0005_ 自带 X，右上装饰区旁）
        var closeGo = NewIconButton(go.transform, "CloseButton", _spClose, new Vector2(64f, 65f), Close);
        var crt = (RectTransform)closeGo.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(295f, 425f);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 刷新
    // ────────────────────────────────────────────────────────────────────────

    void ResolveSprites()
    {
        if (_spritesResolved) return;
        _spritesResolved = true;
        _spPaper = LoadSprite(SpritePaper);
        _spClose = LoadSprite(SpriteClose);
        _spStamp = LoadSprite(SpriteStampClaimed);
    }

    static Sprite LoadSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError("[MailDetail] 缺少美术资源: " + path);
        return sp;
    }

    void RefreshAll()
    {
        ResolveSprites();
        var m = _mail;
        if (m == null) return;

        _title.text = m.title;
        _sender.text = "发件人：" + (string.IsNullOrEmpty(m.sender) ? "系统" : m.sender);

        // 正文：剥掉 [sticker:xxx] 标签，贴纸单独横排
        string rawBody = m.body ?? string.Empty;
        List<string> stickers = new List<string>();
        var matches = StickerRegex.Matches(rawBody);
        for (int i = 0; i < matches.Count; i++)
        {
            string name = matches[i].Groups[1].Value;
            if (!stickers.Contains(name)) stickers.Add(name);
        }
        _body.text = StickerRegex.Replace(rawBody, "").Trim();
        RebuildStickers(stickers);

        // 配图：detailImagePath 优先；没有则用第一项附件的 iconOverride（共用同一图位，避免与附件文本重叠）
        string imagePath = !string.IsNullOrEmpty(m.detailImagePath) ? m.detailImagePath : FirstIconOverride(m);
        if (!string.IsNullOrEmpty(imagePath))
        {
            var sp = Resources.Load<Sprite>(imagePath);
            if (sp == null) Debug.LogError("[MailDetail] 缺少配图: " + imagePath);
            _detailImage.sprite = sp;
            _detailImage.gameObject.SetActive(sp != null);
        }
        else
        {
            _detailImage.gameObject.SetActive(false);
        }

        // 2026-10-16 主人要求「给的奖励都要有图标」：flat 货币不再走文本行，统一进下方物品格带图标渲染；
        // RewardText 只当「附件：」表头用（字面在预制体里摆好），有奖励才显示。
        bool hasReward = m.gold > 0 || m.diamond > 0 || m.stamina > 0 || m.enchantStones > 0
                         || m.decomposeMats > 0 || m.talentPoints > 0
                         || (m.attachments != null && m.attachments.Exists(a => a != null && a.amount > 0));
        if (_rewardText != null) _rewardText.gameObject.SetActive(hasReward);

        // 底部双按钮互斥显隐：有奖励且未领→领(lingqu)；否则→确定(ActionButton)
        bool showClaim = hasReward && !m.claimed;
        if (_claimBtn != null) _claimBtn.gameObject.SetActive(showClaim);
        if (_actionBtn != null)
        {
            _actionBtn.gameObject.SetActive(!showClaim);
            _actionLabel.text = "确定";
        }

        // 物品格渲染（按附件克隆）
        RebuildRewardCells(m);
    }

    void RebuildStickers(List<string> names)
    {
        for (int i = _stickerRow.childCount - 1; i >= 0; i--)
            Destroy(_stickerRow.GetChild(i).gameObject);
        _stickerRow.gameObject.SetActive(names.Count > 0);

        for (int i = 0; i < names.Count && i < MaxStickers; i++)
        {
            var sp = Resources.Load<Sprite>(StickerDir + "/" + names[i]);
            if (sp == null)
            {
                Debug.LogError("[MailDetail] 缺少贴纸: " + names[i]);
                continue;
            }
            var go = new GameObject("Sticker_" + names[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_stickerRow, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(96f * i, 0f);
            rt.sizeDelta = new Vector2(88f, 88f);
            var img = go.GetComponent<Image>();
            img.sprite = sp;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
    }

    /// <summary>奖励条目（2026-10-16 主人要求「给的奖励都要有图标」）：flat 货币与结构化附件统一走物品格。</summary>
    struct RewardItem
    {
        public Sprite icon;
        public string name;
        public long amount;
        public bool isMerc; // 2026-10-16 主人拆分：佣兵附件走 RewardCellTemplate (1) 头像格
    }

    /// <summary>按奖励清单克隆物品格（RewardCellTemplate 当模板），已领取则整格变灰盖印章。</summary>
    void RebuildRewardCells(MailEntry m)
    {
        // 销毁旧克隆
        for (int i = 0; i < _rewardCells.Count; i++)
            Destroy(_rewardCells[i]);
        _rewardCells.Clear();

        if (_rewardCellTemplate == null) return;

        List<RewardItem> items = new List<RewardItem>();
        AppendFlatRewardItems(m, items);
        AppendAttachmentRewardItems(m, items);

        int cellW = 96;
        // 【2026-10-17 修·主人报「附件图标和领取按钮重叠」】cellY 原硬编码 -330，没跟预制体附件区走：
        // 主人把 RewardText/lingqu 在预制体里上移后，格子仍停在 -330，正好压在领取按钮上。
        // 改为取预制体 RewardText 的 y（附件区以主人预制体摆位为准），绑定缺失才退回 -330。
        int cellY = _rewardText != null ? Mathf.RoundToInt(_rewardText.rectTransform.anchoredPosition.y) : -330;

        int startX = -((cellW * items.Count) - 8) / 2;   // 居中横排
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // 2026-10-16 主人拆分：佣兵附件走 RewardCellTemplate (1)，其余走 RewardCellTemplate
            Transform tmpl = item.isMerc ? _rewardCellTemplateMerc : _rewardCellTemplate;
            if (tmpl == null) tmpl = _rewardCellTemplate;
            if (tmpl == null) return;
            var cell = UnityEngine.Object.Instantiate(tmpl.gameObject, tmpl.parent);
            var crt = (RectTransform)cell.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(startX + i * cellW, cellY);
            crt.sizeDelta = new Vector2(88f, 88f);

            var icon = FindDeep(cell.transform, "Icon");
            var name = FindDeep(cell.transform, "Name");
            var count = FindDeep(cell.transform, "Count");
            var stamp = FindDeep(cell.transform, "StampClaimed");
            var iconImg = icon != null ? icon.GetComponent<Image>() : null;
            var nameTxt = name != null ? name.GetComponent<Text>() : null;
            var countTxt = count != null ? count.GetComponent<Text>() : null;
            var stampImg = stamp != null ? stamp.GetComponent<Image>() : null;
            var cellImg = cell.GetComponent<Image>();

            // 图标
            if (iconImg != null)
            {
                if (item.icon == null) Debug.LogError("[MailDetail] 物品格缺图标: " + item.name);
                iconImg.sprite = item.icon;
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
            }

            // 名称 / 数量（2026-10-18 主人拍板：去掉「x」，只显示数量）
            if (nameTxt != null) nameTxt.text = item.name;
            if (countTxt != null) countTxt.text = item.amount.ToString();

            // 已领取：整格变灰 + 盖章
            if (m.claimed)
            {
                var grey = new Color(0.5f, 0.5f, 0.5f, 1f);
                if (iconImg != null) iconImg.color = grey;
                if (nameTxt != null) nameTxt.color = grey;
                if (countTxt != null) countTxt.color = grey;
                if (cellImg != null) cellImg.color = grey;
                if (stampImg != null)
                {
                    stampImg.sprite = _spStamp;
                    stampImg.raycastTarget = false;
                    stampImg.preserveAspect = true;
                    stamp.gameObject.SetActive(true);
                }
            }
            else
            {
                if (stampImg != null && stamp != null) stamp.gameObject.SetActive(false);
            }

            cell.SetActive(true);
            _rewardCells.Add(cell);
        }
    }

    /// <summary>flat 货币 → 物品格清单。图标同 DailyLoginUI 口径：Common 目录（金币/钻石/天赋石/强化石/分解材料），体力奖励格用体力药水图（2026-10-17 主人拍板）。</summary>
    static void AppendFlatRewardItems(MailEntry m, List<RewardItem> items)
    {
        if (m.gold > 0) items.Add(new RewardItem { icon = CommonIconSprites.Load("icon_gold"), name = "金币", amount = m.gold });
        if (m.diamond > 0) items.Add(new RewardItem { icon = CommonIconSprites.Load("icon_diamond_blue"), name = "钻石", amount = m.diamond });
        if (m.stamina > 0) items.Add(new RewardItem { icon = Resources.Load<Sprite>("Art/UI/Icons/Common/icon_stamina_potion"), name = "体力", amount = m.stamina });
        if (m.enchantStones > 0) items.Add(new RewardItem { icon = CommonIconSprites.Load("icon_enchant_stone"), name = "附魔石", amount = m.enchantStones });
        if (m.decomposeMats > 0) items.Add(new RewardItem { icon = CommonIconSprites.Load("icon_decompose_mat"), name = "分解材料", amount = m.decomposeMats });
        if (m.talentPoints > 0) items.Add(new RewardItem { icon = CommonIconSprites.Load("icon_talent_stone"), name = "天赋石", amount = m.talentPoints });
    }

    /// <summary>结构化附件 → 物品格清单（佣兵走花名册真名 + 头像，物品走 iconOverride）。</summary>
    static void AppendAttachmentRewardItems(MailEntry m, List<RewardItem> items)
    {
        if (m.attachments == null) return;
        for (int i = 0; i < m.attachments.Count; i++)
        {
            var att = m.attachments[i];
            if (att == null || att.amount <= 0) continue;
            string attName = att.type == "merc" && !string.IsNullOrEmpty(att.hireId)
                ? MercDisplayName(att.hireId)
                : RewardTypeName(att.type);
            items.Add(new RewardItem
            {
                icon = Resources.Load<Sprite>(att.iconOverride),
                name = attName,
                amount = att.amount,
                isMerc = att.type == "merc",
            });
        }
    }

    void OnClaimClicked()
    {
        if (_mail == null) { Close(); return; }
        if (_mail.claimed) return;          // 公告/已领时领取按钮不显示，仅走领取分支

        // 【2026-10-18 主人拍板】领取体力会超出上限：先确认「多余的会浪费」，确认后才领（多余部分作废）
        int waste = MailSystem.GetStaminaWasteOnClaim(_mail);
        if (waste > 0)
        {
            StaminaConfirmPopup.Show(
                $"体力将超出上限，多余的 {waste} 点会浪费。\n是否仍要领取？",
                "仍要领取",
                () =>
                {
                    MailSystem.TryClaim(_mail != null ? _mail.id : null);   // notify=true：内部自动 toast
                    GuildHallUI.RefreshAllHudStatic();
                    if (_mail != null) RefreshAll();
                });
            return;
        }

        MailSystem.TryClaim(_mail.id);      // notify=true：内部自动 toast + 刷新红点
        GuildHallUI.RefreshAllHudStatic();
        RefreshAll();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 数据辅助
    // ────────────────────────────────────────────────────────────────────────

    static MailEntry FindMail(string mailId)
    {
        var inbox = MailSystem.GetInbox();
        if (inbox == null || string.IsNullOrEmpty(mailId)) return null;
        for (int i = 0; i < inbox.Count; i++)
        {
            var m = inbox[i];
            if (m != null && m.id == mailId) return m;
        }
        return null;
    }

    static string FirstIconOverride(MailEntry m)
    {
        if (m.attachments == null) return null;
        for (int i = 0; i < m.attachments.Count; i++)
        {
            var att = m.attachments[i];
            if (att != null && !string.IsNullOrEmpty(att.iconOverride)) return att.iconOverride;
        }
        return null;
    }

    static string RewardTypeName(string key)
    {
        switch (key)
        {
            case "gold": return "金币";
            case "diamond": return "钻石";
            case "stamina": return "体力";
            case "enchant": return "附魔石";
            case "mat": return "分解材料";
            case "talent": return "天赋石";
            case "scrollCommon": return "普通招募卷轴";
            case "scrollLegendary": return "高级招募卷轴";
            default: return key;
        }
    }

    /// <summary>🆕 佣兵奖励物品格的名称：优先取花名册真名（如 H017→莉娜）。</summary>
    static string MercDisplayName(string hireId)
    {
        if (MercRosterDefs.TryGetByHireId(hireId, out var def) && !string.IsNullOrEmpty(def.Name))
            return def.Name;
        return hireId;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 构建工具
    // ────────────────────────────────────────────────────────────────────────

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.supportRichText = false;
        t.raycastTarget = false;
        return t;
    }

    static Button NewTextButton(Transform parent, string name, string label, int fontSize, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = ColBtnPaper;
        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var t = NewText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white);
        var trt = (RectTransform)t.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var brt = (RectTransform)go.transform;
        brt.sizeDelta = size;
        return btn;
    }

    GameObject NewIconButton(Transform parent, string name, Sprite sprite, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = true;
        img.preserveAspect = true;
        if (img.sprite == null)
            Debug.LogError("[MailDetail] 关闭按钮缺图: " + SpriteClose);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        return go;
    }

    /// <summary>兜底分支：构建物品格模板（含 Icon/Name/Count/StampClaimed 子节点），当模板隐藏。</summary>
    static Transform BuildRewardCellTemplate(Transform parent)
    {
        var tpl = new GameObject("RewardCellTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tpl.transform.SetParent(parent, false);
        var rt = (RectTransform)tpl.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -330f);
        rt.sizeDelta = new Vector2(88f, 88f);
        tpl.GetComponent<Image>().color = ColInkDim;   // 底灰

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(tpl.transform, false);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(56f, 56f);

        var name = NewText(tpl.transform, "Name", string.Empty, 16, TextAnchor.MiddleCenter, Color.white);
        var nrt = (RectTransform)name.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, -34f);
        nrt.sizeDelta = new Vector2(84f, 20f);

        var count = NewText(tpl.transform, "Count", string.Empty, 16, TextAnchor.MiddleRight, Color.white);
        var crt = (RectTransform)count.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, 34f);
        crt.sizeDelta = new Vector2(84f, 20f);

        var stamp = new GameObject("StampClaimed", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stamp.transform.SetParent(tpl.transform, false);
        var srt = (RectTransform)stamp.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(60f, 60f);
        stamp.SetActive(false);

        tpl.SetActive(false);   // 当模板用
        return tpl.transform;
    }
}
