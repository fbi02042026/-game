using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「获得佣兵」全屏演出（2026-10-20 主人拍板）：
/// 背景淡入 → 立绘三段式动态登场（2026-10-27 晚主人二次拍板：全程慢 3 倍，3 倍大上半身从右侧屏幕外
/// 扫到左侧屏幕外 → 下半身从左扫回右出画 → 从正上方猛砸落位）→ 脚下名字淡入上移 → 点击任意处关闭。
///
/// 预制体：Assets/Resources/Prefabs/Town/MercGainCinematic（Tools → 生成获得佣兵演出预制体 生成/重建）。
/// 节点：BG（全屏底）/ Stand（立绘，pivot 底部中心）/ NamePlate（脚下名字）。
/// 【位置以预制体为准】动画只改 alpha / scale / 相对偏移，落定值 = 预制体里摆的值，
/// 主人挪完节点代码不会掰回去；名字颜色由代码按稀有度刷（RarityPalette 真源），预制体里改色无效。
/// </summary>
public class MercGainCinematicUI : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Town/MercGainCinematic";

    const float BgFadeIn = 0.25f;      // 背景淡入
    const float HeadIn = 0.22f;        // 【登场①基准时长】淡入（实际 = ×SlowMo，之后进入上半身横扫）
    const float BodySlide = 0.5f;      // 【登场②基准时长】下半身横扫（①上半身横扫时长 = (HeadIn+BodySlide*0.5)×SlowMo）
    const float SlamIn = 0.16f;        // 【登场③基准时长】正上方加速猛砸回落（×SlowMo）
    const float SlamSettle = 0.12f;    // 【登场③基准时长】落地一压一弹回正（×SlowMo）
    const float ZoomScale = 3f;        // 起手放大倍数（2026-10-27 主人拍板：放大 3 倍）
    const float SlowMo = 6f;           // 【2026-10-27 晚主人拍板】登场动效整体慢 3 倍后又加长一倍（各段时长 ×此值）
    const float NameDelay = 0.15f;     // 立绘落定后名字延迟
    const float NameRise = 0.3f;       // 名字上移淡入时长
    const float AutoCloseAfter = 8f;   // 无点击兜底自动关（防卡流程）

    Transform _stand;
    Text _name;
    RectTransform _bgRt;
    CanvasGroup _bgCg, _standCg, _nameCg;
    ParticleSystem[] _bgFx;   // 主人在 BG 节点下摆的特效，界面出现时一起播放
    Vector2 _nameBasePos;
    float _bgTargetAlpha;
    Coroutine _co;
    System.Action _onClosed;

    /// <summary>播一场获得佣兵演出；关闭（点击 / 超时）后回调 onClosed。预制体缺失时直接回调不阻塞流程。
    /// subtitle 非空时在名字下方显示副标题（引导入队用「已加入小队」；领取场景不传）。
    /// 【2026-10-20 主人拍板】默认发放的 3 白（路加/马库斯/米娅）**永远不弹演出**——他们开局就在队，
    /// 没有"获得"仪式；此闸加在入口，任何接线点都不会误弹。</summary>
    public static void Show(string hireId, System.Action onClosed, string subtitle = null)
    {
        string resolved = MercRosterDefs.ResolveHireId(hireId) ?? hireId;
        if (System.Array.IndexOf(SaveData.DefaultUnlockedMercIds, resolved) >= 0)
        {
            onClosed?.Invoke();
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[MercGainCinematic] 找不到预制体 {PrefabPath}——先跑 Tools → 生成获得佣兵演出预制体");
            onClosed?.Invoke();
            return;
        }
        var go = Object.Instantiate(prefab);
        var ui = go.GetComponent<MercGainCinematicUI>() ?? go.AddComponent<MercGainCinematicUI>();
        ui.Play(hireId, onClosed, subtitle);
    }

    void Play(string hireId, System.Action onClosed, string subtitle = null)
    {
        _onClosed = onClosed;
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.MercGainCinematic);

        var bg = transform.Find("BG");
        _stand = transform.Find("Stand");
        var nameTr = transform.Find("NamePlate");
        if (bg == null || _stand == null || nameTr == null)
        {
            Debug.LogError("[MercGainCinematic] 预制体缺 BG/Stand/NamePlate 节点，演出跳过");
            Done();
            return;
        }
        _bgRt = bg as RectTransform;

        // 副标题（可选）：挂在 NamePlate 子节点，随名字一起淡入上移；null / 空 → 隐藏
        var subTr = nameTr.Find("SubTitle");
        if (subTr != null)
        {
            var subTxt = subTr.GetComponent<Text>();
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            if (subTxt != null) subTxt.text = subtitle;
            subTr.gameObject.SetActive(hasSub);
        }

        _bgCg = EnsureGroup(bg.gameObject, out _bgTargetAlpha);
        _standCg = EnsureGroup(_stand.gameObject, out _);
        _nameCg = EnsureGroup(nameTr.gameObject, out _);

        // BG 下特效：关掉 Play On Awake、清空残留，等 CoPlay 开场统一播（2026-10-20 主人拍板）
        _bgFx = bg.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in _bgFx)
        {
            var m = ps.main;
            m.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 2026-10-20 主人报「背景拉伸后人物离开台子」：先把立绘/名字/BG 特效贴回背景台子，
        // 再取 _nameBasePos（名字动画基准要取贴台后的位置）
        FitStageAlignment(nameTr);

        _name = nameTr.GetComponent<Text>();
        _nameBasePos = nameTr.GetComponent<RectTransform>().anchoredPosition;

        ApplyPortrait(hireId);
        ApplyName(hireId);

        var btn = bg.GetComponent<Button>() ?? bg.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(Close);
        // 【2026-10-24 主人拍板】点背景只关闭，不要缩放：UICanvasSetup.RefreshPopup 已给全屏
        // BG 按钮自动挂了 UiButtonPressFeedback（按下整屏缩到 0.94 + 变暗一闪），这里压掉。
        UiButtonPressFeedback.Suppress(bg.gameObject);

        _co = StartCoroutine(CoPlay());
    }

    void ApplyName(string hireId)
    {
        if (_name == null) return;
        bool hasRoster = MercRosterDefs.TryGetByHireId(hireId, out var rd);
        string fullName = hasRoster && !string.IsNullOrEmpty(rd.Name) ? rd.Name : hireId;
        _name.text = fullName;
        var rarity = hasRoster ? rd.Rarity : MercRosterDefs.MercRarity.Common;
        var c = RarityPalette.Get(rarity);
        c.a = 1f;
        _name.color = c;
    }

    /// <summary>
    /// 立绘按 hireId 刷（2026-10-20 修·主人报「人物立绘不对」）：预制体 Stand 摆的是 H011
    /// 路加的静态图，此前代码只刷名字从不换图 → 获得谁都显示路加。统一入口
    /// MercPortraitSprites.GetStand（与酒馆招募卡/图鉴同源，预制体摆的 H011 也是这套）；
    /// 只换 sprite 不动排版。取不到 LogError 并清空，不留上一次的图（同 MercObtainPopupUI 口径）。
    /// </summary>
    void ApplyPortrait(string hireId)
    {
        if (_stand == null) return;
        var img = _stand.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("[MercGainCinematic] Stand 节点上没有 Image，立绘不显示");
            return;
        }
        var sp = MercPortraitSprites.GetStand(hireId);
        if (sp == null)
            Debug.LogError("[MercGainCinematic] 取不到佣兵立绘 " + hireId +
                           "（Resources/Icons/MercStand/ 下缺图，需主人补图）");
        img.sprite = sp;
        img.enabled = sp != null;
    }

    /// <summary>
    /// 2026-10-20 主人报「随着背景拉伸，人物离开背景上台子位置了」的贴台适配（本界面专属）：
    /// BG 是拉伸铺满（锚点 0~1 + Simple 拉伸），瘦屏逻辑高会超过 1280，背景图里的台子随拉伸
    /// 上下移动；而 Stand（脚底 -337）/ NamePlate（-437）/ BG 下特效（台子光圈 -292、光线 134）
    /// 都是固定坐标 → 人物飘离台子。
    /// 修法：这些元素的 y 一律乘「BG 实际高 ÷ 设计高 1280」——归一化后钉在背景图同一个点上。
    /// 720×1280 设计分辨率下因子=1，预制体摆的值一个像素不动；只有被拉伸时才补偿。x 居中不动。
    /// </summary>
    void FitStageAlignment(Transform nameTr)
    {
        if (_bgRt == null) return;
        Canvas.ForceUpdateCanvases();
        float h = _bgRt.rect.height;
        if (h <= 0f) h = (transform as RectTransform).rect.height;   // 兜底读根画布
        if (h <= 0f) return;
        float k = h / GameConfig.DESIGN_HEIGHT;
        if (Mathf.Approximately(k, 1f)) return;

        // ① 立绘：pivot 底部中心，脚底 y 随台子等比移动
        var srt = _stand as RectTransform;
        if (srt != null)
            srt.anchoredPosition = new Vector2(srt.anchoredPosition.x, srt.anchoredPosition.y * k);

        // ② 名字：跟脚走（必须在取 _nameBasePos 之前调，见 Play 里的顺序）
        var nrt = nameTr as RectTransform;
        if (nrt != null)
            nrt.anchoredPosition = new Vector2(nrt.anchoredPosition.x, nrt.anchoredPosition.y * k);

        // ③ BG 下特效（台子光圈 BasicZoneYellow / 光线 TreasureChestGlowRays）跟着背景里的位置走
        for (int i = 0; i < _bgRt.childCount; i++)
        {
            var child = _bgRt.GetChild(i);
            var p = child.localPosition;
            child.localPosition = new Vector3(p.x, p.y * k, p.z);
        }
    }

    static CanvasGroup EnsureGroup(GameObject go, out float targetAlpha)
    {
        var img = go.GetComponent<Image>();
        targetAlpha = img != null ? img.color.a : 1f;
        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        return cg;
    }

    IEnumerator CoPlay()
    {
        float t = 0f;
        // ⓪ BG 特效随界面出现一起播放
        foreach (var ps in _bgFx)
            if (ps != null && ps.gameObject.activeInHierarchy)
                ps.Play(true);
        // ① 背景先出现
        while (t < BgFadeIn) { t += Time.unscaledDeltaTime; _bgCg.alpha = _bgTargetAlpha * Mathf.Clamp01(t / BgFadeIn); yield return null; }
        _bgCg.alpha = _bgTargetAlpha;

        // ② 立绘三段式登场（2026-10-27 主人拍板：3 倍大上半身右→左扫 → 左→右扫回看下半身 → 猛砸落位；
        //    覆盖 2026-10-20「立绘不要动态」旧拍板）。只改 alpha / scale / 相对偏移，
        //    落定值 = 预制体里摆的值，主人挪完节点不受影响。
        yield return StartCoroutine(CoPortraitEntrance());

        // ③ 名字从脚下淡入上移
        yield return new WaitForSecondsRealtime(NameDelay);
        yield return StartCoroutine(CoNameIn());

        // ④ 等点击 / 超时（点击即 Close 直接销毁，协程随之结束，无需 _closing 标记）
        float wait = 0f;
        while (wait < AutoCloseAfter)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        Close();
    }

    /// <summary>
    /// 立绘三段式登场（2026-10-27 晚主人二次拍板·全程慢 3 倍 + 完整出画横移版）：
    /// ① 3 倍大淡入，上半身取景从右侧屏幕外一路移到左侧屏幕外；
    /// ② 下半身取景从左侧屏幕外移回右侧屏幕外（换 y 的瞬间在画外，看不见跳变）；
    /// ③ 跳到落点的正上方（完全出上边界）→ 加速猛砸落位，落地一压（0.92）一弹回正，落定值严格还原。
    /// 出画幅度按父容器实际宽高 + 立绘 3 倍视觉尺寸推导，不写死布局尺寸；
    /// 动画只改 alpha / scale / anchoredPosition 相对偏移，结束严格还原预制体值。
    /// </summary>
    IEnumerator CoPortraitEntrance()
    {
        var srt = (RectTransform)_stand;
        Canvas.ForceUpdateCanvases();
        Vector2 basePos = srt.anchoredPosition;             // 预制体摆的落定位置
        float baseScale = srt.localScale.x;                 // 预制体摆的落定缩放（应为 1）
        float w = srt.rect.width;
        float h = srt.rect.height;
        if (w <= 0f || h <= 0f)                             // rect 未就绪的极端兜底，量级对齐设计分辨率
        {
            w = GameConfig.DESIGN_WIDTH;
            h = GameConfig.DESIGN_HEIGHT;
        }
        var prt = _stand.parent as RectTransform;           // 出画边界按父容器实际宽高算
        float halfW = prt != null ? prt.rect.width * 0.5f : GameConfig.DESIGN_WIDTH * 0.5f;
        float halfH = prt != null ? prt.rect.height * 0.5f : GameConfig.DESIGN_HEIGHT * 0.5f;
        const float ExitMargin = 40f;                       // 完全出画后再多挪的余量
        float visualHalfW = w * ZoomScale * 0.5f;           // 3 倍时立绘半宽
        // pivot=底部中心：底边即 anchored y；完全出画 = 视觉边缘越过父容器边界再多挪余量
        float exitRight = (halfW - basePos.x) + visualHalfW + ExitMargin;   // ①起点：完全出右边界
        float exitLeft  = -(halfW + basePos.x) - visualHalfW - ExitMargin;  // ①终点/②起点：完全出左边界
        float dropY = (halfH - basePos.y) + ExitMargin;     // ③起点：正上方完全出上边界
        float upperY = -(ZoomScale - 0.9f) * h;             // ①上半身取景：立绘顶部落到脚底上方 0.9h 处
        float lowerY = -(ZoomScale - 2.2f) * h;             // ②下半身取景：露出腿脚
        // 全程慢 3 倍后再长一倍（2026-10-27 晚主人拍板：SlowMo=6）
        float durSweep1 = (HeadIn + BodySlide * 0.5f) * SlowMo;
        float durSweep2 = BodySlide * SlowMo;
        float durSlam = SlamIn * SlowMo;
        float durSettle = SlamSettle * SlowMo;
        float t;

        // ① 3 倍大·上半身从右侧屏幕外 → 左侧屏幕外（起点已在画外，淡入只是保险）
        srt.localScale = Vector3.one * baseScale * ZoomScale;
        srt.anchoredPosition = basePos + new Vector2(exitRight, upperY);
        t = 0f;
        while (t < durSweep1)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durSweep1);
            _standCg.alpha = Mathf.Clamp01(t / (HeadIn * SlowMo));
            float ease = u * u * (3f - 2f * u);             // smoothstep：两端缓、中间匀速扫过
            srt.anchoredPosition = basePos + new Vector2(Mathf.Lerp(exitRight, exitLeft, ease), upperY);
            yield return null;
        }
        _standCg.alpha = 1f;

        // ② 下半身从左侧屏幕外 → 右侧屏幕外（y 换档发生在画外）
        t = 0f;
        while (t < durSweep2)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durSweep2);
            float ease = u * u * (3f - 2f * u);
            srt.anchoredPosition = basePos + new Vector2(Mathf.Lerp(exitLeft, exitRight, ease), lowerY);
            yield return null;
        }
        srt.anchoredPosition = basePos + new Vector2(exitRight, lowerY);

        // ③ 跳到落点正上方（完全出画）→ 加速猛砸回落 + 落地一压一弹
        srt.anchoredPosition = basePos + new Vector2(0f, dropY);
        t = 0f;
        while (t < durSlam)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durSlam);
            float ease = u * u;                              // easeIn：越落越快，砸感
            srt.localScale = Vector3.one * baseScale * Mathf.Lerp(ZoomScale, 0.92f, ease);
            srt.anchoredPosition = basePos + new Vector2(0f, Mathf.Lerp(dropY, 0f, ease));
            yield return null;
        }
        srt.localScale = Vector3.one * baseScale * 0.92f;    // 落地瞬间压扁
        srt.anchoredPosition = basePos;                      // 落定 = 预制体位置
        t = 0f;
        while (t < durSettle)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durSettle);
            // 0.92 → 过冲 1.04 → 回正 1，正弦半波 + 线性抬升模拟弹回（u=1 时恰好回到 baseScale）
            float s = baseScale * (0.92f + (1.04f - 0.92f) * Mathf.Sin(u * Mathf.PI) + (u * 0.08f));
            srt.localScale = Vector3.one * s;
            yield return null;
        }
        srt.localScale = Vector3.one * baseScale;            // 回正 = 预制体缩放
        _standCg.alpha = 1f;
    }

    IEnumerator CoNameIn()
    {
        float t = 0f;
        while (t < NameRise)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / NameRise);
            float ease = 1f - (1f - u) * (1f - u);
            _nameCg.alpha = ease;
            var rt = _name.transform.GetComponent<RectTransform>();
            rt.anchoredPosition = _nameBasePos + new Vector2(0f, (1f - ease) * 26f);
            yield return null;
        }
        _nameCg.alpha = 1f;
        _name.transform.GetComponent<RectTransform>().anchoredPosition = _nameBasePos;
    }

    /// <summary>点击任意处 / 超时 → 直接关闭。
    /// 【2026-10-22 主人拍板】原实现是 0.2 秒全体淡出，主人看「背景像被缩放了一下」；改为不要任何动画，点击立即关。</summary>
    public void Close()
    {
        Done();
    }

    void Done()
    {
        var cb = _onClosed;
        _onClosed = null;
        Destroy(gameObject);
        cb?.Invoke();
    }
}
