#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性脚手架（2026-10-15 主人拍板）：生成「酒馆 · 佣兵抽奖池」预制体。
/// 用法：菜单 Tools → Build MercDrawPoolUI Prefab。
///
/// ⚠ 本生成器与 <see cref="EquipReplaceConfirmPrefabBuilder"/> 同性质：跑完即过时。
/// 预制体生成后若已手工调整过排版，禁止再跑（会抹掉手工节点，重签 guid 风险）。
/// 改界面请直接改 prefab YAML 或改 <see cref="MercDrawPoolUI"/> 的建树逻辑后重新评估。
///
/// 生成内容 = MercDrawPoolUI.BuildIfNeeded 的完整结构（Root/Dim/Panel/招募池 6 格/
/// 已获得+未获得滚动区/替换弹窗），根带 Canvas（Screen Space - Camera）+ CanvasScaler。
/// 按钮的匿名监听不序列化，实例化后由 MercDrawPoolUI.WireClicks 重绑。
/// </summary>
public static class MercDrawPoolPrefabBuilder
{
    const string Path = "Assets/Resources/Prefabs/Town/MercDrawPoolUI.prefab";

    [MenuItem("Tools/Build MercDrawPoolUI Prefab")]
    public static void Build()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var go = new GameObject("MercDrawPoolUI", typeof(RectTransform));
        var ui = go.AddComponent<MercDrawPoolUI>();

        // 复用运行时建树逻辑（private BuildIfneeded）
        var mi = typeof(MercDrawPoolUI).GetMethod("BuildIfNeeded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (mi == null)
        {
            Debug.LogError("[MercDrawPoolPrefabBuilder] BuildIfNeeded not found");
            Object.DestroyImmediate(go);
            return;
        }
        mi.Invoke(ui, null);

        // ---- 标准件对齐（不依赖 UICanvasSetup.ApplyPopup 的副作用）----
        // 坑（2026-10-15 实测）：编辑器里没有运行时相机，Canvas 处于
        // 「Screen Space - Camera + worldCamera 空」时，Unity 保存 prefab 会把它
        // **规范化成 World Space**（m_RenderMode 2），并清掉 overrideSorting、
        // 丢掉 CanvasScaler —— 结果就是「看着缺组件 / 不是摄像机模式」。
        // 所以这里显式写死与角色界面（CharacterUI.prefab）一致的标准件。
        ApplyStandardCanvasSetup(go);

        // 按压反馈不进 prefab（项目口径：运行时自动挂载，不改 prefab）
        var fbs = go.GetComponentsInChildren<UiButtonPressFeedback>(true);
        for (int i = 0; i < fbs.Length; i++)
            if (fbs[i] != null) Object.DestroyImmediate(fbs[i]);

        var saved = PrefabUtility.SaveAsPrefabAsset(go, Path);
        Object.DestroyImmediate(go);

        // 复核：保存过程可能再次规范化，逐项比对，不一致就地修正后重存
        FixIfNeeded(Path);

        if (saved != null)
            Debug.Log("[MercDrawPoolPrefabBuilder] saved: " + Path);
        else
            Debug.LogError("[MercDrawPoolPrefabBuilder] save failed: " + Path);
    }

    /// <summary>
    /// 对齐角色界面：Canvas（Screen Space - Camera / planeDistance 100 / Default 层 / overrideSorting）
    /// + CanvasScaler（720×1280 Match Width Or Height）+ GraphicRaycaster，三件套齐整。
    /// </summary>
    static void ApplyStandardCanvasSetup(GameObject go)
    {
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = null;              // 场景引用存不进 prefab，运行时 RefreshPopup 重绑
        canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GameConfig.UiSort.TownPopup;
        canvas.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
    }

    /// <summary>保存后复核 + 修正（Unity 在无相机的编辑态可能把 Camera 模式规范化掉）。</summary>
    static void FixIfNeeded(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        bool changed = false;

        var canvas = root.GetComponent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                changed = true;
            }
            if (canvas.worldCamera != null)
            {
                canvas.worldCamera = null;
                changed = true;
            }
            if (Mathf.Abs(canvas.planeDistance - GameConfig.UI_PLANE_DISTANCE) > 0.01f)
            {
                canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;
                changed = true;
            }
            if (!canvas.overrideSorting || canvas.sortingOrder != GameConfig.UiSort.TownPopup)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = GameConfig.UiSort.TownPopup;
                changed = true;
            }
        }

        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = root.AddComponent<CanvasScaler>();
            changed = true;
        }
        if (Mathf.Abs(scaler.matchWidthOrHeight - 1f) > 0.001f)
        {
            scaler.matchWidthOrHeight = 1f;
            changed = true;
        }
        if (root.GetComponent<GraphicRaycaster>() == null)
        {
            root.AddComponent<GraphicRaycaster>();
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[MercDrawPoolPrefabBuilder] fixed canvas setup: " + path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }
}

#endif
