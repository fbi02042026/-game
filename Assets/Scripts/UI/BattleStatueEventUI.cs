using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-22 主人拍板】石像事件（圣者/祭司）：复用 BattleUI.prefab 现成节点，prefab 零改动。
///   buff（go 854838359548669700，BattleUI 根下）
///     ├ 恢复（圣者：lizi + 对话框/Text，文案「朝圣」）
///     └ 诅咒（祭司：lizi (1) + 对话框/Text，文案「祭祀」——【2026-10-22 主人拍板 v4.1】定稿）
/// 平时 buff 整体 SetActive(false)；主线正式关回合末抽奖窗弹出时掷 <see cref="StatueChance"/>（10%），
/// 每关最多 1 次、恢复/诅咒互斥二选一；点击对话框立即结算随机事件（不弹选择窗）。
/// 抽奖窗关闭（点继续/超时）未点石像 → 一并收起，不结转（BattleManager.CoWaveIntermissionDraftThenNextWave 挂 ForceCollapse）。
/// 引导局 / 金币本 / 无限裂隙：TryShowOnDraft 直接 false（IsFormalMainRun 过滤）。
/// </summary>
public class BattleStatueEventUI : MonoBehaviour
{
    /// <summary>石像出现概率（0~1，可调）。</summary>
    public const float StatueChance = 0.10f;

    const string HealEventText = "朝圣";    // 圣者文案（拍板）
    const string CurseEventText = "祭祀";   // 祭司文案（【2026-10-22 主人拍板 v4.1】「祈祷」→「祭祀」）

    const float HealAllRatio = 0.30f;       // 恢复：全体回 30% MaxHp
    const float HealSoloRatio = 0.50f;      // 恢复：无人阵亡时随机 1 名存活者回 50%
    const float CurseHpCutRatio = 0.20f;    // 诅咒：随机 1 名存活我方扣 20% 当前血

    static BattleStatueEventUI _instance;

    Transform _healNode;
    Transform _curseNode;
    Text _healDialogText;
    Text _curseDialogText;
    bool _wired;
    bool _shownThisStage;
    string _stageKey;

    /// <summary>
    /// 绑定入口（BattleUI.Binding.AutoBindMissingRefs 调一次 + TryShowOnDraft 兜底调）：
    /// 找 BattleUI 下的 buff 节点，挂本组件并整体隐藏（平时 buff SetActive(false)）。
    /// </summary>
    public static BattleStatueEventUI EnsureOn(Transform battleUiRoot)
    {
        if (_instance != null) return _instance;
        if (battleUiRoot == null) return null;
        var buffT = FindDeep(battleUiRoot, "buff");
        if (buffT == null)
        {
            Debug.LogError("[BattleStatueEvent] BattleUI 里找不到 buff 节点（go 854838359548669700），石像事件不可用");
            return null;
        }
        _instance = buffT.GetComponent<BattleStatueEventUI>();
        if (_instance == null)
            _instance = buffT.gameObject.AddComponent<BattleStatueEventUI>();
        buffT.gameObject.SetActive(false);   // 平时 buff 整体隐藏
        return _instance;
    }

    /// <summary>
    /// 触发口：回合末抽奖窗弹出前调用（BattleManager.CoWaveIntermissionDraftThenNextWave）。
    /// 正式关且本关未出过石像且 Random.value &lt; StatueChance → 随机启用 恢复/诅咒 之一，返回 true。
    /// 进关第一次抽奖（CoStageEntryDraft）不调用本方法 —— 按主人口径「只有在抽奖时出现」按回合末抽奖窗执行。
    /// </summary>
    public static bool TryShowOnDraft()
    {
        var bm = BattleManager.Instance;
        // 【2026-10-22 主人拍板】只加主线正式关：引导局 / 金币本 / 无限裂隙一律不出现
        if (bm == null || !bm.IsFormalMainRun) return false;
        var ui = BattleUI.Instance != null ? EnsureOn(BattleUI.Instance.transform) : null;
        if (ui == null) return false;
        return ui.TryShowInternal();
    }

    /// <summary>抽奖窗关闭（点继续/超时）未点石像 → 一并收起，不结转（幂等）。</summary>
    public static void ForceCollapse()
    {
        if (_instance != null && _instance.gameObject.activeSelf)
        {
            _instance.gameObject.SetActive(false);
            Debug.Log("[BattleStatueEvent] 抽奖窗关闭，未点击的石像收起（不结转）");
        }
    }

