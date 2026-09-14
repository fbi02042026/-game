using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 角色槽位UI（玩家/佣兵头像 + 血条 + 技能能量环）
/// 玩家槽位：点击头像释放技能，能量环显示进度，满时金色描边
/// </summary>
[System.Serializable]
public class CharacterSlotUI
{
    public GameObject root;             // 槽位根对象
    public Image portrait;              // 头像图标（不改头像框）
    public GameObject portraitPlaceholder; // 占位图
    public Image energyRing;            // 圆形能量环（可选）
    public Image glowBorder;            // 金色描边（能量满时显示）
    public Text levelLabel;             // 等级标签 "Lv.4"
    public Text nameText;               // 角色名（签名后延用）
    public Image hpBarFill;             // 血条填充 HPBarFill
    public Text hpText;                 // HP数值 "28/28"
    public Image lanBarFill;            // 蓝条/技能能量 lanBarFill
    /// <summary>未解锁/空槽时为 false，蓝条强制保持 0</summary>
    public bool EnergyEnabled { get; private set; } = true;
    public Text lanText;                // 蓝条文字
    public GameObject lockedOverlay;    // 锁定遮罩

    private float _lastEnergy = 0f;

    /// <summary>
    /// 更新槽位显示
    /// </summary>
    public void UpdateSlot(string name, int level, float currentHp, float maxHp, bool showLevel = true)
    {
        if (root == null) return;
        root.SetActive(true);
        var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le != null) le.ignoreLayout = false;

