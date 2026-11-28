using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物界面交互：解锁 / 上阵 / 喂养 / 皮肤购买切换。
/// 钻石扣费走 DiamondSpendGate 唯一出口（含二次确认）；金币走 ResourceWallet.TrySpend。
/// 所有操作后统一 SaveSystem.Save() + RefreshAll()。
/// </summary>
public partial class PetUI
{
    // 本次喂养增量（仅一次刷新可见，RefreshDetail 消费后清空）。皮肤与本体共享 bodyId。
    internal static string _feedGainBodyId;
    internal static string _feedGainAttr;
    internal static int _feedGainAmount;

    void OnDeployClicked()
    {
        var def = SelectedDef;
        var save = SaveSystem.Instance?.Data;
        if (def == null || save == null) return;

        bool unlocked = save.IsPetUnlocked(def.id);
        if (!unlocked)
        {
            TryUnlock(def, save);
            return;
        }

        if (save.petDeployedId == def.id)
        {
            TryFeed(def, save);
            return;
        }

        // 上阵
        save.petDeployedId = def.id;
        SaveSystem.Instance.Save();
        GlobalToastUI.Show(def.name + " 已上阵");
        RefreshAll();
        PetFeatureGuide.NotifyDeployed(def.id);
    }

    /// <summary>解锁条件文案唯一出口：点未解锁图标、点解锁按钮共用（2026-11-21）。</summary>
    public static string UnlockHint(PetDefs.Def def, SaveData save)
    {
        if (def == null) return "";
        switch (def.unlock)
        {
            case PetDefs.UnlockType.Default: return "这只已默认解锁";
            case PetDefs.UnlockType.Gold: return "解锁需 " + def.unlockValue + " 金币";
            case PetDefs.UnlockType.Chapter: return "通关第 " + def.unlockValue + " 章后解锁";
            case PetDefs.UnlockType.Diamond: return "解锁需 " + def.unlockValue + " 钻石";
            case PetDefs.UnlockType.AdWatch:
                int watched = save != null ? save.petAdWatchCount : 0;
                return "累计看广告 " + watched + "/" + def.unlockValue + " 次解锁";
            case PetDefs.UnlockType.SkinDiamond: return "购买皮肤需 " + def.unlockValue + " 钻石";
            default: return "暂未开放";
        }
    }

    /// <summary>未解锁宠物的条件提示（点列表图标 / 点解锁按钮都调这里）。</summary>
    static void ShowUnlockHint(PetDefs.Def def, SaveData save)
    {
        if (def == null || save == null || save.IsPetUnlocked(def.id)) return;
        GlobalToastUI.Show(UnlockHint(def, save));
    }

    void TryUnlock(PetDefs.Def def, SaveData save)
    {
        // 点「解锁」按钮先弹一次条件（2026-11-21：点图标已弹一次，点解锁再弹一次）
        GlobalToastUI.Show(UnlockHint(def, save));
        switch (def.unlock)
        {
            case PetDefs.UnlockType.Gold:
                {
                    // 2026-11-25 主人「解锁和皮肤都弹」：金币解锁也走通用确认框（金币不是钻石，不能走 DiamondSpendGate）。
                    long haveGold = ResourceWallet.Get(save, ResourceWallet.ResourceType.Gold);
                    if (haveGold < def.unlockValue)
                    {
                        GlobalToastUI.Show($"金币不足（持有 {haveGold} / 需要 {def.unlockValue}）", true);
                        return;
                    }
                    string goldBody = "解锁「" + def.name + "」\n\n花费金币 ×" + def.unlockValue
                        + $"\n（持有 {haveGold} → 剩余 {haveGold - def.unlockValue}）";
                    StaminaConfirmPopup.Show("解锁宠物", goldBody, "解锁", () =>
                    {
                        var d = SaveSystem.Instance?.Data;
                        if (d == null) return;
                        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, def.unlockValue, false, true)) return;
                        d.UnlockPet(def.id);
                        SaveSystem.Instance.Save();
                        GlobalToastUI.Show("解锁 " + def.name + "！");
                        RefreshAll();
                    });
                    break;
                }

            case PetDefs.UnlockType.Chapter:
                if (!save.IsChapterUnlocked(def.unlockValue))
                {
                    GlobalToastUI.Show("通关第 " + def.unlockValue + " 章后解锁");
                    return;
                }
                save.UnlockPet(def.id);
                SaveSystem.Instance.Save();
                GlobalToastUI.Show("解锁 " + def.name + "！");
                RefreshAll();
                break;

