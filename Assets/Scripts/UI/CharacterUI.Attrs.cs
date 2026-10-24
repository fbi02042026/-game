using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：属性面板（RefreshAttrs、天赋加成汇总、魔攻/魔防行与图标、美术纠错）
public partial class CharacterUI
{
    void RefreshIdentity()
    {
        if (titleText != null)
            titleText.text = PlayerIdentity.DisplayName;
        if (titleSubText != null)
            titleSubText.text = PlayerIdentity.Title;
    }

    /// <summary>
    /// 纠正手做预制体里明显错绑的图：名字旁用了「切换」、背包用了战斗大边框、
    /// 右侧天赋半透底用了横板翻转。只换资源/显隐，不改坐标。
    /// </summary>
    void FixWrongArtBindings()
    {
        // Header/icon：切换箭头 → 玩家头像
        if (headerAvatar == null)
            headerAvatar = transform.Find("Content/Header/icon")?.GetComponent<Image>();
        if (headerAvatar != null)
        {
            var avatar = LoadNavArt("角色_0000s_0000_玩家头像");
            if (avatar != null)
            {
                headerAvatar.sprite = avatar;
                headerAvatar.preserveAspect = true;
                headerAvatar.color = Color.white;
            }
        }

        // 背包底框：战斗「大边框」→ 角色页面板
        var bagPanel = (transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel"))
                       ?.GetComponent<Image>();
        if (bagPanel != null)
        {
            var frame = LoadNavArt("角色_0002s_0009_图层-6-拷贝");
            if (frame != null)
            {
                bagPanel.sprite = frame;
                bagPanel.type = Image.Type.Sliced;
                bagPanel.color = Color.white;
            }
        }

        // 右侧天赋底：错用横板+Y翻转，关掉以免脏半透条；按钮本身已有底图
        var rightBg = transform.Find("Content/Stage/RightButtons/bg");
        if (rightBg != null)
            rightBg.gameObject.SetActive(false);
    }
    // 🔴 2026-10-08 主人拍板删除 EnsureRightButtonArt() 调用：不要兜底。
    //    断链就修 guid / 重拖预制体，不许运行时 Resources.Load 补图；
    //    上次兜底把 4 张图复制进 Resources 造成同图两份，团结遂签 56 字符反复复发。
    //    4 张现已回到预制体（指向 Resources 那份的新 hex32），此处不需要任何补图逻辑。

    static Sprite LoadNavArt(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        // 统一走 Resources，避免 Art / Resources 双份
        var sp = Resources.Load<Sprite>("UI/NavCharacter/" + fileNameWithoutExt);
        if (sp != null) return sp;
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            string path = "Assets/Art/UI/NavCharacter/" + fileNameWithoutExt + ".png";
            var ed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ed != null) return ed;
        }
#endif
        return null;
    }

    /// <summary>
    /// <summary>
    /// 2026-09-29：取「魔攻 / 魔防」这两行的 Text。
    /// <para>🔴 主人口径「不要兜底、出错要让我看见」：只按标准名
    /// <c>AttrPanel/{stdName}Root/{stdName}</c> 找，**不做关键字模糊匹配、也不运行时克隆** ——
    /// 缺节点直接报错，绝不静默地显示不出来。</para>
    /// </summary>
    Text ResolveMagicAttrRow(string stdName, string cnName)
    {
        var hit = FindTxt("Content/AttrPanel/" + stdName + "Root/" + stdName)
                  ?? FindTxt("Content/AttrPanel/" + stdName);
        if (hit == null)
        {
            Debug.LogError($"[CharacterUI] 角色面板缺少属性节点 Content/AttrPanel/{stdName}Root/{stdName}，" +
                           $"【{cnName}】这一条不会显示 —— 请在 CharacterUI 预制体的 AttrPanel 下补这个节点。");
        }
        return hit;
    }

    /// <summary>给魔攻 / 魔防那两行换上对应图标（图在 Resources/UI/AttrIcons 下）。同样不兜底。</summary>
    void EnsureMagicAttrIcons()
    {
        SetRowIcon(attrMagicAtkText, "角色_0001s_0000_魔攻", "魔攻");
        SetRowIcon(attrMagicDefText, "角色_0001s_0000_魔防", "魔防");
    }

    static void SetRowIcon(Text valueText, string iconFile, string cnName)
    {
        if (valueText == null) return;   // 节点缺失上面已经报过错，不重复刷屏
        var row = valueText.transform.parent;
        var spr = TalentIcons.GetAttrSprite(iconFile);
        if (spr == null)
        {
            Debug.LogError($"[CharacterUI] 属性图标缺失：Resources/UI/AttrIcons/{iconFile}.png，" +
                           $"【{cnName}】那一行会显示成别的图标 —— 请把图放进 Resources/UI/AttrIcons。");
            return;
        }
        if (row == null) return;
        bool applied = false;
        foreach (var img in row.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            img.sprite = spr;
            applied = true;
            break;   // 只换该行第一个 Image（图标位）
        }
        if (!applied)
            Debug.LogError($"[CharacterUI] 【{cnName}】行（{row.name}）下找不到 Image 组件，图标换不上去。");
    }

    void RefreshAttrs()
    {
        float hp = GameConfig.BASE_HP, atk = GameConfig.BASE_ATTACK, def = GameConfig.BASE_DEFENSE;
        float spd = GameConfig.BASE_ATTACK_SPEED, crit = GameConfig.BASE_CRIT_RATE, resist = 0f;
        // 2026-09-29：攻击/防御各拆两条（物攻 mag 侧 / 魔攻，物防 / 魔防），面板分区显示
        float mag = GameConfig.BASE_ATTACK, magicDef = 0f;
        float talAtk = 0f, talMag = 0f, talHp = 0f, talDef = 0f, talCrit = 0f, talSpd = 0f;
        SumTalentAttrBonuses(ref talAtk, ref talMag, ref talHp, ref talDef, ref talCrit, ref talSpd);
        try
        {
            AttrSystem src = null;
            var hero = Hero.Instance;
            if (hero != null && hero.attr != null)
            {
                src = hero.attr;
            }
            else
            {
                src = new AttrSystem(AttrOwnerKind.Player);
                var bonuses = EquipStatRollup.BuildBonusList(BattleBagSystem.Instance);
                src.RecalcAllAttr(bonuses);
            }

            if (src != null)
            {
                hp = src.GetAttr(AttrType.MaxHp);
                // 2026-09-29：面板分区 —— 物攻 / 魔攻 / 物防 / 魔防 **四条都显示**。
                // 天赋是跨局成长、两条都给，玩家换职业要看得到自己攒的两边各是多少。
                atk = src.GetAttr(AttrType.Attack);
                mag = src.GetAttr(AttrType.MagicAttack);
                def = src.GetAttr(AttrType.Defense);
                magicDef = src.GetAttr(AttrType.MagicDefense);
                spd = src.GetAttr(AttrType.AttackSpeed);
                crit = src.GetAttr(AttrType.CritRate);
                resist = 0f;
            }
        }
        catch { }

        float critDisplay = crit;
        if (critDisplay <= 1.5f)
            critDisplay *= 100f;

        // 天赋加成在后面用 +N 标出（与天赋页汇总一致）
        if (attrHpText != null) attrHpText.text = FormatAttrWithTalent(hp, talHp, roundInt: true);
        if (attrAtkText != null) attrAtkText.text = FormatAttrWithTalent(atk, talAtk, roundInt: true);
        if (attrDefText != null) attrDefText.text = FormatAttrWithTalent(def, talDef, roundInt: true);
        // 魔攻 / 魔防：需要面板新增节点。找不到就不显示（不会报错），等排版定好后接上。
        if (attrMagicAtkText != null) attrMagicAtkText.text = FormatAttrWithTalent(mag, talMag, roundInt: true);
        if (attrMagicDefText != null) attrMagicDefText.text = FormatAttrWithTalent(magicDef, talDef, roundInt: true);
        // 【2026-10-24 主人拍板】速度显示不用小数：原值 ×100 取整（0.85 → 85），只改显示口径，战斗数值不动。
        if (attrSpdText != null) attrSpdText.text = Mathf.RoundToInt(spd * 100f).ToString();
        if (attrCritText != null)
        {
            string core = FormatAttrWithTalent(critDisplay, talCrit, roundInt: false, decimals: "0.#");
            attrCritText.text = core.EndsWith("%") ? core : core + "%";
        }
        if (attrResistText != null) attrResistText.text = resist.ToString("0.#") + "%";
    }

    static void SumTalentAttrBonuses(ref float atk, ref float mag, ref float hp, ref float def, ref float crit, ref float spd)
    {
        var talents = SaveSystem.Instance?.Data?.talents;
        if (talents == null) return;
        int leftN = TalentDefs.LeftUnlockedCount(talents);
        for (int i = 0; i < leftN; i++)
        {
            var e = TalentDefs.Left[i].effect;
            if (e == null) continue;
            switch (e.kind)
            {
                // 2026-10-10：左列第 4 槽已改走 Attack；攻击类节点同时增加 Attack 和 MagicAttack，
                // 不再区分物理/法系，因此同一份天赋值要同时累加进 atk 与 mag。
                // 面板两侧都显示 +N，保证与 AttrSystem 的实际加成一致。
                case TalentDefs.AttrKind.Attack: atk += e.value; mag += e.value; break;
                case TalentDefs.AttrKind.Hp: hp += e.value; break;
                case TalentDefs.AttrKind.Defense: def += e.value; break;
                case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
            }
        }
        // 右列重制节点：累加可计入面板的属性（攻击/生命/防御/暴击/攻速）
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            int lv = TalentDefs.GetRightNodeLevel(talents, node.id);
            if (lv <= 0) continue;
            int job = TalentDefs.GetRightNodeChosenJob(talents, node.id);
            var opt = node.IsJobChoice ? node.ChosenOption(job)
                                       : (node.options != null && node.options.Length > 0 ? node.options[0] : null);
            if (opt == null) continue;
            AccumulateRightTalent(opt.kind, node.EffectValue(lv, job),
                ref atk, ref mag, ref hp, ref def, ref crit, ref spd);
        }
    }

    static void AccumulateRightTalent(TalentDefs.AttrKind kind, float value,
        ref float atk, ref float mag, ref float hp, ref float def, ref float crit, ref float spd)
    {
        switch (kind)
        {
            // 右列喂的是**战斗属性**；与 AttrSystem.ApplyTalentEffect 同口径：
            // AttrKind.Attack 是「固定值攻击」，物攻 / 魔攻**两侧都加**（与 Defense 同口径，左右列一致）。
            // 区分度在 PhysDamage / MagicDamage 专精节点上，不在 Attack。
            case TalentDefs.AttrKind.Attack: atk += value; mag += value; break;
            case TalentDefs.AttrKind.Hp: hp += value; break;
            case TalentDefs.AttrKind.Defense: def += value; break;
            case TalentDefs.AttrKind.CritRate: crit += value; break;
            case TalentDefs.AttrKind.AtkSpeed: spd += value; break;
        }
    }

    /// <summary>总数已含天赋：直接显示最终属性值，不再拆「基础+加成」（2026-10-17 主人拍板）。</summary>
    static string FormatAttrWithTalent(float total, float talentBonus, bool roundInt, string decimals = "0")
    {
        if (roundInt) return Mathf.RoundToInt(total).ToString("N0");
        return total.ToString(decimals);
    }

    void RefreshBag()
    {
        backpackGrid?.Refresh();
        int unlocked = backpackGrid != null ? backpackGrid.UnlockedSlotCount() : GameConfig.BACKPACK_DEFAULT_ROWS * GameConfig.BACKPACK_WIDTH;
        int used = 0;
        if (backpackGrid != null)
        {
            for (int i = 0; i < backpackGrid.cells.Count; i++)
                if (backpackGrid.cells[i]?.equippedItem != null) used++;
        }
        if (bagCapacityText != null)
            bagCapacityText.text = used + "/" + unlocked;
        // 2026-10-24 主人拍板：角色页背包标题「随身仓库」每刷一次都兜底重写，防止被别处重置。
        if (bagTitleText != null) bagTitleText.text = "随身仓库";
        RefreshStaminaPotionRow();
    }
}
