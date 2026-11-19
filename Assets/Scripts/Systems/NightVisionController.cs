using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗视野显隐（清单 §2 / T4）：每帧 LateUpdate 算敌人到光心（玩家）的世界距离，
/// 超出 BattleAtmosphere.LightRadiusWorld 则隐「身体 / 头顶词条 / 名字」，血条节点保留常显，
/// 进圈短淡入（0.15~0.3s）；Boss / 精英同链路；凡是夜晚（Night）敌人隐身（身体 / 词条 / 名字）、血条常显，白天与黄昏全显，与天气无关；退战还原，无残留隐身。
/// 🔴 执行时机：必须晚于 AutoGameInitializer.ForceEnableRenderers（会强制 enabled+alpha=1），
///    故这里每帧 LateUpdate 无条件写回渲染状态，压过强制启用。圈外 AI / 索敌完全不动。
/// </summary>
public class NightVisionController : MonoBehaviour
{
    static NightVisionController _instance;

    /// <summary>战斗期按需装配；非 DDOL，随 Battle 场景销毁。</summary>
    public static NightVisionController Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<NightVisionController>();
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject("NightVisionController");
                _instance = go.AddComponent<NightVisionController>();
            }
            return _instance;
        }
    }

    /// <summary>进圈淡入时长（秒），落在 0.15~0.3s；切到夜战时出圈淡出也走同一速度。</summary>
    const float FadeSeconds = 0.22f;

    BattleAtmosphere _atmo;
    // 每只怪的当前显隐 alpha（0=全隐，1=全显）；键为怪本体，退战 OnDestroy 统一还原。
    readonly Dictionary<Monster, float> _alpha = new Dictionary<Monster, float>();

    /// <summary>按 §3 契约绑定状态源；未显式绑定时 LateUpdate 自动取 Instance。</summary>
    public void Bind(BattleAtmosphere atmo)
    {
        _atmo = atmo;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    void LateUpdate()
    {
        if (_atmo == null) _atmo = BattleAtmosphere.Instance;
        if (_atmo == null) return;

        // 凡是夜晚（Night）敌人都隐身，与天气无关（下雨/下雪的夜晚也隐身）；白天与黄昏不隐。
        bool hide = _atmo.Phase == DayPhase.Night;
        var bm = BattleManager.InstanceQuiet;
        if (bm == null || bm.monsters == null) return;

        var hero = Hero.Instance;
        if (hide && hero == null) hide = false; // 无光心（非战斗）时不隐，避免全屏怪消失
        Vector2 center = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
        float radius = Mathf.Max(0.01f, _atmo.LightRadiusWorld);
        float step = Time.deltaTime / FadeSeconds;

        var list = bm.monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i] as Monster;
            if (m == null || m.isDead) continue;

            float target = 1f;
            if (hide)
            {
                Transform body = m.GetBodyTransform();
                Vector2 mp = body != null ? (Vector2)body.position : (Vector2)m.transform.position;
                target = Vector2.Distance(center, mp) > radius ? 0f : 1f;
            }

            float cur;
            if (!_alpha.TryGetValue(m, out cur)) cur = 1f;
            cur = Mathf.MoveTowards(cur, target, step);
            _alpha[m] = cur;
            ApplyAlpha(m, cur); // 每帧写回，压过 ForceEnableRenderers 的强制 enabled+alpha
        }
    }

    /// <summary>把 alpha 写到怪身体 / 头顶标签；血条子树排除，保持常显。SpriteRenderer 与 TextMesh 改 alpha，其余 Renderer 用 enabled。</summary>
    void ApplyAlpha(Monster m, float a)
    {
        var renderers = m.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || IsBloodBar(r.transform, m.transform)) continue;
            var sr = r as SpriteRenderer;
            if (sr != null)
            {
                Color c = sr.color;
                c.a = a;
                sr.color = c;
                continue;
            }
            var tm = r.GetComponent<TextMesh>();
            if (tm != null)
            {
                Color c = tm.color;
                c.a = a;
                tm.color = c;
                continue;
            }
            r.enabled = a > 0.001f;
        }
    }

    /// <summary>血条是独立节点（HPBar / *HealthBar*），向上回溯整棵子树排除，绝不跟着身体一起隐。</summary>
    static bool IsBloodBar(Transform t, Transform root)
    {
        Transform cur = t;
        while (cur != null && cur != root)
        {
            string n = cur.name;
            if (n == "HPBar" || n.IndexOf("HealthBar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            cur = cur.parent;
        }
        return false;
    }

    void OnDestroy()
    {
        // 退战 / 切场景还原：所有仍活着的怪恢复全显，杜绝残留隐身带进下一局。
        foreach (var kv in _alpha)
        {
            if (kv.Key == null) continue;
            ApplyAlpha(kv.Key, 1f);
        }
        _alpha.Clear();
        if (_instance == this) _instance = null;
    }
}
