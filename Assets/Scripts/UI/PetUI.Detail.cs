using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物详情页：只替换图片与文字，不碰任何节点位置/尺寸/锚点。
/// 好感进度照搬 MercFavorUI 口径（每颗心 5 次喂养、3 颗满；空心 Filled 从下往上露实心），
/// 区别：宠物无数字显示（用户 18:35 明确）。
/// </summary>
public partial class PetUI
{
    const int FEED_PER_HEART = 5;
    const int HEART_COUNT = 3;

    void RefreshDetail()
    {
        // 列表选中的永远是本体（左侧只列 5 只本体）；展示条目按「当前上阵的是不是这只有皮肤」再解析一次。
        var body = SelectedDef;
        var save = SaveSystem.Instance?.Data;
        if (body == null) return;

        // 2026-11-21 用户点名「切换皮肤后要是换皮后的展示和进入战斗」：
        // 上阵 id = 皮肤 id 时，详情页的名字/立绘/属性/技能一律按皮肤条目显示。
        // 进战斗同源（BattleManager.PetDeploy 读的也是 petDeployedId），展示与战斗恒同款。
        var def = DisplayDef(body, save);

        // 解锁态按本体判：皮肤能上阵的前提是本体已解锁，不拿皮肤 id 当解锁条件。
        bool unlocked = save != null && save.IsPetUnlocked(body.id);

        // 名字（皮肤显示皮肤名）：按品质星级 1~4 分色，色值取项目真源 RarityPalette（2026-11-21）
        if (_nameText != null)
        {
            SetText(_nameText, def.name);
            var nameLabel = GetText(_nameText);
            if (nameLabel != null) nameLabel.color = QualityNameColor(def.qualityStar);
        }

        // PetArt：实例化当前宠物 prefab 播 idle（皮肤跟着换模型）
        RefreshPetArt(def);

        // 属性 / 战力（只填数字，不动节点）。2026-11-22：叠加喂养随机属性加成（键 = 本体 id）。
        string attrBodyId = PetDefs.IsSkin(def) ? def.skinOf : def.id;
        var bonus = save != null ? save.GetPetAttrBonus(attrBodyId) : null;
        int hpBonus = bonus != null ? bonus.hp : 0;
        int atkBonus = bonus != null ? bonus.atk : 0;
        int defBonus = bonus != null ? bonus.def : 0;
        if (_hpText != null) SetText(_hpText, (def.hp + hpBonus).ToString());
        if (_atkText != null) SetText(_atkText, (def.atk + atkBonus).ToString());
        if (_defText != null) SetText(_defText, (def.def + defBonus).ToString());
        if (_powerText != null) SetText(_powerText, "战力 " + def.power);

        // 品质星级 1~4：Star0..3，前 qualityStar 颗点亮（只换图/色，不动位置）
        for (int i = 0; i < _stars.Length; i++)
            if (_stars[i] != null)
            {
                var img = GetImage(_stars[i]);
                if (img != null) img.color = (i < def.qualityStar) ? _starLitColor : new Color(0.45f, 0.45f, 0.5f, 1f);
            }

        // 技能图标 + 文案（皮肤跟随本体技能；恶魔狼换皮后文案变暗影箭）
        var skillSpr = PetDefs.LoadSkillIcon(def.id);
        if (_skillIcon != null) _skillIcon.sprite = skillSpr;
        if (_skillTitle != null) SetText(_skillTitle.transform, def.skill);
        if (_skillInfo != null) SetText(_skillInfo.transform, def.skillDesc);

        // 右上皮肤槽：默认显示皮肤的图（用户 18:35），未拥有则盖锁
        var skin = PetDefs.SkinOf(def.skinOf ?? def.id) ?? (PetDefs.IsSkin(def) ? def : null);
        var bodyId = PetDefs.IsSkin(def) ? def.skinOf : def.id;
        var skinDef = PetDefs.SkinOf(bodyId);
        if (skinDef != null)
        {
            // 皮肤槽显示「点击后的切换目标」：当前本体→显示皮肤图/皮肤名，当前皮肤→显示本体图/本体名。
            // 主人 11-22「切换后应该显示另一个皮肤的图，现在始终固定显示一个」——旧写法恒 LoadAvatar(skinDef.id)，
            // 无论上阵本体还是皮肤都是同一张皮图，切了看不出变化。def 已由 DisplayDef 解析为上阵条目（本体或皮肤）。
            var switchTarget = PetDefs.IsSkin(def) ? PetDefs.Get(bodyId) : skinDef;
            if (_skinPortrait != null)
            {
                var si = GetImage(_skinPortrait);
                if (si != null) si.sprite = PetDefs.LoadAvatar(switchTarget != null ? switchTarget.id : skinDef.id);
            }
            if (_skinName != null) SetText(_skinName, switchTarget != null ? switchTarget.name : skinDef.name);
            // 锁态仍按皮肤本体是否已拥有判（切回本体不需要皮肤解锁），与显示目标无关。
            bool skinOwned = save != null && save.IsPetUnlocked(skinDef.id);
            if (_skinLock != null) _skinLock.gameObject.SetActive(!skinOwned);
        }

        // 好感进度：喂养次数 → 3 颗心，无数字
        RefreshFavor(bodyId, save);

        // 按钮态（上阵/喂养/解锁）
        RefreshDeployButton(def, unlocked, save);
    }

