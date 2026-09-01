/// <summary>
/// 冒险日志 V1.0 图鉴文案与解锁判定。
/// 数据来自 Docs/像素冒险：裂隙之刃_冒险日志数据设计_V1.0.md
/// 未单独落盘遭遇表时，用存档进度近似解锁。
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
        public string Nickname;
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
        // —— 暮影森林（第一章）——
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
        M("M012", "森之守护者", "forest_412", "首领", "暮影森林·首领关", "首次击败",
            "沉睡于石碑之后的古老树灵，既是森林的意志，也是裂缝侵蚀的第一个牺牲品。",
            "它原本守护着每一棵树。现在它只能守护自己的疯狂。"),

        // —— 雨林遗迹（第二章）——
        Later("M101", "跳跳蛙", "jungle_201", "普通", "雨林遗迹·普通关",
            "跳得比见习冒险者跑得还快，但总是跳进自己同伴怀里。"),
        Later("M102", "毒箭蛙", "jungle_202", "普通", "雨林遗迹·普通关",
            "雨林原住民把它当染料用，直到有人染成了绿色。"),
        Later("M103", "藤蔓蛇", "jungle_203", "普通", "雨林遗迹·普通关",
            "它其实不是想咬你，只是想借你的体温暖和一下。"),
        Later("M104", "孢子蝠", "jungle_204", "普通", "雨林遗迹·普通关",
            "被孢子呛到的人会打喷嚏，连打七个，老盾数过。"),
        Later("M105", "泥潭蟹", "jungle_205", "普通", "雨林遗迹·普通关",
            "它夹什么取决于前一天吃了什么。"),
        Later("M106", "食虫花", "jungle_206", "普通", "雨林遗迹·普通关",
            "老盾曾把糖水涂在盾牌上测试它，洗了一下午。"),
        Later("M107", "雨林史莱姆", "jungle_207", "普通", "雨林遗迹·普通关",
            "它的座右铭是：你是什么，我就是什么的一部分。"),
        Later("M108", "巨颚蚁", "jungle_208", "精英", "雨林遗迹·精英关",
            "它们排队搬家时，新手常误以为是条路。"),
        Later("M109", "毒蛇藤", "jungle_209", "精英", "雨林遗迹·精英关",
            "很多新手把它当绳子爬过，结局都不太体面。"),
        Later("M110", "沼泽鳄", "jungle_210", "精英", "雨林遗迹·精英关",
            "它等猎物耐心十足，等开饭也一样。"),
        Later("M111", "雨林巨蟒", "jungle_211", "首领", "雨林遗迹·首领关",
            "它把自己绕成迷宫，结果自己也不记得尾巴在哪。"),
        Later("M112", "遗迹花后", "jungle_212", "首领", "雨林遗迹·首领关",
            "它之所以巨大，是因为从来没人敢告诉它该减肥。"),

        // —— 亡者墓地（第三章）——
        Later("M201", "骷髅兵", "undead_101", "普通", "亡者墓地·普通关",
            "生前是公会会计，死后还在数金币。你打死它，它掉的是铜板。"),
        Later("M202", "僵尸农夫", "undead_102", "普通", "亡者墓地·普通关",
            "总在问‘我的锄头呢’，但其实它早把锄头当武器抡你了。"),
        Later("M203", "幽灵学徒", "undead_103", "普通", "亡者墓地·普通关",
            "它生前魔法考试不及格，死后依然放不出火球。"),
        Later("M204", "腐肉犬", "undead_104", "普通", "亡者墓地·普通关",
            "它追你的原因可能是你昨晚吃了肉干。"),
        Later("M205", "游魂", "undead_105", "普通", "亡者墓地·普通关",
            "它只是想找个人说话，但没人听得懂。"),
        Later("M206", "骷髅弓箭手", "undead_106", "普通", "亡者墓地·普通关",
            "它射出的箭都是自己骨头做的，射一根少一根。"),
        Later("M207", "瘟疫鼠", "undead_107", "普通", "亡者墓地·普通关",
            "它不是故意散播瘟疫，只是毛掉得厉害。"),
        Later("M208", "幽灵会计", "undead_108", "精英", "亡者墓地·精英关",
            "死后掉的账本比装备还多。老盾说它的字比会长还难认。"),
        Later("M209", "无头骑士", "undead_109", "精英", "亡者墓地·精英关",
            "它最大的烦恼是下雨时脑袋会进水。"),
        Later("M210", "死灵法师", "undead_110", "精英", "亡者墓地·精英关",
            "它其实想复活自己，结果每次都复活出一只青蛙。"),
        Later("M211", "骸骨领主", "undead_111", "首领", "亡者墓地·首领关",
            "它觉得自己是王，但连个能坐的椅子都没有。"),
        Later("M212", "骨龙幼崽", "undead_112", "首领", "亡者墓地·首领关",
            "它觉得自己很可怕，直到一只猫对着它打了个哈欠。"),

        // —— 海岛沉船（第四章）——
        Later("M301", "海盐蟹", "sea_301", "普通", "海岛沉船·普通关",
            "它夹你的力气取决于今天潮水大不大。"),
        Later("M302", "刺尾鱼", "sea_302", "普通", "海岛沉船·普通关",
            "它们的队伍像一把梳子，把海水梳得哗哗响。"),
        Later("M303", "铁甲蟹", "sea_303", "普通", "海岛沉船·普通关",
            "壳硬到能挡箭，但翻过来就再也翻不回去。这是它的秘密。"),
        Later("M304", "海藻怪", "sea_304", "普通", "海岛沉船·普通关",
            "它只是想上岸晒晒太阳，但你打断了它的假期。"),
        Later("M305", "灯笼水母", "sea_305", "普通", "海岛沉船·普通关",
            "发光是为了吸引猎物，但常常吸引来的是另一只灯笼水母。"),
        Later("M306", "潮汐史莱姆", "sea_306", "普通", "海岛沉船·普通关",
            "退潮时它会缩小一半，像一块漏气的垫子。"),
        Later("M307", "海盗幽灵", "sea_307", "普通", "海岛沉船·普通关",
            "它说的藏宝地点每次都不一样，老盾怀疑它根本记不住。"),
        Later("M308", "深海章鱼", "sea_308", "精英", "海岛沉船·精英关",
            "它其实只有七条触手，第八条被它自己吃掉了。"),
        Later("M309", "暴风雨海鸥", "sea_309", "精英", "海岛沉船·精英关",
            "它不是生气，只是静电太多，碰一下会炸毛。"),
        Later("M310", "贝壳精", "sea_310", "精英", "海岛沉船·精英关",
            "它把壳当成房子，房贷还了五十年。"),
        Later("M311", "沉船船长", "sea_311", "首领", "海岛沉船·首领关",
            "他下令全船陪葬，结果自己先忘了为什么。"),
        Later("M312", "克拉肯触须", "sea_312", "首领", "海岛沉船·首领关",
            "船员打赌它有多长，目前没人看到尽头。"),

        // —— 风车平原（第五章）——
        Later("M401", "绿史莱姆", "field_501", "普通", "风车平原·普通关",
            "普通史莱姆的梦想是变大，最后都变成了别人的经验。"),
        Later("M402", "红史莱姆", "field_502", "普通", "风车平原·普通关",
            "它变红不是因为愤怒，是因为吃了太多辣椒。"),
        Later("M403", "跳跳兔", "field_503", "普通", "风车平原·普通关",
            "它跳起来比你跑得快，但落地时常常崴脚。"),
        Later("M404", "稻草人", "field_504", "普通", "风车平原·普通关",
            "农民说它能吓跑乌鸦，结果乌鸦学会了放火。"),
        Later("M405", "田鼠", "field_505", "普通", "风车平原·普通关",
            "它偷的麦子够开一家面包店，但它只囤不吃。"),
        Later("M406", "风精灵", "field_506", "普通", "风车平原·普通关",
            "它说话声音很轻，老盾以为那是耳鸣。"),
        Later("M407", "野狼", "field_507", "普通", "风车平原·普通关",
            "月圆之夜它们会嚎叫，但平原没有月亮，它们只是跟风车较劲。"),
        Later("M408", "狂暴野狼", "field_508", "精英", "风车平原·精英关",
            "它被狼群赶了出来，所以把气撒在你身上。"),
        Later("M409", "风车巨人", "field_509", "精英", "风车平原·精英关",
            "它转起来的时候，农民会跑出门喊‘要下雨啦’。"),
        Later("M410", "毒蘑菇集群", "field_510", "精英", "风车平原·精英关",
            "它们不是团结，只是互相挤得没法分开。"),
        Later("M411", "史莱姆王", "field_511", "首领", "风车平原·首领关",
            "普通史莱姆的梦想是变大，它的梦想是学会减肥。"),
        Later("M412", "平原巨像", "field_512", "首领", "风车平原·首领关",
            "农民以为它是山神，后来发现它只是迷路了。"),

        // —— 深岩洞穴（第六章）——
        Later("M501", "洞穴蝙蝠", "cave_601", "普通", "深岩洞穴·普通关",
            "它们不是瞎，只是懒得睁眼。"),
        Later("M502", "岩蜥", "cave_602", "普通", "深岩洞穴·普通关",
            "它舌头的长度是身体的两倍，主要用于抢同伴的食物。"),
        Later("M503", "水晶幼虫", "cave_603", "普通", "深岩洞穴·普通关",
            "矿工把它当成会动的宝石，结果赔了一根手指。"),
        Later("M504", "矿骷髅", "cave_604", "普通", "深岩洞穴·普通关",
            "它生前挖了一辈子矿，死后还在挖自己的坟。"),
        Later("M505", "毒气孢子", "cave_605", "普通", "深岩洞穴·普通关",
            "它不是想毒你，只是想让你闻一闻它的香水。"),
        Later("M506", "熔岩史莱姆", "cave_606", "普通", "深岩洞穴·普通关",
            "它觉得很热，但从来找不到空调遥控器。"),
        Later("M507", "石像鬼幼体", "cave_607", "普通", "深岩洞穴·普通关",
            "它觉得自己很吓人，但冒险者觉得它像会动的屋顶装饰。"),
        Later("M508", "水晶蜘蛛", "cave_608", "精英", "深岩洞穴·精英关",
            "腿是水晶做的，跑起来像风铃。老盾说听着像公会食堂开饭铃。"),
        Later("M509", "岩甲虫", "cave_609", "精英", "深岩洞穴·精英关",
            "它滚下山的时候，矿工以为是山体滑坡。"),
        Later("M510", "暗影潜行者", "cave_610", "精英", "深岩洞穴·精英关",
            "它最怕火把，因为照出自己的影子会吓它一跳。"),
        Later("M511", "洞穴巨魔", "cave_611", "首领", "深岩洞穴·首领关",
            "它数数只能数到三，所以战斗时只会喊‘一、二、打’。"),
        Later("M512", "晶簇巨兽", "cave_612", "首领", "深岩洞穴·首领关",
            "矿工协会给它起了个绰号：会走路的退休金。"),

        // —— 熔岩核心（第七章）——
        Later("M601", "小火魔", "devil_701", "普通", "熔岩核心·普通关",
            "脾气和体温一样高，但 cooling 方式是哭。别让它哭，会更热。"),
        Later("M602", "熔岩史莱姆", "devil_702", "普通", "熔岩核心·普通关",
            "它冷却后变硬，但没人愿意等那么久。"),
        Later("M603", "火焰蝙蝠", "devil_703", "普通", "熔岩核心·普通关",
            "它其实不想着火，只是翅膀太干燥。"),
        Later("M604", "灰烬小鬼", "devil_704", "普通", "熔岩核心·普通关",
            "它最怕打喷嚏，因为一个喷嚏能让自己少半边脸。"),
        Later("M605", "硫磺蜘蛛", "devil_705", "普通", "熔岩核心·普通关",
            "它的网不是用来捕猎，是用来熏走天敌。"),
        Later("M606", "焦炭骷髅", "devil_706", "普通", "熔岩核心·普通关",
            "它生前是厨师，死后依然掌握不好火候。"),
        Later("M607", "爆裂虫", "devil_707", "普通", "熔岩核心·普通关",
            "它们不是勇敢，只是紧张到控制不住自己。"),
        Later("M608", "岩浆巨人", "devil_708", "精英", "熔岩核心·精英关",
            "它洗澡的地方叫火山口，但它从不洗澡。"),
        Later("M609", "深渊犬", "devil_709", "精英", "熔岩核心·精英关",
            "左边头想咬你，右边头想追你，中间头在发呆。"),
        Later("M610", "炎魔卫士", "devil_710", "精英", "熔岩核心·精英关",
            "它每天的工作是站岗，但从来没人告诉它防谁。"),
        Later("M611", "深渊大眼", "devil_711", "首领", "熔岩核心·首领关",
            "它看你的时候，你也在看它。建议不要对视超过三秒。"),
        Later("M612", "熔岩龙王", "devil_712", "首领", "熔岩核心·首领关",
            "它打了个哈欠，结果引发了一次小喷发。"),

        // —— 永冻雪原（第八章）——
        Later("M701", "雪球怪", "ice_801", "普通", "永冻雪原·普通关",
            "越滚越大，但滚到火堆旁边会主动停下喝口汤。"),
        Later("M702", "冰晶虫", "ice_802", "普通", "永冻雪原·普通关",
            "它掉下来的碎片被冒险者当成宝石，结果只是冰。"),
        Later("M703", "冻土鼠", "ice_803", "普通", "永冻雪原·普通关",
            "它囤的粮食够过冬三次，但它自己总忘记藏哪了。"),
        Later("M704", "雪狐", "ice_804", "普通", "永冻雪原·普通关",
            "它偷鱼的本事一流，但吃完会留下道歉似的爪印。"),
        Later("M705", "寒冰史莱姆", "ice_805", "普通", "永冻雪原·普通关",
            "它觉得自己很酷，字面意义上的。"),
        Later("M706", "冰棱蝙蝠", "ice_806", "普通", "永冻雪原·普通关",
            "它倒挂在洞顶时，常被误认为是吊灯。"),
        Later("M707", "雪怪幼崽", "ice_807", "普通", "永冻雪原·普通关",
            "它想找妈妈，但妈妈可能正在被勇者打。"),
        Later("M708", "冰晶狼", "ice_808", "精英", "永冻雪原·精英关",
            "奔跑时会打滑，精英怪里摔倒次数最多的一位。"),
        Later("M709", "霜巨人", "ice_809", "精英", "永冻雪原·精英关",
            "它打个喷嚏就能冻住一片湖，但它从不会感冒。"),
        Later("M710", "寒冰幽灵", "ice_810", "精英", "永冻雪原·精英关",
            "它不是想杀你，只是想要一个温暖的拥抱。"),
        Later("M711", "雪原猛犸", "ice_811", "首领", "永冻雪原·首领关",
            "它走过的地方会留下深坑，冒险者以为是陨石。"),
        Later("M712", "冰霜巨龙", "ice_812", "首领", "永冻雪原·首领关",
            "它睡前会数羊，结果把一整群羊冻成了冰雕。"),
    };

    public static readonly MercEntry[] Mercs =
    {
        Npc("C001", "小美", "npc_xiaomei", "青梅竹马 / 失踪小队队长",
            "绿色衣服、棕色双马尾的少女，法系冒险者，既能用法术输出也能为队友恢复生命。",
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

        Hire("H001", "马库斯", "老盾", "dunbing101", "剑盾卫士", "新手引导自动入队", "暮影森林·教学关",
            "手持大盾、全身重甲的中年佣兵，被玩家从怪物包围中救出。擅长挡刀和吐槽。",
            "他的盾牌上刻着前任主人的名字。他说是自己，只是字磨花了。"),
        Hire("H002", "洛恩", "铁皮", "dunbing102", "剑盾卫士", "完成新手引导后酒馆可招募", "酒馆",
            "比老盾年轻一点的盾兵，盔甲更亮，但实战经验更少。",
            "老盾总说他‘盾举得太高，挡住自己的视线’。洛恩不服，直到撞上一棵树。"),
        Hire("H003", "塔克", "重盾", "dunbing201", "剑盾卫士", "累计在酒馆招募 3 名佣兵后开放招募", "酒馆",
            "比老盾还壮实的重装盾兵，移动慢，但几乎不会被击退。",
            "他一顿饭能吃三个人的量。公会食堂看见他来，会提前多蒸一锅。"),
        Hire("H004", "维克", "钢盾", "dunbing202", "剑盾卫士", "通关第一章噩梦难度后开放招募（中期传奇）", "酒馆",
            "寡言的退伍士兵，盾牌上全是旧伤疤，从不主动提起过去。",
            "有人说他的盾救过会长的命。也有人说那道疤是被会长派人砍的。"),
        Hire("H005", "米娅", "小红", "gongshou101", "游侠", "完成新手引导后酒馆可招募", "酒馆",
            "粉发弓箭手，动作轻快，箭无虚发——至少她是这么说的。",
            "她自称‘百步穿杨’，但有一次射中了老盾的盾牌。老盾安慰她：‘也算命中。’"),
        Hire("H006", "希尔", "鹰眼", "gongshou201", "游侠", "累计击败精英怪物 10 次后开放招募", "酒馆",
            "戴兜帽的沉稳弓箭手，话少箭多，据说以前做过边境巡林人。",
            "他从不笑。有人打赌他笑一次请全队喝酒，目前欠账已经够买一把弓。"),
        Hire("H007", "布罗克", "大锤", "kuangzhan101", "狂战士", "完成新手引导后酒馆可招募", "酒馆",
            "手持巨型战斧的红角战士，攻击范围大，脾气和斧子一样直。",
            "他每次挥斧都会喊招式名。虽然招式名都是他自己编的，但气势很足。"),
        Hire("H008", "古恩", "斩铁", "kuangzhan102", "狂战士", "完成新手引导后酒馆可招募", "酒馆",
            "独眼狂战士，专砍重甲敌人，据说他的左眼是被自己的斧头弹片伤的。",
            "他讨厌螃蟹。所有带壳的。尤其是吃饭时咬不动的。"),
        Hire("H009", "莫丁", "碎岩", "kuangzhan201", "狂战士", "累计通关裂缝 20 次后开放招募", "酒馆",
            "使用双斧的矿石工人，退休后转职佣兵，对石头有执念。",
            "他判断敌人强不强，先看对方长得像不像矿石。首领怪在他眼里都是‘宝石’。"),
        Hire("H010", "凯恩", "狂牙", "kuangzhan202", "狂战士", "累计击败精英怪物 30 次后开放招募（前期传奇）", "酒馆",
            "披兽皮、戴兽牙项链的野性战士，攻击时会发出怪叫。",
            "他的怪叫其实是为了壮胆。老盾知道，但从不拆穿。"),
        Hire("H011", "索菲", "小白", "naima101", "牧师", "完成新手引导后酒馆可招募", "酒馆",
            "持法杖的白衣治疗者，新手冒险者最喜欢的队友类型。",
            "她的治疗术是公会同级考试第一名。缺点是念咒时不能被打断，否则会奶到怪物。"),
        Hire("H012", "塞拉", "小蓝", "naima102", "水系法师", "累计使用佣兵完成 15 次战斗后开放招募", "酒馆",
            "蓝发法师，擅长冰系控制法术，性格冷淡但战斗中很可靠。",
            "她的法杖总是凉的。夏天老盾喜欢偷偷靠过去乘凉，被她瞪过三次。"),
        Hire("H013", "莫娜", "紫晶", "naima201", "雷系法师", "累计进入裂缝 30 次后开放招募", "酒馆",
            "紫发法师，能召唤小型闪电，表情总是很不耐烦。",
            "她觉得见习冒险者问题太多。但如果你请她喝酒，她能把你想知道的全倒出来。"),
        Hire("H014", "伊芙", "火舞", "naima202", "火系法师", "完成成就「噩梦先驱」后开放招募（后期传奇）", "酒馆",
            "红发法师，脾气和火焰一样烈，擅长范围灼烧。",
            "她不许别人碰她的袍子角。上次有人踩到，那根火把三天没灭。"),
        Hire("H015", "艾拉", "风羽", "gongshou101", "游侠", "完成新手引导后酒馆可招募", "酒馆",
            "棕色短发的游侠，箭袋里永远留着一支没名字的羽箭。",
            "她说那支箭要留给‘配得上的人’。目前老盾的盾牌得分最高。"),
        Hire("H016", "杜娅", "怒角", "kuangzhan201", "狂战士", "累计击败精英怪物 8 次后开放招募", "酒馆",
            "红角狂战士，双斧挥砍时像一阵红色旋风。",
            "她开战前会整理护腕。老盾说那是她唯一的仪式感。"),
        Hire("H017", "莉娜", "圣光", "naima102", "牧师", "累计在酒馆招募 5 名佣兵后开放招募", "酒馆",
            "金发白衣牧师，治疗时嘴里念念有词。",
            "她念的是炖菜配方。据她说，步骤和祷告一样能让人安心。"),
        Hire("H018", "布朗", "铁壁", "dunbing101", "剑盾卫士", "完成新手引导后酒馆可招募", "酒馆",
            "年轻寡言的盾兵，盾牌上贴着一张旧报销单。",
            "他说欠公会钱的人命硬，所以把报销单当护身符。"),
        Hire("H019", "艾琳", "星火", "fashi101", "法师", "完成新手引导后酒馆可招募", "酒馆",
            "戴兜帽的初级法师，发色偏紫，手持短杖。看起来只是学徒，但杖尖偶尔溢出的魔力让老盾都往后退。",
            "她总说自己是'刚入门的法师'。但上次她不小心把营地篝火变成了火球。"),
        Hire("H020", "凯尔", "谜面", "fashi102", "法师", "累计击败精英怪物 15 次后开放招募", "酒馆",
            "浑身裹在深蓝兜袍里的神秘法师，从不露脸，声音像从很远的地方传来。",
            "有人打赌他兜帽下没有脸。老盾说'没脸的人不会点酒'，但他确实每次都点。"),
        Hire("H021", "格拉克斯", "懒鬼", "zhongzhan101", "重武者", "完成新手引导后酒馆可招募", "酒馆",
            "戴着露出半张脸头盔的重武者，扛着长柄巨斧，脸上写满了'随便吧'。",
            "他参战是因为'懒得找工作'。但真打起来，他懒得逃跑，所以总是站到最后。"),
        Hire("H022", "索尔", "铁面", "zhongzhan201", "重武者", "累计通关裂缝 25 次后开放招募", "酒馆",
            "头盔遮住整张脸的重武者，手持短柄巨斧，信奉实力代表一切。",
            "他从不摘盔。酒馆里有人说他其实是女的，有人说他其实是骷髅，没人敢当面问。"),
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
        W("W003", "皇家冒险者公会", "组织", "进入城镇后解锁",
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
            Summary = "玩家加入皇家冒险者公会，会长派发森林层委托。老盾作为临时队友入队，咨询台小姐传授三条下裂缝规则。",
            Extra = "新人，森林层最近有些怪物躁动。去吧，证明你有资格留下。"
        },
        new StoryEntry
        {
            Id = "C1A", Title = "第一章·一 蘑菇小径", Unlock = "通关 1-1",
            Summary = "玩家独自深入暮影森林，遭遇大量走路菇与毒伞菇，发现怪物比往年更躁动。",
            Extra = "这些蘑菇往年不会离开腐木区，有人在惊扰它们。"
        },
        new StoryEntry
        {
            Id = "C1B", Title = "第一章·二 浆果与史莱姆", Unlock = "通关 1-3",
            Summary = "穿过浆果怪聚集区与森林史莱姆湿地，老盾在营地休息时提起自己第一次下裂缝的糗事。",
            Extra = "我第一次下裂缝，比你还紧张。至少你没把火把扔进蘑菇堆。"
        },
        new StoryEntry
        {
            Id = "C1C", Title = "第一章·三 精英：刺毛兽", Unlock = "首次击败刺毛兽",
            Summary = "在精英关遇到炸毛的刺毛兽，击败后发现其巢穴附近有奇怪焦痕，疑似火系法术残留。",
            Extra = "这不是普通火烧的……是某种法术。"
        },
        new StoryEntry
        {
            Id = "C1D", Title = "第一章·四 甲虫巡逻道", Unlock = "通关 1-5",
            Summary = "沿甲虫巡逻道前进，硬壳甲虫数量异常增多，似乎被某种气息驱赶向森林外围。",
            Extra = "它们在逃。前面有什么东西让它们害怕。"
        },
        new StoryEntry
        {
            Id = "C1E", Title = "第一章·五 精英：赤甲蟹", Unlock = "首次击败赤甲蟹",
            Summary = "湿地深处遭遇赤甲蟹，战后从壳缝中发现一片破旧布条，绣有圣殿骑士纹章。",
            Extra = "阿尔托真的来过。他为什么不等我们？"
        },
        new StoryEntry
        {
            Id = "C1F", Title = "第一章·六 石碑残片", Unlock = "通关 1-7",
            Summary = "森林深处出现一座破损石碑，上面刻着三种古老符文。玩家选择触摸其中一种，影响后续对话倾向与属性加成。",
            Extra = "选一条你认为正确的路。公会不在乎过程，只在乎结果。"
        },
        new StoryEntry
        {
            Id = "C1G", Title = "第一章·七 森之守护者", Unlock = "首次击败森之守护者",
            Summary = "石碑后方正是森之守护者栖身地。击败首领后，森林暂时恢复平静，但裂缝的躁动并未停止。",
            Extra = "她还活着，一定来过这里。 / 这剑痕……不能确定是谁。 / 不管是谁，先活下去再说。"
        },
        new StoryEntry
        {
            Id = "C1Z", Title = "第一章·尾声 归来的见习者", Unlock = "通关第一章普通难度",
            Summary = "回到公会提交委托，会长态度微妙。咨询台小姐偷偷塞给玩家一张写有小美名字的纸条。",
            Extra = "做得好。但记住，见习者不该问太多问题。"
        },
    };

    public static readonly StoryEntry[] Side =
    {
        new StoryEntry
        {
            Id = "S001", Title = "老盾的过去", Unlock = "老盾累计参战 10 次",
            Summary = "老盾透露自己曾是正式冒险者，因一次任务失败被公会边缘化。他保住新人、丢了徽章，从此只能当佣兵。",
            Extra = "奖励：老盾好感度 +10，金币 ×100"
        },
        new StoryEntry
        {
            Id = "S002", Title = "咨询台的歉意", Unlock = "通关第一章后与咨询台小姐对话",
            Summary = "咨询台小姐欲言又止，暗示森林层的任务并不只是“清理怪物”，有人在借委托之名掩盖裂缝扩张。",
            Extra = "奖励：天赋石 ×1"
        },
        new StoryEntry
        {
            Id = "S003", Title = "格雷的笔记", Unlock = "在暮影森林精英关概率掉落",
            Summary = "捡到一本破损的猎人笔记，署名格雷，记录了对裂缝异常的观察：怪物躁动、符文发光、有人深入裂缝未归。",
            Extra = "奖励：强化石 ×8"
        },
        new StoryEntry
        {
            Id = "S004", Title = "阿尔托的剑痕", Unlock = "击败森之守护者后概率触发",
            Summary = "石碑旁的岩石上有一道新鲜的圣殿骑士剑痕，阿尔托似乎刚离开不久。他为什么没回公会报告？",
            Extra = "奖励：金币 ×200"
        },
        new StoryEntry
        {
            Id = "S005", Title = "商人的第一笔生意", Unlock = "累计在商店消费 1000 金币",
            Summary = "酒馆商人记住你的名字，送你一瓶中级生命药水，并暗示“好酒才配好客户”。",
            Extra = "奖励：中级生命药水 ×1"
        },
        new StoryEntry
        {
            Id = "S006", Title = "米娅的赌约", Unlock = "米娅累计参战 10 次",
            Summary = "米娅承认自己打赌输过老盾一瓶酒，但坚称“那支箭是故意射偏的”。",
            Extra = "奖励：米娅好感度 +10，羽箭 ×5"
        },
        new StoryEntry
        {
            Id = "S007", Title = "布罗克的招式册", Unlock = "布罗克累计参战 10 次",
            Summary = "布罗克向玩家展示他手写的招式册，每一招都比上一招更中二。他希望玩家帮他取一个配得上“终极奥义”的名字。",
            Extra = "奖励：布罗克好感度 +10，技能书 ×1"
        },
        new StoryEntry
        {
            Id = "S008", Title = "索菲的炖菜配方", Unlock = "索菲累计参战 10 次",
            Summary = "索菲终于把治疗咒语和炖菜配方分清了，并送给玩家一份“吃了不会看见会长跳舞”的应急口粮。",
            Extra = "奖励：索菲好感度 +10，应急口粮 ×3"
        },
        new StoryEntry
        {
            Id = "S009", Title = "失踪的猎人", Unlock = "收集 3 页格雷笔记后解锁",
            Summary = "将格雷的笔记交给咨询台小姐，她脸色变了，只说“这事不要问会长”。",
            Extra = "奖励：稀有装备箱 ×1"
        },
        new StoryEntry
        {
            Id = "S010", Title = "商人的老客户", Unlock = "累计在商店消费 3000 金币",
            Summary = "商人开始跟你聊进货渠道，暗示酒馆地下可能有“另一份价目表”。",
            Extra = "奖励：高级生命药水 ×2，金币 ×300"
        },
        new StoryEntry
        {
            Id = "S011", Title = "森林植物图鉴", Unlock = "击败走路菇、毒伞菇、红浆果怪各 20 只",
            Summary = "公会学者委托收集森林植物样本，完成图鉴后他对“红浆果能不能吃”保持沉默。",
            Extra = "奖励：天赋石 ×1，金币 ×150"
        },
        new StoryEntry
        {
            Id = "S012", Title = "精英猎手", Unlock = "击败刺毛兽、赤甲蟹、岩块傀儡各 3 次",
            Summary = "公会战士团认可你的实力，提供一份精英怪物弱点笔记。",
            Extra = "奖励：强化石 ×12"
        },
        new StoryEntry
        {
            Id = "S013", Title = "会长室的秘密", Unlock = "通关第一章困难难度后解锁",
            Summary = "会长室的门没关严，里面传出关于“空洞之喉”的低声争论。",
            Extra = "奖励：解锁世界条目 W008"
        },
        new StoryEntry
        {
            Id = "S014", Title = "小美的信物", Unlock = "完成 S004 与 S009 后解锁",
            Summary = "咨询台小姐终于承认：小美小队不是失踪，是会长派他们去裂缝更深处执行任务。",
            Extra = "奖励：小美羁绊线索 ×1，金币 ×500"
        },
        new StoryEntry
        {
            Id = "S015", Title = "老盾的盾纹", Unlock = "老盾好感度达到 20",
            Summary = "老盾喝醉后揭开盾牌内侧的纹章——那是他失去的正式冒险者编号。",
            Extra = "奖励：老盾专属被动解锁"
        },
        new StoryEntry
        {
            Id = "S016", Title = "最初的裂缝", Unlock = "通关第一章噩梦难度后解锁",
            Summary = "一位神秘老人出现在酒馆角落，讲述诸神撕裂世界封印空洞之喉的传说，与石碑符文呼应。",
            Extra = "奖励：传奇装备箱 ×1"
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
        if (AdventureCodex.IsSeenMerc(assetId)) return true;
        var data = SaveSystem.Instance?.Data;
        if (data?.hiredMercs != null)
        {
            for (int i = 0; i < data.hiredMercs.Count; i++)
            {
                var m = data.hiredMercs[i];
                if (m != null && m.mercId == assetId) return true;
            }
        }
        var list = data?.permanentMercs;
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
        // 章未开：整章不可见（图鉴分页侧处理）；条目「亮图」看遭遇存档
        int ch = AdventureCodex.MonsterChapter(e);
        if (!AdventureCodex.ChapterUnlocked(ch)) return false;
        return AdventureCodex.IsSeenMonster(e.Id);
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
        if (MercRosterDefs.TryGetByHireId(e.Id, out var roster) && roster.InInitialPool)
            return StoryProgress.TutorialDone || StoryProgress.TutorialBattleCleared;
        return false;
    }

    public static bool AchUnlocked(AchEntry e)
    {
        if (AdventureLogAchievements.IsCompleted(e.Id) || AdventureLogAchievements.IsClaimed(e.Id))
            return true;
        return AdventureLogAchievements.CheckCondition(e.Id);
    }

    public static bool WorldUnlocked(WorldEntry e)
    {
        if (AdventureCodex.IsWorldUnlockedFlag(e.Id)) return true;
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
        if (AdventureCodex.IsMainCompleted(e.Id)) return true;
        if (e.Id == "P0") return StoryProgress.TutorialDone || StoryProgress.TutorialIntroDone;
        if (e.Id == "C1A")
            return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || ChapterCleared(1);
        if (e.Id == "C1B")
            return ChapterCleared(1) || (ChapterManager.Instance != null && ChapterManager.Instance.currentStageIndex >= 2);
        if (e.Id == "C1C")
            return AdventureLogAchievements.GetProgress("elite_kill") > 0 || ChapterCleared(1);
        if (e.Id == "C1D")
            return ChapterCleared(1) || (ChapterManager.Instance != null && ChapterManager.Instance.currentStageIndex >= 4);
        if (e.Id == "C1E")
            return AdventureLogAchievements.GetProgress("elite_kill") > 0 || ChapterCleared(1);
        if (e.Id == "C1F")
            return StoryProgress.Chapter1ChoiceDone || ChapterCleared(1);
        if (e.Id == "C1G")
            return AdventureLogAchievements.GetProgress("boss_ch1") > 0 || ChapterCleared(1);
        if (e.Id == "C1Z")
            return ChapterCleared(1);
        if (e.Id != null && e.Id.StartsWith("C1"))
            return ChapterCleared(1);
        return false;
    }

    public static bool SideUnlocked(StoryEntry e)
    {
        var sides = SaveSystem.Instance?.Data?.completedSideIds;
        if (sides != null && sides.Contains(e.Id)) return true;

        switch (e.Id)
        {
            case "S001":
                return AdventureLogAchievements.GetProgress(AdventureLogAchievements.ProgressLaodunBattles) >= 10;
            case "S002":
            case "S003":
            case "S004":
                return ChapterCleared(1);
            case "S011":
                return AdventureLogAchievements.GetProgress(AdventureLogAchievements.ProgressForestKills) >= 20;
            case "S012":
                return AdventureLogAchievements.GetProgress("elite_kill") >= 3;
            case "S013":
                return (SaveSystem.Instance?.Data?.ch1BestClearDifficulty ?? -1) >= 1;
            case "S016":
                return (SaveSystem.Instance?.Data?.ch1BestClearDifficulty ?? -1) >= 2;
            default:
                return false;
        }
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
            Id = id, Name = name, Nickname = "", AssetId = asset, Role = role, Unlock = "主线推进",
            Place = "剧情", Desc = desc, Lore = lore, StoryNpc = true
        };
    }

    static MercEntry Hire(string id, string name, string nickname, string asset, string role, string unlock, string place, string desc, string lore)
    {
        return new MercEntry
        {
            Id = id, Name = name, Nickname = nickname, AssetId = asset, Role = role, Unlock = unlock,
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
