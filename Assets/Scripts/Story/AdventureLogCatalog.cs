/// <summary>
/// 冒险日志 V1.0 图鉴文案与解锁判定（暮影森林）。
/// 数据来自《冒险日志数据设计》；未单独落盘遭遇表时，用存档进度近似解锁。
/// </summary>
public static class AdventureLogCatalog
{
    public struct MonsterEntry
    {
        public string Id;
        public string Name;
        public string AssetId;
        public string Kind;
        public string Place;
        public string Unlock;
        public string Desc;
        public string Lore;
        public bool LaterChapter;
    }

    public struct MercEntry
    {
        public string Id;
        public string Name;
        public string AssetId;
        public string Role;
        public string Unlock;
        public string Place;
        public string Desc;
        public string Lore;
        public bool StoryNpc;
    }

    public struct AchEntry
    {
        public string Id;
        public string Name;
        public string Category;
        public string Unlock;
        public string Reward;
        public string Desc;
    }

    public struct WorldEntry
    {
        public string Id;
        public string Name;
        public string Category;
        public string Unlock;
        public string Desc;
        public string Flavor;
    }

    public struct StoryEntry
    {
        public string Id;
        public string Title;
        public string Unlock;
        public string Summary;
        public string Extra;
    }

    public static readonly MonsterEntry[] Monsters =
    {
        M("M001", "走路菇", "forest_401", "普通", "暮影森林·普通关", "首次遭遇",
            "黄色小蘑菇，靠两根短腿一蹦一跳移动。据说它自己也不知道为什么要追人。",
            "老猎人格雷说：‘这玩意儿烤了能吃，但吃了会看见会长在跳舞。’没人敢验证。"),
        M("M002", "毒伞菇", "forest_402", "普通", "暮影森林·普通关", "首次击败",
            "绿色蘑菇，伞盖上长了斑点。走路不稳，但脾气很稳——一直很坏。",
            "公会食堂曾把它当蔬菜收过，结果当天全员请假。咨询台小姐至今不解释。"),
        M("M003", "红浆果怪", "forest_403", "普通", "暮影森林·普通关", "首次遭遇",
            "一丛长眼睛的红色浆果，滚起来像颗愤怒的水果。",
            "冒险者之间打赌：谁吃它一口，谁下顿饭请客。目前没人赢过。"),
        M("M004", "森林史莱姆", "forest_404", "普通", "暮影森林·普通关", "首次击败",
            "绿色半透明胶状物，撞到人会发出“咕叽”一声。",
            "它的身体 90% 是水分，剩下 10% 是对你的不满。"),
        M("M005", "硬壳甲虫", "forest_405", "普通", "暮影森林·普通关", "首次击败",
            "橙色大甲虫，背甲硬到能当平底锅用。",
            "营地传说：用它的壳煎蛋，蛋会自己站起来逃跑。"),
        M("M006", "刺毛兽", "forest_406", "精英", "暮影森林·精英关", "首次遭遇",
            "白色刺猬状野兽，背上长满尖刺，生气时会炸毛。",
            "别摸。上次有人摸了一下，三天后还在从手套里拔刺。"),
        M("M007", "赤甲蟹", "forest_407", "精英", "暮影森林·精英关", "首次击败",
            "红色甲壳生物，两只钳子一大一小，看起来不太协调。",
            "它总把大钳子当锤子用，小钳子当牙签用。没人知道它从哪学来的。"),
        M("M008", "冰晶虫", "forest_408", "普通", "暮影森林·普通关", "首次遭遇",
            "蓝色结晶小虫，体内封存着一丝寒气。",
            "森林层明明不冷，但它走到哪都自带空调。老盾很爱站在它旁边乘凉。"),
        M("M009", "食人花苞", "forest_409", "普通", "暮影森林·普通关", "首次击败",
            "绿色食肉植物，嘴巴一直张着，像在等人喂它。",
            "它其实不是吃人，只是嘴巴合不上。但你要把手伸进去，它也乐意配合。"),
        M("M010", "岩块傀儡", "forest_410", "精英", "暮影森林·精英关", "首次击败",
            "由碎石和苔藓拼成的人形怪物，动作缓慢但血量惊人。",
            "传闻它是某个迷路石匠的杰作。石匠后来成了它的晚饭。"),
        M("M011", "巨型食人花", "forest_411", "首领", "暮影森林·首领关", "首次击败",
            "大型食肉植物，花瓣边缘呈锯齿状，会喷吐酸液。",
            "森林里的怪物都叫它‘大姐头’，虽然没人确认过它的性别。"),
        M("M012", "獠牙野猪王", "forest_412", "首领", "暮影森林·首领关", "首次击败",
            "红色巨野猪，獠牙泛着红光，冲锋能把小树撞断。",
            "它之所以红，不是因为愤怒，是因为小时候掉进了染缸。至少老盾是这么说的。"),
        Later("M101", "骷髅兵", "undead_101", "普通", "亡者墓地", "生前是公会会计，死后还在数金币。你打死它，它掉的是铜板。"),
        Later("M102", "僵尸农夫", "undead_102", "普通", "亡者墓地", "总在问‘我的锄头呢’，但其实它早把锄头当武器抡你了。"),
        Later("M103", "幽灵会计", "undead_103", "精英", "亡者墓地", "比骷髅兵高一级，会 floating 也会算账。死后掉的账本比装备还多。"),
        Later("M104", "骨龙幼崽", "undead_112", "首领", "亡者墓地深处", "它觉得自己很可怕，直到一只猫对着它打了个哈欠。"),
        Later("M201", "跳跳蛙", "jungle_201", "普通", "雨林遗迹", "跳得比见习冒险者跑得还快，但总是跳进自己同伴怀里。"),
        Later("M202", "毒蛇藤", "jungle_211", "精英", "雨林遗迹", "一种会模仿绳索的植物。很多新手把它当绳子爬过，结局都不太体面。"),
        Later("M301", "灯笼水母", "sea_305", "普通", "海岛沉船", "发光是为了吸引猎物，但常常吸引来的是另一只灯笼水母。"),
        Later("M302", "铁甲蟹", "sea_303", "精英", "海岛沉船", "壳硬到能挡箭，但翻过来就再也翻不回去。这是它的秘密。"),
        Later("M401", "史莱姆王", "field_502", "首领", "风车平原", "普通史莱姆的梦想是变大，它的梦想是学会减肥。"),
        Later("M501", "水晶蜘蛛", "cave_608", "精英", "深岩洞穴", "腿是水晶做的，跑起来像风铃。老盾说听着像公会食堂开饭铃。"),
        Later("M601", "小火魔", "devil_704", "普通", "熔岩核心", "脾气和体温一样高，但 cooling 方式是哭。别让它哭，会更热。"),
        Later("M602", "深渊大眼", "devil_711", "首领", "熔岩核心", "它看你的时候，你也在看它。建议不要对视超过三秒。"),
        Later("M701", "雪球怪", "ice_801", "普通", "永冻雪原", "越滚越大，但滚到火堆旁边会主动停下喝口汤。"),
        Later("M702", "冰晶狼", "ice_805", "精英", "永冻雪原", "奔跑时会打滑，精英怪里摔倒次数最多的一位。"),
    };

