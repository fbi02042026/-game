using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备替换确认弹窗（2026-10-05 主人拍板；2026-10-06 改走<b>预制体</b>）。
///
/// 主人原话：「只会抽中高级的直接替换低级的，然后低级的变成材料，不过需要弹出个弹窗告诉玩家是否替换」
/// → 拿到新装备、且同部位已经有旧件时，**先把新旧摆在一起给玩家看**，由玩家决定：
///   · 换上新的 → 新件入包，旧件走 <see cref="BattleBagSystem.ScrapEquip"/> 折成强化石（低级的变成材料）
///   · 留着旧的 → 新件同样折成强化石（不占背包，也不白白吞掉这次奖励）
///
/// <para><b>2026-10-06 主人二次拍板</b>：「用项目里的资源把替换装备的那个弹窗拼出来然后生成个预制体，
/// 把'换上新装备吗'那个字放到弹窗的上方 不要挡住弹窗」。
/// → 外观<b>不再运行时用纯色块拼</b>，改为加载预制体
/// <c>Resources/Prefabs/Battle/EquipReplaceConfirmPopup</c>
/// （由 <c>Assets/Editor/EquipReplaceConfirmPrefabBuilder.cs</c> 单点生成，
/// 资源全部取自项目现有美术：撤离弹窗同款 Panel 底图 + 确定/取消按钮）；
/// 标题「换上新装备吗？」在预制体里就挂在 <b>Panel 顶边之外</b>，不占面板内部空间、不挡内容。
/// 本类只负责<b>填数据</b>与按钮回调，一行排版都不写。</para>
///
/// <para>🔴 <b>2026-10-12 立：排版真值在预制体里，且禁止重跑生成器菜单。</b>
/// 该预制体已被手工调整（实际 600×680，还多了 biaoti / Tile / Icon (1) ×2 四个手工节点），
/// <c>EquipReplaceConfirmPrefabBuilder</c> 的常量<b>早已过时</b>。重跑菜单会把手工节点抹掉、
/// 排版打回旧版 —— 要调排版就<b>直接改预制体 YAML</b>（脚本与备份见
/// <c>.workbuddy/backup/20261012-equipreplace-prefab/</c>）。
/// 属性采用「一条一行」竖排：文本由 <c>DraftPool.FormatEquipDescLines</c> 逐条给出，这里用 <c>\n</c> 拼接。</para>
///
/// <para>【2026-10-25 主人拍板「战力只要有提升就是新的装备好」】强弱判据 = 战力净差
/// <see cref="PowerCalc.EquipSwapPowerDelta"/> 一处（判定行文案 / 着色 / 战力行 / 超时自动选全同源）；
/// 旧判据 <see cref="EquipCompare"/>（品质&gt;星级&gt;强化）不再服务本弹窗。</para>
/// <para>【2026-10-16 主人原话「如果玩家没点按钮 自动替换成战力高的」】玩家一直不点（超时）时
/// 改按战力自动选强的一件（<see cref="AutoPickByPower"/>）。
/// 【2026-10-20 主人原话「不要给玩家关闭」】点空白处不再关闭弹窗，玩家必须二选一。</para>
/// </summary>
public class EquipReplaceConfirmUI : MonoBehaviour
{
    /// <summary>玩家多久不点就按「留着旧的」处理（防协程永远挂着）。</summary>
    const float TimeoutSec = 60f;

    /// <summary>
    /// 多行文本的上下行距（2026-10-10 主人：「多行字的时候字的上下间距拉开一点」）。
    /// Unity 默认 1.0，1.3 是明显松一档又不至于散；要再松/再紧改这一个常量。
    /// 只作用在会折行的文本（Desc / Verdict），单行文本设了也看不出差别。
    /// </summary>
    const float MultiLineLineSpacing = 1.3f;

    /// <summary>
    /// 预制体路径 —— 与 <c>EquipReplaceConfirmPrefabBuilder.PrefabPath</c> <b>必须同值</b>。
    /// 改一边忘了另一边 = 运行时加载不到。
    /// </summary>
    public const string PrefabPath = "Prefabs/Battle/EquipReplaceConfirmPopup";

