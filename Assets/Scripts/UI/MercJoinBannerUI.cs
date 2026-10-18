using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「佣兵入队」横幅：抽中佣兵入队的瞬间播一次（2026-10-12 主人拍板新增）。
///
/// <para>节点：主人已在 BattleUI 预制体里摆好「佣兵入队」（根 Image 提示框 +
/// mask/icon 佣兵立绘 + name 佣兵名称 + Text1「— 加入小队 —」），代码只驱动不改预制体。</para>
///
/// <para>动画（主人原话「出现的和以前方式一样」= 复刻 <see cref="SlotMachineAgainBanner"/>）：
/// 抽中瞬间从左侧屏外**瞬间划入**到中间（0.30s）→ 慢慢向右滑行 1s → 快速向右消失（0.22s）。
/// 全程 unscaled 时间（抽奖阶段战斗冻住，timeScale 可能为 0）。</para>
///
/// <para>立绘：<c>Resources/UI/Icons/佣兵立绘/佣兵立绘_{hireId}.png</c>（Art 同名图复制入 Resources）。
/// 名字颜色按稀有度走 <see cref="RarityPalette"/>（主人：名字要和稀有度对齐）。</para>
///
/// <para>取不到图 / 找不到节点 → LogError 后不播（fail closed，不静默当成功，不兜底造节点）。</para>
/// </summary>
public class MercJoinBannerUI : MonoBehaviour
{
    // ============================================================
    // 动画时长（与 SlotMachineAgainBanner 同款，主人拍板的「以前方式」）
    // ============================================================
    const float SlideInSec = 0.30f;    // 左侧屏外 → 中间
    const float CruiseSec = 1.00f;     // 中间慢慢向右滑 1 秒
    const float SlideOutSec = 0.22f;   // 快速向右滑走
    /// <summary>慢滑阶段走过的屏宽比例（同 SlotMachineAgainBanner）。</summary>
    const float CruiseScreenRatio = 0.12f;

    /// <summary>立绘资源目录：按路径加载，不依赖 guid。</summary>
    const string PortraitFolder = "UI/Icons/佣兵立绘/";
    const string PortraitPrefix = "佣兵立绘_";

    /// <summary>BattleUI 下主人摆好的节点名（2026-10-18 由「佣兵入队」改英文：中文节点名在预制体 YAML 里被转义，排查易误判）。</summary>
    const string NodeName = "MercJoinBanner";

    static MercJoinBannerUI _inst;

    RectTransform _rt;        // 「佣兵入队」根
    Image _icon;              // mask/icon 的 Image（佣兵立绘）
    Text _nameText;           // name（佣兵名称）
    Coroutine _co;
    bool _bound;
    /// <summary>抬层 Canvas 是否已幂等挂好（见 <see cref="EnsureOverheadCanvas"/>）。</summary>
    bool _canvasLifted;

    // ============================================================
    // 对外接口
    // ============================================================

    /// <summary>播一次入队横幅。hireId = 花名册 HireId（H001/H003…），立绘按它取。</summary>
    public static void Show(string hireId, string displayName, MercRosterDefs.MercRarity rarity)
    {
        var ui = Ensure();
        if (ui == null) return;
        ui.Play(hireId, displayName, rarity);
    }

    static MercJoinBannerUI Ensure()
    {
        if (_inst != null) return _inst;

        var battleUi = BattleUI.Instance;
        if (battleUi == null)
        {
            Debug.LogError("[MercJoinBanner] 不在战斗中（BattleUI 不存在），入队横幅不播");
            return null;
        }

        var nodeTf = FindDeep(battleUi.transform, NodeName);
        if (nodeTf == null)
        {
            Debug.LogError("[MercJoinBanner] BattleUI 下找不到节点「" + NodeName + "」，入队横幅不播");
            return null;
        }

        var rt = nodeTf as RectTransform;
        if (rt == null)
        {
            Debug.LogError("[MercJoinBanner] 节点「" + NodeName + "」不是 RectTransform，入队横幅不播");
            return null;
        }

        _inst = nodeTf.gameObject.GetComponent<MercJoinBannerUI>();
        if (_inst == null) _inst = nodeTf.gameObject.AddComponent<MercJoinBannerUI>();
        _inst.Bind(rt);
        return _inst;
    }

