using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-13 主人拍板】抽中瞬间的「奖励详情」弹窗：抽中技能 / 装备 / 佣兵弹「是什么 + 干什么 + 参数」，
/// 抽中重复（升星 / 升级）弹「★N → ★N+1（LvN → LvN+1）+ 每项参数前后变化」。
///
/// <para>与 <see cref="DraftPrizePopupUI"/>（1 秒自动淡出的开奖卡）分工：那个管「喜悦」，这个管「看明白」。
/// 金币 / 强化安慰奖不弹（<see cref="NeedsDetail"/>）。</para>
///
/// <para>主人拍板口径：点任意位置关闭；参数变化配色＝游戏通用「绿升红降」（<see cref="RarityPalette.PowerGain"/> /
/// <see cref="RarityLoss"/>）；4 秒无操作自动关兜底（不挡连抽节奏）。</para>
///
/// <para>实现约定：运行时建树、不落预制体；技能参数与 <see cref="RunDraftDirector.BuildRunSkill"/> 同源算式；
/// 装备属性行借 <see cref="DraftPool.FormatEquipDescLines"/>；佣兵属性走 <see cref="MercRosterDefs.ApplyCombatStats"/>；
/// 蓝条走 <see cref="MpProfile"/>（唯一出口，不自己写数字）。</para>
/// </summary>
public class DraftRewardDetailUI : MonoBehaviour
{
    static DraftRewardDetailUI _instance;

    GameObject _root;
    Image _frame;
    Image _icon;
    Text _kindText;
    Text _titleText;
    Text _metaText;
    Text _bodyText;
    Text _paramsText;
    Text _hintText;

    System.Action _onClosed;
    bool _closeReq;
    Coroutine _autoCo;

    /// <summary>无操作自动关兜底（秒）：不挡连抽，也不怕玩家忘了关。</summary>
    const float AutoCloseSeconds = 4f;

    const float CardW = 640f;
    const float CardH = 680f;

    public static DraftRewardDetailUI Instance => _instance;

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>这档奖励要不要弹详情（金币 / 强化不弹）。</summary>
    public static bool NeedsDetail(DraftCard card)
    {
        switch (card.Kind)
        {
            case DraftCardKind.SkillNew:
            case DraftCardKind.SkillUp:
            case DraftCardKind.MercRecruit:
            case DraftCardKind.MercLevelUp:
            case DraftCardKind.MercStarUp:
            case DraftCardKind.Equip:
                return card.IsValid;
            default:
                return false;
        }
    }

    /// <summary>
    /// 弹详情并等到关闭（调用方 yield 它即可）。
    /// 兜底自动关协程挂在 UI 自己身上：调用方协程被外部 StopCoroutine（撤离 / 死亡）打断时，
    /// 弹窗仍会自己关掉并回调，不会留一个挡脸的面板。
    /// </summary>
    public static IEnumerator CoShow(DraftCard card, System.Action onClosed = null)
    {
        var ui = Ensure();
        if (ui == null || !card.IsValid) { onClosed?.Invoke(); yield break; }
        ui.Fill(card);
        ui._onClosed = onClosed;
        ui._closeReq = false;
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
        while (ui._root != null && ui._root.activeSelf) yield return null;
    }

