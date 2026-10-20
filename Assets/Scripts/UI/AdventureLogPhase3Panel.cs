using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志三期运行时面板：碎片合成 / 里程商店。
/// 2026-10-19 正式化：木质描边弹窗 + 全屏遮罩 + 右上角关闭，
/// 修复旧版行数溢出面板、关闭按钮落在面板外的问题。
/// 2026-10-19 预制体化：运行时只实例化预制体并按节点名绑定，缺预制体直接报错
/// （不做代码兜底，出错要能看见）。
/// 2026-10-20 主人手工改版商品行：Row_i 下 = Icon（物品图标）/ 兑换（点击兑换的按钮）/
/// 兑换/次数（可领次数/总次数，领完或点数不足置灰），Label 只写名称+点数。
/// 2026-10-20 主人拍板：8 个 UI 预制体一键生成脚本全部删除，本预制体以磁盘手工版为唯一真源，
/// 改版直接改 Assets/Resources/Prefabs/Town/AdventureLogPhase3Panel.prefab。
/// </summary>
public class AdventureLogPhase3Panel : MonoBehaviour
{
    enum Mode { None, Craft, Shop }

    public const string PrefabResPath = "Prefabs/Town/AdventureLogPhase3Panel";

    // ===== 节点名（预制体与代码按名字对齐；改 prefab 节点名会断绑定，Console 会报缺哪个） =====
    public const string RootName = "Phase3Root";
    public const string BarName = "Phase3Bar";
    public const string DimName = "bg";   // 2026-10-20 主人手工改版：原 Phase3Dim 改名 bg
    public const string PanelName = "Phase3Panel";
    public const string InnerName = "Inner";
    public const string TitleName = "Title";
    public const string BodyName = "Body";
    public const string CraftBtnName = "Btn_Craft";
    public const string ShopBtnName = "Btn_Shop";
    public const string CloseXName = "CloseX";
    public const string IconName = "Icon";
    public const string ExchangeName = "ExchangeBtn";
    public const string CountName = "CountText";
    public const string RenwuName = "renwu";
    public static string RowName(int i) => "Row_" + i;

    AdventureLogUI _host;
    GameObject _bar;
    GameObject _dim;
    GameObject _panel;
    GameObject _renwu;   // 预制体根下的装饰人物，跟着面板一起显隐（不接管就会常驻盖住日志标签）
    Text _title;
    Text _body;
    Mode _mode;
    readonly GameObject[] _rows = new GameObject[6];
    readonly Button[] _exchangeBtns = new Button[6];   // 兑换按钮（挂在兑换节点上，点击才兑换）
    readonly Text[] _actionLabels = new Text[6];       // Label：只写名称 + 消耗点数
    readonly Text[] _countTexts = new Text[6];         // 次数：本周可领次数/总次数
    readonly Image[] _icons = new Image[6];            // Icon：物品图标
    readonly Color[] _countBaseColors = new Color[6];  // 次数文字原色（置灰后恢复用）
    RectTransform _listContent;                        // 行滑动列表的 Content（运行时动态套 ScrollRect，不动预制体）
    bool _missingLogged;

    // ===== 配色（RefreshPanel 运行时用；构建用常量已随一键生成脚本删除） =====
    static readonly Color CreamColor = new Color(0.93f, 0.87f, 0.72f, 1f);      // 正文米白
    static readonly Color PriceColorHex = new Color32(232, 200, 134, 255);      // 价格亮金
    static readonly Color DisabledColor = new Color(0.55f, 0.50f, 0.44f, 1f);   // 置灰

    // ===== 手工预制体行几何（与磁盘预制体实测对齐，勿按旧构建常量猜） =====
    // 行宽 414 / 行高 83 / 步距 70；Row_0 顶边 Inner 本地 y=-37.5，Inner 底边 -460
    const float HandRowH = 83f;
    const float HandRowStep = 70f;
    const float ListViewportTopY = -37.5f;
    const float ListViewportBottomY = -460f;
    const float ListViewportW = 424f;

