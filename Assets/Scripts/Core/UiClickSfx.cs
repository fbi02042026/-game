using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// 全局 UI 点击音效（2026-10-24 主人拍板）。
///
/// 设计要点（铁律约束）：
/// - 单一音效出口 = <see cref="GameAudio.PlaySfx"/>（sfx 开关、Loading 静音自动生效），**不新建第二套播放器**。
/// - 不碰任何 prefab / 不逐按钮挂组件；靠 EventSystem 射线判定落点是否指向 <see cref="Selectable"/>（Button 等）。
/// - Update 轮询天然每帧最多播一次（触摸/鼠标分支互斥），无需额外去重。
/// - 缺素材只 LogError（铁律 15/24：不兜底、不静默）。
///
/// 素材（路径，走 Resources 加载，不依赖 prefab guid）：
///   Assets/Resources/Audio/SFX/ui_click.aif  ← 主人指定：SoundPack01/Rise03.aif
/// </summary>
public sealed class UiClickSfx : MonoBehaviour
{
    const string ClipPath = "Audio/SFX/ui_click";   // 资源真源：Resources/ 下，不依赖 prefab 引用
    const float Volume = 0.4f;

    static UiClickSfx _inst;
    static AudioClip _clip;
    static bool _missingLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_inst != null) return;
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
        if (EventSystem.current == null) return;

        bool up = false;
        Vector2 pos = Vector2.zero;

        // 触摸优先：取刚抬起的手指位置（移动端 touchCount>0，跳过鼠标分支避免双触发）
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended) { up = true; pos = t.position; break; }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            up = true;
            pos = Input.mousePosition;
        }

        if (!up) return;
        EnsureClip();                 // 资源晚导入时下一次点击自己补上，不让一次加载失败永久静音
        if (_clip == null) return;

        // 射线判定落点是否指向一个可交互 UI 元素（Button / Selectable）
        var ped = new PointerEventData(EventSystem.current) { position = pos };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            var g = hits[i].gameObject;
            if (g == null) continue;
            // 沿父链找：Button 常挂在无图的行根/父节点上，命中又落在子 Label、Icon、底图上，
            // 只判 g 自身会把这类点击全漏掉（点了没音效）。GetComponentInParent 含自身。
            if (g.GetComponentInParent<Selectable>() != null)
            {
                GameAudio.PlaySfx(_clip, Volume);
                break;
            }
        }
    }
}