    static EquipReplaceConfirmUI _inst;
    GameObject _root;
    Text _title;
    Text _verdict;
    Button _replaceBtn;
    Button _keepBtn;
    Button _dimBtn;

    System.Action<bool> _onDone;
    bool _answered;
    bool _answer;

    /// <summary>
    /// 弹窗并等玩家选择。超时或异常都返回 false（= 留着旧的）。
    /// 调用方用 <c>yield return CoShow(...)</c> 接住。
    /// </summary>
    public static IEnumerator CoShow(EquipInstance newEq, EquipInstance oldEq, System.Action<bool> onDone)
    {
        var ui = Ensure();
        if (ui == null)
        {
            // UI 建不起来就按「留着旧的」放行 —— 绝不能卡住抽奖协程
            Debug.LogError("[EquipReplaceConfirm] 弹窗建不起来，按「留着旧的」处理");
            onDone?.Invoke(false);
            yield break;
        }

        ui._answered = false;
        ui._answer = false;
        ui.Fill(newEq, oldEq);

        // 【2026-10-20 主人拍板】引导局抽到装备：替换确认弹窗也带引导 —— 圈住「换上新的」按钮，
        // 教玩家把新武器装上，点了才继续。非引导局不受影响（不弹提示）。
        TutorialHintUI guideHint = null;
        if (BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun
            && ui._replaceBtn != null)
        {
            guideHint = TutorialHintUI.Ensure();
            guideHint.ShowHard("点「换上新的」，把这件新武器装上！", ui._replaceBtn.GetComponent<RectTransform>());
        }

        float t = 0f;
        while (!ui._answered && t < TimeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (guideHint != null) guideHint.Hide();
        bool result = ui._answered ? ui._answer : AutoPickByPower(newEq, oldEq);
        // 【2026-10-12 主人报「点了换上新的，装备却没换上」】→ 三处诊断日志，下次一测就现形：
        // 看这里是「玩家选择=…」还是「超时」—— 若主人点了按钮但这里打的是超时，说明 Answer 没被触发（点击被吃/绑定丢失）。
        // ⚠️ 别把它写回跨行插值字符串：C# 9 报 CS8967（非逐字插值串里不能有换行）—— 拆成两个串再拼。
        string how = ui._answered
            ? "玩家选择=" + (result ? "换上新的" : "留着旧的")
            : "超时 " + TimeoutSec + "s 未选择，按战力自动选=" + (result ? "换上新的" : "留着旧的");
        Debug.Log($"[EquipReplaceConfirm] 弹窗结束：{how}｜新={newEq?.equipName} 旧={oldEq?.equipName}");
        ui.Hide();
        onDone?.Invoke(result);
    }

    /// <summary>
    /// 【2026-10-16 主人原话「如果玩家没点按钮 自动替换成战力高的」】超时/未选择时的兜底方向：
    /// 不再一刀切「留着旧的」，改按战力自动选强的一件。
    /// 【2026-10-20 方案 B】判据与弹窗「战力 ±N」、头顶飘字同一真值 <see cref="PowerCalc.EquipSwapPowerDelta"/>
    /// （换装净差，含顶掉旧件）；旧件为空 → 新件直接穿。
    /// 【2026-10-26 主人拍板「加了属性肯定得加战力」配套】d=0 打平时不再保守留旧，
    /// 退回 <see cref="EquipCompare"/> 品质&gt;星级&gt;强化&gt;词条数分高下（同分才留旧）——
    /// 否则「旧件 0 战力垃圾 vs 新件高品质减伤/吸血装」永远换不上，卡死装备成长。
    /// </summary>
    static bool AutoPickByPower(EquipInstance newEq, EquipInstance oldEq)
    {
        if (oldEq == null) return true;
        int d = PowerCalc.EquipSwapPowerDelta(newEq);
        if (d > 0) return true;
        if (d < 0) return false;
        // 【2026-10-26 主人拍板「加了属性肯定得加战力」配套】战力打平时（两件都没进口径的词条，
        // 或增量互相抵消）不再一刀切留旧的 —— 旧件可能是同品质垃圾、新件是高品质减伤/吸血装，
        // 永远换不上会卡装备成长。退回 EquipCompare 的品质>星级>强化>词条数口径分高下（同分留旧）。
        return EquipCompare.Compare(newEq, oldEq) > 0;
    }

    static EquipReplaceConfirmUI Ensure()
    {
        if (_inst != null) return _inst;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过（铁律 14）：没预制体就没得谈，交给上层按「留着旧的」放行
            Debug.LogError($"[EquipReplaceConfirm] 找不到预制体 Resources/{PrefabPath}，" +
                           "请先在团结菜单跑 Tools → 装备 → 生成替换装备确认弹窗预制体");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "EquipReplaceConfirmPopup";
        // 【2026-10-14 主人拍板「以后的界面都要走摄像机模式」】prefab 自带的 Overlay Canvas
        // 实例化后套全项目统一规范（Screen Space - Camera + 925 层 + 根铺满），
        // 与 EvacuateConfirmPopupUI 同口径——不再裸 Overlay。DDOL 相机重绑已有场景钩子兜住。
        var canvas = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.EquipReplaceConfirm);
        // 与 EvacuateConfirmPopup 同一套口径：不挂在 BattleUI 下、不随场景销毁
        DontDestroyOnLoad(go);
        return go.GetComponent<EquipReplaceConfirmUI>() ?? go.AddComponent<EquipReplaceConfirmUI>();
    }

