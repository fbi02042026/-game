using System.Collections.Generic;

/// <summary>
/// 【2026-10-06 主人拍板】本局抽奖「新获得」标记。
///
/// <para>主人原话：「抽奖时给新获得的东西上加个新字，不管是佣兵技能还是装备，点击继续按钮后消失。」</para>
///
/// <b>两条铁律（谁都不许破）：</b>
/// <list type="bullet">
/// <item><b>打点只有一处</b>：<c>RunDraftDirector</c> 发奖成功之后（技能 / 佣兵 / 装备各按自己的 Kind）；
///       别处一律不许 <c>Mark</c>，免得「新」字冒在与抽奖无关的地方。</item>
/// <item><b>清除只有一处</b>：<c>BattleEntryDraftPanel.DoContinue()</c>（玩家点「继续」、
///       倒计时归零两条路都走它）调 <see cref="ClearAll"/>。</item>
/// </list>
///
/// <para>本类只存「哪些 id 是新的」这一份事实，<b>不碰任何 UI</b>；
/// 显示侧（技能槽 / 装备槽）自己来问 <see cref="Has"/>。</para>
/// </summary>
public static class NewLootMarks
{
    /// <summary>玩家技能 / 佣兵技能：key 用技能 id。主人点名「佣兵技能也要标」。</summary>
    public const string KindSkill = "skill";
    /// <summary>装备：key 用 <c>EquipInstance.templateId</c>。</summary>
    public const string KindEquip = "equip";

    static readonly HashSet<string> _keys = new HashSet<string>();

    /// <summary>当前有几个「新」标记（诊断用）。</summary>
    public static int Count => _keys.Count;

    /// <summary>发奖成功时打一个标记。id 为空视为无效，不打（也不报错 —— 空 id 显示不出「新」）。</summary>
    public static void Mark(string kind, string id)
    {
        if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id)) return;
        _keys.Add(Key(kind, id));
    }

    /// <summary>这个 id 是不是「本拍刚抽到的」—— 显示侧唯一判据。</summary>
    public static bool Has(string kind, string id)
    {
        if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id)) return false;
        return _keys.Contains(Key(kind, id));
    }

    /// <summary>「继续」的唯一出口调用它：玩家的「新」一次性消费完。</summary>
    public static void ClearAll() => _keys.Clear();

    static string Key(string kind, string id) => kind + ":" + id;
}
