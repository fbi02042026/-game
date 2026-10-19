using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「获得技能」弹窗（2026-10-14 主人拍板）：抽中玩家技能时弹出，取代
/// <see cref="DraftRewardDetailUI"/> 的技能分支（SkillNew / SkillUp）。
///
/// <para>预制体已由主人在编辑器摆好：<c>Resources/Prefabs/Battle/huodejineng</c>
/// （技能图标 + 名字 + 简介 + 确认按钮；biaoti「获得技能」与「技能属性/主动技能」
/// 是预制体静态文本，一律不碰）。本类<b>只驱动、不建树</b>：实例化预制体后
/// 只绑定节点 + 填数据，排版一行不碰。</para>
///
/// <para>组织范式与 <see cref="MercObtainPopupUI"/> 完全相同：static _instance + Ensure + CoShow
/// + 唯一关闭出口 <see cref="Close"/> + 无操作自动关兜底协程；实例化范式照抄
/// <see cref="EquipReplaceConfirmUI"/>（ApplyPopup + DontDestroyOnLoad，925 层压过抽奖面板 920）。</para>
///
/// <para>数据口径：技能定义走 <see cref="PlayerSkillDefs.GetById"/>（desc 为空回退 numbers，
/// 与 <see cref="DraftRewardDetailUI"/> 的技能简介同源算式）；图标走
/// <see cref="BattleUI.LoadRunSkillIcon"/>（全项目唯一出口，不自己拼路径）。</para>
/// </summary>
public class SkillObtainPopupUI : MonoBehaviour
{
    /// <summary>预制体路径（ Resources 下，不带后缀）。</summary>
    const string PrefabPath = "Prefabs/Battle/huodejineng";

    /// <summary>无操作自动关兜底（秒）——2026-10-14 主人拍板 4→8，让玩家看清。</summary>
    const float AutoCloseSeconds = 8f;

    /// <summary>Canvas sortingOrder：与预制体 Canvas 一致，压过抽奖面板 920。</summary>
    const int SortOrder = 925;

    static SkillObtainPopupUI _instance;

    GameObject _root;
    Button _dimBtn;
    Button _keepBtn;
    GameObject _skillRoot;
    Text _skillName;
    Text _skillInfo;
    Image _skillIcon;

    System.Action _onClosed;
    bool _closeReq;
    Coroutine _autoCo;

    public static SkillObtainPopupUI Instance => _instance;

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 弹「获得技能」并等到关闭（调用方 yield 它即可）。
    /// 取不到预制体 / 数据缺失时 LogError 后 onClosed 优雅收尾，绝不卡抽奖协程。
    /// </summary>
    public static IEnumerator CoShow(DraftCard card, System.Action onClosed = null)
    {
        var ui = Ensure();
        // DraftCard 是 struct，判空走 IsValid（default 卡 Id 为空即不合法）
        if (ui == null || !card.IsValid) { onClosed?.Invoke(); yield break; }
        ui._onClosed = onClosed;
        ui._closeReq = false;
        if (!ui.Fill(card.Id, card.Title)) { ui.Close(); yield break; }
        // 【2026-10-18 主人拍板】获得类弹窗互斥：技能弹窗要开了，先把开着的佣兵详情弹窗关掉，
        // 别两个叠在一起（玩家点佣兵头像看详情时又抽中技能就会叠，主人报）。
        MercObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
        while (ui._root != null && ui._root.activeSelf) yield return null;
    }

    /// <summary>
    /// 【2026-10-19 主人拍板「点击后弹出获得技能那个弹窗 统一一下」】战斗技槽点击入口：
    /// 点底部技能槽 → 弹<b>同一个</b>「获得技能」弹窗（同一预制体 / 填充 / 8 秒自动关，
    /// 与抽中技能、佣兵头像点弹详情完全一套口径，不另写第二份）。
    /// 互斥同款：开之前先关掉开着的 <see cref="MercObtainPopupUI"/>。
    /// </summary>
    public static void ShowFromId(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        var ui = Ensure();
        if (ui == null) return;
        ui._onClosed = null;
        ui._closeReq = false;
        if (!ui.Fill(skillId, null)) { ui.Close(); return; }
        MercObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
    }