    void Awake()
    {
        if (_inst != null && _inst != this)
        {
            Destroy(gameObject);
            return;
        }
        _inst = this;
        BindRefs();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    void BindRefs()
    {
        if (_root == null) _root = transform.Find("Root")?.gameObject ?? gameObject;
        _title = Text_(FindDeep(_root.transform, "Title"));
        _verdict = Text_(FindDeep(_root.transform, "Verdict"));
        _replaceBtn = FindDeep(_root.transform, "ReplaceButton")?.GetComponent<Button>();
        _keepBtn = FindDeep(_root.transform, "KeepButton")?.GetComponent<Button>();
        _dimBtn = FindDeep(_root.transform, "Dim")?.GetComponent<Button>();
    }

    void Wire()
    {
        if (_replaceBtn != null)
        {
            _replaceBtn.onClick.RemoveAllListeners();
            _replaceBtn.onClick.AddListener(() => Answer(true));
        }
        if (_keepBtn != null)
        {
            _keepBtn.onClick.RemoveAllListeners();
            _keepBtn.onClick.AddListener(() => Answer(false));
        }
        // 【2026-10-20 主人原话「出现替换装备那个窗口时不要给玩家关闭」】
        // 点空白处不再当「留着旧的」关弹窗 —— 玩家必须点「换上新的」或「留着旧的」二选一。
        // Dim 节点保留（它还要挡住点击穿透到底下面板），只是不再绑关闭回调。
        // 60 秒超时按战力自动选是 2026-10-16 的既有拍板，不受本条影响。
    }

    /// <summary>
    /// 只<b>填数据</b>，不碰排版：所有位置 / 字体 / 图片都是预制体里主人调好的。
    /// </summary>
    void Fill(EquipInstance newEq, EquipInstance oldEq)
    {
        BindRefs();
        Wire();

        if (_title != null) _title.text = "换上新装备吗？";
        Debug.Log($"[EquipReplaceConfirm] 弹窗打开：新={newEq?.equipName}({newEq?.templateId})｜旧={oldEq?.equipName}({oldEq?.templateId})");

        // 【2026-10-25 主人拍板「战力只要有提升就是新的装备好」】判定行文案 / 着色 / 战力行
        // 三者同源：唯一判据 = EquipSwapPowerDelta 换装净差（>0 新强 / <0 旧强 / =0 差不多），
        // 与超时自动选 AutoPickByPower、头顶飘字 / TotalPower 同一真值。
        // 旧判据 EquipCompare（品质>星级>强化）不看属性数值，曾出现「战力 -6 却说新的更强」的
        // 自相矛盾（2026-10-25 主人截图报障），弹窗不再用它；背包排序等场景仍可调用 EquipCompare。
        // 2026-10-11 的「着色与文案同一出口」拍板不变，只是出口从 EquipCompare 换成战力净差。
        int d = PowerCalc.EquipSwapPowerDelta(newEq);
        bool newBetter = d > 0;
        if (_verdict != null)
        {
            // 【2026-10-13 改版】底部判定行不再塞「战力 ±N」（主人原话「不要在下面」「有点挤」）。
            // 战力差放进「新装备」栏内（见 FillColumn 的 ColumnNew 分支）。这里只留强弱结论 + 颜色。
            _verdict.text = d > 0 ? "新的更强" : d < 0 ? "现在这件更好" : "两件差不多";
            _verdict.color = newBetter ? new Color(0.55f, 0.95f, 0.60f) : new Color(0.95f, 0.72f, 0.45f);
        }

        // 战力 ±N，准备塞进「新装备」栏（主人拍板：不要在底部判定行）
        // 【2026-10-16 主人拍板】涨改成亮黄（RarityPalette.Gold #FFD959，原绿 PowerGain）；减仍红。
        // 【2026-10-20 方案 B】换成换装真值 EquipSwapPowerDelta（含顶掉旧件的净差），与头顶飘字/TotalPower 同源。
        // 2026-10-25 起 d 在上方已算好（判定行同源），这里直接复用，不重复模拟。
        string powerLine = d >= 0
            ? "<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.Gold) + ">战力 +" + d + "</color>"
            : "<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.PowerLoss) + ">战力 -" + (-d) + "</color>";

        FillColumn(_root.transform, "ColumnNew", newEq, oldEq, powerLine);
        FillColumn(_root.transform, "ColumnOld", oldEq, null, null);

        GameFonts.ApplyToHierarchy(_root.transform);
        if (_root != null)
        {
            _root.SetActive(true);
            transform.SetAsLastSibling();
        }
    }

    void FillColumn(Transform scope, string columnName, EquipInstance eq, EquipInstance compareTo, string powerLine)
    {
        var col = FindDeep(scope, columnName);
        if (col == null)
        {
            Debug.LogError($"[EquipReplaceConfirm] 预制体里缺少节点 {columnName}");
            return;
        }

        // 【2026-10-14 主人拍板】该部位没有旧装备时，右侧栏（ColumnOld）写「无」
        //（原「（空）」），不静默空白也不开天窗。
        string name = eq != null && !string.IsNullOrEmpty(eq.equipName) ? eq.equipName : "无";
        SetText(col, "Name", name);
        // 2026-10-11 主人拍板：稀有度文案走装备自己的真实档位（EquipUiText.RarityName），
        // 旧写法走 SkillRarityUtil(DraftPool.MapRarity) 会把「史诗(紫)」说成「传说」——
        // 名字按稀有度上色后就会变成「紫色的传说」，自相矛盾。
        // 2026-10-11 主人拍板：强化等级为 0 时**不显示「+0」**（主人原话「如果没有加就不要显示+0」）
        SetText(col, "Info", eq == null ? ""
            : eq.enhanceLevel > 0
                ? $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}　+{eq.enhanceLevel}"
                : $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}");
        // 2026-10-12 主人拍板：属性「一条一行、竖着往下排」，不再挤成一长串靠自动折行。
        // 条目内容与抽卡卡面同一处真源（DraftPool.FormatEquipDescLines），这里只换分隔符。
        // 【2026-10-13 改版】ColumnNew 传 compareTo=旧装备 → 同属性自动加 ▲/▼ 对比箭头。
        var descLines = eq != null
            ? DraftPool.FormatEquipDescLines(eq, columnName == "ColumnNew" ? compareTo : null)
            : new List<string>();
        SetText(col, "Desc", descLines.Count > 0 ? string.Join("\n", descLines.ToArray()) : "");

        var infoText = Text_(FindDeep(col, "Info"));
        if (infoText != null && eq != null) EquipUiText.ApplyRarityColor(infoText, eq.rarity);

        // 【2026-10-12 主人拍板】「不要代码动态修改，直接去改预制体」
        // → Name / Info / Desc 的**字号、对齐、行距、高度全部写在 EquipReplaceConfirmPrefabBuilder 里**，
        //   那个生成器是排版的唯一真源，本方法一行排版都不碰。要调排版：改 Builder → 重跑生成菜单。
        // 运行时只做两件事：填文本（Desc 用 \n 一条一行）+ 按稀有度上色（颜色是数据，不是排版）。
        var nameT = Text_(FindDeep(col, "Name"));
        if (nameT != null)
        {
            // 2026-10-11 主人拍板：装备名字的字色跟稀有度一致（与 Info 行同一个色表）
            if (eq != null) EquipUiText.ApplyRarityColor(nameT, eq.rarity);
        }

        // 图标走项目唯一出口，不自己拼路径
        var icon = FindDeep(col, "Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            Sprite sp = eq != null ? EquipIcons.Resolve(eq) : null;
            icon.sprite = sp;
            icon.enabled = sp != null;
        }

        // 2026-10-14 主人拍板：Iconbg（装备格子框）按装备稀有度换图，三色三档。
        // 白=普通 / 蓝=稀有（绿蓝紫都归它）/ 橙金=只有传奇。
        ApplyIconBg(col, eq);

        // 【2026-10-14 主人拍板】战力 ±N 移到 Iconbg 框里最下方的新节点 zhanli，
        // 只有「新获得装备」那栏有（旧装备栏不给）。不再塞进 Desc 文本末尾。
        ApplyPowerLine(col, columnName == "ColumnNew" ? powerLine : null);

        // 2026-10-10 主人：新获得的装备图标右上角要有个「新」字 —— 只有 ColumnNew 有，ColumnOld 明确关掉
        if (columnName == "ColumnNew")
            EnsureNewBadge(col, icon);
        else
            HideNewBadge(col);
    }

