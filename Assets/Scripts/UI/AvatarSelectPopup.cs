using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 头像选择弹窗（2026-10-25 主人需求）。
/// 预制体：Resources/Prefabs/Town/头像选择框（主人手摆；meta 是 56 字符 guid，只走路径加载）。
/// 结构：根（带 Canvas，prefab scale=0 需运行时扶正）→ Dim（全屏黑底+按钮=点空白关）
///       + panl（Tile 标题 / 头像1-5、7、8 七格（6 号位主人没摆）/ 确定钮（scale 0.4））。
/// 行为：点头像选中（金框高亮；未解锁置灰不可选）→ 点确定换头像并存档 → 主界面名牌 Icon 同步刷新。
/// 排版：预制体里 7 格全叠在同一个位置（主人原样），本类 Build 时按下述网格铺开；
///       只动 anchoredPosition / localScale，不改 sizeDelta。要调间距/缩放只改本文件常量。
/// 头像图与解锁规则真源：<see cref="AvatarDefs"/>（图 = Resources/Art/UI/Main/头像/）。
/// </summary>
public class AvatarSelectPopup : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Town/头像选择框";
    const string LogTag = "[AvatarSelect] ";

    // === 网格排版（panl 450×350 局部系；格原尺寸 117×109，缩放 0.7 后 81.9×76.3）===
    // 纵向带宽：Tile 标题文字下沿 ≈ +129.7，确定钮（scale 0.4）上沿 ≈ -30.4，
    // 两行正好塞进 ±(行高+缝)，行1顶贴标题、行2底贴确定钮。
    const float CellScale = 0.7f;
    const float Row1Y = 92f;
    const float Row2Y = 7f;
    const float ColStep = 88f;      // 列间距 = 缩放后格宽 81.9 + 6 缝
    // 【2026-10-25 主人反馈「弹窗太小」】panl 整体放大倍率：标题/格子/确定钮一起大，
    // 只动 localScale、内部相对排版不变（Dim 全屏黑底在 panl 外，不受影响）；要再调只改这一个常量。
    const float PanelScale = 1.4f;

    public static AvatarSelectPopup Instance { get; private set; }

    readonly Dictionary<int, Image> _cellImages = new Dictionary<int, Image>();
    readonly Dictionary<int, Outline> _cellOutlines = new Dictionary<int, Outline>();
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
        Instantiate(prefab);
    }

    void Awake()
    {
        Instance = this;
        // 预制体根 scale=(0,0)（主人保存原样），不扶正永远不可见；顺带套全项目 Canvas 规范
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
            Debug.LogError(LogTag + "预制体缺 panl 节点，头像摆不出来（不兜底）");
            return;
        }

        // Dim：全屏黑底按钮（预制体已带 Button），点空白 = 取消关闭
        var dim = FindDeep(transform, "Dim");
        var dimBtn = dim != null ? dim.GetComponent<Button>() : null;
        if (dimBtn != null) dimBtn.onClick.AddListener(Close);
        else Debug.LogError(LogTag + "预制体 Dim 缺 Button，点空白无法关闭弹窗");

        // 确定：预制体只有 Image，运行时补 Button 接线（同「战斗失败确定钮」口径）
        var okT = FindDeep(panl, "确定");
        if (okT != null)
        {
            var okBtn = okT.GetComponent<Button>();
            if (okBtn == null) okBtn = okT.gameObject.AddComponent<Button>();
            okBtn.transition = Selectable.Transition.None;
            okBtn.onClick.AddListener(OnConfirm);
        }
        else Debug.LogError(LogTag + "预制体缺「确定」节点，选完头像没法保存");

        // 【2026-10-25 主人反馈「弹窗太小」】panl 整体放大（Dim 全屏黑底在 panl 外，不受影响）
        panl.localScale = Vector3.one * PanelScale;

        // 七格头像：节点名 = 槽位号（头像1..8；6 号位无节点无图属设计缺口，跳过不报错）。
        // 【2026-10-25 主人反馈「头像数量没有适配」】先收集真实存在的节点再排版：
        // 两行均分（上行 ceil(n/2)、下行其余），按槽位顺序居中铺开——
        // 以后主人增删头像节点，网格自动跟随不用改代码（7 格时 = 行1 四列 + 行2 三列，与原版一致）。
        var layoutSlots = new List<int>();
        for (int slot = AvatarDefs.MinSlot; slot <= AvatarDefs.MaxSlot; slot++)
        {
            if (FindDeep(panl, "头像" + slot) != null) layoutSlots.Add(slot);
            else if (AvatarDefs.HasSprite(slot))
                Debug.LogError(LogTag + "预制体缺节点 头像" + slot + "，该头像不显示（不兜底）");
        }
        int row1Count = (layoutSlots.Count + 1) / 2;

        for (int li = 0; li < layoutSlots.Count; li++)
        {
            int slot = layoutSlots[li];
            var node = FindDeep(panl, "头像" + slot);

            // 排版（主人点名的活）：两行网格居中铺开
            bool row1 = li < row1Count;
            int cols = row1 ? row1Count : layoutSlots.Count - row1Count;
            int col = row1 ? li : li - row1Count;
            var rt = (RectTransform)node;
            rt.anchoredPosition = new Vector2((col - (cols - 1) * 0.5f) * ColStep, row1 ? Row1Y : Row2Y);
            rt.localScale = Vector3.one * CellScale;

            // 图：路径主动设图（预制体 sprite 引用是悬空的旧 hex32，不依赖）
            var img = node.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogError(LogTag + "节点 头像" + slot + " 缺 Image，摆不了图（不兜底）");
                continue;
            }
            var sp = AvatarDefs.LoadSprite(slot);
            if (sp != null) img.sprite = sp;
            else Debug.LogError(LogTag + "头像图取不到：槽位 " + slot +
                                "（应在 Resources/" + "Art/UI/Main/头像/ 下），保持预制体原样");

            // 未解锁：置灰 + 点了只提示；已解锁：加金框（选中时开）
            bool unlocked = AvatarDefs.IsUnlocked(slot);
            if (!unlocked) img.color = new Color(0.42f, 0.42f, 0.42f, 1f);
            var outline = node.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.83f, 0.3f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var btn = node.GetComponent<Button>();
            if (btn == null) btn = node.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int captured = slot;
            btn.onClick.AddListener(() => OnCellClicked(captured));

            _cellImages[slot] = img;
            _cellOutlines[slot] = outline;
        }

        // 初始选中 = 当前存档头像
        Select(AvatarDefs.CurrentSlot());
    }

    void OnCellClicked(int slot)
    {
        if (!AvatarDefs.HasSprite(slot)) return;
        if (!AvatarDefs.IsUnlocked(slot))
        {
            Debug.Log(LogTag + "头像 " + slot + " 未解锁（需获得对应佣兵），不可选择");
            return;
        }
        Select(slot);
    }

    /// <summary>选中效果：金框只亮当前格（Outline 开关），不动颜色/缩放，预制体原样保持。</summary>
    void Select(int slot)
    {
        _selected = slot;
        foreach (var kv in _cellOutlines)
            kv.Value.enabled = kv.Key == slot;
    }

    void OnConfirm()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError(LogTag + "存档取不到（SaveSystem 未就绪），头像不保存");
            return;
        }
        if (!AvatarDefs.IsUnlocked(_selected))
        {
            Debug.LogError(LogTag + "确认的头像 " + _selected + " 未解锁，不保存（异常点击，不兜底）");
            return;
        }
        data.playerAvatar = _selected;
        SaveSystem.Instance.Save();
        // 主界面名牌 Icon 同步刷新（GuildHallUI.RefreshNameplate 按存档槽位设图）
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