    // ============================================================
    // 绑定（只做一次）
    // ============================================================

    void Bind(RectTransform rt)
    {
        if (_bound) return;
        _bound = true;
        _rt = rt;

        // 【2026-10-18 修】抬层 Canvas 不在这里挂，挪到 <see cref="EnsureOverheadCanvas"/>（播放前、节点已点亮时）。
        // 根因（Editor.log 2026-10-18 实锤）：Bind 跑在 Ensure 里、节点那一刻没进激活层级 →
        // 未激活对象上 AddComponent<Canvas> 的 native 侧没建好 → set_overrideSorting 当场抛
        // MissingComponentException（同 BattleBossHpBar.cs:131-136 记载的 10-16 同款事故），
        // 异常顺 RecruitMerc → TryApplyCard → CoInstantPick 把抽奖协程整条炸死
        // = 主人看到的「引导第二次抽奖没有抽出」（佣兵其实已入队，但横幅/Toast/扣钱/定序全断）。

        var iconTf = rt.Find("mask/icon");
        if (iconTf == null) Debug.LogError("[MercJoinBanner] 「" + NodeName + "」下找不到 mask/icon，立绘位置会空");
        else
        {
            _icon = iconTf.GetComponent<Image>();
            if (_icon == null) Debug.LogError("[MercJoinBanner] " + NodeName + "/mask/icon 上找不到 Image");
        }

        var nameTf = rt.Find("name");
        if (nameTf == null) Debug.LogError("[MercJoinBanner] 「" + NodeName + "」下找不到 name，佣兵名称会空");
        else
        {
            _nameText = nameTf.GetComponent<Text>();
            if (_nameText == null) Debug.LogError("[MercJoinBanner] " + NodeName + "/name 上找不到 Text");
        }

        // 防挡点击：预制体里 Image / Text 默认 raycastTarget=1，这里全部关掉
        SetRaycastTargetRecursive(rt, false);

        // 平时隐藏，等 Show 再亮
        rt.gameObject.SetActive(false);
    }

    // ============================================================
    // 播放
    // ============================================================

    void Play(string hireId, string displayName, MercRosterDefs.MercRarity rarity)
    {
        if (!_bound) return;

        var portrait = LoadPortrait(hireId);
        if (_icon != null) _icon.sprite = portrait;

        if (_nameText != null)
        {
            _nameText.text = displayName ?? "";
            _nameText.color = RarityPalette.Get(rarity);   // 名字与稀有度对齐
        }

        if (_co != null) StopCoroutine(_co);
        // 【2026-10-14 修】必须在 StartCoroutine **之前**把节点点亮：
        // Bind() 里 rt.gameObject.SetActive(false)，而 Unity 的 StartCoroutine 要求宿主 GameObject 是激活的，
        // 否则报 "Coroutine couldn't be started because the game object '佣兵入队' is inactive!"
        // → 横幅一次都没播过（2026-10-14 Editor.log:19525 实锤），主人看到的是「该出横幅却干等」。
        _rt.gameObject.SetActive(true);
        // 节点已点亮，这里才是挂抬层 Canvas 的安全时机（幂等，见方法注释）
        EnsureOverheadCanvas();
        _co = StartCoroutine(CoPlay());
    }