    public static readonly MercEntry[] Mercs =
    {
        Npc("C001", "小美", "npc_xiaomei", "青梅竹马 / 失踪小队队长",
            "绿色衣服、棕色双马尾的少女，总是把队友放在第一位。",
            "她离开前借了你三枚铜板买面包，至今没还。公会没把这个写进失踪报告。"),
        Npc("C002", "阿尔托", "npc_shengdian", "圣殿骑士",
            "金发、红缨头盔、持剑盾的年轻骑士，把荣耀挂在嘴边。",
            "他的盔甲擦得比公会的地板还亮。老盾怀疑他晚上穿着盔甲睡觉。"),
        Npc("C003", "格雷", "", "老猎人",
            "头发花白、披着旧斗篷的老练猎人，小美小队成员。",
            "据说他闻一口空气就能判断附近有没有精英怪。也能判断食堂今天有没有肉。"),
        Npc("C004", "独眼", "npc_duyan", "酒馆情报商",
            "戴眼罩的中年男子，常在酒馆角落低声交换情报。",
            "他知道公会很多事，但只换酒，不换金币。咨询台小姐看见他会假装没看见。"),
        Hire("H001", "老盾", "dunbing101", "剑盾卫士", "新手引导自动入队", "暮影森林·教学关",
            "手持大盾、全身重甲的中年佣兵，被玩家从怪物包围中救出。擅长挡刀和吐槽。",
            "他的盾牌上刻着前任主人的名字。他说是自己，只是字磨花了。"),
        Hire("H002", "铁皮", "dunbing102", "剑盾卫士", "通关第一章后开放招募", "酒馆",
            "比老盾年轻一点的盾兵，盔甲更亮，但实战经验更少。",
            "老盾总说他‘盾举得太高，挡住自己的视线’。铁皮不服，直到撞上一棵树。"),
        Hire("H003", "小红", "gongshou101", "游侠", "通关第一章后开放招募", "酒馆",
            "粉发弓箭手，动作轻快，箭无虚发——至少她是这么说的。",
            "她自称‘百步穿杨’，但有一次射中了老盾的盾牌。老盾安慰她：‘也算命中。’"),
        Hire("H004", "鹰眼", "gongshou201", "游侠", "通关第一章困难难度后开放招募", "酒馆",
            "戴兜帽的沉稳弓箭手，话少箭多，据说以前做过边境巡林人。",
            "他从不笑。有人打赌他笑一次请全队喝酒，目前欠账已经够买一把弓。"),
        Hire("H005", "大锤", "kuangzhan101", "狂战士", "通关第一章后开放招募", "酒馆",
            "手持巨型战斧的红角战士，攻击范围大，脾气和斧子一样直。",
            "他每次挥斧都会喊招式名。虽然招式名都是他自己编的，但气势很足。"),
        Hire("H006", "碎岩", "kuangzhan201", "狂战士", "通关第一章困难难度后开放招募", "酒馆",
            "使用双斧的矿石工人，退休后转职佣兵，对石头有执念。",
            "他判断敌人强不强，先看对方长得像不像矿石。首领怪在他眼里都是‘宝石’。"),
        Hire("H007", "小白", "naima101", "牧师", "通关第一章后开放招募", "酒馆",
            "持法杖的白衣治疗者，新手冒险者最喜欢的队友类型。",
            "她的治疗术是公会同级考试第一名。缺点是念咒时不能被打断，否则会奶到怪物。"),
        Hire("H008", "紫晶", "naima201", "元素法师", "通关第一章噩梦难度后开放招募", "酒馆",
            "紫发法师，能召唤小型闪电，表情总是很不耐烦。",
            "她觉得见习冒险者问题太多。但如果你请她喝酒，她能把你想知道的全倒出来。"),
    };

