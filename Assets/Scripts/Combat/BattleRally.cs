using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// 【2026-11-09 主人拍板】战斗中「点击地点 → 我方全体集结」。
/// 口径：战场 = 顶栏底边 ~ 底部面板顶边之间（UI 实时矩形）；落点 X 钳到当前相机视野、
///       Y 钳到 walk 可行走带；点击不推动相机 / 推图；引导期间禁点；
///       首人到达后 2 秒才恢复自动索敌；落点有敌人立即恢复。
/// 【2026-11-11 第五版】判「点在哪」用屏幕 UI 区域，判「走到哪」用世界真源，两层分开。
///       旧版把可行走带（walk 投影只有约 1.65 世界单位）当可点区，整屏只剩中间一条窄带能点，
///       主人报「有的地方有用有的没用」；再上一版拿 map UI 矩形（140×140）判更是全屏误杀。
/// 纯进程级状态，复位统一走 NewGameReset.ApplyAll()。
/// </summary>
public static class BattleRally
{
    /// <summary>首人到达后驻留时长（秒）。</summary>
    public const float HOLD_SEC = 2f;
    /// <summary>到达判定阈值（世界单位）。</summary>
    public const float ARRIVE_EPS = 0.35f;
    /// <summary>落点附近有敌人的判定半径（世界单位）。</summary>
    public const float ENEMY_NEAR_RADIUS = 1.2f;
    /// <summary>点击落点向屏内收的边距（与 GetBattleVisibleX 同口径）：X 越界时钳到这里，不推镜头。</summary>
    const float TAP_PAD = 0.35f;

    // 【2026-11-09 主人摆的「集结」节点动效】旗子从上落下 + 圈由大缩小到当前大小，
    // 两者同一进度同时完成；整组出现 2 秒后隐藏。
    const float MARKER_SHOW_SEC = 2f;
    const float MARKER_ANIM_SEC = 0.18f;      // 出来快：0.18 秒内播完
    const float MARKER_FLAG_DROP = 120f;      // 旗子起始高出落点多少（UI 单位）
    const float MARKER_RING_FROM_MUL = 2.4f;  // 圈起始放大倍数，缩到预制体里现在的大小
    /// <summary>集结动效排序层：高于技能栏 955 / 招募横幅 995，保证永远压在战斗 UI 之上。</summary>
    const int MARKER_SORTING_ORDER = 998;

    static Vector3 _point;
    static bool _active;
    static float _firstArriveAt = -1f;
    static float _resumeAt;

    static GameObject _markerRoot;
    static RectTransform _markerRootRt;
    static RectTransform _markerFlagRt;
    static RectTransform _markerRingRt;
    // 【2026-11-11 第五版 · 点击范围真源】战场 = 顶栏底边 ~ 底部面板顶边之间的整块屏幕区域，
    // 边界从 TopBar / BackpackPanel 的实时 UI 矩形投影读（不写死像素、不按节点名猜图）；
    // 面板收起 / 展开、竖屏适配、map 动画全部自动跟随。
    static RectTransform _topBarRt;
    static RectTransform _bottomPanelRt;
    static readonly Vector3[] _panelCorners = new Vector3[4];
    static Vector2 _flagIdlePos;
    static Vector3 _ringIdleScale;
    static bool _markerSearched;
    static bool _markerVisible;
    static float _markerAge;

    public static bool Active => _active;
    public static Vector3 Point => _point;

    /// <summary>清档 / 打断：清状态并让英雄立刻回到自动索敌。</summary>
    public static void Reset()
    {
        bool wasActive = _active;
        if (_active)
            Hero.Instance?.BeginAutoAcquireOnRelease();
        _active = false;
        _firstArriveAt = -1f;
        _resumeAt = 0f;
        _point = Vector3.zero;
        // 【2026-11-11 第五版 · 旗子不出现的根因之一】旧版在这里 HideMarker()，与 Tick 里
        // 「落点动效独立推进：集结提前结束也要能按时消失」自相矛盾。战斗中 AnyAllyArrived
        // （只比 X）+ EnemyNearPoint 会在点击同帧触发 Reset，旗子刚 SetActive(true) 就被关掉，
        // 肉眼完全看不到。动效生命周期只由 TickMarker 的 2 秒独立管理，Reset 一律不碰。
        Debug.Log($"[BattleRally] Reset wasActive={wasActive} markerAge={_markerAge:F2} markerVisible={_markerVisible}");
    }

