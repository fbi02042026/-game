using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：绑定与建树（AutoBind、编辑器 BuildHierarchyForPrefab、Find/Create 小件）
public partial class CharacterUI
{

    public void AutoBind()
    {
        titleText = FindTxt("Content/Header/TitleText");
        // 主人会把中文节点名改成英文：英文名优先、中文名回退，两种都能命中
        titleSubText = FindTxt("Content/Header/SubTitle")
                       ?? FindTxt("Content/Header/称号")
                       ?? FindTxt("Content/Header/TitleSub");
        headerAvatar = transform.Find("Content/Header/icon")?.GetComponent<Image>();
        talentButton = transform.Find("Content/Stage/RightButtons/TalentButton")?.GetComponent<Button>();
        skillButton = transform.Find("Content/Stage/RightButtons/SkillButton")?.GetComponent<Button>();
        // 2026-09-25 适配主人新层级：LeftSkillButton 已改名且暂时不要了，原节点在预制体里不复存在。
        // leftButtonRoot 只用于定位/参考（指向 LeftButton 组），代码不会调整它的显隐；
        // leftSkillButton 保持 null（不再查找、不再赋值），保留公开字段以免其它文件引用报错。
        leftButtonRoot = transform.Find("Content/Stage/LeftButton");
        portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        // 2026-09-25：flipPortraitButton 只绑旧名 Stage/FlipButton（现已不存在 → 为 null）；
        // 主人把「立绘」按钮改成了看板入口，已绑给 boardEntryButton，不可再绑给翻转。
        flipPortraitButton = transform.Find("Content/Stage/FlipButton")?.GetComponent<Button>();
        // 看板入口：主人改英文后优先 BoardButton，中文旧名「立绘」回退，再退回旧运行时生成的 BoardButton
        boardEntryButton = transform.Find("Content/Stage/RightButtons/BoardButton")?.GetComponent<Button>()
                           ?? transform.Find("Content/Stage/RightButtons/立绘")?.GetComponent<Button>();
        // 背包入口：2026-09-25 主人把按钮移到右列了 → 优先 Content/Stage/RightButtons，
        // 再回退左列（LeftButton），最后才是旧路径 Content/Stage/LeftButton。
        backpackEntryButton = transform.Find("Content/Stage/RightButtons/BackpackButton")?.GetComponent<Button>()
                              ?? transform.Find("Content/Stage/RightButtons/背包")?.GetComponent<Button>();
        if (backpackEntryButton == null && leftButtonRoot != null)
            backpackEntryButton = leftButtonRoot.Find("BackpackButton")?.GetComponent<Button>()
                                  ?? leftButtonRoot.Find("背包")?.GetComponent<Button>();
        if (backpackEntryButton == null)
            backpackEntryButton = transform.Find("Content/Stage/LeftButton/BackpackButton")?.GetComponent<Button>();
        EnsureCarriedSkillIcon();

        attrHpText = FindTxt("Content/AttrPanel/AttrHpRoot/AttrHp") ?? FindTxt("Content/AttrPanel/AttrHp");
        attrAtkText = FindTxt("Content/AttrPanel/AttrAtkRoot/AttrAtk") ?? FindTxt("Content/AttrPanel/AttrAtk");
        attrDefText = FindTxt("Content/AttrPanel/AttrDefRoot/AttrDef") ?? FindTxt("Content/AttrPanel/AttrDef");
        // 2026-09-29：魔攻 / 魔防节点（已规范命名为 AttrMagicAtkRoot/AttrMagicAtk 等），
        // 找不到就报错，不模糊匹配、不克隆。
        attrMagicAtkText = ResolveMagicAttrRow("AttrMagicAtk", "魔攻");
        attrMagicDefText = ResolveMagicAttrRow("AttrMagicDef", "魔防");
        attrSpdText = FindTxt("Content/AttrPanel/AttrSpdRoot/AttrSpd") ?? FindTxt("Content/AttrPanel/AttrSpd");
        attrCritText = FindTxt("Content/AttrPanel/AttrCritRoot/AttrCrit") ?? FindTxt("Content/AttrPanel/AttrCrit");
        attrResistText = FindTxt("Content/AttrPanel/AttrResistRoot/AttrResist") ?? FindTxt("Content/AttrPanel/AttrResist");

        EnsureMagicAttrIcons();

        // 界面内背包面板（「背包」按钮切换用；可能在预制体里被展开着，初始化时会收起）
        bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");
        bagCapacityText = FindTxt("Content/BackpackPanel/CapacityText")
                          ?? FindTxt("Content/BagPanel/CapacityText");
        bagExpandButton = transform.Find("Content/BackpackPanel/CapacityPlus")?.GetComponent<Button>()
                          ?? transform.Find("Content/BagPanel/CapacityPlus")?.GetComponent<Button>();
        // 角色页只用自己的专属网格；预制体上自带的通用 TownBackpackGrid 会被运行时停用
        var bag = ResolveBagGrid();
        if (useWideBackpackGrid)
        {
            bag.columns = WideBackpackColumns;
            bag.unlockedDisplayRows = WideBackpackUnlockedRows;
        }
        backpackGrid.BindFromHierarchy(transform);
        // 拿到网格后立刻套「8 列 / 下 2 行锁」的显示规格（只作用于本组件实例）
        ApplyWideBackpackGrid();

        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
        {
            skillSelect.onSkillSelected -= OnSkillSelectedFromPanel;
            skillSelect.onSkillSelected += OnSkillSelectedFromPanel;
        }
    }

