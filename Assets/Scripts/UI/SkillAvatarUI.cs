using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 技能头像UI：圆形头像 + 底部能量槽 + 能量满时金色光边
/// </summary>
[System.Serializable]
public class SkillAvatarUI
{
    public GameObject root;           // 根对象
    public Image avatarImage;         // 圆形头像（Mask裁剪）
    public Image energyRing;          // 能量环（圆形填充）
    public Image glowBorder;          // 光边（能量满时显示）
    public Text cooldownText;         // 冷却倒计时文字
    /// <summary>底部充能细条（新底部布局运行时补建）。</summary>
    public Image energyFill;
    /// <summary>槽位底字（如临时 UI 的「被动」），有技能图标时隐藏。</summary>
    public Text labelText;

    [System.NonSerialized]
    public System.Action onClick;     // 点击回调

    private bool _isReady = false;

    public bool IsReadyPulse => _isReady;

    /// <summary>
    /// 设置能量比例 0~1
    /// </summary>
    public void SetEnergy(float ratio)
    {
        if (energyRing != null)
        {
            energyRing.fillAmount = Mathf.Clamp01(ratio);
        }

        bool ready = ratio >= 1f;
        if (ready != _isReady)
        {
            _isReady = ready;
            if (glowBorder != null)
            {
                glowBorder.gameObject.SetActive(ready);
                if (ready)
                    glowBorder.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            }
        }
    }

    public void TickReadyPulse()
    {
        if (!_isReady || glowBorder == null || !glowBorder.gameObject.activeSelf) return;
        float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
        var c = glowBorder.color;
        c.r = 1f;
        c.g = 0.85f;
        c.b = 0.15f;
        c.a = a;
        glowBorder.color = c;
    }

    /// <summary>
    /// 设置冷却文字
    /// </summary>
    public void SetCooldownText(string text)
    {
        if (cooldownText != null)
        {
            cooldownText.text = text;
            cooldownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    /// <summary>
    /// 设置头像图片（技能头像区圆形头像）
    /// </summary>
    public void SetAvatar(Sprite icon)
    {
        if (avatarImage != null)
        {
            avatarImage.preserveAspect = true;
            avatarImage.sprite = icon;
            avatarImage.gameObject.SetActive(icon != null);
        }
    }

    /// <summary>底字（「被动」这类占位标签）显隐：有真图标时藏起来。</summary>
    public void SetLabelVisible(bool visible)
    {
        if (labelText != null) labelText.gameObject.SetActive(visible);
    }

    /// <summary>充能比例 0~1（走底部细条，不依赖美术预设的能量环）。</summary>
    public void SetEnergyFill(float ratio)
    {
        if (energyFill != null) energyFill.fillAmount = Mathf.Clamp01(ratio);
    }
}
