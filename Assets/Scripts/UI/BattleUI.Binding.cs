using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 节点绑定：Awake 里按名字自动补引用、绑定自检、系统就绪后的重绑。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>
    /// 整理阶段那个确认按钮的文案（<b>唯一出口</b>）：创建时与每次显隐都取这一个值，
    /// 别在两处各写一份 —— 2026-10-11 之前就是两处都写「确定」，改文案漏一处就又对不上。
    /// </summary>
    public const string LootConfirmLabel = "确定 · 继续";

    /// <summary>
    /// 美术尚未出拾取确定按钮（旧预制体叫「整理」，新预制体 BackpackPanel 下没有这个节点），
    /// 导致 lootConfirmButton 恒为 null、拾取模式无法确认。这里运行时补一个默认隐藏的按钮，
    /// 显隐仍由 UpdateBackpackGrid 按 BattleLootMode.Active 控制。
    /// </summary>
    void EnsureLootConfirmButton()
    {
        if (lootConfirmButton != null) return;
        Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel")
            ?? FindDeepChildIgnoreCase(transform, "Backpack");
        if (backpack == null)
        {
            // 2026-10-11：这里原来静默 return —— 按钮建不出来，整理阶段就永远点不到「确定」，
            // `StageClearRewardDirector` 的 while(!lootDone) 一直空转 = 玩家看到的「卡死」。
            // 铁律：出错必须让主人看见，所以这里打 Error 而不是悄悄过去。
            Debug.LogError("[BattleUI] 整理阶段「确定」按钮建不出来：BattleUI 下既没有 BackpackPanel " +
                           "也没有 Backpack 节点 → 玩家无法确认，传送门不会开");
            return;
        }

        var go = new GameObject("确定", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(backpack, false);
        var rt = go.transform as RectTransform;
        // 锚点做成「面板左下角」：以后屏再变，位置只需重算 anchoredPosition 就够了。
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(260f, 88f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.12f, 0.09f, 0.95f);

        var tGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        tGo.transform.SetParent(go.transform, false);
        var tr = tGo.transform as RectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tx = tGo.GetComponent<Text>();
        tx.font = GameFonts.GetChinese();
        // 2026-10-11 主人拍板：只写「确定」玩家不知道点了会怎样（主人反馈「到这一步卡死了」）。
        // 文案写成「确定 · 继续」，说明点了就能往下走。
        tx.text = LootConfirmLabel;
        tx.fontSize = 32;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.color = Color.white;

        // 位置统一由 ReanchorLootConfirmButton 按当前面板 rect 算（竖屏适配会重锚/改高面板，
        // 写死 "中心往上 100" 屏一变就跑到面板外面看不见了）。
        ReanchorLootConfirmButton();

        // BackpackPanel 带 overrideSorting 的 Canvas，zhezhao 遮罩会盖住同级节点 → 抬一层
        var parentCanvas = backpack.GetComponent<Canvas>();
        if (parentCanvas != null)
        {
            var cv = go.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = parentCanvas.sortingOrder + 1;
            go.AddComponent<GraphicRaycaster>();
        }

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        lootConfirmButton = btn;
        lootConfirmButton.onClick.AddListener(OnLootConfirm);
        go.SetActive(false);
    }

    /// <summary>
    /// 把「确定」钉在<b>屏幕</b>（BattleUI 根）底边居中上方 ConfirmLiftY 处 —— 与 BackpackPanel
    /// 怎么被竖屏适配重锚<b>无关</b>，任何屏型都在可视区内。
    /// <para>2026-10-11 主人拍板（起因：正式关打完到这一步卡死）。旧实现按 BackpackPanel 的
    /// 本地矩形算「面板底边再往下 60」，可竖屏适配（UiLayoutStretch / BattleViewportFit）会把面板
    /// 重锚成贴底 —— 此时 r.yMin≈0，落点 y≈-60 直接掉到屏幕外，玩家根本看不到「确定」，
    /// 于是 StageClearRewardDirector 那个 <c>while (!lootDone)</c> 永远等不到回调 = 卡死。</para>
    /// 幂等，可反复调用。
    /// </summary>
    const float ConfirmLiftY = 90f;

    void ReanchorLootConfirmButton()
    {
        if (lootConfirmButton == null) return;
        var rt = lootConfirmButton.transform as RectTransform;
        var rootRt = transform as RectTransform;            // BattleUI 根 = 整块战斗 UI
        if (rt == null || rootRt == null) return;

        // 换父到根节点：不再受面板重锚/改高的影响（按钮自带 overrideSorting Canvas，层级不受影响）。
        if (rt.parent != rootRt) rt.SetParent(rootRt, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, ConfirmLiftY);
        rt.SetAsLastSibling();                              // 压在所有同级节点之上，别被遮挡
    }

    /// <summary>按节点名自动补全未拖拽的引用</summary>
    void AutoBindMissingRefs()
    {
        if (stageLabel == null) stageLabel = FindUIText("StageLabel");
        if (difficultyLabel == null) difficultyLabel = FindUIText("DifficultyLabel");
        // 【2026-10-18】DraftDisplay 子节点已改名 DraftCoinIcon/DraftCoinText（不再与金币区重名）；
        // 下面的排除逻辑保留当防呆：以后美术若再摆同名节点，顶栏金币也不会被串绑。
        if (goldText == null)
        {
            var t = FindDeepExcluding(transform, "GoldText", FindDeepChildIgnoreCase(transform, "DraftDisplay"));
            if (t != null) goldText = t.GetComponent<Text>();
        }
        if (talentStoneText == null)
            talentStoneText = FindUIText("TalentText") ?? FindUIText("TalentStoneText") ?? FindUIText("DiamondText");
        if (enchantStoneText == null) enchantStoneText = FindUIText("EnchantText");
        if (decomposeMatText == null) decomposeMatText = FindUIText("DecomposeText");

        // 若金币误绑到分解文字，纠正
        if (goldText != null && goldText.gameObject.name.IndexOf("Decompose", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var realGold = FindUIText("GoldText");
            if (realGold != null) goldText = realGold;
        }

        if (settingsButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "SettingsButton");
            if (t != null) settingsButton = t.GetComponent<Button>();
        }
        if (characterPanel == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterPanel");
            if (t != null) characterPanel = t.gameObject;
        }
        if (characterButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterButton");
            if (t != null) characterButton = t.GetComponent<Button>();
        }
        if (autoButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "AutoButton");
            if (t != null) autoButton = t.GetComponent<Button>();
        }
        if (lootConfirmButton == null)
        {
            // 拾取模式的确认按钮：旧预制体叫「整理」，新美术还没出，两种名字都认
            Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel")
                ?? FindDeepChildIgnoreCase(transform, "Backpack");
            Transform t = null;
            if (backpack != null)
                t = FindDeepChildIgnoreCase(backpack, "确定")
                    ?? FindDeepChildIgnoreCase(backpack, "Confirm")
                    ?? FindDeepChildIgnoreCase(backpack, "ConfirmButton")
                    ?? FindDeepChildIgnoreCase(backpack, "整理")
                    ?? FindDeepChildIgnoreCase(backpack, "OrganizeButton");
            if (t == null)
                t = FindDeepChildIgnoreCase(transform, "确定")
                    ?? FindDeepChildIgnoreCase(transform, "ConfirmButton")
                    ?? FindDeepChildIgnoreCase(transform, "整理")
                    ?? FindDeepChildIgnoreCase(transform, "OrganizeButton");
            if (t != null) lootConfirmButton = t.GetComponent<Button>();
        }
        EnsureLootConfirmButton();

        if (questDesc == null) questDesc = FindUIText("QuestDesc");
        if (questProgress == null) questProgress = FindUIText("QuestProgress");
        if (questTitle == null) questTitle = FindUIText("QuestTitle");
        if (questRewardIcon == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglitubiao");
            if (rt != null) questRewardIcon = rt.GetComponent<Image>();
        }
        if (questRewardAmount == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglishuzi");
            if (rt != null) questRewardAmount = rt.GetComponent<Text>();
        }
        if (questPanel == null)
        {
            Transform qt = FindDeepChildIgnoreCase(transform, "QuestPanel")
                ?? FindDeepChildIgnoreCase(transform, "QuestText")
                ?? FindDeepChildIgnoreCase(transform, "TaskPanel")
                ?? FindDeepChildIgnoreCase(transform, "Task");
            if (qt != null) questPanel = qt.gameObject;
            else if (questDesc != null) questPanel = questDesc.transform.parent?.gameObject;
        }

        BindProgressBar();
        // 引用一律按名字绑齐；未解锁槽只是不改 Fill / 不加 Button
        BindCharacterSlot(ref playerSlot, "PlayerSlot", true);
        BindCharacterSlot(ref mercSlot1, "MercSlot1", false);
        BindCharacterSlot(ref mercSlot2, "MercSlot2", false);
        // 【2026-10-15 主人拍板】佣兵头像点击 = 查看「获得佣兵」详情（入口已从抽中默认弹出迁来）
        EnsureMercAvatarClick();

        BindSkillAvatar(ref playerSkillAvatar, "SkillBtn1", "PlayerSkill");
        BindSkillAvatar(ref merc1SkillAvatar, "SkillBtn2", "MercSkill1");
        BindSkillAvatar(ref merc2SkillAvatar, "SkillBtn3", "MercSkill2");

        // 新底部布局：skill 4 槽 / zhuangbei 5 槽（头/胸/脚/主手/副手）
        BindBottomQuickSlots();
        // 佣兵自带技能槽（角色卡内的 skill 节点），与上面玩家 4 槽是两套
        BindMercSkillSlots();

        EnsureGridCellsBound();
        FixCharacterBarLayout();
        // MercenaryManager 可能尚未创建，系统就绪后再 Wire / Configure
        WireSlotSkillClicks();
        ApplySoloBattleHud();
        // 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】顶栏货币 = 本局获得金币 → 用金币图；
        // （10-18 早前「顶栏统一抽奖币图」的口径被本条取代 —— 抽奖币挪到抽奖面板旁单独显示）
        EnsureTopBarGoldIcon();
        // 【2026-10-18 主人摆的 DraftDisplay 抽奖币条】绑数字 + 换抽奖币图
        EnsureDraftDisplayBindings();
        // 【2026-10-22 主人拍板】石像事件（圣者/祭司）：绑现成 buff 节点（恢复/诅咒子节点），
        // 平时 buff 整体隐藏；FindDeepChildIgnoreCase 模式同源（BattleStatueEventUI 内自带深搜，含未激活节点）
        BattleStatueEventUI.EnsureOn(transform);
        // 2026-10-24 主人拍板：战斗内网格背包改名「作战背包」，与角色页「随身仓库」区分，避免两个背包混为一谈。
        // 真源 = 预制体 BackpackPanel 下的 Text 节点 BackpackTitle / BackpackTitle (1)；运行时覆盖文案，不改预制体文件。
        EnsureBattleBackpackTitleLabel();
        // 【2026-10-26 主人拍板「只留一个抽奖券」】进场即隐藏金币/天赋石条，不等 0.1s 后的首次全量刷新
        ApplyTopBarResourceVisibility();
        ReportBindingStatus();
    }

    /// <summary>
    /// 【2026-10-24 主人拍板】战斗内网格背包标题改名「作战背包」：与角色页「随身仓库」区分，避免两个背包混为一谈。
    /// 节点名可能是 BackpackTitle 或 BackpackTitle (1)（两种都认）；只改<b>激活</b>节点，跳过隐藏副本。
    /// 缺节点不报错（fail soft，标题缺了只是少个名字，不影响玩法）。
    /// </summary>
    void EnsureBattleBackpackTitleLabel()
    {
        Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel")
            ?? FindDeepChildIgnoreCase(transform, "Backpack");
        if (backpack == null) return;
        SetBackpackTitleRecursive(backpack);
    }

    static void SetBackpackTitleRecursive(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name.IndexOf("BackpackTitle", System.StringComparison.OrdinalIgnoreCase) >= 0
                && c.gameObject.activeInHierarchy)
            {
                var t = c.GetComponent<Text>();
                if (t != null) t.text = "作战背包";
            }
            SetBackpackTitleRecursive(c);
        }
    }

    /// <summary>
    /// 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】顶栏货币图标用金币图（icon_gold）：
    /// 顶栏显示的是「本局获得金币」（会写回城镇那笔），抽奖币池另有自己的图（icon_draft_coin，
    /// 显示在抽奖面板旁）。显式加载保证不受预制体默认图影响；数字口径见 BattleUI.RefreshBattleHud。
    /// 缺图时 LogError 保持原图（fail closed，不静默拿别的图顶）。
    /// </summary>
    void EnsureTopBarGoldIcon()
    {
        // DraftDisplay 子树排除（防呆）：其子节点已改名 DraftCoinIcon，本处排除只是保险，防美术再摆同名
        Transform t = FindDeepExcluding(transform, "GoldIcon", FindDeepChildIgnoreCase(transform, "DraftDisplay"));
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img == null) return;
        var sp = Resources.Load<Sprite>("UI/Art/Icons/Common/icon_gold");
        if (sp == null)
        {
            Debug.LogError("[BattleUI] 金币图标取不到：Art/UI/Icons/Common/icon_gold —— 顶栏维持原图");
            return;
        }
        img.sprite = sp;
        img.preserveAspect = true;
    }

    /// <summary>
    /// 【2026-10-18 主人在 TopBar 下摆了 DraftDisplay 抽奖币资源条，子节点已改名 DraftCoinIcon/DraftCoinText
    ///（原叫 GoldIcon/GoldText，与顶栏金币区重名，主人拍板改名防混）】
    /// 绑定数字（<see cref="BattleUI.draftCoinText"/>）+ 显式设抽奖币图（icon_draft_coin）。
    /// 数字真源 = BattleManager.currentGold；后续变化由 BattleEntryDraftPanel.NotifyPoolGoldChanged
    /// → BattleUI.UpdateDraftCoin 同步，这里只负责绑引用 + 初值。
    /// 缺图时 LogError 保持原图（fail closed，不静默拿别的图顶）。
    /// </summary>
    void EnsureDraftDisplayBindings()
    {
        Transform dd = FindDeepChildIgnoreCase(transform, "DraftDisplay");
        if (dd == null) return;
        if (draftCoinText == null)
        {
            Transform t = FindDeepChildIgnoreCase(dd, "DraftCoinText");
            if (t != null) draftCoinText = t.GetComponent<Text>();
        }
        Transform iconT = FindDeepChildIgnoreCase(dd, "DraftCoinIcon");
        if (iconT != null)
        {
            var img = iconT.GetComponent<Image>();
            if (img != null)
            {
                var sp = Resources.Load<Sprite>("UI/Art/Icons/Common/icon_draft_coin");
                if (sp != null) { img.sprite = sp; img.preserveAspect = true; }
                else Debug.LogError("[BattleUI] 抽奖币图标取不到：Art/UI/Icons/Common/icon_draft_coin —— DraftDisplay 维持原图");
            }
        }
        // 初值：BattleManager 未装配时先写 0，RefreshBattleHud / NotifyPoolGoldChanged 会补上
        if (draftCoinText != null)
            draftCoinText.text = BattleManager.Instance != null
                ? BattleManager.Instance.currentGold.ToString() : "0";
    }

    /// <summary>DFS 找指定名节点，但整个跳过 exclude 子树（含 exclude 自己）；exclude 为空时同 FindDeepChildIgnoreCase。</summary>
    static Transform FindDeepExcluding(Transform root, string name, Transform exclude)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (exclude != null)
        {
            for (var n = root; n != null; n = n.parent)
                if (n == exclude) return null;
        }
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeepExcluding(root.GetChild(i), name, exclude);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// 绑定自检：把没绑上的引用一次性打到 Console（前缀 [BattleUI-绑定]），
    /// 美术/策划对照补节点后重跑即可看到清单变短。
    /// </summary>
    void ReportBindingStatus()
    {
        var miss = new List<string>();
        void Add(string field, bool ok) { if (!ok) miss.Add(field); }

        Add("StageLabel", stageLabel != null);
        Add("DifficultyLabel", difficultyLabel != null);
        Add("GoldText", goldText != null);
        Add("DraftDisplay.GoldText(抽奖币条)", draftCoinText != null);
        Add("天赋石文本", talentStoneText != null || enchantStoneText != null);
        Add("DecomposeText", decomposeMatText != null);
        Add("SettingsButton(右上角)", settingsButton != null);

        Add("ProgressBar", progressContainer != null);
        Add("PlayerMarker", playerMarker != null);
        Add("EndFlag", endFlag != null);
        if (progressNodes == null || progressNodes.Count == 0) miss.Add("ProgressNodes(Node_0..9)");

        Add("QuestPanel", questPanel != null);
        Add("QuestTitle", questTitle != null);
        Add("QuestDesc", questDesc != null);
        Add("QuestProgress", questProgress != null);
        Add("jianglitubiao", questRewardIcon != null);
        Add("jianglishuzi", questRewardAmount != null);

        void AddSlot(string tag, CharacterSlotUI s)
        {
            if (s == null || s.root == null) { miss.Add(tag + ".root"); return; }
            Add(tag + ".Portrait", s.portrait != null);
            Add(tag + ".HPBarFill", s.hpBarFill != null);
            Add(tag + ".HPText", s.hpText != null);
            Add(tag + ".lanBarFill", s.lanBarFill != null);
            Add(tag + ".lanText", s.lanText != null);
            Add(tag + ".LockedOverlay", s.lockedOverlay != null);
            // 【2026-10-07 主人拍板】Glow(光边) 绑定自检项删除：头像外圈金环整条链路已删。
            Add(tag + ".LevelLabel", s.levelLabel != null);
            Add(tag + ".NameText", s.nameText != null);
            Add(tag + ".PortraitPlaceholder", s.portraitPlaceholder != null);
        }
        AddSlot("PlayerSlot", playerSlot);
        AddSlot("MercSlot1", mercSlot1);
        AddSlot("MercSlot2", mercSlot2);

        Add("SkillBar(主动技槽容器)", skillSlotRoot != null);
        Add("zhuangbei(装备槽容器)", equipSlotRoot != null);
        if (runSkillSlots == null || runSkillSlots.Count == 0) miss.Add("主动技槽(0 个)");
        if (equipQuickSlots == null || equipQuickSlots.Count == 0) miss.Add("装备快捷槽(0 个)");
        if (gridCells == null || gridCells.Count == 0) miss.Add("GridContainer 格子(0 个)");
        if (mercSkillSlots == null || mercSkillSlots.TrueForAll(s => s == null || s.root == null))
            miss.Add("佣兵技能槽(MercSlot*/skill 均缺失)");

        Add("PlayerSlot.职业icon", playerSlot?.jobIcon != null);

        Add("拾取确定按钮", lootConfirmButton != null);
        Add("CharacterButton", characterButton != null);
        Add("CharacterPanel", characterPanel != null);
        Add("AutoButton", autoButton != null);

        string quick = equipQuickSlots != null && equipQuickSlots.Count > 0
            ? string.Join("/", equipQuickSlots.ConvertAll(s => s.slotType.ToString()))
            : "-";
        Debug.Log($"[BattleUI-绑定] 技能槽={runSkillSlots?.Count ?? 0} 装备槽={(equipQuickSlots?.Count ?? 0)}({quick}) " +
                  $"格子={gridCells?.Count ?? 0} 进度点={progressNodes?.Count ?? 0}");
        Debug.Log(miss.Count == 0
            ? "[BattleUI-绑定] 全部就绪 ✅"
            : "[BattleUI-绑定] 缺失 " + miss.Count + " 项 -> " + string.Join(" | ", miss));
    }

    /// <summary>GameRoot/佣兵系统就绪后重绑：Fill、点击、进度条、槽位刷新</summary>
    public void RebindAfterSystemsReady()
    {
        BattleSideHud.EnsureOn(transform);
        BindProgressBar();
        ApplySoloBattleHud();
        int maxSlots = GameConfig.SOLO_PLAYER_BATTLE ? 0
            : MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
        if (maxSlots > 0) ApplyFillBars(mercSlot1);
        if (maxSlots > 1) ApplyFillBars(mercSlot2);
        WireSlotSkillClicks();
        BindAutoBattleUnavailable();
        UpdateCharacterSlots();
        UpdateSkillAvatars();
        UpdateRunSkillSlots();
        UpdateMercSkillSlots();
        UpdateEquipQuickSlots();
        // 【2026-10-21 主人拍板】进度条改按当前关波次推进（与 RefreshBattleHud 同口径）：
        // 已清波数 = 当前进行中波下标（-1 尚未开刷 → 0），总波数 = _waves.Count（同 Assembly，internal 可读）。
        var bmProgress = BattleManager.Instance;
        if (bmProgress != null)
        {
            int totalW = bmProgress._waves != null ? bmProgress._waves.Count : 0;
            int clearedW = Mathf.Clamp(bmProgress._activeWaveIndex, 0, totalW);
            UpdateWaveProgress(clearedW, totalW);
        }
        // 系统一就绪立刻校正摇杆/遮罩开关状态（bm 此时已装配，能正确打开摇杆，避免进战斗无摇杆）
        RefreshLootModeChrome();
    }

    void BindProgressBar()
    {
        Transform t = FindDeepChildIgnoreCase(transform, "ProgressBar")
            ?? FindDeepChildIgnoreCase(transform, "Progress")
            ?? FindDeepChildIgnoreCase(transform, "StageProgress");
        if (t != null) progressContainer = t;
        if (progressContainer == null) return;

        // 【2026-10-26 主人拍板】顶部进度条整条去掉：绑定时先藏一次（后续显隐由 Hud.UpdateStageProgress 关死）
        if (progressContainer.gameObject.activeSelf)
            progressContainer.gameObject.SetActive(false);

        Transform markerT = FindDeepChildIgnoreCase(progressContainer, "PlayerMarker");
        if (markerT != null)
        {
            playerMarker = markerT.GetComponent<Image>();
            if (playerMarker == null) playerMarker = markerT.GetComponentInChildren<Image>(true);
        }

        Transform flagT = FindDeepChildIgnoreCase(progressContainer, "EndFlag");
        if (flagT != null)
        {
            endFlag = flagT.GetComponent<Image>();
            if (endFlag == null) endFlag = flagT.GetComponentInChildren<Image>(true);
        }

        // 每次按 Node_0..Node_4 重建，丢掉预制体里多余的空 fileID
        // 【2026-10-23 批 B1】大关 5 小关制：只认前 5 个节点（预制体里多的 Node_5..9 自然忽略）
        progressNodes = new List<Image>(GameConfig.STAGES_PER_GRAND);
        for (int i = 0; i < GameConfig.STAGES_PER_GRAND; i++)
        {
            Transform n = FindDeepChildIgnoreCase(progressContainer, $"Node_{i}");
            if (n == null) continue;
            var img = n.GetComponent<Image>();
            if (img == null)
            {
                img = n.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;
            }
            progressNodes.Add(img);
        }
    }

    static void ApplyFillBars(CharacterSlotUI slot)
    {
        if (slot == null) return;
        ConfigureFillBar(slot.hpBarFill);
        ConfigureFillBar(slot.lanBarFill);
        ConfigureFillBar(slot.energyRing);
    }

    void BindCharacterSlot(ref CharacterSlotUI slot, string rootName, bool configureFills = true)
    {
        if (slot == null) slot = new CharacterSlotUI();
        if (slot.root == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, rootName);
            if (t != null) slot.root = t.gameObject;
        }
        if (slot.root == null) return;
        Transform root = slot.root.transform;

        if (slot.portrait == null)
        {
            // 用户约定：Portrait 是头像位；PlayerSlot 是头像框背景，不能被当成头像图层
            Transform portraitRoot = FindDeepChildIgnoreCase(root, "Portrait");
            if (portraitRoot != null)
                slot.portrait = portraitRoot.GetComponent<Image>();
            if (slot.portrait == null)
                slot.portrait = FindImageNamedNoFallback(root, "Portrait");
        }
        // 头像框：玩家槽 root 自己就叫 PlayerSlot，佣兵槽是 root 下的 PlayerSlot 子节点。
        // 只取这一层的 Image 当框，不做任何尺寸/节点改动。
        if (slot.frameImage == null)
        {
            Transform frameRoot = FindDeepChildIgnoreCase(root, "PlayerSlot");
            if (frameRoot != null && frameRoot != slot.portrait?.transform)
                slot.frameImage = frameRoot.GetComponent<Image>();
        }
        // 占位图：有真实头像时隐藏
        if (slot.portraitPlaceholder == null)
        {
            Transform ph = FindDeepChildIgnoreCase(root, "PortraitPlaceholder");
            if (ph != null) slot.portraitPlaceholder = ph.gameObject;
        }

        if (slot.levelLabel == null)
            slot.levelLabel = FindTextNamed(root, "LevelLabel", "Level", "Lv");
        if (slot.nameText == null)
            slot.nameText = FindTextNamed(root, "Name", "NameText", "PlayerName");

        // HP：优先 HPBarBg/HPBarFill，避免误绑到底板
        if (slot.hpBarFill == null)
        {
            Transform hpBg = FindDeepChildIgnoreCase(root, "HPBarBg")
                ?? FindDeepChildIgnoreCase(root, "HPBarBG");
            if (hpBg != null)
                slot.hpBarFill = FindImageNamedNoFallback(hpBg, "HPBarFill", "Fill");
            if (slot.hpBarFill == null)
                slot.hpBarFill = FindImageNamedNoFallback(root, "HPBarFill", "HPFill", "HpFill");
        }
        if (slot.hpText == null)
            slot.hpText = FindTextNamed(root, "HPText", "HpText");

        // 蓝条/技能能量：lanBarBg/lanBarFill
        if (slot.lanBarFill == null)
        {
            Transform lanBg = FindDeepChildIgnoreCase(root, "lanBarBg")
                ?? FindDeepChildIgnoreCase(root, "LanBarBg");
            if (lanBg != null)
                slot.lanBarFill = FindImageNamedNoFallback(lanBg, "lanBarFill", "LanBarFill", "Fill");
            if (slot.lanBarFill == null)
                slot.lanBarFill = FindImageNamedNoFallback(root, "lanBarFill", "LanBarFill");
        }
        if (slot.lanText == null)
            slot.lanText = FindTextNamed(root, "lanText", "LanText");

        // 兼容旧能量环命名
        if (slot.energyRing == null)
            slot.energyRing = FindImageNamedNoFallback(root, "Energy", "EnergyRing", "Ring");

        // 【2026-10-07 主人拍板删除】原 glowBorder 的 Glow / GlowBorder / SkillGlow 三个查找。
        // 头像外圈那个「技能就绪」金色大圆环不要了（自动释放，不需要就绪提示）。
        // 按用户要求：不对头像框做任何运行时改造，不再改尺寸/新增节点

        if (slot.lockedOverlay == null)
        {
            Transform l = FindDeepChildIgnoreCase(root, "LockedOverlay")
                ?? FindDeepChildIgnoreCase(root, "Locked")
                ?? FindDeepChildIgnoreCase(root, "Lock");
            if (l != null) slot.lockedOverlay = l.gameObject;
        }
        // 新预制体没有锁遮罩节点：运行时补一个（默认隐藏），这样「未解锁」才有可见效果
        slot.EnsureLockedOverlay();
        // 右上角技能角标容器先备好（有技能图标时才显示）
        slot.EnsureSkillBadge();
        // 美术还没画的名字 / 等级 / 占位底盘 / 技能就绪光边：一律运行时新建节点补上，
        // 绑定自检（[BattleUI-绑定]）里的 NameText / LevelLabel / Glow / PortraitPlaceholder 就是缺这四样。
        slot.EnsureRuntimeDecorations();

        // 职业图标位：xuetiaodi/职业icon（玩家=所选职业，佣兵=佣兵职业）
        if (slot.jobIcon == null) slot.jobIcon = EnsureJobIcon(root);

        // 未解锁槽：不改 Image.Filled，完全保留美术默认
        if (configureFills) ApplyFillBars(slot);
    }

    static void ConfigureFillBar(Image img)
    {
        if (img == null) return;
        if (img.type != Image.Type.Filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    // 【2026-10-06 已删除】private static bool TutorialMercDrawn（本局抽数判据）。
    //   主人拍板：同一个语义只留一个出口 → 搬到 SlotMachineSystem.TutorialMercDrawn，
    //   BattleUI 与 TutorialDirector.ShowMercHud 都来读它，这里不再写第二份。

    void ApplySoloBattleHud()
    {
        // 单人模式也保留三个头像位：未解锁显示「锁定」，不要藏掉第 3 个
        bool showTutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        SetSlotRootActive(playerSlot, true);
        SetSlotRootActive(mercSlot1, true);
        SetSlotRootActive(mercSlot2, true);

        // 2026-10-06 主人拍板：口径变更，玩家名要正常显示（旧口径「玩家卡只留头像」已作废）。
        // 名字由 BattleUI.CharacterBar 的 playerSlot.UpdateSlot(PlayerIdentity.DisplayName, ...) 写入。

        // 单人/引导：两格伙伴都按未解锁处理，清掉占位血量数字
        bool lockExtraSlots = (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle) && !showTutorialMerc;
        if (lockExtraSlots)
        {
            // 2026-10-05 主人拍板：引导局这个槽「灰掉 + 有佣兵头像和血条 + 没有锁的图标 + 稀有度头像框」，
            // 也就是把「三选一解锁」的空锁换成引导佣兵（塔克）本人的灰像，玩家一眼看到要招的是谁。
            //
            // ⚠ 但**不是一开局就摆出来**（主人 2026-10-05 报「佣兵还没抽怎么就已经在头像栏里了」）：
            // 得等玩家真抽到佣兵才亮。真源只有 SlotMachineSystem.TutorialMercDrawn 一处
            //（本局抽数有没有越过「佣兵」那一格）—— 抽之前 = 空锁；抽完（招募入队）= 走正常头像。
            // 抽数是局内数据所以每局都会归零；碎片那种跨局存档会残留，绝不能拿来当判据。
            // 【2026-10-08 主人拍板删除】ApplyTutorialMercPreview（灰掉 + 塔克头像 + 血条）那一支。
            //   ① 口径是「入队之后才显示头像」，入队前露灰头像正好相反；
            //   ② 它要求 TutorialMercDrawn，而外层 lockExtraSlots 要求 !showTutorialMerc，
            //      两者互斥 → 本来就走不到。删掉后这里恒为「未解锁」。
            mercSlot1?.ShowUnavailable(MercLockedHint);
        }
        else
            mercSlot1?.SetLocked(false);

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
            mercSlot2?.ShowUnavailable(MercLockedHint);
        else
            mercSlot2?.SetLocked(false);

        SetAvatarRootActive(merc1SkillAvatar, showTutorialMerc || !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
        SetAvatarRootActive(merc2SkillAvatar, !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
    }

    public void ApplySoloBattleHudPublic() => ApplySoloBattleHud();

    static void SetSlotRootActive(CharacterSlotUI slot, bool active)
    {
        if (slot?.root != null) slot.root.SetActive(active);
    }

    static void SetAvatarRootActive(SkillAvatarUI avatar, bool active)
    {
        if (avatar?.root != null) avatar.root.SetActive(active);
    }

    /// <summary>
    /// 技能全部改为被动、充能满自动释放（PlayerSkillPassive / MercSkillCaster），
    /// 头像与槽位不再挂 Button 做手动释放，这里保留空实现供既有调用点继续呼叫。
    /// </summary>
    void WireSlotSkillClicks() { }

    void BindSkillAvatar(ref SkillAvatarUI avatar, params string[] names)
    {
        if (avatar == null) avatar = new SkillAvatarUI();
        if (avatar.root != null) return;
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(transform, names[i]);
            if (t == null) continue;
            avatar.root = t.gameObject;
            if (avatar.avatarImage == null)
                avatar.avatarImage = FindImageNamed(t, "Avatar", "Icon", "Portrait", "Mask");
            if (avatar.energyRing == null)
                avatar.energyRing = FindImageNamed(t, "Energy", "EnergyRing", "Ring");
            if (avatar.glowBorder == null)
            {
                Transform g = FindDeepChildIgnoreCase(t, "Glow");
                if (g != null) avatar.glowBorder = g.GetComponent<Image>();
            }
            break;
        }
    }
}
