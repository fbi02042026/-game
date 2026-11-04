using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「佣兵入队」横幅：抽中佣兵入队的瞬间播一次（2026-10-12 主人拍板新增）。
///
/// <para>节点：主人已在 BattleUI 预制体里摆好「佣兵入队」（根 Image 提示框 +
/// mask/icon 佣兵立绘 + name 佣兵名称 + Text1「— 加入小队 —」），代码只驱动不改预制体。</para>
///
/// <para>动画：抽中瞬间从左侧屏外划入到中间（0.30s，起步快末端收住）→ **不停步**，
/// 继续向右极慢缓行（1.60s 才挪 50px，越走越慢=在中间稍作停留），后半段才渐隐消失。
/// 【2026-10-27 主人拍板】条是一直往右滑的、滑到中间后变慢然后消失，不是停在中间不动；
/// 同日二改：中间要「稍作停留、要慢点」，不是一下就滑过去——故缓行段拉长到 1.60s、
/// 距离缩到 50px、渐隐挪到后半段（前半段完全不透明，看得见它赖着不走）。</para>
///
/// <para>全程 unscaled 时间（抽奖阶段战斗冻住，timeScale 可能为 0）。</para>
///
/// <para>立绘：<c>Resources/UI/Icons/MercStand/{hireId}.png</c>（2026-10-31 起与全项目立绘同一目录）。
/// 名字颜色按稀有度走 <see cref="RarityPalette"/>（主人：名字要和稀有度对齐）。</para>
///
/// <para>取不到图 / 找不到节点 → LogError 后不播（fail closed，不静默当成功，不兜底造节点）。</para>
/// </summary>
public class MercJoinBannerUI : MonoBehaviour
{
    // ============================================================
    // 动画时长（2026-10-27 主人拍板：一直往右滑，到中间后明显放慢=稍作停留，后半段才淡出）
    // ============================================================
    const float SlideInSec = 0.30f;    // 左侧屏外 → 中间
    const float FadeOutSec = 1.60f;    // 过中间后：向右极慢缓行，总时长（前半段不透明=稍作停留）
    const float CreepDistance = 50f;   // 过中间后继续向右缓行的总距离（px，慢到肉眼近似停留）

    /// <summary>整体上移量（px，2026-11-02 主人拍板「整体有点偏下了 整体往上100」）。
    /// 只在动画里应用、不改预制体摆位：运行位 = 预制体位 + 本值；播完写回预制体原位，
    /// 防止多次播放把偏移累加进 anchoredPosition。</summary>
    const float YOffsetUp = 100f;

    /// <summary>立绘资源目录：按路径加载，不依赖 guid。
    /// 【2026-10-31 瘦身】原读 <c>UI/Icons/佣兵立绘/佣兵立绘_{id}.png</c>，与 <c>UI/Icons/MercStand/{id}.png</c>
    /// 是**同一批图的第二份**（MD5 全同，Resources 里白占 22MB，且重复内容会触发团结签发 56 字符 guid）。
    /// 全项目立绘唯一出口是 MercStand，这里跟着改过去；改完 佣兵立绘 目录就没有运行时加载点了。</summary>
    const string PortraitFolder = "UI/Icons/MercStand/";
    const string PortraitPrefix = "";

    /// <summary>BattleUI 下主人摆好的节点名（2026-10-18 由「佣兵入队」改英文：中文节点名在预制体 YAML 里被转义，排查易误判）。</summary>
    const string NodeName = "MercJoinBanner";

    static MercJoinBannerUI _inst;

    RectTransform _rt;        // 「佣兵入队」根
    Image _icon;              // mask/icon 的 Image（佣兵立绘）
    Text _nameText;           // name（佣兵名称）
    Coroutine _co;
    bool _bound;
    /// <summary>预制体摆好的原位（Bind 时缓存，只跑一次必然干净）。上移偏移以它为基准、播完写回它，
    /// 播放中途被打断（连续抽中两发）也不会把半路位置当原位累加偏移。</summary>
    Vector2 _homePos;
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
        _homePos = rt.anchoredPosition;   // 预制体原位，一次性缓存（此时位置绝没被代码动过）

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

