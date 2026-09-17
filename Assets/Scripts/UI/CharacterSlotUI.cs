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
    public GameObject lockedOverlay;    // 锁定遮罩（预制体没有时运行时补建）
    /// <summary>右上角技能小图标：佣兵技能的标识，自动释放。</summary>
    public Image skillBadge;

    /// <summary>职业图标：预制体节点 xuetiaodi/职业icon（玩家 = 所选职业，佣兵 = 佣兵职业）。</summary>
    public Image jobIcon;

    private float _lastEnergy = 0f;
    /// <summary>整体压暗前记录的每个 Graphic 原始颜色，解锁时按原样还原。</summary>
    readonly System.Collections.Generic.Dictionary<Graphic, Color> _dimBackup =
        new System.Collections.Generic.Dictionary<Graphic, Color>();
    /// <summary>当前是否已整体压暗（避免每帧重复遍历）。</summary>
    bool _dimmed;
    /// <summary>压暗强度：0.32 左右能明显「暗掉」又不至于糊成一团。</summary>
    const float DimScale = 0.32f;

    /// <summary>
    /// 更新槽位显示
    /// </summary>
    public void UpdateSlot(string name, int level, float currentHp, float maxHp, bool showLevel = true)
    {
        if (root == null) return;
        root.SetActive(true);
        ApplyDim(false);
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

    /// <summary>
    /// 未解锁遮罩：新预制体里没有这个节点，运行时补一个全拉伸的半透明黑底 + 居中文案。
    /// 默认隐藏，由 SetLocked / ShowUnavailable / KeepArtistDefault 决定显隐。
    /// </summary>
    public void EnsureLockedOverlay()
    {
        if (lockedOverlay != null || root == null) return;

        Transform exist = root.transform.Find("LockedOverlay");
        GameObject go;
        if (exist != null)
        {
            go = exist.gameObject;
            // 旧节点是「半透明黑遮罩 + 未解锁文案」：按需求去掉那层遮罩，只留一把锁。
            var oldImg = go.GetComponent<Image>();
            if (oldImg != null) oldImg.color = new Color(0f, 0f, 0f, 0f);
            ApplyLockIcon(go.transform);
        }
        else
        {
            go = new GameObject("LockedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(38f, 38f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            img.sprite = RuntimeLockSprite.Get();
        }
        lockedOverlay = go;
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    /// <summary>把已有 LockedOverlay 节点改造成「居中显示一把锁」。</summary>
    static void ApplyLockIcon(Transform overlay)
    {
        if (overlay == null) return;
        var icon = overlay.Find("LockIcon");
        GameObject iconGo;
        if (icon != null)
        {
            iconGo = icon.gameObject;
        }
        else
        {
            iconGo = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(overlay, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(38f, 38f);
        }
        var img = iconGo.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            img.sprite = RuntimeLockSprite.Get();
        }
        iconGo.SetActive(true);
        // 旧的「未解锁」文案节点不再使用
        for (int i = 0; i < overlay.childCount; i++)
        {
            var child = overlay.GetChild(i);
            if (child != null && child != iconGo.transform && child.name != "LockIcon")
                child.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 整槽「全部暗掉」：把槽里所有 Image/Text 的颜色按 DimScale 压暗，解锁时按备份原样还原。
    /// 锁图标自身不参与压暗，否则连锁都看不见了。
    /// </summary>
    void ApplyDim(bool dim)
    {
        if (root == null) return;
        if (dim == _dimmed) return;
        _dimmed = dim;
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            if (g == null) continue;
            if (IsPartOfLockIcon(g.transform)) continue;
            Color origColor;
            if (!_dimBackup.TryGetValue(g, out origColor))
            {
                origColor = g.color;
                _dimBackup[g] = origColor;
            }
            g.color = dim
                ? new Color(origColor.r * DimScale, origColor.g * DimScale, origColor.b * DimScale, origColor.a)
                : origColor;
        }
    }

    /// <summary>锁图标及其子节点、LockedOverlay 容器本身，都不参与压暗。</summary>
    bool IsPartOfLockIcon(Transform t)
    {
        if (lockedOverlay == null || t == null) return false;
        var lockRoot = lockedOverlay.transform;
        Transform cur = t;
        while (cur != null)
        {
            if (cur == lockRoot) return true;
            cur = cur.parent;
        }
        return false;
    }

    /// <summary>
    /// 右上角技能小图标。必须挂在头像的「父节点」上 —— 头像自己被 FitPortraitNoStretch
    /// 加了 AspectRatioFitter，挂在它身上会被按原图比例拉变形。
    /// </summary>
    public void EnsureSkillBadge()
    {
        if (skillBadge != null || portrait == null) return;

        Transform holder = portrait.transform.parent != null ? portrait.transform.parent : root.transform;
        Transform exist = holder.Find("SkillBadge");
        GameObject go;
        if (exist != null)
        {
            go = exist.gameObject;
        }
        else
        {
            go = new GameObject("SkillBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(holder, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-13f, -13f);
            rt.sizeDelta = new Vector2(28f, 28f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;   // 只按比例缩放，不改原图尺寸
            img.sprite = null;
            img.color = Color.white;
        }
        skillBadge = go.GetComponent<Image>();
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    /// <summary>设置/清除右上角技能图标；给 null 就隐藏。</summary>
    public void SetSkillBadge(Sprite icon)
    {
        if (skillBadge == null) EnsureSkillBadge();
        if (skillBadge == null) return;
        skillBadge.preserveAspect = true;
        skillBadge.sprite = icon;
        skillBadge.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        skillBadge.gameObject.SetActive(icon != null);
    }

    /// <summary>设置职业图标；给 null 就隐藏节点（不显示占位白块）。</summary>
    public void SetJobIcon(Sprite icon)
    {
        if (jobIcon == null) return;
        jobIcon.preserveAspect = true;
        jobIcon.raycastTarget = false;
        jobIcon.sprite = icon;
        jobIcon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        jobIcon.gameObject.SetActive(icon != null);
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

    /// <summary>
    /// 玩家头像下第二条 = 雷击奥义充能（HeroThunderUltimate）。
    /// 与佣兵的技能能量是两套语义，**不要复用 SetEnergy**——两者只是共用 lanBarFill / lanText
    /// 这两个显示控件。按需求：即使 GameConfig.THUNDER_ULT_ENABLED 关闭也显示空条与 0/N，不隐藏。
    /// 不改动条的颜色与光边（那套是技能能量就绪的表现，奥义不参与）。
    /// </summary>
    public void SetUltCharge(float ratio, int cur, int need, bool casting)
    {
        float r = Mathf.Clamp01(ratio);
        if (lanBarFill != null)
        {
            lanBarFill.gameObject.SetActive(true);
            lanBarFill.enabled = true;
            lanBarFill.fillAmount = r;
        }
        if (lanText != null)
        {
            lanText.gameObject.SetActive(true);
            if (casting)
                lanText.text = "雷击中…";
            else if (need > 0)
                lanText.text = r >= 0.999f ? "雷击就绪" : $"{cur}/{need}";
            else
                lanText.text = "0";   // 没数据时也别空着（第二条默认显示 0）
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
        EnsureLockedOverlay();
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        ApplyDim(locked);
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

    /// <summary>空槽占位显示（已解锁但无佣兵）：整槽压暗，一眼看出这个位置没人。</summary>
    public void ShowEmpty()
    {
        SetEnergyEnabled(false);
        if (root != null) root.SetActive(true);
        EnsureLockedOverlay();
        ApplyDim(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        SetSkillBadge(null);
        SetJobIcon(null);               // 空槽不显示职业 icon
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
        EnsureLockedOverlay();
        ApplyDim(true);
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
        EnsureLockedOverlay();
        ApplyDim(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(true);
        SetSkillBadge(null);
        SetJobIcon(null);               // 未解锁不显示职业 icon
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
