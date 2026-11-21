using UnityEngine;

/// <summary>
/// 战斗宠物头顶气泡台词（2026-11-21 用户拍板「宠物也会有战斗中的那个对话框，显示汪汪什么的，其他宠物也一样」）。
///
/// 复用唯一气泡出口 BattleHeadTalkUI.PlayLine(UnitBase, string)（说话人必须是 UnitBase，Pet 正好继承 Mercenary）。
/// 与佣兵闲聊 MercBattleBanter 靠 BattleHeadTalkUI.IsShowing 天然错峰：同一时刻全场只有一个气泡，不抢话。
/// 台词真源 = 本文件常量表（照 MonsterAffixBanter / ChapterBattleFlavor 现成范式，C# 常量不入 csv）；
/// 每只宠物各有自己的叫声，皮肤跟随其本体的物种叫声。
/// </summary>
public class PetBattleBanter : MonoBehaviour
{
    const float MinInterval = 18f;
    const float MaxInterval = 30f;

    float _nextAt = -1f;
    BattleManager _bm;

    public static void EnsureOn(BattleManager bm)
    {
        if (bm == null) return;
        var b = bm.GetComponent<PetBattleBanter>();
        if (b == null) b = bm.gameObject.AddComponent<PetBattleBanter>();
        b._bm = bm;
        b._nextAt = -1f;
    }

    void ArmNext()
    {
        _nextAt = Time.unscaledTime + Random.Range(MinInterval, MaxInterval);
    }

    void Update()
    {
        if (_bm == null) _bm = BattleManager.Instance;
        if (_bm == null || !_bm.isInBattle) return;
        if (_bm.PartyIntroWalking || !_bm.UnitsCanAct) return;
        if (_nextAt < 0f) ArmNext();
        if (Time.unscaledTime < _nextAt) return;

        TrySpeak();
        ArmNext();
    }

    void TrySpeak()
    {
        // 气泡唯一出口：有人正在说（佣兵闲聊/剧情/引导）就让位，不打断
        if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing) return;

        var pet = PickAlivePet();
        if (pet == null) return;
        string line = PickCry(pet.petDefId);
        if (string.IsNullOrEmpty(line)) return;

        BattleHeadTalkUI.Ensure().PlayLine(pet, line);
    }

    Pet PickAlivePet()
    {
        var bm = _bm != null ? _bm : BattleManager.Instance;
        if (bm == null || bm.allyUnits == null) return null;
        int count = 0;
        Pet pick = null;
        for (int i = 0; i < bm.allyUnits.Count; i++)
        {
            if (bm.allyUnits[i] is Pet p && p != null && !p.isDead)
            {
                count++;
                if (Random.value <= 1f / count) pick = p;   // 等概率水塘抽样
            }
        }
        return pick;
    }

    /// <summary>每只宠物各有叫声；皮肤按目录物种跟随本体（狐狸随狗、鹦鹉随鸟……）。</summary>
    static string PickCry(string petDefId)
    {
        if (string.IsNullOrEmpty(petDefId)) return null;
        string[] cries;
        if (petDefId.StartsWith("dog"))          cries = DogCries;
        else if (petDefId.StartsWith("toucan"))  cries = BirdCries;
        else if (petDefId.StartsWith("goat"))    cries = GoatCries;
        else if (petDefId.StartsWith("lizard"))  cries = LizardCries;
        else if (petDefId.StartsWith("wolf"))    cries = WolfCries;
        else return null;
        return cries[Random.Range(0, cries.Length)];
    }

    static readonly string[] DogCries = { "汪汪！", "汪呜——", "嗷汪！" };
    static readonly string[] BirdCries = { "嘎嘎！", "嘎——", "啾嘎！" };
    static readonly string[] GoatCries = { "咩——", "咩咩！", "嘞～" };
    static readonly string[] LizardCries = { "嘶嘶～", "嘶！", "咕噜噜～" };
    static readonly string[] WolfCries = { "嗷呜——", "嗷！", "呜嗷～" };
}
