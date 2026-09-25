#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 冒险者公会大厅 UI 预制体生成器
/// 菜单：Tools/生成公会大厅预制体
/// </summary>
public class GuildHallPrefabGenerator
{
    static readonly Color C_BG = new Color(0.08f, 0.06f, 0.05f, 1f);
    static readonly Color C_PANEL = new Color(0.15f, 0.12f, 0.1f, 0.85f);
    static readonly Color C_GOLD = new Color(1f, 0.85f, 0.35f);
    static readonly Color C_LABEL = new Color(0.95f, 0.9f, 0.75f);
    static readonly Color C_PLACEHOLDER = new Color(0.35f, 0.3f, 0.25f, 0.55f);
    static readonly Color C_NAV_SEL = new Color(0.25f, 0.45f, 0.75f, 0.9f);
    static readonly Color C_NAV = new Color(0.2f, 0.18f, 0.15f, 0.9f);

    [MenuItem("Tools/_归档/生成公会大厅预制体")]
    public static void Generate()
    {
        string dir = "Assets/Resources/Prefabs/Town";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");

        GameObject root = new GameObject("GuildHallUI");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.sortingOrder = 0;
        // 统一规范：Camera / 720×1280 / Match Height（运行时再绑 Main Camera）
        UICanvasSetup.Apply(canvas, null);
        GuildHallUI hall = root.AddComponent<GuildHallUI>();

        // 全屏背景（换大厅插画）
        Image bg = CreateImage(root.transform, "Background", C_BG);
        Stretch(bg.rectTransform);

        // 中央场景层（换像素公会内景）
        Image sceneBg = CreateImage(root.transform, "HallScene", C_PLACEHOLDER);
        Stretch(sceneBg.rectTransform);
        sceneBg.rectTransform.offsetMin = new Vector2(0, 140);
        sceneBg.rectTransform.offsetMax = new Vector2(0, -160);

        // === 顶部 ===
        GameObject topBar = CreateRect(root.transform, "TopBar");
        SetAnchored(topBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 100));
        Image topBg = CreateImage(topBar.transform, "TopBarBg", new Color(0, 0, 0, 0.35f));
        Stretch(topBg.rectTransform);

        GameObject titleBadge = CreateRect(topBar.transform, "TitleBadge");
        SetAnchored(titleBadge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(220, 70));
        CreateImage(titleBadge.transform, "BadgeBg", C_PANEL);
        Text title = CreateText(titleBadge.transform, "TitleText", "皇家冒险者公会", 28, C_GOLD);
        Stretch(title.rectTransform);

        GameObject goldPanel = CreateRect(topBar.transform, "GoldPanel");
        SetAnchored(goldPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(260, 56));
        CreateImage(goldPanel.transform, "GoldBg", C_PANEL);
        Image coin = CreateImage(goldPanel.transform, "CoinIcon", C_GOLD);
        SetAnchored(coin.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(40, 40));
        Text goldText = CreateText(goldPanel.transform, "GoldText", "125,430", 26, C_GOLD);
        SetAnchored(goldText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(160, 40));
        goldText.alignment = TextAnchor.MiddleCenter;
        Button plusBtn = CreateButton(goldPanel.transform, "PlusButton", new Vector2(44, 44), new Color(0.3f, 0.55f, 0.9f, 1f));
        SetAnchored(plusBtn.gameObject, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(44, 44));
        plusBtn.GetComponentInChildren<Text>().text = "+";
        hall.goldText = goldText;
        hall.goldPlusButton = plusBtn;