    public static readonly AchEntry[] Achievements =
    {
        A("A001", "见习冒险者", "成长", "完成新手引导", "金币 ×100", "你拿到了见习徽章，虽然它看起来随时会掉色。"),
        A("A002", "第一次撤离", "战斗", "首次从裂缝中撤离", "金币 ×50", "活着才有收益，公会这句话倒不是骗人的。"),
        A("A003", "第一次阵亡", "战斗", "首次在裂缝中死亡", "强化石 ×5", "金币没了，但装备和材料还在。记住这个教训。"),
        A("A004", "森林清道夫", "战斗", "在暮影森林累计击败 100 只怪物", "金币 ×200", "森林层的怪物看见你都会绕路。"),
        A("A005", "精英猎手", "战斗", "首次击败精英怪物", "金币 ×100", "精英怪不掉好装备，但它们掉的装备比普通怪好一点。"),
        A("A006", "首杀首领", "战斗", "首次击败森之守护者", "天赋石 ×3", "你砍倒了第一章的守门人。但门后面还有更多。"),
        A("A007", "装备收藏家", "收集", "累计拾取 50 件装备", "背包扩容 +1", "你的背包开始发出金属碰撞的声音。"),
        A("A008", "强化入门", "养成", "首次强化装备", "强化石 ×10", "把强化石砸进装备里，是冒险者最朴素的仪式感。"),
        A("A009", "酒馆常客", "养成", "首次在酒馆招募佣兵", "金币 ×150", "一个人下本太危险，带个能挡刀的。"),
        A("A010", "裂缝探索者", "探索", "累计进入裂缝 10 次", "体力上限 +1", "你下裂缝的次数已经比回公会大厅还多了。"),
        A("A011", "金币过万", "经济", "单局携带金币达到 10000", "金币 ×500", "有钱人的烦恼是：到底要不要撤离？"),
        A("A012", "完美首通", "挑战", "无伤通关第一章普通难度", "称号「森林无伤者」", "没有怪物能碰到你，包括那只树灵。"),
        A("A013", "困难挑战者", "挑战", "通关第一章困难难度", "天赋石 ×5", "困难难度的怪物不会更聪明，只会更不讲理。"),
        A("A014", "噩梦先驱", "挑战", "通关第一章噩梦难度", "限定头像框", "能活着走出噩梦难度的人，公会会记住你的名字。"),
        A("A015", "老盾的伙伴", "社交", "老盾累计参战 20 次", "老盾专属皮肤「锈迹盾卫」", "他说他的盾跟你一样旧了，但还能用。"),
    };

