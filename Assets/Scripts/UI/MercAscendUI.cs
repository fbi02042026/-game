using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵进阶（局外升星）弹窗（2026-10-22 施工单《佣兵进阶界面》）。
///
/// 预制体 Assets/Resources/Prefabs/Town/yongbing升阶.prefab 主人已摆好 —— 本类只按名绑定，
/// 几何一个像素不动；缺节点 LogError 不兜底建（AGENTS.md §4）。节点口径（主人 dump 实测）：
///   Root/Panel(800×1050)
///     top/namebg/Name 佣兵名、top/Role 职业徽记位、top/Iconmask/Icon 大立绘
///     zuo / you 结构相同：AttrPanel 七行（AttrXxxRoot/AttrXxx）+ Rating 三星（满星1/2/3）+ Skill 行（只读，不归本类管）
///     碎片/{碎片数量、金币图标、金币数量、yongbingsuipian(主人碎片预制体嵌套实例)}
///     shengjieButton(升阶) / tihaunButton(取消)；Root/Dim 点击关闭
///
/// 数据口径（2026-10-22 主人拍板）：
///   左侧 = 当前值（含当前 outStar 加成）；右侧 = 升到下一星预览 —— 攻/防 ×(1+8%)、
///   魔攻/魔防 ×(1+7%)（相对左侧显示值再乘一档）；生命/速度/暴击两侧同值（不吃加成）。
///   满星后右侧 = 当前值、升阶按钮置灰。
/// 打开动画：Panel 缩放 0.2 → 1.0（主人原话「缩放效果速度慢点 0.2的倍数吧」），
/// 只动 Panel 的 localScale，其它 transform 一律不碰。
/// </summary>
public class MercAscendUI : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Town/yongbing升阶";

    /// <summary>935：2026-10-22 获得佣兵弹窗（925）可叠开本进阶弹窗，需压在其上；仍压过雇佣队列界面（TownPopup=900）。</summary>
    const int SortOrder = 935;

    /// <summary>打开缩放动画：起点 0.2（主人拍板）、时长 0.28 秒（主人要求 0.25~0.3 慢速）、ease-out。</summary>
    const float OpenScaleFrom = 0.2f;
    const float OpenDuration = 0.28f;

    /// <summary>钻石图标（传奇佣兵升阶扣钻石时替换金币图标；Resources 路径已核实存在）。</summary>
    const string DiamondIconPath = "Art/UI/Icons/Common/icon_diamond_blue";

    static MercAscendUI _instance;

    GameObject _root;          // Root（Panel/Dim 的父级，开窗开关作用于它）
    RectTransform _panel;      // Panel（缩放动画只动它的 localScale）

    Text _nameText;
    Image _roleImage;
    Image _iconImage;

    Text[] _leftVals;          // 左（当前）七行数值，下标 = RowValueNames 顺序
    Text[] _rightVals;         // 右（下一星预览）七行数值
    GameObject[] _leftRowRoots;   // 左七行 Root 节点（物攻/魔攻行二选一隐藏用，2026-10-22 主人拍板）
    GameObject[] _rightRowRoots;  // 右七行 Root 节点（同上）
    GameObject[] _leftStars;   // 满星1→满星2→满星3 顺序
    GameObject[] _rightStars;

    Text _fragCountText;
    Text _goldCountText;
    Image _goldIconImage;
    Sprite _goldIconBase;      // 预制体原金币图标（传奇切钻石后，切回金币时还原）
    MercFragmentCardUI _fragCard;

    Button _starUpBtn;
    Button _cancelBtn;
    Button _dimBtn;

    string _hireId;            // 当前展示佣兵（hireId）
    Coroutine _scaleCo;

    /// <summary>七行行名（左/右两侧同名）：顺序按主人 dump：Hp/Spd/MagicDef/Crit/Def/Atk/MagicAtk。</summary>
    static readonly string[] RowRootNames =
        { "AttrHpRoot", "AttrSpdRoot", "AttrMagicDefRoot", "AttrCritRoot", "AttrDefRoot", "AttrAtkRoot", "AttrMagicAtkRoot" };
    static readonly string[] RowValueNames =
        { "AttrHp", "AttrSpd", "AttrMagicDef", "AttrCrit", "AttrDef", "AttrAtk", "AttrMagicAtk" };

    // ============================================================
    // 对外入口（唯一）
    // ============================================================

    /// <summary>打开进阶弹窗。hireIdOrUid：佣兵 hireId 或 AssetId（MercStarSystem 同口径）。</summary>
    public static void Show(string hireIdOrUid)
    {
        if (string.IsNullOrEmpty(hireIdOrUid))
        {
            Debug.LogError("[MercAscend] Show 传入佣兵 id 为空，不开窗");
            return;
        }
        var ui = Ensure();
        if (ui != null) ui.Open(hireIdOrUid);
    }

    static MercAscendUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[MercAscend] 预制体缺失（Resources/" + PrefabPath + "），无法开窗");
            return null;
        }

        var go = Object.Instantiate(prefab);
        go.name = "MercAscendUI";
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<MercAscendUI>();
        _instance.Build();   // 末尾 SetActive(false)（AGENTS.md §4 运行时建树口径）
        return _instance;
    }

    void Awake()
    {
        if (_instance == null) _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ============================================================
    // 建树绑定（缺节点 LogError，不兜底建）
    // ============================================================

    void Build()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null) Debug.LogError("[MercAscend] 缺节点 Root");
        _panel = (_root != null ? _root.transform.Find("Panel") : null) as RectTransform;
        if (_panel == null) Debug.LogError("[MercAscend] 缺节点 Root/Panel");

        Transform p = _panel;

        // ---- top：佣兵名 / 职业徽记 / 大立绘 ----
        _nameText = FindDeep(p, "Name")?.GetComponent<Text>();
        if (_nameText == null) Debug.LogError("[MercAscend] 缺节点 top/namebg/Name（佣兵名 Text）");
        _roleImage = FindDeep(p, "Role")?.GetComponent<Image>();
        if (_roleImage == null) Debug.LogError("[MercAscend] 缺节点 top/Role（职业徽记 Image）");
        _iconImage = FindDeep(p, "Icon")?.GetComponent<Image>();
        if (_iconImage == null) Debug.LogError("[MercAscend] 缺节点 top/Iconmask/Icon（大立绘 Image）");

        // ---- 左（当前）/ 右（下一星预览）----
        var zuo = p.Find("zuo");
        var you = p.Find("you");
        if (zuo == null) Debug.LogError("[MercAscend] 缺节点 zuo（当前属性栏）");
        if (you == null) Debug.LogError("[MercAscend] 缺节点 you（升星预览栏）");
        BindSide(zuo, out _leftVals, out _leftRowRoots, out _leftStars, "zuo");
        BindSide(you, out _rightVals, out _rightRowRoots, out _rightStars, "you");

        // ---- 碎片区 ----
        _fragCountText = FindDeep(p, "碎片数量")?.GetComponent<Text>();
        if (_fragCountText == null) Debug.LogError("[MercAscend] 缺节点 碎片/碎片数量（Text）");
        _goldCountText = FindDeep(p, "金币数量")?.GetComponent<Text>();
        if (_goldCountText == null) Debug.LogError("[MercAscend] 缺节点 碎片/金币数量（Text）");
        _goldIconImage = FindDeep(p, "金币图标")?.GetComponent<Image>();
        if (_goldIconImage == null) Debug.LogError("[MercAscend] 缺节点 碎片/金币图标（Image）");
        else _goldIconBase = _goldIconImage.sprite;
        var fragGo = FindDeep(p, "yongbingsuipian")?.gameObject;
        if (fragGo == null) Debug.LogError("[MercAscend] 缺节点 碎片/yongbingsuipian（主人碎片预制体实例）");
        else _fragCard = MercFragmentCardUI.Bind(fragGo);

        // ---- 按钮 ----
        _starUpBtn = BindButton(p, "shengjieButton", OnStarUpClick, "升阶");
        _cancelBtn = BindButton(p, "tihaunButton", Close, "取消");
        _dimBtn = _root != null ? BindButton(_root.transform, "Dim", Close, "Dim") : null;

        if (_root != null) _root.SetActive(false);   // AGENTS.md §4：建树末尾必须收起
    }

    /// <summary>绑一侧（zuo/you）：AttrPanel 七行数值 + 七行 Root 节点 + Rating 三星。sideName 仅用于报错定位。</summary>
    void BindSide(Transform side, out Text[] vals, out GameObject[] rowRoots, out GameObject[] stars, string sideName)
    {
        vals = new Text[RowRootNames.Length];
        rowRoots = new GameObject[RowRootNames.Length];
        stars = new GameObject[3];
        if (side == null) return;

        var attrPanel = side.Find("AttrPanel");
        if (attrPanel == null) Debug.LogError("[MercAscend] 缺节点 " + sideName + "/AttrPanel");
        for (int i = 0; i < RowRootNames.Length; i++)
        {
            var row = attrPanel != null ? attrPanel.Find(RowRootNames[i]) : null;
            if (row == null)
            {
                Debug.LogError("[MercAscend] 缺节点 " + sideName + "/AttrPanel/" + RowRootNames[i]);
                continue;
            }
            rowRoots[i] = row.gameObject;   // 物攻/魔攻行二选一隐藏要动整个 Root（2026-10-22 主人拍板）
            vals[i] = FindDeep(row, RowValueNames[i])?.GetComponent<Text>();
            if (vals[i] == null)
                Debug.LogError("[MercAscend] 缺数值 Text：" + sideName + "/AttrPanel/" + RowRootNames[i] + "/" + RowValueNames[i]);
        }

        var rating = FindDeep(side, "Rating");
        if (rating == null)
        {
            Debug.LogError("[MercAscend] 缺节点 " + sideName + "/Rating");
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            stars[i] = rating.Find("满星" + (i + 1))?.gameObject;
            if (stars[i] == null) Debug.LogError("[MercAscend] 缺节点 " + sideName + "/Rating/满星" + (i + 1));
        }
    }

    /// <summary>绑按钮：无 Button 运行时补（预制体只有 Image 也接得上，MercDrawPoolUI 同口径）。</summary>
    Button BindButton(Transform from, string name, UnityEngine.Events.UnityAction onClick, string tag)
    {
        var t = FindDeep(from, name);
        if (t == null)
        {
            Debug.LogError("[MercAscend] 缺节点 " + name + "（" + tag + "）");
            return null;
        }
        var img = t.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    // ============================================================
    // 开窗 / 关窗
    // ============================================================

    void Open(string hireIdOrUid)
    {
        // DDOL 弹窗每次 Show 重套画布（MercDrawPoolUI.Open 同口径，防切场景后弹窗变大）
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, SortOrder);

        // fail closed：数据不合法不开窗（窗口保持上一次状态）
        if (!Fill(hireIdOrUid)) return;

        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        PlayOpenScale();
    }

    void Close()
    {
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
        if (_panel != null) _panel.localScale = Vector3.one;   // 动画只动 Panel 缩放，关窗还原
        if (_root != null) _root.SetActive(false);
    }

    // ============================================================
    // 数据填充
    // ============================================================

    bool Fill(string hireIdOrUid)
    {
        if (!MercRosterDefs.TryGetByHireId(hireIdOrUid, out var def)
            && !MercRosterDefs.TryGetByAssetId(hireIdOrUid, out def))
        {
            Debug.LogError("[MercAscend] 花名册查不到佣兵：" + hireIdOrUid + "，不开窗");
            return false;
        }
        _hireId = def.HireId;

        var data = SaveSystem.Instance?.Data;
        if (data == null || !data.IsMercUnlocked(_hireId))
        {
            UIManager.Instance?.ShowToast("尚未获得该佣兵", true);
            return false;
        }

        int cur = MercStarSystem.OutStarOf(_hireId);
        if (cur < 0) cur = 0;
        bool maxed = cur >= MercStarDefs.MaxOutStar;

        // ---- 头部 ----
        if (_nameText != null) _nameText.text = def.Name;
        if (_roleImage != null)
            _roleImage.sprite = MercGrowSprites.LoadBadgeForMerc(_hireId, def.Rarity);
        if (_iconImage != null)
        {
            _iconImage.sprite = MercPortraitSprites.GetStand(_hireId);
            _iconImage.preserveAspect = true;   // 大立绘保比例（施工单口径）
        }

        // ---- 属性数值：基底走 ApplyCombatStats（与「获得佣兵」弹窗同源），等级取存档 ----
        MercRosterDefs.ApplyCombatStats(def.AssetId, ResolveSavedLevel(_hireId, def),
            out float hp, out float atk, out float defV, out float atkSpd, out float move, out float range);
        bool magic = Mercenary.UsesMagicAttack(def.AssetId);

        // ---- 攻/魔攻行二选一（2026-10-22 主人拍板「物攻佣兵只显示物攻 魔攻只显示魔攻」）----
        // 判定与战斗/获得弹窗同源（Mercenary.UsesMagicAttack，不另写前缀表）；
        // 物攻佣兵隐藏魔攻行、魔攻佣兵隐藏物攻行；魔防/防御等其余行保持显示不变。
        // 弹窗是 DDOL 复用的，切换佣兵时保留行要显式 SetActive(true) 防上一次残留。
        SetAtkRowVisible(_leftRowRoots, magic);
        SetAtkRowVisible(_rightRowRoots, magic);

        float atkCur = atk * (1f + MercStarDefs.AtkPctPerStar * cur);
        float defCur = defV * (1f + MercStarDefs.AtkPctPerStar * cur);
        float matkCur = (magic ? atk : 0f) * (1f + MercStarDefs.MagicPctPerStar * cur);
        float mdefCur = defV * (1f + MercStarDefs.MagicPctPerStar * cur);
        // 右侧 = 相对左侧显示值再乘一档；满星后右侧 = 当前值（2026-10-22 主人拍板）
        float atkNext = maxed ? atkCur : atkCur * (1f + MercStarDefs.AtkPctPerStar);
        float defNext = maxed ? defCur : defCur * (1f + MercStarDefs.AtkPctPerStar);
        float matkNext = maxed ? matkCur : matkCur * (1f + MercStarDefs.MagicPctPerStar);
        float mdefNext = maxed ? mdefCur : mdefCur * (1f + MercStarDefs.MagicPctPerStar);

        // 暴击 = 本项目基础值；0.05 这类小数按百分比显示（MercObtainPopupUI 同口径）
        float crit = GameConfig.BASE_CRIT_RATE;
        if (crit <= 1.5f) crit *= 100f;
        string critStr = crit.ToString("0.#") + "%";

        // 行序 = RowValueNames：Hp / Spd / MagicDef / Crit / Def / Atk / MagicAtk
        // 生命/速度/暴击不吃加成，两侧同值（2026-10-22 主人确认「不加属性的可以一样 只是预览下一星属性」）
        string[] leftStrs =
        {
            Mathf.RoundToInt(hp).ToString(),
            move.ToString("0.##"),
            Mathf.RoundToInt(mdefCur).ToString(),
            critStr,
            Mathf.RoundToInt(defCur).ToString(),
            Mathf.RoundToInt(atkCur).ToString(),
            Mathf.RoundToInt(matkCur).ToString(),
        };
        string[] rightStrs =
        {
            leftStrs[0],
            leftStrs[1],
            Mathf.RoundToInt(mdefNext).ToString(),
            critStr,
            Mathf.RoundToInt(defNext).ToString(),
            Mathf.RoundToInt(atkNext).ToString(),
            Mathf.RoundToInt(matkNext).ToString(),
        };
        for (int i = 0; i < leftStrs.Length; i++)
        {
            if (_leftVals != null && i < _leftVals.Length && _leftVals[i] != null) _leftVals[i].text = leftStrs[i];
            if (_rightVals != null && i < _rightVals.Length && _rightVals[i] != null) _rightVals[i].text = rightStrs[i];
        }

        // ---- 星星：亮的颗数 = 星数，点亮顺序 满星1→满星2→满星3，多余隐藏，0 星全隐藏 ----
        SetStars(_leftStars, cur);
        SetStars(_rightStars, maxed ? cur : cur + 1);

        // ---- 碎片区：两行「{佣兵名}碎片\n{当次需要}/{拥有}」（需要在前拥有在后，主人原话 44/55）----
        int have = MercStarSystem.GetBagFragCount(_hireId);
        int need = MercStarSystem.GetNextStarCost(_hireId);   // 满星返回 0
        if (_fragCountText != null)
            _fragCountText.text = maxed
                ? def.Name + "碎片\n" + have
                : def.Name + "碎片\n" + need + "/" + have;
        if (_fragCard != null) _fragCard.Apply(def.Rarity, _hireId);

        // ---- 金币/钻石：显示本次升阶消耗额（主人原话「按钮上方是进阶需要的金钱数」）----
        // 普通/稀有=金币（图标不动）；传奇=钻石（图标换钻石图）。满星显示 0。
        bool legend = def.Rarity == MercRosterDefs.MercRarity.Legendary;
        if (_goldCountText != null)
        {
            long cost = maxed
                ? 0
                : (legend
                    ? MercStarDefs.DiamondCost(def.Rarity, cur + 1)
                    : MercStarDefs.GoldCost(def.Rarity, cur + 1));
            _goldCountText.text = cost.ToString();
        }
        if (_goldIconImage != null)
        {
            if (legend)
            {
                var dia = Resources.Load<Sprite>(DiamondIconPath);
                if (dia != null) _goldIconImage.sprite = dia;
                else Debug.LogError("[MercAscend] 钻石图标缺失：Resources/" + DiamondIconPath + "（不兜底）");
            }
            else _goldIconImage.sprite = _goldIconBase;   // 切回金币（防上次展示传奇留下的钻石图）
        }

        // ---- 升阶按钮：满星置灰 ----
        if (_starUpBtn != null) _starUpBtn.interactable = !maxed;
        return true;
    }

    void SetStars(GameObject[] stars, int lit)
    {
        if (stars == null) return;
        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].SetActive(i < lit);
    }

    /// <summary>
    /// 攻/魔攻行二选一显示（2026-10-22 主人拍板）：RowRootNames 顺序下 5=AttrAtkRoot、6=AttrMagicAtkRoot。
    /// 物攻佣兵（magic=false）隐藏魔攻行；魔攻佣兵隐藏物攻行；保留的一侧显式 SetActive(true) 防复用残留。
    /// 行 Root 缺节点（绑定时报过错）不兜底、直接跳过 —— fail closed。
    /// </summary>
    void SetAtkRowVisible(GameObject[] rowRoots, bool magic)
    {
        if (rowRoots == null) return;
        const int idxAtk = 5, idxMagicAtk = 6;
        if (rowRoots.Length <= idxMagicAtk) return;
        if (rowRoots[idxAtk] != null) rowRoots[idxAtk].SetActive(magic);
        if (rowRoots[idxMagicAtk] != null) rowRoots[idxMagicAtk].SetActive(!magic);
    }

    /// <summary>存档里该佣兵等级（无实例按 1 级显示；口径同 MercDrawPoolUI.ShowMercDetail）。</summary>
    static int ResolveSavedLevel(string hireId, MercRosterDefs.Def def)
    {
        var data = SaveSystem.Instance?.Data;
        if (data?.permanentMercs != null)
            for (int i = 0; i < data.permanentMercs.Count; i++)
            {
                var m = data.permanentMercs[i];
                if (m == null) continue;
                if (m.mercId == hireId || m.mercId == def.AssetId) return Mathf.Max(1, m.level);
            }
        return 1;
    }

    // ============================================================
    // 升阶流程（fail closed：每一步校验不过就 toast 具体原因）
    // ============================================================

    void OnStarUpClick()
    {
        if (string.IsNullOrEmpty(_hireId)) return;
        if (!MercRosterDefs.TryGetByHireId(_hireId, out var def)) return;

        int cur = MercStarSystem.OutStarOf(_hireId);
        if (cur < 0) cur = 0;
        if (cur >= MercStarDefs.MaxOutStar) { Toast(def.Name + " 已满星"); return; }

        // 碎片校验（MercStarSystem 权威；msg 说明缺什么）
        if (!MercStarSystem.CanStarUp(_hireId, out string msg)) { Toast(msg); return; }

        // 金币/钻石门槛（2026-10-22 主人拍板：升阶消耗玩家资金，传奇扣钻石；建议值待主人调）
        int targetStar = cur + 1;
        long goldCost = MercStarDefs.GoldCost(def.Rarity, targetStar);
        long diaCost = MercStarDefs.DiamondCost(def.Rarity, targetStar);
        var data = SaveSystem.Instance?.Data;
        if (goldCost > 0)
        {
            long have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            if (have < goldCost)
            {
                Toast("金币不足：还需 " + (goldCost - have) + "（持有 " + have + "/" + goldCost + "）");
                return;
            }
        }
        if (diaCost > 0)
        {
            long have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
            if (have < diaCost)
            {
                Toast("钻石不足：还需 " + (diaCost - have) + "（持有 " + have + "/" + diaCost + "）");
                return;
            }
        }

        // 执行升星（扣背包碎片 + outStar+1 + 存档，MercStarSystem 权威）
        if (!MercStarSystem.TryStarUp(_hireId, out string upMsg)) { Toast(upMsg); return; }

        // 扣资金：预检已过还扣不动 = 数据异常，报错不吞（fail closed）
        if (goldCost > 0 && !ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, goldCost, true, false))
            Debug.LogError("[MercAscend] " + def.Name + " 升星后扣金币失败 cost=" + goldCost + " —— 数据异常");
        if (diaCost > 0 && !ResourceWallet.TrySpend(ResourceWallet.ResourceType.Diamond, diaCost, true, false))
            Debug.LogError("[MercAscend] " + def.Name + " 升星后扣钻石失败 cost=" + diaCost + " —— 数据异常");

        Toast(upMsg);       // 「XX 升星成功（N 星）」
        Fill(_hireId);      // 左=新当前，右=再下一星预览；满星自动置灰
    }

    void Toast(string s) => UIManager.Instance?.ShowToast(s, true);

    // ============================================================
    // 打开缩放动画（Panel 0.2 → 1.0，ease-out；只动 Panel 的 localScale）
    // ============================================================

    void PlayOpenScale()
    {
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
        if (_panel == null) return;
        _scaleCo = StartCoroutine(CoOpenScale());
    }

    /// <summary>unscaled 时间（防 timescale=0 卡死，项目弹窗动画既有口径）；ease-out 二次曲线。</summary>
    System.Collections.IEnumerator CoOpenScale()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / OpenDuration;
            float k = Mathf.Clamp01(t);
            k = 1f - (1f - k) * (1f - k);   // ease-out quad
            _panel.localScale = Vector3.one * (OpenScaleFrom + (1f - OpenScaleFrom) * k);
            yield return null;
        }
        _panel.localScale = Vector3.one;
        _scaleCo = null;
    }

    // ============================================================
    // 工具
    // ============================================================

    /// <summary>按名字深度优先找子节点 —— 主人以后往中间插一层也不会断（MercFragmentCardUI 同口径）。</summary>
    static Transform FindDeep(Transform from, string childName)
    {
        if (from == null || string.IsNullOrEmpty(childName)) return null;
        for (int i = 0; i < from.childCount; i++)
        {
            var c = from.GetChild(i);
            if (c.name == childName) return c;
            var hit = FindDeep(c, childName);
            if (hit != null) return hit;
        }
        return null;
    }
}
