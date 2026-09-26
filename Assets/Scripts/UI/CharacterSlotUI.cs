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
    /// <summary>
    /// 头像框：预制体里叫 PlayerSlot 的那层（玩家槽 root 本身就是它，佣兵槽是 root 下的子节点）。
    /// 它不是头像图层，别拿它当头像用；这里只换框图，不改尺寸、不新增节点。
    /// </summary>
    public Image frameImage;
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

    // ============================================================
    // 蓝色盾条（抵扣型护盾：先扣盾、再扣血）
    // 建在血条底框（HPBarBg）里、血条正下方，只在有护盾时 SetActive(true)，
    // 平时隐藏不占地方。不动血条 / 蓝条本身的布局与颜色。
    // 一键关掉整条盾条：CharacterSlotUI.ShieldBarEnabled = false。
    // ============================================================
    /// <summary>盾条总开关：置 false 则三个角色槽都不再显示盾条。</summary>
    public static bool ShieldBarEnabled = true;
    const string ShieldBarNodeName = "ShieldBar";
    /// <summary>盾条高度（像素）。血条 12px、与下方雷击充能条之间只有 ≈4.3px 空档，默认 4 刚好不压到。</summary>
    const float ShieldBarHeight = 4f;
    /// <summary>盾条与血条底边的间距（像素）。</summary>
    const float ShieldBarGap = 0f;
    /// <summary>盾条填充色（蓝）。</summary>
    static readonly Color ShieldBarFillColor = new Color(0.30f, 0.68f, 1.00f, 0.95f);
    /// <summary>盾条底色（深蓝）。</summary>
    static readonly Color ShieldBarBgColor = new Color(0.08f, 0.16f, 0.28f, 0.80f);

    GameObject _shieldBarRoot;
    Image _shieldBarFill;

    /// <summary>职业图标：预制体节点 xuetiaodi/职业icon（玩家 = 所选职业，佣兵 = 佣兵职业）。</summary>
    public Image jobIcon;

    private float _lastEnergy = 0f;
    /// <summary>整体压暗前记录的每个 Graphic 原始颜色，解锁时按原样还原。</summary>
    readonly System.Collections.Generic.Dictionary<Graphic, Color> _dimBackup =
        new System.Collections.Generic.Dictionary<Graphic, Color>();
    /// <summary>当前是否已整体压暗（避免每帧重复遍历）。</summary>
    bool _dimmed;
    /// <summary>
    /// 压暗强度：先记录原始色再按系数相乘（不写死颜色），与 DailyLoginUI.EnableCellDim、
    /// SkillAvatarUI.EmptySlotDimK 同款 k=0.45，落在主人给的 0.45~0.5 惯例区间内。
    /// 只影响被压暗的槽（未招募 / 空槽 / 未解锁），已雇佣槽不参与。
    /// </summary>
    const float DimScale = 0.45f;

    /// <summary>
    /// 未招募（未解锁 / 空槽）佣兵槽 =「玩家那一套外观 + 没有头像 + 整体压暗」总开关（2026-09-26 主人最新口径）。
    /// 口径变化记录：
    ///   ① 更早：未雇佣态点亮 portraitPlaceholder（RuntimeUiArt.Disc 深灰圆盘 0.28,0.27,0.32,0.9）当底框；
    ///   ② 2026-09-26 早些时候：主人要求「未雇佣底框用白色」，于是加了 UnhiredFrameWhite +
    ///      PaintUnhiredFrame 把那个盘置白并挡掉整体压暗 —— 该方向已被主人否掉
    ///      （一大块白/灰圆盘太抢眼，像多顶了一个头像）；
    ///   ③ 现在：底框一律不置白，头像位直接留白；血条 / 能量条 / 技能位照抄玩家槽保留；
    ///      「未招募」只靠 ApplyDim 整体压暗 + 锁图标表达。
    /// 一键回退：置 false（回到「点亮占位圆盘」的旧表现）。
    /// </summary>
    public static bool UnhiredShowPlayerLook = true;

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

    /// <summary>
    /// 设置头像图片（只换图，不动排版）。
    /// 预制体里美术摆的是 sizeDelta 200×200 + localScale 0.7，头像资源本身也是 200×200 —— 直接换图即可，
    /// 不要再改 preserveAspect / 加 AspectRatioFitter / 抹 localScale，否则头像会比预制体明显大一圈。
    /// </summary>
    public void SetPortrait(Sprite icon)
    {
        if (portrait != null)
        {
            portrait.type = Image.Type.Simple;
            portrait.sprite = icon;
            portrait.gameObject.SetActive(icon != null);
            portrait.color = Color.white;
        }
        if (portraitPlaceholder != null && portrait != null && portraitPlaceholder != portrait.gameObject)
            portraitPlaceholder.SetActive(icon == null);
    }

    /// <summary>换头像框（普通灰白 / 稀有蓝 / 传奇橙金）。只换图，不动 rect、不加节点。</summary>
    public void SetFrame(Sprite frame)
    {
        if (frameImage == null) return;
        frameImage.sprite = frame;
        // 不改 preserveAspect：预制体里框设的是 0（填满 170×100），改成 1 会留白、跟美术摆的不一样
        frameImage.enabled = frame != null;
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
    /// 2026-09-26 主人最新口径：锁图标（lockedOverlay）也跟着一起压暗 —— 它的语义就是「未招募」，
    /// 跟整槽同一套明暗才协调（0.45 只压暗不隐身，锁仍看得清）。
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

    /// <summary>
    /// 未招募态的头像位：直接留白 —— 头像关掉，占位盘（那个深灰/白圆盘）也不点亮。
    /// 2026-09-26 主人口径：未招募槽就是「玩家那一套，除了头像没有」，头像位不该再顶一个圆盘。
    /// 已废弃的两个方向：① 点亮 portraitPlaceholder 深灰盘；② 把那个盘置白 —— 都不再使用，底框不置白。
    /// </summary>
    void ApplyUnhiredPortrait()
    {
        if (portrait != null) portrait.gameObject.SetActive(false);
        if (portraitPlaceholder == null) return;
        // 开关关掉 = 回退到「点亮占位圆盘」的旧表现；默认关掉圆盘，头像位留白。
        portraitPlaceholder.SetActive(!UnhiredShowPlayerLook);
    }

    /// <summary>
    /// 未招募态也要「玩家那一套」：血条 / 蓝条（能量条）的条本体保留可见，只是 0 填充 + 空文字，
    /// 不整条隐藏 —— 隐藏了就只剩空框，跟左边玩家槽长得不一样。
    /// </summary>
    void KeepBarGraphicsVisible()
    {
        if (hpBarFill != null)
        {
            hpBarFill.enabled = true;
            hpBarFill.fillAmount = 0f;
        }
        if (lanBarFill != null)
        {
            lanBarFill.enabled = true;
            lanBarFill.fillAmount = 0f;
        }
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

    /// <summary>
    /// 显示护盾（抵扣型）：amount = 当前护盾值，max = 护盾上限。两者任一 ≤ 0 就隐藏盾条。
    /// 条本身只表达「当前 / 上限」的比例，不写数字（条高只有 4px，塞字会糊）。
    /// </summary>
    public void SetShield(float amount, float max)
    {
        if (!ShieldBarEnabled || amount <= 0f || max <= 0f || root == null)
        {
            HideShieldBar();
            return;
        }
        if (!EnsureShieldBar()) return;
        _shieldBarRoot.SetActive(true);
        _shieldBarFill.fillAmount = Mathf.Clamp01(amount / max);
    }

    /// <summary>护盾耗尽 / 过期 / 槽位清空时把盾条收起来。</summary>
    public void HideShieldBar()
    {
        if (_shieldBarRoot != null) _shieldBarRoot.SetActive(false);
    }

    /// <summary>运行时建盾条（幂等）。挂在血条底框里，跟着血条一起缩放平移。</summary>
    bool EnsureShieldBar()
    {
        if (_shieldBarRoot != null && _shieldBarFill != null) return true;
        if (root == null) return false;

        // 血条底框（HPBarBg）是血条的父节点；取不到就退回槽位根，保证不崩
        Transform host = hpBarFill != null && hpBarFill.transform.parent != null
            ? hpBarFill.transform.parent
            : root.transform;

        Transform exist = host.Find(ShieldBarNodeName);
        GameObject go = exist != null ? exist.gameObject
            : new GameObject(ShieldBarNodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (exist == null)
        {
            go.transform.SetParent(host, false);
            var bgImg = go.GetComponent<Image>();
            bgImg.raycastTarget = false;
            bgImg.sprite = RuntimeUiArt.Bar();
            bgImg.color = ShieldBarBgColor;
            bgImg.type = Image.Type.Simple;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.raycastTarget = false;
            fillImg.sprite = RuntimeUiArt.Bar();
            fillImg.color = ShieldBarFillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
        }

        var rt = go.GetComponent<RectTransform>();
        var hostRt = host as RectTransform;
        // 布局还没算出来时给一组保底尺寸，避免盾条被压成 0
        float w = hostRt != null && hostRt.rect.width > 1f ? hostRt.rect.width : 100f;
        float h = hostRt != null && hostRt.rect.height > 1f ? hostRt.rect.height : 12f;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, ShieldBarHeight);
        // 血条底边再往下 ShieldBarGap：正好落在血条与雷击充能条之间的空档里
        rt.anchoredPosition = new Vector2(0f, -(h * 0.5f + ShieldBarHeight * 0.5f + ShieldBarGap));

        _shieldBarRoot = go;
        Transform fillT = go.transform.Find("Fill");
        _shieldBarFill = fillT != null ? fillT.GetComponent<Image>() : null;
        if (_shieldBarFill == null) return false;
        go.SetActive(false);
        return true;
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
            // 必须有图：sprite 为空的 Image 会画成一块实心金色方块盖住头像。
            // 这里用运行时生成的白色圆环蒙版，颜色仍由 SetEnergy 写成金色。
            img.sprite = RuntimeUiArt.Ring();
            img.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            img.preserveAspect = true;
        }
        glowBorder = go.GetComponent<Image>();
        go.SetActive(false);
    }

    // ============================================================
    // 运行时补齐美术未画的装饰件（L）
    // CharacterBar 下的三张角色卡预制体里只有 Portrait + xuetiaodi，
    // 绑定自检会报「NameText / LevelLabel / Glow / PortraitPlaceholder 缺失」。
    // 这里全部运行时新建节点：不动预制体、不动已有节点的 rect，
    // 只是给一份「能看」的占位，等美术/策划确认样式后再调位置和配色。
    // ============================================================

    /// <summary>补齐全部缺失装饰件。幂等，重复调用无副作用。</summary>
    public void EnsureRuntimeDecorations()
    {
        EnsureNameText();
        EnsureLevelLabel();
        EnsurePortraitPlaceholderNode();
        EnsureSkillGlow();
    }

    /// <summary>
    /// 名字文字。落在「头像下半部 / 血条面板上方」那条空带上（卡片坐标 y≈8），
    /// 这个位置不被 HP / 蓝条压住。
    /// </summary>
    void EnsureNameText()
    {
        if (nameText != null || root == null) return;
        var t = EnsureRuntimeText("NameText", 20, TextAnchor.MiddleCenter, Color.white);
        var rt = t.rectTransform;
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(160f, 24f);
        t.text = "";
        nameText = t;
    }

    /// <summary>
    /// 等级文字（Lv.N）。放在卡片左上角（y≈34），避开中间的脸。
    /// 玩家槽走 UpdateSlot(showLevel:false)，只有佣兵槽会显示。
    /// </summary>
    void EnsureLevelLabel()
    {
        if (levelLabel != null || root == null) return;
        var t = EnsureRuntimeText("LevelLabel", 18, TextAnchor.MiddleCenter,
                                  new Color(1f, 0.84f, 0.42f));
        var rt = t.rectTransform;
        rt.anchoredPosition = new Vector2(-52f, 34f);
        rt.sizeDelta = new Vector2(64f, 22f);
        t.text = "";
        levelLabel = t;
    }

    /// <summary>在卡片中心建一个 Text，统一字体 + 描边阴影（压在头像上也看得清）。</summary>
    Text EnsureRuntimeText(string nodeName, int fontSize, TextAnchor align, Color color)
    {
        Transform exist = root.transform.Find(nodeName);
        GameObject go = exist != null ? exist.gameObject
            : new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        if (exist == null)
            go.transform.SetParent(root.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(160f, 24f);

        var t = go.GetComponent<Text>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;

        // 名字/等级压在头像上，没有描边会糊成一片
        if (go.GetComponent<Shadow>() == null)
        {
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(1f, -1f);
        }
        go.transform.SetAsLastSibling();
        go.SetActive(true);
        return t;
    }

    /// <summary>
    /// 无头像时的占位底盘。垫在头像框里、头像图层之下（SetAsFirstSibling），
    /// 只在 SetPortrait(null) / SetLocked / ShowEmpty 时点亮。
    /// </summary>
    void EnsurePortraitPlaceholderNode()
    {
        if (portraitPlaceholder != null || root == null) return;

        Transform holder = portrait != null && portrait.transform.parent != null
            ? portrait.transform.parent
            : root.transform;

        Transform exist = holder.Find("PortraitPlaceholder");
        GameObject go;
        if (exist != null)
        {
            go = exist.gameObject;
        }
        else
        {
            go = new GameObject("PortraitPlaceholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(holder, false);
            go.transform.SetAsFirstSibling();     // 必须在头像之下
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;   // 跟 Portrait 同一中心
            rt.sizeDelta = portrait != null ? portrait.rectTransform.sizeDelta : new Vector2(140f, 140f);
            // 头像节点自己带 localScale（预制体是 200×200 + scale 0.7），
            // 占位盘是它的兄弟节点，必须跟着缩放，否则会比头像大一圈。
            rt.localScale = portrait != null ? portrait.rectTransform.localScale : Vector3.one;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = RuntimeUiArt.Disc();
            img.color = new Color(0.28f, 0.27f, 0.32f, 0.9f);
        }
        portraitPlaceholder = go;
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
            // 未招募：头像位留白，其余结构（血条/能量条/技能位）照玩家槽保留可见，只整体压暗。
            // 这里不再 SetEnergyEnabled(false) 把蓝条清零关掉：按主人「用玩家那一套」的口径，
            // 条本体要留着（KeepBarGraphicsVisible），充能仍由 ShowEmpty/ShowUnavailable 那侧负责关。
            ApplyUnhiredPortrait();
            KeepBarGraphicsVisible();
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
        ApplyUnhiredPortrait();         // 空槽（未雇佣）：头像位留白，不再点亮/置白占位盘
        if (levelLabel != null) levelLabel.text = "";
        ClearNumericDisplays();
        if (UnhiredShowPlayerLook) KeepBarGraphicsVisible();   // 条本体保留，跟玩家槽同一套
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
        ApplyUnhiredPortrait();         // 未解锁（未雇佣）：头像位留白，不再点亮/置白占位盘
        ApplyLockedOverlayText(label ?? "未解锁");
        if (levelLabel != null)
            levelLabel.text = "";
        ClearNumericDisplays();
        if (UnhiredShowPlayerLook) KeepBarGraphicsVisible();   // 条本体保留，跟玩家槽同一套
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
        HideShieldBar();          // 空槽 / 未解锁槽不该残留盾条
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