    public static readonly WorldEntry[] World =
    {
        W("W001", "埃索斯大陆", "世界观", "首次进入游戏",
            "玩家所在的奇幻大陆。古语中“Aes-oth”意为“从伤口呼吸的土地”。",
            "世界不是平的，是裂开的。"),
        W("W002", "裂缝", "世界观", "首次进入战斗",
            "连接异世界的空间裂口，越往深处越危险。",
            "裂缝不是门，是伤口。每次有人跳进去，伤口就痒一下。"),
        W("W003", "冒险者公会", "组织", "进入城镇后解锁",
            "表面上保护王国，实际上高层知晓裂缝真相并牺牲优秀冒险者以延缓裂缝扩张。",
            "公会大厅灯火通明，像是什么都没发生过。"),
        W("W004", "暮影森林", "地点", "进入第一章后解锁",
            "新人练手的第一处裂缝层，森林因裂缝能量而扭曲。",
            "这里的树会动，只是动作很慢，慢到你以为只是风吹。"),
        W("W005", "仙泉", "地点", "首次进入恢复关",
            "裂缝中偶尔出现的恢复节点，能恢复冒险者生命。",
            "仙泉不治病，它只借给你一点命，记得还。"),
        W("W006", "见习徽章", "物品", "完成新手引导",
            "新人冒险者身份的象征，剧情中玩家最终将其扔回会长桌上。",
            "徽章很轻，但戴着它的时候，总觉得有点喘不过气。"),
        W("W007", "委托书", "物品", "进入城镇后解锁",
            "公会派发任务的书面凭证，新手引导中连名字都没填。",
            "那不是任务，是一张让你闭嘴的纸。"),
        W("W008", "空洞之喉", "传说", "解锁 W001、W002、W003 后自动解锁",
            "远古灾兽，诸神为将其放逐而撕裂世界皮肤，封入大地深处。",
            "它还在呼吸。每一次裂缝扩张，都是它的哈欠。"),
    };

    public static readonly StoryEntry[] Main =
    {
        new StoryEntry
        {
            Id = "P0", Title = "序章 见习者的第一天", Unlock = "完成新手引导",
            Summary = "玩家加入冒险者公会，会长派发森林层委托。咨询台小姐传授三条下裂缝规则。",
            Extra = "新人，森林层最近有些怪物躁动。去吧，证明你有资格留下。"
        },
        new StoryEntry
        {
            Id = "C1", Title = "第一章 暮影森林", Unlock = "进入第一章普通难度",
            Summary = "玩家进入暮影森林练手，击败森之守护者后发现疑似小美小队留下的剑痕。",
            Extra = "她还活着，一定来过这里。 / 这剑痕……不能确定是谁。 / 不管是谁，先活下去再说。"
        },
    };