    /// <summary>
    /// 装备格子框（Iconbg）按稀有度换图 —— 三色三档，2026-10-14 主人拍板。
    ///
    /// <para>素材位置：<c>Assets/Resources/UI/Battle/zhaungbeitihuan/</c>（主人已放好，textureType=8 Sprite）。
    /// 白=普通 / 蓝=稀有 / 橙金=传奇。<b>只有传奇是橙金</b>（主人原话）；五档枚举里
    /// 绿/蓝/紫都归蓝框 —— 与战斗 HUD 头像框（<c>MercHireSession.LoadPortraitFrame</c>）
    /// 同一套三档思路，两套框图别混用。</para>
    ///
    /// <para>取不到资源只报 LogError 不兜底（铁律 12/14：不让缺资源的弹窗开天窗）。</para>
    /// </summary>
    const string IconBgDir = "UI/Battle/zhaungbeitihuan/";

    static void ApplyIconBg(Transform col, EquipInstance eq)
    {
        var bg = FindDeep(col, "Iconbg")?.GetComponent<Image>();
        if (bg == null) return;

        if (eq == null)
        {
            // 没有装备时不上框，避免留一张白底空框
            bg.sprite = null;
            bg.enabled = false;
            return;
        }

        bg.enabled = true;
        bg.sprite = LoadIconBg(eq.rarity);
    }

