/// <summary>
/// Loading 剧情提示：按进城镇 / 进战斗随机抽一条，文案源自《裂缝之刃》剧情设定。
/// </summary>
public static class LoadingTips
{
    static readonly string[] TownTips =
    {
        "小美说过：等她回来，就给你办转正仪式……",
        "咨询台小姐看你的眼神，总像在隐瞒什么。",
        "公会大厅灯火通明，像是什么都没发生过。",
        "见习徽章还在胸口发烫。今天也要活着回来。",
        "裂缝入口的风，比昨天更冷一点。",
        "有人说正式小队都去调查最大的那道裂缝了。",
        "打份工而已——你当初是这么想的。",
        "阿尔托那家伙出发前还在念骑士荣耀。",
        "格雷走前只丢下一句：别信表面的委托单。",
        "回到公会，先去看看有没有她的消息。",
        "任务板又刷出了一批「普通调查」。真的普通吗？",
        "公会会长笑得很慈祥。你忽然不太想看。",
    };

    static readonly string[] BattleTips =
    {
        "裂缝越深，怪物越不像「记录里的样子」。",
        "森林层：新人练手的地方……据说。",
        "地上的剑痕还很新。有人比你早到一步。",
        "亡灵在逃，不是在猎你——它们怕更深的地方。",
        "补给箱上的编号……小美？",
        "「引导至最深处」——这哪是调查任务。",
        "若失败则封锁入口，不再派遣救援。",
        "有些任务，从一开始就不是给活着的人准备的。",
        "祭品不祭品，我说了算。",
        "我们不是公会的礼物。我们是来砸场子的。",
        "裂缝意志在等一个足够强的灵魂。别成为下一个。",
        "公会不要的人，我们自己救。",
    };

    static int _lastTown = -1;
    static int _lastBattle = -1;

    public static string Pick(SceneLoadingCoordinator.LoadTarget target)
    {
        return target == SceneLoadingCoordinator.LoadTarget.Battle
            ? PickAvoidRepeat(BattleTips, ref _lastBattle)
            : PickAvoidRepeat(TownTips, ref _lastTown);
    }

    static string PickAvoidRepeat(string[] pool, ref int last)
    {
        if (pool == null || pool.Length == 0) return "加载中…";
        if (pool.Length == 1) return pool[0];
        int idx;
        do { idx = UnityEngine.Random.Range(0, pool.Length); }
        while (idx == last);
        last = idx;
        return pool[idx];
    }
}
