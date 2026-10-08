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
    /// <summary>
    /// 左上角释放顺序角标 ①②③④（2026-10-06 主人拍板：底部技槽要能看出「顺序＝优先级」）。
    /// 空槽不显示。与 SkillOrderGuide 的「拖动技能可改释放顺序：① 最先放。」同一口径。
    /// </summary>
    public Text orderText;
    /// <summary>
    /// 【2026-10-06 主人拍板】右上角「新」角标：本拍抽奖刚拿到的<b>技能 / 佣兵技能</b>才显示，
    /// 玩家点「继续」后由 <c>NewLootMarks.ClearAll()</c> 统一清掉（判据只有 <c>NewLootMarks.Has</c> 一处）。
    /// 与左上角 ①②③④ 各占一角，互不遮挡。节点由 <c>BattleUI.WidgetFactory</c> 运行时补建（不改预制体）。
    /// </summary>
    public Text newText;

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

    Color _avatarBaseColor = Color.white;
    bool _avatarBaseCaptured;

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
        if (avatarImage == null) return;
        avatarImage.preserveAspect = true;
        avatarImage.sprite = icon;
        // 不要隐藏空槽，保留槽位框体可见；
        // 但没图时必须把颜色置透明 —— sprite 为空的 Image 会渲染成一块白片。
        avatarImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        avatarImage.gameObject.SetActive(true);
        if (icon != null) EnsureIconRectVisible();
    }

    /// <summary>
    /// 【2026-10-07 主人报「4 技槽上没图标」】sprite 明明加载到了却看不见。
    ///
    /// <para>排查口径：把「图标到底能不能被画出来」的量<b>一次打全</b>，别再靠猜 ——
    /// rect 尺寸 / alpha / 激活 / 兄弟序 / 父节点名。</para>
    ///
    /// <para>只在 rect 退化成 0（画不出来的唯一常见原因）时动手修正：拉伸到父节点的 68% 并居中。
    /// 这不是静默兜底 —— 修之前先 <c>LogError</c> 报出来。</para>
    /// </summary>
    void EnsureIconRectVisible()
    {
        var rt = avatarImage.rectTransform;
        if (rt == null) return;
        var sz = rt.rect.size;
        if (sz.x >= 1f && sz.y >= 1f)
        {
            Debug.Log($"[SkillAvatar] 技槽图标：{avatarImage.sprite?.name} 节点={avatarImage.name}" +
                      $" 父={avatarImage.transform.parent?.name} rect={sz.x:F1}×{sz.y:F1}" +
                      $" alpha={avatarImage.color.a:F2} 激活={avatarImage.gameObject.activeInHierarchy}" +
                      $" 兄弟序={avatarImage.transform.GetSiblingIndex()}/{avatarImage.transform.parent?.childCount ?? 0}");
            return;
        }
        Debug.LogError($"[SkillAvatar] 技槽图标 rect 退化成 {sz.x:F1}×{sz.y:F1}（画不出来）：" +
                       $"节点={avatarImage.name} 父={avatarImage.transform.parent?.name} " +
                       $"锚点={rt.anchorMin}~{rt.anchorMax} sizeDelta={rt.sizeDelta} → 已按父节点 68% 居中拉伸");
        rt.anchorMin = new Vector2(0.16f, 0.16f);
        rt.anchorMax = new Vector2(0.84f, 0.84f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

    /// <summary>
    /// 蓝不够放这一发 → 头像压暗（灰度 ×0.45），够了还原（2026-10-06）。
    /// 只动<b>图标层</b>，与 SetEmptyDim 的底框压暗各管一层，互不打架。幂等，重复调用无副作用。
    /// </summary>
    public void SetMpShortage(bool shortage)
    {
        if (avatarImage == null) return;
        if (!_avatarBaseCaptured)
        {
            _avatarBaseColor = avatarImage.color;
            _avatarBaseCaptured = true;
        }
        var c = _avatarBaseColor;
        if (shortage)
        {
            float g = (c.r + c.g + c.b) / 3f * 0.45f;
            avatarImage.color = new Color(g, g, g, c.a);
        }
        else
        {
            avatarImage.color = c;
        }
    }

    /// <summary>左上角释放顺序角标：空串时整个隐藏。</summary>
    public void SetOrderText(string text)
    {
        if (orderText == null) return;
        orderText.text = text ?? "";
        orderText.gameObject.SetActive(!string.IsNullOrEmpty(orderText.text));
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】右上角「新」角标显隐。幂等，重复调用无副作用；
    /// 节点没绑到就整段跳过（不新建节点、不改预制体）。
    /// </summary>
    public void SetNewBadge(bool isNew)
    {
        if (newText == null) return;
        newText.text = isNew ? "新" : "";
        newText.gameObject.SetActive(isNew);
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