    public static AdventureLogPhase3Panel Ensure(AdventureLogUI host)
    {
        if (host == null) return null;
        var existing = host.GetComponent<AdventureLogPhase3Panel>();
        if (existing != null)
        {
            existing._host = host;
            existing.EnsureUi();
            return existing;
        }
        var p = host.gameObject.AddComponent<AdventureLogPhase3Panel>();
        p._host = host;
        p.EnsureUi();
        return p;
    }

    public void SetVisibleForTab(bool worldOrAch)
    {
        EnsureUi();
        // 2026-10-20 主人拍板：底部入口条整条撤掉，里程商店唯一入口 = 侧栏左下角 Emblem。
        // 手工改版预制体已物理删除该节点，代码仅在节点存在时兜底隐藏（兼容旧版预制体）。
        if (_bar != null) _bar.SetActive(false);
        if (!worldOrAch && _panel != null)
        {
            _mode = Mode.None;
            _panel.SetActive(false);
            if (_dim != null) _dim.SetActive(false);
            if (_renwu != null) _renwu.SetActive(false);
        }
    }

    /// <summary>运行时：只从预制体实例化，不代码构建（唯一路径）。</summary>
    void EnsureUi()
    {
        if (_panel != null) return;   // 已实例化过（Phase3Bar 可选可空，不能用它做构建标记）
        var prefab = Resources.Load<GameObject>(PrefabResPath);
        if (prefab == null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                Debug.LogError($"[Phase3Panel] 缺少预制体 Assets/Resources/{PrefabResPath}.prefab —— 请执行 Tools → UI → 生成三期面板预制体");
            }
            return;
        }
        var canvas = _host != null ? _host.transform : transform;
        var root = Instantiate(prefab, canvas, false);
        root.name = RootName;
        // 统一画布设置：Screen Space - Camera + UI 相机 + 720×1280 缩放 + GraphicRaycaster，
        // 嵌套 Canvas overrideSorting 抬到城镇弹窗档（与商店/酒馆弹窗同层）
        var rootCanvas = root.GetComponent<Canvas>();
        if (rootCanvas != null)
            UICanvasSetup.ApplyPopup(rootCanvas, GameConfig.UiSort.TownPopup);
        else
            Debug.LogError("[Phase3Panel] 预制体根缺 Canvas 组件，请重新生成预制体");
        WireRefs(root.transform);
        if (_bar != null) _bar.SetActive(false);
        _dim.SetActive(false);
        _panel.SetActive(false);
        if (_renwu != null) _renwu.SetActive(false);
    }

    void WireRefs(Transform root)
    {
        // 2026-10-20 手工改版预制体已把底部入口条 Phase3Bar（含 Btn_Craft/Btn_Shop）整条删掉，
        // 里程商店唯一入口 = 侧栏左下角 Emblem。这里改为可选查找，缺节点不再报错。
        _bar = root.Find(BarName)?.gameObject;
        _dim = FindReq(root, DimName);
        _panel = FindReq(root, PanelName);
        _renwu = root.Find(RenwuName)?.gameObject;   // 装饰人物，可选节点
        if (_dim == null || _panel == null) return;

        if (_bar != null)
        {
            BindButton(_bar.transform, CraftBtnName, OpenCraft);
            BindButton(_bar.transform, ShopBtnName, OpenShop);
            // 2026-10-20 主人拍板：碎片合成入口先撤，只留里程商店（Emblem 徽记另接商店入口）。
            var craftBtn = _bar.transform.Find(CraftBtnName);
            if (craftBtn != null)
                craftBtn.gameObject.SetActive(false);
        }

        var dimBtn = _dim.GetComponent<Button>();
        if (dimBtn != null)
        {
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(ClosePanel);
        }

        var inner = _panel.transform.Find(InnerName);
        _title = inner != null ? inner.Find(TitleName)?.GetComponent<Text>() : null;
        _body = inner != null ? inner.Find(BodyName)?.GetComponent<Text>() : null;
        if (_title == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{TitleName}（Text）");
        if (_body == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{BodyName}（Text）");

        for (int i = 0; i < 6; i++)
        {
            var row = inner != null ? inner.Find(RowName(i)) : null;
            if (row == null)
            {
                Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{RowName(i)}");
                continue;
            }
            _rows[i] = row.gameObject;
            _actionLabels[i] = row.Find("Label")?.GetComponent<Text>();
            _icons[i] = row.Find(IconName)?.GetComponent<Image>();
            if (_actionLabels[i] == null)
                Debug.LogError($"[Phase3Panel] 节点 {RowName(i)} 缺子节点 Label（Text）");

            // 兑换节点：点击兑换的按钮（预制体若没挂 Button，运行时补一个并指向自身底图）
            var ex = row.Find(ExchangeName);
            if (ex == null)
            {
                Debug.LogError($"[Phase3Panel] 节点 {RowName(i)} 缺子节点 {ExchangeName}");
                continue;
            }
            var exImg = ex.GetComponent<Image>();
            if (exImg != null) exImg.raycastTarget = true;
            var exBtn = ex.GetComponent<Button>();
            if (exBtn == null)
            {
                exBtn = ex.gameObject.AddComponent<Button>();
                exBtn.targetGraphic = exImg;
            }
            exBtn.onClick.RemoveAllListeners();
            _exchangeBtns[i] = exBtn;

            var cnt = ex.Find(CountName);
            _countTexts[i] = cnt != null ? cnt.GetComponent<Text>() : null;
            if (_countTexts[i] != null)
                _countBaseColors[i] = _countTexts[i].color;
            else
                Debug.LogError($"[Phase3Panel] 节点 {RowName(i)}/{ExchangeName} 缺子节点 {CountName}（Text）");

            // 行根旧 Button 不再承担兑换（兑换只在兑换节点上），清掉残留监听防误触
            var rowBtn = row.GetComponent<Button>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();
        }

        BindButton(inner, CloseXName, ClosePanel);
        BuildRowScroll(inner);
        GameFonts.ApplyToHierarchy(root);
        ForcePanelOpaque();   // 2026-10-20 主人拍板：打开必须全不透明，实例化时先拉一遍
    }

    /// <summary>
    /// 2026-10-20 主人拍板：里程商店打开必须全部实色显示，不要半透效果（以预制体效果为准）。
    /// 把 Phase3Panel 子树所有 Graphic（Image/Text）的 alpha 强制拉满；
    /// Phase3Dim 是背景遮罩、不在 _panel 子树内，不受影响（遮罩半透是设计）。
    /// 实例化和每次打开各拉一遍，防止任何环节把 alpha 压下去。
    /// </summary>
    void ForcePanelOpaque()
    {
        if (_panel == null) return;
        var graphics = _panel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            var c = g.color;
            if (c.a < 0.999f)
                g.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    /// <summary>
    /// 行滑动列表（2026-10-20 主人拍板）：运行时给 6 条商品行套 ScrollRect，不动手工预制体。
    /// Viewport 覆盖行区（Body 下沿到 Inner 底边），Content 顶对齐、行按预制体原步距 70 排布；
    /// 行从 Inner 直挂改挂 Content，屏幕位置与原手工布局完全一致。
    /// </summary>
    void BuildRowScroll(Transform inner)
    {
        if (_listContent != null || inner == null) return;

        var scroll = new GameObject("RowScroll", typeof(RectTransform), typeof(ScrollRect));
        scroll.transform.SetParent(inner, false);
        var srt = scroll.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, (ListViewportTopY + ListViewportBottomY) / 2f);
        srt.sizeDelta = new Vector2(ListViewportW, ListViewportTopY - ListViewportBottomY);

        var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        vp.transform.SetParent(srt, false);
        var vrt = vp.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vrt, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, srt.sizeDelta.y);

        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = crt;
        sr.viewport = vrt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 15f;

        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] == null) continue;
            var rt = _rows[i].GetComponent<RectTransform>();
            rt.SetParent(crt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(HandRowH / 2f + i * HandRowStep));
        }
        _listContent = crt;

        // RowScroll 成为 Inner 最后一个子节点后，行渲染在 CloseX 之上会盖住重叠角 → 关闭钮置顶
        var closeX = inner.Find(CloseXName);
        if (closeX != null) closeX.SetAsLastSibling();
    }

    /// <summary>Content 高度随显示行数伸缩：不足一屏不滚，超出一屏可滚。</summary>
    void UpdateContentHeight(int shownRows)
    {
        if (_listContent == null) return;
        float vpH = ListViewportTopY - ListViewportBottomY;
        float need = (Mathf.Max(1, shownRows) - 1) * HandRowStep + HandRowH;
        _listContent.sizeDelta = new Vector2(0f, Mathf.Max(vpH, need));
    }

    static GameObject FindReq(Transform root, string name)
    {
        var t = root.Find(name);
        if (t == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {name}");
        return t != null ? t.gameObject : null;
    }

    static void BindButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        if (parent == null) return;
        var t = parent.Find(name);
        if (t == null)
        {
            Debug.LogError($"[Phase3Panel] 预制体缺按钮 {parent.name}/{name}");
            return;
        }
        var btn = t.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError($"[Phase3Panel] 节点 {name} 缺 Button 组件");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
    }

    void OpenCraft()
    {
        _mode = Mode.Craft;
        RefreshPanel();
    }

    void OpenShop()
    {
        OpenShopPanel();
    }

    /// <summary>外部入口（侧栏 Emblem 徽记）直接打开里程商店。</summary>
    public void OpenShopPanel()
    {
        _mode = Mode.Shop;
        AdventureLogMileageShop.EnsureWeek();
        RefreshPanel();
    }

    void ClosePanel()
    {
        _mode = Mode.None;
        if (_panel != null) _panel.SetActive(false);
        if (_dim != null) _dim.SetActive(false);
        if (_renwu != null) _renwu.SetActive(false);
    }

    void RefreshPanel()
    {
        EnsureUi();
        if (_panel == null) return;
        if (_mode == Mode.None)
        {
            _panel.SetActive(false);
            _dim.SetActive(false);
            return;
        }
        if (_dim != null)
        {
            _dim.SetActive(true);
            // 层级以预制体为准，不再把 bg 提到最末（否则会压到 renwu 装饰人物之上）；
            // 整个 Phase3Root 在下方已 SetAsLastSibling 置顶，遮罩效果不受影响
        }
        _panel.SetActive(true);
        if (_renwu != null) _renwu.SetActive(true);
        _panel.transform.SetAsLastSibling();
        // 2026-10-20 主人拍板：每次打开都强制全不透明 + 弹窗根提到日志内容之上
        ForcePanelOpaque();
        (_panel.transform.parent as RectTransform)?.SetAsLastSibling();

        if (_mode == Mode.Craft)
        {
            if (_title != null) _title.text = "碎片合成";
            if (_body != null) _body.text = AdventureLogFragments.FormatInventory();
            for (int i = 0; i < 6; i++)
            {
                if (_rows[i] == null) continue;
                bool on = i < AdventureLogFragments.Recipes.Length;
                _rows[i].SetActive(on);
                if (!on) continue;
                int idx = i;
                bool can = AdventureLogFragments.CanCraft(idx);
                if (_actionLabels[i] != null)
                {
                    _actionLabels[i].text = AdventureLogFragments.FormatRecipeLine(idx);
                    _actionLabels[i].color = can ? CreamColor : DisabledColor;
                }
                if (_countTexts[i] != null)
                {
                    _countTexts[i].text = "";
                    _countTexts[i].color = _countBaseColors[i];
                }
                if (_icons[i] != null) _icons[i].sprite = LoadResIcon("Icons/Item/icon_log_fragment");
                if (_exchangeBtns[i] != null)
                {
                    _exchangeBtns[i].interactable = can;
                    _exchangeBtns[i].onClick.RemoveAllListeners();
                    _exchangeBtns[i].onClick.AddListener(() =>
                    {
                        if (AdventureLogFragments.TryCraft(idx, out string msg))
                            UIManager.Instance?.ShowToast(msg);
                        else
                            UIManager.Instance?.ShowToast(msg ?? "无法合成");
                        RefreshPanel();
                        _host?.RefreshAfterPhase3();
                    });
                }
            }
            UpdateContentHeight(AdventureLogFragments.Recipes.Length);
        }
        else
        {
            if (_title != null) _title.text = "里程商店";
            if (_body != null)
            {
                _body.text = AdventureLogMileage.FormatStatusLine() + "\n本周：" + AdventureLogMileageShop.CurrentWeekKey()
                             + $"\n卷轴库存 普通{SaveSystem.Instance?.Data?.mercScrollCommon ?? 0}"
                             + $"/高级{SaveSystem.Instance?.Data?.mercScrollLegendary ?? 0}";
            }

            // 2026-10-20 主人拍板：稀有·暮火之杖（里程 Lv2 等级奖励）放第一；
            // 已领取的杖 / 本周兑完的商品沉到列表底部，未领完的保持原序在上。
            var normal = new List<ShopEntry>();
            var sunk = new List<ShopEntry>();
            var staff = new ShopEntry { IsStaff = true };
            (AdventureLogMileage.IsLevelClaimed(2) ? sunk : normal).Add(staff);
            for (int i = 0; i < AdventureLogMileageShop.Items.Length; i++)
            {
                var e = new ShopEntry { IsStaff = false, ItemIndex = i };
                bool done = AdventureLogMileageShop.RemainThisWeek(AdventureLogMileageShop.Items[i]) <= 0;
                (done ? sunk : normal).Add(e);
            }

            int row = 0;
            foreach (var e in normal) { FillShopRow(row, e); row++; }
            foreach (var e in sunk) { FillShopRow(row, e); row++; }
            for (; row < 6; row++)
            {
                if (_rows[row] != null) _rows[row].SetActive(false);
            }
            UpdateContentHeight(row);
        }
    }

    /// <summary>商店条目：IsStaff=里程 Lv2 等级奖励（暮火之杖），否则为商店商品（ItemIndex）。</summary>
    struct ShopEntry
    {
        public bool IsStaff;
        public int ItemIndex;
    }

    /// <summary>填充第 rowIdx 行（杖条目或商品条目），行必激活。</summary>
    void FillShopRow(int rowIdx, ShopEntry e)
    {
        if (rowIdx < 0 || rowIdx >= 6 || _rows[rowIdx] == null) return;
        _rows[rowIdx].SetActive(true);

        if (e.IsStaff)
        {
            bool claimed = AdventureLogMileage.IsLevelClaimed(2);
            bool can = AdventureLogMileage.CanClaimLevel(2);
            string priceTag = $"<color=#{ColorUtility.ToHtmlStringRGB(PriceColorHex)}>Lv2里程奖励</color>";

            if (_actionLabels[rowIdx] != null)
                _actionLabels[rowIdx].text = $"{SpecialWeapons.InstanceName}　{priceTag}";

            if (_countTexts[rowIdx] != null)
            {
                _countTexts[rowIdx].text = claimed ? "已领取"
                    : can ? "可领取"
                    : $"需{AdventureLogMileage.LevelThresholds[1]}点";
                _countTexts[rowIdx].color = can ? _countBaseColors[rowIdx] : DisabledColor;
            }

            if (_icons[rowIdx] != null)
            {
                var tpl = SpecialWeapons.EnsureTwilightTemplate();
                var sp = tpl != null ? tpl.icon : null;
                if (sp == null) Debug.LogError("[Phase3Panel] 暮火之杖图标缺失（iconFileName=New_Weapon_03）");
                _icons[rowIdx].sprite = sp;
            }

            if (_exchangeBtns[rowIdx] != null)
            {
                _exchangeBtns[rowIdx].interactable = can;
                _exchangeBtns[rowIdx].onClick.RemoveAllListeners();
                _exchangeBtns[rowIdx].onClick.AddListener(() =>
                {
                    // ClaimLevel 内部：发金币/钻石 → 领取即写入存档 → 弹「获得稀有·暮火之杖」确认窗
                    if (!AdventureLogMileage.ClaimLevel(2))
                    {
                        UIManager.Instance?.ShowToast("暂不可领取");
                        return;
                    }
                    UIManager.Instance?.ShowToast($"已领取 Lv2 里程奖励");
                    RefreshPanel();
                    _host?.RefreshAfterPhase3();
                });
            }
            return;
        }

        int idx = e.ItemIndex;
        var item = AdventureLogMileageShop.Items[idx];
        bool canBuy = AdventureLogMileageShop.CanBuy(idx, out _);
        int remain = AdventureLogMileageShop.RemainThisWeek(item);

        // Label：只写名称 + 消耗点数，不含限购次数
        if (_actionLabels[rowIdx] != null)
            _actionLabels[rowIdx].text = $"{item.Name}　<color=#{ColorUtility.ToHtmlStringRGB(PriceColorHex)}>{item.CostPoints}点</color>";

        // 次数：本周可领/总次数；每周领完或点数不足 → 置灰
        if (_countTexts[rowIdx] != null)
        {
            _countTexts[rowIdx].text = $"{remain}/{item.WeeklyLimit}";
            _countTexts[rowIdx].color = canBuy ? _countBaseColors[rowIdx] : DisabledColor;
        }

        // Icon：物品图标
        if (_icons[rowIdx] != null) _icons[rowIdx].sprite = ShopIconFor(item.Id);

        // 兑换按钮：可领可付才可点，否则整钮置灰
        if (_exchangeBtns[rowIdx] != null)
        {
            _exchangeBtns[rowIdx].interactable = canBuy;
            _exchangeBtns[rowIdx].onClick.RemoveAllListeners();
            _exchangeBtns[rowIdx].onClick.AddListener(() =>
            {
                if (AdventureLogMileageShop.TryBuy(idx, out string msg))
                    UIManager.Instance?.ShowToast(msg);
                else
                    UIManager.Instance?.ShowToast(msg ?? "无法兑换");
                RefreshPanel();
            });
        }
    }

    /// <summary>商品图标：按商品 Id 取项目通用图标（取不到报错，不兜底图）。</summary>
    static Sprite ShopIconFor(string id)
    {
        switch (id)
        {
            case "gold_bag": return CommonIconSprites.Load("icon_gold");
            case "mats_3": return CommonIconSprites.Load("icon_talent_stone");
            case "stamina_1": return LoadResIcon("Art/UI/Icons/Common/icon_stamina_potion");
            case "scroll_c": return CommonIconSprites.Load("icon_hire_scroll");
            // 高级招募卷轴：主人 2026-10-17 新图（与战斗掉落同图），缺了回退普通雇佣卷轴
            case "scroll_l":
                var adv = Resources.Load<Sprite>("Art/UI/Icons/Common/高级招募卷轴");
                return adv != null ? adv : CommonIconSprites.Load("icon_hire_scroll");
            default: return null;
        }
    }

    static Sprite LoadResIcon(string resPath)
    {
        var sp = Resources.Load<Sprite>(resPath);
        if (sp == null) Debug.LogError($"[Phase3Panel] 缺图标 Resources/{resPath}");
        return sp;
    }

    // =====================================================================
    // 2026-10-20 主人拍板：8 个 UI 预制体一键生成脚本全部删除（含本面板的
    // Phase3PanelPrefabBuilder 与 BuildHierarchy 构建段），预制体以磁盘手工版为唯一真源。
    // =====================================================================
}
