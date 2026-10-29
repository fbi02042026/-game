using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗中佣兵偶尔头顶说一句（表「战斗中」）。引导战 / 暂停行动 / 剧情气泡占用时不播。
/// </summary>
public class MercBattleBanter : MonoBehaviour
{
    const float MinInterval = 25f;
    const float MaxInterval = 40f;

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
        if (TutorialRules.Current.SkipMercBanter || _bm.PartyIntroWalking || !_bm.UnitsCanAct) return;
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
        // 【2026-10-29 主人拍板「战斗中的对话框也要有信息量，但不是重要的」】
        // 四成概率说章节风味台词（剧情钩子的回声，纯氛围），其余照旧说个人台词。
        string line = null;
        if (Random.value < 0.4f)
            line = ChapterBattleFlavor.PickForCurrentBattle(_bm);
        if (string.IsNullOrEmpty(line))
        {
            string key = !string.IsNullOrEmpty(merc.hireId) ? merc.hireId : merc.mercId;
            line = MercLineTable.Pick(key, MercLineTable.Scene.Combat);
        }
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
