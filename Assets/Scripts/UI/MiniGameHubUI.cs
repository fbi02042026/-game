using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 「小游戏合集」弹窗（礼盒派对）：一块底图上两组横条 + 进入按钮，点进去分别是竹篓纳福 / 逐福狂奔。
///
/// 预制体契约（路径 Resources/Prefabs/UI/MiniGameHubPanel）：
/// ⚠️ 预制体排版与素材已有手工调整；节点契约仅作查找说明，生成器只用于首次建骨架，禁止重跑。
///   MiniGameHubPanel(root: Canvas+CanvasScaler(Match Height 1280)+GraphicRaycaster+MiniGameHubUI)
///   【运行时】Canvas 由 UICanvasSetup.ApplyPopup 接管（ScreenSpaceCamera + 720×1280 参考分辨率 +
///   层级 GameConfig.UiSort.TownPopup），预制体里的初始值只是底稿；不自己写 Camera / ScreenSpaceOverlay。
///   └─ Root
///      ├─ Dim（全屏遮罩，点击关窗，Button 监听运行时补挂）
///      └─ Panel（600×711 底图「弹窗」，原图尺寸；运行时只按设计分辨率等比改 localScale）
///         ├─ CloseBtn   （右上热区，填「关闭」图）
///         ├─ RowBamboo  （横条「竹篓纳福」）
///         ├─ EnterBamboo（进入热区，填「进入按钮」图）
///         ├─ RowRun     （横条「逐福狂奔」）
///         └─ EnterRun   （进入热区，填「进入按钮」图）
///
/// 🔴 prefab 里 6 个 Image 的 sprite 已配好，本类 Bind() 只校验不设图：
/// 缺节点 / 无 Image / 空图一律 Debug.LogError 报「节点名 + 参考 Resources 路径」，
/// 不静默跳过、不运行时补图兜底。横条 / 按钮图自带文字与图形，「进入」文案与子节点一律不碰。
///
/// 🔴 两个小游戏各有 depth=90 的独立相机（BambooBasketGame / EndlessRunGame），
/// 弹窗必须先关再进，禁止并发：EnterBamboo / EnterRun 都是先 Close() 再进本。
/// 跑酷走 RunEntryUI.OnClicked() 唯一出口（CanEnterRun 校验 → TryConsumeEnter 扣次数 → EndlessRunGame.Show），
/// 不裸调 EndlessRunGame.Show() 绕过每日次数闸门。
/// </summary>
public class MiniGameHubUI : MonoBehaviour
{
    public static MiniGameHubUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/MiniGameHubPanel";

    // ── 节点名契约（与 MiniGameHubPrefabBuilder 一一对应）──
    const string NRoot = "Root";
    const string NDim = "Dim";
    const string NPanel = "Panel";
    const string NCloseBtn = "CloseBtn";
    const string NRowBamboo = "RowBamboo";
    const string NEnterBamboo = "EnterBamboo";
    const string NRowRun = "RowRun";
    const string NEnterRun = "EnterRun";

    // ── 美术参考路径（Resources/UI/Art/小游戏/）：只用于缺图时 LogError 指路，不运行时加载设图 ──
    const string ArtDir = "UI/Art/小游戏/";
    const string PPanel = ArtDir + "弹窗";
    const string PClose = ArtDir + "关闭";
    const string PRowBamboo = ArtDir + "竹篓纳福";
    const string PRowRun = ArtDir + "逐福狂奔";
    const string PEnter = ArtDir + "进入按钮";

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

