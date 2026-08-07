using UnityEngine;

/// <summary>
/// 挂在 BattleUI：Game 视图切分辨率时重算适配，避免只在启动时算一次。
/// </summary>
public class ViewportFitDriver : MonoBehaviour
{
    int _lastW;
    int _lastH;

    void OnEnable()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        ApplyNow();
    }

    void LateUpdate()
    {
        if (Screen.width == _lastW && Screen.height == _lastH) return;
        _lastW = Screen.width;
        _lastH = Screen.height;
        ApplyNow();
    }

    void ApplyNow()
    {
        Camera cam = Camera.main;
        Canvas root = GetComponentInParent<Canvas>();
        if (root == null) root = GetComponent<Canvas>();
        BattleViewportFit.Apply(cam, root);
    }
}
