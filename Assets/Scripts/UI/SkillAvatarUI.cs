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
    public Text cooldownText;         // 冷却倒计时文字（已废弃数字显示，仅保留节点兼容）
    /// <summary>冷却黑色半透遮罩（Radial360 填充，钟表式收缩）。</summary>
    public Image cooldownMask;
    /// <summary>底部充能细条（新底部布局运行时补建）。</summary>
    public Image energyFill;
    /// <summary>槽位底字（原临时 UI 的「被动」占位），现在改成显示技能名。</summary>
    public Text labelText;
    /// <summary>右下角等级文字（美术在每个技能槽下加了 level 节点，没有就运行时补建）。</summary>
    public Text levelText;
    /// <summary>槽位底框：槽根自己那层 Image（美术底图就在这一层，图标层是另建的子层）。</summary>
    public Image frameImage;

    [System.NonSerialized]
    public System.Action onClick;     // 点击回调

    /// <summary>
    /// 「没装备技能」时底框压暗总开关（2026-09-26 主人要求）。
    /// 口径与 DailyLoginUI.EnableCellDim 一致：**先记录底框原始色，再按系数相乘压暗**，
    /// 绝不写死颜色去覆盖美术底图。一键回退：置 false。
    /// </summary>
    public static bool EnableEmptySlotDim = true;

    /// <summary>空槽压暗系数（与 DailyLoginUI 未解锁格同档 0.45：够暗又不糊）。</summary>
    const float EmptySlotDimK = 0.45f;

    Color _frameBaseColor;
    bool _frameBaseCaptured;

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
    /// 设置冷却遮罩比例（钟表式收缩）：
    /// ratio<=0 隐藏遮罩；>0 时显示并按比例填充（Radial360，满→空）。
    /// </summary>
    public void SetCooldownRatio(float ratio)
    {
        if (cooldownMask == null) return;
        if (ratio <= 0f)
        {
            cooldownMask.gameObject.SetActive(false);
            return;
        }
        cooldownMask.gameObject.SetActive(true);
        cooldownMask.fillAmount = Mathf.Clamp01(ratio);
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
            // 不要隐藏空槽，保留槽位框体可见；
            // 但没图时必须把颜色置透明 —— sprite 为空的 Image 会渲染成一块白片。
            avatarImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            avatarImage.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 没装备技能 → 底框压暗（base × 0.45，保 alpha）；已装备 → 还原 base。
    /// 幂等，重复调用无副作用。frameImage 没绑到就整段跳过（不新建节点、不改预制体）。
    /// </summary>
    public void SetEmptyDim(bool empty)
    {
        if (frameImage == null) return;
        if (!_frameBaseCaptured)
        {
            _frameBaseColor = frameImage.color;
            _frameBaseCaptured = true;
        }
        var c = _frameBaseColor;
        frameImage.color = (empty && EnableEmptySlotDim)
            ? new Color(c.r * EmptySlotDimK, c.g * EmptySlotDimK, c.b * EmptySlotDimK, c.a)
            : c;
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

    /// <summary>
    /// 显示/隐藏底部充能细条。2026-09-15 玩家技能改纯冷却制后不再需要它，
    /// 这里保留节点只置隐藏（GameConfig.PLAYER_SKILL_USE_ENERGY 置 true 就能原样回来）。
    /// </summary>
    public void SetEnergyFillVisible(bool on)
    {
        if (energyFill != null) energyFill.gameObject.SetActive(on);
    }
}