    static MiniGameHubUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不做代码建树兜底：预制体缺失要当场看见。禁止用生成器重建（会抹掉已有的手工排版）
            Debug.LogError("[MiniGameHub] 合集弹窗预制体缺失：Resources/" + PrefabPath +
                "（请恢复该 prefab，不要重跑生成器）");
            return null;
        }
        var go = Object.Instantiate(prefab);
        DontDestroyOnLoad(go);
        Instance = go.GetComponent<MiniGameHubUI>();
        if (Instance == null)
        {
            Debug.LogError("[MiniGameHub] 预制体根上没挂 MiniGameHubUI 组件，请在 prefab 里补挂该脚本");
            Object.Destroy(go);
            return null;
        }
        Instance.Bind();
        // 弹窗屏幕适配统一出口（ScreenSpaceCamera + 绑 UI 相机 + TownPopup 层级）；
        // Ensure 是静态方法，这里用 Instance.GetComponent 取根上的 Canvas，等价 AdMercEventUI 的 GetComponent。
        UICanvasSetup.ApplyPopup(Instance.GetComponent<Canvas>(), GameConfig.UiSort.TownPopup);
        return Instance;
    }

    void Open()
    {
        gameObject.SetActive(true);
        // 每次打开重绑相机 + 重算适配（DDOL 弹窗跨场景 / 分辨率变化后的统一收口，幂等）
        UICanvasSetup.RefreshPopup(GetComponent<Canvas>(), GameConfig.UiSort.TownPopup);
        // 缩放到设计分辨率：从预制体设计尺寸重算 Panel 的 localScale，幂等不累积
        FitPanelUniformScale();
    }

    /// <summary>只隐藏不销毁，重复打开复用同一个实例。</summary>
    void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 按设计分辨率整体等比缩放 Panel：只改 Panel 一层的 localScale，子节点整体跟缩，零拉伸。
    /// 尺寸取预制体设计尺寸，不读 Screen —— 编辑器 Game 视图比例不可信，
    /// CanvasScaler 已把逻辑空间归一，用静态设计值最稳；只写 localScale，不动布局尺寸、
    /// 锚点坐标与 Image 显示方式，也不碰任何子节点坐标。每次打开从设计尺寸重算，幂等不累积。
    /// 600×711 时 s = min(720/600, 1280/711) = 1.2，缩放后 720×853 居中、不裁切；
    /// 瘦屏逻辑高更大时宽度仍是约束，结果一致。
    /// </summary>
    void FitPanelUniformScale()
    {
        var root = Deep(transform, NRoot);
        if (root == null) { Debug.LogError("[MiniGameHub] 缩放失败：缺节点 " + NRoot); return; }
        var panel = Deep(root, NPanel) as RectTransform;
        if (panel == null) { Debug.LogError("[MiniGameHub] 缩放失败：缺节点 " + NPanel + " 或其不是 RectTransform"); return; }

        // 设计尺寸取预制体面板尺寸（scale=1 时的设计值），异常时用 rect 兜底
        Vector2 design = panel.sizeDelta;
        if (design.x < 1f || design.y < 1f)
            design = new Vector2(Mathf.Abs(panel.rect.width), Mathf.Abs(panel.rect.height));
        if (design.x < 1f || design.y < 1f)
        {
            Debug.LogError("[MiniGameHub] 缩放失败：节点 " + NPanel + " 设计尺寸无效（" + design.x + "×" + design.y + "）");
            return;
        }

        // 取宽高比小者整体等比缩放；只写这一层 localScale
        float s = Mathf.Min(GameConfig.DESIGN_WIDTH / design.x, GameConfig.DESIGN_HEIGHT / design.y);
        if (s < 0.05f) s = 0.05f;
        panel.localScale = new Vector3(s, s, 1f);
    }

    // ============================================================
    // 节点绑定（按契约名找节点；sprite 由预制体提供，代码只校验不设图；缺图 LogError 报参考路径）
    // ============================================================

    void Bind()
    {
        var root = Deep(transform, NRoot);
        if (root == null) { Debug.LogError("[MiniGameHub] 缺节点 " + NRoot); return; }

        // 6 个美术节点只校验 sprite 是否在预制体里配好；缺节点 / 无 Image / 空图一律 LogError，
        // 报出参考 Resources 路径便于排查，绝不运行时补图，不改任何显示属性或布局尺寸。
        VerifySpriteDeep(root, NPanel, PPanel);
        VerifySpriteDeep(root, NCloseBtn, PClose);
        VerifySpriteDeep(root, NRowBamboo, PRowBamboo);
        VerifySpriteDeep(root, NRowRun, PRowRun);
        VerifySpriteDeep(root, NEnterBamboo, PEnter);
        VerifySpriteDeep(root, NEnterRun, PEnter);

        // ── onClick 绑定 ──
        // Dim / CloseBtn → 关窗
        WireDeep(root, NDim, Close);
        WireDeep(root, NCloseBtn, Close);

        // 竹篓纳福：先关弹窗再进本（弹窗与小游戏独立相机禁止并发）
        WireDeep(root, NEnterBamboo, OnEnterBamboo);
        // 逐福狂奔：先关弹窗，再走跑酷唯一点击出口（内含每日次数校验 → 扣次数 → 进本）
        WireDeep(root, NEnterRun, OnEnterRun);
    }

    void OnEnterBamboo()
    {
        Close();
        BambooBasketGame.Show();
    }

    void OnEnterRun()
    {
        Close();
        RunEntryUI.OnClicked();
    }

    /// <summary>按节点名找 Image，只校验预制体里是否已配好 sprite；缺节点 / 无 Image / 空图
    /// 一律 LogError 报「节点名 + 参考 Resources 路径」，不设图、不改任何属性、不运行时补图。</summary>
    static void VerifySpriteDeep(Transform root, string nodeName, string path)
    {
        var t = Deep(root, nodeName);
        if (t == null)
        {
            Debug.LogError("[MiniGameHub] 缺节点 " + nodeName + "（参考 Resources 路径：" + path + "）");
            return;
        }
        var img = t.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("[MiniGameHub] 节点 " + nodeName + " 上没有 Image 组件（参考 Resources 路径：" + path + "）");
            return;
        }
        if (img.sprite == null)
            Debug.LogError("[MiniGameHub] 节点 " + nodeName + " 的 Image.sprite 为空，请在预制体里拖入图（参考 Resources 路径：" + path + "）");
    }

    static void WireDeep(Transform root, string nodeName, UnityAction action)
    {
        var t = Deep(root, nodeName);
        if (t == null) { Debug.LogError("[MiniGameHub] 缺节点 " + nodeName); return; }
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
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
