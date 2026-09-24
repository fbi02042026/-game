using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景屏幕 Boss 血条：按名绑定 BossBar/血条，Sliced 左对齐按宽度比例扣血。
/// 仅 IsBossUnit，或精英关 IsEliteWave 显示。
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
    Transform _progressBar;
    Transform _questMap;
    bool _chromeCached;

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
        if (_inst != null && _inst._introPlaying) return;
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
        if (searchRoot == null) return;

        _root = FindDeep(searchRoot, "BossBar");
        if (_root == null) return;

        var fillT = FindDeep(_root, "血条");
        if (fillT == null) return;

        _fillRt = fillT as RectTransform ?? fillT.GetComponent<RectTransform>();
        _fillImg = fillT.GetComponent<Image>();
        if (_fillRt == null) return;

        EnsureLeftAligned(_fillRt);
        _fullWidth = Mathf.Max(1f, _fillRt.sizeDelta.x);
        _fullHeight = _fillRt.sizeDelta.y;

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
        _questMap = FindDeep(uiRoot, "QuestMap");
        _chromeCached = _progressBar != null || _questMap != null;
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

        // Boss 血条出现时让出顶部：藏进度条与任务图
        CacheChrome(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        bool hideChrome = _wantVisible; // 只要在打 Boss/精英目标就藏，KillCam 期间也保持藏
        SetChromeActive(_progressBar, !hideChrome);
        SetChromeActive(_questMap, !hideChrome);
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

        bool eliteStage = bm.currentStage != null && bm.currentStage.type == StageType.Elite;
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
            else if (eliteStage && m.IsEliteWave)
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
