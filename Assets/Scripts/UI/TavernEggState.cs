using UnityEngine;

// 注：不 using System——避免与 UnityEngine.Random 产生 CS0104 歧义，DateTime 走全限定名

/// <summary>
/// 酒馆彩蛋共享状态（2026-10-18 主人拍板「加彩蛋批次」）：
/// 老板娘好感度（摸头）、连败安慰计数、每日进店问候、老板娘台词池。
/// 全部落 PlayerPrefs，独立于业务存档；只服务彩蛋文案表现，不参与任何数值玩法。
/// </summary>
public static class TavernEggState
{
    // ———— 好感度（摸头） ————
    const string FavorKey = "tavern_landlady_favor";
    const string FavorDayKey = "tavern_landlady_favor_day";
    const string FavorDayClicksKey = "tavern_landlady_favor_day_clicks";

    /// <summary>每日摸头次数上限（防狂点刷数值；超限仍播动画/台词但不再加好感）。</summary>
    public const int DailyFavorClickLimit = 20;

    /// <summary>好感阶段阈值：0 初识 / 10 熟络 / 25 心腹 / 50 挚友。</summary>
    static readonly int[] StageThresholds = { 0, 10, 25, 50 };
    static readonly string[] StageNames = { "初识", "熟络", "心腹", "挚友" };

    public static int Favor => PlayerPrefs.GetInt(FavorKey, 0);

    public static int FavorStageIndex
    {
        get
        {
            int v = Favor;
            int idx = 0;
            for (int i = 0; i < StageThresholds.Length; i++)
                if (v >= StageThresholds[i]) idx = i;
            return idx;
        }
    }

    public static string FavorStageName => StageNames[FavorStageIndex];

    /// <summary>摸头加好感。返回 false = 今日已达上限（不加值）；跨阶段时 leveledUp=true。</summary>
    public static bool AddFavor(int delta, out bool leveledUp)
    {
        leveledUp = false;
        int today = TodayIndex();
        if (PlayerPrefs.GetInt(FavorDayKey, -1) != today)
        {
            PlayerPrefs.SetInt(FavorDayKey, today);
            PlayerPrefs.SetInt(FavorDayClicksKey, 0);
        }
        int clicks = PlayerPrefs.GetInt(FavorDayClicksKey, 0);
        if (clicks >= DailyFavorClickLimit) return false;
        PlayerPrefs.SetInt(FavorDayClicksKey, clicks + 1);

        int before = FavorStageIndex;
        PlayerPrefs.SetInt(FavorKey, Mathf.Min(999, Favor + delta));
        leveledUp = FavorStageIndex > before;
        return true;
    }

    // ———— 摸头台词（按好感阶段递进）————
    // 【2026-10-18 主人拍板】老板娘人设＝豪放爽朗、开放自来熟：摸头不扭捏，大大方方开玩笑
    static readonly string[][] HeadPatLinesByStage =
    {
        new[] { "哟～胆子不小，敢摸老板娘的头？", "哈哈，手感好吧？记酒钱上！" },
        new[] { "摸吧摸吧，今天心情好！", "你这人还挺顺眼，摸摸不收钱。" },
        new[] { "哈哈哈来来来，随便摸！", "全酒馆就你有这待遇，懂？" },
        new[] { "我的头就交给你了，客人！", "裂隙那边凶险，活着回来，老娘请你喝最好的酒！", "独眼那人，嘴上什么都敢说，正经话一句没有。" },
    };

    public const string HeadPatOverflowText = "哈哈行了行了，头都要被你摸秃了！明天再来！";

    public static string HeadPatLine(int stage)
    {
        var pool = HeadPatLinesByStage[Mathf.Clamp(stage, 0, HeadPatLinesByStage.Length - 1)];
        return pool[Random.Range(0, pool.Length)];
    }

    // ———— 每日进店问候（今日首次进酒馆弹一次；深夜 0~5 点走专属池） ————
    const string GreetDayKey = "tavern_greet_day";

