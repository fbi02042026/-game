using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：刷新与压暗（Refresh 全量、InfoBar/格子 CaptureBase 压暗、底部进度行）
public partial class DailyLoginUI
{

    void Refresh()
    {
        int days = DailyLoginSystem.LoginDays;

        // ---- 8 天格 ----
        // 2026-09-23 轮回（主人要求）：第 1 轮 1~8 天，第 2 轮 9~16 天，第 3 轮 17~24 天……
        // 奖励仍按 Starter[i] 循环取；已领记录按累计天数存，所以不需要重置存档。
        int lastDay = DailyLoginDefs.Starter.Length;
        int round = lastDay > 0 ? (Mathf.Max(1, days) - 1) / lastDay : 0;
        // 佣兵日不再硬编码：B8 改为按每格奖励 r.grant == Grant.Merc 判定「限定」角标（FindStarterMercSlot 保留备用）
        for (int i = 0; i < _starterCells.Count && i < lastDay; i++)
        {
            var cell = _starterCells[i];
            // C1: 每帧先清染色标记，再由本格奖励重新判定（稀有度色每格独立，绝不跨天累积）
            cell.hasRarityTint = false;
            int day = round * lastDay + i + 1;                      // ★ 轮回后的真实累计天数
            var r = DailyLoginSystem.EffectiveStarterReward(day);   // 轮回后佣兵日自动换碎片，显示=实发
            cell.day = day;                        // 点击领取用的就是这个

            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            bool claimed = DailyLoginSystem.IsStarterClaimed(day);
            bool reached = days >= day;
            bool canPress = reached && !claimed;

            // 2026-09-25 主人反馈「不能领取的天点一下就变亮」：真凶是 UiButtonPressFeedback.OnPointerUp。
            // 它只有 OnPointerDown 判了 interactable，OnPointerUp / OnPointerExit / OnDisable 都无条件 Restore()，
            // 而 Restore 写回的是 Awake 时抓的旧 base 色（亮色），会把下面压暗的颜色顶掉；
            // 不可领的格子又不会触发 onClick → 没有 Refresh 纠正，于是就一直亮着。
            // 所以：不可领的天直接把该组件摘掉（摘掉时 OnDisable→Restore 会写一次旧色，必须放在本格写色之前）。
            SyncPressFeedback(cell, canPress);

            cell.dayText.text = "第 " + day + " 天";   // 已领/未解锁状态用底色与文字颜色区分
            // B6: 奖励名去掉稀有度前缀（"稀有佣兵·" 等），并把 \n 换成 ·
            cell.rewardText.text = StripRarity(r.DisplayName);
            if (cell.x2Badge != null) cell.x2Badge.SetActive(dbl);
            // B8: 「限定」标识位置以预制体为准，代码只控 active；判定改为该格奖励是 Merc 才显示（佣兵日后自动换碎片则不显示）
            if (cell.rareBadge != null) cell.rareBadge.SetActive(r.grant == DailyLoginDefs.Grant.Merc && !claimed);

            // B7: 碎片格带佣兵头像（复用 MercGrowUI.FragmentCard），其余类型走原 SetIcon
            bool isFrag = r.grant == DailyLoginDefs.Grant.MercFragment
                       || r.grant == DailyLoginDefs.Grant.LegendaryFragment;
            // C1: 碎片格奖励名按稀有度染色（主人要求）。只给本格记一个「原色覆盖值」，
            //     不碰 baseRewardColor（预制体原色），所以关开关 / 非碎片格 / 换轮回后都能干净还原。
            if (isFrag && EnableFragmentNameRarityColor)
            {
                var rc = RarityPalette.Get(RarityOfGrant(r.grant, r.hireId));
                // 只换 RGB、保留预制体文字的 alpha —— 以主人预制体为准，代码只兜底
                cell.rarityTint = new Color(rc.r, rc.g, rc.b, cell.baseRewardColor.a);
                cell.hasRarityTint = true;
            }
            if (isFrag && cell.fragCard != null)
            {
                // 底图图标本身隐藏，只显示带头像的碎片卡（绝不让底图图盖住头像）
                if (cell.icon != null) { cell.icon.enabled = false; cell.icon.color = new Color(1f, 1f, 1f, 0f); }
                if (cell.iconFallback != null) cell.iconFallback.gameObject.SetActive(false);
                // 普通/稀有佣兵碎片按 hireId 查档位；传说碎片固定 Legendary；查不到回退 Rare
                // C1: 与奖励名染色共用同一份取稀有度逻辑（RarityOfGrant），别写两份
                MercRosterDefs.MercRarity fr = RarityOfGrant(r.grant, r.hireId);
                cell.fragCard.Root.SetActive(true);
                cell.fragCard.Apply(fr, r.hireId);
            }
            else
            {
                if (cell.fragCard != null && cell.fragCard.Root != null) cell.fragCard.Root.SetActive(false);
                if (cell.icon != null) cell.icon.enabled = true;
                var sp = LoadRewardIcon(r);
                SetIcon(cell, sp, r.name);
            }

            // ★ 已领斜字：只有 claimed 状态亮出来
            if (cell.markText != null) cell.markText.gameObject.SetActive(claimed);

            // B5: 格子底框只灰度、绝不染色（CaptureBase：以预制体 base 原色为基准做灰度暗化，绝不写死深蓝覆盖美术底图）
            Color bgBase = cell.baseBgColor;
            Color dayBase = cell.baseDayColor;
            if (claimed)
            {
                // 已领：灰度暗化（保持灰不偏色）；关开关则还原 base
                float t = EnableCellDim ? 0.65f : 0f;
                cell.bg.color = Color.Lerp(bgBase, new Color(0.35f, 0.35f, 0.35f, bgBase.a), t);
                cell.dayText.color = Color.Lerp(dayBase, new Color(0.35f, 0.35f, 0.35f, dayBase.a), t);
                // B11: 底框/天数之外的一切（奖励名、图标、X2、限定、已领斜字、碎片卡）一起压暗，
                //      修掉「领取过的只有底和天数暗了，字还亮着」（主人 2026-09-25 反馈）
                ApplyCellDim(cell, EnableCellDim ? DIM_CLAIMED_K : 1f);
            }
            else if (reached)
            {
                // 今天可领：原样；关开关则强行还原 base
                if (EnableCellDim) { cell.bg.color = bgBase; cell.dayText.color = dayBase; }
                ApplyCellDim(cell, 1f);
            }
            else
            {
                // 未解锁：暗化（灰度，保持灰不偏色）；关开关则还原 base
                float t = EnableCellDim ? 0.45f : 0f;
                cell.bg.color = Color.Lerp(bgBase, new Color(0.30f, 0.30f, 0.30f, bgBase.a), t);
                cell.dayText.color = Color.Lerp(dayBase, new Color(0.30f, 0.30f, 0.30f, dayBase.a), t);
                // 2026-10-15 主人拍板：不能领取的天「框和里面的奖励都要灰显」——底框/天数之外的元素（奖励名/图标/角标/碎片卡）一起压暗
                ApplyCellDim(cell, EnableCellDim ? DIM_CLAIMED_K : 1f);
            }

            cell.btn.interactable = canPress;
            // ★ 选中图片：仅当天可领的天（reached && !claimed）显示，其余状态（已领/未解锁）一律隐藏
            if (cell.selectedNode != null) cell.selectedNode.SetActive(canPress);
        }

        // ---- 每日循环 ----
        int idx = DailyLoginSystem.CycleIndex;
        bool cycleDone = DailyLoginSystem.CycleClaimedToday;
        var cr = DailyLoginDefs.Cycle[idx];
        if (_cycleName != null) _cycleName.text = "今日：" + cr.DisplayName.Replace("\n", "、");
        if (_cycleState != null)
        {
            _cycleState.text = cycleDone ? "已领取" : "领取";
            _cycleState.color = cycleDone ? new Color(0.55f, 0.55f, 0.58f, 1f) : new Color(0.16f, 0.06f, 0.04f, 1f);
        }
        if (_cycleBtn != null) _cycleBtn.interactable = !cycleDone;

        // ---- 连击档 ----
        int streak = DailyLoginSystem.StreakDays;
        if (_streakTag != null) _streakTag.text = $"连击 {streak} 天";
        for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
        {
            var s = DailyLoginDefs.Streak[i];
            bool sClaimed = DailyLoginSystem.IsStreakClaimed(s.days);
            bool sReached = streak >= s.days;
            if (_streakLabels[i] != null)
            {
                // 按钮窄（窄屏约 80px），文案保持两三个字，状态用颜色区分
                _streakLabels[i].text = sClaimed ? "已领" : $"连{s.days}天";
                _streakLabels[i].color = sReached && !sClaimed ? ColGold : new Color(0.62f, 0.60f, 0.58f, 1f);
            }
            if (_streakBtns[i] != null)
            {
                _streakBtns[i].interactable = sReached && !sClaimed;
                var img = _streakBtns[i].GetComponent<Image>();
                if (img != null)
                    img.color = sClaimed ? new Color(0.14f, 0.14f, 0.15f, 1f)
                              : sReached ? ColGoldDeep
                              : new Color(0.18f, 0.16f, 0.18f, 1f);
            }
        }

        // ---- 底部信息条：累计大奖领取入口（B4）----
        // 累计大奖三态：已领 / 可领 / 未达天数；只改颜色、不改文字、不改位置（文案以预制体为准）
        bool accumGot = DailyLoginSystem.IsAccumClaimed();
        bool accumRdy = DailyLoginSystem.IsAccumReached() && !accumGot;
        // B3: 左侧天数改回主人格式（预制体原文 "1 / 8"）
        if (_infoDays != null)
            _infoDays.text = days + " / " + DailyLoginDefs.AccumTargetDays;
        // B10: 底部进度行跟着累计天数走（只改那一行，其余 MercText 保持预制体原文）
        ApplyInfoProgressText(days);
        // B4: 整条压暗（CaptureBase 模式）+ 特效挂点，绝不碰文字/位置
        ApplyInfoDim(accumGot, accumRdy);
    }

