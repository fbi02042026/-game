using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场弹窗 · 换位制（2026-10-29 主人拍板改版）。
/// 展示层走预制体 Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab
///（菜单 Tools/竞技场/生成竞技场弹窗预制体 一次性生成；主人手改后【禁止重跑生成器】）。
/// 运行时只按节点名绑定（UiFind.Deep）+ 克隆榜单行 + 填数据/接事件；
/// 主人改排版/图标/按钮色都会保留。行底色例外：换位时玩家行高亮写 _rowMine，
/// 非玩家行恢复隔行色（想改色改本文件常量）。
/// 节点名契约见 ArenaPopupPrefabBuilder.cs，改名前先对齐两边。
/// 字号 24/48（fusion-pixel 12px 点阵整倍数）；数据与规则全部在 ArenaRules（真源 SaveData.arena*）。
/// </summary>
public class ArenaPopupUI : MonoBehaviour
{
    public static ArenaPopupUI Instance;

    public const string PrefabPath = "Prefabs/Town/ArenaPopupUI";

    const int RowTotal = ArenaRules.BotCount + 1;   // 100 机器人 + 玩家 = 101 行
    const float RowStep = 64f;
    const float RowH = 56f;
    const float ViewportH = 592f;
    const float ContentH = RowTotal * RowStep + 8f;

    /// <summary>积分图标（主人 2026-10-29 指定《对战积分.png》，Resources/UI/Art/Common/）。</summary>
    const string PointsIconPath = "UI/Art/Common/对战积分";

    // ── 榜单行控件（行号 0=榜首 … 100=末位；玩家行下标=arenaPlayerPos）──
    readonly GameObject[] _rowRoots = new GameObject[RowTotal];
    readonly Text[] _rowRank = new Text[RowTotal];
    readonly Image[] _rowHead = new Image[RowTotal];
    readonly Text[] _rowName = new Text[RowTotal];
    readonly Text[] _rowPts = new Text[RowTotal];
    readonly GameObject[] _rowFight = new GameObject[RowTotal];   // 「对战」按钮（可挑战行显示）
    readonly GameObject[] _rowDash = new GameObject[RowTotal];    // 「—」（不可挑战行显示）
    readonly bool[] _rowIsMine = new bool[RowTotal];

    ScrollRect _scroll;
    RectTransform _content;
    GameObject _rowTemplate;
    Text _triesText;      // 顶部「对战次数 N/M」
    Text _adBtnText;
    Text _noticeText;
    bool _bindFailed;

    // ── 前三领奖台（Podium1/2/3 卡内同名节点，绑定从各自卡根搜）──
    readonly Image[] _podRoot = new Image[3];    // 卡根（换名次色 tint）
    readonly Text[] _podRank = new Text[3];
    readonly Image[] _podHead = new Image[3];
    readonly Text[] _podName = new Text[3];
    readonly Text[] _podPts = new Text[3];

    // ── 我的排名条 ──
    Text _myRankText;
    Image _myHead;
    Text _myName;
    Text _myPtsText;

    // ── 选职业子面板 ──
    GameObject _jobSelect;

    // ── 结果面板 ──
    Image _resBg;
    Image _resMyIcon;
    Image _resBotIcon;
    Image _resPtsIcon;
    Text _resMyLabel;
    Text _resBotLabel;
    Text _resVerdict;
    Text _resPts;
    Text _resFlavor;
    Text _resGold;
    GameObject _result;

    PlayerJobId _chosenJob;
    int _pendingRowIdx = -1;

    // ── 机器人/我的档案详情（运行时建树，不进 prefab 契约；点榜单行打开）──
    GameObject _detailRoot;
    Transform _detailRows;
    Text _dTitle;
    Text _dPower;
    Text _dPoints;

    // 结算小台词（2026-10-16 调性口径：年轻化、别端着）
    static readonly string[] WinFlavors = { "对面直接躺了", "稳稳拿下！", "这波运气在线", "干净利落" };
    static readonly string[] LoseFlavors = { "差一口气…", "对面开挂了吧？！", "下把必赢", "就当热身了" };