    /// <summary>是否可以接受点击（引导 / 弹窗 / 冻场 / 结算一律挡）。</summary>
    static bool CanTap(BattleManager bm)
    {
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return false;
        if (bm._runClosed || bm.PortalWalkMode) return false;
        if (BattleLootMode.Active) return false;
        if (bm.IsTutorialRun) return false;  // 引导局全程禁点（与 IsTutorialBattle 同口径，无 FindObjectOfType 开销）
        return true;
    }

    /// <summary>每帧入口：先吃点击，再推进集结状态。由 BattleManager.Update 调用。</summary>
    public static void Tick()
    {
        var bm = BattleManager.InstanceQuiet;
        if (bm == null) return;
        EnsureMarkerNodes();   // 进战斗首帧就绑定并把「集结」组藏起来（内部只做一次）
        TickMarker();   // 落点动效独立推进：集结提前结束也要能按时消失

        if (CanTap(bm) && ReadBattlefieldTap(out Vector2 screen))
            TrySetPoint(screen, bm);

        if (!_active) return;
        if (!CanTap(bm)) { Reset(); return; }

        // 【2026-11-09 主人拍板「按首人到达」】第一个我方单位到点就开始 2 秒驻留
        if (_firstArriveAt < 0f && AnyAllyArrived(bm))
        {
            _firstArriveAt = Time.time;
            _resumeAt = _firstArriveAt + HOLD_SEC;
        }

        // 【2026-11-09 主人拍板】只有「到达该地」之后才判落点有没有敌人：
        // 点击当下就判会让点怪堆直接 Reset，全队根本不出发。
        if (_firstArriveAt >= 0f && EnemyNearPoint(bm)) { Reset(); return; }

        if (_firstArriveAt >= 0f && Time.time >= _resumeAt)
            Reset();
    }

