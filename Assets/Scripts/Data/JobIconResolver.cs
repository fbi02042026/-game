using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业图标的唯一解析入口。（2026-09-26 建立，对应 Docs/模块化与防复发方案_2026-09-26.md Phase 7.1 / 规则 R1「单一写入者」）
///
/// 建立原因：同一个「职业图标」概念过去散成三套资源 + 四个加载函数（分布在 MercHireSession、
/// PlayerJobDefs、BattleUI.WidgetFactory），主人已两次反馈「总和玩家职业icon搞混」。
/// 只要还有一处调旧入口，旧图就会被写回来 —— 这就是 bug 复发的机制。
///
/// 使用约定（R1）：
///   · 凡是「职业分类徽标」（战斗左下角角色栏、招募卡 Role 角标），一律只走 <see cref="CombatBadge"/>。
///   · 旧入口 MercHireSession.LoadMercJobBadge / LoadJobIcon / JobIconFile / MercJobBadgeFile
///     与 PlayerJobDefs.TryLoadCombatBadgeIcon 已全部转调到这里，并标 [Obsolete]，新代码不要再用。
///   · 玩家职业立绘头像（职业选择 / 三选一卡面的人像）是另一套，仍走 PlayerJobDefs.TryLoadJobIcon，
///     不在本类管辖范围，别拿它当分类 icon。
///   · 佣兵养成徽记（6 职业 × 三品质）是第三套，走 MercGrowSprites.LoadJobBadge，同样别混进来。
///
/// 规则 R2（禁止静默兜底）：取不到图时会打一条 Warning（同一路径只打一次），不再无声变空白。
/// </summary>
public static class JobIconResolver
{
    /// <summary>四分类徽标 Resources 主目录（主人指定的美术源：Assets/Art/UI/Icons/职业icon）。</summary>
    public const string BadgeRes = "Icons/职业icon";

    /// <summary>四分类图的现有 Resources 副本目录（与 Art 源同名同图），主目录缺资源时回退这里。</summary>
    const string BadgeResFallback = "Icons/Job";

    static readonly HashSet<string> _warnedPaths = new HashSet<string>();

    /// <summary>
    /// 职业分类徽标（四分类：物攻 / 法术 / 防御 / 恢复）。
    /// 传职业名（佣兵）或玩家职业显示名（如「重武者」）都可，内部换算到四分类。
    /// 取不到返回 null —— 调用方应隐藏节点，不要回退成职业立绘头像（那正是过去搞混的原因）。
    /// </summary>
    public static Sprite CombatBadge(string jobOrDisplayName)
    {
        string file = BadgeFile(jobOrDisplayName);
        if (string.IsNullOrEmpty(file)) return null;

        var sp = LoadSprite(BadgeRes + "/" + file);
        if (sp != null) return sp;

        sp = LoadSprite(BadgeResFallback + "/" + file);
        if (sp != null) return sp;

        WarnOnce(BadgeRes + "/" + file);
        return null;
    }

    /// <summary>职业名 → 四分类文件名。</summary>
    public static string BadgeFile(string jobOrDisplayName)
    {
        if (string.IsNullOrEmpty(jobOrDisplayName)) return "物攻";
        // 重武(重武者) 走物攻分支（2026-09-17 用户纠正：重武也是物攻，不是防御）
        if (jobOrDisplayName.Contains("盾") || jobOrDisplayName.Contains("卫") || jobOrDisplayName.Contains("防御"))
            return "防御";
        if (jobOrDisplayName.Contains("牧") || jobOrDisplayName.Contains("恢复") || jobOrDisplayName.Contains("圣"))
            return "恢复";
        if (jobOrDisplayName.Contains("法") || jobOrDisplayName.Contains("术") || jobOrDisplayName.Contains("水系")
            || jobOrDisplayName.Contains("雷系") || jobOrDisplayName.Contains("火系"))
            return "法术";
        return "物攻";
    }

    static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;

        var all = Resources.LoadAll<Sprite>(path);
        if (all != null && all.Length > 0) return all[0];

        // 兜底：从 Assets/Art 拷进 Resources 的 png 若被团结按「默认贴图」导入（meta 里
        // textureType=0 / spriteMode=0，spriteSheet 为空），Resources.Load<Sprite> 取不到，
        // 左下角职业 icon 就会空白。这里退一步读 Texture2D 现造 Sprite，不改 .meta 也能显示。
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    static void WarnOnce(string resPath)
    {
        if (!_warnedPaths.Add(resPath)) return;
        Debug.LogWarning("[JobIconResolver] 职业分类徽标取不到：" + resPath
            + " —— 按项目铁律应从 Assets/Art 复制同名图到该 Resources 路径（Art 目录不进包）。"
            + "此处不再静默回退成职业立绘头像，以免又和玩家职业 icon 搞混。");
    }
}