    /// <summary>
    /// 横幅抬层 Canvas 的唯一出口：播放前（节点已 activeInHierarchy）幂等挂
    /// overrideSorting 嵌套 Canvas 到 <see cref="GameConfig.UiSort.MercJoinBanner"/>(995)。
    /// 为什么不放 <see cref="Bind"/>：见 Bind 内【2026-10-18 修】注释 ——
    /// Ensure 期对象未激活，未激活对象上 AddComponent&lt;Canvas&gt; 的 native 侧没建好，
    /// set_overrideSorting 当场抛 MissingComponentException（BattleBossHpBar.cs:131-136 同款实录）。
    /// </summary>
    void EnsureOverheadCanvas()
    {
        if (_canvasLifted || _rt == null) return;
        if (!_rt.gameObject.activeInHierarchy) return;   // 未激活时不挂：等下次 Play 再幂等补
        var rt = _rt;
        // AddComponent<Canvas> 会把 RectTransform 重置成 100×100 中心锚点，先缓存再还原
        Vector2 ap = rt.anchoredPosition, sd = rt.sizeDelta;
        Vector2 aMin = rt.anchorMin, aMax = rt.anchorMax, pv = rt.pivot, ls = rt.localScale;
        var cv = rt.GetComponent<Canvas>() ?? rt.gameObject.AddComponent<Canvas>();
        if (cv == null) return;
        rt.anchoredPosition = ap; rt.sizeDelta = sd;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pv; rt.localScale = ls;
        cv.overrideSorting = true;
        cv.sortingOrder = GameConfig.UiSort.MercJoinBanner;
        // 铁律：每加一个 Canvas 必补 GraphicRaycaster（横幅图元 raycastTarget 全 false，不会挡点击）
        if (rt.GetComponent<GraphicRaycaster>() == null) rt.gameObject.AddComponent<GraphicRaycaster>();
        _canvasLifted = true;
    }

    IEnumerator CoPlay()
    {
        // 【2026-10-18 修】必须取**父级**Canvas：横幅自己挂了抬层 Canvas 后，
        // GetComponentInParent<Canvas>() 会先取到自己（rect.width=横幅自身 517），
        // 屏宽/滑行距离全错 → 从 parent 往上找，拿到 BattleUI 根 Canvas 的真屏宽。
        RectTransform canvasRt = null;
        if (_rt.parent != null)
        {
            var parentCanvas = _rt.parent.GetComponentInParent<Canvas>();
            if (parentCanvas != null) canvasRt = parentCanvas.transform as RectTransform;
        }
        float canvasW = canvasRt != null ? canvasRt.rect.width : Screen.width;
        float half = canvasW * 0.5f;
        float offscreen = half + _rt.rect.width;      // 整块完全在屏外
        float cruiseTo = canvasW * CruiseScreenRatio;

        Vector2 basePos = _rt.anchoredPosition;       // 预制体摆好的中间位置（x=0）
        _rt.gameObject.SetActive(true);

        // ① 左侧屏外 → 中间（起步快、末端收住）
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SlideInSec;
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(-offscreen, basePos.x, e), basePos.y);
            yield return null;
        }

        // ② 慢慢向右滑 1 秒（匀速）
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / CruiseSec;
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(basePos.x, basePos.x + cruiseTo, Mathf.Clamp01(t)), basePos.y);
            yield return null;
        }

        // ③ 快速向右滑走（越走越快）
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SlideOutSec;
            float e = Mathf.Clamp01(t) * Mathf.Clamp01(t);
            _rt.anchoredPosition = new Vector2(
                Mathf.Lerp(basePos.x + cruiseTo, basePos.x + offscreen, e), basePos.y);
            yield return null;
        }

        _rt.anchoredPosition = basePos;
        _rt.gameObject.SetActive(false);
        _co = null;
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    // ============================================================
    // 资源 / 工具
    // ============================================================

    /// <summary>
    /// 取佣兵立绘。Sprite 取不到（png 被按默认贴图导入）时读 Texture2D 现造，
    /// 与 <see cref="JobIconResolver.LoadSprite"/> / SlotMachineAgainBanner 同一套；都失败返回 null（调用方 LogError）。
    /// </summary>
    public static Sprite LoadPortrait(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return null;
        string path = PortraitFolder + PortraitPrefix + hireId;

        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;

        var tex = Resources.Load<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        Debug.LogError("[MercJoinBanner] 取不到立绘 " + path +
                       " —— 请把 Assets/Art/UI/Icons/佣兵立绘/佣兵立绘_" + hireId +
                       ".png 复制到 Resources/UI/Icons/佣兵立绘/");
        return null;
    }

    static void SetRaycastTargetRecursive(Transform t, bool on)
    {
        var g = t.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = on;
        for (int i = 0; i < t.childCount; i++)
            SetRaycastTargetRecursive(t.GetChild(i), on);
    }

    static Transform FindDeep(Transform root, string nodeName)
    {
        if (root == null) return null;
        if (root.name == nodeName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), nodeName);
            if (hit != null) return hit;
        }
        return null;
    }
}