    static SkillObtainPopupUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过：没预制体就没得谈，交给上层 onClosed 收尾
            Debug.LogError("[SkillObtainPopup] 找不到预制体 Resources/Prefabs/Battle/huodejineng，" +
                           "请确认 Assets/Resources/Prefabs/Battle/huodejineng.prefab 存在");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "SkillObtainPopup";
        // 防回归：根上若还挂着旧的 EquipReplaceConfirmUI（主人已删）， Destroy 掉，别让它抢按钮回调
        var stale = go.GetComponent<EquipReplaceConfirmUI>();
        if (stale != null) Destroy(stale);
        var ui = go.GetComponent<SkillObtainPopupUI>();
        if (ui == null) ui = go.AddComponent<SkillObtainPopupUI>();
        // 【2026-10-14 主人拍板「以后的界面都要走摄像机模式」】与 EquipReplaceConfirmPopup 同口径：
        // 实例化后套全项目统一规范（Screen Space - Camera + 925 层 + 根铺满），不裸 Overlay。
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        // 与 EquipReplaceConfirmPopup 同一套口径：不挂在 BattleUI 下、不随场景销毁
        UnityEngine.Object.DontDestroyOnLoad(go);
        return ui;
    }

    void Awake()
    {
        _instance = this;
        Bind();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ============================================================
    // 节点绑定 / 事件 wiring（只认预制体里的既有节点，取不到置空，Fill 里判空报错）
    // ============================================================

    void Bind()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null)
        {
            Debug.LogError("[SkillObtainPopup] 预制体缺少节点 Root（后续 Fill 会跳过，onClosed 收尾）");
            return;
        }

        _dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        _keepBtn = _root.transform.Find("Panel/KeepButton")?.GetComponent<Button>();
        _skillRoot = _root.transform.Find("Panel/Skill")?.gameObject;
        _skillName = _root.transform.Find("Panel/Skill/SkillName")?.GetComponent<Text>();
        _skillInfo = _root.transform.Find("Panel/Skill/Skillxinxi")?.GetComponent<Text>();
        _skillIcon = _root.transform.Find("Panel/Skill/SkillIcon")?.GetComponent<Image>();
    }

    void Wire()
    {
        // 点确认 / 点暗带都关（预制体本身带了 Dim Button）
        if (_keepBtn != null)
        {
            _keepBtn.onClick.RemoveAllListeners();
            _keepBtn.onClick.AddListener(Close);
        }
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
    }

    // ============================================================
    // 开关 / 关闭出口
    // ============================================================

    void OpenRoot()
    {
        if (_root == null) return;
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        var c = GetComponent<Canvas>();
        if (c != null) UICanvasSetup.RefreshPopup(c, SortOrder);
    }

    void StopAutoClose()
    {
        if (_autoCo != null)
        {
            StopCoroutine(_autoCo);
            _autoCo = null;
        }
    }

    IEnumerator CoAutoClose()
    {
        float t = 0f;
        while (t < AutoCloseSeconds)
        {
            if (_closeReq || _root == null || !_root.activeSelf) yield break;
            // 抽奖阶段 timeScale=0：一律 unscaledDeltaTime / WaitForSecondsRealtime
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Close();
    }

    /// <summary>唯一关闭出口：点确认 / 点暗带 / 自动兜底都走这；回调只发一次。</summary>
    void Close()
    {
        _closeReq = true;
        if (_root != null) _root.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】获得类弹窗互斥（反方向）：本弹窗开着时若佣兵详情弹窗要开，先关掉自己。
    /// 提前关等价于玩家点了确认：onClosed 照常回调（只弹 Toast），抽奖协程不会卡。
    /// </summary>
    public static void CloseIfOpen()
    {
        if (_instance != null && _instance._root != null && _instance._root.activeSelf)
            _instance.Close();
    }

    // ============================================================
    // 内容填充（只填数据，不碰排版）
    // ============================================================

    /// <param name="titleFallback">简介名字回退（抽中路径传卡面 Title；技槽点击路径无卡面传 null）。</param>
    bool Fill(string skillId, string titleFallback)
    {
        if (_root == null)
        {
            Debug.LogError("[SkillObtainPopup] 预制体缺少 Root 节点，本次获得技能弹窗跳过（onClosed 已回调，不卡抽奖）");
            return false;
        }

        var def = PlayerSkillDefs.GetById(skillId);
        if (def == null)
        {
            Debug.LogError("[SkillObtainPopup] 技能数据缺失：" + skillId);
            return false;
        }

        // 名字：displayName 为空回退卡面 Title；仍为空 → 报出来，不静默显示空白
        string name = string.IsNullOrEmpty(def.displayName) ? (titleFallback ?? "") : def.displayName;
        if (string.IsNullOrEmpty(name))
            Debug.LogError("[SkillObtainPopup] 技能名字为空：" + skillId);
        SetText(_skillName, "Panel/Skill/SkillName", name);
        // 【2026-10-19 主人拍板】技能名颜色与角色界面技能弹窗（SkillSelectUI）统一：
        // 预制体里是写死的黄色，这里按统一色表刷色，查不到的 id 回退白色。
        if (_skillName != null)
            _skillName.color = SkillSelectUI.NameColorOf(PlayerSkillDefs.IndexOf(skillId));

        // 简介：desc 为空回退 numbers（与 DraftRewardDetailUI.SkillDescOf 同源）
        string desc = string.IsNullOrEmpty(def.desc) ? def.numbers : def.desc;
        SetText(_skillInfo, "Panel/Skill/Skillxinxi", desc);

        // 图标：全项目唯一出口（Resources 优先，编辑器兜 Art 源目录）
        var sp = BattleUI.LoadRunSkillIcon(skillId);
        if (_skillIcon == null)
            Debug.LogError("[SkillObtainPopup] 缺少节点 Panel/Skill/SkillIcon（技能图标不显示）");
        else
        {
            _skillIcon.sprite = sp;             // sp 为 null 时清掉，不留上一次的图
            _skillIcon.enabled = sp != null;
        }
        if (sp == null) Debug.LogError("[SkillObtainPopup] 取不到技能图标 " + skillId);

        return true;
    }

    // ============================================================
    // 小工具：字段为 null 说明 Bind 没绑到，补一条 LogError 指明缺哪个节点
    // ============================================================

    static void SetText(Text t, string nodePath, string value)
    {
        if (t == null)
        {
            Debug.LogError("[SkillObtainPopup] 缺少文本节点 " + nodePath);
            return;
        }
        t.text = value ?? "";
    }
}
