using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能选择弹层（参考 Art/UI/Character/skill_select_reference.png）。
/// 6 个技能格 + 说明；由角色页右侧「技能」或左侧独立按钮打开。
/// </summary>
public class SkillSelectUI : MonoBehaviour
{
    public const int MaxSkills = 6;

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

    void Awake()
    {
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

    void RefreshSlots()
    {
        var data = SaveSystem.Instance?.Data;
        for (int i = 0; i < MaxSkills && i < PlayerSkillDefs.All.Length; i++)
        {
            var def = PlayerSkillDefs.All[i];
            bool unlocked = PlayerSkillDefs.IsUnlocked(def, data);
            if (skillNames[i] != null)
                skillNames[i].text = def.displayName;
            ApplySlotTint(i, unlocked);
            if (skillButtons[i] != null)
                skillButtons[i].interactable = true;
        }
        ApplySelectedMarks(_selected);
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
            skillNames[i].color = unlocked ? Color.white : LockedTint;
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
            skillIcons[i] = t.Find("Icon")?.GetComponent<Image>();
            skillNames[i] = t.Find("Label")?.GetComponent<Text>();
            var mark = t.Find("选中") ?? t.Find("Selected") ?? t.Find("Select");
            if (mark != null) selectedMarks[i] = mark.gameObject;
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
            var icon = CreateImg(sk.transform, "Icon", PlayerSkillDefs.Get(i).tint);
            Set(icon.rectTransform, 0.5f, 0.55f, 0.5f, 0.55f, 0.5f, 0.5f, 0f, 4f, 48f, 48f);
            var lab = CreateTxt(sk.transform, "Label", PlayerSkillDefs.Get(i).displayName, 14, Color.white);
            Set(lab.rectTransform, 0f, 0f, 1f, 0.28f, 0.5f, 0f, 0f, 2f, 0f, 0f);
        }

        var desc = CreateTxt(panel.transform, "DescText", "这里是技能名称和介绍", 24, new Color(1f, 0.95f, 0.85f));
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
