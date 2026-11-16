using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 全局 UI 点击音效（2026-10-24 主人拍板）。
///
/// 设计要点（铁律约束）：
/// - 单一音效出口 = <see cref="GameAudio.PlaySfx"/>（sfx 开关、Loading 静音自动生效），**不新建第二套播放器**。
/// - 不碰任何 prefab / 不逐按钮挂组件；靠 EventSystem 射线判定落点是否指向可交互控件。
/// - 缺素材只 LogError（铁律 15/24：不兜底、不静默）。
///
/// 【2026-11-14 主人报「现在有的点击的时候没有播放点击音效」】两处根因，都已修：
/// ① 判定太窄 —— 旧口径只认父链上的 <see cref="Selectable"/>。本工程有非 Selectable 的可交互控件
///    （DialogueUI.TapAdvanceRelay 剧情推进层只实现 IPointerClickHandler、
///    BattleBackpackItemDrag 背包格子只实现 IPointerClickHandler、
///    WorldMapPopup 地图面板只实现 IPointerDownHandler）→ 这些点了永远静音。
///    口径与 BattleRally.IsOverInteractiveUI（2026-11-11 第四版，上一轮踩过同一个坑）对齐：
///    Selectable / IPointerClickHandler / IPointerDownHandler 三者任一。
/// ② 判定时机太晚 —— 旧口径等到**抬起帧**才射线。关闭钮 / 页签 / 开始战斗这类按钮在 onClick 里
///    当场 SetActive(false) 或切页，而 EventSystem 的 click 回调与 UiClickSfx.Update 同为默认执行顺序、
///    谁先跑不确定；一旦 EventSystem 先跑，抬起时那个按钮已经不在 → 那一下静音（=「有的点击没声」）。
///    改法：**按下那一帧就快照判定结果**，抬起帧只判「同一次按压 + 没滑走」。
///    顺带把「拖列表」和「点按钮」分开：按下到抬起的位移超过 TapSlopRatio 视为拖拽，不播。
///
/// 素材（路径，走 Resources 加载，不依赖 prefab guid）：
///   Assets/Resources/Audio/SFX/ui_click.aif  ← 主人指定：SoundPack01/Rise03.aif
/// </summary>
public sealed class UiClickSfx : MonoBehaviour
{
    const string ClipPath = "Audio/SFX/ui_click";   // 资源真源：Resources/ 下，不依赖 prefab 引用
    // 【2026-11-14 主人报「点击音效听不见」】0.4 → 1.0。
    // 素材（SoundPack01/Rise03.aif）本身偏轻：mean -26.7dB / 峰值 -4.8dB，再叠 0.4 只剩 -34.7dB，
    // 落在 bgm_town(-14.4dB) 的掩蔽区里。素材不重导，这里给满音量，靠 GameBgm.DefaultVolume 降档拉开层次。
    // 【2026-11-16 主人「点击音效声音减一点」】1.0 -> 0.7（约 -3dB）；比 2026-11-14 听不见的 0.4 档仍高。
    const float Volume = 0.7f;

    /// <summary>抬起点相对按下点的最大允许偏移（屏高百分比）。超过即判为滑动 / 拖拽，不播点击音。</summary>
    const float TapSlopRatio = 0.02f;

    static UiClickSfx _inst;
    static AudioClip _clip;
    static bool _missingLogged;

