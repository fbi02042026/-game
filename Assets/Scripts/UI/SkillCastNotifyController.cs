using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗内「技能释放通知」横条：玩家 / 佣兵从左侧飞入，敌人从右侧飞入。
/// 敌人条整体镜像（scale.x=-1，底图/图标随条翻），名字文字反向二连翻保持方向可读
/// 【2026-10-20 主人拍板：镜像位置，字只有位置变、方向不变】。
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

    /// <summary>第一段：飞到飞入终点（见 <see cref="FlyReachRatio"/> 与 ResolveMidX），EaseOutQuad。</summary>
    const float FlyTime = 0.5f;

    /// <summary>第一段（飞入）最少走完 finalPos 行程的比例；实际取「此值」与「整条刚好完全进屏点」中更远者
    /// —— 保证变慢发生在整条进屏之后【2026-10-20】。</summary>
    const float FlyReachRatio = 0.75f;

    /// <summary>第二段：从飞入终点（≥ 整条进屏点）慢移到 finalPos，EaseInQuad，时长 LifeTime - FlyTime。</summary>
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
        public Vector2 midPos;      // 第一段（飞入）终点：至少整条完全进屏后才在这变慢
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

        // 【2026-10-18 二修】Canvas 已预置进 BattleUI 预制体（SkillUI 节点，overrideSorting=955）。
        // 旧写法运行时 AddComponent<Canvas> 在团结引擎下原生侧未建好 → 访问 overrideSorting 当场抛
        // MissingComponentException（Editor.log 122347 实锤），Awake 中断 → 模板永远绑不上 → 释放技能条静默失效。
        // 现在只取预制体画布设值（幂等），缺失就报错，绝不再 AddComponent。
        var liftCv = _root.GetComponent<Canvas>();
        if (liftCv == null)
        {
            Debug.LogError("[SkillCastNotify] SkillUI 节点缺少预置 Canvas（预制体应带 overrideSorting=955 的画布），通知条不抬层");
            enabled = false;
            return;
        }
        liftCv.overrideSorting = true;
        liftCv.sortingOrder = 955;

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

    /// <summary>敌人技能释放通知（右侧镜像）。
    /// 图标用敌人自身形象（怪物 SpriteRenderer 当前 sprite）；怪物技能 id 在玩家图标表里没图，传 skillId 只会白框。</summary>
    public static void PushEnemySkill(string skillId, string enemyName, Sprite enemySprite = null)
    {
        var c = Instance;
        if (c == null) return;
        var icon = enemySprite != null ? enemySprite : BattleUI.LoadRunSkillIcon(skillId);
        // 【2026-10-20 主人报「敌人条没有头像」】静态链路查不到活怪 sr.sprite 为空的路径，
        // 先把漏网场景打出来（怪物名 + sprite 状态 + 技能 id），复现即定位，不做兜底。
        if (icon == null)
            Debug.LogWarning($"[SkillCastNotify] 敌人技能条无头像：enemy={enemyName} skillId={skillId} " +
                             $"enemySprite={(enemySprite == null ? "null" : "有")}，图标表也无该技能图");
        c.Push(icon, LabelOf(enemyName, skillId), true);
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

        // 【2026-10-15 主人报「放技能头像没出现」根因】模板在 Awake 里 SetActive(false) 作克隆源，
        // Instantiate 会继承禁用状态；池里回收件也是禁用态 —— 不重新激活，横条永远不可见。
        rt.gameObject.SetActive(true);

        // 【2026-10-20 主人拍板「敌人条整体镜像」】敌人侧 scale.x=-1 把整条翻成镜像（底图/怪物图标
        // 随条一起翻到右侧镜像位），名字文字再反向 -1 二连翻（-1×-1=+1）保持方向可读 ——
        // 字只有位置变（挪到条左侧），方向不变。池子跨阵营复用，每次 Push 都按 isEnemy 重设。
        rt.localScale = isEnemy ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        if (lb != null)
            lb.rectTransform.localScale = isEnemy ? new Vector3(-1f, 1f, 1f) : Vector3.one;

        // 【2026-10-18 修「飘字被屏幕左边缘裁切」】SkillUI 不在画布正中（根下 x=-219），
        // 旧逻辑把模板偏移当「相对屏幕中心」用，且钳制取的是 SkillUI 自身宽度(100) → 形同虚设，
        // 终点落在根画布 x=-219、条宽 300 → 左缘 -369 出屏（画布半宽仅 360）。
        // 改为统一在根画布空间求终点，按「画布半宽 − 安全边距 − 条半宽」钳制；敌人侧按设计镜像到右侧。
        float finalX = ResolveFinalX(isEnemy);
        float startX = finalX + (isEnemy ? -StartOffsetX : StartOffsetX);

        // 【2026-10-20 主人报「变慢的地方条一半还在屏外」】旧固定 75% 行程就变慢：敌人条行程 700、
        // 条宽 300，75% 点条右缘仍出屏约 135（近半条身），之后 1.5s 才慢慢挪进屏。
        // 改为第一段终点动态前推：至少飞到「整条刚好完全进屏」的位置（可能比 75% 远）才开始变慢，
        // 之后 1.5s 慢移贴到最终位 → 进屏 → 变慢 → 停住 → 淡出。
        var midPos = new Vector2(ResolveMidX(startX, finalX, isEnemy), _tplFinalPos.y);

        var entry = new NotifyEntry
        {
            rt = rt,
            bg = bg,
            icon = ic,
            label = lb,
            timer = 0f,
            startPos = new Vector2(startX, _tplFinalPos.y),
            midPos = midPos,
            finalPos = new Vector2(finalX, _tplFinalPos.y),
            isEnemy = isEnemy,
            displayY = _tplFinalPos.y,
        };

        if (entry.icon != null)
        {
            entry.icon.sprite = icon;
            // 敌人条现在放怪物全身图（非正方形），保持比例不拉伸；玩家方形技能图不受影响
            entry.icon.preserveAspect = true;
        }
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
                p = e.startPos + (e.midPos - e.startPos) * k;
            }
            else
            {
                float k = EaseInQuad(Mathf.Clamp01((e.timer - FlyTime) / SettleTime));
                p = Vector2.Lerp(e.midPos, e.finalPos, k);
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
        go.transform.localScale = Vector3.one;   // 初始正向；敌人侧镜像在 Push 里按 isEnemy 设置
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

    /// <summary>
    /// 求终点 x（SkillUI 局部坐标）：先换算到根画布空间，再按
    /// 「画布半宽 − SafeMargin − 条半宽」钳制；敌人侧按屏幕中心镜像（右侧）。
    /// </summary>
    float ResolveFinalX(bool isEnemy)
    {
        float skillUiX = SkillUiXInRoot();
        float rootHalf = RootHalfOrScreenFallback();
        float barHalf = TemplateBarHalf();
        float lim = Mathf.Max(0f, rootHalf - SafeMargin - barHalf);

        float desiredRootX = skillUiX + _tplFinalPos.x;
        if (isEnemy) desiredRootX = -desiredRootX;   // 敌人从右侧镜像（以画布中心为轴）
        float clampedRootX = Mathf.Clamp(desiredRootX, -lim, lim);
        return clampedRootX - skillUiX;   // 换回 SkillUI 局部坐标
    }

    /// <summary>
    /// 求第一段（飞入）终点 x（SkillUI 局部坐标）【2026-10-20】：
    /// 取「原 75% 点」与「整条刚好完全进屏点」中更靠行程前方的一个 —— 保证变慢发生在整条进屏之后。
    /// </summary>
    float ResolveMidX(float startX, float finalX, bool isEnemy)
    {
        float span = Mathf.Abs(StartOffsetX);
        if (span < 1f) return finalX;
        float inScreenX = InScreenCenterX(isEnemy);
        float fullRatio = Mathf.Abs(startX - inScreenX) / span;
        float reach = Mathf.Clamp(Mathf.Max(FlyReachRatio, fullRatio), 0f, 1f);
        return startX + (finalX - startX) * reach;
    }

    /// <summary>条「刚好完全进入屏幕」时中心所在 x（SkillUI 局部坐标）：敌人右缘贴右屏缘，玩家左缘贴左屏缘。</summary>
    float InScreenCenterX(bool isEnemy)
    {
        float rootHalf = RootHalfOrScreenFallback();
        float barHalf = TemplateBarHalf();
        float edgeHalf = Mathf.Max(0f, rootHalf - barHalf);
        float rootX = (isEnemy ? 1f : -1f) * edgeHalf;
        return rootX - SkillUiXInRoot();   // 换回 SkillUI 局部坐标
    }

    /// <summary>根画布半宽；取不到根画布时退回屏幕像素（与旧兜底口径一致）。</summary>
    float RootHalfOrScreenFallback()
    {
        var rootRt = RootRect();
        if (rootRt != null && rootRt.rect.width > 1f) return rootRt.rect.width * 0.5f;
        return Screen.width * 0.5f;
    }

    /// <summary>SkillUI 在根画布空间中的 x；取不到时按 0（画布中心系）处理。</summary>
    float SkillUiXInRoot()
    {
        var rootRt = RootRect();
        if (rootRt == null || _root == null) return 0f;
        return rootRt.InverseTransformPoint(_root.position).x;
    }

    /// <summary>条半宽（模板宽 / 2）；模板异常时按 0。</summary>
    float TemplateBarHalf()
    {
        return _template != null && _template.rect.width > 1f ? _template.rect.width * 0.5f : 0f;
    }

    /// <summary>根画布的 RectTransform（SkillUI 自带的嵌套 Canvas 之上那一层）。</summary>
    RectTransform RootRect()
    {
        var cv = GetComponentInParent<Canvas>();
        var rootCv = cv != null ? cv.rootCanvas : null;
        return rootCv != null ? rootCv.transform as RectTransform : null;
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
