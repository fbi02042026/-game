using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽奖概率公示弹窗（2026-10-05 主人拍板；2026-10-11 改走<b>预制体</b>）。
///
/// 主人原话：「保底这个不用写到明面上，但是概率需要告诉玩家……可以再（抽）按钮旁边加个感叹号之类的，
/// 点击弹出弹窗上面写的概率」；「太复杂了 直接说佣兵技能装备分别占多少百分比 然后稀有和传奇的占多少就行」。
///
/// <para>所以弹窗只有两段，别的都不写：<br/>
/// ① <b>抽到什么</b>：技能 / 佣兵 / 装备 各占百分之多少；<br/>
/// ② <b>什么品质</b>：传说 / 稀有 / 普通 / 金币 各占百分之多少（没有「史诗」这一档）。</para>
///
/// <para>概率**不写死**，按 <see cref="DraftPool.BuildOddsRows"/> 从当前真实卡池现算 ——
/// 玩家解锁的技能变多、佣兵池变了，弹窗上的数字会跟着变，永远是真的。
/// 保底定序一个字都不提（主人要求）。</para>
///
/// <para><b>2026-10-11 主人拍板：「概率公示弹窗给我生成预制体」</b>。<br/>
/// 外观<b>不再运行时用纯色块拼</b>，改为加载预制体
/// <c>Resources/Prefabs/Battle/DraftOddsPopup</c>
/// （由 <c>Assets/Editor/DraftOddsPopupPrefabBuilder.cs</c> 单点生成，美术全部取自项目现有资源：
/// 撤离 / 替换装备弹窗同款 Panel 底图 + 天赋界面同款关闭按钮）。<br/>
/// 本类只负责<b>克隆行模板 + 填数字</b>：字号 / 颜色 / 行高 / 间距都在预制体里，主人改预制体即生效。
/// 概率行数是活的（随卡池变），所以预制体里放了两个<b>默认隐藏的模板</b>（RowTemplate / HeadTemplate），
/// 运行时按它克隆 —— 这样连行的样式主人都能在编辑器里调。</para>
/// </summary>
public class DraftOddsPopupUI : MonoBehaviour
{
    /// <summary>
    /// 预制体路径 —— 与 <c>DraftOddsPopupPrefabBuilder.PrefabPath</c> <b>必须同值</b>。
    /// 改一边忘了另一边 = 运行时加载不到。
    /// </summary>
    public const string PrefabPath = "Prefabs/Battle/DraftOddsPopup";

    /// <summary>
    /// 换段时的额外空档（「抽到什么」→「什么品质」之间）。
    /// ⚠ 这是本类里**唯一**的排版数字：行高 / 字号 / 颜色 / 左右留白全在预制体模板里，
    /// 只有「段与段之间多空多少」没法用模板表达，才留在这里。
    /// </summary>
    const float SectionGap = 12f;

    static DraftOddsPopupUI _inst;
    GameObject _root;
    Transform _listRoot;
    RectTransform _rowTemplate;
    RectTransform _headTemplate;
    Button _closeBtn;
    Button _dimBtn;
    RectTransform _panel;
    RectTransform _foot;
    /// <summary>根节点上的 Canvas（适配入口用；DDOL 跨场景后由 RefreshPopup 重绑相机 + 重套缩放）。</summary>
    Canvas _canvas;

    /// <summary>本次打开克隆出来的行，关掉时统一销毁（不销毁模板本身）。</summary>
    readonly List<GameObject> _spawned = new List<GameObject>();

    public static void Toggle()
    {
        var ui = Ensure();
        if (ui == null) return;
        // ⚠ 必须调实例方法 Close：静态 Hide() 用实例点出来会报 CS0176
        if (ui._root != null && ui._root.activeSelf) ui.Close();
        else ui.Open();
    }

    /// <summary>
    /// 外部口径入口（2026-10-17 酒馆招募公示）：调用方自己构建<b>真实</b>概率行
    /// （口径必须与实际随机逻辑一致，公示假数违反合规初衷），弹同一预制体。
    /// 行结构同 <see cref="DraftPool.BuildOddsRows"/>：先 Section=<see cref="DraftPool.SEC_TYPE"/>
    /// 段（抽到什么），再 Section=<see cref="DraftPool.SEC_QUALITY"/> 段（什么品质）。
    /// </summary>
    public static void ToggleWith(List<DraftPool.OddsRow> rows)
    {
        if (rows == null || rows.Count == 0) return;
        var ui = Ensure();
        if (ui == null) return;
        if (ui._root != null && ui._root.activeSelf) ui.Close();
        ui.OpenWith(rows);
    }

