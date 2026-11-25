using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物数据表（2026-11-21 拍板：数据驱动单一真源）。
///
/// 5 本体 + 5 皮肤，每条 = 一个可选条目（皮肤是独立可上阵条目，换皮肤即换 id）。
/// 命名零 IP 挂靠（17:57 口径）：只取宠物自身外形/叫声/通用网络梗。
///
/// 资源全部按 Resources 路径加载（铁律 16：不依赖 prefab 的 sprite guid）：
///   头像 = Resources/UI/Icons/宠物/{avatar}（10 张：狗/狐狸/巨嘴鸟/鹦鹉/愤怒羊/蘑菇羊/角蜥/火蜥蜴/灰狼/恶魔狼）
///   技能 = Resources/UI/Icons/PetSkill/{skillIcon}（5 张）
///   场景展示 = Resources 外的现成 prefab，走 AssetDatabase 无关的运行时加载约定：
///     PetArt 下实例化 EnemyMonster 1/Prefabs 里的动物 prefab（见 PetArt.cs 的 Resources 桥接说明）
///
/// 字段口径：
///   qualityStar  = 宠物品质星级 1~4（决定战力高低；用户 18:35「宠物分星级从1到4」）
///   favorStar    = 亲密度星级 0~3（喂养 5 次加一颗，满 15 次）—— 存在存档，不在本表
///   power        = 界面展示战力（策划草稿，基准≈玩家 1/10；不改战斗数值）
///   unlock       = 解锁途径；解锁判定真源在 PetSave 层，本表只描述类型与数值
///   skinOf       = 皮肤指向的本体 id（皮肤只换外观/形态，品质战力跟随本体的取法在 PetUI.Detail）
/// </summary>
public static class PetDefs
{
    [Serializable]
    public class Def
    {
        public string id;
        public string name;
        /// <summary>本体 id；皮肤条目指向本体，本体为 null。</summary>
        public string skinOf;
        public string avatar;      // Resources/UI/Icons/宠物/ 下文件名（不含扩展名）
        public string prefabPath;  // 动物 prefab 的资源定位（PetArt 展示 + idle 用）
        /// <summary>品质星级：本体 1~4；皮肤 = 本体 +1（2026-11-25 主人「皮肤名字要按稀有度变化」，
        /// 最高到 5 星 = 史诗紫；星级点节点固定 4 颗，5 星时全亮）。</summary>
        public int qualityStar;
        public int hp, atk, def;   // 展示属性（策划草稿值）
        public int power;          // 界面展示战力
        public string skill;       // 唯一技能名
        public string skillIcon;   // Resources/UI/Icons/PetSkill/ 下文件名
        public string skillDesc;
        public UnlockType unlock;
        public int unlockValue;    // 金币价 / 章节号 / 钻石价 / 广告数，按 unlock 解释
        /// <summary>换皮后变远程形态（仅灰狼→恶魔狼）。</summary>
        public bool rangedAfterSkin;
    }

    public enum UnlockType
    {
        Default,     // 默认解锁（剧情预解锁）
        Gold,        // 金币购买
        Chapter,     // 通关章节解锁
        Diamond,     // 钻石购买（本体）
        AdWatch,     // 累计看广告 N 次
        SkinDiamond, // 皮肤：钻石购买
    }

    public const string AvatarPathPrefix = "UI/Icons/宠物/";
    public const string SkillIconPathPrefix = "UI/Icons/PetSkill/";
    public const int SKIN_DIAMOND_PRICE = 100;   // 皮肤统一 100 钻（16:2x 定，17:57 保留）
    // 亲密度 3 颗心，每颗 5 次喂养填满、共 15 次满（规格第五节）。
    // 喂养定价按进度分档：第 1 颗心阶段 300、第 2 颗 400、第 3 颗起 500（满心后继续喂也 500）。
    public const int FEED_STAR2 = 5;             // 喂满第 1 颗心 = 5 次，之后进入 400 档
    public const int FEED_STAR3 = 10;            // 喂满第 2 颗心 = 10 次，之后进入 500 档
    public const int FEED_STAR_MAX = 15;         // 3 颗心全满 = 15 次

