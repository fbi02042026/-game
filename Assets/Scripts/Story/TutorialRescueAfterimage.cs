using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引导演出的单发残影 / 「一道幻影」（运行时生成，不动任何预制体）。
///
/// <para>【2026-10-13 主人拍板】清场演出改成「一道幻影移到怪身后 + 全场同时刀光」后，
/// 原「冲刺拖尾」（连续掉残影、显式开 / 显式停）整段退役 —— 只剩两个单发入口：
/// <see cref="SpawnGhost"/>（原地留影淡出）与 <see cref="SlideGhost"/>（残影从 A 滑到 B）。</para>
///
/// <para>写法照 <see cref="KillComboAfterimage"/>：复制当前所有 SpriteRenderer 再淡出；
/// 淡出与销毁统一由本类的 Update 收尾，不留永久对象。</para>
/// </summary>
public class TutorialRescueAfterimage : MonoBehaviour
{
    /// <summary>残影起步 alpha。</summary>
    const float GhostAlpha = 0.5f;
    /// <summary>残影存活时长（秒，含淡出前的稳定段）。</summary>
    const float GhostLife = 0.3f;
    /// <summary>淡出前的稳定时长（秒）。</summary>
    const float GhostFadeDelay = 0.05f;

    static TutorialRescueAfterimage _runner;

    readonly List<Ghost> _ghosts = new List<Ghost>();
    /// <summary>滑行中的残影（2026-10-13 主人拍板「一道幻影移到怪身后」）：位置补间，淡出仍归 _ghosts 管。</summary>
    readonly List<Slide> _slides = new List<Slide>();

    struct Ghost
    {
        public GameObject root;
        public SpriteRenderer[] srs;
        public float t;
    }

    /// <summary>一道残影从 A 滑到 B（补间 + 不淡出控制，淡出由 _ghosts 按 GhostLife 收尾）。</summary>
    struct Slide
    {
        public GameObject root;
        public float fromX;
        public float toX;
        public float left;
        public float dur;
    }

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 立刻复制一份 <paramref name="unit"/> 的 SpriteRenderer 当残影，<paramref name="life"/> 秒后淡出销毁。
    /// </summary>
    public static void SpawnGhost(UnitBase unit, float life = GhostLife, float fadeDelay = GhostFadeDelay)
    {
        if (unit == null) return;
        var r = EnsureRunner();
        if (r == null) return;
        var root = r.BuildGhost(unit);
        if (root == null) return;
        r._ghosts.Add(new Ghost
        {
            root = root,
            srs = root.GetComponentsInChildren<SpriteRenderer>(true),
            t = -Mathf.Max(0f, fadeDelay),
        });
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】「一道幻影」：复制一份 <paramref name="unit"/> 当残影，
    /// 在 <paramref name="dur"/> 秒内从它当前位置滑到 <paramref name="toX"/>（z 保持 <paramref name="z"/>），
    /// 滑完停在那，淡出销毁仍归 <see cref="GhostLife"/> 收尾。
    /// <para>调用方通常紧接着把真身 <c>SetWorldPosition</c> 到同一落点 —— 视觉＝残影划过、真身已到位。</para>
    /// </summary>
    public static void SlideGhost(UnitBase unit, float toX, float z, float dur = 0.25f)
    {
        if (unit == null || dur <= 0f) return;
        var r = EnsureRunner();
        if (r == null) return;
        var root = r.BuildGhost(unit);
        if (root == null) return;
        r._ghosts.Add(new Ghost
        {
            root = root,
            srs = root.GetComponentsInChildren<SpriteRenderer>(true),
            t = -GhostFadeDelay,
        });
        r._slides.Add(new Slide
        {
            root = root,
            fromX = root.transform.position.x,
            toX = toX,
            left = dur,
            dur = dur,
        });
        // z 由调用方显式指定（幻影与真身落点同层，别留在半空）
        var zp = root.transform.position;
        zp.z = z;
        root.transform.position = zp;
    }

    // ============================================================
    // 内部实现
    // ============================================================

    static TutorialRescueAfterimage EnsureRunner()
    {
        if (_runner != null) return _runner;

        var host = new GameObject("TutorialRescueAfterimage");
        var bm = BattleManager.Instance;
        if (bm != null && bm.unitRoot != null)
            host.transform.SetParent(bm.unitRoot, false);
        _runner = host.AddComponent<TutorialRescueAfterimage>();
        return _runner;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 滑行残影的位置补间（淡出/销毁由下面 _ghosts 那段统一收尾）
        for (int i = _slides.Count - 1; i >= 0; i--)
        {
            var s = _slides[i];
            if (s.root == null) { _slides.RemoveAt(i); continue; }

            s.left -= dt;
            float k = Mathf.Clamp01(1f - Mathf.Max(0f, s.left) / s.dur);
            var p = s.root.transform.position;
            p.x = Mathf.Lerp(s.fromX, s.toX, k);
            s.root.transform.position = p;
            if (s.left <= 0f) _slides.RemoveAt(i);
            else _slides[i] = s;
        }

        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var g = _ghosts[i];
            if (g.root == null) { _ghosts.RemoveAt(i); continue; }

            g.t += dt;
            float k = Mathf.Clamp01(g.t / GhostLife);
            if (g.srs != null)
            {
                for (int j = 0; j < g.srs.Length; j++)
                {
                    var sr = g.srs[j];
                    if (sr == null) continue;
                    var c = sr.color;
                    c.a = GhostAlpha * (1f - k);
                    sr.color = c;
                }
            }
            if (k >= 1f)
            {
                Destroy(g.root);
                _ghosts.RemoveAt(i);
            }
            else
            {
                _ghosts[i] = g;
            }
        }

        // 残影都收完：runner 自己也销毁，场景里不留东西
        if (_ghosts.Count == 0 && _slides.Count == 0)
        {
            if (_runner == this) _runner = null;
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i].root != null) Destroy(_ghosts[i].root);
        _ghosts.Clear();
        _slides.Clear();
        if (_runner == this) _runner = null;
    }

    /// <summary>复制单位当前所有可见 SpriteRenderer 成一个静态残影节点。</summary>
    GameObject BuildGhost(UnitBase unit)
    {
        var srs = unit.GetComponentsInChildren<SpriteRenderer>(false);
        if (srs == null || srs.Length == 0) return null;

        var root = new GameObject("RescueGhost");
        var bm = BattleManager.Instance;
        if (bm != null && bm.unitRoot != null)
            root.transform.SetParent(bm.unitRoot, true);
        var src0 = srs[0];
        root.transform.position = src0.transform.position;
        root.transform.rotation = src0.transform.rotation;

        bool any = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var src = srs[i];
            if (src == null || !src.enabled || !src.gameObject.activeInHierarchy) continue;
            if (src.sprite == null || src.color.a < 0.05f) continue;

            var go = new GameObject(src.name + "_ghost");
            go.transform.SetParent(root.transform, false);
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;

            var dst = go.AddComponent<SpriteRenderer>();
            dst.sprite = src.sprite;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.sortingLayerID = src.sortingLayerID;
            // 排序压到单位下面，别挡住当前这一剑的攻击特效
            dst.sortingOrder = src.sortingOrder - 1;
            dst.sharedMaterial = src.sharedMaterial;

            var c = src.color;
            c.a = GhostAlpha;
            dst.color = c;
            any = true;
        }

        if (!any)
        {
            Destroy(root);
            return null;
        }
        return root;
    }
}
