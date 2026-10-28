using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能选择弹层（参考 Art/UI/Character/skill_select_reference.png）。
/// 6 个技能格 + 说明；由角色页右侧「技能」或左侧独立按钮打开。
/// </summary>
public class SkillSelectUI : MonoBehaviour
{
    /// <summary>城镇技能选择页只展示前 6 个基础技能，其余 18 个是局内抽卡池专用。</summary>
    public const int MaxSkills = PlayerSkillDefs.TownSelectCount;

    public static SkillSelectUI Instance { get; private set; }

    public Button closeButton;
    public Text titleText;
    public Text descText;
    public Button[] skillButtons = new Button[MaxSkills];
    public Image[] skillIcons = new Image[MaxSkills];
    public Text[] skillNames = new Text[MaxSkills];
    public GameObject[] selectedMarks = new GameObject[MaxSkills];

    public Action<int> onSkillSelected;
    public Action onClosed;

    int _selected = 0;
    bool _wired;

    /// <summary>竖屏自适应总开关：关掉则完全不介入，几何保持预制体（主人手调）原样。</summary>
    public bool enableFit = true;

    bool _fitCaptured;

    /// <summary>防递归守卫：改 rect 会触发 OnRectTransformDimensionsChange，避免重入。</summary>
    bool _applying;

    /// <summary>预制体基准几何快照。主人的几何不许覆盖，这里只做兜底：一切适配都从 base 重算，非瘦屏必须完整还原。</summary>
    struct FitBase
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector2 anchoredPosition;
    }

    FitBase _dimBase;
    FitBase _panelBase;
    FitBase _skillsBase;

    // Skills 上 GridLayoutGroup 的基准（cellSize 只允许缩小，绝不允许放大）
    Vector2 _gridCellSize;
    Vector2 _gridSpacing;
    RectOffset _gridPadding;
    GridLayoutGroup.Constraint _gridConstraint;
    int _gridConstraintCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureArrays();
        if (closeButton == null || skillButtons[0] == null) AutoBind();
        Wire();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(int selectIndex = -1)
    {
        EnsureArrays();
        if (closeButton == null || skillButtons[0] == null) AutoBind();
        Wire();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureVisible();
        ApplyFit();
        RefreshSlots();
        if (selectIndex < 0)
        {
            string saved = SaveSystem.Instance?.Data?.selectedPlayerSkillId;
            selectIndex = PlayerSkillDefs.IndexOf(saved);
        }
        Select(Mathf.Clamp(selectIndex, 0, MaxSkills - 1), persist: false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onClosed?.Invoke();
    }

    public void Select(int index)
    {
        Select(index, persist: true);
    }

    public void Select(int index, bool persist)
    {
        index = Mathf.Clamp(index, 0, MaxSkills - 1);
        var def = PlayerSkillDefs.Get(index);
        if (def == null) return;
        bool unlocked = PlayerSkillDefs.IsUnlocked(def, SaveSystem.Instance?.Data);
        if (persist && !unlocked)
        {
            string hint = PlayerSkillDefs.FormatUnlockHint(def);
            UIManager.Instance?.ShowToast(hint);
            if (descText != null) descText.text = hint;
            return;
        }

        _selected = index;
        if (descText != null)
            descText.text = PlayerSkillDefs.FormatDetail(def);

        ApplySelectedMarks(index);

        if (persist)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                data.selectedPlayerSkillId = def.id;
                SaveSystem.Instance.Save();
            }
            onSkillSelected?.Invoke(index);
        }
    }

    static readonly Color LockedTint = new Color(0.42f, 0.42f, 0.42f, 1f);

    // 【2026-10-19 主人拍板】技能名颜色的唯一真源：CharacterUI.prefab 里 Skill_0..Skill_5 的
    // Label 是主人手涂的 6 种颜色，运行时曾被 ApplySlotTint 刷白覆盖；现在恢复手涂色，
    // 并通过 NameColorOf 开放给战斗技槽（BattleUI.Skills）共用，保证两处颜色一致。
    // 下标 = PlayerSkillDefs.All[N]（TownSelectCount=6，顺序稳定）。
    static readonly Color[] SkillNameColors =
    {
        new Color(1f, 1f, 1f),                    // 0 治愈之泉 白
        new Color(0.55899715f, 1f, 0.3726415f),   // 1 圣盾壁垒 浅绿
        new Color(0f, 0.60335493f, 1f),           // 2 战意爆发 蓝
        new Color(0.6794996f, 0f, 1f),            // 3 疾风架势 紫
        new Color(0.64063966f, 1f, 0f),           // 4 致命专注 绿
        new Color(1f, 0.1503219f, 0f),            // 5 天雷裁决 红
    };

    /// <summary>第 index 个技能的名字颜色（角色界面弹窗与战斗技槽共用，越界回退白）。</summary>
    public static Color NameColorOf(int index)
    {
        if (index < 0 || index >= SkillNameColors.Length) return Color.white;
        return SkillNameColors[index];
    }

    static Sprite LoadSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var sp = Resources.Load<Sprite>("UI/Icons/SkillIcon/" + skillId);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("UI/Icons/SkillIcon/" + skillId);
        if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            string artPath = "Assets/Art/UI/Icons/玩家SkillIcon/" + skillId + ".png";
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            if (sp != null) return sp;
        }