    static DraftRewardDetailUI Ensure()
    {
        // 【2026-10-14 修】上次 Build 若中途炸过（Canvas 异常），_instance 活着但 _root 没建——
        // 直接复用半成品会让 Fill 必 NRE。这里检测残缺并原地补 Build（五坑③自带清旧 Root）。
        if (_instance != null && _instance._root == null)
            _instance.Build();
        else if (_instance == null)
        {
            var go = new GameObject("DraftRewardDetailUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DraftRewardDetailUI>();
            _instance.Build();
        }
        // Build 失败（Canvas 挂不上）→ 返回 null，CoShow 走 onClosed 优雅收尾，绝不带病 NRE
        if (_instance == null || _instance._root == null)
        {
            Debug.LogError("[DraftRewardDetailUI] 建树不完整，本次奖励详情跳过（onClosed 已回调，不卡面板）");
            return null;
        }
        return _instance;
    }

    void Awake() => _instance = this;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void OpenRoot()
    {
        if (_root == null) return;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
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
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        CloseRoot();
    }

    /// <summary>唯一关闭出口：点任意位置 / 自动兜底都走这；回调只发一次。</summary>
    void CloseRoot()
    {
        _closeReq = true;
        if (_root != null) _root.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    // ============================================================
    // 建树
    // ============================================================

    void Build()
    {
        // 五坑③：代码生成 UI 的 Build() 开头倒序销毁旧 Root
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var old = transform.GetChild(i);
            if (old != null && old.name == "Root") Destroy(old.gameObject);
        }

        // 【2026-10-14 修】C# 的 ?? 对 UnityEngine.Object 不可靠：组件处于 fake null（已销毁残留）
        // 时 ?? 不会走右侧，canvas 拿着假引用 → overrideSorting 直接抛 MissingComponentException
        // → CoShow 协程中断 → 奖品详情永远弹不出（Editor.log:47567/50953 实锤，主人报「塔克入队卡一会」）。
        // 改显式 Unity 判空：fake null 会被 == null 识别并重新 AddComponent。
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        if (canvas == null)
        {
            // AddComponent 都拿不到（异常环境）→ fail closed：报出来、中止 Build，绝不带病继续
            Debug.LogError("[DraftRewardDetailUI] Canvas 挂不上，奖励详情弹窗不可用");
            return;
        }
        // 【2026-10-14 主人拍板「以后的界面都要走摄像机模式」】套用全项目统一规范：
        // Screen Space - Camera + 720×1280 竖版 + 根铺满 + 场景切换自动重绑相机（DDOL 已有钩子）。
        // 原来的裸 Overlay（不随主相机、恒盖在最上）就是「整个糊住屏幕」的根源之一。
        UICanvasSetup.ApplyPopup(canvas, 1000);   // 与开奖卡同层级：压在所有战斗层之上
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        // 暗带：点任意位置关闭（两个入口都盖满）
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().onClick.AddListener(CloseRoot);

        // 面板：居中，自带 Button 让「点面板任意处」也能关
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(CardW, CardH);
        var pbg = panel.GetComponent<Image>();
        pbg.color = new Color(0.10f, 0.10f, 0.13f, 0.96f);
        panel.GetComponent<Button>().onClick.AddListener(CloseRoot);

        // 稀有度描边
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panel.transform, false);
        Stretch(frame.GetComponent<RectTransform>());
        _frame = frame.GetComponent<Image>();
        _frame.color = RarityPalette.Common;

        // 类型行（顶）
        _kindText = MakeText(panel.transform, "Kind", "", 20);
        Anchor(_kindText.rectTransform, new Vector2(0.06f, 0.925f), new Vector2(0.94f, 0.985f));

        // 名字（大）
        _titleText = MakeText(panel.transform, "Title", "", 32);
        _titleText.fontStyle = FontStyle.Bold;
        Anchor(_titleText.rectTransform, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.925f));

        // meta 行（稀有度 · ★N）
        _metaText = MakeText(panel.transform, "Meta", "", 20);
        Anchor(_metaText.rectTransform, new Vector2(0.06f, 0.775f), new Vector2(0.94f, 0.845f));

