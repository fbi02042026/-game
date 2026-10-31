using System.Collections;
using UnityEngine;

/// <summary>
/// 酒馆独眼情报商：日常情报 + 多轮剧情/问答。招募加成仅 stub。
/// </summary>
public class InformantQuizDirector : MonoBehaviour
{
    const string PrefFailPrefix = "intel_quiz_fail_";
    const string PrefBoostMerc = "intel_quiz_boost_merc";
    const string PrefBoostLeft = "intel_quiz_boost_left";

    // 立绘归一化用的真源 ID（内容包围盒表在 StoryPortraitLayout.PortraitContentFits）
    const string InformantPortraitId = "npc_duyan";
    const string LandladyPortraitId = "landlady";

    /// <summary>
    /// 酒馆底图（2026-10-15 主人：佣兵情报的背景用酒馆背景、变暗，跟引导/剧情一个模式）。
    /// 800×1424 满屏高的那张，走 Resources 路径加载。
    /// </summary>
    const string TavernBackgroundPath = "UI/Art/Tavern/酒馆_0007_图层-4";
    /// <summary>暗化值与 StoryDirector 换背景时一致（0.42）。</summary>
    const float TavernBgDim = 0.42f;

    static InformantQuizDirector _inst;
    bool _busy;

    public static InformantQuizDirector Ensure()
    {
        if (_inst != null) return _inst;
        var go = new GameObject("InformantQuizDirector");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<InformantQuizDirector>();
        return _inst;
    }

    public static void StartDaily()
    {
        Ensure().BeginDaily();
    }

    public static void StartQuiz()
    {
        Ensure().BeginQuizOnly();
    }

    public static bool IsQuizBlockedToday(string quizId)
    {
        if (string.IsNullOrEmpty(quizId)) return true;
        string key = PrefFailPrefix + quizId;
        string day = PlayerPrefs.GetString(key, "");
        return day == TodayKey();
    }

    public static bool IsQuizDoneToday()
    {
        return PlayerPrefs.GetInt(PrefQuizDoneKey(), 0) == 1;
    }

    public static void MarkQuizDoneToday()
    {
        PlayerPrefs.SetInt(PrefQuizDoneKey(), 1);
        PlayerPrefs.Save();
    }

    public static Sprite ResolveInformantPortraitPublic() => ResolveInformantPortrait();

    /// <summary>供日常情报流程 yield：跑完一整条 IQ。</summary>
    public IEnumerator CoRunQuizExternal(IntelQuizTable.Quiz quiz, bool markDailyDone)
    {
        if (quiz == null || quiz.Rounds == null || quiz.Rounds.Count == 0)
            yield break;
        yield return CoRunQuiz(quiz, markDailyDone, releaseBusy: true);
    }

    static string TodayKey() => System.DateTime.Now.ToString("yyyyMMdd");
    static string PrefQuizDoneKey() => "intel_quiz_done_" + TodayKey();

    void BeginDaily()
    {
        if (_busy)
        {
            UIManager.Instance?.ShowToast("情报商正忙…");
            return;
        }
        var entry = IntelDailyTable.Pick(IsQuizDoneToday());
        if (entry == null)
        {
            UIManager.Instance?.ShowToast("今天没什么新鲜情报。");
            return;
        }
        StartCoroutine(CoDaily(entry));
    }

    void BeginQuizOnly()
    {
        if (_busy)
        {
            UIManager.Instance?.ShowToast("情报商正忙…");
            return;
        }
        var quiz = IntelQuizTable.PickAvailable();
        if (quiz == null || quiz.Rounds == null || quiz.Rounds.Count == 0)
        {
            UIManager.Instance?.ShowToast("今天没有新情报了，明天再来。");
            return;
        }
        StartCoroutine(CoRunQuiz(quiz, markDailyDone: true, releaseBusy: true));
    }

