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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_inst != null) return;
        var go = new GameObject("UiClickSfx", typeof(UiClickSfx));
        go.hideFlags = HideFlags.DontSaveInEditor;   // 运行时节点，不入场景存档
        Object.DontDestroyOnLoad(go);
        _inst = go.GetComponent<UiClickSfx>();
    }

    void Awake()
    {
        // 路径加载，缺图/缺文件只报错不兜底
        _clip = Resources.Load<AudioClip>(ClipPath);
        if (_clip == null)
            Debug.LogError("[UiClickSfx] 缺少点击音效素材：Resources/" + ClipPath +
                           ".aif（请把文件放进 Assets/Resources/Audio/SFX/，由团结生成 .meta 后提交）");
    }

    void Update()
    {
        if (_clip == null || EventSystem.current == null) return;

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

        // 射线判定落点是否指向一个可交互 UI 元素（Button / Selectable）
        var ped = new PointerEventData(EventSystem.current) { position = pos };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            var g = hits[i].gameObject;
            if (g == null) continue;
            if (g.GetComponent<Selectable>() != null)
            {
                GameAudio.PlaySfx(_clip, Volume);
                break;
            }
        }
    }
}