    public static void Hide()
    {
        if (_inst == null) return;
        _inst.Close();
    }

    static DraftOddsPopupUI Ensure()
    {
        if (_inst != null) return _inst;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过（铁律 14）：没预制体就没得谈，报出来让主人看见
            Debug.LogError($"[DraftOddsPopup] 找不到预制体 Resources/{PrefabPath}，" +
                           "请先在团结菜单跑 Tools → UI → 生成抽奖概率公示弹窗预制体");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "DraftOddsPopup";
        // 与 EvacuateConfirmPopup 同一套口径：自带 ScreenSpaceCamera Canvas，不挂 BattleUI 下、不随场景销毁
        DontDestroyOnLoad(go);
        var ui = go.GetComponent<DraftOddsPopupUI>() ?? go.AddComponent<DraftOddsPopupUI>();
        // 【2026-10-24 主人拍板】补统一适配入口（此前漏调）：预制体 CanvasScaler 写死「高对齐 720×1280」，
        // 瘦长屏（1080×2400）逻辑宽只剩 576 < Panel 620 → 面板左右被切、关闭按钮被切；
        // 且 worldCamera 为空、sortOrder 因 OverrideSorting=0 不生效。
        // ApplyPopup 会按 DesignAspectLetterbox.ResolveUiMatch() 重设宽对齐 + 绑相机 + override 层级。
        UICanvasSetup.ApplyPopup(go.GetComponent<Canvas>(), GameConfig.UiSort.DraftOddsPopup);
        return ui;
    }