    static readonly Def[] _all =
    {
        // ── 1 狗（默认，1星）→ 狐狸皮肤 ──
        new Def {
            id="dog", name="修勾", avatar="狗", prefabPath="Animal/Animal_Dog",
            qualityStar=1, hp=240, atk=18, def=10, power=260,
            skill="猛扑", skillIcon="猛扑", skillDesc="追残血补刀，少量物理伤害",
            unlock=UnlockType.Default, unlockValue=0,
        },
        new Def {
            id="dog_fox", name="狐大仙", skinOf="dog", avatar="狐狸", prefabPath="Animal/Animal_Fox",
            qualityStar=2, hp=288, atk=22, def=12, power=312,
            skill="猛扑", skillIcon="猛扑", skillDesc="追残血补刀，少量物理伤害",
            unlock=UnlockType.SkinDiamond, unlockValue=SKIN_DIAMOND_PRICE,
        },

        // ── 2 巨嘴鸟（金币，1星）→ 鹦鹉皮肤 ──
        new Def {
            id="toucan", name="钱嘎嘎", avatar="巨嘴鸟", prefabPath="Bird/Bird_Toucan",
            qualityStar=1, hp=180, atk=14, def=8, power=220,
            skill="拾金", skillIcon="拾金", skillDesc="击杀额外掉金币，伤害很低",
            unlock=UnlockType.Gold, unlockValue=1200,
        },
        new Def {
            id="toucan_parrot", name="复读机", skinOf="toucan", avatar="鹦鹉", prefabPath="Bird/Bird_Parrot",
            qualityStar=2, hp=216, atk=17, def=10, power=264,
            skill="拾金", skillIcon="拾金", skillDesc="击杀额外掉金币，伤害很低",
            unlock=UnlockType.SkinDiamond, unlockValue=SKIN_DIAMOND_PRICE,
        },

        // ── 3 愤怒羊（二章解锁，2星）→ 蘑菇羊皮肤 ──
        new Def {
            id="goat", name="顶梁柱", avatar="愤怒羊", prefabPath="Goat/Goat_Angry",
            qualityStar=2, hp=520, atk=12, def=24, power=380,
            skill="羊毛盾", skillIcon="羊毛盾", skillDesc="给队友小护盾，不做治疗",
            unlock=UnlockType.Chapter, unlockValue=2,
        },
        new Def {
            id="goat_mushroom", name="菇凉", skinOf="goat", avatar="蘑菇羊", prefabPath="Goat/Goat_Mushroom",
            qualityStar=3, hp=624, atk=14, def=29, power=456,
            skill="羊毛盾", skillIcon="羊毛盾", skillDesc="给队友小护盾，不做治疗",
            unlock=UnlockType.SkinDiamond, unlockValue=SKIN_DIAMOND_PRICE,
        },

        // ── 4 角蜥（钻石，4星最强）→ 火蜥蜴皮肤 ──
        new Def {
            id="lizard", name="焰角", avatar="角蜥", prefabPath="Lizard/Lizard_Horn",
            qualityStar=4, hp=340, atk=42, def=14, power=620,
            skill="火球术", skillIcon="火球术", skillDesc="火球小范围法伤",
            unlock=UnlockType.Diamond, unlockValue=600,
        },
        new Def {
            id="lizard_fire", name="小火炭", skinOf="lizard", avatar="火蜥蜴", prefabPath="Lizard/Lizard_Fire",
            qualityStar=5, hp=408, atk=50, def=17, power=744,
            skill="火球术", skillIcon="火球术", skillDesc="火球小范围法伤",
            unlock=UnlockType.SkinDiamond, unlockValue=SKIN_DIAMOND_PRICE,
        },

        // ── 5 灰狼（100 广告，3星第二强，本体近战）→ 恶魔狼皮肤（换皮变远程） ──
        new Def {
            id="wolf", name="嗷呜", avatar="灰狼", prefabPath="Wolf/Wolf_Gray",
            qualityStar=3, hp=400, atk=34, def=12, power=500,
            skill="裂爪", skillIcon="裂爪", skillDesc="近身连续挥爪，小爆发物伤",
            unlock=UnlockType.AdWatch, unlockValue=100,
        },
        new Def {
            id="wolf_demon", name="影嗷", skinOf="wolf", avatar="恶魔狼", prefabPath="Wolf/Wolf_Demon",
            qualityStar=4, hp=480, atk=41, def=14, power=600,
            skill="裂爪", skillIcon="裂爪", skillDesc="换皮后变远程暗影箭，同强度",
            rangedAfterSkin=true,
            unlock=UnlockType.SkinDiamond, unlockValue=SKIN_DIAMOND_PRICE,
        },
    };

    static readonly Dictionary<string, Def> _byId = BuildMap();
    static Dictionary<string, Def> BuildMap()
    {
        var m = new Dictionary<string, Def>(StringComparer.Ordinal);
        foreach (var d in _all) m[d.id] = d;
        return m;
    }

    /// <summary>列表展示顺序：本体 + 紧随其皮肤，共 10 条。</summary>
    public static IReadOnlyList<Def> All => _all;

    /// <summary>左侧列表只用本体（5 只），皮肤不单列——皮肤走右上角皮肤槽购买/切换。
    /// 主人 11-21 19:xx 明确：左侧只显示本体那几个，不要全部显示。</summary>
    public static IReadOnlyList<Def> Bodies
    {
        get
        {
            var list = new List<Def>(5);
            foreach (var d in _all) if (!IsSkin(d)) list.Add(d);
            return list;
        }
    }

    public static Def Get(string id)
    {
        Def d;
        return id != null && _byId.TryGetValue(id, out d) ? d : null;
    }

    /// <summary>某本体的皮肤条目（无皮肤返回 null）。</summary>
    public static Def SkinOf(string bodyId)
    {
        foreach (var d in _all)
            if (!string.IsNullOrEmpty(d.skinOf) && d.skinOf == bodyId) return d;
        return null;
    }

    public static bool IsSkin(Def d) => d != null && !string.IsNullOrEmpty(d.skinOf);

    public static Sprite LoadAvatar(string id)
    {
        var d = Get(id);
        return d == null ? null : Resources.Load<Sprite>(AvatarPathPrefix + d.avatar);
    }

    public static Sprite LoadSkillIcon(string id)
    {
        var d = Get(id);
        return d == null ? null : Resources.Load<Sprite>(SkillIconPathPrefix + d.skillIcon);
    }
}