    public static readonly StoryEntry[] Side =
    {
        new StoryEntry
        {
            Id = "S001", Title = "老盾的过去", Unlock = "老盾累计参战 10 次",
            Summary = "老盾透露自己曾是正式冒险者，因一次任务失败被公会边缘化。",
            Extra = "奖励：老盾好感度 +10，金币 ×100"
        },
        new StoryEntry
        {
            Id = "S002", Title = "咨询台的歉意", Unlock = "通关第一章后与咨询台小姐对话",
            Summary = "咨询台小姐欲言又止，暗示森林层的任务并不只是“清理怪物”。",
            Extra = "奖励：天赋石 ×1"
        },
        new StoryEntry
        {
            Id = "S003", Title = "格雷的笔记", Unlock = "在暮影森林精英关概率掉落",
            Summary = "捡到一本破损的猎人笔记，署名格雷，记录了对裂缝异常的观察。",
            Extra = "奖励：强化石 ×8"
        },
        new StoryEntry
        {
            Id = "S004", Title = "阿尔托的剑痕", Unlock = "击败森之守护者后概率触发",
            Summary = "石碑旁的岩石上有一道新鲜的圣殿骑士剑痕，阿尔托似乎刚离开不久。",
            Extra = "奖励：金币 ×200"
        },
        new StoryEntry
        {
            Id = "S005", Title = "商人的第一笔生意", Unlock = "累计在商店消费 1000 金币",
            Summary = "酒馆商人记住你的名字，送你一瓶中级生命药水。",
            Extra = "奖励：中级生命药水 ×1"
        },
    };