    /// <summary>
    /// B4: 首次调用时记录 InfoBar 下每个 Graphic 的「原色」（CaptureBase 模式）。
    /// 之后压暗都从 base 重算，绝不写死 Color.white——否则会把主人美术配色整个换掉。
    /// MercIcon 单独用 _infoMercIcon 字段管理（B1 永不动其 sprite）；MercText 系列走 _infoMercTexts。
    /// </summary>
    void CaptureInfoBase()
    {
        if (_infoDimCaptured) return;
        var info = FindChildDeep(transform, "InfoBar");
        if (info == null) return;
        var gs = info.GetComponentsInChildren<Graphic>(true);
        _infoGraphics.Clear();
        _infoBaseColors.Clear();
        _infoMercTextBaseColors.Clear();
        foreach (var g in gs)
        {
            // B1: 佣兵大图用字段单独管理；B2: MercText 系列单独走列表。两者都不进整条压暗集合
            if (g.gameObject.name == "MercIcon") continue;
            if (g.gameObject.name.StartsWith("MercText")) continue;
            _infoGraphics.Add(g);
            _infoBaseColors.Add(g.color);
        }
        // 单独纳入 _infoMercIcon（B1 永不动其 sprite，只随整条压暗改 color）
        if (_infoMercIcon != null) { _infoGraphics.Add(_infoMercIcon); _infoBaseColors.Add(_infoMercIcon.color); }
        // MercText 系列：只记录原色，压暗时只改 color 绝不改 text
        foreach (var t in _infoMercTexts) _infoMercTextBaseColors.Add(t.color);
        _infoDimCaptured = true;
    }

