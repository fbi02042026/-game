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
    /// <summary>槽位底字（原临时 UI 的「被动」占位），现在改成显示技能名。</summary>
    public Text labelText;
    /// <summary>右下角等级文字（美术在每个技能槽下加了 level 节点，没有就运行时补建）。</summary>
    public Text levelText;

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

    /// <summary>底字（原来是「被动」占位，现在显示技能名）显隐。</summary>
    public void SetLabelVisible(bool visible)
    {
        if (labelText != null) labelText.gameObject.SetActive(visible);
    }

    /// <summary>把底字换成技能名；没名字时退回占位文案。</summary>
    public void SetSkillName(string name, string fallback = "被动")
    {
        if (labelText == null) return;
        labelText.text = string.IsNullOrEmpty(name) ? fallback : name;
        labelText.gameObject.SetActive(true);
    }

    /// <summary>右下角等级/星级：空串时整个隐藏。</summary>
    public void SetLevelText(string text)
    {
        if (levelText == null) return;
        levelText.text = text ?? "";
        levelText.gameObject.SetActive(!string.IsNullOrEmpty(levelText.text));
    }

    /// <summary>充能比例 0~1（走底部细条，不依赖美术预设的能量环）。</summary>
    public void SetEnergyFill(float ratio)
    {
        if (energyFill != null) energyFill.fillAmount = Mathf.Clamp01(ratio);
    }
}
