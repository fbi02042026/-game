using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：背包面板（点外收起层、8列宽规格、瘦屏防溢出自适应、翻页）
public partial class CharacterUI
{

    /// <summary>
    /// D：点面板外任意处收起。运行时建一层全屏透明点击层（Image alpha 0、raycastTarget=1），
    /// 插在 BackpackPanel <b>之前</b>（即之下），所以绝不会挡住面板自己的按钮与格子。
    /// 写法参考 RewardPopupUI.SetupTapAnywhereToClose。
    /// </summary>
    void EnsureBagTapLayer()
    {
        if (!tapOutsideToCloseBag || bagPanelRoot == null) return;
        if (_bagTapLayer != null)
        {
            KeepTapLayerBehindPanel();
            return;
        }

        var parent = bagPanelRoot.parent;
        if (parent == null) return;

        var go = new GameObject("BagTapAnywhereLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.interactable = true;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(CloseBackpackPanel);

        _bagTapLayer = go;
        go.SetActive(false);   // 新建对象默认 active，先关掉，等 OpenBackpack 打开面板时才亮
        KeepTapLayerBehindPanel();
    }

    /// <summary>保证点击层排在 BackpackPanel 之前（之下），不挡面板本身。</summary>
    void KeepTapLayerBehindPanel()
    {
        if (_bagTapLayer == null || bagPanelRoot == null) return;
        int panelIdx = bagPanelRoot.GetSiblingIndex();
        if (_bagTapLayer.transform.parent != bagPanelRoot.parent) return;
        if (_bagTapLayer.transform.GetSiblingIndex() > panelIdx)
            _bagTapLayer.transform.SetSiblingIndex(panelIdx);
    }

    void SetBagTapLayerActive(bool active)
    {
        if (_bagTapLayer == null) return;
        if (_bagTapLayer.activeSelf != active)
            _bagTapLayer.SetActive(active);
        if (active) KeepTapLayerBehindPanel();
    }

    /// <summary>
    /// B：角色页背包网格的显示规格覆盖 —— 每行 8 格、共 4 行、下 2 行上锁。
    /// 只写本组件上的角色页专属网格（CharacterBagGrid）的字段，
    /// 不碰 GameConfig、物品放置逻辑与存档格式，也不碰共用组件 TownBackpackGrid。
    /// 2026-09-25：主人已经在预制体 Content/BackpackPanel/GridContainer 里手摆好 32 格
    /// （Cell_{显示行}_{显示列}，行 0..3、列 0..7）并调好了 GridLayoutGroup（82×82 / 8 列），
    /// 所以这里**不再重建** —— 只绑定；只有预制体里确实没有 GridContainer 时才兜底重建。
    /// </summary>
    void ApplyWideBackpackGrid()
    {
        if (backpackGrid == null) return;

        var bag = backpackGrid as CharacterBagGrid;
        if (bag != null)
        {
            if (useWideBackpackGrid)
            {
                bag.columns = WideBackpackColumns;
                bag.unlockedDisplayRows = WideBackpackUnlockedRows;
            }
            else
            {
                // 关掉宽规格：两个值置 0，子类会回退基类默认（4 列 / 存档真值）
                bag.columns = 0;
                bag.unlockedDisplayRows = 0;
            }
            bag.page = 0;
        }
        _bagPage = 0;

        // 主人的 32 格是手摆的：先绑定，绝不销毁重建（重建会把手摆格子和手调参数全清掉）
        backpackGrid.BindFromHierarchy(transform);
        // 只有预制体里真的没有 GridContainer（绑定没拿到）时才兜底重建
        if (backpackGrid.gridContainer == null)
            backpackGrid.BuildGrid(bagPanelRoot != null ? bagPanelRoot : backpackGrid.transform);
        backpackGrid.Refresh();
    }

    /// <summary>
    /// 背包窗体自适应：只在瘦屏下做「防溢出 / 防裁切」，绝不主动做位移。
    /// a) 格子自适应：算出来的格子宽度比主人调的 82 小才缩小，**只缩小不放大**；
    /// b) 面板完整可见：只有顶边超出父级顶边、或底边超出父级底边时才把 anchoredPosition.y 钳回来，
    ///    不超出就一动不动。
    /// 开关关掉或非瘦屏 → 把主人手摆的原始值完整还原后 return。
    /// 绝不改 BackpackPanel 的 anchor / pivot / sizeDelta（都是主人手摆的）。
    /// </summary>
    void ApplyBackpackPanelFit()
    {
        if (backpackGrid == null || bagPanelRoot == null) return;
        var panelRt = bagPanelRoot as RectTransform;
        var gridRt = backpackGrid.gridContainer;
        if (panelRt == null || gridRt == null) return;

        var gl = gridRt.GetComponent<GridLayoutGroup>();

        // 首次进来先把主人手摆的原始值记下来，后面还原用
        if (!_bagFitCaptured)
        {
            _bagFitBase.panelAnchorMin = panelRt.anchorMin;
            _bagFitBase.panelAnchorMax = panelRt.anchorMax;
            _bagFitBase.panelPivot = panelRt.pivot;
            _bagFitBase.panelSizeDelta = panelRt.sizeDelta;
            _bagFitBase.panelPos = panelRt.anchoredPosition;
            _bagFitBase.gridAnchorMin = gridRt.anchorMin;
            _bagFitBase.gridAnchorMax = gridRt.anchorMax;
            _bagFitBase.gridPivot = gridRt.pivot;
            _bagFitBase.gridSizeDelta = gridRt.sizeDelta;
            _bagFitBase.gridPos = gridRt.anchoredPosition;
            if (gl != null)
            {
                _bagFitBase.cellSize = gl.cellSize;
                _bagFitBase.spacing = gl.spacing;
                if (gl.padding != null)
                {
                    _bagFitBase.hasPadding = true;
                    _bagFitBase.padL = gl.padding.left;
                    _bagFitBase.padR = gl.padding.right;
                    _bagFitBase.padT = gl.padding.top;
                    _bagFitBase.padB = gl.padding.bottom;
                }
            }
            _bagFitCaptured = true;
        }

        if (!enableBackpackFit || !UiLayoutStretch.IsThinnerScreen())
        {
            RestoreBackpackFit(panelRt, gridRt, gl);
            return;
        }

        // a) 格子自适应：只缩小不放大，绝不盖掉主人调好的 82
        if (gl != null)
        {
            int cols = BagDisplayColumns();
            if (cols > 0)
            {
                float padLR = gl.padding != null ? gl.padding.left + gl.padding.right : 0f;
                float w = (gridRt.rect.width - padLR - (cols - 1) * gl.spacing.x) / cols;
                if (w > 0f && w < gl.cellSize.x)
                    gl.cellSize = new Vector2(w, w);
            }
        }

        // b) 面板完整可见：超出父级才钳 y，不超出就一动不动（父级矩形用父级 rect 算）
        var parentRt = panelRt.parent as RectTransform;
        if (parentRt != null)
        {
            var corners = new Vector3[4];
            panelRt.GetWorldCorners(corners);
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float y = parentRt.InverseTransformPoint(corners[i]).y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float dy = 0f;
            if (maxY > parentRt.rect.yMax) dy = parentRt.rect.yMax - maxY;
            else if (minY < parentRt.rect.yMin) dy = parentRt.rect.yMin - minY;
            if (dy > 0.5f || dy < -0.5f)
                panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, panelRt.anchoredPosition.y + dy);
        }
    }