#endif
        return null;
    }

    void RefreshSlots()
    {
        var data = SaveSystem.Instance?.Data;
        for (int i = 0; i < MaxSkills && i < PlayerSkillDefs.All.Length; i++)
        {
            var def = PlayerSkillDefs.All[i];
            bool unlocked = PlayerSkillDefs.IsUnlocked(def, data);
            if (skillNames[i] != null)
                skillNames[i].text = def.displayName;
            EnsureSkillIcon(i);
            var sp = LoadSkillIcon(def.id);
            if (sp == null)
                Debug.LogError($"[SkillSelectUI] 技能 {def.id}（{def.displayName}）缺图标 Icons/SkillIcon/{def.id}，该格将显示为空");
            if (skillIcons[i] != null)
            {
                // 图标位 = 按钮自身的整格底图（没有 Icon 子节点时）。不再禁用 Image：
                // 它同时是按钮的射线接收源，禁了格子就点不动。
                skillIcons[i].sprite = sp;
                skillIcons[i].preserveAspect = true;
                skillIcons[i].color = Color.white;
            }
            // 选中高亮节点（Selected，带发光材质的那张 106×106）也必须换成同一个图标：
            // 预制体里它自带一张（错位的）技能图标，不换的话选中时会看到两张不一样的图（2026-10-13 一起修）。
            var markImg = GetSelectedMarkImage(i);
            if (markImg != null)
            {
                markImg.sprite = sp;
                markImg.enabled = sp != null;
            }
            ApplySlotTint(i, unlocked);
            if (skillButtons[i] != null)
                skillButtons[i].interactable = true;
        }
        ApplySelectedMarks(_selected);
    }

    /// <summary>
    /// 绑定第 i 格的图标 Image。优先用预制体里的 Icon 子节点；
    /// 没有 Icon 子节点时（当前预制体就是这样）**直接用按钮自身的 Image**（110×110 整格底图位）。
    /// <para>【2026-10-13 主人报「图标里面有图标」】以前这里会在 52×52 再补一个小图标，
    /// 叠在 Skill_N 自带的整格底图上 —— 看着就是大图标里套小图标。现在一格只显示一个图标：
    /// 底图位直接换成该技能自己的图标（106×106 方图，Simple 拉伸到 110 不变形）。</para>
    /// </summary>
    void EnsureSkillIcon(int i)
    {
        if (i < 0 || i >= MaxSkills || skillButtons[i] == null) return;
        if (skillIcons[i] != null) return;
        var t = skillButtons[i].transform;
        var iconTf = t.Find("Icon") ?? t.Find("icon") ?? t.Find("SkillIcon");
        if (iconTf != null)
        {
            skillIcons[i] = iconTf.GetComponent<Image>();
            if (skillIcons[i] != null) return;
        }
        // 没有 Icon 子节点 → 用按钮自身的 Image 当图标位，绝不再补建 52×52 小图标（会叠成图标套图标）
        skillIcons[i] = skillButtons[i].targetGraphic as Image
                        ?? skillButtons[i].GetComponent<Image>();
    }

    /// <summary>第 i 格「选中」高亮节点的 Image（预制体里没有就返回 null）。</summary>
    Image GetSelectedMarkImage(int i)
    {
        var mark = GetOrBindSelectedMark(i);
        return mark != null ? mark.GetComponent<Image>() : null;
    }

    void ApplySlotTint(int i, bool unlocked)
    {
        Color tint = unlocked ? Color.white : LockedTint;
        var btn = skillButtons[i];
        if (btn != null)
        {
            var img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
            if (img != null) img.color = tint;
        }
        if (skillIcons[i] != null)
            skillIcons[i].color = tint;
        if (skillNames[i] != null)
            skillNames[i].color = unlocked ? NameColorOf(i) : LockedTint;
    }

    void ApplySelectedMarks(int index)
    {
        for (int i = 0; i < MaxSkills; i++)
        {
            var mark = GetOrBindSelectedMark(i);
            if (mark != null)
                mark.SetActive(i == index);
        }
    }

    void EnsureArrays()
    {
        if (skillButtons == null || skillButtons.Length != MaxSkills)
        {
            var old = skillButtons;
            skillButtons = new Button[MaxSkills];
            if (old != null)
                for (int i = 0; i < MaxSkills && i < old.Length; i++) skillButtons[i] = old[i];
        }
        if (skillIcons == null || skillIcons.Length != MaxSkills)
        {
            var old = skillIcons;
            skillIcons = new Image[MaxSkills];
            if (old != null)
                for (int i = 0; i < MaxSkills && i < old.Length; i++) skillIcons[i] = old[i];
        }
        if (skillNames == null || skillNames.Length != MaxSkills)
        {
            var old = skillNames;
            skillNames = new Text[MaxSkills];
            if (old != null)
                for (int i = 0; i < MaxSkills && i < old.Length; i++) skillNames[i] = old[i];
        }
        if (selectedMarks == null || selectedMarks.Length != MaxSkills)
        {
            var old = selectedMarks;
            selectedMarks = new GameObject[MaxSkills];
            if (old != null)
                for (int i = 0; i < MaxSkills && i < old.Length; i++) selectedMarks[i] = old[i];
        }
    }

    GameObject GetOrBindSelectedMark(int i)
    {
        if (i < 0 || i >= MaxSkills) return null;
        if (selectedMarks[i] != null) return selectedMarks[i];
        if (skillButtons[i] == null) return null;
        var t = skillButtons[i].transform.Find("选中")
                ?? skillButtons[i].transform.Find("Selected")
                ?? skillButtons[i].transform.Find("Select");
        if (t != null) selectedMarks[i] = t.gameObject;
        return selectedMarks[i];
    }

    void Wire()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        for (int i = 0; i < MaxSkills; i++)
        {
            int idx = i;
            if (skillButtons[i] == null) continue;
            skillButtons[i].onClick.RemoveAllListeners();
            skillButtons[i].onClick.AddListener(() => Select(idx));
            skillButtons[i].transition = Selectable.Transition.None;
        }
    }

    public void AutoBind()
    {
        closeButton = transform.Find("Panel/CloseButton")?.GetComponent<Button>()
                      ?? transform.Find("CloseButton")?.GetComponent<Button>();
        titleText = FindTxt("Panel/Title") ?? FindTxt("Title");
        descText = FindTxt("Panel/DescText") ?? FindTxt("DescText");
        for (int i = 0; i < MaxSkills; i++)
        {
            var t = transform.Find($"Panel/Skills/Skill_{i}") ?? transform.Find($"Skills/Skill_{i}");
            if (t == null) continue;
            skillButtons[i] = t.GetComponent<Button>();
            skillIcons[i] = t.Find("Icon")?.GetComponent<Image>()
                            ?? t.Find("icon")?.GetComponent<Image>();
            skillNames[i] = t.Find("Label")?.GetComponent<Text>();
            var mark = t.Find("选中") ?? t.Find("Selected") ?? t.Find("Select");
            if (mark != null) selectedMarks[i] = mark.gameObject;
            if (skillIcons[i] == null)
                EnsureSkillIcon(i);
        }
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    void EnsureVisible()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 瘦屏（逻辑高 > 1280）兜底适配。主人的几何不许覆盖，这里只做两件兜底：
    ///   ① Dim 遮罩按 SkillSelectUI 自身 rect 等比放大（只放大不缩小），保证遮罩盖满不露边；
    ///   ② Skills 网格按 Skills 当前宽高算每行列数，行高溢出时只缩小 cellSize（只缩小不放大）。
    /// Panel / biaotou / Title / CloseButton / DescText / Skill_N 都在 Panel 内用相对锚点，
    /// Panel 不动它们自动跟随 —— 一个都不碰（尤其 Skill_N 的 localScale 0.8 绝不改）。
    /// 判据用项目既有 public 判据 UiLayoutStretch.IsThinnerScreen()（见 UiLayoutStretch.cs）。
    /// </summary>
    void ApplyFit()
    {
        if (!enableFit) return;
        if (_applying) return;

        // 只查不建：找不到节点就原样放弃，绝不在运行时新建任何节点。
        var dim = transform.Find("Dim") as RectTransform;
        var panel = transform.Find("Panel") as RectTransform;
        if (dim == null || panel == null) return;
        var skills = panel.Find("Skills") as RectTransform;
        if (skills == null) return;

        var grid = skills.GetComponent<GridLayoutGroup>();

        // 所有写 rect 之前必须先捕基准（只捕一次）。
        CaptureBase(dim, panel, skills, grid);

        _applying = true;
        try
        {
            // 非瘦屏：完整还原 base（主人的几何一寸不改）。
            if (!UiLayoutStretch.IsThinnerScreen())
            {
                RestoreRect(dim, _dimBase);
                RestoreRect(panel, _panelBase);
                RestoreRect(skills, _skillsBase);
                // 下面几项本文件从不写，diff 判定后实际是 no-op，仅保证「完整还原 base」。
                if (grid != null)
                {
                    if (grid.cellSize != _gridCellSize) grid.cellSize = _gridCellSize;
                    if (grid.spacing != _gridSpacing) grid.spacing = _gridSpacing;
                    if (grid.padding != _gridPadding) grid.padding = _gridPadding;
                    if (grid.constraint != _gridConstraint) grid.constraint = _gridConstraint;
                    if (grid.constraintCount != _gridConstraintCount) grid.constraintCount = _gridConstraintCount;
                }
                return;
            }

            // ① Dim 遮罩覆盖保证：整体等比缩放，绝不拉伸（只写 sizeDelta，anchor/pivot/anchoredPosition 一律不动）。
            var self = transform as RectTransform;
            float rootW = self != null ? self.rect.width : 0f;
            float rootH = self != null ? self.rect.height : 0f;
            if (rootW > 1f && rootH > 1f)
            {
                float k = Mathf.Max(rootW / 800f, rootH / 1400f);
                if (k > 1.001f)
                    dim.sizeDelta = new Vector2(800f * k, 1400f * k);
                else
                    dim.sizeDelta = _dimBase.sizeDelta;   // 只放大不缩小
            }

            // ② Skills 网格防溢出：只缩小格子，绝不放大；Skills 自身 sizeDelta / anchor / pos 一律不许改。
            if (grid != null)
            {
                float w = skills.rect.width;
                float h = skills.rect.height;
                float c = _gridCellSize.x;
                Vector2 s = _gridSpacing;
                if (w > 1f && h > 1f && c + s.x > 0.001f)
                {
                    int perRow = Mathf.Max(1, Mathf.FloorToInt((w + s.x) / (c + s.x)));
                    int rows = Mathf.Max(1, Mathf.CeilToInt((float)MaxSkills / perRow));
                    float need = rows * c + (rows - 1) * s.y;
                    if (need > h)
                    {
                        float nc = (h + s.y) / rows - s.y;
                        if (nc > 0f && nc < c)
                            grid.cellSize = new Vector2(nc, nc);
                        else
                            grid.cellSize = _gridCellSize;
                    }
                    else
                    {
                        grid.cellSize = _gridCellSize;   // 放得下就回到基准尺寸
                    }
                }
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>尺寸变化时重算（带 _applying 守卫防递归）。</summary>
    void OnRectTransformDimensionsChange()
    {
        if (_applying) return;
        ApplyFit();
    }

    /// <summary>只捕一次：记下预制体原始几何，之后每次都从 base 重算，保证幂等且可完整还原。</summary>
    void CaptureBase(RectTransform dim, RectTransform panel, RectTransform skills, GridLayoutGroup grid)
    {
        if (_fitCaptured) return;
        _fitCaptured = true;
        _dimBase = Snapshot(dim);
        _panelBase = Snapshot(panel);
        _skillsBase = Snapshot(skills);
        if (grid != null)
        {
            _gridCellSize = grid.cellSize;
            _gridSpacing = grid.spacing;
            _gridPadding = grid.padding;
            _gridConstraint = grid.constraint;
            _gridConstraintCount = grid.constraintCount;
        }
    }

    static FitBase Snapshot(RectTransform rt)
    {
        return new FitBase
        {
            anchorMin = rt.anchorMin,
            anchorMax = rt.anchorMax,
            pivot = rt.pivot,
            sizeDelta = rt.sizeDelta,
            anchoredPosition = rt.anchoredPosition,
        };
    }

    /// <summary>还原 base：Panel / Skills 本文件从不写，故这里实际是 no-op，仅作兜底；只有 Dim 与 cellSize 会被真正改回。</summary>
    static void RestoreRect(RectTransform rt, FitBase b)
    {
        if (rt == null) return;
        if (rt.anchorMin != b.anchorMin) rt.anchorMin = b.anchorMin;
        if (rt.anchorMax != b.anchorMax) rt.anchorMax = b.anchorMax;
        if (rt.pivot != b.pivot) rt.pivot = b.pivot;
        if (rt.sizeDelta != b.sizeDelta) rt.sizeDelta = b.sizeDelta;
        if (rt.anchoredPosition != b.anchoredPosition) rt.anchoredPosition = b.anchoredPosition;
    }

    /// <summary>编辑器建树</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var dim = CreateImg(transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        StretchFull(dim.rectTransform);

        var panel = CreateImg(transform, "Panel", new Color(0.32f, 0.2f, 0.12f, 1f));
        Set(panel.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 40f, 620f, 360f);

        var titleBg = CreateImg(panel.transform, "TitleBg", new Color(0.25f, 0.45f, 0.75f, 1f));
        Set(titleBg.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -16f, 220f, 40f);
        var title = CreateTxt(panel.transform, "Title", "技能", 28, Color.white);
        Set(title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -16f, 220f, 40f);

        var close = CreateImg(panel.transform, "CloseButton", new Color(0.75f, 0.2f, 0.18f, 1f));
        Set(close.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -12f, -12f, 44f, 44f);
        close.gameObject.AddComponent<Button>().targetGraphic = close;
        var cx = CreateTxt(close.transform, "X", "X", 26, Color.white);
        StretchFull(cx.rectTransform);

        var skills = new GameObject("Skills", typeof(RectTransform));
        skills.transform.SetParent(panel.transform, false);
        var srt = skills.GetComponent<RectTransform>();
        Set(srt, 0.5f, 0.55f, 0.5f, 0.55f, 0.5f, 0.5f, 0f, 20f, 560f, 96f);

        for (int i = 0; i < MaxSkills; i++)
        {
            float x = -230f + i * 92f;
            var sk = CreateImg(skills.transform, "Skill_" + i, new Color(0.55f, 0.4f, 0.25f, 1f));
            Set(sk.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, x, 0f, 80f, 80f);
            sk.gameObject.AddComponent<Button>().targetGraphic = sk;
            var slot = PlayerSkillDefs.Get(i);
            var icon = CreateImg(sk.transform, "Icon", slot != null ? slot.tint : Color.white);
            Set(icon.rectTransform, 0.5f, 0.55f, 0.5f, 0.55f, 0.5f, 0.5f, 0f, 4f, 48f, 48f);
            var lab = CreateTxt(sk.transform, "Label", slot != null ? slot.displayName : "", 14, Color.white);
            Set(lab.rectTransform, 0f, 0f, 1f, 0.28f, 0.5f, 0f, 0f, 2f, 0f, 0f);
        }

        var desc = CreateTxt(panel.transform, "DescText", "恢复 30% 最大生命。冷却 12 秒。血量危险时手动点击。", 24, new Color(1f, 0.95f, 0.85f));
        desc.alignment = TextAnchor.UpperLeft;
        Set(desc.rectTransform, 0.5f, 0f, 0.5f, 0f, 0.5f, 0f, 0f, 28f, 540f, 100f);

        AutoBind();
        Wire();
        GameFonts.ApplyToHierarchy(transform);
        gameObject.SetActive(false);
    }

    static Image CreateImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(p, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform p, string n, string t, int size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(p, false);
        var tx = go.GetComponent<Text>();
        tx.text = t;
        tx.fontSize = size;
        tx.color = c;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.raycastTarget = false;
        tx.font = GameFonts.GetChinese();
        return tx;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Set(RectTransform rt, float aminX, float aminY, float amaxX, float amaxY,
        float px, float py, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(aminX, aminY);
        rt.anchorMax = new Vector2(amaxX, amaxY);
        rt.pivot = new Vector2(px, py);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
