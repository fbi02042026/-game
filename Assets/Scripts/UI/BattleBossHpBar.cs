using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景屏幕 Boss 血条：按名绑定 BossBar/血条，Sliced 左对齐按宽度比例扣血。
/// 显示对象：IsBossUnit，或任意精英（IsEliteWave）。
/// 2026-09-28 主人要求：精英也配上这条血条 —— 原来只在「精英关」才显示，普通关里的精英（如引导第 4 波）看不到。
/// </summary>
public class BattleBossHpBar : MonoBehaviour
{
    static BattleBossHpBar _inst;

    Transform _root;
    RectTransform _fillRt;
    Image _fillImg;
    float _fullWidth;
    float _fullHeight;
    Monster _bound;
    // 【2026-10-18 主人拍板】绑定目标集：Boss + 场上全部存活精英，血条显示 ∑当前血/∑最大血；
    // _bound 只当名牌目标用（取血量上限最高的一只）
    readonly List<Monster> _boundList = new List<Monster>();
    readonly List<Monster> _pickScratch = new List<Monster>();
    // 【2026-10-18 主人拍板】最后一只死亡时血条先归零展示一小段再收起（旧表现是带着血整条消失）
    float _deathZeroHold;
    const float BOSS_DEATH_ZERO_HOLD = 0.8f;
    bool _killCamHidden;
    bool _wantVisible;
    Monster _lastLoggedPick;   // 【2026-10-15】排查日志去重用
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
    /// BOSS 出场血条入场演出：1.2 秒内从 0 慢慢填充到满，同时显示 BossBar 并隐藏顶部 ProgressBar/QuestMap。
    /// 已在播放中则不重启动画（重进战斗/重复调用不打断）。
    /// </summary>
    public static void PlayBossIntro(Monster boss)
    {
        // 已在播放：不打断（重进战斗/重复调用安全）。注意本方法是 static，必须经 _inst 访问实例字段
        if (_inst != null && _inst._introPlaying && _inst._bound == boss) return;
        if (boss == null) return;
        Ensure();                             // 内部自动 BindUi
        if (_inst._root == null || _inst._fillRt == null) return; // 战斗 UI 还没建好，静默返回（不打日志）

        _inst._bound = boss;
        _inst._deathZeroHold = 0f;
        _inst._boundList.Clear();
        _inst._boundList.Add(boss);
        _inst._wantVisible = true;
        _inst.ApplyVisibility();              // BossBar 显示、ProgressBar/QuestMap 隐藏
        _inst._introPlaying = true;
        _inst._introT = 0f;
        _inst._introRatio = 0f;
        _inst.SetRatio(0f);
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
        if (_root != null && !_root.gameObject.activeSelf && !_killCamHidden)
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
            SetRatio(k);
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
                SetRatio(0f);
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
        string name = _bound != null && _bound.config != null && !string.IsNullOrEmpty(_bound.config.monsterName)
            ? _bound.config.monsterName
            : "";
        var af = _bound != null ? MonsterAffix.Get(_bound) : null;
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
        for (int i = 0; i < _boundList.Count; i++)
        {
            var m = _boundList[i];
            if (m == null || m.isDead || m.attr == null) continue;
            cur += m.currentHp;
            max += m.attr.GetAttr(AttrType.MaxHp);
        }
        if (max <= 0f) return;
        SetRatio(Mathf.Clamp01(cur / max));
    }

    void SetRatio(float hp01)
    {
        hp01 = Mathf.Clamp01(hp01);
        _fillRt.sizeDelta = new Vector2(_fullWidth * hp01, _fullHeight);
    }

    void ApplyVisibility()
    {
        if (_root == null) return;
        bool showBoss = _wantVisible && !_killCamHidden;
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

    static void PickTargets(List<Monster> result)
    {
        result.Clear();
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return;

        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i] as Monster;
            if (m == null || m.isDead) continue;
            if (m.IsBossUnit || m.IsEliteWave) result.Add(m);
        }
    }

    /// <summary>名牌目标：绑定集里血量上限最高的一只。</summary>
    Monster PickNameTarget()
    {
        Monster best = null;
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