    bool TryShowInternal()
    {
        // 每关最多 1 次：换关（章/关序号变化）自动重置，不依赖外部复位调用
        var cm = ChapterManager.Instance;
        string key = cm != null ? $"{cm.currentChapter}_{cm.currentStageIndex}" : "";
        if (_stageKey != key)
        {
            _stageKey = key;
            _shownThisStage = false;
        }
        if (_shownThisStage) return false;
        if (Random.value >= StatueChance) return false;

        Bind();
        if (_healNode == null || _curseNode == null) return false;

        // 恢复/诅咒互斥：随机二选一（另一个隐藏），文案每次写入
        bool heal = Random.value < 0.5f;
        _healNode.gameObject.SetActive(heal);
        _curseNode.gameObject.SetActive(!heal);
        if (_healDialogText != null) _healDialogText.text = HealEventText;
        if (_curseDialogText != null) _curseDialogText.text = CurseEventText;

        _shownThisStage = true;
        gameObject.SetActive(true);
        Debug.Log($"[BattleStatueEvent] 石像事件出现：{(heal ? "恢复（圣者·朝圣）" : "诅咒（祭司·祭祀）")} " +
                  $"（概率 {StatueChance:P0}，本关第 1 次；第 {cm?.currentChapter}-{(cm?.currentStageIndex ?? 0) + 1} 关）");
        return true;
    }

    /// <summary>绑 buff 下的 恢复/诅咒 子节点 + 对话框文案 + 运行时补 Button（prefab 零改动）。</summary>
    void Bind()
    {
        if (_wired) return;
        var healT = FindDeep(transform, "恢复");
        var curseT = FindDeep(transform, "诅咒");
        if (healT == null || curseT == null)
        {
            Debug.LogError("[BattleStatueEvent] buff 下找不到 恢复/诅咒 节点，石像事件不可用");
            return;
        }
        _healNode = healT;
        _curseNode = curseT;
        _healDialogText = BindDialogClick(healT, ResolveHealEvent);
        _curseDialogText = BindDialogClick(curseT, ResolveCurseEvent);
        _wired = true;
    }