    /// <summary>把背包面板 / 网格还原成主人手摆的原始值（非瘦屏或开关关闭时走这里）。</summary>
    void RestoreBackpackFit(RectTransform panelRt, RectTransform gridRt, GridLayoutGroup gl)
    {
        if (!_bagFitCaptured) return;
        panelRt.anchorMin = _bagFitBase.panelAnchorMin;
        panelRt.anchorMax = _bagFitBase.panelAnchorMax;
        panelRt.pivot = _bagFitBase.panelPivot;
        panelRt.sizeDelta = _bagFitBase.panelSizeDelta;
        panelRt.anchoredPosition = _bagFitBase.panelPos;
        gridRt.anchorMin = _bagFitBase.gridAnchorMin;
        gridRt.anchorMax = _bagFitBase.gridAnchorMax;
        gridRt.pivot = _bagFitBase.gridPivot;
        gridRt.sizeDelta = _bagFitBase.gridSizeDelta;
        gridRt.anchoredPosition = _bagFitBase.gridPos;
        if (gl != null)
        {
            gl.cellSize = _bagFitBase.cellSize;
            gl.spacing = _bagFitBase.spacing;
            if (_bagFitBase.hasPadding)
                gl.padding = new RectOffset(_bagFitBase.padL, _bagFitBase.padR, _bagFitBase.padT, _bagFitBase.padB);
        }
    }

