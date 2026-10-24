using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结果界面（预制体版，2026-10-24 主人拍板「接下代码还有自适应」）：
/// 战斗结束弹出「胜利 / 失败」大画面 + 特效，淡入弹出 → 停留 → 淡出 → 回调后续流程；
/// 期间<b>点击任意处可跳过</b>（主人拍板：「弹出界面后点击任意地方关闭界面并返回到冒险界面」——
/// 跳过的是演出，后续流程照旧：普通关胜利→回冒险页；阵亡→遗产结算）。
///
/// <para>预制体：<c>Resources/Prefabs/Battle/战斗结果</c>（主人手工搭：根节点自带
/// ScreenSpaceCamera Canvas + GraphicRaycaster（铁律 10）+ CanvasScaler 720×1280；
/// Root → Panel(620×560) → 胜利/失败(各 600×462，图 + Button + 世界空间粒子子树)；
/// Root → Dim 全屏遮罩。两个胜负节点默认全 active，由本类按胜负切显隐）。</para>
///
/// <para>接入（统一门，2026-10-24 主人拍板「正式关卡/支线/活动副本都走，无限裂隙不走」）：<br/>
/// · 胜利：<see cref="BattleManager.FinishStageAfterPortalReached"/> 里 <c>Mode.UsesBattleResultScreen</c> 门；<br/>
/// · 失败：<see cref="BattleManager.OnHeroDead"/> 同一道门。<br/>
/// 无限裂隙（每层通关即有累计奖励）直接走自己的结算续层链；引导局阵亡重进战斗、
/// 通关走传送门撤离结算，均不播；终章追查关不播（通关接揭底剧情，保持节奏）。</para>
///
/// <para>铁律合规：29/30 + AGENTS §2.3 —— 预制体与图都走路径加载，零 guid 依赖
/// （预制体 meta 与图的 meta 都是团结 56 字符标识，不扫不修）；大图按路径<b>主动设图</b>
/// （预制体 m_Sprite 引用断，这是需求不是兜底）；32 —— 不写任何 sizeDelta /
/// anchoredPosition.x / Image.type，动效只动 localScale / CanvasGroup.alpha，用完即毁。</para>
/// </summary>
public static class BattleResultScreen
{
    /// <summary>预制体路径 —— 与预制体实际位置必须同值，改一边忘另一边 = 运行时加载不到。</summary>
    public const string PrefabPath = "Prefabs/Battle/战斗结果";

    /// <summary>
    /// 弹出战斗结果界面。onDone 在界面销毁后回调（后续流程如回城/结算放这里）。
    /// 非战斗场景 / 预制体缺失 → 不弹，直接走 onDone（缺真值不卡流程，铁律 14）。
    /// </summary>
    public static void Show(bool victory, Action onDone)
    {
        if (!Application.isPlaying || !GameSceneGate.IsBattle)
        {
            onDone?.Invoke();
            return;
        }

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到预制体 Resources/{PrefabPath}，本局不播结果界面");
            onDone?.Invoke();
            return;
        }

        // 【2026-10-24 编译修复】Object 因 using System + using UnityEngine 双引用产生歧义（CS0104），
        // 显式限定 UnityEngine.Object；本类非 MonoBehaviour，不能裸调 Instantiate。
        var go = UnityEngine.Object.Instantiate(prefab);
        var ui = go.GetComponent<BattleResultScreenUI>() ?? go.AddComponent<BattleResultScreenUI>();
        ui.Play(victory, onDone);
    }

    /// <summary>
    /// 失败选择界面（2026-10-24 主人拍板「失败界面加复活和返回，复活先扣钻石」）：
    /// 弹出「失败」画面后<b>不自动收</b>，等玩家二选一 ——
    /// 【复活（X钻）】→ onRevive() 扣钻复活；返回 false（钻石不足，toast 已提示）则界面停留可再点；
    /// 【返回】→ onGiveUp()（死亡收尾 → 遗产结算）。
    /// 选择模式下 Dim 与失败大图不接「点击任意处跳过」（防误触放弃）；胜利界面照旧走 <see cref="Show"/>。
    /// </summary>
    public static void ShowFailureChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp)
    {
        if (!Application.isPlaying || !GameSceneGate.IsBattle)
        {
            onGiveUp?.Invoke();
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到预制体 Resources/{PrefabPath}，跳过复活选择直接死亡收尾");
            onGiveUp?.Invoke();
            return;
        }
        var go = UnityEngine.Object.Instantiate(prefab);
        var ui = go.GetComponent<BattleResultScreenUI>() ?? go.AddComponent<BattleResultScreenUI>();
        ui.PlayChoice(revivePrice, onRevive, onGiveUp);
    }
}

