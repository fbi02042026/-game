using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景屏幕 Boss 血条：按名绑定 BossBar/血条，Sliced 左对齐按宽度比例扣血。
/// 显示对象：IsBossUnit，或任意精英（IsEliteWave）。
/// 2026-09-28 主人要求：精英也配上这条血条 —— 原来只在「精英关」才显示，普通关里的精英（如引导第 4 波）看不到。
/// 【2026-11-26 主人拍板】强盗三人组（Mercenary.IsBandit）按 Boss 出场，与 Boss/精英共用这条求和血条。
/// </summary>
public class BattleBossHpBar : MonoBehaviour
{
    static BattleBossHpBar _inst;

    Transform _root;
    RectTransform _fillRt;
    Image _fillImg;
    float _fullWidth;
    float _fullHeight;
    UnitBase _bound;   // 【2026-11-26】Boss/精英(Monster) + 强盗(Mercenary) 共用

    // 双层血条：上层红条先掉血，下层偏白条停顿后匀速追上，露出「刚扣掉的那段」。
    // 延迟层是「血条」的运行时克隆，几何继承美术摆放、不写死尺寸、不改预制体；上色方式见 EnsureDelayLayer。
    RectTransform _delayRt;
    float _targetRatio;      // 真实血量比例（= 红条当前值）
    float _delayRatio;       // 延迟层当前比例
    float _delayHold;        // 掉血后的停顿倒计时（秒）
    bool _delayCycleActive;  // 本轮延迟周期已启动（含停顿与追赶）：连续伤害不反复重启，避免延迟层冻住
    const float BOSS_HP_DELAY_HOLD = 0.6f;     // 停顿时长（0.35 太短，战斗里白段一闪而过看不出延迟）
    const float BOSS_HP_CHASE_SPEED = 0.35f;   // 追赶速度：每秒收拢的比例（1=整条）
    // 下层要「跟上层形状一样、只是白色」：上层贴图是红填充+金边斜角，UGUI tint 乘性染不白，
    // 也不许新增图片/换图/动 prefab → 运行时把同一张 sprite 的像素 RGB 洗成白、alpha 原样保留，
    // 生成白色 sprite 给延迟层，斜角/金边轮廓与红条逐像素一致。做法见 MakeWhiteSprite。
    static readonly Color BossHpDelayColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    // 【2026-10-18 主人拍板】绑定目标集：Boss + 场上全部存活精英，血条显示 ∑当前血/∑最大血；
    // 【2026-11-26】强盗三人组也进本集（按 Boss 出场）；_bound 只当名牌目标用（取血量上限最高的一只）
    readonly List<UnitBase> _boundList = new List<UnitBase>();
    readonly List<UnitBase> _pickScratch = new List<UnitBase>();
    // 【2026-10-18 主人拍板】最后一只死亡时血条先归零展示一小段再收起（旧表现是带着血整条消失）
    float _deathZeroHold;
    const float BOSS_DEATH_ZERO_HOLD = 0.8f;
    bool _killCamHidden;
    bool _wantVisible;
    // 【2026-11-23 主人拍板 A】狗营救演出期间屏幕血条不弹（只压显示、不丢目标），演出结束自动补显。
    // 唯一写入点 = BattleManager.CutsceneDirector 的演出启动 / 复位 / finally 三处。
    static bool _cutsceneSuppressed;
    UnitBase _lastLoggedPick;   // 【2026-10-15】排查日志去重用
    Transform _progressBar;
    Transform _questMap;       // 【2026-10-16 改】= QuestPanel（左上任务面板）；旧引用 "map" 是场景底图根节点，绝不能藏
    bool _chromeCached;
    bool _lastShowLogged;      // 【2026-10-16】显示诊断日志去重：由藏变显才 dump 一次运行时状态
    bool _canvasLifted;        // 【2026-10-16】抬层 Canvas 幂等标记

    // BOSS 出场血条入场演出状态（1.2 秒从 0 填充到满）
    bool _introPlaying;
    float _introT;
    float _introRatio;
    const float BOSS_INTRO_FILL_TIME = 1.2f;

    // 【2026-11-26 主人拍板「Boss 血条标出狂暴线，玩家能预期要变招」】
    // 运行时建一根细竖刻度，钉在血条 BOSS_ENRAGE_HP_RATIO 位置；血线跨过时闪一下。
    // 只绑一只 Boss 时显示（多目标求和血条的百分比不等于单 Boss 狂暴点，宁可不画也不误导）。
    RectTransform _enrageTickRt;
    Image _enrageTickImg;
    float _enrageTickFlash;              // >0 时高亮，逐帧衰减
    float _lastHp01ForEnrage = -1f;      // 上一帧血比例，用于检测"跨过狂暴线"
    static readonly Color EnrageTickColor = new Color(1f, 0.36f, 0.12f, 0.95f);
    static readonly Color EnrageTickFlashColor = new Color(1f, 1f, 0.85f, 1f);

