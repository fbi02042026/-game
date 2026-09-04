using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗中佣兵偶尔头顶说一句（表「战斗中」）。引导战 / 暂停行动 / 剧情气泡占用时不播。
/// </summary>
public class MercBattleBanter : MonoBehaviour
{
    const float MinInterval = 12f;
    const float MaxInterval = 20f;

    float _nextAt = -1f;
    BattleManager _bm;

    public static void EnsureOn(BattleManager bm)
    {
        if (bm == null) return;
        var banter = bm.GetComponent<MercBattleBanter>();
        if (banter == null)
            banter = bm.gameObject.AddComponent<MercBattleBanter>();
        banter._bm = bm;
        banter.ArmNext();
    }

    void ArmNext()
    {
        _nextAt = Time.unscaledTime + Random.Range(MinInterval, MaxInterval);
    }

    void Update()
    {
        if (_bm == null) _bm = BattleManager.Instance;
        if (_bm == null || !_bm.isInBattle) return;
        if (_bm.IsTutorialRun || _bm.PartyIntroWalking || !_bm.UnitsCanAct) return;
        if (_nextAt < 0f) ArmNext();
        if (Time.unscaledTime < _nextAt) return;

        TrySpeak();
        ArmNext();
    }

    void TrySpeak()
    {
        if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing)
            return;

        var pool = CollectAliveMercs();
        if (pool.Count == 0) return;

        var merc = pool[Random.Range(0, pool.Count)];
        string key = !string.IsNullOrEmpty(merc.hireId) ? merc.hireId : merc.mercId;
        string line = MercLineTable.Pick(key, MercLineTable.Scene.Combat);
        if (string.IsNullOrEmpty(line)) return;

        BattleHeadTalkUI.Ensure().PlayLine(merc, line);
    }

    List<Mercenary> CollectAliveMercs()
    {
        var list = new List<Mercenary>();
        if (_bm == null || _bm.allyUnits == null) return list;
        for (int i = 0; i < _bm.allyUnits.Count; i++)
        {
            if (_bm.allyUnits[i] is Mercenary m && m != null && !m.isDead && !m.TutorialStunned)
                list.Add(m);
        }
        return list;
    }
}
