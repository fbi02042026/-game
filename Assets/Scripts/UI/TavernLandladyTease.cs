using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 酒馆老板娘立绘彩蛋：点击会哆嗦 + 升级警告；点满 10 次踢出约 1 分钟。
/// 【2026-10-18 彩蛋批次】区域点击：点上半部（头发）＝摸头加好感度（TavernEggState），
/// 其余区域维持乱摸警示链路；长按 1.5 秒有专属台词；酒馆页挂机 60 秒打哈欠。
/// 挂在「人物」立绘节点上（TavernUI 自动挂）。
/// </summary>
[DisallowMultipleComponent]
public class TavernLandladyTease : MonoBehaviour,
    IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public const int KickClicks = 10;
    public const int DimFromClick = 5;
    public const float BanSeconds = 60f;
    /// <summary>立绘归一化高度 ≥ 此值判为「头发/头部」区（摸头加好感），其余走乱摸警示。实测可调。</summary>
    public const float HeadZoneYNorm = 0.68f;
    public const float LongPressSeconds = 1.5f;
    public const float IdleLineSeconds = 60f;
    // 【2026-10-22 主人拍板】点太快有冷却：距上次有效点击不足 RapidClickSeconds 判为连点，
    // 锁 BurstLockSeconds=5 秒，期间点击一律不算（不加怒气、不说话、不动脸、不加好感）；
    // 已累计的怒气不清零，锁定结束后正常点击继续攒
    public const float RapidClickSeconds = 0.8f;
    public const float BurstLockSeconds = 5f;
    const string BanUntilUtcKey = "tavern_landlady_ban_until_utc";

    // —— 表情立绘（2026-10-18 主人补图，Resources 路径加载，同 MercPortraitSprites 通道）——
    const string ShyFacePath = "Art/UI/Tavern/酒吧老板娘害羞";
    const string AngryFacePath = "Art/UI/Tavern/酒吧老板娘生气";
    const float FaceHoldSeconds = 1.2f;

    // 【2026-10-18 主人拍板】老板娘人设＝豪放爽朗、开放自来熟：警告不娇羞，大大咧咧怼回去
    // 【2026-10-22 主人拍板】隐藏怒气值（不显示、不存档）：乱摸点击累加（OnEnable 清零、摸头不加），
    // 台词按怒气分三档从轻到重，同档内随机取、尽量不与上一句重复；原 10 句线性链拆档保留原句
    static readonly string[] WarnLinesLight =
    {
        "哎哎，摸什么摸！酒钱还想不想结了？",
        "胆子挺肥啊，敢占老娘便宜。",
        "再来我可要加酒钱了啊！",
    };
    static readonly string[] WarnLinesMid =
    {
        "说吧，是想喝酒还是想挨揍？",
        "行，有点意思，我记住你了。",
        "真当我不敢动手？",
    };
    static readonly string[] WarnLinesHeavy =
    {
        "最后一遍——手，拿开。",
        "你是不是嫌命太长？",
        "好，胆量我认了，人你也得出去。",
        "滚出去醒醒酒！",
    };
    string _lastWarnLine;   // 上一句警告台词，同档内尽量不重复

    /// <summary>按隐藏怒气值（＝本轮乱摸累计次数）取警告台词：1-3 轻 / 4-6 中 / 7+ 重。</summary>
    string PickWarnLine()
    {
        string[] pool = _clicks <= 3 ? WarnLinesLight : _clicks <= 6 ? WarnLinesMid : WarnLinesHeavy;
        string line = pool[UnityEngine.Random.Range(0, pool.Length)];
        for (int i = 0; i < 4 && pool.Length > 1 && line == _lastWarnLine; i++)
            line = pool[UnityEngine.Random.Range(0, pool.Length)];
        _lastWarnLine = line;
        return line;
    }

    RectTransform _rt;
    Image _img;
    // 【2026-10-20 主人拍板】动作队列抽成共用组件 PortraitMotion（前台小姐戳一戳复用），本类只持有并转发
    PortraitMotion _portraitMotion;
    CanvasGroup _dim;
    Coroutine _pressCo;
    bool _suppressClick;
    float _idleTimer;
    int _clicks;
    bool _kicking;
    float _lastClickTime;   // 上次有效点击时刻（unscaled），配合 RapidClickSeconds 判连点
    float _burstUntil;      // 连点锁定截止时刻（unscaled），锁定期内点击全忽略
    // 表情切换
    Sprite _baseSprite;
    Sprite _shyFace;
    Sprite _angryFace;
    Coroutine _faceCo;
    bool _faceCaptured;

    public static bool IsBanned
    {
        get
        {
            long until = ReadBanUntilUtc();
            if (until <= 0) return false;
            return DateTime.UtcNow.Ticks < until;
        }
    }

    public static float BanRemainingSeconds
    {
        get
        {
            long until = ReadBanUntilUtc();
            if (until <= 0) return 0f;
            double sec = (until - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond;
            return Mathf.Max(0f, (float)sec);
        }
    }

    public static string BanToastMessage()
    {
        int sec = Mathf.CeilToInt(BanRemainingSeconds);
        if (sec <= 0) return "酒醒了？进来进来！这次可给我老实点。";
        return $"被老板娘一脚踹出来了，酒醒再来（还剩{sec}秒）";
    }

    public static void ClearBan()
    {
        PlayerPrefs.DeleteKey(BanUntilUtcKey);
        PlayerPrefs.Save();
    }

    static long ReadBanUntilUtc()
    {
        string s = PlayerPrefs.GetString(BanUntilUtcKey, "");
        if (string.IsNullOrEmpty(s)) return 0;
        return long.TryParse(s, out long v) ? v : 0;
    }

    static void WriteBan(float seconds)
    {
        long until = DateTime.UtcNow.AddSeconds(seconds).Ticks;
        PlayerPrefs.SetString(BanUntilUtcKey, until.ToString());
        PlayerPrefs.Save();
    }

    public static TavernLandladyTease EnsureOn(RectTransform portrait)
    {
        if (portrait == null) return null;
        var tease = portrait.GetComponent<TavernLandladyTease>();
        if (tease == null) tease = portrait.gameObject.AddComponent<TavernLandladyTease>();
        tease.Setup();
        return tease;
    }

    void Awake() => Setup();

    void OnEnable()
    {
        // 每次打开酒馆：本轮点击清零，但踢出冷却保留；表情强制回默认脸
        _clicks = 0;
        _kicking = false;
        _idleTimer = 0f;
        ApplyDim(0f);
        RestoreFace();
    }

    void OnDisable()
    {
        // 页面关闭会停掉所有协程：句柄必须一并清掉，否则下次 Enable 后动画队列不再启动
        // （动作队列的句柄现在由 PortraitMotion 自己清，本类只清自己的）
        _pressCo = null;
        _faceCo = null;
    }

    void Setup()
    {
        _rt = transform as RectTransform;
        _img = GetComponent<Image>();
        if (_img != null) _img.raycastTarget = true;

        // 【2026-10-18 彩蛋批次】点击改走 IPointerClickHandler（要拿落点坐标分区），
        // Button.onClick 拿不到坐标 → 旧实现自动加的 Button 在这里移除（运行时组件，不动预制体）
        var oldBtn = GetComponent<Button>();
        if (oldBtn != null) Destroy(oldBtn);

        _portraitMotion = PortraitMotion.EnsureOn(_rt);
        EnsureDim();
        EnsureFaces();
    }

    // ———— 表情立绘切换：摸头=害羞，乱摸/长按/踢出=生气；用完回默认脸 ————

    void EnsureFaces()
    {
        if (!_faceCaptured)
        {
            _faceCaptured = true;
            _baseSprite = _img != null ? _img.sprite : null;   // 预制体序列化的默认脸
        }
        if (_shyFace == null) _shyFace = Resources.Load<Sprite>(ShyFacePath);
        if (_angryFace == null) _angryFace = Resources.Load<Sprite>(AngryFacePath);
    }

    /// <summary>换表情并维持 duration 秒（unscaled），随后回默认脸；重复触发刷新计时。</summary>
    void ShowFace(Sprite face, float duration = FaceHoldSeconds)
    {
        if (face == null || _img == null || _baseSprite == null) return;
        if (_faceCo != null) StopCoroutine(_faceCo);
        _img.sprite = face;
        _faceCo = StartCoroutine(CoRestoreFace(duration));
    }

    IEnumerator CoRestoreFace(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        _faceCo = null;
        RestoreFace();
    }

    void RestoreFace()
    {
        if (_faceCo != null) { StopCoroutine(_faceCo); _faceCo = null; }
        if (_img != null && _baseSprite != null) _img.sprite = _baseSprite;
    }

    void EnsureDim()
    {
        if (_dim != null) return;
        var tavern = GetComponentInParent<TavernUI>();
        if (tavern == null) return;
        Transform scene = tavern.transform.Find("TavernScene")
            ?? FindDeep(tavern.transform, "TavernScene");
        if (scene == null) return;

        Transform existing = scene.Find("LandladyTeaseDim");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("LandladyTeaseDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(scene, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.05f, 0.02f, 0.02f, 1f);
            img.raycastTarget = false;
        }
        _dim = go.GetComponent<CanvasGroup>();
        if (_dim == null) _dim = go.AddComponent<CanvasGroup>();
        _dim.blocksRaycasts = false;
        _dim.interactable = false;
        _dim.alpha = 0f;
        go.SetActive(true);
    }

    // ———— 点击分流（2026-10-18 彩蛋批次） ————

    public void OnPointerClick(PointerEventData ev)
    {
        if (_suppressClick) { _suppressClick = false; return; }   // 长按触发后吞掉松手的 click
        if (_kicking) return;
        if (IsBanned) { ShowBannedToast(); return; }
        // 【2026-10-22 主人拍板】点击冷却：点太快触发 5 秒锁定，期间点击全不算
        // （怒气/台词/摸头好感都不结算）；锁完从正常节奏继续
        if (Time.unscaledTime < _burstUntil) return;
        if (Time.unscaledTime - _lastClickTime < RapidClickSeconds)
        {
            _burstUntil = Time.unscaledTime + BurstLockSeconds;
            return;
        }
        _lastClickTime = Time.unscaledTime;
        if (IsHeadZone(ev)) { OnHeadPat(); return; }
        OnClicked();
    }

    /// <summary>落点换算到立绘归一化高度，上半部＝头发区（摸头加好感）。</summary>
    bool IsHeadZone(PointerEventData ev)
    {
        if (_rt == null || ev == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, ev.position, ev.pressEventCamera, out Vector2 local))
            return false;
        Rect r = _rt.rect;
        if (r.width <= 1f || r.height <= 1f) return false;
        float ny = (local.y - r.y) / r.height;
        return ny >= HeadZoneYNorm;
    }

    void OnHeadPat()
    {
        _portraitMotion?.RequestPat();
        ShowFace(_shyFace);
        bool granted = TavernEggState.AddFavor(1, out bool leveledUp);
        string line = granted
            ? TavernEggState.HeadPatLine(TavernEggState.FavorStageIndex)
            : TavernEggState.HeadPatOverflowText;
        UIManager.Instance?.ShowToast(line, force: true);
        if (leveledUp)
            UIManager.Instance?.ShowToast($"老板娘越来越稀罕你啦！信赖提升至：{TavernEggState.FavorStageName}", force: true);
    }

    /// <summary>禁闭期点击/摸头统一提示；15% 概率换成额外吐槽变体。</summary>
    void ShowBannedToast()
    {
        string msg = UnityEngine.Random.value < 0.15f
            ? TavernEggState.BannedTeaseLine(BanRemainingSeconds)
            : BanToastMessage();
        UIManager.Instance?.ShowToast(msg, true);
    }

    void OnClicked()
    {
        if (_kicking) return;

        _clicks++;
        _portraitMotion?.RequestShake();
        // 【2026-10-22 主人拍板】表情要有过程：前几下只给提示台词不动脸，
        // 从开始变暗（第 DimFromClick 下）起才沉下生气脸，第 KickClicks 下踢出时保持
        if (_clicks >= DimFromClick)
            ShowFace(_angryFace);

        // 【2026-10-18 修·主人报「点老板娘不弹对话了」】警示语是玩家主动交互的直接反馈，
        // 改走 force=true：不再被酒馆/看板娘气泡压制吞掉（现象就是「点了没反应」）。
        // 【2026-10-22 主人拍板】台词按隐藏怒气值分档从轻到重（PickWarnLine）。
        UIManager.Instance?.ShowToast(PickWarnLine(), force: true);

        if (_clicks >= DimFromClick)
        {
            float t = Mathf.InverseLerp(DimFromClick, KickClicks, _clicks);
            ApplyDim(0.12f + t * 0.38f);
        }

        if (_clicks >= KickClicks)
            StartCoroutine(CoKickOut());
    }

    IEnumerator CoKickOut()
    {
        _kicking = true;
        WriteBan(BanSeconds);
        ApplyDim(0.55f);
        ShowFace(_angryFace, 10f);   // 被赶出去：生气脸保持到页面关闭（OnEnable 会还原默认脸）
        yield return new WaitForSecondsRealtime(0.35f);
        UIManager.Instance?.ShowToast("好——滚出去醒醒酒！一分钟后老娘照旧欢迎你。");

        var tavern = GetComponentInParent<TavernUI>();
        tavern?.HidePage();

        var hub = TownHubController.Instance != null
            ? TownHubController.Instance
            : FindObjectOfType<TownHubController>();
        if (hub != null)
            hub.OpenGuild();
        else
            TavernUI.SetGuildHallOverlayMode(false);

        _clicks = 0;
        _kicking = false;
        ApplyDim(0f);
    }

    // ———— 长按立绘（1.5 秒）：专属台词 + 抖得更狠 ————

    public void OnPointerDown(PointerEventData ev)
    {
        _suppressClick = false;   // 每次新按下都复位：长按后拖出立绘松手时 click 不来，标志不能带到下一次点击
        if (_kicking || IsBanned) return;
        if (_pressCo != null) StopCoroutine(_pressCo);
        _pressCo = StartCoroutine(CoLongPress());
    }

    public void OnPointerUp(PointerEventData ev) => CancelPress();

    public void OnPointerExit(PointerEventData ev) => CancelPress();

    void CancelPress()
    {
        if (_pressCo != null) { StopCoroutine(_pressCo); _pressCo = null; }
    }

    IEnumerator CoLongPress()
    {
        yield return new WaitForSecondsRealtime(LongPressSeconds);
        _pressCo = null;
        _suppressClick = true;   // 松手后的 click 事件吞掉，避免长按后又算一次点击
        var lines = TavernEggState.LongPressLines;
        UIManager.Instance?.ShowToast(lines[UnityEngine.Random.Range(0, lines.Length)], force: true);
        ShowFace(_angryFace);
        _portraitMotion?.RequestBigShake();
    }

    // ———— 挂机彩蛋：酒馆页打开 60 秒无任何输入，老板娘打哈欠 ————

    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.touchCount > 0)
        {
            _idleTimer = 0f;
            return;
        }
        _idleTimer += Time.unscaledDeltaTime;
        if (_idleTimer < IdleLineSeconds) return;
        _idleTimer = 0f;
        var lines = TavernEggState.IdleLines;
        UIManager.Instance?.ShowToast(lines[UnityEngine.Random.Range(0, lines.Length)], force: true);
        _portraitMotion?.RequestPat();
    }

    void ApplyDim(float alpha)
    {
        EnsureDim();
        if (_dim == null) return;
        _dim.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>解禁后第一次进酒馆：微醺晃一下。</summary>
    public IEnumerator CoWelcomeBackDrunk()
    {
        EnsureDim();
        if (_dim != null) _dim.alpha = 0.2f;
        _portraitMotion?.RequestShake();
        UIManager.Instance?.ShowToast("酒醒了？进来进来！这次可给我老实点。");
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            if (_dim != null)
                _dim.alpha = Mathf.Lerp(0.2f, 0f, t / 0.6f);
            yield return null;
        }
        ApplyDim(0f);
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