    /// <summary>
    /// 详情页展示用条目解析（换皮后展示跟随皮肤的唯一出口）：
    /// 选中本体、而存档里上阵的正是这只的皮肤 → 返回皮肤条目（模型/名字/技能描述一起换）；
    /// 其它情况原样返回本体。上阵 id 是唯一真源（SaveData.petDeployedId），战斗侧读同一字段。
    /// </summary>
    static PetDefs.Def DisplayDef(PetDefs.Def body, SaveData save)
    {
        if (body == null || save == null) return body;
        var deployed = PetDefs.Get(save.petDeployedId);
        if (PetDefs.IsSkin(deployed) && deployed.skinOf == body.id) return deployed;
        return body;
    }

    void RefreshFavor(string bodyId, SaveData save)
    {
        int feed = save != null ? save.GetPetFeedCount(bodyId) : 0;
        for (int i = 0; i < HEART_COUNT; i++)
        {
            float frac = Mathf.Clamp01((feed - i * FEED_PER_HEART) / (float)FEED_PER_HEART);
            var fill = _heartFills[i];
            var empty = _heartEmpties[i];
            if (fill != null) fill.enabled = true;
            if (empty == null) continue;
            empty.enabled = frac < 1f;
            if (frac < 1f)
            {
                if (empty.type != Image.Type.Filled)
                {
                    empty.type = Image.Type.Filled;
                    empty.fillMethod = Image.FillMethod.Vertical;
                    empty.fillOrigin = (int)Image.OriginVertical.Top;
                }
                empty.fillAmount = 1f - frac;
            }
        }
    }

    void RefreshDeployButton(PetDefs.Def def, bool unlocked, SaveData save)
    {
        if (_deployLabel == null) return;
        bool feeding = unlocked && save != null && save.petDeployedId == def.id;
        string label;
        if (!unlocked)
            label = "解锁";
        else if (feeding)
            label = "喂养";
        else
            label = "上阵";
        SetText(_deployLabel, label);

        // 喂养按钮上方的金币图标+消耗数字：仅喂养态显示，价格随好感档位变
        if (_feedCostBadge != null)
        {
            _feedCostBadge.SetActive(feeding);
            if (feeding)
            {
                int fed = save.GetPetFeedCount(PetDefs.IsSkin(def) ? def.skinOf : def.id);
                int cost = FeedCost(fed);
                if (_feedCostText != null) _feedCostText.text = cost.ToString();
            }
        }
    }

    GameObject _feedCostBadge;

    /// <summary>在 DeployBtn 上方建金币图标+数字（prefab 无此节点，运行时新建；不动任何现有节点位置）。</summary>
    void EnsureFeedCostBadge()
    {
        if (_feedCostBadge != null || _deployBtn == null) return;
        var badge = new GameObject("FeedCostBadge", typeof(RectTransform));
        badge.transform.SetParent(_deployBtn, false);
        var bRt = (RectTransform)badge.transform;
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 1f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = new Vector2(0f, 4f);
        bRt.sizeDelta = new Vector2(104f, 32f);   // 图标放大：22→32，容器/数字同步放大

        var iconGo = new GameObject("GoldIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(badge.transform, false);
        var iRt = (RectTransform)iconGo.transform;
        iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot = new Vector2(0f, 0.5f);
        iRt.anchoredPosition = new Vector2(0f, 0f);
        iRt.sizeDelta = new Vector2(32f, 32f);
        _feedCostIcon = iconGo.GetComponent<Image>();
        var gold = Resources.Load<Sprite>("UI/Icons/Common/icon_gold");
        if (gold != null) { _feedCostIcon.sprite = gold; _feedCostIcon.preserveAspect = true; }
        _feedCostIcon.raycastTarget = false;

        var txtGo = new GameObject("CostNum", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(badge.transform, false);
        var tRt = (RectTransform)txtGo.transform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 0.5f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(34f, 0f);
        tRt.sizeDelta = new Vector2(70f, 32f);
        _feedCostText = txtGo.GetComponent<Text>();
        _feedCostText.font = GameFonts.GetNumber();
        _feedCostText.fontSize = 26;
        _feedCostText.color = new Color(1f, 0.92f, 0.5f, 1f);
        _feedCostText.alignment = TextAnchor.MiddleLeft;
        _feedCostText.raycastTarget = false;

        badge.SetActive(false);
        _feedCostBadge = badge;
    }

    /// <summary>品质星级 1~4 的名字色，转发 RarityPalette 唯一真源：1灰白/2绿/3蓝/4橙金。
    /// 11-22 列表图标框底也转发这里，保证「框色 = 字色」同一出口，不另写映射。</summary>
    internal static Color QualityNameColor(int star)
    {
        switch (star)
        {
            case 4: return RarityPalette.Legendary;
            case 3: return RarityPalette.Rare;
            case 2: return RarityPalette.Uncommon;
            default: return RarityPalette.Common;
        }
    }

    static void SetText(Transform t, string s)
    {
        var txt = GetText(t);
        if (txt != null) txt.text = s;
    }
}