        if (_co != null) StopCoroutine(_co);
        // 【2026-10-14 修】必须在 StartCoroutine **之前**把节点点亮：
        // Bind() 里 rt.gameObject.SetActive(false)，而 Unity 的 StartCoroutine 要求宿主 GameObject 是激活的，
        // 否则报 "Coroutine couldn't be started because the game object '佣兵入队' is inactive!"
        // → 横幅一次都没播过（2026-10-14 Editor.log:19525 实锤），主人看到的是「该出横幅却干等」。
        _rt.gameObject.SetActive(true);
        // 节点已点亮，这里才是挂抬层 Canvas 的安全时机（幂等，见方法注释）
        EnsureOverheadCanvas();
        // 立绘加载 / 文本赋值挪进 CoPlay 开头的「屏外准备帧」（2026-11-02 修「播放卡一下」，见那边注释）
        _co = StartCoroutine(CoPlay(hireId, displayName, rarity));
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
        // 【2026-10-18 二修】Canvas 已预置进 BattleUI 预制体（MercJoinBanner 节点，overrideSorting=995
        // + GraphicRaycaster）。旧写法运行时 AddComponent<Canvas> 在团结引擎下原生侧未建好 →
        // set_overrideSorting 抛 MissingComponentException（Editor.log 31344 实锤），抽奖协程被整条炸死。
        // 现在只取预制体画布设值（幂等），缺失就报错，绝不再 AddComponent。
        var cv = rt.GetComponent<Canvas>();
        if (cv == null)
        {
            Debug.LogError("[MercJoinBanner] 节点缺少预置 Canvas（预制体应带 overrideSorting=995 的画布），横幅不抬层");
            return;
        }
        cv.overrideSorting = true;
        cv.sortingOrder = GameConfig.UiSort.MercJoinBanner;
        if (rt.GetComponent<GraphicRaycaster>() == null)
            Debug.LogError("[MercJoinBanner] 节点缺少预置 GraphicRaycaster（预制体应一并带上）");
        _canvasLifted = true;
    }

    IEnumerator CoPlay(string hireId, string displayName, MercRosterDefs.MercRarity rarity)
    {
        // ── 屏外准备帧（2026-11-02 修「播放展示时卡一下」）──────────────────────
        // 根因：Show 同帧干了三件重活——① Resources.Load 首次同步加载立绘大图（读盘+解码+上传 GPU）；
        // ② SetActive(true)（Play 里）让横幅整棵树含 Mask / 995 抬层 Canvas 首次激活全量重建；
        // ③ 滑入用 Time.unscaledDeltaTime 计步，掉帧那帧 dt 巨大 → 0.30s 的滑入一帧跨掉大半 = 「卡一下+瞬移」。
        // 修法：重活全部集中在本帧，横幅此时摆在屏外（玩家看不见这帧的掉帧），
        //       yield return null 之后才开始滑入 → 起跑帧 dt 恢复正常。
        var portrait = LoadPortrait(hireId);

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

        Vector2 rawPos = _homePos;                    // 预制体原位（Bind 缓存），播完写回它防偏移累加
        // 【2026-11-02 主人拍板】整体往上 100：只应用在运行位，不动预制体
        Vector2 basePos = new Vector2(rawPos.x, rawPos.y + YOffsetUp);
        _rt.anchoredPosition = new Vector2(-offscreen, basePos.y);

        // 渐隐用 CanvasGroup（运行时挂，不改预制体）；每次播放先复位不透明度
        var cg = _rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = _rt.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // 立绘 / 名字赋值也在准备帧做完（后续帧不再因它们触发重建）
        if (_icon != null) _icon.sprite = portrait;
        if (_nameText != null)
        {
            _nameText.text = displayName ?? "";
            _nameText.color = RarityPalette.Get(rarity);   // 名字与稀有度对齐
        }

        yield return null;   // ← 准备帧到此为止：上面的重活都发生在屏外，下一帧才开始滑入

        // ① 左侧屏外 → 中间（起步快、末端收住）
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SlideInSec;
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(-offscreen, basePos.x, e), basePos.y);
            yield return null;
        }

        // ② 不停步但明显放慢：过中间后向右极慢缓行（二次缓出，越走越慢），
        // 前半段保持完全不透明=「稍作停留」，后半段才渐隐
        // 【2026-10-27 主人拍板】要慢点、稍作停留，不是一下就滑过去
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / FadeOutSec;
            float k = Mathf.Clamp01(t);
            float e = 1f - (1f - k) * (1f - k);   // 二次缓出：越走越慢
            cg.alpha = k <= 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;   // 后半段才渐隐
            _rt.anchoredPosition = new Vector2(basePos.x + CreepDistance * e, basePos.y);
            yield return null;
        }
        cg.alpha = 1f;   // 复位，避免下次播放半透明

        _rt.anchoredPosition = rawPos;   // 写回预制体原位（不是带偏移的 basePos，防 YOffset 累加）
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
                       " —— 请确认 Assets/Resources/UI/Icons/MercStand/" + hireId +
                       ".png 是否存在（立绘唯一出口是 MercStand，不再使用 UI/Icons/佣兵立绘）");
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
