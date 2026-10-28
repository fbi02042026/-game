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

    [Header("轮播闲置间隔（2026-10-22 主人报「对话框一个接一个没停过」）")]
    /// <summary>说完一句后闲置多久再说下一句（秒，区间内随机）。
    /// 原口径只隔 gapBetweenLines(1.2s) 就接下一句 = 14 句无限连播；现改为看板娘偶尔说一句。</summary>
    public float idleMinSeconds = 25f;
    public float idleMaxSeconds = 45f;

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
    // 【2026-10-22 主人拍板】表情要有过程：连戳前两下只给台词不动脸，第 3 下起才害羞脸
    const int ShyFromClicks = 3;
    // 【2026-10-22 主人拍板】点太快有冷却：距上次有效点击不足 RapidClickSeconds 判为连点，
    // 锁 BurstLockSeconds=5 秒，期间点击一律不算（不加怒气、不说话、不动脸）；怒气不清零
    const float RapidClickSeconds = 0.8f;
    const float BurstLockSeconds = 5f;
    // 【2026-10-25 主人拍板「要加跟老板娘一样的冷却」】戳一戳冷却（同老板娘 TavernLandladyTease
    // 的 BanSeconds=60f 踢出口径）：第 5 次连戳生气即进入冷却，冷却期内点击一律无效
    // （不累计怒气、不说话、不动脸、不动作），防止玩家一直点一直累计；
    // 冷却中每轮只回一句拒绝台词（对齐老板娘「等酒醒了再来！」的拒绝反馈，防误报「点了没反应」）。
    // 冷却只存内存（跟 burst lock 一致），重启清零；老板娘跨进程是因为踢出涉及进店门槛，前台不需要。
    const float PokeCooldownSeconds = 60f;
    const string CooldownLine = "哼，气还没消。等本小姐消气了再说。";
    float _pokeCooldownUntil;   // 冷却截止时刻（unscaled；0=未在冷却）
    bool _cooldownLineSaid;     // 本轮冷却是否已回过拒绝句（每轮只说一次）
    Sprite _faceShy;
    Sprite _faceAngry;
    Sprite _faceRestore;    // 表情恢复目标（切换瞬间的看板图）
    Coroutine _faceCo;
    bool _facesReady;
    int _pokeClicks;
    float _lastPokeTime;
    float _lastCountedClickTime;   // 上次有效点击时刻（unscaled），配合 RapidClickSeconds 判连点
    float _burstUntil;             // 连点锁定截止时刻（unscaled），锁定期内点击全忽略
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
        EnsureBoardPokeButton();
    }

    /// <summary>
    /// 【2026-10-25 主人报「主界面点击人物无反应」】看板立绘本体直接可点。
    /// 根因：戳一戳点击原本绑在 Receptionist 热点上，但看板「show」立绘
    /// （1171×1345，raycastTarget=1）兄弟序在其后、把热区整块盖住 —— 射线永远命中
    /// show，热点 Button 收不到事件（Editor.log [ClickPeek] 证实：点人物 top=show
    /// selected=null，全程无一次命中热点）。
    /// 修法：给 show 运行时补 Button（不改预制体，照抄 Turn 节点的做法），
    /// 走同一条 OnReceptionistClicked 单一出口；佣兵态在 OnReceptionistClicked
    /// 入口守卫拦截（2026-10-20 主人拍板：佣兵态点击保持无响应）。
    /// 调用时机：GuildHallUI.Awake 里 WireHallClicks（106 行，已绑 boardCharacterImage）
    /// 先于 EnsureSpeechBubbleTalker（107 行），本方法在 AddComponent 触发的 Awake 里
    /// 即可拿到引用。
    /// </summary>
    void EnsureBoardPokeButton()
    {
        var hall = GuildHallUI.Instance;
        var img = hall != null ? hall.boardCharacterImage : null;
        if (img == null)
        {
            Debug.LogError("[SpeechBubble] 看板 show 的 Image 未绑定，人物点击接不上（不兜底）");
            return;
        }
        var btn = img.GetComponent<Button>();
        if (btn == null)
        {
            btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;   // 不改美术颜色反馈（同 Turn 做法）
        }
        btn.onClick.RemoveListener(OnReceptionistClicked);
        btn.onClick.AddListener(OnReceptionistClicked);
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
        RefreshLinePool(GuildHallUI.Instance != null ? GuildHallUI.Instance.CurrentBoardId : null);
    }

    // 【2026-10-26 主人拍板「让引导的话也可以让替换的人说」】中性口吻引导池：
    // 看板被换成玩家/佣兵后，轮播的功能提示由台上的人说 —— 前台御姐人设句（DefaultLines）不能塞进佣兵嘴里，
    // 所以功能性信息重写成谁说都不违和的中性版；前台在台时仍用 DefaultLines，原口径不动。
    static readonly string[] BoardGuideLines =
    {
        "欢迎回来，今天做点什么？",
        "体力是有限的，省着点用。",
        "「冒险」就在下面，随时出发。",
        "邮件记得领，别堆着。",
        "酒馆好像有新面孔，去看看？",
        "打完一波还有一波，别急。",
        "过了关就走传送门，别发呆。",
        "装备穿上了就直接显示在身上。",
        "会暴击的冒险者，活得比较久。",
        "体力没了？等着，会自己回来的。",
        "商城以后会有更多好东西。",
        "咨询台的前台小姐，随时在。",
    };

    // 【2026-10-26 主人拍板「某些人会有特定的话 只有换了才知道」】玩家自己的看板专属台词。
    // 玩家不走 merc_lines.csv（加载器只认 H 开头的佣兵 id），走 C# 常量。
    static readonly string[] PlayerBoardLines =
    {
        "我的队伍，我站C位。",
        "今天也元气满满，出发！",
        "看我表演就行。",
    };

    /// <summary>看板人物的专属台词（只有把 TA 换上看板才说得到）：玩家走 C# 常量，佣兵走 merc_lines.csv「主界面」场景。</summary>
    static string[] BoardSpecialLines(string boardId)
    {
        if (string.IsNullOrEmpty(boardId)) return null;
        if (boardId == "player") return PlayerBoardLines;
        var all = MercLineTable.GetAll(boardId, MercLineTable.Scene.Board);
        return all != null && all.Count > 0 ? all.ToArray() : null;
    }

    /// <summary>
    /// 【2026-10-26 主人拍板】看板换人后重刷轮播池（进主界面恢复 + 每次 Turn 换人后都会调）：
    /// 前台在台 = DefaultLines（御姐功能池，原口径）；玩家/佣兵在台 = 中性引导池 + 该人物专属台词混播。
    /// </summary>
    public void RefreshLinePool(string boardId)
    {
        _lines.Clear();
        if (string.IsNullOrEmpty(boardId))
        {
            for (int i = 0; i < DefaultLines.Length; i++)
                _lines.Add(DefaultLines[i]);
            return;
        }
        for (int i = 0; i < BoardGuideLines.Length; i++)
            _lines.Add(BoardGuideLines[i]);
        string[] special = BoardSpecialLines(boardId);
        if (special != null)
            for (int i = 0; i < special.Length; i++)
                _lines.Add(special[i]);
    }

    /// <summary>咨询台：优先说「今日可做 / 功能介绍」；【2026-10-18 彩蛋批次】加情境分支与稀有台词。</summary>
    void OnReceptionistClicked()
    {
        // 【2026-10-26 主人拍板】看板被换成玩家/佣兵后，点 TA 会说话（专属台词优先）——
        // 「特定的话只有换了才知道」的发现入口。原 2026-10-20「佣兵态无响应」口径由本条替代。
        // 此分支同时覆盖热点按钮与看板立绘本体（EnsureBoardPokeButton）两条入口。
        var hallGuard = GuildHallUI.Instance;
        if (hallGuard != null && !hallGuard.BoardIsDefaultReceptionist)
        {
            OnBoardCharacterClicked();
            return;
        }

        // 【2026-10-25 主人拍板】戳一戳冷却（同老板娘踢出口径）：生气后 60 秒内点击一律无效，
        // 不累计怒气、不动脸、不动作；每轮冷却只在第一次点击时回一句拒绝台词
        if (Time.unscaledTime < _pokeCooldownUntil)
        {
            SayCooldownLineOnce();
            return;
        }

        EnsureFaces();

        // 【2026-10-22 主人拍板】点击冷却：点太快触发 5 秒锁定，期间点击全不算
        // （不加怒气、不说话、不动脸）；锁完从正常节奏继续
        if (Time.unscaledTime < _burstUntil) return;
        if (Time.unscaledTime - _lastCountedClickTime < RapidClickSeconds)
        {
            _burstUntil = Time.unscaledTime + BurstLockSeconds;
            return;
        }
        _lastCountedClickTime = Time.unscaledTime;

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
            // 【2026-10-25 主人拍板】生气即进入冷却：60 秒内点击无效（同老板娘踢出 60s）
            _pokeCooldownUntil = Time.unscaledTime + PokeCooldownSeconds;
            _cooldownLineSaid = false;
        }
        else
        {
            line = PickReceptionistLine();
            // 【2026-10-22 主人拍板】表情要有过程：连戳前两下不动脸（台词即提示），攒到第 3 下起才害羞
            if (_pokeClicks >= ShyFromClicks)
                ShowBoardFace(_faceShy);   // 戳了几下＝害羞脸
            PokeMotion(false);             // 常规＝上下轻浮
        }

        // 只停说话链路（TalkLoop/SpeakOnceThenResume 内联执行，停外层即全停）；
        // 不能用 StopAllCoroutines——会把表情还原协程一起杀掉，表情卡住不回原脸
        if (_loop != null) StopCoroutine(_loop);
        _busy = false;
        _loop = StartCoroutine(SpeakOnceThenResume(line));
    }

    /// <summary>
    /// 冷却期拒绝反馈（每轮冷却只说一次）：对齐老板娘「等酒醒了再来！」的口径——
    /// 点击虽然无效，但要给一句拒绝台词让玩家知道是在生气不理人，不是点了没反应。
    /// 只动说话链路：不累计怒气、不动脸、不动作；剧情压制（_suppressed）期间连话也不说。
    /// </summary>
    void SayCooldownLineOnce()
    {
        if (_cooldownLineSaid || _suppressed) return;
        _cooldownLineSaid = true;
        if (_loop != null) StopCoroutine(_loop);
        _busy = false;
        _loop = StartCoroutine(SpeakOnceThenResume(CooldownLine));
    }

    /// <summary>点击咨询台的台词：深夜 / 连败安慰 / 稀有（3%）/ 常规池。
    /// 【2026-10-18 主人拍板】前台小姐姐人设＝御姐、傲娇：从容慵懒、口嫌体正直、偶尔毒舌。</summary>
    string PickReceptionistLine()
    {
        if (TavernEggState.IsLateNight)
            return "这么晚还不休息？……嗯，本小姐也还没困。";
        if (TavernEggState.DefeatStreak >= TavernEggState.ComfortStreakThreshold)
            return "又输了？……过来坐下，缓口气再走，谁让你难过了。";
        // 【2026-10-20 主人拍板】点击回退池从 Tips 换成「戳一戳」专属池（Tips 留给轮播）
        if (Random.value < 0.03f)
            return PickPokeLine(PokeRareLines);
        // 【2026-10-22 主人拍板】台词按隐藏怒气值分档从轻到重：1-2 轻 / 3-4 中 / 5+ 已走生气分支
        string[] pool = _pokeClicks >= ShyFromClicks ? PokeLinesMid : PokeLinesLight;
        return PickPokeLine(pool);
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
    // 【2026-10-22 主人拍板】隐藏怒气值（不显示、不存档）：连戳计数即怒气（5 秒窗口累加、
    // 生气后清零、超窗重新计），台词按怒气分档从轻到重：1-2 轻 / 3-4 中 / 5+ 生气档；
    // 同档内随机取、尽量不与上一句重复。原 12 句混合池拆档保留原句。
    static readonly string[] PokeLinesLight =
    {
        "……手拿开。本小姐可不是摆设。",
        "有事说事，动手动脚的，扣你委托金哦。",
        "本小姐很忙的——……不过，也不是不能理你。",
        "啧，黏人的猫。算了，让你戳一下。",
        "这么闲？裂隙那边可不等人哦。",
        "想引起我注意？直说就好，本小姐又不是不通人情。",
    };
    static readonly string[] PokeLinesMid =
    {
        "哼，胆子不小。看在你还算顺眼的份上。",
        "再戳，今天的推荐委托就没你的份了。",
        "手，收回去。……三秒，我数着呢。",
        "你倒是熟得快。才认识几天？",
        "别闹。被人看见，本小姐的威严还要不要了。",
        "……行了行了，戳够了没？再戳我可要记仇了。",
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
    /// 【2026-10-26 主人拍板】点看板上的人物（非前台）：说 TA 的专属台词（没有专属就走中性引导池）。
    /// 前台那套戳一戳机制（怒气分档 / 害羞生气表情 / 动作 / 冷却）不适用 —— 佣兵没有那张表情图，
    /// 动作表情张冠李戴比不响应更糟；保留 _busy / 剧情压制两个守卫防连点刷屏。
    /// </summary>
    void OnBoardCharacterClicked()
    {
        if (_suppressed || _busy) return;
        string boardId = GuildHallUI.Instance != null ? GuildHallUI.Instance.CurrentBoardId : null;
        string[] special = BoardSpecialLines(boardId);
        string line = special != null && special.Length > 0
            ? PickPokeLine(special)
            : (_lines.Count > 0 ? _lines[Random.Range(0, _lines.Count)] : null);
        if (string.IsNullOrEmpty(line)) return;
        if (_loop != null) StopCoroutine(_loop);
        _busy = false;
        _loop = StartCoroutine(SpeakOnceThenResume(line));
    }

    /// <summary>
    /// 【2026-10-26 主人拍板「只有换了才知道」】Turn 换人过场播完，新上台的人物开腔说一句专属台词，
    /// 这是专属台词的发现时刻。没有专属台词的人物保持安静（不硬凑中性句凑数）。
    /// </summary>
    public static void RevealBoardLine(string boardId)
    {
        if (string.IsNullOrEmpty(boardId) || _suppressed) return;
        string[] special = BoardSpecialLines(boardId);
        if (special == null || special.Length == 0) return;
        string line = special[Random.Range(0, special.Length)];
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var t = _alive[i];
            if (t == null) { _alive.RemoveAt(i); continue; }
            if (t._loop != null) t.StopCoroutine(t._loop);
            t._busy = false;
            t._loop = t.StartCoroutine(t.SpeakOnceThenResume(line));
            return;
        }
    }

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
            // 【2026-10-22 主人报「一个接一个没停过」】真闲置：说完歇 25~45 秒（随机）再轮下一句，
            // 不再 1.2 秒连播。点击插播（SpeakOnceThenResume）不受影响。
            yield return new WaitForSeconds(Random.Range(idleMinSeconds, idleMaxSeconds));
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
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(parent, name);
    }
}