        // 图标（左）+ 描述（右）
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(panel.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.06f, 0.53f);
        irt.anchorMax = new Vector2(0.06f, 0.53f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(0f, 0f);
        irt.sizeDelta = new Vector2(120f, 150f);
        _icon = iconGo.GetComponent<Image>();
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        _bodyText = MakeText(panel.transform, "Body", "", 18);
        _bodyText.alignment = TextAnchor.UpperLeft;
        Anchor(_bodyText.rectTransform, new Vector2(0.28f, 0.50f), new Vector2(0.94f, 0.75f));

        // 参数行（富文本：绿升红降）
        _paramsText = MakeText(panel.transform, "Params", "", 18);
        _paramsText.alignment = TextAnchor.UpperLeft;
        Anchor(_paramsText.rectTransform, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.50f));

        // 底部提示
        _hintText = MakeText(panel.transform, "Hint", "点任意位置关闭", 15);
        _hintText.color = new Color(1f, 1f, 1f, 0.45f);
        Anchor(_hintText.rectTransform, new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.095f));

        _root.SetActive(false);
    }

    // ============================================================
    // 内容填充
    // ============================================================

    void Fill(DraftCard card)
    {
        Color tint = RarityPalette.Get(card.Rarity);
        _frame.color = tint;
        _kindText.color = Color.Lerp(tint, Color.white, 0.45f);
        _titleText.color = Color.white;
        _metaText.color = tint;
        _icon.gameObject.SetActive(false);

        string body = "";
        Sprite icon = null;
        string paramsText = "";

        switch (card.Kind)
        {
            case DraftCardKind.SkillNew:
                _kindText.text = "获得技能";
                _metaText.text = RarityLabel(card.Rarity) + " · ★" + Mathf.Max(1, card.Star);
                body = SkillDescOf(card.Id);
                icon = SkillIconOf(card.Id);
                paramsText = SkillParamLines(card.Id, -1);
                break;

            case DraftCardKind.SkillUp:
            {
                _kindText.text = "技能升星";
                int newStar = Mathf.Max(1, card.Star);
                _metaText.text = RarityLabel(card.Rarity) + " · ★" + (newStar - 1) + " → ★" + newStar;
                body = SkillDescOf(card.Id);
                icon = SkillIconOf(card.Id);
                paramsText = SkillParamLines(card.Id, newStar - 1);
                break;
            }

            case DraftCardKind.MercRecruit:
                _kindText.text = "佣兵入队";
                _metaText.text = MercMetaOf(card);
                body = MercDescOf(card);
                icon = MercStandOf(card);
                paramsText = MercParamLines(card, false);
                break;

            case DraftCardKind.MercLevelUp:
                _kindText.text = "佣兵升级";
                _metaText.text = "Lv" + Mathf.Max(1, card.MercLevel - 1) + " → Lv" + Mathf.Max(1, card.MercLevel);
                body = MercDescOf(card);
                icon = MercStandOf(card);
                paramsText = MercParamLines(card, true);
                break;

            case DraftCardKind.MercStarUp:
                _kindText.text = "佣兵升星";
                _metaText.text = "★" + Mathf.Max(1, card.Star - 1) + " → ★" + Mathf.Max(1, card.Star)
                    + "（Lv 同时 +1）";
                body = MercDescOf(card);
                icon = MercStandOf(card);
                paramsText = MercParamLines(card, true);
                break;

            case DraftCardKind.Equip:
                _kindText.text = "获得装备";
                _metaText.text = RarityLabel(card.Rarity) + " · ★" + Mathf.Max(1, card.Star);
                body = string.IsNullOrEmpty(card.Desc) ? "本局装备，撤离不带出。" : card.Desc;
                icon = card.Equip != null ? EquipIcons.Resolve(card.Equip) : null;
                paramsText = EquipParamLines(card);
                break;
        }

        _titleText.text = string.IsNullOrEmpty(card.Title) ? "获得奖励" : card.Title;
        _bodyText.text = body ?? "";
        _paramsText.text = paramsText ?? "";
        if (icon != null)
        {
            _icon.sprite = icon;
            _icon.color = Color.white;
            _icon.gameObject.SetActive(true);
        }
    }

    // ============================================================
    // 技能参数（与 BuildRunSkill 同源算式；oldStar &lt; 0 = 只展示当前值）
    // ============================================================

    static string SkillDescOf(string skillId)
    {
        var def = PlayerSkillDefs.GetById(skillId);
        if (def == null) return "";
        return string.IsNullOrEmpty(def.desc) ? def.numbers : def.desc;
    }

    static Sprite SkillIconOf(string skillId)
    {
        var sk = RunDraftDirector.BuildRunSkill(skillId, PlayerJobDefs.GetSelected());
        return sk != null ? sk.icon : null;
    }

    /// <summary>
    /// 技能参数行。<paramref name="oldStar"/> ≥ 0 = 前后对比（绿升红降）；&lt; 0 = 单值。
    /// 新值走 <see cref="RunDraftDirector.BuildRunSkill"/>（发奖后存档★已是新★，即真实生效值）；
    /// 旧值用同源算式按 oldStar 复刻（含 Theme / 亲和，保证差值只是星级差）。
    /// </summary>
    static string SkillParamLines(string skillId, int oldStar)
    {
        var job = PlayerJobDefs.GetSelected();
        var now = RunDraftDirector.BuildRunSkill(skillId, job);
        if (now == null) return "参数读取失败";
        var before = oldStar >= 0 ? StarSnapshot(skillId, oldStar, job) : null;
        var sb = new StringBuilder();
        AppendSkillParams(sb, now, before);
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>照 <see cref="RunDraftDirector.BuildRunSkill"/> 的算式按指定★复刻一份参数（原型是副本，不改存档）。</summary>
    static SkillSystem.ActiveSkill StarSnapshot(string skillId, int star, PlayerJobId job)
    {
        var proto = SkillRegistry.Instance != null ? SkillRegistry.Instance.GetActiveSkill(skillId) : null;
        if (proto == null) return null;
        var active = new SkillSystem.ActiveSkill
        {
            skillId = proto.skillId,
            skillName = proto.skillName,
            baseDamage = proto.baseDamage,
            damageMultiplier = proto.damageMultiplier,
            cooldown = proto.cooldown,
            skillType = proto.skillType,
            projectileCount = proto.projectileCount,
            projectileSpeed = proto.projectileSpeed,
            aoeRadius = proto.aoeRadius,
            aoeMaxTargets = proto.aoeMaxTargets,
            starDamageStep = proto.starDamageStep,
            icon = proto.icon,
            healBase = proto.healBase,
            healPercentOfMax = proto.healPercentOfMax,
            healAtkMul = proto.healAtkMul,
            buffAttr = proto.buffAttr,
            buffValue = proto.buffValue,
            buffIsPercent = proto.buffIsPercent,
            duration = proto.duration,
        };
        var tag = SkillDraftMeta.Tag(skillId);
        float dmgMul = SkillDraftMeta.StarDamageMul(star, active.starDamageStep) * RunLoadout.ThemeDamageMul(tag);
        float cdMul = SkillDraftMeta.StarCooldownMul(star);
        if (SkillDraftMeta.IsAffinity(skillId, job))
        {
            dmgMul *= SkillDraftMeta.AffinityDamageMul;
            cdMul *= SkillDraftMeta.AffinityCooldownMul;
        }
        active.damageMultiplier *= dmgMul;
        active.baseDamage *= dmgMul;
        active.cooldown = Mathf.Max(0.6f, active.cooldown * cdMul);
        if (active.aoeMaxTargets > 0)
            active.aoeMaxTargets += Mathf.Max(0, star - 1);
        float buffMul = SkillDraftMeta.StarBuffMul(star) * RunLoadout.ThemeDamageMul(tag);
        if (SkillDraftMeta.IsAffinity(skillId, job))
            buffMul *= SkillDraftMeta.AffinityDamageMul;
        active.healBase *= buffMul;
        active.healPercentOfMax *= buffMul;
        active.healAtkMul *= buffMul;
        active.buffValue *= buffMul;
        if (active.duration > 0f)
            active.duration *= SkillDraftMeta.StarDurationMul(star);
        return active;
    }

    static void AppendSkillParams(StringBuilder sb, SkillSystem.ActiveSkill now, SkillSystem.ActiveSkill before)
    {
        // 伤害倍率 / 基础伤害
        if (now.damageMultiplier > 0f)
        {
            string label = "伤害倍率";
            string nowV = "×" + now.damageMultiplier.ToString("0.##");
            sb.AppendLine(CompareLine(label, nowV, before != null ? "×" + before.damageMultiplier.ToString("0.##") : null,
                now.damageMultiplier, before != null ? before.damageMultiplier : 0f, before != null));
        }
        if (now.baseDamage > 0f)
        {
            string nowV = Mathf.RoundToInt(now.baseDamage).ToString();
            string oldV = before != null ? Mathf.RoundToInt(before.baseDamage).ToString() : null;
            sb.AppendLine(CompareLine("基础伤害", nowV, oldV, now.baseDamage, before != null ? before.baseDamage : 0f, before != null));
        }
        // 冷却
        if (now.cooldown > 0f)
        {
            string nowV = now.cooldown.ToString("0.#") + " 秒";
            string oldV = before != null ? before.cooldown.ToString("0.#") + " 秒" : null;
            sb.AppendLine(CompareLine("冷却", nowV, oldV, -now.cooldown, before != null ? -before.cooldown : 0f, before != null));
        }
        // 治疗三段
        if (now.healBase > 0f)
        {
            string nowV = Mathf.RoundToInt(now.healBase).ToString();
            string oldV = before != null ? Mathf.RoundToInt(before.healBase).ToString() : null;
            sb.AppendLine(CompareLine("治疗", nowV, oldV, now.healBase, before != null ? before.healBase : 0f, before != null));
        }
        if (now.healPercentOfMax > 0f)
        {
            string nowV = (now.healPercentOfMax * 100f).ToString("0.#") + "% 生命";
            string oldV = before != null ? (before.healPercentOfMax * 100f).ToString("0.#") + "% 生命" : null;
            sb.AppendLine(CompareLine("治疗", nowV, oldV, now.healPercentOfMax, before != null ? before.healPercentOfMax : 0f, before != null));
        }
        if (now.healAtkMul > 0f)
        {
            string nowV = "攻击 ×" + now.healAtkMul.ToString("0.##");
            string oldV = before != null ? "攻击 ×" + before.healAtkMul.ToString("0.##") : null;
            sb.AppendLine(CompareLine("治疗", nowV, oldV, now.healAtkMul, before != null ? before.healAtkMul : 0f, before != null));
        }
        // 增益
        if (now.buffValue > 0f)
        {
            string attrName = EquipUiText.Attr(now.buffAttr);
            string nowV = now.buffIsPercent
                ? (now.buffValue * 100f).ToString("0.#") + "%"
                : now.buffValue.ToString("0.##");
            string oldV = before != null
                ? (before.buffIsPercent ? (before.buffValue * 100f).ToString("0.#") + "%" : before.buffValue.ToString("0.##"))
                : null;
            sb.AppendLine(CompareLine("增益 " + attrName, nowV, oldV, now.buffValue, before != null ? before.buffValue : 0f, before != null));
        }
        // 持续
        if (now.duration > 0f)
        {
            string nowV = now.duration.ToString("0.#") + " 秒";
            string oldV = before != null ? before.duration.ToString("0.#") + " 秒" : null;
            sb.AppendLine(CompareLine("持续", nowV, oldV, now.duration, before != null ? before.duration : 0f, before != null));
        }
        // 范围 / 命中数
        if (now.aoeRadius > 0f)
        {
            string nowV = now.aoeRadius.ToString("0.#") + " 米";
            string oldV = before != null ? before.aoeRadius.ToString("0.#") + " 米" : null;
            sb.AppendLine(CompareLine("范围", nowV, oldV, now.aoeRadius, before != null ? before.aoeRadius : 0f, before != null));
        }
        if (now.aoeMaxTargets > 0)
        {
            string nowV = now.aoeMaxTargets + " 个";
            string oldV = before != null ? before.aoeMaxTargets + " 个" : null;
            sb.AppendLine(CompareLine("最多命中", nowV, oldV, now.aoeMaxTargets, before != null ? before.aoeMaxTargets : 0f, before != null));
        }
        // 弹幕
        if (now.projectileCount > 1)
        {
            string nowV = now.projectileCount + " 发";
            string oldV = before != null ? before.projectileCount + " 发" : null;
            sb.AppendLine(CompareLine("弹幕", nowV, oldV, now.projectileCount, before != null ? before.projectileCount : 0f, before != null));
        }
    }

    /// <summary>一行参数：单值 = 「名 值」；对比 = 「名 旧 → 新」（升绿降红，主人拍板游戏通用配色；持平不套色）。</summary>
    static string CompareLine(string label, string nowV, string oldV, float nowNum, float oldNum, bool compare)
    {
        if (!compare) return label + "　" + nowV;
        if (Mathf.Approximately(nowNum, oldNum)) return label + "　" + nowV;
        string color = nowNum > oldNum
            ? ColorUtility.ToHtmlStringRGB(RarityPalette.PowerGain)
            : ColorUtility.ToHtmlStringRGB(RarityPalette.PowerLoss);
        return label + "　" + oldV + " → <color=#" + color + ">" + nowV + "</color>";
    }

    // ============================================================
    // 佣兵参数
    // ============================================================

    static string MercMetaOf(DraftCard card)
    {
        if (!ResolveMercDef(card, out var def))
            return "佣兵";
        return def.JobName + " · " + SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(def.Rarity))
            + " · ★" + Mathf.Max(1, card.Star);
    }

    /// <summary>hireId / AssetId 都能解析到花名册定义（旧档兜底）。</summary>
    static bool ResolveMercDef(DraftCard card, out MercRosterDefs.Def def)
    {
        string key = MercKeyOf(card);
        if (MercRosterDefs.TryGetByHireId(key, out def)) return true;
        if (!string.IsNullOrEmpty(card.Id) && MercRosterDefs.TryGetByAssetId(card.Id, out def)) return true;
        return false;
    }

    static string MercDescOf(DraftCard card)
    {
        // 图鉴条目按 catalogId 找：hireId → AssetId → catalogId 三级解析
        string catalogId = null;
        if (MercRosterDefs.TryGetByHireId(MercKeyOf(card), out var def) && !string.IsNullOrEmpty(def.AssetId))
            catalogId = AdventureCodex.ResolveMercId(def.AssetId);
        else if (!string.IsNullOrEmpty(card.Id))
            catalogId = AdventureCodex.ResolveMercId(card.Id);
        if (!string.IsNullOrEmpty(catalogId) && AdventureLogCatalog.TryFindMerc(catalogId, out var entry)
            && !string.IsNullOrEmpty(entry.Desc))
            return entry.Desc;
        return "新伙伴加入小队，并肩作战。";
    }

    static Sprite MercStandOf(DraftCard card)
    {
        return MercPortraitSprites.GetStand(MercKeyOf(card));
    }

    /// <summary>佣兵卡的 HireId / AssetId 兜底键（卡面两种填法都有过）。</summary>
    static string MercKeyOf(DraftCard card)
    {
        return !string.IsNullOrEmpty(card.HireId) ? card.HireId : card.Id;
    }

    /// <summary>
    /// 佣兵参数行。生命 / 攻击 / 防御走 <see cref="MercRosterDefs.ApplyCombatStats"/>（只随等级），
    /// 蓝池 / 回蓝走 <see cref="MpProfile"/>（随星级）；升星时属性不变就只列变化的项。
    /// </summary>
    static string MercParamLines(DraftCard card, bool compare)
    {
        if (!ResolveMercDef(card, out var def))
            return "";
        int newLv = Mathf.Max(1, card.MercLevel);
        int oldLv = Mathf.Max(1, card.MercLevel - 1);
        int newStar = Mathf.Max(1, card.Star);
        // 只有「升星」拍才动★；纯升级拍★不变（旧值按同★算，持平不套色）
        int oldStar = card.Kind == DraftCardKind.MercStarUp ? Mathf.Max(1, card.Star - 1) : newStar;

        MercRosterDefs.ApplyCombatStats(def.AssetId, newLv, out float hp, out float atk, out float def0, out _, out _, out _);
        var sb = new StringBuilder();
        if (compare)
        {
            MercRosterDefs.ApplyCombatStats(def.AssetId, oldLv, out float ohp, out float oatk, out float odef, out _, out _, out _);
            sb.AppendLine(CompareLine("生命", Mathf.RoundToInt(hp).ToString(), Mathf.RoundToInt(ohp).ToString(), hp, ohp, true));
            sb.AppendLine(CompareLine("攻击", Mathf.RoundToInt(atk).ToString(), Mathf.RoundToInt(oatk).ToString(), atk, oatk, true));
            sb.AppendLine(CompareLine("防御", Mathf.RoundToInt(def0).ToString(), Mathf.RoundToInt(odef).ToString(), def0, odef, true));
        }
        else
        {
            sb.AppendLine("生命　" + Mathf.RoundToInt(hp));
            sb.AppendLine("攻击　" + Mathf.RoundToInt(atk));
            sb.AppendLine("防御　" + Mathf.RoundToInt(def0));
        }

        // 蓝条（仅治疗 / 法术型技能有）
        var arch = MpProfile.OfMercSkill(def.ActiveSkillId);
        if (arch == MpArchetype.Heal || arch == MpArchetype.Magic)
        {
            float pool = MpProfile.Pool(arch, newLv, newStar);
            float regen = MpProfile.Regen(arch, newStar);
            if (compare)
            {
                float opool = MpProfile.Pool(arch, oldLv, oldStar);
                float oregen = MpProfile.Regen(arch, oldStar);
                sb.AppendLine(CompareLine("蓝池", Mathf.RoundToInt(pool).ToString(), Mathf.RoundToInt(opool).ToString(), pool, opool, true));
                sb.AppendLine(CompareLine("每秒回蓝", regen.ToString("0.##"), oregen.ToString("0.##"), regen, oregen, true));
            }
            else
            {
                sb.AppendLine("蓝池　" + Mathf.RoundToInt(pool));
                sb.AppendLine("每秒回蓝　" + regen.ToString("0.##"));
            }
        }

        if (card.PowerDelta > 0)
        {
            sb.AppendLine("<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.PowerGain)
                + ">战力 +" + card.PowerDelta + "</color>");
        }
        return sb.ToString().TrimEnd('\n');
    }

    // ============================================================
    // 装备参数（直接借抽卡池的属性行，自带 ▲▼ 对比格式）
    // ============================================================

    static string EquipParamLines(DraftCard card)
    {
        if (card.Equip == null) return "";
        var lines = DraftPool.FormatEquipDescLines(card.Equip);
        if (lines == null || lines.Count == 0) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine(lines[i]);
        if (card.PowerDelta > 0)
        {
            sb.AppendLine("<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.PowerGain)
                + ">战力 +" + card.PowerDelta + "</color>");
        }
        return sb.ToString().TrimEnd('\n');
    }

    // ============================================================
    // 构件
    // ============================================================

    static string RarityLabel(SkillRarity r)
    {
        return SkillRarityUtil.DisplayName(r);
    }

    static Text MakeText(Transform parent, string name, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