    void OnSkillSelectedFromPanel(int _)
    {
        RefreshCarriedSkill();
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    /// <summary>
    /// 深度查找：在 root 的所有后代里（不含 root 自身）找第一个同名节点，找不到返回 null。
    /// 只给 transform.Find("硬编码路径") 当兜底 —— 以后主人再挪层级（例如把 SkillSelectUI
    /// 挪进 Popups）也不会断。先按精确匹配。
    /// </summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>编辑器首次建树；已换美术勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var content = CreateRect(transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(0f, BottomReserve);
        crt.offsetMax = new Vector2(0f, -TopReserve);

        // Header
        var header = CreateImg(content.transform, "Header", new Color(0.28f, 0.18f, 0.1f, 0.9f));
        Set(header.rectTransform, 0f, 1f, 1f, 1f, 0.5f, 1f, 0f, 0f, 0f, 56f);
        var title = CreateTxt(header.transform, "TitleText", "角色", 32, new Color(1f, 0.9f, 0.55f));
        Set(title.rectTransform, 0f, 0f, 0.4f, 1f, 0f, 0.5f, 24f, 0f, 0f, 0f);
        title.alignment = TextAnchor.MiddleLeft;

        // Stage：立绘 + 左技能钮 + 右天赋/技能（无装备栏）
        var stage = CreateImg(content.transform, "Stage", new Color(0.35f, 0.28f, 0.22f, 1f));
        var srt = stage.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.42f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(12f, 0f);
        srt.offsetMax = new Vector2(-12f, -64f);

        var portrait = CreateImg(stage.transform, "Portrait", new Color(0.45f, 0.5f, 0.55f, 0.85f));
        portrait.preserveAspect = true;
        Set(portrait.rectTransform, 0.5f, 0.15f, 0.5f, 0.15f, 0.5f, 0f, 0f, 0f, 160f, 220f);

        var leftBtn = CreateImg(stage.transform, "LeftSkillButton", new Color(0.4f, 0.32f, 0.55f, 1f));
        Set(leftBtn.rectTransform, 0f, 0.55f, 0f, 0.55f, 0f, 0.5f, 16f, 0f, 100f, 72f);
        leftBtn.gameObject.AddComponent<Button>().targetGraphic = leftBtn;
        var leftLab = CreateTxt(leftBtn.transform, "Label", "技能", 24, Color.white);
        Stretch(leftLab.rectTransform);

        var right = CreateRect(stage.transform, "RightButtons");
        Set(right.GetComponent<RectTransform>(), 1f, 0.2f, 1f, 0.95f, 1f, 1f, -12f, 0f, 120f, 0f);
        BuildSideBtn(right.transform, "TalentButton", "天赋", 0f, new Color(0.35f, 0.55f, 0.35f, 1f));
        BuildSideBtn(right.transform, "SkillButton", "技能", -90f, new Color(0.4f, 0.35f, 0.65f, 1f));

        // Attr panel — 宽度随界面
        var attrs = CreateImg(content.transform, "AttrPanel", new Color(0.22f, 0.16f, 0.12f, 0.95f));
        var art = attrs.rectTransform;
        art.anchorMin = new Vector2(0f, 0.30f);
        art.anchorMax = new Vector2(1f, 0.42f);
        art.offsetMin = new Vector2(12f, 4f);
        art.offsetMax = new Vector2(-12f, -4f);
        var attrTitle = CreateTxt(attrs.transform, "AttrTitle", "基础属性", 22, new Color(1f, 0.92f, 0.75f));
        Set(attrTitle.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -4f, 200f, 28f);
        CreateAttr(attrs.transform, "AttrHp", "生命", "5240", 0f);
        CreateAttr(attrs.transform, "AttrAtk", "攻击", "1280", 1f);
        CreateAttr(attrs.transform, "AttrDef", "防御", "860", 2f);
        CreateAttr(attrs.transform, "AttrSpd", "速度", "105", 3f);
        CreateAttr(attrs.transform, "AttrCrit", "暴击", "18%", 4f);
        CreateAttr(attrs.transform, "AttrResist", "抗性", "15%", 5f);

        // Bag — 宽度自适应，格子与战斗一致
        var bag = CreateImg(content.transform, "BagPanel", new Color(0.4f, 0.28f, 0.16f, 1f));
        var brt = bag.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0.30f);
        brt.offsetMin = new Vector2(12f, 8f);
        brt.offsetMax = new Vector2(-12f, -4f);
        var bagTitle = CreateTxt(bag.transform, "BagTitle", "背包", 24, new Color(1f, 0.92f, 0.75f));
        Set(bagTitle.rectTransform, 0f, 1f, 0f, 1f, 0f, 1f, 16f, -8f, 100f, 32f);
        bagTitle.alignment = TextAnchor.MiddleLeft;
        var cap = CreateTxt(bag.transform, "CapacityText", "0/21", 22, new Color(1f, 0.95f, 0.8f));
        Set(cap.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -56f, -8f, 100f, 32f);
        cap.alignment = TextAnchor.MiddleRight;
        var plus = CreateImg(bag.transform, "CapacityPlus", new Color(0.55f, 0.35f, 0.2f, 1f));
        Set(plus.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -12f, -8f, 32f, 32f);
        plus.gameObject.AddComponent<Button>().targetGraphic = plus;
        var plusL = CreateTxt(plus.transform, "Label", "+", 22, Color.white);
        Stretch(plusL.rectTransform);

