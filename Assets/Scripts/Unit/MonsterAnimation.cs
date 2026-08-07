using UnityEngine;

/// <summary>
/// 怪物站立动画：以底部为基准上下拉伸的呼吸/待机动态
/// 怪物只会站着，不做移动动画
/// </summary>
public class MonsterAnimation : MonoBehaviour
{
    [Header("拉伸参数")]
    public float stretchAmount = 0.05f;  // 拉伸幅度（5%）
    public float stretchSpeed = 2f;      // 拉伸速度

    private SpriteRenderer _sr;
    private Vector3 _baseScale;
    private Vector3 _basePosition;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = GetComponentInChildren<SpriteRenderer>();

        if (_sr != null)
        {
            _baseScale = _sr.transform.localScale;
            _basePosition = _sr.transform.localPosition;
        }
    }

    void Update()
    {
        if (_sr == null) return;

        // 使用正弦波做上下拉伸，底部固定
        float t = Mathf.Sin(Time.time * stretchSpeed) * 0.5f + 0.5f;
        float scaleY = Mathf.Lerp(1f - stretchAmount, 1f + stretchAmount, t);
        float scaleX = Mathf.Lerp(1f + stretchAmount * 0.5f, 1f - stretchAmount * 0.5f, t);

        _sr.transform.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);

        // 底部固定：调整Y位置补偿缩放
        float heightDiff = (_baseScale.y * scaleY - _baseScale.y) * 0.5f;
        _sr.transform.localPosition = new Vector3(_basePosition.x, _basePosition.y + heightDiff, _basePosition.z);
    }

    void OnEnable()
    {
        if (_sr != null)
        {
            _baseScale = _sr.transform.localScale;
            _basePosition = _sr.transform.localPosition;
        }
    }
}