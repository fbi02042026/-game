using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

/// <summary>
/// 【2026-11-09 主人拍板】战斗中「点击地点 → 我方全体集结」。
/// 口径：只在当前相机视野 + map/walk 可行走区内可点；点击不推动相机 / 推图；
///       引导期间禁点；首人到达后 2 秒才恢复自动索敌；落点有敌人立即恢复。
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
    /// <summary>点击落点向屏内收的边距（与 GetBattleVisibleX 同口径）。</summary>
    const float TAP_PAD = 0.35f;

    // 【2026-11-09 主人摆的「集结」节点动效】旗子从上落下 + 圈由大缩小到当前大小，
    // 两者同一进度同时完成；整组出现 2 秒后隐藏。
    const float MARKER_SHOW_SEC = 2f;
    const float MARKER_ANIM_SEC = 0.18f;      // 出来快：0.18 秒内播完
    const float MARKER_FLAG_DROP = 120f;      // 旗子起始高出落点多少（UI 单位）
    const float MARKER_RING_FROM_MUL = 2.4f;  // 圈起始放大倍数，缩到预制体里现在的大小

    static Vector3 _point;
    static bool _active;
    static float _firstArriveAt = -1f;
    static float _resumeAt;

    static GameObject _markerRoot;
    static RectTransform _markerRootRt;
    static RectTransform _markerFlagRt;
    static RectTransform _markerRingRt;
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
        if (_active)
            Hero.Instance?.BeginAutoAcquireOnRelease();
        _active = false;
        _firstArriveAt = -1f;
        _resumeAt = 0f;
        _point = Vector3.zero;
        HideMarker();
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

    /// <summary>与 BattleManager.IsBattlefieldTap 同口径：只认战场空白区点击。</summary>
    static bool ReadBattlefieldTap(out Vector2 screen)
    {
        var es = EventSystem.current;
        if (Input.GetMouseButtonDown(0))
        {
            if (es == null || !es.IsPointerOverGameObject())
            {
                screen = (Vector2)Input.mousePosition;
                return true;
            }
        }
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began
                && (es == null || !es.IsPointerOverGameObject(t.fingerId)))
            {
                screen = t.position;
                return true;
            }
        }
        screen = Vector2.zero;
        return false;
    }

    /// <summary>屏幕点 → 世界落点；X 越当前相机视野直接忽略（不推镜头），Y 钳到 walk 地面条带。</summary>
    static void TrySetPoint(Vector2 screen, BattleManager bm)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float planeZ = bm.unitRoot != null ? bm.unitRoot.position.z : 0f;
        Vector3 s = new Vector3(screen.x, screen.y, planeZ - cam.transform.position.z);
        Vector3 w = cam.ScreenToWorldPoint(s);

        // X：当前相机视野内（相机不动 → 点击不会推动镜头 / 推图）
        BattleManager.GetBattleVisibleX(out float minX, out float maxX, TAP_PAD);
        if (w.x < minX || w.x > maxX) return;

        // Y：超出地面条带时钳到边界（唯一真源 BattleLaneBounds.ClampLaneOffset）
        float lane = BattleLaneBounds.ClampLaneOffset(w.y - UnitBase.GROUND_Y);

        _point = new Vector3(w.x, UnitBase.GROUND_Y + lane, planeZ);
        _active = true;
        _firstArriveAt = -1f;
        _resumeAt = 0f;
        // 切进手动态：不调这个，Hero.AIUpdate 的 _manualMove 分支进不去，集结永远不动
        Hero.Instance?.SetManualMove(Vector2.zero);
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
        if (_markerSearched) return _markerRoot != null;
        var ui = BattleUI.Instance;
        if (ui == null) return false;   // 系统未装配：下一帧再试，不刷 Error
        _markerSearched = true;
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

        _markerRoot.SetActive(false);   // 平时隐藏，点击才亮
        return true;
    }

    /// <summary>点击落点：整组挪到落点，旗子从上方落下、圈由大缩小，同一进度同时到位。</summary>
    static void PlayMarker(Vector2 screenPos)
    {
        if (!EnsureMarkerNodes()) return;
        if (_markerRootRt == null) return;
        var parent = _markerRootRt.parent as RectTransform;
        var cam = Camera.main;
        if (parent == null || cam == null)
        {
            Debug.LogError("[BattleRally] 集结动效：父容器 / 主相机取不到，无法把「集结」摆到落点");
            return;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out Vector2 local))
        {
            Debug.LogError("[BattleRally] 集结动效：屏幕点转 UI 本地坐标失败");
            return;
        }
        _markerRootRt.anchoredPosition = local;
        _markerRoot.SetActive(true);
        _markerVisible = true;
        _markerAge = 0f;
        ApplyMarkerFrame(0f);
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