    /// <summary>B4: 一键还原到首次记录的原色（EnableInfoDim=false 时调用）。</summary>
    void RestoreInfoBase()
    {
        if (!_infoDimCaptured) return;
        for (int i = 0; i < _infoGraphics.Count; i++) _infoGraphics[i].color = _infoBaseColors[i];
        for (int i = 0; i < _infoMercTexts.Count; i++) _infoMercTexts[i].color = _infoMercTextBaseColors[i];
    }

    /// <summary>
    /// B4: 底部信息条压暗 + 特效挂点。只改颜色、不改文字、不改位置（文案以预制体为准）。
    ///   accumGot   已领：整条压暗 ×0.45 灰
    ///   accumRdy   可领：保持原色（原样）
    ///   未达天数        ：压暗 ×0.55 灰
    /// 特效挂点：在 InfoBar 下运行时找名为 Effect / Glow 的子节点，找到才 SetActive(accumRdy)，找不到 no-op（绝不新建）。
    /// </summary>
    void ApplyInfoDim(bool accumGot, bool accumRdy)
    {
        CaptureInfoBase();

        // 2026-09-25 主人要求：底部信息条「可领取」时——① 给 InfoBar 的 Image 挂上发光材质球；② 打开 effect 节点。
        // 跟压暗是两件事，故放在压暗开关之前；压暗关掉时这两样仍按 accumRdy 生效。
        var infoGlow = FindChildDeep(transform, "InfoBar");
        var infoImg = infoGlow != null ? infoGlow.GetComponent<Image>() : null;
        if (infoImg != null)
        {
            // 首次捕获：记下主人预制体里那颗发光材质球（只抓一次，之后都从缓存取，避免把写进去的值又当 base）
            if (_infoGlowMat == null && infoImg.material != null) _infoGlowMat = infoImg.material;
            // 可领取 + 开关开 → 发光材质球；否则还原成没有材质球（默认 UI 材质）
            infoImg.material = (EnableInfoGlow && accumRdy) ? _infoGlowMat : null;
        }
        // effect / glow 子节点：任意深度 + 忽略大小写（主人加的叫小写 effect），找不到 no-op、绝不新建
        var fxGo = FindInfoEffect(infoGlow);
        if (fxGo != null) fxGo.gameObject.SetActive(EnableInfoGlow && accumRdy);   // Transform 没有 SetActive，要走 .gameObject

        if (!EnableInfoDim) { RestoreInfoBase(); return; }

        float k = accumGot ? 0.45f : (accumRdy ? 1f : 0.55f);
        for (int i = 0; i < _infoGraphics.Count; i++)
        {
            var baseC = _infoBaseColors[i];
            // 整条压暗：base × k（保持 alpha，避免整条变透明）
            _infoGraphics[i].color = new Color(baseC.r * k, baseC.g * k, baseC.b * k, baseC.a);
        }
        for (int i = 0; i < _infoMercTexts.Count; i++)
        {
            var baseC = _infoMercTextBaseColors[i];
            _infoMercTexts[i].color = new Color(baseC.r * k, baseC.g * k, baseC.b * k, baseC.a);
        }
    }