    public static BattleBossHpBar Ensure(Transform searchRoot = null)
    {
        if (_inst == null)
        {
            var go = new GameObject("BattleBossHpBarDriver");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<BattleBossHpBar>();
        }
        if (_inst._root == null)
            _inst.BindUi(searchRoot);
        return _inst;
    }

    public static void RefreshFromField()
    {
        if (_inst == null)
        {
            var ui = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
            Ensure(ui);
        }
        // 【2026-10-15 排查日志】主人报血条仍不出现 —— 先让链路说话：
        // 看不到这行 = 出场路径根本没调进来（ eliteWave 旗标/调用点问题）；
        // 看到这行但没有「锁定目标」那行 = PickTarget 挑失败；都有还看不见 = 视觉被盖/位置问题。
        Debug.Log("[BattleBossHpBar] RefreshFromField 触发（精英/Boss 出场）");
        _inst?.Refresh();
    }

    public static void SetKillCamHidden(bool hidden)
    {
        if (_inst == null) return;
        _inst._killCamHidden = hidden;
        _inst.ApplyVisibility();
    }

    /// <summary>
    /// 演出期压制（2026-11-23 主人拍板 A）：狗营救演出期间精英/Boss 屏幕血条一律不弹，
    /// 只压显示、目标照常绑定；演完 suppressed=false 时走 Refresh 唯一出口重新挑目标并补显。
    /// </summary>
    public static void SetCutsceneSuppressed(bool suppressed)
    {
        if (_cutsceneSuppressed == suppressed) return;
        _cutsceneSuppressed = suppressed;
        if (_inst == null) return;          // 还没建实例：静态标志已记下，下次建实例后按它生效
        if (suppressed) _inst.ApplyVisibility();
        else _inst.Refresh();
    }

    /// <summary>
    /// BOSS 出场血条入场演出：1.2 秒内从 0 慢慢填充到满，同时显示 BossBar 并隐藏顶部 ProgressBar/QuestMap。
    /// 已在播放中则不重启动画（重进战斗/重复调用不打断）。
    /// </summary>
    public static void PlayBossIntro(UnitBase boss)
    {
        // 已在播放：不打断（重进战斗/重复调用安全）。注意本方法是 static，必须经 _inst 访问实例字段
        if (_inst != null && _inst._introPlaying && _inst._bound == boss) return;
        if (boss == null) return;
        Ensure();                             // 内部自动 BindUi
        if (_inst._root == null || _inst._fillRt == null) return; // 战斗 UI 还没建好，静默返回（不打日志）

        // 【2026-11-26】换 Boss：狂暴刻度的跨线基准从头计，别拿上一只的血比例误判"刚跨线"
        _inst._lastHp01ForEnrage = -1f;
        _inst._enrageTickFlash = 0f;
        _inst._bound = boss;
        _inst._deathZeroHold = 0f;
        _inst._boundList.Clear();
        _inst._boundList.Add(boss);
        _inst._wantVisible = true;
        _inst.ApplyVisibility();              // BossBar 显示、ProgressBar/QuestMap 隐藏
        _inst._introPlaying = true;
        _inst._introT = 0f;
        _inst._introRatio = 0f;
        _inst.SetRatio(0f, true);
        _inst.UpdateAffixText();
    }

