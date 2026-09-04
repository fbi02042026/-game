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
        if (_bound == null || _bound.isDead) return;
        if (!_wantVisible || _killCamHidden) return;
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
            _wantVisible = false;
            ApplyVisibility();
            return;
        }

        _bound = pick;
        _wantVisible = true;
        ApplyVisibility();
        SyncRatioFromBound();
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