    // 2026-10-15 主人拍板：可领取的天保持亮即可、不要描边——原 Outline 金圈（ApplyGlow）整条删除。

    /// <summary>
    /// 2026-09-25：格子按压反馈同步（主人要求：不可领的天点了不能有任何视觉反馈）。
    ///   on = true  → 可领取：确保挂上 UiButtonPressFeedback，按压缩放/变暗反馈照常。
    ///   on = false → 未达成 / 已领取：摘掉该组件，点击不缩放、不变亮、不变色。
    ///
    /// 为什么是摘组件而不是设 suppress：UiButtonPressFeedback.OnPointerUp 没判 interactable 也没判 suppress，
    /// 无条件 Restore() 会把 Awake 时抓的旧「亮色」base 写回底图，顶掉本文件压暗后的颜色（详见 Refresh 里的注释）。
    ///
    /// ⚠ 调用时机：Destroy 会触发 OnDisable → Restore（写一次旧色），
    ///   所以本方法必须在**本格写颜色之前**调用，写完色才不会被旧色冲掉。
    /// </summary>
    static void SyncPressFeedback(Cell c, bool on)
    {
        if (c?.go == null) return;
        var pf = c.go.GetComponent<UiButtonPressFeedback>();
        if (on)
        {
            if (pf == null) c.go.AddComponent<UiButtonPressFeedback>();   // Awake 抓当前（正常）色为 base
        }
        else if (pf != null)
        {
            UnityEngine.Object.Destroy(pf);
        }
    }

