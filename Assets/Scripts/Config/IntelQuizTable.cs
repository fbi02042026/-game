using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 酒馆独眼情报商问答表（intel_quiz.bytes）。
/// 轮次类型：剧情（任意推进）/ 问答（对错判定）。
/// </summary>
public static class IntelQuizTable
{
    public enum RoundKind
    {
        Story,
        Quiz
    }

    public class Round
    {
        public string QuizId;
        public string MercId;
        public string MercName;
        public string PoolType;
        public int RoundIndex;
        public RoundKind Kind;
        public string Question;
        public string[] Options = new string[3];
        public bool[] Correct = new bool[3];
        public string ReplyOk;
        public string ReplyFail;
        public int BoostMin = 1;
        public int BoostMax = 3;
        public int BoostTurns = 3;

        public bool IsStory => Kind == RoundKind.Story;
    }

    public class Quiz
    {
        public string QuizId;
        public string MercId;
        public string MercName;
        public string PoolType;
        public readonly List<Round> Rounds = new List<Round>();
    }

    static Dictionary<string, Quiz> _byId;
    static Dictionary<string, Quiz> _byMercId;
    static List<Quiz> _all;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _byId = null;
        _byMercId = null;
        _all = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byId = new Dictionary<string, Quiz>();
        _byMercId = new Dictionary<string, Quiz>();
        _all = new List<Quiz>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.IntelQuiz);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[IntelQuizTable] 未找到 intel_quiz 表");
            return;
        }

        var rows = CsvUtil.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Count < 20) continue;
            string quizId = cols[0].Trim().TrimStart('\ufeff');
            if (string.IsNullOrEmpty(quizId) || !quizId.StartsWith("IQ")) continue;

            if (!_byId.TryGetValue(quizId, out var quiz))
            {
                quiz = new Quiz
                {
                    QuizId = quizId,
                    MercId = cols[1].Trim(),
                    MercName = cols[2].Trim(),
                    PoolType = cols.Count > 20 ? cols[20].Trim() : ""
                };
                _byId[quizId] = quiz;
                _all.Add(quiz);
                if (!string.IsNullOrEmpty(quiz.MercId) && !_byMercId.ContainsKey(quiz.MercId))
                    _byMercId[quiz.MercId] = quiz;
            }

            // 0ID 1佣兵 2名 3稀有 4职 5轮次 6轮次类型 7难度 8问题
            // 9A文 10A倾 11A对 12B文 13B倾 14B对 15C文 16C倾 17C对
            // 18对回复 19错回复 20池 21min 22max 23持续 ...
            var kind = ParseKind(cols[6].Trim());
            var round = new Round
            {
                QuizId = quizId,
                MercId = quiz.MercId,
                MercName = quiz.MercName,
                PoolType = quiz.PoolType,
                RoundIndex = CsvUtil.ParseInt(cols[5], 1),
                Kind = kind,
                Question = IntelDailyTable.StripSpeakerPrefix(cols[8].Trim()),
                ReplyOk = cols.Count > 18 ? cols[18].Trim() : "",
                ReplyFail = cols.Count > 19 ? cols[19].Trim() : "",
                BoostMin = cols.Count > 21 ? CsvUtil.ParseInt(cols[21], 1) : 1,
                BoostMax = cols.Count > 22 ? CsvUtil.ParseInt(cols[22], 3) : 3,
                BoostTurns = cols.Count > 23 ? CsvUtil.ParseInt(cols[23], 3) : 3
            };
            round.Options[0] = cols[9].Trim();
            round.Correct[0] = kind == RoundKind.Story || CsvUtil.IsYes(cols[11]);
            round.Options[1] = cols[12].Trim();
            round.Correct[1] = kind == RoundKind.Story || CsvUtil.IsYes(cols[14]);
            round.Options[2] = cols[15].Trim();
            round.Correct[2] = kind == RoundKind.Story || CsvUtil.IsYes(cols[17]);

            if (kind == RoundKind.Story)
            {
                if (string.IsNullOrEmpty(round.Options[0])) round.Options[0] = "继续听下去";
                if (string.IsNullOrEmpty(round.Options[1])) round.Options[1] = "有意思";
                if (string.IsNullOrEmpty(round.Options[2])) round.Options[2] = "然后呢？";
                round.Correct[0] = round.Correct[1] = round.Correct[2] = true;
            }

            if (string.IsNullOrEmpty(quiz.PoolType) && cols.Count > 20)
                quiz.PoolType = cols[20].Trim();

            quiz.Rounds.Add(round);
            ok++;
        }

        for (int i = 0; i < _all.Count; i++)
            _all[i].Rounds.Sort((a, b) => a.RoundIndex.CompareTo(b.RoundIndex));

        Debug.Log($"[IntelQuizTable] 加载问答 {ok} 轮 / {_all.Count} 条情报");
    }

    static RoundKind ParseKind(string s)
    {
        if (s == "剧情" || s == "story" || s == "—") return RoundKind.Story;
        return RoundKind.Quiz;
    }

    public static IReadOnlyList<Quiz> All
    {
        get { EnsureLoaded(); return _all; }
    }

    public static Quiz Get(string quizId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(quizId) || _byId == null) return null;
        return _byId.TryGetValue(quizId, out var q) ? q : null;
    }

    public static Quiz GetByMercId(string mercId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(mercId) || _byMercId == null) return null;
        return _byMercId.TryGetValue(mercId, out var q) ? q : null;
    }

    public static Quiz PickAvailable()
    {
        EnsureLoaded();
        if (_all == null || _all.Count == 0) return null;
        var candidates = new List<Quiz>();
        for (int i = 0; i < _all.Count; i++)
        {
            if (!InformantQuizDirector.IsQuizBlockedToday(_all[i].QuizId))
                candidates.Add(_all[i]);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }
}

