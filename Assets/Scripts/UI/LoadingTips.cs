/// <summary>
/// Loading 剧情提示：按进城镇 / 进战斗随机抽一条，文案源自《裂隙之刃》剧情设定。
/// </summary>
/// <remarks>
/// 叙事 V2.0 修改：原先所有文案从第 1 场战斗起无条件随机，导致第 4–8 章的转折句
/// （如「若失败则封锁入口，不再派遣救援。」）在新手阶段就提前剧透。
/// 现在每条带一个「最低通关章节」门槛，未到进度的不进随机池。
/// </remarks>
public static class LoadingTips
{
    /// <summary>首次进城保证显示的那一句——把「我要去找她」的动机钉死在开局。</summary>
    public const string FirstTownLine = "艾丽娅说过：等她回来，就给你办转正仪式……";

    static readonly string[] TownTips =
    {
        "艾丽娅说过：等她回来，就给你办转正仪式……",
        "咨询台小姐看你的眼神，总像在隐瞒什么。",
        "公会大厅灯火通明，像是什么都没发生过。",
        "见习徽章还在胸口发烫。今天也要活着回来。",
        "裂隙入口的风，比昨天更冷一点。",
        "有人说正式小队都去调查最大的那道裂隙了。",
        "打份工而已——你当初是这么想的。",
        "阿尔托那家伙出发前还在念骑士荣耀。",
        "格雷走前只丢下一句：别信表面的委托单。",
        "回到公会，先去看看有没有她的消息。",
        "任务板又刷出了一批「普通调查」。真的普通吗？",
        "公会会长笑得很慈祥。你忽然不太想看。",
    };

    static readonly int[] TownGate =
    {
        1, 0, 0, 0, 0, 0,
        1, 0, 0, 0, 1, 2,
    };

    static readonly string[] BattleTips =
    {
        "裂隙越深，怪物越不像「记录里的样子」。",
        "森林层：新人练手的地方……据说。",
        "地上的剑痕还很新。有人比你早到一步。",
        "亡灵在逃，不是在猎你——它们怕更深的地方。",
        "补给箱上的编号……艾丽娅？",
        "「引导至最深处」——这哪是调查任务。",
        "若失败则封锁入口，不再派遣救援。",
        "有些任务，从一开始就不是给活着的人准备的。",
        "祭品不祭品，我说了算。",
        "我们不是公会的礼物。我们是来砸场子的。",
        "裂隙意志在等一个足够强的灵魂。别成为下一个。",
        "公会不要的人，我们自己救。",
    };

    /// <summary>每条战斗文案解锁所需的「最高通关章节」。0 = 一开始就能出现。</summary>
    static readonly int[] BattleGate =
    {
        0, 0, 0,
        1,                 // 亡灵逃命：第 2 章（幽冥墓园）之后
        4,                 // 补给箱编号：第 5 章（海岛遗迹）之后
        4,                 // 任务简报：第 5 章之后
        5,                 // 不再派遣救援：第 6 章（巨岩深窟）之后
        4,                 // 不是给活人准备的：第 5 章之后
        6,                 // 第七次：第 7 章（赤焰炼狱）之后
        6,                 // 砸场子：第 7 章之后
        5,                 // 裂隙意志：第 6 章之后
        6,                 // 公会不要的人：第 7 章之后
    };

    static int _lastTown = -1;
    static int _lastBattle = -1;
    static bool _firstTownShown;

    public static string Pick(SceneLoadingCoordinator.LoadTarget target)
    {
        if (target != SceneLoadingCoordinator.LoadTarget.Battle)
        {
            // 第一次进城镇：不随机，直接给她留的那句话
            if (!_firstTownShown && ClearedFar() == 0)
            {
                _firstTownShown = true;
                return FirstTownLine;
            }
            return PickGated(TownTips, TownGate, ref _lastTown);
        }
        return PickGated(BattleTips, BattleGate, ref _lastBattle);
    }

    static int ClearedFar()
    {
        var d = SaveSystem.Instance?.Data;
        return d == null ? 0 : TavernLore.HighestClearedChapter(d);
    }

    static string PickGated(string[] pool, int[] gate, ref int last)
    {
        if (pool == null || pool.Length == 0) return "加载中…";
        int far = ClearedFar();

        // 先收集本阶段可见的下标
        var cand = new System.Collections.Generic.List<int>();
        for (int i = 0; i < pool.Length; i++)
        {
            int g = gate != null && i < gate.Length ? gate[i] : 0;
            if (far >= g) cand.Add(i);
        }
        if (cand.Count == 0) return "加载中…";
        if (cand.Count == 1)
        {
            last = cand[0];
            return pool[last];
        }

        int pick = UnityEngine.Random.Range(0, cand.Count);
        if (cand[pick] == last) pick = (pick + 1) % cand.Count;
        last = cand[pick];
        return pool[last];
    }
}
