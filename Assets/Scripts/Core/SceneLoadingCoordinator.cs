using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 场景 Loading 进度协调：百分比与真实加载阶段对齐，到 100% 并短暂停留后再关闭遮罩。
///
/// 各场景加载内容（权重见 Begin）：
/// · Boot→Town：异步场景 0~55% → 字体/预制体/功能页预热 55~100%
/// · Town→Battle / 回 Town：同上对应目标场景
/// · Battle 进图后：AutoGameInitializer 8 步 + UI 绑定 → 45~100%
/// </summary>
public static class SceneLoadingCoordinator
{
    public enum LoadTarget
    {
        Town,
        Battle
    }

    const float SceneLoadEndTown = 0.55f;
    const float SceneLoadEndBattle = 0.45f;
    const float HoldAt100Seconds = 0.4f;

    static LoadTarget _target;
    static float _progress;
    static bool _active;
    static bool _finishRequested;
    static LoadingProgressRunner _runner;

    public static bool IsActive => _active;
    public static float Progress => _progress;

    public static void Begin(string tip, LoadTarget target)
    {
        _target = target;
        _progress = 0f;
        _active = true;
        _finishRequested = false;
        BattleLoadingOverlay.Show(tip);
        BattleLoadingOverlay.SetProgress(0f);
    }

    /// <summary>绝对进度 0~1，只增不减</summary>
    public static void Report(float progress01)
    {
        if (!_active) return;
        _progress = Mathf.Max(_progress, Mathf.Clamp01(progress01));
        BattleLoadingOverlay.SetProgress(_progress);
    }

    /// <summary>场景 AsyncOperation 进度映射到 [0, sceneEnd]</summary>
    public static void ReportSceneAsync(AsyncOperation op)
    {
        if (!_active || op == null) return;
        float sceneEnd = _target == LoadTarget.Town ? SceneLoadEndTown : SceneLoadEndBattle;
        float p = Mathf.Clamp01(op.progress / 0.9f) * sceneEnd;
        Report(p);
    }

    /// <summary>场景文件加载完成</summary>
    public static void ReportSceneLoaded()
    {
        if (!_active) return;
        Report(_target == LoadTarget.Town ? SceneLoadEndTown : SceneLoadEndBattle);
    }

    /// <summary>场景加载后的初始化分步（step 从 1 开始）</summary>
    public static void ReportPostLoadStep(int step, int total)
    {
        if (!_active || total <= 0) return;
        float start = _target == LoadTarget.Town ? SceneLoadEndTown : SceneLoadEndBattle;
        float span = 1f - start;
        float inner = Mathf.Clamp01((float)step / total);
        Report(start + span * inner);
    }

    public static void Finish()
    {
        if (!_active || _finishRequested) return;
        _finishRequested = true;
        Report(1f);
        EnsureRunner().StartCoroutine(CoHoldThenHide());
    }

    static IEnumerator CoHoldThenHide()
    {
        BattleLoadingOverlay.SetProgress(1f);
        float t = 0f;
        while (t < HoldAt100Seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        BattleLoadingOverlay.Hide();
        _active = false;
        _finishRequested = false;
        _progress = 0f;
    }

    static LoadingProgressRunner EnsureRunner()
    {
        if (_runner != null) return _runner;
        var go = new GameObject("SceneLoadingCoordinator");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<LoadingProgressRunner>();
        return _runner;
    }

    /// <summary>供 DontDestroyOnLoad 上跑协程</summary>
    sealed class LoadingProgressRunner : MonoBehaviour { }
}