/// <summary>结果界面驱动（协程宿主：摆节点 + 接线 + 时间轴；结束自毁）。</summary>
public class BattleResultScreenUI : MonoBehaviour
{
    // 【2026-10-24 编译修复】以下常量原写在 BattleResultScreen（私有），唯一使用方是本类
    // → CS0103（InDur/HoldDur/OutDur/SkipOutDur/ArtDir 找不到）。按「同一语义只留一个出口」
    // 搬到唯一消费方，不改任何数值。
    /// <summary>胜利/失败大图资源目录（按路径设图）。</summary>
    const string ArtDir = "Art/UI/battle/战斗结果/";
    /// <summary>淡入/弹出时长（秒）。</summary>
    const float InDur = 0.28f;
    /// <summary>停留时长（秒）。</summary>
    const float HoldDur = 1.2f;
    /// <summary>正常淡出时长（秒）。</summary>
    const float OutDur = 0.3f;
    /// <summary>点击跳过的淡出时长（秒）——跳过要利落，比正常淡出短。</summary>
    const float SkipOutDur = 0.12f;

    RectTransform _panel;
    CanvasGroup _cg;
    Action _onDone;
    bool _skipped;
    bool _finished;

    // —— 失败选择模式（复活/返回）专用状态 ——
    bool _choiceMode;
    bool _chosen;
    bool _choseGiveUp;
    Func<bool> _onRevive;
    Action _onGiveUp;

    /// <summary>按胜负摆节点 + 自适应 + 接线 + 开播。</summary>
    public void Play(bool victory, Action onDone)
    {
        _onDone = onDone;

        // 统一适配入口（2026-10-24 主人拍板「接下自适应」）：一次修预制体三处硬伤 ——
        // ① worldCamera 为空（不绑相机整块不显示）；② 根节点 localScale=(0,0,0)（EnsureRootStretch 归一）；
        // ③ CanvasScaler 写死高对齐 720×1280（瘦长屏 1080×2400 逻辑宽只剩 576 < Panel 620 会切面板），
        //    按 DesignAspectLetterbox.ResolveUiMatch() 换宽对齐；④ override 层级 985（盖 HUD/技槽/抽奖面板）。
        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.BattleResult);

        var rootT = transform.Find("Root");
        var panelT = rootT != null ? rootT.Find("Panel") : null;
        _panel = panelT as RectTransform;
        if (rootT == null || _panel == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体缺 Root/Panel 节点（结构被改过？），本局不播结果界面");
            Destroy(gameObject);
            onDone?.Invoke();
            return;
        }

