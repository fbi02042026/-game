using UnityEngine;

/// <summary>
/// 玩家靠近敌人时施加集火标记；佣兵 AI 优先攻击带标记目标。
/// 不显示 Toast / 头顶「集火」文案。
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
            return;

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
                    _marked = nearest;
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
    }

    public void ResetForBattle()
    {
        ExpireMark();
    }
}
