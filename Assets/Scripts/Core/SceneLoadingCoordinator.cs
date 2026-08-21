using System.Collections;
using UnityEngine;

/// <summary>
/// 场景 Loading 进度协调：真实进度只增不减；显示进度在等待时缓慢爬向 99%，Finish 后再到 100%。
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
    const float DisplayCap = 0.99f;
    const float HoldAt100Seconds = 0.4f;
    /// <summary>距 99% 越近爬得越慢，长时间等待也不会一直钉在同一数字</summary>
    const float CreepFactor = 0.32f;

    static LoadTarget _target;
    static float _realProgress;
    static float _displayProgress;
    static bool _active;
    static bool _finishRequested;
    static bool _tickRunning;
    static LoadingProgressRunner _runner;

    public static bool IsActive => _active;
    public static float Progress => _displayProgress;

    public static void Begin(string tip, LoadTarget target)
    {
        _target = target;
        _realProgress = 0f;
        _displayProgress = 0f;
        _active = true;
        _finishRequested = false;
        string storyTip = string.IsNullOrEmpty(tip) ? LoadingTips.Pick(target) : tip;
        // Loading 期间关掉 BGM，结束后再按目标场景淡入
        GameBgm.MuteForLoading();
        GameBgm.SetPending(target == LoadTarget.Town ? GameBgm.Track.Town : GameBgm.Track.Battle);
        BattleLoadingOverlay.Show(storyTip);
        BattleLoadingOverlay.SetProgress(0f);
        StartDisplayTick();
    }

    public static void Begin(LoadTarget target) => Begin(null, target);

    /// <summary>绝对进度 0~1，只增不减（驱动显示下限）</summary>
    public static void Report(float progress01)
    {
        if (!_active) return;
        _realProgress = Mathf.Max(_realProgress, Mathf.Clamp01(progress01));
    }

    public static void ReportSceneAsync(AsyncOperation op)
    {
        if (!_active || op == null) return;
        float sceneEnd = _target == LoadTarget.Town ? SceneLoadEndTown : SceneLoadEndBattle;
        float p = Mathf.Clamp01(op.progress / 0.9f) * sceneEnd;
        Report(p);
    }

    public static void ReportSceneLoaded()
    {
        if (!_active) return;
        Report(_target == LoadTarget.Town ? SceneLoadEndTown : SceneLoadEndBattle);
    }

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
        _realProgress = 1f;
    }

    static void StartDisplayTick()
    {
        if (_tickRunning) return;
        _tickRunning = true;
        EnsureRunner().StartCoroutine(CoDisplayTick());
    }

    static IEnumerator CoDisplayTick()
    {
        float holdT = 0f;
        bool holdingAt100 = false;

        while (_active)
        {
            float dt = Time.unscaledDeltaTime;

            if (!holdingAt100)
            {
                if (!_finishRequested)
                {
                    _displayProgress = Mathf.Max(_realProgress, _displayProgress);
                    if (_displayProgress < DisplayCap)
                    {
                        float room = DisplayCap - _displayProgress;
                        _displayProgress += room * CreepFactor * dt;
                        _displayProgress = Mathf.Min(DisplayCap, _displayProgress);
                    }
                }
                else
                {
                    _displayProgress = Mathf.MoveTowards(_displayProgress, 1f, dt * 1.8f);
                    if (_displayProgress >= 0.999f)
                    {
                        _displayProgress = 1f;
                        holdingAt100 = true;
                        holdT = 0f;
                    }
                }

                BattleLoadingOverlay.SetProgress(_displayProgress);
            }
            else
            {
                holdT += dt;
                if (holdT >= HoldAt100Seconds)
                {
                    BattleLoadingOverlay.Hide();
                    // 记住目标场景默认曲，Unmute 时淡入
                    GameBgm.UnmuteAfterLoading();
                    _active = false;
                    _finishRequested = false;
                    _realProgress = 0f;
                    _displayProgress = 0f;
                    _tickRunning = false;
                    yield break;
                }
            }

            yield return null;
        }

        _tickRunning = false;
    }

    static LoadingProgressRunner EnsureRunner()
    {
        if (_runner != null) return _runner;
        var go = new GameObject("SceneLoadingCoordinator");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<LoadingProgressRunner>();
        return _runner;
    }

    sealed class LoadingProgressRunner : MonoBehaviour { }
}