        // === 左侧按钮 ===
        GameObject leftBar = CreateRect(root.transform, "LeftBar");
        SetAnchored(leftBar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 40), new Vector2(80, 320));
        hall.mailButton = CreateSideButton(leftBar.transform, "MailButton", "邮件", 0);
        hall.noticeButton = CreateSideButton(leftBar.transform, "NoticeButton", "公告", 1);
        hall.activityButton = CreateSideButton(leftBar.transform, "ActivityButton", "活动", 2);

        // === 右侧按钮 ===
        GameObject rightBar = CreateRect(root.transform, "RightBar");
        SetAnchored(rightBar, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-50, 40), new Vector2(80, 320));
        hall.rankButton = CreateSideButton(rightBar.transform, "RankButton", "排行榜", 0);
        hall.shopButton = CreateSideButton(rightBar.transform, "ShopButton", "商城", 1);
        hall.settingsButton = CreateSideButton(rightBar.transform, "SettingsButton", "设置", 2);

        // === 中央热点（可点区域，换图） ===
        Transform scene = sceneBg.transform;
        hall.noticeBoardButton = CreateHotspot(scene, "NoticeBoard", new Vector2(-220, 80), new Vector2(160, 200), "公告板");
        hall.licenseHallButton = CreateHotspot(scene, "LicenseHall", new Vector2(0, 120), new Vector2(200, 140), "执照大厅");
        hall.armoryButton = CreateHotspot(scene, "Armory", new Vector2(220, 60), new Vector2(160, 200), "武器库");
        hall.receptionistButton = CreateHotspot(scene, "Receptionist", new Vector2(0, -40), new Vector2(180, 220), "公会看板娘");

        GameObject bubble = CreateRect(scene, "SpeechBubble");
        SetAnchored(bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(280, 70));
        CreateImage(bubble.transform, "BubbleBg", new Color(1f, 0.97f, 0.88f, 0.95f));
        Text bubbleTxt = CreateText(bubble.transform, "BubbleText", "欢迎来到皇家冒险者公会!", 20, new Color(0.35f, 0.25f, 0.15f));
        Stretch(bubbleTxt.rectTransform);

        // === 底部导航 ===
        GameObject bottomNav = CreateRect(root.transform, "BottomNav");
        SetAnchored(bottomNav, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 70), new Vector2(0, 140));
        var navHlg = bottomNav.gameObject.AddComponent<HorizontalLayoutGroup>();
        navHlg.padding = new RectOffset(16, 16, 8, 8);
        navHlg.spacing = 8;
        navHlg.childAlignment = TextAnchor.MiddleCenter;
        navHlg.childForceExpandWidth = true;
        navHlg.childForceExpandHeight = true;

        hall.navGuildButton = CreateNavButton(bottomNav.transform, "NavGuild", "公会", true);
        hall.navCharacterButton = CreateNavButton(bottomNav.transform, "NavCharacter", "角色", false);
        hall.navAdventureButton = CreateNavButton(bottomNav.transform, "NavAdventure", "冒险", false);
        hall.navTavernButton = CreateNavButton(bottomNav.transform, "NavTavern", "酒馆", false);
        hall.navLogButton = CreateNavButton(bottomNav.transform, "NavLog", "冒险日志", false);

        string pathPrefab = $"{dir}/GuildHallUI.prefab";
        GameFonts.ApplyToHierarchy(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, pathPrefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GuildHallPrefabGenerator] 已生成: {pathPrefab}");
        EditorUtility.DisplayDialog("公会大厅", "GuildHallUI 预制体已生成，可在 Resources/Prefabs/Town 替换美术资源。", "OK");
    }

    static Button CreateHotspot(Transform parent, string name, Vector2 pos, Vector2 size, string label)
    {
        GameObject go = CreateRect(parent, name);
        SetAnchored(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        CreateImage(go.transform, "HotspotBg", C_PLACEHOLDER);
        Text t = CreateText(go.transform, "Label", label, 18, C_LABEL);
        Stretch(t.rectTransform);
        return go.AddComponent<Button>();
    }

    static Button CreateSideButton(Transform parent, string name, string label, int index)
    {
        GameObject go = CreateRect(parent, name);
        SetAnchored(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50 - index * 100), new Vector2(72, 72));
        CreateImage(go.transform, "Icon", C_PANEL);
        Text t = CreateText(go.transform, "Label", label, 16, C_LABEL);
        SetAnchored(t.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -38), new Vector2(80, 24));
        return go.AddComponent<Button>();
    }

    static Button CreateNavButton(Transform parent, string name, string label, bool selected)
    {
        GameObject go = CreateRect(parent, name);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 100;
        Image bg = CreateImage(go.transform, "NavBg", selected ? C_NAV_SEL : C_NAV);
        Stretch(bg.rectTransform);
        Image icon = CreateImage(go.transform, "Icon", C_PLACEHOLDER);
        SetAnchored(icon.gameObject, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(56, 56));
        Text t = CreateText(go.transform, "Label", label, 18, C_GOLD);
        SetAnchored(t.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(120, 28));
        return go.AddComponent<Button>();
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.font = GameFonts.GetChinese();
        t.alignment = TextAnchor.MiddleCenter;
        return t;
    }

    /// <summary>数字控件请用此方法，或事后 GameFonts.ApplyToHierarchy。</summary>
    static Text CreateNumberText(Transform parent, string name, string content, int size, Color color)
    {
        var t = CreateText(parent, name, content, size, color);
        t.font = GameFonts.GetNumber();
        return t;
    }

    static Button CreateButton(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        Text txt = CreateText(go.transform, "Text", "", 22, Color.white);
        Stretch(txt.rectTransform);
        return go.GetComponent<Button>();
    }

    static void SetAnchored(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