        if (nameText != null && !string.IsNullOrEmpty(name))
            nameText.text = name;
        if (levelLabel != null)
        {
            levelLabel.gameObject.SetActive(showLevel);
            if (showLevel)
                levelLabel.text = $"Lv.{level}";
        }
        if (hpText != null)
        {
            hpText.gameObject.SetActive(true);
            hpText.text = $"{Mathf.RoundToInt(currentHp)}";
        }
        if (hpBarFill != null)
        {
            float ratio = maxHp > 0 ? currentHp / maxHp : 0;
            hpBarFill.enabled = true;
            hpBarFill.fillAmount = Mathf.Clamp01(ratio);
        }
        if (lanText != null) lanText.gameObject.SetActive(true);
        if (lanBarFill != null) lanBarFill.enabled = true;
    }

    /// <summary>设置头像图片（只换图标，不改头像框；强制保持比例防拉伸）</summary>
    public void SetPortrait(Sprite icon)
    {
        if (portrait != null)
        {
            portrait.preserveAspect = true;
            portrait.type = Image.Type.Simple;
            portrait.sprite = icon;
            portrait.gameObject.SetActive(icon != null);
            portrait.color = Color.white;
            if (icon != null)
                FitPortraitNoStretch(portrait);
        }
        if (portraitPlaceholder != null && portrait != null && portraitPlaceholder != portrait.gameObject)
            portraitPlaceholder.SetActive(icon == null);
    }

    static void FitPortraitNoStretch(Image img)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        // 用 FitInParent 保证正方形/矩形框里不横向拉伸像素图
        var fitter = img.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = img.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (img.sprite != null)
        {
            var r = img.sprite.rect;
            fitter.aspectRatio = Mathf.Max(0.01f, r.width / Mathf.Max(1f, r.height));
        }
        else
            fitter.aspectRatio = 1f;
        rt.localScale = Vector3.one;
    }

    /// <summary>技能能量：底栏 lanBar 显示进度，满时仅显示光边（不改头像框）</summary>
    public void SetEnergy(float energy)
    {
        if (!EnergyEnabled) energy = 0f;
        _lastEnergy = energy;
        float e = Mathf.Clamp01(energy);

        if (lanBarFill != null)
            lanBarFill.fillAmount = e;
        if (lanText != null)
            lanText.text = e <= 0.001f ? "" : $"{Mathf.RoundToInt(e * 100)}%";
        // 不用头像框/头像当进度条
        if (energyRing != null)
            energyRing.fillAmount = 0f;

        bool isReady = e >= 0.99f;
        if (glowBorder != null)
        {
            glowBorder.gameObject.SetActive(isReady);
            if (isReady)
            {
                glowBorder.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            }
        }
    }

    /// <summary>
    /// 头像下「蓝条」改为经验进度（玩家槽专用；佣兵槽仍用 SetEnergy 显示技能能量）。
    /// 仅驱动 lanBarFill + lanText，不影响能量环/光边。
    /// </summary>
    /// <summary>
    /// 等级/经验已从战斗中移除：蓝条不再显示经验，直接隐藏（蓝条留给技能能量）。
    /// 保留方法供旧调用点编译通过。
    /// </summary>
    public void SetExpBar(float ratio, int cur, int max)
    {
        if (lanText != null)
        {
            lanText.gameObject.SetActive(false);
            lanText.text = "";
        }
    }

    public void TickSkillReadyPulse()
    {
        if (glowBorder == null || !glowBorder.gameObject.activeSelf) return;
        float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
        var c = glowBorder.color;
        c.r = 1f;
        c.g = 0.85f;
        c.b = 0.15f;
        c.a = a;
        glowBorder.color = c;
    }

    public void SetEnergyEnabled(bool enabled)
    {
        EnergyEnabled = enabled;
        if (!enabled)
            SetEnergy(0f);
    }

    /// <summary>满能量光边：叠在 Portrait 父节点上，不用头像当进度条</summary>
    public void EnsureSkillGlow()
    {
        if (glowBorder != null) return;
        Transform portraitRoot = portrait != null ? portrait.transform.parent : null;
        if (portraitRoot == null) return;

        Transform existing = portraitRoot.Find("SkillGlow");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("SkillGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(portraitRoot, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-8f, -8f);
            rt.offsetMax = new Vector2(8f, 8f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = null;
            img.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            img.preserveAspect = true;
        }
        glowBorder = go.GetComponent<Image>();
        go.SetActive(false);
    }

    /// <summary>
    /// 设置锁定状态。锁定/空闲时隐藏槽位，但不触发兄弟槽拉伸（需 CharacterBar ForceExpand=false）。
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (root != null)
            root.SetActive(true);
        if (locked)
        {
            if (portrait != null) portrait.gameObject.SetActive(false);
            if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
            SetEnergyEnabled(false);
        }
        else
        {
            SetEnergyEnabled(true);
        }
    }

    /// <summary>
    /// 清空槽位（无佣兵时隐藏，不占布局拉伸）
    /// </summary>
    public void Clear()
    {
        SetEnergyEnabled(false);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (root != null)
        {
            root.SetActive(false);
            var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }
    }

    /// <summary>空槽占位显示（已解锁但无佣兵）</summary>
    public void ShowEmpty()
    {
        SetEnergyEnabled(false);
        if (root != null) root.SetActive(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (portrait != null) portrait.gameObject.SetActive(false);
        if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
        if (levelLabel != null) levelLabel.text = "";
        ClearNumericDisplays();
    }

    /// <summary>
    /// 未解锁槽：保留锁定遮罩与美术布局，但清掉血量/能量数值（预制体占位数字不要露出来）。
    /// </summary>
    public void KeepArtistDefault()
    {
        SetEnergyEnabled(false);
        if (root == null) return;
        root.SetActive(true);
        if (lockedOverlay != null)
            lockedOverlay.SetActive(true);
        ClearNumericDisplays();
    }

    bool _lockedLabelSized;

    /// <summary>未开放槽：保留节点可见，头像关掉，文案显示「未解锁」</summary>
    public void ShowUnavailable(string label = "未解锁")
    {
        SetEnergyEnabled(false);
        if (root == null) return;
        root.SetActive(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(true);
        if (portrait != null) portrait.gameObject.SetActive(false);
        if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
        ApplyLockedOverlayText(label ?? "未解锁");
        if (levelLabel != null)
            levelLabel.text = "";
        ClearNumericDisplays();
    }

    void ApplyLockedOverlayText(string text)
    {
        if (lockedOverlay == null) return;
        var texts = lockedOverlay.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;
            t.text = text;
            if (!_lockedLabelSized)
                t.fontSize += 2;
        }
        _lockedLabelSized = true;
    }

    /// <summary>清掉血条/蓝条上的数值与填充，避免未解锁槽露出占位数字。</summary>
    void ClearNumericDisplays()
    {
        if (hpText != null)
        {
            hpText.text = "";
            hpText.gameObject.SetActive(false);
        }
        if (lanText != null)
        {
            lanText.text = "";
            lanText.gameObject.SetActive(false);
        }
        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = 0f;
            hpBarFill.enabled = false;
        }
        if (lanBarFill != null)
        {
            lanBarFill.fillAmount = 0f;
            lanBarFill.enabled = false;
        }
    }
}