    /// <summary>
    /// 拿角色页专属网格实例。预制体上可能同时挂着通用 TownBackpackGrid 与 CharacterBagGrid，
    /// GetComponent&lt;TownBackpackGrid&gt;() 的返回顺序不确定，所以遍历全部：
    /// 只认 CharacterBagGrid，其余通用组件运行时停用（enabled = false，不写盘、不改预制体），
    /// 避免两个组件抢同一个 GridContainer。
    /// </summary>
    CharacterBagGrid ResolveBagGrid()
    {
        CharacterBagGrid bag = null;
        var all = GetComponents<TownBackpackGrid>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is CharacterBagGrid b) bag = b;
            else all[i].enabled = false;        // 停用预制体上自带的通用组件
        }
        if (bag == null) bag = gameObject.AddComponent<CharacterBagGrid>();
        backpackGrid = bag;                     // 字段类型保持 TownBackpackGrid 不变
        return bag;
    }

    /// <summary>
    /// C：格子下面的左右翻页按钮。预制体优先（BackpackPanel/PrevPageButton、NextPageButton，
    /// 再找中文「上一页」/「下一页」），都找不到才在 GridContainer 同级、它之后运行时创建。
    /// </summary>
    void EnsureBagPageButtons()
    {
        if (bagPanelRoot == null) return;

        // 角色页 32 格（8 列 × 4 显示行）一页就把 16 个逻辑格全显示完了，第 2 页没有新内容 →
        // 翻页没有意义：只有 BagPageCount() > 1 时才创建 / 显示按钮，否则找到也隐藏，绝不新建无用按钮。
        if (BagPageCount() <= 1)
        {
            if (bagPrevPageButton == null) bagPrevPageButton = FindBagPageBtn("PrevPageButton", "上一页");
            if (bagNextPageButton == null) bagNextPageButton = FindBagPageBtn("NextPageButton", "下一页");
            if (bagPrevPageButton != null && bagPrevPageButton.gameObject.activeSelf)
                bagPrevPageButton.gameObject.SetActive(false);
            if (bagNextPageButton != null && bagNextPageButton.gameObject.activeSelf)
                bagNextPageButton.gameObject.SetActive(false);
            return;
        }

        if (bagPrevPageButton == null) bagPrevPageButton = FindBagPageBtn("PrevPageButton", "上一页");
        if (bagNextPageButton == null) bagNextPageButton = FindBagPageBtn("NextPageButton", "下一页");

        if (bagPrevPageButton != null && !bagPrevPageButton.gameObject.activeSelf)
            bagPrevPageButton.gameObject.SetActive(true);
        if (bagNextPageButton != null && !bagNextPageButton.gameObject.activeSelf)
            bagNextPageButton.gameObject.SetActive(true);

        if (bagPrevPageButton == null) bagPrevPageButton = BuildBagPageBtn("PrevPageButton", "◀", -100f);
        if (bagNextPageButton == null) bagNextPageButton = BuildBagPageBtn("NextPageButton", "▶", 100f);

        if (bagPrevPageButton != null)
        {
            bagPrevPageButton.onClick.RemoveAllListeners();
            bagPrevPageButton.onClick.AddListener(PrevBagPage);
        }
        if (bagNextPageButton != null)
        {
            bagNextPageButton.onClick.RemoveAllListeners();
            bagNextPageButton.onClick.AddListener(NextBagPage);
        }
    }

    Button FindBagPageBtn(string enName, string cnName)
    {
        if (bagPanelRoot == null) return null;
        return bagPanelRoot.Find(enName)?.GetComponent<Button>()
               ?? bagPanelRoot.Find(cnName)?.GetComponent<Button>();
    }

    Button BuildBagPageBtn(string name, string label, float x)
    {
        if (bagPanelRoot == null) return null;
        var img = CreateImg(bagPanelRoot, name, new Color(0.45f, 0.36f, 0.2f, 0.95f));
        Set(img.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, x, BagPageButtonY, 120f, 44f);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var tx = CreateTxt(img.transform, "Label", label, 26, new Color(1f, 0.95f, 0.8f));
        Stretch(tx.rectTransform);
        return btn;
    }

    /// <summary>
    /// 背包有效页数：只数「至少有一格未锁」的页。
    /// 角色页 32 格（8 列 × 4 显示行，一个显示行折 2 个逻辑行）一页就铺到 8 个逻辑行，
    /// 已经 ≥ BACKPACK_HEIGHT_MAX(4) → 16 个逻辑格一页全显示完，第 2 页起全是越界锁格 → 只有 1 页。
    /// 返回值始终 >= 1，**绝不返回 0**。
    /// </summary>
    int BagPageCount()
    {
        if (backpackGrid == null) return 1;
        int offsetRows = backpackGrid.LogicalRowsPerPage();    // 页间逻辑行偏移
        if (offsetRows <= 0) return 1;

        // 一页实际铺到的逻辑行数 = 显示行数 × 一个显示行折进去的逻辑行数
        int perRowLogical = GameConfig.BACKPACK_HEIGHT_MAX / offsetRows;
        if (perRowLogical <= 0) perRowLogical = 1;
        int rowsPerPage = BagDisplayRowCount() * perRowLogical;
        if (rowsPerPage <= 0) return 1;

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)GameConfig.BACKPACK_HEIGHT_MAX / rowsPerPage));
        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        int count = 0;
        for (int p = 0; p < totalPages; p++)
        {
            int rowStart = p * rowsPerPage;
            if (rowStart >= GameConfig.BACKPACK_HEIGHT_MAX) break;   // 整页越界 → 全是锁格
            if (rowStart >= unlockedRows) break;                     // 这页起已没有解锁行 → 全是锁格
            count++;
        }
        return count > 0 ? count : 1;
    }

    /// <summary>背包网格的显示行数（总格数 ÷ 显示列数）。</summary>
    int BagDisplayRowCount()
    {
        int cols = BagDisplayColumns();
        if (backpackGrid == null || cols <= 0) return 0;
        return Mathf.CeilToInt((float)backpackGrid.cells.Count / cols);
    }

    /// <summary>背包网格的显示列数（角色页 8 列；基类 / 弹窗为 GameConfig.BACKPACK_WIDTH）。</summary>
    int BagDisplayColumns()
    {
        if (backpackGrid is CharacterBagGrid bag && bag.columns > 0) return bag.columns;
        return GameConfig.BACKPACK_WIDTH;
    }

    public void PrevBagPage() => StepBagPage(-1);
    public void NextBagPage() => StepBagPage(1);

    /// <summary>翻页：只改显示用的页索引并刷新，绝不移动/删除任何存档道具。</summary>
    void StepBagPage(int delta)
    {
        int pages = BagPageCount();
        if (pages <= 1)
        {
            UIManager.Instance?.ShowToast("没有更多页");
            return;
        }

        int next = Mathf.Clamp(_bagPage + delta, 0, pages - 1);
        if (next == _bagPage)
        {
            UIManager.Instance?.ShowToast("没有更多页");
            return;
        }

        _bagPage = next;
        if (backpackGrid is CharacterBagGrid bag) bag.page = next;
        RefreshBag();
    }
}