    /// <summary>
    /// 战力 ±N 写进 Iconbg 框里最下方的 <c>zhanli</c> 节点（2026-10-14 主人拍板：
    /// 「放到 Iconbg 框里最下方，我已经放好位置了，只有新获得装备有」）。
    /// <para><paramref name="powerLine"/> 为 null/空（旧装备栏、或没得算）→ 隐藏该节点，不留空行。
    /// 【2026-10-16 主人拍板】涨 = 亮黄（<see cref="RarityPalette.Gold"/>），跌仍红（PowerLoss）。
    /// 排版（字号/对齐/位置）在预制体里，代码一行不碰。</para>
    /// </summary>
    static void ApplyPowerLine(Transform col, string powerLine)
    {
        var tf = FindDeep(col, "zhanli");

        bool has = !string.IsNullOrEmpty(powerLine);
        if (tf == null)
        {
            // 【2026-10-16 主人拍板「找得到就不要报错」】zhanli 只挂在 ColumnNew 下（旧装备栏
            // 本来就没有这个节点），旧栏走过来 powerLine=null，找不到属正常，静默 return；
            // 只有「真要显示战力行却找不到节点」才是预制体真缺件，保留 LogError 不兜底。
            if (has)
                Debug.LogError("[EquipReplaceConfirm] 预制体里缺少节点 zhanli（战力行），请确认已放进 Iconbg 框内");
            return;
        }

        tf.gameObject.SetActive(has);
        if (!has) return;

        var text = Text_(tf);
        if (text != null) text.text = powerLine;
    }

