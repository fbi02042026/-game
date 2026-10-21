using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 阵亡佣兵头像上的重生护符闪烁（2026-10-21 主人需求「一闪一闪的」）：
/// alpha 呼吸 + 轻微缩放，纯视觉，不接任何逻辑；用 unscaledTime（战斗暂停/冻场时也照闪）。
/// </summary>
public class ReviveCharmBlink : MonoBehaviour
{
    Image _img;
    const float Speed = 5f;          // 闪烁频率（rad/s）
    const float MinAlpha = 0.35f;    // 最暗透明度
    const float ScaleAmp = 0.08f;    // 缩放幅度（±8%）

    public void Bind(Image img) { _img = img; }

    void Update()
    {
        if (_img == null) return;
        float t = (Mathf.Sin(Time.unscaledTime * Speed) + 1f) * 0.5f;   // 0~1 往复
        var c = _img.color;
        c.a = Mathf.Lerp(MinAlpha, 1f, t);
        _img.color = c;
        transform.localScale = Vector3.one * (1f + ScaleAmp * t);
    }
}
