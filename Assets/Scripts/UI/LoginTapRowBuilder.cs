using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 登录页「TapTap 登录 + 游客继续」按钮行（运行时构建，不改预制体）。
///
/// 【为什么运行时建】项目红线：不动美术排版、不重跑预制体生成器。
/// 且实测 Assets/Resources/Prefabs/Login/LoginUI.prefab 内并无 GuestButton 节点
/// （ActionPanel 只有 StartButton / OtherLoginLabel / SocialRow），故只能运行时补。
///
/// 【TapTap 按钮规范（官方硬性，违反会被渠道打回）】
/// · 必须使用官方素材，不可改样式（禁描边 / 渐变 / 投影 / 纹理）；
/// · 默认高度 50 pt，仅允许在宽高比不变的前提下缩放；
/// · 变体只能用「最大圆角半径 / 默认圆角矩形」，禁纯直角。
/// 素材：Assets/Resources/UI/Icons/TapTapLogin/TapLogin_纯净白_默认圆角矩形@2x.png
/// 官方 @1x = 270×50（宽高比 5.4:1），本项目画布 720×1280，直接用 @1x 逻辑尺寸。
///
/// 【防坑】素材 PNG 若被团结导入成 Texture2D（非 Sprite），Resources.Load&lt;Sprite&gt; 会返回 null。
/// 这里直接按 Texture2D 加载并用 Sprite.Create 生成，PPU 取 2（@2x图 ÷ 2 = 270×50 逻辑单位），
/// 这样无论导入类型如何都能出图，避免「按钮空白」这种静默失败。
/// </summary>
public static class LoginTapRowBuilder
{
    public const string TapButtonName = "TapLoginButton";
    public const string GuestButtonName = "GuestContinueButton";

    const string TapSpriteResPath = "UI/Icons/TapTapLogin/TapLogin_纯净白_默认圆角矩形@2x";

    // 官方 @1x 逻辑尺寸，禁止改比例
    const float TapButtonWidth = 270f;
    const float TapButtonHeight = 50f;

    // 位置：ActionPanel 内、StartButton 下方（StartButton 占 panel 顶部 102~225）
    const float TapButtonY = 78f;
    const float GuestButtonY = 24f;
    const float GuestButtonHeight = 40f;

    static Sprite _tapSprite;

    /// <summary>幂等：已建过就直接返回，绝不重复叠加。</summary>
    public static void Ensure(RectTransform actionPanel, UnityAction onTapLogin, UnityAction onGuest)
    {
        if (actionPanel == null)
        {
            Debug.LogError("[LoginTapRow] ActionPanel 为空，无法挂 TapTap 登录按钮");
            return;
        }

        // Transform.Find 返回 Transform，不是 GameObject
        Transform tapTf = actionPanel.Find(TapButtonName);
        if (tapTf == null)
        {
            tapTf = BuildTapButton(actionPanel).transform;
        }
        BindOnce(tapTf.gameObject, onTapLogin);

        Transform guestTf = actionPanel.Find(GuestButtonName);
        if (guestTf == null)
        {
            guestTf = BuildGuestButton(actionPanel).transform;
        }
        BindOnce(guestTf.gameObject, onGuest);
    }

    static void BindOnce(GameObject go, UnityAction action)
    {
        if (go == null || action == null) return;
        var btn = go.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveListener(action); // 防重复叠加
        btn.onClick.AddListener(action);
    }

    static GameObject BuildTapButton(RectTransform parent)
    {
        var go = new GameObject(TapButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(TapButtonWidth, TapButtonHeight);
        rt.anchoredPosition = new Vector2(0f, TapButtonY);

        var img = go.GetComponent<Image>();
        img.sprite = LoadTapSprite();
        img.type = UnityEngine.UI.Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = true;
        if (img.sprite == null)
        {
            // 素材丢失必须让主人看见，不静默出一个空气按钮
            Debug.LogError($"[LoginTapRow] 官方 TapTap 登录按钮素材加载失败：Resources/{TapSpriteResPath}。请确认该 PNG 已随包导入且类型可读。");
        }

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None; // 官方按钮禁染色/闪动效果

        return go;
    }

    static Sprite LoadTapSprite()
    {
        if (_tapSprite != null) return _tapSprite;

        var tex = Resources.Load<Texture2D>(TapSpriteResPath);
        if (tex == null)
        {
            Debug.LogError($"[LoginTapRow] Resources/{TapSpriteResPath} 未找到（Texture2D）");
            return null;
        }

        // PPU = 2：官方 @2x 图 ÷ 2 → 270×50 逻辑单位，正好是官方 @1x 规范尺寸
        _tapSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 2f);
        if (_tapSprite == null)
            Debug.LogError("[LoginTapRow] Sprite.Create 失败，TapTap 登录按钮无法显示");

        return _tapSprite;
    }

    static GameObject BuildGuestButton(RectTransform parent)
    {
        var go = new GameObject(GuestButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(TapButtonWidth, GuestButtonHeight);
        rt.anchoredPosition = new Vector2(0f, GuestButtonY);

        var img = go.GetComponent<Image>();
        // 半透明深色底，与官方白色按钮拉开层次；纯色无贴图，不涉及官方素材规范
        img.color = new Color(0f, 0f, 0f, 0.35f);
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.layer = go.layer;
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var txt = labelGo.GetComponent<Text>();
        txt.text = "游客继续";
        txt.font = GameFonts.GetChinese();
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 1f, 1f, 0.92f);
        txt.raycastTarget = false;

        GameFonts.ApplyToHierarchy(go.transform);
        return go;
    }
}