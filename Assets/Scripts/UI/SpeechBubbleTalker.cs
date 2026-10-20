using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 看板娘对话框：打字机效果、多台词轮换、说完后隐藏。
/// 气泡默认约 180×107；文案先 Wrap 多行，放不下再等比放大（见 SpeechBubbleFit）。
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

    Vector2 _baseBubbleSize = new Vector2(180f, 107f);
    bool _baseSizeCaptured;

    static bool _suppressed;

    // Toast 每次弹都要问一遍「有没有气泡」，用登记表代替 FindObjectsOfType 的全场景扫描
    static readonly List<SpeechBubbleTalker> _alive = new List<SpeechBubbleTalker>();

    public static void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        if (!suppressed) return;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null) _alive.RemoveAt(i);
            else _alive[i].HideBubble();
        }
    }

    /// <summary>任一看板娘气泡正在显示（供 Toast 互斥）。</summary>
    public static bool AnyBubbleShowing()
    {
        if (_suppressed) return false;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var t = _alive[i];
            if (t == null)
            {
                _alive.RemoveAt(i);
                continue;
            }
            if (t._busy) return true;
            if (t.bubbleRoot != null && t.bubbleRoot.activeSelf) return true;
        }
        return false;
    }

    // 【2026-10-18 主人拍板】看板娘（前台小姐姐）人设＝御姐、傲娇：轮播提示也带从容毒舌，功能信息不变
    static readonly string[] DefaultLines =
    {
        "欢迎光临。想好今天做什么了吗？",
        "体力可是有限的，别乱挥霍。",
        "「冒险」就在下面，想清楚了再去。",
        "邮箱快满了？……真是的，自己去领。",
        "酒馆有新佣兵……想见见吗？",
        "有事就来咨询台，本小姐一般都有空。",
        "打完一波还有一波，别急着冲。",
        "过了关就走传送门，别在原地发呆。",
        "今天还有精力的话——冒险、邮件、酒馆，随你挑。",
        "小知识：会暴击的冒险者，活得比较久。",
        "体力没了？等着，会自己回来的。",
        "装备穿上了会直接显示在身上，好好挑。",
        "商城以后会有更多好东西……等着吧。",
        "想打听情报？点我就行，偶尔。",
    };

    readonly List<string> _lines = new List<string>();
    Coroutine _loop;
    bool _busy;

    // —— 前台表情立绘（2026-10-18 主人补图：Resources/UI/Icons/佣兵立绘/前台小姐害羞/生气.png）——
    // 显示节点是 GuildHallUI 的看板（看板/show 的 Image）；仅当看板还是默认前台图时生效，
    // 玩家点 Turn 换成佣兵立绘后不套前台表情（防止张冠李戴）。
    const string FaceShyPath = "UI/Icons/佣兵立绘/前台小姐害羞";
    const string FaceAngryPath = "UI/Icons/佣兵立绘/前台小姐生气";
    const float FaceHoldSeconds = 1.2f;
    const int PokeAngryClicks = 5;          // 5 秒内连点 5 次 → 生气脸 + 专属台词
    const float PokeWindowSeconds = 5f;
    Sprite _faceShy;
    Sprite _faceAngry;
    Sprite _faceRestore;    // 表情恢复目标（切换瞬间的看板图）
    Coroutine _faceCo;
    bool _facesReady;
    int _pokeClicks;
    float _lastPokeTime;
    string _lastPokeLine;   // 上一句戳一戳台词，用于尽量不重复

    void EnsureFaces()
    {
        if (_facesReady) return;
        _facesReady = true;
        _faceShy = Resources.Load<Sprite>(FaceShyPath);
        _faceAngry = Resources.Load<Sprite>(FaceAngryPath);
    }

    /// <summary>换看板表情并维持 duration 秒（unscaled）后回原脸；重复触发刷新计时。</summary>
    void ShowBoardFace(Sprite face, float duration = FaceHoldSeconds)
    {
        if (face == null) return;
        var hall = GuildHallUI.Instance;
        var img = hall != null ? hall.boardCharacterImage : null;
        if (img == null || !hall.BoardIsDefaultReceptionist) return;
        if (_faceCo != null) StopCoroutine(_faceCo);
        _faceRestore = img.sprite;
        img.sprite = face;
        _faceCo = StartCoroutine(CoRestoreBoardFace(duration));
    }

    IEnumerator CoRestoreBoardFace(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        _faceCo = null;
        RestoreBoardFace();
    }

    void RestoreBoardFace()
    {
        if (_faceCo != null) { StopCoroutine(_faceCo); _faceCo = null; }
        var img = GuildHallUI.Instance != null ? GuildHallUI.Instance.boardCharacterImage : null;
        if (img == null || _faceRestore == null) return;
        // 看板图中途被 Turn 换掉时（sprite 已不是表情图）不还原，避免盖掉玩家选的人物
        if (img.sprite == _faceShy || img.sprite == _faceAngry)
            img.sprite = _faceRestore;
        _faceRestore = null;
    }

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
        if (!_alive.Contains(this)) _alive.Add(this);
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(TalkLoop());
    }

    void OnDisable()
    {
        _alive.Remove(this);
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        _faceCo = null;   // 场景切换会停协程：句柄清掉，表情恢复目标一并作废（看板图由 GuildHallUI 自管）
        _faceRestore = null;
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
            bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            bubbleText.resizeTextForBestFit = false;
            bubbleText.alignByGeometry = false;
            bubbleText.alignment = TextAnchor.MiddleCenter;
            // 【2026-10-11 主人拍板】气泡两行字挤在一起（截图「想打听情报？点我就行。」）→ 行距抬到统一下限。
            // 气泡不在 GameFonts.ApplyToHierarchy 的调用链上，所以这里单独对齐同一口径（只增不减）。
            if (bubbleText.lineSpacing < GameFonts.MinLineSpacing)
                bubbleText.lineSpacing = GameFonts.MinLineSpacing;
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
            _lines.Add(DefaultLines[i]);
    }

    /// <summary>咨询台：优先说「今日可做 / 功能介绍」；【2026-10-18 彩蛋批次】加情境分支与稀有台词。</summary>
    void OnReceptionistClicked()
    {
        EnsureFaces();

        // 连戳计数要在 _busy 拦截之前统计：打字期间狂点若全被吞，永远凑不齐 5 次
        if (Time.unscaledTime - _lastPokeTime <= PokeWindowSeconds) _pokeClicks++;
        else _pokeClicks = 1;
        _lastPokeTime = Time.unscaledTime;

        bool angry = _pokeClicks >= PokeAngryClicks;
        if (_suppressed) return;
        if (_busy && !angry) return;   // 上一句还在说：只记连戳，不插话；凑满 5 次的生气句必须插播

        string line;
        if (angry)
        {
            _pokeClicks = 0;
            // 【2026-10-20 主人拍板】连点生气句改为从 AngryPokeLines 随机取（原来固定一句）
            line = AngryPokeLines[Random.Range(0, AngryPokeLines.Length)];
            ShowBoardFace(_faceAngry, 1.5f);
            PokeMotion(true);          // 生气＝左右躲闪
        }
        else
        {
            line = PickReceptionistLine();
            ShowBoardFace(_faceShy);   // 单次招呼＝害羞脸
            PokeMotion(false);         // 常规＝上下轻浮
        }

        // 只停说话链路（TalkLoop/SpeakOnceThenResume 内联执行，停外层即全停）；
        // 不能用 StopAllCoroutines——会把表情还原协程一起杀掉，表情卡住不回原脸
        if (_loop != null) StopCoroutine(_loop);
        _busy = false;
        _loop = StartCoroutine(SpeakOnceThenResume(line));
    }

    /// <summary>点击咨询台的台词：深夜 / 连败安慰 / 稀有（3%）/ 常规池。
    /// 【2026-10-18 主人拍板】前台小姐姐人设＝御姐、傲娇：从容慵懒、口嫌体正直、偶尔毒舌。</summary>
    string PickReceptionistLine()
    {
        if (TavernEggState.IsLateNight)
            return "这么晚还不休息？……嗯，本小姐也还没困。";
        if (TavernEggState.DefeatStreak >= TavernEggState.ComfortStreakThreshold)
            return "又输了？……过来坐下，缓口气再走，谁让你难过了。";
        // 【2026-10-20 主人拍板】点击回退池从 Tips 换成「戳一戳」专属 PokeLines（Tips 留给轮播）
        if (Random.value < 0.03f)
            return PickPokeLine(PokeRareLines);
        return PickPokeLine(PokeLines);
    }

    /// <summary>从池里取一句，尽量不与上一句重复（最多重试 4 次）；顺手记进 _lastPokeLine。</summary>
    string PickPokeLine(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        string line = pool[Random.Range(0, pool.Length)];
        for (int i = 0; i < 4 && pool.Length > 1 && line == _lastPokeLine; i++)
            line = pool[Random.Range(0, pool.Length)];
        _lastPokeLine = line;
        return line;
    }

    /// <summary>
    /// 【2026-10-20 主人拍板】戳一戳轻动作：常规 RequestPat，生气 RequestShake。
    /// 守卫与 ShowBoardFace 一致 —— 看板被 Turn 换成佣兵立绘时（BoardIsDefaultReceptionist=false）绝不动。
    /// 动作期间 PortraitMotion 自己会停掉同节点的 PortraitIdleMotion 呼吸，播完复位并恢复。
    /// </summary>
    void PokeMotion(bool angry)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null || !hall.BoardIsDefaultReceptionist) return;
        var img = hall.boardCharacterImage;
        if (img == null) return;
        var motion = PortraitMotion.EnsureOn(img.rectTransform);
        if (motion == null) return;
        if (angry) motion.RequestShake();
        else motion.RequestPat();
    }

    // 御姐口吻：功能性信息也带着从容和一点点居高临下的关照
    static readonly string[] Tips =
    {
        "今日行程？点「冒险」就好，其他交给本小姐。",
        "邮件都替你收好了，不看可会后悔哦。",
        "佣兵？酒馆里那群粗人，本小姐帮你盯着呢。",
        "想看自己长进了多少？角色页自己看。",
        "有事来找我——不过，最好是有意思的事。",
        // —— 2026-10-18 彩蛋批次扩充 ——
        "老板娘那张嘴啊，心比谁都软，哼。",
        "摸老板娘的头会长好感……啧，谁告诉你的？",
        "裂隙的情报嘛……想知道？表现好一点。",
        "累了就回来坐坐，本小姐这里永远有位子。",
        "佣兵们私下叫我小百通——呵，只叫对了名字而已。",
    };

    static readonly string[] RareLines =
    {
        "老板娘的酒？一半出自我的手哦。嘘，这可是机密。",
        "会长年轻时也是冒险者……想知道细节？以后说给你听。",
    };

    // 【2026-10-20 主人拍板】「戳一戳」专属台词池：只供玩家点击前台小姐时用，
    // 与上面的 Tips / RareLines 分开 —— 那两个是 TalkLoop 自动轮播的功能提示池，不许混用、不许删。
    // 人设沿用 :61 口径：前台小姐姐＝御姐、傲娇（从容慵懒、口嫌体正直、偶尔毒舌）。
    static readonly string[] PokeLines =
    {
        "……手拿开。本小姐可不是摆设。",
        "有事说事，动手动脚的，扣你委托金哦。",
        "哼，胆子不小。看在你还算顺眼的份上。",
        "再戳，今天的推荐委托就没你的份了。",
        "本小姐很忙的——……不过，也不是不能理你。",
        "啧，黏人的猫。算了，让你戳一下。",
        "这么闲？裂隙那边可不等人哦。",
        "手，收回去。……三秒，我数着呢。",
        "你倒是熟得快。才认识几天？",
        "别闹。被人看见，本小姐的威严还要不要了。",
        "……行了行了，戳够了没？再戳我可要记仇了。",
        "想引起我注意？直说就好，本小姐又不是不通人情。",
    };

    // 连点 PokeAngryClicks 次的生气句（第 1 条为原有那句，保留）
    static readonly string[] AngryPokeLines =
    {
        "……手，收回去。本小姐生气的时候，可不温柔哦。",
        "第三次了。你是真的不怕死呢。",
        "好啊——今天的委托，你自己去跑。",
    };

    // 3% 稀有句
    static readonly string[] PokeRareLines =
    {
        "……其实，也不讨厌啦。别告诉别人。",
        "戳够了吗？……那，轮到我戳回去了。",
    };

    /// <summary>
    /// 【2026-10-18 彩蛋批次】外部触发前台小姐姐说一句话（连败回城安慰等）。
    /// 找当前存活实例插播；正在播的内容会被打断并复位状态。没有实例返回 false。
    /// </summary>
    public static bool SayFromReceptionist(string line)
    {
        if (string.IsNullOrEmpty(line) || _suppressed) return false;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var t = _alive[i];
            if (t == null) { _alive.RemoveAt(i); continue; }
            if (t._loop != null) t.StopCoroutine(t._loop);
            t._busy = false;   // 打断 SpeakLine 时 _busy 可能残留，强制复位
            t._loop = t.StartCoroutine(t.SpeakOnceThenResume(line));
            return true;
        }
        return false;
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
        CaptureBaseSize();
        RectTransform bubbleRt = bubbleRoot != null ? bubbleRoot.transform as RectTransform : null;
        string display = SpeechBubbleFit.Apply(bubbleRt, bubbleText, line, _baseBubbleSize);
        if (bubbleRoot != null) bubbleRoot.SetActive(true);
        if (bubbleText != null) bubbleText.text = "";

        float delay = 1f / Mathf.Max(1f, charsPerSecond);
        for (int i = 1; i <= display.Length; i++)
        {
            if (bubbleText != null)
                bubbleText.text = display.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
        if (bubbleText != null)
            bubbleText.text = display;

        yield return new WaitForSeconds(holdAfterFinish);
        yield return new WaitForSeconds(idleHideDelay);
        HideBubble();
        _busy = false;
    }

    void HideBubble()
    {
        if (bubbleText != null) bubbleText.text = "";
        if (bubbleRoot != null)
        {
            CaptureBaseSize();
            SpeechBubbleFit.ResetSize(bubbleRoot.transform as RectTransform, _baseBubbleSize);
            bubbleRoot.SetActive(false);
        }
    }

    void CaptureBaseSize()
    {
        if (_baseSizeCaptured || bubbleRoot == null) return;
        var rt = bubbleRoot.transform as RectTransform;
        if (rt != null && rt.sizeDelta.x > 8f && rt.sizeDelta.y > 8f)
            _baseBubbleSize = rt.sizeDelta;
        _baseSizeCaptured = true;
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