    IEnumerator CoDaily(IntelDailyTable.Entry entry)
    {
        _busy = true;
        var ui = EnsureDialogueUi();
        if (ui == null)
        {
            _busy = false;
            yield break;
        }

        ui.PrepareForStoryBeat();
        ApplyTavernBackdrop(ui);
        Sprite informant = ResolveInformantPortrait();
        Sprite landlady = ResolveLandladyPortrait();

        // 2026-10-16 主人拍板：独眼先说话 → 独眼固定左槽（发起方）、老板娘固定右槽（对方）；
        // 两人都在（纯对话）时两边站人；只有独眼（无老板娘台词）时站中间。
        // showLady 必须提前算——独眼第一句就要按它决定右槽铺不铺人。
        bool showLady = entry.ShowLandlady
            || entry.Type == IntelDailyTable.IntelType.Flirt
            || !string.IsNullOrEmpty(entry.LandladyLine);

        yield return CoSpeak(ui, entry.InformantLine, informant, landlady, showLady, landladySpeaking: false);

        if (showLady && !string.IsNullOrEmpty(entry.LandladyLine))
            yield return CoSpeak(ui, entry.LandladyLine, informant, landlady, ladyOnStage: true, landladySpeaking: true);

        bool wantQuiz = !IsQuizDoneToday()
            && (entry.Type == IntelDailyTable.IntelType.TimedQuiz || entry.CanTriggerQuiz);

        IntelQuizTable.Quiz quiz = null;
        if (wantQuiz)
        {
            if (!string.IsNullOrEmpty(entry.MercId))
                quiz = IntelQuizTable.GetByMercId(entry.MercId);
            if (quiz != null && IsQuizBlockedToday(quiz.QuizId))
                quiz = null;
            if (quiz == null)
                quiz = IntelQuizTable.PickAvailable();
        }

        if (quiz != null)
            yield return CoRunQuiz(quiz, markDailyDone: true, releaseBusy: false);
        else
            ui.Hide();

        _busy = false;
    }

    /// <summary>
    /// 情报对话一句的立绘摆法（2026-10-16 主人拍板）：
    /// 独眼先说话 → 独眼固定左槽（发起方，面朝右），老板娘固定右槽（对方，面朝左）；
    /// 两人都在（纯对话）时两边站人；只有独眼（单人句）时站中间（soloCentered）。
    /// 老板娘开口时 speakerIsInitiator=false，高亮/压暗自动切到右侧（DialogueUI.SetSpeakerHighlight 既有机制）。
    /// 玩家只点选项、从不开口，全程不铺玩家立绘（2026-10-15 主人口径不变）。
    /// </summary>
    static IEnumerator CoSpeak(
        DialogueUI ui, string line,
        Sprite informantSp, Sprite landladySp,
        bool ladyOnStage, bool landladySpeaking)
    {
        // 老板娘能开口就一定在台上；双保险，防调用方漏传 ladyOnStage
        bool dual = ladyOnStage || landladySpeaking;
        bool done = false;
        ui.ShowLine(
            "独眼",
            dual ? "老板娘" : "",
            line,
            informantSp,
            dual ? landladySp : null,
            null,
            speakerIsInitiator: !landladySpeaking,
            onAdvance: () => { done = true; },
            onSkip: () => { done = true; },
            soloCentered: !dual,
            initiatorPortraitId: InformantPortraitId,
            otherPortraitId: dual ? LandladyPortraitId : null,
            normalizePortraitSize: true);
        while (!ui.IsTypeComplete)
            yield return null;
        while (!done)
            yield return null;
    }

    /// <summary>一句台词按「说话人：」前缀拆行后的结果（2026-10-31 修名牌错乱时新增）。</summary>
    struct SpeakerLine
    {
        public bool IsLady;
        public string Text;
    }

