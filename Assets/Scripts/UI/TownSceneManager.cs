using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Town 场景：加载主界面 GuildHallUI。
/// 流程入口之一：Boot → Town(主界面) → 冒险 → Battle。
/// </summary>
public class TownSceneManager : MonoBehaviour
{
    [Header("UI颜色")]
    public Color buttonBgColor = new Color(0.1f, 0.1f, 0.1f, 1f); // 接近黑色
    public Color buttonTextColor = Color.white;
    public Color titleColor = new Color(1f, 0.85f, 0.3f); // 金色

    private Font _font;

    void Awake()
    {
        // 城镇菜单以中文为主，用 fusion-pixel
        _font = GameFonts.GetChinese();
        Debug.Log($"[TownScene] 字体加载: {(_font != null ? _font.name : "失败")}");

        // 确保相机存在
        if (Camera.main == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            camGo.AddComponent<AudioListener>();
        }

        // 确保EventSystem存在
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject hallPrefab = Resources.Load<GameObject>("Prefabs/Town/GuildHallUI");
        if (hallPrefab != null)
        {
            GameObject hall = Instantiate(hallPrefab);
            UICanvasSetup.ApplyOn(hall, UICanvasSetup.ResolveUiCamera());
            if (hall.GetComponent<TownSceneBootstrap>() == null)
                hall.AddComponent<TownSceneBootstrap>();
            StartCoroutine(CoNotifyStoryReady());
            Debug.Log("[TownScene] 已加载 GuildHallUI 预制体（统一 Canvas 720×1280 Match Height）");
            return;
        }

        Debug.LogWarning("[TownScene] 未找到 GuildHallUI，使用简易菜单。请运行 Tools/生成公会大厅预制体");
        CreateTownUI();
    }

    IEnumerator CoNotifyStoryReady()
    {
        yield return null;
        yield return null;
        float t = 0f;
        while (SceneLoadingCoordinator.IsActive && t < 12f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;
        Debug.Log("[TownScene] 通知剧情：大厅已就绪");
        TutorialDirector.Instance?.NotifyTownReady();
    }

    void CreateTownUI()
    {
        // Canvas（统一规范：Camera / 720×1280 / Match Height）
        GameObject canvasGo = new GameObject("TownCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownBootstrap);

        // 背景图（半透明黑色遮罩）
        GameObject bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);

        // 游戏标题
        GameObject titleGo = new GameObject("Title", typeof(Text));
        titleGo.transform.SetParent(canvasGo.transform, false);
        Text titleText = titleGo.GetComponent<Text>();
        titleText.text = "像素冒险：裂隙之刃";
        titleText.font = _font;
        titleText.fontSize = 72;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = titleColor;
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.7f);
        titleRt.anchorMax = new Vector2(1, 0.9f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // 按钮容器
        GameObject btnPanel = new GameObject("ButtonPanel", typeof(RectTransform));
        btnPanel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = btnPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.2f, 0.2f);
        panelRt.anchorMax = new Vector2(0.8f, 0.6f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        float btnHeight = 100f;
        float spacing = 30f;

        // 开始冒险按钮
        CreateButton(btnPanel.transform, "开始冒险", 0, btnHeight, spacing, OnStartAdventure);

        // 商店按钮
        CreateButton(btnPanel.transform, "商店", 1, btnHeight, spacing, OnOpenShop);

        // 天赋按钮
        CreateButton(btnPanel.transform, "天赋升级", 2, btnHeight, spacing, OnOpenTalent);

        // 设置按钮
        CreateButton(btnPanel.transform, "设置", 3, btnHeight, spacing, OnOpenSettings);
    }

    Button CreateButton(Transform parent, string label, int index, float height, float spacing, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(label, typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        float y = -(index * (height + spacing));
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, height);

        Image img = btnGo.GetComponent<Image>();
        img.color = buttonBgColor;
        img.type = Image.Type.Sliced;

        // 圆角：尝试加载默认圆角sprite，失败就用普通
        // 注：Unity默认有圆角sprite在UI/Default资源中
        // 这里用纯色即可，后续可在编辑器中替换为圆角图

        // 文字
        GameObject txtGo = new GameObject("Text", typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        Text txt = txtGo.GetComponent<Text>();
        txt.text = label;
        txt.font = _font;
        txt.fontSize = 40;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = buttonTextColor;
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        Button btn = btnGo.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    void OnStartAdventure()
    {
        GameSceneManager.Instance?.LoadBattleScene();
    }

    void OnOpenShop()
    {
        Debug.Log("[Town] 商店（待实现）");
    }

    void OnOpenTalent()
    {
        if (TalentUI.Instance != null)
        {
            TalentUI.Instance.Show();
            return;
        }
        var prefab = Resources.Load<GameObject>("Prefabs/Talent/TalentUI");
        if (prefab == null)
        {
            Debug.LogWarning("[Town] 缺少 Prefabs/Talent/TalentUI");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "TalentUI";
        var ui = go.GetComponent<TalentUI>();
        if (ui != null) ui.Show();
    }

    void OnOpenSettings()
    {
        BattleSettingsPanel.Ensure().Open(SettingsHost.Town);
    }
}