    bool _pressing;
    bool _pressOnClickable;
    int _pressFingerId = -1;      // -1 = 鼠标
    Vector2 _pressPos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_inst != null) return;
        // 运行中重编译会触发 Domain Reload：static 清空，但 DontDestroyOnLoad 的节点还活着。
        // 不先接管就会 new 出第二份 → 一次点击播两遍（GameBgm.EnsureHost 2026-11-14 同类坑）。
        var existing = Object.FindObjectOfType<UiClickSfx>();
        if (existing != null) { _inst = existing; return; }

        var go = new GameObject("UiClickSfx", typeof(UiClickSfx));
        go.hideFlags = HideFlags.DontSaveInEditor;   // 运行时节点，不入场景存档
        Object.DontDestroyOnLoad(go);
        _inst = go.GetComponent<UiClickSfx>();
    }

    /// <summary>
    /// 点击音效素材唯一取货口：Resources/Audio/SFX/ui_click。
    /// 2026-11-07 主人「不要在查音效为什么丢了，直接补上一个 ui_click」——
    /// 不再追究旧链路为何失效，这里在 Awake 与每次点击前都确保素材在手，
    /// 资源晚导入 / 首次加载没拿到的情况，下一次点击自己就补上（缺素材只报一次错，不静默）。
    /// </summary>
    static void EnsureClip()
    {
        if (_clip != null) return;
        _clip = Resources.Load<AudioClip>(ClipPath);
        if (_clip == null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                Debug.LogError("[UiClickSfx] 缺少点击音效素材：Resources/" + ClipPath +
                               "（请把 ui_click 放进 Assets/Resources/Audio/SFX/，由团结生成 .meta 后提交）");
            }
            return;
        }
        _missingLogged = false;
        Debug.Log("[UiClickSfx] 点击音效已就位：Resources/" + ClipPath);
    }

    void Awake()
    {
        EnsureClip();
    }

    void Update()
    {
        var es = EventSystem.current;
        if (es == null)
        {
            _pressing = false;      // 没有 EventSystem 就没有点击判定，别留着半截按压状态
            return;
        }

        if (!_pressing)
        {
            DetectPressBegin(es);
            return;
        }
        DetectPressEnd();
    }

    /// <summary>按下：记状态，并当场判定这一下落在什么控件上（这一帧界面还没被关闭）。</summary>
    void DetectPressBegin(EventSystem es)
    {
        // 触摸优先：移动端触摸会同时映射成鼠标事件，只走触摸分支避免一次按压播两次
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                BeginPress(es, t.position, t.fingerId);
                return;
            }
            return;
        }
        if (Input.GetMouseButtonDown(0))
            BeginPress(es, Input.mousePosition, -1);
    }

    void BeginPress(EventSystem es, Vector2 screen, int fingerId)
    {
        _pressing = true;
        _pressFingerId = fingerId;
        _pressPos = screen;
        _pressOnClickable = FindClickTarget(es, screen) != null;
    }

    /// <summary>抬起：找到同一次按压的结束点，交给 FinishPress 判定是否算一次点击。</summary>
    void DetectPressEnd()
    {
        if (_pressFingerId < 0)
        {
            if (Input.GetMouseButtonUp(0)) { FinishPress(Input.mousePosition); return; }
            if (!Input.GetMouseButton(0)) _pressing = false;   // 抬起事件丢了，清状态不补音
            return;
        }

        if (Input.touchCount <= 0)
        {
            // 个别设备抬起那一帧 touchCount 直接归零（Ended 帧不保证一定看得到）。
            // 安卓上触摸同时映射一次鼠标抬起事件，用它补结算，位置取最后触点。
            if (Input.GetMouseButtonUp(0)) FinishPress(Input.mousePosition);
            else _pressing = false;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId != _pressFingerId) continue;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                FinishPress(t.position);
            return;   // 这根手指还在按着，等它抬起
        }
    }

    void FinishPress(Vector2 upPos)
    {
        bool wasClickable = _pressOnClickable;
        _pressing = false;
        _pressOnClickable = false;
        if (!wasClickable) return;

        float slop = Mathf.Max(8f, Screen.height * TapSlopRatio);
        if (Vector2.Distance(upPos, _pressPos) > slop) return;   // 滑走 = 拖拽，不是点击

        EnsureClip();
        if (_clip == null) return;
        GameAudio.PlaySfx(_clip, Volume);
    }

    /// <summary>
    /// 这一下按在了「会响应点击的控件」上吗？
    /// 三个条件与 EventSystem 实际分发口径一致（同 <see cref="BattleRally"/> 2026-11-11 第四版口径，
    /// 别再退回只认 Selectable）：Selectable（Button/Toggle/Slider/ScrollRect…）·
    /// IPointerClickHandler（自定义点击层）· IPointerDownHandler（按下型控件）。
    /// </summary>
    static GameObject FindClickTarget(EventSystem es, Vector2 screen)
    {
        var ped = new PointerEventData(es) { position = screen };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            var g = hits[i].gameObject;
            if (g == null) continue;
            // 沿父链找：Button 常挂在无图的行根/父节点上，命中又落在子 Label、Icon、底图上，
            // 只判 g 自身会把这类点击全漏掉（点了没音效）。三条判定都含自身。
            // 顺序按命中概率排（Selectable 最常见），先命中先返回，少几次父链遍历的 GC。
            if (g.GetComponentInParent<Selectable>() != null) return g;
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(g);
            if (clickHandler != null) return clickHandler;
            var downHandler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(g);
            if (downHandler != null) return downHandler;
        }
        return null;
    }
}