    /// <summary>
    /// 把表里的多行台词拆成逐拍：以「老板娘：」开头的归老板娘，「独眼：／莫洛克：」开头的归独眼，
    /// 无前缀的行沿用上一句说话人（首行默认独眼）；「—」空占位直接丢弃。
    /// </summary>
    static System.Collections.Generic.List<SpeakerLine> SplitSpeakerLines(string raw)
    {
        var list = new System.Collections.Generic.List<SpeakerLine>();
        if (string.IsNullOrEmpty(raw)) return list;

        bool lastLady = false;
        string[] lines = raw.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string s = lines[i].Trim();
            if (string.IsNullOrEmpty(s) || s == "—" || s == "-" || s == "–") continue;

            int idx = s.IndexOf('：');
            if (idx < 0) idx = s.IndexOf(':');
            if (idx > 0 && idx <= 6)
            {
                string who = s.Substring(0, idx).Trim();
                if (who == "老板娘") { lastLady = true; s = s.Substring(idx + 1).Trim(); }
                else if (who == "独眼" || who == "莫洛克") { lastLady = false; s = s.Substring(idx + 1).Trim(); }
            }

            if (string.IsNullOrEmpty(s)) continue;
            list.Add(new SpeakerLine { IsLady = lastLady, Text = s });
        }
        return list;
    }

    /// <summary>
    /// 逐拍播一段（可能含多个说话人的）台词。多行＝双人站台（独眼左／老板娘右），且每拍要玩家点一下
    /// 才继续（否则后一句会盖掉前一句）；单行问题保持「打完字直接出选项」的原交互。
    /// </summary>
    IEnumerator CoSpeakLines(DialogueUI ui, string raw, Sprite informantSp, Sprite landladySp, bool isReply)
    {
        var lines = SplitSpeakerLines(raw);
        if (lines.Count == 0) yield break;

        bool multi = lines.Count > 1;
        bool requireClick = isReply || multi;

        for (int k = 0; k < lines.Count; k++)
        {
            bool done = false;
            ui.ShowLine(
                "独眼",
                multi ? "老板娘" : "",
                lines[k].Text,
                informantSp,
                multi ? landladySp : null,
                null,
                speakerIsInitiator: !lines[k].IsLady,
                onAdvance: requireClick ? (System.Action)(() => { done = true; }) : null,
                onSkip: requireClick ? (System.Action)(() => { done = true; }) : null,
                soloCentered: !multi,
                initiatorPortraitId: InformantPortraitId,
                otherPortraitId: multi ? LandladyPortraitId : null,
                normalizePortraitSize: true);

            while (!ui.IsTypeComplete)
                yield return null;
            if (requireClick)
                while (!done)
                    yield return null;
        }
    }

    /// <summary>铺酒馆背景 + 暗化，与剧情/引导同一套表现（2026-10-15 主人）。</summary>
    static void ApplyTavernBackdrop(DialogueUI ui)
    {
        if (ui == null) return;
        var bg = Resources.Load<Sprite>(TavernBackgroundPath);
        if (bg == null)
        {
            Debug.LogError("[InformantQuiz] 酒馆背景缺失：Resources/" + TavernBackgroundPath);
            return;
        }
        ui.SetSceneBackground(bg);
        ui.SetRevealBlack(0f); // 情报不播「地点揭示」黑场，直接出画面
        ui.SetBgDim(TavernBgDim);
        ui.SetSceneBackgroundAlpha(1f);
    }

    static DialogueUI EnsureDialogueUi()
    {
        var ui = DialogueUI.Instance;
        if (ui != null) return ui;
        var prefab = Resources.Load<GameObject>(ContentPaths.Prefab.Dialogue);
        if (prefab == null)
        {
            Debug.LogError("[InformantQuiz] DialogueUI 未就绪");
            return null;
        }
        var go = Object.Instantiate(prefab);
        Object.DontDestroyOnLoad(go);
        return go.GetComponent<DialogueUI>();
    }

    static Sprite ResolveLandladyPortrait()
    {
        var sp = StoryPortraits.Get("landlady");
        if (sp != null) return sp;
        sp = StoryPortraits.Get("boss_niang");
        if (sp != null) return sp;
        return StoryPortraits.Get(StoryPortraits.Receptionist);
    }

    IEnumerator CoRunQuiz(IntelQuizTable.Quiz quiz, bool markDailyDone, bool releaseBusy)
    {
        _busy = true;
        var ui = EnsureDialogueUi();
        if (ui == null)
        {
            if (releaseBusy) _busy = false;
            yield break;
        }

        ui.PrepareForStoryBeat();
        ApplyTavernBackdrop(ui);
        Sprite informant = ResolveInformantPortrait();
        Sprite landlady = ResolveLandladyPortrait();

        bool failed = false;
        int boostTurns = 3;

        for (int r = 0; r < quiz.Rounds.Count; r++)
        {
            var round = quiz.Rounds[r];
            if (round.BoostTurns > 0)
                boostTurns = round.BoostTurns;

            // 【2026-10-31 修】原实现把整段台词塞给「独眼」一个名牌，表里「老板娘：」开头的行
            // 会连同前缀一起显示在独眼名下（IQ 剧情轮 8 处）。改为按行拆拍、按前缀换说话人与立绘。
            yield return CoSpeakLines(ui, round.Question, informant, landlady, isReply: false);

            int picked = -1;
            ui.ShowChoices(round.Options, idx => { picked = idx; });
            while (picked < 0)
                yield return null;
            ui.HideChoices();

            if (round.IsStory)
            {
                yield return CoSpeakLines(ui, round.ReplyOk, informant, landlady, isReply: true);
                continue;
            }

            bool correct = picked >= 0 && picked < 3 && round.Correct[picked];
            string quizReply = CleanReply(correct ? round.ReplyOk : round.ReplyFail);
            if (string.IsNullOrEmpty(quizReply))
                quizReply = correct ? "答对了。" : "答错了。";

            yield return CoSpeakLines(ui, quizReply, informant, landlady, isReply: true);

            if (!correct)
            {
                failed = true;
                MarkFailedToday(quiz.QuizId);
                break;
            }
        }

        if (markDailyDone)
            MarkQuizDoneToday();

        ui.Hide();
        if (!failed)
        {
            SetBoostStub(quiz.MercId, boostTurns);
            UIManager.Instance?.ShowToast($"去招募看看吧——说不定「{quiz.MercName}」就在等你。");
        }
        else
        {
            UIManager.Instance?.ShowToast("佣兵已经离开了，等下次吧。");
        }

        if (releaseBusy)
            _busy = false;
    }

    static Sprite ResolveInformantPortrait()
    {
        var sp = StoryPortraits.Get("npc_duyan");
        if (sp != null) return sp;
        sp = StoryPortraits.Get("duyan");
        if (sp != null) return sp;
        return StoryPortraits.Get(StoryPortraits.Hunter);
    }

    static string CleanReply(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        if (s == "—" || s == "-" || s == "–") return "";
        return s;
    }

    static void MarkFailedToday(string quizId)
    {
        PlayerPrefs.SetString(PrefFailPrefix + quizId, TodayKey());
        PlayerPrefs.Save();
    }

    /// <summary>限时招募加成 stub，招募池后续接入。</summary>
    public static void SetBoostStub(string mercId, int turns)
    {
        if (string.IsNullOrEmpty(mercId)) return;
        PlayerPrefs.SetString(PrefBoostMerc, mercId);
        PlayerPrefs.SetInt(PrefBoostLeft, Mathf.Max(1, turns));
        PlayerPrefs.Save();
        Debug.Log($"[InformantQuiz] stub 招募加成 merc={mercId} turns={turns}");
    }

    public static bool TryGetBoostStub(out string mercId, out int turnsLeft)
    {
        mercId = PlayerPrefs.GetString(PrefBoostMerc, "");
        turnsLeft = PlayerPrefs.GetInt(PrefBoostLeft, 0);
        return !string.IsNullOrEmpty(mercId) && turnsLeft > 0;
    }
}

/// <summary>酒馆「佣兵情报」门面：日常抽条入口。</summary>
public static class InformantIntelDirector
{
    public static void StartDaily() => InformantQuizDirector.StartDaily();
    public static bool IsQuizDoneToday() => InformantQuizDirector.IsQuizDoneToday();
    public static void MarkQuizDoneToday() => InformantQuizDirector.MarkQuizDoneToday();
}
