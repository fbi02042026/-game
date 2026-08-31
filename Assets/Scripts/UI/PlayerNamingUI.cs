using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家起名弹窗（引导签名后）。预制体：Resources/Prefabs/Town/PlayerNamingUI。
/// Canvas 走 <see cref="UICanvasSetup"/>（Screen Space - Camera），与城镇页一致。
/// </summary>
public class PlayerNamingUI : MonoBehaviour
{
    public static PlayerNamingUI Instance { get; private set; }

    const string PrefabPath = ContentPaths.Prefab.PlayerNaming;
    const string ResRoot = ContentPaths.Ui.PlayerNaming;

    [Header("绑定（预制体 / 生成器写入）")]
    public GameObject contentRoot;
    public InputField nameInput;
    public Button diceButton;
    public Button confirmButton;

    InputField _input;
    Action _onDone;

    public bool IsOpen => contentRoot != null && contentRoot.activeSelf;

    public static void Show(Action onDone = null)
    {
        if (StoryProgress.HasPlayerName())
        {
            onDone?.Invoke();
            return;
        }
        Ensure().Open(onDone);
    }

    public static void HideIfOpen()
    {
        if (Instance != null)
            Instance.Close();
    }

    static PlayerNamingUI Ensure()
    {
        if (Instance != null)
        {
            Instance.EnsureCanvasShell();
            return Instance;
        }

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "PlayerNamingUI";
        }
        else
        {
            go = new GameObject("PlayerNamingUI");
            var ui = go.AddComponent<PlayerNamingUI>();
            ui.BuildHierarchyForPrefab();
        }

