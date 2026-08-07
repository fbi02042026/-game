using UnityEngine;

/// <summary>
/// 传送门动画组件
/// 挂在EndPoint（传送门）上，让传送门有旋转/脉动效果
/// 打完所有怪后由 BattleManager.ActivatePortal() 激活
/// </summary>
public class PortalAnimator : MonoBehaviour
{
    [Header("动画参数")]
    public float rotateSpeed = 60f;      // 旋转速度（度/秒）
    public float pulseSpeed = 2f;        // 脉动速度
    public float pulseScale = 1.2f;      // 脉动最大缩放倍率

    Transform _ring;       // 旋转环（可选）
    Transform _glow;       // 光晕（可选）
    Vector3 _baseScale;
    float _time;

    void Awake()
    {
        _baseScale = transform.localScale;
        // 查找子节点作为旋转环（如果存在）
        Transform child = transform.childCount > 0 ? transform.GetChild(0) : null;
        _ring = child;
        _glow = transform;
    }

    void Update()
    {
        _time += Time.deltaTime;

        // 旋转环（如果有子节点则旋转子节点）
        if (_ring != null && _ring != transform)
        {
            _ring.Rotate(0, 0, rotateSpeed * Time.deltaTime);
        }
        else
        {
            // 没有子环时整体轻微旋转
            transform.Rotate(0, 0, rotateSpeed * 0.3f * Time.deltaTime);
        }

        // 脉动缩放（整体呼吸效果）
        float pulse = 1f + (Mathf.Sin(_time * pulseSpeed) * 0.5f + 0.5f) * (pulseScale - 1f);
        transform.localScale = _baseScale * pulse;
    }
}