    void Awake()
    {
        _inst = this;
        BindRefs();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    void BindRefs()
    {
        if (_root == null) _root = transform.Find("Root")?.gameObject ?? gameObject;
        _canvas = GetComponent<Canvas>();
        _listRoot = FindDeep(_root.transform, "List");
        // ⚠ FindDeep 返回的是 Transform，RectTransform 字段必须显式取组件（CS0266）
        _rowTemplate = FindDeep(_root.transform, "RowTemplate")?.GetComponent<RectTransform>();
        _headTemplate = FindDeep(_root.transform, "HeadTemplate")?.GetComponent<RectTransform>();
        _closeBtn = FindDeep(_root.transform, "BtnClose")?.GetComponent<Button>();
        _dimBtn = FindDeep(_root.transform, "Dim")?.GetComponent<Button>();
        _panel = FindDeep(_root.transform, "Panel")?.GetComponent<RectTransform>();
        _foot = FindDeep(_root.transform, "Foot")?.GetComponent<RectTransform>();
    }

    void Wire()
    {
        // 右上角关闭 + 点遮罩 = 同一个出口（非模态，不逼玩家做决定）
        if (_closeBtn != null)
        {
            _closeBtn.onClick.RemoveAllListeners();
            _closeBtn.onClick.AddListener(Close);
        }
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 只<b>填数据 + 克隆行</b>，不碰排版：位置 / 字体 / 图片都是预制体里主人调好的。
    /// </summary>
    void Open()
    {
        // ⚠ 先清掉上一次克隆出来的行，再 BindRefs ——
        //   否则 List 下会同时存在模板与一堆克隆体，查找时容易认错。
        ClearSpawned();
        BindRefs();
        Wire();
        if (_root == null) return;

        FitPanelHeight(FillRows(DraftPool.BuildOddsRows()));

        _root.SetActive(true);
        // 【2026-10-24 主人拍板】每次打开重绑相机 + 重套缩放（与 EvacuateConfirmPopupUI.Open 同构）：
        // 场景来回切换后 RefreshDdolCanvases 只换 worldCamera 不重套缩放，不补这一手瘦长屏会退回预制体的高对齐。
        if (_canvas != null)
            UICanvasSetup.RefreshPopup(_canvas, GameConfig.UiSort.DraftOddsPopup);
    }

    /// <summary>外部口径打开（酒馆招募公示）：同一预制体，行数据由调用方传入。</summary>
    void OpenWith(List<DraftPool.OddsRow> rows)
    {
        ClearSpawned();
        BindRefs();
        Wire();
        if (_root == null) return;

        FitPanelHeight(FillRows(rows));

        _root.SetActive(true);
        // 【2026-10-24 主人拍板】同 Open()：DDOL 弹窗每次打开重绑相机 + 重套缩放。
        if (_canvas != null)
            UICanvasSetup.RefreshPopup(_canvas, GameConfig.UiSort.DraftOddsPopup);
    }

    /// <summary>克隆模板行并填概率数字（Open / OpenWith 共用；段标题文案写死「抽到什么/什么品质」两段）。
    /// 返回克隆出来的内容总高，供 <see cref="FitPanelHeight"/> 收面板高度用。</summary>
    float FillRows(List<DraftPool.OddsRow> rows)
    {
        if (rows == null) return 0f;
        if (_listRoot != null && (_rowTemplate == null || _headTemplate == null))
        {
            Debug.LogError("[DraftOddsPopup] 预制体里缺 RowTemplate / HeadTemplate，无法列出概率行");
            return 0f;
        }
        if (_listRoot == null) return 0f;

        float y = 0f;
        string lastSection = null;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            // 换段就插一个小标题：玩家一眼分清「抽到什么」和「什么品质」
            if (r.Section != lastSection)
            {
                lastSection = r.Section;
                if (y < 0f) y -= SectionGap;
                var head = Clone(_headTemplate, y);
                if (head != null)
                {
                    var ht = head.GetComponentInChildren<Text>();
                    if (ht != null)
                        ht.text = r.Section == DraftPool.SEC_TYPE ? "抽到什么" : "什么品质";
                    y -= head.sizeDelta.y;
                }
            }
            var row = Clone(_rowTemplate, y);
            if (row != null)
            {
                FillRow(row, r);
                y -= row.sizeDelta.y;
            }
        }
        return -y;
    }

    /// <summary>
    /// 按内容收面板高度（2026-10-18 主人反馈「弹窗有点大 东西不多 空间很大」）：
    /// 行数是活的（酒馆招募只有几行，战斗卡池行更多），固定高度必然有一头留大空腔。
    /// 宽度 / 字号 / 上下留白仍归预制体管 —— 这里只把 Panel 高度收到「上下留白 + 内容」，
    /// 页脚贴底距离保持不变；<b>只缩不涨</b>（内容比预制体原高还高时维持原高，行为与旧版一致）。
    /// </summary>
    void FitPanelHeight(float contentH)
    {
        var list = _listRoot as RectTransform;
        if (_panel == null || list == null) return;

        float padTop = -list.offsetMax.y;    // 列表顶部留白（标题 / 副标题占位）
        float padBottom = list.offsetMin.y;  // 列表底部留白（页脚占位）
        float oldH = _panel.sizeDelta.y;
        float newH = Mathf.Clamp(padTop + Mathf.Max(0f, contentH) + padBottom, 240f, oldH);
        if (Mathf.Approximately(newH, oldH)) return;

        _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, newH);
        if (_foot != null)
        {
            // 页脚原本离底边多远，就还离多远（预制体里调了页脚位置这里自动跟上）
            float footFromBottom = oldH + _foot.anchoredPosition.y;
            _foot.anchoredPosition = new Vector2(_foot.anchoredPosition.x, -(newH - footFromBottom));
        }
    }

    /// <summary>一行「名字 ……… 百分比」：左名字、右数字，颜色按品质走（字号/对齐在模板里）。</summary>
    static void FillRow(RectTransform row, DraftPool.OddsRow r)
    {
        var name = FindDeep(row, "Name");
        var pct = FindDeep(row, "Pct");
        var nt = name != null ? name.GetComponent<Text>() : null;
        var pt = pct != null ? pct.GetComponent<Text>() : null;
        if (nt != null)
        {
            nt.text = r.Label;
            nt.color = r.Color;
        }
        if (pt != null)
        {
            pt.text = $"{r.Percent:0.0}%";
            pt.color = r.Color;
        }
    }

    /// <summary>克隆一个模板节点并挂到 List 下（y 为「父顶锚点」的偏移，往下为负）。</summary>
    RectTransform Clone(RectTransform template, float y)
    {
        if (template == null) return null;
        var go = Instantiate(template.gameObject, _listRoot);
        go.SetActive(true);
        _spawned.Add(go);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, y);
        return rt;
    }

    void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i]);
        _spawned.Clear();
    }

    /// <summary>
    /// 关掉自己（实例版）。
    /// ⚠ 不能叫 Hide：外面还有个 <see cref="Hide()"/> 静态入口，同名同参数会报 CS0111。
    /// </summary>
    void Close()
    {
        if (_root == null) return;
        _root.SetActive(false);
        ClearSpawned();
    }

    /// <summary>深度优先找子节点（预制体里层级可能套一层，不能只找直属）。</summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