    /// <summary>
    /// B11: 记录格子内所有可压暗元素的**当前色**作为 base（CaptureBase 模式，与 baseBgColor/baseDayColor 同款）。
    /// 绑定 / 创建完成后调用一次；之后压暗一律从 base 重算，绝不写死颜色去覆盖美术配色。
    /// </summary>
    static void CaptureCellBase(Cell c)
    {
        if (c == null) return;
        c.baseBgColor = c.bg != null ? c.bg.color : Color.white;
        c.baseDayColor = c.dayText != null ? c.dayText.color : Color.white;
        c.baseRewardColor = c.rewardText != null ? c.rewardText.color : Color.white;
        c.baseIconColor = c.icon != null ? c.icon.color : Color.white;
        c.baseMarkColor = c.markText != null ? c.markText.color : Color.white;

        var x2Img = c.x2Badge != null ? c.x2Badge.GetComponent<Image>() : null;
        c.baseX2Color = x2Img != null ? x2Img.color : Color.white;
        var rareImg = c.rareBadge != null ? c.rareBadge.GetComponent<Image>() : null;
        c.baseRareColor = rareImg != null ? rareImg.color : Color.white;

        // 角标里的附属文字（如 X2 角标内的 "X2"）：主人反馈「字没暗」，所以连子级 Graphic 一起记
        c.dimExtras.Clear();
        c.dimExtraBases.Clear();
        CollectDimExtras(c.x2Badge, c.dimExtras, c.dimExtraBases);
        CollectDimExtras(c.rareBadge, c.dimExtras, c.dimExtraBases);

        var fc = c.fragCard;
        c.baseFragClipColor = fc != null && fc.Clip != null ? fc.Clip.color : Color.white;
        c.baseFragPortraitColor = fc != null && fc.Portrait != null ? fc.Portrait.color : Color.white;
        c.baseFragFrameColor = fc != null && fc.Frame != null ? fc.Frame.color : Color.white;
    }

