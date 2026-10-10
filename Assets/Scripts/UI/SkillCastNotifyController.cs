using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗内「技能释放通知」横条：玩家 / 佣兵从左侧飞入，敌人从右侧镜像飞入。
/// 模板直接取自 BattleUI 下的 SkillUI/skill（含子节点 Icon / name），
/// 运行时 Instantiate + 对象池复用；预制体结构不改（只把原模板隐藏）。
/// 纯代码插值，不依赖 DOTween / LeanTween。
/// </summary>
public class SkillCastNotifyController : MonoBehaviour
{
    // ============================================================
    // 常量
    // ============================================================

    /// <summary>单条总生命周期（秒）。</summary>
    const float LifeTime = 2.0f;

    /// <summary>第一段：飞到 finalPos 的 <see cref="FlyReachRatio"/> 处，EaseOutQuad。</summary>
    const float FlyTime = 0.5f;

    /// <summary>第一段结束时走完 finalPos 行程的比例。</summary>
    const float FlyReachRatio = 0.75f;

    /// <summary>第二段：从 75% 点慢移到 finalPos，EaseInQuad，时长 LifeTime - FlyTime。</summary>
    const float SettleTime = LifeTime - FlyTime;   // 1.5s

    /// <summary>开始淡出的时刻，之后 LifeTime - FadeStartTime 秒内 alpha 1→0。</summary>
    const float FadeStartTime = 1.5f;

    /// <summary>起始 x（玩家侧，模板锚点在左）。敌人侧取相反数。</summary>
    const float StartOffsetX = -700f;

    /// <summary>多条堆叠时每往下一格的垂直间距。</summary>
    const float EntrySpacing = 80f;

    /// <summary>玩家侧 / 敌人侧各自最多同时显示的条目数。</summary>
    const int MaxEntries = 5;

    /// <summary>finalPos.x 距屏幕边缘的安全边距，避开刘海 / 打孔。</summary>
    const float SafeMargin = 40f;

    /// <summary>堆叠目标 y 变化时旧条目的靠拢速度（越大越快）。</summary>
    const float StackLerpSpeed = 12f;

    // ============================================================
    // 数据
    // ============================================================

    class NotifyEntry
    {
        public RectTransform rt;
        public Image bg;
        public Image icon;
        public Text label;
        public float timer;
        public Vector2 startPos;
        public Vector2 finalPos;
        public bool isEnemy;

        /// <summary>当前实际 y。堆叠 index 变化时平滑靠拢目标 y，避免整条跳动。</summary>
        public float displayY;
    }

    static SkillCastNotifyController _inst;

    /// <summary>BattleUI 存在但 SkillUI 缺失时置位，避免每帧刷同一条错误。</summary>
    static bool _bindFailed;

    RectTransform _root;          // SkillUI
    RectTransform _template;      // SkillUI/skill
    Image _tplBg;
    Image _tplIcon;
    Text _tplLabel;
    Vector2 _tplFinalPos;         // 模板 anchoredPosition（玩家侧最终位置）

    readonly List<NotifyEntry> _allyEntries = new List<NotifyEntry>();
    readonly List<NotifyEntry> _enemyEntries = new List<NotifyEntry>();
    readonly Queue<RectTransform> _pool = new Queue<RectTransform>();

    // ============================================================
    // 单例（挂在 SkillUI 上，随 BattleUI 一起销毁）
    // ============================================================

    public static SkillCastNotifyController Instance
    {
        get
        {
            if (_inst == null && !_bindFailed) EnsureInstance();
            return _inst;
        }
    }

    static void EnsureInstance()
    {
        var ui = BattleUI.Instance;
        // 不在战斗里（BattleUI 还没实例）不算结构错误，静默跳过，下次 Push 会重试。
        if (ui == null) return;

        var skillUi = FindDeep(ui.transform, "SkillUI");
        if (skillUi == null)
        {
            _bindFailed = true;
            Debug.LogError("[SkillCastNotify] BattleUI 下找不到 SkillUI 节点，技能通知不可用");
            return;
        }

        _inst = skillUi.GetComponent<SkillCastNotifyController>();
        if (_inst == null)
            _inst = skillUi.gameObject.AddComponent<SkillCastNotifyController>();
    }

    // ============================================================
    // 生命周期
    // ============================================================

