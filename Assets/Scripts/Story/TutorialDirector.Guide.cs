using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TutorialDirector.Guide.cs partial —— 2026-10-17 由 TutorialDirector.cs 纯位移拆出（无逻辑改动）
// 职责：回城天赋引导、引导宝箱/装备产出、三拍抽奖文案外的宝箱奖励

public partial class TutorialDirector
{
    /// <summary>
    /// P4：回城后引导点一次天赋（左栏属性节点，消耗金币）。
    /// 金币不够 / 界面缺失 / 玩家直接关窗，都放行——引导不能把人卡在天赋页。
    /// </summary>
    IEnumerator CoGuideTalentUpgrade()
    {
        var hint = TutorialHintUI.Ensure();

        // 2026-09-19：天赋引导已并入「首次启动强制软引导」（FirstRunGuide.Talent）：
        // 角色页 → 天赋按钮 → 节点，三步都带高亮圈 + 半透明遮罩 + 气泡，且每步落盘可续。
        // 只有引导那一步已经结束（含跳过）时，才退回原来这条一次性弱提示兜底。
        if (GuideCovers(FirstRunGuide.GuideStep.Talent))
        {
            Debug.Log("[Tutorial] 天赋引导交给 FirstRunGuide.Talent，跳过旧提示");
            yield break;
        }

        int before = TalentUI.LeftUnlockedCount();
        if (!TalentSystem.CanUnlockLeft(before + 1, out string reason))
        {
            Debug.Log($"[Tutorial] 跳过天赋引导：{reason}");
            hint.Show("金币攒够后，记得来天赋页把属性节点点满哦～", null, 5f);
            yield return new WaitForSecondsRealtime(1.5f);
            hint.Hide();
            yield break;
        }

        // 1) 不自动打开角色页：让玩家自己点 BottomNav 的「角色」（用户口径：都让玩家点）
        var nav = MainBottomNav.Instance;
        RectTransform charRt = nav != null && nav.characterButton != null
            ? nav.characterButton.GetComponent<RectTransform>() : null;

        float bind = 0f;
        const float navTimeout = 15f;
        while (bind < navTimeout)
        {
            var ch = CharacterUI.Instance;
            if (ch != null && ch.gameObject.activeInHierarchy) break;
            if (charRt != null)
                hint.ShowHard("先点下面的「角色」，再点「天赋」哟～", charRt);
            bind += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        var character = CharacterUI.Instance;
        if (character == null || !character.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[Tutorial] 玩家未进入角色页，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        // 2) 指向「天赋」按钮；玩家不点就不往下走，**绝不替玩家点开**
        RectTransform talentRt = character.talentButton != null
            ? character.talentButton.GetComponent<RectTransform>() : null;
        float t = 0f;
        const float openTimeout = 15f;
        while (t < openTimeout)
        {
            var talent = TalentUI.Instance;
            if (talent != null && talent.IsOpen) break;
            if (talentRt != null)
                hint.ShowHard("点「天赋」，用金币点亮一个属性节点吧～", talentRt);
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen)
        {
            Debug.LogWarning("[Tutorial] 玩家未点开天赋页，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        var talentUi = TalentUI.Instance;
        if (talentUi == null || !talentUi.IsOpen)
        {
            Debug.LogWarning("[Tutorial] 天赋页没打开，跳过天赋引导");
            hint.Hide();
            yield break;
        }

        // 3) 高亮下一个可点节点，等玩家点
        float t2 = 0f;
        const float pickTimeout = 25f;
        while (t2 < pickTimeout)
        {
            if (TalentUI.Instance == null || !TalentUI.Instance.IsOpen) break;
            if (TalentUI.LeftUnlockedCount() > before) break;

            var rt = TalentUI.Instance.GetLeftNode(before);
            if (rt != null)
                hint.ShowHard("戳这个节点！属性立刻就生效啦～", rt);

            t2 += Time.unscaledDeltaTime;
            yield return null;
        }

        hint.Hide();
        if (TalentUI.LeftUnlockedCount() > before)
            UIManager.Instance?.ShowToast("天赋点亮啦，属性已更新～");
    }

    // 【2026-10-06 已删除】CoRefreshMercHudNextFrame：只被「救援入队」那一段调用，
    //   入队戏删除后无调用点 → 整方法删除（HUD 现在靠 SlotMachineSystem.TutorialMercDrawn 推导，抽完即亮）。

    IEnumerator OfferTutorialEquip()
    {
        var equip = CreateTutorialEquipDrop();
        if (equip == null)
        {
            UIManager.Instance?.ShowToast("地上有件装备，快去捡呀～");
            yield break;
        }

        // 弹窗前不入包：等玩家点按钮再由 EquipDropPopupUI 入包
        bool closed = false;
        EquipDropPopupUI.ShowSingle(equip, (_, __) => closed = true);
        while (!closed) yield return null;

        BattleUI.Instance?.UpdateBackpackGrid();
    }

    // 【2026-10-06 已删除】TryGrantTutorialMercFragment（引导抽到佣兵发本命碎片）。
    //   主人当天拍板：第 2 抽 = **直接把引导佣兵招募入队**（SlotMachineSystem.BuildGuaranteedMercCard），
    //   碎片那条口径作废。方法 + 调用点整条删除，不留开关；
    //   「引导佣兵招没招」的判据只剩 SlotMachineSystem.TutorialMercDrawn 一处。
    //   【2026-10-08 补记】当时那位是 H011 路加（牧师）；2026-10-07 起引导佣兵为 H003 塔克。

    /// <summary>
    /// 引导宝箱的开箱产出（2026-10-05 主人拍板）：**天赋石**。
    /// 原来掉一把武器，但开局第一抽就会随机给一件装备（2026-10-06：装备没有保底，木制圆盾只是起步装备），
    /// 宝箱再掉装备就重复了；
    /// 天赋石是城镇点天赋的硬通货，正好把「开箱 = 长期成长」这一课补上。
    /// 数量就改这一个常量。
    /// </summary>
    const int TutorialChestTalentStones = 3;

    /// <summary>
    /// 开箱拿到东西后，留给玩家看清楚的停留秒数。
    /// 【2026-10-06 主人反馈「宝箱获得的东西太快了，没看到是啥就没了」】—— 要再长就调这一个值。
    /// </summary>
    const float ChestRewardReadSec = 1.6f;

    static void GrantTutorialChestTalentStones()
    {
        if (TutorialChestTalentStones <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, TutorialChestTalentStones,
                           save: true, notify: true);
        // 【2026-10-06 主人拍板：物品变动一律 force 弹出】普通 toast 会被引导遮罩/气泡吞掉，
        // 走 GlobalToastUI 的 force 通道，保证宝箱给的东西一定露脸。
        GlobalToastUI.Show($"获得天赋石 ×{TutorialChestTalentStones}", true);
        // 【2026-10-07 主人反馈「获得天赋石没有在顶条资源那显示」】
        // 顶栏只在 AutoGameInitializer 里刷过一次，中途拿到石头不会自己重画；
        // 这里拿到就刷一次，玩家能在顶条上看到数字从 0 变成 3。
        BattleUI.Instance?.UpdateTopBarResources();
    }

    static EquipInstance CreateTutorialEquipDrop()
    {
        // 宝箱只掉武器（forceWeapon 强制主手武器分支），避免误给防具。
        // 2026-10-12 主人拍板：引导宝箱不再「强制稀有」。
        // 旧写法 GenerateFromTemplate(tpl, 0, lv, true, Rarity.Rare) 把武器钉死在稀有档
        // （ATK 14~20 × 系数 ≈ 16~28，初始武器仅 4.5~6）→ 一抽就秒怪，体验崩。
        // 现统一走掉落路径（GetRandomEquipInstances，带 RIFT_DROP_ATTR_BONUS +10）：
        //   - 必为武器，且数值 > 初始武器（主人要求「初始武器最弱，随便一个都比它强」）；
        //   - 稀有度由 hidden_level_rules.csv 曲线决定：隐藏等级 1 = 100% 普通，
        //     随杀怪 / 通关慢慢提升稀有 / 传奇概率，前期普通为主。
        // 2026-10-12 主人拍板「每次引导战力一样」：宝箱这件也走确定性生成（fixedForTutorial: true）
        // —— 恒普通 / 必主手武器 / 属性取区间中值，与引导第一拍抽卡那件同口径。
        var list = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetRandomEquipInstances(1, 1, 0, StageType.Normal, GameConfig.RIFT_DROP_ATTR_BONUS, true, ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1, true)
            : null;
        if (list != null && list.Count > 0)
        {
            var eq = list[0];
            if (eq != null)
            {
                eq.requireLevel = 1;
                eq.template?.ResolveIcon();
                if (eq.icon == null && eq.template != null) eq.icon = eq.template.icon;
                if (WeaponLoadoutRules.IsLoadoutItem(eq))
                    AlignWeaponToHeroAttackHand(eq);
                if (string.IsNullOrEmpty(eq.equipName) || LooksLikeEnglishFileName(eq.equipName))
                    eq.equipName = WeaponAffixSystem.RarityPrefix(eq.rarity)
                                   + EquipNameGen.RandomWeaponName(eq.template, eq.slotType);
            }
            return eq;
        }
        return null;
    }

    /// <summary>教程武器固定逻辑主手；实际挂点仍由 HandRig 解析到普攻手。</summary>
    static void AlignWeaponToHeroAttackHand(EquipInstance eq)
    {
        if (eq == null || !WeaponLoadoutRules.IsLoadoutItem(eq)) return;
        eq.slotType = EquipSlotType.MainHand;
        eq.weaponHand = WeaponHandSlot.MainHand;
    }

    static bool LooksLikeEnglishFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c > 127) return false;
        }
        return name.IndexOf('_') >= 0 || name.StartsWith("equip", System.StringComparison.OrdinalIgnoreCase);
    }
}
