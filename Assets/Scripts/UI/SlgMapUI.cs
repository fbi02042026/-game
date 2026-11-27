using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图主逻辑（2026-11-22 升级：真实行军 + 头像图标 + 缩放拖动）。
/// 全代码构建（不依赖预制体）；图片全部 Resources.Load 路径（铁律 16）。
/// <para>职责边界（单一职责）：本文件只管 地图容器/缩放拖动/点位/行军队伍显示/派遣确认弹窗；
/// 战斗演出在 SlgMapUI.Battle.cs，顶部资源条在 SlgMapUI.ResBar.cs。</para>
/// <para>🔴 玩家城堡位置固定（主人拍板），点位坐标来自 SlgDefs.Points 确定性撒点。</para>
/// </summary>
public static partial class SlgMapUI
{
    const int SortOrder = 12500;   // 高于奖励弹窗、低于剧情 13000

    // 地图内容逻辑尺寸（大地图，比屏幕大 → 可拖动；主人 2026-11-22 拍板再大一倍）
    const float MapW = 1500f * SlgDefs.WorldScale;
    const float MapH = 2300f * SlgDefs.WorldScale;
    /// <summary>默认缩放（主人："默认大概三分之一大小，可放大到二分之一"——两档缩放，比例 1 : 1.5）。</summary>
    const float ZoomDefault = 1f;
    const float ZoomIn = 1.5f;
    const float PointSize = 150f;

    static readonly Color DarkBg = new Color(0.05f, 0.04f, 0.06f, 0.92f);

    // ── 共享状态（partial 三文件共用）──
    static Canvas _canvas;
    static RectTransform _viewRt;        // 可视口（拖动手势落点）
    static RectTransform _mapContent;    // 地图内容（缩放 + 平移的对象）
    static RectTransform _pointLayer;
    static RectTransform _marchLayer;    // 行军队伍头像层
    static RectTransform _battleLayer;   // 战斗头像对冲层（挂 MapContent 下，随缩放平移）
    static SlgMapUIHost _host;           // 协程 + 拖拽宿主（MonoBehaviour）
    static float _zoom = ZoomDefault;

    static GameObject _modal;
    static Text _modalTitle, _modalBody;
    static Button _btnA, _btnB, _btnClose;
    static System.Action _onA, _onB;

    static readonly List<GameObject> _pointGos = new List<GameObject>();
    static readonly List<GameObject> _marchGos = new List<GameObject>();

    /// <summary>打开大地图（未解锁给提示）。</summary>
    public static void Show()
    {
        if (!SlgSystem.IsUnlocked())
        {
            GlobalToastUI.ShowQueued("通关第 10 关后开启城镇入口");
            return;
        }
        EnsureCanvas();
        if (_canvas == null) return;
        SlgSystem.EnsureInitialFood();   // 首次进入补初始粮草（幂等）；否则粮草 0 无法出征
        _canvas.gameObject.SetActive(true);
        UICanvasSetup.RefreshPopup(_canvas, SortOrder);
        Refresh();
        BattleOnShow();   // 到达未结算的行军单 → 播战斗演出
    }

