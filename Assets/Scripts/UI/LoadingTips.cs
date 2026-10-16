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

        // ——【2026-10-11 主人拍板】玩法 / 系统向提示（12 条）——
        // 同战斗池：门槛全部 0、不剧透、只写**当前版本真实存在**的机制。
        // ⚠ 金币副本 / 扫荡这些入口目前是隐藏的，别写进去误导玩家。
        "体力不够就等一会，它会自动回。",
        "酒馆能招佣兵，招募卷轴打关卡有机会掉。",
        "天赋石点天赋，先把输出那条点起来。",
        // 【2026-10-11 主人拍板】原句「每日登录别忘了领，白给的石头。」——
        // 主人：「每日登录别忘了领」这句可以，但后面「白给的石头」看不懂，像这种的不要。
        // ⚠ 教训：loading 提示是给玩家看的，不许写内部黑话（"石头"=天赋石的简称）和
        //   网络梗（"白给"）。要写就写玩家在界面上**能认出来的词**。
        "每日登录别忘了领奖励。",
        "冒险日志能看进度和图鉴，缺啥一目了然。",
        "背包满了就整理一下，没用的装备能分解。",
        "装备主要靠抽奖出，关卡掉的是金币和材料。",
        "角色页能看属性和成长，加点前先看一眼。",
        "主线一共八章，通关一章才开下一章。",
        "难度分普通、困难、噩梦，量力而行。",
        "进度会自动存档，换设备也能接着打。",
        "同样的地图多打几次，掉落和钱都会更稳。",
    };

    static readonly int[] TownGate =
    {
        1, 0, 0, 0, 0, 0,
        1, 0, 0, 0, 1, 2,
        // 【2026-10-11】末尾 12 条玩法 / 系统向提示，门槛全部 0（与上面 TownTips 的追加段一一对应）
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    static readonly string[] BattleTips =
    {
        // 【2026-10-10 主人反馈「loading 还老是那一句」】
        // 前期（最高通关章节 0）原本只有下面前 3 条可见，反复进出战斗就在这 3 句里转。
        // 下面 8 条是**不剧透后续章节**的通用战斗句，门槛同为 0，把前期轮换池从 3 条撑到 11 条。
        "裂隙越深，怪物越不像「记录里的样子」。",
        "森林层：新人练手的地方……据说。",
        "地上的剑痕还很新。有人比你早到一步。",
        "刀要常擦。血干了就不好擦了。",
        "第一次进裂隙的人运气都特别好——前三次。",
        "盾在前面，你在后面。顺序错了会很疼。",
        "怪物的叫声越整齐，说明有人在指挥它们。",
        "别贪刀。活着才有下一刀。",
        "地上的骨头多半是装备，捡完记得看一眼。",
        "裂隙里的风是往里吹的。它在换气。",
        "队伍散了就找墙站着，背后挨刀最要命。",
        "亡灵在逃，不是在猎你——它们怕更深的地方。",
        "补给箱上的编号……艾丽娅？",
        "「引导至最深处」——这哪是调查任务。",
        "若失败则封锁入口，不再派遣救援。",
        "有些任务，从一开始就不是给活着的人准备的。",
        "祭品不祭品，我说了算。",
        "我们不是公会的礼物。我们是来砸场子的。",
        "裂隙意志在等一个足够强的灵魂。别成为下一个。",
        "公会不要的人，我们自己救。",

        // ——【2026-10-11 主人拍板】玩法 / 系统向提示（14 条）——
        // 主人：「不一定跟剧情有关，跟游戏系统玩法有关的都可以，把这个池子变大点」。
        // 全部门槛 0（第一章就能出）、不剧透、只写**当前版本真实存在**的机制（写错机制比写少更糟）。
        "清完一波怪再往前走，下一波才会刷出来。",
        "砍不到就换个车道，车道不对等于空砍。",
        "精英怪又硬又疼，先集火它压力小一半。",
        "打完记得撤离，金币才算真正落袋。",
        "局内拿到的装备带不出裂隙，放心用别省。",
        "抽卡价格每抽一次就涨一点，别一次砸完。",
        "佣兵倒下就少一份输出，血线低了先撤。",
        "远程怪站在后排点你，绕过去先切掉。",
        "技能有冷却，别一口气全甩完。",
        "蓝条空了就放不出技能，别光按不管蓝。",
        "打不过就降一档难度，先通关再谈收益。",
        "阵型别站太散，背后挨刀最疼。",
        "通关给的金币随关数往上走，越往后越宽裕。",
        "每关开局都发一笔启动金，留着抽好货。",
    };

    /// <summary>每条战斗文案解锁所需的「最高通关章节」。0 = 一开始就能出现。</summary>
    static readonly int[] BattleGate =
    {
        0, 0, 0,
        // 【2026-10-10 主人拍板】新增 8 条通用战斗句，门槛全部 0（第一章就能出，不剧透后续章节）
        0, 0, 0, 0, 0, 0, 0, 0,
        1,                 // 亡灵逃命：第 2 章（幽冥墓园）之后
        4,                 // 补给箱编号：第 5 章（海岛遗迹）之后
        4,                 // 任务简报：第 5 章之后
        5,                 // 不再派遣救援：第 6 章（巨岩深窟）之后
        4,                 // 不是给活人准备的：第 5 章之后
        6,                 // 第七次：第 7 章（赤焰炼狱）之后
        6,                 // 砸场子：第 7 章之后
        5,                 // 裂隙意志：第 6 章之后
        6,                 // 公会不要的人：第 7 章之后
        // 【2026-10-11】末尾 14 条玩法 / 系统向提示，门槛全部 0（与上面 BattleTips 的追加段一一对应）
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    /// <summary>
    /// 【2026-10-11 主人拍板】首次进城那一句**只出现一次**（跨会话记住）。
    /// 原来靠 static 变量记 —— 每次运行都会重置，于是「每次开游戏 → 进城镇」永远都是
    /// 「艾丽娅说过：等她回来……」那一句，主人反馈「loading 还老是那一句」（Editor.log
    /// 的 [LoadDiag] 里连着两次 target=Town tip 全是这一句，可复现）。
    /// 改成写 PlayerPrefs：看过一次就不再走这条分支，进城文案回到正常随机（杂记 45% + 城镇池）。
    /// </summary>
    const string PP_FIRST_TOWN_LINE_SHOWN = "LoadingFirstTownLineShown";

    static int _lastTown = -1;
    static int _lastBattle = -1;

    /// <summary>
    /// 「佣兵 / 世界杂记」混入概率：0 = 只走剧情，1 = 只走杂记。
    /// 2026-09-18 用户要求：加载文案不要枯燥，掺一些好玩的佣兵杂记与裂隙见闻。
    /// 剧情主线句仍占大头（带章节门槛，不剧透），杂记只负责调味。
    /// 【2026-10-10 主人反馈「loading 还老是那一句」】0.35 → 0.45：杂记池有 36 条、剧情池前期只有 11 条，
    /// 多往杂记那边倾斜一点，连续几次进战斗撞同一句的概率明显下降。
    /// </summary>
    const float MercNoteChance = 0.45f;

    public static string Pick(SceneLoadingCoordinator.LoadTarget target)
    {
        if (target != SceneLoadingCoordinator.LoadTarget.Battle)
        {
            // 第一次进城镇：不随机，直接给她留的那句话 —— 只一次，看过就记住（见 PP_FIRST_TOWN_LINE_SHOWN）
            // 本文件不引入 using UnityEngine（Random 也是全限定写的），这里同样全限定，免得动文件头。
            if (UnityEngine.PlayerPrefs.GetInt(PP_FIRST_TOWN_LINE_SHOWN, 0) == 0 && ClearedFar() == 0)
            {
                UnityEngine.PlayerPrefs.SetInt(PP_FIRST_TOWN_LINE_SHOWN, 1);
                UnityEngine.PlayerPrefs.Save();
                return FirstTownLine;
            }
            if (UnityEngine.Random.value < MercNoteChance)
                return LoadingFlavorText.Next();
            return PickGated(TownTips, TownGate, ref _lastTown);
        }
        if (UnityEngine.Random.value < MercNoteChance)
            return LoadingFlavorText.Next();
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