    /// <summary>对话框节点运行时 AddComponent&lt;Button&gt;（若没有），点击立即结算事件（无选择弹窗）；返回对话框 Text。</summary>
    Text BindDialogClick(Transform node, System.Action onClick)
    {
        Text text = null;
        var dlg = FindDeep(node, "对话框");
        if (dlg != null)
        {
            var textT = dlg.Find("Text");
            text = textT != null ? textT.GetComponent<Text>() : dlg.GetComponentInChildren<Text>(true);
        }
        if (text == null) text = node.GetComponentInChildren<Text>(true);

        var target = dlg != null ? dlg.gameObject : node.gameObject;
        var btn = target.GetComponent<Button>();
        if (btn == null) btn = target.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick());
        // 没有 Graphic 承接点击（按钮不响应）时补一张透明底图
        if (target.GetComponent<Graphic>() == null && target.GetComponentInChildren<Graphic>() == null)
        {
            var img = target.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }
        return text;
    }

    void CollapseSelf()
    {
        gameObject.SetActive(false);
        Debug.Log("[BattleStatueEvent] 石像事件已结算，收起");
    }

    // ============================================================
    // 恢复（圣者·朝圣）
    // ============================================================

    void ResolveHealEvent()
    {
        CollapseSelf();
        // 50% 复活 1 名阵亡佣兵（复用重生护符同款实现）/ 50% 全体回 30% MaxHp；
        // 无人阵亡 → 改为随机 1 名存活者回 50% MaxHp。
        int fallenIdx = FindFirstFallenLoadoutIndex();
        if (fallenIdx >= 0 && Random.value < 0.5f)
        {
            ReviveMercForStatue(fallenIdx);
            return;
        }
        if (fallenIdx >= 0)
        {
            HealAllParty(HealAllRatio);
            return;
        }
        var alive = CollectAliveAllies();
        if (alive.Count == 0) return;
        HealUnit(alive[Random.Range(0, alive.Count)], HealSoloRatio);
    }

    /// <summary>全体（主角 + 存活佣兵）回 pct% MaxHp —— 复用波清回血同款原语 SkillCast.ApplyHealToUnit。</summary>
    static void HealAllParty(float pct)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;
        HealUnit(bm.hero, pct);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
            HealUnit(mercs[i], pct);
    }

    static void HealUnit(UnitBase u, float pct)
    {
        if (u == null || u.isDead || u.attr == null) return;
        var bm = BattleManager.Instance;
        if (bm == null) return;
        // SkillCast 是 BattleManager 的 internal 属性（波清回血同款原语），外部类须经实例访问
        bm.SkillCast.ApplyHealToUnit(u, u.attr.GetAttr(AttrType.MaxHp) * pct);
    }

    /// <summary>本局第一个阵亡佣兵的构筑下标（TryReviveMercWithCharm 的 loadoutIndex 口径）；无 → -1。</summary>
    static int FindFirstFallenLoadoutIndex()
    {
        if (!RunLoadout.IsActive) return -1;
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
            if (mercs[i] != null && mercs[i].fallen) return i;
        return -1;
    }

    /// <summary>
    /// 石像复活：复用重生护符的复活实现 <see cref="BattleManager.TryReviveMercWithCharm"/>（满血重新进场、
    /// spawn 参数与护符同款）——先临时借 1 枚护符额度走同款链，成功后立即还回（石像复活不消耗玩家道具）。
    /// 失败（预制体缺失/场上已有实例）只报错不卡流程。护符实现找不到时才需要就地满血复活兜底 —— 现已找到，无需另写。
    /// </summary>
    static void ReviveMercForStatue(int loadoutIndex)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;
        var data = SaveSystem.Instance?.Data;
        int charmsBefore = data != null ? data.reviveCharms : 0;
        if (data != null) data.reviveCharms = charmsBefore + 1;   // 借额度（护符为 0 也能走复活链）
        bool ok = bm.TryReviveMercWithCharm(loadoutIndex);
        if (data != null)
        {
            data.reviveCharms = charmsBefore;   // 借完即还
            SaveSystem.Instance?.Save();
        }
        if (ok)
        {
            // TryReviveMercWithCharm 内部会弹「重生护符发动」toast，这里补一条石像口径的纠正提示
            UIManager.Instance?.ShowToast("圣者之力：阵亡的伙伴复活了！", true);
        }
        else
            Debug.LogError("[BattleStatueEvent] 石像复活失败（护符复活链未生效，详见其内部报错），本次恢复未生效");
    }

    // ============================================================
    // 诅咒（祭司·祭祀，文案 v4.1 定稿）
    // ============================================================

    void ResolveCurseEvent()
    {
        CollapseSelf();
        var bm = BattleManager.Instance;
        if (bm == null) return;

        if (Random.value < 0.5f)
        {
            // 50%：加 1 个持续整场战斗的 buff（走 BattleManager.tempBuffs / AttrBonusData）。
            // 池：攻击 +20% / 攻速 +20% / 最大生命 +20%（数值待主人调）。
            // 【2026-10-22 主人拍板 v4.1】石像 buff 佣兵也要吃到：主角照旧 RecalcAttr；
            // 全体存活佣兵立即走 Mercenary.RecalcAttr 重算（同一份 tempBuffs 列表、同款口径，不会算重）。
            // 中途抽奖入队 / 阵亡复活的佣兵：装配时机 SpawnMercenary → Init → SetupAttributes
            // 内已统一叠加 tempBuffs（Mercenary.ApplyRunTempBuffs），生成时自然带上。
            AttrType attr = AttrType.AttackSpeed;
            int roll = Random.Range(0, 3);
            if (roll == 0) attr = PlayerJobBaseStats.CurrentAttackAttr();
            else if (roll == 1) attr = AttrType.AttackSpeed;
            else attr = AttrType.MaxHp;
            bm.tempBuffs.Add(new AttrBonusData { attrType = attr, value = 0.20f, isPercent = true });
            if (Hero.Instance != null) Hero.Instance.RecalcAttr();
            var blessedMercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (blessedMercs != null)
            {
                for (int i = 0; i < blessedMercs.Count; i++)
                {
                    var m = blessedMercs[i];
                    // 死亡佣兵不重算（复活链 SpawnMercenary → Init 会重新带上全部 tempBuffs，不吃亏）
                    if (m == null || m.isDead || m.attr == null) continue;
                    m.RecalcAttr();
                }
            }
            BuffHudUI.MarkTempDirty();
            Debug.Log($"[BattleStatueEvent] 祭司赐福：主角与全体佣兵获得 {attr} +20%（整场战斗，tempBuffs）");
            UIManager.Instance?.ShowToast("祭司的低语：力量在体内涌动……", true);
        }
        else
        {
            // 50%：随机 1 名存活我方扣 20% 当前血。
            // 实现口径（报明）：不走受击链（不算击杀来源），直接 currentHp 扣减（保底 1 血不致死）+ 伤害飘字。
            var alive = CollectAliveAllies();
            if (alive.Count == 0) return;
            var victim = alive[Random.Range(0, alive.Count)];
            float cut = victim.currentHp * CurseHpCutRatio;
            victim.currentHp = Mathf.Max(1f, victim.currentHp - cut);
            DamageTextSystem.Instance?.SpawnDamageText(victim.GetHitPosition(), Mathf.RoundToInt(cut), false, true);
            Debug.Log($"[BattleStatueEvent] 祭司的诅咒：{victim.name} 扣 {cut:F0} 点当前血（{CurseHpCutRatio:P0}，不致死）");
            UIManager.Instance?.ShowToast("祭司的诅咒：生命被抽走了一缕……", true);
        }
    }

    /// <summary>存活我方（主角 + 存活佣兵）。</summary>
    static List<UnitBase> CollectAliveAllies()
    {
        var list = new List<UnitBase>();
        var bm = BattleManager.Instance;
        if (bm != null && bm.hero != null && !bm.hero.isDead)
            list.Add(bm.hero);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs != null)
            for (int i = 0; i < mercs.Count; i++)
                if (mercs[i] != null && !mercs[i].isDead)
                    list.Add(mercs[i]);
        return list;
    }

    /// <summary>深搜找节点（含未激活）。</summary>
    static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}