    public static bool ChapterCleared(int chapter)
    {
        var list = SaveSystem.Instance?.Data?.chapterClearCounts;
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].chapter == chapter && list[i].clearCount > 0)
                return true;
        return false;
    }

    public static bool HasMerc(string assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return false;
        var list = SaveSystem.Instance?.Data?.permanentMercs;
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (m.mercId == assetId) return true;
        }
        return false;
    }

    public static bool AchDone(string runtimeId)
    {
        var set = SaveSystem.Instance?.Data?.completedAchievements;
        return set != null && set.Contains(runtimeId);
    }

    public static int AchProgress(string runtimeId)
    {
        var map = SaveSystem.Instance?.Data?.achievementProgress;
        if (map == null) return 0;
        return map.TryGetValue(runtimeId, out int v) ? v : 0;
    }

    public static bool MonsterUnlocked(MonsterEntry e)
    {
        if (e.LaterChapter) return false;
        if (e.Kind == "首领") return ChapterCleared(1) || AchDone("kill_ch1_boss_1") || AchDone("clear_ch1");
        if (e.Kind == "精英") return ChapterCleared(1) || StoryProgress.TutorialBattleCleared;
        return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || ChapterCleared(1);
    }

    public static bool MercUnlocked(MercEntry e)
    {
        if (e.StoryNpc)
        {
            if (e.Id == "C001") return StoryProgress.TutorialIntroDone || StoryProgress.GetBond(StoryProgress.NpcXiaomei) > 0;
            if (e.Id == "C002") return StoryProgress.GetBond(StoryProgress.NpcAltor) > 0 || ChapterCleared(1);
            if (e.Id == "C003") return StoryProgress.GetBond(StoryProgress.NpcGrey) > 0 || ChapterCleared(1);
            return StoryProgress.TutorialIntroDone;
        }
        if (e.Id == "H001") return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || HasMerc("dunbing101") || HasMerc("dunbing102");
        if (HasMerc(e.AssetId)) return true;
        return false;
    }

    public static bool AchUnlocked(AchEntry e)
    {
        switch (e.Id)
        {
            case "A001": return StoryProgress.TutorialDone;
            case "A002": return StoryProgress.TutorialDone;
            case "A003": return false;
            case "A004": return AchProgress("kill_total_100") >= 100 || AchDone("kill_total_100");
            case "A005": return ChapterCleared(1);
            case "A006": return AchDone("kill_ch1_boss_1") || AchDone("clear_ch1") || ChapterCleared(1);
            case "A007": return AchDone("equip_collect_50") || AchProgress("equip_collect_50") >= 50;
            case "A008": return false;
            case "A009": return HasMerc("dunbing101") || HasMerc("dunbing102") || HasMerc("gongshou101") || HasMerc("kuangzhan101") || HasMerc("naima101");
            case "A010": return ChapterCleared(1);
            case "A011": return (SaveSystem.Instance?.Data?.totalGold ?? 0) >= 10000;
            case "A012": return false;
            case "A013": return false;
            case "A014": return false;
            case "A015": return false;
            default: return false;
        }
    }

    public static bool WorldUnlocked(WorldEntry e)
    {
        switch (e.Id)
        {
            case "W001": return true;
            case "W002": return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || ChapterCleared(1);
            case "W003": return StoryProgress.TutorialIntroDone || StoryProgress.TutorialDone;
            case "W004": return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || ChapterCleared(1);
            case "W005": return ChapterCleared(1);
            case "W006": return StoryProgress.TutorialDone;
            case "W007": return StoryProgress.TutorialIntroDone || StoryProgress.TutorialDone;
            case "W008":
                return WorldUnlocked(World[0]) && WorldUnlocked(World[1]) && WorldUnlocked(World[2]);
            default: return false;
        }
    }

    public static bool MainUnlocked(StoryEntry e)
    {
        if (e.Id == "P0") return StoryProgress.TutorialDone || StoryProgress.TutorialIntroDone;
        if (e.Id == "C1") return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || ChapterCleared(1);
        return false;
    }

    public static bool SideUnlocked(StoryEntry e)
    {
        if (e.Id == "S001") return StoryProgress.TutorialDone && HasMerc("dunbing101");
        if (e.Id == "S002") return ChapterCleared(1) && StoryProgress.GetBond(StoryProgress.NpcEileen) > 0;
        if (e.Id == "S003") return ChapterCleared(1);
        if (e.Id == "S004") return StoryProgress.Chapter1ChoiceDone || ChapterCleared(1);
        if (e.Id == "S005") return false;
        return false;
    }

    static MonsterEntry M(string id, string name, string asset, string kind, string place, string unlock, string desc, string lore)
    {
        return new MonsterEntry
        {
            Id = id, Name = name, AssetId = asset, Kind = kind, Place = place,
            Unlock = unlock, Desc = desc, Lore = lore, LaterChapter = false
        };
    }

    static MonsterEntry Later(string id, string name, string asset, string kind, string place, string lore)
    {
        return new MonsterEntry
        {
            Id = id, Name = name, AssetId = asset, Kind = kind, Place = place,
            Unlock = "后续裂缝层", Desc = "在后续裂缝层中可解锁。", Lore = lore, LaterChapter = true
        };
    }

    static MercEntry Npc(string id, string name, string asset, string role, string desc, string lore)
    {
        return new MercEntry
        {
            Id = id, Name = name, AssetId = asset, Role = role, Unlock = "主线推进",
            Place = "剧情", Desc = desc, Lore = lore, StoryNpc = true
        };
    }

    static MercEntry Hire(string id, string name, string asset, string role, string unlock, string place, string desc, string lore)
    {
        return new MercEntry
        {
            Id = id, Name = name, AssetId = asset, Role = role, Unlock = unlock,
            Place = place, Desc = desc, Lore = lore, StoryNpc = false
        };
    }

    static AchEntry A(string id, string name, string cat, string unlock, string reward, string desc)
    {
        return new AchEntry { Id = id, Name = name, Category = cat, Unlock = unlock, Reward = reward, Desc = desc };
    }

    static WorldEntry W(string id, string name, string cat, string unlock, string desc, string flavor)
    {
        return new WorldEntry { Id = id, Name = name, Category = cat, Unlock = unlock, Desc = desc, Flavor = flavor };
    }
}