    static readonly string[] DailyGreetings =
    {
        "来啦！老位子，酒已温好！",
        "哟，今天风把你吹来了？",
        "进来进来，别在门口杵着！",
        "今天的酒烈，敢不敢喝？",
        "又去裂隙了？回来就好，喝一口压压惊！",
        "独眼又赊了两杯。记账上，反正他跑不掉。",
    };

    static readonly string[] LateNightGreetings =
    {
        "这个点来？好！深夜的酒最够劲！",
        "睡不着？坐下，老娘陪你喝两杯。",
        "嘘——轻点，别吵醒那些没酒量的。",
    };

    public static bool IsLateNight
    {
        get { int h = System.DateTime.Now.Hour; return h >= 0 && h < 5; }
    }

    /// <summary>今日首次进酒馆返回问候语（并记日期），之后返回 null。</summary>
    public static string ConsumeDailyGreeting()
    {
        int today = TodayIndex();
        if (PlayerPrefs.GetInt(GreetDayKey, -1) == today) return null;
        PlayerPrefs.SetInt(GreetDayKey, today);
        var pool = IsLateNight ? LateNightGreetings : DailyGreetings;
        return pool[Random.Range(0, pool.Length)];
    }

    // ———— 长按立绘 ————
    public static readonly string[] LongPressLines =
    {
        "看什么看！脸上有酒还是花？",
        "再看两眼——行，看够没？酒要凉了！",
    };

    // ———— 挂机打哈欠（酒馆页打开 60 秒无操作） ————
    public static readonly string[] IdleLines =
    {
        "哈——啊，客人都死哪去了？",
        "（擦杯子）……安静得想唱歌了。",
        "喂！还活着没？酒要放凉了！",
    };

    // ———— 禁闭期额外吐槽（低概率替换倒计时提示） ————
    public static string BannedTeaseLine(float remainSeconds)
    {
        int sec = Mathf.CeilToInt(Mathf.Max(0f, remainSeconds));
        return "老实待着！老娘说一不二！（还剩" + sec + "秒）";
    }

    // ———— 连败安慰（前台小姐姐说，御姐傲娇口吻） ————
    const string DefeatStreakKey = "battle_defeat_streak";
    const string PendingComfortKey = "town_pending_comfort";

    /// <summary>连续死亡达到该局数时，回城后前台小姐姐安慰一次。</summary>
    public const int ComfortStreakThreshold = 3;

    public static int DefeatStreak => PlayerPrefs.GetInt(DefeatStreakKey, 0);

    /// <summary>战斗死亡（非引导局）+1 连败并挂回城安慰标记。</summary>
    public static void RegisterDeath()
    {
        PlayerPrefs.SetInt(DefeatStreakKey, DefeatStreak + 1);
        PlayerPrefs.SetInt(PendingComfortKey, 1);
    }

    /// <summary>主动撤离（非引导局）＝情绪重置，连败清零。</summary>
    public static void RegisterEvacuate()
    {
        PlayerPrefs.SetInt(DefeatStreakKey, 0);
    }

    static readonly string[] ComfortLines =
    {
        "回来了？……哼，输了也没关系，先歇着，热水给你备好了。",
        "又输啦？真是的……大厅的门又不会关，进来吧。",
        "下一局？……嗯，本小姐看好你，别让我失望。",
    };

    /// <summary>回城后消费连败安慰：达到阈值时返回台词（一次性），否则 null。</summary>
    public static string ConsumePendingComfort()
    {
        if (PlayerPrefs.GetInt(PendingComfortKey, 0) == 0) return null;
        PlayerPrefs.SetInt(PendingComfortKey, 0);
        if (DefeatStreak < ComfortStreakThreshold) return null;
        return ComfortLines[Random.Range(0, ComfortLines.Length)];
    }

    static int TodayIndex() => int.Parse(System.DateTime.Now.ToString("yyyyMMdd"));
}
