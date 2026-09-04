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
        Sprite informant = ResolveInformantPortrait();
        Sprite landlady = ResolveLandladyPortrait();
        Sprite player = StoryPortraits.Get(StoryPortraits.Player);

        yield return CoSpeak(ui, "独眼", entry.InformantLine, informant, player, true);

        bool showLady = entry.ShowLandlady
            || entry.Type == IntelDailyTable.IntelType.Flirt
            || !string.IsNullOrEmpty(entry.LandladyLine);
        if (showLady && !string.IsNullOrEmpty(entry.LandladyLine))
            yield return CoSpeak(ui, "老板娘", entry.LandladyLine, landlady, player, true);

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

    static IEnumerator CoSpeak(
        DialogueUI ui, string speaker, string line,
        Sprite speakerSp, Sprite playerSp, bool speakerLeft)
    {
        bool done = false;
        ui.ShowLine(
            speakerLeft ? speaker : StoryProgress.GetPlayerName(),
            speakerLeft ? StoryProgress.GetPlayerName() : speaker,
            line,
            speakerLeft ? speakerSp : playerSp,
            speakerLeft ? playerSp : speakerSp,
            null,
            speakerIsInitiator: speakerLeft,
            onAdvance: () => { done = true; },
            onSkip: () => { done = true; },
            soloCentered: false);
        while (!ui.IsTypeComplete)
            yield return null;
        while (!done)
            yield return null;
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
        Sprite informant = ResolveInformantPortrait();
        Sprite player = StoryPortraits.Get(StoryPortraits.Player);

        bool failed = false;
        int boostTurns = 3;

        for (int r = 0; r < quiz.Rounds.Count; r++)
        {
            var round = quiz.Rounds[r];
            if (round.BoostTurns > 0)
                boostTurns = round.BoostTurns;

            ui.ShowLine(
                "独眼",
                StoryProgress.GetPlayerName(),
                round.Question,
                informant,
                player,
                null,
                speakerIsInitiator: true,
                onAdvance: null,
                onSkip: null,
                soloCentered: false);

            while (!ui.IsTypeComplete)
                yield return null;

            int picked = -1;
            ui.ShowChoices(round.Options, idx => { picked = idx; });
            while (picked < 0)
                yield return null;
            ui.HideChoices();

            if (round.IsStory)
            {
                string reply = CleanReply(round.ReplyOk);
                if (!string.IsNullOrEmpty(reply))
                {
                    bool replyDone = false;
                    ui.ShowLine(
                        "独眼",
                        StoryProgress.GetPlayerName(),
                        reply,
                        informant,
                        player,
                        null,
                        speakerIsInitiator: true,
                        onAdvance: () => { replyDone = true; },
                        onSkip: () => { replyDone = true; },
                        soloCentered: false);
                    while (!replyDone)
                        yield return null;
                }
                continue;
            }

            bool correct = picked >= 0 && picked < 3 && round.Correct[picked];
            string quizReply = CleanReply(correct ? round.ReplyOk : round.ReplyFail);
            if (string.IsNullOrEmpty(quizReply))
                quizReply = correct ? "答对了。" : "答错了。";

            bool done = false;
            ui.ShowLine(
                "独眼",
                StoryProgress.GetPlayerName(),
                quizReply,
                informant,
                player,
                null,
                speakerIsInitiator: true,
                onAdvance: () => { done = true; },
                onSkip: () => { done = true; },
                soloCentered: false);
            while (!done)
                yield return null;

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
