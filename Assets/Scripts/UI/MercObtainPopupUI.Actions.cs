using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 交互：确认/复活/购买/替换/升星按钮，弹窗开关与模式退出，招募模式的入口与按钮。
/// 【2026-10-23 主人拍板】原 MercObtainPopupUI.cs 已 929 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercObtainPopupUI : MonoBehaviour
{
    void OnKeepClicked()
    {
        if (_recruitMode) { OnBuyClicked(); return; }
        if (_fallenMode) { OnReviveClicked(); return; }
        Close();
    }

    /// <summary>阵亡复活模式点「复活」：回调扣护符 + 原地复活；成功才关弹窗，失败留在弹窗（Toast 已提示）。</summary>
    void OnReviveClicked()
    {
        var revive = _tryRevive;
        if (revive == null) { Close(); return; }
        if (revive())
        {
            _tryRevive = null;
            Close();
        }
    }

    /// <summary>招募模式点「购买」：回调招募界面扣款入队；成功才关弹窗，失败留在弹窗（Toast 已提示）。</summary>
    void OnBuyClicked()
    {
        var buy = _tryBuy;
        if (buy == null) { Close(); return; }
        if (buy())
        {
            _tryBuy = null;
            Close();
        }
    }

    // ============================================================
    // 右下「替换」按钮（2026-10-18 主人拍板）
    // ============================================================

    /// <summary>点「替换」：招募/阵亡复活模式下该钮是「取消」= 直接关；其余模式先关弹窗再回调
    /// （回调里 MercDrawPoolUI 会入队/进点选态并刷新列表）。</summary>
    void OnReplaceClicked()
    {
        if (_recruitMode || _fallenMode) { Close(); return; }
        var cb = _onReplace;
        _onReplace = null;
        Close();
        cb?.Invoke();
    }

    /// <summary>
    /// 显隐「替换」按钮（抽中路径/战斗头像入口/未获得详情一律隐藏）。
    /// 【2026-10-18 主人拍板】只留确认按钮时把它居中（预制体现摆 x=-122 是并排态、给右侧替换钮让位），
    /// 显示替换钮时还原原位；只调 x，y 不动。每次开关弹窗都经这里重设，不累积偏移。
    /// </summary>
    void ApplyReplaceButton(bool on)
    {
        if (_replaceBtn != null) _replaceBtn.gameObject.SetActive(on);
        if (_keepRt != null)
        {
            var pos = _keepBasePos;
            pos.x = on ? _keepBasePos.x : 0f;
            _keepRt.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】点「进阶」：开 MercAscendUI.Show(当前佣兵 hireId)。
    /// 只在已拥有时可见（Fill 里刷），理论点不到空 hireId，兜底 LogError 防静默。
    /// </summary>
    void OnAscendClicked()
    {
        if (string.IsNullOrEmpty(_curHireId))
        {
            Debug.LogError("[MercObtainPopup] 进阶钮点击但当前佣兵 hireId 为空（Fill 未正常填充？）");
            return;
        }
        MercAscendUI.Show(_curHireId);
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】「进阶」钮显隐：仅该佣兵已拥有（SaveData.IsMercUnlocked）才显示；
    /// 未拥有的预览态（酒馆招募候选卡）隐藏 —— 与主人「未拥有的佣兵不显示」口径一致。
    /// Fill 每次填充佣兵数据时调用（四个入口都经 Fill）。
    /// </summary>
    void ApplyAscendButton()
    {
        if (_ascendBtn == null) return;
        var data = SaveSystem.Instance?.Data;
        bool owned = data != null && data.IsMercUnlocked(_curHireId);
        _ascendBtn.gameObject.SetActive(owned);
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】获得类弹窗互斥：本弹窗开着时若技能弹窗要开，先把它关掉，别叠在一起。
    /// 两个获得弹窗同层 925、互不感知，玩家点佣兵头像看着详情时又抽中技能就会叠 —— 统一「后来的关先前的」。
    /// Close() 只发一次 onClosed（本入口 _onClosed 本就为 null），自动关协程见 _closeReq 自行退出，无残留。
    /// </summary>
    public static void CloseIfOpen()
    {
        if (_instance != null && _instance._root != null && _instance._root.activeSelf)
            _instance.Close();
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

    /// <summary>唯一关闭出口：点确认/购买/取消 / 点暗带 / 自动兜底都走这；回调只发一次。</summary>
    void Close()
    {
        _closeReq = true;
        if (_recruitMode) ExitRecruitMode();   // 还原按钮/标题文案（确认/替换/获得佣兵），下次普通入口不串味
        if (_fallenMode) ExitFallenMode();     // 同上：还原「复活/取消」双钮，下次普通入口不串味
        if (_root != null) _root.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    /// <summary>退出招募购买模式：清回调 + 还原预制体原文案与按钮布局。</summary>
    void ExitRecruitMode()
    {
        _recruitMode = false;
        _tryBuy = null;
        ApplyRecruitButtons(false, null);
    }

    /// <summary>退出阵亡复活模式：清回调 + 还原预制体原文案与按钮布局（与招募模式同一套还原）。</summary>
    void ExitFallenMode()
    {
        _fallenMode = false;
        _tryRevive = null;
        ApplyRecruitButtons(false, null);
        // 提示行收掉 + 复活钮颜色还原，普通详情/招募入口不残留
        if (_reviveCharmHint != null) _reviveCharmHint.SetActive(false);
        if (_keepLabel != null) _keepLabel.color = _keepLabelColor0;
    }

    // ============================================================
    // 阵亡复活模式 · 「护符 icon + 拥有数量」提示行（2026-10-21 主人需求）
    // 挂在复活按钮正上方：icon + ×N。运行时补建（预制体没有这个节点），只认 Panel/ReviveCharmHint。
    // ============================================================
    const string ReviveCharmHintNode = "ReviveCharmHint";
    const string ReviveCharmItemIdForHint = "item_revive_charm";


    public static void ShowForRecruit(MercenaryData offer, System.Func<bool> tryBuy)
    {
        var ui = Ensure();
        if (ui == null || offer == null) return;
        ui.ExitFallenMode();            // 防串味：先退阵亡复活模式（「复活/取消」双钮别串进招募购买）
        var card = new DraftCard
        {
            Kind = DraftCardKind.MercRecruit,
            Id = offer.mercId,
            HireId = offer.hireId,
            MercLevel = Mathf.Max(1, offer.level),
            // 预制体只摆了 3 个满星节点，超出按 3 显示（结构变更待主人批准，不静默加节点）
            Star = Mathf.Clamp(offer.star < 1 ? 1 : offer.star, 1, MaxStarNodes)
        };
        if (!ui.Fill(card)) { ui.Close(); return; }
        ui._recruitMode = true;
        ui._tryBuy = tryBuy;
        ui._onReplace = null;
        ui.ApplyRecruitFill(offer);
        // 【2026-10-18 主人拍板】获得类弹窗互斥：本弹窗要开了，先关掉开着的技能弹窗
        SkillObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        // 不启动自动关兜底：这是带购买按钮的交互弹窗，8 秒强关会打断购买
    }

    /// <summary>招募模式内容覆盖：标题/名字用招募卡口径（displayName），技能用 offer 的（主动优先、无主动上被动）。</summary>
    void ApplyRecruitFill(MercenaryData offer)
    {
        // 名字兜底链与 Fill 同口径：HireId 查不到走 AssetId（Fill 已保证两者必有一个能查到）
        if (!MercRosterDefs.TryGetByHireId(offer.hireId, out var def))
            MercRosterDefs.TryGetByAssetId(offer.mercId, out def);
        string shown = !string.IsNullOrEmpty(offer.displayName) ? offer.displayName
            : (!string.IsNullOrEmpty(def.Name) ? def.Name : offer.mercId);
        SetText(_nameText, "Panel/zuo/namebg/Name", shown);
        // 【2026-10-19 主人拍板】招募模式名字颜色同 Fill：随 def.Rarity（与名字底牌/卡框同源）
        // Def 是结构体不做 null 比较（上面 def.Name 已直接解引用，口径一致）
        if (_nameText != null) _nameText.color = RarityPalette.Get(def.Rarity);

        string skillId = !string.IsNullOrEmpty(offer.skillId) ? offer.skillId : offer.passiveSkillId;
        FillSkill(skillId);

        ApplyRecruitButtons(true, BuildBuyLabel(offer));
    }

    /// <summary>购买钮文案（【2026-10-19 主人拍板】货币跟稀有度走）：
    /// 普通佣兵=「600金币」；稀有佣兵=「普通卷×1」；传奇佣兵=「稀有卷×1」；
    /// 已拥有的候选=「已拥有」，不可重复购买。</summary>
    string BuildBuyLabel(MercenaryData offer)
    {
        var rarity = MercHireSession.OfferRarity(offer);
        if (MercHireSession.IsMercOwned(offer)) return "已拥有";
        if (rarity == MercRosterDefs.MercRarity.Common)
            return MercHireSession.GoldCost(offer) + "金币";
        return rarity == MercRosterDefs.MercRarity.Legendary ? "稀有卷×1" : "普通卷×1";
    }

    /// <summary>招募模式按钮/标题文案切换：on=true 双钮并排（购买/取消）+ 标题「佣兵信息」；
    /// on=false 还原预制体原文案（确认/替换/获得佣兵），布局复用 ApplyReplaceButton。</summary>
    void ApplyRecruitButtons(bool on, string buyLabel)
    {
        if (_keepLabel != null) _keepLabel.text = on ? (buyLabel ?? "购买") : _keepLabelText0;
        if (_replaceLabel != null) _replaceLabel.text = on ? "取消" : _replaceLabelText0;
        if (_titleText != null) _titleText.text = on ? "佣兵信息" : _titleText0;
        ApplyReplaceButton(on);
    }

    // ============================================================
    // 小工具：字段为 null 说明 Bind 没绑到，补一条 LogError 指明缺哪个节点
    // ============================================================

}
