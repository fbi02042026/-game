using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 看广告送佣兵活动面板（艾拉，2026-10-31 立项）。
/// 运行逻辑见 <see cref="AdMercEventSystem"/>（计数铁律 / 邮件发放 / 红点都在那边，本类只管显示）。
///
/// 预制体契约（生成器：菜单 Tools → 看广告活动 → 生成活动面板预制体；路径
/// Resources/Prefabs/UI/AdMercEventPanel；2026-10-31 晚按主人设计稿排版，美术
/// Resources/UI/广告送佣兵/，底图已含人物/标题/副标题/礼盒框装饰）：
///   AdMercEventPanel(root: Canvas+CanvasScaler(Match Height 1280)+GraphicRaycaster+AdMercEventUI, 保存为 inactive)
///   【2026-10-31 适配】Canvas 运行时被 UICanvasSetup.ApplyPopup 接管（ScreenSpaceCamera+瘦屏按宽匹配），
///   预制体里的 Overlay / Match Height 只是初始值，见 ApplyPopupFit。
///   └─ Root
///      ├─ Dim（全屏遮罩，点击关窗）
///      └─ Panel（680×629，底图 597×552 等比放大，不 stretch）
///         ├─ CloseBtn（右上透明热区，+ 号美术在底图上）
///         ├─ DaysText（累计 x/10 天，进度条上方）
///         ├─ ProgressFill（进度条填充，Filled Horizontal，fillAmount=Days/MaxDay 运行时刷）
///         ├─ StarL / StarR（进度条两端金色星饰，纯装饰）
///         ├─ GiftRow
///         │  ├─ Gift0{GiftIcon, DayText}…Gift3{…}（RewardText/ClaimMarkText 可选，
///         │  │   设计稿无此二节点时运行时自动跳过；红点由代码 RedDot.Bind 挂上）
///         ├─ WatchAdBtn{Label「立即观看」}（运行时【不改文字】只调透明度：今日记满/攒满变灰，
///         │   变灰仍可点 → 弹「今日已满」类 toast；计数收口在 RewardedAdBridge.RewardedSuccess
///         │   ——看任意地方的广告都可累计，2026-10-31 主人拍板）
///         └─ HintText（底部说明，默认空）
/// 旧契约里的 TitleText / MercImage / MercNameText 已并入底图美术，节点省略（绑定代码 null 安全）。
///
/// 交互口径（2026-10-31 主人需求）：
///   · 点礼盒 → 弹详情浮层（代码现搭，样式仿战斗中点装备的 BackpackItemActionUI：
///     全屏透明遮罩点击关闭 + 小面板）；
///   · 详情浮层内「领取」钮只在可领时点亮，点击 = AdMercEventSystem.Claim → 邮件入箱；
///   · 可领取的礼盒右上角红点（RedDot 键 admerc.m0..m3），领取后红点灭。
/// </summary>
public class AdMercEventUI : MonoBehaviour
{
    public static AdMercEventUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/AdMercEventPanel";
    const string MercStandPath = "UI/Icons/MercStand/" + AdMercEventDefs.EventMercId;

    // ---- 配色（对齐 ArenaPopupUI 暗面板口径；只染运行时状态，不动预制体底色）----
    static readonly Color ColClaimedDim = new Color(1f, 1f, 1f, 0.5f);
    static readonly Color ColNormal = Color.white;

    Transform _root;
    Text _title;
    Text _mercName;
    Text _days;
    Button _watchBtn;
    Image _mercImage;
    Image _progressFill;   // 进度条填充（可选节点；fillAmount = 累计天数/最后一档）
    readonly Transform[] _gifts = new Transform[AdMercEventDefs.MilestoneCount];
    readonly Text[] _giftRewardTexts = new Text[AdMercEventDefs.MilestoneCount];
    readonly Text[] _giftMarkTexts = new Text[AdMercEventDefs.MilestoneCount];

    // 详情浮层（代码现搭）
    GameObject _detailDismiss;
    GameObject _detailPanel;
    Text _detailTitle;
    Text _detailProgress;
    Text _detailLines;
    Button _detailClaimBtn;
    Text _detailClaimLabel;
    int _detailIndex = -1;

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    public static void Show()
    {
        var ui = Ensure();
        if (ui != null) ui.Open();
    }