    /// <summary>只认战场空白区点击：纯背景 / 装饰图不算 UI，真正会响应点击的控件才算。</summary>
    static bool ReadBattlefieldTap(out Vector2 screen)
    {
        var es = EventSystem.current;
        if (Input.GetMouseButtonDown(0))
        {
            screen = (Vector2)Input.mousePosition;
            return es == null || !IsOverInteractiveUI(es, screen);
        }
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                screen = t.position;
                return es == null || !IsOverInteractiveUI(es, screen);
            }
        }
        screen = Vector2.zero;
        return false;
    }

    /// <summary>
    /// 【2026-11-11 第四版 · 真凶在这里】旧口径 <c>IsPointerOverGameObject()</c> 在本工程恒为 true：
    /// BattleUI.prefab 里 Background(720×1280)、icon(1171×1345)、1/2/3(2048×768)、walk、TopBar
    /// 等美术层共 209 张 Image 全是 raycastTarget=1，不带任何交互却把整屏点击吃光，
    /// 集结一次都进不去（Editor.log 实测几十次点击全是「点击被 UI 吞掉」，零条「集结触发」）。
    /// 改成只判「真正会响应点击的控件」：命中链上有 Selectable / IPointerClickHandler /
    /// IPointerDownHandler 才算 UI 点击；纯背景装饰图一律放行。
    /// 底部面板 / 顶栏由 IsInsideBattleArea 的屏幕区域挡住，不按节点名猜图、不改预制体。
    /// </summary>
    static bool IsOverInteractiveUI(EventSystem es, Vector2 screen)
    {
        var ped = new PointerEventData(es) { position = screen };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);

        bool blocked = false;
        string blocker = "";
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            if (go == null) continue;
            if (i < 8)
            {
                if (names.Length > 0) names.Append(" | ");
                names.Append(go.name);
            }
            if (!blocked && (go.GetComponentInParent<Selectable>() != null
                    || ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null
                    || ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null))
            {
                blocked = true;
                blocker = go.name;
            }
        }
        // 判别性探针：一次点击必有一条，命中名单能直接看出是谁在吃点击
        Debug.Log($"[BattleRally] UI命中 {hits.Count} 个 前8=[{names}] → "
                  + (blocked ? "挡住:" + blocker : "放行（全是纯装饰层）"));
        return blocked;
    }

    /// <summary>
    /// 点击是否落在战场：顶栏底边 ~ 底部面板顶边之间。两块挡板的边界从 UI 实时矩形投影读，
    /// 不写死像素、不按节点名猜图；面板收起 / 展开、竖屏适配自动跟随。
    /// </summary>
    static bool IsInsideBattleArea(Vector2 screen)
    {
        if (_topBarRt == null || _bottomPanelRt == null) return true; // 绑定处已 LogError，不静默把所有点击判死
        var rootCanvas = _bottomPanelRt.GetComponentInParent<Canvas>()?.rootCanvas;
        var cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        float topBarBottom = float.MaxValue;
        _topBarRt.GetWorldCorners(_panelCorners);
        for (int i = 0; i < 4; i++)
        {
            float y = cam != null ? cam.WorldToScreenPoint(_panelCorners[i]).y : _panelCorners[i].y;
            topBarBottom = Mathf.Min(topBarBottom, y);
        }

        float panelTop = float.MinValue;
        _bottomPanelRt.GetWorldCorners(_panelCorners);
        for (int i = 0; i < 4; i++)
        {
            float y = cam != null ? cam.WorldToScreenPoint(_panelCorners[i]).y : _panelCorners[i].y;
            panelTop = Mathf.Max(panelTop, y);
        }
        return screen.y >= panelTop && screen.y <= topBarBottom;
    }

    /// <summary>屏幕点 → 世界落点：落在战场区内就集结，X/Y 钳到合法范围，越界不推镜头、不丢弃点击。</summary>
    static void TrySetPoint(Vector2 screen, BattleManager bm)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        if (!IsInsideBattleArea(screen))
        {
            Debug.Log($"[BattleRally] 点击忽略：屏幕Y={screen.y:F0} 落在顶栏 / 底部面板");
            return;
        }

        float planeZ = bm.unitRoot != null ? bm.unitRoot.position.z : 0f;
        Vector3 s = new Vector3(screen.x, screen.y, planeZ - cam.transform.position.z);
        Vector3 w = cam.ScreenToWorldPoint(s);

        // X：钳到当前相机视野内（相机不动 → 点击不会推动镜头 / 推图）。
        // 旧版「越界直接忽略」让屏幕左右边缘整块点不到（Editor.log 实锤 屏幕X=39 被丢弃）。
        BattleManager.GetBattleVisibleX(out float minX, out float maxX, TAP_PAD);
        float pointX = Mathf.Clamp(w.x, minX, maxX);

        // Y：钳到可行走带（唯一真源 BattleLaneBounds），带外不再丢弃点击。
        // 旧版拿这条带当「可点区」，walk 投影只有约 1.65 世界单位 → 屏幕中间一条窄带才有反应。
        float rawLane = w.y - UnitBase.GROUND_Y;
        BattleLaneBounds.GetLaneOffsetRange(out float minLane, out float maxLane);
        float lane = BattleLaneBounds.ClampLaneOffset(rawLane);

        _point = new Vector3(pointX, UnitBase.GROUND_Y + lane, planeZ);
        _active = true;
        _firstArriveAt = -1f;
        _resumeAt = 0f;
        // 切进手动态：不调这个，Hero.AIUpdate 的 _manualMove 分支进不去，集结永远不动
        Hero.Instance?.SetManualMove(Vector2.zero);
        Debug.Log($"[BattleRally] 集结触发 屏幕=({screen.x:F0},{screen.y:F0}) "
                  + $"x={pointX:F2}(原{w.x:F2} 视野[{minX:F2},{maxX:F2}]) "
                  + $"lane={lane:F2}(原{rawLane:F2} 带[{minLane:F2},{maxLane:F2}])");
        Vector3 markerScreen = cam.WorldToScreenPoint(_point);
        PlayMarker(new Vector2(markerScreen.x, markerScreen.y));
    }

    static bool AnyAllyArrived(BattleManager bm)
    {
        var hero = bm.hero;
        if (hero != null && !hero.isDead
            && Mathf.Abs(hero.transform.position.x - _point.x) <= ARRIVE_EPS)
            return true;
        var allies = bm.allyUnits;
        if (allies == null) return false;
        for (int i = 0; i < allies.Count; i++)
        {
            var u = allies[i];
            if (u == null || u.isDead) continue;
            if (Mathf.Abs(u.transform.position.x - _point.x) <= ARRIVE_EPS)
                return true;
        }
        return false;
    }

    static bool EnemyNearPoint(BattleManager bm)
    {
        var monsters = bm.monsters;
        if (monsters == null) return false;
        float r2 = ENEMY_NEAR_RADIUS * ENEMY_NEAR_RADIUS;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || m.isDead) continue;
            float dx = m.transform.position.x - _point.x;
            float dy = m.transform.position.y - _point.y;
            if (dx * dx + dy * dy <= r2) return true;
        }
        return false;
    }

    // ============================================================
    // 落点动效：主人 2026-11-09 在 BattleUI 下摆的「集结」节点（旗子 + 圈）。
    // 只按名字绑定并驱动，不动预制体排版；找不到节点直接 LogError，不兜底。
    // 口径（主人 11-09 更正）：圈由大变小、缩到预制体里现在的大小；旗子从上落到地点，
    // 两者用同一个进度 k 同时完成（0.18s），整组 2 秒后隐藏。
    // ============================================================
    static Transform FindDeep(Transform parent, string want)
    {
        if (parent == null) return null;
        if (NameEquals(parent.name, want)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), want);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>prefab 中文节点名是 \uXXXX 大写转义存的，个别引擎版本不自动解码 → 解码后再比一次。</summary>
    static bool NameEquals(string nodeName, string want)
    {
        if (string.Equals(nodeName, want, StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(DecodeUnicodeEscapes(nodeName), want, StringComparison.OrdinalIgnoreCase);
    }

    static string DecodeUnicodeEscapes(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length;)
        {
            if (i + 5 < s.Length && s[i] == '\\' && s[i + 1] == 'u')
            {
                string hex = s.Substring(i + 2, 4);
                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out int code))
                {
                    sb.Append((char)code);
                    i += 6;
                    continue;
                }
            }
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    static bool EnsureMarkerNodes()
    {
        // 【2026-11-11 第五版】旧版只看 _markerSearched：跨局后静态字段不清、旧节点已销毁，
        // EnsureMarkerNodes 静默返回 false → PlayMarker 直接 return，旗子再也不出现且无任何日志。
        // 现在以「引用还活着」为准（Unity 对已销毁对象判假 null），跨局自动重绑。
        if (_markerSearched && _markerRoot != null) return true;
        var ui = BattleUI.Instance;
        if (ui == null) return false;   // 系统未装配：下一帧再试，不刷 Error
        _markerSearched = true;

        // 点击范围的两块挡板与动效节点一起绑定一次，跨局自动重绑
        _topBarRt = FindDeep(ui.transform, "TopBar") as RectTransform;
        _bottomPanelRt = FindDeep(ui.transform, "BackpackPanel") as RectTransform;
        if (_topBarRt == null || _bottomPanelRt == null)
            Debug.LogError("[BattleRally] 点击范围：TopBar / BackpackPanel 没找到 → 点面板也会触发集结");

        Transform root = FindDeep(ui.transform, "集结");
        if (root == null)
        {
            Debug.LogError("[BattleRally] 集结动效：BattleUI 下没找到「集结」节点 → 落点动效不播");
            return false;
        }
        _markerRoot = root.gameObject;
        _markerRootRt = root as RectTransform;
        if (_markerRootRt == null)
            Debug.LogError("[BattleRally] 集结动效：「集结」不是 RectTransform → 无法定位到落点");

        Transform flag = FindDeep(root, "旗子");
        Transform ring = FindDeep(root, "圈");
        _markerFlagRt = flag as RectTransform;
        _markerRingRt = ring as RectTransform;
        if (_markerFlagRt == null)
            Debug.LogError("[BattleRally] 集结动效：「集结」下没找到「旗子」→ 落下动效不播");
        if (_markerRingRt == null)
            Debug.LogError("[BattleRally] 集结动效：「集结」下没找到「圈」→ 缩放动效不播");

        // 记下主人摆好的初始帧，动效只在这上面叠加，结束还原
        _flagIdlePos = _markerFlagRt != null ? _markerFlagRt.anchoredPosition : Vector2.zero;
        _ringIdleScale = _markerRingRt != null ? _markerRingRt.localScale : Vector3.one;

        // 预制体里两个 Image 默认 raycastTarget=1，显示时会吃掉战场点击 → 运行时关掉（不改预制体）
        var imgs = _markerRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
            imgs[i].raycastTarget = false;

        // 【2026-11-11 第六版 · 旗子看不见的根因】运行日志实测：位置对（local/pos 与屏幕点吻合）、
        // 图有效（sprite/白色/alpha=1/enabled=1）、节点 active=true，但 sibling=13/18 ——
        // BattleUI 根下运行时还会插 4 个兄弟节点排在集结之后，且 BackpackPanel(20) / BossBar(950) /
        // SkillUI(955) / MercJoinBanner(995) 都是 overrideSorting 的子 Canvas，整层压在根 Canvas
        // 的普通内容之上。旗子被盖住，所以「位置对但看不见」。给动效单独一个高 sortingOrder 的
        // Canvas（只作用于集结这 2 张图，不改预制体、不影响别的 UI），保证它永远在最上层。
        var markerCanvas = _markerRoot.GetComponent<Canvas>();
        if (markerCanvas == null) markerCanvas = _markerRoot.AddComponent<Canvas>();
        markerCanvas.overrideSorting = true;
        markerCanvas.sortingOrder = MARKER_SORTING_ORDER;

        _markerRoot.SetActive(false);   // 平时隐藏，点击才亮
        return true;
    }

    /// <summary>点击落点：整组挪到落点，旗子从上方落下、圈由大缩小，同一进度同时到位。</summary>
    static void PlayMarker(Vector2 screenPos)
    {
        if (!EnsureMarkerNodes())
        {
            Debug.LogError("[BattleRally] 集结动效：EnsureMarkerNodes 未就绪（BattleUI.Instance / 「集结」节点缺失）→ 旗子不播");
            return;
        }
        if (_markerRootRt == null) return;
        var parent = _markerRootRt.parent as RectTransform;
        // 与 BattleViewportFit 同源：战斗 UI 由根 Canvas.worldCamera 渲染（= Camera.main），Overlay 时传 null
        var rootCanvas = parent != null ? parent.GetComponentInParent<Canvas>()?.rootCanvas : null;
        var cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
        if (parent == null || (cam == null && rootCanvas != null
                && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay))
        {
            Debug.LogError("[BattleRally] 集结动效：父容器 / 根 Canvas 相机取不到，无法把「集结」摆到落点");
            return;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out Vector2 local))
        {
            Debug.LogError("[BattleRally] 集结动效：屏幕点转 UI 本地坐标失败");
            return;
        }
        // 【2026-11-11 修复没动画】集结锚点=(0.5,0.5)、父(BattleUI 根) pivot=(0,0)：
        // ScreenPointToLocalPointInRectangle 返回的是相对父 pivot（左下角）的坐标，
        // 直接赋 anchoredPosition 会再叠加半屏偏移（+360,+640），动效整组被摆出屏幕右上角外。
        // 这里按点锚换算：anchoredPosition = local - 锚点参考点在父本地坐标中的位置。
        Vector2 a = _markerRootRt.anchorMin;
        Vector2 anchorRef = new Vector2(
            parent.rect.width * (a.x - parent.pivot.x),
            parent.rect.height * (a.y - parent.pivot.y));
        _markerRootRt.anchoredPosition = local - anchorRef;
        _markerRootRt.SetAsLastSibling();   // 兄弟顺序也提到最后：后插入的运行时节点不会再压住它
        _markerRoot.SetActive(true);
        _markerVisible = true;
        _markerAge = 0f;
        ApplyMarkerFrame(0f);

        // 判别性探针（验收通过后删）：一次点击就能分清「绑定 / 坐标 / 遮挡 / 立刻被 Reset」四层
        var mcv = _markerRoot.GetComponent<Canvas>();
        var groups = _markerRoot.GetComponentsInParent<CanvasGroup>(true);
        float alphaChain = 1f;
        for (int i = 0; i < groups.Length; i++) alphaChain *= groups[i].alpha;
        int myIdx = _markerRootRt.GetSiblingIndex();
        var sib = new System.Text.StringBuilder();
        for (int i = myIdx; i < parent.childCount && i < myIdx + 8; i++)
        {
            if (sib.Length > 0) sib.Append(',');
            sib.Append(parent.GetChild(i).name);
        }
        Debug.Log($"[BattleRally] 动效 绑定OK root={_markerRoot.name} flag={_markerFlagRt != null} ring={_markerRingRt != null} "
                  + $"mode={(rootCanvas != null ? rootCanvas.renderMode.ToString() : "?")} cam={(cam != null ? cam.name : "null")} "
                  + $"sort={(mcv != null ? mcv.sortingOrder : -999)} alphaChain={alphaChain:F2} "
                  + $"screen=({screenPos.x:F0},{screenPos.y:F0}) local=({local.x:F1},{local.y:F1}) "
                  + $"pos=({_markerRootRt.anchoredPosition.x:F1},{_markerRootRt.anchoredPosition.y:F1}) "
                  + $"world={_markerRootRt.position} parentRect={parent.rect.width:F0}x{parent.rect.height:F0} "
                  + $"sibling={myIdx}/{parent.childCount} 后序=[{sib}] active={_markerRoot.activeSelf}");
    }

    static void TickMarker()
    {
        if (!_markerVisible) return;
        _markerAge += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_markerAge / MARKER_ANIM_SEC);
        float eased = 1f - (1f - k) * (1f - k);   // easeOut：出来快、末尾收住
        ApplyMarkerFrame(eased);
        if (_markerAge >= MARKER_SHOW_SEC) HideMarker();
    }

    static void ApplyMarkerFrame(float k)
    {
        // 旗子与圈共用同一个 k → 旗子落到地点与圈缩到当前大小严格同时完成
        if (_markerFlagRt != null)
        {
            var p = _flagIdlePos;
            p.y += MARKER_FLAG_DROP * (1f - k);
            _markerFlagRt.anchoredPosition = p;
        }
        if (_markerRingRt != null)
        {
            float s = Mathf.Lerp(MARKER_RING_FROM_MUL, 1f, k);
            _markerRingRt.localScale = _ringIdleScale * s;
        }
    }

    static void HideMarker()
    {
        if (_markerRoot != null)
        {
            if (_markerFlagRt != null) _markerFlagRt.anchoredPosition = _flagIdlePos;
            if (_markerRingRt != null) _markerRingRt.localScale = _ringIdleScale;
            _markerRoot.SetActive(false);
        }
        _markerVisible = false;
        _markerAge = 0f;
    }


    /// <summary>取某单位本帧朝集结点的方向；到点返回 zero。返回 false 表示当前不在集结态。</summary>
    public static bool TryGetMoveDir(UnitBase u, out Vector2 dir)
    {
        dir = Vector2.zero;
        if (!_active || u == null) return false;
        float dx = _point.x - u.transform.position.x;
        float dy = _point.y - u.FootY;
        if (Mathf.Abs(dx) <= ARRIVE_EPS && Mathf.Abs(dy) <= ARRIVE_EPS) return true;
        dir = new Vector2(
            Mathf.Abs(dx) > 0.02f ? Mathf.Sign(dx) : 0f,
            Mathf.Abs(dy) > 0.02f ? Mathf.Sign(dy) : 0f);
        return true;
    }
}
