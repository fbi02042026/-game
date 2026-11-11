using System.Text;
using UnityEngine;

/// <summary>
/// 怪物信息弹窗的文案唯一出口（2026-11-11 主人拍板「主要就是说明词条和弱点」）：
/// 冒险日志怪物 Tip（AdventureUI.ShowMonsterTip）与战斗内词条怪首次登场提示（AffixMonsterNoticeUI）
/// 共用同一份内容与排版，禁止各自再拼一份字符串。
///
/// 排布（段间空一行，标签行金色、说明行奶白，配合 MonsterTipPanel 的左对齐 + 自动高度）：
///   攻击 12 · 生命 45
///   攻击方式：近战
///
///   词条                      ← 金色标签
///   荆棘：近战攻击它会被反弹 15% 伤害
///
///   弱点 · 法术伤害            ← 「弱点 · 」金色，物/法字样分色
///   魔防 0 · 法系打它 +10%
///
/// 行长约束：MonsterTipPanel 正文净宽 398px、22 号字约 18 个汉字/行。
/// 最长的固定句「荆棘：被近战攻击反弹 15% 伤害」实测 297px，稳稳一行；
/// 新增文案前先量一下，别超过 390px，否则 Unity 会在空格处提前断行（=「一行没写完就换行」）。
///
/// 弱点口径（真源 = 战斗公式，不另编设定）：
///   怪物只分物理型 / 法术型，攻击与防御二选一（Monster.Init）：物理型物防有值、魔防 0；
///   法术型魔防有值、物防 0；DamageFormula 物理只扣 Defense、魔法只扣 MagicDefense，
///   且类型不同时额外 ×(1 + CrossTypeDamageBonus)。所以「魔法防御 0」的物理型就是怕法系，反之亦然。
///   普通小怪的类型由 monster_attack_style 表 style 决定（确定性）；精英/Boss 每场重掷，
///   弹窗如实写「每局随机」，不假装知道。
/// </summary>
public static class MonsterTipContent
{
    /// <summary>标签行色：报喜金（与默认弹窗标题同源）。</summary>
    static readonly string LabelHex = "#" + ColorUtility.ToHtmlStringRGB(RarityPalette.Gold);
    /// <summary>物理伤害强调色（与词条「暴击」橙同族）。</summary>
    static readonly string PhysicalHex = "#" + ColorUtility.ToHtmlStringRGB(new Color(1f, 0.68f, 0.42f));
    /// <summary>法术伤害强调色（与词条「铁壁」蓝同族）。</summary>
    static readonly string MagicHex = "#" + ColorUtility.ToHtmlStringRGB(new Color(0.62f, 0.78f, 1f));

    /// <summary>弹窗标题：取名，无名回退 id（不造名字）。</summary>
    public static string Title(MonsterConfig cfg)
    {
        if (cfg == null) return "";
        return string.IsNullOrEmpty(cfg.monsterName) ? cfg.id : cfg.monsterName;
    }

    /// <summary>
    /// 正文。<paramref name="gameChapter"/> 传**游戏章**（1~8，不是素材章）：
    /// 血量系数按章首算口径与图鉴一致，攻击方式表按 GameConfig.GetMonsterChapter 映射到素材章。
    /// </summary>
    public static string BuildBody(MonsterConfig cfg, int gameChapter)
    {
        if (cfg == null) return "";
        int monsterChapter = GameConfig.GetMonsterChapter(gameChapter);

        // —— 第一段：战力速览（生命按章首实战值，与 Monster.Init 的倍率链同源）——
        // 攻击/生命挤一行、攻击方式另起一行：两条语义不同，且 Boss 那行（BOSS · 攻击 22 · 生命 900）
        // 与攻击方式放一起会超过正文净宽 382px。
        float hpScale = GameConfig.GetStatScale(gameChapter, 0) * GameConfig.MONSTER_HP_GLOBAL_MUL;
        float shownHp = cfg.baseHp * hpScale;
        string styleName = StyleName(MonsterAttackStyleTable.Get(monsterChapter, cfg.spriteIndex));
        var sb = new StringBuilder();
        if (cfg.isBoss) sb.Append("BOSS · ");
        sb.Append("攻击 ").Append(cfg.baseAttack.ToString("0"));
        sb.Append(" · 生命 ").Append(shownHp.ToString("0"));
        // BOSS 的物理/法术类型每场重掷（命中法系会强制改法球弹道），表里的 style 不作数 →
        // 普通怪才写攻击方式，BOSS 的「随机」统一放在弱点段说一次，避免两段互相打脸。
        if (!cfg.isBoss) sb.Append("\n攻击方式：").Append(styleName);

        // —— 第二段：词条（表定词条怪才有；精英/Boss 的词缀是当局掷骰，不在 cfg 上）——
        var affixes = MonsterAffixDefs.ParseAffixList(cfg.tableAffix);
        if (affixes.Count > 0)
        {
            sb.Append("\n\n");
            sb.Append(Label("词条"));
            // 每个词条一行「名称：说明」（Describe 的唯一出口，多词条也读得清谁是谁）
            sb.Append('\n').Append(MonsterAffixDefs.Describe(affixes));
        }

        // —— 第三段：弱点 ——
        sb.Append("\n\n");
        bool magicType = MonsterAttackTypeResolver.IsMagicMonster(monsterChapter, cfg.spriteIndex);
        int bonus = Mathf.RoundToInt(DamageFormula.CrossTypeDamageBonus * 100f);
        if (cfg.isBoss)
        {
            // 精英/Boss 每场重掷类型（MonsterAttackTypeResolver 掷骰重载），图鉴无法预知 → 如实说明。
            // 第二行只留「打哪个」：22 号字下「类型每场重掷 · 物理型怕法系，法术型怕物理」要 430px，
            // 超过正文净宽 398 会断行；「每局随机」已说明重掷，不必重复。
            sb.Append(Label("弱点 · ")).Append(Label("每局随机"));
            sb.Append("\n物理型怕法系 · 法术型怕物理");
        }
        else if (magicType)
        {
            sb.Append(Label("弱点 · ")).Append(Tint(PhysicalHex, "物理伤害"));
            sb.Append("\n物防 0 · 物理打它 +").Append(bonus).Append("%");
        }
        else
        {
            sb.Append(Label("弱点 · ")).Append(Tint(MagicHex, "法术伤害"));
            sb.Append("\n魔防 0 · 法系打它 +").Append(bonus).Append("%");
        }
        return sb.ToString();
    }

    static string Label(string text) => Tint(LabelHex, text);

    static string Tint(string hex, string text) => "<color=" + hex + ">" + text + "</color>";

    /// <summary>攻击方式（近战 / 远程弓 / 远程法术）——图鉴里玩家最关心的就是这条决定弱点。</summary>
    static string StyleName(MonsterAttackStyle style)
    {
        switch (style)
        {
            case MonsterAttackStyle.Ranged: return "远程 · 法术";
            case MonsterAttackStyle.Bow: return "远程 · 弓";
            default: return "近战";
        }
    }
}
