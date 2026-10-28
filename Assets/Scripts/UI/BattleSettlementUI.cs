using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算：通关 / 撤离 / 阵亡统一展示统计与奖励格。
/// 预制体：Resources/Prefabs/Battle/BattleSettlement
/// </summary>
public class BattleSettlementUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/BattleSettlement";
    const int MaxRewardCells = 16;
    const string ArtRoot = "Assets/Art/UI/战斗结算/";

    public static BattleSettlementUI Instance { get; private set; }

    public GameObject root;
    public Image panel;
    public Text titleText;
    public Text subtitleText;
    public Image portraitImage;
    public Text nameText;
    public Text valueDamage;
    public Text valueKill;
    public Text valueCrit;
    public Text valueCombo;
    public Text valueTaken;
    public Text valueHeal;
    public Transform rewardsGrid;
    public GameObject rewardsHeader;
    public Button confirmButton;
    public Text confirmLabel;
    public GameObject rewardCellPrefab;

    readonly List<GameObject> _spawnedCells = new List<GameObject>();
    Action _onConfirm;
    /// <summary>【2026-10-22 主人拍板】双钮：确认钮回调（下一关）；null = 单确认钮（死亡/撤离，行为不变）。</summary>
    Action _onNextStage;
    /// <summary>【2026-10-22 主人拍板】返回钮：运行时克隆 confirmButton（prefab 零改动），每次 Show 先清旧克隆防叠加。</summary>
    GameObject _backButtonClone;
    BattleRunStats _stats;
    // 【2026-10-10 主人拍板】MVP 台词不再走全屏 Toast：在立绘右侧放战斗里那个对话框（气泡同款图）。
    RectTransform mvpSpeechRt;
    Text mvpSpeechText;

    public static void Show(BattleRunStats stats, Action onConfirm)
    {
        // 【2026-10-18 主人报「撤离结算下面还能点抽奖」】进关抽奖面板是常驻的，玩家可以在
        // 面板开着时从暂停菜单撤离 —— 结算界面弹出时若不收，抽奖按钮和「点这里抽一次奖」
        // 引导气泡会叠在结算界面下面还能点。这里统一收（死亡/撤离/通关三条路都走 Show）：
        // Hide 内部会一并停倒计时、收引导手指和概率弹窗；幂等，面板没开时是空操作。
        BattleEntryDraftPanel.Hide();
        TutorialHintUI.Instance?.Hide();
        Ensure().Open(stats, onConfirm);
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】双钮重载（v4 通关收尾）：confirmButton =【下一关】（onNextStage），
    /// 返回钮 = 运行时克隆 confirmButton、文案「返回」、放确认钮左侧，点击 = onConfirm（回城原行为）。
    /// onNextStage 传 null → 不克隆返回钮、确认钮文案回「确定」，单确认钮路径（死亡/撤离）行为完全不变。
    /// </summary>
    public static void Show(BattleRunStats stats, Action onConfirm, Action onNextStage, string nextLabel = "下一关")
    {
        BattleEntryDraftPanel.Hide();
        TutorialHintUI.Instance?.Hide();
        Ensure().Open(stats, onConfirm, onNextStage, nextLabel);
    }

    public static BattleSettlementUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "BattleSettlement";
        }
        else
        {
            Debug.LogWarning($"[BattleSettlement] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("BattleSettlement", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        return go.GetComponent<BattleSettlementUI>() ?? go.AddComponent<BattleSettlementUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BindRefs();
        Wire();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        if (panel == null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (subtitleText == null)
            subtitleText = FindDeep(root.transform, "Subtitle")?.GetComponent<Text>();
        if (portraitImage == null)
            portraitImage = ResolvePortraitImage(root.transform);
        if (nameText == null)
            nameText = FindDeep(root.transform, "NameText")?.GetComponent<Text>();
        if (valueDamage == null)
            valueDamage = FindDeep(root.transform, "StatRow_Damage")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Damage")?.GetComponent<Text>();
        if (valueKill == null)
            valueKill = FindDeep(root.transform, "StatRow_Kill")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Kill")?.GetComponent<Text>();
        if (valueCrit == null)
            valueCrit = FindDeep(root.transform, "StatRow_Crit")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Crit")?.GetComponent<Text>();
        if (valueCombo == null)
            valueCombo = FindDeep(root.transform, "StatRow_Combo")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Combo")?.GetComponent<Text>();
        if (valueTaken == null)
            valueTaken = FindDeep(root.transform, "StatRow_Taken")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Taken")?.GetComponent<Text>();
        if (valueHeal == null)
            valueHeal = FindDeep(root.transform, "StatRow_Heal")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Heal")?.GetComponent<Text>();
        if (rewardsGrid == null)
            rewardsGrid = FindDeep(root.transform, "RewardsGrid");
        if (confirmButton == null)
            confirmButton = FindDeep(root.transform, "ConfirmButton")?.GetComponent<Button>();
        if (confirmLabel == null && confirmButton != null)
            confirmLabel = confirmButton.GetComponentInChildren<Text>(true);
        if (rewardCellPrefab == null)
        {
            var tmpl = FindDeep(root.transform, "RewardCellTemplate");
            if (tmpl != null) rewardCellPrefab = tmpl.gameObject;
        }
        // 模板格只是克隆母版，不能自己显示出来（否则会多出一个假奖励格，图标还是预制体占位图）
        if (rewardCellPrefab != null && rewardCellPrefab.activeSelf)
            rewardCellPrefab.SetActive(false);
    }

    void Wire()
    {
        if (confirmButton == null) return;
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);
    }

    void Open(BattleRunStats stats, Action onConfirm)
    {
        Open(stats, onConfirm, null, null);
    }

    void Open(BattleRunStats stats, Action onConfirm, Action onNextStage, string nextLabel)
    {
        BindRefs();
        Wire();
        _stats = stats ?? new BattleRunStats();
        _onConfirm = onConfirm;
        _onNextStage = onNextStage;
        Time.timeScale = 0f;

        if (titleText != null)
            titleText.text = "战斗结算";

        if (subtitleText != null)
        {
            string kind = _stats.IsDeath ? "阵亡"
                : _stats.IsEvacFailed ? "撤离失败"
                : _stats.IsVictory ? "通关"
                : "撤离成功";
            subtitleText.text = string.IsNullOrEmpty(_stats.StageTitle)
                ? kind
                : _stats.StageTitle + " · " + kind;
        }

        ApplyPortraitAndName();
        ApplyStatValues();
        PopulateRewards();

        // 【2026-10-22 主人拍板】双钮：下一关模式确认钮文案 = nextLabel（默认「下一关」）；
        // 死亡/撤离（onNextStage=null）文案回原「确定」，且不克隆返回钮。
        if (this.confirmLabel != null)
            this.confirmLabel.text = _onNextStage != null ? (string.IsNullOrEmpty(nextLabel) ? "下一关" : nextLabel) : "确定";

        SetupBackButton();

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.BattleSettlement);

        FitPanelUniformScale();

        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        // MVP 台词气泡要在 root 激活、Canvas 布局生效后再定位（要读 PortraitHost 的世界角点）
        ShowMvpSpeech();
        GameFonts.ApplyToHierarchy(transform);
    }

    // 【2026-10-28 16:00 主人拍板（覆盖当天中午「全屏拉伸」口径）】
    // 「弹窗要整体缩放、等比例缩放、不要拉伸；立绘要跟着弹窗一起缩」：
    // Panel 回预制体设计尺寸（中心锚 680×1100，备份 .workbuddy/backup/20261028-settlement-fullscreen/），
    // 再用 localScale 整体等比放大到设计分辨率内最大（720/680 ≈ 1.059 → 宽铺满、上下各留 ~58px）。
    // 只动 Panel 一层的 localScale —— 立绘/统计/气泡/按钮全是它的子节点，跟着一起缩，零拉伸。
    const float PanelDesignW = 680f;
    const float PanelDesignH = 1100f;

    /// <summary>
    /// 按设计分辨率整体等比缩放 Panel，只改 localScale，不改预制体布局坐标。
    /// </summary>
    void FitPanelUniformScale()
    {
        if (panel == null && root != null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (panel == null) return;

        var rt = panel.rectTransform;
        if (rt == null) return;

        // 中午误改的全屏拉伸 Panel（锚 (0,0)~(1,1)）在此归一回设计尺寸（运行时补节点口径，
        // 编辑器开着不能动 prefab 文件；预制体以后改回定尺寸后本分支自然休眠）。
        if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PanelDesignW, PanelDesignH);
        }

        float designW = Mathf.Max(1f, GameConfig.DESIGN_WIDTH);
        float designH = Mathf.Max(1f, GameConfig.DESIGN_HEIGHT);

        // 以预制体设计尺寸为准（scale=1 时的 sizeDelta / rect）
        Vector2 designSize = rt.sizeDelta;
        if (designSize.x < 1f || designSize.y < 1f)
        {
            var r = rt.rect;
            designSize = new Vector2(Mathf.Abs(r.width), Mathf.Abs(r.height));
        }
        if (designSize.x < 1f || designSize.y < 1f) return;

        // 放到最大：取宽高比的小者整体等比缩（不再按旧口径四周留 40 收缩，也允许 >1 放大）
        float sx = designW / designSize.x;
        float sy = designH / designSize.y;
        float s = Mathf.Min(sx, sy);
        if (s < 0.05f) s = 0.05f;
        rt.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>
    /// PortraitHost 可直接挂 Image，或 Host 作 Mask、子节点才是立绘图。
    /// 不关 Mask；优先绑名为 Portrait / PortraitImage 的子 Image。
    /// </summary>
    static Image ResolvePortraitImage(Transform root)
    {
        var host = FindDeep(root, "PortraitHost");
        if (host == null) return null;

        Transform named = host.Find("Portrait") ?? host.Find("PortraitImage");
        if (named != null)
        {
            var namedImg = named.GetComponent<Image>();
            if (namedImg != null) return namedImg;
        }

        for (int i = 0; i < host.childCount; i++)
        {
            var child = host.GetChild(i);
            if (child == null) continue;
            var img = child.GetComponent<Image>();
            if (img != null) return img;
        }

        return host.GetComponent<Image>();
    }

    static readonly string[] PlayerMvpLines =
    {
        "本场最佳：还是我。",
        "本场最佳：裂缝里就认这个。",
        "本场最佳：记一笔，回酒馆吹。"
    };

    /// <summary>MVP 台词文案源：玩家 / 佣兵各一套口径，与气泡展示共用这一个出口。</summary>
    string ResolveMvpLine()
    {
        if (_stats == null) return null;
        string mvpKey = string.IsNullOrEmpty(_stats.MvpKey)
            ? BattleRunStats.PlayerMvpKey
            : _stats.MvpKey;
        if (mvpKey == BattleRunStats.PlayerMvpKey)
            return PlayerMvpLines[Mathf.Abs(_stats.Chapter) % PlayerMvpLines.Length];
        return MercLineTable.Pick(mvpKey, MercLineTable.Scene.Mvp);
    }

    /// <summary>
    /// 【2026-10-14 主人拍板】MVP 台词改用主人在结算预制体里摆好的 <c>duihuakuang</c> 节点（主人原话
    /// 「我在结算界面加了个duihuakuang节点 这个就是对话框」）——位置/底图/字号全以预制体为准，代码只填字。
    /// 弹出结算界面后出现 MVP 说的话，**4 秒后消失**。旧的「运行时建 MvpSpeech 挂立绘右上」整段废弃删除
    ///（那就是主人报的「对话框位置不对」——压在战斗统计上）。
    /// </summary>
    const float MvpSpeechVisibleSec = 4f;
    Coroutine _mvpSpeechCo;

    void ShowMvpSpeech()
    {
        string line = ResolveMvpLine();
        if (string.IsNullOrEmpty(line))
        {
            EnsureMvpSpeechNode();
            if (mvpSpeechRt != null) mvpSpeechRt.gameObject.SetActive(false);   // 没台词不露空框
            return;
        }

        EnsureMvpSpeechNode();
        if (mvpSpeechRt == null || mvpSpeechText == null)
        {
            // 不静默：预制体里找不到 duihuakuang 要让主人看见
            Debug.LogError("[BattleSettlement] 预制体里找不到 duihuakuang 节点，MVP 台词不显示");
            return;
        }

        mvpSpeechRt.gameObject.SetActive(true);
        mvpSpeechText.text = line;

        // 4 秒后消失（重复调用先停旧协程，不会提前熄灭）
        if (_mvpSpeechCo != null) StopCoroutine(_mvpSpeechCo);
        _mvpSpeechCo = StartCoroutine(CoHideMvpSpeech());
    }

    IEnumerator CoHideMvpSpeech()
    {
        yield return new WaitForSecondsRealtime(MvpSpeechVisibleSec);
        if (mvpSpeechRt != null) mvpSpeechRt.gameObject.SetActive(false);
        _mvpSpeechCo = null;
    }

    /// <summary>找主人预制体里摆好的 duihuakuang（框图 + 子 Text）；找不到报错，不运行时造节点。</summary>
    void EnsureMvpSpeechNode()
    {
        if (mvpSpeechRt != null) return;
        Transform panelTf = panel != null ? panel.transform
            : (root != null ? FindDeep(root.transform, "Panel") : null);
        if (panelTf == null) return;

        var node = FindDeep(panelTf, "duihuakuang");
        if (node == null) return;

        mvpSpeechRt = node as RectTransform;
        mvpSpeechText = node.Find("Text")?.GetComponent<Text>();
    }

    void ApplyPortraitAndName()
    {
        if (_stats == null) return;
        string display = !string.IsNullOrEmpty(_stats.MvpDisplayName)
            ? _stats.MvpDisplayName
            : "冒险者";
        // 【2026-10-18 主人拍板】MVP 铭牌只显示名字，不再拼「地图名 · 名字」前缀
        if (nameText != null)
            nameText.text = display;

        if (portraitImage == null)
            portraitImage = ResolvePortraitImage(root != null ? root.transform : transform);
        if (portraitImage == null) return;

        Sprite sp = null;
        string mvpKey = string.IsNullOrEmpty(_stats.MvpKey)
            ? BattleRunStats.PlayerMvpKey
            : _stats.MvpKey;
        if (mvpKey == BattleRunStats.PlayerMvpKey)
            sp = StoryPortraits.Get(StoryPortraits.Player);
        else
            sp = MercPortraitSprites.GetStand(mvpKey);

        if (sp == null)
            sp = StoryPortraits.Get(StoryPortraits.Player);

        if (sp != null)
        {
            portraitImage.sprite = sp;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            portraitImage.enabled = true;
            portraitImage.gameObject.SetActive(true);
        }
        else
            portraitImage.enabled = false;
    }

    void ApplyStatValues()
    {
        SetValue(valueDamage, FormatInt(_stats.DamageDealt));
        SetValue(valueKill, _stats.KillCount.ToString());
        SetValue(valueCrit, _stats.CritCount.ToString());
        SetValue(valueCombo, _stats.MaxKillCombo.ToString());
        SetValue(valueTaken, FormatInt(_stats.DamageTaken));
        SetValue(valueHeal, FormatInt(_stats.HealingReceived));
    }

    static string FormatInt(float v) => Mathf.RoundToInt(v).ToString("N0");

    static void SetValue(Text t, string v)
    {
        if (t != null) t.text = v ?? "0";
    }

    void PopulateRewards()
    {
        ClearSpawnedCells();
        if (rewardsGrid == null) return;

        var cells = BuildRewardCells(_stats);
        int n = Mathf.Min(cells.Count, MaxRewardCells);

        // 没有任何奖励时整块隐藏（标题 + 网格），不留下空区域
        if (rewardsHeader == null && root != null)
        {
            var headerNode = FindDeep(root.transform, "RewardsHeader");
            rewardsHeader = headerNode != null ? headerNode.gameObject : null;
        }
        bool any = n > 0;
        rewardsGrid.gameObject.SetActive(any);
        if (rewardsHeader != null) rewardsHeader.SetActive(any);
        if (!any) return;

        for (int i = 0; i < n; i++)
            SpawnRewardCell(cells[i]);
        if (cells.Count > MaxRewardCells)
            SpawnRewardCell(new SettlementRewardCell
            {
                label = "更多",
                count = cells.Count - MaxRewardCells,
                frameColor = new Color(0.4f, 0.35f, 0.45f, 1f)
            });
    }

    /// <summary>
    /// 结算奖励格数据口径（唯一出口）：金币/天赋石/附魔石/强化材料/装备。
    /// 【2026-10-25 主人拍板】战斗结果界面胜利奖励区复用同一口径（private→public），禁再开第二条数据链。
    /// </summary>
    public static List<SettlementRewardCell> BuildRewardCells(BattleRunStats s)
    {
        var list = new List<SettlementRewardCell>();
        if (s == null) return list;

        // 2026-10-09 主人拍板：结算物品图标改走项目现有口径 CommonIconSprites（Resources + 编辑器回退）。
        // 旧写法 LoadUiIcon 只读 Resources/UI/Settlement/<key>，该目录不存在且无回退 → 每个物品格子都空图标。
        // 【2026-10-20 主人拍板「没有获得金币就不要显示了」】金币 >=2 才上格：
        // 数量规则是 >1 才显数字，金币=1 时格子只有「金币」两字、数字空白，看着像没给钱；
        // =0 原本就不显示。1 个金币视为没获得，干脆不上格。
        if (s.GoldGained >= 2)   // 【2026-10-18】去掉 !s.IsDeath：死亡也照常显示本局赚到的钱（FillSettlementSnapshot 同日拍板口径，原 Gate 是残留）
            // 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】结算「金币」= GoldEarned
            //（顶部 HUD 同款毛额），与写回城镇的数额同账 → 叫「金币」配 icon_gold；
            // 抽奖币池带不出本局，不上结算。
            list.Add(MakeCell("金币", (int)s.GoldGained, CommonIconSprites.Load("icon_gold"), new Color(0.85f, 0.7f, 0.25f)));
        if (s.TalentGained > 0)
            list.Add(MakeCell("天赋石", s.TalentGained, CommonIconSprites.Load("icon_talent_stone"), new Color(0.55f, 0.4f, 0.75f)));
        if (s.EnchantStoneDelta > 0)
            list.Add(MakeCell("附魔石", s.EnchantStoneDelta, CommonIconSprites.Load("icon_enchant_stone"), new Color(0.45f, 0.55f, 0.85f)));
        if (s.DecomposeMatDelta > 0)
            list.Add(MakeCell("强化材料", s.DecomposeMatDelta, CommonIconSprites.Load("icon_decompose_mat"), new Color(0.5f, 0.55f, 0.45f)));

        // 本局实际获得的装备（弹出「恭喜获得」时记录），不再倒出整个背包
        if (s.EquipGains != null)
        {
            for (int i = 0; i < s.EquipGains.Count; i++)
            {
                var g = s.EquipGains[i];
                if (g == null) continue;
                list.Add(new SettlementRewardCell
                {
                    label = string.IsNullOrEmpty(g.name) ? "装备" : g.name,
                    count = 1,
                    icon = g.icon,
                    frameColor = RarityFrameColor(g.rarity)
                });
            }
        }

        return list;
    }

    static SettlementRewardCell MakeCell(string label, int count, Sprite icon, Color frame)
    {
        return new SettlementRewardCell
        {
            label = label,
            count = count,
            icon = icon,
            frameColor = frame
        };
    }

    static Color RarityFrameColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return new Color(0.85f, 0.55f, 0.15f);
            case Rarity.Epic: return new Color(0.65f, 0.35f, 0.85f);
            case Rarity.Rare: return new Color(0.3f, 0.5f, 0.9f);
            case Rarity.Uncommon: return new Color(0.3f, 0.7f, 0.35f);
            default: return new Color(0.55f, 0.55f, 0.55f);
        }
    }

    // 2026-10-09 主人拍板：删除 LoadUiIcon —— 它只读 Resources/UI/Settlement/<key>（目录不存在、无回退），
    // 是结算物品格子空图标的根因。物品图标统一走 CommonIconSprites（唯一出口）。

    void SpawnRewardCell(SettlementRewardCell data)
    {
        GameObject go;
        if (rewardCellPrefab != null)
        {
            go = Instantiate(rewardCellPrefab, rewardsGrid, false);
            go.name = "RewardCell";
            go.SetActive(true);
        }
        else
        {
            go = CreateRewardCellRuntime(rewardsGrid, data);
        }
        _spawnedCells.Add(go);

        var frame = go.GetComponent<Image>();
        if (frame != null) frame.color = data.frameColor;

        var iconTf = FindDeep(go.transform, "Icon");
        var icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
        if (icon != null)
        {
            if (data.icon != null)
            {
                icon.sprite = data.icon;
                icon.color = Color.white;
                icon.enabled = true;
            }
            else
            {
                // 2026-10-09 主人拍板：缺图标不要静默留灰块，报出来让主人看见（不自己造图）
                Debug.LogError($"[BattleSettlement] 奖励格「{data.label}」缺少图标，请检查资源/取图标口径");
                icon.color = new Color(0.3f, 0.28f, 0.35f, 1f);
            }
            icon.preserveAspect = true;
        }

        var countTf = FindDeep(go.transform, "Count");
        var count = countTf != null ? countTf.GetComponent<Text>() : null;
        if (count != null)
        {
            count.text = data.count > 1 ? data.count.ToString() : "";
            // 【2026-10-15 主人拍板】数量位置以**预制体**为准（铁律 9）：RewardCellTemplate/Count
            // 本就锚在格子右下角（锚(1,0)/pos(-4,-2.1)/pivot(1,0)），运行时候补路径（CreateRewardCellRuntime）
            // 也是右下角。2026-10-14 那段「Count 强制 reparent 到 Icon 下方居中」的运行时覆盖已按主人
            // 指示删除（实测还是不对）—— 代码不再动 Count 的父子/锚点/对齐，预制体摆哪就哪。
        }

        // 【2026-10-12 主人拍板】物品格子下面加名字（预制体/运行时两种 cell 结构都有 Name 节点）
        var nameTf = FindDeep(go.transform, "Name");
        var nameText = nameTf != null ? nameTf.GetComponent<Text>() : null;
        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(data.label) ? "" : data.label;
    }

    void ClearSpawnedCells()
    {
        for (int i = 0; i < _spawnedCells.Count; i++)
        {
            if (_spawnedCells[i] != null)
                Destroy(_spawnedCells[i]);
        }
        _spawnedCells.Clear();
    }

    void OnConfirm()
    {
        // 【2026-10-22 主人拍板】双钮模式：确认钮 =【下一关】（_onNextStage）；
        // 单确认钮（死亡/撤离，_onNextStage=null）= 原 _onConfirm，行为不变。
        var cb = _onNextStage != null ? _onNextStage : _onConfirm;
        ClosePanel(cb);
    }

    /// <summary>【2026-10-22 主人拍板】返回钮点击 = onConfirm 原行为（回城 EndRunAndReturnToTown，带本局收益）。</summary>
    void OnBackClicked()
    {
        var cb = _onConfirm;
        ClosePanel(cb);
    }

    void ClosePanel(Action cb)
    {
        ClearSpawnedCells();
        if (_backButtonClone != null)
        {
            Destroy(_backButtonClone);
            _backButtonClone = null;
        }
        if (root != null) root.SetActive(false);
        Time.timeScale = 1f;
        _onConfirm = null;
        _onNextStage = null;
        cb?.Invoke();
    }

    /// <summary>返回钮与确认钮的间距（本地单位，可调）。</summary>
    const float BackButtonGap = 16f;

    /// <summary>
    /// 【2026-10-22 主人拍板】返回钮：运行时克隆 confirmButton 的 GameObject（prefab 零改动），
    /// 文案改「返回」，放确认钮左侧（anchoredPosition 向左偏移按钮宽度 + 间距）。
    /// 每次 Show 先清旧克隆（防叠加）；onNextStage=null（死亡/撤离）不克隆。
    /// </summary>
    void SetupBackButton()
    {
        if (_backButtonClone != null)
        {
            Destroy(_backButtonClone);
            _backButtonClone = null;
        }
        if (_onNextStage == null || confirmButton == null) return;

        var srcRt = confirmButton.transform as RectTransform;
        var clone = Instantiate(confirmButton.gameObject, confirmButton.transform.parent);
        clone.name = "BackButton";
        var btn = clone.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnBackClicked);
        }
        var cloneLabel = clone.GetComponentInChildren<Text>(true);
        if (cloneLabel != null) cloneLabel.text = "返回";
        var rt = clone.transform as RectTransform;
        if (rt != null && srcRt != null)
        {
            // 锚点/轴心/尺寸照抄确认钮，只平移位置
            rt.anchorMin = srcRt.anchorMin;
            rt.anchorMax = srcRt.anchorMax;
            rt.pivot = srcRt.pivot;
            rt.sizeDelta = srcRt.sizeDelta;
            float w = srcRt.rect.width > 0.01f ? srcRt.rect.width : srcRt.sizeDelta.x;
            rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(-(w + BackButtonGap), 0f);
        }
        clone.SetActive(true);
        _backButtonClone = clone;
        GameFonts.ApplyToHierarchy(clone.transform);
    }

    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleSettlement);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        var ui = host.GetComponent<BattleSettlementUI>() ?? host.AddComponent<BattleSettlementUI>();

        var root = Mk(host.transform, "Root");
        Stretch(root);

        var dim = Mk(root, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var panel = Mk(root, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(680f, 1100f);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);
        var bgSp = LoadSettlementArt("bg");
        if (bgSp != null)
        {
            panelImg.sprite = bgSp;
            panelImg.color = Color.white;
            panelImg.type = Image.Type.Sliced;
        }

        Label(panel, "Title", "战斗结算", 34, new Vector2(0f, -28f), new Vector2(400f, 48f));
        Label(panel, "Subtitle", "通关", 20, new Vector2(0f, -72f), new Vector2(500f, 32f));

        var portrait = Mk(panel, "PortraitHost");
        portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 1f);
        portrait.pivot = new Vector2(0f, 1f);
        portrait.anchoredPosition = new Vector2(28f, -110f);
        portrait.sizeDelta = new Vector2(240f, 420f);
        var pImg = portrait.gameObject.AddComponent<Image>();
        pImg.color = new Color(0.2f, 0.18f, 0.24f, 1f);
        pImg.preserveAspect = true;

        var namePlate = Mk(panel, "NamePlate");
        namePlate.anchorMin = namePlate.anchorMax = new Vector2(0f, 1f);
        namePlate.pivot = new Vector2(0f, 1f);
        namePlate.anchoredPosition = new Vector2(28f, -540f);
        namePlate.sizeDelta = new Vector2(240f, 40f);
        namePlate.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.9f);
        Label(namePlate, "NameText", "Lv.1 冒险者", 22, Vector2.zero, new Vector2(240f, 40f), TextAnchor.MiddleCenter, true);

        var stats = Mk(panel, "StatsPanel");
        stats.anchorMin = stats.anchorMax = new Vector2(1f, 1f);
        stats.pivot = new Vector2(1f, 1f);
        stats.anchoredPosition = new Vector2(-24f, -110f);
        stats.sizeDelta = new Vector2(360f, 420f);

        Label(stats, "StatsHeader", "战斗统计", 24, new Vector2(0f, 0f), new Vector2(340f, 36f));

        float y = -44f;
        CreateStatRow(stats, "StatRow_Damage", "总伤害", "总伤害", ref y);
        CreateStatRow(stats, "StatRow_Kill", "击杀敌人", "击杀敌人", ref y);
        CreateStatRow(stats, "StatRow_Crit", "暴击次数", "暴击次数", ref y);
        CreateStatRow(stats, "StatRow_Combo", "最大连击", "最大连击数", ref y);
        CreateStatRow(stats, "StatRow_Taken", "承受伤害", "承受伤害", ref y);
        CreateStatRow(stats, "StatRow_Heal", "受到治疗", "受到治疗", ref y);

        Label(panel, "RewardsHeader", "获得奖励", 24, new Vector2(0f, -560f), new Vector2(600f, 36f));

        var grid = Mk(panel, "RewardsGrid");
        grid.anchorMin = new Vector2(0.5f, 0f);
        grid.anchorMax = new Vector2(0.5f, 0f);
        grid.pivot = new Vector2(0.5f, 0f);
        grid.anchoredPosition = new Vector2(0f, 120f);
        grid.sizeDelta = new Vector2(620f, 290f);
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        // 【2026-10-12 主人拍板】图标 ×1.3 + 底部名字 → cell 88×88 → 114×130；
        // 620 宽 5 列（5×114 + 4×10 = 610）放得下，列数 6→5。
        gl.cellSize = new Vector2(114f, 130f);
        gl.spacing = new Vector2(10f, 10f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 5;
        gl.childAlignment = TextAnchor.UpperLeft;   // 2026-10-09 主人拍板：物品从左往右排，第一个靠左而非居中

        var tmpl = CreateRewardCellRuntime(grid, new SettlementRewardCell { count = 0 });
        tmpl.name = "RewardCellTemplate";
        tmpl.SetActive(false);
        ui.rewardCellPrefab = tmpl;

        var btn = Mk(panel, "ConfirmButton");
        btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
        btn.pivot = new Vector2(0.5f, 0f);
        btn.anchoredPosition = new Vector2(0f, 28f);
        btn.sizeDelta = new Vector2(320f, 72f);
        btn.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.48f, 1f);
        btn.gameObject.AddComponent<Button>().transition = Selectable.Transition.ColorTint;
        Label(btn, "Label", "确定", 28, Vector2.zero, new Vector2(320f, 72f), TextAnchor.MiddleCenter, true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static void CreateStatRow(Transform parent, string rowName, string label, string iconFile, ref float y)
    {
        var row = Mk(parent, rowName);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, y);
        row.sizeDelta = new Vector2(340f, 52f);

        var icon = Mk(row, "Icon");
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(4f, 0f);
        icon.sizeDelta = new Vector2(40f, 40f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        var sp = LoadSettlementArt(iconFile);
        if (sp != null)
        {
            iconImg.sprite = sp;
            iconImg.color = Color.white;
        }
        else
            iconImg.color = new Color(0.45f, 0.35f, 0.55f, 1f);

        Label(row, "Label", label, 22, new Vector2(-20f, 0f), new Vector2(160f, 40f), TextAnchor.MiddleLeft);
        var labelRt = FindDeep(row, "Label") as RectTransform;
        if (labelRt != null)
        {
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(52f, 0f);
        }

        Label(row, "Value", "0", 24, new Vector2(0f, 0f), new Vector2(120f, 40f), TextAnchor.MiddleRight);
        var valueRt = FindDeep(row, "Value") as RectTransform;
        if (valueRt != null)
        {
            valueRt.anchorMin = valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-8f, 0f);
            var vt = valueRt.GetComponent<Text>();
            if (vt != null) vt.font = GameFonts.GetNumber() ?? vt.font;
        }

        y -= 56f;
    }

    static GameObject CreateRewardCellRuntime(Transform parent, SettlementRewardCell data)
    {
        var cell = Mk(parent, "RewardCell");
        // 【2026-10-12 主人拍板】物品图标放大 30%（格宽 88→114）+ 下面加名字（格高 88→130）
        cell.sizeDelta = new Vector2(114f, 130f);
        var frame = cell.gameObject.AddComponent<Image>();
        frame.color = data != null ? data.frameColor : new Color(0.55f, 0.45f, 0.25f);

        var icon = Mk(cell, "Icon");
        Stretch(icon);
        // 图标区：左右各 10、底部留 34 给名字、顶部 10 → 94×86（原 72×64，约放大 30%）
        icon.offsetMin = new Vector2(10f, 34f);
        icon.offsetMax = new Vector2(-10f, -10f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = new Color(0.25f, 0.22f, 0.3f, 1f);

        // 【2026-10-12 主人拍板】格子底部加物品名字（金色小字、居中、单行裁切不折行）
        // 宽 76 居中 + Count 占右下 72：两块错开不叠（格子总宽 114）
        Label(cell, "Name", data != null ? data.label : "", 15,
            Vector2.zero, new Vector2(76f, 26f), TextAnchor.MiddleCenter);
        var nameRt = FindDeep(cell, "Name") as RectTransform;
        if (nameRt != null)
        {
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 4f);
        }

        Label(cell, "Count", data != null && data.count > 0 ? data.count.ToString() : "", 18,
            Vector2.zero, new Vector2(80f, 24f), TextAnchor.LowerRight);
        var countRt = FindDeep(cell, "Count") as RectTransform;
        if (countRt != null)
        {
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.anchoredPosition = new Vector2(-4f, 2f);
            countRt.sizeDelta = new Vector2(72f, 22f);
        }
        return cell.gameObject;
    }

    static Sprite LoadSettlementArt(string fileNameNoExt)
    {
#if UNITY_EDITOR
        if (DeviceParity.EditorFallbackEnabled)
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + fileNameNoExt + ".png");
#endif
        var sp = Resources.Load<Sprite>("UI/Settlement/" + fileNameNoExt);
        if (sp == null) DeviceParity.ReportMissing("UI/Settlement/" + fileNameNoExt, "结算图");
        return sp;
    }

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = Mk(parent, name);
        if (stretch)
        {
            Stretch(rt);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = new Color(0.95f, 0.85f, 0.55f, 1f);
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}
