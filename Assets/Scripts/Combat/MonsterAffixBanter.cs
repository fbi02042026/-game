using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【2026-10-26 词缀怪】词缀怪吐槽气泡：
/// ① 词缀怪出场时玩家头顶说一句介绍（每种词缀每关只说一次）；
/// ② 荆棘反伤打到玩家时吐槽一句「为啥打他一下我疼一下」（每关一次）。
/// 台词全部走 C# 常量（纪律 11：叙事文案不入表）；调性按 2026-10-16 拍板的年轻人口径。
/// 气泡走 BattleHeadTalkUI（说话人 = 玩家本体）；气泡正在显示时不抢，宁可这次不说。
/// </summary>
public static class MonsterAffixBanter
{
    // ---- 台词常量（出场介绍，按词缀）。主人要求：短 + 有趣（2026-10-26）。
    // 气泡宽 260 / 字号 26 ≈ 一行 8 字，两句即收，所以全部压在一行半以内。
    const string LineBulwark = "我勒个豆，这怪也太肉了";
    const string LineThorns  = "浑身是刺，别贴脸硬刚！";
    const string LineCrit    = "小心那个红的，刀刀要害！";
    const string LineSwift   = "好家伙，跑得比外卖还快";
    const string LineFrenzy  = "攻速开挂了吧？退退退！";
    const string LineLeech   = "我打的血他自己吸回去了？";
    const string LineSplit   = "这货死了会下崽，先集火！";
    // 荆棘反伤吐槽（打他我掉血）
    const string LineThornsHurt = "打他一下我疼一下？离谱！";

    /// <summary>气泡停留（秒）。比默认 2.4 略短，别挡玩家太久。</summary>
    const float HoldSeconds = 2.0f;

    static readonly HashSet<MonsterAffixId> _saidIntro = new HashSet<MonsterAffixId>();
    static bool _saidThornsHurt;

    /// <summary>每关重置（WavePlanner.BuildCombatWaves 调）：每关每种词缀介绍一次、荆棘吐槽一次。</summary>
    public static void ResetForStage()
    {
        _saidIntro.Clear();
        _saidThornsHurt = false;
    }

    /// <summary>词缀怪刷出后调（CoSpawnWaveMonsters）：按首个词缀挑一句介绍。</summary>
    public static void OnAffixMonsterSpawned(Monster m)
    {
        if (m == null || m.isDead) return;
        var affix = MonsterAffix.Get(m);
        if (affix == null || affix.Count <= 0) return;

        // 只介绍第一个词缀（多词缀怪本来就少）
        MonsterAffixId first = affix.Count > 0 && affix.Has(MonsterAffixId.Bulwark) ? MonsterAffixId.Bulwark
            : affix.Has(MonsterAffixId.Thorns) ? MonsterAffixId.Thorns
            : affix.Has(MonsterAffixId.Crit) ? MonsterAffixId.Crit
            : affix.Has(MonsterAffixId.Leech) ? MonsterAffixId.Leech
            : affix.Has(MonsterAffixId.Split) ? MonsterAffixId.Split
            : affix.Has(MonsterAffixId.Frenzy) ? MonsterAffixId.Frenzy
            : MonsterAffixId.Swift;
        if (!_saidIntro.Add(first)) return;

        Speak(LineFor(first));
    }

    /// <summary>荆棘反伤命中玩家侧后调（Monster.TakeDamage 荆棘分支）。</summary>
    public static void OnThornsReflected(UnitBase attacker)
    {
        if (_saidThornsHurt) return;
        if (attacker == null || !attacker.isAlly) return; // 只在玩家/佣兵挨反伤时吐槽
        _saidThornsHurt = true;
        Speak(LineThornsHurt);
    }

    static string LineFor(MonsterAffixId id)
    {
        switch (id)
        {
            case MonsterAffixId.Bulwark: return LineBulwark;
            case MonsterAffixId.Thorns:  return LineThorns;
            case MonsterAffixId.Crit:    return LineCrit;
            case MonsterAffixId.Swift:   return LineSwift;
            case MonsterAffixId.Frenzy:  return LineFrenzy;
            case MonsterAffixId.Leech:   return LineLeech;
            case MonsterAffixId.Split:   return LineSplit;
            default: return "";
        }
    }

    static void Speak(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        // 气泡正在显示（剧情/引导/佣兵闲聊）不抢，这一句作废，等下一只同词缀怪
        if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing)
            return;

        var speaker = Hero.Instance;
        if (speaker == null || speaker.isDead) return;
        BattleHeadTalkUI.Ensure().PlayLine(speaker, line, HoldSeconds);
    }
}