    void BindUi(Transform searchRoot)
    {
        if (searchRoot == null && BattleUI.Instance != null)
            searchRoot = BattleUI.Instance.transform;
        if (searchRoot == null)
        {
            Debug.LogError("[BattleBossHpBar] 绑定失败：BattleUI 未就绪，找不到挂点（BossBar 血条不显示）");
            return;
        }

        _root = FindDeep(searchRoot, "BossBar");
        if (_root == null)
        {
            Debug.LogError("[BattleBossHpBar] 绑定失败：BattleUI 下找不到节点 BossBar（Boss/精英血条不显示）");
            return;
        }

        var fillT = FindDeep(_root, "血条");
        if (fillT == null)
        {
            Debug.LogError("[BattleBossHpBar] 绑定失败：BossBar 下找不到子节点 血条（Boss/精英血条不显示）");
            return;
        }

        _fillRt = fillT as RectTransform ?? fillT.GetComponent<RectTransform>();
        _fillImg = fillT.GetComponent<Image>();
        if (_fillRt == null)
        {
            Debug.LogError("[BattleBossHpBar] 绑定失败：血条 节点不是 RectTransform（Boss/精英血条不显示）");
            return;
        }

        EnsureLeftAligned(_fillRt);
        _fullWidth = Mathf.Max(1f, _fillRt.sizeDelta.x);
        _fullHeight = _fillRt.sizeDelta.y;
        EnsureDelayLayer();

        // 【2026-10-16 血条「显示了却看不见」根因】AutoGameInitializer 给 TopBar/ProgressBar/QuestPanel/
        // BackpackPanel 等顶部 HUD 挂了 overrideSorting=100 的嵌套 Canvas、map=SORT_MAPROOT，
        // 而血条只在根 Canvas（SORT_BATTLE_UI 低层）里 —— SetAsLastSibling 只在根层内有效，
        // 血条永远压不过这些 HUD（嵌套 Canvas 让 SetSiblingIndex 处理不了的深坑）。
        // → 给血条根挂 overrideSorting Canvas 抬到 BattleHud(950)。
        // 【2026-10-16 二修·血条「又不见了」根因（Editor.log 实锤）】这个抬层**不能在这里挂**：
        // BindUi 走 BattleUI.Awake，此刻 BossBar 常常整棵还没激活（预制体实例化时未激活），
        // 未激活对象上 AddComponent<Canvas> 的 native 侧没建好 → set_overrideSorting 抛
        // MissingComponentException（日志 4 次），BindUi 半路夭折 + BattleUI.Awake 后半段
        //（EnsureBattleControls / ViewportFit / 按钮绑定）全被跳过，血条退回 0 层被 HUD 盖住。
        // → 挪到 <see cref="EnsureOverheadCanvas"/>，在显示时（对象已激活）幂等补挂。
        _root.gameObject.SetActive(false);
        _wantVisible = false;
        CacheChrome(searchRoot);
    }

    /// <summary>
    /// 血条抬层 Canvas 的唯一出口：显示时（BossBar 已激活）幂等设 overrideSorting=BattleHud(950)。
    /// 【2026-10-18 二修】Canvas 已预置进 BattleUI 预制体（BossBar 节点，overrideSorting=950）。
    /// 旧写法运行时 AddComponent<Canvas> 在团结引擎下会产出「僵尸画布」（原生侧未建好，
    /// 不抛异常但整棵子树不渲染）—— 血条显示链路全通却看不见的元凶。现在只取预制体画布设值。
    /// </summary>
    void EnsureOverheadCanvas()
    {
        if (_canvasLifted || _root == null) return;
        if (!_root.gameObject.activeInHierarchy) return;
        var bossCanvas = _root.GetComponent<Canvas>();
        if (bossCanvas == null)
        {
            Debug.LogError("[BattleBossHpBar] BossBar 节点缺少预置 Canvas（预制体应带 overrideSorting=950 的画布），血条不抬层");
            return;
        }
        bossCanvas.overrideSorting = true;
        bossCanvas.sortingOrder = GameConfig.UiSort.BattleHud;
        _canvasLifted = true;
    }

    void CacheChrome(Transform searchRoot)
    {
        if (_chromeCached) return;
        Transform uiRoot = searchRoot;
        if (uiRoot == null && BattleUI.Instance != null)
            uiRoot = BattleUI.Instance.transform;
        if (uiRoot == null) return;
        _progressBar = FindDeep(uiRoot, "ProgressBar");
        // 【2026-10-16 主人报「血条出来了但背景没了，打完精英又出现」根因】旧改动把 FindDeep(uiRoot, "map")
        // 当成「map 那里的其他 UI」在血条显示时藏掉 —— 但 "map" 是战斗场景的底图/可行走区根节点
        //（与 BattleLaneBounds.MapNodeName 同一个节点，里面还挂着 walk），血条一出来整个背景就被藏了。
        // → map 一律不藏；要藏的「map 前面这些」= 左上任务面板 QuestPanel + 顶部进度条 ProgressBar。
        // 【2026-10-17 主人口径】TopBar（副本名字/难度/右侧资源条）不藏，血条出现时保持常显。
        _questMap = FindDeep(uiRoot, "QuestPanel");
        _chromeCached = _progressBar != null || _questMap != null;
    }