    public static void Hide()
    {
        if (Instance != null) Instance.Close();
    }

    static AdMercEventUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不做兜底（2026-10-08 主人拍板）：预制体缺失要当场看见，报错指路生成器菜单
            Debug.LogError($"[AdMercEvent] 活动面板预制体缺失：Resources/{PrefabPath} " +
                           "（菜单 Tools → 看广告活动 → 生成活动面板预制体 生成）");
            return null;
        }
        var go = Object.Instantiate(prefab);
        DontDestroyOnLoad(go);
        Instance = go.GetComponent<AdMercEventUI>();
        if (Instance == null)
        {
            Debug.LogError("[AdMercEvent] 预制体根上没挂 AdMercEventUI 组件，重新生成预制体");
            Object.Destroy(go);
            return null;
        }
        Instance.Bind();
        Instance.ApplyPopupFit();
        return Instance;
    }

    /// <summary>
    /// 屏幕适配收口（2026-10-31 主人报「这个界面没有做适配」）：走全项目弹窗统一出口
    /// <see cref="UICanvasSetup.ApplyPopup"/>——ScreenSpaceCamera + 绑 UI 相机 + 720×1280 参考分辨率 +
    /// matchWidthOrHeight 按 <see cref="DesignAspectLetterbox.ResolveUiMatch"/> 动态解析（瘦屏按宽匹配，
    /// 逻辑宽恒 720 不被左右裁）+ 根铺满。预制体自带的 Overlay Canvas + 写死 Match Height 不再生效。
    /// 运行时改，不碰 .prefab。
    /// </summary>
    void ApplyPopupFit()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[AdMercEvent] 预制体根上没有 Canvas，弹窗适配挂不上");
            return;
        }
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        // 每次打开重绑相机 + 重算适配（DDOL 弹窗跨场景 / 分辨率变化后的统一收口，幂等）
        UICanvasSetup.RefreshPopup(GetComponent<Canvas>(), GameConfig.UiSort.TownPopup);
        AdMercEventSystem.RefreshRedDots();
        Refresh();
    }

    void Close()
    {
        HideDetail();
        gameObject.SetActive(false);
    }

    // ============================================================
    // 节点绑定（按契约名，找不到 = LogError 不静默吞）
    // ============================================================

    void Bind()
    {
        _root = Deep(transform, "Root");
        if (_root == null) { Debug.LogError("[AdMercEvent] 缺节点 Root"); return; }

        var titleT = Deep(_root, "TitleText");
        _title = titleT != null ? titleT.GetComponent<Text>() : null;
        var closeBtnT = Deep(_root, "CloseBtn");
        if (closeBtnT != null)
        {
            var btn = closeBtnT.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(Close);
        }
        var dimT = Deep(_root, "Dim");
        if (dimT != null)
        {
            var dimBtn = dimT.GetComponent<Button>() ?? dimT.gameObject.AddComponent<Button>();
            dimBtn.onClick.AddListener(Close);   // 点遮罩关窗（ArenaPopupUI 同款交互）
        }

        var mercImgT = Deep(_root, "MercImage");
        _mercImage = mercImgT != null ? mercImgT.GetComponent<Image>() : null;

        // 进度条填充（可选；设计稿排版下展示累计进度，缺节点只是不显示进度条）
        var fillT = Deep(_root, "ProgressFill");
        _progressFill = fillT != null ? fillT.GetComponent<Image>() : null;
        if (_progressFill == null)
            Debug.LogWarning("[AdMercEvent] 缺节点 ProgressFill，进度条不显示（仅影响展示）");

        var nameT = Deep(_root, "MercNameText");
        _mercName = nameT != null ? nameT.GetComponent<Text>() : null;

        var daysT = Deep(_root, "DaysText");
        _days = daysT != null ? daysT.GetComponent<Text>() : null;

        var watchT = Deep(_root, "WatchAdBtn");
        if (watchT != null)
        {
            _watchBtn = watchT.GetComponent<Button>();
            // 【2026-10-31 主人拍板】按钮文字永远不动（预制体「立即观看」），状态只靠变灰表达
            if (_watchBtn != null) _watchBtn.onClick.AddListener(OnWatchAd);
        }

        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
        {
            var giftT = Deep(_root, "Gift" + i);
            _gifts[i] = giftT;
            if (giftT == null)
            {
                Debug.LogError($"[AdMercEvent] 缺礼盒节点 Gift{i}，重新生成预制体");
                continue;
            }
            var rewardT = Deep(giftT, "RewardText");
            _giftRewardTexts[i] = rewardT != null ? rewardT.GetComponent<Text>() : null;
            var markT = Deep(giftT, "ClaimMarkText");
            _giftMarkTexts[i] = markT != null ? markT.GetComponent<Text>() : null;

            var gBtn = giftT.GetComponent<Button>();
            if (gBtn == null) gBtn = giftT.gameObject.AddComponent<Button>();
            int idx = i;   // 闭包捕获
            // 【2026-10-31 主人拍板】可领取（有红点）时点礼盒**直接发邮件**；
            // 未达成/已领时才弹详情浮层（看进度和奖励明细）。
            gBtn.onClick.AddListener(() => OnGiftClicked(idx));

            // 可领取红点（键 admerc.m{i}；状态由 AdMercEventSystem.RefreshRedDots 统一 Set）
            RedDot.Bind(giftT, AdMercEventSystem.MilestoneRedDotKey(i), new Vector2(-8f, -8f), 26f);
        }

        var hintT = Deep(_root, "HintText");
        if (hintT == null) Debug.LogWarning("[AdMercEvent] 缺节点 HintText（仅说明文字，可后补）");
    }

    // ============================================================
    // 刷新（状态 → 显示；只染状态色，不动预制体排版）
    // ============================================================

    void Refresh()
    {
        int days = AdMercEventSystem.Days;

        if (_title != null)
            _title.text = AdMercEventSystem.AllClaimed() ? "看广告 领艾拉（已完成）" : "看广告 领艾拉";

        if (_mercName != null && string.IsNullOrEmpty(_mercName.text))
            _mercName.text = "艾拉·风羽（稀有·游侠）";   // 预制体里可手改，这里只在空时补默认

        if (_days != null)
        {
            // 【2026-10-31 主人拍板】双参数：当天已看 x/3（看满 3 个记 1 天，当天再看不计）+ 累计天数
            _days.text = $"当天已看广告 {AdMercEventSystem.TodayAdProgress()}/{AdMercEventDefs.AdsPerDay}，已累计 {days} 天";
        }

        // 进度条：0.15/0.4/0.65/0.95 是【可领取节点】（3/5/7/10 天）的刻度，相邻节点间按天数线性插值
        // （中间每天走多少代码自己算）；当天看广告的过程中平滑爬向下一格，当天记满 1 天后只按整数天走
        if (_progressFill != null)
        {
            float effDays = days + (AdMercEventSystem.IsDayCountedToday()
                ? 0f
                : AdMercEventSystem.TodayAdProgress() / (float)AdMercEventDefs.AdsPerDay);
            _progressFill.fillAmount = AdMercEventSystem.DayToFillAmount(effDays);
        }

        if (_mercImage != null && _mercImage.sprite == null)
        {
            var sp = Resources.Load<Sprite>(MercStandPath);
            if (sp != null) { _mercImage.sprite = sp; _mercImage.preserveAspect = true; }
            else Debug.LogError($"[AdMercEvent] 立绘加载失败：Resources/{MercStandPath}（面板露白图，主人可在 prefab 手拖）");
        }

        // 【2026-10-31 主人拍板】按钮文字不动（预制体「立即观看」）：今日记满/攒满只**变灰**，
        // 仍可点击 → OnWatchAd 弹「今日已满」类 toast（不拉广告）
        if (_watchBtn != null)
        {
            bool canWatch = AdMercEventSystem.CanWatchToday();
            var cg = _watchBtn.GetComponent<CanvasGroup>();
            if (cg == null) cg = _watchBtn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = canWatch ? 1f : 0.45f;
        }

        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
        {
            var giftT = _gifts[i];
            if (giftT == null) continue;

            bool claimed = AdMercEventSystem.IsClaimed(i);
            bool reached = AdMercEventSystem.IsReached(i);

            var dayT = Deep(giftT, "DayText");
            if (dayT != null) dayT.GetComponent<Text>().text = $"{AdMercEventDefs.MilestoneDays[i]}天";

            if (_giftRewardTexts[i] != null)
            {
                var lines = AdMercEventSystem.GetRewardLines(i);
                _giftRewardTexts[i].text = lines != null && lines.Count > 0 ? string.Join("\n", lines.ToArray()) : "";
            }

            if (_giftMarkTexts[i] != null)
                _giftMarkTexts[i].text = claimed ? "已领取" : (reached ? "可领取" : "");

            // 已领取整体压暗（只调 CanvasGroup，不改预制体底图）
            var cg = giftT.GetComponent<CanvasGroup>();
            if (cg == null) cg = giftT.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = claimed ? 0.55f : 1f;
        }
    }

    // ============================================================
    // 看广告
    // ============================================================

    void OnWatchAd()
    {
        // 变灰仍可点：不拉广告，直接弹提示（今日已记满 / 已攒满 / 全领完）
        if (!AdMercEventSystem.CanWatchToday())
        {
            if (AdMercEventSystem.AllClaimed() || AdMercEventSystem.Days >= AdMercEventDefs.MaxDay)
                GlobalToastUI.Show("已经攒满啦，点亮礼盒去邮箱领奖励吧");
            else
                GlobalToastUI.Show("今日已满，明天再来看");
            return;
        }
        AdMercEventSystem.WatchAd(ok =>
        {
            if (ok)
            {
                // 计数已在广播里落账（任意广告位都算，含别处看的），这里按最新状态提示
                if (AdMercEventSystem.IsDayCountedToday())
                    GlobalToastUI.Show($"今日 3 个已看完，累计 {AdMercEventSystem.Days}/{AdMercEventDefs.MaxDay} 天，明天再来看");
                else
                    GlobalToastUI.Show($"看广告成功，今日 {AdMercEventSystem.TodayAdProgress()}/{AdMercEventDefs.AdsPerDay} 个，已累计 {AdMercEventSystem.Days}/{AdMercEventDefs.MaxDay} 天");
            }
            Refresh();
        });
    }

    // ============================================================
    // 礼盒点击：可领取 = 直接发邮件；否则弹详情
    // ============================================================

    void OnGiftClicked(int index)
    {
        if (AdMercEventSystem.IsClaimable(index))
        {
            OnClaim(index);   // 直接发邮件（红点礼盒一键领）
            return;
        }
        ShowDetail(index);
    }

    // ============================================================
    // 详情浮层（代码现搭，样式仿 BackpackItemActionUI：遮罩点击关闭 + 小面板）
    // ============================================================

    void ShowDetail(int index)
    {
        if (index < 0 || index >= AdMercEventDefs.MilestoneCount) return;
        EnsureDetailBuilt();
        _detailIndex = index;

        bool claimable = AdMercEventSystem.IsClaimable(index);
        bool claimed = AdMercEventSystem.IsClaimed(index);

        if (_detailTitle != null)
            _detailTitle.text = $"第{index + 1}阶段 · {AdMercEventSystem.StageTitle(index)}";

        if (_detailProgress != null)
        {
            int need = AdMercEventDefs.MilestoneDays[index];
            int days = AdMercEventSystem.Days;
            _detailProgress.text = claimed
                ? "已领取（奖励在邮箱里，记得去领）"
                : $"进度 {Mathf.Min(days, need)}/{need} 天" + (claimable ? " · 可领取" : "");
        }

        if (_detailLines != null)
        {
            var lines = AdMercEventSystem.GetRewardLines(index);
            lines.Add("—— 奖励通过邮件发放 ——");
            _detailLines.text = string.Join("\n", lines.ToArray());
        }

        if (_detailClaimBtn != null)
        {
            _detailClaimBtn.interactable = claimable;
            if (_detailClaimLabel != null)
                _detailClaimLabel.text = claimed ? "已领取" : (claimable ? "领取（发邮箱）" : "未达成");
            _detailClaimBtn.onClick.RemoveAllListeners();
            _detailClaimBtn.onClick.AddListener(() => OnClaim(index));
        }

        _detailDismiss.SetActive(true);
        _detailPanel.SetActive(true);
        _detailPanel.transform.SetAsLastSibling();
    }

    void OnClaim(int index)
    {
        if (AdMercEventSystem.Claim(index))
        {
            GlobalToastUI.Show("奖励已发送至邮箱，请查收");
            HideDetail();
            Refresh();
        }
        else
        {
            // 不可领（未达标/已领）＝ 点击不该发生，防御性提示并留 Log
            Debug.LogWarning($"[AdMercEvent] 第{index + 1}阶段不可领取（未达标或已领），忽略");
            GlobalToastUI.Show("还未达成，先去看广告攒天数吧", true);
        }
    }

    void HideDetail()
    {
        if (_detailPanel != null) _detailPanel.SetActive(false);
        if (_detailDismiss != null) _detailDismiss.SetActive(false);
        _detailIndex = -1;
    }

    void EnsureDetailBuilt()
    {
        if (_detailPanel != null) return;
        Transform host = _root != null ? _root : transform;

        _detailDismiss = new GameObject("DetailDismiss", typeof(RectTransform), typeof(Image));
        _detailDismiss.transform.SetParent(host, false);
        var dImg = _detailDismiss.GetComponent<Image>();
        dImg.color = new Color(0f, 0f, 0f, 0.5f);
        dImg.raycastTarget = true;
        Stretch(_detailDismiss.GetComponent<RectTransform>());
        var dBtn = _detailDismiss.AddComponent<Button>();
        dBtn.onClick.AddListener(HideDetail);

        _detailPanel = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
        _detailPanel.transform.SetParent(host, false);
        var pImg = _detailPanel.GetComponent<Image>();
        pImg.color = new Color(0.12f, 0.12f, 0.16f, 0.97f);
        var pRt = _detailPanel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta = new Vector2(480f, 340f);

        _detailTitle = MakeText(_detailPanel.transform, "DetailTitle", 26, TextAnchor.MiddleCenter,
            new Color(1f, 0.82f, 0.42f));
        var tRt = _detailTitle.GetComponent<RectTransform>();
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -24f);
        tRt.sizeDelta = new Vector2(-40f, 40f);

        _detailProgress = MakeText(_detailPanel.transform, "DetailProgress", 22, TextAnchor.MiddleCenter,
            new Color(0.78f, 0.78f, 0.84f));
        var prRt = _detailProgress.GetComponent<RectTransform>();
        prRt.anchorMin = prRt.anchorMax = new Vector2(0.5f, 1f);
        prRt.pivot = new Vector2(0.5f, 1f);
        prRt.anchoredPosition = new Vector2(0f, -66f);
        prRt.sizeDelta = new Vector2(-40f, 34f);

        _detailLines = MakeText(_detailPanel.transform, "DetailLines", 24, TextAnchor.UpperCenter, Color.white);
        var lRt = _detailLines.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.5f, 0f);
        lRt.anchorMax = new Vector2(0.5f, 1f);
        lRt.pivot = new Vector2(0.5f, 0.5f);
        lRt.anchoredPosition = new Vector2(0f, 40f);
        lRt.sizeDelta = new Vector2(-48f, -140f);

        _detailClaimBtn = MakeButton(_detailPanel.transform, "ClaimBtn");
        var bRt = _detailClaimBtn.GetComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = new Vector2(0f, 18f);
        bRt.sizeDelta = new Vector2(220f, 56f);
        var bImg = _detailClaimBtn.GetComponent<Image>();
        bImg.color = new Color(0.82f, 0.52f, 0.16f, 1f);
        _detailClaimLabel = MakeText(_detailClaimBtn.transform, "Label", 24, TextAnchor.MiddleCenter, Color.white);
        Stretch(_detailClaimLabel.GetComponent<RectTransform>());

        _detailPanel.SetActive(false);
        _detailDismiss.SetActive(false);
    }

    // ============================================================
    // 构建辅助（与 BackpackItemActionUI 同款最小件）
    // ============================================================

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        if (t.font == null)
        {
            // 不做兜底伪装：字没就绪要当场看见（正常链路 StartupPreloader 已备好中文字体）
            Debug.LogError("[AdMercEvent] 中文字体未就绪（GameFonts.GetChinese() == null），详情浮层文字不渲染");
        }
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>递归找子节点（含 inactive；找不到返回 null）。</summary>
    static Transform Deep(Transform t, string name)
    {
        if (t == null || string.IsNullOrEmpty(name)) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = Deep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