        DontDestroyOnLoad(go);
        return go.GetComponent<PlayerNamingUI>() ?? go.AddComponent<PlayerNamingUI>();
    }

    void Awake()
    {
        Instance = this;
        EnsureCanvasShell();
        BindReferences();
        StretchContentRoot();
        WireButtons();
        if (contentRoot != null)
            contentRoot.SetActive(false);
    }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(Action onDone)
    {
        _onDone = onDone;
        gameObject.SetActive(true);
        EnsureCanvasShell();
        BindReferences();
        WireButtons();
        if (contentRoot == null)
        {
            BuildHierarchyForPrefab();
            BindReferences();
            WireButtons();
        }
        if (contentRoot == null || _input == null)
        {
            Debug.LogWarning("[PlayerNamingUI] UI 未就绪");
            Finish();
            return;
        }

        DialogueUI.Instance?.Hide();
        _input.text = PlayerNameGen.Roll();
        ClearInputSelection();
        contentRoot.SetActive(true);
        EnsureCanvasShell();
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        GameFonts.ApplyToHierarchy(transform);
    }

    void EnsureCanvasShell()
    {
        StretchContentRoot();
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryNaming);
    }

    static void FixRootRectTransform(RectTransform rt) => UICanvasSetup.EnsureRootStretch(rt);

    void StretchContentRoot()
    {
        if (contentRoot == null)
            contentRoot = transform.Find("Root")?.gameObject;
        if (contentRoot == null) return;
        Stretch(contentRoot.GetComponent<RectTransform>());
    }

    void BindReferences()
    {
        if (contentRoot == null)
            contentRoot = transform.Find("Root")?.gameObject;
        if (nameInput == null && contentRoot != null)
            nameInput = contentRoot.transform.Find("Panel/InputBar/Input")?.GetComponent<InputField>();
        if (diceButton == null && contentRoot != null)
            diceButton = contentRoot.transform.Find("Panel/InputBar/DiceBtn")?.GetComponent<Button>();
        if (confirmButton == null && contentRoot != null)
            confirmButton = contentRoot.transform.Find("Panel/ConfirmBtn")?.GetComponent<Button>();
        _input = nameInput;
    }

    void WireButtons()
    {
        if (diceButton != null)
        {
            diceButton.onClick.RemoveListener(OnRoll);
            diceButton.onClick.AddListener(OnRoll);
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirm);
            confirmButton.onClick.AddListener(OnConfirm);
        }
    }

    /// <summary>编辑器生成预制体时调用；运行时缺预制体也会回退建树。</summary>
    public void BuildHierarchyForPrefab()
    {
        var existing = transform.Find("Root");
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        contentRoot = new GameObject("Root", typeof(RectTransform));
        contentRoot.transform.SetParent(transform, false);
        Stretch(contentRoot.GetComponent<RectTransform>());

        var dim = MkImg(contentRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
        Stretch(dim.GetComponent<RectTransform>());

        var panelSp = LoadSprite("panel");
        float panelW = 680f;
        float panelH = panelSp != null ? panelW * (panelSp.rect.height / panelSp.rect.width) : 760f;

        var panel = MkImg(contentRoot.transform, "Panel", Color.white);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(panelW, panelH);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panel.GetComponent<Image>();
        if (panelSp != null)
        {
            panelImg.sprite = panelSp;
            panelImg.preserveAspect = true;
            panelImg.type = Image.Type.Simple;
        }
        else
            panelImg.color = new Color(0.45f, 0.36f, 0.24f, 1f);

        MkText(panel.transform, "Title", "请签个名吧", 34,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -panelH * 0.17f),
            new Vector2(panelW - 80f, 52f), new Color(0.12f, 0.08f, 0.05f, 1f));

        MkText(panel.transform, "Subtitle", "✦ 为你的冒险者起一个传说般的名字 ✦", 20,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -panelH * 0.24f),
            new Vector2(panelW - 100f, 40f), new Color(0.28f, 0.2f, 0.14f, 1f));

        var barSp = LoadSprite("input_bar");
        float barW = panelW * 0.78f;
        float barH = barSp != null ? barW * (barSp.rect.height / barSp.rect.width) : 64f;
        var bar = MkImg(panel.transform, "InputBar", Color.white);
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0.5f);
        barRt.pivot = new Vector2(0.5f, 0.5f);
        barRt.sizeDelta = new Vector2(barW, barH);
        barRt.anchoredPosition = new Vector2(0f, panelH * 0.02f);
        var barImg = bar.GetComponent<Image>();
        if (barSp != null)
        {
            barImg.sprite = barSp;
            barImg.preserveAspect = true;
        }
        else
            barImg.color = new Color(0.2f, 0.12f, 0.08f, 1f);

        var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputGo.transform.SetParent(bar.transform, false);
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.04f, 0.12f);
        inputRt.anchorMax = new Vector2(0.78f, 0.88f);
        inputRt.offsetMin = inputRt.offsetMax = Vector2.zero;
        inputGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGo.transform.SetParent(inputGo.transform, false);
        Stretch(phGo.GetComponent<RectTransform>());
        var placeholder = phGo.GetComponent<Text>();
        placeholder.text = "艾伦·晨风";
        placeholder.fontSize = 24;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.75f, 0.7f, 0.62f, 0.55f);
        placeholder.supportRichText = false;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(inputGo.transform, false);
        Stretch(txtGo.GetComponent<RectTransform>());
        var txt = txtGo.GetComponent<Text>();
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        txt.supportRichText = false;

        nameInput = inputGo.GetComponent<InputField>();
        nameInput.textComponent = txt;
        nameInput.placeholder = placeholder;
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.characterLimit = 12;
        nameInput.contentType = InputField.ContentType.Standard;
        _input = nameInput;

        diceButton = MkBtn(bar.transform, "DiceBtn", null, new Color(1f, 1f, 1f, 0.01f));
        var diceRt = diceButton.GetComponent<RectTransform>();
        diceRt.anchorMin = new Vector2(0.8f, 0.08f);
        diceRt.anchorMax = new Vector2(0.96f, 0.92f);
        diceRt.offsetMin = diceRt.offsetMax = Vector2.zero;

        var confirmSp = LoadSprite("confirm_btn");
        float btnW = panelW * 0.42f;
        float btnH = confirmSp != null ? btnW * (confirmSp.rect.height / confirmSp.rect.width) : 58f;
        confirmButton = MkBtn(panel.transform, "ConfirmBtn", confirmSp, Color.white);
        var confirmRt = confirmButton.GetComponent<RectTransform>();
        confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRt.pivot = new Vector2(0.5f, 0.5f);
        confirmRt.sizeDelta = new Vector2(btnW, btnH);
        confirmRt.anchoredPosition = new Vector2(0f, -panelH * 0.14f);
        if (confirmSp != null)
        {
            confirmButton.GetComponent<Image>().preserveAspect = true;
            MkText(confirmButton.transform, "Label", "确定", 26,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.12f, 0.06f, 1f));
        }
        else
        {
            confirmButton.GetComponent<Image>().color = new Color(0.75f, 0.58f, 0.2f, 1f);
            MkText(confirmButton.transform, "Label", "确定", 26,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        }

        contentRoot.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);
    }

    void OnRoll()
    {
        if (_input == null) return;
        _input.text = PlayerNameGen.Roll();
        ClearInputSelection();
    }

    /// <summary>填入随机名后取消全选，避免 InputField 白底高亮。</summary>
    void ClearInputSelection()
    {
        int len = string.IsNullOrEmpty(_input.text) ? 0 : _input.text.Length;
        _input.caretPosition = len;
        _input.selectionAnchorPosition = len;
        _input.selectionFocusPosition = len;
    }

    void OnConfirm()
    {
        string raw = _input != null ? _input.text : "";
        if (!PlayerNameGen.TryValidate(raw, out string cleaned, out string error))
        {
            UIManager.Instance?.ShowToast(error);
            return;
        }

        StoryProgress.SetPlayerName(cleaned);
        Finish();
    }

    void Finish()
    {
        if (contentRoot != null) contentRoot.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke();
    }

    void Close() => Finish();

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject MkImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static Button MkBtn(Transform parent, string name, Sprite sp, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = color;
        return go.GetComponent<Button>();
    }

    static Text MkText(Transform parent, string name, string content, int size,
        Vector2 amin, Vector2 amax, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Sprite LoadSprite(string id)
    {
        var sp = Resources.Load<Sprite>(ResRoot + "/" + id);
        if (sp != null) return sp;
#if UNITY_EDITOR
        string path = "Assets/Art/UI/玩家起名/" + id switch
        {
            "panel" => "背景 拷贝.png",
            "input_bar" => "图层 1.png",
            "confirm_btn" => "图层 2.png",
            "dice" => "图层 3.png",
            _ => id + ".png",
        };
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }
}
