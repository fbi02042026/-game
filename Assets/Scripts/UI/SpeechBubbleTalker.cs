using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 看板娘对话框：打字机效果、多台词轮换、说完后隐藏。
/// 气泡约 180×107，文案需短句，超出自动裁切。
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
    [Range(4, 28)] public int maxCharsPerLine = 11;
    [Range(1, 4)] public int maxLines = 3;

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
        "武器库可整理装备与强化。",
        "公告板有活动情报，常来看看。",
        "小知识：暴击能打出更高伤害！",
        "体力不足时等一会会自动回。",
        "角色页能看成长与穿戴。",
        "商城和活动以后会开放更多。",
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
            bubbleText.alignByGeometry = true;
            var rt = bubbleText.rectTransform;
            if (rt != null)
            {
                // 略缩边距，避免字贴边/溢出气泡
                rt.offsetMin = new Vector2(10f, 12f);
                rt.offsetMax = new Vector2(-10f, -8f);
            }
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
        if (_busy) return;
        string[] tips =
        {
            "今日可做：点「冒险」消耗体力出发。",
            "邮件：溢出资源会存在这里。",
            "酒馆：招募与管理佣兵。",
            "角色：查看成长与装备。",
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

    /// <summary>按气泡宽度粗略断行，避免超出对话框</summary>
    string FitText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        raw = raw.Replace("\r", "").Replace("\n", "");
        int maxTotal = maxCharsPerLine * maxLines;
        if (raw.Length > maxTotal)
            raw = raw.Substring(0, maxTotal - 1) + "…";

        if (raw.Length <= maxCharsPerLine) return raw;

        var sb = new System.Text.StringBuilder();
        int i = 0;
        int lines = 0;
        while (i < raw.Length && lines < maxLines)
        {
            int take = Mathf.Min(maxCharsPerLine, raw.Length - i);
            if (lines > 0) sb.Append('\n');
            sb.Append(raw, i, take);
            i += take;
            lines++;
        }
        return sb.ToString();
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
