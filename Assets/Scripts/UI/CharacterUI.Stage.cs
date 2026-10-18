using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：立绘舞台（看板入口停用、立绘翻转/设置、影子对齐、画布配置、底图 Envelope）
public partial class CharacterUI
{

    /// <summary>
    /// 「看板」入口（2026-10-17 主人拍板：角色界面去掉切换人物立绘功能，入口停用）。
    /// 预制体里主人手摆的 BoardButton 不删，运行时解绑点击并整体隐藏；开关删掉即自动还原旧功能。
    /// </summary>
    void EnsureBoardEntry()
    {
        if (boardEntryButton == null && talentButton != null)
        {
            Transform parent = talentButton.transform.parent;
            if (parent != null)
            {
                // 背包入口 y = -180，看板入口排在其下方 y = -270（与列内 90 间距一致）
                BuildSideBtn(parent, "BoardButton", "看板", -270f, new Color(0.4f, 0.3f, 0.55f, 1f));
                boardEntryButton = parent.Find("BoardButton")?.GetComponent<Button>();
            }
        }
        if (boardEntryButton != null)
        {
            boardEntryButton.onClick.RemoveAllListeners();
            boardEntryButton.gameObject.SetActive(false);
        }
    }

    void FlipPortrait()
    {
        if (portraitImage == null) return;
        _portraitFlipped = !_portraitFlipped;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (_portraitFlipped ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
        portraitImage.GetComponent<PortraitIdleMotion>()?.RefreshBase();
    }

    /// <summary>立绘保持预制体框尺寸，只换 Sprite，禁止 SetNativeSize 撑破布局。</summary>
    public void SetPortrait(Sprite sprite, bool flip = false)
    {
        if (portraitImage == null) return;
        portraitImage.sprite = sprite;
        // 立绘按原比例 fit 入框（preserveAspect），避免更瘦屏/异比例立绘被拉伸变形。
        portraitImage.preserveAspect = true;
        if (sprite != null)
            portraitImage.enabled = true;
        _portraitFlipped = flip;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (flip ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
        EnsureStageShadow();
        if (sprite != null)
            PortraitIdleMotion.EnsureOn(portraitImage.rectTransform, 0.42f);
        else
        {
            var idle = portraitImage.GetComponent<PortraitIdleMotion>();
            if (idle != null) idle.enabled = false;
        }
    }

    void StripLocalChrome()
    {
        DestroyChildNamed(transform, "TopBar");
        DestroyChildNamed(transform, "SharedResourceBar");
        DestroyChildNamed(transform, "BottomNav");
        DestroyChildNamed(transform, "MainBottomNav");
    }

    static void DestroyChildNamed(Transform root, string name)
    {
        var t = root.Find(name);
        if (t != null) Destroy(t.gameObject);
    }

    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 立绘与影子等比对齐兜底（不改预制体文件）：
    /// 1) 预制体保存时把 shadow 的 localScale 存成了 0，影子整个不可见；
    /// 2) 立绘开了 preserveAspect 后实际显示宽随图片比例变化，影子宽度要跟着等比联动。
    /// 影子图是 SPUM 的白椭圆（1024×1024，椭圆约占七成宽），中心贴在人物脚底。
    /// </summary>
    void EnsureStageShadow()
    {
        var stage = transform.Find("Content/Stage");
        var shadowRt = stage != null ? stage.Find("shadow") as RectTransform : null;
        if (shadowRt == null) return;
        if (shadowRt.localScale.sqrMagnitude < 0.0001f)
            shadowRt.localScale = Vector3.one;
        if (portraitImage == null || portraitImage.sprite == null) return;

        var box = portraitImage.rectTransform;
        float boxW = box.rect.width, boxH = box.rect.height;
        if (boxW < 1f || boxH < 1f) return;

        var sp = portraitImage.sprite;
        float ar = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        float boxAr = boxW / boxH;
        float drawW = boxW, drawH = boxH;
        if (ar > boxAr) drawH = drawW / ar;
        else drawW = drawH * ar;

        // 椭圆可见宽 ≈ 节点宽 × 0.7，让影子略宽于人物肩宽
        float shadowW = drawW * 1.3f;
        shadowRt.sizeDelta = new Vector2(shadowW, shadowW);

        // 脚底 y：必须计入 Portrait 的 pivot（预制体里是 (0.5, 0)）与 localScale（0.5），
        // 否则算出来的点不在 Anchor 同一个坐标系里、会掉到画面外导致「阴影没了」。
        //   1) preserveAspect 把图画在 rect 内居中 ⇒ 局部底边 y = rect.yMin + (boxH - drawH) * 0.5
        //   2) 用 Transform 换算到 Stage 局部空间 ⇒ 自动带上 localScale / 翻转 / 旋转
        //   3) 影子与立绘同父但锚点可能不同 ⇒ anchoredPosition = 父空间点 - 影子锚点在父空间的 y
        var parentRt = shadowRt.parent as RectTransform;
        if (parentRt == null) return;
        float localFootY = box.rect.yMin + (boxH - drawH) * 0.5f;
        Vector3 worldFoot = box.TransformPoint(new Vector3(box.rect.center.x, localFootY, 0f));
        Vector2 footInParent = parentRt.InverseTransformPoint(worldFoot);
        float anchorY = parentRt.rect.yMin + shadowRt.anchorMin.y * parentRt.rect.height;
        shadowRt.anchoredPosition = new Vector2(footInParent.x, footInParent.y - anchorY);
    }

    bool _canvasConfigured;
    void ConfigureHostCanvasOnce()
    {        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        TownPageCanvas.Configure(gameObject, 20, stripCanvasWhenNested: true);
        // 页根自身没有布局：CharacterUI.prefab 的 RectTransform 序列化为 anchor(0,0)/(0,0)、sizeDelta(0,0)、localScale(0,0,0)，
        // 过去靠本页自带的 Canvas 走 Apply 时被 EnsureRootStretch 拉满全屏。
        // 现在 TownPageCanvas.Configure 的 nested 分支会把这个 Canvas 销毁掉（改由大厅 Canvas 驱动），
        // 之后就没人铺页根了 ⇒ 整页缩成 0×0、子节点全挤到原点（主人反馈「角色界面整体跑偏」）。
        // 这里自己补一次拉伸：只定页根的边框，不碰任何子节点坐标，删掉本行即可一键回退。
        UICanvasSetup.EnsureRootStretch(transform as RectTransform);
        ApplyBgEnvelopeForThinScreen();
        _canvasConfigured = true;
    }

    RectTransform _bgEnvelopeRt;
    bool _bgEnvelopeBaseCaptured;
    bool _bgEnvelopeOn;
    Vector2 _bgBaseAnchorMin, _bgBaseAnchorMax, _bgBaseOffsetMin, _bgBaseOffsetMax, _bgBasePivot;

    /// <summary>
    /// 角色页底图（bg，800×1421 固定出血图）瘦屏适配（2026-10-17 主人「其他界面高屏也别露底」）：
    /// 高瘦屏（逻辑高>1280）上底图上下各露 ~90px 底，等比 Envelope 放大盖住 —— 比例不变、只裁两侧出血，
    /// 与 GuildHall Background / Tavern 用同一套 ApplyEnvelopeImage 机制；
    /// 标准 9:16 屏一个像素不动（比 GuildHall 的无条件 envelope 更保守），从瘦屏切回标准屏时还原预制体原始锚点。
    /// </summary>
    void ApplyBgEnvelopeForThinScreen()
    {
        if (_bgEnvelopeRt == null)
        {
            var bg = transform.Find("bg");
            _bgEnvelopeRt = bg as RectTransform;
            if (_bgEnvelopeRt == null) return;
        }
        if (!_bgEnvelopeBaseCaptured)
        {
            _bgBaseAnchorMin = _bgEnvelopeRt.anchorMin;
            _bgBaseAnchorMax = _bgEnvelopeRt.anchorMax;
            _bgBaseOffsetMin = _bgEnvelopeRt.offsetMin;
            _bgBaseOffsetMax = _bgEnvelopeRt.offsetMax;
            _bgBasePivot = _bgEnvelopeRt.pivot;
            _bgEnvelopeBaseCaptured = true;
        }
        if (UiLayoutStretch.IsThinnerScreen())
        {
            UiLayoutStretch.ApplyEnvelopeImage(_bgEnvelopeRt);
            _bgEnvelopeOn = true;
        }
        else if (_bgEnvelopeOn)
        {
            // 从瘦屏切回标准屏（编辑器改 Game 分辨率）：还原预制体原始锚点/尺寸，摘掉 Envelope。
            _bgEnvelopeRt.anchorMin = _bgBaseAnchorMin;
            _bgEnvelopeRt.anchorMax = _bgBaseAnchorMax;
            _bgEnvelopeRt.offsetMin = _bgBaseOffsetMin;
            _bgEnvelopeRt.offsetMax = _bgBaseOffsetMax;
            _bgEnvelopeRt.pivot = _bgBasePivot;
            var fitter = _bgEnvelopeRt.GetComponent<AspectRatioFitter>();
            if (fitter != null) Destroy(fitter);
            _bgEnvelopeOn = false;
        }
    }

    static void SetGuildOverlay(bool characterOpen)
    {
        TavernUI.SetGuildHallOverlayMode(characterOpen);
    }
}