        ResolveBagGrid();
        backpackGrid.BuildGrid(bag.transform);

        // 底部五入口占位（不自建按钮，仅留空给 SharedChrome）
        var reserve = CreateRect(transform, "BottomNavReserve");
        var rrt = reserve.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 0f);
        rrt.anchorMax = new Vector2(1f, 0f);
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.sizeDelta = new Vector2(0f, BottomReserve);
        rrt.anchoredPosition = Vector2.zero;

        // Skill select popup
        var skillGo = new GameObject("SkillSelectUI", typeof(RectTransform));
        skillGo.transform.SetParent(transform, false);
        Stretch(skillGo.GetComponent<RectTransform>());
        skillSelect = skillGo.AddComponent<SkillSelectUI>();
        skillSelect.BuildHierarchyForPrefab();

        AutoBind();
        GameFonts.ApplyToHierarchy(transform);
        _built = true;
    }

    static void BuildSideBtn(Transform parent, string name, string label, float y, Color col)
    {
        var img = CreateImg(parent, name, col);
        Set(img.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, 0f, y, 110f, 72f);
        img.gameObject.AddComponent<Button>().targetGraphic = img;
        var t = CreateTxt(img.transform, "Label", label, 24, Color.white);
        Stretch(t.rectTransform);
    }

    static void CreateAttr(Transform parent, string name, string label, string value, float index)
    {
        float x0 = index / 6f;
        float x1 = (index + 1f) / 6f;
        var root = CreateRect(parent, name + "Root");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, 0f);
        rt.anchorMax = new Vector2(x1, 0.7f);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var lab = CreateTxt(root.transform, "Label", label, 16, new Color(0.85f, 0.8f, 0.7f));
        Set(lab.rectTransform, 0f, 0.55f, 1f, 1f, 0.5f, 0.5f, 0f, 0f, 0f, 0f);
        // 数值节点名与 AutoBind 一致
        var val = CreateTxt(root.transform, name, value, 20, new Color(0.7f, 0.95f, 0.7f));
        Set(val.rectTransform, 0f, 0f, 1f, 0.55f, 0.5f, 0.5f, 0f, 0f, 0f, 0f);
    }

    static GameObject CreateRect(Transform p, string n)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        return go;
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

    static void Stretch(RectTransform rt)
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