    void Awake()
    {
        _root = transform as RectTransform;
        if (_root == null)
        {
            Debug.LogError("[SkillCastNotify] 控制器必须挂在 RectTransform 上（SkillUI），当前挂在 " + name);
            enabled = false;
            return;
        }

        var tplTf = transform.Find("skill");
        if (tplTf == null)
        {
            Debug.LogError("[SkillCastNotify] SkillUI 下找不到模板节点 skill");
            enabled = false;
            return;
        }

        _template = tplTf as RectTransform;
        if (_template == null)
        {
            Debug.LogError("[SkillCastNotify] SkillUI/skill 不是 RectTransform");
            enabled = false;
            return;
        }

        _tplBg = _template.GetComponent<Image>();
        if (_tplBg == null)
            Debug.LogError("[SkillCastNotify] SkillUI/skill 上找不到背景 Image");

        var iconTf = _template.Find("Icon");
        if (iconTf == null)
            Debug.LogError("[SkillCastNotify] SkillUI/skill 下找不到子节点 Icon");
        else
        {
            _tplIcon = iconTf.GetComponent<Image>();
            if (_tplIcon == null)
                Debug.LogError("[SkillCastNotify] SkillUI/skill/Icon 上找不到 Image");
        }

        var nameTf = _template.Find("name");
        if (nameTf == null)
            Debug.LogError("[SkillCastNotify] SkillUI/skill 下找不到子节点 name");
        else
        {
            _tplLabel = nameTf.GetComponent<Text>();
            if (_tplLabel == null)
                Debug.LogError("[SkillCastNotify] SkillUI/skill/name 上找不到 Text");
        }

        _tplFinalPos = _template.anchoredPosition;

        // 模板只作克隆源，运行时隐藏
        _template.gameObject.SetActive(false);
    }

    void Update()
    {
        // UI 不受暂停 / 时间缩放影响
        float dt = Time.unscaledDeltaTime;
        TickList(_allyEntries, dt);
        TickList(_enemyEntries, dt);
    }

    void OnDestroy()
    {
        _allyEntries.Clear();
        _enemyEntries.Clear();
        _pool.Clear();
        if (_inst == this) _inst = null;
    }

    // ============================================================
    // 对外接口
    // ============================================================

    /// <summary>玩家技能释放通知（左侧）。icon 走 BattleUI.LoadRunSkillIcon。</summary>
    public static void PushPlayerSkill(string skillId, string skillName)
    {
        var c = Instance;
        if (c == null) return;
        c.Push(BattleUI.LoadRunSkillIcon(skillId), LabelOf(skillName, skillId), false);
    }

    /// <summary>佣兵技能释放通知（左侧）。icon 走 MercSkillTable.LoadIcon。</summary>
    public static void PushMercSkill(string mercId, string displayName, string skillId, string skillName)
    {
        var c = Instance;
        if (c == null) return;
        c.Push(MercSkillTable.LoadIcon(skillId), LabelOf(skillName, skillId, displayName), false);
    }

    /// <summary>敌人技能释放通知（右侧镜像）。</summary>
    public static void PushEnemySkill(string skillId, string enemyName)
    {
        var c = Instance;
        if (c == null) return;
        c.Push(BattleUI.LoadRunSkillIcon(skillId), LabelOf(enemyName, skillId), true);
    }

    /// <summary>名字优先取显示名，空则退回 skillId（再空则退回 displayName）。</summary>
    static string LabelOf(string primary, string skillId, string lastResort = null)
    {
        if (!string.IsNullOrEmpty(primary)) return primary;
        if (!string.IsNullOrEmpty(skillId)) return skillId;
        return string.IsNullOrEmpty(lastResort) ? "" : lastResort;
    }

    // ============================================================
    // 内部：生成 / 推进 / 回收
    // ============================================================

    void Push(Sprite icon, string label, bool isEnemy)
    {
        if (_template == null) return;   // Awake 已 LogError

        var list = isEnemy ? _enemyEntries : _allyEntries;
        // 超过上限：先回收最老的一条（队尾）
        while (list.Count >= MaxEntries)
            RecycleAt(list, list.Count - 1);

        var rt = TakeFromPool();
        // lb 是 Text（不是 Image）：BindVisual 第 4 个 out 参数与 NotifyEntry.label 都是 Text。
        Image bg;
        Image ic;
        Text lb;
        if (!BindVisual(rt, out bg, out ic, out lb)) return;

        float finalX = isEnemy ? -_tplFinalPos.x : _tplFinalPos.x;
        finalX = ClampToSafeArea(finalX);
        float startX = isEnemy ? -StartOffsetX : StartOffsetX;

        var entry = new NotifyEntry
        {
            rt = rt,
            bg = bg,
            icon = ic,
            label = lb,
            timer = 0f,
            startPos = new Vector2(startX, _tplFinalPos.y),
            finalPos = new Vector2(finalX, _tplFinalPos.y),
            isEnemy = isEnemy,
            displayY = _tplFinalPos.y,
        };

        if (entry.icon != null) entry.icon.sprite = icon;
        if (entry.label != null) entry.label.text = label ?? "";
        SetAlpha(entry, 1f);

        // 最新一条插到队首（占模板原位），旧条目 index 变大后平滑下移
        list.Insert(0, entry);
        entry.rt.anchoredPosition = entry.startPos;
    }

    void TickList(List<NotifyEntry> list, float dt)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            e.timer += dt;
            if (e.timer >= LifeTime)
            {
                RecycleAt(list, i);
                continue;
            }

