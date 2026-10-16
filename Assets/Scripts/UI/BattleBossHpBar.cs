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
    bool _killCamHidden;
    bool _wantVisible;
    Monster _lastLoggedPick;   // 【2026-10-15】排查日志去重用
    Transform _progressBar;
    Transform _questMap;
    Transform _topBar;         // 【2026-10-16】顶栏也随血条联动隐藏（主人「map 那里的其他 UI」口径）
    bool _chromeCached;
    bool _lastShowLogged;      // 【2026-10-16】显示诊断日志去重：由藏变显才 dump 一次运行时状态

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
        // 血条永远压不过这些 HUD（嵌套 Canvas 让 SetSiblingIndex 失效的深坑）。
        // → 给血条根挂 overrideSorting Canvas 抬到 BattleHud(950)：战斗 HUD 之上、弹窗/Toast 之下。
        var bossCanvas = _root.GetComponent<Canvas>() ?? _root.gameObject.AddComponent<Canvas>();
        bossCanvas.overrideSorting = true;
        bossCanvas.sortingOrder = GameConfig.UiSort.BattleHud;

        _root.gameObject.SetActive(false);
        _wantVisible = false;
        CacheChrome(searchRoot);
    }

    void CacheChrome(Transform searchRoot)
    {
        if (_chromeCached) return;
        Transform uiRoot = searchRoot;
        if (uiRoot == null && BattleUI.Instance != null)
            uiRoot = BattleUI.Instance.transform;
        if (uiRoot == null) return;
        _progressBar = FindDeep(uiRoot, "ProgressBar");
        // 【2026-10-16 主人拍板「boss血条显示时把map那里的其他UI隐藏」】旧引用 "QuestMap" 在 prefab 里
        // 根本不存在（死引用，血条显示时 map 从来没被藏过）——顶部实际叫 map，另有 TopBar 顶栏一并联动。
        _questMap = FindDeep(uiRoot, "map");
        _topBar = FindDeep(uiRoot, "TopBar");
        _chromeCached = _progressBar != null || _questMap != null || _topBar != null;
    }

    static void EnsureLeftAligned(RectTransform rt)
    {
        float w = rt.sizeDelta.x;
        float h = rt.sizeDelta.y;
        Vector2 pivot = rt.pivot;
        Vector2 pos = rt.anchoredPosition;
        // 换左 pivot 时保持左边缘世界位置不变
        float leftX = pos.x - pivot.x * w;

        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
        rt.pivot = new Vector2(0f, pivot.y);
        rt.anchoredPosition = new Vector2(leftX, pos.y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void LateUpdate()
    {
        // 【2026-10-15 主人报「预制体里血条没隐藏，运行时却被藏了」】自愈：
        // 血条此刻是藏的、又不是击杀镜头在藏 → 重挑目标并重放可见性。
        // 覆盖「漏刷新 / 被别处 SetActive(false) / 绑定晚了」等一切静默失败，绝不无声赖着。
        // 击杀镜头藏血条期间（_killCamHidden）不抢，命中了再自然解藏。
        if (_root != null && !_root.gameObject.activeSelf && !_killCamHidden)
            Refresh();

        // 目标没了（BOSS 被秒 / 切场景）：复位入场标志，否则下次 BOSS 出场会因 _introPlaying 残留而不播
        if (_bound == null || _bound.isDead)
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

        Monster pick = PickTarget();
        if (pick == null)
        {
            _bound = null;
            _introPlaying = false;   // 场上无 Boss/精英：复位入场标志，避免残留导致下次出场不播
            _wantVisible = false;
            ApplyVisibility();
            return;
        }

        _bound = pick;
        _wantVisible = true;
        ApplyVisibility();
        // 【2026-10-15 排查日志】只在锁定目标变化时打一条，不刷屏
        if (_lastLoggedPick != pick)
        {
            _lastLoggedPick = pick;
            float pickMaxHp = pick.attr != null ? pick.attr.GetAttr(AttrType.MaxHp) : -1f;
            Debug.Log($"[BattleBossHpBar] 锁定目标 {pick.name} maxHp={pickMaxHp:0} → 显示屏幕血条");
        }
        if (!_introPlaying)
            SyncRatioFromBound();   // 入场动画播放中不打真实值，避免开场瞬间被覆盖
        UpdateAffixText();
    }

    // ============================================================
    // V6：精英 / Boss 词缀后缀（「森林守卫·狂暴」）
    // ============================================================

    Text _affixText;

    void EnsureAffixText()
    {
        if (_affixText != null || _root == null) return;

        // 场景里若有名字节点就复用，否则运行时建一个（与 TutorialHintUI 同样的兜底套路）
        var existing = FindDeep(_root, "名字") ?? FindDeep(_root, "Name") ?? FindDeep(_root, "NameText");
        if (existing != null)
        {
            var t0 = existing.GetComponent<Text>();
            if (t0 != null) { _affixText = t0; return; }
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

    /// <summary>只有挂了词缀的精英/Boss 才显示，普通目标保持原来的干净外观。</summary>
    void UpdateAffixText()
    {
        var af = _bound != null ? MonsterAffix.Get(_bound) : null;
        string suffix = af != null ? af.Suffix : "";
        if (string.IsNullOrEmpty(suffix))
        {
            if (_affixText != null && _affixText.gameObject.activeSelf)
                _affixText.gameObject.SetActive(false);
            return;
        }

        EnsureAffixText();
        if (_affixText == null) return;

        string name = _bound != null && _bound.config != null && !string.IsNullOrEmpty(_bound.config.monsterName)
            ? _bound.config.monsterName
            : "";
        if (!_affixText.gameObject.activeSelf) _affixText.gameObject.SetActive(true);
        _affixText.text = name + suffix;
    }

    void SyncRatioFromBound()
    {
        if (_bound == null || _bound.attr == null || _fillRt == null) return;
        float maxHp = _bound.attr.GetAttr(AttrType.MaxHp);
        float ratio = maxHp > 0f ? Mathf.Clamp01(_bound.currentHp / maxHp) : 0f;
        SetRatio(ratio);
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

        // Boss 血条出现时让出顶部：藏进度条与任务图
        // 【2026-10-16 主人口令】引导战进度条整场要藏——BossHpBar 收起时不得把进度条重新打开。
        CacheChrome(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        bool hideChrome = _wantVisible; // 只要在打 Boss/精英目标就藏，KillCam 期间也保持藏
        bool hideForTutorial = TutorialDirector.IsTutorialBattle;
        SetChromeActive(_progressBar, !hideChrome && !hideForTutorial);
        SetChromeActive(_questMap, !hideChrome);
        SetChromeActive(_topBar, !hideChrome);
    }

    static void SetChromeActive(Transform t, bool active)
    {
        if (t == null) return;
        if (t.gameObject.activeSelf != active)
            t.gameObject.SetActive(active);
    }

    static Monster PickTarget()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return null;

        Monster bestBoss = null;
        Monster bestElite = null;
        float bestBossHp = -1f;
        float bestEliteHp = -1f;

        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i] as Monster;
            if (m == null || m.isDead) continue;

            float maxHp = m.attr != null ? m.attr.GetAttr(AttrType.MaxHp) : 0f;
            if (m.IsBossUnit)
            {
                if (maxHp > bestBossHp)
                {
                    bestBossHp = maxHp;
                    bestBoss = m;
                }
            }
            // 2026-09-28 主人要求：精英也给血条（去掉「仅精英关」限制）——取血最多的那只精英
            else if (m.IsEliteWave)
            {
                if (maxHp > bestEliteHp)
                {
                    bestEliteHp = maxHp;
                    bestElite = m;
                }
            }
        }

        return bestBoss != null ? bestBoss : bestElite;
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
