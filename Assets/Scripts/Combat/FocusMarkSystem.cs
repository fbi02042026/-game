using UnityEngine;

/// <summary>
/// 玩家靠近敌人时施加集火标记；佣兵 AI 优先攻击带标记目标。
/// </summary>
public class FocusMarkSystem : MonoBehaviour
{
    public static FocusMarkSystem Instance { get; private set; }
    public static UnitBase MarkedEnemy => Instance != null ? Instance._marked : null;

    const float MarkRange = MercAiPolicy.FocusMarkRange;
    const float MarkHold = 2f;
    const float MarkLinger = 1f;

    UnitBase _marked;
    float _markUntil;
    bool _nearMarked;
    bool _toasted;
    Transform _iconRoot;
    TextMesh _iconLabel;

    public static FocusMarkSystem Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("FocusMarkSystem");
        DontDestroyOnLoad(go);
        return go.AddComponent<FocusMarkSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.isInBattle
            || !BattleManager.Instance.UnitsCanAct)
        {
            ClearMarkVisualOnly();
            return;
        }

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            ExpireMark();
            return;
        }

        UnitBase nearest = FindNearestEnemyToHero(hero);
        float now = Time.time;

        if (nearest != null)
        {
            float d = Mathf.Abs(UnitBase.GetCombatX(hero) - UnitBase.GetCombatX(nearest));
            if (d <= MarkRange)
            {
                if (_marked != nearest)
                {
                    _marked = nearest;
                    if (!_toasted)
                    {
                        _toasted = true;
                        UIManager.Instance?.ShowToast("集火！");
                    }
                }
                _nearMarked = true;
                _markUntil = now + MarkHold;
            }
            else if (_marked != null && _nearMarked)
            {
                _nearMarked = false;
                _markUntil = now + MarkLinger;
            }
        }
        else if (_nearMarked)
        {
            _nearMarked = false;
            _markUntil = now + MarkLinger;
        }

        if (_marked != null && (_marked.isDead || now > _markUntil))
            ExpireMark();

        UpdateMarkVisual();
    }

    static UnitBase FindNearestEnemyToHero(Hero hero)
    {
        var list = BattleManager.Instance?.monsters;
        if (list == null) return null;
        float hx = UnitBase.GetCombatX(hero);
        UnitBase best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.isDead) continue;
            float d = Mathf.Abs(hx - UnitBase.GetCombatX(e));
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }
        return best;
    }

    void ExpireMark()
    {
        _marked = null;
        _nearMarked = false;
        ClearMarkVisualOnly();
    }

    void ClearMarkVisualOnly()
    {
        if (_iconRoot != null)
            _iconRoot.gameObject.SetActive(false);
    }

    void UpdateMarkVisual()
    {
        if (_marked == null || _marked.isDead)
        {
            ClearMarkVisualOnly();
            return;
        }

        EnsureIcon();
        _iconRoot.gameObject.SetActive(true);
        Vector3 p = _marked.GetHitPosition();
        p.y += 0.55f;
        _iconRoot.position = p;
    }

    void EnsureIcon()
    {
        if (_iconRoot != null) return;
        var go = new GameObject("FocusMarkIcon");
        _iconRoot = go.transform;
        var tm = go.AddComponent<TextMesh>();
        _iconLabel = tm;
        tm.text = "集火";
        tm.characterSize = 0.08f;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(1f, 0.35f, 0.2f, 1f);
        var font = GameFonts.GetChinese();
        if (font != null)
        {
            tm.font = font;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && font.material != null)
                mr.sharedMaterial = font.material;
        }
        var r = go.GetComponent<MeshRenderer>();
        if (r != null)
            r.sortingOrder = GameConfig.SORT_UNIT + 20;
    }

    public void ResetForBattle()
    {
        _toasted = false;
        ExpireMark();
    }
}
