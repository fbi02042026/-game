using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 角色栏：玩家与两名佣兵的头像、血蓝条、布局修正。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>
    /// 【2026-10-06 主人拍板】佣兵头像框右上角<b>不显示技能角标</b> ——
    /// 佣兵技能由 <c>MercSkillCaster</c> <b>自动释放</b>（<c>MercSkillMigrate.IsMercSkillAutoCast() == true</c>），
    /// 玩家既不需要看、也没有手动点击入口，图标一律隐藏。
    ///
    /// <para>旧口径（2026-09-26）只关 index 0 那一个槽，其余槽照旧显示 —— 现已作废，
    /// <b>全部佣兵槽都不显示</b>。旧开关 <c>HideFirstMercSkillBadge</c> 一并删除
    ///（铁律：不留 <c>xxxEnabled</c> 开关）—— 两处调用点直接写 <c>SetSkillBadge(null)</c>。</para>
    /// </summary>

    /// <summary>
    /// 只刷新玩家头像下的第二条（雷击奥义充能）。
    /// 击杀充能时由 HeroThunderUltimate 回调这里，避免动用完整的 UpdateCharacterSlots
    /// （那会连带刷新两名佣兵与布局，击杀频繁时没必要）。
    /// </summary>
    public void RefreshPlayerUltBar()
    {
        if (playerSlot == null) return;
        var ult = HeroThunderUltimate.Instance;
        // 组件还没建（或功能关闭）时也照常显示「0/10」——按需求第二条不隐藏
        if (ult == null)
            playerSlot.SetUltCharge(0f, 0, GameConfig.THUNDER_ULT_NEED_MIN, false);
        else
            playerSlot.SetUltCharge(ult.ChargeRatio, ult.Charge, ult.Need, ult.IsCasting);
    }

    /// <summary>
    /// 刷新三张角色卡的蓝色盾条（抵扣型护盾：佣兵 SK008 圣光庇护那一层）。
    /// 与血条分开刷：护盾掉值 / 到期时血量数字不一定变，不能挂进 RefreshLiveBars 的
    /// 「血量有变化才刷」判定里，否则盾条会卡在旧值不消失。
    /// </summary>
    public void RefreshShieldBars()
    {
        var passive = PlayerPassiveCombat.Instance;
        if (playerSlot != null)
            playerSlot.SetShield(passive != null ? passive.TeamShieldAmount : 0f,
                                 passive != null ? passive.TeamShieldMax : 0f);

        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        RefreshMercShield(mercSlot1, mercs, 0);
        RefreshMercShield(mercSlot2, mercs, 1);
    }

    static void RefreshMercShield(CharacterSlotUI slot, List<Mercenary> mercs, int index)
    {
        if (slot == null) return;
        var runner = (mercs != null && index < mercs.Count && mercs[index] != null)
            ? mercs[index].PassiveRunner
            : null;
        if (runner == null)
        {
            slot.SetShield(0f, 0f);
            return;
        }
        slot.SetShield(runner.ShieldAmount, runner.ShieldMax);
    }

    /// <summary>
    /// 更新角色栏
    /// </summary>
    public void UpdateCharacterSlots()
    {
        var mm = MercenaryManager.Instance;

        // 玩家槽位
        if (playerSlot != null)
        {
            if (playerSlot.lockedOverlay != null)
                playerSlot.lockedOverlay.SetActive(false);
            var hero = Hero.Instance;
            if (hero != null)
            {
                float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
                // 等级系统已停用（2026-09-14）：局内不再显示 Lv，属性只由天赋/装备决定
                playerSlot.UpdateSlot(PlayerIdentity.DisplayName, hero.level, hero.currentHp, maxHp, showLevel: false);
            }
            // 玩家头像对接；玩家技能走底部 4 个被动槽，头像右上角不放技能图标
            Sprite playerIcon = mm != null ? mm.GetPlayerIcon() : null;
            playerSlot.SetPortrait(playerIcon);
            // 头像框：玩家默认普通，日后「大厅考证」提档只改 MercHireSession.PlayerFrameRarity
            playerSlot.SetFrame(MercHireSession.LoadPlayerPortraitFrame());
            playerSlot.SetSkillBadge(null);
            // 【2026-10-06 主人二次拍板】常驻战力字搬到<b>玩家头像框</b>上（左上角那张卡的最左边）。
            // 宿主就是这张卡的头像框（预制体里叫 PlayerSlot 的那层），位置跟着它走。
            if (playerSlot.frameImage != null)
            {
                PlayerPowerHud.Ensure(hero != null ? hero.transform : null)
                              .AttachToFrame(playerSlot.frameImage.rectTransform);
            }
            else
            {
                Debug.LogError("[BattleUI] 玩家槽没有头像框（frameImage 为空），战力字无处可挂");
            }
            // 职业 icon：xuetiaodi/职业icon。2026-09-17 用户指定用 Icons/职业icon/ 四分类图
            // （防御/恢复/法术/物攻）。
            // 2026-09-26：去掉「取不到回退职业立绘头像」——那正是主人说的「总和玩家职业icon搞混」，
            // 现在四分类走 JobIconResolver.CombatBadge（Icons/职业icon → Icons/Job 副本），能取到。
            var selJob = PlayerJobDefs.GetSelected();
            playerSlot.SetJobIcon(PlayerJobDefs.TryLoadCombatBadgeIcon(selJob));
            // 第二条 = 雷击奥义充能（开关关闭也显示 0/N 空条，不隐藏）
            RefreshPlayerUltBar();
        }

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
        {
            ApplySoloBattleHud();
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud)
                RefreshTutorialMercSlot(mm);
            return;
        }

        // 佣兵槽位（根据酒馆等级解锁 + 存档出战佣兵）
        var mercIds = mm != null ? mm.GetActiveMercIds() : new List<string>();
        var mercHireIds = mm != null ? mm.GetActiveMercHireIds() : new List<string>();
        var activeMercs = mm != null ? mm.GetActiveMercs() : new List<Mercenary>();
        int maxSlots = ResolveMercSlotCount();

        SetupMercSlot(mercSlot1, 0, mercIds, mercHireIds, activeMercs, maxSlots, mm);
        SetupMercSlot(mercSlot2, 1, mercIds, mercHireIds, activeMercs, maxSlots, mm);
    }

    void RefreshTutorialMercSlot(MercenaryManager mm)
    {
        if (mercSlot1 == null || mm == null) return;
        var mercs = mm.GetActiveMercs();
        if (mercs == null || mercs.Count == 0 || mercs[0] == null) return;
        var m = mercs[0];
        mercSlot1.SetLocked(false);
        bool tutHasActive = m.SkillCaster != null && m.SkillCaster.HasActiveSkill;
        // 2026-10-06：蓝条只给「有蓝」的技能显示（治疗/法术型），无蓝技能纯 CD 不显示
        mercSlot1.SetEnergyEnabled(tutHasActive && m.SkillCaster != null
                                   && m.SkillCaster.MpType != MpArchetype.None);
        Sprite mercIcon = MercPortraitSprites.GetHead(!string.IsNullOrEmpty(m.hireId) ? m.hireId : StoryProgress.TutorialMercHireId)
            ?? mm.GetIcon(m.mercId);
        mercSlot1.SetPortrait(mercIcon);
        // 右上角小图标=该佣兵的技能：2026-10-06 主人拍板 → 佣兵技能角标全部隐藏（技能自动释放，不需要手点）
        mercSlot1.SetSkillBadge(null);
        // 教程救援佣兵不在存档出战列表里，技能圆形头像要单独绑
        merc1SkillAvatar?.SetAvatar(mercIcon);
        // 职业 icon：教程佣兵同样显示（四分类徽标，走 JobIconResolver.CombatBadge 唯一入口）
        mercSlot1.SetJobIcon(JobIconResolver.CombatBadge(mm != null ? mm.GetJobName(m.mercId) : null));
        // 没配头像时也不要露出「头像」占位白框
        if (mercIcon == null && mercSlot1.portraitPlaceholder != null)
            mercSlot1.portraitPlaceholder.SetActive(false);
        float maxHp = m.attr.GetAttr(AttrType.MaxHp);
        string tutName = !string.IsNullOrEmpty(m.DisplayName) ? m.DisplayName : StoryProgress.TutorialMercNickname;
        mercSlot1.UpdateSlot(tutName, m.mercLevel, m.currentHp, maxHp);
    }

    /// <summary>
    /// 引导局「未招募」佣兵槽的预览（2026-10-05 主人拍板）：
    /// 灰掉 + 佣兵头像 + 血条 + 稀有度头像框，不挂锁图标。
    /// 人从哪来 = 真源链 <c>StoryProgress.TutorialMercHireId</c>（H011 小白）→ <c>MercRosterDefs</c> 花名册，
    /// 不写死头像 / 血量 / 稀有度，花名册一改这里跟着变。
    /// </summary>
    public void ApplyTutorialMercPreview(CharacterSlotUI slot)
    {
        if (slot == null) return;
        string hireId = StoryProgress.TutorialMercHireId;
        MercRosterDefs.Def def;
        bool hasDef = MercRosterDefs.TryGetByHireId(hireId, out def);
        float maxHp = hasDef ? def.BaseHp : 0f;
        slot.ShowLockedPreview(
            MercPortraitSprites.GetHead(hireId),
            MercHireSession.LoadPortraitFrame(hasDef ? def.Rarity : MercRosterDefs.MercRarity.Common),
            hasDef && !string.IsNullOrEmpty(def.Nickname) ? def.Nickname : StoryProgress.TutorialMercNickname,
            maxHp, maxHp);
    }

    void RefreshTutorialMercLiveBar()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || mercSlot1 == null) return;
        var mercs = mm.GetActiveMercs();
        if (mercs == null || mercs.Count == 0 || mercs[0] == null) return;
        var m = mercs[0];
        float maxHp = m.attr.GetAttr(AttrType.MaxHp);
        string tutName = !string.IsNullOrEmpty(m.DisplayName) ? m.DisplayName : StoryProgress.TutorialMercNickname;
        mercSlot1.UpdateSlot(tutName, m.mercLevel, m.currentHp, maxHp);
        mercSlot1.SetEnergy(BattleManager.Instance != null ? BattleManager.Instance.GetMercMp(0) : 0f);
    }

    /// <summary>
    /// 设置单个佣兵槽位显示
    /// </summary>
    /// <summary>佣兵槽锁定文案：新流程下招募即解锁。</summary>
    const string MercLockedHint = "三选一解锁";

    /// <summary>佣兵槽解锁数：优先本局已招募数（三选一解锁），未开局时回退酒馆等级。</summary>
    static int ResolveMercSlotCount()
    {
        if (RunLoadout.IsActive)
            return Mathf.Clamp(RunLoadout.Mercs() != null ? RunLoadout.Mercs().Count : 0, 0, 2);
        return MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
    }

    /// <summary>佣兵稀有度：优先按 H 编号查花名册，查不到再按战斗 AssetId 查。</summary>
    static MercRosterDefs.MercRarity ResolveMercRarity(string assetId, string hireId)
    {
        MercRosterDefs.Def def;
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out def))
            return def.Rarity;
        if (!string.IsNullOrEmpty(assetId) && MercRosterDefs.TryGetByAssetId(assetId, out def))
            return def.Rarity;
        return MercRosterDefs.MercRarity.Common;
    }

    void SetupMercSlot(CharacterSlotUI slot, int index,
        List<string> mercIds, List<string> mercHireIds, List<Mercenary> activeMercs, int maxSlots, MercenaryManager mm)
    {
        if (slot == null) return;

        // 新流程：佣兵靠「三选一」招募解锁，没招募到的槽给明确指引
        bool unlocked = index < maxSlots;
        if (!unlocked)
        {
            slot.SetJobIcon(null);
            slot.ShowUnavailable(MercLockedHint);
            return;
        }
        if (index < mercIds.Count)
        {
            slot.SetLocked(false);
            bool hasActive = index < activeMercs.Count
                && activeMercs[index] != null
                && activeMercs[index].SkillCaster != null
                && activeMercs[index].SkillCaster.HasActiveSkill;
            // 2026-10-06：蓝条只给「有蓝」的技能显示（治疗/法术型）；无蓝技能纯 CD，不显示。
            // 旧口径是「手动模式才显示」，而自动模式恒为 true → 蓝条永远是关的，这条判定已作废。
            bool hasMpBar = hasActive && activeMercs[index].SkillCaster.MpType != MpArchetype.None;
            slot.SetEnergyEnabled(hasMpBar);
            string id = mercIds[index];
            string hireId = index < mercHireIds.Count ? mercHireIds[index] : null;
            if (index < activeMercs.Count && activeMercs[index] != null && !string.IsNullOrEmpty(activeMercs[index].hireId))
                hireId = activeMercs[index].hireId;
            Sprite icon = MercPortraitSprites.GetHead(hireId) ?? MercPortraitSprites.GetHead(id) ?? (mm != null ? mm.GetIcon(id) : null);
            string job = mm != null ? mm.GetJobName(id) : id;
            slot.SetPortrait(icon);
            // 头像框按本佣兵稀有度换（普通灰白 / 稀有蓝 / 传奇橙金）
            slot.SetFrame(MercHireSession.LoadPortraitFrame(ResolveMercRarity(id, hireId)));
            // 职业 icon：按佣兵职业名取四分类（防御/恢复/法术/物攻），唯一入口 JobIconResolver.CombatBadge
            slot.SetJobIcon(JobIconResolver.CombatBadge(job));
            // 右上角小图标=该佣兵的技能：2026-10-06 主人拍板 → 佣兵技能角标全部隐藏（技能自动释放，不用手点）
            slot.SetSkillBadge(null);

            if (index < activeMercs.Count && activeMercs[index] != null)
            {
                var m = activeMercs[index];
                float maxHp = m.attr.GetAttr(AttrType.MaxHp);
                slot.UpdateSlot(job, m.mercLevel, m.currentHp, maxHp);
            }
            else
            {
                // 出战名单有占位但单位未生成：也不涨蓝条
                slot.SetEnergyEnabled(false);
                slot.UpdateSlot(job, 1, 0, 0);
            }
        }
        else
        {
            // 已解锁但无佣兵：空槽占位
            slot.SetJobIcon(null);
            slot.ShowEmpty();
        }
    }

    /// <summary>角色栏：禁止 ForceExpand 把槽拉扁；不强制 childControl（否则无 LayoutElement 会被压成 0）</summary>
    void FixCharacterBarLayout()
    {
        Transform bar = FindDeepChildIgnoreCase(transform, "CharacterBar");
        if (bar == null) return;
        var hlg = bar.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            // 保留美术预制体的 childControl*，不要改成 true
        }

        // 只给已有 LayoutElement 的槽关 flex，未解锁槽不 AddComponent
        SoftFixLayoutElement(playerSlot?.root);
        SoftFixLayoutElement(mercSlot1?.root);
        SoftFixLayoutElement(mercSlot2?.root);
    }

    /// <summary>
    /// 头像栏保留美术摆的位置，<b>只在父级真有可见边框时才夹</b>。
    ///
    /// <para>⚠ 2026-10-05 主人拍板（根因修复）：父级 <c>zhuangshi</c> 是<b>纯 RectTransform、零组件</b>
    /// 的锚点容器，它的 100×100 sizeDelta 只是 Unity 新建节点的默认值，<b>不是框、不是边界</b>。
    /// 老版本拿这个 100×100 当硬边界，把 CharacterBar（y=165）一路推到 y=0，
    /// 每次进战斗推一次 —— 主人报的「老是调整 zhuangshi 里面的位置」就是这里干的，
    /// 跟 BattleEntryDraftPanel 一点关系都没有。</para>
    ///
    /// <para>现在的判据：父级<b>没有 Image/Graphic 之类的可见框</b>时一律不夹
    /// （美术摆哪就哪，类只做兜底、不做重排）；只有父级确实画了一块可见面板（宽高有效）
    /// 才把越界的部分推回来。</para>
    /// </summary>
    public void ClampCharacterBarInsideParent()
    {
        Transform t = FindDeepChildIgnoreCase(transform, "CharacterBar");
        var bar = t as RectTransform;
        if (bar == null) return;
        var parent = bar.parent as RectTransform;
        if (parent == null) return;

        // 父级没有可见 Graphic（zhuangshi 就是这种纯锚点容器）→ 它没有"框"，无从谈越界。
        // 绝不按它默认的 100×100 去夹子节点，否则每次进战斗都会把美术摆好的栏推走。
        if (parent.GetComponent<Graphic>() == null)
            return;

        // 布局这一帧可能还没算完，先强制刷新再量
        LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

        Rect pr = parent.rect;
        // Canvas 还没定尺寸时不要动，否则会把栏推到错的地方
        if (pr.width <= 1f || pr.height <= 1f) return;

        float halfH = bar.rect.height * 0.5f;
        float halfW = bar.rect.width * 0.5f;
        if (halfH <= 0.01f || halfW <= 0.01f) return;

        // bar 在 parent 局部空间里的中心
        Vector3 center = parent.InverseTransformPoint(bar.TransformPoint(bar.rect.center));
        const float margin = 6f;

        float minY = pr.yMin + halfH + margin;
        float maxY = pr.yMax - halfH - margin;
        float minX = pr.xMin + halfW + margin;
        float maxX = pr.xMax - halfW - margin;

        float wantY = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : (pr.yMin + pr.yMax) * 0.5f;
        float wantX = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : (pr.xMin + pr.xMax) * 0.5f;

        float dx = wantX - center.x;
        float dy = wantY - center.y;
        if (Mathf.Abs(dx) < 0.5f && Mathf.Abs(dy) < 0.5f) return;

        bar.anchoredPosition += new Vector2(dx, dy);
        Debug.Log($"[BattleUI] CharacterBar 越界已推回 dx={dx:F1} dy={dy:F1} " +
                  $"size={bar.rect.width:F0}x{bar.rect.height:F0} parent={pr.width:F0}x{pr.height:F0}");
    }

    static void SoftFixLayoutElement(GameObject root)
    {
        if (root == null) return;
        var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le == null) return; // 不新增组件
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        le.ignoreLayout = false;
    }
}