    public static void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    /// <summary>清档复位。</summary>
    public static void ResetForNewGame()
    {
        BattleReset();
        if (_canvas != null)
        {
            Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
        _viewRt = null; _mapContent = null; _pointLayer = null; _marchLayer = null; _battleLayer = null; _host = null;
        _pointGos.Clear(); _marchGos.Clear();
        _modal = null;
        ResetSelect(); ResetHud();
    }

    // ───────────────────────────────── build ─────────────────────────────────

    static void EnsureCanvas()
    {
        if (_canvas != null) return;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        var root = new GameObject("SlgMapCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(root);
        _canvas = root.GetComponent<Canvas>();
        UICanvasSetup.ApplyPopup(_canvas, SortOrder);

        // 拖拽宿主：吞掉空白点击 + 承载协程 + 平移地图
        var hostGo = new GameObject("Host", typeof(RectTransform), typeof(Image), typeof(SlgMapUIHost));
        hostGo.transform.SetParent(_canvas.transform, false);
        _host = hostGo.GetComponent<SlgMapUIHost>();
        var hostRt = (RectTransform)hostGo.transform;
        Stretch(hostRt);
        hostGo.GetComponent<Image>().color = DarkBg;
        hostGo.GetComponent<Image>().raycastTarget = true;

        // 可视口（坐标系参照，不裁剪；拖动钳位按它算）
        _viewRt = NewRect("View", _canvas.transform);
        Stretch(_viewRt);

        // 地图内容（缩放 + 平移）
        var content = NewRect("MapContent", _viewRt);
        _mapContent = content;
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(MapW, MapH);
        content.anchoredPosition = Vector2.zero;

        // 底图
        var bg = NewImage("MapBg", content, Color.white);
        bg.sprite = LoadSprite(SlgDefs.MapBgPath);
        bg.preserveAspect = true;
        bg.raycastTarget = false;
        var bgRt = bg.rectTransform;
        Stretch(bgRt);

        // 玩家城堡（固定位置，建筑图；不随点位重排）
        var castle = NewImage("Castle", content, Color.white);
        castle.sprite = LoadSprite(SlgDefs.CastlePath);
        castle.preserveAspect = true;
        castle.raycastTarget = false;
        PlaceCentered(castle.rectTransform, SlgDefs.CastlePos, 230f);

        // 点位层 / 行军层
        var pt = NewRect("Points", content); Stretch(pt); _pointLayer = pt;
        var mg = NewRect("Marches", content); Stretch(mg); _marchLayer = mg;
        var bt = NewRect("Battle", content); Stretch(bt); _battleLayer = bt;

        _host.content = content;
        _host.view = _viewRt;
        ApplyZoom(ZoomDefault);
        _host.StartCoroutine(ArrivalPoller());

        BuildResBar(_canvas.transform);
        BuildZoomButton();
        BuildMarchPanel(_canvas.transform);      // 右侧固定倒计时栏
        BuildHomeButton(_canvas.transform);      // 左下角主城图 = 返回游戏主界面
        BuildSelectPanel(_canvas.transform);     // 点地点后的选兵界面
        BuildBattleOverlay();
        BuildModal();
    }

    static void BuildZoomButton()
    {
        var btn = NewButton("ZoomBtn", _canvas.transform, "放大", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(130f, 64f), new Vector2(-20f, 24f));
        btn.GetComponent<Image>().color = new Color(0.18f, 0.32f, 0.46f, 1f);
        btn.onClick.AddListener(ToggleZoom);
    }

    static void ToggleZoom()
    {
        ApplyZoom(_zoom <= ZoomDefault + 0.01f ? ZoomIn : ZoomDefault);
    }

    static void ApplyZoom(float z)
    {
        _zoom = z;
        if (_mapContent != null) _mapContent.localScale = Vector3.one * z;
        if (_host != null) _host.ClampNow();
        // 按钮文案
        var btn = _canvas != null ? _canvas.transform.Find("ZoomBtn") : null;
        if (btn != null)
        {
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = z <= ZoomDefault + 0.01f ? "放大" : "缩小";
        }
    }

    // ───────────────────────────────── refresh ─────────────────────────────────

    static void Refresh()
    {
        ClearGos(_pointGos);
        ClearGos(_marchGos);

        RefreshResBar();

        // 点位（场景图标：营地/大矿/小矿/野怪/城市/城寨——主人 2026-11-22 拍板，不用佣兵头像）
        var points = SlgSystem.ActivePoints();
        for (int i = 0; i < points.Count; i++) BuildPoint(points[i]);

        // 行军队伍（城堡 → 目标的头像小队）
        var marches = SlgSystem.Marches();
        for (int i = 0; i < marches.Count; i++) BuildMarch(marches[i]);
        RefreshMarchPanel();
    }

    static void ClearGos(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) Object.Destroy(list[i]);
        list.Clear();
    }

    static void BuildPoint(SlgDefs.PointDef def)
    {
        var go = new GameObject("Point_" + def.Id, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_pointLayer, false);
        _pointGos.Add(go);
        var rt = (RectTransform)go.transform;
        PlaceCentered(rt, def.Pos, PointSize);

        bool cooling = SlgSystem.IsPointOnCooldown(def.Id);
        bool inMarch = SlgSystem.MarchForPoint(def.Id) != null;
        var icon = go.GetComponent<Image>();
        icon.sprite = LoadSprite(def.IconPath);          // 场景图标（大矿/小矿/野怪/城市/城寨）
        icon.preserveAspect = true;
        icon.color = cooling ? new Color(0.45f, 0.45f, 0.45f, 0.75f) : Color.white;

        var namePlate = NewImage("NamePlate", rt, Color.white);
        namePlate.sprite = LoadSprite(SlgDefs.NamePlatePath);
        namePlate.preserveAspect = true;
        var nprt = namePlate.rectTransform;
        nprt.anchorMin = nprt.anchorMax = new Vector2(0.5f, 0f);
        nprt.pivot = new Vector2(0.5f, 0f);
        nprt.sizeDelta = new Vector2(169f, 42f);
        nprt.anchoredPosition = new Vector2(0f, -14f);
        namePlate.raycastTarget = false;

        string label = cooling ? def.Name + "(" + SlgSystem.PointCooldownLeft(def.Id) / 60 + "分)"
            : inMarch ? def.Name + "(前往中)" : def.Name;
        var nameText = NewText("Name", nprt, label, 22, TextAnchor.MiddleCenter);
        Stretch(nameText.rectTransform);

        var btn = go.GetComponent<Button>();
        btn.interactable = !cooling && !inMarch;
        string id = def.Id;
        btn.onClick.AddListener(() => OnPointClicked(id));
    }

    /// <summary>行军队伍头像：从城堡出发，飞向目标；到达后停在目标旁显示倒计时。</summary>
    static void BuildMarch(SlgMarchEntry m)
    {
        var def = SlgSystem.FindPoint(m.pointId);
        bool arrived = SlgSystem.IsArrived(m);
        Vector2 from = SlgDefs.CastlePos;
        Vector2 to = arrived ? def.Pos : Vector2.Lerp(from, def.Pos, FlightProgress(m));

        var heads = new List<RectTransform>();
        for (int i = 0; i < m.squad.Count && i < SlgDefs.SquadSize; i++)
        {
            var go = new GameObject("March_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_marchLayer, false);
            _marchGos.Add(go);
            var rt = (RectTransform)go.transform;
            var off = new Vector2((i - 1) * 58f, arrived ? -70f : 0f);
            PlaceCentered(rt, to + off, 92f);
            var img = go.GetComponent<Image>();
            img.sprite = MercPortraitSprites.GetHead(m.squad[i]);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            heads.Add(rt);
        }
        // 逐帧飞行：仅在视觉飞行期内（到达时间远未到）由协程插值，避免只在 Refresh 时跳变
        if (!arrived && FlightProgress(m) < 1f && _host != null)
            _host.StartCoroutine(FlyHeads(heads, from, def.Pos));

        // 倒计时 / 到达标记 + 加速按钮（挂在队伍上方）
        string txt = arrived ? "已抵达 · 战斗中" : "行军中 " + FormatSec(SlgSystem.MarchLeftSec(m));
        var label = NewText("MarchLabel", _marchLayer, txt, 22, TextAnchor.MiddleCenter);
        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(260f, 46f);
        lrt.anchoredPosition = to + new Vector2(0f, 78f);
        _marchGos.Add(label.gameObject);

        if (!arrived)
        {
            var spd = NewButton("SpeedUp", _marchLayer, "加速", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(110f, 52f), to + new Vector2(0f, 128f));
            _marchGos.Add(spd.gameObject);
            string id = m.pointId;
            spd.onClick.AddListener(() => OnSpeedUp(id));
        }
    }

    /// <summary>行军飞行进度 0~1（视觉动画时长固定，不代表真实行军耗时）。</summary>
    static float FlightProgress(SlgMarchEntry m)
    {
        float elapsed = NowUnix() - m.departUnix;
        return Mathf.Clamp01(elapsed / SlgDefs.MarchFlyAnimSec);
    }

    /// <summary>逐帧把行军头像从城堡插值飞到目标（仅视觉；与 FlightProgress 同一时刻基准）。</summary>
    static IEnumerator FlyHeads(List<RectTransform> heads, Vector2 from, Vector2 to)
    {
        float t = 0f;
        while (t < SlgDefs.MarchFlyAnimSec)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SlgDefs.MarchFlyAnimSec);
            Vector2 pos = Vector2.Lerp(from, to, k);
            for (int i = 0; i < heads.Count; i++)
            {
                if (heads[i] == null) yield break;   // Refresh 重建了节点 → 旧协程自停
                var rt = heads[i];
                rt.anchoredPosition = pos + new Vector2((i - 1) * 58f, 0f);
            }
            yield return null;
        }
    }

