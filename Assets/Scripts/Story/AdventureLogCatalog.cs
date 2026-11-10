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
        /// <summary>true = 不进佣兵图鉴（如酒馆 NPC 独眼：不是佣兵，只在酒馆出现）。</summary>
        public bool HideInCodex;
        /// <summary>剧情 NPC 等无花名册时的稀有度；Hire 佣兵以 MercRosterDefs 为准。</summary>
        public MercRosterDefs.MercRarity Rarity;
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

    /// <summary>
    /// 冒险日志【里程碑】分类条目（2026-09-19）。
    /// 每章只建 1 条带进度的条目，不按关建；展示形如「第 3 章 · 翡翠秘境　进度 4/10」。
    /// 进度 = 该章已首次通关关数 / 每章关数；已发石 = 首次通关石×已通数 +（整章通关额外石，若已通章）。
    /// </summary>
    public struct MilestoneEntry
    {
        public int Chapter;
        public string Name;          // 翡翠秘境
        public string Title;         // 第 3 章 · 翡翠秘境
        public int FirstClearStone;  // 按章首次通关石（来自 TalentDefs.TalentStoneRewards）
        public int ChapterClearStone;// 整章通关额外石
    }

    public static readonly MonsterEntry[] Monsters =
    {
        // —— 暮影森林（第一章）——
        M("M001", "森野蘑菇", "forest_101", "普通", "暮影森林·普通关", "首次遭遇",
            "拄着两条小短腿满林子巡逻的野生蘑菇，江湖人称「巡山菇」。",
            "自称「巡山的」，其实啥也没查出来过。你走过去它还盘问你：干什么的？——问完就一头撞过来。"),
        M("M002", "喷孢蘑菇", "forest_102", "普通", "暮影森林·普通关", "首次击败",
            "伞盖上全是气孔、见人就开喷的喷射蘑菇，江湖人称「喷子菇」。",
            "现实版网络喷子：喷的全是孢子，糊你一脸还振振有词。打它记得戴口罩。"),
        M("M003", "硬壳甲虫", "forest_103", "普通", "暮影森林·普通关", "首次遭遇",
            "脑袋硬得像口锅的硬壳甲虫，林子里公认的不好惹，江湖人称「硬茬子」。",
            "认准方向就一路撞到底，从不拐弯——不是不想，是脖子生锈了。江湖人称：林中第一硬茬。"),
        M("M004", "淤泥怪", "forest_104", "普通", "暮影森林·普通关", "首次击败",
            "一坨会思考的烂泥，说糊你一脸就糊你一脸，江湖人称「泥石流」。",
            "拦不住、躲不及、赢了也脏一手。新手第一课：绕着它走。"),
        M("M005", "树根怪", "forest_105", "普通", "暮影森林·普通关", "首次击败",
            "埋在地里修炼多年、盘根错节的老树根，江湖人称「盘根老怪」。",
            "修的是绊脚功。你不看路，它就教你做人——摔个狗吃屎，它在土里笑得根须直抖。"),
        M("M006", "疾足怪鸟", "forest_106", "精英", "暮影森林·精英关", "首次遭遇",
            "腿比身子长两倍的怪鸟，森林飞毛腿，江湖人称「腿哥」。",
            "跑得飞快，急刹车却是技术活——经常一个踉跄撞进你怀里，还装作故意的。"),
        M("M007", "湖底巨人", "forest_107", "精英", "暮影森林·精英关", "首次击败",
            "湖里住着的大块头，一天 24 小时里 23 小时在泡澡，江湖人称「泡澡大王」。",
            "谁来吵它泡澡，它就把谁捞起来当搓澡巾。不是坏，就是不懂「私人空间」这四个字。"),
        M("M008", "风之魔灵", "forest_108", "普通", "暮影森林·普通关", "首次遭遇",
            "一阵看得见的风，飘在半空专往人脸上糊沙，江湖人称「风来了」。",
            "它的日常就是：它来了，它糊了你一脸沙，它走了。你打不着它——它连打架都懒得正经打。"),
        M("M009", "愤怒树人", "forest_109", "普通", "暮影森林·普通关", "首次击败",
            "脾气最差的一棵树，叶子碰一下就原地爆炸，江湖人称「绿老头」。",
            "林子里都躲着它走，背地里叫它绿老头。它其实听得见，所以爆炸得更起劲了。"),
        M("M010", "石灵", "forest_110", "精英", "暮影森林·精英关", "首次击败",
            "一块成了精的石头，平生爱好是装死，江湖人称「石头记」。",
            "你路过它一动不动，你一转身，它就偷偷滚两步跟上来。一部会走路的石头记。"),
        M("M011", "自然祭司", "forest_111", "首领", "暮影森林·首领关", "首次击败",
            "林子深处最虔诚的一朵花，举着触须对天祈祷，江湖人称「花间祭」。",
            "它求什么没人知道，但撒起花粉来一点不含糊——念咒的时候，你最好憋住别打喷嚏。"),
        M("M012", "森林守护者", "forest_112", "首领", "暮影森林·首领关", "首次击败",
            "活了几百年的老树精，自称这片林子的看门大爷，江湖人称「老林头」。",
            "谁乱砍树它就扔松果砸谁，脾气倔、心眼不坏。扔松果不是攻击，是想让你陪它聊聊天。"),

        // —— 翡翠秘境（第三章）——
        Later("M101", "雨林疾行者", "jungle_301", "普通", "翡翠秘境·普通关",
            "江湖人称「秘境快递」：雨林跑得最快，使命必达。不跟你打，绕着你转圈，转到你晕了再补一脚——签收人是你的后脑勺。"),
        Later("M102", "翠色弹弓手", "jungle_302", "普通", "翡翠秘境·普通关",
            "江湖人称「皮筋王子」：拿果子当弹丸，准头随缘但量大，主打火力覆盖。它的皮筋从哪来的？雨林里没人敢问。"),
        Later("M103", "寂静毒虫", "jungle_303", "普通", "翡翠秘境·普通关",
            "江湖人称「消音虫」：名字叫「寂静」，本人却嘶嘶叫个不停。想低调但天赋不允许——从草里窜出来吓你一跳再咬一口。"),
        Later("M104", "部落斥候", "jungle_304", "普通", "翡翠秘境·普通关",
            "江湖人称「查户口的」：你是谁？从哪来？到哪去？三个问题问完，看不顺眼就抡家伙。查得比谁都严。"),
        Later("M105", "部落祭司", "jungle_305", "普通", "翡翠秘境·普通关",
            "江湖人称「求雨的」：仪式感拉满，边跳边撒粉末。你以为它在祈福，其实是在给你下「倒霉咒」——雨没求来，你先挨了一顿。"),
        Later("M106", "臭腺花", "jungle_306", "普通", "翡翠秘境·普通关",
            "外号就是本号：自带生化武器，靠近就熏得你头晕。它自己闻不着，还奇怪你怎么总躲着它——这朵花唯一的社交障碍，就是所有人都在躲它。"),
        Later("M107", "羞怯翼灵", "jungle_307", "普通", "翡翠秘境·普通关",
            "江湖人称「社恐鸟」：飞在半空、特别怕生。你一靠近它就捂脸躲开，躲完又忍不住探头看你——又怂又好奇，还想认识你。"),
        Later("M108", "灰毛兽", "jungle_308", "精英", "翡翠秘境·精英关",
            "江湖人称「灰滚滚」：一团灰扑扑的毛球，滚起来收不住，经常把自己滚进你怀里——然后咬你一口，继续装无辜。"),
        Later("M109", "蓝鳞兽", "jungle_309", "精英", "翡翠秘境·精英关",
            "江湖人称「蓝同学」：蓝盈盈的一小坨，秘境里的乖乖牌。你越绕它越跟着——它是唯一想跟你交朋友的，表达方式是咬。"),
        Later("M110", "幽暗织网者", "jungle_310", "精英", "翡翠秘境·精英关",
            "江湖人称「老网管」：躲在暗处织网，网又黏又结实。织好就搬个小板凳坐着等——你是今天第几个撞上来的？它心里有本账。"),
        Later("M111", "灼阳花", "jungle_311", "首领", "翡翠秘境·首领关",
            "江湖人称「小太阳」：自带暖意，越晒越精神。靠近它像贴了个暖宝宝，关不掉的那种——冬天想抱，夏天绕着走。"),
        Later("M112", "秘境骇兽", "jungle_312", "首领", "翡翠秘境·首领关",
            "江湖人称「尖叫怪」：藏在深处的胆小鬼，靠尖叫吓人。物理伤害不高，心理阴影满分——你一回头，它比你还慌。"),

        // —— 幽冥墓园（第二章）——
        Later("M201", "幽冥幼灵", "undead_201", "普通", "幽冥墓园·普通关",
            "江湖人称「见习幽灵」：刚上岗，飘功还差几个学分。经常飘着飘着就卡在半空，吓人全靠配音，一开口还是奶音——目前吓哭冒险者业绩为零。"),
        Later("M202", "骷髅新兵", "undead_202", "普通", "幽冥墓园·普通关",
            "江湖人称「小酥骨」：一身酥脆，骨架还没拼利索。跑两步掉根肋骨，边跑边捡——一半时间打仗，一半时间捡自己。"),
        Later("M203", "骷髅巨人", "undead_203", "普通", "幽冥墓园·普通关",
            "江湖人称「白骨高层」：一堆骨头摞起来的大个子，墓园管理层。气势很足，一推就散，散了也得自己爬回去重拼——形象工程不能塌。"),
        Later("M204", "装甲骷髅巨人", "undead_204", "普通", "幽冥墓园·普通关",
            "江湖人称「铁罐头」：给自己套了一身铁皮，安全感拉满。防御到位，机动随缘：迈一步歇三步，连转身都要提前请假。"),
        Later("M205", "绿皮僵尸", "undead_205", "普通", "幽冥墓园·普通关",
            "江湖人称「夜班尸」：双脚并拢往前蹦，常驻夜班。蹦得慢但胜在执着，认准你就蹦到天荒地老——它也想调白班，但墓园只开夜场。"),
        Later("M206", "骷髅士兵", "undead_206", "普通", "幽冥墓园·普通关",
            "江湖人称「白骨哨兵」：站得笔直，纪律刻进骨头里。你打它一拳，它先立正、再还手。守墓守到变成骷髅，也没等到下班的号声。"),
        Later("M207", "幼年食尸鬼", "undead_207", "普通", "幽冥墓园·普通关",
            "江湖人称「干饭鬼」：刚入行，见啥啃啥，经常啃到自己爪子。梦想是成为宗师——目前还在干饭第一年。"),
        Later("M208", "地狱犬", "undead_208", "精英", "幽冥墓园·精英关",
            "江湖人称「三头汪」：三个脑袋三个主意。还没开打它先自己吵起来：左头要咬，右头要睡，中间那个只会汪。你赢在它内讧。"),
        Later("M209", "嗜血食尸鬼", "undead_209", "精英", "幽冥墓园·精英关",
            "江湖人称「干饭宗师」：啃遍墓园的老饕，吃出了段位。下嘴前还要点评：火候差了。干饭鬼管它叫师傅，它嫌徒弟吵。"),
        Later("M210", "招魂师", "undead_210", "精英", "幽冥墓园·精英关",
            "江湖人称「喊魂的」：站在后排扯嗓子喊「回来吧」，嗓门比法术狠。上一份工作是卖糖葫芦的，吆喝功底全用上了。"),
        Later("M211", "摄魂使", "undead_211", "首领", "幽冥墓园·首领关",
            "江湖人称「收魂人」：专收魂儿，长得像会走路的黑洞，收完还打个饱嗝。最近在搞业绩考核：每周一百个魂，少一个加班。"),
        Later("M212", "墓园仲裁者", "undead_212", "首领", "幽冥墓园·首领关",
            "江湖人称「判官大人」：飘在空中给人「判生死」，判词写得很随意：今日宜挨打。判完还亲自下来执行——流程闭环。"),

        // —— 海岛遗迹（第五章）——
        Later("M301", "赤钳挖掘者", "sea_501", "普通", "海岛遗迹·普通关",
            "江湖人称「挖挖蟹」：举着大红钳子的工程蟹，见滩就挖。你站旁边？施工范围包含你，连你一起埋。"),
        Later("M302", "独角奔袭者", "sea_502", "普通", "海岛遗迹·普通关",
            "江湖人称「冲锋号」：头上一根角，低头就冲。刹车全靠撞墙——没墙就撞你。撞完自己先晕，你没受伤，它倒先躺了。"),
        Later("M303", "硬壳护卫蟹", "sea_503", "普通", "海岛遗迹·普通关",
            "江湖人称「前台蟹」：海滩口的「前台接待」，一身硬壳，笑脸迎人。它笑得越客气你越走不了——它的 KPI 就是你。"),
        Later("M304", "海洋猎手", "sea_504", "普通", "海岛遗迹·普通关",
            "江湖人称「海钓佬」：站在浅滩扔鱼叉，准头极佳。它眼里没有冒险者，只有今天业绩——你就是那条大鱼。"),
        Later("M305", "怒刺珊瑚", "sea_505", "普通", "海岛遗迹·普通关",
            "江湖人称「刺儿头」：一身尖刺，脾气比刺还硬。不动则已，一动浑身刺全飞出来。谁碰谁挨扎，海里第一刺儿头。"),
        Later("M306", "独眼巨蚌", "sea_506", "普通", "海岛遗迹·普通关",
            "江湖人称「蚌住了」：独眼大贝壳，平时闭着壳装死。你一走近它就「啪」地夹住——它绷得住，你蚌得住吗？"),
        Later("M307", "鹦鹉缠绕者", "sea_507", "普通", "海岛遗迹·普通关",
            "江湖人称「复读鹦」：飞在半空爱缠人，缠住就不撒嘴，还学你说话。你说「放开我」，它也「放开我」——气死人的复读机。"),
        Later("M308", "剧毒水母", "sea_508", "精英", "海岛遗迹·精英关",
            "江湖人称「毒奶茶」：飘着的毒水球，看着人畜无害。沾上一滴就「浑身发绿」——海里唯一不提供去冰选项的。"),
        Later("M309", "深海魅妖", "sea_509", "精英", "海岛遗迹·精英关",
            "江湖人称「迷魂鱼」：海里的「颜值担当」，歌声魅惑人心。听着听着就走不动道，等回过神，装备已经被它顺走。"),
        Later("M310", "海狼", "sea_510", "精英", "海岛遗迹·精英关",
            "江湖人称「浪里狼」：能在水里「飞」的海中恶狼，成群出没。单打独斗它一般，来一群你就得祈祷自己游得快。"),
        Later("M311", "三叉戟鱼人", "sea_511", "首领", "海岛遗迹·首领关",
            "江湖人称「掷戟手」：扛三叉戟的鱼人，投叉前要先摆造型——这工夫够你跑出二里地。准头极佳，专叉脚后跟，讲武德。"),
        Later("M312", "深海之王", "sea_512", "首领", "海岛遗迹·首领关",
            "江湖人称「深海大佬」：这片海说了算的老大，出场自带排面。一动手就是「海底地震」级别。口头禅：不好意思，习惯了。"),

        // —— 晨曦草原（第四章）——
        Later("M401", "长老史莱姆", "field_401", "普通", "晨曦草原·普通关",
            "江湖人称「史莱总」：洞里资格最老的史莱姆。走路慢、说话慢，撞人倒是挺快。管理理念：慢就是稳，稳就是撞。"),
        Later("M402", "吐泡水母", "field_402", "普通", "晨曦草原·普通关",
            "江湖人称「泡泡机」：爱吐泡泡的水球，泡泡里包着能炸的水。它自己觉得挺好玩——草原限定款，不支持退换。"),
        Later("M403", "赤翼飞蛾", "field_403", "普通", "晨曦草原·普通关",
            "江湖人称「红扑棱」：见光就往上撞的红飞蛾。不咬人，就是成群结队糊你一脸——打它还得抬头，累的是脖子，烦的是心。"),
        Later("M404", "贪食宝箱", "field_404", "普通", "晨曦草原·普通关",
            "江湖人称「压岁箱」：吃太胖的宝箱，箱盖都合不上。凑近它就「啪」地合上——给你压个「岁」。开箱有风险，伸手需谨慎。"),
        Later("M405", "圆胖兽", "field_405", "普通", "晨曦草原·普通关",
            "江湖人称「小胖墩」：圆滚滚，走一路滚一路。撞人不疼，就是赖上你之后甩不掉——洞里的伙食太好，全是糖分。"),
        Later("M406", "青绿小妖", "field_406", "普通", "晨曦草原·普通关",
            "外号就是本号：绿油油的一小坨，洞里最淡定。别人打架它看热闹，你打它它就瞪你一眼，那眼神很「你礼貌吗」。洞里唯一不加班的。"),
        Later("M407", "洞穴监视者", "field_407", "普通", "晨曦草原·普通关",
            "江湖人称「天眼仔」：悬在半空的「摄像头」，你往哪走它往哪飘。不报告也不攻击，就纯看——看得你发毛。"),
        Later("M408", "迷途行者", "field_408", "精英", "晨曦草原·精英关",
            "江湖人称「路痴」：在洞里转了三年还没出去。见人就问「出口在哪」，问完又忘了路。它打架是因为急——恼羞成怒的那种急。"),
        Later("M409", "洞穴守卫", "field_409", "精英", "晨曦草原·精英关",
            "江湖人称「门禁大爷」：守着洞口，谁进都得先登记。记性不太好：你签过名它转头就忘，于是又拦你一次。大爷的门禁比银行还严。"),
        Later("M410", "洞穴督工", "field_410", "精英", "晨曦草原·精英关",
            "江湖人称「包工头」：戴着安全帽，见你就喊「干活了！」。把打架当施工，抡家伙一板一眼——问题是这工地根本没有活，它就是闲的。"),
        Later("M411", "劳达工兵", "field_411", "首领", "晨曦草原·首领关",
            "江湖人称「劳达师傅」：洞里最能干的老实员工，天天扛石头。口头禅：这点活不算活。腰是有点疼，但活不能停。"),
        Later("M412", "洞穴沉睡者", "field_412", "首领", "晨曦草原·首领关",
            "江湖人称「起床气」：睡了几百年，被你吵醒了。它现在的状态就是它的名字——边打边打哈欠：我不想醒，但你别想跑。"),

        // —— 巨岩深窟（第六章）——
        Later("M501", "剧毒魔爪", "cave_601", "普通", "巨岩深窟·普通关",
            "江湖人称「黑手怪」：一只成精的毒爪子，见啥挠啥，挠完还得意地搓搓手。它就是传说中那只「背后的黑手」，字面意思。"),
        Later("M502", "短角幼魔", "cave_602", "普通", "巨岩深窟·普通关",
            "江湖人称「小犄角」：头上刚冒出小犄角，特别在意自己的角。你多看两眼它就冲过来——它以为你在笑它角短。角短也能顶人。"),
        Later("M503", "魔化黏浆", "cave_603", "普通", "巨岩深窟·普通关",
            "江湖人称「泼墨怪」：一滩黏糊糊的黑色魔浆，远程糊人，糊上就甩不掉。它最烦别人说它像水泥——虽然确实有点像。"),
        Later("M504", "深渊魔眼", "cave_604", "普通", "巨岩深窟·普通关",
            "江湖人称「大眼瞪」：会飞的大眼珠，瞪人专业户。它不眨眼，你也不好意思眨——谁先眨谁输。它还近视，射歪了更气。"),
        Later("M505", "呆滞巨人", "cave_605", "普通", "巨岩深窟·普通关",
            "江湖人称「慢半拍」：块头大、反应慢。你打它三下，它第四下才想起来还手。别急，它没凉——它只是在加载。"),
        Later("M506", "肉甲士兵", "cave_606", "普通", "巨岩深窟·普通关",
            "江湖人称「肉盾兵」：一身肥肉当铠甲，打着打着就往前蹭。不图伤害，就图把你堵在墙角——堵门这项业务，它从不掉线。"),
        Later("M507", "独眼射手", "cave_607", "普通", "巨岩深窟·普通关",
            "江湖人称「闭眼准」：只有一只眼，眯起来瞄准，准得离谱。名言：少一只眼，看得更清楚——你居然觉得有点道理。"),
        Later("M508", "暗影法师", "cave_608", "精英", "巨岩深窟·精英关",
            "江湖人称「卡壳法师」：裹着黑袍装神秘，念咒先「嗯哼」清嗓子，念到一半还卡壳。你冲得够快它就慌——慌了就念错，念错就炸自己。"),
        Later("M509", "恶地毒射手", "cave_609", "精英", "巨岩深窟·精英关",
            "江湖人称「冷箭手」：蹲在暗处放冷箭，箭上淬了毒。你找不着它，但它的箭总能找到你。最讨厌晴天——太阳一晒毒就挥发，业绩打折。"),
        Later("M510", "巨舌魔", "cave_610", "精英", "巨岩深窟·精英关",
            "江湖人称「舔舔怪」：逮啥舔啥，一口下去黏糊糊。掉血事小，那种黏腻感能记一年——深窟卫生评比，它年年垫底。"),
        Later("M511", "深窟剔骨师", "cave_611", "首领", "巨岩深窟·首领关",
            "江湖人称「骨科大夫」：深窟里的「手艺人」，专攻骨头，手法利落。干活时不许人打扰——放心，不疼，就是会被摆成很奇怪的姿势。"),
        Later("M512", "魔灵监视者", "cave_612", "首领", "巨岩深窟·首领关",
            "江湖人称「魔界无人机」：飘在高空的眼线，专门盯梢打小报告。打不着、甩不掉、还不用充电——魔界最强考勤设备。"),

        // —— 赤焰炼狱（第七章）——
        Later("M601", "毒史莱姆", "devil_701", "普通", "赤焰炼狱·普通关",
            "江湖人称「过期史莱」：生产日期不详，保质期早过了——浑身是毒还冒泡。它自己没察觉，还往你身上蹭。"),
        Later("M602", "异种弹跳者", "devil_702", "普通", "赤焰炼狱·普通关",
            "江湖人称「弹簧魔」：被改造得只会蹦，蹦得又高又远，落地自带冲击波。实验室最失败的产物：忘了装刹车。"),
        Later("M603", "异种拘束者", "devil_703", "普通", "赤焰炼狱·普通关",
            "江湖人称「打包魔」：吐着黏丝，专把人捆成粽子。捆得很仔细，还确认一下「紧不紧」——然后补两圈，打上蝴蝶结。"),
        Later("M604", "异种狂怒者", "devil_704", "普通", "赤焰炼狱·普通关",
            "江湖人称「血压魔」：一言不合就暴走，浑身通红，血压常年爆表。你越躲它越气——战斗力全靠怒气，建议直接揍。"),
        Later("M605", "异种火法师", "devil_705", "普通", "赤焰炼狱·普通关",
            "江湖人称「打火魔」：火球一个接一个，喷得畅快，就是偶尔烧到自己——炼狱消防记录保持者：它自己。"),
        Later("M606", "多头异犬", "devil_706", "普通", "赤焰炼狱·普通关",
            "江湖人称「内讧犬」：三个脑袋抢食，谁也不让谁。它们最大的敌人不是你，是彼此——你只要负责看戏。"),
        Later("M607", "石化魔像", "devil_707", "普通", "赤焰炼狱·普通关",
            "江湖人称「移动城墙」：一身石化外壳，硬得像城墙。撞上你，你就知道「稳」字怎么写——会走路的城墙，拆都拆不动。"),
        Later("M608", "异种看守者", "devil_708", "精英", "赤焰炼狱·精英关",
            "江湖人称「查票的」：炼狱门口的保安，没票就是不让进——哪怕你把票吃了它也得搜。眼神不太好，经常拦错人，比如自己的同事。"),
        Later("M609", "异种巡视者", "devil_709", "精英", "赤焰炼狱·精英关",
            "江湖人称「警报精」：空中巡逻，发现你就拉警报，动静能把整个炼狱的怪都喊来。警报是它自己唱的——跑调，但音量补齐了。"),
        Later("M610", "炼狱飞魔", "devil_710", "精英", "赤焰炼狱·精英关",
            "江湖人称「俯冲大队」：成群飞舞，遮天蔽日。单个不厉害，一堆一起俯冲你就找不到躲的方向——专往下扔东西，包括它们自己的同伴。"),
        Later("M611", "异种大长老", "devil_711", "首领", "赤焰炼狱·首领关",
            "江湖人称「喊口号的」：异种们的「精神领袖」，拿着破杖瞎指挥。名言：裂隙说，今天适合加班。——小弟们还真就冲了。"),
        Later("M612", "异种主宰", "devil_712", "首领", "赤焰炼狱·首领关",
            "江湖人称「异种老板」：所有异种的老大，飘在空中俯视众生。你抬头看它太久，它就扔个火球下来——「看什么看」，老板脾气都这样。"),

        // —— 永霜雪境（第八章）——
        Later("M701", "寒冰之灵", "ice_801", "普通", "永霜雪境·普通关",
            "江湖人称「冰疙瘩」：成了精的冰溜子，滑不溜秋。你抓它十次，它溜走十一次——最后一次它嫌你烦，自己撞上来了。"),
        Later("M702", "游荡雪人", "ice_802", "普通", "永霜雪境·普通关",
            "江湖人称「胖雪人」：胡萝卜鼻子、树枝胳膊。笑眯眯的，可一巴掌拍下来，你就成了雪地里一块「人形冰砖」。微笑只是一种表情。"),
        Later("M703", "凛冬雪精", "ice_803", "普通", "永霜雪境·普通关",
            "江湖人称「雪钻风」：会飞的小雪球精，最爱往人脖子里钻。那一下的冰爽能让你从头凉到脚——它还在旁边笑，笑起来掉冰渣。"),
        Later("M704", "悬空雪卵", "ice_804", "普通", "永霜雪境·普通关",
            "江湖人称「待孵蛋」：一颗会飞的雪蛋，谁也不知道它肚子里是什么——它自己也不知道。所以一天到晚到处撞，扔雪球倒是很在行。"),
        Later("M705", "冰锥投掷者", "ice_805", "普通", "永霜雪境·普通关",
            "江湖人称「高空抛冰」：从高空扔冰锥，准头极好，手特别冷。你抬头看它，它朝你扔；你不看，它还是朝你扔。屡教不改。"),
        Later("M706", "冰盾爬行者", "ice_806", "普通", "永霜雪境·普通关",
            "江湖人称「冰盾龟」：背着一面冰盾，正面几乎打不动。它慢吞吞转过身来的时候，就是你该跑的时候——背面也别想跑。"),
        Later("M707", "冰川胖兽", "ice_807", "普通", "永霜雪境·普通关",
            "江湖人称「冰胖墩」：胖墩墩，跑起来一颠一颠。撞人靠体重——被它压住，你就懂什么叫「冰凉的绝望」。刹车？没有这个配置。"),
        Later("M708", "冰风之刃", "ice_808", "精英", "永霜雪境·精英关",
            "江湖人称「冰刀子」：一片会自己飞的冰刀，快得像阵风。削人不见血——因为血都冻住了。冬天的风像刀子，说的就是它本尊。"),
        Later("M709", "冰霜长矛手", "ice_809", "精英", "永霜雪境·精英关",
            "江湖人称「冰标枪」：手持冰矛，站得笔直像个冰雕。投矛前先深吸一口气——趁这工夫，你最好已经不在原地了。"),
        Later("M710", "冰霜力士", "ice_810", "精英", "永霜雪境·精英关",
            "江湖人称「冰大力」：力气极大的冰巨人，一拳能砸出个坑。出拳慢，但你要是硬接，坑里就有你的位置——先躲，再嘲笑它。"),
        Later("M711", "冰川领主", "ice_811", "首领", "永霜雪境·首领关",
            "江湖人称「冰霸王」：走路带着冰渣子，不讲道理，就看谁块头大——很遗憾，它比你大。威风得很，直到打滑摔一跤。"),
        Later("M712", "冰之守护神", "ice_812", "首领", "永霜雪境·首领关",
            "江湖人称「雪山守门员」：雪境最高处的守护神，浑身冰晶。它守的不是宝藏，是「谁也别想从这里过去」——包括你。零封率 100%。"),
    };

    public static readonly MercEntry[] Mercs =
    {
        Npc("C001", "艾丽娅", "npc_xiaomei", "青梅竹马 / 失踪小队队长",
            "绿色衣服、棕色双马尾的少女，法系冒险者，既能用法术输出也能为队友恢复生命。",
            "她离开前借了你三枚铜板买面包，至今没还。公会没把这个写进失踪报告。",
            MercRosterDefs.MercRarity.Rare),
        Npc("C002", "阿尔托", "npc_shengdian", "圣殿骑士",
            "金发、红缨头盔、持剑盾的年轻骑士，把荣耀挂在嘴边。",
            "他的盔甲擦得比公会的地板还亮。老盾怀疑他晚上穿着盔甲睡觉。"),
        Npc("C003", "格雷", "", "老猎人",
            "头发花白、披着旧斗篷的老练猎人，艾丽娅小队成员。",
            "据说他闻一口空气就能判断附近有没有精英怪。也能判断食堂今天有没有肉。"),
        // 2026-10-20 主人拍板：独眼不是佣兵，只在酒馆出现，不进佣兵图鉴
        Npc("C004", "独眼", "npc_duyan", "酒馆情报商",
            "戴眼罩的中年男子，常在酒馆角落低声交换情报。",
            "他知道公会很多事，但只换酒，不换金币。赛琳看见他会假装没看见。",
            hideInCodex: true),

        Hire("H001", "马库斯", "老盾", "dunbing101", "剑盾卫士", "新手引导自动入队", "暮影森林·教学关",
            "手持大盾、全身重甲的中年佣兵，被玩家从怪物包围中救出。擅长挡刀和吐槽。",
            "他的盾牌上刻着前任主人的名字。他说是自己，只是字磨花了。"),
        Hire("H002", "洛恩", "铁皮", "dunbing102", "剑盾卫士", "完成新手引导后酒馆可招募", "酒馆",
            "比老盾年轻一点的盾兵，盔甲更亮，但实战经验更少。",
            "老盾总说他‘盾举得太高，挡住自己的视线’。洛恩不服，直到撞上一棵树。"),
        Hire("H003", "塔克", "重盾", "dunbing201", "剑盾卫士", "招募入队后解锁", "酒馆",
            "比老盾还壮实的重装盾兵，移动慢，但几乎不会被击退。",
            "他一顿饭能吃三个人的量。公会食堂看见他来，会提前多蒸一锅。"),
        Hire("H004", "维克", "钢盾", "dunbing202", "剑盾卫士", "招募入队后解锁", "酒馆",
            "寡言的退伍士兵，盾牌上全是旧伤疤，从不主动提起过去。",
            "有人说他的盾救过会长的命。也有人说那道疤是被会长派人砍的。"),
        Hire("H005", "米娅", "小樱", "gongshou101", "游侠", "完成新手引导后酒馆可招募", "酒馆",
            "粉发弓箭手，动作轻快，箭无虚发——至少她是这么说的。",
            "她自称‘百步穿杨’，但有一次射中了老盾的盾牌。老盾安慰她：‘也算命中。’"),
        Hire("H006", "希尔", "鹰眼", "gongshou201", "游侠", "招募入队后解锁", "酒馆",
            "戴兜帽的沉稳弓箭手，话少箭多，据说以前做过边境巡林人。",
            "他从不笑。有人打赌他笑一次请全队喝酒，目前欠账已经够买一把弓。"),
        Hire("H007", "布罗克", "大锤", "kuangzhan101", "狂战士", "完成新手引导后酒馆可招募", "酒馆",
            "手持巨型战斧的红角战士，攻击范围大，脾气和斧子一样直。",
            "他每次挥斧都会喊招式名。虽然招式名都是他自己编的，但气势很足。"),
        Hire("H008", "古恩", "斩铁", "kuangzhan102", "狂战士", "完成新手引导后酒馆可招募", "酒馆",
            "独眼狂战士，专砍重甲敌人，据说他的左眼是被自己的斧头弹片伤的。",
            "他讨厌螃蟹。所有带壳的。尤其是吃饭时咬不动的。"),
        Hire("H009", "莫丁", "碎岩", "kuangzhan201", "狂战士", "招募入队后解锁", "酒馆",
            "使用双斧的矿石工人，退休后转职佣兵，对石头有执念。",
            "他判断敌人强不强，先看对方长得像不像矿石。首领怪在他眼里都是‘宝石’。"),
        Hire("H010", "凯恩", "狂牙", "kuangzhan202", "狂战士", "招募入队后解锁", "酒馆",
            "披兽皮、戴兽牙项链的野性战士，攻击时会发出怪叫。",
            "他的怪叫其实是为了壮胆。老盾知道，但从不拆穿。"),
        Hire("H011", "路加", "路加", "naima101", "牧师", "完成新手引导后酒馆可招募", "暮影森林·教学关",
            "持法杖的白衣治疗者，新手冒险者最喜欢的队友类型。",
            "她的治疗术是公会同级考试第一名。缺点是念咒时不能被打断，否则会奶到怪物。"),
        Hire("H012", "塞拉", "蓝之泪", "naima102", "水系法师", "招募入队后解锁", "酒馆",
            "蓝发的‘少年’，束胸绑发、压着嗓子说话，一手短杖一手小盾，袖口总有洗不掉的水渍。",
            "没人知道「塞拉」其实是哥哥的名字——兄长随佣兵团进了裂隙再没回来，她揣着发黄的画像一路找下来；男孩子好组队，她便顶着哥哥的名字束胸绑发扮成少年。老盾第一眼就看穿，却从不说破，夏天照旧偷偷靠着她那截凉凉的杖头乘凉。"),
        Hire("H013", "莫娜", "紫晶", "fashi202", "雷系法师", "招募入队后解锁", "酒馆",
            "紫发法师，能召唤小型闪电，表情总是很不耐烦。",
            "她觉得见习冒险者问题太多。但如果你请她喝酒，她能把你想知道的全倒出来。"),
        Hire("H014", "伊芙", "火舞", "fashi201", "火系法师", "招募入队后解锁", "酒馆",
            "红发法师，脾气和火焰一样烈，擅长范围灼烧。",
            "她不许别人碰她的袍子角。上次有人踩到，那根火把三天没灭。"),
        Hire("H015", "艾拉", "风羽", "gongshou101", "游侠", "完成新手引导后酒馆可招募", "酒馆",
            "棕色短发的游侠，箭袋里永远留着一支没名字的羽箭。",
            "她说那支箭要留给‘配得上的人’。目前老盾的盾牌得分最高。"),
        Hire("H016", "杜娅", "怒角", "kuangzhan201", "狂战士", "招募入队后解锁", "酒馆",
            "红角狂战士，双斧挥砍时像一阵红色旋风。",
            "她开战前会整理护腕。老盾说那是她唯一的仪式感。"),
        Hire("H017", "莉娜", "圣光", "naima102", "牧师", "招募入队后解锁", "酒馆",
            "金发白衣牧师，治疗时嘴里念念有词。",
            "她念的是炖菜配方。据她说，步骤和祷告一样能让人安心。"),
        Hire("H018", "布朗", "铁壁", "dunbing101", "剑盾卫士", "完成新手引导后酒馆可招募", "酒馆",
            "年轻寡言的盾兵，盾牌上贴着一张旧报销单。",
            "他说欠公会钱的人命硬，所以把报销单当护身符。"),
        Hire("H019", "艾琳", "星火", "fashi101", "法师", "完成新手引导后酒馆可招募", "酒馆",
            "戴兜帽的初级法师，发色偏紫，手持短杖。看起来只是学徒，但杖尖偶尔溢出的魔力让老盾都往后退。",
            "她总说自己是'刚入门的法师'。但上次她不小心把营地篝火变成了火球。"),
        Hire("H020", "凯尔", "谜面", "fashi102", "法师", "招募入队后解锁", "酒馆",
            "浑身裹在深蓝兜袍里的神秘法师，从不露脸，声音像从很远的地方传来。",
            "有人打赌他兜帽下没有脸。老盾说'没脸的人不会点酒'，但他确实每次都点。"),
        Hire("H021", "格拉克斯", "懒鬼", "zhongzhan101", "重武者", "完成新手引导后酒馆可招募", "酒馆",
            "戴着露出半张脸头盔的重武者，扛着长柄巨斧，脸上写满了'随便吧'。",
            "他参战是因为'懒得找工作'。但真打起来，他懒得逃跑，所以总是站到最后。"),
        Hire("H022", "索尔", "铁面", "zhongzhan201", "重武者", "招募入队后解锁", "酒馆",
            "头盔遮住整张脸的重武者，手持短柄巨斧，信奉实力代表一切。",
            "他从不摘盔。酒馆里有人说他其实是女的，有人说他其实是骷髅，没人敢当面问。"),
        Hire("H023", "加百列", "赤壁", "dunbing203", "剑盾卫士", "招募入队后解锁", "酒馆",
            "酒红袍的年轻教团卫士，白金十字塔盾比人还高，开口先引用见习守则。",
            "守则是他自己写的，一共十七条，第十八条永远在拟。老盾看完只回了句：先学会睡踏实觉。"),
    };

    /// <summary>
    /// 成就表。第 5 列是「奖励」文案，必须与 <see cref="AdventureLogAchievements.GetReward"/> 的 Label 一致。
    /// 【2026-10-31 主人拍板「先改文案不承诺」】A007/A014/A015 原来写的是
    /// 背包扩容+1 / 限定头像框 / 老盾专属皮肤「锈迹盾卫」——
    /// 这三套系统（背包扩容字段、unlockedFrameIds、unlockedSkinIds）都还没做，
    /// 发出去玩家什么都看不到，故文案先改成「（暂无奖励）」，等系统做出来再改回真奖励名。
    /// A012「称号『森林无伤者』」已在 2026-10-31 做完称号系统，文案恢复真奖励名（见 TitleSystem / PlayerTitleHud）。
    /// </summary>
    public static readonly AchEntry[] Achievements =
    {
        A("A001", "见习冒险者", "成长", "完成新手引导", "金币 ×10", "你拿到了见习徽章，虽然它看起来随时会掉色。"),
        A("A002", "第一次撤离", "战斗", "首次从裂隙中撤离", "金币 ×5", "活着才有收益，公会这句话倒不是骗人的。"),
        A("A003", "第一次阵亡", "战斗", "首次在裂隙中死亡", "天赋石 ×3", "金币没了，但装备和材料还在。记住这个教训。"),
        A("A004", "森林清道夫", "战斗", "在暮影森林累计击败 100 只怪物", "金币 ×200", "森林层的怪物看见你都会绕路。"),
        A("A005", "精英猎手", "战斗", "首次击败精英怪物", "金币 ×100", "精英怪不掉好装备，但它们掉的装备比普通怪好一点。"),
        A("A006", "首杀首领", "战斗", "首次击败森之守护者", "天赋石 ×3", "你砍倒了第一章的守门人。但门后面还有更多。"),
        A("A007", "装备收藏家", "收集", "累计拾取 50 件装备", "（暂无奖励）", "你的背包开始发出金属碰撞的声音。"),
        A("A008", "强化入门", "养成", "首次强化装备", "天赋石 ×5", "把材料砸进装备里，是冒险者最朴素的仪式感。"),
        A("A009", "酒馆常客", "养成", "首次在酒馆招募佣兵", "金币 ×15", "一个人下本太危险，带个能挡刀的。"),
        A("A010", "裂隙探索者", "探索", "累计进入裂隙 10 次", "天赋石 ×5", "你下裂隙的次数已经比回公会大厅还多了。"),
        A("A011", "千金在手", "经济", "单局携带金币达到 1000", "金币 ×50", "有钱人的烦恼是：到底要不要撤离？"),
        A("A012", "完美首通", "挑战", "无伤通关第一章普通难度", "称号「森林无伤者」", "没有怪物能碰到你，包括那只树灵。"),
        A("A013", "困难挑战者", "挑战", "通关第一章困难难度", "天赋石 ×5", "困难难度的怪物不会更聪明，只会更不讲理。"),
        A("A014", "噩梦先驱", "挑战", "通关第一章噩梦难度", "（暂无奖励）", "能活着走出噩梦难度的人，公会会记住你的名字。"),
        A("A015", "老盾的伙伴", "社交", "老盾累计参战 20 次", "（暂无奖励）", "他说他的盾跟你一样旧了，但还能用。"),
    };

    public static readonly WorldEntry[] World =
    {
        W("W001", "埃索斯大陆", "世界观", "首次进入游戏",
            "玩家所在的奇幻大陆。古语中“Aes-oth”意为“从伤口呼吸的土地”。",
            "世界不是平的，是裂开的。"),
        W("W002", "裂隙", "世界观", "首次进入战斗",
            "连接异世界的空间裂口，越往深处越危险。",
            "裂隙不是门，是伤口。每次有人跳进去，伤口就痒一下。"),
        W("W003", "皇家冒险者公会", "组织", "进入城镇后解锁",
            "表面上保护王国，实际上高层知晓裂隙真相并牺牲优秀冒险者以延缓裂隙扩张。",
            "公会大厅灯火通明，像是什么都没发生过。"),
        W("W004", "暮影森林", "地点", "进入第一章后解锁",
            "新人练手的第一处裂隙层，森林因裂隙能量而扭曲。",
            "这里的树会动，只是动作很慢，慢到你以为只是风吹。"),
        W("W005", "仙泉", "地点", "通关第一章后解锁",
            "裂隙中偶尔出现的恢复节点，能恢复冒险者生命。",
            "仙泉不治病，它只借给你一点命，记得还。"),
        W("W006", "见习徽章", "物品", "完成新手引导",
            "新人冒险者身份的象征，剧情中玩家最终将其扔回会长桌上。",
            "徽章很轻，但戴着它的时候，总觉得有点喘不过气。"),
        W("W007", "委托书", "物品", "进入城镇后解锁",
            "公会派发任务的书面凭证，新手引导中连名字都没填。",
            "那不是任务，是一张让你闭嘴的纸。"),
        W("W008", "空洞之喉", "传说", "解锁 W001、W002、W003 后自动解锁",
            "远古灾兽，诸神为将其放逐而撕裂世界皮肤，封入大地深处。",
            "它还在呼吸。每一次裂隙扩张，都是它的哈欠。"),
    };

    public static readonly StoryEntry[] Main =
    {
        new StoryEntry
        {
            Id = "P0", Title = "序章 见习者的第一天", Unlock = "完成新手引导",
            Summary = "玩家加入皇家冒险者公会，会长派发森林层委托。老盾作为临时队友入队，赛琳传授三条下裂隙规则。",
            Extra = "新人，森林层那帮怪物最近闹得凶。下去走一趟，能不能留下，看你自己的造化。"
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
            Summary = "穿过浆果怪聚集区与森林史莱姆湿地，老盾在营地休息时提起自己第一次下裂隙的糗事。",
            Extra = "我第一次下裂隙，比你还紧张。至少你没把火把扔进蘑菇堆。"
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
            Id = "C1F", Title = "第一章·六 石碑残片", Unlock = "通关第一章后解锁",
            Summary = "森林深处出现一座破损石碑，上面刻着三种古老符文。玩家选择触摸其中一种，影响后续对话倾向与属性加成。",
            Extra = "选一条你认为正确的路。公会不在乎过程，只在乎结果。"
        },
        new StoryEntry
        {
            Id = "C1G", Title = "第一章·七 森之守护者", Unlock = "首次击败森之守护者",
            Summary = "石碑后方正是森之守护者栖身地。击败首领后，森林暂时恢复平静，但裂隙的躁动并未停止。",
            Extra = "她还活着，一定来过这里。 / 这剑痕……不能确定是谁。 / 不管是谁，先活下去再说。"
        },
        new StoryEntry
        {
            Id = "C1Z", Title = "第一章·尾声 归来的见习者", Unlock = "通关第一章普通难度",
            Summary = "回到公会提交委托，会长态度微妙。赛琳偷偷塞给玩家一张写有艾丽娅名字的纸条。",
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
            Id = "S002", Title = "咨询台的歉意", Unlock = "通关第一章后解锁",
            Summary = "赛琳欲言又止，暗示森林层的任务并不只是“清理怪物”，有人在借委托之名掩盖裂隙扩张。",
            Extra = "奖励：天赋石 ×1"
        },
        new StoryEntry
        {
            Id = "S003", Title = "格雷的笔记", Unlock = "通关第一章后解锁",
            Summary = "捡到一本破损的猎人笔记，署名格雷，记录了对裂隙异常的观察：怪物躁动、符文发光、有人深入裂隙未归。",
            Extra = "奖励：金币 ×800"
        },
        new StoryEntry
        {
            Id = "S004", Title = "阿尔托的剑痕", Unlock = "通关第一章后解锁",
            Summary = "石碑旁的岩石上有一道新鲜的圣殿骑士剑痕，阿尔托似乎刚离开不久。他为什么没回公会报告？",
            Extra = "奖励：金币 ×200"
        },
        new StoryEntry
        {
            // 【2026-10-24 口径改「消费金币」；2026-10-31 商店整体删除】不再写死「在商店」——
            // 当前唯一金币出口是酒馆招募（ShopUI / ShopSystem 已删），S005/S010 文案无需再改。
            Id = "S005", Title = "商人的第一笔生意", Unlock = "累计消费 1000 金币",
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
            Id = "S008", Title = "路加的炖菜配方", Unlock = "路加累计参战 10 次",
            Summary = "路加终于把治疗咒语和炖菜配方分清了，并送给玩家一份“吃了不会看见会长跳舞”的应急口粮。",
            Extra = "奖励：路加好感度 +10，应急口粮 ×3"
        },
        new StoryEntry
        {
            Id = "S009", Title = "失踪的猎人", Unlock = "完成「格雷的笔记」并找到笔记线索后解锁",
            Summary = "将格雷的笔记交给赛琳，她脸色变了，只说“这事不要问会长”。",
            Extra = "奖励：稀有装备箱 ×1"
        },
        new StoryEntry
        {
            Id = "S010", Title = "商人的老客户", Unlock = "累计消费 3000 金币",
            Summary = "商人开始跟你聊进货渠道，暗示酒馆地下可能有“另一份价目表”。",
            Extra = "奖励：高级生命药水 ×2，金币 ×300"
        },
        new StoryEntry
        {
            Id = "S011", Title = "森林植物图鉴", Unlock = "在暮影森林累计击败 20 只怪物",
            Summary = "公会学者委托收集森林植物样本，完成图鉴后他对“红浆果能不能吃”保持沉默。",
            Extra = "奖励：天赋石 ×1，金币 ×150"
        },
        new StoryEntry
        {
            Id = "S012", Title = "精英猎手", Unlock = "累计击败 3 只精英怪物",
            Summary = "公会战士团认可你的实力，提供一份精英怪物弱点笔记。",
            Extra = "奖励：金币 ×1200"
        },
        new StoryEntry
        {
            Id = "S013", Title = "会长室的秘密", Unlock = "通关第一章困难难度后解锁",
            Summary = "会长室的门没关严，里面传出关于“空洞之喉”的低声争论。",
            Extra = "奖励：解锁世界条目 W008"
        },
        new StoryEntry
        {
            Id = "S014", Title = "艾丽娅的信物", Unlock = "完成 S004 与 S009 后解锁",
            Summary = "赛琳终于承认：艾丽娅小队不是失踪，是会长派他们去裂隙更深处执行任务。",
            Extra = "奖励：艾丽娅羁绊线索 ×1，金币 ×500"
        },
        new StoryEntry
        {
            Id = "S015", Title = "老盾的盾纹", Unlock = "老盾好感度达到 20",
            Summary = "老盾喝醉后揭开盾牌内侧的纹章——那是他失去的正式冒险者编号。",
            Extra = "奖励：老盾专属被动解锁"
        },
        new StoryEntry
        {
            Id = "S016", Title = "最初的裂隙", Unlock = "通关第一章噩梦难度后解锁",
            Summary = "一位神秘老人出现在酒馆角落，讲述诸神撕裂世界封印空洞之喉的传说，与石碑符文呼应。",
            Extra = "奖励：传奇装备箱 ×1"
        },
    };

    /// <summary>
    /// 里程碑条目：每章 1 条（共 8 条），展示该章累计已发天赋石与进度。
    /// 章名与 GameConfig.ChapterMapNames 一致（暮影森林 / 幽冥墓园 / 翡翠秘境 / 晨曦草原 /
    /// 海岛遗迹 / 巨岩深窟 / 赤焰炼狱 / 永霜雪境）。
    /// </summary>
    public static readonly MilestoneEntry[] Milestones = new MilestoneEntry[]
    {
        M(1, "暮影森林"), M(2, "幽冥墓园"), M(3, "翡翠秘境"), M(4, "晨曦草原"),
        M(5, "海岛遗迹"), M(6, "巨岩深窟"), M(7, "赤焰炼狱"), M(8, "永霜雪境"),
    };

    static MilestoneEntry M(int ch, string name) => new MilestoneEntry
    {
        Chapter = ch,
        Name = name,
        Title = "第 " + ch + " 章 · " + name,
        FirstClearStone = TalentDefs.TalentStoneRewards.FirstClearStone(ch),
        ChapterClearStone = TalentDefs.TalentStoneRewards.ChapterClearStone(ch),
    };

    /// <summary>某章里程碑进度：已首次通关关数 / 每章关数。</summary>
    public static int MilestoneStageProgress(int chapter)
    {
        return SaveSystem.Instance?.Data?.ClearedStageCountInChapter(chapter) ?? 0;
    }

    /// <summary>某章累计已发天赋石。【2026-10-20 主人拍板】每关首通石/整章首通石均停发，
    /// 天赋石只从精英/Boss 击杀概率掉落（战斗内即时入账，不在章节里程碑记账），固定返回 0。</summary>
    public static int MilestoneIssuedStones(int chapter)
    {
        return 0;
    }

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

    /// <summary>
    /// 按图鉴 Id（H001~H023 / C001~C004）查条目，找不到返回 false。
    /// 2026-09-17：酒馆名册与商店也要按 HireId 取简介/Lore，这里给一个统一入口，
    /// 免得两边各写一遍线性查找。
    /// </summary>
    public static bool TryFindMerc(string id, out MercEntry entry)
    {
        var all = Mercs;
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == id)
                {
                    entry = all[i];
                    return true;
                }
            }
        }
        entry = default(MercEntry);
        return false;
    }

    public static bool MercUnlocked(MercEntry e)
    {
        if (e.StoryNpc)
        {
            if (e.Id == "C001") return StoryProgress.TutorialIntroDone || StoryProgress.GetBond(StoryProgress.NpcXiaomei) > 0;
            // 【2026-11-01 主人拍板「只有出现过才会点亮」】删掉 || ChapterCleared(1) 兜底：
            // 之前第一章通关就直接点亮，哪怕全程没遇到。现在只认好感度（剧情真互动过），
            // 未出场前图鉴保持剪影。
            if (e.Id == "C002") return StoryProgress.GetBond(StoryProgress.NpcAltor) > 0;
            if (e.Id == "C003") return StoryProgress.GetBond(StoryProgress.NpcGrey) > 0;
            return StoryProgress.TutorialIntroDone;
        }
        if (e.Id == "H001") return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || HasMerc("dunbing101") || HasMerc("dunbing102");
        if (e.Id == "H011") return StoryProgress.TutorialBattleCleared || StoryProgress.TutorialDone || HasMerc("naima101");
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
            // 【2026-10-23 复核】此前误判「5 关制下 >=2 恒真」而删过该分支 —— 错：currentStageIndex 域 0..4，
            // >=2 在第 2/3/4 小关均可达，并非恒真。已回退原逻辑（保留章通关兜底不变）。
            return ChapterCleared(1) || (ChapterManager.Instance != null && ChapterManager.Instance.currentStageIndex >= 2);
        if (e.Id == "C1C")
            return AdventureLogAchievements.GetProgress("elite_kill") > 0 || ChapterCleared(1);
        if (e.Id == "C1D")
            // 【2026-10-23 复核】同 C1B：>=4 在 Boss 小关（idx=4）可达，非恒真 → 回退原逻辑
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

            // —— 叙事 V2.0：以下 8 条原先落到 default:return false，永久无法解锁 ——
            case "S005":
                return AdventureLogAchievements.GetProgress(ProgressShopSpend) >= 1000;
            case "S006":
                return AdventureLogAchievements.GetProgress("merc_battles_H005") >= 10;   // 米娅
            case "S007":
                return AdventureLogAchievements.GetProgress("merc_battles_H007") >= 10;   // 布罗克
            case "S008":
                return AdventureLogAchievements.GetProgress("merc_battles_H011") >= 10;   // 路加
            case "S009":
                return SideDone("S003") && (StoryClue.Has("C05") || StoryClue.Has("C20"));
            case "S010":
                return AdventureLogAchievements.GetProgress(ProgressShopSpend) >= 3000;
            case "S014":
                return SideDone("S004") && SideDone("S009");
            case "S015":
                return StoryProgress.GetBond(StoryProgress.NpcLaoDun) >= 20;
            default:
                return false;
        }
    }

    /// <summary>累计金币消费（商店已删，现只算酒馆招募）。需要在消费结算处调用 AddProgress(ProgressShopSpend, 金额)。</summary>
    public const string ProgressShopSpend = "shop_spend_total";

    /// <summary>佣兵参战次数进度键。需要在参战判定处按参战佣兵调用 AddProgress(key, 1)。</summary>
    public static string MercBattleKey(string mercAssetId)
    {
        return "merc_battles_" + mercAssetId;
    }

    static bool SideDone(string id)
    {
        var sides = SaveSystem.Instance?.Data?.completedSideIds;
        return sides != null && sides.Contains(id);
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
            Unlock = "后续裂隙层", Desc = "在后续裂隙层中可解锁。", Lore = lore, LaterChapter = true
        };
    }

    static MercEntry Npc(string id, string name, string asset, string role, string desc, string lore,
        MercRosterDefs.MercRarity rarity = MercRosterDefs.MercRarity.Common, bool hideInCodex = false)
    {
        return new MercEntry
        {
            Id = id, Name = name, Nickname = "", AssetId = asset, Role = role, Unlock = "主线推进",
            Place = "剧情", Desc = desc, Lore = lore, StoryNpc = true, Rarity = rarity,
            HideInCodex = hideInCodex
        };
    }

    static MercEntry Hire(string id, string name, string nickname, string asset, string role, string unlock, string place, string desc, string lore)
    {
        var rarity = MercRosterDefs.MercRarity.Common;
        if (MercRosterDefs.TryGetByHireId(id, out var def))
            rarity = def.Rarity;
        return new MercEntry
        {
            Id = id, Name = name, Nickname = nickname, AssetId = asset, Role = role, Unlock = unlock,
            Place = place, Desc = desc, Lore = lore, StoryNpc = false, Rarity = rarity
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