    static void EnsureLeftAligned(RectTransform rt)
    {
        float w = rt.sizeDelta.x;
        float h = rt.sizeDelta.y;
        // 【2026-10-17 主人报「血条左侧出来一截」根因】pivot→左缘的距离必须乘节点自身缩放：
        // 预制体里「血条」localScale=0.7，旧算法漏乘，anchoredPosition 多退了
        // (1-0.7)×半宽 ≈ 106 单位 → 红色填充从条框左端探出一截。
        float sx = Mathf.Abs(rt.localScale.x) < 0.0001f ? 1f : Mathf.Abs(rt.localScale.x);
        Vector2 pivot = rt.pivot;
        Vector2 pos = rt.anchoredPosition;
        float anchorXBefore = rt.anchorMin.x;

        // 左边缘在父本地坐标的落点：锚点参考线 + 锚点偏移 − pivot 到左缘距离（含缩放）
        float leftEdge = AnchorRefX(rt.parent as RectTransform, anchorXBefore)
                         + pos.x - pivot.x * w * sx;

        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
        rt.pivot = new Vector2(0f, pivot.y);

        // 锚点参考线若从 anchorXBefore 挪到 0，把这段差补回，保证左边缘纹丝不动
        float parentRefAfter = AnchorRefX(rt.parent as RectTransform, 0f);
        rt.anchoredPosition = new Vector2(leftEdge - parentRefAfter, pos.y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>点锚 anchorX 在父 rect（父本地坐标）上的参考线 x。</summary>
    static float AnchorRefX(RectTransform parent, float anchorX)
    {
        if (parent == null) return 0f;
        float xMin = -parent.pivot.x * parent.rect.width;
        float xMax = (1f - parent.pivot.x) * parent.rect.width;
        return Mathf.Lerp(xMin, xMax, anchorX);
    }

    void LateUpdate()
    {
        // 【2026-10-15 主人报「预制体里血条没隐藏，运行时却被藏了」】自愈：
        // 血条此刻是藏的、又不是击杀镜头在藏 → 重挑目标并重放可见性。
        // 覆盖「漏刷新 / 被别处 SetActive(false) / 绑定晚了」等一切静默失败，绝不无声赖着。
        // 击杀镜头藏血条期间（_killCamHidden）不抢，命中了再自然解藏。
        if (_root != null && !_root.gameObject.activeSelf && !_killCamHidden && !_cutsceneSuppressed)
            Refresh();

        // 【2026-10-18 主人拍板】死亡归零展示期：血条已归零，倒数结束再收起。
        // 击杀镜头藏血条期间暂停倒数，保证解藏后玩家仍能看到「空条」再消失。
        if (_deathZeroHold > 0f)
        {
            if (!_killCamHidden)
                _deathZeroHold -= Time.unscaledDeltaTime;
            if (_deathZeroHold <= 0f)
            {
                _deathZeroHold = 0f;
                _bound = null;
                _boundList.Clear();
                _wantVisible = false;
                ApplyVisibility();
            }
            return;
        }

        // 目标全没了/全死了：复位入场标志（死亡归零展示由 Refresh 在最后一次死亡时触发）
        if (!AnyBoundAlive())
        {
            _introPlaying = false;
            return;
        }
        if (!_wantVisible || _killCamHidden) return;

        // BOSS 出场入场动画：1.2 秒内从 0 填充到满，期间绝不回真实血量
        if (_introPlaying)
        {
            _introT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_introT / BOSS_INTRO_FILL_TIME);
            _introRatio = k;
            SetRatio(k, true);
            if (k >= 1f)
            {
                _introPlaying = false;
                // 不 return：继续走下方 SyncRatioFromBound 回归真实血量（满血，无跳变）
            }
            else
            {
                return; // 播放期间必须 return，避免 SyncRatioFromBound 覆盖动画值
            }
        }

        SyncRatioFromBound();
        TickDelayChase();
        TickEnrageTick(_targetRatio);
    }

    public void Refresh()
    {
        if (_root == null)
            BindUi(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        if (_root == null || _fillRt == null) return;

        PickTargets(_pickScratch);
        if (_pickScratch.Count == 0)
        {
            // 【2026-10-18 主人拍板】最后一只绑定的 Boss/精英刚死：血条先归零展示一小段再收起，
            // 不再「还挂着大半截血就整条消失」（观感＝还有血就显示死亡）。
            if (_wantVisible && _deathZeroHold <= 0f && _boundList.Count > 0 && !AnyBoundAlive())
            {
                _introPlaying = false;
                _deathZeroHold = BOSS_DEATH_ZERO_HOLD;
                SetRatio(0f, true);
                _lastLoggedPick = null;
                return;   // _bound/_boundList 先留着（名牌不闪没），由 LateUpdate 倒计时后收起
            }
            if (_deathZeroHold > 0f) return;   // 归零展示期中再次刷新且无新目标：保持现状等倒数结束
            _bound = null;
            _boundList.Clear();
            _introPlaying = false;   // 场上无 Boss/精英：复位入场标志，避免残留导致下次出场不播
            _wantVisible = false;
            ApplyVisibility();
            return;
        }

        _deathZeroHold = 0f;   // 有新目标（多精英打死一只后还剩别的）：取消归零展示，改绑续显
        _boundList.Clear();
        _boundList.AddRange(_pickScratch);
        _bound = PickNameTarget();   // 名牌取血量上限最高的一只
        _wantVisible = true;
        ApplyVisibility();
        // 【2026-10-15 排查日志】只在锁定目标变化时打一条，不刷屏
        if (_lastLoggedPick != _bound)
        {
            _lastLoggedPick = _bound;
            Debug.Log($"[BattleBossHpBar] 锁定目标 ×{_boundList.Count}（名牌={(_bound != null ? _bound.name : "null")}）→ 显示屏幕血条");
        }
        if (!_introPlaying)
            SyncRatioFromBound();   // 入场动画播放中不打真实值，避免开场瞬间被覆盖
        UpdateAffixText();
    }

    // ============================================================
    // V6：精英 / Boss 词缀后缀（「森林守卫·狂暴」）
    // ============================================================

    Text _affixText;
    Transform _nameBg;   // 【2026-10-17】名牌底板 Namebg（BossBar 下）：名字整块藏/显走这里

    void EnsureAffixText()
    {
        if (_affixText != null || _root == null) return;

        // 场景里若有名字节点就复用，否则运行时建一个（与 TutorialHintUI 同样的兜底套路）
        var existing = FindDeep(_root, "名字") ?? FindDeep(_root, "Name") ?? FindDeep(_root, "NameText");
        if (existing != null)
        {
            var t0 = existing.GetComponent<Text>();
            if (t0 != null)
            {
                _affixText = t0;
                _nameBg = FindDeep(_root, "Namebg");   // Namebg（底板）里找不到就为 null，不兜底
                return;
            }
        }

        var go = new GameObject("AffixText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(_root, false);
        var t = go.GetComponent<Text>();
        t.fontSize = 24;
        t.color = new Color(1f, 0.72f, 0.42f);
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;

        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(6f, -2f);
        rt.sizeDelta = new Vector2(560f, 34f);
        _affixText = t;
    }

    /// <summary>
    /// 名牌（2026-10-17 主人加的 Namebg/Name）：Name 常显当前锁定的 Boss/精英名字，
    /// 挂了词缀就拼在后缀后（「森林守卫·狂暴」）；名字取不到且无词缀时整块藏 Namebg，避免空底板悬着。
    /// </summary>
    void UpdateAffixText()
    {
        // 【2026-11-09 主人拍板】金币/徽记本显副本专属名（宝箱怪/金宝箱/宝箱王等），
        // 走 Monster.HudDisplayName 唯一出口；其余模式在出口内部原样回退模板怪物名。
        // 【2026-11-26】强盗是 Mercenary，没有 Monster.HudDisplayName / 词缀；按类型分支取名
        string name = "";
        var boundMonster = _bound as Monster;
        if (boundMonster != null) name = boundMonster.HudDisplayName() ?? "";
        else
        {
            var boundMerc = _bound as Mercenary;
            if (boundMerc != null) name = boundMerc.DisplayName ?? "";
        }
        var af = boundMonster != null ? MonsterAffix.Get(boundMonster) : null;
        string suffix = af != null ? af.Suffix : "";
        string full = name + suffix;

        if (string.IsNullOrEmpty(full))
        {
            if (_nameBg != null && _nameBg.gameObject.activeSelf)
                _nameBg.gameObject.SetActive(false);
            return;
        }

        EnsureAffixText();
        if (_affixText == null) return;

        if (_nameBg != null && !_nameBg.gameObject.activeSelf)
            _nameBg.gameObject.SetActive(true);
        if (!_affixText.gameObject.activeSelf) _affixText.gameObject.SetActive(true);
        _affixText.text = full;
    }

    void SyncRatioFromBound()
    {
        if (_fillRt == null) return;
        // 【2026-10-18 主人拍板】多个精英把血条数值加到一起：∑当前血 / ∑最大血
        float cur = 0f, max = 0f;
        bool deathDoorClamped = false;
        for (int i = 0; i < _boundList.Count; i++)
        {
            var m = _boundList[i];
            if (m == null || m.isDead || m.attr == null) continue;
            cur += m.currentHp;
            max += m.attr.GetAttr(AttrType.MaxHp);
            // 【2026-11-28】引导宿敌被死亡门钳到 1 点＝血条真到底，延迟层立刻贴齐：
            // 否则红条到底后白条还挂着一截，遗言看着像「血没空就说了」。
            if (m.holdAtDeathDoor && m.currentHp <= 1.5f) deathDoorClamped = true;
        }
        if (max <= 0f) return;
        SetRatio(Mathf.Clamp01(cur / max), deathDoorClamped);
    }

    /// <summary>
    /// 【2026-11-26】狂暴刻度：运行时建一根细竖条钉在血条 ratio=BOSS_ENRAGE_HP_RATIO 处。
    /// 挂到「血条」的父节点下（与红条同坐标系），几何全部取自 _fillRt 真值，不写死尺寸。
    /// </summary>
    void EnsureEnrageTick()
    {
        if (_enrageTickRt != null || _fillRt == null) return;
        var parent = _fillRt.parent;
        if (parent == null) return;

        var go = new GameObject("BossEnrageTick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.transform as RectTransform;
        rt.anchorMin = _fillRt.anchorMin;
        rt.anchorMax = _fillRt.anchorMax;
        rt.pivot = _fillRt.pivot;
        rt.localScale = _fillRt.localScale;
        var img = go.GetComponent<Image>();
        img.color = EnrageTickColor;
        img.raycastTarget = false;
        // 细竖条：宽 3px（父坐标乘自身 scale 前的值），高比血条略上下各探 20%，十字好认
        rt.sizeDelta = new Vector2(3f, _fullHeight * 1.4f);
        _enrageTickRt = rt;
        _enrageTickImg = img;
    }

    /// <summary>每帧刷新刻度的位置 / 颜色 / 显隐。跨过狂暴线时闪一下白光。</summary>
    void TickEnrageTick(float hp01)
    {
        // 门禁：只绑一只 Boss、且 Boss 狂暴机制开启时才画（多目标求和口径不一致，宁可不画）
        // 【2026-11-26】强盗不是真 Boss，不画狂暴刻度（多目标求和百分比也不等于单 Boss 狂暴点）
        var boundMonster = _bound as Monster;
        bool eligible = _boundList.Count == 1 && boundMonster != null && boundMonster.IsBossUnit
                        && GameConfig.BOSS_ENRAGE_ENABLED;
        if (!eligible || _fillRt == null)
        {
            if (_enrageTickRt != null && _enrageTickRt.gameObject.activeSelf)
                _enrageTickRt.gameObject.SetActive(false);
            _lastHp01ForEnrage = hp01;
            return;
        }

        EnsureEnrageTick();
        if (_enrageTickRt == null) return;
        if (!_enrageTickRt.gameObject.activeSelf) _enrageTickRt.gameObject.SetActive(true);

        float ratio = Mathf.Clamp01(GameConfig.BOSS_ENRAGE_HP_RATIO);
        // 位置 = 血条左缘 + 满宽 × 狂暴比例；_fillRt pivot=0 左对齐，anchoredPosition.x 即左缘偏移
        float sx = Mathf.Abs(_fillRt.localScale.x) < 0.0001f ? 1f : Mathf.Abs(_fillRt.localScale.x);
        float tickX = _fillRt.anchoredPosition.x + _fullWidth * ratio * sx;
        _enrageTickRt.anchoredPosition = new Vector2(tickX, _fillRt.anchoredPosition.y);
        _enrageTickRt.sizeDelta = new Vector2(3f / sx, _fullHeight * 1.4f);

        // 跨线检测：从线上方掉到线下方（含入场动画 snap 到 0 的情况）
        if (_lastHp01ForEnrage > ratio + 0.0001f && hp01 <= ratio + 0.0001f)
            _enrageTickFlash = 0.5f;
        _lastHp01ForEnrage = hp01;

        if (_enrageTickFlash > 0f)
            _enrageTickFlash = Mathf.Max(0f, _enrageTickFlash - Time.unscaledDeltaTime);
        if (_enrageTickImg != null)
            _enrageTickImg.color = Color.Lerp(EnrageTickColor, EnrageTickFlashColor,
                Mathf.Clamp01(_enrageTickFlash / 0.5f));
    }

    /// <summary>
    /// 扣血唯一出口。snapDelay=true 用于入场填充 / 死亡归零这类瞬时切换（两层同步）；
    /// 实时掉血走 false：只动红条，延迟层由 TickDelayChase 停顿后追上。
    /// </summary>
    void SetRatio(float hp01, bool snapDelay = false)
    {
        hp01 = Mathf.Clamp01(hp01);
        _fillRt.sizeDelta = new Vector2(_fullWidth * hp01, _fullHeight);
        if (hp01 < _targetRatio - 0.0001f && !_delayCycleActive)
        {
            // 从贴齐状态首次掉血才起停顿；本轮停顿/追赶中的后续伤害只降目标
            _delayHold = BOSS_HP_DELAY_HOLD;
            _delayCycleActive = true;
        }
        _targetRatio = hp01;
        if (snapDelay)
        {
            _delayRatio = hp01;
            _delayHold = 0f;
            _delayCycleActive = false;
            ApplyDelayWidth();
        }
    }

    /// <summary>建延迟层：克隆「血条」取美术摆好的几何，排到红条之前（先画＝下层），换成同形状纯白贴图。</summary>
    void EnsureDelayLayer()
    {
        _delayRt = null;
        _targetRatio = 1f;
        _delayRatio = 1f;
        _delayHold = 0f;
        _delayCycleActive = false;

        var go = Instantiate(_fillRt.gameObject, _fillRt.parent);
        go.name = "BossHpDelayFill";   // 不叫「血条」，避免 BattleSideHud 按名绑定时撞上
        var rt = go.transform as RectTransform;
        rt.SetSiblingIndex(_fillRt.GetSiblingIndex());   // 挤到红条前面，红条缩短时露出这一段
        // 几何逐项取自真值节点（不写死数值）：无论编辑器里怎么摆，延迟层都跟红条完全重合
        rt.anchorMin = _fillRt.anchorMin;
        rt.anchorMax = _fillRt.anchorMax;
        rt.pivot = _fillRt.pivot;
        rt.anchoredPosition = _fillRt.anchoredPosition;
        rt.localScale = _fillRt.localScale;
        rt.sizeDelta = new Vector2(_fullWidth, _fullHeight);
        var img = go.GetComponent<Image>();
        var white = MakeWhiteSprite(_fillImg != null ? _fillImg.sprite : null);
        if (white != null) img.sprite = white;
        else Debug.LogError("[BattleBossHpBar] 血条 sprite 为空，延迟层拿不到同形状白色贴图（不补图、不兜底）");
        img.color = BossHpDelayColor;
        img.raycastTarget = false;
        _delayRt = rt;
    }

    static Sprite _whiteSprite;
    static Sprite _whiteSpriteSrc;

    /// <summary>
    /// 把「血条」贴图洗成纯白：只改 RGB、alpha 原样保留 → 斜角/金边轮廓与红条逐像素一致，
    /// 只是颜色白。UGUI tint 是乘性的，红贴图叠任何色都染不白，所以必须换贴图本身。
    /// 贴图 isReadable=0 取不到像素 → 先 Blit 到 sRGB RT 再 ReadPixels 拿可读副本（项目既有做法）。
    /// 不新增图片资源、不换图、不动 prefab/meta；按源 sprite 缓存，避免重复 Blit。
    /// </summary>
    static Sprite MakeWhiteSprite(Sprite src)
    {
        if (src == null) return null;
        if (_whiteSprite != null && _whiteSpriteSrc == src) return _whiteSprite;
        var tex = src.texture;
        if (tex == null) return null;

        int w = tex.width, h = tex.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevActive = RenderTexture.active;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false, false);

            var px = copy.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;   // 透明背景跳过，否则斜角外会露出白方块
                px[i].r = 255; px[i].g = 255; px[i].b = 255;
            }
            copy.SetPixels32(px);
            copy.filterMode = FilterMode.Bilinear;
            copy.wrapMode = TextureWrapMode.Clamp;
            copy.Apply(false, true);

            // 【2026-11-28 主人截图「血条的白底形状不对」】原实现只继承 rect/pixelsPerUnit，
            // pivot 写死 0.5、border 走默认 0 —— 红条用源 sprite 的九宫格保住右端尖角，
            // 白条 border=0 在 Sliced（Image.m_Type:1）下退化成整图拉伸，尖角被拉平变直角。
            // 修法：rect / pivot / pixelsPerUnit / border 全部继承源 sprite，两层 Sliced 行为逐项一致。
            var made = Sprite.Create(copy, src.rect, src.pivot, src.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, src.border);
            _whiteSpriteSrc = src;
            _whiteSprite = made;
            return made;
        }
        finally
        {
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    void ApplyDelayWidth()
    {
        if (_delayRt != null)
            _delayRt.sizeDelta = new Vector2(_fullWidth * _delayRatio, _fullHeight);
    }

    /// <summary>延迟追赶：掉血后停顿 BOSS_HP_DELAY_HOLD 秒，再按固定速度向真实血量收拢。</summary>
    void TickDelayChase()
    {
        if (_delayRt == null) return;
        if (_delayRatio <= _targetRatio + 0.0001f)
        {
            // 满血 / 回血 / 换目标：延迟层立刻贴齐，不留上一场的残影
            if (!Mathf.Approximately(_delayRatio, _targetRatio))
            {
                _delayRatio = _targetRatio;
                ApplyDelayWidth();
            }
            _delayHold = 0f;
            _delayCycleActive = false;
            return;
        }
        if (_delayHold > 0f)
        {
            _delayHold -= Time.unscaledDeltaTime;
            return;
        }
        _delayRatio = Mathf.MoveTowards(_delayRatio, _targetRatio, BOSS_HP_CHASE_SPEED * Time.unscaledDeltaTime);
        ApplyDelayWidth();
    }

    void ApplyVisibility()
    {
        if (_root == null) return;
        bool showBoss = _wantVisible && !_killCamHidden && !_cutsceneSuppressed;
        if (_root.gameObject.activeSelf != showBoss)
            _root.gameObject.SetActive(showBoss);

        if (showBoss)
        {
            // 【2026-10-16 二修】显示时对象已激活，这里才是挂抬层 Canvas 的安全时机（幂等）
            EnsureOverheadCanvas();
            // 【2026-10-15】显示时抬到 Canvas 内容的最后：防被后画的兄弟节点（透明全屏图元等）盖住
            _root.SetAsLastSibling();
            // 【2026-10-16 排查日志】显示链路代码已证实跑通（日志有「锁定目标→显示屏幕血条」）但主人屏幕上看不见，
            // 静态排查（prefab 三图 guid / 父级 active / 几何位置 / 同层 Canvas 覆盖）全部通过 → 只剩运行时视觉层未知。
            // 由藏变显时 dump 一次运行时状态：父级链是否 active、四角世界坐标（在不在屏内）、缩放（有没有被压扁）、Canvas 模式。
            if (!_lastShowLogged)
            {
                _lastShowLogged = true;
                var rtShow = _root as RectTransform;
                if (rtShow != null)
                {
                    var corners = new Vector3[4];
                    rtShow.GetWorldCorners(corners);
                    var cv = rtShow.GetComponentInParent<Canvas>();
                    string mode = cv != null ? cv.renderMode.ToString() : "NoCanvas";
                    Debug.Log($"[BattleBossHpBar] 显示执行：activeInHierarchy={_root.gameObject.activeInHierarchy} " +
                              $"worldBL={corners[0]} worldTR={corners[2]} lossyScale={rtShow.lossyScale} " +
                              $"rect={rtShow.rect.size} canvas={mode}");
                }
            }
        }
        else
        {
            _lastShowLogged = false;   // 收起后，下次再显示重新 dump
            if (_enrageTickRt != null && _enrageTickRt.gameObject.activeSelf)
                _enrageTickRt.gameObject.SetActive(false);
            _lastHp01ForEnrage = -1f;
        }

        // Boss 血条出现时让出顶部：藏进度条与任务面板
        // 【2026-10-16 主人口令】引导战进度条整场要藏——BossHpBar 收起时不得把进度条重新打开。
        // 【2026-10-17 主人口径】TopBar（副本名字/难度/右侧资源条）不用藏，只有飘在 map 前面的
        // QuestPanel / ProgressBar 随血条联动。
        // 【2026-10-26 主人拍板】顶部进度条已整体去掉，血条收起不再点亮它。
        CacheChrome(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        SetChromeActive(_progressBar, false);
        // 【2026-10-18 主人拍板】任务面板与结算「击杀敌人」口径不一致已整体隐藏，血条联动不再把它点亮
        SetChromeActive(_questMap, false);
    }

    static void SetChromeActive(Transform t, bool active)
    {
        if (t == null) return;
        if (t.gameObject.activeSelf != active)
            t.gameObject.SetActive(active);
    }

    // ============================================================
    // 【2026-10-18 主人拍板】目标集挑选：Boss + 全部存活精英都绑进血条（血量求和）；
    // 名牌只显示血量上限最高的一只。旧版只绑「血最多的一只」，多精英时另外几只
    // 掉血/死亡血条纹丝不动，真 Boss 先死就出现「还有血就显示死亡」。
    // ============================================================

    static void PickTargets(List<UnitBase> result)
    {
        result.Clear();
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return;

        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var u = bm.monsters[i];
            if (u == null || u.isDead) continue;
            var m = u as Monster;
            if (m != null && (m.IsBossUnit || m.IsEliteWave)) { result.Add(u); continue; }
            // 【2026-11-26 主人拍板】强盗三人组按 Boss 出场，与 Boss/精英共用顶部求和血条
            var merc = u as Mercenary;
            if (merc != null && merc.IsBandit) result.Add(u);
        }
    }

    /// <summary>名牌目标：绑定集里血量上限最高的一只。</summary>
    UnitBase PickNameTarget()
    {
        UnitBase best = null;
        float bestMax = -1f;
        for (int i = 0; i < _boundList.Count; i++)
        {
            var m = _boundList[i];
            if (m == null || m.attr == null) continue;
            float mh = m.attr.GetAttr(AttrType.MaxHp);
            if (mh > bestMax)
            {
                bestMax = mh;
                best = m;
            }
        }
        return best;
    }

    bool AnyBoundAlive()
    {
        for (int i = 0; i < _boundList.Count; i++)
        {
            var m = _boundList[i];
            if (m != null && !m.isDead) return true;
        }
        return false;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
