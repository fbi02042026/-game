using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 看板娘对话框：打字机效果、多台词轮换、说完后隐藏。
/// 气泡约 180×107，文案需短句；按实际宽度自动换行，最多 maxLines 行。
/// </summary>
public class SpeechBubbleTalker : MonoBehaviour
{
    [Header("引用（可空，按名自动找）")]
    public GameObject bubbleRoot;
    public Text bubbleText;
    public Button receptionistButton;

    [Header("打字机")]
    public float charsPerSecond = 18f;
    public float holdAfterFinish = 2.8f;
    public float idleHideDelay = 0.35f;
    public float gapBetweenLines = 1.2f;
    [Range(1, 4)] public int maxLines = 3;

    TextGenerator _textGen;

    static bool _suppressed;

    public static void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        if (suppressed)
        {
            var talkers = Object.FindObjectsOfType<SpeechBubbleTalker>();
            for (int i = 0; i < talkers.Length; i++)
                talkers[i].HideBubble();
        }
    }

    static readonly string[] DefaultLines =
    {
        "欢迎回来，冒险者！",
        "今日体力记得留着打冒险哦。",
        "点下方「冒险」就能出发。",
        "邮件满了会进邮箱，别忘领。",
        "佣兵可在酒馆招募哦。",
        "有问题随时来咨询台找我。",
        "清完一波怪再往前，会刷下一波。",
        "打完本关走传送门选下一关。",
        "今日还能：冒险、看邮件、逛酒馆。",
        "小知识：暴击能打出更高伤害！",
        "体力不足时等一会会自动回。",
        "角色页能看成长，装备会实时换装。",
        "商城以后会开放更多内容。",
        "想打听情报？点我就行。",
    };

    readonly List<string> _lines = new List<string>();
    Coroutine _loop;
    bool _busy;

    void Awake()
    {
        AutoBind();
        BuildLinePool();
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
        if (receptionistButton != null)
            receptionistButton.onClick.AddListener(OnReceptionistClicked);
    }

    void OnEnable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(TalkLoop());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        HideBubble();
    }

    void AutoBind()
    {
        if (bubbleRoot == null)
        {
            Transform t = FindDeep(transform, "SpeechBubble");
            if (t == null && transform.parent != null)
                t = FindDeep(transform.root, "SpeechBubble");
            if (t != null) bubbleRoot = t.gameObject;
        }
        if (bubbleText == null && bubbleRoot != null)
        {
            Transform t = FindDeep(bubbleRoot.transform, "BubbleText");
            if (t != null) bubbleText = t.GetComponent<Text>();
        }
        if (bubbleText != null)
        {
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            bubbleText.resizeTextForBestFit = false;
            bubbleText.alignByGeometry = false;
            bubbleText.alignment = TextAnchor.MiddleCenter;
        }
        if (receptionistButton == null)
        {
            Transform t = FindDeep(transform.root, "Receptionist");
            if (t != null) receptionistButton = t.GetComponent<Button>();
        }
    }

    void BuildLinePool()
    {
        _lines.Clear();
        for (int i = 0; i < DefaultLines.Length; i++)
            _lines.Add(FitText(DefaultLines[i]));
    }

    /// <summary>咨询台：优先说「今日可做 / 功能介绍」</summary>
    void OnReceptionistClicked()
    {
        if (_busy || _suppressed) return;
        string[] tips =
        {
            "今日可做：点「冒险」消耗体力出发。",
            "邮件：溢出资源会存在这里。",
            "酒馆：招募佣兵加入队伍。",
            "角色：查看成长，装备实时换装。",
            "有问题也可以来找我哦。",
        };
        StopAllCoroutines();
        _loop = StartCoroutine(SpeakOnceThenResume(FitText(tips[Random.Range(0, tips.Length)])));
    }

    IEnumerator SpeakOnceThenResume(string line)
    {
        yield return SpeakLine(line);
        _loop = StartCoroutine(TalkLoop());
    }

    IEnumerator TalkLoop()
    {
        // 开场稍等再说话
        yield return new WaitForSeconds(1.0f);
        while (enabled)
        {
            if (_suppressed)
            {
                HideBubble();
                yield return null;
                continue;
            }
            if (_lines.Count == 0) yield break;
            string line = _lines[Random.Range(0, _lines.Count)];
            yield return SpeakLine(line);
            yield return new WaitForSeconds(gapBetweenLines);
        }
    }

    IEnumerator SpeakLine(string line)
    {
        _busy = true;
        if (bubbleRoot != null) bubbleRoot.SetActive(true);
        if (bubbleText != null) bubbleText.text = "";

        float delay = 1f / Mathf.Max(1f, charsPerSecond);
        for (int i = 1; i <= line.Length; i++)
        {
            if (bubbleText != null)
                bubbleText.text = line.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }

        yield return new WaitForSeconds(holdAfterFinish);
        yield return new WaitForSeconds(idleHideDelay);
        HideBubble();
        _busy = false;
    }

    void HideBubble()
    {
        if (bubbleText != null) bubbleText.text = "";
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }

    /// <summary>按气泡实际宽度裁切，只让 Text 自动换行，避免硬插换行导致多出一行。</summary>
    string FitText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        raw = raw.Replace("\r", "").Replace("\n", "").Trim();
        if (bubbleText == null) return raw;

        string result = raw;
        while (result.Length > 1 && CountWrappedLines(result) > maxLines)
            result = result.Substring(0, result.Length - 1);

        if (result.Length < raw.Length)
        {
            result = result.TrimEnd('，', '。', '、', ' ', '…');
            if (string.IsNullOrEmpty(result))
                result = raw.Substring(0, 1);
            result += "…";
        }
        return result;
    }

    int CountWrappedLines(string text)
    {
        if (bubbleText == null || string.IsNullOrEmpty(text)) return 0;

        float width = bubbleText.rectTransform.rect.width;
        if (width <= 1f)
            width = 148f;

        if (_textGen == null)
            _textGen = new TextGenerator();

        var settings = bubbleText.GetGenerationSettings(new Vector2(width, 0f));
        _textGen.Populate(text, settings);
        return Mathf.Max(1, _textGen.lineCount);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
