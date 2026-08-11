using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在图标下（或由 RedDot.Bind 自动创建）。显示 主界面_0001_红点。
/// 推荐：不在每个图标手摆图，统一用 RedDot.Bind / 本组件 + key。
/// </summary>
[DisallowMultipleComponent]
public class UIRedDot : MonoBehaviour
{
    [Tooltip("与 RedDot.Set(key) 对应，如 mail / nav.tavern")]
    public string key = RedDot.Mail;
    [Tooltip("若节点上还没有 Image，是否自动补齐红点图")]
    public bool autoCreate = true;
    public float size = 22f;
    public Vector2 offset = new Vector2(-6f, -6f);

    Image _image;

    void Awake()
    {
        EnsureVisual();
        RedDot.Register(this);
    }

    void OnEnable() => Refresh();

    void OnDestroy() => RedDot.Unregister(this);

    void EnsureVisual()
    {
        _image = GetComponent<Image>();
        if (_image == null && autoCreate)
            _image = gameObject.AddComponent<Image>();
        if (_image != null)
        {
            if (_image.sprite == null)
                _image.sprite = RedDot.Sprite;
            _image.raycastTarget = false;
            _image.preserveAspect = true;
        }

        var rt = transform as RectTransform;
        if (rt != null)
        {
            if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = offset;
            }
        }
    }

    public void Refresh()
    {
        if (_image == null) EnsureVisual();
        bool on = RedDot.Get(key);
        if (_image != null)
            _image.enabled = on;
        else if (gameObject.activeSelf != on)
            gameObject.SetActive(on);
    }
}