    // 行底色（运行时在克隆与换位时写；预制体里摆的同款色只是初始观感）
    readonly Color _panelColor = new Color(0.13f, 0.12f, 0.18f, 0.97f);   // 详情弹窗盒子底
    readonly Color _btnSlate = new Color(0.35f, 0.36f, 0.45f, 1f);        // 详情弹窗关闭钮
    readonly Color _rowColorA = new Color(0.21f, 0.20f, 0.28f, 0.95f);
    readonly Color _rowColorB = new Color(0.25f, 0.24f, 0.33f, 0.95f);
    readonly Color _rowMine = new Color(0.42f, 0.32f, 0.13f, 0.97f);
    readonly Color _txtSub = new Color(0.75f, 0.75f, 0.80f, 1f);
    readonly Color _txtGold = new Color(1f, 0.80f, 0.35f, 1f);

    // ───────────────────────── 打开 / 生命周期 ─────────────────────────

    public static void Show()
    {
        var ui = Ensure();
        if (ui != null) ui.Open();
    }

    static ArenaPopupUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Arena] 竞技场弹窗预制体缺失：Resources/{PrefabPath}（菜单 Tools/竞技场/生成竞技场弹窗预制体 生成）");
            return null;
        }
        var go = Object.Instantiate(prefab);
        DontDestroyOnLoad(go);
        go.SetActive(false);
        var ui = go.GetComponent<ArenaPopupUI>();
        if (ui == null)
        {
            Debug.LogError($"[Arena] 预制体根上没有 ArenaPopupUI 组件：{PrefabPath}");
            Object.Destroy(go);
            return null;
        }
        ui.Bind();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ArenaRules.Changed -= Refresh;
    }

    void Open()
    {
        if (_bindFailed) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        ArenaRules.EnsurePeriods(data);   // 跨天清次数+机器人漂移换位；跨月赛季结算（懒触发）
        string notice = ArenaRules.TakeSeasonNotice(data);

        var canvas = GetComponent<Canvas>();
        if (canvas != null) UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);

        gameObject.SetActive(true);
        Refresh();
        ScrollToPlayer();
        if (_noticeText != null)
        {
            _noticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
            if (!string.IsNullOrEmpty(notice)) _noticeText.text = notice;
        }
    }

    void Close()
    {
        if (_jobSelect != null) _jobSelect.SetActive(false);
        if (_result != null) _result.SetActive(false);
        if (_detailRoot != null) _detailRoot.SetActive(false);
        _pendingRowIdx = -1;
        gameObject.SetActive(false);
    }

    // ───────────────────────── 绑定（按节点名，契约见 ArenaPopupPrefabBuilder.cs） ─────────────────────────

    T Bind<T>(string name) where T : Component => Bind<T>(transform, name);

    /// <summary>searchRoot 从指定节点往下搜（Result/JobSelect 内部绑定用它，避开榜单行模板里的同名节点）。</summary>
    T Bind<T>(Transform searchRoot, string name) where T : Component
    {
        var t = UiFind.Deep(searchRoot, name);
        if (t == null)
        {
            Debug.LogError($"[Arena] 预制体缺节点：{name}（节点契约见 ArenaPopupPrefabBuilder.cs，改名要两边对齐）");
            _bindFailed = true;
            return null;
        }
        return t.GetComponent<T>();
    }

    void Bind()
    {
        _scroll = Bind<ScrollRect>("List");
        if (_scroll == null) return;
        var viewport = UiFind.Deep(_scroll.transform, "Viewport");
        _content = UiFind.Deep(_scroll.transform, "Content") as RectTransform;
        _rowTemplate = UiFind.Deep(_scroll.transform, "RowTemplate")?.gameObject;
        if (viewport == null || _content == null || _rowTemplate == null)
        {
            Debug.LogError("[Arena] 预制体缺 List/Viewport/Content/RowTemplate（契约见 ArenaPopupPrefabBuilder.cs）");
            _bindFailed = true;
            return;
        }
        // 滚动参数运行时兜一遍（幂等；主人手滑清了引用也不至于滚不动）
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 15f;
        _scroll.viewport = viewport as RectTransform;
        _scroll.content = _content;

        _triesText = Bind<Text>("TriesText");
        _noticeText = Bind<Text>("NoticeText");
        if (_noticeText != null) _noticeText.gameObject.SetActive(false);

        // 顶部：看广告 +2 按钮
        var adBtn = UiFind.Deep(transform, "AdBonusBtn");
        if (adBtn != null)
        {
            var lt = UiFind.Deep(adBtn, "Label");
            if (lt != null) _adBtnText = lt.GetComponent<Text>();
            var btn = adBtn.GetComponent<Button>();
            if (btn == null) btn = adBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(ArenaRules.RequestAdBonus);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：AdBonusBtn"); }

        // 顶部：排行榜奖励说明（通用告知弹窗，铁律：纯告知走 ShowNotice）
        var rewardBtn = UiFind.Deep(transform, "RewardBtn");
        if (rewardBtn != null)
        {
            var btn = rewardBtn.GetComponent<Button>();
            if (btn == null) btn = rewardBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(ShowRewardInfo);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：RewardBtn"); }

        // 前三领奖台（卡内同名子节点从各自卡根搜；点卡 → 详情）
        for (int r = 0; r < 3; r++)
        {
            var cardT = UiFind.Deep(transform, "Podium" + (r + 1));
            if (cardT == null)
            {
                Debug.LogError($"[Arena] 预制体缺节点：Podium{r + 1}");
                _bindFailed = true;
                continue;
            }
            _podRoot[r] = cardT.GetComponent<Image>();
            _podRank[r] = UiFind.Deep(cardT, "RankNum")?.GetComponent<Text>();
            _podHead[r] = UiFind.Deep(cardT, "HeadImg")?.GetComponent<Image>();
            _podName[r] = UiFind.Deep(cardT, "NameText")?.GetComponent<Text>();
            _podPts[r] = UiFind.Deep(cardT, "PtsText")?.GetComponent<Text>();
            var cardBtn = cardT.GetComponent<Button>();
            if (cardBtn == null) cardBtn = cardT.gameObject.AddComponent<Button>();
            if (_podRoot[r] != null) cardBtn.targetGraphic = _podRoot[r];
            int rr = r;   // 领奖台显示的是排名 r+1 的实体；详情下标运行时算
            cardBtn.onClick.AddListener(() => ShowDetailForRank(rr + 1));
        }

        // 我的排名条
        _myRankText = Bind<Text>("MyRankText");
        _myHead = Bind<Image>("MyHead");
        _myName = Bind<Text>("MyName");
        _myPtsText = Bind<Text>("MyPtsText");

        BindResult();
        BindJobSelect();
        BuildRows();
        BuildBotDetail();   // 详情弹窗运行时建树（点榜单行打开；不进 prefab 契约，主人改预制体不受影响）

        ArenaRules.Changed += Refresh;
    }

    void BindResult()
    {
        var result = Bind<Image>("Result");
        if (result == null) return;
        _result = result.gameObject;
        var rt = result.transform;   // Result 内部从这里搜，避开 RowTemplate 里的同名节点
        _resBg = Bind<Image>(rt, "ResultBgImg");
        _resMyIcon = Bind<Image>(rt, "MyIconImg");
        _resBotIcon = Bind<Image>(rt, "BotIconImg");
        _resMyLabel = Bind<Text>(rt, "MyLabelText");
        _resBotLabel = Bind<Text>(rt, "BotLabelText");
        _resVerdict = Bind<Text>(rt, "VerdictText");
        _resPts = Bind<Text>(rt, "ResultPtsText");
        _resFlavor = Bind<Text>(rt, "FlavorText");
        _resGold = Bind<Text>(rt, "GoldText");
        _resPtsIcon = Bind<Image>(rt, "PtsIconImg");

        var ok = UiFind.Deep(rt, "OkBtn");
        if (ok != null)
        {
            var btn = ok.GetComponent<Button>();
            if (btn == null) btn = ok.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => { if (_result != null) _result.SetActive(false); });
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：OkBtn"); }

        // 积分图标（主人口径：胜利显示获得的积分图标和积分数）
        if (_resPtsIcon != null)
        {
            _resPtsIcon.gameObject.SetActive(false);
            var sp = Resources.Load<Sprite>(PointsIconPath);
            if (sp == null)
                Debug.LogError("[Arena] 积分图标加载失败，Resources 路径：" + PointsIconPath);
            else
                _resPtsIcon.sprite = sp;
        }
        _result.SetActive(false);
    }

    void BindJobSelect()
    {
        var js = Bind<Image>("JobSelect");
        if (js == null) return;
        _jobSelect = js.gameObject;
        var jt = js.transform;   // JobSelect 内部从这里搜

        var cancel = UiFind.Deep(jt, "JobCancelBtn");
        if (cancel != null)
        {
            var btn = cancel.GetComponent<Button>();
            if (btn == null) btn = cancel.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => { _jobSelect.SetActive(false); _pendingRowIdx = -1; });
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：JobCancelBtn"); }

        // 职业卡：填图标/名字 + 接点击（ playable 超过 3 张时多余卡隐藏）
        var jobs = PlayerJobDefs.Playable;
        for (int i = 0; i < 3; i++)
        {
            var card = Bind<Image>(jt, "JobCard" + i);
            if (card == null) continue;
            if (i >= jobs.Length) { card.gameObject.SetActive(false); continue; }
            var iconT = UiFind.Deep(card.transform, "JobIcon");
            var labelT = UiFind.Deep(card.transform, "JobNameText");
            var iconImg = iconT != null ? iconT.GetComponent<Image>() : null;
            var labelTxt = labelT != null ? labelT.GetComponent<Text>() : null;
            var sp = PlayerJobDefs.TryLoadJobIcon(jobs[i]);
            if (sp == null)
                Debug.LogError("[Arena] 职业图标加载失败：" + jobs[i]);
            else if (iconImg != null) iconImg.sprite = sp;
            if (labelTxt != null) labelTxt.text = PlayerJobDefs.Get(jobs[i]).DisplayName;

            var cb = card.GetComponent<Button>();
            if (cb == null) cb = card.gameObject.AddComponent<Button>();
            cb.targetGraphic = card;
            PlayerJobId job = jobs[i];
            cb.onClick.AddListener(() => OnJobPicked(job));
        }
        _jobSelect.SetActive(false);
    }

    void BuildRows()
    {
        _content.sizeDelta = new Vector2(640f, ContentH);   // BotCount 变化时运行时同步
        if (_rowTemplate.activeSelf) _rowTemplate.SetActive(false);   // 模板恒隐藏（防手滑）

        for (int i = 0; i < RowTotal; i++)
        {
            var row = Object.Instantiate(_rowTemplate, _content);
            row.name = "Row_" + i;
            row.SetActive(true);
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -(i * RowStep + 4f));
                rt.sizeDelta = new Vector2(640f, RowH);
            }
            var rowImg = row.GetComponent<Image>();
            if (rowImg != null) rowImg.color = i % 2 == 0 ? _rowColorA : _rowColorB;
            _rowRoots[i] = row;

            var rankT = UiFind.Deep(row.transform, "RankText");
            _rowRank[i] = rankT != null ? rankT.GetComponent<Text>() : null;
            var headT = UiFind.Deep(row.transform, "HeadImg");
            _rowHead[i] = headT != null ? headT.GetComponent<Image>() : null;
            var nameT = UiFind.Deep(row.transform, "NameText");
            _rowName[i] = nameT != null ? nameT.GetComponent<Text>() : null;
            var ptsT = UiFind.Deep(row.transform, "PtsText");
            _rowPts[i] = ptsT != null ? ptsT.GetComponent<Text>() : null;
            var dashT = UiFind.Deep(row.transform, "DashText");
            _rowDash[i] = dashT != null ? dashT.gameObject : null;

            // 名次徽章色：Row_i 的名次恒为 i+1（1 金 / 2 银 / 3 铜 / 其余灰白），克隆时设一次
            if (_rowRank[i] != null) _rowRank[i].color = RankColor(i + 1);

            var fight = UiFind.Deep(row.transform, "FightBtn");
            if (fight == null)
            {
                Debug.LogError("[Arena] 行模板缺节点：FightBtn");
                _bindFailed = true;
                continue;
            }
            _rowFight[i] = fight.gameObject;
            var btn = fight.GetComponent<Button>();
            if (btn == null) btn = fight.gameObject.AddComponent<Button>();
            int idx = i;
            btn.onClick.AddListener(() => OnChallengeClicked(idx));

            // 行点击 → 机器人/我的档案详情（运行时补 Button，不改预制体；拖动不受影响——事件冒泡给 ScrollRect）
            if (rowImg != null) rowImg.raycastTarget = true;
            var rowBtn = row.GetComponent<Button>();
            if (rowBtn == null) rowBtn = row.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            rowBtn.onClick.AddListener(() => ShowDetailForRow(idx));
        }
    }

    /// <summary>名次徽章色：1 金 / 2 银 / 3 铜 / 其余灰白（取 RarityPalette 语义，别处不私抄）。</summary>
    static Color RankColor(int rank)
    {
        if (rank == 1) return RarityPalette.Legendary;
        if (rank == 2) return RarityPalette.Common;
        if (rank == 3) return new Color(0.85f, 0.55f, 0.30f, 1f);   // 铜（Palette 无铜档，此处唯一）
        return RarityPalette.Common;
    }

    // ───────────────────────── 交互 / 刷新 ─────────────────────────

    void OnChallengeClicked(int rowIdx)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        if (!ArenaRules.IsChallengeable(data, rowIdx)) return;
        if (ArenaRules.TriesLeft(data) <= 0)
        {
            GlobalToastUI.Show($"今天的挑战次数用完了（每天 {ArenaRules.DailyTries} 次，看广告还能加）", true);
            return;
        }
        _pendingRowIdx = rowIdx;
        if (_jobSelect != null) _jobSelect.SetActive(true);
    }

    void OnJobPicked(PlayerJobId job)
    {
        if (_jobSelect != null) _jobSelect.SetActive(false);
        int rowIdx = _pendingRowIdx;
        _pendingRowIdx = -1;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || rowIdx < 0) return;
        if (!ArenaRules.IsChallengeable(data, rowIdx)) return;
        if (!ArenaRules.TryConsumeChallenge()) return;

        _chosenJob = job;
        // 挑战目标都在玩家上方：榜单行号 == 机器人列表下标；先抓引用（换位后对象不变，名字/头像照用）
        var bot = data.arenaBots[rowIdx];
        int pts, gold, powerDelta;
        bool won = ArenaRules.ResolveChallenge(data, rowIdx, out pts, out gold, out powerDelta);
        if (bot == null) return;
        ShowResult(won, bot, pts, gold, powerDelta);
        Refresh();
        ScrollToPlayer();
    }

    void ShowResult(bool won, ArenaBotEntry bot, int pts, int gold, int powerDelta)
    {
        SetResultBg();
        if (_resMyIcon != null)
        {
            var sp = PlayerJobDefs.TryLoadJobIcon(_chosenJob);
            _resMyIcon.sprite = sp;
            if (sp == null) Debug.LogError("[Arena] 出战职业图标加载失败：" + _chosenJob);
        }
        if (_resBotIcon != null)
        {
            var sp = MercPortraitSprites.GetHead("H" + bot.avatar.ToString("D3"));
            _resBotIcon.sprite = sp;
            if (sp == null) Debug.LogError("[Arena] 机器人头像加载失败：H" + bot.avatar.ToString("D3"));
        }
        if (_resMyLabel != null) _resMyLabel.text = "我·" + PlayerJobDefs.Get(_chosenJob).DisplayName;
        if (_resBotLabel != null) _resBotLabel.text = bot.name;
        if (_resVerdict != null)
        {
            _resVerdict.text = won ? "胜利！" : "惜败…";
            _resVerdict.color = won ? _txtGold : _txtSub;
        }
        if (_resPts != null)
        {
            _resPts.text = won ? $"+{pts} 积分" : $"{pts} 积分";   // 输了 pts 为负（掉积分）
            _resPts.fontSize = won ? 48 : 24;
            _resPts.color = won ? _txtGold : _txtSub;
        }
        if (_resPtsIcon != null)
            _resPtsIcon.gameObject.SetActive(won);   // 只有胜利亮积分图标（主人口径）
        if (_resFlavor != null)
        {
            var pool = won ? WinFlavors : LoseFlavors;
            _resFlavor.text = pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (_resGold != null)
        {
            // 战力变化随输赢展示（主人口径「玩家战力根据输赢情况调整」要看得见）；色取 RarityPalette 语义色
            string powerPart = powerDelta >= 0
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(RarityPalette.PowerGain)}>战力 +{powerDelta}</color>"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(RarityPalette.PowerLoss)}>战力 {powerDelta}</color>";
            _resGold.text = won ? $"金币 +{gold}　{powerPart}" : powerPart;
            _resGold.gameObject.SetActive(true);
        }
        if (_result != null) _result.SetActive(true);
    }

    /// <summary>结算背景：随机章节战斗地图（每次结算都换一张）。</summary>
    void SetResultBg()
    {
        if (_resBg == null) return;
        var reg = Resources.Load<BattleBackgroundRegistry>("Config/BattleBackgroundRegistry");
        if (reg == null)
        {
            Debug.LogError("[Arena] 战斗背景注册表加载失败：Resources/Config/BattleBackgroundRegistry");
            return;
        }
        int chapter = UnityEngine.Random.Range(1, 9);
        var sp = reg.GetLayerSprite(chapter, "mid");
        if (sp == null) sp = reg.GetLayerSprite(chapter, "back");
        if (sp == null)
        {
            Debug.LogError($"[Arena] 第 {chapter} 章战斗背景缺失（mid/back 都为空）");
            return;
        }
        _resBg.sprite = sp;
        _resBg.preserveAspect = true;
    }

    void Refresh()
    {
        if (_bindFailed) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        ArenaRules.EnsurePeriods(data);   // 幂等；保证换位链合法（老档迁移也在这里触发）

        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        int left = ArenaRules.TriesLeft(data);
        int total = ArenaRules.DailyTries + data.arenaAdViewsToday * ArenaRules.AdBonusTries;
        int myPower = ArenaRules.GetPlayerPower(data);

        if (_triesText != null) _triesText.text = $"对战次数 {left}/{total}";
        if (_adBtnText != null)
            _adBtnText.text = data.arenaAdViewsToday >= ArenaRules.AdViewCapPerDay ? "已满" : $"+{ArenaRules.AdBonusTries}";

        // 我的排名条
        if (_myRankText != null) _myRankText.text = $"我的排名 第{pos + 1}名";
        if (_myName != null) _myName.text = "我";
        if (_myPtsText != null) _myPtsText.text = myPower.ToString();
        if (_myHead != null)
        {
            var sp = MercPortraitSprites.GetHead("player");
            _myHead.sprite = sp;
            if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
        }

        // 前三领奖台：排名 r 的实体 = r-1 < pos ? 机器人[listIdx=r-1] : 玩家
        var cardColors = new[] { new Color(0.62f, 0.44f, 0.14f, 0.95f), new Color(0.42f, 0.46f, 0.56f, 0.95f), new Color(0.52f, 0.33f, 0.20f, 0.95f) };
        for (int r = 1; r <= 3; r++)
        {
            int idx = r - 1;
            bool isMe = idx < pos;
            var bot = !isMe && idx < data.arenaBots.Count ? data.arenaBots[idx] : null;
            int i = r - 1;
            if (_podRank[i] != null)
            {
                _podRank[i].text = r.ToString();
                _podRank[i].color = RankColor(r);
            }
            if (isMe)
            {
                if (_podName[i] != null) _podName[i].text = "我";
                if (_podPts[i] != null) _podPts[i].text = myPower.ToString();
                if (_podHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("player");
                    _podHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
                }
            }
            else if (bot != null)
            {
                if (_podName[i] != null) _podName[i].text = bot.name;
                if (_podPts[i] != null) _podPts[i].text = ArenaRules.GetBotPower(data, idx).ToString();
                if (_podHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("H" + bot.avatar.ToString("D3"));
                    _podHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 机器人头像加载失败：H" + bot.avatar.ToString("D3"));
                }
            }
            if (_podRoot[i] != null) _podRoot[i].color = cardColors[i];
        }

        for (int i = 0; i < RowTotal; i++)
        {
            bool isPlayer = i == pos;
            int listIdx = i < pos ? i : i - 1;   // 玩家行没有对应机器人
            var bot = (!isPlayer && listIdx >= 0 && listIdx < data.arenaBots.Count) ? data.arenaBots[listIdx] : null;
            bool on = isPlayer || bot != null;
            if (_rowRoots[i] != null) _rowRoots[i].SetActive(on);
            if (!on) continue;

            // 行底色：只在「是不是玩家行」切换时写，主人改的隔行色平时不被覆盖
            if (isPlayer != _rowIsMine[i])
            {
                _rowIsMine[i] = isPlayer;
                if (_rowRoots[i] != null)
                {
                    var img = _rowRoots[i].GetComponent<Image>();
                    if (img != null) img.color = isPlayer ? _rowMine : (i % 2 == 0 ? _rowColorA : _rowColorB);
                }
            }

            if (_rowRank[i] != null) _rowRank[i].text = (i + 1).ToString();
            if (isPlayer)
            {
                if (_rowName[i] != null) _rowName[i].text = "我";
                if (_rowPts[i] != null) _rowPts[i].text = myPower.ToString();
                if (_rowHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("player");
                    _rowHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
                }
            }
            else
            {
                if (_rowName[i] != null) _rowName[i].text = bot.name;
                if (_rowPts[i] != null) _rowPts[i].text = ArenaRules.GetBotPower(data, listIdx).ToString();
                if (_rowHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("H" + bot.avatar.ToString("D3"));
                    _rowHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 机器人头像加载失败：H" + bot.avatar.ToString("D3"));
                }
            }

            // 「对战」按钮 vs 「—」互斥（参考图口径：可挑战亮按钮，其余灰杠）
            bool canFight = !isPlayer && ArenaRules.IsChallengeable(data, i);
            if (_rowFight[i] != null) _rowFight[i].SetActive(canFight);
            if (_rowDash[i] != null) _rowDash[i].SetActive(!canFight);
        }
    }

    /// <summary>把玩家行滚到视口中间。</summary>
    void ScrollToPlayer()
    {
        if (_scroll == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        float offset = pos * RowStep + RowH * 0.5f - ViewportH * 0.5f;   // 玩家行中心对齐视口中心
        float max = Mathf.Max(1f, ContentH - ViewportH);
        _scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - offset / max);
    }

    /// <summary>排行榜奖励说明（铁律：纯告知走通用 ShowNotice 单按钮，不手搓弹窗）。</summary>
    void ShowRewardInfo()
    {
        var now = System.DateTime.Now;
        StaminaConfirmPopup.ShowNotice("排行榜奖励",
            $"每月 1 日按最终排名发奖（当前 {now.Year}年{now.Month}月赛季）：\n" +
            "第 1 名　金币×2000 + 钻石×200\n" +
            "第 2~3 名　金币×1200 + 钻石×100\n" +
            "第 4~10 名　金币×800 + 钻石×50\n" +
            "第 11 名及以后　金币×400\n\n" +
            "赛季积分清零，榜单换一批新对手。",
            "知道了", null);
    }

    /// <summary>领奖台点卡 → 详情：排名 rank 的实体（rank-1 &lt; 玩家位 = 玩家自己，否则机器人）。</summary>
    void ShowDetailForRank(int rank)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, ArenaRules.BotCount);
        int idx = rank - 1;
        if (idx < pos) { ShowMyDetail(data, pos); return; }
        if (idx < data.arenaBots.Count) ShowBotDetail(data, data.arenaBots[idx], idx);
    }

    // ───────────────────────── 档案详情弹窗（运行时建树，不进 prefab 契约） ─────────────────────────

    /// <summary>点榜单行：玩家行弹我的档案（战力估计+积分），机器人行弹机器人档案（战力+佣兵/技能/装备）。</summary>
    void ShowDetailForRow(int rowIdx)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, ArenaRules.BotCount);
        if (rowIdx == pos) { ShowMyDetail(data, pos); return; }
        int listIdx = rowIdx < pos ? rowIdx : rowIdx - 1;
        if (listIdx < 0 || listIdx >= data.arenaBots.Count) return;
        ShowBotDetail(data, data.arenaBots[listIdx], listIdx);
    }

    void ShowBotDetail(SaveData data, ArenaBotEntry bot, int listIdx)
    {
        if (_detailRoot == null || bot == null) return;
        int rank = ArenaRules.RankOfIndex(data, listIdx);
        if (_dTitle != null) _dTitle.text = $"{bot.name} · 第{rank}名";
        if (_dPower != null) _dPower.text = $"战力 {ArenaRules.GetBotPower(data, listIdx)}";
        if (_dPoints != null) _dPoints.text = $"赛季积分 {bot.points}";
        FillLoadout(ArenaRules.BuildBotLoadout(bot));
        _detailRoot.SetActive(true);
    }

    void ShowMyDetail(SaveData data, int pos)
    {
        if (_detailRoot == null) return;
        if (_dTitle != null) _dTitle.text = $"我 · 第{pos + 1}名";
        if (_dPower != null) _dPower.text = $"战力 {ArenaRules.GetPlayerPower(data)}";
        if (_dPoints != null) _dPoints.text = $"赛季积分 {data.arenaPoints}";
        // 玩家没有假阵容；给一条估计机制说明（主人口径「战力根据输赢情况调整」要让玩家看见）
        ClearDetailRows();
        var tip = UiFactory.NewTextColored(_detailRows, "MyTip", "你的战力随输赢浮动：\n赢一把 +5% · 输一把 -4% —— 打赢强敌涨得快",
            24, TextAnchor.MiddleLeft, _txtSub);
        SetRT(tip.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, -8f), V2(488f, 80f));
        _detailRoot.SetActive(true);
    }

    void CloseDetail()
    {
        if (_detailRoot != null) _detailRoot.SetActive(false);
    }

    /// <summary>详情弹窗建树（一次性；UiFactory 真源建 Image/Text，字体 GameFonts）。</summary>
    void BuildBotDetail()
    {
        if (_detailRoot != null) return;

        var dim = UiFactory.NewImage(transform, "BotDetail", new Color(0f, 0f, 0f, 0.66f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 挡住下层点击
        _detailRoot = dim.gameObject;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(CloseDetail);   // 点空白关闭

        var box = UiFactory.NewImage(dim.transform, "DetailBox", _panelColor);
        SetRT(box.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(560f, 860f));

        var close = UiFactory.NewImage(box.transform, "DCloseBtn", _btnSlate);
        SetRT(close.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(240f, -36f), V2(56f, 56f));
        close.raycastTarget = true;
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = close;
        closeBtn.onClick.AddListener(CloseDetail);
        var closeLabel = UiFactory.NewText(close.transform, "Label", "X", 24, TextAnchor.MiddleCenter);
        Stretch(closeLabel.rectTransform);

        _dTitle = UiFactory.NewText(box.transform, "DTitle", "", 24, TextAnchor.MiddleCenter);
        SetRT(_dTitle.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -70f), V2(460f, 40f));

        _dPower = UiFactory.NewText(box.transform, "DPower", "", 48, TextAnchor.MiddleCenter);
        _dPower.color = _txtGold;
        SetRT(_dPower.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -135f), V2(460f, 70f));

        _dPoints = UiFactory.NewText(box.transform, "DPoints", "", 24, TextAnchor.MiddleCenter);
        _dPoints.color = _txtSub;
        SetRT(_dPoints.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -185f), V2(460f, 34f));

        var rows = new GameObject("DetailRows", typeof(RectTransform));
        rows.transform.SetParent(box.transform, false);
        // 标题区（名字/战力/积分）占到 -200，行容器从 -210 起往下，避免叠印
        SetRT(rows.GetComponent<RectTransform>(), V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, -210f), V2(560f, 640f));
        _detailRows = rows.transform;

        _detailRoot.SetActive(false);   // 运行时建树弹层，建完即藏
    }

    /// <summary>渲染阵容档案：按 section 分段（0 佣兵带头像 / 1 技能 / 2 装备），颜色走 RarityPalette。
    /// ⚠ LoadoutEntry 嵌套在 ArenaRules 类里，外部引用必须带前缀（裸名字会 CS0246）。</summary>
    void FillLoadout(List<ArenaRules.LoadoutEntry> entries)
    {
        ClearDetailRows();
        if (entries == null || entries.Count == 0) return;

        float y = -8f;
        int lastSection = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.section != lastSection)
            {
                lastSection = e.section;
                var secT = UiFactory.NewText(_detailRows, "Sec" + i, SectionName(e.section), 24, TextAnchor.MiddleLeft);
                secT.color = _txtGold;
                SetRT(secT.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, y), V2(480f, 34f));
                y -= 42f;
            }
            if (e.iconId > 0)
            {
                var img = UiFactory.NewImage(_detailRows, "Merc" + i, Color.white);
                img.preserveAspect = true;
                SetRT(img.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-214f, y - 24f), V2(44f, 44f));
                var sp = MercPortraitSprites.GetHead("H" + e.iconId.ToString("D3"));
                if (sp == null) Debug.LogError("[Arena] 详情佣兵头像加载失败：H" + e.iconId.ToString("D3"));
                else img.sprite = sp;

                var t = UiFactory.NewTextColored(_detailRows, "MercT" + i, e.label, 24, TextAnchor.MiddleLeft, e.color);
                SetRT(t.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-184f, y - 24f), V2(380f, 32f));
                y -= 48f;
            }
            else
            {
                var t = UiFactory.NewTextColored(_detailRows, "Item" + i, e.label, 24, TextAnchor.MiddleLeft, e.color);
                SetRT(t.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, y - 15f), V2(480f, 30f));
                y -= 30f;
            }
        }
    }

    /// <summary>清空详情行容器（重建前调用）。</summary>
    void ClearDetailRows()
    {
        if (_detailRows == null) return;
        for (int i = _detailRows.childCount - 1; i >= 0; i--)
            Destroy(_detailRows.GetChild(i).gameObject);
    }

    static string SectionName(int section)
        => section == 0 ? "佣兵阵容" : section == 1 ? "技能" : "装备";

    // ───────────────────────── 建树小工具（详情弹窗运行时建树用） ─────────────────────────

    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static void SetRT(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
