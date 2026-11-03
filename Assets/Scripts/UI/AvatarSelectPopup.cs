using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 头像选择弹窗（2026-10-25 主人需求；2026-11-03 主人重做预制体 → 佣兵头像切换模式）。
/// 预制体：Resources/Prefabs/Town/头像选择框（主人手摆；meta 是 56 字符 guid，只走路径加载）。
/// 结构：根（带 Canvas，prefab scale=0 需运行时扶正）→ Dim（全屏黑底+按钮=点空白关）
///       + panl（Tile 标题 / 「头像1」模板格 = mask(底框裁剪)+icon(头像图)+kuang(选中框) / 确定钮）。
/// 行为：预制体只摆一格「头像1」模板 —— 本类按玩家【已拥有】佣兵克隆格子塞进
///       运行时补建的【横向两排滑动列表】（ScrollRect 左右滑，2026-11-03 主人拍板「可以左右滑动」+
///       2026-11-03 二次拍板「头像看玩家拥有几个就显示几个」= 1 号玩家 + 已解锁 H001~H024，
///       未拥有的佣兵不出格；UI 运行时补节点不改预制体，先例 TalentUI.Build/QuestPanelUI）。
///       点格选中（kuang 外框常显，选中那格染高亮色）→ 点确定换头像并存档 → 主界面名牌等非战斗界面统一刷新。
///       默认头像 = 1 号玩家（2026-11-03 主人拍板「默认是玩家」）；可切换范围 = 玩家 + 已拥有佣兵头像。
///       【2026-11-03 主人拍板「预制体引用拖了总保存不了，直接按路径加载」】预制体 6 处 sprite 引用
///       全是悬空 guid —— panl/Tile/确定/mask/kuang 底图全部运行时按 Resources 路径主动设图
///       （Common 通用窗体组 + Main/头像 192×180 头像框组，见 Sprite* 常量），icon 按佣兵号/玩家设图。
/// 头像真源：<see cref="AvatarDefs"/>（1=玩家 player，2~25=H001~H024，图源 Resources/UI/Icons/MercHead）。
/// </summary>
public class AvatarSelectPopup : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Town/头像选择框";
    const string LogTag = "[AvatarSelect] ";

    // === 底图路径（2026-11-03 主人拍板「预制体引用拖了总保存不了，直接按路径加载」）===
    // 预制体 6 处 sprite 引用全是悬空 guid（全库 meta 解析不到），运行时按项目现成素材主动设图：
    // mask/kuang 用 UI/Art/Main/头像/ 那组 192×180 头像框 —— 与主人预制体节点尺寸严丝合缝（原拖图就是这组）。
    const string DirCommon = "UI/Art/Common/";
    const string SpriteWindow = DirCommon + "通用窗体1";        // panl 弹窗底图（九宫切片；450×350 与 320×246 近同比例）
    const string SpriteHeader = DirCommon + "通用窗体标头1";     // Tile 标题条
    const string SpriteOkBtn = DirCommon + "确定按钮";          // 确定钮底图
    // 每格 mask/icon/kuang 三张图由 AvatarNodeUI.BindPrefabNode 统一按路径设（底框/头像框那组 192×180）。

    // === 滑动列表排版（两排；2026-11-03 修：步进全部由预制体实测推导，不再硬编码）===
    // 【踩坑实锤】主人手摆的 mask 是 192×180 × localScale 0.7 = 实际渲染 134×126，旧代码步进却写死
    // 110/105 → 每格宽出 24px，相邻格子互相压叠（主人截图「越改越坏」）。现在：
    //   步进 = 实测头像渲染尺寸 + 缝隙；视口宽 = panl 实测宽；视口高/中心 = Tile 底边与确定钮顶边
    //   之间的可用区 —— 主人改 prefab 尺寸或位置，布局自动跟随（硬规矩：以主人预制体为准）。
    const float GapX = 12f;              // 列缝隙（实测 134.4 + 12 = 146.4 → panl 450 宽每排 3 格）
    const float GapY = 10f;              // 行缝隙（实测 126 + 10 = 136 → 两排 272 高）
    const float FallbackCellW = 134.4f;  // 量不到 mask 时的兜底值（192×0.7；不兜底口径，仅记 LogError）
    const float FallbackCellH = 126f;
    /// <summary>【2026-11-03 主人拍板「选择头像框放的太大了 缩小30%」】整体等比缩放再打七折。</summary>
    const float PanelScaleBias = 0.7f;

    // 选中标识：kuang 外框【常显】（与主界面名牌同款，2026-11-03 主人「为什么把 kuang 的 Image 都关了」——
    // 关掉就只剩底框，跟主界面长得不一样），选中的那格染高亮色区分，不碰尺寸/显隐/位置。
    static readonly Color NormalFrameColor = Color.white;
    static readonly Color SelectedFrameColor = new Color(1f, 0.86f, 0.35f);   // 金黄高亮

    public static AvatarSelectPopup Instance { get; private set; }

    readonly Dictionary<int, Image> _cellKuangs = new Dictionary<int, Image>();   // 头像号 → 选中框（选中才亮）
    readonly List<int> _avatars = new List<int>();             // 本局展示的头像号（1 玩家 + 已拥有佣兵，按号升序）
    readonly Dictionary<int, int> _avatarIndex = new Dictionary<int, int>();     // 头像号 → 列表下标（滚动定位用）
    ScrollRect _scroll;
    int _perRow = 1;          // 本局每排格数（panl 实测宽 / 实测步进）
    float _colStep = 146.4f;  // 列步进（实测头像渲染宽 + 缝）
    float _viewW = 450f;      // 列表窗口宽（panl 实测宽）
    int _selected = AvatarDefs.DefaultSlot;

    /// <summary>打开弹窗（已开着就重建一份，保证选中态与存档一致）。缺预制体 LogError 不兜底。</summary>
    public static void Open()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(LogTag + "取不到预制体 Resources/" + PrefabPath + "，头像选择弹窗打不开（不兜底）");
            return;
        }
        var inst = Instantiate(prefab);
        // 【2026-11-03 实测根因】预制体根上没挂本脚本（主人手摆重做后只剩 RectTransform+Canvas，
        // 脚本 guid 在 prefab 里零引用）—— 弹窗以裸布局显示：不适配、格子不铺、按钮全死。
        // 这里运行时自动补挂（幂等：主人以后挂上了也只挂一份）；AddComponent 对 active 对象
        // 立即触发 Awake → 扶正 scale / ApplyPopup / Build 全链路照跑。
        if (inst.GetComponent<AvatarSelectPopup>() == null)
        {
            Debug.Log(LogTag + "预制体根上没挂脚本，运行时自动补挂");
            inst.AddComponent<AvatarSelectPopup>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 预制体根 scale=(0,0)（主人保存原样）：ApplyPopup 的 EnsureRootStretch 会扶正，
        // 这里先自行扶正一次双保险（万一 Canvas 缺失 ApplyPopup 早退，弹窗也不至于隐形）
        var rootRt = transform as RectTransform;
        if (rootRt != null && rootRt.localScale.sqrMagnitude < 0.0001f)
            rootRt.localScale = Vector3.one;
        // 顺带套全项目 Canvas 规范
        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.TownPopup);
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        var panl = FindDeep(transform, "panl");
        if (panl == null)
        {
            Debug.LogError(LogTag + "预制体缺 panl 节点，头像列表摆不出来（不兜底）");
            return;
        }

        // panl 整体等比放大（先放大再铺内容；Dim 全屏黑底在 panl 外，不受影响）
        FitPanelToScreen(panl);

        // 底图按路径设（预制体引用悬空，主人拍板路径加载口径）
        var panlImg = panl.GetComponent<Image>();
        if (panlImg != null) panlImg.sprite = LoadFixedSprite(SpriteWindow, "panl");
        else Debug.LogError(LogTag + "panl 缺 Image，弹窗底图设不了");
        var tileT = FindDeep(panl, "Tile");
        var tileImg = tileT != null ? tileT.GetComponent<Image>() : null;
        if (tileImg != null) tileImg.sprite = LoadFixedSprite(SpriteHeader, "Tile");
        else Debug.LogError(LogTag + "预制体缺 Tile 节点或缺 Image，标题底图设不了");

        // Dim：全屏黑底按钮，点空白 = 取消关闭。预制体 Image raycast 实测=1，这里再强制一遍防改丢；
        // 没补过 Button 就补一个 —— 保证点空白必关、且挡住下层全部点击（真模态，同 2026-11-02 每日登录口径）
        var dim = FindDeep(transform, "Dim");
        if (dim != null)
        {
            var dimImg = dim.GetComponent<Image>();
            if (dimImg == null) dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.raycastTarget = true;
            var dimBtn = dim.GetComponent<Button>();
            if (dimBtn == null) dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(Close);
        }
        else Debug.LogError(LogTag + "预制体缺 Dim 节点，点空白无法关闭弹窗（不兜底）");

        // 确定：预制体只有 Image，运行时补 Button 接线（同「战斗失败确定钮」口径）
        var okT = FindDeep(panl, "确定");
        if (okT != null)
        {
            var okImg = okT.GetComponent<Image>();
            if (okImg != null) okImg.sprite = LoadFixedSprite(SpriteOkBtn, "确定");
            var okBtn = okT.GetComponent<Button>();
            if (okBtn == null) okBtn = okT.gameObject.AddComponent<Button>();
            okBtn.transition = Selectable.Transition.None;
            okBtn.onClick.AddListener(OnConfirm);
        }
        else Debug.LogError(LogTag + "预制体缺「确定」节点，选完头像没法保存");

        // 模板格：预制体唯一一格「头像1」（mask 底框裁剪 + icon 头像图 + kuang 选中框）
        var template = FindDeep(panl, "头像1");
        if (template == null)
        {
            Debug.LogError(LogTag + "预制体缺模板格 头像1，头像列表克隆不出来（不兜底）");
            return;
        }
        BuildScrollList(panl, template);
        int cur = AvatarDefs.CurrentSlot();
        if (!_avatarIndex.ContainsKey(cur))
        {
            Debug.Log(LogTag + "当前头像号 " + cur + " 不在本局列表（未拥有或无图），选中回落 1 号（玩家）");
            cur = AvatarDefs.DefaultSlot;
        }
        Select(cur);
        ScrollToSelected();
    }

    /// <summary>按路径取固定底图；取不到 LogError 不兜底（预制体引用悬空，路径加载是唯一口径）。</summary>
    static Sprite LoadFixedSprite(string path, string what)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError(LogTag + what + " 底图取不到：Resources/" + path + "（不兜底）");
        return sp;
    }

    /// <summary>
    /// 【2026-11-03 主人需求「这个界面要适配、根据分辨率放大」】弹窗整体等比缩放，
    /// 口径同 BattleSettlementUI.FitPanelUniformScale（2026-10-28 拍板：只改一层 localScale、
    /// 子节点整体跟缩、禁止拉伸）。
    /// 【2026-11-03 修】改用静态设计值，不再读 Screen —— 编辑器 Game 视图比例不可信：
    /// 横条视图曾把逻辑高解析成 280 → s 被压到 0.8 下限 → 弹窗反而缩没了（主人截图「太小」的根因）。
    /// CanvasScaler 已把逻辑空间归一到 ≥720×1280（瘦屏按宽、胖屏按高、极端比例相机裁回 9:16），
    /// panl 1.6 倍 = 720 宽正好铺满，真机任何屏都放得下。panl 尺寸运行时读 rect（主人改预制体自动跟随）。
    /// </summary>
    void FitPanelToScreen(Transform panel)
    {
        var rt = panel as RectTransform;
        float pw = rt != null ? rt.rect.width : 450f;
        float ph = rt != null ? rt.rect.height : 350f;
        if (pw < 1f) pw = 450f;
        if (ph < 1f) ph = 350f;
        float s = Mathf.Min(GameConfig.DESIGN_WIDTH / pw, GameConfig.DESIGN_HEIGHT / ph);
        // 【2026-11-03 主人拍板「选择头像框放的太大了 缩小30%」】整体等比再乘 0.7
        s *= PanelScaleBias;
        if (s < 0.8f) s = 0.8f;
        if (s > 2.0f) s = 2.0f;
        panel.localScale = new Vector3(s, s, 1f);
        Debug.Log(LogTag + "panl 适配缩放 " + s.ToString("0.00") + "（panl " + pw.ToString("0") + "×" + ph.ToString("0") + "）");
    }

    /// <summary>运行时补横向滑动列表 + 按已拥有佣兵克隆格子（1 玩家 + 已解锁 H001~H024；不改预制体；先例 TalentUI.Build.cs / QuestPanelUI.cs）。</summary>
    void BuildScrollList(Transform panl, Transform template)
    {
        CollectOwnedAvatars();
        int count = _avatars.Count;
        // 一格的真实渲染尺寸 + 头像图(mask)相对格根的偏移 —— 全按主人 prefab 实测（2026-11-03 踩坑后改）
        var cellSize = MeasureCellSize(template);
        var maskOff = MeasureMaskOffset(template);
        float colStep = cellSize.x + GapX;
        float rowStep = cellSize.y + GapY;
        Debug.Log(LogTag + "实测一格头像 " + cellSize.x.ToString("0.0") + "×" + cellSize.y.ToString("0.0")
            + "，mask 偏移(" + maskOff.x.ToString("0.0") + "," + maskOff.y.ToString("0.0") + ")"
            + " → 步进 " + colStep.ToString("0.0") + "×" + rowStep.ToString("0.0"));

        // 视口：宽 = panl 实测宽；高 = 两排；中心 = Tile 底边到确定钮顶边这段可用区的中点
        var panlRt = (RectTransform)panl;
        float viewW = panlRt.rect.width;
        float top = UsableTop(panl);
        float bottom = UsableBottom(panl);
        float viewH = rowStep * 2f;
        float viewY = (top + bottom) * 0.5f;
        if (viewH > top - bottom + 1f)
            Debug.LogError(LogTag + "两排头像要 " + viewH.ToString("0") + " 高，panl 可用区只有 "
                + (top - bottom).ToString("0") + "（加高 panl 或挪 Tile/确定钮），下面一排会被裁掉");

        // 视口（横向滑动窗口）：隐形 Image 接射线（拖动滚动靠它）+ RectMask2D 裁剪
        var scrollGo = new GameObject("AvatarScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(panl, false);
        var viewRt = (RectTransform)scrollGo.transform;
        viewRt.anchorMin = new Vector2(0.5f, 0.5f);
        viewRt.anchorMax = new Vector2(0.5f, 0.5f);
        viewRt.pivot = new Vector2(0.5f, 0.5f);
        viewRt.anchoredPosition = new Vector2(0f, viewY);
        viewRt.sizeDelta = new Vector2(viewW, viewH);
        scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        // 内容：两排铺开（第一排按窗口宽装满，装不下的去第二排），左中锚；不满一排时整体居中
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = new Vector2(0f, 0.5f);
        contentRt.anchorMax = new Vector2(0f, 0.5f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        int perRow = PerRowOf(count, viewW, colStep);
        float contentW = perRow * colStep;
        contentRt.sizeDelta = new Vector2(contentW, viewH);
        // 【2026-11-03 主人截图「右边的空隙多」】居中不能靠 anchoredPosition 偏移 —— ScrollRect 首帧布局
        // 会把 content 重置回滚动原点（左对齐），偏移被吃掉 → 整排贴左边框、右侧空一块。
        // 装得下一屏时改用居中锚点（ScrollRect 对零滚动范围的居中内容不动它）；装不下才左锚交给滚动。
        if (contentW <= viewW)
        {
            contentRt.anchorMin = new Vector2(0.5f, 0.5f);
            contentRt.anchorMax = new Vector2(0.5f, 0.5f);
            contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
        }
        else
        {
            contentRt.anchoredPosition = Vector2.zero;   // 可滚动：从最左开始，ScrollRect 接管
        }
        _perRow = perRow;
        _colStep = colStep;
        _viewW = viewW;

        _scroll = scrollGo.GetComponent<ScrollRect>();
        _scroll.content = contentRt;
        _scroll.viewport = viewRt;
        _scroll.horizontal = true;
        _scroll.vertical = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;

        // 克隆：第 1 格直接用预制体模板「头像1」，其余按同款节点模式克隆（改名 防重名；
        // 显式 worldPositionStays=false，防把世界缩放带进 localScale）
        template.SetParent(contentRt, false);
        var cells = new List<Transform> { template };
        for (int i = 1; i < count; i++)
        {
            var clone = Instantiate(template, contentRt, false);
            clone.name = "头像" + _avatars[i];
            cells.Add(clone);
        }
        for (int i = 0; i < cells.Count; i++)
        {
            int avatar = _avatars[i];
            int col = i % perRow;
            int row = i / perRow;
            // 让【头像图 mask 的中心】落在网格点上：减掉 mask 相对格根的偏移，整组头像才不会偏
            float x = (col + 0.5f) * colStep - maskOff.x;
            float y = (row == 0 ? rowStep * 0.5f : -rowStep * 0.5f) - maskOff.y;
            ConfigureCell(cells[i], avatar, new Vector2(x, y));
        }
    }

    /// <summary>
    /// 【2026-11-03 主人拍板「头像看玩家拥有几个就显示几个」】收集本局要展示的头像号：
    /// 1 号玩家本人恒在；2~25 号 = H001~H024 逐个查 IsMercUnlocked（真源 SaveData.unlockedMercIds）
    /// 且 Resources 里有头像图 —— 没拥有的佣兵不出格（C001~C003 是剧情 NPC，不在 H 体系，天然不进列表）。
    /// </summary>
    void CollectOwnedAvatars()
    {
        _avatars.Clear();
        _avatarIndex.Clear();
        _avatars.Add(AvatarDefs.DefaultSlot);
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError(LogTag + "存档取不到（SaveSystem 未就绪），头像列表只出玩家格（不兜底）");
        }
        else
        {
            for (int avatar = AvatarDefs.MinSlot + 1; avatar <= AvatarDefs.MaxSlot; avatar++)
            {
                string hireId = AvatarDefs.HireIdOf(avatar);
                if (!data.IsMercUnlocked(hireId)) continue;
                if (MercPortraitSprites.GetHead(hireId) == null) continue;   // 该号没头像图不摆（不兜底口径）
                _avatars.Add(avatar);
            }
        }
        for (int i = 0; i < _avatars.Count; i++) _avatarIndex[_avatars[i]] = i;
    }

    /// <summary>
    /// 【2026-11-03 主人拍板「直接复制过去，效果要一致」+「别再改我选择框里的头像大小」】
    /// 一格 = 主人手摆「头像1」节点原样：mask/icon/kuang 子件与尺寸全部保留，代码只设图
    /// （AvatarNodeUI.BindPrefabNode，与主界面名牌同一份代码、零几何操作）；
    /// Button 挂 mask 上接点击（主人手摆根上无 Image，mask 是现成可点击图形）；
    /// kuang 外框常显（与主界面名牌同款），选中格染高亮色 = 选中标识。
    /// </summary>
    void ConfigureCell(Transform cell, int avatar, Vector2 pos)
    {
        var rt = (RectTransform)cell;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;   // 只摆进网格；尺寸/缩放/子件全按主人 prefab 原样，代码零操作

        // 设图：mask=头像框底 / icon=该头像号头像 / kuang=头像框（与主界面名牌同一套）
        AvatarNodeUI.BindPrefabNode(cell, AvatarDefs.HireIdOf(avatar));

        // 点击：Button 挂 mask 上（不建透明层、不销毁任何手摆子件）
        var maskT = FindDeep(cell, "mask");
        if (maskT == null)
        {
            Debug.LogError(LogTag + "格子缺 mask 子件，点击接不上（不兜底）");
        }
        else
        {
            var maskImg = maskT.GetComponent<Image>();
            if (maskImg != null) maskImg.raycastTarget = true;
            var btn = maskT.GetComponent<Button>();
            if (btn == null) btn = maskT.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            int captured = avatar;
            btn.onClick.AddListener(() => OnCellClicked(captured));
        }

        // 选中标识：kuang（头像框）常显，只有选中那格染高亮色（颜色切换，不碰 enabled/缩放）
        var kuangT = FindDeep(cell, "kuang");
        if (kuangT == null)
        {
            Debug.LogError(LogTag + "格子缺 kuang 子件，选中标识做不了（不兜底）");
        }
        else
        {
            var kuangImg = kuangT.GetComponent<Image>();
            if (kuangImg == null) Debug.LogError(LogTag + "kuang 缺 Image，选中标识亮不了（不兜底）");
            else
            {
                // 【2026-11-03 主人「为什么把 kuang 的 Image 都关了」】kuang 是外框，与主界面名牌同款【常显】；
                // 关掉它就只剩底框、跟主界面长得不一样。选中标识改成【染高亮色】，不碰尺寸/显隐/位置。
                kuangImg.raycastTarget = false;
                kuangImg.color = NormalFrameColor;
                _cellKuangs[avatar] = kuangImg;
            }
        }
    }

    /// <summary>两排布局的每排格数：按窗口宽能装几个装几个（第一排永远满）。</summary>
    static int PerRowOf(int count, float viewW, float colStep)
    {
        int byWidth = Mathf.Max(1, (int)(viewW / colStep));
        return Mathf.Clamp(byWidth, 1, Mathf.Max(1, count));
    }

    /// <summary>
    /// 实测一格头像的渲染尺寸（mask 层 sizeDelta × 从 mask 到格根的 scale 累乘）。
    /// 主人 prefab 实测 192×180 × 0.7 = 134.4×126 —— 旧代码写死 100 就是这里踩的坑。
    /// </summary>
    Vector2 MeasureCellSize(Transform cell)
    {
        var maskT = FindDeep(cell, "mask");
        if (maskT == null)
        {
            Debug.LogError(LogTag + "模板格缺 mask，量不到头像尺寸，走兜底值（不兜底口径，仅记错）");
            return new Vector2(FallbackCellW, FallbackCellH);
        }
        var rt = (RectTransform)maskT;
        float sx = cell.localScale.x, sy = cell.localScale.y;
        for (var cur = maskT; cur != null && cur != cell; cur = cur.parent)
        {
            sx *= cur.localScale.x;
            sy *= cur.localScale.y;
        }
        return new Vector2(rt.rect.width * sx, rt.rect.height * sy);
    }

    /// <summary>实测头像图(mask)相对格根的偏移（主人手摆值 -111.2,+37.6）：摆放时减掉它，头像才落在网格点上。</summary>
    static Vector2 MeasureMaskOffset(Transform cell)
    {
        var maskT = FindDeep(cell, "mask");
        if (maskT == null) return Vector2.zero;
        var sum = Vector2.zero;
        float k = 1f;
        for (var cur = maskT; cur != null && cur != cell; cur = cur.parent)
        {
            var rt = cur as RectTransform;
            if (rt != null) sum += new Vector2(rt.anchoredPosition.x * k, rt.anchoredPosition.y * k);
            var p = cur.parent;
            if (p == null) break;
            k *= p.localScale.x;
        }
        return sum;
    }

    /// <summary>列表可用区上边：Tile 标题条底边 y（缺 Tile 则用 panl 顶边）。</summary>
    static float UsableTop(Transform panl)
    {
        var panlRt = (RectTransform)panl;
        var tile = FindDeep(panl, "Tile") as RectTransform;
        if (tile == null) return panlRt.rect.height * 0.5f;
        return tile.anchoredPosition.y - tile.rect.height * tile.localScale.y * 0.5f;
    }

    /// <summary>列表可用区下边：确定钮顶边 y（缺确定钮则用 panl 底边）。</summary>
    static float UsableBottom(Transform panl)
    {
        var panlRt = (RectTransform)panl;
        var ok = FindDeep(panl, "确定") as RectTransform;
        if (ok == null) return -panlRt.rect.height * 0.5f;
        return ok.anchoredPosition.y + ok.rect.height * ok.localScale.y * 0.5f;
    }

    /// <summary>打开时把当前头像滚到可视区（横向滚动，选中格的【列】贴左，夹在 0~1；两排布局按列算）。</summary>
    void ScrollToSelected()
    {
        if (_scroll == null || _scroll.content == null) return;
        float maxScroll = Mathf.Max(0f, _perRow * _colStep - _viewW);
        if (maxScroll <= 0f) return;
        if (!_avatarIndex.TryGetValue(_selected, out int idx)) return;
        float x = (idx % _perRow) * _colStep;
        _scroll.horizontalNormalizedPosition = Mathf.Clamp01(x / maxScroll);
    }

    void OnCellClicked(int avatar)
    {
        if (!AvatarDefs.HasSprite(avatar)) return;
        Select(avatar);
    }

    /// <summary>
    /// 选中效果：kuang 外框【常显】（与主界面名牌同款），选中的那格染高亮色当标识。
    /// 【2026-11-03 主人「为什么把 kuang 的 Image 都关了」】旧版用 enabled 开关 → 没选中的格子只剩底框，
    /// 跟主界面长得不一样；现在只改 color，不动显隐/尺寸/位置。
    /// </summary>
    void Select(int avatar)
    {
        _selected = avatar;
        foreach (var kv in _cellKuangs)
            kv.Value.color = kv.Key == avatar ? SelectedFrameColor : NormalFrameColor;
    }

    void OnConfirm()
    {
        Debug.Log(LogTag + "点确定：选中头像号 " + _selected);
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError(LogTag + "存档取不到（SaveSystem 未就绪），头像不保存");
            return;
        }
        if (!AvatarDefs.HasSprite(_selected))
        {
            Debug.LogError(LogTag + "确认的头像号 " + _selected + " 非法，不保存（异常点击，不兜底）");
            return;
        }
        data.playerAvatar = _selected;
        data.avatarMercModeV2 = true;   // 新语义落盘标记（旧档槽位一次性迁移完成）
        SaveSystem.Instance.Save();
        // 主界面名牌 Icon 同步刷新（GuildHallUI.RefreshNameplate 按存档头像号整节点设图）
        if (GuildHallUI.Instance != null) GuildHallUI.Instance.RefreshAllHud();
        Close();
    }

    void Close()
    {
        Destroy(gameObject);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.name == name) return child;
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
