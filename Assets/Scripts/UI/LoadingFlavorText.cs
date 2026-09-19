using UnityEngine;

/// <summary>
/// Loading 界面的「裂隙杂记」：剧情 / 佣兵向的小段子，随 Loading 进度轮流显示。
/// 只为让等待不枯燥，不参与任何玩法逻辑、不读表、不写存档。
///
/// 用法：<see cref="Next"/> 每次取一条（一轮内不重复，走完自动重洗），
/// 调用方把它塞进 LoadingUI.SetTip 即可。
/// </summary>
public static class LoadingFlavorText
{
    /// <summary>
    /// 杂记正文。世界设定取自 chapter_theme_map（暮影森林 / 幽冥墓园 / 翡翠秘境 / 晨曦草原 /
    /// 海岛遗迹 / 巨岩深窟 / 赤焰炼狱 / 永霜雪境），佣兵名与性格取自 merc_lines。
    /// 加新条目直接往数组里追加即可，不需要改别的地方。
    /// </summary>
    static readonly string[] Lines =
    {
        // —— 佣兵杂记 ——
        "维克把盾擦了三遍，说反光能晃到敌人。我们信了。",
        "凯恩又在营地自燃了。别奶他，他说血越少越嗨。",
        "米娅的箭从不落空，她的嘴也是。",
        "莫丁今天单挑了一棵树。树赢了。",
        "索菲怕疼，却站在最前面给所有人加血。奇怪的勇敢。",
        "莫娜施法前一定要洗手。她说这是对魔法的尊重。",
        "布朗带的干粮比武器还重，他说这叫战略储备。",
        "格拉克斯把武器落在巨岩深窟了，已经找了三天。",
        "索尔一跺脚地面就裂，队友先跑，敌人后跑。",
        "艾拉射完不回头看结果。她说结果不影响下一箭。",
        "艾琳算过：这一箭命中率 87.3%。然后她射偏了。",
        "凯尔一句话不说，只是看着怪物。怪物自己退了。",
        "塞拉的水法很好用，就是打完地太滑，布朗会摔。",
        "伊芙笑得越温柔，敌人倒得越难看。",
        "希尔说自己不慢，是风追不上她。",
        "塔克一边挡刀一边提醒你站位，操心盾卫名不虚传。",
        "杜娅请全队喝酒，账记在你头上。",
        "莉娜说伤口会好的，然后给你加了三次血。",
        "洛恩站桩一整天，说这是修行。怪物绕过了他。",
        "马库斯是老实沙包，但沙包也有脾气。",
        "布罗克的斧头比他人高。他本人也很高。",
        "古恩转起来的时候，队友也会跟着转。",

        // —— 裂隙见闻 ——
        "裂隙之刃的刀锋上有倒影，据说那是另一个你。",
        "暮影森林的树会挪位置。别记路，记方向。",
        "幽冥墓园的墓碑每天多一块，没人承认是自己立的。",
        "翡翠秘境的虫子比人懂战术，它们会绕后。",
        "晨曦草原的清晨很美，前提是你活得到清晨。",
        "海岛遗迹的潮水一天涨两次，怪物也跟着涨。",
        "巨岩深窟回声很大，你喊一声，会有东西回答。",
        "赤焰炼狱里冰系法术最贵，因为没人愿意带。",
        "永霜雪境的雪不化，脚印也不化。",
        "酒馆老板娘说：活着回来再谈工钱。",
        "佣兵守则第一条：盾先上。第二条：凯恩除外。",
        "据说裂隙另一边也有间酒馆，老板娘长得一模一样。",
        "冒险日志第七页被撕掉了，没人肯说是谁撕的。",
        "如果你在副本里看见自己的尸体，别打招呼。",
        "裂隙会挑人。它挑中的人，通常都活不太久。",
        "老佣兵的忠告：看得见的怪好打，看不见的才算敌人。",
    };

    static int[] _bag;
    static int _cursor;

    /// <summary>取一条杂记；一轮内不重复，走完自动重洗。</summary>
    public static string Next()
    {
        if (Lines == null || Lines.Length == 0) return "";
        if (Lines.Length == 1) return Lines[0];

        if (_bag == null || _bag.Length != Lines.Length)
            Reshuffle();

        // 一轮走完（或上次洗牌的末尾刚好和这次开头撞上）→ 重洗，保证不连续重复
        if (_cursor >= _bag.Length)
            Reshuffle();

        int idx = _bag[_cursor];
        string s = Lines[idx];
        _cursor++;
        _lastServed = idx;
        return s;
    }

    /// <summary>重开一局 / 重新进 Loading 时调用，避免每次都从同一条开始。</summary>
    public static void Reset()
    {
        _bag = null;
        _cursor = 0;
    }

    static void Reshuffle()
    {
        _bag = new int[Lines.Length];
        for (int i = 0; i < _bag.Length; i++) _bag[i] = i;
        // Fisher–Yates
        for (int i = _bag.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = _bag[i];
            _bag[i] = _bag[j];
            _bag[j] = tmp;
        }
        // 洗完若首条与上一条结尾重复（一轮刚结束时可能撞），把首条换到后面
        if (_bag.Length > 1 && _lastServed >= 0 && _bag[0] == _lastServed)
        {
            int swap = Random.Range(1, _bag.Length);
            int tmp = _bag[0];
            _bag[0] = _bag[swap];
            _bag[swap] = tmp;
        }
        _cursor = 0;
    }

    /// <summary>上一条已发出的杂记下标，供下次洗牌避开「一轮首尾撞车」。</summary>
    static int _lastServed = -1;
}
