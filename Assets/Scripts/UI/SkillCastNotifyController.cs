using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗内「技能释放通知」横条：我方（玩家/佣兵）从左侧飞入，敌人从右侧飞入。
/// 【2026-10-20 主人拍板：两根条分家】我方条根 = BattleUI/SkillUI（原模板），敌人条根 =
/// 主人手工加的 BattleUI/SkillUI1（skill/Icon/name 已在预制体里按镜像预摆好，
/// 字与图标各自反向翻回可读方向）——代码不再做任何镜像/翻面，克隆原样继承模板变换。
/// 两根条各自模板、各自对象池、互不干扰；模板直接取各自根下的 skill（含子节点 Icon / name），
/// 运行时 Instantiate + 池复用；预制体结构不改（运行时把模板隐藏）。
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

    /// <summary>起始 x 偏移（我方侧，模板锚点在左）。敌人侧取相反数（从右侧入屏）。</summary>
    const float StartOffsetX = -700f;

    /// <summary>多条堆叠时每往下一格的垂直间距。</summary>
    const float EntrySpacing = 80f;

    /// <summary>我方侧 / 敌人侧各自最多同时显示的条目数。</summary>
    const int MaxEntries = 5;

    /// <summary>finalPos.x 距屏幕边缘的安全边距，避开刘海 / 打孔（仅我方侧钳制用）。</summary>
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

    /// <summary>单侧上下文：一根条的根节点 / 模板 / 对象池 / 活动列表（SkillUI=我方，SkillUI1=敌人）。</summary>
    class SideContext
    {
        public bool isEnemy;
        public RectTransform root;
        public RectTransform template;
        public Image tplBg;
        public Image tplIcon;
        public Text tplLabel;
        public Vector2 tplFinalPos;   // 模板 anchoredPosition（该侧最终位置）
        public readonly List<NotifyEntry> entries = new List<NotifyEntry>();
        public readonly Queue<RectTransform> pool = new Queue<RectTransform>();
    }

    static SkillCastNotifyController _inst;

    /// <summary>BattleUI 存在但 SkillUI 缺失时置位，避免每帧刷同一条错误。</summary>
    static bool _bindFailed;

    SideContext _ally;    // SkillUI（我方条）
    SideContext _enemy;   // SkillUI1（敌人条，2026-10-20 主人手工加）

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
        var rootRt = transform as RectTransform;
        if (rootRt == null)
        {
            Debug.LogError("[SkillCastNotify] 控制器必须挂在 RectTransform 上（SkillUI），当前挂在 " + name);
            enabled = false;
            return;
        }

        // 【2026-10-18 二修】Canvas 已预置进 BattleUI 预制体（SkillUI 节点，overrideSorting=955）。
        // 旧写法运行时 AddComponent<Canvas> 在团结引擎下原生侧未建好 → 访问 overrideSorting 当场抛
        // MissingComponentException（Editor.log 122347 实锤），Awake 中断 → 模板永远绑不上 → 释放技能条静默失效。
        // 现在只取预制体画布设值（幂等），缺失就报错，绝不再 AddComponent。
        var liftCv = rootRt.GetComponent<Canvas>();
        if (liftCv == null)
        {
            Debug.LogError("[SkillCastNotify] SkillUI 节点缺少预置 Canvas（预制体应带 overrideSorting=955 的画布），通知条不抬层");
            enabled = false;
            return;
        }
        liftCv.overrideSorting = true;
        liftCv.sortingOrder = 955;

        _ally = BuildSide(rootRt, false);
        if (_ally == null)
        {
            enabled = false;
            return;
        }

        // 【2026-10-20 主人手工加】敌人条根 SkillUI1：与 SkillUI 同级；找不到就报错不做兜底
        //（主人摆的节点缺失属于结构问题，要让我看见）。
        var enemyTf = transform.parent != null ? transform.parent.Find("SkillUI1") : null;
        if (enemyTf == null)
        {
            Debug.LogError("[SkillCastNotify] BattleUI 下找不到 SkillUI1 节点（2026-10-20 主人手工加的敌人技能条根），敌人技能通知不可用");
        }
        else
        {
            _enemy = BuildSide(enemyTf as RectTransform, true);
        }
    }

    /// <summary>
    /// 绑定一根条的模板与子件（skill / Icon / name）。结构缺失返回 null：
    /// 我方侧缺失 → 整个控制器禁用；敌人侧缺失 → 只废敌人条（报错可见）。
    /// </summary>
    SideContext BuildSide(RectTransform root, bool isEnemy)
    {
        if (root == null) return null;
        var side = new SideContext { isEnemy = isEnemy, root = root };

        if (isEnemy)
        {
            // SkillUI1 画布预制体里已预置 overrideSorting=955；幂等设值，缺失就报错（与 SkillUI 同口径）
            var cv = root.GetComponent<Canvas>();
            if (cv == null)
            {
                Debug.LogError("[SkillCastNotify] SkillUI1 节点缺少预置 Canvas（预制体应带 overrideSorting=955 的画布），敌人通知条不抬层");
                return null;
            }
            cv.overrideSorting = true;
            cv.sortingOrder = 955;
        }

        var tplTf = root.Find("skill");
        if (tplTf == null)
        {
            Debug.LogError("[SkillCastNotify] " + root.name + " 下找不到模板节点 skill");
            return null;
        }

        side.template = tplTf as RectTransform;
        if (side.template == null)
        {
            Debug.LogError("[SkillCastNotify] " + root.name + "/skill 不是 RectTransform");
            return null;
        }

        side.tplBg = side.template.GetComponent<Image>();
        if (side.tplBg == null)
            Debug.LogError("[SkillCastNotify] " + root.name + "/skill 上找不到背景 Image");

        var iconTf = side.template.Find("Icon");
        if (iconTf == null)
            Debug.LogError("[SkillCastNotify] " + root.name + "/skill 下找不到子节点 Icon");
        else
        {
            side.tplIcon = iconTf.GetComponent<Image>();
            if (side.tplIcon == null)
                Debug.LogError("[SkillCastNotify] " + root.name + "/skill/Icon 上找不到 Image");
        }

        var nameTf = side.template.Find("name");
        if (nameTf == null)
            Debug.LogError("[SkillCastNotify] " + root.name + "/skill 下找不到子节点 name");
        else
        {
            side.tplLabel = nameTf.GetComponent<Text>();
            if (side.tplLabel == null)
                Debug.LogError("[SkillCastNotify] " + root.name + "/skill/name 上找不到 Text");
        }

        side.tplFinalPos = side.template.anchoredPosition;

        // 模板只作克隆源，运行时隐藏（敌人模板在预制体里是激活态，同样收掉，防静态残影）
        side.template.gameObject.SetActive(false);
        return side;
    }

    void Update()
    {
        // UI 不受暂停 / 时间缩放影响
        float dt = Time.unscaledDeltaTime;
        if (_ally != null) TickList(_ally, dt);
        if (_enemy != null) TickList(_enemy, dt);
    }

    void OnDestroy()
    {
        if (_ally != null)
        {
            _ally.entries.Clear();
            _ally.pool.Clear();
        }
        if (_enemy != null)
        {
            _enemy.entries.Clear();
            _enemy.pool.Clear();
        }
        if (_inst == this) _inst = null;
    }

    // ============================================================
    // 对外接口
    // ============================================================

    /// <summary>玩家技能释放通知（左侧）。
    /// 【2026-10-20 主人拍板：谁释放显示谁】头像放玩家头像（MercenaryManager.GetPlayerIcon），
    /// 拿不到才回退技能图标并告警（与 PushEnemySkill 同口径，不静默白框）。</summary>
    public static void PushPlayerSkill(string skillId, string skillName)
    {
        var c = Instance;
        if (c == null) return;
        var icon = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetPlayerIcon() : null;
        if (icon == null)
        {
            Debug.LogWarning($"[SkillCastNotify] 玩家技能条无头像：skillId={skillId}，回退技能图标");
            icon = BattleUI.LoadRunSkillIcon(skillId);
        }
        c.Push(icon, LabelOf(skillName, skillId), false);
    }

    /// <summary>佣兵技能释放通知（左侧）。
    /// 【2026-10-20 主人拍板：谁释放显示谁】头像放释放佣兵自己的头像（MercenaryManager.GetIcon，
    /// 内部已串 MercPortraitSprites.GetHead → CharacterRegistry → icon_命名 多级链路），
    /// 拿不到才回退技能图标并告警。</summary>
    public static void PushMercSkill(string mercId, string displayName, string skillId, string skillName)
    {
        var c = Instance;
        if (c == null) return;
        var icon = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetIcon(mercId) : null;
        if (icon == null)
        {
            Debug.LogWarning($"[SkillCastNotify] 佣兵技能条无头像：mercId={mercId} skillId={skillId}，回退技能图标");
            icon = MercSkillTable.LoadIcon(skillId);
        }
        c.Push(icon, LabelOf(skillName, skillId, displayName), false);
    }

    /// <summary>敌人技能释放通知（右侧，走 SkillUI1）。
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
        var side = isEnemy ? _enemy : _ally;
        if (side == null || side.template == null) return;   // 侧绑定失败已在 Awake LogError

        var list = side.entries;
        // 超过上限：先回收最老的一条（队尾）
        while (list.Count >= MaxEntries)
            RecycleAt(side, list.Count - 1);

        var rt = TakeFromPool(side);
        // lb 是 Text（不是 Image）：BindVisual 第 4 个 out 参数与 NotifyEntry.label 都是 Text。
        Image bg;
        Image ic;
        Text lb;
        if (!BindVisual(rt, out bg, out ic, out lb)) return;

        // 【2026-10-15 主人报「放技能头像没出现」根因】模板在 Awake 里 SetActive(false) 作克隆源，
        // Instantiate 会继承禁用状态；池里回收件也是禁用态 —— 不重新激活，横条永远不可见。
        rt.gameObject.SetActive(true);

        // 【2026-10-20 两根条分家】镜像/翻字全部由各自模板自带（SkillUI1 在预制体里已整体预镜像，
        // 字与图标已反向翻回可读方向），克隆原样继承，这里不再改任何 scale。

        // 【2026-10-18 修「飘字被屏幕左边缘裁切」】我方条终点统一在根画布空间按
        // 「画布半宽 − 安全边距 − 条半宽」钳制（SkillUI 不在画布正中）；
        // 【2026-10-20 主人拍板】敌人条终点 = SkillUI1 模板原位（主人手摆的镜像位），不镜像不钳制。
        float finalX = ResolveFinalX(side);
        float startX = finalX + (side.isEnemy ? -StartOffsetX : StartOffsetX);

        // 【2026-10-20 主人报「变慢的地方条一半还在屏外」】第一段终点动态前推：至少飞到
        // 「整条刚好完全进屏」的位置才开始变慢，之后慢移贴到最终位 → 进屏 → 变慢 → 停住 → 淡出。
        var midPos = new Vector2(ResolveMidX(side, startX, finalX), side.tplFinalPos.y);

        var entry = new NotifyEntry
        {
            rt = rt,
            bg = bg,
            icon = ic,
            label = lb,
            timer = 0f,
            startPos = new Vector2(startX, side.tplFinalPos.y),
            midPos = midPos,
            finalPos = new Vector2(finalX, side.tplFinalPos.y),
            isEnemy = isEnemy,
            displayY = side.tplFinalPos.y,
        };

        if (entry.icon != null)
        {
            entry.icon.sprite = icon;
            // 玩家/佣兵侧放释放者头像，敌人侧放怪物全身图（非正方形），统一保持比例不拉伸
            entry.icon.preserveAspect = true;
        }
        if (entry.label != null) entry.label.text = label ?? "";
        SetAlpha(entry, 1f);

        // 最新一条插到队首（占模板原位），旧条目 index 变大后平滑下移
        list.Insert(0, entry);
        entry.rt.anchoredPosition = entry.startPos;
    }

    void TickList(SideContext side, float dt)
    {
        var list = side.entries;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            e.timer += dt;
            if (e.timer >= LifeTime)
            {
                RecycleAt(side, i);
                continue;
            }

            // 堆叠目标 y 随 index 变化；旧条目平滑靠拢，避免新条插入时整列跳动
            float targetY = side.tplFinalPos.y - i * EntrySpacing;
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

    void RecycleAt(SideContext side, int index)
    {
        if (index < 0 || index >= side.entries.Count) return;
        var e = side.entries[index];
        side.entries.RemoveAt(index);

        // 回收前清空 sprite 与文字，避免下一条复用到池里时闪现旧内容
        if (e.icon != null) e.icon.sprite = null;
        if (e.label != null) e.label.text = "";

        if (e.rt != null)
        {
            e.rt.gameObject.SetActive(false);
            side.pool.Enqueue(e.rt);
        }
    }

    RectTransform TakeFromPool(SideContext side)
    {
        while (side.pool.Count > 0)
        {
            var rt = side.pool.Dequeue();
            if (rt != null) return rt;
        }

        var go = Instantiate(side.template.gameObject, side.root, false);
        go.name = "skill_notify";
        // 缩放不重置：克隆继承模板自带变换（我方模板正向，敌人模板预镜像的 -1 链）
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
    /// 求终点 x（条根局部坐标）：我方沿用「画布半宽 − SafeMargin − 条半宽」钳制；
    /// 【2026-10-20 主人拍板】敌人条根 SkillUI1 已由主人在预制体里摆好镜像位，
    /// 模板原位即终点——不做画布镜像、不做钳制，主人摆哪儿条停哪儿。
    /// </summary>
    float ResolveFinalX(SideContext side)
    {
        if (side.isEnemy) return side.tplFinalPos.x;

        float skillUiX = SkillUiXInRoot(side.root);
        float rootHalf = RootHalfOrScreenFallback(side.root);
        float barHalf = TemplateBarHalf(side.template);
        float lim = Mathf.Max(0f, rootHalf - SafeMargin - barHalf);

        float desiredRootX = skillUiX + side.tplFinalPos.x;
        float clampedRootX = Mathf.Clamp(desiredRootX, -lim, lim);
        return clampedRootX - skillUiX;   // 换回条根局部坐标
    }

    /// <summary>
    /// 求第一段（飞入）终点 x（条根局部坐标）【2026-10-20】：
    /// 取「原 75% 点」与「整条刚好完全进屏点」中更靠行程前方的一个 —— 保证变慢发生在整条进屏之后。
    /// </summary>
    float ResolveMidX(SideContext side, float startX, float finalX)
    {
        float span = Mathf.Abs(StartOffsetX);
        if (span < 1f) return finalX;
        float inScreenX = InScreenCenterX(side);
        float fullRatio = Mathf.Abs(startX - inScreenX) / span;
        float reach = Mathf.Clamp(Mathf.Max(FlyReachRatio, fullRatio), 0f, 1f);
        return startX + (finalX - startX) * reach;
    }

    /// <summary>条「刚好完全进入屏幕」时中心所在 x（条根局部坐标）：敌人右缘贴右屏缘，我方左缘贴左屏缘。</summary>
    float InScreenCenterX(SideContext side)
    {
        float rootHalf = RootHalfOrScreenFallback(side.root);
        float barHalf = TemplateBarHalf(side.template);
        float edgeHalf = Mathf.Max(0f, rootHalf - barHalf);
        float rootX = (side.isEnemy ? 1f : -1f) * edgeHalf;
        return rootX - SkillUiXInRoot(side.root);   // 换回条根局部坐标
    }

    /// <summary>根画布半宽；取不到根画布时退回屏幕像素（与旧兜底口径一致）。</summary>
    float RootHalfOrScreenFallback(RectTransform rootRt)
    {
        if (rootRt != null)
        {
            var cv = rootRt.GetComponentInParent<Canvas>();
            var rootCv = cv != null ? cv.rootCanvas : null;
            if (rootCv != null)
            {
                var rr = rootCv.transform as RectTransform;
                if (rr != null && rr.rect.width > 1f) return rr.rect.width * 0.5f;
            }
        }
        return Screen.width * 0.5f;
    }

    /// <summary>条根在根画布空间中的 x；取不到时按 0（画布中心系）处理。</summary>
    float SkillUiXInRoot(RectTransform rootRt)
    {
        if (rootRt == null) return 0f;
        var cv = rootRt.GetComponentInParent<Canvas>();
        var rootCv = cv != null ? cv.rootCanvas : null;
        if (rootCv == null) return 0f;
        return rootCv.transform.InverseTransformPoint(rootRt.position).x;
    }

    /// <summary>条半宽（模板宽 / 2）；模板异常时按 0。</summary>
    float TemplateBarHalf(RectTransform template)
    {
        return template != null && template.rect.width > 1f ? template.rect.width * 0.5f : 0f;
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
