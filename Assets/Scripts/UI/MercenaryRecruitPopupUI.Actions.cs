using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 交互与结算：按钮绑定、雇佣按钮、刷新按钮、招募成功处理、概率说明弹窗。
/// 【2026-10-23 主人拍板】原 MercenaryRecruitPopupUI.cs 已 1461 行（项目约定 >600 行必须拆），
/// 按职责切成 5 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercenaryRecruitPopupUI : MonoBehaviour
{

    void WireOnce()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefresh);
        if (infoButton != null) infoButton.onClick.AddListener(ShowRecruitOdds);   // 2026-10-17：info 改弹概率公示（口径与实际随机一致）；旧规则面板 ShowInfo 保留未删

        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            var c = cards[i];
            if (c == null || c.root == null) continue;
            // 【2026-10-19 主人拍板】卡上按钮不隐藏，点击必给反馈：
            // 金币钮/卷钮 → 买得起弹「佣兵信息」确认弹窗（购买/取消），不够弹字提示；
            // 点卡其余区域 → 直接看「佣兵信息」弹窗（购买在弹窗里走同一确认口径）。
            if (c.goldButton != null)
            {
                c.goldButton.onClick.RemoveAllListeners();
                c.goldButton.onClick.AddListener(() => OnBuyButtonClicked(idx));
            }
            if (c.scrollButton != null)
            {
                c.scrollButton.onClick.RemoveAllListeners();
                c.scrollButton.onClick.AddListener(() => OnBuyButtonClicked(idx));
            }
            var rootBtn = c.root.GetComponent<Button>();
            if (rootBtn == null) rootBtn = c.root.AddComponent<Button>();
            rootBtn.onClick.RemoveAllListeners();
            rootBtn.onClick.AddListener(() => OnCardClicked(idx));
        }
    }

    /// <summary>
    /// 卡上购买钮点击（2026-10-19 主人拍板）：先按货币口径查够不够 ——
    /// 普通=金币（不够弹「金币不足」）；稀有=普通卷、传奇=稀有卷（不够弹「道具不足」）；
    /// 够才弹「佣兵信息」确认弹窗（是否购买）。购买 = 永久解锁进「已拥有」；
    /// 已拥有的候选不可重复购买（点击只提示）。
    /// </summary>
    void OnBuyButtonClicked(int index)
    {
        if (index < 0 || index >= _offers.Count || _offers[index] == null)
        {
            UIManager.Instance?.ShowToast("无效的佣兵");
            return;
        }
        var offer = _offers[index];
        if (MercHireSession.IsMercOwned(offer))
        {
            UIManager.Instance?.ShowToast("该佣兵已拥有");
            return;
        }
        var rarity = MercHireSession.OfferRarity(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        if (isCommon)
        {
            if (!HasEnoughGold(offer))
            {
                UIManager.Instance?.ShowToast("金币不足");
                return;
            }
        }
        else if (!MercHireSession.HasScrollFor(rarity))
        {
            UIManager.Instance?.ShowToast("道具不足");
            return;
        }

        // 买得起 → 弹确认（佣兵信息 + 购买/取消，购买钮带货币价格）
        MercObtainPopupUI.ShowForRecruit(offer, () => TryHireOffer(offer, index));
    }

    static bool HasEnoughGold(MercenaryData offer)
    {
        return ResourceWallet.Get(SaveSystem.Instance?.Data, ResourceWallet.ResourceType.Gold)
            >= MercHireSession.GoldCost(offer);
    }

    /// <summary>点招募卡：弹佣兵信息弹窗（MercObtainPopupUI），弹窗内「购买」回调 TryHireOffer。</summary>
    void OnCardClicked(int index)
    {
        if (index < 0 || index >= _offers.Count || _offers[index] == null) return;
        var offer = _offers[index];
        MercObtainPopupUI.ShowForRecruit(offer, () => TryHireOffer(offer, index));
    }

    void OnRefresh()
    {
        // 【2026-10-23 主人拍板】免费冷却好了 → 原免费刷新；冷却中 → 走钻石刷新（每天 2 次，50/100 钻）。
        if (MercHireSession.CanManualRefresh(out int remain))
        {
            RerollOffers();
            MercHireSession.MarkRefreshed();
            RefreshAll();
            if (!MaybeToastAppearBanter(force: false, avoidLast: true))
                UIManager.Instance?.ShowToast("已刷新候选佣兵");
            ScheduleNextIdleBanter();
            return;
        }

        if (!MercHireSession.CanDiamondRefresh(out _, out int cost))
        {
            // 【2026-10-24 主人拍板】钻石/广告次数用光：按钮保持可点，点击弹这句
            UIManager.Instance?.ShowToast("已无刷新次数 请明天再试", force: true);
            return;
        }

        // 【2026-10-24 主人拍板】所有花费钻石的地方都要二次确认：
        // 弹确认门（含余额/花费行），点确认才真正扣钻刷新；不足时门内直接 Toast 缺口不弹窗。
        DiamondSpendGate.Request("刷新候选佣兵", "重新随机候选佣兵（钻石刷新，每日 2 次）。", cost, () =>
        {
            if (!MercHireSession.TryDiamondRefresh())
                return;   // 钻不足时 TrySpendDiamond 已弹「钻石不足」，这里不再重复提示

            RerollOffers();
            RefreshAll();
            if (!MaybeToastAppearBanter(force: false, avoidLast: true))
                UIManager.Instance?.ShowToast($"已花 {cost} 钻石刷新候选佣兵");
            ScheduleNextIdleBanter();
        });
    }

    /// <summary>
    /// 购买一名招募候选（2026-10-19 起由「佣兵信息」弹窗的购买钮触发，返回是否成交）。
    /// 【2026-10-19 主人拍板】购买 = 永久解锁进「已拥有」——与雇佣队列、战斗内抽奖队列、
    /// 临时出战名单都无关，不再走 MercHireSession.AddHired。
    /// 支付口径：普通=金币；稀有=普通卷；传奇=稀有卷（高级卷）。
    /// 失败时 Toast 提示并返回 false（弹窗不关，玩家可改点取消）。
    /// </summary>
    bool TryHireOffer(MercenaryData offer, int index)
    {
        if (offer == null)
        {
            UIManager.Instance?.ShowToast("无效的佣兵");
            return false;
        }
        if (MercHireSession.IsMercOwned(offer))
        {
            UIManager.Instance?.ShowToast("该佣兵已拥有");
            return false;
        }

        var rarity = MercHireSession.OfferRarity(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        if (isCommon)
        {
            int gold = MercHireSession.GoldCost(offer);
            if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, gold, save: false, notify: true))
            {
                UIManager.Instance?.ShowToast("金币不足");
                return false;
            }
            // 【2026-10-24 主人拍板「商店未开启」】金币消费累计喂食点之一（S005/S010）：
            // 商城按钮隐藏期间，酒馆招募是唯一活跃的金币出口，口径并入消费累计。
            // 出口唯一 = OnShopGoldSpent（商店 ShopSystem.Buy 是另一喂食点）。
            AdventureLogAchievements.OnShopGoldSpent(gold);
        }
        else if (!MercHireSession.TrySpendScroll(rarity))
        {
            UIManager.Instance?.ShowToast("道具不足");
            return false;
        }

        // 永久解锁（进「已拥有」，下次开酒馆自动按既有口径补进三选一池资格）
        var data = SaveSystem.Instance?.Data;
        string hid = offer.hireId;
        if (string.IsNullOrEmpty(hid) && MercRosterDefs.TryGetByAssetId(offer.mercId, out var def0))
            hid = def0.HireId;
        if (data == null || string.IsNullOrEmpty(hid) || !data.UnlockMerc(hid))
        {
            Debug.LogError("[MercRecruit] 解锁佣兵失败：hireId=" + hid
                + "（data=" + (data != null) + "，钱已扣，需主人确认是否补发）");
            UIManager.Instance?.ShowToast("购买失败");
            return false;
        }
        AdventureCodex.MarkMercSeen(hid);
        SaveSystem.Instance.Save();

        // 【2026-10-20 主人拍板】酒馆三选一购买完成 = 已拥有 → 弹「获得佣兵」全屏演出（同战斗三选一口径）。
        // 酒馆能买成功的必是首次获得（已拥有灰显不可买）；演出层 11500 盖过 Toast，点任意处关闭回到招募界面。
        MercGainCinematicUI.Show(hid, null);

        string name = !string.IsNullOrEmpty(offer.displayName) ? offer.displayName
            : (!string.IsNullOrEmpty(offer.nickname) ? offer.nickname
            : (MercRosterDefs.TryGetByHireId(hid, out var def1) ? def1.Name : "佣兵"));
        // 【2026-10-19 主人拍板】购买报喜文案 = 恭喜获得XXX实力大增；佣兵台词仍走该卡上方气泡框。
        // force:true —— 购买结果不允许被闲聊气泡压制（主人报「购买后没有提示获得信息」的根因）
        UIManager.Instance?.ShowToast($"恭喜获得{name}实力大增", force: true);
        ShowMercBubble(index, MercLineTable.Pick(OfferBanterKey(offer), MercLineTable.Scene.HireSuccess));
        RefreshAll();
        ScheduleNextIdleBanter();
        return true;
    }

    /// <summary>
    /// info 按钮弹出招募概率公示（2026-10-17 主人拍板：info 节点 → 概率弹窗）。
    /// 口径与 MercenaryOfferGenerator 实际掷点完全一致（常量直引，不会公示假数）：
    /// 佣兵 100%；品质 普通 133/200 = 66.5% / 稀有 66/200 = 33% / 传奇 1/200 = 0.5%（2026-10-19 主人拍板）。
    /// 卡面美术随佣兵稀有度实时切换（Card0/1/2 三套稀有度卡牌）。
    /// </summary>
    void ShowRecruitOdds()
    {
        var rows = new List<DraftPool.OddsRow>
        {
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_TYPE,
                Label = "佣兵",
                Percent = 100f,
                Color = RarityPalette.Common,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "传奇",
                Percent = MercenaryOfferGenerator.LegendaryWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Legendary,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "稀有",
                Percent = MercenaryOfferGenerator.RareWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Rare,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "普通",
                Percent = MercenaryOfferGenerator.CommonWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Common,
            },
        };
        DraftOddsPopupUI.ToggleWith(rows);
    }

    // 【2026-10-23 主人拍板】ShowInfo 已删除：2026-10-17 info 钮改弹概率公示（ShowRecruitOdds）后此面板零调用。

}
