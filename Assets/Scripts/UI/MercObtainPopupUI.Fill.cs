using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 内容填充：阵亡护符提示、星级居中、卡面与技能文案填充，以及 SetText/SetActive 小工具。
/// 【2026-10-23 主人拍板】原 MercObtainPopupUI.cs 已 929 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercObtainPopupUI : MonoBehaviour
{
    void EnsureReviveCharmHint()
    {
        if (_root == null) return;
        _reviveCharmHint = _root.transform.Find("Panel/" + ReviveCharmHintNode)?.gameObject;
        if (_reviveCharmHint != null) return;

        var panel = _root.transform.Find("Panel");
        if (panel == null || _keepRt == null)
        {
            Debug.LogError("[MercObtainPopup] 缺 Panel 或 KeepButton，复活护符提示行挂不上（不影响其它功能）");
            return;
        }

        _reviveCharmHint = new GameObject(ReviveCharmHintNode, typeof(RectTransform));
        _reviveCharmHint.transform.SetParent(panel, false);
        var rt = _reviveCharmHint.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        // 跟着复活钮的锚位走：正上方 ≈ 一个按钮高 + 间距（暂 95px，效果不对再调）
        rt.anchoredPosition = new Vector2(_keepBasePos.x, _keepBasePos.y + 95f);
        rt.sizeDelta = new Vector2(220f, 44f);

        // 护符 icon
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(_reviveCharmHint.transform, false);
        var iRt = iconGo.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0f, 0.5f); iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot = new Vector2(0f, 0.5f);
        iRt.anchoredPosition = new Vector2(0f, 0f);
        iRt.sizeDelta = new Vector2(40f, 40f);
        var iImg = iconGo.GetComponent<Image>();
        iImg.raycastTarget = false;
        iImg.preserveAspect = true;
        var iconSp = ItemDefs.LoadIcon(ReviveCharmItemIdForHint);
        if (iconSp == null)
            Debug.LogError("[MercObtainPopup] 重生护符图标加载失败（item_defs icon 路径或 Resources 图缺失）");
        iImg.sprite = iconSp;
        iImg.enabled = iconSp != null;

        // ×N 数量
        var txtGo = new GameObject("Count", typeof(RectTransform));
        txtGo.transform.SetParent(_reviveCharmHint.transform, false);
        var t = txtGo.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = 30;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.raycastTarget = false;
        var tRt = txtGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f); tRt.anchorMax = new Vector2(0f, 1f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(48f, 0f);
        tRt.sizeDelta = new Vector2(170f, 44f);
        _reviveCharmCount = t;

        _reviveCharmHint.SetActive(false);   // 只有阵亡复活模式显示
    }

    /// <summary>阵亡复活模式开窗时刷提示行与复活钮颜色：有护符正常色，没护符字变红（点了 Toast 道具不足）。</summary>
    void ApplyFallenCharm()
    {
        int charms = 0;
        var data = SaveSystem.Instance?.Data;
        if (data != null) charms = data.reviveCharms;

        if (_reviveCharmHint != null)
        {
            _reviveCharmHint.SetActive(true);
            if (_reviveCharmCount != null) _reviveCharmCount.text = "×" + charms;
        }
        if (_keepLabel != null)
            _keepLabel.color = charms > 0 ? _keepLabelColor0 : new Color(1f, 0.4f, 0.4f);
    }

    // ============================================================
    // 内容填充（只填数据，不碰排版）
    // ============================================================

    /// <summary>
    /// 【2026-10-14 主人拍板】亮 1 / 2 / 3 颗星都让整组居中：
    /// 以「满星 3 颗」的包围盒中心为基准（预制体的设计居中态），
    /// 亮 star 颗时把整组从原始位置平移 (基准中心 − star 颗组中心)。
    /// 每次都从 _starBasePos 出发重算，反复开弹窗不会累积偏移。
    /// </summary>
    void CenterActiveStars(int star)
    {
        if (_stars == null || _starBasePos == null) return;
        for (int i = 0; i < MaxStarNodes; i++)
            if (_stars[i] == null || _stars[i].GetComponent<RectTransform>() == null) return; // 结构缺失就不动，交给上面的 LogError

        Vector2 bMin = _starBasePos[0], bMax = _starBasePos[0];
        for (int i = 1; i < MaxStarNodes; i++)
        {
            bMin = Vector2.Min(bMin, _starBasePos[i]);
            bMax = Vector2.Max(bMax, _starBasePos[i]);
        }
        Vector2 baseCenter = (bMin + bMax) * 0.5f;

        Vector2 cMin = _starBasePos[0], cMax = _starBasePos[0];
        for (int i = 1; i < star; i++)
        {
            cMin = Vector2.Min(cMin, _starBasePos[i]);
            cMax = Vector2.Max(cMax, _starBasePos[i]);
        }
        Vector2 delta = baseCenter - (cMin + cMax) * 0.5f;

        for (int i = 0; i < MaxStarNodes; i++)
        {
            var rt = _stars[i].GetComponent<RectTransform>();
            rt.anchoredPosition = _starBasePos[i] + delta;
        }
    }

    bool Fill(DraftCard card)
    {
        if (_root == null)
        {
            Debug.LogError("[MercObtainPopup] 预制体缺少 Root 节点，本次获得佣兵弹窗跳过（onClosed 已回调，不卡抽奖）");
            return false;
        }

        // 1) 佣兵数据：HireId 优先，AssetId 兜底（旧档卡面两种填法都有过）
        bool ok = MercRosterDefs.TryGetByHireId(card.HireId, out var def);
        if (!ok) ok = MercRosterDefs.TryGetByAssetId(card.Id, out def);
        if (!ok)
        {
            Debug.LogError("[MercObtainPopup] 佣兵数据缺失：HireId=" + card.HireId + " Id=" + card.Id);
            return false;
        }

        // 2) 立绘（LoadPortrait 内部已 LogError，不重复刷屏）
        string hireId = string.IsNullOrEmpty(card.HireId) ? def.HireId : card.HireId;
        // 【2026-10-22 主人拍板】记下当前佣兵 hireId 并刷「进阶」钮显隐（已拥有才显示，见 ApplyAscendButton）
        _curHireId = hireId;
        ApplyAscendButton();
        var sp = MercJoinBannerUI.LoadPortrait(hireId);
        if (_icon == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/zuo/Iconmask/Icon（立绘不显示）");
        else
        {
            _icon.sprite = sp;                 // sp 为 null 时清掉，不留上一次的图
            _icon.enabled = sp != null;
        }

        // 3) 名字：要名字不要代号；仍为空 → 报出来，不静默显示空白
        string mercName = string.IsNullOrEmpty(def.Name) ? def.Nickname : def.Name;
        if (string.IsNullOrEmpty(mercName))
            Debug.LogError("[MercObtainPopup] 佣兵名字为空：HireId=" + def.HireId);
        SetText(_nameText, "Panel/zuo/namebg/Name", mercName);
        // 【2026-10-19 主人拍板】名字颜色随稀有度（RarityPalette 唯一配色表）：
        // 与下方名字底牌/卡框同一 def.Rarity 口径，色档与底牌图档同源不穿帮
        if (_nameText != null) _nameText.color = RarityPalette.Get(def.Rarity);

        // 3.2)【2026-10-18 主人拍板】名字底牌按稀有度换三档图（与佣兵池列表卡同口径，
        // 唯一入口 MercHireSession.LoadTavernNamePlate；取不到报错不静默）
        if (_nameBg == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/zuo/namebg（名字底牌不随稀有度变化）");
        else
        {
            var plate = MercHireSession.LoadTavernNamePlate(def.Rarity);
            if (plate != null)
            {
                _nameBg.sprite = plate;
                _nameBg.type = Image.Type.Sliced;
                _nameBg.color = Color.white;
            }
            else
                Debug.LogError("[MercObtainPopup] 取不到稀有度名字底牌图：HireId=" + def.HireId
                    + " Rarity=" + def.Rarity);
        }

        // 3.3)【2026-10-18 主人拍板】立绘卡框（头像框）也随稀有度换三档图 —— 与名字底牌同一套
        // （同目录 Art/UI/Tavern/酒馆头像/，同佣兵池列表卡口径 LoadTavernCardFrame）。
        // 旧态只有名字底牌换、卡框是静态图 → 稀有佣兵「蓝名字框 + 灰卡框」不一致（主人报）。
        if (_zuoImg == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/zuo（立绘卡框不随稀有度变化）");
        else
        {
            var cardFrame = MercHireSession.LoadTavernCardFrame(def.Rarity);
            if (cardFrame != null)
            {
                _zuoImg.sprite = cardFrame;
                _zuoImg.type = Image.Type.Sliced;
                _zuoImg.color = Color.white;
            }
            else
                Debug.LogError("[MercObtainPopup] 取不到稀有度卡框图：HireId=" + def.HireId
                    + " Rarity=" + def.Rarity);
        }

        // 3.5) Role 四分类图标：CombatBadge（自带 WarnOnce），取不到隐藏节点（T6）
        if (_role == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/Role（四分类图标不显示）");
        else
        {
            var roleSp = JobIconResolver.CombatBadge(def.JobName);
            _role.sprite = roleSp;
            _role.enabled = roleSp != null;
            // 【2026-10-19 主人拍板】职业徽章素材原生 42×50，显示不许改变图片大小：
            // 节点尺寸跟随精灵原生宽高，不开 preserveAspect（那会按节点框改显示尺寸）
            if (roleSp != null)
            {
                _role.rectTransform.sizeDelta = new Vector2(roleSp.rect.width, roleSp.rect.height);
                _role.preserveAspect = false;
            }
        }

        // 4) 数值：生命/攻击/防御走 ApplyCombatStats（只随等级）
        MercRosterDefs.ApplyCombatStats(def.AssetId, Mathf.Max(1, card.MercLevel),
            out float hp, out float atk, out float defVal, out float atkSpd, out float move, out float range);
        SetText(_attrHp, "Panel/you/AttrPanel/AttrHpRoot/AttrHp", Mathf.RoundToInt(hp).ToString());
        // 法术型佣兵：攻击/防御两行换成「法术攻击/法术防御」（数值同源，只换标签）
        bool magic = Mercenary.UsesMagicAttack(def.AssetId);
        SetActive(_attrAtkRoot, "Panel/you/AttrPanel/AttrAtkRoot", !magic);
        SetText(_attrAtk, "Panel/you/AttrPanel/AttrAtkRoot/AttrAtk", Mathf.RoundToInt(atk).ToString());
        SetActive(_attrMagicAtkRoot, "Panel/you/AttrPanel/AttrMagicAtkRoot", magic);
        SetText(_attrMagicAtk, "Panel/you/AttrPanel/AttrMagicAtkRoot/AttrMagicAtk", Mathf.RoundToInt(atk).ToString());
        SetActive(_attrDefRoot, "Panel/you/AttrPanel/AttrDefRoot", !magic);
        SetText(_attrDef, "Panel/you/AttrPanel/AttrDefRoot/AttrDef", Mathf.RoundToInt(defVal).ToString());
        SetActive(_attrMagicDefRoot, "Panel/you/AttrPanel/AttrMagicDefRoot", magic);
        SetText(_attrMagicDef, "Panel/you/AttrPanel/AttrMagicDefRoot/AttrMagicDef", Mathf.RoundToInt(defVal).ToString());
        // 速度 = 移动速度（主人 2026-10-14 口径）
        // 【2026-10-24 主人拍板】速度显示不用小数：原值 ×100 取整（0.85 → 85），只改显示口径，战斗数值不动。
        SetActive(_attrSpdRoot, "Panel/you/AttrPanel/AttrSpdRoot", true);
        SetText(_attrSpd, "Panel/you/AttrPanel/AttrSpdRoot/AttrSpd", Mathf.RoundToInt(move * 100f).ToString());
        // 暴击 = 本项目基础值（未计入星级/装备成长）；0.05 这类小数按百分比显示
        SetActive(_attrCritRoot, "Panel/you/AttrPanel/AttrCritRoot", true);
        float crit = GameConfig.BASE_CRIT_RATE;
        if (crit <= 1.5f) crit *= 100f;
        SetText(_attrCrit, "Panel/you/AttrPanel/AttrCritRoot/AttrCrit", crit.ToString("0.#") + "%");

        // 5) 星星（预制体只摆了 3 个满星节点，正好对上局外星上限 MaxOutStar=3）。
        //    【2026-10-24 主人拍板】星级显示统一 = 已拥有的局外星级（MercStarSystem.OutStarOf，
        //    唯一出口）：没升过星的一律 0 星（整排不亮）；招募候选 / 战斗头像详情 / 复活框同口径。
        //    局内 star（1~5）体系不动 —— card.Star 是局内战力口径，不再用于显示。
        int outStar = MercStarSystem.OutStarOf(hireId);
        if (outStar < 0) outStar = 0;   // 非花名册（Fill 上方已保证能查到 def，理论到不了）按 0 星显示
        int star = Mathf.Clamp(outStar, 0, MaxStarNodes);
        for (int i = 0; i < _stars.Length; i++)
            if (_stars[i] != null) _stars[i].SetActive(i < star);
        if (star > 0) CenterActiveStars(star);   // 0 星全灭，无需居中

        // 6) 技能：没有主动技就空着（主人拍板）；有则填名字/简介/图标
        FillSkill(def.ActiveSkillId);

        return true;
    }

    /// <summary>
    /// 技能栏填充（Fill 与酒馆招募入口共用）：skillId 为空或表里查不到时整栏隐藏，有则填名字/简介/图标。
    /// </summary>
    void FillSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId))
        {
            if (_skillRoot == null)
                Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Skill（该佣兵无主动技，整栏不显示）");
            else _skillRoot.SetActive(false);
            return;
        }
        if (MercSkillTable.TryGet(skillId, out var row))
        {
            SetActive(_skillRoot, "Panel/you/Skill", true);
            SetText(_skillName, "Panel/you/Skill/SkillName", row.DisplayName ?? "");
            SetText(_skillInfo, "Panel/you/Skill/Skillxinxi", row.RecruitDesc ?? "");
            var si = MercSkillTable.LoadIcon(skillId);
            if (_skillIcon == null)
                Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Skill/SkillIcon（技能图标不显示）");
            else
            {
                _skillIcon.sprite = si;
                _skillIcon.enabled = si != null;
            }
            if (si == null) Debug.LogError("[MercObtainPopup] 取不到佣兵技能图标 " + skillId);
        }
        else
        {
            Debug.LogError("[MercObtainPopup] 佣兵技能表查不到 " + skillId);
            SetActive(_skillRoot, "Panel/you/Skill", false);
        }
    }

    // ============================================================
    // 酒馆招募购买模式（2026-10-19 主人拍板）
    // ============================================================

    /// <summary>
    /// 酒馆招募卡点进来的「佣兵信息」弹窗：标题改佣兵信息，两钮换成 购买（带价格两行）/取消。
    /// <paramref name="tryBuy"/> 由招募界面传入（扣卷/扣金币 + 入队 + 刷新 + Toast），
    /// 返回 true 才关弹窗；失败留在弹窗由玩家改点取消。买不起时招募界面只弹字提示、不会进到这里，
    /// 弹窗内再点购买若失败（如中途金币变动）也只 Toast 不关弹窗。
    /// </summary>

    static void SetText(Text t, string nodePath, string value)
    {
        if (t == null)
        {
            Debug.LogError("[MercObtainPopup] 缺少文本节点 " + nodePath);
            return;
        }
        t.text = value ?? "";
    }

    static void SetActive(GameObject go, string nodePath, bool on)
    {
        if (go == null)
        {
            Debug.LogError("[MercObtainPopup] 缺少节点 " + nodePath);
            return;
        }
        go.SetActive(on);
    }
}

