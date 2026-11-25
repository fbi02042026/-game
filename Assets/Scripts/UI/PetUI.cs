using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物界面（2026-11-21 主人手工搭好 prefab 后数据化）。
///
/// 预制体：Resources/Prefabs/Pet/PetUI（主人手工搭，禁改节点位置/尺寸/锚点/颜色——铁律 20/24）。
/// 本脚本只做四件事：① 替换图片 ② 替换文字 ③ 运行时补交互(Button/ScrollRect) ④ 整体等比缩放。
/// 一律不写 anchoredPosition / sizeDelta / anchorMin/Max / Image.type（除进度条 fillAmount 属点名需求）。
///
/// 结构（只读，来自 prefab）：
///   PetUI(Canvas SCamera) → Root → Panel
///     → TitlePlate/TitleText, CloseBtn
///     → PetList/Slot0..4（每格: Slot0/Portrait · 选中 · LockIcon；10 只时克隆补行）
///     → Stage/皮肤(Slot0/Portrait · LockIcon · NameText) · NamePlate/NameText · PetArt(空)
///            · DeployBtn/Label · 好感进度/爱心1..3(各带「空心」)
///     → InfoPanel/InfoTitle · HpText · AtkText · DefText · PowerText · Star0..3
///     → SkillPanel/SkillTitle · SkillInfo · SkillSlot0
///
/// 契约（PetDefs 是唯一数据表；PetSave 经 SaveData 读写）：
///   选中列表项 → 详情刷新 → 按钮态(上阵/喂养/解锁)；皮肤点击 → DiamondSpendGate → 立即切换并同步技能。
/// </summary>
public partial class PetUI : MonoBehaviour
{
    const string TAG = "[PetUI]";
    public const string PrefabPath = "Prefabs/Pet/PetUI";

    public static bool IsOpen { get; private set; }
    static PetUI _instance;

    // ---- 宠物功能引导只读出口（2026-11-22）：高亮锚点 + 叫声气泡定位，不改任何节点 ----
    /// <summary>上阵/喂养按钮（引导高亮用）。</summary>
    internal static RectTransform DeployBtnRect => _instance != null ? _instance._deployBtn as RectTransform : null;
    /// <summary>宠物模型展示节点（气泡浮在其上方）。</summary>
    internal static Transform PetArtAnchor => _instance != null ? _instance._petArt : null;

    // ── 绑定（Bind.cs 填充）──
    Transform _panel;
    Button _closeBtn;

    /// <summary>打开宠物界面（唯一入口）。重复调用幂等。</summary>
    public static void Open()
    {
        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            IsOpen = true;   // 【2026-11-25】与 Close 对称：复用实例重开必须恢复开合状态
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(TAG + "缺预制体 Resources/" + PrefabPath + "（宠物界面打不开）");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "PetUI";
        var ui = go.GetComponent<PetUI>() ?? go.AddComponent<PetUI>();
        ui.Bootstrap();
    }

    public static void Close()
    {
        if (_instance == null) return;
        _instance.gameObject.SetActive(false);
        // 【2026-11-25 主人报「喂养后退出，狗的气泡跑到角色页主角头上」】关闭必须同步开合状态，
        // 否则 IsOpen 恒 true，PetFeatureGuide.PlayTwoCries 的「玩家已退出就收尾」永远不生效。
        IsOpen = false;
    }

    void Bootstrap()
    {
        _instance = this;
        IsOpen = true;

        var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
        // 根节点 scale=0 是 prefab 里主人临时收起的初值，运行时归一（不碰任何子节点位置）
        var rootRt = transform as RectTransform;
        if (rootRt != null && rootRt.localScale == Vector3.zero) rootRt.localScale = Vector3.one;

        BindAll();       // Bind.cs
        FitPanel();      // Fit.cs —— 只设 Panel.localScale
        SetupList();     // List.cs —— 运行时补 ScrollRect + 克隆行
        BindClicks();    // Bind.cs —— 关闭/皮肤/上阵等按钮
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnsureDebugToggle();   // Debug.cs —— 测试全解锁开关（正式包不含）
#endif
        SelectDefault(); // List.cs

        RefreshAll();    // Detail.cs
    }

    void OnDestroy()
    {
        if (_skinShakeCo != null) { StopCoroutine(_skinShakeCo); _skinShakeCo = null; }
        DisposeArtStage();
        if (_instance == this) { _instance = null; IsOpen = false; }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update() => DebugHotkey();   // Debug.cs —— F8 切换测试全解锁
#endif

    /// <summary>全量刷新（列表 + 详情 + 按钮）。解锁/上阵/喂养/换肤后统一调这个。</summary>
    void RefreshAll()
    {
        RefreshList();
        RefreshDetail();
        // 【2026-11-25】解锁 / 上阵 / 喂养都会改「可解锁、可喂养」状态 → 顺带刷角色页宠物入口红点
        RefreshEntryRedDot();
    }

    // Find 深度查找（prefab 同步工具）；找不到打日志不静默
    static Transform FindDeep(Transform root, string path)
    {
        if (root == null) return null;
        var t = root.Find(path);
        if (t != null) return t;
        // 逐级找（兼容部分节点名重复）
        var segs = path.Split('/');
        Transform cur = root;
        for (int i = 0; i < segs.Length; i++)
        {
            var next = cur.Find(segs[i]);
            if (next == null)
            {
                // 最后手段：遍历所有子孙按名字匹配
                next = FindByName(cur, segs[i]);
            }
            if (next == null) return null;
            cur = next;
        }
        return cur;
    }

    static Transform FindByName(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
            var deep = FindByName(c, name);
            if (deep != null) return deep;
        }
        return null;
    }

    static Text GetText(Transform t) => t != null ? t.GetComponent<Text>() : null;
    static Image GetImage(Transform t) => t != null ? t.GetComponent<Image>() : null;
    static Button GetButton(Transform t) => t != null ? t.GetComponent<Button>() : null;
}
