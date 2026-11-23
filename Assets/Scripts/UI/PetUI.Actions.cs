using System;
using UnityEngine;

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
                if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, def.unlockValue, false, true)) return;
                save.UnlockPet(def.id);
                SaveSystem.Instance.Save();
                GlobalToastUI.Show("解锁 " + def.name + "！");
                RefreshAll();
                break;

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

        // 2026-11-22：喂养随机给攻/血/防其中一项 +1~3，落盘（皮肤与本体共享 bodyId）。
        string attrName;
        int hpAdd = 0, atkAdd = 0, defAdd = 0;
        int roll = UnityEngine.Random.Range(0, 3);
        int amount = UnityEngine.Random.Range(1, 4);
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
        GlobalToastUI.Show("喂养 " + def.name + " -" + cost + " 金币，好感 +1，" + attrName + " +" + amount);
        RefreshAll();
        PetFeatureGuide.NotifyFed(def.id);
    }

    /// <summary>喂养定价按亲密度星级分档：1→2星 300、2→3星 400、满星后 500（规格 16:2x）。</summary>
    static int FeedCost(int fedCount)
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

        DiamondSpendGate.Request("购买皮肤",
            "购买「" + skin.name + "」皮肤（仅换外观 / 形态，战力不变）", skin.unlockValue,
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