    static string FormatSec(int sec)
    {
        if (sec >= 3600) return (sec / 3600) + "时" + (sec % 3600 / 60) + "分";
        if (sec >= 60) return (sec / 60) + "分" + (sec % 60) + "秒";
        return sec + "秒";
    }

    /// <summary>每 2 秒检查到达单：开着界面也会自动播战斗演出（不必关了重开）。</summary>
    static IEnumerator ArrivalPoller()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(2f);
            if (_canvas == null || !_canvas.gameObject.activeSelf) continue;
            RefreshMarchPanel();   // 右侧固定栏倒计时实时刷新
            BattleOnShow();
        }
    }

    // ───────────────────────────────── 派遣 / 加速 ─────────────────────────────────

    /// <summary>点地点 → 选兵界面（主人 2026-11-22 拍板：玩家自选 3 名闲置佣兵再出发）。</summary>
    static void OnPointClicked(string pointId)
    {
        OpenSelect(pointId);
    }

    /// <summary>广告加速：走 RewardedAdBridge 单一出口，核验成功才改到达时间。</summary>
    static void OnSpeedUp(string pointId)
    {
        if (RewardedAdBridge.PlatformClosed)
        {
            GlobalToastUI.ShowQueued("暂未开放");
            return;
        }
        if (AdKillSwitch.AdsOff)
        {
            GlobalToastUI.ShowQueued("广告维护中，请稍后再试");
            return;
        }
        GlobalToastUI.ShowQueued("广告加载中…");
        RewardedAdBridge.ShowRewarded(SlgDefs.AdPlacementMarchSpeed, ok =>
        {
            if (!ok)
            {
                GlobalToastUI.ShowQueued("激励未完成，队伍继续行军");
                return;
            }
            if (SlgSystem.SpeedUpMarch(pointId))
            {
                Refresh();
                BattleOnShow();
                GlobalToastUI.ShowQueued("队伍已加速抵达～");
            }
        });
    }

    // ───────────────────────────────── modal helpers ─────────────────────────────────

    static void BuildModal()
    {
        _modal = new GameObject("Modal", typeof(RectTransform));
        _modal.transform.SetParent(_canvas.transform, false);
        Stretch((RectTransform)_modal.transform);
        var veil = NewImage("Veil", _modal.transform, new Color(0f, 0f, 0f, 0.66f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = true;

        var panel = NewImage("Panel", _modal.transform, new Color(0.12f, 0.11f, 0.14f, 1f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(600f, 470f);
        prt.anchoredPosition = Vector2.zero;

        _modalTitle = NewText("Title", prt, "", 36, TextAnchor.MiddleCenter);
        var tRt = _modalTitle.rectTransform;
        Stretch(tRt); tRt.sizeDelta = new Vector2(-40f, -390f); tRt.anchoredPosition = new Vector2(0f, 190f);

        _modalBody = NewText("Body", prt, "", 27, TextAnchor.UpperLeft);
        var bRt = _modalBody.rectTransform;
        bRt.anchorMin = new Vector2(0f, 1f); bRt.anchorMax = new Vector2(1f, 1f);
        bRt.pivot = new Vector2(0.5f, 1f);
        bRt.sizeDelta = new Vector2(-70f, 240f);
        bRt.anchoredPosition = new Vector2(0f, -96f);
        _modalBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        _modalBody.verticalOverflow = VerticalWrapMode.Overflow;

        _btnA = NewButton("BtnA", prt, "派遣", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(220f, 74f), new Vector2(-150f, -170f));
        _btnA.GetComponent<Image>().color = new Color(0.20f, 0.48f, 0.26f, 1f);
        _btnA.onClick.AddListener(() => _onA?.Invoke());

        _btnB = NewButton("BtnB", prt, "通商", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(220f, 74f), new Vector2(150f, -170f));
        _btnB.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.58f, 1f);
        _btnB.onClick.AddListener(() => _onB?.Invoke());

        _btnClose = NewButton("BtnClose", prt, "关闭", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(110f, 50f), new Vector2(-10f, -10f));
        _btnClose.onClick.AddListener(CloseModal);

        _modal.SetActive(false);
    }

    static void OpenModal(string title, string body, string aText, System.Action onA,
        string bText, System.Action onB, System.Action onClose)
    {
        _modalTitle.text = title;
        _modalBody.text = body;
        _onA = onA; _onB = onB;
        _btnA.gameObject.SetActive(onA != null || !string.IsNullOrEmpty(aText));
        _btnA.GetComponentInChildren<Text>().text = aText ?? "确定";
        bool hasB = !string.IsNullOrEmpty(bText) && onB != null;
        _btnB.gameObject.SetActive(hasB);
        if (hasB) _btnB.GetComponentInChildren<Text>().text = bText;
        _btnClose.GetComponentInChildren<Text>().text = onClose == null ? "关闭" : "取消";
        _modal.SetActive(true);
        _modal.transform.SetAsLastSibling();
    }

    static void CloseModal()
    {
        if (_modal != null) _modal.SetActive(false);
        _onA = null; _onB = null;
    }

    // ───────────────────────────────── tiny helpers ─────────────────────────────────

    static int NowUnix() => (int)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    static Sprite LoadSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError("[SlgMap] 缺少美术资源: " + path);
        return sp;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text NewText(string name, Transform parent, string value, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = value;
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = align;
        t.color = new Color(0.99f, 0.98f, 0.88f, 1f);
        t.raycastTarget = false;
        return t;
    }

    static Button NewButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var t = NewText("Label", rt, label, 28, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        return go.GetComponent<Button>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void PlaceCentered(RectTransform rt, Vector2 pos, float size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
    }

    /// <summary>拖拽 / 协程宿主（单一职责：只做地图平移 + 承载战斗演出协程）。</summary>
    sealed class SlgMapUIHost : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public RectTransform content;
        public RectTransform view;
        Vector2 _last;

        public void ClampNow()
        {
            if (content == null || view == null) return;
            var p = content.anchoredPosition;
            content.anchoredPosition = ClampPos(p);
        }

        public void OnBeginDrag(PointerEventData e) { _last = e.position; }

        public void OnDrag(PointerEventData e)
        {
            if (content == null || view == null) return;
            var canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            Vector2 delta = (e.position - _last) / factor;
            _last = e.position;
            content.anchoredPosition = ClampPos(content.anchoredPosition + delta);
        }

        Vector2 ClampPos(Vector2 p)
        {
            float cw = content.rect.width * content.localScale.x;
            float ch = content.rect.height * content.localScale.y;
            float vw = view.rect.width, vh = view.rect.height;
            float maxX = Mathf.Max(0f, (cw - vw) * 0.5f);
            float maxY = Mathf.Max(0f, (ch - vh) * 0.5f);
            p.x = Mathf.Clamp(p.x, -maxX, maxX);
            p.y = Mathf.Clamp(p.y, -maxY, maxY);
            return p;
        }
    }
}
