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
        // 【2026-11-01 主人拍板「无限关卡不要出现剧情」】裂隙里不播佣兵闲聊气泡（含章节风味台词入口，
        // ChapterBattleFlavor 本就只认 Normal 模式，这里把个人台词一并关掉）
        if (_bm.IsEndlessRift) return;
        if (TutorialRules.Current.SkipMercBanter || _bm.PartyIntroWalking || !_bm.UnitsCanAct) return;
        if (_bm.IsOccupying) return;   // 【2026-11-24】狗营救演出占用战场期间禁佣兵闲聊，避免「公会样本瓶…」插话和剧情打架
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
            // 【2026-11-24】Pet 继承 Mercenary，会被 is Mercenary 误收进来；宠物没有 hireId/不在花名册，
            // 选中后 MercLineTable 兜底成字面「佣兵：上！」（主人报「佣兵还说自己是佣兵」）。闲聊池只收真佣兵。
            if (_bm.allyUnits[i] is Mercenary m && m != null && !m.isDead && !m.TutorialStunned && !(m is Pet))
                list.Add(m);
        }
        return list;
    }

    // ============================================================
    // 【2026-11-26 主人拍板】佣兵升级时说的那一句：几个通用短句，所有佣兵换着说。
    // 叙事文案不入表（同 ChapterBattleFlavor / MonsterAffixBanter 口径），改词只改这里；
    // 不按佣兵定制（那是 MercLineTable 的活，目前表里没有升级场景）。
    // 气泡挂在战斗头像上（BattleHeadTalkUI.PlayLineAt），不走世界坐标跟随。
    // ============================================================
    static readonly string[] LevelUpLines =
    {
        "嗯，这下更稳了。",
        "变强了！交给我吧。",
        "嘿嘿，感觉不错。",
        "再来几个，我还能升！",
        "这点提升，小菜一碟。",
        "多谢关照，接下来更卖力。",
        "力量涌上来了！",
        "好兆头，接着来。",
    };
    static int _lastLevelUpLine = -1;

    /// <summary>
    /// 升级时在对应头像上冒一句。与自动闲聊共用同一个气泡，正在说话时不抢
    ///（主人拍板过「两句话撞车」要禁掉，剧情 / 引导 / 上一句还在就跳过本句）。
    /// </summary>
    public static void SayLevelUpAt(Transform anchor)
    {
        if (anchor == null) return;
        var alive = BattleHeadTalkUI.Instance;
        if (alive != null && alive.IsShowing) return;

        int idx = Random.Range(0, LevelUpLines.Length);
        if (idx == _lastLevelUpLine)
            idx = (idx + 1) % LevelUpLines.Length;   // 「换着说」：相邻两次不重复同一句
        _lastLevelUpLine = idx;

        BattleHeadTalkUI.Ensure().PlayLineAt(anchor, LevelUpLines[idx]);
    }
}
