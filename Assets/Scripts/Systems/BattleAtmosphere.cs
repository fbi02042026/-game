using System;
using UnityEngine;

// 战斗昼夜/天气 —— 全局枚举。T2 唯一定义处；并行任务按清单 §3 契约共用，勿在别处重复定义。
public enum DayPhase { Day, Dusk, Night }
public enum WeatherType { Clear, Rain, Snow }

/// <summary>
/// 战斗昼夜/天气唯一状态源。非 DDOL：随 Battle 场景销毁，清事件订阅。
/// 进关由 BattleManager.LoadStage 调用一次 ApplyForStage；其余系统只读 Phase/Weather/LightRadiusWorld 或订阅 OnChanged。
/// </summary>
public class BattleAtmosphere : MonoBehaviour
{
    static BattleAtmosphere _instance;
    static bool _nightToastDone;

    /// <summary>战斗期按需查找/装配；非 DDOL，随 Battle 场景销毁。</summary>
    public static BattleAtmosphere Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<BattleAtmosphere>();
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject("BattleAtmosphere");
                _instance = go.AddComponent<BattleAtmosphere>();
            }
            return _instance;
        }
    }

    public DayPhase Phase { get; private set; } = DayPhase.Day;
    public WeatherType Weather { get; private set; } = WeatherType.Clear;

    /// <summary>视野半径（世界单位）唯一真源：T3 遮罩挖洞与 T4 显隐判定共用此值。主人按手感调这一个数即可。</summary>
    public float LightRadiusWorld = 2.88f;

    public event Action<DayPhase, WeatherType> OnChanged;

    public void ApplyForStage(DayPhase phase, WeatherType weather)
    {
        Phase = phase;
        Weather = weather;
        if (OnChanged != null) OnChanged(Phase, Weather);
        // T4 视野控制器随进关装配并绑定（懒加载，非 DDOL，随 Battle 场景销毁）。
        NightVisionController.Instance.Bind(this);
    }

    /// <summary>
    /// 波清推进昼夜：仅黄昏起始关、清完第 1 波且还有后续波次时，从 Dusk 渐变到 Night（一次）。
    /// 其余相位（Day/Night）与天气一律不动；已是 Night 时不再触发，天然幂等。
    /// </summary>
    public void OnWaveCleared(int clearedWaves, int totalWaves)
    {
        if (Phase != DayPhase.Dusk) return;
        if (clearedWaves < 1 || clearedWaves >= totalWaves) return;
        Phase = DayPhase.Night;
        if (!_nightToastDone)
        {
            _nightToastDone = true;
            UIManager.Instance?.ShowToast("黑夜降临，小心暗处的敌人。", false);
        }
        if (OnChanged != null) OnChanged(Phase, Weather);
    }

    void OnDestroy()
    {
        OnChanged = null;
        if (_instance == this) _instance = null;
    }
}