    /// <summary>B11: 收集角标子树里的附属 Graphic（角标自身那张 Image 走 baseX2Color / baseRareColor，不重复收）。</summary>
    static void CollectDimExtras(GameObject root, List<Graphic> gs, List<Color> bases)
    {
        if (root == null) return;
        var self = root.GetComponent<Graphic>();
        var all = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var g = all[i];
            if (g == null || g == self) continue;
            gs.Add(g);
            bases.Add(g.color);
        }
    }

    /// <summary>
    /// B11: 格子内「底框 / 天数之外」的元素统一压暗或还原。k = 1 还原到 base，k &lt; 1 为 base × k（保持 alpha，避免变透明）。
    /// 只改 color：**绝不改 sprite、绝不改 active、绝不改位置**（碎片卡显示什么图由 Refresh 的 Apply 决定）。
    /// </summary>
    static void ApplyCellDim(Cell c, float k)
    {
        if (c == null) return;
        // C1: 碎片格用稀有度色当「原色」，其余格用预制体原色；k=1 即还原到该原色
        if (c.rewardText != null) c.rewardText.color = MulColor(RewardBase(c), k);
        // 2026-10-15 主人拍板：「已领」二字不灰显，恒用原色
        if (c.markText != null) c.markText.color = c.baseMarkColor;

        // 图标：没 sprite 时 SetIcon 已把它调成透明（避免露白块），这里必须保持透明，绝不能把白块还原出来
        if (c.icon != null && c.icon.enabled)
            c.icon.color = c.icon.sprite != null
                ? MulColor(c.baseIconColor, k)
                : new Color(c.baseIconColor.r, c.baseIconColor.g, c.baseIconColor.b, 0f);

        var x2Img = c.x2Badge != null ? c.x2Badge.GetComponent<Image>() : null;
        if (x2Img != null) x2Img.color = MulColor(c.baseX2Color, k);
        var rareImg = c.rareBadge != null ? c.rareBadge.GetComponent<Image>() : null;
        if (rareImg != null) rareImg.color = MulColor(c.baseRareColor, k);
        for (int i = 0; i < c.dimExtras.Count; i++)
            c.dimExtras[i].color = MulColor(c.dimExtraBases[i], k);

        // 碎片卡：只压暗 Clip / Portrait / Frame 三张 Image 的 color，绝不换 sprite
        var fc = c.fragCard;
        if (fc != null)
        {
            if (fc.Clip != null) fc.Clip.color = MulColor(c.baseFragClipColor, k);
            if (fc.Portrait != null) fc.Portrait.color = MulColor(c.baseFragPortraitColor, k);
            if (fc.Frame != null) fc.Frame.color = MulColor(c.baseFragFrameColor, k);
        }
    }

    /// <summary>B11: base × k，保持 alpha（压暗时绝不让元素变透明）。</summary>
    static Color MulColor(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    /// <summary>
    /// C1: 本格奖励名的「原色」——碎片格是稀有度色（rarityTint），其余格是预制体原色（baseRewardColor）。
    /// 只读取值，绝不回写 baseRewardColor：保证任何一帧都从 base / 稀有度色重算，不会累积漂移。
    /// </summary>
    static Color RewardBase(Cell c) => c != null && c.hasRarityTint ? c.rarityTint : c.baseRewardColor;

    /// <summary>
    /// B10: 认出底部信息条里的「进度文案」那一行（预制体原文 "第4天可领取"）。
    /// 铁律：只认「含『可领取』且不含『限定角色』」的那一行——"限定角色：战士" 是主人摆好的文案，绝不碰。
    /// 只认一次并缓存 index：改写成"已领取"之后，按内容就再也认不出来了。
    /// </summary>
    void ResolveInfoProgressText()
    {
        if (_infoProgressResolved) return;
        _infoProgressResolved = true;
        for (int i = 0; i < _infoMercTexts.Count; i++)
        {
            var t = _infoMercTexts[i];
            if (t == null) continue;
            string s2 = t.text ?? "";
            if (s2.Contains("限定角色")) continue;    // 主人特意摆的角色说明行，跳过
            if (!s2.Contains("可领取")) continue;     // 只认进度行
            _infoProgressIdx = i;
            return;
        }
    }

    /// <summary>
    /// B10: 底部「第N天可领取」跟着累计天数走（主人反馈：一天没领，最下面却写着"第4天"）。
    /// 只改 ResolveInfoProgressText 认出来的那一行，其余 MercText（含「限定角色」行）一律保持预制体原文。
    ///   未达 AccumTargetDays → "还差 N 天可领取" / 已达未领 → "可领取" / 已领 → "已领取"
    /// </summary>
    void ApplyInfoProgressText(int days)
    {
        ResolveInfoProgressText();
        if (_infoProgressIdx < 0 || _infoProgressIdx >= _infoMercTexts.Count) return;
        var t = _infoMercTexts[_infoProgressIdx];
        if (t == null) return;

        string txt;
        if (DailyLoginSystem.IsAccumClaimed()) txt = "已领取";
        else if (days >= DailyLoginDefs.AccumTargetDays) txt = "可领取";
        else txt = "还差 " + (DailyLoginDefs.AccumTargetDays - days) + " 天可领取";

        if (t.text != txt) t.text = txt;
    }
}
