using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备替换确认弹窗（2026-10-05 主人拍板；2026-10-06 改走<b>预制体</b>）。
///
/// 主人原话：「只会抽中高级的直接替换低级的，然后低级的变成材料，不过需要弹出个弹窗告诉玩家是否替换」
/// → 拿到新装备、且同部位已经有旧件时，**先把新旧摆在一起给玩家看**，由玩家决定：
///   · 换上新的 → 新件入包，旧件走 <see cref="GridBackpackSystem.ScrapEquip"/> 折成强化石（低级的变成材料）
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
/// <para>强弱判据只有 <see cref="EquipCompare"/> 一处，这里只负责显示。</para>
/// <para>玩家一直不点时**默认「留着旧的」** —— 保守方向，绝不会弄丢玩家身上已有的装备。</para>
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

        float t = 0f;
        while (!ui._answered && t < TimeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        bool result = ui._answered ? ui._answer : false;
        ui.Hide();
        onDone?.Invoke(result);
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
        // 与 EvacuateConfirmPopup 同一套口径：自带 Overlay Canvas，不挂在 BattleUI 下、不随场景销毁
        DontDestroyOnLoad(go);
        return go.GetComponent<EquipReplaceConfirmUI>() ?? go.AddComponent<EquipReplaceConfirmUI>();
    }

    void Awake()
    {
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
        // 点空白处＝不冒险，按「留着旧的」处理（与超时同一个保守方向）
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(() => Answer(false));
        }
    }

    /// <summary>
    /// 只<b>填数据</b>，不碰排版：所有位置 / 字体 / 图片都是预制体里主人调好的。
    /// </summary>
    void Fill(EquipInstance newEq, EquipInstance oldEq)
    {
        BindRefs();
        Wire();

        if (_title != null) _title.text = "换上新装备吗？";

        // 2026-10-11 主人拍板：着色判据与 Verdict 文案**同一出口**（EquipCompare.NewBetter）——
        // 旧写法 newBetter 只看 Compare（品质/星级/强化分），会出现「文案说新的更强、颜色却是橙」的分裂。
        bool newBetter = EquipCompare.NewBetter(newEq, oldEq);
        if (_verdict != null)
        {
            _verdict.text = EquipCompare.Verdict(newEq, oldEq);
            _verdict.color = newBetter ? new Color(0.55f, 0.95f, 0.60f) : new Color(0.95f, 0.72f, 0.45f);
            _verdict.lineSpacing = MultiLineLineSpacing;   // 判定文案折行时同样拉开行距
        }

        FillColumn(_root.transform, "ColumnNew", newEq);
        FillColumn(_root.transform, "ColumnOld", oldEq);

        GameFonts.ApplyToHierarchy(_root.transform);
        if (_root != null)
        {
            _root.SetActive(true);
            transform.SetAsLastSibling();
        }
    }

    void FillColumn(Transform scope, string columnName, EquipInstance eq)
    {
        var col = FindDeep(scope, columnName);
        if (col == null)
        {
            Debug.LogError($"[EquipReplaceConfirm] 预制体里缺少节点 {columnName}");
            return;
        }

        string name = eq != null && !string.IsNullOrEmpty(eq.equipName) ? eq.equipName : "（空）";
        SetText(col, "Name", name);
        // 2026-10-11 主人拍板：稀有度文案走装备自己的真实档位（EquipUiText.RarityName），
        // 旧写法走 SkillRarityUtil(DraftPool.MapRarity) 会把「史诗(紫)」说成「传说」——
        // 名字按稀有度上色后就会变成「紫色的传说」，自相矛盾。
        // 2026-10-11 主人拍板：强化等级为 0 时**不显示「+0」**（主人原话「如果没有加就不要显示+0」）
        SetText(col, "Info", eq == null ? "-"
            : eq.enhanceLevel > 0
                ? $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}　+{eq.enhanceLevel}"
                : $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}");
        SetText(col, "Desc", eq != null ? DraftPool.FormatEquipDesc(eq) : "");

        var infoText = Text_(FindDeep(col, "Info"));
        if (infoText != null && eq != null) EquipUiText.ApplyRarityColor(infoText, eq.rarity);

        // 2026-10-10 主人拍板：左右两列的属性居中显示、属性字放大 ——
        // 预制体里 Desc 是 19 号左对齐（看着跟旁边居中的 Name/Info 不齐），运行时统一：
        // Name/Info 24 居中，Desc（主手 / 攻击+N 等属性行）24 号 UpperCenter。不动预制体文件。
        var nameT = Text_(FindDeep(col, "Name"));
        if (nameT != null)
        {
            nameT.alignment = TextAnchor.MiddleCenter;
            // 2026-10-11 主人拍板：装备名字的字色跟稀有度一致（与 Info 行同一个色表）
            if (eq != null) EquipUiText.ApplyRarityColor(nameT, eq.rarity);
        }
        if (infoText != null) { infoText.alignment = TextAnchor.MiddleCenter; infoText.fontSize = 24; }
        var descT = Text_(FindDeep(col, "Desc"));
        if (descT != null)
        {
            descT.alignment = TextAnchor.UpperCenter;
            descT.fontSize = 24;
            descT.lineSpacing = MultiLineLineSpacing;   // 多行时行距拉开，别挤成一坨
        }

        // 图标走项目唯一出口，不自己拼路径
        var icon = FindDeep(col, "Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            Sprite sp = eq != null ? EquipIcons.Resolve(eq) : null;
            icon.sprite = sp;
            icon.enabled = sp != null;
        }

        // 2026-10-10 主人：新获得的装备图标右上角要有个「新」字 —— 只有 ColumnNew 有，ColumnOld 明确关掉
        if (columnName == "ColumnNew")
            EnsureNewBadge(col, icon);
        else
            HideNewBadge(col);
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
            rt.anchoredPosition = new Vector2(10f, 8f);          // 骑在角上，略往外偏
            rt.sizeDelta = new Vector2(51f, 45f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.text = "新";
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            // 没了红底，白字直接压在图标上会看不清 → 补一圈黑描边（Outline 只描字，不是底）
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
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