            // 堆叠目标 y 随 index 变化；旧条目平滑靠拢，避免新条插入时整列跳动
            float targetY = _tplFinalPos.y - i * EntrySpacing;
            e.displayY = Mathf.Lerp(e.displayY, targetY, 1f - Mathf.Exp(-StackLerpSpeed * dt));

            Vector2 p;
            if (e.timer <= FlyTime)
            {
                float k = EaseOutQuad(e.timer / FlyTime);
                p = e.startPos + (e.finalPos - e.startPos) * (FlyReachRatio * k);
            }
            else
            {
                float k = EaseInQuad(Mathf.Clamp01((e.timer - FlyTime) / SettleTime));
                Vector2 p75 = e.startPos + (e.finalPos - e.startPos) * FlyReachRatio;
                p = Vector2.Lerp(p75, e.finalPos, k);
            }

            if (e.rt != null)
                e.rt.anchoredPosition = new Vector2(p.x, e.displayY);

            float a = e.timer <= FadeStartTime
                ? 1f
                : 1f - (e.timer - FadeStartTime) / (LifeTime - FadeStartTime);
            SetAlpha(e, Mathf.Clamp01(a));
        }
    }

    void RecycleAt(List<NotifyEntry> list, int index)
    {
        if (index < 0 || index >= list.Count) return;
        var e = list[index];
        list.RemoveAt(index);

        // 回收前清空 sprite 与文字，避免下一条复用到池里时闪现旧内容
        if (e.icon != null) e.icon.sprite = null;
        if (e.label != null) e.label.text = "";

        if (e.rt != null)
        {
            e.rt.gameObject.SetActive(false);
            _pool.Enqueue(e.rt);
        }
    }

    RectTransform TakeFromPool()
    {
        while (_pool.Count > 0)
        {
            var rt = _pool.Dequeue();
            if (rt != null) return rt;
        }

        var go = Instantiate(_template.gameObject, _root, false);
        go.name = "skill_notify";
        go.transform.localScale = Vector3.one;   // 不靠 scale.x=-1 做镜像，保证文字不镜像
        return go.transform as RectTransform;
    }

    bool BindVisual(RectTransform rt, out Image bg, out Image icon, out Text label)
    {
        bg = null; icon = null; label = null;
        if (rt == null)
        {
            Debug.LogError("[SkillCastNotify] 取到的条目 RectTransform 为空");
            return false;
        }

        bg = rt.GetComponent<Image>();
        if (bg == null)
            Debug.LogError("[SkillCastNotify] 条目 " + rt.name + " 上找不到背景 Image");

        var iconTf = rt.Find("Icon");
        if (iconTf == null)
            Debug.LogError("[SkillCastNotify] 条目 " + rt.name + " 下找不到子节点 Icon");
        else
        {
            icon = iconTf.GetComponent<Image>();
            if (icon == null)
                Debug.LogError("[SkillCastNotify] 条目 " + rt.name + "/Icon 上找不到 Image");
        }

        var nameTf = rt.Find("name");
        if (nameTf == null)
            Debug.LogError("[SkillCastNotify] 条目 " + rt.name + " 下找不到子节点 name");
        else
        {
            label = nameTf.GetComponent<Text>();
            if (label == null)
                Debug.LogError("[SkillCastNotify] 条目 " + rt.name + "/name 上找不到 Text");
        }

        return bg != null && icon != null && label != null;
    }

    static void SetAlpha(NotifyEntry e, float a)
    {
        if (e == null) return;
        if (e.bg != null) e.bg.color = WithAlpha(e.bg.color, a);
        if (e.icon != null) e.icon.color = WithAlpha(e.icon.color, a);
        if (e.label != null) e.label.color = WithAlpha(e.label.color, a);
    }

    static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    /// <summary>finalPos.x 收进安全边距内，避开刘海 / 打孔。</summary>
    float ClampToSafeArea(float x)
    {
        float half = HalfWidth();
        float lim = Mathf.Max(0f, half - SafeMargin);
        return Mathf.Clamp(x, -lim, lim);
    }

    /// <summary>画布半宽：优先 SkillUI 自身矩形，其次父 Canvas，最后退回屏幕像素。</summary>
    float HalfWidth()
    {
        if (_root != null && _root.rect.width > 1f)
            return _root.rect.width * 0.5f;

        var canvas = GetComponentInParent<Canvas>();
        var crt = canvas != null ? canvas.transform as RectTransform : null;
        if (crt != null && crt.rect.width > 1f)
            return crt.rect.width * 0.5f;

        return Screen.width * 0.5f;
    }

    // ============================================================
    // 缓动
    // ============================================================

    /// <summary>快进慢出：末速度为 0，正好接上第二段的 EaseInQuad。</summary>
    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    /// <summary>慢进快出：第二段从静止起步，1.5s 内慢速贴到终点。</summary>
    static float EaseInQuad(float t) => t * t;

    static Transform FindDeep(Transform root, string nodeName)
    {
        if (root == null) return null;
        if (root.name == nodeName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), nodeName);
            if (hit != null) return hit;
        }
        return null;
    }
}