    static Sprite LoadIconBg(Rarity r)
    {
        string name;
        switch (r)
        {
            case Rarity.Legendary:
                name = "zhuangbeijin"; // 只有传奇 → 橙金
                break;
            case Rarity.Epic:
            case Rarity.Rare:
            case Rarity.Uncommon:
                name = "zhuangbeilan"; // 紫 / 蓝 / 绿 → 蓝
                break;
            default:
                name = "zhuangbeibai"; // 普通 → 白
                break;
        }

        var path = IconBgDir + name;
        var sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            Debug.LogError($"[EquipReplaceConfirm] 找不到装备格子框 {path}，请确认素材已放进 Resources");
        }
        return sp;
    }

    /// <summary>
    /// 「新」角标：挂在 ColumnNew 的 Icon 下、骑在图标右上角。
    /// 【2026-10-11 主人拍板】**不要红底**（主人原话「那个新字不要带红底了 只有一个新字」）→
    /// 预制体已有 NewMark（红底 Image）就禁掉它的 Image 只留文字；运行时补建也不再建 Image，只建白字黑描边。
    /// </summary>
    void EnsureNewBadge(Transform col, Image icon)
    {
        if (col == null) return;
        var exist = FindDeep(col, "NewMark");
        GameObject mark;
        if (exist != null)
        {
            mark = exist.gameObject;
            // 预制体自带的红底：整张禁用，只留「新」字
            var existImg = mark.GetComponent<Image>();
            if (existImg != null) existImg.enabled = false;
        }
        else
        {
            if (icon == null) return;
            var go = new GameObject("NewMark", typeof(RectTransform));
            go.transform.SetParent(icon.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);   // 图标右上角
            rt.pivot = new Vector2(1f, 0.5f);
            // 【2026-10-12 主人拍板】y 8 → 5：新字再往下挪 3 个像素
            rt.anchoredPosition = new Vector2(10f, 5f);
            rt.sizeDelta = new Vector2(51f, 45f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.text = "新";
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            // 【2026-10-12 主人拍板】新字要用红色（此前 10-11 拍板是白字黑描边，主人现在点名要红）
            label.color = new Color(0.92f, 0.15f, 0.15f);
            label.raycastTarget = false;
            // 红字压在图标上也要看得清，黑描边保留
            labelGo.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            label.font = GameFonts.GetChinese();
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            mark = go;
        }
        mark.SetActive(true);
        mark.transform.SetAsLastSibling();
    }

    static void HideNewBadge(Transform col)
    {
        var old = FindDeep(col, "NewMark");
        if (old != null) old.gameObject.SetActive(false);
    }

    void Answer(bool replace)
    {
        Debug.Log($"[EquipReplaceConfirm] 玩家点击：{(replace ? "换上新的" : "留着旧的")}");
        _answer = replace;
        _answered = true;
    }

    void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ——— 小工具 ———

    static void SetText(Transform scope, string nodeName, string text)
    {
        var t = Text_(FindDeep(scope, nodeName));
        if (t != null) t.text = text;
    }

    static Text Text_(Transform t) => t != null ? t.GetComponent<Text>() : null;

    // 2026-10-11 删除本地 RarityName / RarityColor：
    // 两者都走 SkillRarityUtil(DraftPool.MapRarity)，把「史诗(紫)」归并显示成「传说」，
    // 且自己抄了一份色值（与 RarityPalette 对不上）。现在稀有度文案 = EquipUiText.RarityName、
    // 颜色 = EquipUiText.ApplyRarityColor，全项目一处改。旧实现不留（主人口径：不留开关）。

    static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}
