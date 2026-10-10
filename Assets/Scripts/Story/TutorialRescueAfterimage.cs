using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引导演出「塔克清场冲刺」的人物拖尾 / 残影（运行时生成，不动任何预制体）。
///
/// <para>【2026-10-10 主人拍板】冲刺那几秒要有连续淡出的残影，冲刺结束立刻停止生成，
/// 且不能依赖 <c>SkillCastService.IsTeamAttackSpeedBuffActive</c>（那是攻速 buff 的门控，与演出无关）。</para>
///
/// <para>写法照 <see cref="KillComboAfterimage"/>：复制当前所有 SpriteRenderer 再淡出；
/// 门控改成「显式开 / 显式停」，淡出与销毁统一由本类的 Update 收尾，不留永久对象。</para>
/// </summary>
public class TutorialRescueAfterimage : MonoBehaviour
{
    /// <summary>残影起步 alpha。</summary>
    const float GhostAlpha = 0.5f;
    /// <summary>残影存活时长（秒，含淡出前的稳定段）。</summary>
    const float GhostLife = 0.3f;
    /// <summary>生成间隔（秒）：0.07 秒一个，连起来就是拖尾。</summary>
    const float GhostInterval = 0.07f;
    /// <summary>淡出前的稳定时长（秒）。</summary>
    const float GhostFadeDelay = 0.05f;

    static TutorialRescueAfterimage _runner;

    UnitBase _trailUnit;
    float _trailLeft;
    float _spawnCd;
    readonly List<Ghost> _ghosts = new List<Ghost>();

    struct Ghost
    {
        public GameObject root;
        public SpriteRenderer[] srs;
        public float t;
    }

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 让 <paramref name="unit"/> 在 <paramref name="duration"/> 秒内持续掉残影；到时自动停止生成。
    /// 重复调用 = 续期（覆盖剩余时长），不会叠出第二个 runner。
    /// </summary>
    public static void RunTrail(UnitBase unit, float duration)
    {
        if (unit == null || duration <= 0f) return;
        var r = EnsureRunner();
        if (r == null) return;
        r._trailUnit = unit;
        r._trailLeft = duration;
        r._spawnCd = 0f;   // 起步立刻掉一个，别空一拍
    }

    /// <summary>立刻停止生成残影（冲刺结束就调）。已在淡出的残影会自己收尾。</summary>
    public static void StopTrail()
    {
        if (_runner == null) return;
        _runner._trailLeft = 0f;
        _runner._trailUnit = null;
    }

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

        if (_trailLeft > 0f && _trailUnit != null)
        {
            _trailLeft -= dt;
            _spawnCd -= dt;
            if (_spawnCd <= 0f)
            {
                _spawnCd = GhostInterval;
                var root = BuildGhost(_trailUnit);
                if (root != null)
                    _ghosts.Add(new Ghost
                    {
                        root = root,
                        srs = root.GetComponentsInChildren<SpriteRenderer>(true),
                        t = -GhostFadeDelay,
                    });
            }
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

        // 拖尾结束且残影都收完：runner 自己也销毁，场景里不留东西
        if (_trailLeft <= 0f && _ghosts.Count == 0)
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