/// <summary>简易 CSV 解析（支持引号内换行与逗号）。</summary>
public static class CsvUtil
{
    public static List<List<string>> ParseRows(string raw)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(raw)) return rows;

        var row = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;
                }
                else
                    sb.Append(c);
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }
            if (c == ',')
            {
                row.Add(sb.ToString());
                sb.Length = 0;
                continue;
            }
            if (c == '\r') continue;
            if (c == '\n')
            {
                row.Add(sb.ToString());
                sb.Length = 0;
                if (row.Count > 1 || (row.Count == 1 && !string.IsNullOrWhiteSpace(row[0])))
                    rows.Add(row);
                row = new List<string>();
                continue;
            }
            sb.Append(c);
        }

        row.Add(sb.ToString());
        if (row.Count > 1 || (row.Count == 1 && !string.IsNullOrWhiteSpace(row[0])))
            rows.Add(row);
        return rows;
    }

    public static bool IsYes(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.Trim();
        return s == "是" || s == "1" || s.Equals("true", System.StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", System.StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseInt(string s, int fallback)
    {
        if (int.TryParse(s, out int v)) return v;
        return fallback;
    }
}

/// <summary>
/// 酒馆日常情报表（intel_daily.bytes）。
/// 类型：普通介绍 / 打情骂俏 / 限时问答。
/// </summary>
public static class IntelDailyTable
{
    public enum IntelType
    {
        Intro,
        Flirt,
        TimedQuiz
    }

    public class Entry
    {
        public string Id;
        public IntelType Type;
        public string MercId;
        public string MercName;
        public bool ShowLandlady;
        public string InformantLine;
        public string LandladyLine;
        public bool CanTriggerQuiz;
    }

    public const float WeightIntro = 0.70f;
    public const float WeightFlirt = 0.20f;
    public const float WeightTimedQuiz = 0.10f;

    static List<Entry> _all;
    static List<Entry> _intro;
    static List<Entry> _flirt;
    static List<Entry> _timed;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _all = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _all = new List<Entry>();
        _intro = new List<Entry>();
        _flirt = new List<Entry>();
        _timed = new List<Entry>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.IntelDaily);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[IntelDailyTable] 未找到 intel_daily 表");
            return;
        }

        var rows = CsvUtil.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Count < 10) continue;
            string id = cols[0].Trim().TrimStart('\ufeff');
            if (string.IsNullOrEmpty(id) || !id.StartsWith("DI"))
            {
                if (id.StartsWith("情报")) continue;
                continue;
            }

            if (!TryParseType(cols[1].Trim(), out var type)) continue;
            var e = new Entry
            {
                Id = id,
                Type = type,
                MercId = cols[2].Trim(),
                MercName = cols[3].Trim(),
                ShowLandlady = CsvUtil.IsYes(cols[6]),
                InformantLine = StripSpeakerPrefix(cols[7].Trim()),
                LandladyLine = StripSpeakerPrefix(cols[8].Trim()),
                CanTriggerQuiz = CsvUtil.IsYes(cols[9])
            };
            if (string.IsNullOrEmpty(e.InformantLine)) continue;
            _all.Add(e);
            switch (type)
            {
                case IntelType.Intro: _intro.Add(e); break;
                case IntelType.Flirt: _flirt.Add(e); break;
                case IntelType.TimedQuiz: _timed.Add(e); break;
            }
            ok++;
        }
        Debug.Log($"[IntelDailyTable] 加载日常情报 {ok} 条");
    }

    static bool TryParseType(string s, out IntelType type)
    {
        switch (s)
        {
            case "普通介绍": type = IntelType.Intro; return true;
            case "打情骂俏": type = IntelType.Flirt; return true;
            case "限时问答": type = IntelType.TimedQuiz; return true;
            default:
                type = IntelType.Intro;
                return false;
        }
    }

    /// <summary>去掉「莫洛克：」「老板娘：」等前缀，交给名牌显示说话人。</summary>
    public static string StripSpeakerPrefix(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        int idx = line.IndexOf('：');
        if (idx < 0) idx = line.IndexOf(':');
        if (idx > 0 && idx < 12)
            return line.Substring(idx + 1).Trim();
        return line;
    }

    /// <summary>按权重抽一条；quizUsedToday 时限时问答降级。</summary>
    public static Entry Pick(bool quizUsedToday)
    {
        EnsureLoaded();
        float r = Random.value;
        IntelType want;
        if (!quizUsedToday && r < WeightTimedQuiz)
            want = IntelType.TimedQuiz;
        else if (r < WeightTimedQuiz + WeightFlirt)
            want = IntelType.Flirt;
        else
            want = IntelType.Intro;

        var pool = Pool(want);
        if (pool == null || pool.Count == 0)
            pool = _intro;
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    static List<Entry> Pool(IntelType t)
    {
        switch (t)
        {
            case IntelType.Flirt: return _flirt;
            case IntelType.TimedQuiz: return _timed;
            default: return _intro;
        }
    }

    public static Entry Get(string id)
    {
        EnsureLoaded();
        if (_all == null) return null;
        for (int i = 0; i < _all.Count; i++)
            if (_all[i].Id == id) return _all[i];
        return null;
    }
}