        // 胜利/失败是 Panel 的直属子节点；特效子树里还有一个叫「胜利」的粒子节点，深度查找会撞名
        var showNode = panelT.Find(victory ? "胜利" : "失败");
        var hideNode = panelT.Find(victory ? "失败" : "胜利");
        if (showNode == null || hideNode == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体 Panel 下缺 胜利/失败 节点，本局不播结果界面");
            Destroy(gameObject);
            onDone?.Invoke();
            return;
        }
        BindSprite(showNode, victory);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);   // 另一个节点的粒子随失活停止（预制体两节点默认全 active）

        // 点击任意处跳过：Dim 全屏钮 + 胜负图上的钮，同一个出口（2026-10-24 主人拍板）
        WireSkip(rootT.Find("Dim"));
        WireSkip(showNode);

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        StartCoroutine(CoPlay());
    }

    /// <summary>
    /// 失败选择模式：摆失败节点（不接跳过）+ 运行时建【复活/返回】双钮 + 开播等抉择。
    /// 按钮为<b>新建的运行时节</b>点（非预制体布局改动，先例 = 2026-10-22 BattleSettlementUI 运行时克隆钮）；
    /// 尺寸/位置只写在这两个新节点上，不碰预制体任何既有节点的 rect（铁律 32）。
    /// </summary>
    public void PlayChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp)
    {
        _choiceMode = true;
        _onRevive = onRevive;
        _onGiveUp = onGiveUp;

        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.BattleResult);

        var rootT = transform.Find("Root");
        var panelT = rootT != null ? rootT.Find("Panel") : null;
        _panel = panelT as RectTransform;
        var showNode = panelT != null ? panelT.Find("失败") : null;
        var hideNode = panelT != null ? panelT.Find("胜利") : null;
        if (rootT == null || _panel == null || showNode == null || hideNode == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体缺 Root/Panel/失败/胜利 节点，跳过复活选择直接死亡收尾");
            Destroy(gameObject);
            onGiveUp?.Invoke();
            return;
        }
        BindSprite(showNode, victory: false);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);

        // 选择模式：Dim 与失败大图上的钮一律不接「点击任意处」（防误触放弃复活机会）
        var dimBtn = rootT.Find("Dim") != null ? rootT.Find("Dim").GetComponent<Button>() : null;
        dimBtn?.onClick.RemoveAllListeners();
        var imgBtn = showNode.GetComponent<Button>();
        imgBtn?.onClick.RemoveAllListeners();

        // 双钮：复活在左（带钻石价），返回在右；底部留白区居中对称
        var reviveGo = BuildChoiceButton(showNode, "ReviveButton", $"复活（{revivePrice}钻）", -125f);
        var backGo = BuildChoiceButton(showNode, "BackButton", "返回", 125f);
        reviveGo.GetComponent<Button>().onClick.AddListener(OnReviveClicked);
        backGo.GetComponent<Button>().onClick.AddListener(OnGiveUpClicked);

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        StartCoroutine(CoPlayChoice());
    }

    /// <summary>新建一个运行时按钮（底色块 + 居中文本，字体走 GameFonts 统一口径）。</summary>
    static GameObject BuildChoiceButton(Transform parent, string name, string label, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(230f, 70f);      // 仅新建节点用，非预制体布局
        rt.anchoredPosition = new Vector2(x, -180f);
        go.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.20f, 0.92f);

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(rt, false);
        trt.sizeDelta = rt.sizeDelta;               // 文本铺满按钮本体
        var txt = txtGo.GetComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 34;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;
        GameFonts.ApplyToHierarchy(go.transform);
        return go;
    }

    void OnReviveClicked()
    {
        if (_chosen) return;
        // 扣费失败（钻石不足，toast 已提示）→ 界面停留，可去凑钻再点
        if (_onRevive == null || !_onRevive()) return;
        _chosen = true;
        _choseGiveUp = false;
    }

    void OnGiveUpClicked()
    {
        if (_chosen) return;
        _chosen = true;
        _choseGiveUp = true;
    }

    IEnumerator CoPlayChoice()
    {
        Vector3 baseScale = _panel.localScale;   // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）

        // 段一：淡入 + 弹出（与正常演出同手感）
        float t = 0f;
        while (t < InDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _panel.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
            yield return null;
        }
        _cg.alpha = 1f;
        _panel.localScale = baseScale;

        // 段二：原地等玩家二选一（不自动收 —— 收了就没有复活机会了）
        while (!_chosen)
            yield return null;

        // 段三：淡出后按选择分流 —— 返回 → 死亡收尾；复活 → 战斗已在 DoReviveHero 恢复，无回调
        t = 0f;
        while (t < OutDur)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = 1f - Mathf.Clamp01(t / OutDur);
            yield return null;
        }
        _cg.alpha = 0f;
        _finished = true;
        Destroy(gameObject);
        if (_choseGiveUp)
            _onGiveUp?.Invoke();
    }

    /// <summary>
    /// 按路径设大图（预制体 m_Sprite 引用断 + 图 meta 是团结 56 字符 guid → 铁律 29/30 走路径）。
    /// 取不到 → LogError 不静默（铁律 14）；界面照播（缺图看得见，不卡流程）。
    /// </summary>
    static void BindSprite(Transform node, bool victory)
    {
        var img = node.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError($"[BattleResultScreen] 节点 {node.name} 上没有 Image，设不了大图");
            return;
        }
        string art = ArtDir + (victory ? "胜利" : "失败");
        var sp = Resources.Load<Sprite>(art);
        if (sp == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到图 Resources/{art}，结果界面将缺图（不静默）");
            return;
        }
        img.sprite = sp;
    }

    void WireSkip(Transform node)
    {
        if (node == null) return;
        var btn = node.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();   // 预制体里本无持久回调，只清运行时残留
        btn.onClick.AddListener(Skip);
    }

    /// <summary>点击跳过：只打断「未结束」的等待段，随后走缩短淡出。已结束后点击无效。</summary>
    public void Skip()
    {
        if (_finished) return;
        _skipped = true;
    }

    IEnumerator CoPlay()
    {
        Vector3 baseScale = _panel.localScale;   // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）

        // 段一：淡入 + 面板缩放弹出（EaseOutBack 过冲）；全程 unscaled —— 战斗结束可能处于定格/慢动作
        float t = 0f;
        while (t < InDur && !_skipped)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _panel.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
            yield return null;
        }

        // 段二：停留（点击可跳过）
        t = 0f;
        while (t < HoldDur && !_skipped)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 段三：淡出（跳过走缩短版；先从弹出中途归位再淡，避免跳过瞬间画面跳变）
        _cg.alpha = 1f;
        _panel.localScale = baseScale;
        float outDur = _skipped ? SkipOutDur : OutDur;
        t = 0f;
        while (t < outDur)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = 1f - Mathf.Clamp01(t / outDur);
            yield return null;
        }
        _cg.alpha = 0f;

        _finished = true;
        Destroy(gameObject);   // 粒子随对象销毁消失（CanvasGroup 淡出管不了世界空间粒子，见任务文档验收 7）
        _onDone?.Invoke();
    }

    /// <summary>回弹缓动（ease-out-back）：中段越过 1 再回来，「砸出来」的手感。</summary>
    static float BackOut(float u)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = u - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
