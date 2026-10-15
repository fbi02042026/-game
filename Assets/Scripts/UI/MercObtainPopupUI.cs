using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「获得佣兵」弹窗（2026-10-14 主人拍板）：抽中佣兵入队时弹出，取代
/// <see cref="DraftRewardDetailUI"/> 的佣兵分支（MercRecruit）。
///
/// <para>【2026-10-15 改】不再随抽中默认弹出 —— 入口移到战斗头像栏：
/// 点佣兵头像弹（<see cref="ShowFromEntry"/>，BattleUI.OnMercAvatarClicked），
/// 抽中当场只流入队横幅 + Toast；引导局在二抽（塔克入队）后教这一步。</para>
///
/// <para>预制体已由主人在编辑器摆好：<c>Resources/Prefabs/Battle/huodeyongbing</c>
/// （立绘 + 名字 + 三星 + 七对属性 + 技能一栏 + 确认按钮）。
/// 本类<b>只驱动、不建树</b>：实例化预制体后只绑定节点 + 填数据，
/// 排版（位置/字号/颜色/静态文本）一行不碰。</para>
///
/// <para>组织范式照抄 <see cref="DraftRewardDetailUI"/>：static _instance + Ensure + CoShow
/// + 唯一关闭出口 <see cref="Close"/> + 无操作自动关兜底协程；实例化范式照抄
/// <see cref="EquipReplaceConfirmUI"/>（ApplyPopup + DontDestroyOnLoad，925 层压过抽奖面板 920）。</para>
///
/// <para>数值口径（2026-10-14 主人拍板）：
/// 生命/攻击/防御走 <see cref="MercRosterDefs.ApplyCombatStats"/>（只随等级）；
/// 法术型佣兵（<see cref="Mercenary.UsesMagicAttack"/>）攻击/防御两行换成「法术攻击/法术防御」（数值同源，只是换标签）；
/// 速度 = 移动速度；暴击 = <see cref="GameConfig.BASE_CRIT_RATE"/>（本项目基础值，未计入星级/装备成长）；
/// 立绘走 <see cref="MercJoinBannerUI.LoadPortrait"/>；技能走 <see cref="MercSkillTable"/>。</para>
/// </summary>
public class MercObtainPopupUI : MonoBehaviour
{
    /// <summary>预制体路径（ Resources 下，不带后缀）。</summary>
    const string PrefabPath = "Prefabs/Battle/huodeyongbing";

    /// <summary>无操作自动关兜底（秒）——2026-10-14 主人拍板 4→8，让玩家看清。</summary>
    const float AutoCloseSeconds = 8f;

    /// <summary>Canvas sortingOrder：与预制体 Canvas 一致，压过抽奖面板 920。</summary>
    const int SortOrder = 925;

    /// <summary>预制体只摆了 3 个满星节点（满星1/满星2/满星3）。</summary>
    const int MaxStarNodes = 3;

    static MercObtainPopupUI _instance;

    GameObject _root;
    Button _dimBtn;
    Button _keepBtn;
    Image _icon;
    /// <summary>四分类类型图标位（Panel/Role，预制体已有节点）。</summary>
    Image _role;
    Text _nameText;
    GameObject[] _stars;
    /// <summary>满星布局的原始位置（Fill 首次记录，重开弹窗从这里重算，不累积偏移）。</summary>
    Vector2[] _starBasePos;

    // 七对属性（Root 整行 GameObject + 值 Text；Label 静态文本不碰）
    GameObject _attrHpRoot; Text _attrHp;
    GameObject _attrAtkRoot; Text _attrAtk;
    GameObject _attrMagicAtkRoot; Text _attrMagicAtk;
    GameObject _attrDefRoot; Text _attrDef;
    GameObject _attrMagicDefRoot; Text _attrMagicDef;
    GameObject _attrSpdRoot; Text _attrSpd;
    GameObject _attrCritRoot; Text _attrCrit;