            case PetDefs.UnlockType.Diamond:
                DiamondSpendGate.Request("解锁宠物",
                    "解锁「" + def.name + "」（品质 " + def.qualityStar + " 星）", def.unlockValue,
                    () =>
                    {
                        var d = SaveSystem.Instance?.Data;
                        if (d == null) return;
                        if (!ResourceWallet.TrySpendDiamond(def.unlockValue, "解锁宠物 " + def.name, false, false)) return;
                        d.UnlockPet(def.id);
                        SaveSystem.Instance.Save();
                        GlobalToastUI.Show("解锁 " + def.name + "！");
                        RefreshAll();
                    }, "解锁");
                break;

            case PetDefs.UnlockType.AdWatch:
                if (save.petAdWatchCount < def.unlockValue)
                {
                    GlobalToastUI.Show("累计看广告 " + save.petAdWatchCount + "/" + def.unlockValue + " 次解锁");
                    return;
                }
                save.UnlockPet(def.id);
                SaveSystem.Instance.Save();
                GlobalToastUI.Show("解锁 " + def.name + "！");
                RefreshAll();
                break;

            case PetDefs.UnlockType.Default:
                save.UnlockPet(def.id);
                SaveSystem.Instance.Save();
                RefreshAll();
                break;

            default:
                Debug.LogWarning(TAG + "未处理的解锁类型 " + def.unlock + "（" + def.id + "）");
                break;
        }
    }

    void TryFeed(PetDefs.Def def, SaveData save)
    {
        // 只有上阵中的宠物能喂（规格第五节）
        if (save.petDeployedId != def.id) return;
        string bodyId = PetDefs.IsSkin(def) ? def.skinOf : def.id;
        int fed = save.GetPetFeedCount(bodyId);
        int cost = FeedCost(fed);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, false, true)) return;
        save.SetPetFeedCount(bodyId, fed + 1);
        save.CountPetFeed();   // 【2026-11-25】日常「喂养宠物」计数（每日清零；日常未开启时方法内部早退）

        // 2026-11-22：喂养随机给攻/血/防其中一项，落盘（皮肤与本体共享 bodyId）。
        // 2026-11-26 主人「喂养涨的属性再+50%」：原 1~3 → 2/3/5（向上取整）。
        string attrName;
        int hpAdd = 0, atkAdd = 0, defAdd = 0;
        int roll = UnityEngine.Random.Range(0, 3);
        int amount = Mathf.CeilToInt(UnityEngine.Random.Range(1, 4) * 1.5f);
        if (roll == 0) { hpAdd = amount; attrName = "血量"; }
        else if (roll == 1) { atkAdd = amount; attrName = "攻击"; }
        else { defAdd = amount; attrName = "防御"; }
        save.AddPetAttrBonus(bodyId, hpAdd, atkAdd, defAdd);

        SaveSystem.Instance.Save();
        // 本次喂养的属性增量交给详情页贴在属性数字右侧（主人 11-23：「这样玩家才知道涨了多少属性」）；
        // 只显示这一次刷新，RefreshDetail 消费后即清，不影响后续任何刷新。
        _feedGainBodyId = bodyId;
        _feedGainAttr = attrName;
        _feedGainAmount = amount;

        // 【2026-11-26 主人拍板】表情统一：只播 prefab 里唯一一组 Love 特效，喂一次播一次；
        // 引导那一拍的「叫两声」由 PetFeatureGuide 接管，这里只叫一声，不叠第二套气泡。
        // 原来代码生成的飘心已去掉（Love 自带 BigHeart / TinyHearts，不再叠两套视觉）。
        // 喂养成功后 biaoqing 整组播一次 Love（喂一次播一次，不再随机）
        PlayFeedLoveFx();
        if (!PetFeatureGuide.IsFeedGuideActive)
        {
            var anchor = _petArt as RectTransform;
            var cries = PetBattleBanter.GetTwoCries(bodyId);
            if (anchor != null && cries.Length > 0)
            {
                var talk = BattleHeadTalkUI.Ensure();
                talk.SetSortOrderBoostOnce(GameConfig.UiSort.TownPopup + 40);
                talk.PlayLineAt(anchor, cries[0], 2.2f, -50f);   // 【2026-11-28】宠物气泡往下 50（独立偏移，不影响战斗/抽卡气泡）
            }
        }
        GlobalToastUI.Show("喂养 " + def.name + " -" + cost + " 金币，好感 +1，" + attrName + " +" + amount);
        RefreshAll();
        PetFeatureGuide.NotifyFed(def.id);
    }

    // ── 入口可操作判定（2026-11-25 主人「宠物图标如果能喂养或者解锁要加红点」）──
    /// <summary>
    /// 宠物入口红点 / 箭头气泡的统一口径：宠物功能已解锁（1-3 通关）且
    /// 「有本体现在就能解锁」或「上阵宠物喂得起一次」。只读判定，不改任何存档。
    /// </summary>
    internal static bool HasActionable(SaveData save)
    {
        if (save == null) return false;
        if (!save.HasStageCleared(1, 2)) return false;   // 宠物功能未开：入口本身隐藏，红点无意义
        return HasUnlockable(save) || HasFeedable(save);
    }

    /// <summary>
    /// 任一未解锁本体现在就能解锁：Default（点一下就开）/ 金币够 / 章节已通 / 钻石够 / 广告次数够。
    /// 皮肤条目不计（皮肤走右上角皮肤槽，不属于「解锁宠物」提醒）。
    /// </summary>
    internal static bool HasUnlockable(SaveData save)
    {
        if (save == null) return false;
        var bodies = PetDefs.Bodies;
        for (int i = 0; i < bodies.Count; i++)
        {
            var def = bodies[i];
            if (def == null || save.IsPetUnlocked(def.id)) continue;
            switch (def.unlock)
            {
                case PetDefs.UnlockType.Default:
                    return true;
                case PetDefs.UnlockType.Gold:
                    if (ResourceWallet.Get(save, ResourceWallet.ResourceType.Gold) >= def.unlockValue) return true;
                    break;
                case PetDefs.UnlockType.Chapter:
                    if (save.IsChapterUnlocked(def.unlockValue)) return true;
                    break;
                case PetDefs.UnlockType.Diamond:
                    if (ResourceWallet.Get(save, ResourceWallet.ResourceType.Diamond) >= def.unlockValue) return true;
                    break;
                case PetDefs.UnlockType.AdWatch:
                    if (save.petAdWatchCount >= def.unlockValue) return true;
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// 上阵宠物（皮肤归一到本体）喂得起一次：金币 ≥ 当前亲密度档位的喂养价。
    /// 判定与 TryFeed 同源（同一个 FeedCost），不另抄价格。
    /// </summary>
    internal static bool HasFeedable(SaveData save)
    {
        if (save == null) return false;
        var def = PetDefs.Get(save.petDeployedId);
        if (def == null) return false;
        string bodyId = PetDefs.IsSkin(def) ? def.skinOf : def.id;
        if (!save.IsPetUnlocked(bodyId)) return false;
        long gold = ResourceWallet.Get(save, ResourceWallet.ResourceType.Gold);
        return gold >= FeedCost(save.GetPetFeedCount(bodyId));
    }

    /// <summary>宠物入口红点刷新唯一出口（进角色页 / 解锁 / 喂养 / 上阵 / RedDot.RefreshCommon 都调这里）。</summary>
    internal static void RefreshEntryRedDot()
    {
        RedDot.Set(RedDot.Pet, HasActionable(SaveSystem.Instance?.Data));
    }

    /// <summary>
    /// 【2026-11-28】宠物界面内部红点统一刷新：喂养按钮 / 皮肤槽 / 列表逐只可解锁。
    /// 由 RefreshAll() 调用（界面打开 / 解锁 / 喂养 / 上阵 / 换肤后），此时 SelectedDef 已就绪。
    /// 判定全部复用本文件已有的 HasFeedable 口径 + CanUnlockNow（与 HasUnlockable 同源），不另抄第二份。
    /// </summary>
    void RefreshInnerRedDots()
    {
        var save = SaveSystem.Instance?.Data;
        var sel = SelectedDef;   // 列表选中的本体（实例属性）

        // 喂养按钮：只有选中项就是上阵宠物（按钮显示「喂养」）时才亮，且喂得起。
        bool feeding = sel != null && save != null && save.IsPetUnlocked(sel.id) && save.petDeployedId == sel.id;
        RedDot.Set(RedDot.PetFeed, feeding && HasFeedable(save));

        // 皮肤槽：当前选中本体已解锁 + 皮肤未拥有 + 钻石够买。
        bool skinBuyable = false;
        if (save != null && sel != null && save.IsPetUnlocked(sel.id))
        {
            var skin = PetDefs.SkinOf(sel.id);
            if (skin != null && !save.IsPetUnlocked(skin.id)
                && ResourceWallet.Get(save, ResourceWallet.ResourceType.Diamond) >= skin.unlockValue)
                skinBuyable = true;
        }
        RedDot.Set(RedDot.PetSkin, skinBuyable);

        // 列表逐只：每只单独一个键（pet.unlock.<bodyId>），判定口径与 HasUnlockable 一致。
        var bodies = PetDefs.Bodies;
        for (int i = 0; i < bodies.Count; i++)
        {
            var def = bodies[i];
            if (def == null) continue;
            RedDot.Set(RedDot.PetUnlockPrefix + def.id, CanUnlockNow(def, save));
        }
    }

    /// <summary>单只宠物当前能否解锁（HasUnlockable 的逐只版本，口径逐字一致）。皮肤条目不计。</summary>
    static bool CanUnlockNow(PetDefs.Def def, SaveData save)
    {
        if (def == null || save == null || save.IsPetUnlocked(def.id)) return false;
        switch (def.unlock)
        {
            case PetDefs.UnlockType.Default: return true;
            case PetDefs.UnlockType.Gold:
                return ResourceWallet.Get(save, ResourceWallet.ResourceType.Gold) >= def.unlockValue;
            case PetDefs.UnlockType.Chapter:
                return save.IsChapterUnlocked(def.unlockValue);
            case PetDefs.UnlockType.Diamond:
                return ResourceWallet.Get(save, ResourceWallet.ResourceType.Diamond) >= def.unlockValue;
            case PetDefs.UnlockType.AdWatch:
                return save.petAdWatchCount >= def.unlockValue;
            default: return false;
        }
    }

    /// <summary>喂养定价按亲密度星级分档：1→2星 300、2→3星 400、满星后 500（规格 16:2x）。</summary>
    internal static int FeedCost(int fedCount)
    {
        if (fedCount < PetDefs.FEED_STAR2) return 300;
        if (fedCount < PetDefs.FEED_STAR3) return 400;
        return 500;
    }

    Coroutine _skinShakeCo;

    /// <summary>钻石不足时皮肤图标左右快速晃一下（照 UiBannerPopAnim 抖动写法：临时位移、结束归位）。
    /// 只读当前 anchoredPosition 作基准，不写死坐标、不碰 prefab；重复点击会重启，不叠加漂移。</summary>
    void StartSkinSlotShake()
    {
        if (_skinSlot == null) return;
        if (_skinShakeCo != null) StopCoroutine(_skinShakeCo);
        _skinShakeCo = StartCoroutine(ShakeSkinSlot());
    }

    System.Collections.IEnumerator ShakeSkinSlot()
    {
        var rt = _skinSlot as RectTransform;
        if (rt == null) yield break;
        Vector2 basePos = rt.anchoredPosition;
        const float duration = 0.32f;   // 快速晃一下
        const float amplitude = 14f;    // 左右幅度（像素）
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float falloff = 1f - u;
            float dx = Mathf.Sin(u * Mathf.PI * 8f) * amplitude * falloff;
            rt.anchoredPosition = basePos + new Vector2(dx, 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;   // 归位到 prefab 原坐标，不留偏移
        _skinShakeCo = null;
    }

    void OnSkinClicked()
    {
        var def = SelectedDef;
        var save = SaveSystem.Instance?.Data;
        if (def == null || save == null) return;

        var bodyId = PetDefs.IsSkin(def) ? def.skinOf : def.id;
        // 2026-11-25：未解锁本体选中后皮肤槽可见，禁止先买皮肤（否则会买到「本体没解锁的皮肤」
        // 并直接写 petDeployedId 上阵）。只提示本体解锁条件，不扣费。
        if (!save.IsPetUnlocked(bodyId))
        {
            ShowUnlockHint(PetDefs.Get(bodyId), save);
            return;
        }
        var skin = PetDefs.SkinOf(bodyId);
        if (skin == null) { Debug.LogWarning(TAG + "该宠物无皮肤（" + def.id + "）"); return; }

        // 已拥有皮肤：直接切换上阵到皮肤（互换）；未拥有：钻石购买
        if (save.IsPetUnlocked(skin.id))
        {
            save.petDeployedId = save.petDeployedId == skin.id ? bodyId : skin.id;
            SaveSystem.Instance.Save();
            GlobalToastUI.Show("已切换为「" + (save.petDeployedId == skin.id ? skin.name : def.name) + "」");
            RefreshAll();
            return;
        }

        // 钻石不足：皮肤图标左右快速晃一下（纯视觉反馈；扣费/校验仍走 DiamondSpendGate 唯一出口，
        // 它本就会弹「钻石不足」Toast）。只读余额判定要不要晃，不动任何扣费逻辑。
        long haveDiamond = ResourceWallet.Get(save, ResourceWallet.ResourceType.Diamond);
        if (haveDiamond < skin.unlockValue) StartSkinSlotShake();

        // 2026-11-25 主人「只说购买皮肤XXX，下一行消耗钻石就行」：正文只给名字，
        // 「花费钻石 ×N（持有 X → 剩余 Y）」由 DiamondSpendGate 自动追加，不在这里重复。
        DiamondSpendGate.Request("购买皮肤", skin.name, skin.unlockValue,
            () =>
            {
                var d = SaveSystem.Instance?.Data;
                if (d == null) return;
                if (!ResourceWallet.TrySpendDiamond(skin.unlockValue, "购买宠物皮肤 " + skin.id, false, false)) return;
                d.UnlockPet(skin.id);
                d.petDeployedId = skin.id;   // 买后立刻换上（用户 18:35：确定后立即切换皮肤）
                SaveSystem.Instance.Save();
                GlobalToastUI.Show("已购买并换上「" + skin.name + "」");
                RefreshAll();
            }, "购买");
    }

}
