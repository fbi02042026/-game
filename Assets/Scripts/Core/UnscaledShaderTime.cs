using UnityEngine;

/// <summary>
/// 全局着色器时间：每帧向所有着色器写入不受 Time.timeScale 影响的 _UnscaledTime。
/// 结算界面打开时会把 Time.timeScale 设为 0 定格战斗场景，此时需要该变量驱动
/// Shine 流光等必须继续播放的效果（着色器里 _Time 会随 timeScale 一起冻结）。
/// </summary>
public class UnscaledShaderTime : MonoBehaviour
{
    static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("UnscaledShaderTime");
        DontDestroyOnLoad(go);
        go.AddComponent<UnscaledShaderTime>();
    }

    void Update()
    {
        Shader.SetGlobalFloat(UnscaledTimeId, Time.unscaledTime);
    }
}