    GameObject _skillRoot;
    Text _skillName;
    Text _skillInfo;
    Image _skillIcon;

    System.Action _onClosed;
    bool _closeReq;
    Coroutine _autoCo;

    public static MercObtainPopupUI Instance => _instance;

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 弹「获得佣兵」并等到关闭（调用方 yield 它即可）。
    /// 取不到预制体 / 数据缺失时 LogError 后 onClosed 优雅收尾，绝不卡抽奖协程。
    /// </summary>
    public static IEnumerator CoShow(DraftCard card, System.Action onClosed = null)
    {
        var ui = Ensure();
        // DraftCard 是 struct，判空走 IsValid（default 卡 Id 为空即不合法）
        if (ui == null || !card.IsValid) { onClosed?.Invoke(); yield break; }
        ui._onClosed = onClosed;
        ui._closeReq = false;
        if (!ui.Fill(card)) { ui.Close(); yield break; }
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
        while (ui._root != null && ui._root.activeSelf) yield return null;
    }

    /// <summary>
    /// 【2026-10-15 主人拍板】点战斗头像栏的佣兵头像查看详情：弹窗不再抽中默认弹出
    /// （抽中当场只流入队横幅 MercJoinBannerUI + Toast，引导局在二抽后教这一步）。
    /// 数据从局内佣兵条目来（hireId / AssetId / 等级 / 星级），填充、开关、无操作自动关
    /// 与抽中路径完全同一套，不另写第二份。
    /// </summary>
    public static void ShowFromEntry(string hireId, string assetId, int level, int star)
    {
        var ui = Ensure();
        if (ui == null) return;
        var card = new DraftCard
        {
            Kind = DraftCardKind.MercRecruit,
            Id = assetId,
            HireId = hireId,
            MercLevel = Mathf.Max(1, level),
            Star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5)
        };
        if (!ui.Fill(card)) { ui.Close(); return; }
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
    }

    static MercObtainPopupUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过：没预制体就没得谈，交给上层 onClosed 收尾
            Debug.LogError("[MercObtainPopup] 找不到预制体 Resources/Prefabs/Battle/huodeyongbing，" +
                           "请确认 Assets/Resources/Prefabs/Battle/huodeyongbing.prefab 存在");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "MercObtainPopup";
        // 防回归：根上若还挂着旧的 EquipReplaceConfirmUI（主人已删）， Destroy 掉，别让它抢按钮回调
        var stale = go.GetComponent<EquipReplaceConfirmUI>();
        if (stale != null) Destroy(stale);
        var ui = go.GetComponent<MercObtainPopupUI>();
        if (ui == null) ui = go.AddComponent<MercObtainPopupUI>();
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
            Debug.LogError("[MercObtainPopup] 预制体缺少节点 Root（后续 Fill 会跳过，onClosed 收尾）");
            return;
        }

        _dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        _keepBtn = _root.transform.Find("Panel/KeepButton")?.GetComponent<Button>();
        _icon = _root.transform.Find("Panel/zuo/Iconmask/Icon")?.GetComponent<Image>();
        _nameText = _root.transform.Find("Panel/zuo/namebg/Name")?.GetComponent<Text>();
        _role = _root.transform.Find("Panel/Role")?.GetComponent<Image>();

        // 满星节点：预制体只摆了 3 个（满星1/满星2/满星3）
        _stars = new GameObject[MaxStarNodes];
        var rating = _root.transform.Find("Panel/you/Rating");
        if (rating == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Rating（星级将不显示）");
        else
        {
            _stars[0] = rating.Find("满星1")?.gameObject;
            _stars[1] = rating.Find("满星2")?.gameObject;
            _stars[2] = rating.Find("满星3")?.gameObject;

            // 【2026-10-14 主人拍板】星级要 1 颗 / 2 颗 / 3 颗都居中 —— 记下满星布局原始位置，
            // Fill 时按激活数量整组平移（见 CenterActiveStars）。
            _starBasePos = new Vector2[MaxStarNodes];
            for (int i = 0; i < MaxStarNodes; i++)
            {
                var srt = _stars[i] != null ? _stars[i].GetComponent<RectTransform>() : null;
                if (srt != null) _starBasePos[i] = srt.anchoredPosition;
            }
        }

        // 七对属性（Root 整行 + 值 Text；Label 静态文本不碰）
        _attrHpRoot = _root.transform.Find("Panel/you/AttrPanel/AttrHpRoot")?.gameObject;
        _attrHp = _root.transform.Find("Panel/you/AttrPanel/AttrHpRoot/AttrHp")?.GetComponent<Text>();
        _attrAtkRoot = _root.transform.Find("Panel/you/AttrPanel/AttrAtkRoot")?.gameObject;
        _attrAtk = _root.transform.Find("Panel/you/AttrPanel/AttrAtkRoot/AttrAtk")?.GetComponent<Text>();
        _attrMagicAtkRoot = _root.transform.Find("Panel/you/AttrPanel/AttrMagicAtkRoot")?.gameObject;
        _attrMagicAtk = _root.transform.Find("Panel/you/AttrPanel/AttrMagicAtkRoot/AttrMagicAtk")?.GetComponent<Text>();
        _attrDefRoot = _root.transform.Find("Panel/you/AttrPanel/AttrDefRoot")?.gameObject;
        _attrDef = _root.transform.Find("Panel/you/AttrPanel/AttrDefRoot/AttrDef")?.GetComponent<Text>();
        _attrMagicDefRoot = _root.transform.Find("Panel/you/AttrPanel/AttrMagicDefRoot")?.gameObject;
        _attrMagicDef = _root.transform.Find("Panel/you/AttrPanel/AttrMagicDefRoot/AttrMagicDef")?.GetComponent<Text>();
        _attrSpdRoot = _root.transform.Find("Panel/you/AttrPanel/AttrSpdRoot")?.gameObject;
        _attrSpd = _root.transform.Find("Panel/you/AttrPanel/AttrSpdRoot/AttrSpd")?.GetComponent<Text>();
        _attrCritRoot = _root.transform.Find("Panel/you/AttrPanel/AttrCritRoot")?.gameObject;
        _attrCrit = _root.transform.Find("Panel/you/AttrPanel/AttrCritRoot/AttrCrit")?.GetComponent<Text>();

        _skillRoot = _root.transform.Find("Panel/you/Skill")?.gameObject;
        _skillName = _root.transform.Find("Panel/you/Skill/SkillName")?.GetComponent<Text>();
        _skillInfo = _root.transform.Find("Panel/you/Skill/Skillxinxi")?.GetComponent<Text>();
        _skillIcon = _root.transform.Find("Panel/you/Skill/SkillIcon")?.GetComponent<Image>();
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

    // ============================================================
    // 内容填充（只填数据，不碰排版）
    // ============================================================

    /// <summary>
    /// 【2026-10-14 主人拍板】亮 1 / 2 / 3 颗星都让整组居中：
    /// 以「满星 3 颗」的包围盒中心为基准（预制体的设计居中态），
    /// 亮 star 颗时把整组从原始位置平移 (基准中心 − star 颗组中心)。
    /// 每次都从 _starBasePos 出发重算，反复开弹窗不会累积偏移。
    /// </summary>
    void CenterActiveStars(int star)
    {
        if (_stars == null || _starBasePos == null) return;
        for (int i = 0; i < MaxStarNodes; i++)
            if (_stars[i] == null || _stars[i].GetComponent<RectTransform>() == null) return; // 结构缺失就不动，交给上面的 LogError

        Vector2 bMin = _starBasePos[0], bMax = _starBasePos[0];
        for (int i = 1; i < MaxStarNodes; i++)
        {
            bMin = Vector2.Min(bMin, _starBasePos[i]);
            bMax = Vector2.Max(bMax, _starBasePos[i]);
        }
        Vector2 baseCenter = (bMin + bMax) * 0.5f;

        Vector2 cMin = _starBasePos[0], cMax = _starBasePos[0];
        for (int i = 1; i < star; i++)
        {
            cMin = Vector2.Min(cMin, _starBasePos[i]);
            cMax = Vector2.Max(cMax, _starBasePos[i]);
        }
        Vector2 delta = baseCenter - (cMin + cMax) * 0.5f;

        for (int i = 0; i < MaxStarNodes; i++)
        {
            var rt = _stars[i].GetComponent<RectTransform>();
            rt.anchoredPosition = _starBasePos[i] + delta;
        }
    }

    bool Fill(DraftCard card)
    {
        if (_root == null)
        {
            Debug.LogError("[MercObtainPopup] 预制体缺少 Root 节点，本次获得佣兵弹窗跳过（onClosed 已回调，不卡抽奖）");
            return false;
        }

        // 1) 佣兵数据：HireId 优先，AssetId 兜底（旧档卡面两种填法都有过）
        bool ok = MercRosterDefs.TryGetByHireId(card.HireId, out var def);
        if (!ok) ok = MercRosterDefs.TryGetByAssetId(card.Id, out def);
        if (!ok)
        {
            Debug.LogError("[MercObtainPopup] 佣兵数据缺失：HireId=" + card.HireId + " Id=" + card.Id);
            return false;
        }

        // 2) 立绘（LoadPortrait 内部已 LogError，不重复刷屏）
        string hireId = string.IsNullOrEmpty(card.HireId) ? def.HireId : card.HireId;
        var sp = MercJoinBannerUI.LoadPortrait(hireId);
        if (_icon == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/zuo/Iconmask/Icon（立绘不显示）");
        else
        {
            _icon.sprite = sp;                 // sp 为 null 时清掉，不留上一次的图
            _icon.enabled = sp != null;
        }

        // 3) 名字：要名字不要代号；仍为空 → 报出来，不静默显示空白
        string mercName = string.IsNullOrEmpty(def.Name) ? def.Nickname : def.Name;
        if (string.IsNullOrEmpty(mercName))
            Debug.LogError("[MercObtainPopup] 佣兵名字为空：HireId=" + def.HireId);
        SetText(_nameText, "Panel/zuo/namebg/Name", mercName);

        // 3.5) Role 四分类图标：CombatBadge（自带 WarnOnce），取不到隐藏节点（T6）
        if (_role == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/Role（四分类图标不显示）");
        else
        {
            var roleSp = JobIconResolver.CombatBadge(def.JobName);
            _role.sprite = roleSp;
            _role.enabled = roleSp != null;
        }

        // 4) 数值：生命/攻击/防御走 ApplyCombatStats（只随等级）
        MercRosterDefs.ApplyCombatStats(def.AssetId, Mathf.Max(1, card.MercLevel),
            out float hp, out float atk, out float defVal, out float atkSpd, out float move, out float range);
        SetText(_attrHp, "Panel/you/AttrPanel/AttrHpRoot/AttrHp", Mathf.RoundToInt(hp).ToString());
        // 法术型佣兵：攻击/防御两行换成「法术攻击/法术防御」（数值同源，只换标签）
        bool magic = Mercenary.UsesMagicAttack(def.AssetId);
        SetActive(_attrAtkRoot, "Panel/you/AttrPanel/AttrAtkRoot", !magic);
        SetText(_attrAtk, "Panel/you/AttrPanel/AttrAtkRoot/AttrAtk", Mathf.RoundToInt(atk).ToString());
        SetActive(_attrMagicAtkRoot, "Panel/you/AttrPanel/AttrMagicAtkRoot", magic);
        SetText(_attrMagicAtk, "Panel/you/AttrPanel/AttrMagicAtkRoot/AttrMagicAtk", Mathf.RoundToInt(atk).ToString());
        SetActive(_attrDefRoot, "Panel/you/AttrPanel/AttrDefRoot", !magic);
        SetText(_attrDef, "Panel/you/AttrPanel/AttrDefRoot/AttrDef", Mathf.RoundToInt(defVal).ToString());
        SetActive(_attrMagicDefRoot, "Panel/you/AttrPanel/AttrMagicDefRoot", magic);
        SetText(_attrMagicDef, "Panel/you/AttrPanel/AttrMagicDefRoot/AttrMagicDef", Mathf.RoundToInt(defVal).ToString());
        // 速度 = 移动速度（主人 2026-10-14 口径）
        SetActive(_attrSpdRoot, "Panel/you/AttrPanel/AttrSpdRoot", true);
        SetText(_attrSpd, "Panel/you/AttrPanel/AttrSpdRoot/AttrSpd", move.ToString("0.##"));
        // 暴击 = 本项目基础值（未计入星级/装备成长）；0.05 这类小数按百分比显示
        SetActive(_attrCritRoot, "Panel/you/AttrPanel/AttrCritRoot", true);
        float crit = GameConfig.BASE_CRIT_RATE;
        if (crit <= 1.5f) crit *= 100f;
        SetText(_attrCrit, "Panel/you/AttrPanel/AttrCritRoot/AttrCrit", crit.ToString("0.#") + "%");

        // 5) 星星（按佣兵星级；预制体只摆了 3 个满星节点）
        int star = Mathf.Clamp(Mathf.Max(1, card.Star), 1, MaxStarNodes);
        if (card.Star > MaxStarNodes)
            Debug.LogError("[MercObtainPopup] 星级 " + card.Star + " 超出预制体满星节点数 3——需主人补 满星4/满星5（结构变更待批准）");
        for (int i = 0; i < _stars.Length; i++)
            if (_stars[i] != null) _stars[i].SetActive(i < star);
        CenterActiveStars(star);

        // 6) 技能：没有主动技就空着（主人拍板）；有则填名字/简介/图标
        string skillId = def.ActiveSkillId;
        if (string.IsNullOrEmpty(skillId))
        {
            if (_skillRoot == null)
                Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Skill（该佣兵无主动技，整栏不显示）");
            else _skillRoot.SetActive(false);
        }
        else if (MercSkillTable.TryGet(skillId, out var row))
        {
            SetActive(_skillRoot, "Panel/you/Skill", true);
            SetText(_skillName, "Panel/you/Skill/SkillName", row.DisplayName ?? "");
            SetText(_skillInfo, "Panel/you/Skill/Skillxinxi", row.RecruitDesc ?? "");
            var si = MercSkillTable.LoadIcon(skillId);
            if (_skillIcon == null)
                Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Skill/SkillIcon（技能图标不显示）");
            else
            {
                _skillIcon.sprite = si;
                _skillIcon.enabled = si != null;
            }
            if (si == null) Debug.LogError("[MercObtainPopup] 取不到佣兵技能图标 " + skillId);
        }
        else
        {
            Debug.LogError("[MercObtainPopup] 佣兵技能表查不到 " + skillId);
            SetActive(_skillRoot, "Panel/you/Skill", false);
        }

        return true;
    }

    // ============================================================
    // 小工具：字段为 null 说明 Bind 没绑到，补一条 LogError 指明缺哪个节点
    // ============================================================

    static void SetText(Text t, string nodePath, string value)
    {
        if (t == null)
        {
            Debug.LogError("[MercObtainPopup] 缺少文本节点 " + nodePath);
            return;
        }
        t.text = value ?? "";
    }

    static void SetActive(GameObject go, string nodePath, bool on)
    {
        if (go == null)
        {
            Debug.LogError("[MercObtainPopup] 缺少节点 " + nodePath);
            return;
        }
        go.SetActive(on);
    }
}
